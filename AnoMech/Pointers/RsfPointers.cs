using Dalamud.Utility.Signatures;

namespace AnoMech.Pointers;

// Native RsfReceive — the file-level twin of RSV (LayoutWorld::AddRsvString). RSF
// resolves obfuscated model / VFX / sound *file paths* for duty content the shipped
// game files hide: the server sends only the first 64 bytes of each locked file's
// zlib stream at zone-in, and RsfReceive stitches it onto the client-held tail (see
// xiv.dev/game-internals/rsf). FFXIVClientStructs doesn't bind it, so we sig-scan.
//
// Signature from UnknownX7/ARealmRecorded (ReplayPacketManager.RSFPacket). It is short
// and therefore build-sensitive: the property is nullable so a post-patch miss degrades
// gracefully (RSF seeding is skipped, RSV still works) instead of throwing at load.
internal unsafe class RsfPointers
{
    [Signature("48 8B 11 4C 8D 41 08", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static RsfReceiveDelegate? RsfReceive { get; private set; }

    // data -> the 0x48-byte record the function consumes; returns a bool (1 byte).
    public delegate byte RsfReceiveDelegate(byte* data);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new RsfPointers());
    }
}
