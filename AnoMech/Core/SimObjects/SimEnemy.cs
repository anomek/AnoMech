using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
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
    bool PacketSpawnEnableDraw = false);

public sealed class SimEnemy : SimNpc
{
    // Cast bar, action-effect release, omen telegraph, and animation lock live in
    // SimCast. SimEnemy just converts target coords to world space and reads IsBusy.
    private readonly SimCast cast;
    private readonly EnemyActionHandler actions;

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
            if (Proxy is not { Exists: true } chara) return field;
            var name = chara.Name;
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

    internal SimEnemy(IBattleCharaProxy proxy, uint bNpcBaseId, string displayName, EnemyListMode enemyListMode, SimWorld world, bool packetSpawned = false) : base(proxy, world.Coordinates, pendingDraw: !packetSpawned)
    {
        BNpcBaseId = bNpcBaseId;
        DisplayName = displayName;
        EnemyListMode = enemyListMode;
        this.packetSpawned = packetSpawned;
        cast = new SimCast(this, world.Coordinates);
        actions = new EnemyActionHandler(this, world);
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

    // Spawns the BattleChara (see IBattleCharas.SpawnBattleNpc) and wraps it. Caller is responsible
    // for registering the result in the world's children list (so reset/teardown covers it).
    // Null on missing LocalPlayer, BNpcBase miss, or no free slot.
    internal static SimEnemy? Spawn(EnemySpawnConfig config, SimWorld world)
    {
        if (!Natives.BattleCharas.LocalPlayer.Exists) return null;
        if (config.NpcSpawnTemplate is not null) return SpawnFromPacket(config, world);

        if (Natives.BattleCharas.SpawnBattleNpc(config, world.Coordinates.ToGlobal(config.Placement)) is not { } chara) return null;
        var enemy = new SimEnemy(chara, config.BNpcBaseId, chara.Name, config.EnemyList, world)
        {
            SpawnConfig = config,
        };
        // Mirror the native position/rotation writes into the C#-side fields.
        enemy.SetPosition(config.Placement);
        enemy.SetTargetable(config.Targetable);
        if (!config.IsVisible) enemy.SetVisible(false);
        return enemy;
    }

    // The engine's own NpcSpawn handler builds the actor from a captured packet, the way the
    // real client does. Null when the handler refused it.
    private static SimEnemy? SpawnFromPacket(EnemySpawnConfig config, SimWorld world)
    {
        if (Natives.BattleCharas.SpawnBattleNpcFromPacket(config, world.Coordinates.ToGlobal(config.Placement), out var entityId) is not { } chara)
            return null;
        var displayName = Natives.Data.BNpcName(config.NameId) ?? $"BNpc {config.BNpcBaseId:X}";
        var enemy = new SimEnemy(chara, config.BNpcBaseId, displayName, config.EnemyList, world, packetSpawned: true)
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
        if (!PacketSpawnPending || Proxy is not { } chara) return;
        if (!chara.Exists)
        {
            if (packetSpawnFrames < PacketSpawnTimeoutFrames) return;
            PacketSpawnPending = false;
            PacketSpawnFailed = true;
            Natives.BattleCharas.ReleaseSlot(chara.Slot);
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] {DisplayName}: nothing arrived at slot {chara.Slot} within {packetSpawnFrames} frames -- the engine dropped the spawn; the caller falls back.");
            return;
        }
        PacketSpawnPending = false;
        Natives.BattleCharas.ReleaseSlot(chara.Slot);
        if (chara.EntityId != packetEntityId)
        {
            PacketSpawnFailed = true;
            DiagnosticLog.Warn($"[SimEnemy.SpawnFromPacket] {DisplayName}: slot {chara.Slot} holds entity 0x{chara.EntityId:X}, not the packet's 0x{packetEntityId:X} -- not ours; treating the spawn as failed.");
            DetachSlot();
            return;
        }
        var targetableBefore = chara.TargetableStatus;
        SetTargetable(SpawnConfig.Targetable);
        if (SpawnConfig.PacketSpawnEnableDraw) RequestDraw();
        DiagnosticLog.Info($"[SimEnemy.SpawnFromPacket] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) created by the engine after {packetSpawnFrames} frames: {DescribeDrawState()} "
            + $"Targetable=0x{targetableBefore:X}->0x{chara.TargetableStatus:X} name=\"{chara.Name}\" pos {chara.Position}.");
    }

