using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

internal sealed class InstanceContentDirector : IInstanceContentDirector
{
    public bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
        => InstanceContentDirectorHelper.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6);

    public void SetDirectorData(byte sequence, byte unknown, byte[] unionData, bool fillExtraData = true)
        => InstanceContentDirectorHelper.SetDirectorData(sequence, unknown, unionData, fillExtraData);

    public void Commence() => InstanceContentDirectorHelper.Commence();

    public bool BattleTalk(uint speakerNameId, uint textId, uint durationMs)
        => InstanceContentDirectorHelper.BattleTalk(speakerNameId, textId, durationMs);
}
