using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using AnoMech.Core.Native.Interfaces;
using Actions = AnoMech.Scenarios.Uwu.UwuActions;

namespace AnoMech.Scenarios.Uwu.UltimateSuppression;

public class UltimateSuppressionScenario : IMultiplayerReplayable
{
    public string Name => "Ultimate Suppression";
    public IPhase Phase => UwuZone.Ultima;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UltimateSuppressionAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private readonly UltimateSuppressionSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;

    // Lazy off `world`: RunInstanceEvents runs for a peer, which never calls Run.
    private UwuUtils? utilsInstance;
    private UwuUtils utils => utilsInstance ??= new UwuUtils(world);
    private UltimateSuppressionState state = null!;

    // Polled by the multiplayer host; null until Run has assigned roles.
    public UltimateSuppressionState? LastState { get; private set; }

    private SimEnemy? ultima;
    private SimEnemy? garuda;
    private SimEnemy? ifrit;
    private SimEnemy? titan;

    private SimEnemy? chirada;
    private SimEnemy? suparna;

    private SimEnemy?[] dummies = new SimEnemy?[13];

    // Seconds into each plume movement; null while it isn't running.
    private float? razorPlumesRotate;
    private float? razorPlumesBack;
    private Dictionary<SimEnemy, Placement> razorPlumes = new();
    private bool razorPlumesDamage;
    private readonly HashSet<SimEnemy> razorPlumesFired = [];

