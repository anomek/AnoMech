using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

public enum FinalWordDebuff { LightBeacon, LightFollower, DarkBeacon, DarkFollower }

public enum AlphaSentence { SharedSentence, Defamation, AggravatedAssault, Nothing }

// Also the projection's place in Fate Calibration β's id order, which decides its part.
public enum BetaRole { LightFar, DarkFar, LightNear, DarkNear, LightShared, DarkUntethered, LightBeacon, DarkBeacon }

// Per-run randomization: who carries each Final Word debuff, the Ordained and Optical Sight orders
// and the stack targets, each Fate Projection's clone order (which decides everyone's part), Fate
// Calibration α's two requirements and safe clone, β's Optical Sight and donut clone, both
// Almighty Judgments' tile orders, the Irresistible Grace targets and the bots' pacing.
public sealed class TeaP4PerfectAlexanderState
{
    // The fight's own times, from scenario start (12s before The Final Word).
    public const float TargetableAt = 1.76f;
    public const float TemporalPrisonCast = 315.06f;
    // UNVERIFIED: the first jail lands 10s into Temporal Prison, the rest 5s apart.
    public const float FirstJail = 10f;
    public const float JailEvery = 5f;
    private const float FinalWordAt = 12f;

    // Perfect Alexander's death in the slowest real kill, 341.8s after The Final Word's cast start.
    public const float KillAt = FinalWordAt + 341.8f;

    private readonly Rng rng = Rng.Detached;

    public TeaP4PerfectAlexanderScenarioObjects Objects { get; } = new();
    public P4Practice Practice { get; }
    public PartyRole LightBeacon { get; }
    public PartyRole DarkBeacon { get; }
    public IReadOnlyList<PartyRole> LightFollowers { get; }
    public IReadOnlyList<PartyRole> DarkFollowers { get; }
    // The second Ordained cast is always the other one.
    public bool FinalWordMotionFirst { get; }
    public bool OpticalSpreadFirst { get; }
    public IReadOnlyList<PartyRole> OpticalStackTargets { get; }
    // By projection id, lowest first: the last shares the sentence, the one before takes the
    // defamation, the three before that the aggravated assaults.
    public IReadOnlyList<PartyRole> AlphaClones { get; }
    public bool AlphaFirstMotion { get; }
    public bool AlphaSecondMotion { get; }
    // 1 or 2: the second or third Perfect Alexander clone from the west.
    public int AlphaSafeClone { get; }
    // By projection id: which of the real paths it walks.
    public IReadOnlyList<int> AlphaPathOrder { get; }
    // By BetaRole.
    public IReadOnlyList<PartyRole> BetaClones { get; }
    public bool BetaOpticalStack { get; }
    // 1 or 2: the second or third clone from the west holds Optical Sight; the second mirrors β east for west.
    public int BetaOpticalClone { get; }
    // The jumping clone that shows the donut: 0 (the east bait's), 3 (the west bait's) or the middle one (the south bait's).
    public int BetaDonutClone { get; }
    // Each Almighty Judgment's tile sets in the order they fall (0 = the centre's, 1 = waymark 4's,
    // 2 = waymark 3's).
    public IReadOnlyList<IReadOnlyList<int>> JudgmentOrders { get; }
    public IReadOnlyList<PartyRole> GraceTargets { get; }
    // The bots' own dice for their pacing, carried to peers so their bots pace a seat as the host's do.
    public int BotSeed { get; }

    // The run's own dice, for the choices made mid-run: where a spread with nobody left to take it goes.
    public T PickRandom<T>(IReadOnlyList<T> values) => values[rng.Next(values.Count)];

    public AlphaSentence AlphaSentenceOf(PartyRole role) => AlphaSentenceOfSlot(AlphaClones.ToList().IndexOf(role));

    public static AlphaSentence AlphaSentenceOfSlot(int slot) => slot switch
    {
        7 => AlphaSentence.SharedSentence,
        6 => AlphaSentence.Defamation,
        >= 3 => AlphaSentence.AggravatedAssault,
        _ => AlphaSentence.Nothing,
    };

    public BetaRole BetaRoleOf(PartyRole role) => (BetaRole)BetaClones.ToList().IndexOf(role);

    public PartyRole BetaHolder(BetaRole role) => BetaClones[(int)role];

    public FinalWordDebuff FinalWordOf(PartyRole role)
        => role == LightBeacon ? FinalWordDebuff.LightBeacon
            : role == DarkBeacon ? FinalWordDebuff.DarkBeacon
            : LightFollowers.Contains(role) ? FinalWordDebuff.LightFollower
            : FinalWordDebuff.DarkFollower;

