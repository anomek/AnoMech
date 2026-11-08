using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P3UltimateRelativity.RelativityFire;
using StatusId = AnoMech.Scenarios.Fru.FruConstants.StatusId;

namespace AnoMech.Tests;

// Whose fire is whose, as each seat shows it once the debuffs land: the time left on its Dark Fire,
// or for the one holding Dark Blizzard instead, on its Return.
public class FruP3UltimateRelativitySettingsTests
{
    private const float ShownAt = 13f;

    internal static RelativityFire? Shows(IReadOnlyDictionary<ushort, float> left)
    {
        if (left.TryGetValue(StatusId.DarkFireWaiting, out var fire))
            return Enum.GetValues<RelativityFire>().MinBy(f => MathF.Abs(fire - (FruP3UltimateRelativityState.FireAt((int)f) - ShownAt)));
        if (left.ContainsKey(StatusId.DarkBlizzardWaiting) && left.TryGetValue(StatusId.ReturnWaiting, out var back))
            return MathF.Abs(back - (FruP3UltimateRelativityState.ReturnAt(earlyReturn: true) - ShownAt))
                   < MathF.Abs(back - (FruP3UltimateRelativityState.ReturnAt(earlyReturn: false) - ShownAt)) ? Short : Long;
        return null;
    }

    private static readonly Dictionary<PartyRole, RelativityFire> Full = new()
    {
        [MainTank] = Long, [OffTank] = Short, [RegenHealer] = Long, [ShieldHealer] = Medium,
        [MeleeDpsA] = Short, [MeleeDpsB] = Long, [PhysRangedDps] = Short, [CasterDps] = Medium,
    };

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EverySeatShowsTheFireItsHostGaveIt(int seed)
    {
        var shown = Play(seed, o => { foreach (var (role, fire) in Full) o.Fire[role] = fire; });
        Assert.That(PerRole.All.ToDictionary(r => r, r => Shows(shown[r])), Is.EquivalentTo(Full.ToDictionary(f => f.Key, f => (RelativityFire?)f.Value)));
    }

    // The seats left on Auto take what the others left: still a short, a medium and two long among
    // the supports, two short, a medium and a long among the DPS.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SeatsLeftOnAutoFillTheRest(int seed)
    {
        var forced = new Dictionary<PartyRole, RelativityFire> { [ShieldHealer] = Short, [PhysRangedDps] = Long, [MeleeDpsB] = Medium };
        var shown = Play(seed, o => { foreach (var (role, fire) in forced) o.Fire[role] = fire; });
        foreach (var (role, fire) in forced)
            Assert.That(Shows(shown[role]), Is.EqualTo(fire), $"{role}");
        var supports = PerRole.All.Where(r => !r.IsDps()).Select(r => Shows(shown[r])).Order().ToList();
        var dps = PerRole.All.Where(r => r.IsDps()).Select(r => Shows(shown[r])).Order().ToList();
        Assert.That(supports, Is.EqualTo(new RelativityFire?[] { Short, Medium, Long, Long }), "supports");
        Assert.That(dps, Is.EqualTo(new RelativityFire?[] { Short, Short, Medium, Long }), "DPS");
    }

    // With Dark Blizzard on the long DPS, the DPS asking for long fire holds it instead.
    [Test]
    public void TheLongDpsHoldsDarkBlizzardWhenItIsTheirs()
    {
        var shown = Play(1, o =>
        {
            o.DpsIce = true;
            o.Fire[CasterDps] = Long;
        });
        Assert.That(shown[CasterDps].ContainsKey(StatusId.DarkBlizzardWaiting), Is.True);
        Assert.That(Shows(shown[CasterDps]), Is.EqualTo(Long));
    }

    [TestCase(OffTank, Short)]
    [TestCase(CasterDps, Long)]
    [TestCase(MeleeDpsA, Medium)]
    public void SoloThePlayerShowsTheFireTheyPicked(PartyRole player, RelativityFire fire)
    {
        var shown = Play(1, o => o.Fire.Mine = fire, player);
        Assert.That(Shows(shown[player]), Is.EqualTo(fire));
    }

    [Test]
    public void MoreSeatsOnAFireThanItsGroupHasCantHappen()
    {
        var problems = Problems(o =>
        {
            o.Fire[MainTank] = o.Fire[RegenHealer] = Medium;
            o.Fire[MeleeDpsA] = o.Fire[MeleeDpsB] = o.Fire[CasterDps] = Short;
            o.Fire[PhysRangedDps] = Long;
        });
        Assert.That(problems, Is.EqualTo(new[]
        {
            "MT, H1 all want medium fire among the supports, but only one player gets it.",
            "M1, M2, R2 all want short fire among the DPS, but only 2 players get it.",
        }));
    }

    [Test]
    public void AFullTableCanHappen()
        => Assert.That(Problems(o => { foreach (var (role, fire) in Full) o.Fire[role] = fire; }), Is.Empty);

    // The time left on each status each seat's character shows at ShownAt.
    private static Dictionary<PartyRole, IReadOnlyDictionary<ushort, float>> Play(int seed, Action<FruP3UltimateRelativityStateOverrides> set, PartyRole? solo = null)
    {
        var shown = new Dictionary<PartyRole, IReadOnlyDictionary<ushort, float>>();
        var run = ScenarioRun.Execute(typeof(FruP3UltimateRelativityScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = ShownAt + 0.1f,
            Solo = solo != null,
            PlayerRole = solo,
            Overrides = o => set((FruP3UltimateRelativityStateOverrides)o),
            Probe = probe =>
            {
                if (!probe.Crossed(ShownAt)) return;
                var characters = (FakeBattleCharas)Natives.BattleCharas;
                foreach (var role in PerRole.All)
                    if (probe.Member(role) is { } member && characters.Find(member.EntityId) is { } character)
                        shown[role] = character.Statuses.Values.GroupBy(s => s.StatusId).ToDictionary(g => g.Key, g => g.Max(s => s.RemainingTime));
            },
        });
        Assert.That(run.Failure, Is.Null);
        Assert.That(shown.Keys, Is.EquivalentTo(PerRole.All), "every seat's character");
        return shown;
    }

    // What the host's panel says can't happen.
    private static IReadOnlyList<string> Problems(Action<FruP3UltimateRelativityStateOverrides> set)
    {
        var overrides = new FruP3UltimateRelativityStateOverrides();
        set(overrides);
        PerRole.ForceSeats = true;
        try { return overrides.Validate().Problems; }
        finally { PerRole.ForceSeats = false; }
    }
}
