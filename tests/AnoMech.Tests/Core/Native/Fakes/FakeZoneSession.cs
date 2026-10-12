using System.Numerics;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// Starts in the inn; Enter loads at once and puts the player at the spawn, Revert puts them back.
internal sealed class FakeZoneSession(FakeCharacter player) : IZoneSession
{
    private Vector3 innPosition;
    private float innRotation;

    public bool IsActive { get; private set; }

    public bool IsInInn() => !IsActive;

    public string? StartBlockedReason(out string? settling)
    {
        settling = null;
        return null;
    }

    public void EndSpeedBuffs() { }

    public bool Enter(uint territoryId, Vector3 playerSpawn, byte levelSync, ushort itemLevelSync)
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

    public void ApplyWeather(byte weatherId) { }
    public void SetWeather(byte weatherId, float transition = 0.5f) { }
    public void TickWeather() { }
    public float? FogHold { get; set; }
    public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what) => false;
    public void HoldSendFirewall(bool hold) { }
    public uint MaxActionEffectCounter => 0;
}
