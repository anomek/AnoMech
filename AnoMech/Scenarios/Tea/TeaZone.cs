using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea;

public sealed class TeaZone : IZone
{
    public static readonly TeaZone Instance = new();

    public static readonly Phase I1 = new(Instance, "I1", Weather.FairSkies, Bgm.P1);

    // Arena slot 2 rests on Alexander Prime's floor from Temporal Stasis on.
    public static readonly Phase P3 = new(Instance, "P3", Weather.BeyondTime, Bgm.P3, init: world =>
    {
        world.Map.AddEffect(0x00010001, 2);
        world.Map.DirectorUpdate(0x40000007, 0x0, 0x1);
    });

    public string Name => "The Epic of Alexander";
    public uint TerritoryId => 887;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => TeaConstants.Level;
    public ushort ItemLevel => TeaConstants.ItemLevel;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("PF Trines", TeaConstants.PfTrinesWaymarks)];

    public DutyInitData DutyInitData => TeaReplayData.DutyInitData;

    // Every restart sets each arena slot on its own rather than in one snapshot.
    public void Run(SimWorld world)
    {
        world.EnforceArenaBoundary(Geometry.ArenaRadius);
        for (var i = 0; i < ArenaStates.Length; i++)
        {
            var state = ArenaStates[i];
            world.Map.AddEffect(((uint)state << 16) | state, (byte)(i + 1));
        }
        foreach (var (layout, gimmick) in ArenaLayouts)
            world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = ArenaEObjId,
                Placement = new Placement(Vector3.Zero, 0f),
                TargetableStatus = 5,
                LayoutId = layout,
                GimmickId = gimmick,
                TimelineState = 4,
            });
    }
}
