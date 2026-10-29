using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using StatusId = AnoMech.Scenarios.Fru.FruConstants.StatusId;

namespace AnoMech.Tests;

// What the host assigns a peer's seat is what that peer's own client shows on its player: each
// status, and for a timer the time it has left.
public class PerSeatSettingsTests
{
    private const PartyRole Host = PartyRole.MainTank;
    private const PartyRole Peer = PartyRole.CasterDps;
    // Timers that tell picks apart are 10 s or more apart.
    private const float Slack = 2f;

    private static IEnumerable<TestCaseData> Cases()
    {
        yield return Case<FruP3UltimateRelativityScenario, FruP3UltimateRelativityStateOverrides>("Ultimate Relativity", "long fire", 13, o =>
            {
                o.DpsIce = false;
                o.Fire[Peer] = RelativityFire.Long;
            },
            (StatusId.DarkFireWaiting, FruP3UltimateRelativityState.FireAt(2) - 13), (StatusId.ShadoweyeWaiting, null));
        yield return Case<FruP3ApocalypseScenario, FruP3ApocalypseStateOverrides>("Apocalypse", "long Dark Water", 13.5, o => o.DarkWater[Peer] = ApocalypseWater.Long,
            (StatusId.DarkWaterWaiting, FruP3ApocalypseState.WaterAt(3) - 13.5f));
        yield return Case<FruP4CrystallizeTimeScenario, FruP4CrystallizeTimeStateOverrides>("Crystallize Time", "Blue + Darkness", 13,
            o => o.PlayerDebuffs[Peer] = CrystallizeDebuffChoice.BlueDarkness, (StatusId.Wyrmfang, null), (StatusId.UnholyDarknessWaiting, null));
        yield return Case<FruP4CrystallizeTimeScenario, FruP4CrystallizeTimeStateOverrides>("Crystallize Time", "Red + Ice", 13,
            o => o.PlayerDebuffs[Peer] = CrystallizeDebuffChoice.RedIce, (StatusId.Wyrmclaw, null), (StatusId.DarkBlizzardWaiting, null));
        yield return Case<FruP4DarklitDragonsongScenario, FruP4DarklitDragonsongStateOverrides>("Darklit Dragonsong", "chained with Dark Water", 22.5, o =>
            {
                o.Chained[Peer] = true;
                o.DarkWater[Peer] = true;
            },
            (StatusId.RefulgentChain, null), (StatusId.DarkWaterWaiting, null));
    }

    private static TestCaseData Case<TScenario, TOverrides>(string scenario, string pick, double at, Action<TOverrides> set,
        params (ushort Status, float? Left)[] expected)
        => new TestCaseData(typeof(TScenario), at, (Action<object>)(o => set((TOverrides)o)), expected).SetArgDisplayNames(scenario, pick);

    [TestCaseSource(nameof(Cases))]
    public void APeerShowsWhatTheHostGaveItsSeat(Type scenarioType, double at, Action<object> set, (ushort Status, float? Left)[] expected)
    {
        Dictionary<ushort, float>? shown = null;
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(Host), [new PlayerSeat(Peer)],
            configure: client =>
            {
                if (client.IsHost) set(client.Game.Catalog.Scenarios.Single(s => s.GetType() == scenarioType).SettingsOverrides!);
            },
            inputs: [(at, 1, peer => shown = peer.Fake.BattleCharas.Player.Statuses.Values
                .GroupBy(s => s.StatusId).ToDictionary(g => g.Key, g => g.Max(s => s.RemainingTime)))]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(shown, Is.Not.Null);
        foreach (var (status, left) in expected)
        {
            Assert.That(shown!.ContainsKey(status), Is.True, $"status {status}");
            if (left is { } wanted) Assert.That(shown[status], Is.EqualTo(wanted).Within(Slack), $"time left on status {status}");
        }
    }
}
