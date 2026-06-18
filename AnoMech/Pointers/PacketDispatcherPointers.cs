using Dalamud.Utility.Signatures;
using System.Runtime.InteropServices;

namespace AnoMech.Pointers;

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

    public delegate void HandleActorCastPacketDelegate(uint entityId, ActorCastPacket* packet);
    public delegate void HandleDespawnObjectPacketDelegate(uint unused, byte* packet);
    public delegate void HandleDespawnCharacterPacketDelegate(ulong unused, DespawnCharacterPacket* packet);
    public delegate void HandleUpdateClassInfoPacketDelegate(ulong unused, UpdateClassInfoPacket* packet);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new PacketDispatcherPointers());
    }
}
