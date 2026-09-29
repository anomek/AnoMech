using System;
using System.Collections.Generic;
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
//
// It also clears ParticipatingInCrossWorldPartyOrAlliance: with it set, ECommons'
// UniversalParty (Splatoon priority lists, script GetPartyMembers) reads the server's
// cross-realm roster instead of MainGroup, so it lists the player's real party and never the
// doppels PartyHud writes there.
internal sealed class SplatoonCompat : IDisposable
{
    public const string SplatoonInternalName = "Splatoon";
    public const int SceneOverrideOff = -1;

    private readonly CombatGate combat = new();
    private byte? wantedScene;
    private byte? sceneBeforeRun;
    private Dictionary<ConditionFlag, bool>? conditionsBeforeRun;

    // Held for the whole run; InCombat is driven by the gate instead.
    private static readonly (ConditionFlag Flag, bool Value)[] HeldConditions =
    [
        (ConditionFlag.BoundByDuty, true),
        (ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance, false),
    ];

    private static readonly ConditionFlag[] OwnedConditions =
        [ConditionFlag.InCombat, .. HeldConditions.Select(h => h.Flag)];

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

        conditionsBeforeRun ??= OwnedConditions.ToDictionary(f => f, f => Plugin.Condition[f]);
        if (combat.Tick() is { } inCombat)
            EncounterFlags.SetCondition(ConditionFlag.InCombat, inCombat);
        foreach (var (flag, value) in HeldConditions)
            EncounterFlags.SetCondition(flag, value);

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
            foreach (var (flag, value) in conditions)
                EncounterFlags.SetCondition(flag, value);
        }
        conditionsBeforeRun = null;
        if (sceneBeforeRun is { } original)
            EncounterFlags.WriteEnvScene(original);
        sceneBeforeRun = null;
        wantedScene = null;
    }

    public void Dispose() => Release();
}
