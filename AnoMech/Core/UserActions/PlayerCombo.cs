using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// How using an action participates in the player's linear combo right now.
internal enum ComboRole { None, Starter, Continuation }

// Queries the player's live linear-combo state (ActionManager.Combo) against the Action
// sheet's ActionCombo chain. Shared by the combo and resource-gen handlers (resource
// gen is combo-gated). Reads the PRE-advance state, so callers that also advance the
// combo (ActionEffectHandler) must query before firing. Combo routes (Gnashing Fang etc.) use
// a separate system and aren't covered.
//
// All id comparisons go through GetAdjustedActionId: an action that upgrades with level
// (Hakaze->Gyofu, Split Shot->Heated Split Shot) is pressed as the upgraded id, but the
// sheet records combos against the base id — adjusting both sides makes them match
// regardless of which form the game stored, so upgraded chains combo like any other.
internal static unsafe class PlayerCombo
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

    // Actions the sheet doesn't chain but the game combos as another: RDM's enchanted melee, whose swap
    // is gone by the time the press resolves, having spent the mana it read, and Verholy, which leads
    // into Scorch as Verflare does though the sheet names only Verflare.
    private static readonly Dictionary<uint, uint> ComboForms = new()
    {
        [7527] = 7504, [45960] = 7504, // Enchanted Riposte
        [7528] = 7512, [45961] = 7512, // Enchanted Zwerchhau
        [7529] = 7516, [45962] = 7516, // Enchanted Redoublement
        [7526] = 7525,                 // Verholy
    };

    private static uint ComboId(uint actionId) => ComboForms.GetValueOrDefault(actionId, actionId);

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
        var am = ActionManager.Instance();
        if (am == null) return ComboRole.None;

        actionId = ComboId(actionId);
        var prereq = PrerequisiteOf(actionId);
        if (prereq != 0)
            return am->Combo.Timer > 0f && Same(am, am->Combo.Action, prereq)
                ? ComboRole.Continuation : ComboRole.None;

        if (Starters.Contains(actionId)) return ComboRole.Starter;
        var adjusted = am->GetAdjustedActionId(actionId);
        foreach (var starter in Starters)
            if (am->GetAdjustedActionId(starter) == adjusted) return ComboRole.Starter;
        return ComboRole.None;
    }

    // True while using `actionId` correctly continues the active combo (gets its bonus).
    public static bool IsActiveContinuation(uint actionId) => RoleOf(actionId) == ComboRole.Continuation;

    // Whether two action ids denote the same action once level upgrades are applied.
    private static bool Same(ActionManager* am, uint a, uint b)
        => ComboId(a) == ComboId(b) || am->GetAdjustedActionId(a) == am->GetAdjustedActionId(b);
}
