using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Services;

internal unsafe class EventFrameworkService
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 54 24 70 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static InitDirectorDelegate InitDirector { get; private set; } = null!;

    public delegate void InitDirectorDelegate(EventFramework* thisPtr, EventId eventId, uint contentId, uint flags);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventFrameworkService());;
    }
}
