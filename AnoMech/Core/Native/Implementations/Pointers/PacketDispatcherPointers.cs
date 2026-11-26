using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Runtime.InteropServices;

namespace AnoMech.Core.Native.Implementations.Pointers;

[StructLayout(LayoutKind.Explicit, Size = 0x20)]
public partial struct ActorCastPacket
{
    [FieldOffset(0x00)] public ushort ActionId;
    [FieldOffset(0x02)] public byte ActionType;
    [FieldOffset(0x03)] public byte OmenDelay; // The value gets divided by 10.0f
    [FieldOffset(0x04)] public uint ActionId_2;
    [FieldOffset(0x08)] public float CastTime;
    [FieldOffset(0x0C)] public uint TargetEntityId;
    [FieldOffset(0x10)] public ushort RotationInt; // Quantized Rotation
    [FieldOffset(0x12)] public bool Interruptible;
    [FieldOffset(0x14)] public uint BallistaEntityId;
    [FieldOffset(0x18)] public ushort PositionX; // Quantized Position
    [FieldOffset(0x1A)] public ushort PositionY; // Quantized Position
    [FieldOffset(0x1C)] public ushort PositionZ; // Quantized Position
}

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public struct ActorSetPosPacket
{
    [FieldOffset(0x00)] public ushort RotationInt; // Quantized Rotation
    [FieldOffset(0x02)] public byte Type;
    [FieldOffset(0x03)] public byte Flags;
    [FieldOffset(0x08)] public float X;
    [FieldOffset(0x0C)] public float Y;
    [FieldOffset(0x10)] public float Z;
}

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public struct ActorMovePacket
{
    [FieldOffset(0x00)] public ushort RotationInt; // Quantized Rotation
    [FieldOffset(0x02)] public byte MoveFlags;
    [FieldOffset(0x03)] public byte State;
    [FieldOffset(0x04)] public byte Speed;
    [FieldOffset(0x06)] public ushort PositionX; // Quantized Position
    [FieldOffset(0x08)] public ushort PositionY; // Quantized Position
    [FieldOffset(0x0A)] public ushort PositionZ; // Quantized Position
}

// Flag: retail sends 1 for Confused; what 0 would do is UNVERIFIED.
[StructLayout(LayoutKind.Explicit, Size = 0x8)]
public struct ForcedFollowPacket
{
    [FieldOffset(0x00)] public uint TargetEntityId;
    [FieldOffset(0x04)] public byte Flag;
}

// Range in yalms, whole numbers only.
[StructLayout(LayoutKind.Explicit, Size = 0x8)]
public struct ForcedApproachPacket
{
    [FieldOffset(0x00)] public byte Range;
    [FieldOffset(0x04)] public uint TargetEntityId;
}

