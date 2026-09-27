using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Pointers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Network;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu.UltimatePredation;

public unsafe class UltimatePredationScenario : IScenario
{
    public string Name => "Ultimate Predation";
    public IPhase Phase => UwuZone.Ultima;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UltimatePredationAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private readonly UltimatePredationSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;

    // Lazy off `world`: RunInstanceEvents runs for a peer, which never calls Run.
    private UwuUtils? utilsInstance;
    private UwuUtils utils => utilsInstance ??= new UwuUtils(world);
    private UltimatePredationState state = null!;

    private SimEnemy? ultima;
    private SimEnemy? garuda;
    private SimEnemy? ifrit;

    private SimEnemy?[] dummies = new SimEnemy?[13];

    private SimEnemy? titan => state.ScenarioObjects.Titan;
    private Func<SimEnemy?>[] featherRainDummies => [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]];

    public void Run(SimWorld world, int? selectedAi)
    {
        this.world = world;
        party = world.Party;

        state = new(settingsWindow.Overrides);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UltimatePredationState>)AiStrats[idx]).Run(state, world);

        Init();
        Arena();
        Ultima();
        UltimaPost();
        Garuda();
        GarudaPost();
        Ifrit();
        IfritPost();
        Titan();
        TitanPost();
    }

    // UwuUtils.UpdateArena is a fixed-time native call with no dependency on this run's
    // randomized state -- same class as UmadP3BlackHoleScenario.RunInstanceEvents' replays. It
    // has no broadcast wiring of its own, so leaving it in Run() left it host-only: it never
    // revealed the arena for a peer, previously misdiagnosed as a native streaming race
    // (LayoutId resolves null on the HOST too, despite the host's arena rendering fine).
    public void RunInstanceEvents(SimWorld world)
    {
        this.world = world;
        ArenaReveal();
    }

    private void Init()
    {
        world.Events.Add(0, () =>
        {
            ultima = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.UltimaWeapon,
                    NameId: BNpcNameId.UltimaWeapon,
                    Level: 70,
                    Targetable: true,
                    EnemyList: EnemyListMode.Always,
                    IsVisible: true,
                    Placement: new Placement(
                        new Vector3(0, 0, -10),
                        0),
                    InitialModeAttributeFlags: 0x11)
                );

            garuda = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Garuda,
                    NameId: BNpcNameId.Garuda,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    IsVisible: false,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
                );

            ifrit = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Ifrit,
                    NameId: BNpcNameId.Ifrit,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    IsVisible: false,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
                );

            var titan = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Titan,
                    NameId: BNpcNameId.Titan,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    IsVisible: false,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

            utils.Awaken(ultima, true);
            utils.Awaken(garuda, false);
            utils.Awaken(ifrit, false);
            utils.Awaken(titan, false);

            state.ScenarioObjects.Titan = titan;

            for (int i = 0; i < dummies.Length; i++)
            {
                dummies[i] = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Dummy,
                    NameId: BNpcNameId.Dummy,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    IsVisible: true,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0)
                    )
                );
            }
        });
    }

    private void Arena()
    {
        world.Events.Add(0, () =>
        {
            var config = new EventObjectSpawnConfig
            {
                EObjId = EObjId.Arena,
                Placement = new(new(0.16f, 0, 1.4434f), 0),
                ObjectIndex = 1,
                TargetableStatus = 5,
                EntityId = 0x4000829C,
                LayoutId = EObjId.ArenaLayoutId,
                GimmickId = 7538258,
                TimelineState = 1
            };

            world.SpawnEventObject(config);
        });
    }

    private void ArenaReveal()
    {
        world.Events.Add(1, () => utils.UpdateArena(1));

        world.Events.Add(71.57f, () => utils.UpdateArena(2));
    }

    private void Ultima()
    {
        world.Events.Add(2.50f, () => ultima?.NativeCast(
            ActionId.UltimatePredation,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: ultima.GameObjectId
            ));

        world.Events.Add(5.45f, () => ultima?.NativeActionEffect(
            ActionId.UltimatePredation,
            4.5f,
            (ushort)ActionId.UltimatePredation,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(10.05f, () =>
        {
            ultima?.SetTargetable(false);
            ultima?.PlayActionTimeline(ActionTimelineId.WarpStart);
        });

        world.Events.Add(12.03f, () => ultima?.SetPosition(state.UltimaPlacement));

        world.Events.Add(12.28f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        IReadOnlyList<SimCharacter> ceruleumVentSnapshot = null!;
        world.Events.Add(22.28f, () =>
        {
            ultima?.NativeActionEffect(
                ActionId.CeruleumVent,
                2.1f,
                (ushort)ActionId.CeruleumVent,
                0,
                ActionType.Action,
                0,
                animationTargetId: ultima.GameObjectId
                );

            ceruleumVentSnapshot = party.Find.InsideActionAoe(ActionId.CeruleumVent, ultima!.Placement());
        });

        world.Events.Add(22.94f, () =>
        {
            foreach (var character in ceruleumVentSnapshot)
            {
                character.Die("Ceruleum Vent");
            }
        });

        world.Events.Add(25.50f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void UltimaPost()
    {
        var mt = party.Get(PartyRole.MainTank);
        var ot = party.Get(PartyRole.OffTank);

        world.Events.Add(27.45f, () => ultima?.SetPosition(
             new Placement(
                 new Vector3(0f, 0f, 0f),
                 0
             )));

        world.Events.Add(27.70f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(29.77f, () =>
        {
            ultima?.SetTargetable(true);
            ultima?.SetTarget(mt);
        });

        world.Events.Add(32.92f, () => ultima?.NativeCast(
            ActionId.PostUltimatePredation2,
            ActionType.Action,
            0f,
            1.7f,
            false,
            targetId: ultima.GameObjectId
            ));

        world.Events.Add(34.77f, () => ultima?.NativeActionEffect(
            ActionId.PostUltimatePredation2,
            2.1f,
            (ushort)ActionId.PostUltimatePredation2,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(43.94f, () => ultima?.NativeCast(
            ActionId.PostUltimatePredation3,
            ActionType.Action,
            0f,
            1.7f,
            false,
            targetId: ultima.GameObjectId
            ));

        world.Events.Add(45.90f, () => ultima?.NativeActionEffect(
            ActionId.PostUltimatePredation3,
            2.1f,
            (ushort)ActionId.PostUltimatePredation3,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(49.05f, () => ultima?.NativeCast(
            ActionId.RadiantPlumeUltima,
            ActionType.Action,
            0f,
            2.9f,
            false,
            targetId: ultima.GameObjectId
            ));

        for (int i = 0; i < Geometry.UltimaRadiantPlumePositions.Length; i++)
        {
            RadiantPlume(Geometry.UltimaRadiantPlumePositions[i], 5 + i);
        }

        world.Events.Add(52.20f, () => ultima?.NativeActionEffect(
            ActionId.RadiantPlumeUltima,
            2.1f,
            (ushort)ActionId.RadiantPlumeUltima,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(55.09f, () =>
        {
            var bait = party.GetRandom();
            ultima?.Face(bait);

            ultima?.NativeCast(
                ActionId.LandslideUltima,
                ActionType.Action,
                0f,
                1.9f,
                false,
                targetId: ultima.GameObjectId
                );
        });

        utils.LandslideLines(() => ultima, [() => dummies[5], () => dummies[6], () => dummies[7]], 55.09f, 57.25f, LandslideType.Ultima);

        world.Events.Add(57.25f, () => ultima?.NativeActionEffect(
            ActionId.LandslideUltima,
            2.1f,
            (ushort)ActionId.LandslideUltima,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(60.37f, () => ultima?.NativeCast(
            ActionId.PostUltimatePredation1,
            ActionType.Action,
            0f,
            1.7f,
            false,
            targetId: ultima.GameObjectId
            ));

        world.Events.Add(62.29f, () => ultima?.NativeActionEffect(
            ActionId.PostUltimatePredation1,
            2.1f,
            (ushort)ActionId.PostUltimatePredation1,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(64.38f, () => ultima?.NativeActionEffect(
            ActionId.ViscousAetheroplasmUltima,
            2.1f,
            (ushort)ActionId.ViscousAetheroplasmUltima,
            0,
            ActionType.Action,
            0,
            animationTargetId: mt!.GameObjectId
            ));

        world.Events.Add(65.34f, () => mt!.AddStatus(1532, 10));

        world.Events.Add(69, () =>
        {
            // TODO: Use a proper Enmity system for this
            world.Announce($"{Name}: Assuming Tank Swap");

            ultima?.SetTarget(ot);
        });

        world.Events.Add(73.64f, () => ultima?.NativeCast(
            ActionId.HomingLasers,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: mt!.GameObjectId
            ));

        // Viscous Aetheroplasm is handled by dummies[7]
        IReadOnlyList<SimCharacter> viscousAetheroplasm = null!;
        world.Events.Add(75.57f, () =>
        {
            dummies[7]?.NativeActionEffect(
                ActionId.ViscousAetheroplasmEffect,
                1.1f,
                (ushort)ActionId.ViscousAetheroplasmEffect,
                0,
                ActionType.Action,
                0,
                animationTargetId: mt!.GameObjectId
                );

            viscousAetheroplasm = party.Find.InsideActionAoe(ActionId.ViscousAetheroplasmEffect, new(mt!.Position, 0));
        });

        // TODO: Should do an invuln check
        world.Events.Add(76.07f, () => ResolveSnapshotTankbuster(viscousAetheroplasm, "Viscous Aetheroplasm"));

        IReadOnlyList<SimCharacter> homingLasersSnapshot = null!;
        world.Events.Add(76.34f, () => homingLasersSnapshot = party.Find.InsideActionAoe(ActionId.HomingLasers, new(mt!.Position, 0)));

        world.Events.Add(76.76f, () => ultima?.NativeActionEffect(
            ActionId.HomingLasers,
            2.1f,
            (ushort)ActionId.HomingLasers,
            0,
            ActionType.Action,
            0,
            animationTargetId: mt!.GameObjectId
            ));

        world.Events.Add(77.34f, () =>
        {
            // TODO: Use a proper Enmity system for this
            world.Announce($"{Name}: Assuming Tank Swap");

            ultima?.SetTarget(mt);
        });

        // TODO: Should do an invuln check
        world.Events.Add(79.22f, () => ResolveSnapshotTankbuster(homingLasersSnapshot, "Homing Lasers"));

        world.Events.Add(80.97f, () => ultima?.NativeCast(
            ActionId.UltimateAnnihilation,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: ultima.GameObjectId
            ));

        world.Events.Add(83.81f, () => ultima?.NativeActionEffect(
            ActionId.UltimateAnnihilation,
            4.5f,
            (ushort)ActionId.UltimateAnnihilation,
            0,
            ActionType.Action,
            0,
            animationTargetId: ultima.GameObjectId
            ));

        world.Events.Add(88.41f, () =>
        {
            ultima?.SetTargetable(false);
            ultima?.PlayActionTimeline(ActionTimelineId.WarpStart);
        });
    }

    private void Garuda()
    {
        world.Events.Add(12.03f, () => garuda?.SetPosition(state.GarudaPlacement));

        world.Events.Add(12.28f, () =>
        {
            garuda?.SetVisible(true);
            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(17.52f, () => garuda?.NativeCast(
            ActionId.WickedWheelAwaken,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: garuda.GameObjectId
            ));

        IReadOnlyList<SimCharacter> wickedWheelSnapshot = null!;
        world.Events.Add(20.22f, () => wickedWheelSnapshot = party.Find.InsideActionAoe(ActionId.WickedWheelAwaken, garuda!.Placement()));

        world.Events.Add(20.43f, () => garuda?.NativeActionEffect(
            ActionId.WickedWheelAwaken,
            2.8f,
            (ushort)ActionId.WickedWheelAwaken,
            0,
            ActionType.Action,
            0,
            animationTargetId: garuda.GameObjectId
            ));

        world.Events.Add(21.53f, () => utils.ResolveSnapshot(wickedWheelSnapshot, "Wicked Wheel"));

        // Wicked Tornado is handled by dummies[2]
        world.Events.Add(22.28f, () => dummies[2]?.SetPosition(garuda!.Placement()));

        IReadOnlyList<SimCharacter> wickedTornadoSnapshot = null!;
        world.Events.Add(22.48f, () =>
        {
            dummies[2]?.NativeActionEffect(
                ActionId.WickedTornado,
                2.1f,
                (ushort)ActionId.WickedTornado,
                0,
                ActionType.Action,
                0,
                animationTargetId: dummies[2]!.GameObjectId
                );

            wickedTornadoSnapshot = party.Find.InsideActionAoe(ActionId.WickedTornado, dummies[2]!.Placement(), size: 7); // 7 is ActionId.WickedWheelAwaken's EffectRange
        });

        world.Events.Add(22.98f, () => utils.ResolveSnapshot(wickedTornadoSnapshot, "Wicked Tornado"));

        world.Events.Add(25.25f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(featherRainDummies, 25.25f, 26.72f, 27.70f);
    }

    private void GarudaPost()
    {
        GarudaSister(new Placement(new Vector3(15, 0, 0), -90), BNpcNameId.Chirada);
        GarudaSister(new Placement(new Vector3(-15, 0, 0), 90), BNpcNameId.Suparna);

        world.Events.Add(64.38f, () =>
        {
            garuda?.SetPosition(
                new Placement(
                    new Vector3(0f, 0f, 0f),
                    0
                    ));

            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(68.61f, () => garuda?.NativeCast(
            ActionId.MistralShriek,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: garuda.GameObjectId
            ));

        world.Events.Add(71.57f, () => garuda?.NativeActionEffect(
            ActionId.MistralShriek,
            2.3f,
            (ushort)ActionId.MistralShriek,
            0,
            ActionType.Action,
            0,
            animationTargetId: garuda.GameObjectId
            ));

        // Technically these are for the Sisters. But adding it to their code section will duplicate the amount (10) that the actual fight uses (5), so it's kept here.
        utils.FeatherRain(featherRainDummies, 72.49f, 73.91f, 74.87f);

        // These are actually from Garuda
        world.Events.Add(76.04f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(featherRainDummies, 76.04f, 77.46f, 78.41f);
    }

    private void Ifrit()
    {
        world.Events.Add(12.03f, () => ifrit?.SetPosition(state.IfritPlacement));

        world.Events.Add(12.28f, () =>
        {
            ifrit?.SetVisible(true);
            ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(17.52f, () => ifrit?.NativeCast(
            ActionId.CrimsonCyclone,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: ifrit.GameObjectId
            ));

        IReadOnlyList<SimCharacter> crimsonCycloneSnapshot = null!;
        world.Events.Add(20.22f, () => crimsonCycloneSnapshot = party.Find.InsideActionAoe(ActionId.CrimsonCyclone, ifrit!.Placement()));

        world.Events.Add(20.43f, () => ifrit?.NativeActionEffect(
            ActionId.CrimsonCyclone,
            2.1f,
            (ushort)ActionId.CrimsonCyclone,
            0,
            ActionType.Action,
            0,
            animationTargetId: ifrit.GameObjectId
            ));

        world.Events.Add(20.69f, () => utils.ResolveSnapshot(crimsonCycloneSnapshot, "Crimson Cyclone"));

        CrimsonCycloneAwaken(Geometry.CrimsonCycloneAwakenPlacements[0], 11);
        CrimsonCycloneAwaken(Geometry.CrimsonCycloneAwakenPlacements[1], 12);
    }

    private void IfritPost()
    {
        var dpsRole = state.Rng.NextDpsRole();
        var dps = party.Get(dpsRole);

        var ot = party.Get(PartyRole.OffTank);

        world.Events.Add(36.74f, () => ifrit?.SetPosition(
             new Placement(
                 new Vector3(0f, 0f, -19.5f),
                 0
             )));

        world.Events.Add(36.99f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(39.03f, () => ifrit?.NativeCast(
            ActionId.EruptionIfrit,
            ActionType.Action,
            0f,
            2.2f,
            false,
            targetId: ifrit.GameObjectId
            ));

        IReadOnlyList<SimCharacter> eruptionBaits = null!;
        world.Events.Add(39.03f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[11], () => eruptionBaits[0], 39.03f, 42.05f);
        utils.EruptionPuddle(() => dummies[12], () => eruptionBaits[1], 39.03f, 42.05f);

        world.Events.Add(41.13f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[9], () => eruptionBaits[0], 41.13f, 43.94f);
        utils.EruptionPuddle(() => dummies[10], () => eruptionBaits[1], 41.13f, 43.94f);

        world.Events.Add(41.59f, () => ifrit?.NativeActionEffect(
            ActionId.EruptionIfrit,
            2.4f,
            (ushort)ActionId.EruptionIfrit,
            0,
            ActionType.Action,
            0,
            animationTargetId: ifrit.GameObjectId
            ));

        world.Events.Add(43.02f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[11], () => eruptionBaits[0], 43.02f, 46.15f);
        utils.EruptionPuddle(() => dummies[12], () => eruptionBaits[1], 43.02f, 46.15f);

        world.Events.Add(45.15f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[9], () => eruptionBaits[0], 45.15f, 48.13f);
        utils.EruptionPuddle(() => dummies[10], () => eruptionBaits[1], 45.15f, 48.13f);

        // TODO: Actual Infernal Fetters logic
        world.Events.Add(46.94f, () => world.Tether(dps, ot, TetherId.InfernalFetters, 21, StatusId.InfernalFetters));

        // Infernal Fetters VFX is handled by dummies[11] and dummies[12]
        world.Events.Add(47.19f, () =>
        {
            dummies[11]?.NativeActionEffect(
                ActionId.InfernalFetters,
                0.6f,
                (ushort)ActionId.InfernalFetters,
                0,
                ActionType.Action,
                0,
                animationTargetId: dps!.GameObjectId
                );

            dummies[12]?.NativeActionEffect(
                ActionId.InfernalFetters,
                0.6f,
                (ushort)ActionId.InfernalFetters,
                0,
                ActionType.Action,
                0,
                animationTargetId: ot!.GameObjectId
                );
        });

        world.Events.Add(49.05f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Titan()
    {
        world.Events.Add(12.03f, () => titan?.SetPosition(state.TitanPlacement));

        world.Events.Add(12.28f, () =>
        {
            titan?.SetVisible(true);
            titan?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(18.22f, () => titan?.NativeCast(
            ActionId.LandslideTitan,
            ActionType.Action,
            0f,
            1.9f,
            false,
            targetId: titan.GameObjectId
            ));

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 18.22f, 20.43f, LandslideType.Normal);

        world.Events.Add(20.43f, () => titan?.NativeActionEffect(
            ActionId.LandslideTitan,
            4.1f,
            (ushort)ActionId.LandslideTitan,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        utils.LandslideLines(() => titan, [() => dummies[3], () => dummies[4], () => dummies[5], () => dummies[6], () => dummies[7]], 20.43f, 22.48f, LandslideType.Awaken);

        world.Events.Add(25.50f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void TitanPost()
    {
        world.Events.Add(47.88f, () =>
        {
            var positions = new Placement[]
            {
                new(new(-13.7f, 0, -13.7f), Geometry.LookAtCenterRotation[DirectionEnum.NW]),
                new(new(13.7f, 0, -13.7f), Geometry.LookAtCenterRotation[DirectionEnum.NE]),
                new(new(13.7f, 0, 13.7f), Geometry.LookAtCenterRotation[DirectionEnum.SE]),
                new(new(-13.7f, 0, 13.7f), Geometry.LookAtCenterRotation[DirectionEnum.SW])
            };

            var ultimaPosition2 = new Vector2(ultima!.Position.X, ultima!.Position.Z);

            var furthest = positions
            .OrderByDescending(x => Vector2.DistanceSquared(x.Position2, ultimaPosition2))
            .First();

            titan?.SetPosition(furthest);
        });

        world.Events.Add(48.13f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(50.28f, () => titan?.NativeActionEffect(
            ActionId.BoulderTitan,
            2.1f,
            (ushort)ActionId.BoulderTitan,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        var boulderPositions = state.Rng.RandomStart(Geometry.TitanBoulderPositions.ToArray());

        BombBoulder(52.70f, 53.19f, 55.34f, 58.73f, 58.99f, 60.37f, boulderPositions[0]);
        BombBoulder(54.65f, 55.34f, 57.25f, 60.82f, 61.04f, 62.29f, boulderPositions[1]);
        BombBoulder(56.75f, 57.25f, 59.24f, 62.74f, 62.99f, 64.17f, boulderPositions[2]);
        BombBoulder(58.73f, 59.24f, 61.29f, 64.89f, 65.14f, 66.39f, boulderPositions[3]);
        BombBoulder(60.62f, 61.29f, 63.24f, 66.87f, 67.11f, 66.39f, boulderPositions[4]);
        BombBoulder(62.74f, 63.24f, 65.39f, 68.86f, 69.11f, 70.26f, boulderPositions[5]);

        world.Events.Add(54.45f, () =>
        {
            var bait = party.GetRandom();
            titan?.Face(bait);

            titan?.NativeCast(
                ActionId.LandslideTitan,
                ActionType.Action,
                0f,
                1.9f,
                false,
                targetId: titan.GameObjectId
                );
        });

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 54.45f, 56.50f, LandslideType.Normal);

        world.Events.Add(56.50f, () => titan?.NativeActionEffect(
            ActionId.LandslideTitan,
            4.1f,
            (ushort)ActionId.LandslideTitan,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        utils.LandslideLines(() => titan, [() => dummies[0], () => dummies[1], () => dummies[2], () => dummies[3], () => dummies[4]], 56.50f, 58.49f, LandslideType.Awaken);

        world.Events.Add(61.79f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(62.79f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(63.91f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(65.14f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(66.14f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(67.27f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(68.36f, () => titan?.NativeActionEffect(
            ActionId.Tumult,
            1.1f,
            (ushort)ActionId.Tumult,
            0,
            ActionType.Action,
            0,
            animationTargetId: titan.GameObjectId
            ));

        world.Events.Add(69.61f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void RadiantPlume(Vector3 position, int dummyIndex)
    {
        world.Events.Add(49.05f, () => dummies[dummyIndex]?.NativeCast(
            ActionId.RadiantPlumePuddle,
            ActionType.Action,
            0f,
            3.7f,
            false,
            rotation: float.Pi,
            position: position
            ));

        IReadOnlyList<SimCharacter> radiantPlumeSnapshot = null!;
        world.Events.Add(52.75f, () => radiantPlumeSnapshot = party.Find.InsideActionAoe(ActionId.RadiantPlumePuddle, new(position, 0)));

        world.Events.Add(52.95f, () => dummies[dummyIndex]?.NativeActionEffect(
            ActionId.RadiantPlumePuddle,
            0.1f,
            (ushort)ActionId.RadiantPlumePuddle,
            0,
            ActionType.Action,
            0,
            position: position
            ));

        world.Events.Add(53.61f, () => utils.ResolveSnapshot(radiantPlumeSnapshot, "Radiant Plume"));
    }

    private void GarudaSister(Placement placement, uint nameId)
    {
        SimEnemy? sister = null;

        world.Events.Add(64.38f, () =>
        {
            sister = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SuparnaChirada,
                    NameId: nameId,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    IsVisible: true,
                    Placement: placement)
            );

            sister?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(66.64f, () => sister?.NativeCast(
            ActionId.WickedWheel,
            ActionType.Action,
            0f,
            2.7f,
            false,
            targetId: sister.GameObjectId
            ));

        IReadOnlyList<SimCharacter> wickedWheelSnapshot = null!;
        world.Events.Add(69.34f, () => wickedWheelSnapshot = party.Find.InsideActionAoe(ActionId.WickedWheel, placement));

        world.Events.Add(69.61f, () => sister?.NativeActionEffect(
            ActionId.WickedWheel,
            2.8f,
            (ushort)ActionId.WickedWheel,
            0,
            ActionType.Action,
            0,
            animationTargetId: sister.GameObjectId
            ));

        world.Events.Add(70.71f, () => utils.ResolveSnapshot(wickedWheelSnapshot, "Wicked Wheel"));

        world.Events.Add(72.49f, () => sister?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        world.Events.Add(74.37f, () => sister?.SetPosition(
             new Placement(
                 new Vector3(0f, 0f, -19.5f),
                 0
             )));
    }

    private void CrimsonCycloneAwaken(Placement placement, int dummyIndex)
    {
        IReadOnlyList<SimCharacter> crimsonCycloneAwakenSnapshot = null!;
        world.Events.Add(22.48f, () =>
        {
            var dummy = dummies[dummyIndex];

            dummy?.SetPosition(placement);

            dummy?.NativeActionEffect(
                ActionId.CrimsonCycloneAwaken,
                2.1f,
                (ushort)ActionId.CrimsonCycloneAwaken,
                0,
                ActionType.Action,
                0,
                animationTargetId: dummy.GameObjectId
                );

            crimsonCycloneAwakenSnapshot = party.Find.InsideActionAoe(ActionId.CrimsonCycloneAwaken, dummy!.Placement());
        });

        world.Events.Add(22.95f, () => utils.ResolveSnapshot(crimsonCycloneAwakenSnapshot, "Crimson Cyclone (Awaken)"));
    }

    private void BombBoulder(float spawnOffset, float buryOffset, float castOffset, float effectOffset, float fadeOffset, float despawnOffset, Vector3 position)
    {
        SimEnemy? boulder = null;

        world.Events.Add(spawnOffset, () => boulder = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.BombBoulder,
                NameId: BNpcNameId.BombBoulder,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: false,
                Placement: new(position, 0)
                )
            )
        );

        IReadOnlyList<SimCharacter> burySnapshot = null!;
        world.Events.Add(buryOffset, () =>
        {
            boulder?.SetVisible(true);

            boulder?.NativeActionEffect(
            ActionId.Bury,
            0.6f,
            (ushort)ActionId.Bury,
            0,
            ActionType.Action,
            0,
            animationTargetId: boulder.GameObjectId
            );

            burySnapshot = party.Find.InsideActionAoe(ActionId.Bury, boulder!.Placement());
        });

        world.Events.Add(buryOffset + 0.53f, () => utils.ResolveSnapshot(burySnapshot, "Bury"));

        world.Events.Add(castOffset, () => boulder?.NativeCast(
            ActionId.Burst,
            ActionType.Action,
            0f,
            3.2f,
            false,
            targetId: boulder.GameObjectId
            )
        );

        IReadOnlyList<SimCharacter> burstSnapshot = null!;
        world.Events.Add(castOffset + 3.2f, () => burstSnapshot = party.Find.InsideActionAoe(ActionId.Burst, boulder!.Placement()));

        world.Events.Add(effectOffset, () => boulder?.NativeActionEffect(
            ActionId.Burst,
            2.1f,
            (ushort)ActionId.Burst,
            0,
            ActionType.Action,
            0,
            animationTargetId: boulder.GameObjectId
            )
        );

        world.Events.Add(effectOffset + 0.16f, () => utils.ResolveSnapshot(burstSnapshot, "Burst"));

        world.Events.Add(fadeOffset, () => PacketDispatcher.HandleActorControlPacket(boulder!.EntityId, 607, boulder.EntityId, 1, 0, 100, 0, 0, 0, 0, 0xE0000000, false));
        world.Events.Add(despawnOffset, () => boulder?.Despawn());
    }

    private void ResolveSnapshotTankbuster(IReadOnlyList<SimCharacter> snapshot, string dieCause)
    {
        foreach (var character in snapshot)
        {
            if (character is not ISimPartyMember { Role: PartyRole.MainTank or PartyRole.OffTank })
            {
                character.Die($"{dieCause} (Tank Buster)");
            }
        }
    }
}
