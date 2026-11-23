using System.Numerics;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using static AnoMech.Scenarios.Umad.P1TeleTrouncing.TelePortentDirection;

namespace AnoMech.Tests;

public class UmadP1HinoTests
{
    [TestCase(167906810)]
    [TestCase(939637214)]
    [TestCase(1463017531)]
    [TestCase(1586316199)]
    [TestCase(1481105243)]
    [TestCase(499175993)]
    [TestCase(1478722863)]
    public void HinoClearsPlacementAndSleepTransition(int seed)
    {
        var run = ScenarioRun.Execute(typeof(UmadP1TeleTrouncingScenario), 1, seed);
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void DifferentArrowsFollowReferenceQuadrants()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Up, Left, true), Is.EqualTo(new Vector2(-6, -6)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Left, Up, false), Is.EqualTo(new Vector2(-6, -12)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Right, Up, true), Is.EqualTo(new Vector2(6, -6)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Up, Right, false), Is.EqualTo(new Vector2(12, -6)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Down, Right, true), Is.EqualTo(new Vector2(6, 6)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Right, Down, false), Is.EqualTo(new Vector2(6, 12)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Left, Down, true), Is.EqualTo(new Vector2(-6, 6)));
            Assert.That(UmadP1TeleTrouncingAi.HinoArrowSpot(Down, Left, false), Is.EqualTo(new Vector2(-12, 6)));
        });
    }
}
