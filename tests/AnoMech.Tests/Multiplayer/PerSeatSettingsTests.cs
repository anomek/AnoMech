using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
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
        yield return Case<FruP3UltimateRelativityScenario, FruP3UltimateRelativityStateOverrides>("Ultimate Relativity", "long fire", 13, (o, solo) =>
            {
                o.DpsIce = false;
                Pick(o.Fire, RelativityFire.Long, solo);
            },
            (StatusId.DarkFireWaiting, FruP3UltimateRelativityState.FireAt(2) - 13), (StatusId.ShadoweyeWaiting, null));
        yield return Case<FruP3ApocalypseScenario, FruP3ApocalypseStateOverrides>("Apocalypse", "long Dark Water", 13.5,
            (o, solo) => Pick(o.DarkWater, ApocalypseWater.Long, solo), (StatusId.DarkWaterWaiting, FruP3ApocalypseState.WaterAt(3) - 13.5f));
        yield return Case<FruP4CrystallizeTimeScenario, FruP4CrystallizeTimeStateOverrides>("Crystallize Time", "Blue + Darkness", 13,
            (o, solo) => Pick(o.PlayerDebuffs, CrystallizeDebuffChoice.BlueDarkness, solo), (StatusId.Wyrmfang, null), (StatusId.UnholyDarknessWaiting, null));
        yield return Case<FruP4CrystallizeTimeScenario, FruP4CrystallizeTimeStateOverrides>("Crystallize Time", "Red + Ice", 13,
            (o, solo) => Pick(o.PlayerDebuffs, CrystallizeDebuffChoice.RedIce, solo), (StatusId.Wyrmclaw, null), (StatusId.DarkBlizzardWaiting, null));
        yield return Case<FruP4DarklitDragonsongScenario, FruP4DarklitDragonsongStateOverrides>("Darklit Dragonsong", "chained with Dark Water", 22.5, (o, solo) =>
            {
                Pick(o.Chained, true, solo);
                Pick(o.DarkWater, true, solo);
            },
            (StatusId.RefulgentChain, null), (StatusId.DarkWaterWaiting, null));
    }

    private static TestCaseData Case<TScenario, TOverrides>(string scenario, string pick, double at, Action<TOverrides, bool> set,
        params (ushort Status, float? Left)[] expected)
        => new TestCaseData(typeof(TScenario), at, (Action<object, bool>)((o, solo) => set((TOverrides)o, solo)), expected).SetArgDisplayNames(scenario, pick);

    // Solo, a pick is the player's own; hosting, the peer's seat's.
    private static void Pick<T>(PerRoleSetting<T> setting, T value, bool solo) where T : struct
    {
        if (solo) setting.Mine = value;
        else setting[Peer] = value;
    }

    [TestCaseSource(nameof(Cases))]
    public void APeerShowsWhatTheHostGaveItsSeat(Type scenarioType, double at, Action<object, bool> set, (ushort Status, float? Left)[] expected)
    {
        Dictionary<ushort, float>? shown = null;
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(Host), [new PlayerSeat(Peer)],
            configure: client =>
            {
                if (client.IsHost) set(client.Game.Catalog.Scenarios.Single(s => s.GetType() == scenarioType).SettingsOverrides!, false);
            },
            inputs: [(at, 1, peer => shown = Shown(peer.Fake.BattleCharas.Player))]);
        Assert.That(run.Failure, Is.Null);
        AssertShows(shown, expected);
    }

    // A session leaves the process as it found it: a solo run after it in the same process shows its
    // player their own pick.
    [TestCaseSource(nameof(Cases))]
    public void ASoloPlayerAfterASessionShowsTheirOwnPick(Type scenarioType, double at, Action<object, bool> set, (ushort Status, float? Left)[] expected)
    {
        Assert.That(MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(Host), [new PlayerSeat(Peer)]).Failure, Is.Null);
        Dictionary<ushort, float>? shown = null;
        var run = ScenarioRun.Execute(scenarioType, 0, 1, new ScenarioRunOptions
        {
            StopAt = (float)at + 0.1f,
            Solo = true,
            PlayerRole = Peer,
            Overrides = o => set(o, true),
            Probe = probe =>
            {
                if (probe.Crossed((float)at)) shown = Shown(((FakeBattleCharas)Natives.BattleCharas).Player);
            },
        });
        Assert.That(run.Failure, Is.Null);
        AssertShows(shown, expected);
    }

    private static Dictionary<ushort, float> Shown(FakeCharacter player)
        => player.Statuses.Values.GroupBy(s => s.StatusId).ToDictionary(g => g.Key, g => g.Max(s => s.RemainingTime));

    private static void AssertShows(Dictionary<ushort, float>? shown, (ushort Status, float? Left)[] expected)
    {
        Assert.That(shown, Is.Not.Null);
        foreach (var (status, left) in expected)
        {
            Assert.That(shown!.ContainsKey(status), Is.True, $"status {status}");
            if (left is { } wanted) Assert.That(shown[status], Is.EqualTo(wanted).Within(Slack), $"time left on status {status}");
        }
    }
}
