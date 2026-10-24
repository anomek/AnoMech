using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Network;

namespace AnoMech.Tests;

// SessionStream's promise, whatever happens to the links: every numbered message the host or a
// peer sends is taken by the other end exactly once and in order, and the logs that make that
// possible empty once everyone has caught up. The host and its peers talk through the real relay
// (TestRelay), and each end drives its stream as MultiplayerManager does (Hello on every connect,
// the ping cycle, gap requests, resends). Each seed draws its own traffic and its own
// interruptions: a link dropping and reconnecting on the real backoff, an upload or a download
// dying before the link does, an inbound queue overflowing, a peer leaving and joining again -- on
// either end, at any moment, several at once. An end is only faulted once it has been back a
// while, so someone is always heard within the relay's idle timeout, as in any live session. No
// case names a message or a moment, so anything sent through the stream later is covered as it is.
public class SessionStreamTests
{
    private static IEnumerable<TestCaseData> Seeds() =>
        Enumerable.Range(0, 500).Select(seed => new TestCaseData(seed).SetArgDisplayNames($"seed {seed}"));

    [TestCaseSource(nameof(Seeds))]
    public void EveryNumberedMessageIsTakenOnceAndInOrderWhateverTheLinksDo(int seed)
    {
        var net = new StreamNet(seed);
        net.Run();
        foreach (var line in net.Log.TakeLast(30)) TestContext.Out.WriteLine(line);
        Assert.That(net.Problems(), Is.Empty);
    }

    private sealed class StreamNet
    {
        // MultiplayerManager's own: PingIntervalSeconds, HelloRetrySeconds, ReconnectBackoff.
        private const long PingIntervalMs = 2000;
        private const long HelloRetryMs = 2000;
        private static readonly long[] BackoffMs = [2000, 4000, 8000, 15000];
        private const long StepMs = 5;
        private const long SettleMs = 60_000;
        // A fault keeps an end away for at most its outage and one backoff; this long back first,
        // the host's pings keep the room heard.
        private const long SteadyMs = 3000;

        private readonly Random rng;
        private readonly TestRelay relay;
        private readonly List<End> ends = [];
        // By the order the ends first connect, as TestRelay numbers its clients.
        private readonly List<End> linked = [];
        private readonly List<string> problems = [];
        private readonly long activeMs;
        private string? code;
        private long now;
        private long nextPing;

        public List<string> Log { get; } = [];

        private End Host => ends[0];

        private sealed class End(string secret, bool isHost, ConnectionProfile link)
        {
            public readonly string Secret = secret;
            public readonly Guid Id = RelayWire.PeerId(secret);
            public readonly bool IsHost = isHost;
            public readonly ConnectionProfile Link = link;
            public SessionStream Stream = null!;
            public IRelayConnection? Connection;
            public bool Member = true;
            public bool Connected, Dialing, Reconnecting;
            public long ConnectedAt;
            public long? ReconnectAt;
            public int ReconnectAttempt;
            public long? LeaveAt, JoinAt;
            public long InboundDropUntil = -1;
            public bool HelloAcknowledged;
            public long NextHello;
            public long Baseline = -1;
            public double Rate;
            public long NextFault = long.MaxValue;
            public readonly Queue<(MpMessage Message, Guid From)> Inbox = new();
            // This stream's numbered messages, and what it took from each other end's current one.
            public readonly List<MpMessage> Sent = [];
            public readonly Dictionary<Guid, List<MpMessage>> Taken = new();
            public readonly Dictionary<Guid, long> EpochTaken = new();
        }

        public StreamNet(int seed)
        {
            rng = new Random(seed);
            relay = new TestRelay(seed, client => linked[client].Link, StepMs / 1000.0);
            activeMs = rng.Next(40_000, 120_000);
            var peers = rng.Next(1, 8);
            for (var i = 0; i <= peers; i++)
            {
                var secret = new byte[32];
                rng.NextBytes(secret);
                var link = TestRelay.Perfect with
                {
                    Name = "", UploadLatency = rng.Next(1, 150) / 1000.0, DownloadLatency = rng.Next(1, 150) / 1000.0, DownloadJitter = 0.02,
                };
                var end = new End(Convert.ToHexString(secret), i == 0, link);
                end.Stream = NewStream(end);
                end.Rate = NextRate(end);
                end.NextFault = NextFaultAt(end);
                ends.Add(end);
            }
            // The host first; each peer joins a moment later.
            Connect(Host);
            foreach (var peer in ends.Skip(1)) peer.JoinAt = rng.Next(0, 3000);
        }

