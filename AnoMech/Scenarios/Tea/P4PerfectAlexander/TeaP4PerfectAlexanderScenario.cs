using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

// Perfect Alexander from just before it turns targetable to its death, at the slowest real kill so
// every mechanic shows up; a practice runs one of its mechanics alone. Scenario time 0 is 12s before
// The Final Word.
public sealed class TeaP4PerfectAlexanderScenario : IMultiplayerReplayable
{
    // The engine builds a spawned actor up to ~0.3s later and drops an instant cast made before then.
    private const float HelperLead = 1f;
    private const float HelperLinger = 1.5f;
    // An ActorControl or status sent before the engine created the actor is dropped.
    private const float SpawnSettles = 0.5f;
    private const float AutoEvery = 3.03f;
    private const float ProvokeTurnSeconds = 0.65f;
    private const float FinalWordSeconds = 10f;
    private const float OrdainedSeconds = 5f;
    private const float MarchLength = 20f;
    private const float MarchSpeed = 5f;
    private const float MarchAfterOrdained = 0.8f;
    // A dark follower carried further from its beacon than this loses its status at once and is
    // struck down EscapeStrike later.
    private const float EscapeLimit = 24f;
    private const float EscapeStrike = 0.12f;
    // A light follower this close to its beacon while marching dies. UNVERIFIED: between about 1.7y
    // and 2.3y.
    private const float ContactReach = 2f;
    // Moving: shifted this far within the last MotionWindow before the check.
    private const float MotionWindow = 0.3f;
    private const float MotionShift = 0.05f;
    // Stillness fails a step, a turn, a press, a cast bar or an auto-attack swing in its last
    // StillnessWindow. UNVERIFIED: where the window opens, between 2.06s and 1.5s before the check.
    private const float StillnessWindow = 2f;
    private const float StillnessTurn = 0.035f;
    private const float TrackSeconds = 3f;
    private const float CloneSpeed = 6f;
    // A projection's death follows its fate's fx by this long; the beacon pair's sooner.
    private const float ProjectionDies = 0.6f;
    private const float BeaconProjectionDies = 0.37f;
    private const float HouseArrestLimit = 5f;
    private const float RestrainingOrderMinimum = 30f;
    private const float JumpLands = 0.9f;
    // UNVERIFIED: how far a jail reaches.
    private const float JailRadius = 4f;
    private const float JailShown = 8f;
    private const float CorpseFade = 8.12f;
    // The P4 track has played this long by scenario start.
    private const float BgmAtStart = 49.94f;

    private static readonly Vector3 CloneHome = new(-0.02f, 0f, -3.75f);

    // The four Perfect Alexander clones, west to east, facing the centre.
    private static readonly Placement[] CloneSpots =
    [
        new(new Vector3(-21.71f, 0f, -8.99f), 1.178f),
        new(new Vector3(-8.99f, 0f, -21.71f), 0.393f),
        new(new Vector3(8.99f, 0f, -21.71f), -0.393f),
        new(new Vector3(21.71f, 0f, -8.99f), -1.178f),
    ];

    // Fate Calibration α's projection paths: where each runs to, then where it waits, and whether it
    // sets off later. Real paths change from pull to pull, so these, one pull's, are dealt out anew
    // each run. UNVERIFIED: how retail deals them; its Defamation projection ends NNE more often.
    private static readonly (Vector2 First, Vector2 Final, bool Late)[] AlphaPaths =
    [
        (new(-9.15f, -3.10f), new(-9.24f, -3.84f), false),
        (new(6.88f, -3.10f), new(3.85f, 9.13f), true),
        (new(-8.66f, 3.00f), new(9.23f, 3.82f), true),
        (new(7.36f, 3.00f), new(-3.84f, 9.23f), true),
        (new(-8.69f, -0.14f), new(9.23f, -3.84f), false),
        (new(7.30f, -0.20f), new(-9.24f, 3.82f), false),
        (new(-7.53f, 0.38f), new(3.82f, -9.24f), true),
        (new(8.46f, 0.38f), new(-3.84f, -9.24f), false),
    ];

    // Fate Calibration β's projections by BetaRole with Optical Sight on the third clone: the first stop,
    // the colour spot (from the later departure when Late), and where the demo march takes the followers.
    private static readonly (Vector2 First, Vector2 Colour, bool Late, Vector2? Marched)[] BetaPaths =
    [
        (new(-5.33f, 5.75f), new(-0.11f, 9.93f), true, new Vector2(17.47f, 7.27f)),
        (new(8.83f, 5.75f), new(-7.07f, 7.06f), true, new Vector2(-15.28f, 1.11f)),
        (new(-7.25f, 7.94f), new(7.06f, -7.07f), false, new Vector2(17.50f, 7.21f)),
        (new(6.88f, 7.94f), new(7.06f, 7.06f), true, new Vector2(6.66f, -10.40f)),
        (new(-10.34f, 7.33f), new(-9.24f, -3.84f), true, new Vector2(12.98f, 5.26f)),
        (new(3.79f, 7.36f), new(4.62f, 16.43f), false, new Vector2(-17.54f, 7.27f)),
        (new(-6.52f, 7.52f), new(17.56f, 7.27f), false, null),
        (new(7.61f, 7.52f), new(7.27f, 17.56f), true, null),
    ];
    private static readonly Vector2 LightBeaconCloneSteps = new(-5.19f, -2.13f);
    private static readonly Vector2 DarkBeaconCloneSteps = new(-2.13f, -5.19f);

    // Almighty Judgment's fixed grid: the centre's set, waymark 4's, waymark 3's.
    private static readonly Vector2[][] TileSets =
    [
        [new(-16, -16), new(16, -16), new(8, -8), new(-8, 0), new(0, 0), new(8, 0), new(-8, 8), new(-16, 16), new(16, 16)],
        [new(-8, -16), new(8, -16), new(0, -8), new(16, -8), new(-16, 0), new(8, 8), new(16, 8), new(0, 16)],
        [new(0, -16), new(-16, -8), new(-8, -8), new(16, 0), new(-16, 8), new(0, 8), new(-8, 16), new(8, 16)],
    ];

    // UNVERIFIED: the jails land NE first and on clockwise, 12y out, each shown 8s before.
    private static readonly Vector2[] JailSpots =
        Enumerable.Range(0, 7).Select(k => Flat(Geometry.OnCompass(45f + 45f * k, 12f))).ToArray();

    // UNVERIFIED: the arena piece's beats for a jail about to land, and landed.
    private const uint JailShownState = 0x400;
    private const uint JailShownMask = 0x840;
    private const uint JailLandedState = 0x1000;
    private const uint JailLandedMask = 0x2000;

