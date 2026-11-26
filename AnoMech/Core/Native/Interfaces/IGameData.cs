namespace AnoMech.Core.Native.Interfaces;

// Excel sheets and game files. Rows are our own records, not Lumina's: tests have no game files.
// A new column of a sheet that already has a row record goes on that record, not on a new
// per-column method here.
public interface IGameData
{
    ActionRow? Action(uint actionId);
    KnockbackRow? Knockback(uint knockbackId);
    ClassJobRow? ClassJob(uint classJobId);
    BNpcBaseRow? BNpcBase(uint bnpcBaseId);
    ModelCharaRow? ModelChara(uint modelCharaId);
    ModelSkeletonRow? ModelSkeleton(uint skeletonId);
    StatusRow? Status(ushort statusId);
    TerritoryTypeRow? TerritoryType(uint territoryId);
    ContentFinderConditionRow? ContentFinderCondition(uint contentFinderConditionId);
    bool ClassJobCategoryIncludes(uint classJobCategoryId, byte classJob);

    // Null for a missing row or an empty name.
    string? BNpcName(uint nameId);

    bool WeatherExists(byte weatherId);

    bool FileExists(string path);
}

public static class GameDataExtensions
{
    // Falls back to the raw id so a message naming the row never goes blank.
    public static string ActionName(this IGameData data, uint actionId)
        => data.Action(actionId) is { Name.Length: > 0 } row ? row.Name : actionId.ToString();

    public static string StatusName(this IGameData data, ushort statusId)
        => data.Status(statusId) is { Name.Length: > 0 } row ? row.Name : statusId.ToString();

    public static bool CanUse(this IGameData data, byte classJob, uint actionId)
        => data.Action(actionId) is { } action && data.ClassJobCategoryIncludes(action.ClassJobCategory, classJob);

    // The level the duty syncs everyone to.
    public static byte? DutyLevel(this IGameData data, uint territoryId)
        => data.TerritoryType(territoryId) is { ContentFinderCondition: not 0 } territory
           && data.ContentFinderCondition(territory.ContentFinderCondition) is { ClassJobLevelSync: not 0 } duty
            ? duty.ClassJobLevelSync
            : null;
}
