using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game.Motion;

// Statuses whose param is a percentage of movement speed, by the Status sheet's ParamEffect: 1 takes
// it off (Heavy), 18 adds it (Sprint). The game sums them: Sprint's 30 with Heavy's 20 moves at 110%.
internal static class MovementSpeed
{
    private const byte Slower = 1;
    private const byte Faster = 18;

    // Every character reads this every tick, so each status's row is read once per installed game data.
    private static IGameData? readFrom;
    private static readonly Dictionary<ushort, byte> paramEffects = [];

    // What an ordered walk's speed is multiplied by. An order's speed already holds the speed-ups (an
    // AI that sprints walks at its sprint speed), so only the slows change it.
    public static float Factor(IReadOnlyList<(ushort StatusId, ushort Param)> statuses)
    {
        int faster = 0, slower = 0;
        for (var i = 0; i < statuses.Count; i++)
        {
            var (statusId, param) = statuses[i];
            switch (ParamEffect(statusId))
            {
                case Faster:
                    faster += param;
                    break;
                case Slower:
                    slower += param;
                    break;
            }
        }
        return slower == 0 ? 1f : MathF.Max(0f, (100f + faster - slower) / (100f + faster));
    }

    private static byte ParamEffect(ushort statusId)
    {
        var data = Natives.Data;
        if (!ReferenceEquals(readFrom, data))
        {
            paramEffects.Clear();
            readFrom = data;
        }
        if (!paramEffects.TryGetValue(statusId, out var effect))
            paramEffects[statusId] = effect = data.Status(statusId)?.ParamEffect ?? 0;
        return effect;
    }
}
