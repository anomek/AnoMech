using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Services;

internal unsafe class StatusManagerService
{
    [Signature("48 8B C4 55 57 41 54 41 56", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text)]
    public static OnGainStatusDelegate OnGainStatus { get; private set; } = null!;

    public delegate void OnGainStatusDelegate(StatusManager* self, ushort statusId, float a3, ushort param, long source, byte a6);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new StatusManagerService());
    }
}
