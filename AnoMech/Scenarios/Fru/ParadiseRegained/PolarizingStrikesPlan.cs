using System;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

// Continuation of Paradise Regained, on the same scenario clock and true north.
internal static class PolarizingStrikesPlan
{
    public const float Start = 25f;
    public const float CleanupTime = 54f;
    public const float HalfWidth = 3f;
    public const float Length = 100f;
    public static float StrikeTime(int round) => Start + 10.3f + 4.7f * round;
    public static float EchoTime(int round) => round == 3 ? Start + 26.7f : StrikeTime(round) + 2.1f;
    public static float InTime(int round) => round == 0 ? Start + 5f : StrikeTime(round) - 1.6f;
    public static float OutTime(int round) => StrikeTime(round) + 1f;
    public static float PathsCastTime(int round) => StrikeTime(round) + 1.9f;

    public static int BaitOrder(PartyRole role) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => 0,
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 1,
        PartyRole.PhysRangedDps or PartyRole.CasterDps => 2,
        _ => 3,
    };

    public static bool StartsLight(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    // Same pair rotation as PSPos: tanks, melee, ranged, healers. A pair swaps
    // sides after baiting. Use game yalms and leave margin beyond a 3y half-width.
    public static Vector3 Position(PartyRole role, int round, bool dodge)
    {
        var order = BaitOrder(role);
        var swapped = dodge ? order <= round : order < round;
        var light = StartsLight(role) != swapped;
        var sign = light ? -1f : 1f;
        if (!dodge)
        {
            var component = order == round ? 4f : 6f;
            return new(sign * component, 0f, component);
        }
        // Both parties step south of their locked diagonals; pairs cross only
        // after their own bait, while the next pair prepares at the near spot.
        var near = order == Math.Min(round + 1, 3);
        return new(sign * (near ? 0.6f : 1.8f), 0f, near ? 6.2f : 8.5f);
    }

    public static Vector3 Preposition(PartyRole role)
        => BaitOrder(role) == 0 ? new(0f, 0f, -3.5f) : Position(role, 0, false);

    public static Vector3 PlayerPosition(PartyRole role, float elapsed)
    {
        if (elapsed < InTime(0)) return Preposition(role);
        for (var round = 3; round >= 0; round--)
        {
            if (elapsed >= OutTime(round)) return Position(role, round, true);
            if (elapsed >= InTime(round)) return Position(role, round, false);
        }
        return Preposition(role);
    }
}
