using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// Each client plays its own music: the phase's track from the run's start, then each change of
// track, which every client's run makes for itself at the same time (a scenario's
// RunInstanceEvents). Every client's game is told the same; with music suppressed, nothing.
public class MusicTests
{
    private const double SwitchAt = 5, SilenceAt = 8;
    private static readonly ushort NextTrack = UmadZone.P1.Bgm;

    [TestCase(true)]
    [TestCase(false)]
    public void EveryClientPlaysTheSameMusic(bool music)
    {
        void Switch(TestClient client) => client.Game.World.SwitchBgm(NextTrack);
        void Silence(TestClient client) => client.Game.World.SilenceBgm();
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, new PlayerSeat(PartyRole.MeleeDpsA), [new PlayerSeat(PartyRole.CasterDps)],
            immortal: true, configure: client => client.Config.SuppressBgm = !music,
            inputs: [(SwitchAt, 0, Switch), (SwitchAt, 1, Switch), (SilenceAt, 0, Silence), (SilenceAt, 1, Silence)]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "changes the fight ended before");

        IScenario scenario = new UmadP3BlackHoleScenario();
        string[] played = music
            ? [$"{scenario.Phase.Bgm} seconds_in={SimTimeline.F(scenario.BgmSecondsAtStart)}", $"{NextTrack} switch", " silence"]
            : [];
        Assert.That(Music(run.Host), Is.EqualTo(played), "the host's");
        Assert.That(Music(run.Peers[0]), Is.EqualTo(played), "the peer's");
        Assert.That(ClientParity.Compare(run, run.Peers[0]).Where(g => g.What.StartsWith("bgm ", StringComparison.Ordinal)), Is.Empty);
    }

    private static List<string> Music(TestClient client)
        => client.Fake.Timeline.Rows.Where(r => r["kind"] == "bgm").Select(r => $"{r["id"]} {r["detail"]}").ToList();
}
