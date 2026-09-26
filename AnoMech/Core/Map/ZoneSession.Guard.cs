using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Map;

// The firewall cuts both directions for the whole stay, so anything the server does to the
// character meanwhile (a Teleport cast before it went up, a duty pop) leaves client and server
// disagreeing about where the character is, and the inn reload would then put the client in a
// zone the server doesn't have it in. This part refuses a start the server could still act on,
// watches the stay for the client-side signs of such a move, and kills the game rather than lift
// the firewall on one: a dead client sends nothing, and the next login starts from the server's
// truth.
public sealed unsafe partial class ZoneSession
{
    // ---- Start gate ---------------------------------------------------------------------

    // The one instance, for the events Plugin forwards.
    internal static ZoneSession? Current { get; private set; }

    private const uint TeleportActionId = 5;
    private const uint ReturnActionId = 6;
    // A Teleport/Return press blocks Start until its cast is seen interrupted, the zone changes,
    // or this passes (the server refused it). A press the client refused shows no cast at all.
    private const double ZoneChangeHoldSeconds = 20;
    private const double NoCastGraceSeconds = 1.5;
    private const float InterruptedBelowProgress = 0.9f;
    // The server may still be acting on whatever the player was just doing (an event's zone
    // change lands after the event ends), and a zone just entered is still being synced.
    private const double SettleSeconds = 3;

    private static long? zoneChangePressedAt;
    private static uint zoneChangeActionId;
    private static bool zoneChangeCastSeen;
    private static bool zoneChangeCastLost;
    private static float zoneChangeCastProgress;
    private static long? lastTerritoryChangeAt;
    private static long? lastBusyAt;
    private static bool wasServerActing;

    public static void NoteActionPressed(ActionType type, uint actionId)
    {
        if (type != ActionType.Action || actionId is not (TeleportActionId or ReturnActionId)) return;
        zoneChangePressedAt = Stopwatch.GetTimestamp();
        zoneChangeActionId = actionId;
        zoneChangeCastSeen = false;
        zoneChangeCastLost = false;
        zoneChangeCastProgress = 0f;
        DiagnosticLog.Info($"[ZoneGuard] {ActionLookup.Name(actionId)} pressed -- Start is blocked until it resolves.");
    }

    public static void NoteTerritoryChanged(uint territory)
    {
        lastTerritoryChangeAt = Stopwatch.GetTimestamp();
        if (zoneChangePressedAt != null)
            DiagnosticLog.Info($"[ZoneGuard] Zone changed to {territory} -- the pending {ActionLookup.Name(zoneChangeActionId)} resolved.");
        zoneChangePressedAt = null;
        Current?.GuardTerritoryChanged(territory);
    }

    public static void NoteLogout(int type, int code) => Current?.GuardLogout(type, code);

    // Every frame, from Plugin: the gate's timers run outside a session too.
    public static void TickGuard()
    {
        // Stamped on the first frame without the state too, so the settle counts from its end:
        // Dalamud reports a zone change as the load begins, not as it ends.
        var acting = IsServerActingSoon();
        if (acting || wasServerActing) lastBusyAt = Stopwatch.GetTimestamp();
        wasServerActing = acting;
        TickZoneChangeLatch();
        Current?.TickSessionGuard();
        Current?.TickSendHold();
    }

