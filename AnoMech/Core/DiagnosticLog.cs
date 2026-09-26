using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using AnoMech.Multiplayer;

namespace AnoMech.Core;

// Mirrors Plugin.Log's three levels -- swap Plugin.Log.X for DiagnosticLog.X and it still
// reaches the normal Dalamud log, just also captured here.
//
// Two independent halves:
//  - An in-memory "this run" buffer (currentRunLines), capped by byte size, read synchronously
//    by DamageDebugWindow's dump so a periodic mid-run dump never hitches reading a big file.
//  - A disk-backed, async rotating log: a bounded channel feeds one background writer task, so
//    Info/Warn/Debug never block on I/O. The active segment gzips/rotates at SegmentMaxBytes or
//    on RotateNow() (Game.Leave forces a fresh segment on leave). Archives are pruned
//    oldest-first past TotalArchiveCapBytes. Leftover unrotated active files from previous
//    sessions (crash, plugin reload, a load that threw before Plugin.Dispose) are rotated at
//    startup instead of being overwritten; one still held open by a dead load is left alone and
//    this session writes to a stamped sibling instead. Offline mode's sessions are kept whatever
//    windows are open, in segments of their own archived and capped alike as AnoMech-Offline-*.
internal static class DiagnosticLog
{
    // ---- In-memory "this run" view (DamageDebugWindow's Snapshot) -----------------------

    private const long RunBufferCapBytes = 10 * 1024 * 1024;
    private static readonly Queue<string> currentRunLines = new();
    private static long currentRunBytes;
    private static readonly object gate = new();

    public static void Info(string message)
    {
        Plugin.Log.Information(message);
        Add(message);
    }

    public static void Warn(string message)
    {
        Plugin.Log.Warning(message);
        Add(message);
    }

    public static void Debug(string message)
    {
        Plugin.Log.Debug(message);
        Add(message);
    }

    // A delimited multi-line block (DamageDebugWindow's state dump). Skips Plugin.Log: it can
    // fire every few seconds mid-run.
    public static void LogSnapshot(string label, string content)
        => Add($"=== {label} ==={Environment.NewLine}{content}{Environment.NewLine}=== end {label} ===");

