using AnoMech.Core.Game;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

public class MovementSpeedTests
{
    private const ushort Sprint = 50, Heavy = 1796, VulnerabilityUp = 444;

    private SimEnemy walker = null!;

    [SetUp]
    public void SpawnWalker()
    {
        FakeGame.Install();
        walker = new SimWorld(new EventScheduler()).SpawnEnemy(new EnemySpawnConfig(8722u))!;
    }

    [Test]
    public void HeavyTakesItsParamOffARun()
        => Assert.That(FactorWith((Heavy, 20)), Is.EqualTo(0.8f).Within(1e-5f));

    // Retail's speed bytes: a sprint of 78 runs at 66 under Heavy 20.
    [Test]
    public void SprintAndHeavySum()
        => Assert.That(FactorWith((Sprint, 30), (Heavy, 20)), Is.EqualTo(110f / 130f).Within(1e-5f));

    // An order's speed already holds Sprint's.
    [Test]
    public void SprintAloneLeavesAnOrderAsIs()
        => Assert.That(FactorWith((Sprint, 30)), Is.EqualTo(1f));

    [Test]
    public void OtherStatusesLeaveIt()
        => Assert.That(FactorWith((VulnerabilityUp, 1)), Is.EqualTo(1f));

    private float FactorWith(params (ushort StatusId, int Param)[] statuses)
    {
        foreach (var (statusId, param) in statuses) walker.AddStatusParam(statusId, param);
        return MovementSpeed.Factor(walker.ActiveStatuses);
    }
}
