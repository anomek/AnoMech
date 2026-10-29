using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P5ParadiseRegained.FruP5ParadiseRegainedState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P5ParadiseRegained;

public sealed class FruP5ParadiseRegainedScenario : IMultiplayerReplayable
{
    public string Name => "Paradise Regained";
    public IPhase Phase => FruZone.P5;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP5ParadiseRegainedAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly FruP5ParadiseRegainedSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private FruP5ParadiseRegainedState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it.
    public FruP5ParadiseRegainedState? LastState { get; private set; }
    private SimEnemy? pandora;
    private readonly MapEffectTower?[] towers = new MapEffectTower?[3];
    private SimTether? wingTether;
    private const uint NoActor = 0xE0000000;
    // Per round: where the light and the dark line point, from the center.
    private readonly (float Light, float Dark)?[] lineAims = new (float, float)?[4];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP5ParadiseRegainedState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        Array.Clear(lineAims);
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<FruP5ParadiseRegainedState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(1f, () => pandora?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
        world.Events.Add(1.3f, () => pandora?.Cast(ActionId.ParadiseRegained, animationLock: 3.1f));
        world.Events.Add(TowerShowAt(0), () => ShowTower(0));
        world.Events.Add(7.27f, () => BattleTalk(0x27, RyneBattleTalk));
        world.Events.Add(8.3f, () =>
        {
            pandora?.SetModelState(4);
            TetherWingBait(0);
            pandora?.Cast(state.DarkFirst ? ActionId.WingsDarkThenLight : ActionId.WingsLightThenDark, animationLock: 0.2f);
        });
        world.Events.Add(TowerShowAt(1), () => ShowTower(1));
        world.Events.Add(12.4f, TargetWingBait);
        world.Events.Add(12.4f, () => BattleTalk(0x28, GaiaBattleTalk));
        world.Events.Add(TowerShowAt(2), () => ShowTower(2));
        world.Events.Add(15.5f, () => FaceTank(PartyRole.MainTank));
        world.Events.Add(TowerHitAt(0), () => { ResolveTower(0); WingCleave(0); });
        world.Events.Add(16.5f, () => WingBuster(0));
        world.Events.Add(17f, () => TetherWingBait(1));
        world.Events.Add(TowerHitAt(1), () => ResolveTower(1));
        world.Events.Add(19.4f, () => { FaceTank(PartyRole.OffTank); WingCleave(1); });
        world.Events.Add(20.2f, () => { WingBuster(1); pandora?.SetModelState(0); });
        world.Events.Add(20.7f, () => wingTether?.Despawn());
        world.Events.Add(TowerHitAt(2), () => ResolveTower(2));
        world.Events.Add(23.1f, () =>
        {
            pandora?.SetRotation(MathF.PI);
            pandora?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        });

        world.Events.Add(30.35f, () => pandora?.Cast(ActionId.PolarizingStrikes, animationLock: 2.1f));
        for (var round = 0; round < 4; round++)
        {
            var r = round;
            world.Events.Add(LinesAt(r) - 0.1f, () => AimLines(r));
            world.Events.Add(LinesAt(r), () => CastLines(r));
            if (r < 3) world.Events.Add(PathsCastAt(r), () => pandora?.Cast(ActionId.PolarizingPaths, animationLock: 2.1f));
            world.Events.Add(EchoAt(r), () => Echo(r));
        }
        world.Events.Add(49.45f, () => BattleTalk(0x29, GaiaBattleTalk));
    }

    public void Tick(float delta, float elapsed)
    {
        foreach (var tower in towers) tower?.Tick(world.Party);
    }

