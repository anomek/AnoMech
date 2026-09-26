using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.ClientState.Conditions;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// Random sequences of everything a player, the server, Dalamud and a buggy scenario can do, with
// the harness judging every packet, every hook change and every frame. Deterministic per seed;
// set ANOMECH_FUZZ_SEEDS to run more locally (the default keeps a CI run to seconds).
public class FuzzTests
{
    private const int DefaultSeedsPerBatch = 200;
    private const int Batches = 10;

    public static IEnumerable<object[]> SeedBatches() => Enumerable.Range(0, Batches).Select(b => new object[] { b });

    private static int SeedsPerBatch()
        => int.TryParse(Environment.GetEnvironmentVariable("ANOMECH_FUZZ_SEEDS"), out var total) && total > 0
            ? Math.Max(1, total / Batches)
            : DefaultSeedsPerBatch;

    [Theory]
    [MemberData(nameof(SeedBatches))]
    public void RandomSequences_KeepEveryInvariant(int batch)
    {
        var perBatch = SeedsPerBatch();
        for (var i = 0; i < perBatch; i++)
        {
            var seed = batch * 1_000_003 + i;
            var failure = FuzzRun.Run(seed);
            Assert.True(failure == null, failure);
        }
    }

    // A seed from a failure message, rerun alone with its full trace.
    [Fact]
    public void ReproduceSeed()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("ANOMECH_FUZZ_REPRO"), out var seed)) return;
        var failure = FuzzRun.Run(seed, fullTrace: true);
        Assert.True(failure == null, failure);
    }
}

internal sealed class FuzzRun
{
    private readonly Random rng;
    private readonly VirtualGame game;
    private readonly PluginDriver plugin;
    private readonly List<string> ops = new();
    private bool hazard;
    private bool loggedOut;
    private bool unloaded;
    private bool disableRefused;

    private FuzzRun(int seed)
    {
        rng = new Random(seed);
        var syncs = rng.Next(4) == 0;
        game = new VirtualGame(dalamudSyncsTerritory: syncs);
        plugin = new PluginDriver(game);
        game.FrameworkTasksBeforeUpdate = rng.Next(2) == 0;
        game.InnLoadSeconds = 0.3 + rng.NextDouble() * 1.9;
        game.SimLoadSeconds = 0.5 + rng.NextDouble() * 2.3;
        game.ServerLatencySeconds = rng.Next(4) == 0 ? rng.NextDouble() * 8 : rng.NextDouble() * 0.3;
        game.Backend = rng.Next(2) == 0 ? HookBackend.Reloaded : HookBackend.SafetyHook;
        game.DalamudSeesZoneInitAt = rng.Next(2) == 0 ? 0 : rng.NextDouble();
        game.FrameworkTaskOrder = new Random(rng.Next());
        ops.Add($"variant: dalamud syncs territory={syncs}, tasksFirst={game.FrameworkTasksBeforeUpdate}, innLoad={game.InnLoadSeconds:F2}s, simLoad={game.SimLoadSeconds:F2}s, latency={game.ServerLatencySeconds:F2}s, hooks={game.Backend}, zoneInitSeenAt={game.DalamudSeesZoneInitAt:F2}");
    }

    public static string? Run(int seed, bool fullTrace = false)
    {
        var run = new FuzzRun(seed);
        try
        {
            return run.Execute(seed, fullTrace);
        }
        catch (Exception e) when (e is not GameTerminated)
        {
            run.game.Violation($"the plugin threw out of a harness call: {e}");
            return run.Failure(seed, fullTrace);
        }
        finally
        {
            try
            {
                run.plugin.Dispose();
            }
            catch (Exception)
            {
            }
            run.game.Dispose();
        }
    }

    private string? Execute(int seed, bool fullTrace)
    {
        var steps = 60 + rng.Next(120);
        for (var step = 0; step < steps && !game.Dead && !loggedOut && !unloaded; step++)
        {
            Step();
            var frames = 1 + rng.Next(rng.Next(4) == 0 ? 240 : 30);
            var dt = rng.Next(3) switch { 0 => 1f / 30f, 1 => 1f / 60f, _ => 1f / 144f };
            for (var f = 0; f < frames && !game.Dead; f++)
            {
                game.Frame(dt);
                NoteHazards();
            }
            if (game.Violations.Count > 0) return Failure(seed, fullTrace);
        }
        if (!game.Dead && !loggedOut && !unloaded) Settle();
        if (game.Dead && !hazard)
            game.Violation($"FALSE KILL: the game was stopped ({game.DeathReason}) though nothing unsafe happened");
        return game.Violations.Count > 0 ? Failure(seed, fullTrace) : null;
    }

