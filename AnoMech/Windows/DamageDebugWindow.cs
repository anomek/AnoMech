#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top;

namespace AnoMech.Windows;

// DEBUG-only visual debugger for EnemyActionHandler's AoE queries. Renders a top-down map of
// the arena; every query re-runs the SAME CharacterFind.InsideActionAoe query
// against a fixed grid of virtual points (instead of party members) and records
// the covered cells as one hit instance. Each instance lives FadeSeconds on its
// OWN timer (re-hitting a cell adds a new instance, it does not refresh an old
// one), and a cell's brightness reflects how many of its instances are still
// alive. The user can freeze the image (no paint, no fade).
//
// EnemyActionHandler reaches us through the static Instance (set in the ctor,
// constructed only in DEBUG builds from Plugin). The grid and pixel buffers are
// built lazily on first open so a never-opened window costs nothing.
internal sealed class DamageDebugWindow : Window, IDisposable
{
    internal static DamageDebugWindow? Instance;

    private const int Res = 510;            // grid + texture resolution (px)
    private const float HalfExtent = 25f;   // half-width of mapped region (yalms); > arena so edge AOEs show
    private const float FadeSeconds = 3f;
    private const float HeatCap = 4f;       // max stacked intensity per cell

    private readonly Plugin plugin;

    // Virtual grid, built lazily. gridFind reuses InsideActionAoe verbatim.
    private List<GridCell>? grid;
    private CharacterFind<GridCell>? gridFind;

    // Each Resolve becomes one HitEvent with its own countdown. Cell index is
    // c = iz * Res + ix (row-major, row 0 = north / -Z).
    private readonly List<HitEvent> events = [];
    private float[]? intensity;  // scratch: summed live contribution per cell
    private byte[]? pixels;      // RGBA upload buffer

    private bool frozen;
    private bool freezeRebuildPending;        // one-shot: bake the final hit into the frozen tex
    private List<Vector3>? frozenPositions;   // party-dot snapshot held while frozen
    private Vector3? lastSource;
    private float sourceRemaining;
    private IDalamudTextureWrap? tex;

    // Captured regardless of IsOpen/frozen, so Freeze()'s dump works without the window ever
    // having been opened.
    private AoeQuery? lastRecordedQuery;

    public DamageDebugWindow(Plugin plugin)
        : base("Damage Debug##AnoMechDamageDebug")
    {
        this.plugin = plugin;
        IsOpen = false;
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        Instance = this;
    }

    public void Dispose()
    {
        tex?.Dispose();
        tex = null;
        if (Instance == this) Instance = null;
    }

    // Called from EnemyActionHandler with the SAME AoeQuery that drives the real
    // party query, so the picture matches the resolved AOE exactly (it's literally
    // re-run against the grid). Runs on the framework thread; `events` is read back in
    // Draw on the same (main) thread.
    internal void Record(AoeQuery query)
    {
        lastRecordedQuery = query;
        if (!IsOpen || frozen) return;
        EnsureGrid();
        var hits = query.Run(gridFind!);
        if (hits.Count > 0)
        {
            // Within one query a cell appears at most once (single-pass shapes don't
            // repeat; star/cross Distincts), so an event never double-counts a cell.
            var cells = new int[hits.Count];
            for (var i = 0; i < hits.Count; i++) cells[i] = hits[i].Iz * Res + hits[i].Ix;
            events.Add(new HitEvent(cells));
        }
        lastSource = query.Source.Position;
        sourceRemaining = FadeSeconds;
    }

