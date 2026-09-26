using System.Numerics;
using AnoMech.Core.Map;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.ClientState.Conditions;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// Anything the server does to the character during a stay leaves the client and server
// disagreeing; the guard must stop the game with the firewall still up rather than lift it.
public class StayGuardTests : FirewallTestBase
{
    [Fact]
    public void LogoutDuringTheStay_StopsTheGame()
    {
        Enter();
        Game.RunFor(2);
        Game.LogOut();
        Game.RunFor(1);
        AssertStoppedWithTheFirewallUp("logout");
    }

    [Fact]
    public void LogoutDuringThePendingLift_StopsTheGame()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.LogOut();
        Game.RunFor(3);
        AssertStoppedWithTheFirewallUp("logout");
    }

    [Theory]
    [InlineData(ConditionFlag.BetweenAreas)]
    [InlineData(ConditionFlag.BetweenAreas51)]
    [InlineData(ConditionFlag.LoggingOut)]
    public void ZoneTransitionOrLogoutStartingDuringTheStay_StopsTheGame(ConditionFlag flag)
    {
        Enter();
        Game.RunFor(1);
        Game.SetCondition(flag, true);
        Game.Frame();
        AssertStoppedWithTheFirewallUp("while the firewall was up");
    }

    // A zone change the client processed for real (an allowed inbound opcode, a server move that
    // got through): Dalamud's territory moves off both the inn and the sim.
    [Fact]
    public void RealZoneChangeDuringTheStay_StopsTheGame()
    {
        Game.Config.ZoneDownOpcodes = [VirtualGame.KeepAliveInboundOpcode, VirtualGame.InitZoneInboundOpcode];
        Enter();
        Game.RunFor(1);
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "a duty commenced");
        Game.RunFor(3);
        AssertStoppedWithTheFirewallUp("while the firewall was up");
    }

    [Fact]
    public void TeleportCastBegunBeforeTheStay_StopsTheGame()
    {
        Enter();
        Game.ShowCastBegunEarlier(VirtualGame.TeleportActionId, elapsed: 2f);
        Game.Frame();
        AssertStoppedWithTheFirewallUp("Teleport cast begun before the firewall went up");
    }

    [Fact]
    public void TeleportPressedDuringTheStay_IsHeldAndHarmless()
    {
        Enter();
        Game.RunFor(1);
        Game.PressZoneChangeAction();
        Game.RunFor(VirtualGame.TeleportCastSeconds + 1);
        Assert.False(Game.ServerHasPendingZoneChange);
        Assert.Equal(VirtualGame.InnTerritory, Game.ServerTerritory);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Theory]
    [InlineData(HookRole.Send)]
    [InlineData(HookRole.Receive)]
    public void FilterTakenDownByAnythingElse_StopsTheGameInTheSameFrame(HookRole role)
    {
        Enter();
        Game.RunFor(1);
        var packets = Game.ServerReceived.Count;
        Game.DisableHookExternally(role);
        Game.Walk(new Vector3(5, 0, 0));
        Game.Frame();
        Assert.True(Game.Dead, Game.Report());
        Assert.Contains("disabled", Game.DeathReason);
        Assert.Equal(packets, Game.ServerReceived.Count);
    }

    [Fact]
    public void FilterTakenDownDuringThePendingLift_StopsTheGame()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.DisableHookExternally(HookRole.Receive);
        Game.RunFor(2);
        Assert.True(Game.Dead, Game.Report());
    }

    [Fact]
    public void LocalPlayerBrieflyMissingDuringTheStay_IsNotAStop()
    {
        Enter();
        Game.LocalPlayerPresent = false;
        Game.RunFor(2);
        Game.LocalPlayerPresent = true;
        Game.RunFor(2);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void GameMainTerritoryReadingZero_IsNotAStop()
    {
        Enter();
        Game.RunFor(40);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void TrippedStay_LeaveStopsTheGameInsteadOfReloadingTheInn()
    {
        Enter();
        Game.LogOut();
        Game.Frame();
        Assert.True(Game.Dead);
        Assert.Equal(VirtualGame.DutyTerritory, Game.LoadedTerritory);
    }

    // Today a trip stops the game on the spot; should it ever not, a Leave must still stop rather
    // than show the client an inn the server may not have it in.
    [Fact]
    public void LeaveAfterATrip_StopsInsteadOfReloadingTheInn()
    {
        Enter();
        typeof(ZoneSession).GetField("tripReason", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(Plugin.Zone, "test trip");
        Plugin.ClickLeave();
        Game.RunFor(2);
        Assert.True(Game.Dead, Game.Report());
        Assert.Equal(VirtualGame.DutyTerritory, Game.LoadedTerritory);
        Assert.True(Game.SendFilterUp);
    }

    [Fact]
    public void ARestartAfterATrip_IsRefused()
    {
        Enter();
        var reason = typeof(ZoneSession).GetField("tripReason", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        reason.SetValue(Plugin.Zone, "test trip");
        Assert.NotNull(ZoneSession.StartBlockedReason());
    }
}
