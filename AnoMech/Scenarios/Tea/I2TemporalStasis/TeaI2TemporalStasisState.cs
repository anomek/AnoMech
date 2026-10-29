using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Tea.I2TemporalStasis;

public enum StasisDebuff { None, AggravatedAssault, HouseArrest, RestrainingOrder }

// Per-run randomization: which side Brute Justice comes back on (Cruise Chaser takes the other), and
// who gets each of Temporal Stasis' debuffs: one support and one DPS each, the last pair nothing.
public sealed class TeaI2TemporalStasisState
{
    public static readonly PartyRole[] Supports = [MainTank, OffTank, RegenHealer, ShieldHealer];
    public static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];

    public TeaI2TemporalStasisScenarioObjects Objects { get; } = new();
    public bool BruteJusticeEast { get; }
    public IReadOnlyDictionary<PartyRole, StasisDebuff> Debuffs { get; }

    public TeaI2TemporalStasisState(Rng rng, SimParty party, TeaI2TemporalStasisStateOverrides overrides)
    {
        var asked = overrides.Debuff.Resolve(party.PlayerRole);
        BruteJusticeEast = overrides.BruteJusticeEast ?? rng.NextBool();
        var debuffs = new Dictionary<PartyRole, StasisDebuff>();
        foreach (var half in new[] { Supports, Dps })
        foreach (var (role, debuff) in Deal(rng, half, asked))
            debuffs[role] = debuff;
        Debuffs = debuffs;
    }

    // A forced debuff takes its seat outright; a second seat of the same half asking for it keeps the roll.
    private static Dictionary<PartyRole, StasisDebuff> Deal(Rng rng, PartyRole[] half, IReadOnlyList<(PartyRole Role, StasisDebuff Value)> asked)
    {
        var dealt = rng.Shuffle(Enum.GetValues<StasisDebuff>()).ToList();
        var byRole = half.Select((role, i) => (role, i)).ToDictionary(x => x.role, x => dealt[x.i]);
        var taken = new HashSet<StasisDebuff>();
        foreach (var (role, debuff) in asked)
        {
            if (!byRole.ContainsKey(role)) continue;
            if (!taken.Add(debuff))
            {
                DiagnosticLog.Warn($"[TeaI2TemporalStasis] {role} asked for {debuff}, already forced -- leaving {role} to the roll.");
                continue;
            }
            var holder = byRole.First(kv => kv.Value == debuff).Key;
            (byRole[holder], byRole[role]) = (byRole[role], debuff);
        }
        return byRole;
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaI2TemporalStasisState(bool bruteJusticeEast, Dictionary<PartyRole, StasisDebuff> debuffs)
    {
        BruteJusticeEast = bruteJusticeEast;
        Debuffs = debuffs;
    }

    // debuffs: one per role in PartyRole order; each half deals all four once.
    public static TeaI2TemporalStasisState? FromNetworkReplay(bool bruteJusticeEast, int[] debuffs)
    {
        if (debuffs.Length != 8 || debuffs.Any(d => d is < 0 or > 3)) return null;
        var byRole = PerRole.All.ToDictionary(r => r, r => (StasisDebuff)debuffs[(int)r]);
        foreach (var half in new[] { Supports, Dps })
            if (half.Select(r => byRole[r]).Distinct().Count() != 4) return null;
        return new(bruteJusticeEast, byRole);
    }

    public static int[] Encode(IReadOnlyDictionary<PartyRole, StasisDebuff> debuffs)
        => PerRole.All.Select(r => (int)debuffs.GetValueOrDefault(r)).ToArray();
}
