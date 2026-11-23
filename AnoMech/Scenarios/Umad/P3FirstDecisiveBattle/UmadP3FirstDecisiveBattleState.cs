using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3FirstDecisiveBattle;

public sealed class UmadP3FirstDecisiveBattleStateOverrides
{
    public int? BlasterStartSpot { get; set; }
    public bool? BlasterClockwise { get; set; }
    public int? WindCorner { get; set; }
    public bool? Mirror { get; set; }
    public bool? FireFirst { get; set; }
    public bool? FrontFirst { get; set; }
    public bool? Headwind { get; set; }
    
}

public sealed class UmadP3FirstDecisiveBattleState
{
    public int WindCorner { get; }
    public bool Mirror { get; }
    public bool FireFirst { get; }
    public bool FrontFirst { get; }
    public IReadOnlyList<PartyRole> Fire { get; }
    public IReadOnlyList<PartyRole> Water { get; }
    public IReadOnlyDictionary<PartyRole, bool> Headwinds { get; }
    public SimEnemy? Chaos { get; set; }
    public SimEnemy? Exdeath { get; set; }

    public UmadP3FirstDecisiveBattleState(Rng rng, UmadP3FirstDecisiveBattleStateOverrides settings)
    {
        WindCorner = settings.WindCorner ?? rng.NextInt(4);
        Mirror = settings.Mirror ?? rng.NextBool();
        FireFirst = settings.FireFirst ?? rng.NextBool();
        FrontFirst = settings.FrontFirst ?? rng.NextBool();
        var supports = rng.Shuffle(new[] { PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer });
        var dps = rng.Shuffle(new[] { PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps });
        Fire = [supports[0], dps[0]];
        Water = [supports[1], dps[1]];
        var headwind = settings.Headwind ?? rng.NextBool();
        Headwinds = Enum.GetValues<PartyRole>().ToDictionary(r => r, _ => headwind);
    }

    public Vector3 At(float x, float z)
    {
        if (Mirror) (x, z) = (-z, -x);
        for (var i = 0; i < WindCorner; i++) (x, z) = (-z, x);
        return new(x, 0f, z);
    }

    public float Heading(float heading)
    {
        var p = At(MathF.Sin(heading), MathF.Cos(heading));
        return MathF.Atan2(p.X, p.Z);
    }

    public Vector3 WindCrystal => At(10f, -10f);
    public Vector3 FireCrystal => At(10f, 10f);
    public Vector3 WaterCrystal => At(-10f, -10f);

    public Vector3 FixedSpot(PartyRole role, bool second)
        => role switch
        {
            PartyRole.MeleeDpsA => At(14f, -14f),
            PartyRole.MeleeDpsB when second => At(14f, -14f),
            PartyRole.PhysRangedDps => At(-18f, -8f),
            PartyRole.CasterDps => At(-6f, -18f),
            _ => At(5f, -5f),
        };

    public Vector3 ImplosionSpot(PartyRole role, bool frontSafe)
        => role switch
        {
            PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => frontSafe ? At(14f, -14f) : At(14f, -8f),
            PartyRole.PhysRangedDps => frontSafe ? At(-18f, -6f) : At(-15f, -12f),
            PartyRole.CasterDps => frontSafe ? At(-6f, -6f) : At(-6f, -18f),
            _ => frontSafe ? At(4f, -4f) : At(4f, -14f),
        };
}