    public TeaP4PerfectAlexanderState(Rng rng, SimParty party, TeaP4PerfectAlexanderStateOverrides overrides)
    {
        this.rng = rng;
        var local = party.PlayerRole;
        Practice = overrides.Practice;
        var finalWord = Deal<FinalWordDebuff>(rng, [FinalWordDebuff.LightBeacon, FinalWordDebuff.DarkBeacon, .. Copies(FinalWordDebuff.LightFollower, 3),
            .. Copies(FinalWordDebuff.DarkFollower, 3)], overrides.FinalWord.Resolve(local), "Final Word debuff");
        LightBeacon = finalWord.Single(kv => kv.Value == FinalWordDebuff.LightBeacon).Key;
        DarkBeacon = finalWord.Single(kv => kv.Value == FinalWordDebuff.DarkBeacon).Key;
        LightFollowers = PerRole.All.Where(r => finalWord[r] == FinalWordDebuff.LightFollower).ToList();
        DarkFollowers = PerRole.All.Where(r => finalWord[r] == FinalWordDebuff.DarkFollower).ToList();
        FinalWordMotionFirst = overrides.FinalWordMotionFirst ?? rng.NextBool();
        OpticalSpreadFirst = overrides.OpticalSpreadFirst ?? rng.NextBool();
        OpticalStackTargets = overrides.OpticalStackTargets ?? Pick(rng, overrides.OpticalStack.Resolve(local), 2);

        var alpha = Deal<AlphaSentence>(rng, [AlphaSentence.SharedSentence, AlphaSentence.Defamation, .. Copies(AlphaSentence.AggravatedAssault, 3),
            .. Copies(AlphaSentence.Nothing, 3)], overrides.Alpha.Resolve(local), "Fate Calibration α sentence");
        AlphaClones = Slots(rng, alpha, sentence => sentence switch
        {
            AlphaSentence.SharedSentence => [7],
            AlphaSentence.Defamation => [6],
            AlphaSentence.AggravatedAssault => [3, 4, 5],
            _ => [0, 1, 2],
        });
        AlphaFirstMotion = overrides.AlphaFirstMotion ?? rng.NextBool();
        AlphaSecondMotion = overrides.AlphaSecondMotion ?? rng.NextBool();
        AlphaSafeClone = overrides.AlphaSafeClone ?? 1 + rng.NextInt(2);

        var beta = Deal<BetaRole>(rng, Enum.GetValues<BetaRole>(), overrides.Beta.Resolve(local), "Fate Calibration β part");
        BetaClones = Slots(rng, beta, role => [(int)role]);
        BetaOpticalStack = overrides.BetaOpticalStack ?? rng.NextBool();
        var donutBait = overrides.BetaDonutClone ?? rng.NextObj(0, 1, 3);

        JudgmentOrders = [overrides.JudgmentOrders[0] ?? rng.Shuffle(0, 1, 2), overrides.JudgmentOrders[1] ?? rng.Shuffle(0, 1, 2)];
        GraceTargets = overrides.GraceTargets ?? [rng.NextRole(), rng.NextRole()];
        // The second and the third about equally in real pulls.
        BetaOpticalClone = overrides.BetaOpticalClone ?? 1 + rng.NextInt(2);
        BetaDonutClone = donutBait == 1 ? 3 - BetaOpticalClone : donutBait;
        AlphaPathOrder = rng.Shuffle(Enumerable.Range(0, 8));
        // Last, so the rolls before it stay what each seed gave.
        BotSeed = overrides.BotSeed ?? rng.Next(int.MaxValue);
    }

    private static IEnumerable<T> Copies<T>(T value, int count) => Enumerable.Repeat(value, count);

    // Every seat one card: those asking for one take it while it lasts, the rest are dealt the
    // shuffled remainder.
    private static Dictionary<PartyRole, T> Deal<T>(Rng rng, IReadOnlyList<T> cards, IReadOnlyList<(PartyRole Role, T Value)> asked, string what)
        where T : struct
    {
        var deck = rng.Shuffle(cards).ToList();
        var dealt = new Dictionary<PartyRole, T>();
        foreach (var (role, value) in asked)
        {
            var index = deck.FindIndex(card => EqualityComparer<T>.Default.Equals(card, value));
            if (index < 0)
            {
                DiagnosticLog.Warn($"[TeaP4PerfectAlexander] {role} asked for {what} {value}, already taken -- leaving {role} to the roll.");
                continue;
            }
            dealt[role] = deck[index];
            deck.RemoveAt(index);
        }
        foreach (var role in PerRole.All.Where(r => !dealt.ContainsKey(r)))
        {
            dealt[role] = deck[0];
            deck.RemoveAt(0);
        }
        return dealt;
    }

    // The clone order: each card's places, filled in a random order.
    private static PartyRole[] Slots<T>(Rng rng, Dictionary<PartyRole, T> dealt, Func<T, int[]> places)
    {
        var slots = new PartyRole[8];
        foreach (var group in dealt.GroupBy(kv => kv.Value))
        {
            var spots = places(group.Key);
            var holders = rng.Shuffle(group.Select(kv => kv.Key)).ToList();
            for (var k = 0; k < holders.Count; k++) slots[spots[k]] = holders[k];
        }
        return slots;
    }

