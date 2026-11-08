using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using AnoMech.Multiplayer;
using AnoMech.Network;
using AnoMech.Relay;

namespace AnoMech.Tests;

// What a client's link can suffer: it drops, with all it carries, and reconnecting fails for
// Outage seconds; or its uploads (downloads) vanish for Dead seconds first, as a link does before
// anyone notices it's gone; or its uploads hold for Stall seconds and then go on in order, as
// behind a lost packet.
internal abstract record LinkFault(double Outage);
internal sealed record LinkDrops(double Outage) : LinkFault(Outage);
internal sealed record UploadsDie(double Dead, double Outage) : LinkFault(Outage);
internal sealed record DownloadsDie(double Dead, double Outage) : LinkFault(Outage);
internal sealed record UploadsStall(double Stall) : LinkFault(0);

// A message a client handed its link, From being the n-th client to connect. Resent: a numbered
// message that was on the wire before.
internal sealed record Carried(int From, double At, MpMessage Message, bool Resent)
{
    public string Kind => Message.GetType().Name + (Resent ? " sent again" : "");
}

// The real relay (RelayServer) and each client's link to it, in virtual time. Every connection is
// a Wire: the relay serves its end exactly as a connection it accepted on its port, and the
// client's end speaks the relay's protocol through RelayClient's own upgrade headers, greeting,
// receive loop and encoding. A client's upload is RelayClient's: one queue in order, one message on
// the socket at a time. A link dropping is told to its client during that client's own frame, as
// the real client hears it on its own; the relay hears nothing of it.
internal sealed class TestRelay : ISessionNetwork
{
    public string Address => "test-relay";
    private const double StepSeconds = 0.0005;

    // An unreachable rate or interval, for a link that never limits anything.
    private const double Never = 1e12;

    public static readonly ConnectionProfile Perfect = new("perfect link",
        UploadRate: Never, SendBufferBytes: int.MaxValue / 2, UploadLatency: 0,
        UploadStallEvery: Never, UploadStallMin: 0, UploadStallMax: 0,
        DownloadRate: Never, DownloadLatency: 0, DownloadJitter: 0,
        DownloadStallEvery: Never, DownloadStallMin: 0, DownloadStallMax: 0,
        PeerHitchEvery: Never, PeerHitchMin: 0, PeerHitchMax: 0);

    private readonly int seed;
    private readonly Func<int, ConnectionProfile> linkFor;
    private readonly double step;
    private readonly VirtualClock clock = new();
    private readonly RelayServer server;
    private readonly List<Connection> connections = [];
    private readonly List<Guid> identities = [];
    private readonly Dictionary<Guid, LinkState> links = new();
    private readonly Dictionary<int, int> networks = new();
    private readonly List<string> rejected = [];
    // Calls made off the test's thread, taken at the next step.
    private readonly ConcurrentQueue<Action> posted = new();
    // By client: the n-th to connect, as linkFor numbers them.
    private readonly List<(double At, int Client, LinkFault Fault)> scheduled = [];
    private readonly List<(int Client, Func<Carried, bool> Match, LinkFault Fault)> afterSending = [];
    private readonly List<(int Client, Func<Carried, bool> Match, LinkFault Fault)> beforeReceiving = [];
    private readonly Dictionary<Guid, long> lastSeq = new();
    private object? relayRead;
    private long tick;
    private int connectionsMade;

    private sealed class LinkState
    {
        public double DownUntil = double.NegativeInfinity;
        public double UploadsDeadUntil = double.NegativeInfinity;
        public double DownloadsDeadUntil = double.NegativeInfinity;
        public double UploadsStalledUntil = double.NegativeInfinity;
    }

    // step: how far the links move at a time, in seconds.
    public TestRelay(int seed, Func<int, ConnectionProfile> linkFor, double step = StepSeconds)
    {
        this.seed = seed;
        this.linkFor = linkFor;
        this.step = step;
        server = new RelayServer(new RelayOptions(), new Unlogged(), clock, keepAlive: TimeSpan.Zero);
        server.StartUnbound();
    }

