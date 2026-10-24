using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

internal sealed record PlayerSeat(PartyRole Role, bool BotControlled = true);

// A multiplayer session played headless: a host and its peers joined through one relay, each on
// its own fake game client, frame by frame from the lobby to the end of the fight. In virtual time,
// so a seed replays the run, unless it's over a real connection (liveConnection).
internal sealed class MultiplayerRun
{
    private const double LobbySeconds = 10;
    // A ping every 2 s, the first answer reported at the next.
    private const double RoundTripsSeconds = 20;
    private const double ResetSeconds = 10;
    // Enter, the stage's settle and the start, with the start check's own wait.
    private const double StartSeconds = 45;
    private const double FlushSeconds = 2;
    // A link that dropped is back within the reconnect backoff, and the ping cycle finds what's
    // still missing within two more; a download that fell behind drains what it holds.
    private const double SettleSeconds = 60;
    // As in game. The host's arrive at once, so a peer has to show everything the host's client
    // was told even while its own copies are still on the way.
    public static readonly (int Min, int Max) PeerSpawnFrames = (2, 6);

    // A peer rolls its own seed, as in game: one that played the host's rolls without being sent
    // them would pass by luck.
    private const int PeerSeedOffset = 7919;

    // Where a peer's client hands out actor slots: its entity ids differ from the host's, as two
    // real clients' do, so a packet that kept the host's would miss.
    private const int PeerSlotOffset = 37;

    private readonly List<(TestClient Client, double Frame, double LastFrame, double NextFrame, Random Hitches, double NextHitch, ConnectionProfile Link)> clients = [];
    private readonly List<Death> deaths = [];
    private readonly List<(double At, int Client, Action<TestClient> Input)> inputs = [];
    private readonly bool live;
    private Action<TestClient>? beforeFrame;

    public ISessionNetwork Network { get; }
    // The simulated network, in a run that isn't over a real connection.
    public TestRelay Relay => Network as TestRelay ?? throw new InvalidOperationException("This run is over a real connection.");
    public ConnectionProfile PeerLink { get; }
    public TestClient Host { get; }
    public IReadOnlyList<TestClient> Peers { get; }
    public IReadOnlyList<Death> Deaths => deaths;
    // On the relay's clock, from the host's last run starting to its end.
    public double StartedAt { get; private set; } = double.NaN;
    public double EndedAt { get; private set; } = double.NaN;
    // Every run's, in order.
    public List<(double StartedAt, double EndedAt)> Fights { get; } = [];
    public string? Failure { get; private set; }

    private MultiplayerRun(int seed, PlayerSeat host, IReadOnlyList<PlayerSeat> peers, ConnectionProfile hostLink, ConnectionProfile peerLink, bool live)
    {
        this.live = live;
        Network = live ? new LiveRelay() : new TestRelay(seed, index => index == 0 ? hostLink : peerLink);
        PeerLink = peerLink;
        Host = new TestClient("Host", host.Role, seed, Network, isHost: true, slotOffset: 0, spawnFrames: (0, 0));
        Peers = peers.Select((p, i) => new TestClient($"Peer{i + 1}", p.Role, seed + PeerSeedOffset * (i + 1), Network,
            isHost: false, slotOffset: PeerSlotOffset, spawnFrames: PeerSpawnFrames)).ToList();
        Add(Host, hostLink.HostFps, hostLink, seed);
        foreach (var peer in Peers) Add(peer, peerLink.PeerFps, peerLink, seed + clients.Count);
    }

    private void Add(TestClient client, double fps, ConnectionProfile link, int seed)
    {
        var hitches = new Random(seed + 77);
        clients.Add((client, 1 / fps, 0, 1 / fps, hitches, Exponential(hitches, link.PeerHitchEvery), link));
    }

    // immortal: nobody dies on the host, so a player who never moves still sees the whole fight.
    // runs: the fight played again after the host's Reset, in the same stay and on the same rolls,
    // as forced rolls replay it. interrupt: the faults to put on the links, by client (the host 0,
    // then the peers), before anyone connects. inputs: what a client's player does, by client and
    // seconds into the host's fight. configure: each client's own settings, before anyone connects.
    // beforeFrame: what a client's player does at the start of each of that client's frames.
    // liveConnection: over the real relay on this machine's loopback, in real time, the links'
    // own limits left out. network: the session's network, either kind, before anyone connects.
    public static MultiplayerRun Play(Type scenarioType, int strat, int seed, PlayerSeat host, IReadOnlyList<PlayerSeat> peers,
        ConnectionProfile? hostLink = null, ConnectionProfile? peerLink = null, double timeout = 600, bool immortal = false, int runs = 1,
        Action<TestRelay>? interrupt = null, IEnumerable<(double At, int Client, Action<TestClient> Input)>? inputs = null,
        Action<TestClient>? configure = null, Action<TestClient>? beforeFrame = null, bool liveConnection = false,
        Action<ISessionNetwork>? network = null)
    {
        var run = new MultiplayerRun(seed, host, peers, hostLink ?? TestRelay.Perfect, peerLink ?? TestRelay.Perfect, liveConnection);
        if (interrupt != null) interrupt(run.Relay);
        network?.Invoke(run.Network);
        foreach (var client in run.Peers.Prepend(run.Host)) configure?.Invoke(client);
        run.inputs.AddRange((inputs ?? []).OrderBy(i => i.At));
        run.beforeFrame = beforeFrame;
        run.Host.Game.GodMode = immortal;
        run.Host.Game.PartyMemberKilled += (role, cause, actionId, statusId) => run.deaths.Add(new Death(role, cause, actionId, statusId, run.Host.Game.World.Events.Elapsed, ""));
        try
        {
            run.Lobby(host, peers);
            for (var i = 0; i < runs; i++)
            {
                if (i > 0) run.Reset();
                run.Fight(scenarioType, strat, seed, timeout);
            }
        }
        catch (Exception e)
        {
            run.Failure ??= $"threw at t={run.Host.Game.Events.Elapsed:F2}: {e}";
        }
        finally
        {
            (run.Network as IDisposable)?.Dispose();
        }
        return run;
    }

