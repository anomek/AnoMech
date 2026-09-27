using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P5Flood;

// Stacks near the boss in a cardinal quadrant and rotates 90 deg after every tick's resolve, in
// one direction for the whole sequence. Addresses every slot; PlayerMovement decides whether a
// real player's slot actually moves.
public sealed class UmadP5FloodAi : IScenarioAi<UmadP5FloodState>
{
    public string Name => "Rotate";

    private const float ConvergeAt = 0.8f;   // well before the first telegraph
    private const float MoveSpeed = 8.3f;
    private const float QuadrantRadius = 4.0f;
    private const float ClumpRadius = 1.2f;    // spread within the quadrant so bots don't overlap

    // Just after the hit, never on the same instant: an equal timestamp fires in insertion
    // order, and the AI's events are added before the scenario's own resolve.
    private const float DepartAfterResolve = 0.1f;

    // Cardinal quadrant centres: 0=N, 1=E, 2=S, 3=W (local coords, +X east/+Z south).
    private static readonly Vector3[] Quadrants =
    [
        new(0f, 0f, -QuadrantRadius), // N
        new(QuadrantRadius, 0f, 0f),  // E
        new(0f, 0f, QuadrantRadius),  // S
        new(-QuadrantRadius, 0f, 0f), // W
    ];

    public void Run(UmadP5FloodState state, SimWorld world)
    {
        state.Timeline.Add(ConvergeAt, () => MoveToQuadrant(world, QuadrantAt(state, 0)));

        for (var tick = 1; tick < UmadP5FloodScenario.TickCount; tick++)
        {
            var quadrant = QuadrantAt(state, tick);
            if (quadrant == QuadrantAt(state, tick - 1)) continue;

            var previousResolve = UmadP5FloodScenario.FirstTelegraphAt
                + (tick - 1) * UmadP5FloodScenario.TelegraphStagger
                + UmadP5FloodScenario.ResolveDelayAfterTelegraph;
            state.Timeline.Add(previousResolve + DepartAfterResolve, () => MoveToQuadrant(world, quadrant));
        }
    }

    // Ticks 0 and 1 share a quadrant: the tick-1-safe cardinal is always also tick-2-safe (see
    // UmadP5FloodState); rotation starts from tick 2.
    private static int QuadrantAt(UmadP5FloodState state, int tick)
    {
        var rotations = Math.Max(0, tick - 1);
        var step = state.RotationClockwise ? rotations : -rotations;
        return ((state.StartQuadrant + step) % 4 + 4) % 4;
    }

    private static void MoveToQuadrant(SimWorld world, int quadrant)
    {
        var centre = Quadrants[quadrant];
        for (var slot = 0; slot < 8; slot++)
        {
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;

            var angle = slot * MathF.PI / 4f;
            var offset = new Vector3(MathF.Sin(angle) * ClumpRadius, 0f, MathF.Cos(angle) * ClumpRadius);
            bot.MoveTo(centre + offset, MoveSpeed);
        }
    }
}
