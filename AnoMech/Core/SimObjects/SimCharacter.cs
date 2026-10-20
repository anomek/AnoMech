using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// Common base for anything in the simulated world that has a BattleChara behind it
public abstract class SimCharacter : ISimObject, IPositioned
{
    // createMotion gets the character itself, which a derived constructor can't hand its base.
    private protected SimCharacter(Coordinates coordinates, Func<SimCharacter, CharacterMotion> createMotion)
    {
        Coordinates = coordinates;
        Motion = createMotion(this);
        Casting = new SimCast(this, coordinates);
    }

    // Authoritative list of statuses for character. In-game status list can be be little different and it's ok by design.
    private readonly List<SimStatus> statusList = [];

    // A status on it holds the others' timers (SimStatus.HoldingOtherTimers); set each tick before they run.
    internal bool StatusTimersHeld { get; private set; }

    // Null once the character has no native object to stand for (despawned).
    internal abstract IBattleCharaProxy? Proxy { get; }

    private protected CharacterMotion Motion { get; }

    internal SimCast Casting { get; }

    protected readonly Coordinates Coordinates;

    public virtual bool IsActive => Proxy is { Exists: true };

    // True while the character is rooted by an in-progress action (cast bar up or
    // release animation still playing): an ordered walk holds in place until it ends.
    // Types that simulate casts (SimEnemy) override it.
    public virtual bool AnimationLock => false;

    public GameObjectId GameObjectId => Proxy?.GameObjectId ?? default;
    public float HitboxRadius => Proxy?.HitboxRadius ?? 0f;
    public uint EntityId => Proxy?.EntityId ?? 0u;

    // For an NPC that wears this character's look (TEA's Fate Projections).
    public NpcAppearance? Appearance => Proxy?.Appearance;

    public virtual void Tick(float deltaSeconds)
    {
        StatusTimersHeld = statusList.Any(s => s.IsActive && s.HoldsOtherTimers);
        statusList.Update(deltaSeconds);
        FlushStatusList();
        Casting.Tick(deltaSeconds);
        Motion.Tick(deltaSeconds, new MotionGates(this.IsAlive(), MovementLocked, AnimationLock, SpeedFactor()));
        confusedChaseHold = MathF.Max(0f, confusedChaseHold - deltaSeconds);
    }

    private float confusedChaseHold;

    // A confused character (ActorControl.Confused) chases its nearest ally; this stands the chase
    // still for a while, as a mechanic's own pause. A forced move in flight holds it too.
    public void HoldConfusedChase(float seconds) => confusedChaseHold = MathF.Max(confusedChaseHold, seconds);

    internal bool ConfusedChaseHeld => confusedChaseHold > 0f || Motion.IsForcedMoving;


    public virtual void Despawn()
    {
        EndStatuses(null);
        Motion.Clear();
    }

    // Set by whoever seats or spawns the character in a world.
    internal SimEvents? SimEvents { get; set; }

    private void EndStatuses(StatusLossReason? reason)
    {
        foreach (var status in statusList) status.End(reason);
        statusList.Clear();
    }

    // -------------------------
    // Location Subsystem
    // -------------------------

    // What mechanics and the AI read, like the server's position; CharacterMotion says where each
    // kind of character gets it.
    public Vector3 Position => Motion.Position;
    public float Rotation => Motion.Rotation;

    // Yalms (XZ) between Position and the model at the start of the last tick.
    public float ModelDivergence => Motion.ModelDivergence;
    public float MaxModelDivergence => Motion.MaxModelDivergence;

    // Teleport: an ActorSetPos.
    public void SetPosition(Vector3 position) => Motion.SetPosition(position);
    public void SetPosition(Placement placement) => Motion.SetPlacement(placement);
    public void SetRotation(float rotation) => Motion.SetPlacement(new Placement(Position, rotation));

    public void TurnTo(float rotation) => Motion.TurnTo(rotation);
    public void Face(Vector3? target)
    {
        if (target is { } t) Motion.Face(t);
    }
    public void Face(IPositioned? target) => Face(target?.Position);

