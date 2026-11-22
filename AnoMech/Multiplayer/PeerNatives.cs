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
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Multiplayer;

// What this client's own sim may still send to an actor's native on a peer.
internal enum PeerSeat
{
    // A mirror of an actor the host spawned: only the host's packets reach it.
    Silent,
    // A seat the host's bot plays: this client spawns and despawns it, and gives it the ZoneIn
    // each client makes for a spawn it sees; the rest is the host's.
    Bot,
    // Another player's seat: a bot's, plus the steering from that player's poses (RemoteMotion).
    Remote,
    // Our own player: its movement, falls and the client's own presses are ours; its state (HP,
    // life, statuses, and the casts and actions the run plays on it) is the host's.
    LocalPlayer,
}

// Peer: the host's sim is the server for what the run shows. What this client's own sim sends
// there is dropped, and PacketReplay applies the host's packets to the real natives instead. The
// zone stay, our player's movement and presses, and the zone-in before the first run still go out.
// Installed for the multiplayer stay.
internal sealed class PeerNatives
{
    public IBattleCharas RealCharas { get; }
    public IEventObjects RealObjects { get; }
    public IMapEffects RealMap { get; }
    public IInstanceContentDirector RealDirector { get; }
    public IHaterList RealHaters { get; }
    public IZoneControl RealZone { get; }
    public IPlayerConditions RealConditions { get; }

    public PeerProxy LocalPlayer { get; }

    // From the first run on the arena (map effects, director, weather, enemy list) is the host's;
    // the zone-in and its first phase are each client's own.
    public bool HostOwnsArena { get; set; }

    private (IBattleCharaProxy Chara, uint EntityId)? adoptingCharacter;
    private IEventObjectProxy? adoptingObject;

    private PeerNatives()
    {
        RealCharas = Natives.BattleCharas;
        RealObjects = Natives.EventObjects;
        RealMap = Natives.MapEffects;
        RealDirector = Natives.Director;
        RealHaters = Natives.HaterList;
        RealZone = Natives.Zone;
        RealConditions = Natives.PlayerConditions;
        LocalPlayer = new PeerProxy(RealCharas.LocalPlayer, PeerSeat.LocalPlayer);
    }

    public static PeerNatives Install()
    {
        var natives = new PeerNatives();
        Natives.BattleCharas = new PeerBattleCharas(natives);
        Natives.EventObjects = new PeerEventObjects(natives);
        Natives.MapEffects = new PeerMapEffects(natives);
        Natives.Director = new PeerDirector(natives);
        Natives.HaterList = new PeerHaterList(natives);
        Natives.Zone = new PeerZone(natives);
        Natives.PlayerConditions = new PeerPlayerConditions(natives);
        return natives;
    }

    public void Uninstall()
    {
        Natives.BattleCharas = RealCharas;
        Natives.EventObjects = RealObjects;
        Natives.MapEffects = RealMap;
        Natives.Director = RealDirector;
        Natives.HaterList = RealHaters;
        Natives.Zone = RealZone;
        Natives.PlayerConditions = RealConditions;
    }

    // A mirror's spawn through the sim's own path, onto the actor the replay already spawned.
    public T Adopting<T>(IBattleCharaProxy chara, uint entityId, Func<T> spawn)
    {
        adoptingCharacter = (chara, entityId);
        try { return spawn(); }
        finally { adoptingCharacter = null; }
    }

    public T Adopting<T>(IEventObjectProxy obj, Func<T> spawn)
    {
        adoptingObject = obj;
        try { return spawn(); }
        finally { adoptingObject = null; }
    }

