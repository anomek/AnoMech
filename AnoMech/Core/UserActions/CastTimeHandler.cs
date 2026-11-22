using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Instant-cast enablers for the player's own spells. Cast time is resolved client-side, so for
// Swiftcast/Acceleration granting the status is enough for the client to make the next cast instant.
// The client ignores a granted Dualcast status, so under it the handler answers the client's cast-time
// question itself (AdjustCastTime): 0 for a spell. Driving the cast directly instead glitched the UI.
//
// The server-driven status removal is firewalled, so we consume the enablers. Consumption differs:
//  - Swiftcast / Triplecast — consumed by the next cast-time spell (priority below Dualcast; skipped
//    when Acceleration/Firestarter/Thunderhead already made the cast instant).
//  - Dualcast — consumed by executing ANY action except an ability (oGCD, ActionCategory 4): instant
//    spells, weaponskills, items, Sprint, etc. all waste it. The one exception is a cast-time spell
//    that Acceleration reduces to instant — then Acceleration is consumed and Dualcast is preserved.
//    (Odd, but this is how the game behaves.)
//  - Acceleration / Firestarter / Thunderhead — granted+consumed by the JobActions table.
//
// A press is handled when it happens: OnCastStart for a hard cast, OnAction for an instant one.
// Dualcast is granted when a genuine RDM hard cast resolves, so never for an interrupted one. Runs
// before JobActionHandler so the spell-specific statuses are still present when we check them.
internal sealed class CastTimeHandler : IUserActionHandler
{
    private const ushort SwiftcastStatus = 167, DualcastStatus = 1393, TriplecastStatus = 1211;
    private const ushort Acceleration = 1238, Firestarter = 165, Thunderhead = 3870;
    private const uint SwiftcastAction = 7561, TriplecastAction = 7421, Foul = 7422;

    // The hard cast in progress (0 = none), and whether resolving it grants Dualcast.
    private uint castAction;
    private bool castGrantsDualcast;

    public void OnScenarioStart() => castAction = 0;

    public void OnCastStart(ActionType actionType, uint actionId)
    {
        castAction = actionId;
        castGrantsDualcast = OnPress(actionType, actionId);
    }

    public void OnCastInterrupted(ActionType actionType, uint actionId) => castAction = 0;

    public int? AdjustCastTime(ActionType actionType, uint actionId)
        => actionType == ActionType.Action && Plugin.Game?.Player is { } player && player.HasStatus(DualcastStatus)
           && IsCastTimeSpell(actionId) ? 0 : null;

    private static bool IsCastTimeSpell(uint actionId)
        => Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var row)
           && row.ActionCategory.RowId == 2 && row.Cast100ms > 0 && actionId != Foul;

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (castAction != 0 && actionId == castAction)
        {
            castAction = 0;
            if (castGrantsDualcast && Plugin.Game?.Player is { } player)
            {
                player.RemoveStatus(DualcastStatus);
                player.AddStatusParam(DualcastStatus, 0, 15f);
            }
            return;
        }
        OnPress(actionType, actionId);
    }

    // Grants and consumes the enablers; returns whether the action is a cast-time spell whose
    // completion would grant Dualcast.
    private static bool OnPress(ActionType actionType, uint actionId)
    {
        var player = Plugin.Game?.Player;
        if (player == null) return false;

        // Grant Swiftcast / Triplecast (both abilities → they don't consume Dualcast, so return).
        if (actionType == ActionType.Action && actionId == SwiftcastAction) { player.RemoveStatus(SwiftcastStatus); player.AddStatusParam(SwiftcastStatus, 0, 10f); return false; }
        if (actionType == ActionType.Action && actionId == TriplecastAction) { player.RemoveStatus(TriplecastStatus); player.AddStatusParam(TriplecastStatus, 3, 15f); return false; }

        // Classify the executed action. Anything that isn't a category-4 ability (spell, weaponskill,
        // item, Sprint, …) consumes Dualcast.
        bool isAbility = false, isCastTimeSpell = false;
        if (actionType == ActionType.Action)
        {
            var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet.TryGetRow(actionId, out var row))
            {
                var cat = row.ActionCategory.RowId;
                isAbility = cat == 4;
                isCastTimeSpell = cat == 2 && row.Cast100ms > 0 && actionId != Foul;
            }
        }
        bool accelReduced = isCastTimeSpell && player.HasStatus(Acceleration) && actionId is 25855 or 25856 or 16526;

        // Dualcast: any non-ability action consumes it, except an Acceleration-reduced cast.
        bool dualcastConsumed = false;
        if (player.HasStatus(DualcastStatus) && !isAbility && !accelReduced)
        {
            player.RemoveStatus(DualcastStatus);
            dualcastConsumed = true;
        }

        // Swiftcast / Triplecast: the cast-time spell they make instant consumes one — but Dualcast wins
        // the priority, and a spell-specific status (Acceleration/Firestarter/Thunderhead) already took it.
        if (isCastTimeSpell && !dualcastConsumed && !SpellSpecific(player, actionId))
        {
            if (player.HasStatus(TriplecastStatus)) player.AddStatus(TriplecastStatus, 0f, -1);
            else if (player.HasStatus(SwiftcastStatus)) player.RemoveStatus(SwiftcastStatus);
        }

        return isCastTimeSpell && !dualcastConsumed && PlayerJob.Current == JobId.RedMage;
    }

    // A spell-specific instant status the player holds that applies to this spell (Acceleration →
    // Verthunder III/Veraero III/Impact; Firestarter → Fire III; Thunderhead → the Thunder line).
    private static bool SpellSpecific(SimPlayer p, uint actionId)
    {
        if (p.HasStatus(Acceleration) && actionId is 25855 or 25856 or 16526) return true;
        if (p.HasStatus(Firestarter) && actionId == 152) return true;
        if (p.HasStatus(Thunderhead) && actionId is 144 or 7447 or 153 or 7420 or 36986 or 36987) return true;
        return false;
    }
}
