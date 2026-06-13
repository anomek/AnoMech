using AnoMech.Core.Game;
using AnoMech.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// EventObject-backed tower whose SharedGroup state is indexed by occupancy.
// `states[i]` is the state to display when exactly i party members stand
// within `radius` of the tower (XZ plane). Counts past states.Length-1 clamp
// to the last entry, so a 3-element array gives "empty / 1-inside / 2+ inside".
// Occupancy is sampled each tick via Party.Find.InsideCircle; SetState only
// fires on a count change so the engine SG notify isn't spammed every frame.
public sealed unsafe class SimTower : SimEventObject
{
    private readonly SimParty party;
    private readonly float radius;
    private readonly ushort[] states;
    private int? lastCount;

    private SimTower(int slot, GameObject* obj, SimWorld world, uint eObjRowId,
                     ushort[] states, float radius, SimParty party, float lifetime)
        : base(slot, obj, world, eObjRowId, states[0], lifetime)
    {
        this.party = party;
        this.radius = radius;
        this.states = states;
    }

    internal static SimTower? Spawn(
        EventObjectSpawnConfig config, SimWorld world, EventScheduler events,
        ushort[] states, float radius, SimParty party)
    {
        if (states == null || states.Length == 0)
        {
            Plugin.Log.Warning("SimTower: states array must contain at least one entry (states[0] = empty)");
            return null;
        }

        var packet = config.ToPacket(world);

        if (!EventObjectHelper.Create(&packet, out var slot, out var obj))
            return null;

        var worldPos = world.Coordinates.ToGlobal(config.Placement.Position);
        obj->SetPosition(worldPos.X, worldPos.Y, worldPos.Z);
        obj->SetRotation(MathUtil.NormalizeRotation(config.Placement.Rotation));

        var tower = new SimTower(slot, obj, world, config.EObjId, states, radius, party, config.Lifetime);

        Plugin.Log.Info($"SimTower: spawned EObj 0x{config.EObjId:X} at slot {slot} ({worldPos.X:F2},{worldPos.Y:F2},{worldPos.Z:F2}) radius={radius:F1} states=[{string.Join(",", states)}]");
        return tower;
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        if (!IsAlive) return;
        var count = party.Find.InsideCircle(Position, radius).Count;
        if (lastCount == count) return;
        lastCount = count;
        var idx = count >= states.Length ? states.Length - 1 : count;
        SetState(states[idx]);
    }
}
