using System;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyState
{
    private readonly Rng rng = Rng.Detached;

    public Direction NewNorthA { get; }
    public Direction NewNorthB { get; }
    public Direction AttackDir { get; }
    public RoleList Order { get; }
    public RoleList Stacks { get; }
    public GlitchType Glitch { get; }
    public OmegaAttack AttackM { get; }
    public OmegaAttack AttackF { get; }
    // The real clones look at whoever tops their enmity, a healer most of the time.
    public PartyRole CloneTarget { get; }

    public TopP2PartySynergyState(Rng rng, SimParty party, TopP2PartySynergyStateOverrides overrides)
    {
        this.rng = rng;
        NewNorthA = overrides.NewNorthA ?? rng.NextDirection();
        NewNorthB = overrides.NewNorthB ?? rng.NextDirection();
        Order = new RoleListBuilder
        {
            Slots = overrides.Symbol.Resolve(party.PlayerRole)
                             .ToDictionary(r => r.Role, r => new[] { 2 * (int)r.Value, 2 * (int)r.Value + 1 }),
        }.Build(rng, party);
        Stacks = new RoleListBuilder
        {
            Size = 2,
            Membership = overrides.Stack.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value),
        }.Build(rng, party);
        Glitch = overrides.Glitch ?? rng.NextObj(GlitchType.Far, GlitchType.Mid);
        AttackM = overrides.AttackM ?? rng.NextObj(OmegaAttack.Sword, OmegaAttack.Shield);
        AttackF = overrides.AttackF ?? rng.NextObj(OmegaAttack.Staff, OmegaAttack.Legs);
        var attackDir = rng.NextDirection().RotateRad(MathF.Tau / 16);
        AttackDir = overrides.AttackDir ?? attackDir;
        CloneTarget = rng.NextHealerRole();
    }

    // Network-replay constructor: reconstructs the fields TopP2PartySynergyAi reads; CloneTarget
    // is a harmless placeholder -- only the scenario's own host-only targeting reads it.
    // GlitchType/OmegaAttack aren't JSON-serializable (identified only by reference equality to
    // a static instance), so the wire message carries which named instance was chosen.
    private TopP2PartySynergyState(
        SimParty party, PartyRole[] order, PartyRole[] stacks, float newNorthARadians, float newNorthBRadians,
        float attackDirRadians, bool glitchIsFar, bool attackMIsSword, bool attackFIsStaff)
    {
        NewNorthA = new Direction(newNorthARadians);
        NewNorthB = new Direction(newNorthBRadians);
        Order = new RoleList(party, order);
        Stacks = new RoleList(party, stacks);
        Glitch = glitchIsFar ? GlitchType.Far : GlitchType.Mid;
        AttackM = attackMIsSword ? OmegaAttack.Sword : OmegaAttack.Shield;
        AttackF = attackFIsStaff ? OmegaAttack.Staff : OmegaAttack.Legs;
        AttackDir = new Direction(attackDirRadians);
        CloneTarget = PartyRole.RegenHealer;
    }

    public static TopP2PartySynergyState FromNetworkReplay(
        SimParty party, PartyRole[] order, PartyRole[] stacks, float newNorthARadians, float newNorthBRadians,
        float attackDirRadians, bool glitchIsFar, bool attackMIsSword, bool attackFIsStaff)
        => new(party, order, stacks, newNorthARadians, newNorthBRadians, attackDirRadians, glitchIsFar, attackMIsSword, attackFIsStaff);
}
