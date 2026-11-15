using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

// DSR P3 from Nidhogg's arrival to the Revenge of the Horde enrage: Dive from Grace (three rounds
// of jumps, the towers they leave, and the Geirskogul lines each tower's clone aims at whoever is
// closest when it starts), two Drachenlance cleaves, and the four counted towers with the Soul
// Tether busters.
public sealed class DsrP3NidhoggScenario : IScenario
{
    public string Name => "Nidhogg";
    public IPhase Phase => DsrZone.P3;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats =>
        [new DsrP3NidhoggAi(DsrP3NidhoggAi.ArrowFacing.West), new DsrP3NidhoggAi(DsrP3NidhoggAi.ArrowFacing.East)];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly DsrP3NidhoggSettingsWindow settingsWindow = new();

    private static readonly ushort[] OrderStatus = [StatusId.FirstInLine, StatusId.SecondInLine, StatusId.ThirdInLine];
    private static readonly uint[] OrderLockon = [LockonId.FirstInLine, LockonId.SecondInLine, LockonId.ThirdInLine];

    private SimWorld world = null!;
    private DsrP3NidhoggState state = null!;
    private SimEnemy? nidhogg;
    private readonly Dictionary<PartyRole, SimEnemy> jumpClones = new();
    private readonly Dictionary<int, List<SimEnemy>> diveClones = new();
    private readonly List<SimEnemy> counterClones = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP3NidhoggState(world.Rng, settingsWindow.Overrides);
        jumpClones.Clear();
        diveClones.Clear();
        counterClones.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP3NidhoggState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.00f, PlacePlayersInCenter);
        world.Events.Add(0.50f, SpawnNidhogg);
        world.Events.Add(3.00f, () => nidhogg?.Cast(ActionId.FinalChorus));
        world.Events.Add(5.28f, Engage);
        world.Events.Add(5.32f, () => nidhogg?.Cast(Actions.FinalChorus));
        world.Events.Add(10.17f, AutoAttack);
        world.Events.Add(13.29f, AutoAttack);
        world.Events.Add(16.41f, AutoAttack);

        world.Events.Add(18.51f, () => nidhogg?.Cast(ActionId.DiveFromGrace, animationLock: 1f));
        world.Events.Add(18.51f, MarkOrders);
        world.Events.Add(24.25f, MarkJumps);
        world.Events.Add(25.64f, () => CastGnashAndLash(0));
        world.Events.Add(33.12f, () => SpawnJumpClones(1));
        world.Events.Add(33.32f, () => Jump(1));
        world.Events.Add(33.48f, () => EyeOfTheTyrant(1));
        world.Events.Add(34.32f, () => DismissJumpClones(1));
        world.Events.Add(36.94f, () => Wheel(0));
        world.Events.Add(37.42f, () => RaiseDiveTowers(1));
        world.Events.Add(39.99f, () => Wheel(1));
        world.Events.Add(42.53f, () => Geirskogul(diveClones[1]));
        world.Events.Add(43.13f, () => SpawnJumpClones(2));
        world.Events.Add(43.33f, () => Jump(2));
        world.Events.Add(44.33f, () => DismissJumpClones(2));
        world.Events.Add(47.15f, () => CastGnashAndLash(1));
        world.Events.Add(47.43f, () => RaiseDiveTowers(2));
        world.Events.Add(50.60f, () => Dismiss(diveClones[1]));
        world.Events.Add(52.54f, () => Geirskogul(diveClones[2]));
        world.Events.Add(54.11f, () => SpawnJumpClones(3));
        world.Events.Add(54.31f, () => Jump(3));
        world.Events.Add(54.99f, () => EyeOfTheTyrant(3));
        world.Events.Add(55.31f, () => DismissJumpClones(3));
        world.Events.Add(58.42f, () => RaiseDiveTowers(3));
        world.Events.Add(58.45f, () => Wheel(2));
        world.Events.Add(60.60f, () => Dismiss(diveClones[2]));
        world.Events.Add(61.48f, () => Wheel(3));
        world.Events.Add(63.54f, () => Geirskogul(diveClones[3]));
        world.Events.Add(65.70f, AutoAttack);
        world.Events.Add(68.82f, AutoAttack);

        world.Events.Add(70.07f, () => Drachenlance(atMainTank: true));
        world.Events.Add(71.60f, () => Dismiss(diveClones[3]));
        world.Events.Add(74.60f, SpawnCounterClones);
        world.Events.Add(75.10f, RaiseCounterTowers);
        world.Events.Add(79.80f, TetherSouls);
        world.Events.Add(82.72f, () => Geirskogul(counterClones.Where((_, i) => i != state.TetherTower).ToList()));
        world.Events.Add(87.16f, SoulTether);
        world.Events.Add(90.80f, () => Dismiss(counterClones));
        world.Events.Add(92.28f, AutoAttack);
        world.Events.Add(95.40f, AutoAttack);
        world.Events.Add(98.52f, AutoAttack);
        world.Events.Add(101.64f, AutoAttack);
        world.Events.Add(104.76f, AutoAttack);
        world.Events.Add(108.05f, () => Drachenlance(atMainTank: false));
        // The enrage is cast for show only: a kill ends the phase before it lands.
        world.Events.Add(113.06f, () => nidhogg?.Cast(ActionId.RevengeOfTheHorde, animationLock: 1f));
        world.Events.Add(127.00f, DespawnAll);
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
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Vector3.Zero, 0f)));
    }

    private SimEnemy? SpawnClone(Placement placement, SpawnVisibility visibility = SpawnVisibility.Visible) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: BNpcBaseId.NidhoggClone,
        NameId: BNpcNameId.Nidhogg,
        Level: Level,
        Targetable: false,
        EnemyList: EnemyListMode.Never,
        Visibility: visibility,
        Placement: placement));

    private SimCharacter? MainTank => world.Party.Get(PartyRole.MainTank);

    private void Engage()
    {
        nidhogg?.SetTargetable(true);
        nidhogg?.SetTarget(MainTank, follow: false);
    }

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
        => nidhogg?.Cast(state.OutFirst[cast] ? ActionId.GnashAndLash : ActionId.LashAndGnash, animationLock: 1f);

    // Each jump lands on its target; the tower it leaves goes where they stand, or 14y ahead of or
    // behind them along their facing for an arrow.
    private void Jump(int order)
    {
        var clones = diveClones[order] = new List<SimEnemy>();
        var towers = state.DiveTowers[order] = new List<Vector3>();
        foreach (var role in state.InOrder(order))
        {
            if (world.Party.Get(role) is not { } member) continue;
            member.RemoveStatus(OrderStatus[order - 1]);
            member.RemoveStatus(JumpStatus(state.Jump[role]));
            if (!member.IsAlive()) continue;
            var kind = state.Jump[role];
            var sign = kind switch { JumpKind.Forward => 1f, JumpKind.Backward => -1f, _ => 0f };
            var facing = new Vector3(MathF.Sin(member.Rotation), 0f, MathF.Cos(member.Rotation));
            var tower = member.Position + sign * DsrP3NidhoggState.ArrowTowerOffset * facing;
            towers.Add(tower);
            if (jumpClones.GetValueOrDefault(role) is { } jumpClone)
            {
                jumpClone.TeleportTo(new Placement(member.Position, member.Rotation));
                jumpClone.Cast(JumpAction(kind), member);
            }
            if (SpawnClone(new Placement(tower, member.Rotation), SpawnVisibility.HiddenUntilShown) is { } clone) clones.Add(clone);
        }
    }

    // A jump's clone is seen only in its jump's own animation, which shows and hides it. The client
    // drops a cast sent the frame an actor spawns, so each is spawned hidden a moment early.
    // UNVERIFIED: the animation's length.
    private void SpawnJumpClones(int order)
    {
        foreach (var role in state.InOrder(order))
            if (world.Party.Get(role) is { } member && SpawnClone(new Placement(member.Position, member.Rotation), SpawnVisibility.HiddenUntilShown) is { } clone)
                jumpClones[role] = clone;
    }

    private void DismissJumpClones(int order)
    {
        foreach (var role in state.InOrder(order))
            if (jumpClones.Remove(role, out var clone)) clone.Despawn();
    }

    // No clone is seen between a jump and its tower's dive.
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

    // Each line is aimed at whoever is closest to its clone as the cast starts.
    private void Geirskogul(IReadOnlyList<SimEnemy> clones)
    {
        foreach (var clone in clones)
        {
            if (world.Party.Find.Closest(clone.Position) is { } bait) clone.Face(bait);
            clone.Cast(Actions.Geirskogul);
        }
    }

    private static void Dismiss(List<SimEnemy> clones)
    {
        foreach (var clone in clones) clone.Despawn();
        clones.Clear();
    }

    // The first is aimed at the main tank; the second always points south.
    private void Drachenlance(bool atMainTank)
    {
        if (nidhogg is not { } boss) return;
        if (!atMainTank) boss.Face(Compass.Point(Compass.S, 1f));
        else if (MainTank is { } tank && tank.IsAlive()) boss.Face(tank);
        boss.Cast(ActionId.Drachenlance, animationLock: 1f);
        boss.CastFromHelper(Actions.DrachenlanceCleave);
    }

    // The client creates a spawned actor a few frames later and drops a cast sent before then, so
    // the clones arrive ahead of their towers, hidden until their dive's own animation shows them.
    private void SpawnCounterClones()
    {
        for (var i = 0; i < 4; i++)
            if (SpawnClone(new Placement(DsrP3NidhoggState.CounterTower(i), 0f).Face(Vector3.Zero), SpawnVisibility.HiddenUntilShown) is { } clone)
                counterClones.Add(clone);
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

    private void DespawnAll()
    {
        foreach (var (_, tether) in state.SoulTethers) tether.Despawn();
        state.SoulTethers.Clear();
        foreach (var clone in jumpClones.Values) clone.Despawn();
        jumpClones.Clear();
        foreach (var clones in diveClones.Values) Dismiss(clones);
        Dismiss(counterClones);
        nidhogg?.Despawn();
        nidhogg = null;
    }
}
