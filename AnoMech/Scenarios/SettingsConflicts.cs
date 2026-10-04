using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios;

// Per-player settings a host can ask for that the fight cannot actually produce: two healers
// both holding Accretion, two seats on the same Limit Cut number, five people in a four-slot
// Hello World. The run already drops what it can't seat and logs it, but by then the host has
// started and won't see why their setup didn't happen -- so each settings panel checks while
// they're still editing.
//
// Only checked while hosting. Solo sets one seat, which can't contradict itself, and the local
// player's role isn't known until the run starts.
public sealed class SettingsConflicts
{
    private readonly List<string> problems = [];

    public IReadOnlyList<string> Problems => problems;
    public bool Any => problems.Count > 0;

    public void Add(string problem)
    {
        if (!problems.Contains(problem)) problems.Add(problem);
    }

    public static string Seats(IEnumerable<PartyRole> roles)
        => string.Join(", ", roles.OrderBy(SettingsGrid.SeatIndex).Select(SettingsGrid.RoleLabel));

    // More seats want the thing than the fight has places for it.
    public void AtMost(int places, IReadOnlyCollection<PartyRole> claimants, string what)
    {
        if (claimants.Count <= places) return;
        Add(places == 1
            ? $"{Seats(claimants)} all want {what}, but only one player gets it."
            : $"{Seats(claimants)} all want {what}, but only {places} players get it.");
    }

    // So many seats refuse that the fight can't fill the places it has to fill.
    public void AtLeast(int needed, IReadOnlyCollection<PartyRole> refusers, int candidates, string what)
    {
        if (candidates - refusers.Count >= needed) return;
        Add(needed == 1
            ? $"{Seats(refusers)} all refuse {what}, but someone has to take it."
            : $"{Seats(refusers)} all refuse {what}, but {needed} players have to take it.");
    }

    // A seat asked for something its role can never hold.
    public void Forbidden(IReadOnlyCollection<PartyRole> roles, string what)
    {
        if (roles.Count == 0) return;
        Add($"{Seats(roles)} can never {what}.");
    }

    // Can every listed role be given one of its allowed indices, all at once? Roles with no
    // entry are unconstrained and ignored. Brute-force matching; at most 8 roles over 8 slots.
    public static bool CanPlaceAll(IReadOnlyDictionary<PartyRole, int[]> slots, int size)
    {
        var roles = slots.Keys.OrderBy(r => (int)r).ToList();
        var used = new bool[size];

        bool Place(int i)
        {
            if (i == roles.Count) return true;
            foreach (var slot in slots[roles[i]])
            {
                if (slot < 0 || slot >= size || used[slot]) continue;
                used[slot] = true;
                if (Place(i + 1)) return true;
                used[slot] = false;
            }
            return false;
        }

        return Place(0);
    }
}
