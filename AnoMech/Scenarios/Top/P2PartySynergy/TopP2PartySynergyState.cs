using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyState
{
    private readonly Rng rng = new();
    
    public EightWayDirection NewNorth { get; }
    public RoleList Order { get; }
    public RoleList Stacks { get; }
    public GlitchType Glitch { get; }
    public OmegaAttack AttackM { get; }
    public OmegaAttack AttackF { get; }

    public TopP2PartySynergyState(SimParty party)
    {
        NewNorth = rng.NextDirection();
        Order = RoleList.Random(party);
        Stacks = RoleList.Random(party, 2);
        Glitch = rng.NextObj(GlitchType.Far, GlitchType.Mid);
        AttackM = rng.NextObj(OmegaAttack.Sword, OmegaAttack.Shield);
        AttackF = rng.NextObj(OmegaAttack.Staff, OmegaAttack.Legs);
    }
}
