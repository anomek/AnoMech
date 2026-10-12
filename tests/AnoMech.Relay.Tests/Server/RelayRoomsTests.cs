using System.Net;
using AnoMech.Network;

namespace AnoMech.Relay.Tests;

public class RelayRoomsTests
{
    private static readonly IPAddress Home = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress Away = IPAddress.Parse("192.0.2.2");

    private uint nextConnection;
    private RelayRooms rooms = null!;
    private string code = "";
    private PeerConn host = null!;

    [SetUp]
    public void HostARoom()
    {
        rooms = new RelayRooms(new RelayOptions { MaxPeersPerSession = 4 });
        host = Connection(Home);
        Assert.That(rooms.TryCreate(host, out code));
        Assert.That(rooms.MarkReady(code, host));
    }

    private PeerConn Connection(IPAddress ip, Guid? identity = null)
        => new(new StubSocket(), ++nextConnection, ip, identity ?? Guid.NewGuid());

    private PeerConn Join(IPAddress ip, Guid? identity = null)
    {
        var peer = Connection(ip, identity);
        Assert.That(rooms.Join(code, peer).Outcome, Is.EqualTo(JoinOutcome.Joined));
        Assert.That(rooms.MarkReady(code, peer));
        return peer;
    }

    private List<PeerConn> TargetsOf(PeerConn sender) => rooms.Route(code, sender)?.Targets ?? [];

    [Test]
    public void HostingIssuesAWellFormedCode()
    {
        Assert.That(RelayWire.IsValidCode(code), code);
        Assert.That(rooms.CountPeers(code), Is.EqualTo(1));
    }

    [Test]
    public void RelayInAGroupHoldsOnlyItsOwnCodes()
    {
        var grouped = new RelayRooms(new RelayOptions
        {
            GroupNumber = 1,
            Group = [new(0, "wss://us.example.com", "US East"), new(1, "wss://de.example.com", "EU Central")],
        });
        Assert.That(grouped.TryCreate(Connection(Home), out var own));
        Assert.That(RelayWire.CodeServer(own), Is.EqualTo(1));

        var moved = grouped.Join(RelayWire.NewCode(0), Connection(Away));
        Assert.That(moved.Outcome, Is.EqualTo(JoinOutcome.Moved));
        Assert.That(RelayWire.MovedTo(moved.Reason), Is.EqualTo("wss://us.example.com"));
        Assert.That(grouped.Join(RelayWire.NewCode(2), Connection(Away)).Outcome, Is.EqualTo(JoinOutcome.NotFound), "no relay in the group has number 2");
        Assert.That(grouped.Join(RelayWire.NewCode(1), Connection(Away)).Outcome, Is.EqualTo(JoinOutcome.NotFound), "its own number, but never hosted");
    }

    [Test]
    public void RelayRefusesRoomsPastItsCap()
    {
        var full = new RelayRooms(new RelayOptions { MaxTotalSessions = 1 });
        Assert.That(full.TryCreate(Connection(Home), out _));
        Assert.That(full.TryCreate(Connection(Home), out _), Is.False);
    }

    [Test]
    public void UnknownCodeIsNotFound()
        => Assert.That(rooms.Join("ZZZZZZ", Connection(Home)).Reason, Is.EqualTo("session not found"));

    [Test]
    public void FullRoomRefusesNewcomersButNotAReconnect()
    {
        var peers = Enumerable.Range(0, 3).Select(_ => Join(Away)).ToList();
        var refused = rooms.Join(code, Connection(Away));
        Assert.That(refused.Reason, Is.EqualTo("session full"));

        var reconnect = Connection(Away, peers[0].PeerId);
        var result = rooms.Join(code, reconnect);
        Assert.That(result.Outcome, Is.EqualTo(JoinOutcome.Joined));
        Assert.That(result.Replaced, Is.SameAs(peers[0]), "one connection per identity");
        Assert.That(peers[0].Ready, Is.False);
        Assert.That(rooms.MarkReady(code, peers[0]), Is.False, "a replaced connection never becomes ready");
    }

    [Test]
    public void PeerTrafficReachesOnlyTheHostAndHostTrafficReachesEveryone()
    {
        var a = Join(Away);
        var b = Join(Away);
        var greeting = Connection(Away);
        rooms.Join(code, greeting);

        Assert.That(rooms.Route(code, a)?.FromHost, Is.False);
        Assert.That(TargetsOf(a), Is.EqualTo(new[] { host }));
        Assert.That(rooms.Route(code, host)?.FromHost, Is.True);
        Assert.That(TargetsOf(host), Is.EqualTo(new[] { a, b }), "nobody hears traffic before its greeting is out");
        Assert.That(rooms.Route(code, greeting), Is.Null, "nor sends it");
    }

    [Test]
    public void RoomOutlivesItsHostsConnectionAndTheHostComesBack()
    {
        var peer = Join(Away);
        rooms.Leave(code, host);
        Assert.That(rooms.CountPeers(code), Is.EqualTo(1));

        var returned = Join(Home, host.PeerId);
        Assert.That(rooms.Route(code, returned)?.FromHost, Is.True);
        Assert.That(TargetsOf(peer), Is.EqualTo(new[] { returned }));
    }

    [Test]
    public void EmptyRoomIsGone()
    {
        var peer = Join(Away);
        rooms.Leave(code, host);
        rooms.Leave(code, peer);
        Assert.That(rooms.Count, Is.Zero);
        Assert.That(rooms.Join(code, Connection(Away)).Outcome, Is.EqualTo(JoinOutcome.NotFound));
    }

