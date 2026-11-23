using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu;

// Titan gets a "P3" menu prefix.
public sealed class UwuZone : IZone
{
    public static readonly UwuZone Instance = new();
    // Titan uses his trial arena's weather; Ultimania is Ultima's.
    private const byte EruptionsWeather = 29;
    public static readonly Phase Titan = new(Instance, "P3", EruptionsWeather, 546, init: SpawnTitanArenas);
    public static readonly Phase Ultima = new(Instance, "", 95, 547, init: SpawnUltimaArenas);

    // Titan's yellow ring; it shows up and shrinks on his jumps.
    public SimEventObject? TitanArena { get; private set; }

    public string Name => "The Weapon's Refrain";
    public uint TerritoryId => 777;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Standard", UwuConstants.StandardWaymarks), new WaymarkLayout("Naur", UwuConstants.NaurWaymarks)];

    public DutyInitData DutyInitData => UwuReplayData.DutyInitData;

    public void Run(SimWorld world)
    {
        world.EnforceArenaBoundary(UwuConstants.Geometry.ArenaRadius);
        world.StopNpcKnockbacksAt(UwuConstants.Geometry.NpcKnockbackStopRadius);
    }

    private static void SpawnTitanArenas(SimWorld world)
    {
        Instance.TitanArena = SpawnArenaEObj(world, UwuConstants.ArenaLayout.Titan);
        SpawnArenaEObj(world, UwuConstants.ArenaLayout.Static);
        foreach (var layout in UwuConstants.ArenaLayout.SwitchedAfterTitan) SpawnArenaEObj(world, layout);
    }

    // The same SharedGroups as Titan's, in the states his phase leaves them.
    private static void SpawnUltimaArenas(SimWorld world)
    {
        SpawnArenaEObj(world, UwuConstants.ArenaLayout.Titan);
        SpawnArenaEObj(world, UwuConstants.ArenaLayout.Static);
        foreach (var layout in UwuConstants.ArenaLayout.SwitchedAfterTitan) SpawnArenaEObj(world, layout, timelineState: 1);
    }

    private static SimEventObject? SpawnArenaEObj(SimWorld world, (uint Layout, uint Gimmick) layout, ushort timelineState = 4)
        => world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = UwuConstants.EObjId.Arena,
            Placement = new Placement(Vector3.Zero, 0f),
            TargetableStatus = 5,
            LayoutId = layout.Layout,
            GimmickId = layout.Gimmick,
            TimelineState = timelineState,
        });
}
