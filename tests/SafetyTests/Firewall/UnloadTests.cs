using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// Disabling, reloading or updating the plugin: Dalamud disposes the plugin's hooks right after
// Dispose, so the zone session must have restored and verified the client by then.
public class UnloadTests : FirewallTestBase
{
    [Fact]
    public void UnloadWhileIdle_LeavesNothingBehind()
    {
        Game.RunFor(1);
        Plugin.Unload();
        Game.RunFor(2);
        AssertClean();
        Assert.False(Game.SendFilterUp);
    }

    [Fact]
    public void UnloadMidStay_RestoresTheInnBeforeTheHooksGo()
    {
        Enter();
        Plugin.SimMovesPlayer(new Vector3(95, 0, 120));
        Game.RunFor(2);
        Plugin.Unload();
        Game.RunFor(3);
        AssertClean();
        Assert.Equal(VirtualGame.InnTerritory, Game.LoadedTerritory);
        Assert.Empty(Game.Divergence());
        Assert.False(Game.PluginHoldsOccupied);
    }

    [Fact]
    public void UnloadMidStay_UnverifiableRestore_StopsTheGame()
    {
        Enter();
        Game.IgnorePositionWrites = true;
        Plugin.SimMovesPlayer(new Vector3(95, 0, 120));
        Game.IgnorePositionWrites = false;
        Game.RunFor(1);
        Game.IgnorePositionWrites = true;
        Plugin.Unload(dalamudSweepsHooks: false);
        Assert.True(Game.Dead, Game.Report());
        Assert.True(Game.SendFilterUp);
        Assert.Empty(Game.Violations);
    }

    [Fact]
    public void UnloadDuringThePendingLift_VerifiesBeforeLifting()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.Walk(new Vector3(1.5f, 0, 0));
        Game.RunFor(0.3);
        Plugin.Unload();
        Game.RunFor(3);
        AssertClean();
        Assert.Empty(Game.Divergence());
        Assert.False(Game.PluginHoldsOccupied, "The unload left the player Occupied.");
    }

    // The delayed re-assert queued by Leave outlives an unload that lifted the firewall itself;
    // run afterwards, it snaps the character back with nothing to hold the move.
    [Fact]
    public void UnloadDuringThePendingLift_TheStaleDelayedRestoreDoesNotMoveThePlayer()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.RunFor(0.2);
        Plugin.Unload();
        for (var i = 0; i < 60; i++)
        {
            Game.Walk(new Vector3(0.08f, 0, 0));
            Game.Frame();
        }
        Game.RunFor(3);
        AssertClean();
    }

    [Fact]
    public void UnloadMidStay_ScenarioAfflictionsAreCleared()
    {
        Enter();
        Game.SetPluginStatusAffliction();
        Plugin.Unload();
        Game.RunFor(2);
        Assert.False(Game.PluginSetStatusAffliction, "A status affliction the sim set survived the unload.");
    }

    [Fact]
    public void LeaveThenUnloadLater_Clean()
    {
        Enter();
        Leave();
        Plugin.Unload();
        Game.RunFor(2);
        AssertClean();
    }
}
