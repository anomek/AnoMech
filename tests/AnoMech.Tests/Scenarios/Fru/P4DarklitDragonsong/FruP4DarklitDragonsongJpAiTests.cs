using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat, the guide's example: H1 OT D1 D2 chained, so the free D3 stands west and D4
// east between the towers, the free healer H2 north-west and the free tank MT north-east. Both
// Waters are north (H1 chained, H2 free), so H2 swaps with their partner D3 before the towers.
public class FruP4DarklitDragonsongJpAiTests
{
    private const float Seated = 27.5f;
    private const float Swapped = 32.1f;

    private static readonly int Mana = new FruP4DarklitDragonsongScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP4DarklitDragonsongJpAi)
        .index;

    [Test]
    public void FreePlayersSeatWestToEastAndTheFreeWaterSwapsWithItsPartner()
    {
        var seated = new Dictionary<PartyRole, Vector3>();
        var swapped = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (FruP4DarklitDragonsongStateOverrides)o;
                overrides.ChainedTank = OffTank;
                overrides.ChainedHealer = RegenHealer;
                overrides.DpsOrder = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                overrides.Shape = 0;
                overrides.TetherWaterIndex = 0;
                overrides.FreeWaterIndex = 0;
                overrides.EastWing = true;
                overrides.SpiritTakerTarget = CasterDps;
            },
            Probe = p =>
            {
                if (p.Crossed(Seated)) Capture(p, seated);
                if (p.Crossed(Swapped)) Capture(p, swapped);
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP4DarklitDragonsongScenario), Mana, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);

        Assert.Multiple(() =>
        {
            ExpectQuadrant(seated, ShieldHealer, west: true, north: true);
            ExpectQuadrant(seated, MainTank, west: false, north: true);
            ExpectQuadrant(seated, PhysRangedDps, west: true, north: false);
            ExpectQuadrant(seated, CasterDps, west: false, north: false);
            ExpectQuadrant(swapped, ShieldHealer, west: true, north: false);
            ExpectQuadrant(swapped, PhysRangedDps, west: true, north: true);
        });
    }

    private static void Capture(ScenarioProbe probe, Dictionary<PartyRole, Vector3> into)
    {
        foreach (var role in PerRole.All)
            if (probe.Member(role) is { } member)
                into[role] = member.Position;
    }

    private static void ExpectQuadrant(Dictionary<PartyRole, Vector3> positions, PartyRole role, bool west, bool north)
    {
        var p = positions[role];
        Assert.That(p.X < 0, Is.EqualTo(west), $"{role} at ({p.X:F1}, {p.Z:F1}) should be {(west ? "west" : "east")}");
        Assert.That(p.Z < 0, Is.EqualTo(north), $"{role} at ({p.X:F1}, {p.Z:F1}) should be {(north ? "north" : "south")}");
    }
}
