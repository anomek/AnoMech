using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AnoMech.Multiplayer;
using AnoMech.Network;
using AnoMech.Relay;

namespace AnoMech.Tests;

// The real relay listening on this machine's loopback, every client on a real RelayClient over TCP
// as a player's is, in real time. A RelayClient calls back on its own threads, and Plugin's
// services are process-wide, so what MultiplayerManager handles through them (a disconnect, a
// connect finishing) waits for that client's next frame; messages go straight to its queue, as in
// game.
//
// Each client's connections run through a TCP hop here that fails as a player's link does
// (LinkFault, played as TestRelay plays it): a drop resets the client's end while the relay's end
// hears nothing more, and dialing fails until the outage is over; a dying direction carries
// nothing until the drop that follows it.
internal sealed class LiveRelay : ISessionNetwork, IDisposable
{
    private readonly RelayServer server = new(new RelayOptions { BindAddress = IPAddress.Loopback, Port = 0 }, new QuietLog());
    private readonly TcpListener hopListener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<Action>> frames = new();
    private readonly ConcurrentBag<RelayClient> clients = [];
    private readonly ConcurrentQueue<string> rejected = new();
    private readonly object gate = new();
    // By client: the n-th to connect.
    private readonly List<Guid> identities = [];
    private readonly Dictionary<Guid, LinkState> links = new();
    private readonly List<(double At, int Client, LinkFault Fault)> scheduled = [];
    private readonly List<Hop> hops = [];
    // The links that have their greeting, which the host's messages reach.
    private readonly List<Link> listening = [];
    private readonly Dictionary<Guid, long> lastSeq = new();
    private Guid hostIdentity;
    private double maxHostDelay;

    private sealed class LinkState
    {
        public double DownUntil = double.NegativeInfinity;
        public double UploadsDeadUntil = double.NegativeInfinity;
        public double DownloadsDeadUntil = double.NegativeInfinity;
    }

    public LiveRelay()
    {
        server.Start();
        hopListener.Start();
        Address = $"ws://127.0.0.1:{((IPEndPoint)hopListener.LocalEndpoint).Port}";
        _ = AcceptAsync();
    }

    public string Address { get; }
    public TimeProvider Time => TimeProvider.System;
    public double Now => clock.Elapsed.TotalSeconds;
    public IReadOnlyList<string> Rejected => [.. rejected, .. ISessionNetwork.RefusedBy(server)];
    public List<Carried> Sends { get; } = [];
    // When each client's link went down and when it next had its greeting.
    public List<(int Client, double Down, double Up)> Outages { get; } = [];

    public double MaxHostDelay
    {
        get { lock (gate) return maxHostDelay; }
    }

    public IRelayConnection Connect(string secret)
    {
        var client = new RelayClient(secret);
        clients.Add(client);
        var identity = RelayWire.PeerId(secret);
        lock (gate)
            if (!identities.Contains(identity)) identities.Add(identity);
        return new Link(this, client, identity, frames.GetOrAdd(identity, _ => new ConcurrentQueue<Action>()));
    }

    public void Schedule(int client, double at, LinkFault fault)
    {
        lock (gate) scheduled.Add((at, client, fault));
    }

    public void AdvanceTo(double time)
    {
        while (true)
        {
            double until;
            lock (gate)
            {
                foreach (var fault in scheduled.Where(s => s.At <= Now).ToList())
                {
                    scheduled.Remove(fault);
                    if (fault.Client < identities.Count) Apply(identities[fault.Client], fault.Fault);
                }
                until = scheduled.Select(s => s.At).Append(time).Min();
            }
            var now = Now;
            if (now >= time) return;
            var milliseconds = (int)((until - now) * 1000);
            if (milliseconds > 1) Thread.Sleep(milliseconds - 1);
            else Thread.Yield();
        }
    }

    public void RaiseLinkEvents(Guid identity)
    {
        if (!frames.TryGetValue(identity, out var frame)) return;
        while (frame.TryDequeue(out var call)) call();
    }

    public void Dispose()
    {
        foreach (var client in clients) client.Dispose();
        server.StopAsync().GetAwaiter().GetResult();
        stopping.Cancel();
        hopListener.Stop();
        lock (gate)
            foreach (var hop in hops) hop.Close();
    }

    private LinkState LinkOf(Guid identity)
    {
        if (!links.TryGetValue(identity, out var link)) links[identity] = link = new LinkState();
        return link;
    }

