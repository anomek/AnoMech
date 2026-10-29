using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Multiplayer;

// Peer: plays the host's packets on this client. Each actor a packet names is resolved to this
// client's own -- a seat to its doppel or to our player, a spawned actor to the copy spawned here
// from the same packet -- and a packet naming one that hasn't arrived yet waits for it, behind
// the earlier packets for its own actor.
//
// What it spawns also gets a mirror in this client's world (SimEnemy, SimEventObject, SimTether
// on silent proxies), and the host's bots' seats follow their packets there: a debug bot here
// reads that world, so it acts on what this client was shown.
internal sealed class PacketReplay(PeerNatives natives, SimWorld world, PartyRole myRole)
{
    private const uint NoTarget = 0xE0000000;
    private const uint SetTetherCategory = 0x23;
    private const uint ClearTetherCategory = 0x2F;
    private const uint MarkingCategory = 0x1F6;
    private const int StatusSlots = 60;
    // As many as a spawn packet carries.
    private const int SpawnStatuses = 30;
    private const int SignCount = 17;
    private const int MaxEffectTargets = 64;
    private const int MaxEntriesPerTarget = 8;
    private const int MaxDirectorDataBytes = 256;
    private const int MaxHaters = 32;
    // An actor that hasn't arrived by then never will; MapController gives the zone as long.
    private const int MaxWaitFrames = 300;
    private const int RememberedEffects = 4096;

    private sealed class Spawned
    {
        public uint LocalEntityId;
        public IBattleCharaProxy? Character;
        public IEventObjectProxy? Object;
        public SimEnemy? Enemy;
        public SimEventObject? Prop;
    }

    private readonly Dictionary<uint, Spawned> spawned = new();
    // Each status's source as this client was first shown it, kept while the status lasts: the
    // host's list goes on naming an applier that has despawned, or whose id a later spawn took.
    private readonly Dictionary<(uint Actor, ushort StatusId, uint Source), uint> statusSources = new();
    // What each actor's status list shows, on its SimCharacter here, as the host's sim holds it there.
    private readonly Dictionary<(uint Actor, ushort StatusId, uint Source), SimStatus> shownStatuses = new();

    // Set per run (IMultiplayerReplayable.MirrorsHostStatuses).
    public bool MirrorStatuses { get; set; }
    private readonly Dictionary<uint, SimTether> tethers = new();
    private readonly Dictionary<uint, uint> effectSequences = new();
    private readonly Queue<uint> effectOrder = new();
    // An effect waiting for its caster holds back the results that name it.
    private readonly Dictionary<uint, uint> waitingEffects = new();
    private readonly Dictionary<uint, (Queue<GamePacket> Packets, int Frames)> waiting = new();
    // Map effects and director updates, in the host's order, each retried until the zone takes it.
    private readonly Queue<GamePacket> arena = new();
    private int arenaFrames;

    public IReadOnlyDictionary<int, SimEnemy> Enemies
        => spawned.Where(s => s.Value.Enemy != null).ToDictionary(s => (int)s.Key, s => s.Value.Enemy!);

    public IReadOnlyDictionary<int, SimEventObject> Props
        => spawned.Where(s => s.Value.Prop != null).ToDictionary(s => (int)s.Key, s => s.Value.Prop!);

    public int Waiting => waiting.Values.Sum(w => w.Packets.Count) + arena.Count;

    // A copy spawned here: its entity id, and the host actor it stands for.
    public event Action<uint, uint>? Copied;

    public uint HostActorOf(SimEnemy mirror)
    {
        foreach (var (actor, copy) in spawned)
            if (ReferenceEquals(copy.Enemy, mirror)) return actor;
        return PacketActor.None;
    }

    // An actor here, by its GameObjectId, as the host's packets name it.
    public uint HostActorOfObject(ulong gameObjectId)
    {
        var entityId = (uint)gameObjectId;
        if (entityId is 0 or NoTarget) return PacketActor.None;
        foreach (var (role, member) in world.Party.FilledSlots())
            if (member.EntityId == entityId) return PacketActor.Seat(role);
        foreach (var (actor, copy) in spawned)
            if (copy.LocalEntityId == entityId) return actor;
        return PacketActor.None;
    }

