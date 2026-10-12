using System;
using AnoMech.Multiplayer;

namespace AnoMech.Windows;

// What Settings and the Multiplayer screen show about the chosen server, from its /info. Asked
// once the address stops changing (not on every keystroke), then every half minute while shown.
internal static class RelayServerStatus
{
    internal enum Reachability { Checking, Online, Unreachable }

    private const long SettleMs = 500;
    private const long RecheckMs = 30_000;

    private static string url = "";
    private static long urlSinceMs;
    private static long checkedAtMs;
    private static string? fetching;
    private static Reachability state;
    private static bool requiresPassword;

    internal static (Reachability State, bool RequiresPassword) Of(string address)
    {
        var now = Environment.TickCount64;
        if (address != url)
        {
            url = address;
            urlSinceMs = now;
            state = Reachability.Checking;
            requiresPassword = false;
        }
        var due = state == Reachability.Checking ? now - urlSinceMs >= SettleMs : now - checkedAtMs >= RecheckMs;
        if (due && fetching != address)
        {
            fetching = address;
            _ = RelayClient.FetchInfoAsync(address).ContinueWith(t => Plugin.Framework.Run(() =>
            {
                if (fetching == address) fetching = null;
                if (url != address) return;
                var info = t.IsCompletedSuccessfully ? t.Result : null;
                checkedAtMs = Environment.TickCount64;
                state = info is null ? Reachability.Unreachable : Reachability.Online;
                requiresPassword = info?.RequiresToken ?? false;
                if (info is not null) RelayGroups.Learn(address, info);
            }));
        }
        return (state, requiresPassword);
    }

    // What an earlier Of found, without asking the server; null until something has.
    internal static (Reachability State, bool RequiresPassword)? Known(string address)
        => address == url && state != Reachability.Checking ? (state, requiresPassword) : null;
}
