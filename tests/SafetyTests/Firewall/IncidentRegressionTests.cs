using System.Linq;
using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// One test per problem found in development, named for what went wrong.
public class IncidentRegressionTests : FirewallTestBase
{
    // "Leave session" without ever entering a sim ran the revert: its delayed restore moved the
    // character to a stale (or zero) position in the real world with no firewall at all.
    [Fact]
    public void LeaveWithoutASim_TouchesNothing()
    {
        Game.RunFor(1);
        Plugin.Map.Unload();
        Game.RunFor(4);
        AssertClean();
        Assert.Empty(Game.NativePositionWrites);
        Assert.Empty(Game.HookHistory);
        Assert.False(Game.PluginHoldsOccupied);
    }

    [Fact]
    public void LeaveWithoutASim_AfterAnEarlierRunAndAWalk_TouchesNothing()
    {
        Enter();
        Leave();
        var writes = Game.NativePositionWrites.Count;
        var hooks = Game.HookHistory.Count;
        for (var i = 0; i < 100; i++)
        {
            Game.Walk(new Vector3(0.5f, 0, 0));
            Game.Frame();
        }
        Plugin.Map.Unload();
        Game.RunFor(4);
        AssertClean();
        Assert.Equal(writes, Game.NativePositionWrites.Count);
        Assert.Equal(hooks, Game.HookHistory.Count);
    }