    // The host's enemy (by its entity id) or seat, as this client's world has it.
    public SimCharacter? MirrorOfHost(uint actor) => actor == PacketActor.None ? null : MirrorOf(actor);

    public void Apply(GamePacket packet)
    {
        if (packet.ShownBy == myRole) return;
        switch (packet)
        {
            case SpawnNpcPacket p: SpawnNpc(p); return;
            case SpawnObjPacket p: SpawnObject(p); return;
            case MapEffectPacket or MapEffectSnapshotPacket or DirectorUpdatePacket:
                QueueArena(packet);
                return;
            case DirectorDataPacket p:
                if (p.Data.Length <= MaxDirectorDataBytes) natives.RealDirector.SetDirectorData(p.Sequence, p.Unknown, p.Data);
                return;
            case CommencePacket:
                natives.RealDirector.Commence();
                return;
            case WeatherPacket p: Weather(p); return;
        }

        var actor = ActorOf(packet);
        if (packet is EffectResultPacket result && waitingEffects.TryGetValue(result.Sequence, out var caster)) actor = caster;
        if (waiting.ContainsKey(actor) || !Arrived(packet))
        {
            Wait(actor, packet);
            return;
        }
        Play(packet);
    }

    public void Tick()
    {
        foreach (var actor in waiting.Keys.ToList())
        {
            var (packets, frames) = waiting[actor];
            while (packets.TryPeek(out var head) && (Arrived(head) || frames >= MaxWaitFrames))
            {
                packets.Dequeue();
                if (head is EffectPacket effect) waitingEffects.Remove(effect.Sequence);
                if (Readiness(ActorOf(head)) == false)
                    DiagnosticLog.Warn($"[Multiplayer] Peer: dropping the host's {head.GetType().Name} for actor 0x{ActorOf(head):X} -- it never arrived here.");
                Play(head);
                frames = 0;
            }
            if (packets.Count == 0) waiting.Remove(actor);
            else waiting[actor] = (packets, frames + 1);
        }
        DrainArena();
    }

    // Past MaxWaitFrames a named actor still missing is no target, and a packet whose own actor
    // is missing is dropped. One bad packet must not take down the tick.
    private void Play(GamePacket packet)
    {
        try
        {
            if (packet is HatersPacket haters) Haters(haters);
            else if (Readiness(ActorOf(packet)) == true) ApplyTo(packet);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: error playing the host's {packet.GetType().Name}: {e}");
        }
    }

    // Whatever the host's own despawns didn't reach: the run ended, the host left, or we did.
    public void Clear()
    {
        foreach (var actor in spawned.Keys.ToList()) Despawn(actor);
        foreach (var tether in tethers.Values) tether.Despawn();
        tethers.Clear();
        foreach (var status in shownStatuses.Values) status.Despawn();
        shownStatuses.Clear();
        waiting.Clear();
        waitingEffects.Clear();
        arena.Clear();
        effectSequences.Clear();
        effectOrder.Clear();
        statusSources.Clear();
    }

    // ── Actors ───────────────────────────────────────────────────────────────

    private static uint ActorOf(GamePacket packet) => packet switch
    {
        DespawnPacket p => p.Actor,
        SetPosPacket p => p.Actor,
        MovePacket p => p.Actor,
        NpcKnockbackPacket p => p.Actor,
        CastPacket p => p.Actor,
        EffectPacket p => p.Actor,
        EffectResultPacket p => p.Actor,
        ControlPacket p => p.Actor,
        ControlTargetPacket p => p.Actor,
        StatusListPacket p => p.Actor,
        AfflictionsPacket p => p.Actor,
        _ => PacketActor.None,
    };

