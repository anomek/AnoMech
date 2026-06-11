using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Pointers;

internal unsafe class EventObjectManagerPointers
{
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 57 41 54 41 57 48 83 EC ?? 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static CreateEventObjectDelegate CreateEventObject { get; private set; } = null!;

    public delegate int CreateEventObjectDelegate(EventObjectManager* thisPtr, uint entityId, uint eObjId, ulong a4, uint layoutId, uint gimmickId, int objectIndex, byte flag);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventObjectManagerPointers());
    }
}
