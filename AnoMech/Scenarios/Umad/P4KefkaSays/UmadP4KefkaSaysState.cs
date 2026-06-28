using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P4KefkaSays;

public sealed record KefkaCast(uint DamageAction, uint OmenAction, uint Lockon)
{
    public static KefkaCast BlizzardReal = new(ActionId.BlizzardIIIBlowout_Real, 0, LockonId.ColdTrue);

    public static KefkaCast BlizzardFake = new(ActionId.BlizzardIIIBlowout_FakeAnim,
                                               ActionId.BlizzardIIIBlowout_FakeOmen, LockonId.ColdFalse);

    public static KefkaCast LightningReal = new(ActionId.ThrummingThunderIII_Real, 0, LockonId.LightningTrue);

    public static KefkaCast LightningFake = new(ActionId.ThrummingThunderIII_FakeAnim,
                                                ActionId.ThrummingThunderIII_FakeOmen, LockonId.LightningFalse);
}

public sealed record MysteryCast(KefkaCast Blizzard, KefkaCast Lightning, int BlizzardOffset, int LightningOffset, float LightningOrientation);

public sealed record ChaosCast(uint Action, uint Visual, ushort Status, float DurationFirst, float DurationSecond, uint TrueSolution, uint FakeSolution)
{
    public static ChaosCast Inferno = new(ActionId.Inferno, ActionId.Inferno_Visual, StatusId.Entropy, 60, 45, ActionId.StrayFlames_Chariot, ActionId.StrayFlames_Donut);
    public static ChaosCast Tsunami = new(ActionId.Tsunami, ActionId.Tsunami_Visual, StatusId.DynamicFluid, 84, 69, ActionId.StraySpray_Donut, ActionId.StraySpray_Chariot);
}


public sealed record Antilight(uint Action, DamageType DamageType, ushort Status, uint FloodReal, uint FloodFake)
{
   public static Antilight White = new(ActionId.WhiteAntilight, DamageType.White, StatusId.WhiteWound, ActionId.FloodOfNaught_WhiteTrue, ActionId.FloodOfNaught_BlackFake);
   public static Antilight Black = new(ActionId.BlackAntilight, DamageType.Black, StatusId.BlackWound, ActionId.FloodOfNaught_BlackTrue, ActionId.FloodOfNaught_WhiteFake);
   
   
   public Antilight Flip()
   {
       return this == White ?  Black : White;
   }
}

public sealed record MysteryAntilight(Antilight Antilight, bool DebuffTrue, bool CastTrue)
{
    public DamageType ResolvedDamageType => (DebuffTrue ? Antilight : Antilight.Flip()).DamageType;
    
    public uint ResolveFloodAction => CastTrue ? Antilight.FloodReal : Antilight.FloodFake;
}

public sealed record ChaosMystery(ChaosCast Cast, bool IsTrue)
{
    public ushort StatusValue => (ushort)(IsTrue ? 1120 : 1119);
    public uint Solution => IsTrue ? Cast.TrueSolution : Cast.FakeSolution;

    // Circle (run out of the bait) vs donut (stay inside). Inferno's real solution is
    // the Chariot, Tsunami's is the Donut, so this keys on the resolved shape directly
    // rather than on IsTrue.
    public bool SolutionIsChariot => Solution == ActionId.StrayFlames_Chariot || Solution == ActionId.StraySpray_Chariot;
}

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic for
// the duration of one play. See UmadP2ForsakenState for the canonical shape.
public sealed class UmadP4KefkaSaysState
{
    private readonly Rng rng = new();

    public IReadOnlyList<ChaosMystery> ChaosMysteries { get; }
    
    public ChaosMystery InfernoMystery { get; }
    public ChaosMystery TsunamiMystery { get; }
    
    public bool Wave1First { get; }
    public bool Wave1True { get; }

    public ushort Wave1TrueVal => (ushort)(Wave1True ? 1122 : 1121);

    public bool Wave2True { get; }

    public ushort Wave2TrueVal => (ushort)(Wave2True ? 1122 : 1121);
    public bool Wave3True { get; }
    public ushort Wave3TrueVal => (ushort)(Wave3True ? 1122 : 1121);
    public bool Wave4True { get; }

    public ushort Wave4TrueVal => (ushort)(Wave4True ? 1122 : 1121);

    public IReadOnlyList<MysteryCast> Mystery { get; }

    public uint ManaReleaseBlizzardLockon => Mystery[3].Blizzard.Lockon == Mystery[4].Blizzard.Lockon ? LockonId.ColdTrue : LockonId.ColdFalse;

