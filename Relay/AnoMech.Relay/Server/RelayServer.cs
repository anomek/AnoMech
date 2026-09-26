using AnoMech.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Relay;

// Session-scoped relay. Authenticates each connection's identity, tags every forwarded frame
// with it, routes peer traffic only to the host, and enforces the host's kicks and bans. Message
// bodies pass through unread and are never decompressed; all app-level meaning lives in the
// plugin, which also does every check on message content.
//
// Exists because a Dalamud client is firewalled off from FFXIV's own server traffic during
// a scenario (see ZoneSession), and most players sit behind NAT, so direct P2P isn't viable.
//
// Meant to be runnable as a public service (anyone can point a plugin at it, not just people
// you've personally shared a URL with) -- see Relay/README.md's Security notes for the full
// threat model this is designed against.
//
// All state is per instance so a host process (the CLI, or the plugin embedding it) can stop
// and start relays without leftovers. One instance runs once: Start, then StopAsync.
internal sealed class RelayServer : IAsyncDisposable
{
    // ---- Fixed tunables ---------------------------------------------------------------------

    private static readonly TimeSpan FailedJoinWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan JoinLockoutDuration = TimeSpan.FromMinutes(5);

    // A stalled request or WS handshake, or a message that takes too long to fully arrive,
    // gets abandoned instead of held open indefinitely (slowloris-style).
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MessageAssemblyTimeout = TimeSpan.FromSeconds(30);

    // Bounds every close handshake. CloseAsync waits for the peer's own close frame, so
    // without this one unresponsive socket stalls whoever is closing it -- which for ReapLoop
    // means every timed control in the process stops with it.
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private const int MaxAdminBodyBytes = 64 * 1024;

    // How many rejected connections inside one ReapInterval trips an [ALERT] log line and
    // shows as elevated in the admin dashboard. Not a hard block -- purely a "go look at this"
    // signal, since a real distributed attack won't be stopped by anything in this process
    // anyway (see README's Security notes on volumetric/botnet attacks).
    private const long AlertRejectionThresholdPerTick = 50;

    // Sent once right after a socket joins so a client can detect a narrower relay before
    // relying on a behavior it doesn't have. Not an MpMessage: the relay reads no message
    // bodies beyond the host's relayControl frames.
    private const int RelayVersion = RelayWire.Version;
    private static readonly string[] RelayCapabilities = ["binaryCompression", "authenticatedIdentity", "roomModeration"];
    private static readonly string RelayCapabilitiesJson = string.Join(",", RelayCapabilities.Select(c => $"\"{c}\""));

    // The relay owns the room namespace, so it's the only party that can guarantee no
    // collision (vs. a client picking one locally and hoping). Hosting goes through /host
    // (no code in the URL); the relay picks a free one and hands it back in the greeting.
    // Cryptographically random (not System.Random) -- on a public relay, a stranger who's
    // observed a few issued codes must not be able to predict a future one and race the real
    // host into a session before they've even shared its code.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I, 32 chars
    private const int CodeLength = 6;

    // A room idle this long is considered abandoned (crashed host, dead sockets that never
    // closed cleanly). Comfortably above MultiplayerManager's 2s ping, so any live host
    // keeps its room for free.
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReapInterval = TimeSpan.FromSeconds(2);

    // How often ReapLoop also emits a summary line, independent of idle-session sweeps --
    // ambient "is this thing alive and how loaded" visibility in the console/journal without
    // needing the admin endpoint.
    private static readonly TimeSpan SummaryLogInterval = TimeSpan.FromMinutes(1);

    private const int MaxRoomBans = 1024;

    // ---- Live state ---------------------------------------------------------------------

    internal sealed class PeerConn(WebSocket socket, uint id, IPAddress ip, Guid peerId)
    {
        public readonly WebSocket Socket = socket;
        public readonly Guid PeerId = peerId;
        // A WebSocket allows one send at a time, and a broadcast, a greeting and a close can all
        // target the same socket at once.
        public readonly SemaphoreSlim SendGate = new(1, 1);
        // Set once the greeting is out, so no room traffic can arrive ahead of it.
        public bool Ready;
        public readonly uint Id = id;
        public readonly IPAddress Ip = ip;
        public readonly DateTime JoinedUtc = DateTime.UtcNow;
        public long MessagesIn;
        public long BytesIn;
        public int PeakMessagesPerSecond;
        public long PeakBytesPerSecond;
        public bool NearLimitWarned;
    }

    private sealed class Room
    {
        public readonly List<PeerConn> Peers = new();
        // Guards this room's own Peers/LastActivityUtc only -- NOT the Sessions table below.
        // One lock per room (not one relay-wide lock) so unrelated sessions never contend with
        // each other on join/leave/broadcast; only two operations touching the SAME session
        // ever serialize against one another. See TryJoin/Leave for why Sessions removal also
        // has to happen while holding this lock, not the table's own (lock-free) operations.
        public readonly object Lock = new();
        // Whoever created the room, by authenticated identity rather than socket, so a host
        // that reconnects is the host again. Tagged onto every broadcast from them (see
        // BroadcastAsync) so a receiving client can tell a real host message from a forgery.
        public Guid HostPeerId;
        // Null address: banned while not connected. Filled in the next time that identity tries.
        public readonly Dictionary<Guid, IPAddress?> Bans = new();
        public DateTime LastActivityUtc = DateTime.UtcNow;
        public readonly DateTime CreatedUtc = DateTime.UtcNow;
    }

    private readonly RelayOptions options;
    private readonly IRelayLog log;

    private readonly ConcurrentDictionary<string, Room> Sessions = new();
    private int nextConnectionId;

    // Per-IP abuse tracking, separate lock since it's touched on a different cadence
    // (every connection/join attempt) than the session table. Keys are AbuseKey(ip), not the
    // raw address -- see there.
    private readonly Dictionary<IPAddress, int> ConnectionsByIp = new();
    private readonly Dictionary<IPAddress, Queue<DateTime>> FailedJoinsByIp = new();
    private readonly Dictionary<IPAddress, DateTime> JoinLockoutUntilByIp = new();
    private readonly Dictionary<IPAddress, Queue<DateTime>> FailedAdminByIp = new();
    private readonly Dictionary<IPAddress, DateTime> AdminLockoutUntilByIp = new();
    private readonly HashSet<IPAddress> BannedIps = new();
    private readonly object AbuseLock = new();

    // Admin kill switch: existing sessions keep running, nothing new is accepted.
    private volatile bool acceptingConnections = true;

    private TcpListener? listener;
    private readonly CancellationTokenSource shutdown = new();
    private Task acceptLoop = Task.CompletedTask;
    private Task reapLoop = Task.CompletedTask;
    // Every open TCP connection, so StopAsync can close ones still mid-handshake too.
    private readonly ConcurrentDictionary<TcpClient, byte> connections = new();
    private readonly ConcurrentDictionary<Task, byte> handlers = new();

    // ---- Abuse-relevant counters, all lifetime totals exposed via /admin/stats. Interlocked,
    // not lock-guarded -- each is an independent running total, no cross-field consistency
    // needed. recentRejections resets every ReapInterval (see ReapLoop) and drives the alert
    // threshold above.
    private DateTime startedAtUtc = DateTime.UtcNow;
    private long totalConnectionsAccepted;
    private long totalMessagesBroadcast;
    private long totalBytesBroadcast;
    private long recentRejections;
    private long peakMessagesPerSecond, peakBytesPerSecond, nearLimitWarnings;
    private long rejectedOrigin, rejectedIpCap, rejectedJoinLockout, rejectedRelayFull,
        rejectedSessionFull, rejectedSessionNotFound, rejectedBadToken, rejectedHandshakeTimeout,
        rejectedMessageTooLarge, rejectedMessageTimeout, rejectedMessageRate, rejectedByteRate,
        rejectedUnencrypted, rejectedTooManyFragments, rejectedBanned, rejectedPaused, rejectedBadProtocol;

    public RelayServer(RelayOptions options, IRelayLog log)
    {
        this.options = options with { TrustedProxies = [.. options.TrustedProxies] };
        this.log = log;
    }

