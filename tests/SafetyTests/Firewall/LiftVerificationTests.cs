using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.ClientState.Conditions;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// "The packet filter should never be lifted unless the things can be verified, and if they can't
// after 3 seconds killing the game sounds reasonable."
public class LiftVerificationTests : FirewallTestBase
{
    [Fact]
    public void PositionRestoreDoesNotTake_StopsTheGameWithinThreeSeconds()
    {
        Enter();
        Plugin.SimMovesPlayer(new Vector3(100, 0, 130));
        Game.RunFor(1);
        Game.IgnorePositionWrites = true;
        Plugin.ClickLeave();
        Game.RunFor(1.2);
        Assert.False(Game.Dead, "Stopped before the three-second verification window.");
        Assert.True(Game.SendFilterUp);
        Game.RunFor(3.5);
        AssertStoppedWithTheFirewallUp("from where the inn left them");
    }

    [Fact]
    public void PositionRestoreTakesLate_LiftsOnceVerified()
    {
        Enter();
        Game.IgnorePositionWrites = true;
        Plugin.ClickLeave();
        Game.RunFor(1.5);
        Assert.True(Game.SendFilterUp);
        Game.IgnorePositionWrites = false;
        Game.ClientPosition = VirtualGame.InnPosition;
        Game.RunFor(2);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void NoLocalPlayerAtTheLift_WaitsThenStops()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.LocalPlayerPresent = false;
        Game.RunFor(2);
        Assert.False(Game.Dead, "A missing player must be waited for, not treated as fatal at once.");
        Assert.True(Game.SendFilterUp);
        Game.RunFor(3);
        AssertStoppedWithTheFirewallUp("no local player");
    }

    [Fact]
    public void NoLocalPlayerAtTheLift_ComingBackInPlace_Lifts()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.LocalPlayerPresent = false;
        Game.RunFor(1.5);
        Game.LocalPlayerPresent = true;
        Game.RunFor(2);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Theory]
    [InlineData(ConditionFlag.BetweenAreas)]
    [InlineData(ConditionFlag.LoggingOut)]
    public void ZoneTransitionAtTheLift_NeverLifts(ConditionFlag flag)
    {
        Enter();
        Plugin.ClickLeave();
        Game.RunFor(0.5);
        Game.SetCondition(flag, true);
        Game.RunFor(5);
        Assert.True(Game.Dead, Game.Report());
        Assert.True(Game.SendFilterUp);
        Assert.Empty(Game.Violations);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NonFinitePositionAtTheLift_NeverLifts(float bad)
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.IgnorePositionWrites = true;
        Game.ClientPosition = new Vector3(bad, 0, 0);
        Game.RunFor(5);
        Assert.True(Game.Dead, Game.Report());
        Assert.True(Game.SendFilterUp);
    }

    [Fact]
    public void RealZoneInDuringThePendingLift_StopsTheGame()
    {
        Game.Config.ZoneDownOpcodes = [VirtualGame.KeepAliveInboundOpcode, VirtualGame.InitZoneInboundOpcode];
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "moved during the settle");
        Game.RunFor(4);
        Assert.True(Game.Dead, Game.Report());
    }

    // An inn reload that throws leaves the client showing the sim zone. The lift must not treat a
    // re-asserted position as proof the client is back in the inn.
    [Fact]
    public void InnReloadThrows_TheSimZoneIsNeverReportedToTheServer()
    {
        Enter();
        Game.RunFor(1);
        Game.LocalPlayerPresent = false;
        Plugin.ClickLeave();
        Game.Frame();
        Game.LocalPlayerPresent = true;
        Game.RunFor(5);
        Assert.True(Game.Violations.Count == 0, Game.Report());
        Assert.True(Game.Dead || Game.LoadedTerritory == VirtualGame.InnTerritory, Game.Report());
    }

    [Fact]
    public void Lift_HappensExactlyOnce()
    {
        Enter();
        Leave(6);
        Assert.Equal(1, Game.SendHookDisableCount);
        AssertBackInTheInnWithTheFirewallDown();
    }
}