    // While a status locks movement only forced moves run (a forced Follow included); an AI order
    // is dropped, not deferred.
    public void MoveTo(Vector3 target, float speed = 6f, float? finalRotation = null)
    {
        if (this.IsAlive() && !MovementLocked) Motion.MoveTo(target, speed, finalRotation);
    }
    public void MoveTo(Placement p) => MoveTo(p.Position);
    public void StopMoving() => Motion.Stop();

    public void Intercept(SimTether? tether, float margin = 3f)
    {
        if (!MovementLocked) Motion.Intercept(tether, margin);
    }
    public bool IsIntercepting => Motion.IsIntercepting;
    public Vector3? MoveDestination => Motion.Destination;

    // An AI order; a mechanic that takes control uses ForceFollow. A null or dead target releases it.
    public void Follow(SimCharacter? target = null, float speed = 6f)
    {
        if (MovementLocked && target.IsAlive()) return;
        Motion.Follow(target, speed);
    }

    // Forced movement says what the mechanic does; the motion sends what retail shows for this kind
    // of character. Each call below names the replay packets it stands for and what the sim sends:
    // the local player gets the packet retail sends the pushed player; a bot gets what a replay shows
    // for another player, ActorMoves its own client reports (flag 0x08 on a slide). A KO'd character
    // isn't moved.

    // Replay: ActionEffect entry 0x1F on the target, value = Knockback row; for a Direction 6 row
    // also ActorControlSelf 0xE1 to the pushed player; for an NPC, the NPC knockback packet. Usually
    // reached through an EnemyAction's Knockback(knockbackId) effect, which also puts the entry in
    // the ActionEffect.
    // Sim: player - nothing (the client slides itself from the entry), or 0xE1 for Direction 6;
    // bot - ActorMove slide with flag 0x08; NPC - the NPC knockback packet.
    public void Knockback(Vector3 source, uint knockbackRow)
    {
        if (Natives.Data.Knockback(knockbackRow) is { } row) Force(new ForcedMove.Knockback(source, row));
    }

    // Replay: ActorControlSelf 0xE1 to the pushed player with no Knockback entry in the
    // ActionEffect; p1 heading, p2 distance x10000, p3 duration x10000. Away from a caster through
    // an EnemyAction's Push(distance, speed) effect (UMAD Vacuum Wave).
    // Sim: player - 0xE1; bot - ActorMove slide with flag 0x08; NPC - the NPC knockback packet.
    public void Push(float heading, float distance, float durationSeconds)
        => Force(new ForcedMove.Push(heading, distance, durationSeconds));

    public void Push(Vector3 source, float distance, float durationSeconds)
        => Force(new ForcedMove.PushAway(source, distance, durationSeconds));

    // Replay: ActorControl 0xF1 on the carried character, p1/p2 the quantized destination (UMAD P1
    // arrows); chained carries ~1s apart, each after the first following 0xE5 WarpEnd.
    // Sim: 0xF1 for player and bot alike; no ActorMove while it plays.
    public void CarryTo(Vector3 destination, ushort? timeline = null) => Force(new ForcedMove.Carry(destination, timeline));

    // Replay (FRU P2 Frost Armor): ActorControl 0xDF on every sliding player, p1/p2 the quantized
    // destination and the slide's heading, p4 ActionTimeline icefloor_short.
    // Sim: 0xDF for player and bot alike; no ActorMove while it plays.
    public void IceSlideTo(Vector3 destination, float durationSeconds) => Force(new ForcedMove.IceSlide(destination, durationSeconds));

    // A mechanic's snap, which a remote player's own client has to make; SetPosition is the
    // engine-side one. Replay and sim: ActorSetPos.
    public void TeleportTo(Placement placement) => Motion.Force(new ForcedMove.Teleport(placement));

    // Replay (TEA's Ordained statuses): ActorControlSelf 0x602 to the marched player about every
    // 0.25s, the next point on a straight line with its heading and speed, then 0x603 at the end;
    // another player's march is ActorMoves. Sim: player - those ActorControlSelfs (UNVERIFIED how
    // the client walks on them); bot - ActorMoves; a remote player - its own client marches it.
    public void MarchTo(Vector3 destination, float speed) => Force(new ForcedMove.March(destination, speed));