    private string Failure(int seed, bool fullTrace)
        => $"Fuzz seed {seed} (rerun with ANOMECH_FUZZ_REPRO={seed}):\nOps:\n  {string.Join("\n  ", fullTrace ? ops : ops.TakeLast(60))}\n{game.Report(fullTrace ? 2000 : 150)}";

    private bool Armed => plugin.Zone.IsActive || ZoneSessionProbe.GuardArmed(plugin.Zone);

    // Conditions under which stopping the game is a correct answer.
    private void NoteHazards()
    {
        // A filter that will not come down makes any lift, and so the stay's end, a stop.
        if (disableRefused) hazard = true;
        if (!Armed) return;
        if (game.IgnorePositionWrites || !game.LocalPlayerPresent || game.ThrowOnActorControl || game.InnLoadSeconds > 3.5
            || game.GameFlags.Contains(ConditionFlag.BetweenAreas) || game.GameFlags.Contains(ConditionFlag.BetweenAreas51)
            || game.GameFlags.Contains(ConditionFlag.LoggingOut) || game.ServerTerritory != VirtualGame.InnTerritory
            || game.ServerHasPendingZoneChange || game.ClientZoning)
            hazard = true;
    }

    private void Op(string what) => ops.Add($"{game.Now:F2}s {what}");

    private Vector3 ArenaPoint() => new(100 + (float)(rng.NextDouble() * 40 - 20), 0, 100 + (float)(rng.NextDouble() * 40 - 20));

