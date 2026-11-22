using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P2LightRampant;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat counts the six tethered players clockwise round its ring from H1, and the n-th
// takes the n-th tower: NW, S, NE, SW, N, SE. The scenario chains the loop in that ring order, so
// every link stays long once the curse sets in.
public class FruP2LightRampantJpAiTests
{
    private const float TowersHeld = 18.7f;
    private const float OnTower = 3f;

    private static readonly int Mana = new FruP2LightRampantScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP2LightRampantJpAi)
        .index;

    private static readonly PartyRole[] ClockwiseFromH1 =
        [RegenHealer, ShieldHealer, MainTank, OffTank, CasterDps, PhysRangedDps, MeleeDpsB, MeleeDpsA];

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void TheNthTetheredPlayerFromH1TakesTheNthTower(int seed)
    {
        var positions = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Probe = p =>
            {
                if (!p.Crossed(TowersHeld)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = member.Position;
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP2LightRampantScenario), Mana, seed, options);
        Assert.That(run.Passed, Is.True, run.ToString);

        var tethered = ClockwiseFromH1
            .Where(role => Enumerable.Range(0, 6).Any(t => Flat(positions[role] - FruP2LightRampantState.Tower(t)) < OnTower))
            .ToList();
        Assert.That(tethered, Has.Count.EqualTo(6), "six players should be standing in towers");
        Assert.Multiple(() =>
        {
            for (var n = 0; n < tethered.Count; n++)
                Assert.That(Flat(positions[tethered[n]] - FruP2LightRampantState.Tower(n)), Is.LessThan(OnTower),
                    $"{tethered[n]} is tethered player {n + 1} from H1");
        });
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();
}
