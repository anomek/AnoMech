using System;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using Dalamud.Game.Text;

namespace AnoMech.Core.Game;

// High-level orchestrator: owns the World, holds the scenario catalog, and routes UI calls to
// the current InstanceSession (the zone stay) and ScenarioSession (the run inside it).
public sealed class Game : IDisposable
{
    public EventScheduler Events { get; } = new();
    public SimWorld World { get; }  
    public ScenarioCatalog Catalog { get; } = ScenarioCatalog.Create();
    public float EventTimeScale { get; set; } = 1f;
    public bool GodMode { get; set; }
    
    public SimPlayer? Player => World.Party.Player;
    public IZone? Zone => instance?.Zone;
    public SessionMode? Mode => instance?.Mode;
    public IScenario? ActiveScenario => run?.Scenario;
    public bool Paused => run?.Paused ?? false;
    public SessionStatus Status => run?.Status ?? (instance is null ? SessionStatus.Offline : SessionStatus.InZone);
    // The current run, else the one that ended last, so its result outlives a Stop.
    public ScenarioSession? LastRun => run ?? endedRun;
    
    // bool: a local run, not a multiplayer one.
    public event Action<RunScenarioParams, bool>? RunStarted;
    public event Action<bool>? RunFinished;
    public event Action<string>? MechanicFailed;
    // statusId names the status that killed, for a death no action dealt (DieOfStatus).
    public event Action<PartyRole, string, uint?, ushort?>? PartyMemberKilled;
    // Back in the inn: a Leave, or the native zone going away under the stay.
    public event Action? InstanceExited;

    private InstanceSession? instance;
    private ScenarioSession? run;
    private ScenarioSession? endedRun;

    public Game()
    {
        World = new SimWorld(Events);
    }


    public void ZoneIn(IZone zone, SessionMode mode, Action<string?>? resolved = null)
        => Deferred(() => ZoneInNow(zone, mode), resolved);

    public void StartScenario(RunScenarioParams p, Action<string?>? resolved = null)
        => Deferred(() => StartScenarioNow(p), resolved);

    private static readonly TimeSpan ZoneInStartDelay = TimeSpan.FromSeconds(2);

    // Zones in first unless already staying in the run's zone in `mode`; a stay entered in another
    // mode refuses the run rather than playing it there. A fresh zone-in prepares the run at once
    // and waits ZoneInStartDelay before it starts.
    public void EnterAndStart(RunScenarioParams p, SessionMode mode, Action<string?>? resolved = null)
    {
        var zone = p.Scenario.Phase.Zone;
        if (Zone == zone && Mode == mode)
        {
            StartScenario(p, resolved);
            return;
        }
        p = p with { Seed = p.Seed ?? Random.Shared.Next() };
        ZoneIn(zone, mode, refusal =>
        {
            if (refusal != null)
            {
                resolved?.Invoke(refusal);
                return;
            }
            PrepareRunNow(p);
            Plugin.Framework.RunOnTick(() => StartScenario(p, resolved), ZoneInStartDelay);
        });
    }

    // The stage a run starts on, set before it: the phase applied and the party seated, so the
    // doppels are standing by the time the run talks to them. p.Seed must be set, and passed to
    // the StartScenario that follows: the run reseeds to the stream the party was seated from, and
    // its bots keep their ring spots.
    public void PrepareRun(RunScenarioParams p, Action<string?>? resolved = null)
        => Deferred(() =>
        {
            if (instance is null || instance.Zone != p.Scenario.Phase.Zone) return $"not in {p.Scenario.Phase.Zone.Name}";
            PrepareRunNow(p);
            return null;
        }, resolved);

    private void PrepareRunNow(RunScenarioParams p)
    {
        instance?.ApplyPhase(p.Scenario.Phase);
        var player = Natives.BattleCharas.LocalPlayer;
        if (!player.Exists) return;
        World.Reseed(p.Seed!.Value);
        World.SeatParty(p.Party.Roster(player.ClassJob));
    }

    public void Reset() => Plugin.Framework.Run(() =>
    {
        if (ActiveScenario is not null)
            instance?.ReturnPlayerIfOutsideArena();
        EndRun();
    });

    public void Leave()
    {
        // Leaving always finalizes its own log segment.
        DiagnosticLog.RotateNow();
        Plugin.Framework.Run(LeaveNow);
    }

    // Framework thread only.
    public void LeaveNow()
    {
        EndRun();
        ExitInstance();
    }


