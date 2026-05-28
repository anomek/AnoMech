using System;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyState
{
    private readonly Rng rng = new();
    
    public Direction NewNorthA { get; }
    public Direction NewNorthB { get; }
    public Direction AttackDir { get; }
    public RoleList Order { get; }
    public RoleList Stacks { get; }
    public GlitchType Glitch { get; }
    public OmegaAttack AttackM { get; }
    public OmegaAttack AttackF { get; }

    public TopP2PartySynergyState(SimParty party, TopP2PartySynergyStateOverrides overrides)
    {
        NewNorthA = overrides.NewNorthA ?? rng.NextDirection();
        NewNorthB = overrides.NewNorthB ?? rng.NextDirection();
        Order = RoleList.Random(party);
        Stacks = RoleList.Random(party, 2);
        Glitch = overrides.Glitch ?? rng.NextObj(GlitchType.Far, GlitchType.Mid);
        AttackM = overrides.AttackM ?? rng.NextObj(OmegaAttack.Sword, OmegaAttack.Shield);
        AttackF = overrides.AttackF ?? rng.NextObj(OmegaAttack.Staff, OmegaAttack.Legs);
        AttackDir = rng.NextDirection().RotateRad(MathF.Tau / 16);
    }
    
    
}