    private void Lobby(PlayerSeat host, IReadOnlyList<PlayerSeat> peers)
    {
        Host.Do(() =>
        {
            Host.Multiplayer.HostSession(Network.Address);
            Host.Multiplayer.SetDebugBotControlled(host.BotControlled);
            Host.Multiplayer.ClaimRole(host.Role);
        });
        RunUntil(() => Host.Multiplayer.SessionCode != null, LobbySeconds, "the relay to give the host a session code");
        var code = Host.Multiplayer.SessionCode!;
        for (var i = 0; i < Peers.Count; i++)
        {
            var (peer, seat) = (Peers[i], peers[i]);
            peer.Do(() =>
            {
                peer.Multiplayer.JoinSession(Network.Address, code);
                peer.Multiplayer.SetDebugBotControlled(seat.BotControlled);
            });
        }
        RunUntil(() => Peers.All(p => p.Multiplayer.Session.Names.ContainsKey(p.Multiplayer.MyPeerId)), LobbySeconds, "peers joining the lobby");
        for (var i = 0; i < Peers.Count; i++)
        {
            var (peer, seat) = (Peers[i], peers[i]);
            peer.Do(() => peer.Multiplayer.ClaimRole(seat.Role));
        }
        RunUntil(() => Peers.All(p => p.Multiplayer.MyClaimedRole == p.Seat), LobbySeconds, "peers claiming their seats");
        // In real time the lobby lasts until each peer has the round trips its clock lead is built
        // on, as a real lobby does. Ones measured while this process was still starting up, its
        // clients connecting for the first time, would lead each peer by the CPU it was short of.
        if (live) RunUntil(RoundTripsTold(), RoundTripsSeconds, "the host to measure every peer's round trips");
    }

    // Whether every peer has been told MultiplayerManager.LeadRoundTrips of its round trips since
    // this was called, in the host's PeerStatusMessages.
    private Func<bool> RoundTripsTold()
    {
        var seen = Network.Sends.Count;
        var told = Peers.ToDictionary(p => p.PeerId, _ => 0);
        return () =>
        {
            for (; seen < Network.Sends.Count; seen++)
                if (Network.Sends[seen] is { From: 0, Message: PeerStatusMessage status })
                    foreach (var (peerId, entry) in status.Statuses)
                        if (entry.LatencyMs != null && told.ContainsKey(peerId)) told[peerId]++;
            return told.Values.All(count => count >= MultiplayerManager.LeadRoundTrips);
        };
    }

    // The host's Reset button: every client's run ends, and the stay goes on.
    private void Reset()
    {
        Host.Do(() => Host.Game.Reset());
        RunUntil(() => Host.Game.ActiveScenario == null && Peers.All(p => p.Game.ActiveScenario == null), ResetSeconds, "everyone resetting");
    }

    private void Fight(Type scenarioType, int strat, int seed, double timeout)
    {
        // As a player would: Start does nothing while the host's link is down.
        RunUntil(() => Host.Multiplayer.IsConnected, StartSeconds, "the host's link to be up");
        Host.Do(() => Host.Multiplayer.StartScenario(new AnoMech.ScenarioSelection(
            Host.Game.Catalog.Scenarios.Single(s => s.GetType() == scenarioType), strat, 0, seed, null)));
        bool Refused() => Host.Multiplayer.StartCheckFailureReason != null || Host.Multiplayer.RunEndReason != null;
        RunUntil(() => Host.Game.ActiveScenario != null || Refused(), StartSeconds, "the host starting the fight");
        StartedAt = Network.Now;
        RunUntil(() => Peers.All(p => p.Game.ActiveScenario != null) || Refused(), StartSeconds, "everyone entering the fight");
        if ((Host.Multiplayer.StartCheckFailureReason ?? Host.Multiplayer.RunEndReason) is { } refused) throw new InvalidOperationException(refused);
        RunUntil(() => Host.Game.Status is SessionStatus.Succeeded or SessionStatus.CompletedWithMistake or SessionStatus.CompletedFailed or SessionStatus.Frozen
                       || Host.Game.ActiveScenario == null, timeout, "the fight to end");
        HostOutcome = Host.Game.ActiveScenario != null ? Host.Game.Status : null;
        EndedAt = Network.Now;
        Fights.Add((StartedAt, EndedAt));
        HostClockAtEnd = ClockNow(0);
        PeerClocksAtEnd = Enumerable.Range(1, Peers.Count).Select(ClockNow).ToList();
        var until = Network.Now + FlushSeconds;
        RunUntil(() => Network.Now >= until && StreamsSettled() && Idle(), SettleSeconds, "the last messages to land");
    }

