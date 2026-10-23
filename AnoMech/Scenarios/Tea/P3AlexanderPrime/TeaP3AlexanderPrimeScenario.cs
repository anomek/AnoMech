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

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

// Alexander Prime from his first appearance to Divine Judgment's success, which comes at the slowest
// real kill so every mechanic shows up; a practice runs Inception alone, or Wormhole on to the end. The
// Temporal Stasis intermission before it and Perfect Alexander's arrival after it are their own
// scenarios. Scenario time 0 is 2s before he turns targetable.
public sealed class TeaP3AlexanderPrimeScenario : IMultiplayerReplayable
{
    // The fight's own clock counts from Temporal Stasis' cast start.
    private const float StasisToStart = 17.25f;
    // A provoked boss turns to its new tank 1.1-1.6s after the press.
    private const float ProvokeTurnSeconds = 1.3f;
    // An ActorControl or status sent before the engine created the actor is dropped.
    private const float SpawnSettles = 1f;

    private const float OrbSpeed = 3.8f;
    // UNVERIFIED: every orb went off 0.6-1.0y from its player's centre.
    private const float OrbContact = 1f;
    // Two orbs' blasts, 6y each, overlapping set off the burst.
    private const float OrbBurstSpacing = 12f;
    private const float BurstAfterPop = 0.35f;
    // Each orb leaves a zone where it went off, there until it fades: whoever stands in it is Heavy,
    // 20% slower, and leaving takes it off.
    private const float OrbZoneRadius = 6f;
    private const ushort OrbZoneSlow = 20;
    private const float OrbZoneFades = 18.9f;
    private const int CrystalCount = 4;
    private const float HeartWalksAt = 54.55f;
    private const float HeartSpeed = 10f;
    // The heart moves 3.1y a step and looks for crystals only on its steps.
    private const float HeartStep = 0.31f;
    // The heart sets off every real crystal within 6y of its centre.
    private const float HeartReach = 6f;
    // A crystal whose 5y zone touches another's, or Alexander Prime's hitbox, blows up on its own:
    // checked 0.86s after they spawn, just after the heart sets out, and as he moves to his Sacrament
    // spot. UNVERIFIED: his reach past 11.6y, and the check time on him (real ones spread about 0.5s).
    private const float CrystalRadius = 5f;
    private const float CrystalsTouchAt = 50.72f;
    private const float AlexanderPrimeTouchesAt = 55.0f;
    private const float SacramentSpotTouchesAt = 72.65f;
    private const float GoldAfterShatter = 0.58f;
    private const float VulnerabilityDownAfterShatter = 0.76f;
    // Real pulls: Bleeding by a dropped crystal from 5.7s after the drop until the heart shatters it.
    private const float CrystalBleedRadius = 5f;
    private const float CrystalBleedsAfter = 4.83f;
    private const float RestrainingOrderReach = 30f;
    private const float SacramentRadius = 21.5f;
    private const float WarpBehind = 2f;
    // Cruise Chaser lands behind an odd number this long before slashing.
    private const float WarpLead = 1.2f;
    // The client plays a hit along the model's facing, so Cruise Chaser turns to each number by a
    // teleport on his own spot this long before it.
    private const float TurnLead = 0.13f;
    // Brute Justice turns to each Flarethrower's bait by ActorMoves and fires once facing them, each
    // turn after the first a set time after the last flame. UNVERIFIED: when he starts turning; these
    // land a typical 57-degree turn's flame on time.
    private const float FirstFlameTurnLead = 0.12f;
    private const float NextFlameTurn = 2.02f;
    // UNVERIFIED: Cruise Chaser's share of the time both robots take.
    private const float CruiseChaserShare = 0.625f;
    private const float SuccessAfterDown = 0.13f;
    private const float EndAfterSuccess = 2f;
    // Divine Judgment's last hit, which only a tank's LB3 carries the party through.
    private const float RaidwideAfterSuccess = 15.46f;
    // An orb nobody has set off by then bursts on its own.
    private const float OrbsBurst = 55.1f;
    private const float DivineJudgmentEnds = 198.01f + 75f;

    // Brute Justice's going down, and Divine Judgment's success, in the slowest real kill, from
    // Temporal Stasis' cast start.
    private const float BruteJusticeDown = 252.05f;
    private const float DivineJudgmentSuccess = 289.52f;
    private const float BruteJusticeDownAt = BruteJusticeDown - StasisToStart;
    private const float CruiseChaserDownAt = 197.92f + (BruteJusticeDownAt - 197.92f) * CruiseChaserShare;
    private const float AlexanderPrimeDownAt = DivineJudgmentSuccess - StasisToStart - SuccessAfterDown;

    private static readonly Vector3[] OrbSpawns = [new(-19.10f, 0f, 10.08f), new(-20.63f, 0f, 6.38f), new(19.10f, 0f, -10.08f), new(20.63f, 0f, -6.38f)];

    public string Name => "Alexander Prime";
    public IPhase Phase => TeaZone.P3;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaP3AlexanderPrimeAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    public float BgmSecondsAtStart => Practiced.Start;

    private readonly TeaP3AlexanderPrimeSettingsWindow settingsWindow = new();
    private P3PracticeEntry Practiced => TeaP3AlexanderPrimePractice.Of(settingsWindow.Overrides.Practice);
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaP3AlexanderPrimeState state = null!;

    private SimEnemy? alexanderPrime;
    private SimEnemy? bruteJustice;
    private SimEnemy? cruiseChaser;
    private SimEnemy? shanoa;
    private SimEnemy? trueHeart;
    private SimEnemy? punisher;
    private SimEnemy? alexander;
    private SimEnemy? countdown;
    private PartyRole alexanderPrimeTank;
    // Up the whole run, as the real ones are: a client catching up after a dropped link builds an
    // actor some frames after it hears of it, so a helper spawned just before its hit would miss it.
    private readonly List<SimEnemy> alexanderPrimeHelpers = [];
    private readonly List<SimEnemy> bruteJusticeHelpers = [];
    private readonly SimEnemy?[] orbs = new SimEnemy?[TeaP3AlexanderPrimeState.Orbs];
    private readonly SimTether?[] orbTethers = new SimTether?[TeaP3AlexanderPrimeState.Orbs];
    private readonly bool[] orbsHoming = new bool[TeaP3AlexanderPrimeState.Orbs];
    private readonly bool[] orbsPopped = new bool[TeaP3AlexanderPrimeState.Orbs];
    private readonly List<(Vector3 At, SimCharacter SetOffBy)> orbBursts = [];
    private readonly List<(Vector3 At, float Until)> orbZones = [];
    private bool electromagneticBurst;
    private readonly List<(Vector3 At, SimCharacter By)> crystalDrops = [];
    private readonly List<(SimEnemy Crystal, TeaDotPuddles.Puddle Voidzone, SimCharacter Dropper)> crystals = [];
    private readonly TeaDotPuddles puddles = new();
    private readonly HashSet<SimEnemy> touchedCrystals = [];
    private readonly HashSet<SimEnemy> failedCrystals = [];
    private readonly Dictionary<SimEnemy, CrystalFault> crystalFaults = [];
    private readonly Dictionary<SimCharacter, float> shatterTaken = [];
    private bool heartWalking;
    private int heartSteps;
    private bool heartGold;
    private float heartGoldAt;
    private SimTether? restrainingOrderTether;
    private readonly HashSet<SimCharacter> flareBaits = [];
    private readonly HashSet<SimCharacter> swordBaits = [];
    private SimCharacter? swordBait;
    private readonly SimCharacter?[] numberBaits = new SimCharacter?[8];
    // A pair per size, each size spawned as the last one bursts.
    private readonly SimEventObject?[,] wormholes = new SimEventObject?[3, 2];
    private float rayFacing;
    private readonly SimTether?[] robotTethers = new SimTether?[2];

