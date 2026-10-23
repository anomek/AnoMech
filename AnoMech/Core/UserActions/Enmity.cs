namespace AnoMech.Core.UserActions;

// The tanks' Provoke (ProvokeEffect).
public static class Enmity
{
    public const uint ProvokeActionId = 7533;

    public static bool KnowsProvoke(byte classJob)
        => (JobId)classJob is JobId.Gladiator or JobId.Paladin or JobId.Marauder or JobId.Warrior or JobId.DarkKnight or JobId.Gunbreaker;
}
