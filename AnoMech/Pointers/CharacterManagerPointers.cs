using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Pointers;

internal unsafe class CharacterManagerPointers
{
    [Signature("48 8B C4 44 89 48 20 44 89 40 18 57", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static CreateCharacterAtIndexDelegate CreateCharacterAtIndex { get; private set; } = null!;

    [Signature("83 FA 64 0F 8D ?? ?? ?? ?? 48", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static DeleteCharacterAtIndexDelegate DeleteCharacterAtIndex { get; private set; } = null!;

    public delegate Character* CreateCharacterAtIndexDelegate(CharacterManager* thisPtr, uint entityId, int index, uint layoutId);
    public delegate void DeleteCharacterAtIndexDelegate(CharacterManager* thisPtr, int index);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new CharacterManagerPointers());
    }
}
