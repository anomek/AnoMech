using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Helpers;

internal static unsafe class InstanceContentDirectorHelper
{
    public static void ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        var eventFramework = EventFramework.Instance();
        var director = eventFramework->GetInstanceContentDirector();

        if (eventFramework == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.Commence] EventFramework.Instance() was null");
            return;
        }

        if (director == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.Commence] eventFramework->GetInstanceContentDirector() was null");
            return;
        }

        var eventId = director->GetEventId();
        eventFramework->ProcessDirectorUpdate(eventId, category, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    // Fire the three DirectorUpdate events the server sends at instance Commence
    // Note: these IDs/params are TOP-specific — other content fires different values
    // (e.g. T=1045 uses 40000007 / 40000001-E10 / 80000004-E0F). Future work to parameterize.
    public static void Commence()
    {
        ProcessDirectorUpdate(0x4000000C);
        ProcessDirectorUpdate(0x40000001, 0x1C20);
        ProcessDirectorUpdate(0x80000004, 0x1C1F);
    }
}
