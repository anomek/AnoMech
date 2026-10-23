using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea;

public sealed class TeaZone : IZone
{
    public static readonly TeaZone Instance = new();

    // The first intermission, Limit Cut, runs to the P1 track; P2's own starts with J Kick.
    public static readonly Phase P1 = new(Instance, "P1", Weather.FairSkies, Bgm.P1);
    public static readonly Phase I1 = new(Instance, "I1", Weather.FairSkies, Bgm.P1);
    public static readonly Phase P2 = new(Instance, "P2", Weather.FairSkies, Bgm.P1);
    public static readonly Phase I2 = new(Instance, "I2", Weather.FairSkiesP2, Bgm.P2);

    // Arena slot 2 rests on Alexander Prime's floor from Temporal Stasis on.
    public static readonly Phase P3 = new(Instance, "P3", Weather.BeyondTime, Bgm.P3, init: world => world.Map.AddEffect(0x00010001, 2));

    // The arena as Divine Judgment leaves it: the floor's map effects played as the transition plays
    // them, and the arena pieces lit.
    public static readonly Phase P4 = new(Instance, "P4", Weather.FairSkiesP4, Bgm.P4, init: world =>
    {
        world.Map.AddEffect(0x00080004, 2);
        world.Map.AddEffect(0x00020001, 5);
        world.Map.AddEffect(0x00020001, 3);
        world.Map.AddEffect(0x00020001, 4);
        Instance.SpawnArenaFeatures(world);
    });

    private static readonly (uint LayoutId, uint GimmickId)[] ArenaFeatureLayouts =
    [
        (ArenaFeatureLayoutId.NorthEastJail, 0x7C5C3E), (ArenaFeatureLayoutId.EastJail, 0x7C5C3F),
        (ArenaFeatureLayoutId.SouthEastJail, 0x7C5C40), (ArenaFeatureLayoutId.SouthJail, 0x7C5C41),
        (ArenaFeatureLayoutId.SouthWestJail, 0x7C5C42), (ArenaFeatureLayoutId.WestJail, 0x7C5C43),
        (ArenaFeatureLayoutId.NorthWestJail, 0x7C5C44), (ArenaFeatureLayoutId.WholeArena, 0x7C5C45),
    ];

    // A beat only lands once the piece's SharedGroup has attached, about 1s after the spawn.
    private const float ArenaFeatureBeatAt = 1f;

    private readonly SimEventObject?[] arenaFeatures = new SimEventObject?[8];

    public string Name => "The Epic of Alexander";
    public uint TerritoryId => 887;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => TeaConstants.Level;
    public ushort ItemLevel => TeaConstants.ItemLevel;

    // unsure if this delay is accurate for every level sync or duty, reason it's for tea is because FFlogs
    // showed the median of people pressing the tank lb which when pressed here would result in a wipe due
    // to there not being the correct delay
    public StatusLanding? TankLimitBreakLanding => new StatusLanding(1.27f, 0.145f);

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
    }

    // 0-6 the jails in landing order, 7 the whole arena's.
    public SimEventObject? ArenaFeature(int index) => arenaFeatures[index];

    private void SpawnArenaFeatures(SimWorld world)
    {
        for (var i = 0; i < ArenaFeatureLayouts.Length; i++)
            arenaFeatures[i] = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.ArenaFeature,
                Placement = new Placement(Vector3.Zero, 0f),
                TargetableStatus = 5,
                LayoutId = ArenaFeatureLayouts[i].LayoutId,
                GimmickId = ArenaFeatureLayouts[i].GimmickId,
            });
        world.Events.Add(ArenaFeatureBeatAt, () =>
        {
            foreach (var feature in arenaFeatures) feature?.PlayBeat(1, 2);
        });
    }
}
