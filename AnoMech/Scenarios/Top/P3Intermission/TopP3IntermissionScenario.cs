using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P3Intermission.TopP3IntermissionConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P3Intermission;

// From P2's kill to Final Omega turning targetable. Nobody heals, and any hit a player should not
// take kills them: a ring, a hand, the void zone, someone else's spread, a second hit, a stack
// taken alone.
public sealed class TopP3IntermissionScenario : IMultiplayerReplayable
{
    public string Name => "Intermission";
    public IPhase Phase => TopZone.P3Intermission;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 142.91f;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP3IntermissionSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP3IntermissionAi(automarkers: true), new TopP3IntermissionAi(automarkers: false)];

    private static readonly Placement Centre = new(Vector3.Zero, MathF.PI);

    private enum Fodder { Spread, Stack }

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP3IntermissionState state = null!;
    private DamageSolver damage = null!;
    private TopHelpers? helpers;
    private TopHelpers? omegaFHelper;
    private SimEnemy? waveRepeaterCaster;
    private SimEnemy? centreOmega;
    private SimEnemy? northOmega;
    private SimEnemy? finalOmega;
    private SimEventObject? voidzone;
    private readonly SimEnemy?[] arms = new SimEnemy?[6];
    private SimCharacter? armsTarget;
    private readonly Dictionary<SimCharacter, Fodder> fodder = new();
    private readonly HashSet<SimCharacter> doomed = [];
    private readonly HashSet<SimCharacter> jumpVictims = [];
    private float glideStart;
    private bool gliding;
    private bool voidzoneActive;
    private bool trackingMainTank;

    public TopP3IntermissionState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP3IntermissionState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        helpers = null;
        omegaFHelper = null;
        waveRepeaterCaster = null;
        centreOmega = null;
        northOmega = null;
        finalOmega = null;
        voidzone = null;
        Array.Clear(arms);
        armsTarget = null;
        fodder.Clear();
        doomed.Clear();
        jumpVictims.Clear();
        gliding = false;
        voidzoneActive = false;
        trackingMainTank = false;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP3IntermissionState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnP2Omegas);
        world.Events.Add(0f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 16, "TopP3Intermission"));
        world.Events.Add(0f, () => omegaFHelper = new TopHelpers(world, BNpcNameId.OmegaF, 1, "TopP3Intermission"));
        world.Events.Add(0.1f, TakeDownedForms);
        world.Events.Add(0.68f, StopLaserShower);
        world.Events.Add(0.77f, () => BossVisual(northOmega, ActionId.DieF, 2.1f));
        world.Events.Add(2.24f, PlaySuperfluidAnimation);
        world.Events.Add(2.42f, TurnSuperfluid);
        world.Events.Add(2.82f, () => BossVisual(northOmega, ActionId.Unknown7b17, 4.1f));
        world.Events.Add(3.45f, () => northOmega?.SetModelState(0x0B));
        world.Events.Add(6.97f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.ImitationOfMortalForm, 5000));
        world.Events.Add(7.19f, SpawnFinalOmega);
        world.Events.Add(7.19f, SpawnArms);
        world.Events.Add(10.00f, () => world.Map.AddEffect(0x00200010, 0x01));
        world.Events.Add(10.00f, ApplyDebuffs);
        world.Events.Add(10.09f, CastWaveRepeater);
        world.Events.Add(10.09f, MergeIntoTheCentre);
        world.Events.Add(10.71f, StartGlide);
        world.Events.Add(11.20f, () => centreOmega?.FadeOut());
        world.Events.Add(12.51f, () => centreOmega?.Despawn());
        world.Events.Add(13.03f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.ReconfigurationSequence, 5000));
        world.Events.Add(13.08f, () => PlaceArms(0));
        world.Events.Add(13.12f, () => BossVisual(northOmega, ActionId.IntermissionMergeStart, 3.1f));
        world.Events.Add(13.13f, () => RevealArms(0));
        world.Events.Add(13.75f, () => northOmega?.SetModelState(0x1B));
        world.Events.Add(14.21f, () => ArmsTarget(0));
        world.Events.Add(15.06f, ResolveWaveRepeaterCircle);
        world.Events.Add(16.06f, () => PlaceArms(1));
        world.Events.Add(16.06f, SpawnVoidzone);
        world.Events.Add(16.15f, RevealFinalOmega);
        world.Events.Add(16.15f, () => northOmega?.FadeOut());
        world.Events.Add(16.17f, () => RevealArms(1));
        world.Events.Add(17.16f, () => WaveRepeaterRing(ActionId.WaveRepeater2, 6f));
        world.Events.Add(17.28f, () => ArmsTarget(1));
        world.Events.Add(17.35f, () => northOmega?.Despawn());
        world.Events.Add(18.23f, CastWaveRepeater);
        world.Events.Add(19.09f, () => Talk(BNpcNameId.OmegaIntermission, BattleTalkId.BlipBloopBleep, 5000));
        world.Events.Add(19.09f, () => world.Map.AddEffect(0x00020004, 0x0B));
        world.Events.Add(19.09f, () => world.Map.AddEffect(0x00080004, 0x0A));
        world.Events.Add(19.21f, () => WaveRepeaterRing(ActionId.WaveRepeater3, 12f));
        world.Events.Add(21.10f, () => world.Map.AddEffect(0x04000004, 0x01));
        world.Events.Add(21.27f, () => WaveRepeaterRing(ActionId.WaveRepeater4, 18f));
        world.Events.Add(22.07f, () => world.SetWeather(79, 0.1f));
        world.Events.Add(23.21f, ResolveWaveRepeaterCircle);
        world.Events.Add(25.15f, () => CastColossalBlows(0));
        world.Events.Add(25.31f, () => WaveRepeaterRing(ActionId.WaveRepeater2, 6f));
        world.Events.Add(27.11f, () => ResolveColossalBlows(0));
        world.Events.Add(27.15f, () => Talk(BNpcNameId.OmegaFinal, BattleTalkId.ExperimentConcluded, 6000));
        world.Events.Add(27.36f, () => WaveRepeaterRing(ActionId.WaveRepeater3, 12f));
        world.Events.Add(27.67f, () => CastColossalBlows(1));
        world.Events.Add(29.11f, ResolveSniperCannons);
        world.Events.Add(29.21f, () => ArmsLeave(0));
        world.Events.Add(29.42f, () => WaveRepeaterRing(ActionId.WaveRepeater4, 18f));
        world.Events.Add(29.59f, () => ArmsDropTarget(0));
        world.Events.Add(29.63f, () => ResolveColossalBlows(1));
        world.Events.Add(31.60f, EndVoidzone);
        world.Events.Add(31.76f, () => ArmsLeave(1));
        world.Events.Add(32.15f, () => HideArms(0));
        world.Events.Add(32.64f, () => ArmsDropTarget(1));
        world.Events.Add(33.18f, () => finalOmega?.SetTargetable(true));
        world.Events.Add(34.09f, () => trackingMainTank = true);
        world.Events.Add(34.20f, () => finalOmega?.SetVisibleInEnemyList(true));
        world.Events.Add(34.60f, TargetMainTank);
        world.Events.Add(34.71f, () => HideArms(1));
    }

    // Host and peer alike: P3's track takes over from its top as the merge begins.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP3Intermission");
        instanceWorld.Events.Add(13.03f, () => Natives.Bgm.Switch(BgmId.TopP3));
    }

    public void Tick(float delta, float elapsed)
    {
        GlideIntoTheCentre();
        TurnToTheMainTank(delta);
        DetonateDeadHolders();
        KillInVoidzone();
    }

    private void Talk(uint speakerNameId, uint textId, uint durationMs) => world.Map.BattleTalk(speakerNameId, textId, durationMs);

    private static void BossVisual(SimEnemy? boss, uint actionId, float animationLock)
        => boss?.Cast(actionId, castSeconds: 0f, targetId: boss.GameObjectId, animationLock: animationLock);

    // The real headers: a helper is its own animation target, except for a player's debuff, which
    // plays on that player.
    private void HelperEffect(Placement at, uint actionId, float animationLock, SimCharacter? onTarget = null)
    {
        if (helpers?.Next(at) is not { } helper) return;
        helper.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0, rotation: at.Rotation, position: at.Position,
            animationTargetId: onTarget?.GameObjectId ?? helper.GameObjectId, actionTargetId: onTarget?.GameObjectId);
    }

    // P2's two Omegas as P2 ends: the one downed first at the centre, the other at north, still
    // in Omega-F's form until its kill plays out, and still targetable and casting P2's enrage
    // until the kill registers.
    private void SpawnP2Omegas()
    {
        centreOmega = SpawnP2Omega(BNpcBaseId.OmegaF, Vector3.Zero, 0x31, targetable: false);
        northOmega = SpawnP2Omega(BNpcBaseId.OmegaM, NorthOmega, 0x10, targetable: true);
        northOmega?.Cast(ActionId.LaserShower, castSeconds: 59.7f);
        northOmega?.SkipCastAhead(54.84f);
    }

    private SimEnemy? SpawnP2Omega(uint baseId, Vector3 position, byte modeAttributeFlags, bool targetable) => world.SpawnEnemy(new EnemySpawnConfig(
        InitialModeAttributeFlags: modeAttributeFlags, BNpcBaseId: baseId, NameId: BNpcNameId.OmegaM_1DD3, Level: Level,
        Targetable: targetable, EnemyList: EnemyListMode.Never, Placement: new Placement(position, 0f)));

    private void StopLaserShower()
    {
        northOmega?.CancelCast();
        northOmega?.SetTargetable(false);
    }

    private void TakeDownedForms()
    {
        centreOmega?.AddStatus(StatusId.Superfluid, stacks: 493, overrideStacks: true);
        centreOmega?.SetModelState(0x0B);
        northOmega?.AddStatus(StatusId.OmegaF, stacks: 491, overrideStacks: true);
        northOmega?.SetModelState(0x0B);
    }

    // On a helper of its own, named for Omega-F and its own target.
    private void PlaySuperfluidAnimation()
    {
        if (omegaFHelper?.At(0, new Placement(NorthOmega, 0f)) is not { } helper) return;
        helper.NativeActionEffect(ActionId.SuperfluidAnimationF, 2.1f, (ushort)ActionId.SuperfluidAnimationF, 0, ActionType.Action, 0, rotation: 0f, position: NorthOmega,
            animationTargetId: helper.GameObjectId, actionTargetId: helper.GameObjectId);
    }

    private void TurnSuperfluid()
    {
        northOmega?.SetModelState(0x05);
        northOmega?.RemoveStatus(StatusId.OmegaF);
        northOmega?.AddStatus(StatusId.Superfluid, stacks: 493, overrideStacks: true);
    }

    // IntermissionTeleportM's only target is its caster, with no animation target.
    private void MergeIntoTheCentre()
    {
        centreOmega?.Cast(ActionId.IntermissionTeleportF, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f);
        northOmega?.NativeActionEffect(ActionId.IntermissionTeleportM, 1.1f, (ushort)ActionId.IntermissionTeleportM, 0, ActionType.Action, 0,
            rotation: northOmega.Rotation, position: Vector3.Zero, actionTargetId: northOmega.GameObjectId);
    }

    private void StartGlide()
    {
        glideStart = world.Events.Elapsed;
        gliding = true;
    }

    // The server slides it in while the teleport's animation plays; a walk would wait out the lock.
    private void GlideIntoTheCentre()
    {
        if (!gliding || northOmega == null) return;
        var progress = MathF.Min(1f, (world.Events.Elapsed - glideStart) / GlideSeconds);
        northOmega.SetPosition(new Placement(Vector3.Lerp(NorthOmega, Vector3.Zero, progress), northOmega.Rotation));
        gliding = progress < 1f;
    }

    private void SpawnFinalOmega()
    {
        finalOmega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal, NameId: BNpcNameId.OmegaFinal, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, IsVisible: false,
            Placement: new Placement(Vector3.Zero, 0f)));
        finalOmega?.SetMaxHealth(FinalOmegaMaxHealth);
        finalOmega?.SetHealth(FinalOmegaMaxHealth);
    }

    private void RevealFinalOmega()
    {
        finalOmega?.SetVisible(true);
        BossVisual(finalOmega, ActionId.IntermissionMergeEnd, 3.1f);
    }

    // It swings round to its tank rather than snapping, and keeps facing it as the tank moves.
    private void TurnToTheMainTank(float delta)
    {
        if (trackingMainTank) TopBoss.TurnToMainTank(party, finalOmega, TopBoss.FinalOmegaTurnSpeed, delta);
    }

    private void TargetMainTank()
    {
        if (finalOmega is { IsActive: true } omega && party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive())
            omega.SetTarget(tank, follow: false);
    }

    // All six wait hidden in the centre; each set is moved out to its spots and appears there.
    private void SpawnArms()
    {
        for (var spot = 0; spot < arms.Length; spot++)
        {
            var left = state.LeftArms[spot];
            arms[spot] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: left ? BNpcBaseId.LeftArmUnit : BNpcBaseId.RightArmUnit,
                NameId: left ? BNpcNameId.LeftArmUnit : BNpcNameId.RightArmUnit,
                Level: Level, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false,
                Placement: Centre));
        }
    }

    private static Placement ArmPlacement(int spot)
    {
        var at = TopCompass.Point(ArmRadius, TopP3IntermissionState.SpotBearing(spot));
        return new Placement(new Vector3(at.X, 0f, at.Y), 0f).Face(Vector3.Zero);
    }

    private void PlaceArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.SetPosition(ArmPlacement(spot));
    }

    private void RevealArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
        {
            arms[spot]?.SetVisible(true);
            arms[spot]?.PlayActionTimeline(state.LeftArms[spot] ? TimelineId.Spawn2 : TimelineId.Spawn);
        }
    }

    private void ArmsLeave(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.PlayActionTimeline(state.LeftArms[spot] ? TimelineId.WarpOut2 : TimelineId.WarpOut);
    }

    private void HideArms(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.SetVisible(false);
    }

    // UNVERIFIED stand-in: the real arms go for whoever tops their enmity, which varies by pull
    // and has never been a tank; the sim keeps no enmity, so both sets take one non-tank at random.
    private void ArmsTarget(int set)
    {
        if (armsTarget is not { } target || !target.IsAlive())
        {
            var candidates = party.ActiveMembers().Where(member => member is not ISimPartyMember { Role: var role } || !role.IsTank()).ToList();
            if (candidates.Count == 0) return;
            armsTarget = target = candidates[state.Rng.NextInt(candidates.Count)];
        }
        foreach (var spot in state.SetSpots(set))
            if (arms[spot] is { IsActive: true } arm) arm.SetTarget(target, follow: false);
    }

    private void ArmsDropTarget(int set)
    {
        foreach (var spot in state.SetSpots(set))
            if (arms[spot] is { IsActive: true } arm) arm.SetTarget(null);
    }

    // The bar and the hit are sent apart: the hit's animation plays on the arm, but it names no
    // target, which a Cast() cannot send.
    private void CastColossalBlows(int set)
    {
        foreach (var spot in state.SetSpots(set))
            arms[spot]?.NativeCast(ActionId.ColossalBlow, ActionType.Action, 0f, 1.7f, false, targetId: arms[spot]?.GameObjectId, animationLock: 2.1f, fireDelay: 0.26f);
    }

    private void ResolveColossalBlows(int set)
    {
        foreach (var spot in state.SetSpots(set))
        {
            if (arms[spot] is { IsActive: true } arm)
                arm.NativeActionEffect(ActionId.ColossalBlow, 2.1f, (ushort)ActionId.ColossalBlow, 0, ActionType.Action, 0,
                    rotation: arm.Rotation, position: arm.Position, animationTargetId: arm.GameObjectId);
            damage.Resolve(TopPositioned.From(ArmPlacement(spot)), ActionId.ColossalBlow, [DamageType.Lethal], []);
        }
    }

    // The bar names no target, which a Cast() cannot send; the hit goes out on its own when the
    // circle resolves.
    private void CastWaveRepeater()
    {
        waveRepeaterCaster = helpers?.Next(Centre);
        waveRepeaterCaster?.NativeCast(ActionId.WaveRepeater1, ActionType.Action, 0f, 4.7f, false, position: Vector3.Zero, animationLock: 2.1f, fireDelay: 0.28f);
    }

    private void ResolveWaveRepeaterCircle()
    {
        waveRepeaterCaster?.NativeActionEffect(ActionId.WaveRepeater1, 2.1f, (ushort)ActionId.WaveRepeater1, 0, ActionType.Action, 0,
            rotation: waveRepeaterCaster.Rotation, position: Vector3.Zero);
        waveRepeaterCaster = null;
        damage.Resolve(TopPositioned.From(Centre), ActionId.WaveRepeater1, [DamageType.Lethal], []);
    }

    private void WaveRepeaterRing(uint actionId, float inner)
    {
        HelperEffect(Centre, actionId, 2.1f);
        damage.Resolve(TopPositioned.From(Centre), actionId, [DamageType.Lethal], [], size: inner);
    }

    private void SpawnVoidzone()
    {
        voidzone = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.IntermissionVoidzone,
            Placement = new Placement(Vector3.Zero, 0f),
            TargetableStatus = 5,
            EventId = EObjId.DirectorEventId,
            EntityId = VoidzoneEntityId,
            Arg2 = VoidzoneArg2,
        });
        voidzoneActive = true;
    }

    private void EndVoidzone()
    {
        voidzone?.DirectorEObjMod(VoidzoneOff);
        voidzoneActive = false;
    }

    // UNVERIFIED: no pull stood in it; it kills like everything else nobody should be in.
    private void KillInVoidzone()
    {
        if (!voidzoneActive) return;
        foreach (var member in party.Find.InsideCircle(Vector3.Zero, VoidzoneRadius))
            Kill(member, "the void zone in the middle", LethalResultDelay);
    }

    private void ApplyDebuffs()
    {
        Give(state.Spreads, Fodder.Spread);
        Give(state.Stacks, Fodder.Stack);
    }

    private void Give(IEnumerable<PartyRole> roles, Fodder kind)
    {
        foreach (var role in roles)
        {
            if (party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(StatusOf(kind), DebuffSeconds);
            fodder[member] = kind;
        }
    }

    private static uint ActionOf(Fodder kind) => kind == Fodder.Spread ? ActionId.SniperCannon : ActionId.HighPoweredSniperCannon;

    private static ushort StatusOf(Fodder kind) => kind == Fodder.Spread ? StatusId.SniperCannonFodder : StatusId.HighPoweredSniperCannonFodder;

    // A holder who dies before the debuffs go off has theirs go off on someone random, who dies
    // along with anyone near them. It jumps once: a debuff on someone it kills goes with them.
    // UNVERIFIED: no pull lost a holder early, so when it goes off is a guess.
    private void DetonateDeadHolders()
    {
        foreach (var (holder, kind) in fodder.Where(f => !f.Key.IsAlive()).ToList())
        {
            fodder.Remove(holder);
            holder.RemoveStatus(StatusOf(kind));
            if (!jumpVictims.Contains(holder)) Jump(kind);
        }
    }

    private void Jump(Fodder kind)
    {
        var alive = party.ActiveMembers().ToList();
        if (alive.Count == 0) return;
        var target = alive[state.Rng.NextInt(alive.Count)];
        HelperEffect(Centre, ActionOf(kind), 1.1f, target);
        foreach (var member in damage.Resolve(target, ActionOf(kind), [DamageType.Lethal], []))
            jumpVictims.Add(member);
    }

    // Every hit leaves Magic Vulnerability Up, so anyone two of them reach dies. A spread is its
    // holder's alone, and kills anyone else it reaches; a stack needs a second player.
    private void ResolveSniperCannons()
    {
        var holders = fodder.Where(f => f.Key.IsAlive()).ToList();
        fodder.Clear();
        foreach (var (holder, kind) in holders)
        {
            HelperEffect(Centre, ActionOf(kind), 1.1f, holder);
            if (kind == Fodder.Stack)
            {
                damage.Resolve(holder, ActionOf(kind), [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)], stackMinTargets: StackPlayers);
                continue;
            }
            var others = party.ActiveMembers().Where(member => member != holder).ToArray();
            damage.Resolve(holder, ActionOf(kind), [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)], excludeTargets: others);
            damage.Resolve(holder, ActionOf(kind), [DamageType.Lethal], [], excludeTargets: [holder]);
        }
    }

    private void Kill(SimCharacter member, string cause, float resultDelay)
    {
        if (!doomed.Add(member)) return;
        world.Events.Add(resultDelay, () => member.Die(SimCharacterDeathExtensions.NoAction, cause));
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP3IntermissionAiReplayStateMessage(s.Debuffs.ToArray(), s.FirstHands == FirstHands.North) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP3IntermissionAiReplayStateMessage msg) return null;
        if (TopP3IntermissionState.FromNetworkReplay(msg.Debuffs, msg.FirstHandsNorth) is not { } shadowState) return null;
        ((IScenarioAi<TopP3IntermissionState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
