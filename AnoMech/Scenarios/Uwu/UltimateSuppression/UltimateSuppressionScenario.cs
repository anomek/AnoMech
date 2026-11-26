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

    // Seconds into the plumes' circling; null while it isn't running.
    private float? razorPlumesCircling;
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
        razorPlumesCircling = null;
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

    public void OnActionResolved(ActionResolved resolved) => LeaveWhirlwindBehindMistralSong(resolved);
    public void OnStatusLost(StatusLost lost) => SuperCyclone.FollowMesohigh(world, lost, garuda);

    public void Tick(float delta, float elapsed)
    {
        if (razorPlumesCircling is { } circling)
        {
            razorPlumesCircling = circling + delta;
            WalkRazorPlumesAroundArena(razorPlumesCircling.Value);
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

    // Retail walks the plumes with a stream of ActorMoves, so each one is walked towards a point a
    // little ahead of it on its circle rather than set on it.
    private void WalkRazorPlumesAroundArena(float circling)
    {
        const float Lead = 0.5f;
        var fraction = float.Min((circling + Lead) / Duration.RazorPlumeCircling, 1);
        foreach (var (razorPlume, start) in razorPlumes)
        {
            var speed = float.Abs(Geometry.RazorPlumeRotation) * start.Position.Length() / Duration.RazorPlumeCircling;
            razorPlume.MoveTo(start.RotateAroundOrigin(fraction * Geometry.RazorPlumeRotation).Position, speed);
        }
        if (fraction >= 1) razorPlumesCircling = null;
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
                    LayoutId: BNpcLayoutId.UltimaWeapon,
                    Level: 70,
                    Targetable: true,
                    EnemyList: EnemyListMode.Always,
                    Visibility: SpawnVisibility.Visible,
                    Placement: new Placement(
                        new Vector3(0, 0, -10),
                        0),
                    InitialModeAttributeFlags: 0x11,
                    Statuses: UwuUtils.WokenAtSpawn(isUltima: true),
                    AnimationState: UwuUtils.AwakenedAnimationState)
                );

            garuda = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Garuda,
                    NameId: BNpcNameId.Garuda,
                    LayoutId: BNpcLayoutId.Garuda,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0),
                    Statuses: UwuUtils.WokenAtSpawn(isUltima: false),
                    AnimationState: UwuUtils.AwakenedAnimationState)
                );

            ifrit = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Ifrit,
                    NameId: BNpcNameId.Ifrit,
                    LayoutId: BNpcLayoutId.Ifrit,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    DisplayFlags: 0x60008,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0),
                    Statuses: UwuUtils.WokenAtSpawn(isUltima: false),
                    AnimationState: UwuUtils.AwakenedAnimationState)
                );

            titan = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Titan,
                    NameId: BNpcNameId.Titan,
                    LayoutId: BNpcLayoutId.Titan,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    DisplayFlags: 0x60008,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0),
                    Statuses: UwuUtils.WokenAtSpawn(isUltima: false),
                    AnimationState: UwuUtils.AwakenedAnimationState)
            );

            chirada = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SuparnaChirada,
                    NameId: BNpcNameId.Chirada,
                    LayoutId: BNpcLayoutId.Chirada,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    DisplayFlags: 0x60008,
                    Engaged: false,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

            suparna = world.SpawnEnemy(
                new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SuparnaChirada,
                    NameId: BNpcNameId.Suparna,
                    LayoutId: BNpcLayoutId.Suparna,
                    Level: 70,
                    Targetable: false,
                    EnemyList: EnemyListMode.Never,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    DisplayFlags: 0x60008,
                    Engaged: false,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0))
            );

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

        world.Events.Add(10.02f, () => ultima?.SetTargetable(false));

        world.Events.Add(10.07f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));

        world.Events.Add(12.08f, () => ultima?.SetPosition(
             new Placement(
                 new Vector3(13.7f, 0f, -13.7f),
                 float.DegreesToRadians(-45f)
             )));

        world.Events.Add(12.18f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(20.35f, () => ultima?.Cast(ActionId.LightPillarUltima, animationLock: 2.1f));

        AetherochemicalLaser(0, 24.47f);

        LightPillar(24.47f, true);
        LightPillar(25.50f);
        LightPillar(26.45f);
        LightPillar(27.47f);
        LightPillar(28.47f);

        AetherochemicalLaser(1, 28.60f);

        LightPillar(29.47f);

        AetherochemicalLaser(2, 32.73f);

        world.Events.Add(39.95f, () => ultima?.Cast(Actions.TankPurge));

        world.Events.Add(46.03f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Garuda()
    {
        world.Events.Add(12.08f, () =>
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

        world.Events.Add(12.18f, () =>
        {
            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            chirada?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            suparna?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(14.33f, () => garuda?.PlayActionTimeline(TimelineId.RazorPlume));

        world.Events.Add(15.27f, () =>
        {
            state.PlayerMistralSongs[0]?.AttachLockonVfx(LockonId.MistralSong);
            state.PlayerMistralSongs[1]?.AttachLockonVfx(LockonId.MistralSong);
        });

        world.Events.Add(17.87f, () =>
        {
            var placement1 = new Placement(new(12, 0, 12), 0);
            razorPlumes.Add(RazorPlume(placement1)!, placement1);

            var placement2 = new Placement(new(-12, 0, 12), 0);
            razorPlumes.Add(RazorPlume(placement2)!, placement2);

            var placement3 = new Placement(new(12, 0, -12), 0);
            razorPlumes.Add(RazorPlume(placement3)!, placement3);

            var placement4 = new Placement(new(-12, 0, -12), 0);
            razorPlumes.Add(RazorPlume(placement4)!, placement4);

            razorPlumesDamage = true;
        });

        world.Events.Add(20.47f, () =>
        {
            chirada?.Cast(Actions.MistralSongSuparnaChirada, state.PlayerMistralSongs[0]);
            suparna?.Cast(Actions.MistralSongSuparnaChirada, state.PlayerMistralSongs[1]);
        });

        world.Events.Add(20.60f, () =>
        {
            garuda?.Face(state.GarudaFacing is { } role ? party.Get(role) : party.GetRandom(world.Rng));
            garuda?.Cast(Actions.MistralSong);
        });

        world.Events.Add(22.53f, () =>
        {
            chirada?.PlayActionTimeline(ActionTimelineId.WarpStart2);
            suparna?.PlayActionTimeline(ActionTimelineId.WarpStart2);
        });

        utils.FeatherRain(() => garuda, 22.53f, 24.07f, state.FeatherRainTargets, () => state.PlayerGaol);

        world.Events.Add(22.60f, () => razorPlumesCircling = 0f);

        world.Events.Add(24.47f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(() => garuda, 24.47f, 26.00f, state.FeatherRainTargets, () => state.PlayerGaol);

        world.Events.Add(30.58f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(32.67f, () => state.MesohighTether = world.Tether(garuda, End.Passable(), TetherId.Mesohigh));

        world.Events.Add(37.72f, () =>
        {
            garuda?.Cast(Actions.Mesohigh, state.MesohighTether!.B);
            state.MesohighTether.Despawn();
        });

        world.Events.Add(40.88f, () =>
        {
            foreach (var (razorPlume, _) in razorPlumes)
                FireFeatherlance(razorPlume);
        });

        world.Events.Add(40.88f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain(() => garuda, 40.88f, 42.38f, state.FeatherRainTargets);

        world.Events.Add(42.97f, () => razorPlumesDamage = false);

        world.Events.Add(52.87f, DespawnRazorPlumes);
    }

    private void Ifrit()
    {
        world.Events.Add(12.08f, () => ifrit?.SetPosition(
             new Placement(
                 new Vector3(13.7f, 0f, 13.7f),
                 float.DegreesToRadians(-135f)
             )));

        world.Events.Add(12.18f, () =>
        {
            ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(14.33f, () => ifrit?.Cast(ActionId.EruptionIfrit, animationLock: 2.4f));

        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[0], 14.33f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[1], 14.33f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerGaol, 14.33f);

        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[0], 16.33f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[1], 16.33f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerGaol, 16.33f);

        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[0], 18.35f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[1], 18.35f);

        world.Events.Add(19.28f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));

        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[0], 20.35f);
        utils.EruptionPuddle(() => ifrit, () => state.PlayerEruptions[1], 20.35f);

        world.Events.Add(31.47f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(33.50f, () => state.PlayerFlamingCrush?.AttachLockonVfx(LockonId.FlamingCrush));

        world.Events.Add(38.60f, () =>
        {
            ifrit?.Cast(Actions.FlamingCrush, state.PlayerFlamingCrush);
        });

        world.Events.Add(40.70f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Titan()
    {
        world.Events.Add(12.08f, () => titan?.SetPosition(
             new Placement(
                 new Vector3(-13.7f, 0f, 13.7f),
                 float.DegreesToRadians(135f)
             )));

        world.Events.Add(12.18f, () =>
        {
            titan?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(16.33f, () => titan?.Cast(Actions.RockThrow, state.PlayerGaol));

        world.Events.Add(18.47f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));

        world.Events.Add(21.28f, () =>
        {
            state.PlayerGaol!.SetTargetable(false);

            state.PlayerGaol!.AddStatusParam(StatusId.Fetters, 0);
            state.PlayerGaol.StopMoving();
        });

        SimEnemy? graniteGaol = null;

        world.Events.Add(22.67f, () => graniteGaol = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.GraniteGaol,
                NameId: BNpcNameId.GraniteGaol,
                LayoutId: BNpcLayoutId.GraniteGaol,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Manual,
                Visibility: SpawnVisibility.HiddenUntilPopIn,
                Engaged: false,
                Placement: new(state.PlayerGaol!.Position, 0)
                )
            )
        );

        world.Events.Add(23.35f, () => graniteGaol!.ActorControl.PopIn());

        world.Events.Add(23.42f, () =>
        {
            graniteGaol!.SetVisibleInEnemyList(true);
            graniteGaol!.SetTargetable(true);

            graniteGaol?.Cast(Actions.GraniteImpactShort);
        });

        // The gaol breaks just before Granite Impact (cast ends 30.12) goes off, as late as possible so the
        // jailed player gets little advantage.
        world.Events.Add(30.00f, () =>
        {
            state.PlayerGaol!.SetTargetable(true);
            state.PlayerGaol!.RemoveStatus(StatusId.Fetters);

            graniteGaol!.Defeat();
        });

        world.Events.Add(30.58f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(32.84f, () =>
        {
            var bait = state.LandslideBait is { } role ? party.Get(role) : party.GetRandom(world.Rng);
            titan?.Face(bait);
            titan?.Cast(ActionId.LandslideTitan, animationLock: 4.1f);
        });

        utils.LandslideLines(() => titan, 32.84f, LandslideType.Normal);

        utils.LandslideLines(() => titan, 35.07f, LandslideType.Awaken);

        world.Events.Add(41.15f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void LightPillar(float castOffset, bool direct = false)
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

            ultima?.CastFromHelper(Actions.LightPillarCircle, position, at: UwuUtils.OnLocation(position));
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
                DisplayFlags: 0x40008,
                Engaged: false,
                LayoutId: BNpcLayoutId.RazorPlume,
                Placement: placement,
                InitialModeAttributeFlags: 0x0
                )
            );

        return enemy;
    }

    private void LeaveWhirlwindBehindMistralSong(ActionResolved resolved)
    {
        if (resolved.Action.ActionId != ActionId.MistralSongSuparnaChirada || resolved.Hits is not [var front, ..]) return;
        if (resolved.Caster is not SimEnemy sister || (sister != chirada && sister != suparna)) return;
        var at = front.Position;
        world.Events.Add(3.1f, () => sister.CastFromHelper(Actions.GreatWhirlwind, at, at: UwuUtils.OnLocation(at)));
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
