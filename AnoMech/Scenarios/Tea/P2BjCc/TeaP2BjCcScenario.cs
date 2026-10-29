using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P2BjCc;

// Brute Justice and Cruise Chaser from J Kick to the slowest real kill, which beats their double
// enrage by 0.08s; the Temporal Stasis intermission that follows is its own scenario.
// Scenario time 0 is 2s before J Kick.
public sealed class TeaP2BjCcScenario : IMultiplayerReplayable
{
    private const float BossSpeed = 7f;
    // Both robots close in to about a yalm past touching hitboxes.
    private const float RobotReach = 1f;
    private const float NisiSeconds = 36f;
    private const float CompressedSeconds = 29f;
    private const float ResistanceDownSeconds = 31f;
    // Final Judgment shows 45s and is sent again about every 3s until Gavel clears it.
    private const float JudgmentSeconds = 45f;
    private const float JudgmentRefreshEvery = 3f;
    private const float ChakramRadius = 21.5f;
    private const float ShieldAhead = 4.26f;
    // Hits from further round than this off its facing do nothing.
    private const float ShieldFrontHalfAngle = 40f;
    // Five players in front break it in about 2.8s.
    private const float ShieldDrainPerPlayer = 0.071f;
    private const float ShieldReach = 25f;
    // Unbroken by then, it goes, and Cruise Chaser comes out of it with Damage Up.
    private const float ShieldLife = 5.96f;
    // The ice's radius past the tornado's 4y hitbox. UNVERIFIED: the first ice froze from 9.45y and
    // not from 11.77y; the grown ice froze from 12.38y.
    private const float SmallIceReach = 10f;
    private const float LargeIceReach = 13f;
    private const float GaolAfterIce = 1.57f;
    private const float TornadoFadesBeforeGaol = 0.38f;
    private const float MineTrigger = 2f;
    // UNVERIFIED: the reach, between 9.7y (drained) and 10.5y (left asleep); the sheet's 6y past the
    // tornado's 4y hitbox stands in.
    private const float DrainageReach = 10f;
    // The first ice harms only from 3.7s after its missile lands.
    private const float SmallIceSets = 2.77f;
    private const float GaolRadius = 2.88f;
    // The engine builds a spawned actor up to ~0.3s later and drops an instant cast made before then.
    private const float HelperLead = 1f;
    // The slowest real kill.
    private const float RobotsLeave = 195.87f;
    private const float AutoEvery = 3.03f;

    private static readonly (ushort Status, uint Lockon)[] Compressed =
        [(StatusId.CompressedWater, LockonId.CompressedWater), (StatusId.CompressedLightning, LockonId.CompressedLightning)];