    [Test]
    public void KickRemovesOnlyItsTarget()
    {
        var target = Join(Away);
        var roommate = Join(Away);
        var result = rooms.Moderate(code, host, "kick", target.PeerId);
        Assert.That(result!.Removed, Is.EqualTo(new[] { target }));
        Assert.That(result.Others, Is.Empty);
        Assert.That(TargetsOf(host), Is.EqualTo(new[] { roommate }));
        Assert.That(Join(Away, target.PeerId), Is.Not.Null, "a kick is not a ban");
    }

    [Test]
    public void BanClearsTheTargetsNetworkButNotTheHostOrOtherNetworks()
    {
        var target = Join(Home);
        var roommate = Join(Home);
        var elsewhere = Join(Away);
        var result = rooms.Moderate(code, host, "ban", target.PeerId);
        Assert.That(result!.Removed, Is.EquivalentTo(new[] { target, roommate }));
        Assert.That(result.Others, Is.EqualTo(new[] { roommate.PeerId }), "host is told who else the ban took");
        Assert.That(TargetsOf(host), Is.EqualTo(new[] { elsewhere }));
    }

    [Test]
    public void BanSurvivesIdentityRotationButNotTheHost()
    {
        var target = Join(Home);
        rooms.Moderate(code, host, "ban", target.PeerId);
        Assert.That(rooms.Join(code, Connection(Home)).Reason, Is.EqualTo("banned from room"), "new identity, same network");
        Assert.That(rooms.Join(code, Connection(Home, target.PeerId)).Outcome, Is.EqualTo(JoinOutcome.Banned));
        Assert.That(rooms.Join(code, Connection(Away, target.PeerId)).Outcome, Is.EqualTo(JoinOutcome.Banned), "same identity, new network");
        Join(Home, host.PeerId);
        Join(Away);
    }

    [Test]
    public void BanWhileAwayBlocksTheReturnAndClearsThatNetwork()
    {
        var absent = Guid.NewGuid();
        var roommate = Join(Away);
        var elsewhere = Join(Home);
        Assert.That(rooms.Moderate(code, host, "ban", absent), Is.Null, "nobody removed yet");
        Assert.That(TargetsOf(host), Is.EquivalentTo(new[] { roommate, elsewhere }));

        var result = rooms.Join(code, Connection(Away, absent));
        Assert.That(result.Outcome, Is.EqualTo(JoinOutcome.Banned));
        Assert.That(result.Evicted, Is.EqualTo(new[] { roommate }));
        Assert.That(result.Host, Is.SameAs(host));
        Assert.That(TargetsOf(host), Is.EqualTo(new[] { elsewhere }));
    }

    [Test]
    public void UnbanPermitsTheReturn()
    {
        var target = Join(Away);
        rooms.Moderate(code, host, "ban", target.PeerId);
        rooms.Moderate(code, host, "unban", target.PeerId);
        Join(Away, target.PeerId);
    }

    [Test]
    public void OnlyTheReadyHostModeratesAndNeverItself()
    {
        var peer = Join(Away);
        var other = Join(Away);
        Assert.That(rooms.Moderate(code, peer, "kick", other.PeerId), Is.Null);
        Assert.That(rooms.Moderate(code, host, "ban", host.PeerId), Is.Null);
        Assert.That(rooms.Moderate(code, host, "mute", other.PeerId), Is.Null);
        host.Ready = false;
        Assert.That(rooms.Moderate(code, host, "kick", other.PeerId), Is.Null);
        host.Ready = true;
        Assert.That(TargetsOf(host), Is.EquivalentTo(new[] { peer, other }));
    }

    [Test]
    public void BanListIsBoundedButAFullListStillKicks()
    {
        for (var i = 0; i < 1024; i++) rooms.Moderate(code, host, "ban", Guid.NewGuid());
        var overflow = Guid.NewGuid();
        rooms.Moderate(code, host, "ban", overflow);
        Join(Away, overflow);
        var target = Join(Away);
        Assert.That(rooms.Moderate(code, host, "ban", target.PeerId)!.Removed, Does.Contain(target));
    }

    [Test]
    public void IdleRoomsAreReaped()
    {
        var peer = Join(Away);
        Assert.That(rooms.RemoveIdleSince(DateTime.UtcNow.AddMinutes(-1)), Is.Empty);
        var reaped = rooms.RemoveIdleSince(DateTime.UtcNow.AddMinutes(1));
        Assert.That(reaped.Single().Code, Is.EqualTo(code));
        Assert.That(reaped.Single().Peers, Is.EquivalentTo(new[] { host, peer }));
        Assert.That(rooms.Count, Is.Zero);
    }

    [Test]
    public void AdminLookups()
    {
        var peer = Join(Away);
        Assert.That(rooms.FindConnection(peer.Id), Is.EqualTo((code, peer)));
        Assert.That(rooms.FindConnection(999), Is.Null);
        Assert.That(rooms.PeersAt(Away), Is.EqualTo(new[] { peer }));
        Assert.That(rooms.TotalPeers, Is.EqualTo(2));
        Assert.That(rooms.Snapshot().Single().Peers.Single(p => p.IsHost).Id, Is.EqualTo(host.Id));
        Assert.That(rooms.Disband("ZZZZZZ"), Is.Null);
        Assert.That(rooms.Disband(code), Is.EquivalentTo(new[] { host, peer }));
        Assert.That(rooms.Count, Is.Zero);
    }

    [Test]
    public void StoppingEmptiesEveryRoom()
    {
        var peer = Join(Away);
        Assert.That(rooms.TryCreate(Connection(Away), out _));
        Assert.That(rooms.Clear(), Has.Count.EqualTo(3).And.Contains(peer));
        Assert.That(rooms.Count, Is.Zero);
    }
}
