using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;

namespace AnoMech.Helpers;

public static unsafe class GameObjectHelper
{
    public static void WriteName(GameObject* obj, string name)
    {
        var max = Math.Min(name.Length, 63);
        for (int i = 0; i < max; i++) obj->Name[i] = (byte)name[i];
        obj->Name[max] = 0;
    }
}
