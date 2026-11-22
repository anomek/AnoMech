using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergyScenario : IMultiplayerReplayable
{
    public string Name => "Party Synergy";
    public IPhase Phase => TopZone.P2;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP2PartySynergySettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP2PartySynergyAi(), new TopP2PartySynergyLpduAi()];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP2PartySynergyState state = null!;
    private SimEnemy? omegaM, omegaF, omegaFClone;

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP2PartySynergyState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP2PartySynergyState(world.Rng, world.Party, settingsWindow.Overrides);
        LastState = state;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP2PartySynergyState>)AiStrats[idx]).Run(state, world);

        Run_Omega_4000A4E9();
        Run_Omega_4000A4E8();
        Run_Omega_F_4000A4FD();
        Run_Omega_M_4000A4FE();
        Run_Omega_M_4000A4FF();
        Run_Optical_Unit_4000A3E7();
        Run_SuperliminalSteel();
        Run_Superfluid();
        Run_Spotlight();
        Run_OptimizedFire();
        Run_InstanceEvents();
        Run_PlayerTethers(solo);
        Run_PlayerLockons(solo);
    }

    private void Run_InstanceEvents()
    {
        var index = (byte)(state.NewNorthA.Index() + 1);
        world.Events.Add(7.93f, () => world.Map.AddEffect(packetFlags: 0x00020001U, index: index));
        world.Events.Add(17.95f, () => world.Map.AddEffect(packetFlags: 0x00800040U, index: index));
        world.Events.Add(20.67f, () => world.Map.AddEffect(packetFlags: 0x10000001U, index: index));
        world.Events.Add(26.82f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: index));
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
            world.Events.Add(7.93f, () => state.Order.ForEach(p => p.AttachLockonVfx(LockonId.Playstation[world.Rng.Next(4)])));
            return;
        }
        world.Events.Add(7.93f, () => state.Order.ForEach((i, p) =>
        {
            p.AttachLockonVfx(LockonId.Playstation[i/2]); 
        }));
        world.Events.Add(22.63f, () => state.Stacks.ForEach(p => p.AttachLockonVfx(LockonId.Stack)));
    }

    public void Tick(float delta, float elapsed) { }

    private void Run_Omega_4000A4E9()
    {
        world.Events.Add(0f, () => omegaM = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaM, NameId: BNpcNameId.OmegaM_1DD3, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.OmegaM, Placement: new Placement(new Vector3(-3f, 0f, 0f), MathF.PI))));
        world.Events.Add(1f, () => omegaM?.AddStatus(StatusId.OmegaM_D7E, stacks: (ushort)490, overrideStacks: true));
        world.Events.Add(1.88f, () => omegaM?.Cast(ActionId.PartySynergyM, animationLock: 3.1f));
        world.Events.Add(7.93f, () => omegaM?.SetTargetable(false));
        world.Events.Add(8.02f, () => omegaM?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(10.07f, () => omegaM?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -0.000f)));
        world.Events.Add(10.16f, () => omegaM?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(14.48f, () => omegaM?.Cast(ActionId.SubjectSimulationF, animationLock: 2.1f));
        world.Events.Add(15.54f, () => omegaM?.SetModelState((byte)0x06));
        world.Events.Add(15.54f, () => omegaM?.RemoveStatus(StatusId.OmegaM_D7E));
        world.Events.Add(15.54f, () => omegaM?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omegaM?.Cast(ActionId.SubjectSimulationFWarpDown, animationLock: 4.1f));
        world.Events.Add(17.15f, () => omegaM?.SetModelState((byte)0x0B));
        world.Events.Add(20.72f, () => omegaM?.Cast(ActionId.SubjectSimulationFWarpUp, animationLock: 4.1f));
        world.Events.Add(24.41f, () => omegaM?.SetModelState((byte)0x05));
        world.Events.Add(24.41f, () => omegaM?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(24.41f, () => omegaM?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true));
        world.Events.Add(24.82f, () => omegaM?.Cast(ActionId.Unknown7b20, animationLock: 2.1f));
        world.Events.Add(25.44f, () => omegaM?.SetModelState((byte)0x0B));
        world.Events.Add(28.93f, () => omegaM?.Cast(Actions.Discharger));
        world.Events.Add(36.50f, () => omegaM?.SetTargetable(true));
    }

    private void Run_Omega_4000A4E8()
    {
        world.Events.Add(0f, () => omegaF = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaF, NameId: BNpcNameId.OmegaM_1DD3, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.OmegaF, Placement: new Placement(new Vector3(3f, 0f, 0f), MathF.PI))));
        world.Events.Add(1f, () => omegaF?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true));
        world.Events.Add(1.92f, () => omegaF?.Cast(ActionId.PartySynergyF, animationLock: 3.1f));
        world.Events.Add(7.98f, () => omegaF?.SetTargetable(false));
        world.Events.Add(8.02f, () => omegaF?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(10.07f, () => omegaF?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0f, -0.000f, -13.000f), 0f))));
        world.Events.Add(10.16f, () => omegaF?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(14.48f, () => omegaF?.Cast(ActionId.SubjectSimulationM, animationLock: 2.1f));
        world.Events.Add(16.12f, () => omegaF?.SetModelState((byte)0x05));
        world.Events.Add(16.12f, () => omegaF?.RemoveStatus(StatusId.OmegaF));
        world.Events.Add(16.12f, () => omegaF?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omegaF?.Cast(ActionId.Unknown7b17, animationLock: 4.1f));
        world.Events.Add(17.24f, () => omegaF?.SetModelState((byte)0x0B));
        world.Events.Add(20.72f, () => omegaF?.Cast(ActionId.Unknown7b1d, animationLock: 4.1f));
        world.Events.Add(24.01f, () => omegaF?.SetModelState((byte)0x06));
        world.Events.Add(24.01f, () => omegaF?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(24.01f, () => omegaF?.AddStatus(StatusId.OmegaM_D7E, stacks: (ushort)490, overrideStacks: true));
        world.Events.Add(24.82f, () => omegaF?.Cast(ActionId.Unknown7b1f, animationLock: 2.1f));
        world.Events.Add(25.62f, () => omegaF?.SetModelState((byte)0x0B));
        world.Events.Add(31.82f, () => omegaF?.Cast(Actions.EfficientBladework));
        world.Events.Add(36.50f, () => omegaF?.SetTargetable(true));
    }

    private void Run_Omega_F_4000A4FD()
    {
        world.Events.Add(2.23f, () => omegaFClone = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: OmegaAttack.SpawnAttributeFlags, BNpcBaseId: BNpcBaseId.OmegaFClone, NameId: BNpcNameId.OmegaF, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.OmegaFClone, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(10.07f, () => state.AttackF.Reshape(omegaFClone));
        world.Events.Add(10.07f, () => omegaFClone?.SetPosition(state.AttackDir.Apply(new Placement(new Vector3(0f, 0f, -10f), 0))));
        world.Events.Add(10.16f, () => omegaFClone?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(13.81f, () => omegaFClone?.Cast(state.AttackF.Action));
        world.Events.Add(18.44f, () => omegaFClone?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(20.66f, () => omegaFClone?.Despawn());
    }

    private void Run_Omega_M_4000A4FE()
    {
        SimEnemy? omega_M_4000A4FE = null;
        world.Events.Add(2.23f, () => omega_M_4000A4FE = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: OmegaAttack.SpawnAttributeFlags, BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.OmegaMClone, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(10.07f, () => state.AttackM.Reshape(omega_M_4000A4FE));
        world.Events.Add(10.07f, () => omega_M_4000A4FE?.SetPosition(state.AttackDir.Flip().Apply(new Placement(new Vector3(0, 0, -10f), 0f))));
        world.Events.Add(10.16f, () => omega_M_4000A4FE?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(13.81f, () => omega_M_4000A4FE?.Cast(state.AttackM.Action));
        world.Events.Add(18.44f, () => omega_M_4000A4FE?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(20.66f, () => omega_M_4000A4FE?.Despawn());
    }

    private void Run_Omega_M_4000A4FF()
    {
        for (int i = 0; i < 4; i++) {
            SimEnemy? omega_M_4000A4FF = null;
            var position = state.NewNorthB.Rotate(1 + i * 2).Apply(new Placement(new Vector3(0, 0, -13), 0));
            world.Events.Add(2.23f, () => omega_M_4000A4FF = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: OmegaAttack.SpawnAttributeFlags, BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.OmegaMClone, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
            world.Events.Add(10.07f, () => omega_M_4000A4FF?.SetPosition(position));
            world.Events.Add(16.17f, () => omega_M_4000A4FF?.PlayActionTimeline(ActionTimelineId.WarpEnd));
            world.Events.Add(31.82f, () => omega_M_4000A4FF?.Cast(Actions.EfficientBladework));
            world.Events.Add(36.45f, () => omega_M_4000A4FF?.PlayActionTimeline(ActionTimelineId.WarpStart));
            world.Events.Add(38.71f, () => omega_M_4000A4FF?.Despawn());
        }
    }

    private void Run_Optical_Unit_4000A3E7()
    {
        SimEnemy? optical_Unit_4000A3E7 = null;
        world.Events.Add(0f, () => optical_Unit_4000A3E7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OpticalUnit, NameId: BNpcNameId.OpticalUnit, Level: 90, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.OpticalUnit, Placement: new Placement(new Vector3(0.150f, 0.000f, -0.600f), -0.000f))));
        world.Events.Add(7.93f, () => optical_Unit_4000A3E7?.SetPosition(state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, 45f), MathF.PI))));
        world.Events.Add(20.76f, () => optical_Unit_4000A3E7?.Cast(Actions.OpticalLaser));
        
    }

    private void Run_SuperliminalSteel()
    {
        if (state.AttackF != OmegaAttack.Legs) return;
        world.Events.Add(13.81f, () => omegaFClone?.CastFromHelper(Actions.SuperliminalSteelR, state.AttackDir.Apply(Geometry.SuperliminalSteelOmenTargetR),
            at: state.AttackDir.Apply(Geometry.SuperliminalSteelOmenPlacement)));
        world.Events.Add(13.81f, () => omegaFClone?.CastFromHelper(Actions.SuperliminalSteelL, state.AttackDir.Apply(Geometry.SuperliminalSteelOmenTargetL),
            at: state.AttackDir.Apply(Geometry.SuperliminalSteelOmenPlacement)));
    }

    private void Run_Superfluid()
    {
        world.Events.Add(15.46f, () => omegaM?.CastFromHelper(ActionId.SuperfluidAnimationM, animationLock: 2.1f));
        world.Events.Add(15.95f, () => omegaF?.CastFromHelper(ActionId.SuperfluidAnimationF, animationLock: 2.1f));
        world.Events.Add(23.88f, () => omegaF?.CastFromHelper(ActionId.SuperfluidAnimationM, animationLock: 2.1f));
        world.Events.Add(24.28f, () => omegaM?.CastFromHelper(ActionId.SuperfluidAnimationF, animationLock: 2.1f));
    }

    private void Run_Spotlight()
    {
        if (state.Stacks.Get(0) is { } first)
            world.Events.Add(33.25f, () => omegaF?.CastFromHelper(Actions.Spotlight, first));
        if (state.Stacks.Get(1) is { } second)
            world.Events.Add(33.25f, () => omegaM?.CastFromHelper(Actions.Spotlight, second));
    }

    private void Run_OptimizedFire()
    {
        for (int i = 0; i < 8; i++)
        {
            if (state.Order.Get(i) is not {} character) continue;
            world.Events.Add(21.74f, () => omegaM?.CastFromHelper(Actions.OptimizedFireIII, character, at: new Placement(new Vector3(0f, 0f, -10f), 0f)));
        }
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP2PartySynergyAiReplayStateMessage(
            s.Order.List, s.Stacks.List, s.NewNorthA.RadiansFromNorth, s.NewNorthB.RadiansFromNorth,
            s.AttackDir.RadiansFromNorth, s.Glitch == GlitchType.Far, s.AttackM == OmegaAttack.Sword, s.AttackF == OmegaAttack.Staff)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP2PartySynergyAiReplayStateMessage msg) return null;
        var shadowState = TopP2PartySynergyState.FromNetworkReplay(
            replayWorld.Party, msg.Order, msg.Stacks, msg.NewNorthARadians, msg.NewNorthBRadians,
            msg.AttackDirRadians, msg.GlitchIsFar, msg.AttackMIsSword, msg.AttackFIsStaff);
        ((IScenarioAi<TopP2PartySynergyState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
