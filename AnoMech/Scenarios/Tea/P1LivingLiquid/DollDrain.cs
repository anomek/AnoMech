using System;
using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

// Where one doll falls in each of DollDrain's tables, as percentiles in [0.1, 0.9].
public readonly record struct DollRoll(float Pace, float HpBeforeLastHit, float HpAfterLastHit, float SecondsBeforeLastHit);

// The sim deals no damage, so a doll's HP falls on a schedule from its first hit, in hits at its
// taker's pace, the last taking it under 25%, where it rests until fed.
internal sealed class DollDrain
{
    private static readonly float[] Percentiles = [0.1f, 0.25f, 0.5f, 0.75f, 0.9f];

    // Seconds count from the first hit; the wait before the last hit is how long the doll shows just
    // over 25%. A slower taker leaves it closer to 25% before that hit: the rank correlation of pace
    // with hpBeforeLastHit.
    private static readonly DollDrain Melee = new(
        secondsUnderAQuarter: [4.0f, 5.5f, 6.8f, 8.5f, 9.8f],
        hpBeforeLastHit: [0.258f, 0.271f, 0.294f, 0.342f, 0.386f],
        hpAfterLastHit: [0.146f, 0.177f, 0.208f, 0.234f, 0.243f],
        secondsBeforeLastHit: [0.17f, 0.4f, 0.8f, 1.38f, 1.96f],
        secondsPerHit: 0.9f,
        paceToHpBeforeLastHit: -0.23f);
    private static readonly DollDrain PhysicalRanged = new(
        secondsUnderAQuarter: [7.6f, 8.8f, 10.4f, 12.7f, 15.2f],
        hpBeforeLastHit: [0.259f, 0.272f, 0.303f, 0.340f, 0.402f],
        hpAfterLastHit: [0.112f, 0.165f, 0.204f, 0.229f, 0.238f],
        secondsBeforeLastHit: [0.27f, 0.71f, 1.52f, 2.18f, 2.67f],
        secondsPerHit: 1.25f,
        paceToHpBeforeLastHit: -0.23f);
    private static readonly DollDrain Caster = new(
        secondsUnderAQuarter: [6.4f, 8.9f, 11.0f, 14.3f, 17.7f],
        hpBeforeLastHit: [0.264f, 0.286f, 0.332f, 0.412f, 0.484f],
        hpAfterLastHit: [0.095f, 0.136f, 0.178f, 0.222f, 0.239f],
        secondsBeforeLastHit: [0.58f, 1.56f, 2.5f, 3.96f, 7.35f],
        secondsPerHit: 2.5f,
        paceToHpBeforeLastHit: -0.53f);

    private readonly float[] secondsUnderAQuarter;
    private readonly float[] hpBeforeLastHit;
    private readonly float[] hpAfterLastHit;
    private readonly float[] secondsBeforeLastHit;
    private readonly float secondsPerHit;
    private readonly float paceToHpBeforeLastHit;

    private DollDrain(float[] secondsUnderAQuarter, float[] hpBeforeLastHit, float[] hpAfterLastHit, float[] secondsBeforeLastHit,
        float secondsPerHit, float paceToHpBeforeLastHit)
    {
        this.secondsUnderAQuarter = secondsUnderAQuarter;
        this.hpBeforeLastHit = hpBeforeLastHit;
        this.hpAfterLastHit = hpAfterLastHit;
        this.secondsBeforeLastHit = secondsBeforeLastHit;
        this.secondsPerHit = secondsPerHit;
        this.paceToHpBeforeLastHit = paceToHpBeforeLastHit;
    }

    // UNVERIFIED for a tank or healer, who seldom takes a doll: given the caster's.
    public static DollDrain Of(PartyRole taker) => taker switch
    {
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => Melee,
        PartyRole.PhysRangedDps => PhysicalRanged,
        _ => Caster,
    };

    // (seconds after the first hit, HP fraction) per hit; `jitter` varies the sizes of all but the last.
    public List<(float At, float Hp)> Hits(DollRoll roll, Rng jitter)
    {
        var seconds = At(secondsUnderAQuarter, roll.Pace);
        var before = At(hpBeforeLastHit, Correlated(roll.Pace, roll.HpBeforeLastHit, paceToHpBeforeLastHit));
        var reachesBefore = seconds - MathF.Min(At(secondsBeforeLastHit, roll.SecondsBeforeLastHit), seconds - secondsPerHit);
        var count = Math.Max(2, (int)MathF.Round(reachesBefore / secondsPerHit) + 1);
        var weights = new float[count];
        for (var i = 0; i < count; i++) weights[i] = 0.5f + jitter.NextSingle();
        var total = 0f;
        foreach (var w in weights) total += w;

        var hits = new List<(float At, float Hp)>(count + 1);
        var hp = 1f;
        for (var i = 0; i < count; i++)
        {
            hp -= (1f - before) * weights[i] / total;
            hits.Add((reachesBefore * i / (count - 1), i == count - 1 ? before : hp));
        }
        hits.Add((seconds, At(hpAfterLastHit, roll.HpAfterLastHit)));
        return hits;
    }

    // Keeps `own`'s spread while correlating it `rho` with `with`.
    private static float Correlated(float with, float own, float rho)
        => Math.Clamp(0.5f + rho * (with - 0.5f) + MathF.Sqrt(1f - rho * rho) * (own - 0.5f), Percentiles[0], Percentiles[^1]);

    private static float At(float[] table, float percentile)
    {
        var p = Math.Clamp(percentile, Percentiles[0], Percentiles[^1]);
        for (var i = 1; i < Percentiles.Length; i++)
            if (p <= Percentiles[i])
                return float.Lerp(table[i - 1], table[i], (p - Percentiles[i - 1]) / (Percentiles[i] - Percentiles[i - 1]));
        return table[^1];
    }
}