    private void Step()
    {
        if (disableRefused) hazard = true;
        var roll = rng.Next(1000);
        switch (roll)
        {
            case < 130:
            {
                var zone = rng.Next(20) == 0
                    ? SimZone.Top with { TerritoryId = VirtualGame.TerritoryWithoutDutyEntry }
                    : rng.Next(2) == 0 ? SimZone.Top : SimZone.Umad;
                Op($"start {zone.Name}/{zone.TerritoryId}: {plugin.ClickStart(zone) ?? "accepted"}");
                break;
            }
            case < 230:
                Op("leave");
                plugin.ClickLeave();
                break;
            case < 270:
                Op("reset");
                plugin.ClickReset();
                break;
            case < 370:
                if (!plugin.ScenarioActive) goto default;
                var bad = rng.Next(50) == 0;
                var to = bad ? new Vector3(float.NaN, 0, 0) : ArenaPoint();
                Op($"sim moves the player to {VirtualGame.Fmt(to)}");
                plugin.SimMovesPlayer(to);
                break;
            case < 490:
                var stepV = new Vector3((float)(rng.NextDouble() - 0.5), 0, (float)(rng.NextDouble() - 0.5));
                Op($"walk {VirtualGame.Fmt(stepV)}");
                game.Walk(stepV);
                break;
            case < 530:
            {
                game.ServerAcceptsTeleport = rng.Next(5) != 0;
                var teleport = rng.Next(2) == 0;
                Op($"press {(teleport ? "Teleport" : "Return")} (server accepts: {game.ServerAcceptsTeleport})");
                game.PressZoneChangeAction(teleport ? VirtualGame.TeleportActionId : VirtualGame.ReturnActionId);
                break;
            }
            case < 560:
                Op("interrupt the cast");
                game.InterruptCast(new Vector3(0.3f, 0, 0));
                break;
            case < 620:
            {
                var flags = StartSpecification.BusyFlags.ToArray();
                var flag = flags[rng.Next(flags.Length)];
                var on = !game.GameFlags.Contains(flag);
                Op($"condition {flag} {(on ? "on" : "off")}");
                game.SetCondition(flag, on);
                break;
            }
            case < 650:
                game.LocalPlayerPresent = !game.LocalPlayerPresent;
                Op($"local player {(game.LocalPlayerPresent ? "back" : "gone")}");
                break;
            case < 665:
            {
                var where = rng.Next(3) switch { 0 => VirtualGame.CityTerritory, 1 => VirtualGame.OtherInnTerritory, _ => VirtualGame.InnTerritory };
                Op($"server moves the character to {where}");
                // With its zone change held, a server-only move during a stay is invisible to the
                // client: the documented blind spot. Only the detectable form is generated then.
                if (Armed || rng.Next(3) == 0) game.Config.ZoneDownOpcodes = [VirtualGame.KeepAliveInboundOpcode, VirtualGame.InitZoneInboundOpcode];
                else game.Config.ZoneDownOpcodes = [VirtualGame.KeepAliveInboundOpcode];
                // Dalamud reports the zone change as the client handles it, inside this call.
                hazard |= Armed;
                game.ServerMovesCharacter(where, where == VirtualGame.CityTerritory ? VirtualGame.CityAetherytePosition : VirtualGame.InnPosition, "fuzz");
                break;
            }
            case < 668:
                Op("log out");
                hazard |= Armed;
                game.LogOut();
                loggedOut = true;
                break;
            case < 673:
                Op("something else disables a filter hook");
                game.DisableHookExternally(rng.Next(2) == 0 ? HookRole.Send : HookRole.Receive);
                hazard |= Armed;
                break;
            case < 700:
                if (ZoneSessionProbe.SendHoldActive(plugin.Zone)) goto default;
                game.IgnorePositionWrites = !game.IgnorePositionWrites;
                Op($"position writes {(game.IgnorePositionWrites ? "ignored" : "taken")}");
                break;
            case < 710:
                game.ThrowOnActorControl = !game.ThrowOnActorControl;
                Op($"ActorControl {(game.ThrowOnActorControl ? "throws" : "works")}");
                break;
            case < 720:
                game.ThrowAfterNativeLoad = !game.ThrowAfterNativeLoad;
                Op($"sim load {(game.ThrowAfterNativeLoad ? "throws after the native load" : "works")}");
                break;
            case < 750:
            {
                // The debug hold's release restores the character but does not verify it (a
                // DEBUG-only bench, see the README); not exercised against a refusing engine.
                if (game.IgnorePositionWrites) goto default;
                var hold = rng.Next(2) == 0;
                Op($"debug hold {hold}");
                plugin.Map.HoldSendFirewall(hold);
                break;
            }
            case < 780:
                Op("stray map effect / weather / director update");
                plugin.Map.AddEffect((uint)rng.Next(), (byte)rng.Next(16));
                plugin.Map.SetWeather((byte)rng.Next(1, 20));
                plugin.Map.DirectorUpdate(0x80000004, 1);
                break;
            case < 790:
                Op("unload the plugin");
                plugin.Unload();
                unloaded = true;
                break;
            case < 800:
                game.InnLoadSeconds = rng.Next(4) == 0 ? 3.6 + rng.NextDouble() * 3 : 0.3 + rng.NextDouble() * 1.9;
                Op($"inn loads take {game.InnLoadSeconds:F2}s");
                break;
            case < 806:
            {
                var role = rng.Next(2) == 0 ? HookRole.Send : HookRole.Receive;
                if (rng.Next(2) == 0)
                {
                    Op($"the {role} hook will not enable ({game.Backend})");
                    game.RefuseToEnableHook(role);
                }
                else
                {
                    Op($"the {role} hook will not disable ({game.Backend})");
                    game.RefuseToDisableHook(role);
                    disableRefused = true;
                }
                break;
            }
            case < 812:
                Op("hook changes work again");
                game.AllowHookChanges();
                disableRefused = false;
                break;
            default:
                Op("wait");
                break;
        }
    }

    // Let every injected fault clear, leave, and require the client to end up back in the inn with
    // the firewall down (or stopped for a reason seen during the run).
    private void Settle()
    {
        Op("settle: faults cleared, leave");
        game.AllowHookChanges();
        game.IgnorePositionWrites = false;
        game.ThrowOnActorControl = false;
        game.ThrowAfterNativeLoad = false;
        game.LocalPlayerPresent = true;
        foreach (var flag in game.GameFlags.ToList()) game.SetCondition(flag, false);
        plugin.Map.HoldSendFirewall(false);
        // Stop cancels a start still waiting to settle; Leave only acts inside a sim.
        plugin.ClickReset();
        game.RunFor(1);
        plugin.ClickLeave();
        game.RunFor(12);
        if (game.Dead) return;
        if (plugin.Zone.IsActive || ZoneSessionProbe.GuardArmed(plugin.Zone))
            game.Violation("STUCK: a stay is still armed long after the last Leave");
        if (game.SendFilterUp || game.ReceiveFilterUp)
            game.Violation($"STUCK: a filter is still up after the last Leave (send {game.SendFilterUp}, receive {game.ReceiveFilterUp})");
        if (game.PluginHoldsOccupied) game.Violation("STUCK: the plugin left Occupied set");
        if (game.Divergence() is { Count: > 0 } d && game.LoadedTerritory == VirtualGame.InnTerritory)
            game.Violation($"the client ended the run diverged: {string.Join("; ", d)}");
    }
}
