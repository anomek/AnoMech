using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class PlayerActionState : IPlayerActionState
{
    public bool HasJobGauge
    {
        get
        {
            var live = JobGaugeManager.Instance();
            return live != null && live->CurrentGauge != null;
        }
    }

    public uint ComboAction
    {
        get
        {
            var am = ActionManager.Instance();
            return am == null ? 0 : am->Combo.Action;
        }
    }

    public float ComboTimer
    {
        get
        {
            var am = ActionManager.Instance();
            return am == null ? 0f : am->Combo.Timer;
        }
    }

    public uint AdjustedActionId(uint actionId)
    {
        var am = ActionManager.Instance();
        return am == null ? actionId : am->GetAdjustedActionId(actionId);
    }
}