    // Replay (UMAD P1 Confusion): the confused player's own movement, nothing that names the
    // target. Live, never in a replay: forced-move end + follow {target, 1} to the confused player
    // about every 0.1s, which only turns them; what walks them in retail is UNVERIFIED. A null or
    // dead target releases it, which always applies.
    // Sim: player - forced-move end + approach {range, target}, which the client walks itself;
    // bot - ActorMoves of the navigator's chase.
    public void ForceFollow(SimCharacter? target, float speed = 6f)
    {
        if (this.IsAlive() || target is null) Motion.Force(new ForcedMove.Follow(target.IsAlive() ? target : null, speed));
    }

    private void Force(ForcedMove move)
    {
        if (this.IsAlive()) Motion.Force(move);
    }

    internal bool WalksWith(INavigated walker) => ReferenceEquals(Motion, walker);


    public void AttachLockonVfx(uint lockonId) => ActorControl.HeadMarker(lockonId);

    // FIXME: minor, keep track of tethers and slots attached to character
    public bool HasTetherInSlot0(ushort tetherId)
        => Proxy is { Exists: true } chara && chara.GetTetherId(0) == tetherId;

    // -------------------------
    // Status Subsystem
    // -------------------------

    // sourceObject distinguishes independent same-id instances (UMAD P1 Tele-portent applies
    // the same id twice with separate expiries). broadcastAddEvent false leaves a new status's
    // gain flytext to the caller, e.g. an ActionEffect's status entry. hidden keeps a new status out
    // of the status list until SimStatus.Reveal; refreshing one the client already shows ignores it.
    public SimStatus? AddStatus(ushort statusId, float duration = 0f, int stacks = 1, bool overrideStacks = false, GameObjectId sourceObject = default, bool broadcastAddEvent = true, bool hidden = false)
        => AddStatus(statusId, duration, stacks, overrideStacks, sourceObject, broadcastAddEvent, hidden, appliedBy: null);

    // appliedBy: the action whose effect applies it, for StatusAdded.
    internal SimStatus? AddStatus(ushort statusId, float duration, int stacks, bool overrideStacks, GameObjectId sourceObject, bool broadcastAddEvent,
        bool hidden, (EnemyAction Action, SimCharacter Caster)? appliedBy)
    {
        Core.DiagnosticLog.Info($"[SimCharacter] AddStatus: {DiagnosticName} gets status {statusId} (duration={duration:F1}, stacks={stacks}, overrideStacks={overrideStacks}, source=0x{sourceObject.ObjectId:X}).");
        if (FindStatus(statusId, sourceObject) is {} status)
        {
            var previous = status.Stacks;
            // overrideStacks: stacks is the absolute target; otherwise it's a
            // relative delta (negative consumes stacks).
            int delta = overrideStacks ? stacks - status.Stacks : stacks;
            // Reapplying refreshes a status; it gains stacks only up to the sheet's MaxStacks.
            if (!overrideStacks && delta > 0 && Natives.Data.Status(statusId) is { } row)
                delta = Math.Clamp(Math.Max((int)row.MaxStacks, 1) - status.Stacks, 0, delta);
            status.Reapply(duration, delta);
            if (status.Stacks == 0)
            {
                status.Despawn();   // last stack consumed → remove the status
                return null;
            }
            if (status.Slot >= 0 && !status.Hidden) statusListChanged = true;
            // Consuming a stack or holding an override where it is applies nothing.
            if (overrideStacks ? status.Stacks > previous : stacks > 0) RaiseStatusAdded(status, previous, appliedBy);
            return status;
        }

        // No existing status: a non-positive request has nothing to remove.
        if (stacks <= 0) return null;
        var added = SpawnStatus(statusId, duration, (ushort)stacks, sourceObject, broadcastAddEvent, hidden);
        RaiseStatusAdded(added, 0, appliedBy);
        return added;
    }

