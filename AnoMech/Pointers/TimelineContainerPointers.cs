using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Pointers;

internal unsafe class TimelineContainerPointers
{
    [Signature("E8 ?? ?? ?? ?? 8B D5 48 8D 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetModelStateDelegate SetModelState { get; private set; } = null!;

    public delegate void SetModelStateDelegate(TimelineContainer* self, uint modelStateId);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new TimelineContainerPointers());
    }
}
