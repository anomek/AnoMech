using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using AnoMech.Scenarios.Umad.P2Forsaken;
using AnoMech.Scenarios.Umad.P3BlackHole;

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
[JsonDerivedType(typeof(SpawnOmenMessage), "spawnOmen")]
[JsonDerivedType(typeof(EndMessage), "end")]
[JsonDerivedType(typeof(PingMessage), "ping")]
[JsonDerivedType(typeof(PongMessage), "pong")]
[JsonDerivedType(typeof(PeerStatusMessage), "status")]
[JsonDerivedType(typeof(SessionEndedMessage), "sessionEnded")]
[JsonDerivedType(typeof(ResetRequestMessage), "resetRequest")]
[JsonDerivedType(typeof(LeaveRequestMessage), "leaveRequest")]
[JsonDerivedType(typeof(AiReplayStateMessage), "aiReplayState")]
[JsonDerivedType(typeof(P2AiReplayStateMessage), "p2AiReplayState")]
[JsonDerivedType(typeof(P2LockonsUpdateMessage), "p2LockonsUpdate")]
[JsonDerivedType(typeof(MapEffectMessage), "mapEffect")]
[JsonDerivedType(typeof(MapDirectorUpdateMessage), "mapDirectorUpdate")]
[JsonDerivedType(typeof(MapBattleTalkMessage), "mapBattleTalk")]
[JsonDerivedType(typeof(SetWeatherMessage), "setWeather")]
[JsonDerivedType(typeof(SetFogHoldMessage), "setFogHold")]
[JsonDerivedType(typeof(AnnouncementMessage), "announcement")]
[JsonDerivedType(typeof(P4AiReplayStateMessage), "p4AiReplayState")]
[JsonDerivedType(typeof(P5AiReplayStateMessage), "p5AiReplayState")]
[JsonDerivedType(typeof(TopP6WaveCannon2AiReplayStateMessage), "topP6Wc2AiReplayState")]
[JsonDerivedType(typeof(TopP2PartySynergyAiReplayStateMessage), "topP2PsAiReplayState")]
[JsonDerivedType(typeof(TopP5SigmaAiReplayStateMessage), "topP5SigmaAiReplayState")]
[JsonDerivedType(typeof(TopP5OmegaAiReplayStateMessage), "topP5OmegaAiReplayState")]
[JsonDerivedType(typeof(TopP1ProgramLoopAiReplayStateMessage), "topP1ProgramLoopAiReplayState")]
[JsonDerivedType(typeof(TopP3IntermissionAiReplayStateMessage), "topP3IntermissionAiReplayState")]
[JsonDerivedType(typeof(UltimatePredationAiReplayStateMessage), "ultimatePredationAiReplayState")]
[JsonDerivedType(typeof(TopP5DeltaAiReplayStateMessage), "topP5DeltaAiReplayState")]
[JsonDerivedType(typeof(UmadP3LimitCutAiReplayStateMessage), "umadP3LimitCutAiReplayState")]
[JsonDerivedType(typeof(UmadP1TeleTrouncingAiReplayStateMessage), "umadP1TeleTrouncingAiReplayState")]
[JsonDerivedType(typeof(UmadP5FloodAiReplayStateMessage), "umadP5FloodAiReplayState")]
[JsonDerivedType(typeof(UcobP5ExaflaresAiReplayStateMessage), "ucobP5ExaflaresAiReplayState")]
[JsonDerivedType(typeof(UmadP5CelestriadAiReplayStateMessage), "umadP5CelestriadAiReplayState")]
[JsonDerivedType(typeof(UltimateSuppressionAiReplayStateMessage), "ultimateSuppressionAiReplayState")]
[JsonDerivedType(typeof(TopP5DeltaBeyondDefenseUpdateMessage), "topP5DeltaBeyondDefenseUpdate")]
[JsonDerivedType(typeof(TopP5OmegaHelloWorld2UpdateMessage), "topP5OmegaHelloWorld2Update")]
[JsonDerivedType(typeof(PeerAppliedEnemyStatusMessage), "peerAppliedEnemyStatus")]
[JsonDerivedType(typeof(PeerAppliedRoleStatusMessage), "peerAppliedRoleStatus")]
[JsonDerivedType(typeof(PeerLimitBreakMessage), "peerLimitBreak")]
[JsonDerivedType(typeof(PeerClearedStatusMessage), "peerClearedStatus")]
[JsonDerivedType(typeof(KickMessage), "kick")]
public abstract record MpMessage;

