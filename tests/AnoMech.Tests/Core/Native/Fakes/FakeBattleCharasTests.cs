using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

public class FakeBattleCharasTests
{
    [SetUp]
    public void InstallGame() => FakeGame.Install();

    // The radii the game itself resolved for these spawns.
    [TestCase(8168u, 2112u, 4.2f)]
    [TestCase(8722u, 0u, 1.7f)]
    [TestCase(8723u, 0u, 1.36f)]
    [TestCase(8725u, 0u, 0.5f)]
    [TestCase(8727u, 0u, 4.55f)]
    [TestCase(8728u, 0u, 1.3f)]
    [TestCase(8729u, 0u, 1.8f)]
    [TestCase(8730u, 0u, 5f)]
    [TestCase(8731u, 0u, 1f)]
    [TestCase(8734u, 0u, 6f)]
    [TestCase(8735u, 0u, 1f)]
    [TestCase(14669u, 0u, 12.502f)]
    [TestCase(15709u, 0u, 1.5f)]
    [TestCase(15712u, 0u, 5.01f)]
    [TestCase(15713u, 0u, 5.01f)]
    [TestCase(15716u, 0u, 0.5f)]
    [TestCase(15718u, 0u, 1.68f)]
    [TestCase(15723u, 0u, 6.72f)]
    [TestCase(15724u, 0u, 12.006f)]
    [TestCase(18475u, 0u, 6f)]
    [TestCase(19506u, 0u, 6.02f)]
    [TestCase(19507u, 0u, 6f)]
    [TestCase(19509u, 0u, 3.8f)]
    [TestCase(19510u, 0u, 9f)]
    [TestCase(19511u, 0u, 8.01f)]
    [TestCase(19513u, 0u, 3.5f)]
    public void SpawnedEnemyHasTheGamesHitboxRadius(uint bnpcBaseId, uint modelCharaId, float expected)
    {
        var enemy = Natives.BattleCharas.SpawnBattleNpcFromPacket(new EnemySpawnConfig(bnpcBaseId, ModelCharaId: modelCharaId), new Placement(), out _);

        Assert.That(enemy?.HitboxRadius, Is.EqualTo(expected).Within(1e-4f));
    }
}