        private SessionStream NewStream(End end) => new(end.Id, end.IsHost, rng.NextInt64(1, long.MaxValue), () => now);

        private double NextRate(End end) => rng.NextDouble() < 0.3 ? 0 : end.IsHost ? rng.Next(1, 61) : rng.Next(0, 6);

        private long NextFaultAt(End end) => now + (long)(-Math.Log(1 - rng.NextDouble()) * rng.Next(6_000, 20_000));

        public void Run()
        {
            var until = activeMs + SettleMs;
            while (now < until)
            {
                var active = now < activeMs;
                foreach (var end in ends) TickEnd(end, active);
                if (Host.Connected && now >= nextPing)
                {
                    nextPing = now + PingIntervalMs;
                    var (sent, received) = Host.Stream.PingFields();
                    Transmit(Host, new PingMessage(now, sent, received));
                }
                if (!active && CaughtUp()) break;
                relay.AdvanceTo(relay.Now + StepMs / 1000.0);
                now = (long)Math.Round(relay.Now * 1000);
            }
        }

        private void TickEnd(End end, bool active)
        {
            relay.RaiseLinkEvents(end.Id);
            if (end.JoinAt is { } joinAt && now >= joinAt && code != null) Join(end);
            if (!end.Member) return;
            // MultiplayerManager's drain, which an overflowing inbound queue had already dropped.
            while (end.Inbox.TryDequeue(out var received))
                if (now >= end.InboundDropUntil && ends.FirstOrDefault(e => e.Id == received.From) is { } from)
                    Handle(end, from, received.Message);
            if (end.LeaveAt is { } leaveAt && now >= leaveAt) Leave(end);
            if (!end.Member) return;
            if (end.Connection == null && end.ReconnectAt is { } reconnectAt && now >= reconnectAt) Connect(end);
            if (!end.IsHost && end.Connected && !end.HelloAcknowledged && now >= end.NextHello) SendHello(end);
            if (!active) return;
            if (now >= end.NextFault) Fault(end);
            if (rng.NextDouble() < 0.0005) end.Rate = NextRate(end);
            if (rng.NextDouble() < end.Rate * StepMs / 1000.0)
                Send(end, end.IsHost
                    ? new AnnouncementMessage($"{end.Stream.Sent + 1}")
                    : new ClaimRoleMessage(end.Id, (PartyRole)rng.Next(8)));
            // Never numbered, never kept: it rides along and must not disturb anything.
            if (!end.IsHost && rng.NextDouble() < 0.02) Transmit(end, new SelfPoseMessage(end.Id, 0, 0, 0, 0, 0, 0, false));
        }

        // ── Interruptions ────────────────────────────────────────────────────

        private void Fault(End end)
        {
            end.NextFault = NextFaultAt(end);
            if (!end.Connected || now - end.ConnectedAt < SteadyMs) return;
            var client = linked.IndexOf(end);
            var kind = rng.NextDouble();
            if (kind < 0.4)
            {
                Note($"{Name(end)} drops");
                relay.Schedule(client, relay.Now, new LinkDrops(rng.Next(0, 6000) / 1000.0));
            }
            else if (kind < 0.6)
            {
                var dead = rng.Next(100, 5000);
                relay.Schedule(client, relay.Now, new UploadsDie(dead / 1000.0, rng.Next(0, 2000) / 1000.0));
                Note($"{Name(end)}'s uploads die for {dead}");
            }
            else if (kind < 0.8)
            {
                var dead = rng.Next(100, 5000);
                relay.Schedule(client, relay.Now, new DownloadsDie(dead / 1000.0, rng.Next(0, 2000) / 1000.0));
                Note($"{Name(end)}'s downloads die for {dead}");
            }
            else if (kind < 0.95 || end.IsHost)
            {
                end.InboundDropUntil = now + rng.Next(10, 500);
                Note($"{Name(end)}'s inbound queue overflows until {end.InboundDropUntil}");
            }
            else
            {
                Note($"{Name(end)} leaves");
                end.LeaveAt = now;
            }
        }