    public string Name => "BJ/CC";
    public IPhase Phase => TeaZone.P2;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaP2BjCcAi(lightningThreeToOffTank: false), new TeaP2BjCcAi(lightningThreeToOffTank: true)];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;

    // The P1 track as Limit Cut leaves it; P2's starts with J Kick.
    public float BgmSecondsAtStart => 141.06f;

    private readonly TeaP2BjCcSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaP2BjCcState state = null!;

    private SimEnemy? bruteJustice;
    private SimEnemy? cruiseChaser;
    private SimEnemy? punisher;
    private readonly bool[] down = new bool[2];
    private readonly List<SimEnemy> helpers = [];
    // Each Compressed's current holder; the scenario hands both out.
    private readonly SimCharacter?[] holders = new SimCharacter?[2];
    // Whom each element went off on, until its hit says who carries the next: the hit can't name its target.
    private readonly SimCharacter?[] wentOffOn = new SimCharacter?[2];
    private readonly List<Vector3> aimSpots = [];
    private readonly List<Vector3> hawkSpots = [];
    private readonly List<Vector3> missileSpots = [];
    private readonly List<Vector3> minefieldSpots = [];
    private readonly List<(Vector3 At, bool Popped)> mines = [];
    private readonly TeaDotPuddles puddles = new();
    private readonly TeaDotPuddles.Puddle?[] missilePuddles = new TeaDotPuddles.Puddle?[2];
    private TeaDotPuddles.Puddle? iceSmall;
    private TeaDotPuddles.Puddle? iceLarge;
    private Vector3? iceDrop;
    private float shieldHp;
    private bool cleared, gavelFailed;
    private readonly HashSet<SimCharacter> sentenced = [];
    private float rayFacing;
    // Its hits follow only a ray that went off.
    private bool rayFired;

    public TeaP2BjCcState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaP2BjCcState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        bruteJustice = cruiseChaser = punisher = null;
        Array.Clear(down);
        Array.Clear(holders);
        Array.Clear(wentOffOn);
        helpers.Clear();
        aimSpots.Clear();
        hawkSpots.Clear();
        missileSpots.Clear();
        minefieldSpots.Clear();
        mines.Clear();
        Array.Clear(missilePuddles);
        iceSmall = iceLarge = null;
        puddles.Start(world, settingsWindow.Overrides.TickClock);
        iceDrop = null;
        shieldHp = 0f;
        cleared = gavelFailed = rayFired = false;
        sentenced.Clear();
        DiagnosticLog.Info($"[TeaP2BjCc] Roll: nisi on {(state.NisiOnDps ? "DPS" : "supports")} "
                           + string.Join(" ", state.Nisi.Select(kv => $"{kv.Key}={kv.Value}")) + ", judgment "
                           + string.Join(" ", state.Judgment.Select(kv => $"{kv.Key}={kv.Value}")) + $", water {state.FirstWater}, lightning {state.FirstLightning}, "
                           + $"chakram axis {state.ChakramAxis}, enumerations {string.Join(" ", state.Enumerations)}, ice {state.IceTank}.");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaP2BjCcState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnRobots);
        world.Events.Add(1.91f, () => world.Map.BattleTalk(BNpcNameId.BruteJustice, BattleTalkId.BruteJusticeArrives, 4000));
        world.Events.Add(2.00f, JKick);
        world.Events.Add(5.07f, RobotsTurnTargetable);
        for (var t = 10.17f; t < RobotsLeave; t += AutoEvery)
            world.Events.Add(t, Autos);

        world.Events.Add(10.19f, () => CastBy(Robot.CruiseChaser, TeaActions.Whirlwind));
        world.Events.Add(13.19f, () => CastBy(Robot.BruteJustice, TeaActions.JudgmentNisi));
        world.Events.Add(17.95f, GiveNisis);
        world.Events.Add(20.31f, () => CastBy(Robot.BruteJustice, TeaActions.LinkUp));
        world.Events.Add(23.31f, AimChakrams);
        world.Events.Add(24.11f, GiveFirstCompressed);
        world.Events.Add(24.27f, SpawnChakrams);
        world.Events.Add(25.17f, () => { foreach (var chakram in state.Objects.Chakrams) chakram.ActorControl.PopIn(); });
        world.Events.Add(26.22f, CastChakrams);
        world.Events.Add(29.33f, () => CastBy(Robot.CruiseChaser, TeaActions.OpticalSight));
        world.Events.Add(31.31f, SnapshotHawkBlasters);
        world.Events.Add(31.51f - HelperLead, () => SpawnHelpers(8, BNpcNameId.CruiseChaser));
        world.Events.Add(31.51f, HawkBlasters);
        world.Events.Add(34.47f, DespawnChakrams);
        world.Events.Add(35.45f, () => CastBy(Robot.CruiseChaser, TeaActions.Photon));
        world.Events.Add(38.79f, () => PhotonHits(party.ActiveMembers()));
        world.Events.Add(39.5f, DespawnHelpers);
        world.Events.Add(45.61f, () => CastBy(Robot.CruiseChaser, TeaActions.SpinCrusher));

        world.Events.Add(53.20f - HelperLead, () => SpawnHelpers(2));
        world.Events.Add(53.20f, () => ResolveCompressed(1));
        world.Events.Add(57.51f, () => CastBy(Robot.BruteJustice, TeaActions.MissileCommand));
        world.Events.Add(58.00f, () => Drain(0));
        world.Events.Add(61.68f - HelperLead, () => SpawnHelpers(7));
        world.Events.Add(61.68f, EarthMissiles);
        world.Events.Add(63.57f, MarkEnumerationsAndIce);
        world.Events.Add(63.67f, HiddenMinefields);
        world.Events.Add(65.50f, PlaceMissileVoidzones);
        world.Events.Add(66.67f, () => { foreach (var at in minefieldSpots) mines.Add((at, false)); });
        world.Events.Add(68.72f, ResolveEnumerationsAndIce);
        world.Events.Add(69.65f, () =>
        {
            iceSmall = SpawnIce(EObjId.VoidzoneIceSmall, 0, 0x520003u, Geometry.SmallIceRadius, SmallIceSets);
            FreezeIfReached(SmallIceReach);
        });
        world.Events.Add(73.55f, () =>
        {
            iceLarge = SpawnIce(EObjId.VoidzoneIceLarge, 1, 0x530003u, Geometry.LargeIceRadius, 0f);
            FreezeIfReached(LargeIceReach);
        });
        world.Events.Add(75.90f, ExplodeUnpoppedMines);
        world.Events.Add(76.40f, () => iceSmall?.Fade());
        world.Events.Add(77.20f, () => { foreach (var puddle in missilePuddles) puddle?.Fade(); });
        world.Events.Add(78.93f, () => GoOffIfLeft(0));
        world.Events.Add(79.30f, () => iceLarge?.Fade());
        world.Events.Add(79.5f, DespawnHelpers);
        world.Events.Add(79.60f, () => iceSmall?.Despawn());
        world.Events.Add(80.40f, () => { foreach (var puddle in missilePuddles) puddle?.Despawn(); });
        world.Events.Add(82.40f, () => iceLarge?.Despawn());

        world.Events.Add(80.60f, () => world.Map.BattleTalk(BNpcNameId.BruteJustice, BattleTalkId.FinalJudgmentProtocol, 4000));
        world.Events.Add(80.69f, () => CastBy(Robot.BruteJustice, TeaActions.Verdict));
        world.Events.Add(82.90f - HelperLead, () => SpawnHelpers(2));
        world.Events.Add(82.90f, () => ResolveCompressed(2));
        world.Events.Add(85.46f, GiveFinalJudgments);
        world.Events.Add(87.70f, () => Drain(1));
        world.Events.Add(87.80f, CruiseChaserLimitCut);
        world.Events.Add(87.83f, () => CastBy(Robot.BruteJustice, TeaActions.Flarethrower));
        world.Events.Add(90.38f, () =>
        {
            cruiseChaser?.AddStatus(StatusId.Invincibility);
            cruiseChaser?.SetAnimationState(1, 1);
        });
        world.Events.Add(92.01f, FlarethrowerAtClosest);
        world.Events.Add(92.85f, () =>
        {
            cruiseChaser?.SetModelState(4);
            cruiseChaser?.SetAnimationState(0, 1);
        });
        world.Events.Add(93.23f, SpawnPlasmaShield);
        world.Events.Add(99.95f, () => CastBy(Robot.CruiseChaser, cruiseChaser?.HasStatus(StatusId.DamageUp) == true
            ? TeaActions.WhirlwindEmpowered
            : TeaActions.Whirlwind));
        world.Events.Add(108.65f, () => GoOffIfLeft(1));
        world.Events.Add(110f, DespawnHelpers);

        world.Events.Add(112.65f - HelperLead, () => SpawnHelpers(2));
        world.Events.Add(112.65f, () => ResolveCompressed(3));
        world.Events.Add(117.45f, () => Drain(2));
        world.Events.Add(125.12f, () => CastBy(Robot.CruiseChaser, PropellerWind()));
        world.Events.Add(128.13f, () => CastBy(Robot.BruteJustice, TeaActions.Gavel));
        world.Events.Add(133.13f, JudgeNisis);
        world.Events.Add(133.88f, ClearTornadoes);
        world.Events.Add(135.18f, ClearP2Statuses);
        world.Events.Add(139.30f, () =>
        {
            DespawnHelpers();
            SpawnHelpers(2, BNpcNameId.CruiseChaser);
        });
        world.Events.Add(140.30f, () => CastBy(Robot.CruiseChaser, TeaActions.Photon));
        world.Events.Add(143.65f, () => PhotonHits(party.ActiveMembers().Where(DamageCheck.IsTank)));
        world.Events.Add(145.0f, DespawnHelpers);
        world.Events.Add(147.35f, () => CastBy(Robot.BruteJustice, TeaActions.DoubleRocketPunch, TankOf(Robot.BruteJustice)));
        world.Events.Add(154.50f, () => CastBy(Robot.BruteJustice, TeaActions.SuperJump));
        world.Events.Add(158.81f, SuperJumpOnFarthest);
        world.Events.Add(161.17f - HelperLead, () => SpawnHelpers(1));
        world.Events.Add(161.17f, ApocalypticRay);
        foreach (var t in (float[])[161.79f, 162.86f, 163.93f, 165.00f, 166.07f])
            world.Events.Add(t, RayHit);
        world.Events.Add(167.57f, () => CastBy(Robot.CruiseChaser, TeaActions.Whirlwind));
        world.Events.Add(175.69f, () => CastBy(Robot.CruiseChaser, TeaActions.Whirlwind));
        world.Events.Add(187.95f, () =>
        {
            CastBy(Robot.BruteJustice, TeaActions.FinalSentenceEnrage);
            CastBy(Robot.CruiseChaser, TeaActions.EternalDarknessP2);
        });
        world.Events.Add(RobotsLeave, () =>
        {
            Leave(Robot.BruteJustice);
            Leave(Robot.CruiseChaser);
        });
    }

    // P2's music starts with J Kick.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(HelperTimelines, "TeaP2BjCc");
        instanceWorld.Events.Add(2.00f, () => instanceWorld.SwitchBgm(Bgm.P2));
    }

    // The helpers' hits play gimmick timelines (LoadType 0), which an actor drops unless they are
    // already loaded. UNVERIFIED in game beside Hawk Blaster's.
    private static readonly (ushort Id, string Key)[] HelperTimelines =
    [
        (7587, "mon_sp/gimmick/d2az_boss_gimmick06"),
        (5056, "mon_sp/gimmick/d2ab2_boss_gimmick04"),
        (4696, "mon_sp/gimmick/d2a6_boss_gimmick02"),
        (4698, "mon_sp/gimmick/d2a6_boss_gimmick03"),
        (4700, "mon_sp/gimmick/d2a6_boss_gimmick04"),
        (4702, "mon_sp/gimmick/d2a6_boss_gimmick05"),
        (4704, "mon_sp/gimmick/d2a6_boss_gimmick06"),
        (4706, "mon_sp/gimmick/d2a6_boss_gimmick07"),
        (4708, "mon_sp/gimmick/d2a6_boss_gimmick08"),
        (4716, "mon_sp/gimmick/d2a6_boss_gimmick12"),
        (2915, "mon_sp/gimmick/bahamut2_souchi_ziraibom_syou"),
        (2917, "mon_sp/gimmick/bahamut2_souchi_ziraibom_dai"),
        (4449, "mon_sp/gimmick/alexfour_hitogata_shinpan"),
    ];

    public void OnActionResolved(ActionResolved resolved) => PassCompressed(resolved);

    public void OnStatusLost(StatusLost lost) => PunishDeadHolder(lost);

    public void Tick(float delta, float elapsed)
    {
        if (state is null) return;
        TouchNisis();
        PopMines();
        DrainShield(delta);
        puddles.Tick();
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new TeaP2BjCcAiReplayStateMessage(s.NisiOnDps, TeaP2BjCcState.Encode(s.Nisi), TeaP2BjCcState.Encode(s.Judgment), s.FirstWater,
                s.FirstLightning, s.ChakramAxis, s.Enumerations.ToArray(), s.IceTank, s.BotPacing)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaP2BjCcAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaP2BjCcState.FromNetworkReplay(msg.NisiOnDps, msg.Nisi, msg.Judgment, msg.FirstWater, msg.FirstLightning, msg.ChakramAxis,
                msg.Enumerations, msg.IceTank, msg.BotPacing) is not { } shadowState) return null;
        ((IScenarioAi<TeaP2BjCcState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The robots, tornadoes, chakrams, gaol and shield may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaP2BjCcState shadow) return;
        var objects = shadow.Objects;
        objects.BruteJustice ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.BruteJustice);
        objects.CruiseChaser ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
        objects.GelidGaol ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.GelidGaol && e.IsActive);
        if (objects.PlasmaShield is not { IsActive: true })
            objects.PlasmaShield = peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.PlasmaShield && e.IsActive);
        foreach (var rage in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.LiquidRage && e.IsActive))
            if (!objects.Tornadoes.Contains(rage)) objects.Tornadoes.Add(rage);
        foreach (var chakram in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.SteamChakram && e.IsActive))
            if (!objects.Chakrams.Contains(chakram)) objects.Chakrams.Add(chakram);
    }

    // -- Robots ----------------------------------------------------------------

    private SimEnemy? RobotOf(Robot robot) => robot == Robot.BruteJustice ? bruteJustice : cruiseChaser;

    private bool Up(Robot robot) => RobotOf(robot) is { IsActive: true } && !down[(int)robot];

    private void CastBy(Robot robot, EnemyAction action, CastTarget target = default)
    {
        if (Up(robot)) RobotOf(robot)!.Cast(action, target);
    }

    private SimCharacter? TankOf(Robot robot) => party.Get(robot == Robot.BruteJustice ? MainTank : OffTank) is { } tank && tank.IsAlive() ? tank : null;

    // Both exist unseen from the start: Brute Justice shows himself with J Kick, Cruise Chaser warps
    // back in beside him.
    private void SpawnRobots()
    {
        state.Objects.BruteJustice = bruteJustice = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BruteJustice, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(-6f, 0f, 0f), 0f)));
        state.Objects.CruiseChaser = cruiseChaser = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.CruiseChaser, NameId: BNpcNameId.CruiseChaser, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(4f, 0f, 0f), 0f)));
        punisher = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(Vector3.Zero, 0f)));
    }

    private void JKick()
    {
        bruteJustice?.Cast(TeaActions.JKick);
        cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        world.Map.SetWeather(Weather.FairSkiesP2, transition: 1f);
        world.Map.AddEffect(0x00020004, 7);
    }

    // Each turns on its tank: Brute Justice the MT, Cruise Chaser the OT.
    private void RobotsTurnTargetable()
    {
        foreach (var robot in new[] { Robot.BruteJustice, Robot.CruiseChaser })
        {
            if (!Up(robot)) continue;
            var boss = RobotOf(robot)!;
            boss.SetTargetable(true);
            boss.SetVisibleInEnemyList(true);
            FollowTank(robot);
        }
    }

    private void FollowTank(Robot robot)
    {
        if (!Up(robot) || TankOf(robot) is not { } tank) return;
        var boss = RobotOf(robot)!;
        boss.SetTarget(tank, follow: false);
        boss.Follow(tank, BossSpeed, RobotReach);
    }

    // Cruise Chaser holds off while invincible.
    private void Autos()
    {
        foreach (var (robot, action) in new[] { (Robot.BruteJustice, TeaActions.BruteJusticeAttack), (Robot.CruiseChaser, TeaActions.CruiseChaserAttack) })
        {
            if (!Up(robot) || RobotOf(robot) is not { IsCasting: false, AnimationLock: false } boss) continue;
            if (boss.HasStatus(StatusId.Invincibility)) continue;
            if (TankOf(robot) is { } target) boss.Cast(action, target);
        }
    }

    // A cast yet to go off goes down with it; an action already going off finishes first.
    private void Leave(Robot robot)
    {
        if (RobotOf(robot) is not { IsActive: true } boss || down[(int)robot]) return;
        down[(int)robot] = true;
        boss.CancelCast(CastCancelReason.Interrupted);
        LeaveWhenFree(robot, boss);
    }

    // Untargetable and warped out; the intermission brings both back.
    private void LeaveWhenFree(Robot robot, SimEnemy boss)
    {
        if (boss.AnimationLock)
        {
            world.Events.Add(0.1f, () => LeaveWhenFree(robot, boss));
            return;
        }
        boss.Follow(null);
        boss.SetTarget(null);
        boss.SetTargetable(false);
        boss.SetVisibleInEnemyList(false);
        boss.PlayActionTimeline(ActionTimelineId.WarpStart);
        DiagnosticLog.Info($"[TeaP2BjCc] {robot} down.");
    }

    // -- Decree Nisi --------------------------------------------------------------

    private void GiveNisis()
    {
        foreach (var (role, letter) in state.Nisi)
            if (party.Get(role) is { } member && member.IsAlive())
                member.AddStatus(StatusId.FinalDecreeNisi(letter), NisiSeconds);
    }

    private void GiveFinalJudgments()
    {
        foreach (var (role, letter) in state.Judgment)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(StatusId.FinalJudgment(letter), JudgmentSeconds);
            member.AddStatus(StatusId.FinalJudgmentPenaltyIII);
        }
        world.Events.Add(JudgmentRefreshEvery, RefreshFinalJudgments);
    }

    private void RefreshFinalJudgments()
    {
        if (cleared) return;
        foreach (var (role, letter) in state.Judgment)
            if (party.Get(role) is { } member && member.IsAlive() && member.HasStatus(StatusId.FinalJudgment(letter)))
                member.AddStatus(StatusId.FinalJudgment(letter), JudgmentSeconds);
        world.Events.Add(JudgmentRefreshEvery, RefreshFinalJudgments);
    }

    private static NisiLetter? NisiOf(SimCharacter member)
    {
        foreach (var letter in Enum.GetValues<NisiLetter>())
            if (member.FindStatus(StatusId.FinalDecreeNisi(letter)) != null) return letter;
        return null;
    }

    // Touching hitboxes copy a holder's nisi onto a player without one, with a fresh timer; the
    // holder's own runs on. Two different letters touching each take the other's and are sentenced.
    private void TouchNisis()
    {
        if (cleared) return;
        var alive = party.ActiveMembers().Where(m => m.IsAlive() && !sentenced.Contains(m)).ToList();
        for (var i = 0; i < alive.Count; i++)
        for (var j = i + 1; j < alive.Count; j++)
        {
            var (a, b) = (alive[i], alive[j]);
            if (Vector2.Distance(Flat(a.Position), Flat(b.Position)) > a.HitboxRadius + b.HitboxRadius) continue;
            var (la, lb) = (NisiOf(a), NisiOf(b));
            if (la == lb) continue;
            if (la is { } x && lb is { } y)
            {
                DiagnosticLog.Info($"[TeaP2BjCc] {RoleName(a)} ({x}) touched {RoleName(b)} ({y}).");
                a.AddStatus(StatusId.FinalDecreeNisi(y), NisiSeconds);
                b.AddStatus(StatusId.FinalDecreeNisi(x), NisiSeconds);
                Sentence(a, "touched a different nisi");
                Sentence(b, "touched a different nisi");
                continue;
            }
            var (giver, taker, letter) = la is { } l ? (a, b, l) : (b, a, lb!.Value);
            taker.AddStatus(StatusId.FinalDecreeNisi(letter), NisiSeconds);
            DiagnosticLog.Info($"[TeaP2BjCc] {RoleName(giver)} passed nisi {letter} to {RoleName(taker)}.");
        }
    }

    private void Sentence(SimCharacter member, string why)
    {
        if (sentenced.Add(member)) party.Act(member, TeaActions.FinalSentence with { DeathExplanation = why }, member);
    }

    // As Gavel lands everyone must hold the nisi their Final Judgment calls for; if anyone doesn't,
    // everyone is sentenced as the statuses clear.
    private void JudgeNisis()
    {
        Array.Clear(holders);
        var wrong = PerRole.All.Where(role => party.Get(role) is not { } member || !member.IsAlive()
                                              || NisiOf(member) != state.Judgment.GetValueOrDefault(role)).ToList();
        if (wrong.Count == 0)
        {
            world.Map.BattleTalk(BNpcNameId.BruteJustice, BattleTalkId.GavelFalls, 4000);
            return;
        }
        DiagnosticLog.Info($"[TeaP2BjCc] Gavel: wrong or missing nisi on {string.Join(" ", wrong)}.");
        world.Map.BattleTalk(BNpcNameId.BruteJustice, BattleTalkId.GavelFails, 4000);
        gavelFailed = true;
    }

    private void ClearP2Statuses()
    {
        cleared = true;
        if (gavelFailed)
            foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()).ToList())
                Sentence(member, "a nisi didn't match its Final Judgment at Gavel");
        foreach (var member in party.ActiveMembers())
        {
            foreach (var letter in Enum.GetValues<NisiLetter>())
            {
                member.RemoveStatus(StatusId.FinalDecreeNisi(letter));
                member.RemoveStatus(StatusId.FinalJudgment(letter));
            }
            member.RemoveStatus(StatusId.FinalJudgmentPenaltyIII);
            member.RemoveStatus(StatusId.CompressedWater);
            member.RemoveStatus(StatusId.CompressedLightning);
            member.RemoveStatus(StatusId.WaterResistanceDownIIP2);
            member.RemoveStatus(StatusId.LightningResistanceDownII);
        }
    }

    // -- Compressed Water and Lightning --------------------------------------------

    private void GiveFirstCompressed()
    {
        Give(0, party.Get(state.FirstWater));
        Give(1, party.Get(state.FirstLightning));
    }

    private void Give(int k, SimCharacter? member)
    {
        holders[k] = member is not null && member.IsAlive() ? member : null;
        if (holders[k] is not { } holder) return;
        holder.AddStatus(Compressed[k].Status, CompressedSeconds);
        holder.AttachLockonVfx(Compressed[k].Lockon);
    }

    // A holder's death sets its element off on the whole party.
    private void PunishDeadHolder(StatusLost lost)
    {
        if (lost.Reason != StatusLossReason.Died) return;
        for (var k = 0; k < 2; k++)
        {
            if (lost.StatusId != Compressed[k].Status || !ReferenceEquals(holders[k], lost.Holder)) continue;
            holders[k] = null;
            var at = lost.Holder.Position;
            var action = k == 0 ? TeaActions.PunishingWave : TeaActions.PunishingThunder;
            world.Events.Add(0.1f, () =>
            {
                punisher?.SetPosition(new Placement(at, 0f));
                punisher?.Cast(action);
            });
        }
    }

    // At expiry each stack goes off on its holder, who takes the element's Resistance Down; a random
    // other player the water hit, never one carrying its Resistance Down, and the lightning holder's
    // partner, carry the next. The water leaves a Liquid Rage where it went off.
    private void ResolveCompressed(int round)
    {
        var free = helpers.Where(h => h.IsActive).Take(2).ToArray();
        for (var k = 0; k < 2 && k < free.Length; k++)
        {
            if (holders[k] is not { } holder || !holder.IsAlive()) continue;
            holders[k] = null;
            var water = k == 0;
            holder.RemoveStatus(Compressed[k].Status);
            var at = holder.Position;
            free[k].SetPosition(new Placement(at, 0f));
            wentOffOn[k] = holder;
            free[k].Cast(water ? TeaActions.CrashingWave : TeaActions.CrashingThunder, holder);
            var resistance = water ? StatusId.WaterResistanceDownIIP2 : StatusId.LightningResistanceDownII;
            world.Events.Add(0.6f, () =>
            {
                if (holder.IsAlive()) holder.AddStatus(resistance, ResistanceDownSeconds);
            });
            if (water) world.Events.Add(round == 1 ? 0.75f : 0.9f, () => SpawnTornado(at));
        }
    }

    // Who carries the next is read 0.68s after the hit, from those it caught alongside its holder.
    private void PassCompressed(ActionResolved resolved)
    {
        var k = resolved.Action.ActionId switch { ActionId.CrashingWave => 0, ActionId.CrashingThunder => 1, _ => -1 };
        if (k < 0 || wentOffOn[k] is not { } holder) return;
        wentOffOn[k] = null;
        var water = k == 0;
        var resistance = water ? StatusId.WaterResistanceDownIIP2 : StatusId.LightningResistanceDownII;
        world.Events.Add(0.68f, () =>
        {
            var others = resolved.Hits.Where(m => !ReferenceEquals(m, holder) && m.IsAlive() && !(water && m.HasStatus(resistance))).ToList();
            Give(k, water ? others.Count > 0 ? state.PickRandom(others) : null : others.Count == 1 ? others[0] : null);
        });
    }

    private void SpawnTornado(Vector3 at)
    {
        if (world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.LiquidRage, NameId: BNpcNameId.LiquidRage, Level: Level,
                Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
                Placement: new Placement(at with { Y = 0f }, 0f))) is { } rage)
            state.Objects.Tornadoes.Add(rage);
    }

    // 4.8s after its water stack a Liquid Rage drains the nearest player in reach; with nobody in
    // reach it never wakes. UNVERIFIED: a woken one drains again every few seconds, unmodelled since
    // the first already kills.
    private void Drain(int k)
    {
        if (Tornado(k) is not { } rage) return;
        var at = Flat(rage.Position);
        if (party.ActiveMembers().Where(m => m.IsAlive()).MinBy(m => Vector2.Distance(Flat(m.Position), at)) is not { } nearest
            || Vector2.Distance(Flat(nearest.Position), at) > DrainageReach) return;
        rage.Cast(TeaActions.DrainageP2, nearest);
    }

    // A Liquid Rage nobody froze or burned goes off on everyone.
    private void GoOffIfLeft(int k)
    {
        if (Tornado(k) is not { } rage) return;
        DiagnosticLog.Info($"[TeaP2BjCc] Tornado {k + 1} was left standing.");
        rage.Cast(TeaActions.SevereContamination);
    }

    private SimEnemy? Tornado(int k) => k < state.Objects.Tornadoes.Count && state.Objects.Tornadoes[k] is { IsActive: true, FadingOut: false } t ? t : null;

    // Ice reaching the first tornado freezes it into the Gelid Gaol.
    private void FreezeIfReached(float reach)
    {
        if (state.Objects.GelidGaol is not null || Tornado(0) is not { } rage || iceDrop is not { } drop) return;
        if (Vector2.Distance(Flat(rage.Position), Flat(drop)) > reach) return;
        var at = rage.Position;
        world.Events.Add(GaolAfterIce - TornadoFadesBeforeGaol, rage.FadeOut);
        world.Events.Add(GaolAfterIce, () => state.Objects.GelidGaol = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.GelidGaol, NameId: BNpcNameId.GelidGaol, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(at, 0f))));
    }

    // Gavel takes the Gelid Gaol and the last tornado. UNVERIFIED: a failed Gavel too.
    private void ClearTornadoes()
    {
        state.Objects.GelidGaol?.FadeOut();
        foreach (var rage in state.Objects.Tornadoes.Where(t => t is { IsActive: true, FadingOut: false })) rage.FadeOut();
    }

    // -- Chakrams and Hawk Blasters -----------------------------------------------------

    private static readonly float[] AxisBearings = [0f, 45f, 90f, 135f];

    // Link-Up's end aims each chakram at a player picked on its own, at times the same one twice.
    private void AimChakrams()
    {
        aimSpots.Clear();
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        if (alive.Count == 0) return;
        aimSpots.Add(state.PickRandom(alive).Position);
        aimSpots.Add(state.PickRandom(alive).Position);
    }

    private void SpawnChakrams()
    {
        state.Objects.Chakrams.Clear();
        var bearing = AxisBearings[state.ChakramAxis];
        for (var k = 0; k < 2; k++)
        {
            var from = Geometry.OnCompass(bearing + 180f * k, ChakramRadius);
            var aim = k < aimSpots.Count ? aimSpots[k] : Vector3.Zero;
            var facing = MathF.Atan2(aim.X - from.X, aim.Z - from.Z);
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SteamChakram, NameId: BNpcNameId.SteamChakram, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.HiddenUntilPopIn,
                    Placement: new Placement(from, facing))) is { } chakram)
                state.Objects.Chakrams.Add(chakram);
        }
    }

    private void CastChakrams()
    {
        foreach (var chakram in state.Objects.Chakrams.Where(c => c.IsActive)) chakram.Cast(TeaActions.EyeOfTheChakram);
    }

    private void DespawnChakrams()
    {
        foreach (var chakram in state.Objects.Chakrams) chakram.Despawn();
    }

    // Each robot's mechanics name their helpers after it.
    private void SpawnHelpers(int count, uint nameId = BNpcNameId.BruteJustice)
    {
        for (var i = 0; i < count; i++)
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Helper, NameId: nameId, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(Vector3.Zero, 0f))) is { } helper)
                helpers.Add(helper);
    }

    private void DespawnHelpers()
    {
        foreach (var helper in helpers) helper.Despawn();
        helpers.Clear();
    }

    // Optical Sight's end marks where every player stands; a Hawk Blaster goes off on each spot.
    private void SnapshotHawkBlasters()
    {
        hawkSpots.Clear();
        hawkSpots.AddRange(party.ActiveMembers().Where(m => m.IsAlive()).Select(m => m.Position));
    }

    private void HawkBlasters()
    {
        foreach (var (at, helper) in hawkSpots.Zip(helpers.Where(h => h.IsActive)))
        {
            helper.SetPosition(new Placement(at, 0f));
            helper.Cast(TeaActions.HawkBlasterP2, at);
        }
    }

    // From Cruise Chaser, one helper per player.
    private void PhotonHits(IEnumerable<SimCharacter> targets)
    {
        var from = cruiseChaser?.Position ?? Vector3.Zero;
        foreach (var (member, helper) in targets.Where(m => m.IsAlive()).Zip(helpers.Where(h => h is { IsActive: true, IsCasting: false })))
        {
            helper.SetPosition(new Placement(from, 0f));
            helper.Cast(TeaActions.PhotonHit, member);
        }
    }

    // -- Missile Command ----------------------------------------------------------------------

    private List<SimCharacter> FarthestFromBruteJustice(int count)
    {
        var from = bruteJustice?.Position ?? Vector3.Zero;
        return party.ActiveMembers().Where(m => m.IsAlive()).OrderByDescending(m => Vector2.Distance(Flat(m.Position), Flat(from))).Take(count).ToList();
    }

    private void EarthMissiles()
    {
        missileSpots.Clear();
        foreach (var (member, helper) in FarthestFromBruteJustice(2).Zip(helpers.Where(h => h.IsActive)))
        {
            missileSpots.Add(member.Position);
            helper.SetPosition(new Placement(member.Position, 0f));
            helper.Cast(TeaActions.EarthMissile, member.Position);
        }
    }

    private void HiddenMinefields()
    {
        minefieldSpots.Clear();
        foreach (var (member, helper) in FarthestFromBruteJustice(2).Zip(helpers.Where(h => h.IsActive).Skip(2)))
        {
            minefieldSpots.Add(member.Position);
            helper.SetPosition(new Placement(member.Position, 0f));
            helper.Cast(TeaActions.HiddenMinefield, member.Position);
        }
    }

    private void PlaceMissileVoidzones()
    {
        for (var k = 0; k < missileSpots.Count && k < 2; k++)
        {
            var at = missileSpots[k] with { Y = 0f };
            missilePuddles[k] = puddles.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.VoidzoneEarthMissile,
                Placement = new Placement(at, 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = EObjId.MissileVoidzonesEntityId + (uint)k,
                TargetableStatus = 5,
                Arg2 = 0x510003u - ((uint)k << 16),
            }), at, Geometry.MissilePuddleRadius, StatusId.Sludge, "Sludge: stood in a missile's puddle");
        }
    }

    private TeaDotPuddles.Puddle? SpawnIce(uint eobjId, int k, uint arg2, float radius, float armedAfter)
    {
        if (iceDrop is not { } drop) return null;
        var at = drop with { Y = 0f };
        return puddles.Add(world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = eobjId,
            Placement = new Placement(at, 0f),
            EventId = EObjId.DirectorEventId,
            EntityId = EObjId.IceVoidzonesEntityId + (uint)k,
            TargetableStatus = 5,
            Arg2 = arg2,
        }), at, radius, StatusId.Frostbite, "Frostbite: stood in the ice", armedAfter);
    }

    private void MarkEnumerationsAndIce()
    {
        foreach (var role in state.Enumerations)
            if (party.Get(role) is { } member && member.IsAlive()) member.AttachLockonVfx(LockonId.Enumeration);
        if (party.Get(state.IceTank) is { } tank && tank.IsAlive()) tank.AttachLockonVfx(LockonId.EarthMissileIce);
    }

    private void ResolveEnumerationsAndIce()
    {
        var free = helpers.Where(h => h is { IsActive: true, IsCasting: false }).Skip(4).Concat(helpers.Where(h => h is { IsActive: true, IsCasting: false }).Take(4)).ToList();
        var k = 0;
        foreach (var role in state.Enumerations)
        {
            if (party.Get(role) is not { } member || !member.IsAlive() || k >= free.Count) continue;
            free[k].SetPosition(new Placement(member.Position, 0f));
            free[k++].Cast(TeaActions.Enumeration, member.Position);
        }
        if (party.Get(state.IceTank) is { } tank && tank.IsAlive() && k < free.Count)
        {
            iceDrop = tank.Position;
            free[k].SetPosition(new Placement(tank.Position, 0f));
            free[k].Cast(TeaActions.EarthMissileIce, tank);
        }
    }

    // The mines are unseen; anyone walking onto one sets it off: a tank takes it, anyone else dies.
    private void PopMines()
    {
        for (var i = 0; i < mines.Count; i++)
        {
            var (at, popped) = mines[i];
            if (popped) continue;
            if (!party.ActiveMembers().Any(m => m.IsAlive() && Vector2.Distance(Flat(m.Position), Flat(at)) <= MineTrigger)) continue;
            mines[i] = (at, true);
            if (helpers.FirstOrDefault(h => h is { IsActive: true, IsCasting: false }) is { } helper)
            {
                helper.SetPosition(new Placement(at, 0f));
                helper.Cast(TeaActions.HiddenMine);
            }
        }
    }

    private void ExplodeUnpoppedMines()
    {
        if (mines.Any(m => !m.Popped)) punisher?.Cast(TeaActions.HiddenMineShrapnel);
        mines.Clear();
    }

    // -- Limit Cut, Plasma Shield, Flarethrower --------------------------------------

    // Cruise Chaser stands still behind the Plasma Shield until it is gone.
    private void CruiseChaserLimitCut()
    {
        if (!Up(Robot.CruiseChaser)) return;
        CastBy(Robot.CruiseChaser, TeaActions.LimitCutP2);
        cruiseChaser!.Follow(null);
    }

    private void SpawnPlasmaShield()
    {
        if (!Up(Robot.CruiseChaser)) return;
        var shield = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.PlasmaShield, NameId: BNpcNameId.PlasmaShield, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, DisplayFlags: 0x40003,
            Placement: cruiseChaser!.Placement().MoveForward(ShieldAhead)));
        state.Objects.PlasmaShield = shield;
        shieldHp = 1f;
        world.Events.Add(ShieldLife, () =>
        {
            if (shield is not { IsActive: true, Dead: false } || shieldHp <= 0f) return;
            shieldHp = 0f;
            shield.Despawn();
            world.Events.Add(0.14f, () =>
            {
                ReleaseCruiseChaser();
                cruiseChaser?.AddStatus(StatusId.DamageUp);
            });
        });
    }

    // Only players in front of it wear it down.
    private void DrainShield(float delta)
    {
        if (state.Objects.PlasmaShield is not { IsActive: true, Dead: false } shield || shieldHp <= 0f) return;
        var facing = new Vector2(MathF.Sin(shield.Rotation), MathF.Cos(shield.Rotation));
        var front = party.ActiveMembers().Count(m => m.IsAlive() && InFront(Flat(m.Position) - Flat(shield.Position)));
        shieldHp = settingsWindow.Overrides.ShieldBreaksAt is { } breaksAt
            ? world.Events.Elapsed < breaksAt ? 1f : 0f
            : shieldHp - ShieldDrainPerPlayer * front * delta;
        shield.SetHpFraction(MathF.Max(shieldHp, 0f));
        if (shieldHp > 0f) return;
        shield.Defeat(corpseFadeDelay: 2.13f);
        ReleaseCruiseChaser();

        bool InFront(Vector2 offset)
        {
            var distance = offset.Length();
            return distance is > 1e-3f and <= ShieldReach
                   && Vector2.Dot(facing, offset / distance) >= MathF.Cos(float.DegreesToRadians(ShieldFrontHalfAngle));
        }
    }

    private void ReleaseCruiseChaser()
    {
        if (cruiseChaser is null) return;
        cruiseChaser.SetModelState(0);
        cruiseChaser.SetAnimationState(0, 0);
        cruiseChaser.RemoveStatus(StatusId.Invincibility);
        FollowTank(Robot.CruiseChaser);
    }

    // At the player closest to Brute Justice as it goes off; it burns the second tornado if it reaches it.
    private void FlarethrowerAtClosest()
    {
        if (!Up(Robot.BruteJustice)) return;
        var from = bruteJustice!.Position;
        if (party.ActiveMembers().Where(m => m.IsAlive()).MinBy(m => Vector2.Distance(Flat(m.Position), Flat(from))) is not { } bait) return;
        bruteJustice.Cast(TeaActions.FlarethrowerCone, bait);
        if (Tornado(1) is not { } rage) return;
        var aim = Flat(bait.Position) - Flat(from);
        var toRage = Flat(rage.Position) - Flat(from);
        if (aim.LengthSquared() > 1e-4f && toRage.LengthSquared() > 1e-4f
            && Vector2.Dot(Vector2.Normalize(aim), Vector2.Normalize(toRage)) >= MathF.Cos(MathF.PI / 4f))
            rage.FadeOut();
    }

    // -- Propeller Wind and after ----------------------------------------------------------

    // Cruise Chaser hits everyone he can see.
    private EnemyAction PropellerWind() => TeaActions.PropellerWind with
    {
        Area = new() { AdjustTargets = (ctx, hits) => hits.Where(h => !BehindGaol(ctx.Caster.Position, h.Position)).ToList() },
    };

    // UNVERIFIED: the Gelid Gaol's shadow as BossMod draws it. In real clears Cruise Chaser stands
    // just inside its far edge, where the shadow is the whole half beyond it.
    private bool BehindGaol(Vector3 from, Vector3 at)
    {
        if (state.Objects.GelidGaol is not { IsActive: true } gaol) return false;
        var toGaol = Flat(gaol.Position) - Flat(from);
        var toPlayer = Flat(at) - Flat(from);
        var gaolDistance = toGaol.Length();
        if (gaolDistance < 1e-3f) return true;
        if (toPlayer.Length() < gaolDistance) return false;
        var halfWidth = MathF.Asin(Math.Clamp(GaolRadius / gaolDistance, 0f, 1f));
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(toGaol / gaolDistance, Vector2.Normalize(toPlayer)), -1f, 1f));
        return angle <= halfWidth;
    }

    // Brute Justice lands on his farthest player.
    private void SuperJumpOnFarthest()
    {
        if (!Up(Robot.BruteJustice) || FarthestFromBruteJustice(1).FirstOrDefault() is not { } bait) return;
        bruteJustice!.Follow(null);
        bruteJustice.SetPosition(new Placement(bait.Position, bruteJustice.Rotation));
        bruteJustice.Cast(TeaActions.SuperJumpLanding, bait);
    }

    // Five rays at whoever is farthest as it starts. Its lock holds Brute Justice there; then he goes
    // back to his tank.
    private void ApocalypticRay()
    {
        if (!Up(Robot.BruteJustice) || FarthestFromBruteJustice(1).FirstOrDefault() is not { } aim) return;
        var from = bruteJustice!.Position;
        rayFacing = MathF.Atan2(aim.Position.X - from.X, aim.Position.Z - from.Z);
        bruteJustice.TurnTo(rayFacing);
        bruteJustice.Cast(TeaActions.ApocalypticRay);
        rayFired = true;
        FollowTank(Robot.BruteJustice);
    }

    private void RayHit()
    {
        if (!rayFired || !Up(Robot.BruteJustice) || helpers.FirstOrDefault(h => h is { IsActive: true, IsCasting: false }) is not { } helper) return;
        helper.SetPosition(new Placement(bruteJustice!.Position, rayFacing));
        helper.Cast(TeaActions.ApocalypticRayHit);
    }

    // -- Helpers ---------------------------------------------------------------------------------

    private static string RoleName(SimCharacter member) => (member as ISimPartyMember)?.Role.ToString() ?? "?";

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
