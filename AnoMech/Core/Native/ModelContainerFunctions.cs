using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace AnoMech.Core.Native;

internal static unsafe class ModelContainerFunctions
{
    // Client::Game::Character::ModelContainer_CalculateUnscaledRadius
    private delegate float CalculateUnscaledRadiusDelegate(ModelContainer* modelContainer);
    private static readonly CalculateUnscaledRadiusDelegate CalculateUnscaledRadiusFunction;

    static ModelContainerFunctions()
    {
        var addr = Plugin.SigScanner.ScanText(
            "40 53 48 83 EC 20 48 8B D9 48 8B 49 08 48 8B 01 FF 50 10 83 F8 02");

        CalculateUnscaledRadiusFunction =
            Marshal.GetDelegateForFunctionPointer<CalculateUnscaledRadiusDelegate>(addr);

        Plugin.Log.Information("[ModelContainerFunctions] Initialized.");
    }

    internal static float CalculateUnscaledRadius(ModelContainer* modelContainer)
    {
        return CalculateUnscaledRadiusFunction(modelContainer);
    }
}
