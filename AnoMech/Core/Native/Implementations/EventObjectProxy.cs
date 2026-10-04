using System.Numerics;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class EventObjectProxy(int slot, GameObject* obj) : IEventObjectProxy
{
    private const uint NoTarget = 0xE0000000;

    private EventObject* EventObject => (EventObject*)obj;

    public bool Exists => obj != null;
    public int Slot => slot;
    public uint EntityId => obj->EntityId;
    public GameObjectId GameObjectId => obj->GetGameObjectId();

    public Vector3 Position => obj->Position;
    public float Rotation => obj->Rotation;
    public void SetPosition(Vector3 position) => obj->SetPosition(position.X, position.Y, position.Z);
    public void SetRotation(float rotation) => obj->SetRotation(rotation);

    // ── SharedGroup state ────────────────────────────────────────────────────

    public void SetState(ushort state) => EventObjectHelper.SetState(obj, state);

    public ushort SharedTimelineState => EventObject->SharedTimelineState;

    // Diff-based: plays the SGB timelines mapped to whichever state bits change.
    public void SetSharedTimelineState(ushort state) => EventObject->SetSharedTimelineState(state, true, 0);

    public void UpdateSharedTimelineState(ushort from, ushort to) => obj->UpdateSharedTimelineState(from, to);

    public void PlayAnimation(uint state, uint bitmask) => EventObject->PlayAnimation(state, bitmask, 0);

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        => PacketDispatcher.HandleActorControlPacket(obj->EntityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, NoTarget, false);

    public bool IsSharedGroupAttached => EventObject->SharedGroupLayoutInstance != null;
    public bool IsSharedGroupTimelinePlaying => LayoutInstanceDiagnostics.IsAnyTimelinePlaying(EventObject->SharedGroupLayoutInstance);
    public void SilenceSharedGroupSounds() => LayoutInstanceDiagnostics.SilenceSounds(EventObject->SharedGroupLayoutInstance);
    public bool DeactivateSharedGroup() => LayoutInstanceDiagnostics.Deactivate(EventObject->SharedGroupLayoutInstance);
    public bool ForceSharedGroupActive() => LayoutInstanceDiagnostics.ForceActive(EventObject->SharedGroupLayoutInstance);

    public void Despawn()
    {
        byte[] packet = [(byte)slot];
        fixed (byte* packetPtr = packet)
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, packetPtr);
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    public string DescribeSharedGroup() => LayoutInstanceDiagnostics.Describe(EventObject->SharedGroupLayoutInstance);

    public string DescribeLoadState(uint layoutId)
    {
        var layoutNote = layoutId != 0 ? $" layoutIdInstance={(LayoutInstanceDiagnostics.Exists(layoutId) ? "found" : "none")}" : "";
        return $"SharedTimelineState=0x{EventObject->SharedTimelineState:X} Flags=0x{EventObject->Flags:X} Arg=0x{EventObject->Arg:X} EventId=0x{(uint)obj->EventId:X} EntityId=0x{obj->EntityId:X} "
            + $"DrawObject=0x{(nint)obj->DrawObject:X} RenderFlags={obj->RenderFlags} IsReadyToDraw={obj->IsReadyToDraw()}{layoutNote} -- SG: {DescribeSharedGroup()}";
    }
}
