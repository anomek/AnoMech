using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;

namespace AnoMech.Relay;

public static class RelayAdmin
{
    // Matches the hand-written camelCase of /info and the WS greeting, so every response this
    // relay serves is shaped the same way.
    public static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    // The admin token goes out on every request, so it must not leave in the clear or be
    // redirected to another server.
    public static bool IsSafeAdminUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        return uri.Scheme == "https" || (uri.Scheme == "http" &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
             || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip)
                 && IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))));
    }
}

public sealed record RejectionCounts(
    long Origin, long IpCap, long JoinLockout, long RelayFull, long SessionFull,
    long SessionNotFound, long BadToken, long HandshakeTimeout, long MessageTooLarge, long MessageTimeout,
    long MessageRate, long ByteRate, long Unencrypted, long TooManyFragments, long Banned, long Paused,
    long BadProtocol, long TlsHandshake);

public sealed record LimitSettings(
    int MaxPeersPerSession, int MaxTotalSessions, long MaxMessageBytes, int MaxConnectionsPerIp,
    int MaxMessagesPerSecond, long MaxBytesPerSecond, int MaxFragmentsPerMessage, int MaxFailedJoinsPerWindow,
    double UsageWarnFraction);

public sealed record AdminStats(
    double UptimeSeconds, int RelayVersion, int Sessions, int TotalPeers, int ConnectionsByIpCount,
    int ActiveJoinLockouts, int BannedIpCount, bool AcceptingConnections,
    long TotalConnectionsAccepted, long TotalMessagesBroadcast, long TotalBytesBroadcast,
    RejectionCounts Rejections, long RecentRejections,
    long PeakMessagesPerSecond, long PeakBytesPerSecond, long NearLimitWarnings, LimitSettings Limits,
    long MemoryBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections);

public sealed record PeerInfo(uint Id, string Ip, bool IsHost, double AgeSeconds, long MessagesIn, long BytesIn,
    int PeakMessagesPerSecond, long PeakBytesPerSecond);

public sealed record SessionInfo(string Code, double AgeSeconds, double IdleSeconds, List<PeerInfo> Peers);

public sealed record AdminActionRequest(string Action, string? SessionCode, uint? ConnectionId, string? Ip, string? Name, double? Value);

public sealed record AdminActionResult(bool Ok, string Message);