    // For a deliberate process kill (ZoneSession's guard): the message and the run's last lines
    // are written synchronously to their own file, and the async writer is drained, since
    // Environment.FailFast runs no finalizers. Returns that file's path, or null.
    public static string? Fatal(string message)
    {
        Plugin.Log.Fatal(message);
        ForcePersist = true;
        Add(message);
        string? notePath = null;
        try
        {
            if (logDir != null)
            {
                notePath = Path.Combine(logDir, $"AnoMech-FATAL-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                var header = $"# AnoMech build={PluginBuildInfo.Checksum} fatal at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                File.WriteAllLines(notePath, Snapshot().TakeLast(400).Prepend(message).Prepend(header));
            }
        }
        catch
        {
            // Best-effort: the Dalamud log has the message either way.
        }
        Shutdown();
        return notePath;
    }

    private static void Add(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        lock (gate)
        {
            currentRunLines.Enqueue(line);
            currentRunBytes += line.Length + 1;
            // Queue.Dequeue is O(1), so sustained per-tick eviction stays cheap.
            while (currentRunBytes > RunBufferCapBytes && currentRunLines.Count > 0)
                currentRunBytes -= currentRunLines.Dequeue().Length + 1;
        }
        if (ShouldPersistToDisk()) EnqueueForDisk(line);
    }

    // Disk logging only while the plugin is doing something; idle time in the overworld would
    // generate I/O for nothing. Null-checked since this can run before Plugin's constructor has
    // assigned these statics.
    private static bool ShouldPersistToDisk()
        => ForcePersist
           || offline
           || (Plugin.MainWindow?.IsOpen ?? false)
           || Plugin.GameInstance?.ActiveScenario != null
           || Plugin.MultiplayerInstance?.SessionCode != null;

    // Set by DebugMenu's Position Logger: with MainWindow closed and no scenario active, nothing
    // would reach disk. A bool, not a refcount, while it has one caller.
    public static bool ForcePersist;

    // Called at the start of each scenario run. Only resets the in-memory view; the disk log
    // has no notion of runs.
    public static void Clear()
    {
        var marker = $"{DateTime.Now:HH:mm:ss.fff} === New run ===";
        lock (gate)
        {
            currentRunLines.Clear();
            currentRunLines.Enqueue(marker);
            currentRunBytes = marker.Length + 1;
        }
        if (ShouldPersistToDisk()) EnqueueForDisk(marker);
    }

    public static IReadOnlyList<string> Snapshot()
    {
        lock (gate) return currentRunLines.ToArray();
    }

    // ---- Disk-backed rotating log ---------------------------------------------------------

    private const long SegmentMaxBytes = 10L * 1024 * 1024;
    private const long TotalArchiveCapBytes = 20L * 1024 * 1024;
    private const string ActiveFileName = "AnoMech-active.log";
    private const string ArchivePrefix = "AnoMech-Debug-";
    private const string OfflineArchivePrefix = "AnoMech-Offline-";
    private const string ArchiveSuffix = ".log.gz";
    private const string ArchiveHeaderField = "archive=";
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromMilliseconds(500);

    // Prefix, with Rotate, names the segments after it; Flushed is set once everything before it
    // is on disk.
    private readonly record struct LogCommand(string? Line, bool Rotate, string? Prefix = null, TaskCompletionSource? Flushed = null);

    private static Channel<LogCommand>? channel;
    private static Task? writerTask;
    private static string? logDir;
    private static bool initialized;
    private static volatile bool offline;
    // One slow flush and the rest of the session stops waiting, so a stalled disk costs one pause.
    private static volatile bool flushTimedOut;

    // Null when disk logging is disabled; other debug captures live under the same folder.
    internal static string? LogDirectory => logDir;

    // Called once from Plugin's constructor; the only synchronous work is creating a directory.
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        try
        {
            var baseDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (baseDir == null)
            {
                Plugin.Log.Warning("[DiagnosticLog] No plugin assembly directory -- disk logging disabled, in-memory buffer still works.");
                return;
            }
            logDir = Path.Combine(baseDir, "anomech-logs");
            Directory.CreateDirectory(logDir);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[DiagnosticLog] Failed to set up log directory: {e.Message}");
            return;
        }

        channel = Channel.CreateBounded<LogCommand>(new BoundedChannelOptions(20_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        });
        writerTask = Task.Run(RunWriterLoopAsync);
    }

    private static void EnqueueForDisk(string line) => channel?.Writer.TryWrite(new LogCommand(line, false));

    // Archives the active segment now regardless of size (Game.Leave). Non-blocking.
    public static void RotateNow() => channel?.Writer.TryWrite(new LogCommand(null, true));

    // From offline mode's first check until it has put the game back.
    public static void BeginOffline()
    {
        offline = true;
        flushTimedOut = false;
        channel?.Writer.TryWrite(new LogCommand(null, true, OfflineArchivePrefix));
    }

    public static void EndOffline()
    {
        channel?.Writer.TryWrite(new LogCommand(null, true, ArchivePrefix));
        offline = false;
    }

    // Waits until everything logged so far is on disk, for a line a native crash must not lose.
    public static void Flush()
    {
        if (flushTimedOut || channel is not { } current || writerTask is not { IsCompleted: false }) return;
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (current.Writer.TryWrite(new LogCommand(null, false, Flushed: flushed)) && !flushed.Task.Wait(FlushTimeout))
            flushTimedOut = true;
    }

    // Waits briefly for the writer to flush, so an unload/reload doesn't lose the queued tail.
    public static void Shutdown()
    {
        channel?.Writer.TryComplete();
        try
        {
            writerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort -- an unusually slow disk shouldn't hang plugin teardown.
        }
        channel = null;
        writerTask = null;
        initialized = false;
    }

