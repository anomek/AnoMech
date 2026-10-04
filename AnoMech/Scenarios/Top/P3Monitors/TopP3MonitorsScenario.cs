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
using static AnoMech.Scenarios.Top.TopConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsScenario : IMultiplayerReplayable
{
    public string Name => "Monitors";
    public IPhase Phase => TopZone.P3;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 139.2f;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP3MonitorsSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP3MonitorsAi(automarkers: true), new TopP3MonitorsAi(automarkers: false)];

    private static readonly (ushort Id, string Key)[] GimmickTimelines = [(6738, "mon_sp/gimmick/z3of_boss_gimmick06")];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP3MonitorsState state = null!;
    private DamageSolver damage = null!;
    private bool tracksMainTank;

    public TopP3MonitorsState? LastState { get; private set; }

    private SimEnemy? omega;
    private TopHelpers? helpers;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP3MonitorsState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        helpers = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP3MonitorsState>)AiStrats[idx]).Run(state, world);
        damage = new DamageSolver(party);
        tracksMainTank = false;
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);

        world.Events.Add(0f, () => HelloWorld.ApplyDebuggers(party, state.Rng));
        world.Events.Add(0.1f, SpawnOmega);
        world.Events.Add(0.1f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 16, "TopP3Monitors"));
        world.Events.Add(1.80f, () => tracksMainTank = true);
        world.Events.Add(2.04f, AutoAttack);
        world.Events.Add(5.06f, AutoAttack);
        world.Events.Add(8.09f, AutoAttack);
        world.Events.Add(8.71f, () => omega?.Cast(ActionId.TeleportP3, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(9.91f, ApplyMonitors);
        world.Events.Add(10.00f, CastMonitor);
        world.Events.Add(19.98f, ClearMonitors);
        world.Events.Add(20.07f, FireMonitors);
        world.Events.Add(20.07f, FollowMainTank);
        world.Events.Add(24.07f, AutoAttack);
        world.Events.Add(25.90f, () => omega?.Follow());
        world.Events.Add(26.10f, () => omega?.Cast(ActionId.IonEfflux, castSeconds: 9.7f, fireDelay: 0.28f));
        world.Events.Add(32.66f, () => omega?.CancelCast());
        world.Events.Add(32.66f, () => omega?.SetTargetable(false));
        world.Events.Add(32.66f, () => world.Map.BattleTalk(BNpcNameId.OmegaFinal, BattleTalkId.CriticalDamageDetected, 5000));
        world.Events.Add(32.75f, () => omega?.Cast(ActionId.P3End, castSeconds: 0f, targetId: omega?.GameObjectId, animationLock: 7.1f));
        world.Events.Add(33.55f, () => omega?.SetModelState(0x04));
        world.Events.Add(34.67f, () => world.Map.AddEffect(0x00020001, 0x14));
    }

    private void AutoAttack() => TopBoss.FinalOmegaAutoAttack(world, omega, state.Rng);

    // The cast turns Omega from its tank to face north; its monitor's side is read from that facing.
    private void CastMonitor()
    {
        if (omega == null) return;
        omega.SetRotation(MathF.PI);
        omega.Cast(state.BossSide.BossActionId, castSeconds: 9.7f, targetId: omega.GameObjectId, animationLock: 3.1f, fireDelay: 0.28f);
    }

    // Held by the monitor's 3.1s release, it then turns back to its tank and follows it. It stops
    // short of Ion Efflux: a walk still running as a cast begins resets the caster's animation.
    private void FollowMainTank() => omega?.Follow(party.Get(PartyRole.MainTank));

    public void Tick(float delta, float elapsed)
    {
        if (tracksMainTank) TopBoss.TurnToMainTank(party, omega, TopBoss.FinalOmegaTurnSpeed, delta);
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(GimmickTimelines, "TopP3Monitors");

    private void SpawnOmega()
    {
        omega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFinal,
            NameId: BNpcNameId.OmegaFinal,
            Level: Level,
            Targetable: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    private void ApplyMonitors()
    {
        for (var i = 0; i < state.Monitors.Count; i++)
            party.Get(state.Monitors[i])?.AddStatus(state.MonitorSides[i].LoadingStatusId);
    }

    private void ClearMonitors()
    {
        for (var i = 0; i < state.Monitors.Count; i++)
            party.Get(state.Monitors[i])?.RemoveStatus(state.MonitorSides[i].LoadingStatusId);
    }

    // Omega's monitor takes everyone on its side; a player's monitor takes two of those on its side
    // at random. Either tops up at random to two when its side holds fewer.
    private void FireMonitors()
    {
        if (omega == null) return;
        var targets = BossTargets(omega.Placement()).ToList();
        for (var i = 0; i < state.Monitors.Count; i++)
            if (party.Get(state.Monitors[i]) is { } monitor && monitor.IsAlive())
                targets.AddRange(party.Find.OnSideN(world.Rng, monitor.Placement(), state.MonitorSides[i].Mul, count: 2, exclude: monitor));

        foreach (var target in targets)
            helpers?.Next(omega.Placement())?.Cast(ActionId.OversampledWaveCannonAoe, castSeconds: 0f, targetLocation: target.Position, targetId: target.GameObjectId, animationLock: 1.1f);
        foreach (var target in targets)
            ResolveRuinHit(target, ActionId.OversampledWaveCannonAoe);
    }

    private IReadOnlyList<SimCharacter> BossTargets(Placement boss)
    {
        var right = new Vector2(-MathF.Cos(boss.Rotation), MathF.Sin(boss.Rotation));
        var onSide = party.ActiveMembers()
                          .Where(member => Vector2.Dot(new Vector2(member.Position.X, member.Position.Z) - boss.Position2, right) * state.BossSide.Mul < 0f)
                          .ToList();
        return onSide.Count >= 2 ? onSide : party.Find.OnSideN(world.Rng, boss, state.BossSide.Mul, count: 2);
    }

    // A second spread lands under the first's Magic Vulnerability Up and kills.
    private void ResolveRuinHit(IPositioned source, uint actionId)
    {
        foreach (var hit in damage.Resolve(source, actionId, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 4.96f)]))
            if (hit.IsAlive()) TopActions.ComeRuin.Land(hit, 2, 6.96f, actionId);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP3MonitorsAiReplayStateMessage(
            s.BossSide == CleaveSide.Left, s.Monitors.ToArray(), s.MonitorSides.Select(side => side == CleaveSide.Left).ToArray())
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP3MonitorsAiReplayStateMessage msg) return null;
        if (TopP3MonitorsState.FromNetworkReplay(msg.BossIsLeft, msg.Monitors, msg.MonitorsAreLeft) is not { } shadowState)
            return null;
        ((IScenarioAi<TopP3MonitorsState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
