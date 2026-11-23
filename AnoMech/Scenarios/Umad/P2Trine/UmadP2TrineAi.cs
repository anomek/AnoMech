using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Trine;

public sealed class UmadP2TrineAi : IScenarioAi<UmadP2TrineState>
{
    public string Name => "Yoruha";
    public string Group => "JP";
    private static readonly Vector2[] TankPriority = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];
    private static readonly Vector2[] PartyPriority = [new(0, 1), new(1, 0), new(-1, 0), new(0, -1)];

    public void Run(UmadP2TrineState state, SimWorld world)
    {
        var (mt, st, party) = FinalPositions(state);
        var first = state.WaveVertices(0);
        var waiting = (from x in Enumerable.Range(-18, 37)
                       from z in Enumerable.Range(-18, 37)
                       let p = new Vector2(x, z)
                       where p.LengthSquared() <= 18.5f * 18.5f && Clear(p, first)
                       orderby Vector2.DistanceSquared(p, party)
                       select p).First();
        var ai = new AiManager(world);
        ai.Move(0.1f, () => AiMove.All(new(state.LeftWing ? -5f : 5f, 0f)), jitter: 0f);
        ai.Move(10.6f, () => AiMove.All(waiting), jitter: 0f, sprint: true);
        ai.Move(16.0f, () => AiMove.Create(mt, st, party, party, party, party, party, party).NaturalOrder(), jitter: 0f, sprint: true);
    }

    internal static (Vector2 Mt, Vector2 St, Vector2 Party) FinalPositions(UmadP2TrineState state)
    {
        var pending = state.WaveVertices(1).Concat(state.WaveVertices(2)).ToArray();
        var outer = Ring(19.4f).Where(p => Clear(p, pending)).OrderBy(p => Priority(p, TankPriority)).ToArray();
        var group = Ring(16.2f).Where(p => Clear(p, pending)).OrderBy(p => Priority(p, PartyPriority)).ToArray();
        var inner = Enumerable.Range(0, 11).SelectMany(i => Ring(8f + i * 0.5f))
            .Where(p => Clear(p, pending)).OrderBy(p => Priority(p, TankPriority)).ThenBy(p => p.LengthSquared()).ToArray();
        foreach (var st in outer)
        foreach (var party in group)
        {
            if (Vector2.DistanceSquared(st, party) < 7.8f * 7.8f) continue;
            foreach (var mt in inner)
                if (Vector2.DistanceSquared(mt, st) >= 7.8f * 7.8f
                    && Vector2.DistanceSquared(mt, party) >= 7.8f * 7.8f)
                    return (mt, st, party);
        }
        throw new InvalidOperationException("Trine layout has no separated safe tank and party formation.");
    }

    private static IEnumerable<Vector2> Ring(float radius)
        => Enumerable.Range(0, 72).Select(i => new Vector2(MathF.Sin(i * MathF.PI / 36), MathF.Cos(i * MathF.PI / 36)) * radius);
    private static bool Clear(Vector2 p, IReadOnlyList<Vector2> blasts)
        => blasts.All(b => Vector2.DistanceSquared(p, b) >= 6.7f * 6.7f);
    private static float Priority(Vector2 point, IReadOnlyList<Vector2> directions)
    {
        var p = Vector2.Normalize(point);
        for (var i = 0; i < directions.Count; i++)
        {
            var dot = Vector2.Dot(p, directions[i]);
            if (dot >= 0.707f) return i * 10 + MathF.Acos(Math.Clamp(dot, -1f, 1f));
        }
        return 100f;
    }
}

