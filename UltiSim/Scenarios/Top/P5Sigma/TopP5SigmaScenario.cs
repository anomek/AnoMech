using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UltiSim.Core;
using UltiSim.Core.Map;
using UltiSim.Core.SimObjects;
using UltiSim.Scenarios;
using static UltiSim.Scenarios.Top.P5Sigma.TopP5SigmaConstants;

namespace UltiSim.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaScenario : IScenario
{
    public string Name => "TOP P5 Sigma";

    public TargetInstance? TargetInstance { get; } = new(
        TerritoryId: 1122,
        Origin: new Vector3(100f, 0f, 100f),
        PlayerPosition: new Vector3(100f, 0f, 116f),
        WeatherId: 174);

    public IReadOnlyList<ScenarioOriginOverride> OriginOverrides { get; } = [
        new(TerritoryId: 801, X: 100f, Z: 100f),
        new(TerritoryId: 1045, X: 0f, Z: 0)
    ];

    public IReadOnlyList<Waymark> Waymarks { get; } = TopUtils.TopWaymarks;

    public ushort Bgm => TopConstants.BgmId.TopP5;

    public void DrawSettings() => settingsWindow.Draw();
    private readonly TopP5SigmaSettingsWindow settingsWindow = new();

    private TopP5SigmaState state = null!;
    private TopP5SigmaAi ai = null!;
    private SimWorld world = null!;
    private SimParty party = null!;

    private SimEnemy? omegaM;
    private SimEnemy? finalHelper;
    private SimEnemy? beetleHelper;
    private SimEnemy? omegaMClone;
    private SimEnemy? omegaFCloneA;
    private SimEnemy? armUnitA;
    private SimEnemy? armUnitB;
    private SimTether? tetherUnitA;
    private SimTether? tetherUnitB;
    private SimEnemy? rearPower;
    private List<SimEnemy?>? towers;
    private readonly List<SimTether> sigmaTethers = new();

    public void Run(SimWorld worldParam, PartyRole playerRole)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5SigmaState(settingsWindow.Overrides, playerRole);
        ai = new TopP5SigmaAi(state);
        ai.Run(world);

        // Sigma fragment timeline. Absolute literal timestamps only, sorted ascending.
        // Don't add events anywhere else unless there is no other way around it.

