using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AnoMech.Core.Game;

public enum WaymarkSlot : int
{
    A = 0, B = 1, C = 2, D = 3,
    One = 4, Two = 5, Three = 6, Four = 7,
}

public sealed record Waymark(WaymarkSlot Slot, Vector3 Offset);

// A named waymark layout, selectable in the main window's "Waymarks:" dropdown.
// Markers are scenario-local offsets, same frame as Waymark.Offset.
public sealed record WaymarkLayout(string Name, IReadOnlyList<Waymark> Markers);

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

// Bulk-applied waymark layout. Writes directly into MarkingController._fieldMarkers
// instead of going through PlacePreset / ClearFieldMarkers — those are gated by
// territory ("No markers allowed in territory" return code 5) and would no-op
// in the overworld. The renderer reads _fieldMarkers each frame, so direct writes
// place client-side markers anywhere. The layout sits until ClearAll (scenario
// reset) blanks the eight slots. Mirrors Markings (party signs): a writer owned
// by SimWorld, not a ticking SimObject. Offsets passed to Place are scenario-local;
// the injected Coordinates resolves them against the live ScenarioOrigin.
public sealed unsafe class Waymarks(Coordinates coordinates)
{
    private const int SlotCount = 8;

    public void Place(IReadOnlyList<Waymark> waymarks)
    {
        if (waymarks.Count == 0) return;
        var controller = MarkingController.Instance();
        if (controller == null) { Plugin.Log.Warning("Waymarks: MarkingController unavailable"); return; }

        for (int i = 0; i < waymarks.Count; i++)
        {
            var wm = waymarks[i];
            var idx = (int)wm.Slot;
            if (idx < 0 || idx >= SlotCount) continue;
            var world = coordinates.ToGlobal(wm.Offset);
            ref var slot = ref controller->FieldMarkers[idx];
            slot.Position = world;
            slot.X = (int)MathF.Round(world.X * 1000f);
            slot.Y = (int)MathF.Round(world.Y * 1000f);
            slot.Z = (int)MathF.Round(world.Z * 1000f);
            slot.Active = true;
        }
    }

    public void ClearAll()
    {
        var controller = MarkingController.Instance();
        if (controller == null) return;
        for (int i = 0; i < SlotCount; i++)
            controller->FieldMarkers[i].Active = false;
    }
}
