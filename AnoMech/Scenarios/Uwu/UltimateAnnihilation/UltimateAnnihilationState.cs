using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.UltimateAnnihilation;

public class UltimateAnnihilationState
{
    public readonly record struct Puddle(Vector2 Center, float Radius, float LandsAt);

    public Rng Rng { get; }

    public List<Puddle> Puddles { get; } = [];
    public int UnpoppedOrbs { get; set; }

    public PartyRole SearingWindTarget { get; }
    public PartyRole FlamingCrushTarget { get; }
    public PartyRole FirstMesohighTaker => PartyRole.PhysRangedDps;
    public PartyRole SecondMesohighTaker => SearingWindTarget;

    public SimTether? Mesohigh;

    public UltimateAnnihilationState(Rng rng, SimParty party, UltimateAnnihilationStateOverrides overrides)
    {
        Rng = rng;
        var playerRole = party.PlayerRole;
        var playerIsHealer = !playerRole.IsTank() && !playerRole.IsDps();

        SearingWindTarget = overrides.SearingWindOnPlayer && playerIsHealer ? playerRole : Rng.NextHealerRole();
        FlamingCrushTarget = overrides.FlamingCrushOnPlayer && playerRole.IsDps() ? playerRole : Rng.NextDpsRole();
    }
}
