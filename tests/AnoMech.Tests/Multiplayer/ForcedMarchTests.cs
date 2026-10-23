using System.Globalization;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Tea.P4PerfectAlexander;

namespace AnoMech.Tests;

// A march a mechanic puts on a peer's player is walked by that peer's own client, on the
// ActorControlSelf steps retail sends the marched player; the host's client only sees the walk in
// that player's poses.
public class ForcedMarchTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MainTank);
    private static readonly PlayerSeat Peer = new(PartyRole.CasterDps);
    private const uint MarchStep = 0x602, MarchEnd = 0x603;

    [Test]
    public void APeersOwnClientMarchesItsPlayer()
    {
        var run = MultiplayerRun.Play(typeof(TeaP4PerfectAlexanderScenario), 0, 1, Host, [Peer], immortal: true, configure: client =>
        {
            if (!client.IsHost) return;
            var overrides = (TeaP4PerfectAlexanderStateOverrides)client.Game.Catalog.Scenarios.OfType<TeaP4PerfectAlexanderScenario>().Single().SettingsOverrides;
            overrides.Practice = P4Practice.TheFinalWord;
            overrides.FinalWord[Peer.Role] = FinalWordDebuff.DarkFollower;
        });
        Assert.That(run.Failure, Is.Null);
        var peer = run.Peers[0];
        Assert.Multiple(() =>
        {
            Assert.That(Controls(peer, MarchStep), Is.GreaterThan(5), "the peer's client never marched its player");
            Assert.That(Controls(peer, MarchEnd), Is.EqualTo(1), "the peer's march never ended");
            Assert.That(Controls(run.Host, MarchStep), Is.Zero, "the host's client was sent the peer's march");
        });
    }

    private static int Controls(TestClient client, uint category)
        => client.Fake.Timeline.Rows.Count(row => row["kind"] == "ac_self" && Convert.ToUInt32(row["id"], 16) == category
                                                  && row.GetValueOrDefault("actor") is { Length: > 0 } hex
                                                  && client.NameAt(uint.Parse(hex, NumberStyles.HexNumber), double.Parse(row["t"], CultureInfo.InvariantCulture)) == Peer.Role.ToString());
}
