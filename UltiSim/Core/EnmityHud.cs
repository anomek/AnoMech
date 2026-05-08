using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using UltiSim.Core.SimObjects;
using Action = Lumina.Excel.Sheets.Action;

namespace UltiSim.Core;

// Drives the in-game _EnemyList addon by writing rows directly into its
// NumberArray / StringArray during PreRequestedUpdate. We deliberately don't go
// through UIState.Hate/Hater + HudManager copier — that path requires the BC to
// be findable via CharacterManager.LookupBattleCharaByEntityId, which means
// inserting the BC into CharacterManager._battleCharas, which triggers a per-frame
// CharacterManager update that attaches the BC into render-side caches (Skeleton,
// CharacterLookAtController). Those attachments aren't drained by
// DeleteObjectByIndex, so freeing the BC produces a ghost render that crashes the
// next render task on a freed Skeleton vtable.
//
// Writing the addon arrays directly avoids the resolver lookup entirely. The
// addon doesn't care that the EntityId doesn't resolve to a real BC — it just
// renders whatever we put in the slots.
//
// Number array layout (EnemyListNumberArray): 5-int header, then 8 × 6-int rows:
//   header[0] Unk0, [1] EnemyCount, [2] TargetEntityId, [3] UnkEntityId, [4] Unk4
//   row[i] base = 5 + i*6: [+0] HP%, [+1] MaxHP%, [+2] Cast%, [+3] EntityId,
//          [+4] ActiveInList (bool-as-int), [+5] Unk5
// String array layout (EnemyListStringArray): 8 × 2 strings, base i*2:
//   [+0] EnemyName, [+1] CastName
internal sealed unsafe class EnmityHud : IDisposable
{
    private const int EnemyListSize = 8;
    private const string AddonName = "_EnemyList";

    private readonly SimEnemy?[] slotEnemies = new SimEnemy?[EnemyListSize];

    public EnmityHud()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    // Snapshot which enemies should occupy each addon slot. Called from
    // SimWorld.Tick once per frame; OnPreRequestedUpdate reads slotEnemies later
    // in the same frame to populate the addon arrays.
    public void Refresh(IEnumerable<SimEnemy> enemies)
    {
        Array.Clear(slotEnemies);
        var written = 0;
        foreach (var e in enemies)
        {
            if (written >= EnemyListSize) break;
            if (!e.InEnemyList) continue;
            if (!e.IsAlive) continue;
            slotEnemies[written++] = e;
        }
    }

    public void Clear() => Array.Clear(slotEnemies);

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        // No scenario running — all slots null after Clear(). Let the game
        // populate the list without interference.
        var anyTracked = false;
        for (int i = 0; i < EnemyListSize; i++)
            if (slotEnemies[i] != null) { anyTracked = true; break; }
        if (!anyTracked) return;

        if (args is not AddonRequestedUpdateArgs reqArgs) return;
        var numArrays = (NumberArrayData**)reqArgs.NumberArrayData;
        var strArrays = (StringArrayData**)reqArgs.StringArrayData;
        if (numArrays == null || strArrays == null) return;

        var numArr = numArrays[(int)NumberArrayType.EnemyList];
        var strArr = strArrays[(int)StringArrayType.EnemyList];
        if (numArr == null || strArr == null) return;

        var actionSheet = Plugin.DataManager.GetExcelSheet<Action>();

        var activeCount = 0;
        var firstEntityId = 0;
        for (int i = 0; i < EnemyListSize; i++)
        {
            var e = slotEnemies[i];
            if (e is null || !e.IsAlive)
            {
                WriteEmptyRow(numArr, strArr, i);
                continue;
            }

            activeCount++;
            var entityId = (int)e.EntityId;
            if (firstEntityId == 0) firstEntityId = entityId;

            var castPercent = -1;
            var castName = string.Empty;
            if (e.IsCasting)
            {
                castPercent = (int)Math.Clamp(e.CastProgress * 100f, 0f, 100f);
                if (e.CastActionId != 0 && actionSheet.TryGetRow(e.CastActionId, out var action))
                    castName = action.Name.ExtractText() ?? string.Empty;
            }

            var rowBase = 5 + i * 6;
            numArr->SetValue(rowBase + 0, 100); // HP%
            numArr->SetValue(rowBase + 1, 100); // MaxHP%
            numArr->SetValue(rowBase + 2, castPercent);
            numArr->SetValue(rowBase + 3, entityId);
            numArr->SetValue(rowBase + 4, 1); // ActiveInList
            numArr->SetValue(rowBase + 5, 0);

            var strBase = i * 2;
            strArr->SetValue(strBase + 0, e.DisplayName, managed: true);
            strArr->SetValue(strBase + 1, castName, managed: true);
        }

        numArr->SetValue(1, activeCount);
        numArr->SetValue(2, firstEntityId);
    }

    private static void WriteEmptyRow(NumberArrayData* numArr, StringArrayData* strArr, int i)
    {
        var rowBase = 5 + i * 6;
        numArr->SetValue(rowBase + 0, 0);
        numArr->SetValue(rowBase + 1, 0);
        numArr->SetValue(rowBase + 2, -1);
        numArr->SetValue(rowBase + 3, 0);
        numArr->SetValue(rowBase + 4, 0);
        numArr->SetValue(rowBase + 5, 0);

        var strBase = i * 2;
        strArr->SetValue(strBase + 0, string.Empty, managed: true);
        strArr->SetValue(strBase + 1, string.Empty, managed: true);
    }
}
