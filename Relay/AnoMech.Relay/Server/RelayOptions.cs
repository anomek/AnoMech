using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace AnoMech.Relay;

internal interface IRelayLog
{
    // Events an operator should see live: startup, session lifecycle, rejections severe enough
    // to carry their own message, alerts, summaries.
    void Info(string message);
    void Warn(string message);
    // High-volume lines (individual rejection reasons, every broadcast) that would drown out
    // the live events if echoed alongside them.
    void Detail(string message);
}

// Limits are mutable because the admin endpoint can retune them on a live relay; RelayServer
// works on its own copy, so changing this object after construction has no effect.
internal sealed record RelayOptions
{
    // IPv6Any listens dual-stack.
    public IPAddress BindAddress { get; set; } = IPAddress.IPv6Any;
    public int Port { get; set; } = 7890;

    // Hard cap per session so one room can't be griefed into an unbounded fan-out.
    public int MaxPeersPerSession { get; set; } = 8;

    // Hard cap on live rooms process-wide -- without this, spamming /host costs nothing and
    // grows the session table unbounded.
    public int MaxTotalSessions { get; set; } = 500;

    // One logical message can't exceed this once reassembled from fragments -- otherwise a
    // client that never sends EndOfMessage (or sends gigabytes of it) can OOM the process.
    // ~100x the largest frame a full 8-peer run produces (see MaxMessagesPerSecond).
    public long MaxMessageBytes { get; set; } = 1 * 1024 * 1024;

    // Live sockets allowed from one source address at once, across every room. Sized with
    // slack for legitimate NAT/CGNAT sharing (mobile carriers, corporate networks) -- a
    // public relay sees much more of this than a friend-only one, so don't set this too tight.
    public int MaxConnectionsPerIp { get; set; } = 64;

    // Brute-force guard shared by every kind of guessable secret (session codes, the access
    // token): this many failures from one address inside the window trips a lockout, so
    // guessing at scale isn't free. Admin-token failures use their own bucket so an
    // admin-endpoint scan can never lock players out of joining.
    public int MaxFailedJoinsPerWindow { get; set; } = 10;

    // Per-connection send caps. Budgeted from the worst case a legitimate full session
    // produces, then multiplied by ~10:
    //   A host broadcasts one WorldSnapshot + one RolesSnapshot per frame (backpressure-gated
    //   in MultiplayerManager.Tick), so an uncapped 200fps client tops out near 450 msg/s and,
    //   with a ~40-enemy scenario compressing to <10 KB a snapshot, near 2 MB/s. A peer sends
    //   one SelfPose per frame plus occasional state, so roughly half that.
    // Anything past these is far outside what any scenario can generate.
    public int MaxMessagesPerSecond { get; set; } = 5000;
    public long MaxBytesPerSecond { get; set; } = 10L * 1024 * 1024;

    // Fraction of any per-connection cap that triggers a one-shot [NEAR-LIMIT] log line.
    // Purely advisory: it is how an operator finds out a real scenario is creeping toward a
    // cap before anyone actually gets cut off.
    public double UsageWarnFraction { get; set; } = 0.5;

    // Fragments allowed while assembling ONE message, independent of MaxMessageBytes -- a
    // real client's sends arrive as whatever chunk size the OS socket buffer gives, nowhere
    // near this many frames even for a large message; this only bounds someone deliberately
    // sending many tiny frames to burn CPU on ReceiveAsync round-trips while staying under
    // the byte cap.
    public int MaxFragmentsPerMessage { get; set; } = 2000;

    // Optional shared secret gating /host and /session/<code> -- null means anyone can connect
    // (the original friend-relay default). Compared with FixedTimeEquals so response timing
    // can't leak how much of a guess was right.
    public string? AccessToken { get; set; }

    // Separate secret gating /admin/*. Deliberately independent from AccessToken -- the
    // people you hand the relay's join token to are not necessarily people who should see
    // live abuse counters and IP-level state. Endpoints are 404 (not "401 with an empty
    // check") when unset, so they don't even reveal they exist on a relay nobody enabled.
    public string? AdminToken { get; set; }

    // Independent of the tokens -- a relay with no password at all still carries session
    // codes and full match state, which an operator may want encrypted end-to-end regardless
    // of whether anyone's protecting a secret.
    public bool RequireTls { get; set; }

    // Addresses allowed to speak for someone else: only a request arriving FROM one of these
    // has its forwarded client address / X-Forwarded-Proto believed. Empty means every request
    // is judged purely on its transport address, which is correct for a directly-exposed relay
    // and is why this can't default to "trust the header".
    public List<IPNetwork> TrustedProxies { get; set; } = new();
    public string ClientIpHeader { get; set; } = "X-Forwarded-For";

    public const int MinTokenLength = 16;

    // Null when the options are usable. The relay refuses to start rather than just warn:
    // a short token is still guessable within the lockout's own budget given enough patience
    // or rotating IPs, and a generated secret costs nothing to make longer.
    public string? Validate()
    {
        if (AccessToken is { Length: < MinTokenLength } || AdminToken is { Length: < MinTokenLength })
            return $"--token/--admin-token must be at least {MinTokenLength} characters -- " +
                   "a short shared secret is still guessable over time even with the lockout in place. " +
                   "Generate one with e.g. `openssl rand -hex 16`.";

        // TLS here always means a reverse proxy terminating it, so "is this encrypted" is only
        // ever answerable from a header -- and a header is only evidence if the request came
        // from a proxy we were told to trust. Starting without that mapping would mean either
        // believing the header from anyone (spoofable) or rejecting every connection.
        if ((RequireTls || AccessToken != null || AdminToken != null) && TrustedProxies.Count == 0)
            return "A token or --require-tls is set, so TLS is enforced -- which needs --trusted-proxy " +
                   "<cidr> naming the reverse proxy that terminates it (e.g. --trusted-proxy 127.0.0.1/32 " +
                   "for a local Caddy/nginx). Without it, X-Forwarded-Proto could be spoofed by anyone " +
                   "who can reach this port directly. See Relay/README.md.";

        return null;
    }

    public static bool TryParseNetwork(string cidr, out IPNetwork network)
    {
        if (IPNetwork.TryParse(cidr, out network))
        {
            // Stored in the form NormalizeIp gives the addresses it is matched against.
            if (!network.BaseAddress.IsIPv4MappedToIPv6) return true;
            if (network.PrefixLength < 96) return false;
            network = new IPNetwork(network.BaseAddress.MapToIPv4(), network.PrefixLength - 96);
            return true;
        }
        // A bare address is the common case for a local proxy; treat it as a single host.
        if (IPAddress.TryParse(cidr, out var single))
        {
            single = RelayServer.NormalizeIp(single);
            network = new IPNetwork(single, single.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32);
            return true;
        }
        return false;
    }
}
