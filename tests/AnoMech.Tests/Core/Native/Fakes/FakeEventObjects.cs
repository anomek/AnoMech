using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

// EventObjectManager's 40-slot pool.
internal sealed class FakeEventObjects(SimTimeline timeline) : IEventObjects
{
    private const int SlotCount = 40;
    private const uint EntityIdBase = 0x40100000u;

    private readonly FakeEventObject?[] slots = new FakeEventObject?[SlotCount];

    // As FakeBattleCharas.SlotOffset.
    public int SlotOffset { get; set; }

    internal int Live => slots.Count(o => o is { Exists: true });

    internal IEnumerable<uint> LiveEntityIds => slots.Where(o => o is { Exists: true }).Select(o => o!.EntityId);

    public IEventObjectProxy? Spawn(EventObjectSpawnConfig config, Placement placement)
    {
        var slot = FindFreeSlot();
        if (slot < 0) return null;
        var entityId = config.EntityId != 0 ? config.EntityId : EntityIdBase + (uint)slot;
        timeline.Add("eobj_spawn", entityId, $"EObj{config.EObjId}", config.EObjId.ToString(), "EObj", position: placement.Position,
            rotation: placement.Rotation,
            detail: $"layout=0x{config.LayoutId:X} event=0x{(uint)config.EventId:X} owner=0x{config.OwnerId:X} gimmick=0x{config.GimmickId:X} "
                    + $"radius={SimTimeline.F(config.Radius)} event_state={config.EventState} targetable={config.TargetableStatus} "
                    + $"visibility={(config.Hidden ? 1 : 0)} index={config.ObjectIndex} timeline_state={config.TimelineState} arg2=0x{config.Arg2:X}",
            data: new SpawnData(config, placement));
        var obj = new FakeEventObject(slot, entityId, placement.Position, placement.Rotation, timeline, $"EObj{config.EObjId}");
        slots[slot] = obj;
        return obj;
    }

    private int FindFreeSlot()
    {
        for (var k = 0; k < slots.Length; k++)
        {
            var i = (SlotOffset + k) % slots.Length;
            if (slots[i] is not { Exists: true }) return i;
        }
        return -1;
    }
}
