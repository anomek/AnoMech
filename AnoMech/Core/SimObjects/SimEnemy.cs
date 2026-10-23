using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Native.Interfaces;
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

// How the engine shows the actor on spawn; the value is the SpawnNpcPacket DisplayFlags sent
// unless EnemySpawnConfig.DisplayFlags gives retail's exact ones.
// Visible          — drawn at spawn.
// HiddenUntilShown — bit 0x20000; hidden until a warp_end/show timeline reveals it (bosses,
//                    clones, arm units).
// HiddenUntilPopIn — bit 0x20; hidden until ActorControl.PopIn (0x24) ~0.8s
//                    later (summoned adds, pets). UNVERIFIED on our actors.
// InvisibleHelper  — the meshless helper that casts a fight's AoEs (ModelChara 480); never
//                    drawn, and spawned with no MP.
public enum SpawnVisibility : uint
{
    Visible = 0x4000B,
    HiddenUntilShown = 0x6000B,
    HiddenUntilPopIn = 0x4002B,
    InvisibleHelper = 0x40008,
}

// Whether a SimEnemy shows in the _EnemyList HUD (sent in the Hater packet by EnemyList).
// Always          — listed while alive.
// OnlyWhenVisible — follows the engine's DrawObject.IsVisible; for adds that warp
//                   in/out. Transforming bosses use Always.
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
    SpawnVisibility Visibility = SpawnVisibility.Visible,
    Placement Placement = default,
    uint ModelCharaId = 0,
    byte? InitialModeAttributeFlags = null, // null = engine default; set when the idle sub-mesh variant differs (Omega-M = 0x10)
    // Retail's exact DisplayFlags where they differ from the Visibility's own. What the bits outside
    // the Visibility ones do is UNVERIFIED.
    uint? DisplayFlags = null,
    // Weapon drawn and in combat from spawn. Retail never engages some actors. UNVERIFIED: a drawn
    // weapon swaps the model's idle for its battle idle, hiding idle-only visuals (Razor Plume).
    bool Engaged = true,
    // Retail's layout instance for the BNpc, in the territory's own layout. The Razor Plume's idle
    // visual needs this or the director EventId BattleCharas sends; UNVERIFIED which.
    uint LayoutId = 0,
    // False for an ally the duty spawns (FRU's Gaia and Ryne): it sends battalion 0, as retail
    // does, so the client treats it as friendly.
    bool IsHostile = true,
    NpcAppearance? Appearance = null,
    // Statuses retail's spawn packet already carries, for an actor that holds them from the start
    // (FRU's Visions levitate). Up to 30.
    IReadOnlyList<SpawnStatus>? Statuses = null,
    byte ModelState = 0,
    // ModelScale row; retail sends 0 for most BNpcs.
    byte ModelScale = 0);

public readonly record struct SpawnStatus(ushort StatusId, ushort Param = 0);

// The weapons, gear and customize bytes retail's spawn packet carries. The client builds human and
// demihuman models from these parts; a demihuman spawned without them draws nothing.
public sealed record NpcAppearance(
    ulong MainHand = 0,
    ulong OffHand = 0,
    // Per slot (head, body, hands, legs, feet, ears, neck, wrists, right ring, left ring):
    // model id | variant << 16 | stain << 24.
    uint[]? Equipment = null,
    // The 26 CustomizeData bytes.
    byte[]? Customize = null);

public sealed class SimEnemy : SimNpc
{
    // The cast packets and the animation lock live in Casting; EnemyActionHandler decides when
    // they go out and what the action does.
    private readonly EnemyActionHandler actions;
    private readonly EventScheduler events;

    // The server's own ceiling: bosses turn at most ~145° per ~0.3s movement tick.
    private const float BossTurnSpeed = MathF.PI * 8f / 3f;