    public double Now => clock.Now;
    public TimeProvider Time => clock;
    public IReadOnlyList<string> Rejected => [.. rejected, .. ISessionNetwork.RefusedBy(server)];
    // The longest a host's message took from its send to a peer's queue.
    public double MaxHostDelay { get; private set; }
    // When each client's link went down and came back.
    public List<(int Client, double Down, double Up)> Outages { get; } = [];
    // Everything the clients handed their links, and what reached each, in order.
    public List<Carried> Sends { get; } = [];
    public List<(int To, Carried Carried)> Delivered { get; } = [];
    public int FaultsApplied { get; private set; }
    public int TriggersPending => afterSending.Count + beforeReceiving.Count;

    // MultiplayerManager.RelayFactory: the n-th client to connect gets linkFor(n), its
    // reconnections included.
    public IRelayConnection Connect(string secret)
    {
        var identity = RelayWire.PeerId(secret);
        if (!identities.Contains(identity)) identities.Add(identity);
        var client = identities.IndexOf(identity);
        var connection = new Connection(this, secret, identity, client, linkFor(client), seed + 1000 * connectionsMade++);
        connections.Add(connection);
        return connection;
    }

    // These clients reach the relay from one address, as a household behind one router does.
    public void SameNetwork(params int[] clients)
    {
        foreach (var client in clients) networks[client] = clients[0];
    }

    private IPAddress AddressOf(int client)
    {
        var network = networks.GetValueOrDefault(client, client);
        return new IPAddress([10, 0, (byte)(network / 250), (byte)(network % 250 + 1)]);
    }

    public void Schedule(int client, double at, LinkFault fault) => scheduled.Add((at, client, fault));

    // The client's link drops right after it sends the first message `match` takes, which goes
    // with it.
    public void DropAfterSending(int client, Func<Carried, bool> match, LinkFault fault) => afterSending.Add((client, match, fault));

    // The client's link drops right as the first message `match` takes reaches it, which is lost.
    public void DropBeforeReceiving(int client, Func<Carried, bool> match, LinkFault fault) => beforeReceiving.Add((client, match, fault));

    // The client's uploads stall right after it sends the first message `match` takes, which
    // waits with them.
    public void StallAfterSending(int client, Func<Carried, bool> match, double seconds) => afterSending.Add((client, match, new UploadsStall(seconds)));

    public void RaiseLinkEvents(Guid identity)
    {
        for (var i = 0; i < connections.Count; i++)
            if (connections[i].Identity == identity) connections[i].Tell();
    }

    public void AdvanceTo(double time)
    {
        while (Now < time)
        {
            while (posted.TryDequeue(out var call)) call();
            relayRead = null;
            if (scheduled.Count > 0)
                foreach (var fault in scheduled.Where(s => s.At <= Now).ToList())
                {
                    scheduled.Remove(fault);
                    if (fault.Client < identities.Count) Apply(identities[fault.Client], fault.Fault);
                }
            for (var i = 0; i < connections.Count; i++) connections[i].Step();
            connections.RemoveAll(c => c.Finished);
            clock.AdvanceTo(++tick * step);
        }
    }

    private LinkState LinkOf(Guid identity)
    {
        if (!links.TryGetValue(identity, out var link)) links[identity] = link = new LinkState();
        return link;
    }

    private void Apply(Guid identity, LinkFault fault)
    {
        FaultsApplied++;
        var link = LinkOf(identity);
        switch (fault)
        {
            case UploadsDie dies:
                link.UploadsDeadUntil = Now + dies.Dead;
                scheduled.Add((Now + dies.Dead, identities.IndexOf(identity), new LinkDrops(dies.Outage)));
                return;
            case DownloadsDie dies:
                link.DownloadsDeadUntil = Now + dies.Dead;
                scheduled.Add((Now + dies.Dead, identities.IndexOf(identity), new LinkDrops(dies.Outage)));
                return;
            case UploadsStall stall:
                link.UploadsStalledUntil = Now + stall.Stall;
                return;
        }
        var live = connections.FirstOrDefault(c => c.Identity == identity && c.Live);
        if (live == null) return;
        link.DownUntil = Now + fault.Outage;
        live.Drop();
        Outages.Add((identities.IndexOf(identity), Now, double.NaN));
    }

