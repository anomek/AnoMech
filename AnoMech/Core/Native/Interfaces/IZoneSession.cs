using System;
using System.Numerics;

namespace AnoMech.Core.Native.Interfaces;

// The client-side zone a scenario runs in: entered from the inn, with the server firewalled off.
public interface IZoneSession
{
    bool IsInInn();

    // Why a scenario can't start now, or null. `settling` names a transient state worth waiting
    // out (null when the refusal is final).
    string? StartBlockedReason(out string? settling);

    // When Sprint, Peloton or Smudge is all a start waits on, ends it as a right-click on the buff would.
    void EndSpeedBuffs();

    bool IsActive { get; }

    // False when refused; nothing is loaded then.
    bool Enter(uint territoryId, Vector3 playerSpawn, byte levelSync, ushort itemLevelSync);

    // Back to the inn territory and position.
    void Revert();

    // ApplyWeather waits for the engine to settle after a fresh load; SetWeather applies now.
    void ApplyWeather(byte weatherId);
    void SetWeather(byte weatherId, float transition = 0.5f);
    void TickWeather();

    // Environment fog held each frame; null releases it.
    float? FogHold { get; set; }

    // A captured server packet, replayed through the client's own dispatcher. False when it could
    // not be delivered.
    bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what);

    // Outside a session, block every outbound packet but the heartbeat.
    void HoldSendFirewall(bool hold);

    // Highest ActionEffect global sequence the real server has sent this session.
    uint MaxActionEffectCounter { get; }
}
