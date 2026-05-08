using System;
using System.Diagnostics;
using System.IO;

namespace UltiSim.Core;

internal sealed class LogManager : IDisposable
{
    private StreamWriter? writer;

    internal string LogsDir =>
        Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "logs");

    internal void Open()
    {
        if (writer != null) return;
        Directory.CreateDirectory(LogsDir);
        var path = Path.Combine(LogsDir, $"ultisim-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        writer = new StreamWriter(path, append: false, System.Text.Encoding.UTF8) { AutoFlush = true };
        writer.WriteLine($"# UltiSim event log — {DateTime.Now:O}");
    }

    internal void Close()
    {
        writer?.Dispose();
        writer = null;
    }

    internal void OpenLogsFolder()
    {
        Directory.CreateDirectory(LogsDir);
        Process.Start(new ProcessStartInfo { FileName = LogsDir, UseShellExecute = true });
    }

    internal void LogMapEffect(uint index, ushort state, ushort flags)
    {
        writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MapEffect index=0x{index:X} state=0x{state:X} flags=0x{flags:X}");
    }

    internal void LogEnterInstance(uint territoryId, string territoryName)
    {
        writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] EnterInstance territory={territoryId} name=\"{territoryName}\"");
    }

    internal void LogCombatStart(uint territoryId)
    {
        writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CombatStart territory={territoryId}");
    }

    internal void LogCombatEnd(uint territoryId, bool wipe)
    {
        writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CombatEnd territory={territoryId} wipe={wipe}");
    }

    public void Dispose() => Close();
}
