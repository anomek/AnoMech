using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Scenarios.Uwu.UwuUtils;
using static AnoMech.Scenarios.Uwu.P3Titan.UwuP3TitanState;

namespace AnoMech.Scenarios.Uwu.P3Titan;

// Landslides follow Titan's facing at cast start.
public sealed class UwuP3TitanScenario : IScenario
{
    public string Name => "Titan";
    public IPhase Phase => UwuZone.Titan;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UwuP3TitanAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private readonly UwuP3TitanSettingsWindow settingsWindow = new();

    private const float LandslideLength = 40f;
    private const float LandslideKnockback = 15f;
    private const float UpheavalKnockback = 24f;
    private const float FreefireRadius = 6f;
    // Gaols chain along spots 6.7-7y apart; a player's gaol may land this far off its spot.
    private const float GaolSpotTolerance = 2.5f;
    private const float GaolChainDelay = 0.7f;
    private const float BurstReachesGaolAfter = 0.35f;
    private const float GaolDespawnsAfterBreaking = 1f;
    private const float BombFadesAfterBurst = 0.3f;
    private const float HelperLifetime = 1.5f;
    private const float GeocrushLandingDamage = 0.5f;
    private const float GeocrushLandingFalloff = 20f;
    private const float GeocrushJumpDamage = 0.8f;
    private const float GeocrushJumpFalloff = 32f;
    private const float GeocrushSafeBand = 7f;
    private const float EarthenFuryDamage = 0.57f;
    private const float TumultDamage = 0.13f;
    private const float RockBusterDamage = 0.28f;
    private const float MountainBusterDamage = 0.6f;
    private const float UpheavalDamage = 0.26f;
    private const float LandslideDamage = 0.76f;
    private const float PrisonerFreedAfter = 1.1f;
    private const float TankBusterHalfAngle = MathF.PI / 4f;
    private const uint HealerGaolMaxHp = 1_300_000;
    private const float HealerGaolDrainFrom = 99.5f;
    private const float HealerGaolDrainTo = 102.0f;
    private const float JumpTurnSpeed = 3f;
    private const string ShrunkenFloorDeath = "Fell off the shrunken floor";
    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private DamageSolver damage = null!;
    private UwuP3TitanState state = null!;

