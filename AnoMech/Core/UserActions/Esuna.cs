namespace AnoMech.Core.UserActions;

// The healers' cleanse (DispelEffect).
public static class Esuna
{
    public const uint ActionId = 7568;

    public static bool KnownBy(byte classJob)
        => (JobId)classJob is JobId.Conjurer or JobId.WhiteMage or JobId.Scholar or JobId.Astrologian or JobId.Sage;

    // Esuna, or a Bard's The Warden's Paean.
    public static bool CanCleanse(byte classJob) => KnownBy(classJob) || (JobId)classJob is JobId.Bard;
}
