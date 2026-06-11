using System;
using System.Runtime.InteropServices;
using AnoMech.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

internal static unsafe class TimelineFunctions
{
    public static void SetModelState(TimelineContainer* tc, byte value)
    {
        if (tc == null) return;
        TimelineContainerService.SetModelState(tc, value);
    }
}
