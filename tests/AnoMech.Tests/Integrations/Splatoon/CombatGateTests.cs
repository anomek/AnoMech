using AnoMech.Integrations.Splatoon;

namespace AnoMech.Tests;

public class CombatGateTests
{
    [Test]
    public void IdleGateLeavesTheConditionAlone()
    {
        var gate = new CombatGate();
        Assert.That(gate.Tick(), Is.Null);
        Assert.That(gate.Owns, Is.False);
    }

    [Test]
    public void BeginHoldsOutOfCombatForTheGapThenEngages()
    {
        var gate = new CombatGate();
        gate.Begin();

        for (var i = 0; i < CombatGate.GapFrames; i++)
        {
            Assert.That(gate.Tick(), Is.False, $"gap frame {i}");
            Assert.That(gate.Engaged, Is.False);
        }

        Assert.That(gate.Tick(), Is.True);
        Assert.That(gate.Engaged, Is.True);
        Assert.That(gate.Tick(), Is.True, "stays engaged and keeps re-asserting");
    }

    [Test]
    public void GateOwnsTheConditionsFromBeginOnward()
    {
        var gate = new CombatGate();
        gate.Begin();
        Assert.That(gate.Owns, Is.True);
    }

    [Test]
    public void EndReportsWhetherTheGateHeldTheConditions()
    {
        var gate = new CombatGate();
        Assert.That(gate.End(), Is.False, "nothing to clear when never begun");

        gate.Begin();
        Assert.That(gate.End(), Is.True, "clears even mid-gap");

        gate.Begin();
        RunUntilEngaged(gate);
        Assert.That(gate.End(), Is.True);
        Assert.That(gate.Owns, Is.False);
        Assert.That(gate.Tick(), Is.Null);
    }

    [Test]
    public void RestartOnTheSameFrameStillShowsAFallingEdge()
    {
        var gate = new CombatGate();
        gate.Begin();
        RunUntilEngaged(gate);

        gate.End();
        gate.Begin();

        Assert.That(gate.Engaged, Is.False);
        Assert.That(gate.Tick(), Is.False, "a restart must read out of combat before re-engaging");
    }

    private static void RunUntilEngaged(CombatGate gate)
    {
        for (var i = 0; i <= CombatGate.GapFrames; i++) gate.Tick();
        Assert.That(gate.Engaged, Is.True);
    }
}
