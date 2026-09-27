using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Peer: reporting our own pose --------------------------------------

    // Backpressure-gated like SampleAndBroadcastSnapshot.
    private Task? pendingSelfPoseSend;

    private void SendSelfPose()
    {
        if (pendingSelfPoseSend is not (null or { IsCompleted: true })) return;
        var player = Plugin.GameInstance.World.Party.Player;
        if (player == null) return;
        pendingSelfPoseSend = relay!.SendAsync(new SelfPoseMessage(MyPeerId, player.Position.X, player.Position.Y, player.Position.Z, player.Rotation));
    }

    // Reads the native StatusManager (ActiveTrackedStatusIds), not ActiveStatusSnapshot: a real
    // button press never populates the latter.
    private HashSet<ushort> lastSentMitigationStatusIds = new();
    private float lastSentShieldFraction;

    private void SendSelfMitigationIfChanged()
    {
        var party = Plugin.GameInstance.World.Party;
        var player = party.Player;
        if (player == null) return;
        var current = TankMitigation.ActiveTrackedStatusIds(player).ToHashSet();
        var shieldFraction = TankShieldTracker.RemainingFraction(party.PlayerRole);
        if (current.SetEquals(lastSentMitigationStatusIds) && MathF.Abs(shieldFraction - lastSentShieldFraction) < 0.001f)
            return;
        lastSentMitigationStatusIds = current;
        lastSentShieldFraction = shieldFraction;
        _ = relay!.SendAsync(new SelfMitigationMessage(MyPeerId, current.ToList(), shieldFraction));
    }

    public void ReportAppliedEnemyStatus(IReadOnlyList<SimEnemy> enemies, ushort statusId, float duration)
    {
        if (IsHost || relay is not { IsConnected: true } || enemies.Count == 0) return;
        var netIds = peerEnemies.Where(kv => enemies.Contains(kv.Value)).Select(kv => kv.Key).ToList();
        if (netIds.Count == 0) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting applied status {statusId} on enemy NetIds [{string.Join(",", netIds)}] to host.");
        _ = relay.SendAsync(new PeerAppliedEnemyStatusMessage(MyPeerId, netIds, statusId, duration));
    }

    public void ReportAppliedRoleStatus(IReadOnlyList<PartyRole> roles, ushort statusId, float duration, float shieldFraction = 0f)
    {
        if (IsHost || relay is not { IsConnected: true } || roles.Count == 0) return;
        DiagnosticLog.Info($"[Multiplayer] Peer: reporting applied status {statusId} on roles [{string.Join(",", roles)}] to host" +
                            (shieldFraction > 0f ? $" (shieldFraction={shieldFraction:F3})." : "."));
        _ = relay.SendAsync(new PeerAppliedRoleStatusMessage(MyPeerId, roles.ToList(), statusId, duration, shieldFraction));
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
        if (Plugin.GameInstance.World.Party.Get(role) is SimNetworkPuppet puppet)
            puppet.ApplyNetworkPose(pose, NetGuard.Rotation(msg.Rotation));
        else
            DiagnosticLog.Debug($"[Multiplayer] SelfPose from {Session.NameOf(msg.PeerId)} ({role}) but that slot isn't a SimNetworkPuppet -- dropping.");
    }

    // ---- Peer: applying a world snapshot ------------------------------------

    private void OnWorldSnapshotReceived(WorldSnapshotMessage snap)
    {
        // Before the zone entry, spawned enemies would be torn down by the load while
        // peerEnemies still tracked them.
        if (!PeerInRun) return;
        var world = Plugin.GameInstance.World;

        var seenEnemyIds = new HashSet<int>();
        foreach (var e in NetGuard.Cap(snap.Enemies, NetGuard.MaxEnemiesPerSnapshot))
        {
            if (!NetGuard.TryPosition(e.X, e.Y, e.Z, out var netPosition))
            {
                DiagnosticLog.Warn($"[Multiplayer] Peer: enemy NetId {e.NetId} sent an out-of-range position -- dropping.");
                continue;
            }
            var placement = new Placement(netPosition, NetGuard.Rotation(e.Rotation));
            if (!SimAssets.Allow(SimAssetKind.BNpcBase, e.BNpcBaseId, $"enemy NetId {e.NetId}")) continue;
            seenEnemyIds.Add(e.NetId);
            var freshlySpawned = !peerEnemies.ContainsKey(e.NetId);
            if (!peerEnemies.TryGetValue(e.NetId, out var enemy))
            {
                // The template is resolved by name from this build's own captures, never from
                // wire bytes; the plain doppel is the fallback either way.
                byte[]? template = null;
                var enableDraw = false;
                if (e.NpcSpawnTemplate is { } templateName && !peerEnemyTemplateFailed.Contains(e.NetId))
                {
                    if (UmadRealPackets.NpcSpawnTemplates.TryGetValue(templateName, out var bytes))
                    {
                        template = bytes;
                        enableDraw = e.PacketSpawnEnableDraw;
                    }
                    else
                        DiagnosticLog.Warn($"[Multiplayer] Peer: enemy NetId {e.NetId} names unknown spawn template '{NetGuard.Clean(templateName)}' -- spawning the plain doppel.");
                }
                // A scenario can override the BNpcBase row's model (UCOB P5's Golden Bahamut), so
                // this has to travel; the allowlist is what stops it naming a foreign one.
                var config = new EnemySpawnConfig(
                    e.BNpcBaseId, e.NameId, e.Level, e.Targetable, Enum.IsDefined(e.EnemyList) ? e.EnemyList : EnemyListMode.Never, e.Visible,
                    placement,
                    ModelCharaId: e.ModelCharaId != 0
                        && SimAssets.Allow(SimAssetKind.ModelChara, e.ModelCharaId, $"enemy NetId {e.NetId} model")
                        ? e.ModelCharaId : 0,
                    NetGuard.Clamp(e.Scale, 0f, 100f), NetGuard.Clamp(e.HitboxRadius, 0f, 100f),
                    e.InitialModeAttributeFlags,
                    NpcSpawnTemplate: template, PacketSpawnEnableDraw: enableDraw);
                DiagnosticLog.Info($"[Multiplayer] Peer: first snapshot of enemy NetId {e.NetId} -- BNpcBase {e.BNpcBaseId}, pos ({e.X:F2},{e.Y:F2},{e.Z:F2}), rot {e.Rotation:F2}, visible {e.Visible}"
                    + $", cast {e.CastActionId}/seq {e.CastSeq}, instant {e.LastInstantCastActionId}/seq {e.LastInstantCastSeq}"
                    + $"{(template != null ? $", template {e.NpcSpawnTemplate}" : "")} -- spawning local doppel.");
                enemy = world.SpawnEnemy(config);
                if (enemy == null)
                {
                    DiagnosticLog.Warn($"[Multiplayer] Peer: SpawnEnemy returned null for NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) -- skipping this enemy.");
                    continue;
                }
                peerEnemies[e.NetId] = enemy;
            }
            // The engine dropped the real-packet spawn: the next snapshot recreates it as a plain doppel.
            if (enemy.PacketSpawnFailed)
            {
                DiagnosticLog.Warn($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) packet spawn failed locally -- falling back to the plain doppel.");
                peerEnemyTemplateFailed.Add(e.NetId);
                enemy.Despawn();
                ForgetPeerEnemy(e.NetId);
                continue;
            }
            // Interpolated in Tick; a hard SetPosition every snapshot stutters.
            enemy.ApplyNetworkPosition(placement.Position, placement.Rotation);
            enemy.SetVisible(e.Visible);
            enemy.SetTargetable(e.Targetable);
            // Nothing below lands on an actor the engine hasn't created yet; leaving the seqs
            // unrecorded makes the next snapshot retry.
            if (enemy.PacketSpawnPending) continue;
            // Only on change: SetModelState rebuilds the model.
            if (!peerEnemyModelState.TryGetValue(e.NetId, out var lastModelState) || lastModelState != e.ModelState)
            {
                peerEnemyModelState[e.NetId] = e.ModelState;
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) ModelState -> 0x{e.ModelState:X2}.");
                enemy.SetModelState(e.ModelState);
            }
            var enemyStatuses = NetGuard.Cap(e.Statuses, NetGuard.MaxStatusesPerEntity);
            if (!peerEnemyStatusInstances.TryGetValue(e.NetId, out var enemyInstances))
                peerEnemyStatusInstances[e.NetId] = enemyInstances = new Dictionary<ushort, int>();
            if (!peerEnemyReconciledStatuses.TryGetValue(e.NetId, out var enemyReconciled))
                peerEnemyReconciledStatuses[e.NetId] = enemyReconciled = new HashSet<(ushort, GameObjectId)>();
            DropRecreatedStatuses(enemy, enemyStatuses, enemyInstances, _ => true, $"enemy NetId {e.NetId}");
            ReconcileStatuses(enemy, enemyStatuses, enemyReconciled);
            if (!peerEnemyLastLoggedStatuses.TryGetValue(e.NetId, out var lastStatuses))
                peerEnemyLastLoggedStatuses[e.NetId] = lastStatuses = new Dictionary<(ushort Id, int Ordinal), ushort>();
            LogStatusChanges($"Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId})",
                enemyStatuses.Select(s => (s.StatusId, s.Stacks, s.RemainingTime)).ToList(), lastStatuses);
            // Replayed through the real SimCast pipeline so the cast bar and omen match. Keyed
            // on CastSeq (see EnemyState); seq 0 is the never-cast default.
            if (e.CastSeq > 0
                && (!peerEnemyLastCastSeq.TryGetValue(e.NetId, out var lastCastSeq) || lastCastSeq != e.CastSeq))
            {
                peerEnemyLastCastSeq[e.NetId] = e.CastSeq;
                // Snap first: Cast() reads Position/Rotation directly, and ApplyNetworkPosition
                // above only set an interpolation target.
                enemy.SetPosition(placement);
                var targetLocation = NetGuard.TryPosition(e.CastTargetX, e.CastTargetY, e.CastTargetZ);
                var targetId = ResolvePeerEnd(world, e.CastTargetEnemyNetId, e.CastTargetRole)?.GameObjectId;
                if (SimAssets.Allow(SimAssetKind.Action, e.CastActionId, $"enemy NetId {e.NetId} cast"))
                    enemy.Cast(e.CastActionId, targetLocation: targetLocation,
                        castSeconds: NetGuard.Clamp(e.CastSeconds, 0f, 600f),
                        omenDelay: NetGuard.Clamp(e.CastOmenDelay, 0f, 60f),
                        omenRotate: NetGuard.Clamp(e.CastOmenRotate, -MathF.Tau, MathF.Tau), targetId: targetId);
            }
            var instantKnown = peerEnemyLastInstantCastSeq.TryGetValue(e.NetId, out var lastInstantSeq);
            // NetIds are never reused, so a recorded seq here means state outlived its enemy.
            if (freshlySpawned && instantKnown)
                DiagnosticLog.Warn($"[Multiplayer] Peer: enemy NetId {e.NetId} arrived with a stale instant-cast seq {lastInstantSeq} already recorded -- its action {e.LastInstantCastActionId} will be dropped.");
            if (e.LastInstantCastSeq > 0 && (!instantKnown || lastInstantSeq != e.LastInstantCastSeq))
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) instant cast -> action {e.LastInstantCastActionId} "
                    + $"(seq {e.LastInstantCastSeq}, native={e.LastInstantCastIsNativeEffect}, raw='{NetGuard.Clean(e.LastInstantCastRawPacket)}').");
                peerEnemyLastInstantCastSeq[e.NetId] = e.LastInstantCastSeq;
                enemy.SetPosition(placement); // same snap as above
                var instantTargetLocation = NetGuard.TryPosition(e.LastInstantCastTargetX, e.LastInstantCastTargetY, e.LastInstantCastTargetZ);
                var instantTargetId = ResolvePeerEnd(world, e.LastInstantCastTargetEnemyNetId, e.LastInstantCastTargetRole)?.GameObjectId;
                var instantLock = NetGuard.Clamp(e.LastInstantCastAnimationLock, 0f, 60f, 0.6f);
                if (SimAssets.Allow(SimAssetKind.Action, e.LastInstantCastActionId, $"enemy NetId {e.NetId} instant cast"))
                {
                    // A raw delivery replays our own copy of the capture, patched onto the local
                    // carrier; a version or actor mismatch falls through to the native effect.
                    var rawName = NetGuard.Clean(e.LastInstantCastRawPacket);
                    var rawDelivered = rawName.Length > 0
                        && UmadRealPackets.RawActionEffects.TryGetValue(rawName, out var capture)
                        && RawActionEffect.TryInject(world, enemy, capture.Body, capture.Opcode, capture.GameVersion,
                            $"{rawName} replay, enemy NetId {e.NetId}");
                    if (rawDelivered)
                        DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} delivered {rawName} as a raw packet.");
                    else if (e.LastInstantCastIsNativeEffect)
                    {
                        // Field for field, as the host fired it: a Cast() would re-face the caster
                        // at the target position (world zero for Flood's waves) and use its own lock.
                        var instantActionTargetId = ResolvePeerEnd(world, e.LastInstantCastActionTargetEnemyNetId, e.LastInstantCastActionTargetRole)?.GameObjectId;
                        enemy.NativeActionEffect(e.LastInstantCastActionId, instantLock, (ushort)e.LastInstantCastActionId, 0, ActionType.Action, 0,
                            position: instantTargetLocation, animationTargetId: instantTargetId, actionTargetId: instantActionTargetId);
                    }
                    else
                        enemy.Cast(e.LastInstantCastActionId, targetLocation: instantTargetLocation, castSeconds: 0f, targetId: instantTargetId, animationLock: instantLock);
                }
            }
            ApplyNewVfx(enemy, e.NewVfx, $"enemy NetId {e.NetId}");
            ReconcilePersistentVfx(enemy, e.PersistentVfx, $"enemy NetId {e.NetId}");
            ApplyEngineState(enemy, e.NetId, e.Engine);
            // Keyed on the seq, not the id: a reused enemy replays the same timeline id.
            if (e.AnimationTimelineId is { } timelineId
                && (!peerEnemyAnimationTimeline.TryGetValue(e.NetId, out var lastSeq) || lastSeq != e.AnimationTimelineSeq))
            {
                peerEnemyAnimationTimeline[e.NetId] = e.AnimationTimelineSeq;
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) AnimationTimelineId -> 0x{timelineId:X4} (seq {e.AnimationTimelineSeq}).");
                if (SimAssets.Allow(SimAssetKind.Timeline, timelineId, $"enemy NetId {e.NetId} timeline"))
                    enemy.PlayAnimationTimeline(timelineId);
            }
            if (e.NewLockonVfxIds.Count > 0)
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) NewLockonVfxIds -> [{string.Join(",", e.NewLockonVfxIds)}].");
                foreach (var lockonId in NetGuard.Cap(e.NewLockonVfxIds, NetGuard.MaxLockonVfxPerEntity))
                    if (SimAssets.Allow(SimAssetKind.Lockon, lockonId, $"enemy NetId {e.NetId} lockon"))
                        enemy.AttachLockonVfx(lockonId, persistent: false);
            }
            if (e.AnimationStateArg2 is { } arg2 && e.AnimationStateArg3 is { } arg3
                && (!peerEnemyAnimationState.TryGetValue(e.NetId, out var lastStateSeq) || lastStateSeq != e.AnimationStateSeq))
            {
                peerEnemyAnimationState[e.NetId] = e.AnimationStateSeq;
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {e.NetId} (BNpcBase {e.BNpcBaseId}) AnimationState -> ({arg2},{arg3}) (seq {e.AnimationStateSeq}).");
                if (arg2 is >= 0 and <= NetGuard.MaxAnimationStateArg && arg3 is >= 0 and <= NetGuard.MaxAnimationStateArg)
                    enemy.SetAnimationState(arg2, arg3);
            }
        }
        foreach (var staleId in peerEnemies.Keys.Where(id => !seenEnemyIds.Contains(id)).ToList())
        {
            DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {staleId} no longer in snapshot -- despawning local doppel.");
            peerEnemies[staleId].Despawn();
            ForgetPeerEnemy(staleId);
            peerEnemyTemplateFailed.Remove(staleId);
        }

        world.Obstacles.Clear();
        foreach (var o in NetGuard.Cap(snap.Obstacles, NetGuard.MaxObstaclesPerSnapshot))
        {
            if (!NetGuard.TryPosition(o.X, 0f, o.Z, out var center)) continue;
            world.Obstacles.Add(new CircleObstacle(new Vector2(center.X, center.Z), NetGuard.Clamp(o.Radius, 0f, 200f)));
        }

        if (debugShadowStateGeneric != null && TryResolveScenario() is IMultiplayerReplayable replayable)
            replayable.RefreshLiveHandles(debugShadowStateGeneric, peerEnemies);

        var seenTetherIds = new HashSet<int>();
        foreach (var t in NetGuard.Cap(snap.Tethers, NetGuard.MaxTethersPerSnapshot))
        {
            if (!SimAssets.Allow(SimAssetKind.Tether, t.TetherId, $"tether NetId {t.NetId}")) continue;
            seenTetherIds.Add(t.NetId);
            var a = ResolvePeerEnd(world, t.AEnemyNetId, t.ARole);
            var b = ResolvePeerEnd(world, t.BEnemyNetId, t.BRole);
            if (a == null && b == null) continue;
            // SimTether's endpoints are fixed at construction, so an endpoint change re-creates it.
            var aDesc = t.AEnemyNetId is { } aId ? $"enemy#{aId}" : t.ARole?.ToString() ?? "null";
            var bDesc = t.BEnemyNetId is { } bId ? $"enemy#{bId}" : t.BRole?.ToString() ?? "null";
            if (peerTethers.TryGetValue(t.NetId, out var existing))
            {
                if (ReferenceEquals(existing.A, a) && ReferenceEquals(existing.B, b)) continue;
                DiagnosticLog.Info($"[Multiplayer] Peer: tether NetId {t.NetId} endpoint changed -- recreating (A={aDesc}, B={bDesc}).");
                existing.Despawn();
            }
            else
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: first snapshot of tether NetId {t.NetId} (TetherId {t.TetherId}) -- A={aDesc}, B={bDesc}.");
            }
            peerTethers[t.NetId] = world.Tether(a, b, t.TetherId);
        }
        foreach (var staleId in peerTethers.Keys.Where(id => !seenTetherIds.Contains(id)).ToList())
        {
            DiagnosticLog.Info($"[Multiplayer] Peer: tether NetId {staleId} no longer in snapshot -- despawning.");
            peerTethers[staleId].Despawn();
            peerTethers.Remove(staleId);
        }

        var seenEventObjectIds = new HashSet<int>();
        foreach (var o in NetGuard.Cap(snap.EventObjects, NetGuard.MaxEventObjectsPerSnapshot))
        {
            if (!NetGuard.TryPosition(o.X, o.Y, o.Z, out var eoPosition))
            {
                DiagnosticLog.Warn($"[Multiplayer] Peer: event object NetId {o.NetId} sent an out-of-range position -- dropping.");
                continue;
            }
            var eoPlacement = new Placement(eoPosition, NetGuard.Rotation(o.Rotation));
            if (!SimAssets.Allow(SimAssetKind.EObj, o.EObjId, $"event object NetId {o.NetId}")) continue;
            if (o.LayoutId != 0 && !SimAssets.Allow(SimAssetKind.Layout, o.LayoutId, $"event object NetId {o.NetId} layout")) continue;
            if (o.EventId != 0 && !SimAssets.Allow(SimAssetKind.EventId, o.EventId, $"event object NetId {o.NetId} EventId")) continue;
            seenEventObjectIds.Add(o.NetId);
            if (!peerEventObjects.TryGetValue(o.NetId, out var eo))
            {
                var config = new EventObjectSpawnConfig
                {
                    EObjId = o.EObjId,
                    Placement = eoPlacement,
                    TimelineState = o.TimelineState,
                    SpawnVisible = true,
                    LayoutId = o.LayoutId,
                    EventId = o.EventId,
                    EntityId = o.EntityId,
                    TargetableStatus = o.TargetableStatus,
                    Arg2 = o.Arg2,
                    MuteSound = o.MuteSound,
                    ForceSharedGroupActive = o.ForceSharedGroupActive,
                    HideAtState = o.HideAtState,
                };
                DiagnosticLog.Info($"[Multiplayer] Peer: first snapshot of event object NetId {o.NetId} -- EObj 0x{o.EObjId:X}, pos ({o.X:F2},{o.Y:F2},{o.Z:F2}), state {o.CurrentState} -- spawning local copy.");
                eo = world.SpawnEventObject(config);
                if (eo == null)
                {
                    DiagnosticLog.Warn($"[Multiplayer] Peer: SpawnEventObject returned null for NetId {o.NetId} (EObj 0x{o.EObjId:X}) -- skipping.");
                    continue;
                }
                peerEventObjects[o.NetId] = eo;
                // CurrentState is only meaningful once something has written it; seeding stops
                // the reconcile below stamping a never-written 0 over the spawn's TimelineState.
                peerEventObjectState[o.NetId] = o.CurrentState;
                if (o.CurrentState != 0) eo.SetState(o.CurrentState);
            }
            eo.SetPosition(eoPlacement);
            // A beat writes the state itself, so the plain SetState below stays quiet for it.
            if (o.AnimationState is { } animState && o.AnimationBitmask is { } animBitmask
                && (!peerEventObjectAnimationSeq.TryGetValue(o.NetId, out var lastAnimSeq) || lastAnimSeq != o.AnimationSeq))
            {
                peerEventObjectAnimationSeq[o.NetId] = o.AnimationSeq;
                peerEventObjectState[o.NetId] = o.CurrentState;
                var beatMode = Enum.IsDefined(o.AnimationMode) ? o.AnimationMode : PropBeatMode.ActorControl;
                DiagnosticLog.Info($"[Multiplayer] Peer: event object NetId {o.NetId} (EObj 0x{o.EObjId:X}) beat (0x{animState:X},0x{animBitmask:X}) via {beatMode} (seq {o.AnimationSeq}).");
                if (animState <= ushort.MaxValue) eo.PlayBeat(animState, animBitmask, beatMode);
            }
            else if (!peerEventObjectState.TryGetValue(o.NetId, out var lastState) || lastState != o.CurrentState)
            {
                peerEventObjectState[o.NetId] = o.CurrentState;
                DiagnosticLog.Info($"[Multiplayer] Peer: event object NetId {o.NetId} (EObj 0x{o.EObjId:X}) CurrentState -> {o.CurrentState}.");
                eo.SetState(o.CurrentState);
            }
            if (o.FadeOutSeq > 0 && peerEventObjectFadeSeq.GetValueOrDefault(o.NetId) != o.FadeOutSeq)
            {
                peerEventObjectFadeSeq[o.NetId] = o.FadeOutSeq;
                DiagnosticLog.Info($"[Multiplayer] Peer: event object NetId {o.NetId} (EObj 0x{o.EObjId:X}) fading out (seq {o.FadeOutSeq}).");
                eo.FadeOut();
            }
            if (o.DirectorModSeq > 0 && o.DirectorState <= ushort.MaxValue
                && peerEventObjectDirectorSeq.GetValueOrDefault(o.NetId) != o.DirectorModSeq)
            {
                peerEventObjectDirectorSeq[o.NetId] = o.DirectorModSeq;
                DiagnosticLog.Info($"[Multiplayer] Peer: event object NetId {o.NetId} (EObj 0x{o.EObjId:X}) director state {o.DirectorState} (seq {o.DirectorModSeq}).");
                eo.DirectorEObjMod(o.DirectorState);
            }
        }
        foreach (var staleId in peerEventObjects.Keys.Where(id => !seenEventObjectIds.Contains(id)).ToList())
        {
            DiagnosticLog.Info($"[Multiplayer] Peer: event object NetId {staleId} no longer in snapshot -- despawning local copy.");
            peerEventObjects[staleId].Despawn();
            peerEventObjects.Remove(staleId);
            peerEventObjectState.Remove(staleId);
            peerEventObjectAnimationSeq.Remove(staleId);
            peerEventObjectFadeSeq.Remove(staleId);
            peerEventObjectDirectorSeq.Remove(staleId);
        }
    }

    // A status the host dropped and re-added arrives as a new instance of the same id. Refreshed
    // in place it would skip the engine's gain path, which is what applies a param-driven look
    // (Kefka's trance aura), so it is dropped here for the reconcile to add back. Ids the host
    // holds more than once are left to the plain reconcile.
    // A status's identity is (id, source), not id. The host's source ids mean nothing here, so a
    // duplicated id gets a local source per host instance; a unique one keeps the default source,
    // which is also what matches slots the engine wrote.
    private const ulong DuplicateStatusSourceBase = 0xE100_0000;

    private static GameObjectId StatusSource(IReadOnlyList<EnemyStatusState> targets, EnemyStatusState status,
        HashSet<(ushort Id, GameObjectId Source)> tracked)
    {
        var perInstance = (GameObjectId)(DuplicateStatusSourceBase | ((uint)status.Instance & 0xFFFFFF));
        if (targets.Count(s => s.StatusId == status.StatusId) > 1) return perInstance;
        // Sticky: switching the survivor of an expiring pair back would re-add it and flicker.
        return tracked.Contains((status.StatusId, perInstance)) ? perInstance : default;
    }

    // Only what `tracked` holds is removed, so a peer's own statuses are left alone.
    private static void ReconcileStatuses(SimCharacter character, IReadOnlyList<EnemyStatusState> targets,
        HashSet<(ushort Id, GameObjectId Source)> tracked)
    {
        var wanted = new HashSet<(ushort Id, GameObjectId Source)>();
        foreach (var target in targets)
        {
            var source = StatusSource(targets, target, tracked);
            wanted.Add((target.StatusId, source));
            if (character.FindStatus(target.StatusId, source) is { } held && held.Stacks == target.Stacks) continue;
            character.AddStatus(target.StatusId, duration: NetGuard.Clamp(target.RemainingTime, -1f, 3600f),
                stacks: target.Stacks, overrideStacks: true, sourceObject: source);
        }
        foreach (var stale in tracked.Where(s => !wanted.Contains(s)).ToList())
            character.RemoveStatus(stale.Id, stale.Source);
        tracked.Clear();
        tracked.UnionWith(wanted);
    }

    private static void DropRecreatedStatuses(SimCharacter character, IReadOnlyList<EnemyStatusState> targets,
        Dictionary<ushort, int> instances, Func<ushort, bool> mayDrop, string who)
    {
        foreach (var target in targets)
        {
            if (targets.Count(s => s.StatusId == target.StatusId) != 1)
            {
                instances.Remove(target.StatusId);
                continue;
            }
            if (instances.TryGetValue(target.StatusId, out var seen) && seen != target.Instance
                && mayDrop(target.StatusId) && character.HasStatus(target.StatusId))
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: {who} status {target.StatusId} was re-added by the host -- re-adding it here.");
                character.RemoveStatus(target.StatusId);
            }
            instances[target.StatusId] = target.Instance;
        }
        foreach (var id in instances.Keys.Where(id => targets.All(s => s.StatusId != id)).ToList())
            instances.Remove(id);
    }

    private void ForgetPeerEnemy(int netId)
    {
        peerEnemies.Remove(netId);
        peerEnemyStatusInstances.Remove(netId);
        peerEnemyReconciledStatuses.Remove(netId);
        peerEnemyModelState.Remove(netId);
        peerEnemyLastLoggedStatuses.Remove(netId);
        peerEnemyAnimationTimeline.Remove(netId);
        peerEnemyAnimationState.Remove(netId);
        peerEnemyLastInstantCastSeq.Remove(netId);
        peerEnemyLastCastSeq.Remove(netId);
        peerEnemyEngineSeqs.Remove(netId);
        peerEnemyModelHidden.Remove(netId);
    }

    // The engine-level state behind a VFX-only cue: the carrier's timeline hold, its AnimLock,
    // and the diagnostic delivery modes. Each applies on its own seq change.
    private void ApplyEngineState(SimEnemy enemy, int netId, ActorEngineState? engine)
    {
        if (engine is null) return;
        if (!peerEnemyModelHidden.TryGetValue(netId, out var hidden) || hidden != engine.ModelHidden)
        {
            peerEnemyModelHidden[netId] = engine.ModelHidden;
            DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {netId} ModelHidden -> {engine.ModelHidden}.");
            enemy.SetModelHidden(engine.ModelHidden);
        }
        var applied = peerEnemyEngineSeqs.GetValueOrDefault(netId);
        if (engine.ModeSeq > 0 && engine.ModeSeq != applied.Mode)
        {
            applied.Mode = engine.ModeSeq;
            if (Enum.IsDefined((CharacterModes)engine.Mode))
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {netId} Mode -> {(CharacterModes)engine.Mode}/{engine.ModeParam} (seq {engine.ModeSeq}).");
                enemy.SetMode((CharacterModes)engine.Mode, engine.ModeParam);
            }
            else
                DiagnosticLog.Warn($"[Multiplayer] Peer: enemy NetId {netId} sent mode {engine.Mode}, which is no CharacterModes value -- dropping.");
        }
        if (engine.HoldSeq > 0 && engine.HoldSeq != applied.Hold)
        {
            applied.Hold = engine.HoldSeq;
            if (Enum.IsDefined(engine.HoldKind)
                && (engine.HoldKind == TimelineHoldKind.None || SimAssets.Allow(SimAssetKind.Timeline, engine.HoldTimelineId, $"enemy NetId {netId} timeline hold")))
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {netId} timeline hold {engine.HoldKind} {engine.HoldTimelineId} (seq {engine.HoldSeq}).");
                switch (engine.HoldKind)
                {
                    case TimelineHoldKind.Loop: enemy.HoldTimelineLoop(engine.HoldTimelineId); break;
                    case TimelineHoldKind.Base: enemy.HoldTimelineBase(engine.HoldTimelineId); break;
                    default: enemy.ReleaseTimelineHold(engine.HoldTimelineId); break;
                }
            }
        }
        if (engine.DirectTimelineSeq > 0 && engine.DirectTimelineSeq != applied.Direct)
        {
            applied.Direct = engine.DirectTimelineSeq;
            if (SimAssets.Allow(SimAssetKind.Timeline, engine.DirectTimelineId, $"enemy NetId {netId} direct timeline"))
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: enemy NetId {netId} PlayTimelineDirect({engine.DirectTimelineId}) (seq {engine.DirectTimelineSeq}).");
                enemy.PlayTimelineDirect(engine.DirectTimelineId);
            }
        }
        if (engine.ForceLoadTimelineSeq > 0 && engine.ForceLoadTimelineSeq != applied.ForceLoad)
        {
            applied.ForceLoad = engine.ForceLoadTimelineSeq;
            enemy.ForceLoadBaseTimeline();
        }
        peerEnemyEngineSeqs[netId] = applied;
    }

    // Reconciled like the statuses: the host's set is the truth, and a path that drops out of it
    // is removed locally. Only VFX this client added persistently are ever in its own set.
    private static void ReconcilePersistentVfx(SimCharacter target, IReadOnlyList<string>? paths, string who)
    {
        var wanted = NetGuard.Cap(paths, NetGuard.MaxVfxPerEntity);
        var current = target.ActivePersistentVfxPaths;
        if (wanted.Count == 0 && current.Count == 0) return;
        foreach (var raw in wanted)
        {
            var path = NetGuard.Clean(raw);
            if (current.Contains(path)) continue;
            if (!SimAssets.AllowOmenPath(path, $"{who} persistent vfx")) continue;
            DiagnosticLog.Info($"[Multiplayer] Peer: {who} attaching persistent vfx '{path}'.");
            target.AddVfx(path, persistent: true);
        }
        foreach (var path in current)
        {
            if (wanted.Any(p => NetGuard.Clean(p) == path)) continue;
            DiagnosticLog.Info($"[Multiplayer] Peer: {who} removing persistent vfx '{path}'.");
            target.RemoveVfx(path);
        }
    }

    private static void ApplyNewVfx(SimCharacter target, IReadOnlyList<AttachedVfxState>? newVfx, string who)
    {
        foreach (var v in NetGuard.Cap(newVfx, NetGuard.MaxVfxPerEntity))
        {
            var path = NetGuard.Clean(v.Path);
            if (!SimAssets.AllowOmenPath(path, $"{who} vfx")) continue;
            DiagnosticLog.Info($"[Multiplayer] Peer: {who} attaching vfx '{path}'.");
            target.AddVfx(path, NetGuard.Clamp(v.DurationSeconds, 0f, 600f), persistent: false);
        }
    }

    private unsafe void OnRolesSnapshotReceived(RolesSnapshotMessage snap)
    {
        if (!PeerInRun) return;
        var world = Plugin.GameInstance.World;

        var myRole = MyClaimedRole;
        foreach (var r in NetGuard.Cap(snap.Roles, Enum.GetValues<PartyRole>().Length))
        {
            // Position is self-authoritative for our own role; statuses, lockons and HP are not.
            if (r.Role != myRole && world.Party.Get(r.Role) is SimNetworkPuppet puppet
                && NetGuard.TryPosition(r.X, r.Y, r.Z, out var rolePosition))
                puppet.ApplyNetworkPose(rolePosition, NetGuard.Rotation(r.Rotation));

            if (world.Party.Get(r.Role) is not { } member) continue;

            // Our own character goes through SimPlayer so the real MaxHealth is restored on Despawn.
            var maxHp = Math.Min(r.MaxHp, NetGuard.MaxHp);
            var currentHp = Math.Min(r.CurrentHp, maxHp);
            var bc = member.BattleCharaPtr;
            if (maxHp > 0)
            {
                if (member is SimPlayer me) me.ApplyNetworkHp(currentHp, maxHp);
                else if (bc != null)
                {
                    bc->MaxHealth = maxHp;
                    bc->Health = currentHp;
                }
            }
            var roleStatuses = NetGuard.Cap(r.Statuses, NetGuard.MaxStatusesPerEntity);
            if (!peerRoleReconciledStatuses.TryGetValue(r.Role, out var reconciled))
                peerRoleReconciledStatuses[r.Role] = reconciled = new HashSet<(ushort, GameObjectId)>();
            if (!peerRoleStatusInstances.TryGetValue(r.Role, out var roleInstances))
                peerRoleStatusInstances[r.Role] = roleInstances = new Dictionary<ushort, int>();
            DropRecreatedStatuses(member, roleStatuses, roleInstances, id => reconciled.Any(s => s.Id == id), $"role {r.Role}");
            ReconcileStatuses(member, roleStatuses, reconciled);
            if (!peerRoleLastLoggedStatuses.TryGetValue(r.Role, out var lastStatuses))
                peerRoleLastLoggedStatuses[r.Role] = lastStatuses = new Dictionary<(ushort Id, int Ordinal), ushort>();
            LogStatusChanges($"Peer: role {r.Role} ({DescribeRoleOwner(r.Role, member)})",
                roleStatuses.Select(s => (s.StatusId, s.Stacks, s.RemainingTime)).ToList(), lastStatuses);
            if (r.NewLockonVfxIds.Count > 0)
            {
                DiagnosticLog.Info($"[Multiplayer] Peer: role {r.Role} NewLockonVfxIds -> [{string.Join(",", r.NewLockonVfxIds)}].");
                foreach (var lockonId in NetGuard.Cap(r.NewLockonVfxIds, NetGuard.MaxLockonVfxPerEntity))
                    if (SimAssets.Allow(SimAssetKind.Lockon, lockonId, $"role {r.Role} lockon"))
                        member.AttachLockonVfx(lockonId, persistent: false);
            }
            ApplyNewVfx(member, r.NewVfx, $"role {r.Role}");
            ReconcilePersistentVfx(member, r.PersistentVfx, $"role {r.Role}");
            // A doppel's own action animation (a bot tank's limit break). Our own seat is a
            // SimPlayer, whose actions are its owner's real button presses.
            if (r.PlayedActionSeq > 0 && member is SimNpc actor
                && (!peerRolePlayedActionSeq.TryGetValue(r.Role, out var lastPlayed) || lastPlayed != r.PlayedActionSeq))
            {
                peerRolePlayedActionSeq[r.Role] = r.PlayedActionSeq;
                if (SimAssets.Allow(SimAssetKind.Action, r.PlayedActionId, $"role {r.Role} action"))
                {
                    DiagnosticLog.Info($"[Multiplayer] Peer: role {r.Role} plays action {r.PlayedActionId} (seq {r.PlayedActionSeq}).");
                    actor.PlayAction(r.PlayedActionId, NetGuard.Clamp(r.PlayedActionAnimationLock, 0f, 60f, 0.6f));
                }
            }
            // The KO pose comes with RoleKilledMessage; id 0 is a reset.
            if (!r.Dead && r.AnimationTimelineId is { } roleTimeline
                && (!peerRoleAnimationTimelineSeq.TryGetValue(r.Role, out var lastRoleSeq) || lastRoleSeq != r.AnimationTimelineSeq))
            {
                peerRoleAnimationTimelineSeq[r.Role] = r.AnimationTimelineSeq;
                DiagnosticLog.Info($"[Multiplayer] Peer: role {r.Role} AnimationTimelineId -> {roleTimeline} (loop {r.AnimationTimelineLoopId}, seq {r.AnimationTimelineSeq}).");
                if (roleTimeline == 0)
                    member.ResetActionTimeline();
                else if (SimAssets.Allow(SimAssetKind.Timeline, roleTimeline, $"role {r.Role} timeline")
                    && (r.AnimationTimelineLoopId == 0 || SimAssets.Allow(SimAssetKind.Timeline, r.AnimationTimelineLoopId, $"role {r.Role} loop timeline")))
                    member.PlayActionTimeline(roleTimeline, r.AnimationTimelineLoopId);
            }
        }
    }

    // Forced moves land on our own character only: every other slot is a puppet the host's
    // pose snapshots already place.
    private ISimPartyMember? OwnMember(PartyRole role, string what)
    {
        if (role != MyClaimedRole) return null;
        if (Plugin.GameInstance.World.Party.Get(role) is ISimPartyMember member) return member;
        DiagnosticLog.Debug($"[Multiplayer] {what} for {role} but that slot isn't an ISimPartyMember locally -- dropping.");
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
        var mode = NetGuard.InRange(msg.Mode, 3) ? (Core.Native.CarryMode)msg.Mode : Core.Native.CarryMode.Native;
        OwnMember(msg.Role, "Carry")?.CarryTo(destination, mode);
    }

    private void OnPushReceived(PushMessage msg)
    {
        if (!PeerInRun) return;
        if (OwnMember(msg.Role, "Push") is not { } member) return;
        var heading = NetGuard.Rotation(msg.Heading);
        var distance = NetGuard.Clamp(msg.Distance, 0f, 200f);
        if (msg.DurationSeconds > 0f)
            member.PushInDirectionEased(heading, distance, NetGuard.Clamp(msg.DurationSeconds, 0.01f, 60f));
        else
            member.PushInDirection(heading, distance, NetGuard.Clamp(msg.Speed, 0f, 500f));
    }

    private void OnFollowReceived(FollowMessage msg)
    {
        if (!PeerInRun) return;
        if (OwnMember(msg.Role, "Follow") is not SimCharacter me) return;
        var target = ResolvePeerEnd(Plugin.GameInstance.World, msg.TargetEnemyNetId, msg.TargetRole);
        DiagnosticLog.Info($"[Multiplayer] Peer: {msg.Role} {(target == null ? "released from follow" : $"following {msg.TargetRole?.ToString() ?? $"enemy#{msg.TargetEnemyNetId}"} at {msg.Speed:F1}y/s{(msg.Forced ? " (forced)" : "")}")}.");
        me.Follow(target, NetGuard.Clamp(msg.Speed, 0f, 20f), msg.Forced);
    }

    // The bot choreography cases below are sent for every role; PlayerMovement drops them unless
    // this client is bot-controlled.
    private void OnMoveReceived(MoveMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var target)) return;
        var finalRotation = msg.FinalRotation is { } rot ? NetGuard.Rotation(rot) : (float?)null;
        (OwnMember(msg.Role, "Move") as SimCharacter)?.MoveTo(target, NetGuard.Clamp(msg.Speed, 0f, 20f), finalRotation);
    }

    private void OnInterceptReceived(InterceptMessage msg)
    {
        if (!PeerInRun) return;
        if (OwnMember(msg.Role, "Intercept") is not SimCharacter me) return;
        if (!peerTethers.TryGetValue(msg.TetherNetId, out var tether))
        {
            DiagnosticLog.Debug($"[Multiplayer] Peer: Intercept for {msg.Role} names tether NetId {msg.TetherNetId}, not replicated yet -- dropping.");
            return;
        }
        me.Intercept(tether, NetGuard.Clamp(msg.Margin, 0f, 50f));
    }

    private void OnFaceReceived(FaceMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var target)) return;
        (OwnMember(msg.Role, "Face") as SimCharacter)?.Face(target);
    }

    private SimCharacter? ResolvePeerEnd(SimWorld world, int? enemyNetId, PartyRole? role)
    {
        if (enemyNetId is { } id) return peerEnemies.GetValueOrDefault(id);
        if (role is { } r) return world.Party.Get(r);
        return null;
    }

    private void OnRoleKilledReceived(RoleKilledMessage msg)
    {
        if (!PeerInRun) return;
        var cause = NetGuard.Clean(msg.Cause);
        DiagnosticLog.Info($"[Multiplayer] {msg.Role} killed: {cause}");
        if (Plugin.GameInstance.World.Party.Get(msg.Role) is ISimPartyMember member)
            Plugin.GameInstance.Kill(member, cause);
        else
            DiagnosticLog.Debug($"[Multiplayer] RoleKilled for {msg.Role} but that slot isn't an ISimPartyMember locally -- dropping.");
    }

    private void OnKnockbackReceived(KnockbackMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.SourceX, msg.SourceY, msg.SourceZ, out var source)) return;
        if (Plugin.GameInstance.World.Party.Get(msg.Role) is ISimPartyMember member)
            member.Knockback(source, NetGuard.Clamp(msg.Distance, 0f, 200f), NetGuard.Clamp(msg.Speed, 0f, 500f));
        else
            DiagnosticLog.Debug($"[Multiplayer] Knockback for {msg.Role} but that slot isn't an ISimPartyMember locally -- dropping.");
    }

    private void OnSpawnOmenReceived(SpawnOmenMessage msg)
    {
        if (!PeerInRun) return;
        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var position)) return;
        if (!Plugin.GameInstance.World.CanSpawnOmen)
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: dropping SpawnOmen -- already at {NetGuard.MaxLiveOmens} live omens.");
            return;
        }
        var omenPath = NetGuard.Clean(msg.Path);
        if (!SimAssets.AllowOmenPath(omenPath, "SpawnOmen")) return;
        Plugin.GameInstance.World.SpawnOmen(
            omenPath, new Placement(position, NetGuard.Rotation(msg.Rotation)),
            new Vector3(NetGuard.Clamp(msg.ScaleX, 0f, 1000f), NetGuard.Clamp(msg.ScaleY, 0f, 1000f), NetGuard.Clamp(msg.ScaleZ, 0f, 1000f)),
            NetGuard.Clamp(msg.DurationSeconds, 0f, 600f));
    }

    private void OnEndReceived(EndMessage msg)
    {
        if (IsHost) return;
        DiagnosticLog.Info($"[Multiplayer] Peer received EndMessage (ReturnedToInn={msg.ReturnedToInn}, Reason={msg.Reason ?? "none"}).");
        if (NetGuard.Clean(msg.Reason) is { Length: > 0 } reason) AnnounceRunEnded(reason);
        running = false;
        StopDebugBotReplay();
        // Leave() assumes a zone was entered: an end that beats our queued entry is acted on by
        // OnPeerStartResolved once the entry completes.
        if (peerEntryQueued)
        {
            endAfterPeerEntry = msg.ReturnedToInn;
            DiagnosticLog.Info("[Multiplayer] EndMessage received while our zone entry is queued -- acting on it once it completes.");
            return;
        }
        if (!Plugin.GameInstance.World.Map.IsInInstance) return;
        if (msg.ReturnedToInn)
            Plugin.GameInstance.Leave();
        else
            Plugin.GameInstance.Reset();
    }

}
