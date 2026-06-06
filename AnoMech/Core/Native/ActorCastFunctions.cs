// https://github.com/aers/FFXIVClientStructs/pull/1855
using System.Runtime.InteropServices;

namespace AnoMech.Core.Native;

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

internal static unsafe class ActorCastFunctions
{
    private delegate nint HandleActorCastPacketDelegate(uint entityId, ActorCastPacket* packet);

    private static readonly HandleActorCastPacketDelegate HandleActorCastPacketFunction;

    static ActorCastFunctions()
    {
        var addr = Plugin.SigScanner.ScanText(
            "40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B");

        HandleActorCastPacketFunction =
            Marshal.GetDelegateForFunctionPointer<HandleActorCastPacketDelegate>(addr);

        Plugin.Log.Information("[DirectorFunctions] Initialized.");
    }

    internal static void HandleActorCastPacket(uint entityId, ActorCastPacket* packet)
    {
        HandleActorCastPacketFunction(entityId, packet);
    }
}
