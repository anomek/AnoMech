using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat. Spread is P1's clock (MT N, D4 NE, H2 E, D2 SE, ST S, D1 SW, H1 W, D3 NW), all
// turned an eighth clockwise when the stones would land on the first circles. MT H1 D1 D3 knock back
// into the first circle between W and NE, the rest into its opposite, and each party then walks the
// rim for Sinbound Holy by where the Mirror stands relative to it.
public class FruP2DiamondDustJpAiTests
{
    private const float KickArrival = 20.65f;
    private const float PreIce = 39.85f;
    private const float OctantTolerance = 0.15f;

    private static readonly int Mana = new FruP2DiamondDustScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP2DiamondDustJpAi)
        .index;

    private static readonly PartyRole[] NorthParty = [MainTank, RegenHealer, MeleeDpsA, PhysRangedDps];
    private static readonly PartyRole[] SouthParty = [OffTank, ShieldHealer, MeleeDpsB, CasterDps];

    [Test]
    public void BotsStartOnThePhaseOneSpreadBeforeAnyTellsAppear()
    {
        var start = Capture(7f, o => o.SupportsMarked = true);
        ExpectOctants(start, new()
        {
            [MainTank] = 0, [CasterDps] = 1, [ShieldHealer] = 2, [MeleeDpsB] = 3,
            [OffTank] = 4, [MeleeDpsA] = 5, [RegenHealer] = 6, [PhysRangedDps] = 7,
        });
        Assert.Multiple(() =>
        {
            foreach (var (role, p) in start)
                Assert.That(new Vector2(p.X, p.Z).Length(), Is.EqualTo(8f).Within(0.5f), $"{role} radius");
        });
    }

    [Test]
    public void DpsStonesWithCardinalCirclesKeepTheSpread()
    {
        var spread = Capture(KickArrival, o => o.SupportsMarked = false);
        ExpectOctants(spread, new()
        {
            [MainTank] = 0, [CasterDps] = 1, [ShieldHealer] = 2, [MeleeDpsB] = 3,
            [OffTank] = 4, [MeleeDpsA] = 5, [RegenHealer] = 6, [PhysRangedDps] = 7,
        });
    }

    [Test]
    public void SupportStonesWithCardinalCirclesTurnTheSpreadClockwise()
    {
        var spread = Capture(KickArrival, o => o.SupportsMarked = true);
        ExpectOctants(spread, new()
        {
            [PhysRangedDps] = 0, [MainTank] = 1, [CasterDps] = 2, [ShieldHealer] = 3,
            [MeleeDpsB] = 4, [OffTank] = 5, [MeleeDpsA] = 6, [RegenHealer] = 7,
        });
    }

    [TestCase(2, 2, 6, TestName = "Mirror 90° from both: both walk 90° clockwise")]
    [TestCase(7, 3, 7, TestName = "Mirror 45° anticlockwise: both walk 135° clockwise")]
    [TestCase(1, 6, 6, TestName = "Mirror 45° clockwise: both meet 135° from it")]
    [TestCase(0, 3, 7, TestName = "Mirror on a party: both walk 135° clockwise")]
    public void SinboundHolyEndsWhereTheGuideSays(int mirror, int northEnds, int southEnds)
    {
        var preIce = Capture(PreIce, o => o.MirrorOctant = mirror);
        ExpectOctants(preIce, NorthParty.ToDictionary(r => r, _ => northEnds));
        ExpectOctants(preIce, SouthParty.ToDictionary(r => r, _ => southEnds));
    }

    private static Dictionary<PartyRole, Vector3> Capture(float at, Action<FruP2DiamondDustStateOverrides> tweak)
    {
        var positions = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (FruP2DiamondDustStateOverrides)o;
                overrides.Axe = true;
                overrides.Stillness = true;
                overrides.FirstIcicle = 0;
                overrides.SupportsMarked = true;
                overrides.MirrorOctant = 2;
                overrides.GazeOctant = 0;
                tweak(overrides);
            },
            Probe = p =>
            {
                if (!p.Crossed(at)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = member.Position;
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustScenario), Mana, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        return positions;
    }

    private static void ExpectOctants(Dictionary<PartyRole, Vector3> positions, Dictionary<PartyRole, int> expected)
    {
        Assert.Multiple(() =>
        {
            foreach (var (role, octant) in expected)
            {
                var p = positions[role];
                var actual = MathF.Atan2(p.X, -p.Z) / (MathF.PI / 4);
                var off = ((actual - octant) % 8 + 12) % 8 - 4;
                Assert.That(MathF.Abs(off), Is.LessThan(OctantTolerance), $"{role} at octant {actual:F2}, expected {octant}");
            }
        });
    }
}
