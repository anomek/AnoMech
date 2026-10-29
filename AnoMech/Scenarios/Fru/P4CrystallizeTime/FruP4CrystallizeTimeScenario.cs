using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4CrystallizeTime.FruP4CrystallizeTimeState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

public sealed class FruP4CrystallizeTimeScenario : IMultiplayerReplayable
{
    public string Name => "Crystallize Time";
    public IPhase Phase => FruZone.P4;
    public bool SupportsMultiplayer => true;
    // The strat reacts to the Wyrmclaw and Wyrmfang it sees going, and the Stun holds everyone.
    public bool MirrorsHostStatuses => true;

    public IReadOnlyList<IScenarioAi> AiStrats => strats;
    private readonly IReadOnlyList<IScenarioAi> strats = [new FruP4CrystallizeTimeAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly FruP4CrystallizeTimeSettingsWindow settingsWindow = new();

    private const float PuddleLifetime = 17.8f;
    private const float PuddleGrace = 1f;
    private const float PuddleRadius = 1f;
    private const float PuddleDelay = 0.9f;
    private const float PuddleFadeOut = 3.3f;
    // Each puddle's Arg2 index counts from here in drop order, east before west.
    private const uint PuddleArg2Base = 0x50;
    // The ModelScale row the heads spawn at, and the Activate state that shrinks them at their
    // first catch.
    private const byte DragonSpawnScale = 8;
    private const uint DragonShrunkState = 4;
    // It takes every one of these and breaks on any other hit.
    private static readonly uint[] FragmentSurvives =
    [
        ActionId.CrystallizeTimeOracle, ActionId.Maelstrom, ActionId.TidalLightFirst, ActionId.TidalLightRest, ActionId.Quietus,
        ActionId.HallowedWingsKnockback,
    ];

    private SimWorld world = null!;
    private FruP4CrystallizeTimeState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it. LastState aliasing `state` is
    // what lets BuildMidRunUpdateMessage see the puddles as they drop.
    public FruP4CrystallizeTimeState? LastState { get; private set; }
    // Edge-triggers BuildMidRunUpdateMessage.
    private List<DragonPuddleState> sentPuddles = [];
    private IScenarioAi<FruP4CrystallizeTimeState>? ai;
    private SimEnemy? usurper, oracle, fragment;
    private readonly SimEnemy?[] hourglasses = new SimEnemy?[6];
    private readonly SimEnemy?[] dragons = new SimEnemy?[2];
    private readonly int[] dragonCatches = new int[2];
    private readonly float[] dragonReadyAt = new float[2];
    private readonly PartyRole?[] lastCatcher = new PartyRole?[2];
    private readonly List<SimTether> speedTethers = [];
    private readonly Dictionary<PartyRole, (float DroppedAt, SimEventObject? Visual)> puddles = new();
    private readonly Dictionary<PartyRole, Vector3> returns = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP4CrystallizeTimeState(world.Rng, world.Party.PlayerRole, settingsWindow.Overrides);
        LastState = state;
        sentPuddles = [];
        Array.Clear(dragons);
        Array.Clear(dragonCatches);
        Array.Clear(lastCatcher);
        Array.Fill(dragonReadyAt, DragonsMoveAt + DragonHitCooldown);
        speedTethers.Clear();
        puddles.Clear();
        returns.Clear();
        ai = selectedAi is { } idx && idx < AiStrats.Count ? (IScenarioAi<FruP4CrystallizeTimeState>)AiStrats[idx] : null;
        ai?.Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(0.9f, () => world.Map.SetWeather(WeatherId.TimeStop, 0f));
        world.Events.Add(0.9f, () => world.Map.AddEffect(MapEffect.FreezeFloor, MapEffect.OracleStage));
        world.Events.Add(0.9f, () => OracleBattleTalk(0xE));
        world.Events.Add(0.9f, () => usurper?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
        world.Events.Add(0.9f, () => oracle?.SetTarget(world.Party.Get(PartyRole.OffTank), follow: false));
        world.Events.Add(1f, () => usurper?.Cast(Actions.CrystallizeTimeUsurper));
        world.Events.Add(1f, () => oracle?.Cast(Actions.CrystallizeTimeOracle));
        world.Events.Add(6f, () => OracleBattleTalk(0xF));
        world.Events.Add(DebuffsAt, AssignDebuffs);
        world.Events.Add(11.8f, () => usurper?.AddStatus(StatusId.UsurperReturnWaiting, 28.8f - 11.8f, sourceObject: WaveReturnSource(second: false)));
        world.Events.Add(11.8f, () => usurper?.AddStatus(StatusId.UsurperReturnWaiting, 34.8f - 11.8f, sourceObject: WaveReturnSource(second: true)));
        world.Events.Add(11.8f, () => SetHourglassScenery(MapEffect.RelativityHourglassShow, 0, 1, 2, 3, 4, 5));
        world.Events.Add(12f, SpawnHourglasses);
        world.Events.Add(12f, EdgeOfOblivion);
        world.Events.Add(14.1f, () => usurper?.SetTargetable(false));
        world.Events.Add(14.1f, () => usurper?.PlayActionTimeline(TimelineId.UsurperHide));
        world.Events.Add(15.1f, TetherHourglasses);
        world.Events.Add(15.1f, () => oracle?.SetTargetable(false));
        world.Events.Add(15.2f, () => oracle?.Cast(ActionId.Speed, animationLock: 3.1f));
        world.Events.Add(DragonsSpawnAt, SpawnDragons);
        world.Events.Add(16.9f, () => SetHourglassScenery(MapEffect.RelativityHourglassFlowing, 0, 1, 2, 3, 4, 5));
        world.Events.Add(19.9f, () => SetHourglassScenery(MapEffect.RelativityHourglassDraining, state.HourglassPair(0)));
        world.Events.Add(20.7f, () => speedTethers.ForEach(t => t.Despawn()));
        world.Events.Add(20.8f, SpeedUpAndSlowDownHourglasses);
        world.Events.Add(21.5f, () => Maelstrom(0));
        world.Events.Add(21.9f, () => SetHourglassScenery(MapEffect.RelativityHourglassDraining, state.HourglassPair(1)));
        world.Events.Add(23.8f, () => SetHourglassScenery(MapEffect.RelativityHourglassDraining, state.HourglassPair(2)));
        world.Events.Add(23.8f, () => oracle?.PlayActionTimeline(TimelineId.OracleHide));
        world.Events.Add(23.9f, () => CastOnHolders(CrystallizeDebuff.BlueWater, Actions.DarkWater));
        world.Events.Add(25.8f, () => usurper?.SetPosition(Vector3.Zero));
        world.Events.Add(25.8f, () => usurper?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(25.9f, Fireworks);
        world.Events.Add(27f, () => Maelstrom(1));
        world.Events.Add(28f, () => TurnToward(usurper, state.WaveOrigin(false, 0)));
        world.Events.Add(28f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(28.7f, () => MoveFacingTravel(usurper, state.WaveOrigin(false, 0)));
        world.Events.Add(28.8f, () => usurper?.AddStatus(StatusId.UsurperReturn, 56.8f - 28.8f, sourceObject: WaveReturnSource(second: false)));
        world.Events.Add(28.8f, () => CastOnHolders(CrystallizeDebuff.BlueDarkness, Actions.UnholyDarkness));
        world.Events.Add(29.2f, () => usurper?.SetRotation(WaveRotation(second: false)));
        world.Events.Add(29.2f, () => world.PlaceGroundVfx(VfxId.TidalLightOrigin, state.WaveOrigin(false, 0)));
        world.Events.Add(29.3f, () => TidalLight(second: false));
        world.Events.Add(31.95f, () => Maelstrom(2));
        world.Events.Add(34.4f, () => TurnToward(usurper, state.WaveOrigin(true, 0)));
        world.Events.Add(34.4f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(34.8f, () => usurper?.AddStatus(StatusId.UsurperReturn, 51.8f - 34.8f, sourceObject: WaveReturnSource(second: true)));
        world.Events.Add(35.1f, () => MoveFacingTravel(usurper, state.WaveOrigin(true, 0)));
        world.Events.Add(35.6f, () => usurper?.SetRotation(WaveRotation(second: true)));
        world.Events.Add(35.6f, () => world.PlaceGroundVfx(VfxId.TidalLightOrigin, state.WaveOrigin(true, 0)));
        world.Events.Add(35.7f, () => TidalLight(second: true));
        world.Events.Add(42.9f, Quietus);
        world.Events.Add(42.9f, () => oracle?.SetPosition(new Placement(new Vector3(0, 0, 5), 0f)));
        world.Events.Add(43f, () => oracle?.PlayActionTimeline(TimelineId.OracleShow));
        world.Events.Add(ReturnSavedAt, SaveReturns);
        world.Events.Add(45.1f, () => oracle?.Cast(ActionId.SpiritTakerCast, animationLock: 0.2f));
        world.Events.Add(46.2f, () => TurnToward(usurper, Vector3.Zero));
        world.Events.Add(46.2f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(46.9f, () => MoveFacingTravel(usurper, Vector3.Zero));
        world.Events.Add(47.4f, () => usurper?.SetRotation(0f));
        world.Events.Add(47.5f, () => usurper?.Cast(ActionId.HallowedWingsCast, animationLock: 1.4f));
        world.Events.Add(48.5f, SpiritTaker);
        world.Events.Add(StunAt, () => world.Party.ActiveMembers().ToList().ForEach(m => m.AddStatus(StatusId.Stun, StunEndsAt - StunAt)));
        world.Events.Add(52.8f, Rewind);
        world.Events.Add(53.6f, () => PositionUsurperForWaves(second: false));
        world.Events.Add(53.7f, () => usurper?.Cast(Actions.HallowedWingsKnockback));
        world.Events.Add(56.3f, () => usurper?.Cast(Actions.HallowedWingsSwap));
        world.Events.Add(56.6f, VanishHungryHeads);
        world.Events.Add(58.1f, () => PositionUsurperForWaves(second: true));
        world.Events.Add(58.3f, () => usurper?.Cast(Actions.HallowedWingsKnockback));
        world.Events.Add(60.7f, PunishHungryHeads);
        world.Events.Add(62.5f, () => world.Map.SetWeather(WeatherId.Oracle, 0f));
        world.Events.Add(62.5f, () => world.Map.AddEffect(MapEffect.ThawFloor, MapEffect.OracleStage));
        world.Events.Add(62.5f, () => SetHourglassScenery(MapEffect.HourglassHide, 0, 1, 2, 3, 4, 5));
        world.Events.Add(62.7f, RemoveHourglasses);
        world.Events.Add(62.7f, () => TurnToward(usurper, new Vector3(-3.55f, 0, 0)));
        world.Events.Add(62.7f, () => TurnToward(oracle, new Vector3(3.53f, 0, 0)));
        world.Events.Add(62.7f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(62.7f, () => oracle?.Cast(ActionId.OracleReposition, animationLock: 1.1f));
        world.Events.Add(63.3f, () => MoveFacingTravel(usurper, new Vector3(-3.55f, 0, 0)));
        world.Events.Add(63.3f, () => MoveFacingTravel(oracle, new Vector3(3.53f, 0, 0)));
        world.Events.Add(63.8f, () => oracle?.SetRotation(MathF.PI / 4));
        world.Events.Add(63.9f, () => usurper?.SetRotation(-MathF.PI / 4));
        world.Events.Add(64f, () => usurper?.SetTargetable(true));
        world.Events.Add(64f, () => oracle?.SetTargetable(true));
        world.Events.Add(64f, () => usurper?.Cast(ActionId.AkhMornUsurper, animationLock: 4.1f));
        world.Events.Add(64f, () => usurper?.VoiceLine(VoiceLineId.AkhMorn));
        world.Events.Add(64f, () => oracle?.Cast(ActionId.AkhMornOracle, animationLock: 4.1f));
        for (var hit = 0; hit < 4; hit++)
            world.Events.Add(69f + 1.05f * hit, AkhMorn);
    }

    // TODO: the Fragment's HP bar empties when it's hit; a spawned enemy's HP can't be set.
    private void SpawnActors()
    {
        usurper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.UsurperP4,
            NameId: BNpcNameId.Usurper,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(0, 0, 5), 0f),
            InitialModeAttributeFlags: 0xFC,
            LayoutId: LayoutId.UsurperP4,
            Statuses: [new(TransfigurationStatus, UsurperDragonTransformation)],
            ModelState: 0x4));
        oracle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OracleP4,
            NameId: BNpcNameId.Oracle,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.OnlyWhenVisible,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            LayoutId: LayoutId.OracleP4,
            Appearance: Appearance.Oracle));
        fragment = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.FragmentOfFate,
            NameId: BNpcNameId.FragmentOfFate,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(FragmentPosition, 0f),
            DisplayFlags: 0x40003,
            LayoutId: LayoutId.FragmentOfFate,
            IsHostile: false,
            Statuses: [new(StatusId.Incurable)]));
        SpawnVision(BNpcBaseId.VisionOfGaia, BNpcNameId.Gaia, Appearance.VisionOfGaia, new Placement(new Vector3(-0.71f, 0, -16), 1.22f),
            0x4020B, LayoutId.VisionOfGaia, 0x1F);
        SpawnVision(BNpcBaseId.VisionOfRyne, BNpcNameId.Ryne, Appearance.VisionOfRyne, new Placement(new Vector3(0.71f, 0, -16), -0.84f),
            0x4020A, LayoutId.VisionOfRyne, 0x05);
    }

    // Spawned as they stand once shown: retail's DisplayFlags less the hidden-until-shown bit, and
    // the ModelState their show timeline leaves them in.
    private void SpawnVision(uint bnpcBaseId, uint nameId, NpcAppearance appearance, Placement placement, uint displayFlags, uint layoutId,
        byte modelState) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: bnpcBaseId,
        NameId: nameId,
        Level: Level,
        EnemyList: EnemyListMode.Never,
        Placement: placement,
        DisplayFlags: displayFlags,
        LayoutId: layoutId,
        IsHostile: false,
        Appearance: appearance,
        Statuses: [new(StatusId.Levitation, 200)],
        ModelState: modelState));

    private void OracleBattleTalk(uint line)
    {
        if (oracle is { } boss) world.Map.DirectorUpdate(0x80000027U, line, 0x2U, BNpcNameId.Oracle, boss.EntityId);
    }

    private void EdgeOfOblivion()
    {
        fragment?.Cast(Actions.EdgeOfOblivion);
        fragment?.CastFromHelper(Actions.EdgeOfOblivionAnimation);
    }

    // Retail's two instances come from two of its helpers; any two distinct sources keep them apart.
    private GameObjectId WaveReturnSource(bool second) => second ? usurper?.GameObjectId ?? default : default;

    // TODO: retail also shows an actor-bound clock (VFX 1001, d1049_stlp_b0k1) over each player
    // until their Return is saved; there's no way to attach a VFX sheet row to an actor.
    private void AssignDebuffs()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
            var debuff = state.Debuff(role);
            var cleanseBy = debuff == CrystallizeDebuff.RedIce ? RedIceCleanseBy : CleanseBy;
            member.AddStatus(IsRed(debuff) ? StatusId.Wyrmclaw : StatusId.Wyrmfang, cleanseBy - DebuffsAt);
            var (spell, at) = debuff switch
            {
                CrystallizeDebuff.RedAero => (StatusId.DarkAeroWaiting, 25.9f),
                CrystallizeDebuff.BlueEruption => (StatusId.DarkEruptionWaiting, 25.9f),
                CrystallizeDebuff.BlueWater => (StatusId.DarkWaterWaiting, 23.9f),
                CrystallizeDebuff.BlueDarkness => (StatusId.UnholyDarknessWaiting, 28.8f),
                _ => (StatusId.DarkBlizzardWaiting, 25.9f),
            };
            member.AddStatus(spell, at - DebuffsAt);
            member.AddStatus(StatusId.CrystallizeReturnWaiting, ReturnSavedAt - DebuffsAt);
            member.AddStatus(StatusId.RelativityClock, 37f);
        }
        foreach (var role in state.Quietus)
            world.Party.Get(role)?.AddStatus(StatusId.QuietusWaiting, 42.9f - DebuffsAt);
    }

    // The hourglass itself is scenery in its MapEffect slot; the BNpc only casts and holds tethers.
    private void SpawnHourglasses()
    {
        for (var i = 0; i < hourglasses.Length; i++)
            hourglasses[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.SorrowsHourglassP4,
                NameId: BNpcNameId.SorrowsHourglass,
                Level: Level,
                Placement: new Placement(Hourglass(i), MathF.Atan2(-Hourglass(i).X, -Hourglass(i).Z)),
                LayoutId: LayoutId.CrystallizeHourglass));
    }

    private void SetHourglassScenery(uint state, params int[] indexes)
    {
        foreach (var i in indexes) world.Map.AddEffect(state, (byte)(MapEffect.CrystallizeFirstHourglass + i));
    }

    private void TetherHourglasses()
    {
        foreach (var i in state.HourglassPair(0)) speedTethers.Add(world.Tether(hourglasses[i], oracle, TetherId.Quicken));
        foreach (var i in state.HourglassPair(2)) speedTethers.Add(world.Tether(hourglasses[i], oracle, TetherId.Slow));
    }

    private void SpeedUpAndSlowDownHourglasses()
    {
        foreach (var i in state.HourglassPair(0)) oracle?.CastFromHelper(Actions.Quicken, hourglasses[i]);
        foreach (var i in state.HourglassPair(2)) oracle?.CastFromHelper(Actions.Slow, hourglasses[i]);
    }

    private void Maelstrom(int wave)
    {
        SetHourglassScenery(MapEffect.RelativityHourglassEmptying, state.HourglassPair(wave));
        foreach (var i in state.HourglassPair(wave)) hourglasses[i]?.Cast(Actions.Maelstrom);
    }

    private void RemoveHourglasses()
    {
        foreach (var hourglass in hourglasses) hourglass?.Despawn();
    }

    private void BreakFragmentOnAvoidableHit(ActionResolved resolved)
    {
        if (fragment is null || !resolved.NpcHits.Contains(fragment) || FragmentSurvives.Contains(resolved.Action.ActionId)) return;
        world.Fail($"{Natives.Data.ActionName(resolved.Action.ActionId)} hit the Fragment of Fate");
    }

    private void CastOnHolders(CrystallizeDebuff debuff, EnemyAction action)
    {
        foreach (var role in state.Holders(debuff))
            if (world.Party.Get(role) is { } holder && holder.IsAlive())
                oracle?.CastFromHelper(action, holder);
    }

    private void Fireworks()
    {
        CastOnHolders(CrystallizeDebuff.RedAero, Actions.DarkAero);
        CastOnHolders(CrystallizeDebuff.BlueEruption, Actions.DarkEruption);
        CastOnHolders(CrystallizeDebuff.RedIce, Actions.DarkBlizzard);
        CastOnHolders(CrystallizeDebuff.BlueIce, Actions.DarkBlizzard);
    }

    // Retail kills its holder alone, with no action.
    private static void KillUncleansed(StatusLost lost)
    {
        if (lost is { StatusId: StatusId.Wyrmclaw or StatusId.Wyrmfang, Reason: StatusLossReason.Expired })
            lost.Holder.DieOfStatus(lost.StatusId, "not cleansed");
    }

    private void PositionUsurperForWaves(bool second)
        => usurper?.SetPosition(new Placement(state.WaveOrigin(second, 0), WaveRotation(second)));

    private float WaveRotation(bool second)
    {
        var direction = state.WaveDirection(second);
        return MathF.Atan2(direction.X, direction.Z);
    }

    // Retail turns a boss to where it goes in one packet, moves it there facing that way, then turns
    // it on the spot.
    private static void TurnToward(SimEnemy? boss, Vector3 point)
    {
        if (boss is null) return;
        var travel = point - boss.Position;
        boss.SetRotation(MathF.Atan2(travel.X, travel.Z));
    }

    private static void MoveFacingTravel(SimEnemy? boss, Vector3 to)
    {
        if (boss is null) return;
        var travel = to - boss.Position;
        boss.SetPosition(new Placement(to, MathF.Atan2(travel.X, travel.Z)));
    }

    // Four strips 10y deep, 2s apart.
    private void TidalLight(bool second)
    {
        usurper?.Cast(ActionId.TidalLightCast, animationLock: 2.1f);
        var direction = state.WaveDirection(second);
        for (var step = 0; step < 4; step++)
        {
            var strip = new Placement(state.WaveOrigin(second, step), MathF.Atan2(direction.X, direction.Z));
            if (step == 0) usurper?.CastFromHelper(Actions.TidalLightFirst, at: strip);
            else world.Events.Add(1.1f + 2f * step, () => usurper?.CastFromHelper(Actions.TidalLightRest, at: strip));
        }
    }

    private void Quietus()
    {
        foreach (var role in state.Quietus)
            if (world.Party.Get(role) is { } carrier)
                oracle?.CastFromHelper(Actions.Quietus, at: carrier.Placement());
    }

    private void SaveReturns()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
        {
            if (member is not ISimPartyMember slot) continue;
            returns[slot.Role] = member.Position;
            member.AddStatus(StatusId.Return, StunAt - ReturnSavedAt);
            world.PlaceGroundVfx(VfxId.ReturnMarker, member.Position);
        }
    }

    private void SpiritTaker()
    {
        if (oracle is not { } boss || world.Party.Get(state.SpiritTakerTarget) is not { } target || !target.IsAlive()) return;
        TurnToward(boss, target.Position);
        boss.Cast(Actions.SpiritTaker, target);
        var landing = target.Position;
        world.Events.Add(0.7f, () => boss.SetPosition(landing));
    }

    private void Rewind()
    {
        foreach (var (role, position) in returns)
            if (world.Party.Get(role) is { } member && member.IsAlive())
                member.CarryTo(position, TimelineId.Rewind);
    }

    private void AkhMorn()
    {
        if (world.Party.Get(PartyRole.MainTank) is { } mainTank && mainTank.IsAlive())
            usurper?.CastFromHelper(Actions.AkhMornUsurperHit, mainTank, at: mainTank.Placement());
        if (world.Party.Get(PartyRole.OffTank) is { } offTank && offTank.IsAlive())
            oracle?.CastFromHelper(Actions.AkhMornOracleHit, offTank, at: offTank.Placement());
    }

    private void SpawnDragons()
    {
        for (var side = 0; side < 2; side++)
        {
            var west = side == 0;
            dragons[side] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.DrachenWanderer,
                NameId: BNpcNameId.DrachenWanderer,
                Level: Level,
                Placement: new Placement(new Vector3(west ? -2 : 2, 0, -13), west ? -MathF.PI / 2 : MathF.PI / 2),
                LayoutId: LayoutId.DrachenWanderer,
                ModelScale: DragonSpawnScale));
        }
    }

    public void OnActionResolved(ActionResolved resolved) => BreakFragmentOnAvoidableHit(resolved);
    public void OnStatusLost(StatusLost lost) => KillUncleansed(lost);

    public void Tick(float delta, float elapsed)
    {
        if (elapsed >= DragonsMoveAt)
            for (var side = 0; side < 2; side++) TickDragon(side, elapsed);
        TickPuddles(elapsed);
        ai?.Tick(state, world, delta);
    }

    // A head flies its half circle, catching the first player it touches without stopping, and
    // vanishes after its second catch. Whoever it just caught has to step out of it before it can
    // catch again. A head that reaches the south still hungry catches no one more.
    private void TickDragon(int side, float elapsed)
    {
        if (dragons[side] is not { } head || dragonCatches[side] >= 2 || elapsed >= DragonsReachSouthAt) return;
        var position = head.Position;
        head.MoveTo(DragonPosition(side, elapsed + 0.5f), DragonSpeed);
        var contact = dragonCatches[side] == 0 ? DragonInitialContactRadius : DragonContactRadius;
        if (lastCatcher[side] is { } previous
            && (world.Party.Get(previous) is not { } catcher || !catcher.IsAlive() || Vector3.Distance(catcher.Position, position) > DragonInitialContactRadius))
            lastCatcher[side] = null;
        if (elapsed < dragonReadyAt[side]) return;
        var target = world.Party.ActiveMembers()
            .Where(m => m is ISimPartyMember slot && slot.Role != lastCatcher[side])
            .Where(m => Vector3.Distance(m.Position, position) <= contact)
            .MinBy(m => Vector3.DistanceSquared(m.Position, position));
        if (target is not ISimPartyMember caught) return;
        lastCatcher[side] = caught.Role;
        dragonReadyAt[side] = elapsed + DragonHitCooldown;
        head.CastFromHelper(Actions.LongingOfTheLost, target, at: new Placement(position, head.Rotation));
        var puddleIndex = PuddleArg2Base + (uint)(dragonCatches[side] * 2 + (side == 0 ? 1 : 0));
        world.Events.Add(PuddleDelay, () => DropPuddle(caught.Role, position, elapsed + PuddleDelay, puddleIndex));
        if (dragonCatches[side] == 0) head.Activate(DragonShrunkState);
        if (++dragonCatches[side] < 2) return;
        head.Cast(ActionId.DrachenWandererDisappear, animationLock: 4.1f);
        world.Events.Add(4.2f, head.Despawn);
    }

    private bool IsHungry(int side) => dragons[side] is not null && dragonCatches[side] < 2;

    // UNVERIFIED: where a hungry head goes after the south; retail shows it only as it vanishes, back
    // in the north.
    private void VanishHungryHeads()
    {
        for (var side = 0; side < 2; side++)
            if (IsHungry(side) && dragons[side] is { } head)
            {
                head.Cast(ActionId.DrachenWandererDisappear, animationLock: 4.1f);
                world.Events.Add(4.2f, head.Despawn);
            }
    }

    private void PunishHungryHeads()
    {
        var hungry = IsHungry(0) ? dragons[0] : IsHungry(1) ? dragons[1] : null;
        hungry?.CastFromHelper(Actions.JoylessDragonsong, at: new Placement(Vector3.Zero, 0f));
    }

    private void DropPuddle(PartyRole catcher, Vector3 position, float elapsed, uint arg2Index)
    {
        var visual = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.DragonPuddle,
            Placement = new Placement(position, 0f),
            EventId = DirectorEventId,
            TargetableStatus = 5,
            Arg2 = arg2Index << 16 | 3,
        });
        puddles[catcher] = (elapsed, visual);
        state.AddDragonPuddle(catcher, position);
    }

    // A puddle cleanses Wyrmfang off the first player to step in it, but never its own catcher's;
    // stepped in without Wyrmfang, it's simply spent.
    private void TickPuddles(float elapsed)
    {
        foreach (var (catcher, puddle) in puddles.ToList())
        {
            if (elapsed - puddle.DroppedAt >= PuddleLifetime)
            {
                RemovePuddle(catcher, puddle.Visual);
                continue;
            }
            if (elapsed - puddle.DroppedAt < PuddleGrace) continue;
            var position = state.DragonPuddles[catcher];
            var stepper = world.Party.ActiveMembers()
                .FirstOrDefault(m => m is ISimPartyMember slot && slot.Role != catcher && Vector3.Distance(m.Position, position) <= PuddleRadius);
            if (stepper is null) continue;
            stepper.RemoveStatus(StatusId.Wyrmfang);
            puddle.Visual?.DirectorEObjMod(7);
            world.Events.Add(PuddleFadeOut, () => puddle.Visual?.Despawn());
            puddles.Remove(catcher);
            state.RemoveDragonPuddle(catcher);
        }
    }

    private void RemovePuddle(PartyRole catcher, SimEventObject? visual)
    {
        visual?.Despawn();
        puddles.Remove(catcher);
        state.RemoveDragonPuddle(catcher);
    }

    private sealed record Replay(FruP4CrystallizeTimeState State, IScenarioAi<FruP4CrystallizeTimeState> Ai, SimWorld World);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new FruP4CrystallizeTimeAiReplayStateMessage(Enum.GetValues<PartyRole>().Select(s.Debuff).ToArray(), s.SlowNorthwest, s.Corner)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP4CrystallizeTimeAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP4CrystallizeTimeState.FromNetworkReplay(msg.Debuffs, msg.SlowNorthwest, msg.Corner);
        if (shadowState == null) return null;
        var replayAi = (IScenarioAi<FruP4CrystallizeTimeState>)AiStrats[aiIndex];
        replayAi.Run(shadowState, replayWorld);
        return new Replay(shadowState, replayAi, replayWorld);
    }

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (LastState is not { } s) return null;
        var current = s.DragonPuddles.OrderBy(p => p.Key).Select(p => new DragonPuddleState(p.Key, p.Value.X, p.Value.Y, p.Value.Z)).ToList();
        if (current.SequenceEqual(sentPuddles)) return null;
        sentPuddles = current;
        return new FruP4CrystallizeTimeDragonPuddlesUpdateMessage(current);
    }

    public void ApplyMidRunUpdate(object shadowState, MpMessage message)
    {
        if (shadowState is not Replay { State: var shadow } || message is not FruP4CrystallizeTimeDragonPuddlesUpdateMessage update) return;
        var dropped = new Dictionary<PartyRole, Vector3>();
        foreach (var puddle in NetGuard.Cap(update.Puddles, 8))
            if (Enum.IsDefined(puddle.Catcher) && NetGuard.TryPosition(puddle.X, puddle.Y, puddle.Z, out var position))
                dropped[puddle.Catcher] = position;
        foreach (var catcher in shadow.DragonPuddles.Keys.Where(c => !dropped.ContainsKey(c)).ToList()) shadow.RemoveDragonPuddle(catcher);
        foreach (var (catcher, position) in dropped) shadow.AddDragonPuddle(catcher, position);
    }

    // The strat reacts every frame to what it sees, which the scenario's own Tick drives on the host.
    public void TickReplay(object shadowState, float deltaSeconds)
    {
        if (shadowState is Replay replay) replay.Ai.Tick(replay.State, replay.World, deltaSeconds);
    }
}
