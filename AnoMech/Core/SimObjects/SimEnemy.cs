using AnoMech.Core.Game;
using AnoMech.Helpers;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Base;
using InteropGenerator.Runtime;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
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

// How a VFX-only timeline is pinned in place after its action fired (see
// SimEnemy.HoldTimelineLoop/HoldTimelineBase): None also carries the id being released.
public enum TimelineHoldKind
{
    None,
    Loop,
    Base,
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
    byte? InitialModeAttributeFlags = null, // null = engine default; set when the idle sub-mesh variant differs (Omega-M = 0x10)
                                            // Only for a ModelChara.Type==0 (Character) row, whose look is Customize+equipment driven;
                                            // without it the engine never builds a DrawObject for such a spawn.
    CustomizeData? Customize = null,
    // A captured real NpcSpawn packet body (see UmadRealPackets): the engine's own spawn
    // handler builds the actor from it, and of the fields above only NameId, Targetable,
    // EnemyList and Placement still apply.
    byte[]? NpcSpawnTemplate = null,
    // Packet path only: request the draw object ourselves. The engine never draws a packet
    // actor on its own, and a caster without a draw object has its action timeline cleared
    // within frames. With IsVisible=false the built model is hidden the moment it appears.
    bool PacketSpawnEnableDraw = false,
    bool IsHostile = true,
    ushort SpawnTimeline = 0);

public sealed unsafe class SimEnemy : SimNpc
{
    // Cast bar, action-effect release, omen telegraph, and animation lock live in
    // SimCast. SimEnemy just converts target coords to world space and reads IsBusy.
    private readonly SimCast cast;
    private ushort pendingSpawnTimeline;

    // Peer-only smoothing for ApplyNetworkPosition, same model as SimNetworkPuppet: the
    // catch-up speed is a floor once the real snapshot interval is known, anything beyond
    // NetworkSnapThreshold (a scripted teleport, a lag spike) snaps, extrapolation only feeds
    // the visual glide, and rotation is stepped as well.
    private const float NetworkCatchUpSpeed = 20f;
    private const float NetworkSnapThreshold = 15f;
    private const ushort NetworkRunTimelineId = 22; // mirrors Game.Movement.RunTimelineId

    private const float NetworkIntervalSmoothingFactor = 0.3f;
    private const float MinNetworkPacingWindowSeconds = 0.05f;
    private float timeSinceLastNetworkUpdate;
    private float estimatedNetworkUpdateInterval = 0.05f;

    private const float MaxNetworkExtrapolationSeconds = 1f;
    private Vector3 networkVelocity;

    private const float NetworkAngularCatchUpSpeed = MathF.PI * 20f;

    private Vector3? networkTargetPosition;
    private float networkTargetRotation;
    private bool networkInterpAnimActive;
    private bool networkMoving;

    // Below this, two consecutive snapshots read as "same spot" rather than motion.
    private const float NetworkMovementEpsilon = 0.01f;

    // networkMoving comes from whether the host's reported position is advancing, not from
    // local interpolation state (see TickNetworkPosition).
    public void ApplyNetworkPosition(Vector3 position, float rotation)
    {
        if (networkTargetPosition is { } previous)
        {
            networkMoving = Vector3.DistanceSquared(previous, position) > NetworkMovementEpsilon * NetworkMovementEpsilon;
            estimatedNetworkUpdateInterval += (timeSinceLastNetworkUpdate - estimatedNetworkUpdateInterval) * NetworkIntervalSmoothingFactor;
            networkVelocity = timeSinceLastNetworkUpdate > MinNetworkPacingWindowSeconds
                ? (position - previous) / timeSinceLastNetworkUpdate
                : Vector3.Zero;
        }
        networkTargetPosition = position;
        networkTargetRotation = rotation;
        timeSinceLastNetworkUpdate = 0f;
    }

    // The run animation is keyed off networkMoving rather than "interpolation caught up":
    // Movement.Tick resets the native animation whenever AnimationLock holds (a boss casts
    // constantly), and per-frame snapshots make "arrived" true almost every tick. The host's
    // own position is just as frozen during its cast, so this self-corrects.
    private void TickNetworkPosition(float deltaSeconds)
    {
        if (networkTargetPosition is not { } rawTarget) return;
        timeSinceLastNetworkUpdate += deltaSeconds;

        var target = rawTarget + networkVelocity * MathF.Min(timeSinceLastNetworkUpdate, MaxNetworkExtrapolationSeconds);
        var basePos = Position;
        var delta = target - basePos;
        var dist = delta.Length();
        var remainingWindow = MathF.Max(estimatedNetworkUpdateInterval - timeSinceLastNetworkUpdate, MinNetworkPacingWindowSeconds);
        var step = MathF.Max(dist / remainingWindow, NetworkCatchUpSpeed) * deltaSeconds;
        var nextRotation = MathUtil.StepRotation(Rotation, networkTargetRotation, NetworkAngularCatchUpSpeed * deltaSeconds);
        if (dist > NetworkSnapThreshold)
        {
            // Logged: position otherwise rides silently in every snapshot.
            DiagnosticLog.Info($"[SimEnemy.TickNetworkPosition] {DisplayName} (BNpcBase {BNpcBaseId}) snapped {dist:F1}y (> {NetworkSnapThreshold}y threshold): {basePos} -> {target}.");
            SetPosition(new Placement(target, nextRotation));
        }
        else if (dist <= step)
            SetPosition(new Placement(target, nextRotation));
        else
            SetPosition(new Placement(basePos + delta / dist * step, nextRotation));

        if (networkMoving && !networkInterpAnimActive)
        {
            // Native entry point: movement smoothing is not a scenario cue to broadcast.
            PlayActionTimelineNative(NetworkRunTimelineId, baseOverride: NetworkRunTimelineId);
            networkInterpAnimActive = true;
        }
        else if (!networkMoving && networkInterpAnimActive)
        {
            ResetActionTimelineNative();
            networkInterpAnimActive = false;
        }
    }

