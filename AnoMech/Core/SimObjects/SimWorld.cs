using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Scenarios;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// Holds the live game state Game manipulates, in two lifetimes: Map and Party last the zone stay,
// the children (enemies, tethers, waymarks, ...) one run, gone in Despawn. Spawn entry points
// (ApplyParty, SpawnEnemy, Tether, PlaceWaymarks, EnforceArenaBoundary) construct the SimObject
// and register it for teardown. Zone loading and map effects go through world.Map.
public sealed class SimWorld : ISimObject, IDisposable
{
    // Ownership
    private readonly SimChildren children = new();
    private readonly EnemyList enemyList = new();

    // Zone loading and map effects entry point.
    public MapController Map { get; } = new();

    // Scenario geometry: areas party bots steer around while moving. Empty by
    // default (straight-line movement). Scenarios mutate it over their timeline
    // (Add/Remove/Clear); bots hold this one instance across runs, cleared on Despawn.
    public ObstacleField Obstacles { get; } = new();

    // No members outside a zone stay. Ticked before the children, which reference it.
    public SimParty Party { get; } = new();
    public IEnumerable<ISimObject> Children => children;
    // Root container — Game owns its lifetime; no parent reaps it.
    public bool IsActive => true;
    public EventScheduler Events { get; }
    // The zone's origin for as long as an InstanceSession holds it; outlives the runs inside it.
    public Vector3 ScenarioOrigin { get; set; }

    // The run's mechanic randomness; engine noise draws from a named Stream instead, so it
    // can't shift these rolls. Replaced per run by Game (see Reseed).
    public Rng Rng { get; private set; } = Rng.Detached;
    private readonly Dictionary<string, Rng> streams = new();

    public void Reseed(int seed)
    {
        Rng = new Rng(seed);
        streams.Clear();
    }

    // This run's independent stream for one consumer, created on first use.
    public Rng Stream(string name)
    {
        if (!streams.TryGetValue(name, out var stream))
            streams[name] = stream = Rng.Fork(name);
        return stream;
    }

    // Converts between scenario-local coordinates (the SimXxx public API) and
    // world/global coordinates (the engine's GameObject->Position). Shared by
    // every SimCharacter (injected as a protected field) and used by spawners
    // for the pre-construction native writes. Reads ScenarioOrigin live.
    public Coordinates Coordinates { get; }

    public SimWorld(EventScheduler events)
    {
        Events = events;
        Coordinates = new Coordinates(() => ScenarioOrigin);
    }

