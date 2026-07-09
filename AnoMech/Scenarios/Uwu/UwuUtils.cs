using AnoMech.Helpers;

namespace AnoMech.Scenarios.Uwu;

public static class UwuUtils
{
    public static void UpdateArena(byte value)
    {
        byte[] unionData = [value];
        InstanceContentDirectorHelper.SetDirectorData(1, 0, unionData, true);
    }
}
