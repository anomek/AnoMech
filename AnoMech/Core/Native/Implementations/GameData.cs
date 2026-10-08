using AnoMech.Core.Native.Interfaces;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaBNpcBase = Lumina.Excel.Sheets.BNpcBase;
using LuminaBNpcName = Lumina.Excel.Sheets.BNpcName;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaKnockback = Lumina.Excel.Sheets.Knockback;
using LuminaModelChara = Lumina.Excel.Sheets.ModelChara;
using LuminaModelSkeleton = Lumina.Excel.Sheets.ModelSkeleton;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace AnoMech.Core.Native.Implementations;

internal sealed class GameData : IGameData
{
    public ActionRow? Action(uint actionId)
    {
        if (!Plugin.DataManager.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row)) return null;
        return new ActionRow(
            actionId,
            row.Name.ExtractText(),
            row.Cast100ms / 10f,
            (CastType)row.CastType,
            row.EffectRange,
            row.XAxisModifier,
            row.Omen.ValueNullable is { RowId: not 0 } omen ? omen.Path.ToString() : null,
            row.OmenAlt.ValueNullable is { RowId: not 0 } omenAlt ? omenAlt.Path.ToString() : null,
            row.ActionCategory.RowId,
            row.CanTargetSelf,
            row.CanTargetParty);
    }

    public KnockbackRow? Knockback(uint knockbackId)
        => Plugin.DataManager.GetExcelSheet<LuminaKnockback>().TryGetRow(knockbackId, out var row)
            ? new KnockbackRow(knockbackId, row.Distance, row.Speed)
            : null;

    public ClassJobRow? ClassJob(uint classJobId)
        => Plugin.DataManager.GetExcelSheet<LuminaClassJob>().TryGetRow(classJobId, out var row)
            ? new ClassJobRow(classJobId, row.LimitBreak1.RowId, row.LimitBreak2.RowId, row.LimitBreak3.RowId)
            : null;

    public BNpcBaseRow? BNpcBase(uint bnpcBaseId)
        => Plugin.DataManager.GetExcelSheet<LuminaBNpcBase>().TryGetRow(bnpcBaseId, out var row)
            ? new BNpcBaseRow(bnpcBaseId, row.Scale, row.ModelChara.RowId)
            : null;

    public ModelCharaRow? ModelChara(uint modelCharaId)
        => Plugin.DataManager.GetExcelSheet<LuminaModelChara>().TryGetRow(modelCharaId, out var row)
            ? new ModelCharaRow(modelCharaId, row.Type, row.Model, row.Unknown0)
            : null;

    public ModelSkeletonRow? ModelSkeleton(uint skeletonId)
        => Plugin.DataManager.GetExcelSheet<LuminaModelSkeleton>().TryGetRow(skeletonId, out var row)
            ? new ModelSkeletonRow(skeletonId, row.Radius)
            : null;

    public StatusRow? Status(ushort statusId)
        => Plugin.DataManager.GetExcelSheet<LuminaStatus>().TryGetRow(statusId, out var row)
            ? new StatusRow(statusId, row.LockMovement, row.LockActions, row.LockControl)
            : null;

    public string? StatusName(ushort statusId)
        => Plugin.DataManager.GetExcelSheet<LuminaStatus>().TryGetRow(statusId, out var row) ? NonEmpty(row.Name.ExtractText()) : null;

    public string? BNpcName(uint nameId)
        => Plugin.DataManager.GetExcelSheet<LuminaBNpcName>().TryGetRow(nameId, out var row) ? NonEmpty(row.Singular.ExtractText()) : null;

    public bool FileExists(string path) => Plugin.DataManager.FileExists(path);

    private static string? NonEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;
}
