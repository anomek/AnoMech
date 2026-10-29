using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P3Apocalypse.ApocalypseWater;
using StatusId = AnoMech.Scenarios.Fru.FruConstants.StatusId;

namespace AnoMech.Tests;

// Whose Dark Water is whose, as each seat shows it once the waters land: the time left on it, or
// none at all.
public class FruP3ApocalypseSettingsTests
{
    private const float ShownAt = 13.5f;

    internal static ApocalypseWater Shows(IReadOnlyDictionary<ushort, float> left)
        => left.TryGetValue(StatusId.DarkWaterWaiting, out var water)
            ? new[] { Short, Medium, Long }.MinBy(w => MathF.Abs(water - (FruP3ApocalypseState.WaterAt((int)w) - ShownAt)))
            : None;

    private static readonly Dictionary<PartyRole, ApocalypseWater> Full = new()
    {
        [MainTank] = Short, [OffTank] = None, [RegenHealer] = Long, [ShieldHealer] = Medium,
        [MeleeDpsA] = Medium, [MeleeDpsB] = Short, [PhysRangedDps] = None, [CasterDps] = Long,
    };

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EverySeatShowsTheDarkWaterItsHostGaveIt(int seed)
    {
        var shown = Play(seed, o => { foreach (var (role, water) in Full) o.DarkWater[role] = water; });
        Assert.That(PerRole.All.ToDictionary(r => r, r => Shows(shown[r])), Is.EquivalentTo(Full));
    }

    // The seats left on Auto take what the others left: still two of each.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SeatsLeftOnAutoFillTheRest(int seed)
    {
        var forced = new Dictionary<PartyRole, ApocalypseWater> { [CasterDps] = Short, [MainTank] = Short, [RegenHealer] = None };
        var shown = Play(seed, o => { foreach (var (role, water) in forced) o.DarkWater[role] = water; });
        foreach (var (role, water) in forced)
            Assert.That(Shows(shown[role]), Is.EqualTo(water), $"{role}");
        Assert.That(PerRole.All.Select(r => Shows(shown[r])).Order(), Is.EqualTo(new[] { None, None, Short, Short, Medium, Medium, Long, Long }));
    }

    [TestCase(ShieldHealer, Short)]
    [TestCase(MeleeDpsA, Long)]
    [TestCase(OffTank, Medium)]
    public void SoloThePlayerShowsTheDarkWaterTheyPicked(PartyRole player, ApocalypseWater water)
    {
        var shown = Play(1, o => o.DarkWater.Mine = water, player);
        Assert.That(Shows(shown[player]), Is.EqualTo(water));
    }

    [Test]
    public void MoreSeatsOnADarkWaterThanItHasCantHappen()
    {
        var problems = Problems(o =>
        {
            o.DarkWater[MainTank] = o.DarkWater[OffTank] = o.DarkWater[CasterDps] = Medium;
            o.DarkWater[RegenHealer] = o.DarkWater[ShieldHealer] = o.DarkWater[MeleeDpsA] = None;
        });
        Assert.That(problems, Is.EqualTo(new[]
        {
            "H1, H2, M1 all want no Dark Water, but only 2 players get it.",
            "MT, OT, R2 all want a medium Dark Water, but only 2 players get it.",
        }));
    }

    [Test]
    public void AFullTableCanHappen()
        => Assert.That(Problems(o => { foreach (var (role, water) in Full) o.DarkWater[role] = water; }), Is.Empty);

    // The time left on each status each seat's character shows at ShownAt.
    private static Dictionary<PartyRole, IReadOnlyDictionary<ushort, float>> Play(int seed, Action<FruP3ApocalypseStateOverrides> set, PartyRole? solo = null)
    {
        var shown = new Dictionary<PartyRole, IReadOnlyDictionary<ushort, float>>();
        var run = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = ShownAt + 0.1f,
            Solo = solo != null,
            PlayerRole = solo,
            Overrides = o => set((FruP3ApocalypseStateOverrides)o),
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
    private static IReadOnlyList<string> Problems(Action<FruP3ApocalypseStateOverrides> set)
    {
        var overrides = new FruP3ApocalypseStateOverrides();
        set(overrides);
        PerRole.ForceSeats = true;
        try { return overrides.Validate().Problems; }
        finally { PerRole.ForceSeats = false; }
    }
}