    // After every seating: which seats another player holds; the rest are the host's bots.
    public void TagSeats(SimParty party, IReadOnlySet<PartyRole> remoteSeats)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (party.Get(role)?.Proxy is PeerProxy proxy && proxy.Seat != PeerSeat.LocalPlayer)
                proxy.Seat = remoteSeats.Contains(role) ? PeerSeat.Remote : PeerSeat.Bot;
    }

    private sealed class PeerBattleCharas(PeerNatives natives) : IBattleCharas
    {
        public IBattleCharaProxy LocalPlayer => natives.LocalPlayer;

        // A peer's sim spawns no NPC of its own: every one is the host's.
        public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
        {
            if (natives.adoptingCharacter is { } adopted)
            {
                entityId = adopted.EntityId;
                return new PeerProxy(adopted.Chara, PeerSeat.Silent);
            }
            entityId = 0;
            DiagnosticLog.Warn($"[Multiplayer] Peer: refused a local spawn of BNpcBase {config.BNpcBaseId} -- the host spawns every NPC.");
            return null;
        }

        public IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, byte level, Placement placement, out uint entityId)
            => natives.RealCharas.SpawnDoppel(preset, level, placement, out entityId) is { } chara ? new PeerProxy(chara, PeerSeat.Bot) : null;

        public bool IsInCharacterManager(uint entityId) => natives.RealCharas.IsInCharacterManager(entityId);

        public void ReleaseSlot(int slot) => natives.RealCharas.ReleaseSlot(slot);
    }

    // The spawn wall is the zone stay's, each client's own.
    private sealed class PeerEventObjects(PeerNatives natives) : IEventObjects
    {
        public IEventObjectProxy? Spawn(EventObjectSpawnConfig config, Placement placement)
            => natives.adoptingObject is { } adopted ? new SilentEventObject(adopted) : natives.RealObjects.Spawn(config, placement);
    }

    private sealed class SilentEventObject(IEventObjectProxy inner) : IEventObjectProxy
    {
        public int Slot => inner.Slot;
        public uint EntityId => inner.EntityId;
        public Vector3 Position => inner.Position;
        public float Rotation => inner.Rotation;
        public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0) { }
        public void Despawn() { }
    }

    // Taken as applied, so the caller's retry doesn't keep it.
    private sealed class PeerMapEffects(PeerNatives natives) : IMapEffects
    {
        public bool Loaded
        {
            get => natives.RealMap.Loaded;
            set => natives.RealMap.Loaded = value;
        }

        public bool Apply(uint packetFlags, byte index) => natives.HostOwnsArena || natives.RealMap.Apply(packetFlags, index);

        public bool ApplySnapshot(ReadOnlySpan<ushort> states, bool zoneIn) => natives.HostOwnsArena || natives.RealMap.ApplySnapshot(states, zoneIn);

        public void LogAllSlots(string label) => natives.RealMap.LogAllSlots(label);

        public void Dispose() => natives.RealMap.Dispose();
    }

    private sealed class PeerDirector(PeerNatives natives) : IInstanceContentDirector
    {
        public bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
            => natives.HostOwnsArena || natives.RealDirector.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6);

        public void SetDirectorData(byte sequence, byte unknown, byte[] unionData)
        {
            if (!natives.HostOwnsArena) natives.RealDirector.SetDirectorData(sequence, unknown, unionData);
        }

        public void Commence()
        {
            if (!natives.HostOwnsArena) natives.RealDirector.Commence();
        }
    }

    private sealed class PeerHaterList(PeerNatives natives) : IHaterList
    {
        public void Receive(IReadOnlyList<Hater> haters)
        {
            if (!natives.HostOwnsArena) natives.RealHaters.Receive(haters);
        }
    }

    private sealed class PeerZone(PeerNatives natives) : IZoneControl
    {
        private IZoneControl Real => natives.RealZone;

        public bool IsInInn() => Real.IsInInn();
        public string? StartBlockedReason(out string? settling) => Real.StartBlockedReason(out settling);
        public void EndSpeedBuffs() => Real.EndSpeedBuffs();
        public bool IsActive => Real.IsActive;
        public void TickGuard() => Real.TickGuard();
        public bool Enter(uint territoryId, Vector3 playerSpawn) => Real.Enter(territoryId, playerSpawn);
        public void Revert() => Real.Revert();

        public void ApplyWeather(byte weatherId)
        {
            if (!natives.HostOwnsArena) Real.ApplyWeather(weatherId);
        }

        public void SetWeather(byte weatherId, float transition = 0.5f)
        {
            if (!natives.HostOwnsArena) Real.SetWeather(weatherId, transition);
        }

        public void TickWeather() => Real.TickWeather();

        public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what)
            => Real.InjectIncomingPacket(sourceEntityId, opcode, body, what);

        public void HoldSendFirewall(bool hold) => Real.HoldSendFirewall(hold);
        public uint MaxActionEffectCounter => Real.MaxActionEffectCounter;
        public void Dispose() => Real.Dispose();
    }

    // Our player's afflictions are the host's: it holds the statuses that cause them.
    private sealed class PeerPlayerConditions(PeerNatives natives) : IPlayerConditions
    {
        public bool StatusAffliction => natives.RealConditions.StatusAffliction;
        public bool ControlAffliction => natives.RealConditions.ControlAffliction;
        public void SetAfflictions(bool status, bool control) { }
        public void EndClientLoad() => natives.RealConditions.EndClientLoad();
    }
}