        // A first connect, a reconnect, or a join: Hello once connected, as on every connect.
        private void Connect(End end)
        {
            if (!linked.Contains(end)) linked.Add(end);
            var connection = relay.Connect(end.Secret);
            end.Connection = connection;
            end.Dialing = true;
            end.ReconnectAt = null;
            connection.MessageReceived += (message, _, _, sender) =>
            {
                if (end.Connection == connection) end.Inbox.Enqueue((message, sender));
            };
            connection.Disconnected += _ =>
            {
                if (end.Connection == connection) Lost(end);
            };
            var dialed = end.IsHost && code == null ? connection.ConnectAndHostAsync(relay.Address) : connection.ConnectAsync(relay.Address, code!);
            dialed.ContinueWith(_ =>
            {
                if (end.Connection != connection || !connection.IsConnected) return;
                if (dialed is Task<string?> { Result: { } assigned }) code = assigned;
                end.Dialing = false;
                end.Connected = true;
                end.ConnectedAt = now;
                end.ReconnectAttempt = 0;
                Note($"{Name(end)} connects");
                if (!end.IsHost) SendHello(end);
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        // As MultiplayerManager's: a dropped link or a failed join starts the backoff over, a
        // failed reconnect moves along it.
        private void Lost(End end)
        {
            if (end.Dialing && end.Reconnecting) end.ReconnectAttempt++;
            else end.ReconnectAttempt = 0;
            end.Reconnecting = true;
            end.Connection?.Dispose();
            end.Connection = null;
            end.Connected = end.Dialing = false;
            end.ReconnectAt = now + BackoffMs[Math.Min(end.ReconnectAttempt, BackoffMs.Length - 1)];
        }

        // As LeaveSession: its leave goes out on the stream, then the link closes. Back later, as
        // a new member with a new stream.
        private void Leave(End end)
        {
            end.LeaveAt = null;
            var numbered = end.Stream.Number(new SessionEndedMessage(end.Id));
            end.Sent.Add(numbered);
            if (end.Connection is { } connection)
            {
                var leaving = end.Connected ? connection.SendAsync(numbered) : Task.CompletedTask;
                leaving.ContinueWith(_ => connection.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
            }
            end.Connection = null;
            end.Connected = end.Dialing = false;
            end.Member = false;
            end.JoinAt = now + rng.Next(1000, 5000);
        }

        private void Join(End end)
        {
            end.JoinAt = null;
            end.Member = true;
            end.Stream = NewStream(end);
            end.Sent.Clear();
            end.Taken.Clear();
            end.Inbox.Clear();
            end.Baseline = -1;
            end.HelloAcknowledged = false;
            end.ReconnectAttempt = 0;
            end.Reconnecting = false;
            Note($"{Name(end)} joins");
            Connect(end);
        }

        // ── Sending ──────────────────────────────────────────────────────────

        private void Send(End end, MpMessage message)
        {
            var numbered = end.Stream.Number(message);
            if (numbered.Seq != 0) end.Sent.Add(numbered);
            Transmit(end, numbered);
        }

        private void Transmit(End end, MpMessage message)
        {
            if (end.Connected) _ = end.Connection!.SendAsync(message);
        }

        private void Transmit(End end, IEnumerable<MpMessage> messages)
        {
            foreach (var message in messages) Transmit(end, message);
        }

        private void SendHello(End end)
        {
            end.NextHello = now + HelloRetryMs;
            var (epoch, sent, received) = end.Stream.HelloFields(Host.Id);
            Transmit(end, new HelloMessage(end.Id, "", "", "", 0, epoch, sent, received));
        }

        // MultiplayerManager's Dispatch, for what concerns the stream.
        private void Handle(End to, End from, MpMessage message)
        {
            if (message.Seq != 0)
            {
                if (!to.Stream.Take(from.Id, message, out var ask))
                {
                    if (ask != null) Transmit(to, ask);
                    return;
                }
                if (!to.Taken.TryGetValue(from.Id, out var taken)) to.Taken[from.Id] = taken = [];
                taken.Add(message);
                if (to.IsHost && message is SessionEndedMessage) to.Stream.Forget(from.Id);
                return;
            }
            switch (message)
            {
                case HelloMessage hello when to.IsHost:
                    // A new epoch is a new stream: what came before it is the old one's.
                    if (to.EpochTaken.GetValueOrDefault(from.Id) != hello.StreamEpoch)
                    {
                        to.EpochTaken[from.Id] = hello.StreamEpoch;
                        to.Taken[from.Id] = [];
                    }
                    Transmit(to, to.Stream.Hello(hello.PeerId, hello.StreamEpoch, hello.StreamReceived));
                    break;
                case StreamStateMessage state when !to.IsHost && state.PeerId == to.Id:
                    if (state.StreamEpoch == to.Stream.Epoch)
                    {
                        to.HelloAcknowledged = true;
                        if (to.Baseline < 0) to.Baseline = state.Sent;
                    }
                    Transmit(to, to.Stream.State(from.Id, state.StreamEpoch, state.Sent, state.Received));
                    break;
                case PingMessage ping when !to.IsHost:
                {
                    var (epoch, sent, received) = to.Stream.PongFields(from.Id);
                    Transmit(to, new PongMessage(to.Id, ping.SentAtMs, epoch, sent, received));
                    var ours = ping.StreamReceived?.FirstOrDefault(c => c.PeerId == to.Id);
                    if (to.Stream.Ping(from.Id, ping.StreamSent, ours) is { } ask) Transmit(to, ask);
                    break;
                }
                case PongMessage pong when to.IsHost:
                    if (to.Stream.Pong(pong.PeerId, pong.StreamEpoch, pong.StreamSent, pong.StreamReceived) is { } askPeer) Transmit(to, askPeer);
                    break;
                case StreamGapMessage gap when to.IsHost:
                    Transmit(to, to.Stream.Resend(gap.PeerId, gap.From));
                    break;
                case StreamGapMessage gap when gap.PeerId == to.Id:
                    Transmit(to, to.Stream.Resend(from.Id, gap.From));
                    break;
                case StreamLostMessage lost when to.IsHost || lost.PeerId == to.Id:
                    problems.Add($"{Name(to)} was told {Name(from)} no longer has #{lost.Next - 1} and before");
                    to.Stream.Lost(to.IsHost ? lost.PeerId : from.Id, lost.Next, lost.Epoch);
                    break;
            }
        }

        // ── Checks ───────────────────────────────────────────────────────────

        private bool CaughtUp()
            => ends.All(e => !e.Member || e.Connected && (e.IsHost || e.Baseline >= 0))
               && Members().All(p => TakenFrom(p, Host).Count == Host.Sent.Count(m => m.Seq > p.Baseline)
                                     && TakenFrom(Host, p).Count == p.Sent.Count)
               && Host.Stream.Logged == 0 && Members().All(p => p.Stream.Logged == 0);

        private IEnumerable<End> Members() => ends.Skip(1).Where(p => p.Member);

        private static List<MpMessage> TakenFrom(End to, End from) => to.Taken.GetValueOrDefault(from.Id) ?? [];

        public List<string> Problems()
        {
            var all = new List<string>(problems);
            foreach (var peer in Members())
            {
                if (!peer.Connected || peer.Baseline < 0)
                {
                    all.Add($"{Name(peer)} never got back in");
                    continue;
                }
                Compare(all, Host, peer, Host.Sent.Where(m => m.Seq > peer.Baseline).ToList(), TakenFrom(peer, Host));
                Compare(all, peer, Host, peer.Sent, TakenFrom(Host, peer));
                if (peer.Stream.Logged > 0) all.Add($"{Name(peer)} still keeps {peer.Stream.Logged} messages the host has");
            }
            if (Host.Stream.Logged > 0) all.Add($"the host still keeps {Host.Stream.Logged} messages every peer has");
            return all;
        }

        private void Compare(List<string> all, End from, End to, List<MpMessage> sent, List<MpMessage> taken)
        {
            for (var i = 0; i < Math.Max(sent.Count, taken.Count); i++)
            {
                var expected = i < sent.Count ? sent[i] : null;
                var got = i < taken.Count ? taken[i] : null;
                if (Equals(expected, got)) continue;
                all.Add($"{Name(to)} took {Describe(got)} as {Name(from)}'s message #{i + 1} of its stream, which is {Describe(expected)} ({sent.Count} sent, {taken.Count} taken)");
                return;
            }
        }

        private static string Describe(MpMessage? message) => message == null ? "nothing" : $"#{message.Seq} {message.GetType().Name}";

        private string Name(End end) => end.IsHost ? "the host" : $"peer {ends.IndexOf(end)}";

        private void Note(string what) => Log.Add($"{now,7} {what}");
    }
}