        world.Events.Add(0.0f, SpawnOmegaM);
        world.Events.Add(0.1f, ApplyQuickeningDynamis);
        world.Events.Add(0.4f, () => omegaM?.Cast(ActionId.Unknown7C01));            // TODO: identify
        world.Events.Add(0.7f, () => omegaM?.Cast(ActionId.Unknown7B42));            // TODO: identify
        world.Events.Add(1.0f, () => TopUtils.InitTopArena(world));
        world.Events.Add(2.0f, () => omegaM?.Cast(ActionId.RunMiSigmaVersion, castSeconds: 4.7f));
        world.Events.Add(10.1f, () => omegaM?.SetTargetable(false));
        world.Events.Add(10.1f, () => omegaM?.PlayActionTimeline(TopConstants.TimelineId.WarpOut));
        world.Events.Add(10.1f, ApplySigmaTethers);
        world.Events.Add(10.1f, ApplyHelloWorldDebuffs);
        world.Events.Add(10.2f, SpawnArmUnits);
        world.Events.Add(10.8f, ApplyHyperPulseTethers);
        world.Events.Add(11.7f, WarpOmegaToNewNorth);
        world.Events.Add(15.2f, SpawnSpinner);
        world.Events.Add(18.2f, SpawnSigmaHelper);
        world.Events.Add(20.4f, CastWaveCannon);
        world.Events.Add(24.4f, () => omegaM?.Cast(ActionId.SubjectSimulationF));
        world.Events.Add(25.4f, () => omegaMClone?.Cast(ActionId.Unknown7B14));      // TODO: identify
        world.Events.Add(25.9f, CastProgramLoop);
        world.Events.Add(26.5f, () => omegaM?.Cast(ActionId.Unknown7B16));           // TODO: identify
        world.Events.Add(28.1f, ResolveWaveCannon);
        world.Events.Add(29.2f, FireHyperPulse);
        world.Events.Add(29.4f, SpawnTowers);
        world.Events.Add(29.4f, ResolveTowerWaveCannon);
        world.Events.Add(30.6f, () => omegaM?.Cast(ActionId.Unknown7F30));           // TODO: identify
        world.Events.Add(31.2f, () => { armUnitA?.Despawn(TopConstants.TimelineId.WarpOut, 1f); armUnitB?.Despawn(TopConstants.TimelineId.WarpOut, 1f); });
        // TODO: second arm-unit cycle — log shows warp-in at t=44.2, tether re-target,
        // second Hyper Pulse cast at t=66.2, final warp-out at t=68.3. Wire when ready.
        world.Events.Add(31.7f, SpawnOmegaClones);
        world.Events.Add(34.2f, () => towers?[0]?.Cast(ActionId.Unknown7B15));       // TODO: identify
        world.Events.Add(34.7f, () => omegaM?.Cast(ActionId.Unknown7B20));           // TODO: identify
        world.Events.Add(36.8f, () => omegaM?.Cast(ActionId.Unknown7B43));           // TODO: identify
        world.Events.Add(37.9f, () => omegaM?.Cast(ActionId.Discharger));
        world.Events.Add(41.9f, ResolveStorageViolation);
        world.Events.Add(53.2f, SpawnRearPowerUnit);
        world.Events.Add(53.2f, () => rearPower?.Cast(ActionId.RearLasersStart, castSeconds: 2.7f));
        world.Events.Add(56.84f, () => rearPower?.Cast(ActionId.RearLasersTick));
        world.Events.Add(57.42f, () => rearPower?.Cast(ActionId.RearLasersTick));
        world.Events.Add(57.9f, ResolveSuperliminalSteel);
        world.Events.Add(58.00f, () => rearPower?.Cast(ActionId.RearLasersTick));
        world.Events.Add(58.58f, () => rearPower?.Cast(ActionId.RearLasersTick));
        world.Events.Add(59.16f, () => rearPower?.Cast(ActionId.RearLasersTick));
        // Log: 4000A63C plays warp-out (273 0197 1E39) at 01:24:05.18 from
        // NewNorthA before the second teleport back to center.
        world.Events.Add(62.5f, () => omegaM?.PlayActionTimeline(TopConstants.TimelineId.WarpOut));
        world.Events.Add(66.2f, ResolveHelloWorldInitial);
        world.Events.Add(67.2f, ResolveHelloWorldJump);
        // Log: second jump fires at 01:24:10.883 (t=68.2).
        world.Events.Add(68.2f, ResolveHelloWorldJump);
        // Log: 4000A63C plays warp-in (273 0197 1E43) at 01:24:10.348 (t=67.6)
        // and flips targetable=01 at 01:24:13.43 (t=70.7). Warp-in must precede
        // SetTargetable so the model is back before clicks land.
        world.Events.Add(67.6f, WarpOmegaBackToCenter);
        world.Events.Add(70.7f, () => omegaM?.SetTargetable(true));
    }

    public void Tick(float delta, float elapsed)
    {
        List<SimTether> resolved = [];
        foreach (var tether in sigmaTethers)
        {
            if (tether.IsActive)
            {
                tether.A.RemoveStatus(TopConstants.StatusId.VulnerabilityUp);
                tether.B.RemoveStatus(TopConstants.StatusId.VulnerabilityUp);
                resolved.Add(tether);
            }
            else if (tether.StretchLt(Geometry.SigmaTetherMinDistance) ||
                tether.StretchGt(Geometry.SigmaTetherMaxDistance))
            {
                tether.A.AddStatus(TopConstants.StatusId.VulnerabilityUp);
                tether.B.AddStatus(TopConstants.StatusId.VulnerabilityUp);
            }
            else
            {
                tether.A.RemoveStatus(TopConstants.StatusId.VulnerabilityUp);
                tether.B.RemoveStatus(TopConstants.StatusId.VulnerabilityUp);
            }
        }
        resolved.ForEach(t => sigmaTethers.Remove(t));

        if (tetherUnitA is { Resolved: false })
           armUnitA?.Face(tetherUnitA.B);
        if (tetherUnitB is { Resolved: false })
            armUnitB?.Face(tetherUnitB.B);
    }

    private void SpawnOmegaM()
    {
        omegaM = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.StarterOmega,
            NameId: TopConstants.BNpcNameId.OmegaM,
            Level: TopConstants.Level,
            Targetable: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    private void ApplyQuickeningDynamis()
    {
        var members = party.AllMembers().ToArray();
        for (int i = 0; i < members.Length && i < state.QuickenedSlots.Count; i++)
            if (state.QuickenedSlots[i])
                members[i].AddStatus(TopConstants.StatusId.QuickeningDynamis, 0f, 1);
    }

    private void ApplySigmaTethers()
    {
        // Four player-to-player pairs from the log type-35 lines at t=10.1.
        // SigmaOrder shuffles roles; pairs are (0,1), (2,3), (4,5), (6,7).
        // The returned tethers are tracked so Tick can apply the distance-fail
        // vuln-up when paired players drift too close or too far.
        var targets = new SimCharacter[8];
        for (int i = 0; i < 8; i++) targets[i] = party.Get(state.SigmaOrder[i])!;
        sigmaTethers.Clear();
        sigmaTethers.Add(world.Tether(targets[0], targets[1], TetherId.SigmaPair, 18f, StatusId.MidGlitch));
        sigmaTethers.Add(world.Tether(targets[2], targets[3], TetherId.SigmaPair, 18f, StatusId.MidGlitch));
        sigmaTethers.Add(world.Tether(targets[4], targets[5], TetherId.SigmaPair, 18f, StatusId.MidGlitch));
        sigmaTethers.Add(world.Tether(targets[6], targets[7], TetherId.SigmaPair, 18f, StatusId.MidGlitch));

        ReadOnlySpan<uint> pairMarkers =
        [
            LockonId.PairTriangle,
            LockonId.PairCircle,
            LockonId.PairSquare,
            LockonId.PairCross,
        ];
        for (int p = 0; p < 4; p++)
        {
            targets[p * 2]?.AttachLockonVfx(pairMarkers[p], persistent: false);
            targets[p * 2 + 1]?.AttachLockonVfx(pairMarkers[p], persistent: false);
        }
    }

    private void ApplyHelloWorldDebuffs()
    {
        party.Get(state.NearWorldRole)?.AddStatus(StatusId.HelloNearWorld, Durations.HelloWorldDebuff);
        party.Get(state.FarWorldRole)?.AddStatus(StatusId.HelloFarWorld, Durations.HelloWorldDebuff);
    }

    private void CastProgramLoop()
    {
        beetleHelper?.Cast(ActionId.ProgramLoop);
        foreach (var member in party.AllMembers())
            member.AddStatus(StatusId.Looper, Durations.Looper);
    }

    private void SpawnArmUnits()
    {
        armUnitA = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.RightArmUnit,
            NameId: TopConstants.BNpcNameId.RightArmUnit,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: state.NewNorthA.Apply(new Placement(new(-8.5f, 0, 8.5f), MathF.PI / 4))));
        armUnitA?.PlayActionTimeline(TopConstants.TimelineId.Spawn);

        armUnitB = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.RightArmUnit,
            NameId: TopConstants.BNpcNameId.RightArmUnit,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: state.NewNorthA.Apply(new Placement(new (8.5f, 0, 8.5f), - MathF.PI / 4))));
        armUnitB?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }

    private void ApplyHyperPulseTethers()
    {
        // Log: type-35 tetherId 0x0011 from each arm unit to a player at t=10.78.
        // The mechanic targets the furthest player from each arm unit; players move
        // and the tether re-targets dynamically (line 54461 shows 4000A643 retether
        // to a different player at t=13.2). For now, freeze the target at apply-time.
        if (armUnitA != null)
            tetherUnitA = world.TetherFarestPlayer(armUnitA, TetherId.HyperPulseBait, duration: 20f);
        if (armUnitB != null)
            tetherUnitB = world.TetherFarestPlayer(armUnitB, TetherId.HyperPulseBait, duration: 20f);
    }

    private void WarpOmegaToNewNorth()
    {
        omegaM?.SetPosition(state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, -20f), 0f)).LocalToGlobal(world.ScenarioOrigin));
        omegaM?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }

    private void SpawnSpinner()
    {
        finalHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.FinalHelper,
            NameId: TopConstants.BNpcNameId.OmegaFinal,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: state.NewNorthA.Apply(new Placement(Vector3.Zero, 0))));
        finalHelper?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }

    private void SpawnSigmaHelper()
    {
        beetleHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.BeetleHelper,
            NameId: TopConstants.BNpcNameId.OmegaBeetle,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: state.NewNorthA.Apply(new Placement(new Vector3(0f, 0f, 20f), MathF.PI))));
        beetleHelper?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }

    private void CastWaveCannon()
    {
        finalHelper?.Cast(ActionId.WaveCannon, castSeconds: 7.7f);
        foreach (var slot in state.WaveCannonTargetSlots)
            party.Get(state.SigmaOrder[slot])?.AttachLockonVfx(LockonId.WaveCannon, persistent: false);
    }

    private void ResolveWaveCannon()
    {
        // TODO: damage from wave cannon
        // foreach (var slot in state.WaveCannonTargetSlots)
        //     finalHelper?.Cast(ActionId.WaveCannonHit, party.Get(state.SigmaOrder[slot])!.Position);
    }

    private void FireHyperPulse()
    {
        if (tetherUnitA != null)
            armUnitA?.Cast(ActionId.HyperPulse, tetherUnitA.B.Position);
        if (tetherUnitB != null)
            armUnitB?.Cast(ActionId.HyperPulse, tetherUnitB.B.Position);
    }

    private void SpawnTowers()
    {
        // Four cardinal towers at the arena center. Real positions in the log are
        // (100, 100) (overlapping); separate them visually for the sim.
        towers = new List<SimEnemy?>
        {
            SpawnTower(new Vector3(0f, 0f, -10f)),
            SpawnTower(new Vector3(10f, 0f, 0f)),
            SpawnTower(new Vector3(0f, 0f, 10f)),
            SpawnTower(new Vector3(-10f, 0f, 0f)),
        };
    }

    private SimEnemy? SpawnTower(Vector3 offset) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.OmegaHelper,
            NameId: TopConstants.BNpcNameId.OmegaFinal,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: false,
            Placement: new Placement(offset, 0f)));

    private void ResolveTowerWaveCannon()
    {
        // TODO: 4 wave-cannon hits on tower-target slots. PairIsTarget +
        // NonTargetMemberIsFirst pick which players the towers fire at.
        towers?.ForEach(t => t?.Cast(ActionId.WaveCannonHit));
    }

    private void SpawnOmegaClones()
    {
        // Per Sigma.md item #3: untargetable Omega-M spawns at "new north"
        // (state.NewNorthA — random cardinal or intercardinal). Distance ~14
        // to match the original sigma-helper intercardinal radius (sqrt(2)*10).
        // Rotation faces inward toward arena center.
        var mPos = OffsetAtDirection(state.NewNorthA, distance: 14f);
        var mFacing = state.NewNorthA.RadiansFromNorth + MathF.PI;        // face center (180° from outward)
        omegaMClone = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaMClone,
            NameId: TopConstants.BNpcNameId.OmegaM,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: new Placement(mPos, mFacing)));
        omegaMClone?.PlayActionTimeline(TopConstants.TimelineId.Spawn);

        // Omega-F clone — placeholder at the opposite intercardinal until
        // state.NewNorthB / OmegaFForm wiring is hardened.
        omegaFCloneA = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaFClone,
            NameId: BNpcNameId.OmegaF,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: true,
            Placement: new Placement(-mPos, state.NewNorthA.RadiansFromNorth)));
        omegaFCloneA?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }

    // EightWayDirection -> scenario-local offset. North = -Z, East = +X
    // (standard FFXIV convention, matches TopP5DeltaConstants.Geometry.ArmUnitPlacements).
    private static Vector3 OffsetAtDirection(EightWayDirection dir, float distance)
    {
        var rad = dir.RadiansFromNorth;
        return new Vector3(MathF.Sin(rad) * distance, 0f, -MathF.Cos(rad) * distance);
    }

    private void ResolveStorageViolation()
    {
        // TODO: 2-target stack (7B05) on two players + 1-target spread (7B04) on the rest.
        // Current placeholder fires both via the towers if alive.
        if (towers == null) return;
        towers[0]?.Cast(ActionId.StorageViolationStack);
        towers[1]?.Cast(ActionId.StorageViolationSpread);
    }

    private void SpawnRearPowerUnit()
    {
        rearPower = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.RearPowerUnit,
            Level: TopConstants.Level,
            Targetable: false,
            InEnemyList: false,
            Placement: new Placement(Vector3.Zero, state.NewNorthB.RadiansFromNorth)));
    }

    private void ResolveSuperliminalSteel()
    {
        // M is the visible cast; the two F clones fire the side variants based
        // on state.OmegaFForm (LegBlades vs Staff).
        omegaM?.Cast(ActionId.SuperliminalSteel, castSeconds: 1.2f);
        if (state.OmegaFForm == OmegaFForm.LegBlades)
        {
            omegaFCloneA?.Cast(ActionId.SuperliminalSteelLeft, castSeconds: 1.2f);
            omegaMClone?.Cast(ActionId.SuperliminalSteelRight, castSeconds: 1.2f);
        }
        else
        {
            omegaFCloneA?.Cast(ActionId.SuperliminalSteelRight, castSeconds: 1.2f);
            omegaMClone?.Cast(ActionId.SuperliminalSteelLeft, castSeconds: 1.2f);
        }
    }

    private void ResolveHelloWorldInitial()
    {
        DropHelloPuddle(state.NearWorldRole, TopConstants.ActionId.HelloNearWorld);
        DropHelloPuddle(state.FarWorldRole, TopConstants.ActionId.HelloDistantWorld);
    }

    private void DropHelloPuddle(PartyRole role, uint spellId)
    {
        var target = party.Get(role);
        if (target == null) return;
        var pos = target.Position;
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: TopConstants.BNpcBaseId.OmegaHelper,
            Targetable: false,
            InEnemyList: false,
            Placement: new Placement(pos - world.ScenarioOrigin, 0f),
            Lifetime: 5f));
        helper?.Cast(spellId, targetLocation: pos, targetId: target.GameObjectId);
        target.AddStatus(TopConstants.StatusId.QuickeningDynamis, 0f, 1);
        target.AddStatus(TopConstants.StatusId.MagicVulnUp1, 4.96f);
    }

    private void ResolveHelloWorldJump()
    {
        state.NearWorldRole = HopHelloPuddle(state.NearWorldRole, TopConstants.ActionId.HelloNearWorldJump, useClosest: true);
        state.FarWorldRole = HopHelloPuddle(state.FarWorldRole, TopConstants.ActionId.HelloDistantWorldJump, useClosest: false);
    }

    private PartyRole HopHelloPuddle(PartyRole currentRole, uint jumpSpell, bool useClosest)
    {
        var current = party.Get(currentRole);
        if (current == null) return currentRole;
        var next = useClosest
            ? party.Find.Closest(current.Position, exclude: current)
            : party.Find.Farest(current.Position, exclude: current);
        if (next == null) return currentRole;
        DropHelloPuddle(next.Role, jumpSpell);
        return next.Role;
    }

    private void WarpOmegaBackToCenter()
    {
        omegaM?.SetPosition(new Placement(world.ScenarioOrigin, MathF.PI));
        omegaM?.PlayActionTimeline(TopConstants.TimelineId.Spawn);
    }
}
