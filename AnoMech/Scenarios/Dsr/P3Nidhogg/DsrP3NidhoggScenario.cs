using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

// DSR P3 from Nidhogg's arrival to the auto-attack after the Soul Tether busters: Dive from Grace
// (three rounds of jumps, the towers they leave, and the Geirskogul lines each tower's clone aims at
// whoever is closest), the Drachenlance cleave, and the four counted towers with the Soul Tether
// busters.
public sealed class DsrP3NidhoggScenario : IMultiplayerReplayable
{
    public string Name => "Nidhogg";
    public IPhase Phase => DsrZone.P3;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats =>
        [new DsrP3NidhoggAi(DsrP3NidhoggAi.ArrowFacing.West), new DsrP3NidhoggAi(DsrP3NidhoggAi.ArrowFacing.East)];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly DsrP3NidhoggSettingsWindow settingsWindow = new();

    private static readonly ushort[] OrderStatus = [StatusId.FirstInLine, StatusId.SecondInLine, StatusId.ThirdInLine];
    private static readonly uint[] OrderLockon = [LockonId.FirstInLine, LockonId.SecondInLine, LockonId.ThirdInLine];

    private SimWorld world = null!;
    private DsrP3NidhoggState state = null!;
    public DsrP3NidhoggState? LastState { get; private set; }
    // Edge-triggers BuildMidRunUpdateMessage.
    private int broadcastDiveTowerRounds;
    private SimEnemy? nidhogg;
    private readonly List<SimEnemy> clones = new();
    private readonly Dictionary<PartyRole, SimEnemy> jumpClones = new();
    private readonly Dictionary<int, List<SimEnemy>> diveClones = new();
    private readonly List<SimEnemy> counterClones = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP3NidhoggState(world.Rng, world.Party.PlayerRole, settingsWindow.Overrides);
        LastState = state;
        broadcastDiveTowerRounds = 0;
        clones.Clear();
        jumpClones.Clear();
        diveClones.Clear();
        counterClones.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP3NidhoggState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.00f, PlacePlayersInCenter);
        world.Events.Add(0.00f, () => world.Map.BattleTalk(BNpcNameId.WanderingMinstrel, BattleTalkId.MinstrelBeforeNidhogg, 5000));
        world.Events.Add(0.50f, SpawnNidhogg);
        world.Events.Add(0.50f, SpawnClones);
        world.Events.Add(3.00f, () => nidhogg?.Cast(ActionId.FinalChorus, animationLock: 5.1f));
        world.Events.Add(3.00f, () => nidhogg?.SetVisibleInEnemyList(true));
        world.Events.Add(3.37f, () => nidhogg?.SetTarget(MainTank, follow: false));
        world.Events.Add(3.97f, () => nidhogg?.VoiceLine(VoiceLineId.NidhoggArrives));
        world.Events.Add(3.97f, () => nidhogg?.BattleTalk(BattleTalkId.NidhoggArrives, 4000));
        world.Events.Add(5.28f, () => nidhogg?.SetTargetable(true));
        world.Events.Add(5.32f, () => nidhogg?.CastFromHelper(Actions.FinalChorus));
        world.Events.Add(8.05f, () => nidhogg?.Face(MainTank));
        world.Events.Add(10.02f, () => nidhogg?.BattleTalk(BattleTalkId.NidhoggTaunt, 4000));
        world.Events.Add(10.17f, AutoAttack);
        world.Events.Add(13.29f, AutoAttack);
        world.Events.Add(16.41f, AutoAttack);

        world.Events.Add(18.42f, () => nidhogg?.VoiceLine(VoiceLineId.DiveFromGrace));
        world.Events.Add(18.51f, () => nidhogg?.Cast(ActionId.DiveFromGrace, animationLock: 2.1f));
        world.Events.Add(18.51f, MarkOrders);
        world.Events.Add(24.25f, MarkJumps);
        world.Events.Add(25.64f, () => CastGnashAndLash(0));
        world.Events.Add(33.16f, () => PlaceJumpClones(1));
        world.Events.Add(33.32f, () => Jump(1));
        world.Events.Add(33.48f, () => EyeOfTheTyrant(1));
        world.Events.Add(36.94f, () => Wheel(0));
        world.Events.Add(37.28f, () => MoveDiveClonesToTowers(1));
        world.Events.Add(37.42f, () => RaiseDiveTowers(1));
        world.Events.Add(39.99f, () => Wheel(1));
        world.Events.Add(41.63f, () => AimGeirskogul(diveClones[1]));
        world.Events.Add(42.53f, () => Geirskogul(diveClones[1]));
        world.Events.Add(43.23f, () => PlaceJumpClones(2));
        world.Events.Add(43.33f, () => Jump(2));
        world.Events.Add(47.15f, () => CastGnashAndLash(1));
        world.Events.Add(47.29f, () => MoveDiveClonesToTowers(2));
        world.Events.Add(47.43f, () => RaiseDiveTowers(2));
        world.Events.Add(49.13f, () => Hide(diveClones[1]));
        world.Events.Add(51.64f, () => AimGeirskogul(diveClones[2]));
        world.Events.Add(52.54f, () => Geirskogul(diveClones[2]));
        world.Events.Add(54.22f, () => PlaceJumpClones(3));
        world.Events.Add(54.31f, () => Jump(3));
        world.Events.Add(54.99f, () => EyeOfTheTyrant(3));
        world.Events.Add(58.28f, () => MoveDiveClonesToTowers(3));
        world.Events.Add(58.42f, () => RaiseDiveTowers(3));
        world.Events.Add(58.45f, () => Wheel(2));
        world.Events.Add(59.14f, () => Hide(diveClones[2]));
        world.Events.Add(61.48f, () => Wheel(3));
        world.Events.Add(62.64f, () => AimGeirskogul(diveClones[3]));
        world.Events.Add(63.54f, () => Geirskogul(diveClones[3]));
        world.Events.Add(65.70f, AutoAttack);
        world.Events.Add(68.82f, AutoAttack);

        world.Events.Add(69.60f, FaceFirstDrachenlanceTarget);
        world.Events.Add(70.07f, Drachenlance);
        world.Events.Add(70.09f, () => Hide(diveClones[3]));
        world.Events.Add(74.93f, PlaceCounterClones);
        world.Events.Add(75.10f, RaiseCounterTowers);
        world.Events.Add(75.20f, () => nidhogg?.Face(MainTank));
        world.Events.Add(79.80f, TetherSouls);
        world.Events.Add(81.72f, () => ListInEnemyList(counterClones));
        world.Events.Add(81.72f, () => AimGeirskogul(UntetheredCounterClones()));
        world.Events.Add(82.72f, () => Geirskogul(UntetheredCounterClones()));
        world.Events.Add(87.16f, SoulTether);
        world.Events.Add(89.25f, () => Hide(counterClones));
        world.Events.Add(92.28f, AutoAttack);
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.DarkdragonDive, Actions.DarkdragonDiveFail, minSoakers: 1);
        for (var i = 0; i < ActionId.DarkdragonDiveForSoakers.Length; i++)
            Towers.ResolveTower(world, resolved, ActionId.DarkdragonDiveForSoakers[i], Actions.DarkdragonDiveFail, minSoakers: i + 1);
    }

    // P3 opens with the party in the middle, where P2 left it, not at the entrance.
    private void PlacePlayersInCenter()
    {
        foreach (var player in world.Party.Players().ToList())
            if (player.IsAlive()) player.TeleportTo(new Placement(Vector3.Zero, MathF.PI));
    }

    // Hidden until Final Chorus's own animation brings him in.
    private void SpawnNidhogg()
    {
        nidhogg = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.NidhoggP3,
            NameId: BNpcNameId.Nidhogg,
            Level: Level,
            Targetable: false,
            EnemyList: EnemyListMode.Manual,
            Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Vector3.Zero, 0f),
            LayoutId: LayoutId.NidhoggP3,
            Appearance: Appearance.Nidhogg,
            Statuses: [new SpawnStatus(StatusId.HeartOfMan)]));
    }

    // Every jump and tower is one of these seven, teleported into place: the client drops what a
    // freshly spawned actor is sent. Each is seen only in its own actions' animations.
    private void SpawnClones()
    {
        for (var i = 0; i < 7; i++)
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.NidhoggClone,
                    NameId: BNpcNameId.Nidhogg,
                    Level: Level,
                    Targetable: false,
                    EnemyList: EnemyListMode.Manual,
                    Visibility: SpawnVisibility.HiddenUntilShown,
                    Placement: new Placement(new Vector3(23f, 0f, 4f), 0f),
                    LayoutId: LayoutId.NidhoggClone,
                    Appearance: Appearance.Nidhogg)) is { } clone)
                clones.Add(clone);
    }

    // The highest-numbered clone not already out, as retail picks them.
    private SimEnemy? FreeClone() => clones.LastOrDefault(clone =>
        !jumpClones.ContainsValue(clone) && !diveClones.Values.Any(round => round.Contains(clone)) && !counterClones.Contains(clone));

    private SimCharacter? MainTank => world.Party.Get(PartyRole.MainTank);

    private void AutoAttack()
    {
        if (MainTank is { } tank && tank.IsAlive()) nidhogg?.Cast(Actions.AutoAttack, tank);
    }

    private void MarkOrders()
    {
        foreach (var (role, order) in state.Order)
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(OrderStatus[order - 1]);
            member.AttachLockonVfx(OrderLockon[order - 1]);
        }
    }

    private void MarkJumps()
    {
        foreach (var (role, kind) in state.Jump)
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(JumpStatus(kind), state.Order[role] switch { 1 => 9f, 2 => 19f, _ => 30f });
        }
    }

    private static ushort JumpStatus(JumpKind kind) => kind switch
    {
        JumpKind.Forward => StatusId.SpineshatterDiveTarget,
        JumpKind.Backward => StatusId.ElusiveJumpTarget,
        _ => StatusId.HighJumpTarget,
    };

    private static EnemyAction JumpAction(JumpKind kind) => kind switch
    {
        JumpKind.Forward => Actions.DarkSpineshatterDive,
        JumpKind.Backward => Actions.DarkElusiveJump,
        _ => Actions.DarkHighJump,
    };

    private void CastGnashAndLash(int cast)
        => nidhogg?.Cast(state.OutFirst[cast] ? ActionId.GnashAndLash : ActionId.LashAndGnash, animationLock: 0.2f);

    // The jump lands where the jumper stands as its clone is placed, facing their way, or away from
    // them for Elusive Jump.
    private void PlaceJumpClones(int order)
    {
        foreach (var role in state.InOrder(order))
            if (world.Party.Get(role) is { } member && member.IsAlive() && FreeClone() is { } clone)
            {
                var turn = state.Jump[role] == JumpKind.Backward ? MathF.PI : 0f;
                clone.TeleportTo(new Placement(member.Position, member.Rotation + turn));
                jumpClones[role] = clone;
            }
    }

    // The jump's clone stays for its tower: where it landed, or 14y ahead of it for an arrow.
    private void Jump(int order)
    {
        var dives = diveClones[order] = new List<SimEnemy>();
        var towers = state.DiveTowers[order] = new List<Vector3>();
        foreach (var role in state.InOrder(order))
        {
            if (world.Party.Get(role) is not { } member) continue;
            member.RemoveStatus(OrderStatus[order - 1]);
            member.RemoveStatus(JumpStatus(state.Jump[role]));
            if (!jumpClones.Remove(role, out var clone) || !member.IsAlive()) continue;
            var kind = state.Jump[role];
            var offset = kind == JumpKind.High ? 0f : DsrP3NidhoggState.ArrowTowerOffset;
            var facing = new Vector3(MathF.Sin(clone.Rotation), 0f, MathF.Cos(clone.Rotation));
            towers.Add(clone.Position + offset * facing);
            dives.Add(clone);
            clone.Cast(JumpAction(kind), member);
        }
    }

    private void MoveDiveClonesToTowers(int order)
    {
        var dives = diveClones[order];
        for (var i = 0; i < dives.Count; i++)
            if (Vector3.Distance(dives[i].Position, state.DiveTowers[order][i]) > 0.1f)
                dives[i].TeleportTo(new Placement(state.DiveTowers[order][i], dives[i].Rotation));
    }

    private void RaiseDiveTowers(int order)
    {
        foreach (var clone in diveClones[order]) clone.Cast(Actions.DarkdragonDive);
    }

    // A random survivor outside the round's jumpers.
    private void EyeOfTheTyrant(int jumpers)
    {
        var candidates = state.Order.Where(e => e.Value != jumpers).Select(e => world.Party.Get(e.Key))
            .Where(m => m.IsAlive()).Cast<SimCharacter>().ToList();
        if (candidates.Count == 0) return;
        nidhogg?.Cast(Actions.EyeOfTheTyrant, candidates[world.Rng.Next(candidates.Count)]);
    }

    private void Wheel(int wheel)
    {
        var gnashing = (wheel % 2 == 0) == state.OutFirst[wheel / 2];
        nidhogg?.Cast(gnashing ? Actions.GnashingWheel : Actions.LashingWheel);
    }

    // Each line is aimed at whoever is closest to its clone just under a second before the cast.
    private void AimGeirskogul(IReadOnlyList<SimEnemy> aimers)
    {
        ListInEnemyList(aimers);
        foreach (var clone in aimers)
            if (world.Party.Find.Closest(clone.Position) is { } bait) clone.Face(bait);
    }

    private static void Geirskogul(IReadOnlyList<SimEnemy> aimers)
    {
        foreach (var clone in aimers) clone.Cast(Actions.Geirskogul);
    }

    private static void ListInEnemyList(IEnumerable<SimEnemy> listed)
    {
        foreach (var clone in listed) clone.SetVisibleInEnemyList(true);
    }

    private static void Hide(List<SimEnemy> spent)
    {
        foreach (var clone in spent)
        {
            clone.SetVisibleInEnemyList(false);
            clone.PlayActionTimeline(ActionTimelineId.WarpStart);
        }
        spent.Clear();
    }

    private List<SimEnemy> UntetheredCounterClones() => counterClones.Where((_, i) => i != state.TetherTower).ToList();

    private void FaceFirstDrachenlanceTarget()
    {
        if (nidhogg is { } boss && state.FirstDrachenlanceTargetIn(world.Party, boss.Position) is { } target) boss.Face(target);
    }

    private void Drachenlance()
    {
        nidhogg?.Cast(ActionId.Drachenlance, animationLock: 2.1f);
        nidhogg?.CastFromHelper(Actions.DrachenlanceCleave);
    }

    private void PlaceCounterClones()
    {
        for (var i = 0; i < 4; i++)
            if (FreeClone() is { } clone)
            {
                clone.TeleportTo(new Placement(DsrP3NidhoggState.CounterTower(i), 0f).Face(Vector3.Zero));
                counterClones.Add(clone);
            }
    }

    private void RaiseCounterTowers()
    {
        for (var i = 0; i < counterClones.Count; i++)
            counterClones[i].Cast(Actions.DarkdragonDiveForSoakers[state.CounterTowerSoakers[i] - 1]);
    }

    // They appear as the towers go off, each on a random player; tanks take them over by crossing.
    private void TetherSouls()
    {
        if (nidhogg is { } boss) state.SoulTethers.Add((boss, world.Tether(boss, End.Passable(), TetherId.SoulTether)));
        if (counterClones.ElementAtOrDefault(state.TetherTower) is { } clone)
            state.SoulTethers.Add((clone, world.Tether(clone, End.Passable(), TetherId.SoulTether)));
    }

    private void SoulTether()
    {
        foreach (var (source, tether) in state.SoulTethers)
        {
            if (tether.B is { } holder && holder.IsAlive()) source.Cast(Actions.SoulTether, holder);
            tether.Despawn();
        }
        state.SoulTethers.Clear();
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new DsrP3NidhoggAiReplayStateMessage(
                new Dictionary<PartyRole, int>(s.Order), new Dictionary<PartyRole, JumpKind>(s.Jump), s.OrdersWithArrows.ToArray(),
                s.OutFirst.ToArray(), s.CounterTowerSoakers.ToArray(), s.TetherTower)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not DsrP3NidhoggAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = DsrP3NidhoggState.FromNetworkReplay(
            msg.Order, msg.Jump, msg.OrdersWithArrows, msg.OutFirst, msg.CounterTowerSoakers, msg.TetherTower);
        if (shadowState == null) return null;
        world = replayWorld;
        ((IScenarioAi<DsrP3NidhoggState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (LastState is not { } s || s.DiveTowers.Count == broadcastDiveTowerRounds) return null;
        broadcastDiveTowerRounds = s.DiveTowers.Count;
        return new DsrP3NidhoggDiveTowersUpdateMessage(
            s.DiveTowers.ToDictionary(e => e.Key, e => e.Value.SelectMany(t => new[] { t.X, t.Y, t.Z }).ToArray()));
    }

    public void ApplyMidRunUpdate(object shadowStateObj, MpMessage message)
    {
        if (shadowStateObj is not DsrP3NidhoggState shadowState || message is not DsrP3NidhoggDiveTowersUpdateMessage update) return;
        foreach (var (order, coords) in update.Towers)
        {
            if (order is < 1 or > 3 || coords.Length % 3 != 0 || coords.Length > 3 * DsrP3NidhoggState.OrderSize(order) || !coords.All(float.IsFinite))
                continue;
            shadowState.DiveTowers[order] = coords.Chunk(3).Select(c => new Vector3(c[0], c[1], c[2])).ToList();
        }
    }

    // A peer's tethers are its own mirrors of the host's, so its Soul Tethers are read off the world.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not DsrP3NidhoggState shadowState) return;
        shadowState.SoulTethers.Clear();
        shadowState.SoulTethers.AddRange(world.Children.OfType<SimTether>()
            .Where(t => t.IsActive && t.TetherId == TetherId.SoulTether && t.A is SimEnemy)
            .Select(t => ((SimEnemy)t.A!, t))
            .OrderBy(t => t.Item1.BNpcBaseId == BNpcBaseId.NidhoggP3 ? 0 : 1));
    }
}
