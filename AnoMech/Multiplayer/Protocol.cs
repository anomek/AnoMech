using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Tea.P3AlexanderPrime;
using AnoMech.Scenarios.Tea.P4PerfectAlexander;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using AnoMech.Scenarios.Umad.P2Forsaken;
using AnoMech.Scenarios.Umad.P3BlackHole;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Multiplayer;

// Wire format; the relay is a per-session broadcaster that knows nothing of it. Positions are
// flattened to floats: System.Text.Json doesn't serialize Vector3's fields without a converter.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(LobbyStateMessage), "lobby")]
[JsonDerivedType(typeof(ClaimRoleMessage), "claim")]
[JsonDerivedType(typeof(ReleaseRoleMessage), "release")]
[JsonDerivedType(typeof(EnterMessage), "enter")]
[JsonDerivedType(typeof(EnteredMessage), "entered")]
[JsonDerivedType(typeof(StartMessage), "start")]
[JsonDerivedType(typeof(StartCheckMessage), "startCheck")]
[JsonDerivedType(typeof(StartCheckResponseMessage), "startCheckResponse")]
[JsonDerivedType(typeof(StartAbortMessage), "startAbort")]
[JsonDerivedType(typeof(SelfPoseMessage), "pose")]
[JsonDerivedType(typeof(GamePacketsMessage), "packets")]
[JsonDerivedType(typeof(RolesSnapshotMessage), "rolesSnapshot")]
[JsonDerivedType(typeof(RoleKilledMessage), "killed")]
[JsonDerivedType(typeof(KnockbackMessage), "knockback")]
[JsonDerivedType(typeof(TeleportMessage), "teleport")]
[JsonDerivedType(typeof(PushMessage), "push")]
[JsonDerivedType(typeof(CarryMessage), "carry")]
[JsonDerivedType(typeof(FollowMessage), "follow")]
[JsonDerivedType(typeof(MarchMessage), "march")]
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
[JsonDerivedType(typeof(AnnouncementMessage), "announcement")]
[JsonDerivedType(typeof(P4AiReplayStateMessage), "p4AiReplayState")]
[JsonDerivedType(typeof(P5AiReplayStateMessage), "p5AiReplayState")]
[JsonDerivedType(typeof(TopP6WaveCannon2AiReplayStateMessage), "topP6Wc2AiReplayState")]
[JsonDerivedType(typeof(TopP2PartySynergyAiReplayStateMessage), "topP2PsAiReplayState")]
[JsonDerivedType(typeof(TopP5SigmaAiReplayStateMessage), "topP5SigmaAiReplayState")]
[JsonDerivedType(typeof(TopP5OmegaAiReplayStateMessage), "topP5OmegaAiReplayState")]
[JsonDerivedType(typeof(UltimatePredationAiReplayStateMessage), "ultimatePredationAiReplayState")]
[JsonDerivedType(typeof(TopP5DeltaAiReplayStateMessage), "topP5DeltaAiReplayState")]
[JsonDerivedType(typeof(UmadP3LimitCutAiReplayStateMessage), "umadP3LimitCutAiReplayState")]
[JsonDerivedType(typeof(UmadP1TeleTrouncingAiReplayStateMessage), "umadP1TeleTrouncingAiReplayState")]
[JsonDerivedType(typeof(UmadP5FloodAiReplayStateMessage), "umadP5FloodAiReplayState")]
[JsonDerivedType(typeof(UcobP5ExaflaresAiReplayStateMessage), "ucobP5ExaflaresAiReplayState")]
[JsonDerivedType(typeof(UmadP5CelestriadAiReplayStateMessage), "umadP5CelestriadAiReplayState")]
[JsonDerivedType(typeof(UltimateSuppressionAiReplayStateMessage), "ultimateSuppressionAiReplayState")]
[JsonDerivedType(typeof(TeaP1LivingLiquidAiReplayStateMessage), "teaP1LivingLiquidAiReplayState")]
[JsonDerivedType(typeof(TeaI1LimitCutAiReplayStateMessage), "teaI1LimitCutAiReplayState")]
[JsonDerivedType(typeof(TeaP2BjCcAiReplayStateMessage), "teaP2BjCcAiReplayState")]
[JsonDerivedType(typeof(TeaI2TemporalStasisAiReplayStateMessage), "teaI2TemporalStasisAiReplayState")]
[JsonDerivedType(typeof(TeaP3AlexanderPrimeAiReplayStateMessage), "teaP3AlexanderPrimeAiReplayState")]
[JsonDerivedType(typeof(TeaP4PerfectAlexanderAiReplayStateMessage), "teaP4PerfectAlexanderAiReplayState")]
[JsonDerivedType(typeof(TopP5DeltaBeyondDefenseUpdateMessage), "topP5DeltaBeyondDefenseUpdate")]
[JsonDerivedType(typeof(TopP5OmegaHelloWorld2UpdateMessage), "topP5OmegaHelloWorld2Update")]
[JsonDerivedType(typeof(PeerAppliedEnemyStatusMessage), "peerAppliedEnemyStatus")]
[JsonDerivedType(typeof(PeerAppliedRoleStatusMessage), "peerAppliedRoleStatus")]
[JsonDerivedType(typeof(PeerDispelledMessage), "peerDispelled")]
[JsonDerivedType(typeof(PeerProvokedEnemyMessage), "peerProvokedEnemy")]
[JsonDerivedType(typeof(PeerAttackedEnemyMessage), "peerAttackedEnemy")]
[JsonDerivedType(typeof(KickMessage), "kick")]
[JsonDerivedType(typeof(ActedMessage), "acted")]
[JsonDerivedType(typeof(StreamStateMessage), "streamState")]
[JsonDerivedType(typeof(StreamGapMessage), "streamGap")]
[JsonDerivedType(typeof(StreamLostMessage), "streamLost")]
public abstract record MpMessage
{
    // Its place in the sender's stream (SessionStream); 0 outside it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Seq { get; init; }

