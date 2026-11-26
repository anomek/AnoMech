using System.Linq;
using System.Numerics;
using AnoMech.Core.Map;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Core.Game;

// One stay in a client-side zone, from the load out of the inn to Leave. Outlives the scenario
// runs played inside it: what it sets up (zone, origin, job-gauge snapshot, spawn wall) a restart
// must not redo.
public sealed class InstanceSession
{
    private const float EntranceBarrierOpenTime = 2f;
    private const uint DirectorEObjModCategory = 0x6A;
    private const uint EntranceBarrierOpenState = 7;

    private readonly SimWorld world;
    private readonly Vector3 playerSpawnLocal;
    private IEventObjectProxy? entranceBarrier;
    private float entranceBarrierOpenIn = -1f;
    private bool phaseApplied;
    private bool playerPlaced;

    public IZone Zone { get; }

    // Also who entered the stay: a run from another mode is refused rather than played in it.
    public SessionMode Mode { get; }

    // False once the native zone went away without a Leave.
    public bool IsLoaded => Natives.Zone.IsActive;

    public bool IsReady => Natives.Zone.IsReady(mapEffects: Zone.DutyInitData.MapEffectSnapshot.Count > 0);

    private InstanceSession(SimWorld world, IZone zone, Vector3 playerSpawnLocal, SessionMode mode)
    {
        this.world = world;
        this.playerSpawnLocal = playerSpawnLocal;
        Zone = zone;
        Mode = mode;
    }

    // I wish we could just return Either<string refusal, InstanceSession>
    public static InstanceSession? Enter(SimWorld world, IZone zone, SessionMode mode, out string? refusal)
    {
        // Hard gate: scenarios only ever run from an inn, and only from a state the server isn't
        // about to act on. Everything downstream (CharacterManager registration, zone load, doppel
        // spawn) assumes the inn; a deferred call may land in a state the click didn't see, and
        // ZoneControl.Enter asks once more before the firewall goes up.
        refusal = Natives.Zone.StartBlockedReason(out _)
                  ?? (Natives.BattleCharas.LocalPlayer.Exists ? null : "no local player");
        if (refusal is not null) return null;
        
        var init = zone.DutyInitData;
        if (!Natives.Zone.IsActive) // defensive check, should always be true
        {
            AddRsData(init); // need to be done before Enter
            if (!Natives.Zone.Enter(zone.TerritoryId, init.EntranceBarrier.Position))
            {
                refusal = "the zone was not entered (see the log)";
                return null;
            }
        }
        world.Map.Enter(zone.TerritoryId);
        var session = new InstanceSession(world, zone, init.EntranceBarrier.Position - zone.Origin, mode);
        // Before any action mutates it, so Exit can restore it.
        Natives.UserActions.OnSessionStart();
        world.ScenarioOrigin = zone.Origin;
        session.OpenEntranceBarrier(init.EntranceBarrier);
        return session;
    }

    private static void AddRsData(DutyInitData data)
    {
        foreach (var record in data.Rsv) Natives.Rsv.Add(record);
        foreach (var record in data.Rsf) Natives.Rsf.Add(record);
    }

    // Each run's phase. The first one gets retail's zone-in treatment (settled weather, zone-in
    // snapshot header), every later one the wipe-reset one.
    public void ApplyPhase(IPhase phase)
    {
        var zoneIn = !phaseApplied;
        phaseApplied = true;
        world.Map.SetPhaseWeather(phase.Weather, zoneIn);
        if (Zone.DutyInitData.MapEffectSnapshot.Count > 0)
            world.Map.SetEffectSnapshot(ArenaMapEffects(phase), zoneIn);
    }

    // Entering the zone always starts at spawn; later runs only recenter a player standing outside
    // the arena ring.
    public void PlacePlayer()
    {
        if (playerPlaced)
        {
            ReturnPlayerIfOutsideArena();
            return;
        }
        playerPlaced = true;
        TeleportPlayerToSpawn();
    }

    // No-op when the run enforces no boundary, so it must run before the run's teardown drops it.
    // Reads the live game-object position: a SimPlayer created this frame hasn't ticked yet.
    public void ReturnPlayerIfOutsideArena()
    {
        var lp = Natives.BattleCharas.LocalPlayer;
        if (!lp.Exists) return;
        if (!world.IsOutsideArena(world.Coordinates.ToLocal(lp.Position))) return;
        TeleportPlayerToSpawn();
    }

