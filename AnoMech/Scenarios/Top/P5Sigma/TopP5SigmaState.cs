using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P5Sigma
{
    public sealed record Rotation(float Mul, uint LockonId)
    {
        public static readonly Rotation Clockwise = new(-1, TopConstants.LockonId.RotateCw);
        public static readonly Rotation CounterClockwise = new(1,  TopConstants.LockonId.RotateCcw);
    }

    public sealed record GlitchType(ushort StatusId, Predicate<SimTether> Condition)
    {
        public static readonly GlitchType Mid = new(TopConstants.StatusId.MidGlitch,
                                                    tether => tether.StretchGt(Geometry.MidGlitchMaxDistance) ||
                                                              tether.StretchLt(Geometry.MidGlitchMinDistance));

        public static readonly GlitchType Far = new(TopConstants.StatusId.FarGlitch,
                                                    tether => tether.StretchLt(Geometry.FarGlitchMinDistance));
    }

    public sealed class TopP5SigmaState
    {
        private readonly Rng rng = new();

        public RoleList Order { get; }
        public RoleList WaveCannonTargets { get; }
        public RoleList DynamisTargets { get; }


        public GlitchType GlitchType { get; }


        public EightWayDirection NewNorthA { get; }

        public EightWayDirection AdjustedNorthA => TowerNorthFlipped ? NewNorthA.Flip() : NewNorthA;

        public EightWayDirection NewNorthB { get; }
        public bool TowerNorthFlipped { get; }
        public Rotation SpinnerRotation { get; }
        public OmegaAttack OmegaFAttack { get; }

        public RoleList HelloWorldTargets { get; }

        public readonly Tower?[] Towers;
    
        public int FirstMissing;
        public int SecondMissing;
        
        public TopP5SigmaState(SimParty party, TopP5SigmaStateOverrides overrides)
        {
            Order = RoleList.Random(party);
            DynamisTargets = new RoleListBuilder
            {
                Size = 6,
                IncludePlayer = overrides.Dynamis,
            }.Build(party);
            WaveCannonTargets = SelectWaveCannonTargets(Order);

            NewNorthA = overrides.NewNorthA ?? rng.NextDirection();
            GlitchType = overrides.CloseFarTether ?? rng.NextObj(GlitchType.Mid, GlitchType.Far);
            TowerNorthFlipped = overrides.TowerNorthFlip ?? rng.NextBool();
            NewNorthB = overrides.NewNorthB ?? rng.NextDirection();
            SpinnerRotation = overrides.SpinnerRotation ?? rng.NextObj(Rotation.Clockwise, Rotation.CounterClockwise);
            OmegaFAttack = overrides.OmegaFForm ?? rng.NextObj(OmegaAttack.Legs, OmegaAttack.Staff);

            HelloWorldTargets = new RoleListBuilder()
            {
                Size = 2,
                ForcePlayerIndex = overrides.HelloWorld switch { HelloWorldOption.Near => [0], HelloWorldOption.Far => [1], _ => [] },
                IncludePlayer = overrides.HelloWorld switch { HelloWorldOption.No => false, _ => null }
            }.Build(party);
            
            Towers = (GlitchType == GlitchType.Mid ? MidGlitchTowers : FarGlitchTowers)
                     .Select(t => t == null ? t : t with {Position = AdjustedNorthA.Apply(t.Position) })
                     .ToArray();
        }


        // MidGlitch: 6 towers on the 22.5°-offset inner ring at radius 17.
        // Extracted from TOP_pull_05_clear.log (01:23:34.933), rotated so the two
        // adjacent SOLOs frame compass N (bossmod-canonical: relNorth → N).
        // N half holds only the two SOLOs; S half going E→W is PAIR, SOLO, SOLO, PAIR.
        // 15.706 = 17·cos 22.5°, 6.506 = 17·sin 22.5°.
        private static readonly Tower?[] MidGlitchTowers =
        {
            new(new Vector3(+15.706f, 0f, +6.506f), MinPlayers: 2), // PAIR ESE (112.5°)
            new(new Vector3(-15.706f, 0f, +6.506f), MinPlayers: 2), // PAIR WSW (247.5°)
            new(new Vector3(+6.506f, 0f, -15.706f), MinPlayers: 1), // SOLO NNE (22.5°)
            new(new Vector3(-6.506f, 0f, -15.706f), MinPlayers: 1), // SOLO NNW (337.5°)
            new(new Vector3(+6.506f, 0f, +15.706f), MinPlayers: 1), // SOLO SSE (157.5°)
            new(new Vector3(-6.506f, 0f, +15.706f), MinPlayers: 1), // SOLO SSW (202.5°)
        };

        // FarGlitch: 5 towers — derived from bossmod P5Sigma.cs (apex pair at rel N,
        // base pairs at rel SE/SW, solos at rel W/E). Rotated so the alone (apex)
        // pair-tower is at compass N. Radius 17 assumed to match MidGlitch; positions
        // on true cardinals/intercardinals. NOT verified against a log — no
        // FarGlitch Sigma pull in the available logs reached tower spawn.
        // 12.021 = 17/√2.
        private static readonly Tower?[] FarGlitchTowers =
        {
            new(new Vector3(0f, 0f, -17f), MinPlayers: 2),           // PAIR apex N (0°)
            new(new Vector3(+12.021f, 0f, +12.021f), MinPlayers: 2), // PAIR base SE (135°)
            new(new Vector3(-12.021f, 0f, +12.021f), MinPlayers: 2), // PAIR base SW (225°)
            new(new Vector3(+17f, 0f, 0f), MinPlayers: 1),           // SOLO E (90°)
            new(new Vector3(-17f, 0f, 0f), MinPlayers: 1),           // SOLO W (270°)
            null
        };

        private RoleList SelectWaveCannonTargets(RoleList tethers)
        {
            var skip1 = rng.NextInt(8);
            var skip2 = rng.NextInt(6);
            if (skip2 >= skip1 / 2 * 2) skip2 += 2;
            FirstMissing = skip1;
            SecondMissing = skip2;
            return RoleList.AllExcept(tethers.Party, tethers[skip1], tethers[skip2]);
        }
    }
}