// An actor's native as this client's own sim reaches it on a peer (see PeerSeat). Reads always go
// through: the sim reads what its client shows.
internal sealed class PeerProxy(IBattleCharaProxy inner, PeerSeat seat) : IBattleCharaProxy
{
    private const int StatusSlots = 60;
    private const uint GainEffectCategory = 0x14;
    private const uint LoseEffectCategory = 0x15;

    public IBattleCharaProxy Inner => inner;
    public PeerSeat Seat { get; set; } = seat;

    private bool Moves => Seat is PeerSeat.Remote or PeerSeat.LocalPlayer;

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
    public string DescribeDrawState() => inner.DescribeDrawState();
    public string DescribeActionTimeline() => inner.DescribeActionTimeline();
    public string DescribeTimelineSlotIds() => inner.DescribeTimelineSlotIds();
    public string DescribeModelSlots() => inner.DescribeModelSlots();

    public void SetPos(Vector3 position, float rotation)
    {
        if (Moves) inner.SetPos(position, rotation);
    }

    public void Move(Vector3 position, float rotation, ActorMoveFlags flags, float speed)
    {
        if (Moves) inner.Move(position, rotation, flags, speed);
    }

    public void NpcKnockback(Vector3 position, float rotation, float durationSeconds) { }

    // Our player's own presses reach the client without coming through here.
    public void ReceiveActorCast(ActorCastData cast) { }

    public uint ReceiveActionEffect(ActionEffectData effect) => 0;

    public void ReceiveEffectResult(uint globalSequence, byte targetIndex, uint health) { }

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
    {
        var passes = PacketRules.Controls.TryGetValue(category, out var rule) && Seat switch
        {
            PeerSeat.Bot or PeerSeat.Remote => rule.Delivery == Delivery.ZoneIn,
            // A status reported to the host is the host's to show (see WriteStatuses), and to announce.
            PeerSeat.LocalPlayer => rule.OwnPlayer
                                    && !(category is GainEffectCategory or LoseEffectCategory && HostReport.IsReported((ushort)arg1)),
            _ => false,
        };
        if (passes) inner.ActorControl(category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }

    public void ActorControlTarget(uint category, GameObjectId target, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0) { }

    public void ForcedFollow(uint? targetEntityId)
    {
        if (Seat == PeerSeat.LocalPlayer) inner.ForcedFollow(targetEntityId);
    }

    public void StopForcedMove()
    {
        if (Seat == PeerSeat.LocalPlayer) inner.StopForcedMove();
    }

    public void Despawn()
    {
        if (Seat != PeerSeat.Silent) inner.Despawn();
    }

    // ── Our player's status list ─────────────────────────────────────────────

    private StatusSlot[] hostStatuses = new StatusSlot[StatusSlots];
    private StatusSlot[] ownStatuses = new StatusSlot[StatusSlots];
    private byte hostShield;

    // From our own sim: only what our presses put on us, since every status the run puts on us
    // is on the host's copy of the seat.
    public void ReceiveStatusList(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
    {
        if (Seat != PeerSeat.LocalPlayer) return;
        ownStatuses = Fill(statuses);
        WriteStatuses(inner.Health, inner.Mana, Math.Max(hostShield, shield));
    }

    // The host's statuses for our seat, with its health already on our scale.
    public void ApplyHostStatuses(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
    {
        hostStatuses = Fill(statuses);
        hostShield = shield;
        WriteStatuses(health, mana, shield);
    }

    // The host's keep their slots. One of ours fills its own slot, or the next free one, unless
    // the host shows it too; a status our press reports to the host (mitigation, Sprint) is shown
    // from there, as the server's answer to the press, so the host's damage checks and every
    // client agree on it.
    private void WriteStatuses(uint health, uint mana, byte shield)
    {
        var combined = hostStatuses.ToArray();
        for (var i = 0; i < ownStatuses.Length; i++)
        {
            var own = ownStatuses[i];
            if (own.StatusId == 0 || HostReport.IsReported(own.StatusId)) continue;
            if (Array.Exists(combined, s => s.StatusId == own.StatusId)) continue;
            var slot = combined[i].StatusId == 0 ? i : Array.FindIndex(combined, s => s.StatusId == 0);
            if (slot >= 0) combined[slot] = own;
        }
        inner.ReceiveStatusList(combined, health, mana, shield);
    }

    private static StatusSlot[] Fill(IReadOnlyList<StatusSlot> statuses)
    {
        var slots = new StatusSlot[StatusSlots];
        for (var i = 0; i < statuses.Count && i < StatusSlots; i++) slots[i] = statuses[i];
        return slots;
    }
}
