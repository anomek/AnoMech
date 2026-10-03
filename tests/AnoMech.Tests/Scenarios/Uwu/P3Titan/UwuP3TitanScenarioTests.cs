using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Uwu.P3Titan;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

public class UwuP3TitanScenarioTests
{
    [Test]
    public void StandingBesideTitanDiesToTheLandingGeocrush()
        => Negative<UwuP3TitanScenario>(MeleeDpsA)
            .TeleportAt(4.5f, to: new Vector2(0f, 3f))
            .ShouldKill(ActionId.GeocrushLanding, MeleeDpsA);

    [Test]
    public void StayingInTheMiddleDiesToTheJumpGeocrush()
        => Negative<UwuP3TitanScenario>(RegenHealer)
            .TeleportAt(34f, to: Vector2.Zero)
            .ShouldKill(ActionId.GeocrushJump, RegenHealer);

    [Test]
    public void TitanFacesTheCentreThroughUpheavalWhereverTheMainTankStands()
    {
        var facings = new System.Collections.Generic.List<float>();
        var towardTheCentre = 0f;
        var run = ScenarioRun.Execute(typeof(UwuP3TitanScenario), 0, 1234, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            WriteArtifactsOnFailure = false,
            Takeover = new PlayerTakeover(39f, new Vector2(7f, 2f)),
            Probe = probe =>
            {
                if (probe.Time < 40.1f || probe.Time > 48.3f) return;
                var titan = probe.World.Children.OfType<SimEnemy>().First(e => e.SpawnConfig.BNpcBaseId == BNpcBaseId.Titan);
                facings.Add(titan.Rotation);
                towardTheCentre = MathF.Atan2(-titan.Position.X, -titan.Position.Z);
            },
        });
        Assert.That(facings, Is.Not.Empty);
        Assert.That(facings.Max() - facings.Min(), Is.LessThan(1e-3f));
        Assert.That(facings[0], Is.EqualTo(towardTheCentre).Within(1e-3f));
    }
}
