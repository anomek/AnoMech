using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru;

public sealed class FruZone : IZone
{
    public static readonly FruZone Instance = new();
    public static readonly Phase P5 = new(Instance, "P5", WeatherId.Pandora, BgmId.Pandora, InitP5Arena);

    public string Name => "Futures Rewritten";
    public uint TerritoryId => 1238;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => FruConstants.Level;
    public ushort ItemLevel => 735;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("FRU PF - Inner Circle", FruUtils.FruWaymarks)];

    public void Run(SimWorld world)
    {
        world.EnforceArenaBoundary(Geometry.ArenaRadius);
    }

    private static void InitP5Arena(SimWorld world) => world.Events.Add(1f, () =>
    {
        // Wait for the zone's shared groups to load before applying their hide timelines.
        for (byte slot = 0; slot < MapEffect.SlotCount; slot++)
            world.Map.AddEffect(slot == MapEffect.PandoraArena ? MapEffect.Show : MapEffect.Hide, slot);
    });
}
