using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core;

// Resolves an Action-sheet row id to its display name for user-facing messages.
internal static class ActionLookup
{
    // Falls back to the raw id so a death message never goes blank.
    public static string Name(uint actionId)
        => Natives.Data.Action(actionId) is { Name: { Length: > 0 } name } ? name : actionId.ToString();

    // The sheet name as-is, blank for an unnamed row (enemy auto-attacks): flytext labels mirror
    // what the game prints.
    public static string FlyTextName(uint actionId) => Natives.Data.Action(actionId)?.Name ?? "";
}
