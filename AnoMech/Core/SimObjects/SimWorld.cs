using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;

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
    private readonly EnmityHud enmityHud = new();
    private readonly PartyHud partyHud = new();
    private readonly Waymarks waymarks;

    // Zone loading and map effects entry point.
    public MapController Map { get; } = new();

    // Convenience reference — SimParty.Empty until CreateParty is called.
    public SimParty Party { get; private set; } = SimParty.Empty;
    public IEnumerable<ISimObject> Children => children;
    // Root container — Game owns its lifetime; no parent reaps it.
    public bool IsActive => true;
    public EventScheduler Events { get; }
    public Vector3 ScenarioOrigin { get; set; }

    // Converts between scenario-local coordinates (the SimXxx public API) and
    // world/global coordinates (the engine's GameObject->Position). Shared by
    // every SimCharacter (injected as a protected field) and used by spawners
    // for the pre-construction native writes. Reads ScenarioOrigin live.
    public Coordinates Coordinates { get; }

    public SimWorld(EventScheduler events)
    {
        Events = events;
        Coordinates = new Coordinates(() => ScenarioOrigin);
        waymarks = new Waymarks(Coordinates);
    }

    public SimTether Tether(SimCharacter? a, SimCharacter? b, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
    {
        return Tether(a, () => b, tetherId, duration, debuffStatusId);
    }

    public SimTether TetherFarestPlayer(SimCharacter? a, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
    {
        return Tether(a, () => a is null ? null : Party.Find.Farest(a.Position), tetherId, duration, debuffStatusId);
    }
    

    // Passable tether: target is fixed, source migrates each tick to whichever
    // alive party member stands on the beam between current source and target.
    // The half-width of the beam corridor is 0.5f (~1 yalm). Candidates who
    // already host this tether id at slot 0 are skipped — naturally
    // coordinating parallel passable tethers of the same id without an
    // external shared set.
    public SimTether TetherPassable(SimCharacter? source, SimEnemy? target, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
    {
        SimCharacter? currentSource = source;
        Func<SimCharacter?> sourceResolver = () =>
        {
            if (currentSource is not { } cs || !cs.IsAlive()) return currentSource;
            if (target is not { IsActive: true }) return currentSource;
            var srcPos = cs.Position;
            var tgtPos = target.Position;
            var dx = tgtPos.X - srcPos.X;
            var dz = tgtPos.Z - srcPos.Z;
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 0.01f) return currentSource;
            var placement = new Placement(srcPos, MathF.Atan2(dx, dz));
            var candidates = Party.Find.InsideRect(placement, 0.5f, len);
            SimCharacter? best = null;
            var bestDistSq = float.MaxValue;
            foreach (var c in candidates)
            {
                if (ReferenceEquals(c, cs)) continue;
                if (c.HasTetherInSlot0(tetherId)) continue;
                var ddx = c.Position.X - srcPos.X;
                var ddz = c.Position.Z - srcPos.Z;
                var d = ddx * ddx + ddz * ddz;
                if (d < bestDistSq) { bestDistSq = d; best = c; }
            }
            if (best != null) currentSource = best;
            return currentSource;
        };
        var tether = new SimTether(sourceResolver, () => target, tetherId, debuffStatusId, duration);
        children.Add(tether);
        return tether;
    }
    
    public SimTether Tether(SimCharacter? a, Func<SimCharacter?> b, ushort tetherId, float duration = 0f, ushort debuffStatusId = 0)
    {
        var tether = new SimTether(a, b, tetherId, debuffStatusId, duration);
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
        var eo = SimEventObject.Spawn(config, this, Events);
        if (eo != null) children.Add(eo);
        return eo;
    }

    // EventObject tower variant — picks `states[count]` each tick based on how
    // many party members stand within `radius` of the EObj (counts past the
    // array length clamp to the last entry). Bound to the current Party so AI
    // and scenario movement drive the visual.
    public SimTower? SpawnTower(EventObjectSpawnConfig config, short[] states, float radius)
    {
        var tower = SimTower.Spawn(config, this, Events, states, radius, Party);
        if (tower != null) children.Add(tower);
        return tower;
    }

    // Places the scenario's waymark layout. Offsets are scenario-relative;
    // Waymarks resolves them through Coordinates. Cleared in Reset (like
    // Markings) — Waymarks is a writer owned here, not a tracked child.
    public void PlaceWaymarks(IReadOnlyList<Waymark> layout)
        => waymarks.Place(layout);

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

    // Spawns a standalone AOE telegraph (omen StaticVfx) that auto-expires after
    // `durationSeconds` and is cleaned up on world reset. `placement` is scenario-local
    // (like the rest of the SimXxx API); SimOmen lifts it to world coords. `scale`
    // follows SimOmen's convention: scale.X = halfWidth, scale.Z = length for rect omens.
    public void SpawnOmen(string path, Placement placement, Vector3 scale, float durationSeconds)
        => children.Add(new SimOmen(Coordinates, path, placement, scale, durationSeconds));

    // Spawns the eight party slots and wires in the local player. Must be called
    // after ScenarioOrigin is set. Party is added first so it despawns last in
    // Reset's reverse-order teardown (tethers and enemies reference slot positions).
    public void CreateParty(uint playerJob, PartyRole? roleOverride = null, bool solo = false)
    {
        var party = new SimParty();
        PartyCreator.Populate(party, new SimPlayer(Coordinates), playerJob, this, roleOverride, solo);
        children.Add(party);
        Party = party;
    }

    public void Tick(float deltaSeconds)
    {
        Map.Tick();
        children.Update(deltaSeconds);
        enmityHud.Refresh(children.OfType<SimEnemy>(), deltaSeconds);
        partyHud.Refresh(Party);
    }

    public void Despawn()
    {
        children.Despawn();
        Party = SimParty.Empty;
        enmityHud.Clear();
        partyHud.Clear();
        Markings.ClearAll();
        waymarks.ClearAll();
        ScenarioOrigin = default;
    }

    public void Dispose()
    {
        Despawn();
        enmityHud.Dispose();
        Map.Dispose();
    }
}
