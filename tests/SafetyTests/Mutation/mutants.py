"""Mutants for run.py: bugs fixed during development, put back one at a time (PAST), and
mistakes a new scenario or engine change could make (NEW). Each edit must match its file
exactly once; update an edit when the code it targets changes."""

ZS = "AnoMech/Core/Map/ZoneSession.cs"
ZG = "AnoMech/Core/Map/ZoneSession.Guard.cs"
MC = "AnoMech/Core/Map/MapController.cs"
GM = "AnoMech/Core/Game/Game.cs"
PL = "AnoMech/Plugin.cs"
LH = "AnoMech/Core/Native/LocalPlayerInputHooks.cs"
PS = "AnoMech/Multiplayer/MultiplayerManager.PeerSnapshot.cs"


def m(id, desc, *edits):
    return {"id": id, "kind": "past-fix", "desc": desc, "edits": [{"file": f, "find": a, "replace": b} for f, a, b in edits]}


GATE_PREVIOUS = '        if (Current is { guardArmed: true }) return Settling("the previous run", out settling);\n'
GATE_CASTING = '        if (player.IsCasting) return $"casting {ActionLookup.Name(player.CastActionId)}";\n'
GATE_PRESS = ('        if (zoneChangePressedAt is { } pressed)\n'
              '            return $"{ActionLookup.Name(zoneChangeActionId)} was used {Stopwatch.GetElapsedTime(pressed).TotalSeconds:F0}s ago and has not resolved";\n')
ENTER_ARM = ('        EnableFirewall();\n'
             '        if (!FirewallArmed(out var why))\n'
             '        {\n'
             '            // Half up with no session, nothing would ever release what it holds.\n'
             '            if (!DisableFirewall()) Die($"{why}, and the half-armed firewall would not come back down");\n'
             '            AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] Enter refused: {why}.");\n'
             '            return false;\n'
             '        }\n'
             '        CaptureInnState();\n'
             '        try\n'
             '        {\n'
             '            LoadZoneInternal(territoryId, false, playerSpawn);\n'
             '        }')
ENTER_ARM_AFTER = ('        CaptureInnState();\n'
                   '        try\n'
                   '        {\n'
                   '            LoadZoneInternal(territoryId, false, playerSpawn);\n'
                   '            EnableFirewall();\n'
                   '        }')
ENTER_CATCH = ('        catch (Exception e)\n'
               '        {\n'
               '            // Part-way into the load with the firewall up: back out through the verified revert,\n'
               '            // or nothing would ever lift the firewall.\n'
               '            AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] Enter: the zone load threw -- reverting to the inn. {e}");\n'
               '            IsActive = true;\n'
               '            ArmGuard(territoryId);\n'
               '            Revert(false);\n'
               '            return false;\n'
               '        }')
RESTORE_TRY = ('                try\n'
               '                {\n'
               '                    RestoreBeforeLift(savedPosition, savedRotation);\n'
               '                }\n'
               '                catch (Exception e)\n'
               '                {\n'
               '                    AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] The restore before the lift threw: {e}");\n'
               '                }\n')

ENABLE_SEND = ('        try { sendPacketHook.Enable(); }\n'
               '        catch (Exception e) { AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] The send filter would not enable: {e.Message}"); }\n')
ENABLE_RECEIVE = ('        try { receivePacketHook.Enable(); }\n'
                  '        catch (Exception e) { AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] The receive filter would not enable: {e.Message}"); }\n')
DISABLE_RECEIVE = ('        try { receivePacketHook.Disable(); }\n'
                   '        catch (Exception e) { AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] The receive filter would not disable: {e.Message}"); }\n'
                   '        if (!receivePacketHook.IsEnabled)\n'
                   '        {\n'
                   '            try { sendPacketHook.Disable(); }\n'
                   '            catch (Exception e) { AnoMech.Core.DiagnosticLog.Warn($"[ZoneGuard] The send filter would not disable: {e.Message}"); }\n'
                   '        }\n')
BUSY_STAMP = ('        var acting = IsServerActingSoon();\n'
              '        if (acting || wasServerActing) lastBusyAt = Stopwatch.GetTimestamp();\n')