    private static bool Zoning() => Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51];

    // The debug hold keeps the character where the server last placed it; while the server may
    // still move it, or with no character, there is no such place.
    private static string? HoldBlockedReason()
    {
        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is not { } player) return "no local player";
        if (Zoning()) return "zoning";
        if (player.IsCasting || IsServerActingSoon()) return "the server may still act on the last action";
        if (zoneChangePressedAt != null) return $"{ActionLookup.Name(zoneChangeActionId)} has not resolved";
        return null;
    }

    private void TickSendHold()
    {
        if (!sendHoldActive) return;
        if (Zoning())
        {
            holdSawZoning = true;
            return;
        }
        if (!holdSawZoning || Plugin.ObjectTable.LocalPlayer is not { } player) return;
        holdSawZoning = false;
        holdPosition = player.Position;
        DiagnosticLog.Info($"[ZoneSession] Debug send hold: a zone-in placed the character at {Describe(player.Position, player.Rotation)}; the release restores to there.");
    }

    // The busy states whose outcome the server delivers after they end (an event's zone change,
    // a queue pop, a cast's effect), as opposed to ones that only occupy the player.
    private static bool IsServerActingSoon()
    {
        var c = Plugin.Condition;
        return c[ConditionFlag.Casting]
            || c[ConditionFlag.Casting87]
            || c[ConditionFlag.BetweenAreas]
            || c[ConditionFlag.BetweenAreas51]
            || c[ConditionFlag.Occupied]
            || c[ConditionFlag.Occupied30]
            || c[ConditionFlag.Occupied33]
            || c[ConditionFlag.Occupied38]
            || c[ConditionFlag.Occupied39]
            || c[ConditionFlag.OccupiedInEvent]
            || c[ConditionFlag.OccupiedInQuestEvent]
            || c[ConditionFlag.OccupiedInCutSceneEvent]
            || c[ConditionFlag.OccupiedSummoningBell]
            || c[ConditionFlag.WatchingCutscene]
            || c[ConditionFlag.WatchingCutscene78]
            || c[ConditionFlag.TradeOpen]
            || c[ConditionFlag.LoggingOut]
            || c[ConditionFlag.SystemError]
            || c[ConditionFlag.WaitingForDuty]
            || c[ConditionFlag.WaitingForDutyFinder]
            || c[ConditionFlag.InDutyQueue]
            || c[ConditionFlag.ReadyingVisitOtherWorld]
            || c[ConditionFlag.WaitingToVisitOtherWorld];
    }

    private static void TickZoneChangeLatch()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        // The cast on screen is what the server acts on, whichever press it came from: a later
        // press the game refused (it re-latches the press hook) must not release it.
        if (player is { IsCasting: true } && player.CastActionId is TeleportActionId or ReturnActionId
            && (zoneChangePressedAt == null || player.CastActionId != zoneChangeActionId))
        {
            zoneChangePressedAt = Stopwatch.GetTimestamp();
            zoneChangeActionId = player.CastActionId;
            zoneChangeCastSeen = false;
            zoneChangeCastLost = false;
            DiagnosticLog.Info($"[ZoneGuard] {ActionLookup.Name(zoneChangeActionId)} cast seen -- Start is blocked until it resolves.");
        }
        if (zoneChangePressedAt is not { } pressedAt) return;
        var elapsed = Stopwatch.GetElapsedTime(pressedAt).TotalSeconds;
        // Without a local player (a redraw) the cast can't be watched; whatever it did meanwhile,
        // only the zone change or the timeout can resolve it now.
        if (player == null) zoneChangeCastLost = true;
        if (player is { IsCasting: true } && player.CastActionId == zoneChangeActionId)
        {
            zoneChangeCastSeen = true;
            zoneChangeCastProgress = player.TotalCastTime > 0f ? player.CurrentCastTime / player.TotalCastTime : 0f;
            return;
        }
        // Ended under the debug hold, the interrupting move never reached the server, which then
        // finishes the cast.
        if (zoneChangeCastSeen && Current is { sendHoldActive: true }) zoneChangeCastLost = true;
        string? release = null;
        if (player != null && !zoneChangeCastLost && zoneChangeCastSeen && zoneChangeCastProgress < InterruptedBelowProgress)
            release = $"its cast was interrupted at {zoneChangeCastProgress:P0}";
        else if (player != null && !zoneChangeCastLost && !zoneChangeCastSeen && elapsed > NoCastGraceSeconds)
            release = "no cast followed the press";
        else if (elapsed > ZoneChangeHoldSeconds)
            release = $"{ZoneChangeHoldSeconds:F0}s passed with no zone change";
        if (release == null) return;
        DiagnosticLog.Info($"[ZoneGuard] {ActionLookup.Name(zoneChangeActionId)} hold released: {release}.");
        zoneChangePressedAt = null;
    }

    private static double SecondsSince(long? stamp) => stamp is { } s ? Stopwatch.GetElapsedTime(s).TotalSeconds : double.PositiveInfinity;

    // Null when a scenario may start now, otherwise why not. Every start path ends in Enter,
    // which asks again, so a change between the click and the deferred start is caught too. A
    // restart inside a loaded zone changes nothing the server can see, so only a tripped guard
    // refuses it.
    public static string? StartBlockedReason() => StartBlockedReason(out _);

    // `settling` names what the start is waiting on when that passes by itself within a few
    // seconds, so a caller can retry instead of refusing.
    public static string? StartBlockedReason(out string? settling)
    {
        settling = null;
        if (Current is { IsActive: true } active)
            return active.tripReason is { } tripped ? $"the session guard tripped ({tripped})" : null;
        // The previous stay's delayed lift is still pending; a new stay under it would lose its
        // firewall a second in.
        if (Current is { guardArmed: true }) return Settling("the previous run", out settling);
        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is not { } player) return "not logged in";
        if (!IsInInn()) return "not in an inn";
        var c = Plugin.Condition;
        if (c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51]) return "zoning";
        if (SecondsSince(lastTerritoryChangeAt) < SettleSeconds) return Settling("the zone", out settling);
        if (player.IsCasting) return $"casting {ActionLookup.Name(player.CastActionId)}";
        if (zoneChangePressedAt is { } pressed)
            return $"{ActionLookup.Name(zoneChangeActionId)} was used {Stopwatch.GetElapsedTime(pressed).TotalSeconds:F0}s ago and has not resolved";
        if (IsPlayerBusy()) return "busy (cutscene, NPC event, crafting, trading, zoning, combat, mounted, queued, etc.)";
        // Nothing the character did during the hold reached the server.
        if (Current is { sendHoldActive: true }) return Settling("the debug send hold", out settling);
        if (SecondsSince(lastBusyAt) < SettleSeconds) return Settling("the last action", out settling);
        return null;
    }

    private static string Settling(string subject, out string? settling)
    {
        settling = subject;
        return $"{subject} is still settling";
    }

    // ---- Session guard ------------------------------------------------------------------

    private bool guardArmed;
    private long guardArmedAt;
    private int stayId;
    private string? tripReason;
    // Dalamud's territory follows real zone-ins only (the sim's own loads leave it on the inn),
    // so anything else is one. GameMain's reads 0 after the sim's load, so it is only logged.
    private uint innClientTerritory;
    private uint loadedTerritory;
    private uint lastNativeTerritory;
    // Position and territory are the whole surface the sim can corrupt; the lift checks the live
    // ones against these before the client may report them.
    private Vector3 armedPosition;
    private float armedRotation;
    private const float LiftPositionTolerance = 2f;
    // The territory the last zone load finished; 0 while one is under way or after one threw. The
    // only proof the inn reload happened: Dalamud's reading never follows the sim's loads.
    private uint completedLoad;
    private long lastHeartbeatAt;
    private const double HeartbeatSeconds = 30;
    // The detours may not run on the framework thread, and the snapshot reads these from it.
    private long heldInbound;
    private readonly ConcurrentDictionary<ushort, long> heldOutbound = new();

    private static uint NativeTerritory()
    {
        var gm = GameMain.Instance();
        return gm == null ? 0 : gm->CurrentTerritoryTypeId;
    }

    private string? TerritoryDrift()
    {
        var client = Plugin.ClientState.TerritoryType;
        if (client != innClientTerritory && client != loadedTerritory)
            return $"the client's territory reads {client}, neither the inn ({innClientTerritory}) nor the loaded zone ({loadedTerritory})";
        return null;
    }

    private const string NoLocalPlayer = "there is no local player to check the position against";

    private string? PositionDrift()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } player) return NoLocalPlayer;
        var here = player.Position;
        if (!Finite(here) || !Finite(armedPosition))
            return $"a position is not a real number (player {Describe(here, player.Rotation)}, inn {Describe(armedPosition, armedRotation)})";
        var apart = Vector3.Distance(here, armedPosition);
        if (apart > LiftPositionTolerance)
            return $"the character is {apart:F1}y from where the inn left them (player {Describe(here, player.Rotation)}, inn {Describe(armedPosition, armedRotation)})";
        return null;
    }

    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    private static string Describe(Vector3 p, float rotation) => $"({p.X:F2},{p.Y:F2},{p.Z:F2}) rot {rotation:F2}";

    private static string Describe(Vector3? p) => p is { } v ? $"({v.X:F2},{v.Y:F2},{v.Z:F2})" : "none";

    // Everything the guard judges, so a FATAL dump says what the client looked like rather than
    // only which check failed.
    private string StateSnapshot(string where)
    {
        try
        {
            return BuildStateSnapshot(where);
        }
        catch (Exception e)
        {
            return $"[ZoneGuard] {where} -- snapshot failed ({e.GetType().Name}: {e.Message})";
        }
    }

    private string BuildStateSnapshot(string where)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        var here = player is { } p ? Describe(p.Position, p.Rotation) : "none";
        var apart = player is { } q ? $"{Vector3.Distance(q.Position, armedPosition):F2}y" : "n/a";
        var outbound = heldOutbound.Count == 0
            ? "none"
            : string.Join(",", heldOutbound.OrderByDescending(kv => kv.Value).Select(kv => $"0x{kv.Key:X4}x{kv.Value}"));
        var conditions = string.Join(",", Enum.GetValues<ConditionFlag>()
            .Where(f => (int)f != 0 && Plugin.Condition[f]).Select(f => f.ToString()).Distinct());
        return $"[ZoneGuard] {where} -- stay #{stayId}, {Stopwatch.GetElapsedTime(guardArmedAt).TotalSeconds:F2}s in, armed={guardArmed}, sessionActive={IsActive}, trip={tripReason ?? "none"}; "
             + $"player {here}, inn {Describe(armedPosition, armedRotation)}, apart {apart}; "
             + $"territory Dalamud={Plugin.ClientState.TerritoryType} GameMain={NativeTerritory()} (inn {innClientTerritory}, loaded {loadedTerritory}, last finished load {completedLoad}); "
             + $"loggedIn={Plugin.ClientState.IsLoggedIn}; hooks send={sendPacketHook.IsEnabled}/disposed={sendPacketHook.IsDisposed} recv={receivePacketHook.IsEnabled}/disposed={receivePacketHook.IsDisposed}; "
             + $"held {heldInbound} inbound, outbound [{outbound}]; conditions [{conditions}]";
    }

    private void LogStayHeartbeat()
    {
        if (Stopwatch.GetElapsedTime(lastHeartbeatAt).TotalSeconds < HeartbeatSeconds) return;
        lastHeartbeatAt = Stopwatch.GetTimestamp();
        DiagnosticLog.Info(StateSnapshot("stay heartbeat"));
    }

    private void LogNativeTerritoryChange()
    {
        var native = NativeTerritory();
        if (native == lastNativeTerritory) return;
        DiagnosticLog.Info($"[ZoneGuard] GameMain territory {lastNativeTerritory} -> {native}, {Stopwatch.GetElapsedTime(guardArmedAt).TotalSeconds:F2}s into the stay (logged only).");
        lastNativeTerritory = native;
    }

    // Enter refuses a start whose firewall is not actually up; a hook that failed to install
    // after a patch would otherwise arm nothing and load the zone anyway.
    private bool FirewallArmed(out string why)
    {
        if (!sendPacketHook.IsEnabled || sendPacketHook.IsDisposed) { why = "the send filter did not arm"; return false; }
        if (!receivePacketHook.IsEnabled || receivePacketHook.IsDisposed) { why = "the receive filter did not arm"; return false; }
        if (heartbeatOpcode == 0) { why = "no heartbeat opcode"; return false; }
        why = "";
        return true;
    }

    // sessionSave was filled from the live player one statement earlier in Enter.
    private void CaptureInnState()
    {
        innClientTerritory = Plugin.ClientState.TerritoryType;
        lastNativeTerritory = NativeTerritory();
        armedPosition = sessionSave.Position;
        armedRotation = sessionSave.Rotation;
    }

    private void ArmGuard(uint territoryId)
    {
        loadedTerritory = territoryId;
        tripReason = null;
        heldInbound = 0;
        heldOutbound.Clear();
        guardArmedAt = Stopwatch.GetTimestamp();
        lastHeartbeatAt = guardArmedAt;
        pendingLift = null;
        liftHoldLoggedReason = null;
        stayId++;
        guardArmed = true;
        DiagnosticLog.Info($"[ZoneGuard] Armed for territory {territoryId}: inn territory {innClientTerritory}, Dalamud now reads {Plugin.ClientState.TerritoryType}, GameMain {lastNativeTerritory} -> {NativeTerritory()}.");
        DiagnosticLog.Info(StateSnapshot("armed"));
        lastNativeTerritory = NativeTerritory();
    }

    private void TickSessionGuard()
    {
        if (!guardArmed || tripReason != null) return;
        LogNativeTerritoryChange();
        LogStayHeartbeat();
        var c = Plugin.Condition;
        if (!sendPacketHook.IsEnabled || !receivePacketHook.IsEnabled)
            Trip("a firewall hook was found disabled while armed");
        else if (!Plugin.ClientState.IsLoggedIn)
            Trip("the client logged out while the firewall was up");
        else if (c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51])
            Trip("the client began a zone transition while the firewall was up");
        else if (c[ConditionFlag.LoggingOut])
            Trip("a logout began while the firewall was up");
        else if (TerritoryDrift() is { } drift)
            Trip($"{drift} while the firewall was up");
        else if (Plugin.ObjectTable.LocalPlayer is { IsCasting: true } player
                 && player.CastActionId is TeleportActionId or ReturnActionId
                 && player.CurrentCastTime > Stopwatch.GetElapsedTime(guardArmedAt).TotalSeconds + 0.05)
            Trip($"a {ActionLookup.Name(player.CastActionId)} cast begun before the firewall went up is completing on the server");

        if (pendingLift != null && tripReason == null) TryLift();
    }

    private void GuardTerritoryChanged(uint territory)
    {
        if (!guardArmed || tripReason != null || territory == innClientTerritory || territory == loadedTerritory) return;
        Trip($"the client processed a zone change to {territory} while the firewall was up");
    }

    private void GuardLogout(int type, int code)
    {
        if (!guardArmed || tripReason != null) return;
        Trip($"logout (type {type}, code {code}) while the firewall was up");
    }

    // Sticky, and fatal at once: none of the causes clears on its own, and the stay would only
    // drift further from the server.
    private void Trip(string reason)
    {
        tripReason = reason;
        DiagnosticLog.Warn(StateSnapshot($"TRIPPED: {reason}"));
        Die(reason);
    }

    // Dalamud's territory must read the inn again here: the reload has run, and only a real
    // zone-in moves that reading.
    private const string HookDisabled = "a firewall hook was found disabled";

    private string? LiftBlockedReason()
    {
        if (tripReason is { } tripped) return tripped;
        if (!sendPacketHook.IsEnabled || !receivePacketHook.IsEnabled) return HookDisabled;
        if (!Plugin.ClientState.IsLoggedIn) return "not logged in";
        var c = Plugin.Condition;
        if (c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51]) return "a zone transition is in progress";
        if (c[ConditionFlag.LoggingOut]) return "logging out";
        if (Plugin.ClientState.TerritoryType != innClientTerritory)
            return $"the client reports territory {Plugin.ClientState.TerritoryType}, not the inn ({innClientTerritory}) the firewall was armed in";
        if (completedLoad != innClientTerritory || loadedInstanceContent != null)
            return $"the inn reload did not complete (last finished load {completedLoad}, sim duty {(loadedInstanceContent is { } content ? content.ToString() : "none")})";
        // The engine finishes the reload over the next second and then sends its post-load packet;
        // its territory reads 0 until then. An unload cannot wait for it (no frames are left), so
        // only a lift that may retry holds on it.
        if (pendingLiftMayRetry && NativeTerritory() != innClientTerritory)
            return $"the client is still loading the inn (the game's territory reads {NativeTerritory()})";
        return TerritoryDrift() ?? PositionDrift();
    }

    private string? pendingLift;
    private bool pendingLiftMayRetry;
    private string? liftHoldLoggedReason;
    private long pendingLiftSince;
    private const double LiftRetrySeconds = 3;

    // Any blocker holds the lift with the firewall up and is re-checked every frame; the stay
    // dies if it hasn't cleared within LiftRetrySeconds. These two die at once instead: waiting
    // cannot improve a tripped stay, and a hook already off means the filter is down now.
    private bool MustDieNow(string reason) => reason == tripReason || reason == HookDisabled;

    // Once per stay: Dispose lifts an early one, and the delayed lift then finds nothing to do.
    // Dispose passes mayRetry: false, since there are no further frames to re-check in.
    private void LiftFirewallOrDie(string when, bool mayRetry = true)
    {
        if (!guardArmed) return;
        if (pendingLift == null)
        {
            pendingLift = when;
            pendingLiftMayRetry = mayRetry;
            pendingLiftSince = Stopwatch.GetTimestamp();
            DiagnosticLog.Info(StateSnapshot($"lift check ({when})"));
        }
        else
        {
            pendingLiftMayRetry &= mayRetry;
        }
        TryLift();
    }

    private void TryLift()
    {
        if (pendingLift is not { } when) return;
        if (LiftBlockedReason() is { } reason)
        {
            var waited = Stopwatch.GetElapsedTime(pendingLiftSince).TotalSeconds;
            if (pendingLiftMayRetry && !MustDieNow(reason) && waited < LiftRetrySeconds)
            {
                if (liftHoldLoggedReason == reason) return;
                liftHoldLoggedReason = reason;
                DiagnosticLog.Warn($"[ZoneGuard] Lift held {waited:F2}s in ({when}): {reason} -- firewall stays up, re-checking every frame until {LiftRetrySeconds:F0}s.");
                return;
            }
            DiagnosticLog.Warn(StateSnapshot($"LIFT REFUSED after {waited:F2}s ({when}): {reason}"));
            Die($"{reason} ({when}, unverified for {waited:F1}s)");
        }
        var player = Plugin.ObjectTable.LocalPlayer;
        var here = player is { } p ? Describe(p.Position, p.Rotation) : "none";
        pendingLift = null;
        liftHoldLoggedReason = null;
        guardArmed = false;
        if (!DisableFirewall()) Die($"a filter would not come down after the lift was verified ({when})");
        DiagnosticLog.Info($"[ZoneGuard] Firewall lifted {when}: territory {innClientTerritory} (GameMain {NativeTerritory()}), player {here} vs inn {Describe(armedPosition, armedRotation)}, held {heldInbound} inbound / {heldOutbound.Values.Sum()} outbound.");
    }

    private void LogStaySummary()
    {
        var top = string.Join(", ", heldOutbound.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"0x{kv.Key:X4}x{kv.Value}"));
        DiagnosticLog.Info($"[ZoneGuard] Stay held {heldInbound} inbound and {heldOutbound.Values.Sum()} outbound packets (outbound top: {top}).");
    }

    // ---- The stop -------------------------------------------------------------------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    private const uint MessageBoxErrorOnTop = 0x10 | 0x1000 | 0x10000 | 0x40000; // ICONERROR, SYSTEMMODAL, SETFOREGROUND, TOPMOST

    // Environment.FailFast runs no finalizers, so nothing on the way out (Plugin.Dispose
    // included) can lift the firewall.
    private static void Die(string reason)
    {
        var note = DiagnosticLog.Fatal($"[ZoneGuard] FATAL: {reason} -- stopping the game with the firewall up.");
        var text = "AnoMech stopped the game on purpose.\n\n"
                   + $"Reason: {reason}.\n\n"
                   + "The packet filter was still up, so nothing was sent to the server. Log in again; your character will be wherever the server has it.\n\n"
                   + (note != null ? $"Details: {note}" : "See dalamud.log.");
        try { MessageBoxW(0, text, "AnoMech safety stop", MessageBoxErrorOnTop); }
        catch { /* the stop must not depend on the dialog */ }
        Environment.FailFast($"AnoMech safety stop: {reason}");
    }
}
