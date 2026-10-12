using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using AnoMech.Network;

namespace AnoMech.Relay;

internal sealed class PeerConn(WebSocket socket, uint id, IPAddress ip, Guid peerId)
{
    public readonly WebSocket Socket = socket;
    public readonly Guid PeerId = peerId;
    // A WebSocket allows one send at a time, and a broadcast, a greeting and a close can all
    // target the same socket at once.
    public readonly SemaphoreSlim SendGate = new(1, 1);
    // Set once the greeting is out, so no room traffic can arrive ahead of it.
    public bool Ready;
    public readonly uint Id = id;
    public readonly IPAddress Ip = ip;
    public readonly DateTime JoinedUtc = DateTime.UtcNow;
    public long MessagesIn;
    public long BytesIn;
    public int PeakMessagesPerSecond;
    public long PeakBytesPerSecond;
    public bool NearLimitWarned;
}

internal enum JoinOutcome { Joined, NotFound, Full, Banned, Moved }

// Evicted/Host: a ban issued while its target was away, learning the target's address on its
// return and clearing that network. The caller closes Evicted and tells Host who went.
// MovedTo: the relay in this one's group that holds the code.
internal sealed record JoinResult(JoinOutcome Outcome, PeerConn? Replaced = null, IReadOnlyList<PeerConn>? Evicted = null, PeerConn? Host = null,
    string? MovedTo = null)
{
    public string Reason => Outcome switch
    {
        JoinOutcome.NotFound => "session not found",
        JoinOutcome.Full => "session full",
        JoinOutcome.Banned => "banned from room",
        JoinOutcome.Moved => RelayWire.MovedPrefix + MovedTo,
        _ => "",
    };
}

// Others: everyone Removed besides the id the host named, which the host has to be told about.
internal sealed record ModerationResult(IReadOnlyList<PeerConn> Removed, Guid[] Others);

// The relay's session table and every membership rule over it. No I/O: each operation returns
// the connections the caller has to close or notify.
internal sealed class RelayRooms(RelayOptions options)
{
    private const int MaxRoomBans = 1024;

    private sealed class Room
    {
        public readonly List<PeerConn> Peers = new();
        // Guards this room's own Peers/LastActivityUtc only -- NOT the sessions table.
        // One lock per room (not one relay-wide lock) so unrelated sessions never contend with
        // each other on join/leave/broadcast; only two operations touching the SAME session
        // ever serialize against one another. See Join/Leave for why sessions removal also
        // has to happen while holding this lock, not the table's own (lock-free) operations.
        public readonly object Lock = new();
        // Whoever created the room, by authenticated identity rather than socket, so a host
        // that reconnects is the host again. Tagged onto every broadcast from them so a
        // receiving client can tell a real host message from a forgery.
        public Guid HostPeerId;
        // Null address: banned while not connected. Filled in the next time that identity tries.
        public readonly Dictionary<Guid, IPAddress?> Bans = new();
        public DateTime LastActivityUtc = DateTime.UtcNow;
        public readonly DateTime CreatedUtc = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<string, Room> sessions = new();

    public int Count => sessions.Count;

    public int TotalPeers => sessions.Values.Sum(r => { lock (r.Lock) return r.Peers.Count; });

    public bool TryCreate(PeerConn host, out string sessionCode)
    {
        // A soft cap now, not a hard one -- a burst of concurrent /host requests right at the
        // ceiling could transiently overshoot it by a few. MaxTotalSessions exists to bound
        // resource usage, not as a security invariant, so this is an acceptable trade for not
        // needing a relay-wide lock on every session creation.
        if (sessions.Count >= options.MaxTotalSessions)
        {
            sessionCode = "";
            return false;
        }
        var room = new Room { HostPeerId = host.PeerId };
        room.Peers.Add(host);
        string code;
        do { code = RelayWire.NewCode(options.GroupNumber); } while (!sessions.TryAdd(code, room));
        sessionCode = code;
        return true;
    }