    // On a peer's numbered message, or its StreamLost, which of its streams it's about: each join
    // starts a new one.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Epoch { get; init; }
}

// Sent outside the sender's stream: never numbered, kept or sent again. Only the newest one
// matters (poses, pings, statuses), or it keeps the stream itself in step.
internal interface IUnsequencedMessage;

// Host -> PeerId, answering the Hello of its stream StreamEpoch: the host's latest message (where
// a fresh join starts) and the last of that stream's the host has taken.
public sealed record StreamStateMessage(Guid PeerId, long StreamEpoch, long Sent, long Received) : MpMessage, IHostOnlyMessage, IUnsequencedMessage;

// How far the host has taken one peer's stream, named by its epoch: a peer that joined again only
// trusts what is about its current one.
public sealed record StreamCursor(Guid PeerId, long Epoch, long Received);

// The sender lacks the other end's messages from From on. From a peer, PeerId is the sender;
// from the host, the peer whose stream it is.
public sealed record StreamGapMessage(Guid PeerId, long From) : MpMessage, IPeerMessage, IUnsequencedMessage;

// The answer to a StreamGap that can't be filled any more: go on from Next. PeerId as StreamGap's.
public sealed record StreamLostMessage(Guid PeerId, long Next) : MpMessage, IPeerMessage, IUnsequencedMessage;

// Only the host legitimately sends these; DispatchCore drops one the relay says came from
// elsewhere. A relay without "senderIdentity" can't attest, and the tag then defaults to
// trusted.
internal interface IHostOnlyMessage;

// A peer may send these, naming itself: the only types RelayWire.PeerMessageTypes takes from a
// peer, and the host drops one whose PeerId isn't its sender.
internal interface IPeerMessage
{
    Guid PeerId { get; }
}

// One generic Dispatch case per interface instead of one per scenario (see IMultiplayerReplayable).
internal interface IScenarioReplayStateMessage : IHostOnlyMessage;
internal interface IScenarioMidRunUpdateMessage : IHostOnlyMessage;

// Peer -> host on connect. Version/Checksum catch a build mismatch before it desyncs. Stream*: the
// peer's stream (a new epoch for each join) and how far it has taken the host's, -1 if not yet.
// DebugBot: the peer's own debug bot plays its seat.
public sealed record HelloMessage(Guid PeerId, string DisplayName, string Version, string Checksum, byte ClassJob = 0,
    long StreamEpoch = 0, long StreamSent = 0, long StreamReceived = -1, bool DebugBot = false) : MpMessage, IPeerMessage, IUnsequencedMessage;

