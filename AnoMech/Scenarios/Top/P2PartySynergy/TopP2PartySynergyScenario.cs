using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergyScenario : IScenario
{
    public string Name => "TOP P2 Party Synergy";
    public TargetInstance TargetInstance { get; } = new(
        TerritoryId: 1122,
        Origin: new Vector3(100.000f, 0f, 100.000f),
        PlayerPosition: new Vector3(100.000f, 0f, 116.000f),
        WeatherId: 78);
    public IReadOnlyList<Waymark> Waymarks { get; } = TopUtils.TopWaymarks;
    public ushort Bgm => BgmId.TopP2;
    public bool SupportsSolo => true;
    public byte Level => 90;
    public ushort ItemLevel => 365;

    public void DrawSettings() => settingsWindow.Draw();
    private readonly TopP2PartySynergySettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP2PartySynergyAi()];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP2PartySynergyState state = null!;
    private TopUtils topUtils = null!;
    private DamageSolver damage = null!;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP2PartySynergyState(world.Party, settingsWindow.Overrides);
        topUtils = new TopUtils(world);
        damage = new DamageSolver(world.Party);
        damage.SetStatuses(DamageType.Any, StatusId.VulnerabilityUp);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP2PartySynergyState>)AiStrats[idx]).Run(state, world);

        world.EnforceArenaBoundary(Geometry.ArenaRadius);
        world.Events.Add(1f, () => topUtils.InitTopArena());
        
        Run_Omega_4000A4E9();
        Run_Omega_4000A4E8();
        Run_Omega_F_4000A4FD();
        Run_Omega_M_4000A4FE();
        Run_Omega_M_4000A4FF();
        Run_Optical_Unit_4000A3E7();
        Run_Omega_4000A40B_0();
        Run_Omega_4000A40C_0();
        Run_Omega_4000A40A_0();
        Run_Omega_4000A409_0();
        Run_Omega_4000A405();
        Run_Omega_M_4000A40B_3();
        Run_InstanceEvents();
        Run_PlayerTethers(solo);
        Run_PlayerLockons(solo);
    }

    private void Run_InstanceEvents()
    {
        var index = (byte)(state.NewNorthA.Index() + 1);
        world.Events.Add(7.93f, () => world.Map.AddEffect(packetFlags: 0x00020002U, index: index));
        world.Events.Add(17.95f, () => world.Map.AddEffect(packetFlags: 0x00800040U, index: index));
        world.Events.Add(20.67f, () => world.Map.AddEffect(packetFlags: 0x10000001U, index: index));
        world.Events.Add(26.82f, () => world.Map.AddEffect(packetFlags: 0x00080008U, index: index));
    }

    private void Run_PlayerTethers(bool solo)
    {
        if (solo)
        {
            world.Events.Add(7.93f, () => state.Order.ForEach(p => p.AddStatus(state.Glitch.StatusId, duration: 27f)));
            return;
        }
        world.Events.Add(7.93f, () =>
        {
            state.Order.ForEachPair((p1, p2) => world.Tether(
                                                         p1, p2,
                                                         TetherId.Glitch , duration: 27.000f,
                                                         debuffStatusId: state.Glitch.StatusId)
                                                     .SetConditionalStatus(StatusId.VulnerabilityUp, state.Glitch.Condition));
        });
    }

    private void Run_PlayerLockons(bool solo)
    {
        if(solo)
        {
            world.Events.Add(7.93f, () => state.Order.ForEach(p => p.AttachLockonVfx(LockonId.Playstation[new Random().Next(4)], persistent: false)));
            return;
        }
        world.Events.Add(7.93f, () => state.Order.ForEach((i, p) =>
        {
            p.AttachLockonVfx(LockonId.Playstation[i/2], persistent: false); 
        }));
        world.Events.Add(22.63f, () => state.Stacks.ForEach(p => p.AttachLockonVfx(LockonId.Stack, persistent: false)));
    }

    public void Tick(float delta, float elapsed) { }

    private void Run_Omega_4000A4E9()
    {
        SimEnemy? omega_4000A4E9 = null;
        world.Events.Add(0f, () => omega_4000A4E9 = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x32, BNpcBaseId: BNpcBaseId.OmegaM, NameId: BNpcNameId.OmegaM_1DD3, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true , Placement: new Placement(new Vector3(-3.420f, -0.000f, -1.030f), -0.000f))));
        world.Events.Add(1f, () => omega_4000A4E9?.AddStatus(StatusId.OmegaM, stacks: (ushort)490, overrideStacks: true));
        world.Events.Add(1.88f, () => omega_4000A4E9?.Cast(ActionId.PartySynergyM, castSeconds: 2.700f));
        world.Events.Add(7.93f, () => omega_4000A4E9?.SetTargetable(false));
        world.Events.Add(8.02f, () => omega_4000A4E9?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(9.55f, () => omega_4000A4E9?.SetVisible(false));
        world.Events.Add(10.07f, () => omega_4000A4E9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -0.000f)));
        world.Events.Add(10.16f, () => omega_4000A4E9?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(10.22f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(14.48f, () => omega_4000A4E9?.RemoveStatus(StatusId.OmegaM));
        world.Events.Add(14.48f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationF));
        world.Events.Add(15.54f, () => omega_4000A4E9?.SetModelState((byte)0x06));
        world.Events.Add(15.54f, () => omega_4000A4E9?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationFWarpDown));
        // world.Events.Add(16.62f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(17.15f, () => omega_4000A4E9?.SetModelState((byte)0x0B));
        world.Events.Add(20.72f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationFWarpUp, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId));
        world.Events.Add(24.41f, () => omega_4000A4E9?.SetModelState((byte)0x05));
        world.Events.Add(24.41f, () => omega_4000A4E9?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(24.41f, () => omega_4000A4E9?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true));
        world.Events.Add(24.82f, () => omega_4000A4E9?.Cast(ActionId.Unknown7b20, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId));
        world.Events.Add(25.46f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(25.44f, () => omega_4000A4E9?.SetModelState((byte)0x0B));
        world.Events.Add(28.42f, () => omega_4000A4E9?.Cast(ActionId.Discharger, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        world.Events.Add(28.92f, () => { if (omega_4000A4E9 != null) world.Party.Knockback(omega_4000A4E9.Position, KnockbackId.Discharger); });
        world.Events.Add(36.50f, () => omega_4000A4E9?.SetTargetable(true));
    }

    private void Run_Omega_4000A4E8()
    {
        SimEnemy? omega_4000A4E8 = null;
        world.Events.Add(0f, () => omega_4000A4E8 = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaF, NameId: BNpcNameId.OmegaF, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(3.210f, -0.000f, -1.470f), -0.000f))));
        world.Events.Add(1f, () => omega_4000A4E8?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true));
        world.Events.Add(1.92f, () => omega_4000A4E8?.Cast(ActionId.PartySynergyF, targetLocation: new Vector3(2.983f, -0.015f, -0.008f), castSeconds: 2.700f, targetId: omega_4000A4E8?.GameObjectId));
        world.Events.Add(7.98f, () => omega_4000A4E8?.SetTargetable(false));
        world.Events.Add(8.02f, () => omega_4000A4E8?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(9.55f, () => omega_4000A4E8?.SetVisible(false));
        world.Events.Add(10.07f, () => omega_4000A4E8?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0f, -0.000f, -13.000f), 0f))));
        world.Events.Add(10.16f, () => omega_4000A4E8?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(11.15f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(14.48f, () => omega_4000A4E8?.Cast(ActionId.SubjectSimulationM, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId));
        world.Events.Add(16.12f, () => omega_4000A4E8?.SetModelState((byte)0x05));
        world.Events.Add(16.12f, () => omega_4000A4E8?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b17, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId));
        // world.Events.Add(17.21f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(17.24f, () => omega_4000A4E8?.SetModelState((byte)0x0B));
        world.Events.Add(20.72f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b1d, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId));
        world.Events.Add(24.01f, () => omega_4000A4E8?.SetModelState((byte)0x06));
        world.Events.Add(24.01f, () => omega_4000A4E8?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(24.01f, () => omega_4000A4E8?.AddStatus(StatusId.OmegaM_D7E, stacks: (ushort)490, overrideStacks: true));
        // world.Events.Add(24.04f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(24.82f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b1f, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId));
        world.Events.Add(25.62f, () => omega_4000A4E8?.SetModelState((byte)0x0B));
        world.Events.Add(31.82f, () => omega_4000A4E8?.Cast(ActionId.EfficientBladework, castSeconds: 1.200f, targetId: omega_4000A4E8?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
        world.Events.Add(33.02f, () => topUtils.ResolveOmegaAttack(omega_4000A4E8 ,ActionId.EfficientBladework));
        world.Events.Add(36.50f, () => omega_4000A4E8?.SetTargetable(true));
    }

    private void Run_Omega_F_4000A4FD()
    {
        SimEnemy? omega_F_4000A4FD = null;
        world.Events.Add(2.23f, () => omega_F_4000A4FD = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: state.AttackF.AttributeFlags, BNpcBaseId: BNpcBaseId.OmegaFClone, NameId: BNpcNameId.OmegaF, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(10.07f, () => omega_F_4000A4FD?.SetPosition(state.AttackDir.Apply(new Placement(new Vector3(0f, 0f, -10f), 0))));
        world.Events.Add(10.16f, () => omega_F_4000A4FD?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(10.16f, () => omega_F_4000A4FD?.SetVisible(true));
        world.Events.Add(13.81f, () => omega_F_4000A4FD?.Cast(state.AttackF.ActionId, castSeconds: 1.200f, targetId: omega_F_4000A4FD?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
        world.Events.Add(15.01f, () => topUtils.ResolveOmegaAttack(omega_F_4000A4FD, state.AttackF.ActionId));
        world.Events.Add(18.44f, () => omega_F_4000A4FD?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(19.93f, () => omega_F_4000A4FD?.SetVisible(false));
        world.Events.Add(20.66f, () => omega_F_4000A4FD?.Despawn());
    }

    private void Run_Omega_M_4000A4FE()
    {
        SimEnemy? omega_M_4000A4FE = null;
        world.Events.Add(2.23f, () => omega_M_4000A4FE = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: state.AttackM.AttributeFlags, BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(10.07f, () => omega_M_4000A4FE?.SetPosition(state.AttackDir.Flip().Apply(new Placement(new Vector3(0, 0, -10f), 0f))));
        world.Events.Add(10.16f, () => omega_M_4000A4FE?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(10.16f, () => omega_M_4000A4FE?.SetVisible(true));
        world.Events.Add(13.81f, () => omega_M_4000A4FE?.Cast(state.AttackM.ActionId, castSeconds: 1.200f, targetId: omega_M_4000A4FE?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
        world.Events.Add(15.01f, () => topUtils.ResolveOmegaAttack(omega_M_4000A4FE, state.AttackM.ActionId));
        world.Events.Add(18.44f, () => omega_M_4000A4FE?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(19.93f, () => omega_M_4000A4FE?.SetVisible(false));
        world.Events.Add(20.66f, () => omega_M_4000A4FE?.Despawn());
    }

    private void Run_Omega_M_4000A4FF()
    {
        for (int i = 0; i < 4; i++) {
            SimEnemy? omega_M_4000A4FF = null;
            var position = state.NewNorthB.Rotate(1 + i * 2).Apply(new Placement(new Vector3(0, 0, -13), 0));
            world.Events.Add(2.23f, () => omega_M_4000A4FF = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x32, BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
            // world.Events.Add(3.24f, () => omega_M_4000A4FF?.SetVisible(false));
            world.Events.Add(10.07f, () => omega_M_4000A4FF?.SetPosition(position));
            world.Events.Add(16.17f, () => omega_M_4000A4FF?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(16.23f, () => omega_M_4000A4FF?.SetVisible(true));
            world.Events.Add(31.82f, () => omega_M_4000A4FF?.Cast(ActionId.EfficientBladework, castSeconds: 1.200f, targetId: omega_M_4000A4FF?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
            world.Events.Add(33.02f, () => topUtils.ResolveOmegaAttack(omega_M_4000A4FF, ActionId.EfficientBladework));
            world.Events.Add(36.45f, () => omega_M_4000A4FF?.PlayActionTimeline(TimelineId.WarpOut));
            // world.Events.Add(37.95f, () => omega_M_4000A4FF?.SetVisible(false));
            world.Events.Add(38.71f, () => omega_M_4000A4FF?.Despawn());
        }
    }

    private void Run_Optical_Unit_4000A3E7()
    {
        SimEnemy? optical_Unit_4000A3E7 = null;
        world.Events.Add(0f, () => optical_Unit_4000A3E7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OpticalUnit, NameId: BNpcNameId.OpticalUnit, Level: 90, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.150f, 0.000f, -0.600f), -0.000f))));
        world.Events.Add(7.93f, () => optical_Unit_4000A3E7?.SetPosition(state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, 45f), 0f))));
        world.Events.Add(20.76f, () => optical_Unit_4000A3E7?.Cast(ActionId.OpticalLaser, castSeconds: 1.000f, targetId: optical_Unit_4000A3E7?.GameObjectId));
        world.Events.Add(21.76f, () => topUtils.ResolveOpticalLaser(optical_Unit_4000A3E7));
        
    }

    private void Run_Omega_4000A40B_0()
    {
        if (state.AttackF == OmegaAttack.Legs)
        {
            SimEnemy? omega_4000A40B_0 = null;
            world.Events.Add(0, () => omega_4000A40B_0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), -0.000f))));
            world.Events.Add(13.72f, () => omega_4000A40B_0?.SetPosition(state.AttackDir.Apply(Geometry.SuperliminalSteelOmenPlacement)));
            world.Events.Add(13.81f, () => omega_4000A40B_0?.Cast(ActionId.SuperliminalSteelOmenR, targetLocation: state.AttackDir.Apply(Geometry.SuperliminalSteelOmenTargetR), castSeconds: 1.200f, targetId: omega_4000A40B_0?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
        }
    }

    private void Run_Omega_4000A40C_0()
    {
        if (state.AttackF == OmegaAttack.Legs)
        {
            SimEnemy? omega_4000A40C_0 = null;
            world.Events.Add(0f, () => omega_4000A40C_0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), -0.000f))));
            world.Events.Add(13.72f, () => omega_4000A40C_0?.SetPosition(state.AttackDir.Apply(Geometry.SuperliminalSteelOmenPlacement)));
            world.Events.Add(13.81f, () => omega_4000A40C_0?.Cast(ActionId.SuperliminalSteelOmenL, targetLocation: state.AttackDir.Apply(Geometry.SuperliminalSteelOmenTargetL), castSeconds: 1.200f, targetId: omega_4000A40C_0?.GameObjectId, omenDelay: Duration.OmegaAttackOmenDelay));
        }
    }

    private void Run_Omega_4000A40A_0()
    {
        SimEnemy? omega_4000A40A_0 = null;
        world.Events.Add(0f, () => omega_4000A40A_0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), -0.000f))));
        world.Events.Add(15.37f, () => omega_4000A40A_0?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -0.000f)));
        world.Events.Add(15.46f, () => omega_4000A40A_0?.Cast(ActionId.SuperfluidAnimationM, castSeconds: 0f, targetId: omega_4000A40A_0?.GameObjectId));
        world.Events.Add(24.28f, () => omega_4000A40A_0?.Cast(ActionId.SuperfluidAnimationF, castSeconds: 0f, targetId: omega_4000A40A_0?.GameObjectId));
    }

    private void Run_Omega_4000A409_0()
    {
        SimEnemy? omega_4000A409_0 = null;
        world.Events.Add(0f, () => omega_4000A409_0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), -0.000f))));
        world.Events.Add(15.86f, () => omega_4000A409_0?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0.000f, -0.000f, -13.000f), 0))));
        world.Events.Add(15.95f, () => omega_4000A409_0?.Cast(ActionId.SuperfluidAnimationF, castSeconds: 0f, targetId: omega_4000A409_0?.GameObjectId));
        world.Events.Add(23.88f, () => omega_4000A409_0?.Cast(ActionId.SuperfluidAnimationM, castSeconds: 0f, targetId: omega_4000A409_0?.GameObjectId));
    }

    private void Run_Omega_4000A405()
    {
        SimEnemy? omega_4000A405 = null;
        for (int i = 0; i < 8; i++)
        {
            if (state.Order.Get(i) is not {} character) continue;
            world.Events.Add(0f, () => omega_4000A405 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), -0.000f))));
            world.Events.Add(21.74f, () => omega_4000A405?.Cast(ActionId.OptimizedFireIII, castSeconds: 0f, targetId: character.GameObjectId));
            world.Events.Add(21.74f, () => damage.Resolve(character, ActionId.OptimizedFireIII, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.96f)]));
        }
    }

    private void Run_Omega_M_4000A40B_3()
    {
        for(int i = 0; i < 2; i++)
        {
            SimEnemy? omega_M_4000A40B_3 = null;
            if (state.Stacks.Get(i) is not {} character) continue;
            world.Events.Add(33.21f, () => omega_M_4000A40B_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaM, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), -0.000f))));
            world.Events.Add(33.25f, () => omega_M_4000A40B_3?.Cast(ActionId.Spotlight, castSeconds: 0f, targetId: character.GameObjectId));
            world.Events.Add(33.25f, () => damage.Resolve(character, ActionId.Spotlight, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.96f)], stackMinTargets: 4));
        }
    }
}