    // Null once in the zone, else why not.
    private string? ZoneInNow(IZone zone, SessionMode mode)
    {
        if (instance is not null)
        {
            Plugin.Log.Warning($"Game: refusing to enter {zone.Name} -- already in {instance.Zone.Name}.");
            return $"already in {instance.Zone.Name}";
        }
        instance = InstanceSession.Enter(World, zone, mode, out var refusal);
        if (refusal is not null) Plugin.Log.Warning($"InstanceSession: not entering {zone.Name} -- {refusal}.");
        return refusal;
    }

    // Null once the run is up, else why it was refused.
    private string? StartScenarioNow(RunScenarioParams p)
    {
        if (instance is null)
        {
            Plugin.Log.Warning($"Game: refusing to start {p.Scenario.Name} -- not in a zone.");
            return "not in a zone";
        }
        if (ScenarioSession.Create(World, instance, p, out var refusal) is not { } next) return refusal;

        EndRun();
        run = next;
        run.Finished += OnRunFinished;
        run.MechanicFailed += OnMechanicFailed;
        run.Start();
        RunStarted?.Invoke(p, instance.Mode == SessionMode.Local);
        return null;
    }
    
    private void EndRun()
    {
        if (run is { } ending)
        {
            run = null;
            endedRun = ending;
            ending.Finished -= OnRunFinished;
            ending.MechanicFailed -= OnMechanicFailed;
            ending.End();
        }
    }

    private void ExitInstance()
    {
        if (instance is null) return;
        instance.Exit();
        instance = null;
        InstanceExited?.Invoke();
    }
    
    public void Tick(float deltaSeconds)
    {
        // The native zone can go away under a live stay.
        if (instance is { IsLoaded: false })
        {
            Plugin.Log.Warning($"Game: {instance.Zone.Name} is no longer loaded; ending the stay.");
            EndRun();
            ExitInstance();
        }
        instance?.Tick(deltaSeconds);
        if (instance?.CatchFall() == true && run is not null)
            World.Party.Player?.Die(SimCharacterDeathExtensions.Environment, "Fell off the arena");
        if (Paused) return;
        Events.Tick(deltaSeconds * EventTimeScale);
        World.Tick(deltaSeconds * EventTimeScale);
        run?.Tick(deltaSeconds * EventTimeScale);
    }

    // Reports the death even in godmode, so the user learns what would have killed them; only the
    // actual kill is skipped. Returns false if nothing died (already dead or godmode), so callers
    // gate their own on-death logic on it.
    public bool Kill(ISimPartyMember target, string cause, uint? actionId = null, ushort? statusId = null)
    {
        if (target == null) return false;
        if (target.Dead) return false;

        DiagnosticLog.Warn(
            $"[Game] Kill: {target.Role} died at ({(target as IPositioned)?.Position.X:F1},{(target as IPositioned)?.Position.Z:F1}) -- {cause}");
        Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] {DescribeName(target)} died: {cause}" });
        run?.OnDeathReported(DescribeName(target), cause);

        if (GodMode)
        {
            // TODO: don't kill player but indicate damage was fatal
            return false;
        }
        target.OnKilled(byEnvironment: actionId == SimCharacterDeathExtensions.Environment);
        PartyMemberKilled?.Invoke(target.Role, cause, actionId, statusId);
        run?.OnKilled();
        return true;
    }


    // Synchronous: Plugin.Dispose runs on the framework thread during unload, and a
    // Framework.Run wrapper would never fire.
    public void Dispose()
    {
        run = null;
        Events.Clear();
        instance?.Dispose();
        instance = null;
        World.Dispose();
    }
    
    private void OnRunFinished(bool clean) => RunFinished?.Invoke(clean);
    private void OnMechanicFailed(string reason) => MechanicFailed?.Invoke(reason);
    

    private static string DescribeName(ISimPartyMember target) => target switch
    {
        SimPlayer => "You",
        SimPartyNpc pm => pm.DisplayName,
        SimNetworkPuppet pm => pm.DisplayName,
        _ => "Character",
    };
    
    // `resolved` runs in the same deferred callback, with why it was refused, or null once done;
    // an exception counts as a refusal.
    private static void Deferred(Func<string?> action, Action<string?>? resolved)
    {
        Plugin.Framework.Run(() =>
        {
            string? refusal;
            try
            {
                refusal = action();
            }
            catch (Exception e)
            {
                resolved?.Invoke($"the scenario threw {e.GetType().Name} while loading");
                throw;
            }
            resolved?.Invoke(refusal);
        });
    }
}

// A null Seed draws a fresh one on every start
// A null SelectedAi runs no strat: solo, or a peer
public sealed record RunScenarioParams(IScenario Scenario, IPartyProvider Party, int? SelectedAi, int SelectedWaymark, int? Seed = null);
