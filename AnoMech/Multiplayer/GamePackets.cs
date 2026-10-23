using System.Collections.Generic;
using System.Text.Json.Serialization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Multiplayer;

// What the host's sim sent its own game client in one frame, in order, for every peer to play on
// its own (PacketRecorder, PacketReplay).
public sealed record GamePacketsMessage(List<GamePacket> Packets) : MpMessage, IHostOnlyMessage;

// An actor as every client can find it: a party seat, the receiving client's own player, or an
// actor the host spawned, by its entity id on the host. Never a party member's entity id, which
// differs on every client.
internal static class PacketActor
{
    public const uint None = 0;
    private const uint FirstSeat = 1;
    public const uint Self = FirstSeat + 8;

    public static uint Seat(PartyRole role) => FirstSeat + (uint)role;

    public static bool IsSeat(uint actor, out PartyRole role)
    {
        role = (PartyRole)(actor - FirstSeat);
        return actor >= FirstSeat && actor < Self;
    }

    public static bool IsSpawned(uint actor) => actor > Self;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "k")]
[JsonDerivedType(typeof(SpawnNpcPacket), "npc")]
[JsonDerivedType(typeof(SpawnObjPacket), "obj")]
[JsonDerivedType(typeof(DespawnPacket), "despawn")]
[JsonDerivedType(typeof(SetPosPacket), "setPos")]
[JsonDerivedType(typeof(MovePacket), "move")]
[JsonDerivedType(typeof(NpcKnockbackPacket), "npcKnockback")]
[JsonDerivedType(typeof(CastPacket), "cast")]
[JsonDerivedType(typeof(EffectPacket), "effect")]
[JsonDerivedType(typeof(EffectResultPacket), "effectResult")]
[JsonDerivedType(typeof(ControlPacket), "control")]
[JsonDerivedType(typeof(ControlTargetPacket), "controlTarget")]
[JsonDerivedType(typeof(StatusListPacket), "statuses")]
[JsonDerivedType(typeof(MapEffectPacket), "mapEffect")]
[JsonDerivedType(typeof(MapEffectSnapshotPacket), "mapSnapshot")]
[JsonDerivedType(typeof(DirectorUpdatePacket), "director")]
[JsonDerivedType(typeof(DirectorDataPacket), "directorData")]
[JsonDerivedType(typeof(CommencePacket), "commence")]
[JsonDerivedType(typeof(BattleTalkPacket), "battleTalk")]
[JsonDerivedType(typeof(HatersPacket), "haters")]
[JsonDerivedType(typeof(WeatherPacket), "weather")]
[JsonDerivedType(typeof(AfflictionsPacket), "afflictions")]
public abstract record GamePacket
{
    // The seat whose own client showed this already, its player's own action: that client skips it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PartyRole? ShownBy { get; init; }
}

// Positions and rotations are world space, as the natives take them.
public sealed record SpawnNpcPacket(
    uint Actor, uint BNpcBaseId, uint NameId, byte Level, bool Targetable, EnemyListMode EnemyList, SpawnVisibility Visibility,
    uint ModelCharaId, byte? InitialModeAttributeFlags, uint? DisplayFlags, bool Engaged, uint LayoutId, bool IsHostile,
    float X, float Y, float Z, float Rotation, byte ModelState, byte ModelScale, NpcAppearance? Appearance, List<SpawnStatus>? Statuses)
    : GamePacket;

public sealed record SpawnObjPacket(
    uint Actor, uint EObjId, sbyte ObjectIndex, byte TargetableStatus, bool Hidden, uint EntityId, uint LayoutId, uint EventId,
    uint Owner, uint GimmickId, float Radius, ushort FateId, byte EventState, uint Arg2, ushort TimelineState,
    float X, float Y, float Z, float Rotation) : GamePacket;

public sealed record DespawnPacket(uint Actor) : GamePacket;

public sealed record SetPosPacket(uint Actor, float X, float Y, float Z, float Rotation) : GamePacket;

public sealed record MovePacket(uint Actor, float X, float Y, float Z, float Rotation, ActorMoveFlags Flags, float Speed) : GamePacket;

public sealed record NpcKnockbackPacket(uint Actor, float X, float Y, float Z, float Rotation, float DurationSeconds) : GamePacket;

public sealed record CastPacket(
    uint Actor, uint ActionId, ActionType ActionType, float CastTime, float OmenDelay, bool Interruptible, float Rotation,
    float X, float Y, float Z, uint Target, uint Ballista) : GamePacket;

// Sequence is the host's global one, which its EffectResults name.
public sealed record EffectPacket(
    uint Actor, uint Sequence, uint ActionId, ActionType ActionType, float AnimationLock, ushort SpellId, byte AnimationVariation,
    byte Flags, float Rotation, float X, float Y, float Z, uint AnimationTarget, List<EffectTargetPacket>? Targets, uint Ballista) : GamePacket;

public sealed record EffectTargetPacket(uint Target, List<ActionEffectEntry>? Effects);

// Health is on the host's scale, MaxHealth that actor's maximum on the host.
public sealed record EffectResultPacket(uint Actor, uint Sequence, byte TargetIndex, uint Health, uint MaxHealth) : GamePacket;

// An argument that names an actor goes as ArgActor, at Args[ArgIndex]. OwnerOnly: an ActorControlSelf
// only the seat's own client acts on.
public sealed record ControlPacket(uint Actor, uint Category, uint[] Args, int ArgIndex = -1, uint ArgActor = 0, bool OwnerOnly = false) : GamePacket;

public sealed record ControlTargetPacket(uint Actor, uint Category, uint Target, uint[] Args, int ArgIndex = -1, uint ArgActor = 0) : GamePacket;

public sealed record StatusListPacket(uint Actor, List<StatusSlotPacket> Slots, uint Health, uint MaxHealth, uint Mana, byte Shield) : GamePacket;

public sealed record StatusSlotPacket(byte Slot, ushort StatusId, ushort Param, float RemainingTime, uint Source);

public sealed record MapEffectPacket(uint PacketFlags, byte Index) : GamePacket;

public sealed record MapEffectSnapshotPacket(ushort[] States, bool ZoneIn) : GamePacket;

public sealed record DirectorUpdatePacket(uint Category, uint Arg1, uint Arg2, uint Arg3, uint Arg4, uint Arg5, uint Arg6) : GamePacket;

public sealed record DirectorDataPacket(byte Sequence, byte Unknown, byte[] Data) : GamePacket;

public sealed record CommencePacket : GamePacket;

public sealed record BattleTalkPacket(uint SpeakerNameId, uint TextId, uint DurationMs) : GamePacket;

public sealed record HatersPacket(List<HaterPacket> Haters) : GamePacket;

public sealed record HaterPacket(uint Actor, byte Enmity);

// AfterLoad: IZoneControl.ApplyWeather, which waits for a fresh load to settle.
public sealed record WeatherPacket(byte WeatherId, float Transition, bool AfterLoad) : GamePacket;

// The server's PlayerStateFlags for a seat's own client: the host computes it from the statuses
// it holds on that seat, as SimPlayer does for the local player.
public sealed record AfflictionsPacket(uint Actor, bool Status, bool Control) : GamePacket;