    // A fixed end is just the character; dynamic ends are End.Passable(...) /
    // End.FarthestPlayer(). `from` hosts the channeling VFX (drawn from → to), so
    // argument order picks the visual direction. The overload pair enforces "at
    // least one fixed end" at compile time: two dynamic ends match neither overload
    // (nor the both-character convenience) and won't compile — no runtime guard.
    public SimTether Tether(SimCharacter? from, ITetherEnd to, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
        => CreateTether(End.Fixed(from), to, tetherId, duration, debuffStatusId);
    public SimTether Tether(DynamicEnd from, SimCharacter? to, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
        => CreateTether(from, End.Fixed(to), tetherId, duration, debuffStatusId);
    public SimTether Tether(SimCharacter? a, SimCharacter? b, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
        => CreateTether(End.Fixed(a), End.Fixed(b), tetherId, duration, debuffStatusId);
    public SimTether TetherFarestPlayer(SimCharacter? a, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
        => CreateTether(End.Fixed(a), End.FarthestPlayer(), tetherId, duration, debuffStatusId);


    private SimTether CreateTether(ITetherEnd from, ITetherEnd to, ushort tetherId, float duration, ushort debuffStatusId)
    {
        var tether = new SimTether(from, to, new TetherContext(Party.Find, tetherId, Rng), debuffStatusId, duration, SimEvents);
        children.Add(tether);
        return tether;
    }

    
    public SimEnemy? SpawnEnemy(EnemySpawnConfig config)
    {
        var enemy = SimEnemy.Spawn(config, this);
        if (enemy != null) children.Add(enemy);
        return enemy;
    }

    // Allocates an EventObject actor in EventObjectManager's 40-slot pool and
    // wires it to the given EObj sheet row. Mirror of SpawnEnemy for the EObj
    // side of the engine — see SimEventObject / EventObjectSpawn for details.
    public SimEventObject? SpawnEventObject(EventObjectSpawnConfig config)
    {
        var eo = SimEventObject.Spawn(config, Coordinates, Events);
        if (eo != null) children.Add(eo);
        return eo;
    }

    // EventObject tower variant — picks `states[count]` each tick based on how
    // many party members stand within `radius` of the EObj (counts past the
    // array length clamp to the last entry). Bound to the current Party so AI
    // and scenario movement drive the visual.
    public SimTower? SpawnTower(EventObjectSpawnConfig config, ushort[] states, uint stateUnk, float radius)
    {
        var tower = SimTower.Spawn(config, Coordinates, Events, states, stateUnk, radius, Party);
        if (tower != null) children.Add(tower);
        return tower;
    }

    // Places the scenario's waymark layout (scenario-local offsets), replacing any markers already
    // placed.
    public void PlaceWaymarks(IReadOnlyList<Waymark> layout)
        => children.GetOrAdd(() => new SimWaymarks(Coordinates)).Place(layout);

    // Per-frame arena fence at `radius` from ScenarioOrigin. Kills any active
    // party member (player included) who leaves the ring.
    public void EnforceArenaBoundary(float radius, string cause = "Walked out of arena")
        => children.SetSingleton(new SimArenaBoundary(Party, radius, cause));

    // Where the server stops an NPC a knockback pushes: a circle this far from ScenarioOrigin. Set by
    // the zone for the run; without it a push goes its full distance.
    public float? NpcKnockbackStopRadius { get; private set; }

    public void StopNpcKnockbacksAt(float radius) => NpcKnockbackStopRadius = radius;

    // How far an NPC at `position` (scenario-local) pushed away from `from` goes, at most `distance`.
    public float NpcPushDistance(Vector3 position, Vector3 from, float distance)
    {
        if (NpcKnockbackStopRadius is not { } radius) return distance;
        var start = new Vector2(position.X, position.Z);
        var away = start - new Vector2(from.X, from.Z);
        if (away == Vector2.Zero) return distance;
        var direction = Vector2.Normalize(away);
        var along = Vector2.Dot(start, direction);
        var discriminant = along * along - (start.LengthSquared() - radius * radius);
        var toCircle = discriminant <= 0f ? 0f : MathF.Max(0f, -along + MathF.Sqrt(discriminant));
        return MathF.Min(distance, toCircle);
    }

    // Plays for the rest of the run, replacing any track already playing.
    public void PlayBgm(ushort bgmId, float secondsIn = 0f)
        => children.SetSingleton(new SimBgm()).Play(bgmId, secondsIn);

    // The fight's own change of track (IBgm.Switch); nothing while music is suppressed. Each client
    // plays its own music, so these belong in RunInstanceEvents.
    public void SwitchBgm(ushort bgmId) => children.Singleton<SimBgm>()?.Switch(bgmId);

    public void SilenceBgm() => children.Singleton<SimBgm>()?.Silence();

    // True when `local` (scenario-local) is outside the active arena fence; false
    // when the current scenario enforces no boundary.
    public bool IsOutsideArena(Vector3 local)
        => children.Singleton<SimArenaBoundary>()?.IsOutside(local) ?? false;

    // A scenario message the whole party should see (which arrow was missed, why everyone just
    // died). Printed here and mirrored to peers, which have no other channel for mid-run text.
    public event Action<string>? Announced;

    public void Announce(string text)
    {
        Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] {text}" });
        Announced?.Invoke(text);
    }

    // The run failed without anyone dying, e.g. a puzzle retail only punishes minutes later. Play
    // the mechanic out as retail does; `freeze` when the scenario can't go on from here, which
    // freezes the run 5s later as a death does.
    public event Action<string, bool>? Failed;

    public void Fail(string reason, bool freeze = false) => Failed?.Invoke(reason, freeze);

    internal SimEvents SimEvents { get; } = new();

    // A one-shot VFX sheet row at a scenario-local point, e.g. FRU's Return marker (1013) under each
    // player. Shown on this client only.
    public void PlaceGroundVfx(uint vfxId, Vector3 position)
        => Party.Player?.ActorControl.PlaceGroundVfx(vfxId, Coordinates.ToGlobal(position));

    // Seats the run's roster, spawning only the seats that changed since the last run. Must be
    // called after ScenarioOrigin is set and the run reseeded: the spawn ring draws from it.
    public void ApplyParty(IReadOnlyList<PartySlot?> roster)
    {
        SeatParty(roster);
        Party.SetInCombat(true);
    }

    // ApplyParty without entering combat: the party standing in the zone before a run starts.
    public void SeatParty(IReadOnlyList<PartySlot?> roster) => Party.Apply(roster, this);

    public void Tick(float deltaSeconds)
    {
        Map.Tick();
        Party.Tick(deltaSeconds);
        children.Update(deltaSeconds);
        enemyList.Tick(children.OfType<SimEnemy>(), deltaSeconds);
        SimEvents.Dispatch();
    }

    // The run's teardown; the party stays. The listener goes first, so the teardown raises nothing.
    public void Despawn()
    {
        SimEvents.Clear();
        children.Despawn();
        enemyList.Clear();
        Obstacles.Clear();
        NpcKnockbackStopRadius = null;
        Party.EndRun();
    }

    public void DespawnParty() => Party.Despawn();

    public void Dispose()
    {
        Despawn();
        DespawnParty();
    }
}
