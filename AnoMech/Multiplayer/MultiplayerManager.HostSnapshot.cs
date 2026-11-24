using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Host: what the run sends besides its packets -----------------------

    private MpMessage ForcedMoveMessage(SimWorld world, PartyRole role, ForcedMove move)
    {
        switch (move)
        {
            case ForcedMove.Knockback kb:
                return new KnockbackMessage(role, kb.From.X, kb.From.Y, kb.From.Z, kb.Row.Id);
            case ForcedMove.Push push:
                return new PushMessage(role, push.Heading, push.Distance, 0f, push.DurationSeconds);
            case ForcedMove.Carry carry:
                return new CarryMessage(role, carry.To.X, carry.To.Y, carry.To.Z);
            case ForcedMove.Teleport teleport:
                return new TeleportMessage(role, teleport.To.Position.X, teleport.To.Position.Y, teleport.To.Position.Z, teleport.To.Rotation);
            case ForcedMove.Follow follow:
                var (targetEnemy, targetRole) = ResolveEnd(world, follow.Target);
                return new FollowMessage(role, targetRole, targetEnemy, follow.Speed);
            default:
                throw new ArgumentOutOfRangeException(nameof(move), move, null);
        }
    }

    // In the order the mechanic made them: Umad P1's arrow releases the chase, then carries.
    private void SendForcedMoves()
    {
        var world = Plugin.Game.World;
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not SimNetworkPuppet puppet) continue;
            foreach (var move in puppet.DrainForcedMoves())
                _ = Send(ForcedMoveMessage(world, role, move));
        }
    }

    private void SendMidRunUpdate()
    {
        if (TryResolveScenario() is IMultiplayerReplayable replayable && replayable.BuildMidRunUpdateMessage() is { } update)
            _ = Send(update);
    }

    // Backpressure-gated like SelfPose, and sent when any seat in it changes.
    private Task? pendingRolesSend;
    private List<RoleState>? lastRoles;
    private long lastRolesMs;

    // The players' seats; a bot's goes out as its own movement packets.
    private void BroadcastRoles()
    {
        if (pendingRolesSend is not (null or { IsCompleted: true })) return;
        var party = Plugin.Game.World.Party;
        var roles = new List<RoleState>(8);
        foreach (var role in Enum.GetValues<PartyRole>())
            if (party.Get(role) is { } member and (SimPlayer or SimNetworkPuppet))
            {
                var (velocity, jumping) = member is SimNetworkPuppet puppet ? (puppet.PoseVelocity, puppet.PoseJumping) : (ownVelocity, Natives.PlayerInput.IsJumping);
                roles.Add(new RoleState(role, member.Position.X, member.Position.Y, member.Position.Z, member.Rotation, velocity.X, velocity.Z, jumping));
            }
        if (lastRoles != null && roles.SequenceEqual(lastRoles) && NowMs - lastRolesMs < PoseRefreshMs) return;
        lastRoles = roles;
        lastRolesMs = NowMs;
        pendingRolesSend = Send(new RolesSnapshotMessage(roles));
    }

    // An enemy goes by its entity id on the host, as its packets name it.
    private static (uint Enemy, PartyRole? Role) ResolveEnd(SimWorld world, SimCharacter? c)
    {
        if (c is null) return (PacketActor.None, null);
        if (c is SimEnemy e) return (e.EntityId, null);
        foreach (var role in Enum.GetValues<PartyRole>())
            if (ReferenceEquals(world.Party.Get(role), c)) return (PacketActor.None, role);
        return (PacketActor.None, null);
    }

    private void OnPartyMemberKilledHost(PartyRole role, string cause, uint? actionId, ushort? statusId)
        => _ = Send(new RoleKilledMessage(role, cause));

    // The host reads its own bookkeeping; a peer reads what the host relayed (PeerStatusMessage).
    public bool IsPeerStale(Guid peerId) => IsHost
        ? peerLastSeenMs.TryGetValue(peerId, out var lastSeen) && NowMs - lastSeen > PeerStaleTimeoutMs
        : peerStatuses.TryGetValue(peerId, out var entry) && entry.SecondsSinceLastSeen * 1000f > PeerStaleTimeoutMs;

    // TickCount64 only moves every ~15.6 ms, too coarse for the clock lead built on this ping.
    private long PingClockMs() => Time.GetElapsedTime(0).Ticks / TimeSpan.TicksPerMillisecond;

    // Comfortably more than MaxBehindMs of them.
    private const int KeptPings = 16;

    private void SendPingAndRefreshStatuses()
    {
        var nowMs = NowMs;
        var (streamSent, streamReceived) = stream?.PingFields() ?? (0, null!);
        var sentAt = PingClockMs();
        pingsSentMs.Enqueue(sentAt);
        while (pingsSentMs.Count > KeptPings) pingsSentMs.Dequeue();
        _ = Send(new PingMessage(sentAt, streamSent, streamReceived, HostRunClock()));

        peerStatuses.Clear();
        foreach (var peerId in Session.ClaimedBy.Values.Distinct())
        {
            if (peerId == MyPeerId || !peerLastSeenMs.TryGetValue(peerId, out var lastSeen)) continue;
            var latency = peerLatencyMs.TryGetValue(peerId, out var ms) ? ms : (float?)null;
            peerStatuses[peerId] = new PeerStatusEntry(latency, (nowMs - lastSeen) / 1000f);
        }
        _ = Send(new PeerStatusMessage(new Dictionary<Guid, PeerStatusEntry>(peerStatuses)));

        CheckPeerLiveness();
    }

    private void CheckPeerLiveness()
    {
        foreach (var (role, peerId) in Session.ClaimedBy)
        {
            if (peerId == MyPeerId) continue;
            var stale = IsPeerStale(peerId);
            if (stale && warnedStalePeers.Add(peerId))
            {
                DiagnosticLog.Warn($"[Multiplayer] {Session.NameOf(peerId)} ({role}) hasn't reported in over {PeerStaleTimeoutMs / 1000}s -- likely disconnected.");
                // Ends the run but keeps the role claim: staleness may be a blip, and their
                // client is already reconnecting.
                if (running && Plugin.Game.World.Map.IsInInstance)
                {
                    DiagnosticLog.Info($"[Multiplayer] Ending the run because {Session.NameOf(peerId)} went stale mid-fight.");
                    Plugin.Game.Leave();
                }
            }
            else if (!stale && warnedStalePeers.Remove(peerId))
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(peerId)} ({role}) is reporting in again.");
        }
    }

    // A peer whose download can't keep up still hears from the host, only ever later, so it never
    // goes stale; this far behind it plays a fight the others left seconds ago.
    private const long MaxBehindMs = 10_000;

    // How long ago the oldest ping the peer hasn't answered went out. Pings go in order with
    // everything else, so it hasn't taken anything the host sent since.
    private long PingsBehindMs(Guid peerId)
    {
        if (!answeredPingMs.TryGetValue(peerId, out var answered)) return 0;
        foreach (var sentAt in pingsSentMs)
            if (sentAt > answered) return PingClockMs() - sentAt;
        return 0;
    }

    private void EndRunIfBehind()
    {
        foreach (var peerId in Session.ClaimedBy.Values)
        {
            if (peerId == MyPeerId || IsPeerStale(peerId) || PingsBehindMs(peerId) <= MaxBehindMs) continue;
            var reason = $"{Session.NameOf(peerId)} fell more than {MaxBehindMs / 1000} seconds behind";
            DiagnosticLog.Warn($"[Multiplayer] {reason} -- ending the run for everyone.");
            Plugin.Game.Leave();
            BroadcastRunEnded(returnedToInn: true, reason);
            return;
        }
    }
}
