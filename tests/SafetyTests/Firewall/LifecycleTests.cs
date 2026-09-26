using System.Linq;
using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// The ordinary stay: enter from the inn, run, leave, and the firewall comes down only once the
// client is verifiably back where the server has it.
public class LifecycleTests : FirewallTestBase
{
    [Fact]
    public void Enter_ArmsBothFiltersBeforeTheSimZoneLoads()
    {
        Enter();
        Assert.Equal(VirtualGame.DutyTerritory, Game.LoadedTerritory);
        Assert.Equal(VirtualGame.InnTerritory, Game.ServerTerritory);
        var enabledAt = Game.HookHistory.FindIndex(h => h.Contains("Send on"));
        Assert.True(enabledAt >= 0);
        Assert.Contains(Game.NativeLoads, l => l.Territory == VirtualGame.DutyTerritory);
        AssertClean();
    }

    [Fact]
    public void Stay_HoldsEverythingButTheHeartbeat()
    {
        Enter();
        var before = Game.ServerReceived.Count;
        for (var i = 0; i < 50; i++)
        {
            Game.Walk(new Vector3(0.2f, 0, 0));
            Game.ClientSends(VirtualGame.ChatOpcode, "chat");
            Game.ClientSends(VirtualGame.ActionRequestOpcode, "UseAction something");
            Game.Frame();
        }
        Game.RunFor(12);
        var arrived = Game.ServerReceived.Skip(before).ToList();
        Assert.All(arrived, p => Assert.Equal(VirtualGame.HeartbeatOpcode, p.Opcode));
        Assert.True(arrived.Count >= 2, "The heartbeat must keep flowing during a stay.");
        AssertClean();
    }

    [Fact]
    public void Leave_LiftsOnlyAfterTheInnReloadAndPositionCheck()
    {
        Enter();
        Game.RunFor(5);
        Plugin.ClickLeave();
        Game.Frame();
        Assert.Equal(VirtualGame.InnTerritory, Game.LoadedTerritory);
        Assert.True(Game.SendFilterUp, "The filter must stay up through the reload.");
        Game.RunFor(0.9);
        Assert.True(Game.SendFilterUp, "Lifted before the one-second settle after the reload.");
        Game.RunFor(2);
        AssertBackInTheInnWithTheFirewallDown();
        Assert.Equal(1, Game.SendHookDisableCount);
    }

    [Fact]
    public void Leave_AfterTheSimMovedThePlayerAround_RestoresTheInnPosition()
    {
        Enter();
        Plugin.SimMovesPlayer(new Vector3(100, 0, 80));
        Game.RunFor(1);
        Plugin.SimMovesPlayer(new Vector3(135, 0, 100));
        Game.Walk(new Vector3(3, 0, 3));
        Game.RunFor(1);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
        Assert.True(Vector3.Distance(Game.ClientPosition, VirtualGame.InnPosition) < 0.01f);
    }

    [Fact]
    public void Leave_PlayerWalksAwayDuringTheSettle_IsPutBackBeforeTheLift()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        for (var i = 0; i < 40; i++)
        {
            Game.Walk(new Vector3(0.1f, 0, 0.1f));
            Game.Frame();
        }
        Game.RunFor(3);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restart_InsideTheLoadedZone_KeepsTheStayArmed(bool outsideArena)
    {
        Enter();
        if (outsideArena) Plugin.SimMovesPlayer(new Vector3(200, 0, 200));
        Game.RunFor(4);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(0.5);
        Assert.Single(Game.NativeLoads);
        Assert.True(Plugin.ScenarioActive);
        Assert.True(Game.SendFilterUp);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void Reset_ThenLeave_StillLiftsCleanly()
    {
        Enter();
        Plugin.SimMovesPlayer(new Vector3(300, 0, 300));
        Plugin.ClickReset();
        Game.RunFor(1);
        Assert.False(Plugin.ScenarioActive);
        Assert.True(Game.SendFilterUp);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void RepeatedStays_NeverLiftEarlyNorStick(int stays)
    {
        for (var i = 0; i < stays; i++)
        {
            Enter(i % 2 == 0 ? SimZone.Top : SimZone.Umad);
            Game.RunFor(2);
            Leave(4);
            AssertBackInTheInnWithTheFirewallDown();
            Game.RunFor(3.1);
        }
        Assert.Equal(stays, ZoneSessionProbe.StayId(Plugin.Zone));
    }

    [Theory]
    [InlineData(1f / 30f, true)]
    [InlineData(1f / 144f, true)]
    [InlineData(1f / 20f, false)]
    [InlineData(0.25f, false)]
    public void FrameRateAndTaskOrder_DoNotMatter(float dt, bool tasksBeforeUpdate)
    {
        Game.FrameworkTasksBeforeUpdate = tasksBeforeUpdate;
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(1, dt);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        Game.RunFor(3, dt);
        Plugin.ClickLeave();
        Game.RunFor(4, dt);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void TheSimLevelSyncAndDutyDirector_AreGoneBeforeTheLift()
    {
        Enter();
        Assert.Equal((ushort)90, Game.SyncedLevel);
        Assert.NotNull(Game.DirectorContent);
        Leave();
        Assert.Equal((ushort)0, Game.SyncedLevel);
        Assert.Null(Game.DirectorContent);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void WeatherWrites_OutsideAStay_AreIgnored()
    {
        Enter();
        Plugin.Map.SetWeather(9);
        Assert.Equal(9, Game.ActiveWeather);
        Leave();
        Plugin.Map.SetWeather(4);
        Game.RunFor(4);
        Assert.Equal(9, Game.ActiveWeather);
        AssertBackInTheInnWithTheFirewallDown();
    }
}

// The same stays against a Dalamud whose territory reading the zone load can overwrite.
public class LifecycleWithSyncedTerritoryTests : FirewallTestBase
{
    public LifecycleWithSyncedTerritoryTests() : base(dalamudSyncsTerritory: true)
    {
    }

    [Fact]
    public void EnterAndLeave()
    {
        Enter();
        Assert.Equal(VirtualGame.DutyTerritory, Game.DalamudTerritory);
        Game.RunFor(5);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
        Assert.Equal(VirtualGame.InnTerritory, Game.DalamudTerritory);
    }
}
