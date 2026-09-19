using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.CrystallizeTime.CrystallizeTimeConstants;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

public sealed partial class FruCrystallizeTimeScenario
{
    // Spirit Taker is a 5y jump. The extra yard gives movement some tolerance.
    private const float SpreadClearance = 6f;
    private readonly Dictionary<PartyRole, Vector3> spreadDestinations = [];
    private static readonly Vector3[] SpreadCandidates = CreateSpreadCandidates();

    private static Vector3[] CreateSpreadCandidates()
    {
        var result = new List<Vector3>();
        for (var x = -18; x <= 18; x += 6)
        for (var z = -18; z <= 18; z += 6)
        {
            var position = new Vector3(x, 0, z);
            if (position.Length() <= 18.5f && Vector3.Distance(position, FragmentPosition) >= SpreadClearance)
                result.Add(position);
        }
        return result.ToArray();
    }

    private void StartSpiritSpread()
    {
        puddleDestinations.Clear();
        spreadDestinations.Clear();
        UpdateSpiritSpread();
    }

    private void UpdateSpiritSpread()
    {
        var player = world.Party.Player;
        var bots = Members().OfType<SimPartyNpc>().OrderBy(b => b.Role).ToArray();
        foreach (var role in spreadDestinations.Keys.ToArray())
            if (!bots.Any(b => b.Role == role)) spreadDestinations.Remove(role);

        foreach (var bot in bots)
        {
            bool Clear(Vector3 position) =>
                (!player.IsAlive() || Vector3.Distance(position, player!.Position) >= SpreadClearance)
                && spreadDestinations.All(d => d.Key == bot.Role || Vector3.Distance(position, d.Value) >= SpreadClearance);

            // Keep a safe assignment to avoid making the whole formation shuffle
            // whenever the player moves. Only displaced bots need a new spot.
            if (spreadDestinations.TryGetValue(bot.Role, out var current) && Clear(current)) continue;
            var candidates = SpreadCandidates.Where(Clear).OrderBy(p => Vector3.DistanceSquared(bot.Position, p)).ToArray();
            if (candidates.Length == 0) continue;
            var destination = candidates[0];
            spreadDestinations[bot.Role] = destination;
            bot.MoveTo(destination, CrystallizeTimePattern.RunSpeed);
        }
    }
}
