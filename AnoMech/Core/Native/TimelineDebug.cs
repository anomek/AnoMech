using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

// Timeline/effect test bench shared by the debug menu and the running-sim window. Every action
// arms a log-persist window and, outside a session, holds the send-side firewall for it so a
// played animation or client-side spawn stays invisible to the server. Nothing here targets a
// spawn: targeting a client-side actor in a real zone would hand its fake entity id to the
// server before any hold applied.
internal static unsafe class TimelineDebug
{
    // Chaos: a real Type-3 monster model, the Flood scenario's stand-in carrier.
    private const uint ChaosBNpcBase = 19507;
    private const uint HelperNameId = 7131;

    private static bool ticking;
    private static int holdFramesLeft;
    private static int playerWatchFrames = -1;
    private static string timelineIdText = "10690";
    private static string actionIdText = "49769";

    public static SimEnemy? LastSpawn { get; set; }

    private static void EnsureTicking()
    {
        if (ticking) return;
        Plugin.Framework.Update += OnUpdate;
        ticking = true;
    }

    public static void Shutdown()
    {
        if (!ticking) return;
        Plugin.Framework.Update -= OnUpdate;
        ticking = false;
        if (holdFramesLeft > 0) Plugin.GameInstance?.World.Map.HoldSendFirewall(false);
        holdFramesLeft = 0;
        DiagnosticLog.ForcePersist = false;
    }

    private static void OnUpdate(IFramework framework)
    {
        if (holdFramesLeft > 0 && --holdFramesLeft == 0)
        {
            DiagnosticLog.ForcePersist = false;
            Plugin.GameInstance?.World.Map.HoldSendFirewall(false);
        }
        if (playerWatchFrames < 0) return;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            playerWatchFrames = -1;
            return;
        }
        if (playerWatchFrames < 15 || playerWatchFrames % 15 == 0)
            DiagnosticLog.Info($"[TimelineDebug] player timeline +{playerWatchFrames}f: {SimEnemy.DescribeActionTimeline((BattleChara*)player.Address)}");
        if (++playerWatchFrames > 150) playerWatchFrames = -1;
    }

    // ~7s of persisted logging with the send firewall held (outside a session).
    private static void Arm()
    {
        EnsureTicking();
        DiagnosticLog.ForcePersist = true;
        Plugin.GameInstance?.World.Map.HoldSendFirewall(true);
        holdFramesLeft = 400;
    }

    private static bool TryParse(string text, out uint value)
    {
        var t = text.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return uint.TryParse(t, out value);
    }

    public static void PlayOnPlayer(ushort timelineId)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            Plugin.Log.Warning("[TimelineDebug] no local player");
            return;
        }
        var chara = (BattleChara*)player.Address;
        Arm();
        DiagnosticLog.Info($"[TimelineDebug] Play timeline {timelineId} on the local player (territory {Plugin.ClientState.TerritoryType}) -- before: {SimEnemy.DescribeActionTimeline(chara)}");
        chara->Timeline.PlayActionTimeline(timelineId, 0);
        DiagnosticLog.Info($"[TimelineDebug] Play timeline {timelineId} on the local player -- after: {SimEnemy.DescribeActionTimeline(chara)}");
        playerWatchFrames = 0;
    }

    public static void SpawnChaosNearPlayer()
    {
        var game = Plugin.GameInstance;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (game == null || player == null)
        {
            Plugin.Log.Warning("[TimelineDebug] no game/player to spawn next to");
            return;
        }
        Arm();
        // 4y in front of the player, facing the way the player faces, so an effect fired from it
        // runs away from the player like a wave from its anchor.
        var world = game.World;
        var forward = new Vector3(MathF.Sin(player.Rotation), 0f, MathF.Cos(player.Rotation));
        var local = world.Coordinates.ToLocal(player.Position + forward * 4f);
        LastSpawn = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: ChaosBNpcBase, NameId: HelperNameId, Level: 1,
            Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
            Placement: new Placement(local, player.Rotation)));
        DiagnosticLog.Info($"[TimelineDebug] spawned Chaos test carrier at {local} (territory {Plugin.ClientState.TerritoryType}): {(LastSpawn == null ? "failed" : LastSpawn.DescribeDrawState())}");
    }

    public static void PlayOnLastSpawn(ushort timelineId)
    {
        if (LastSpawn is not { IsActive: true } enemy)
        {
            Plugin.Log.Warning("[TimelineDebug] no live test spawn -- spawn one first");
            return;
        }
        Arm();
        DiagnosticLog.Info($"[TimelineDebug] Play timeline {timelineId} on test spawn (territory {Plugin.ClientState.TerritoryType}) -- before: {enemy.DescribeActionTimeline()}");
        enemy.PlayActionTimeline(timelineId);
        enemy.StartTimelineWatch(4f);
    }

    // The whole wave path on the test spawn: the action effect exactly as the scenario fires it
    // (caster = animation target, no targets, lock 1.1, zero target position).
    public static void EffectOnLastSpawn(uint actionId)
    {
        var game = Plugin.GameInstance;
        if (game == null || LastSpawn is not { IsActive: true } enemy)
        {
            Plugin.Log.Warning("[TimelineDebug] no live test spawn -- spawn one first");
            return;
        }
        Arm();
        DiagnosticLog.Info($"[TimelineDebug] Action effect {actionId} on test spawn at {enemy.Position} rot={enemy.Rotation:F3} (territory {Plugin.ClientState.TerritoryType}).");
        enemy.NativeActionEffect(actionId, 1.1f, (ushort)actionId, 0, ActionType.Action, 0,
            position: game.World.Coordinates.ToLocal(Vector3.Zero), animationTargetId: enemy.GameObjectId);
        enemy.StartTimelineWatch(4f);
    }

    public static void DespawnLastSpawn()
    {
        if (LastSpawn == null) return;
        LastSpawn.Despawn();
        LastSpawn = null;
        DiagnosticLog.Info("[TimelineDebug] test spawn despawned.");
    }

    public static void DrawControls()
    {
        if (!ImGui.CollapsingHeader("Timeline test (debug)")) return;
        ImGui.TextDisabled("Never target the test spawn in a real zone. Every button holds the send firewall for ~7s outside a session.");
        ImGui.SetNextItemWidth(100);
        ImGui.InputText("Timeline id##tldbg", ref timelineIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Play on self##tldbg"))
        {
            if (TryParse(timelineIdText, out var id)) PlayOnPlayer((ushort)id);
        }
        ImGui.SameLine();
        if (ImGui.Button("Play on test spawn##tldbg"))
        {
            if (TryParse(timelineIdText, out var id)) PlayOnLastSpawn((ushort)id);
        }
        ImGui.SetNextItemWidth(100);
        ImGui.InputText("Action id##tldbg", ref actionIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Effect on test spawn##tldbg"))
        {
            if (TryParse(actionIdText, out var id)) EffectOnLastSpawn(id);
        }
        if (ImGui.Button("Spawn Chaos test carrier##tldbg")) SpawnChaosNearPlayer();
        ImGui.SameLine();
        if (ImGui.Button("Despawn it##tldbg")) DespawnLastSpawn();
        ImGui.SameLine();
        ImGui.TextDisabled(LastSpawn is { IsActive: true } ? "test spawn: live" : "test spawn: none");
    }
}