PAST = [
    m("A01", "INC-01: Revert runs outside a session (Leave without a sim moved the character to a stale spot, no filter)",
      (ZS, '        if (!IsActive) return;\n        if (guardArmed) AnoMech.Core.DiagnosticLog.Info(StateSnapshot($"revert starting',
       '        if (guardArmed) AnoMech.Core.DiagnosticLog.Info(StateSnapshot($"revert starting')),
    m("A02", "INC-02: a stay's delayed lift is not keyed to its stay (stayId race)",
      (ZS, "if (stay != stayId || !guardArmed) return;", "if (!guardArmed) return;")),
    m("A03", "INC-02: a new stay may start while the previous stay's lift is pending", (ZG, GATE_PREVIOUS, "")),
    m("A04", "INC-02 (both): stale lift unkeyed AND the start gate open under a pending lift",
      (ZS, "if (stay != stayId || !guardArmed) return;", "if (!guardArmed) return;"), (ZG, GATE_PREVIOUS, "")),
    m("A05", "INC-07: start allowed while casting", (ZG, GATE_CASTING, "")),
    m("A06", "INC-07: start allowed with an unresolved Teleport/Return press", (ZG, GATE_PRESS, "")),
    m("A07", "INC-07: both start checks for a teleport in flight removed", (ZG, GATE_CASTING, ""), (ZG, GATE_PRESS, "")),
    m("A08", "INC-07: the in-stay trip on a Teleport cast begun before the arm removed",
      (ZG, "&& player.CurrentCastTime > Stopwatch.GetElapsedTime(guardArmedAt).TotalSeconds + 0.05)", "&& false)")),
    m("A09", "INC-07: Revert of a tripped stay reloads the inn instead of stopping",
      (ZS, "        if (tripReason is { } tripped) Die(tripped);\n", "")),
    m("A10", "INC-08: GameMain's territory (0 after a sim load) used for the drift check (false kills)",
      (ZG, "        var client = Plugin.ClientState.TerritoryType;", "        var client = NativeTerritory();")),
    m("A11", "FIX-03: position check effectively off (tolerance 2000y)",
      (ZG, "        if (apart > LiftPositionTolerance)", "        if (apart > 2000f)")),
    m("A12", "FIX-03: non-finite positions accepted at the lift",
      (ZG, "        if (!Finite(here) || !Finite(armedPosition))", "        if (false)")),
    m("A13", "FIX-03: a missing local player passes the lift",
      (ZG, "        if (Plugin.ObjectTable.LocalPlayer is not { } player) return NoLocalPlayer;",
       "        if (Plugin.ObjectTable.LocalPlayer is not { } player) return null;")),
    m("A14", "FIX-03: no 3-second cap (an unverifiable lift waits forever)", (ZG, "waited < LiftRetrySeconds", "waited < double.MaxValue")),
    m("A15", "FIX-03: the lift ignores every blocker",
      (ZG, "        if (LiftBlockedReason() is { } reason)", "        if (LiftBlockedReason() is { } reason && false)")),
    m("A16", "FIX-04: the debug hold can drop the send filter during the post-revert second",
      (ZS, "        if (IsActive || guardArmed || hold == sendHoldActive) return;", "        if (IsActive || hold == sendHoldActive) return;")),
    m("A17", "FIX-07: RunScenarioInternal ignores TryLoad's result (character moved with no sim)",
      (GM, "                zone.Level, zone.ItemLevel))", "                zone.Level, zone.ItemLevel) && false)")),
    m("A18", "INC-05: weather written outside a session",
      (ZS, "        if (!IsActive) return;\n        desiredWeather = weatherId;", "        desiredWeather = weatherId;")),
    m("A19", "FIX-05: a host Teleport message acts outside the peer's run",
      (PS, "    private void OnTeleportReceived(TeleportMessage msg)\n    {\n        if (!PeerInRun) return;\n",
       "    private void OnTeleportReceived(TeleportMessage msg)\n    {\n")),
    m("A20", "FIX-05: map effects replayed outside a sim", (MC, "        if (IsInInstance) return true;", "        if (true) return true;")),
    m("A21", "Enter loads with the filter unconfirmed (arm check removed)",
      (ZS, "        if (!FirewallArmed(out var why))", "        if (!FirewallArmed(out var why) && false)")),
    m("A22", "Enter loads the zone before raising the filter", (ZS, ENTER_ARM, ENTER_ARM_AFTER)),
    m("A23", "the send detour passes every opcode",
      (ZS, "            if (opcode == heartbeatOpcode)", "            if (opcode == heartbeatOpcode || opcode != 0)")),
    m("A24", "the send detour passes one extra opcode (movement)",
      (ZS, "            if (opcode == heartbeatOpcode)", "            if (opcode == heartbeatOpcode || opcode == 0x01F2)")),
    m("A25", "the receive filter passes every opcode",
      (ZS, "            if (!safeMode || Plugin.Config.ZoneDownOpcodes.Contains(incomingOpcode))",
       "            if (true || Plugin.Config.ZoneDownOpcodes.Contains(incomingOpcode))")),
    m("A26", "the safety stop returns instead of ending the process",
      (ZG, '        Environment.FailFast($"AnoMech safety stop: {reason}");', "        _ = reason;")),
    m("A27", "the guard no longer runs every frame", (PL, "        try { ZoneSession.TickGuard(); }", "        try { }")),
    m("A28", "zone changes no longer reach the guard", (PL, "        ZoneSession.NoteTerritoryChanged(territory);\n", "")),
    m("A29", "Teleport/Return presses no longer reach the guard", (LH, "        Core.Map.ZoneSession.NoteActionPressed(type, actionId);\n", "")),
    m("A30", "unload no longer reverts the zone when the teardown throws (finally removed)", (PL, "            ZoneSession.Current?.Dispose();\n", "")),
    m("A31", "OPEN-A: the lift doesn't require the inn reload to have completed",
      (ZG, "        if (completedLoad != innClientTerritory || loadedInstanceContent != null)", "        if (false)")),
    m("A32", "OPEN-B: the lift doesn't wait for the inn load to finish (post-load packet escapes)",
      (ZG, "        if (pendingLiftMayRetry && NativeTerritory() != innClientTerritory)", "        if (false)")),
    m("A33", "OPEN-C: a throwing zone load leaves the filter up with no session",
      (ZS, ENTER_CATCH, "        catch (Exception)\n        {\n            throw;\n        }")),
    m("A34", "OPEN-D: a throw in the delayed restore strands the stay",
      (ZS, RESTORE_TRY, "                RestoreBeforeLift(savedPosition, savedRotation);\n")),
    m("A35", "OPEN-E: an unload during the pending lift leaves Occupied set",
      (ZS, "                SetLocalPlayerPosition(armedPosition, armedRotation);\n                ReleasePlayerConditions();\n",
       "                SetLocalPlayerPosition(armedPosition, armedRotation);\n")),
    m("A36", "OPEN-E: a mid-sim unload leaves the Suppression afflictions set",
      (ZS, '            SetLocalPlayerPosition(sessionSave.Position, sessionSave.Rotation);\n            ReleasePlayerConditions();\n            LiftFirewallOrDie("on plugin unload", mayRetry: false);',
       '            SetLocalPlayerPosition(sessionSave.Position, sessionSave.Rotation);\n            Conditions.Instance()->Occupied = false;\n            LiftFirewallOrDie("on plugin unload", mayRetry: false);')),
    m("A37", "OPEN-G: the stale delayed restore runs after an unload already lifted",
      (ZS, "if (stay != stayId || !guardArmed) return;", "if (stay != stayId) return;")),
    m("A38", "latch: a missing local player reads as the cast ending", (ZG, "        if (player == null) zoneChangeCastLost = true;\n", "")),
    m("A39", "latch: a later refused press releases an earlier cast's hold",
      (ZG, "            && (zoneChangePressedAt == null || player.CastActionId != zoneChangeActionId))", "            && false)")),
    m("A40", "debug hold: a start may run under the hold",
      (ZG, '        if (Current is { sendHoldActive: true }) return Settling("the debug send hold", out settling);\n', "")),
    m("A41", "debug hold: an interruption the server never heard releases the latch",
      (ZG, "        if (zoneChangeCastSeen && Current is { sendHoldActive: true }) zoneChangeCastLost = true;\n", "")),
    m("A42", "debug hold: the release doesn't restore the character",
      (ZS, "                SetLocalPlayerPosition(at);\n            try { sendPacketHook.Disable(); }", "                _ = at;\n            try { sendPacketHook.Disable(); }")),
    m("A43", "debug hold: a lift doesn't end the hold flag (hold later refuses)",
      (ZS, '        // A debug hold the lift just took down is over too, or the next hold would find it "on".\n        sendHoldActive = false;\n', "")),
    m("A44", "MapController marks the sim loaded even when the zone session refused",
      (MC, "            if (!Load(target.TerritoryId, target.PlayerPosition, levelSync, itemLevelSync)) return false;",
       "            Load(target.TerritoryId, target.PlayerPosition, levelSync, itemLevelSync);")),
    m("A45", "the zone-in and busy settle is lost (3s -> 0s)", (ZG, "    private const double SettleSeconds = 3;", "    private const double SettleSeconds = 0;")),
    m("A46", "InCombat dropped from the busy check", (ZS, "            || c[ConditionFlag.InCombat]\n", "")),
    m("A47", "the per-frame trip on a disabled hook removed",
      (ZG, '            Trip("a firewall hook was found disabled while armed");', "            _ = 0;")),
    m("A48", "a disabled hook at the lift waits 3s instead of stopping at once",
      (ZG, "    private bool MustDieNow(string reason) => reason == tripReason || reason == HookDisabled;",
       "    private bool MustDieNow(string reason) => reason == tripReason;")),
    m("A49", "zone changes don't restart the settle", (ZG, "        lastTerritoryChangeAt = Stopwatch.GetTimestamp();\n", "")),
    m("A50", "lift without the one-second settle after the reload",
      (ZS, "            ThreadingTask.Delay(1000).ContinueWith(_ => Plugin.Framework.Run(() =>",
       "            ThreadingTask.Delay(0).ContinueWith(_ => Plugin.Framework.Run(() =>")),
    m("A51", "TerritoryDrift dropped from the lift check", (ZG, "        return TerritoryDrift() ?? PositionDrift();", "        return PositionDrift();")),
    m("A52", "the logout event no longer trips the guard",
      (ZG, "    public static void NoteLogout(int type, int code) => Current?.GuardLogout(type, code);",
       "    public static void NoteLogout(int type, int code) { }")),
    m("A53", "SafetyHook: a throwing Enable escapes EnableFirewall (Enter throws with the send filter up)",
      (ZS, ENABLE_SEND + ENABLE_RECEIVE, "        sendPacketHook.Enable();\n        receivePacketHook.Enable();\n")),
    m("A54", "Enter's refusal ignores a half-armed filter that would not come back down",
      (ZS, '            if (!DisableFirewall()) Die($"{why}, and the half-armed firewall would not come back down");\n', "            DisableFirewall();\n")),
    m("A55", "the lift ignores a filter that would not come down",
      (ZG, '        if (!DisableFirewall()) Die($"a filter would not come down after the lift was verified ({when})");\n', "        DisableFirewall();\n")),
    m("A56", "SafetyHook: a throwing Disable escapes DisableFirewall",
      (ZS, DISABLE_RECEIVE, "        receivePacketHook.Disable();\n        sendPacketHook.Disable();\n")),
    m("A57", "a lift that fails part-way takes the send filter down anyway",
      (ZS, "        if (!receivePacketHook.IsEnabled)\n        {\n            try { sendPacketHook.Disable(); }", "        {\n            try { sendPacketHook.Disable(); }")),
    m("A58", "the settle counts from the last busy frame, one frame before the zone-in ends",
      (ZG, BUSY_STAMP, "        var acting = IsServerActingSoon();\n        if (acting) lastBusyAt = Stopwatch.GetTimestamp();\n")),
    m("A59", "the debug hold starts mid-zone-change or under a pending Teleport",
      (ZS, "            if (HoldBlockedReason() is { } blocked)", "            if (HoldBlockedReason() is { } blocked && false)")),
    m("A60", "the debug hold keeps its start position through a zone-in",
      (ZG, "        holdSawZoning = false;\n        holdPosition = player.Position;\n", "        holdSawZoning = false;\n")),
    m("A61", "the debug hold restores a stale spot when a zone-in had no character to re-anchor on",
      (ZS, "            if (!Zoning() && !holdSawZoning && sendPacketHook.IsEnabled", "            if (!Zoning() && sendPacketHook.IsEnabled")),
    m("A62", "the debug hold's flag follows the request, not the hook",
      (ZS, "        sendHoldActive = sendPacketHook.IsEnabled;\n        AnoMech.Core.DiagnosticLog.Info(sendHoldActive == hold", "        sendHoldActive = hold;\n        AnoMech.Core.DiagnosticLog.Info(sendHoldActive == hold")),
    m("A63", "a plugin unload drops the debug hold without putting the character back",
      (ZS, "            // Through its own release, which puts the character back first.\n            HoldSendFirewall(false);\n", "")),
]

