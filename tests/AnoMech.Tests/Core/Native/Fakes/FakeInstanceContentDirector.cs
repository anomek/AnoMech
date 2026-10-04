using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// The director exists while a zone is loaded.
internal sealed class FakeInstanceContentDirector(FakeZoneSession zone) : IInstanceContentDirector
{
    public bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
        => zone.IsActive;

    public void SetDirectorData(byte sequence, byte unknown, byte[] unionData, bool fillExtraData = true) { }
    public void Commence() { }
    public bool BattleTalk(uint speakerNameId, uint textId, uint durationMs) => zone.IsActive;
}