// Only the host legitimately sends these; DispatchCore drops one the relay says came from
// elsewhere. A relay without "senderIdentity" can't attest, and the tag then defaults to
// trusted.
internal interface IHostOnlyMessage;

// One generic Dispatch case per interface instead of one per scenario (see IMultiplayerReplayable).
internal interface IScenarioReplayStateMessage : IHostOnlyMessage;
internal interface IScenarioMidRunUpdateMessage : IHostOnlyMessage;

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
    List<string>? ScenarioSettings = null,
    string? ScenarioSettingsJson = null,
    RunClockState? Clock = null,
    bool ResolveOwnActions = true,
    bool RequireTankMitigation = true) : MpMessage, IHostOnlyMessage;

// The host's event clock when the message left. A peer starts its run from it instead of from
// zero, which would leave it behind by the host's load time plus the travel time. FrameSeconds is
// the host's average frame, part of the lead the peer takes on top.
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
public sealed record SelfPoseMessage(Guid PeerId, float X, float Y, float Z, float Rotation) : MpMessage;

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

// NpcSpawnTemplate names a captured spawn packet (NpcSpawnTemplates, resolved by name on receipt): a
// packet-spawned carrier's real, model-less look is what its action VFX attach to.
// LastInstantCastIsNativeEffect: the host fired a bare NativeActionEffect; a peer replays it
// with the same animation target, action target, position and lock instead of a Cast().
// LastInstantCastRawPacket instead names a captured resolve the receiver replays from its own
// copy of the bytes. PersistentVfx is a reconciled set, unlike the one-shot NewVfx.
// ModeAttributeFlags is the live value, which a boss changing form rewrites after its spawn.
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
    IReadOnlyList<string>? PersistentVfx = null,
    float CastAnimationLock = 0.6f, float CastFireDelay = 0f,
    uint CurrentHp = 0, uint MaxHp = 0, int DeathSeq = 0, int CancelledCastSeq = 0, int FadeOutSeq = 0,
    float CastStartElapsed = 0f, byte? ModeAttributeFlags = null, bool WeaponDrawn = false, bool InEnemyList = false);

// Each end resolves to a live enemy (by NetId) or a party role.
public sealed record TetherState(int NetId, ushort TetherId, int? AEnemyNetId, PartyRole? ARole, int? BEnemyNetId, PartyRole? BRole);

// Host-authoritative even for a peer's own role: a peer runs no scenario logic, so statuses,
// lockons and HP all come from here. The animation
// timeline is a scripted pose (Umad P1's sleep, a confused member's swing); the KO pose travels
// as RoleKilledMessage, so a dead role's timeline is not replayed. PlayedAction is a doppel's
// own action animation (a bot's limit break, with its bar and enemy target or ground location),
// edge-triggered on its seq.
public sealed record RoleState(
    PartyRole Role, bool Filled, bool Dead, float X, float Y, float Z, float Rotation,
    IReadOnlyList<EnemyStatusState> Statuses, IReadOnlyList<uint> NewLockonVfxIds,
    uint CurrentHp, uint MaxHp,
    ushort? AnimationTimelineId = null, ushort AnimationTimelineLoopId = 0, int AnimationTimelineSeq = 0,
    IReadOnlyList<AttachedVfxState>? NewVfx = null, IReadOnlyList<string>? PersistentVfx = null,
    uint PlayedActionId = 0, float PlayedActionAnimationLock = 0.6f, int PlayedActionSeq = 0,
    float PlayedActionCastSeconds = 0f, float PlayedActionEffectDelay = 0f, int? PlayedActionTargetEnemyNetId = null,
    float? PlayedActionLocationX = null, float? PlayedActionLocationY = null, float? PlayedActionLocationZ = null,
    bool PlayedActionHoldsStill = false);

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
// LimitBreakBars is the party's gauge, set only while the scenario keeps one
// (IPartyLimitBreakScenario).
public sealed record WorldSnapshotMessage(
    List<EnemyState> Enemies, List<TetherState> Tethers,
    List<EventObjectState> EventObjects, float? LimitBreakBars = null) : MpMessage, IHostOnlyMessage;

// Paced independently of WorldSnapshotMessage (see RelayClient's priority queue): role
// positions are small and urgent, enemy data can be large.
public sealed record RolesSnapshotMessage(List<RoleState> Roles) : MpMessage, IHostOnlyMessage;

// One per Game.PartyMemberKilled; the recipient kills whatever holds that role locally.
public sealed record RoleKilledMessage(PartyRole Role, string Cause) : MpMessage, IHostOnlyMessage;

