using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Spends the scenario's faked limit break gauge once a limit break resolves; a cast
// interrupted before the slidecast window never reaches here, so it costs nothing. What the
// limit break does is an ordinary JobActions row.
internal sealed class LimitBreakHandler : IUserActionHandler
{
    private const uint LimitBreakCategory = 9;

    public static bool IsLimitBreak(uint actionId)
        => Natives.Data.Action(actionId)?.ActionCategory == LimitBreakCategory;

    // level 1-3; 0 when the job has none at that level.
    public static uint ActionId(uint classJob, int level)
    {
        if (Natives.Data.ClassJob(classJob) is not { } job) return 0;
        return level switch
        {
            1 => job.LimitBreak1,
            2 => job.LimitBreak2,
            3 => job.LimitBreak3,
            _ => 0,
        };
    }

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action || !IsLimitBreak(actionId)) return;
        if (Plugin.GameInstance?.World is not { } world) return;
        world.Party.LimitBreak.Spend();
        DiagnosticLog.Info($"[LimitBreak] {ActionLookup.Name(actionId)} ({actionId}) resolved -- the gauge is spent.");
        var aim = Plugin.PlayerInputHooks.LimitBreakAimNow(world);
        Plugin.MultiplayerInstance?.ReportLimitBreak(actionId, aim);
        world.Party.LimitBreak.Land(actionId, aim);
    }
}
