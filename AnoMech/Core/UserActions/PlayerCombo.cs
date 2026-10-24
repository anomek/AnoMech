using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.UserActions;

// How using an action participates in the player's linear combo right now.
internal enum ComboRole { None, Starter, Continuation }

// Queries the player's live linear-combo state (ActionManager.Combo) against the Action
// sheet's ActionCombo chain. Shared by the combo and resource-gen handlers (resource
// gen is combo-gated). Reads the PRE-advance state, so callers that also advance the
// combo (ComboHandler) must query before firing. Combo routes (Gnashing Fang etc.) use
// a separate system and aren't covered.
//
// All id comparisons go through GetAdjustedActionId: an action that upgrades with level
// (Hakaze->Gyofu, Split Shot->Heated Split Shot) is pressed as the upgraded id, but the
// sheet records combos against the base id — adjusting both sides makes them match
// regardless of which form the game stored, so upgraded chains combo like any other.
internal static class PlayerCombo
{
    // Actions that are the prerequisite of some action's combo — i.e. combo starters
    // and middles. Built once from the sheet (base ids; upgrades resolved at compare time).
    private static HashSet<uint>? starters;
    private static HashSet<uint> Starters => starters ??= BuildStarters();

    private static HashSet<uint> BuildStarters()
    {
        var set = new HashSet<uint>();
        foreach (var row in Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>())
        {
            var prereq = row.ActionCombo.RowId;
            if (prereq != 0) set.Add(prereq);
        }
        return set;
    }

    // The action `actionId` combos from (0 = it isn't a continuation).
    private static uint PrerequisiteOf(uint actionId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        return sheet.TryGetRow(actionId, out var row) ? row.ActionCombo.RowId : 0;
    }

    // Classifies how using `actionId` now participates in a linear combo: Continuation
    // (correctly follows the active combo, so it gets its bonus), Starter (begins one),
    // or None (not a combo action, or a continuation used out of order → doesn't advance).
    public static ComboRole RoleOf(uint actionId)
    {
        var player = Natives.PlayerActionState;
        var prereq = PrerequisiteOf(actionId);
        if (prereq != 0)
            return player.ComboTimer > 0f && Same(player, player.ComboAction, prereq)
                ? ComboRole.Continuation : ComboRole.None;

        if (Starters.Contains(actionId)) return ComboRole.Starter;
        var adjusted = player.AdjustedActionId(actionId);
        foreach (var starter in Starters)
            if (player.AdjustedActionId(starter) == adjusted) return ComboRole.Starter;
        return ComboRole.None;
    }

    // True while using `actionId` correctly continues the active combo (gets its bonus).
    public static bool IsActiveContinuation(uint actionId)
        => Natives.PlayerActionState.ComboTimer > 0f && RoleOf(actionId) == ComboRole.Continuation;

    // Whether two action ids denote the same action once level upgrades are applied.
    private static bool Same(IPlayerActionState player, uint a, uint b)
        => a == b || player.AdjustedActionId(a) == player.AdjustedActionId(b);
}
