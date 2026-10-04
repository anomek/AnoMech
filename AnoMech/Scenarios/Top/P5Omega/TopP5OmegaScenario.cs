using AnoMech.Core.Native.Interfaces;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P5Omega;

public sealed class TopP5OmegaScenario : IMultiplayerReplayable
{
    public string Name => "Omega";
    public IPhase Phase => TopZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    private SimWorld world = null!;
    private SimParty party = null!;

    TopP5OmegaState state = null!;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP5OmegaSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP5OmegaAi()];

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP5OmegaState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5OmegaState(world.Rng, world.Party, settingsWindow.Overrides);
        LastState = state;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP5OmegaState>)AiStrats[idx]).Run(state, world);


        Run_Omega_M_4000A63C();
        Run_Omega_F_4000A72A();
        if(!solo) Run_Omega_4000A72E();
        Run_Omega_4000A72F(solo);
        Run_Omega_4000A40B_1();
        Run_Omega_F_4000A40B_2(solo);
        if(!solo) Run_Omega_4000A409_1();
        Run_InstanceEvents();
        if (!solo) Run_OtherDebuffs();
    }

    private void Run_InstanceEvents()
    {
        world.Events.Add(30.96f, () => Natives.Director.ProcessDirectorUpdate(0x80000004U, 0x1517U));
    }

    private void Run_OtherDebuffs()
    {
        world.Events.Add(0.5f, () =>
        {
            party.ForEachActive(member => member.AddStatus(StatusId.QuickeningDynamis, stacks: 1));
            state.DoubleDynamicTargets.ForEach(member => member.AddStatus(StatusId.QuickeningDynamis, stacks: 1));
        });
        world.Events.Add(9.21f, () =>
        {
            ushort[] statuses1 = [StatusId.HelloNearWorld, StatusId.HelloDistantWorld, StatusId.HelloNearWorld, StatusId.HelloDistantWorld];
            float[] durations = [32f, 32f, 50f, 50f];
            ushort[] statuses2 = [StatusId.FirstInLine, StatusId.FirstInLine, StatusId.SecondInLine, StatusId.SecondInLine];
            state.HelloWorldTargets.ForEach((i, member) =>
            {
               member.AddStatus(statuses1[i], durations[i]);
               member.AddStatus(statuses2[i]);
            });
        });
        world.Events.Add(41.16f, () =>
        {
            state.HelloWorldTargets.Get(0)?.RemoveStatus(StatusId.FirstInLine);
            state.HelloWorldTargets.Get(1)?.RemoveStatus(StatusId.FirstInLine);
        });
        world.Events.Add(59.18f, () =>
        {
            state.HelloWorldTargets.Get(2)?.RemoveStatus(StatusId.SecondInLine);
            state.HelloWorldTargets.Get(3)?.RemoveStatus(StatusId.SecondInLine);
        });
        // Host-only, resolved here (not in TopP5OmegaAi, which also runs for a peer's own
        // replay) and broadcast via BuildMidRunUpdateMessage below -- a live status read +
        // shuffle run independently on both sides could disagree on who stands where.
        world.Events.Add(46f, () => state.HelloWorld2 ??= ResolveHelloWorld2());
    }

    private PartyRole[] ResolveHelloWorld2()
    {
        List<PartyRole> freeAgents = [];
        List<PartyRole> tethers = [];
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (role == state.HelloWorldTargets[2] || role == state.HelloWorldTargets[3]) continue;
            if (party.Get(role)?.FindStatus(StatusId.QuickeningDynamis) is { Stacks: 3 })
                tethers.Add(role);
            else
                freeAgents.Add(role);
        }
        freeAgents = world.Rng.Shuffle(freeAgents).ToList();
        tethers = world.Rng.Shuffle(tethers).ToList();
        // Live soak count can land short of (or over) 2 by t=46s -- borrow from the other
        // pool instead of assuming an exact 2/4 split.
        while (tethers.Count < 2) tethers.Add(Pop(freeAgents));
        while (freeAgents.Count < 4) freeAgents.Add(Pop(tethers));
        return
        [
            state.HelloWorldTargets[2], state.HelloWorldTargets[3], tethers[0], tethers[1],
            freeAgents[0], freeAgents[1], freeAgents[2], freeAgents[3]
        ];
    }

    private static PartyRole Pop(List<PartyRole> list)
    {
        var last = list[^1];
        list.RemoveAt(list.Count - 1);
        return last;
    }

    public void Tick(float delta, float elapsed)
    {
        HelloWorld.CheckHolderDeaths(world);
    }

    private void Run_Omega_M_4000A63C()
    {
        SimEnemy? omega_M_4000A63C = null;
        world.Events.Add(0, () => omega_M_4000A63C = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaFDynamis, NameId: BNpcNameId.OmegaFDynamis, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(-000f, -0.000f, 0.000f), MathF.PI))));
        world.Events.Add(1.15f, () => omega_M_4000A63C?.Cast(Actions.RunMiOmegaVersion));
        var target = party.Get(PartyRole.MainTank) ?? party.Get(party.PlayerRole);
        world.Events.Add(6f, () => omega_M_4000A63C?.Follow(target));
        world.Events.Add(6.81f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(9.83f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(12.85f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(15.88f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(18.90f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(21.93f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(24.96f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(27.98f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(31.01f, () => omega_M_4000A63C?.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target?.GameObjectId));
        world.Events.Add(31.54f, () => omega_M_4000A63C?.Follow());
        world.Events.Add(31.54f, () => omega_M_4000A63C?.SetTargetable(false));
        world.Events.Add(31.63f, () => omega_M_4000A63C?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(59.40f, () => omega_M_4000A63C?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 3.142f)));
        world.Events.Add(59.49f, () => omega_M_4000A63C?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(60.52f, () => omega_M_4000A63C?.SetVisible(true));
        world.Events.Add(63.59f, () => omega_M_4000A63C?.SetTargetable(true));
    }

    private void Run_Omega_F_4000A72A()
    {
        uint[] bNpcBaseIds = [BNpcBaseId.OmegaFDynamis, BNpcBaseId.OmegaMDynamis, BNpcBaseId.OmegaFDynamis, BNpcBaseId.OmegaMDynamis];
        uint[] bNpcNameIds = [BNpcNameId.OmegaFDynamis, BNpcNameId.OmegaMDynamis, BNpcNameId.OmegaFDynamis, BNpcNameId.OmegaMDynamis];
        float[] timeOffset = [-3.95f, -3.95f, 0f, 0f];
        for (var i = 0; i < 4; i++)
        {
            var baseId = bNpcBaseIds[i];
            var nameId = bNpcNameIds[i];
            var attack = state.OmegaAttacks[i];
            var direction = state.AttackDirections[i];
            var offset = timeOffset[i];
            SimEnemy? omega_F_4000A72A = null;
            world.Events.Add(1.39f, () => omega_F_4000A72A = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: attack.AttributeFlags, BNpcBaseId: baseId, NameId: nameId, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: direction.Apply(new Placement(new Vector3(0,  0, -9.8f), 0)))));
            world.Events.Add(15.30f + offset, () => omega_F_4000A72A?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(15.39f + offset, () => omega_F_4000A72A?.SetVisible(true));
            world.Events.Add(26.96f + offset, () => omega_F_4000A72A?.Cast(attack.Action));
            world.Events.Add(31.59f + offset, () => omega_F_4000A72A?.PlayActionTimeline(TimelineId.WarpOut));
            world.Events.Add(33.83f + offset, () => omega_F_4000A72A?.Despawn());
        }
    }



    private void Run_Omega_4000A72E()
    {
        SimEnemy? omega_4000A72E = null;
        SimTether? tether1 = null;
        SimTether? tether2 = null;
        var placement =state.BettleSpawnDirection.Apply(new Placement(new Vector3(0.000f, 0.000f, -20.000f), 0));
        world.Events.Add(1.39f, () => omega_4000A72E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BeetleHelper, NameId: BNpcNameId.OmegaBeetle, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: placement)));
        world.Events.Add(41.29f, () => omega_4000A72E?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(41.29f, () => omega_4000A72E?.SetVisible(true));
        world.Events.Add(45.35f, () => tether1 = world.Tether(End.Passable(state.BlasterTetherTargets.Get(0)), omega_4000A72E, TetherId.PassableTether));
        world.Events.Add(45.35f, () => tether2 = world.Tether(End.Passable(state.BlasterTetherTargets.Get(1)), omega_4000A72E, TetherId.PassableTether));
        world.Events.Add(45.43f, () => omega_4000A72E?.Cast(ActionId.OmegaBlaster));
        world.Events.Add(57.53f, () => omega_4000A72E?.Cast(ActionId.BlasterEffect, castSeconds: 0f, targetId: omega_4000A72E?.GameObjectId));
        world.Events.Add(60.65f, () => omega_4000A72E?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(61.44f, () => omega_4000A72E?.SetVisible(false));
        world.Events.Add(63.07f, () => omega_4000A72E?.Despawn());
        
        for (int index = 0; index < 2; index++)
        {
            SimEnemy? omega_4000A40C_3 = null;
            var i = index;
            world.Events.Add(57.50f, () => omega_4000A40C_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaBeetle, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: placement)));
            world.Events.Add(57.58f, () =>
            {
                var tether = i == 0 ? tether1 : tether2;
                if (tether?.A == null) return;
                if (tether.A.IsAlive()) omega_4000A40C_3?.Cast(Actions.Blaster, tether.A);
                tether.Despawn();
            });
        }
    }

    private void Run_Omega_4000A72F(bool solo)
    {
        SimEnemy? omega_4000A72F = null;
        var waveCannonId = state.FirstWaveCannonFront ? ActionId.OmegaDiffuseWaveCannonFront : ActionId.OmegaDiffuseWaveCannonSides;
        var repCannonId = state.FirstWaveCannonFront ? ActionId.OmegaDiffuseWaveCannonRepeatSides : ActionId.OmegaDiffuseWaveCannonRepeatFront;
        world.Events.Add(1.39f, () => omega_4000A72F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.FinalHelper, NameId: BNpcNameId.OmegaFinal, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(11.34f, () => omega_4000A72F?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(11.44f, () => omega_4000A72F?.SetVisible(true));
        world.Events.Add(15.48f, () => omega_4000A72F?.Cast(waveCannonId));
        world.Events.Add(27.54f, () => omega_4000A72F?.Cast(repCannonId));
        if (solo) return;
        world.Events.Add(31.68f, () => omega_4000A72F?.Cast(state.MonitorSide.ActionId));
        world.Events.Add(44.81f, () => omega_4000A72F?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(47.13f, () => omega_4000A72F?.Despawn());
    }

    private void Run_Omega_4000A40B_1()
    {
        float[] timeOffset = [0f, 0f, 4f, 4f];
        float[] orientations = state.FirstWaveCannonFront
            ? [0, MathF.PI, MathF.PI / 2, -MathF.PI / 2]
            : [MathF.PI / 2, -MathF.PI / 2, 0, MathF.PI];
        
        for(var i = 0; i < 4; i++)
        {
            var offset = timeOffset[i];
            var orientation = orientations[i];
            SimEnemy? omega_4000A40B_1 = null;
            world.Events.Add(23.56f + offset, () => omega_4000A40B_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaFinal, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), orientation))));
            world.Events.Add(23.58f + offset, () => omega_4000A40B_1?.Cast(Actions.DiffuseWaveCannon));
        }
    }

    private void Run_Omega_4000A409_1()
    {
        SimEnemy? omega_4000A409_1 = null;
        SimEnemy? omega_4000A40A_1 = null;
        world.Events.Add(27.67f, () => omega_4000A409_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaFinal, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), -0.000f))));
        world.Events.Add(27.67f, () => omega_4000A40A_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaFinal, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(41.74f, () =>
        {
            // Both picked before the first circle lands, so a death can't change the second pick.
            var targets = party.Find.OnSideN(world.Rng, new Placement(new(0, 0, 0), MathF.PI), state.MonitorSide.Mul, 2);
            if (targets.Count > 0)
                omega_4000A409_1?.Cast(Actions.OversampledWaveCannon, targets[0]);
            if (targets.Count > 1)
                omega_4000A40A_1?.Cast(Actions.OversampledWaveCannon, targets[1]);
        });
    }


    private void Run_Omega_F_4000A40B_2(bool solo)
    {
        var firstLegs = state.OmegaAttacks[0] == OmegaAttack.Legs;
        var secondLegs = state.OmegaAttacks[2] == OmegaAttack.Legs;
        EnemyAction?[] superliminalSteel =
        [
            firstLegs ? Actions.SuperliminalSteelR : null, firstLegs ? Actions.SuperliminalSteelL : null,
            secondLegs ? Actions.SuperliminalSteelR : null, secondLegs ? Actions.SuperliminalSteelL : null,
            null, null,
        ];
        Vector3[] superliminalTargets = [Geometry.SuperliminalSteelOmenTargetR, Geometry.SuperliminalSteelOmenTargetL, Geometry.SuperliminalSteelOmenTargetR, Geometry.SuperliminalSteelOmenTargetL, default, default];
        Direction[] superliminalDirections = [state.AttackDirections[0], state.AttackDirections[0], state.AttackDirections[3], state.AttackDirections[3], Direction.N, Direction.N];
        float[] superliminalOffset = [-4f, -4f, 0, 0, 0, 0];
        float[] dynamisOffsets = [0, 0, 1, 1, 2, 2, 3, 3];
        var nearHelper1 = new HelloWorld(world.Party, state.HelloWorldTargets[0], true);
        var farHelper1 = new HelloWorld(world.Party, state.HelloWorldTargets[1], false);
        var nearHelper2 = new HelloWorld(world.Party, state.HelloWorldTargets[2], true);
        var farHelper2 = new HelloWorld(world.Party, state.HelloWorldTargets[3], false);
                                
        for (int i = 0; i < 6; i++)
        {
            SimEnemy? omega_F_4000A40B_2 = null;
            var offset = superliminalOffset[i];
            var direction = superliminalDirections[i];
            var steel = superliminalSteel[i];
            var target = superliminalTargets[i];
            var helper1 = i % 2 == 0 ? nearHelper1 : farHelper1;
            var helper2 = i  % 2 == 0 ? nearHelper2 : farHelper2;
            var dynamisOffset = dynamisOffsets[i];
            
            world.Events.Add(26.92f + offset, () => omega_F_4000A40B_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.OmegaFDynamis, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: direction.Apply(Geometry.SuperliminalSteelOmenPlacement))));
            if (steel != null)
                world.Events.Add(26.96f + offset, () => omega_F_4000A40B_2?.Cast(steel, direction.Apply(target)));
            
            if(solo) continue;
            world.Events.Add(41.16f + dynamisOffset, () => helper1.SetPosition(omega_F_4000A40B_2));
            world.Events.Add(41.25f + dynamisOffset, () => helper1.CastSpell(omega_F_4000A40B_2));
            
            world.Events.Add(59.26f + dynamisOffset, () => helper2.SetPosition(omega_F_4000A40B_2));
            world.Events.Add(59.27f + dynamisOffset, () => helper2.CastSpell(omega_F_4000A40B_2));
        }
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP5OmegaAiReplayStateMessage(
            s.HelloWorldTargets.List, s.DoubleDynamicTargets.List, s.MonitorTargets.List, s.HelloWorld1JumpOrder.List,
            s.AttackDirections.Select(d => d.RadiansFromNorth).ToArray(), s.OmegaAttacks.ToArray(),
            s.BettleSpawnDirection.RadiansFromNorth, s.FirstWaveCannonFront, s.MonitorSide == MonitorSide.Left)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP5OmegaAiReplayStateMessage msg) return null;
        var shadowState = TopP5OmegaState.FromNetworkReplay(
            replayWorld.Party, msg.HelloWorldTargets, msg.DoubleDynamicTargets, msg.MonitorTargets, msg.HelloWorld1JumpOrder,
            msg.AttackDirectionsRadians,
            msg.OmegaAttacks, msg.BettleSpawnDirectionRadians, msg.FirstWaveCannonFront, msg.MonitorIsLeft);
        ((IScenarioAi<TopP5OmegaState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Edge-triggers BuildMidRunUpdateMessage -- see IMultiplayerReplayable.BuildMidRunUpdateMessage.
    private bool helloWorld2Broadcast;

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (helloWorld2Broadcast || LastState?.HelloWorld2 is not { } roles) return null;
        helloWorld2Broadcast = true;
        DiagnosticLog.Info($"[Multiplayer] Host: broadcasting P5 Omega HelloWorld2 update -- [{string.Join(",", roles)}].");
        return new TopP5OmegaHelloWorld2UpdateMessage(roles);
    }

    public void ApplyMidRunUpdate(object shadowStateObj, MpMessage message)
    {
        if (shadowStateObj is TopP5OmegaState shadowState && message is TopP5OmegaHelloWorld2UpdateMessage update)
            shadowState.HelloWorld2 = update.Roles;
    }
}