# Equivalent: each removes a check another one already makes, so no test can tell them apart.
EQUIVALENT = {
    "A41": "no Teleport can be in flight under the hold: it refuses while one is pending and holds the request of one pressed during it",
    "A48": "the per-frame guard trips on a disabled hook before a held lift could matter",
    "A49": "the settle already counts from the zone-in's end (BetweenAreas); Dalamud reports the change as the load begins",
    "A51": "the lift already requires the client's territory to be the inn",
}


FLOOD = "AnoMech/Scenarios/Umad/P5Flood/UmadP5FloodScenario.cs"
RUN_ANCHOR = "        world = worldParam;\n        party = worldParam.Party;\n"
CLASS_ANCHOR = "public sealed class UmadP5FloodScenario : IMultiplayerReplayable\n{\n"
PS = "AnoMech/Multiplayer/MultiplayerManager.PeerSnapshot.cs"
FM = "AnoMech/Core/Native/ForcedMovement.cs"
FM_ANCHOR = "internal static class ForcedMovement\n{\n"


def in_run(id, desc, code):
    return {"id": id, "kind": "new-scenario", "desc": desc,
            "edits": [{"file": FLOOD, "find": RUN_ANCHOR, "replace": RUN_ANCHOR + code}]}


def edit(id, desc, *edits):
    return {"id": id, "kind": "new-scenario", "desc": desc, "edits": [{"file": f, "find": a, "replace": b} for f, a, b in edits]}