    public override void Despawn()
    {
        Movement.Follow(null);
        cast.Despawn();
        if (PacketSpawnPending)
        {
            // The engine will still fill the slot; SimWorld's orphan sweep despawns the actor
            // when it arrives.
            PacketSpawnPending = false;
            if (Proxy is { } chara) Natives.BattleCharas.NoteOrphan(chara.Slot, packetEntityId);
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
        Proxy?.SetTargetable(targetable);
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
            Proxy?.SetTarget(NoTarget);
        }
        else
        {
            Proxy?.SetTarget(target.GameObjectId);

            if (follow)
            {
                Follow(target);
            }
        }
    }

    private static readonly GameObjectId NoTarget = 0xE0000000;

    public void SetVisible(bool visible) => desiredVisible = visible;

    // Same ActorControl 607 fade as SimEventObject.FadeOut.
    public void FadeOut()
    {
        if (EntityId == 0) return;
        ActorControl(607, EntityId, 1, 0, 100);
    }

    // For adds whose HP bar the sim drains on a timer.
    public void SetHealth(uint maxHealth, float fraction)
    {
        if (Proxy is not { Exists: true } chara) return;
        chara.MaxHealth = maxHealth;
        chara.Health = (uint)MathF.Ceiling(maxHealth * Math.Clamp(fraction, 0f, 1f));
    }

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

    private void ApplyModelHidden() => Proxy?.SetModelHidden(modelHidden);

    // AnimLock (8) is the mode the client holds an actor in while an action animation plays.
    public void SetMode(CharacterModes mode, byte param = 0)
    {
        LastMode = ((byte)mode, param);
        ModeSeq++;
        HasEngineState = true;
        Proxy?.SetMode(mode, param);
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
            if (Proxy is { HasDrawObject: true } drawn)
            {
                drawn.IsDrawObjectVisible = false;
                packetModelHidden = true;
                DiagnosticLog.Info($"[SimEnemy.PacketSpawn] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) draw object built at +{packetSpawnFrames} frames -- hidden (IsVisible=false): {DescribeDrawState()}");
            }
        }
        if (packetSpawnFrames is 5 or 30 or 90 or 210)
        {
            var targetable = Proxy?.TargetableStatus ?? 0;
            DiagnosticLog.Info($"[SimEnemy.PacketSpawn] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) +{packetSpawnFrames} frames: {DescribeDrawState()} Targetable=0x{targetable:X} -- {DescribeActionTimeline()}");
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
        // Slot ids decide "changed"; playback positions advance every frame.
        var ids = Proxy?.DescribeTimelineSlotIds() ?? "-";
        var changed = ids != timelineWatchLast;
        if (changed || timelineWatchFrames < 15 || timelineWatchFrames % 15 == 0)
        {
            var state = DescribeActionTimeline();
            DiagnosticLog.Info($"[SimEnemy.TimelineWatch] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) +{timelineWatchFrames}f: {state}{(changed ? "" : " (slots unchanged)")}");
        }
        timelineWatchLast = ids;
        timelineWatchFrames++;
        if (timelineWatchRemaining <= 0f)
            DiagnosticLog.Info($"[SimEnemy.TimelineWatch] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) watch ended after {timelineWatchFrames} frames.");
    }

    internal string DescribeActionTimeline() => Proxy?.DescribeActionTimeline() ?? "no BattleChara";

    // Debug: the engine's own resource loader for the base slot's scheduler timeline.
    public ulong ForceLoadBaseTimeline()
    {
        ForceLoadTimelineSeq++;
        HasEngineState = true;
        if (Proxy is not { Exists: true } chara) return 0;
        var result = chara.LoadBaseTimelineResources();
        DiagnosticLog.Info($"[SimEnemy] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) LoadTimelineResources on slot 0 -> {result}: {DescribeActionTimeline()}");
        return result;
    }