    // Every actor the packet names besides its own, which it can't show without.
    private static IEnumerable<uint> Named(GamePacket packet) => packet switch
    {
        CastPacket p => [p.Target, p.Ballista],
        EffectPacket p => [p.AnimationTarget, p.Ballista, .. NetGuard.Cap(p.Targets, MaxEffectTargets).Select(t => t?.Target ?? PacketActor.None)],
        ControlPacket p => [p.ArgActor],
        ControlTargetPacket p => [p.Target, p.ArgActor],
        StatusListPacket p => NetGuard.Cap(p.Slots, StatusSlots).Select(s => s?.Source ?? PacketActor.None),
        HatersPacket p => NetGuard.Cap(p.Haters, MaxHaters).Select(h => h?.Actor ?? PacketActor.None),
        _ => [],
    };

    // Whether every actor the packet names is here, or never will be.
    private bool Arrived(GamePacket packet) => Readiness(ActorOf(packet)) != false && Named(packet).All(actor => Readiness(actor) != false);

    // Null: nothing here stands for it, or ever will.
    private bool? Readiness(uint actor)
    {
        if (actor == PacketActor.None) return null;
        if (actor == PacketActor.Self) return true;
        if (PacketActor.IsSeat(actor, out var role))
        {
            if (role == myRole) return true;
            return world.Party.Get(role) is { } member ? member.Proxy is { Exists: true } : null;
        }
        if (!spawned.TryGetValue(actor, out var copy)) return null;
        return copy.Object != null || copy.Character is { Exists: true };
    }

    private bool IsOurSeat(uint actor) => actor == PacketActor.Self || (PacketActor.IsSeat(actor, out var role) && role == myRole);

    private void Wait(uint actor, GamePacket packet)
    {
        if (!waiting.TryGetValue(actor, out var queue)) waiting[actor] = queue = (new Queue<GamePacket>(), 0);
        if (queue.Packets.Count >= NetGuard.MaxQueuedMessages) return;
        queue.Packets.Enqueue(packet);
        if (packet is EffectPacket effect) waitingEffects[effect.Sequence] = actor;
    }

    // The real native: our player, a seat's doppel, or an actor spawned from the host's packet.
    private IBattleCharaProxy? Character(uint actor)
    {
        if (IsOurSeat(actor)) return natives.LocalPlayer.Inner;
        if (PacketActor.IsSeat(actor, out var role))
            return world.Party.Get(role)?.Proxy is PeerProxy seat ? seat.Inner : null;
        return spawned.GetValueOrDefault(actor)?.Character;
    }

    private uint EntityOf(uint actor)
    {
        if (actor == PacketActor.None) return NoTarget;
        if (Character(actor) is { Exists: true } chara) return chara.EntityId;
        return spawned.GetValueOrDefault(actor)?.Object?.EntityId ?? NoTarget;
    }

    private GameObjectId? ObjectIdOf(uint actor)
    {
        if (actor == PacketActor.None) return null;
        if (Character(actor) is { Exists: true } chara) return chara.GameObjectId;
        return spawned.GetValueOrDefault(actor)?.Object is { } obj ? new GameObjectId { ObjectId = obj.EntityId, Type = 0 } : null;
    }

    // What this client's world shows for an actor: a seat's member, or a spawned actor's mirror.
    private SimCharacter? MirrorOf(uint actor)
    {
        if (actor == PacketActor.Self) return world.Party.Get(myRole);
        if (PacketActor.IsSeat(actor, out var role)) return world.Party.Get(role);
        return spawned.GetValueOrDefault(actor)?.Enemy;
    }

    private static uint Scale(uint health, uint hostMax, uint localMax)
    {
        if (hostMax == 0 || localMax == 0 || hostMax == localMax) return Math.Min(health, NetGuard.MaxHp);
        return (uint)Math.Min(Math.Round((double)Math.Min(health, hostMax) * localMax / hostMax), localMax);
    }

    // ── Spawns ───────────────────────────────────────────────────────────────