    public uint ManaReleaseLightningLockon => Mystery[3].Lightning.Lockon == Mystery[4].Lightning.Lockon ? LockonId.LightningTrue : LockonId.LightningFalse;

    public Direction NeoExdeathDirection { get; }

    public RoleList Wave1 { get; }
    public RoleList Wave2 { get; }
    public RoleList Wave3 { get; }
    
    public IReadOnlyList<RoleList> ElemRoles => Wave1First ? [Wave1, Wave2] : [Wave2, Wave1];
    public IReadOnlyList<bool> ElemTrue => Wave1First ? [Wave1True, Wave2True] : [Wave2True, Wave1True];
    
    public IReadOnlyList<MysteryAntilight> Antilights;
    
    public bool[] Wounds { get; }

    public ushort BeyondDeathStatus => Wave3True ? StatusId.BeyondDeath : StatusId.AllaganField;
    public ushort AllaganFieldStatus => Wave3True ? StatusId.AllaganField : StatusId.BeyondDeath;

    public UmadP4KefkaSaysState(SimParty party, UmadP4KefkaSaysStateOverrides overrides)
    {
        // Chaos casts are controlled by position: shuffle which element casts first, then
        // apply the per-cast Real/Fake override (or randomize). InfernoMystery / TsunamiMystery
        // point back at the same instances so the later Mana Release resolution
        // (Run_Chaos_400040E2_2, which reads them by element) stays consistent with these casts.
        var chaosOrder = rng.Shuffle(ChaosCast.Inferno, ChaosCast.Tsunami);
        ChaosMysteries =
        [
            new ChaosMystery(chaosOrder[0], overrides.ChaosCast1Real ?? rng.NextBool()),
            new ChaosMystery(chaosOrder[1], overrides.ChaosCast2Real ?? rng.NextBool()),
        ];
        InfernoMystery = ChaosMysteries.First(m => m.Cast == ChaosCast.Inferno);
        TsunamiMystery = ChaosMysteries.First(m => m.Cast == ChaosCast.Tsunami);

        Wave1First = rng.NextBool();
        Wave1True = overrides.ExdeathCast1Real ?? rng.NextBool();
        Wave2True = overrides.ExdeathCast2 switch
        {
            ExdeathCast2Mode.Real => true,
            ExdeathCast2Mode.Fake => false,
            ExdeathCast2Mode.OppositeTo1 => !Wave1True,
            _ => rng.NextBool(),
        };
        Wave3True = overrides.ExdeathCast3Real ?? rng.NextBool();
        Wave4True = overrides.ExdeathCast4Real ?? rng.NextBool();

        Mystery = Enumerable.Range(0, 5)
                            .Select(i => NextMystery(i == 0 ? overrides : null))
                            .ToList(); 

        Wave1 = RoleList.RandomRoleStable(party);
        Wave2 = CalcWave2();
        Wave3 = RoleList.RandomRoleStable(party);
        Wounds = Enumerable.Range(0, 8).Select(_ => rng.NextBool()).ToArray();
        NeoExdeathDirection = rng.NextDirection();
        
        Antilights = rng.Shuffle(
            new MysteryAntilight(Antilight.White, Wave3True, Wave4True),
            new MysteryAntilight(Antilight.Black, Wave3True, Wave4True)
        );
    }

    private MysteryCast NextMystery(UmadP4KefkaSaysStateOverrides? overrides = null)
    {
        var blizzard = overrides?.FirstBlizzardReal switch
        {
            true => KefkaCast.BlizzardReal,
            false => KefkaCast.BlizzardFake,
            _ => rng.NextObj(KefkaCast.BlizzardReal, KefkaCast.BlizzardFake),
        };
        var lightning = overrides?.FirstLightningReal switch
        {
            true => KefkaCast.LightningReal,
            false => KefkaCast.LightningFake,
            _ => rng.NextObj(KefkaCast.LightningReal, KefkaCast.LightningFake),
        };
        return new MysteryCast(
            blizzard,
            lightning,
            overrides?.FirstBlizzardOffset ?? rng.NextInt(2),
            rng.NextInt(2),
            rng.NextSign()
        );
    }

    private RoleList CalcWave2()
    {
        List<PartyRole> list = [Wave1[2], Wave1[3], Wave1[0], Wave1[1], Wave1[6], Wave1[7], Wave1[4], Wave1[5]];
        for (int i = 0; i < 4; i++)
            if (rng.NextBool())
                (list[2 * i], list[2 * i + 1]) = (list[2 * i + 1], list[2 * i]);
        return new RoleList(Wave1.Party, list);
    }
}
