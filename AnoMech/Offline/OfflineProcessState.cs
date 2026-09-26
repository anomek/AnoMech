using System;

namespace AnoMech.Offline;

// Process-wide facts that must survive a plugin reload (a reloaded assembly starts with fresh
// statics, but the game it runs in keeps whatever an earlier load did to it).
internal static class OfflineProcessState
{
    private const string LastLogoutKey = "AnoMech.Offline.LastLogout";
    private const string TaintedKey = "AnoMech.Offline.Tainted";
    private const string InWorldKey = "AnoMech.Offline.InWorld";

    // Offline mode has changed game state that isn't put back yet.
    public static bool Tainted
    {
        get => Get(TaintedKey);
        set => Set(TaintedKey, value);
    }

    // Environment.TickCount64 of the last real logout, whose saves offline mode waits out.
    public static long LastLogout
    {
        get => AppDomain.CurrentDomain.GetData(LastLogoutKey) is long ticks ? ticks : 0;
        set => AppDomain.CurrentDomain.SetData(LastLogoutKey, value);
    }

    public static bool InWorld
    {
        get => Get(InWorldKey);
        set => Set(InWorldKey, value);
    }

    private static bool Get(string key) => AppDomain.CurrentDomain.GetData(key) is true;

    private static void Set(string key, bool value) => AppDomain.CurrentDomain.SetData(key, value);
}