    public IPEndPoint? LocalEndPoint => listener?.LocalEndpoint as IPEndPoint;
    public bool IsRunning => listener != null && !shutdown.IsCancellationRequested;

    // ---- Lifecycle ------------------------------------------------------------------------

    // Throws SocketException when the port can't be bound.
    public void Start()
    {
        if (listener != null) throw new InvalidOperationException("A relay instance can only be started once.");
        listener = Bind(options.BindAddress, options.Port);
        startedAtUtc = DateTime.UtcNow;

        var endpoint = LocalEndPoint!;
        log.Info($"[AnoMech.Relay] Listening on {endpoint}. Host a session at /host, join one at /session/<code>.");
        log.Info($"[AnoMech.Relay] Access token: {(options.AccessToken != null ? "required" : "not set -- anyone can connect")}. " +
                 $"Admin endpoint: {(options.AdminToken != null ? "enabled" : "disabled (no --admin-token)")}.");
        log.Info(options.TrustedProxies.Count == 0
            ? "[AnoMech.Relay] No --trusted-proxy set: client addresses are read from the transport only, and forwarded headers are ignored."
            : $"[AnoMech.Relay] Trusting {options.ClientIpHeader} / X-Forwarded-Proto from: {string.Join(", ", options.TrustedProxies)}.");
        log.Info($"[AnoMech.Relay] Per-connection caps: {options.MaxMessagesPerSecond} msg/s, " +
                 $"{options.MaxBytesPerSecond / (1024.0 * 1024):F1} MB/s, {options.MaxMessageBytes / 1024} KB/message. " +
                 $"Logging a [NEAR-LIMIT] line past {options.UsageWarnFraction:P0} of either rate.");
        if (options.RequireTls || options.AccessToken != null || options.AdminToken != null)
            log.Info($"[AnoMech.Relay] {(options.RequireTls ? "--require-tls is set" : "A token is set")}, so every connection now " +
                     "REQUIRES a TLS-terminating reverse proxy in front (X-Forwarded-Proto: https) -- see " +
                     "Relay/README.md. Unencrypted connections will be rejected with 426, including plain local testing.");

        acceptLoop = AcceptLoopAsync(listener);
        reapLoop = ReapLoop();
    }

    private static TcpListener Bind(IPAddress address, int port)
    {
        var candidate = new TcpListener(address, port);
        try
        {
            if (address.Equals(IPAddress.IPv6Any)) candidate.Server.DualMode = true;
            candidate.Start();
            return candidate;
        }
        // Hosts with IPv6 disabled still get the wildcard bind, over IPv4 alone.
        catch (SocketException e) when (address.Equals(IPAddress.IPv6Any) && e.SocketErrorCode == SocketError.AddressFamilyNotSupported)
        {
            candidate.Server.Dispose();
            return Bind(IPAddress.Any, port);
        }
    }

