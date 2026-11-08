using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Core.Game.Party.PartyRole;
using StatusId = AnoMech.Scenarios.Fru.FruConstants.StatusId;

namespace AnoMech.Tests;

// Who is chained and who carries Dark Water, as each seat shows it once the chains are on:
// Refulgent Chain on the four chained, Dark Water's countdown on its two.
public class FruP4DarklitDragonsongSettingsTests
{
    private const float ShownAt = 22.5f;

    public sealed record Forced(Dictionary<PartyRole, bool> Chained, Dictionary<PartyRole, bool> Water)
    {
        public override string ToString()
            => string.Join(", ", Chained.Select(c => $"{c.Key} {(c.Value ? "chained" : "free")}")
                .Concat(Water.Select(w => $"{w.Key} {(w.Value ? "water" : "no water")}")));
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        Forced[] cases =
        [
            new(new() { [MainTank] = true, [ShieldHealer] = false, [MeleeDpsB] = true, [PhysRangedDps] = false },
                new() { [MainTank] = true, [ShieldHealer] = true }),
            new([], new() { [MeleeDpsA] = true, [CasterDps] = true }),
            new(new() { [OffTank] = false, [RegenHealer] = false, [MeleeDpsA] = false, [PhysRangedDps] = false },
                new() { [MainTank] = false, [CasterDps] = false }),
            new(new() { [OffTank] = true, [CasterDps] = true }, new() { [OffTank] = false, [MainTank] = true, [CasterDps] = true }),
        ];
        foreach (var forced in cases)
            foreach (var seed in new[] { 1, 2 })
                yield return new TestCaseData(forced, seed).SetArgDisplayNames(forced.ToString(), seed.ToString());
    }

    [TestCaseSource(nameof(Cases))]
    public void EverySeatShowsTheChainAndDarkWaterItsHostGaveIt(Forced forced, int seed)
    {
        var shown = Play(seed, o =>
        {
            foreach (var (role, chained) in forced.Chained) o.Chained[role] = chained;
            foreach (var (role, water) in forced.Water) o.DarkWater[role] = water;
        });
        var chained = PerRole.All.Where(r => shown[r].Contains(StatusId.RefulgentChain)).ToList();
        var waters = PerRole.All.Where(r => shown[r].Contains(StatusId.DarkWaterWaiting)).ToList();
        foreach (var (role, want) in forced.Chained)
            Assert.That(chained.Contains(role), Is.EqualTo(want), $"{role} {(want ? "chained" : "free")}");
        foreach (var (role, want) in forced.Water)
            Assert.That(waters.Contains(role), Is.EqualTo(want), $"{role} {(want ? "with" : "without")} Dark Water");
        Assert.That(chained.Count(r => r.IsTank()), Is.EqualTo(1), $"one tank chained: {string.Join(", ", chained)}");
        Assert.That(chained.Count(r => r is RegenHealer or ShieldHealer), Is.EqualTo(1), $"one healer chained: {string.Join(", ", chained)}");
        Assert.That(chained.Count, Is.EqualTo(4), $"four chained: {string.Join(", ", chained)}");
        Assert.That(waters.Count, Is.EqualTo(2), $"two waters: {string.Join(", ", waters)}");
        Assert.That(waters.Count(chained.Contains), Is.EqualTo(1), $"one water chained: {string.Join(", ", waters)}");
    }

    [TestCase(RegenHealer, false, true)]
    [TestCase(MeleeDpsA, true, true)]
    [TestCase(OffTank, true, false)]
    public void SoloThePlayerShowsTheChainAndDarkWaterTheyPicked(PartyRole player, bool chained, bool water)
    {
        var shown = Play(1, o =>
        {
            o.Chained.Mine = chained;
            o.DarkWater.Mine = water;
        }, player);
        Assert.That(shown[player].Contains(StatusId.RefulgentChain), Is.EqualTo(chained), "chained");
        Assert.That(shown[player].Contains(StatusId.DarkWaterWaiting), Is.EqualTo(water), "Dark Water");
    }

    [Test]
    public void TooManyOrTooFewOnAChainCantHappen()
    {
        var problems = Problems(o =>
        {
            o.Chained[MainTank] = o.Chained[OffTank] = true;
            o.Chained[RegenHealer] = o.Chained[ShieldHealer] = false;
            o.Chained[MeleeDpsA] = o.Chained[MeleeDpsB] = o.Chained[CasterDps] = true;
        });
        Assert.That(problems, Is.EqualTo(new[]
        {
            "MT, OT all want a tank's chain, but only one player gets it.",
            "H1, H2 all refuse a healer's chain, but someone has to take it.",
            "M1, M2, R2 all want a DPS chain, but only 2 players get it.",
        }));
    }

    [Test]
    public void TwoWatersOnTheChainsCantHappen()
    {
        var problems = Problems(o =>
        {
            o.Chained[MainTank] = o.Chained[RegenHealer] = true;
            o.DarkWater[MainTank] = o.DarkWater[RegenHealer] = true;
        });
        Assert.That(problems, Is.EqualTo(new[] { "MT, H1 all want Dark Water on a chain, but only one player gets it." }));
    }

    // Both waters end up free: each of MT and H1 is left out by the other tank's and healer's chain.
    [Test]
    public void ChainsThatLeaveBothWatersFreeCantHappen()
    {
        var problems = Problems(o =>
        {
            o.Chained[OffTank] = o.Chained[ShieldHealer] = true;
            o.DarkWater[MainTank] = o.DarkWater[RegenHealer] = true;
        });
        Assert.That(problems, Is.EqualTo(new[] { "MT, OT, H1, H2 can't all get the chain and Dark Water they asked for." }));
    }

    [Test]
    public void AFullDealCanHappen()
        => Assert.That(Problems(o =>
        {
            foreach (var role in PerRole.All) o.Chained[role] = role is MainTank or ShieldHealer or MeleeDpsB or CasterDps;
            foreach (var role in PerRole.All) o.DarkWater[role] = role is ShieldHealer or PhysRangedDps;
        }), Is.Empty);

    // The statuses each seat's character shows at ShownAt.
    private static Dictionary<PartyRole, IReadOnlySet<ushort>> Play(int seed, Action<FruP4DarklitDragonsongStateOverrides> set, PartyRole? solo = null)
    {
        var shown = new Dictionary<PartyRole, IReadOnlySet<ushort>>();
        var run = ScenarioRun.Execute(typeof(FruP4DarklitDragonsongScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = ShownAt + 0.1f,
            Solo = solo != null,
            PlayerRole = solo,
            Overrides = o => set((FruP4DarklitDragonsongStateOverrides)o),
            Probe = probe =>
            {
                if (!probe.Crossed(ShownAt)) return;
                var characters = (FakeBattleCharas)Natives.BattleCharas;
                foreach (var role in PerRole.All)
                    if (probe.Member(role) is { } member && characters.Find(member.EntityId) is { } character)
                        shown[role] = character.Statuses.Keys.Select(k => k.StatusId).ToHashSet();
            },
        });
        Assert.That(run.Failure, Is.Null);
        Assert.That(shown.Keys, Is.EquivalentTo(PerRole.All), "every seat's character");
        return shown;
    }

    // What the host's panel says can't happen.
    private static IReadOnlyList<string> Problems(Action<FruP4DarklitDragonsongStateOverrides> set)
    {
        var overrides = new FruP4DarklitDragonsongStateOverrides();
        set(overrides);
        PerRole.ForceSeats = true;
        try { return overrides.Validate().Problems; }
        finally { PerRole.ForceSeats = false; }
    }
}
