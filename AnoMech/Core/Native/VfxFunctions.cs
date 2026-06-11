using AnoMech.Core.Game;
using AnoMech.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;
using System.Text;

namespace AnoMech.Core.Native;

// Wrappers around native VfxContainer functions FFXIVClientStructs doesn't bind.
// SetTether allocates/releases the VFX pointer at Tether+0x08 — writing the
// Tether struct directly leaves a dangling vfx, so the line keeps drawing.
//
// Static VFX (omens): setting CastInfo manually doesn't trigger Character::StartCast's
// omen spawn. We call the same StaticVfxCreate / StaticVfxRun / StaticVfxRemove
// trio that VFXEditor and FFXIV-RaidsRewritten use — proven sigs, simple layout.
internal static unsafe class VfxFunctions
{
    private static byte[]? poolBytes;

    public static void SetTether(Character* chara, byte slot, ushort tetherId, GameObjectId targetId, byte progress)
    {
        if (chara == null) return;
        VfxContainerService.SetTether(&chara->Vfx, slot, tetherId, (ulong)targetId, progress);
    }

    public static void ClearTether(Character* chara, byte slot)
        => SetTether(chara, slot, 0, default, 0);

    // Reads the channeling-sheet id currently occupying a tether slot. Returns 0
    // when chara is null or the slot is empty. Used by SimTether's auto-expire
    // path to avoid clearing a slot that a chained tether has already overwritten.
    public static ushort GetTetherId(Character* chara, byte slot)
    {
        if (chara == null) return 0;
        return chara->Vfx.Tethers[slot].Id;
    }

    public static VfxObject* SpawnStaticVfx(string path, Placement placement, Vector3 scale)
    {
        if (string.IsNullOrEmpty(path)) return null;

        poolBytes ??= Encoding.UTF8.GetBytes("Client.System.Scheduler.Instance.VfxObject\0");

        var pathBytes = Encoding.UTF8.GetBytes(path + "\0");
        VfxObject* vfx;
        fixed (byte* pathPtr = pathBytes)
        fixed (byte* poolPtr = poolBytes)
        {
            vfx = VfxObject.Create(pathPtr, poolPtr);
        }
        if (vfx == null) return null;

        vfx->Position = placement.Position;
        var q = Quaternion.CreateFromYawPitchRoll(placement.Rotation, 0f, 0f);
        vfx->Rotation = q;
        vfx->Scale = scale;
        vfx->Flags |= 0x2;          // mark dirty so position/rotation/scale apply
        vfx->SomeFlags &= 0xF7;     // clear flag that sometimes hides the vfx

        vfx->Update(0f, -1);
        return vfx;
    }

    public static void RemoveStaticVfx(VfxObject* vfx)
    {
        if (vfx == null) return;
        vfx->CleanupRender();
    }

    // Spawns an entity-attached VFX (follows caster/target, used for head markers and
    // similar continuous effects). Path must exist in game data — caller validates.
    public static VfxData* SpawnActorVfx(string path, Character* caster, Character* target)
    {
        if (string.IsNullOrEmpty(path) || caster == null || target == null) return null;

        var bytes = Encoding.UTF8.GetBytes(path + "\0");
        fixed (byte* p = bytes)
        {
            return VfxDataService.ActorVfxCreate(p, (GameObject*)caster, (GameObject*)target, -1f, 0, 0, 0);
        }
    }

    public static void RemoveActorVfx(VfxData* vfx)
    {
        if (vfx == null) return;
        VfxDataService.Dtor(vfx, 0);
    }

    public static bool VfxPathExists(string path)
    {
        try
        {
            if (Plugin.DataManager.FileExists(path)) return true;
            Plugin.Log.Warning($"AddVfx: path not found '{path}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"AddVfx: FileExists threw for '{path}': {ex.Message}");
        }
        return false;
    }
    
    public static string? LockonVfxIconName(uint lockonId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lockon>();
        if (!sheet.TryGetRow(lockonId, out var row)) return null;
        var iconName = row.IconName.ExtractText();
        if (string.IsNullOrEmpty(iconName)) return null;
        return iconName;
    }
}
