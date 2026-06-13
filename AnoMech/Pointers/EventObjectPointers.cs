using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Pointers;

internal unsafe class EventObjectPointers
{
    [Signature("E8 ?? ?? ?? ?? 49 8B 06 44 0F B6 C5", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetEventObjectStateDelegate SetEventObjectState { get; private set; } = null!;

    // State-change driver. Writes `actor[0x1B2] = state` and notifies the
    // attached SharedGroupLayoutInstance (actor[0x108]) so its sub-instances
    // can switch visible content according to the new state. Used by the
    // packet handler with packet[0x2c] (the TimelineId / state field).
    // If actor[0x108] is null (SG context not attached yet), the function
    // only writes the state field and skips the notify — engine populates
    // actor[0x108] asynchronously after the SG resource loads, so a later
    // call will propagate.
    public delegate void SetEventObjectStateDelegate(EventObject* thisPtr, ushort state, byte immediate, ulong extra);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventObjectPointers());
    }
}
