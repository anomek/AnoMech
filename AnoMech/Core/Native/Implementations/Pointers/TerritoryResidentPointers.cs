using Dalamud.Utility.Signatures;

namespace AnoMech.Core.Native.Implementations.Pointers;

// The client's per-zone preloader. RequestTerritory looks up TerritoryType.Resident and loads that
// Resident-sheet row (the zone's boss timelines, VFX, omens, ...), unloading the previous zone's.
internal unsafe class TerritoryResidentPointers
{
    [Signature("40 57 48 83 EC 20 48 8B 05 ?? ?? ?? ?? 48 8B F9 44 8B C2 BA 5E 00 00 00", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static RequestTerritoryDelegate RequestTerritory = null!;

    public delegate void RequestTerritoryDelegate(nint preloader, uint territoryTypeId);

    public static nint Preloader { get; private set; }

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new TerritoryResidentPointers());
        // StartTerritoryTransition's call site, the only one.
        Preloader = Plugin.SigScanner.GetStaticAddressFromSig(
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 40 0F B6 D5");
    }
}