    public async Task StopAsync()
    {
        if (listener == null || shutdown.IsCancellationRequested) return;
        shutdown.Cancel();
        listener.Stop();

        foreach (var (_, room) in Sessions)
        {
            List<PeerConn> peers;
            lock (room.Lock)
            {
                peers = [.. room.Peers];
                room.Peers.Clear();
            }
            foreach (var peer in peers) peer.Socket.Abort();
        }
        Sessions.Clear();
        foreach (var client in connections.Keys) client.Dispose();

        try
        {
            await Task.WhenAll([acceptLoop, reapLoop, .. handlers.Keys]).WaitAsync(StopTimeout);
        }
        catch (Exception e)
        {
            log.Warn($"[AnoMech.Relay] Shutdown didn't finish cleanly: {e.Message}");
        }
        log.Info("[AnoMech.Relay] Stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(TcpListener activeListener)
    {
        while (!shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await activeListener.AcceptTcpClientAsync(shutdown.Token);
            }
            catch (Exception e)
            {
                if (shutdown.IsCancellationRequested) break;
                // One failed accept (a connection reset before we picked it up) must not take
                // down every other session.
                log.Warn($"[AnoMech.Relay] Unexpected error accepting a connection: {e.Message} -- continuing.");
                continue;
            }
            // Relayed frames are small and latency-sensitive; don't let Nagle hold them back.
            client.NoDelay = true;
            var handler = HandleClientAsync(client);
            handlers.TryAdd(handler, 0);
            _ = handler.ContinueWith(done => handlers.TryRemove(done, out _), TaskScheduler.Default);
        }
    }

    // Nothing may escape here: an embedding process must never see an unobserved relay fault.
    private async Task HandleClientAsync(TcpClient client)
    {
        connections.TryAdd(client, 0);
        try
        {
            await HandleConnectionAsync(client);
        }
        catch (Exception e)
        {
            if (!shutdown.IsCancellationRequested)
                log.Detail($"[AnoMech.Relay] connection ended with an error: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            connections.TryRemove(client, out _);
            client.Dispose();
        }
    }

    private void CountRejection(ref long counter)
    {
        Interlocked.Increment(ref counter);
        Interlocked.Increment(ref recentRejections);
    }

    // For rejection reasons that otherwise leave no individual trace anywhere (only the
    // aggregate counter) -- logs one Detail line per rejection so "what happened to this one
    // connection attempt" is answerable later, without spamming the console for high-volume
    // abuse (the [ALERT] summary in ReapLoop covers that).
    private void CountRejection(ref long counter, string reason, IPAddress ip, string sessionTag)
    {
        CountRejection(ref counter);
        log.Detail($"[{sessionTag}] rejected ({reason}) from {ip}");
    }

    private static void RecordMax(ref long target, long value)
    {
        long seen;
        while (value > (seen = Interlocked.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, seen) == seen) return;
    }

    // ---- Client identity ----------------------------------------------------------------

    // A dual-stack listener reports IPv4 clients as ::ffff:a.b.c.d, which AbuseKey's /64 would
    // otherwise lump into one bucket shared by every IPv4 client.
    internal static IPAddress NormalizeIp(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private bool IsTrustedProxy(IPAddress ip) => options.TrustedProxies.Any(n => n.Contains(NormalizeIp(ip)));

    // The address every abuse control is keyed on. Behind a reverse proxy the transport
    // address is the proxy's, which would collapse every per-IP cap and lockout into one
    // shared bucket -- so a forwarded header is consulted, but ONLY when the request actually
    // came from a configured proxy. The rightmost untrusted entry is the one the trusted hop
    // observed directly; anything further left was written by the client and is forgeable.
    private IPAddress ResolveClientIp(RelayHttpRequest request)
    {
        var transport = request.RemoteAddress;
        if (!IsTrustedProxy(transport)) return transport;
        var header = request.Header(options.ClientIpHeader);
        if (string.IsNullOrEmpty(header)) return transport;
        var parts = header.Split(',');
        for (var i = parts.Length - 1; i >= 0; i--)
            if (TryParseForwardedAddress(parts[i], out var candidate) && !IsTrustedProxy(candidate))
                return NormalizeIp(candidate);
        return transport;
    }

    private static bool TryParseForwardedAddress(string raw, out IPAddress address)
    {
        var text = raw.Trim();
        if (IPAddress.TryParse(text, out address!)) return true;
        // "[::1]:1234" and "10.0.0.1:1234" both appear in the wild.
        if (text.StartsWith('[') && text.IndexOf(']') is var close && close > 0)
            return IPAddress.TryParse(text[1..close], out address!);
        var colon = text.LastIndexOf(':');
        return colon > 0 && IPAddress.TryParse(text[..colon], out address!);
    }

    // IPv6 is handed out in blocks, so a per-address key would let anyone with a /64 (the
    // standard VPS allocation) sidestep every cap by rotating the low bits.
    internal static IPAddress AbuseKey(IPAddress ip)
    {
        ip = NormalizeIp(ip);
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return ip;
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes);
    }

    // The relay has no TLS of its own -- wss:// is always a reverse proxy terminating TLS in
    // front of us (see README), so X-Forwarded-Proto is the only signal we have for "was the
    // real client connection actually encrypted", and it counts as a signal only when the
    // request reached us from a proxy we were configured to trust.
    private bool IsRequestEncrypted(RelayHttpRequest request)
    {
        var transport = request.RemoteAddress;
        var forwardedProto = request.Header("X-Forwarded-Proto");
        // A loopback request that wasn't forwarded never left the machine, so there is nothing
        // for TLS to protect -- this is what lets the admin CLI reach a local relay. An
        // attacker can't produce a loopback transport address, and a proxy (which is itself
        // usually on loopback) always sets the header, so it falls through to the check below.
        if (string.IsNullOrEmpty(forwardedProto) && IPAddress.IsLoopback(transport)) return true;
        return IsTrustedProxy(transport)
               && string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
    }

    // ---- Connection handling -------------------------------------------------------------

    private async Task HandleConnectionAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var transport = NormalizeIp(((IPEndPoint)client.Client.RemoteEndPoint!).Address);
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        handshake.CancelAfter(HandshakeTimeout);

        RelayHttpRequest? request;
        try
        {
            request = await RelayHttp.ReadRequestAsync(stream, transport, handshake.Token);
        }
        catch (OperationCanceledException) when (!shutdown.IsCancellationRequested)
        {
            CountRejection(ref rejectedHandshakeTimeout, "request stalled", transport, "http");
            return;
        }
        if (request is null)
        {
            await RelayHttp.WriteStatusAsync(stream, 400, handshake.Token);
            return;
        }

        Task Respond(int status) => RelayHttp.WriteStatusAsync(stream, status, handshake.Token);
        var path = request.Path;

        // Plain-HTTP endpoints, no WebSocket upgrade -- handled entirely separately from the
        // relay/join flow below.
        if (!request.IsWebSocketRequest)
        {
            switch (path)
            {
                case "info": await ServeInfoAsync(stream, request, handshake.Token); return;
                case "admin/stats":
                    await ServeAdminAsync(stream, request, handshake.Token, () => Task.FromResult(JsonSerializer.Serialize(GetStats(), RelayAdmin.Json)));
                    return;
                case "admin/sessions":
                    await ServeAdminAsync(stream, request, handshake.Token, () => Task.FromResult(JsonSerializer.Serialize(GetSessions(), RelayAdmin.Json)));
                    return;
                case "admin/action":
                    await ServeAdminAsync(stream, request, handshake.Token, async () =>
                        ApplyAdminActionBody(await RelayHttp.ReadBodyAsync(stream, request, MaxAdminBodyBytes, handshake.Token), ResolveClientIp(request)));
                    return;
            }
        }

        var isHostRequest = path == "host";
        var joinCode = isHostRequest ? null : ExtractSessionCode(path);
        if ((!isHostRequest && joinCode is null) || !request.IsWebSocketRequest)
        {
            await Respond(400);
            return;
        }

        var ip = ResolveClientIp(request);
        var key = AbuseKey(ip);
        // Best available session identity before a room necessarily exists yet -- lets a
        // rejection that happens pre-join (bad token, IP cap, lockout...) still show up under
        // --session-log for the code someone was trying to reach. "host" requests don't have
        // a real code to attach to until TryCreateSession succeeds.
        var sessionTag = isHostRequest ? "host" : joinCode!;

        if (!acceptingConnections)
        {
            CountRejection(ref rejectedPaused, "relay paused by admin", ip, sessionTag);
            await Respond(503);
            return;
        }

        if (IsBanned(key))
        {
            CountRejection(ref rejectedBanned, "banned", ip, sessionTag);
            await Respond(403);
            return;
        }

        // A password is worthless if it's sent in the clear -- once one is set, every
        // connection must be TLS-terminated in front of us (see IsRequestEncrypted).
        // --require-tls forces the same regardless, even with no token at all.
        if ((options.RequireTls || options.AccessToken != null) && !IsRequestEncrypted(request))
        {
            CountRejection(ref rejectedUnencrypted, "unencrypted", ip, sessionTag);
            await Respond(426);
            return;
        }

        // Real Dalamud clients never send Origin; a browser tab always does. Rejecting it
        // outright closes off drive-by abuse from an arbitrary webpage with no allow-list to maintain.
        if (!string.IsNullOrEmpty(request.Header("Origin")))
        {
            CountRejection(ref rejectedOrigin, "origin header present", ip, sessionTag);
            await Respond(403);
            return;
        }

        // One shared lockout bucket for session-code guesses and --token guesses -- both cost
        // the same budget. Checked before the token comparison itself so a locked-out address
        // can't keep spending CPU on repeated guesses in the meantime.
        if (IsLockedOut(JoinLockoutUntilByIp, key))
        {
            CountRejection(ref rejectedJoinLockout, "auth locked out", ip, sessionTag);
            await Respond(429);
            return;
        }

        if (options.AccessToken != null && !IsValidToken(request.Header("X-AnoMech-Relay-Token"), options.AccessToken))
        {
            CountRejection(ref rejectedBadToken, "bad token", ip, sessionTag);
            RecordFailedAuth(FailedJoinsByIp, JoinLockoutUntilByIp, key, "join");
            await Respond(401);
            return;
        }

        if (AuthenticatePeer(request.Header("X-AnoMech-Protocol"), request.Header("X-AnoMech-Peer-Secret")) is not { } authenticatedPeerId)
        {
            // File-only like other per-request refusals: anything can send a bare upgrade request.
            CountRejection(ref rejectedBadProtocol, "missing or wrong protocol version or peer credential; the plugin and relay need to be the same version", ip, sessionTag);
            await Respond(400);
            return;
        }

        if (!TryReserveConnectionSlot(key))
        {
            CountRejection(ref rejectedIpCap, "ip connection cap", ip, sessionTag);
            await Respond(429);
            return;
        }

        WebSocket socket;
        try
        {
            socket = await RelayHttp.AcceptWebSocketAsync(stream, request, handshake.Token);
        }
        catch (Exception e)
        {
            ReleaseConnectionSlot(key);
            if (shutdown.IsCancellationRequested) return;
            if (e is OperationCanceledException)
            {
                CountRejection(ref rejectedHandshakeTimeout);
                log.Warn($"[{sessionTag}] WebSocket handshake from {ip} stalled past {HandshakeTimeout.TotalSeconds:F0}s -- abandoning.");
            }
            else log.Warn($"[{sessionTag}] WebSocket handshake from {ip} failed: {e.Message}");
            return;
        }

        Interlocked.Increment(ref totalConnectionsAccepted);
        var peer = new PeerConn(socket, (uint)Interlocked.Increment(ref nextConnectionId), ip, authenticatedPeerId);

        // The slot is released here and nowhere else, so no path between "reserved" and
        // "socket finished" can leak it -- including the rejection closes below, which each
        // wait on a close handshake the peer is free never to answer.
        try
        {
            string sessionCode;
            if (isHostRequest)
            {
                if (!TryCreateSession(peer, out sessionCode!))
                {
                    CountRejection(ref rejectedRelayFull, "relay full", ip, sessionTag);
                    await CloseQuietlyAsync(socket, WebSocketCloseStatus.PolicyViolation, "relay full");
                    return;
                }
            }
            else
            {
                sessionCode = joinCode!;
                if (!TryJoin(sessionCode, peer, out var reason))
                {
                    // Only "not found" is a guessing signal. A full room or a room ban both mean
                    // the code was right, and charging those toward the lockout would lock a
                    // whole household out of the relay over a banned player's retries.
                    if (reason == "session not found")
                    {
                        CountRejection(ref rejectedSessionNotFound, reason, ip, sessionTag);
                        RecordFailedAuth(FailedJoinsByIp, JoinLockoutUntilByIp, key, "join");
                    }
                    else CountRejection(ref reason == "banned from room" ? ref rejectedBanned : ref rejectedSessionFull, reason, ip, sessionTag);
                    await CloseQuietlyAsync(socket, WebSocketCloseStatus.PolicyViolation, reason);
                    return;
                }
            }

            try
            {
                await RunPeerAsync(peer, sessionCode, isHostRequest);
            }
            finally
            {
                Leave(sessionCode, peer);
                log.Info($"[{sessionCode}] peer #{peer.Id} left ({CountPeers(sessionCode)} connected, " +
                         $"{peer.MessagesIn} msgs / {peer.BytesIn / 1024} KB in, peak {peer.PeakMessagesPerSecond} msg/s " +
                         $"/ {peer.PeakBytesPerSecond / 1024} KB/s)");
            }
        }
        finally
        {
            socket.Dispose();
            ReleaseConnectionSlot(key);
        }
    }

    private async Task RunPeerAsync(PeerConn peer, string sessionCode, bool isHostRequest)
    {
        var socket = peer.Socket;
        try
        {
            log.Info($"[{sessionCode}] {(isHostRequest ? "session created" : "peer joined")} as #{peer.Id} from {peer.Ip} ({CountPeers(sessionCode)} connected)");
            await SendGreetingAsync(peer, isHostRequest ? sessionCode : null);
            if (!Sessions.TryGetValue(sessionCode, out var joinedRoom)) return;
            lock (joinedRoom.Lock)
            {
                if (!Sessions.TryGetValue(sessionCode, out var live) || !ReferenceEquals(live, joinedRoom)
                    || !joinedRoom.Peers.Contains(peer) || socket.State != WebSocketState.Open) return;
                peer.Ready = true;
            }

            // A large message (e.g. WorldSnapshotMessage) can arrive split across several
            // frames; buffer until EndOfMessage before forwarding, or fragments get broadcast
            // standalone and peers see truncated/corrupt JSON once a snapshot outgrows one frame.
            var readBuffer = new byte[16 * 1024];
            using var messageBuffer = new MemoryStream();
            // Per-connection, not per-IP -- a single connection sending far faster than any
            // legitimate client gets cut off regardless of which address it's coming from.
            var recentMessageTimes = new Queue<DateTime>();
            var recentBytes = new Queue<(DateTime At, int Bytes)>();
            long recentByteSum = 0;
            while (socket.State == WebSocketState.Open)
            {
                messageBuffer.SetLength(0);
                // Armed only once the first fragment lands: this bounds how long a message may
                // take to finish arriving, not how long a connection may sit idle between
                // messages (which is what IdleTimeout is for, at the room level).
                using var messageCts = new CancellationTokenSource();
                WebSocketReceiveResult result;
                var fragmentCount = 0;
                try
                {
                    do
                    {
                        result = await socket.ReceiveAsync(readBuffer, messageCts.Token);
                        if (fragmentCount == 0) messageCts.CancelAfter(MessageAssemblyTimeout);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ClosePeerAsync(peer, WebSocketCloseStatus.NormalClosure, null, linger: false);
                            return;
                        }
                        messageBuffer.Write(readBuffer, 0, result.Count);
                        if (messageBuffer.Length > options.MaxMessageBytes)
                        {
                            CountRejection(ref rejectedMessageTooLarge);
                            log.Warn($"[{sessionCode}] Message from {peer.Ip} exceeded {options.MaxMessageBytes} bytes -- aborting.");
                            socket.Abort();
                            return;
                        }
                        // Bounds fragment COUNT, not just total bytes -- a real client never
                        // sends anywhere near this many frames for one message; this only
                        // catches something deliberately splitting a message into many tiny
                        // frames to burn ReceiveAsync round-trips while staying under the size cap.
                        if (++fragmentCount > options.MaxFragmentsPerMessage)
                        {
                            CountRejection(ref rejectedTooManyFragments);
                            log.Warn($"[{sessionCode}] Message from {peer.Ip} exceeded {options.MaxFragmentsPerMessage} fragments -- aborting.");
                            socket.Abort();
                            return;
                        }
                    } while (!result.EndOfMessage);
                }
                // Only our own deadline is a slow message: an abort from elsewhere (a kick, a ban,
                // or the same identity reconnecting) cancels the receive too.
                catch (OperationCanceledException) when (messageCts.IsCancellationRequested)
                {
                    CountRejection(ref rejectedMessageTimeout);
                    log.Warn($"[{sessionCode}] Message from {peer.Ip} took over {MessageAssemblyTimeout.TotalSeconds:F0}s to arrive -- aborting.");
                    socket.Abort();
                    return;
                }

                var messageBytes = messageBuffer.ToArray();
                var now = DateTime.UtcNow;
                var cutoff = now - TimeSpan.FromSeconds(1);

                recentMessageTimes.Enqueue(now);
                while (recentMessageTimes.Count > 0 && recentMessageTimes.Peek() < cutoff)
                    recentMessageTimes.Dequeue();
                recentBytes.Enqueue((now, messageBytes.Length));
                recentByteSum += messageBytes.Length;
                while (recentBytes.Count > 0 && recentBytes.Peek().At < cutoff)
                    recentByteSum -= recentBytes.Dequeue().Bytes;

                peer.MessagesIn++;
                peer.BytesIn += messageBytes.Length;
                if (recentMessageTimes.Count > peer.PeakMessagesPerSecond) peer.PeakMessagesPerSecond = recentMessageTimes.Count;
                if (recentByteSum > peer.PeakBytesPerSecond) peer.PeakBytesPerSecond = recentByteSum;
                RecordMax(ref peakMessagesPerSecond, recentMessageTimes.Count);
                RecordMax(ref peakBytesPerSecond, recentByteSum);

                if (recentMessageTimes.Count > options.MaxMessagesPerSecond)
                {
                    CountRejection(ref rejectedMessageRate);
                    log.Warn($"[{sessionCode}] #{peer.Id} {peer.Ip} exceeded {options.MaxMessagesPerSecond} messages/sec -- aborting.");
                    socket.Abort();
                    return;
                }
                if (recentByteSum > options.MaxBytesPerSecond)
                {
                    CountRejection(ref rejectedByteRate);
                    log.Warn($"[{sessionCode}] #{peer.Id} {peer.Ip} exceeded {options.MaxBytesPerSecond / (1024 * 1024)} MB/sec -- aborting.");
                    socket.Abort();
                    return;
                }
                WarnIfNearLimit(peer, sessionCode, recentMessageTimes.Count, recentByteSum);

                // The one body the relay reads: a small, uncompressed control frame, obeyed only
                // from the room's host. Everything else is forwarded exactly as sent.
                if (result.MessageType == WebSocketMessageType.Text && RelayWire.IsControl(messageBytes))
                {
                    Moderate(sessionCode, peer, messageBytes);
                    continue;
                }
                var reachedPeers = await BroadcastAsync(sessionCode, peer, messageBytes, result.MessageType);
                // Detail-only -- this is the highest-volume event the relay sees, and echoing it
                // live would drown out everything else. Never the message body itself, only
                // shape/size/routing, matching the relay's "we don't log what you said" stance
                // (see README's Security notes).
                log.Detail($"[{sessionCode}] broadcast from #{peer.Id} {peer.Ip} type={result.MessageType} bytes={messageBytes.Length} " +
                           $"fragments={fragmentCount} rate={recentMessageTimes.Count}/s,{recentByteSum}B/s -> {reachedPeers} peer(s)");
            }
        }
        catch (WebSocketException)
        {
            // Peer dropped without a clean close handshake -- caller's finally runs Leave.
        }
        catch (OperationCanceledException)
        {
            // Aborted (kick, shutdown) -- same.
        }
    }

