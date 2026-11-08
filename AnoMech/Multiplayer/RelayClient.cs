using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Network;

namespace AnoMech.Multiplayer;

// The relay rejected the connection with a close frame right after the upgrade (see
// ReadGreetingAsync), almost always "session not found" after a relay restart. Retrying the
// same code can never succeed, so the reconnect loop gives up instead of backing off forever.
// MovedTo: the server in the relay's group that holds the session (RelayWire.MovedTo).
internal sealed class RelaySessionRejectedException(string reason, string? movedTo = null) : Exception(reason)
{
    public string? MovedTo { get; } = movedTo;
}

// A server's /info. Server and Group are set on a relay run as one of a group (RelayGroups).
internal sealed record RelayInfo(int RelayVersion, bool RequiresToken, int? Server, RelayGroupMember[]? Group)
{
    // Came over https, which the server itself can't vouch for.
    [JsonIgnore] public bool Encrypted { get; init; }
}

// ClientWebSocket wrapper for AnoMech.Relay (AnoMech.Relay.Host/README.md). The relay forwards bodies
// unread, prefixed with who sent them (RelayWire.Envelope): the identity it derived from that
// sender's secret, so every check on what a message says is made here, by the receiver.
// Outgoing, the WebSocket message type doubles as the compression flag: Text = raw JSON,
// Binary = Brotli (from CompressionThresholdBytes up; below that Brotli's framing costs more
// than it saves).
public sealed class RelayClient(string peerSecret) : IRelayConnection
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int CompressionThresholdBytes = 256;
    // ClientWebSocket's own handler follows redirects and resends every request header, the
    // peer secret and relay password included, to wherever a relay points it.
    private static readonly HttpMessageInvoker WebSocketHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

    // Replaced per fallback attempt: a ClientWebSocket can only ConnectAsync once.
    private ClientWebSocket socket = new();
    private readonly CancellationTokenSource cts = new();
    // One queue: what the host sends is an ordered stream (a run's packets, then the end that
    // follows them).
    private readonly Channel<(byte[] Bytes, WebSocketMessageType Type, TaskCompletionSource Completion)> sendQueue =
        Channel.CreateUnbounded<(byte[], WebSocketMessageType, TaskCompletionSource)>();
    private bool disposed;

    // The relay is untrusted; 2 MB is ~100x the largest real message.
    private const int MaxIncomingMessageBytes = 2 * 1024 * 1024;
    private const int MaxIncomingFragments = 4096;

    // (message, isFromHost, relay-assigned sender connection id, authenticated sender identity)
    // -- see IHostOnlyMessage.
    public event Action<MpMessage, bool, uint, Guid>? MessageReceived;
    // A message dropped for failing to decode, validate or deserialize: (sender, fromHost, why).
    public event Action<Guid, bool, string>? MessageRejected;
    // Others the relay removed along with a banned player (same address); host only.
    public event Action<IReadOnlyList<Guid>>? PeersRemoved;
    public event Action<Exception?>? Disconnected;

    public bool IsConnected => socket.State == WebSocketState.Open;

    // The scheme actually dialed; the relay can't say (wss:// terminates in a reverse proxy).
    public bool IsEncrypted { get; private set; }

    // Distinct from a ws:// the user typed on purpose.
    public bool FellBackToUnencrypted { get; private set; }

    // From the relay's greeting; a missing entry just means "not supported".
    public IReadOnlySet<string> RelayCapabilities { get; private set; } = new HashSet<string>();
    public bool HasRelayCapability(string name) => RelayCapabilities.Contains(name);
    public bool SupportsCompression => HasRelayCapability("binaryCompression");
    public bool SupportsSenderIdentity => HasRelayCapability("authenticatedIdentity");

    // Each connection's usage is counted from zero: the caps it is measured against are
    // per-connection too.
    public Task ConnectAsync(string relayUrl, string sessionCode, string? accessToken = null)
    {
        RelayStats.Reset();
        return ConnectCoreAsync(relayUrl, JoinPath(sessionCode), accessToken);
    }

    // Only the relay can guarantee a collision-free session code.
    public Task<string?> ConnectAndHostAsync(string relayUrl, string? accessToken = null)
    {
        RelayStats.Reset();
        return ConnectCoreAsync(relayUrl, HostPath, accessToken);
    }

    internal const string HostPath = "host";

    internal static string JoinPath(string sessionCode) => $"session/{Uri.EscapeDataString(sessionCode)}";

    // What the relay authenticates a connection by, sent with its upgrade request.
    internal static IEnumerable<(string Name, string Value)> UpgradeHeaders(string peerSecret, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) yield return ("X-AnoMech-Relay-Token", accessToken);
        yield return ("X-AnoMech-Peer-Secret", peerSecret);
        yield return ("X-AnoMech-Protocol", RelayWire.Version.ToString());
    }

    // A bare host tries wss:// then falls back to ws://; the only way to learn whether a server
    // speaks TLS is to try. Default ports match AnoMech.Relay.Host/README.md (443 behind Caddy, 7890
    // direct). An explicit scheme is tried once, literally.
    private static IReadOnlyList<Uri> ResolveCandidateUris(string relayUrl, string path)
    {
        var trimmed = relayUrl.Trim();
        var explicitScheme = trimmed.IndexOf("://", StringComparison.Ordinal) is var idx && idx > 0
            ? trimmed[..idx].ToLowerInvariant()
            : null;
        if (explicitScheme is not null)
        {
            var mapped = explicitScheme switch { "https" => "wss", "http" => "ws", _ => explicitScheme };
            return [BuildUri($"{mapped}://{trimmed[(idx + 3)..]}", path)];
        }

        var (host, explicitPort) = SplitHostPort(trimmed);
        return
        [
            BuildUri($"wss://{host}:{explicitPort ?? 443}", path),
            BuildUri($"ws://{host}:{explicitPort ?? 7890}", path),
        ];
    }

    // A probe scheme with no default port: "ws://host" would resolve an unspecified port to 80.
    private static (string Host, int? Port) SplitHostPort(string hostAndOptionalPort)
    {
        if (!Uri.TryCreate($"anomech-probe://{hostAndOptionalPort}", UriKind.Absolute, out var probe))
            return (hostAndOptionalPort, null);
        return (probe.Host, probe.Port < 0 ? null : probe.Port);
    }

    private static Uri BuildUri(string baseUrl, string path) => new($"{baseUrl.TrimEnd('/')}/{path}");

    // Same host/port resolution mapped to https/http; /info lives on the same host:port as the
    // WS endpoint.
    private static IReadOnlyList<Uri> ResolveInfoCandidateUris(string relayUrl)
    {
        var trimmed = relayUrl.Trim();
        var explicitScheme = trimmed.IndexOf("://", StringComparison.Ordinal) is var idx && idx > 0
            ? trimmed[..idx].ToLowerInvariant()
            : null;
        if (explicitScheme is not null)
        {
            var mapped = explicitScheme switch { "wss" => "https", "ws" => "http", _ => explicitScheme };
            return [BuildUri($"{mapped}://{trimmed[(idx + 3)..]}", "info")];
        }
        var (host, explicitPort) = SplitHostPort(trimmed);
        return
        [
            BuildUri($"https://{host}:{explicitPort ?? 443}", "info"),
            BuildUri($"http://{host}:{explicitPort ?? 7890}", "info"),
        ];
    }

    private static readonly HttpClient InfoHttpClient = new() { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 64 * 1024 };

    // Plain HTTP, so the UI can learn whether a relay needs a token before the user has one.
    // Null = unreachable or an old relay without /info; callers assume no token needed.
    internal static async Task<RelayInfo?> FetchInfoAsync(string relayUrl, CancellationToken ct = default)
    {
        foreach (var uri in ResolveInfoCandidateUris(relayUrl))
        {
            try
            {
                var json = await InfoHttpClient.GetStringAsync(uri, ct).ConfigureAwait(false);
                if (JsonSerializer.Deserialize<RelayInfo>(json, JsonOptions) is { } info) return info with { Encrypted = uri.Scheme == Uri.UriSchemeHttps };
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                DiagnosticLog.Info($"[RelayClient] /info fetch from {uri} failed: {e.Message}");
            }
        }
        return null;
    }

    // While a fallback candidate remains, an attempt still unanswered after this is abandoned for
    // it: a plain relay older than RelayHttp's TLS check holds a wss:// attempt for its 10 s
    // handshake timeout, and a port that silently drops packets holds it until the OS gives up.
    // Far above a normal connect, and leaves most of MultiplayerManager's 15 s join deadline.
    internal TimeSpan FallbackAfter { get; init; } = TimeSpan.FromSeconds(5);

    private async Task<string?> ConnectCoreAsync(string relayUrl, string path, string? accessToken)
    {
        var candidates = ResolveCandidateUris(relayUrl, path);
        // A password must never go over ws://, whether from the fallback or typed explicitly
        // (the relay enforces the same; see Program.cs IsRequestEncrypted).
        if (!string.IsNullOrEmpty(accessToken))
        {
            var encryptedOnly = candidates.Where(u => u.Scheme == "wss").ToList();
            if (encryptedOnly.Count == 0)
            {
                var reason = "A relay password is set, but this connection isn't encrypted (wss://) -- refusing to send it in plaintext.";
                DiagnosticLog.Warn($"[RelayClient] {reason}");
                Disconnected?.Invoke(new InvalidOperationException(reason));
                return null;
            }
            candidates = encryptedOnly;
        }
        Exception? lastFailure = null;
        Uri? connected = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (i > 0)
            {
                socket.Dispose();
                socket = new ClientWebSocket();
            }
            // Options only apply before ConnectAsync, so they are set per instance.
            foreach (var (name, value) in UpgradeHeaders(peerSecret, accessToken))
                socket.Options.SetRequestHeader(name, value);
            var uri = candidates[i];
            IsEncrypted = uri.Scheme == "wss";
            var fallbackLeft = i < candidates.Count - 1;
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                if (fallbackLeft) attempt.CancelAfter(FallbackAfter);
                await socket.ConnectAsync(uri, WebSocketHttp, attempt.Token).ConfigureAwait(false);
                DiagnosticLog.Info($"[RelayClient] Connected to {uri}.");
                if (i > 0)
                {
                    FellBackToUnencrypted = true;
                    DiagnosticLog.Warn($"[RelayClient] {candidates[0]} wasn't reachable -- fell back to {uri}, unencrypted.");
                }
                connected = uri;
                break;
            }
            catch (Exception e)
            {
                // Only the fallback cap cancels while this client is still alive.
                lastFailure = e is OperationCanceledException && !cts.IsCancellationRequested
                    ? new TimeoutException($"No answer within {FallbackAfter.TotalSeconds:0.#}s.")
                    : e;
                DiagnosticLog.Info($"[RelayClient] {uri} failed: {lastFailure.Message}"
                    + (fallbackLeft ? " -- trying the next candidate." : ""));
            }
        }
        // A relay that answered but refused or never greeted isn't retried on the next
        // candidate: that would only resend the same credentials over a weaker connection.
        if (connected != null)
        {
            try
            {
                var assignedCode = await ReadGreetingAsync(path == HostPath).ConfigureAwait(false);
                _ = Task.Run(ReceiveLoopAsync);
                _ = Task.Run(SendLoopAsync);
                return assignedCode;
            }
            catch (Exception e)
            {
                lastFailure = e;
                DiagnosticLog.Warn($"[RelayClient] {connected} didn't complete the greeting: {e.Message}");
                try { socket.Abort(); } catch { /* best-effort */ }
            }
        }
        // Callers are fire-and-forget; without Disconnected a failed handshake would only be
        // an unobserved Task exception.
        DiagnosticLog.Warn($"[RelayClient] Connect failed: {lastFailure?.Message}");
        Disconnected?.Invoke(lastFailure);
        return null;
    }

    private sealed record RelayGreeting(int RelayVersion, string[]? Capabilities, string? SessionCode, Guid PeerId);

    internal static readonly TimeSpan DefaultGreetingTimeout = TimeSpan.FromSeconds(5);
    internal TimeSpan GreetingTimeout { get; init; } = DefaultGreetingTimeout;

    // The greeting is not an MpMessage and is consumed once before ReceiveLoopAsync starts. It
    // names the identity the relay derived from our secret, so a relay that can't authenticate
    // senders fails here rather than mid-session. A slow greeting is an ordinary network failure
    // (the reconnect loop retries); only an explicit refusal is RelaySessionRejectedException.
    private async Task<string?> ReadGreetingAsync(bool hosting)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        timeout.CancelAfter(GreetingTimeout);
        var (sessionCode, capabilities) = await ReadGreetingAsync(socket, RelayWire.PeerId(peerSecret), hosting, timeout.Token).ConfigureAwait(false);
        RelayCapabilities = capabilities;
        return sessionCode;
    }

    internal static async Task<(string? SessionCode, IReadOnlySet<string> Capabilities)> ReadGreetingAsync(WebSocket socket, Guid expectedPeerId,
        bool hosting, CancellationToken token)
    {
        using var message = new MemoryStream();
        var buffer = new byte[1024];
        WebSocketReceiveResult result;
        var fragments = 0;
        do
        {
            result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
            // The relay accepts the WS upgrade before validating the session code, so a rejected
            // /session/<code> or /host arrives here as a close frame. Surfaced distinctly so the
            // reconnect loop can tell "give up" from "keep retrying".
            if (result.MessageType == WebSocketMessageType.Close)
            {
                var reason = socket.CloseStatusDescription ?? "the relay closed the connection";
                throw RelayWire.MovedTo(reason) is { } movedTo
                    ? new RelaySessionRejectedException("the session is on another server", movedTo)
                    : new RelaySessionRejectedException(reason);
            }
            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > 4096 || ++fragments > 16)
                throw new RelaySessionRejectedException("The relay sent an invalid greeting.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return ParseGreeting(message.ToArray(), expectedPeerId, hosting);
    }

    internal static (string? SessionCode, IReadOnlySet<string> Capabilities) ParseGreeting(byte[] json, Guid expectedPeerId, bool hosting)
    {
        RelayGreeting? greeting;
        try { greeting = JsonSerializer.Deserialize<RelayGreeting>(json, JsonOptions); }
        catch (JsonException) { throw new RelaySessionRejectedException("The relay sent an invalid greeting."); }
        var capabilities = greeting?.Capabilities is { } caps ? new HashSet<string>(caps) : new HashSet<string>();
        DiagnosticLog.Info($"[RelayClient] Relay version {greeting?.RelayVersion}, capabilities: [{string.Join(", ", capabilities)}].");
        if (greeting is null || greeting.RelayVersion != RelayWire.Version || !capabilities.Contains("authenticatedIdentity")
            || !capabilities.Contains("roomModeration") || greeting.PeerId != expectedPeerId
            || (hosting && string.IsNullOrEmpty(greeting.SessionCode)))
            throw new RelaySessionRejectedException("This relay doesn't match your AnoMech version -- update the relay.");
        return (greeting.SessionCode, capabilities);
    }

    public async Task SendAsync(MpMessage message)
    {
        if (!IsConnected) return;
        var (bytes, type) = Encode(message, SupportsCompression);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!sendQueue.Writer.TryWrite((bytes, type, completion))) return;
        await completion.Task.ConfigureAwait(false);
    }

    internal static (byte[] Bytes, WebSocketMessageType Type) Encode(MpMessage message, bool compress)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        return compress && jsonBytes.Length >= CompressionThresholdBytes
            ? (Compress(jsonBytes), WebSocketMessageType.Binary)
            : (jsonBytes, WebSocketMessageType.Text);
    }

    // A forwarded body as this client takes it: within its sender's budget, checked, then typed.
    internal static MpMessage? Read(byte[] body, bool compressed, bool isFromHost, Guid sender, TrafficBudget budget)
    {
        var raw = RelayWire.Decode(body, compressed, budget, isFromHost ? RelayWire.MaxMessageBytes : RelayWire.MaxPeerMessageBytes);
        RelayWire.Validate(raw, isFromHost, sender);
        return JsonSerializer.Deserialize<MpMessage>(raw, JsonOptions);
    }

    // A host's kick, ban or unban, carried out by the relay itself. Always small and
    // uncompressed: it's the one body the relay reads (RelayWire.IsControl).
    public async Task ModerateAsync(string operation, Guid peerId)
    {
        if (!IsConnected) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!sendQueue.Writer.TryWrite((ControlBody(operation, peerId), WebSocketMessageType.Text, completion))) return;
        await completion.Task.ConfigureAwait(false);
    }

    internal static byte[] ControlBody(string operation, Guid peerId)
        => JsonSerializer.SerializeToUtf8Bytes(new { t = RelayWire.ControlType, Operation = operation, PeerId = peerId });

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest))
            brotli.Write(data, 0, data.Length);
        return output.ToArray();
    }

    // ClientWebSocket allows one outstanding send with no timeout; aborting also faults the
    // receive loop's pending read, which triggers Disconnected/reconnect.
    private const int SendTimeoutMs = 10_000;

    private async Task SendLoopAsync()
    {
        try
        {
            while (await sendQueue.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                while (sendQueue.Reader.TryRead(out var entry))
                    await SendOneAsync(entry.Bytes, entry.Type, entry.Completion).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // cancelled by Dispose()
        }
    }

    private async Task SendOneAsync(byte[] bytes, WebSocketMessageType type, TaskCompletionSource completion)
    {
        try
        {
            var sendTask = socket.SendAsync(bytes, type, true, cts.Token);
            if (await Task.WhenAny(sendTask, Task.Delay(SendTimeoutMs, cts.Token)).ConfigureAwait(false) != sendTask)
            {
                DiagnosticLog.Warn($"[RelayClient] Send stuck for over {SendTimeoutMs / 1000}s -- treating the connection as dead.");
                try { socket.Abort(); } catch { /* best-effort */ }
                completion.SetException(new TimeoutException($"Send timed out after {SendTimeoutMs}ms."));
                return;
            }
            await sendTask.ConfigureAwait(false);
            RelayStats.RecordSent(bytes.Length);
            completion.SetResult();
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[RelayClient] Send failed: {e.Message}");
            completion.SetException(e);
        }
    }

    private sealed record RelayNotice(string? T, Guid[]? Removed);

    private async Task ReceiveLoopAsync()
    {
        Exception? failure = null;
        try
        {
            failure = await ReceiveAsync(socket, cts.Token,
                (message, isFromHost, senderConnectionId, sender) => MessageReceived?.Invoke(message, isFromHost, senderConnectionId, sender),
                removed => PeersRemoved?.Invoke(removed),
                (sender, isFromHost, reason) => MessageRejected?.Invoke(sender, isFromHost, reason)).ConfigureAwait(false);
        }
        finally
        {
            DiagnosticLog.Debug($"[RelayClient] Receive loop exiting -- final socket state {socket.State}.");
            Disconnected?.Invoke(failure);
        }
    }

    // Everything the relay sends on a connection after its greeting, until it ends: the failure
    // that ended it, or null for a close or a cancel.
    internal static async Task<Exception?> ReceiveAsync(WebSocket socket, CancellationToken token, Action<MpMessage, bool, uint, Guid> received,
        Action<IReadOnlyList<Guid>> removed, Action<Guid, bool, string> rejected, TimeProvider? time = null)
    {
        var buffer = new byte[64 * 1024];
        // Per sender: a host hears from every peer at once, and one shared budget would let a few
        // peers exhaust it together.
        var budgets = new Dictionary<Guid, TrafficBudget>();
        long overBudget = 0, dropped = 0;
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                var fragments = 0;
                do
                {
                    result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        DiagnosticLog.Info($"[RelayClient] Received close frame: status={result.CloseStatus}, description=\"{result.CloseStatusDescription}\".");
                        return null;
                    }
                    ms.Write(buffer, 0, result.Count);
                    if (ms.Length > MaxIncomingMessageBytes || ++fragments > MaxIncomingFragments)
                    {
                        var failure = new InvalidDataException(
                            $"Relay sent a message over {MaxIncomingMessageBytes / (1024 * 1024)} MB or {MaxIncomingFragments} fragments -- dropping the connection.");
                        DiagnosticLog.Warn($"[RelayClient] {failure.Message}");
                        try { socket.Abort(); } catch { /* best-effort */ }
                        return failure;
                    }
                } while (!result.EndOfMessage);
                RelayStats.RecordReceived(ms.Length);

                // Written by the relay, never by the sender (RelayWire.Envelope).
                var frame = ms.ToArray();
                if (result.MessageType != WebSocketMessageType.Binary || frame.Length < RelayWire.PrefixBytes || frame[0] > 1 || frame[21] > 1)
                {
                    DiagnosticLog.Warn("[RelayClient] Dropped a frame without a relay envelope.");
                    continue;
                }
                var isFromHost = frame[0] == 1;
                var senderConnectionId = BitConverter.ToUInt32(frame, 1);
                var sender = new Guid(frame.AsSpan(5, 16));
                if (senderConnectionId == 0 && sender == Guid.Empty)
                {
                    if (ReadNotice(frame) is { } others) removed(others);
                    continue;
                }
                if (!budgets.TryGetValue(sender, out var budget))
                {
                    if (budgets.Count >= 64) budgets.Clear();
                    // The host already runs the whole simulation, so only peers are held to one.
                    budget = isFromHost ? new TrafficBudget(time: time) : new TrafficBudget(RelayWire.PeerMessagesPerSecond, RelayWire.PeerBytesPerSecond, time);
                    budgets[sender] = budget;
                }

                MpMessage? message;
                try
                {
                    message = Read(frame[RelayWire.PrefixBytes..], frame[21] == 1, isFromHost, sender, budget);
                }
                catch (TrafficLimitException)
                {
                    if (overBudget++ % 1000 == 0)
                        DiagnosticLog.Warn($"[RelayClient] Dropping traffic over the per-sender budget from {sender} ({overBudget} so far).");
                    continue;
                }
                // One bad message shouldn't take the connection down.
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    if (dropped++ % 100 == 0)
                        DiagnosticLog.Warn($"[RelayClient] Malformed message from {sender} dropped ({dropped} so far): {e.Message}");
                    rejected(sender, isFromHost, e.Message);
                    continue;
                }
                if (message != null) received(message, isFromHost, senderConnectionId, sender);
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            var code = e is WebSocketException wse ? $" (WebSocketErrorCode={wse.WebSocketErrorCode})" : "";
            DiagnosticLog.Warn($"[RelayClient] Receive loop faulted: {e}{code}");
            return e;
        }
    }

    private static IReadOnlyList<Guid>? ReadNotice(byte[] frame)
    {
        try
        {
            var notice = JsonSerializer.Deserialize<RelayNotice>(frame.AsSpan(RelayWire.PrefixBytes), JsonOptions);
            return notice?.T == RelayWire.NoticeType && notice.Removed is { Length: > 0 and <= 64 } removed ? removed : null;
        }
        catch (JsonException e)
        {
            DiagnosticLog.Warn($"[RelayClient] Ignored an unreadable relay notice: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DiagnosticLog.Debug($"[RelayClient] Dispose() -- socket state was {socket.State}.");
        // Complete before Cancel, so an enqueued SendAsync caller sees a failed send instead
        // of hanging.
        sendQueue.Writer.TryComplete();
        while (sendQueue.Reader.TryRead(out var pending))
            pending.Completion.TrySetException(new ObjectDisposedException(nameof(RelayClient)));
        cts.Cancel();
        try { socket.Abort(); } catch { /* best-effort */ }
        socket.Dispose();
        cts.Dispose();
    }
}
