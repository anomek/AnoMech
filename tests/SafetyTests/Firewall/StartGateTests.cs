using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Map;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.ClientState.Conditions;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// A start the server could still act on must never load the sim: the firewall would hide the
// outcome and the lift would report a client the server no longer has.
public class StartGateTests : FirewallTestBase
{
    private void AssertNothingLoaded()
    {
        Assert.DoesNotContain(Game.NativeLoads, l => l.Territory == VirtualGame.DutyTerritory);
        Assert.False(Plugin.ScenarioActive);
        Assert.False(Game.SendFilterUp);
        Assert.False(Game.ReceiveFilterUp);
        AssertClean();
    }

    // Clicks Start and lets the deferred start run; returns the refusal if there was one.
    private string? TryStart()
    {
        var refusal = Plugin.ClickStart(SimZone.Top);
        Game.RunFor(0.2);
        return refusal ?? Plugin.StartWaitingOn ?? Plugin.LastRefusal;
    }

    [Fact]
    public void NotLoggedIn_Refused()
    {
        Game.LogOut();
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void NoLocalPlayer_Refused()
    {
        Game.LocalPlayerPresent = false;
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void OutsideAnInn_Refused()
    {
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "walked out");
        Game.RunFor(10);
        Assert.Equal(VirtualGame.CityTerritory, Game.LoadedTerritory);
        Assert.Contains("inn", TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void WhileZoning_Refused()
    {
        Game.SetCondition(ConditionFlag.BetweenAreas, true);
        Assert.NotNull(TryStart());
        Game.SetCondition(ConditionFlag.BetweenAreas, false);
        AssertNothingLoaded();
    }

    [Fact]
    public void JustAfterAZoneIn_WaitsForTheZoneToSettle()
    {
        Game.ServerMovesCharacter(VirtualGame.OtherInnTerritory, VirtualGame.InnPosition, "entered another inn");
        Game.RunFor(VirtualGame.ClientZoneLoadSeconds + 0.1);
        Assert.Equal(VirtualGame.OtherInnTerritory, Game.LoadedTerritory);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(1);
        Assert.False(Plugin.ScenarioActive);
        Assert.NotNull(Plugin.StartWaitingOn);
        Game.RunFor(3);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        AssertClean();
    }

    // The cast bar alone must block: a condition can lag the bar it belongs to.
    [Fact]
    public void CastBarWithoutTheCastingCondition_Refused()
    {
        Game.ShowCastBarOnly(7, 0.3f, 2.5f);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void WhileCastingAnything_Refused()
    {
        Game.ShowCastBegunEarlier(7531, 0.2f, 1f);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    // 2026-09-19: Teleport cast, Start pressed mid-cast: the sim started and the character was
    // teleported away underneath it.
    [Fact]
    public void TeleportMidCast_Refused_UntilTheZoneChangeHasSettled()
    {
        Game.PressZoneChangeAction();
        Game.RunFor(1);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
        Game.RunFor(VirtualGame.TeleportCastSeconds);
        Assert.Equal(VirtualGame.CityTerritory, Game.LoadedTerritory);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    // Under lag the zone change lands seconds after the cast bar ends; a second press the game
    // refused must not release the first cast's hold.
    [Theory]
    [InlineData(0.5)]
    [InlineData(4)]
    [InlineData(12)]
    public void TeleportCompletedButItsZoneChangeLagging_Refused(double latency)
    {
        Game.ServerLatencySeconds = latency;
        Game.PressZoneChangeAction(VirtualGame.ReturnActionId);
        Game.RunFor(1);
        Game.PressZoneChangeAction(VirtualGame.TeleportActionId);
        Game.RunFor(VirtualGame.TeleportCastSeconds - 1 + latency - 0.2);
        Assert.True(Game.ZoneChangeInFlight, "The zone change should still be in flight.");
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void Return_IsTreatedLikeTeleport()
    {
        Game.PressZoneChangeAction(VirtualGame.ReturnActionId);
        Game.RunFor(0.5);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void TeleportPressed_ClickedInTheSameFrame_Refused()
    {
        Game.PressZoneChangeAction();
        Assert.NotNull(Plugin.ClickStart(SimZone.Top) ?? Plugin.StartWaitingOn);
        Game.RunFor(0.3);
        AssertNothingLoaded();
    }

    // The gate is asked again when the deferred start runs; a Teleport pressed after the click
    // but before the framework task must still stop it.
    [Fact]
    public void TeleportPressedBetweenTheClickAndTheDeferredStart_Refused()
    {
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.PressZoneChangeAction();
        Game.RunFor(0.3);
        AssertNothingLoaded();
    }

    [Fact]
    public void TeleportInterruptedEarly_ReleasesTheGate()
    {
        Game.PressZoneChangeAction();
        Game.RunFor(1);
        Game.InterruptCast(new Vector3(0.5f, 0, 0));
        Game.RunFor(0.5);
        Assert.False(Game.ServerHasPendingZoneChange);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(4);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        AssertClean();
    }

    // Interrupted this late, the server may already have completed the cast.
    [Fact]
    public void TeleportInterruptedNearCompletion_KeepsTheGateShut()
    {
        Game.PressZoneChangeAction();
        Game.RunFor(VirtualGame.TeleportCastSeconds * 0.95);
        Game.InterruptCast(new Vector3(0.5f, 0, 0));
        Game.RunFor(4);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
    }

    [Fact]
    public void TeleportRefusedByTheServer_ReleasesTheGate()
    {
        Game.ServerAcceptsTeleport = false;
        Game.PressZoneChangeAction();
        Game.RunFor(0.2);
        Assert.NotNull(TryStart());
        Game.RunFor(4);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(4);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        AssertClean();
    }

    public static IEnumerable<object[]> BusyFlags() => StartSpecification.BusyFlags.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(BusyFlags))]
    public void WhileBusy_Refused(ConditionFlag flag)
    {
        Game.SetCondition(flag, true);
        Game.RunFor(0.1);
        Assert.NotNull(TryStart());
        Assert.DoesNotContain(Game.NativeLoads, l => l.Territory == VirtualGame.DutyTerritory);
        Assert.False(Plugin.ScenarioActive);
        Assert.False(Game.SendFilterUp);
        AssertClean();
    }

    public static IEnumerable<object[]> ServerActingFlags() => StartSpecification.ServerActingSoonFlags.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(ServerActingFlags))]
    public void RightAfterAServerActingState_WaitsForItToSettle(ConditionFlag flag)
    {
        Game.SetCondition(flag, true);
        Game.RunFor(0.5);
        Game.SetCondition(flag, false);
        Game.RunFor(0.5);
        Plugin.ClickStart(SimZone.Top);
        Game.RunFor(1);
        Assert.False(Plugin.ScenarioActive, $"Started {1:F1}s after {flag} cleared.\n{Game.Report()}");
        Game.RunFor(3);
        AssertClean();
    }

    // The previous stay's delayed lift is still pending; a stay started under it would lose its
    // firewall when that lift ran.
    [Fact]
    public void StartDuringThePreviousStaysPendingLift_WaitsForTheLift()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Assert.True(ZoneSessionProbe.GuardArmed(Plugin.Zone));
        Plugin.ClickStart(SimZone.Umad);
        Game.RunFor(0.3);
        Assert.False(Plugin.ScenarioActive, "Started while the previous lift was pending.");
        Game.RunFor(6);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        Assert.True(Game.SendFilterUp);
        Game.RunFor(3);
        Assert.True(Game.SendFilterUp, "The new stay lost its firewall.");
        AssertClean();
    }

    // Dalamud reports a zone change as its load begins; the settle must still count from the
    // load's end. A start deferred on it runs on the first frame the gate opens.
    [Theory]
    [InlineData(VirtualGame.OtherInnTerritory)]
    [InlineData(VirtualGame.InnTerritory)]
    public void AStartRightAfterAZoneIn_WaitsTheWholeSettleFromTheLoadsEnd(uint territory)
    {
        Game.ServerMovesCharacter(territory, VirtualGame.InnPosition, "test");
        Game.RunFor(VirtualGame.ClientZoneLoadSeconds + 0.1);
        Assert.False(Game.ClientZoning);
        Assert.Null(Plugin.ClickStart(SimZone.Top));
        Game.RunFor(4);
        Assert.True(Plugin.ScenarioActive, Game.Report());
        var load = Game.NativeLoads.First(l => !VirtualGame.IsInn(l.Territory));
        Assert.True(load.At - Game.LastZoneInAt >= StartSpecification.ZoneSettleSeconds, $"Loaded {load.At - Game.LastZoneInAt:F3}s after the zone-in.");
        AssertClean();
    }

    [Fact]
    public void SendFilterFailsToArm_EnterRefusesAndLoadsNothing()
    {
        Game.RefuseToEnableHook(HookRole.Send);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
        Assert.Contains(Game.DiagnosticLines, l => l.Contains("send filter did not arm"));
    }

    [Fact]
    public void ReceiveFilterFailsToArm_EnterRefusesAndLoadsNothing()
    {
        Game.RefuseToEnableHook(HookRole.Receive);
        Assert.NotNull(TryStart());
        AssertNothingLoaded();
        Assert.Contains(Game.DiagnosticLines, l => l.Contains("receive filter did not arm"));
    }

    [Fact]
    public void MissingSendSignature_PluginFailsToLoad()
    {
        Plugin.Dispose();
        Game.SendSignatureMissing = true;
        Assert.ThrowsAny<System.Exception>(() => new MapController());
        Assert.False(Game.SendFilterUp);
    }

    [Fact]
    public void GateAgreesWithTheSpecification_WhenIdle()
    {
        Assert.Null(ZoneSession.StartBlockedReason());
        Assert.Null(StartSpecification.Violation(Game.StartSpecNow(), false));
    }
}
