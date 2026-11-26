using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public sealed class TopZone : IZone
{
    public static readonly TopZone Instance = new();
    public static readonly Phase P2 = new(Instance, "P2", 78, BgmId.TopP2,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [0x0A] = 1 });
    public static readonly Phase P5 = new(Instance, "P5", 174, BgmId.TopP5);
    public static readonly Phase P6 = new(Instance, "P6", 175, BgmId.TopP6);

    public string Name => "The Omega Protocol";
    public uint TerritoryId => 1122;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Ring", Core.Game.WaymarkPresets.Ring(13.63f))];

    public DutyInitData DutyInitData => TopReplayData.DutyInitData;


    public void Run(SimWorld world) => world.EnforceArenaBoundary(new CircleArena(Geometry.ArenaRadius));
}
