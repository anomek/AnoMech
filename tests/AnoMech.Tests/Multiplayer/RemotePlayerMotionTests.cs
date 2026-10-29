using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// How every client shows the other players moving, over good and bad links: no snap a fast move of
// their own doesn't explain, no standing still while they walk, no rushing to catch up, never far
// behind, and standing where they stopped. Each player is a debug bot on their own client, so their
// real path is what their own client has them do. Every other client's copy is drawn from the
// packets that client's game was sent, the way its ground controller walks them (ModelSync): at
// each ActorMove's speed byte toward its spot, and a jump at each ActorSetPos. The fake's own model
// glides every ActorMove over a second, which hides a copy stopping and starting.
public class RemotePlayerMotionTests
{
    private const PartyRole HostSeat = PartyRole.MeleeDpsA;

    // A long line; a VPN at half a second's ping, as players have played through; bad Wi-Fi; a
    // host whose frame rate spaces its poses out.
    private static readonly ConnectionProfile LongLine = TestRelay.Perfect with
    {
        Name = "250 ms ping", UploadLatency = 0.125, DownloadLatency = 0.125, DownloadJitter = 0.03,
    };

    private static readonly ConnectionProfile Vpn = new("500 ms ping over a VPN",
        UploadRate: 1_000_000, SendBufferBytes: 64 * 1024, UploadLatency: 0.25,
        UploadStallEvery: 6, UploadStallMin: 0.1, UploadStallMax: 0.3,
        DownloadRate: 2_000_000, DownloadLatency: 0.25, DownloadJitter: 0.1,
        DownloadStallEvery: 6, DownloadStallMin: 0.1, DownloadStallMax: 0.4,
        PeerHitchEvery: 8, PeerHitchMin: 0.08, PeerHitchMax: 0.2);

    private static readonly ConnectionProfile SlowHost = LongLine with { Name = "20 fps host, 250 ms ping", HostFps = 20 };

    private static readonly ConnectionProfile[] Links = [TestRelay.Perfect, LongLine, Vpn, BadConnections.BadWifiPeer, SlowHost];

    private static IEnumerable<TestCaseData> Cases() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from link in Links
        select new TestCaseData(scenario.GetType(), link).SetArgDisplayNames(scenario.FullName(), link.Name);

    [TestCaseSource(nameof(Cases))]
    public void EveryClientShowsTheOtherPlayersMovingSmoothly(Type scenarioType, ConnectionProfile link)
    {
        var track = new MotionTrack();
        var peers = Enum.GetValues<PartyRole>().Where(r => r != HostSeat).Select(r => new PlayerSeat(r)).ToList();
        var hostLink = TestRelay.Perfect with { HostFps = link.HostFps };
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(HostSeat), peers, hostLink: hostLink, peerLink: link,
            configure: client => client.Fake.UserActions.Enabled = true, beforeFrame: track.BeforeFrame);
        Assert.That(run.Failure, Is.Null);

        var report = MotionCheck.Measure(run, (viewer, seat) => Delay(link, viewer, seat), track);
        foreach (var line in report.Lines) TestContext.Out.WriteLine(line);
        Assert.That(report.Problems, Is.Empty, string.Join(Environment.NewLine, report.Problems.Take(20)));
    }

    // Each client's own part of the path: its poses up to the host, the host's down to it.
    private static double Delay(ConnectionProfile link, string viewer, PartyRole seat)
    {
        var fromOwner = seat == HostSeat ? 0 : link.UploadLatency;
        var toViewer = viewer == "Host" ? 0 : link.DownloadLatency;
        return fromOwner + toViewer;
    }
}