public sealed record PeerBuildInfo(string Version, string Checksum)
{
    public string ShortChecksum => Checksum.Length >= 6 ? Checksum[..6] : Checksum;
}

// Full state, so a client that missed an update self-heals and a late joiner resolves the same
// scenario/strat/waymark and clock. ScenarioSettings is the display summary; ScenarioSettingsJson
// is the same overrides for a peer to actually apply (see ScenarioSettingsSync). DebugBots: the
// players, the host included, whose own debug bot plays their seat.
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
    List<Guid>? DebugBots = null) : MpMessage, IHostOnlyMessage;

// The host's event clock when the message left. A peer starts its run from it instead of from
// zero, which would leave it behind by the host's load time plus the travel time. FrameSeconds is
// the host's average frame, part of the lead the peer takes on top.
public sealed record RunClockState(float EventClock, float FrameSeconds);

public sealed record ClaimRoleMessage(Guid PeerId, PartyRole Role) : MpMessage, IPeerMessage;
public sealed record ReleaseRoleMessage(Guid PeerId) : MpMessage, IPeerMessage;

// The named peer leaves on receipt. Banned: the host also drops everything it sends afterwards,
// a fresh Hello included, until unbanned.
public sealed record KickMessage(Guid PeerId, bool Banned = false) : MpMessage, IHostOnlyMessage;

// After the start check: everyone zones in and sets the run's stage (Game.PrepareRun), answering
// with EnteredMessage; the host starts once all have.
public sealed record EnterMessage : MpMessage, IHostOnlyMessage;

public sealed record EnteredMessage(Guid PeerId) : MpMessage, IPeerMessage;

public sealed record StartMessage(RunClockState? Clock = null) : MpMessage, IHostOnlyMessage;

// Sent before StartMessage; every claimed peer answers, so "can't start" reaches the host
// instead of the peer's own start refusing it unseen.
public sealed record StartCheckMessage : MpMessage, IHostOnlyMessage;

public sealed record StartCheckResponseMessage(Guid PeerId, bool Ready, string? Reason) : MpMessage, IPeerMessage;

// The start check is advisory (a host can skip it, and state changes in between), so the
// receiver re-checks on Start; the host then ends the run for everyone and names the reason.
public sealed record StartAbortMessage(Guid PeerId, string Reason) : MpMessage, IPeerMessage;

// Peer -> host. Applied to that peer's SimNetworkPuppet and republished in the next Roles list.
// VX/VZ: the player's own velocity over the ground, so the others' copies carry on at their pace
// between poses and know when they stopped. Jumping: their own client has them in a jump.
public sealed record SelfPoseMessage(Guid PeerId, float X, float Y, float Z, float Rotation, float VX, float VZ, bool Jumping) : MpMessage, IPeerMessage, IUnsequencedMessage;

// The players' seats, the host's own included, where the host has them.
public sealed record RoleState(PartyRole Role, float X, float Y, float Z, float Rotation, float VX, float VZ, bool Jumping);

public sealed record RolesSnapshotMessage(List<RoleState> Roles) : MpMessage, IHostOnlyMessage, IUnsequencedMessage;

// One per Game.PartyMemberKilled; the recipient kills whatever holds that role locally.
public sealed record RoleKilledMessage(PartyRole Role, string Cause) : MpMessage, IHostOnlyMessage;

// One per knockback on a SimNetworkPuppet, applied to whoever holds that role locally. Row: the
// Knockback sheet row, whose ActionEffect entry that seat's own client applies itself.
public sealed record KnockbackMessage(PartyRole Role, float SourceX, float SourceY, float SourceZ, uint Row) : MpMessage, IHostOnlyMessage;

// The other forced movements a scenario applies to a party member, each from the matching
// ForcedMove on a SimNetworkPuppet. Push takes DurationSeconds when > 0, else Distance / Speed;
// Follow with a null TargetRole releases the follow; March walks to (X, Y, Z) at Speed.
public sealed record TeleportMessage(PartyRole Role, float X, float Y, float Z, float Rotation) : MpMessage, IHostOnlyMessage;
public sealed record PushMessage(PartyRole Role, float Heading, float Distance, float Speed, float DurationSeconds) : MpMessage, IHostOnlyMessage;
public sealed record CarryMessage(PartyRole Role, float X, float Y, float Z) : MpMessage, IHostOnlyMessage;
public sealed record FollowMessage(PartyRole Role, PartyRole? TargetRole, uint TargetEnemy, float Speed) : MpMessage, IHostOnlyMessage;
public sealed record MarchMessage(PartyRole Role, float X, float Y, float Z, float Speed) : MpMessage, IHostOnlyMessage;

