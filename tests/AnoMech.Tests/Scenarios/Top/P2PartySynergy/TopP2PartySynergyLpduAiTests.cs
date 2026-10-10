using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P2PartySynergy;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.P2PartySynergy.PlaystationSymbol;

namespace AnoMech.Tests;

// Light parties MT RH M1 R (west) and OT SH M2 C (east), both new norths pinned to N so the AI's
// coordinates are its own, with the eye north. Every run deals the LPDU toolbox example: the west
// party holds both Circles, so R crosses east; the east party holds both Triangles, so M2 crosses
// west.
public class TopP2PartySynergyLpduAiTests
{
    private const float SpreadCheck = 22.5f;
    private const float StackCheck = 33.2f;
    private const float Tolerance = 1f;

    private static readonly int Lpdu = new TopP2PartySynergyScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is TopP2PartySynergyLpduAi)
        .index;

    private static readonly Dictionary<PartyRole, PlaystationSymbol> ToolboxExample = new()
    {
        [MainTank] = Cross,
        [OffTank] = Square,
        [RegenHealer] = Circle,
        [ShieldHealer] = Triangle,
        [MeleeDpsA] = Square,
        [MeleeDpsB] = Triangle,
        [PhysRangedDps] = Circle,
        [CasterDps] = Cross,
    };

    private sealed record Positions(
        IReadOnlyDictionary<PartyRole, Vector2> Spread, IReadOnlyDictionary<PartyRole, Vector2> Stacks);

    [Test]
    public void MidGlitchSpreadsAsInTheToolbox()
    {
        var positions = Deal(GlitchType.Mid, stacks: [MainTank, ShieldHealer]);
        ExpectSpread(positions, GlitchType.Mid,
            westFromEye: [MainTank, MeleeDpsA, RegenHealer, MeleeDpsB],
            eastFromEye: [CasterDps, OffTank, PhysRangedDps, ShieldHealer]);
        ExpectStacks(positions, GlitchType.Mid,
            west: [MainTank, MeleeDpsA, RegenHealer, MeleeDpsB],
            other: [CasterDps, OffTank, PhysRangedDps, ShieldHealer]);
    }

    [Test]
    public void FarGlitchFullyCrossesTheEastParty()
    {
        var positions = Deal(GlitchType.Far, stacks: [MainTank, ShieldHealer]);
        ExpectSpread(positions, GlitchType.Far,
            westFromEye: [MainTank, MeleeDpsA, RegenHealer, MeleeDpsB],
            eastFromEye: [ShieldHealer, PhysRangedDps, OffTank, CasterDps]);
        ExpectStacks(positions, GlitchType.Far,
            west: [MainTank, MeleeDpsA, RegenHealer, MeleeDpsB],
            other: [ShieldHealer, PhysRangedDps, OffTank, CasterDps]);
    }

    // Both stacks west; M2 in the Triangle row is the south-most and swaps with SH, its partner.
    [Test]
    public void SameSideStacksOnMidGlitchSwapTheSouthmostWithItsPartner()
    {
        var positions = Deal(GlitchType.Mid, stacks: [MainTank, MeleeDpsB]);
        ExpectStacks(positions, GlitchType.Mid,
            west: [MainTank, MeleeDpsA, RegenHealer, ShieldHealer],
            other: [CasterDps, OffTank, PhysRangedDps, MeleeDpsB]);
    }

    // Both stacks west in the inner rows; RH (Circle) is the south-most and its partner R stands
    // in the east Square row after the full cross.
    [Test]
    public void SameSideStacksOnFarGlitchFindTheMirroredInnerRowPartner()
    {
        var positions = Deal(GlitchType.Far, stacks: [MeleeDpsA, RegenHealer]);
        ExpectStacks(positions, GlitchType.Far,
            west: [MainTank, MeleeDpsA, PhysRangedDps, MeleeDpsB],
            other: [ShieldHealer, RegenHealer, OffTank, CasterDps]);
    }

    // Both stacks east; OT (Square) stands in the Circle row after the full cross, south of SH in
    // the Cross row, and swaps with M1 in the west Square row.
    [Test]
    public void SameSideStacksOnFarGlitchReadTheSouthmostFromTheCrossedRows()
    {
        var positions = Deal(GlitchType.Far, stacks: [OffTank, ShieldHealer]);
        ExpectStacks(positions, GlitchType.Far,
            west: [MainTank, OffTank, RegenHealer, MeleeDpsB],
            other: [ShieldHealer, PhysRangedDps, MeleeDpsA, CasterDps]);
    }

    private static Positions Deal(GlitchType glitch, PartyRole[] stacks)
    {
        var spread = new Dictionary<PartyRole, Vector2>();
        var stacked = new Dictionary<PartyRole, Vector2>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o =>
            {
                var overrides = (TopP2PartySynergyStateOverrides)o;
                overrides.NewNorthA = Direction.N;
                overrides.NewNorthB = Direction.N;
                overrides.Glitch = glitch;
                foreach (var (role, symbol) in ToolboxExample) overrides.Symbol[role] = symbol;
                foreach (var role in stacks) overrides.Stack[role] = true;
            },
            Probe = p =>
            {
                if (p.Crossed(SpreadCheck)) Capture(p, spread);
                if (p.Crossed(StackCheck)) Capture(p, stacked);
            },
        };
        var run = ScenarioRun.Execute(typeof(TopP2PartySynergyScenario), Lpdu, seed: 1, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        return new Positions(spread, stacked);
    }

    private static void Capture(ScenarioProbe probe, Dictionary<PartyRole, Vector2> into)
    {
        foreach (var role in PerRole.All)
            if (probe.Member(role) is { } member)
                into[role] = new Vector2(member.Position.X, member.Position.Z);
    }

    private static void ExpectSpread(Positions positions, GlitchType glitch, PartyRole[] westFromEye, PartyRole[] eastFromEye)
    {
        float[] rows = [-16, -5.5f, 5.5f, 16];
        for (var row = 0; row < rows.Length; row++)
        {
            var x = glitch == GlitchType.Far && row is 1 or 2 ? 18f : 11f;
            ExpectAt(positions.Spread, westFromEye[row], new(-x, rows[row]));
            ExpectAt(positions.Spread, eastFromEye[row], new(x, rows[row]));
        }
    }

    private static void ExpectStacks(Positions positions, GlitchType glitch, PartyRole[] west, PartyRole[] other)
    {
        var (westSpot, otherSpot) = glitch == GlitchType.Far
            ? (new Vector2(-19, 0), new Vector2(19, 0))
            : (new Vector2(-15, 0), new Vector2(0, 15));
        foreach (var role in west) ExpectAt(positions.Stacks, role, westSpot);
        foreach (var role in other) ExpectAt(positions.Stacks, role, otherSpot);
    }

    private static void ExpectAt(IReadOnlyDictionary<PartyRole, Vector2> positions, PartyRole role, Vector2 expected)
    {
        Assert.That(positions.ContainsKey(role), Is.True, $"{role} was not captured.");
        var actual = positions[role];
        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance),
            $"{role} stood at ({actual.X:F1},{actual.Y:F1}), expected ({expected.X:F1},{expected.Y:F1}).");
    }
}