    // Peer-join only -- a code nobody actually hosted is rejected immediately instead of
    // silently vivifying an empty room. Loops rather than a single TryGetValue+lock because
    // Leave() can retire (empty + remove) this exact room between the lookup and acquiring its
    // lock; the re-check inside the lock catches that race and retries against whatever's
    // actually current instead of joining a room that's already been thrown away.
    public JoinResult Join(string sessionCode, PeerConn peer)
    {
        if (options.GroupNumber is { } own && RelayWire.CodeServer(sessionCode) is var owner && owner != own)
            return options.Group.FirstOrDefault(m => m.Number == owner) is { } member
                ? new JoinResult(JoinOutcome.Moved, MovedTo: member.Url)
                : new JoinResult(JoinOutcome.NotFound);
        while (true)
        {
            if (!sessions.TryGetValue(sessionCode, out var room)) return new JoinResult(JoinOutcome.NotFound);
            lock (room.Lock)
            {
                if (!IsLive(sessionCode, room)) continue;
                // The host is exempt so banning someone on its own network can't lock it out.
                if (peer.PeerId != room.HostPeerId
                    && (room.Bans.ContainsKey(peer.PeerId) || room.Bans.Values.Contains(RelayServer.AbuseKey(peer.Ip))))
                {
                    // A ban made while this identity was disconnected learns its address now, and
                    // clears that address the way a ban on a connected player would have.
                    if (!room.Bans.TryGetValue(peer.PeerId, out var bannedAddress) || bannedAddress is not null)
                        return new JoinResult(JoinOutcome.Banned);
                    var address = RelayServer.AbuseKey(peer.Ip);
                    room.Bans[peer.PeerId] = address;
                    var evicted = room.Peers.Where(p => p.PeerId != room.HostPeerId && RelayServer.AbuseKey(p.Ip).Equals(address)).ToList();
                    foreach (var other in evicted) Remove(room, other);
                    return new JoinResult(JoinOutcome.Banned, Evicted: evicted, Host: room.Peers.FirstOrDefault(p => p.PeerId == room.HostPeerId));
                }
                // One connection per identity: a reconnect replaces a connection that hasn't
                // noticed it's dead yet, the host's included.
                var replaced = room.Peers.FirstOrDefault(p => p.PeerId == peer.PeerId);
                if (room.Peers.Count - (replaced == null ? 0 : 1) >= options.MaxPeersPerSession)
                    return new JoinResult(JoinOutcome.Full);
                if (replaced != null) Remove(room, replaced);
                room.Peers.Add(peer);
                room.LastActivityUtc = DateTime.UtcNow;
                return new JoinResult(JoinOutcome.Joined, Replaced: replaced);
            }
        }
    }

    public void Leave(string sessionCode, PeerConn peer)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return;
        // Removal from sessions happens while still holding this room's own lock -- the one
        // point that has to agree with Join's re-check above, so a peer can never be added
        // to a room in the instant between it going empty and being removed from the table.
        lock (room.Lock)
        {
            Remove(room, peer);
            if (room.Peers.Count == 0) sessions.TryRemove(new KeyValuePair<string, Room>(sessionCode, room));
        }
    }

