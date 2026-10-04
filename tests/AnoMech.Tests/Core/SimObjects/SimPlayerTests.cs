using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

public class SimPlayerTests
{
    private const ushort Jog = 4209;

    [Test]
    public void TheOutOfCombatJogBuffIsStrippedEveryTick()
    {
        var game = FakeGame.Install();
        var player = new SimPlayer(new Coordinates(() => Vector3.Zero));
        game.BattleCharas.Player.Statuses.Add(Jog);

        player.Tick(1f / 60f);

        Assert.That(game.BattleCharas.Player.Statuses, Does.Not.Contain(Jog));
    }
}
