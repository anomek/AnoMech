using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeMapEffects : IMapEffects
{
    public bool Loaded { get; set; }

    private readonly Dictionary<byte, ushort> states = new();

    public bool Apply(uint packetFlags, byte index)
    {
        if (Loaded) states[index] = (ushort)packetFlags;
        return Loaded;
    }

    public ushort? StateOf(byte index) => Loaded ? states.GetValueOrDefault(index) : null;
    public bool SuppressSlot(byte index) => Loaded;
    public void KeepSlotSuppressed(byte index) { }
    public void RestoreSlot(byte index) { }
    public IReadOnlyCollection<byte> SuppressedSlots => [];
    public void ForgetSuppressions() { }
    public void LogAllSlots(string label) { }
}