    public int CountPeers(string sessionCode)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return 0;
        lock (room.Lock) return room.Peers.Count;
    }

    // False if the peer was removed (kicked, replaced, room gone) before its greeting went out.
    public bool MarkReady(string sessionCode, PeerConn peer)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return false;
        lock (room.Lock)
        {
            if (!IsLive(sessionCode, room) || !room.Peers.Contains(peer) || peer.Socket.State != WebSocketState.Open) return false;
            peer.Ready = true;
            return true;
        }
    }

    // Null when nothing was removed: an unban, a command not from the host, or a ban on
    // someone who isn't connected (recorded for their return).
    public ModerationResult? Moderate(string sessionCode, PeerConn sender, string? operation, Guid id)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return null;
        List<PeerConn> removed;
        lock (room.Lock)
        {
            if (!sender.Ready || !room.Peers.Contains(sender) || sender.PeerId != room.HostPeerId) return null;
            if (operation == "unban") { room.Bans.Remove(id); return null; }
            if (operation is not ("kick" or "ban") || id == room.HostPeerId) return null;
            var target = room.Peers.FirstOrDefault(p => p.PeerId == id);
            // Recorded even when they aren't connected: a ban issued between their reconnects
            // still has to stop the next one. A full ban list still kicks.
            if (operation == "ban" && (room.Bans.ContainsKey(id) || room.Bans.Count < MaxRoomBans))
                room.Bans[id] = target is null ? room.Bans.GetValueOrDefault(id) : RelayServer.AbuseKey(target.Ip);
            if (target == null) return null;
            removed = room.Peers.Where(p => ReferenceEquals(p, target)
                || (operation == "ban" && p.PeerId != room.HostPeerId && RelayServer.AbuseKey(p.Ip).Equals(RelayServer.AbuseKey(target.Ip)))).ToList();
            foreach (var peer in removed) Remove(room, peer);
        }
        // The host removed only the id it named. Anyone else the ban took with them leaves
        // without a word of their own, so without this they'd stay seated in its roster.
        return new ModerationResult(removed, removed.Where(p => p.PeerId != id).Select(p => p.PeerId).Distinct().ToArray());
    }

    // Null when the sender may not broadcast (not ready, no longer in the room). A peer's
    // traffic goes to the host alone; only the host speaks to everyone.
    public (bool FromHost, List<PeerConn> Targets)? Route(string sessionCode, PeerConn sender)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return null;
        lock (room.Lock)
        {
            if (!IsLive(sessionCode, room) || !sender.Ready || !room.Peers.Contains(sender)) return null;
            room.LastActivityUtc = DateTime.UtcNow;
            var fromHost = sender.PeerId == room.HostPeerId;
            return (fromHost, room.Peers.Where(peer => !ReferenceEquals(peer, sender) && peer.Ready && peer.Socket.State == WebSocketState.Open
                && (fromHost || peer.PeerId == room.HostPeerId)).ToList());
        }
    }

    public List<PeerConn> Clear()
    {
        var all = new List<PeerConn>();
        foreach (var (code, room) in sessions)
        {
            lock (room.Lock)
            {
                all.AddRange(room.Peers);
                room.Peers.Clear();
                sessions.TryRemove(new KeyValuePair<string, Room>(code, room));
            }
        }
        return all;
    }

    // Null: no such session.
    public List<PeerConn>? Disband(string sessionCode)
    {
        if (!sessions.TryGetValue(sessionCode, out var room)) return null;
        lock (room.Lock)
        {
            sessions.TryRemove(new KeyValuePair<string, Room>(sessionCode, room));
            return [.. room.Peers];
        }
    }

    // Enumerating a ConcurrentDictionary while calling TryRemove on it is safe (unlike
    // a plain Dictionary) -- no snapshot copy needed first.
    public List<(string Code, List<PeerConn> Peers)> RemoveIdleSince(DateTime cutoff)
    {
        var dead = new List<(string, List<PeerConn>)>();
        foreach (var (code, room) in sessions)
        {
            lock (room.Lock)
            {
                if (room.LastActivityUtc >= cutoff) continue;
                dead.Add((code, [.. room.Peers]));
                sessions.TryRemove(new KeyValuePair<string, Room>(code, room));
            }
        }
        return dead;
    }

    public (string Code, PeerConn Peer)? FindConnection(uint connectionId)
    {
        foreach (var (code, room) in sessions)
        {
            PeerConn? target;
            lock (room.Lock) target = room.Peers.FirstOrDefault(peer => peer.Id == connectionId);
            if (target != null) return (code, target);
        }
        return null;
    }

    public List<PeerConn> PeersAt(IPAddress key)
    {
        var matches = new List<PeerConn>();
        foreach (var (_, room) in sessions)
            lock (room.Lock) matches.AddRange(room.Peers.Where(peer => RelayServer.AbuseKey(peer.Ip).Equals(key)));
        return matches;
    }

    public List<SessionInfo> Snapshot()
    {
        var now = DateTime.UtcNow;
        var list = new List<SessionInfo>();
        foreach (var (code, room) in sessions)
        {
            lock (room.Lock)
            {
                list.Add(new SessionInfo(code, (now - room.CreatedUtc).TotalSeconds, (now - room.LastActivityUtc).TotalSeconds,
                    room.Peers.Select(peer => new PeerInfo(peer.Id, peer.Ip.ToString(),
                        peer.PeerId == room.HostPeerId, (now - peer.JoinedUtc).TotalSeconds,
                        peer.MessagesIn, peer.BytesIn, peer.PeakMessagesPerSecond, peer.PeakBytesPerSecond)).ToList()));
            }
        }
        return list.OrderBy(s => s.Code).ToList();
    }

    // Retired (or replaced) concurrently since the caller's lookup.
    private bool IsLive(string sessionCode, Room room)
        => sessions.TryGetValue(sessionCode, out var current) && ReferenceEquals(current, room);

    private static void Remove(Room room, PeerConn peer)
    {
        peer.Ready = false;
        room.Peers.Remove(peer);
    }
}
