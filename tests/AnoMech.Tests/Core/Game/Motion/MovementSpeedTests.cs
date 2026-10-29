using AnoMech.Core.Game.Motion;

namespace AnoMech.Tests;

public class MovementSpeedTests
{
    private const ushort Sprint = 50, Heavy = 1796, VulnerabilityUp = 444;

    [SetUp]
    public void InstallGame() => FakeGame.Install();

    [Test]
    public void HeavyTakesItsParamOffARun()
        => Assert.That(MovementSpeed.Factor([(Heavy, 20)]), Is.EqualTo(0.8f).Within(1e-5f));

    // Retail's speed bytes: a sprint of 78 runs at 66 under Heavy 20.
    [Test]
    public void SprintAndHeavySum()
        => Assert.That(MovementSpeed.Factor([(Sprint, 30), (Heavy, 20)]), Is.EqualTo(110f / 130f).Within(1e-5f));

    // An order's speed already holds Sprint's.
    [Test]
    public void SprintAloneLeavesAnOrderAsIs()
        => Assert.That(MovementSpeed.Factor([(Sprint, 30)]), Is.EqualTo(1f));

    [Test]
    public void OtherStatusesLeaveIt()
        => Assert.That(MovementSpeed.Factor([(VulnerabilityUp, 1)]), Is.EqualTo(1f));
}