    private SimEnemy? titan;
    private SimEventObject? floor;
    private bool titanFacesTank;
    private float? turningTo;
    private PartyRole? busterTank;
    private SimEnemy? healerGaol;
    private float landslideRotation;
    private readonly List<SimEnemy> helpers = [];
    private readonly SimEnemy?[] bombs = new SimEnemy?[6];
    private readonly SimEnemy?[] lateBombs = new SimEnemy?[4];
    private readonly Dictionary<SimEnemy, PartyRole> gaols = [];
    private readonly List<(SimEnemy? Caster, float Rotation)> landslideCasters = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        utils = new UwuUtils(world);
        damage = new DamageSolver(party);
        state = new UwuP3TitanState(world.Rng, settingsWindow.Overrides, party.Player != null ? party.PlayerRole : null);
        titan = null;
        floor = null;
        titanFacesTank = false;
        turningTo = null;
        busterTank = null;
        healerGaol = null;
        helpers.Clear();
        gaols.Clear();
        landslideCasters.Clear();
        Array.Clear(bombs);
        Array.Clear(lateBombs);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UwuP3TitanState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnTitan);
        world.Events.Add(2.56f, () => titan?.SetVisible(true));
        world.Events.Add(2.56f, () => CastSelf(titan, ActionId.GeocrushLanding, 2.7f));
        world.Events.Add(5.55f, () => Geocrush(ActionId.GeocrushLanding, GeocrushLandingDamage, GeocrushLandingFalloff, Geometry.ArenaRadius));
        world.Events.Add(7.91f, () => TitanTargetable(true));
        world.Events.Add(8.00f, () => CastSelf(titan, ActionId.EarthenFury, 2.7f));
        world.Events.Add(10.98f, () => Raidwide(ActionId.EarthenFury, EarthenFuryDamage));
        world.Events.Add(19.17f, () => TankBuster(PartyRole.MainTank, ActionId.RockBuster, RockBusterDamage));
        world.Events.Add(22.29f, () => TankBuster(PartyRole.MainTank, ActionId.MountainBuster, MountainBusterDamage));

        world.Events.Add(24.39f, () => CastWeights(0, 27.37f));
        world.Events.Add(26.88f, () => PlayEffect(titan, ActionId.WeightOfTheLandTitan, 1.1f));
        world.Events.Add(27.37f, () => ResolveWeights(0));
        world.Events.Add(27.41f, () => CastWeights(1, 30.40f));

        world.Events.Add(29.40f, () => FaceJump(state.FirstJumpBearing));
        world.Events.Add(30.40f, () => ResolveWeights(1));
        world.Events.Add(30.90f, () => Leave(titan));
        world.Events.Add(32.00f, () => titan?.SetVisible(false));
        world.Events.Add(32.60f, () => LandOnEdge(state.FirstJumpBearing));
        world.Events.Add(32.67f, () => CastSelf(titan, ActionId.GeocrushJump, 2.7f));
        world.Events.Add(32.40f, () => floor = utils.SpawnTitanArena());
        world.Events.Add(32.67f, () => AnimateFloor(1, 2));
        world.Events.Add(35.66f, () => Geocrush(ActionId.GeocrushJump, GeocrushJumpDamage, GeocrushJumpFalloff, Geometry.ArenaRadius));
        world.Events.Add(36.70f, () => world.EnforceArenaBoundary(FirstShrinkRadius, ShrunkenFloorDeath));
        world.Events.Add(38.02f, () => TitanTargetable(true));

        world.Events.Add(40.01f, () => SpawnUpheavalBombs(41.14f));
        world.Events.Add(40.00f, FaceTheCentreThroughUpheaval);
        world.Events.Add(40.21f, () => CastSelf(titan, ActionId.Upheaval, 3.7f));
        world.Events.Add(41.14f, () => BuryBombs(bombs, 0, 5));
        world.Events.Add(43.23f, () => CastBursts(bombs, 0, 5, 46.71f));
        world.Events.Add(44.17f, Upheaval);
        world.Events.Add(46.31f, MarkGaolTargets);
        world.Events.Add(46.71f, () => ResolveBursts(bombs, 0, 5));
        world.Events.Add(48.27f, () => SpawnBomb(bombs, 5, state.SixthBomb, 49.16f));
        world.Events.Add(48.49f, () => titanFacesTank = true);
        world.Events.Add(48.49f, () => CastLandslide(50.68f, state.GaolWindowLandslideTarget));
        world.Events.Add(49.16f, () => BuryBombs(bombs, 5, 1));
        world.Events.Add(50.68f, ResolveLandslide);
        world.Events.Add(51.26f, () => Jail(state.GaolTargets));
        world.Events.Add(51.26f, () => CastBursts(bombs, 5, 1, 54.74f));
        world.Events.Add(52.30f, () => SpawnGaols(state.GaolTargets, [55.09f, 55.79f, 56.49f]));
        world.Events.Add(52.95f, () => CastLandslide(55.14f, aimAt: null));
        world.Events.Add(53.40f, () => CastGraniteImpact(ActionId.GraniteImpactGaols, 17.7f));
        world.Events.Add(54.74f, BurstSixthBombIntoGaols);
        world.Events.Add(55.14f, ResolveLandslide);
        world.Events.Add(57.28f, Tumult);
        world.Events.Add(58.40f, Tumult);
        world.Events.Add(59.51f, Tumult);
        world.Events.Add(60.63f, Tumult);
        world.Events.Add(61.74f, Tumult);
        world.Events.Add(62.86f, Tumult);
        world.Events.Add(63.97f, Tumult);
        world.Events.Add(65.08f, Tumult);
        world.Events.Add(71.10f, () => GraniteImpact(ActionId.GraniteImpactGaols));
        world.Events.Add(71.43f, () => utils.Awaken(titan, false));

        world.Events.Add(70.21f, () => CastWeights(2, 73.20f));
        world.Events.Add(72.70f, () => PlayEffect(titan, ActionId.WeightOfTheLandTitan, 1.1f));
        world.Events.Add(73.20f, () => ResolveWeights(2));
        world.Events.Add(73.25f, () => CastWeights(3, 76.23f));
        world.Events.Add(74.00f, () => titan?.MoveTo(state.FromJumpFrame(new Vector3(8.3f, 0f, 0f)), 3f));
        world.Events.Add(76.05f, () => CastLandslide(78.24f, state.AwakenedLandslideTargets[0], 80.24f));
        world.Events.Add(76.23f, () => ResolveWeights(3));
        world.Events.Add(78.24f, ResolveLandslide);
        world.Events.Add(78.28f, CastAwakenedSecondHit);
        world.Events.Add(80.24f, ResolveAwakenedSecondHit);

        world.Events.Add(83.75f, () => FaceJump(state.SecondJumpBearing));
        world.Events.Add(85.25f, () => Leave(titan));
        world.Events.Add(86.35f, () => titan?.SetVisible(false));
        world.Events.Add(86.95f, () => LandOnEdge(state.SecondJumpBearing));
        world.Events.Add(87.02f, () => CastSelf(titan, ActionId.GeocrushJump, 2.7f));
        world.Events.Add(87.02f, () => AnimateFloor(10, 20));
        world.Events.Add(90.00f, () => Geocrush(ActionId.GeocrushJump, GeocrushJumpDamage, GeocrushJumpFalloff, FirstShrinkRadius));
        world.Events.Add(91.00f, () => world.EnforceArenaBoundary(SecondShrinkRadius, ShrunkenFloorDeath));
        world.Events.Add(92.36f, () => TitanTargetable(true));
        world.Events.Add(92.40f, PullTitanTowardCentre);
        world.Events.Add(92.45f, () => MarkGaolTargets([state.JailedHealer]));
        world.Events.Add(97.39f, () => Jail([state.JailedHealer]));
        world.Events.Add(98.45f, SpawnHealerGaol);
        world.Events.Add(99.53f, () => CastGraniteImpact(ActionId.GraniteImpact, 6.7f));
        world.Events.Add(104.84f, () => CastLandslide(107.02f, state.JailedHealer, 109.02f));
        world.Events.Add(106.23f, () => GraniteImpact(ActionId.GraniteImpact));
        world.Events.Add(107.02f, ResolveLandslide);
        world.Events.Add(107.06f, CastAwakenedSecondHit);
        world.Events.Add(109.02f, ResolveAwakenedSecondHit);
        world.Events.Add(112.18f, Tumult);
        world.Events.Add(113.29f, Tumult);
        world.Events.Add(114.41f, Tumult);
        world.Events.Add(115.52f, Tumult);
        world.Events.Add(116.64f, Tumult);
        world.Events.Add(117.75f, Tumult);
        world.Events.Add(116.00f, () => busterTank = PartyRole.OffTank);
        world.Events.Add(119.88f, () => TankBuster(PartyRole.OffTank, ActionId.RockBuster, RockBusterDamage));
        world.Events.Add(123.98f, () => TankBuster(PartyRole.OffTank, ActionId.MountainBuster, MountainBusterDamage));
        world.Events.Add(125.50f, () => busterTank = null);
        world.Events.Add(125.60f, () => titan?.MoveTo(AtBearing(state.SecondJumpBearing, 7.5f), 3f));

        world.Events.Add(126.08f, ForewarnLateBombs);
        world.Events.Add(128.00f, () => SpawnBomb(lateBombs, 0, state.LateBomb(0), 129.10f));
        world.Events.Add(128.17f, () => CastWeights(4, 131.16f));
        world.Events.Add(129.10f, () => BuryBombs(lateBombs, 0, 1));
        world.Events.Add(130.09f, () => SpawnBomb(lateBombs, 1, state.LateBomb(1), 131.11f));
        world.Events.Add(130.66f, () => PlayEffect(titan, ActionId.WeightOfTheLandTitan, 1.1f));
        world.Events.Add(131.11f, () => BuryBombs(lateBombs, 1, 1));
        world.Events.Add(131.16f, () => ResolveWeights(4));
        world.Events.Add(131.20f, () => CastWeights(5, 134.19f));
        world.Events.Add(131.20f, () => CastBursts(lateBombs, 0, 1, 134.68f));
        world.Events.Add(131.96f, () => SpawnBomb(lateBombs, 2, state.LateBomb(2), 133.12f));
        world.Events.Add(133.12f, () => BuryBombs(lateBombs, 2, 1));
        world.Events.Add(133.21f, () => CastBursts(lateBombs, 1, 1, 136.69f));
        world.Events.Add(134.08f, () => SpawnBomb(lateBombs, 3, state.LateBomb(3), 135.12f));
        world.Events.Add(134.19f, () => ResolveWeights(5));
        world.Events.Add(134.19f, () => CastWeights(6, 137.18f));
        world.Events.Add(134.41f, () => CastLandslide(136.60f, state.AwakenedLandslideTargets[1], 138.60f));
        world.Events.Add(134.68f, () => ResolveBursts(lateBombs, 0, 1));
        world.Events.Add(135.12f, () => BuryBombs(lateBombs, 3, 1));
        world.Events.Add(135.21f, () => CastBursts(lateBombs, 2, 1, 138.69f));
        world.Events.Add(136.60f, ResolveLandslide);
        world.Events.Add(136.64f, CastAwakenedSecondHit);
        world.Events.Add(136.69f, () => ResolveBursts(lateBombs, 1, 1));
        world.Events.Add(137.18f, () => ResolveWeights(6));
        world.Events.Add(137.22f, () => CastBursts(lateBombs, 3, 1, 140.70f));
        world.Events.Add(138.60f, ResolveAwakenedSecondHit);
        world.Events.Add(138.69f, () => ResolveBursts(lateBombs, 2, 1));
        world.Events.Add(140.70f, () => ResolveBursts(lateBombs, 3, 1));
        world.Events.Add(141.00f, () => busterTank = PartyRole.MainTank);
        world.Events.Add(144.80f, () => TankBuster(PartyRole.MainTank, ActionId.RockBuster, RockBusterDamage));

        world.Events.Add(148.05f, () => Leave(titan));
        world.Events.Add(148.05f, () => AnimateFloor(4, 8));
        world.Events.Add(150.00f, DespawnAll);

    }

    public void Tick(float delta, float elapsed)
    {
        DrainHealerGaol(world.Events.Elapsed);
        if (titan == null) return;
        state.TitanPosition = titan.Position;
        if (turningTo is { } goal) TurnToward(goal, delta);
        if (titanFacesTank && Get(busterTank ?? PartyRole.MainTank) is { } holder && holder.IsAlive() && !titan.IsCasting)
            titan.Face(holder);
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private static bool IsJailed(SimCharacter member) => member.HasStatus(StatusId.Fetters);

    private SimCharacter[] Jailed() => party.ActiveMembers().Where(IsJailed).ToArray();

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

    private void DespawnHelper(SimEnemy? helper)
    {
        if (helper == null) return;
        helpers.Remove(helper);
        helper.Despawn();
    }

    private void SpawnTitan() =>
        titan = SpawnEnemy(BNpcBaseId.Titan, BNpcNameId.Titan, new Placement(Vector3.Zero, MathF.PI), false, false, EnemyListMode.Always);

    private void TitanTargetable(bool targetable)
    {
        titan?.SetTargetable(targetable);
        titanFacesTank = targetable;
        if (targetable && Get(PartyRole.MainTank) is { } tank) titan?.SetTarget(tank, follow: false);
    }

    private void Leave(SimEnemy? enemy)
    {
        TitanTargetable(false);
        enemy?.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    // Titan slowly turns to the cardinal he is about to jump to, the tell the party reads.
    private void FaceJump(float bearing)
    {
        if (titan == null) return;
        titanFacesTank = false;
        turningTo = Facing(titan.Position, AtBearing(bearing, JumpRadius));
    }

    private void TurnToward(float goal, float delta)
    {
        var diff = MathF.IEEERemainder(goal - titan!.Rotation, 2f * MathF.PI);
        var step = JumpTurnSpeed * delta;
        if (MathF.Abs(diff) <= step)
        {
            titan.SetRotation(goal);
            turningTo = null;
            return;
        }
        titan.SetRotation(titan.Rotation + MathF.Sign(diff) * step);
    }

    private void LandOnEdge(float bearing)
    {
        turningTo = null;
        var edge = AtBearing(bearing, JumpRadius);
        titan?.SetPosition(new Placement(edge, FacingCentre(edge)));
        titan?.SetVisible(true);
    }

    private void PullTitanTowardCentre()
    {
        if (titan == null) return;
        var to = AtBearing(state.SecondJumpBearing, 2f);
        titan.MoveTo(to, 3f);
    }

    private void AnimateFloor(ushort oldState, ushort newState)
    {
        if (floor is not { IsAlive: true }) return;
        floor.UpdateSharedTimelineState(oldState, newState);
    }

    // Damage falls off with distance from where Titan lands.
    // The party reads left or right off Titan's facing, so it holds on the centre whatever the tank does.
    private void FaceTheCentreThroughUpheaval()
    {
        titanFacesTank = false;
        if (titan == null) return;
        titan.SetPosition(new Placement(titan.Position, FacingCentre(titan.Position)));
    }

    // UNVERIFIED: survivable band beside the far edge; anyone closer to Titan dies.
    private void Geocrush(uint actionId, float peak, float falloff, float arenaRadius)
    {
        if (titan == null) return;
        PlayEffect(titan, actionId, 2.1f);
        var lethalWithin = Flat(titan.Position).Length() + arenaRadius - GeocrushSafeBand;
        foreach (var member in party.ActiveMembers().ToList())
        {
            var distance = Vector2.Distance(Flat(member.Position), Flat(titan.Position));
            var fraction = peak * MathF.Max(0f, 1f - distance / falloff);
            damage.ApplyDamage(member, fraction, actionId, "Proximity", distance < lethalWithin);
        }
    }

    private void Raidwide(uint actionId, float fraction)
    {
        PlayEffect(titan, actionId, 2.1f);
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, fraction, actionId, "Raidwide", false);
    }

    private void Tumult() => Raidwide(ActionId.Tumult, TumultDamage);

    private void TankBuster(PartyRole role, uint actionId, float fraction)
    {
        if (titan == null || Get(role) is not { } tank || !tank.IsAlive()) return;
        var rotation = Facing(titan.Position, tank.Position);
        titan.SetPosition(new Placement(titan.Position, rotation));
        PlayEffect(titan, actionId, 1.1f, rotation, tank.GameObjectId);
        var hits = damage.Resolve(titan, actionId, [DamageType.TankBuster], [], size: TankBusterHalfAngle, extraRange: titan.HitboxRadius);
        foreach (var survivor in hits.Where(h => h.IsAlive()))
            damage.ApplyDamage(survivor, fraction, actionId, "Tankbuster", false);
    }

    private readonly Dictionary<int, List<(SimEnemy? Caster, Vector3 At)>> weightPuddles = [];

    private void CastWeights(int wave, float resolveAt)
    {
        if (wave is 0 or 2 or 4) CastSelf(titan, ActionId.WeightOfTheLandTitan, 2.2f);
        var puddles = new List<(SimEnemy?, Vector3)>();
        foreach (var role in state.WeightTargets[wave])
        {
            if (Get(role) is not { } target || !target.IsAlive()) continue;
            var at = target.Position;
            var caster = SpawnHelper(at);
            caster?.NativeCast(ActionId.WeightOfTheLand, ActionType.Action, 0f, 2.7f, false, position: at);
            puddles.Add((caster, at));
            state.Hazards.Add(new Hazard(Flat(at), 0f, WeightRadius, resolveAt, false));
        }
        weightPuddles[wave] = puddles;
    }

    private void ResolveWeights(int wave)
    {
        if (!weightPuddles.Remove(wave, out var puddles)) return;
        foreach (var (caster, at) in puddles)
        {
            PlayEffect(caster, ActionId.WeightOfTheLand, 0.1f, at: at);
            damage.Resolve(IPositioned.From(at), ActionId.WeightOfTheLand, [DamageType.Lethal], [], excludeTargets: Jailed());
            world.Events.Add(HelperLifetime, () => DespawnHelper(caster));
        }
    }

    // The sixth bomb's spot is the knockback's landing spot, so the party knows to leave it.
    private void SpawnUpheavalBombs(float buryAt)
    {
        var at = state.UpheavalBombs;
        for (var i = 0; i < at.Count; i++) SpawnBomb(bombs, i, at[i], buryAt);
        state.Hazards.Add(new Hazard(Flat(state.SixthBomb), 0f, BuryRadius, 49.16f, false));
        state.Hazards.Add(new Hazard(Flat(state.SixthBomb), 0f, BurstRadius, 54.74f, false));
    }

    private void SpawnBomb(SimEnemy?[] set, int index, Vector3 at, float buryAt)
    {
        set[index] = SpawnEnemy(BNpcBaseId.BombBoulder, BNpcNameId.BombBoulder, new Placement(at, 0f), false, true, EnemyListMode.Never);
        state.Hazards.Add(new Hazard(Flat(at), 0f, BuryRadius, buryAt, false));
    }

    private void BuryBombs(SimEnemy?[] set, int from, int count)
    {
        for (var i = from; i < from + count; i++)
        {
            if (set[i] is not { } bomb) continue;
            bomb.SetVisible(true);
            PlayEffect(bomb, ActionId.Bury, 0.6f);
            damage.Resolve(bomb, ActionId.Bury, [DamageType.Lethal], [], excludeTargets: Jailed());
        }
    }

    // The party knows the four corners the late bombs fall on before they drop.
    private void ForewarnLateBombs()
    {
        float[] buryAt = [129.10f, 131.11f, 133.12f, 135.12f];
        float[] burstAt = [134.68f, 136.69f, 138.69f, 140.70f];
        for (var i = 0; i < 4; i++)
        {
            var at = Flat(state.LateBomb(i));
            state.Hazards.Add(new Hazard(at, 0f, BuryRadius, buryAt[i], false));
            state.Hazards.Add(new Hazard(at, 0f, BurstRadius, burstAt[i], false));
        }
    }

    private void CastBursts(SimEnemy?[] set, int from, int count, float burstAt)
    {
        for (var i = from; i < from + count; i++)
        {
            if (set[i] is not { } bomb) continue;
            bomb.NativeCast(ActionId.Burst, ActionType.Action, 0f, 3.2f, false, targetId: bomb.GameObjectId);
            state.Hazards.Add(new Hazard(Flat(bomb.Position), 0f, BurstRadius, burstAt, false));
        }
    }

    private void ResolveBursts(SimEnemy?[] set, int from, int count)
    {
        for (var i = from; i < from + count; i++)
        {
            if (set[i] is not { } bomb) continue;
            PlayEffect(bomb, ActionId.Burst, 2.1f);
            damage.Resolve(bomb, ActionId.Burst, [DamageType.Lethal], [], excludeTargets: Jailed());
            set[i] = null;
            world.Events.Add(BombFadesAfterBurst, bomb.FadeOut);
            world.Events.Add(HelperLifetime, bomb.Despawn);
        }
    }

    private void Upheaval()
    {
        if (titan == null) return;
        PlayEffect(titan, ActionId.Upheaval, 2.1f);
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, UpheavalDamage, ActionId.Upheaval, "Knockback", false);
        party.Knockback(titan.Position, UpheavalKnockback);
    }

    private void MarkGaolTargets() => MarkGaolTargets(state.GaolTargets);

    // The automarker lives in the AI; this is only Titan's Rock Throw on each target.
    private void MarkGaolTargets(IReadOnlyList<PartyRole> roles)
    {
        foreach (var role in roles)
            if (Get(role) is { } target && target.IsAlive())
                PlayEffect(titan, ActionId.RockThrow, 1.1f, target: target.GameObjectId);
    }

    private void Jail(IEnumerable<PartyRole> roles)
    {
        foreach (var role in roles)
        {
            if (Get(role) is not { } target || !target.IsAlive()) continue;
            target.StopMoving();
            target.AddStatus(StatusId.Fetters, 25f);
            state.Jailed.Add(role);
        }
    }

    // The player's gaol has to land on its numbered waymark or the chain order breaks.
    // The bots keep clear of every gaol for the whole window its Freefire chain can go off in.
    private void SpawnGaols(IEnumerable<PartyRole> roles, float[] freefireAt)
    {
        foreach (var role in roles)
        {
            if (Get(role) is not { } target || !IsJailed(target)) continue;
            if (SpawnEnemy(BNpcBaseId.GraniteGaol, BNpcNameId.GraniteGaol, new Placement(target.Position, 0f), true, true, EnemyListMode.Always) is not { } gaol) continue;
            gaols[gaol] = role;
            foreach (var at in freefireAt) state.Hazards.Add(new Hazard(Flat(gaol.Position), 0f, FreefireRadius, at, false));
        }
    }

    private void CastGraniteImpact(uint actionId, float castSeconds)
    {
        foreach (var gaol in gaols.Keys) CastSelf(gaol, actionId, castSeconds);
    }

    private void BurstSixthBombIntoGaols()
    {
        if (bombs[5] is not { } bomb) return;
        var reached = gaols.Keys.Where(g => Vector2.Distance(Flat(g.Position), Flat(bomb.Position)) <= BurstRadius + g.HitboxRadius + GaolSpotTolerance).ToList();
        ResolveBursts(bombs, 5, 1);
        foreach (var gaol in reached) world.Events.Add(BurstReachesGaolAfter, () => BreakGaol(gaol, explode: true));
    }

    // Freefire chains to gaols in reach; the prisoner is freed ~1.1s later.
    private void BreakGaol(SimEnemy gaol, bool explode)
    {
        if (!gaols.Remove(gaol, out var role)) return;
        if (explode)
        {
            PlayEffect(gaol, ActionId.Freefire, 1.1f);
            var at = gaol.Position;
            damage.Resolve(gaol, ActionId.Freefire, [DamageType.Lethal], [], excludeTargets: Jailed());
            foreach (var next in gaols.Keys.Where(g => Vector2.Distance(Flat(g.Position), Flat(at)) <= FreefireRadius + g.HitboxRadius + GaolSpotTolerance).ToList())
                world.Events.Add(GaolChainDelay, () => BreakGaol(next, explode: true));
        }
        world.Events.Add(PrisonerFreedAfter, () => Free(role));
        world.Events.Add(GaolDespawnsAfterBreaking, gaol.Despawn);
    }

    private void Free(PartyRole role)
    {
        state.Jailed.Remove(role);
        Get(role)?.RemoveStatus(StatusId.Fetters);
    }

    private void SpawnHealerGaol()
    {
        SpawnGaols([state.JailedHealer], []);
        healerGaol = gaols.FirstOrDefault(g => g.Value == state.JailedHealer).Key;
        SetHealerGaolHp(1f);
    }

    // The bots burst it down; a jailed bot also needs one hit from the player before it breaks.
    private void DrainHealerGaol(float elapsed)
    {
        if (healerGaol is not { IsActive: true } gaol || !gaols.ContainsKey(gaol)) return;
        var hp = 1f - Math.Clamp((elapsed - HealerGaolDrainFrom) / (HealerGaolDrainTo - HealerGaolDrainFrom), 0f, 1f);
        SetHealerGaolHp(hp);
        if (hp <= 0f) BreakGaol(gaol, explode: false);
    }

    private void SetHealerGaolHp(float fraction) => SetHp(healerGaol, HealerGaolMaxHp, fraction);

    private static void SetHp(SimEnemy? enemy, uint maxHp, float fraction) => enemy?.SetHealth(maxHp, fraction);

    // A gaol the chain didn't reach explodes and takes the whole party with it.
    private void GraniteImpact(uint actionId)
    {
        if (gaols.Count == 0) return;
        foreach (var (gaol, role) in gaols.ToList())
        {
            PlayEffect(gaol, actionId, 1.1f);
            gaols.Remove(gaol);
            Free(role);
            gaol.Despawn();
        }
        party.WipeAllPlayers("Died to Granite Impact (a gaol was never broken)");
    }

    // Only the awakened cast has a second hit and aims at a random player; the bots know where it lands from the first cast.
    // A null target keeps the last Landslide's line: the gaol window's two casts share one.
    private void CastLandslide(float hitAt, PartyRole? aimAt, float? secondHitAt = null)
    {
        if (titan == null) return;
        var rotation = landslideRotation = aimAt is not { } role
            ? landslideRotation
            : Get(role) is { } target ? Facing(titan.Position, target.Position) : FacingCentre(titan.Position);
        titan.SetPosition(new Placement(titan.Position, rotation));
        CastSelf(titan, secondHitAt == null ? ActionId.LandslideTitanNormal : ActionId.LandslideTitan, 1.9f);
        CastLandslideLines(rotation, LandslideOffsets, ActionId.LandslideLine, 1.9f, hitAt);
        if (secondHitAt is not { } at) return;
        RegisterLandslideHazards(rotation, AwakenedLandslideOffsets, at);
        state.AwakenedLandslide = new AwakenedLandslideCast(Flat(titan.Position), rotation, at);
    }

    private void CastLandslideLines(float rotation, float[] offsets, uint actionId, float castSeconds, float hitAt)
    {
        foreach (var caster in landslideCasters) DespawnHelper(caster.Caster);
        landslideCasters.Clear();
        foreach (var offset in offsets)
        {
            var lineRotation = rotation - offset * MathF.PI / 180f;
            var caster = SpawnHelper(titan!.Position, lineRotation);
            caster?.NativeCast(actionId, ActionType.Action, 0f, castSeconds, false, rotation: lineRotation, position: titan.Position);
            landslideCasters.Add((caster, lineRotation));
        }
        RegisterLandslideHazards(rotation, offsets, hitAt);
    }

    private void RegisterLandslideHazards(float rotation, float[] offsets, float hitAt)
    {
        foreach (var offset in offsets)
        {
            var lineRotation = rotation - offset * MathF.PI / 180f;
            state.Hazards.Add(new Hazard(Flat(titan!.Position), BearingOfRotation(lineRotation), LandslideHalfWidth, hitAt, true));
        }
    }

    private void CastAwakenedSecondHit()
    {
        if (titan == null) return;
        foreach (var caster in landslideCasters) DespawnHelper(caster.Caster);
        landslideCasters.Clear();
        foreach (var offset in AwakenedLandslideOffsets)
        {
            var lineRotation = landslideRotation - offset * MathF.PI / 180f;
            var caster = SpawnHelper(titan.Position, lineRotation);
            caster?.NativeCast(ActionId.LandslideAwaken, ActionType.Action, 0f, 1.7f, false, rotation: lineRotation, position: titan.Position);
            landslideCasters.Add((caster, lineRotation));
        }
    }

    private void ResolveAwakenedSecondHit() => ResolveLandslide();

    private void ResolveLandslide()
    {
        if (titan == null) return;
        PlayEffect(titan, ActionId.LandslideTitanNormal, 1.1f);
        var hit = new HashSet<SimCharacter>();
        foreach (var (caster, rotation) in landslideCasters)
        {
            PlayEffect(caster, ActionId.LandslideLine, 1.1f, rotation);
            foreach (var member in party.Find.InsideRect(new Placement(titan.Position, rotation), LandslideHalfWidth, LandslideLength))
                if (!IsJailed(member)) hit.Add(member);
        }
        foreach (var member in hit)
        {
            damage.ApplyDamage(member, LandslideDamage, ActionId.LandslideLine, "Landslide", false);
            (member as ISimPartyMember)?.Knockback(titan.Position, LandslideKnockback, 20f);
        }
        foreach (var caster in landslideCasters) world.Events.Add(HelperLifetime, () => DespawnHelper(caster.Caster));
        landslideCasters.Clear();
    }

    private void KillTitan()
    {
        titanFacesTank = false;
        titan?.Despawn();
        titan = null;
    }

    private void DespawnAll()
    {
        KillTitan();
        foreach (var bomb in bombs.Concat(lateBombs)) bomb?.Despawn();
        foreach (var gaol in gaols.Keys) gaol.Despawn();
        foreach (var helper in helpers) helper.Despawn();
        foreach (var role in state.Jailed.ToList()) Free(role);
        Array.Clear(bombs);
        Array.Clear(lateBombs);
        gaols.Clear();
        helpers.Clear();
        landslideCasters.Clear();
    }

    private static float BearingOfRotation(float rotation) => BearingOf(new Vector2(MathF.Sin(rotation), MathF.Cos(rotation)));

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
