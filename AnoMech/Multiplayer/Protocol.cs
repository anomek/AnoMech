using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Multiplayer;

// Wire format; the relay is a per-session broadcaster that knows nothing of it. Positions are
// flattened to floats: System.Text.Json doesn't serialize Vector3's fields without a converter.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(LobbyStateMessage), "lobby")]
[JsonDerivedType(typeof(ClaimRoleMessage), "claim")]
[JsonDerivedType(typeof(ReleaseRoleMessage), "release")]
[JsonDerivedType(typeof(StartMessage), "start")]
[JsonDerivedType(typeof(StartCheckMessage), "startCheck")]
[JsonDerivedType(typeof(StartCheckResponseMessage), "startCheckResponse")]
[JsonDerivedType(typeof(StartAbortMessage), "startAbort")]
[JsonDerivedType(typeof(SelfPoseMessage), "pose")]
[JsonDerivedType(typeof(WorldSnapshotMessage), "snapshot")]
[JsonDerivedType(typeof(RolesSnapshotMessage), "rolesSnapshot")]
[JsonDerivedType(typeof(RoleKilledMessage), "killed")]
[JsonDerivedType(typeof(KnockbackMessage), "knockback")]
[JsonDerivedType(typeof(TeleportMessage), "teleport")]
[JsonDerivedType(typeof(PushMessage), "push")]
[JsonDerivedType(typeof(CarryMessage), "carry")]
[JsonDerivedType(typeof(FollowMessage), "follow")]
[JsonDerivedType(typeof(MoveMessage), "move")]
[JsonDerivedType(typeof(InterceptMessage), "intercept")]
[JsonDerivedType(typeof(FaceMessage), "face")]
[JsonDerivedType(typeof(SpawnOmenMessage), "spawnOmen")]
[JsonDerivedType(typeof(EndMessage), "end")]
[JsonDerivedType(typeof(PingMessage), "ping")]
[JsonDerivedType(typeof(PongMessage), "pong")]
[JsonDerivedType(typeof(PeerStatusMessage), "status")]
[JsonDerivedType(typeof(SessionEndedMessage), "sessionEnded")]
[JsonDerivedType(typeof(ResetRequestMessage), "resetRequest")]
[JsonDerivedType(typeof(LeaveRequestMessage), "leaveRequest")]
[JsonDerivedType(typeof(MapEffectMessage), "mapEffect")]
[JsonDerivedType(typeof(MapDirectorUpdateMessage), "mapDirectorUpdate")]
[JsonDerivedType(typeof(SetWeatherMessage), "setWeather")]
[JsonDerivedType(typeof(SetFogHoldMessage), "setFogHold")]
[JsonDerivedType(typeof(AnnouncementMessage), "announcement")]
[JsonDerivedType(typeof(SelfMitigationMessage), "selfMitigation")]
[JsonDerivedType(typeof(PeerAppliedEnemyStatusMessage), "peerAppliedEnemyStatus")]
[JsonDerivedType(typeof(PeerAppliedRoleStatusMessage), "peerAppliedRoleStatus")]
[JsonDerivedType(typeof(KickMessage), "kick")]
public abstract record MpMessage;

// Only the host legitimately sends these; DispatchCore drops one the relay says came from
// elsewhere. A relay without "senderIdentity" can't attest, and the tag then defaults to
// trusted.
internal interface IHostOnlyMessage;

// Peer -> host on connect. Version/Checksum catch a build mismatch before it desyncs.
public sealed record HelloMessage(Guid PeerId, string DisplayName, string Version, string Checksum, byte ClassJob = 0) : MpMessage;

public sealed record PeerBuildInfo(string Version, string Checksum)
{
    public string ShortChecksum => Checksum.Length >= 6 ? Checksum[..6] : Checksum;
}

// Full state, so a client that missed an update self-heals and a late joiner resolves the same
// scenario/strat/waymark and clock. ScenarioSettings is the display summary; ScenarioSettingsJson
// is the same overrides for a peer to actually apply (see ScenarioSettingsSync).
public sealed record LobbyStateMessage(
    Guid HostId,
    Dictionary<PartyRole, Guid> ClaimedBy,
    Dictionary<Guid, string> Names,
    Dictionary<Guid, PeerBuildInfo> Builds,
    Dictionary<Guid, byte> Jobs,
    bool Started,
    int ScenarioIndex,
    int SelectedAi,
    int SelectedWaymark,
    Dictionary<string, ushort> TankBusterPlan,
    List<string>? ScenarioSettings = null,
    string? ScenarioSettingsJson = null,
    RunClockState? Clock = null) : MpMessage, IHostOnlyMessage;