// One per SimNetworkPuppet.Knockback, applied to whoever holds that role locally.
public sealed record KnockbackMessage(PartyRole Role, float SourceX, float SourceY, float SourceZ, float Distance, float Speed) : MpMessage, IHostOnlyMessage;

// The other forced movements a scenario applies to a party member, each from the matching
// SimNetworkPuppet call. Teleport is an ISimPartyMember.TeleportTo (Umad P1's arrow snap);
// Push is a run when Walk, eased when DurationSeconds > 0, else a constant-speed slide; Follow
// with a null TargetRole releases the follow.
public sealed record TeleportMessage(PartyRole Role, float X, float Y, float Z, float Rotation) : MpMessage, IHostOnlyMessage;
public sealed record PushMessage(PartyRole Role, float Heading, float Distance, float Speed, float DurationSeconds, bool Walk = false) : MpMessage, IHostOnlyMessage;
public sealed record CarryMessage(PartyRole Role, float X, float Y, float Z, int Mode = 0) : MpMessage, IHostOnlyMessage;
public sealed record FollowMessage(PartyRole Role, PartyRole? TargetRole, int? TargetEnemyNetId, float Speed) : MpMessage, IHostOnlyMessage;

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

// The host's per-run rolls, the subset UmadP3BlackHoleAi reads (UmadP3BlackHoleState
// .FromNetworkReplay). ThunderSet1/2 carry the host's real plan so a debug-bot peer
// self-applies the right kit.
public sealed record AiReplayStateMessage(
    PartyRole[] Roles, PartyRole[] StackTargets, uint[] SlapAttacks,
    float[] KefkaPositionRadians, uint ImplosionAttack,
    ThunderIIIAssignment ThunderSet1, ThunderIIIAssignment ThunderSet2) : MpMessage, IScenarioReplayStateMessage;

// The whole P2 state surface: every field is a plain value and 7 Ai variants read different subsets.
public sealed record P2AiReplayStateMessage(
    EndAttack[] EndAttacks, float NewNorthRadians, int Rotation, Dictionary<PartyRole, uint> Lockons) : MpMessage, IScenarioReplayStateMessage;

// Lockons are reassigned mid-run by ReapplyLockons (host-only); a stale replay lookup would
// miss and throw inside EventScheduler.Tick, killing every later scheduled move.
public sealed record P2LockonsUpdateMessage(Dictionary<PartyRole, uint> Lockons) : MpMessage, IScenarioMidRunUpdateMessage;

// 1:1 replays of MapController.AddEffect/DirectorUpdate, which scenarios call for native
// instance state (arena colour, tower reveals, director flags) outside any SimObject.
public sealed record MapEffectMessage(uint PacketFlags, byte Index) : MpMessage, IHostOnlyMessage;
public sealed record MapDirectorUpdateMessage(
    uint Category, uint Arg1, uint Arg2, uint Arg3, uint Arg4, uint Arg5, uint Arg6) : MpMessage, IHostOnlyMessage;

// Replay of MapController.BattleTalk, a boss line.
public sealed record MapBattleTalkMessage(uint SpeakerNameId, uint TextId, uint DurationMs) : MpMessage, IHostOnlyMessage;

// Replay of world.SetWeather; scenarios use it mid-fight for lighting cues.
public sealed record SetWeatherMessage(byte WeatherId, float Transition) : MpMessage, IHostOnlyMessage;

// Replay of MapController.SetFogHold, a scenario's live fog toggle; null releases the hold.
public sealed record SetFogHoldMessage(float? FogHold) : MpMessage, IHostOnlyMessage;

// Replay of SimWorld.Announce: a scenario's own mid-run message to the party.
public sealed record AnnouncementMessage(string Text) : MpMessage, IHostOnlyMessage;

// The subset UmadP4KefkaSaysAi reads (UmadP4KefkaSaysState.FromNetworkReplay); MysteryCast
// reduced to its three scalars.
public sealed record P4AiReplayStateMessage(
    int[] MysteryBlizzardOffset, int[] MysteryLightningOffset, float[] MysteryLightningOrientation,
    bool Wave1First, PartyRole[] Wave1, bool Wave1True, PartyRole[] Wave2, bool Wave2True,
    bool InfernoIsTrue, bool TsunamiIsTrue, PartyRole[] Wave3, bool[] Wounds,
    bool Antilight0IsWhite, float NeoExdeathDirectionRadians) : MpMessage, IScenarioReplayStateMessage;

// LeftOrder/RightOrder is the whole meaningful state; Timeline/SpreadTick are rebuilt locally.
public sealed record P5AiReplayStateMessage(int[] LeftOrder, int[] RightOrder) : MpMessage, IScenarioReplayStateMessage;

