using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Scenarios.Uwu.UwuUtils;
using Actions = AnoMech.Scenarios.Uwu.UwuActions;

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
    private const float FeatherRainRadius = 3f;
    private const float OrbTouchRadius = 1.5f;
    private const float OrbArming = 0.8f;
    private const float OrbTetherAfterPop = 0.97f;
    private const float OrbTetherSeconds = 3.07f;
    private const float OrbChaseAfter = 3.2f;
    private const float OrbChaseSpeed = 1.9f;
    private const float OrbBurstAfter = 5.85f;
    private const float ThermalHighSeconds = 3f;

    private static readonly PartyRole[] ThermalLowHolders = [PartyRole.MainTank, PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.PhysRangedDps];
    private static readonly Vector3 OrbSpawn = new(2f, 0f, -4f);
    private static readonly Placement UltimaNorth = new(new Vector3(0f, 0f, -10f), 0f);
    private static readonly Placement GarudaSouth = new(new Vector3(0f, 0f, 19.5f), MathF.PI);
    private static readonly Placement SouthEastCorner = new(new Vector3(13.7f, 0f, 13.7f), float.DegreesToRadians(-135f));
    private static readonly Placement SouthWestCorner = new(new Vector3(-13.7f, 0f, 13.7f), float.DegreesToRadians(135f));
    // The second awakened dash comes from Ifrit's side.
    private static readonly Placement CrimsonCycloneAwakenFromSouth = new(new Vector3(0f, 0f, 19.3747f), MathF.PI);
    private static readonly Placement CrimsonCycloneAwakenFromEast = new(new Vector3(19.3747f, 0f, 0f), float.DegreesToRadians(-90f));
    private static readonly Placement CrimsonCycloneAwakenFromWest = new(new Vector3(-19.3747f, 0f, 0f), float.DegreesToRadians(90f));

    private readonly UltimateAnnihilationSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private UltimateAnnihilationState state = null!;

    private SimEnemy? ultima;
    private SimEnemy? garuda;
    private SimEnemy? ifrit;
    private SimEnemy? titan;
    private readonly List<SimEnemy> unpoppedOrbs = [];
    private readonly Dictionary<SimEnemy, int> orbSoakersNeeded = [];

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
        state = new UltimateAnnihilationState(world.Rng, party, settingsWindow.Overrides);
        unpoppedOrbs.Clear();
        orbSoakersNeeded.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UltimateAnnihilationState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnBosses);
        world.Events.Add(0f, GiveThermalLow);
        world.Events.Add(0.6f, UltimaTargetsMainTank);

        world.Events.Add(2.50f, () => ultima?.Cast(ActionId.UltimateAnnihilation, animationLock: 4.5f));
        world.Events.Add(9.93f, () => ultima?.SetTargetable(false));
        world.Events.Add(10.02f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(12.07f, PlaceBosses);
        world.Events.Add(12.16f, WarpInBosses);
        world.Events.Add(14.21f, () => ultima?.SetTargetable(true));
        world.Events.Add(14.21f, UltimaTargetsMainTank);
        world.Events.Add(14.21f, () => Lockon(Get(state.FlamingCrushTarget), LockonId.FlamingCrush));
        world.Events.Add(14.30f, () => titan?.Cast(ActionId.WeightOfTheLandTitan, animationLock: 2.1f));
        world.Events.Add(14.30f, () => WeightOfTheLand(snapshotAt: 17.00f));
        world.Events.Add(14.32f, () => garuda?.PlayActionTimeline(TimelineId.Garuda1411));
        world.Events.Add(17.33f, () => WeightOfTheLand(snapshotAt: 20.03f));
        world.Events.Add(17.42f, TetherMesohigh);
        world.Events.Add(17.46f, EyeOfTheStorm);
        world.Events.Add(18.32f, UltimaSelf);
        world.Events.Add(18.93f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(19.34f, FlamingCrush);
        world.Events.Add(20.32f, () => WeightOfTheLand(snapshotAt: 23.02f));
        world.Events.Add(20.80f, () => SpawnOrb(soakers: 2));
        world.Events.Add(21.43f, InfernoHowl);
        world.Events.Add(22.50f, ResolveMesohigh);
        world.Events.Add(24.29f, () => Get(state.SearingWindTarget)?.AddStatus(StatusId.SearingWind, 30f, sourceObject: ifrit?.GameObjectId ?? default));
        world.Events.Add(24.45f, UltimaSelf);
        world.Events.Add(24.60f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(25.57f, () => ifrit?.SetPosition(IfritCorner));
        utils.FeatherRain(() => garuda, 24.60f, 26.11f, onTargeted: at => AddPuddle(at, FeatherRainRadius, 26.81f));
        world.Events.Add(26.78f, () => SpawnOrb(soakers: 2));
        world.Events.Add(27.10f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(28.65f, () => ifrit?.Cast(Actions.CrimsonCyclone));
        world.Events.Add(29.36f, SearingWind);
        world.Events.Add(30.25f, () => titan?.Cast(ActionId.LandslideTitan, animationLock: 4.1f));
        utils.LandslideLines(() => titan, 30.25f, LandslideType.Normal);
        world.Events.Add(30.53f, UltimaSelf);
        utils.LandslideLines(() => titan, 32.44f, LandslideType.Awaken);
        world.Events.Add(32.75f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(32.82f, () => SpawnOrb(soakers: 1));
        world.Events.Add(33.77f, CrimsonCycloneAwaken);
        world.Events.Add(34.48f, EyeOfTheStorm);
        world.Events.Add(34.84f, TetherMesohigh);
        world.Events.Add(35.37f, SearingWind);
        world.Events.Add(36.57f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(36.67f, UltimaSelf);
        world.Events.Add(38.71f, () => ultima?.Cast(Actions.TankPurge));
        world.Events.Add(38.83f, () => SpawnOrb(soakers: 1));
        world.Events.Add(39.92f, ResolveMesohigh);
        world.Events.Add(41.39f, SearingWind);
        world.Events.Add(42.01f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        utils.FeatherRain(() => garuda, 42.01f, 43.52f, onTargeted: at => AddPuddle(at, FeatherRainRadius, 44.22f));
        world.Events.Add(46.73f, () => ultima?.SetTargetable(false));
        world.Events.Add(46.82f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(47.40f, SearingWind);
        world.Events.Add(48.91f, () => ultima?.SetPosition(new Placement(Vector3.Zero, 0f)));
        world.Events.Add(49.00f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(51.04f, () => ultima?.SetTargetable(true));
        world.Events.Add(51.26f, () => ultima?.MoveTo(new Vector3(0f, 0f, -2.8f), 3f, MathF.PI));
        world.Events.Add(51.49f, EyeOfTheStorm);
        world.Events.Add(53.40f, SearingWind);
    }

    public void OnStatusLost(StatusLost lost)
    {
        SuperCyclone.FollowMesohigh(world, lost, garuda);
        if (lost is not { StatusId: StatusId.ThermalLow, Reason: StatusLossReason.Removed }) return;
        lost.Holder.AddStatus(StatusId.ThermalHigh, ThermalHighSeconds, sourceObject: garuda?.GameObjectId ?? default);
        if (!party.ActiveMembers().Any(m => m != lost.Holder && m.IsAlive() && m.FindStatus(StatusId.ThermalLow) != null))
            world.Fail("Every Thermal Low was cleansed");
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved.Action != Actions.Aetheroplasm) return;
        if (resolved.Caster is SimEnemy orb && orbSoakersNeeded.Remove(orb, out var needed) && resolved.Hits.Count < needed)
            world.Fail($"An orb went off with {resolved.Hits.Count} in it; it needs {needed}");
        foreach (var hit in resolved.Hits)
            world.Events.Add(OrbTetherAfterPop, () => world.Tether(hit, ultima, TetherId.Aetheroplasm, OrbTetherSeconds));
    }

    public void Tick(float delta, float elapsed)
    {
        foreach (var orb in unpoppedOrbs.ToList())
        {
            if (!orb.IsActive)
                unpoppedOrbs.Remove(orb);
            else if (party.Find.InsideCircle(orb.Position, OrbTouchRadius).Count > 0)
                PopOrb(orb);
        }
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private SimEnemy? SpawnEnemy(uint baseId, uint nameId, uint layoutId, Placement placement, bool targetable, bool visible, EnemyListMode enemyList,
        byte? modeAttributeFlags = null, uint? displayFlags = null, IReadOnlyList<SpawnStatus>? statuses = null, byte animationState = 0) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: targetable,
            EnemyList: enemyList,
            Visibility: visible ? SpawnVisibility.Visible : SpawnVisibility.HiddenUntilShown,
            Placement: placement,
            InitialModeAttributeFlags: modeAttributeFlags,
            DisplayFlags: displayFlags,
            LayoutId: layoutId,
            Statuses: statuses,
            AnimationState: animationState));

    private SimEnemy? SpawnHiddenPrimal(uint baseId, uint nameId, uint layoutId) =>
        SpawnEnemy(baseId, nameId, layoutId, new Placement(Vector3.Zero, 0f), false, false, EnemyListMode.Never,
            statuses: UwuUtils.WokenAtSpawn(isUltima: false), animationState: UwuUtils.AwakenedAnimationState);

    private void SpawnBosses()
    {
        ultima = SpawnEnemy(BNpcBaseId.UltimaWeapon, BNpcNameId.UltimaWeapon, BNpcLayoutId.UltimaWeapon, UltimaNorth, true, true, EnemyListMode.Always, 0x11,
            statuses: UwuUtils.WokenAtSpawn(isUltima: true), animationState: UwuUtils.AwakenedAnimationState);
        garuda = SpawnHiddenPrimal(BNpcBaseId.Garuda, BNpcNameId.Garuda, BNpcLayoutId.Garuda);
        ifrit = SpawnHiddenPrimal(BNpcBaseId.Ifrit, BNpcNameId.Ifrit, BNpcLayoutId.Ifrit);
        titan = SpawnHiddenPrimal(BNpcBaseId.Titan, BNpcNameId.Titan, BNpcLayoutId.Titan);
    }

    private void GiveThermalLow()
    {
        foreach (var role in ThermalLowHolders)
            Get(role)?.AddStatus(StatusId.ThermalLow);
    }

    private void UltimaSelf() => ultima?.Cast(ActionId.UltimaSelf, animationLock: 2.1f);

    private void UltimaTargetsMainTank() => ultima?.SetTarget(Get(PartyRole.MainTank), follow: false);

    private Placement IfritCorner => state.IfritSouthWest ? SouthWestCorner : SouthEastCorner;

    private void PlaceBosses()
    {
        ultima?.SetPosition(UltimaNorth);
        garuda?.SetPosition(GarudaSouth);
        ifrit?.SetPosition(IfritCorner);
        titan?.SetPosition(state.IfritSouthWest ? SouthEastCorner : SouthWestCorner);
    }

    private void WarpInBosses()
    {
        ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        foreach (var primal in new[] { garuda, ifrit, titan })
            primal?.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    private void WeightOfTheLand(float snapshotAt)
    {
        foreach (var role in RoleList.Random(world.Rng, party, WeightOfTheLandPuddles).List)
        {
            var spot = Get(role)?.Position ?? Vector3.Zero;
            CastOnLocation(titan, Actions.WeightOfTheLand, spot);
            AddPuddle(spot, WeightOfTheLandRadius, snapshotAt);
        }
    }

    // Puddles snapshot at the end of their cast, so that is when the bots must be out.
    private void AddPuddle(Vector3 at, float radius, float snapshotAt) =>
        state.Puddles.Add(new UltimateAnnihilationState.Puddle(new Vector2(at.X, at.Z), radius, snapshotAt));

    private void FlamingCrush()
    {
        if (Get(state.FlamingCrushTarget) is not { } target || !target.IsAlive()) return;
        ifrit?.Cast(Actions.FlamingCrush, target);
    }

    private void TetherMesohigh() => state.Mesohigh = world.Tether(garuda, End.ClosestPlayer(), TetherId.Mesohigh);

    // Thermal Low comes off in Super Cyclone (OnStatusLost).
    private void ResolveMesohigh()
    {
        if (state.Mesohigh is not { B: { } holder } tether) return;
        garuda?.Cast(Actions.Mesohigh, holder);
        tether.Despawn();
        state.Mesohigh = null;
    }

    private void InfernoHowl()
    {
        if (Get(state.SearingWindTarget) is not { } target) return;
        ifrit?.Face(target);
        ifrit?.Cast(Actions.InfernoHowl, target);
    }

    private void EyeOfTheStorm() => garuda?.CastFromHelper(Actions.EyeOfTheStorm, at: new Placement(Vector3.Zero, 0f));

    // Cast from a helper on the holder, whom it spares.
    private void SearingWind()
    {
        if (Get(state.SearingWindTarget) is not { } holder || !holder.IsAlive()) return;
        ifrit?.CastFromHelper(Actions.SearingWind, holder, at: holder.Placement());
    }

    private void CrimsonCycloneAwaken()
    {
        Placement[] froms = [CrimsonCycloneAwakenFromSouth, state.IfritSouthWest ? CrimsonCycloneAwakenFromWest : CrimsonCycloneAwakenFromEast];
        foreach (var from in froms)
            ifrit?.CastFromHelper(Actions.CrimsonCycloneAwaken, at: from);
    }

    private void SpawnOrb(int soakers)
    {
        if (SpawnEnemy(BNpcBaseId.Aetheroplasm, BNpcNameId.Aetheroplasm, BNpcLayoutId.Aetheroplasm, new Placement(OrbSpawn, 0f), false, true,
                EnemyListMode.Never, displayFlags: 0x40008) is not { } orb) return;
        state.UnpoppedOrbs++;
        orbSoakersNeeded[orb] = soakers;
        world.Events.Add(OrbArming, () => unpoppedOrbs.Add(orb));
        world.Events.Add(OrbChaseAfter, () => ChaseRandomPlayer(orb));
        world.Events.Add(OrbBurstAfter, () => PopOrb(orb));
    }

    // UNVERIFIED: who an orb chases; its pick is random here.
    private void ChaseRandomPlayer(SimEnemy orb)
    {
        if (!unpoppedOrbs.Contains(orb)) return;
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        if (alive.Count > 0) orb.Follow(alive[world.Rng.Next(alive.Count)], OrbChaseSpeed);
    }

    private void PopOrb(SimEnemy orb)
    {
        if (!unpoppedOrbs.Contains(orb)) return;
        RemoveOrb(orb);
        orb.StopMoving();
        orb.Cast(Actions.Aetheroplasm);
    }

    private void RemoveOrb(SimEnemy orb)
    {
        unpoppedOrbs.Remove(orb);
        state.UnpoppedOrbs--;
    }
}
