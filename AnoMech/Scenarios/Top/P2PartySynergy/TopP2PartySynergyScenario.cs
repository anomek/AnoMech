using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Scenarios.Top.TopConstants;
using AnoMech.Core.Native.Interfaces;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergyScenario : IMultiplayerReplayable
{
    public string Name => "Party Synergy";
    public IPhase Phase => TopZone.P2;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 26.11f;

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP2PartySynergySettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP2PartySynergyAi()];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP2PartySynergyState state = null!;
    private TopHelpers? helpers;

    // Each hit's result, and with it any death, lands this long after the effect.
    private const float AttackResultDelay = 0.63f;
    private const float LaserResultDelay = 0.18f;

    private static TimingSpec AttackLands(float fireDelay)
        => new() { VfxOffset = fireDelay, ResolveOffset = fireDelay, DeathDelay = AttackResultDelay };

    private static readonly EnemyAction OpticalLaser = Actions.OpticalLaser with
    {
        Timing = new() { VfxOffset = 0.3f, ResolveOffset = 0.3f, DeathDelay = LaserResultDelay },
    };

    private static readonly EnemyAction Discharger = Actions.Discharger with { Timing = new() { ResolveOffset = 0.64f } };

    // Solo has nobody to share a Spotlight with.
    private static readonly EnemyAction SoloSpotlight = Actions.Spotlight with
    {
        Effects = [Damage(Actions.Magic), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
    };

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (6757, "mon_sp/gimmick/z3of_boss_gimmick16"),
        (6758, "mon_sp/gimmick/z3of_boss_gimmick17"),
        (6730, "mon_sp/gimmick/z3of_boss_gimmick02"),
        (10767, "mon_sp/gimmick/z3oz_boss_gimmick24"),
    ];

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP2PartySynergyState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP2PartySynergyState(world.Rng, world.Party, settingsWindow.Overrides);
        LastState = state;
        helpers = null;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP2PartySynergyState>)AiStrats[idx]).Run(state, world);

        Run_Omega_4000A4E9();
        Run_Omega_4000A4E8();
        Run_Omega_F_4000A4FD();
        Run_Omega_M_4000A4FE();
        Run_Omega_M_4000A4FF();
        Run_Optical_Unit_4000A3E7();
        Run_Helpers(solo);
        Run_InstanceEvents();
        Run_PacketFilters();
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

    // Firewall, cast before this window opens, gives each light party one boss's filter.
    private void Run_PacketFilters()
    {
        world.Events.Add(0f, () =>
        {
            foreach (var role in Enum.GetValues<PartyRole>())
                party.Get(role)?.AddStatus(TopLightParties.IsLeft(role) ? StatusId.PacketFilterF : StatusId.PacketFilterM);
        });
    }

    private void Run_PlayerTethers(bool solo)
    {
        if (solo)
        {
            world.Events.Add(7.93f, () => state.Order.ForEach(p => p.AddStatus(state.Glitch.StatusId, duration: 27f)));
            return;
        }
        // It looks like there is a second of leeway here, but unclear if it accounts for snapshotting to be honest
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
        if (solo)
            world.Events.Add(7.93f, () => state.Order.ForEach(p => p.AttachLockonVfx(LockonId.Playstation[world.Rng.Next(4)], persistent: false)));
        else
            world.Events.Add(7.93f, () => state.Order.ForEach((i, p) => p.AttachLockonVfx(LockonId.Playstation[i / 2], persistent: false)));
        world.Events.Add(22.59f, () => state.Stacks.ForEach(p => p.AttachLockonVfx(LockonId.Stack, persistent: false)));
    }

    public void Tick(float delta, float elapsed) { }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(GimmickTimelines, "TopP2PartySynergy");

    // The legs Omega-F and the shield Omega-M take their form while still hidden.
    private static bool HasForm(OmegaAttack attack) => attack == OmegaAttack.Legs || attack == OmegaAttack.Shield;

    private static byte FormFlags(OmegaAttack attack) => HasForm(attack) ? (byte)0x31 : (byte)0x10;

    private void TargetClone(SimEnemy? clone)
    {
        if (clone is not { IsActive: true }) return;
        var target = party.Get(state.CloneTarget) is { } healer && healer.IsAlive() ? healer : party.Find.RandomMember(world.Rng);
        clone.SetTarget(target, follow: false);
    }

    private void Run_Omega_4000A4E9()
    {
        SimEnemy? omega_4000A4E9 = null;
        world.Events.Add(0f, () =>
        {
            omega_4000A4E9 = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaM, NameId: BNpcNameId.OmegaM_1DD3, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true , Placement: new Placement(new Vector3(-3.030f, 0.000f, -0.010f), 2.570f), WeaponDrawn: true));
            omega_4000A4E9?.AddStatus(StatusId.OmegaM_D7E, stacks: (ushort)490, overrideStacks: true);
        });
        world.Events.Add(1.88f, () => omega_4000A4E9?.Cast(ActionId.PartySynergyM, castSeconds: 2.700f, targetId: omega_4000A4E9?.GameObjectId, animationLock: 3.1f, fireDelay: 0.29f));
        world.Events.Add(7.93f, () => omega_4000A4E9?.SetTargetable(false));
        world.Events.Add(8.02f, () => omega_4000A4E9?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(9.55f, () => omega_4000A4E9?.SetVisible(false));
        world.Events.Add(10.07f, () => omega_4000A4E9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -0.000f)));
        world.Events.Add(10.16f, () => omega_4000A4E9?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(10.22f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(14.48f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationF, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId, animationLock: 2.1f));
        world.Events.Add(15.54f, () => omega_4000A4E9?.SetModelState((byte)0x06));
        world.Events.Add(15.54f, () => omega_4000A4E9?.RemoveStatus(StatusId.OmegaM_D7E));
        world.Events.Add(15.54f, () => omega_4000A4E9?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationFWarpDown, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId, animationLock: 4.1f));
        // world.Events.Add(16.62f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(17.15f, () => omega_4000A4E9?.SetModelState((byte)0x0B));
        world.Events.Add(20.67f, () => omega_4000A4E9?.Cast(ActionId.SubjectSimulationFWarpUp, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId, animationLock: 4.1f));
        world.Events.Add(24.41f, () => omega_4000A4E9?.SetModelState((byte)0x05));
        world.Events.Add(24.41f, () => omega_4000A4E9?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(24.41f, () => omega_4000A4E9?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true));
        world.Events.Add(24.77f, () => omega_4000A4E9?.Cast(ActionId.Unknown7b20, castSeconds: 0f, targetId: omega_4000A4E9?.GameObjectId, animationLock: 2.1f));
        world.Events.Add(25.46f, () => omega_4000A4E9?.SetVisible(true));
        world.Events.Add(25.44f, () => omega_4000A4E9?.SetModelState((byte)0x0B));
        world.Events.Add(28.91f, () => omega_4000A4E9?.Cast(Discharger));
        world.Events.Add(36.50f, () => omega_4000A4E9?.SetTargetable(true));
    }

    private void Run_Omega_4000A4E8()
    {
        SimEnemy? omega_4000A4E8 = null;
        world.Events.Add(0f, () =>
        {
            omega_4000A4E8 = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaF, NameId: BNpcNameId.OmegaM_1DD3, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(2.980f, 0.000f, -0.010f), -2.390f), WeaponDrawn: true));
            omega_4000A4E8?.AddStatus(StatusId.OmegaF, stacks: (ushort)491, overrideStacks: true);
        });
        world.Events.Add(1.79f, () => world.Map.BattleTalk(BNpcNameId.OmegaF, BattleTalkId.PartyMemberGeneration, 6000));
        world.Events.Add(1.88f, () => omega_4000A4E8?.Cast(ActionId.PartySynergyF, castSeconds: 2.700f, targetId: omega_4000A4E8?.GameObjectId, animationLock: 3.1f, fireDelay: 0.29f));
        world.Events.Add(7.98f, () => omega_4000A4E8?.SetTargetable(false));
        world.Events.Add(8.02f, () => omega_4000A4E8?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(9.55f, () => omega_4000A4E8?.SetVisible(false));
        world.Events.Add(10.07f, () => omega_4000A4E8?.SetPosition(state.NewNorthB.Apply(new Placement(new Vector3(0f, -0.000f, -13.000f), 0f))));
        world.Events.Add(10.16f, () => omega_4000A4E8?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(11.15f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(14.48f, () => omega_4000A4E8?.Cast(ActionId.SubjectSimulationM, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId, animationLock: 2.1f));
        world.Events.Add(16.12f, () => omega_4000A4E8?.SetModelState((byte)0x05));
        world.Events.Add(16.12f, () => omega_4000A4E8?.RemoveStatus(StatusId.OmegaF));
        world.Events.Add(16.12f, () => omega_4000A4E8?.AddStatus(StatusId.Superfluid, stacks: (ushort)493, overrideStacks: true));
        world.Events.Add(16.61f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b17, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId, animationLock: 4.1f));
        // world.Events.Add(17.21f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(17.24f, () => omega_4000A4E8?.SetModelState((byte)0x0B));
        world.Events.Add(20.67f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b1d, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId, animationLock: 4.1f));
        world.Events.Add(23.97f, () => omega_4000A4E8?.SetModelState((byte)0x06));
        world.Events.Add(23.97f, () => omega_4000A4E8?.RemoveStatus(StatusId.Superfluid));
        world.Events.Add(23.97f, () => omega_4000A4E8?.AddStatus(StatusId.OmegaM_D7E, stacks: (ushort)490, overrideStacks: true));
        // world.Events.Add(24.04f, () => omega_4000A4E8?.SetVisible(true));
        world.Events.Add(24.77f, () => omega_4000A4E8?.Cast(ActionId.Unknown7b1f, castSeconds: 0f, targetId: omega_4000A4E8?.GameObjectId, animationLock: 2.1f));
        world.Events.Add(25.62f, () => omega_4000A4E8?.SetModelState((byte)0x0B));
        world.Events.Add(31.80f, () => omega_4000A4E8?.Cast(Actions.EfficientBladework with { Timing = AttackLands(0.28f) }));
        world.Events.Add(36.50f, () => omega_4000A4E8?.SetTargetable(true));
    }

    private void Run_Omega_F_4000A4FD()
    {
        SimEnemy? omega_F_4000A4FD = null;
        world.Events.Add(2.23f, () => omega_F_4000A4FD = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: FormFlags(state.AttackF), BNpcBaseId: BNpcBaseId.OmegaFClone, NameId: BNpcNameId.OmegaF, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f), WeaponDrawn: true)));
        world.Events.Add(10.07f, () => omega_F_4000A4FD?.SetPosition(state.AttackDir.Apply(new Placement(new Vector3(0f, 0f, -10f), 0))));
        if (HasForm(state.AttackF)) world.Events.Add(10.07f, () => omega_F_4000A4FD?.SetModelState(0x04));
        world.Events.Add(10.16f, () => omega_F_4000A4FD?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(10.16f, () => omega_F_4000A4FD?.SetVisible(true));
        world.Events.Add(12.15f, () => TargetClone(omega_F_4000A4FD));
        world.Events.Add(13.81f, () => omega_F_4000A4FD?.Cast(state.AttackF.Action with { Timing = AttackLands(0.25f) }));
        world.Events.Add(18.44f, () => omega_F_4000A4FD?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(19.93f, () => omega_F_4000A4FD?.SetVisible(false));
        world.Events.Add(20.66f, () => omega_F_4000A4FD?.Despawn());
    }

    private void Run_Omega_M_4000A4FE()
    {
        SimEnemy? omega_M_4000A4FE = null;
        world.Events.Add(2.23f, () => omega_M_4000A4FE = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: FormFlags(state.AttackM), BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f), WeaponDrawn: true)));
        world.Events.Add(10.07f, () => omega_M_4000A4FE?.SetPosition(state.AttackDir.Flip().Apply(new Placement(new Vector3(0, 0, -10f), 0f))));
        if (HasForm(state.AttackM)) world.Events.Add(10.07f, () => omega_M_4000A4FE?.SetModelState(0x04));
        world.Events.Add(10.16f, () => omega_M_4000A4FE?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(10.16f, () => omega_M_4000A4FE?.SetVisible(true));
        world.Events.Add(12.15f, () => TargetClone(omega_M_4000A4FE));
        world.Events.Add(13.81f, () => omega_M_4000A4FE?.Cast(state.AttackM.Action with { Timing = AttackLands(0.25f) }));
        world.Events.Add(18.44f, () => omega_M_4000A4FE?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(19.93f, () => omega_M_4000A4FE?.SetVisible(false));
        world.Events.Add(20.66f, () => omega_M_4000A4FE?.Despawn());
    }

    private void Run_Omega_M_4000A4FF()
    {
        for (int i = 0; i < 4; i++) {
            SimEnemy? omega_M_4000A4FF = null;
            var position = state.NewNorthB.Rotate(1 + i * 2).Apply(new Placement(new Vector3(0, 0, -13), 0));
            world.Events.Add(2.23f, () => omega_M_4000A4FF = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x10, BNpcBaseId: BNpcBaseId.OmegaMClone, NameId: BNpcNameId.OmegaM, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f), WeaponDrawn: true)));
            // world.Events.Add(3.24f, () => omega_M_4000A4FF?.SetVisible(false));
            world.Events.Add(10.07f, () => omega_M_4000A4FF?.SetPosition(position));
            world.Events.Add(16.17f, () => omega_M_4000A4FF?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(16.23f, () => omega_M_4000A4FF?.SetVisible(true));
            world.Events.Add(18.27f, () => TargetClone(omega_M_4000A4FF));
            world.Events.Add(31.86f, () => omega_M_4000A4FF?.Cast(Actions.EfficientBladework with { Timing = AttackLands(0.27f) }));
            world.Events.Add(36.50f, () => omega_M_4000A4FF?.PlayActionTimeline(TimelineId.WarpOut));
            // world.Events.Add(37.95f, () => omega_M_4000A4FF?.SetVisible(false));
            world.Events.Add(38.71f, () => omega_M_4000A4FF?.Despawn());
        }
    }

    private void Run_Optical_Unit_4000A3E7()
    {
        SimEnemy? optical_Unit_4000A3E7 = null;
        world.Events.Add(0f, () => optical_Unit_4000A3E7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OpticalUnit, NameId: BNpcNameId.OpticalUnit, Level: 90, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.150f, 0.000f, -0.600f), -0.000f))));
        world.Events.Add(7.93f, () => optical_Unit_4000A3E7?.SetPosition(state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, -45f), 0f))));
        world.Events.Add(20.76f, () => optical_Unit_4000A3E7?.Cast(OpticalLaser));
        
    }

    // Each player's Optimized Fire comes from its own helper; two of them also cast the legs side
    // rects, two the superfluid transformations and then the Spotlights.
    private void Run_Helpers(bool solo)
    {
        // The real helpers are renamed per mechanic (Omega-M, Omega-F) for the battle log; ours keep one name.
        world.Events.Add(0f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaBeetle, 8, "TopP2PartySynergy"));
        if (state.AttackF == OmegaAttack.Legs)
        {
            world.Events.Add(13.72f, () => helpers?.At(0, state.AttackDir.Apply(Geometry.LegsSideHelperPlacement)));
            world.Events.Add(13.72f, () => helpers?.At(1, state.AttackDir.Apply(Geometry.LegsSideHelperPlacement)));
            world.Events.Add(13.81f, () => CastLegsSide(0, Actions.SuperliminalSteelR, Geometry.LegsSideTargetR));
            world.Events.Add(13.81f, () => CastLegsSide(1, Actions.SuperliminalSteelL, Geometry.LegsSideTargetL));
        }
        world.Events.Add(15.37f, () => helpers?.At(2, new Placement(Vector3.Zero, 0f)));
        world.Events.Add(15.46f, () => CastTransformation(2, ActionId.SuperfluidAnimationM));
        world.Events.Add(15.86f, () => helpers?.At(3, state.NewNorthB.Apply(new Placement(new Vector3(0f, 0f, -13f), 0f))));
        world.Events.Add(15.95f, () => CastTransformation(3, ActionId.SuperfluidAnimationF));
        world.Events.Add(21.74f, CastOptimizedFire);
        world.Events.Add(23.82f, () => CastTransformation(3, ActionId.SuperfluidAnimationM));
        world.Events.Add(24.28f, () => CastTransformation(2, ActionId.SuperfluidAnimationF));
        // Solo has nobody to share a Spotlight with.
        world.Events.Add(33.20f, () => CastSpotlights(solo ? SoloSpotlight : Actions.Spotlight));
    }

    private void CastLegsSide(int index, EnemyAction steel, Vector3 target)
        => helpers?.At(index)?.Cast(steel with { Timing = AttackLands(0.25f) }, state.AttackDir.Apply(target));

    private void CastTransformation(int index, uint actionId)
    {
        if (helpers?.At(index) is not { } helper) return;
        helper.Cast(actionId, castSeconds: 0f, targetId: helper.GameObjectId, animationLock: 2.1f);
    }

    private void CastOptimizedFire()
    {
        for (var i = 0; i < 8; i++)
        {
            if (state.Order.Get(i) is not { } owner) continue;
            if ((owner.IsAlive() ? owner : party.Find.RandomMember(world.Rng)) is not { } target) continue;
            helpers?.At(i)?.Cast(Actions.OptimizedFireIII, target);
        }
    }

    private void CastSpotlights(EnemyAction spotlight)
    {
        for (var i = 0; i < 2; i++)
            if (state.Stacks.Get(i) is { } target)
                helpers?.At(2 + i)?.Cast(spotlight, target);
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