// The whole UmadP3LimitCutState roll: first clone spot and walk direction, the eight numbers,
// who carries Headwind, the intercardinal the bosses are held at, and the Umbra Smash bait.
public sealed record UmadP3LimitCutAiReplayStateMessage(
    int StartSpot, bool Clockwise, PartyRole[] Numbers, PartyRole[] Headwinds, int BossSpot, PartyRole BaitRole,
    ThunderIIIAssignment ThunderPlan) : MpMessage, IScenarioReplayStateMessage;

// The whole UmadP1TeleTrouncingState roll, parallel arrays indexed by Roles. Spots derive from
// Debuffs on receipt.
public sealed record UmadP1TeleTrouncingAiReplayStateMessage(
    bool DpsGetsDifferent, bool DpsGetsConfused,
    PartyRole[] Roles, TelePortentDirection[] FirstDirections, TelePortentDirection[] SecondDirections, bool[] Polarity,
    PartyRole ConfettiStackSupport, PartyRole ConfettiStackDps,
    bool GazeInverted, bool FireIsStack, bool FireIsLie, PartyRole FireStackSupport, PartyRole FireStackDps,
    int ThunderRealOffset, bool ThunderOrientationFlipped, bool ThunderIsLie) : MpMessage, IScenarioReplayStateMessage;

// The whole UmadP5FloodState roll; the stack target is rolled per tick on the host and never
// read by the Ai.
public sealed record UmadP5FloodAiReplayStateMessage(
    bool NeSwReversed, bool NwSeReversed, bool NeSwFirst, int StartQuadrant, bool RotationClockwise) : MpMessage, IScenarioReplayStateMessage;

// The set's travel direction plus the order its six lanes fire in; everything else about the
// pattern is derived from those two.
public sealed record UcobP5ExaflaresAiReplayStateMessage(
    float DirectionRadians, float[] LaneOrder) : MpMessage, IScenarioReplayStateMessage;

// Elements and the Catastrophic Choice travel as indices into UmadP5CelestriadState's own fixed
// orders; -1 is "free"/"none". Tower indices index AllTowers, which is fixed for a run.
// TowerElementOrder carries the NE/S/NW sector permutation; null is the legacy fixed layout.
public sealed record UmadP5CelestriadAiReplayStateMessage(
    int[] DoubleElement, Dictionary<PartyRole, int> PlayerDebuffElement,
    int[][] SetActiveTowers, int[] AeroVariant, int[]? TowerElementOrder = null) : MpMessage, IScenarioReplayStateMessage;

// Who has what. The state's other fields are live SimCharacter handles the peer resolves from
// its own party, and LightPillarPlacement is host-only (the Ai never reads it).
public sealed record UltimateSuppressionAiReplayStateMessage(
    PartyRole LightPillar, PartyRole[] MistralSongs, PartyRole[] Eruptions,
    PartyRole Gaol, PartyRole FlamingCrush, int[] SuppressionSpotOrder) : MpMessage, IScenarioReplayStateMessage;

// InFirst is TopP6WaveCannon2Ai's entire read set.
public sealed record TopP6WaveCannon2AiReplayStateMessage(bool InFirst) : MpMessage, IScenarioReplayStateMessage;

// The whole state surface. GlitchType holds a Predicate (not JSON-friendly); it and
// OmegaAttack travel as a bool naming the static instance.
public sealed record TopP2PartySynergyAiReplayStateMessage(
    PartyRole[] Order, PartyRole[] Stacks, float NewNorthARadians, float NewNorthBRadians,
    float AttackDirRadians, bool GlitchIsFar, bool AttackMIsSword, bool AttackFIsStaff) : MpMessage, IScenarioReplayStateMessage;

// The subset TopP5SigmaAi reads; GlitchType/OmegaAttack/Rotation travel as bools naming the
// static instance (see TopP5SigmaState.FromNetworkReplay).
public sealed record TopP5SigmaAiReplayStateMessage(
    PartyRole[] Order, PartyRole[] DynamisTargets, PartyRole[] HelloWorldTargets, PartyRole[] HandBait,
    PartyRole[] HelloWorldJumpOrder,
    float NewNorthARadians, float NewNorthBRadians, bool TowerNorthFlipped, bool GlitchIsFar,
    bool SpinnerIsClockwise, bool OmegaFIsStaff, int FirstMissing, int SecondMissing) : MpMessage, IScenarioReplayStateMessage;

