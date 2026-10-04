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

namespace AnoMech.Scenarios.Uwu.UltimateAnnihilation;

public sealed class UltimateAnnihilationScenario : IScenario
{
    public string Name => "Ultimate Annihilation";
    public IPhase Phase => UwuZone.Ultima;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UltimateAnnihilationAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private const int WeightOfTheLandPuddles = 4;
    private const float WeightOfTheLandRadius = 6f;
    private const float WeightOfTheLandCastTime = 2.7f;
    private const float FeatherRainRadius = 3f;
    private const float EyeOfTheStormInner = 12f;
    private const float EyeOfTheStormOuter = 25f;
    private const float MesohighRadius = 3f;
    private const float OrbTouchRadius = 1.5f;
    private const float OrbFadeOut = 0.9f;
    // UNVERIFIED: what an orb nobody pops does; treated as a wipe.
    private const float OrbUnpoppedWipeAfter = 6f;
    private const float TankPurgeDamage = 0.56f;
    private const int FlamingCrushMinStack = 5;
    private const float AetheroplasmDamage = 0.16f;
    private const float MesohighDamage = 0.16f;
    private const float SuperCycloneDamage = 0.03f;
    private const float InfernoHowlDamage = 0.1f;
    private const float SearingWindDamage = 0.42f;

    private const int WeightOfTheLandFirstDummies = 0;
    private const int WeightOfTheLandSecondDummies = 4;
    private const int LandslideDummies = 0;
    private const int LandslideAwakenDummies = 5;
    private const int FeatherRainDummies = 8;
    private const int EyeOfTheStormDummy = 13;
    private const int SearingWindDummy = 14;
    private const int SuperCycloneDummy = 15;
    private const int CrimsonCycloneAwakenDummies = 16;
    private const int DummyCount = 18;

    private static readonly Vector3 OrbSpawn = new(2f, 0f, -4f);
    private static readonly Placement UltimaNorth = new(new Vector3(0f, 0f, -10f), 0f);
    private static readonly Placement GarudaSouth = new(new Vector3(0f, 0f, 19.5f), MathF.PI);
    private static readonly Placement IfritSouthEast = new(new Vector3(13.7f, 0f, 13.7f), float.DegreesToRadians(-135f));
    private static readonly Placement TitanSouthWest = new(new Vector3(-13.7f, 0f, 13.7f), float.DegreesToRadians(135f));
    private static readonly Placement[] CrimsonCycloneAwakenFrom =
    [
        new(new Vector3(0f, 0f, 19.3747f), MathF.PI),
        new(new Vector3(19.3747f, 0f, 0f), float.DegreesToRadians(-90f)),
    ];

    private readonly UltimateAnnihilationSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private DamageSolver damage = null!;
    private UltimateAnnihilationState state = null!;

    private SimEnemy? ultima;
    private SimEnemy? garuda;
    private SimEnemy? ifrit;
    private SimEnemy? titan;
    private readonly SimEnemy?[] dummies = new SimEnemy?[DummyCount];
    private readonly List<Orb> orbs = [];
    private readonly HashSet<SimCharacter> searingWindHits = [];
    private readonly List<Vector3> superCycloneSpots = [];
    private IReadOnlyList<SimCharacter> crimsonCycloneSnapshot = [];
    private IReadOnlyList<SimCharacter> homingLasersSnapshot = [];

