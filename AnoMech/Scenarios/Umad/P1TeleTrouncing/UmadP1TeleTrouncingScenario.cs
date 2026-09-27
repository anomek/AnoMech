using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

using Constants = UmadP1TeleTrouncingConstants;

// Dancing Mad P1 from Kefka's Tele-trouncing cast to the boss going untargetable: arrows,
// Confused/Sleep, Confetti-3, then Mystery Magic's three overlapping resolves. Cast bars are the
// 4.7s/2.7s the real packets carry, with the release 0.29s after the bar fills.
//
// Multiplayer: the host resolves everything against each peer's reported pose; the forced
// movement the mechanic applies to a peer (the confused chase, an arrow's snap and push) reaches
// them through SimNetworkPuppet's pending network moves.
public sealed class UmadP1TeleTrouncingScenario : IScenario
{
    public string Name => "Tele-trouncing";
    public IPhase Phase => UmadZone.P1;
    public float BgmSecondsAtStart => Constants.BgmSecondsAtStart;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP1TeleTrouncingAi()];
    public object SettingsOverrides => settingsWindow.Overrides;

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP1TeleTrouncingState state = null!;
    private SimEnemy? kefka;
    private SimEnemy? confusedStatue;
    private SimEnemy? sleepStatue;
    // The gazes are cast from the statues' eye points, not the tether sources 14.5y / 9y away.
    private SimEnemy? gazeCasterInverted;
    private SimEnemy? gazeCasterNormal;
    // The real gaze-animation props, spawned as real EObjs so the client plays the statue
    // SharedGroup timelines. Two instances of each, as in the real fight: appear and tether-fire
    // play on one pair, the gaze wind-up and resolve on the other instance of the gazing type.
    // The BNpc doppels still do the mechanical work and the marker "?" stand-in, so a full EObj
    // pool costs only the model.
    private SimEventObject? gazeStatueNormal;       // tether pair: appear + tether-fire
    private SimEventObject? gazeStatueInverted;
    private SimEventObject? gazeAnimStatueNormal;   // second instances: wind-up + resolve
    private SimEventObject? gazeAnimStatueInverted;
    // The colossus at the arena centre, already standing when Tele-trouncing starts; its real
    // state history is replayed at scenario start.
    private SimEventObject? gravenStatue;
    private DamageSolver damage = null!;
    private readonly List<SimEventObject> activeArrows = [];
    // Every teleporter must be used by a Confused player; otherwise Kefka answers after Mystery
    // Magic with the lethal Light of Judgment.
    private int arrowsPlaced;
    private int arrowsSoakedByConfused;
    private readonly List<string> arrowSoakFailures = [];
    // The real fight's 8 invisible helpers, teleported in front of each cast. Spawned with Kefka
    // from the real NpcSpawn packet so the gimmick timelines have an actor to play on; a helper
    // the engine refuses falls back to the plain KefkaHelper (no action VFX). Nothing is spawned
    // at cast time: a packet spawn is pending for a few frames.
    private readonly SimEnemy?[] helpers = new SimEnemy?[8];
    private readonly List<SimCharacter> fireSpreadHits = [];
    private readonly UmadP1TeleTrouncingSettingsWindow settingsWindow = new();

    // Gimmick rows the helpers' hit VFX ride on. Double-trouble Trap's and the arrow bursts' are
    // drawn by hand and stay unpreloaded so a native play can't double them.
    private static readonly (ushort Id, string Key)[] ThunderFireTimelines =
    [
        (6090, "mon_sp/gimmick/z3oa_boss_gimmick03"),
        (7530, "mon_sp/gimmick/n4g1_boss_gimmick06"),
        (5823, "mon_sp/gimmick/e3d3_boss_gimmick01"),
    ];

    // Local to UmadZone.Origin (100, 0, 100). Full 3D, because the two tethers attach to
    // different parts of the colossus.
    internal static readonly Vector3 ConfusedStatuePos = new(-5f, 27f, -75f);
    internal static readonly Vector3 SleepStatuePos = new(7f, 8.5f, -57f);

    // BossMod's "look toward/away from" points (P1StatueGaze). Normal is ~9y from
    // SleepStatuePos, so it stays its own point.
    internal static readonly Vector3 GazeSourceInverted = new(-5f, 0f, -75f);
    internal static readonly Vector3 GazeSourceNormal = new(5.25f, 0f, -66f);

    // Spawn positions of the statue props, local to the origin.
    private static readonly Vector3 GazeStatueNormalPos = new(7f, 8.5f, -57f);           // world (107, 8.5, 43)
    private static readonly Vector3 GazeStatueNormalAnimPos = new(5.25f, 13.5f, -66f);   // world (105.25, 13.5, 34)
    private static readonly Vector3 GazeStatueInvertedPos = new(-5f, 27f, -75f);         // world (95, 27, 25)
    private static readonly Vector3 GazeStatueInvertedAnimPos = new(-5f, 12.5f, -75f);   // world (95, 12.5, 25)

    // The real EObjAnimation beats for the P1 statue, packed as (Param1 << 16) | Param2 as
    // BossMod does: appear ~t19, tether-fire t28.44, gaze wind-up t31.56, gaze resolve t41.46,
    // despawn t52.46.
    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimTetherFire = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimGazeWindUp = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimGazeResolve = (0x0100, 0x0200);
    private static readonly (uint State, uint Bitmask) EObjAnimDespawn = (0x0004, 0x0008);
    // The colossus's own beats: the two mid-phase stages before Tele-trouncing, and the
    // collapse/final pair at the P1 end.
    private static readonly (uint State, uint Bitmask) EObjAnimStatueStage2 = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimStatueStage3 = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimStatueCollapse = (0x0004, 0x0200);
    // Real offset of the collapse from the Tele-trouncing cast start.
    private const float StatueCollapseAt = 1.59f + 54.60f;
    // The colossus's pre-Tele-trouncing beats. Fired at t=0.2/0.4/0.6 they wrote only the static
    // state: the SharedGroup attaches ~1s after the spawn. TickStatueCatchUp waits for it, fires
    // them one timeline at a time, then unmutes the prop for its real collapse sounds.
    private static readonly (uint State, uint Bitmask)[] StatueCatchUpBeats =
        [EObjAnimAppear, EObjAnimStatueStage2, EObjAnimStatueStage3];
    private const float StatueCatchUpMaxWait = 8f;
    private int statueCatchUpStage;
    private float statueCatchUpReadyAt;

    // Graven Image is ModelChara Type=0: no baked-in mesh, the engine builds one from
    // CustomizeData. PartyCreator.WriteCustomize's Lalafell values, proven to load a body.
    // EyeShape (0x10) and Mouth (0x13) are written by offset: they are 1-based rows where the 0
    // an initializer leaves aborts the whole human model build, and their bitfield accessors
    // aren't verified in this CS build.
    private static readonly CustomizeData GravenImageCustomize = BuildGravenImageCustomize();

    private static unsafe CustomizeData BuildGravenImageCustomize()
    {
        var c = new CustomizeData
        {
            Race = 3, Tribe = 5, Sex = 1, BodyType = 1, Height = 50,
            Face = 1, Hairstyle = 1, SkinColor = 1,
            EyeColorRight = 1, EyeColorLeft = 1, HairColor = 1, HighlightsColor = 1, TattooColor = 1,
            Eyebrows = 1, Nose = 1, Jaw = 1, LipColorFurPattern = 1,
            MuscleMass = 50, TailShape = 1, BustSize = 50, FacePaintColor = 1,
        };
        var raw = (byte*)&c;
        raw[0x10] = 1; // EyeShape
        raw[0x13] = 1; // Mouth
        return c;
    }

    public void DrawSettings() => settingsWindow.Draw(FireAppearNow, FireWindUpNow);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP1TeleTrouncingState(settingsWindow.Overrides);
        damage = new DamageSolver(party);
        hazeHoldApplied = true;   // what MapController.TryLoad just applied from the phase

        // Game.Scenarios reuses one instance across runs; a stopped run's arrow wrappers still
        // hold their frozen positions, and TickArrows would bind someone against them on the
        // first tick.
        foreach (var staleArrow in activeArrows.ToArray()) RemoveArrow(staleArrow);
        arrowPushLock.Clear();
        arrowDwell.Clear();
        confusedWaitTimer.Clear();
        meleeDwellTimer.Clear();
        confusedChaseLastTarget.Clear();
        confusedChaseLogTimer.Clear();
        pendingArrowPlacements.Clear();
        arrowsPlaced = 0;
        arrowsSoakedByConfused = 0;
        arrowSoakFailures.Clear();
        DespawnHelpers();
        thunderReals.Clear();
        fireSpreadHits.Clear();
        // The props' SharedGroups outlive the EObj actor, so park each back in the hidden state
        // or a mid-scenario Reset leaves a statue standing until the zone reloads.
        DespawnProp(ref gazeStatueNormal);
        DespawnProp(ref gazeStatueInverted);
        DespawnProp(ref gazeAnimStatueNormal);
        DespawnProp(ref gazeAnimStatueInverted);
        DespawnProp(ref gravenStatue);

        world.Events.Add(0f, () =>
        {
            kefka = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: Constants.BNpcBaseId.Kefka, NameId: Constants.BNpcNameId.Kefka, Level: 100,
                Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true,
                Placement: new Placement(Vector3.Zero, -float.Pi)));
            for (var i = 0; i < helpers.Length; i++) helpers[i] = SpawnHelper();
            // Applied here rather than resolved from the earlier, unmodeled wave, so the
            // duration is what's left until the real expiry (0.09s before the stack lands).
            var support = party.Get(state.ConfettiStackSupport);
            support?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 22.69f);
            var dps = party.Get(state.ConfettiStackDps);
            dps?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 22.69f);

            confusedStatue = SpawnGravenImage(ConfusedStatuePos);
            sleepStatue = SpawnGravenImage(SleepStatuePos);
            gazeCasterInverted = SpawnGravenImage(GazeStatueInvertedAnimPos);
            gazeCasterNormal = SpawnGravenImage(GazeStatueNormalAnimPos);

            // The props, spawned as the real 0x00F5 packets spawn them: EObjId, LayoutId,
            // position, the hidden SharedTimelineState, plus the remaining packet bytes
            // (targetable byte, entity id, Arg2). The LayoutIds resolve to nothing on the client
            // (searched every LGB of this zone and Sigmascape's), so they are carried for
            // fidelity; EventId is the director link, and the two knobs are the in-game A/B.
            var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
            var propForceActive = settingsWindow.Overrides.PropsForceActive;
            DiagnosticLog.Info($"[UmadP1TeleTrouncing] Statue props: EventId=0x{propEventId:X} forceActive={propForceActive} staticVfxTest={settingsWindow.Overrides.PropsStaticVfxTest}.");
            gazeStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueNormal,
                LayoutId = Constants.EObjId.GazeStatueNormalLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB80u,
                TargetableStatus = 5,
                Arg2 = 0x004A0003u,
                Placement = new Placement(GazeStatueNormalPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
                MuteSound = true,
                ForceSharedGroupActive = propForceActive,
            });
            gazeStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueInverted,
                LayoutId = Constants.EObjId.GazeStatueInvertedLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB81u,
                TargetableStatus = 5,
                Arg2 = 0x00490003u,
                Placement = new Placement(GazeStatueInvertedPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
                MuteSound = true,
                ForceSharedGroupActive = propForceActive,
            });
            // The second instances: the real fight plays the gaze wind-up/resolve on these.
            gazeAnimStatueNormal = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueNormal,
                LayoutId = Constants.EObjId.GazeStatueNormalAnimLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB82u,
                TargetableStatus = 5,
                Arg2 = 0x00480003u,
                Placement = new Placement(GazeStatueNormalAnimPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
                MuteSound = true,
                ForceSharedGroupActive = propForceActive,
            });
            gazeAnimStatueInverted = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GazeStatueInverted,
                LayoutId = Constants.EObjId.GazeStatueInvertedAnimLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB83u,
                TargetableStatus = 5,
                Arg2 = 0x00470003u,
                Placement = new Placement(GazeStatueInvertedAnimPos, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
                MuteSound = true,
                ForceSharedGroupActive = propForceActive,
            });
            // The colossus, like its real pull-start spawn (state 4 = hidden).
            gravenStatue = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.GravenStatue,
                LayoutId = Constants.EObjId.GravenStatueLayoutId,
                EventId = propEventId,
                EntityId = 0x4000EB8Au,
                TargetableStatus = 5,
                Arg2 = Constants.EObjId.GravenStatueArg2,
                Placement = new Placement(Vector3.Zero, 0f),
                TimelineState = Constants.EObjId.GazeStatueSpawnState,
                MuteSound = true,
                ForceSharedGroupActive = propForceActive,
            });
            if (gravenStatue == null)
                DiagnosticLog.Warn("[UmadP1TeleTrouncing] The colossus (0x1EBFB4) failed to spawn -- EObj pool full?");
            statueCatchUpStage = 0;
            statueCatchUpReadyAt = 0f;

            if (gazeStatueNormal == null || gazeStatueInverted == null
                || gazeAnimStatueNormal == null || gazeAnimStatueInverted == null)
                DiagnosticLog.Warn(
                    $"[UmadP1TeleTrouncing] Gaze prop(s) failed to spawn (tether NE={gazeStatueNormal != null}, NW={gazeStatueInverted != null}; "
                    + $"anim NE={gazeAnimStatueNormal != null}, NW={gazeAnimStatueInverted != null}) -- "
                    + "falling back to the BNpc doppels + marker \"?\" for the gaze tell.");
        });

        // [19.35s] Real statue "power up", 16.91s before Mystery Magic's cast start; tether pair
        // only, as in the real EObjAnimation stream.
        world.Events.Add(19.43f, PlayStatueAppear);

        // The colossus's real collapse at the P1 end (its earlier beats are caught up by
        // TickStatueCatchUp).
        world.Events.Add(StatueCollapseAt, () => Beat(gravenStatue, EObjAnimStatueCollapse));

        // [1.59s] Tele-trouncing cast start; resolves at 6.58s.
        world.Events.Add(1.59f, () => kefka?.Cast(
            Constants.ActionId.TeleTrouncing, castSeconds: Constants.CastBar.Long, fireDelay: Constants.CastBar.FireDelay,
            animationLock: Constants.AnimationLock.TeleTrouncing, targetId: kefka?.GameObjectId));

        // Auto-attacks on the main tank at their real beats: none through the Confused window
        // and Mystery Magic.
        foreach (var at in new[] { 0.83f, 8.90f, 11.93f, 19.01f, 44.48f, 48.62f, 51.65f })
            world.Events.Add(at, () => kefka?.Cast(UmadConstants.ActionId.AutoAttack1, castSeconds: 0f,
                targetId: party.Get(PartyRole.MainTank)?.GameObjectId, animationLock: Constants.AnimationLock.AutoAttack));

        // [7.34s] Every role gets its 2 debuffs (the EffectResult lands 0.76s after the resolve).
        world.Events.Add(7.34f, ApplyDebuffs);

        // Teleporters drop at each debuff's own expiry (14.33s/17.32s). Two beats, a
        // replay-confirmed gap: the placement burst (8 helper casts) fires before the teleporter
        // becomes a walkable EObj.
        world.Events.Add(14.42f, () => SpawnArrowBursts(first: true));
        world.Events.Add(15.29f, SpawnArrowObjects);
        world.Events.Add(17.43f, () => SpawnArrowBursts(first: false));
        world.Events.Add(18.32f, SpawnArrowObjects);

        // [15.67s] Graven Image (2.7s bar, resolves 18.66s); the tethers appear as it lands.
        world.Events.Add(15.67f, () => kefka?.Cast(
            Constants.ActionId.GravenImage, castSeconds: Constants.CastBar.Short, fireDelay: Constants.CastBar.FireDelay,
            animationLock: Constants.AnimationLock.GravenImage, targetId: kefka?.GameObjectId));

        // [19.41s] Tethers land, one role category to each statue.
        world.Events.Add(19.41f, TetherStatues);

        // [20.80s] Unnamed 2.7s cast: its resolve sets Kefka's model state to 4 until Unk2BossP1
        // resolves.
        world.Events.Add(20.80f, () => kefka?.Cast(
            Constants.ActionId.Unk1BossP1, castSeconds: Constants.CastBar.Short, fireDelay: Constants.CastBar.FireDelay,
            animationLock: Constants.AnimationLock.Unk1, targetId: kefka?.GameObjectId));
        world.Events.Add(23.78f, () => kefka?.SetModelState(Constants.KefkaModelState.Unk1));

        // [22.78s] Confetti-3's stack hit, the one beat that drifts pull to pull (timed off the
        // earlier Double-trouble Trap cast). Pushes the ~3 players within 6y of each holder 14y
        // at 20y/s; the Ai holds its next move until the slide ends.
        world.Events.Add(22.78f, ResolveConfettiKnockback);

        // [28.40s] Tethers drop and the props play tether-fire; [28.46s] the Wills land;
        // [29.09s] the Confused/Sleep statuses (the EffectResult trails the hit by 0.63s), 6.00s
        // each.
        world.Events.Add(28.40f, PlayStatueTetherFire);
        world.Events.Add(28.46f, ResolveGravenWills);
        world.Events.Add(ConfusedChaseStart, ApplyConfusedAndSleepStatuses);

        // Release the forced chase when Confused's 6.00s runs out (see ReleaseConfusedControl).
        world.Events.Add(ConfusedChaseEnd, ReleaseConfusedControl);
        // Sleep expires on the same timer; the sleep-pose timeline doesn't self-clear.
        world.Events.Add(ConfusedChaseEnd, ReleaseSleepPose);

        // [32.78s] Unnamed instant: puts Kefka's model state back to 0. [34.87s] Kefka's
        // teleport action; he stays at centre, only the animation plays.
        world.Events.Add(32.78f, () =>
        {
            kefka?.Cast(Constants.ActionId.Unk2BossP1, castSeconds: 0f, animationLock: Constants.AnimationLock.Unk2, targetId: kefka?.GameObjectId);
            kefka?.SetModelState(Constants.KefkaModelState.Normal);
        });
        world.Events.Add(34.87f, () => kefka?.Cast(Constants.ActionId.TeleportP1, castSeconds: 0f, animationLock: Constants.AnimationLock.Teleport));

        // Every arrow is a steer-around obstacle from its spawn (SpawnArrowObjects) so the Ai's
        // Confetti/Tether moves path around them; cleared as Confused starts, since the chase's
        // Follow needs to walk straight into whatever arrow is in the way.
        world.Events.Add(ConfusedChaseStart, () => world.Obstacles.Clear());

        // [36.41s] Mystery Magic (4.7s bar), resolves 41.40s with the thunder lines.
        world.Events.Add(36.41f, () => kefka?.Cast(
            Constants.ActionId.MysteryMagic, castSeconds: Constants.CastBar.Long, fireDelay: Constants.CastBar.FireDelay,
            animationLock: Constants.AnimationLock.MysteryMagic, targetId: kefka?.GameObjectId));

        // Mystery Magic's three resolves land in a ~1s window (thunder 41.40s, gaze 41.49s, fire
        // 42.19s); the boss goes untargetable ~10s later.

        // [36.31s] Kefka's two orb headmarkers and the fire stack/spread marker, 0.1s before the
        // cast start; a lie flips the shown fire icon. The Ai reads state directly.
        world.Events.Add(36.31f, () =>
        {
            kefka?.AttachLockonVfx(state.FireIsLie ? Constants.LockonId.FireLie : Constants.LockonId.FireTruth, persistent: false);
            kefka?.AttachLockonVfx(state.ThunderIsLie ? Constants.LockonId.LightningLie : Constants.LockonId.LightningTruth, persistent: false);
            AttachFireMarkers();
        });

        // Thrumming Thunder III: on a truth set the 2 real lines cast 0xBA9F (a visible
        // telegraph) and nothing else; on a lie the reals cast 0xBAA1 (no telegraph, still
        // lethal) and the safe slots cast 0xBAA0 (a harmless telegraph). Hits 4.99s after the
        // cast start.
        world.Events.Add(36.41f, SpawnThunderLines);
        world.Events.Add(41.40f, ResolveThunderLines);

        // Statue gaze: the gazing prop plays the wind-up 4.8s before Mystery Magic's cast start
        // and the resolve 5.08s after it (BossMod's FutureTime(9.9f)).
        world.Events.Add(31.61f, PlayGazeTell);
        world.Events.Add(41.49f, ResolveGaze);

        // Flagrant Fire III, 0.79s after the thunder lines.
        world.Events.Add(42.19f, ResolveFire);

        // We just gonna add a fake lethal raidwide here to pretend we wipe here if arrows are not
        // soaked by the time confused runs out. The failure line is the real P1-end 0x05, pulled
        // forward to 1s after the fire so nobody waits for the wipe.
        world.Events.Add(ConfusedChaseEnd, JudgeArrowSoaks);
        world.Events.Add(43.19f, PlayFailedP1EndLine);
        world.Events.Add(44.19f, StartArrowSoakPunishment);
        world.Events.Add(44.69f, ResolveArrowSoakPunishment);
        world.Events.Add(44.47f, PlayFireLine);
        world.Events.Add(52.67f, PlayP1EndLine);

        // [52.68s] Boss goes untargetable; the P2 actors spawn 2.2s later in the real fight.
        world.Events.Add(52.68f, EndP1);
        world.Events.Add(54.20f, DespawnGazeProps);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP1TeleTrouncingState>)AiStrats[idx]).Run(state, world);
    }

    // Host and peer alike (see IScenario.RunInstanceEvents), so broadcast: false.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        ActionTimelinePreload.Preload(ThunderFireTimelines, "UmadP1TeleTrouncing");

        instanceWorld.Events.Add(3f, () => instanceWorld.Map.LogArena("3s"));
        instanceWorld.Events.Add(10f, () => instanceWorld.Map.LogArena("10s"));

        // Kefka's later lines depend on the arrow outcome, so the host schedules them in Run.
        instanceWorld.Events.Add(15.58f, () => instanceWorld.Map.DirectorUpdate(0x80000027U, 0x3U, 0x2U, 0x1BDBU, 0x400250DCU, broadcast: false));
    }

    private const float ArrowTriggerRadius = 2.0f;
    // Two arrows collide once their hitboxes touch.
    private const float ArrowCollideDistance = ArrowTriggerRadius * 2f;

    private const float ArrowPushDistance = 6f; // == UmadP1TeleTrouncingState.Step
    private const float ArrowBindDuration = 1.000f;
    private const float ArrowTriggerDwell = 0.35f;
    private readonly Dictionary<(SimEventObject Arrow, PartyRole Role), float> arrowDwell = [];

    // A fresh arrow spawns under whoever's debuff just expired, who would retrigger it at once.
    // 3.00s is the real gap between the wave-1 and wave-2 expiries: the most that is still
    // always spent walking to place the next one.
    private const float ArrowGraceDuration = 3.0f;
    private readonly Dictionary<SimEventObject, float> arrowGrace = [];

    // An untouched arrow expires on its own, flipping to EventState 7 this long after its spawn.
    private const float ArrowLifespanAfterSpawn = 18.687f;
    private readonly Dictionary<SimEventObject, float> arrowLifespan = [];

    // Confused roles walk toward whoever is closest; the arrows are meant to keep carrying them
    // away, but that is an outcome the player has to earn, so an unintercepted role reaches and
    // hits someone (TickConfusedChase). Sleep can't move at all. The window is Confused's own
    // apply-to-expiry span.
    private const float ConfusedChaseStart = 29.09f;
    private const float ConfusedChaseEnd = 29.09f + 6.000f;
    private const float ConfusedChaseSpeed = 6f;
    private readonly Dictionary<PartyRole, float> arrowPushLock = [];

    // FFXIV's standard melee engagement range; ActionTimeline row 112 is "battle/auto_attack1",
    // universal like the KO/revive rows Die() plays. public, like every id a peer replays: the
    // allowlist harvests declared constants (see SimAssets).
    private const float AutoAttackRange = 2.0f;
    public const ushort AutoAttackTimelineId = 112;

    // UNVERIFIED, from the user's description rather than a capture. The post-carry wait is what
    // keeps a completed chain harmless: its last carry lands with ~1.5s of Confused left.
    private const float ConfusedWaitDuration = 1.000f;
    private const float PostCarryWaitDuration = 2.000f;
    private readonly Dictionary<PartyRole, float> confusedWaitTimer = [];
    private const float MeleeDwellDuration = 1.000f;
    private readonly Dictionary<PartyRole, float> meleeDwellTimer = [];

    public void Tick(float delta, float elapsed)
    {
        TickArrows(delta, elapsed);
        TickGravenImageFallback();
        TickStatueCatchUp(elapsed);
        TickHazeHold();
        FaceMainTank();
        if (elapsed >= ConfusedChaseStart && elapsed <= ConfusedChaseEnd) TickConfusedChase(delta, elapsed);
    }

    // The real Kefka turns with his enmity target the whole phase (every cast packet's rotation
    // is the main tank's bearing).
    private void FaceMainTank()
    {
        if (kefka is not { } boss || party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        boss.Face(tank.Position);
    }

    private SimEnemy? SpawnHelper()
    {
        var real = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.KefkaHelper, NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
            Placement: new Placement(Vector3.Zero, 0f),
            NpcSpawnTemplate: UmadRealPackets.HelperNpcSpawn, PacketSpawnEnableDraw: true));
        if (real != null) return real;   // pending until the engine fills the slot (SimEnemy.PacketSpawnPending)
        DiagnosticLog.Warn("[UmadP1TeleTrouncing] real-packet helper spawn was refused -- falling back to the plain KefkaHelper (no action VFX).");
        return world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: UmadConstants.BNpcBaseId.KefkaHelper, NameId: UmadConstants.BNpcNameId.Kefka,
            Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
            Placement: new Placement(Vector3.Zero, 0f)));
    }

    // A helper the engine dropped after all (SimEnemy.PacketSpawnFailed) is replaced by the plain
    // KefkaHelper so its casts still land; one still in flight at cast time can't cast this frame.
    private SimEnemy? Helper(int index)
    {
        var helper = helpers[index];
        if (helper is { PacketSpawnFailed: true })
        {
            helper.Despawn();
            helper = helpers[index] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: UmadConstants.BNpcBaseId.KefkaHelper, NameId: UmadConstants.BNpcNameId.Kefka,
                Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
                Placement: new Placement(Vector3.Zero, 0f)));
        }
        if (helper is { PacketSpawnPending: true })
        {
            DiagnosticLog.Warn($"[UmadP1TeleTrouncing] helper {index} is still awaiting its packet actor at cast time -- this cast is lost.");
            return null;
        }
        return helper is { IsActive: true } ? helper : null;
    }

    private void DespawnHelpers()
    {
        for (var i = 0; i < helpers.Length; i++)
        {
            helpers[i]?.Despawn();
            helpers[i] = null;
        }
    }

    private void TickStatueCatchUp(float elapsed)
    {
        // Stages 0..2 = the beats; stage 3 = unmute once the last timeline has finished.
        if (gravenStatue == null || statueCatchUpStage > StatueCatchUpBeats.Length) return;
        if (!gravenStatue.IsSharedGroupAttached || elapsed < statueCatchUpReadyAt) return;
        var waitedOut = elapsed >= statueCatchUpReadyAt + StatueCatchUpMaxWait;
        if (statueCatchUpStage > 0 && gravenStatue.IsSharedGroupTimelinePlaying && !waitedOut) return;
        if (statueCatchUpStage == StatueCatchUpBeats.Length)
        {
            gravenStatue.MuteSound = false;
            statueCatchUpStage++;
            DiagnosticLog.Info($"[UmadP1TeleTrouncing] Colossus catch-up complete at {elapsed:F2}s -- sounds unmuted for the P1-end collapse.");
            return;
        }
        var beat = StatueCatchUpBeats[statueCatchUpStage];
        DiagnosticLog.Info($"[UmadP1TeleTrouncing] Colossus catch-up beat {statueCatchUpStage + 1}/{StatueCatchUpBeats.Length} (0x{beat.State:X}/0x{beat.Bitmask:X}) at {elapsed:F2}s{(waitedOut ? " (previous timeline still playing after the max wait)" : "")}.");
        Beat(gravenStatue, beat);
        statueCatchUpStage++;
        statueCatchUpReadyAt = elapsed + 0.5f;   // let the beat's timeline start before polling it
    }

    private bool hazeHoldApplied;

    private void TickHazeHold()
    {
        var want = settingsWindow.Overrides.HoldHaze;
        if (want == hazeHoldApplied) return;
        hazeHoldApplied = want;
        world.Map.SetFogHold(want ? UmadZone.P1Haze : null);
        DiagnosticLog.Info($"[UmadP1TeleTrouncing] P1 haze hold {(want ? "on" : "off")} (settings window).");
    }

    private void TickArrows(float delta, float elapsed)
    {
        // Counted down here, unconditionally: a role can pick up this lock before
        // ConfusedChaseStart, and decrementing it only inside TickConfusedChase never counted an
        // early lock down.
        foreach (var role in arrowPushLock.Keys.ToArray())
        {
            var remaining = arrowPushLock[role] - delta;
            if (remaining > 0f) { arrowPushLock[role] = remaining; continue; }
            arrowPushLock.Remove(role);
            confusedWaitTimer[role] = PostCarryWaitDuration;
        }

        if (activeArrows.Count == 0) return;

        // Two roles' spawn spots can land within a hitbox-width of each other; overlapping arrows
        // collide and remove each other (user's call).
        var snapshot = activeArrows.ToArray();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var a = snapshot[i];
            if (!activeArrows.Contains(a)) continue; // already poofed by an earlier pair this tick
            for (var j = i + 1; j < snapshot.Length; j++)
            {
                var b = snapshot[j];
                if (!activeArrows.Contains(b)) continue;
                var adx = a.Position.X - b.Position.X;
                var adz = a.Position.Z - b.Position.Z;
                if (adx * adx + adz * adz > ArrowCollideDistance * ArrowCollideDistance) continue;
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] Two arrows collided at t={elapsed:F2}s -- ({a.Position.X:F1},{a.Position.Z:F1}) and ({b.Position.X:F1},{b.Position.Z:F1}), {MathF.Sqrt(adx * adx + adz * adz):F1}y apart -- poofing both.");
                // No confirmed real VFX for this: the validated arrow-spawn burst rather than an
                // unvalidated path, which crashes on the file thread.
                world.SpawnOmen(Constants.VfxPath.TeleTrouncingArrowSpawnHit, new Placement(a.Position, 0f), Vector3.One, HitVfxDuration);
                NoteArrowSoakFailure($"two arrows collided and vanished unused at t={elapsed:F2}s -- ({a.Position.X:F1},{a.Position.Z:F1}) and ({b.Position.X:F1},{b.Position.Z:F1})");
                RemoveArrow(a);
                RemoveArrow(b);
                break;
            }
        }
        if (activeArrows.Count == 0) return;

        foreach (var arrow in activeArrows.ToArray())
        {
            var lifespan = arrowLifespan.GetValueOrDefault(arrow) - delta;
            if (lifespan <= 0f)
            {
                NoteArrowSoakFailure($"the arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}) expired unused at t={elapsed:F2}s ({ArrowLifespanAfterSpawn:F2}s after it spawned)");
                RetireArrow(arrow);
                continue;
            }
            arrowLifespan[arrow] = lifespan;

            var grace = arrowGrace.GetValueOrDefault(arrow) - delta;
            if (grace > 0f) { arrowGrace[arrow] = grace; continue; }
            arrowGrace.Remove(arrow);

            // No time gate: walking into an arrow early is a real failure mode. world.Obstacles
            // makes bots steer around arrows without preventing the interaction.
            foreach (var role in state.Debuffs.Keys)
            {
                if (party.Get(role) is not { } member) continue;
                var dx = member.Position.X - arrow.Position.X;
                var dz = member.Position.Z - arrow.Position.Z;
                if (dx * dx + dz * dz > ArrowTriggerRadius * ArrowTriggerRadius)
                {
                    arrowDwell.Remove((arrow, role));
                    continue;
                }
                // Counts through a carry, so a ride ending on the next arrow fires it as Bind lifts.
                var dwell = arrowDwell.GetValueOrDefault((arrow, role)) + delta;
                arrowDwell[(arrow, role)] = dwell;
                if (dwell < ArrowTriggerDwell || arrowPushLock.GetValueOrDefault(role) > 0f) continue;
                // Logged unconditionally with positions, so an early trigger can be checked
                // against the knockback's own logged spots.
                var confused = member.HasStatus(Constants.StatusId.Confused);
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] {role} used an arrow at t={elapsed:F2}s ({(elapsed < ConfusedChaseStart ? "BEFORE confused-chase start" : "during/after confused-chase")}, {(confused ? "Confused" : "NOT Confused")}) -- "
                    + $"member at ({member.Position.X:F1},{member.Position.Z:F1}), arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}).");
                if (confused) arrowsSoakedByConfused++;
                else NoteArrowSoakFailure($"{role} used the arrow at ({arrow.Position.X:F1},{arrow.Position.Z:F1}) at t={elapsed:F2}s while not Confused");
                UseArrow(role, member, arrow);
                RetireArrow(arrow);
                break;
            }
        }
    }

    // Both exit paths go through here so no stale entry survives; despawning is the caller's job.
    private void UntrackArrow(SimEventObject arrow)
    {
        activeArrows.Remove(arrow);
        arrowGrace.Remove(arrow);
        arrowLifespan.Remove(arrow);
        foreach (var key in arrowDwell.Keys.Where(k => k.Arrow == arrow).ToArray()) arrowDwell.Remove(key);
    }

    private bool ArrowSoakFailed => arrowSoakFailures.Count > 0 || arrowsSoakedByConfused < arrowsPlaced;

    private void NoteArrowSoakFailure(string what)
    {
        arrowSoakFailures.Add(what);
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow-soak failure {arrowSoakFailures.Count}: {what}.");
    }

    private void JudgeArrowSoaks()
    {
        var summary = $"{arrowsSoakedByConfused}/{arrowsPlaced} arrows soaked by Confused players";
        if (!ArrowSoakFailed)
        {
            DiagnosticLog.Info($"[UmadP1TeleTrouncing] Arrow soaks passed: {summary}.");
            return;
        }
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow soaks FAILED ({summary}): {string.Join("; ", arrowSoakFailures)}.");
    }

    private void PlayFireLine()
    {
        if (!ArrowSoakFailed) world.Map.DirectorUpdate(0x80000027U, 0xCU, 0x2U, 0x1BDBU, 0x400250DCU);
    }

    private void PlayFailedP1EndLine()
    {
        if (ArrowSoakFailed) world.Map.DirectorUpdate(0x80000027U, 0x5U, 0x2U, 0x1BDBU, 0x400250DCU);
    }

    private void PlayP1EndLine()
    {
        if (!ArrowSoakFailed) world.Map.DirectorUpdate(0x80000027U, 0xDU, 0x2U, 0x1BDBU, 0x400250DCU);
    }

    private void StartArrowSoakPunishment()
    {
        if (!ArrowSoakFailed) return;
        var summary = $"{arrowsSoakedByConfused}/{arrowsPlaced} arrows soaked by Confused players";
        world.Announce($"Not all arrows were soaked by confused players ({summary}): {string.Join("; ", arrowSoakFailures)}.");
    }

    private void ResolveArrowSoakPunishment()
    {
        if (!ArrowSoakFailed) return;
        foreach (var role in state.Debuffs.Keys)
        {
            if (party.Get(role) is { } member && member.IsAlive())
                member.Die("Not all arrows were soaked by confused players");
        }
        kefka?.SetTargetable(false);
        kefka?.SetVisible(false);
    }

    // Collisions and resets only: a used or expired arrow is retired, not removed.
    private void RemoveArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        arrow.Despawn();
    }

    private void RetireArrow(SimEventObject arrow)
    {
        UntrackArrow(arrow);
        switch (settingsWindow.Overrides.ArrowSoak)
        {
            case ArrowSoakMode.SetSharedTimelineState:
                arrow.PlayBeat(Constants.EObjState.TelePortentUsed, 0, PropBeatMode.SetSharedTimelineState);
                break;
            case ArrowSoakMode.Despawn:
                arrow.Despawn();
                break;
            default:
                arrow.DirectorEObjMod(Constants.EObjState.TelePortentUsed);
                break;
        }
    }

    // Confused roles chase their live nearest ally every tick (party.Find.Closest excludes the
    // dead, so a kill retargets on its own), except while arrowPushLock counts down: Follow and
    // PushInDirection share the destination field. A member killed earlier this tick is
    // skipped, or its body would keep sliding. The last-target and log-timer maps are
    // diagnostic only.
    private readonly Dictionary<PartyRole, SimCharacter> confusedChaseLastTarget = [];
    private readonly Dictionary<PartyRole, float> confusedChaseLogTimer = [];
    private const float ConfusedChaseLogInterval = 0.3f;

    private void TickConfusedChase(float delta, float elapsed)
    {
        foreach (var role in state.Debuffs.Keys)
        {
            if (UmadP1TeleTrouncingState.IsDps(role) != state.DpsGetsConfused) continue;
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            // Already decremented this tick by TickArrows.
            if (arrowPushLock.ContainsKey(role)) continue;

            // A full freeze, seeded when Confused first applies and after a kill.
            var waitRemaining = MathF.Max(0f, confusedWaitTimer.GetValueOrDefault(role) - delta);
            confusedWaitTimer[role] = waitRemaining;
            if (waitRemaining > 0f) continue;

            if (party.Find.Closest(member.Position, member) is not { } nearest) continue;

            var dx = member.Position.X - nearest.Position.X;
            var dz = member.Position.Z - nearest.Position.Z;
            var distSq = dx * dx + dz * dz;

            // Logged on every retarget, so a log shows whether the chase ever ran near an unused arrow.
            if (!ReferenceEquals(confusedChaseLastTarget.GetValueOrDefault(role), nearest))
            {
                confusedChaseLastTarget[role] = nearest;
                // Dwell doesn't carry over to a new target.
                meleeDwellTimer.Remove(role);
                var nearestRole = (nearest as ISimPartyMember)?.Role.ToString() ?? nearest.GetType().Name;
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] {role} (confused) retargets to {nearestRole} at t={elapsed:F2}s -- "
                    + $"from ({member.Position.X:F1},{member.Position.Z:F1}) to ({nearest.Position.X:F1},{nearest.Position.Z:F1}), dist={MathF.Sqrt(distSq):F1}y.");
            }

            if (distSq <= AutoAttackRange * AutoAttackRange)
            {
                // A full continuous MeleeDwellDuration first; a chase that closes the gap at the
                // last moment can run out of time here.
                var dwell = meleeDwellTimer.GetValueOrDefault(role) + delta;
                if (dwell < MeleeDwellDuration) { meleeDwellTimer[role] = dwell; continue; }

                // Nothing intercepted this player: they reach and hit whoever's nearest.
                member.PlayActionTimeline(AutoAttackTimelineId);
                nearest.Die($"Caught by {role} (confused) during Tele-trouncing");
                meleeDwellTimer.Remove(role);
                confusedWaitTimer[role] = ConfusedWaitDuration;
                continue;
            }

            meleeDwellTimer.Remove(role);
            // forced: Confusion takes control in the real fight, so this is the one follow that
            // may walk a real player's character.
            member.Follow(nearest, ConfusedChaseSpeed, forced: true);

            var logTimer = confusedChaseLogTimer.GetValueOrDefault(role) - delta;
            if (logTimer <= 0f)
            {
                confusedChaseLogTimer[role] = ConfusedChaseLogInterval;
                DiagnosticLog.Info(
                    $"[UmadP1TeleTrouncing] {role} (confused) chasing at t={elapsed:F2}s, "
                    + $"pos=({member.Position.X:F1},{member.Position.Z:F1}), dist={MathF.Sqrt(distSq):F1}y.");
            }
            else
            {
                confusedChaseLogTimer[role] = logTimer;
            }
        }
    }

    // The matching release of the forced Follow: TickFollow knows nothing about this window and
    // would keep walking the character, a real player's included, after Confused expired.
    private void ReleaseConfusedControl()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            if (UmadP1TeleTrouncingState.IsDps(role) != state.DpsGetsConfused) continue;
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.Follow(null);
        }
    }

    // ActionTimeline row 199 = "status/facial/sleep"; cleared on expiry, since the loop doesn't
    // self-clear the way the status does.
    public const ushort SleepPoseTimelineId = 199;

    private void ReleaseSleepPose()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            if (UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused) continue;
            if (party.Get(role) is { } member && member.IsAlive())
                member.ResetActionTimeline();
        }
    }

    // Follow(null) first, or TickFollow re-issues the chase under the carry.
    private void UseArrow(PartyRole role, SimCharacter member, SimEventObject arrow)
    {
        member.Follow(null);
        member.AddStatus(Constants.StatusId.Bind, ArrowBindDuration);
        arrowPushLock[role] = ArrowBindDuration;
        var heading = arrow.Rotation;
        var destination = arrow.Position + new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * ArrowPushDistance;
        (member as ISimPartyMember)?.CarryTo(destination, settingsWindow.Overrides.ArrowCarry);
    }

    // Heading as SimCast's facing calc: south=0, increasing clockwise toward east (north matches
    // Kefka's own logged heading).
    private static float Heading(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => float.Pi,
        TelePortentDirection.Down => 0f,
        TelePortentDirection.Right => float.Pi / 2f,
        TelePortentDirection.Left => -float.Pi / 2f,
        _ => 0f,
    };

    // Fire-and-forget hit effects only need to outlast the avfx's own animation.
    private const float HitVfxDuration = 2.0f;

    // Resolved by SpawnArrowBursts, consumed by SpawnArrowObjects ~0.8s later; the waves never overlap.
    private readonly Dictionary<PartyRole, Placement> pendingArrowPlacements = [];

    // Under the member's live position, not the Ai target: the real teleporter drops wherever the
    // player stands when the debuff expires. Locked in at the resolve and reused unchanged for
    // the EObj 0.8s later.
    private void SpawnArrowBursts(bool first)
    {
        pendingArrowPlacements.Clear();
        var helperIndex = 0;
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null) continue;
            var (at7, at10) = state.ExpiryDirections(role);
            var direction = first ? at7 : at10;
            var placement = new Placement(member.Position, Heading(direction));
            pendingArrowPlacements[role] = placement;
            world.SpawnOmen(Constants.VfxPath.TeleTrouncingArrowSpawnHit, placement, Vector3.One, HitVfxDuration);
            // The 0-damage placement tell, cast from a helper on the player as the real 8 are; off
            // Kefka it played the gimmick timeline at arena centre.
            if (Helper(helperIndex++) is { } caster)
            {
                caster.SetPosition(placement);
                caster.Cast(Constants.ActionId.TeleTrouncingArrowSpawn, castSeconds: 0f, targetId: member.GameObjectId,
                    animationLock: Constants.AnimationLock.Helper);
            }
        }
    }

    private void SpawnArrowObjects()
    {
        foreach (var placement in pendingArrowPlacements.Values)
        {
            // ActorControl 106 routes by entity id; an arrow left at 0 never receives it.
            var arrow = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = Constants.EObjId.TelePortent,
                Placement = placement,
                TargetableStatus = 5,
                EventId = Constants.EObjId.PropEventId,
                EntityId = Constants.Spawn.TelePortentEntityIdBase + (uint)arrowsPlaced,
                Arg2 = ((uint)(Constants.Spawn.TelePortentArg2Serial + arrowsPlaced) << 16) | 0x3U,
                HideAtState = (ushort)Constants.EObjState.TelePortentUsed,
            });
            if (arrow == null)
            {
                // Tagged here too so the symptom ("stepped on the arrow, nothing happened") and
                // its cause share a grep.
                DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Arrow failed to spawn at ({placement.Position.X:F1},{placement.Position.Z:F1}) -- it will never trigger for anyone.");
                continue;
            }
            activeArrows.Add(arrow);
            arrowsPlaced++;
            arrowGrace[arrow] = ArrowGraceDuration;
            arrowLifespan[arrow] = ArrowLifespanAfterSpawn;
            // Cleared in one shot as Confused starts (see Run).
            world.Obstacles.Add(new CircleObstacle(new Vector2(placement.Position.X, placement.Position.Z), ArrowTriggerRadius));
        }
        pendingArrowPlacements.Clear();
    }

    // The real BAA7 target list is exactly "everyone but the 2 holders".
    private const float ConfettiKnockbackRadius = 6f;

    private void ResolveConfettiKnockback()
    {
        // The "before" half for checking a post-knockback landing against a live arrow.
        var positions = new List<string>();
        foreach (var role in state.Debuffs.Keys)
            if (party.Get(role) is { } member)
                positions.Add($"{role}=({member.Position.X:F1},{member.Position.Z:F1})");
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Confetti-3 knockback resolving -- support stack={state.ConfettiStackSupport}, dps stack={state.ConfettiStackDps}, positions: {string.Join(", ", positions)}.");

        // The holder is never flung by their own knockback (explicit exclude). Cast before
        // party.Knockback: the real ability resolves as one packet, and the movement plays out
        // over 0.7s, so casting after put the hit-react a frame into the slide. FindWithinRadius
        // duplicates the radius check so the affected list is known first.
        ResolveConfettiStack(state.ConfettiStackSupport, 4);
        ResolveConfettiStack(state.ConfettiStackDps, 5);
    }

    // BossMod models this as a stack of exactly 4: short a body, everyone who stacked dies
    // except the holder (user's call). Exactly 4 is the only case that resolves as the knockback.
    private const int ConfettiStackRequiredCount = 4;

    private void ResolveConfettiStack(PartyRole holderRole, int helperIndex)
    {
        if (party.Get(holderRole) is not { } holder) return;
        var others = FindWithinRadius(holder.Position, ConfettiKnockbackRadius, holder);

        // The real BAA7 comes from a helper teleported onto the holder, with only the per-target
        // hit reaction. The trap-spring burst is attached to the holder so it renders at body
        // height, not flat on the floor.
        var caster = Helper(helperIndex);
        caster?.SetPosition(new Placement(holder.Position, 0f));
        foreach (var m in others)
        {
            caster?.Cast(Constants.ActionId.DoubleTroubleTrapStack, castSeconds: 0f, targetId: m.GameObjectId,
                animationLock: Constants.AnimationLock.Helper);
            m.AddStatus(UmadConstants.StatusId.MagicVulnerabilityUp, Constants.MagicVulnerabilityUpSeconds);
        }
        holder.AddVfx(Constants.VfxPath.DoubleTroubleTrapStackHit, persistent: false);

        if (others.Count + 1 != ConfettiStackRequiredCount)
        {
            DiagnosticLog.Info(
                $"[UmadP1TeleTrouncing] Confetti stack on {holderRole} enumerated {others.Count + 1}, not {ConfettiStackRequiredCount} -- failed stack, killing everyone but the holder.");
            foreach (var m in others) m.Die($"Failed Confetti stack ({others.Count + 1}/{ConfettiStackRequiredCount}) on {holderRole} during Tele-trouncing");
            return;
        }

        party.Knockback(holder.Position, Constants.KnockbackId.DoubleTroubleTrapStack, ConfettiKnockbackRadius, exclude: holder);
    }

    private List<SimCharacter> FindWithinRadius(Vector3 source, float radius, SimCharacter? exclude)
    {
        var radiusSq = radius * radius;
        var found = new List<SimCharacter>();
        foreach (var role in state.Debuffs.Keys)
        {
            if (party.Get(role) is not { } member || ReferenceEquals(member, exclude)) continue;
            var dx = member.Position.X - source.X;
            var dz = member.Position.Z - source.Z;
            if (dx * dx + dz * dz <= radiusSq) found.Add(member);
        }
        return found;
    }

    // The real Graven Image: the captured NpcSpawn packet through the engine's own handler,
    // hidden by its display flags, which is what the gaze/tether VFX bind to. The Lalafell
    // doppel stays as the fallback; its Pc-path 0.6 Height / 0.4 VfxScale put the gaze's bone
    // attach ~1y low at 40% size.
    private SimEnemy? SpawnGravenImage(Vector3 position)
    {
        var real = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.GravenImage, NameId: Constants.BNpcNameId.GravenImage, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Never,
            Placement: new Placement(position, 0f), NpcSpawnTemplate: UmadRealPackets.GravenImageNpcSpawn));
        if (real != null) return real;   // pending until the engine fills the slot (SimEnemy.PacketSpawnPending)
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Graven Image packet spawn at {position} was refused -- falling back to the Lalafell doppel.");
        return SpawnLalafellDoppel(position);
    }

    private SimEnemy? SpawnLalafellDoppel(Vector3 position)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.GravenImage, NameId: Constants.BNpcNameId.GravenImage, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true,
            Placement: new Placement(position, 0f), Customize: GravenImageCustomize));

    // A Graven Image whose packet spawn the engine dropped is replaced by the Lalafell doppel.
    private void TickGravenImageFallback()
    {
        ReplaceIfDropped(ref confusedStatue, ConfusedStatuePos);
        ReplaceIfDropped(ref sleepStatue, SleepStatuePos);
        ReplaceIfDropped(ref gazeCasterInverted, GazeStatueInvertedAnimPos);
        ReplaceIfDropped(ref gazeCasterNormal, GazeStatueNormalAnimPos);
    }

    private void ReplaceIfDropped(ref SimEnemy? actor, Vector3 position)
    {
        if (actor is not { PacketSpawnFailed: true }) return;
        DiagnosticLog.Warn($"[UmadP1TeleTrouncing] Graven Image packet spawn at {position} was dropped by the engine -- falling back to the Lalafell doppel.");
        actor.Despawn();
        actor = SpawnLalafellDoppel(position);
    }

    private void DespawnProp(ref SimEventObject? prop)
    {
        if (prop == null) return;
        if (prop.IsAlive) Beat(prop, EObjAnimDespawn);
        prop.Despawn();
        prop = null;
    }

    private void Beat(SimEventObject? prop, (uint State, uint Bitmask) beat)
        => prop?.PlayBeat(beat.State, beat.Bitmask, settingsWindow.Overrides.PropsBeatMode);

    // Settings-window buttons: the same beats the timeline fires at 19.35 / 31.63, on demand.
    private void FireAppearNow()
    {
        if (gazeStatueNormal == null && gazeStatueInverted == null && gravenStatue == null) return;
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Statue appear fired from the settings window.");
        Beat(gravenStatue, EObjAnimAppear);
        PlayStatueAppear();
    }

    private void FireWindUpNow()
    {
        if (state == null || GazingProp == null) return;
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Statue wind-up fired from the settings window.");
        PlayGazeTell();
    }

    private void PlayStatueAppear()
    {
        Beat(gazeStatueNormal, EObjAnimAppear);
        Beat(gazeStatueInverted, EObjAnimAppear);
        if (settingsWindow.Overrides.PropsStaticVfxTest)
        {
            // Diagnostic only: the props' VFX files standalone. mari3/nemu3 have emitters of their
            // own, mari2/nemu2 don't.
            world.SpawnOmen(Constants.VfxPath.StatueNemuriAppear, new Placement(GazeStatueNormalAnimPos, 0f), Vector3.One, 30f);
            world.SpawnOmen(Constants.VfxPath.StatueMariaAppear, new Placement(GazeStatueInvertedAnimPos, 0f), Vector3.One, 30f);
            world.SpawnOmen(Constants.VfxPath.StatueNemuriResolve, new Placement(GazeStatueNormalPos, 0f), Vector3.One, 30f);
            world.SpawnOmen(Constants.VfxPath.StatueMariaResolve, new Placement(GazeStatueInvertedPos, 0f), Vector3.One, 30f);
        }
    }

    private void TetherStatues()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused;
            var anchor = confused ? confusedStatue : sleepStatue;
            var member = party.Get(role);
            if (anchor != null && member != null)
                world.Tether(anchor, member, Constants.TetherId.GravenImage, duration: 8.99f);
        }
    }

    // Idyllic Will is a spread (BossMod's UniformStackSpread(0, 5)): each sleeper's 5y circle
    // kills anyone else caught in it, usually a force-marched Confused player.
    private const float IdyllicWillSpreadRadius = 5f;

    // Both gaze props play tether-fire 0.06s before the two Wills land.
    private void PlayStatueTetherFire()
    {
        Beat(gazeStatueNormal, EObjAnimTetherFire);
        Beat(gazeStatueInverted, EObjAnimTetherFire);
    }

    // The Confused/Sleep statuses come 0.63s later, when the real EffectResult applies them.
    private void ResolveGravenWills()
    {
        var confusedStatuePlayed = false;
        var sleepStatuePlayed = false;
        var sleepTargets = new List<SimCharacter>();
        foreach (var role in state.Debuffs.Keys)
        {
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused;
            var member = party.Get(role);

            // Cast before AddVfx: the real ability resolves as one packet, so the hit-react lands
            // with the vfx. The Cast exists for the per-target animation.
            if (confused && member != null) confusedStatue?.Cast(Constants.ActionId.IndulgentWill, castSeconds: 0f, targetId: member.GameObjectId, animationLock: Constants.AnimationLock.Helper);
            if (!confused && member != null) sleepStatue?.Cast(Constants.ActionId.IdyllicWill, castSeconds: 0f, targetId: member.GameObjectId, animationLock: Constants.AnimationLock.Helper);

            // Caster-side on the statue once, target-side on every hit player.
            if (confused)
            {
                if (!confusedStatuePlayed)
                {
                    confusedStatuePlayed = true;
                    confusedStatue?.AddVfx(Constants.VfxPath.IndulgentWillCasterShoot, persistent: false);
                    confusedStatue?.AddVfx(Constants.VfxPath.IndulgentWillCasterBurst, persistent: false);
                }
                member?.AddVfx(Constants.VfxPath.IndulgentWillTarget, persistent: false);
            }
            else
            {
                if (!sleepStatuePlayed)
                {
                    sleepStatuePlayed = true;
                    sleepStatue?.AddVfx(Constants.VfxPath.IdyllicWillCaster, persistent: false);
                }
                member?.AddVfx(Constants.VfxPath.IdyllicWillTarget, persistent: false);
                // Idyllic Will's hit carries the 0.96s Magic Vulnerability Up (Indulgent's doesn't).
                member?.AddStatus(UmadConstants.StatusId.MagicVulnerabilityUp, Constants.MagicVulnerabilityUpSeconds);
                if (member is { } m && m.IsAlive()) sleepTargets.Add(m);
            }
        }

        // Two clipping players are each in the other's circle, so both die.
        var killed = new HashSet<SimCharacter>();
        foreach (var sleeper in sleepTargets)
            foreach (var caught in FindWithinRadius(sleeper.Position, IdyllicWillSpreadRadius, sleeper))
                if (caught.IsAlive() && killed.Add(caught))
                    caught.Die($"Clipped {(sleeper as ISimPartyMember)?.Role.ToString() ?? "a sleeper"}'s Idyllic Will spread during Tele-trouncing");
        if (killed.Count > 0)
            DiagnosticLog.Info(
                $"[UmadP1TeleTrouncing] Idyllic Will spread: {killed.Count} player(s) clipped and died -- "
                + string.Join(", ", killed.Select(c => (c as ISimPartyMember)?.Role.ToString() ?? "?")));
    }

    // 6.00s each, at the real EffectResult time: the chase and the sleep pose start with the
    // status, not the hit.
    private void ApplyConfusedAndSleepStatuses()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var confused = UmadP1TeleTrouncingState.IsDps(role) == state.DpsGetsConfused;
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(confused ? Constants.StatusId.Confused : Constants.StatusId.Sleep, 6.000f);
            // No freeze on apply: confused players walk at once, and a freeze pushed the last arrow
            // past its expiry.
            if (!confused)
            {
                // Otherwise a slept doppel stands in its normal idle.
                member.PlayActionTimeline(SleepPoseTimelineId);
            }
        }
    }

    // A matching pair uses the primary id both times; a different pair's id form is fixed by
    // cycle order (primary First + secondary Second, never the reverse), while the 7s/10s split
    // is the independent coin flip (12 real resolves). A matching role's two AddStatus calls
    // share one id, so distinct sourceObject tags keep them from collapsing into one refreshed
    // stack; Kefka and the statue are just two stable ids.
    private void ApplyDebuffs()
    {
        foreach (var (role, (first, second)) in state.Debuffs)
        {
            var member = party.Get(role);
            var firstSource = kefka?.GameObjectId ?? default;
            var secondSource = confusedStatue?.GameObjectId ?? default;
            if (first == second)
            {
                member?.AddStatus(PrimaryStatusId(first), 7.000f, sourceObject: firstSource);
                member?.AddStatus(PrimaryStatusId(second), 10.000f, sourceObject: secondSource);
            }
            else
            {
                var firstAt7 = !state.DifferentPolarity[role];
                member?.AddStatus(PrimaryStatusId(first), firstAt7 ? 7.000f : 10.000f);
                member?.AddStatus(SecondaryStatusId(second), firstAt7 ? 10.000f : 7.000f);
            }
        }
    }

    // --- Mystery Magic (end of P1): line dodges + stack/spread + statue gaze ----------------

    // Thrumming Thunder III's 4 line anchors, local, each rect heading -pi/4 (len 40, width 10):
    // the same along=-20 / perp +-5,+-15 lattice P5 Flood's decode found. internal so the Ai
    // derives its copy from this array instead of a hand-synced duplicate.
    internal static readonly Placement[] ThunderAnchors =
    [
        new(new Vector3(24.75f, 0f, -3.54f), -MathF.PI / 4f),
        new(new Vector3(17.68f, 0f, -10.61f), -MathF.PI / 4f),
        new(new Vector3(10.61f, 0f, -17.68f), -MathF.PI / 4f),
        new(new Vector3(3.54f, 0f, -24.75f), -MathF.PI / 4f),
    ];

    // The real lines spawned this run, for ResolveThunderLines.
    private readonly List<(SimEnemy Helper, uint ActionId)> thunderReals = [];

    // What the players see: a lie flips the icon vs the real outcome. Shown-stack marks only the
    // 2 targets, shown-spread everyone.
    private void AttachFireMarkers()
    {
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null) continue;
            if (state.FireShowsStackIcon)
            {
                if (role == state.FireStackSupport || role == state.FireStackDps)
                    member.AttachLockonVfx(Constants.LockonId.FireStack, persistent: false);
            }
            else
            {
                member.AttachLockonVfx(Constants.LockonId.FireSpread, persistent: false);
            }
        }
    }

    // Reals: 2 of the 4 slots by (i + ThunderRealOffset) parity. Fakes only telegraph on a lie,
    // identically to a real line (user's call: the only distinguisher is Kefka's thunder orb).
    private void SpawnThunderLines()
    {
        thunderReals.Clear();
        // A truthful set is 2x Real1 and nothing else; a lying set switches the reals to Real2
        // and adds Fake at the other slots.
        var realId = state.ThunderIsLie ? Constants.ActionId.ThrummingThunderReal2 : Constants.ActionId.ThrummingThunderReal1;
        var lines = 0;
        for (var i = 0; i < ThunderAnchors.Length; i++)
        {
            var real = (i + state.ThunderRealOffset) % 2 == 0;
            if (!real && !state.ThunderIsLie) continue; // truth: fakes don't appear at all
            var placement = ThunderAnchors[i].MulX(state.ThunderOrientation).MulRot(state.ThunderOrientation);
            if (Helper(i) is not { } helper) continue;
            helper.SetPosition(placement);
            lines++;
            var actionId = real ? realId : Constants.ActionId.ThrummingThunderFake;
            helper.Cast(actionId, castSeconds: Constants.CastBar.Long, fireDelay: Constants.CastBar.FireDelay, animationLock: Constants.AnimationLock.Helper);
            if (real) thunderReals.Add((helper, actionId));
        }
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Thunder lines: offset={state.ThunderRealOffset} orient={state.ThunderOrientation:F0} lie={state.ThunderIsLie} "
            + $"-> {lines} telegraph(s), {thunderReals.Count} real.");
    }

    private void ResolveThunderLines()
    {
        foreach (var (helper, actionId) in thunderReals)
            damage.Resolve(helper, actionId, [DamageType.Lethal], []);
    }

    // The doppel that casts and carries the marker, and the real prop that plays the statue
    // animation: the second instance of the gazing type, matching the real beat routing.
    private SimEnemy? GazingStatue => state.GazeInverted ? gazeCasterInverted : gazeCasterNormal;
    private SimEventObject? GazingProp => state.GazeInverted ? gazeAnimStatueInverted : gazeAnimStatueNormal;

    // The ~10s "eyes light up" wind-up; on the real 0x1EBFBF the prop's animation draws the "?".
    // Which side glows is also a real tell (NW = look toward, NE = look away).
    private void PlayGazeTell()
    {
        Beat(GazingProp, EObjAnimGazeWindUp);
        if (settingsWindow.Overrides.PropsStaticVfxTest)
            world.SpawnOmen(
                state.GazeInverted ? Constants.VfxPath.StatueMariaWindUp : Constants.VfxPath.StatueNemuriWindUp,
                new Placement(state.GazeInverted ? GazeStatueInvertedAnimPos : GazeStatueNormalAnimPos, 0f),
                Vector3.One, 15f);

        // Only the caster's own wind-up vfx: the "?" is the prop's timeline. The eye lockon
        // stand-ins showed as a stray marker once the caster became the model-less actor.
        GazingStatue?.AddVfx(Constants.VfxPath.GazeWindUp, duration: 10f, persistent: false);
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Gaze tell: {(state.GazeInverted ? "INVERTED (\"?\" -- look TOWARD the NW confused statue)" : "NORMAL (no \"?\" -- look AWAY from the NE sleep statue)")} "
            + $"(prop {(GazingProp != null ? "playing wind-up anim" : "absent -- marker only")}).");
    }

    private void ResolveGaze()
    {
        Beat(GazingProp, EObjAnimGazeResolve);

        var statue = GazingStatue;
        var source = state.GazeInverted ? GazeSourceInverted : GazeSourceNormal;
        statue?.Cast(state.GazeInverted ? Constants.ActionId.AveMaria : Constants.ActionId.IndolentWill,
            castSeconds: 0f, targetId: statue.GameObjectId);
        statue?.AddVfx(state.GazeInverted ? Constants.VfxPath.AveMariaHit : Constants.VfxPath.IndolentWillHit, persistent: false);
        var killed = damage.ResolveGaze(IPositioned.From(source), lookAway: !state.GazeInverted);
        DiagnosticLog.Info($"[UmadP1TeleTrouncing] Gaze resolved (lookAway={!state.GazeInverted}) -- {killed.Count} died.");
    }

    private void ResolveFire()
    {
        if (state.FireIsStack) ResolveFireStack();
        else ResolveFireSpread();
    }

    // A 5y circle on every player: a clean spread hurts nobody, two coverings is lethal.
    private void ResolveFireSpread()
    {
        fireSpreadHits.Clear();
        var helperIndex = 0;
        foreach (var role in state.Debuffs.Keys)
        {
            var member = party.Get(role);
            if (member == null || !member.IsAlive()) continue;
            if (Helper(helperIndex++) is not { } helper) continue;
            helper.SetPosition(new Placement(member.Position, 0f));
            helper.Cast(Constants.ActionId.FlagrantFireSpread, castSeconds: 0f, targetId: member.GameObjectId, animationLock: Constants.AnimationLock.Helper);
            fireSpreadHits.AddRange(damage.Resolve(helper, Constants.ActionId.FlagrantFireSpread,
                [DamageType.Lethal], [], killTargets: false));
        }
        foreach (var grp in fireSpreadHits.GroupBy(c => c))
        {
            var count = grp.Count();
            grp.Key.AddStatus(UmadConstants.StatusId.MagicVulnerabilityUp, Constants.MagicVulnerabilityUpSeconds);
            for (var i = 0; i < count; i++)
                damage.ApplyDamage(grp.Key, 0.6f, Constants.ActionId.FlagrantFireSpread, "fire spread",
                    lethal: count >= 2 && i == count - 1);
        }
        fireSpreadHits.Clear();
        DiagnosticLog.Info("[UmadP1TeleTrouncing] Flagrant Fire III: SPREAD resolved.");
    }

    // Two stack points, each needing 4 bodies; short a body and everyone in it dies, a full
    // stack is survivable.
    private void ResolveFireStack()
    {
        ResolveOneFireStack(state.FireStackSupport, 2);
        ResolveOneFireStack(state.FireStackDps, 3);
        DiagnosticLog.Info(
            $"[UmadP1TeleTrouncing] Flagrant Fire III: STACK resolved (support={state.FireStackSupport}, dps={state.FireStackDps}).");
    }

    private void ResolveOneFireStack(PartyRole holderRole, int helperIndex)
    {
        if (party.Get(holderRole) is not { } holder || !holder.IsAlive()) return;
        // Helper->players: a helper on the holder so the VFX lands at the stack point.
        var caster = Helper(helperIndex);
        caster?.SetPosition(new Placement(holder.Position, 0f));
        caster?.Cast(Constants.ActionId.FlagrantFireStack, castSeconds: 0f, targetId: holder.GameObjectId, animationLock: Constants.AnimationLock.Helper);
        damage.Resolve(holder, Constants.ActionId.FlagrantFireStack, [DamageType.Magic],
            [(UmadConstants.StatusId.MagicVulnerabilityUp, Constants.MagicVulnerabilityUpSeconds)], stackMinTargets: 4);
    }

    private void EndP1()
    {
        thunderReals.Clear();
        // A failed set keeps Kefka up for the deaths; ResolveArrowSoakPunishment hides him.
        if (!ArrowSoakFailed)
        {
            kefka?.SetTargetable(false);
            kefka?.SetVisible(false);
        }
        confusedStatue?.SetVisible(false);
        sleepStatue?.SetVisible(false);
        // The EObj slots are released a beat later by DespawnGazeProps so the animation plays.
        Beat(gazeStatueNormal, EObjAnimDespawn);
        Beat(gazeStatueInverted, EObjAnimDespawn);
        Beat(gazeAnimStatueNormal, EObjAnimDespawn);
        Beat(gazeAnimStatueInverted, EObjAnimDespawn);
        world.Obstacles.Clear();
        DiagnosticLog.Info("[UmadP1TeleTrouncing] P1 complete -- boss untargetable, mechanic finished.");
    }

    private void DespawnGazeProps()
    {
        DespawnHelpers();
        gazeStatueNormal?.Despawn();
        gazeStatueNormal = null;
        gazeStatueInverted?.Despawn();
        gazeStatueInverted = null;
        gazeAnimStatueNormal?.Despawn();
        gazeAnimStatueNormal = null;
        gazeAnimStatueInverted?.Despawn();
        gazeAnimStatueInverted = null;
    }

    private static ushort PrimaryStatusId(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpPrimary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownPrimary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightPrimary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftPrimary,
        _ => Constants.StatusId.TelePortentUpPrimary,
    };

    private static ushort SecondaryStatusId(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Up => Constants.StatusId.TelePortentUpSecondary,
        TelePortentDirection.Down => Constants.StatusId.TelePortentDownSecondary,
        TelePortentDirection.Right => Constants.StatusId.TelePortentRightSecondary,
        TelePortentDirection.Left => Constants.StatusId.TelePortentLeftSecondary,
        _ => Constants.StatusId.TelePortentUpSecondary,
    };
}