    private void Joined(Connection connection)
    {
        for (var i = Outages.Count - 1; i >= 0; i--)
            if (Outages[i].Client == connection.Client && double.IsNaN(Outages[i].Up))
            {
                Outages[i] = Outages[i] with { Up = Now };
                break;
            }
    }

    private Carried Record(Connection sender, MpMessage message)
    {
        var last = lastSeq.GetValueOrDefault(sender.Identity);
        var carried = new Carried(sender.Client, Now, message, message.Seq != 0 && message.Seq <= last);
        if (message.Seq > last) lastSeq[sender.Identity] = message.Seq;
        Sends.Add(carried);
        return carried;
    }

    private void Sent(Connection sender, Carried carried)
    {
        var trigger = afterSending.FirstOrDefault(t => t.Client == sender.Client && t.Match(carried));
        if (trigger.Match == null) return;
        afterSending.Remove(trigger);
        Apply(sender.Identity, trigger.Fault);
    }

    // True when the delivery was the trigger, and is lost with the link.
    private bool Receiving(Connection recipient, Carried carried)
    {
        var trigger = beforeReceiving.FirstOrDefault(t => t.Client == recipient.Client && t.Match(carried));
        if (trigger.Match == null) return false;
        beforeReceiving.Remove(trigger);
        Apply(recipient.Identity, trigger.Fault);
        return true;
    }

    private Wire NewWire(ConnectionProfile link, int wireSeed, Guid identity) => new(link, wireSeed, () => Now,
        at => at < LinkOf(identity).UploadsDeadUntil, at => at < LinkOf(identity).DownloadsDeadUntil,
        at => at < LinkOf(identity).UploadsStalledUntil, read => relayRead = read, () => relayRead);