// The host's event clock when the message left. A peer starts its run from it instead of from
// zero, which would leave it behind by the host's load time plus the travel time. FrameSeconds
// is the host's average frame, part of the lead the peer takes on top.
public sealed record RunClockState(float EventClock, float FrameSeconds);

public sealed record ClaimRoleMessage(Guid PeerId, PartyRole Role) : MpMessage;
public sealed record ReleaseRoleMessage(Guid PeerId) : MpMessage;

// The named peer leaves on receipt. Banned: the host also drops everything it sends afterwards,
// a fresh Hello included, until unbanned.
public sealed record KickMessage(Guid PeerId, bool Banned = false) : MpMessage, IHostOnlyMessage;

public sealed record StartMessage(RunClockState? Clock = null) : MpMessage, IHostOnlyMessage;

// Sent before StartMessage; every claimed peer answers, so "can't start" reaches the host
// instead of RunScenarioInternal silently no-op'ing.
public sealed record StartCheckMessage : MpMessage, IHostOnlyMessage;

public sealed record StartCheckResponseMessage(Guid PeerId, bool Ready, string? Reason) : MpMessage;

// The start check is advisory (a host can skip it, and state changes in between), so the
// receiver re-checks on Start; the host then ends the run for everyone and names the reason.
public sealed record StartAbortMessage(Guid PeerId, string Reason) : MpMessage;

// Peer -> host. Applied to that peer's SimNetworkPuppet and republished in the next Roles list.
// BotControlled tells the host nobody presses buttons in that seat (TankMitigation.IsBotDriven).
public sealed record SelfPoseMessage(Guid PeerId, float X, float Y, float Z, float Rotation, bool BotControlled = false) : MpMessage;

// One SimEnemy as the host has it. NetId is a host-assigned per-run id. Cast* fields mirror
// SimCast rather than a sheet, which wouldn't match a scenario's synthetic helper actions. The
// Seq counters are the edge triggers: an instant cast never sets IsCasting. Targets resolve by
// NetId/role since a GameObjectId means nothing across clients.
public sealed record EnemyStatusState(ushort StatusId, ushort Stacks, float RemainingTime, int Instance = 0);

// One fire-and-forget SimCharacter.AddVfx since the last drain; the path is checked against
// SimAssets on receipt. Persistent VFX are not carried: their removal has no replication path.
public sealed record AttachedVfxState(string Path, float DurationSeconds);

// Engine-level actor state a scenario drives directly: the timeline holds and AnimLock a
// VFX-only cue needs, and the two diagnostic delivery modes. Each is edge-triggered on its own
// seq, because the run animation and the cast pipeline write the same native fields every frame.
public sealed record ActorEngineState(
    bool ModelHidden,
    byte Mode, byte ModeParam, int ModeSeq,
    TimelineHoldKind HoldKind, ushort HoldTimelineId, int HoldSeq,
    ushort DirectTimelineId, int DirectTimelineSeq,
    int ForceLoadTimelineSeq);

// NpcSpawnTemplate names a UmadRealPackets capture (resolved by name on receipt): a
// packet-spawned carrier's real, model-less look is what its action VFX attach to.
// LastInstantCastIsNativeEffect: the host fired a bare NativeActionEffect; a peer replays it
// with the same animation target, action target, position and lock instead of a Cast().
// LastInstantCastRawPacket instead names a captured resolve the receiver replays from its own
// copy of the bytes. PersistentVfx is a reconciled set, unlike the one-shot NewVfx.
public sealed record EnemyState(
    int NetId, uint BNpcBaseId, uint NameId, byte Level, bool Targetable,
    EnemyListMode EnemyList, uint ModelCharaId, float Scale, float HitboxRadius,
    byte? InitialModeAttributeFlags, bool Visible, byte ModelState,
    IReadOnlyList<EnemyStatusState> Statuses, ushort? AnimationTimelineId, int AnimationTimelineSeq, IReadOnlyList<uint> NewLockonVfxIds,
    int? AnimationStateArg2, int? AnimationStateArg3, int AnimationStateSeq,
    float X, float Y, float Z, float Rotation,
    bool IsCasting, int CastSeq, uint CastActionId, float CastSeconds, float CastOmenDelay, float CastOmenRotate,
    float? CastTargetX, float? CastTargetY, float? CastTargetZ,
    int? CastTargetEnemyNetId, PartyRole? CastTargetRole,
    int LastInstantCastSeq, uint LastInstantCastActionId,
    float? LastInstantCastTargetX, float? LastInstantCastTargetY, float? LastInstantCastTargetZ,
    int? LastInstantCastTargetEnemyNetId, PartyRole? LastInstantCastTargetRole,
    string? NpcSpawnTemplate = null, bool PacketSpawnEnableDraw = false,
    bool LastInstantCastIsNativeEffect = false, float LastInstantCastAnimationLock = 0.6f,
    int? LastInstantCastActionTargetEnemyNetId = null, PartyRole? LastInstantCastActionTargetRole = null,
    IReadOnlyList<AttachedVfxState>? NewVfx = null,
    string? LastInstantCastRawPacket = null, ActorEngineState? Engine = null,
    IReadOnlyList<string>? PersistentVfx = null);

