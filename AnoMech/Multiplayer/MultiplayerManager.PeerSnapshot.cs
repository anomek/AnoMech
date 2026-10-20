using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Our own player, as the peer's SelfPose and the host's Roles give it -

    // Our player's velocity over the ground. The client walks them after the plugin's update, so a
    // tick sees the walk of the frame before, made in that frame's time.
    private Vector3 ownVelocity;
    private Vector3? ownLastPosition;
    private float ownLastDelta;

    private void TrackOwnVelocity(float deltaSeconds)
    {
        var position = Plugin.Game.World.Party.Player?.Position;
        ownVelocity = position is { } now && ownLastPosition is { } last && ownLastDelta > 0f
            ? new Vector3(now.X - last.X, 0f, now.Z - last.Z) / ownLastDelta
            : Vector3.Zero;
        ownLastPosition = position;
        ownLastDelta = deltaSeconds;
    }

    // A pose that hasn't changed goes out again this often, for whoever missed it.
    private const long PoseRefreshMs = 1000;

    // ---- Peer: reporting our own pose --------------------------------------

    // Backpressure-gated: one in flight at a time. Sent when it changes; a stop changes the
    // velocity, so the others' copies stop with it.
    private Task? pendingSelfPoseSend;
    private SelfPoseMessage? lastSelfPose;
    private long lastSelfPoseMs;

    private void SendSelfPose()
    {
        if (pendingSelfPoseSend is not (null or { IsCompleted: true })) return;
        var player = Plugin.Game.World.Party.Player;
        if (player == null) return;
        var pose = new SelfPoseMessage(MyPeerId, player.Position.X, player.Position.Y, player.Position.Z, player.Rotation,
            ownVelocity.X, ownVelocity.Z, Natives.PlayerInput.IsJumping);
        if (pose == lastSelfPose && NowMs - lastSelfPoseMs < PoseRefreshMs) return;
        lastSelfPose = pose;
        lastSelfPoseMs = NowMs;
        pendingSelfPoseSend = Send(pose);
    }

    public void ReportAppliedEnemyStatus(IReadOnlyList<SimEnemy> enemies, ushort statusId, float duration, ushort param)
    {
        if (IsHost || relay is not { IsConnected: true } || replay is null || enemies.Count == 0) return;
        var hostIds = enemies.Select(replay.HostActorOf).Where(id => id != PacketActor.None).ToList();
        if (hostIds.Count == 0) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting applied status {statusId} on the host's enemies [{string.Join(",", hostIds.Select(id => $"0x{id:X}"))}].");
        _ = Send(new PeerAppliedEnemyStatusMessage(MyPeerId, hostIds, statusId, duration, param));
    }

    public void ReportAppliedRoleStatus(IReadOnlyList<PartyRole> roles, ushort statusId, float duration, ushort param)
    {
        if (IsHost || relay is not { IsConnected: true } || roles.Count == 0) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting applied status {statusId} on roles [{string.Join(",", roles)}] to host.");
        _ = Send(new PeerAppliedRoleStatusMessage(MyPeerId, roles.ToList(), statusId, duration, param));
    }

    public void ReportProvokedEnemies(IReadOnlyList<SimEnemy> enemies)
    {
        if (IsHost || relay is not { IsConnected: true } || replay is null || enemies.Count == 0) return;
        var hostIds = enemies.Select(replay.HostActorOf).Where(id => id != PacketActor.None).ToList();
        if (hostIds.Count == 0) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting a provoke on the host's enemies [{string.Join(",", hostIds.Select(id => $"0x{id:X}"))}].");
        _ = Send(new PeerProvokedEnemyMessage(MyPeerId, hostIds));
    }

    // One report per enemy per run: only the first attacker matters.
    private readonly HashSet<uint> reportedAttacks = [];

    public void ReportAttackedEnemy(SimEnemy enemy)
    {
        if (IsHost || relay is not { IsConnected: true } || replay is null) return;
        var hostId = replay.HostActorOf(enemy);
        if (hostId == PacketActor.None || !reportedAttacks.Add(hostId)) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting a first attack on the host's enemy 0x{hostId:X}.");
        _ = Send(new PeerAttackedEnemyMessage(MyPeerId, hostId));
    }

    // True when the host now owns the cleanse: its copy holds the statuses.
    public bool ReportDispelled(PartyRole role)
    {
        if (IsHost || relay is not { IsConnected: true } || replay is null) return false;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting a cleanse on role {role} to host.");
        _ = Send(new PeerDispelledMessage(MyPeerId, role));
        return true;
    }

    // ---- Host: applying a peer's reported pose to their puppet -------------

    private void OnSelfPoseReceived(SelfPoseMessage msg)
    {
        if (!IsHost) return;
        if (Session.RoleOf(msg.PeerId) is not { } role)
        {
            DiagnosticLog.Debug($"[Multiplayer] SelfPose from {msg.PeerId} but they hold no claimed role -- dropping.");
            return;
        }
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var pose)) return;
        if (Plugin.Game.World.Party.Get(role) is SimNetworkPuppet puppet)
            puppet.ApplyNetworkPose(pose, NetGuard.Rotation(msg.Rotation), NetGuard.Velocity(msg.VX, msg.VZ), msg.Jumping);
        else
            DiagnosticLog.Debug($"[Multiplayer] SelfPose from {Session.NameOf(msg.PeerId)} ({role}) but that slot isn't a SimNetworkPuppet -- dropping.");
    }

    // ---- Peer: entering the host's run --------------------------------------

    // The other players' seats; the rest are the host's bots.
    private HashSet<PartyRole> RemoteSeats() => Session.ClaimedBy.Where(kv => kv.Value != MyPeerId).Select(kv => kv.Key).ToHashSet();

    // Zones in (unless already staying there) and sets the run's stage, as the host does at the
    // same time; answered with EnteredMessage once done.
    private void OnEnterReceived(EnterMessage msg)
    {
        if (IsHost) return;
        if (MyClaimedRole is not { } myRole)
        {
            DiagnosticLog.Warn("[Multiplayer] Host is entering the run, but I never claimed a role -- ignoring.");
            return;
        }
        if (TryResolveScenario() is not { } scenario)
        {
            AbortStart("the host chose a scenario this build doesn't have");
            return;
        }
        var zone = scenario.Phase.Zone;
        var inZone = Plugin.Game.Zone == zone && Plugin.Game.Mode == SessionMode.Peer;
        // The start check is advisory (state changes in between), so refuse here and say why.
        if (!inZone && CheckOwnStartReadiness() is { } notReady)
        {
            AbortStart(notReady);
            return;
        }

        RunEndReason = null;
        DiagnosticLog.Info($"[Multiplayer] Peer entering '{scenario.Name}' as {myRole}.");
        // Our own seed, as each client rolls: a peer's bot replays the host's rolls from its
        // replay state, never from a stream it shares with the host.
        peerRunParams = new RunScenarioParams(scenario, new LobbyPartyProvider(this, isPeer: true), null, Session.SelectedWaymark, NextSeed());
        peerNatives ??= PeerNatives.Install();
        if (replay == null)
        {
            replay = new PacketReplay(peerNatives, Plugin.Game.World, myRole);
            replay.Copied += (local, host) => ActorReplayed?.Invoke(local, host);
        }
        earlyPackets.Clear();
        reportedAttacks.Clear();
        peerEnteredInstance = false;
        peerRunStarted = false;
        peerEntryQueued = true;
        endAfterPeerEntry = null;
        StopDebugBotReplay();
        running = true;
        if (inZone)
        {
            Plugin.Game.PrepareRun(peerRunParams, OnPeerStageSet);
            return;
        }
        Plugin.Game.ZoneIn(zone, SessionMode.Peer, refusal =>
        {
            if (refusal != null) OnPeerStageSet(refusal);
            else if (peerRunParams is { } p) Plugin.Game.PrepareRun(p, OnPeerStageSet);
        });
    }

    private void OnPeerStageSet(string? refusal)
    {
        peerEntryQueued = false;
        var end = endAfterPeerEntry;
        endAfterPeerEntry = null;
        if (!running)
        {
            // Ended while the entry was queued: what the end would have done had it come after.
            if (refusal != null) return;
            if (end == false) Plugin.Game.Reset();
            else if (Plugin.Game.World.Map.IsInInstance) LeavePeerStay();
            return;
        }
        if (refusal != null)
        {
            running = false;
            AbortStart(refusal);
            return;
        }
        peerNatives?.TagSeats(Plugin.Game.World.Party, RemoteSeats());
        TagBotSeats();
        peerEnteredInstance = true;
        lastSelfPose = null;
        DiagnosticLog.Info("[Multiplayer] Peer is in the zone with the party seated -- telling the host.");
        foreach (var packet in earlyPackets) replay?.Apply(packet);
        earlyPackets.Clear();
        _ = Send(new EnteredMessage(MyPeerId));
    }

    // Idempotent: a fresh start delivers both LobbyState(Started) and StartMessage. A peer that
    // wasn't there for the Enter waits for the next run: the run so far reached it as packets
    // it never got. A lobby broadcast from before the host's run is up carries no clock.
    private void OnStartReceived(RunClockState? clock)
    {
        if (IsHost || peerRunStarted || clock is null) return;
        if (!running || !peerEnteredInstance || peerRunParams is not { } p) return;
        peerRunStarted = true;
        peerRunStartPending = true;
        var receivedAt = Time.GetTimestamp();
        if (peerNatives != null) peerNatives.HostOwnsArena = true;
        Plugin.Game.StartScenario(p, refusal => OnPeerStartResolved(refusal, clock, receivedAt));
    }

    // The clock syncs to the host's once the run is up: starting it sets it to zero.
    private void OnPeerStartResolved(string? refusal, RunClockState clock, long receivedAt)
    {
        peerRunStartPending = false;
        if (!running) return;
        if (refusal != null)
        {
            running = false;
            AbortStart(refusal);
            return;
        }
        hostClockAtStart = clock;
        hostClockReceivedAt = receivedAt;
        eventClockSynced = false;
        pingClock = null;
        DiagnosticLog.Info("[Multiplayer] Peer's run is up -- applying the host's packets and sending SelfPose.");
        lastSelfPose = null;
        peerNatives?.TagSeats(Plugin.Game.World.Party, RemoteSeats());
        TagBotSeats();
        TryStartDebugBotReplay();
    }

    private void OnGamePacketsReceived(GamePacketsMessage msg)
    {
        if (IsHost || !running || replay is null) return;
        var packets = NetGuard.Cap(msg.Packets, NetGuard.MaxPacketsPerMessage);
        if (!peerEnteredInstance)
        {
            if (earlyPackets.Count + packets.Count <= NetGuard.MaxQueuedMessages) earlyPackets.AddRange(packets);
            return;
        }
        if (!Plugin.Game.World.Map.IsInInstance) return;
        foreach (var packet in packets) replay.Apply(packet);
    }

    // Everything the stay put on this client goes before the zone does: the natives the teardown
    // writes through are the real ones again, so the party's despawn and our own player's
    // restore (life, statuses, afflictions) reach the client.
    private void LeavePeerStay()
    {
        replay?.Clear();
        replay = null;
        peerNatives?.Uninstall();
        peerNatives = null;
        peerEnteredInstance = false;
        peerRunStarted = false;
        if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
    }

    // The players' seats as the host has them; a bot's seat moves with its packets instead.
    private void OnRolesSnapshotReceived(RolesSnapshotMessage snap)
    {
        if (!PeerInRun) return;
        foreach (var r in NetGuard.Cap(snap.Roles, Enum.GetValues<PartyRole>().Length))
            if (r.Role != MyClaimedRole && Plugin.Game.World.Party.Get(r.Role) is SimNetworkPuppet puppet
                && NetGuard.TryPosition(r.X, r.Y, r.Z, out var position))
                puppet.ApplyNetworkPose(position, NetGuard.Rotation(r.Rotation), NetGuard.Velocity(r.VX, r.VZ), r.Jumping);
    }

    // Forced moves land on our own character only: every other slot is a puppet the host's
    // poses already place.
    private SimCharacter? OwnMember(PartyRole role, string what)
    {
        if (role != MyClaimedRole) return null;
        if (Plugin.Game.World.Party.Get(role) is { } member) return member;
        DiagnosticLog.Debug($"[Multiplayer] {what} for {role} but that slot is empty locally -- dropping.");
        return null;
    }

    private void OnTeleportReceived(TeleportMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var position)) return;
        OwnMember(msg.Role, "Teleport")?.TeleportTo(new Placement(position, NetGuard.Rotation(msg.Rotation)));
    }

    private void OnCarryReceived(CarryMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var destination)) return;
        OwnMember(msg.Role, "Carry")?.CarryTo(destination);
    }

    private void OnPushReceived(PushMessage msg)
    {
        if (!PeerInRun) return;
        if (OwnMember(msg.Role, "Push") is not { } member) return;
        var heading = NetGuard.Rotation(msg.Heading);
        var distance = NetGuard.Clamp(msg.Distance, 0f, 200f);
        if (msg.DurationSeconds > 0f)
            member.Push(heading, distance, NetGuard.Clamp(msg.DurationSeconds, 0.01f, 60f));
        else if (msg.Speed > 0f)
            member.Push(heading, distance, distance / NetGuard.Clamp(msg.Speed, 0.01f, 500f));
    }

    private void OnMarchReceived(MarchMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var destination)) return;
        OwnMember(msg.Role, "March")?.MarchTo(destination, NetGuard.Clamp(msg.Speed, 0.01f, 50f));
    }

    private void OnFollowReceived(FollowMessage msg)
    {
        if (!PeerInRun) return;
        if (OwnMember(msg.Role, "Follow") is not { } me) return;
        var target = msg.TargetRole is { } role ? Plugin.Game.World.Party.Get(role) : replay?.MirrorOfHost(msg.TargetEnemy);
        DiagnosticLog.Info($"[Multiplayer] Peer: {msg.Role} {(target == null ? "released from follow" : $"following {msg.TargetRole?.ToString() ?? $"enemy 0x{msg.TargetEnemy:X}"} at {msg.Speed:F1}y/s")}.");
        // Only forced follows are ever sent (see RemoteMotion.ForceFollow).
        me.ForceFollow(target, NetGuard.Clamp(msg.Speed, 0f, 20f));
    }

    private void OnRoleKilledReceived(RoleKilledMessage msg)
    {
        if (!PeerInRun) return;
        var cause = NetGuard.Clean(msg.Cause);
        DiagnosticLog.Info($"[Multiplayer] {msg.Role} killed: {cause}");
        if (Plugin.Game.World.Party.Get(msg.Role) is ISimPartyMember member)
            Plugin.Game.Kill(member, cause);
        else
            DiagnosticLog.Debug($"[Multiplayer] RoleKilled for {msg.Role} but that slot isn't an ISimPartyMember locally -- dropping.");
    }

    // The hit's ActionEffect already carried the knockback entry, which this client's own player
    // acts on; the forced move holds a debug bot's walk for it, or pushes for a row the client
    // leaves to the server.
    private void OnKnockbackReceived(KnockbackMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.SourceX, msg.SourceY, msg.SourceZ, out var source)) return;
        OwnMember(msg.Role, "Knockback")?.Knockback(source, msg.Row);
    }

    private void OnEndReceived(EndMessage msg)
    {
        if (IsHost) return;
        DiagnosticLog.Info($"[Multiplayer] Peer received EndMessage (ReturnedToInn={msg.ReturnedToInn}, Reason={msg.Reason ?? "none"}).");
        if (NetGuard.Clean(msg.Reason) is { Length: > 0 } reason) AnnounceRunEnded(reason);
        running = false;
        peerRunStarted = false;
        StopDebugBotReplay();
        replay?.Clear();
        // Leave() assumes a zone was entered: an end that beats our queued entry is acted on by
        // OnPeerStageSet once the entry completes.
        if (peerEntryQueued)
        {
            endAfterPeerEntry = msg.ReturnedToInn;
            DiagnosticLog.Info("[Multiplayer] EndMessage received while our zone entry is queued -- acting on it once it completes.");
            return;
        }
        if (!Plugin.Game.World.Map.IsInInstance) return;
        if (msg.ReturnedToInn) LeavePeerStay();
        else Plugin.Game.Reset();
    }
}
