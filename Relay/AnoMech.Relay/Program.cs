using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace AnoMech.Relay;

// Command-line host for RelayServer. See RelayServer for what the relay itself does.
internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Contains("--admin"))
        {
            await AdminConsole.RunAsync(args);
            return;
        }

        if (args.Contains("--session-log"))
        {
            var code = GetArg(args, "--session-log");
            var dir = GetArg(args, "--log-dir") ?? Path.Combine(AppContext.BaseDirectory, "logs");
            if (string.IsNullOrEmpty(code))
            {
                Console.Error.WriteLine("--session-log requires a session code, e.g. --session-log ABCD23");
                return;
            }
            RelayLog.PrintSessionLog(dir, code);
            return;
        }

        var options = new RelayOptions();
        if (int.TryParse(GetArg(args, "--port", "-p"), out var port)) options.Port = port;
        var bind = GetArg(args, "--bind") ?? "*";
        if (!TryParseBind(bind, out var bindAddress))
        {
            Console.Error.WriteLine($"--bind: '{bind}' isn't an IP address, '*' or 'localhost'.");
            return;
        }
        options.BindAddress = bindAddress;
        // Env var fallback, CLI flag wins if both are set -- a CLI arg is visible to any other
        // local user via a process listing (ps/tasklist) and often ends up in shell history;
        // an env var isn't immune to a sufficiently privileged local reader either, but doesn't
        // leak through either of those two common paths.
        options.AccessToken = NullIfEmpty(GetArg(args, "--token")) ?? NullIfEmpty(Environment.GetEnvironmentVariable("ANOMECH_RELAY_TOKEN"));
        options.AdminToken = NullIfEmpty(GetArg(args, "--admin-token")) ?? NullIfEmpty(Environment.GetEnvironmentVariable("ANOMECH_RELAY_ADMIN_TOKEN"));
        if (int.TryParse(GetArg(args, "--max-sessions"), out var mts)) options.MaxTotalSessions = mts;
        if (int.TryParse(GetArg(args, "--max-connections-per-ip"), out var mcpi)) options.MaxConnectionsPerIp = mcpi;
        if (long.TryParse(GetArg(args, "--max-message-bytes"), out var mmb)) options.MaxMessageBytes = mmb;
        if (int.TryParse(GetArg(args, "--max-failed-joins"), out var mfj)) options.MaxFailedJoinsPerWindow = mfj;
        if (int.TryParse(GetArg(args, "--max-peers-per-session"), out var mpps)) options.MaxPeersPerSession = mpps;
        if (int.TryParse(GetArg(args, "--max-messages-per-second"), out var mmps)) options.MaxMessagesPerSecond = mmps;
        if (long.TryParse(GetArg(args, "--max-bytes-per-second"), out var mbps)) options.MaxBytesPerSecond = mbps;
        if (int.TryParse(GetArg(args, "--max-fragments-per-message"), out var mfpm)) options.MaxFragmentsPerMessage = mfpm;
        if (double.TryParse(GetArg(args, "--usage-warn-fraction"), out var uwf) && uwf is > 0 and <= 1) options.UsageWarnFraction = uwf;
        if (GetArg(args, "--client-ip-header") is { Length: > 0 } cih) options.ClientIpHeader = cih;
        options.RequireTls = args.Contains("--require-tls");
        foreach (var cidr in GetArgs(args, "--trusted-proxy"))
        {
            if (RelayOptions.TryParseNetwork(cidr, out var network)) options.TrustedProxies.Add(network);
            else { Console.Error.WriteLine($"--trusted-proxy: '{cidr}' isn't a valid CIDR (e.g. 127.0.0.1/32 or ::1/128)."); return; }
        }

        if (options.Validate() is { } invalid)
        {
            Console.Error.WriteLine(invalid);
            return;
        }

        // Configured before the listener even tries to bind, so a bind failure still gets a
        // file record -- useful under systemd/journald where the console output of a crashed
        // service is easy to lose. Defaults to a directory next to the executable (not the
        // working directory, which varies by how the process was launched).
        if (!args.Contains("--no-file-log"))
        {
            var logDir = GetArg(args, "--log-dir") ?? Path.Combine(AppContext.BaseDirectory, "logs");
            var maxLogBytes = long.TryParse(GetArg(args, "--log-max-bytes"), out var mlb) ? mlb : 5L * 1024 * 1024 * 1024;
            RelayLog.Configure(logDir, maxLogBytes);
            // Log writes are buffered and flushed roughly once a second (see RelayLog) -- catch
            // a graceful shutdown (systemd stop, Ctrl+C) so the last stretch isn't silently lost.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RelayLog.FlushOnShutdown();
            RelayLog.Info($"[AnoMech.Relay] Logging to {logDir} (compressed, capped at {maxLogBytes / (1024.0 * 1024 * 1024):F1} GB). " +
                           "Use --session-log <code> to read back one session's lines.");
        }

        await using var server = new RelayServer(options, new ConsoleRelayLog());
        try
        {
            server.Start();
        }
        catch (SocketException e)
        {
            RelayLog.Warn($"Failed to bind {bind}:{options.Port}: {e.Message}");
            RelayLog.Warn("Another process may already own the port -- check `netstat -ano | findstr " + options.Port + "` " +
                          "(Windows) or `ss -ltnp` (Linux).");
            return;
        }

        var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void RequestStop(PosixSignalContext context)
        {
            context.Cancel = true;
            stopRequested.TrySetResult();
        }
        using var onInterrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestStop);
        using var onTerminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestStop);
        await stopRequested.Task;
    }

    private static bool TryParseBind(string value, out IPAddress address)
    {
        switch (value)
        {
            case "*" or "+":
                address = IPAddress.IPv6Any;
                return true;
            case "localhost":
                address = IPAddress.Loopback;
                return true;
            default:
                return IPAddress.TryParse(value, out address!);
        }
    }

    private static string? GetArg(string[] args, params string[] names)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (names.Contains(args[i]))
                return args[i + 1];
        return null;
    }

    private static IEnumerable<string> GetArgs(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                yield return args[i + 1];
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

internal sealed class ConsoleRelayLog : IRelayLog
{
    public void Info(string message) => RelayLog.Info(message);
    public void Warn(string message) => RelayLog.Warn(message);
    public void Detail(string message) => RelayLog.Detail(message);
}

// Polls a running relay's admin endpoints and renders a live text dashboard with actions.
// No TUI dependency -- periodic clear + rewrite plus single-key commands is enough here.
internal static class AdminConsole
{
    private static readonly JsonSerializerOptions Json = RelayAdmin.Json;
    private static HttpClient http = null!;
    private static string host = "";
    private static string? lastMessage;
    // Redirected output (a pipe, systemd) has no console buffer, so Clear/KeyAvailable throw.
    // The dashboard still works as a plain append-only feed there.
    private static bool interactive = true;

    public static async Task RunAsync(string[] args)
    {
        var port = 7890;
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] is "--port" or "-p" && int.TryParse(args[i + 1], out var parsed)) port = parsed;
        host = (ValueOf(args, "--host") ?? $"http://localhost:{port}").TrimEnd('/');
        var adminToken = ValueOf(args, "--admin-token") ?? Environment.GetEnvironmentVariable("ANOMECH_RELAY_ADMIN_TOKEN");
        if (string.IsNullOrEmpty(adminToken))
        {
            Console.Error.WriteLine("--admin-token (or ANOMECH_RELAY_ADMIN_TOKEN) is required in --admin mode.");
            return;
        }

        // The admin token goes out on every request, so it must not leave in the clear or be
        // redirected to another server.
        if (!IsSafeAdminUri(host))
        {
            Console.Error.WriteLine("Admin connections require HTTPS, except HTTP on loopback.");
            return;
        }
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("X-AnoMech-Admin-Token", adminToken);

        var showSessions = false;
        while (true)
        {
            try
            {
                var stats = JsonSerializer.Deserialize<AdminStats>(await http.GetStringAsync($"{host}/admin/stats"), Json);
                var sessions = showSessions
                    ? JsonSerializer.Deserialize<List<SessionInfo>>(await http.GetStringAsync($"{host}/admin/sessions"), Json)
                    : null;
                Render(stats, sessions, null);
            }
            catch (Exception e)
            {
                Render(null, null, e.Message);
            }

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (!interactive) { await Task.Delay(200); continue; }
                bool pressed;
                try { pressed = Console.KeyAvailable; }
                catch (Exception) { interactive = false; continue; }
                if (!pressed) { await Task.Delay(50); continue; }
                var key = Console.ReadKey(intercept: true).KeyChar;
                if (key == 'q') return;
                if (key == 's') { showSessions = !showSessions; break; }
                await HandleCommandAsync(key);
                break;
            }
        }
    }

    internal static bool IsSafeAdminUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        return uri.Scheme == "https" || (uri.Scheme == "http" &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
             || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip)
                 && IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))));
    }

    private static string? ValueOf(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    private static async Task HandleCommandAsync(char key)
    {
        switch (key)
        {
            case 'k': await PostAsync(new AdminActionRequest("kick-peer", null, AskUint("Connection id"), null, null, null)); break;
            case 'd': await PostAsync(new AdminActionRequest("disband-session", Ask("Session code")?.ToUpperInvariant(), null, null, null, null)); break;
            case 'b': await PostAsync(new AdminActionRequest("ban-ip", null, null, Ask("IP to ban"), null, null)); break;
            case 'u': await PostAsync(new AdminActionRequest("unban-ip", null, null, Ask("IP to unban"), null, null)); break;
            case 'c': await PostAsync(new AdminActionRequest("clear-lockouts", null, null, null, null, null)); break;
            case 'p': await PostAsync(new AdminActionRequest("pause", null, null, null, null, null)); break;
            case 'r': await PostAsync(new AdminActionRequest("resume", null, null, null, null, null)); break;
            case 'l':
            {
                var name = Ask("Limit name (max-messages-per-second, max-bytes-per-second, max-message-bytes, " +
                               "max-connections-per-ip, max-peers-per-session, max-sessions, max-failed-joins, " +
                               "max-fragments-per-message, usage-warn-fraction)");
                var value = AskDouble("New value");
                await PostAsync(new AdminActionRequest("set-limit", null, null, null, name, value));
                break;
            }
        }
    }

    private static string? Ask(string prompt)
    {
        Console.Write($"{prompt}: ");
        var line = Console.ReadLine();
        return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
    }

    private static uint? AskUint(string prompt) => uint.TryParse(Ask(prompt), out var value) ? value : null;
    private static double? AskDouble(string prompt) => double.TryParse(Ask(prompt), out var value) ? value : null;

    private static async Task PostAsync(AdminActionRequest request)
    {
        try
        {
            var response = await http.PostAsync($"{host}/admin/action",
                new StringContent(JsonSerializer.Serialize(request, Json), Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AdminActionResult>(body, Json);
            lastMessage = result is null ? $"HTTP {(int)response.StatusCode}" : $"{(result.Ok ? "OK" : "FAILED")}: {result.Message}";
        }
        catch (Exception e)
        {
            lastMessage = $"FAILED: {e.Message}";
        }
    }

    private static void Render(AdminStats? s, List<SessionInfo>? sessions, string? error)
    {
        if (interactive)
        {
            try { Console.Clear(); }
            catch (Exception) { interactive = false; }
        }
        Console.WriteLine();
        Console.WriteLine($"AnoMech.Relay admin -- {host}   (refreshes every 2s)");
        if (interactive)
            Console.WriteLine("[s] sessions  [k] kick peer  [d] disband  [b] ban ip  [u] unban  [c] clear lockouts  [p] pause  [r] resume  [l] set limit  [q] quit");
        Console.WriteLine(new string('-', 110));
        if (error != null) { Console.WriteLine($"Failed to fetch stats: {error}"); return; }
        if (s == null) { Console.WriteLine("No data."); return; }

        var uptime = TimeSpan.FromSeconds(s.UptimeSeconds);
        Console.WriteLine($"Uptime {(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s   relay v{s.RelayVersion}   " +
                          $"{(s.AcceptingConnections ? "ACCEPTING" : "PAUSED")}");
        Console.WriteLine($"Sessions {s.Sessions}   peers {s.TotalPeers}   distinct IPs {s.ConnectionsByIpCount}   " +
                          $"lockouts {s.ActiveJoinLockouts}   banned {s.BannedIpCount}");
        Console.WriteLine($"Lifetime: {s.TotalConnectionsAccepted} connections, {s.TotalMessagesBroadcast} messages, {s.TotalBytesBroadcast:N0} bytes");
        Console.WriteLine();
        Console.WriteLine("Load vs caps:");
        Console.WriteLine($"  Messages/sec peak:  {s.PeakMessagesPerSecond,10:N0} / {s.Limits.MaxMessagesPerSecond,-10:N0} {Bar(s.PeakMessagesPerSecond, s.Limits.MaxMessagesPerSecond)}");
        Console.WriteLine($"  Bytes/sec peak:     {s.PeakBytesPerSecond,10:N0} / {s.Limits.MaxBytesPerSecond,-10:N0} {Bar(s.PeakBytesPerSecond, s.Limits.MaxBytesPerSecond)}");
        Console.WriteLine($"  Near-limit warnings:{s.NearLimitWarnings,10:N0}  (logged past {s.Limits.UsageWarnFraction:P0} of a cap)");
        Console.WriteLine();
        Console.WriteLine("Rejections (lifetime):");
        Console.WriteLine($"  origin {s.Rejections.Origin}   ipCap {s.Rejections.IpCap}   lockout {s.Rejections.JoinLockout}   " +
                          $"relayFull {s.Rejections.RelayFull}   sessionFull {s.Rejections.SessionFull}   notFound {s.Rejections.SessionNotFound}");
        Console.WriteLine($"  badToken {s.Rejections.BadToken}   handshake {s.Rejections.HandshakeTimeout}   tooLarge {s.Rejections.MessageTooLarge}   " +
                          $"msgTimeout {s.Rejections.MessageTimeout}   msgRate {s.Rejections.MessageRate}   byteRate {s.Rejections.ByteRate}");
        Console.WriteLine($"  unencrypted {s.Rejections.Unencrypted}   fragments {s.Rejections.TooManyFragments}   banned {s.Rejections.Banned}   paused {s.Rejections.Paused}   protocol {s.Rejections.BadProtocol}");
        Console.WriteLine($"  last ~2s: {s.RecentRejections}{(s.RecentRejections >= 50 ? "   [ELEVATED -- possible abuse]" : "")}");
        Console.WriteLine();
        Console.WriteLine($"Memory {s.MemoryBytes / 1024 / 1024:N0} MB   GC {s.Gen0Collections}/{s.Gen1Collections}/{s.Gen2Collections}");

        if (sessions != null)
        {
            Console.WriteLine();
            Console.WriteLine("Sessions:");
            if (sessions.Count == 0) Console.WriteLine("  (none)");
            foreach (var session in sessions)
            {
                Console.WriteLine($"  {session.Code}  age {session.AgeSeconds:F0}s  idle {session.IdleSeconds:F1}s  {session.Peers.Count} peer(s)");
                foreach (var peer in session.Peers)
                    Console.WriteLine($"    #{peer.Id,-6} {peer.Ip,-40} {(peer.IsHost ? "HOST" : "peer")}  " +
                                      $"{peer.MessagesIn,8:N0} msg  {peer.BytesIn / 1024,8:N0} KB  " +
                                      $"peak {peer.PeakMessagesPerSecond}/s {peer.PeakBytesPerSecond / 1024}KB/s");
            }
        }

        if (lastMessage != null)
        {
            Console.WriteLine();
            Console.WriteLine($"> {lastMessage}");
        }
    }

    private static string Bar(long value, double limit)
    {
        var fraction = limit <= 0 ? 0 : Math.Clamp(value / limit, 0, 1);
        var filled = (int)Math.Round(fraction * 30);
        return $"[{new string('#', filled)}{new string('.', 30 - filled)}] {fraction:P0}";
    }
}

// File logging: mirrors console-worthy events to disk plus much higher-volume per-message
// detail, rotated and gzip-compressed, held under a total size cap. Never logs message
// CONTENTS -- only metadata (who, when, which session, how big, how many peers it reached),
// same stance as the README's existing "the relay doesn't log message contents" note.
//
// A plain rotating/compressed log directory rather than a database: this is meant to run as a
// single, dependency-free binary an operator can drop on a box (see Program's own header
// comment on that goal) -- a database would mean standing up and separately securing another
// service just to hold logs, for a data volume this design already keeps well within one
// process's own housekeeping.
internal static class RelayLog
{
    private static string? logDir;
    private static long maxTotalBytes;
    private static StreamWriter? activeWriter;
    private static string? activeFilePath;
    private static long activeBytesWritten;

    // Segment size before a file is compressed and a new one started. Well under the total
    // cap -- keeps the always-uncompressed "live" segment a small, bounded slice of the
    // budget, and keeps each rotation's compression work modest.
    private const long RotateThresholdBytes = 64 * 1024 * 1024;

    // Producers (Info/Warn/Detail, called from every connection's own async flow) only ever
    // enqueue a string -- no lock, no disk I/O, on that path. A single background task is the
    // only thing that ever touches the file, so it needs no locking either. At hundreds of
    // sessions all broadcasting, this is what keeps logging from becoming the actual bottleneck
    // (it used to be a synchronous, AutoFlush=true write under one relay-wide lock, shared by
    // literally every connection).
    // Bounded, not unbounded: a stuck/full disk should drop log lines rather than let the queue
    // grow without limit and eventually pressure the process's own memory. Capacity is generous
    // relative to realistic burst rates -- dropping is the rare, "something's already wrong" case.
    private static readonly Channel<string> Queue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(20_000) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropWrite });
    private static long droppedLines;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private static DateTime lastFlushUtc = DateTime.UtcNow;

    public static void Configure(string directory, long maxBytes)
    {
        logDir = directory;
        maxTotalBytes = maxBytes;
        Directory.CreateDirectory(logDir);
        OpenNewActiveFile();
        _ = SuperviseWriterAsync();
    }

    // Logging is an abuse-forensics control, so a transient disk failure must not silently
    // retire it for the rest of the process's life -- the writer is restarted instead, and the
    // failure is reported through the console, which doesn't depend on the file being writable.
    private static async Task SuperviseWriterAsync()
    {
        while (true)
        {
            try
            {
                await RunWriterLoopAsync();
                return; // channel completed
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[AnoMech.Relay] Log writer failed: {e.Message} -- restarting in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5));
                try { OpenNewActiveFile(); }
                catch (Exception reopen) { Console.Error.WriteLine($"[AnoMech.Relay] Could not reopen the log: {reopen.Message}"); }
            }
        }
    }

    // The only place that touches activeWriter/activeBytesWritten/rotation/compression --
    // single-reader by construction (see Queue above), so none of that needs its own lock.
    private static async Task RunWriterLoopAsync()
    {
        await foreach (var line in Queue.Reader.ReadAllAsync())
        {
            activeWriter!.WriteLine(line);
            activeBytesWritten += line.Length + 2;

            // Batches flushes instead of one disk write per line (what AutoFlush did) --
            // still bounds how stale the file can be to ~1s, without paying a syscall per
            // message broadcast under real load.
            if (DateTime.UtcNow - lastFlushUtc >= FlushInterval)
            {
                activeWriter.Flush();
                lastFlushUtc = DateTime.UtcNow;
            }

            if (activeBytesWritten >= RotateThresholdBytes)
                Rotate();
        }
    }

    private static void OpenNewActiveFile()
    {
        activeFilePath = Path.Combine(logDir!, $"relay-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log");
        // FileShare.ReadWrite (not StreamWriter's own default of Read-only sharing) so
        // --session-log can open and read the still-active segment while the relay keeps
        // writing to it, instead of hitting a sharing-violation IOException.
        var stream = new FileStream(activeFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        activeWriter = new StreamWriter(stream, Encoding.UTF8);
        activeBytesWritten = 0;
    }

    // Console + file. Events an operator should see live: startup, session lifecycle,
    // rejections severe enough to already carry their own explicit message, alerts, summaries.
    public static void Info(string message)
    {
        Console.WriteLine(message);
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Console.Error.WriteLine(message);
        Write("WARN", message);
    }

    // File only. For anything high-volume enough that echoing it to the console would drown
    // out the events actually worth watching live: individual rejection reasons, every broadcast.
    public static void Detail(string message) => Write("DETAIL", message);

    private static void Write(string level, string message)
    {
        if (logDir == null) return; // file logging disabled (--no-file-log) -- console-only.
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        if (!Queue.Writer.TryWrite(line))
            Interlocked.Increment(ref droppedLines);
    }

    // Called from the writer loop only.
    private static void Rotate()
    {
        activeWriter!.Dispose();
        var finished = activeFilePath!;
        OpenNewActiveFile();
        var dropped = Interlocked.Exchange(ref droppedLines, 0);
        if (dropped > 0) activeWriter!.WriteLine($"{DateTime.UtcNow:O} [WARN] {dropped} log line(s) dropped -- write queue was full.");
        CompressAndDelete(finished);
        EnforceCap();
    }

    // Best-effort flush for a graceful shutdown (see Main's ProcessExit hook) -- anything still
    // sitting in the queue at the instant of a hard kill is lost either way, same tradeoff any
    // buffered logger makes.
    public static void FlushOnShutdown()
    {
        try { activeWriter?.Flush(); } catch { /* best effort */ }
    }

    private static void CompressAndDelete(string path)
    {
        try
        {
            using (var input = File.OpenRead(path))
            using (var output = File.Create(path + ".gz"))
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                input.CopyTo(gzip);
            File.Delete(path);
        }
        catch (Exception)
        {
            // Best effort -- an uncompressed leftover segment still counts toward EnforceCap's
            // total below, so it still ages out via oldest-first deletion even if compression
            // itself failed for some reason (e.g. disk full).
        }
    }

    // Deletes the oldest completed (.gz) segments until the directory is back under the cap.
    // Never touches the currently-active segment.
    private static void EnforceCap()
    {
        try
        {
            var completed = new DirectoryInfo(logDir!).GetFiles("relay-*.log.gz").OrderBy(f => f.Name).ToList();
            var activeSize = File.Exists(activeFilePath) ? new FileInfo(activeFilePath!).Length : 0;
            var total = activeSize + completed.Sum(f => f.Length);
            foreach (var file in completed)
            {
                if (total <= maxTotalBytes) break;
                total -= file.Length;
                try { file.Delete(); } catch (IOException) { /* best effort */ }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[AnoMech.Relay] Log cap enforcement failed: {e.Message}");
        }
    }

    // `AnoMech.Relay --session-log <CODE> --log-dir <dir>` -- scans every segment (live and
    // compressed), oldest first, for lines tagged with that session code. The tag format
    // ("[CODE] ...") is the same one every session-scoped log line already uses, so this needs
    // no separate structured format to stay useful.
    public static void PrintSessionLog(string directory, string sessionCode)
    {
        var tag = $"[{sessionCode}]";
        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"No log directory at {directory}.");
            return;
        }
        var files = new DirectoryInfo(directory).GetFiles("relay-*.log*").OrderBy(f => f.Name).ToList();
        var found = 0;
        foreach (var file in files)
        {
            // ReadWrite sharing -- the currently-active segment is still open for writing by
            // a live relay process (see OpenNewActiveFile) when this runs alongside it.
            var raw = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = file.Extension == ".gz"
                ? new StreamReader(new GZipStream(raw, CompressionMode.Decompress))
                : new StreamReader(raw);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.Contains(tag)) continue;
                Console.WriteLine(line);
                found++;
            }
        }
        if (found == 0) Console.WriteLine($"No log lines found for session {sessionCode} under {directory}.");
    }
}
