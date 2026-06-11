using System;
using System.Runtime.InteropServices;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

internal static unsafe class TimelineFunctions
{
    public static void SetModelState(TimelineContainer* tc, byte value)
    {
        if (tc == null) return;
        TimelineContainerPointers.SetModelState(tc, value);
    }
}
