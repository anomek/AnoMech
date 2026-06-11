using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;

namespace AnoMech.Services;

internal unsafe class VfxDataService
{
    [Signature("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static CreateActorCharacterVfxDelegate ActorVfxCreate { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 8B FA 48 8D 05 ?? ?? ?? ?? 48 89 81 ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 09", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static DtorDelegate Dtor { get; private set; } = null!;

    public delegate VfxData* CreateActorCharacterVfxDelegate(byte* path, GameObject* caster, GameObject* target, float a4, byte a5, ushort a6, byte a7);
    public delegate VfxData* DtorDelegate(VfxData* thisPtr, byte a2);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new VfxDataService());
    }
}