    public TeaP3AlexanderPrimeState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaP3AlexanderPrimeState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        alexanderPrime = bruteJustice = cruiseChaser = shanoa = trueHeart = punisher = alexander = countdown = null;
        alexanderPrimeTank = PartyRole.MainTank;
        alexanderPrimeHelpers.Clear();
        bruteJusticeHelpers.Clear();
        Array.Clear(orbs);
        Array.Clear(orbTethers);
        Array.Clear(orbsHoming);
        Array.Clear(orbsPopped);
        orbBursts.Clear();
        orbZones.Clear();
        electromagneticBurst = false;
        crystalDrops.Clear();
        crystals.Clear();
        puddles.Start(world, settingsWindow.Overrides.TickClock);
        touchedCrystals.Clear();
        failedCrystals.Clear();
        crystalFaults.Clear();
        shatterTaken.Clear();
        heartWalking = heartGold = false;
        heartGoldAt = 0f;
        heartSteps = 0;
        restrainingOrderTether = null;
        flareBaits.Clear();
        swordBaits.Clear();
        swordBait = null;
        Array.Clear(numberBaits);
        Array.Clear(wormholes);
        Array.Clear(robotTethers);
        var practice = TeaP3AlexanderPrimePractice.Of(state.Practice);
        DiagnosticLog.Info($"[TeaP3AlexanderPrime] Roll: practice {practice.Label}, orbs {string.Join(" ", state.OrbHolders)}, Restraining Order {state.RestrainingOrderTank}+{state.RestrainingOrderDps}, "
                           + $"Shared Sentence {state.SharedSentence}, Alexander Prime {(state.AlexanderPrimeNorth ? "north" : "south")}, "
                           + $"Sacrament spot {state.SacramentSpot}, Brute Justice {(state.BruteJusticeNorthWest ? "NW" : "NE")}, "
                           + $"wormholes {(state.PuddlesMirrored ? "WSW/ENE" : "WNW/ESE")}, numbers {string.Join(" ", state.Numbers.Select((r, k) => $"{k + 1}={r}"))}, "
                           + $"enumerations {string.Join(" ", state.Enumerations)}.");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaP3AlexanderPrimeState>)AiStrats[idx]).Run(state, world);

        if (practice.Start > 0f) At(practice.Start, PickUpAtPractice);
        At(0f, SpawnActors);
        At(2.00f, () => AlexanderPrimeAppears(Vector3.Zero));
        // None before the first Chastening Heat; each 2.0s after his last action, then every 3.03s.
        foreach (var t in (float[])[14.25f, 21.57f, 24.60f, 92.57f, 95.60f, 103.66f, 106.69f, 113.03f, 116.07f, 165.82f, 168.85f, 175.89f, 182.94f, 185.97f])
            At(t, AutoAttack);
        At(7.21f, () => AtTank(TeaActions.ChasteningHeat));
        At(15.37f, () => AtTank(TeaActions.DivineSpear));
        At(17.46f, () => AtTank(TeaActions.DivineSpear));
        At(19.56f, () => AtTank(TeaActions.DivineSpear));
        At(24.78f, () => alexanderPrime?.Cast(TeaActions.InceptionFormation));

        At(31.85f, AlexanderPrimeLeaves);
        At(34.00f, InceptionLayout);
        At(34.10f, () =>
        {
            bruteJustice?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        At(34.17f, SpawnShanoa);
        At(35.24f, () => cruiseChaser?.Cast(TeaActions.InceptionVisual));
        At(36.19f, SpawnOrbs);
        At(36.45f, TetherOrbs);
        At(40.24f, () => alexanderPrime?.Cast(TeaActions.JudgmentCrystal));
        At(42.05f, OrbsHome);
        At(42.89f, () =>
        {
            foreach (var tether in orbTethers) tether?.Despawn();
        });
        At(43.96f, MarkCrystalHolders);
        foreach (var (t, step) in new[] { (43.96f, 0u), (45.95f, 1u), (47.95f, 2u), (49.95f, 3u) })
            At(t, () => DecayOrbs(step));
        At(49.01f, DropCrystals);
        At(49.66f, SpawnTrueHeart);
        At(49.86f, SpawnCrystals);
        At(CrystalsTouchAt, FailCrystalsTouchingEachOther);
        At(HeartWalksAt, HeartWalks);
        At(AlexanderPrimeTouchesAt, () => FailCrystalsTouching(alexanderPrime?.Position, ByAlexanderPrime, shatterTaken));
        At(OrbsBurst, BurstUnpoppedOrbs);
        const float firstFlameTurn = 60.37f - FirstFlameTurnLead;
        At(firstFlameTurn, () => TurnToClosestUnflamed(firstFlameTurn, flames: 3));
        At(66.84f, GiveInceptionDebuffs);
        At(66.91f, () => alexanderPrime?.Cast(TeaActions.Inception));
        At(67.86f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(72.55f, HeartFlies);
        // Healers have long healed the shatters by then, so its blasts add up only with each other.
        At(SacramentSpotTouchesAt, () => FailCrystalsTouching(SacramentSpot(state.SacramentSpot), Missed, []));
        At(75.95f, TrueHeartSucceeds);
        At(77.95f, () =>
        {
            cruiseChaser?.SetPosition(new Placement(Vector3.Zero, 0f));
            cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        // Only the gold heart's flight gives his spot away. UNVERIFIED: that without it he shows only
        // as Sacrament goes off.
        At(79.11f, () =>
        {
            if (heartGold) alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        At(79.82f, ClearInceptionDebuffs);
        At(79.93f, InceptionPlaints);
        At(80.23f, () => alexanderPrime?.Cast(heartGold ? TeaActions.SacramentInception : TeaActions.SacramentUnforeseen(WhyTheHeartFailed(), CrystalMistakes())));
        At(82.37f, () => alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(85.28f, () => bruteJustice?.Cast(TeaActions.SuperJump));
        At(86.11f, TurnToClosestUnslashed);
        At(86.24f, SlashAtBait);
        At(87.18f, TurnToClosestUnslashed);
        At(87.31f, SlashAtBait);
        At(88.25f, TurnToClosestUnslashed);
        At(88.38f, SlashAtBait);
        At(89.42f, SuperJumpOnFarthest);
        At(89.47f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(91.45f, () =>
        {
            AlexanderPrimeAppears(Vector3.Zero);
            shanoa?.FadeOut();
        });
        At(91.53f, () => bruteJustice?.PlayActionTimeline(ActionTimelineId.WarpStart));

        At(96.67f, () => AtTank(TeaActions.ChasteningHeat));
        At(106.83f, () => AtTank(TeaActions.DivineSpear));
        At(108.93f, () => AtTank(TeaActions.DivineSpear));
        At(111.03f, () => AtTank(TeaActions.DivineSpear));
        At(116.20f, () => alexanderPrime?.Cast(TeaActions.WormholeFormation));

        At(123.27f, AlexanderPrimeLeaves);
        At(125.47f, WormholeLayout);
        At(126.66f, () => cruiseChaser?.Cast(TeaActions.LimitCutP3));
        At(128.67f, () => bruteJustice?.Cast(TeaActions.LinkUp));
        At(129.37f, () => cruiseChaser?.SetAnimationState(1, 1));
        At(131.67f, () => alexanderPrime?.Cast(TeaActions.VoidOfRepentance));
        At(131.83f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(132.45f, () => cruiseChaser?.SetAnimationState(1, 1));
        for (var k = 0; k < 8; k++)
        {
            var number = k;
            At(132.45f + 0.03f * k, () => AttachNumber(number));
        }
        At(132.74f, SpawnChakrams);
        At(133.46f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams) chakram.ActorControl.PopIn();
        });
        At(134.59f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams.Where(c => c.IsActive)) chakram.Cast(TeaActions.EyeOfTheChakram);
        });
        At(135.53f, () => SpawnWormholes(0, EObjId.Wormhole1, 0x400003u));
        At(136.50f, () => WormholesBeat(0, 1, 0x2));
        At(137.80f, () => bruteJustice?.Cast(TeaActions.SuperJump));
        WarpBehindOddAt(141.90f, number: 0);
        At(141.90f, SuperJumpOnFarthest);
        CruiseChaserHitAt(141.90f, number: 0);
        At(142.75f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams) chakram.Despawn();
        });
        At(142.81f, () => alexanderPrime?.Cast(TeaActions.SacramentWormhole));
        CruiseChaserHitAt(143.37f, number: 1);
        At(144.31f, ApocalypticRay);
        foreach (var t in (float[])[144.94f, 146.00f, 147.07f, 148.14f, 149.21f])
            At(t, RayHit);
        WarpBehindOddAt(146.13f, number: 2);
        CruiseChaserHitAt(146.13f, number: 2);
        At(146.38f, () => WormholesBeat(0, 4, 0x8));
        At(146.50f, () => Repent(TeaActions.Repentance1));
        At(146.75f, () => SpawnWormholes(1, EObjId.Wormhole2, 0x420003u));
        At(147.40f, () => WormholesBeat(1, 1, 0x2));
        CruiseChaserHitAt(147.60f, number: 3);
        At(147.60f, () => DespawnWormholes(0));
        WarpBehindOddAt(150.38f, number: 4);
        CruiseChaserHitAt(150.38f, number: 4);
        At(150.60f, () => WormholesBeat(1, 4, 0x8));
        At(150.74f, () => Repent(TeaActions.Repentance2));
        At(150.90f, () => SpawnWormholes(2, EObjId.Wormhole3, 0x400003u));
        At(151.60f, () => WormholesBeat(2, 1, 0x2));
        CruiseChaserHitAt(151.85f, number: 5);
        At(151.90f, () => DespawnWormholes(1));
        WarpBehindOddAt(154.61f, number: 6);
        At(154.46f, () => bruteJustice?.Cast(TeaActions.MissileCommand));
        CruiseChaserHitAt(154.61f, number: 6);
        At(154.84f, () => WormholesBeat(2, 4, 0x8));
        At(154.97f, () => Repent(TeaActions.Repentance3));
        At(155.98f, IncineratingHeat);
        CruiseChaserHitAt(156.08f, number: 7);
        At(156.10f, () => DespawnWormholes(2));
        At(157.78f, () => cruiseChaser?.SetAnimationState(1, 0));
        At(160.51f, MarkEnumerations);
        At(160.64f, () => bruteJustice?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(163.15f, () => alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(165.19f, () => AlexanderPrimeAppears(Vector3.Zero));
        At(165.64f, ResolveEnumerations);
        At(169.92f, () => alexanderPrime?.Cast(TeaActions.MegaHoly));
        At(177.06f, () => alexanderPrime?.Cast(TeaActions.MegaHoly));

        At(187.45f, () => world.Map.BattleTalk(BNpcNameId.AlexanderPrime, BattleTalkId.TimeOfJudgment, 4000));
        At(187.57f, () => alexanderPrime?.Cast(TeaActions.SummonAlexander));
        At(194.65f, () => alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpStart));
        At(196.76f, AddPhaseLayout);
        At(196.88f, () =>
        {
            bruteJustice?.Cast(TeaActions.JStorm);
            alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        At(197.92f, () =>
        {
            foreach (var boss in new[] { alexanderPrime, cruiseChaser })
            {
                boss?.SetTargetable(true);
                boss?.SetVisibleInEnemyList(true);
            }
            world.Map.AddEffect(0x00080004, 3);
        });
        At(198.01f, DivineJudgmentAndEternalDarkness);
        At(198.92f, () =>
        {
            bruteJustice?.SetTargetable(true);
            bruteJustice?.SetVisibleInEnemyList(true);
        });
        for (var t = 202.01f; t < 260f; t += 3.03f)
        {
            WhileBruteJusticeUp(t, JWave);
            WhileBruteJusticeUp(t + 0.54f, () => bruteJustice?.AddStatus(StatusId.DamageUpStacking));
        }
        ScheduleDowns();

        if (practice.Start > 0f) world.Events.Advance(practice.Start);
    }

    // A wormhole nobody soaks goes off on everyone.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved is { Action.ActionId: ActionId.Repentance1 or ActionId.Repentance2 or ActionId.Repentance3, Hits.Count: 0, Caster: SimEnemy wormhole })
            wormhole.Cast(TeaActions.Confession);
    }

    public void Tick(float delta, float elapsed)
    {
        if (state is null) return;
        PopOrbsOnContact();
        puddles.Tick();
        SlowInOrbZones();
    }

    // Scheduled by host and peer alike, so each client fakes its own tank's LB3 gauge.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(HelperTimelines, "TeaP3AlexanderPrime");
        instanceWorld.Party.LimitBreak.Set(3f);
    }

    // The helpers' and adds' hits play gimmick timelines (LoadType 0), which an actor drops unless
    // they are already loaded. UNVERIFIED in game.
    private static readonly (ushort Id, string Key)[] HelperTimelines =
    [
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),
        (1604, "mon_sp/gimmick/ultima_ultimadan_sakuretu"),
        (4716, "mon_sp/gimmick/d2a6_boss_gimmick12"),
        (4722, "mon_sp/gimmick/d2a6_boss_gimmick15"),
        (5027, "mon_sp/gimmick/d2ac2_boss_gimmick05"),
        (5029, "mon_sp/gimmick/d2ac2_boss_gimmick06"),
        (5031, "mon_sp/gimmick/d2ac2_boss_gimmick07"),
        (5037, "mon_sp/gimmick/d2ac2_boss_gimmick10"),
        (5041, "mon_sp/gimmick/d2ac2_boss_gimmick12"),
        (5057, "mon_sp/gimmick/d2ab2_boss_gimmick05"),
        (7563, "mon_sp/gimmick/d2az_boss_gimmick02"),
    ];

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new TeaP3AlexanderPrimeAiReplayStateMessage(s.Practice, s.OrbHolders.ToArray(), s.RestrainingOrderTank, s.RestrainingOrderDps,
                s.SharedSentence, s.AlexanderPrimeNorth, s.SacramentSpot, s.BruteJusticeNorthWest, s.PuddlesMirrored, s.Numbers.ToArray(),
                s.Enumerations.ToArray())
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaP3AlexanderPrimeAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaP3AlexanderPrimeState.FromNetworkReplay(msg.Practice, msg.OrbHolders, msg.RestrainingOrderTank, msg.RestrainingOrderDps,
                msg.SharedSentence, msg.AlexanderPrimeNorth, msg.SacramentSpot, msg.BruteJusticeNorthWest, msg.PuddlesMirrored, msg.Numbers,
                msg.Enumerations) is not { } shadowState) return null;
        ((IScenarioAi<TeaP3AlexanderPrimeState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The bosses, the heart and the chakrams may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaP3AlexanderPrimeState shadow) return;
        var objects = shadow.Objects;
        objects.AlexanderPrime ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.AlexanderPrime);
        objects.BruteJustice ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.BruteJustice);
        objects.CruiseChaser ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
        objects.TrueHeart ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.TrueHeart && e.IsActive);
        foreach (var orb in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.Plasmasphere && e.IsActive))
        {
            if (objects.Plasmaspheres.Contains(orb)) continue;
            var k = Enumerable.Range(0, OrbSpawns.Length).MinBy(i => Vector2.Distance(Flat(orb.Position), Flat(OrbSpawns[i])));
            objects.Plasmaspheres[k] ??= orb;
        }
        foreach (var chakram in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.SteamChakram && e.IsActive))
            if (!objects.Chakrams.Contains(chakram)) objects.Chakrams.Add(chakram);
    }

    // -- Alexander Prime -----------------------------------------------------------

    private SimCharacter? LivingOrNext(PartyRole role)
    {
        if (party.Get(role) is { } member && member.IsAlive()) return member;
        return PerRole.All.Select(party.Get).FirstOrDefault(m => m.IsAlive());
    }

    // All three exist unseen from the start, and step in and out with warps.
    private void At(float time, Action action, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP3AlexanderPrimePractice.Covers(state.Practice, time)) world.Events.Add(time, action, file, line);
    }

    // Alexander Prime back in the centre as the party regroups; the robots wait out of sight for Wormhole.
    private void PickUpAtPractice()
    {
        SpawnActors();
        world.Events.Add(SpawnSettles, () => AlexanderPrimeAppears(Vector3.Zero));
    }

    private void SpawnActors()
    {
        state.Objects.AlexanderPrime = alexanderPrime = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.AlexanderPrime, NameId: BNpcNameId.AlexanderPrime, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Vector3.Zero, 0f)));
        state.Objects.BruteJustice = bruteJustice = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BruteJustice, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(-6f, 0f, 0f), 0f)));
        state.Objects.CruiseChaser = cruiseChaser = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.CruiseChaser, NameId: BNpcNameId.CruiseChaser, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(6f, 0f, 0f), 0f)));
        punisher = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.AlexanderPrime, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(Vector3.Zero, 0f)));
        // The hidden Alexander who strikes Divine Judgment's last blow, and the helper that counts down to it.
        alexander = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Alexander, NameId: BNpcNameId.Alexander, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(new Vector3(9.83f, 0f, -11.16f), 0f)));
        countdown = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.Alexander, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(new Vector3(0f, 28.70f, -55.10f), 0f)));
        SpawnHelpers(alexanderPrimeHelpers, 4, BNpcNameId.AlexanderPrime);
        SpawnHelpers(bruteJusticeHelpers, 2, BNpcNameId.BruteJustice);
        if (alexanderPrime is not null) alexanderPrime.Provoked += TurnOnProvoke;
    }

    private void TurnOnProvoke(SimCharacter by)
    {
        if (by is not ISimPartyMember tank) return;
        world.Events.Add(ProvokeTurnSeconds, () =>
        {
            if (!by.IsAlive()) return;
            alexanderPrimeTank = tank.Role;
            FaceTank();
        });
    }

    private void FaceTank()
    {
        if (alexanderPrime is not { Targetable: true } boss || LivingOrNext(alexanderPrimeTank) is not { } tank) return;
        boss.SetTarget(tank, follow: false);
        boss.KeepFacing(tank);
    }

    private void AlexanderPrimeAppears(Vector3 at)
    {
        if (alexanderPrime is not { } boss) return;
        boss.SetPosition(new Placement(at, 0f));
        boss.SetTargetable(true);
        boss.SetVisibleInEnemyList(true);
        boss.PlayActionTimeline(ActionTimelineId.WarpEnd);
        FaceTank();
    }

    private void AlexanderPrimeLeaves()
    {
        if (alexanderPrime is not { } boss) return;
        boss.KeepFacing(null);
        boss.SetTarget(null);
        boss.SetTargetable(false);
        boss.SetVisibleInEnemyList(false);
        boss.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    private void AutoAttack()
    {
        if (alexanderPrime is not { Targetable: true, IsCasting: false } boss) return;
        if (LivingOrNext(alexanderPrimeTank) is { } tank) boss.Cast(TeaActions.AlexanderPrimeAttack, tank);
    }

    // Chastening Heat and Divine Spear go at whoever holds his enmity as they start.
    private void AtTank(EnemyAction action)
    {
        if (alexanderPrime is { Targetable: true } boss && LivingOrNext(alexanderPrimeTank) is { } tank) boss.Cast(action, tank);
    }

    // -- True Heart HP -----------------------------------------------------------------

    private static float Lerp(float from, float to, float start, float end, float now)
        => from + (to - from) * Math.Clamp((now - start) / MathF.Max(end - start, 1e-3f), 0f, 1f);

    // Each second while it lives, until its success.
    private void UpdateTrueHeartHp()
    {
        if (trueHeart is not { IsActive: true, Dead: false } heart) return;
        var now = world.Events.Elapsed;
        heart.SetHpFraction(heartGold ? Lerp(1f, 0.96f, heartGoldAt, 75.95f, now) : Lerp(1f, 0.8f, 49.66f, 55.25f, now));
        if (now + 1f < 75.95f) world.Events.Add(1f, UpdateTrueHeartHp);
    }

    // -- Inception: orbs, crystals and the True Heart -------------------------------------

    // The layout as it runs with Alexander Prime south; north, every spot of it is mirrored.
    private Vector3 InceptionSide(Vector3 southLayout) => state.AlexanderPrimeNorth ? southLayout with { Z = -southLayout.Z } : southLayout;

    private float InceptionSide(float southFacing) => state.AlexanderPrimeNorth ? MathF.PI - southFacing : southFacing;

    // Alexander Prime on the south or north edge, Brute Justice west and Cruise Chaser east.
    private void InceptionLayout()
    {
        alexanderPrime?.SetPosition(new Placement(InceptionSide(new Vector3(0f, 0f, SacramentRadius)), InceptionSide(MathF.PI)));
        alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        bruteJustice?.SetPosition(new Placement(new Vector3(-21.5f, 0f, 0f), MathF.PI / 2f));
        cruiseChaser?.SetPosition(new Placement(new Vector3(21.5f, 0f, 0f), -MathF.PI / 2f));
    }

    private void SpawnShanoa()
        => shanoa = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Shanoa, NameId: BNpcNameId.Shanoa, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(InceptionSide(new Vector3(0f, 0f, -21.5f)), InceptionSide(0f))));

    private SimCharacter? OrbHolder(int k) => party.Get(state.OrbHolders[k]) is { } member && member.IsAlive() ? member : null;

    // Each faces its player from the spawn on.
    private void SpawnOrbs()
    {
        for (var k = 0; k < orbs.Length; k++)
        {
            var at = InceptionSide(OrbSpawns[k]);
            var aim = OrbHolder(k)?.Position ?? Vector3.Zero;
            state.Objects.Plasmaspheres[k] = orbs[k] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Plasmasphere, NameId: BNpcNameId.Plasmasphere, Level: Level,
                Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
                Placement: new Placement(at, MathF.Atan2(aim.X - at.X, aim.Z - at.Z))));
            if (OrbHolder(k) is { } holder) orbs[k]?.KeepFacing(holder);
        }
    }

    private void TetherOrbs()
    {
        for (var k = 0; k < orbs.Length; k++)
            if (orbs[k] is { IsActive: true } orb && OrbHolder(k) is { } holder)
                orbTethers[k] = world.Tether(orb, holder, TetherId.Plasmasphere);
    }

    // After standing still a while, each one makes straight for its player at a walk.
    private void OrbsHome()
    {
        for (var k = 0; k < orbs.Length; k++)
        {
            if (orbs[k] is not { IsActive: true } orb || OrbHolder(k) is not { } holder) continue;
            orbsHoming[k] = true;
            orb.KeepFacing(null);
            orb.HomeIn(holder, OrbSpeed);
        }
    }

    // Four steps, each shrinking every orb and stacking it a Damage Down.
    private void DecayOrbs(uint step)
    {
        for (var k = 0; k < orbs.Length; k++)
        {
            if (orbs[k] is not { IsActive: true } orb || orbsPopped[k]) continue;
            orb.Activate(0x10u + step);
            orb.AddStatus(StatusId.DamageDown, 30f);
        }
    }

    private void BurstUnpoppedOrbs()
    {
        for (var k = 0; k < orbs.Length; k++)
        {
            if (orbsPopped[k] || orbs[k] is not { IsActive: true } orb) continue;
            orbsPopped[k] = true;
            orb.Follow(null);
            orb.StopMoving();
            DiagnosticLog.Info($"[TeaP3AlexanderPrime] Orb {k} burst on its own.");
            orb.Cast(TeaActions.Burst, OrbHolder(k));
            world.Events.Add(1.2f, () => orb.Defeat(corpseFadeDelay: 8.1f));
        }
    }

    // Whoever an orb reaches sets it off, its own player or not.
    private void PopOrbsOnContact()
    {
        for (var k = 0; k < orbs.Length; k++)
        {
            if (!orbsHoming[k] || orbsPopped[k] || orbs[k] is not { IsActive: true, PacketSpawnPending: false } orb) continue;
            var touched = party.ActiveMembers().Where(m => m.IsAlive())
                .FirstOrDefault(m => Vector2.Distance(Flat(m.Position), Flat(orb.Position)) <= OrbContact);
            if (touched is null) continue;
            PopOrb(k, orb, touched);
        }
    }

    private void PopOrb(int k, SimEnemy orb, SimCharacter touched)
    {
        orbsPopped[k] = true;
        orb.Follow(null);
        orb.StopMoving();
        var at = orb.Placement();
        DiagnosticLog.Info($"[TeaP3AlexanderPrime] Orb {k} went off on {RoleName(touched)} at ({at.Position.X:F1},{at.Position.Z:F1}).");
        orb.Cast((orb.HasStatus(StatusId.DamageDown) ? TeaActions.Aetheroplasm : TeaActions.AetheroplasmUnshrunk) with
        {
            Area = new() { AdjustTargets = (ctx, _) => ctx.Party.Find.InsideActionAoe(ActionId.Aetheroplasm, at) },
        }, OrbHolder(k));
        if (!electromagneticBurst && orbBursts.FirstOrDefault(b => Vector2.Distance(Flat(b.At), Flat(at.Position)) < OrbBurstSpacing) is { SetOffBy: { } other })
        {
            electromagneticBurst = true;
            world.Events.Add(BurstAfterPop, () => punisher?.Cast(TeaActions.ElectromagneticBurst(other, touched)));
        }
        var order = orbBursts.Count;
        orbBursts.Add((at.Position, touched));
        world.Events.Add(0.5f, () =>
        {
            var voidzone = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.VoidzonePlasmasphere,
                Placement = new Placement(at.Position with { Y = 0f }, 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = EObjId.OrbVoidzonesEntityId + (uint)order,
                TargetableStatus = 5,
                Arg2 = 0x540003u + ((uint)order << 16),
            });
            orbZones.Add((at.Position with { Y = 0f }, world.Events.Elapsed + OrbZoneFades));
            world.Events.Add(OrbZoneFades, () => voidzone?.DirectorEObjMod(7));
            world.Events.Add(21.9f, () => voidzone?.Despawn());
        });
        world.Events.Add(1.2f, () => orb.Defeat(corpseFadeDelay: 8.1f));
    }

    private void SlowInOrbZones()
    {
        if (orbZones.Count == 0) return;
        var now = world.Events.Elapsed;
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()))
        {
            var inside = orbZones.Any(z => now < z.Until && Vector2.Distance(Flat(member.Position), Flat(z.At)) <= OrbZoneRadius);
            if (inside == member.HasStatus(StatusId.Heavy)) continue;
            if (inside) member.AddStatus(StatusId.Heavy, stacks: OrbZoneSlow);
            else member.RemoveStatus(StatusId.Heavy);
        }
    }

    private IEnumerable<SimCharacter> CrystalHolders()
        => state.CrystalHolders.Select(party.Get).Where(m => m.IsAlive()).Cast<SimCharacter>();

    private void MarkCrystalHolders()
    {
        foreach (var member in CrystalHolders()) member.AttachLockonVfx(LockonId.JudgmentCrystal);
    }

    private void DropCrystals()
    {
        crystalDrops.Clear();
        foreach (var (member, helper) in CrystalHolders().Zip(alexanderPrimeHelpers.Where(h => h.IsActive)))
        {
            crystalDrops.Add((member.Position, member));
            helper.SetPosition(new Placement(member.Position, 0f));
            helper.Cast(TeaActions.JudgmentCrystalAoe, member);
        }
        shanoa?.Cast(TeaActions.UndyingAffection);
    }

    private void SpawnTrueHeart()
    {
        state.Objects.TrueHeart = trueHeart = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.TrueHeart, NameId: BNpcNameId.TrueHeart, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(InceptionSide(new Vector3(0f, 0f, -21f)), InceptionSide(0f))));
        world.Events.Add(SpawnSettles, UpdateTrueHeartHp);
    }

    private void SpawnCrystals()
    {
        for (var k = 0; k < crystalDrops.Count; k++)
        {
            var at = crystalDrops[k].At with { Y = 0f };
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.JudgmentCrystal, NameId: BNpcNameId.JudgmentCrystal, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
                    Placement: new Placement(at, 0f))) is not { } crystal) continue;
            var voidzone = puddles.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.VoidzoneJudgmentCrystal,
                Placement = new Placement(at, 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = EObjId.CrystalVoidzonesEntityId + (uint)k,
                TargetableStatus = 5,
                Arg2 = 0x500003u + ((uint)k << 16),
            }), at, CrystalBleedRadius, StatusId.Bleeding, "Bleeding: stood by a Judgment Crystal", CrystalBleedsAfter);
            crystals.Add((crystal, voidzone, crystalDrops[k].By));
        }
    }

    // What kept a crystal from the heart: as the heart's failure reads, and as its own player's mistake.
    private sealed record CrystalFault(string Heart, string Dropper);

    private static readonly CrystalFault TooClose = new("Judgment Crystals dropped too close together", "dropped their Judgment Crystal too close to another");
    private static readonly CrystalFault ByAlexanderPrime = new("a Judgment Crystal dropped by Alexander Prime", "dropped their Judgment Crystal by Alexander Prime");
    private static readonly CrystalFault Missed = new("a Judgment Crystal the True Heart missed", "dropped their Judgment Crystal out of the True Heart's way");

    private bool OnTheGround(SimEnemy crystal) => crystal.IsActive && !touchedCrystals.Contains(crystal) && !failedCrystals.Contains(crystal);

    // Every crystal with another's zone touching its own, each of a group as one.
    private void FailCrystalsTouchingEachOther()
    {
        var touching = crystals.Where(c => OnTheGround(c.Crystal) && crystals.Any(o => o.Crystal != c.Crystal && OnTheGround(o.Crystal)
            && Vector2.Distance(Flat(o.Crystal.Position), Flat(c.Crystal.Position)) <= 2f * CrystalRadius)).ToList();
        foreach (var (crystal, voidzone, _) in touching) Fail(crystal, voidzone, TooClose, shatterTaken);
    }

    private void FailCrystalsTouching(Vector3? alexanderPrimeAt, CrystalFault fault, Dictionary<SimCharacter, float> taken)
    {
        if (alexanderPrimeAt is not { } at || alexanderPrime is not { } boss) return;
        foreach (var (crystal, voidzone, _) in crystals.Where(c => OnTheGround(c.Crystal)).ToList())
            if (Vector2.Distance(Flat(crystal.Position), Flat(at)) <= boss.HitboxRadius + CrystalRadius)
                Fail(crystal, voidzone, fault, taken);
    }

    // Out of the heart's reach from now, and spent as it goes off. UNVERIFIED: that it goes as a
    // shattered one does.
    private void Fail(SimEnemy crystal, TeaDotPuddles.Puddle voidzone, CrystalFault fault, Dictionary<SimCharacter, float> taken)
    {
        failedCrystals.Add(crystal);
        crystalFaults.TryAdd(crystal, fault);
        var at = crystal.Position;
        DiagnosticLog.Info($"[TeaP3AlexanderPrime] Judgment Crystal at ({at.X:F1},{at.Z:F1}) set off: {fault.Heart}.");
        crystal.Cast(TeaActions.TetrashatterFail(taken, $"the blast of {fault.Heart}",
            () => world.Events.Add(CastSpec.ActionEffectOffset, () => Spend(crystal, voidzone))));
    }

    // Every crystal the heart never shattered, and any never dropped.
    private string? WhyTheHeartFailed()
    {
        var why = crystals.Where(c => !touchedCrystals.Contains(c.Crystal)).Select(c => crystalFaults.GetValueOrDefault(c.Crystal, Missed).Heart).ToList();
        if (crystals.Count < CrystalCount) why.Add("a Judgment Crystal never dropped");
        return why.Count == 0 ? null : string.Join(", ", why.Distinct());
    }

    private Dictionary<SimCharacter, string> CrystalMistakes()
        => crystals.Where(c => !touchedCrystals.Contains(c.Crystal) && c.Dropper.IsAlive())
            .ToDictionary(c => c.Dropper, c => crystalFaults.GetValueOrDefault(c.Crystal, Missed).Dropper);

    // Straight down the centre line toward Alexander Prime.
    private void HeartWalks()
    {
        if (trueHeart is not { IsActive: true } heart) return;
        heartWalking = true;
        heartSteps = 0;
        var stop = SacramentRadius - (alexanderPrime?.HitboxRadius ?? 0f) - heart.HitboxRadius;
        heart.MoveTo(InceptionSide(new Vector3(0f, 0f, stop)), speed: HeartSpeed);
        NextHeartStep();
    }

    // On the walk's own step clock, so a late frame doesn't push back the steps after it.
    private void NextHeartStep() => world.Events.Add(HeartWalksAt + ++heartSteps * HeartStep - world.Events.Elapsed, HeartSteps);

    // Each crystal in reach on a step shatters; the fourth stops it there and turns it gold. Reaching
    // Alexander Prime first, on the last step short of him, it goes into him and powers him up; the
    // crystals it missed stay (real pulls bled by them at 87s). UNVERIFIED: his Vulnerability Down.
    private void HeartSteps()
    {
        if (!heartWalking || trueHeart is not { IsActive: true } heart) return;
        foreach (var (crystal, voidzone, _) in crystals)
        {
            if (!OnTheGround(crystal)) continue;
            if (Vector2.Distance(Flat(crystal.Position), Flat(heart.Position)) > HeartReach) continue;
            touchedCrystals.Add(crystal);
            Shatter(crystal, voidzone);
            if (touchedCrystals.Count < CrystalCount) continue;
            heartWalking = false;
            heart.StopMoving();
            if (punisher is { } helper)
            {
                helper.SetPosition(new Placement(heart.Position, 0f));
                helper.Cast(TeaActions.TetrashatterVisual);
            }
            world.Events.Add(GoldAfterShatter, TurnHeartGold);
            return;
        }
        if (heart.MoveDestination is { } stop && Vector2.Distance(Flat(heart.Position), Flat(stop)) > HeartSpeed * HeartStep)
        {
            NextHeartStep();
            return;
        }
        heartWalking = false;
        DiagnosticLog.Info($"[TeaP3AlexanderPrime] The True Heart reached Alexander Prime after {touchedCrystals.Count} crystals: {WhyTheHeartFailed()}.");
        heart.FadeOut();
        alexanderPrime?.AddStatus(StatusId.DamageUp);
        alexanderPrime?.AddStatus(StatusId.VulnerabilityDownAlexanderPrime);
    }

    private void Shatter(SimEnemy crystal, TeaDotPuddles.Puddle voidzone)
    {
        if (!crystal.IsActive) return;
        crystal.Cast(TeaActions.Tetrashatter(shatterTaken));
        Spend(crystal, voidzone);
        world.Events.Add(VulnerabilityDownAfterShatter, () =>
        {
            if (trueHeart is { IsActive: true } heart) heart.AddStatus(StatusId.VulnerabilityDown);
        });
    }

    private void Spend(SimEnemy crystal, TeaDotPuddles.Puddle voidzone)
    {
        voidzone.Fade();
        world.Events.Add(2.0f, () => crystal.Defeat(corpseFadeDelay: 8f));
        world.Events.Add(3.1f, voidzone.Despawn);
    }

    private void TurnHeartGold()
    {
        if (trueHeart is not { IsActive: true } heart) return;
        heartGold = true;
        heartGoldAt = world.Events.Elapsed;
        heart.AddStatusParam(StatusId.EnigmaCodexHeart, 0x171);
    }

    private static Vector3 SacramentSpot(int spot) => Geometry.OnCompass(22.5f + 45f * spot, SacramentRadius);

    // Gold, it flies straight to where Alexander Prime will stand for Sacrament.
    private void HeartFlies()
    {
        if (!heartGold || trueHeart is not { IsActive: true } heart) return;
        heart.MoveTo(SacramentSpot(state.SacramentSpot), speed: HeartSpeed);
    }

    // Alexander Prime, still unseen, takes his Sacrament spot facing the centre; a gold heart grants
    // everyone Enigma Codex.
    private void TrueHeartSucceeds()
    {
        var spot = SacramentSpot(state.SacramentSpot);
        alexanderPrime?.SetPosition(new Placement(spot, MathF.Atan2(-spot.X, -spot.Z)));
        if (!heartGold || trueHeart is not { IsActive: true } heart) return;
        heart.SetHpFraction(0.001f);
        heart.Cast(TeaActions.TrueHeartSuccess);
        world.Events.Add(1.1f, () => heart.Defeat());
    }

    // -- Inception: flames, debuffs and Sacrament ------------------------------------------

    // At the closest player not yet baited in this set; the flame goes along his facing and burns
    // whoever is in it. On the flames' own clock, so a late frame doesn't push back the ones after it.
    private void TurnToClosestUnflamed(float turnAt, int flames)
    {
        if (bruteJustice is not { } boss) return;
        if (party.ActiveMembers().Where(m => m.IsAlive() && !flareBaits.Contains(m))
                .MinBy(m => Vector2.Distance(Flat(m.Position), Flat(boss.Position))) is not { } bait) return;
        flareBaits.Add(bait);
        var flameAt = turnAt + boss.SecondsToFace(bait.Position);
        boss.Face(bait.Position);
        world.Events.Add(flameAt - world.Events.Elapsed, () => boss.Cast(TeaActions.FlarethrowerP3));
        if (flames > 1)
            world.Events.Add(flameAt + NextFlameTurn - world.Events.Elapsed, () => TurnToClosestUnflamed(flameAt + NextFlameTurn, flames - 1));
    }

    private SimCharacter? Living(PartyRole role) => party.Get(role) is { } member && member.IsAlive() ? member : null;

    private void GiveInceptionDebuffs()
    {
        var tank = Living(state.RestrainingOrderTank);
        var dps = Living(state.RestrainingOrderDps);
        tank?.AddStatus(StatusId.RestrainingOrder);
        dps?.AddStatus(StatusId.RestrainingOrder);
        if (tank is not null && dps is not null) restrainingOrderTether = world.Tether(dps, tank, TetherId.RestrainingOrder);
        Living(state.SharedSentence)?.AddStatus(StatusId.SharedSentence);
        foreach (var role in state.AggravatedAssault) Living(role)?.AddStatus(StatusId.AggravatedAssault);
    }

    private void ClearInceptionDebuffs()
    {
        foreach (var member in party.ActiveMembers())
        {
            member.RemoveStatus(StatusId.RestrainingOrder);
            member.RemoveStatus(StatusId.SharedSentence);
            member.RemoveStatus(StatusId.AggravatedAssault);
        }
        restrainingOrderTether?.Despawn();
    }

    // Severity marks each Aggravated Assault, Solidarity stacks on Shared Sentence, and Surety tests
    // the Restraining Order pair's distance. A True Heart that reached Alexander Prime makes Severity
    // and the stack lethal, and Surety lands for 6.
    private void InceptionPlaints()
    {
        var free = alexanderPrimeHelpers.Where(h => h.IsActive).ToList();
        var next = 0;
        var poweredUp = alexanderPrime?.HasStatus(StatusId.DamageUp) == true;
        var why = poweredUp ? WhyTheHeartFailed() : null;
        foreach (var role in state.AggravatedAssault)
            if (Living(role) is { } assaulted && next < free.Count)
                free[next++].Cast(poweredUp ? TeaActions.PlaintOfSeverityPoweredUp(why) : TeaActions.PlaintOfSeverity, assaulted);
        if (Living(state.SharedSentence) is { } sentenced && next < free.Count)
        {
            var helper = free[next++];
            helper.SetPosition(new Placement(sentenced.Position, 0f));
            helper.Cast(poweredUp ? TeaActions.PlaintOfSolidarityPoweredUp(why) : TeaActions.PlaintOfSolidarity, sentenced);
        }
        if (Living(state.RestrainingOrderTank) is not { } tank || Living(state.RestrainingOrderDps) is not { } dps || next >= free.Count) return;
        var apart = Vector2.Distance(Flat(tank.Position), Flat(dps.Position));
        free[next].Cast(TeaActions.PlaintOfSurety(tank, dps, apart >= RestrainingOrderReach ? null : $"Restraining Order partners only {apart:F1}y apart",
            poweredUp ? 6u : 1u));
    }

    // From the centre at the closest player not yet slashed in this set. The client plays a slash
    // along the model's facing, so Cruise Chaser turns by a teleport on his own spot before each.
    private void TurnToClosestUnslashed()
    {
        if (cruiseChaser is not { } boss) return;
        swordBait = party.ActiveMembers().Where(m => m.IsAlive() && !swordBaits.Contains(m))
            .MinBy(m => Vector2.Distance(Flat(m.Position), Flat(boss.Position)));
        if (swordBait is null) return;
        swordBaits.Add(swordBait);
        boss.SetRotation(boss.Placement().Face(swordBait.Position).Rotation);
    }

    private void SlashAtBait()
    {
        if (cruiseChaser is { } boss && swordBait is { } bait) boss.Cast(TeaActions.AlphaSwordP3(bait));
        swordBait = null;
    }

    // Brute Justice lands on his farthest player.
    private void SuperJumpOnFarthest()
    {
        if (bruteJustice is not { } boss || FarthestFrom(boss.Position) is not { } bait) return;
        boss.SetPosition(new Placement(bait.Position, boss.Rotation));
        boss.Cast(bait.HasStatus(StatusId.PhysicalVulnerabilityUp) ? TeaActions.SuperJumpLandingVulnerable : TeaActions.SuperJumpLanding, bait);
    }

    private SimCharacter? FarthestFrom(Vector3 from)
        => party.ActiveMembers().Where(m => m.IsAlive()).MaxBy(m => Vector2.Distance(Flat(m.Position), Flat(from)));

    // -- Wormhole -------------------------------------------------------------------------

    // Alexander Prime south; Brute Justice and Cruise Chaser on the north intercardinals, either way round.
    private void WormholeLayout()
    {
        var west = new Placement(new Vector3(-15.2f, 0f, -15.2f), MathF.PI / 4f);
        var east = new Placement(new Vector3(15.2f, 0f, -15.2f), -MathF.PI / 4f);
        alexanderPrime?.SetPosition(new Placement(new Vector3(0f, 0f, SacramentRadius), MathF.PI));
        bruteJustice?.SetPosition(state.BruteJusticeNorthWest ? west : east);
        cruiseChaser?.SetPosition(state.BruteJusticeNorthWest ? east : west);
        foreach (var boss in new[] { alexanderPrime, bruteJustice, cruiseChaser })
            boss?.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    private SimCharacter? Numbered(int k) => Living(state.Numbers[k]);

    // Dead players get none; nothing removes a marker, it plays out on its own.
    private void AttachNumber(int k) => Numbered(k)?.AttachLockonVfx(LimitCutLockons[k]);

    // North, and on Brute Justice's side, each aimed at a player of its own picking.
    private void SpawnChakrams()
    {
        state.Objects.Chakrams.Clear();
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        foreach (var from in new[] { new Vector3(0f, 0f, -21.5f), new Vector3(state.BruteJusticeNorthWest ? -21.5f : 21.5f, 0f, 0f) })
        {
            var aim = alive.Count > 0 ? state.PickRandom(alive).Position : Vector3.Zero;
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SteamChakram, NameId: BNpcNameId.SteamChakram, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.HiddenUntilPopIn,
                    Placement: new Placement(from, MathF.Atan2(aim.X - from.X, aim.Z - from.Z)))) is { } chakram)
                state.Objects.Chakrams.Add(chakram);
        }
    }

    public static Vector3[] WormholeSpots(bool mirrored)
        => mirrored ? [new(-11.09f, 0f, 4.59f), new(11.09f, 0f, -4.59f)] : [new(-11.09f, 0f, -4.59f), new(11.09f, 0f, 4.59f)];

    // Each size is a new pair of objects in the same spots.
    private void SpawnWormholes(int size, uint eobjId, uint arg2)
    {
        var spots = WormholeSpots(state.PuddlesMirrored);
        for (var k = 0; k < 2; k++)
            wormholes[size, k] = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = eobjId,
                Placement = new Placement(spots[k], 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = EObjId.WormholesEntityId + (uint)(2 * size + k),
                TargetableStatus = 5,
                Arg2 = arg2 + ((uint)k << 16),
            });
    }

    private void WormholesBeat(int size, uint beat, uint mask)
    {
        for (var k = 0; k < 2; k++) wormholes[size, k]?.PlayBeat(beat, mask);
    }

    private void DespawnWormholes(int size)
    {
        for (var k = 0; k < 2; k++)
        {
            wormholes[size, k]?.Despawn();
            wormholes[size, k] = null;
        }
    }

    // Each wormhole takes its soak from a helper on its centre.
    private void Repent(EnemyAction repentance)
    {
        var spots = WormholeSpots(state.PuddlesMirrored);
        foreach (var (spot, helper) in spots.Zip(alexanderPrimeHelpers.Where(h => h.IsActive)))
        {
            helper.SetPosition(new Placement(spot, 0f));
            helper.Cast(repentance);
        }
    }

    private void WarpBehindOddAt(float hitAt, int number)
    {
        At(hitAt - WarpLead, () => MoveBehind(number));
        At(hitAt - WarpLead + 0.12f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd));
    }

    // 2y behind the odd number, facing where they face. With no odd number alive it warps in place.
    private void MoveBehind(int number)
    {
        if (cruiseChaser is not { } boss || Numbered(number) is not { } odd) return;
        var back = new Vector3(MathF.Sin(odd.Rotation), 0f, MathF.Cos(odd.Rotation)) * -WarpBehind;
        boss.SetPosition(new Placement(odd.Position + back, odd.Rotation));
    }

    private void CruiseChaserHitAt(float hitAt, int number)
    {
        At(hitAt - TurnLead, () => TurnToNumber(number));
        At(hitAt, () => CruiseChaserHits(number));
    }

    // Odd numbers take Alpha Sword from behind, even numbers the charge from where Cruise Chaser
    // last slashed; the hit goes along this facing even if its bait moves or dies before it. A dead
    // number's hit goes to another living player, at random.
    private void TurnToNumber(int number)
    {
        numberBaits[number] = null;
        if (cruiseChaser is not { } boss) return;
        var target = Numbered(number);
        if (target is null)
        {
            var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
            if (alive.Count == 0) return;
            target = state.PickRandom(alive);
        }
        numberBaits[number] = target;
        boss.SetRotation(boss.Placement().Face(target.Position).Rotation);
    }

    private void CruiseChaserHits(int number)
    {
        if (cruiseChaser is not { } boss || numberBaits[number] is not { } bait) return;
        boss.Cast(number % 2 == 0 ? TeaActions.AlphaSword(bait) : TeaActions.SuperBlasstyChargeP3(bait));
    }

    // At whoever is farthest as it starts; the five rays keep that line.
    private void ApocalypticRay()
    {
        if (bruteJustice is not { } boss || FarthestFrom(boss.Position) is not { } aim) return;
        var from = boss.Position;
        rayFacing = MathF.Atan2(aim.Position.X - from.X, aim.Position.Z - from.Z);
        boss.TurnTo(rayFacing);
        boss.Cast(TeaActions.ApocalypticRay);
    }

    private void RayHit()
    {
        if (bruteJustice is not { } boss || bruteJusticeHelpers.FirstOrDefault(h => h.IsActive) is not { } helper) return;
        helper.SetPosition(new Placement(boss.Position, rayFacing));
        helper.Cast(TeaActions.ApocalypticRayHit);
    }

    private void IncineratingHeat()
    {
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        if (alexanderPrime is not { } boss || alive.Count == 0) return;
        var target = state.PickRandom(alive);
        target.AttachLockonVfx(LockonId.IncineratingHeat);
        boss.Cast(TeaActions.IncineratingHeat, target);
    }

    private void MarkEnumerations()
    {
        foreach (var role in state.Enumerations) Living(role)?.AttachLockonVfx(LockonId.Enumeration);
    }

    private void ResolveEnumerations()
    {
        foreach (var (member, helper) in state.Enumerations.Select(Living).OfType<SimCharacter>().Zip(bruteJusticeHelpers.Where(h => h.IsActive)))
        {
            helper.SetPosition(new Placement(member.Position, 0f));
            helper.Cast(TeaActions.Enumeration, member.Position);
        }
    }

    // -- Summon Alexander: the robots and Divine Judgment ------------------------------------

    // Alexander Prime invincible to the north, the robots south-west and south-east.
    private void AddPhaseLayout()
    {
        if (alexanderPrime is { } boss)
        {
            boss.KeepFacing(null);
            boss.SetTarget(null);
            boss.SetTargetable(false);
            boss.SetVisibleInEnemyList(false);
            boss.AddStatus(StatusId.Invincibility);
            boss.SetPosition(new Placement(new Vector3(0f, 0f, -12f), 0f));
        }
        bruteJustice?.SetPosition(new Placement(new Vector3(-9f, 0f, 6f), 0f));
        cruiseChaser?.SetPosition(new Placement(new Vector3(9f, 0f, 6f), 0f));
    }

    // Divine Judgment's bar runs until the kill or the enrage; Cruise Chaser's Eternal Darkness is
    // the robots' own clock. Both robots tether to Alexander Prime.
    private void DivineJudgmentAndEternalDarkness()
    {
        if (alexanderPrime is { } boss)
        {
            boss.Cast(TeaActions.DivineJudgment);
            for (var k = 0; k < 2; k++)
                if ((k == 0 ? bruteJustice : cruiseChaser) is { } robot)
                    robotTethers[k] = world.Tether(robot, boss, TetherId.Plasmasphere);
        }
        cruiseChaser?.Cast(TeaActions.EternalDarknessP3);
        bruteJustice?.SetModelState(4);
    }

    // Whatever Brute Justice does after he goes down never happens, so a kill ends it there.
    private void WhileBruteJusticeUp(float time, Action action, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (settingsWindow.Overrides.CruiseChaserSurvives || time < BruteJusticeDownAt) At(time, action, file, line);
    }

    // Each wave stacks him a Damage Up.
    private void JWave()
    {
        if (bruteJusticeHelpers.FirstOrDefault(h => h.IsActive) is not { } helper || bruteJustice is not { } boss) return;
        helper.SetPosition(boss.Placement());
        helper.Cast(TeaActions.JWave);
    }

    // A kill falling short leaves the robots' enrage, or Divine Judgment's, to go off.
    private void ScheduleDowns()
    {
        var overrides = settingsWindow.Overrides;
        if (overrides.CruiseChaserSurvives) return;
        At(CruiseChaserDownAt, CruiseChaserGoesDown);
        At(BruteJusticeDownAt, BruteJusticeGoesDown);
        if (overrides.AlexanderPrimeSurvives)
        {
            At(DivineJudgmentEnds, DivineJudgmentEnrage);
            return;
        }
        const float success = AlexanderPrimeDownAt + SuccessAfterDown;
        At(AlexanderPrimeDownAt, AlexanderPrimeGoesDown);
        At(success, () => alexanderPrime?.Cast(TeaActions.DivineJudgmentSuccess));
        At(success + EndAfterSuccess, AlexanderPrimeVanishes);
        At(success + 0.75f, () => countdown?.Cast(TeaActions.DivineJudgmentCountdown, Vector3.Zero));
        foreach (var (after, line, ms) in CountdownLines)
            At(success + after, () => world.Map.BattleTalk(BNpcNameId.AlexanderCountdown, line, ms));
        At(success + RaidwideAfterSuccess, () => alexander?.Cast(TeaActions.DivineJudgmentRaidwide));
    }

    private static readonly (float After, uint Line, uint Ms)[] CountdownLines =
    [
        (0.75f, BattleTalkId.ExecutingJudgment, 4000),
        (5.69f, BattleTalkId.FiveSeconds, 1000),
        (6.70f, BattleTalkId.FourSeconds, 1000),
        (7.60f, BattleTalkId.ThreeSeconds, 1000),
        (8.69f, BattleTalkId.TwoSeconds, 1000),
        (9.62f, BattleTalkId.OneSecond, 1000),
    ];

    private void CruiseChaserGoesDown() => RobotLeaves(cruiseChaser, 1);

    // Alexander Prime loses his invincibility with the second robot.
    private void BruteJusticeGoesDown()
    {
        RobotLeaves(bruteJustice, 0);
        alexanderPrime?.RemoveStatus(StatusId.Invincibility);
    }

    // Its cast called off, untargetable and warped out.
    private void RobotLeaves(SimEnemy? robot, int tether)
    {
        robotTethers[tether]?.Despawn();
        if (robot is not { IsActive: true }) return;
        if (robot.IsCasting) robot.CancelCast(CastCancelReason.Interrupted);
        robot.SetTargetable(false);
        robot.SetVisibleInEnemyList(false);
        robot.PlayActionTimeline(ActionTimelineId.WarpStart);
        DiagnosticLog.Info($"[TeaP3AlexanderPrime] {robot.DisplayName} down.");
    }

    // Divine Judgment stops, and its success follows.
    private void AlexanderPrimeGoesDown()
    {
        if (alexanderPrime is not { IsActive: true } boss) return;
        if (boss.IsCasting) boss.CancelCast(CastCancelReason.Interrupted);
        DiagnosticLog.Info("[TeaP3AlexanderPrime] Alexander Prime down.");
    }

    private void AlexanderPrimeVanishes()
    {
        if (alexanderPrime is not { } boss) return;
        boss.SetTargetable(false);
        boss.SetVisibleInEnemyList(false);
        boss.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    // Divine Judgment completing ends the party; only his defeat cuts its bar short.
    private void DivineJudgmentEnrage()
    {
        if (!alexanderPrime.IsAlive()) return;
        party.WipeAllPlayers(ActionId.DivineJudgmentEnrage, "Alexander Prime wasn't down before Divine Judgment");
    }

    // -- Helpers ----------------------------------------------------------------------------

    // The real helpers carry the name of the boss whose attack they fire.
    private void SpawnHelpers(List<SimEnemy> pool, int count, uint nameId)
    {
        for (var i = 0; i < count; i++)
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Helper, NameId: nameId, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(Vector3.Zero, 0f))) is { } helper)
                pool.Add(helper);
    }

    private static string RoleName(SimCharacter member) => (member as ISimPartyMember)?.Role.ToString() ?? "?";

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
