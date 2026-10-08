using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using Actions = AnoMech.Scenarios.Uwu.UwuActions;

namespace AnoMech.Scenarios.Uwu.UltimatePredation;

public class UltimatePredationScenario : IMultiplayerReplayable
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

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public UltimatePredationState? LastState { get; private set; }

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

        state = new(world.Rng, settingsWindow.Overrides);
        LastState = state;
        // Unconditional, before the optional bot-run below -- a debug-bot peer needs these
        // resolved even when the host runs no bots itself (real players, selectedAi null).
        // Idempotent against AiStrats[idx].Run also calling into the same resolution below.
        new UltimatePredationAi().ResolveSafeSpots(state);

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
        world.Events.Add(2.50f, () => ultima?.Cast(ActionId.UltimatePredation, animationLock: 4.5f));

        world.Events.Add(10.05f, () =>
        {
            ultima?.SetTargetable(false);
            ultima?.PlayActionTimeline(ActionTimelineId.WarpStart);
        });

        world.Events.Add(12.03f, () => ultima?.SetPosition(state.UltimaPlacement));

        world.Events.Add(12.28f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(22.28f, () => ultima?.Cast(Actions.CeruleumVent));

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

        world.Events.Add(29.67f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 0));

        world.Events.Add(29.77f, () =>
        {
            ultima?.SetTargetable(true);
            ultima?.SetTarget(mt);
        });

        world.Events.Add(32.70f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 2));

        world.Events.Add(32.92f, () => ultima?.Cast(ActionId.PostUltimatePredation2, animationLock: 2.1f));

        world.Events.Add(37.73f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 2));

        world.Events.Add(40.75f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 0));

        world.Events.Add(43.80f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 0));

        world.Events.Add(43.94f, () => ultima?.Cast(ActionId.PostUltimatePredation3, animationLock: 2.1f));

        world.Events.Add(48.83f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 1));

        world.Events.Add(49.05f, () => ultima?.Cast(ActionId.RadiantPlumeUltima, animationLock: 2.1f));

        for (int i = 0; i < Geometry.UltimaRadiantPlumePositions.Length; i++)
        {
            RadiantPlume(Geometry.UltimaRadiantPlumePositions[i], 5 + i);
        }

        world.Events.Add(55.09f, () =>
        {
            var bait = state.UltimaLandslideBait is { } role ? party.Get(role) : party.GetRandom(world.Rng);
            ultima?.Face(bait);
            ultima?.Cast(ActionId.LandslideUltima, animationLock: 2.1f);
        });

        utils.LandslideLines(() => ultima, [() => dummies[5], () => dummies[6], () => dummies[7]], 55.09f, LandslideType.Ultima);

        world.Events.Add(58.33f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 0));

        world.Events.Add(60.37f, () => ultima?.Cast(ActionId.PostUltimatePredation1, animationLock: 2.1f));

        world.Events.Add(63.37f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 2));

        world.Events.Add(64.38f, () => state.ViscousAetheroplasmUltima = ultima?.Cast(Actions.ViscousAetheroplasmUltima, mt));

        world.Events.Add(66.40f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 2));

        world.Events.Add(69, () =>
        {
            // TODO: Use a proper Enmity system for this
            world.Announce($"{Name}: Assuming Tank Swap");

            ultima?.SetTarget(ot);
        });

        world.Events.Add(69.48f, () => ultima?.Cast(Actions.UltimaAttack, ot, animationVariation: 0));

        world.Events.Add(72.47f, () => ultima?.Cast(Actions.UltimaAttack, ot, animationVariation: 0));

        world.Events.Add(73.64f, () => ultima?.Cast(Actions.HomingLasers, mt));

        world.Events.Add(75.57f, () =>
        {
            var targets = (state.ViscousAetheroplasmUltima?.Hits ?? []).Select(h => h.Who).Where(w => w.IsAlive());
            foreach (var (who, dummy) in targets.Zip(dummies.Reverse()))
                dummy?.Cast(Actions.ViscousAetheroplasm, who);
        });

        world.Events.Add(77.34f, () =>
        {
            // TODO: Use a proper Enmity system for this
            world.Announce($"{Name}: Assuming Tank Swap");

            ultima?.SetTarget(mt);
        });

        world.Events.Add(78.55f, () => ultima?.Cast(Actions.UltimaAttack, mt, animationVariation: 2));

        world.Events.Add(80.97f, () =>
        {
            ultima?.Follow();
            ultima?.Cast(ActionId.UltimateAnnihilation, animationLock: 4.5f);
        });

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
            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(17.52f, () => garuda?.Cast(Actions.WickedWheelAwaken));

        // Wicked Tornado is handled by dummies[2]
        world.Events.Add(22.28f, () => dummies[2]?.SetPosition(garuda!.Placement()));

        world.Events.Add(22.48f, () => dummies[2]?.Cast(Actions.WickedTornado));

        world.Events.Add(25.25f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(featherRainDummies, 25.25f, 26.72f, state.FeatherRainTargets);
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

        world.Events.Add(68.61f, () => garuda?.Cast(Actions.MistralShriek));

        // Technically these are for the Sisters. But adding it to their code section will duplicate the amount (10) that the actual fight uses (5), so it's kept here.
        utils.FeatherRain(featherRainDummies, 72.49f, 73.91f, state.FeatherRainTargets);

        // These are actually from Garuda
        world.Events.Add(76.04f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(featherRainDummies, 76.04f, 77.46f, state.FeatherRainTargets);
    }

    private void Ifrit()
    {
        world.Events.Add(12.03f, () => ifrit?.SetPosition(state.IfritPlacement));

        world.Events.Add(12.28f, () =>
        {
            ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(17.52f, () => ifrit?.Cast(Actions.CrimsonCyclone));

        CrimsonCycloneAwaken(Geometry.CrimsonCycloneAwakenPlacements[0], 11);
        CrimsonCycloneAwaken(Geometry.CrimsonCycloneAwakenPlacements[1], 12);
    }

    private void IfritPost()
    {
        var dps = party.Get(state.InfernalFettersDps);

        var ot = party.Get(PartyRole.OffTank);

        world.Events.Add(36.74f, () => ifrit?.SetPosition(
             new Placement(
                 new Vector3(0f, 0f, -19.5f),
                 0
             )));

        world.Events.Add(36.99f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(39.03f, () => ifrit?.Cast(ActionId.EruptionIfrit, animationLock: 2.4f));

        IReadOnlyList<SimCharacter> eruptionBaits = null!;
        world.Events.Add(39.03f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[11], () => eruptionBaits[0], 39.03f);
        utils.EruptionPuddle(() => dummies[12], () => eruptionBaits[1], 39.03f);

        world.Events.Add(41.13f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[9], () => eruptionBaits[0], 41.13f);
        utils.EruptionPuddle(() => dummies[10], () => eruptionBaits[1], 41.13f);

        world.Events.Add(43.02f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[11], () => eruptionBaits[0], 43.02f);
        utils.EruptionPuddle(() => dummies[12], () => eruptionBaits[1], 43.02f);

        world.Events.Add(45.15f, () => eruptionBaits = party.Find.FarestN(ifrit!.Position, 2));
        utils.EruptionPuddle(() => dummies[9], () => eruptionBaits[0], 45.15f);
        utils.EruptionPuddle(() => dummies[10], () => eruptionBaits[1], 45.15f);

        // TODO: Actual Infernal Fetters logic
        world.Events.Add(46.94f, () => world.Tether(dps, ot, TetherId.InfernalFetters, 21, StatusId.InfernalFetters));

        // Infernal Fetters VFX is handled by dummies[11] and dummies[12]
        world.Events.Add(47.19f, () =>
        {
            dummies[11]?.Cast(ActionId.InfernalFetters, dps, 0.6f);
            dummies[12]?.Cast(ActionId.InfernalFetters, ot, 0.6f);
        });

        world.Events.Add(49.05f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Titan()
    {
        world.Events.Add(12.03f, () => titan?.SetPosition(state.TitanPlacement));

        world.Events.Add(12.28f, () =>
        {
            titan?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(18.22f, () => titan?.Cast(ActionId.LandslideTitan, animationLock: 4.1f));

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 18.22f, LandslideType.Normal);

        utils.LandslideLines(() => titan, [() => dummies[3], () => dummies[4], () => dummies[5], () => dummies[6], () => dummies[7]], 20.43f, LandslideType.Awaken);

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

        world.Events.Add(50.28f, () => titan?.Cast(ActionId.BoulderTitan, animationLock: 2.1f));

        var boulderPositions = state.BoulderPositions;

        BombBoulder(52.70f, 53.19f, 55.34f, 58.99f, 60.37f, boulderPositions[0]);
        BombBoulder(54.65f, 55.34f, 57.25f, 61.04f, 62.29f, boulderPositions[1]);
        BombBoulder(56.75f, 57.25f, 59.24f, 62.99f, 64.17f, boulderPositions[2]);
        BombBoulder(58.73f, 59.24f, 61.29f, 65.14f, 66.39f, boulderPositions[3]);
        BombBoulder(60.62f, 61.29f, 63.24f, 67.11f, 68.39f, boulderPositions[4]);
        BombBoulder(62.74f, 63.24f, 65.39f, 69.11f, 70.26f, boulderPositions[5]);

        world.Events.Add(54.45f, () =>
        {
            var bait = state.TitanLandslideBait is { } role ? party.Get(role) : party.GetRandom(world.Rng);
            titan?.Face(bait);
            titan?.Cast(ActionId.LandslideTitan, animationLock: 4.1f);
        });

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 54.45f, LandslideType.Normal);

        utils.LandslideLines(() => titan, [() => dummies[0], () => dummies[1], () => dummies[2], () => dummies[3], () => dummies[4]], 56.50f, LandslideType.Awaken);

        world.Events.Add(61.79f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(62.79f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(63.91f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(65.14f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(66.14f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(67.27f, () => titan?.Cast(Actions.Tumult));
        world.Events.Add(68.36f, () => titan?.Cast(Actions.Tumult));

        world.Events.Add(69.61f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void RadiantPlume(Vector3 position, int dummyIndex)
        => world.Events.Add(49.05f, () => dummies[dummyIndex]?.Cast(Actions.RadiantPlumePuddle, position));

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

        world.Events.Add(66.64f, () => sister?.Cast(Actions.WickedWheel));

        world.Events.Add(72.49f, () => sister?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        world.Events.Add(74.37f, () => sister?.SetPosition(
             new Placement(
                 new Vector3(0f, 0f, -19.5f),
                 0
             )));
    }

    private void CrimsonCycloneAwaken(Placement placement, int dummyIndex)
    {
        world.Events.Add(22.48f, () =>
        {
            var dummy = dummies[dummyIndex];
            dummy?.SetPosition(placement);
            dummy?.Cast(Actions.CrimsonCycloneAwaken);
        });
    }

    private void BombBoulder(float spawnOffset, float buryOffset, float castOffset, float fadeOffset, float despawnOffset, Vector3 position)
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

        world.Events.Add(buryOffset, () =>
        {
            boulder?.Cast(Actions.Bury);
        });

        world.Events.Add(castOffset, () => boulder?.Cast(Actions.Burst));

        world.Events.Add(fadeOffset, () => boulder!.ActorControl.Send(607, boulder.EntityId, 1, 0, 100));
        world.Events.Add(despawnOffset, () => boulder?.Despawn());
    }

    public MpMessage? BuildReplayStateMessage()
    {
        if (LastState is not { } s) return null;
        // ResolveSafeSpots (called unconditionally in Run, before this can ever be polled)
        // guarantees these three are set.
        if (s.ResolvedSafeCardinal is not { } safeCardinal
            || s.ResolvedSafeFirstSet is not { } safeFirstSet
            || s.ResolvedSafeSecondSet is not { } safeSecondSet) return null;
        return new UltimatePredationAiReplayStateMessage(
            s.GarudaPlacement.Position.X, s.GarudaPlacement.Position.Y, s.GarudaPlacement.Position.Z, s.GarudaPlacement.Rotation,
            s.TitanPlacement.Position.X, s.TitanPlacement.Position.Y, s.TitanPlacement.Position.Z, s.TitanPlacement.Rotation,
            s.IfritPlacement.Position.X, s.IfritPlacement.Position.Y, s.IfritPlacement.Position.Z, s.IfritPlacement.Rotation,
            s.UltimaPlacement.Position.X, s.UltimaPlacement.Position.Y, s.UltimaPlacement.Position.Z, s.UltimaPlacement.Rotation,
            safeCardinal.Position.X, safeCardinal.Position.Y, safeCardinal.Position.Z, safeCardinal.Rotation,
            safeFirstSet.Position.X, safeFirstSet.Position.Y, safeFirstSet.Position.Z, safeFirstSet.Rotation,
            safeSecondSet.Position.X, safeSecondSet.Position.Y, safeSecondSet.Position.Z, safeSecondSet.Rotation);
    }

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UltimatePredationAiReplayStateMessage msg) return null;
        var shadowState = UltimatePredationState.FromNetworkReplay(
            new Placement(new Vector3(msg.GarudaX, msg.GarudaY, msg.GarudaZ), msg.GarudaRotation),
            new Placement(new Vector3(msg.TitanX, msg.TitanY, msg.TitanZ), msg.TitanRotation),
            new Placement(new Vector3(msg.IfritX, msg.IfritY, msg.IfritZ), msg.IfritRotation),
            new Placement(new Vector3(msg.UltimaX, msg.UltimaY, msg.UltimaZ), msg.UltimaRotation));
        shadowState.ResolvedSafeCardinal = new Placement(new Vector3(msg.SafeCardinalX, msg.SafeCardinalY, msg.SafeCardinalZ), msg.SafeCardinalRotation);
        shadowState.ResolvedSafeFirstSet = new Placement(new Vector3(msg.SafeFirstSetX, msg.SafeFirstSetY, msg.SafeFirstSetZ), msg.SafeFirstSetRotation);
        shadowState.ResolvedSafeSecondSet = new Placement(new Vector3(msg.SafeSecondSetX, msg.SafeSecondSetY, msg.SafeSecondSetZ), msg.SafeSecondSetRotation);
        ((IScenarioAi<UltimatePredationState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not UltimatePredationState shadowState) return;
        shadowState.ScenarioObjects.Titan ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.Titan);
    }
}
