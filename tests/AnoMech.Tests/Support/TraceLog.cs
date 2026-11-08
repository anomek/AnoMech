using System.Reflection;
using Dalamud.Plugin.Services;

namespace AnoMech.Tests;

// Plugin.Log for a headless run: every line at every level, stamped with the scenario clock.
public class TraceLog : DispatchProxy
{
    private readonly List<string> lines = [];
    private Func<float> clock = () => 0f;

    public IReadOnlyList<string> Lines => lines;
    public List<string> Warnings { get; } = [];

    // file:line of the scheduler entry fired last.
    public string LastFired { get; private set; } = "";

    public static TraceLog Create(Func<float> clock)
    {
        var log = (TraceLog)(object)Create<IPluginLog, TraceLog>();
        log.clock = clock;
        return log;
    }

    // A real RelayClient logs from its own threads.
    public void Add(string level, string message)
    {
        lock (lines) lines.Add($"t={clock(),7:F2} {level,-5} {message}");
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name is { } name && Level(name) is { } level)
        {
            var message = Format(args ?? []);
            if (message.StartsWith(FiredPrefix, StringComparison.Ordinal))
                LastFired = message[FiredPrefix.Length..].Split(' ')[0];
            Add(level, message);
            if (level is "WARN" or "ERROR" or "FATAL")
                lock (lines) Warnings.Add(message);
        }
        return method is { ReturnType.IsValueType: true } && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }

    private const string FiredPrefix = "[EventScheduler] fired ";

    private static string? Level(string method) => method switch
    {
        nameof(IPluginLog.Verbose) => "VERB",
        nameof(IPluginLog.Debug) => "DEBUG",
        nameof(IPluginLog.Info) or nameof(IPluginLog.Information) => "INFO",
        nameof(IPluginLog.Warning) => "WARN",
        nameof(IPluginLog.Error) => "ERROR",
        nameof(IPluginLog.Fatal) => "FATAL",
        _ => null,
    };

    private static string Format(object?[] args)
    {
        var parts = new List<string>();
        foreach (var arg in args)
        {
            switch (arg)
            {
                case string text: parts.Add(text); break;
                case Exception e: parts.Add(e.ToString()); break;
                case object?[] values when values.Length > 0: parts.Add($"[{string.Join(", ", values)}]"); break;
            }
        }
        return string.Join(" ", parts);
    }
}
