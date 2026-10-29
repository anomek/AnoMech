using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

// Living Liquid from the pull to its death at the slowest real kill, about 3.4s after the Fluid Swing
// that follows the second Hand of Parting/Prayer. Scenario time 0 is the pull. The enrage, whose cast
// would begin about then, is left out.
public sealed class TeaP1LivingLiquidScenario : IMultiplayerReplayable
{
    private const float LivingLiquidSpeed = 8.7f;
    private const float LiquidHandSpeed = 9.1f;
    private const float JagdDollSpeed = 8.3f;
    private const float EmbolusSpeed = 1.93f;
    private const float EmbolusGoneRadius = 22f;
    private const float EmbolusFadeSeconds = 1.15f;
    // Bosses closer than this at the tell get Prayer, farther Parting.
    private const float PrayerWithin = 15.5f;
    // Parting passes with the two this close. UNVERIFIED: between about 7.0y and 7.9y.
    private const float TogetherWithin = 7.1f;
    // Prayer passes with the two this far apart. UNVERIFIED: between about 17y and 18.4y.
    private const float ApartFrom = 17.5f;
    // The same for both bosses, centre to centre. UNVERIFIED: about 4.6y, the farthest real feeds went off from.
    private const float DollFeedReach = 4.6f;
    // Dropsy's damage KOs 2.1s or more after it lands, later on more HP; the sim takes the earliest. In game it lifts
    // about 0.3-0.6s after stepping out; here only an invulned holder's does.
    private const float DropsySeconds = 2.1f;
    private const byte HandOfPrayerTell = 19;
    private const byte HandOfPartingTell = 20;
    // A provoked boss turns to its new tank 1.1-1.6s after the press.
    private const float ProvokeTurnSeconds = 1.3f;
    // A doll fed at this HP or more sets off a lethal Reducible Complexity.
    private const float DollFeedHp = 0.25f;

    public string Name => "Living Liquid";
    public IPhase Phase => TeaZone.P1;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaP1LivingLiquidAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private readonly TeaP1LivingLiquidSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaP1LivingLiquidState state = null!;
    private Rng autoVariation = null!;
    private Rng dollHitSizes = null!;
    private Rng drainageStandIns = null!;

    private readonly SimEnemy?[] firstRages = new SimEnemy?[3];
    private readonly SimEnemy?[] secondRages = new SimEnemy?[3];
    private readonly SimEventObject?[] firstVoidzones = new SimEventObject?[3];
    private readonly SimEventObject?[] secondVoidzones = new SimEventObject?[3];
    private readonly CircleObstacle?[] puddleObstacles = new CircleObstacle?[3];
    private readonly SimEnemy?[] tornadoHelpers = new SimEnemy?[3];
    private readonly SimEnemy?[] bossHelpers = new SimEnemy?[8];
    private readonly SimEnemy?[] dolls = new SimEnemy?[4];
    private readonly SimTether?[] dollFixates = new SimTether?[4];
    private readonly bool[] dollFed = new bool[4];
    private readonly PartyRole?[] dollTakers = new PartyRole?[4];
    private bool dollsChase;
    private readonly SimTether?[] drainageTethers = new SimTether?[2];
    private readonly Dictionary<SimCharacter, float> dropsy = [];
    private PartyRole livingLiquidTank = PartyRole.MainTank;
    private PartyRole liquidHandTank = PartyRole.OffTank;
    private bool liquidHandWalks;
    private byte handMechanic;
    private float bossProteanFacing;
    private bool outburst;
    private bool livingLiquidDead;
    private bool dollsFeedable;

