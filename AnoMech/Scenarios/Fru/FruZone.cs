using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru;

public sealed class FruZone : IZone
{
    public static readonly FruZone Instance = new();
    public static readonly Phase P2 = new(Instance, "P2", WeatherId.Usurper, BgmId.Usurper,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [MapEffect.P2Platform] = 0x1 });
    public static readonly Phase P3 = new(Instance, "P3", WeatherId.Oracle, BgmId.Oracle,
        arenaMapEffectOverrides: new Dictionary<byte, ushort>
        {
            [MapEffect.OracleStage] = 0x1,
            [MapEffect.OracleFloor] = 0x1,
        });
    public static readonly Phase P4 = new(Instance, "P4", WeatherId.Oracle, BgmId.OracleAndUsurper,
        arenaMapEffectOverrides: new Dictionary<byte, ushort>
        {
            [MapEffect.OracleStage] = 0x1,
            [MapEffect.OracleFloor] = 0x1,
            [MapEffect.FragmentOfFate] = 0x1,
        });
    public static readonly Phase P5 = new(Instance, "P5", WeatherId.Pandora, BgmId.Pandora,
        arenaMapEffectOverrides: new Dictionary<byte, ushort> { [MapEffect.PandoraArena] = 0x1 });

    public string Name => "Futures Rewritten";
    public uint TerritoryId => 1238;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Inner Circle", InnerCircleWaymarks), FruJpWaymarks.Layout];

    public DutyInitData DutyInitData => FruReplayData.DutyInitData;

    public void Run(SimWorld world) => world.EnforceArenaBoundary(Geometry.ArenaRadius);
}
