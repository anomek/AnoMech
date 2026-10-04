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
using static AnoMech.Scenarios.Top.P4BlueScreen.TopP4BlueScreenConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

// From the P3 kill to Blue Screen's raidwide. The boss's HP stays full: the DPS check is left out.
public sealed class TopP4BlueScreenScenario : IMultiplayerReplayable, IPartyLimitBreakScenario
{
    public string Name => "Blue Screen";
    public IPhase Phase => TopZone.P4;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 169.16f;
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly TopP4BlueScreenSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP4BlueScreenAi()];

    // Every Wave Cannon line and repeater ring is cast from the arena centre.
    private static readonly Placement Centre = new(Vector3.Zero, MathF.PI);

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP4BlueScreenState state = null!;
    private DamageSolver damage = null!;
    private SimEnemy? boss;
    private TopHelpers? helpers;
    private readonly List<Placement> lingeringLines = [];
    private float elapsed;
    private float gaugeUnits;
    private float nextGaugeTick;

    public TopP4BlueScreenState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP4BlueScreenState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        boss = null;
        helpers = null;
        lingeringLines.Clear();
        elapsed = 0f;
        gaugeUnits = LimitBreak.GaugeFull;
        nextGaugeTick = float.MaxValue;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP4BlueScreenState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnBoss);
        world.Events.Add(0f, ContinueIonEfflux);
        world.Events.Add(0f, () => HelloWorld.ApplyDebuggers(party, state.Rng));
        world.Events.Add(0f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 32, "TopP4BlueScreen"));
        world.Events.Add(1.91f, () => boss?.CancelCast());
        world.Events.Add(1.91f, () => boss?.SetTargetable(false));
        world.Events.Add(1.91f, () => Talk(BattleTalkId.CriticalDamageDetected, 5000));
        world.Events.Add(2.00f, () => BossVisual(ActionId.P3End, 7.1f));
        world.Events.Add(2.80f, () => boss?.SetModelState(0x04));
        world.Events.Add(3.92f, () => world.Map.AddEffect(0x00020001, 0x14));
        world.Events.Add(3.92f, () => world.SetWeather(89, 2f));
        world.Events.Add(9.02f, () => Talk(BattleTalkId.InitiatingSystemReboot, 6000));
        world.Events.Add(9.11f, () => BossVisual(ActionId.P4Begin, 8.1f));
        world.Events.Add(9.11f, () => boss?.AddStatus(StatusId.InfiniteLimit));
        world.Events.Add(9.11f, () => boss?.SetTargetable(true));
        world.Events.Add(9.78f, () => MeleeLimitBreak(LimitBreakTiming.PhaseStart));
        world.Events.Add(9.91f, () => boss?.SetModelState(0x0B));
        world.Events.Add(17.23f, () => boss?.Cast(ActionId.TeleportP3, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(17.81f, () => boss?.SetPosition(Vector3.Zero));
        world.Events.Add(18.56f, () => Talk(BattleTalkId.DefeatUnacceptable, 6000));
        world.Events.Add(18.65f, () => boss?.SetRotation(MathF.PI));
        world.Events.Add(18.65f, () => boss?.Cast(ActionId.P4WaveCannonVisualStart, castSeconds: 4.7f, targetId: boss?.GameObjectId, fireDelay: 0.28f, animationLock: 5.1f));
        world.Events.Add(21.18f, () => MarkStackTargets(0));
        world.Events.Add(23.72f, AimLingeringLines);
        world.Events.Add(24.25f, Proteans);
        world.Events.Add(26.66f, CastWaveRepeater);
        world.Events.Add(28.77f, () => BossVisual(ActionId.P4WaveCannonVisual2, 5.1f));
        world.Events.Add(28.99f, ResolveLingeringLines);
        world.Events.Add(29.20f, () => ResolveStacks(0));
        world.Events.Add(31.27f, () => MarkStackTargets(1));
        world.Events.Add(31.63f, ResolveWaveRepeaterCircle);
        world.Events.Add(33.76f, () => WaveRepeaterRing(ActionId.WaveRepeater2, 6f));
        world.Events.Add(33.86f, () => BossVisual(ActionId.P4WaveCannonVisual2, 5.1f));
        world.Events.Add(33.86f, AimLingeringLines);
        world.Events.Add(34.40f, Proteans);
        world.Events.Add(35.81f, () => WaveRepeaterRing(ActionId.WaveRepeater3, 12f));
        world.Events.Add(37.87f, () => WaveRepeaterRing(ActionId.WaveRepeater4, 18f));
        world.Events.Add(38.99f, () => BossVisual(ActionId.P4WaveCannonVisual1, 5.1f));
        world.Events.Add(39.15f, ResolveLingeringLines);
        world.Events.Add(39.30f, () => ResolveStacks(1));
        world.Events.Add(39.77f, () => boss?.SetModelState(0x05));
        world.Events.Add(41.52f, () => MarkStackTargets(2));
        world.Events.Add(44.02f, () => Talk(BattleTalkId.StructuralLimitationsExceeded, 4000));
        world.Events.Add(44.11f, () => BossVisual(ActionId.P4WaveCannonVisual3, 5.1f));
        world.Events.Add(44.11f, CastWaveRepeater);
        world.Events.Add(44.11f, AimLingeringLines);
        world.Events.Add(44.64f, Proteans);
        world.Events.Add(49.09f, ResolveWaveRepeaterCircle);
        world.Events.Add(49.23f, () => BossVisual(ActionId.P4WaveCannonVisual4, 5.1f));
        world.Events.Add(49.37f, ResolveLingeringLines);
        world.Events.Add(49.55f, () => ResolveStacks(2));
        world.Events.Add(50.03f, () => boss?.SetModelState(0x06));
        world.Events.Add(51.20f, () => WaveRepeaterRing(ActionId.WaveRepeater2, 6f));
        world.Events.Add(53.25f, () => WaveRepeaterRing(ActionId.WaveRepeater3, 12f));
        world.Events.Add(55.31f, () => WaveRepeaterRing(ActionId.WaveRepeater4, 18f));
        world.Events.Add(56.27f, () => Talk(BattleTalkId.StructuralFailureImminent, 6000));
        world.Events.Add(56.36f, () => boss?.Cast(ActionId.BlueScreen, castSeconds: 7.7f, targetId: boss?.GameObjectId, fireDelay: 0.26f, animationLock: 2.1f));
        world.Events.Add(57.03f, () => MeleeLimitBreak(LimitBreakTiming.BlueScreen));
        world.Events.Add(64.33f, () => boss?.SetTargetable(false));
        world.Events.Add(64.33f, () => HelloWorld.RemoveDebuggers(party));
        world.Events.Add(64.42f, CastBlueScreenAoe);
        // UNVERIFIED: some real pulls gain 300 or 600 LB gauge here; the trigger is unknown, so none is added.
        world.Events.Add(66.34f, () => Talk(BattleTalkId.MustEvolve, 5000));
        world.Events.Add(66.34f, () => world.Map.AddEffect(0x00080004, 0x14));
        world.Events.Add(66.34f, () => world.SetWeather(174, 0f));
    }

    // Host and peer alike: the gauge starts full for the melee LB3 the phase opens with.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP4BlueScreen");
        instanceWorld.Party.LimitBreak.Set(3f);
        instanceWorld.Events.Add(64.33f, () => Natives.Bgm.Silence());
        instanceWorld.Party.LimitBreak.Landed += OnLocalLimitBreak;
    }

    // The gauge follows the event clock, which the debug speed buttons scale.
    public void Tick(float delta, float time)
    {
        elapsed = world.Events.Elapsed;
        TickGauge();
    }

    private void Talk(uint textId, uint durationMs) => world.Map.BattleTalk(BNpcNameId.OmegaFinal, textId, durationMs);

    // Where the P3 boss goes down, short of the centre it teleports to.
    private void SpawnBoss()
    {
        boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal,
            NameId: BNpcNameId.OmegaFinal,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(0f, 0f, -2.54f), MathF.PI)));
        boss?.SetMaxHealth(BossMaxHealth);
        boss?.SetHealth(BossMaxHealth);
        if (party.Get(PartyRole.MainTank) is { } tank) boss?.SetTarget(tank, follow: false);
    }

    // P3's last Ion Efflux is already under way as the phase opens.
    private void ContinueIonEfflux()
    {
        boss?.Cast(ActionId.IonEfflux, castSeconds: 9.7f, fireDelay: 0.28f);
        boss?.SkipCastAhead(4.67f);
    }

    private void BossVisual(uint actionId, float animationLock)
        => boss?.Cast(actionId, castSeconds: 0f, targetId: boss?.GameObjectId, animationLock: animationLock);

    // The real headers: a helper is its own animation target, except for the stack marker, which
    // plays on its target.
    private void HelperEffect(Placement at, uint actionId, float animationLock, SimCharacter? target = null, bool onTarget = false)
    {
        if (helpers?.Next(at) is not { } helper) return;
        helper.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0, rotation: at.Rotation, position: at.Position,
            animationTargetId: onTarget && target != null ? target.GameObjectId : helper.GameObjectId, actionTargetId: target?.GameObjectId);
    }

    private static Placement FromCentre(Vector3 toward) => new Placement(Vector3.Zero, 0f).Face(toward);

    // A dead player's line goes to a living one instead, who then takes two.
    private List<SimCharacter> LineTargets()
    {
        var alive = party.ActiveMembers().ToList();
        var targets = new List<SimCharacter>(alive);
        while (alive.Count > 0 && targets.Count < PerRole.All.Length) targets.Add(alive[state.Rng.NextInt(alive.Count)]);
        return targets;
    }

    // A target already dead is replaced by a living player.
    private void MarkStackTargets(int set)
    {
        var alive = PerRole.All.Where(r => party.Get(r).IsAlive()).ToList();
        var targets = state.StackTargets[set].ToArray();
        for (var i = 0; i < targets.Length; i++)
        {
            if (alive.Contains(targets[i])) continue;
            var spare = alive.Except(targets).ToList();
            if (spare.Count > 0) targets[i] = spare[state.Rng.NextInt(spare.Count)];
        }
        state.Retarget(set, targets);
        foreach (var role in targets)
            if (party.Get(role) is { } target && target.IsAlive())
                HelperEffect(Centre, ActionId.P4WaveCannonStackTarget, 8.1f, target, onTarget: true);
    }

    // Aimed where each player stands as the cast starts; the telegraph shows only for its last 0.7s.
    private void AimLingeringLines()
    {
        lingeringLines.Clear();
        foreach (var target in LineTargets())
        {
            var line = FromCentre(target.Position);
            lingeringLines.Add(line);
            if (helpers?.Next(line) is { } helper)
                helper.Cast(ActionId.P4WaveCannonProteanAoe, castSeconds: 5f, targetId: helper.GameObjectId, omenDelay: 4.3f, fireDelay: 0.27f, animationLock: 1.1f);
        }
    }

    private void Proteans()
    {
        var shots = LineTargets().Select(target => (Target: target, Line: FromCentre(target.Position))).ToList();
        foreach (var (target, line) in shots)
            HelperEffect(line, ActionId.P4WaveCannonProtean, 1.1f, target);
        foreach (var (_, line) in shots)
            damage.Resolve(TopPositioned.From(line), ActionId.P4WaveCannonProtean, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)]);
    }

    private void ResolveLingeringLines()
    {
        foreach (var line in lingeringLines)
            damage.Resolve(TopPositioned.From(line), ActionId.P4WaveCannonProteanAoe, [DamageType.Lethal], []);
    }

    // Both line stacks go off together. A target who died before it went off has their stack go
    // on someone random.
    private void ResolveStacks(int set)
    {
        var lines = new List<Placement>();
        foreach (var role in state.StackTargets[set])
        {
            var living = party.ActiveMembers().ToList();
            if (living.Count == 0) break;
            var target = party.Get(role) is { } marked && marked.IsAlive() ? marked : living[state.Rng.NextInt(living.Count)];
            var line = FromCentre(target.Position);
            HelperEffect(line, ActionId.P4WaveCannonStack, 1.1f, target);
            lines.Add(line);
        }
        foreach (var line in lines)
            damage.Resolve(TopPositioned.From(line), ActionId.P4WaveCannonStack, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)],
                stackMinTargets: StackMinimum);
    }

    private void CastWaveRepeater()
        => helpers?.Next(Centre)?.Cast(ActionId.WaveRepeater1, castSeconds: 4.7f, targetLocation: Vector3.Zero, fireDelay: 0.28f, animationLock: 2.1f);

    private void ResolveWaveRepeaterCircle() => damage.Resolve(TopPositioned.From(Centre), ActionId.WaveRepeater1, [DamageType.Lethal], []);

    private void WaveRepeaterRing(uint actionId, float inner)
    {
        HelperEffect(Centre, actionId, 2.1f);
        damage.Resolve(TopPositioned.From(Centre), actionId, [DamageType.Lethal], [], size: inner);
    }

    private void CastBlueScreenAoe()
    {
        if (helpers?.Next(Centre) is { } helper)
            helper.Cast(ActionId.BlueScreenAoe, castSeconds: 0.7f, targetId: helper.GameObjectId, fireDelay: 0.28f, animationLock: 1.1f);
    }

    // The melee LB3: NIN = MNK > DRG > VPR > SAM > RPR, M1 on a tie, and a lone melee always. It is
    // pressed as the boss turns targetable, or as long into Blue Screen's cast (UNVERIFIED: no pull
    // saved it). It does no damage with the boss's HP left alone. A human's seat presses its own.
    private void MeleeLimitBreak(LimitBreakTiming at)
    {
        if (state.MeleeLimitBreakTiming != at) return;
        if (LimitBreakMelee() is not SimPartyNpc npc || !npc.IsAlive() || gaugeUnits < LimitBreak.GaugeFull) return;
        if (!LimitBreak.ByJob.TryGetValue(npc.ClassJob, out var actionId)) return;
        npc.PlayAction(actionId, LimitBreak.AnimationLockOf(actionId), LimitBreak.DpsBar, LimitBreak.DpsLands, boss, holdStill: true);
        world.Events.Add(LimitBreak.DpsLands, () =>
        {
            if (npc.IsAlive() && gaugeUnits >= LimitBreak.GaugeFull) SpendGauge(3);
        });
    }

    private SimCharacter? LimitBreakMelee()
    {
        if (state.MeleeLimitBreakBy is { } forced) return party.Get(forced);
        var first = MeleePriority((party.Get(PartyRole.MeleeDpsA) as ISimPartyMember)?.ClassJob ?? 0);
        var second = MeleePriority((party.Get(PartyRole.MeleeDpsB) as ISimPartyMember)?.ClassJob ?? 0);
        if (Math.Min(first, second) == NotMelee) return null;
        return party.Get(second < first ? PartyRole.MeleeDpsB : PartyRole.MeleeDpsA);
    }

    private const int NotMelee = 5;

    private static int MeleePriority(byte job) => job switch
    {
        30 or 20 => 0,
        22 => 1,
        41 => 2,
        34 => 3,
        39 => 4,
        _ => NotMelee,
    };

    // Host only: a peer's gauge is the host's (IPartyLimitBreakScenario), and its own limit
    // break reaches the host as a report.
    private void OnLocalLimitBreak(uint actionId, LimitBreakAim aim)
    {
        if (Plugin.GameInstance is not { } game || game.ActiveScenario != this || game.RunningScenario != this) return;
        SpendGauge(LimitBreak.LevelOf(actionId));
    }

    public void OnPartyLimitBreak(PartyRole role, uint actionId, LimitBreakAim aim)
    {
        if (Plugin.GameInstance is not { } game || game.RunningScenario != this) return;
        SpendGauge(LimitBreak.LevelOf(actionId));
    }

    // A limit break spends the bars of its level; the gauge then fills on its own.
    private void SpendGauge(int level)
    {
        gaugeUnits = MathF.Max(0f, gaugeUnits - level * LimitBreak.GaugeBar);
        nextGaugeTick = elapsed + LimitBreak.GaugeFirstTick;
        PushGauge();
    }

    private void TickGauge()
    {
        if (elapsed < nextGaugeTick) return;
        nextGaugeTick += LimitBreak.GaugeTickSeconds;
        gaugeUnits = MathF.Min(LimitBreak.GaugeFull, gaugeUnits + LimitBreak.GaugeTick);
        PushGauge();
    }

    private void PushGauge() => world.Party.LimitBreak.Set(gaugeUnits / LimitBreak.GaugeBar);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP4BlueScreenAiReplayStateMessage(s.StackTargets.SelectMany(t => t).ToArray()) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP4BlueScreenAiReplayStateMessage msg) return null;
        if (TopP4BlueScreenState.FromNetworkReplay(msg.StackTargets) is not { } shadowState) return null;
        ((IScenarioAi<TopP4BlueScreenState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