    // A long session logs a line per message it carries; what the tests need of the relay comes
    // from its stats.
    private sealed class Unlogged : IRelayLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Detail(string message) { }
    }

    // A message on its way: Carried is null for a relayControl, which isn't an MpMessage.
    private sealed class Outgoing(Carried? carried, byte[] body, WebSocketMessageType type, double sentAt)
    {
        public readonly Carried? Carried = carried;
        public readonly byte[] Body = body;
        public readonly WebSocketMessageType Type = type;
        public readonly double SentAt = sentAt;
        public readonly TaskCompletionSource Completion = new();
    }

    private sealed class Connection : IRelayConnection
    {
        private readonly TestRelay relay;
        private readonly string secret;
        private readonly ConnectionProfile link;
        private readonly Random race, keys;
        private readonly Wire wire;
        private readonly Queue<Outgoing> queued = new();
        private readonly Queue<Action> told = new();
        private readonly CancellationTokenSource receiving = new();
        private Outgoing? writing;
        private bool sending, dialing, open, gone, closed;
        private WebSocket? socket;
        private TaskCompletionSource<string?>? dialed;
        private IReadOnlySet<string> capabilities = new HashSet<string>();

        public readonly Guid Identity;
        public readonly int Client;

        public Connection(TestRelay relay, string secret, Guid identity, int client, ConnectionProfile link, int seed)
        {
            this.relay = relay;
            this.secret = secret;
            this.link = link;
            Identity = identity;
            Client = client;
            race = new Random(seed);
            keys = new Random(seed + 5);
            wire = relay.NewWire(link, seed, identity);
        }

        public bool Live => dialing || open;
        // Nothing left to carry or to tell.
        public bool Finished => gone && told.Count == 0 && wire.Idle;
        public bool IsConnected => open;
        public bool IsEncrypted => false;
        public bool FellBackToUnencrypted => false;
        public bool SupportsCompression => capabilities.Contains("binaryCompression");
        public bool SupportsSenderIdentity => capabilities.Contains("authenticatedIdentity");

        public event Action<MpMessage, bool, uint, Guid>? MessageReceived;
        public event Action<IReadOnlyList<Guid>>? PeersRemoved;
        public event Action<Exception?>? Disconnected;

        public Task ConnectAsync(string relayUrl, string sessionCode, string? accessToken = null)
            => Dial(RelayClient.JoinPath(sessionCode), hosting: false, accessToken);

        public Task<string?> ConnectAndHostAsync(string relayUrl, string? accessToken = null)
            => Dial(RelayClient.HostPath, hosting: true, accessToken);

        // Fails at once while this identity's outage lasts.
        private Task<string?> Dial(string path, bool hosting, string? accessToken)
        {
            dialed = new TaskCompletionSource<string?>();
            if (relay.Now < relay.LinkOf(Identity).DownUntil)
            {
                gone = true;
                wire.Cut();
                var unreachable = new WebSocketException(WebSocketError.Faulted, "Unable to connect to the remote server.");
                Later(() => Disconnected?.Invoke(unreachable), () => dialed.TrySetResult(null));
                return dialed.Task;
            }
            dialing = true;
            _ = relay.server.ServeAsync(wire.RelayEnd, relay.AddressOf(Client));
            _ = HandshakeAsync(path, hosting, accessToken);
            return dialed.Task;
        }

        // As ClientWebSocket and RelayClient connect: the upgrade request, the 101, the greeting.
        private async Task HandshakeAsync(string path, bool hosting, string? accessToken)
        {
            try
            {
                var keyBytes = new byte[16];
                keys.NextBytes(keyBytes);
                var key = Convert.ToBase64String(keyBytes);
                var request = new StringBuilder($"GET /{path} HTTP/1.1\r\nHost: {relay.Address}\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n"
                                                + $"Sec-WebSocket-Version: 13\r\nSec-WebSocket-Key: {key}\r\n");
                foreach (var (name, value) in RelayClient.UpgradeHeaders(secret, accessToken)) request.Append($"{name}: {value}\r\n");
                request.Append("\r\n");
                await wire.ClientEnd.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()));
                await ReadUpgradeAsync(key);
                socket = WebSocket.CreateFromStream(wire.ClientEnd, new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = TimeSpan.Zero });
                using var greeting = new CancellationTokenSource(RelayClient.DefaultGreetingTimeout, relay.Time);
                var (code, relayCapabilities) = await RelayClient.ReadGreetingAsync(socket, Identity, hosting, greeting.Token);
                if (gone) return;
                capabilities = relayCapabilities;
                dialing = false;
                open = true;
                relay.Joined(this);
                _ = ReceiveAsync(socket);
                Later(() => dialed!.TrySetResult(code));
            }
            catch (Exception e)
            {
                if (gone) return;
                dialing = false;
                gone = true;
                if (socket != null) socket.Abort();
                else wire.ClientEnd.Dispose();
                Later(() => Disconnected?.Invoke(e), () => dialed!.TrySetResult(null));
            }
        }

        private async Task ReadUpgradeAsync(string key)
        {
            var head = new MemoryStream();
            var buffer = new byte[1024];
            while (!head.ToArray().AsSpan().EndsWith("\r\n\r\n"u8))
            {
                var read = await wire.ClientEnd.ReadAsync(buffer);
                if (read == 0) throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The relay closed the connection during the upgrade.");
                head.Write(buffer, 0, read);
            }
            var lines = Encoding.ASCII.GetString(head.ToArray()).Split("\r\n");
            var status = lines[0].Split(' ') is [_, var code, ..] && int.TryParse(code, out var parsed) ? parsed : 0;
            if (status != 101)
                throw new WebSocketException(WebSocketError.NotAWebSocket, $"The server returned status code '{status}' when status code '101' was expected.");
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            if (!lines.Contains($"Sec-WebSocket-Accept: {accept}"))
                throw new WebSocketException(WebSocketError.HeaderError, "The relay's upgrade answered another key.");
        }

        private async Task ReceiveAsync(WebSocket connected)
        {
            var failure = await RelayClient.ReceiveAsync(connected, receiving.Token, Received, removed => PeersRemoved?.Invoke(removed), Rejected, relay.Time);
            if (gone) return;
            open = false;
            gone = true;
            Later(() => Disconnected?.Invoke(failure));
        }

        private void Received(MpMessage message, bool isFromHost, uint senderId, Guid sender)
        {
            if (wire.ClientEnd.Tag is Outgoing { Carried: { } carried } original)
            {
                if (relay.Receiving(this, carried)) return;
                relay.Delivered.Add((Client, carried));
                if (isFromHost) relay.MaxHostDelay = Math.Max(relay.MaxHostDelay, relay.Now - original.SentAt);
            }
            MessageReceived?.Invoke(message, isFromHost, senderId, sender);
        }

        private void Rejected(Guid sender, bool isFromHost, string reason)
            => relay.rejected.Add($"{(wire.ClientEnd.Tag as Outgoing)?.Carried?.Message.GetType().Name ?? "a message"} from {sender}: {reason}");

        public Task SendAsync(MpMessage message)
        {
            if (!open) return Task.CompletedTask;
            var carried = relay.Record(this, message);
            var (body, type) = RelayClient.Encode(message, SupportsCompression);
            var outgoing = new Outgoing(carried, body, type, relay.Now);
            Queue(outgoing);
            relay.Sent(this, carried);
            return outgoing.Completion.Task;
        }

        public Task ModerateAsync(string operation, Guid peerId)
        {
            if (!open) return Task.CompletedTask;
            var outgoing = new Outgoing(null, RelayClient.ControlBody(operation, peerId), WebSocketMessageType.Text, relay.Now);
            Queue(outgoing);
            return outgoing.Completion.Task;
        }

        // The send loop wakes on the first message queued, and races the rest of the frame's sends.
        private void Queue(Outgoing outgoing)
        {
            if (writing == null && queued.Count == 0 && race.NextDouble() >= link.SendLoopLate) writing = outgoing;
            else queued.Enqueue(outgoing);
        }

        public void Step()
        {
            Send();
            wire.Step(relay.step);
        }

        // One message on the socket at a time, each done once the socket buffer has all of it.
        private void Send()
        {
            while (open && !sending)
            {
                writing ??= queued.Count > 0 ? queued.Dequeue() : null;
                if (writing is not { } next) return;
                sending = true;
                wire.ClientEnd.Sending = next;
                var sent = socket!.SendAsync(next.Body, next.Type, endOfMessage: true, CancellationToken.None);
                wire.ClientEnd.Sending = null;
                if (!sent.IsCompleted)
                {
                    sent.ContinueWith(done =>
                    {
                        Sent(next, done);
                        Send();
                    }, TaskContinuationOptions.ExecuteSynchronously);
                    return;
                }
                Sent(next, sent);
            }
        }

        private void Sent(Outgoing outgoing, Task done)
        {
            sending = false;
            if (writing == outgoing) writing = null;
            if (done.IsCompletedSuccessfully) outgoing.Completion.TrySetResult();
            else outgoing.Completion.TrySetException(done.Exception?.InnerException ?? new ObjectDisposedException(nameof(RelayClient)));
        }

        private void FailSends()
        {
            if (!sending && writing != null) writing.Completion.TrySetException(new ObjectDisposedException(nameof(RelayClient)));
            foreach (var waiting in queued) waiting.Completion.TrySetException(new ObjectDisposedException(nameof(RelayClient)));
            queued.Clear();
            if (!sending) writing = null;
        }

        // Everything on the link goes with it; a send still waiting for the socket fails, as
        // RelayClient's do once it's disposed. Told as RelayClient's receive loop ends with it.
        public void Drop()
        {
            if (gone) return;
            var wasDialing = dialing;
            gone = true;
            dialing = false;
            open = false;
            receiving.Cancel();
            socket?.Abort();
            wire.Cut();
            FailSends();
            if (wasDialing)
            {
                var lost = new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The link went down while connecting.");
                Later(() => Disconnected?.Invoke(lost), () => dialed!.TrySetResult(null));
            }
            else Later(() => Disconnected?.Invoke(null));
        }

        // As RelayClient's: queued sends fail, the socket goes with what it already holds. Taken
        // on the test's thread, since MultiplayerManager may dispose from a send's continuation.
        public void Dispose() => relay.posted.Enqueue(Close);

        private void Close()
        {
            if (closed) return;
            closed = true;
            var wasDialing = dialing && !gone;
            gone = true;
            dialing = false;
            open = false;
            FailSends();
            receiving.Cancel();
            if (socket != null) socket.Abort();
            else wire.ClientEnd.Dispose();
            if (wasDialing) Later(() => dialed!.TrySetResult(null));
        }

        // Raised within this client's next frame, in order.
        private void Later(params Action[] calls)
        {
            foreach (var call in calls) told.Enqueue(call);
        }

        public void Tell()
        {
            while (told.TryDequeue(out var call)) call();
        }
    }
}