    // Visibility runs through the DrawObject lifecycle: SetVisible records a desired
    // state; Tick's reconciler fires EnableDraw/DisableDraw once per change, gated on
    // IsReadyToDraw so toggles can't race the async model load. RenderFlags writes
    // were tried and don't reliably keep enemies visible — only this path does.
    // ReconcileVisibility still writes the native flag once on the first tick.
    private bool desiredVisible = true;
    private bool currentVisible = true;
    private bool loggedInitialVisibility;

    // SpawnConfig.Targetable is only the spawn-time default.
    private bool desiredTargetable;
    public bool Targetable => desiredTargetable;

    // Diagnostic: EnableDraw/IsVisible don't say whether each equipment/body model slot
    // finished streaming; logged shortly after spawn and again a few seconds later.
    private int slotCheckFrames;
    private bool slotCheckDone;
    private bool slotReloadAttempted;

    public uint BNpcBaseId { get; }

    // Lets a peer reconstruct the same doppel via world.SpawnEnemy.
    public EnemySpawnConfig SpawnConfig { get; internal set; }


    // Live via GameObject::GetName() so engine-driven renames propagate (the Name[] buffer is
    // never refreshed for doppels). Falls back to the spawn-time name mid-despawn.
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
        EnemyListMode.Always => true,
        EnemyListMode.Never => false,
        EnemyListMode.Manual => manualInEnemyList,
        EnemyListMode.OnlyWhenVisible => IsEngineVisible(),
        _ => false,
    };

    public bool IsCasting => cast.IsCasting;
    public int CastSeq => cast.CastSeq;
    public uint CastActionId => cast.ActionId;
    public float CastProgress => cast.Progress;
    public Vector3? CastTargetLocation => cast.TargetLocation;
    public GameObjectId? CastTargetId => cast.TargetId;
    public float CastTotalSeconds => cast.Total;
    public float CastOmenDelay => cast.OmenDelay;
    public float CastOmenRotate => cast.OmenRotate;
    public int LastInstantCastSeq => cast.LastInstantCastSeq;
    public uint LastInstantCastActionId => cast.LastInstantCastActionId;
    public Vector3? LastInstantCastTargetLocation => cast.LastInstantCastTargetLocation;
    public GameObjectId? LastInstantCastTargetId => cast.LastInstantCastTargetId;
    public GameObjectId? LastInstantCastActionTargetId => cast.LastInstantCastActionTargetId;
    public bool LastInstantCastIsNativeEffect => cast.LastInstantCastIsNativeEffect;
    public float LastInstantCastAnimationLock => cast.LastInstantCastAnimationLock;
    public string? LastInstantCastRawPacket => cast.LastInstantCastRawPacket;

    public void NoteRawActionEffect(uint actionId, string captureName, float animationLock)
        => cast.NoteRawActionEffect(actionId, captureName, animationLock);

    // The last SetVisible value; IsEngineVisible lags behind the async model load.
    public bool Visible => desiredVisible;

    internal SimEnemy(int index, uint bNpcBaseId, string displayName, EnemyListMode enemyListMode, Coordinates coordinates, bool packetSpawned = false) : base(index, coordinates, pendingDraw: !packetSpawned)
    {
        BNpcBaseId = bNpcBaseId;
        DisplayName = displayName;
        EnemyListMode = enemyListMode;
        this.packetSpawned = packetSpawned;
        cast = new SimCast(this, coordinates);
    }

    // Created by the engine's own NpcSpawn handler (SpawnFromPacket); the engine owns its draw
    // and visibility state, so the reconcilers below leave it alone.
    private readonly bool packetSpawned;
    private int packetSpawnFrames;
    private uint packetEntityId;
    // The engine creates a packet-spawned actor a few frames after HandleSpawnNpcPacket
    // returns, so the wrapper polls its reserved slot each tick. Failed = nothing arrived
    // within PacketSpawnTimeoutFrames; the caller falls back to its regular spawn.
    public bool PacketSpawnPending { get; private set; }
    public bool PacketSpawnFailed { get; private set; }
    private bool packetModelHidden;
    private const int PacketSpawnTimeoutFrames = 20;

    // Pending counts as alive, or SimWorld's reaper would drop the wrapper before its actor
    // exists; every consumer of IsActive null-checks the native pointer.
    public override bool IsActive => PacketSpawnPending || base.IsActive;

    // A Character-type mesh won't build from Customize alone. PartyPresets' White Mage set,
    // not Graven Image's real gear.
    private static readonly (DrawDataContainer.EquipmentSlot Slot, uint ItemId)[] Type0PlaceholderEquipment =
    [
        (DrawDataContainer.EquipmentSlot.Head, 2902),
        (DrawDataContainer.EquipmentSlot.Body, 3225),
        (DrawDataContainer.EquipmentSlot.Hands, 3687),
        (DrawDataContainer.EquipmentSlot.Legs, 3463),
        (DrawDataContainer.EquipmentSlot.Feet, 3894),
    ];

    // Allocates a BattleChara, configures it as a BattleNpc per the supplied
    // config, and returns a SimEnemy wrapping it. Caller is responsible for
    // registering the result in the world's children list (so reset/teardown
    // covers it). Returns null on missing LocalPlayer, BNpcBase miss, or
    // CreateBattleChara failure.
    internal static SimEnemy? Spawn(EnemySpawnConfig config, SimWorld world)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return null;
        if (config.NpcSpawnTemplate is { } template) return SpawnFromPacket(config, template, world);

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

        if (!CharacterManagerHelper.CreateCharacter(out var idx, out var obj)) return null;

        var gameObj = (GameObject*)obj;
        var chara = (BattleChara*)obj;
        // SetupBNpc populates ModelContainer (incl. ModeAttributeFlags) from BNpcBase and must
        // run before the overrides below. Skipped for a Type 0 row with a Customize: a PC-style
        // actor is Customize+equipment driven and SetupBNpc left it permanently un-rendered.
        // Gated on Customize, not Type 0 alone: the invisible Type 0 helpers rely on the
        // BattleNpc path loading no mesh, and routing them through the Pc path built a player
        // mesh out of the reused slot's stale CustomizeData.
        var pcStyle = modelChara.Type == 0 && config.Customize is not null;
        if (pcStyle)
        {
            chara->ObjectKind = ObjectKind.Pc;
            // Match PartyCreator.SpawnNative field for field: a reused slot's stale skeleton id
            // and equipment compete with the engine's Race/Tribe resolution and half-load a
            // broken mesh.
            chara->ModelContainer.ModelCharaId = 0;
            chara->ModelContainer.ModelSkeletonId = 0;
            chara->VfxScale = 0.4f;   // PartyCreator.LalafellVfxScale
            chara->Height = 0.6f;     // PartyCreator.LalafellHeight
            chara->Mode = CharacterModes.Normal;
            chara->ModeParam = 0;
        }
        else
        {
            chara->CharacterSetup.SetupBNpc(config.BNpcBaseId, config.NameId);
            chara->ObjectKind = ObjectKind.BattleNpc;
            chara->ModelContainer.ModelCharaId = (int)modelCharaId;
        }
        chara->Position = world.Coordinates.ToGlobal(config.Placement.Position);
        chara->SetRotation(MathUtil.NormalizeRotation(config.Placement.Rotation));
        var scale = config.Scale > 0f ? config.Scale : bnpc.Scale;
        chara->Scale = scale;
        chara->SEPack = bnpc.SEPack;

        var nativeHitbox = true;

        // From Client::Game::Character::CharacterSetupContainer_SetupRaw
        switch (modelChara.Type)
        {
            // A Customize-less Type 0 (invisible helper) matches no case and keeps nativeHitbox.
            case 0 when pcStyle:
                // The engine resolves a PC skeleton from Race/Tribe once CustomizeData is
                // written; the hitbox is PartyCreator's fixed 0.5.
                if (config.Customize is { } customize)
                {
                    chara->DrawData.CustomizeData = customize;
                    // Zero every slot first: the reused slot's previous occupant leaves stale ids
                    // in the slots the placeholder set doesn't write.
                    foreach (DrawDataContainer.EquipmentSlot slot in Enum.GetValues<DrawDataContainer.EquipmentSlot>())
                        chara->DrawData.Equipment(slot).Value = 0;
                    // An all-zero CustomizeData is the real invisible helpers' own spawn data and
                    // must stay bare.
                    if (customize.Race != 0)
                    {
                        var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                        foreach (var (slot, itemId) in Type0PlaceholderEquipment)
                            if (itemSheet.TryGetRow(itemId, out var item))
                                chara->DrawData.Equipment(slot).Value = item.ModelMain;
                    }
                }
                chara->HitboxRadius = config.HitboxRadius > 0f ? config.HitboxRadius : 0.5f;
                nativeHitbox = false;
                break;
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
            chara->ModelContainer.UnscaledRadius = ModelContainerPointers.CalculateUnscaledRadius(&chara->ModelContainer);
            chara->HitboxRadius = chara->Scale * chara->ModelContainer.UnscaledRadius; // From Client::Game::Character::ModelContainer_UpdateHitboxRadius
        }

        // Engine-resolved name (vfunc 6), same source as the nameplate. Empty for Type 0 (it
        // resolves from state SetupBNpc sets up), so fall back to the BNpcName sheet.
        var displayName = gameObj->GetName().ToString();
        if (string.IsNullOrEmpty(displayName) && config.NameId != 0
            && Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>().TryGetRow(config.NameId, out var bnpcName))
            displayName = bnpcName.Singular.ExtractText();
        if (string.IsNullOrEmpty(displayName)) displayName = $"BNpc {config.BNpcBaseId:X}";
        GameObjectHelper.WriteName(gameObj, displayName);
        obj->RenderFlags = 0;

        chara->CharacterSetup.CopyFromCharacter((Character*)chara, CharacterSetupContainer.CopyFlags.None);

        chara->BattleNpcSubKind = BattleNpcSubKind.Combatant;
        chara->MaxHealth = 1_000_000;
        chara->Health = 1_000_000;
        chara->Battalion = config.IsHostile ? (byte)4 : checked((byte)bnpc.Battalion.RowId);
        chara->IsHostile = config.IsHostile;
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

        DiagnosticLog.Info($"[SimEnemy.Spawn] BNpcBase {config.BNpcBaseId}: resolved modelCharaId={modelCharaId} (sheet default {bnpc.ModelChara.RowId}), scale={scale} (sheet default {bnpc.Scale}), hitboxRadius={chara->HitboxRadius} (nativeHitbox={nativeHitbox}), modelChara.Type={modelChara.Type}, ModelSkeletonId={chara->ModelContainer.ModelSkeletonId}, ModeAttributeFlags=0x{chara->ModelContainer.ModeAttributeFlags:X2} -- at index {idx}, goid {gameObj->GetGameObjectId()}, pos {config.Placement.Position}, visible {config.IsVisible}.");
        var enemy = new SimEnemy(idx, config.BNpcBaseId, displayName, config.EnemyList, world.Coordinates)
        {
            SpawnConfig = config,
        };
        enemy.pendingSpawnTimeline = config.SpawnTimeline;
        // Mirror the native position/rotation writes above into the C#-side fields.
        enemy.SetPosition(config.Placement);
        enemy.SetTargetable(config.Targetable);
        if (!config.IsVisible) enemy.SetVisible(false);
        return enemy;
    }

    // Outside the engine's player/server-actor ranges and CreateCharacter's 0xE00000xx ids.
    private const uint PacketSpawnEntityIdBase = 0x4000FE00u;

    // The engine's own NpcSpawn handler builds the actor from a captured packet, the way the
    // real client does. Only per-instance fields are patched: slot, position/rotation, the
    // English name, and a dangling owner reference. Null when the handler leaves the slot empty.
    private static SimEnemy? SpawnFromPacket(EnemySpawnConfig config, byte[] template, SimWorld world)
    {
        if (template.Length != sizeof(SpawnNpcPacket))
        {
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] template is {template.Length} bytes, expected {sizeof(SpawnNpcPacket)}.");
            return null;
        }
        var characterManager = CharacterManager.Instance();
        if (characterManager == null) return null;
        var idx = CharacterManagerHelper.FindFreeIndex();
        if (idx < 0)
        {
            DiagnosticLog.Warn("[SimEnemy.SpawnFromPacket] no free BattleChara slot.");
            return null;
        }

        var packet = new SpawnNpcPacket();
        fixed (byte* src = template) Buffer.MemoryCopy(src, &packet, sizeof(SpawnNpcPacket), template.Length);
        var entityId = PacketSpawnEntityIdBase + (uint)idx;
        var globalPos = world.Coordinates.ToGlobal(config.Placement.Position);
        packet.Common.SpawnIndex = (byte)idx;
        packet.Common.Position = globalPos;
        packet.Common.Rotation = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(config.Placement.Rotation));
        // The capture's owner reference names an actor that doesn't exist here.
        if (packet.Common.ObjectType is >= 0x40000000 and < 0xE0000000) packet.Common.ObjectType = 0xE0000000;

        var displayName = $"BNpc {config.BNpcBaseId:X}";
        if (config.NameId != 0 && Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>().TryGetRow(config.NameId, out var bnpcName))
            displayName = bnpcName.Singular.ExtractText();
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(displayName);
        var nameField = (byte*)&packet + 0x10 + 0x232;   // SpawnNpcPacket.Common (+0x10) . _name (+0x232, 32 bytes)
        for (var i = 0; i < 32; i++) nameField[i] = i < nameBytes.Length && i < 31 ? nameBytes[i] : (byte)0;

        DiagnosticLog.Info($"[SimEnemy.SpawnFromPacket] BNpcBase {packet.Common.BaseId} NameId {packet.Common.NameId} ModelChara {packet.Common.ModelChara} "
            + $"DisplayFlags=0x{packet.Common.DisplayFlags:X} Kind={packet.Common.ObjectKind}/{packet.Common.SubKind} Mode={packet.Common.CharacterMode} "
            + $"Level={packet.Common.Level} EventId=0x{packet.Common.EventId:X} LayoutId=0x{packet.Common.LayoutId:X} -> index {idx}, entity 0x{entityId:X}, "
            + $"pos {globalPos}, rot {config.Placement.Rotation:F3}.");
        try
        {
            PacketDispatcher.HandleSpawnNpcPacket(entityId, &packet);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] HandleSpawnNpcPacket threw {e.GetType().Name}: {e.Message}");
            return null;
        }
        // The actor arrives a few frames later (see PacketSpawnPending); hold the slot for it.
        CharacterManagerHelper.Reserve(idx);
        var enemy = new SimEnemy(idx, config.BNpcBaseId, displayName, config.EnemyList, world.Coordinates, packetSpawned: true)
        {
            SpawnConfig = config,
            packetEntityId = entityId,
            PacketSpawnPending = true,
        };
        enemy.SeedTransform(config.Placement.Position, config.Placement.Rotation);
        // The packet's own flags hide the model; this only keeps Visible (sampled for peers) honest.
        enemy.SetVisible(config.IsVisible);
        enemy.ProbePacketSpawn();
        return enemy;
    }

    // Polled each tick while pending; once the engine fills the slot, the sim-side spawn
    // settings (targetability) go on.
    private void ProbePacketSpawn()
    {
        if (!PacketSpawnPending) return;
        var obj = BattleCharaPtr;
        if (obj == null)
        {
            if (packetSpawnFrames < PacketSpawnTimeoutFrames) return;
            PacketSpawnPending = false;
            PacketSpawnFailed = true;
            CharacterManagerHelper.Release(Index);
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] {DisplayName}: nothing arrived at slot {Index} within {packetSpawnFrames} frames -- the engine dropped the spawn; the caller falls back.");
            return;
        }
        PacketSpawnPending = false;
        CharacterManagerHelper.Release(Index);
        if (obj->EntityId != packetEntityId)
        {
            PacketSpawnFailed = true;
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] {DisplayName}: slot {Index} holds entity 0x{obj->EntityId:X}, not the packet's 0x{packetEntityId:X} -- not ours; treating the spawn as failed.");
            DetachSlot();
            return;
        }
        var targetableBefore = (byte)obj->TargetableStatus;
        SetTargetable(SpawnConfig.Targetable);
        if (SpawnConfig.PacketSpawnEnableDraw) RequestDraw();
        DiagnosticLog.Info($"[SimEnemy.SpawnFromPacket] {DisplayName} (goid {GameObjectId}) created by the engine after {packetSpawnFrames} frames: {DescribeDrawState()} "
            + $"ObjectKind={obj->ObjectKind} SubKind={obj->BattleNpcSubKind} Mode={obj->Mode}/{obj->ModeParam} Targetable=0x{targetableBefore:X}->0x{(byte)obj->TargetableStatus:X} "
            + $"ModelSkeletonId={obj->ModelContainer.ModelSkeletonId} Race={obj->DrawData.CustomizeData.Race} name=\"{((GameObject*)obj)->GetName()}\" pos {obj->Position}.");
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
        pendingSpawnTimeline = 0;
        Movement.Follow(null);
        cast.Despawn();
        if (PacketSpawnPending)
        {
            // The engine will still fill the slot; SimWorld's orphan sweep despawns the actor
            // when it arrives.
            PacketSpawnPending = false;
            CharacterManagerHelper.NoteOrphan(Index, packetEntityId);
        }
        base.Despawn();
    }

    /// <summary>
    /// Sets the targetable status of this <see cref="SimEnemy"/>, which will reflect in their Nameplate and in the Enemy List (if visible there).
    /// </summary>
    /// <param name="targetable">
    /// If <see langword="true"/>, then the Nameplate will be visible, and able to target them using the Enemy List.
    /// If <see langword="false"/>, then the Nameplate will not be visible, and not able to target them using the Enemy List.
    /// </param>
    public void SetTargetable(bool targetable)
    {
        desiredTargetable = targetable;
        var chara = BattleCharaPtr;
        if (chara == null) return;
        if (targetable)
        {
            chara->TargetableStatus |= (ObjectTargetableFlags)1 | ObjectTargetableFlags.IsTargetable;
        }
        else
        {
            chara->TargetableStatus &= ~((ObjectTargetableFlags)1 | ObjectTargetableFlags.IsTargetable);
        }
    }

    public void SetHealth(uint current, uint maximum)
    {
        if (maximum == 0) throw new System.ArgumentOutOfRangeException(nameof(maximum));
        var chara = BattleCharaPtr;
        if (chara == null) return;
        chara->MaxHealth = maximum;
        chara->Health = System.Math.Min(current, maximum);
    }

    /// <summary>
    /// Only executed when <see cref="EnemyListMode"/> is <see cref="EnemyListMode.Manual"/>
    /// </summary>
    /// <param name="inEnemyList">Will make the Enemy appear or not in the Enemy List (Enmity List)</param>
    public void SetVisibleInEnemyList(bool inEnemyList)
    {
        if (EnemyListMode != EnemyListMode.Manual)
        {
            Plugin.Log.Warning($"SetInEnemyList({inEnemyList}) ignored: SimEnemy {DisplayName} has mode {EnemyListMode}; declare EnemyListMode.Manual in EnemySpawnConfig to use explicit toggles.");
            return;
        }
        manualInEnemyList = inEnemyList;
    }

    /// <summary>
    /// Sets the target of this <see cref="SimEnemy"/>.
    /// </summary>
    /// <remarks>For now, this is purely visual and does not contain any logic relating to auto-attacks or similar.</remarks>
    /// <param name="target">The <see cref="SimCharacter.GameObjectId"/> will be retrieved and used as the TargetId. If <see langword="null"/>, then the target is cleared.</param>
    /// <param name="follow">If <paramref name="target"/> is valid, this will determine if the <see cref="SimEnemy"/> should now follow <paramref name="target"/> or not.</param>
    /// <param name="speed">If <paramref name="target"/> is valid and <paramref name="follow"/> is <see langword="true"/>, this will be the speed that the <see cref="SimEnemy"/> will follow the <paramref name="target"/></param>
    public void SetTarget(SimCharacter? target, bool follow = true, float speed = 6f)
    {
        if (target == null)
        {
            BattleCharaPtr->TargetId = 0xE0000000;
        }
        else
        {
            BattleCharaPtr->TargetId = target.GameObjectId;

            if (follow)
            {
                Follow(target);
            }
        }
    }

    public void SetVisible(bool visible) => desiredVisible = visible;

    // RenderFlags Model|Nameplate. The engine then drops the DrawObject entirely, so this does
    // not keep action VFX alive on a hidden carrier; kept for the Flood carrier A/B.
    // Re-asserted every tick because EnableDraw resets RenderFlags.
    private bool modelHidden;

    // The engine-level state below is driven by explicit scenario calls and sampled for peers.
    // Tracked here rather than read back from native, which the run animation and the cast
    // pipeline overwrite every frame; each is edge-triggered on its own seq.
    public bool ModelHidden => modelHidden;
    public (byte Mode, byte Param)? LastMode { get; private set; }
    public int ModeSeq { get; private set; }
    public TimelineHoldKind TimelineHoldState { get; private set; }
    public ushort TimelineHoldId { get; private set; }
    public int TimelineHoldSeq { get; private set; }
    public ushort DirectTimelineId { get; private set; }
    public int DirectTimelineSeq { get; private set; }
    public int ForceLoadTimelineSeq { get; private set; }

    // Sticky, so an ordinary enemy carries no engine block while a carrier that has been driven
    // keeps reporting: dropping the block after the last call would strand a peer on it.
    public bool HasEngineState { get; private set; }

    public void SetModelHidden(bool hidden)
    {
        modelHidden = hidden;
        HasEngineState = true;
        ApplyModelHidden();
    }

    private void ApplyModelHidden()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        const VisibilityFlags bits = VisibilityFlags.Model | VisibilityFlags.Nameplate;
        var go = (GameObject*)obj;
        if (modelHidden) go->RenderFlags |= bits;
        else go->RenderFlags &= ~bits;
    }

    // AnimLock (8) is the mode the client holds an actor in while an action animation plays.
    public void SetMode(CharacterModes mode, byte param = 0)
    {
        LastMode = ((byte)mode, param);
        ModeSeq++;
        HasEngineState = true;
        var obj = BattleCharaPtr;
        if (obj == null) return;
        ((Character*)obj)->SetMode(mode, param);
    }

    // Logs when the packet actor's draw object appears and hides it per IsVisible.
    private void TickPacketSpawnCheckpoints()
    {
        packetSpawnFrames++;
        if (PacketSpawnPending)
        {
            ProbePacketSpawn();
            return;
        }
        if (PacketSpawnFailed) return;
        if (SpawnConfig.PacketSpawnEnableDraw && !SpawnConfig.IsVisible && !packetModelHidden)
        {
            var drawn = BattleCharaPtr;
            if (drawn != null && drawn->DrawObject != null)
            {
                drawn->DrawObject->IsVisible = false;
                packetModelHidden = true;
                DiagnosticLog.Info($"[SimEnemy.PacketSpawn] {DisplayName} (goid {GameObjectId}) draw object built at +{packetSpawnFrames} frames -- hidden (IsVisible=false): {DescribeDrawState()}");
            }
        }
        if (packetSpawnFrames is 5 or 30 or 90 or 210)
        {
            var obj = BattleCharaPtr;
            var targetable = obj == null ? 0 : (byte)obj->TargetableStatus;
            DiagnosticLog.Info($"[SimEnemy.PacketSpawn] {DisplayName} (goid {GameObjectId}) +{packetSpawnFrames} frames: {DescribeDrawState()} Targetable=0x{targetable:X} -- {DescribeActionTimeline()}");
        }
    }

    private float timelineWatchRemaining;
    private int timelineWatchFrames;
    private string? timelineWatchLast;

    // Per-frame trace of the action-timeline state for `seconds`, logged on change plus a
    // heartbeat. The first sample is taken synchronously.
    public void StartTimelineWatch(float seconds)
    {
        timelineWatchRemaining = seconds;
        timelineWatchFrames = 0;
        timelineWatchLast = null;
        TickTimelineWatch(0f);
    }

    private void TickTimelineWatch(float deltaSeconds)
    {
        if (timelineWatchRemaining <= 0f) return;
        timelineWatchRemaining -= deltaSeconds;
        var obj = BattleCharaPtr;
        // Slot ids decide "changed"; playback positions advance every frame.
        var ids = DescribeSlotIds(obj);
        var changed = ids != timelineWatchLast;
        if (changed || timelineWatchFrames < 15 || timelineWatchFrames % 15 == 0)
        {
            var state = obj == null
                ? "no BattleChara"
                : $"{DescribeActionTimeline()} Mode={obj->Mode}/{obj->ModeParam} RenderFlags={((GameObject*)obj)->RenderFlags} Casting={obj->CastInfo.IsCasting} rot={obj->Rotation:F3}";
            DiagnosticLog.Info($"[SimEnemy.TimelineWatch] {DisplayName} (goid {GameObjectId}) +{timelineWatchFrames}f: {state}{(changed ? "" : " (slots unchanged)")}");
        }
        timelineWatchLast = ids;
        timelineWatchFrames++;
        if (timelineWatchRemaining <= 0f)
            DiagnosticLog.Info($"[SimEnemy.TimelineWatch] {DisplayName} (goid {GameObjectId}) watch ended after {timelineWatchFrames} frames.");
    }

    // Which ActionTimeline id each sequencer slot holds, with its playback position, so a
    // timeline that ran out reads differently from one cut short.
    internal string DescribeActionTimeline() => DescribeActionTimeline(BattleCharaPtr);

    internal static string DescribeActionTimeline(BattleChara* obj)
    {
        if (obj == null) return "no BattleChara";
        if (obj->Timeline.TimelineSequencer.Parent == null) return "no sequencer";
        var slots = new System.Text.StringBuilder();
        for (uint slot = 0; slot < 14; slot++)
        {
            var id = obj->Timeline.TimelineSequencer.GetSlotTimeline(slot);
            if (id == 0) continue;
            slots.Append($"[{slot}]={id}");
            var scheduler = obj->Timeline.TimelineSequencer.GetSchedulerTimeline(slot);
            if (scheduler != null)
            {
                slots.Append($"@{scheduler->CurrentTimestamp:F2}");
                if (slot == 0)
                {
                    slots.Append($"({scheduler->ActionTimelineKey})");
                    // Base-slot internals: load state, group, resolved resource name and the
                    // sequencer's shadow id arrays.
                    var state = *(int*)((byte*)scheduler + 0x78);
                    slots.Append($"[state={state} group=0x{(nint)scheduler->OwningGroup:X}");
                    var resource = scheduler->SchedulerResource;
                    if (resource == null) slots.Append(" res=none");
                    else
                    {
                        var name = resource->Name.DataPointer != null ? ((CStringPointer)resource->Name.DataPointer).ToString() : "(inline)";
                        slots.Append($" res=\"{name}\" handle={(resource->Resource == null ? "none" : $"LoadState={resource->Resource->LoadState}")}");
                    }
                    slots.Append($" ids2/3/4={obj->Timeline.TimelineSequencer.TimelineIds2[0]}/{obj->Timeline.TimelineSequencer.TimelineIds3[0]}/{obj->Timeline.TimelineSequencer.TimelineIds4[0]}]");
                }
            }
            slots.Append(' ');
        }
        return $"slots {(slots.Length == 0 ? "(all empty)" : slots.ToString().TrimEnd())} slot0speed={obj->Timeline.TimelineSequencer.GetSlotSpeed(0):F2} BaseOverride={obj->Timeline.BaseOverride} Speed={obj->Timeline.OverallSpeed:F2} DrawObject={(obj->DrawObject == null ? "null" : obj->DrawObject->IsVisible ? "visible" : "hidden")}";
    }

    // Debug: the engine's own resource loader for the base slot's scheduler timeline.
    public ulong ForceLoadBaseTimeline()
    {
        ForceLoadTimelineSeq++;
        HasEngineState = true;
        var obj = BattleCharaPtr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return 0;
        var scheduler = obj->Timeline.TimelineSequencer.GetSchedulerTimeline(0);
        if (scheduler == null)
        {
            DiagnosticLog.Info($"[SimEnemy] {DisplayName} ForceLoadBaseTimeline: no scheduler timeline in slot 0.");
            return 0;
        }
        var result = scheduler->LoadTimelineResources();
        DiagnosticLog.Info($"[SimEnemy] {DisplayName} (goid {GameObjectId}) LoadTimelineResources on slot 0 -> {result}: {DescribeActionTimeline()}");
        return result;
    }

    // Debug: the sequencer's own entry point, with no action effect around it.
    public void PlayTimelineDirect(ushort timelineId)
    {
        DirectTimelineId = timelineId;
        DirectTimelineSeq++;
        HasEngineState = true;
        var obj = BattleCharaPtr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return;
        obj->Timeline.TimelineSequencer.PlayTimeline(timelineId);
    }

    private static string DescribeSlotIds(BattleChara* obj)
    {
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return "-";
        var ids = new System.Text.StringBuilder();
        for (uint slot = 0; slot < 14; slot++)
        {
            var id = obj->Timeline.TimelineSequencer.GetSlotTimeline(slot);
            if (id != 0) ids.Append($"[{slot}]={id} ");
        }
        return ids.Length == 0 ? "(all empty)" : ids.ToString();
    }

    // Debug holds for a timeline whose VFX dies the moment its slot clears: Loop re-queues it
    // as its own loop, Base sets TimelineContainer.BaseOverride. Release clears both.
    public void HoldTimelineLoop(ushort timelineId)
    {
        NoteTimelineHold(TimelineHoldKind.Loop, timelineId);
        var obj = BattleCharaPtr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return;
        obj->Timeline.PlayActionTimeline(timelineId, timelineId);
    }

    public void HoldTimelineBase(ushort timelineId)
    {
        NoteTimelineHold(TimelineHoldKind.Base, timelineId);
        var obj = BattleCharaPtr;
        if (obj == null) return;
        obj->Timeline.BaseOverride = timelineId;
    }

    private void NoteTimelineHold(TimelineHoldKind kind, ushort timelineId)
    {
        TimelineHoldState = kind;
        TimelineHoldId = timelineId;
        TimelineHoldSeq++;
        HasEngineState = true;
    }

    public void ReleaseTimelineHold(ushort timelineId)
    {
        NoteTimelineHold(TimelineHoldKind.None, timelineId);
        var obj = BattleCharaPtr;
        if (obj == null) return;
        obj->Timeline.BaseOverride = 0;
        if (obj->Timeline.TimelineSequencer.Parent != null && obj->Timeline.TimelineSequencer.GetSlotTimeline(0) == timelineId)
            obj->Timeline.TimelineSequencer.SetSlotTimeline(0, 0);
        DiagnosticLog.Info($"[SimEnemy] {DisplayName} (goid {GameObjectId}) timeline hold released: {DescribeActionTimeline()}");
    }

    internal string DescribeDrawState()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return "no BattleChara";
        var go = (GameObject*)obj;
        var draw = obj->DrawObject;
        return $"DrawObject={(draw == null ? "null" : draw->IsVisible ? "visible" : "hidden")} "
            + $"RenderFlags={go->RenderFlags} ModelCharaId={obj->ModelContainer.ModelCharaId} "
            + $"VfxScale={go->VfxScale:F2} Height={go->Height:F2} Scale={go->Scale:F2}";
    }

    // Alias kept for existing call sites.
    public void PlayAnimationTimeline(ushort timelineId, ushort loopId = 0, ushort baseOverride = 0)
        => PlayActionTimeline(timelineId, loopId, baseOverride);

    // Same as AnimationTimelineId for a raw SetAnimationState call, which has no replication
    // path of its own.
    public (int Arg2, int Arg3)? AnimationState { get; private set; }
    public int AnimationStateSeq { get; private set; }

    public void SetAnimationState(int arg2, int arg3)
    {
        AnimationState = (arg2, arg3);
        AnimationStateSeq++;
        var chara = BattleCharaPtr;
        if (chara == null) return;
        TimelineContainerPointers.SetAnimationState(&chara->Timeline, arg2, arg3);
    }

    private void ReconcileVisibility()
    {
        if (packetSpawned)
        {
            // The engine's spawn handler owns a packet actor's visibility; writing IsVisible
            // here would fight it.
            if (!loggedInitialVisibility)
            {
                loggedInitialVisibility = true;
                DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (goid {GameObjectId}) is packet-spawned -- visibility left to the engine: {DescribeDrawState()}.");
            }
            return;
        }
        var firstTick = !loggedInitialVisibility;
        if (firstTick)
        {
            loggedInitialVisibility = true;
            var chara = BattleCharaPtr;
            DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid {GameObjectId}) first tick: desiredVisible={desiredVisible} currentVisible={currentVisible} DrawObject={(chara == null ? "no BattleChara" : chara->DrawObject == null ? "null" : "present")}.");
        }

        var obj = BattleCharaPtr;
        // A model still streaming when it was hidden shows itself again once its load completes,
        // so a hidden enemy is checked against the live flag every tick.
        var reshown = !desiredVisible && currentVisible == desiredVisible
                      && obj != null && obj->DrawObject != null && obj->DrawObject->IsVisible;

        // One explicit native write on the first tick regardless of agreement: currentVisible's
        // initial true is an assumption, and a peer's reconstructed doppel was hidden despite it.
        if (!firstTick && desiredVisible == currentVisible && !reshown)
        {
            return;
        }

        if (obj == null || obj->DrawObject == null)
        {
            return;
        }

        obj->DrawObject->IsVisible = desiredVisible;
        currentVisible = desiredVisible;

        if (reshown)
        {
            if (!loggedReshown)
                DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid {GameObjectId}) was shown again by the engine while hidden -- hid it again at pos {Position}.");
            loggedReshown = true;
            return;
        }
        DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid {GameObjectId})'s visibility was set to {desiredVisible} at pos {Position}");
    }

    private bool loggedReshown;

    // Authoritative draw state (DrawObject.Flags bits 0 and 3, set by Enable/DisableDraw).
    // False during the async model-load window where DrawObject is still null.
    private bool IsEngineVisible()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return false;
        var draw = obj->DrawObject;
        return draw != null && draw->IsVisible;
    }

    // Engine doesn't expose post-action animation-lock duration via EXD — the
    // real value only ships in the server's ActionEffect packet. 0.6s is a
    // reasonable approximation for most boss abilities; if a scenario needs
    // tighter timing we can derive per-action values from captured ACT logs.
    public bool Cast(uint actionId, Vector3? targetLocation = null, float? castSeconds = null, GameObjectId? targetId = null, float omenDelay = 0f, float omenRotate = 0f, byte animationVariation = 0, float animationLock = 0.6f, float? fireDelay = null)
    {
        Core.DiagnosticLog.Info(
            $"[SimEnemy] Cast: {Core.ActionLookup.Name(actionId)} ({actionId}) from ({Position.X:F1},{Position.Z:F1}) rot={Rotation:F3} castSeconds={castSeconds?.ToString("F2") ?? "default"}.");
        // targetLocation stays scenario-local; SimCast lifts to world at native boundaries.
        return cast.Start(actionId, targetLocation, castSeconds, targetId, omenDelay, omenRotate, animationVariation, animationLock, fireDelay);
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
        // Before ReconcileVisibility, so "become visible" and a position snap land in the same tick.
        TickNetworkPosition(deltaSeconds);
        var chara = BattleCharaPtr;
        if (pendingSpawnTimeline != 0 && desiredVisible && chara != null && chara->DrawObject != null
            && chara->IsReadyToDraw() && chara->Timeline.TimelineSequencer.Parent != null)
        {
            PlayActionTimeline(pendingSpawnTimeline);
            pendingSpawnTimeline = 0;
        }
        ReconcileVisibility();
        if (modelHidden) ApplyModelHidden();
        cast.Tick(deltaSeconds);
        TickTimelineWatch(deltaSeconds);
        if (packetSpawned) TickPacketSpawnCheckpoints();

        if (!slotCheckDone && desiredVisible)
        {
            slotCheckFrames++;
            if (slotCheckFrames == 1) LogModelSlotState("+1 frame");
            else if (slotCheckFrames == 5) LogModelSlotState("+5 frames");
            else if (slotCheckFrames == 210)
            {
                var anyStuck = LogModelSlotState("+210 frames (~3.5s)");
                // A slot still unloaded here is a failed load (HasModelInSlotLoaded cleared without
                // populating Models[]); ReloadModel's DisableDraw/EnableDraw cycle gives it one retry.
                if (anyStuck && !slotReloadAttempted)
                {
                    slotReloadAttempted = true;
                    DiagnosticLog.Warn($"[SimEnemy] {DisplayName} (goid {GameObjectId}) has a model slot stuck unloaded after 3.5s -- forcing one ReloadModel retry.");
                    ReloadModel();
                    slotCheckFrames = 0;
                }
                else
                {
                    slotCheckDone = true;
                }
            }
        }
    }

    // Returns true if any slot is still unloaded.
    private unsafe bool LogModelSlotState(string label)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return false;
        var draw = (CharacterBase*)chara->DrawObject;
        if (draw == null)
        {
            DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} (goid {GameObjectId}) {label}: DrawObject null.");
            return false;
        }
        var slotLoaded = Enumerable.Range(0, draw->SlotCount).Select(i => draw->ModelsSpan[i].Value != null).ToList();
        var slots = string.Join(",", slotLoaded.Select((loaded, i) => loaded ? $"{i}:loaded" : $"{i}:null"));
        DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} (goid {GameObjectId}) {label}: SlotCount={draw->SlotCount} HasModelInSlotLoaded=0x{draw->HasModelInSlotLoaded:X} HasModelFilesInSlotLoaded=0x{draw->HasModelFilesInSlotLoaded:X} slots=[{slots}].");

        // PerSlotStagingArea is the in-progress load record; its resource handle carries the file
        // path and the engine's own load/read/IO state, which says where a stuck slot stopped.
        for (var i = 0; i < draw->SlotCount; i++)
        {
            string resolvedPath;
            try { resolvedPath = draw->ResolveMdlPath((uint)i); }
            catch (Exception ex) { resolvedPath = $"<ResolveMdlPath threw: {ex.Message}>"; }

            if (draw->PerSlotStagingArea == null)
            {
                DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} slot {i} {label}: PerSlotStagingArea=null, ResolveMdlPath={resolvedPath}.");
                continue;
            }
            var staging = draw->PerSlotStagingArea[i];
            if (staging.ModelResourceHandle == null)
            {
                DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} slot {i} {label}: staging.Flags={staging.Flags}, ModelResourceHandle=null, ResolveMdlPath={resolvedPath}.");
                continue;
            }
            var rh = (ResourceHandle*)staging.ModelResourceHandle;
            DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} slot {i} {label}: staging.Flags={staging.Flags}, handle.FileName=\"{rh->FileName}\", LoadState={rh->LoadState}, ReadState={rh->ReadState}, OtherState={rh->OtherState}, LastIOResult={rh->LastIOResult}, RefCount={rh->RefCount}, ResolveMdlPath={resolvedPath}.");
        }

        return slotLoaded.Any(loaded => !loaded);
    }

    public CharacterFind<T> Find<T>(List<T> targets) where T : IPositioned
    {
        return new CharacterFind<T>(targets);
    }
}
