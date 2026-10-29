using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P2BjCc;

public enum Robot { BruteJustice, CruiseChaser }

// Per-run randomization: which half of the party Judgment Nisi marks and with which letters, every
// player's Final Judgment letter (one support and one DPS each), the first Compressed Water healer
// and Compressed Lightning DPS, the Steam Chakrams' axis, the Enumeration pair and the Ice tank.
// Water's later holders are picked live from each stack.
public sealed class TeaP2BjCcState
{
    public static readonly PartyRole[] Supports = [MainTank, OffTank, RegenHealer, ShieldHealer];
    public static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
    public const int Axes = 4;

    private readonly Rng rng = Rng.Detached;

    public TeaP2BjCcScenarioObjects Objects { get; } = new();
    public bool NisiOnDps { get; }
    public IReadOnlyDictionary<PartyRole, NisiLetter> Nisi { get; }
    public IReadOnlyDictionary<PartyRole, NisiLetter> Judgment { get; }
    public PartyRole FirstWater { get; }
    public PartyRole FirstLightning { get; }
    // 0..3 = N/S, NE/SW, E/W, SE/NW.
    public int ChakramAxis { get; }
    public IReadOnlyList<PartyRole> Enumerations { get; }
    public PartyRole IceTank { get; }
    // The bots' own dice for their reaction times, paths and steps; forked off, so it shifts no roll.
    public int BotPacing { get; }

    public IReadOnlyList<PartyRole> NisiHolders => NisiOnDps ? Dps : Supports;

    // The run's own dice, for the choices made mid-run: who a stack hands Compressed Water to.
    public T PickRandom<T>(IReadOnlyList<T> values) => values[rng.Next(values.Count)];

    public TeaP2BjCcState(Rng rng, SimParty party, TeaP2BjCcStateOverrides overrides)
    {
        this.rng = rng;
        var local = party.PlayerRole;
        var askedNisi = overrides.Nisi.Resolve(local);
        NisiOnDps = overrides.NisiOnDps ?? (askedNisi.Count > 0 ? askedNisi[0].Role.IsDps() : rng.NextBool());
        Nisi = Letters(NisiHolders, askedNisi, "nisi");
        var askedJudgment = overrides.Judgment.Resolve(local);
        var judgment = new Dictionary<PartyRole, NisiLetter>(Letters(Supports, askedJudgment, "Final Judgment"));
        foreach (var (role, letter) in Letters(Dps, askedJudgment, "Final Judgment")) judgment[role] = letter;
        Judgment = judgment;
        FirstWater = overrides.FirstWaterHealer ?? rng.NextHealerRole();
        FirstLightning = Pick(overrides.FirstLightning.Resolve(local), 1)[0];
        ChakramAxis = overrides.ChakramAxis ?? rng.NextInt(Axes);
        Enumerations = Pick(overrides.Enumeration.Resolve(local), 2);
        IceTank = overrides.IceTank ?? rng.NextTankRole();
        BotPacing = rng.Fork("tea-p2-bot-pacing").Seed;
    }

    // `count` DPS: those asking first, then the roll among the rest, those refusing last.
    private List<PartyRole> Pick(IReadOnlyList<(PartyRole Role, bool Value)> asked, int count)
    {
        var yes = asked.Where(a => a.Value && a.Role.IsDps()).Select(a => a.Role).Take(count).ToList();
        var no = asked.Where(a => !a.Value).Select(a => a.Role).ToHashSet();
        var rest = rng.Shuffle(Dps.Except(yes)).OrderBy(no.Contains);
        return yes.Concat(rest).Take(count).ToList();
    }

    // A forced letter takes its holder outright; a second seat asking for the same one keeps the roll.
    private Dictionary<PartyRole, NisiLetter> Letters(IReadOnlyList<PartyRole> holders, IReadOnlyList<(PartyRole Role, NisiLetter Value)> asked, string what)
    {
        var letters = rng.Shuffle(Enum.GetValues<NisiLetter>()).ToList();
        var byRole = holders.Select((role, i) => (role, i)).ToDictionary(x => x.role, x => letters[x.i]);
        var taken = new HashSet<NisiLetter>();
        foreach (var (role, letter) in asked)
        {
            if (!byRole.ContainsKey(role)) continue;
            if (!taken.Add(letter))
            {
                DiagnosticLog.Warn($"[TeaP2BjCc] {role} asked for {what} {letter}, already forced -- leaving {role} to the roll.");
                continue;
            }
            var holder = byRole.First(kv => kv.Value == letter).Key;
            (byRole[holder], byRole[role]) = (byRole[role], letter);
        }
        return byRole;
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaP2BjCcState(bool nisiOnDps, Dictionary<PartyRole, NisiLetter> nisi, Dictionary<PartyRole, NisiLetter> judgment, PartyRole firstWater,
        PartyRole firstLightning, int chakramAxis, IReadOnlyList<PartyRole> enumerations, PartyRole iceTank, int botPacing)
    {
        NisiOnDps = nisiOnDps;
        Nisi = nisi;
        Judgment = judgment;
        FirstWater = firstWater;
        FirstLightning = firstLightning;
        ChakramAxis = chakramAxis;
        Enumerations = enumerations;
        IceTank = iceTank;
        BotPacing = botPacing;
    }

    // nisi/judgment: a letter per role in PartyRole order, -1 for none.
    public static TeaP2BjCcState? FromNetworkReplay(bool nisiOnDps, int[] nisi, int[] judgment, PartyRole firstWater, PartyRole firstLightning,
        int chakramAxis, PartyRole[] enumerations, PartyRole iceTank, int botPacing)
    {
        if (nisi.Length != 8 || judgment.Length != 8 || chakramAxis is < 0 or >= Axes) return null;
        if (firstWater is not (RegenHealer or ShieldHealer) || !firstLightning.IsDps() || !iceTank.IsTank()) return null;
        if (enumerations.Length != 2 || enumerations.Distinct().Count() != 2 || enumerations.Any(r => !r.IsDps())) return null;
        var holders = nisiOnDps ? Dps : Supports;
        var nisiLetters = Decode(nisi, holders);
        var supportLetters = Decode(judgment, Supports);
        var dpsLetters = Decode(judgment, Dps);
        if (nisiLetters is null || supportLetters is null || dpsLetters is null) return null;
        if (PerRole.All.Except(holders).Any(r => nisi[(int)r] != -1)) return null;
        foreach (var (role, letter) in dpsLetters) supportLetters[role] = letter;
        return new(nisiOnDps, nisiLetters, supportLetters, firstWater, firstLightning, chakramAxis, enumerations, iceTank, botPacing);
    }

    // Every holder one letter, all four letters once.
    private static Dictionary<PartyRole, NisiLetter>? Decode(int[] letters, IReadOnlyList<PartyRole> holders)
    {
        var result = new Dictionary<PartyRole, NisiLetter>();
        foreach (var role in holders)
        {
            var letter = letters[(int)role];
            if (letter is < 0 or > 3) return null;
            result[role] = (NisiLetter)letter;
        }
        return result.Values.Distinct().Count() == 4 ? result : null;
    }

    public static int[] Encode(IReadOnlyDictionary<PartyRole, NisiLetter> letters)
        => PerRole.All.Select(r => letters.TryGetValue(r, out var l) ? (int)l : -1).ToArray();
}