    // The arena reveal is fixed-time and independent of this run's randomization, so it belongs
    // here: a peer never runs Run and would stay in the void.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        world = instanceWorld;
        world.Events.Add(1, () =>
        {
            utils.UpdateArena(1);
            utils.UpdateArena(2);
        });
        world.Events.Add(24.70f, () => utils.UpdateArena(4));
    }

    public void Run(SimWorld world, int? selectedAi)
    {
        this.world = world;
        party = world.Party;

        state = new(world.Rng, party, settingsWindow.Overrides);
        LastState = state;

        razorPlumesDamage = false;
        razorPlumesFired.Clear();
        razorPlumesRotate = null;
        razorPlumesBack = null;
        DespawnRazorPlumes();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UltimateSuppressionState>)AiStrats[idx]).Run(state, world);

        Init();
        Arena();
        Ultima();
        Garuda();
        Ifrit();
        Titan();
    }

    public void Tick(float delta, float elapsed)
    {
        if (razorPlumesRotate is { } rotateElapsed)
        {
            razorPlumesRotate = rotateElapsed + delta;
            var fraction = float.Min(razorPlumesRotate.Value / Duration.RazorPlumeRotation, 1);
            var angle = fraction * Geometry.RazorPlumeRotation;

            foreach (var (razorPlume, placement) in razorPlumes)
            {
                var rotatedPlacement = placement.RotateAroundOrigin(angle);
                razorPlume!.SetPosition(rotatedPlacement);
            }
        }
        else if (razorPlumesBack is { } backElapsed)
        {
            razorPlumesBack = backElapsed + delta;
            var fraction = float.Min(razorPlumesBack.Value / Duration.RazorPlumeBack, 1);
            var distance = fraction * Geometry.RazorPlumeBackDistance;

            foreach (var (razorPlume, placement) in razorPlumes)
            {
                var movedPlacement = placement.MoveForward(-distance);
                razorPlume!.SetPosition(movedPlacement.Position);
            }
        }

        if (razorPlumesDamage)
        {
            foreach (var (razorPlume, _) in razorPlumes)
            {
                if (party.ActiveMembers().Any(m => Touches(razorPlume, m)))
                    FireFeatherlance(razorPlume);
            }
        }
    }

    private static bool Touches(SimCharacter a, SimCharacter b)
    {
        var reach = a.HitboxRadius + b.HitboxRadius;
        return a.Placement().DistanceSq(b.Position) <= reach * reach;
    }

    private void FireFeatherlance(SimEnemy razorPlume)
    {
        if (razorPlumesFired.Add(razorPlume))
            razorPlume.Cast(Actions.Featherlance);
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
                    Visibility: SpawnVisibility.Visible,
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
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
                );

            garuda?.AddStatusParam(StatusId.Woken, 0);

            ifrit = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Ifrit,
                    NameId: BNpcNameId.Ifrit,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
                );

            ifrit?.AddStatusParam(StatusId.Woken, 0);

            titan = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Titan,
                    NameId: BNpcNameId.Titan,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

            titan?.AddStatusParam(StatusId.Woken, 0);

            utils.Awaken(ultima!, true);
            utils.Awaken(garuda!, false);
            utils.Awaken(ifrit!, false);
            utils.Awaken(titan!, false);

            chirada = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SuparnaChirada,
                    NameId: BNpcNameId.Chirada,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

            suparna = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SuparnaChirada,
                    NameId: BNpcNameId.Suparna,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

            for (int i = 0; i < dummies.Length; i++)
            {
                dummies[i] = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Dummy,
                    NameId: BNpcNameId.Dummy,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0)
                    )
                );
            }

            var mt = party.Get(PartyRole.MainTank);
            var healer = party.Get(state.ThermalLowHealer);

            mt?.AddStatus(StatusId.ThermalLow);
            healer?.AddStatus(StatusId.ThermalLow);
        });
    }

    private void Arena()
    {
        world.Events.Add(0, () =>
        {
            var config = new EventObjectSpawnConfig
            {
                EObjId = 2007457,
                Placement = new(new(0.16f, 0, 1.4434f), 0),
                ObjectIndex = 1,
                TargetableStatus = 5,
                EntityId = 0x4000829C,
                LayoutId = 7538913,
                GimmickId = 7538258,
                TimelineState = 1
            };

            world.SpawnEventObject(config);
        });

    }

    private void Ultima()
    {
        world.Events.Add(2.50f, () => ultima?.Cast(ActionId.UltimateSuppression, animationLock: 4.5f));

        world.Events.Add(10.13f, () =>
        {
            ultima?.SetTargetable(false);
            ultima?.PlayActionTimeline(ActionTimelineId.WarpStart);
        });

        world.Events.Add(12.15f, () => ultima?.SetPosition(
             new Placement(
                 new Vector3(13.7f, 0f, -13.7f),
                 float.DegreesToRadians(-45f)
             )));

        world.Events.Add(12.37f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(20.55f, () => ultima?.Cast(ActionId.LightPillarUltima, animationLock: 2.1f));

        AetherochemicalLaser(0, 24.70f);

        LightPillar(() => dummies[5], 24.70f, true);
        LightPillar(() => dummies[4], 25.64f);
        LightPillar(() => dummies[10], 26.60f);
        LightPillar(() => dummies[9], 27.55f);

        AetherochemicalLaser(1, 28.71f);

        LightPillar(() => dummies[10], 28.71f);
        LightPillar(() => dummies[12], 29.68f);

        AetherochemicalLaser(2, 32.84f);

        world.Events.Add(40.03f, () => ultima?.Cast(Actions.TankPurge));

        world.Events.Add(46.10f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Garuda()
    {
        world.Events.Add(12.15f, () =>
        {
            garuda?.SetPosition(
                new Placement(
                    new Vector3(-13.7f, 0f, -13.7f),
                    float.DegreesToRadians(45f)
                ));

            chirada?.SetPosition(
                new Placement(
                    new Vector3(6f, 0f, 6f),
                    float.DegreesToRadians(-135f)
                ));

            suparna?.SetPosition(
                new Placement(
                    new Vector3(-6f, 0f, -6f),
                    float.DegreesToRadians(45f)
                ));
        });

        world.Events.Add(12.37f, () =>
        {
            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            chirada?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            suparna?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(14.50f, () => garuda?.PlayActionTimeline(TimelineId.RazorPlume));

        world.Events.Add(15.48f, () =>
        {
            state.PlayerMistralSongs[0]?.AttachLockonVfx(LockonId.MistralSong);
            state.PlayerMistralSongs[1]?.AttachLockonVfx(LockonId.MistralSong);
        });

        world.Events.Add(17.95f, () =>
        {
            var placement1 = new Placement(new(12, 0, 12), Geometry.LookAtCenterRotation[DirectionEnum.SE]);
            razorPlumes.Add(RazorPlume(placement1)!, placement1);

            var placement2 = new Placement(new(-12, 0, 12), Geometry.LookAtCenterRotation[DirectionEnum.SW]);
            razorPlumes.Add(RazorPlume(placement2)!, placement2);

            var placement3 = new Placement(new(12, 0, -12), Geometry.LookAtCenterRotation[DirectionEnum.NE]);
            razorPlumes.Add(RazorPlume(placement3)!, placement3);

            var placement4 = new Placement(new(-12, 0, -12), Geometry.LookAtCenterRotation[DirectionEnum.NW]);
            razorPlumes.Add(RazorPlume(placement4)!, placement4);

            razorPlumesDamage = true;
        });

        world.Events.Add(20.55f, () =>
        {
            state.ChiradaMistralSong = chirada?.Cast(Actions.MistralSongSuparnaChirada, state.PlayerMistralSongs[0]);
            state.SuparnaMistralSong = suparna?.Cast(Actions.MistralSongSuparnaChirada, state.PlayerMistralSongs[1]);
        });

        world.Events.Add(20.60f, () =>
        {
            garuda?.Face(state.GarudaFacing is { } role ? party.Get(role) : party.GetRandom(world.Rng));
            garuda?.Cast(Actions.MistralSong);
            razorPlumesRotate = 0f;
        });

        world.Events.Add(20.60f + Duration.RazorPlumeRotation, () =>
        {
            razorPlumesRotate = null;

            foreach (var key in razorPlumes.Keys)
            {
                razorPlumes[key] = key.Placement();
            }

            razorPlumesBack = 0f;
        });

        world.Events.Add(20.60f + Duration.RazorPlumeRotation + Duration.RazorPlumeBack, () => razorPlumesBack = null);

        world.Events.Add(22.57f, () =>
        {
            chirada?.PlayActionTimeline(ActionTimelineId.WarpStart2);
            suparna?.PlayActionTimeline(ActionTimelineId.WarpStart2);
        });

        utils.FeatherRain([() => dummies[6], () => dummies[7], () => dummies[8], () => dummies[9], () => dummies[10]], 22.57f, 24.11f, state.FeatherRainTargets);

        world.Events.Add(23.57f, () =>
        {
            if (state.ChiradaMistralSong?.Hits is [var chiradaHit, ..]) dummies[11]?.Cast(Actions.GreatWhirlwind, chiradaHit.At);
            if (state.SuparnaMistralSong?.Hits is [var suparnaHit, ..]) dummies[12]?.Cast(Actions.GreatWhirlwind, suparnaHit.At);
        });

        world.Events.Add(24.70f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain([() => dummies[0], () => dummies[1], () => dummies[2], () => dummies[3], () => dummies[5]], 24.70f, 26.10f, state.FeatherRainTargets);

        world.Events.Add(30.67f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(32.84f, () => state.MesohighTether = world.Tether(garuda, End.Passable(), TetherId.Mesohigh));

        world.Events.Add(37.92f, () =>
        {
            state.MesohighTether!.Resolved = true;
            var holder = state.MesohighTether.B;
            garuda?.Cast(Actions.Mesohigh, holder);
            state.MesohighTether.Despawn();

            // After the cast: Mesohigh checks Thermal Low as it resolves, inside Cast().
            if (holder?.FindStatus(StatusId.ThermalLow) is { Stacks: var thermalLowStacks and > 0 })
            {
                holder.RemoveStatus(StatusId.ThermalLow);
                state.MesohighThermalLowStacks = thermalLowStacks;
            }
        });

        world.Events.Add(38.99f, () =>
        {
            if (state.MesohighThermalLowStacks > 0)
                garuda?.Cast(Actions.SuperCyclone(state.MesohighThermalLowStacks));
        });

        world.Events.Add(40.88f, () =>
        {
            foreach (var (razorPlume, _) in razorPlumes)
                FireFeatherlance(razorPlume);
        });

        world.Events.Add(41.20f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain([() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 41.20f, 42.47f, state.FeatherRainTargets);

        world.Events.Add(42.97f, () => razorPlumesDamage = false);

        world.Events.Add(52.87f, DespawnRazorPlumes);
    }

    private void Ifrit()
    {
        world.Events.Add(12.15f, () => ifrit?.SetPosition(
             new Placement(
                 new Vector3(13.7f, 0f, 13.7f),
                 float.DegreesToRadians(-135f)
             )));

        world.Events.Add(12.37f, () =>
        {
            ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(14.50f, () => ifrit?.Cast(ActionId.EruptionIfrit, animationLock: 2.4f));

        utils.EruptionPuddle(() => dummies[10], () => state.PlayerEruptions[0], 14.50f);
        utils.EruptionPuddle(() => dummies[11], () => state.PlayerEruptions[1], 14.50f);
        utils.EruptionPuddle(() => dummies[12], () => state.PlayerGaol, 14.50f);

        utils.EruptionPuddle(() => dummies[7], () => state.PlayerEruptions[0], 16.50f);
        utils.EruptionPuddle(() => dummies[8], () => state.PlayerEruptions[1], 16.50f);
        utils.EruptionPuddle(() => dummies[9], () => state.PlayerGaol, 16.50f);

        utils.EruptionPuddle(() => dummies[11], () => state.PlayerEruptions[0], 18.42f);
        utils.EruptionPuddle(() => dummies[12], () => state.PlayerEruptions[1], 18.42f);

        world.Events.Add(19.38f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));

        utils.EruptionPuddle(() => dummies[9], () => state.PlayerEruptions[0], 20.55f);
        utils.EruptionPuddle(() => dummies[10], () => state.PlayerEruptions[1], 20.55f);

        world.Events.Add(31.60f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(33.60f, () => state.PlayerFlamingCrush?.AttachLockonVfx(LockonId.FlamingCrush));

        world.Events.Add(38.78f, () =>
        {
            ifrit?.Cast(Actions.FlamingCrush, state.PlayerFlamingCrush);
        });

        world.Events.Add(41.00f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Titan()
    {
        world.Events.Add(12.15f, () => titan?.SetPosition(
             new Placement(
                 new Vector3(-13.7f, 0f, 13.7f),
                 float.DegreesToRadians(135f)
             )));

        world.Events.Add(12.37f, () =>
        {
            titan?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(16.50f, () => titan?.Cast(ActionId.RockThrow, state.PlayerGaol, 2.1f));

        world.Events.Add(18.65f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));

        world.Events.Add(21.32f, () =>
        {
            state.PlayerGaol!.SetTargetable(false);

            state.PlayerGaol!.AddStatusParam(StatusId.Fetters, 0);
            state.PlayerGaol.StopMoving();
        });

        SimEnemy? graniteGaol = null;

        world.Events.Add(22.82f, () => graniteGaol = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.GraniteGaol,
                NameId: BNpcNameId.GraniteGaol,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Manual,
                Visibility: SpawnVisibility.Visible,
                Placement: new(state.PlayerGaol!.Position, 0)
                )
            )
        );

        // Unknown
        world.Events.Add(23.32f, () => graniteGaol!.ActorControl.Send(36, 1, 142));

        world.Events.Add(23.54f, () =>
        {
            graniteGaol!.SetVisibleInEnemyList(true);
            graniteGaol!.SetTargetable(true);

            graniteGaol?.Cast(Actions.GraniteImpact);
        });

        world.Events.Add(30.67f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        // Gaol gets removed as soon as cast ends, to not give that much of an advantage to the player
        world.Events.Add(30.24f, () =>
        {
            state.PlayerGaol!.SetTargetable(true);
            state.PlayerGaol!.RemoveStatus(StatusId.Fetters);

            graniteGaol!.Defeat();
        });

        world.Events.Add(32.84f, () =>
        {
            var bait = state.LandslideBait is { } role ? party.Get(role) : party.GetRandom(world.Rng);
            titan?.Face(bait);
            titan?.Cast(ActionId.LandslideTitan, animationLock: 4.1f);
        });

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 32.84f, LandslideType.Normal);

        utils.LandslideLines(() => titan, [() => dummies[3], () => dummies[4], () => dummies[5], () => dummies[6], () => dummies[7]], 35.07f, LandslideType.Awaken);

        world.Events.Add(41.20f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void LightPillar(Func<SimEnemy?> getDummy, float castOffset, bool direct = false)
    {
        const float Distance = 3f;
        const float DistanceSquared = Distance * Distance;

        world.Events.Add(castOffset, () =>
        {
            var playerPosition = state.PlayerLightPillar!.Position;
            var lightPillarPlacement = state.LightPillarPlacement;

            var position = direct || Vector3.DistanceSquared(lightPillarPlacement.Position, playerPosition) < DistanceSquared
                ? playerPosition
                : lightPillarPlacement.Face(playerPosition).MoveForward(Distance).Position;
            state.LightPillarPlacement = new(position, 0);

            getDummy()?.Cast(Actions.LightPillarCircle, position);
        });
    }

    private void AetherochemicalLaser(int index, float castOffset)
        => world.Events.Add(castOffset, () => ultima?.Cast(Actions.AetherochemicalLaserById(state.AetherochemicalLasers[index])));

    private SimEnemy? RazorPlume(Placement placement)
    {
        var enemy = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.RazorPlume,
                NameId: BNpcNameId.RazorPlume,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.Visible,
                Placement: placement,
                InitialModeAttributeFlags: 0x0
                )
            );

        return enemy;
    }

    private void DespawnRazorPlumes()
    {
        var plumes = razorPlumes.Keys.ToArray();
        razorPlumes.Clear();

        foreach (var plume in plumes)
        {
            plume?.Despawn();
        }
    }

    public MpMessage? BuildReplayStateMessage()
    {
        if (LastState is not { } s) return null;
        if (RoleOf(s.PlayerLightPillar) is not { } lightPillar || RoleOf(s.PlayerGaol) is not { } gaol
            || RoleOf(s.PlayerFlamingCrush) is not { } flamingCrush) return null;
        var mistralSongs = s.PlayerMistralSongs.Select(RoleOf).ToArray();
        var eruptions = s.PlayerEruptions.Select(RoleOf).ToArray();
        if (mistralSongs.Any(r => r is null) || eruptions.Any(r => r is null)) return null;
        return new UltimateSuppressionAiReplayStateMessage(
            lightPillar, mistralSongs.Select(r => r!.Value).ToArray(),
            eruptions.Select(r => r!.Value).ToArray(), gaol, flamingCrush, s.SuppressionSpotOrder);
    }

    private static PartyRole? RoleOf(SimCharacter? member) => (member as ISimPartyMember)?.Role;

    // The Ai drives AiManager, which ticks on a peer like any other scenario, so there is no
    // replay clock of its own to keep.
    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UltimateSuppressionAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = UltimateSuppressionState.FromNetworkReplay(
            replayWorld.Party, msg.LightPillar, msg.MistralSongs, msg.Eruptions, msg.Gaol, msg.FlamingCrush,
            msg.SuppressionSpotOrder);
        if (shadowState == null) return null;
        ((IScenarioAi<UltimateSuppressionState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
