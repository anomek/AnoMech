using System.Collections.Generic;

namespace AnoMech.Core.Native.Interfaces;

// The zone's MapEffect slots (the server's type-257 packets). Apply and SuppressSlot return
// false while the slot's SharedGroup hasn't streamed in yet; the caller retries.
public interface IMapEffects
{
    // While false, Apply refuses: nothing outside a sim may reach the native path.
    bool Loaded { get; set; }

    bool Apply(uint packetFlags, byte index);

    // The slot's current state; 0 until something sets it after the territory loads, null while
    // not Loaded.
    ushort? StateOf(byte index);

    // Hard-deactivates the slot (geometry, VFX and sound); KeepSlotSuppressed re-silences the
    // sound children the engine turns back on.
    bool SuppressSlot(byte index);
    void KeepSlotSuppressed(byte index);
    void RestoreSlot(byte index);
    IReadOnlyCollection<byte> SuppressedSlots { get; }
    void ForgetSuppressions();

    void LogAllSlots(string label);
}
