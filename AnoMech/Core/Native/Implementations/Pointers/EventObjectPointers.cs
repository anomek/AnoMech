using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native.Implementations.Pointers;

internal unsafe class EventObjectPointers
{
    [Signature("E8 ?? ?? ?? ?? 49 8B 06 44 0F B6 C5", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetEventObjectStateDelegate SetEventObjectState { get; private set; } = null!;

    // Writes actor[0x1B2] = state and, once the SharedGroup is attached, notifies it.
    public delegate void SetEventObjectStateDelegate(EventObject* thisPtr, ushort state, byte immediate, ulong extra);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventObjectPointers());
    }
}
