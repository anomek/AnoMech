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

// Holds the live game state Game manipulates: a children list of SimObjects
// (party, enemies, tethers, waymarks, hidden objects). Spawn entry points
// (CreateParty, SpawnEnemy, Tether, PlaceWaymarks, HideObject,
// EnforceArenaBoundary) construct the SimObject and register it for teardown.
// Zone loading and map effects go through world.Map.
public sealed class SimWorld : ISimObject, IDisposable
{
    // Ownership
    private readonly List<ISimObject> children = new();

    // Zone loading and map effects entry point.
    public MapController Map { get; } = new();

    // Scenario geometry: areas party bots steer around while moving. Empty by
    // default (straight-line movement). Scenarios mutate it over their timeline
    // (Add/Remove/Clear); wired to each doppel by PartyCreator, cleared on Despawn.
    public ObstacleField Obstacles { get; } = new();

    // Convenience reference — SimParty.Empty until CreateParty is called.
    public SimParty Party { get; private set; } = SimParty.Empty;
    public IEnumerable<ISimObject> Children => children;
    // Root container — Game owns its lifetime; no parent reaps it.
    public bool IsActive => true;
    public EventScheduler Events { get; }
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
        var tether = new SimTether(from, to, new TetherContext(Party.Find, tetherId, Rng), debuffStatusId, duration);
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
    public SimTower? SpawnTower(EventObjectSpawnConfig config, ushort[] states, float radius)
    {
        var tower = SimTower.Spawn(config, Coordinates, Events, states, radius, Party);
        if (tower != null) children.Add(tower);
        return tower;
    }

    // Places the scenario's waymark layout (scenario-local offsets). Cleared in Despawn, not a
    // tracked child.
    public void PlaceWaymarks(IReadOnlyList<Waymark> layout)
    {
        foreach (var waymark in layout)
        {
            if (Natives.Waymarks.Set(waymark.Slot, Coordinates.ToGlobal(waymark.Offset))) continue;
            Plugin.Log.Warning("Waymarks: MarkingController unavailable");
            return;
        }
    }

    // Suppress a native GameObject (by BaseId) for the duration of the scenario.
    public void HideObject(uint baseId)
    {
        var hidden = SimHiddenObject.Hide(baseId);
        if (hidden != null) children.Add(hidden);
    }

    // Per-frame arena fence at `radius` from ScenarioOrigin. Kills any active
    // party member (player included) who leaves the ring, and spawns a VFX border.
    public void EnforceArenaBoundary(float radius, string cause = "Walked out of arena")
        => children.Add(new SimArenaBoundary(Party, this, radius, cause, showVfx: !Map.IsInInstance));

    // True when `local` (scenario-local) is outside the active arena fence; false
    // when the current scenario enforces no boundary.
    public bool IsOutsideArena(Vector3 local)
        => children.OfType<SimArenaBoundary>().FirstOrDefault()?.IsOutside(local) ?? false;

    // Mirrored to peers by MultiplayerManager (host only, so a replayed SpawnOmen doesn't loop):
    // an omen isn't a SimObject the snapshot sync walks.
    public event Action<string, Placement, Vector3, float>? OmenSpawned;

    // A scenario message the whole party should see (which arrow was missed, why everyone just
    // died). Printed here and mirrored to peers, which have no other channel for mid-run text.
    public event Action<string>? Announced;

    public void Announce(string text)
    {
        Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] {text}" });
        Announced?.Invoke(text);
    }

    // Spawns a standalone AOE telegraph (omen StaticVfx) that auto-expires after
    // `durationSeconds` and is cleaned up on world reset. `placement` is scenario-local
    // (like the rest of the SimXxx API); SimOmen lifts it to world coords. `scale`
    // follows SimOmen's convention: scale.X = halfWidth, scale.Z = length for rect omens.
    // The live count is remotely driven on a peer, and each omen holds a native VfxObject.
    public bool CanSpawnOmen => children.Count(c => c is SimOmen) < Multiplayer.NetGuard.MaxLiveOmens;

    public void SpawnOmen(string path, Placement placement, Vector3 scale, float durationSeconds)
    {
        children.Add(new SimOmen(Coordinates, path, placement, scale, durationSeconds));
        OmenSpawned?.Invoke(path, placement, scale, durationSeconds);
    }

    // Standalone telegraph derived from `actionId`'s own Omen sheet entry (shape/scale
    // read from Action.CastType/EffectRange/XAxisModifier), for a boss ability whose real
    // telegraph is duty-scripted rather than driven by the caster's own cast. SimCast
    // bypasses Character::StartCast, so the native auto-omen never fires on its own.
    public void SpawnActionOmen(uint actionId, Vector3 origin, float rotation, float durationSeconds)
        => children.Add(new SimOmen(Coordinates, actionId, origin, rotation, durationSeconds));

    // Change the active weather mid-scenario. weatherId is a Weather-sheet row;
    // transition is the fade-in time in seconds. A scenario's default weather
    // (TargetInstance.WeatherId) is re-applied automatically on restart, so any
    // mid-run change made here is reset whenever the scenario is run again.
    public void SetWeather(byte weatherId, float transition = 0.5f)
        => Map.SetWeather(weatherId, transition);

    // Spawns the eight party slots and wires in the local player. Must be called
    // after ScenarioOrigin is set. Party is added first so it despawns last in
    // Reset's reverse-order teardown (tethers and enemies reference slot positions).
    // networkRoles: multiplayer slots claimed by other real participants — see
    // PartyCreator.Populate.
    public void CreateParty(uint playerJob, PartyRole? roleOverride = null, bool solo = false, IReadOnlySet<PartyRole>? networkRoles = null, IReadOnlyDictionary<PartyRole, NetworkSeat>? networkSeats = null)
    {
        var party = new SimParty();
        PartyCreator.Populate(party, new SimPlayer(Coordinates), playerJob, this, roleOverride, solo, networkRoles, networkSeats);
        party.SetInCombat(true);
        children.Add(party);
        Party = party;
    }

    public void Tick(float deltaSeconds)
    {
        Map.Tick();
        Natives.VfxSpawnLog.Tick();
        Natives.BattleCharas.SweepOrphans();
        children.Update(deltaSeconds);
        Natives.EnmityHud.Refresh(children.OfType<SimEnemy>(), deltaSeconds);
        Natives.PartyHud.Refresh(Party);
    }

    public void Despawn()
    {
        children.Despawn();
        Party = SimParty.Empty;
        Natives.EnmityHud.Clear();
        Natives.PartyHud.Clear();
        Natives.Markings.ClearAll();
        Natives.Waymarks.ClearAll();
        Obstacles.Clear();
        ScenarioOrigin = default;
    }

    public void Dispose()
    {
        Despawn();
    }
}