    private void RaiseStatusAdded(SimStatus status, ushort previousStacks, (EnemyAction Action, SimCharacter Caster)? appliedBy)
        => SimEvents?.Raise(new StatusAdded(this, status.StatusId, status.Stacks, previousStacks, appliedBy?.Action, appliedBy?.Caster));

    // For logging: the party role when available, else the type name.
    private string DiagnosticName => (this as ISimPartyMember)?.Role.ToString() ?? GetType().Name;

    public SimStatus AddStatusParam(ushort statusId, int param, float duration = 0f)
    {
        var added = SpawnStatus(statusId, duration, (ushort)param, default, broadcastAddEvent: true, hidden: false);
        RaiseStatusAdded(added, 0, appliedBy: null);
        return added;
    }

    // One the spawn packet already carried: tracked like any other, with nothing sent for it.
    internal void AdoptSpawnStatus(ushort statusId, ushort param)
        => statusList.Add(new SimStatus(this, statusId, 0f, param, default, FreeStatusSlot(statusId), hidden: false));

    private SimStatus SpawnStatus(ushort statusId, float duration, ushort param, GameObjectId sourceObject, bool broadcastAddEvent, bool hidden)
    {
        var status = new SimStatus(this, statusId, duration, param, sourceObject, FreeStatusSlot(statusId), hidden);
        statusList.Add(status);
        if (status.Slot < 0 || hidden) return status;
        statusListChanged = true;
        if (broadcastAddEvent) unannouncedGains.Add(status);
        return status;
    }

    private const int StatusSlots = 60;

    // The lowest free slot, as the server fills them. Ids without a sheet row get none: the party
    // list and nameplates look the row up.
    private int FreeStatusSlot(ushort statusId)
    {
        if (SimOnlyStatus.Is(statusId) || Natives.Data.Status(statusId) is null) return -1;
        for (var slot = 0; slot < StatusSlots; slot++)
            if (!statusList.Any(s => s.IsActive && s.Slot == slot))
                return slot;
        return -1;
    }

    private bool statusListChanged;
    private readonly List<SimStatus> unannouncedGains = [];

    internal void StatusListChanged() => statusListChanged = true;

    // Once per tick, however many statuses changed. A status removed and re-added within one tick
    // keeps its slot, so the client sees a refresh and doesn't replay the gain effects.
    private void FlushStatusList()
    {
        if (statusListChanged) SendStatusList(Proxy?.Health ?? 0, Proxy?.Mana ?? 0);
        foreach (var status in unannouncedGains)
            if (status.IsActive)
                ActorControl.GainEffect(status.StatusId, status.Stacks);
        unannouncedGains.Clear();
    }

    // HP no action dealt goes out on the status list, as the server's does.
    private protected void SendHealth(uint health) => SendStatusList(health, Proxy?.Mana ?? 0);

    private void SendStatusList(uint health, uint mana)
    {
        statusListChanged = false;
        if (Proxy is not { } chara) return;
        var slots = new StatusSlot[StatusSlots];
        foreach (var status in statusList)
            if (status.IsActive && status.Slot >= 0 && !status.Hidden)
                slots[status.Slot] = new StatusSlot(status.StatusId, status.Stacks, status.RemainingTime, (status.ShownSource ?? status.SourceObject).ObjectId);
        var shield = Mitigation.ShieldFraction(statusList.Where(s => s.IsActive).Select(s => s.StatusId));
        chara.ReceiveStatusList(slots, health, mana, (byte)Math.Clamp(shield * 100f, 0f, 100f));
    }

    public void RemoveStatus(ushort statusId)
    {
        RemoveStatus(statusId, default);
    }

    // Source-aware: an id the character holds twice (two appliers) needs the right one named.
    public void RemoveStatus(ushort statusId, GameObjectId sourceObject)
    {
        if (FindStatus(statusId, sourceObject) is not {} status) return;
        Core.DiagnosticLog.Info($"[SimCharacter] RemoveStatus: {DiagnosticName} loses status {statusId}.");
        status.Despawn();
    }

