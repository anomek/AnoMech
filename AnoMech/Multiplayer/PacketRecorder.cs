using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Multiplayer;

// Host: what its sim sends its own game client, recorded as it goes out so every peer can play it
// on its own. Installed over Natives for the multiplayer stay; every call reaches the real client
// first. Recording starts with the first run: the zone-in each client makes for itself (zone,
// spawn wall, first phase, party seating) is not the host's to send.
internal sealed class PacketRecorder
{
    private const uint NoTarget = 0xE0000000;

    private readonly IBattleCharas realCharas;
    private readonly IEventObjects realObjects;
    private readonly IMapEffects realMap;
    private readonly IInstanceContentDirector realDirector;
    private readonly IHaterList realHaters;
    private readonly IZoneControl realZone;

    private readonly List<GamePacket> pending = [];
    // Host entity ids of the actors spawned while recording: the only ones peers have.
    private readonly HashSet<uint> spawned = [];
    // Those despawned since, which a status still names as its applier.
    private readonly HashSet<uint> gone = [];
    private readonly Dictionary<IBattleCharaProxy, (PartyRole Role, SeatKind Kind)> seats = new();
    private readonly Dictionary<PartyRole, (bool Status, bool Control)> afflictions = new();
    private readonly HashSet<uint> unruled = [];
    private readonly RecordingBattleChara localPlayer;
    private PartyRole? shownBy;

    public bool Recording { get; set; }
    public bool HasPending => pending.Count > 0;

    private PacketRecorder()
    {
        realCharas = Natives.BattleCharas;
        realObjects = Natives.EventObjects;
        realMap = Natives.MapEffects;
        realDirector = Natives.Director;
        realHaters = Natives.HaterList;
        realZone = Natives.Zone;
        localPlayer = new RecordingBattleChara(realCharas.LocalPlayer, this, 0);
    }

    public static PacketRecorder Install()
    {
        var recorder = new PacketRecorder();
        Natives.BattleCharas = new RecordingBattleCharas(recorder);
        Natives.EventObjects = new RecordingEventObjects(recorder);
        Natives.MapEffects = new RecordingMapEffects(recorder);
        Natives.Director = new RecordingDirector(recorder);
        Natives.HaterList = new RecordingHaterList(recorder);
        Natives.Zone = new RecordingZone(recorder);
        return recorder;
    }

    public void Uninstall()
    {
        Natives.BattleCharas = realCharas;
        Natives.EventObjects = realObjects;
        Natives.MapEffects = realMap;
        Natives.Director = realDirector;
        Natives.HaterList = realHaters;
        Natives.Zone = realZone;
    }

    public List<GamePacket> Drain()
    {
        var packets = pending.ToList();
        pending.Clear();
        return packets;
    }

