using System.Numerics;
using AnoMech.Core.Map;

namespace AnoMech.Scenarios.Dsr;

// Placeholder until a DSR replay is run through tools/replay_export_duty_init.py, which replaces
// this file. The barrier is the territory's LVD_zone_01 EObj and wall. UNVERIFIED: no MapEffect
// snapshot, RSV or RSF records yet, since only a replay carries them.
public static class DsrReplayData
{
    public static readonly DutyInitData DutyInitData = new(
        new EntranceBarrier(2007457, 0x875BE2, 0x875BE0, new Vector3(100f, 0f, 118f), 0f),
        new HelperPool(9020, 24),
        [],
        [],
        []);
}
