using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using AnoMech.Multiplayer;
using AnoMech.Network;
using AnoMech.Relay;
using static AnoMech.Tests.TestWire;

namespace AnoMech.Tests;

// RelayClient against the real RelayServer over loopback.
[Category("Integration")]
public class RelayLoopbackTests
{
    // Membership, routing and ban rules are covered socket-free in RelayRoomsTests; this checks
    // that the client and relay agree on the wire.
    [Test]
    public async Task LiveRelay()
    {
        var log = new QuietLog();
        await using var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, log);
        server.Start();
        var completed = false;
        try
        {
            var url = $"ws://127.0.0.1:{server.LocalEndPoint!.Port}";

            var hostSecret = RelayWire.NewSecret();
            var hostId = RelayWire.PeerId(hostSecret);
            using var host = new RelayClient(hostSecret);
            var code = await host.ConnectAndHostAsync(url);
            Assert.That(host.IsConnected && code != null, "client hosts room");
            var helloReceived = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.MessageReceived += (message, fromHost, connection, sender) => { if (message is HelloMessage) helloReceived.TrySetResult(sender); };

            var peerSecret = RelayWire.NewSecret();
            var peerId = RelayWire.PeerId(peerSecret);
            using var peer = new RelayClient(peerSecret);
            var pingReceived = new TaskCompletionSource<(bool FromHost, Guid Sender)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var compressedReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.MessageReceived += (message, fromHost, connection, sender) =>
            {
                if (message is PingMessage) pingReceived.TrySetResult((fromHost, sender));
                if (message is AnnouncementMessage announcement) compressedReceived.TrySetResult(announcement.Text == new string('x', 512));
            };
            await peer.ConnectAsync(url, code!);
            Assert.That(peer.IsConnected, "client joins room");
            await peer.SendAsync(new HelloMessage(peerId, "Test", "1", "test"));
            Assert.That(await Within(helloReceived.Task, "hello"), Is.EqualTo(peerId), "relay stamps the authenticated sender");
            await host.SendAsync(new PingMessage(123));
            Assert.That(await Within(pingReceived.Task, "ping"), Is.EqualTo((true, hostId)), "host tag reaches clients");
            await host.SendAsync(new AnnouncementMessage(new string('x', 512)));
            Assert.That(await Within(compressedReceived.Task, "announcement"), "compressed payload survives the envelope");

            var rejected = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.MessageRejected += (sender, fromHost, reason) => rejected.TrySetResult(sender);
            var roommateSecret = RelayWire.NewSecret();
            using var roommate = await ConnectRaw(url, code!, roommateSecret);
            await SendRaw(roommate, "{\"t\":\"pose\",");
            Assert.That(await Within(rejected.Task, "rejection"), Is.EqualTo(RelayWire.PeerId(roommateSecret)), "host drops a malformed peer message");
            Assert.That(host.IsConnected && peer.IsConnected, "and the room stays up");

            var kicked = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var removedNotice = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.Disconnected += e => kicked.TrySetResult(e);
            host.PeersRemoved += ids => removedNotice.TrySetResult(ids);
            await host.ModerateAsync("ban", peerId);
            await Within(kicked.Task, "ban disconnect");
            Assert.That(peer.IsConnected, Is.False, "ban disconnects the peer");
            Assert.That(await ClosedByRelay(roommate), "ban closes other identities on the same address");
            Assert.That(await Within(removedNotice.Task, "removal notice"), Is.EqualTo(new[] { RelayWire.PeerId(roommateSecret) }), "host told who else the ban removed");
            Assert.That(host.IsConnected, "ban leaves the host connected");
            completed = true;
        }
        finally
        {
            if (!completed) TestContext.Out.WriteLine(string.Join(Environment.NewLine, log.Lines));
        }
    }

    [Test]
    public async Task StoppingTheRelayDropsItsConnections()
    {
        await using var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, new QuietLog());
        server.Start();
        var port = server.LocalEndPoint!.Port;
        using var host = new RelayClient(RelayWire.NewSecret());
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Disconnected += _ => disconnected.TrySetResult();
        Assert.That(await host.ConnectAndHostAsync($"ws://127.0.0.1:{port}"), Is.Not.Null, "hosts over the managed listener");
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(host.IsConnected, Is.False);
    }

    [Test]
    public async Task GroupedRelaysSendAJoinToTheRelayHoldingTheCode()
    {
        var log = new QuietLog();
        // Each relay's list names both, so both ports are picked before either starts.
        int portA = FreePort(), portB = FreePort();
        List<RelayGroupMember> group = [new(0, $"ws://127.0.0.1:{portA}", "A"), new(1, $"ws://127.0.0.1:{portB}", "B")];
        await using var relayA = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = portA, GroupNumber = 0, Group = group }, log);
        await using var relayB = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = portB, GroupNumber = 1, Group = group }, log);
        relayA.Start();
        relayB.Start();

        using var host = new RelayClient(RelayWire.NewSecret());
        var code = await host.ConnectAndHostAsync(group[1].Url);
        Assert.That(code is not null && RelayWire.CodeServer(code) == 1, "relay B issues its own codes");

        using var lost = new RelayClient(RelayWire.NewSecret());
        var refused = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lost.Disconnected += e => refused.TrySetResult(e);
        await lost.ConnectAsync(group[0].Url, code!);
        var moved = await Within(refused.Task, "redirect") as RelaySessionRejectedException;
        Assert.That(moved?.MovedTo, Is.EqualTo(group[1].Url), "relay A names the relay holding the session");

        using var peer = new RelayClient(RelayWire.NewSecret());
        await peer.ConnectAsync(moved!.MovedTo!, code!);
        Assert.That(peer.IsConnected, "and the session is there");

        var info = await RelayClient.FetchInfoAsync(group[0].Url);
        Assert.That(info?.Server, Is.EqualTo(0));
        Assert.That(info?.Group, Is.EqualTo(group));
        Assert.That(info?.Encrypted, Is.False);
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task<T> Within<T>(Task<T> task, string what)
    {
        try { return await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { throw new TimeoutException($"Timed out: {what}"); }
    }

    private static async Task<ClientWebSocket> ConnectRaw(string url, string code, string secret)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-AnoMech-Protocol", RelayWire.Version.ToString());
        socket.Options.SetRequestHeader("X-AnoMech-Peer-Secret", secret);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri($"{url}/session/{code}"), timeout.Token);
        var frame = await ReadFrame(socket);
        Assert.That(frame.Type, Is.EqualTo(WebSocketMessageType.Text));
        Assert.That(JsonDocument.Parse(frame.Bytes).RootElement.GetProperty("peerId").GetGuid(), Is.EqualTo(RelayWire.PeerId(secret)),
            "greeting names the authenticated identity");
        return socket;
    }

    private static async Task SendRaw(ClientWebSocket socket, string text)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(Bytes(text), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<(byte[] Bytes, WebSocketMessageType Type)> ReadFrame(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var output = new MemoryStream(); var buffer = new byte[65536];
        WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer, timeout.Token); output.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
        return (output.ToArray(), result.MessageType);
    }

    // A close frame or an abort, after any room traffic already on its way; silence is a failure.
    private static async Task<bool> ClosedByRelay(ClientWebSocket socket)
    {
        try
        {
            for (var frames = 0; frames < 64; frames++)
                if ((await ReadFrame(socket)).Type == WebSocketMessageType.Close) return true;
            return false;
        }
        catch (WebSocketException) { return true; }
        catch (OperationCanceledException) { return false; }
    }
}
