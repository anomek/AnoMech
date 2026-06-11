using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Pointers;

internal unsafe class VfxContainerPointers
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 0F B6 54 24 ?? 45", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetTetherDelegate SetTether { get; private set; } = null!;

    public delegate void SetTetherDelegate(VfxContainer* thisPtr, byte tetherIndex, ushort tetherId, ulong targetId, byte tetherProgress);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new VfxContainerPointers());
    }
}
