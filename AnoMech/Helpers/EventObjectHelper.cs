using AnoMech.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Helpers;

internal static unsafe class EventObjectHelper
{
    // Allocates a new EventObject slot bound to the given EObj sheet row.
    // Returns the manager slot index (0..39) on success, -1 on failure. The
    // engine sets up the SharedGroup model internally via FUN_14174dac0; the
    // caller is responsible for SetPosition/SetRotation/SetVisible afterwards.
    public static bool Create(uint eObjRowId, out int slot, out GameObject* obj)
    {
        slot = -1;
        obj = null;

        var eventObjectManager = EventObjectManager.Instance();

        if (eventObjectManager == null)
        {
            Plugin.Log.Warning("[EventObjectHelper.Create] EventObjectManager.Instance() was null");
            return false;
        }

        // All other params 0: we want the engine to auto-resolve the SharedGroup
        // from EObj.SgbPath. Inside CreateEventObject, FUN_14174dac0 contains:
        //
        //   if (param_5 == 0 && (actor[0x9c] & 4) == 0) {
        //       actor->ExportedSGRowPtr = ExdModule.GetExportedSGRow(eobjRow.SgbPath);
        //   }
        //
        // param_5 = 0 ✓. The bit-2 check on actor[0x9c] depends on our final
        // `flag` arg: CreateEventObject writes `actor[0x9c] |= flag << 2`. flag=1
        // sets bit 2, which causes the engine to skip the auto-lookup (the packet
        // path needs the layer-side resolution since the server passes an explicit
        // param_5). For simulator-spawned EObjs we want the EObj-sheet lookup, so
        // flag must be 0 — confirmed empirically by reading actor->ExportedSGRowPtr
        // after spawn.
        slot = EventObjectManagerService.CreateEventObject(eventObjectManager, 0, eObjRowId, 0, 0, 0, -1, 0);
        if (slot < 0)
        {
            Plugin.Log.Warning($"EventObjectSpawn: CreateEventObject returned -1 for EObj row {eObjRowId} (0x{eObjRowId:X}) — pool full or invalid row");
            return false;
        }

        // CreateEventObject writes the new actor pointer into _EventObjects[slot]
        // itself (matches the `param_1[slot + 2] = puVar3` line in the decompile).
        // No GetObjectByIndex binding needed — index straight in.
        obj = eventObjectManager->EventObjects[slot].Value;
        if (obj == null)
        {
            Plugin.Log.Warning($"EventObjectSpawn: slot {slot} resolved but EventObjects[{slot}] is null");
            return false;
        }

        return true;
    }

    // Per-slot teardown. CreateEventObject itself fires the vfunc at vtable+0x1E0
    // when reusing an occupied slot — that's GameObject.Terminate (VirtualFunction(60))
    // as bound by ClientStructs. We invoke the same canonical path, then null the
    // manager's _EventObjects[slot] pointer to release the slot for reuse.
    public static void Destroy(int slot)
    {
        if (slot < 0 || slot >= 40)
        {
            Plugin.Log.Warning($"[EventObjectHelper.Destroy] Invalid Slot ({slot}) was introduced.");
            return;
        }

        var mgr = EventObjectManager.Instance();

        if (mgr == null)
        {
            Plugin.Log.Warning("[EventObjectHelper.Destroy] EventObjectManager.Instance() was null.");
            return;
        }

        var arr = mgr->EventObjects;
        var entry = arr[slot].Value;

        if (entry == null)
        {
            Plugin.Log.Warning("[EventObjectHelper.Destroy] entry was null.");
            return;
        }

        entry->Terminate();
        arr[slot] = (GameObject*)null;
    }

    // Writes the EObj state field (actor[0x1B2]) and notifies the attached
    // SharedGroupLayoutInstance to switch sub-instances. Different SGs use
    // different state values to gate which sub-instances are visible —
    // simulator code finds the right value empirically per EObj row.
    public static void SetState(GameObject* obj, short state)
    {
        if (obj == null)
        {
            Plugin.Log.Warning("[EventObjectHelper.SetState] obj was null.");
            return;
        }

        EventObjectService.SetEventObjectState((EventObject*)obj, state, 1, 0);
    }
}
