using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat spreads for Spirit Taker with the second Tidal Light's origin as north: MT and
// ST far in its two corners, D3/D4 on its intercard markers, H1/H2 on the sides, D1/D2 on the far
// intercards. Akh Morn 2 is 7:1 with the ST solo, east of the party stack.
public class FruP4CrystallizeTimeJpAiTests
{
    private const float SpreadHeld = 48.25f;
    private const float AkhMornHeld = 68.8f;

    private static readonly int Mana = new FruP4CrystallizeTimeScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP4CrystallizeTimeJpAi)
        .index;

    [TestCase(CrystallizeCorner.NW, 0)]
    [TestCase(CrystallizeCorner.SE, 4)]
    public void SpiritTakerSpreadsAroundTheSecondLinesOrigin(CrystallizeCorner corner, int relativeNorth)
    {
        var (spread, _) = Run(corner);
        Assert.Multiple(() =>
        {
            ExpectAt(spread, MainTank, relativeNorth - 0.7f, 17.6f);
            ExpectAt(spread, OffTank, relativeNorth + 0.7f, 17.6f);
            ExpectAt(spread, PhysRangedDps, relativeNorth - 1, 10);
            ExpectAt(spread, CasterDps, relativeNorth + 1, 10);
            ExpectAt(spread, RegenHealer, relativeNorth - 2, 10);
            ExpectAt(spread, ShieldHealer, relativeNorth + 2, 10);
            ExpectAt(spread, MeleeDpsA, relativeNorth - 3, 10);
            ExpectAt(spread, MeleeDpsB, relativeNorth + 3, 10);
        });
    }

    [Test]
    public void TheOffTankSolosAkhMorn()
    {
        var (_, akhMorn) = Run(CrystallizeCorner.NW);
        Assert.Multiple(() =>
        {
            Assert.That(Vector3.Distance(akhMorn[OffTank], new Vector3(9.3f, 0, 0)), Is.LessThan(1f), $"OffTank at {akhMorn[OffTank]}");
            foreach (var role in PerRole.All.Where(r => r != OffTank))
                Assert.That(akhMorn[role].Length(), Is.LessThan(1f), $"{role} at {akhMorn[role]}");
        });
    }

    private static (Dictionary<PartyRole, Vector3> Spread, Dictionary<PartyRole, Vector3> AkhMorn) Run(CrystallizeCorner corner)
    {
        var spread = new Dictionary<PartyRole, Vector3>();
        var akhMorn = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (FruP4CrystallizeTimeStateOverrides)o;
                overrides.Corner = corner;
                overrides.SlowNorthwest = true;
            },
            Probe = p =>
            {
                if (p.Crossed(SpreadHeld)) Capture(p, spread);
                if (p.Crossed(AkhMornHeld)) Capture(p, akhMorn);
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeScenario), Mana, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        return (spread, akhMorn);
    }

    private static void Capture(ScenarioProbe probe, Dictionary<PartyRole, Vector3> into)
    {
        foreach (var role in PerRole.All)
            if (probe.Member(role) is { } member)
                into[role] = member.Position;
    }

    private static void ExpectAt(Dictionary<PartyRole, Vector3> positions, PartyRole role, float octant, float radius)
    {
        var p = positions[role];
        var actual = MathF.Atan2(p.X, -p.Z) / (MathF.PI / 4);
        var off = ((actual - octant) % 8 + 12) % 8 - 4;
        var r = new Vector2(p.X, p.Z).Length();
        Assert.That(MathF.Abs(off), Is.LessThan(0.1f), $"{role} at octant {actual:F2}, expected {octant}");
        Assert.That(r, Is.EqualTo(radius).Within(0.6f), $"{role} at radius {r:F1}");
    }
}
