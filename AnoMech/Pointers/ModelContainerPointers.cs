using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Pointers;

internal unsafe class ModelContainerPointers
{
    [Signature("E8 ?? ?? ?? ?? 49 8B 06 44 0F B6 C5", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static CalculateUnscaledRadiusDelegate CalculateUnscaledRadius { get; private set; } = null!;

    public delegate float CalculateUnscaledRadiusDelegate(ModelContainer* modelContainer);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new ModelContainerPointers());
    }
}
