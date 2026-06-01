#if DEBUG
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top;

namespace AnoMech.Windows;

// DEBUG-only visual debugger for DamageSolver.Resolve. Renders a top-down map of
// the arena; every Resolve re-runs the SAME CharacterFind.InsideActionAoe query
// against a fixed grid of virtual points (instead of party members) and records
// the covered cells as one hit instance. Each instance lives FadeSeconds on its
// OWN timer (re-hitting a cell adds a new instance, it does not refresh an old
// one), and a cell's brightness reflects how many of its instances are still
// alive. The user can freeze the image (no paint, no fade).
//
// DamageSolver.Resolve reaches us through the static Instance (set in the ctor,
// constructed only in DEBUG builds from Plugin). The grid and pixel buffers are
// built lazily on first open so a never-opened window costs nothing.
internal sealed class DamageDebugWindow : Window, IDisposable
{
    internal static DamageDebugWindow? Instance;

    private const int Res = 300;            // grid + texture resolution (px)
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
    private Vector3? lastSource;
    private float sourceRemaining;
    private IDalamudTextureWrap? tex;

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

    // Called from DamageSolver.Resolve with the same (actionId, source) that drives
    // the real party query, so the picture matches the resolved AOE exactly. Runs on
    // the framework thread; `events` is read back in Draw on the same (main) thread.
    internal void Record(uint actionId, Placement source)
    {
        if (!IsOpen || frozen) return;
        EnsureGrid();
        var hits = gridFind!.InsideActionAoe(actionId, source);
        if (hits.Count > 0)
        {
            // Within one query a cell appears at most once (single-pass shapes don't
            // repeat; star/cross Distincts), so an event never double-counts a cell.
            var cells = new int[hits.Count];
            for (var i = 0; i < hits.Count; i++) cells[i] = hits[i].Iz * Res + hits[i].Ix;
            events.Add(new HitEvent(cells));
        }
        lastSource = source.Position;
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

        var origin = ImGui.GetCursorScreenPos();
        if (tex != null) ImGui.Image(tex.Handle, new Vector2(Res, Res));
        else ImGui.Dummy(new Vector2(Res, Res));

        var dl = ImGui.GetWindowDrawList();
        Vector2 ToCanvas(Vector3 l) => origin + new Vector2(
            (l.X + HalfExtent) / (2f * HalfExtent) * Res,
            (l.Z + HalfExtent) / (2f * HalfExtent) * Res);

        // Arena ring at the 20y TOP radius for orientation.
        var ring = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f));
        dl.AddCircle(ToCanvas(Vector3.Zero), TopConstants.Geometry.ArenaRadius / HalfExtent * (Res / 2f), ring);

        // Live party members (reuse Find to enumerate without a new public API).
        var dot = ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 1f, 1f));
        foreach (var m in plugin.Game.World.Party.Find.InsideCircle(Vector3.Zero, 1000f))
            dl.AddCircleFilled(ToCanvas(m.Position), 3f, dot);

        // Most-recent AOE source.
        if (sourceRemaining > 0f && lastSource is { } s)
            dl.AddCircleFilled(ToCanvas(s), 4f, ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 1f)));
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
