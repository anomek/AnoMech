using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// The director exists while a zone is loaded.
internal sealed class FakeInstanceContentDirector(FakeZoneControl zone, SimTimeline timeline) : IInstanceContentDirector
{
    public bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        if (zone.IsActive)
            timeline.Add("director", id: $"0x{category:X}", detail: $"values={SimTimeline.Params(arg1, arg2, arg3, arg4, arg5, arg6)}",
                p: SimTimeline.Params(category, arg1, arg2, arg3, arg4, arg5, arg6));
        return zone.IsActive;
    }

    public void SetDirectorData(byte sequence, byte unknown, byte[] unionData)
        => timeline.Add("director_data", id: sequence.ToString(), detail: $"unknown={unknown} data={Convert.ToHexString(unionData)}");

    public void Commence() => timeline.Add("commence");

    public bool BattleTalk(uint speakerNameId, uint textId, uint durationMs)
    {
        if (zone.IsActive)
            timeline.Add("battle_talk", id: textId.ToString(), name: "BattleTalk", detail: $"speaker={speakerNameId} duration_ms={durationMs}");
        return zone.IsActive;
    }
}