    // As TestRelay applies a fault, under the gate.
    private void Apply(Guid identity, LinkFault fault)
    {
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
        }
        if (hops.LastOrDefault(h => h.Identity == identity && !h.Dropped && !h.Ended) is not { } live) return;
        link.DownUntil = Now + fault.Outage;
        live.Drop();
        Outages.Add((identities.IndexOf(identity), Now, double.NaN));
    }

    private bool Dying(Guid identity, bool up)
    {
        lock (gate)
        {
            var link = LinkOf(identity);
            return Now < (up ? link.UploadsDeadUntil : link.DownloadsDeadUntil);
        }
    }

    private void Record(Guid identity, MpMessage message)
    {
        lock (gate)
        {
            var last = lastSeq.GetValueOrDefault(identity);
            Sends.Add(new Carried(identities.IndexOf(identity), Now, message, message.Seq != 0 && message.Seq <= last));
            if (message.Seq > last) lastSeq[identity] = message.Seq;
            if (identity != hostIdentity) return;
            var json = JsonSerializer.Serialize(message);
            foreach (var link in listening)
                if (link.Identity != hostIdentity) link.Await(json, Now);
        }
    }

    private void HostMessageReached(Link link, MpMessage message)
    {
        if (link.SentAt(JsonSerializer.Serialize(message)) is not { } sentAt) return;
        lock (gate) maxHostDelay = Math.Max(maxHostDelay, Now - sentAt);
    }

    private void Listen(Link link)
    {
        lock (gate)
        {
            listening.Add(link);
            var client = identities.IndexOf(link.Identity);
            var last = Outages.FindLastIndex(o => o.Client == client);
            if (last >= 0 && double.IsNaN(Outages[last].Up)) Outages[last] = Outages[last] with { Up = Now };
        }
    }

    // What the host's lost link carried never arrives.
    private void Deafen(Link link)
    {
        lock (gate)
        {
            listening.Remove(link);
            if (link.Identity != hostIdentity) return;
            foreach (var other in listening) other.ForgetAwaited();
        }
    }

    private async Task AcceptAsync()
    {
        while (true)
        {
            Socket player;
            try { player = await hopListener.AcceptSocketAsync(stopping.Token); }
            catch (Exception) { return; }
            _ = CarryAsync(player);
        }
    }

    // The client's upgrade request says who it is; a client whose link is down is reset at once.
    private async Task CarryAsync(Socket player)
    {
        player.NoDelay = true;
        var head = await ReadHeadAsync(player);
        Hop? hop = null;
        lock (gate)
            if (head != null && IdentityIn(head) is { } identity && Now >= LinkOf(identity).DownUntil)
                hops.Add(hop = new Hop(this, identity, player));
        if (hop == null)
        {
            Reset(player);
            return;
        }
        await hop.CarryAsync(head!, server.LocalEndPoint!.Port);
    }

    private async Task<byte[]?> ReadHeadAsync(Socket player)
    {
        var buffer = new byte[16 * 1024];
        var length = 0;
        try
        {
            while (length < buffer.Length)
            {
                var read = await player.ReceiveAsync(buffer.AsMemory(length), SocketFlags.None, stopping.Token);
                if (read == 0) return null;
                length += read;
                if (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) >= 0) return buffer[..length];
            }
        }
        catch (Exception)
        {
            // Gone before it asked for anything.
        }
        return null;
    }

    private static Guid? IdentityIn(byte[] head)
    {
        foreach (var line in Encoding.ASCII.GetString(head).Split("\r\n"))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || !line[..colon].Trim().Equals("X-AnoMech-Peer-Secret", StringComparison.OrdinalIgnoreCase)) continue;
            var secret = line[(colon + 1)..].Trim();
            return RelayWire.IsValidSecret(secret) ? RelayWire.PeerId(secret) : null;
        }
        return null;
    }

    private static void Reset(Socket socket)
    {
        try
        {
            socket.LingerState = new LingerOption(true, 0);
            socket.Close();
        }
        catch (Exception)
        {
            // Already gone.
        }
    }

    // One of a client's connections, between its end and the relay's.
    private sealed class Hop(LiveRelay relay, Guid identity, Socket player)
    {
        private readonly Socket relayEnd = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        public Guid Identity => identity;
        public volatile bool Dropped;
        // Nothing left to carry either way.
        public volatile bool Ended;

        public async Task CarryAsync(byte[] head, int relayPort)
        {
            try
            {
                await relayEnd.ConnectAsync(IPAddress.Loopback, relayPort, relay.stopping.Token);
                await relayEnd.SendAsync(head, SocketFlags.None, relay.stopping.Token);
                await Task.WhenAll(PumpAsync(player, relayEnd, up: true), PumpAsync(relayEnd, player, up: false));
            }
            catch (Exception)
            {
                Reset(player);
            }
            finally
            {
                Ended = true;
            }
        }

        public void Drop()
        {
            Dropped = true;
            Reset(player);
        }

        public void Close()
        {
            Reset(player);
            Reset(relayEnd);
        }

        private async Task PumpAsync(Socket from, Socket to, bool up)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (true)
                {
                    var read = await from.ReceiveAsync(buffer, SocketFlags.None, relay.stopping.Token);
                    while (!Dropped && relay.Dying(identity, up)) await Task.Delay(5, relay.stopping.Token);
                    if (Dropped) return;
                    if (read == 0)
                    {
                        to.Shutdown(SocketShutdown.Send);
                        return;
                    }
                    await to.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, relay.stopping.Token);
                }
            }
            catch (Exception)
            {
                // An end reset or closed: the other hears of it, unless the link dropped first.
                if (!Dropped) Reset(to);
            }
        }
    }

    private sealed class Link : IRelayConnection
    {
        private readonly LiveRelay relay;
        private readonly RelayClient client;
        private readonly ConcurrentQueue<Action> frame;
        // What the host sent while this link listened, by what it says, oldest first.
        private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> awaited = new();

        public Link(LiveRelay relay, RelayClient client, Guid identity, ConcurrentQueue<Action> frame)
        {
            this.relay = relay;
            this.client = client;
            Identity = identity;
            this.frame = frame;
            client.MessageReceived += (message, isFromHost, senderId, sender) =>
            {
                if (isFromHost) relay.HostMessageReached(this, message);
                MessageReceived?.Invoke(message, isFromHost, senderId, sender);
            };
            client.PeersRemoved += removed => PeersRemoved?.Invoke(removed);
            client.MessageRejected += (sender, isFromHost, reason) => relay.rejected.Enqueue($"a message from {sender}: {reason}");
            client.Disconnected += failure =>
            {
                relay.Deafen(this);
                frame.Enqueue(() => Disconnected?.Invoke(failure));
            };
        }

        public Guid Identity { get; }
        public bool IsConnected => client.IsConnected;
        public bool IsEncrypted => client.IsEncrypted;
        public bool FellBackToUnencrypted => client.FellBackToUnencrypted;
        public bool SupportsCompression => client.SupportsCompression;
        public bool SupportsSenderIdentity => client.SupportsSenderIdentity;

        public event Action<MpMessage, bool, uint, Guid>? MessageReceived;
        public event Action<IReadOnlyList<Guid>>? PeersRemoved;
        public event Action<Exception?>? Disconnected;

        public void Await(string json, double at) => awaited.GetOrAdd(json, _ => new ConcurrentQueue<double>()).Enqueue(at);

        public double? SentAt(string json) => awaited.TryGetValue(json, out var sends) && sends.TryDequeue(out var at) ? at : null;

        public void ForgetAwaited() => awaited.Clear();

        public Task ConnectAsync(string relayUrl, string sessionCode, string? accessToken = null)
            => InFrame(client.ConnectAsync(relayUrl, sessionCode, accessToken));

        public Task<string?> ConnectAndHostAsync(string relayUrl, string? accessToken = null)
        {
            relay.hostIdentity = Identity;
            return InFrame(client.ConnectAndHostAsync(relayUrl, accessToken));
        }

        public Task SendAsync(MpMessage message)
        {
            relay.Record(Identity, message);
            return client.SendAsync(message);
        }

        public Task ModerateAsync(string operation, Guid peerId) => client.ModerateAsync(operation, peerId);

        public void Dispose()
        {
            relay.Deafen(this);
            client.Dispose();
        }

        private Task InFrame(Task task)
        {
            var done = new TaskCompletionSource();
            task.ContinueWith(finished =>
            {
                // A connect that fails raises Disconnected and returns.
                if (finished.Exception is null && client.IsConnected) relay.Listen(this);
                frame.Enqueue(() =>
                {
                    if (finished.Exception is { } failure) done.TrySetException(failure.InnerExceptions);
                    else done.TrySetResult();
                });
            }, TaskScheduler.Default);
            return done.Task;
        }

        private Task<T> InFrame<T>(Task<T> task)
        {
            var done = new TaskCompletionSource<T>();
            task.ContinueWith(finished =>
            {
                // A connect that fails raises Disconnected and returns.
                if (finished.Exception is null && client.IsConnected) relay.Listen(this);
                frame.Enqueue(() =>
                {
                    if (finished.Exception is { } failure) done.TrySetException(failure.InnerExceptions);
                    else done.TrySetResult(finished.Result);
                });
            }, TaskScheduler.Default);
            return done.Task;
        }
    }
}
