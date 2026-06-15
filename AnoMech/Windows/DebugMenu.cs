#if DEBUG
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Helpers;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
    }

    private string debugBNpcBaseIdText = "15720";
    private string debugSpawnScaleText = "0";
    private string debugSpawnModeAttrFlagsText = "";
    private string debugTimelineIdText = "0x53C";
    private string debugModelStateText = "0x00";
    private string debugModeAttrFlagsText = "0x00";
    private string debugCastActionIdText = "0";
    private string debugCastAnimVariationText = "0";
    private string debugLockonIdText = "0";
    private string debugStatusIdText = "0";
    private string debugStatusDurationText = "0";
    private string debugStatusStacksText = "1";
    private string debugMapEffectIndexText = "0x00";
    private string debugMapEffectStatusText = "0x0000";
    private string debugMapEffectFlagText = "0x00";
    private string debugDirectorCategoryText = "0x8000001E";
    private string debugDirectorArg1Text = "0x2AC";
    private string debugBgmIdText = "964";
    // EObj 1EB83C (decimal 2013244) = the TOP P5 Sigma falling-orb tower; useful default.
    private string debugEObjRowIdText = "2013244";

    // Buttons modify Game.EventTimeScale live so callers can speed up / slow down a
    // scenario mid-run. Only event scheduling is affected; cast bars and animations
    // continue at real time (see Game.Tick).
    public void DrawSpeedControl()
    {
        var game = plugin.Game;
        ImGui.TextUnformatted("Speed:");
        for (int x = 1; x <= 4; x++)
        {
            ImGui.SameLine();
            var active = MathF.Abs(game.EventTimeScale - x) < 0.01f;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.Button($"x{x}")) game.EventTimeScale = x;
            if (active) ImGui.PopStyleColor();
        }
    }

    public void DrawDebugContent()
    {
        ImGui.TextUnformatted($"TerritoryId: {Plugin.ClientState.TerritoryType}");

        if (ImGui.Button("Damage debug window"))
            DamageDebugWindow.Instance!.Toggle();

        ImGui.Spacing();
        ImGui.TextUnformatted("Player activity (live)");
        ImGui.Separator();
        // IsActing/IsMoving are only sampled while a scenario player is ticking; the raw
        // input-hook signals below stay live everywhere (handy to verify detection at the inn).
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
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("BNpcBaseId", ref debugBNpcBaseIdText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Scale (0 = default)", ref debugSpawnScaleText, 16);
        ImGui.SetNextItemWidth(80);
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
                // Ad-hoc spawn outside any scenario — anchor to the player so
                // Offset=0 lands at our feet. Scenario runs overwrite this in
                // Game.RunScenarioInternal, so the stamp is non-leaking.
                var player = Plugin.ObjectTable.LocalPlayer;
                if (player != null) plugin.Game.World.ScenarioOrigin = player.Position;
                plugin.Game.World.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: baseId,
                    Targetable: true,
                    Scale: scale,
                    InitialModeAttributeFlags: initialModeAttrFlags));
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Manual EObj spawn");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("EObjRowId", ref debugEObjRowIdText, 16);
        if (ImGui.Button("Spawn EObj"))
        {
            if (!TryParseId(debugEObjRowIdText, out var eObjRowId))
            {
                Plugin.Log.Warning($"Spawn EObj: can't parse EObjRowId '{debugEObjRowIdText}'");
            }
            else
            {
                // Same anchor trick as the BNpc spawn — drop the prop at the
                // player's feet by stamping ScenarioOrigin. Scenarios overwrite
                // this in Game.RunScenarioInternal.
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
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("TimelineId", ref debugTimelineIdText, 16);
        if (ImGui.Button("Play on target"))
        {
            if (TryParseId(debugTimelineIdText, out var timelineId))
                PlayAnimationOnTarget((ushort)timelineId);
            else Plugin.Log.Warning($"Play animation: can't parse TimelineId '{debugTimelineIdText}'");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Attach lockon VFX to target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("LockonId", ref debugLockonIdText, 16);
        if (ImGui.Button("Attach lockon on target"))
        {
            if (TryParseId(debugLockonIdText, out var lockonId))
                AttachLockonOnTarget(lockonId);
            else Plugin.Log.Warning($"Lockon: can't parse LockonId '{debugLockonIdText}'");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Set ModelState on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
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
        ImGui.SetNextItemWidth(120);
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
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("ActionId", ref debugCastActionIdText, 16);
        ImGui.SetNextItemWidth(120);
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
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("StatusId", ref debugStatusIdText, 16);
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("Duration (0 = default)", ref debugStatusDurationText, 16);
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("Stacks (blank = 1)", ref debugStatusStacksText, 16);
        if (ImGui.Button("Apply on target##status"))
            ApplyStatus(onPlayer: false);
        ImGui.SameLine();
        if (ImGui.Button("Apply on player##status"))
            ApplyStatus(onPlayer: true);

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
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Index", ref debugMapEffectIndexText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Status", ref debugMapEffectStatusText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Flag", ref debugMapEffectFlagText, 16);
        if (ImGui.Button("Apply map effect"))
        {
            if (!TryParseId(debugMapEffectIndexText, out var idx) || idx > 0xFF)
                Plugin.Log.Warning($"Map effect: can't parse Index '{debugMapEffectIndexText}'");
            else if (!TryParseId(debugMapEffectStatusText, out var status) || status > 0xFFFF)
                Plugin.Log.Warning($"Map effect: can't parse Status '{debugMapEffectStatusText}'");
            else if (!TryParseId(debugMapEffectFlagText, out var flag) || flag > 0xFF)
                Plugin.Log.Warning($"Map effect: can't parse Flag '{debugMapEffectFlagText}'");
            else
                plugin.Game.World.Map.AddEffect((status << 16) | (flag & 0xFFu), (byte)idx);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Director update (ActorControl replay)");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("Category", ref debugDirectorCategoryText, 16);
        ImGui.SetNextItemWidth(120);
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
            // Replays the P5 Sigma transition trigger observed in TOP_pull_05_clear.log
            // at 01:21:13.0890 (~135 ms before Omega-M's 7B85 / Omega-F's 7B86 cast):
            //   33 | 800375AC | 8000001E | 2AC
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
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("BgmId", ref debugBgmIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Play##bgm"))
        {
            if (!TryParseId(debugBgmIdText, out var bgmId) || bgmId > ushort.MaxValue)
                Plugin.Log.Warning($"BGM: can't parse BgmId '{debugBgmIdText}'");
            else
                plugin.Game.Bgm.Play((ushort)bgmId);
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop##bgm")) plugin.Game.Bgm.Reset();
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
                enemy.PlayActionTimeline(timelineId);
                Plugin.Log.Info($"Play animation: timeline 0x{timelineId:X} on '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"Play animation: target '{target.Name}' is not a tracked enemy");
    }

    // Resolves the Lockon-sheet IconName for lockonId, builds vfx/lockon/eff/{name}.avfx,
    // and attaches it (entity-following) to the targeted sim character — enemy doppel,
    // party doppel, or the player if self-targeted. Fire-and-forget (persistent: false):
    // the game owns the VFX lifetime, the sim doesn't track or remove it.
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
        var iconName = VfxFunctions.LockonVfxIconName(lockonId);
        if (iconName == null)
        {
            Plugin.Log.Warning($"Lockon: no IconName for LockonId {lockonId}");
            return;
        }
        chara.AttachLockonVfx(lockonId, persistent: false);
        Plugin.Log.Info($"Lockon: attached {lockonId} ({iconName}) on '{target.Name}'");
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

    // Applies the StatusId/Duration/Stacks inputs to either the local player or
    // the currently-targeted sim character (enemy doppel, party doppel, or the
    // player if they target themselves). Goes through SimCharacter.AddStatus so
    // the status is tracked + re-stamped like a scenario-applied one; blank
    // Duration falls through to AddStatus's default, blank Stacks means 1.
    // overrideStacks: true so re-applying sets the absolute stack count typed.
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

    // Resolves a targeted game object to the SimCharacter behind it — searches
    // spawned enemies (World.Children) and every party slot, which includes the
    // SimPlayer. Returns null if the target isn't one of ours.
    private SimCharacter? ResolveSimCharacter(ulong gameObjectId)
    {
        foreach (var c in plugin.Game.World.Children.OfType<SimCharacter>())
            if ((ulong)c.GameObjectId == gameObjectId) return c;
        foreach (var m in plugin.Game.World.Party.AllMembers())
            if ((ulong)m.GameObjectId == gameObjectId) return m;
        return null;
    }

    // Dumps the BattleChara fields we suspect drive Omega-M's shield/weapon variant —
    // Mode/ModeParam, TransformationId, ModelContainer, DrawData weapon slots, Timeline.ModelState,
    // plus the DrawObject* address — so before/after snapshots can be diffed to find the field
    // that actually changes when boss appearance mutates.
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

    // Logs every object in the table sorted by distance from the local player. BaseId is
    // the underlying BNpcBase row for BNpcs (and the equivalent base id for other kinds);
    // Scale is read from the unsafe GameObject struct.
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

    // Lists every SharedGroup ILayoutInstance in the active layout with its
    // sgb path + world position. Used to verify which EObj scenery (TOP arena
    // tiles, the 1EA1A1 fixture, the Exit portal, Sigma ring spokes) is
    // LGB-baked vs. duty-director-runtime-spawned. Match the output against
    // ACT log positions to identify which acquirable instances exist.
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

    // Cycles SetState(N) across every live SimEventObject in the world, where
    // N increments each click. SGs gate sub-instance visibility on this state
    // field — we use this to find empirically which value activates a given
    // EObj's hidden visuals (e.g., the P5 Sigma tower ground circles).
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