// The server moving an NPC a knockback pushed; it slides there over DurationSeconds.
[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public struct NpcKnockbackPacket
{
    [FieldOffset(0x00)] public ushort RotationInt; // Quantized Rotation
    [FieldOffset(0x04)] public float DurationSeconds;
    [FieldOffset(0x0A)] public ushort PositionX; // Quantized Position
    [FieldOffset(0x0C)] public ushort PositionY; // Quantized Position
    [FieldOffset(0x0E)] public ushort PositionZ; // Quantized Position
}

[StructLayout(LayoutKind.Explicit, Size = 0x1)]
public partial struct DespawnCharacterPacket
{
    [FieldOffset(0x0)] public byte Index;
}

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public partial struct UpdateClassInfoPacket
{
    [FieldOffset(0x0)] public byte ClassJobId;
    [FieldOffset(0x2)] public ushort CurrentLevel;
    [FieldOffset(0x4)] public ushort ClassJobLevel;
    [FieldOffset(0x6)] public ushort SyncedLevel;
    [FieldOffset(0x8)] public ushort ClassJobExp;
    [FieldOffset(0xC)] public uint BaseRestedExperience;
}

// The local player's whole attribute block. The handler writes every BaseParam row with a
// PacketIndex to PlayerState.Attributes[row] from Attributes[PacketIndex], so a packet must carry
// all of them, not only the ones that change. BaseStats go to PlayerState.BaseStrength..BasePiety.
[StructLayout(LayoutKind.Explicit, Size = 0x90)]
public unsafe struct PlayerStatsPacket
{
    public const int AttributesCapacity = 30;
    public const int BaseStatsCapacity = 6;

    [FieldOffset(0x00)] public fixed int Attributes[AttributesCapacity];
    [FieldOffset(0x78)] public fixed int BaseStats[BaseStatsCapacity];
}

[StructLayout(LayoutKind.Explicit, Size = 0x8)]
public struct HaterPacketEntry
{
    [FieldOffset(0x0)] public uint EntityId;
    [FieldOffset(0x4)] public byte Enmity;
}

[StructLayout(LayoutKind.Explicit, Size = 0x108)]
public struct HaterPacket
{
    public const int Capacity = 32;
    public const int EntriesOffset = 0x4;

    [FieldOffset(0x0)] public byte Count;
}

[StructLayout(LayoutKind.Explicit, Size = 0xC)]
public struct StatusEffectListStatus
{
    [FieldOffset(0x0)] public ushort StatusId;
    [FieldOffset(0x2)] public ushort Param;
    [FieldOffset(0x4)] public float RemainingTime;
    [FieldOffset(0x8)] public uint SourceEntityId;
}

// The server's full HP/MP and status state for one character: two 30-entry status lists around
// the HP/MP block, Statuses for StatusManager slots 0-29 and ExtraStatuses for 30-59.
[StructLayout(LayoutKind.Explicit, Size = 0x2E8)]
public struct StatusEffectListDoublePacket
{
    public const int Capacity = 30;
    public const int ExtraStatusesOffset = 0x000;
    public const int StatusesOffset = 0x17C;

    [FieldOffset(0x168)] public byte ClassJob;
    [FieldOffset(0x169)] public byte Level;
    [FieldOffset(0x16B)] public byte Level_2;
    [FieldOffset(0x16C)] public uint CurrentHP;
    [FieldOffset(0x170)] public uint MaxHP;
    [FieldOffset(0x174)] public ushort CurrentMP;
    [FieldOffset(0x176)] public ushort MaxMP;
    [FieldOffset(0x178)] public byte DamageShield;
}

// One target's confirmation of one ActionEffect; the client applies that target's queued effects
// when it arrives, then sets its HP.
[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public struct EffectResultBasicPacket
{
    [FieldOffset(0x00)] public byte Count;
    [FieldOffset(0x04)] public uint GlobalSequence;
    [FieldOffset(0x08)] public uint EntityId;
    [FieldOffset(0x0C)] public uint CurrentHP;
    // The target's index in the ActionEffect's target list.
    [FieldOffset(0x10)] public byte TargetIndex;
}

[StructLayout(LayoutKind.Explicit, Size = 0xC)]
public struct UpdatePartyPacketStatus
{
    [FieldOffset(0x0)] public ushort StatusId;
    [FieldOffset(0x2)] public ushort Param;
    // The handler applies every status with no remaining time; this field is never read.
    [FieldOffset(0x4)] public float RemainingTime;
    [FieldOffset(0x8)] public uint SourceEntityId;
}

[StructLayout(LayoutKind.Explicit, Size = 0x1C8)]
public unsafe struct UpdatePartyPacketMember
{
    public const int NameLength = 0x20;
    public const int StatusCount = 30;
    public const int StatusesOffset = 0x60;

    [FieldOffset(0x00)] public fixed byte Name[NameLength];
    [FieldOffset(0x28)] public ulong AccountId;
    [FieldOffset(0x30)] public ulong ContentId;
    [FieldOffset(0x38)] public uint EntityId;
    [FieldOffset(0x3C)] public uint PetEntityId;
    [FieldOffset(0x40)] public uint CompanionEntityId;
    [FieldOffset(0x44)] public uint CurrentHP;
    [FieldOffset(0x48)] public uint MaxHP;
    [FieldOffset(0x4C)] public ushort CurrentMP;
    [FieldOffset(0x4E)] public ushort MaxMP;
    [FieldOffset(0x50)] public ushort HomeWorld;
    [FieldOffset(0x52)] public ushort TerritoryType;
    // Always 1 in retail; bit 0 goes to PartyMember's valid flag.
    [FieldOffset(0x54)] public byte Valid;
    [FieldOffset(0x55)] public byte ClassJob;
    [FieldOffset(0x56)] public byte Sex;
    [FieldOffset(0x57)] public byte Level;
    // Retail carries the synced level; the handler only sets PartyMember.Flags 0x08 when non-zero.
    [FieldOffset(0x58)] public byte SyncedLevel;
}

// The whole roster: the server resends it on every join and leave, compacting the slots;
// leaving the party sends it all zero.
[StructLayout(LayoutKind.Explicit, Size = 0xE58)]
public struct UpdatePartyPacket
{
    public const int Capacity = 8;

    [FieldOffset(0xE40)] public long PartyId;
    [FieldOffset(0xE48)] public long PartyId_2;
    [FieldOffset(0xE50)] public byte LeaderIndex;
    [FieldOffset(0xE51)] public byte MemberCount;
}

// All eight field markers at once; a slot whose ActiveMask bit is clear is removed.
[StructLayout(LayoutKind.Explicit, Size = 0x64)]
public unsafe struct FieldMarkerPresetPacket
{
    public const int Capacity = 8;
    public const float PositionScale = 1000f;

    [FieldOffset(0x00)] public byte ActiveMask;
    [FieldOffset(0x04)] public fixed int X[Capacity];
    [FieldOffset(0x24)] public fixed int Y[Capacity];
    [FieldOffset(0x44)] public fixed int Z[Capacity];
}

// One MapEffect slot change. The handler drops it unless DirectorId is the active content
// director's event id.
[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public struct MapEffectPacket
{
    [FieldOffset(0x0)] public uint DirectorId;
    [FieldOffset(0x4)] public ushort State;
    [FieldOffset(0x6)] public ushort Timeline;
    [FieldOffset(0x8)] public byte Index;
}

// Every slot's resting state at once; plays no timeline. Retail sends 0x88 bytes, but the handler
// reads one state per slot up to MapEffectList.ItemCount, so this holds all 128.
[StructLayout(LayoutKind.Explicit, Size = 0x106)]
public unsafe struct MapEffectSnapshotPacket
{
    public const int Capacity = 128;
    // Bit 0 applies the states on the director's next update (retail always sets it); bit 2 also
    // writes 5 to MapEffectList+0x604 (retail: zone-in only); bit 1 skips slots whose item+4 bit 0 is set.
    public const ushort HeaderReset = 0x1;
    public const ushort HeaderZoneIn = 0x5;

    [FieldOffset(0x0)] public uint DirectorId;
    [FieldOffset(0x4)] public ushort Header;
    [FieldOffset(0x6)] public fixed ushort States[Capacity];
}

// Every recast group's state at once; at zone-in the server sends it all zero, which clears every
// cooldown. The Duty Recorder never writes it, so replays don't contain it.
[StructLayout(LayoutKind.Explicit, Size = 0x280)]
public unsafe struct UpdateRecastTimesPacket
{
    public const int Capacity = 80;

    [FieldOffset(0x000)] public fixed float Elapsed[Capacity];
    [FieldOffset(0x140)] public fixed float Total[Capacity];
}

// The server's whole job gauge. The current gauge's ReceivePacketValues copies Values onto its own
// struct from offset 0x08 on; how many bytes, and which it keeps (MCH's overheat timer), is per job.
[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public unsafe struct ActorGaugePacket
{
    public const int ValuesLength = 0x0E;
    // Where Values land in JobGaugeManager: its gauge union starts at 0x08.
    public const int ManagerValuesOffset = 0x10;

    [FieldOffset(0x0)] public byte ClassJobId;
    [FieldOffset(0x2)] public fixed byte Values[ValuesLength];
}

// Sets WeatherManager's server weather slot, which the environment follows. Retail sends it at
// zone-in, at every phase change and on a wipe reset.
[StructLayout(LayoutKind.Explicit, Size = 0x8)]
public struct WeatherChangePacket
{
    [FieldOffset(0x0)] public byte WeatherId;
    // Bit 0 only. Retail sets it on duty weathers; UCOB clears it when resetting to the
    // territory's own weather.
    [FieldOffset(0x1)] public byte Forced;
    [FieldOffset(0x4)] public float TransitionTime;
}

internal unsafe class PacketDispatcherPointers
{
    [Signature("40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleActorCastPacketDelegate HandleActorCastPacket { get; private set; } = null!;

    [Signature("40 53 48 83 EC 20 48 8B DA 48 8D 0D ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleDespawnObjectPacketDelegate HandleDespawnObjectPacket { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 57 48 83 EC 40 0F B6 1A", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleDespawnCharacterPacketDelegate HandleDespawnCharacterPacket { get; private set; } = null!;

    // Technically the real HandleUpdateClassInfoPacket is a wrapper to this sig... but this is still close to other HandleX methods, so it fits here
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 48 8D 0D ?? ?? ?? ?? 33", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleUpdateClassInfoPacketDelegate HandleUpdateClassInfoPacket { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 FF 48 8B F2 8B DF E8 ?? ?? ?? ?? 85 C0 74 ?? 90 8B CB", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandlePlayerStatsPacketDelegate HandlePlayerStatsPacket { get; private set; } = null!;

    [Signature("48 89 74 24 ?? 57 48 83 EC ?? 0F B6 02 33 F6 89 05 ?? ?? ?? ?? 48 8B FA 40 38 32", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleUpdateHaterPacketDelegate HandleUpdateHaterPacket { get; private set; } = null!;

    // replay 0 is what the live packet dispatcher passes.
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 83 3D ?? ?? ?? ?? 03 41 0F B6 F0 48 8B DA 8B F9 74 ?? 48 81 C2 68 01 00 00", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleStatusEffectListDoublePacketDelegate HandleStatusEffectListDoublePacket { get; private set; } = null!;

    // replay 0 is what the live packet dispatcher passes.
    [Signature("40 53 41 54 41 55 48 83 EC 40 83 3D ?? ?? ?? ?? 03 45 0F B6 E0 48 8B DA", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleEffectResultBasicPacketDelegate HandleEffectResultBasicPacket { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 41 0F B6 D8", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleUpdatePartyPacketDelegate HandleUpdatePartyPacket { get; private set; } = null!;

    // A 12-byte thunk shared in shape by many handlers; the pattern runs on into
    // HandleActorMovePacket, which the compiler places right after it, to stay unique.
    [Signature("4C 8B C2 8B D1 48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 08 57 48 83 EC 20 4C 63 05", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleActorSetPosPacketDelegate HandleActorSetPosPacket { get; private set; } = null!;

    // Queues the packet; the client applies it to the actor's move controller on a later frame.
    [Signature("48 89 5C 24 08 57 48 83 EC 20 4C 63 05 ?? ?? ?? ?? 48 8B DA 33 D2 8B F9 41 8D 40 01", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleActorMovePacketDelegate HandleActorMovePacket { get; private set; } = null!;

    // Queued like ActorMove, then handed to the character's move controller at +0x4C0.
    [Signature("48 89 6C 24 18 56 48 83 EC 20 4C 63 0D ?? ?? ?? ?? 48 8B F2 44 8B 1D ?? ?? ?? ?? 33 D2 44 8B 05 ?? ?? ?? ?? 8B E9 41 8D", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleNpcKnockbackPacketDelegate HandleNpcKnockbackPacket { get; private set; } = null!;

    // The handler body behind the dispatcher's thunk, which only loads MarkingController.Instance().
    // Unlike PlacePreset it has no territory check.
    [Signature("0F B6 02 4C 8B D9 F3 0F 10 15 ?? ?? ?? ?? 24 01", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleFieldMarkerPresetPacketDelegate HandleFieldMarkerPresetPacket { get; private set; } = null!;

    // The handler body behind the dispatcher's thunk, which only loads EventFramework.Instance().
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B F9 48 8B DA 48 8B 89 58 01 00 00 48 85 C9", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleMapEffectPacketDelegate HandleMapEffectPacket { get; private set; } = null!;

    // Also a body behind a thunk. The game has two byte-identical bodies, one per snapshot
    // opcode; the pattern matches both and either does the same.
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 48 8B FA 48 8B 89 58 01 00 00 48 85 C9 0F 84 ?? ?? ?? ?? 48 8B 01 FF 50 18 84 C0 0F 84 ?? ?? ?? ?? 4C 8B 9B 58 01 00 00 8B 07 49 8B 8B D8 02 00 00 39 01 0F 85 ?? ?? ?? ?? 49 8B 83 90 0C 00 00 48 85 C0 0F 84 ?? ?? ?? ?? 0F B6 57 04 45 32 D2", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleMapEffectSnapshotPacketDelegate HandleMapEffectSnapshotPacket { get; private set; } = null!;

    // A packet for another job than ClassJobId rebuilds the gauge for that job instead.
    [Signature("4C 8B 01 4D 85 C0 74 ?? 0F B6 02 38 41 58 75 ?? 49 8B 00 49 8B C8 48 FF 60 18", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleActorGaugePacketDelegate HandleActorGaugePacket { get; private set; } = null!;

    // 16 B, u16 TerritoryType at +4; sent at zone-in and ~3.2s after every wipe. Another territory
    // than the one loaded makes the client load it. What it resets in place is UNVERIFIED. The live
    // dispatcher passes 1.
    [Signature("44 0F B6 C2 48 8B D1 48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleTerritoryInitPacketDelegate HandleTerritoryInitPacket { get; private set; } = null!;

    public delegate void HandleTerritoryInitPacketDelegate(byte* packet, byte flag);

    // The handler body behind the dispatcher's thunk; it ignores the WeatherManager the thunk
    // passes and loads its own.
    [Signature("44 0F B6 4A 01 48 8D 0D ?? ?? ?? ?? F3 0F 10 52 04 41 80 E1 01", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleWeatherChangePacketDelegate HandleWeatherChangePacket { get; private set; } = null!;

    public delegate void HandleWeatherChangePacketDelegate(WeatherManager* manager, WeatherChangePacket* packet);

    // The dispatcher handles the director-vars packet {u32 director id, u8 sequence, u8 unknown,
    // u8 data[12]} inline and calls this with its fields; replay playback calls it the same way.
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B D9 41 0F B6 F8 0F B6 F2 8B E9 E8", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleDirectorVarsPacketDelegate HandleDirectorVarsPacket { get; private set; } = null!;

    public const int DirectorVarsDataLength = 12;

    public delegate void HandleDirectorVarsPacketDelegate(uint directorId, byte sequence, byte unknown, byte* data, ulong length);

    // Where the BattleTalk packet lands: shows the line in the BattleTalk box and plays its voice.
    // speakerKind is the speaker's ObjectKind, which picks the sheet speakerNameId is read from
    // (BattleNpc: BNpcName). Nullable so a patch that moves it costs the lines, not the plugin.
    [Signature("40 53 55 56 48 81 EC C0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 B0 00 00 00 F7 05 ?? ?? ?? ?? FD FF FF FF", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleBattleTalkPacketDelegate? HandleBattleTalkPacket { get; private set; }

    public delegate void HandleBattleTalkPacketDelegate(
        EventFramework* framework, uint directorId, ulong speakerEntityId, byte speakerKind, uint speakerNameId,
        uint textId, uint durationMs, uint unknown1, uint unknown2, uint* textArgs, byte textArgCount);

    public const string HandleUpdateRecastTimesPacketSignature = "48 89 5C 24 08 57 48 83 EC 20 48 8B DA 33 FF 90 F3 0F 10 9B 40 01 00 00";

    [Signature(HandleUpdateRecastTimesPacketSignature, UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleUpdateRecastTimesPacketDelegate HandleUpdateRecastTimesPacket { get; private set; } = null!;

    // The server addresses it to the local player.
    public delegate void HandleUpdateRecastTimesPacketDelegate(uint entityId, UpdateRecastTimesPacket* packet);

    // The handler body behind the dispatcher's thunk, which only loads Conditions.Instance(). Bit n
    // is the n-th Condition row with IsNetworked, in row order; every networked condition is
    // written, so a packet must carry all of them. The Duty Recorder never writes it, so replays
    // don't contain it.
    [Signature("48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 56 41 57 48 83 EC 20 4C 8B FA 4C 8B E1 E8 ?? ?? ?? ?? 48 8B C8 48 8B D8 E8", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandlePlayerStateFlagsPacketDelegate HandlePlayerStateFlagsPacket { get; private set; } = null!;

    public const int PlayerStateFlagsLength = 16;

    public delegate void HandlePlayerStateFlagsPacketDelegate(Conditions* conditions, byte* bits);

    // A forced-move controller on the client drives the local player; these are never in a
    // replay. Follow only turns the player to face the target, done once facing it; retail sends
    // end + follow about every 0.1s to a Confused player.
    [Signature("44 0F B6 41 04 8B 11 48 8D 0D ?? ?? ?? ?? E9", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleForcedFollowPacketDelegate HandleForcedFollowPacket { get; private set; } = null!;

    // Unique only together with the next function, which the compiler places after it.
    [Signature("48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 8B 15 ?? ?? ?? ?? 48 85 D2 74 0C 48 8D 0D", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleForcedFollowEndPacketDelegate HandleForcedFollowEndPacket { get; private set; } = null!;

    public delegate void HandleForcedFollowPacketDelegate(ForcedFollowPacket* packet);

    // Takes no payload.
    public delegate void HandleForcedFollowEndPacketDelegate(nint unused);

    // The same controller's approach mode: the client walks its own player to the entity until
    // within Range. UNVERIFIED that retail uses it for Confused.
    [Signature("0F B6 01 44 8B 41 04 48 8D 0D ?? ?? ?? ?? 66 0F 6E C8", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleForcedApproachPacketDelegate HandleForcedApproachPacket { get; private set; } = null!;

    public delegate void HandleForcedApproachPacketDelegate(ForcedApproachPacket* packet);

    // Hard stop: ends the controller's mode and halts the local player where they stand. Takes no
    // payload; the handler loads the player itself.
    [Signature("48 8B 15 ?? ?? ?? ?? 48 85 D2 74 0C 48 8D 0D ?? ?? ?? ?? E9", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static HandleForcedStopPacketDelegate HandleForcedStopPacket { get; private set; } = null!;

    public delegate void HandleForcedStopPacketDelegate(nint unused);

    public delegate void HandleActorCastPacketDelegate(uint entityId, ActorCastPacket* packet);
    public delegate void HandleActorSetPosPacketDelegate(uint entityId, ActorSetPosPacket* packet);
    public delegate void HandleActorMovePacketDelegate(uint entityId, ActorMovePacket* packet);
    public delegate void HandleNpcKnockbackPacketDelegate(uint entityId, NpcKnockbackPacket* packet);
    public delegate void HandleDespawnObjectPacketDelegate(uint unused, byte* packet);
    public delegate void HandleDespawnCharacterPacketDelegate(ulong unused, DespawnCharacterPacket* packet);
    public delegate void HandleUpdateClassInfoPacketDelegate(ulong unused, UpdateClassInfoPacket* packet);
    public delegate void HandlePlayerStatsPacketDelegate(uint unused, PlayerStatsPacket* packet);
    public delegate void HandleUpdateHaterPacketDelegate(ulong unused, HaterPacket* packet);
    public delegate void HandleEffectResultBasicPacketDelegate(uint entityId, EffectResultBasicPacket* packet, byte replay);
    public delegate void HandleStatusEffectListDoublePacketDelegate(uint entityId, StatusEffectListDoublePacket* packet, byte replay);
    // replayGroup: non-zero writes GroupManager.ReplayGroup (replay playback) instead of MainGroup.
    public delegate void HandleUpdatePartyPacketDelegate(uint unused, UpdatePartyPacket* packet, byte replayGroup);
    public delegate void HandleFieldMarkerPresetPacketDelegate(MarkingController* controller, FieldMarkerPresetPacket* packet);
    public delegate void HandleMapEffectPacketDelegate(EventFramework* framework, MapEffectPacket* packet);
    public delegate void HandleMapEffectSnapshotPacketDelegate(EventFramework* framework, MapEffectSnapshotPacket* packet);
    public delegate void HandleActorGaugePacketDelegate(JobGaugeManager* manager, ActorGaugePacket* packet);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new PacketDispatcherPointers());
    }
}
