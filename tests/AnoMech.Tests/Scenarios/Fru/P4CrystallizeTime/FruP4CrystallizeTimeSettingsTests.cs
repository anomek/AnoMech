using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P4CrystallizeTime.CrystallizeDebuffChoice;
using StatusId = AnoMech.Scenarios.Fru.FruConstants.StatusId;

namespace AnoMech.Tests;

// Whose debuffs are whose: what each seat shows a second after they land, a red one Wyrmclaw and
// its spell, a blue one Wyrmfang and its spell.
public class FruP4CrystallizeTimeSettingsTests
{
    private const float ShownAt = 13f;

    private static readonly ushort[] Spells =
    [
        StatusId.DarkBlizzardWaiting, StatusId.DarkAeroWaiting, StatusId.UnholyDarknessWaiting,
        StatusId.DarkWaterWaiting, StatusId.DarkEruptionWaiting,
    ];

    private static (ushort Colour, ushort[] Spells) Shows(CrystallizeDebuffChoice choice) => choice switch
    {
        RedIce => (StatusId.Wyrmclaw, [StatusId.DarkBlizzardWaiting]),
        RedAero => (StatusId.Wyrmclaw, [StatusId.DarkAeroWaiting]),
        BlueDarkness => (StatusId.Wyrmfang, [StatusId.UnholyDarknessWaiting]),
        _ => (StatusId.Wyrmfang, [StatusId.DarkBlizzardWaiting, StatusId.DarkWaterWaiting, StatusId.DarkEruptionWaiting]),
    };

    private static string? Wrong(IReadOnlySet<ushort> shown, CrystallizeDebuffChoice choice)
    {
        var (colour, spells) = Shows(choice);
        var other = colour == StatusId.Wyrmclaw ? StatusId.Wyrmfang : StatusId.Wyrmclaw;
        var held = Spells.Where(shown.Contains).ToList();
        return shown.Contains(colour) && !shown.Contains(other) && held.Count == 1 && spells.Contains(held[0])
            ? null
            : $"shows {string.Join(", ", shown.Select(id => Natives.Data.StatusName(id)))}";
    }

    // A full table: two of each red, Darkness and the three other blues.
    private static readonly Dictionary<PartyRole, CrystallizeDebuffChoice> Full = new()
    {
        [MainTank] = RedIce, [OffTank] = BlueDarkness, [RegenHealer] = BlueIceWaterEruption, [ShieldHealer] = RedAero,
        [MeleeDpsA] = BlueIceWaterEruption, [MeleeDpsB] = RedAero, [PhysRangedDps] = RedIce, [CasterDps] = BlueIceWaterEruption,
    };

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EverySeatShowsTheDebuffsItsHostGaveIt(int seed)
    {
        var shown = Play(seed, o => { foreach (var (role, choice) in Full) o.PlayerDebuffs[role] = choice; });
        var problems = Full.Select(f => (f.Key, Problem: Wrong(shown[f.Key], f.Value))).Where(p => p.Problem != null)
            .Select(p => $"{p.Key} asked for {FruP4CrystallizeTimeStateOverrides.Label(Full[p.Key])}, {p.Problem}").ToList();
        Assert.That(problems, Is.Empty);
    }

    // The seats left on Random take what the others left, so the party still holds the fight's own
    // eight.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SeatsLeftOnRandomFillTheRest(int seed)
    {
        var forced = new Dictionary<PartyRole, CrystallizeDebuffChoice> { [MeleeDpsA] = BlueDarkness, [CasterDps] = RedIce, [MainTank] = RedIce };
        var shown = Play(seed, o => { foreach (var (role, choice) in forced) o.PlayerDebuffs[role] = choice; });
        foreach (var (role, choice) in forced)
            Assert.That(Wrong(shown[role], choice), Is.Null, $"{role} asked for {FruP4CrystallizeTimeStateOverrides.Label(choice)}");
        var spells = shown.Values.SelectMany(s => s.Where(Spells.Contains)).Order().ToList();
        ushort[] dealt =
        [
            StatusId.UnholyDarknessWaiting, StatusId.DarkEruptionWaiting, StatusId.DarkWaterWaiting,
            StatusId.DarkBlizzardWaiting, StatusId.DarkBlizzardWaiting, StatusId.DarkBlizzardWaiting,
            StatusId.DarkAeroWaiting, StatusId.DarkAeroWaiting,
        ];
        Assert.That(spells, Is.EqualTo(dealt.Order().ToList()));
        Assert.That(shown.Values.Count(s => s.Contains(StatusId.Wyrmclaw)), Is.EqualTo(4));
        Assert.That(shown.Values.Count(s => s.Contains(StatusId.Wyrmfang)), Is.EqualTo(4));
    }

    [TestCase(RegenHealer, BlueDarkness)]
    [TestCase(MeleeDpsB, RedAero)]
    [TestCase(OffTank, BlueIceWaterEruption)]
    public void SoloThePlayerShowsTheDebuffsTheyPicked(PartyRole player, CrystallizeDebuffChoice choice)
    {
        var shown = Play(1, o => o.PlayerDebuffs.Mine = choice, player);
        Assert.That(Wrong(shown[player], choice), Is.Null);
    }

    [Test]
    public void MoreSeatsOnAPickThanItHasCantHappen()
    {
        var problems = Problems(o =>
        {
            foreach (var role in new[] { MainTank, OffTank, MeleeDpsA }) o.PlayerDebuffs[role] = RedAero;
            foreach (var role in new[] { RegenHealer, ShieldHealer }) o.PlayerDebuffs[role] = BlueDarkness;
        });
        Assert.That(problems, Is.EqualTo(new[]
        {
            "MT, OT, M1 all want Red + Aero, but only 2 players get it.",
            "H1, H2 all want Blue + Darkness, but only one player gets it.",
        }));
    }

    [Test]
    public void AFullTableCanHappen()
        => Assert.That(Problems(o => { foreach (var (role, choice) in Full) o.PlayerDebuffs[role] = choice; }), Is.Empty);

    // The statuses each seat's character shows at ShownAt.
    private static Dictionary<PartyRole, IReadOnlySet<ushort>> Play(int seed, Action<FruP4CrystallizeTimeStateOverrides> set, PartyRole? solo = null)
    {
        var shown = new Dictionary<PartyRole, IReadOnlySet<ushort>>();
        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = ShownAt + 0.1f,
            Solo = solo != null,
            PlayerRole = solo,
            Overrides = o => set((FruP4CrystallizeTimeStateOverrides)o),
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
    private static IReadOnlyList<string> Problems(Action<FruP4CrystallizeTimeStateOverrides> set)
    {
        var overrides = new FruP4CrystallizeTimeStateOverrides();
        set(overrides);
        PerRole.ForceSeats = true;
        try { return overrides.Validate().Problems; }
        finally { PerRole.ForceSeats = false; }
    }
}
