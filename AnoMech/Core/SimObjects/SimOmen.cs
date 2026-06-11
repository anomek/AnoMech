using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AnoMech.Core.SimObjects;

// The AOE telegraph for a simulated cast (or a standalone scenario flash).
// Manually writing CastInfo bypasses Character::StartCast, so the engine never
// auto-spawns the omen that normally accompanies a cast bar; SimOmen replicates
// it by spawning a StaticVfx at the target location.
//
// A SimOmen is an ISimObject owned by whoever creates it — SimCast for a cast
// telegraph, SimWorld for a standalone flash — and that owner ticks and despawns
// it. It is built either from an action id (path + scale derived from the action's
// Omen sheet entry) or from an explicit omen path + scale. With no duration it is
// persistent (IsActive until the owner despawns it); with a duration it reports
// IsActive=false once the time elapses, so a list-owner (SimWorld.children)
// reaps it automatically.
//
// One SimOmen owns at most two native StaticVfx pointers: `primary` plus an
// optional `alt` — the perpendicular arm of a CastType-11 "+" cross, or a
// paired shape some actions declare in OmenAlt. Despawn() frees both.
//
// Bad VFX paths crash on the file thread, so every candidate is validated via
// DataManager.FileExists (ResolveOmenPath) before StaticVfxCreate is called.
public sealed unsafe class SimOmen : ISimObject
{
    // Like every SimObject, SimOmen works in scenario-local coordinates and converts
    // to global only at the native boundary (SpawnStaticVfx).
    private readonly Coordinates coordinates;

    private VfxObject* primary;
    private VfxObject* alt;

    // null = persistent (cleared explicitly by the owner); otherwise seconds left
    // before this omen reports itself inactive for reaping.
    private float? remaining;

    // Telegraph for `actionId` centered at `origin` (scenario-local) and oriented at
    // `rotation` (radians, already including any per-cast offset). No-ops when the
    // action has no Omen entry or its file can't be resolved.
    internal SimOmen(Coordinates coordinates, uint actionId, Vector3 origin, float rotation, float? durationSeconds = null)
    {
        this.coordinates = coordinates;
        remaining = durationSeconds;
        SpawnFromAction(actionId, origin, rotation);
    }

    // Telegraph from an explicit omen path + scale. Used by scenarios that want a
    // synthetic AOE not backed by an action's Omen sheet entry. `placement` is
    // scenario-local.
    internal SimOmen(Coordinates coordinates, string omenPath, Placement placement, Vector3 scale, float? durationSeconds = null)
    {
        this.coordinates = coordinates;
        remaining = durationSeconds;
        var resolved = ResolveOmenPath(omenPath);
        if (resolved == null)
        {
            Plugin.Log.Warning($"SimOmen: omen file not found (raw='{omenPath}')");
            return;
        }
        primary = VfxFunctions.SpawnStaticVfx(resolved, coordinates.ToGlobal(placement), scale);
    }

    public bool IsActive => (primary != null || alt != null) && (remaining is not { } r || r > 0f);

    public void Tick(float deltaSeconds)
    {
        if (remaining is { } r) remaining = r - deltaSeconds;
    }

    public void Despawn()
    {
        if (primary != null)
        {
            VfxFunctions.RemoveStaticVfx(primary);
            primary = null;
        }
        if (alt != null)
        {
            VfxFunctions.RemoveStaticVfx(alt);
            alt = null;
        }
    }

    private void SpawnFromAction(uint actionId, Vector3 origin, float rotation)
    {
        var actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (!actionSheet.TryGetRow(actionId, out var action))
        {
            Plugin.Log.Information($"SimOmen: action row {actionId:X} ({actionId}) not found in sheet");
            return;
        }
        if (action.Omen.ValueNullable is not { } omen || omen.RowId == 0)
        {
            Plugin.Log.Information($"SimOmen: action {actionId:X} ({actionId}) has no Omen entry (Omen.RowId=0 or null)");
            return;
        }
        var resolvedPath = ResolveActionOmenPath(actionId, omen.Path.ToString());
        if (resolvedPath == null) return;
        Plugin.Log.Information($"SimOmen: action {actionId:X} omen path resolved to '{resolvedPath}' (CastType={action.CastType}, EffectRange={action.EffectRange}, XAxisMod={action.XAxisModifier})");

        var range = action.EffectRange;
        if (range <= 0) range = 1;
        // CastType 4/11/12 use XAxisModifier as the rectangle's full width along X.
        var halfWidth = action.XAxisModifier > 0 ? action.XAxisModifier * 0.5f : range;
        var scale = action.CastType switch
        {
            4 or 11 or 12 => new Vector3(halfWidth, 1f, range),
            _ => new Vector3(range, 1f, range),
        };
        var globalOrigin = coordinates.ToGlobal(origin);
        Plugin.Log.Information($"SimOmen: action {actionId:X} origin(local)=<{origin.X:F2},{origin.Y:F2},{origin.Z:F2}> rot={rotation:F3} scale=<{scale.X:F2},{scale.Y:F2},{scale.Z:F2}>");
        primary = VfxFunctions.SpawnStaticVfx(resolvedPath, new Placement(globalOrigin, rotation), scale);

        // CastType 11 is a "+" cross whose Omen sheet entry points at the same single-bar
        // file (`general_x02f`) as a regular rect; the cross visual is formed by spawning
        // that bar twice — second copy rotated 90° to make the perpendicular arm.
        if (action.CastType == 11)
        {
            var perpRotation = MathUtil.NormalizeRotation(rotation + MathF.PI / 2f);
            alt = VfxFunctions.SpawnStaticVfx(resolvedPath, new Placement(globalOrigin, perpRotation), scale);
        }
        else if (action.OmenAlt.ValueNullable is { } omenAlt && omenAlt.RowId != 0)
        {
            // Defensive: some non-cross actions populate OmenAlt with a paired shape.
            var altPath = ResolveActionOmenPath(actionId, omenAlt.Path.ToString());
            if (altPath != null)
                alt = VfxFunctions.SpawnStaticVfx(altPath, new Placement(globalOrigin, rotation), scale);
        }
    }

    private static string? ResolveActionOmenPath(uint actionId, string rawPath)
    {
        var resolved = ResolveOmenPath(rawPath);
        if (resolved == null)
            Plugin.Log.Warning($"SimOmen: omen file not found for action {actionId} (raw='{rawPath}')");
        return resolved;
    }

    // Lumina's Omen.Path is typically a bare name (`gl_circle_5007_x1`); the on-disk
    // resource lives at `vfx/omen/eff/{name}.avfx`. A full path (containing `/`) is
    // used as-is. Lumina.FileExists throws on paths without an extension, so we only
    // test candidates that have one.
    private static string? ResolveOmenPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var withExt = raw.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase) ? raw : raw + ".avfx";
        var fullPath = withExt.Contains('/') ? withExt : $"vfx/omen/eff/{withExt}";
        try
        {
            if (Plugin.DataManager.FileExists(fullPath)) return fullPath;
            if (fullPath != withExt && Plugin.DataManager.FileExists(withExt)) return withExt;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"SimOmen.ResolveOmenPath: FileExists threw for '{fullPath}': {ex.Message}");
        }
        return null;
    }
}