    private sealed class Orb(SimEnemy enemy, float spawnedAt)
    {
        public SimEnemy Enemy { get; } = enemy;
        public float SpawnedAt { get; } = spawnedAt;
        public float? PoppedAt { get; set; }
    }

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        var instanceUtils = new UwuUtils(instanceWorld);
        instanceWorld.Events.Add(1f, () =>
        {
            instanceUtils.UpdateArena(1);
            instanceUtils.UpdateArena(2);
        });
    }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        utils = new UwuUtils(world);
        damage = new DamageSolver(party);
        state = new UltimateAnnihilationState(world.Rng, party, settingsWindow.Overrides);
        orbs.Clear();
        searingWindHits.Clear();
        superCycloneSpots.Clear();
        crimsonCycloneSnapshot = [];
        homingLasersSnapshot = [];

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UltimateAnnihilationState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnBosses);
        world.Events.Add(0f, GiveEveryoneThermalLow);

        world.Events.Add(2.50f, () => CastSelf(ultima, ActionId.UltimateAnnihilation, 2.7f));
        world.Events.Add(5.48f, () => PlayEffect(ultima, ActionId.UltimateAnnihilation, 4.5f));
        world.Events.Add(9.93f, () => ultima?.SetTargetable(false));
        world.Events.Add(10.02f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(12.07f, PlaceBosses);
        world.Events.Add(12.16f, WarpInBosses);
        world.Events.Add(14.21f, () => ultima?.SetTargetable(true));
        world.Events.Add(14.21f, UltimaTargetsMainTank);
        world.Events.Add(14.21f, () => Lockon(Get(state.FlamingCrushTarget), LockonId.FlamingCrush));
        world.Events.Add(14.30f, () => CastSelf(titan, ActionId.WeightOfTheLandTitan, 2.2f));
        WeightOfTheLand(14.30f, 17.28f, WeightOfTheLandFirstDummies);
        world.Events.Add(16.79f, () => PlayEffect(titan, ActionId.WeightOfTheLandTitan, 2.1f));
        WeightOfTheLand(17.33f, 20.32f, WeightOfTheLandSecondDummies);
        world.Events.Add(17.42f, TetherMesohigh);
        world.Events.Add(17.46f, CastEyeOfTheStorm);
        world.Events.Add(18.93f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(19.34f, ResolveFlamingCrush);
        world.Events.Add(20.17f, SpawnOrb);
        WeightOfTheLand(20.32f, 23.31f, WeightOfTheLandFirstDummies);
        world.Events.Add(20.45f, ResolveEyeOfTheStorm);
        world.Events.Add(21.43f, CastInfernoHowl);
        world.Events.Add(22.50f, ResolveMesohigh);
        world.Events.Add(23.39f, ResolveInfernoHowl);
        world.Events.Add(23.53f, SuperCyclone);
        world.Events.Add(24.29f, () => Get(state.SearingWindTarget)?.AddStatus(StatusId.SearingWind, 30f));
        world.Events.Add(24.60f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        utils.FeatherRain(FeatherRainDummyGetters(), 25.95f, 26.11f, 27.10f, at => AddPuddle(at, FeatherRainRadius, 26.81f), KillFeatherRain);
        world.Events.Add(26.52f, SpawnOrb);
        world.Events.Add(27.10f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(28.65f, () => CastSelf(ifrit, ActionId.CrimsonCyclone, 2.7f));
        world.Events.Add(29.36f, SearingWindPulse);
        world.Events.Add(30.25f, () => CastSelf(titan, ActionId.LandslideTitan, 1.9f));
        utils.LandslideLines(() => titan, DummyGetters(LandslideDummies, 5), 30.25f, 32.44f, LandslideType.Normal);
        world.Events.Add(31.35f, SnapshotCrimsonCyclone);
        world.Events.Add(31.63f, () => PlayEffect(ifrit, ActionId.CrimsonCyclone, 2.1f));
        world.Events.Add(31.90f, () => UwuUtils.KillSnapshot(damage, crimsonCycloneSnapshot, ActionId.CrimsonCyclone, "stood in Ifrit's path"));
        world.Events.Add(32.44f, () => PlayEffect(titan, ActionId.LandslideTitan, 4.1f));
        utils.LandslideLines(() => titan, DummyGetters(LandslideAwakenDummies, 5), 32.44f, 34.44f, LandslideType.Awaken);
        world.Events.Add(32.75f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(32.93f, SpawnOrb);
        world.Events.Add(33.77f, ResolveCrimsonCycloneAwaken);
        world.Events.Add(34.48f, CastEyeOfTheStorm);
        world.Events.Add(34.84f, TetherMesohigh);
        world.Events.Add(35.37f, SearingWindPulse);
        world.Events.Add(36.57f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(37.47f, ResolveEyeOfTheStorm);
        world.Events.Add(38.67f, SpawnOrb);
        world.Events.Add(38.71f, () => CastSelf(ultima, ActionId.TankPurge, 3.7f));
        world.Events.Add(39.92f, ResolveMesohigh);
        world.Events.Add(40.94f, SuperCyclone);
        world.Events.Add(41.39f, SearingWindPulse);
        world.Events.Add(42.01f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(42.68f, () => Raidwide(ultima, ActionId.TankPurge, TankPurgeDamage, 2.1f));
        utils.FeatherRain(FeatherRainDummyGetters(), 43.32f, 43.52f, 44.50f, at => AddPuddle(at, FeatherRainRadius, 44.22f), KillFeatherRain);
        world.Events.Add(46.73f, () => ultima?.SetTargetable(false));
        world.Events.Add(46.82f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(47.40f, SearingWindPulse);
        world.Events.Add(48.91f, () => ultima?.SetPosition(new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(49.00f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(51.04f, () => ultima?.SetTargetable(true));
        world.Events.Add(51.26f, () => ultima?.MoveTo(new Vector3(0f, 0f, -2.8f), 3f, MathF.PI));
        world.Events.Add(51.49f, CastEyeOfTheStorm);
        world.Events.Add(53.40f, SearingWindPulse);
        world.Events.Add(54.47f, ResolveEyeOfTheStorm);
        world.Events.Add(55.14f, CastHomingLasers);
        world.Events.Add(57.84f, SnapshotHomingLasers);
        world.Events.Add(58.12f, PlayHomingLasers);
        world.Events.Add(58.40f, ResolveHomingLasers);
        world.Events.Add(60.30f, () => CastSelf(ultima, ActionId.UltimateSuppression, 2.7f));
        world.Events.Add(63.27f, () => PlayEffect(ultima, ActionId.UltimateSuppression, 4.5f));
    }

    public void Tick(float delta, float elapsed)
    {
        if (orbs.Count == 0) return;
        var now = world.Events.Elapsed;
        foreach (var orb in orbs.ToList())
        {
            if (!orb.Enemy.IsActive)
                orbs.Remove(orb);
            else if (orb.PoppedAt is { } poppedAt)
            {
                if (now - poppedAt < OrbFadeOut) continue;
                orb.Enemy.Despawn();
                orbs.Remove(orb);
            }
            else if (party.Find.InsideCircle(orb.Enemy.Position, OrbTouchRadius).Count > 0)
                PopOrb(orb, now);
            else if (now - orb.SpawnedAt >= OrbUnpoppedWipeAfter)
                BurstUnpoppedOrb(orb, now);
        }
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private static bool IsTank(SimCharacter member) => member is ISimPartyMember { Role: PartyRole.MainTank or PartyRole.OffTank };

    private SimEnemy? Dummy(int index) => dummies[index];

    private Func<SimEnemy?>[] DummyGetters(int first, int count) =>
        Enumerable.Range(first, count).Select(i => (Func<SimEnemy?>)(() => dummies[i])).ToArray();

    private Func<SimEnemy?>[] FeatherRainDummyGetters() => DummyGetters(FeatherRainDummies, 5);

    private SimEnemy? SpawnEnemy(uint baseId, uint nameId, Placement placement, bool targetable, bool visible, EnemyListMode enemyList, byte? modeAttributeFlags = null) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: targetable,
            EnemyList: enemyList,
            IsVisible: visible,
            Placement: placement,
            InitialModeAttributeFlags: modeAttributeFlags));

    private SimEnemy? SpawnHiddenPrimal(uint baseId, uint nameId) =>
        SpawnEnemy(baseId, nameId, new Placement(Vector3.Zero, 0f), false, false, EnemyListMode.Never);

    private void SpawnBosses()
    {
        ultima = SpawnEnemy(BNpcBaseId.UltimaWeapon, BNpcNameId.UltimaWeapon, UltimaNorth, true, true, EnemyListMode.Always, 0x11);
        garuda = SpawnHiddenPrimal(BNpcBaseId.Garuda, BNpcNameId.Garuda);
        ifrit = SpawnHiddenPrimal(BNpcBaseId.Ifrit, BNpcNameId.Ifrit);
        titan = SpawnHiddenPrimal(BNpcBaseId.Titan, BNpcNameId.Titan);

        utils.Awaken(ultima, true);
        utils.Awaken(garuda, false);
        utils.Awaken(ifrit, false);
        utils.Awaken(titan, false);

        for (var i = 0; i < dummies.Length; i++)
            dummies[i] = SpawnEnemy(BNpcBaseId.Dummy, BNpcNameId.Dummy, new Placement(Vector3.Zero, 0f), false, true, EnemyListMode.Never);

        UltimaTargetsMainTank();
    }

    private void GiveEveryoneThermalLow()
    {
        foreach (var member in party.ActiveMembers().ToList())
            member.AddStatus(StatusId.ThermalLow);
    }

    private void UltimaTargetsMainTank() => ultima?.SetTarget(Get(PartyRole.MainTank), follow: false);

    private void PlaceBosses()
    {
        ultima?.SetPosition(UltimaNorth);
        garuda?.SetPosition(GarudaSouth);
        ifrit?.SetPosition(IfritSouthEast);
        titan?.SetPosition(TitanSouthWest);
    }

    private void WarpInBosses()
    {
        ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        foreach (var primal in new[] { garuda, ifrit, titan })
        {
            primal?.SetVisible(true);
            primal?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        }
    }

    private void Raidwide(SimEnemy? caster, uint actionId, float fraction, float animationLock)
    {
        PlayEffect(caster, actionId, animationLock);
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, fraction, actionId, "Raidwide", false);
    }

    private void WeightOfTheLand(float castAt, float effectAt, int firstDummy)
    {
        var spots = new Vector3[WeightOfTheLandPuddles];
        world.Events.Add(castAt, () =>
        {
            var targets = RoleList.Random(world.Rng, party, WeightOfTheLandPuddles).List;
            for (var i = 0; i < spots.Length; i++)
            {
                spots[i] = Get(targets[i])?.Position ?? Vector3.Zero;
                dummies[firstDummy + i]?.SetPosition(new Placement(spots[i], 0f));
                AddPuddle(spots[i], WeightOfTheLandRadius, castAt + WeightOfTheLandCastTime);
            }
        });

        for (var i = 0; i < WeightOfTheLandPuddles; i++)
        {
            var index = i;
            Func<Vector3> spot = () => spots[index];
            utils.Cast(() => dummies[firstDummy + index],
                castAt, new() { ActionId = ActionId.WeightOfTheLand, ActionType = ActionType.Action, CastTime = WeightOfTheLandCastTime },
                effectAt, new() { ActionId = ActionId.WeightOfTheLand, AnimationLock = 1.1f, SpellId = (ushort)ActionId.WeightOfTheLand, ActionType = ActionType.Action },
                new() { CastPosition = spot, ActionEffectPosition = spot },
                0.2f, snapshot => UwuUtils.KillSnapshot(damage, snapshot, ActionId.WeightOfTheLand, "stood in a puddle"));
        }
    }

    // Puddles snapshot at the end of their cast, so that is when the bots must be out.
    private void AddPuddle(Vector3 at, float radius, float snapshotAt) =>
        state.Puddles.Add(new UltimateAnnihilationState.Puddle(new Vector2(at.X, at.Z), radius, snapshotAt));

    private void KillFeatherRain(IReadOnlyList<SimCharacter> snapshot) =>
        UwuUtils.KillSnapshot(damage, snapshot, ActionId.FeatherRain, "stood under a feather");

    private void CastEyeOfTheStorm() => CastSelf(Dummy(EyeOfTheStormDummy), ActionId.EyeOfTheStorm, 2.7f);

    private void ResolveEyeOfTheStorm()
    {
        PlayEffect(Dummy(EyeOfTheStormDummy), ActionId.EyeOfTheStorm, 2.1f);
        // The sheet only has the circle; the safe eye is cut out here.
        UwuUtils.KillSnapshot(damage, party.Find.InsideRing(Vector3.Zero, EyeOfTheStormInner, EyeOfTheStormOuter), ActionId.EyeOfTheStorm, "outside the eye");
    }

    private void ResolveFlamingCrush()
    {
        if (Get(state.FlamingCrushTarget) is not { } target || !target.IsAlive()) return;
        PlayEffect(ifrit, ActionId.FlamingCrush, 2.1f, target: target.GameObjectId);
        damage.Resolve(target, ActionId.FlamingCrush, [DamageType.Magic], [(StatusId.AccursedFlame, 3f)], stackMinTargets: FlamingCrushMinStack);
    }

    private void TetherMesohigh() => state.Mesohigh = world.Tether(garuda, End.ClosestPlayer(), TetherId.Mesohigh);

    private void ResolveMesohigh()
    {
        superCycloneSpots.Clear();
        if (state.Mesohigh is not { B: { } holder } tether) return;
        PlayEffect(garuda, ActionId.Mesohigh, 1.1f, target: holder.GameObjectId);
        foreach (var hit in party.Find.InsideCircle(holder.Position, MesohighRadius).ToList())
        {
            if (!hit.HasStatus(StatusId.ThermalLow))
            {
                damage.ApplyDamage(hit, 1f, ActionId.Mesohigh, "took it without Thermal Low", lethal: true);
                continue;
            }
            damage.ApplyDamage(hit, MesohighDamage, ActionId.Mesohigh, "Mesohigh", false);
            hit.RemoveStatus(StatusId.ThermalLow);
            hit.AddStatus(StatusId.ThermalHigh, 3f);
            superCycloneSpots.Add(hit.Position);
        }
        tether.Despawn();
        state.Mesohigh = null;
    }

    // The Thermal Low cleanse bursts where it happened and grazes everyone.
    private void SuperCyclone()
    {
        if (superCycloneSpots.Count == 0) return;
        var burst = Dummy(SuperCycloneDummy);
        foreach (var spot in superCycloneSpots)
        {
            burst?.SetPosition(new Placement(spot, 0f));
            PlayEffect(burst, ActionId.SuperCyclone, 1.1f);
        }
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, SuperCycloneDamage, ActionId.SuperCyclone, "Super Cyclone", false);
    }

    private void CastInfernoHowl()
    {
        if (Get(state.SearingWindTarget) is not { } target) return;
        ifrit?.Face(target);
        ifrit?.NativeCast(ActionId.InfernoHowl, ActionType.Action, 0f, 1.7f, false, targetId: target.GameObjectId);
    }

    private void ResolveInfernoHowl()
    {
        if (Get(state.SearingWindTarget) is not { } target || !target.IsAlive()) return;
        PlayEffect(ifrit, ActionId.InfernoHowl, 2.1f, target: target.GameObjectId);
        damage.ApplyDamage(target, InfernoHowlDamage, ActionId.InfernoHowl, "Inferno Howl", false);
    }

    // A second pulse on the same bystander is lethal.
    private void SearingWindPulse()
    {
        if (Get(state.SearingWindTarget) is not { } carrier || !carrier.IsAlive()) return;
        var pulse = Dummy(SearingWindDummy);
        pulse?.SetPosition(new Placement(carrier.Position, 0f));
        PlayEffect(pulse, ActionId.SearingWind, 1.1f);
        foreach (var hit in party.Find.InsideActionAoe(ActionId.SearingWind, carrier.Placement()))
        {
            if (hit == carrier) continue;
            damage.ApplyDamage(hit, SearingWindDamage, ActionId.SearingWind, "stood by the Searing Wind healer twice", !searingWindHits.Add(hit));
        }
    }

    private void SnapshotCrimsonCyclone() =>
        crimsonCycloneSnapshot = ifrit == null ? [] : party.Find.InsideActionAoe(ActionId.CrimsonCyclone, ifrit.Placement());

    private void ResolveCrimsonCycloneAwaken()
    {
        for (var i = 0; i < CrimsonCycloneAwakenFrom.Length; i++)
        {
            var from = CrimsonCycloneAwakenFrom[i];
            var dummy = Dummy(CrimsonCycloneAwakenDummies + i);
            dummy?.SetPosition(from);
            PlayEffect(dummy, ActionId.CrimsonCycloneAwaken, 2.1f, from.Rotation);
            damage.Resolve(IPositioned.From(from), ActionId.CrimsonCycloneAwaken, [DamageType.Lethal], []);
        }
    }

    private void SpawnOrb()
    {
        if (SpawnEnemy(BNpcBaseId.Aetheroplasm, BNpcNameId.Aetheroplasm, new Placement(OrbSpawn, 0f), false, true, EnemyListMode.Never) is not { } enemy) return;
        orbs.Add(new Orb(enemy, world.Events.Elapsed));
        state.UnpoppedOrbs++;
    }

    private void PopOrb(Orb orb, float now)
    {
        orb.PoppedAt = now;
        state.UnpoppedOrbs--;
        PlayEffect(orb.Enemy, ActionId.Aetheroplasm, 1.1f);
        foreach (var hit in party.Find.InsideActionAoe(ActionId.Aetheroplasm, orb.Enemy.Placement()))
            damage.ApplyDamage(hit, AetheroplasmDamage, ActionId.Aetheroplasm, "only tanks should pop the orbs", !IsTank(hit));
    }

    private void BurstUnpoppedOrb(Orb orb, float now)
    {
        orb.PoppedAt = now;
        state.UnpoppedOrbs--;
        PlayEffect(orb.Enemy, ActionId.Aetheroplasm, 1.1f);
        party.WipeAllPlayers("Died to Aetheroplasm (an orb was never popped)");
    }

    private void CastHomingLasers()
    {
        if (Get(PartyRole.OffTank) is not { } offTank) return;
        ultima?.NativeCast(ActionId.HomingLasers, ActionType.Action, 0f, 2.7f, false, targetId: offTank.GameObjectId);
    }

    private void SnapshotHomingLasers() =>
        homingLasersSnapshot = Get(PartyRole.OffTank) is { } offTank
            ? party.Find.InsideActionAoe(ActionId.HomingLasers, new Placement(offTank.Position, 0f))
            : [];

    private void PlayHomingLasers()
    {
        if (Get(PartyRole.OffTank) is { } offTank)
            PlayEffect(ultima, ActionId.HomingLasers, 2.1f, target: offTank.GameObjectId);
    }

    private void ResolveHomingLasers()
    {
        UwuUtils.KillSnapshot(damage, homingLasersSnapshot.Where(hit => !IsTank(hit)), ActionId.HomingLasers, "tank buster");
    }
}