    // Debug: the sequencer's own entry point, with no action effect around it.
    public void PlayTimelineDirect(ushort timelineId)
    {
        DirectTimelineId = timelineId;
        DirectTimelineSeq++;
        HasEngineState = true;
        Proxy?.PlayTimelineDirect(timelineId);
    }

    // Debug holds for a timeline whose VFX dies the moment its slot clears: Loop re-queues it
    // as its own loop, Base sets TimelineContainer.BaseOverride. Release clears both.
    public void HoldTimelineLoop(ushort timelineId)
    {
        NoteTimelineHold(TimelineHoldKind.Loop, timelineId);
        Proxy?.PlayActionTimeline(timelineId, timelineId, baseOverride: null);
    }

    public void HoldTimelineBase(ushort timelineId)
    {
        NoteTimelineHold(TimelineHoldKind.Base, timelineId);
        Proxy?.SetBaseOverride(timelineId);
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
        if (Proxy is not { Exists: true } chara) return;
        chara.SetBaseOverride(0);
        if (chara.GetSlotTimeline(0) == timelineId)
            chara.SetSlotTimeline(0, 0);
        DiagnosticLog.Info($"[SimEnemy] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) timeline hold released: {DescribeActionTimeline()}");
    }

    internal string DescribeDrawState() => Proxy?.DescribeDrawState() ?? "no BattleChara";

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
        Proxy?.SetAnimationState(arg2, arg3);
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
                DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) is packet-spawned -- visibility left to the engine: {DescribeDrawState()}.");
            }
            return;
        }
        var firstTick = !loggedInitialVisibility;
        if (firstTick)
        {
            loggedInitialVisibility = true;
            var drawObject = Proxy is not { Exists: true } chara ? "no BattleChara" : chara.HasDrawObject ? "present" : "null";
            DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid 0x{GameObjectId.ObjectId:X}) first tick: desiredVisible={desiredVisible} currentVisible={currentVisible} DrawObject={drawObject}.");
        }

        // A model still streaming when it was hidden shows itself again once its load completes,
        // so a hidden enemy is checked against the live flag every tick.
        var reshown = !desiredVisible && currentVisible == desiredVisible && IsEngineVisible();

        // One explicit native write on the first tick regardless of agreement: currentVisible's
        // initial true is an assumption, and a peer's reconstructed doppel was hidden despite it.
        if (!firstTick && desiredVisible == currentVisible && !reshown)
        {
            return;
        }

        if (Proxy is not { HasDrawObject: true } drawn)
        {
            return;
        }

        drawn.IsDrawObjectVisible = desiredVisible;
        currentVisible = desiredVisible;

        if (reshown)
        {
            if (!loggedReshown)
                DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid 0x{GameObjectId.ObjectId:X}) was shown again by the engine while hidden -- hid it again at pos {Position}.");
            loggedReshown = true;
            return;
        }
        DiagnosticLog.Info($"[SimEnemy.ReconcileVisibility] {DisplayName} (BNpcBase {BNpcBaseId}, goid 0x{GameObjectId.ObjectId:X})'s visibility was set to {desiredVisible} at pos {Position}");
    }

    private bool loggedReshown;

    // Authoritative draw state (DrawObject.Flags bits 0 and 3, set by Enable/DisableDraw).
    // False during the async model-load window where DrawObject is still null.
    private bool IsEngineVisible() => Proxy?.IsDrawObjectVisible ?? false;

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

    public void Cast(EnemyAction action) => actions.Start(action, null, null);

    // A null target casts on the caster.
    public void Cast(EnemyAction action, SimCharacter? target) => actions.Start(action, target, null);

    // Scenario-local ground target, fixed at the call.
    public void Cast(EnemyAction action, Vector3 location) => actions.Start(action, null, location);

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
                    DiagnosticLog.Warn($"[SimEnemy] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) has a model slot stuck unloaded after 3.5s -- forcing one ReloadModel retry.");
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
    private bool LogModelSlotState(string label)
    {
        if (Proxy is not { Exists: true } chara) return false;
        DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) {label}: {chara.DescribeModelSlots()}");
        return chara.HasUnloadedModelSlot ?? false;
    }

    public CharacterFind<T> Find<T>(List<T> targets) where T : IPositioned
    {
        return new CharacterFind<T>(targets);
    }
}
