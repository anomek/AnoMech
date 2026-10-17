using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Node;

namespace AnoMech.Core.Native.Implementations.Interop;

// Direct EObj state writes UWU's props were tuned against in game.
internal static unsafe partial class EventObjectHelper
{
    // Writes the EObj state field (actor[0x1B2]) and notifies the attached SharedGroup to switch
    // sub-instances; which state shows what is per SGB.
    public static void SetState(GameObject* obj, ushort state)
    {
        if (obj != null) EventObjectPointers.SetEventObjectState((EventObject*)obj, state, 1, 0);
    }

    public static bool ForceActive(SharedGroupLayoutInstance* sg)
    {
        if (sg == null || ((ILayoutInstance*)sg)->IsActive) return false;
        SetActiveRecursive((ILayoutInstance*)sg, true, 3);
        return true;
    }

    public static void Deactivate(SharedGroupLayoutInstance* sg)
    {
        if (sg != null) SetActiveRecursive((ILayoutInstance*)sg, false, 3);
    }

    private static void SetActiveRecursive(ILayoutInstance* inst, bool active, int depth)
    {
        if (inst == null) return;
        inst->SetActive(active);
        if (depth <= 0 || inst->Id.Type != InstanceType.SharedGroup) return;
        var sg = (SharedGroupLayoutInstance*)inst;
        var count = sg->Instances.Instances.Count;
        for (var i = 0; i < count && i < 16; i++)
        {
            var child = (ChildNodeInstance*)sg->Instances.Instances[i].Value;
            if (child != null && child->Instance != null)
                SetActiveRecursive(child->Instance, active, depth - 1);
        }
    }
}
