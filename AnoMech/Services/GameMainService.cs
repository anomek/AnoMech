using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Services;

internal unsafe class GameMainService
{
    [Signature("40 55 56 41 54 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static LoadZoneDelegate LoadZone;

    public delegate void LoadZoneDelegate(GameMain* thisPtr, uint territoryTypeId, uint transitionTerritoryFilterKey, byte a4, byte a5);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new GameMainService());
    }
}
