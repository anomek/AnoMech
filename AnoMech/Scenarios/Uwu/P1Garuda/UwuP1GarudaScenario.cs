using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Scenarios.Uwu.UwuUtils;

namespace AnoMech.Scenarios.Uwu.P1Garuda;

// Thermal Low cleansed at two stacks charges Garuda; four charges wake her.
public sealed class UwuP1GarudaScenario : IScenario
{
    public string Name => "Garuda";
    public IPhase Phase => UwuZone.Garuda;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UwuP1GarudaAi()];

    private const float MistralSongHalfWidth = 2.5f;
    private const float MistralSongLength = 40f;
    private static readonly MistralSongDamage GarudaSongDamage = new(Intercept: 0.59f, Behind: 0.60f);
    private static readonly MistralSongDamage SistersSongDamage = new(Intercept: 0.48f, Behind: 0.28f);
    private const float MistralShriekDamage = 0.4f;
    private const float AerialBlastDamage = 0.5f;
    private const float FrictionDamage = 0.15f;
    private const float SuperCycloneDamage = 0.1f;
    private const float SlipstreamHalfAngle = MathF.PI / 4f;
    private const float FrictionRadius = 5f;
    private const float EyeOfTheStormInner = 12f;
    private const float EyeOfTheStormOuter = 25f;
    private const float MesohighRadius = 3f;
    private const float PassableHalfWidth = 1f;
    private const int MaxThermalLow = 3;
    private const uint BubbleEObjId = 0x1E8F68;
    private const int ChargesToWake = 4;
    private const uint SatinPlumeMaxHp = 35827;
    private const float SatinPlumeWalkSpeed = 7f;
    private static readonly Vector3 FirstPlumesGather = new(-7f, 0f, 3f);
    private static readonly Vector3 SecondPlumesGather = new(0f, 0f, 3.5f);

    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private DamageSolver damage = null!;
    private UwuP1GarudaState state = null!;

    private SimEnemy? garuda;
    private SimEnemy? suparna;
    private SimEnemy? chirada;
    private SimEnemy? spiny;
    private SimEventObject? bubble;
    private bool bubbleActive;
    private Vector3 bubbleCenter;
    private int aetherialCharges;
    private readonly List<SimEnemy> satinPlumes = [];
    private readonly Dictionary<SimEnemy, float> satinPlumeHp = [];
    private readonly Dictionary<SimEnemy, float> satinPlumeBotDrain = [];
    private float lastTimelineTick;
    private readonly List<SimEnemy> helpers = [];
    private readonly SimEnemy?[] featherDummies = new SimEnemy?[5];

    private Func<SimEnemy?>[] FeatherRainDummies =>
        [() => featherDummies[0], () => featherDummies[1], () => featherDummies[2], () => featherDummies[3], () => featherDummies[4]];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        utils = new UwuUtils(world);
        damage = new DamageSolver(party);
        state = new UwuP1GarudaState(world.Rng);
        satinPlumes.Clear();
        satinPlumeHp.Clear();
        satinPlumeBotDrain.Clear();
        lastTimelineTick = 0f;
        helpers.Clear();
        greatWhirlwindCasters.Clear();
        greatWhirlwindSpots.Clear();
        eyeOfTheStorm = null;
        bubbleActive = false;
        aetherialCharges = 0;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UwuP1GarudaState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnGaruda);
        world.Events.Add(0.2f, () => garuda?.MoveTo(new Vector3(0f, 0f, -0.7f), 8f, MathF.PI));
        world.Events.Add(0.2f, () => garuda?.SetTarget(Get(PartyRole.MainTank), follow: false));

        world.Events.Add(5.20f, () => Lockon(Get(state.MistralSongTarget), LockonId.MistralSong));
        world.Events.Add(5.29f, () => CastSelf(garuda, ActionId.Slipstream, 2.2f));
        world.Events.Add(7.78f, () => ResolveCone(garuda, ActionId.Slipstream, garuda?.Rotation ?? MathF.PI, 2.1f));
        world.Events.Add(10.32f, () => ResolveMistralSong(garuda, ActionId.MistralSongBoss, Get(state.MistralSongTarget), GreatWhirlwindSpot.Boss, GarudaSongDamage));
        world.Events.Add(12.60f, () => garuda?.MoveTo(new Vector3(-6.3f, 0f, -0.5f), 8f, MathF.PI));
        world.Events.Add(13.44f, () => CastGreatWhirlwind(GreatWhirlwindSpot.Boss));
        world.Events.Add(16.43f, () => ResolveGreatWhirlwind(GreatWhirlwindSpot.Boss));

        world.Events.Add(19.59f, () => CastGreatWhirlwind(GreatWhirlwindSpot.Boss));
        world.Events.Add(20.33f, () => SpawnPlumes(state.SatinPlumesFirst, withSpiny: true));
        world.Events.Add(20.52f, FixateSpinyOnOffTank);
        world.Events.Add(20.61f, () => CastSelf(garuda, ActionId.Slipstream, 2.2f));
        world.Events.Add(22.57f, () => ResolveGreatWhirlwind(GreatWhirlwindSpot.Boss));
        world.Events.Add(23.10f, () => SatinPlumesWalkTo(FirstPlumesGather));
        world.Events.Add(24.50f, () => SatinPlumesGathered(24.50f, [33.9f, 35.0f, 35.8f, 37.5f]));
        world.Events.Add(23.11f, () => ResolveCone(garuda, ActionId.Slipstream, garuda?.Rotation ?? MathF.PI, 2.1f));
        world.Events.Add(25.73f, () => CastGreatWhirlwind(GreatWhirlwindSpot.Boss));
        world.Events.Add(26.58f, () => ResolveDownburst());
        world.Events.Add(26.71f, SpinyCyclone);
        world.Events.Add(26.80f, () => spiny?.MoveTo(new Vector3(-6.8f, 0f, 1.9f), 2f));
        world.Events.Add(28.72f, () => ResolveGreatWhirlwind(GreatWhirlwindSpot.Boss));

        world.Events.Add(33.93f, () => garuda?.SetTargetable(false));
        world.Events.Add(34.02f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummies, 34.02f, 35.53f, 36.52f, resolve: KillFeatherRain);
        world.Events.Add(35.17f, () => garuda?.SetVisible(false));
        world.Events.Add(35.85f, SpinyCyclone);
        world.Events.Add(35.90f, () => spiny?.MoveTo(UwuP1GarudaState.GigastormSpot, 2f));
        world.Events.Add(36.07f, () => garuda?.SetPosition(new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(36.16f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(37.99f, () => garuda?.SetVisible(true));
        world.Events.Add(38.21f, () => garuda?.SetTargetable(true));
        world.Events.Add(38.21f, () => CastSelf(spiny, ActionId.Gigastorm, 2.7f));
        world.Events.Add(38.30f, () => CastSelf(garuda, ActionId.MistralShriek, 2.7f));
        world.Events.Add(41.19f, ResolveGigastorm);
        world.Events.Add(41.28f, () => Raidwide(garuda, ActionId.MistralShriek, MistralShriekDamage, 2.3f));
        world.Events.Add(43.45f, SpawnBubble);
        world.Events.Add(43.45f, () => bubbleActive = true);
        world.Events.Add(47.77f, () => bubble?.SetVisible(true));

        world.Events.Add(48.68f, () => CastFriction(state.FrictionTargets[0]));
        world.Events.Add(50.64f, () => ResolveFriction(state.FrictionTargets[0]));
        world.Events.Add(54.78f, () => CastFriction(state.FrictionTargets[1]));
        world.Events.Add(56.74f, () => ResolveFriction(state.FrictionTargets[1]));
        world.Events.Add(66.19f, DeactivateBubble);

        world.Events.Add(68.81f, () => garuda?.SetTargetable(false));
        world.Events.Add(68.90f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummies, 68.90f, 70.42f, 71.40f, resolve: KillFeatherRain);
        world.Events.Add(69.63f, () => bubble?.Despawn());
        world.Events.Add(70.37f, () => garuda?.SetVisible(false));
        world.Events.Add(70.91f, () => garuda?.SetVisible(true));
        world.Events.Add(70.95f, () => garuda?.SetPosition(new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(71.04f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(73.09f, () => garuda?.SetTargetable(true));
        world.Events.Add(73.18f, () => CastSelf(garuda, ActionId.AerialBlast, 2.7f));
        world.Events.Add(76.17f, () => Raidwide(garuda, ActionId.AerialBlast, AerialBlastDamage, 2.3f));

        world.Events.Add(87.62f, SpawnSisters);
        world.Events.Add(87.71f, () => PlaySisters(ActionTimelineId.SistersArrive));
        world.Events.Add(89.52f, () => ShowSisters(true));
        world.Events.Add(89.85f, () => PlaySisters(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummies, 89.85f, 91.36f, 92.34f, resolve: KillFeatherRain);
        world.Events.Add(91.57f, () => ShowSisters(false));
        world.Events.Add(91.90f, () => PlaceSisters(state.SuparnaSongSpot, state.ChiradaSongSpot));
        world.Events.Add(91.99f, () => PlaySisters(ActionTimelineId.WarpEnd));
        world.Events.Add(94.04f, MarkSistersSongTargets);
        world.Events.Add(95.99f, () => ShowSisters(true));
        world.Events.Add(96.58f, CastEyeOfTheStorm);
        world.Events.Add(96.71f, () => CastSelf(garuda, ActionId.WickedWheelAwaken, 2.7f));
        world.Events.Add(99.20f, ResolveSistersSongs);
        world.Events.Add(99.56f, ResolveEyeOfTheStorm);
        world.Events.Add(99.69f, ResolveWickedWheel);
        world.Events.Add(101.29f, () => PlaySisters(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummies, 101.29f, 102.81f, 103.79f, resolve: KillFeatherRain);
        world.Events.Add(102.32f, () => CastGreatWhirlwind(GreatWhirlwindSpot.Suparna));
        world.Events.Add(102.32f, () => CastGreatWhirlwind(GreatWhirlwindSpot.Chirada));
        world.Events.Add(102.57f, () => ShowSisters(false));
        world.Events.Add(105.31f, () => ResolveGreatWhirlwind(GreatWhirlwindSpot.Suparna));
        world.Events.Add(105.31f, () => ResolveGreatWhirlwind(GreatWhirlwindSpot.Chirada));

        world.Events.Add(110.84f, () => SpawnPlumes(state.SatinPlumesSecond, withSpiny: false));
        world.Events.Add(113.00f, () => SatinPlumesWalkTo(SecondPlumesGather));
        world.Events.Add(114.50f, () => SatinPlumesGathered(114.50f, [123.1f, 124.3f, 125.3f, 126.3f]));
        world.Events.Add(112.40f, () => garuda?.MoveTo(new Vector3(-0.1f, 0f, -6.6f), 1.5f, MathF.PI));
        world.Events.Add(117.38f, () => PlaceSisters(UwuP1GarudaState.SuparnaTetherSpot, UwuP1GarudaState.ChiradaTetherSpot));
        world.Events.Add(117.47f, () => PlaySisters(ActionTimelineId.WarpEnd));
        world.Events.Add(118.76f, () => CastSelf(garuda, ActionId.Slipstream, 2.2f));
        world.Events.Add(119.28f, () => ShowSisters(true));
        world.Events.Add(119.56f, TetherMesohigh);
        world.Events.Add(119.61f, CastEyeOfTheStorm);
        world.Events.Add(121.26f, () => ResolveCone(garuda, ActionId.Slipstream, garuda?.Rotation ?? MathF.PI, 2.1f));
        world.Events.Add(122.59f, ResolveEyeOfTheStorm);
        world.Events.Add(124.64f, ResolveMesohigh);
        world.Events.Add(124.73f, () => ResolveDownburst());
        world.Events.Add(126.74f, () => PlaySisters(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummies, 126.74f, 128.25f, 129.23f, resolve: KillFeatherRain);
        world.Events.Add(134.50f, KillGaruda);
        world.Events.Add(136f, DespawnAll);
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private static bool IsTank(SimCharacter member) => member is ISimPartyMember { Role: PartyRole.MainTank or PartyRole.OffTank };

    private SimEnemy? SpawnEnemy(uint baseId, uint nameId, Placement placement, bool targetable, bool visible, EnemyListMode enemyList) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: targetable,
            EnemyList: enemyList,
            IsVisible: visible,
            Placement: placement));

    private SimEnemy? SpawnHelper(Vector3 position, float rotation = 0f)
    {
        var helper = SpawnEnemy(BNpcBaseId.Dummy, BNpcNameId.Dummy, new Placement(position, rotation), false, true, EnemyListMode.Never);
        if (helper != null) helpers.Add(helper);
        return helper;
    }

    // Her walks are scripted; she only turns to the MT.
    // The plumes drain on the timeline's clock.
    public void Tick(float delta, float elapsed)
    {
        if (garuda is { Targetable: true, IsMoving: false, IsCasting: false } boss && Get(PartyRole.MainTank) is { } tank && tank.IsAlive())
            boss.Face(tank);
        var timeline = world.Events.Elapsed;
        DrainSatinPlumes(timeline - lastTimelineTick);
        lastTimelineTick = timeline;
        CleanseInBubble();
    }

    private void SpawnGaruda()
    {
        garuda = SpawnEnemy(BNpcBaseId.Garuda, BNpcNameId.Garuda, new Placement(new Vector3(0f, 0f, -10f), MathF.PI), true, true, EnemyListMode.Always);
        for (var i = 0; i < featherDummies.Length; i++) featherDummies[i] = SpawnHelper(Vector3.Zero);
    }

    private void ResolveCone(SimEnemy? caster, uint actionId, float rotation, float animationLock)
    {
        if (caster == null) return;
        PlayEffect(caster, actionId, animationLock, rotation);
        damage.Resolve(caster, actionId, [DamageType.TankBuster], [], size: SlipstreamHalfAngle,
            coneRotationDelta: rotation - caster.Rotation, extraRange: caster.HitboxRadius);
    }

    private void KillFeatherRain(IReadOnlyList<SimCharacter> snapshot) =>
        UwuUtils.KillSnapshot(damage, snapshot, ActionId.FeatherRain, "stood under a feather");

    private void ResolveDownburst()
    {
        if (garuda == null || Get(PartyRole.MainTank) is not { } mainTank) return;
        var toTank = mainTank.Position - garuda.Position;
        ResolveCone(garuda, ActionId.Downburst, MathF.Atan2(toTank.X, toTank.Z), 2.1f);
    }

    private enum GreatWhirlwindSpot { Boss, Suparna, Chirada }

    private readonly Dictionary<GreatWhirlwindSpot, Vector3> greatWhirlwindSpots = [];
    private readonly Dictionary<GreatWhirlwindSpot, SimEnemy?> greatWhirlwindCasters = [];

    // First player in the line takes the heavy hit; its tornado drops there.
    private readonly record struct MistralSongDamage(float Intercept, float Behind);

    private void ResolveMistralSong(SimEnemy? caster, uint actionId, SimCharacter? target, GreatWhirlwindSpot spot, MistralSongDamage songDamage)
    {
        if (caster == null || target == null) return;
        var toTarget = target.Position - caster.Position;
        var rotation = MathF.Atan2(toTarget.X, toTarget.Z);

        var line = party.Find.InsideRect(new Placement(caster.Position, rotation), MistralSongHalfWidth, MistralSongLength)
            .OrderBy(m => FlatDistance(m.Position, caster.Position)).ToList();
        var first = line.FirstOrDefault();
        PlayEffect(caster, actionId, 1.1f, rotation, (first ?? target).GameObjectId);
        greatWhirlwindSpots[spot] = first?.Position ?? target.Position;
        if (first == null) return;
        damage.ApplyDamage(first, songDamage.Intercept, actionId, "no tank intercepted it", lethal: !IsTank(first));
        foreach (var behind in line.Skip(1))
            damage.ApplyDamage(behind, songDamage.Behind, actionId, "Mistral Song", false);
    }

    private void CastGreatWhirlwind(GreatWhirlwindSpot spot)
    {
        if (!greatWhirlwindSpots.TryGetValue(spot, out var at)) return;
        if (!greatWhirlwindCasters.TryGetValue(spot, out var caster) || caster == null)
            greatWhirlwindCasters[spot] = caster = SpawnHelper(at);
        caster?.SetPosition(new Placement(at, 0f));
        caster?.NativeCast(ActionId.GreatWhirlwind, ActionType.Action, 0f, 2.7f, false, position: at);
    }

    private void ResolveGreatWhirlwind(GreatWhirlwindSpot spot)
    {
        if (!greatWhirlwindSpots.TryGetValue(spot, out var at)) return;
        if (greatWhirlwindCasters.GetValueOrDefault(spot) is { } caster) PlayEffect(caster, ActionId.GreatWhirlwind, 2.1f, at: at);
        damage.Resolve(IPositioned.From(at), ActionId.GreatWhirlwind, [DamageType.Lethal], []);
    }

    private unsafe void SpawnPlumes(IReadOnlyList<Vector3> satinSpots, bool withSpiny)
    {
        satinPlumes.Clear();
        satinPlumeHp.Clear();
        satinPlumeBotDrain.Clear();
        foreach (var at in satinSpots)
        {
            if (SpawnEnemy(BNpcBaseId.SatinPlume, BNpcNameId.SatinPlume, new Placement(at, 0f), true, true, EnemyListMode.Always) is not { } plume) continue;
            satinPlumes.Add(plume);
            satinPlumeHp[plume] = 1f;
            plume.SetHealth(SatinPlumeMaxHp, 1f);
        }
        if (withSpiny)
            spiny = SpawnEnemy(BNpcBaseId.SpinyPlume, BNpcNameId.SpinyPlume, new Placement(UwuP1GarudaState.SpinyPlumeSpawn, 0f), true, true, EnemyListMode.Always);
    }

    private void SatinPlumesWalkTo(Vector3 gather)
    {
        foreach (var plume in satinPlumes)
        {
            var fromGather = plume.Position - gather;
            var stop = fromGather.Length() > 1.5f ? gather + Vector3.Normalize(fromGather) * 1.5f : plume.Position;
            plume.MoveTo(stop, SatinPlumeWalkSpeed);
        }
    }

    // The bots AoE the plumes down on a fixed schedule.
    private void SatinPlumesGathered(float at, float[] botKillAt)
    {
        for (var i = 0; i < satinPlumes.Count && i < botKillAt.Length; i++)
            satinPlumeBotDrain[satinPlumes[i]] = 1f / (botKillAt[i] - at);
    }

    private void DrainSatinPlumes(float seconds)
    {
        foreach (var plume in satinPlumes.ToList())
            DamageSatinPlume(plume, satinPlumeBotDrain.GetValueOrDefault(plume) * seconds);
    }

    private void DamageSatinPlume(SimEnemy plume, float fraction)
    {
        if (!satinPlumeHp.TryGetValue(plume, out var hp)) return;
        hp -= fraction;
        satinPlumeHp[plume] = hp;
        ShowSatinPlumeHp(plume, hp);
        if (hp > 0f) return;
        satinPlumeHp.Remove(plume);
        satinPlumes.Remove(plume);
        plume.Despawn();
    }

    private static void ShowSatinPlumeHp(SimEnemy plume, float hp) => plume.SetHealth(SatinPlumeMaxHp, hp);

    private void FixateSpinyOnOffTank()
    {
        if (spiny == null || Get(PartyRole.OffTank) is not { } offTank) return;
        world.Tether(spiny, offTank, TetherId.SpinyFixate, 20f);
        spiny.MoveTo(new Vector3(-8f, 0f, 1.6f), 2f);
    }

    private void SpinyCyclone()
    {
        if (spiny == null || Get(PartyRole.OffTank) is not { } offTank || !offTank.IsAlive()) return;
        PlayEffect(spiny, ActionId.Cyclone, 1.1f, target: offTank.GameObjectId);
        AddThermalLow(offTank);
    }

    private void ResolveGigastorm()
    {
        if (spiny == null) return;
        bubbleCenter = spiny.Position;
        PlayEffect(spiny, ActionId.Gigastorm, 2.1f);
        damage.Resolve(spiny, ActionId.Gigastorm, [DamageType.Lethal], [], extraRange: spiny.HitboxRadius);
        spiny.Despawn();
    }

    private void SpawnBubble()
    {
        bubble = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = BubbleEObjId,
            Placement = new Placement(bubbleCenter, 0f),
            TimelineState = 1,
            SpawnVisible = false,
        });
    }

    private void DeactivateBubble()
    {
        bubble?.SetState(0);
        bubbleActive = false;
    }

    private void CastFriction(PartyRole role)
    {
        if (Get(role) is not { } target) return;
        garuda?.NativeCast(ActionId.Friction, ActionType.Action, 0f, 1.7f, false, targetId: target.GameObjectId);
    }

    private void ResolveFriction(PartyRole role)
    {
        if (Get(role) is not { } target) return;
        PlayEffect(garuda, ActionId.Friction, 1.1f, target: target.GameObjectId);
        foreach (var hit in party.Find.InsideCircle(target.Position, FrictionRadius).ToList())
        {
            damage.ApplyDamage(hit, FrictionDamage, ActionId.Friction, "Friction", false);
            AddThermalLow(hit);
        }
    }

    private static void AddThermalLow(SimCharacter member)
    {
        if (member.FindStatus(StatusId.ThermalLow) is { Stacks: >= MaxThermalLow }) return;
        member.AddStatus(StatusId.ThermalLow);
    }

    // One stack is a light hit, two charge Garuda, three wipe the party.
    private void CleanseThermalLow(SimCharacter member)
    {
        if (member.FindStatus(StatusId.ThermalLow) is not { } thermalLow) return;
        var stacks = thermalLow.Stacks;
        member.RemoveStatus(StatusId.ThermalLow);
        var caster = SpawnHelper(member.Position);
        if (stacks >= MaxThermalLow)
        {
            PlayEffect(caster, ActionId.SuperCycloneOverload, 1.1f);
            for (var slot = 0; slot < 8; slot++)
                if (party.Get(slot) is { } hit && hit.IsAlive())
                    damage.ApplyDamage(hit, 1f, ActionId.SuperCycloneOverload, "cleansed three Thermal Low", lethal: true);
            return;
        }
        var awakening = stacks == MaxThermalLow - 1;
        PlayEffect(caster, awakening ? ActionId.SuperCycloneAwaken : ActionId.SuperCyclone, 1.1f);
        foreach (var hit in party.Find.InsideCircle(member.Position, 50f).ToList())
            damage.ApplyDamage(hit, SuperCycloneDamage, ActionId.SuperCyclone, "Super Cyclone", false);
        if (!awakening) return;
        aetherialCharges++;
        garuda?.AddStatus(StatusId.AetheriallyCharged, 0f, 1);
        if (aetherialCharges == ChargesToWake) utils.Awaken(garuda, false);
    }

    private void CleanseInBubble()
    {
        if (!bubbleActive) return;
        foreach (var member in party.Find.InsideCircle(bubbleCenter, UwuP1GarudaState.BubbleRadius).Where(m => m.HasStatus(StatusId.ThermalLow)).ToList())
            CleanseThermalLow(member);
    }

    private void Raidwide(SimEnemy? caster, uint actionId, float fraction, float animationLock)
    {
        PlayEffect(caster, actionId, animationLock);
        for (var slot = 0; slot < 8; slot++)
            if (party.Get(slot) is { } member && member.IsAlive())
                damage.ApplyDamage(member, fraction, actionId, "Raidwide", false);
    }

    private void SpawnSisters()
    {
        suparna = SpawnEnemy(BNpcBaseId.SuparnaChirada, BNpcNameId.Suparna, new Placement(new Vector3(-6f, 0f, 0f), 0f), false, false, EnemyListMode.Never);
        chirada = SpawnEnemy(BNpcBaseId.SuparnaChirada, BNpcNameId.Chirada, new Placement(new Vector3(6f, 0f, 0f), 0f), false, false, EnemyListMode.Never);
    }

    private void PlaySisters(ushort timelineId)
    {
        suparna?.PlayActionTimeline(timelineId);
        chirada?.PlayActionTimeline(timelineId);
    }

    private void ShowSisters(bool visible)
    {
        suparna?.SetVisible(visible);
        chirada?.SetVisible(visible);
    }

    private void PlaceSisters(Vector3 suparnaAt, Vector3 chiradaAt)
    {
        suparna?.SetPosition(new Placement(suparnaAt, FacingCentre(suparnaAt)));
        chirada?.SetPosition(new Placement(chiradaAt, FacingCentre(chiradaAt)));
    }

    private static float FacingCentre(Vector3 from) => MathF.Atan2(-from.X, -from.Z);

    private void MarkSistersSongTargets()
    {
        foreach (var role in state.SistersSongTargets)
            Lockon(Get(role), LockonId.MistralSong);
    }

    private void ResolveSistersSongs()
    {
        ResolveMistralSong(suparna, ActionId.MistralSongSuparnaChirada, Get(state.SistersSongTargets[0]), GreatWhirlwindSpot.Suparna, SistersSongDamage);
        ResolveMistralSong(chirada, ActionId.MistralSongSuparnaChirada, Get(state.SistersSongTargets[1]), GreatWhirlwindSpot.Chirada, SistersSongDamage);
    }

    private SimEnemy? eyeOfTheStorm;

    private void CastEyeOfTheStorm()
    {
        eyeOfTheStorm ??= SpawnHelper(Vector3.Zero);
        CastSelf(eyeOfTheStorm, ActionId.EyeOfTheStorm, 2.7f);
    }

    private void ResolveEyeOfTheStorm()
    {
        PlayEffect(eyeOfTheStorm, ActionId.EyeOfTheStorm, 2.1f);
        // The sheet only has the circle; the safe eye is cut out here.
        UwuUtils.KillSnapshot(damage, party.Find.InsideRing(Vector3.Zero, EyeOfTheStormInner, EyeOfTheStormOuter), ActionId.EyeOfTheStorm, "outside the eye");
    }

    private void ResolveWickedWheel()
    {
        if (garuda == null) return;
        PlayEffect(garuda, ActionId.WickedWheelAwaken, 2.1f);
        damage.Resolve(garuda, ActionId.WickedWheelAwaken, [DamageType.Lethal], [], extraRange: garuda.HitboxRadius);
    }

    private void TetherMesohigh()
    {
        if (suparna != null && Get(state.MesohighTargets[0]) is { } suparnaTarget)
            state.SuparnaMesohigh = world.Tether(suparna, End.Passable(suparnaTarget, PassableHalfWidth), TetherId.Mesohigh);
        if (chirada != null && Get(state.MesohighTargets[1]) is { } chiradaTarget)
            state.ChiradaMesohigh = world.Tether(chirada, End.Passable(chiradaTarget, PassableHalfWidth), TetherId.Mesohigh);
    }

    // Kills holders without Thermal Low, cleanses those with it.
    private void ResolveMesohigh()
    {
        foreach (var (sister, tether) in new[] { (suparna, state.SuparnaMesohigh), (chirada, state.ChiradaMesohigh) })
        {
            if (sister == null || tether?.B is not { } holder) continue;
            PlayEffect(sister, ActionId.Mesohigh, 1.1f, target: holder.GameObjectId);
            foreach (var hit in party.Find.InsideCircle(holder.Position, MesohighRadius).ToList())
            {
                if (hit.HasStatus(StatusId.ThermalLow)) CleanseThermalLow(hit);
                else damage.ApplyDamage(hit, 1f, ActionId.Mesohigh, "took it without Thermal Low", lethal: true);
            }
            tether.Despawn();
        }
        state.SuparnaMesohigh = null;
        state.ChiradaMesohigh = null;
    }

    private void KillGaruda()
    {
        garuda?.Despawn();
        garuda = null;
    }

    private void DespawnAll()
    {
        KillGaruda();
        suparna?.Despawn();
        chirada?.Despawn();
        spiny?.Despawn();
        bubble?.Despawn();
        foreach (var plume in satinPlumes) plume.Despawn();
        foreach (var helper in helpers) helper.Despawn();
        satinPlumes.Clear();
        helpers.Clear();
        greatWhirlwindCasters.Clear();
        greatWhirlwindSpots.Clear();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
