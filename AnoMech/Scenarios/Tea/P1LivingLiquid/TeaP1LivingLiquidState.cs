using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

// Per-run randomization: each tornado T on any of the eight 45° orientations, which two tornadoes
// Pressurize and which Embolus aims where, which two tether Drainage to which tank, and six
// Throttle targets, and how fast each doll comes down. The second Hand mechanic is decided live, by
// how far apart the bosses are.
public sealed class TeaP1LivingLiquidState
{
    public TeaP1LivingLiquidScenarioObjects Objects { get; } = new();
    public TornadoT FirstTornadoes { get; }
    public TornadoT SecondTornadoes { get; }
    public int FirstCalmTornado { get; }
    public int SecondCalmTornado { get; }
    public bool FirstHandOrbFromEarlier { get; }
    public bool SecondHandOrbFromEarlier { get; }
    public int UntetheredTornado { get; }
    public bool MainTankHoldsEarlierTether { get; }
    public IReadOnlyList<PartyRole> Throttled { get; }
    // Doll k's, by TeaP1LivingLiquidState.DollBearings[k]; empty on a peer.
    public IReadOnlyList<DollRoll> DollRolls { get; }

    public static readonly float[] DollBearings = [337.5f, 247.5f, 157.5f, 67.5f];

    public TeaP1LivingLiquidState(Rng rng, TeaP1LivingLiquidStateOverrides overrides)
    {
        FirstTornadoes = new TornadoT(overrides.FirstTornadoesOpenSide ?? rng.NextInt(8) * 45);
        SecondTornadoes = new TornadoT(overrides.SecondTornadoesOpenSide ?? rng.NextInt(8) * 45);
        FirstCalmTornado = overrides.FirstCalmTornado ?? rng.NextInt(3);
        FirstHandOrbFromEarlier = overrides.FirstHandOrbFromEarlier ?? rng.NextBool();
        UntetheredTornado = overrides.UntetheredTornado ?? rng.NextInt(3);
        MainTankHoldsEarlierTether = overrides.MainTankHoldsEarlierTether ?? rng.NextBool();
        Throttled = overrides.Throttled ?? rng.Shuffle(PerRole.All).Take(6).ToList();
        SecondCalmTornado = overrides.SecondCalmTornado ?? rng.NextInt(3);
        SecondHandOrbFromEarlier = overrides.SecondHandOrbFromEarlier ?? rng.NextBool();
        DollRolls = DollBearings.Select(_ => overrides.DollRoll ?? new DollRoll(Percentile(rng), Percentile(rng), Percentile(rng), Percentile(rng))).ToList();
    }

    private static float Percentile(Rng rng) => 0.1f + 0.8f * rng.NextSingle();

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaP1LivingLiquidState(int firstOpenSide, int secondOpenSide, int firstCalm, int secondCalm, bool firstHandOrb,
        bool secondHandOrb, int untethered, bool mainTankEarlier, IReadOnlyList<PartyRole> throttled)
    {
        FirstTornadoes = new TornadoT(firstOpenSide);
        SecondTornadoes = new TornadoT(secondOpenSide);
        FirstCalmTornado = firstCalm;
        SecondCalmTornado = secondCalm;
        FirstHandOrbFromEarlier = firstHandOrb;
        SecondHandOrbFromEarlier = secondHandOrb;
        UntetheredTornado = untethered;
        MainTankHoldsEarlierTether = mainTankEarlier;
        Throttled = throttled;
        DollRolls = [];
    }

    public static TeaP1LivingLiquidState? FromNetworkReplay(int firstOpenSide, int secondOpenSide, int firstCalm, int secondCalm,
        bool firstHandOrb, bool secondHandOrb, int untethered, bool mainTankEarlier, IReadOnlyList<PartyRole> throttled)
    {
        if (!IsOpenSide(firstOpenSide) || !IsOpenSide(secondOpenSide)) return null;
        if (firstCalm is < 0 or > 2 || secondCalm is < 0 or > 2 || untethered is < 0 or > 2) return null;
        if (throttled.Count != 6 || throttled.Distinct().Count() != 6 || throttled.Any(r => !PerRole.All.Contains(r))) return null;
        return new(firstOpenSide, secondOpenSide, firstCalm, secondCalm, firstHandOrb, secondHandOrb, untethered, mainTankEarlier, throttled);
    }

    private static bool IsOpenSide(int degrees) => degrees is >= 0 and < 360 && degrees % 45 == 0;
}
