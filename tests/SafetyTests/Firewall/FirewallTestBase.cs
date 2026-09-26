using System;
using System.Linq;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

public abstract class FirewallTestBase : IDisposable
{
    protected readonly VirtualGame Game;
    protected readonly PluginDriver Plugin;

    protected FirewallTestBase(bool dalamudSyncsTerritory = false)
    {
        Game = new VirtualGame(dalamudSyncsTerritory);
        Plugin = new PluginDriver(Game);
    }

    public void Dispose()
    {
        try
        {
            Plugin.Dispose();
        }
        catch (Exception)
        {
        }
        finally
        {
            Game.Dispose();
        }
    }

    protected void Enter(SimZone? zone = null)
    {
        var refusal = Plugin.ClickStart(zone ?? SimZone.Top);
        Assert.True(refusal == null, $"Start refused: {refusal}\n{Game.Report()}");
        Game.RunFor(0.25);
        Assert.True(Plugin.ScenarioActive, $"The scenario did not start ({Plugin.LastRefusal}).\n{Game.Report()}");
        Assert.True(Game.SendFilterUp && Game.ReceiveFilterUp, $"The firewall is not up in the sim.\n{Game.Report()}");
    }

    protected void Leave(double runSeconds = 3)
    {
        Plugin.ClickLeave();
        Game.RunFor(runSeconds);
    }

    protected void AssertClean()
    {
        Assert.True(Game.Violations.Count == 0, $"Safety violations:\n{Game.Report()}");
        Assert.False(Game.Dead, $"The game was stopped: {Game.DeathReason}\n{Game.Report()}");
    }

    protected void AssertBackInTheInnWithTheFirewallDown()
    {
        AssertClean();
        Assert.False(Game.SendFilterUp, $"The send filter is still up.\n{Game.Report()}");
        Assert.False(Game.ReceiveFilterUp, $"The receive filter is still up.\n{Game.Report()}");
        Assert.Equal(VirtualGame.InnTerritory, Game.LoadedTerritory);
        Assert.True(Game.Divergence().Count == 0, $"The client still diverges: {string.Join("; ", Game.Divergence())}\n{Game.Report()}");
        Assert.False(Game.PluginHoldsOccupied, "The player was left Occupied.");
        Assert.False(Plugin.Map.IsInInstance);
        Assert.False(Plugin.Zone.IsActive);
        Assert.False(ZoneSessionProbe.GuardArmed(Plugin.Zone));
    }

    // Stopped by the safety stop for the expected reason, with nothing unsafe before it.
    protected void AssertStoppedWithTheFirewallUp(string reasonFragment)
    {
        Assert.True(Game.Dead, $"The game was not stopped.\n{Game.Report()}");
        Assert.True(Game.DeathReason!.Contains(reasonFragment, StringComparison.OrdinalIgnoreCase),
            $"Stopped for '{Game.DeathReason}', expected '{reasonFragment}'.\n{Game.Report()}");
        Assert.True(Game.Violations.Count == 0, $"Safety violations before the stop:\n{Game.Report()}");
        Assert.True(Game.SendFilterUp, $"The send filter was lowered before the stop.\n{Game.Report()}");
    }

    protected void AssertNothingLeaked()
    {
        var leaked = Game.ServerReceived.Where(p => p.Opcode != VirtualGame.HeartbeatOpcode && p.ClientTerritory != VirtualGame.InnTerritory).ToList();
        Assert.True(leaked.Count == 0, $"Packets reached the server from the sim: {string.Join(", ", leaked)}\n{Game.Report()}");
    }
}
