using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnoMech.Multiplayer;
using AnoMech.Network;
using AnoMech.Relay;

internal static class SecurityTests
{
    private static int passed;

    private sealed class QuietLog : IRelayLog
    {
        public readonly ConcurrentQueue<string> Lines = new();
        public void Info(string message) => Lines.Enqueue(message);
        public void Warn(string message) => Lines.Enqueue(message);
        public void Detail(string message) => Lines.Enqueue(message);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine($"PASS {name}");
        passed++;
    }

    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException or JsonException or ArgumentException or FormatException or TrafficLimitException)
        { Check(true, name); return; }
        throw new Exception($"Accepted invalid input: {name}");
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static byte[] Zip(byte[] bytes)
    {
        using var stream = new MemoryStream();
        using (var compressor = new BrotliStream(stream, CompressionLevel.Fastest, true)) compressor.Write(bytes);
        return stream.ToArray();
    }

    public static async Task<int> Main(string[] args)
    {
        try { await Run(args); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static async Task Run(string[] args)
    {
        WireChecks();
        ConfigurationChecks();
        RelayHelperChecks();
        await SendSerialization();
        await LiveRelay();
        await HttpFrontDoor();
        await GreetingTests();
        await RedirectTests();
        Console.WriteLine($"{passed} security checks passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");
    }

    private static void WireChecks()
    {
        var secret = RelayWire.NewSecret();
        var id = RelayWire.PeerId(secret);
        Check(id == RelayWire.PeerId(secret) && id != RelayWire.PeerId(RelayWire.NewSecret()), "private credential determines identity");
        Reject(() => RelayWire.PeerId("bad"), "malformed credential");
        Reject(() => RelayWire.PeerId(new string('g', 64)), "non-hex credential");

        var hello = Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"Test","Version":"1","Checksum":"test"}""");
        Check(RelayWire.Validate(hello, false, id) == "hello", "valid peer message");
        Reject(() => RelayWire.Validate(hello, false, Guid.NewGuid()), "forged peer identity");
        var victim = Guid.NewGuid();
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"hello","PeerId":"{{id}}","PeerId":"{{victim}}"}"""), false, id), "duplicate identity property");
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"hello","peerid":"{{id}}","PeerId":"{{victim}}"}"""), false, id), "duplicate identity differing only in case");
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"hello","X":{"PeerId":"{{id}}"},"PeerId":"{{victim}}"}"""), false, id), "nested identity can't stand in for the real one");
        Check(RelayWire.Validate(Bytes($$"""{"t":"hello","X":{"PeerId":"{{victim}}"},"PeerId":"{{id}}"}"""), false, id) == "hello", "nested identity field ignored");
        Reject(() => RelayWire.Validate(Bytes("{\"t\":\"pose\",\"X\":0}"), false, id), "peer message without an identity");
        Reject(() => RelayWire.Validate(Bytes("{\"t\":\"snapshot\",\"Enemies\":[]}"), false, id), "peer host-only message");
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"pong","T":"snapshot","PeerId":"{{id}}"}"""), false, id), "duplicate discriminator");
        Reject(() => RelayWire.Validate(Bytes("{\"X\":1,\"t\":\"ping\"}"), true, id), "discriminator not first");
        Reject(() => RelayWire.Validate(Bytes("{}"), true, id), "empty object");
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"pose","PeerId":"{{id}}","X":[1,2"""), false, id), "truncated peer message");
        Reject(() => RelayWire.Validate(Bytes($$"""{"t":"pose","PeerId":"{{id}}","X":""" + string.Concat(Enumerable.Repeat("[", 80)) + new string(']', 80) + "}"), false, id),
            "excessively nested peer message");
        // Host messages are only typed here; the rest falls to deserialization, as it did before.
        Reject(() => Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":1e999}"), true, id), "out-of-range number refused");
        Reject(() => Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":1"), true, id), "truncated host message refused");
        Reject(() => Receive(Bytes("{\"t\":\"notAMessage\"}"), true, id), "unknown host message type refused");
        Check(Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":5}"), true, id) is PingMessage { SentAtMs: 5 }, "host message received");

        // Shape limits would refuse real data the branch accepted, so only structure is checked.
        var accented = Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"{{string.Concat(Enumerable.Repeat("\\u00e9", 5000))}}"}""");
        Check(RelayWire.Validate(accented, false, id) == "hello", "long escaped text accepted");
        var bigSnapshot = Bytes("{\"t\":\"snapshot\",\"Enemies\":[" + string.Join(',', Enumerable.Repeat("{}", 5000)) + "]}");
        Check(RelayWire.Validate(bigSnapshot, true, id) == "snapshot", "large collections accepted");
        var validateStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) RelayWire.Validate(bigSnapshot, true, id);
        Check(GC.GetAllocatedBytesForCurrentThread() - validateStart < 100 * 256, "checking a host message costs next to nothing");

        var install = RelayWire.NewSecret();
        var relayA = RelayWire.RelayCredential(install, "relay.example.com");
        Check(RelayWire.IsValidSecret(relayA) && relayA == RelayWire.RelayCredential(install, "wss://RELAY.example.com:443/x")
            && relayA != RelayWire.RelayCredential(install, "other.example.com")
            && relayA != RelayWire.RelayCredential(RelayWire.NewSecret(), "relay.example.com")
            && RelayWire.PeerId(relayA) != RelayWire.PeerId(RelayWire.RelayCredential(install, "other.example.com")),
            "credential and id are per relay");

        Check(RelayWire.IsControl(Bytes("{\"t\":\"relayControl\",\"Operation\":\"kick\"}")) && !RelayWire.IsControl(Bytes("{\"t\":\"ping\"}"))
            && !RelayWire.IsControl(Bytes("not json")) && !RelayWire.IsControl(Bytes("{}"))
            && !RelayWire.IsControl(Bytes("{\"t\":\"relayControl\",\"X\":\"" + new string('x', 2000) + "\"}")),
            "relay reads only small control frames");
        var envelope = RelayWire.Envelope(true, 7, id, true, Bytes("x"));
        Check(envelope.Length == RelayWire.PrefixBytes + 1 && envelope[0] == 1 && BitConverter.ToUInt32(envelope, 1) == 7
            && new Guid(envelope.AsSpan(5, 16)) == id && envelope[21] == 1 && envelope[^1] == (byte)'x', "envelope layout");

        Reject(() => RelayWire.Decode(Zip(new byte[RelayWire.MaxPeerMessageBytes + 1]), true, new TrafficBudget(), RelayWire.MaxPeerMessageBytes),
            "peer decompression ceiling");
        Reject(() => RelayWire.Decode(Zip(new byte[RelayWire.MaxMessageBytes + 1]), true, new TrafficBudget()), "host decompression ceiling");
        var bomb = Zip(Bytes($$"""{"t":"hello","PeerId":"{{id}}","DisplayName":"{{new string('x', 1_000_000)}}"}"""));
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        Reject(() => RelayWire.Decode(bomb, true, new TrafficBudget(), RelayWire.MaxPeerMessageBytes), "peer compression bomb stopped while decoding");
        Check(GC.GetAllocatedBytesForCurrentThread() - allocationStart < 2 * 1024 * 1024, "bounded allocation for a peer compression bomb");
        Reject(() => RelayWire.Decode(bomb, true, new TrafficBudget(bytes: 512)), "budget charges decompressed bytes");
        var traffic = new TrafficBudget(messages: 1);
        RelayWire.Decode(hello, false, traffic);
        Reject(() => RelayWire.Decode(hello, false, traffic), "message rate ceiling");

        var role = new RoleState(default, true, false, 0, 0, 0, 0, [], [], 10000, 10000);
        var roleMessage = new RolesSnapshotMessage(Enumerable.Repeat(role, 8).ToList());
        Check(RelayWire.Validate(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(roleMessage), true, id) == "rolesSnapshot", "full eight-player snapshot accepted");
        var forms = typeof(MpMessage).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(MpMessage)) && !t.IsAbstract).ToArray();
        // The peer types a relay passes from peers to the host; the rest are the host's alone.
        string[] peerTypes = ["hello", "claim", "release", "pose", "pong", "startCheckResponse", "startAbort", "sessionEnded",
            "resetRequest", "leaveRequest", "selfMitigation", "peerAppliedEnemyStatus", "peerAppliedRoleStatus"];
        var peerForms = 0;
        foreach (var form in forms)
        {
            var constructor = form.GetConstructors().Single();
            var message = (MpMessage)constructor.Invoke(constructor.GetParameters().Select(p => SampleValue(p.ParameterType, id)).ToArray());
            var bytes = JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message);
            var type = RelayWire.Validate(bytes, true, id);
            Check(Receive(bytes, true, id).GetType() == form, $"{type} round-trips from the host");
            if (!peerTypes.Contains(type))
            {
                Reject(() => RelayWire.Validate(bytes, false, id), $"{type} refused from a peer");
                continue;
            }
            // Every Guid field is the sender's id, so a peer type names its real sender.
            Check(Receive(bytes, false, id).GetType() == form, $"{type} accepted from its sender");
            Reject(() => RelayWire.Validate(bytes, false, Guid.NewGuid()), $"{type} refused from anyone else");
            peerForms++;
        }
        Check(forms.Length > 40 && peerForms == peerTypes.Length, $"all {forms.Length} protocol message forms checked, including every peer type");
        var enemyCtor = typeof(EnemyState).GetConstructors().Single();
        var enemy = (EnemyState)enemyCtor.Invoke(enemyCtor.GetParameters().Select(p => SampleValue(p.ParameterType, id)).ToArray());
        enemy = enemy with
        {
            Statuses = Enumerable.Repeat(new EnemyStatusState(1, 1, 30), 64).ToArray(),
            NewVfx = Enumerable.Repeat(new AttachedVfxState("vfx/x.avfx", 1f), 200).ToArray(),
        };
        var worldMessage = new WorldSnapshotMessage(Enumerable.Repeat(enemy, 256).ToList(), [], []);
        Check(Receive(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(worldMessage), true, id) is WorldSnapshotMessage, "large real snapshot accepted");
    }

    // What RelayClient does with a decoded body.
    private static readonly JsonSerializerOptions ClientJson = new() { PropertyNameCaseInsensitive = true };
    private static MpMessage Receive(byte[] body, bool fromHost, Guid sender)
    {
        RelayWire.Validate(body, fromHost, sender);
        return JsonSerializer.Deserialize<MpMessage>(body, ClientJson) ?? throw new InvalidDataException("Null message.");
    }

    private static void ConfigurationChecks()
    {
        Check(RelayWire.Origin("relay.example.com") == "wss://relay.example.com:443"
            && RelayWire.Origin("RELAY.example.com:7890") == "wss://relay.example.com:7890"
            && RelayWire.Origin("https://relay.example.com/x") == "wss://relay.example.com:443"
            && RelayWire.Origin("ws://203.0.113.5:7890") == "ws://203.0.113.5:7890", "relay origins canonical");
        var config = new AnoMech.Configuration { RelayServerUrl = "relay.example.com", RelayAccessToken = "secret" };
        Check(config.TokenForRelay("relay.example.com") == "secret" && config.RelayTokenOrigin == "wss://relay.example.com:443",
            "password saved before origins binds to the saved relay");
        Check(config.TokenForRelay("wss://relay.example.com/path") == "secret"
            && config.TokenForRelay("other.example.com") == ""
            && config.TokenForRelay("relay.example.com:8443") == ""
            && config.TokenForRelay("ws://relay.example.com:443") == ""
            && config.TokenForRelay("not a url ::") == "", "saved password only returned for the relay it was entered for");
        var first = config.EnsurePeerSecret();
        Check(RelayWire.IsValidSecret(first) && config.EnsurePeerSecret() == first, "per-install credential is stable");
        config.PeerSecret = "tampered";
        Check(RelayWire.IsValidSecret(config.EnsurePeerSecret()) && config.PeerSecret != "tampered", "invalid saved credential replaced");
    }

    private static void RelayHelperChecks()
    {
        var secret = RelayWire.NewSecret();
        var version = RelayWire.Version.ToString();
        Check(RelayServer.AuthenticatePeer(version, secret) == RelayWire.PeerId(secret)
            && RelayServer.AuthenticatePeer(null, secret) is null && RelayServer.AuthenticatePeer((RelayWire.Version + 1).ToString(), secret) is null
            && RelayServer.AuthenticatePeer(version, null) is null && RelayServer.AuthenticatePeer(version, "short") is null
            && RelayServer.AuthenticatePeer(version, new string('z', 64)) is null,
            "relay requires the protocol version and a well-formed credential");
        Check(AdminConsole.IsSafeAdminUri("http://localhost:7890") && AdminConsole.IsSafeAdminUri("http://[::1]:7890")
            && AdminConsole.IsSafeAdminUri("https://relay.example") && !AdminConsole.IsSafeAdminUri("http://relay.example")
            && !AdminConsole.IsSafeAdminUri("https://user:secret@relay.example"), "admin transport policy");
        var mappedA = RelayServer.AbuseKey(IPAddress.Parse("::ffff:192.0.2.1"));
        var mappedB = RelayServer.AbuseKey(IPAddress.Parse("::ffff:192.0.2.2"));
        Check(mappedA.Equals(IPAddress.Parse("192.0.2.1")) && !mappedA.Equals(mappedB), "mapped IPv4 abuse buckets stay independent");
        Check(RelayOptions.TryParseNetwork("::ffff:192.0.2.0/120", out var mappedNetwork) && mappedNetwork.Contains(IPAddress.Parse("192.0.2.8")),
            "mapped proxy CIDR normalized");
    }

    private static object? SampleValue(Type type, Guid id)
    {
        if (type == typeof(Guid)) return id;
        if (type == typeof(string)) return "";
        if (type.IsValueType) return Activator.CreateInstance(type);
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType)
        {
            var concrete = type.IsInterface ? typeof(List<>).MakeGenericType(type.GenericTypeArguments) : type;
            return Activator.CreateInstance(concrete);
        }
        return null;
    }

    private static async Task SendSerialization()
    {
        var socket = new ProbeSocket();
        var server = new RelayServer(new RelayOptions(), new QuietLog());
        var peer = new RelayServer.PeerConn(socket, 1u, IPAddress.Loopback, Guid.NewGuid());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sends = Enumerable.Range(0, 20).Select(_ => server.SendOneAsync(peer, new byte[] { 1 }, WebSocketMessageType.Text, timeout.Token));
        await Task.WhenAll(sends);
        Check(socket.PeakSends == 1 && socket.Sends == 20, "relay serializes concurrent writers");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }

    private static async Task<T> Within<T>(Task<T> task, string what)
    {
        try { return await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { throw new Exception($"Timed out: {what}"); }
    }

    // The real relay over loopback. Clients a test places on another network name it in
    // X-Test-Address, which the relay believes because loopback is configured as its proxy.
    private static async Task LiveRelay()
    {
        var log = new QuietLog();
        var options = new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0, ClientIpHeader = "X-Test-Address" };
        options.TrustedProxies.Add(new IPNetwork(IPAddress.Loopback, 32));
        await using var server = new RelayServer(options, log);
        server.Start();
        var port = server.LocalEndPoint!.Port;
        var completed = false;
        try
        {
            var url = $"ws://localhost:{port}";

            var hostSecret = RelayWire.NewSecret();
            var hostId = RelayWire.PeerId(hostSecret);
            var host = new RelayClient(hostSecret);
            var code = await host.ConnectAndHostAsync(url);
            Check(host.IsConnected && code != null, "client hosts room");
            var helloReceived = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.MessageReceived += (message, fromHost, connection, sender) => { if (message is HelloMessage) helloReceived.TrySetResult(sender); };

            var peerSecret = RelayWire.NewSecret();
            var peerId = RelayWire.PeerId(peerSecret);
            using var peer = new RelayClient(peerSecret);
            using var peerPings = new BlockingCollection<(bool FromHost, Guid Sender)>();
            var compressedReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.MessageReceived += (message, fromHost, connection, sender) =>
            {
                if (message is PingMessage) peerPings.Add((fromHost, sender));
                if (message is AnnouncementMessage announcement) compressedReceived.TrySetResult(announcement.Text == new string('x', 512));
            };
            await peer.ConnectAsync(url, code!);
            Check(peer.IsConnected, "client joins room");
            await peer.SendAsync(new HelloMessage(peerId, "Test", "1", "test"));
            Check(await Within(helloReceived.Task, "hello") == peerId, "relay stamps the authenticated sender");
            await host.SendAsync(new PingMessage(123));
            Check(peerPings.TryTake(out var ping, 5000) && ping.FromHost && ping.Sender == hostId, "host tag reaches clients");
            await host.SendAsync(new AnnouncementMessage(new string('x', 512)));
            Check(await Within(compressedReceived.Task, "announcement"), "compressed payload survives the envelope");

            var rejections = new ConcurrentDictionary<Guid, TaskCompletionSource<string>>();
            host.MessageRejected += (sender, fromHost, reason) => { if (!fromHost && rejections.TryGetValue(sender, out var waiter)) waiter.TrySetResult(reason); };
            using var observer = await ConnectRaw(url, code!, RelayWire.NewSecret());
            async Task<bool> DroppedByHost(byte[] body, WebSocketMessageType type)
            {
                var secret = RelayWire.NewSecret();
                var waiter = rejections.GetOrAdd(RelayWire.PeerId(secret), _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
                using var attacker = await ConnectRaw(url, code!, secret);
                await SendRaw(attacker, body, type);
                await Within(waiter.Task, "rejection");
                return host.IsConnected && peer.IsConnected;
            }
            Check(await DroppedByHost(Bytes($$"""{"t":"hello","PeerId":"{{hostId}}","DisplayName":"Fake"}"""), WebSocketMessageType.Text),
                "host identity spoof dropped");
            Check(await DroppedByHost(Bytes($$"""{"t":"claim","PeerId":"{{peerId}}","Role":1}"""), WebSocketMessageType.Text),
                "peer identity spoof dropped");
            Check(await DroppedByHost(Bytes("{\"t\":\"snapshot\",\"Enemies\":[],\"EventObjects\":[],\"Tethers\":[]}"), WebSocketMessageType.Text),
                "forged host snapshot dropped");
            Check(await DroppedByHost(Bytes("{\"t\":\"pose\","), WebSocketMessageType.Text), "malformed peer message leaves the room up");
            Check(await DroppedByHost(Zip(Bytes("{\"t\":\"pose\",\"X\":\"" + new string('x', 1_000_000) + "\"}")), WebSocketMessageType.Binary),
                "relay forwards a peer's compression bomb unread and the host stops it");
            Check(await TryReadFrame(observer, TimeSpan.FromMilliseconds(500)) is null, "peer traffic reaches only the host");

            var replacementSecret = RelayWire.NewSecret();
            var replacementId = RelayWire.PeerId(replacementSecret);
            using var oldPeer = await ConnectRaw(url, code!, replacementSecret);
            using var replacement = await ConnectRaw(url, code!, replacementSecret);
            var resumedHello = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.MessageReceived += (message, fromHost, connection, sender) => { if (message is HelloMessage && sender == replacementId) resumedHello.TrySetResult(true); };
            await SendRaw(replacement, $$"""{"t":"hello","PeerId":"{{replacementId}}","DisplayName":"Reconnected","Version":"1","Checksum":"test"}""");
            Check(await Within(resumedHello.Task, "resumed hello"), "credential resumes on a new connection");
            Check(await ClosedByRelay(oldPeer), "resumed credential replaces the old connection");

            host.Dispose();
            await Task.Delay(300);
            Check(peer.IsConnected, "room outlives the host's connection");
            var reconnectedHost = new RelayClient(hostSecret);
            await reconnectedHost.ConnectAsync(url, code!);
            Check(reconnectedHost.IsConnected, "host reconnects to its room");
            await reconnectedHost.SendAsync(new PingMessage(456));
            Check(peerPings.TryTake(out var restored, 5000) && restored.FromHost && restored.Sender == hostId, "reconnected host is the host again");
            host = reconnectedHost;

            using var otherNetwork = await ConnectRaw(url, code!, RelayWire.NewSecret(), address: "192.0.2.20");
            var kicked = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var removedNotice = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.Disconnected += e => kicked.TrySetResult(e);
            host.PeersRemoved += ids => removedNotice.TrySetResult(ids);
            await host.ModerateAsync("ban", peerId);
            await Within(kicked.Task, "ban disconnect");
            Check(!peer.IsConnected, "ban disconnects the peer");
            Check(await ClosedByRelay(replacement), "ban closes other identities on the same address");
            var removedIds = await Within(removedNotice.Task, "removal notice");
            Check(removedIds.Contains(replacementId) && !removedIds.Contains(peerId), "host told who else the ban removed");
            Check(host.IsConnected, "ban leaves the host connected");
            Check(otherNetwork.State == WebSocketState.Open && await TryReadFrame(otherNetwork, TimeSpan.FromMilliseconds(500)) is null,
                "ban leaves players on another network alone");
            using (var rotated = await ConnectRaw(url, code!, RelayWire.NewSecret(), expectGreeting: false))
                Check((await ReadFrame(rotated)).Type == WebSocketMessageType.Close, "room ban survives identity rotation on the same address");
            var hostAgain = new RelayClient(hostSecret);
            await hostAgain.ConnectAsync(url, code!);
            Check(hostAgain.IsConnected, "a ban on the host's own network doesn't lock the host out");
            host = hostAgain;
            await host.ModerateAsync("unban", peerId);
            await Task.Delay(200);
            using var resumed = new RelayClient(peerSecret);
            await resumed.ConnectAsync(url, code!);
            Check(resumed.IsConnected, "unban permits reconnect with the same credential");

            var absentSecret = RelayWire.NewSecret();
            var roommateSecret = RelayWire.NewSecret();
            using var roommate = await ConnectRaw(url, code!, roommateSecret, address: "192.0.2.30");
            var laterNotice = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.PeersRemoved += ids => laterNotice.TrySetResult(ids);
            await host.ModerateAsync("ban", RelayWire.PeerId(absentSecret));
            await Task.Delay(200);
            Check(roommate.State == WebSocketState.Open && resumed.IsConnected, "banning an absent identity removes nobody yet");
            using (var absent = await ConnectRaw(url, code!, absentSecret, expectGreeting: false, address: "192.0.2.30"))
                Check((await ReadFrame(absent)).Type == WebSocketMessageType.Close, "ban issued while the target is away blocks its return");
            Check(await ClosedByRelay(roommate), "the returning banned player's network is cleared like a live ban");
            Check((await Within(laterNotice.Task, "later removal notice")).Contains(RelayWire.PeerId(roommateSecret)), "host told who that removed");
            Check(resumed.IsConnected, "players on other networks stay");
            await host.ModerateAsync("unban", RelayWire.PeerId(absentSecret));

            using (var impostor = await ConnectRaw(url, code!, RelayWire.NewSecret()))
            {
                await SendRaw(impostor, $$"""{"t":"relayControl","Operation":"kick","PeerId":"{{RelayWire.PeerId(peerSecret)}}"}""");
                await Task.Delay(300);
                Check(resumed.IsConnected, "only the host can moderate");
            }

            host.Dispose();
            resumed.Dispose();
            await Task.Delay(300);
            using var late = await ConnectRaw(url, code!, RelayWire.NewSecret(), expectGreeting: false);
            Check((await ReadFrame(late)).Type == WebSocketMessageType.Close, "empty room is gone");
            completed = true;
        }
        finally
        {
            if (!completed) Console.WriteLine(string.Join(Environment.NewLine, log.Lines));
        }
    }

    private static async Task<(WebSocket Socket, string Path, Dictionary<string, string> Headers)> AcceptSocket(TcpListener listener)
    {
        var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        var request = (await reader.ReadLineAsync())!.Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            var colon = line.IndexOf(':');
            headers[line[..colon]] = line[(colon + 1)..].Trim();
        }
        var accept = Convert.ToBase64String(SHA1.HashData(Bytes(headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        await stream.WriteAsync(Bytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"));
        return (WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30)), request[1], headers);
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

    private static async Task HttpFrontDoor()
    {
        var server = new RelayServer(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, new QuietLog());
        server.Start();
        var port = server.LocalEndPoint!.Port;
        var info = await RawHttp(port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n");
        Check(info.StartsWith("HTTP/1.1 200 ") && JsonDocument.Parse(info[(info.IndexOf("\r\n\r\n") + 4)..]).RootElement
            .GetProperty("requiresToken").GetBoolean() == false, "info served without a token requirement");
        Check((await RawHttp(port, "not http at all\r\n\r\n")).StartsWith("HTTP/1.1 400 "), "malformed request line refused");
        Check((await RawHttp(port, "GET /host HTTP/1.1\r\nHost: x\r\n\r\n")).StartsWith("HTTP/1.1 400 "), "plain GET to a WebSocket path refused");
        var oversized = await RawHttp(port, "GET /info HTTP/1.1\r\nX-Pad: " + new string('a', RelayHttp.MaxHeaderBytes) + "\r\n\r\n");
        Check(oversized == "" || oversized.StartsWith("HTTP/1.1 400 "), "oversized header block refused");
        Check((await RawHttp(port, "GET /admin/stats HTTP/1.1\r\nHost: x\r\n\r\n")).StartsWith("HTTP/1.1 404 "), "admin hidden without an admin token");

        using var host = new RelayClient(RelayWire.NewSecret());
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Disconnected += _ => disconnected.TrySetResult();
        Check(await host.ConnectAndHostAsync($"ws://127.0.0.1:{port}") != null, "hosts over the managed listener");
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!host.IsConnected, "stopping the relay drops its connections");
        using var late = new TcpClient();
        var refused = false;
        try { await late.ConnectAsync(IPAddress.Loopback, port); } catch (SocketException) { refused = true; }
        Check(refused, "stopped relay releases its port");
        await server.DisposeAsync();
    }

    private static async Task<ClientWebSocket> ConnectRaw(string url, string code, string secret, bool expectGreeting = true, string? address = null)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-AnoMech-Protocol", RelayWire.Version.ToString());
        socket.Options.SetRequestHeader("X-AnoMech-Peer-Secret", secret);
        if (address != null) socket.Options.SetRequestHeader("X-Test-Address", address);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri($"{url}/session/{code}"), timeout.Token);
        if (expectGreeting)
        {
            var frame = await ReadFrame(socket);
            Check(frame.Type == WebSocketMessageType.Text && JsonDocument.Parse(frame.Bytes).RootElement.GetProperty("peerId").GetGuid() == RelayWire.PeerId(secret), "greeting names the authenticated identity");
        }
        return socket;
    }

    private static Task SendRaw(ClientWebSocket socket, string text) => SendRaw(socket, Bytes(text), WebSocketMessageType.Text);

    private static async Task SendRaw(ClientWebSocket socket, byte[] body, WebSocketMessageType type)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(body, type, true, timeout.Token);
    }

    private static async Task<(byte[] Bytes, WebSocketMessageType Type)> ReadFrame(ClientWebSocket socket, TimeSpan? wait = null)
    {
        using var timeout = new CancellationTokenSource(wait ?? TimeSpan.FromSeconds(5));
        using var output = new MemoryStream(); var buffer = new byte[65536];
        WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer, timeout.Token); output.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
        return (output.ToArray(), result.MessageType);
    }

    private static async Task<(byte[] Bytes, WebSocketMessageType Type)?> TryReadFrame(ClientWebSocket socket, TimeSpan wait)
    {
        try { return await ReadFrame(socket, wait); }
        catch (OperationCanceledException) { return null; }
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

    private static async Task GreetingTests()
    {
        foreach (var mode in new[] { "current", "wrong version", "missing capabilities", "older relay build", "wrong identity", "timeout" })
        {
            var valid = mode == "current";
            var port = FreePort();
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            var secret = RelayWire.NewSecret();
            using var client = new RelayClient(secret);
            Exception? failure = null;
            client.Disconnected += e => failure = e;
            var connect = client.ConnectAndHostAsync($"ws://localhost:{port}");
            var accepted = await AcceptSocket(listener).WaitAsync(TimeSpan.FromSeconds(5));
            using var socket = accepted.Socket;
            listener.Stop();
            Check(accepted.Headers.GetValueOrDefault("X-AnoMech-Peer-Secret") == secret && accepted.Headers.GetValueOrDefault("X-AnoMech-Protocol") == RelayWire.Version.ToString(),
                $"{mode}: credential and protocol headers sent");
            if (mode != "timeout")
            {
                var capabilities = new[] { "binaryCompression", "authenticatedIdentity", "roomModeration" };
                var data = mode switch
                {
                    "older relay build" => JsonSerializer.SerializeToUtf8Bytes(new { relayVersion = RelayWire.Version, capabilities = new[] { "binaryCompression", "senderIdentity" }, sessionCode = "ABCDEF" }),
                    "wrong version" => JsonSerializer.SerializeToUtf8Bytes(new { relayVersion = RelayWire.Version + 1, capabilities, sessionCode = "ABCDEF", peerId = RelayWire.PeerId(secret) }),
                    "missing capabilities" => JsonSerializer.SerializeToUtf8Bytes(new { relayVersion = RelayWire.Version, capabilities = Array.Empty<string>(), sessionCode = "ABCDEF", peerId = RelayWire.PeerId(secret) }),
                    "wrong identity" => JsonSerializer.SerializeToUtf8Bytes(new { relayVersion = RelayWire.Version, capabilities, sessionCode = "ABCDEF", peerId = Guid.NewGuid() }),
                    _ => JsonSerializer.SerializeToUtf8Bytes(new { relayVersion = RelayWire.Version, capabilities, sessionCode = "ABCDEF", peerId = RelayWire.PeerId(secret) }),
                };
                await socket.SendAsync(data.AsMemory(0, 7), WebSocketMessageType.Text, false, CancellationToken.None);
                await socket.SendAsync(data.AsMemory(7), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            var code = await connect.WaitAsync(TimeSpan.FromSeconds(8));
            Check(client.IsConnected == valid && (code == "ABCDEF") == valid, valid ? "fragmented current greeting accepted" : $"{mode} greeting rejected");
            if (mode == "timeout") Check(failure is not null and not RelaySessionRejectedException, "slow greeting isn't treated as a refusal");
            else if (!valid) Check(failure is RelaySessionRejectedException, $"{mode} greeting is a terminal refusal");
        }
    }

    // A redirect must not carry the credential headers to a server the user never named.
    private static async Task RedirectTests()
    {
        foreach (var scheme in new[] { "ws", "http" })
        {
            var source = new TcpListener(IPAddress.Loopback, 0);
            var target = new TcpListener(IPAddress.Loopback, 0);
            source.Start(); target.Start();
            try
            {
                var sourcePort = ((IPEndPoint)source.LocalEndpoint).Port;
                var targetPort = ((IPEndPoint)target.LocalEndpoint).Port;
                using var client = new RelayClient(RelayWire.NewSecret());
                var connect = client.ConnectAndHostAsync($"ws://localhost:{sourcePort}");
                using (var accepted = await source.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    var stream = accepted.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                    await stream.WriteAsync(Bytes($"HTTP/1.1 302 Found\r\nLocation: {scheme}://localhost:{targetPort}/host\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
                }
                // The redirect, if followed at all, is only attempted once this connection closes.
                var settled = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(5))) == connect;
                await Task.Delay(500);
                Check(settled && !client.IsConnected && !target.Pending(), $"{scheme} redirect doesn't forward peer credentials");
            }
            finally { source.Stop(); target.Stop(); }
        }
    }

    private sealed class ProbeSocket : WebSocket
    {
        private int concurrent;
        public int PeakSends;
        public int Sends;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException();
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token)
        {
            var count = Interlocked.Increment(ref concurrent);
            PeakSends = Math.Max(count, PeakSends);
            await Task.Delay(5, token);
            Interlocked.Decrement(ref concurrent);
            Interlocked.Increment(ref Sends);
        }
    }
}
