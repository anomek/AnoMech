using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core;

// A Knockback-sheet row's Distance/Speed pair.
internal static class KnockbackLookup
{
    public static bool TryGet(uint knockbackId, out float distance, out float speed)
    {
        if (Natives.Data.Knockback(knockbackId) is not { } row)
        {
            Plugin.Log.Warning($"KnockbackLookup: Knockback row {knockbackId} missing in sheet");
            distance = 0f;
            speed = 0f;
            return false;
        }
        distance = row.Distance;
        speed = row.Speed;
        return true;
    }
}