    private void SpawnNpc(SpawnNpcPacket p)
    {
        if (!PacketActor.IsSpawned(p.Actor) || !NetGuard.TryPosition(p.X, p.Y, p.Z, out var position)) return;
        if (!Enum.IsDefined(p.EnemyList) || !Enum.IsDefined(p.Visibility)) return;
        if (!SimAssets.Allow(SimAssetKind.BNpcBase, p.BNpcBaseId, "NPC spawn")) return;
        if (p.ModelCharaId != 0 && !SimAssets.Allow(SimAssetKind.ModelChara, p.ModelCharaId, "NPC spawn model")) return;
        if (p.LayoutId != 0 && !SimAssets.Allow(SimAssetKind.Layout, p.LayoutId, "NPC spawn layout")) return;
        if (spawned.ContainsKey(p.Actor)) Despawn(p.Actor);
        var placement = new Placement(position, NetGuard.Rotation(p.Rotation));
        var config = new EnemySpawnConfig(p.BNpcBaseId, p.NameId, p.Level, p.Targetable, p.EnemyList, p.Visibility,
            world.Coordinates.ToLocal(placement), p.ModelCharaId, p.InitialModeAttributeFlags, p.DisplayFlags, p.Engaged, p.LayoutId, p.IsHostile,
            p.Appearance, p.Statuses is { } statuses ? NetGuard.Cap(statuses, SpawnStatuses).ToList() : null, p.ModelState, p.ModelScale);
        if (natives.RealCharas.SpawnBattleNpcFromPacket(config, placement, out var entityId) is not { } chara)
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: the host's BNpcBase {p.BNpcBaseId} (0x{p.Actor:X}) didn't spawn here -- its packets will be dropped.");
            return;
        }
        var enemy = natives.Adopting(chara, entityId, () => world.SpawnEnemy(config));
        spawned[p.Actor] = new Spawned { LocalEntityId = entityId, Character = chara, Enemy = enemy };
        Copied?.Invoke(entityId, p.Actor);
    }

    private void SpawnObject(SpawnObjPacket p)
    {
        if (!PacketActor.IsSpawned(p.Actor) || !NetGuard.TryPosition(p.X, p.Y, p.Z, out var position)) return;
        if (!SimAssets.Allow(SimAssetKind.EObj, p.EObjId, "event object spawn")) return;
        if (p.LayoutId != 0 && !SimAssets.Allow(SimAssetKind.Layout, p.LayoutId, "event object layout")) return;
        if (p.EventId != 0 && !SimAssets.Allow(SimAssetKind.EventId, p.EventId, "event object EventId")) return;
        if (spawned.ContainsKey(p.Actor)) Despawn(p.Actor);
        var placement = new Placement(position, NetGuard.Rotation(p.Rotation));
        var config = new EventObjectSpawnConfig
        {
            EObjId = p.EObjId,
            Placement = world.Coordinates.ToLocal(placement),
            ObjectIndex = p.ObjectIndex,
            TargetableStatus = p.TargetableStatus,
            Hidden = p.Hidden,
            EntityId = p.EntityId,
            LayoutId = p.LayoutId,
            EventId = p.EventId,
            OwnerId = EntityOf(p.Owner),
            GimmickId = p.GimmickId,
            Radius = NetGuard.Clamp(p.Radius, 0f, 100f, 1f),
            FateId = p.FateId,
            EventState = p.EventState,
            Arg2 = p.Arg2,
            TimelineState = p.TimelineState,
        };
        if (natives.RealObjects.Spawn(config, placement) is not { } obj)
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: the host's EObj 0x{p.EObjId:X} didn't spawn here -- its packets will be dropped.");
            return;
        }
        var prop = natives.Adopting(obj, () => world.SpawnEventObject(config));
        spawned[p.Actor] = new Spawned { LocalEntityId = obj.EntityId, Object = obj, Prop = prop };
        Copied?.Invoke(obj.EntityId, p.Actor);
    }

    private void Despawn(uint actor)
    {
        if (tethers.Remove(actor, out var tether)) tether.Despawn();
        ForgetStatusSources(actor, null);
        foreach (var key in shownStatuses.Keys.Where(k => k.Actor == actor).ToList())
        {
            shownStatuses[key].Despawn();
            shownStatuses.Remove(key);
        }
        if (!spawned.Remove(actor, out var copy)) return;
        copy.Enemy?.Despawn();
        copy.Prop?.Despawn();
        copy.Character?.Despawn();
        copy.Object?.Despawn();
    }

    // ── Per-actor packets ────────────────────────────────────────────────────

    private void ApplyTo(GamePacket packet)
    {
        switch (packet)
        {
            case DespawnPacket p:
                Despawn(p.Actor);
                break;
            case SetPosPacket p when NetGuard.TryPosition(p.X, p.Y, p.Z, out var position):
                Character(p.Actor)?.SetPos(position, NetGuard.Rotation(p.Rotation));
                Follow(p.Actor, position, NetGuard.Rotation(p.Rotation));
                break;
            case MovePacket p when NetGuard.TryPosition(p.X, p.Y, p.Z, out var position):
                Character(p.Actor)?.Move(position, NetGuard.Rotation(p.Rotation), p.Flags, NetGuard.Clamp(p.Speed, 0f, 100f));
                Follow(p.Actor, position, NetGuard.Rotation(p.Rotation));
                break;
            case NpcKnockbackPacket p when NetGuard.TryPosition(p.X, p.Y, p.Z, out var position):
                Character(p.Actor)?.NpcKnockback(position, NetGuard.Rotation(p.Rotation), NetGuard.Clamp(p.DurationSeconds, 0f, 10f));
                Follow(p.Actor, position, NetGuard.Rotation(p.Rotation));
                break;
            case CastPacket p: Cast(p); break;
            case EffectPacket p: Effect(p); break;
            case EffectResultPacket p: EffectResult(p); break;
            case ControlPacket p: Control(p); break;
            case ControlTargetPacket p: ControlTarget(p); break;
            case StatusListPacket p: StatusList(p); break;
            case AfflictionsPacket p:
                if (IsOurSeat(p.Actor)) natives.RealConditions.SetAfflictions(p.Status, p.Control);
                break;
        }
    }

    // A spawned actor's mirror and a bot seat's puppet stand where the host's packets put them.
    private void Follow(uint actor, Vector3 worldPosition, float rotation)
    {
        var local = world.Coordinates.ToLocal(worldPosition);
        if (spawned.GetValueOrDefault(actor)?.Enemy is { } enemy)
            enemy.SetPosition(new Placement(local, rotation));
        else if (PacketActor.IsSeat(actor, out var role) && role != myRole && world.Party.Get(role) is SimNetworkPuppet puppet)
            puppet.ApplyNetworkPose(local, rotation, Vector3.Zero, jumping: false);
    }

    private void Cast(CastPacket p)
    {
        if (!Enum.IsDefined(p.ActionType) || !NetGuard.TryPosition(p.X, p.Y, p.Z, out var position)) return;
        if (!SimAssets.AllowAction(p.ActionId, "cast")) return;
        Character(p.Actor)?.ReceiveActorCast(new ActorCastData(p.ActionId, p.ActionType, NetGuard.Clamp(p.CastTime, 0f, 600f),
            NetGuard.Clamp(p.OmenDelay, 0f, 60f), p.Interruptible, NetGuard.Rotation(p.Rotation), position,
            ObjectIdOf(p.Target), ObjectIdOf(p.Ballista)));
    }

    // A target missing here keeps its place, as no target: the results name targets by index.
    private void Effect(EffectPacket p)
    {
        if (!Enum.IsDefined(p.ActionType) || !NetGuard.TryPosition(p.X, p.Y, p.Z, out var position)) return;
        if (!SimAssets.AllowAction(p.ActionId, "effect")) return;
        if (Character(p.Actor) is not { } caster) return;
        var targets = NetGuard.Cap(p.Targets, MaxEffectTargets)
            .Select(t => new ActionEffectTarget(ObjectIdOf(t.Target) ?? new GameObjectId { ObjectId = NoTarget, Type = 0 },
                t.Effects == null ? null : NetGuard.Cap(t.Effects, MaxEntriesPerTarget).ToList()))
            .ToList();
        var sequence = caster.ReceiveActionEffect(new ActionEffectData(p.ActionId, p.ActionType, NetGuard.Clamp(p.AnimationLock, 0f, 60f),
            p.SpellId, p.AnimationVariation, p.Flags, NetGuard.Rotation(p.Rotation), position, ObjectIdOf(p.AnimationTarget),
            p.Targets == null ? null : targets, ObjectIdOf(p.Ballista)));
        if (effectSequences.TryAdd(p.Sequence, sequence)) effectOrder.Enqueue(p.Sequence);
        while (effectOrder.Count > RememberedEffects) effectSequences.Remove(effectOrder.Dequeue());
    }

    private void EffectResult(EffectResultPacket p)
    {
        if (!effectSequences.TryGetValue(p.Sequence, out var sequence) || Character(p.Actor) is not { } target) return;
        target.ReceiveEffectResult(sequence, p.TargetIndex, Scale(p.Health, p.MaxHealth, target.MaxHealth));
    }

    private void Control(ControlPacket p)
    {
        if (p.Args.Length != 8 || !NetGuard.IsReplayableActorControl(p.Category)) return;
        if (p.OwnerOnly && !IsOurSeat(p.Actor)) return;
        var args = p.Args.ToArray();
        if (p.ArgIndex is >= 0 and < 8) args[p.ArgIndex] = EntityOf(p.ArgActor);
        if (p.Category == SetTetherCategory && !SimAssets.Allow(SimAssetKind.Tether, args[1], "tether")) return;
        if (Character(p.Actor) is { } chara)
            chara.ActorControl(p.Category, args[0], args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
        else if (spawned.GetValueOrDefault(p.Actor)?.Object is { } obj)
            obj.ActorControl(p.Category, args[0], args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
        MirrorTether(p);
    }

    // The tethers this client shows, as world objects the debug bot can grab.
    private void MirrorTether(ControlPacket p)
    {
        if (p.Category is not (SetTetherCategory or ClearTetherCategory)) return;
        if (tethers.Remove(p.Actor, out var shown)) shown.Despawn();
        if (p.Category == SetTetherCategory && MirrorOf(p.Actor) is { } from && MirrorOf(p.ArgActor) is { } to)
            tethers[p.Actor] = world.Tether(from, to, (ushort)p.Args[1]);
    }

    private void ControlTarget(ControlTargetPacket p)
    {
        if (p.Args.Length != 4 || !PacketRules.TargetControls.ContainsKey(p.Category)) return;
        if (p.Category == MarkingCategory && p.Args[0] >= SignCount) return;
        var args = p.Args.ToArray();
        if (p.ArgIndex is >= 0 and < 4) args[p.ArgIndex] = EntityOf(p.ArgActor);
        var target = ObjectIdOf(p.Target) ?? new GameObjectId { ObjectId = NoTarget, Type = 0 };
        Character(p.Actor)?.ActorControlTarget(p.Category, target, args[0], args[1], args[2], args[3]);
    }

    private uint StatusSource(uint actor, ushort statusId, uint source)
    {
        if (source == PacketActor.None) return 0;
        if (statusSources.TryGetValue((actor, statusId, source), out var shown)) return shown;
        var local = EntityOf(source) is var id and not NoTarget ? id : 0;
        if (local != 0) statusSources[(actor, statusId, source)] = local;
        return local;
    }

    // Those `actor` no longer holds; all of them for null.
    private void ForgetStatusSources(uint actor, HashSet<(uint, ushort, uint)>? listed)
    {
        foreach (var key in statusSources.Keys.Where(k => k.Actor == actor && listed?.Contains(k) != true).ToList())
            statusSources.Remove(key);
    }

    private void StatusList(StatusListPacket p)
    {
        if (Character(p.Actor) is not { } chara) return;
        var slots = new StatusSlot[StatusSlots];
        var listed = new HashSet<(uint, ushort, uint)>();
        foreach (var s in NetGuard.Cap(p.Slots, StatusSlots))
            if (s.Slot < StatusSlots)
            {
                listed.Add((p.Actor, s.StatusId, s.Source));
                slots[s.Slot] = new StatusSlot(s.StatusId, s.Param, NetGuard.Clamp(s.RemainingTime, 0f, 86_400f), StatusSource(p.Actor, s.StatusId, s.Source));
            }
        ForgetStatusSources(p.Actor, listed);
        var health = Scale(p.Health, p.MaxHealth, chara.MaxHealth);
        if (IsOurSeat(p.Actor)) natives.LocalPlayer.ApplyHostStatuses(slots, health, p.Mana, p.Shield);
        else chara.ReceiveStatusList(slots, health, p.Mana, p.Shield);
        if (MirrorStatuses) ShowStatuses(p.Actor, slots);
    }

    // The list names whoever the client shows as the applier, not the sim's own key: a lone id is
    // kept under no source, as the sim keys a status it applies once. A status our own press
    // reported to the host is our own sim's already.
    private void ShowStatuses(uint actor, StatusSlot[] slots)
    {
        if (MirrorOf(actor) is not { } mirror) return;
        var own = IsOurSeat(actor);
        var listed = slots.Where(s => s.StatusId != 0 && !(own && HostReport.IsReported(s.StatusId))).ToList();
        var keys = new HashSet<(uint, ushort, uint)>();
        foreach (var slot in listed)
        {
            var source = listed.Count(s => s.StatusId == slot.StatusId) > 1 ? slot.SourceEntityId : 0;
            var key = (actor, slot.StatusId, source);
            if (!keys.Add(key)) continue;
            if (shownStatuses.TryGetValue(key, out var shown) && shown.IsActive)
                shown.Reapply(slot.RemainingTime, slot.Param - shown.Stacks);
            else
                shownStatuses[key] = mirror.ShowStatus(slot.StatusId, slot.Param, slot.RemainingTime,
                    source == 0 ? default : new GameObjectId { ObjectId = source, Type = 0 });
        }
        foreach (var key in shownStatuses.Keys.Where(k => k.Actor == actor && !keys.Contains(k)).ToList())
        {
            shownStatuses[key].Despawn();
            shownStatuses.Remove(key);
        }
    }

    // ── The arena ────────────────────────────────────────────────────────────

    private void QueueArena(GamePacket packet)
    {
        switch (packet)
        {
            case DirectorUpdatePacket d when !NetGuard.IsReplayableDirectorCategory(d.Category):
                DiagnosticLog.Warn($"[Multiplayer] Peer: dropped DirectorUpdate category 0x{d.Category:X8} -- no scenario sends it.");
                return;
            case MapEffectSnapshotPacket s when s.States.Length > NetGuard.MaxMapEffectSlots:
                return;
        }
        if (arena.Count >= NetGuard.MaxPendingMapCalls) return;
        arena.Enqueue(packet);
        DrainArena();
    }

    private void DrainArena()
    {
        while (arena.TryPeek(out var packet))
        {
            var taken = packet switch
            {
                MapEffectPacket m => natives.RealMap.Apply(m.PacketFlags, m.Index),
                MapEffectSnapshotPacket s => natives.RealMap.ApplySnapshot(s.States, s.ZoneIn),
                DirectorUpdatePacket d => natives.RealDirector.ProcessDirectorUpdate(d.Category, d.Arg1, d.Arg2, d.Arg3, d.Arg4, d.Arg5, d.Arg6),
                _ => true,
            };
            if (!taken && ++arenaFrames < MaxWaitFrames) return;
            if (!taken) DiagnosticLog.Warn($"[Multiplayer] Peer: gave up applying the host's {packet.GetType().Name} -- the zone never took it.");
            arena.Dequeue();
            arenaFrames = 0;
        }
    }

    private void Haters(HatersPacket p)
    {
        var haters = new List<Hater>();
        foreach (var hater in NetGuard.Cap(p.Haters, MaxHaters))
            if (EntityOf(hater.Actor) is var entityId and not NoTarget)
                haters.Add(new Hater(entityId, Math.Min(hater.Enmity, (byte)100)));
        natives.RealHaters.Receive(haters);
    }

    private void Weather(WeatherPacket p)
    {
        if (!Natives.Data.WeatherExists(p.WeatherId))
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: dropped weather {p.WeatherId} -- no such Weather row.");
            return;
        }
        if (p.AfterLoad) natives.RealZone.ApplyWeather(p.WeatherId);
        else natives.RealZone.SetWeather(p.WeatherId, NetGuard.Clamp(p.Transition, 0f, 60f, 0.5f));
    }
}
