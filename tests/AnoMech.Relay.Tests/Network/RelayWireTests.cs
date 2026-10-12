using AnoMech.Network;
using static AnoMech.Relay.Tests.TestWire;

namespace AnoMech.Relay.Tests;

public class RelayWireTests
{
    [Test]
    public void PrivateCredentialDeterminesIdentity()
    {
        var secret = RelayWire.NewSecret();
        var id = RelayWire.PeerId(secret);
        Assert.That(RelayWire.PeerId(secret), Is.EqualTo(id));
        Assert.That(RelayWire.PeerId(RelayWire.NewSecret()), Is.Not.EqualTo(id));
        Rejects(() => RelayWire.PeerId("bad"), "malformed credential");
        Rejects(() => RelayWire.PeerId(new string('g', 64)), "non-hex credential");
    }

    [Test]
    public void PeerMessagesMustCarryTheSendersIdentity()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var victim = Guid.NewGuid();
        var hello = Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"Test","Version":"1","Checksum":"test"}""");
        Assert.That(RelayWire.Validate(hello, false, id), Is.EqualTo("hello"), "valid peer message");
        Rejects(() => RelayWire.Validate(hello, false, Guid.NewGuid()), "forged peer identity");
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"hello","PeerId":"{{id}}","PeerId":"{{victim}}"}"""), false, id), "duplicate identity property");
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"hello","peerid":"{{id}}","PeerId":"{{victim}}"}"""), false, id), "duplicate identity differing only in case");
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"hello","X":{"PeerId":"{{id}}"},"PeerId":"{{victim}}"}"""), false, id), "nested identity can't stand in for the real one");
        Assert.That(RelayWire.Validate(Bytes($$"""{"t":"hello","X":{"PeerId":"{{victim}}"},"PeerId":"{{id}}"}"""), false, id), Is.EqualTo("hello"), "nested identity field ignored");
        Rejects(() => RelayWire.Validate(Bytes("{\"t\":\"pose\",\"X\":0}"), false, id), "peer message without an identity");
        Rejects(() => RelayWire.Validate(Bytes("{\"t\":\"snapshot\",\"Enemies\":[]}"), false, id), "peer host-only message");
    }

    [Test]
    public void MalformedMessagesAreRefused()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"pong","T":"snapshot","PeerId":"{{id}}"}"""), false, id), "duplicate discriminator");
        Rejects(() => RelayWire.Validate(Bytes("{\"X\":1,\"t\":\"ping\"}"), true, id), "discriminator not first");
        Rejects(() => RelayWire.Validate(Bytes("{}"), true, id), "empty object");
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"pose","PeerId":"{{id}}","X":[1,2"""), false, id), "truncated peer message");
        Rejects(() => RelayWire.Validate(Bytes($$"""{"t":"pose","PeerId":"{{id}}","X":""" + string.Concat(Enumerable.Repeat("[", 80)) + new string(']', 80) + "}"), false, id),
            "excessively nested peer message");
    }

    // Shape limits would refuse real data, so only structure is checked.
    [Test]
    public void LargeWellFormedMessagesAreAccepted()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var accented = Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"{{string.Concat(Enumerable.Repeat("\\u00e9", 5000))}}"}""");
        Assert.That(RelayWire.Validate(accented, false, id), Is.EqualTo("hello"), "long escaped text accepted");
        var bigSnapshot = Bytes("{\"t\":\"snapshot\",\"Enemies\":[" + string.Join(',', Enumerable.Repeat("{}", 5000)) + "]}");
        Assert.That(RelayWire.Validate(bigSnapshot, true, id), Is.EqualTo("snapshot"), "large collections accepted");

        var validateStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) RelayWire.Validate(bigSnapshot, true, id);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - validateStart, Is.LessThan(100 * 256), "checking a host message costs next to nothing");
    }

    [Test]
    public void CredentialAndIdArePerRelay()
    {
        var install = RelayWire.NewSecret();
        var relayA = RelayWire.RelayCredential(install, "relay.example.com");
        Assert.That(RelayWire.IsValidSecret(relayA));
        Assert.That(RelayWire.RelayCredential(install, "wss://RELAY.example.com:443/x"), Is.EqualTo(relayA));
        Assert.That(RelayWire.RelayCredential(install, "other.example.com"), Is.Not.EqualTo(relayA));
        Assert.That(RelayWire.RelayCredential(RelayWire.NewSecret(), "relay.example.com"), Is.Not.EqualTo(relayA));
        Assert.That(RelayWire.PeerId(RelayWire.RelayCredential(install, "other.example.com")), Is.Not.EqualTo(RelayWire.PeerId(relayA)));
    }

    [Test]
    public void RelayReadsOnlySmallControlFrames()
    {
        Assert.That(RelayWire.IsControl(Bytes("{\"t\":\"relayControl\",\"Operation\":\"kick\"}")));
        Assert.That(RelayWire.IsControl(Bytes("{\"t\":\"ping\"}")), Is.False);
        Assert.That(RelayWire.IsControl(Bytes("not json")), Is.False);
        Assert.That(RelayWire.IsControl(Bytes("{}")), Is.False);
        Assert.That(RelayWire.IsControl(Bytes("{\"t\":\"relayControl\",\"X\":\"" + new string('x', 2000) + "\"}")), Is.False);
    }

    [Test]
    public void EnvelopeLayout()
    {
        var id = Guid.NewGuid();
        var envelope = RelayWire.Envelope(true, 7, id, true, Bytes("x"));
        Assert.That(envelope, Has.Length.EqualTo(RelayWire.PrefixBytes + 1));
        Assert.That(envelope[0], Is.EqualTo(1));
        Assert.That(BitConverter.ToUInt32(envelope, 1), Is.EqualTo(7));
        Assert.That(new Guid(envelope.AsSpan(5, 16)), Is.EqualTo(id));
        Assert.That(envelope[21], Is.EqualTo(1));
        Assert.That(envelope[^1], Is.EqualTo((byte)'x'));
    }

    [Test]
    public void DecodingIsBoundedBySizeAndBudget()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        Rejects(() => RelayWire.Decode(Zip(new byte[RelayWire.MaxPeerMessageBytes + 1]), true, new TrafficBudget(), RelayWire.MaxPeerMessageBytes),
            "peer decompression ceiling");
        Rejects(() => RelayWire.Decode(Zip(new byte[RelayWire.MaxMessageBytes + 1]), true, new TrafficBudget()), "host decompression ceiling");

        var bomb = Zip(Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"{{new string('x', 1_000_000)}}"}"""));
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        Rejects(() => RelayWire.Decode(bomb, true, new TrafficBudget(), RelayWire.MaxPeerMessageBytes), "peer compression bomb stopped while decoding");
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocationStart, Is.LessThan(2 * 1024 * 1024), "bounded allocation for a peer compression bomb");
        Rejects(() => RelayWire.Decode(bomb, true, new TrafficBudget(bytes: 512)), "budget charges decompressed bytes");

        var hello = Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"Test","Version":"1","Checksum":"test"}""");
        var traffic = new TrafficBudget(messages: 1);
        RelayWire.Decode(hello, false, traffic);
        Rejects(() => RelayWire.Decode(hello, false, traffic), "message rate ceiling");
    }

    [TestCase("relay.example.com", "wss://relay.example.com:443")]
    [TestCase("RELAY.example.com:7890", "wss://relay.example.com:7890")]
    [TestCase("https://relay.example.com/x", "wss://relay.example.com:443")]
    [TestCase("ws://203.0.113.5:7890", "ws://203.0.113.5:7890")]
    public void RelayOriginsAreCanonical(string url, string origin)
        => Assert.That(RelayWire.Origin(url), Is.EqualTo(origin));

    [Test]
    public void EachRelayOfAGroupIssuesOnlyItsOwnCodes()
    {
        for (var server = 0; server < RelayWire.CodeServers; server++)
            for (var i = 0; i < 100; i++)
            {
                var code = RelayWire.NewCode(server);
                Assert.That(RelayWire.IsValidCode(code) && RelayWire.CodeServer(code) == server, $"{code} from relay {server}");
            }
    }

    // A gap would shrink a relay's share of codes below 32^5, or make its codes guessable.
    [Test]
    public void GroupedCodesStayRandomInEveryPosition()
    {
        var codes = Enumerable.Range(0, 3000).Select(_ => RelayWire.NewCode(5)).ToList();
        for (var position = 0; position < RelayWire.CodeLength; position++)
            Assert.That(codes.Select(c => c[position]).Distinct().Count(), Is.EqualTo(RelayWire.CodeAlphabet.Length), $"position {position}");
        Assert.That(Enumerable.Range(0, 3000).Select(_ => RelayWire.CodeServer(RelayWire.NewCode())).Distinct().Count(), Is.EqualTo(RelayWire.CodeServers),
            "a relay outside a group issues codes for every number");
    }

    [Test]
    public void CodeNamesItsRelayBySumOfAlphabetPositions()
    {
        Assert.That(RelayWire.CodeServer("AAAAAA"), Is.Zero);
        Assert.That(RelayWire.CodeServer("AAAAAB"), Is.EqualTo(1));
        Assert.That(RelayWire.CodeServer("999999"), Is.EqualTo(6 * 31 % 32));
        Assert.That(RelayWire.IsValidCode("ABCDE0"), Is.False, "ambiguous characters are outside the alphabet");
    }

    // What lets a client catch most typos without asking any relay; swapping two characters
    // keeps the sum, so that one only the relay can catch.
    [Test]
    public void OneMistypedCharacterAlwaysNamesAnotherRelay()
    {
        var code = RelayWire.NewCode(3);
        for (var position = 0; position < RelayWire.CodeLength; position++)
            foreach (var typo in RelayWire.CodeAlphabet.Where(c => c != code[position]))
                Assert.That(RelayWire.CodeServer(code[..position] + typo + code[(position + 1)..]), Is.Not.EqualTo(3));
    }

    [Test]
    public void MovedReasonNamesTheRelay()
    {
        Assert.That(RelayWire.MovedTo(RelayWire.MovedPrefix + "wss://de.example.com"), Is.EqualTo("wss://de.example.com"));
        Assert.That(RelayWire.MovedTo("session not found"), Is.Null);
        Assert.That(RelayWire.MovedTo(null), Is.Null);
    }
}
