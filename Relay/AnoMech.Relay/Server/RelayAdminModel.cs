using System.Collections.Generic;
using System.Text.Json;

namespace AnoMech.Relay;

internal static class RelayAdmin
{
    // Matches the hand-written camelCase of /info and the WS greeting, so every response this
    // relay serves is shaped the same way.
    public static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
}

internal sealed record RejectionCounts(
    long Origin, long IpCap, long JoinLockout, long RelayFull, long SessionFull,
    long SessionNotFound, long BadToken, long HandshakeTimeout, long MessageTooLarge, long MessageTimeout,
    long MessageRate, long ByteRate, long Unencrypted, long TooManyFragments, long Banned, long Paused,
    long BadProtocol);

internal sealed record LimitSettings(
    int MaxPeersPerSession, int MaxTotalSessions, long MaxMessageBytes, int MaxConnectionsPerIp,
    int MaxMessagesPerSecond, long MaxBytesPerSecond, int MaxFragmentsPerMessage, int MaxFailedJoinsPerWindow,
    double UsageWarnFraction);

internal sealed record AdminStats(
    double UptimeSeconds, int RelayVersion, int Sessions, int TotalPeers, int ConnectionsByIpCount,
    int ActiveJoinLockouts, int BannedIpCount, bool AcceptingConnections,
    long TotalConnectionsAccepted, long TotalMessagesBroadcast, long TotalBytesBroadcast,
    RejectionCounts Rejections, long RecentRejections,
    long PeakMessagesPerSecond, long PeakBytesPerSecond, long NearLimitWarnings, LimitSettings Limits,
    long MemoryBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections);

internal sealed record PeerInfo(uint Id, string Ip, bool IsHost, double AgeSeconds, long MessagesIn, long BytesIn,
    int PeakMessagesPerSecond, long PeakBytesPerSecond);

internal sealed record SessionInfo(string Code, double AgeSeconds, double IdleSeconds, List<PeerInfo> Peers);

internal sealed record AdminActionRequest(string Action, string? SessionCode, uint? ConnectionId, string? Ip, string? Name, double? Value);

internal sealed record AdminActionResult(bool Ok, string Message);