    private void SpawnActors()
    {
        pandora = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Pandora,
            NameId: BNpcNameId.Pandora,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            LayoutId: LayoutId.PandoraP5));
    }

    private void ShowTower(int tower)
    {
        var slot = state.TowerSlot(tower);
        world.Map.AddEffect(MapEffect.TowerShow, slot);
        world.Map.AddEffect(MapEffect.TowerBase, (byte)(slot - MapEffect.ParadiseFirstTower + MapEffect.ParadiseFirstTowerBase));
        towers[tower] = new MapEffectTower(world.Map, slot, state.TowerPosition(tower), TowerRadius, MapEffect.TowerOccupied[..2], MapEffect.DarklitTowerEmpty);
    }

    private void BattleTalk(uint line, uint speaker) => world.Map.DirectorUpdate(0x80000027U, line, 0x2U, speaker, NoActor);

    private void FaceTank(PartyRole role)
    {
        if (world.Party.Get(role) is not { } tank || !tank.IsAlive()) return;
        pandora?.Face(tank);
    }

    private void ResolveTower(int tower)
    {
        towers[tower] = null;
        world.Map.AddEffect(MapEffect.TowerHide, state.TowerSlot(tower));
        pandora?.CastFromHelper(Actions.ParadiseTowerExplosion, at: new Placement(state.TowerPosition(tower), 0f).Face(Vector3.Zero));
    }

    public void OnActionResolved(ActionResolved resolved)
        => Towers.ResolveTower(world, resolved, ActionId.ParadiseTowerExplosion, Actions.ParadiseTowerFailure, minSoakers: 2);

    // The buster goes to whoever the tether holds: the nearest player for dark, the farthest for light.
    private void TetherWingBait(int wing)
    {
        wingTether?.Despawn();
        wingTether = state.IsDark(wing)
            ? world.Tether(pandora, End.ClosestPlayer(), TetherId.WingsDark)
            : world.Tether(pandora, End.FarthestPlayer(), TetherId.WingsLight);
    }

    private void TargetWingBait()
    {
        if (wingTether?.B is { } bait) pandora?.SetTarget(bait, follow: false);
    }

    private void WingCleave(int wing) => pandora?.Cast(state.IsDark(wing) ? Actions.WingsCleaveDark : Actions.WingsCleaveLight);

    private void WingBuster(int wing)
    {
        if (wingTether?.B is not { } bait) return;
        pandora?.CastFromHelper(state.IsDark(wing) ? Actions.WingsBusterDark : Actions.WingsBusterLight, bait,
            at: new Placement(Vector3.Zero, MathF.PI));
    }

    private void AimLines(int round)
    {
        if (pandora is not { } boss) return;
        var nearest = world.Party.ActiveMembers().OrderBy(m => Vector3.DistanceSquared(m.Position, boss.Position)).Take(2).ToList();
        if (nearest.Count < 2) return;
        var right = new Vector3(MathF.Cos(boss.Rotation), 0f, -MathF.Sin(boss.Rotation));
        var (light, dark) = Vector3.Dot(nearest[0].Position - nearest[1].Position, right) > 0f
            ? (nearest[0], nearest[1])
            : (nearest[1], nearest[0]);
        lineAims[round] = (FromCenter(light), FromCenter(dark));
    }

    private static float FromCenter(SimCharacter bait) => new Placement(Vector3.Zero, 0f).Face(bait.Position).Rotation;

    private void CastLines(int round)
    {
        if (lineAims[round] is not { } aim) return;
        pandora?.CastFromHelper(Actions.CruelPathOfLight, at: new Placement(Vector3.Zero, aim.Light));
        pandora?.CastFromHelper(Actions.CruelPathOfDarkness, at: new Placement(Vector3.Zero, aim.Dark));
    }

    // The echo repeats each line where it went, still turned to its bait.
    private void Echo(int round)
    {
        if (lineAims[round] is not { } aim) return;
        pandora?.CastFromHelper(Actions.CruelPathOfLightEcho, at: new Placement(Vector3.Zero, aim.Light));
        pandora?.CastFromHelper(Actions.CruelPathOfDarknessEcho, at: new Placement(Vector3.Zero, aim.Dark));
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new FruP5ParadiseRegainedAiReplayStateMessage(s.Rotation, s.DarkFirst) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP5ParadiseRegainedAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP5ParadiseRegainedState.FromNetworkReplay(msg.Rotation, msg.DarkFirst);
        if (shadowState == null) return null;
        ((IScenarioAi<FruP5ParadiseRegainedState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
