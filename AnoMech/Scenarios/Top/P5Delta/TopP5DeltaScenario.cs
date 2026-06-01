using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaScenario : IScenario
{
    public string Name => "TOP P5 Delta";
    // TerritoryId 1122 = The Omega Protocol (Ultimate). Origin and PlayerPosition Y
    // values are placeholders — tune after first in-game test once zone loads.
    public TargetInstance TargetInstance { get; } = new(
        TerritoryId: 1122,
        Origin: new Vector3(100f, 0f, 100f),
        PlayerPosition: new Vector3(100f, 0f, 116f),
        WeatherId: 174);
    public IReadOnlyList<Waymark> Waymarks { get; } = TopUtils.TopWaymarks;
    public ushort Bgm => BgmId.TopP5;
    public void DrawSettings() => settingsWindow.Draw();
    private readonly TopP5DeltaSettingsWindow settingsWindow = new();

    private TopUtils topUtils = null!;
    
    private TopP5DeltaState state = null!;
    private TopP5DeltaAi ai = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private readonly Random rng = new();

    private SimEnemy? omega;
    private SimEnemy? beetle;
    private SimEnemy? finalHelper;
    private SimEnemy? opticalUnit;
    private List<SimEnemy?>? rocketPunches;
    private List<SimEnemy?>? armUnits;
    private List<SimTether> tethersShort = [];
    private List<SimTether> tethersLong = [];
    private Vector3? pilePitchPosition;
    private TopUtils.HelloWorldSolver? nearSolver;
    private TopUtils.HelloWorldSolver? farSolver;

    public void Run(SimWorld worldParam, bool solo)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5DeltaState(settingsWindow.Overrides, party.PlayerRole);
        ai = new TopP5DeltaAi(state);
        ai.Run(world);
        topUtils = new TopUtils(world);

        world.EnforceArenaBoundary(Geometry.ArenaRadius);

        // Replay the server MapEffect IPC that sets up the TOP arena for P5.
        // Only has visible effect when physically inside TOP (ContentDirector must exist
        // with the territory's layout rows loaded). Safe to call elsewhere — no-ops.
        world.Events.Add(1f, () => topUtils.InitTopArena());

        world.Events.Add(0.1f, SpawnOmega);
        world.Events.Add(2f, () => omega?.Cast(ActionId.RunMiDeltaVersion));
        // Delta arena transition animation (index 0x07) — real game fires these
        // at +8/+24/+27/+42s relative to the Run: mi cast. Cast is at t=2f here.
        world.Events.Add(10f, EyeSpawn);
        world.Events.Add(26.1f, EyeStartCharging);
        world.Events.Add(29f, EyeDoneCharging);
        world.Events.Add(44f, EyeDespawn);
        world.Events.Add(10f, () => omega?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(10.1f, ApplyDeltaTethers);           // HW debuffs confirmed at t=10.053s
        world.Events.Add(10.1f, SpawnDeltaAdds);
        world.Events.Add(10f, () => omega?.SetTargetable(false));
        world.Events.Add(17.3f, SpawnRocketPunches); // Peripheral Synthesis fires t=17.31s
        world.Events.Add(20.3f, () =>finalHelper?.Cast(ActionId.ArchivePeripheral));
        world.Events.Add(23.5f, SpawnArmUnits);               // Archive Peripheral fires t=20.30s
        world.Events.Add(25.3f, MarkArmUnitRotations);        // +1s after arm spawn
        world.Events.Add(28.4f, () => omega?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(28.1f, ApplyDeltaRealTethers); // same window as optical laser
        world.Events.Add(29.5f, () => topUtils.ResolveOpticalLaser(opticalUnit));
        world.Events.Add(30.5f, StartMonitors);         // BeyondDefense + OWC casts start t=30.43/30.47s
        world.Events.Add(30.1f, StartPunchExplosions);  // 3s visual cast, resolves at 33.5f
        world.Events.Add(33.5f, ResolvePunchExplosions);
        world.Events.Add(35.3f, FireBeyondDefenseAoe);        // BeyondDefense jump t=35.336s
        world.Events.Add(35.6f, StartHyperPulse);             // HyperPulse cast starts t=35.559s
        world.Events.Add(35.7f, ResolveBeyondDefenseAoe);     // BeyondDefense AOE lands t=35.649s
        world.Events.Add(35.2f, DespawnRocketPunches);
        world.Events.Add(38.1f, ResolveHyperPulseFirst);      // HyperPulse fires t=38.060s
        world.Events.Add(38.6f, NextHyperPulse);              // rotation steps ~0.58s each
        world.Events.Add(39.2f, NextHyperPulse);
        world.Events.Add(39.8f, NextHyperPulse);
        world.Events.Add(40.4f, NextHyperPulse);
        world.Events.Add(40.5f, ResolveMonitors);             // OWC AOE fires t=40.509s
        world.Events.Add(41.0f, NextHyperPulse);              // last HP step, same tick as pile pitch t=40.999s
        world.Events.Add(41.0f, FirePilePitch);               // Pile Pitch fires t=40.999s
        world.Events.Add(41.1f, ResolvePilePitch);
        world.Events.Add(44.5f, () => armUnits?.ForEach(unit => unit?.PlayActionTimeline(TimelineId.WarpOut)));
        world.Events.Add(45.5f, () => armUnits?.ForEach(unit => unit?.Despawn()));
        world.Events.Add(43.5f, StartSwivelCannon);           // Swivel Cannon cast starts t=43.458s
        world.Events.Add(44.1f, () => omega?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(43.5f, () => finalHelper?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(45.5f, () => finalHelper?.Despawn());  // despawn signal t=43.591s
        world.Events.Add(47.5f, () => CheckTethersExpired(tethersShort));             // tethers applied t=30.2, 18s life → expire 48.2
        world.Events.Add(53.2f, ResolveSwivelCannon);         // 43.458 + 9.7s cast = t=53.158s
        world.Events.Add(53.2f, () => DropHelloPuddle(state.NearWorldRole, true));
        world.Events.Add(53.2f, () => DropHelloPuddle(state.FarWorldRole, false));
        world.Events.Add(54.2f, () => omega?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(54.2f, () => HopHelloPuddle(true));
        world.Events.Add(54.2f, () => HopHelloPuddle(false));
        world.Events.Add(55.2f, () => HopHelloPuddle(true));
        world.Events.Add(55.2f, () => HopHelloPuddle(false));
        world.Events.Add(56.6f, () => beetle?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(58.6f, () => beetle?.Despawn());
        world.Events.Add(56.5f, () => omega?.SetTargetable(true));
        world.Events.Add(65.1f, () => CheckTethersExpired(tethersLong));             // tethers expire t=30.2+36=66.2
    }
    
    public void Tick(float delta, float elapsed)
    {
        TickTethers(tethersLong, tether => tether.StretchLt(Geometry.HwTetherBreakDistance));
        TickTethers(tethersShort, tether => tether.StretchGt(Geometry.HwTetherBreakDistance));
        topUtils.CheckHelloWorldDeath();
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
            Level: Level,
            Targetable: true,
            InitialModeAttributeFlags: 0x10,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    private void SpawnDeltaAdds()
    {
        beetle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BeetleHelper,
            NameId: BNpcNameId.OmegaBeetle,
            Level: Level,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(-20f, 0f, 0f) * state.EyeSpawn.Mul, MathF.PI / 2f * state.EyeSpawn.Mul)));
        beetle?.PlayActionTimeline(TimelineId.Spawn);

        opticalUnit = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OpticalUnit,
            NameId: BNpcNameId.OpticalUnit,
            Level: Level,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(new Vector3(0f, 0f, -45f) * state.EyeSpawn.Mul, MathF.PI / 2f - MathF.PI / 2 * state.EyeSpawn.Mul)));

        finalHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.FinalHelper,
            NameId: BNpcNameId.OmegaFinal,
            Level: Level,
            Targetable: false,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(20f, 0f, 0f) * state.EyeSpawn.Mul, -MathF.PI / 2f * state.EyeSpawn.Mul)));
        finalHelper?.PlayActionTimeline(TimelineId.Spawn);
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
                                             Level: Level,
                                             Targetable: false,
                                             EnemyList: EnemyListMode.Always,
                                             Placement: placement));
            punch?.AddVfx("vfx/monster/m0114/eff/m0114cbbm_sp_pop_c0i.avfx", persistent: false);
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
                Level: Level,
                Targetable: false,
                EnemyList: EnemyListMode.Always,
                Placement: new Placement(Geometry.ArmUnitPlacements[i].Position * new Vector3(state.EyeSpawn.Mul, 1, 1), Geometry.ArmUnitPlacements[i].Rotation)));
            unit?.PlayActionTimeline(TimelineId.Spawn);
            return unit;
        }).ToList();
    }

    private void MarkArmUnitRotations()
    {
        armUnits?.Select((unit, i) => (unit, i))
            .ToList()
            .ForEach(t => t.unit?.AttachLockonVfx(state.ArmHandedness[t.i].RotateLockonId, persistent: false));
            // .ForEach(t => t.unit?.AttachLockonVfx(state.ArmHandedness[t.i].RotateLockonId, duration: 8f));
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
        SpawnHwTetherHelper(a.Position, ActionId.HwTetherBreak);
        SpawnHwTetherHelper(b.Position, ActionId.HwTetherBreak);
        tether.Despawn();
    }

    private void OnTetherFailed(SimTether tether)
    {
        if (tether.Resolved) return;
        if (tether.A is not { } a || tether.B is not { } b) return;
        Plugin.Log.Info($"Tether failed {tether.TetherId}");
        tether.Resolved = true;
        party.WipeAllPlayers("HW Tether Fail (raidwide wipe)");
        SpawnHwTetherHelper(a.Position, ActionId.HwTetherFail);
        SpawnHwTetherHelper(b.Position, ActionId.HwTetherFail);
        tether.Despawn();
    }

    private void SpawnHwTetherHelper(Vector3 pos, uint actionId)
    {
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaHelper,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(pos, 0f)));
        if (helper != null) world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
        helper?.Cast(actionId);
        party
             .ActiveMembers()
             .ToList()
             .ForEach(ApplyHwTetherBreakHit);
    }

    private void ApplyHwTetherBreakHit(SimCharacter player)
    {
        if (IsDamageLethal(player, magic: true, comeRuin: 3)) { player.Die("HW Tether Break"); return; }
        player.AddStatus(StatusId.TriceComeRuin, Duration.HwTetherBreakStack);
        player.AddStatus(StatusId.MagicVulnerabilityUpMini, Duration.HwTetherBreakStack);
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
        state.PunchTargets = Enumerable.Range(0, 8)
                                       .Select(i => party.Get(state.TetherOrder[i])!.Position)
                                       .ToList();
        for(var i = 0; i < 8; i++)
        {
            var punch = rocketPunches[i];
            if (punch is null) continue;
            var targets = Enumerable.Range(0, 8)
                                    .Where(k => k != i)
                                    .Select(k => new RocketPunchTarget(state.PunchTargets[k], 0f, state.FistColors[k]))
                                    .ToList();
            var inRange = punch.Find(targets).InsideCircle(state.PunchTargets[i], Geometry.RocketPunchAoeRadius);
            bool failed = inRange.Count != 1 || inRange[0].FistColor == state.FistColors[i];
            if (failed) state.PunchExplosionUnmitigated = true;
            punch.Cast(failed ? ActionId.DeltaUnmitigatedExplosion : ActionId.DeltaExplosion, state.PunchTargets[i]);
        }
    }

    private void ResolvePunchExplosions()
    {
        if (state.PunchTargets is null) return;
        state.PunchTargets
             .SelectMany(target => party.Find.InsideCircle(target, Geometry.RocketPunchAoeRadius))
             .ToList()
             .ForEach(hit =>
             {
                 Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Rocket Punch AOE (lethal)");
                 hit.Die("Rocket Punch AOE");
             });
        state.PunchTargets = null;
        if (state.PunchExplosionUnmitigated)
        {
            Plugin.Log.Info("Hit: ALL PARTY by Rocket Punch — unmitigated explosion (lethal raidwide)");
            party.WipeAllPlayers("Rocket Punch — unmitigated explosion");
        }
    }

    private void FireBeyondDefenseAoe()
    {
        if (omega is null) return;
        SimCharacter? target;
        switch (state.BeyondDefenceForPlayer)
        {
            case true:
                target = party.Get(party.PlayerRole);
                break;
            case false:
            {
                var player = party.Get(party.PlayerRole);
                var closest2 = party.Find.ClosestN(omega.Position, 2);
                if (closest2.Any(m => m == player))
                    target = closest2.FirstOrDefault(m => m != player);
                else
                    target = closest2.Count > 0 ? closest2[Random.Shared.Next(closest2.Count)] : null;
                break;
            }
            default:
                target = party.Find.RandomClosestN(omega.Position, 2);
                break;
        }
        if (target is null) return;
        state.BeyondDefenseTarget = ((ISimPartyMember)target).Role;
        Plugin.Log.Info($"Beyond defense target {((ISimPartyMember)target).Role}");
        omega.Cast(
            ActionId.BeyondDefenseAOE,
            targetLocation: target.Position,
            targetId: target.GameObjectId);
    }

    private void ResolveBeyondDefenseAoe()
    {
        var mainTarget = party.Get(state.BeyondDefenseTarget);
        if (mainTarget is null) return;

        foreach (var hit in party.Find.InsideCircle(mainTarget.Position, Geometry.BeyondDefenseAoeRadius))
            if (hit != mainTarget)
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Beyond Defense AOE (lethal)");
                hit.Die("Beyond Defense AOE");
            }

        var mainLethal = IsDamageLethal(mainTarget, magic: false, comeRuin: 2);
        Plugin.Log.Info($"Hit: {(mainTarget as ISimPartyMember)?.Role} by Beyond Defense ({(mainLethal ? "lethal" : "non-lethal")})");
        if (mainLethal)
            mainTarget.Die("Beyond Defense");
        else
            mainTarget.AddStatus(StatusId.TwiceComeRuin, 6.96f);
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
                unit.Cast(ActionId.HyperPulseDeltaCharging);
            });
    }

    private void DespawnRocketPunches()
    {
        rocketPunches?.ForEach(punch => punch?.Despawn());
        rocketPunches = null;
    }

    private void ResolveHyperPulseFirst()
    {
        if (armUnits is null) return;
        foreach (var arm in armUnits)
            if (arm is { IsActive: true }) ResolveHyperPulseRect(arm);
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
                t.unit.Cast(ActionId.HyperPulseDeltaShoot);
                ResolveHyperPulseRect(t.unit);
            });
    }

    private void ResolveHyperPulseRect(SimEnemy arm)
    {
        foreach (var hit in party.Find.InsideRect(arm.Placement(), Geometry.HyperPulseHalfWidth, Geometry.HyperPulseLength))
        {
            Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Delta Hyper Pulse (lethal)");
            hit.Die("Delta Hyper Pulse");
        }
    }

    private void ResolveMonitors()
    {
        var aoePositions = new List<Vector3>();

        if (finalHelper is {} helper)
            foreach (var m in FireMonitorOnSide(helper.Placement(), state.OmegaMonitorSide, exclude: null))
                aoePositions.Add(m.Position);

        var playerMonitor = party.Get(state.TetherOrder[state.PlayerMonitorIndex])!;
        foreach (var m in FireMonitorOnSide(playerMonitor.Placement(), state.PlayerMonitorSide, exclude: playerMonitor))
            aoePositions.Add(m.Position);
        playerMonitor.RemoveStatus(state.PlayerMonitorSide.MonitorDebuffId);

        var hit = new HashSet<SimCharacter>();
        foreach (var pos in aoePositions)
            foreach (var member in party.Find.InsideCircle(pos, Geometry.OversampledWaveCannonAoeRadius))
                hit.Add(member);

        foreach (var member in hit)
        {
            if (!member.IsActive) continue;
            var lethal = IsDamageLethal(member, magic: true, comeRuin: 2);
            Plugin.Log.Info($"Hit: {(member as ISimPartyMember)?.Role} by Oversampled Wave Cannon ({(lethal ? "lethal" : "non-lethal")})");
            if (lethal)
                member.Die("Oversampled Wave Cannon");
            else
            {
                member.AddStatus(StatusId.MagicVulnerabilityUp, 4.96f);
                member.AddStatus(StatusId.TwiceComeRuin, 6.96f);
            }
        }
    }

    private IReadOnlyList<SimCharacter> FireMonitorOnSide(Placement src, Side side, SimCharacter? exclude = null)
    {
        var targets = party.Find.OnSideN(src, side.Mul, count: 2, exclude: exclude);
        foreach (var member in targets)
        {
            var pos = member.Position;
            var spawned = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.OmegaHelper,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Placement: new Placement(pos, 0f)));
            if (spawned != null) world.Events.Add(Duration.MonitorHelperLifetime, spawned.Despawn);
            spawned?.Cast(ActionId.OversampledWaveCannonAoe, targetLocation: pos, targetId: member.GameObjectId);
        }
        return targets;
    }

    private void FirePilePitch()
    {
        if (omega is null) return;
        if (party.Find.Closest(omega.Position) is not { } target) return;
        pilePitchPosition = target.Position;
        omega.Cast(
            ActionId.PilePitch,
            targetLocation: target.Position,
            targetId: target.GameObjectId);
    }

    private void ResolvePilePitch()
    {
        if (pilePitchPosition is null) return;
        var pos = pilePitchPosition.Value;
        pilePitchPosition = null;

        var inAoe = party.Find.InsideCircle(pos, Geometry.PilePitchAoeRadius);
        if (inAoe.Count < 3)
        {
            foreach (var hit in inAoe)
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Pile Pitch — too few players (lethal)");
                hit.Die("Pile Pitch (too few players)");
            }
            return;
        }

        foreach (var hit in inAoe)
        {
            if (!hit.IsActive) continue;
            var lethal = IsDamageLethal(hit, magic: true, comeRuin: 2);
            Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Pile Pitch ({(lethal ? "lethal" : "non-lethal")})");
            if (lethal)
                hit.Die("Pile Pitch");
            else
                hit.AddStatus(StatusId.TwiceComeRuin, 6.96f);
        }
    }

    private void StartSwivelCannon()
    {
        beetle?.Cast(
            state.SwivelCannonSide.SwivelCannonActionId,
            omenDelay: 8.5f,
            omenRotate: state.SwivelCannonSide.Mul * MathF.PI / 2);
    }

    private void ResolveSwivelCannon()
    {
        omega?.SetModeAttributeFlags(0x32);
        omega?.SetModelState(0x00);
        if (beetle is null) return;
        var rotation = beetle.Rotation + state.SwivelCannonSide.Mul * MathF.PI / 2;
        var placement = new Placement(beetle.Position, rotation);
        foreach (var hit in party.Find.InsideCone(placement, Geometry.SwivelCannonHalfAngle, Geometry.SwivelCannonRange))
        {
            Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Swivel Cannon (lethal)");
            hit.Die("Swivel Cannon");
        }
    }

    private void CheckTethersExpired(List<SimTether> tethers)
    {
        foreach (var t in tethers)
            if (!t.Resolved) OnTetherFailed(t);
    }

    private void DropHelloPuddle(PartyRole role, bool near)
    {
        if (near)
            nearSolver = topUtils.HelloWorld(role, true);
        else
            farSolver = topUtils.HelloWorld(role, false);
        HopHelloPuddle(near);
    }

    private void HopHelloPuddle(bool near)
    {
        var solver = near ? nearSolver : farSolver;
        if ( solver?.Position is not {} position) return;
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
                                          BNpcBaseId: BNpcBaseId.OmegaHelper,
                                          Targetable: false,
                                          EnemyList: EnemyListMode.Never,
                                          Placement: new Placement(position, 0f)));
        if (helper != null) world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
        solver.CastSpell(helper);
    }


    private bool IsDamageLethal(SimCharacter character, bool magic, int comeRuin)
    {
        var who = (character as ISimPartyMember)?.Role.ToString() ?? character.GetType().Name;
        var ruinWeight = comeRuin switch { 2 => 0.6f, 3 => 0.4f, _ => 0f };
        var twiceRuin = character.HasStatus(StatusId.TwiceComeRuin);
        if (twiceRuin) ruinWeight += 0.6f;
        var triceStacks = character.FindStatus(StatusId.TriceComeRuin)?.Stacks ?? 0;
        ruinWeight += triceStacks * 0.4f;
        var ruinLethal = ruinWeight > 1;
        var magicVuln1 = character.HasStatus(StatusId.MagicVulnerabilityUp);
        var magicVuln2Stacks = character.FindStatus(StatusId.MagicVulnerabilityUpMini)?.Stacks ?? 0;
        var magicLethal = magic && (magicVuln1 || magicVuln2Stacks > 1);
        var lethal = ruinLethal || magicLethal;
        Plugin.Log.Info($"IsDamageLethal: {who} magic={magic} comeRuin={comeRuin} → {lethal} [ruinWeight={ruinWeight:F2} TwiceComeRuin={twiceRuin} TriceComeRuin={triceStacks} MagicVulnerabilityUp={magicVuln1} MagicVulnUp2={magicVuln2Stacks}]");
        return lethal;
    }

    // Delta arena transition animation (index 0x07).
    // Real game fires at +8/+24/+27/+42s relative to "Run: mi (Delta Version)" cast.
    private void EyeSpawn() => world.Map.AddEffect(0x00000002, state.EyeSpawn.EffectIndex);
    private void EyeStartCharging()  => world.Map.AddEffect(0x00800040, state.EyeSpawn.EffectIndex);
    private void EyeDoneCharging()  => world.Map.AddEffect(0x10000001, state.EyeSpawn.EffectIndex);
    private void EyeDespawn()    => world.Map.AddEffect(0x00000008, state.EyeSpawn.EffectIndex);
}