    // One line per connection per threshold crossing, not per message: the point is to notice
    // that a real scenario is creeping toward a cap, which a per-message line would bury.
    private void WarnIfNearLimit(PeerConn peer, string sessionCode, int messagesPerSecond, long bytesPerSecond)
    {
        if (peer.NearLimitWarned) return;
        var msgFraction = messagesPerSecond / (double)options.MaxMessagesPerSecond;
        var byteFraction = bytesPerSecond / (double)options.MaxBytesPerSecond;
        if (msgFraction < options.UsageWarnFraction && byteFraction < options.UsageWarnFraction) return;
        peer.NearLimitWarned = true;
        Interlocked.Increment(ref nearLimitWarnings);
        log.Warn($"[NEAR-LIMIT] [{sessionCode}] #{peer.Id} {peer.Ip} reached {messagesPerSecond} msg/s ({msgFraction:P0} of cap) " +
                 $"and {bytesPerSecond / 1024} KB/s ({byteFraction:P0} of cap). Raise --max-messages-per-second / " +
                 "--max-bytes-per-second if this is a legitimate run.");
    }

    // Path is already trimmed of leading/trailing slashes -- see RelayHttpRequest.Path.
    // Validated against the real alphabet/length, not just a length ceiling, so scanner/bot
    // garbage gets rejected as a bad request instead of doing a session lookup at all.
    private static string? ExtractSessionCode(string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 2 || parts[0] != "session") return null;
        var code = parts[1];
        return code.Length == CodeLength && code.All(CodeAlphabet.Contains) ? code : null;
    }

    // Timing-safe: a naive string comparison returns early on the first mismatched byte,
    // which lets a remote attacker recover the token one byte at a time from response timing.
    private static bool IsValidToken(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        // Compare a fixed-size hash of each instead of the raw (different-length) values --
        // FixedTimeEquals itself requires equal-length inputs, and short-circuiting on a
        // length check first would leak length the same way a naive compare leaks content.
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(a), SHA256.HashData(b));
    }

    // ---- Abuse bookkeeping ----------------------------------------------------------------

    private bool TryReserveConnectionSlot(IPAddress key)
    {
        lock (AbuseLock)
        {
            var count = ConnectionsByIp.GetValueOrDefault(key);
            if (count >= options.MaxConnectionsPerIp) return false;
            ConnectionsByIp[key] = count + 1;
            return true;
        }
    }

    private void ReleaseConnectionSlot(IPAddress key)
    {
        lock (AbuseLock)
        {
            if (!ConnectionsByIp.TryGetValue(key, out var count)) return;
            if (count <= 1) ConnectionsByIp.Remove(key);
            else ConnectionsByIp[key] = count - 1;
        }
    }

    private bool IsBanned(IPAddress key)
    {
        lock (AbuseLock) return BannedIps.Contains(key);
    }

    private bool IsLockedOut(Dictionary<IPAddress, DateTime> table, IPAddress key)
    {
        lock (AbuseLock)
            return table.TryGetValue(key, out var until) && until > DateTime.UtcNow;
    }

    // Sliding window of recent failures; crossing the threshold inside it trips a lockout.
    private void RecordFailedAuth(Dictionary<IPAddress, Queue<DateTime>> attemptsTable,
                                  Dictionary<IPAddress, DateTime> lockoutTable, IPAddress key, string kind)
    {
        var justTripped = false;
        lock (AbuseLock)
        {
            if (!attemptsTable.TryGetValue(key, out var attempts))
                attemptsTable[key] = attempts = new Queue<DateTime>();
            var now = DateTime.UtcNow;
            attempts.Enqueue(now);
            while (attempts.Count > 0 && now - attempts.Peek() > FailedJoinWindow) attempts.Dequeue();
            if (attempts.Count >= options.MaxFailedJoinsPerWindow)
            {
                justTripped = !(lockoutTable.TryGetValue(key, out var until) && until > now);
                lockoutTable[key] = now + JoinLockoutDuration;
            }
        }
        // Logged once at the moment it trips, not on every renewal while already locked out --
        // a real lockout is rare and worth an operator's attention live; a locked-out address
        // still hammering the endpoint is not new information.
        if (justTripped)
            log.Warn($"[AnoMech.Relay] {key} locked out of {kind} for {JoinLockoutDuration.TotalMinutes:F0}m after " +
                     $"{options.MaxFailedJoinsPerWindow}+ failed attempts in {FailedJoinWindow.TotalSeconds:F0}s.");
    }

    // ---- Room membership -------------------------------------------------------------------

    // The connection's identity is derived from a secret only the client holds, so a public id
    // seen in room traffic can't be used to act as someone else.
    internal static Guid? AuthenticatePeer(string? protocolHeader, string? secretHeader)
        => protocolHeader == RelayVersion.ToString() && RelayWire.IsValidSecret(secretHeader) ? RelayWire.PeerId(secretHeader!) : null;

    // Peer-join only -- a code nobody actually hosted is rejected immediately instead of
    // silently vivifying an empty room. Loops rather than a single TryGetValue+lock because
    // Leave() can retire (empty + remove) this exact room between the lookup and acquiring its
    // lock; the re-check inside the lock catches that race and retries against whatever's
    // actually current instead of joining a room that's already been thrown away.
    private bool TryJoin(string sessionCode, PeerConn peer, out string reason)
    {
        while (true)
        {
            if (!Sessions.TryGetValue(sessionCode, out var room))
            {
                reason = "session not found";
                return false;
            }
            PeerConn? replaced = null;
            List<PeerConn>? evicted = null;
            PeerConn? host = null;
            var banned = false;
            lock (room.Lock)
            {
                if (!Sessions.TryGetValue(sessionCode, out var current) || !ReferenceEquals(current, room))
                    continue; // retired (or replaced) concurrently -- retry against the live one
                // The host is exempt so banning someone on its own network can't lock it out.
                if (peer.PeerId != room.HostPeerId
                    && (room.Bans.ContainsKey(peer.PeerId) || room.Bans.Values.Contains(AbuseKey(peer.Ip))))
                {
                    banned = true;
                    // A ban made while this identity was disconnected learns its address now, and
                    // clears that address the way a ban on a connected player would have.
                    if (room.Bans.TryGetValue(peer.PeerId, out var bannedAddress) && bannedAddress is null)
                    {
                        var address = AbuseKey(peer.Ip);
                        room.Bans[peer.PeerId] = address;
                        evicted = room.Peers.Where(p => p.PeerId != room.HostPeerId && AbuseKey(p.Ip).Equals(address)).ToList();
                        foreach (var other in evicted)
                        {
                            other.Ready = false;
                            room.Peers.Remove(other);
                        }
                        host = room.Peers.FirstOrDefault(p => p.PeerId == room.HostPeerId);
                    }
                }
                else
                {
                    // One connection per identity: a reconnect replaces a connection that hasn't
                    // noticed it's dead yet, the host's included.
                    replaced = room.Peers.FirstOrDefault(p => p.PeerId == peer.PeerId);
                    if (room.Peers.Count - (replaced == null ? 0 : 1) >= options.MaxPeersPerSession)
                    {
                        reason = "session full";
                        return false;
                    }
                    if (replaced != null)
                    {
                        replaced.Ready = false;
                        room.Peers.Remove(replaced);
                    }
                    room.Peers.Add(peer);
                    room.LastActivityUtc = DateTime.UtcNow;
                }
            }
            if (banned)
            {
                if (evicted is { Count: > 0 })
                {
                    foreach (var other in evicted)
                        _ = ClosePeerAsync(other, WebSocketCloseStatus.PolicyViolation, "banned from room");
                    if (host != null) _ = SendNoticeAsync(host, evicted.Select(p => p.PeerId).Distinct().ToArray());
                }
                reason = "banned from room";
                return false;
            }
            replaced?.Socket.Abort();
            reason = "";
            return true;
        }
    }

    private bool TryCreateSession(PeerConn host, out string sessionCode)
    {
        // A soft cap now, not a hard one -- a burst of concurrent /host requests right at the
        // ceiling could transiently overshoot it by a few. MaxTotalSessions exists to bound
        // resource usage, not as a security invariant, so this is an acceptable trade for not
        // needing a relay-wide lock on every session creation.
        if (Sessions.Count >= options.MaxTotalSessions)
        {
            sessionCode = "";
            return false;
        }
        var room = new Room { HostPeerId = host.PeerId };
        room.Peers.Add(host);
        string code;
        do { code = GenerateCode(); } while (!Sessions.TryAdd(code, room));
        sessionCode = code;
        return true;
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        // CodeAlphabet.Length (32) divides 256 evenly, so byte % 32 is exactly uniform --
        // no rejection sampling needed.
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        for (var i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        return new string(chars);
    }

    private void Leave(string sessionCode, PeerConn peer)
    {
        if (!Sessions.TryGetValue(sessionCode, out var room)) return;
        // Removal from Sessions happens while still holding this room's own lock -- the one
        // point that has to agree with TryJoin's re-check above, so a peer can never be added
        // to a room in the instant between it going empty and being removed from the table.
        lock (room.Lock)
        {
            peer.Ready = false;
            room.Peers.Remove(peer);
            if (room.Peers.Count == 0) Sessions.TryRemove(new KeyValuePair<string, Room>(sessionCode, room));
        }
    }

    private int CountPeers(string sessionCode)
    {
        if (!Sessions.TryGetValue(sessionCode, out var room)) return 0;
        lock (room.Lock) return room.Peers.Count;
    }

    // peerId tells the client which identity the relay derived from its secret.
    private async Task SendGreetingAsync(PeerConn peer, string? assignedSessionCode)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            relayVersion = RelayVersion, capabilities = RelayCapabilities,
            sessionCode = assignedSessionCode, peerId = peer.PeerId,
        });
        using var timeout = new CancellationTokenSource(SendTimeout);
        await SendOneAsync(peer, bytes, WebSocketMessageType.Text, timeout.Token);
    }

    // A malformed command is ignored rather than closing the sender: the sender is the host.
    private void Moderate(string sessionCode, PeerConn sender, byte[] message)
    {
        string? operation;
        var id = Guid.Empty;
        try
        {
            using var json = JsonDocument.Parse(message);
            if (!json.RootElement.TryGetProperty("Operation", out var action) || action.ValueKind != JsonValueKind.String
                || !json.RootElement.TryGetProperty("PeerId", out var identity) || identity.ValueKind != JsonValueKind.String
                || !identity.TryGetGuid(out id)) return;
            operation = action.GetString();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return; }
        if (!Sessions.TryGetValue(sessionCode, out var room)) return;
        List<PeerConn> removed;
        lock (room.Lock)
        {
            if (!sender.Ready || !room.Peers.Contains(sender) || sender.PeerId != room.HostPeerId) return;
            if (operation == "unban") { room.Bans.Remove(id); return; }
            if (operation is not ("kick" or "ban") || id == room.HostPeerId) return;
            var target = room.Peers.FirstOrDefault(p => p.PeerId == id);
            // Recorded even when they aren't connected: a ban issued between their reconnects
            // still has to stop the next one. A full ban list still kicks.
            if (operation == "ban" && (room.Bans.ContainsKey(id) || room.Bans.Count < MaxRoomBans))
                room.Bans[id] = target is null ? room.Bans.GetValueOrDefault(id) : AbuseKey(target.Ip);
            if (target == null) return;
            removed = room.Peers.Where(p => ReferenceEquals(p, target)
                || (operation == "ban" && p.PeerId != room.HostPeerId && AbuseKey(p.Ip).Equals(AbuseKey(target.Ip)))).ToList();
            foreach (var peer in removed)
            {
                peer.Ready = false;
                room.Peers.Remove(peer);
            }
        }
        log.Info($"[{sessionCode}] host {operation} of {id}: {removed.Count} connection(s) closed.");
        foreach (var peer in removed)
            _ = ClosePeerAsync(peer, WebSocketCloseStatus.PolicyViolation, operation == "ban" ? "banned from room" : "kicked from room");
        // The host removed only the id it named. Anyone else the ban took with them leaves
        // without a word of their own, so without this they'd stay seated in its roster.
        var others = removed.Where(p => p.PeerId != id).Select(p => p.PeerId).Distinct().ToArray();
        if (others.Length > 0) _ = SendNoticeAsync(sender, others);
    }

    private async Task SendNoticeAsync(PeerConn host, Guid[] removed)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { t = RelayWire.NoticeType, Removed = removed });
        using var timeout = new CancellationTokenSource(SendTimeout);
        await SendOneAsync(host, RelayWire.Envelope(false, 0, Guid.Empty, false, body), WebSocketMessageType.Binary, timeout.Token);
    }

    // ---- Public / admin HTTP ---------------------------------------------------------------

    // Unauthenticated by design -- a client must learn whether a token is needed BEFORE it
    // has one, or the plugin's "only show the password box if required" UI has no way to
    // decide. Nothing here is sensitive (it's the same info the WS greeting already sends,
    // minus a live session code).
    private async Task ServeInfoAsync(Stream stream, RelayHttpRequest request, CancellationToken token)
    {
        // /info itself carries no secret, but --require-tls means no exceptions -- keeps the
        // policy simple (nothing talks to this relay unencrypted) rather than case-by-case.
        if (options.RequireTls && !IsRequestEncrypted(request))
        {
            CountRejection(ref rejectedUnencrypted, "unencrypted", ResolveClientIp(request), "info");
            await RelayHttp.WriteStatusAsync(stream, 426, token);
            return;
        }
        var json = $$"""{"relayVersion":{{RelayVersion}},"capabilities":[{{RelayCapabilitiesJson}}],"requiresToken":{{(options.AccessToken != null ? "true" : "false")}}}""";
        await RelayHttp.WriteJsonAsync(stream, json, token);
    }

    // The handler runs only once the request is authorized, so an action body is never read
    // from anyone without the admin token.
    private async Task ServeAdminAsync(Stream stream, RelayHttpRequest request, CancellationToken token, Func<Task<string>> handler)
    {
        // 404, not 401 -- a relay with no --admin-token set shouldn't even reveal these exist.
        if (options.AdminToken is null)
        {
            await RelayHttp.WriteStatusAsync(stream, 404, token);
            return;
        }
        var ip = ResolveClientIp(request);
        var key = AbuseKey(ip);
        if (!IsRequestEncrypted(request))
        {
            CountRejection(ref rejectedUnencrypted, "unencrypted", ip, "admin");
            await RelayHttp.WriteStatusAsync(stream, 426, token);
            return;
        }
        if (IsLockedOut(AdminLockoutUntilByIp, key))
        {
            CountRejection(ref rejectedJoinLockout, "admin locked out", ip, "admin");
            await RelayHttp.WriteStatusAsync(stream, 429, token);
            return;
        }
        if (!IsValidToken(request.Header("X-AnoMech-Admin-Token"), options.AdminToken))
        {
            CountRejection(ref rejectedBadToken, "bad admin token", ip, "admin");
            RecordFailedAuth(FailedAdminByIp, AdminLockoutUntilByIp, key, "admin");
            await RelayHttp.WriteStatusAsync(stream, 401, token);
            return;
        }
        string json;
        try
        {
            json = await handler();
        }
        catch (Exception e)
        {
            log.Warn($"[admin] request from {ip} failed: {e.Message}");
            await RelayHttp.WriteStatusAsync(stream, 400, token);
            return;
        }
        await RelayHttp.WriteJsonAsync(stream, json, token);
    }

    // ---- Admin model ------------------------------------------------------------------------

    private LimitSettings BuildLimits() => new(
        options.MaxPeersPerSession, options.MaxTotalSessions, options.MaxMessageBytes, options.MaxConnectionsPerIp,
        options.MaxMessagesPerSecond, options.MaxBytesPerSecond, options.MaxFragmentsPerMessage, options.MaxFailedJoinsPerWindow,
        options.UsageWarnFraction);

    public AdminStats GetStats()
    {
        var sessionCount = Sessions.Count;
        var totalPeers = Sessions.Values.Sum(r => { lock (r.Lock) return r.Peers.Count; });
        int ipCount, lockoutCount, bannedCount;
        lock (AbuseLock)
        {
            ipCount = ConnectionsByIp.Count;
            lockoutCount = JoinLockoutUntilByIp.Count(kv => kv.Value > DateTime.UtcNow);
            bannedCount = BannedIps.Count;
        }
        return new AdminStats(
            (DateTime.UtcNow - startedAtUtc).TotalSeconds, RelayVersion, sessionCount, totalPeers, ipCount,
            lockoutCount, bannedCount, acceptingConnections,
            Interlocked.Read(ref totalConnectionsAccepted), Interlocked.Read(ref totalMessagesBroadcast), Interlocked.Read(ref totalBytesBroadcast),
            new RejectionCounts(
                Interlocked.Read(ref rejectedOrigin), Interlocked.Read(ref rejectedIpCap), Interlocked.Read(ref rejectedJoinLockout),
                Interlocked.Read(ref rejectedRelayFull), Interlocked.Read(ref rejectedSessionFull), Interlocked.Read(ref rejectedSessionNotFound),
                Interlocked.Read(ref rejectedBadToken), Interlocked.Read(ref rejectedHandshakeTimeout), Interlocked.Read(ref rejectedMessageTooLarge),
                Interlocked.Read(ref rejectedMessageTimeout), Interlocked.Read(ref rejectedMessageRate), Interlocked.Read(ref rejectedByteRate),
                Interlocked.Read(ref rejectedUnencrypted), Interlocked.Read(ref rejectedTooManyFragments),
                Interlocked.Read(ref rejectedBanned), Interlocked.Read(ref rejectedPaused), Interlocked.Read(ref rejectedBadProtocol)),
            Interlocked.Read(ref recentRejections),
            Interlocked.Read(ref peakMessagesPerSecond), Interlocked.Read(ref peakBytesPerSecond),
            Interlocked.Read(ref nearLimitWarnings), BuildLimits(),
            GC.GetTotalMemory(false), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }

    public List<SessionInfo> GetSessions()
    {
        var now = DateTime.UtcNow;
        var list = new List<SessionInfo>();
        foreach (var (code, room) in Sessions)
        {
            lock (room.Lock)
            {
                list.Add(new SessionInfo(code, (now - room.CreatedUtc).TotalSeconds, (now - room.LastActivityUtc).TotalSeconds,
                    room.Peers.Select(peer => new PeerInfo(peer.Id, peer.Ip.ToString(),
                        peer.PeerId == room.HostPeerId, (now - peer.JoinedUtc).TotalSeconds,
                        peer.MessagesIn, peer.BytesIn, peer.PeakMessagesPerSecond, peer.PeakBytesPerSecond)).ToList()));
            }
        }
        return list.OrderBy(s => s.Code).ToList();
    }

    private string ApplyAdminActionBody(string body, IPAddress from)
    {
        var request = JsonSerializer.Deserialize<AdminActionRequest>(body, RelayAdmin.Json)
                      ?? throw new InvalidOperationException("empty action body");
        var result = ApplyAdminAction(request);
        log.Warn($"[admin] {request.Action} from {from}: {result.Message}");
        return JsonSerializer.Serialize(result, RelayAdmin.Json);
    }

    public AdminActionResult ApplyAdminAction(AdminActionRequest request)
    {
        switch (request.Action)
        {
            case "kick-peer":
            {
                if (request.ConnectionId is not { } id) return new AdminActionResult(false, "kick-peer needs a connectionId");
                foreach (var (code, room) in Sessions)
                {
                    PeerConn? target;
                    lock (room.Lock) target = room.Peers.FirstOrDefault(peer => peer.Id == id);
                    if (target == null) continue;
                    target.Socket.Abort();
                    return new AdminActionResult(true, $"kicked #{id} ({target.Ip}) from {code}");
                }
                return new AdminActionResult(false, $"no live connection #{id}");
            }
            case "disband-session":
            {
                if (request.SessionCode is not { Length: > 0 } code) return new AdminActionResult(false, "disband-session needs a sessionCode");
                if (!Sessions.TryGetValue(code, out var room)) return new AdminActionResult(false, $"no session {code}");
                List<PeerConn> peers;
                lock (room.Lock)
                {
                    peers = new List<PeerConn>(room.Peers);
                    Sessions.TryRemove(new KeyValuePair<string, Room>(code, room));
                }
                foreach (var peer in peers) peer.Socket.Abort();
                return new AdminActionResult(true, $"disbanded {code} ({peers.Count} peers)");
            }
            case "ban-ip":
            case "unban-ip":
            {
                if (!IPAddress.TryParse(request.Ip, out var address)) return new AdminActionResult(false, "ban-ip/unban-ip needs a valid ip");
                var key = AbuseKey(address);
                var banning = request.Action == "ban-ip";
                lock (AbuseLock)
                {
                    if (banning) BannedIps.Add(key);
                    else BannedIps.Remove(key);
                }
                if (!banning) return new AdminActionResult(true, $"unbanned {key}");
                var dropped = 0;
                foreach (var (_, room) in Sessions)
                {
                    List<PeerConn> matches;
                    lock (room.Lock) matches = room.Peers.Where(peer => AbuseKey(peer.Ip).Equals(key)).ToList();
                    foreach (var peer in matches) { peer.Socket.Abort(); dropped++; }
                }
                return new AdminActionResult(true, $"banned {key}, dropped {dropped} live connection(s)");
            }
            case "clear-lockouts":
                lock (AbuseLock)
                {
                    JoinLockoutUntilByIp.Clear();
                    AdminLockoutUntilByIp.Clear();
                    FailedJoinsByIp.Clear();
                    FailedAdminByIp.Clear();
                }
                return new AdminActionResult(true, "cleared all lockouts and failure counters");
            case "pause":
                acceptingConnections = false;
                return new AdminActionResult(true, "paused -- no new connections accepted");
            case "resume":
                acceptingConnections = true;
                return new AdminActionResult(true, "resumed");
            case "set-limit":
            {
                if (request.Name is not { Length: > 0 } name || request.Value is not { } value)
                    return new AdminActionResult(false, "set-limit needs name and value");
                switch (name)
                {
                    case "max-messages-per-second": options.MaxMessagesPerSecond = (int)value; break;
                    case "max-bytes-per-second": options.MaxBytesPerSecond = (long)value; break;
                    case "max-message-bytes": options.MaxMessageBytes = (long)value; break;
                    case "max-connections-per-ip": options.MaxConnectionsPerIp = (int)value; break;
                    case "max-peers-per-session": options.MaxPeersPerSession = (int)value; break;
                    case "max-sessions": options.MaxTotalSessions = (int)value; break;
                    case "max-failed-joins": options.MaxFailedJoinsPerWindow = (int)value; break;
                    case "max-fragments-per-message": options.MaxFragmentsPerMessage = (int)value; break;
                    case "usage-warn-fraction": options.UsageWarnFraction = value; break;
                    default: return new AdminActionResult(false, $"unknown limit '{name}'");
                }
                return new AdminActionResult(true, $"{name} = {value}");
            }
            default:
                return new AdminActionResult(false, $"unknown action '{request.Action}'");
        }
    }

    // ---- Background maintenance -------------------------------------------------------------

    // Runs for the relay's whole lifetime, alongside the accept loop. Also prunes stale per-IP
    // abuse-tracking entries so a long-running relay doesn't accumulate one dictionary entry
    // per distinct attacker IP forever, and periodically logs a summary line plus an [ALERT]
    // if rejections spiked -- see AlertRejectionThresholdPerTick.
    //
    // Every control in here is timed, so the loop must never be able to die or stall: an
    // unguarded throw or one unresponsive socket would silently stop idle-session reaping,
    // abuse-table pruning and the alert signal all at once.
    private async Task ReapLoop()
    {
        var sinceLastSummary = TimeSpan.Zero;
        while (true)
        {
            try
            {
                await Task.Delay(ReapInterval, shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                await ReapOnceAsync();
                sinceLastSummary += ReapInterval;
                if (sinceLastSummary >= SummaryLogInterval)
                {
                    sinceLastSummary = TimeSpan.Zero;
                    LogSummary();
                }
            }
            catch (Exception e)
            {
                log.Warn($"[AnoMech.Relay] Reap tick failed: {e} -- continuing.");
            }
        }
    }

    private async Task ReapOnceAsync()
    {
        List<(string Code, List<PeerConn> Peers)> dead = new();
        // Enumerating a ConcurrentDictionary while calling TryRemove on it is safe (unlike
        // a plain Dictionary) -- no snapshot copy needed first.
        var cutoff = DateTime.UtcNow - IdleTimeout;
        foreach (var (code, room) in Sessions)
        {
            lock (room.Lock)
            {
                if (room.LastActivityUtc >= cutoff) continue;
                dead.Add((code, new List<PeerConn>(room.Peers)));
                Sessions.TryRemove(new KeyValuePair<string, Room>(code, room));
            }
        }
        foreach (var (code, peers) in dead)
        {
            log.Info($"[{code}] idle for over {IdleTimeout.TotalSeconds:F0}s -- disbanding ({peers.Count} connected).");
            await Task.WhenAll(peers.Select(peer =>
                CloseQuietlyAsync(peer.Socket, WebSocketCloseStatus.EndpointUnavailable, "session idle timeout")));
        }

        lock (AbuseLock)
        {
            var now = DateTime.UtcNow;
            PruneLockouts(JoinLockoutUntilByIp, FailedJoinsByIp, now);
            PruneLockouts(AdminLockoutUntilByIp, FailedAdminByIp, now);
        }

        var recent = Interlocked.Exchange(ref recentRejections, 0);
        if (recent >= AlertRejectionThresholdPerTick)
            log.Warn($"[ALERT] {recent} rejected connections in the last {ReapInterval.TotalSeconds:F0}s -- possible abuse in progress.");
    }

    private static void PruneLockouts(Dictionary<IPAddress, DateTime> lockouts,
                                      Dictionary<IPAddress, Queue<DateTime>> attempts, DateTime now)
    {
        foreach (var ip in lockouts.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
            lockouts.Remove(ip);
        foreach (var (ip, queue) in attempts.ToList())
        {
            while (queue.Count > 0 && now - queue.Peek() > FailedJoinWindow) queue.Dequeue();
            if (queue.Count == 0) attempts.Remove(ip);
        }
    }

    private void LogSummary()
    {
        var stats = GetStats();
        log.Info($"[Summary] {stats.Sessions} sessions, {stats.TotalPeers} peers, {stats.ConnectionsByIpCount} distinct IPs live, "
            + $"{stats.ActiveJoinLockouts} active lockouts, {stats.TotalConnectionsAccepted} connections accepted lifetime.");
        log.Info($"[Summary] Peak per-connection load: {stats.PeakMessagesPerSecond} msg/s "
            + $"({stats.PeakMessagesPerSecond / (double)options.MaxMessagesPerSecond:P0} of cap), "
            + $"{stats.PeakBytesPerSecond / 1024} KB/s ({stats.PeakBytesPerSecond / (double)options.MaxBytesPerSecond:P0} of cap), "
            + $"{stats.NearLimitWarnings} near-limit warning(s).");
        foreach (var session in GetSessions())
            log.Detail($"[{session.Code}] usage: {session.Peers.Count} peers, "
                + string.Join("; ", session.Peers.Select(peer =>
                    $"#{peer.Id} {peer.MessagesIn}msg/{peer.BytesIn / 1024}KB peak {peer.PeakMessagesPerSecond}/s,{peer.PeakBytesPerSecond / 1024}KB/s")));
    }

    // ---- Fan-out -----------------------------------------------------------------------------

    // Returns how many peers it actually reached -- the caller logs that alongside the message
    // shape (see the Detail call in RunPeerAsync) without needing its own session lookup.
    private async Task<int> BroadcastAsync(string sessionCode, PeerConn sender, byte[] bytes, WebSocketMessageType type)
    {
        if (!Sessions.TryGetValue(sessionCode, out var room)) return 0;
        List<PeerConn> targets;
        bool isFromHost;
        // Only THIS room's lock, not a relay-wide one -- unrelated sessions broadcasting at the
        // same time never contend with each other here, only two sends to the same session would.
        lock (room.Lock)
        {
            if (!Sessions.TryGetValue(sessionCode, out var current) || !ReferenceEquals(current, room)) return 0;
            if (!sender.Ready || !room.Peers.Contains(sender)) return 0;
            room.LastActivityUtc = DateTime.UtcNow;
            isFromHost = sender.PeerId == room.HostPeerId;
            // A peer's traffic goes to the host alone; only the host speaks to everyone.
            targets = room.Peers.Where(peer => !ReferenceEquals(peer, sender) && peer.Ready && peer.Socket.State == WebSocketState.Open
                && (isFromHost || peer.PeerId == room.HostPeerId)).ToList();
        }

        // Prefix identifying the ORIGINAL sender (see RelayWire.Envelope): host flag, the
        // relay-assigned connection id, the authenticated identity, and whether the body is
        // compressed. All of it is written here, never taken from the client, which is what makes
        // it unforgeable. Identity bytes need not be UTF-8, so the frame is always binary.
        var tagged = RelayWire.Envelope(isFromHost, sender.Id, sender.PeerId, type == WebSocketMessageType.Binary, bytes);

        Interlocked.Increment(ref totalMessagesBroadcast);
        Interlocked.Add(ref totalBytesBroadcast, tagged.Length * (long)targets.Count);

        // One shared CancellationTokenSource for the whole fan-out instead of one per target --
        // every send below starts at essentially the same instant (WhenAll launches them all
        // before awaiting), so a shared deadline is functionally identical to a per-target one
        // while costing O(1) timer/CTS allocations per broadcast instead of O(peers). At
        // hundreds of sessions this adds up fast otherwise (see README's Security notes).
        using var cts = new CancellationTokenSource(SendTimeout);
        // Parallel, not sequential -- one slow peer must not delay delivery to everyone else.
        await Task.WhenAll(targets.Select(target => SendOneAsync(target, tagged, WebSocketMessageType.Binary, cts.Token)));
        return targets.Count;
    }

    // A timed-out send is treated as fatal for that connection (aborted, not skipped): a
    // cancelled send can leave a half-written frame in the OS buffer, and reusing the
    // connection risks interleaving a fresh frame with that leftover -- corrupting the
    // stream from then on. Abort lets both sides' own receive loops notice and clean up.
    internal async Task SendOneAsync(PeerConn target, byte[] bytes, WebSocketMessageType type, CancellationToken timeout)
    {
        try
        {
            await target.SendGate.WaitAsync(timeout);
            try { await target.Socket.SendAsync(bytes, type, endOfMessage: true, timeout); }
            finally { target.SendGate.Release(); }
        }
        catch (OperationCanceledException)
        {
            log.Warn($"[Relay] Send to #{target.Id} timed out after {SendTimeout.TotalSeconds:F0}s -- aborting that connection.");
            target.Socket.Abort();
        }
        catch (WebSocketException)
        {
            // Dead socket -- its own receive loop will observe the failure and Leave().
        }
        catch (ObjectDisposedException)
        {
            // Raced with a kick/teardown -- same.
        }
    }

    // CloseAsync waits for the peer's own close frame, which a hostile or wedged client is
    // free never to send. Every close in this process goes through here so none of them can
    // block on that.
    // A close is a send too, so it waits its turn behind any broadcast to the same socket. A
    // close the relay starts (kick, ban) lingers so the client can read why before the abort;
    // answering the client's own close has nothing left to wait for.
    private static async Task ClosePeerAsync(PeerConn peer, WebSocketCloseStatus status, string? description, bool linger = true)
    {
        using var timeout = new CancellationTokenSource(CloseTimeout);
        try
        {
            await peer.SendGate.WaitAsync(timeout.Token);
            try { await peer.Socket.CloseOutputAsync(status, description, timeout.Token); }
            finally { peer.SendGate.Release(); }
            if (linger) await Task.Delay(CloseTimeout);
        }
        catch (Exception) { }
        finally { peer.Socket.Abort(); }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket, WebSocketCloseStatus status, string? description)
    {
        try
        {
            using var cts = new CancellationTokenSource(CloseTimeout);
            await socket.CloseAsync(status, description, cts.Token);
        }
        catch (Exception)
        {
            try { socket.Abort(); } catch { /* best effort */ }
        }
    }
}