// Each end resolves to a live enemy (by NetId) or a party role.
public sealed record TetherState(int NetId, ushort TetherId, int? AEnemyNetId, PartyRole? ARole, int? BEnemyNetId, PartyRole? BRole);

// Host-authoritative even for a peer's own role: a peer runs no scenario logic, so statuses,
// lockons and HP (TankMitigation/TankHpRegen are host-only) all come from here. The animation
// timeline is a scripted pose (Umad P1's sleep, a confused member's swing); the KO pose travels
// as RoleKilledMessage, so a dead role's timeline is not replayed. PlayedAction is a doppel's
// own action animation (a bot tank's limit break), edge-triggered on its seq.
public sealed record RoleState(
    PartyRole Role, bool Filled, bool Dead, float X, float Y, float Z, float Rotation,
    IReadOnlyList<EnemyStatusState> Statuses, IReadOnlyList<uint> NewLockonVfxIds,
    uint CurrentHp, uint MaxHp,
    ushort? AnimationTimelineId = null, ushort AnimationTimelineLoopId = 0, int AnimationTimelineSeq = 0,
    IReadOnlyList<AttachedVfxState>? NewVfx = null, IReadOnlyList<string>? PersistentVfx = null,
    uint PlayedActionId = 0, float PlayedActionAnimationLock = 0.6f, int PlayedActionSeq = 0);

// LayoutId picks the SharedGroup the engine attaches; 0 requests the wrong one. EventId binds
// the prop to the instance director as the real spawn packets do. Animation* is the last
// EObjAnimation beat (SimEventObject.PlayBeat) with the delivery the host chose, edge-triggered
// on the seq; FadeOutSeq is the ActorControl 607 counter.
public sealed record EventObjectState(
    int NetId, uint EObjId, ushort TimelineState, ushort CurrentState,
    float X, float Y, float Z, float Rotation, uint LayoutId,
    uint EventId = 0, uint EntityId = 0, byte TargetableStatus = 1, uint Arg2 = 0, bool MuteSound = false,
    uint? AnimationState = null, uint? AnimationBitmask = null, int AnimationSeq = 0,
    PropBeatMode AnimationMode = PropBeatMode.ActorControl, bool ForceSharedGroupActive = false, int FadeOutSeq = 0,
    uint DirectorState = 0, int DirectorModSeq = 0, ushort HideAtState = 0);

// Full-state, so a dropped frame costs one tick of staleness, not a wrong reconstruction.
public sealed record WorldSnapshotMessage(
    List<EnemyState> Enemies, List<TetherState> Tethers,
    List<EventObjectState> EventObjects, List<ObstacleState>? Obstacles = null) : MpMessage, IHostOnlyMessage;

// One CircleObstacle of world.Obstacles, which a bot-controlled peer's own character steers
// around; a peer never runs the scenario code that builds the field.
public sealed record ObstacleState(float X, float Z, float Radius);

// Paced independently of WorldSnapshotMessage (see RelayClient's priority queue): role
// positions are small and urgent, enemy data can be large.
public sealed record RolesSnapshotMessage(List<RoleState> Roles) : MpMessage, IHostOnlyMessage;

// One per Game.PartyMemberKilled; the recipient kills whatever holds that role locally.
public sealed record RoleKilledMessage(PartyRole Role, string Cause) : MpMessage, IHostOnlyMessage;

// One per SimNetworkPuppet.Knockback, applied to whoever holds that role locally.
public sealed record KnockbackMessage(PartyRole Role, float SourceX, float SourceY, float SourceZ, float Distance, float Speed) : MpMessage, IHostOnlyMessage;

// The other forced movements a scenario applies to a party member, each from the matching
// SimNetworkPuppet call. Teleport is an ISimPartyMember.TeleportTo (Umad P1's arrow snap);
// Push is eased when DurationSeconds > 0, else a constant-speed slide; Follow with a null
// TargetRole releases the follow.
public sealed record TeleportMessage(PartyRole Role, float X, float Y, float Z, float Rotation) : MpMessage, IHostOnlyMessage;
public sealed record PushMessage(PartyRole Role, float Heading, float Distance, float Speed, float DurationSeconds) : MpMessage, IHostOnlyMessage;
public sealed record CarryMessage(PartyRole Role, float X, float Y, float Z, int Mode = 0) : MpMessage, IHostOnlyMessage;
public sealed record FollowMessage(PartyRole Role, PartyRole? TargetRole, int? TargetEnemyNetId, float Speed, bool Forced = true) : MpMessage, IHostOnlyMessage;

