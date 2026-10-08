namespace AnoMech.Core.Native.Interfaces;

// The Action-sheet columns the sim reads. Omen paths are the Omen sheet's raw Path (usually a bare
// name under vfx/omen/eff/); null when the action has none.
public sealed record ActionRow(
    uint Id,
    string Name,
    float CastSeconds,
    CastType CastType,
    byte EffectRange,
    byte XAxisModifier,
    string? OmenPath,
    string? OmenAltPath,
    uint ActionCategory,
    bool CanTargetSelf,
    bool CanTargetParty,
    sbyte AttackType);
