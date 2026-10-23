using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

// Per-run randomization: the numbers a plain permutation, the first Hawk Blaster pair on any of
// the four axes. Every later pair turns 45° clockwise from it.
public sealed class TeaI1LimitCutState
{
    public const int Axes = 4;

    private readonly Rng rng = Rng.Detached;

    public TeaI1LimitCutScenarioObjects Objects { get; } = new();
    // Numbers[k] holds number k+1.
    public IReadOnlyList<PartyRole> Numbers { get; }
    // 0..3 = N/S, NE/SW, E/W, SE/NW.
    public int FirstBlasterAxis { get; }

    public int NumberOf(PartyRole role)
    {
        for (var k = 0; k < Numbers.Count; k++)
            if (Numbers[k] == role) return k + 1;
        return 0;
    }

    // The run's own dice, for the one choice made mid-run: who a dead number's hit goes to.
    public T PickRandom<T>(T[] values) => rng.NextObj(values);

    public TeaI1LimitCutState(Rng rng, SimParty party, TeaI1LimitCutStateOverrides overrides)
    {
        this.rng = rng;
        FirstBlasterAxis = overrides.FirstBlasterAxis ?? rng.NextInt(Axes);
        Numbers = BuildNumbers(party.PlayerRole, overrides);
    }

    // A forced number takes its slot outright; a second seat asking for the same one keeps the
    // fight's own roll, since the eight numbers are a permutation.
    private List<PartyRole> BuildNumbers(PartyRole localPlayerRole, TeaI1LimitCutStateOverrides overrides)
    {
        var numbers = rng.Shuffle(PerRole.All).ToList();
        var taken = new bool[8];
        foreach (var (role, wanted) in overrides.Number.Resolve(localPlayerRole))
        {
            if (wanted is < 1 or > 8) continue;
            var index = wanted - 1;
            if (taken[index])
            {
                DiagnosticLog.Warn($"[TeaI1LimitCut] {role} asked for number {wanted}, already forced for {numbers[index]} -- leaving {role} to the roll.");
                continue;
            }
            var current = numbers.IndexOf(role);
            if (current != index) (numbers[current], numbers[index]) = (numbers[index], numbers[current]);
            taken[index] = true;
        }
        return numbers;
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaI1LimitCutState(IReadOnlyList<PartyRole> numbers, int firstBlasterAxis)
    {
        Numbers = numbers;
        FirstBlasterAxis = firstBlasterAxis;
    }

    public static TeaI1LimitCutState? FromNetworkReplay(IReadOnlyList<PartyRole> numbers, int firstBlasterAxis)
    {
        if (firstBlasterAxis is < 0 or >= Axes) return null;
        if (numbers.Count != 8 || numbers.Distinct().Count() != 8 || numbers.Any(r => !PerRole.All.Contains(r))) return null;
        return new(numbers, firstBlasterAxis);
    }
}
