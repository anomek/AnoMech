using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Network;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu.UltimateSuppression;

public unsafe class UltimateSuppressionScenario : IScenario
{
    public string Name => "Ultimate Suppression";
    public IPhase Phase => UwuZone.Ultima;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UltimateSuppressionAi()];
    public void DrawSettings() => settingsWindow.Draw();

    private readonly UltimateSuppressionSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private SimParty party = null!;

    // Lazy off `world`: RunInstanceEvents runs for a peer, which never calls Run.
    private UwuUtils? utilsInstance;
    private UwuUtils utils => utilsInstance ??= new UwuUtils(world);
    private UltimateSuppressionState state = null!;

    private SimEnemy? ultima;
    private SimEnemy? garuda;
    private SimEnemy? ifrit;
    private SimEnemy? titan;

    private SimEnemy? chirada;
    private SimEnemy? suparna;

    private SimEnemy?[] dummies = new SimEnemy?[13];

    private bool razorPlumesDamage = false;
    private Stopwatch razorPlumesRotate = new();
    private Stopwatch razorPlumesBack = new();
    private Dictionary<SimEnemy, Placement> razorPlumes = new();

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

        state = new(party, settingsWindow.Overrides);

        razorPlumesDamage = false;
        razorPlumesRotate.Reset();
        razorPlumesBack.Reset();
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
        if (razorPlumesRotate.IsRunning)
        {
            var fraction = float.Min((float)razorPlumesRotate.Elapsed.TotalSeconds / Duration.RazorPlumeRotation, 1);
            var angle = fraction * Geometry.RazorPlumeRotation;

            foreach (var (razorPlume, placement) in razorPlumes)
            {
                var rotatedPlacement = placement.RotateAroundOrigin(angle);
                razorPlume!.SetPosition(rotatedPlacement);
            }
        }
        else if (razorPlumesBack.IsRunning)
        {
            var fraction = float.Min((float)razorPlumesBack.Elapsed.TotalSeconds / Duration.RazorPlumeBack, 1);
            var distance = fraction * Geometry.RazorPlumeBackDistance;

            foreach (var (razorPlume, placement) in razorPlumes)
            {
                var movedPlacement = placement.MoveForward(-distance);
                razorPlume!.SetPosition(movedPlacement.Position);
            }
        }

        if (razorPlumesDamage)
        {
            foreach (var (razorPlume, placement) in razorPlumes)
            {
                // TODO: Eyeballed Radius
                foreach (var character in party.Find.InsideCircle(razorPlume.Position, 2))
                {
                    character.Die("Razor Plume");
                }
            }
        }
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

            garuda?.AddStatusParam(StatusId.Woken, 0);

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

            ifrit?.AddStatusParam(StatusId.Woken, 0);

            titan = world.SpawnEnemy(
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
                    IsVisible: false,
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
                    IsVisible: false,
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
                    IsVisible: true,
                    Placement: new Placement(
                        new Vector3(0, 0, 0),
                        0)
                    )
                );
            }

            var mt = party.Get(PartyRole.MainTank);
            var healer = party.Get(state.Rng.NextHealerRole());

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
        var getUltima = () => ultima;

        // ActionId.UltimateSuppression is Animation Only
        utils.Cast(getUltima,
            2.50f, new() { ActionId = ActionId.UltimateSuppression, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 2.7f, Interruptible = false },
            5.47f, new() { ActionId = ActionId.UltimateSuppression, AnimationLock = 4.5f, SpellId = (ushort)ActionId.UltimateSuppression, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getUltima, ActionEffectAnimationTarget = getUltima });

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

        // ActionId.LightPillarUltima is Animation Only. Actual AOEs are handled in the LightPillar calls
        utils.Cast(getUltima,
            20.55f, new() { ActionId = ActionId.LightPillarUltima, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 1.7f, Interruptible = false },
            22.57f, new() { ActionId = ActionId.LightPillarUltima, AnimationLock = 2.1f, SpellId = (ushort)ActionId.LightPillarUltima, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getUltima, ActionEffectAnimationTarget = getUltima });

        AetherochemicalLaser(() => ultima, 24.70f, 27.55f);

        LightPillar(() => dummies[5], 24.70f, 25.64f, true);
        LightPillar(() => dummies[4], 25.64f, 26.60f);
        LightPillar(() => dummies[10], 26.60f, 27.55f);
        LightPillar(() => dummies[9], 27.55f, 28.71f);

        AetherochemicalLaser(() => ultima, 28.71f, 31.60f);

        LightPillar(() => dummies[10], 28.71f, 29.68f);
        LightPillar(() => dummies[12], 29.68f, 30.65f);

        AetherochemicalLaser(() => ultima, 32.84f, 35.78f);

        // ActionId.TankPurge is Animation Only (TODO for more fleshed out damage logic?)
        utils.Cast(getUltima,
            40.03f, new() { ActionId = ActionId.TankPurge, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 3.7f, Interruptible = false },
            43.88f, new() { ActionId = ActionId.TankPurge, AnimationLock = 2.1f, SpellId = (ushort)ActionId.TankPurge, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getUltima, ActionEffectAnimationTarget = getUltima });

        world.Events.Add(46.10f, () => ultima?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Garuda()
    {
        var getGaruda = () => garuda;

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
            garuda?.SetVisible(true);
            garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            chirada?.SetVisible(true);
            chirada?.PlayActionTimeline(ActionTimelineId.WarpEnd);

            suparna?.SetVisible(true);
            suparna?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(14.50f, () => garuda?.PlayActionTimeline(ActionTimelineId.RazorPlume));

        world.Events.Add(15.48f, () =>
        {
            var playerMistral1 = state.PlayerMistralSongs[0]!;
            var playerMistral2 = state.PlayerMistralSongs[1]!;

            Lockon(playerMistral1, LockonId.MistralSong);
            Lockon(playerMistral2, LockonId.MistralSong);
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

        IReadOnlyList<SimCharacter> chiradaMistralSnapshot = null!;
        IReadOnlyList<SimCharacter> suparnaMistralSnapshot = null!;

        world.Events.Add(20.55f, () =>
        {
            chirada?.Face(state.PlayerMistralSongs[0]);
            suparna?.Face(state.PlayerMistralSongs[1]);

            chirada?.NativeActionEffect(
                ActionId.MistralSongSuparnaChirada,
                2.1f,
                (ushort)ActionId.MistralSongSuparnaChirada,
                0,
                ActionType.Action,
                0,
                animationTargetId: chirada.GameObjectId
                );

            suparna?.NativeActionEffect(
                ActionId.MistralSongSuparnaChirada,
                2.1f,
                (ushort)ActionId.MistralSongSuparnaChirada,
                0,
                ActionType.Action,
                0,
                animationTargetId: suparna.GameObjectId
                );

            // ActionId.MistralSongSuparnaChirada has a CastType of 6, which gets treated as a circle, so we manually check it as a Cone (Garuda's Mistral Song IS a Cone)
            // EffectRange is 40.
            chiradaMistralSnapshot = party.Find.InsideCone(chirada!.Placement(), MathF.PI / 6f, 40).OrderBy(x => Vector3.DistanceSquared(chirada!.Position, x.Position)).ToList();
            suparnaMistralSnapshot = party.Find.InsideCone(suparna!.Placement(), MathF.PI / 6f, 40).OrderBy(x => Vector3.DistanceSquared(suparna!.Position, x.Position)).ToList();
        });

        var chiradaMistralHit = Vector3.Zero;
        var suparnaMistralHit = Vector3.Zero;

        world.Events.Add(20.81f, () =>
        {
            chiradaMistralHit = ResolveMistralSong(chiradaMistralSnapshot);
            suparnaMistralHit = ResolveMistralSong(suparnaMistralSnapshot);
        });

        world.Events.Add(20.82f, () =>
        {
            garuda?.Face(party.GetRandom());
            razorPlumesRotate.Start();
        });

        world.Events.Add(20.82f + Duration.RazorPlumeRotation, () =>
        {
            razorPlumesRotate.Stop();

            foreach (var key in razorPlumes.Keys)
            {
                razorPlumes[key] = key.Placement();
            }

            razorPlumesBack.Start();
        });

        world.Events.Add(20.82f + Duration.RazorPlumeRotation + Duration.RazorPlumeBack, razorPlumesBack.Stop);

        // ActionId.MistralSong is Animation Only (TODO: does this actually do damage outside the cone?)
        utils.Cast(getGaruda,
            20.82f, new() { ActionId = ActionId.MistralSong, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 1.7f, Interruptible = false },
            22.82f, new() { ActionId = ActionId.MistralSong, AnimationLock = 1.8f, SpellId = (ushort)ActionId.MistralSong, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getGaruda, ActionEffectActionTarget = getGaruda });

        world.Events.Add(22.57f, () =>
        {
            chirada?.PlayActionTimeline(ActionTimelineId.WarpStart2);
            suparna?.PlayActionTimeline(ActionTimelineId.WarpStart2);
        });

        utils.FeatherRain([() => dummies[6], () => dummies[7], () => dummies[8], () => dummies[9], () => dummies[10]], 22.57f, 24.11f, 25.15f);

        GreatWhirlwind(() => dummies[11], () => chiradaMistralHit, 23.81f, 26.60f);
        GreatWhirlwind(() => dummies[12], () => suparnaMistralHit, 23.81f, 26.60f);

        world.Events.Add(24.70f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2));

        utils.FeatherRain([() => dummies[0], () => dummies[1], () => dummies[2], () => dummies[3], () => dummies[5]], 24.70f, 26.10f, 27.05f);

        world.Events.Add(30.67f, () => garuda?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(32.84f, () => state.MesohighTether = world.Tether(garuda, End.Passable(), TetherId.Mesohigh));

        SimCharacter? tetherTarget = null;
        var tetherHadThermalLow = false;

        world.Events.Add(37.92f, () =>
        {
            state.MesohighTether!.Resolved = true;
            tetherTarget = state.MesohighTether.B;

            garuda?.NativeActionEffect(
                ActionId.Mesohigh,
                2.1f,
                (ushort)ActionId.Mesohigh,
                0,
                ActionType.Action,
                0,
                animationTargetId: tetherTarget!.GameObjectId
                );

            tetherHadThermalLow = tetherTarget!.HasStatus(StatusId.ThermalLow);
            tetherTarget.RemoveStatus(StatusId.ThermalLow);
            state.MesohighTether.Despawn();
        });

        // TODO: I don't know if Mesohigh does any damage aside from the tether
        world.Events.Add(38.75f, () =>
        {
            if (!tetherHadThermalLow)
            {
                tetherTarget!.Die("Mesohigh");
            }
        });

        List<IReadOnlyList<SimCharacter>> featherLanceSnapshot = new();
        world.Events.Add(41.20f, () =>
        {
            garuda?.PlayActionTimeline(ActionTimelineId.WarpStart2);

            foreach (var (razorPlume, _) in razorPlumes)
            {
                razorPlume?.NativeActionEffect(
                    ActionId.Featherlance,
                    2.1f,
                    (ushort)ActionId.Featherlance,
                    0,
                    ActionType.Action,
                    0,
                    animationTargetId: razorPlume.GameObjectId
                    );

                featherLanceSnapshot.Add(party.Find.InsideActionAoe(ActionId.Featherlance, razorPlume!.Placement()));
            }
        });

        utils.FeatherRain([() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 41.20f, 42.47f, 43.43f);

        world.Events.Add(41.70f, () =>
        {
            utils.ResolveSnapshot(featherLanceSnapshot.SelectMany(x => x).ToList(), "Featherlance");
        });

        world.Events.Add(42.97f, () =>
        {
            foreach (var (razorPlume, _) in razorPlumes)
            {
                PacketDispatcher.HandleActorControlPacket(razorPlume!.EntityId, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Death Animation
                PacketDispatcher.HandleActorControlPacket(razorPlume!.EntityId, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Set Mode
            }

            razorPlumesDamage = false;
        });

        world.Events.Add(51.12f, () =>
        {
            foreach (var (razorPlume, _) in razorPlumes)
            {
                PacketDispatcher.HandleActorControlPacket(razorPlume!.EntityId, 39, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Fade-Out
            }
        });

        world.Events.Add(52.87f, DespawnRazorPlumes);
    }

    private void Ifrit()
    {
        var getIfrit = () => ifrit;

        world.Events.Add(12.15f, () => ifrit?.SetPosition(
             new Placement(
                 new Vector3(13.7f, 0f, 13.7f),
                 float.DegreesToRadians(-135f)
             )));

        world.Events.Add(12.37f, () =>
        {
            ifrit?.SetVisible(true);
            ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        // ActionId.EruptionIfrit is Animation Only
        utils.Cast(getIfrit,
            14.50f, new() { ActionId = ActionId.EruptionIfrit, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 2.2f, Interruptible = false },
            17.00f, new() { ActionId = ActionId.EruptionIfrit, AnimationLock = 2.4f, SpellId = (ushort)ActionId.EruptionIfrit, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getIfrit, ActionEffectActionTarget = getIfrit });

        utils.EruptionPuddle(() => dummies[10], () => state.PlayerEruptions[0], 14.50f, 17.51f);
        utils.EruptionPuddle(() => dummies[11], () => state.PlayerEruptions[1], 14.50f, 17.51f);
        utils.EruptionPuddle(() => dummies[12], () => state.PlayerGaol, 14.50f, 17.51f);

        utils.EruptionPuddle(() => dummies[7], () => state.PlayerEruptions[0], 16.50f, 19.38f);
        utils.EruptionPuddle(() => dummies[8], () => state.PlayerEruptions[1], 16.50f, 19.38f);
        utils.EruptionPuddle(() => dummies[9], () => state.PlayerGaol, 16.50f, 19.38f);

        utils.EruptionPuddle(() => dummies[11], () => state.PlayerEruptions[0], 18.42f, 21.32f);
        utils.EruptionPuddle(() => dummies[12], () => state.PlayerEruptions[1], 18.42f, 21.32f);

        world.Events.Add(19.38f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));

        utils.EruptionPuddle(() => dummies[9], () => state.PlayerEruptions[0], 20.55f, 23.32f);
        utils.EruptionPuddle(() => dummies[10], () => state.PlayerEruptions[1], 20.55f, 23.32f);

        world.Events.Add(31.60f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        world.Events.Add(33.60f, () => Lockon(state.PlayerFlamingCrush, LockonId.FlamingCrush));

        IReadOnlyList<SimCharacter> flamingCrushSnapshot = null!;
        world.Events.Add(38.78f, () =>
        {
            ifrit?.NativeActionEffect(
                ActionId.FlamingCrush,
                2.1f,
                (ushort)ActionId.FlamingCrush,
                0,
                ActionType.Action,
                0,
                animationTargetId: state.PlayerFlamingCrush!.GameObjectId
                );

            foreach (var character in party.AllMembers())
            {
                character.AddStatusParam(StatusId.AccursedFlame, 0, 3);
            }

            flamingCrushSnapshot = party.Find.InsideActionAoe(ActionId.FlamingCrush, state.PlayerFlamingCrush!.Placement());
        });

        world.Events.Add(39.51f, () =>
        {
            var count = flamingCrushSnapshot.Count;

            if (count != 7)
            {
                var cause = $"Flaming Crush ({count}/7 players in Stack)";

                foreach (var character in flamingCrushSnapshot)
                {
                    character.Die(cause);
                }
            }
        });

        world.Events.Add(41.00f, () => ifrit?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Titan()
    {
        var getTitan = () => titan;

        world.Events.Add(12.15f, () => titan?.SetPosition(
             new Placement(
                 new Vector3(-13.7f, 0f, 13.7f),
                 float.DegreesToRadians(135f)
             )));

        world.Events.Add(12.37f, () =>
        {
            titan?.SetVisible(true);
            titan?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });

        world.Events.Add(16.50f, () => titan?.NativeActionEffect(
            ActionId.RockThrow,
            2.1f,
            (ushort)ActionId.RockThrow,
            0,
            ActionType.Action,
            0,
            animationTargetId: state.PlayerGaol!.GameObjectId
            ));

        world.Events.Add(18.65f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));

        world.Events.Add(21.32f, () =>
        {
            PacketDispatcher.HandleActorControlPacket(state.PlayerGaol!.EntityId, 54, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Make Untargetable

            state.PlayerGaol!.AddStatusParam(StatusId.Fetters, 0);
            state.PlayerGaol.StopMoving();

            if (state.PlayerGaol is SimPlayer player)
            {
                SetStun(true);
            }
        });

        SimEnemy? graniteGaol = null;

        world.Events.Add(22.82f, () => graniteGaol = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.GraniteGaol,
                NameId: BNpcNameId.GraniteGaol,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Manual,
                IsVisible: true,
                Placement: new(state.PlayerGaol!.Position, 0)
                )
            )
        );

        // Unknown
        world.Events.Add(23.32f, () => PacketDispatcher.HandleActorControlPacket(graniteGaol!.EntityId, 36, 1, 142, 0, 0, 0, 0, 0, 0, 0xE0000000, false));

        world.Events.Add(23.54f, () =>
        {
            graniteGaol!.SetVisibleInEnemyList(true);
            graniteGaol!.SetTargetable(true);

            graniteGaol?.NativeCast(
                ActionId.GraniteImpact,
                ActionType.Action,
                0f,
                6.7f,
                false,
                targetId: graniteGaol.GameObjectId
                );
        });

        world.Events.Add(30.67f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpEnd));

        // Gaol gets removed as soon as cast ends, to not give that much of an advantage to the player
        world.Events.Add(30.24f, () =>
        {
            PacketDispatcher.HandleActorControlPacket(state.PlayerGaol!.EntityId, 54, 1, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Make Targetable

            state.PlayerGaol!.RemoveStatus(StatusId.Fetters);

            if (state.PlayerGaol is SimPlayer player)
            {
                SetStun(false);
            }

            ((Character*)graniteGaol!.BattleCharaPtr)->SetMode(CharacterModes.Dead, 0);
            PacketDispatcher.HandleActorControlPacket(graniteGaol!.EntityId, 15, 540, 1, ActionId.GraniteImpact, 1, 0, 0, 0, 0, 0xE0000000, false); // Cast Interrupt
            PacketDispatcher.HandleActorControlPacket(graniteGaol!.EntityId, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false); // Death Animation
        });

        world.Events.Add(32.84f, () =>
        {
            var bait = party.GetRandom();
            titan?.Face(bait);
        });

        // ActionId.LandslideTitan is Animation Only. Actual Landslides are handled at utils.LandslideLines calls
        utils.Cast(getTitan,
            32.84f, new() { ActionId = ActionId.LandslideTitan, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 1.9000001f, Interruptible = false },
            35.07f, new() { ActionId = ActionId.LandslideTitan, AnimationLock = 4.1f, SpellId = (ushort)ActionId.LandslideTitan, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastTarget = getTitan, ActionEffectActionTarget = getTitan });

        utils.LandslideLines(() => titan, [() => dummies[8], () => dummies[9], () => dummies[10], () => dummies[11], () => dummies[12]], 32.84f, 35.07f, LandslideType.Normal);

        utils.LandslideLines(() => titan, [() => dummies[3], () => dummies[4], () => dummies[5], () => dummies[6], () => dummies[7]], 35.07f, 37.22f, LandslideType.Awaken);

        world.Events.Add(36.95f, () => PacketDispatcher.HandleActorControlPacket(graniteGaol!.EntityId, 39, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false)); // Fade-Out

        world.Events.Add(38.78f, () => graniteGaol?.Despawn());

        world.Events.Add(41.20f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void LightPillar(Func<SimEnemy?> getDummy, float castOffset, float effectOffset, bool direct = false)
    {
        const float Distance = 3f;
        const float DistanceSquared = Distance * Distance;

        var position = Vector3.Zero;

        var castInfo = new UwuUtilsRecords
        {
            ActionId = ActionId.LightPillarCircle,
            ActionType = ActionType.Action,
            OmenDelay = 0f,
            CastTime = 0.7f,
            Interruptible = false
        };

        var actionEffectInfo = new ActionEffectInfo
        {
            ActionId = ActionId.LightPillarCircle,
            AnimationLock = 0.1f,
            SpellId = (ushort)ActionId.LightPillarCircle,
            AnimationVariaton = 0,
            ActionType = ActionType.Action,
            Flags = 0
        };

        var dynamicInfo = new DynamicInfo
        {
            CastPosition = () => state.LightPillarPlacement.Position,
            ActionEffectPosition = () => state.LightPillarPlacement.Position
        };

        world.Events.Add(castOffset, () =>
        {
            var playerPosition = state.PlayerLightPillar!.Position;

            if (direct)
            {
                position = playerPosition;
                state.LightPillarPlacement = new(position, 0);
            }
            else
            {
                var lightPillarPlacement = state.LightPillarPlacement;
                if (Vector3.DistanceSquared(lightPillarPlacement.Position, playerPosition) < DistanceSquared)
                {
                    position = playerPosition;
                }
                else
                {
                    var rotated = lightPillarPlacement.Face(playerPosition);
                    position = rotated.MoveForward(Distance).Position;
                }
                state.LightPillarPlacement = new(position, 0);
            }
        });

        utils.Cast(getDummy, castOffset, castInfo, effectOffset, actionEffectInfo, dynamicInfo, 0.66f, snapshot => utils.ResolveSnapshot(snapshot, "Light Pillar"));
    }

    private void AetherochemicalLaser(Func<SimEnemy?> getEnemy, float castOffset, float effectOffset)
    {
        var laserActions = new uint[] { ActionId.AetherochemicalLaserCenter, ActionId.AetherochemicalLaserRight, ActionId.AetherochemicalLaserLeft };
        var laserAction = state.Rng.NextObj(laserActions);

        var castInfo = new UwuUtilsRecords
        {
            ActionId = laserAction,
            ActionType = ActionType.Action,
            CastTime = 2.7f
        };

        var actionEffectInfo = new ActionEffectInfo
        {
            ActionId = laserAction,
            AnimationLock = 1.1f,
            SpellId = (ushort)laserAction,
            ActionType = ActionType.Action
        };

        var rotationOffset = laserAction switch
        {
            ActionId.AetherochemicalLaserRight => -45,
            ActionId.AetherochemicalLaserLeft => 45,
            _ => 0
        };

        var dynamicInfo = new DynamicInfo
        {
            CastRotation = () => getEnemy()!.Rotation + float.DegreesToRadians(rotationOffset),
            CastTarget = getEnemy,
            ActionEffectAnimationTarget = getEnemy
        };

        utils.Cast(getEnemy, castOffset, castInfo, effectOffset, actionEffectInfo, dynamicInfo, 0.66f, snapshot => utils.ResolveSnapshot(snapshot, "Aetherochemical Laser"));
    }

    private SimEnemy? RazorPlume(Placement placement)
    {
        var enemy = world.SpawnEnemy(
            new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.RazorPlume,
                NameId: BNpcNameId.RazorPlume,
                Level: 70,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: true,
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

    private void GreatWhirlwind(Func<SimEnemy?> getDummy, Func<Vector3> getPosition, float castOffset, float effectOffset)
    {
        utils.Cast(getDummy,
            castOffset, new() { ActionId = ActionId.GreatWhirlwind, ActionType = ActionType.Action, OmenDelay = 0f, CastTime = 2.7f, Interruptible = false },
            effectOffset, new() { ActionId = ActionId.GreatWhirlwind, AnimationLock = 2.1f, SpellId = (ushort)ActionId.GreatWhirlwind, AnimationVariaton = 0, ActionType = ActionType.Action, Flags = 0 },
            new() { CastPosition = getPosition, ActionEffectPosition = getPosition });
    }

    private void SetStun(bool value)
    {
        var condition = Conditions.Instance();
        condition->SufferingStatusAffliction = value;
        condition->SufferingStatusAffliction2 = value;
    }

    private void Lockon(SimCharacter? target, uint lockonId)
    {
        PacketDispatcher.HandleActorControlPacket(target!.EntityId, 34, lockonId, target!.GameObjectId.ObjectId, 0, 0, 0, 0, 0, 0, 0xE0000000, false);
    }

    private Vector3 ResolveMistralSong(IReadOnlyList<SimCharacter> snapshot)
    {
        var closest = snapshot[0];
        var partyMember = (ISimPartyMember)closest;

        if (partyMember != state.PlayerGaol && !partyMember.Role.IsTank())
        {
            closest.Die("Mistral Song");
        }

        return closest.Position;
    }

    private static PartyRole? RoleOf(SimCharacter? member) => (member as ISimPartyMember)?.Role;
}
