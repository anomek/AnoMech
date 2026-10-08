using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaScenario : IMultiplayerReplayable
{
    public string Name => "Delta";
    public IPhase Phase => TopZone.P5;
    public bool SupportsMultiplayer => true;
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
    // see UmadP3BlackHoleScenario.LastState. BeyondDefenseTarget resolves later (t=35.3s);
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
    private HelloWorld? nearSolver;
    private HelloWorld? farSolver;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5DeltaState(world.Rng, settingsWindow.Overrides, party.PlayerRole);
        LastState = state;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP5DeltaState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.1f, SpawnOmega);
        world.Events.Add(2f, () => omega?.Cast(Actions.RunMiDeltaVersion));
        // Delta arena transition animation (index 0x07) — real game fires these
        // at +8/+24/+27/+42s relative to the Run: mi cast. Cast is at t=2f here.
        world.Events.Add(10f, EyeSpawn);
        world.Events.Add(26.1f, EyeStartCharging);
        world.Events.Add(29f, EyeDoneCharging);
        world.Events.Add(44f, EyeDespawn);
        world.Events.Add(10f, () => omega?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(10.1f, ApplyDeltaTethers);           // HW debuffs confirmed at t=10.053s
        world.Events.Add(10.1f, SpawnDeltaAdds);
        world.Events.Add(10f, () => omega?.SetTargetable(false));
        world.Events.Add(17.3f, SpawnRocketPunches); // Peripheral Synthesis fires t=17.31s
        world.Events.Add(20.3f, () =>finalHelper?.Cast(ActionId.ArchivePeripheral));
        world.Events.Add(23.5f, SpawnArmUnits);               // Archive Peripheral fires t=20.30s
        world.Events.Add(25.3f, MarkArmUnitRotations);        // +1s after arm spawn
        world.Events.Add(28.1f, ApplyDeltaRealTethers); // same window as optical laser
        world.Events.Add(28.2f, () => opticalUnit?.Cast(Actions.OpticalLaser));
        world.Events.Add(28.4f, () => omega?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(30.5f, StartMonitors);         // BeyondDefense + OWC casts start t=30.43/30.47s
        world.Events.Add(30.1f, StartPunchExplosions);  // 3s cast, resolves at 33.5f
        world.Events.Add(35.3f, FireBeyondDefenseAoe);        // BeyondDefense jump t=35.336s, AOE lands t=35.649s
        world.Events.Add(35.6f, StartHyperPulse);             // HyperPulse cast starts t=35.559s, fires t=38.060s
        world.Events.Add(35.2f, DespawnRocketPunches);
        world.Events.Add(38.6f, NextHyperPulse);              // rotation steps ~0.58s each
        world.Events.Add(39.2f, NextHyperPulse);
        world.Events.Add(39.8f, NextHyperPulse);
        world.Events.Add(40.4f, NextHyperPulse);
        world.Events.Add(40.5f, FireMonitors);                // OWC AOE fires t=40.509s
        world.Events.Add(41.0f, NextHyperPulse);              // last HP step, same tick as pile pitch t=40.999s
        world.Events.Add(41.0f, FirePilePitch);               // Pile Pitch fires t=40.999s
        world.Events.Add(44.5f, () => armUnits?.Select((unit, i) => (unit, i)).ToList().ForEach(t => t.unit?.PlayActionTimeline(state.ArmHandedness[t.i].ArmUnitWarpStartTimelineId)));
        world.Events.Add(45.5f, () => armUnits?.ForEach(unit => unit?.Despawn()));
        world.Events.Add(43.5f, StartSwivelCannon);           // Swivel Cannon cast starts t=43.458s
        world.Events.Add(44.1f, () => omega?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(43.5f, () => finalHelper?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(45.5f, () => finalHelper?.Despawn());  // despawn signal t=43.591s
        world.Events.Add(47.5f, () => CheckTethersExpired(tethersShort));             // tethers applied t=30.2, 18s life → expire 48.2
        world.Events.Add(53.2f, EndSwivelCannon);             // 43.458 + 9.7s cast = t=53.158s
        world.Events.Add(53.2f, () => DropHelloPuddle(state.NearWorldRole, true));
        world.Events.Add(53.2f, () => DropHelloPuddle(state.FarWorldRole, false));
        world.Events.Add(54.2f, () => omega?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(54.2f, () => HopHelloPuddle(true));
        world.Events.Add(54.2f, () => HopHelloPuddle(false));
        world.Events.Add(55.2f, () => HopHelloPuddle(true));
        world.Events.Add(55.2f, () => HopHelloPuddle(false));
        world.Events.Add(56.6f, () => beetle?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(58.6f, () => beetle?.Despawn());
        world.Events.Add(56.5f, () => omega?.SetTargetable(true));
        world.Events.Add(65.1f, () => CheckTethersExpired(tethersLong));             // tethers expire t=30.2+36=66.2
    }
    
    public void Tick(float delta, float elapsed)
    {
        TickTethers(tethersLong, tether => tether.StretchLt(Geometry.HwTetherBreakDistance));
        TickTethers(tethersShort, tether => tether.StretchGt(Geometry.HwTetherBreakDistance));
        HelloWorld.CheckHolderDeaths(world);
    }


    private void TickTethers(List<SimTether> tethers, Predicate<SimTether> breakCondition)
    {
        var dead = tethers.Where(SimTether.IsAnyDead).ToList();
        dead.ForEach(OnTetherFailed);
        dead.ForEach(tether => tethers.Remove(tether));
               
        
        var broken = tethers.Where(breakCondition.Invoke).ToList();
        broken.ForEach(OnTetherBroken);
        broken.ForEach(tether =>
        {
            var a= tethers.Remove(tether);
            Plugin.Log.Info($"Removed {a} tether");
        });
    }


    private void SpawnOmega()
    {
        omega = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaMDynamis,
            NameId: BNpcNameId.OmegaMDynamis,
            Level: 90,
            Targetable: true,
            InitialModeAttributeFlags: 0x10,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    private void SpawnDeltaAdds()
    {
        beetle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BeetleHelper,
            NameId: BNpcNameId.OmegaBeetle,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(-20f, 0f, 0f) * state.EyeSpawn.Mul, MathF.PI / 2f * state.EyeSpawn.Mul)));
        beetle?.PlayActionTimeline(ActionTimelineId.WarpEnd);

        opticalUnit = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OpticalUnit,
            NameId: BNpcNameId.OpticalUnit,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(new Vector3(0f, 0f, -45f) * state.EyeSpawn.Mul, MathF.PI / 2f - MathF.PI / 2 * state.EyeSpawn.Mul)));

        finalHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.FinalHelper,
            NameId: BNpcNameId.OmegaFinal,
            Level: 90,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(20f, 0f, 0f) * state.EyeSpawn.Mul, -MathF.PI / 2f * state.EyeSpawn.Mul)));
        finalHelper?.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    private void ApplyDeltaTethers()
    {
        var targets = new SimCharacter[8];
        for (int i = 0; i < 8; i++) targets[i] = party.Get(state.TetherOrder[i])!;

        world.Tether(targets[0], targets[1], TetherId.HWPrepRemote, 18f, StatusId.HWPrepRemoteTether);
        world.Tether(targets[2], targets[3], TetherId.HWPrepRemote, 18f, StatusId.HWPrepRemoteTether);
        world.Tether(targets[4], targets[5], TetherId.HWPrepLocal, 18f, StatusId.HWPrepLocalTether);
        world.Tether(targets[6], targets[7], TetherId.HWPrepLocal, 18f, StatusId.HWPrepLocalTether);

        targets[state.NearWorldTetherIndex].AddStatus(StatusId.HelloNearWorld, Duration.HelloWorldDebuff);
        targets[state.FarWorldTetherIndex].AddStatus(StatusId.HelloDistantWorld, Duration.HelloWorldDebuff);
    }

    private void SpawnRocketPunches()
    {
        beetle?.Cast(ActionId.PeripheralSynthesis);
        rocketPunches = Enumerable.Range(0, 8).Select(i =>
        {
            var placement = party.Get(state.TetherOrder[i])!.Placement().MoveForward(-Geometry.PunchBackDistance);
            var punch = world.SpawnEnemy(new EnemySpawnConfig(
                                             BNpcBaseId: state.FistColors[i],
                                             NameId: BNpcNameId.RocketPunch,
                                             Level: 90,
                                             Targetable: false,
                                             EnemyList: EnemyListMode.Always,
                                             Placement: placement));
            punch?.AddVfx(VfxPath.RocketPunchSpawn, persistent: false);
            return punch;
        }).ToList();
    }

    private void SpawnArmUnits()
    {
        omega?.SetModeAttributeFlags(0x31);
        omega?.SetModelState(0x04);
        
        armUnits = Enumerable.Range(0, 6).Select(i =>
        {
            var unit = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: state.ArmHandedness[i].ArmUnitId,
                NameId: state.ArmHandedness[i].ArmUnitNameId,
                Level: 90,
                Targetable: false,
                EnemyList: EnemyListMode.Always,
                Placement: new Placement(Geometry.ArmUnitPlacements[i].Position * new Vector3(state.EyeSpawn.Mul, 1, 1), Geometry.ArmUnitPlacements[i].Rotation)));
            unit?.PlayActionTimeline(state.ArmHandedness[i].ArmUnitWarpEndTimelineId);
            return unit;
        }).ToList();
    }

    private void MarkArmUnitRotations()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .ToList()
            .ForEach(t => t.unit?.AttachLockonVfx(state.ArmHandedness[t.i].RotateLockonId, persistent: false));
    }

    private void ApplyDeltaRealTethers()
    {
        var targets = new SimCharacter[8];
        for (int i = 0; i < 8; i++) targets[i] = party.Get(state.TetherOrder[i])!;

        tethersShort =
        [
            world.Tether(targets[0], targets[1], TetherId.HWRemote, 18f, StatusId.HWRemoteTether),
            world.Tether(targets[2], targets[3], TetherId.HWRemote, 18f, StatusId.HWRemoteTether)
        ];
        tethersLong =
        [
            world.Tether(targets[4], targets[5], TetherId.HWLocal,  36f, StatusId.HWLocalTether),
            world.Tether(targets[6], targets[7], TetherId.HWLocal,  36f, StatusId.HWLocalTether)
        ];
    }

    private void OnTetherBroken(SimTether tether)
    {
        if (tether.Resolved) return;
        if (tether.A is not { } a || tether.B is not { } b) return;
        Plugin.Log.Info($"Tether broken {tether.TetherId}");
        tether.Resolved = true;
        SpawnHwTetherHelper(a.Position, Actions.HwTetherBreak);
        SpawnHwTetherHelper(b.Position, Actions.HwTetherBreak);
        tether.Despawn();
    }

    private void OnTetherFailed(SimTether tether)
    {
        if (tether.Resolved) return;
        if (tether.A is not { } a || tether.B is not { } b) return;
        Plugin.Log.Info($"Tether failed {tether.TetherId}");
        tether.Resolved = true;
        SpawnHwTetherHelper(a.Position, Actions.HwTetherFail);
        SpawnHwTetherHelper(b.Position, Actions.HwTetherFail);
        tether.Despawn();
    }

    private void SpawnHwTetherHelper(Vector3 pos, EnemyAction action)
        => SpawnHelper(pos)?.Cast(action);

    private SimEnemy? SpawnHelper(Vector3 pos)
    {
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaHelper,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(pos, 0f)));
        if (helper != null) world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
        return helper;
    }

    private void StartMonitors()
    {
        omega?.Cast(ActionId.BeyondDefense);
        finalHelper?.Cast(state.OmegaMonitorSide.DeltaOversampledWaveCannonActionId);

        var playerMonitor = party.Get(state.TetherOrder[state.PlayerMonitorIndex])!;
        playerMonitor.AddStatus(state.PlayerMonitorSide.MonitorDebuffId);
    }

    private record RocketPunchTarget(Vector3 Position, float Rotation, uint FistColor) : IPositioned { }

    private void StartPunchExplosions()
    {
        if (rocketPunches is null) return;
        var punchTargets = Enumerable.Range(0, 8)
                                     .Select(i => party.Get(state.TetherOrder[i])!.Position)
                                     .ToList();
        for(var i = 0; i < 8; i++)
        {
            var punch = rocketPunches[i];
            if (punch is null) continue;
            var targets = Enumerable.Range(0, 8)
                                    .Where(k => k != i)
                                    .Select(k => new RocketPunchTarget(punchTargets[k], 0f, state.FistColors[k]))
                                    .ToList();
            var inRange = punch.Find(targets).InsideCircle(punchTargets[i], Geometry.RocketPunchAoeRadius);
            bool failed = inRange.Count != 1 || inRange[0].FistColor == state.FistColors[i];
            punch.Cast(failed ? Actions.DeltaUnmitigatedExplosion : Actions.DeltaExplosion, punchTargets[i]);
        }
    }

    private void FireBeyondDefenseAoe()
    {
        if (omega is null) return;
        SimCharacter? target;
        if (state.ForcedBeyondDefenceRole is { } forced)
            target = party.Get(forced);
        else if (state.BeyondDefenceExcluded.Count > 0)
        {
            var refused = state.BeyondDefenceExcluded.Select(party.Get).OfType<SimCharacter>().ToHashSet();
            var closest2 = party.Find.ClosestN(omega.Position, 2);
            // Someone within range has to eat it, so a refusal only counts while anyone else can.
            var allowed = closest2.Where(m => !refused.Contains(m)).ToList();
            if (allowed.Count == 0) allowed = closest2.ToList();
            target = allowed.Count > 0 ? allowed[world.Rng.Next(allowed.Count)] : null;
        }
        else
            target = party.Find.RandomClosestN(world.Rng, omega.Position, 2);
        if (target is null) return;
        state.BeyondDefenseTarget = ((ISimPartyMember)target).Role;
        Plugin.Log.Info($"Beyond defense target {((ISimPartyMember)target).Role}");
        omega.Cast(Actions.BeyondDefense, target);
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

    private void DespawnRocketPunches()
    {
        rocketPunches?.ForEach(punch => punch?.Despawn());
        rocketPunches = null;
    }

    private void NextHyperPulse()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .Where((t, i) => t.unit is { IsActive: true })
            .ToList()
            .ForEach(t =>
            {
                var step = state.ArmHandedness[t.i].Mul * Geometry.HyperPulseStep;
                t.unit!.SetPosition(new Placement(t.unit.Position, t.unit.Rotation + step));
                t.unit.Cast(Actions.HyperPulseShoot);
            });
    }

    // Every target is picked before the first circle lands, so a death can't change the later picks.
    private void FireMonitors()
    {
        var targets = new List<SimCharacter>();
        if (finalHelper is {} helper)
            targets.AddRange(MonitorTargets(helper.Placement(), state.OmegaMonitorSide, exclude: null));

        var playerMonitor = party.Get(state.TetherOrder[state.PlayerMonitorIndex])!;
        targets.AddRange(MonitorTargets(playerMonitor.Placement(), state.PlayerMonitorSide, exclude: playerMonitor));
        playerMonitor.RemoveStatus(state.PlayerMonitorSide.MonitorDebuffId);

        foreach (var member in targets)
            SpawnHelper(member.Position)?.Cast(Actions.OversampledWaveCannon, member);
    }

    private IReadOnlyList<SimCharacter> MonitorTargets(Placement src, Side side, SimCharacter? exclude)
        => party.Find.OnSideN(world.Rng, src, side.Mul, count: 2, exclude: exclude);

    private void FirePilePitch()
    {
        if (omega is null) return;
        if (party.Find.Closest(omega.Position) is not { } target) return;
        omega.Cast(Actions.PilePitch, target);
    }

    private void StartSwivelCannon()
        => beetle?.Cast(state.SwivelCannonSide == Side.Left ? Actions.SwivelCannonLeft : Actions.SwivelCannonRight);

    private void EndSwivelCannon()
    {
        omega?.SetModeAttributeFlags(0x32);
        omega?.SetModelState(0x00);
    }

    private void CheckTethersExpired(List<SimTether> tethers)
    {
        foreach (var t in tethers)
            if (!t.Resolved) OnTetherFailed(t);
    }

    private void DropHelloPuddle(PartyRole role, bool near)
    {
        if (near)
            nearSolver = new HelloWorld(world.Party, role, true);
        else
            farSolver = new HelloWorld(world.Party, role, false);
        HopHelloPuddle(near);
    }

    private void HopHelloPuddle(bool near)
    {
        var solver = near ? nearSolver : farSolver;
        if ( solver?.Position is not {} position) return;
        solver.CastSpell(SpawnHelper(position));
    }

    // Delta arena transition animation (index 0x07).
    // Real game fires at +8/+24/+27/+42s relative to "Run: mi (Delta Version)" cast.
    private void EyeSpawn() => world.Map.AddEffect(0x00020001, state.EyeSpawn.EffectIndex);
    private void EyeStartCharging()  => world.Map.AddEffect(0x00800040, state.EyeSpawn.EffectIndex);
    private void EyeDoneCharging()  => world.Map.AddEffect(0x10000001, state.EyeSpawn.EffectIndex);
    private void EyeDespawn()    => world.Map.AddEffect(0x00080004, state.EyeSpawn.EffectIndex);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP5DeltaAiReplayStateMessage(
            s.TetherOrder.ToArray(), s.FistColors.ToArray(), s.PlayerMonitorIndex,
            s.PlayerMonitorSide == Side.Left, s.OmegaMonitorSide == Side.Left,
            s.EyeSpawn == NorthSouth.North, s.SwivelCannonSide == Side.Left,
            s.ArmHandedness.Select(side => side == Side.Left).ToArray(),
            s.FarWorldRole, s.NearWorldRole, s.FarWorldTetherIndex)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP5DeltaAiReplayStateMessage msg) return null;
        // BeyondDefenseTarget starts null here, guaranteed set by ApplyMidRunUpdate before the
        // Ai reads it (t=35.3s < 36.2s).
        var shadowState = TopP5DeltaState.FromNetworkReplay(
            msg.TetherOrder, msg.FistColors, msg.PlayerMonitorIndex,
            msg.PlayerMonitorSideIsLeft, msg.OmegaMonitorSideIsLeft, msg.EyeSpawnIsNorth,
            msg.SwivelCannonSideIsLeft, msg.ArmHandednessIsLeft, msg.FarWorldRole,
            msg.NearWorldRole, msg.FarWorldTetherIndex);
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
