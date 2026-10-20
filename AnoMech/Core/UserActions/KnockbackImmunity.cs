using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// The statuses that negate a knockback the action lets them block: the two role
// actions', and Inner Strength from a Warrior's Inner Release.
public static class KnockbackImmunity
{
    public const uint ArmsLengthActionId = 7548;
    public const uint SurecastActionId = 7559;
    public const ushort ArmsLength = 1209;
    public const ushort Surecast = 160;
    public const ushort InnerStrength = 2663;

    public static bool IsImmunity(ushort statusId) => statusId is ArmsLength or Surecast or InnerStrength;

    public static bool Has(SimCharacter character)
        => character.FindStatus(ArmsLength) is not null || character.FindStatus(Surecast) is not null
           || character.FindStatus(InnerStrength) is not null;

    // Healers and casters have Surecast; every other job has Arm's Length.
    public static uint ActionId(byte classJob) => (JobId)classJob switch
    {
        JobId.Conjurer or JobId.WhiteMage or JobId.Scholar or JobId.Astrologian or JobId.Sage
            or JobId.Thaumaturge or JobId.BlackMage or JobId.Arcanist or JobId.Summoner
            or JobId.RedMage or JobId.BlueMage or JobId.Pictomancer => SurecastActionId,
        _ => ArmsLengthActionId,
    };
}
