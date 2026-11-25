using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler;

namespace AnoMech.Offline;

// GameMain's update hands the weather and the time of day on to the environment only while the
// game is connected to a zone server or plays a cutscene. Offline it never is, so a zone's or a
// scenario's weather never reached the sky or the layout (an arena that changes with the weather
// kept its default look) and the time of day stood still.
internal sealed unsafe class OfflineWeather
{
    // That check in GameMain's update, then WeatherManager's update (picks the weather source in
    // charge and hands its weather on) and its time-of-day update.
    private const string GateSignature = "80 BB ?? ?? ?? ?? 00 75 ?? 48 8B 05 ?? ?? ?? ?? 48 83 78 38 00 74 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8";
    private const int UpdateCallOffset = 30, TimeOfDayCallOffset = 42;

    private delegate void WeatherManagerDelegate(WeatherManager* manager);

    private readonly WeatherManagerDelegate? update, timeOfDay;

    public OfflineWeather()
    {
        if (!Plugin.SigScanner.TryScanText(GateSignature, out var gate)) return;
        update = Marshal.GetDelegateForFunctionPointer<WeatherManagerDelegate>(CallTarget(gate + UpdateCallOffset));
        timeOfDay = Marshal.GetDelegateForFunctionPointer<WeatherManagerDelegate>(CallTarget(gate + TimeOfDayCallOffset));
    }

    public bool Found => update != null && timeOfDay != null;

    private static nint CallTarget(nint call) => call + 5 + *(int*)(call + 1);

    // Every frame from the zone-in on, in exactly the frames the game skipped them.
    public void Tick()
    {
        if (!Found) return;
        var game = GameMain.Instance();
        if (game == null || game->ConnectedToZone) return;
        var schedule = ScheduleManagement.Instance();
        if (schedule != null && schedule->CutSceneController != null) return;
        var weather = WeatherManager.Instance();
        update!(weather);
        timeOfDay!(weather);
    }
}
