using AnoMech.Core.Game;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// Placement.Position is scenario-local (offset from SimWorld.ScenarioOrigin), same
// coordinate space as the rest of the SimXxx API: +X = east, +Z = south.
// Placement.Rotation is absolute radians: 0 = south, π/2 = east, π = north, -π/2 = west.
// ModelCharaId (non-zero) overrides the BNpcBase visual, e.g. a no-shield variant.
// Hitbox radius = BNpcBase.Scale × ModelChara's unscaled radius, unless HitboxRadius
// (non-zero) overrides it — decoupling the clickable/targetable hitbox from Scale.

// Whether a SimEnemy shows in the _EnemyList HUD (read each frame by EnmityHud.Refresh).
// Always          — listed while alive.
// OnlyWhenVisible — follows the engine's DrawObject.IsVisible; for adds that warp
//                   in/out. Don't combine with SetModelState (its rebuild briefly
//                   DisableDraws and flaps the list); transforming bosses use Always.
// Never           — never listed (AOE-source dummies, tether endpoints).
// Manual          — scenario drives it via SetInEnemyList(bool); default false.
public enum EnemyListMode
{
    Always,
    OnlyWhenVisible,
    Never,
    Manual,
}

public record struct EnemySpawnConfig(
    uint BNpcBaseId,
    uint NameId = 0,
    byte Level = 0,
    bool Targetable = false,
    EnemyListMode EnemyList = EnemyListMode.Always,
    bool IsVisible = true,
    Placement Placement = default,
    uint ModelCharaId = 0,
    float Scale = 0f,    // 0 = use BNpcBase.Scale
    float HitboxRadius = 0f,    // 0 = ModelChara unscaled radius × Scale
    byte? InitialModeAttributeFlags = null); // null = leave at engine default (0x00); set when the boss's canonical idle sub-mesh variant differs (e.g. Omega-M = 0x10)

public sealed unsafe class SimEnemy : SimNpc
{
    // Cast bar, action-effect release, omen telegraph, and animation lock live in
    // SimCast. SimEnemy just converts target coords to world space and reads IsBusy.
    private readonly SimCast cast;

    // Visibility runs through the DrawObject lifecycle: SetVisible records a desired
    // state; Tick's reconciler fires EnableDraw/DisableDraw once per change, gated on
    // IsReadyToDraw so toggles can't race the async model load. RenderFlags writes
    // were tried and don't reliably keep enemies visible — only this path does.
    // currentVisible starts true because base SimNpc.Tick fires the initial EnableDraw.
    // FIXME: this is not valid, but good enough i guess
    private bool desiredVisible = true;
    private bool currentVisible = true;

    public uint BNpcBaseId { get; }

    // CharacterManager._battleCharas registration is kept aligned with visibility:
    // a registered BC is force-drawn by the engine's per-frame CharacterManager
    // update, so staying registered while invisible would override our DisableDraw.
    // We register only while visible (toggled by SyncRegistration). Safe because
    // scenarios are inn-gated, so the open-world render-cache teardown crash
    // documented in EnmityHud.cs is unreachable.
    private bool registeredNow;


    // Live-read via GameObject::GetName() (vfunc 6, resolves NameId -> BNpcName) —
    // same path the target bar uses, so engine-driven renames mid-fight propagate
    // (e.g. TOP P5 Sigma Omega: 1DD3 -> 1DD4 -> 1E0F -> 2FE2). Reading the
    // GameObject.Name[] buffer directly does NOT work for doppels — the engine never
    // refreshes it on rename. Falls back to the spawn-time name mid-despawn.
    public string DisplayName
    {
        get
        {
            var chara = BattleCharaPtr;
            if (chara == null) return field;
            var name = ((GameObject*)chara)->GetName().ToString();
            return string.IsNullOrEmpty(name) ? field : name;
        }
    }

    public EnemyListMode EnemyListMode { get; }
    private bool manualInEnemyList;

    // OnlyWhenVisible reads the live DrawObject.IsVisible flag, so any draw-lifecycle
    // toggle is reflected without extra plumbing; Manual lets the scenario drive it.
    public bool InEnemyList => EnemyListMode switch
    {
        EnemyListMode.Always          => true,
        EnemyListMode.Never           => false,
        EnemyListMode.Manual          => manualInEnemyList,
        EnemyListMode.OnlyWhenVisible => IsEngineVisible(),
        _ => false,
    };
    public bool IsCasting => cast.IsCasting;
    public uint CastActionId => cast.ActionId;
    public float CastProgress => cast.Progress;

    internal SimEnemy(uint index, uint bNpcBaseId, string displayName, EnemyListMode enemyListMode, Coordinates coordinates, bool initiallyVisible = true) : base(index, coordinates)
    {
        BNpcBaseId = bNpcBaseId;
        DisplayName = displayName;
        EnemyListMode = enemyListMode;
        cast = new SimCast(this, coordinates);
        // Mirrors the conditional register call in Spawn: registered iff
        // spawned visible.
        registeredNow = initiallyVisible;
    }