    // How long the model takes to turn from its facing to `point`.
    public float SecondsToFace(Vector3 point) => MathF.Abs(MathUtil.NormalizeRotation(this.Placement().Face(point).Rotation - Rotation)) / BossTurnSpeed;

    private readonly SimulatedMotion motion;

    // SpawnConfig.Targetable is only the spawn-time default.
    private bool desiredTargetable;
    public bool Targetable => desiredTargetable;

    // Diagnostic: EnableDraw/IsVisible don't say whether each equipment/body model slot
    // finished streaming; logged shortly after spawn and again a few seconds later.
    private int slotCheckFrames;
    private bool slotCheckDone;

    public uint BNpcBaseId { get; }

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
    public bool InEnemyList => !Dead && EnemyListMode switch
    {
        EnemyListMode.Always          => true,
        EnemyListMode.Never           => false,
        EnemyListMode.Manual          => manualInEnemyList,
        EnemyListMode.OnlyWhenVisible => IsEngineVisible(),
        _ => false,
    };

    public bool IsCasting => Casting.IsCasting;
    public uint CastActionId => Casting.ActionId;
    public float CastProgress => Casting.Progress;
    public Vector3? CastTargetLocation => Casting.TargetLocation;
    public float CastTotalSeconds => Casting.Total;

    // The engine's own NpcSpawn handler creates the actor and enables its draw, as for a real
    // server spawn.
    private SimEnemy(IBattleCharaProxy proxy, uint bNpcBaseId, string displayName, EnemyListMode enemyListMode, SimWorld world)
        : base(proxy, world.Coordinates,
            self => new SimulatedMotion(() => self.Proxy, world.Coordinates, self.ActorControl, ObstacleField.Empty, BossTurnSpeed, npc: true))
    {
        motion = (SimulatedMotion)Motion;
        BNpcBaseId = bNpcBaseId;
        DisplayName = displayName;
        EnemyListMode = enemyListMode;
        actions = new EnemyActionHandler(this, Casting, world);
        events = world.Events;
        SimEvents = world.SimEvents;
    }

    // Spawns the BattleChara through the engine's own NpcSpawn handler (see
    // IBattleCharas.SpawnBattleNpcFromPacket) and wraps it; the actor arrives a few frames later.
    // Caller is responsible for registering the result in the world's children list (so
    // reset/teardown covers it). Null on missing LocalPlayer, BNpcBase miss, or no free slot.
    internal static SimEnemy? Spawn(EnemySpawnConfig config, SimWorld world)
    {
        if (!Natives.BattleCharas.LocalPlayer.Exists) return null;
        return SpawnFromPacket(config, world);
    }

    // Null when the handler refused the packet.
    private static SimEnemy? SpawnFromPacket(EnemySpawnConfig config, SimWorld world)
    {
        if (Natives.BattleCharas.SpawnBattleNpcFromPacket(config, world.Coordinates.ToGlobal(config.Placement), out var entityId) is not { } chara)
            return null;
        var displayName = Natives.Data.BNpcName(config.NameId) ?? $"BNpc {config.BNpcBaseId:X}";
        var enemy = new SimEnemy(chara, config.BNpcBaseId, displayName, config.EnemyList, world)
        {
            SpawnConfig = config,
        };
        foreach (var status in config.Statuses ?? []) enemy.AdoptSpawnStatus(status.StatusId, status.Param);
        enemy.BeginPacketSpawn(entityId, config.Placement);
        return enemy;
    }

    protected override void OnPacketSpawned(IBattleCharaProxy chara)
    {
        var targetableBefore = chara.TargetableStatus;
        if (SpawnConfig.Targetable) // enemies spawn untargettable
            SetTargetable(SpawnConfig.Targetable);
        // Retail engages enemies ~0.8s into the pull and adds on arrival; the sim has no pre-pull,
        // so they are engaged from their spawn.
        if (SpawnConfig.Engaged)
        {
            ActorControl.SetWeaponDrawn(true);
            ActorControl.SetInCombat(true);
        }
        DiagnosticLog.Info($"[SimEnemy.SpawnFromPacket] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) created by the engine after {PacketSpawnFrames} frames: {DescribeDrawState()} "
            + $"Targetable=0x{targetableBefore:X}->0x{chara.TargetableStatus:X} name=\"{chara.Name}\" pos {chara.Position}.");
    }