// The whole state surface; tower sets travel flattened, two cardinals per set.
public sealed record TopP1ProgramLoopAiReplayStateMessage(
    PartyRole[] InLine, int[] TowerCardinals, int[] TowerShifts, PartyRole[] FirstTetherHolders) : MpMessage, IScenarioReplayStateMessage;

// The subset TopP3IntermissionAi reads: the debuff order (four spreads, two stacks, two without)
// and where the first arm units appear.
public sealed record TopP3IntermissionAiReplayStateMessage(PartyRole[] Debuffs, bool FirstHandsNorth) : MpMessage, IScenarioReplayStateMessage;

// The subset TopP5OmegaAi reads. MonitorSide travels as a bool; MonitorTargets is the host's
// already-resolved pick.
public sealed record TopP5OmegaAiReplayStateMessage(
    PartyRole[] HelloWorldTargets, PartyRole[] DoubleDynamicTargets, PartyRole[] MonitorTargets,
    PartyRole[] HelloWorld1JumpOrder, float[] AttackDirectionsRadians,
    OmegaAttack[] OmegaAttacks, float BettleSpawnDirectionRadians, bool FirstWaveCannonFront,
    bool MonitorIsLeft) : MpMessage, IScenarioReplayStateMessage;

// Resolved live at t=46s (a status-stack read + shuffle), so it follows the replay state.
public sealed record TopP5OmegaHelloWorld2UpdateMessage(PartyRole[] Roles) : MpMessage, IScenarioMidRunUpdateMessage;

// The subset TopP5DeltaAi reads; Side/NorthSouth travel as bools. BeyondDefenseTarget is
// resolved at t=35.3s and follows in TopP5DeltaBeyondDefenseUpdateMessage.
public sealed record TopP5DeltaAiReplayStateMessage(
    PartyRole[] TetherOrder, uint[] FistColors, int PlayerMonitorIndex, bool PlayerMonitorSideIsLeft,
    bool OmegaMonitorSideIsLeft, bool EyeSpawnIsNorth, bool SwivelCannonSideIsLeft, bool[] ArmHandednessIsLeft,
    PartyRole FarWorldRole, PartyRole NearWorldRole, int FarWorldTetherIndex) : MpMessage, IScenarioReplayStateMessage;

public sealed record TopP5DeltaBeyondDefenseUpdateMessage(PartyRole BeyondDefenseTarget) : MpMessage, IScenarioMidRunUpdateMessage;

// The four boss placements plus the three positions the Ai's live tie-break RNG resolved on
// the host, so a replayed Ai can't land on a different-but-valid safe spot.
public sealed record UltimatePredationAiReplayStateMessage(
    float GarudaX, float GarudaY, float GarudaZ, float GarudaRotation,
    float TitanX, float TitanY, float TitanZ, float TitanRotation,
    float IfritX, float IfritY, float IfritZ, float IfritRotation,
    float UltimaX, float UltimaY, float UltimaZ, float UltimaRotation,
    float SafeCardinalX, float SafeCardinalY, float SafeCardinalZ, float SafeCardinalRotation,
    float SafeFirstSetX, float SafeFirstSetY, float SafeFirstSetZ, float SafeFirstSetRotation,
    float SafeSecondSetX, float SafeSecondSetY, float SafeSecondSetZ, float SafeSecondSetRotation) : MpMessage, IScenarioReplayStateMessage;

// Peer -> host: an enemy debuff (Reprisal) applied locally; the peer's enemy doppels
// are cosmetic, so this is how the host's enemy gets the debuff.
public sealed record PeerAppliedEnemyStatusMessage(Guid PeerId, List<int> EnemyNetIds, ushort StatusId, float Duration) : MpMessage;

// Party counterpart, the peer's own role included: a peer's presses land only on its own
// client, and the host decides who lives from the statuses on its own copies.
public sealed record PeerAppliedRoleStatusMessage(Guid PeerId, List<PartyRole> Roles, ushort StatusId, float Duration) : MpMessage;

// Peer -> host: the peer's own limit break landed, and where it was aimed (LimitBreakAim,
// scenario-local). The host's scenario owns what it does.
public sealed record PeerLimitBreakMessage(Guid PeerId, uint ActionId, float X = 0f, float Y = 0f, float Z = 0f, float Heading = 0f,
    float? LocationX = null, float? LocationY = null, float? LocationZ = null) : MpMessage;

// Peer -> host: statuses the peer's own press took off itself (Shake It Off's dispel).
public sealed record PeerClearedStatusMessage(Guid PeerId, List<ushort> StatusIds) : MpMessage;
