using System.Text.Json;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dsr.P3Nidhogg;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class DsrP3NidhoggStateTests
{
    private static readonly IEnumerable<int> Seeds = Enumerable.Range(0, 200);

    // A multiplayer test can leave a hosting session installed, which would make every pick a seat's.
    [SetUp]
    public void PlaySolo()
    {
        DalamudServices.Install(nameof(Plugin.Multiplayer), null!);
        PerRole.ForceSeats = false;
    }

    [TearDown]
    public void LeaveSeats() => PerRole.ForceSeats = false;

    [Test]
    public void SoloPickIsDealtWithArrowsOnAuto()
    {
        var overrides = new DsrP3NidhoggStateOverrides();
        overrides.Order.Mine = 2;
        overrides.Jump.Mine = JumpKind.Backward;
        foreach (var seed in Seeds)
        {
            var state = new DsrP3NidhoggState(new Rng(seed), RegenHealer, overrides);
            Assert.That(state.Order[RegenHealer], Is.EqualTo(2), $"seed {seed}");
            Assert.That(state.Jump[RegenHealer], Is.EqualTo(JumpKind.Backward), $"seed {seed}");
            Assert.That(state.OrdersWithArrows, Does.Contain(2), $"seed {seed}");
            ExpectValidDeal(state, seed);
        }
    }

    [Test]
    public void SoloJumpAloneStillLandsInAnArrowGroup()
    {
        var overrides = new DsrP3NidhoggStateOverrides();
        overrides.Jump.Mine = JumpKind.Forward;
        foreach (var seed in Seeds)
        {
            var state = new DsrP3NidhoggState(new Rng(seed), CasterDps, overrides);
            Assert.That(state.Jump[CasterDps], Is.EqualTo(JumpKind.Forward), $"seed {seed}");
            Assert.That(state.OrdersWithArrows, Does.Contain(state.Order[CasterDps]), $"seed {seed}");
            ExpectValidDeal(state, seed);
        }
    }

    [Test]
    public void EverySeatPickIsDealt()
    {
        PerRole.ForceSeats = true;
        var overrides = new DsrP3NidhoggStateOverrides { ArrowPattern = 0 };
        overrides.Order[MainTank] = 1;
        overrides.Jump[MainTank] = JumpKind.High;
        overrides.Order[OffTank] = 3;
        overrides.Order[MeleeDpsA] = 2;
        overrides.Jump[MeleeDpsA] = JumpKind.Forward;
        overrides.Jump[CasterDps] = JumpKind.Backward;
        Assert.That(overrides.Validate().Problems, Is.Empty);
        foreach (var seed in Seeds)
        {
            var state = new DsrP3NidhoggState(new Rng(seed), MainTank, overrides);
            Assert.That((state.Order[MainTank], state.Jump[MainTank]), Is.EqualTo((1, JumpKind.High)), $"seed {seed}");
            Assert.That(state.Order[OffTank], Is.EqualTo(3), $"seed {seed}");
            Assert.That((state.Order[MeleeDpsA], state.Jump[MeleeDpsA]), Is.EqualTo((2, JumpKind.Forward)), $"seed {seed}");
            Assert.That(state.Jump[CasterDps], Is.EqualTo(JumpKind.Backward), $"seed {seed}");
            ExpectValidDeal(state, seed);
        }
    }

    [Test]
    public void TooManySeatsOnANumberIsAConflict()
    {
        PerRole.ForceSeats = true;
        var overrides = new DsrP3NidhoggStateOverrides();
        overrides.Order[MainTank] = 2;
        overrides.Order[OffTank] = 2;
        overrides.Order[CasterDps] = 2;
        Assert.That(overrides.Validate().Problems, Has.Count.EqualTo(1));
    }

    [Test]
    public void HighJumpOnAnArrowedSecondIsAConflictAndRolledInstead()
    {
        var overrides = new DsrP3NidhoggStateOverrides { ArrowPattern = 0 };
        overrides.Order.Mine = 2;
        overrides.Jump.Mine = JumpKind.High;
        Assert.That(overrides.Validate().Problems, Has.Count.EqualTo(1));
        foreach (var seed in Seeds)
            ExpectValidDeal(new DsrP3NidhoggState(new Rng(seed), MainTank, overrides), seed);
    }

    [Test]
    public void NoPicksStillDealsTheUsualShape()
    {
        foreach (var seed in Seeds)
            ExpectValidDeal(new DsrP3NidhoggState(new Rng(seed), MainTank, new DsrP3NidhoggStateOverrides()), seed);
    }

    [Test]
    public void HostRollSurvivesTheWireAndReplaysAsDealt()
    {
        foreach (var seed in Seeds.Take(20))
        {
            var host = new DsrP3NidhoggState(new Rng(seed), MainTank, new DsrP3NidhoggStateOverrides());
            var msg = (DsrP3NidhoggAiReplayStateMessage)JsonSerializer.Deserialize<MpMessage>(JsonSerializer.Serialize<MpMessage>(ReplayMessage(host)))!;
            var replayed = DsrP3NidhoggState.FromNetworkReplay(msg.Order, msg.Jump, msg.OrdersWithArrows, msg.OutFirst, msg.CounterTowerSoakers, msg.TetherTower);
            Assert.That(replayed, Is.Not.Null, $"seed {seed}");
            Assert.That(replayed!.Order, Is.EquivalentTo(host.Order), $"seed {seed}");
            Assert.That(replayed.Jump, Is.EquivalentTo(host.Jump), $"seed {seed}");
            Assert.That(replayed.OrdersWithArrows, Is.EquivalentTo(host.OrdersWithArrows), $"seed {seed}");
            Assert.That(replayed.OutFirst, Is.EqualTo(host.OutFirst), $"seed {seed}");
            Assert.That(replayed.CounterTowerSoakers, Is.EqualTo(host.CounterTowerSoakers), $"seed {seed}");
            Assert.That(replayed.TetherTower, Is.EqualTo(host.TetherTower), $"seed {seed}");
        }
    }

    [Test]
    public void ReplayRefusesADealTheFightCantMake()
    {
        var msg = ReplayMessage(new DsrP3NidhoggState(new Rng(1), MainTank, new DsrP3NidhoggStateOverrides()));
        var jump = new Dictionary<PartyRole, JumpKind>(msg.Jump);
        foreach (var role in jump.Keys.ToList()) jump[role] = JumpKind.Forward;
        Assert.That(DsrP3NidhoggState.FromNetworkReplay(msg.Order, jump, msg.OrdersWithArrows, msg.OutFirst, msg.CounterTowerSoakers, msg.TetherTower), Is.Null);
        Assert.That(DsrP3NidhoggState.FromNetworkReplay(msg.Order, msg.Jump, [1, 2, 3], msg.OutFirst, msg.CounterTowerSoakers, msg.TetherTower), Is.Null);
        Assert.That(DsrP3NidhoggState.FromNetworkReplay(msg.Order, msg.Jump, msg.OrdersWithArrows, msg.OutFirst, [4, 4, 0, 0], msg.TetherTower), Is.Null);
    }

    private static DsrP3NidhoggAiReplayStateMessage ReplayMessage(DsrP3NidhoggState s)
        => new(new Dictionary<PartyRole, int>(s.Order), new Dictionary<PartyRole, JumpKind>(s.Jump), s.OrdersWithArrows.ToArray(),
            s.OutFirst.ToArray(), s.CounterTowerSoakers.ToArray(), s.TetherTower);

    private static void ExpectValidDeal(DsrP3NidhoggState state, int seed)
    {
        Assert.That(state.Order.Keys, Is.EquivalentTo(Enum.GetValues<PartyRole>()), $"seed {seed}");
        foreach (var order in new[] { 1, 2, 3 })
        {
            var kinds = state.InOrder(order).Select(r => state.Jump[r]).OrderBy(k => k).ToList();
            List<JumpKind> expected = !state.OrdersWithArrows.Contains(order)
                ? Enumerable.Repeat(JumpKind.High, DsrP3NidhoggState.OrderSize(order)).ToList()
                : order == 2 ? [JumpKind.Forward, JumpKind.Backward] : [JumpKind.High, JumpKind.Forward, JumpKind.Backward];
            Assert.That(kinds, Is.EqualTo(expected), $"seed {seed}, order {order}");
        }
    }
}
