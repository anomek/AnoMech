using AnoMech.Core.Native.Implementations;

namespace AnoMech.Tests;

public class LiftWaitTests
{
    // Revert first checks the lift a second after the inn reload, then every frame.
    private const double FirstCheck = 1;
    private const double Frame = 1.0 / 60;

    // When the guard stops the game, or null once the filter comes down. The client reports the inn
    // loading until innLoadsFor after the reload; something else then blocks the lift until
    // blockedUntil.
    private static double? StopsAt(double innLoadsFor, double blockedUntil = 0)
    {
        for (var frame = 0; frame < 60 * 60; frame++)
        {
            var t = FirstCheck + frame * Frame;
            var innLoading = t < innLoadsFor;
            if (!innLoading && t >= blockedUntil) return null;
            if (!LiftWait.Waits(innLoading, t, t - FirstCheck)) return t;
        }
        Assert.Fail("The guard neither lifted nor stopped the game within a minute.");
        return null;
    }

    [TestCase(0.5)]
    [TestCase(2.7)]
    [TestCase(4.5)]
    [TestCase(9.5)]
    public void AnInnThatLoadsWithinTenSecondsOfTheReloadIsWaitedOut(double loadSeconds)
    {
        Assert.That(StopsAt(loadSeconds), Is.Null);
    }

    [Test]
    public void AnInnStillLoadingTenSecondsAfterTheReloadStopsTheGame()
    {
        Assert.That(StopsAt(innLoadsFor: 60), Is.EqualTo(10).Within(Frame));
    }

    [Test]
    public void AnythingElseBlockingStopsTheGameThreeSecondsAfterTheFirstCheck()
    {
        Assert.That(StopsAt(innLoadsFor: 0, blockedUntil: 60), Is.EqualTo(FirstCheck + 3).Within(Frame));
    }

    [Test]
    public void AnythingElseThatClearsWithinThreeSecondsLetsTheFilterDown()
    {
        Assert.That(StopsAt(innLoadsFor: 0, blockedUntil: FirstCheck + 2.5), Is.Null);
    }
}