    // `count` seats: those asking first, then the roll among the rest, those refusing last.
    private static List<PartyRole> Pick(Rng rng, IReadOnlyList<(PartyRole Role, bool Value)> asked, int count)
    {
        var yes = asked.Where(a => a.Value).Select(a => a.Role).Take(count).ToList();
        var no = asked.Where(a => !a.Value).Select(a => a.Role).ToHashSet();
        var rest = rng.Shuffle(PerRole.All.Except(yes)).OrderBy(no.Contains);
        return yes.Concat(rest).Take(count).ToList();
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaP4PerfectAlexanderState(P4Practice practice, PartyRole lightBeacon, PartyRole darkBeacon, PartyRole[] lightFollowers,
        bool finalWordMotionFirst, bool opticalSpreadFirst, PartyRole[] opticalStackTargets, PartyRole[] alphaClones, bool alphaFirstMotion,
        bool alphaSecondMotion, int alphaSafeClone, PartyRole[] betaClones, bool betaOpticalStack, int betaOpticalClone, int betaDonutClone,
        int[][] judgmentOrders, PartyRole[] graceTargets, int botSeed)
    {
        Practice = practice;
        LightBeacon = lightBeacon;
        DarkBeacon = darkBeacon;
        LightFollowers = lightFollowers;
        DarkFollowers = PerRole.All.Except(lightFollowers).Where(r => r != lightBeacon && r != darkBeacon).ToList();
        FinalWordMotionFirst = finalWordMotionFirst;
        OpticalSpreadFirst = opticalSpreadFirst;
        OpticalStackTargets = opticalStackTargets;
        AlphaClones = alphaClones;
        AlphaFirstMotion = alphaFirstMotion;
        AlphaSecondMotion = alphaSecondMotion;
        AlphaSafeClone = alphaSafeClone;
        // A peer moves no projections.
        AlphaPathOrder = Enumerable.Range(0, 8).ToArray();
        BetaClones = betaClones;
        BetaOpticalStack = betaOpticalStack;
        BetaOpticalClone = betaOpticalClone;
        BetaDonutClone = betaDonutClone;
        JudgmentOrders = judgmentOrders;
        GraceTargets = graceTargets;
        BotSeed = botSeed;
    }

    public static TeaP4PerfectAlexanderState? FromNetworkReplay(P4Practice practice, PartyRole lightBeacon, PartyRole darkBeacon,
        PartyRole[]? lightFollowers, bool finalWordMotionFirst, bool opticalSpreadFirst, PartyRole[]? opticalStackTargets, PartyRole[]? alphaClones,
        bool alphaFirstMotion, bool alphaSecondMotion, int alphaSafeClone, PartyRole[]? betaClones, bool betaOpticalStack, int betaOpticalClone,
        int betaDonutClone, int[][]? judgmentOrders, PartyRole[]? graceTargets, int botSeed)
    {
        if (!Enum.IsDefined(practice)) return null;
        if (!Enum.IsDefined(lightBeacon) || !Enum.IsDefined(darkBeacon) || lightBeacon == darkBeacon) return null;
        if (lightFollowers is not { Length: 3 } || lightFollowers.Distinct().Count() != 3 || !lightFollowers.All(Enum.IsDefined)
            || lightFollowers.Contains(lightBeacon) || lightFollowers.Contains(darkBeacon)) return null;
        if (opticalStackTargets is not { Length: 2 } || opticalStackTargets.Distinct().Count() != 2 || !opticalStackTargets.All(Enum.IsDefined)) return null;
        if (!IsPermutation(alphaClones) || !IsPermutation(betaClones)) return null;
        if (alphaSafeClone is not (1 or 2) || betaOpticalClone is not (1 or 2)) return null;
        if (betaDonutClone is not (0 or 3) && betaDonutClone != 3 - betaOpticalClone) return null;
        if (judgmentOrders is not { Length: 2 } || judgmentOrders.Any(o => o is not { Length: 3 } || !o.OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2 }))) return null;
        if (graceTargets is not { Length: 2 } || !graceTargets.All(Enum.IsDefined)) return null;
        return new(practice, lightBeacon, darkBeacon, lightFollowers, finalWordMotionFirst, opticalSpreadFirst, opticalStackTargets, alphaClones!,
            alphaFirstMotion, alphaSecondMotion, alphaSafeClone, betaClones!, betaOpticalStack, betaOpticalClone, betaDonutClone, judgmentOrders,
            graceTargets, botSeed);
    }

    private static bool IsPermutation(PartyRole[]? roles)
        => roles is { Length: 8 } && roles.All(Enum.IsDefined) && roles.Distinct().Count() == 8;
}
