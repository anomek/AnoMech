namespace AnoMech.Core.Native.Interfaces;

public interface IInstanceContentDirector
{
    // The server's DirectorUpdate packet. False while there is no director yet; the caller retries.
    bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0);

    void SetDirectorData(byte sequence, byte unknown, byte[] unionData);

    void Commence();

    // The server's BattleTalk packet: a boss line (InstanceContentTextData textId) in the BattleTalk
    // box under BNpcName speakerNameId, voiced, for durationMs. False while there is no director
    // yet; the caller retries.
    bool BattleTalk(uint speakerNameId, uint textId, uint durationMs);
}
