using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native.Implementations.Pointers;

internal unsafe class TimelineContainerPointers
{
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 8B FA 4C 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetAnimationStateDelegate SetAnimationState { get; private set; } = null!;

    public delegate void SetAnimationStateDelegate(TimelineContainer* thisPtr, int a2, int a3);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new TimelineContainerPointers());
    }
}
