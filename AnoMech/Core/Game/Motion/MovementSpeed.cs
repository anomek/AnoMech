using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// Statuses whose param is a percentage of movement speed, by the Status sheet's ParamEffect: 1 takes
// it off (Heavy), 18 adds it (Sprint). The game sums them: Sprint's 30 with Heavy's 20 moves at 110%.
internal static class MovementSpeed
{
    private const byte Slower = 1;
    private const byte Faster = 18;

    // What an ordered walk's speed is multiplied by. An order's speed already holds the speed-ups (an
    // AI that sprints walks at its sprint speed), so only the slows change it.
    public static float Factor(IReadOnlyList<SimStatus> statuses)
    {
        int faster = 0, slower = 0;
        for (var i = 0; i < statuses.Count; i++)
        {
            var status = statuses[i];
            if (!status.IsActive) continue;
            switch (Natives.Data.Status(status.StatusId)?.ParamEffect)
            {
                case Faster:
                    faster += status.Stacks;
                    break;
                case Slower:
                    slower += status.Stacks;
                    break;
            }
        }
        return slower == 0 ? 1f : MathF.Max(0f, (100f + faster - slower) / (100f + faster));
    }
}
