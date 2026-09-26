using AnoMech.Core;
using AnoMech.Multiplayer;

namespace AnoMech.Offline;

// Offline mode's own steps, in the disk log's offline segments. The log is written
// asynchronously, so each step waits until it is on disk: a native crash loses the last lines.
internal static class OfflineTrace
{
    public static void Begin()
    {
        DiagnosticLog.BeginOffline();
        Write($"[Offline] AnoMech {PluginBuildInfo.Version} ({PluginBuildInfo.ShortChecksum}), game {OfflineGameFiles.GameVersion()}.");
    }

    public static void End() => DiagnosticLog.EndOffline();

    public static void Write(string line)
    {
        DiagnosticLog.Info(line);
        DiagnosticLog.Flush();
    }
}
