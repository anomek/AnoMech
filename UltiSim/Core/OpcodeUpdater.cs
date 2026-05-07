using System;
using System.Linq;
using System.Net.Http;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ThreadingTask = System.Threading.Tasks.Task;

namespace UltiSim.Core;

// Downloads per-version ZoneDown opcode allowlist from Hyperborea's GitHub so the
// packet firewall stays current after game patches. Runs asynchronously on first
// plugin load when the stored game version doesn't match the running binary.
internal sealed unsafe class OpcodeUpdater : IDisposable
{
    private static string CurrentVersion =>
        $"{Framework.Instance()->GameVersionString}_{typeof(OpcodeUpdater).Assembly.GetName().Version}";

    private volatile bool disposed;

    internal OpcodeUpdater()
    {
        if (CurrentVersion == Plugin.Config.ZoneFirewallGameVersion)
        {
            Plugin.Log.Information("[OpcodeUpdater] Opcodes are current.");
            return;
        }
        Plugin.Log.Information("[OpcodeUpdater] Game version changed — fetching new opcodes.");
        var version = new string(Framework.Instance()->GameVersionString);
        ThreadingTask.Run(() => DownloadOpcodes(version));
    }

    private void DownloadOpcodes(string gameVersion)
    {
        if (disposed) return;
        using var client = new HttpClient();
        try
        {
            var url = $"https://github.com/kawaii/Hyperborea/raw/main/opcodes/{gameVersion}.txt";
            var lines = client.GetStringAsync(url).Result
                .ReplaceLineEndings()
                .Split(Environment.NewLine);
            if (disposed) return;
            foreach (var line in lines)
            {
                if (!line.StartsWith("ZoneDown=")) continue;
                var opcodes = line["ZoneDown=".Length..].Split(",")
                    .Select(uint.Parse)
                    .ToArray();
                if (!opcodes.Any(o => o != 0)) throw new Exception("Parsed empty opcode list.");
                Plugin.Framework.Run(() =>
                {
                    Plugin.Config.ZoneDownOpcodes = opcodes;
                    Plugin.Config.ZoneFirewallGameVersion = CurrentVersion;
                    Plugin.Config.Save();
                    Plugin.Log.Information($"[OpcodeUpdater] ZoneDown opcodes updated: {string.Join(", ", opcodes)}");
                });
                return;
            }
            Plugin.Log.Warning("[OpcodeUpdater] ZoneDown= line not found in opcode file.");
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"[OpcodeUpdater] Failed to fetch opcodes: {e.Message}");
        }
    }

    public void Dispose() => disposed = true;
}
