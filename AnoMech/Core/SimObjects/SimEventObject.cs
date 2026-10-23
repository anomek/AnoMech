using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

public sealed partial record EventObjectSpawnConfig
{
    // References Lumina's EObj sheet,
    // the row's SgbPath/PopType drives the model picked by the engine's internal SharedGroup attach.
    // ModelChara substitution is not part of the EObj pipeline; pick the right EObj row.
    public uint EObjId { get; init; }

    // Placement.Position is scenario-local (offset from SimWorld.ScenarioOrigin),
    // same coordinate space as SimEventObject.Position once spawned.
    public Placement Placement { get; init; }

    public sbyte ObjectIndex { get; init; } = -1;
    public byte TargetableStatus { get; init; } = 1; // 1 - untargettable
    public bool Hidden { get; init; }
    public uint EntityId { get; init; } = 0;
    public uint LayoutId { get; init; } = 0;
    public EventId EventId { get; init; } = 0;
    public uint OwnerId { get; init; } = 0xE0000000;
    public uint GimmickId { get; init; } = 0;
    public float Radius { get; init; } = 1;
    public ushort FateId { get; init; } = 0;
    public byte EventState { get; init; } = 0;
    // SpawnObjectPacket +0x30; the real EObj spawns carry 3 in the low byte plus a per-object
    // index in the high word.
    public uint Arg2 { get; init; } = 0;

    // The SharedGroup state the EObj spawns in (SpawnObjectPacket +0x2C); 0 is the engine default.
    public ushort TimelineState { get; init; } = 0;

    public float Lifetime { get; init; } = 0;
}

// An EventObject in EventObjectManager's 40-slot pool, spawned and despawned through the
// client's own spawn/despawn-object packet handlers. EObjs render through their attached
// SharedGroup, not GameObject.DrawObject; after the spawn, every change goes through the
// ActorControl categories retail sends.
public partial class SimEventObject : ISimObject, IPositioned
{
    private IEventObjectProxy? obj;
    private float lifetimeElapsed;

    public uint EObjRowId => SpawnConfig.EObjId;

    public EventObjectSpawnConfig SpawnConfig { get; }

    public bool IsAlive => obj != null;

    // No death-vs-presence distinction for event objects: kept while the slot is live.
    public virtual bool IsActive => IsAlive;

    // Fixed at spawn: retail never moves an EObj (no ActorSetPos/ActorMove), it respawns one.
    public Vector3 Position { get; }
    public float Rotation { get; }

    protected SimEventObject(IEventObjectProxy obj, Coordinates coordinates, EventObjectSpawnConfig config)
    {
        this.obj = obj;
        SpawnConfig = config;
        Position = coordinates.ToLocal(obj.Position);
        Rotation = obj.Rotation;
    }

    internal static SimEventObject? Spawn(EventObjectSpawnConfig config, Coordinates coordinates, EventScheduler events)
    {
        if (Natives.EventObjects.Spawn(config, coordinates.ToGlobal(config.Placement)) is not { } spawned) return null;
        DiagnosticLog.Info($"[SimEventObject.Create] Spawned EObj with EObjId 0x{config.EObjId:X} at Slot: {spawned.Slot} {spawned.Position}");
        var eObj = new SimEventObject(spawned, coordinates, config);
        eObj.OnLegacySpawned();
        return eObj;
    }

    // ActorControl 607 (self, 1, 0, 100): the prop fades out, as sent to spent boulders and
    // resolved crystals.
    public void FadeOut() => obj?.ActorControl(607, obj.EntityId, 1, 0, 100);

    public void EObjSetState(ushort state, uint unk) => obj?.ActorControl(409, state, unk);

    public void DirectorEObjMod(uint state) => obj?.ActorControl(106, state);

    public void PlayBeat(uint state, uint bitmask) => obj?.ActorControl(413, state, bitmask);

    public virtual void Tick(float deltaSeconds)
    {
        TickLegacy();
        if (obj == null || SpawnConfig.Lifetime <= 0) return;

        lifetimeElapsed += deltaSeconds;
        if (lifetimeElapsed >= SpawnConfig.Lifetime) Despawn();
    }

    public void Despawn()
    {
        if (obj == null) return;
        BeforeLegacyDespawn();
        var released = obj;
        obj = null;
        released.Despawn();
        DiagnosticLog.Info($"[SimEventObject] Despawned slot {released.Slot}.");
    }
}
