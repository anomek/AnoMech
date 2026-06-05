using Lumina.Excel;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace AnoMech.Core;

// Resolves an Action-sheet row id to its display name for user-facing messages.
internal static class ActionLookup
{
    private static readonly ExcelSheet<LuminaAction> Sheet =
        Plugin.DataManager.GetExcelSheet<LuminaAction>();

    // Action name for `actionId`, or the raw id as a fallback when the row is
    // missing or unnamed — death messages must never go blank.
    public static string Name(uint actionId)
    {
        if (Sheet.TryGetRow(actionId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return actionId.ToString();
    }
}