    // Helpers' hits play these gimmick timelines, which an actor drops unless already loaded.
    // UNVERIFIED in game.
    private static readonly (ushort Id, string Key)[] HelperTimelines =
    [
        (7562, "mon_sp/gimmick/d2az_boss_gimmick01"),
        (7564, "mon_sp/gimmick/d2az_boss_gimmick03"),
        (7565, "mon_sp/gimmick/d2az_boss_gimmick04"),
        (3638, "mon_sp/gimmick/baha_bahamut_lenge_gt"),
        (5037, "mon_sp/gimmick/d2ac2_boss_gimmick10"),
        (3607, "mon_sp/gimmick/baha_quarl_blasta_hatudou"),
        (5021, "mon_sp/gimmick/d2ac2_boss_gimmick02"),
        (5025, "mon_sp/gimmick/d2ac2_boss_gimmick04"),
        (5026, "mon_sp/gimmick/d2ac2_boss_gimmick04_hit"),
        (5027, "mon_sp/gimmick/d2ac2_boss_gimmick05"),
        (5028, "mon_sp/gimmick/d2ac2_boss_gimmick05_hit"),
        (5029, "mon_sp/gimmick/d2ac2_boss_gimmick06"),
        (5030, "mon_sp/gimmick/d2ac2_boss_gimmick06_hit"),
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),
    ];

    public string Name => "Perfect Alexander";
    public IPhase Phase => TeaZone.P4;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaP4PerfectAlexanderAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    public float BgmSecondsAtStart => BgmAtStart + Practiced.Start;

    private readonly TeaP4PerfectAlexanderSettingsWindow settingsWindow = new();
    private P4PracticeEntry Practiced => TeaP4PerfectAlexanderPractice.Of(settingsWindow.Overrides.Practice);

    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaP4PerfectAlexanderState state = null!;
    private Rng autoVariation = Rng.Detached;
    private SimEnemy? perfectAlexander;
    private readonly SimEnemy?[] clones = new SimEnemy?[4];
    private readonly Dictionary<PartyRole, SimEnemy> projections = new();
    private readonly Dictionary<SimEnemy, List<SimTether>> projectionTethers = new();
    private readonly List<SimTether> playerTethers = [];
    private readonly List<SimEnemy> helpers = [];
    private readonly Dictionary<SimEnemy, float> helperFreeAt = new();
    private readonly SimEnemy?[] tileHelpers = new SimEnemy?[25];
    private (PartyRole Beacon, IReadOnlyList<PartyRole> Followers)? contactWatch;
    private readonly HashSet<PartyRole> touchedBeacon = [];
    private (PartyRole Beacon, IReadOnlyList<PartyRole> Followers)? escapeWatch;
    private readonly HashSet<PartyRole> escaped = [];
    private readonly Dictionary<PartyRole, (int Actions, int Swings)> actionCounts = new();
    // Ordained Punishment's target and when its vulnerability runs out.
    private (SimCharacter Target, float Until)? punishment;
    private readonly List<(PartyRole Beacon, IReadOnlyList<PartyRole> Followers, EnemyAction Retribution)> beaconBonds = [];
    private readonly List<SimCharacter> marchers = [];
    private readonly Dictionary<PartyRole, List<(float At, Vector3 Position, float Rotation, bool Acted)>> tracks = new();
    private readonly Vector3[] jumpLandings = new Vector3[4];
    private PartyRole tank;
    private bool headingHeld;
    private bool killed;

    public TeaP4PerfectAlexanderState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaP4PerfectAlexanderState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        autoVariation = world.Stream("tea-p4-autos");
        perfectAlexander = null;
        Array.Clear(clones);
        projections.Clear();
        projectionTethers.Clear();
        playerTethers.Clear();
        helpers.Clear();
        helperFreeAt.Clear();
        Array.Clear(tileHelpers);
        contactWatch = null;
        touchedBeacon.Clear();
        escapeWatch = null;
        escaped.Clear();
        actionCounts.Clear();
        punishment = null;
        beaconBonds.Clear();
        marchers.Clear();
        tracks.Clear();
        tank = PartyRole.MainTank;
        headingHeld = false;
        killed = false;
        var practice = TeaP4PerfectAlexanderPractice.Of(state.Practice);
        DiagnosticLog.Info($"[TeaP4PerfectAlexander] Roll: practice {practice.Label}"
                           + $"; light beacon {state.LightBeacon} + {string.Join(" ", state.LightFollowers)}, "
                           + $"dark beacon {state.DarkBeacon} + {string.Join(" ", state.DarkFollowers)}, {(state.FinalWordMotionFirst ? "Motion" : "Stillness")} first, "
                           + $"{(state.OpticalSpreadFirst ? "spread" : "stack")} first, stacks {string.Join(" ", state.OpticalStackTargets)}; "
                           + $"α {string.Join(" ", state.AlphaClones)} {(state.AlphaFirstMotion ? "M" : "S")}{(state.AlphaSecondMotion ? "M" : "S")} safe P{state.AlphaSafeClone + 1} "
                           + $"paths {string.Join("", state.AlphaPathOrder)}; "
                           + $"β {string.Join(" ", state.BetaClones)} {(state.BetaOpticalStack ? "stack" : "spread")} on P{state.BetaOpticalClone + 1}, donut P{state.BetaDonutClone + 1}; "
                           + $"judgments {string.Join("/", state.JudgmentOrders.Select(o => string.Join("", o)))}, grace {string.Join(" ", state.GraceTargets)}; "
                           + $"bots {state.BotSeed}.");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaP4PerfectAlexanderState>)AiStrats[idx]).Run(state, world);

        if (practice.Start > 0f) At(practice.Start, PickUpAtPractice);
        else
        {
            At(0f, () => SpawnPerfectAlexander(targetable: false));
            At(TeaP4PerfectAlexanderState.TargetableAt, PerfectAlexanderTurnsTargetable);
        }
        for (var t = 6.83f; t < TeaP4PerfectAlexanderState.KillAt; t += AutoEvery) At(t, AutoAttack);

        // The Final Word.
        Helpers(16.80f, 34.32f, 8);
        At(12.00f, () => BossCast(TeaActions.TheFinalWord));
        At(16.80f, GiveFinalWords);
        At(19.15f, () => BossCast(state.FinalWordMotionFirst ? TeaActions.OrdainedMotion : TeaActions.OrdainedStillness));
        At(23.21f, () => JudgeOrdained(state.FinalWordMotionFirst));
        At(26.61f, OrdainFinalWords);
        At(27.41f, () => March(state.LightFollowers, state.LightBeacon, state.DarkFollowers, state.DarkBeacon));
        At(30.26f, () => BossCast(state.FinalWordMotionFirst ? TeaActions.OrdainedStillness : TeaActions.OrdainedMotion));
        At(31.61f, EndOrdained);
        At(34.32f, () => JudgeOrdained(!state.FinalWordMotionFirst));

        // Optical Sight.
        Helpers(46.57f, 52.64f, 8);
        At(41.48f, () => OpticalSight(state.OpticalSpreadFirst));
        At(46.57f, () => Reprobations(state.OpticalSpreadFirst, state.OpticalStackTargets));
        At(47.56f, () => OpticalSight(!state.OpticalSpreadFirst));
        At(52.64f, () => Reprobations(!state.OpticalSpreadFirst, state.OpticalStackTargets));

        // Fate Projection α and Fate Calibration α.
        Helpers(74.87f, 103.04f, 12);
        At(57.79f, Leap);
        At(60.36f, FaceSouth);
        At(60.58f, () =>
        {
            world.Map.BattleTalk(BNpcNameId.PerfectAlexander, BattleTalkId.FateProjectionAlpha, 4000);
            BossCast(TeaActions.FateProjectionAlpha);
        });
        At(66.71f, ShowClones);
        At(66.98f, () => SpawnProjections(beta: false));
        At(66.98f + SpawnSettles, TetherProjections);
        At(68.73f, () => BossCast(TeaActions.FateCalibrationAlpha));
        At(69.77f, () => MoveAlphaProjections(final: false, late: false));
        At(69.77f, () => MoveAlphaProjections(final: false, late: true));
        At(73.81f, () => SafeClone?.Cast(TeaActions.AlphaFirstOrdainedTell));
        At(73.85f, () => MoveAlphaProjections(final: true, late: false));
        At(73.93f, () => MoveAlphaProjections(final: true, late: true));
        At(74.87f, () => ShowOnAlphaProjections(_ => true, state.AlphaFirstMotion ? ActionId.AlphaMotionShown : ActionId.AlphaStillnessShown, kill: false));
        At(80.83f, () => ShowAlphaDefamation());
        At(81.37f, () => ShowOnAlphaProjections(s => s == AlphaSentence.SharedSentence, ActionId.SharedSentenceShown, kill: true));
        At(81.91f, () => ShowOnAlphaProjections(s => s == AlphaSentence.AggravatedAssault, ActionId.AlphaAggravatedShown, kill: true));
        At(81.91f, () => SafeClone?.Cast(TeaActions.AlphaSecondOrdainedTell));
        At(83.68f, () => ShowOnAlphaProjections(s => s is AlphaSentence.Defamation or AlphaSentence.Nothing,
            state.AlphaSecondMotion ? ActionId.AlphaMotionKill : ActionId.AlphaStillnessKill, kill: true));
        At(83.91f, () => UnsafeClone(0)?.Cast(ActionId.AlphaSacramentShown));
        At(84.45f, () => UnsafeClone(1)?.Cast(ActionId.AlphaSacramentShown));
        At(84.98f, () => UnsafeClone(2)?.Cast(ActionId.AlphaSacramentShown));
        At(88.97f, () => PlayOnClones(ActionTimelineId.WarpStart));
        At(90.71f, ReturnClones);
        At(91.81f, EndProjectionLook);
        At(93.72f, PerfectAlexanderLeaves);
        At(97.90f, () => SafeClone?.Cast(state.AlphaFirstMotion ? ActionId.ResolveOrdainedMotion : ActionId.ResolveOrdainedStillness));
        At(97.97f, () => JudgeOrdained(state.AlphaFirstMotion));
        At(100.89f, () => PlaintOn(AlphaHolders(AlphaSentence.Defamation), TeaActions.PlaintOfObloquy));
        At(101.43f, () => PlaintOn(AlphaHolders(AlphaSentence.SharedSentence), TeaActions.PlaintOfSolidarity));
        At(101.96f, () => SafeClone?.Cast(state.AlphaSecondMotion ? ActionId.ResolveOrdainedMotion : ActionId.ResolveOrdainedStillness));
        At(101.97f, () => PlaintOn(AlphaHolders(AlphaSentence.AggravatedAssault), TeaActions.PlaintOfSeverity));
        At(101.97f, () => UnsafeClone(0)?.Cast(TeaActions.Sacrament));
        At(102.00f, () => JudgeOrdained(state.AlphaSecondMotion));
        At(102.50f, () => UnsafeClone(1)?.Cast(TeaActions.Sacrament));
        At(103.04f, () => UnsafeClone(2)?.Cast(TeaActions.Sacrament));
        At(107.04f, () => PlayOnClones(ActionTimelineId.WarpStart));
        At(107.90f, PerfectAlexanderReturns);

        // Ordained Capital Punishment 1.
        CapitalPunishment(113.21f);

        // Fate Projection β and Fate Calibration β.
        Helpers(155.58f, 197.90f, 10);
        At(132.67f, Leap);
        At(135.25f, FaceSouth);
        At(135.46f, () => BossCast(TeaActions.FateProjectionBeta));
        At(141.59f, ShowClones);
        At(141.72f, () => SpawnProjections(beta: true));
        At(141.72f + SpawnSettles, TetherProjections);
        At(143.61f, () => BossCast(TeaActions.FateCalibrationBeta));
        At(144.73f, () => MoveBetaProjections(leg: 0, late: false));
        At(148.68f, () => MoveBetaProjections(leg: 1, late: false));
        At(148.80f, () => MoveBetaProjections(leg: 1, late: true));
        At(153.58f, ColourBetaProjections);
        At(153.71f, () => MoveBetaProjections(leg: 2, late: false));
        At(153.84f, () => MoveBetaProjections(leg: 2, late: true));
        At(155.58f, () => ShowOnBetaProjection(BetaRole.DarkFar, ActionId.BetaDarkTetheredKill));
        At(156.73f, () => ShowOnBetaProjection(BetaRole.DarkNear, ActionId.BetaDarkTetheredKill));
        At(156.76f, () => ShowOnBetaProjection(BetaRole.LightNear, ActionId.BetaLightTetheredKill));
        At(156.76f, () => ShowOnBetaProjection(BetaRole.LightFar, ActionId.BetaLightTetheredKill));
        At(157.80f, () => ShowOnBetaProjection(BetaRole.LightShared, ActionId.SharedSentenceShown));
        At(158.77f, JumpOntoProjections);
        At(158.77f + JumpLands, LandJumps);
        At(159.79f, () => DefeatProjection(BetaRole.DarkUntethered));
        At(164.29f, () => clones[state.BetaOpticalClone]?.Cast(state.BetaOpticalStack ? ActionId.BetaStackShown : ActionId.BetaSpreadShown));
        At(165.77f, BetaBeaconsStepIn);
        At(166.38f, () =>
        {
            var kill = state.BetaOpticalStack ? ActionId.BetaStackKill : ActionId.BetaSpreadKill;
            ShowOnBetaProjection(BetaRole.LightBeacon, kill, BeaconProjectionDies);
            ShowOnBetaProjection(BetaRole.DarkBeacon, kill, BeaconProjectionDies);
        });
        At(169.81f, () => clones[state.BetaDonutClone]?.Cast(ActionId.BetaDonutShown));
        At(174.77f, () => PlayOnClones(ActionTimelineId.WarpStart));
        At(178.60f, ReturnClones);
        At(179.79f, EndProjectionLook);
        At(181.69f, PerfectAlexanderLeaves);
        At(183.73f, OrdainBeta);
        At(184.53f, () => March(BetaFollowers(light: true), state.BetaHolder(BetaRole.LightBeacon),
            BetaFollowers(light: false), state.BetaHolder(BetaRole.DarkBeacon)));
        At(188.73f, EndOrdained);
        At(190.70f, ReleaseBetaTethers);
        At(190.79f, JudgeBetaTethers);
        At(190.79f, () => PlaintOn([state.BetaHolder(BetaRole.LightShared)], TeaActions.PlaintOfSolidarity));
        At(191.79f, JJumps);
        At(191.79f + JumpLands, LandJumps);
        At(195.80f, () => clones[state.BetaOpticalClone]?.Cast(state.BetaOpticalStack ? ActionId.ResolveOpticalSightStack : ActionId.ResolveOpticalSightSpread));
        At(197.90f, () => Reprobations(!state.BetaOpticalStack, [state.BetaHolder(BetaRole.LightBeacon), state.BetaHolder(BetaRole.DarkBeacon)]));
        At(202.82f, () => clones[state.BetaDonutClone]?.Cast(TeaActions.RadiantSacrament));
        At(206.82f, () => PlayOnClones(ActionTimelineId.WarpStart));
        At(207.87f, PerfectAlexanderReturns);

        // Ordained Capital Punishment 2, Almighty Judgment 1, Ordained Capital Punishment 3.
        CapitalPunishment(213.15f);
        AlmightyJudgment(232.49f, 0);
        CapitalPunishment(258.82f);

        // Almighty Judgment 2 and the enrage after it.
        AlmightyJudgment(277.94f, 1);
        At(304.25f, Leap);
        At(306.86f, () => BossCast(TeaActions.TemporalInterference));
        var prison = TeaP4PerfectAlexanderState.TemporalPrisonCast;
        At(prison, () => BossCast(TeaActions.TemporalPrison));
        for (var k = 0; k < JailSpots.Length; k++)
        {
            var jail = k;
            var lands = prison + TeaP4PerfectAlexanderState.FirstJail + TeaP4PerfectAlexanderState.JailEvery * k;
            At(lands - JailShown, () => TeaZone.Instance.ArenaFeature(jail)?.PlayBeat(JailShownState, JailShownMask));
            At(lands, () => LandJail(jail));
        }

        At(TeaP4PerfectAlexanderState.KillAt, Kill);

        if (practice.Start > 0f) world.Events.Advance(practice.Start);
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(HelperTimelines, "TeaP4PerfectAlexander");

    // A stack taken alone sets off Faithlessness.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved is { Action.ActionId: ActionId.CollectiveReprobation, Hits.Count: < 2, Caster: SimEnemy helper })
            helper.Cast(TeaActions.Faithlessness);
    }

    public void Tick(float delta, float elapsed)
    {
        if (state is null) return;
        PunishFollowersOfFallenBeacons();
        PunishContact();
        PunishEscapes();
        var now = world.Events.Elapsed;
        foreach (var role in PerRole.All)
        {
            if (party.Get(role) is not { } member) continue;
            if (!tracks.TryGetValue(role, out var track)) tracks[role] = track = [];
            track.Add((now, member.Position, member.Rotation, ActedSinceLastTick(role, member)));
            while (track.Count > 0 && track[0].At < now - TrackSeconds) track.RemoveAt(0);
        }
    }

    // A press, a cast bar, or an action or auto-attack swing let go since the last tick; an earlier
    // action's lock playing out is not.
    private bool ActedSinceLastTick(PartyRole role, SimCharacter member)
    {
        var counts = (member.Casting.ActionsSent, (member as SimPlayer)?.AutoAttackSwings ?? 0);
        var released = actionCounts.TryGetValue(role, out var last) && last != counts;
        actionCounts[role] = counts;
        return released || member.Casting.IsCasting || member is SimPlayer { IsMoving: true };
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new TeaP4PerfectAlexanderAiReplayStateMessage(s.Practice, s.LightBeacon, s.DarkBeacon, s.LightFollowers.ToArray(),
                s.FinalWordMotionFirst, s.OpticalSpreadFirst, s.OpticalStackTargets.ToArray(), s.AlphaClones.ToArray(), s.AlphaFirstMotion,
                s.AlphaSecondMotion, s.AlphaSafeClone, s.BetaClones.ToArray(), s.BetaOpticalStack, s.BetaOpticalClone, s.BetaDonutClone,
                s.JudgmentOrders.Select(o => o.ToArray()).ToArray(), s.GraceTargets.ToArray(), s.BotSeed)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaP4PerfectAlexanderAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaP4PerfectAlexanderState.FromNetworkReplay(msg.Practice, msg.LightBeacon, msg.DarkBeacon, msg.LightFollowers,
                msg.FinalWordMotionFirst, msg.OpticalSpreadFirst, msg.OpticalStackTargets, msg.AlphaClones, msg.AlphaFirstMotion,
                msg.AlphaSecondMotion, msg.AlphaSafeClone, msg.BetaClones, msg.BetaOpticalStack, msg.BetaOpticalClone, msg.BetaDonutClone, msg.JudgmentOrders,
                msg.GraceTargets, msg.BotSeed) is not { } shadowState) return null;
        ((IScenarioAi<TeaP4PerfectAlexanderState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Perfect Alexander spawns first and its four clones after it, west to east.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaP4PerfectAlexanderState shadow) return;
        shadow.Objects.Projections.RemoveAll(p => !p.IsActive);
        foreach (var projection in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.FateProjection && e.IsActive))
            if (!shadow.Objects.Projections.Contains(projection)) shadow.Objects.Projections.Add(projection);
        var alexanders = peerEnemies.OrderBy(kv => kv.Key).Select(kv => kv.Value).Where(e => e.BNpcBaseId == BNpcBaseId.PerfectAlexander).ToList();
        if (alexanders.Count == 0) return;
        shadow.Objects.PerfectAlexander ??= alexanders[0];
        for (var k = 0; k < 4 && k + 1 < alexanders.Count; k++) shadow.Objects.Clones[k] ??= alexanders[k + 1];
    }

    // -- Timeline plumbing -------------------------------------------------------------

    private void At(float time, Action action, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP4PerfectAlexanderPractice.Covers(state.Practice, time)) world.Events.Add(time, action, file, line);
    }

    private void Helpers(float firstUse, float lastUse, int count)
    {
        At(firstUse - HelperLead, () => SpawnHelpers(count));
        At(lastUse + HelperLinger, DespawnHelpers);
    }

    private void SpawnHelpers(int count)
    {
        for (var i = 0; i < count; i++)
            if (SpawnHelper() is { } helper) helpers.Add(helper);
    }

    private SimEnemy? SpawnHelper()
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.PerfectAlexander, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(Vector3.Zero, 0f)));

    private void DespawnHelpers()
    {
        foreach (var helper in helpers) helper.Despawn();
        helpers.Clear();
        helperFreeAt.Clear();
    }

    // A helper nothing else used this moment, moved onto `at`.
    private SimEnemy? Helper(Vector3 at)
    {
        var now = world.Events.Elapsed;
        if (helpers.FirstOrDefault(h => h.IsActive && helperFreeAt.GetValueOrDefault(h) <= now) is not { } helper)
        {
            DiagnosticLog.Warn($"[TeaP4PerfectAlexander] No free helper at {now:F2}s.");
            return null;
        }
        helperFreeAt[helper] = now + 0.3f;
        helper.SetPosition(new Placement(at with { Y = 0f }, 0f));
        return helper;
    }

    private SimCharacter? Alive(PartyRole role) => party.Get(role) is { } member && member.IsAlive() ? member : null;

    private List<SimCharacter> Living() => PerRole.All.Select(Alive).OfType<SimCharacter>().ToList();

    // -- Perfect Alexander -------------------------------------------------------------

    private SimEnemy? SpawnPerfectAlexander(bool targetable)
    {
        state.Objects.PerfectAlexander = perfectAlexander = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.PerfectAlexander, NameId: BNpcNameId.PerfectAlexander, Level: Level,
            Targetable: targetable, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, 0f)));
        if (perfectAlexander is { } boss)
        {
            boss.SetVisibleInEnemyList(targetable);
            boss.Provoked += OnProvoked;
        }
        for (var k = 0; k < clones.Length; k++)
            state.Objects.Clones[k] = clones[k] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.PerfectAlexander, NameId: BNpcNameId.PerfectAlexander, Level: Level,
                Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.HiddenUntilShown,
                Placement: new Placement(CloneHome, 0f)));
        return perfectAlexander;
    }

    private void PerfectAlexanderTurnsTargetable()
    {
        if (perfectAlexander is not { } boss) return;
        boss.SetTargetable(true);
        boss.SetVisibleInEnemyList(true);
        SetTank(tank);
    }

    // As the phase would leave it: whoever holds it after the Ordained Punishments so far, and the
    // last one's vulnerability still on its target.
    private void PickUpAtPractice()
    {
        var start = TeaP4PerfectAlexanderPractice.Of(state.Practice).Start;
        var punished = PunishmentsBefore(start);
        tank = punished % 2 == 0 ? PartyRole.MainTank : PartyRole.OffTank;
        SpawnPerfectAlexander(targetable: true);
        world.Events.Add(SpawnSettles, () => SetTank(tank));
        world.Events.Add(1f, () =>
        {
            for (var i = 0; i < 8; i++) TeaZone.Instance.ArenaFeature(i)?.PlayBeat(1, 2);
        });
        if (punished > 0 && start - PunishmentLands[punished - 1] < PunishedSeconds
            && Alive(punished % 2 == 1 ? PartyRole.MainTank : PartyRole.OffTank) is { } punishedTank)
        {
            punishedTank.AddStatus(StatusId.PhysicalVulnerabilityUp, PunishedSeconds - (start - PunishmentLands[punished - 1]));
            punishment = (punishedTank, PunishmentLands[punished - 1] + PunishedSeconds);
        }
    }

    private static readonly float[] PunishmentLands = [125.37f, 225.30f, 270.98f];
    private const float PunishedSeconds = 30f;

    private static int PunishmentsBefore(float time) => PunishmentLands.Count(t => t < time);

    private SimCharacter? TankMember
        => Alive(tank) ?? Alive(tank == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank);

    private void SetTank(PartyRole role)
    {
        tank = role;
        if (perfectAlexander is not { } boss || TankMember is not { } member) return;
        boss.SetTarget(member, follow: false);
        if (!headingHeld) boss.KeepFacing(member);
    }

    private void OnProvoked(SimCharacter by)
    {
        if (by is not ISimPartyMember { } member) return;
        world.Events.Add(ProvokeTurnSeconds, () =>
        {
            if (by.IsAlive()) SetTank(member.Role);
        });
    }

    // Only while it is free and its target in reach. A tank it still holds through Ordained
    // Punishment's vulnerability, the swap missed, dies to it.
    private void AutoAttack()
    {
        if (perfectAlexander is not { IsActive: true, Dead: false, Targetable: true, IsCasting: false, AnimationLock: false } boss) return;
        if (TankMember is not { } target) return;
        if (Vector2.Distance(Flat(boss.Position), Flat(target.Position)) > boss.HitboxRadius + target.HitboxRadius + Geometry.EnemyReach) return;
        var unswapped = punishment is { } p && p.Target == target && world.Events.Elapsed < p.Until && target.HasStatus(StatusId.PhysicalVulnerabilityUp);
        boss.Cast(unswapped ? TeaActions.PerfectAlexanderAttackUnswapped : TeaActions.PerfectAlexanderAttack, target, (byte)autoVariation.NextInt(3));
    }

    private void BossCast(EnemyAction action)
    {
        if (perfectAlexander is { IsActive: true, Dead: false } boss) boss.Cast(action);
    }

    private void Leap()
    {
        if (perfectAlexander is { IsActive: true, Dead: false } boss) boss.Cast(TeaActions.FateProjectionLeap, Vector3.Zero);
    }

    // It turns south for each projection, and back to its target once it returns.
    private void FaceSouth()
    {
        headingHeld = true;
        perfectAlexander?.KeepFacing(null);
        perfectAlexander?.TurnTo(0f);
    }

    private void PerfectAlexanderLeaves()
    {
        if (perfectAlexander is not { } boss) return;
        boss.SetTargetable(false);
        boss.SetVisibleInEnemyList(false);
        boss.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    private void PerfectAlexanderReturns()
    {
        if (perfectAlexander is not { } boss) return;
        boss.PlayActionTimeline(ActionTimelineId.WarpEnd);
        boss.SetTargetable(true);
        boss.SetVisibleInEnemyList(true);
        headingHeld = false;
        SetTank(tank);
    }

    private void Kill()
    {
        killed = true;
        if (perfectAlexander is { IsActive: true } boss)
        {
            boss.SetHpFraction(0f);
            boss.Defeat(corpseFadeDelay: CorpseFade);
        }
        world.Map.AddEffect(0x00080004, 1);
        world.Map.AddEffect(0x00010001, 5);
        world.Map.AddEffect(0x00040004, 4);
        world.Map.AddEffect(0x00040004, 6);
        for (var i = 0; i < 8; i++) TeaZone.Instance.ArenaFeature(i)?.PlayBeat(1, 1);
        foreach (var member in Living()) member.RemoveStatus(StatusId.TemporalPrison);
        DiagnosticLog.Info($"[TeaP4PerfectAlexander] Perfect Alexander down at {world.Events.Elapsed:F2}s.");
        world.Events.Add(0.28f, () =>
        {
            DespawnHelpers();
            foreach (var helper in tileHelpers) helper?.Despawn();
            foreach (var clone in clones) clone?.Despawn();
            foreach (var projection in projections.Values) projection.Despawn();
            foreach (var tether in playerTethers.Concat(projectionTethers.Values.SelectMany(t => t))) tether.Despawn();
        });
    }

    // -- The Final Word ------------------------------------------------------------------

    private static ushort FinalWordStatus(FinalWordDebuff debuff) => debuff switch
    {
        FinalWordDebuff.LightBeacon => StatusId.FinalWordContactRegulation,
        FinalWordDebuff.LightFollower => StatusId.FinalWordContactProhibition,
        FinalWordDebuff.DarkBeacon => StatusId.FinalWordEscapeDetection,
        _ => StatusId.FinalWordEscapeProhibition,
    };

    private void GiveFinalWords()
    {
        foreach (var role in PerRole.All)
            Alive(role)?.AddStatus(FinalWordStatus(state.FinalWordOf(role)), FinalWordSeconds);
        BindFollowers(state.LightBeacon, state.LightFollowers, state.DarkBeacon, state.DarkFollowers);
    }

    // Until the march ends, a beacon going down takes its followers with it.
    private void BindFollowers(PartyRole lightBeacon, IReadOnlyList<PartyRole> lights, PartyRole darkBeacon, IReadOnlyList<PartyRole> darks)
    {
        beaconBonds.Clear();
        beaconBonds.Add((lightBeacon, lights, TeaActions.DivineRetributionLight));
        beaconBonds.Add((darkBeacon, darks, TeaActions.DivineRetributionDark));
    }

    private void PunishFollowersOfFallenBeacons()
    {
        for (var i = beaconBonds.Count - 1; i >= 0; i--)
        {
            var (beacon, followers, retribution) = beaconBonds[i];
            if (Alive(beacon) is not null) continue;
            beaconBonds.RemoveAt(i);
            foreach (var role in followers)
                if (Alive(role) is { } member)
                    Helper(member.Position)?.Cast(retribution with { DeathExplanation = "their beacon fell" }, member);
        }
    }

    // The Final Word's statuses turn into the Ordained ones that hold the beacons and march the
    // followers.
    private void OrdainFinalWords()
    {
        foreach (var role in PerRole.All)
            Alive(role)?.RemoveStatus(FinalWordStatus(state.FinalWordOf(role)));
        Ordain([state.LightBeacon], state.LightFollowers, [state.DarkBeacon], state.DarkFollowers);
    }

    private void Ordain(IReadOnlyList<PartyRole> lightBeacons, IReadOnlyList<PartyRole> lights, IReadOnlyList<PartyRole> darkBeacons, IReadOnlyList<PartyRole> darks)
    {
        var lightBeacon = lightBeacons.Select(Alive).FirstOrDefault(m => m is not null);
        var darkBeacon = darkBeacons.Select(Alive).FirstOrDefault(m => m is not null);
        lightBeacon?.AddStatus(StatusId.ContactRegulationOrdained, OrdainedSeconds);
        darkBeacon?.AddStatus(StatusId.EscapeDetectionOrdained, OrdainedSeconds);
        contactWatch = (lightBeacons[0], lights);
        touchedBeacon.Clear();
        escapeWatch = (darkBeacons[0], darks);
        escaped.Clear();
        marchers.Clear();
        foreach (var role in lights)
            if (Alive(role) is { } member)
            {
                member.AddStatus(StatusId.ContactProhibitionOrdained, OrdainedSeconds, sourceObject: lightBeacon?.GameObjectId ?? default);
                member.ActorControl.Send(MarchControl.Category, MarchControl.Contact);
                marchers.Add(member);
            }
        foreach (var role in darks)
            if (Alive(role) is { } member)
            {
                member.AddStatus(StatusId.EscapeProhibitionOrdained, OrdainedSeconds, sourceObject: darkBeacon?.GameObjectId ?? default);
                member.ActorControl.Send(MarchControl.Category, MarchControl.Escape);
                marchers.Add(member);
            }
    }

    // Straight lines, fixed as they start: the light followers toward their beacon, the dark ones
    // directly away from theirs.
    private void March(IReadOnlyList<PartyRole> lights, PartyRole lightBeacon, IReadOnlyList<PartyRole> darks, PartyRole darkBeacon)
    {
        foreach (var role in lights) MarchFollower(role, Alive(lightBeacon), toward: true);
        foreach (var role in darks) MarchFollower(role, Alive(darkBeacon), toward: false);
    }

    private void MarchFollower(PartyRole role, SimCharacter? beacon, bool toward)
    {
        if (Alive(role) is not { } member || beacon is null) return;
        var offset = Flat(beacon.Position) - Flat(member.Position);
        var distance = offset.Length();
        var heading = distance > 1e-3f ? offset / distance : new Vector2(MathF.Sin(member.Rotation), MathF.Cos(member.Rotation));
        if (!toward) heading = -heading;
        member.MarchTo(member.Position + new Vector3(heading.X, 0f, heading.Y) * MarchLength, MarchSpeed);
    }

    private void PunishContact()
    {
        if (contactWatch is not { } watch || Alive(watch.Beacon) is not { } beacon) return;
        foreach (var role in watch.Followers)
            if (Alive(role) is { } member && Vector2.Distance(Flat(member.Position), Flat(beacon.Position)) < ContactReach && touchedBeacon.Add(role))
                Helper(member.Position)?.Cast(TeaActions.DivineRetributionLight with { DeathExplanation = "touched the Contact Regulation player" }, member);
    }

    private void PunishEscapes()
    {
        if (escapeWatch is not { } watch || Alive(watch.Beacon) is not { } beacon) return;
        foreach (var role in watch.Followers)
        {
            if (Alive(role) is not { } member || escaped.Contains(role)) continue;
            if (Vector2.Distance(Flat(member.Position), Flat(beacon.Position)) <= EscapeLimit) continue;
            escaped.Add(role);
            member.RemoveStatus(StatusId.EscapeProhibitionOrdained);
            if (marchers.Remove(member)) member.ActorControl.Send(MarchControl.Category, 0);
            world.Events.Add(EscapeStrike, () =>
            {
                if (member.IsAlive())
                    Helper(member.Position)?.Cast(TeaActions.DivineRetributionDark with { DeathExplanation = "marched too far from the Escape Detection player" }, member);
            });
        }
    }

    // As the Ordained statuses end: the marches' state is cleared, and the beacons let go.
    private void EndOrdained()
    {
        foreach (var member in marchers.Where(m => m.IsAlive())) member.ActorControl.Send(MarchControl.Category, 0);
        marchers.Clear();
        beaconBonds.Clear();
        contactWatch = null;
        escapeWatch = null;
    }

    // Motion wants everyone moving as it lands; Stillness wants nobody moving, turning or acting for a
    // while before.
    private void JudgeOrdained(bool motion)
    {
        foreach (var role in PerRole.All)
        {
            if (Alive(role) is not { } member) continue;
            var passes = motion ? Moving(role, member) : Still(role);
            DiagnosticLog.Info($"[TeaP4PerfectAlexander] Ordained {(motion ? "Motion" : "Stillness")}: {role} {(passes ? "passes" : "fails")}.");
            var verdict = motion
                ? passes ? TeaActions.OrdainedMotionSuccess : TeaActions.OrdainedMotionFail
                : passes ? TeaActions.OrdainedStillnessSuccess : TeaActions.OrdainedStillnessFail;
            Helper(member.Position)?.Cast(verdict, member);
        }
    }

    private bool Moving(PartyRole role, SimCharacter member)
        => member is SimPlayer { IsMoving: true } || Shift(role, MotionWindow) >= MotionShift;

    private bool Still(PartyRole role)
        => Shift(role, StillnessWindow) < MotionShift && Turn(role, StillnessWindow) < StillnessTurn && !Acted(role, StillnessWindow);

    private bool Acted(PartyRole role, float window)
        => tracks.TryGetValue(role, out var track) && track.Any(s => s.At >= world.Events.Elapsed - window && s.Acted);

    private float Shift(PartyRole role, float window)
    {
        if (!tracks.TryGetValue(role, out var track) || track.Count == 0) return 0f;
        var since = world.Events.Elapsed - window;
        var last = track[^1].Position;
        return track.Where(s => s.At >= since).Select(s => Vector3.Distance(s.Position, last)).DefaultIfEmpty(0f).Max();
    }

    private float Turn(PartyRole role, float window)
    {
        if (!tracks.TryGetValue(role, out var track) || track.Count == 0) return 0f;
        var since = world.Events.Elapsed - window;
        var last = track[^1].Rotation;
        return track.Where(s => s.At >= since).Select(s => MathF.Abs(MathF.IEEERemainder(s.Rotation - last, MathF.Tau))).DefaultIfEmpty(0f).Max();
    }

    // -- Optical Sight -----------------------------------------------------------------------

    private void OpticalSight(bool spread)
    {
        BossCast(spread ? TeaActions.OpticalSightSpread : TeaActions.OpticalSightStack);
        if (spread)
            foreach (var member in Living()) member.AttachLockonVfx(LockonId.OpticalSightSpread);
        else
            foreach (var role in state.OpticalStackTargets) Alive(role)?.AttachLockonVfx(LockonId.OpticalSightStack);
    }

    // Eight spreads land whoever is left, so with deaths someone takes two; the stacks go on their two.
    private void Reprobations(bool spread, IReadOnlyList<PartyRole> stackTargets)
    {
        if (spread)
        {
            var living = Living();
            if (living.Count == 0) return;
            var targets = living.Concat(Enumerable.Range(0, 8 - living.Count).Select(_ => state.PickRandom(living))).ToList();
            foreach (var target in targets) Helper(target.Position)?.Cast(TeaActions.IndividualReprobation, target);
            return;
        }
        foreach (var role in stackTargets)
            if (Alive(role) is { } target)
                Helper(target.Position)?.Cast(TeaActions.CollectiveReprobation, target);
    }

    // -- Fate Projections ------------------------------------------------------------------------

    private SimEnemy? SafeClone => clones[state.AlphaSafeClone];

    // The clones that fire Sacrament, west to east.
    private SimEnemy? UnsafeClone(int nth) => Enumerable.Range(0, 4).Where(k => k != state.AlphaSafeClone).Select(k => clones[k]).ElementAt(nth);

    private void ShowClones()
    {
        for (var k = 0; k < clones.Length; k++)
        {
            clones[k]?.SetPosition(CloneSpots[k]);
            clones[k]?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        }
        foreach (var slot in (byte[])[1, 5, 4]) world.Map.AddEffect(0x00800040, slot);
        world.Map.SetWeather(Weather.FateProjection);
        perfectAlexander?.SetModelState(4);
    }

    private void PlayOnClones(ushort timeline)
    {
        foreach (var clone in clones) clone?.PlayActionTimeline(timeline);
    }

    private void ReturnClones()
    {
        for (var k = 0; k < clones.Length; k++) clones[k]?.SetPosition(CloneSpots[k]);
        world.Events.Add(0.09f, () => PlayOnClones(ActionTimelineId.WarpEnd));
    }

    private void EndProjectionLook()
    {
        world.Map.AddEffect(0x00020001, 1);
        world.Map.AddEffect(0x00200001, 5);
        world.Map.AddEffect(0x00200001, 4);
        world.Map.SetWeather(Weather.FairSkiesP4);
        perfectAlexander?.SetModelState(0);
    }

    // Each wears its player's look, where its player stands, spawned in id order.
    private void SpawnProjections(bool beta)
    {
        projections.Clear();
        projectionTethers.Clear();
        state.Objects.Projections.Clear();
        foreach (var role in beta ? state.BetaClones : state.AlphaClones)
        {
            if (Alive(role) is not { } owner) continue;
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.FateProjection, NameId: BNpcNameId.FateProjection, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, DisplayFlags: 0x4020B,
                    IsHostile: false, Appearance: owner.Appearance, Placement: owner.Placement())) is { } projection)
            {
                projections[role] = projection;
                state.Objects.Projections.Add(projection);
            }
        }
    }

    private void TetherProjections()
    {
        foreach (var (role, projection) in projections)
            if (Alive(role) is { } owner && projection.IsAlive())
                Tethers(projection).Add(world.Tether(owner, projection, TetherId.FateProjection));
    }

    private List<SimTether> Tethers(SimEnemy projection)
    {
        if (!projectionTethers.TryGetValue(projection, out var list)) projectionTethers[projection] = list = [];
        return list;
    }

    private void DefeatProjection(SimEnemy projection)
    {
        if (!projection.IsAlive()) return;
        projection.Defeat();
        if (projectionTethers.Remove(projection, out var tethers))
            foreach (var tether in tethers) tether.Despawn();
    }

    private void DefeatProjection(BetaRole role)
    {
        if (projections.GetValueOrDefault(state.BetaHolder(role)) is { } projection) DefeatProjection(projection);
    }

    private static Vector3 Mirrored(Vector2 spot, bool mirror) => new(mirror ? -spot.X : spot.X, 0f, spot.Y);

    private void MoveAlphaProjections(bool final, bool late)
    {
        for (var slot = 0; slot < 8; slot++)
        {
            var path = AlphaPaths[state.AlphaPathOrder[slot]];
            if (final && path.Late != late || !final && late) continue;
            if (projections.GetValueOrDefault(state.AlphaClones[slot]) is { } projection && projection.IsAlive())
                projection.MoveTo(Mirrored(final ? path.Final : path.First, mirror: false), CloneSpeed);
        }
    }

    // A projection's fate shows on it: the requirement's effect on all, the sentence's on its owner's.
    private void ShowOnAlphaProjections(Func<AlphaSentence, bool> which, uint actionId, bool kill)
    {
        for (var slot = 0; slot < 8; slot++)
        {
            if (!which(TeaP4PerfectAlexanderState.AlphaSentenceOfSlot(slot))) continue;
            if (projections.GetValueOrDefault(state.AlphaClones[slot]) is not { } projection || !projection.IsAlive()) continue;
            Helper(projection.Position)?.Cast(actionId, projection);
            if (kill) world.Events.Add(ProjectionDies, () => DefeatProjection(projection));
        }
    }

    // A helper lands on the defamation projection, its effect around it.
    private void ShowAlphaDefamation()
    {
        var slot = Enumerable.Range(0, 8).First(s => TeaP4PerfectAlexanderState.AlphaSentenceOfSlot(s) == AlphaSentence.Defamation);
        if (projections.GetValueOrDefault(state.AlphaClones[slot]) is { } projection && projection.IsAlive())
            Helper(projection.Position)?.Cast(ActionId.AlphaDefamationShown);
    }

    private IReadOnlyList<PartyRole> AlphaHolders(AlphaSentence sentence)
        => Enumerable.Range(0, 8).Where(s => TeaP4PerfectAlexanderState.AlphaSentenceOfSlot(s) == sentence).Select(s => state.AlphaClones[s]).ToList();

    private void PlaintOn(IReadOnlyList<PartyRole> roles, EnemyAction plaint)
    {
        foreach (var role in roles)
            if (Alive(role) is { } member)
                Helper(member.Position)?.Cast(plaint, member);
    }

    private bool BetaMirrored => state.BetaOpticalClone == 1;

    private void MoveBetaProjections(int leg, bool late)
    {
        foreach (var role in Enum.GetValues<BetaRole>())
        {
            var path = BetaPaths[(int)role];
            if (projections.GetValueOrDefault(state.BetaHolder(role)) is not { } projection || !projection.IsAlive()) continue;
            switch (leg)
            {
                case 0 when !late:
                    projection.MoveTo(Mirrored(path.First, BetaMirrored), CloneSpeed);
                    break;
                case 1 when path.Late == late:
                    projection.MoveTo(Mirrored(path.Colour, BetaMirrored), CloneSpeed);
                    break;
                case 2 when path.Marched is { } marched && late == (role == BetaRole.DarkNear):
                    projection.MoveTo(Mirrored(marched, BetaMirrored), CloneSpeed);
                    break;
            }
        }
    }

    // Each projection shows its colour, and the near and far pairs their tethers.
    private void ColourBetaProjections()
    {
        foreach (var role in Enum.GetValues<BetaRole>())
            if (projections.GetValueOrDefault(state.BetaHolder(role)) is { } projection && projection.IsAlive())
                projection.AddStatusParam(StatusId.FateCalibrationBeta, IsLight(role) ? StatusParam.BetaLight : StatusParam.BetaDark, 4f);
        TetherBetaPair(BetaRole.DarkNear, BetaRole.LightNear, TetherId.HouseArrest);
        TetherBetaPair(BetaRole.DarkFar, BetaRole.LightFar, TetherId.RestrainingOrder);
    }

    private void TetherBetaPair(BetaRole from, BetaRole to, ushort tetherId)
    {
        if (projections.GetValueOrDefault(state.BetaHolder(from)) is not { } a || projections.GetValueOrDefault(state.BetaHolder(to)) is not { } b) return;
        var tether = world.Tether(a, b, tetherId);
        Tethers(a).Add(tether);
        Tethers(b).Add(tether);
    }

    private static bool IsLight(BetaRole role) => role is BetaRole.LightFar or BetaRole.LightNear or BetaRole.LightShared or BetaRole.LightBeacon;

    private void ShowOnBetaProjection(BetaRole role, uint actionId, float dies = ProjectionDies)
    {
        if (projections.GetValueOrDefault(state.BetaHolder(role)) is not { } projection || !projection.IsAlive()) return;
        Helper(projection.Position)?.Cast(actionId, projection);
        world.Events.Add(dies, () => DefeatProjection(projection));
    }

    // The three clones but the one with Optical Sight leap onto whatever projection stands farthest.
    private IEnumerable<int> Jumpers => Enumerable.Range(0, 4).Where(k => k != state.BetaOpticalClone);

    private void JumpOntoProjections()
    {
        foreach (var k in Jumpers)
        {
            if (clones[k] is not { } clone) continue;
            if (projections.Values.Where(p => p.IsAlive()).MaxBy(p => Vector2.Distance(Flat(p.Position), Flat(clone.Position))) is not { } farthest) continue;
            jumpLandings[k] = farthest.Position;
            clone.Cast(ActionId.BetaJumpShown, farthest.Position);
        }
    }

    private void LandJumps()
    {
        foreach (var k in Jumpers)
            if (clones[k] is { } clone && jumpLandings[k] != Vector3.Zero)
                clone.SetPosition(new Placement(jumpLandings[k], clone.Rotation));
        Array.Clear(jumpLandings);
    }

    private void BetaBeaconsStepIn()
    {
        foreach (var (role, step) in new[] { (BetaRole.LightBeacon, LightBeaconCloneSteps), (BetaRole.DarkBeacon, DarkBeaconCloneSteps) })
            if (projections.GetValueOrDefault(state.BetaHolder(role)) is { } projection && projection.IsAlive())
                projection.MoveTo(projection.Position + Mirrored(step, BetaMirrored), CloneSpeed);
    }

    private IReadOnlyList<PartyRole> BetaFollowers(bool light)
        => light
            ? [state.BetaHolder(BetaRole.LightFar), state.BetaHolder(BetaRole.LightNear), state.BetaHolder(BetaRole.LightShared)]
            : [state.BetaHolder(BetaRole.DarkFar), state.BetaHolder(BetaRole.DarkNear), state.BetaHolder(BetaRole.DarkUntethered)];

    // The future comes true: the march, and the pairs' tethers with their debuffs.
    private void OrdainBeta()
    {
        Ordain([state.BetaHolder(BetaRole.LightBeacon)], BetaFollowers(light: true), [state.BetaHolder(BetaRole.DarkBeacon)], BetaFollowers(light: false));
        BindFollowers(state.BetaHolder(BetaRole.LightBeacon), BetaFollowers(light: true), state.BetaHolder(BetaRole.DarkBeacon), BetaFollowers(light: false));
        foreach (var (light, dark, statusId, tetherId) in BetaPairs)
        {
            var a = Alive(state.BetaHolder(light));
            var b = Alive(state.BetaHolder(dark));
            a?.AddStatus(statusId);
            b?.AddStatus(statusId);
            if (a is not null && b is not null) playerTethers.Add(world.Tether(a, b, tetherId));
        }
    }

    private static readonly (BetaRole Light, BetaRole Dark, ushort StatusId, ushort TetherId)[] BetaPairs =
    [
        (BetaRole.LightNear, BetaRole.DarkNear, StatusId.HouseArrest, TetherId.HouseArrest),
        (BetaRole.LightFar, BetaRole.DarkFar, StatusId.RestrainingOrder, TetherId.RestrainingOrder),
    ];

    private void ReleaseBetaTethers()
    {
        foreach (var tether in playerTethers) tether.Despawn();
        playerTethers.Clear();
        foreach (var member in Living())
        {
            member.RemoveStatus(StatusId.HouseArrest);
            member.RemoveStatus(StatusId.RestrainingOrder);
        }
    }

    // House Arrest keeps its pair together, Restraining Order apart.
    private void JudgeBetaTethers()
    {
        foreach (var (light, dark, statusId, _) in BetaPairs)
        {
            if (Alive(state.BetaHolder(light)) is not { } a || Alive(state.BetaHolder(dark)) is not { } b) continue;
            var apart = Vector2.Distance(Flat(a.Position), Flat(b.Position));
            var holds = statusId == StatusId.HouseArrest ? apart <= HouseArrestLimit : apart >= RestrainingOrderMinimum;
            var why = statusId == StatusId.HouseArrest
                ? $"House Arrest partners {apart:F1}y apart"
                : $"Restraining Order partners only {apart:F1}y apart";
            Helper(a.Position)?.Cast(TeaActions.PlaintOfSurety(a, b, holds ? null : why));
        }
    }

    // Each jumping clone lands on the player farthest from it.
    private void JJumps()
    {
        foreach (var k in Jumpers)
        {
            if (clones[k] is not { } clone) continue;
            if (Living().MaxBy(m => Vector2.Distance(Flat(m.Position), Flat(clone.Position))) is not { } bait) continue;
            jumpLandings[k] = bait.Position;
            clone.Cast(TeaActions.JJump, bait);
        }
    }

    // -- Ordained Capital Punishment -----------------------------------------------------

    // Three shared hits on whoever Perfect Alexander targets at each, and Ordained Punishment on its
    // target as the second lands.
    private void CapitalPunishment(float cast)
    {
        Helpers(cast + 6.10f, cast + 8.24f, 1);
        At(cast, () => BossCast(TeaActions.OrdainedCapitalPunishment));
        At(cast + 6.10f, CapitalPunishmentHit);
        At(cast + 7.17f, CapitalPunishmentHit);
        At(cast + 7.17f, () =>
        {
            if (TankMember is not { } target || perfectAlexander is not { IsActive: true, Dead: false } boss) return;
            boss.Cast(TeaActions.OrdainedPunishment, target);
            punishment = (target, PunishmentLands.First(t => t > world.Events.Elapsed) + PunishedSeconds);
        });
        At(cast + 8.24f, CapitalPunishmentHit);
    }

    private void CapitalPunishmentHit()
    {
        if (TankMember is { } target) Helper(target.Position)?.Cast(TeaActions.OrdainedCapitalPunishmentHit, target);
    }

    // -- Almighty Judgment ------------------------------------------------------------

    // Three sets of tiles light up 2s apart and go off 8s later in the same order; Irresistible Grace
    // starts as the first goes off and lands just after the third.
    private void AlmightyJudgment(float cast, int judgment)
    {
        At(cast + 5.11f - HelperLead, SpawnTileHelpers);
        At(cast, () => BossCast(TeaActions.AlmightyJudgment));
        for (var wave = 0; wave < 3; wave++)
        {
            var set = wave;
            At(cast + 6.11f + 1.99f * wave, () => Tiles(judgment, set, TeaActions.AlmightyJudgmentTile));
            At(cast + 14.14f + 1.99f * wave, () => Tiles(judgment, set, TeaActions.AlmightyJudgmentBlast));
        }
        At(cast + 14.18f, () =>
        {
            var target = Alive(state.GraceTargets[judgment]) ?? Living().FirstOrDefault();
            if (target is not null && perfectAlexander is { IsActive: true, Dead: false } boss) boss.Cast(TeaActions.IrresistibleGrace, target);
        });
        At(cast + 19.62f, DespawnTileHelpers);
    }

    private void SpawnTileHelpers()
    {
        for (var i = 0; i < tileHelpers.Length; i++) tileHelpers[i] ??= SpawnHelper();
    }

    private void DespawnTileHelpers()
    {
        for (var i = 0; i < tileHelpers.Length; i++)
        {
            tileHelpers[i]?.Despawn();
            tileHelpers[i] = null;
        }
    }

    private void Tiles(int judgment, int wave, EnemyAction action)
    {
        var set = state.JudgmentOrders[judgment][wave];
        var first = TileSets.Take(set).Sum(s => s.Length);
        for (var k = 0; k < TileSets[set].Length; k++)
        {
            if (tileHelpers[first + k] is not { } helper) continue;
            var tile = new Vector3(TileSets[set][k].X, 0f, TileSets[set][k].Y);
            helper.SetPosition(new Placement(tile, 0f));
            helper.Cast(action);
        }
    }

    // -- Temporal Prison -----------------------------------------------------------------

    // Whoever stands in a jail as it lands is held there; one that catches nobody ends the fight.
    // UNVERIFIED: that an empty jail wipes.
    private void LandJail(int jail)
    {
        if (killed) return;
        var spot = JailSpots[jail];
        var caught = Living().Where(m => Vector2.Distance(Flat(m.Position), spot) <= JailRadius).ToList();
        TeaZone.Instance.ArenaFeature(jail)?.PlayBeat(JailLandedState, JailLandedMask);
        DiagnosticLog.Info($"[TeaP4PerfectAlexander] Jail {jail + 1} caught {string.Join(" ", caught.Select(m => (m as ISimPartyMember)?.Role))}.");
        if (caught.Count == 0)
        {
            party.WipeAllPlayers(ActionId.TemporalPrison, $"jail {jail + 1} landed on nobody");
            return;
        }
        foreach (var member in caught)
        {
            member.StopMoving();
            member.AddStatus(StatusId.TemporalPrison);
        }
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