    // After every seating: which seat each doppel and the local player stand in, and who plays it.
    public void TagSeats(SimParty party)
    {
        seats.Clear();
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var member = party.Get(role);
            if (member?.Proxy is not { } proxy) continue;
            seats[proxy] = (role, member switch
            {
                SimPlayer => SeatKind.LocalPlayer,
                SimNetworkPuppet => SeatKind.Remote,
                _ => SeatKind.Bot,
            });
        }
    }

    // The PlayerStateFlags each remote seat's own client would get: the same rule SimPlayer applies
    // for the local player, from the statuses the host holds on that seat.
    public void RecordAfflictions(SimParty party)
    {
        foreach (var (proxy, seat) in seats)
        {
            if (seat.Kind != SeatKind.Remote || party.Get(seat.Role) is not { } member) continue;
            var now = (Status: false, Control: false);
            foreach (var status in member.ActiveStatuses)
            {
                if (Natives.Data.Status(status.StatusId) is not { } row) continue;
                now.Status |= row.LockMovement || row.LockActions;
                now.Control |= row.LockControl;
            }
            if (afflictions.GetValueOrDefault(seat.Role) == now) continue;
            afflictions[seat.Role] = now;
            Add(new AfflictionsPacket(PacketActor.Seat(seat.Role), now.Status, now.Control));
        }
    }

    private void Add(GamePacket packet)
    {
        if (Recording) pending.Add(shownBy is { } seat ? packet with { ShownBy = seat } : packet);
    }

    // What `show` sends is the seat's own action, which its client showed already: tagged so that
    // client skips it, and kept off the host's own player.
    public void ShownBy(PartyRole seat, Action show)
    {
        shownBy = seat;
        try { show(); }
        finally { shownBy = null; }
    }

    private bool AlreadyShown(RecordingBattleChara proxy) => shownBy != null && ReferenceEquals(proxy, localPlayer);

    // ── Addressing ───────────────────────────────────────────────────────────

    private enum SeatKind { Bot, Remote, LocalPlayer }

    private const uint SetTetherCategory = 0x23;

    private bool TryAddress(IBattleCharaProxy proxy, Delivery delivery, out uint actor, out bool ownerOnly)
    {
        actor = PacketActor.None;
        ownerOnly = false;
        if (!Recording) return false;
        if (seats.TryGetValue(proxy, out var seat))
        {
            switch (seat.Kind)
            {
                case SeatKind.Bot when delivery != Delivery.ZoneIn:
                    actor = PacketActor.Seat(seat.Role);
                    return true;
                case SeatKind.Remote when delivery is Delivery.Everyone or Delivery.SeatOwner or Delivery.EachOwnPlayer:
                    actor = PacketActor.Seat(seat.Role);
                    ownerOnly = delivery != Delivery.Everyone;
                    return true;
                case SeatKind.LocalPlayer when delivery == Delivery.Everyone:
                    actor = PacketActor.Seat(seat.Role);
                    return true;
                case SeatKind.LocalPlayer when delivery == Delivery.EachOwnPlayer:
                    actor = PacketActor.Self;
                    return true;
                default:
                    return false;
            }
        }
        var entityId = proxy.EntityId;
        if (!spawned.Contains(entityId)) return false;
        actor = entityId;
        return true;
    }

    private uint ActorOf(uint entityId)
    {
        if (entityId is 0 or NoTarget) return PacketActor.None;
        if (spawned.Contains(entityId)) return entityId;
        foreach (var (proxy, seat) in seats)
            if (proxy.EntityId == entityId) return PacketActor.Seat(seat.Role);
        return PacketActor.None;
    }

    private uint ActorOf(GameObjectId? id) => id is { } objectId ? ActorOf(objectId.ObjectId) : PacketActor.None;

    // The server's status list keeps naming an applier that has since despawned.
    private uint SourceOf(uint entityId)
    {
        var actor = ActorOf(entityId);
        return actor == PacketActor.None && gone.Contains(entityId) ? entityId : actor;
    }

    // An unknown entity goes out as no target rather than as an id that means nothing elsewhere.
    private (uint[] Args, int Index, uint Actor) WithEntityArg(uint[] args, int index)
    {
        if (index < 0 || args[index] == NoTarget) return (args, -1, PacketActor.None);
        var actor = ActorOf(args[index]);
        args[index] = actor == PacketActor.None ? NoTarget : 0;
        return (args, actor == PacketActor.None ? -1 : index, actor);
    }

    // ── Recording ────────────────────────────────────────────────────────────

    // A call to an actor the host's own client doesn't have yet does nothing there; peers get
    // only what that client got.
    private void Record(IBattleCharaProxy proxy, Delivery delivery, Func<uint, bool, GamePacket> packet)
    {
        if (!proxy.Exists || !TryAddress(proxy, delivery, out var actor, out var ownerOnly)) return;
        Add(packet(actor, ownerOnly));
    }

    // No peer would take a category without a rule.
    private ControlRule? Rule(IReadOnlyDictionary<uint, ControlRule> rules, uint category, string what)
    {
        if (rules.TryGetValue(category, out var rule)) return rule;
        if (Recording && unruled.Add(category))
            DiagnosticLog.Warn($"[Multiplayer] Host: {what} category 0x{category:X} has no PacketRules entry -- peers won't see it.");
        return null;
    }

    private void RecordControl(IBattleCharaProxy proxy, uint category, uint[] args)
    {
        if (Rule(PacketRules.Controls, category, "ActorControl") is not { } rule) return;
        if (!proxy.Exists || !TryAddress(proxy, rule.Delivery, out var actor, out var ownerOnly)) return;
        if (category == SetTetherCategory) SimAssets.WarnIfUnknown(SimAssetKind.Tether, args[1], "tether");
        var (sent, index, argActor) = WithEntityArg(args, rule.ActorArg);
        Add(new ControlPacket(actor, category, sent, index, argActor, ownerOnly));
    }

    private void RecordControlTarget(IBattleCharaProxy proxy, uint category, GameObjectId target, uint[] args)
    {
        if (Rule(PacketRules.TargetControls, category, "ActorControlTarget") is not { } rule) return;
        if (!proxy.Exists || !TryAddress(proxy, rule.Delivery, out var actor, out var ownerOnly)) return;
        if (ownerOnly) return;
        var (sent, index, argActor) = WithEntityArg(args, rule.ActorArg);
        Add(new ControlTargetPacket(actor, category, ActorOf(target), sent, index, argActor));
    }

    private void RecordCast(IBattleCharaProxy proxy, ActorCastData cast)
        => Record(proxy, Delivery.Everyone, (actor, _) =>
        {
            SimAssets.WarnIfUnplayable(cast.ActionId, "cast");
            return new CastPacket(actor, cast.ActionId, cast.ActionType, cast.CastTime, cast.OmenDelay,
                cast.Interruptible, cast.Rotation, cast.Position.X, cast.Position.Y, cast.Position.Z, ActorOf(cast.Target), ActorOf(cast.Ballista));
        });

    // The client answers an effect naming its own request (SourceSequence) only on the host.
    private void RecordEffect(IBattleCharaProxy proxy, uint sequence, ActionEffectData effect)
        => Record(proxy, Delivery.Everyone, (actor, _) =>
        {
            SimAssets.WarnIfUnplayable(effect.ActionId, "effect");
            return new EffectPacket(actor, sequence, effect.ActionId, effect.ActionType, effect.AnimationLock,
                effect.SpellId, effect.AnimationVariation, effect.Flags, effect.Rotation, effect.Position.X, effect.Position.Y, effect.Position.Z,
                ActorOf(effect.AnimationTarget),
                effect.Targets?.Select(t => new EffectTargetPacket(ActorOf(t.Id), t.Effects?.ToList())).ToList(),
                ActorOf(effect.Ballista));
        });

    private void RecordStatusList(IBattleCharaProxy proxy, IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
        => Record(proxy, Delivery.Everyone, (actor, _) =>
        {
            var slots = new List<StatusSlotPacket>();
            for (var i = 0; i < statuses.Count; i++)
                if (statuses[i] is { StatusId: not 0 } s)
                    slots.Add(new StatusSlotPacket((byte)i, s.StatusId, s.Param, s.RemainingTime, SourceOf(s.SourceEntityId)));
            return new StatusListPacket(actor, slots, health, proxy.MaxHealth, mana, shield);
        });

    // A despawn always goes out: a peer's copy of an actor the host dropped before it arrived
    // would otherwise stay.
    private void RecordDespawn(uint entityId)
    {
        if (!spawned.Remove(entityId)) return;
        gone.Add(entityId);
        Add(new DespawnPacket(entityId));
    }

    private IBattleCharaProxy? SpawnNpc(EnemySpawnConfig config, Placement placement, out uint entityId)
    {
        if (realCharas.SpawnBattleNpcFromPacket(config, placement, out entityId) is not { } chara) return null;
        if (Recording)
        {
            SimAssets.WarnIfUnknown(SimAssetKind.BNpcBase, config.BNpcBaseId, "NPC spawn");
            SimAssets.WarnIfUnknown(SimAssetKind.ModelChara, config.ModelCharaId, "NPC spawn model");
            SimAssets.WarnIfUnknown(SimAssetKind.Layout, config.LayoutId, "NPC spawn layout");
            spawned.Add(entityId);
            Add(new SpawnNpcPacket(entityId, config.BNpcBaseId, config.NameId, config.Level, config.Targetable, config.EnemyList,
                config.Visibility, config.ModelCharaId, config.InitialModeAttributeFlags, config.DisplayFlags, config.Engaged, config.LayoutId,
                config.IsHostile, placement.Position.X, placement.Position.Y, placement.Position.Z, placement.Rotation,
                config.ModelState, config.ModelScale, config.Appearance, config.Statuses?.ToList()));
        }
        return new RecordingBattleChara(chara, this, entityId);
    }

    private IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, byte level, Placement placement, out uint entityId)
        => realCharas.SpawnDoppel(preset, level, placement, out entityId) is { } chara ? new RecordingBattleChara(chara, this, entityId) : null;

    private IEventObjectProxy? SpawnObject(EventObjectSpawnConfig config, Placement placement)
    {
        if (realObjects.Spawn(config, placement) is not { } obj) return null;
        if (Recording)
        {
            SimAssets.WarnIfUnknown(SimAssetKind.EObj, config.EObjId, "event object spawn");
            SimAssets.WarnIfUnknown(SimAssetKind.Layout, config.LayoutId, "event object layout");
            SimAssets.WarnIfUnknown(SimAssetKind.EventId, (uint)config.EventId, "event object EventId");
            spawned.Add(obj.EntityId);
            Add(new SpawnObjPacket(obj.EntityId, config.EObjId, config.ObjectIndex, config.TargetableStatus, config.Hidden, config.EntityId,
                config.LayoutId, (uint)config.EventId, ActorOf(config.OwnerId), config.GimmickId, config.Radius, config.FateId, config.EventState,
                config.Arg2, config.TimelineState, placement.Position.X, placement.Position.Y, placement.Position.Z, placement.Rotation));
        }
        return new RecordingEventObject(obj, this);
    }

    private void RecordObjectControl(IEventObjectProxy obj, uint category, uint[] args)
    {
        var entityId = obj.EntityId;
        if (!Recording || !spawned.Contains(entityId) || Rule(PacketRules.Controls, category, "ActorControl") is not { } rule) return;
        var (sent, index, argActor) = WithEntityArg(args, rule.ActorArg);
        Add(new ControlPacket(entityId, category, sent, index, argActor));
    }

    private void RecordWorld(GamePacket packet) => Add(packet);

    private void RecordBattleTalk(uint speakerNameId, uint textId, uint durationMs)
    {
        if (Recording) SimAssets.WarnIfUnknown(SimAssetKind.BattleTalk, textId, "BattleTalk");
        Add(new BattleTalkPacket(speakerNameId, textId, durationMs));
    }

    private void RecordHaters(IReadOnlyList<Hater> haters)
    {
        var sent = new List<HaterPacket>();
        foreach (var hater in haters)
            if (ActorOf(hater.EntityId) is var actor and not PacketActor.None)
                sent.Add(new HaterPacket(actor, hater.Enmity));
        Add(new HatersPacket(sent));
    }

    // ── The recording natives ────────────────────────────────────────────────

    private sealed class RecordingBattleCharas(PacketRecorder recorder) : IBattleCharas
    {
        public IBattleCharaProxy LocalPlayer => recorder.localPlayer;

        public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
            => recorder.SpawnNpc(config, placement, out entityId);

        public IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, byte level, Placement placement, out uint entityId)
            => recorder.SpawnDoppel(preset, level, placement, out entityId);

        public bool IsInCharacterManager(uint entityId) => recorder.realCharas.IsInCharacterManager(entityId);

        public void ReleaseSlot(int slot) => recorder.realCharas.ReleaseSlot(slot);
    }

    // spawnedEntityId: the spawn packet's, for a despawn before the actor arrived; 0 for the local
    // player.
    private sealed class RecordingBattleChara(IBattleCharaProxy inner, PacketRecorder recorder, uint spawnedEntityId) : IBattleCharaProxy
    {
        public bool Exists => inner.Exists;
        public int Slot => inner.Slot;
        public uint EntityId => inner.EntityId;
        public GameObjectId GameObjectId => inner.GameObjectId;
        public string Name => inner.Name;
        public byte ClassJob => inner.ClassJob;
        public Vector3 Position => inner.Position;
        public float Rotation => inner.Rotation;
        public float HitboxRadius => inner.HitboxRadius;
        public bool HasDrawObject => inner.HasDrawObject;
        public bool IsDrawObjectVisible => inner.IsDrawObjectVisible;
        public byte ModelState => inner.ModelState;
        public NpcAppearance? Appearance => inner.Appearance;
        public bool IsCasting => inner.IsCasting;
        public uint CastActionId => inner.CastActionId;
        public float CurrentCastTime => inner.CurrentCastTime;
        public float TotalCastTime => inner.TotalCastTime;
        public uint Health => inner.Health;
        public uint MaxHealth => inner.MaxHealth;
        public uint Mana => inner.Mana;
        public uint MaxMana => inner.MaxMana;
        public byte TargetableStatus => inner.TargetableStatus;
        public ushort GetTetherId(byte slot) => inner.GetTetherId(slot);
        public bool ShowsStatus(ushort statusId) => inner.ShowsStatus(statusId);
        public string DescribeDrawState() => inner.DescribeDrawState();
        public string DescribeActionTimeline() => inner.DescribeActionTimeline();
        public string DescribeTimelineSlotIds() => inner.DescribeTimelineSlotIds();
        public string DescribeModelSlots() => inner.DescribeModelSlots();

        public void SetPos(Vector3 position, float rotation)
        {
            recorder.Record(this, Delivery.Movement, (actor, _) => new SetPosPacket(actor, position.X, position.Y, position.Z, rotation));
            inner.SetPos(position, rotation);
        }

        public void Move(Vector3 position, float rotation, ActorMoveFlags flags, float speed)
        {
            recorder.Record(this, Delivery.Movement, (actor, _) => new MovePacket(actor, position.X, position.Y, position.Z, rotation, flags, speed));
            inner.Move(position, rotation, flags, speed);
        }

        public void NpcKnockback(Vector3 position, float rotation, float durationSeconds)
        {
            recorder.Record(this, Delivery.Movement, (actor, _) => new NpcKnockbackPacket(actor, position.X, position.Y, position.Z, rotation, durationSeconds));
            inner.NpcKnockback(position, rotation, durationSeconds);
        }

        public void ReceiveActorCast(ActorCastData cast)
        {
            recorder.RecordCast(this, cast);
            if (!recorder.AlreadyShown(this)) inner.ReceiveActorCast(cast);
        }

        public uint ReceiveActionEffect(ActionEffectData effect)
        {
            var sequence = recorder.AlreadyShown(this) ? 0 : inner.ReceiveActionEffect(effect);
            recorder.RecordEffect(this, sequence, effect);
            return sequence;
        }

        public void ReceiveEffectResult(uint globalSequence, byte targetIndex, uint health)
        {
            recorder.Record(this, Delivery.Everyone, (actor, _) => new EffectResultPacket(actor, globalSequence, targetIndex, health, inner.MaxHealth));
            inner.ReceiveEffectResult(globalSequence, targetIndex, health);
        }

        public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        {
            recorder.RecordControl(this, category, [arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8]);
            if (!recorder.AlreadyShown(this)) inner.ActorControl(category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public void ActorControlTarget(uint category, GameObjectId target, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0)
        {
            recorder.RecordControlTarget(this, category, target, [arg1, arg2, arg3, arg4]);
            inner.ActorControlTarget(category, target, arg1, arg2, arg3, arg4);
        }

        public void ForcedFollow(uint? targetEntityId) => inner.ForcedFollow(targetEntityId);

        public void StopForcedMove() => inner.StopForcedMove();

        public void ReceiveStatusList(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
        {
            recorder.RecordStatusList(this, statuses, health, mana, shield);
            inner.ReceiveStatusList(statuses, health, mana, shield);
        }

        public void Despawn()
        {
            recorder.RecordDespawn(spawnedEntityId != 0 ? spawnedEntityId : inner.EntityId);
            inner.Despawn();
        }
    }

    private sealed class RecordingEventObjects(PacketRecorder recorder) : IEventObjects
    {
        public IEventObjectProxy? Spawn(EventObjectSpawnConfig config, Placement placement) => recorder.SpawnObject(config, placement);
    }

    private sealed class RecordingEventObject(IEventObjectProxy inner, PacketRecorder recorder) : IEventObjectProxy
    {
        public int Slot => inner.Slot;
        public uint EntityId => inner.EntityId;
        public Vector3 Position => inner.Position;
        public float Rotation => inner.Rotation;

        public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        {
            recorder.RecordObjectControl(inner, category, [arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8]);
            inner.ActorControl(category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public void Despawn()
        {
            recorder.RecordDespawn(inner.EntityId);
            inner.Despawn();
        }
    }

    // Only what the client took: a call refused while the zone streams in is retried, and goes out
    // when it lands.
    private sealed class RecordingMapEffects(PacketRecorder recorder) : IMapEffects
    {
        public bool Loaded
        {
            get => recorder.realMap.Loaded;
            set => recorder.realMap.Loaded = value;
        }

        public bool Apply(uint packetFlags, byte index)
        {
            if (!recorder.realMap.Apply(packetFlags, index)) return false;
            recorder.RecordWorld(new MapEffectPacket(packetFlags, index));
            return true;
        }

        public bool ApplySnapshot(ReadOnlySpan<ushort> states, bool zoneIn)
        {
            if (!recorder.realMap.ApplySnapshot(states, zoneIn)) return false;
            recorder.RecordWorld(new MapEffectSnapshotPacket(states.ToArray(), zoneIn));
            return true;
        }

        public void LogAllSlots(string label) => recorder.realMap.LogAllSlots(label);

        public void Dispose() => recorder.realMap.Dispose();
    }

    private sealed class RecordingDirector(PacketRecorder recorder) : IInstanceContentDirector
    {
        public bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
        {
            if (!recorder.realDirector.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6)) return false;
            recorder.RecordWorld(new DirectorUpdatePacket(category, arg1, arg2, arg3, arg4, arg5, arg6));
            return true;
        }

        public void SetDirectorData(byte sequence, byte unknown, byte[] unionData)
        {
            recorder.realDirector.SetDirectorData(sequence, unknown, unionData);
            recorder.RecordWorld(new DirectorDataPacket(sequence, unknown, unionData.ToArray()));
        }

        public void Commence()
        {
            recorder.realDirector.Commence();
            recorder.RecordWorld(new CommencePacket());
        }

        public bool BattleTalk(uint speakerNameId, uint textId, uint durationMs)
        {
            if (!recorder.realDirector.BattleTalk(speakerNameId, textId, durationMs)) return false;
            recorder.RecordBattleTalk(speakerNameId, textId, durationMs);
            return true;
        }
    }

    private sealed class RecordingHaterList(PacketRecorder recorder) : IHaterList
    {
        public void Receive(IReadOnlyList<Hater> haters)
        {
            recorder.realHaters.Receive(haters);
            recorder.RecordHaters(haters);
        }
    }

    private sealed class RecordingZone(PacketRecorder recorder) : IZoneControl
    {
        private IZoneControl Real => recorder.realZone;

        public bool IsInInn() => Real.IsInInn();
        public string? StartBlockedReason(out string? settling) => Real.StartBlockedReason(out settling);
        public void EndSpeedBuffs() => Real.EndSpeedBuffs();
        public bool IsActive => Real.IsActive;
        public void TickGuard() => Real.TickGuard();
        public bool Enter(uint territoryId, Vector3 playerSpawn) => Real.Enter(territoryId, playerSpawn);
        public void Revert() => Real.Revert();

        public void ApplyWeather(byte weatherId)
        {
            Real.ApplyWeather(weatherId);
            recorder.RecordWorld(new WeatherPacket(weatherId, 0f, AfterLoad: true));
        }

        public void SetWeather(byte weatherId, float transition = 0.5f)
        {
            Real.SetWeather(weatherId, transition);
            recorder.RecordWorld(new WeatherPacket(weatherId, transition, AfterLoad: false));
        }

        public void TickWeather() => Real.TickWeather();

        public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what)
            => Real.InjectIncomingPacket(sourceEntityId, opcode, body, what);

        public void HoldSendFirewall(bool hold) => Real.HoldSendFirewall(hold);
        public uint MaxActionEffectCounter => Real.MaxActionEffectCounter;
        public void Dispose() => Real.Dispose();
    }
}
