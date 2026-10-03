#if DEBUG
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Windows;

// DEBUG-only developer tooling extracted from MainWindow: manual spawns, model /
// pose / cast mutators, status application, object-table dumps, map-effect and
// director replay, BGM test. Owned by MainWindow (constructed only in DEBUG
// builds) and drawn inline via DrawDebugContent / DrawSpeedControl. Holds the
// Plugin reference so the moved bodies keep their `plugin.Game.*` access; the
// Plugin.* services (Log, TargetManager, ObjectTable, ClientState) are static.
internal sealed unsafe class DebugMenu
{
    private readonly Plugin plugin;

    public DebugMenu(Plugin plugin)
    {
        this.plugin = plugin;
        // The position log samples off the framework tick, not this window's draw cadence.
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        positionLogActionEffectHook?.Disable();
        positionLogActionEffectHook?.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework) => TickPositionLog();

    private string debugBNpcBaseIdText = "15720";
    private string debugSpawnScaleText = "0";
    private string debugSpawnModeAttrFlagsText = "";
    private string debugTimelineIdText = "0x53C";
    private string debugModelStateText = "0x00";
    private string debugModeAttrFlagsText = "0x00";
    private string debugCastActionIdText = "0";
    private string debugCastAnimVariationText = "0";
    private string debugLockonIdText = "0";
    private string debugVfxPathText = "vfx/lockon/eff/lockon_en_01v.avfx";
    private string debugStatusIdText = "0";
    private string debugStatusDurationText = "0";
    private string debugStatusStacksText = "1";
    private string debugMapEffectIndexText = "0x00";
    private string debugMapEffectStateText = "0x0000";
    private string debugMapEffectTimelineText = "0x0000";
    private string debugDirectorCategoryText = "0x8000001E";
    private string debugDirectorArg1Text = "0x2AC";
    private string debugBgmIdText = "964";
    private float debugBgmSeekSeconds = 17.73f;
    private string debugWeatherIdText = "77";
    private float debugDayTimeSeconds = 43200f; // noon
    // EObj 1EB83C (decimal 2013244) = the TOP P5 Sigma falling-orb tower; useful default.
    private string debugEObjRowIdText = "2013244";

    // ── Position logger ──────────────────────────────────────────────────────
    // Ground-truth capture: every object table entry's live Position/Rotation to a CSV, every
    // real game tick, for reverse engineering a mechanic's movement curve by playing the
    // matching ARR replay back in-game. Every object is logged, since a capture shouldn't need
    // to know in advance which one matters. Two extra columns pin boundaries without live
    // annotation: the object's statuses (a status appearing or dropping is the boundary) and,
    // on "Hit" rows, the real action id and per-target effect.
    private bool positionLogActive;
    private StreamWriter? positionLogWriter;
    private DateTime positionLogStart;
    private string? positionLogPath;
    private Hook<ActionEffectHandler.Delegates.Receive>? positionLogActionEffectHook;

    private void TogglePositionLog()
    {
        if (positionLogActive)
        {
            positionLogActionEffectHook?.Disable();
            positionLogActionEffectHook?.Dispose();
            positionLogActionEffectHook = null;
            positionLogWriter?.Flush();
            positionLogWriter?.Dispose();
            positionLogWriter = null;
            positionLogActive = false;
            DiagnosticLog.Info($"[DebugMenu] Position log stopped: {positionLogPath}");
            // After the stop line, so it reaches disk.
            DiagnosticLog.ForcePersist = false;
            return;
        }

        try
        {
            var logDir = DiagnosticLog.LogDirectory;
            if (logDir == null) { DiagnosticLog.Warn("[DebugMenu] Disk logging is disabled -- can't start position log."); return; }
            var dir = System.IO.Path.Combine(logDir, "captures");
            Directory.CreateDirectory(dir);
            positionLogPath = System.IO.Path.Combine(dir, $"positions-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            positionLogWriter = new StreamWriter(positionLogPath) { AutoFlush = false };
            positionLogWriter.WriteLine("elapsed_s,wall_clock,object_id,kind,name,x,y,z,rotation,statuses,detail");
            positionLogStart = DateTime.Now;
            positionLogActive = true;
            DiagnosticLog.ForcePersist = true;
            // Installed only while a capture runs.
            positionLogActionEffectHook = Plugin.GameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                ActionEffectHandler.Addresses.Receive.Value, ActionEffectReceiveDetour);
            positionLogActionEffectHook.Enable();
            DiagnosticLog.Info($"[DebugMenu] Position log started: {positionLogPath}");
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[DebugMenu] Failed to start position log: {e.Message}");
            positionLogActive = false;
            DiagnosticLog.ForcePersist = false;
        }
    }

