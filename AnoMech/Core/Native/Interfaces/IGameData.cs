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

    // Null for a missing row or an empty name.
    string? StatusName(ushort statusId);
    string? BNpcName(uint nameId);

    bool StatusLocksMovement(ushort statusId);
    bool StatusLocksActions(ushort statusId);

    bool FileExists(string path);
}
