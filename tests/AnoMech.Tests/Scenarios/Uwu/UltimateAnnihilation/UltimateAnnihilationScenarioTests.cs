using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Uwu.UltimateAnnihilation;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// With Ultima above 50%, another Eye + Radiant Plume round runs before Suppression.
public class UltimateAnnihilationScenarioTests
{
    [Test]
    public void LeavingTheEyeBeforeItResolvesDies()
        => Negative<UltimateAnnihilationScenario>(MeleeDpsA)
            .TeleportAt(65f, to: new Vector2(-10.5f, -10.5f))
            .ShouldKill(ActionId.EyeOfTheStorm, MeleeDpsA);

    [Test]
    public void StayingInTheStackDiesToARadiantPlume()
        => Negative<UltimateAnnihilationScenario>(CasterDps)
            .TeleportAt(66.6f, to: new Vector2(-6.5f, -6f))
            .ShouldKill(ActionId.RadiantPlumePuddle, CasterDps);

    [Test]
    public void StandingBehindTheMainTankDiesToDiffractiveLaser()
        => Negative<UltimateAnnihilationScenario>(RegenHealer)
            .TeleportAt(70.5f, to: new Vector2(0.5f, -10f))
            .ShouldKill(ActionId.DiffractiveLaser, RegenHealer);

    [TestCase(true, 60f, 70f)]
    [TestCase(false, 80f, 90f)]
    public void SuppressionComesSoonerWhenUltimaIsPushedBelowHalf(bool pushed, float endsAfter, float endsBefore)
    {
        var run = ScenarioRun.Execute(typeof(UltimateAnnihilationScenario), 0, 99, new ScenarioRunOptions
        {
            WriteArtifactsOnFailure = false,
            Overrides = o => ((UltimateAnnihilationStateOverrides)o).UltimaPushedBelowHalf = pushed,
        });
        Assert.That(run.Deaths, Is.Empty);
        Assert.That(run.Elapsed, Is.InRange(endsAfter, endsBefore));
    }

    [TestCase(IfritCorner.SouthEast, 13.7f)]
    [TestCase(IfritCorner.SouthWest, -13.7f)]
    public void IfritAndTitanTakeOppositeSouthCorners(IfritCorner corner, float ifritX)
    {
        var (ifrit, titan) = PrimalsAfterTheWarp(1234, corner);
        Assert.That(ifrit, Is.EqualTo(new Vector2(ifritX, 13.7f)));
        Assert.That(titan, Is.EqualTo(new Vector2(-ifritX, 13.7f)));
    }

    [Test]
    public void IfritLandsInEitherSouthCornerAcrossPulls()
    {
        var corners = Enumerable.Range(0, 12).Select(seed => PrimalsAfterTheWarp(seed, IfritCorner.Random).Ifrit.X).Distinct();
        Assert.That(corners, Is.EquivalentTo(new[] { 13.7f, -13.7f }));
    }

    [TestCase(IfritCorner.SouthWest, 13.7f)]
    [TestCase(IfritCorner.SouthEast, -13.7f)]
    public void TitansLandslideRunsFromHisCornerTowardTheCentre(IfritCorner corner, float titanX)
    {
        var lines = new System.Collections.Generic.List<(Vector2 From, float Rotation)>();
        ScenarioRun.Execute(typeof(UltimateAnnihilationScenario), 0, 1234, new ScenarioRunOptions
        {
            StopAt = 31f,
            WriteArtifactsOnFailure = false,
            Overrides = o => ((UltimateAnnihilationStateOverrides)o).IfritCorner = corner,
            Probe = probe =>
            {
                if (!probe.Crossed(30.5f)) return;
                lines.AddRange(probe.World.Children.OfType<SimEnemy>()
                    .Where(e => e.IsCasting && e.CastActionId == ActionId.LandslideLine)
                    .Select(e => (Flat(e.Position), e.Rotation)));
            },
        });

        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines.Select(l => l.From).Distinct(), Is.EquivalentTo(new[] { new Vector2(titanX, 13.7f) }));
        Assert.That(lines.Any(l => MathF.Abs(float.Ieee754Remainder(l.Rotation - MathF.Atan2(-titanX, -13.7f), MathF.Tau)) < 0.05f));
    }

    [TestCase(IfritCorner.SouthWest, 5f)]
    [TestCase(IfritCorner.SouthEast, -5f)]
    public void IfritDashesFromHisCornerAcrossTheCentreWhoeverHowlTurnedHimTo(IfritCorner corner, float x)
        => Negative<UltimateAnnihilationScenario>(CasterDps)
            .Overrides<UltimateAnnihilationStateOverrides>(o => o.IfritCorner = corner)
            .TeleportAt(30.5f, to: new Vector2(x, -5f))
            .ShouldKill(ActionId.CrimsonCyclone, CasterDps);

    private static (Vector2 Ifrit, Vector2 Titan) PrimalsAfterTheWarp(int seed, IfritCorner corner)
    {
        Vector2 ifrit = default, titan = default;
        ScenarioRun.Execute(typeof(UltimateAnnihilationScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = 13f,
            WriteArtifactsOnFailure = false,
            Overrides = o => ((UltimateAnnihilationStateOverrides)o).IfritCorner = corner,
            Probe = probe =>
            {
                if (!probe.Crossed(12.5f)) return;
                var enemies = probe.World.Children.OfType<SimEnemy>().ToList();
                ifrit = Flat(enemies.First(e => e.SpawnConfig.BNpcBaseId == BNpcBaseId.Ifrit).Position);
                titan = Flat(enemies.First(e => e.SpawnConfig.BNpcBaseId == BNpcBaseId.Titan).Position);
            },
        });
        return (ifrit, titan);
    }

    private static Vector2 Flat(Vector3 p) => new(MathF.Round(p.X, 1), MathF.Round(p.Z, 1));
}