NEW = [
    in_run("B01", "moves the player after a Task.Delay (outlives the stay)",
           "        System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ => worldParam.Party.Player?.SetPosition(new Placement(Vector3.Zero, 0f)));\n"),
    in_run("B02", "subscribes to the framework update (outlives the stay)",
           "        Plugin.Framework.Update += _ => worldParam.Party.Player?.SetPosition(Vector3.Zero);\n"),
    in_run("B03", "calls Game.Leave when its timeline ends",
           "        worldParam.Events.Add(60f, () => Plugin.GameInstance.Leave());\n"),
    in_run("B04", "unloads the map itself when its timeline ends",
           "        worldParam.Events.Add(60f, () => worldParam.Map.Unload());\n"),
    in_run("B05", "creates its own hook",
           "        var hook = Plugin.GameInterop.HookFromAddress<Action>(nint.Zero, () => { });\n"),
    in_run("B06", "writes the local player's position natively",
           "        unsafe { ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Plugin.ObjectTable.LocalPlayer!.Address)->SetPosition(0, 0, 0); }\n"),
    in_run("B07", "uses the real action manager (auto Sprint)",
           "        unsafe { ActionManager.Instance()->UseAction(ActionType.Action, 3); }\n"),
    edit("B08", "keeps the SimPlayer in a static field for a later run",
         (FLOOD, CLASS_ANCHOR, CLASS_ANCHOR + "    private static SimPlayer? rememberedPlayer;\n"),
         (FLOOD, RUN_ANCHOR, RUN_ANCHOR + "        rememberedPlayer = worldParam.Party.Player;\n")),
    in_run("B09", "writes the client's conditions",
           "        unsafe { Conditions.Instance()->Occupied = true; }\n"),
    in_run("B10", "moves the player from a System.Threading.Timer",
           "        _ = new System.Threading.Timer(_ => worldParam.Party.Player?.SetPosition(Vector3.Zero), null, 10000, System.Threading.Timeout.Infinite);\n"),
    in_run("B11", "moves the player from an async continuation",
           "        async void Later() { await System.Threading.Tasks.Task.Delay(1000); worldParam.Party.Player?.SetPosition(Vector3.Zero); }\n        Later();\n"),
    in_run("B12", "fabricates an ActorControl on the local player",
           "        FFXIVClientStructs.FFXIV.Client.Network.PacketDispatcher.HandleActorControlPacket(Plugin.ObjectTable.LocalPlayer!.EntityId, 241, 0, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false);\n"),
    in_run("B13", "writes the weather manager",
           "        unsafe { WeatherManager.Instance()->WeatherId = 2; }\n"),
    edit("B14", "adds a host message handler that moves the peer without PeerInRun",
         (PS, "    private void OnCarryReceived(CarryMessage msg)\n",
          "    private void OnDashReceived(TeleportMessage msg)\n    {\n        if (!NetGuard.TryPosition(msg.X, msg.Y, msg.Z, out var position)) return;\n        OwnMember(msg.Role, \"Dash\")?.TeleportTo(new Placement(position, 0f));\n    }\n\n    private void OnCarryReceived(CarryMessage msg)\n")),
    edit("B15", "gives its zone a mistyped territory id",
         ("AnoMech/Scenarios/Umad/UmadZone.cs", "    public uint TerritoryId => 1363;", "    public uint TerritoryId => 1336;")),
    in_run("B16", "drops the debug send hold itself",
           "        worldParam.Map.HoldSendFirewall(false);\n"),
    in_run("B17", "reaches into the zone session",
           "        ZoneSession.Current?.DisableFirewall();\n"),
    in_run("B18", "moves the player from Framework.RunOnTick",
           "        Plugin.Framework.RunOnTick(() => worldParam.Party.Player?.SetPosition(Vector3.Zero), TimeSpan.FromSeconds(10));\n"),
    in_run("B19", "moves the player from its own thread",
           "        new System.Threading.Thread(() => { System.Threading.Thread.Sleep(10000); worldParam.Party.Player?.SetPosition(Vector3.Zero); }).Start();\n"),
    in_run("B20", "injects a fabricated server packet",
           "        worldParam.Map.InjectIncomingPacket(0, 0x199, ReadOnlySpan<byte>.Empty, \"test\");\n"),
    in_run("B21", "resets the run itself",
           "        worldParam.Events.Add(60f, () => Plugin.GameInstance.Reset());\n"),
    in_run("B22", "carries the player with a native forced move",
           "        worldParam.Events.Add(5f, () => ForcedMovement.CarryTo(Plugin.ObjectTable.LocalPlayer!.EntityId, Vector3.Zero, 0f, true));\n"),
    edit("B23", "new engine code creates a hook",
         (FM, FM_ANCHOR, FM_ANCHOR + "    public static object Watch() => Plugin.GameInterop.HookFromAddress<Action>(nint.Zero, () => { });\n\n")),
    edit("B24", "new engine code snaps the local player natively",
         (FM, FM_ANCHOR, FM_ANCHOR + "    public static unsafe void SnapLocalPlayer(Vector3 to)\n    {\n        if (Plugin.ObjectTable.LocalPlayer is { } p) ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)p.Address)->SetPosition(to.X, to.Y, to.Z);\n    }\n\n")),
    in_run("B26", "opens the inbound filter for its own packets",
           "        Plugin.Config.ZoneDownOpcodes = [0x0199];\n"),
    edit("B27", "its settings panel moves the player it kept from the last run (drawn every frame, outside any stay)",
         (FLOOD, "    public void DrawSettings() => settingsWindow.Draw();",
          "    public void DrawSettings()\n    {\n        settingsWindow.Draw();\n        party?.Player?.SetPosition(new Placement(Vector3.Zero, 0f));\n    }")),
    in_run("B28", "defers a move with a CancellationTokenSource timer (fires after the stay)",
           "        new System.Threading.CancellationTokenSource(10000).Token.Register(() => worldParam.Party.Player?.SetPosition(new Placement(Vector3.Zero, 0f)));\n"),
    in_run("B29", "moves the player from an addon-lifecycle callback (any time the Social window opens)",
           "        Plugin.AddonLifecycle.RegisterListener(Dalamud.Game.Addon.Lifecycle.AddonEvent.PostSetup, \"Social\", (_, _) => worldParam.Party.Player?.SetPosition(new Placement(Vector3.Zero, 0f)));\n"),
    edit("B30", "calls a new engine helper that defers the move with Task.Delay",
         (FM, FM_ANCHOR, FM_ANCHOR + "    public static void MoveLater(AnoMech.Core.SimObjects.SimCharacter who, Vector3 to) => System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ => who.SetPosition(to));\n\n"),
         (FLOOD, RUN_ANCHOR, RUN_ANCHOR + "        if (worldParam.Party.Player is { } me) ForcedMovement.MoveLater(me, Vector3.Zero);\n")),
    in_run("B25", "keeps the world in a static field",
           "        UmadP5FloodScenarioCache.World = worldParam;\n"),
]

# B25 needs the static holder type in the scenario namespace.
NEW[-1]["edits"].append({"file": FLOOD, "find": CLASS_ANCHOR,
                       "replace": "internal static class UmadP5FloodScenarioCache\n{\n    public static SimWorld? World;\n}\n\n" + CLASS_ANCHOR})
