using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace AnoMech.Integrations.Splatoon;

// Client-side writes of the encounter state the server normally drives. The firewall drops
// every packet that would set these, so nothing overwrites them during a stay; the owner still
// re-asserts them each frame in case the client recomputes one.
internal static unsafe class EncounterFlags
{
    // EnvManager's active scene: the server advances it at phase transitions, and Splatoon
    // reads this byte for Controller.Scene and for layout scene locks. Not named in
    // FFXIVClientStructs.
    private const int EnvSceneOffset = 0x24;

    public static void SetCondition(ConditionFlag flag, bool value)
    {
        var address = Plugin.Condition.Address;
        if (address == 0) return;
        ((bool*)address)[(int)flag] = value;
    }

    public static byte? ReadEnvScene()
    {
        var env = EnvManager.Instance();
        return env == null ? null : *((byte*)env + EnvSceneOffset);
    }

    public static void WriteEnvScene(byte scene)
    {
        var env = EnvManager.Instance();
        if (env == null) return;
        *((byte*)env + EnvSceneOffset) = scene;
    }
}