    public override void Draw()
    {
        ImGui.Checkbox("Freeze", ref frozen);
        ImGui.SameLine();
        if (ImGui.Button("Clear")) Clear();
        ImGui.SameLine();
        ImGui.TextDisabled($"hits fade over {FadeSeconds:0}s");

        // Advance each hit's own countdown (skipped while frozen) and drop expired
        // ones. Rebuild every frame a hit is alive (continuous fade) plus the single
        // frame the list empties (to clear the canvas).
        if (gridFind != null && !frozen)
        {
            var dt = ImGui.GetIO().DeltaTime;
            var changed = false;
            for (var i = events.Count - 1; i >= 0; i--)
            {
                events[i].Remaining -= dt;
                if (events[i].Remaining <= 0f) { events.RemoveAt(i); changed = true; }
            }
            if (sourceRemaining > 0f) sourceRemaining -= dt;
            if (events.Count > 0 || changed) RebuildTexture();
        }

        // The lethal AOE is appended to `events` in the same frame Freeze() fires, but the
        // fade/rebuild block above is gated on !frozen — so the snapshot would otherwise
        // omit the very hit that caused death. Rebuild once on the freeze transition.
        if (freezeRebuildPending && gridFind != null)
        {
            freezeRebuildPending = false;
            RebuildTexture();
        }

        var uiScale = ImGuiHelpers.GlobalScale;
        var canvasSize = Res * uiScale;
        var origin = ImGui.GetCursorScreenPos();
        if (tex != null) ImGui.Image(tex.Handle, new Vector2(canvasSize, canvasSize));
        else ImGui.Dummy(new Vector2(canvasSize, canvasSize));

        var dl = ImGui.GetWindowDrawList();
        Vector2 ToCanvas(Vector3 l) => origin + new Vector2(
            (l.X + HalfExtent) / (2f * HalfExtent) * canvasSize,
            (l.Z + HalfExtent) / (2f * HalfExtent) * canvasSize);

        // Arena ring at the 20y TOP radius for orientation.
        var ring = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f));
        dl.AddCircle(ToCanvas(Vector3.Zero), TopConstants.Geometry.ArenaRadius / HalfExtent * (canvasSize / 2f), ring);

        // Party members: while frozen, show positions snapshotted at the freeze so the
        // dots stay put with the frozen heatmap; live otherwise.
        var dot = ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 1f, 1f));
        if (frozen)
        {
            frozenPositions ??= CapturePartyPositions();
            foreach (var p in frozenPositions) dl.AddCircleFilled(ToCanvas(p), 3f * uiScale, dot);
        }
        else
        {
            frozenPositions = null;
            foreach (var m in plugin.Game.World.Party.AllMembers())
                dl.AddCircleFilled(ToCanvas(m.Position), 3f * uiScale, dot);
        }

        // Most-recent AOE source.
        if (sourceRemaining > 0f && lastSource is { } s)
            dl.AddCircleFilled(ToCanvas(s), 4f * uiScale, ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 1f)));
    }

    // Auto-freeze hook for the wipe sequence (Game.Kill). Snapshots the heatmap as-is
    // so the killing AOE — recorded moments earlier in the same Resolve — stays visible,
    // and dumps that query whether or not the window was ever opened.
    internal void Freeze()
    {
        frozen = true;
        freezeRebuildPending = true;
        DumpToFile();
    }

    // A party/AOE state snapshot into DiagnosticLog; the last query is re-run against every
    // party member (dead ones included) to show who was inside the shape. Also called
    // periodically by Game.Tick.
    internal void DumpToFile()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AnoMech damage debug dump -- {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            CharacterFind<SimCharacter>? hitFind = null;
            AoeQuery? query = lastRecordedQuery;
            if (query is { } q)
            {
                var name = ActionLookup.Name(q.ActionId);
                var actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
                sb.AppendLine($"Most recent AOE -- Action: {q.ActionId} (0x{q.ActionId:X}) \"{name}\"");
                if (actionSheet.TryGetRow(q.ActionId, out var action))
                    sb.AppendLine($"  CastType={action.CastType}  EffectRange={action.EffectRange}  XAxisModifier={action.XAxisModifier}");
                else
                    sb.AppendLine("  (row not found in Action sheet)");
                sb.AppendLine($"  OmenRotate={q.OmenRotate:F3}  SizeOverride={(q.Size is { } sz ? sz.ToString("F3") : "(none)")}  CastTypeOverride={(q.CastTypeOverride is { } ct ? ct.ToString() : "(none)")}");
                sb.AppendLine();

                var src = q.Source;
                sb.AppendLine($"Source position: ({src.Position.X:F2}, {src.Position.Y:F2}, {src.Position.Z:F2})  Rotation: {src.Rotation:F3} rad (forward = (sin, cos) of this)");
                sb.AppendLine();
                hitFind = new CharacterFind<SimCharacter>(() => plugin.Game.World.Party.AllMembers());
            }
            else
            {
                sb.AppendLine("(no AOE resolved yet this run)");
                sb.AppendLine();
            }

            var hits = query is { } qq && hitFind is { } hf ? qq.Run(hf) : [];
            sb.AppendLine("Party members at this moment:");
            foreach (var member in plugin.Game.World.Party.AllMembers())
            {
                var role = (member as ISimPartyMember)?.Role.ToString() ?? member.GetType().Name;
                var alive = member.IsAlive() ? "ALIVE" : "DEAD ";
                var hitLabel = query is null ? "  -  " : hits.Contains(member) ? "HIT  " : "clear";
                var distLabel = query is { } q2
                    ? $"  dist from source={MathF.Sqrt(MathF.Pow(member.Position.X - q2.Source.Position.X, 2) + MathF.Pow(member.Position.Z - q2.Source.Position.Z, 2)):F2}"
                    : "";
                var statuses = member.ActiveStatusSnapshot;
                var statusLabel = statuses.Count == 0
                    ? ""
                    : "  statuses=[" + string.Join(", ", statuses.Select(s => s.Stacks > 1 ? $"{s.StatusId}x{s.Stacks}" : $"{s.StatusId}")) + "]";
                // Who drives this slot on this client, for comparing a host dump with a guest's.
                var driver = member switch
                {
                    SimPlayer => DebugBotControl.Enabled ? "YOU(bot)" : "YOU",
                    SimNetworkPuppet => "PEER",
                    _ => "BOT",
                };
                sb.AppendLine($"  {role,-14} [{driver,-8}] {alive}  {hitLabel}  pos=({member.Position.X:F2}, {member.Position.Z:F2}){distLabel}{statusLabel}");
            }
            sb.AppendLine();

            sb.AppendLine("Enemies currently in world:");
            var enemies = plugin.Game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive).ToList();
            if (enemies.Count == 0)
                sb.AppendLine("  (none)");
            else
                foreach (var enemy in enemies)
                    sb.AppendLine($"  {enemy.DisplayName,-20} pos=({enemy.Position.X:F2}, {enemy.Position.Z:F2})  rot={enemy.Rotation:F3}");
            sb.AppendLine();

            sb.AppendLine("Tethers currently active:");
            var tethers = plugin.Game.World.Children.OfType<SimTether>().Where(t => t.IsActive).ToList();
            if (tethers.Count == 0)
                sb.AppendLine("  (none)");
            else
                foreach (var t in tethers)
                {
                    var aLabel = t.A is { } a ? DescribeParticipant(a) : "(none)";
                    var bLabel = t.B is { } b ? DescribeParticipant(b) : "(none)";
                    sb.AppendLine($"  TetherId={t.TetherId}  {aLabel} -> {bLabel}");
                }
            sb.AppendLine();

            // Into the diagnostic log rather than a second file, so a bug report is one file.
            AnoMech.Core.DiagnosticLog.LogSnapshot("Damage Debug Snapshot", sb.ToString());
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[DamageDebugWindow] Failed to write damage debug dump: {ex.Message}");
        }
    }

    // Cleared at the start of each run so a new scenario records from a blank, live map.
    internal void ResetFreeze() { frozen = false; freezeRebuildPending = false; Clear(); }

    // Labels a tether endpoint as its party role if it's a party member, else its
    // enemy display name -- tethers can end on either.
    private static string DescribeParticipant(SimCharacter c) =>
        (c as ISimPartyMember)?.Role.ToString() ?? (c as SimEnemy)?.DisplayName ?? c.GetType().Name;

    // Snapshot of every party dot's position (alive or dead), taken on the first
    // frozen frame — AllMembers, not Find, so the just-killed member is included.
    private List<Vector3> CapturePartyPositions()
    {
        var list = new List<Vector3>();
        foreach (var m in plugin.Game.World.Party.AllMembers())
            list.Add(m.Position);
        return list;
    }

    private void Clear()
    {
        events.Clear();
        lastSource = null;
        sourceRemaining = 0f;
        if (gridFind != null) RebuildTexture();
    }

    private void RebuildTexture()
    {
        // Sum each live hit's faded contribution (Remaining/FadeSeconds, 1→0 over its
        // own lifetime) onto the cells it covers. Overlapping live hits add up.
        Array.Clear(intensity!);
        foreach (var e in events)
        {
            var f = e.Remaining / FadeSeconds;
            foreach (var c in e.Cells) intensity![c] += f;
        }

        for (var c = 0; c < intensity!.Length; c++)
        {
            var o = c * 4;
            var v = intensity[c];
            if (v <= 0f)
            {
                pixels![o] = pixels[o + 1] = pixels[o + 2] = pixels[o + 3] = 0;
                continue;
            }
            // Heatmap: red (1 live hit) → yellow → white (HeatCap live hits). Alpha
            // holds until the cell's last hit fades out.
            var t = MathF.Min(1f, v / HeatCap);
            pixels![o] = 255;
            pixels[o + 1] = (byte)(t * 255f);
            pixels[o + 2] = (byte)(MathF.Max(0f, (t - 0.5f) * 2f) * 255f);
            pixels[o + 3] = (byte)(MathF.Min(1f, v) * 255f);
        }
        tex?.Dispose();
        tex = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(Res, Res), pixels, "AnoMech.DamageDebug");
    }

    private void EnsureGrid()
    {
        if (gridFind != null) return;
        grid = new List<GridCell>(Res * Res);
        for (var iz = 0; iz < Res; iz++)
        for (var ix = 0; ix < Res; ix++)
        {
            var lx = -HalfExtent + (ix + 0.5f) / Res * 2f * HalfExtent;
            var lz = -HalfExtent + (iz + 0.5f) / Res * 2f * HalfExtent;
            grid.Add(new GridCell(new Vector3(lx, 0f, lz), ix, iz));
        }
        gridFind = new CharacterFind<GridCell>(grid);
        intensity = new float[Res * Res];
        pixels = new byte[Res * Res * 4];
    }

    // One Resolve's covered cells, with its own independent countdown.
    private sealed class HitEvent(int[] cells)
    {
        public readonly int[] Cells = cells;
        public float Remaining = FadeSeconds;
    }

    // A virtual sample point fed to CharacterFind in place of a party member.
    // Reference type so the star/cross InsideActionAoe path dedupes by identity.
    private sealed class GridCell(Vector3 position, int ix, int iz) : IPositioned
    {
        public Vector3 Position { get; } = position;
        public float Rotation => 0f;
        public int Ix { get; } = ix;
        public int Iz { get; } = iz;
    }
}
#endif
