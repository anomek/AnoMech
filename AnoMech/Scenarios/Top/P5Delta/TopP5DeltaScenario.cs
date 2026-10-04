using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Top.TopConstants;
using AnoMech.Core.Native.Interfaces;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaScenario : IMultiplayerReplayable
{
    public string Name => "Delta";
    public IPhase Phase => TopZone.P5;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 17.92f;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP5DeltaSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP5DeltaAi()];

    private TopP5DeltaState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;

    // Exposed so MultiplayerManager can broadcast the AI-relevant subset after a host Start --
    // see UmadP3BlackHoleScenario.LastState. BeyondDefenseTarget resolves later (t=35.69s);
    // LastState aliasing `state` is what lets BuildMidRunUpdateMessage pick that change up.
    public TopP5DeltaState? LastState { get; private set; }

    // Edge-triggers BuildMidRunUpdateMessage -- see IMultiplayerReplayable.BuildMidRunUpdateMessage.
    private PartyRole? lastBroadcastBeyondDefenseTarget;

    private SimEnemy? omega;
    private SimEnemy? beetle;
    private SimEnemy? finalHelper;
    private SimEnemy? opticalUnit;
    private List<SimEnemy?>? rocketPunches;
    private List<SimEnemy?>? armUnits;
    private List<SimTether> tethersShort = [];
    private List<SimTether> tethersLong = [];
    private List<Vector3>? punchSpots;
    private Vector3? beyondDefenseApproach;
    private HelloWorld? nearSolver;
    private HelloWorld? farSolver;
    private TopHelpers? omegaMHelpers;
    private TopHelpers? monitorHelpers;

    private const string Tag = "TopP5Delta";

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (6755, "mon_sp/gimmick/z3of_boss_gimmick15"),
        (6738, "mon_sp/gimmick/z3of_boss_gimmick06"),
        (10726, "mon_sp/gimmick/z3oz_boss_gimmick14"),
        (10727, "mon_sp/gimmick/z3oz_boss_gimmick15"),
        (10728, "mon_sp/gimmick/z3oz_boss_gimmick16"),
    ];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5DeltaState(world.Rng, settingsWindow.Overrides, party.PlayerRole);
        LastState = state;
        lastBroadcastBeyondDefenseTarget = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP5DeltaState>)AiStrats[idx]).Run(state, world);
        omegaMHelpers = monitorHelpers = null;
        punchSpots = null;

        world.Events.Add(0.1f, SpawnOmega);
        world.Events.Add(1f, SpawnHelpers);
        world.Events.Add(1f, SpawnArmUnits);
        world.Events.Add(0.58f, TeleportToTheCentre);
        world.Events.Add(1.34f, () => omega?.SetPosition(new Placement(Vector3.Zero, omega.Rotation)));
        world.Events.Add(1.87f, () => omega?.SetRotation(MathF.PI));
        world.Events.Add(1.91f, () => world.Map.BattleTalk(BNpcNameId.OmegaMDynamis, BattleTalkId.UnknownAugmentation, 6000));
        world.Events.Add(2f, () => omega?.Cast(Actions.RunMiDeltaVersion));
        world.Events.Add(10.05f, EyeSpawn);
        world.Events.Add(10.05f, ApplyDeltaTethers);
        world.Events.Add(10.05f, () => omega?.SetTargetable(false));
        world.Events.Add(10.14f, () => omega?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(10.14f, SpawnDeltaAdds);
        world.Events.Add(17.3f, () => beetle?.Cast(ActionId.PeripheralSynthesis, targetId: beetle.GameObjectId, animationLock: 3.1f));
        world.Events.Add(18.48f, SpawnRocketPunches);
        world.Events.Add(19.14f, SignalRocketPunches);
        world.Events.Add(20.32f, () => finalHelper?.Cast(ActionId.ArchivePeripheral, targetId: finalHelper.GameObjectId, animationLock: 3.1f));
        world.Events.Add(23.43f, WarpInArmUnits);
        world.Events.Add(25.35f, MarkArmUnitRotations);
        world.Events.Add(26.08f, EyeStartCharging);
        world.Events.Add(28.03f, ApplyDeltaRealTethers);
        world.Events.Add(28.22f, () => SetOmegaForm(0x31, 0x04));
        world.Events.Add(28.31f, () => omega?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(28.8f, EyeDoneCharging);
        world.Events.Add(28.89f, () => opticalUnit?.Cast(Actions.OpticalLaser));
        world.Events.Add(30.18f, StartPunchExplosions);
        world.Events.Add(30.36f, () => party.Get(state.PlayerMonitorRole)?.AddStatus(state.PlayerMonitorSide.MonitorDebuffId));
        world.Events.Add(30.45f, () => finalHelper?.Cast(state.OmegaMonitorSide.DeltaOversampledWaveCannonActionId, castSeconds: 9.7f, targetId: finalHelper.GameObjectId, animationLock: 3.1f, fireDelay: 0.28f));
        world.Events.Add(30.49f, () => omega?.Cast(ActionId.BeyondDefense, castSeconds: 4.6f, targetId: omega.GameObjectId, animationLock: 0.2f, fireDelay: 0.27f));
        world.Events.Add(33.88f, MoveRocketPunchesOntoTheirExplosions);
        world.Events.Add(35.2f, () => rocketPunches?.ForEach(punch => punch?.FadeOut()));
        world.Events.Add(35.58f, StartHyperPulse);
        world.Events.Add(35.69f, FireBeyondDefense);
        world.Events.Add(36.33f, CloseOnBeyondDefenseTarget);
        world.Events.Add(36.52f, DespawnRocketPunches);
        world.Events.Add(38.7f, NextHyperPulse);
        world.Events.Add(39.28f, NextHyperPulse);
        world.Events.Add(39.86f, NextHyperPulse);
        world.Events.Add(40.03f, () => FailUnbrokenTethers(tethersShort));
        world.Events.Add(40.44f, NextHyperPulse);
        world.Events.Add(40.53f, FireMonitors);
        world.Events.Add(40.94f, FirePilePitch);
        world.Events.Add(41.01f, NextHyperPulse);
        world.Events.Add(41.59f, WarpOutArmUnits);
        world.Events.Add(42.39f, () => armUnits?.ForEach(unit => unit?.SetVisibleInEnemyList(false)));
        world.Events.Add(43.45f, StartSwivelCannon);
        world.Events.Add(43.6f, () => finalHelper?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(44f, EyeDespawn);
        world.Events.Add(44.01f, () => omega?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(46.01f, () => finalHelper?.Despawn());
        world.Events.Add(54.11f, () => omega?.SetPosition(new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(54.13f, () => DropHelloPuddle(state.NearWorldRole, true));
        world.Events.Add(54.13f, () => DropHelloPuddle(state.FarWorldRole, false));
        world.Events.Add(54.18f, () => SetOmegaForm(0x32, 0x00));
        world.Events.Add(54.27f, () => omega?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(54.33f, () => beetle?.SetRotation(beetle.Rotation + MathF.PI));
        world.Events.Add(55.15f, () => HopHelloPuddle(true));
        world.Events.Add(55.15f, () => HopHelloPuddle(false));
        world.Events.Add(56.14f, () => HopHelloPuddle(true));
        world.Events.Add(56.14f, () => HopHelloPuddle(false));
        world.Events.Add(56.59f, () => beetle?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(58.38f, () => omega?.SetTargetable(true));
        world.Events.Add(59.03f, () => beetle?.Despawn());
        world.Events.Add(64.03f, () => FailUnbrokenTethers(tethersLong));
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => Natives.TimelinePreload.Preload(GimmickTimelines, Tag);

    // Real-packet helpers, named for the mechanic each casts, spawned well ahead of their first hit.
    private void SpawnHelpers()
    {
        omegaMHelpers = new TopHelpers(world, BNpcNameId.OmegaMDynamis, 6, Tag);
        monitorHelpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 4, Tag);
    }

    public void Tick(float delta, float elapsed)
    {
        TickTethers(tethersLong, tether => tether.StretchLt(Geometry.HwTetherBreakDistance));
        TickTethers(tethersShort, tether => tether.StretchGt(Geometry.HwTetherBreakDistance));
        HelloWorld.CheckHolderDeaths(world, omegaMHelpers);
    }

    // A regression that has run out can no longer break; FailUnbrokenTethers settles it.
    private void TickTethers(List<SimTether> tethers, Predicate<SimTether> breakCondition)
    {
        foreach (var tether in tethers.Where(SimTether.IsAnyDead).ToList())
            OnTetherFailed(tether);
        foreach (var tether in tethers.Where(t => t is { IsActive: true, Resolved: false } && breakCondition(t)).ToList())
            OnTetherBroken(tether);
        tethers.RemoveAll(tether => tether.Resolved);
    }

    // Held just north of the centre by the main tank until the teleport.
    private void SpawnOmega()
    {
        omega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaMDynamis,
            NameId: BNpcNameId.OmegaMDynamis,
            Level: 90,
            Targetable: true,
            InitialModeAttributeFlags: 0x10,
            Placement: new Placement(new Vector3(0f, 0f, -2.94f), MathF.PI)));
        omega?.AddStatus(StatusId.OmegaM);
    }

    // The teleport plays with no animation target at the spot it lands on.
    private void TeleportToTheCentre()
    {
        if (omega is null) return;
        omega.Face(Vector3.Zero);
        omega.NativeActionEffect(ActionId.Teleport7b42, 1.1f, (ushort)ActionId.Teleport7b42, 0, ActionType.Action, 0,
                                 rotation: omega.Rotation, position: Vector3.Zero, actionTargetId: omega.GameObjectId);
    }

    private void SetOmegaForm(byte modeAttributeFlags, byte modelState)
    {
        omega?.SetModeAttributeFlags(modeAttributeFlags);
        omega?.SetModelState(modelState);
    }

    private void SpawnDeltaAdds()
    {
        beetle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BeetleHelper,
            NameId: BNpcNameId.OmegaBeetle,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: state.EyeSpawn.Turn(new Placement(new Vector3(-20f, 0f, 0f), MathF.PI / 2f))));
        beetle?.PlayActionTimeline(TimelineId.Spawn);

        opticalUnit = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OpticalUnit,
            NameId: BNpcNameId.OpticalUnit,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: state.EyeSpawn.Turn(new Placement(new Vector3(0f, 0f, -45f), 0f))));

        finalHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.FinalHelper,
            NameId: BNpcNameId.OmegaFinal,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: state.EyeSpawn.Turn(new Placement(new Vector3(20f, 0f, 0f), -MathF.PI / 2f))));
        finalHelper?.PlayActionTimeline(TimelineId.Spawn);
    }

    private SimCharacter[] TetherTargets() => state.TetherOrder.Select(role => party.Get(role)!).ToArray();

    private void ApplyDeltaTethers()
    {
        var targets = TetherTargets();
        world.Tether(targets[0], targets[1], TetherId.HWPrepRemote, 18f, StatusId.DeltaPrepRemoteTether);
        world.Tether(targets[2], targets[3], TetherId.HWPrepRemote, 18f, StatusId.DeltaPrepRemoteTether);
        world.Tether(targets[4], targets[5], TetherId.HWPrepLocal, 18f, StatusId.DeltaPrepLocalTether);
        world.Tether(targets[6], targets[7], TetherId.HWPrepLocal, 18f, StatusId.DeltaPrepLocalTether);

        targets[state.NearWorldTetherIndex].AddStatus(StatusId.HelloNearWorld, Duration.HelloWorldDebuff);
        targets[state.FarWorldTetherIndex].AddStatus(StatusId.HelloDistantWorld, Duration.HelloWorldDebuff);
    }

    private void SpawnRocketPunches()
    {
        rocketPunches = Enumerable.Range(0, 8).Select(i =>
        {
            var color = state.FistColors[i];
            return world.SpawnEnemy(new EnemySpawnConfig(
                                        BNpcBaseId: color,
                                        NameId: color == BNpcBaseId.RocketPunchBlue ? BNpcNameId.RocketPunchBlue : BNpcNameId.RocketPunchYellow,
                                        Level: 90,
                                        Targetable: false,
                                        EnemyList: EnemyListMode.Always,
                                        IsVisible: false,
                                        Placement: party.Get(state.TetherOrder[i])!.Placement()));
        }).ToList();
    }

    // The fists' reveal: ActionTimeline 142 is specialpop, and a real fist stays hidden until this
    // arrives. Host-only: nothing replicates a raw ActorControl.
    private void SignalRocketPunches()
        => rocketPunches?.ForEach(punch =>
        {
            if (punch is not { IsActive: true }) return;
            punch.Proxy?.ActorControl(36, 1, 142);
            punch.SetVisible(true);
            punch.AddVfx(VfxPath.RocketPunchSpawn, persistent: false);
        });

    private void SpawnArmUnits()
    {
        armUnits = Enumerable.Range(0, 6).Select(i =>
        {
            var model = state.ArmModels[i];
            return world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: model.BaseId,
                NameId: model.NameId,
                Level: 90,
                Targetable: false,
                EnemyList: EnemyListMode.Manual,
                IsVisible: false,
                Placement: state.EyeSpawn.Turn(Geometry.ArmUnitPlacements[i])));
        }).ToList();
    }

    private void WarpInArmUnits()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .ToList()
            .ForEach(t =>
            {
                t.unit?.PlayActionTimeline(state.ArmModels[t.i].SpawnTimeline);
                t.unit?.SetVisible(true);
                t.unit?.SetVisibleInEnemyList(true);
            });
    }

    private void MarkArmUnitRotations()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .ToList()
            .ForEach(t => t.unit?.AttachLockonVfx(state.ArmRotations[t.i].IconId, persistent: false));
    }

    private void WarpOutArmUnits()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .ToList()
            .ForEach(t => t.unit?.PlayActionTimeline(state.ArmModels[t.i].WarpOutTimeline));
    }

    private void ApplyDeltaRealTethers()
    {
        var targets = TetherTargets();
        tethersShort =
        [
            world.Tether(targets[0], targets[1], TetherId.HWRemote, 12f, StatusId.DeltaRemoteTether),
            world.Tether(targets[2], targets[3], TetherId.HWRemote, 12f, StatusId.DeltaRemoteTether)
        ];
        tethersLong =
        [
            world.Tether(targets[4], targets[5], TetherId.HWLocal, 36f, StatusId.DeltaLocalTether),
            world.Tether(targets[6], targets[7], TetherId.HWLocal, 36f, StatusId.DeltaLocalTether)
        ];
    }

    private void OnTetherBroken(SimTether tether)
    {
        if (tether.Resolved) return;
        if (tether.A is not { } a || tether.B is not { } b) return;
        Plugin.Log.Info($"Tether broken {tether.TetherId}");
        tether.Resolved = true;
        PatchTetherEnd(a);
        PatchTetherEnd(b);
        tether.Despawn();
    }

    private void OnTetherFailed(SimTether tether)
    {
        if (tether.Resolved) return;
        if (tether.A is not { } a || tether.B is not { } b) return;
        Plugin.Log.Info($"Tether failed {tether.TetherId}");
        tether.Resolved = true;
        omegaMHelpers?.Next(a.Placement())?.Cast(Actions.HwTetherFail);
        omegaMHelpers?.Next(b.Placement())?.Cast(Actions.HwTetherFail);
        tether.Despawn();
    }

    // UNVERIFIED: no pull let a regression run out; it fails as a tethered player's death does.
    private void FailUnbrokenTethers(List<SimTether> tethers)
    {
        foreach (var tether in tethers.ToList())
            OnTetherFailed(tether);
        tethers.Clear();
    }

    private void PatchTetherEnd(SimCharacter member)
        => omegaMHelpers?.Next(member.Placement())?.Cast(Actions.HwTetherBreak, member);

    private record RocketPunchTarget(Vector3 Position, float Rotation, uint FistColor) : IPositioned { }

    private void StartPunchExplosions()
    {
        if (rocketPunches is null) return;
        punchSpots = Enumerable.Range(0, 8)
                               .Select(i => party.Get(state.TetherOrder[i])!.Position)
                               .ToList();
        for (var i = 0; i < 8; i++)
        {
            var punch = rocketPunches[i];
            if (punch is null) continue;
            var targets = Enumerable.Range(0, 8)
                                    .Where(k => k != i)
                                    .Select(k => new RocketPunchTarget(punchSpots[k], 0f, state.FistColors[k]))
                                    .ToList();
            var inRange = punch.Find(targets).InsideCircle(punchSpots[i], Geometry.RocketPunchAoeRadius);
            bool failed = inRange.Count != 1 || inRange[0].FistColor == state.FistColors[i];
            punch.Cast(failed ? Actions.DeltaUnmitigatedExplosion : Actions.DeltaExplosion, punchSpots[i]);
        }
    }

    // The server sets each fist down on its own explosion once the blast is over.
    private void MoveRocketPunchesOntoTheirExplosions()
    {
        if (rocketPunches is null || punchSpots is not { } spots) return;
        for (var i = 0; i < rocketPunches.Count; i++)
            if (rocketPunches[i] is { } punch)
                punch.SetPosition(new Placement(spots[i], punch.Rotation));
    }

    private void DespawnRocketPunches()
    {
        rocketPunches?.ForEach(punch => punch?.Despawn());
        rocketPunches = null;
    }

    private void FireBeyondDefense()
    {
        if (omega is null || PickBeyondDefenseTarget(omega) is not { } target) return;
        state.BeyondDefenseTarget = ((ISimPartyMember)target).Role;
        Plugin.Log.Info($"Beyond defense target {state.BeyondDefenseTarget}");
        beyondDefenseApproach = Approach(omega.Position, target.Position);
        omega.Cast(Actions.BeyondDefense, target);
    }

    private SimCharacter? PickBeyondDefenseTarget(SimEnemy caster)
    {
        if (state.ForcedBeyondDefenceRole is { } forced && party.Get(forced) is { } chosen && chosen.IsAlive())
            return chosen;
        var closest2 = party.Find.ClosestN(caster.Position, 2);
        var refused = state.BeyondDefenceExcluded.Select(party.Get).OfType<SimCharacter>().ToHashSet();
        // Someone within range has to eat it, so a refusal only counts while anyone else can.
        var allowed = closest2.Where(m => !refused.Contains(m)).ToList();
        if (allowed.Count == 0) allowed = closest2.ToList();
        return allowed.Count > 0 ? allowed[world.Rng.Next(allowed.Count)] : null;
    }

    // Where Omega-M stops when the target stands beyond its reach; null when it doesn't need to move.
    private static Vector3? Approach(Vector3 from, Vector3 target)
    {
        var offset = new Vector3(target.X - from.X, 0f, target.Z - from.Z);
        var distance = offset.Length();
        return distance > Geometry.BeyondDefenseReach ? target - offset / distance * Geometry.BeyondDefenseReach : null;
    }

    private void CloseOnBeyondDefenseTarget()
    {
        if (beyondDefenseApproach is { } spot)
            omega?.SetPosition(new Placement(spot, omega.Rotation));
    }

    private void StartHyperPulse()
    {
        armUnits?.OfType<SimEnemy>()
            .Where(unit => unit.IsActive)
            .ToList()
            .ForEach(unit =>
            {
                if (party.Find.Closest(unit.Position) is { } target)
                    unit.Face(target.Position);
                unit.Cast(Actions.HyperPulseCharging);
            });
    }

    private void NextHyperPulse()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .Where(t => t.unit is { IsActive: true })
            .ToList()
            .ForEach(t =>
            {
                var step = state.ArmRotations[t.i].Mul * Geometry.HyperPulseStep;
                t.unit!.SetPosition(new Placement(t.unit.Position, t.unit.Rotation + step));
                t.unit.Cast(Actions.HyperPulseShoot);
            });
    }

    // The two monitors pick their targets together, then each AoE lands in turn: a player caught
    // twice finds the first one's Magic Vulnerability Up.
    private void FireMonitors()
    {
        var playerMonitor = party.Get(state.PlayerMonitorRole)!;
        var targets = new List<SimCharacter>();
        if (finalHelper is { } helper)
            targets.AddRange(party.Find.OnSideN(world.Rng, helper.Placement(), state.OmegaMonitorSide.Mul, count: 2));
        targets.AddRange(party.Find.OnSideN(world.Rng, playerMonitor.Placement(), state.PlayerMonitorSide.Mul, count: 2, exclude: playerMonitor));
        playerMonitor.RemoveStatus(state.PlayerMonitorSide.MonitorDebuffId);
        foreach (var target in targets)
            monitorHelpers?.Next(new Placement(target.Position, 0f))?.Cast(Actions.OversampledWaveCannon, target);
    }

    private void FirePilePitch()
    {
        if (omega is null || party.Find.RandomClosestN(world.Rng, omega.Position, 2) is not { } target) return;
        omega.Cast(Actions.PilePitch, target);
    }

    private void StartSwivelCannon()
        => beetle?.Cast(state.SwivelCannonSide == Side.Left ? Actions.SwivelCannonLeft : Actions.SwivelCannonRight);

    private void DropHelloPuddle(PartyRole role, bool near)
    {
        if (near)
            nearSolver = new HelloWorld(party, role, true);
        else
            farSolver = new HelloWorld(party, role, false);
        HopHelloPuddle(near);
    }

    private void HopHelloPuddle(bool near)
    {
        var solver = near ? nearSolver : farSolver;
        if (solver?.Position is not { } position) return;
        solver.CastSpell(omegaMHelpers?.Next(new Placement(position, 0f)));
    }

    private void EyeSpawn() => world.Map.AddEffect(0x00020001, state.EyeSpawn.EffectIndex);
    private void EyeStartCharging() => world.Map.AddEffect(0x00800040, state.EyeSpawn.EffectIndex);
    private void EyeDoneCharging() => world.Map.AddEffect(0x10000001, state.EyeSpawn.EffectIndex);
    private void EyeDespawn() => world.Map.AddEffect(0x00080004, state.EyeSpawn.EffectIndex);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP5DeltaAiReplayStateMessage(
            s.TetherOrder.ToArray(), s.FistColors.ToArray(), s.PlayerMonitorIndex,
            s.PlayerMonitorSide == Side.Left, s.OmegaMonitorSide == Side.Left,
            EyeDirection.All.ToList().IndexOf(s.EyeSpawn), s.SwivelCannonSide == Side.Left,
            s.ArmRotations.Select(rotation => rotation == ArmRotation.Clockwise).ToArray(),
            s.FarWorldRole, s.NearWorldRole, s.FarWorldTetherIndex)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP5DeltaAiReplayStateMessage msg) return null;
        // BeyondDefenseTarget starts null here, guaranteed set by ApplyMidRunUpdate before the
        // Ai reads it (t=35.69s < 36.2s).
        if (TopP5DeltaState.FromNetworkReplay(
                msg.TetherOrder, msg.FistColors, msg.PlayerMonitorIndex,
                msg.PlayerMonitorSideIsLeft, msg.OmegaMonitorSideIsLeft, msg.EyeSpawn,
                msg.SwivelCannonSideIsLeft, msg.ArmRotatesClockwise, msg.FarWorldRole,
                msg.NearWorldRole, msg.FarWorldTetherIndex) is not { } shadowState)
            return null;
        ((IScenarioAi<TopP5DeltaState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (LastState?.BeyondDefenseTarget is not { } target || lastBroadcastBeyondDefenseTarget == target) return null;
        lastBroadcastBeyondDefenseTarget = target;
        DiagnosticLog.Info($"[Multiplayer] Host: broadcasting P5 Delta BeyondDefenseTarget update -- {target}.");
        return new TopP5DeltaBeyondDefenseUpdateMessage(target);
    }

    public void ApplyMidRunUpdate(object shadowStateObj, MpMessage message)
    {
        if (shadowStateObj is TopP5DeltaState shadowState && message is TopP5DeltaBeyondDefenseUpdateMessage update)
            shadowState.BeyondDefenseTarget = update.BeyondDefenseTarget;
    }
}
