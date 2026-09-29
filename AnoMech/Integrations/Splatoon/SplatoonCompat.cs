using System;
using System.Linq;
using AnoMech.Scenarios;
using Dalamud.Game.ClientState.Conditions;

namespace AnoMech.Integrations.Splatoon;

// Makes a running scenario look like a live pull to Splatoon. Casts, action effects, actor
// VFX, tethers and statuses already reach Splatoon's hooks, because the sim drives them
// through the same native functions the server's packets do. What the firewall leaves unset is
// the encounter state Splatoon gates on: InCombat (script resets, combat-only layouts,
// Controller.CombatSeconds), BoundByDuty (duty-only layouts) and the phase's env scene
// (Controller.Scene, layout scene locks). This holds those for the length of a run.
internal sealed class SplatoonCompat : IDisposable
{
    public const string SplatoonInternalName = "Splatoon";
    public const int SceneOverrideOff = -1;

    private readonly CombatGate combat = new();
    private byte? wantedScene;
    private byte? sceneBeforeRun;
    private (bool InCombat, bool BoundByDuty)? conditionsBeforeRun;

    public bool IsHoldingEncounter => combat.Owns;
    public byte? AppliedScene => combat.Owns ? wantedScene : null;

    public static bool IsSplatoonLoaded()
        => Plugin.PluginInterface.InstalledPlugins.Any(p => p.InternalName == SplatoonInternalName && p.IsLoaded);

    public void OnScenarioStarted(IPhase phase)
    {
        if (!Plugin.Config.SplatoonCompat) return;
        combat.Begin();
        wantedScene = ResolveScene(phase, Plugin.Config.SplatoonSceneOverride);
        Core.DiagnosticLog.Info($"[SplatoonCompat] Holding encounter state for {phase.Zone.Name} {phase.Name}, scene {wantedScene?.ToString() ?? "untouched"}.");
    }

    public void OnScenarioStopped() => Release();

    // Every frame, after Game.Tick.
    public void Tick()
    {
        if (!combat.Owns) return;
        if (!Plugin.Config.SplatoonCompat || !Plugin.GameInstance.World.Map.IsInInstance)
        {
            Release();
            return;
        }

        conditionsBeforeRun ??= (Plugin.Condition[ConditionFlag.InCombat], Plugin.Condition[ConditionFlag.BoundByDuty]);
        if (combat.Tick() is { } inCombat)
            EncounterFlags.SetCondition(ConditionFlag.InCombat, inCombat);
        EncounterFlags.SetCondition(ConditionFlag.BoundByDuty, true);

        if (wantedScene is { } scene)
        {
            sceneBeforeRun ??= EncounterFlags.ReadEnvScene();
            EncounterFlags.WriteEnvScene(scene);
        }
    }

    internal static byte? ResolveScene(IPhase phase, int sceneOverride)
        => sceneOverride is >= byte.MinValue and <= byte.MaxValue ? (byte)sceneOverride : phase.EnvScene;

    // Synchronous, so a Leave's inn reload never starts with the flags still up.
    private void Release()
    {
        if (combat.End())
            Core.DiagnosticLog.Info("[SplatoonCompat] Released encounter state.");
        if (conditionsBeforeRun is { } conditions)
        {
            EncounterFlags.SetCondition(ConditionFlag.InCombat, conditions.InCombat);
            EncounterFlags.SetCondition(ConditionFlag.BoundByDuty, conditions.BoundByDuty);
        }
        conditionsBeforeRun = null;
        if (sceneBeforeRun is { } original)
            EncounterFlags.WriteEnvScene(original);
        sceneBeforeRun = null;
        wantedScene = null;
    }

    public void Dispose() => Release();
}
