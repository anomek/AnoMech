using AnoMech.Integrations.Splatoon;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Umad;

namespace AnoMech.Tests;

public class SplatoonCompatTests
{
    [Test]
    public void PhaseSceneIsUsedWhenNoOverride()
    {
        var phase = new Phase(TopZone.Instance, "Test", null, 0, envScene: 6);
        Assert.That(SplatoonCompat.ResolveScene(phase, SplatoonCompat.SceneOverrideOff), Is.EqualTo((byte)6));
    }

    [Test]
    public void PhaseWithoutASceneIsLeftUntouched()
    {
        var phase = new Phase(TopZone.Instance, "Test", null, 0);
        Assert.That(SplatoonCompat.ResolveScene(phase, SplatoonCompat.SceneOverrideOff), Is.Null);
    }

    [TestCase(0, (byte)0)]
    [TestCase(9, (byte)9)]
    [TestCase(255, (byte)255)]
    public void OverrideWinsOverThePhaseScene(int sceneOverride, byte expected)
    {
        var phase = new Phase(TopZone.Instance, "Test", null, 0, envScene: 6);
        Assert.That(SplatoonCompat.ResolveScene(phase, sceneOverride), Is.EqualTo(expected));
    }

    [TestCase(-5)]
    [TestCase(256)]
    public void OutOfRangeOverrideFallsBackToThePhase(int sceneOverride)
    {
        var phase = new Phase(TopZone.Instance, "Test", null, 0, envScene: 6);
        Assert.That(SplatoonCompat.ResolveScene(phase, sceneOverride), Is.EqualTo((byte)6));
    }

    // Splatoon's official scripts gate on these scene ids for the matching phases.
    [Test]
    public void KnownPhasesCarryTheScenesSplatoonScriptsExpect()
    {
        Assert.That(TopZone.P5.EnvScene, Is.EqualTo((byte)6));
        Assert.That(TopZone.P6.EnvScene, Is.EqualTo((byte)7));
        Assert.That(UmadZone.P2.EnvScene, Is.EqualTo((byte)7));
        Assert.That(UmadZone.P3.EnvScene, Is.EqualTo((byte)8));
    }
}
