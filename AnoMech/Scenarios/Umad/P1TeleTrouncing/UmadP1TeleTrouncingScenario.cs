using AnoMech.Core.EnemyActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

using Constants = UmadConstants;
using AnoMech.Core.Native.Interfaces;

// Dancing Mad P1 from Kefka's Tele-trouncing cast to the boss going untargetable: arrows,
// Confused/Sleep, Confetti-3, then Mystery Magic's three overlapping resolves.
//
// Multiplayer: the host resolves everything against each peer's reported pose; the forced
// movement the mechanic applies to a peer (the confused chase, an arrow's snap and push) reaches
// them through SimNetworkPuppet's pending network moves.
public sealed class UmadP1TeleTrouncingScenario : IMultiplayerReplayable
{
    public string Name => "Tele-trouncing";
    public IPhase Phase => UmadZone.P1;
    public float BgmSecondsAtStart => 17.73f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP1TeleTrouncingAi()];
    public object SettingsOverrides => settingsWindow.Overrides;

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP1TeleTrouncingState state = null!;
    private SimEnemy? kefka;
    // One invisible Graven Image per player, at the statue that tethers them: it holds the tether,
    // casts that player's Will, and one on the gazing side is moved to the eye point for the gaze.
    private readonly Dictionary<PartyRole, SimEnemy> tetherStatues = [];
    private readonly List<SimEventObject> earlierStatues = [];
    // The real gaze-animation props, spawned as real EObjs so the client plays the statue
    // SharedGroup timelines. Two instances of each, as in the real fight: appear and tether-fire
    // play on one pair, the gaze wind-up and resolve on the other instance of the gazing type.
    // The Graven Image helpers still do the mechanical work, so a full EObj pool costs only the
    // look.
    private SimEventObject? gazeStatueNormal;       // tether pair: appear + tether-fire
    private SimEventObject? gazeStatueInverted;
    private SimEventObject? gazeAnimStatueNormal;   // second instances: wind-up + resolve
    private SimEventObject? gazeAnimStatueInverted;
    // The colossus at the arena centre, already standing when Tele-trouncing starts; its real
    // state history is replayed at scenario start.
    private SimEventObject? gravenStatue;
    private readonly List<SimEventObject> activeArrows = [];
    // Every teleporter must be used by a Confused player; otherwise Kefka answers after Mystery
    // Magic with the lethal Light of Judgment.
    private int arrowsPlaced;
    private int arrowsSoakedByConfused;
    private readonly List<string> arrowSoakFailures = [];
    private readonly UmadP1TeleTrouncingSettingsWindow settingsWindow = new();

    // Gimmick rows the helpers' hit VFX ride on. Double-trouble Trap's and the arrow bursts' are
    // drawn by hand and stay unpreloaded so a native play can't double them.
    private static readonly (ushort Id, string Key)[] ThunderFireTimelines =
    [
        (6090, "mon_sp/gimmick/z3oa_boss_gimmick03"),
        (7530, "mon_sp/gimmick/n4g1_boss_gimmick06"),
        (5823, "mon_sp/gimmick/e3d3_boss_gimmick01"),
    ];

    public UmadP1TeleTrouncingState? LastState { get; private set; }

    // Local to UmadZone.Origin (100, 0, 100). Full 3D, because the two tethers attach to
    // different parts of the colossus.
    internal static readonly Vector3 ConfusedStatuePos = new(-5f, 27f, -75f);
    internal static readonly Vector3 SleepStatuePos = new(7f, 8.5f, -57f);

    // BossMod's "look toward/away from" points (P1StatueGaze). Normal is ~9y from
    // SleepStatuePos, so it stays its own point.
    internal static readonly Vector3 GazeSourceInverted = new(-5f, 0f, -75f);
    internal static readonly Vector3 GazeSourceNormal = new(5.25f, 0f, -66f);

    // Spawn positions of the statue props, local to the origin.
    private static readonly Vector3 GazeStatueNormalPos = new(7f, 8.5f, -57f);           // world (107, 8.5, 43)
    private static readonly Vector3 GazeStatueNormalAnimPos = new(5.25f, 13.5f, -66f);   // world (105.25, 13.5, 34)
    private static readonly Vector3 GazeStatueInvertedPos = new(-5f, 27f, -75f);         // world (95, 27, 25)
    private static readonly Vector3 GazeStatueInvertedAnimPos = new(-5f, 12.5f, -75f);   // world (95, 12.5, 25)

    // The real EObjAnimation beats for the P1 statue, packed as (Param1 << 16) | Param2 as
    // BossMod does: appear ~t19, tether-fire t28.44, gaze wind-up t31.56, gaze resolve t41.46,
    // despawn t52.46.
    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimTetherFire = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimGazeWindUp = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimGazeResolve = (0x0100, 0x0200);
    private static readonly (uint State, uint Bitmask) EObjAnimDespawn = (0x0004, 0x0008);
    // The colossus's own beats: the last stage it reaches before Tele-trouncing (it spawns in
    // that state), and the collapse at the P1 end.
    private const ushort StatueStage3State = 0x0040;
    private static readonly (uint State, uint Bitmask) EObjAnimStatueCollapse = (0x0004, 0x0200);
    // The earlier P1 statues: where they stand and the beat they last played before Tele-trouncing.
    private static readonly (uint EObjId, uint LayoutId, Vector3 Position, ushort State, uint Arg2)[] EarlierStatues =
    [
        (Constants.EObjId.EarlierStatueA, Constants.EObjId.EarlierStatueA1LayoutId, new(0f, 18.5f, -44f), 0x0010, 0x00420003U),
        (Constants.EObjId.EarlierStatueA, Constants.EObjId.EarlierStatueA2LayoutId, new(0f, 0f, -35f), 0x0100, 0x00410003U),
        (Constants.EObjId.EarlierStatueB, Constants.EObjId.EarlierStatueB1LayoutId, new(2.5f, 22.5f, -73f), 0x0010, 0x00450003U),
        (Constants.EObjId.EarlierStatueC, Constants.EObjId.EarlierStatueC1LayoutId, new(26f, 7f, -58.5f), 0x0010, 0x00460003U),
        (Constants.EObjId.EarlierStatueB, Constants.EObjId.EarlierStatueB2LayoutId, new(-8f, 15f, -73f), Constants.EObjId.GazeStatueSpawnState, 0x00430003U),
        (Constants.EObjId.EarlierStatueC, Constants.EObjId.EarlierStatueC2LayoutId, new(16f, 6.5f, -57f), 0x0100, 0x00440003U),
    ];
    private const float StatueCollapseAt = 55.96f;

    public void DrawSettings() => settingsWindow.Draw(FireAppearNow, FireWindUpNow);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP1TeleTrouncingState(world.Rng, settingsWindow.Overrides);
        LastState = state;

        // Game.Catalog reuses one instance across runs; a stopped run's arrow wrappers still
        // hold their frozen positions, and TickArrows would bind someone against them on the
        // first tick.
        foreach (var staleArrow in activeArrows.ToArray()) RemoveArrow(staleArrow);
        arrowPushLock.Clear();
        arrowDwell.Clear();
        pendingArrowPlacements.Clear();
        arrowsPlaced = 0;
        arrowsSoakedByConfused = 0;
        arrowSoakFailures.Clear();
        // The props' SharedGroups outlive the EObj actor, so park each back in the hidden state
        // or a mid-scenario Reset leaves a statue standing until the zone reloads.
        DespawnProp(ref gazeStatueNormal);
        DespawnProp(ref gazeStatueInverted);
        DespawnProp(ref gazeAnimStatueNormal);
        DespawnProp(ref gazeAnimStatueInverted);
        DespawnProp(ref gravenStatue);
        for (var i = 0; i < earlierStatues.Count; i++)
        {
            var prop = earlierStatues[i];
            DespawnProp(ref prop);
        }
        earlierStatues.Clear();
        tetherStatues.Clear();

        world.Events.Add(0f, () =>
        {
            kefka = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: Constants.BNpcBaseId.KefkaP3, NameId: Constants.BNpcNameId.Kefka, Level: 100,
                Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
                Placement: new Placement(Vector3.Zero, -float.Pi), LayoutId: Constants.BNpcLayoutId.KefkaP3));
            kefka?.KeepFacing(party.Get(PartyRole.MainTank));
            // Applied here rather than resolved from the earlier, unmodeled wave, so the
            // duration is what's left until the real expiry (0.09s before the stack lands).
            var support = party.Get(state.ConfettiStackSupport);
            support?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 22.69f);
            var dps = party.Get(state.ConfettiStackDps);
            dps?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 22.69f);

            foreach (var role in state.Debuffs.Keys)
            {
                var confused = UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused;
                if (SpawnGravenImage(confused ? ConfusedStatuePos : SleepStatuePos) is { } statue)
                    tetherStatues[role] = statue;
            }

            // The props, spawned as the real 0x00F5 packets spawn them: EObjId, LayoutId,
            // position, the hidden SharedTimelineState, plus the remaining packet bytes
            // (targetable byte, entity id, Arg2). The LayoutIds resolve to nothing on the client
            // (searched every LGB of this zone and Sigmascape's), so they are carried for
            // fidelity; EventId is the director link, and its knob is the in-game A/B.
            var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
            DiagnosticLog.Info($"[UmadP1TeleTrouncing] Statue props: EventId=0x{propEventId:X}.");
            gazeStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueNormal,
                LayoutId = Constants.EObjId.GazeStatueNormalLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB80u,
                TargetableStatus = 5,
                Arg2 = 0x004A0003u,
                Placement = new Placement(GazeStatueNormalPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
            });
            gazeStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueInverted,
                LayoutId = Constants.EObjId.GazeStatueInvertedLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB81u,
                TargetableStatus = 5,
                Arg2 = 0x00490003u,
                Placement = new Placement(GazeStatueInvertedPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
            });
            // The second instances: the real fight plays the gaze wind-up/resolve on these.
            gazeAnimStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueNormal,
                LayoutId = Constants.EObjId.GazeStatueNormalAnimLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB82u,
                TargetableStatus = 5,
                Arg2 = 0x00480003u,
                Placement = new Placement(GazeStatueNormalAnimPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
            });
            gazeAnimStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueInverted,
                LayoutId = Constants.EObjId.GazeStatueInvertedAnimLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB83u,
                TargetableStatus = 5,
                Arg2 = 0x00470003u,
                Placement = new Placement(GazeStatueInvertedAnimPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
            });
            // The colossus, already standing in the state its pre-Tele-trouncing beats leave it in.
            gravenStatue = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GravenStatue,
                LayoutId = Constants.EObjId.GravenStatueLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB8Au,
                TargetableStatus = 5,
                Arg2 = Constants.EObjId.GravenStatueArg2,
                Placement = new Placement(Vector3.Zero, 0f),
                TimelineState = StatueStage3State,
            });
            if (gravenStatue == null)
                DiagnosticLog.Warn("[UmadP1TeleTrouncing] The colossus (0x1EBFB4) failed to spawn -- EObj pool full?");
            for (var i = 0; i < EarlierStatues.Length; i++)
            {
                var (eobjId, layoutId, position, timelineState, arg2) = EarlierStatues[i];
                var prop = world.SpawnEventObject(new EventObjectSpawnConfig
                {
                    EObjId = eobjId,
                    LayoutId = layoutId,
                    EventId = propEventId,
                    EntityId = Constants.Spawn.EarlierStatueEntityIdBase + (uint)i,
                    TargetableStatus = 5,
                    Arg2 = arg2,
                    Placement = new Placement(position, 0f),
                    TimelineState = timelineState,
                });
                if (prop != null) earlierStatues.Add(prop);
            }

            if (gazeStatueNormal == null || gazeStatueInverted == null
                || gazeAnimStatueNormal == null || gazeAnimStatueInverted == null)
                DiagnosticLog.Warn(
                    $"[UmadP1TeleTrouncing] Gaze prop(s) failed to spawn (tether NE={gazeStatueNormal != null}, NW={gazeStatueInverted != null}; "
                    + $"anim NE={gazeAnimStatueNormal != null}, NW={gazeAnimStatueInverted != null}) -- "
                    + "the gaze still resolves, with no statue animation for its tell.");
        });

        // [19.35s] Real statue "power up", 16.91s before Mystery Magic's cast start; tether pair
        // only, as in the real EObjAnimation stream.
        world.Events.Add(19.43f, PlayStatueAppear);

        // The colossus's real collapse at the P1 end.
        world.Events.Add(StatueCollapseAt, () => Beat(gravenStatue, EObjAnimStatueCollapse));

        // [1.59s] Tele-trouncing cast start; resolves at 6.58s.
        world.Events.Add(1.59f, () => kefka?.Cast(Constants.ActionId.TeleTrouncing, animationLock: 3.1f));

        // Auto-attacks on the main tank at their real beats: none through the Confused window
        // and Mystery Magic.
        foreach (var at in new[] { 8.90f, 11.93f, 19.01f, 44.48f, 48.62f, 51.65f })
            world.Events.Add(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        // [7.34s] Every role gets its 2 debuffs (the EffectResult lands 0.76s after the resolve).
        world.Events.Add(7.34f, ApplyDebuffs);

        // Teleporters drop at each debuff's own expiry (14.33s/17.32s). Two beats, a
        // replay-confirmed gap: the placement burst (8 helper casts) fires before the teleporter
        // becomes a walkable EObj.
        world.Events.Add(14.42f, () => SpawnArrowBursts(first: true));
        world.Events.Add(15.29f, SpawnArrowObjects);
        world.Events.Add(17.43f, () => SpawnArrowBursts(first: false));
        world.Events.Add(18.32f, SpawnArrowObjects);

        world.Events.Add(15.58f, () => PlayKefkaLine(0x3U));

        // [15.67s] Graven Image (2.7s bar, resolves 18.66s); the tethers appear as it lands.
        world.Events.Add(15.67f, () => kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f));

        // [19.41s] Tethers land, one role category to each statue.
        world.Events.Add(19.41f, TetherStatues);

        // [20.80s] Unnamed 2.7s cast: 0.8s after it resolves, Kefka's model state goes to 4 until
        // Unk2BossP1 resolves.
        world.Events.Add(20.80f, () =>
        {
            kefka?.KeepFacing(null);
            kefka?.Cast(Constants.ActionId.KefkaRest, animationLock: 2.1f);
        });

        // [22.78s] Confetti-3's stack hit, the one beat that drifts pull to pull (timed off the
        // earlier Double-trouble Trap cast). Pushes the ~3 players within 6y of each holder 14y
        // at 20y/s; the Ai holds its next move until the slide ends.
        world.Events.Add(22.78f, ResolveConfettiKnockback);

        world.Events.Add(24.56f, () => kefka?.SetModelState(4));

        // [28.40s] Tethers drop and the props play tether-fire; [28.46s] the Wills land;
        // [29.09s] the Confused/Sleep statuses (the EffectResult trails the hit by 0.63s), 6.00s
        // each.
        world.Events.Add(28.40f, PlayStatueTetherFire);
        world.Events.Add(28.46f, ResolveGravenWills);
        world.Events.Add(ConfusedChaseStart, ApplyConfusedAndSleepStatuses);

        // Release the forced chase when Confused's 6.00s runs out (see ReleaseConfusedControl).
        world.Events.Add(ConfusedChaseEnd, ReleaseConfusedControl);

        // [32.78s] Unnamed instant; Kefka's model state goes back to 0 0.8s after it. [34.87s]
        // Kefka's teleport action; he stays at centre, only the animation plays.
        world.Events.Add(32.78f, () => kefka?.Cast(Constants.ActionId.KefkaUnrest, animationLock: 2.1f));
        world.Events.Add(33.56f, () => kefka?.SetModelState(0));
        world.Events.Add(34.87f, () => kefka?.Cast(Constants.ActionId.KefkaPoof, animationLock: 1.1f));

        // Every arrow is a steer-around obstacle from its spawn (SpawnArrowObjects) so the Ai's
        // Confetti/Tether moves path around them; cleared as Confused starts, since the chase's
        // Follow needs to walk straight into whatever arrow is in the way.
        world.Events.Add(ConfusedChaseStart, () => world.Obstacles.Clear());

        // [36.41s] Mystery Magic (4.7s bar), resolves 41.40s with the thunder lines.
            
            
        world.Events.Add(36, () => kefka?.KeepFacing(party.Get(PartyRole.MainTank)));
        world.Events.Add(36.41f, () => kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f));

        // Mystery Magic's three resolves land in a ~1s window (thunder 41.40s, gaze 41.49s, fire
        // 42.19s); the boss goes untargetable ~10s later.

        // [36.31s] Kefka's two orb headmarkers and the fire stack/spread marker, 0.1s before the
        // cast start; a lie flips the shown fire icon. The Ai reads state directly.
        world.Events.Add(36.31f, () =>
        {
            kefka?.AttachLockonVfx(state.FireIsLie ? Constants.LockonId.FireFalse : Constants.LockonId.FireTrue);
            kefka?.AttachLockonVfx(state.ThunderIsLie ? Constants.LockonId.LightningFalse : Constants.LockonId.LightningTrue);
            AttachFireMarkers();
        });

        // Thrumming Thunder III: on a truth set the 2 real lines cast 0xBA9F (a visible
        // telegraph) and nothing else; on a lie the reals cast 0xBAA1 (no telegraph, still
        // lethal) and the safe slots cast 0xBAA0 (a harmless telegraph). Hits 4.99s after the
        // cast start.
        world.Events.Add(36.41f, SpawnThunderLines);

        // Statue gaze: the gazing prop plays the wind-up 4.8s before Mystery Magic's cast start
        // and the resolve 5.08s after it (BossMod's FutureTime(9.9f)).
        world.Events.Add(31.61f, PlayGazeTell);
        world.Events.Add(41.49f, ResolveGaze);

        // Flagrant Fire III, 0.79s after the thunder lines.
        world.Events.Add(42.19f, ResolveFire);

        // We just gonna add a fake lethal raidwide here to pretend we wipe here if arrows are not
        // soaked by the time confused runs out. The failure line is the real P1-end 0x05, pulled
        // forward to 1s after the fire so nobody waits for the wipe.
        world.Events.Add(ConfusedChaseEnd, JudgeArrowSoaks);
        world.Events.Add(43.19f, PlayFailedP1EndLine);
        world.Events.Add(44.47f, PlayFireLine);
        world.Events.Add(52.67f, PlayP1EndLine);

        // [52.68s] Boss goes untargetable; the P2 actors spawn 2.2s later in the real fight.
        world.Events.Add(52.68f, EndP1);
        world.Events.Add(55.00f, DespawnGazeProps);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP1TeleTrouncingState>)AiStrats[idx]).Run(state, world);
    }

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(ThunderFireTimelines, "UmadP1TeleTrouncing");

        instanceWorld.Events.Add(3f, () => instanceWorld.Map.LogArena("3s"));
        instanceWorld.Events.Add(10f, () => instanceWorld.Map.LogArena("10s"));
    }

    public MpMessage? BuildReplayStateMessage()
    {
        if (LastState is not { } s) return null;
        var roles = s.Debuffs.Keys.ToArray();
        return new UmadP1TeleTrouncingAiReplayStateMessage(
            s.DpsGetsDifferent, s.DpsGetsConfused,
            roles, roles.Select(r => s.Debuffs[r].First).ToArray(), roles.Select(r => s.Debuffs[r].Second).ToArray(),
            roles.Select(r => s.DifferentPolarity.GetValueOrDefault(r)).ToArray(),
            s.ConfettiStackSupport, s.ConfettiStackDps,
            s.GazeInverted, s.FireIsStack, s.FireIsLie, s.FireStackSupport, s.FireStackDps,
            s.ThunderRealOffset, s.ThunderOrientationFlipped, s.ThunderIsLie);
    }

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UmadP1TeleTrouncingAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = UmadP1TeleTrouncingState.FromNetworkReplay(
            msg.DpsGetsDifferent, msg.DpsGetsConfused, msg.Roles, msg.FirstDirections, msg.SecondDirections, msg.Polarity,
            msg.ConfettiStackSupport, msg.ConfettiStackDps,
            msg.GazeInverted, msg.FireIsStack, msg.FireIsLie, msg.FireStackSupport, msg.FireStackDps,
            msg.ThunderRealOffset, msg.ThunderOrientationFlipped, msg.ThunderIsLie);
        if (shadowState == null) return null;
        ((IScenarioAi<UmadP1TeleTrouncingState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The arrows steer a bot's Confetti/Tether moves the same way SpawnArrowObjects does on the
    // host, and stop doing so once Confused starts, when the chase has to walk into them: the
    // client's control lock, which the host's Confused puts on it.
    public void RebuildPeerObstacles(ObstacleField obstacles, IReadOnlyDictionary<int, SimEnemy> peerEnemies, IReadOnlyDictionary<int, SimEventObject> peerEventObjects, SimCharacter? localPlayer)
    {
        if (localPlayer == null || Natives.PlayerConditions.ControlAffliction) return;
        foreach (var eo in peerEventObjects.Values)
            if (eo.EObjRowId == Constants.EObjId.TelePortent)
                obstacles.Add(new CircleObstacle(new Vector2(eo.Position.X, eo.Position.Z), ArrowTriggerRadius));
    }

    private const float ArrowTriggerRadius = 2.0f;
    // Two arrows collide once their hitboxes touch.
    private const float ArrowCollideDistance = ArrowTriggerRadius * 2f;

    private const float ArrowPushDistance = 6f; // == UmadP1TeleTrouncingState.Step
    private const float ArrowBindDuration = 1.000f;
    private const float ArrowTriggerDwell = 0.35f;
    private readonly Dictionary<(SimEventObject Arrow, PartyRole Role), float> arrowDwell = [];

    // A fresh arrow spawns under whoever's debuff just expired, who would retrigger it at once.
    // 3.00s is the real gap between the wave-1 and wave-2 expiries: the most that is still
    // always spent walking to place the next one.
    private const float ArrowGraceDuration = 3.0f;
    private readonly Dictionary<SimEventObject, float> arrowGrace = [];

    // An untouched arrow expires on its own, flipping to EventState 7 this long after its spawn.
    private const float ArrowLifespanAfterSpawn = 18.687f;
    private readonly Dictionary<SimEventObject, float> arrowLifespan = [];

    // Confused roles walk toward whoever is closest and swing at whoever they reach (SimParty);
    // the arrows are meant to keep carrying them away, but that is an outcome the player has to
    // earn. Sleep can't move at all. The window is Confused's own apply-to-expiry span.
    private const float ConfusedChaseStart = 29.09f;
    private const float ConfusedChaseEnd = 29.09f + 6.000f;
    private readonly Dictionary<PartyRole, float> arrowPushLock = [];

    // UNVERIFIED, from the user's description rather than a capture. The post-carry wait is what
    // keeps a completed chain harmless: its last carry lands with ~1.5s of Confused left.
    private const float PostCarryWaitDuration = 2.000f;

    public void Tick(float delta, float elapsed)
    {
        TickArrows(delta, elapsed);
    }



    private void TickArrows(float delta, float elapsed)
    {
        foreach (var role in arrowPushLock.Keys.ToArray())
        {
            var remaining = arrowPushLock[role] - delta;
            if (remaining > 0f) arrowPushLock[role] = remaining;
            else arrowPushLock.Remove(role);
        }

        if (activeArrows.Count == 0) return;

        // Two roles' spawn spots can land within a hitbox-width of each other; overlapping arrows
        // collide and remove each other (user's call).
        var snapshot = activeArrows.ToArray();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var a = snapshot[i];
            if (!activeArrows.Contains(a)) continue; // already poofed by an earlier pair this tick
            for (var j = i + 1; j < snapshot.Length; j++)
            {
                var b = snapshot[j];
                if (!activeArrows.Contains(b)) continue;
                var adx = a.Position.X - b.Position.X;
                var adz = a.Position.Z - b.Position.Z;
                if (adx * adx + adz * adz > ArrowCollideDistance * ArrowCollideDistance) continue;
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] Two arrows collided at t={elapsed:F2}s -- ({a.Position.X:F1},{a.Position.Z:F1}) and ({b.Position.X:F1},{b.Position.Z:F1}), {MathF.Sqrt(adx * adx + adz * adz):F1}y apart -- poofing both.");
                NoteArrowSoakFailure($"two arrows collided and vanished unused at t={elapsed:F2}s -- ({a.Position.X:F1},{a.Position.Z:F1}) and ({b.Position.X:F1},{b.Position.Z:F1})");
                RemoveArrow(a);
                RemoveArrow(b);
                break;
            }
        }
        if (activeArrows.Count == 0) return;

        foreach (var arrow in activeArrows.ToArray())
        {
            var lifespan = arrowLifespan.GetValueOrDefault(arrow) - delta;
            if (lifespan <= 0f)
            {
                NoteArrowSoakFailure($"the arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}) expired unused at t={elapsed:F2}s ({ArrowLifespanAfterSpawn:F2}s after it spawned)");
                RetireArrow(arrow);
                continue;
            }
            arrowLifespan[arrow] = lifespan;

            var grace = arrowGrace.GetValueOrDefault(arrow) - delta;
            if (grace > 0f) { arrowGrace[arrow] = grace; continue; }
            arrowGrace.Remove(arrow);

            // No time gate: walking into an arrow early is a real failure mode. world.Obstacles
            // makes bots steer around arrows without preventing the interaction.
            foreach (var role in state.Debuffs.Keys)
            {
                if (party.Get(role) is not { } member) continue;
                var dx = member.Position.X - arrow.Position.X;
                var dz = member.Position.Z - arrow.Position.Z;
                if (dx * dx + dz * dz > ArrowTriggerRadius * ArrowTriggerRadius)
                {
                    arrowDwell.Remove((arrow, role));
                    continue;
                }
                // Counts through a carry, so a ride ending on the next arrow fires it as Bind lifts.
                var dwell = arrowDwell.GetValueOrDefault((arrow, role)) + delta;
                arrowDwell[(arrow, role)] = dwell;
                if (dwell < ArrowTriggerDwell || arrowPushLock.GetValueOrDefault(role) > 0f) continue;
                // Logged unconditionally with positions, so an early trigger can be checked
                // against the knockback's own logged spots.
                // Confused as the 0x52 says: Indulgent Will puts the status on at the hit,
                // hidden until its EffectResult.
                var confused = member.ActorControl.Confused;
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] {role} used an arrow at t={elapsed:F2}s ({(elapsed < ConfusedChaseStart ? "BEFORE confused-chase start" : "during/after confused-chase")}, {(confused ? "Confused" : "NOT Confused")}) -- "
                    + $"member at ({member.Position.X:F1},{member.Position.Z:F1}), arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}).");
                if (confused) arrowsSoakedByConfused++;
                else NoteArrowSoakFailure($"{role} used the arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}) at t={elapsed:F2}s while not Confused");
                UseArrow(role, member, arrow);
                RetireArrow(arrow);
                break;
            }
        }
    }

    // Both exit paths go through here so no stale entry survives; despawning is the caller's job.
    private void UntrackArrow(SimEventObject arrow)
    {
        activeArrows.Remove(arrow);
        arrowGrace.Remove(arrow);
        arrowLifespan.Remove(arrow);
        foreach (var key in arrowDwell.Keys.Where(k => k.Arrow == arrow).ToArray()) arrowDwell.Remove(key);
    }

    private bool ArrowSoakFailed => arrowSoakFailures.Count > 0 || arrowsSoakedByConfused < arrowsPlaced;

    private void NoteArrowSoakFailure(string what)
    {
        arrowSoakFailures.Add(what);
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow-soak failure {arrowSoakFailures.Count}: {what}.");
        world.Fail($"Arrow soak failed: {what}");
    }

    private void JudgeArrowSoaks()
    {
        var summary = $"{arrowsSoakedByConfused}/{arrowsPlaced} arrows soaked by Confused players";
        if (!ArrowSoakFailed)
        {
            DiagnosticLog.Info($"[UmadP1TeleTrouncing] Arrow soaks passed: {summary}.");
            return;
        }
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow soaks FAILED ({summary}): {string.Join("; ", arrowSoakFailures)}.");
        if (arrowSoakFailures.Count == 0) world.Fail($"Arrow soak failed: only {summary}");
    }

    private void PlayFireLine()
    {
        if (ArrowSoakFailed) return;
        PlayKefkaLine(0xCU);
        kefka?.PlayActionTimeline(Constants.TimelineId.KefkaFireLine);
    }

    private void PlayFailedP1EndLine()
    {
        if (ArrowSoakFailed) PlayKefkaLine(0x5U);
    }

    private void PlayP1EndLine()
    {
        if (ArrowSoakFailed) return;
        PlayKefkaLine(0xDU);
        kefka?.PlayActionTimeline(Constants.TimelineId.KefkaP1End);
    }

    // The battle talk names its speaker by entity id.
    private void PlayKefkaLine(uint line)
    {
        if (kefka is { } boss) world.Map.DirectorUpdate(0x80000027U, line, 0x2U, 0x1BDBU, boss.EntityId);
    }

    // Collisions and resets only: a used or expired arrow is retired first and removed later.
    private void RemoveArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        arrow.Despawn();
    }

    private const float RetiredArrowDespawnDelay = 3.26f;

    private void RetireArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        arrow.DirectorEObjMod(Constants.EObjState.TelePortentUsed);
        world.Events.Add(RetiredArrowDespawnDelay, arrow.Despawn);
    }

    // 0x52 off ends the chase (SimParty).
    private void ReleaseConfusedControl()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            if (UmadP1TeleTrouncingState.IsDps(role) != state.DpsGetsConfused) continue;
            if (party.Get(role) is not { } member) continue;
            member.ActorControl.SetConfused(false);
        }
    }

    // The carry holds the chase while it flies; the post-carry wait holds it after.
    private void UseArrow(PartyRole role, SimCharacter member, SimEventObject arrow)
    {
        member.HoldConfusedChase(ArrowBindDuration + PostCarryWaitDuration);
        member.AddStatus(Constants.StatusId.Bind, ArrowBindDuration);
        arrowPushLock[role] = ArrowBindDuration;
        var heading = arrow.Rotation;
        var destination = arrow.Position + new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * ArrowPushDistance;
        member.CarryTo(destination);
    }

    // Heading as SimCast's facing calc: south=0, increasing clockwise toward east (north matches
    // Kefka's own logged heading).
    private static float Heading(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => float.Pi,
        TelePortentDirection.Down => 0f,
        TelePortentDirection.Right => float.Pi / 2f,
        TelePortentDirection.Left => -float.Pi / 2f,
        _ => 0f,
    };

    // Resolved by SpawnArrowBursts, consumed by SpawnArrowObjects ~0.8s later; the waves never overlap.
    private readonly Dictionary<PartyRole, Placement> pendingArrowPlacements = [];

    // Under the member's live position, not the Ai target: the real teleporter drops wherever the
    // player stands when the debuff expires. Locked in at the resolve and reused unchanged for
    // the EObj 0.8s later.
    private void SpawnArrowBursts(bool first)
    {
        pendingArrowPlacements.Clear();
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null) continue;
            var (at7, at10) = state.ExpiryDirections(role);
            var direction = first ? at7 : at10;
            var placement = new Placement(member.Position, Heading(direction));
            pendingArrowPlacements[role] = placement;
            // The 0-damage placement tell.
            kefka?.CastFromHelper(UmadActions.TeleTrouncingArrowSpawn, member);
        }
    }

    private void SpawnArrowObjects()
    {
        foreach (var placement in pendingArrowPlacements.Values)
        {
            // ActorControl 106 routes by entity id; an arrow left at 0 never receives it.
            var arrow = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.TelePortent,
                Placement = placement,
                TargetableStatus = 5,
                EventId = Constants.EObjId.PropEventId,
                EntityId = Constants.Spawn.TelePortentEntityIdBase + (uint)arrowsPlaced,
                Arg2 = ((uint)(Constants.Spawn.TelePortentArg2Serial + arrowsPlaced) << 16) | 0x3U,
            });
            if (arrow == null)
            {
                // Tagged here too so the symptom ("stepped on the arrow, nothing happened") and
                // its cause share a grep.
                DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow failed to spawn at ({placement.Position.X:F1},{placement.Position.Z:F1}) -- it will never trigger for anyone.");
                continue;
            }
            activeArrows.Add(arrow);
            arrowsPlaced++;
            arrowGrace[arrow] = ArrowGraceDuration;
            arrowLifespan[arrow] = ArrowLifespanAfterSpawn;
            // Cleared in one shot as Confused starts (see Run).
            world.Obstacles.Add(new CircleObstacle(new Vector2(placement.Position.X, placement.Position.Z), ArrowTriggerRadius));
        }
        pendingArrowPlacements.Clear();
    }

    private void ResolveConfettiKnockback()
    {
        // The "before" half for checking a post-knockback landing against a live arrow.
        var positions = new List<string>();
        foreach (var role in state.Debuffs.Keys)
            if (party.Get(role) is { } member)
                positions.Add($"{role}=({member.Position.X:F1},{member.Position.Z:F1})");
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Confetti-3 knockback resolving -- support stack={state.ConfettiStackSupport}, dps stack={state.ConfettiStackDps}, positions: {string.Join(", ", positions)}.");

        // The second stack's hit sees the first's vuln, so standing in both is lethal.
        CastConfettiStack(state.ConfettiStackSupport);
        CastConfettiStack(state.ConfettiStackDps);
    }

    // The stack and its push centre on the holder.
    private void CastConfettiStack(PartyRole holderRole)
    {
        if (party.Get(holderRole) is not { } holder) return;
        kefka?.CastFromHelper(UmadActions.DoubleTroubleTrapStack, holder);
    }

    private SimEnemy? SpawnGravenImage(Vector3 position)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.GravenImage, NameId: Constants.BNpcNameId.GravenImage, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(position, 0f), LayoutId: Constants.BNpcLayoutId.GravenImage));

    private void DespawnProp(ref SimEventObject? prop)
    {
        if (prop == null) return;
        if (prop.IsAlive) Beat(prop, EObjAnimDespawn);
        prop.Despawn();
        prop = null;
    }

    private void Beat(SimEventObject? prop, (uint State, uint Bitmask) beat)
        => prop?.PlayBeat(beat.State, beat.Bitmask);

    // Settings-window buttons: the same beats the timeline fires at 19.35 / 31.63, on demand.
    private void FireAppearNow()
    {
        if (gazeStatueNormal == null && gazeStatueInverted == null && gravenStatue == null) return;
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Statue appear fired from the settings window.");
        Beat(gravenStatue, EObjAnimAppear);
        PlayStatueAppear();
    }

    private void FireWindUpNow()
    {
        if (state == null || GazingProp == null) return;
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Statue wind-up fired from the settings window.");
        PlayGazeTell();
    }

    private void PlayStatueAppear()
    {
        Beat(gazeStatueNormal, EObjAnimAppear);
        Beat(gazeStatueInverted, EObjAnimAppear);
    }

    private void TetherStatues()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            if (tetherStatues.GetValueOrDefault(role) is { } statue && party.Get(role) is { } member)
                world.Tether(statue, member, Constants.TetherId.GravenImage, duration: 8.99f);
        }
    }

    // Both gaze props play tether-fire 0.06s before the two Wills land.
    private void PlayStatueTetherFire()
    {
        Beat(gazeStatueNormal, EObjAnimTetherFire);
        Beat(gazeStatueInverted, EObjAnimTetherFire);
    }

    // The Confused/Sleep statuses come 0.63s later, when the real EffectResult applies them.
    // Every Idyllic Will goes first, so its vuln makes Indulgent Will lethal to a Confused player
    // caught in a sleeper's circle.
    private void ResolveGravenWills()
    {
        CastGravenWills(confused: false);
        CastGravenWills(confused: true);
    }

    private void CastGravenWills(bool confused)
    {
        foreach (var (role, statue) in tetherStatues)
        {
            if ((UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused) != confused) continue;
            if (party.Get(role) is { } member)
                statue.Cast(confused ? UmadActions.IndulgentWill : UmadActions.IdyllicWill, member);
        }
    }

    // 6.00s each, at the real EffectResult time: the chase starts with the status, not the hit.
    // Confused itself comes from Indulgent Will's hit.
    private void ApplyConfusedAndSleepStatuses()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused;
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            // No freeze on apply: confused players walk at once, and a freeze pushed the last arrow
            // past its expiry.
            if (confused)
            {
                member.ActorControl.SetConfused(true);
                continue;
            }
            var status = member.AddStatus(Constants.StatusId.Sleep, 6.000f);
            if (tetherStatues.GetValueOrDefault(role) is { } statue) status?.ShownAsFrom(statue);
        }
    }

    // A matching pair uses the primary id both times; a different pair's id form is fixed by
    // cycle order (primary First + secondary Second, never the reverse), while the 7s/10s split
    // is the independent coin flip (12 real resolves). A matching role's two AddStatus calls
    // share one id, so distinct sourceObject tags keep them from collapsing into one refreshed
    // stack; Kefka and no source are just two distinct tags.
    private void ApplyDebuffs()
    {
        foreach (var (role, (first, second)) in state.Debuffs)
        {
            var member = party.Get(role);
            var firstSource = kefka?.GameObjectId ?? default;
            if (first == second)
            {
                member?.AddStatus(PrimaryStatusId(first), 7.000f, sourceObject: firstSource);
                member?.AddStatus(PrimaryStatusId(second), 10.000f);
            }
            else
            {
                var firstAt7 = !state.DifferentPolarity[role];
                member?.AddStatus(PrimaryStatusId(first), firstAt7 ? 7.000f : 10.000f);
                member?.AddStatus(SecondaryStatusId(second), firstAt7 ? 10.000f : 7.000f);
            }
        }
    }

    // --- Mystery Magic (end of P1): line dodges + stack/spread + statue gaze ----------------

    // Thrumming Thunder III's 4 line anchors, local, each rect heading -pi/4 (len 40, width 10):
    // the same along=-20 / perp +-5,+-15 lattice P5 Flood's decode found. internal so the Ai
    // derives its copy from this array instead of a hand-synced duplicate.
    internal static readonly Placement[] ThunderAnchors =
    [
        new(new Vector3(24.75f, 0f, -3.54f), -MathF.PI / 4f),
        new(new Vector3(17.68f, 0f, -10.61f), -MathF.PI / 4f),
        new(new Vector3(10.61f, 0f, -17.68f), -MathF.PI / 4f),
        new(new Vector3(3.54f, 0f, -24.75f), -MathF.PI / 4f),
    ];

    // What the players see: a lie flips the icon vs the real outcome. Shown-stack marks only the
    // 2 targets, shown-spread everyone.
    private void AttachFireMarkers()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null) continue;
            if (state.FireShowsStackIcon)
            {
                if (role == state.FireStackSupport || role == state.FireStackDps)
                    member.AttachLockonVfx(Constants.LockonId.FireStack);
            }
            else
            {
                member.AttachLockonVfx(Constants.LockonId.FireSpread);
            }
        }
    }

    // Reals: 2 of the 4 slots by (i + ThunderRealOffset) parity. Fakes only telegraph on a lie,
    // identically to a real line (user's call: the only distinguisher is Kefka's thunder orb).
    private void SpawnThunderLines()
    {
        // A truthful set is 2x Real1 and nothing else; a lying set switches the reals to Real2
        // and adds Fake at the other slots.
        var realLine = state.ThunderIsLie ? UmadActions.ThrummingThunderReal2 : UmadActions.ThrummingThunderReal1;
        var lines = 0;
        for (var i = 0; i < ThunderAnchors.Length; i++)
        {
            var real = (i + state.ThunderRealOffset) % 2 == 0;
            if (!real && !state.ThunderIsLie) continue; // truth: fakes don't appear at all
            var placement = ThunderAnchors[i].MulX(state.ThunderOrientation).MulRot(state.ThunderOrientation);
            lines++;
            if (real) kefka?.CastFromHelper(realLine, at: placement);
            else kefka?.CastFromHelper(Constants.ActionId.ThrummingThunderIII_FakeOmen, at: placement, animationLock: 1.1f);
        }
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Thunder lines: offset={state.ThunderRealOffset} orient={state.ThunderOrientation:F0} lie={state.ThunderIsLie} "
            + $"-> {lines} telegraph(s).");
    }

    // One of the gazing side's tether statues casts the gaze, and the real prop that plays the
    // statue animation is the second instance of the gazing type, matching the real beat routing.
    private SimEnemy? GazingStatue => tetherStatues
        .LastOrDefault(kv => (UmadP1TeleTrouncingState.IsDps(kv.Key) == state.DpsGetsConfused) == state.GazeInverted).Value;
    private SimEventObject? GazingProp => state.GazeInverted ? gazeAnimStatueInverted : gazeAnimStatueNormal;

    // The ~10s "eyes light up" wind-up; on the real 0x1EBFBF the prop's animation draws the "?".
    // Which side glows is also a real tell (NW = look toward, NE = look away).
    private void PlayGazeTell()
    {
        Beat(GazingProp, EObjAnimGazeWindUp);
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Gaze tell: {(state.GazeInverted ? "INVERTED (\"?\" -- look TOWARD the NW confused statue)" : "NORMAL (no \"?\" -- look AWAY from the NE sleep statue)")} "
            + $"(prop {(GazingProp != null ? "playing wind-up anim" : "absent -- no tell")}).");
    }

    private void ResolveGaze()
    {
        Beat(GazingProp, EObjAnimGazeResolve);

        if (GazingStatue is not { } statue) return;
        statue.SetPosition(new Placement(state.GazeInverted ? GazeStatueInvertedAnimPos : GazeStatueNormalAnimPos, 0f));
        statue.Cast(state.GazeInverted ? UmadActions.AveMaria : UmadActions.IndolentWill);
    }

    private void ResolveFire()
    {
        if (state.FireIsStack) ResolveFireStack();
        else ResolveFireSpread();
    }

    private void ResolveFireSpread()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null || !member.IsAlive()) continue;
            kefka?.CastFromHelper(UmadActions.FlagrantFireSpread, member);
        }
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Flagrant Fire III: SPREAD resolved.");
    }

    private void ResolveFireStack()
    {
        ResolveOneFireStack(state.FireStackSupport);
        ResolveOneFireStack(state.FireStackDps);
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Flagrant Fire III: STACK resolved (support={state.FireStackSupport}, dps={state.FireStackDps}).");
    }

    private void ResolveOneFireStack(PartyRole holderRole)
    {
        if (party.Get(holderRole) is not { } holder || !holder.IsAlive()) return;
        kefka?.CastFromHelper(UmadActions.FlagrantFireStack, holder);
    }

    private void EndP1()
    {
        // A failed set doesn't end P1, so Kefka stays targetable.
        if (!ArrowSoakFailed)
        {
            kefka?.SetTargetable(false);
        }
        world.Obstacles.Clear();
        DiagnosticLog.Info("[UmadP1TeleTrouncing] P1 complete -- boss untargetable, mechanic finished.");
    }

    private void DespawnGazeProps()
    {
        gazeStatueNormal?.Despawn();
        gazeStatueNormal = null;
        gazeStatueInverted?.Despawn();
        gazeStatueInverted = null;
        gazeAnimStatueNormal?.Despawn();
        gazeAnimStatueNormal = null;
        gazeAnimStatueInverted?.Despawn();
        gazeAnimStatueInverted = null;
        foreach (var prop in earlierStatues) prop.Despawn();
        earlierStatues.Clear();
        foreach (var statue in tetherStatues.Values) statue.Despawn();
        tetherStatues.Clear();
    }

    private static ushort PrimaryStatusId(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpPrimary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownPrimary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightPrimary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftPrimary,
        _ => Constants.StatusId.TelePortentUpPrimary,
    };

    private static ushort SecondaryStatusId(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpSecondary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownSecondary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightSecondary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftSecondary,
        _ => Constants.StatusId.TelePortentUpSecondary,
    };
}
