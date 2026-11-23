using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Network;

namespace AnoMech.Core.Native.Implementations;

// Through the spawn-object packet handler with a pre-picked slot. The handler doesn't return
// the slot it used, so EventObjectHelper picks the free one itself.
internal sealed unsafe class EventObjects : IEventObjects
{
    public IEventObjectProxy? Spawn(EventObjectSpawnConfig config, Placement placement)
    {
        var packet = BuildPacket(config, placement);
        if (!EventObjectHelper.Create(&packet, out var slot, out var obj))
        {
            // A full 40-slot EventObjectManager pool (EObjs left over from an earlier run, or
            // the zone's own) is the usual cause.
            DiagnosticLog.Warn($"[EventObjects.Spawn] Failed to spawn EObjId 0x{config.EObjId:X} at ({packet.PositionX:F2}, {packet.PositionY:F2}, {packet.PositionZ:F2}) -- EventObjectManager's 40-slot pool is likely full.");
            return null;
        }
        return new EventObjectProxy(slot, obj);
    }

    private static SpawnObjectPacket BuildPacket(EventObjectSpawnConfig config, Placement placement)
    {
        var packet = new SpawnObjectPacket
        {
            ObjectKind = 7, // EventObject
            // Retail sends 4 or 5 here, not the 0/1 that IsTargetable assumes.
#pragma warning disable CS0618
            TargetableStatus = config.TargetableStatus,
#pragma warning restore CS0618
            IsHidden = config.Hidden,
            BaseId = config.EObjId,
            EntityId = config.EntityId,
            LayoutId = config.LayoutId,
            EventId = config.EventId,
            OwnerId = config.OwnerId,
            GimmickId = config.GimmickId,
            Radius = config.Radius,
            Rotation = MathUtil.QuantizeRotation(placement.Rotation),
            FateId = config.FateId,
            EventState = config.EventState,
            Arg2 = config.Arg2,
            PositionX = placement.Position.X,
            PositionY = placement.Position.Y,
            PositionZ = placement.Position.Z,
        };

        // Private in FFXIVClientStructs.
        *(ushort*)((byte*)&packet + 0x2C) = config.TimelineState;
        return packet;
    }
}
