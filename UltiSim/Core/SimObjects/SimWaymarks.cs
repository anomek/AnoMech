using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace UltiSim.Core.SimObjects;

public enum WaymarkSlot : int
{
    A = 0, B = 1, C = 2, D = 3,
    One = 4, Two = 5, Three = 6, Four = 7,
}

public sealed record Waymark(WaymarkSlot Slot, Vector3 Offset);

public static class WaymarkPresets
{
    // Ring of A,1,B,2,C,3,D,4 spaced 45° apart, A at north going clockwise.
    // Angle convention: offset = (sin(a)*r, 0, cos(a)*r), a=π is north (-Z).
    public static Waymark[] Ring(float radius)
    {
        var slots = new[]
        {
            WaymarkSlot.A, WaymarkSlot.One,
            WaymarkSlot.B, WaymarkSlot.Two,
            WaymarkSlot.C, WaymarkSlot.Three,
            WaymarkSlot.D, WaymarkSlot.Four,
        };
        var ring = new Waymark[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var angle = MathF.PI - i * (MathF.PI / 4f);
            ring[i] = new Waymark(slots[i], new Vector3(radius * MathF.Sin(angle), 0, radius * MathF.Cos(angle)));
        }
        return ring;
    }
}

// Bulk-applied waymark layout. The native API (MarkingController.PlacePreset /
// ClearFieldMarkers) is all-or-nothing, so this models all eight slots as one
// SimObject — there's no per-slot lifecycle. Tick is a no-op; once placed the
// layout sits until Despawn (or scenario reset) tears it down.
public sealed unsafe class SimWaymarks : ISimObject
{
    private bool placed;

    internal SimWaymarks(IReadOnlyList<Waymark> waymarks, Vector3 origin)
    {
        if (waymarks.Count == 0) return;
        var controller = MarkingController.Instance();
        if (controller == null) { Plugin.Log.Warning("SimWaymarks: MarkingController unavailable"); return; }

        var placement = default(MarkerPresetPlacement);
        for (int i = 0; i < waymarks.Count; i++)
        {
            var wm = waymarks[i];
            var idx = (int)wm.Slot;
            if (idx < 0 || idx > 7) continue;
            var world = origin + wm.Offset;
            placement.Active[idx] = true;
            placement.X[idx] = (int)MathF.Round(world.X * 1000f);
            placement.Y[idx] = (int)MathF.Round(world.Y * 1000f);
            placement.Z[idx] = (int)MathF.Round(world.Z * 1000f);
        }

        var result = controller->PlacePreset(&placement);
        if (result != 0) Plugin.Log.Warning($"SimWaymarks: PlacePreset returned {result}");
        placed = true;
    }

    public bool IsAlive => placed;
    public void Tick(float deltaSeconds) { }

    public void Despawn()
    {
        if (!placed) return;
        var controller = MarkingController.Instance();
        if (controller != null) controller->ClearFieldMarkers();
        placed = false;
    }
}
