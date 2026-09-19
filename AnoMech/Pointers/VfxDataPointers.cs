using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;

namespace AnoMech.Pointers;

internal unsafe class VfxDataPointers
{
    // Resource-instance trigger queue (2026.09.15.0000.0000: RVA 0x38F5E0).
    // The signature resolves the entry point; the RVA is only a verification note.
    // Do NOT bind the low-level Apricot timeline constructor as taking a scene VfxObject*.
    [Signature("48 89 5C 24 10 57 48 83 EC 20 48 8B D9 89 91 ?? ?? ?? ?? 8B CA B8 01 00 00 00 D3 E0", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static QueueResourceTriggerDelegate QueueResourceTrigger { get; private set; } = null!;

    // The queue is void and takes a one-based trigger number, not a timeline index.
    public delegate void QueueResourceTriggerDelegate(VfxResourceInstance* resource, uint triggerNumber);

    [Signature("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static CreateActorCharacterVfxDelegate ActorVfxCreate { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 8B FA 48 8D 05 ?? ?? ?? ?? 48 89 81 ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 09", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static DtorDelegate Dtor { get; private set; } = null!;

    public delegate VfxData* CreateActorCharacterVfxDelegate(byte* path, GameObject* caster, GameObject* target, float a4, byte a5, ushort a6, byte a7);
    public delegate VfxData* DtorDelegate(VfxData* thisPtr, byte a2);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new VfxDataPointers());
    }
}
