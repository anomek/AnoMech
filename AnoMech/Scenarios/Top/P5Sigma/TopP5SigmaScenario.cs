using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Core.EnemyActions;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using AnoMech.Core.Native.Interfaces;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaScenario : IMultiplayerReplayable
{
    public string Name => "Sigma";
    public IPhase Phase => TopZone.P5;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 98.28f;

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP5SigmaSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP5SigmaAi()];

    // Rear Lasers are cast from this far behind the spinner: the 50 y line runs through it, 25 y each way.
    private const float RearLasersBehind = 25f;

    private TopP5SigmaState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? omegaM;
    private TopHelpers? grayishMHelpers;
    private TopHelpers? grayishFHelpers;
    private TopHelpers? waveCannonHelpers;
    private TopHelpers? towerHelpers;
    private TopHelpers? omegaFHelpers;

    private const string Tag = "TopP5Sigma";

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (6757, "mon_sp/gimmick/z3of_boss_gimmick16"),
        (6758, "mon_sp/gimmick/z3of_boss_gimmick17"),
        (10725, "mon_sp/gimmick/z3oz_boss_gimmick13"),
        (6545, "mon_sp/gimmick/z3oe_boss_gimmick03"),
        (10726, "mon_sp/gimmick/z3oz_boss_gimmick14"),
        (10727, "mon_sp/gimmick/z3oz_boss_gimmick15"),
        (10728, "mon_sp/gimmick/z3oz_boss_gimmick16"),
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),
    ];

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP5SigmaState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5SigmaState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP5SigmaState>)AiStrats[idx]).Run(state, world);
        omegaM = null;
        grayishMHelpers = grayishFHelpers = waveCannonHelpers = towerHelpers = omegaFHelpers = null;

        world.Events.Add(1f, SpawnHelpers);
        Run_Omega_M_4000A63C();
        Run_Omega_4000A68F();
        Run_Omega_4000A690();
        Run_Right_Arm_Unit_4000A643();
        Run_Omega_M_4000A40C_0();
        Run_Omega_4000A408();
        Run_EventObj_1EB83C_4000A6E7();
        Run_EventObj_1EB83E_4000A6E8();
        Run_Rear_Power_Unit_4000A641();
        Run_Omega_F_4000A40B_2();
        Run_Omega_F_4000A40C_2();
        Run_PlayerTethers();
        Run_OtherDebuffs();
        Run_PlayerLockons();
    }

    public void Tick(float delta, float elapsed)
    {
        HelloWorld.CheckHolderDeaths(world, omegaFHelpers);
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(GimmickTimelines, Tag);

    // Real-packet helpers, named for the mechanic each casts, spawned well ahead of their first hit.
    private void SpawnHelpers()
    {
        grayishMHelpers = new TopHelpers(world, BNpcNameId.OmegaMDynamis, 1, Tag);
        grayishFHelpers = new TopHelpers(world, BNpcNameId.OmegaM_1DD3, 1, Tag);
        waveCannonHelpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 6, Tag);
        towerHelpers = new TopHelpers(world, BNpcNameId.OmegaBeetle, 6, Tag);
        omegaFHelpers = new TopHelpers(world, BNpcNameId.OmegaFDynamis, 6, Tag);
    }

    // It looks like there is a second of leeway here, but unclear if it accounts for snapshotting to be honest
    private void Run_PlayerTethers()
    {
        world.Events.Add(11.82f, () =>
        {
            state.Order.ForEachPair((p1, p2) => world.Tether(
                p1, p2,
                TetherId.Glitch , duration: 32.000f,
                debuffStatusId: state.GlitchType.StatusId)
                     .SetConditionalStatus(StatusId.VulnerabilityUp, state.GlitchType.Condition)
            );
        });
    }

    private void Run_OtherDebuffs()
    {
        state.DynamisTargets.ForEach(p => p.AddStatus(StatusId.QuickeningDynamis, stacks: 1));
        world.Events.Add(11.82f, () => state.HelloWorldTargets.Get(0)?.AddStatus(StatusId.HelloNearWorld, 56.000f));
        world.Events.Add(11.82f, () => state.HelloWorldTargets.Get(1)?.AddStatus(StatusId.HelloDistantWorld, 56.000f));
        world.Events.Add(28.39f, () => party.ForEachActive(member => member.AddStatus(StatusId.Looper, 18.000f)));
    }

    private void Run_PlayerLockons()
    {
        world.Events.Add(11.82f, () => state.Order.ForEachPair((i, p1, p2) =>
        {
            p1.AttachLockonVfx(LockonId.Playstation[i], persistent: false);
            p2.AttachLockonVfx(LockonId.Playstation[i], persistent: false);
        }));
        world.Events.Add(22.02f, () => state.WaveCannonTargets.ForEach(p => p.AttachLockonVfx(LockonId.WaveCannon, persistent: false)));
    }


    private void Run_Omega_M_4000A63C()
    {
        world.Events.Add(0f, () => omegaM = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaMDynamis, NameId: BNpcNameId.OmegaMDynamis, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(0.000f, 0.000f, 5.000f), MathF.PI), InitialModeAttributeFlags: 0x32)));
        // The real status param is 0; left at 1, since a zero-stack status never reaches peers.
        world.Events.Add(0.1f, () => omegaM?.AddStatus(StatusId.OmegaM));
        world.Events.Add(2.46f, () => omegaM?.Cast(ActionId.Teleport7b42, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(3.22f, () => omegaM?.SetPosition(Vector3.Zero));
        world.Events.Add(3.66f, () => world.Map.BattleTalk(BNpcNameId.OmegaMDynamis, BattleTalkId.AmplificationInconsistent, 6000));
        world.Events.Add(3.75f, () => omegaM?.Cast(Actions.RunMiSigmaVersion));
        world.Events.Add(11.82f, () => omegaM?.SetTargetable(false));
        world.Events.Add(11.87f, () => omegaM?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(13.37f, () => omegaM?.SetVisible(false));
        world.Events.Add(13.92f, () => omegaM?.SetPosition(state.NewNorthA.Apply(new Placement(new(0f, 0f, -20f), 0f))));
        world.Events.Add(13.96f, () => omegaM?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(13.96f, () => omegaM?.SetVisible(true));
        world.Events.Add(26.16f, () => omegaM?.Cast(ActionId.SubjectSimulationFDynamis, castSeconds: 0f, targetId: omegaM.GameObjectId, animationLock: 2.1f));
        world.Events.Add(27.23f, () => omegaM?.SetModelState(0x06));
        world.Events.Add(27.23f, () => omegaM?.RemoveStatus(StatusId.OmegaM));
        world.Events.Add(27.23f, () => omegaM?.AddStatus(StatusId.Superfluid, stacks: 493, overrideStacks: true));
        world.Events.Add(28.25f, () => omegaM?.Cast(ActionId.SubjectSimulationFWarpDown, castSeconds: 0f, targetId: omegaM.GameObjectId, animationLock: 4.1f));
        world.Events.Add(28.79f, () => omegaM?.SetModelState(0x0B));
        world.Events.Add(32.36f, () => omegaM?.Cast(ActionId.Unknown7f30, castSeconds: 0f, targetId: omegaM.GameObjectId, animationLock: 4.1f));
        world.Events.Add(36.02f, () => omegaM?.SetModelState(0x05));
        world.Events.Add(36.02f, () => omegaM?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(36.02f, () => omegaM?.AddStatus(StatusId.OmegaF, stacks: 492, overrideStacks: true));
        world.Events.Add(36.47f, () => omegaM?.Cast(ActionId.Unknown7b20, castSeconds: 0f, targetId: omegaM.GameObjectId, animationLock: 2.1f));
        world.Events.Add(37.13f, () => omegaM?.SetModelState(0x0B));
        world.Events.Add(38.56f, () => omegaM?.Cast(ActionId.Teleport7b43, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(39.30f, () => omegaM?.SetPosition(Vector3.Zero));
        world.Events.Add(39.68f, () => omegaM?.Cast(Actions.Discharger));
        world.Events.Add(42.79f, () => omegaM?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(44.33f, () => omegaM?.SetVisible(false));
        world.Events.Add(45.88f, () => omegaM?.SetModeAttributeFlags(state.OmegaFAttack.AttributeFlags));
        world.Events.Add(45.88f, () => omegaM?.SetModelState(state.OmegaFAttack == OmegaAttack.Legs ? (byte)0x04 : (byte)0x00));
        world.Events.Add(45.88f, () => omegaM?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0f, 0f, -10f), 0))));
        world.Events.Add(45.97f, () => omegaM?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(45.97f, () => omegaM?.SetVisible(true));
        world.Events.Add(59.59f, () => omegaM?.Cast(state.OmegaFAttack.Action));
        world.Events.Add(64.21f, () => omegaM?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(65.77f, () => omegaM?.SetVisible(false));
        world.Events.Add(66.26f, () => omegaM?.SetModeAttributeFlags(0x32));
        world.Events.Add(66.26f, () => omegaM?.SetModelState(0x00));
        world.Events.Add(69.29f, () => omegaM?.SetPosition(new Placement(Vector3.Zero, 3.142f)));
        world.Events.Add(69.38f, () => omegaM?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(69.38f, () => omegaM?.SetVisible(true));
        world.Events.Add(72.46f, () => omegaM?.SetTargetable(true));
    }


    private void Run_Omega_4000A68F()
    {
        SimEnemy? omega_4000A68F = null;
        world.Events.Add(3.94f, () => omega_4000A68F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BeetleHelper, NameId: BNpcNameId.OmegaBeetle, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, 20f), MathF.PI)))));
        world.Events.Add(19.93f, () => omega_4000A68F?.PlayAnimationTimeline(TimelineId.Spawn));
        world.Events.Add(20.04f, () => omega_4000A68F?.SetVisible(true));
        world.Events.Add(27.63f, () => omega_4000A68F?.Cast(ActionId.SigmaProgramLoop, castSeconds: 0f, targetId: omega_4000A68F?.GameObjectId, animationLock: 3.1f));
        world.Events.Add(30.75f, () => omega_4000A68F?.PlayAnimationTimeline(TimelineId.WarpOut));
        world.Events.Add(45.27f, () => omega_4000A68F?.Despawn());
    }

    private void Run_Omega_4000A690()
    {
        SimEnemy? omega_4000A690 = null;
        world.Events.Add(3.94f, () => omega_4000A690 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.FinalHelper, NameId: BNpcNameId.OmegaFinal, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: state.NewNorthA.Apply(new Placement(new Vector3(0.000f, -0.000f, 0.000f), 0)))));
        world.Events.Add(16.95f, () => omega_4000A690?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(17.06f, () => omega_4000A690?.SetVisible(true));
        world.Events.Add(22.11f, () => omega_4000A690?.Cast(ActionId.WaveCannon, castSeconds: 7.700f, targetId: omega_4000A690?.GameObjectId, animationLock: 3.1f, fireDelay: 0.27f));
        world.Events.Add(33.25f, () => omega_4000A690?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(35.50f, () => omega_4000A690?.Despawn());
    }

    private void Run_Right_Arm_Unit_4000A643()
    {
        for(int i = 0; i < 2; i++)
        {
            var offset = i * 2 - 1; // -1, 1
            SimEnemy? unit = null;
            SimTether? tether = null;
            SimCharacter? pulseTarget = null;
            world.Events.Add(1, () => unit = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.RightArmUnit, NameId: BNpcNameId.RightArmUnit, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false,
                                                                  Placement: state.NewNorthA.Apply(new Placement(new Vector3(7.07f * offset, 0f, 7.07f), 0f).Face(Vector3.Zero)))));
            world.Events.Add(11.91f, () => unit?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(11.91f, () => unit?.SetVisible(true));
            world.Events.Add(12.53f, () => tether = world.TetherFarestPlayer(unit, TetherId.AutoTarget)
                                                         .SetAutoFaceTarget(true));
            world.Events.Add(30.84f, () => pulseTarget = ReleaseHyperPulseTether(tether));
            world.Events.Add(30.93f, () => FireHyperPulse(unit, pulseTarget));
            world.Events.Add(32.98f, () => unit?.PlayActionTimeline(TimelineId.WarpOut));
            world.Events.Add(45.88f, () => unit?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(14.14f * offset, 0f, 14.14f), 0f).Face(Vector3.Zero))));
            world.Events.Add(45.97f, () => unit?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(46.59f, () => tether = world.TetherFarestPlayer(unit, TetherId.AutoTarget)
                  .SetAutoFaceTarget(true));
            world.Events.Add(67.84f, () => pulseTarget = ReleaseHyperPulseTether(tether));
            world.Events.Add(68.00f, () => FireHyperPulse(unit, pulseTarget));
            world.Events.Add(70.05f, () => unit?.PlayActionTimeline(TimelineId.WarpOut));
        }
    }

    // The tether drops just before the shot; the shot still goes to the player it held.
    private static SimCharacter? ReleaseHyperPulseTether(SimTether? tether)
    {
        tether?.Despawn();
        return tether?.B;
    }

    private static void FireHyperPulse(SimEnemy? unit, SimCharacter? target)
    {
        if (unit is not { IsActive: true } || target == null) return;
        unit.Cast(Actions.HyperPulseSigma, target);
    }

    private void Run_Omega_M_4000A40C_0()
    {
        world.Events.Add(27.04f, () => PlaceOnOmegaM(grayishMHelpers));
        world.Events.Add(27.13f, () => CastGrayish(grayishMHelpers, ActionId.SuperfluidAnimationM));
        world.Events.Add(35.84f, () => PlaceOnOmegaM(grayishFHelpers));
        world.Events.Add(35.93f, () => CastGrayish(grayishFHelpers, ActionId.SuperfluidAnimationF));
    }

    private void PlaceOnOmegaM(TopHelpers? helpers)
    {
        if (omegaM != null) helpers?.At(0, omegaM.Placement());
    }

    private static void CastGrayish(TopHelpers? helpers, uint actionId)
    {
        if (helpers?.At(0) is { } helper)
            helper.Cast(actionId, castSeconds: 0f, targetId: helper.GameObjectId, animationLock: 2.1f);
    }

    private void Run_Omega_4000A408()
    {
        HelloWorld[] solvers = [
            new HelloWorld(party, state.HelloWorldTargets[0], true),
            new HelloWorld(party, state.HelloWorldTargets[1], false)];
        for (int index = 0; index < 6; index++)
        {
            SimEnemy? helloWorldHelper = null;
            var i = index;
            var target = state.WaveCannonTargets.Get(i);
            var tower = state.Towers[i];
            var helloWorldOffset = i / 2;
            var solverId = i % 2;

            world.Events.Add(31.15f, () => FireWaveCannon(waveCannonHelpers?.At(i, new Placement(Vector3.Zero, 0f)), target));
            if (tower != null)
            {
                world.Events.Add(43.60f, () => towerHelpers?.At(i, new Placement(tower.Position, 0f)));
                world.Events.Add(43.69f, () => ResolveTower(towerHelpers?.At(i), tower));
            }

            world.Events.Add(67.7f + helloWorldOffset, () => solvers[solverId].SetPosition(helloWorldHelper = omegaFHelpers?.At(i)));
            world.Events.Add(67.9f + helloWorldOffset, () => solvers[solverId].CastSpell(helloWorldHelper));
        }
    }

    private static void FireWaveCannon(SimEnemy? helper, SimCharacter? target)
    {
        if (helper is not { IsActive: true } || target == null) return;
        helper.Cast(Actions.WaveCannon, target);
    }

    // One hit per tower, and Obliteration on everyone when it is short.
    private void ResolveTower(SimEnemy? helper, Tower tower)
    {
        var inTower = party.Find.InsideCircle(tower.Position, Geometry.TowerRadius);
        if (inTower.Count > 0)
            helper?.Cast(tower.MinPlayers == 1 ? Actions.StorageViolationSolo : Actions.StorageViolationPair);
        if (inTower.Count < tower.MinPlayers)
            helper?.Cast(Actions.StorageViolationObliteration);
    }

    private void Run_EventObj_1EB83C_4000A6E7()
    {
        for (int index = 0; index < 6; index++)
        {
            var i = index;
            var tower = state.Towers[i];
            if (tower == null) continue;
            SimEventObject? eventObj_1EB83C_4000A6E7 = null;
            world.Events.Add(33.96f, () => eventObj_1EB83C_4000A6E7 = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.TowerTimer, Placement = new Placement(tower.Position, -0.000f), TimelineState = 2 }));
            world.Events.Add(43.93f, () => eventObj_1EB83C_4000A6E7?.Despawn());
        }
    }

    private void Run_EventObj_1EB83E_4000A6E8()
    {
        for(int index = 0; index < 6; index++)
        {
            var i = index;
            var tower = state.Towers[i];
            if (tower == null) continue;
            var eObjId = tower.MinPlayers == 1 ? EObjId.TowerSolo : EObjId.TowerPair;
            ushort[] stateIds = tower.MinPlayers == 1 ? [8, 16] : [8, 16, 32];
            SimEventObject? eventObj_1EB83E_4000A6E8 = null;
            world.Events.Add(33.96f, () => eventObj_1EB83E_4000A6E8 = world.SpawnTower(new EventObjectSpawnConfig { EObjId = eObjId, Placement = new Placement(tower.Position, -0.000f) }, stateIds, Geometry.TowerRadius));
            world.Events.Add(43.93f, () => eventObj_1EB83E_4000A6E8?.Despawn());
        }
    }

    private void Run_Rear_Power_Unit_4000A641()
    {
        SimEnemy? rear_Power_Unit_4000A641 = null;
        world.Events.Add(1f, () => rear_Power_Unit_4000A641 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.RearPowerUnit, NameId: BNpcNameId.RearPowerUnit, Level: 90, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: state.NewNorthB.Apply(new Placement(new Vector3(0.000f, -0.000f, 0.000f), 0f)))));
        world.Events.Add(45.97f, () => rear_Power_Unit_4000A641?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(46.00f, () => rear_Power_Unit_4000A641?.SetVisible(true));
        world.Events.Add(47.88f, () => rear_Power_Unit_4000A641?.AttachLockonVfx(state.SpinnerRotation.LockonId, persistent: false));
        world.Events.Add(54.99f, () => FireRearLasers(rear_Power_Unit_4000A641, Actions.RearLasersCharging));
        for (int i = 0; i < 13; i++)
        {
            var rotation = state.SpinnerRotation.Mul * (i + 1) * MathF.PI / 20;
            world.Events.Add(58.54f + i * 0.58f, () => rear_Power_Unit_4000A641?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0.000f, 0.000f, 0.000f), rotation))));
            world.Events.Add(58.60f + i * 0.58f, () => FireRearLasers(rear_Power_Unit_4000A641, Actions.RearLasersShoot));
        }
        world.Events.Add(66.13f, () => rear_Power_Unit_4000A641?.PlayActionTimeline(TimelineId.WarpOut));
    }

    private static void FireRearLasers(SimEnemy? spinner, EnemyAction lasers)
    {
        if (spinner is not { IsActive: true }) return;
        spinner.Cast(lasers, spinner.Placement().MoveForward(-RearLasersBehind).Position);
    }

    private void Run_Omega_F_4000A40B_2()
    {
        if (state.OmegaFAttack == OmegaAttack.Legs)
        {
            world.Events.Add(59.54f, () => omegaFHelpers?.At(0, state.NewNorthB.Apply(Geometry.LegsSideHelperPlacement)));
            world.Events.Add(59.59f, () => CastLegsSide(0, Actions.SuperliminalSteelR, Geometry.LegsSideTargetR));
        }
    }

    private void Run_Omega_F_4000A40C_2()
    {
        if (state.OmegaFAttack == OmegaAttack.Legs)
        {
            world.Events.Add(59.54f, () => omegaFHelpers?.At(1, state.NewNorthB.Apply(Geometry.LegsSideHelperPlacement)));
            world.Events.Add(59.59f, () => CastLegsSide(1, Actions.SuperliminalSteelL, Geometry.LegsSideTargetL));
        }
    }

    private void CastLegsSide(int index, EnemyAction steel, Vector3 target)
        => omegaFHelpers?.At(index)?.Cast(steel, state.NewNorthB.Apply(target));

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP5SigmaAiReplayStateMessage(
            s.Order.List, s.DynamisTargets.List, s.HelloWorldTargets.List, s.HandBait.List, s.HelloWorldJumpOrder.List,
            s.NewNorthA.RadiansFromNorth, s.NewNorthB.RadiansFromNorth, s.TowerNorthFlipped,
            s.GlitchType == GlitchType.Far, s.SpinnerRotation == Rotation.Clockwise, s.OmegaFAttack == OmegaAttack.Staff,
            s.FirstMissing, s.SecondMissing)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP5SigmaAiReplayStateMessage msg) return null;
        var shadowState = TopP5SigmaState.FromNetworkReplay(
            replayWorld.Party, msg.Order, msg.DynamisTargets, msg.HelloWorldTargets, msg.HandBait, msg.HelloWorldJumpOrder,
            msg.NewNorthARadians, msg.NewNorthBRadians, msg.TowerNorthFlipped,
            msg.GlitchIsFar, msg.SpinnerIsClockwise, msg.OmegaFIsStaff, msg.FirstMissing, msg.SecondMissing);
        ((IScenarioAi<TopP5SigmaState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
