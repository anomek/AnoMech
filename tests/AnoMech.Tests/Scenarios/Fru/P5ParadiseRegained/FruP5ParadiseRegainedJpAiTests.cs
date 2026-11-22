using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat, first tower south: Light first cleaves the west (Dark first the east), the DPS
// wait on the safe side, then the melee take the tower on the side just cleaved and the ranged the
// tower on their own side.
public class FruP5ParadiseRegainedJpAiTests
{
    private static readonly int Mana = new FruP5ParadiseRegainedScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP5ParadiseRegainedJpAi)
        .index;

    [TestCase(false, TestName = "Light first: melee north-west, ranged north-east")]
    [TestCase(true, TestName = "Dark first: melee north-east, ranged north-west")]
    public void MeleeTakeTheCleavedSidesTower(bool darkFirst)
    {
        var positions = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (FruP5ParadiseRegainedStateOverrides)o;
                overrides.Rotation = 0;
                overrides.EastSecond = true;
                overrides.DarkFirst = darkFirst;
            },
            Probe = p =>
            {
                if (!p.Crossed(FruP5ParadiseRegainedState.TowerHitAt(1) - 0.1f)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = member.Position;
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP5ParadiseRegainedScenario), Mana, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);

        var meleeTower = new Vector3(darkFirst ? 6.06f : -6.06f, 0, -3.5f);
        var rangedTower = meleeTower with { X = -meleeTower.X };
        Assert.Multiple(() =>
        {
            foreach (var role in new[] { MeleeDpsA, MeleeDpsB })
                Assert.That(Vector3.Distance(positions[role], meleeTower), Is.LessThan(3f), $"{role} at {positions[role]}");
            foreach (var role in new[] { PhysRangedDps, CasterDps })
                Assert.That(Vector3.Distance(positions[role], rangedTower), Is.LessThan(3f), $"{role} at {positions[role]}");
        });
    }
}
