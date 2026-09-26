using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// A zone load that throws once the firewall is up (a scenario with a wrong territory, a sheet row
// the client doesn't have) must neither leave the firewall up for good nor lower it unverified.
public class ZoneLoadFailureTests : FirewallTestBase
{
    private void StartAndSettle(SimZone zone)
    {
        Plugin.ClickStart(zone);
        Game.RunFor(4);
    }

    [Fact]
    public void TerritoryWithoutADutyEntry_IsBackedOutAndVerified()
    {
        StartAndSettle(SimZone.Top with { TerritoryId = VirtualGame.TerritoryWithoutDutyEntry });
        Assert.False(Plugin.ScenarioActive);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void TerritoryMissingFromTheSheet_IsBackedOutAndVerified()
    {
        StartAndSettle(SimZone.Top with { TerritoryId = VirtualGame.UnknownTerritory });
        Assert.False(Plugin.ScenarioActive);
        AssertBackInTheInnWithTheFirewallDown();
    }

    [Fact]
    public void ThrowAfterTheNativeLoad_RevertsTheInnBeforeLifting()
    {
        Game.ThrowAfterNativeLoad = true;
        StartAndSettle(SimZone.Top);
        Assert.False(Plugin.ScenarioActive);
        Assert.Contains(Game.NativeLoads, l => l.Territory == VirtualGame.DutyTerritory);
        AssertBackInTheInnWithTheFirewallDown();
        Assert.True(Vector3.Distance(Game.ClientPosition, VirtualGame.InnPosition) < 0.01f);
    }

    [Fact]
    public void ThrowAfterTheNativeLoad_AndTheInnReloadThrowsToo_StopsTheGame()
    {
        Game.ThrowAfterNativeLoad = true;
        Game.BeforeNativeLoad = territory =>
        {
            if (VirtualGame.IsInn(territory)) throw new System.InvalidOperationException("injected inn reload failure");
        };
        StartAndSettle(SimZone.Top);
        Game.RunFor(3);
        AssertStoppedWithTheFirewallUp("inn reload did not complete");
    }

    [Fact]
    public void AfterABackedOutStart_TheNextStartWorks()
    {
        StartAndSettle(SimZone.Top with { TerritoryId = VirtualGame.TerritoryWithoutDutyEntry });
        Game.RunFor(3.5);
        Enter(SimZone.Top);
        Leave();
        AssertBackInTheInnWithTheFirewallDown();
    }
}