// ReturnedToInn distinguishes Reset() (stays in-zone) from Leave() (unloads); both clear
// ActiveScenario identically. Reason is set only when the group couldn't otherwise explain
// the end (see StartAbortMessage).
public sealed record EndMessage(bool ReturnedToInn, string? Reason = null) : MpMessage, IHostOnlyMessage;

// Every PingIntervalSeconds, lobby included. SentAtMs is the host's own clock and only the
// host compares it. StreamSent: the host's latest message; StreamReceived: how far it has taken
// each peer's. Clock: the run's, while one is up.
public sealed record PingMessage(long SentAtMs, long StreamSent = 0, List<StreamCursor>? StreamReceived = null, RunClockState? Clock = null)
    : MpMessage, IHostOnlyMessage, IUnsequencedMessage;

public sealed record PongMessage(Guid PeerId, long SentAtMs, long StreamEpoch = 0, long StreamSent = 0, long StreamReceived = -1) : MpMessage, IPeerMessage, IUnsequencedMessage;

public sealed record PeerStatusEntry(float? LatencyMs, float SecondsSinceLastSeen);

public sealed record PeerStatusMessage(Dictionary<Guid, PeerStatusEntry> Statuses) : MpMessage, IHostOnlyMessage, IUnsequencedMessage;

// From whoever clicks Leave session. Ends the session for everyone only when
// PeerId == Session.HostId; a departing peer is just dropped from the roster.
public sealed record SessionEndedMessage(Guid PeerId) : MpMessage, IPeerMessage;

// Peer -> host. The host resets its authoritative run, which reaches everyone via EndMessage.
public sealed record ResetRequestMessage(Guid PeerId) : MpMessage, IPeerMessage;

// Peer -> host: end the run for everyone; the session stays up. SessionEndedMessage instead
// disconnects the sender.
public sealed record LeaveRequestMessage(Guid PeerId) : MpMessage, IPeerMessage;

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

// Every roll; the Hand mechanics are decided live and the peer reads them off the Hand.
public sealed record TeaP1LivingLiquidAiReplayStateMessage(
    int FirstOpenSide, int SecondOpenSide, int FirstCalmTornado, int SecondCalmTornado,
    bool FirstHandOrbFromEarlier, bool SecondHandOrbFromEarlier, int UntetheredTornado, bool MainTankHoldsEarlierTether,
    PartyRole[] Throttled) : MpMessage, IScenarioReplayStateMessage;

// The numbers and the first Hawk Blaster axis; a dead number's retarget is the host's alone.
public sealed record TeaI1LimitCutAiReplayStateMessage(PartyRole[] Numbers, int FirstBlasterAxis) : MpMessage, IScenarioReplayStateMessage;

// Every roll; water's later holders are picked live and the peer reads them off its own screen.
// Nisi and Judgment: a letter per role in PartyRole order, -1 for none.
public sealed record TeaP2BjCcAiReplayStateMessage(
    bool NisiOnDps, int[] Nisi, int[] Judgment, PartyRole FirstWater, PartyRole FirstLightning, int ChakramAxis,
    PartyRole[] Enumerations, PartyRole IceTank, int BotPacing) : MpMessage, IScenarioReplayStateMessage;

// Debuffs: one per role in PartyRole order (StasisDebuff).
public sealed record TeaI2TemporalStasisAiReplayStateMessage(bool BruteJusticeEast, int[] Debuffs) : MpMessage, IScenarioReplayStateMessage;

// Every roll; the chakrams' and Incinerating Heat's targets are picked live and the peer reads them
// off its own screen. Aggravated Assault is the two DPS left over.
public sealed record TeaP3AlexanderPrimeAiReplayStateMessage(
    P3Practice Practice, PartyRole[] OrbHolders, PartyRole RestrainingOrderTank, PartyRole RestrainingOrderDps, PartyRole SharedSentence,
    bool AlexanderPrimeNorth, int SacramentSpot, bool BruteJusticeNorthWest, bool PuddlesMirrored, PartyRole[] Numbers,
    PartyRole[] Enumerations) : MpMessage, IScenarioReplayStateMessage;

