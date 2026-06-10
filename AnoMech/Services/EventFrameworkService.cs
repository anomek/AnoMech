using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Services;

internal unsafe class EventFrameworkService
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 54 24 70 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static InitDirectorDelegate InitDirector { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 70 48 8D B1", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static TerminateDirectorDelegate TerminateDirector { get; private set; } = null!;

    public delegate void InitDirectorDelegate(EventFramework* thisPtr, EventId eventId, uint contentId, uint flags);
    public delegate void TerminateDirectorDelegate(EventFramework* thisPtr, EventId eventId);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventFrameworkService());;
    }
}
