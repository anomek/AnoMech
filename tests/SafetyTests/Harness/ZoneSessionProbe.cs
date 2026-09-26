using System;
using System.Reflection;
using AnoMech.Core.Map;

namespace AnoMech.SafetyTests.Harness;

// Read access to ZoneSession's private state, for judging the harness's invariants. A renamed
// field fails loudly here rather than silently weakening a check.
internal static class ZoneSessionProbe
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static FieldInfo Field(string name)
        => typeof(ZoneSession).GetField(name, Any)
           ?? throw new InvalidOperationException($"ZoneSession no longer has a field '{name}'; update ZoneSessionProbe.");

    public static bool GuardArmed(ZoneSession z) => (bool)Field("guardArmed").GetValue(z)!;
    public static bool SendHoldActive(ZoneSession z) => (bool)Field("sendHoldActive").GetValue(z)!;
    public static string? TripReason(ZoneSession z) => (string?)Field("tripReason").GetValue(z);
    public static string? PendingLift(ZoneSession z) => (string?)Field("pendingLift").GetValue(z);
    public static int StayId(ZoneSession z) => (int)Field("stayId").GetValue(z)!;

    // The start gate's timers are static and outlive a ZoneSession; every test starts clean.
    public static void ResetStatics()
    {
        foreach (var f in typeof(ZoneSession).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsLiteral || f.IsInitOnly) continue;
            f.SetValue(null, f.FieldType.IsValueType ? Activator.CreateInstance(f.FieldType) : null);
        }
    }
}
