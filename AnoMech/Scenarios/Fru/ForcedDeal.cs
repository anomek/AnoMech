using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru;

// Per-seat picks on top of a shuffled deal, its cards indexed by PartyRole. Each forced seat in
// turn swaps its card for one that suits it, drawn from the cards no earlier forced seat holds;
// one with none left keeps the roll rather than displacing someone. Nothing forced, nothing drawn.
internal static class ForcedDeal
{
    // Returns the picks that kept the roll.
    public static List<(PartyRole Role, TPick Value)> Swap<TCard, TPick>(Rng rng, TCard[] deck,
        IReadOnlyList<(PartyRole Role, TPick Value)> forced, Func<TPick, TCard, bool> suits)
    {
        var held = new HashSet<PartyRole>();
        var kept = new List<(PartyRole, TPick)>();
        foreach (var (role, pick) in forced)
        {
            var choices = Enum.GetValues<PartyRole>().Where(r => !held.Contains(r) && suits(pick, deck[(int)r])).ToArray();
            if (choices.Length == 0)
            {
                kept.Add((role, pick));
                continue;
            }
            var chosen = (int)choices[rng.Next(choices.Length)];
            (deck[(int)role], deck[chosen]) = (deck[chosen], deck[(int)role]);
            held.Add(role);
        }
        return kept;
    }
}