// Every roll; the spreads a death leaves are re-aimed live and the peer reads them off its own
// screen. Clones are in projection-id order, BetaClones by BetaRole.
public sealed record TeaP4PerfectAlexanderAiReplayStateMessage(
    P4Practice Practice, PartyRole LightBeacon, PartyRole DarkBeacon, PartyRole[] LightFollowers, bool FinalWordMotionFirst,
    bool OpticalSpreadFirst, PartyRole[] OpticalStackTargets, PartyRole[] AlphaClones, bool AlphaFirstMotion, bool AlphaSecondMotion,
    int AlphaSafeClone, PartyRole[] BetaClones, bool BetaOpticalStack, int BetaOpticalClone, int BetaDonutClone, int[][] JudgmentOrders,
    PartyRole[] GraceTargets, int BotSeed) : MpMessage, IScenarioReplayStateMessage;

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

// The subset TopP5OmegaAi reads. MonitorSide travels as a bool; MonitorTargets is the host's
// already-resolved pick.
public sealed record TopP5OmegaAiReplayStateMessage(
    PartyRole[] HelloWorldTargets, PartyRole[] DoubleDynamicTargets, PartyRole[] MonitorTargets,
    PartyRole[] HelloWorld1JumpOrder, float[] AttackDirectionsRadians,
    OmegaAttack[] OmegaAttacks, float BettleSpawnDirectionRadians, bool FirstWaveCannonFront,
    bool MonitorIsLeft) : MpMessage, IScenarioReplayStateMessage;

// Resolved live at t=46s (a status-stack read + shuffle), so it follows the replay state.
public sealed record TopP5OmegaHelloWorld2UpdateMessage(PartyRole[] Roles) : MpMessage, IScenarioMidRunUpdateMessage;

// The subset TopP5DeltaAi reads; Side/NorthSouth/ArmRotation travel as bools. BeyondDefenseTarget is
// resolved at t=35.7s and follows in TopP5DeltaBeyondDefenseUpdateMessage.
public sealed record TopP5DeltaAiReplayStateMessage(
    PartyRole[] TetherOrder, uint[] FistColors, int PlayerMonitorIndex, bool PlayerMonitorSideIsLeft,
    bool OmegaMonitorSideIsLeft, bool EyeSpawnIsNorth, bool SwivelCannonSideIsLeft, bool[] ArmRotatesClockwise,
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
// are cosmetic, so this is how the host's enemy gets the debuff. Enemies by host entity id.
public sealed record PeerAppliedEnemyStatusMessage(Guid PeerId, List<uint> Enemies, ushort StatusId, float Duration, ushort Param = 0) : MpMessage, IPeerMessage;

// Party counterpart, the peer's own role included: a peer's presses land only on its own
// client, and the host decides who lives from the statuses on its own copies.
public sealed record PeerAppliedRoleStatusMessage(Guid PeerId, List<PartyRole> Roles, ushort StatusId, float Duration, ushort Param = 0) : MpMessage, IPeerMessage;

// Peer -> host: this player's cleanse (Esuna) landed on Role; the host's copy picks what comes off.
public sealed record PeerDispelledMessage(Guid PeerId, PartyRole Role) : MpMessage, IPeerMessage;

// Peer -> host: this player provoked these enemies, by host entity id.
public sealed record PeerProvokedEnemyMessage(Guid PeerId, List<uint> Enemies) : MpMessage, IPeerMessage;

// Peer -> host: this player's first attack on this enemy, by host entity id, landed.
public sealed record PeerAttackedEnemyMessage(Guid PeerId, uint Enemy) : MpMessage, IPeerMessage;

// Peer -> host: a step of this player's own action (IUserActions.Acted), which the host shows on
// the peer's seat for the rest of the party. Target names an actor as a GamePacket does.
public sealed record ActedMessage(Guid PeerId, PlayerActionStep Step, ActionType ActionType, uint ActionId, uint Target, float CastSeconds) : MpMessage, IPeerMessage;