    public TeaP1LivingLiquidState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaP1LivingLiquidState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        autoVariation = world.Stream("tea-p1-autos");
        dollHitSizes = world.Stream("tea-p1-doll-hits");
        drainageStandIns = world.Stream("tea-p1-drainage-stand-ins");
        Array.Clear(dollFed);
        Array.Clear(dollTakers);
        dollsChase = false;
        dropsy.Clear();
        livingLiquidTank = PartyRole.MainTank;
        liquidHandTank = PartyRole.OffTank;
        liquidHandWalks = false;
        outburst = false;
        livingLiquidDead = false;
        dollsFeedable = false;
        DiagnosticLog.Info($"[TeaP1LivingLiquid] Roll: tornadoes open {state.FirstTornadoes.OpenSide} then {state.SecondTornadoes.OpenSide}, "
                           + $"calm {state.FirstCalmTornado}/{state.SecondCalmTornado}, Hand orb from earlier {state.FirstHandOrbFromEarlier}/{state.SecondHandOrbFromEarlier}, "
                           + $"untethered {state.UntetheredTornado}, MT earlier tether {state.MainTankHoldsEarlierTether}, Throttle on {string.Join(" ", state.Throttled)}, "
                           + $"doll paces {string.Join(" ", state.DollRolls.Select(r => r.Pace.ToString("F2")))}.");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaP1LivingLiquidState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnLivingLiquidAndHelpers);
        world.Events.Add(0.17f, () => state.Objects.LivingLiquid?.Follow(LivingOrNext(livingLiquidTank), LivingLiquidSpeed, Geometry.EnemyReach));
        foreach (var t in (float[])[3.07f, 6.08f, 9.14f, 12.17f, 19.20f, 22.22f, 25.21f, 28.23f, 31.31f, 34.34f, 37.35f, 40.36f,
                     43.32f, 46.39f, 49.43f, 52.49f, 55.56f, 58.52f, 61.56f, 64.60f, 76.94f, 79.96f, 83.00f, 86.07f, 93.06f,
                     96.10f, 99.18f, 102.19f, 115.23f, 118.26f, 121.29f, 124.32f, 127.35f])
            world.Events.Add(t, LivingLiquidAttack);

        world.Events.Add(10.15f, () => FluidSwing());
        world.Events.Add(14.27f, () => state.Objects.LivingLiquid?.Cast(TeaActions.Cascade));

        world.Events.Add(19.45f, () => state.Objects.LivingLiquid?.SetAnimationState(0, 1));
        world.Events.Add(19.66f, SplitIntoLiquidHandAndFirstTornadoes);
        world.Events.Add(21.50f, () => state.Objects.LiquidHand?.SetTarget(LivingOrNext(liquidHandTank), follow: false));
        world.Events.Add(24.40f, LiquidHandStartsWalking);
        foreach (var t in (float[])[24.29f, 27.38f, 30.41f, 33.40f, 36.38f, 42.51f, 45.58f, 48.57f, 51.59f, 54.64f, 57.72f, 60.76f,
                     63.72f, 69.83f, 72.81f, 75.87f, 78.89f, 81.96f, 84.94f, 90.98f, 94.07f, 97.06f, 100.18f, 103.15f, 109.25f,
                     112.25f, 115.29f, 118.32f, 121.35f])
            world.Events.Add(t, LiquidHandAttack);

        world.Events.Add(29.35f, ShowHandTell);
        world.Events.Add(32.34f, () => AimTornadoProteans(firstRages));
        world.Events.Add(32.44f, CastTornadoProteans);
        world.Events.Add(32.63f, SpawnDolls);
        world.Events.Add(33.44f, DollsTurnTargetable);
        world.Events.Add(34.33f, HideHandTell);
        world.Events.Add(34.46f, ResolveHandMechanic);
        world.Events.Add(36.47f, () => FluidSwing());
        world.Events.Add(36.61f, FluidStrike);
        world.Events.Add(37.53f, () => TornadoProteansOnClosest(firstRages));
        world.Events.Add(38.56f, Exhaust);
        // A doll is absorbed from its first Exhaust on, though one may spawn beside a boss.
        world.Events.Add(38.56f, () => dollsFeedable = true);
        world.Events.Add(38.73f, () => state.Objects.LiquidHand?.Cast(TeaActions.HandOfPain));
        world.Events.Add(39.06f, DollsChase);
        // Each doll's own swings start a second after its first Exhaust and land every 3s while it is hungry.
        foreach (var t in (float[])[39.57f, 42.57f, 45.57f, 48.57f, 51.57f, 54.57f, 57.57f, 60.57f, 63.57f, 66.57f, 69.57f, 72.57f,
                     75.57f, 78.57f, 81.57f, 84.57f, 87.57f, 90.57f, 93.57f, 96.57f, 99.57f, 102.57f, 105.57f, 108.57f, 111.57f,
                     114.57f, 117.57f, 120.57f, 123.57f, 126.57f, 129.57f, 132.57f, 135.57f])
            world.Events.Add(t, DollAttacks);
        world.Events.Add(40.46f, () => Pressurize(firstRages, state.FirstCalmTornado));
        world.Events.Add(41.18f, () => SpawnEmboli(state.FirstTornadoes, state.FirstCalmTornado, state.FirstHandOrbFromEarlier));
        world.Events.Add(46.01f, RollEmboli);
        world.Events.Add(49.19f, Exhaust);

        world.Events.Add(55.61f, () => FluidSwing());
        world.Events.Add(55.85f, FluidStrike);
        // UNVERIFIED: whether a doll still hungry after this third one Exhausts again.
        world.Events.Add(59.83f, Exhaust);

        world.Events.Add(65.67f, GatherProteanHelpers);
        world.Events.Add(65.79f, CastBossProteans);
        world.Events.Add(66.01f, () => state.Objects.LiquidHand?.Cast(TeaActions.HandOfPain));
        world.Events.Add(70.83f, () => BossProteansOnClosest(sluices: true));
        world.Events.Add(73.90f, () => BossProteansOnClosest(sluices: false));

        world.Events.Add(78.53f, TetherDrainage);
        foreach (var t in (float[])[80.11f, 81.20f, 82.27f, 83.40f, 84.52f, 85.65f])
            world.Events.Add(t, () => state.Objects.LivingLiquid?.Cast(TeaActions.Splash));
        world.Events.Add(85.16f, () => state.Objects.LiquidHand?.Cast(TeaActions.HandOfPain));
        world.Events.Add(85.65f, ResolveDrainage);
        world.Events.Add(86.79f, () =>
        {
            state.Objects.LivingLiquid?.Cast(TeaActions.Cascade);
            foreach (var voidzone in firstVoidzones) voidzone?.DirectorEObjMod(7);
            foreach (var rage in firstRages) rage?.FadeOut();
        });
        world.Events.Add(88.00f, () => DespawnTornadoes(firstRages));
        world.Events.Add(89.90f, () => DespawnVoidzones(firstVoidzones));
        world.Events.Add(91.89f, () => state.Objects.LivingLiquid?.SetAnimationState(0, 1));
        world.Events.Add(92.10f, () => SpawnTornadoes(state.SecondTornadoes, secondRages, secondVoidzones, EObjId.SecondVoidzonesEntityId));
        world.Events.Add(94.93f, () =>
        {
            foreach (var rage in secondRages) rage?.Cast(TeaActions.LiquidGaol);
        });
        world.Events.Add(95.38f, Throttle);

        world.Events.Add(102.87f, GatherProteanHelpers);
        world.Events.Add(102.99f, CastBossProteans);
        world.Events.Add(104.92f, () => state.Objects.LiquidHand?.Cast(TeaActions.HandOfPain));
        world.Events.Add(108.04f, () => BossProteansOnClosest(sluices: true));
        world.Events.Add(108.89f, () => AimTornadoProteans(secondRages));
        world.Events.Add(108.99f, CastTornadoProteans);
        world.Events.Add(111.16f, () => BossProteansOnClosest(sluices: false));
        world.Events.Add(114.14f, () => TornadoProteansOnClosest(secondRages));

        world.Events.Add(118.04f, ShowHandTell);
        world.Events.Add(118.10f, () => Pressurize(secondRages, state.SecondCalmTornado));
        world.Events.Add(118.88f, () => SpawnEmboli(state.SecondTornadoes, state.SecondCalmTornado, state.SecondHandOrbFromEarlier));
        world.Events.Add(122.99f, HideHandTell);
        world.Events.Add(123.12f, ResolveHandMechanic);
        world.Events.Add(123.71f, RollEmboli);

        world.Events.Add(125.18f, () => state.Objects.LiquidHand?.Cast(TeaActions.HandOfPain));
        foreach (var t in (float[])[126.26f, 127.38f, 128.49f, 129.60f, 130.72f, 131.86f])
            world.Events.Add(t, () => state.Objects.LivingLiquid?.Cast(TeaActions.Splash));
        world.Events.Add(132.95f, () => FluidSwing());
        // UNVERIFIED: both keep their 3.03s auto cadence until Living Liquid dies.
        world.Events.Add(133.41f, LivingLiquidAttack);
        world.Events.Add(133.47f, LiquidHandAttack);
        // The run ends as Living Liquid dies; the second set and the orbs go as the first set went at the
        // second Cascade.
        world.Events.Add(136.40f, FadeOutLastHazards);
        world.Events.Add(137.55f, () =>
        {
            foreach (var orb in state.Objects.Emboli.Where(o => o.IsActive)) orb.Despawn();
        });
        world.Events.Add(137.61f, () => DespawnTornadoes(secondRages));
        world.Events.Add(139.51f, () => DespawnVoidzones(secondVoidzones));
    }

    // Throttle kills as it runs out; an Esuna takes it off with time left.
    public void OnStatusLost(StatusLost lost)
    {
        if (lost is { StatusId: StatusId.Throttle, Reason: StatusLossReason.Expired }) lost.Holder.Die(ActionId.LiquidGaol, "Throttle ran out");
    }

    public void Tick(float delta, float elapsed)
    {
        if (state is null) return;
        FeedDolls();
        MoveEmboli();
        SoakPuddles();
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new TeaP1LivingLiquidAiReplayStateMessage(s.FirstTornadoes.OpenSide, s.SecondTornadoes.OpenSide, s.FirstCalmTornado,
                s.SecondCalmTornado, s.FirstHandOrbFromEarlier, s.SecondHandOrbFromEarlier, s.UntetheredTornado,
                s.MainTankHoldsEarlierTether, s.Throttled.ToArray())
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaP1LivingLiquidAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaP1LivingLiquidState.FromNetworkReplay(msg.FirstOpenSide, msg.SecondOpenSide, msg.FirstCalmTornado, msg.SecondCalmTornado,
                msg.FirstHandOrbFromEarlier, msg.SecondHandOrbFromEarlier, msg.UntetheredTornado, msg.MainTankHoldsEarlierTether,
                msg.Throttled) is not { } shadowState) return null;
        ((IScenarioAi<TeaP1LivingLiquidState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The bosses and the orbs may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaP1LivingLiquidState shadow) return;
        shadow.Objects.LivingLiquid ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.LivingLiquid);
        shadow.Objects.LiquidHand ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.LiquidHand);
        foreach (var orb in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.Embolus && e.IsActive))
            if (!shadow.Objects.Emboli.Contains(orb)) shadow.Objects.Emboli.Add(orb);
        foreach (var doll in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.JagdDoll && e.IsActive))
        {
            var k = Enumerable.Range(0, TeaP1LivingLiquidState.DollBearings.Length)
                .MinBy(i => Vector2.Distance(Flat(doll.Position), Flat(Geometry.OnCompass(TeaP1LivingLiquidState.DollBearings[i], 7f))));
            shadow.Objects.Dolls[k] ??= doll;
        }
    }

    public void RebuildPeerObstacles(ObstacleField obstacles, IReadOnlyDictionary<int, SimEnemy> peerEnemies,
        IReadOnlyDictionary<int, SimEventObject> peerEventObjects, SimCharacter? localPlayer)
    {
        foreach (var rage in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.LiquidRage && e.IsActive))
            obstacles.Add(new CircleObstacle(Flat(rage.Position), Geometry.PuddleRadius));
    }

    private void SpawnLivingLiquidAndHelpers()
    {
        state.Objects.LivingLiquid = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.LivingLiquid, NameId: BNpcNameId.LivingLiquid, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(new Vector3(0f, 0f, -15f), 0f)));
        if (state.Objects.LivingLiquid is { } livingLiquid)
        {
            livingLiquid.SetTarget(party.Get(livingLiquidTank), follow: false);
            livingLiquid.Provoked += by => TurnOnProvoke(by, LivingLiquidProvoked);
        }
        foreach (var pool in new[] { tornadoHelpers, bossHelpers })
            for (var i = 0; i < pool.Length; i++)
                pool[i] = world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.AlexanderPrime, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(Vector3.Zero, 0f)));
    }

    private void SplitIntoLiquidHandAndFirstTornadoes()
    {
        state.Objects.LiquidHand = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.LiquidHand, NameId: BNpcNameId.LiquidHand, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, 0f)));
        if (state.Objects.LiquidHand is { } hand) hand.Provoked += by => TurnOnProvoke(by, LiquidHandProvoked);
        SpawnTornadoes(state.FirstTornadoes, firstRages, firstVoidzones, EObjId.FirstVoidzonesEntityId);
    }

    private void SpawnTornadoes(TornadoT tornadoes, SimEnemy?[] rages, SimEventObject?[] voidzones, uint firstVoidzoneEntityId)
    {
        for (var i = 0; i < 3; i++)
        {
            var spot = tornadoes.Tornado(i);
            var position = new Vector3(spot.X, 0f, spot.Y);
            rages[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.LiquidRage, NameId: BNpcNameId.LiquidRage, Level: Level,
                Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
                Placement: new Placement(position, 0f)));
            voidzones[i] = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.VoidzoneLiquidRage,
                Placement = new Placement(position, 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = firstVoidzoneEntityId + (uint)i,
                TargetableStatus = 5,
                Arg2 = 0x500003u + ((uint)i << 16),
            });
            puddleObstacles[i] = world.Obstacles.Add(new CircleObstacle(spot, Geometry.PuddleRadius));
        }
    }

    private void DespawnTornadoes(SimEnemy?[] rages)
    {
        for (var i = 0; i < 3; i++)
        {
            rages[i]?.Despawn();
            rages[i] = null;
            if (puddleObstacles[i] is { } obstacle) world.Obstacles.Remove(obstacle);
            puddleObstacles[i] = null;
        }
    }

    private static void DespawnVoidzones(SimEventObject?[] voidzones)
    {
        for (var i = 0; i < 3; i++)
        {
            voidzones[i]?.Despawn();
            voidzones[i] = null;
        }
    }

    private SimCharacter? LivingOrNext(PartyRole role)
    {
        if (party.Get(role) is { } member && member.IsAlive()) return member;
        return PerRole.All.Select(party.Get).FirstOrDefault(m => m.IsAlive());
    }

    private void LivingLiquidAttack()
    {
        if (state.Objects.LivingLiquid is { } livingLiquid && LivingOrNext(livingLiquidTank) is { } target)
            livingLiquid.Cast(TeaActions.LivingLiquidAttack, target, (byte)autoVariation.NextInt(3));
    }

    private void LiquidHandAttack()
    {
        if (state.Objects.LiquidHand is { } hand && LivingOrNext(liquidHandTank) is { } target)
            hand.Cast(TeaActions.LiquidHandAttack, target, (byte)autoVariation.NextInt(3));
    }

    private void TurnOnProvoke(SimCharacter by, Action<PartyRole> turn)
    {
        if (by is not ISimPartyMember { } tank) return;
        world.Events.Add(ProvokeTurnSeconds, () =>
        {
            if (by.IsAlive()) turn(tank.Role);
        });
    }

    private void LivingLiquidProvoked(PartyRole tank)
    {
        livingLiquidTank = tank;
        if (state.Objects.LivingLiquid is not { } livingLiquid || LivingOrNext(tank) is not { } target) return;
        livingLiquid.SetTarget(target, follow: false);
        livingLiquid.Follow(target, LivingLiquidSpeed, Geometry.EnemyReach);
    }

    private void LiquidHandProvoked(PartyRole tank)
    {
        liquidHandTank = tank;
        if (state.Objects.LiquidHand is not { } hand || LivingOrNext(tank) is not { } target) return;
        hand.SetTarget(target, follow: false);
        if (liquidHandWalks) hand.Follow(target, LiquidHandSpeed, Geometry.EnemyReach);
    }

    private void LiquidHandStartsWalking()
    {
        liquidHandWalks = true;
        LiquidHandProvoked(liquidHandTank);
    }

    private void FluidSwing()
        => state.Objects.LivingLiquid?.Cast(TeaActions.FluidSwing, LivingOrNext(livingLiquidTank));

    private void FluidStrike()
        => state.Objects.LiquidHand?.Cast(TeaActions.FluidStrike, LivingOrNext(liquidHandTank));

    // Parting when the bosses stand apart at the tell, Prayer when close.
    private void ShowHandTell()
    {
        if (state.Objects.LiquidHand is not { } hand || state.Objects.LivingLiquid is not { } livingLiquid) return;
        handMechanic = Vector2.Distance(Flat(hand.Position), Flat(livingLiquid.Position)) < PrayerWithin ? HandOfPrayerTell : HandOfPartingTell;
        hand.SetModelState(handMechanic);
        hand.SetAnimationState(0, 1);
    }

    private void HideHandTell()
    {
        state.Objects.LiquidHand?.SetModelState(0);
        state.Objects.LiquidHand?.SetAnimationState(0, 0);
    }

    // Parting wants the two together, Prayer apart.
    private void ResolveHandMechanic()
    {
        if (state.Objects.LiquidHand is not { } hand || state.Objects.LivingLiquid is not { } livingLiquid) return;
        var apart = Vector2.Distance(Flat(hand.Position), Flat(livingLiquid.Position));
        hand.Cast(handMechanic == HandOfPrayerTell
            ? apart >= ApartFrom ? TeaActions.HandOfPrayer : TeaActions.HandOfPrayerTogether
            : apart <= TogetherWithin ? TeaActions.HandOfParting : TeaActions.HandOfPartingApart);
    }

    // Each cone points at whoever stood closest to its tornado as the cast began, and keeps that line.
    private void AimTornadoProteans(SimEnemy?[] rages)
    {
        for (var i = 0; i < 3; i++)
        {
            if (rages[i] is not { } rage || tornadoHelpers[i] is not { } helper) continue;
            var closest = party.Find.Closest(rage.Position);
            helper.SetPosition(new Placement(rage.Position, closest is null ? 0f : Heading(rage.Position, closest.Position)));
        }
    }

    private void CastTornadoProteans()
    {
        foreach (var helper in tornadoHelpers) helper?.Cast(TeaActions.ProteanWaveTornadoVisible);
    }

    private void TornadoProteansOnClosest(SimEnemy?[] rages)
    {
        for (var i = 0; i < 3; i++)
        {
            if (rages[i] is not { } rage || tornadoHelpers[i] is not { } helper) continue;
            helper.SetPosition(new Placement(rage.Position, 0f));
            if (party.Find.Closest(rage.Position) is { } closest)
                helper.Cast(TeaActions.ProteanWaveTornadoInvisible, closest);
        }
    }

    private void GatherProteanHelpers()
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid) return;
        foreach (var helper in bossHelpers) helper?.SetPosition(new Placement(livingLiquid.Position, 0f));
    }

    // Living Liquid's own cone runs along its facing, at its tank; a helper's at each other player.
    private void CastBossProteans()
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid) return;
        livingLiquid.Cast(TeaActions.ProteanWaveBossVisible);
        bossProteanFacing = livingLiquid.Rotation;
        var tank = LivingOrNext(livingLiquidTank);
        var others = party.ActiveMembers().Where(m => m.IsAlive() && m != tank).ToList();
        for (var i = 0; i < others.Count && i < bossHelpers.Length; i++)
        {
            if (bossHelpers[i] is not { } helper) continue;
            helper.SetPosition(new Placement(livingLiquid.Position, Heading(livingLiquid.Position, others[i].Position)));
            helper.Cast(TeaActions.ProteanWaveHelperVisible);
        }
    }

    // The shadow cone keeps the facing the visible cast began with; the four helper cones go to the
    // four players closest to Living Liquid, and the first wave's Sluices to the four farthest.
    private void BossProteansOnClosest(bool sluices)
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid) return;
        livingLiquid.SetRotation(bossProteanFacing);
        livingLiquid.Cast(TeaActions.ProteanWaveBossInvisible);
        var closest = party.Find.ClosestN(livingLiquid.Position, 4);
        for (var i = 0; i < closest.Count; i++)
        {
            if (bossHelpers[i] is not { } helper) continue;
            helper.SetPosition(new Placement(livingLiquid.Position, 0f));
            helper.Cast(TeaActions.ProteanWaveHelperInvisible, closest[i]);
        }
        if (!sluices) return;
        var farthest = party.Find.FarestN(livingLiquid.Position, 4);
        for (var i = 0; i < farthest.Count; i++)
            bossHelpers[4 + i]?.Cast(TeaActions.Sluice, farthest[i].Position);
    }

    private void SpawnDolls()
    {
        for (var k = 0; k < dolls.Length; k++)
        {
            dolls[k] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.JagdDoll, NameId: BNpcNameId.JagdDoll, Level: Level,
                Targetable: false, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
                Appearance: Appearance.JagdDoll,
                Placement: new Placement(Geometry.OnCompass(TeaP1LivingLiquidState.DollBearings[k], 7f), 0f)));
            state.Objects.Dolls[k] = dolls[k];
            var index = k;
            if (dolls[k] is { } doll) doll.Attacked += by => DollHit(index, by);
        }
    }

    // Each turns on the healer first, whose heals hold its enmity, then on whoever hits it first.
    private void DollsTurnTargetable()
    {
        foreach (var doll in dolls)
        {
            doll?.SetTargetable(true);
            doll?.SetTarget(party.Get(PartyRole.RegenHealer), follow: false);
        }
    }

    private void DollHit(int k, SimCharacter by)
    {
        if (dollTakers[k] is not null || dollFed[k] || by is not ISimPartyMember { } member || dolls[k] is not { Targetable: true } doll) return;
        dollTakers[k] = member.Role;
        doll.SetTarget(by, follow: false);
        dollFixates[k] = world.Tether(doll, by, TetherId.DollFixate);
        if (dollsChase) doll.Follow(by, JagdDollSpeed, Geometry.EnemyReach);
        var hits = DollDrain.Of(member.Role).Hits(state.DollRolls[k], dollHitSizes);
        DiagnosticLog.Info($"[TeaP1LivingLiquid] Doll {k} taken by {member.Role}, under 25% in {hits[^1].At:F1}s.");
        foreach (var (at, hp) in hits)
            world.Events.Add(at, () =>
            {
                if (!dollFed[k] && doll.IsActive) doll.SetHpFraction(hp);
            });
    }

    private SimCharacter? DollTarget(int k) => LivingOrNext(dollTakers[k] ?? PartyRole.RegenHealer);

    private void DollsChase()
    {
        dollsChase = true;
        for (var k = 0; k < dolls.Length; k++)
            if (dolls[k] is { } doll && !dollFed[k] && DollTarget(k) is { } target)
                doll.Follow(target, JagdDollSpeed, Geometry.EnemyReach);
    }

    private void DollAttacks()
    {
        for (var k = 0; k < dolls.Length; k++)
            if (dolls[k] is { IsActive: true } doll && !dollFed[k] && DollTarget(k) is { } target)
                doll.Cast(TeaActions.JagdDollAttack, target, (byte)autoVariation.NextInt(3));
    }

    private void Exhaust()
    {
        for (var k = 0; k < dolls.Length; k++)
            if (dolls[k] is { IsActive: true } doll && !dollFed[k]) doll.Cast(TeaActions.Exhaust);
    }

    // A doll near either boss is absorbed at any HP, Living Liquid first on a near tie.
    private void FeedDolls()
    {
        if (!dollsFeedable) return;
        for (var k = 0; k < dolls.Length; k++)
        {
            if (dollFed[k] || dolls[k] is not { IsActive: true } doll) continue;
            var absorber = new[] { state.Objects.LivingLiquid, state.Objects.LiquidHand }
                .FirstOrDefault(b => b is { IsActive: true } && Vector2.Distance(Flat(b.Position), Flat(doll.Position)) <= DollFeedReach);
            if (absorber is null) continue;
            dollFed[k] = true;
            var tooHigh = doll.HpFraction >= DollFeedHp;
            DiagnosticLog.Info($"[TeaP1LivingLiquid] Doll {k} fed to {absorber.DisplayName} at {doll.HpFraction:P0}.");
            dollFixates[k]?.Despawn();
            doll.Follow(null);
            doll.SetTargetable(false);
            var feed = world.Tether(doll, absorber, TetherId.DollFeed);
            world.Events.Add(1.1f, () =>
            {
                feed.Despawn();
                if (doll.IsActive) doll.Cast(tooHigh ? TeaActions.ReducibleComplexityTooHigh : TeaActions.ReducibleComplexity);
            });
        }
    }

    private void Pressurize(SimEnemy?[] rages, int calm)
    {
        for (var i = 0; i < 3; i++)
            if (i != calm) rages[i]?.Cast(TeaActions.Pressurize);
    }

    // The orb spawned first aims at Liquid Hand, the second at Living Liquid.
    private void SpawnEmboli(TornadoT tornadoes, int calm, bool handOrbFromEarlier)
    {
        var sources = Enumerable.Range(0, 3).Where(i => i != calm).ToList();
        if (!handOrbFromEarlier) sources.Reverse();
        var targets = new[] { state.Objects.LiquidHand, state.Objects.LivingLiquid };
        for (var j = 0; j < 2; j++)
        {
            var spot = tornadoes.Tornado(sources[j]);
            var from = new Vector3(spot.X, 0f, spot.Y);
            var aim = (targets[j] is { IsActive: true } boss ? boss : targets[1 - j])?.Position ?? Vector3.Zero;
            var heading = Heading(from, aim);
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Embolus, NameId: BNpcNameId.Embolus, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible,
                    Placement: new Placement(from, heading))) is not { } orb) continue;
            state.Objects.Emboli.Add(orb);
        }
    }

    // Straight on along its spawn heading, at walking pace, until it leaves the arena.
    private void RollEmboli()
    {
        foreach (var orb in state.Objects.Emboli.Where(o => o.IsActive && o.MoveDestination is null))
        {
            var heading = new Vector3(MathF.Sin(orb.Rotation), 0f, MathF.Cos(orb.Rotation));
            orb.MoveTo(orb.Position + heading * (EmbolusGoneRadius * 2f + TornadoT.Radius), speed: EmbolusSpeed);
        }
    }

    // Touching hitboxes sets it off. UNVERIFIED: that a boss does as well as a player.
    private void MoveEmboli()
    {
        // An orb the engine hasn't built yet can't be touched.
        foreach (var orb in state.Objects.Emboli.Where(o => o.IsActive && !o.PacketSpawnPending).ToList())
        {
            var radius = Flat(orb.Position).Length();
            if (radius >= EmbolusGoneRadius)
            {
                orb.Despawn();
                continue;
            }
            if (!orb.FadingOut && radius >= EmbolusGoneRadius - EmbolusSpeed * EmbolusFadeSeconds) orb.FadeOut();
            if (outburst || livingLiquidDead) continue;
            var touched = party.ActiveMembers().Where(m => m.IsAlive()).Cast<SimCharacter?>()
                .Concat([state.Objects.LivingLiquid, state.Objects.LiquidHand])
                .FirstOrDefault(c => c is { IsActive: true } && Vector2.Distance(Flat(c.Position), Flat(orb.Position)) <= c.HitboxRadius + orb.HitboxRadius);
            if (touched is null) continue;
            outburst = true;
            DiagnosticLog.Info($"[TeaP1LivingLiquid] Embolus touched {(touched as ISimPartyMember)?.Role.ToString() ?? (touched as SimEnemy)?.DisplayName}.");
            orb.Follow(null);
            orb.Cast(TeaActions.Outburst);
        }
    }

    private void TetherDrainage()
    {
        var sources = Enumerable.Range(0, 3).Where(i => i != state.UntetheredTornado).ToList();
        var tanks = state.MainTankHoldsEarlierTether ? new[] { PartyRole.MainTank, PartyRole.OffTank } : [PartyRole.OffTank, PartyRole.MainTank];
        var holders = tanks.Select(role => party.Get(role) is { } tank && tank.IsAlive() ? tank : null).ToArray();
        for (var j = 0; j < 2; j++)
            holders[j] ??= DrainageStandIn(tanks[j], holders);
        // UNVERIFIED: that a Drainage tether passes through its beam at all. It passes over a member
        // already holding the other.
        for (var j = 0; j < 2; j++)
            if (firstRages[sources[j]] is { } rage && holders[j] is { } holder)
                drainageTethers[j] = world.Tether(rage, End.Passable(holder, skipsHolders: true), TetherId.Drainage);
    }

    // A dead tank's tether goes to another living player, at random, never the other tether's holder.
    // UNVERIFIED: who; the real pick likely follows enmity.
    private SimCharacter? DrainageStandIn(PartyRole deadTank, SimCharacter?[] holders)
    {
        var alive = party.ActiveMembers().Where(m => m.IsAlive() && !holders.Contains(m)).ToArray();
        var standIn = settingsWindow.Overrides.DeadTanksDrainageGoesTo is { } role && party.Get(role) is { } forced && alive.Contains(forced)
            ? forced
            : alive.Length == 0 ? null : drainageStandIns.NextObj(alive);
        DiagnosticLog.Info($"[TeaP1LivingLiquid] {deadTank} is dead; their Drainage tether goes to {(standIn as ISimPartyMember)?.Role.ToString() ?? "nobody"}.");
        return standIn;
    }

    private void ResolveDrainage()
    {
        foreach (var tether in drainageTethers)
        {
            if (tether is null) continue;
            if (tether.A is SimEnemy rage && tether.B is { } holder && holder.IsAlive())
                rage.Cast(TeaActions.Drainage, holder);
            tether.Despawn();
        }
        Array.Clear(drainageTethers);
    }

    private void Throttle()
    {
        foreach (var role in state.Throttled)
            if (party.Get(role) is { } member && member.IsAlive())
                member.AddStatus(StatusId.Throttle, 10f);
    }

    // Dropsy lands on stepping into a tornado's water and kills once it has held long enough.
    private void SoakPuddles()
    {
        var now = world.Events.Elapsed;
        var tornadoes = firstRages.Concat(secondRages).Where(r => r is { IsActive: true, FadingOut: false }).Select(r => Flat(r!.Position)).ToList();
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()))
        {
            var inside = tornadoes.Any(t => Vector2.Distance(Flat(member.Position), t) < Geometry.PuddleRadius);
            if (!dropsy.TryGetValue(member, out var since))
            {
                if (!inside) continue;
                member.AddStatus(StatusId.Dropsy);
                dropsy[member] = since = now;
            }
            // Under an invuln it lifts on stepping out, as in game; without one it stays and the KO lands.
            if (!inside && Invulnerable(member))
            {
                member.RemoveStatus(StatusId.Dropsy);
                dropsy.Remove(member);
                continue;
            }
            if (now - since < DropsySeconds) continue;
            // An invuln outlasts the KO; the next comes as long after.
            if (!Invulnerable(member) && member.Die(SimCharacterDeathExtensions.Environment, "Dropsy: stood in a tornado's water")) dropsy.Remove(member);
            else dropsy[member] = now;
        }
    }

    // UNVERIFIED: that the orbs stop as Living Liquid dies and fade out harmless.
    private void FadeOutLastHazards()
    {
        livingLiquidDead = true;
        foreach (var voidzone in secondVoidzones) voidzone?.DirectorEObjMod(7);
        foreach (var rage in secondRages) rage?.FadeOut();
        foreach (var orb in state.Objects.Emboli.Where(o => o.IsActive))
        {
            orb.StopMoving();
            if (!orb.FadingOut) orb.FadeOut();
        }
    }

    private static bool Invulnerable(SimCharacter member)
        => Mitigation.Effective(member.ActiveStatusSnapshot.Select(s => s.StatusId), DamageKind.Unique) >= 1f;

    private static float Heading(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