    /// <summary>
    /// Sets the targetable status of this <see cref="SimEnemy"/>, which will reflect in their Nameplate and in the Enemy List (if visible there).
    /// </summary>
    /// <param name="targetable">
    /// If <see langword="true"/>, then the Nameplate will be visible, and able to target them using the Enemy List.
    /// If <see langword="false"/>, then the Nameplate will not be visible, and not able to target them using the Enemy List.
    /// </param>
    public override void SetTargetable(bool targetable)
    {
        desiredTargetable = targetable;
        base.SetTargetable(targetable);
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
            ActorControl.ClearTarget();
        }
        else
        {
            ActorControl.SetTarget(target.GameObjectId);

            if (follow)
            {
                Follow(target);
            }
        }
    }

    // Follow, stopping `reach` past touching hitboxes: the distance an enemy closes to.
    public void Follow(SimCharacter? target, float speed, float reach)
    {
        if (MovementLocked && target.IsAlive()) return;
        motion.Follow(target, speed, reach);
    }

    // Make for the target's centre and stay on it. A live actor's hitbox can change mid-chase, so
    // homing can't be a follow reach worked out from the hitboxes as it starts.
    public void HomeIn(SimCharacter? target, float speed)
    {
        if (MovementLocked && target.IsAlive()) return;
        motion.HomeIn(target, speed);
    }

    // A Provoke landed on this enemy. Nothing changes unless a scenario models the enmity it moves.
    public event Action<SimCharacter>? Provoked;

    public void Provoke(SimCharacter by) => Provoked?.Invoke(by);

    // A party member's attack landed on this enemy, for a scenario that hands it to its first attacker.
    public event Action<SimCharacter>? Attacked;

    public void NoteAttack(SimCharacter by) => Attacked?.Invoke(by);

    // Shown HP; the sim deals no damage, so this is only ever set.
    public float HpFraction => Proxy is { Exists: true, MaxHealth: > 0 } chara ? (float)chara.Health / chara.MaxHealth : 1f;

    public void SetHpFraction(float fraction)
    {
        if (Proxy is { Exists: true } chara) SendHealth((uint)(chara.MaxHealth * Math.Clamp(fraction, 0f, 1f)));
    }

    // AnimLock (8) is the mode the client holds an actor in while an action animation plays.
    public void SetMode(CharacterModes mode, byte param = 0) => ActorControl.SetMode(mode, param);

    private void TickPacketSpawnCheckpoints()
    {
        if (PacketSpawnPending || PacketSpawnFailed) return;
        if (PacketSpawnFrames is 5 or 30 or 90 or 210)
        {
            var targetable = Proxy?.TargetableStatus ?? 0;
            DiagnosticLog.Info($"[SimEnemy.PacketSpawn] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) +{PacketSpawnFrames} frames: {DescribeDrawState()} Targetable=0x{targetable:X} -- {DescribeActionTimeline()}");
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

    internal string DescribeDrawState() => Proxy?.DescribeDrawState() ?? "no BattleChara";

    // UNVERIFIED what it does: FRU's dragon heads get 4 at their first catch, when retail shrinks
    // them from their spawn scale.
    public void Activate(uint state) => ActorControl.Activate(state);

    public void SetAnimationState(byte slot, byte value) => ActorControl.SetAnimationState(slot, value);

    // Authoritative draw state (DrawObject.Flags bits 0 and 3, set by Enable/DisableDraw).
    // False during the async model-load window where DrawObject is still null.
    private bool IsEngineVisible() => Proxy?.IsDrawObjectVisible ?? false;

    // A purely visual cast: just the action's bar and animation, hitting no one. For an action that
    // has effects or needs more configuration, define an EnemyAction and use the overload taking it.
    public void Cast(uint actionId, CastTarget target = default, float animationLock = 0.6f, byte animationVariation = 0) =>
        actions.Start(new EnemyAction(actionId) { Cast = new() { AnimationLock = animationLock } }, target, animationVariation);

    // An action with effects (area, damage, statuses, ...) or configuration beyond the sheet's; a
    // purely visual one needs no EnemyAction, use Cast(actionId).
    public void Cast(EnemyAction action, CastTarget target = default, byte animationVariation = 0) =>
        actions.Start(action, target, animationVariation);

    // Interrupts the cast still on its bar: neither its effect nor its mechanics go out.
    public void CancelCast(CastCancelReason reason) => actions.CancelCast(reason);

    public override bool AnimationLock => Casting.IsBusy || actions.AwaitingRelease;

    // Stays turned toward the target, holding still while casting or animation-locked; null releases it.
    public void KeepFacing(SimCharacter? target) => motion.KeepFacing(target);

    public const float DefaultCorpseFadeDelay = 8f;
    private const float CorpseFadeDuration = 1.7f;

    // Killed by damage. corpseFadeDelay counts from the death animation; a boss leaving at a
    // phase transition fades sooner (FRU Fatebreaker: 3s).
    public void Defeat(float corpseFadeDelay = DefaultCorpseFadeDelay)
    {
        if (Dead) return;
        actions.CancelCast(CastCancelReason.Interrupted);
        GoDown(byEnvironment: false);
        ScheduleCorpse(0f, corpseFadeDelay);
    }

    // Only for DefeatCasterEffect. The enemy dies by its own action (a self-destruct): it goes
    // down as the action lands and plays its death animation once the action's lock ends.
    internal void DefeatByOwnAction(EnemyAction action)
    {
        if (Dead) return;
        actions.CancelCast(CastCancelReason.Cancelled);
        MarkDead();
        ActorControl.SetMode(CharacterModes.Dead, 2);
        var animationDelay = action.Cast.AnimationLock;
        events.Add(animationDelay, PlayDeath);
        ScheduleCorpse(animationDelay, DefaultCorpseFadeDelay);
    }

    private void ScheduleCorpse(float animationDelay, float corpseFadeDelay)
    {
        events.Add(animationDelay + corpseFadeDelay, ActorControl.FadeCorpse);
        events.Add(animationDelay + corpseFadeDelay + CorpseFadeDuration, Despawn);
    }

    private const float FadeOutDuration = 1.3f;

    public bool FadingOut { get; private set; }

    // A spent add or a boss leaving at a phase transition: gone without dying.
    public void FadeOut()
    {
        FadingOut = true;
        ActorControl.FadeOut();
        events.Add(FadeOutDuration, Despawn);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        TickTimelineWatch(deltaSeconds);
        TickPacketSpawnCheckpoints();

        if (!slotCheckDone && SpawnConfig.Visibility == SpawnVisibility.Visible)
        {
            slotCheckFrames++;
            if (slotCheckFrames == 1) LogModelSlotState("+1 frame");
            else if (slotCheckFrames == 5) LogModelSlotState("+5 frames");
            else if (slotCheckFrames == 210)
            {
                LogModelSlotState("+210 frames (~3.5s)");
                slotCheckDone = true;
            }
        }
    }

    private void LogModelSlotState(string label)
    {
        if (Proxy is not { Exists: true } chara) return;
        DiagnosticLog.Info($"[SimEnemy.LogModelSlotState] {DisplayName} (goid 0x{GameObjectId.ObjectId:X}) {label}: {chara.DescribeModelSlots()}");
    }

    public CharacterFind<T> Find<T>(List<T> targets) where T : IPositioned
    {
        return new CharacterFind<T>(targets);
    }
}
