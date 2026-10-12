using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AnoMech.Network;
using static AnoMech.Relay.Tests.TestWire;

namespace AnoMech.Relay.Tests;

public class RelayServerTests
{
    [Test]
    public void RelayRequiresTheProtocolVersionAndAWellFormedCredential()
    {
        var secret = RelayWire.NewSecret();
        var version = RelayWire.Version.ToString();
        Assert.That(RelayServer.AuthenticatePeer(version, secret), Is.EqualTo(RelayWire.PeerId(secret)));
        Assert.That(RelayServer.AuthenticatePeer(null, secret), Is.Null);
        Assert.That(RelayServer.AuthenticatePeer((RelayWire.Version + 1).ToString(), secret), Is.Null);
        Assert.That(RelayServer.AuthenticatePeer(version, null), Is.Null);
        Assert.That(RelayServer.AuthenticatePeer(version, "short"), Is.Null);
        Assert.That(RelayServer.AuthenticatePeer(version, new string('z', 64)), Is.Null);
    }

    [Test]
    public void MappedIpv4AbuseBucketsStayIndependent()
    {
        var mappedA = RelayServer.AbuseKey(IPAddress.Parse("::ffff:192.0.2.1"));
        var mappedB = RelayServer.AbuseKey(IPAddress.Parse("::ffff:192.0.2.2"));
        Assert.That(mappedA, Is.EqualTo(IPAddress.Parse("192.0.2.1")));
        Assert.That(mappedA, Is.Not.EqualTo(mappedB));
    }

    [Test]
    public async Task RelaySerializesConcurrentWriters()
    {
        var socket = new StubSocket();
        var server = new RelayServer(new RelayOptions(), new QuietLog());
        var peer = new PeerConn(socket, 1u, IPAddress.Loopback, Guid.NewGuid());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => server.SendOneAsync(peer, new byte[] { 1 }, WebSocketMessageType.Text, timeout.Token)));
        Assert.That(socket.PeakSends, Is.EqualTo(1));
        Assert.That(socket.Sends, Is.EqualTo(20));
    }

    [Test]
    public async Task HttpFrontDoor()
    {
        await using var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, new QuietLog());
        server.Start();
        var port = server.LocalEndPoint!.Port;

        var info = await RawHttp(port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n");
        Assert.That(info, Does.StartWith("HTTP/1.1 200 "));
        var infoJson = JsonDocument.Parse(info[(info.IndexOf("\r\n\r\n") + 4)..]).RootElement;
        Assert.That(infoJson.GetProperty("requiresToken").GetBoolean(), Is.False, "info served without a token requirement");
        Assert.That(infoJson.TryGetProperty("group", out _) || infoJson.TryGetProperty("server", out _), Is.False, "a relay outside a group lists none");
        Assert.That(await RawHttp(port, "not http at all\r\n\r\n"), Does.StartWith("HTTP/1.1 400 "), "malformed request line refused");
        Assert.That(await RawHttp(port, "GET /host HTTP/1.1\r\nHost: x\r\n\r\n"), Does.StartWith("HTTP/1.1 400 "), "plain GET to a WebSocket path refused");
        var oversized = await RawHttp(port, "GET /info HTTP/1.1\r\nX-Pad: " + new string('a', RelayHttp.MaxHeaderBytes) + "\r\n\r\n");
        Assert.That(oversized, Is.Empty.Or.StartWith("HTTP/1.1 400 "), "oversized header block refused");
        Assert.That(await RawHttp(port, "GET /admin/stats HTTP/1.1\r\nHost: x\r\n\r\n"), Does.StartWith("HTTP/1.1 404 "), "admin hidden without an admin token");
    }

    [Test]
    public async Task InfoListsTheGroup()
    {
        List<RelayGroupMember> group = [new(0, "wss://us.example.com", "US East"), new(1, "wss://de.example.com", "EU Central")];
        await using var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0, GroupNumber = 1, Group = group }, new QuietLog());
        server.Start();
        var info = await RawHttp(server.LocalEndPoint!.Port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n");
        var json = JsonDocument.Parse(info[(info.IndexOf("\r\n\r\n") + 4)..]).RootElement;
        Assert.That(json.GetProperty("server").GetInt32(), Is.EqualTo(1));
        Assert.That(json.GetProperty("group").Deserialize<RelayGroupMember[]>(RelayGroup.Json), Is.EqualTo(group));
    }

    [Test]
    public async Task JoinForAnotherRelaysCodeIsSentThereWithoutCountingAsAGuess()
    {
        await using var server = new RelayServer(new RelayOptions
        {
            BindAddress = IPAddress.Loopback, Port = 0, MaxFailedJoinsPerWindow = 2, GroupNumber = 1,
            Group = [new(0, "wss://us.example.com", "US East"), new(1, "wss://de.example.com", "EU Central")],
        }, new QuietLog());
        server.Start();
        var url = $"ws://127.0.0.1:{server.LocalEndPoint!.Port}";
        for (var i = 0; i < 5; i++)
            Assert.That(await JoinRefusal(url, RelayWire.NewCode(0)), Is.EqualTo(RelayWire.MovedPrefix + "wss://us.example.com"));
        Assert.That(await JoinRefusal(url, RelayWire.NewCode(1)), Is.EqualTo("session not found"), "not locked out");
    }

    [Test]
    public async Task StoppedRelayReleasesItsPort()
    {
        await using var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, new QuietLog());
        server.Start();
        var port = server.LocalEndPoint!.Port;
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var rebound = new TcpListener(IPAddress.Loopback, port);
        Assert.That(rebound.Start, Throws.Nothing);
        rebound.Stop();
    }

    [Test]
    public void HostControlFramesNameAnOperationAndAnIdentity()
    {
        var id = Guid.NewGuid();
        Assert.That(RelayServer.ParseControl(Bytes($$"""{"t":"relayControl","Operation":"ban","PeerId":"{{id}}"}""")), Is.EqualTo(("ban", id)));
        Assert.That(RelayServer.ParseControl(Bytes("""{"t":"relayControl","Operation":"ban","PeerId":"not a guid"}""")), Is.Null);
        Assert.That(RelayServer.ParseControl(Bytes($$"""{"t":"relayControl","Operation":1,"PeerId":"{{id}}"}""")), Is.Null);
        Assert.That(RelayServer.ParseControl(Bytes($$"""{"t":"relayControl","PeerId":"{{id}}"}""")), Is.Null);
        Assert.That(RelayServer.ParseControl(Bytes("""{"t":"relayControl",""")), Is.Null);
        Assert.That(RelayServer.ParseControl(Bytes("[]")), Is.Null);
    }

    private static async Task<string?> JoinRefusal(string url, string code)
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-AnoMech-Protocol", RelayWire.Version.ToString());
        socket.Options.SetRequestHeader("X-AnoMech-Peer-Secret", RelayWire.NewSecret());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri($"{url}/session/{code}"), timeout.Token);
        var result = await socket.ReceiveAsync(new byte[1024], timeout.Token);
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
        return result.CloseStatusDescription;
    }

    // A refusal may reach the client as a reset rather than a readable response when the
    // relay closes with part of the request still unread; that comes back as "".
    private static async Task<string> RawHttp(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        try
        {
            await stream.WriteAsync(Bytes(request));
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (IOException) { return ""; }
    }
}