    private static async Task RunWriterLoopAsync()
    {
        string activePath;
        FileStream activeStream;
        StreamWriter activeWriter;
        long activeBytes;
        var prefix = ArchivePrefix;

        try
        {
            // Leftover active files from a reload, crash or failed load are preserved, not truncated.
            await RotateLeftoverActiveFilesAsync();
            (activePath, activeStream, activeWriter, activeBytes) = OpenFreshActiveFile(prefix);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[DiagnosticLog] Failed to open an active log file -- disk logging disabled for this session: {e.Message}");
            return;
        }

        var reader = channel!.Reader;
        await foreach (var cmd in reader.ReadAllAsync())
        {
            // Per-command try/catch -- one failed write/rotation shouldn't kill the writer.
            try
            {
                if (cmd.Line is { } line)
                {
                    await activeWriter.WriteLineAsync(line);
                    activeBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                }

                if ((cmd.Rotate || activeBytes >= SegmentMaxBytes) && activeBytes > 0)
                {
                    await activeWriter.FlushAsync();
                    await activeWriter.DisposeAsync();
                    await activeStream.DisposeAsync();
                    await RotateActiveFileAsync(activePath);
                    prefix = cmd.Prefix ?? prefix;
                    (activePath, activeStream, activeWriter, activeBytes) = OpenFreshActiveFile(prefix);
                }
                else if (reader.Count == 0 || cmd.Flushed != null)
                {
                    // Burst drained: flush so the active file is current mid-session or after a
                    // crash. Writes still batch under load.
                    await activeWriter.FlushAsync();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warning($"[DiagnosticLog] Writer loop error (continuing): {e.Message}");
            }
            finally
            {
                cmd.Flushed?.TrySetResult();
            }
        }

        await activeWriter.DisposeAsync();
        await activeStream.DisposeAsync();
    }

    // The canonical name first, then stamped siblings: a load whose constructor threw keeps its
    // handle on the canonical file until the game exits, which must cost a filename, not the log.
    private static (string FilePath, FileStream Stream, StreamWriter Writer, long Bytes) OpenFreshActiveFile(string prefix)
    {
        var canonical = Path.Combine(logDir!, ActiveFileName);
        foreach (var candidate in ActiveFileCandidates(canonical))
        {
            FileStream stream;
            try
            {
                // CreateNew: anything still at this name couldn't be rotated and must not be
                // truncated. FileShare.Delete so the next load can rename it away if this one
                // dies without closing it.
                stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
            }
            catch (IOException) when (File.Exists(candidate))
            {
                continue;
            }

            if (candidate != canonical)
                Plugin.Log.Warning($"[DiagnosticLog] {ActiveFileName} is still held open by an earlier load -- this session logs to {Path.GetFileName(candidate)} instead.");

            // Flushing every line would make each log call synchronous.
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
            // Stamped so a later rotation knows which build wrote the segment, and its archive name.
            var header = $"# AnoMech build={PluginBuildInfo.Checksum} started={DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                         + (prefix == ArchivePrefix ? "" : $" {ArchiveHeaderField}{prefix}");
            writer.WriteLine(header);
            writer.Flush();
            return (candidate, stream, writer, Encoding.UTF8.GetByteCount(header) + Environment.NewLine.Length);
        }

        throw new IOException("every active log file name is already in use");
    }

    private static IEnumerable<string> ActiveFileCandidates(string canonical)
    {
        yield return canonical;
        var stem = Path.GetFileNameWithoutExtension(ActiveFileName);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        yield return Path.Combine(logDir!, $"{stem}-{stamp}.log");
        for (var i = 2; i <= 10; i++)
            yield return Path.Combine(logDir!, $"{stem}-{stamp}-{i}.log");
    }

    // Every "AnoMech-active*.log", each on its own so one still held open doesn't stop the rest.
    private static async Task RotateLeftoverActiveFilesAsync()
    {
        var stem = Path.GetFileNameWithoutExtension(ActiveFileName);
        foreach (var file in new DirectoryInfo(logDir!).GetFiles($"{stem}*.log"))
        {
            try
            {
                await RotateActiveFileAsync(file.FullName);
            }
            catch (Exception e)
            {
                Plugin.Log.Warning($"[DiagnosticLog] Skipping leftover {file.Name}: {e.Message}");
            }
        }
    }

    // Moved aside first, then compressed, so a failed compression leaves the original intact.
    private static async Task RotateActiveFileAsync(string activePath)
    {
        if (!File.Exists(activePath)) return;
        if (new FileInfo(activePath).Length == 0)
        {
            try { File.Delete(activePath); } catch { /* harmless leftover, ignore */ }
            return;
        }

        var quarantinePath = Path.Combine(logDir!, $"AnoMech-active-pending-{Guid.NewGuid():N}.log");
        try
        {
            File.Move(activePath, quarantinePath);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[DiagnosticLog] Could not move {Path.GetFileName(activePath)} aside for rotation ({e.Message}) -- left in place. A load that died before Plugin.Dispose keeps its handle until the game exits; the file rotates on the first load after that.");
            return;
        }

        await CompressAndArchiveAsync(quarantinePath);
    }

    private static async Task CompressAndArchiveAsync(string sourcePath)
    {
        try
        {
            var (checksum, prefix) = ReadHeader(sourcePath);
            var shortChecksum = checksum.Length >= 6 ? checksum[..6] : checksum;
            var archivePath = NextArchivePath(prefix, shortChecksum);

            // ReadWrite|Delete share: a dead load's writer may still hold this file.
            var openedSource = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            await using (openedSource)
            {
                var openedDest = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using (openedDest)
                {
                    var gzip = new GZipStream(openedDest, CompressionLevel.Optimal);
                    await using (gzip)
                        await openedSource.CopyToAsync(gzip);
                }
            }

            File.Delete(sourcePath);
            EnforceTotalArchiveCap(prefix);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[DiagnosticLog] Failed to compress log segment {sourcePath}: {e.Message} (left uncompressed on disk).");
        }
    }

    private static (string Checksum, string Prefix) ReadHeader(string path)
    {
        try
        {
            using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            const string start = "# AnoMech build=";
            if (reader.ReadLine() is { } first && first.StartsWith(start))
            {
                var fields = first[start.Length..].Split(' ');
                var offlineSegment = fields.Contains($"{ArchiveHeaderField}{OfflineArchivePrefix}");
                return (fields[0], offlineSegment ? OfflineArchivePrefix : ArchivePrefix);
            }
        }
        catch
        {
            // Fall through -- an unreadable/missing header just means an older or damaged file.
        }
        return ("unknown", ArchivePrefix);
    }

    private static string NextArchivePath(string prefix, string shortChecksum)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var basePath = Path.Combine(logDir!, $"{prefix}{stamp}-{shortChecksum}{ArchiveSuffix}");
        if (!File.Exists(basePath)) return basePath;
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(logDir!, $"{prefix}{stamp}-{shortChecksum}-{i}{ArchiveSuffix}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    // Null for a non-matching name, so the caller can fall back to filesystem metadata.
    private static DateTime? ParseArchiveTimestamp(string fileName, string prefix)
    {
        if (!fileName.StartsWith(prefix)) return null;
        var rest = fileName[prefix.Length..];
        if (rest.Length < 15) return null; // "yyyyMMdd-HHmmss" is 15 chars
        var stamp = rest[..15];
        return DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", null,
            System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    // Each kind of archive has its own cap, so offline sessions and the debug log never evict each other.
    private static void EnforceTotalArchiveCap(string prefix)
    {
        try
        {
            // By the filename's stamp, not CreationTime, which a copy/move can reset.
            var files = new DirectoryInfo(logDir!)
                .GetFiles($"{prefix}*{ArchiveSuffix}")
                .OrderBy(f => ParseArchiveTimestamp(f.Name, prefix) ?? f.CreationTimeUtc)
                .ToList();
            var total = files.Sum(f => f.Length);
            foreach (var file in files)
            {
                if (total <= TotalArchiveCapBytes) break;
                try
                {
                    total -= file.Length;
                    file.Delete();
                }
                catch (Exception e)
                {
                    Plugin.Log.Warning($"[DiagnosticLog] Failed to delete old log archive {file.Name}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[DiagnosticLog] Failed to enforce archive size cap: {e.Message}");
        }
    }
}