    public SimStatus? FindStatus(ushort statusId, GameObjectId sourceObject = default)
    {
        foreach (var status in statusList)
        {
            if (status.IsActive && status.StatusId == statusId && status.SourceObject == sourceObject)
                return status;
        }
        return null;
    }

    public bool HasStatus(ushort statusId) => FindStatus(statusId) != null;

    // What this client shows on the character, for a bot reading its own screen: on a peer the
    // sim's own list holds none of the host's statuses.
    public bool ShowsStatus(ushort statusId) => Proxy?.ShowsStatus(statusId) ?? false;

    public IReadOnlyList<(ushort StatusId, ushort Stacks, float RemainingTime)> ActiveStatusSnapshot =>
        statusList.Where(s => s.IsActive).Select(s => (s.StatusId, s.Stacks, s.RemainingTime)).ToList();

    public IReadOnlyList<SimStatus> ActiveStatuses => statusList.Where(s => s.IsActive).ToList();

    private readonly List<(ushort StatusId, ushort Param)> speedStatuses = [];

    private float SpeedFactor()
    {
        speedStatuses.Clear();
        foreach (var status in statusList)
            if (status.IsActive) speedStatuses.Add((status.StatusId, status.Stacks));
        return speedStatuses.Count == 0 ? 1f : MovementSpeed.Factor(speedStatuses);
    }

    // A status's Lock* flags don't stop anyone client-side: the local player only through the
    // server's conditions (SimPlayer), doppels not at all, so the sheet is read here.
    public bool MovementLocked => AnyActiveStatusRow(row => row.LockMovement || row.LockControl);
    public bool ActionsLocked => AnyActiveStatusRow(row => row.LockActions || row.LockControl);

    private protected bool AnyActiveStatusRow(Func<StatusRow, bool> predicate)
    {
        foreach (var status in statusList)
        {
            if (status.IsActive && Natives.Data.Status(status.StatusId) is { } row && predicate(row))
                return true;
        }
        return false;
    }


    // -------------------------
    // Other Subsystem
    // -------------------------

    public void PlayActionTimeline(ushort timelineId) => ActorControl.PlayActionTimeline(timelineId);

    public void VoiceLine(uint voiceLineId) => ActorControl.PlayVoiceLine(voiceLineId);

    public virtual void SetTargetable(bool targetable) => ActorControl.SetTargetable(targetable);

    public bool Dead { get; private set; }

    // Retail's death, all on one tick: 0x50 first for a death no action dealt (wall, falling off).
    protected bool GoDown(bool byEnvironment)
    {
        if (!MarkDead()) return false;
        if (byEnvironment) ActorControl.WallDeath(Proxy?.MaxHealth ?? 0);
        PlayDeath();
        return true;
    }

    // Split from PlayDeath for a death the client animates later than the server records it.
    protected bool MarkDead()
    {
        if (Dead) return false;
        Dead = true;
        StopMoving();
        if (Proxy is { Exists: true } chara && chara.GetTetherId(0) != 0) ActorControl.ClearTether();
        return true;
    }

    protected void PlayDeath()
    {
        ActorControl.PlayDeathAnimation();
        ActorControl.SetMode(CharacterModes.Dead);
        EndStatuses(StatusLossReason.Died);
        SendStatusList(0, 0);
    }

    protected void Revive()
    {
        if (!Dead) return;
        Dead = false;
        ActorControl.Revive();
        if (Proxy is { } chara) SendStatusList(chara.MaxHealth, chara.MaxMana);
    }

    // A party member outlives the run: back on its feet at full HP, with nothing the run put on it.
    internal virtual void ClearRunState()
    {
        EndStatuses(null);
        if (ActorControl.Confused) ActorControl.SetConfused(false);
        confusedChaseHold = 0f;
        Motion.Clear();
        if (Dead) Revive();
        else if (Proxy is { } chara) SendStatusList(chara.MaxHealth, chara.MaxMana);
    }

    private ActorControl? actorControl;
    public ActorControl ActorControl => actorControl ??= new ActorControl(() => Proxy);
}
