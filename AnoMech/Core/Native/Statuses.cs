using System;
using System.Runtime.InteropServices;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native;

// Status helpers for sim-only buffs. Apply writes directly into the
// StatusManager.Status array — bypasses bc->StatusManager.AddStatus, which
// drives _flags / ExtraFlags from the Status sheet AND auto-prunes any status
// whose sheet MaxDuration is 0 (most sim-only ids). Direct-slot insertion lets
// any statusId stick at the duration we ask for, with no sheet dependency.
//
// Trade-off: _flags / ExtraFlags don't get the sheet-driven bit set. That bit
// vector controls "is this entity stunned/silenced/etc" gameplay flags; for
// purely visual debuffs (tether markers, raid buffs we just want shown) this
// doesn't matter. If a future caller needs gameplay-effective flags, route
// that one through bc->StatusManager.AddStatus instead.
internal static unsafe class Statuses
{
    public static void Apply(Character* chara, ushort statusId, float duration, ushort param = 0, GameObjectId sourceObject = default)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        var slots = bc->StatusManager.Status;

        // Refresh in place if the status is already present (matches sheet
        // behavior of "reapply replaces" without needing a Remove + re-Add).
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].StatusId != statusId) continue;
            slots[i].Param = param;
            slots[i].RemainingTime = duration == 0 ? 20: duration;
            slots[i].SourceObject = sourceObject;
            return;
        }

        // Otherwise drop into the first empty slot. Keep the array packed at
        // low indices — see Remove's comment for why that matters to PartyHud.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].StatusId != 0) continue;
            slots[i].StatusId = statusId;
            slots[i].Param = param;
            slots[i].RemainingTime = duration == 0 ? 20: duration;
            slots[i].SourceObject = sourceObject;
            if (bc->StatusManager.NumValidStatuses <= i)
                bc->StatusManager.NumValidStatuses = (byte)(i + 1);
            return;
        }
    }

    // Applies a status through the engine's native StatusManager::AddStatus.
    // Triggers the sheet-driven side effects our direct-slot Apply skips:
    // _flags / ExtraFlags bits, StatusLoopVFX, and (most importantly for our
    // boss form swaps) the Param → CharacterData.TransformationId derivation
    // that runs inline inside the engine's apply path. Use for real game
    // statuses like Superfluid / OmegaM / OmegaF where that visual is wanted.
    //
    // Caveats:
    // - Duration comes from the sheet's MaxDuration. Sim-only ids with
    //   MaxDuration=0 will be auto-pruned by the engine immediately, so this
    //   helper is only safe for real game statuses today.
    // - All sheet-driven side effects fire (StatusLoopVFX, Flags etc.). For
    //   the transformation buffs that's exactly the canonical visual; for
    //   purely-visual sim debuffs you still want plain Apply.
    public static void AddStatusInit(Character* chara, ushort statusId, ushort param)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        bc->StatusManager.AddStatus(statusId, param);

        // AddStatus no-ops on the local player: its IsValidClientObject(this->Owner) guard rejects
        // the player (the player's own statuses come from the server, not this predictive add), so
        // nothing is written and no StatusLoopVFX spawns. Doppels are valid client objects, so
        // AddStatus runs fully (slot + gain VFX) and we early-return below.
        //
        // For the player, replicate AddStatus's body sans guard: write the slot ourselves, then
        // call OnGainStatus directly — the same gain path AddStatus uses on doppels, so the loop
        // VFX renders immediately at full scale (Owner stays the player, so it targets us).
        var slots = bc->StatusManager.Status;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].StatusId == statusId) return; // AddStatus took (doppel) — leave it alone

        Apply(chara, statusId, 0f, param);
        StatusManagerPointers.OnGainStatus(&bc->StatusManager, statusId, 0f, param, 0, 0);
    }

    public static void Remove(Character* chara, ushort statusId)
    {
        if (chara == null || statusId == 0) return;
        var bc = (BattleChara*)chara;
        var slot = bc->StatusManager.GetStatusIndex(statusId);
        if (slot >= 0 && slot <= bc->StatusManager.NumValidStatuses)
            bc->StatusManager.RemoveStatus(slot);
    }
}