    private void TeleportPlayerToSpawn() => world.Party.Player?.SetPosition(playerSpawnLocal);

    // True when the player was below FallFloor and has been put back at the centre, with
    // retail's answer to the client's own fall report. Caught early on purpose: moving a corpse back
    // after a long fall (2.45s in) left the client unable to fall or act again. Retail's own
    // placement (ActorSetPos type 25 or 5) starts a client load, and with it the zone guard's trip
    // and a desync with the real server ("too far" after leaving).
    public bool CatchFall()
    {
        if (fallCloseIn >= 0f) return false;
        var lp = Natives.BattleCharas.LocalPlayer;
        if (!lp.Exists || world.Coordinates.ToLocal(lp.Position).Y >= FallFloor) return false;
        playerControl.FellOutOfArena();
        lp.SetPos(world.Coordinates.ToGlobal(Vector3.Zero), lp.Rotation);
        fallCloseIn = FallCloseDelay;
        return true;
    }

    // Scenario-local Y below which the player has fallen off the arena. The whole stay puts them
    // straight back at the centre, and a running scenario also kills them.
    private const float FallFloor = -5f;
    private const float FallCloseDelay = 2f;
    // Below the platform but not yet caught: the client has (or is about to have) reported the fall.
    private const float FallingBelow = -0.5f;
    private float fallCloseIn = -1f;
    private readonly ActorControl playerControl = new(() => Natives.BattleCharas.LocalPlayer);

    public void Tick(float deltaSeconds)
    {
        if (fallCloseIn >= 0f && (fallCloseIn -= deltaSeconds) < 0f) CloseFall();
        if (entranceBarrierOpenIn < 0 || (entranceBarrierOpenIn -= deltaSeconds) >= 0) return;
        entranceBarrier?.ActorControl(DirectorEObjModCategory, EntranceBarrierOpenState);
    }

    // Back to the inn.
    public void Exit()
    {
        world.DespawnParty();
        Dispose();
        Natives.Zone.Revert();
        world.Map.Unload();
        world.ScenarioOrigin = default;
    }

    // Plugin unload: the native zone's own dispose stops the game while a stay is up.
    public void Dispose()
    {
        SettleFall();
        Natives.UserActions.OnSessionEnd();
        entranceBarrier?.Despawn();
        entranceBarrier = null;
        entranceBarrierOpenIn = -1f;
    }

    private void CloseFall()
    {
        playerControl.FallRecovered();
        Natives.PlayerConditions.EndClientLoad();
        fallCloseIn = -1f;
    }

    // Leaving mid-fall: the client would carry the fall into the inn, still falling (the inn restore
    // only moves the character) and with its fall report unanswered (no falling, "too far" at every
    // interaction until a real zone change). Judged by height, not by the report itself: its
    // outbound opcode moves every patch and nothing keeps it current.
    private void SettleFall()
    {
        var lp = Natives.BattleCharas.LocalPlayer;
        var falling = lp.Exists && world.Coordinates.ToLocal(lp.Position).Y < FallingBelow;
        if (falling)
        {
            if (fallCloseIn < 0f) playerControl.FellOutOfArena();
            lp.SetPos(world.Coordinates.ToGlobal(Vector3.Zero), lp.Rotation);
        }
        if (falling || fallCloseIn >= 0f) CloseFall();
    }

    // As retail's zone-in spawn: LayoutId binds the EObj to its layout placement, GimmickId to the
    // wall SharedGroup. Retail sends the open state at commence, seconds after zone-in; here it
    // only has to come after the SharedGroup attaches (~1s after the spawn). Lives outside
    // SimWorld so a run's teardown doesn't despawn it.
    private void OpenEntranceBarrier(EntranceBarrier entrance)
    {
        var config = new EventObjectSpawnConfig
        {
            EObjId = entrance.EObjId,
            LayoutId = entrance.EObjLayoutId,
            GimmickId = entrance.WallLayoutId,
            TargetableStatus = 5,
        };
        entranceBarrier = Natives.EventObjects.Spawn(config, new Placement(entrance.Position, entrance.Rotation));
        entranceBarrierOpenIn = EntranceBarrierOpenTime;
    }

    private ushort[] ArenaMapEffects(IPhase phase)
    {
        var states = Zone.DutyInitData.MapEffectSnapshot.ToArray();
        foreach (var (slot, state) in phase.ArenaMapEffectOverrides)
            states[slot] = state;
        return states;
    }
}