    // Allocates a BattleChara, configures it as a BattleNpc per the supplied
    // config, and returns a SimEnemy wrapping it. Caller is responsible for
    // registering the result in the world's children list (so reset/teardown
    // covers it). Returns null on missing LocalPlayer, BNpcBase miss, or
    // CreateBattleChara failure.
    internal static SimEnemy? Spawn(EnemySpawnConfig config, SimWorld world)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return null;

        var bnpcSheet = Plugin.DataManager.GetExcelSheet<BNpcBase>();
        if (!bnpcSheet.TryGetRow(config.BNpcBaseId, out var bnpc))
        {
            Plugin.Log.Warning($"BNpcBase row {config.BNpcBaseId} (0x{config.BNpcBaseId:X}) not found");
            return null;
        }

        var modelCharaId = config.ModelCharaId != 0 ? config.ModelCharaId : bnpc.ModelChara.RowId;
        var modelCharaSheet = Plugin.DataManager.GetExcelSheet<ModelChara>();
        if (!modelCharaSheet.TryGetRow(modelCharaId, out var modelChara))
        {
            Plugin.Log.Warning($"ModelChara row {config.BNpcBaseId} (0x{config.BNpcBaseId:X}) not found");
            return null;
        }

        if (!BattleCharaSpawn.CreateBattleChara(out var idx, out var obj)) return null;

        var chara = (BattleChara*)obj;
        // Engine's canonical BNpc initializer — populates ModelContainer from BNpcBase,
        // including ModeAttributeFlags (body sub-mesh, e.g. Omega-M's shield). Must run
        // before our overrides below.
        chara->CharacterSetup.SetupBNpc(config.BNpcBaseId, config.NameId);
        chara->ObjectKind = ObjectKind.BattleNpc;
        chara->Position = world.Coordinates.ToGlobal(config.Placement.Position);
        chara->SetRotation(MathUtil.NormalizeRotation(config.Placement.Rotation));
        var scale = config.Scale > 0f ? config.Scale : bnpc.Scale;
        chara->Scale = scale;
        chara->ModelContainer.ModelCharaId = (int)modelCharaId;
        chara->SEPack = bnpc.SEPack;

        var nativeHitbox = true;

        // From Client::Game::Character::CharacterSetupContainer_SetupRaw
        switch (modelChara.Type)
        {
            case 1:
                // TODO: This Type in the game's .exe is a bit complex, for now we just fallback to the previous solving method
                var hitboxRadius = config.HitboxRadius > 0f ? config.HitboxRadius : ResolveHitboxRadius(modelCharaId, scale);
                chara->HitboxRadius = hitboxRadius;
                nativeHitbox = false;
                break;
            case 2:
                chara->ModelContainer.ModelSkeletonId = modelChara.Model + 10000;
                break;
            case 3:
                chara->ModelContainer.ModelSkeletonId = modelChara.Model;
                break;
        }

        if (nativeHitbox)
        {
            chara->ModelContainer.UnscaledRadius = ModelContainerFunctions.CalculateUnscaledRadius(&chara->ModelContainer);
            chara->HitboxRadius = chara->Scale * chara->ModelContainer.UnscaledRadius; // From Client::Game::Character::ModelContainer_UpdateHitboxRadius
        }

        // Engine-resolved name (vfunc 6), same source as the nameplate, so the Name[]
        // buffer we stamp below stays consistent with the rest of the UI.
        var displayName = ((GameObject*)chara)->GetName().ToString();
        if (string.IsNullOrEmpty(displayName)) displayName = $"BNpc {config.BNpcBaseId:X}";
        BattleCharaSpawn.WriteName(obj, displayName);
        obj->RenderFlags = 0;

        chara->CharacterSetup.CopyFromCharacter((Character*)chara, CharacterSetupContainer.CopyFlags.None);

        chara->BattleNpcSubKind = BattleNpcSubKind.Combatant;
        chara->MaxHealth = 1_000_000;
        chara->Health = 1_000_000;
        chara->Battalion = 4;
        chara->IsHostile = true;
        chara->InCombat = true;
        chara->CombatTagType = 1;
        chara->CombatTaggerId = ((GameObject*)player.Address)->GetGameObjectId();
        chara->Mode = CharacterModes.Normal;
        chara->ModeParam = 0;
        if (config.InitialModeAttributeFlags is { } maf)
            chara->ModelContainer.ModeAttributeFlags = maf;
        chara->CastInfo.IsCasting = false;
        if (config.NameId != 0) chara->NameId = config.NameId;
        if (config.Level != 0) chara->Level = config.Level;

        // Registration lets the engine resolve the boss by entity id (needed for
        // ActionEffect delivery and model-state morphs). Register only while visible —
        // see the registeredNow field for the full rationale.
        if (config.IsVisible)
            BattleCharaSpawn.RegisterInCharacterManager(chara);

