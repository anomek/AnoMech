using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat's first worked example: lights N and S rolling anticlockwise, no swaps. T/H
// reference N turned 45° against the lights to NE, DPS S to SW. Z rank 0 takes the further near spot
// (E / W), rank 1 the near spot, ranks 2 and 3 the edge 12.5° clockwise and anticlockwise of it.
public class FruP3ApocalypseJpAiTests
{
    private static readonly int Mana = new FruP3ApocalypseScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP3ApocalypseJpAi)
        .index;

    [Test]
    public void SpreadsMatchTheGuidesFirstExample()
    {
        var spread = Capture(FruP3ApocalypseState.WaveAt(0) - 0.1f);
        Assert.Multiple(() =>
        {
            Expect(spread, MainTank, octant: 2, radius: 10);
            Expect(spread, OffTank, octant: 1, radius: 10);
            Expect(spread, RegenHealer, octant: 1.28f, radius: 19);
            Expect(spread, ShieldHealer, octant: 0.72f, radius: 19);
            Expect(spread, MeleeDpsA, octant: 6, radius: 10);
            Expect(spread, MeleeDpsB, octant: 5, radius: 10);
            Expect(spread, PhysRangedDps, octant: 5.28f, radius: 19);
            Expect(spread, CasterDps, octant: 4.72f, radius: 19);
        });
    }

    private static Dictionary<PartyRole, Vector3> Capture(float at)
    {
        var positions = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (FruP3ApocalypseStateOverrides)o;
                overrides.Rotation = 0;
                overrides.Clockwise = false;
                overrides.WaterDurations = [0, 1, 2, 3, 0, 1, 2, 3];
                overrides.SpiritTakerTarget = OffTank;
            },
            Probe = p =>
            {
                if (!p.Crossed(at)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = member.Position;
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), Mana, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        return positions;
    }

    private static void Expect(Dictionary<PartyRole, Vector3> positions, PartyRole role, float octant, float radius)
    {
        var p = positions[role];
        var actual = MathF.Atan2(p.X, -p.Z) / (MathF.PI / 4);
        var off = ((actual - octant) % 8 + 12) % 8 - 4;
        var r = new Vector2(p.X, p.Z).Length();
        Assert.That(MathF.Abs(off), Is.LessThan(0.08f), $"{role} at octant {actual:F2}, expected {octant}");
        Assert.That(r, Is.EqualTo(radius).Within(0.5f), $"{role} at radius {r:F1}");
    }
}
