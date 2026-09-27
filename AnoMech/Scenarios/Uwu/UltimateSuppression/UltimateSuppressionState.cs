using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.UltimateSuppression;

public class UltimateSuppressionState
{
    public readonly Rng Rng = new();

    public Placement LightPillarPlacement { get; set; } = new();

    public SimCharacter? PlayerLightPillar = null!;
    public SimCharacter?[] PlayerMistralSongs = new SimCharacter?[2];
    public SimCharacter?[] PlayerEruptions = new SimCharacter?[2];
    public SimCharacter? PlayerGaol = null!;
    public SimCharacter? PlayerFlamingCrush = null!;

    public SimTether? MesohighTether = null;

    public const int SuppressionSpots = 6;
    // Which spread spot each non-tank takes.
    public int[] SuppressionSpotOrder { get; private init; } = [];

    public UltimateSuppressionState(SimParty party, UltimateSuppressionStateOverrides overrides)
    {
        RoleList roles;
        var doOverride = !party.PlayerRole.IsTank() && overrides.Assignment != UltimateSuppressionAssignment.Auto;

        if (doOverride)
        {
            roles = RoleList.AllExcept(party, [PartyRole.MainTank, PartyRole.OffTank, party.PlayerRole]);
        }
        else
        {
            roles = RoleList.AllExcept(party, [PartyRole.MainTank, PartyRole.OffTank]);
        }

        var index = 0;

        PlayerLightPillar = (doOverride && overrides.Assignment == UltimateSuppressionAssignment.LightPillar) ? party.Player : roles.Get(index++);
        PlayerMistralSongs[0] = (doOverride && overrides.Assignment == UltimateSuppressionAssignment.MistralSong) ? party.Player : roles.Get(index++);
        PlayerMistralSongs[1] = roles.Get(index++);
        PlayerEruptions[0] = (doOverride && overrides.Assignment == UltimateSuppressionAssignment.Eruption) ? party.Player : roles.Get(index++);
        PlayerEruptions[1] = roles.Get(index++);
        PlayerGaol = (doOverride && overrides.Assignment == UltimateSuppressionAssignment.Gaol) ? party.Player : roles.Get(index++);
        PlayerFlamingCrush = party.Get(Rng.NextDpsRole());
        SuppressionSpotOrder = Rng.Shuffle(Enumerable.Range(0, SuppressionSpots).ToArray()).ToArray();
    }
}
