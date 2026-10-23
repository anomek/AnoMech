namespace AnoMech.Core.Native.Interfaces;

// Excel sheets and game files. Rows are our own records, not Lumina's: tests have no game files.
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

    // Null for a missing row or an empty name.
    string? BNpcName(uint nameId);

    bool WeatherExists(byte weatherId);

    // Esuna can remove it.
    bool CanDispel(ushort statusId);

    bool FileExists(string path);
}

public static class GameDataExtensions
{
    // Falls back to the raw id so a message naming the row never goes blank.
    public static string ActionName(this IGameData data, uint actionId)
        => data.Action(actionId) is { Name.Length: > 0 } row ? row.Name : actionId.ToString();

    public static string StatusName(this IGameData data, ushort statusId)
        => data.Status(statusId) is { Name.Length: > 0 } row ? row.Name : statusId.ToString();

    // The level the duty syncs everyone to.
    public static byte? DutyLevel(this IGameData data, uint territoryId)
        => data.TerritoryType(territoryId) is { ContentFinderCondition: not 0 } territory
           && data.ContentFinderCondition(territory.ContentFinderCondition) is { ClassJobLevelSync: not 0 } duty
            ? duty.ClassJobLevelSync
            : null;
}
