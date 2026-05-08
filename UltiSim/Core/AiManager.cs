using System;
using System.Collections.Generic;
using System.Numerics;
using UltiSim.Core.SimObjects;

namespace UltiSim.Core;

// Drives slot-ordered party movement from a scenario's position functions.
// Owns jitter, run speed, event scheduling, and local→world coordinate conversion.
// Position functions return an AiMove whose entries are scenario-local XZ coords;
// AiManager adds ScenarioOrigin and sets Y from origin. Eye-spawn flip and slot
// reordering are handled inside the AiMove before it reaches here.
public sealed class AiManager
{
    private const float RunSpeed = 6f;
    private const float DefaultJitter = 0.3f;

    private readonly SimWorld world;
    private readonly Random rng = new();

    public AiManager(SimWorld world)
    {
        this.world = world;
    }

    // Schedule a slot-move at `time`. `positions` is evaluated at fire-time;
    // null entries in the returned AiMove are skipped (no movement that slot).
    public void Move(float time, Func<AiMove> positions, float jitter = DefaultJitter)
    {
        world.Events.Add(time, () =>
        {
            var move = positions();
            var origin = world.ScenarioOrigin;
            for (int i = 0; i < 8; i++)
            {
                if (move[i] is not { } local) continue;
                var member = world.Party.Get(i);
                if (member is not { IsAlive: true }) continue;
                var target = new Vector3(origin.X + local.X, origin.Y, origin.Z + local.Y);
                member.MoveTo(Jitter(target, jitter), RunSpeed);
            }
        });
    }

    private Vector3 Jitter(Vector3 target, float radius)
    {
        var theta = rng.NextDouble() * 2.0 * Math.PI;
        var r = radius * MathF.Sqrt((float)rng.NextDouble());
        return new Vector3(
            target.X + r * MathF.Cos((float)theta),
            target.Y,
            target.Z + r * MathF.Sin((float)theta));
    }
}
