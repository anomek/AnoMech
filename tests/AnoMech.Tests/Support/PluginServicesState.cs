using System.Reflection;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

// Plugin's services and debug bot control as they stand, put back on Dispose. A client points them
// at its own and leaves them so: a solo run after a session in the same process would read the
// host's open session and play its seats' settings, not its player's own.
internal sealed class PluginServicesState : IDisposable
{
    private static readonly PropertyInfo[] Properties = typeof(Plugin)
        .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Where(p => p.SetMethod != null)
        .ToArray();

    private readonly object?[] values = Properties.Select(p => p.GetValue(null)).ToArray();
    private readonly bool debugBotControl = DebugBotControl.Enabled;

    public void Dispose()
    {
        for (var i = 0; i < Properties.Length; i++) Properties[i].SetValue(null, values[i]);
        DebugBotControl.Enabled = debugBotControl;
    }
}
