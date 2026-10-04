using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top;

// The NA conga H1 R1 M1 T1 | T2 M2 R2 H2 split into its two light parties.
public static class TopLightParties
{
    private static readonly PartyRole[] Left = [PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MainTank];

    public static bool IsLeft(PartyRole role) => Left.Contains(role);

    // P1's In Line flex: when both holders of a number share a light party, the one nearer the
    // middle of the conga swaps (H1 < R1 < M1 < T1 / T2 > M2 > R2 > H2). True = left party.
    public static Dictionary<PartyRole, bool> SplitByInLine(Func<int, IEnumerable<PartyRole>> withNumber)
    {
        var left = new Dictionary<PartyRole, bool>();
        for (var number = 1; number <= 4; number++)
        {
            var pair = withNumber(number).ToArray();
            var home = pair.Select(role => Left.Contains(role)).ToArray();
            if (home[0] != home[1])
            {
                left[pair[0]] = home[0];
                left[pair[1]] = home[1];
                continue;
            }
            var flexer = FlexPriority(pair[0]) > FlexPriority(pair[1]) ? 0 : 1;
            left[pair[flexer]] = !home[0];
            left[pair[1 - flexer]] = home[0];
        }
        return left;
    }

    // Those of the role's own light party it swaps for, outermost first.
    public static IReadOnlyList<PartyRole> FlexesFor(PartyRole role)
        => Enum.GetValues<PartyRole>()
               .Where(other => IsLeft(other) == IsLeft(role) && FlexPriority(other) < FlexPriority(role))
               .OrderBy(FlexPriority)
               .ToList();

    private static int FlexPriority(PartyRole role) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => 3,
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 2,
        PartyRole.PhysRangedDps or PartyRole.CasterDps => 1,
        _ => 0,
    };
}
