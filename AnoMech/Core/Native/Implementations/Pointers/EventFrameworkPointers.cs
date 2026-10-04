using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace AnoMech.Core.Native.Implementations.Pointers;

internal unsafe class EventFrameworkPointers
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 54 24 70 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static InitDirectorDelegate InitDirector { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 70 48 8D B1", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static TerminateDirectorDelegate TerminateDirector { get; private set; } = null!;

    [Signature("89 54 24 10 48 89 4C 24 ?? 53 56 57 41 55 41 57 48 83 EC 30 48 8B 99", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static SetDirectorDataDelegate SetDirectorData { get; private set; } = null!;

    // Where the BattleTalk packet lands: shows the text box and plays the voice through the
    // handler's own BattleTalk. speakerKind is the speaker's ObjectKind, which picks the sheet
    // speakerNameId is read from (2 = BNpcName). Nullable so a patch that moves it costs the
    // lines, not the plugin.
    [Signature("40 53 55 56 48 81 EC C0 00 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 B0 00 00 00 F7 05 ?? ?? ?? ?? FD FF FF FF", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static ProcessBattleTalkDelegate? ProcessBattleTalk { get; private set; }

    public delegate void InitDirectorDelegate(EventFramework* thisPtr, EventId eventId, uint contentId, uint flags);
    public delegate void TerminateDirectorDelegate(EventFramework* thisPtr, EventId eventId);
    public delegate void SetDirectorDataDelegate(EventFramework* thisPtr, EventId eventId, byte sequence, byte unknown, byte* unionDataBuffer, ulong length);
    public delegate void ProcessBattleTalkDelegate(
        EventFramework* thisPtr, EventId handlerId, ulong speakerEntityId, byte speakerKind, uint speakerNameId,
        uint textId, uint durationMs, uint unknown1, uint unknown2, uint* textArgs, byte textArgCount);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new EventFrameworkPointers());
    }
}