    // How the host's last fight ended; null when it left the fight before its end.
    public SessionStatus? HostOutcome { get; private set; }

    // The inputs the fight ended before.
    public int InputsLeft => inputs.Count;

    // Each run clock as the host's run ended; a peer's leads the host's by its poses' delay.
    public double HostClockAtEnd { get; private set; }
    public IReadOnlyList<double> PeerClocksAtEnd { get; private set; } = [];

    // A client's run clock now: where its last frame left it plus the time since, as the host
    // reads its own between frames. The clients' last frames come at different moments, and in
    // real time far apart when the thread runs late.
    private double ClockNow(int index)
    {
        var (client, _, last, _, _, _, _) = clients[index];
        var game = client.Game;
        return game.Events.Elapsed + (game.Paused ? 0 : (Network.Now - last) * game.EventTimeScale);
    }

    // Every client still in the session has taken everything the others numbered for it.
    private bool StreamsSettled()
        => Peers.Where(p => p.Multiplayer.SessionCode != null && Host.Multiplayer.SessionCode != null)
            .All(p => Taken(p, Host).Count == Host.StreamSent.Count(m => m.Message.Seq > Baseline(p))
                      && Taken(Host, p).Count == p.StreamSent.Count);

    // What a client queued for its next frame has run: one whose session just ended leaves the
    // fight on the frame after.
    private bool Idle() => Peers.Prepend(Host).All(c => c.Fake.Framework.Idle);

    public static List<MpMessage> Taken(TestClient to, TestClient from)
        => to.StreamTaken.Where(t => t.From == from.PeerId).Select(t => t.Message).ToList();

    // Where a peer took up the host's stream: just before the first message it took of it.
    public long Baseline(TestClient peer)
        => peer.StreamTaken.FirstOrDefault(t => t.From == Host.PeerId).Message is { } first ? first.Seq - 1 : Host.StreamSent.Count;

    // The longest any numbered message took from its first send to being taken.
    public double MaxStreamDelay()
    {
        var clients = Peers.Prepend(Host).ToList();
        var sentAt = clients.ToDictionary(c => c.PeerId, c => c.StreamSent.GroupBy(s => s.Message.Seq).ToDictionary(g => g.Key, g => g.First().At));
        var worst = 0.0;
        foreach (var to in clients)
        foreach (var (at, from, message) in to.StreamTaken)
            if (sentAt.TryGetValue(from, out var times) && times.TryGetValue(message.Seq, out var sent))
                worst = Math.Max(worst, at - sent);
        return worst;
    }

    // Each client's frames at its own rate, the relay advancing to each. In real time a frame is as
    // long as it really took, as in game: the thread can be late for one, while the clients time
    // what they're sent by the clock.
    private void RunUntil(Func<bool> done, double seconds, string what)
    {
        var deadline = Network.Now + seconds;
        while (!done())
        {
            if (Network.Now > deadline) throw new TimeoutException($"Gave up waiting for {what} after {seconds:F0}s.");
            var index = 0;
            for (var i = 1; i < clients.Count; i++)
                if (clients[i].NextFrame < clients[index].NextFrame) index = i;
            var (client, frame, last, next, hitches, nextHitch, link) = clients[index];
            Network.AdvanceTo(next);
            var at = live ? Network.Now : next;
            if (beforeFrame is { } drive) client.Do(() => drive(client));
            RunInputs(index, client);
            client.Frame((float)(at - last));
            var gap = frame;
            if (next >= nextHitch)
            {
                gap = link.PeerHitchMin + hitches.NextDouble() * (link.PeerHitchMax - link.PeerHitchMin);
                nextHitch = next + gap + Exponential(hitches, link.PeerHitchEvery);
            }
            clients[index] = (client, frame, at, next + gap, hitches, nextHitch, link);
        }
    }

    private void RunInputs(int index, TestClient client)
    {
        if (double.IsNaN(StartedAt)) return;
        for (var i = 0; i < inputs.Count && StartedAt + inputs[i].At <= Network.Now; i++)
        {
            var (_, owner, input) = inputs[i];
            if (owner != index) continue;
            inputs.RemoveAt(i--);
            client.Do(() => input(client));
        }
    }

    private static double Exponential(Random random, double mean) => -mean * Math.Log(1 - random.NextDouble());
}
