using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.Core;

namespace AnoMech.Multiplayer;

// How far each server of a group is from this PC: the median of a few TCP connects, a round trip
// the server answers without its software seeing a request. Measured only while the Multiplayer
// screen asks (never from Settings), and only to public addresses unless the active server is
// itself on a private network, so a server list can't point the game at the player's own network.
internal static class RelayPings
{
    private const int Samples = 5;
    private const int ConnectTimeoutMs = 2000;
    private const long RepeatMs = 120_000;

    // By origin; a member that didn't answer has no entry.
    private static readonly Dictionary<string, double> results = new();
    private static string measuredList = "";
    private static long measuredAtMs;
    private static int pending;

    internal static bool Measuring => pending > 0;

    internal static double? Of(string url) => results.TryGetValue(Configuration.OriginOf(url), out var ms) ? ms : null;

    // Framework thread. Cheap to call every frame: it measures again only for a new list or once
    // RepeatMs has passed. Each member's result lands on its own, so one that never answers
    // doesn't hold up the rest.
    internal static void Measure(string homeUrl, RelayGroupInfo group)
    {
        var list = string.Join(' ', group.Members.Select(m => m.Url));
        var now = Environment.TickCount64;
        if (pending > 0 || (list == measuredList && now - measuredAtMs < RepeatMs)) return;
        measuredList = list;
        measuredAtMs = now;
        pending = group.Members.Length;
        var homeIsPrivate = Task.Run(async () => await ResolveAsync(homeUrl) is { Length: > 0 } home && home.All(a => !IsPublic(a)));
        foreach (var member in group.Members)
        {
            var origin = Configuration.OriginOf(member.Url);
            _ = Task.Run(async () => await PingAsync(member.Url, await homeIsPrivate)).ContinueWith(t => Plugin.Framework.Run(() =>
            {
                pending--;
                var ms = t.IsCompletedSuccessfully ? t.Result : null;
                if (ms is { } answered) results[origin] = answered;
                else results.Remove(origin);
                DiagnosticLog.Info($"[RelayPings] {member.Location}: {(ms is null ? "no answer" : $"{ms:F0} ms")}.");
            }));
        }
    }

    // An address that misses its first two connects is given up on for the next one, which also
    // covers a name with an IPv6 address on a PC without IPv6. A single miss can be a lost packet.
    private static async Task<double?> PingAsync(string url, bool allowPrivate)
    {
        if (Endpoint(url) is not { } endpoint) return null;
        foreach (var address in (await ResolveAsync(url) ?? []).Where(a => allowPrivate || IsPublic(a)))
        {
            var samples = new List<double>(Samples);
            for (var i = 0; i < Samples && (i < 2 || samples.Count > 0); i++)
                if (await ConnectMsAsync(address, endpoint.Port) is { } ms) samples.Add(ms);
            if (samples.Count == 0) continue;
            samples.Sort();
            return samples[samples.Count / 2];
        }
        return null;
    }

    private static async Task<double?> ConnectMsAsync(IPAddress address, int port)
    {
        using var timeout = new CancellationTokenSource(ConnectTimeoutMs);
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var started = Stopwatch.GetTimestamp();
            await socket.ConnectAsync(address, port, timeout.Token);
            return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException) { return null; }
    }

    private static async Task<IPAddress[]?> ResolveAsync(string url)
    {
        if (Endpoint(url) is not { } endpoint) return null;
        try { return await Dns.GetHostAddressesAsync(endpoint.Host); }
        catch (Exception e) when (e is SocketException or ArgumentException) { return null; }
    }

    // A bare address is dialed as wss:// first (RelayClient), so its port defaults to 443.
    private static (string Host, int Port)? Endpoint(string url)
    {
        var text = url.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "wss://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.IdnHost.Length == 0) return null;
        return (uri.IdnHost, uri.IsDefaultPort ? (uri.Scheme is "wss" or "https" ? 443 : 80) : uri.Port);
    }

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal
            || address.IsIPv6Multicast || address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.AddressFamily != AddressFamily.InterNetwork) return true;
        var b = address.GetAddressBytes();
        return b[0] is not (0 or 10) && b[0] < 224
               && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and < 32)
               && !(b[0] == 192 && b[1] == 168) && !(b[0] == 100 && b[1] is >= 64 and < 128);
    }
}