    // The first multiplayer run entered twice in a row; the first stay's delayed lift then ran
    // inside the second, moved the character to the inn spot inside the arena and dropped the
    // second stay's firewall. The start gate now refuses that overlap; this forces it past the
    // gate to check the second line, the stay id.
    [Fact]
    public unsafe void AStaysDelayedLift_NeverActsInsideTheNextStay()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        ForceOpenTheStartGate();
        Assert.Null(Plugin.ClickStart(SimZone.Umad));
        Game.Frame();
        Game.Frame();
        Assert.True(Plugin.ScenarioActive, Game.Report());
        Assert.Equal(2, ZoneSessionProbe.StayId(Plugin.Zone));
        var writes = Game.NativePositionWrites.Count;
        Game.RunFor(5);
        Assert.False(Game.Dead, Game.Report());
        Assert.True(Game.SendFilterUp, "A previous stay's lift dropped this stay's firewall.");
        Assert.Equal(writes, Game.NativePositionWrites.Count);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }

    private unsafe void ForceOpenTheStartGate()
    {
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        var type = typeof(AnoMech.Core.Map.ZoneSession);
        type.GetField("guardArmed", any)!.SetValue(Plugin.Zone, false);
        type.GetField("lastBusyAt", any)!.SetValue(null, null);
        ((FFXIVClientStructs.FFXIV.Client.Game.Conditions*)Game.ConditionsMemory)->Occupied = false;
    }

    // The fixed one-second settle can end before the inn has finished loading; the game's
    // post-load packet would then leave with nothing holding it.
    [Theory]
    [InlineData(1.05)]
    [InlineData(1.6)]
    [InlineData(2.5)]
    public void SlowInnLoad_ThePostLoadPacketIsHeld(double innLoadSeconds)
    {
        Game.InnLoadSeconds = innLoadSeconds;
        Enter();
        Game.RunFor(3);
        Leave(6);
        AssertBackInTheInnWithTheFirewallDown();
        Assert.DoesNotContain(Game.ServerReceived, p => p.Opcode == VirtualGame.PostLoadOpcode);
    }

    [Fact]
    public void InnLoadNeverFinishing_StopsTheGame()
    {
        Game.InnLoadSeconds = 60;
        Enter();
        Leave(6);
        AssertStoppedWithTheFirewallUp("loading");
    }

    // An unload has no frames left to wait for the inn load in: the post-load packet follows the
    // lift. Documented in the README as an accepted exposure.
    [Fact]
    public void UnloadMidStay_PostLoadPacketIsTheOnlyExposure()
    {
        Enter();
        Game.RunFor(2);
        Plugin.Unload();
        Game.RunFor(3);
        Assert.Empty(Game.Violations);
        Assert.Single(Game.AcceptedRisks);
        Assert.Contains("zone-load-finished", Game.AcceptedRisks[0]);
    }

    // A throw between the revert and the lift used to strand the stay: the firewall up for good
    // and every later start refused.
    [Fact]
    public void ThrowInTheDelayedRestore_TheStayStillEnds()
    {
        Enter();
        Game.ThrowOnActorControl = true;
        Leave(6);
        Assert.True(Game.Dead || !Game.SendFilterUp, Game.Report());
        Assert.Empty(Game.Violations);
        Assert.False(ZoneSessionProbe.GuardArmed(Plugin.Zone));
    }

    [Fact]
    public void ThrowRestoringThePosition_TheStayStillEnds()
    {
        Enter();
        Plugin.ClickLeave();
        Game.Frame();
        Game.ThrowOnPositionWrite = true;
        Game.RunFor(1.5);
        Game.ThrowOnPositionWrite = false;
        Game.RunFor(6);
        Assert.True(Game.Dead || !Game.SendFilterUp, Game.Report());
        Assert.Empty(Game.Violations);
        Assert.False(ZoneSessionProbe.GuardArmed(Plugin.Zone));
    }

    // The debug bench's send-only hold, still counted as held after a stay's lift took the hook
    // down, refused to hold again.
    [Fact]
    public void DebugHold_AfterAStay_HoldsAgain()
    {
        Plugin.Map.HoldSendFirewall(true);
        typeof(AnoMech.Core.Map.ZoneSession).GetMethod("DisableFirewall")!.Invoke(Plugin.Zone, null);
        Plugin.Map.HoldSendFirewall(true);
        Assert.True(Game.SendFilterUp, "The hold believed it was on while the send filter was down.");
        Plugin.Map.HoldSendFirewall(false);
        Enter();
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
        Plugin.Map.HoldSendFirewall(true);
        Assert.True(Game.SendFilterUp);
        Plugin.Map.HoldSendFirewall(false);
        AssertClean();
    }

    // The local player vanishing mid-cast (a redraw) looked like the cast being interrupted; under
    // lag the zone change then landed after a start.
    [Fact]
    public void TeleportCastUnobservedDuringARedraw_KeepsTheGateShut()
    {
        Game.ServerLatencySeconds = 7.5;
        Game.PressZoneChangeAction();
        Game.RunFor(0.5);
        Game.LocalPlayerPresent = false;
        Game.RunFor(6.5);
        Game.LocalPlayerPresent = true;
        while (Game.ZoneChangeInFlight)
        {
            Plugin.ClickStart(SimZone.Umad);
            Game.RunFor(0.5);
        }
        Assert.DoesNotContain(Game.NativeLoads, l => l.Territory == VirtualGame.OtherDutyTerritory);
        AssertClean();
    }

    // Replayed map effects or director updates after a Leave would run the native state changes in
    // the real inn.
    [Fact]
    public void MapEffectsAndDirectorUpdates_OutsideASim_AreIgnored()
    {
        Enter();
        Leave();
        var effects = Game.NativeMapEffects;
        var updates = Game.NativeDirectorUpdates;
        Plugin.Map.AddEffect(0x00020001, 3);
        Plugin.Map.DirectorUpdate(0x80000004, 1);
        Plugin.Map.SuppressArenaSlot(4);
        Game.RunFor(2);
        Assert.Equal(effects, Game.NativeMapEffects);
        Assert.Equal(updates, Game.NativeDirectorUpdates);
        AssertClean();
    }

    // A real zone change outside any sim: the game's own post-load packet is expected.
    [Fact]
    public void OrdinaryZoneChange_IsNotMistakenForALeak()
    {
        Game.ServerMovesCharacter(VirtualGame.CityTerritory, VirtualGame.CityAetherytePosition, "walked out of the inn");
        Game.RunFor(3);
        Assert.Contains(Game.ServerReceived, p => p.Opcode == VirtualGame.PostLoadOpcode);
        AssertClean();
    }
}
