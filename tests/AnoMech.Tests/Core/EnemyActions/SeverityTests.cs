using AnoMech.Core.EnemyActions;

namespace AnoMech.Tests;

public class SeverityTests
{
    [Test]
    public void AShownAmountReplacesASurvivedHitsFlytext()
        => Assert.Multiple(() =>
        {
            Assert.That(Severity.Harmless.Showing(1).FlyTextAmount(kills: false), Is.EqualTo(1u));
            Assert.That(Severity.Harmless.FlyTextAmount(kills: false), Is.EqualTo(0u));
        });

    [Test]
    public void AKillingHitShowsTheKillNumberWhateverItsShownAmount()
        => Assert.That(Severity.Harmless.Showing(1).FlyTextAmount(kills: true), Is.EqualTo(120_000u));
}
