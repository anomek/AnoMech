using System.Numerics;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// Starts in the inn; Enter loads at once and puts the player at the spawn, Revert puts them back.
internal sealed class FakeZoneControl(FakeCharacter player, SimTimeline timeline) : IZoneControl
{
    private Vector3 innPosition;
    private float innRotation;

    public bool IsActive { get; private set; }

    public bool IsReady(bool mapEffects) => IsActive;

    public bool IsInInn() => !IsActive;

    public string? StartBlockedReason(out string? settling)
    {
        settling = null;
        return null;
    }

    public void EndSpeedBuffs() { }

    public bool Enter(uint territoryId, Vector3 playerSpawn)
    {
        innPosition = player.Position;
        innRotation = player.Rotation;
        player.Position = playerSpawn;
        IsActive = true;
        return true;
    }

    public void Revert()
    {
        if (!IsActive) return;
        player.Position = innPosition;
        player.Rotation = innRotation;
        IsActive = false;
    }

    public void ApplyWeather(byte weatherId) => timeline.Add("weather", id: weatherId.ToString(), name: "Weather", detail: "apply");
    public void SetWeather(byte weatherId, float transition = 0.5f)
        => timeline.Add("weather", id: weatherId.ToString(), name: "Weather", detail: $"transition={SimTimeline.F(transition, "F1")}");
    public void TickWeather() { }
    public void TickGuard() { }
    public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what) => false;
    public void HoldSendFirewall(bool hold) { }
    public uint MaxActionEffectCounter => 0;

    public void Dispose() { }
}