// A strat's bot choreography for a role, from the matching NetworkPuppetMovement call. Sent
// for every role; the owner applies it through its own PlayerMovement, which only acts on it
// while the owner is bot-controlled.
public sealed record MoveMessage(PartyRole Role, float X, float Y, float Z, float Speed, float? FinalRotation) : MpMessage, IHostOnlyMessage;
public sealed record InterceptMessage(PartyRole Role, int TetherNetId, float Margin) : MpMessage, IHostOnlyMessage;
public sealed record FaceMessage(PartyRole Role, float X, float Y, float Z) : MpMessage, IHostOnlyMessage;

// One per SimWorld.OmenSpawned. Path is checked against SimAssets on receipt: it is the one
// field that names a raw game file.
public sealed record SpawnOmenMessage(string Path, float X, float Y, float Z, float Rotation, float ScaleX, float ScaleY, float ScaleZ, float DurationSeconds) : MpMessage, IHostOnlyMessage;

// ReturnedToInn distinguishes Reset() (stays in-zone) from Leave() (unloads); both clear
// ActiveScenario identically. Reason is set only when the group couldn't otherwise explain
// the end (see StartAbortMessage).
public sealed record EndMessage(bool ReturnedToInn, string? Reason = null) : MpMessage, IHostOnlyMessage;

// Every PingIntervalSeconds, lobby included. SentAtMs is the host's own clock and only the
// host compares it.
public sealed record PingMessage(long SentAtMs) : MpMessage, IHostOnlyMessage;

public sealed record PongMessage(Guid PeerId, long SentAtMs) : MpMessage;

public sealed record PeerStatusEntry(float? LatencyMs, float SecondsSinceLastSeen);

public sealed record PeerStatusMessage(Dictionary<Guid, PeerStatusEntry> Statuses) : MpMessage, IHostOnlyMessage;

// From whoever clicks Leave session. Ends the session for everyone only when
// PeerId == Session.HostId; a departing peer is just dropped from the roster.
public sealed record SessionEndedMessage(Guid PeerId) : MpMessage;

// Peer -> host. The host resets its authoritative run, which reaches everyone via EndMessage.
public sealed record ResetRequestMessage(Guid PeerId) : MpMessage;

// Peer -> host: end the run for everyone; the session stays up. SessionEndedMessage instead
// disconnects the sender.
public sealed record LeaveRequestMessage(Guid PeerId) : MpMessage;

// 1:1 replays of MapController.AddEffect/DirectorUpdate, which scenarios call for native
// instance state (arena colour, tower reveals, director flags) outside any SimObject.
public sealed record MapEffectMessage(uint PacketFlags, byte Index) : MpMessage, IHostOnlyMessage;
public sealed record MapDirectorUpdateMessage(
    uint Category, uint Arg1, uint Arg2, uint Arg3, uint Arg4, uint Arg5, uint Arg6) : MpMessage, IHostOnlyMessage;

// Replay of world.SetWeather; scenarios use it mid-fight for lighting cues.
public sealed record SetWeatherMessage(byte WeatherId, float Transition) : MpMessage, IHostOnlyMessage;

// Replay of MapController.SetFogHold, a scenario's live fog toggle; null releases the hold.
public sealed record SetFogHoldMessage(float? FogHold) : MpMessage, IHostOnlyMessage;

// Replay of SimWorld.Announce: a scenario's own mid-run message to the party.
public sealed record AnnouncementMessage(string Text) : MpMessage, IHostOnlyMessage;

// Peer -> host, on change. A real Rampart/invuln press never touches the host's puppet copy.
// SelfShieldFraction is the current total (TankShieldTracker.SetFromPeerReport), not an increment.
public sealed record SelfMitigationMessage(Guid PeerId, List<ushort> ActiveMitigationStatusIds, float SelfShieldFraction = 0f) : MpMessage;

// Peer -> host: a SourceSide mitigation (Reprisal) applied locally; the peer's enemy doppels
// are cosmetic, so this is how the host's enemy gets the debuff.
public sealed record PeerAppliedEnemyStatusMessage(Guid PeerId, List<int> EnemyNetIds, ushort StatusId, float Duration) : MpMessage;

// Party/Ally-scope counterpart: a party-wide mitigation touches roles whose puppets are
// cosmetic. Self-scope presses use SelfMitigationMessage.
public sealed record PeerAppliedRoleStatusMessage(Guid PeerId, List<PartyRole> Roles, ushort StatusId, float Duration, float ShieldFraction = 0f) : MpMessage;
