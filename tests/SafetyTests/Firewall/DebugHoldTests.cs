using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.ClientState.Conditions;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// The debug menu's send-only hold must never touch a stay's firewall.
public class DebugHoldTests : FirewallTestBase
{
    [Fact]
    public void HoldOutsideAStay_HoldsOnlyTheSendSide()
    {
        Plugin.Map.HoldSendFirewall(true);
        Assert.True(Game.SendFilterUp);
        Assert.False(Game.ReceiveFilterUp);
        Game.ClientSends(VirtualGame.ChatOpcode, "chat");
        Assert.DoesNotContain(Game.ServerReceived, p => p.Opcode == VirtualGame.ChatOpcode);
        Game.RunFor(2);
        Plugin.Map.HoldSendFirewall(false);
        Assert.False(Game.SendFilterUp);
        AssertClean();
    }

    [Fact]
    public void HoldAndReleaseDuringAStay_AreIgnored()
    {
        Enter();
        Plugin.Map.HoldSendFirewall(true);
        Plugin.Map.HoldSendFirewall(false);
        Assert.True(Game.SendFilterUp);
        Game.RunFor(2);
        AssertClean();
    }

    // Nothing done during a hold reached the server, so a stay started under one would save (and
    // later restore) a position the server never saw.
    [Fact]
    public void StartDuringAHold_WaitsForTheRelease()
    {
        Plugin.Map.HoldSendFirewall(true);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(2);
        Assert.False(Plugin.ScenarioActive);
        Plugin.Map.HoldSendFirewall(false);
        Game.RunFor(1);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        AssertClean();
    }

    [Fact]
    public void WalkingDuringAHold_IsUndoneBeforeTheRelease()
    {
        Plugin.Map.HoldSendFirewall(true);
        for (var i = 0; i < 60; i++)
        {
            Game.Walk(new Vector3(0.15f, 0, 0));
            Game.Frame();
        }
        Plugin.Map.HoldSendFirewall(false);
        Game.RunFor(1);
        AssertClean();
    }

    [Fact]
    public void UnloadDuringAHold_PutsTheCharacterBackFirst()
    {
        Plugin.Map.HoldSendFirewall(true);
        for (var i = 0; i < 60; i++)
        {
            Game.Walk(new Vector3(0.15f, 0, 0));
            Game.Frame();
        }
        Plugin.Unload();
        Game.RunFor(1);
        AssertClean();
    }

    // The lift owns the send filter from the revert until it verifies; a hold cycled in that
    // second must not drop it early.
    [Fact]
    public void HoldCycledDuringThePendingLift_IsIgnored()
    {
        Game.InnLoadSeconds = 0.9;
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Plugin.Map.HoldSendFirewall(true);
        Plugin.Map.HoldSendFirewall(false);
        Assert.True(Game.SendFilterUp, "The debug hold dropped the filter the pending lift owns.");
        Game.RunFor(4);
        AssertBackInTheInnWithTheFirewallDown();
    }

    // The same with the settle's Occupied gone (the game may clear a flag it did not set), which
    // leaves the lift's ownership as the only thing keeping the hold off the filter.
    [Fact]
    public void HoldCycledDuringThePendingLift_WithOccupiedCleared_IsIgnored()
    {
        Game.InnLoadSeconds = 0.9;
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.SetCondition(ConditionFlag.Occupied, false);
        Plugin.Map.HoldSendFirewall(true);
        Plugin.Map.HoldSendFirewall(false);
        Assert.True(Game.SendFilterUp, "The debug hold dropped the filter the pending lift owns.");
        Game.RunFor(4);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void ReleaseDuringThePendingLift_IsIgnored()
    {
        Enter();
        Plugin.Map.HoldSendFirewall(true);
        Plugin.ClickLeave();
        Game.Frame();
        Plugin.Map.HoldSendFirewall(false);
        Assert.True(Game.SendFilterUp);
        Game.RunFor(3);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void HoldDuringAStay_ThenLeave_LiftsNormally()
    {
        Enter();
        Plugin.Map.HoldSendFirewall(true);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
        Plugin.Map.HoldSendFirewall(false);
        Game.RunFor(1);
        AssertClean();
    }

    // A hold under a Teleport cast would hide the move that interrupts it, and the server would
    // complete the cast; the hold refuses to start until the Teleport resolves.
    [Fact]
    public void HoldWhileATeleportIsPending_IsRefused()
    {
        Game.PressZoneChangeAction();
        Game.RunFor(0.5);
        Plugin.Map.HoldSendFirewall(true);
        Assert.False(Game.SendFilterUp);
        Assert.Contains(Game.DiagnosticLines, l => l.Contains("Debug send hold refused"));
        Game.InterruptCast(new Vector3(0.5f, 0, 0));
        Game.RunFor(0.5);
        Assert.False(Game.ServerHasPendingZoneChange, "The server should have heard the interruption.");
        AssertClean();
    }

    // Mid-load there is no position the server has settled on to hold the character to.
    [Fact]
    public void HoldDuringAZoneChange_IsRefused()
    {
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "test");
        Assert.True(Game.ClientZoning);
        Plugin.Map.HoldSendFirewall(true);
        Assert.False(Game.SendFilterUp);
        Game.RunFor(2);
        AssertClean();
    }

    // Dalamud reports the zone change as its load begins; the release must restore to where the
    // zone-in placed the character, not to where the hold began.
    [Theory]
    [InlineData(VirtualGame.CityTerritory)]
    [InlineData(VirtualGame.InnTerritory)]
    public void ZoneInDuringAHold_TheReleaseRestoresToWhereTheServerPlacedTheCharacter(uint territory)
    {
        var placed = territory == VirtualGame.CityTerritory ? VirtualGame.CityAetherytePosition : VirtualGame.InnPosition + new Vector3(4, 0, 0);
        Plugin.Map.HoldSendFirewall(true);
        Game.Walk(new Vector3(1, 0, 0));
        Game.RunFor(0.5);
        Game.ServerMovesCharacter(territory, placed, "test");
        Game.RunFor(VirtualGame.ClientZoneLoadSeconds + 0.5);
        for (var i = 0; i < 30; i++)
        {
            Game.Walk(new Vector3(0.2f, 0, 0));
            Game.Frame();
        }
        Plugin.Map.HoldSendFirewall(false);
        Game.RunFor(1);
        AssertClean();
    }

    [Fact]
    public void ZoneInWithNoCharacterDuringAHold_TheReleaseLeavesItWhereTheZoneInPutIt()
    {
        Plugin.Map.HoldSendFirewall(true);
        Game.Walk(new Vector3(1, 0, 0));
        Game.RunFor(0.5);
        Game.LocalPlayerPresent = false;
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "test");
        Game.RunFor(VirtualGame.ClientZoneLoadSeconds + 0.5);
        Game.LocalPlayerPresent = true;
        Plugin.Map.HoldSendFirewall(false);
        Game.RunFor(1);
        AssertClean();
    }
}