    private void TickPositionLog()
    {
        if (!positionLogActive || positionLogWriter == null) return;
        var elapsed = (DateTime.Now - positionLogStart).TotalSeconds;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj == null || obj.Address == IntPtr.Zero) continue;
            var name = obj.Name.TextValue.Replace(",", " ");
            var p = obj.Position;
            // IBattleChara is exactly "has a StatusManager"; the two ObjectKind enums collide by name.
            var statuses = obj is Dalamud.Game.ClientState.Objects.Types.IBattleChara
                ? StatusesOn((BattleChara*)obj.Address)
                : "";
            positionLogWriter.WriteLine(FormattableString.Invariant(
                $"{elapsed:F4},{DateTime.Now:O},0x{obj.GameObjectId:X8},{obj.ObjectKind},{name},{p.X:F5},{p.Y:F5},{p.Z:F5},{obj.Rotation:F5},{statuses},"));
        }
        // Flushed periodically; per-line flushing every tick would be needless I/O.
        if ((int)(elapsed * 10) % 10 == 0) positionLogWriter.Flush();
    }

    // id:remaining pairs from the native StatusManager, as one variable-length CSV column.
    private static string StatusesOn(BattleChara* bc)
    {
        if (bc == null) return "";
        var parts = new List<string>();
        foreach (var status in bc->StatusManager.Status)
            if (status.StatusId != 0)
                parts.Add(FormattableString.Invariant($"{status.StatusId}:{status.RemainingTime:F2}"));
        return string.Join('|', parts);
    }

    // Read-only tap on ActionEffectHandler.Receive: one "Hit" row per target, with the action id
    // and that target's effect type/value; position columns blank.
    private void ActionEffectReceiveDetour(uint casterEntityId, Character* casterPtr, Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
    {
        positionLogActionEffectHook!.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);
        if (!positionLogActive || positionLogWriter == null) return;
        try
        {
            var elapsed = (DateTime.Now - positionLogStart).TotalSeconds;
            var numTargets = Math.Min(header->NumTargets, (byte)8);
            for (var i = 0; i < numTargets; i++)
            {
                var effect = effects->Effects[i];
                positionLogWriter.WriteLine(FormattableString.Invariant(
                    $"{elapsed:F4},{DateTime.Now:O},0x{targetEntityIds[i].ObjectId:X8},Hit,,,,,,,action=0x{header->ActionId:X} caster=0x{casterEntityId:X8} type={effect.Type} value={effect.Value}"));
            }
            positionLogWriter.Flush();
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[DebugMenu] ActionEffect capture failed: {e.Message}");
        }
    }

    public void DrawPositionLogger()
    {
        ImGui.TextUnformatted("Position logger");
        ImGui.Separator();
        ImGui.TextWrapped(
            "Logs every object table entry's Position/Rotation/statuses every real game tick (not " +
            "tied to this window being open), plus every real action hit (action id, target, effect " +
            "type/value), to a CSV. Play the matching ARR replay back in-game while this is active to " +
            "capture ground-truth samples for reverse engineering a mechanic's real movement curve --" +
            " statuses and hits pin down what's happening at each moment without needing to watch and " +
            "mark it live.");
        // Not a bare "Start": CollapsingHeader pushes no ID scope, so it would collide with the
        // scenario Start button.
        if (ImGui.Button(positionLogActive ? "Stop capture" : "Start capture"))
            TogglePositionLog();
        if (positionLogActive)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"logging to {positionLogPath}");
        }
    }

    private readonly SpeedControl speedControl = new();

    public void DrawSpeedControl() => speedControl.Draw(plugin.Game, "debug-speed");

    public void DrawDebugContent()
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        ImGui.TextUnformatted($"TerritoryId: {Plugin.ClientState.TerritoryType}");

        if (ImGui.Button("Damage debug window"))
            DamageDebugWindow.Instance!.Toggle();

        ImGui.Spacing();
        DrawPositionLogger();

        ImGui.Spacing();
        ImGui.TextUnformatted("Player activity (live)");
        ImGui.Separator();
        // IsActing/IsMoving are only sampled while a scenario player ticks; the raw hook
        // signals stay live everywhere.
        var simPlayer = plugin.Game.Player;
        if (simPlayer == null)
            ImGui.TextDisabled("IsActing: -   IsMoving: -   (no scenario player)");
        else
        {
            DrawBoolFlag("IsActing", simPlayer.IsActing);
            ImGui.SameLine();
            DrawBoolFlag("IsMoving", simPlayer.IsMoving);
        }
        var inputHooks = Plugin.PlayerInputHooks;
        DrawBoolFlag("MovementInput", inputHooks.MovementInputActive);
        ImGui.SameLine();
        DrawBoolFlag("AutoAttacking", inputHooks.IsAutoAttacking);

        ImGui.Spacing();
        ImGui.TextUnformatted("Manual spawn");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("BNpcBaseId", ref debugBNpcBaseIdText, 16);
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("Scale (0 = default)", ref debugSpawnScaleText, 16);
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("ModeAttrFlags (blank = none)", ref debugSpawnModeAttrFlagsText, 16);
        if (ImGui.Button("Spawn"))
        {
            if (!TryParseId(debugBNpcBaseIdText, out var baseId))
            {
                Plugin.Log.Warning($"Spawn: can't parse BNpcBaseId '{debugBNpcBaseIdText}'");
            }
            else
            {
                float scale = 0f;
                var trimmed = debugSpawnScaleText.Trim();
                if (trimmed.Length > 0 && !float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                    Plugin.Log.Warning($"Spawn: can't parse Scale '{debugSpawnScaleText}', using default");
                byte? initialModeAttrFlags = null;
                var mafTrimmed = debugSpawnModeAttrFlagsText.Trim();
                if (mafTrimmed.Length > 0)
                {
                    if (TryParseId(mafTrimmed, out var maf) && maf <= 0xFF)
                        initialModeAttrFlags = (byte)maf;
                    else
                        Plugin.Log.Warning($"Spawn: can't parse ModeAttrFlags '{debugSpawnModeAttrFlagsText}', using default");
                }
                // Anchor to the player so Offset=0 lands at our feet; a scenario run overwrites it.
                var player = Plugin.ObjectTable.LocalPlayer;
                if (player != null) plugin.Game.World.ScenarioOrigin = player.Position;
                TimelineDebug.LastSpawn = plugin.Game.World.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: baseId,
                    Targetable: true,
                    Scale: scale,
                    InitialModeAttributeFlags: initialModeAttrFlags));
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Manual EObj spawn");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("EObjRowId", ref debugEObjRowIdText, 16);
        if (ImGui.Button("Spawn EObj"))
        {
            if (!TryParseId(debugEObjRowIdText, out var eObjRowId))
            {
                Plugin.Log.Warning($"Spawn EObj: can't parse EObjRowId '{debugEObjRowIdText}'");
            }
            else
            {
                // Same anchor trick as the BNpc spawn.
                var player = Plugin.ObjectTable.LocalPlayer;
                if (player != null) plugin.Game.World.ScenarioOrigin = player.Position;
                plugin.Game.World.SpawnEventObject(new EventObjectSpawnConfig
                {
                    EObjId = eObjRowId,
                    SpawnVisible = true
                });
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Play animation on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("TimelineId", ref debugTimelineIdText, 16);
        if (ImGui.Button("Play on target"))
        {
            if (TryParseId(debugTimelineIdText, out var timelineId))
                PlayAnimationOnTarget((ushort)timelineId);
            else Plugin.Log.Warning($"Play animation: can't parse TimelineId '{debugTimelineIdText}'");
        }
        TimelineDebug.DrawControls();

        ImGui.Spacing();
        ImGui.TextUnformatted("Attach lockon VFX to target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("LockonId", ref debugLockonIdText, 16);
        if (ImGui.Button("Attach lockon on target"))
        {
            if (TryParseId(debugLockonIdText, out var lockonId))
                AttachLockonOnTarget(lockonId);
            else Plugin.Log.Warning($"Lockon: can't parse LockonId '{debugLockonIdText}'");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Spawn VFX path on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(320 * uiScale);
        ImGui.InputText("VfxPath", ref debugVfxPathText, 256);
        if (ImGui.Button("Spawn VFX on target"))
            SpawnVfxOnTarget(debugVfxPathText);

        ImGui.Spacing();
        ImGui.TextUnformatted("Set ModelState on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("ModelState", ref debugModelStateText, 16);
        if (ImGui.Button("Set ModelState"))
        {
            if (!TryParseId(debugModelStateText, out var modelState) || modelState > 0xFF)
                Plugin.Log.Warning($"ModelState: can't parse '{debugModelStateText}'");
            else
                SetModelStateOnTarget((byte)modelState);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Apply ModeAttributeFlags on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("ModeAttrFlags", ref debugModeAttrFlagsText, 16);
        if (ImGui.Button("Apply ModeAttributeFlags"))
        {
            if (TryParseId(debugModeAttrFlagsText, out var flags) && flags <= 0xFF)
                SetModeAttributeFlagsOnTarget((byte)flags);
            else Plugin.Log.Warning($"ModeAttrFlags: can't parse '{debugModeAttrFlagsText}'");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Cast on player");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("ActionId", ref debugCastActionIdText, 16);
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("AnimationVariation", ref debugCastAnimVariationText, 16);
        if (ImGui.Button("Cast on player"))
        {
            if (!TryParseId(debugCastActionIdText, out var actionId))
                Plugin.Log.Warning($"Cast: can't parse ActionId '{debugCastActionIdText}'");
            else if (!TryParseId(debugCastAnimVariationText, out var animVar) || animVar > 0xFF)
                Plugin.Log.Warning($"Cast: can't parse AnimationVariation '{debugCastAnimVariationText}'");
            else
                CastOnPlayerFromTarget(actionId, (byte)animVar);
        }
        ImGui.SameLine();
        if (ImGui.Button("Cast on self"))
        {
            if (!TryParseId(debugCastActionIdText, out var actionId))
                Plugin.Log.Warning($"Cast: can't parse ActionId '{debugCastActionIdText}'");
            else if (!TryParseId(debugCastAnimVariationText, out var animVar) || animVar > 0xFF)
                Plugin.Log.Warning($"Cast: can't parse AnimationVariation '{debugCastAnimVariationText}'");
            else
                CastOnSelfFromTarget(actionId, (byte)animVar);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Apply status");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("StatusId", ref debugStatusIdText, 16);
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("Duration (0 = default)", ref debugStatusDurationText, 16);
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("Stacks (blank = 1)", ref debugStatusStacksText, 16);
        if (ImGui.Button("Apply on target##status"))
            ApplyStatus(onPlayer: false);
        ImGui.SameLine();
        if (ImGui.Button("Apply on player##status"))
            ApplyStatus(onPlayer: true);

        ImGui.Spacing();
        ImGui.TextUnformatted("Tank mitigation id lookup");
        ImGui.Separator();
        ImGui.TextWrapped("Press a mitigation cooldown in-game, then read its real id off " +
                           "these two lists -- use them to fill in the Mitigation and " +
                           "JobActions tables for whichever ability you just pressed.");
        var myStatuses = MyActiveStatuses();
        ImGui.TextUnformatted("My active statuses:");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy all##statuses"))
            ImGui.SetClipboardText(string.Join("\n", myStatuses.Select(s => $"{s.Id} -- {s.Name}")));
        foreach (var (id, name) in myStatuses)
            ImGui.BulletText($"{id} -- {name}");

        // For a SourceSide mitigation like Reprisal, which debuffs the target, not the caster.
        var targetStatuses = TargetActiveStatuses();
        ImGui.TextUnformatted("My target's active statuses:");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy all##targetStatuses"))
            ImGui.SetClipboardText(string.Join("\n", targetStatuses.Select(s => $"{s.Id} -- {s.Name}")));
        foreach (var (id, name) in targetStatuses)
            ImGui.BulletText($"{id} -- {name}");

        var recentActions = Plugin.PlayerInputHooks.RecentActions;
        ImGui.TextUnformatted("Recent actions pressed (newest last):");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy all##actions"))
            ImGui.SetClipboardText(string.Join("\n", recentActions.Select(a => $"{a.ActionId} ({a.Type}) -- {ActionLookup.Name(a.ActionId)}")));
        foreach (var (id, type) in recentActions)
            ImGui.BulletText($"{id} ({type}) -- {ActionLookup.Name(id)}");

        ImGui.Spacing();
        ImGui.TextUnformatted("Dump objects near player");
        ImGui.Separator();
        if (ImGui.Button("Dump"))
            DumpNearbyObjects();
        ImGui.SameLine();
        if (ImGui.Button("Dump target fields"))
            DumpTargetFields();
        ImGui.SameLine();
        if (ImGui.Button("Enumerate SharedGroups"))
            DumpSharedGroups();
        ImGui.SameLine();
        if (ImGui.Button("Bump EObj State"))
            BumpEventObjectState();

        ImGui.Spacing();
        ImGui.TextUnformatted("Map effect");
        ImGui.Separator();
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("Index", ref debugMapEffectIndexText, 16);
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("State", ref debugMapEffectStateText, 16);
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("Timeline", ref debugMapEffectTimelineText, 16);
        if (ImGui.Button("Apply map effect"))
        {
            if (!TryParseId(debugMapEffectIndexText, out var idx) || idx > 0xFF)
                Plugin.Log.Warning($"Map effect: can't parse Index '{debugMapEffectIndexText}'");
            else if (!TryParseId(debugMapEffectStateText, out var mapState) || mapState > 0xFFFF)
                Plugin.Log.Warning($"Map effect: can't parse State '{debugMapEffectStateText}'");
            else if (!TryParseId(debugMapEffectTimelineText, out var timeline) || timeline > 0xFFFF)
                Plugin.Log.Warning($"Map effect: can't parse Timeline '{debugMapEffectTimelineText}'");
            else
                plugin.Game.World.Map.AddEffect((timeline << 16) | mapState, (byte)idx);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Weather lab (sky-tint only -- writes EnvManager.ActiveWeather, never WeatherManager)");
        ImGui.Separator();
        ImGui.TextWrapped("This zone's own catalog (read live from EnvScene._weatherIds): 2 Fair Skies, "
            + "77/78/79/89/174/175/176 all \"Dimensional Disruption\". One click each, or type any other id below.");
        foreach (var id in (ReadOnlySpan<byte>)[2, 77, 78, 79, 89, 174, 175, 176])
        {
            if (ImGui.Button($"{id}##weatherquick")) plugin.Game.World.SetWeather(id);
            ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Weather id (any)##weatherlab", ref debugWeatherIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Apply##weatherid"))
        {
            if (!TryParseId(debugWeatherIdText, out var wid) || wid > 0xFF)
                Plugin.Log.Warning($"Weather: can't parse id '{debugWeatherIdText}'");
            else
                plugin.Game.World.SetWeather((byte)wid);
        }
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Day time (seconds, 0=midnight/43200=noon)", ref debugDayTimeSeconds, 0f, 86400f))
        {
            var env = EnvManager.Instance();
            if (env != null) env->DayTimeSeconds = debugDayTimeSeconds;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Director update (ActorControl replay)");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("Category", ref debugDirectorCategoryText, 16);
        ImGui.SetNextItemWidth(120 * uiScale);
        ImGui.InputText("Arg1", ref debugDirectorArg1Text, 16);
        if (ImGui.Button("Fire director update"))
        {
            if (!TryParseId(debugDirectorCategoryText, out var cat))
                Plugin.Log.Warning($"Director update: can't parse Category '{debugDirectorCategoryText}'");
            else if (!TryParseId(debugDirectorArg1Text, out var arg1))
                Plugin.Log.Warning($"Director update: can't parse Arg1 '{debugDirectorArg1Text}'");
            else
                InstanceContentDirectorHelper.ProcessDirectorUpdate(cat, arg1);
        }
        ImGui.SameLine();
        if (ImGui.Button("Fire P5 Sigma transition"))
        {
            // The real trigger (~135 ms before Omega-M's 7B85 cast): 33 | 800375AC | 8000001E | 2AC
            InstanceContentDirectorHelper.ProcessDirectorUpdate(0x8000001E, 0x2AC);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Death system");
        ImGui.Separator();
        if (ImGui.Button("Kill MT"))
        {
            var mt = plugin.Game.World.Party.Get(PartyRole.MainTank);
            mt?.Die("Debug kill");
        }
        ImGui.SameLine();
        if (ImGui.Button("Kill player") && plugin.Game.Player is { } p) p.Die("Debug kill");

        ImGui.Spacing();
        ImGui.TextUnformatted("BGM test");
        ImGui.Separator();
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputText("BgmId", ref debugBgmIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Play##bgm"))
        {
            if (!TryParseId(debugBgmIdText, out var bgmId) || bgmId > ushort.MaxValue)
                Plugin.Log.Warning($"BGM: can't parse BgmId '{debugBgmIdText}'");
            else
                Natives.Bgm.Play((ushort)bgmId);
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop##bgm")) Natives.Bgm.Reset();
        ImGui.SetNextItemWidth(80 * uiScale);
        ImGui.InputFloat("s##bgmseek", ref debugBgmSeekSeconds, 0f, 0f, "%.2f");
        ImGui.SameLine();
        if (ImGui.Button("Sync##bgm")) Natives.Bgm.Sync(debugBgmSeekSeconds);
        ImGui.SameLine();
        if (ImGui.Button("Where##bgm")) Natives.Bgm.LogPosition("debug");
    }

    // Live read-only flag readout: green when set, dimmed when clear.
    private static void DrawBoolFlag(string label, bool value)
    {
        var color = value
            ? new System.Numerics.Vector4(0.3f, 1f, 0.3f, 1f)
            : new System.Numerics.Vector4(0.55f, 0.55f, 0.55f, 1f);
        ImGui.TextColored(color, $"{label}: {value}");
    }

    // Accepts decimal ("1340") or hex ("0x53C" / "53Ch" — case-insensitive).
    private static bool TryParseId(string input, out uint value)
    {
        var s = input.Trim();
        if (s.Length == 0) { value = 0; return false; }
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private void PlayAnimationOnTarget(ushort timelineId)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("Play animation: no target selected");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                DiagnosticLog.ForcePersist = true;
                DiagnosticLog.Info($"[DebugMenu] Play timeline {timelineId} on '{enemy.DisplayName}' (territory {Plugin.ClientState.TerritoryType}) -- before: {enemy.DescribeActionTimeline()}");
                enemy.PlayActionTimeline(timelineId);
                enemy.StartTimelineWatch(4f);
                Plugin.Log.Info($"Play animation: timeline 0x{timelineId:X} on '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"Play animation: target '{target.Name}' is not a tracked enemy");
    }

    // Fire-and-forget: the game owns the VFX lifetime.
    private void AttachLockonOnTarget(uint lockonId)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("Lockon: no target selected");
            return;
        }
        var chara = ResolveSimCharacter(target.GameObjectId);
        if (chara == null)
        {
            Plugin.Log.Warning($"Lockon: target '{target.Name}' is not a tracked sim character");
            return;
        }
        var iconName = Natives.Vfx.LockonIconName(lockonId);
        if (iconName == null)
        {
            Plugin.Log.Warning($"Lockon: no IconName for LockonId {lockonId}");
            return;
        }
        chara.AttachLockonVfx(lockonId, persistent: false);
        Plugin.Log.Info($"Lockon: attached {lockonId} ({iconName}) on '{target.Name}'");
    }

    // Attaches a raw VFX path to the targeted sim character — enemy doppel, party doppel, or
    // the player if self-targeted — falling back to the player when nothing tracked is targeted,
    // since testing a mark on yourself is the common case. Fire-and-forget (persistent: false):
    // the game owns the VFX lifetime, the sim doesn't track or remove it.
    private void SpawnVfxOnTarget(string path)
    {
        path = path.Trim();
        if (path.Length == 0)
        {
            Plugin.Log.Warning("Spawn VFX: empty path");
            return;
        }
        if (!Natives.Data.FileExists(path)) return;

        SimCharacter? chara = null;
        var who = "player";
        var target = Plugin.TargetManager.Target;
        if (target != null)
        {
            chara = ResolveSimCharacter(target.GameObjectId);
            if (chara != null) who = target.Name.ToString();
        }
        chara ??= plugin.Game.World.Party.Player;
        if (chara == null)
        {
            Plugin.Log.Warning("Spawn VFX: no tracked target and no player; start a scenario first");
            return;
        }
        if (!chara.IsActive)
        {
            Plugin.Log.Warning($"Spawn VFX: '{who}' is not active");
            return;
        }

        chara.AddVfx(path, persistent: false);
        Plugin.Log.Info($"Spawn VFX: '{path}' on '{who}'");
    }

    private void SetModelStateOnTarget(byte value)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("ModelState: no target selected");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                enemy.SetModelState(value);
                Plugin.Log.Info($"ModelState: 0x{value:X2} on '{enemy.DisplayName}' (no commit)");
                return;
            }
        }
        Plugin.Log.Warning($"ModelState: target '{target.Name}' is not a tracked enemy");
    }

    private void SetModeAttributeFlagsOnTarget(byte value)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("ModeAttrFlags: no target selected");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                enemy.SetModeAttributeFlags(value);
                Plugin.Log.Info($"ModeAttrFlags: 0x{value:X2} on '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"ModeAttrFlags: target '{target.Name}' is not a tracked enemy");
    }

    private void CastOnPlayerFromTarget(uint actionId, byte animationVariation)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("Cast: no target selected");
            return;
        }
        var player = plugin.Game.Player;
        if (player == null)
        {
            Plugin.Log.Warning("Cast: no local player");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                enemy.Cast(actionId, targetLocation: player.Position, targetId: player.GameObjectId, animationVariation: animationVariation);
                Plugin.Log.Info($"Cast: action 0x{actionId:X} (anim variation {animationVariation}) on player from '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"Cast: target '{target.Name}' is not a tracked enemy");
    }

    private void CastOnSelfFromTarget(uint actionId, byte animationVariation)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("Cast: no target selected");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                enemy.Cast(actionId, targetLocation: enemy.Position, targetId: enemy.GameObjectId, animationVariation: animationVariation);
                Plugin.Log.Info($"Cast: action 0x{actionId:X} (anim variation {animationVariation}) on self from '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"Cast: target '{target.Name}' is not a tracked enemy");
    }

    // Off the native StatusManager and ObjectTable.LocalPlayer, so it works outside a scenario;
    // unfiltered, since the point is spotting an id not in the chart yet.
    private List<(ushort Id, string Name)> MyActiveStatuses()
    {
        var result = new List<(ushort, string)>();
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null) return result;
        var bc = (BattleChara*)localPlayer.Address;
        if (bc == null) return result;
        foreach (var status in bc->StatusManager.Status)
            if (status.StatusId != 0)
                result.Add((status.StatusId, StatusLookup.Name(status.StatusId)));
        return result;
    }

    private List<(ushort Id, string Name)> TargetActiveStatuses()
    {
        var result = new List<(ushort, string)>();
        var target = Plugin.TargetManager.Target;
        if (target == null) return result;
        var bc = (BattleChara*)target.Address;
        if (bc == null) return result;
        foreach (var status in bc->StatusManager.Status)
            if (status.StatusId != 0)
                result.Add((status.StatusId, StatusLookup.Name(status.StatusId)));
        return result;
    }

    // Blank Duration = AddStatus's default, blank Stacks = 1.
    private void ApplyStatus(bool onPlayer)
    {
        if (!TryParseId(debugStatusIdText, out var statusId) || statusId == 0 || statusId > ushort.MaxValue)
        {
            Plugin.Log.Warning($"Status: can't parse StatusId '{debugStatusIdText}'");
            return;
        }

        float duration = 0f;
        var durTrimmed = debugStatusDurationText.Trim();
        if (durTrimmed.Length > 0 && !float.TryParse(durTrimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out duration))
        {
            Plugin.Log.Warning($"Status: can't parse Duration '{debugStatusDurationText}', using default");
            duration = 0f;
        }

        ushort stacks = 1;
        var stacksTrimmed = debugStatusStacksText.Trim();
        if (stacksTrimmed.Length > 0)
        {
            if (TryParseId(stacksTrimmed, out var st) && st <= ushort.MaxValue)
                stacks = (ushort)st;
            else
                Plugin.Log.Warning($"Status: can't parse Stacks '{debugStatusStacksText}', using 1");
        }

        SimCharacter? chara;
        string who;
        if (onPlayer)
        {
            chara = plugin.Game.Player;
            who = "player";
            if (chara == null) { Plugin.Log.Warning("Status: no local player"); return; }
        }
        else
        {
            var target = Plugin.TargetManager.Target;
            if (target == null) { Plugin.Log.Warning("Status: no target selected"); return; }
            chara = ResolveSimCharacter(target.GameObjectId);
            who = $"'{target.Name}'";
            if (chara == null) { Plugin.Log.Warning($"Status: target {who} is not a tracked sim character"); return; }
        }

        chara.AddStatus((ushort)statusId, duration, stacks, overrideStacks: true);
        Plugin.Log.Info($"Status: applied {statusId} (duration {(duration == 0f ? "default" : duration.ToString(CultureInfo.InvariantCulture))}, stacks {stacks}) on {who}");
    }

    // Null if the target isn't one of ours.
    private SimCharacter? ResolveSimCharacter(ulong gameObjectId)
    {
        foreach (var c in plugin.Game.World.Children.OfType<SimCharacter>())
            if ((ulong)c.GameObjectId == gameObjectId) return c;
        foreach (var m in plugin.Game.World.Party.AllMembers())
            if ((ulong)m.GameObjectId == gameObjectId) return m;
        return null;
    }

    // The BattleChara fields that drive a boss's appearance variant, for before/after diffs.
    private static void DumpTargetFields()
    {
        var target = Plugin.TargetManager.Target;
        if (target == null) { Plugin.Log.Warning("DumpTarget: no target selected"); return; }

        var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
        if (go == null) { Plugin.Log.Warning("DumpTarget: target address is null"); return; }

        var name = target.Name.TextValue;
        if (string.IsNullOrEmpty(name)) name = "<unnamed>";

        var ch = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)go;

        Plugin.Log.Info($"=== DumpTarget '{name}' addr=0x{(nint)go:X} kind={target.ObjectKind} BaseId=0x{target.BaseId:X} ({target.BaseId}) ===");
        Plugin.Log.Info($"  Pos=({go->Position.X:F2},{go->Position.Y:F2},{go->Position.Z:F2}) Rot={go->Rotation:F3} Scale={go->Scale:F2}");
        Plugin.Log.Info($"  DrawObject*=0x{(nint)go->DrawObject:X}");
        Plugin.Log.Info($"  Mode={ch->Mode} ({(byte)ch->Mode}) ModeParam=0x{ch->ModeParam:X2} ({ch->ModeParam})");
        Plugin.Log.Info($"  TransformationId={ch->TransformationId} StatusLoopVfxId={ch->StatusLoopVfxId} Battalion={ch->Battalion} ShieldValue={ch->ShieldValue}");
        Plugin.Log.Info($"  ModelContainer: ModelCharaId={ch->ModelContainer.ModelCharaId} ModelSkeletonId={ch->ModelContainer.ModelSkeletonId} ModelCharaId_2={ch->ModelContainer.ModelCharaId_2} ModelSkeletonId_2={ch->ModelContainer.ModelSkeletonId_2}");
        Plugin.Log.Info($"  ModelContainer: ModelScaleId=0x{ch->ModelContainer.ModelScaleId:X2} ModeAttributeFlags=0x{ch->ModelContainer.ModeAttributeFlags:X2} UnscaledRadius={ch->ModelContainer.UnscaledRadius:F2}");
        Plugin.Log.Info($"  WeaponFlags=0x{ch->WeaponFlags:X2} ActorControlFlags=0x{ch->ActorControlFlags:X2}");
        Plugin.Log.Info($"  Timeline.ModelState=0x{ch->Timeline.ModelState:X2} AnimationState=[0x{ch->Timeline.AnimationState[0]:X2},0x{ch->Timeline.AnimationState[1]:X2}]");
        for (int s = 0; s < 3; s++)
        {
            ref var w = ref ch->DrawData.WeaponData[s];
            Plugin.Log.Info($"  DrawData.Weapon[{s}]: Id={w.ModelId.Id} Type={w.ModelId.Type} Variant={w.ModelId.Variant} Stain=({w.ModelId.Stain0},{w.ModelId.Stain1}) State=0x{w.State:X2} Flags1=0x{w.Flags1:X4} Flags2=0x{w.Flags2:X2} DrawObject*=0x{(nint)w.DrawObject:X}");
        }
        Plugin.Log.Info($"  DrawData.Flags1=0x{ch->DrawData.Flags1:X2} Flags2=0x{ch->DrawData.Flags2:X2}");
    }

    // Every object in the table, sorted by distance from the local player.
    private static void DumpNearbyObjects()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) { Plugin.Log.Warning("DumpObjects: no local player"); return; }
        var origin = player.Position;

        var rows = new List<(float Dist, string Line)>();
        foreach (var obj in Plugin.ObjectTable)
        {
            var dx = obj.Position.X - origin.X;
            var dz = obj.Position.Z - origin.Z;
            var dist = MathF.Sqrt(dx * dx + dz * dz);
            var name = obj.Name.TextValue;
            if (string.IsNullOrEmpty(name)) name = "<unnamed>";
            var scale = ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address)->Scale;
            rows.Add((dist, $"  [{obj.ObjectKind,-12}] BaseId=0x{obj.BaseId:X} ({obj.BaseId}) dist={dist,7:F2} scale={scale,5:F2}  '{name}'"));
        }
        rows.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        Plugin.Log.Info($"=== ObjectTable: {rows.Count} objects ===");
        foreach (var (_, line) in rows) Plugin.Log.Info(line);
    }

    // Every SharedGroup in the active layout with its sgb path and position, to tell LGB-baked
    // scenery from director-spawned.
    private static void DumpSharedGroups()
    {
        var rows = new List<(float Dist, string Line)>();
        var player = Plugin.ObjectTable.LocalPlayer;
        var origin = player?.Position ?? default;
        int total = LayoutQuery.EnumerateAll(p =>
        {
            var sg = (FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group.SharedGroupLayoutInstance*)p;
            var pos = sg->Transform.Translation;
            var path = LayoutQuery.GetSgbPath(sg) ?? "(no resource handle)";
            var dx = pos.X - origin.X;
            var dz = pos.Z - origin.Z;
            var dist = MathF.Sqrt(dx * dx + dz * dz);
            var inst = (FFXIVClientStructs.FFXIV.Client.LayoutEngine.ILayoutInstance*)sg;
            rows.Add((dist, $"  dist={dist,7:F2} pos=({pos.X,8:F2},{pos.Y,7:F2},{pos.Z,8:F2}) active={inst->IsActive,-5} key=0x{inst->Id.InstanceKey:X8} sub=0x{inst->SubId:X8}  '{path}'"));
        });
        rows.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        Plugin.Log.Info($"=== SharedGroups in active layout: {total} ===");
        foreach (var (_, line) in rows) Plugin.Log.Info(line);
    }

    // Increments SetState on every live SimEventObject per click, to find which state value
    // activates an EObj's hidden visuals.
    private static ushort eventObjectStateProbe;
    private void BumpEventObjectState()
    {
        eventObjectStateProbe++;
        int n = 0;
        foreach (var child in plugin.Game.World.Children)
        {
            if (child is not SimEventObject eo || !eo.IsAlive) continue;
            eo.SetState(eventObjectStateProbe);
            n++;
        }
        Plugin.Log.Info($"Bumped EObj state to {eventObjectStateProbe} on {n} SimEventObjects");
    }
}
#endif