        Plugin.Log.Info($"SimEnemy: spawned BNpcBase {config.BNpcBaseId} (ModelChara {bnpc.ModelChara.RowId}, scale {bnpc.Scale}) at index {idx}");
        var enemy = new SimEnemy(idx, config.BNpcBaseId, displayName, config.EnemyList, world.Coordinates, config.IsVisible);
        // Mirror the native position/rotation writes above into the C#-side fields.
        enemy.SetPosition(config.Placement);
        enemy.SetTargetable(config.Targetable);
        if (!config.IsVisible) enemy.SetVisible(false);
        return enemy;
    }

    private static float ResolveHitboxRadius(uint modelCharaId, float scale)
    {
        const float DefaultUnscaledRadius = 0.5f;
        var sheet = Plugin.DataManager.GetExcelSheet<ModelChara>();
        var unscaled = DefaultUnscaledRadius;
        if (sheet.TryGetRow(modelCharaId, out var row) && row.Unknown0 > 0f)
            unscaled = row.Unknown0;
        return unscaled * scale;
    }

    public override void Despawn()
    {
        if (registeredNow)
        {
            var bc = BattleCharaPtr;
            if (bc != null) BattleCharaSpawn.UnregisterFromCharacterManager(bc);
            registeredNow = false;
        }
        Movement.Follow(null);
        cast.Despawn();
        base.Despawn();
    }

    public void SetTargetable(bool targetable)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return;
        if (targetable)
        {
            chara->TargetableStatus |= ObjectTargetableFlags.IsTargetable | ObjectTargetableFlags.Unk1;
            // chara->RenderFlags &= ~VisibilityFlags.Nameplate;
        }
        else
        {
            chara->TargetableStatus &= ~(ObjectTargetableFlags.IsTargetable | ObjectTargetableFlags.Unk1);
            // chara->RenderFlags |= VisibilityFlags.Nameplate;
        }
    }

    // Only meaningful when the spawn config set EnemyListMode.Manual.
    public void SetInEnemyList(bool inEnemyList)
    {
        if (EnemyListMode != EnemyListMode.Manual)
        {
            Plugin.Log.Warning($"SetInEnemyList({inEnemyList}) ignored: SimEnemy {DisplayName} has mode {EnemyListMode}; declare EnemyListMode.Manual in EnemySpawnConfig to use explicit toggles.");
            return;
        }
        manualInEnemyList = inEnemyList;
    }

    public void SetVisible(bool visible) => desiredVisible = visible;

    public void Follow(SimCharacter? target = null, float speed = 6f) => Movement.Follow(target, speed);

    private void ReconcileVisibility()
    {
        if (desiredVisible == currentVisible) return;
        var obj = BattleCharaPtr;
        if (obj == null) return;
        if (!obj->IsReadyToDraw()) return;
        if (desiredVisible) obj->EnableDraw();
        else                obj->DisableDraw();
        currentVisible = desiredVisible;
        SyncRegistration();
    }

    // Align registration with visibility after each transition (see registeredNow).
    // The SetModelState morph path doesn't touch currentVisible, so registration
    // stays stable through a morph.
    private void SyncRegistration()
    {
        var bc = BattleCharaPtr;
        if (bc == null) return;
        if (currentVisible && !registeredNow)
        {
            BattleCharaSpawn.RegisterInCharacterManager(bc);
            registeredNow = true;
        }
        else if (registeredNow && !currentVisible)
        {
            BattleCharaSpawn.UnregisterFromCharacterManager(bc);
            registeredNow = false;
        }
    }

    // Authoritative draw state (DrawObject.Flags bits 0 and 3, set by Enable/DisableDraw).
    // False during the async model-load window where DrawObject is still null.
    private bool IsEngineVisible()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return false;
        var draw = obj->DrawObject;
        return draw != null && draw->IsVisible;
    }
    
    public bool Cast(uint actionId, Vector3? targetLocation = null, float? castSeconds = null, GameObjectId? targetId = null, float omenDelay = 0f, float omenRotate = 0f, byte animationVariation = 0)
    {
        // targetLocation stays scenario-local; SimCast lifts to world at native boundaries.
        return cast.Start(actionId, targetLocation, castSeconds, targetId, omenDelay, omenRotate, animationVariation);
    }

    public void NativeCast(uint actionId, ActionType actionType, float omenDelay, float castTime, bool interruptible, float? rotation = null, Vector3? position = null, GameObjectId? targetId = null, GameObjectId? ballistaId = null)
    {
        cast.NativeCast(actionId, actionType, omenDelay, castTime, interruptible, rotation, position, targetId, ballistaId);
    }

    public void NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariaton, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, GameObjectId? actionTargetId = null, GameObjectId? ballistaId = null)
    {
        cast.NativeActionEffect(actionId, animationLock, spellId, animationVariaton, actionType, flags, rotation, position, animationTargetId, actionTargetId, ballistaId);
    }

    public override bool AnimationLock => cast.IsBusy;

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        ReconcileVisibility();
        cast.Tick(deltaSeconds);
    }

    public CharacterFind<T> Find<T>(List<T> targets) where T : IPositioned
    {
        return new CharacterFind<T>(targets);
    }
}
