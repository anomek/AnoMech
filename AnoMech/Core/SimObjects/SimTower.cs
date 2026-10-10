using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// EventObject-backed tower whose SharedGroup state is indexed by occupancy.
// `states[i]` is the state to display when exactly i party members stand
// within `radius` of the tower (XZ plane). Counts past states.Length-1 clamp
// to the last entry, so a 3-element array gives "empty / 1-inside / 2+ inside".
// Occupancy is sampled each tick via Party.Find.InsideCircle; the state only changes on a count
// change. The first sample is written directly, as retail sets the spawn state in the spawn
// packet; later changes go through ActorControl 409 (EObjSetState) with `stateUnk` as p2.
public sealed class SimTower : SimEventObject
{
    private readonly SimParty party;
    private readonly float radius;
    private readonly ushort[] states;
    private readonly uint stateUnk;
    private int? lastCount;

    private SimTower(IEventObjectProxy obj, Coordinates coordinates, uint eObjRowId,
                     ushort[] states, uint stateUnk, float radius, SimParty party, float lifetime, uint layoutId)
        : base(obj, coordinates, eObjRowId, states[0], lifetime, layoutId)
    {
        this.party = party;
        this.radius = radius;
        this.states = states;
        this.stateUnk = stateUnk;
    }

    internal static SimTower? Spawn(
        EventObjectSpawnConfig config, Coordinates coordinates, EventScheduler events,
        ushort[] states, uint stateUnk, float radius, SimParty party)
    {
        if (states == null || states.Length == 0)
        {
            Plugin.Log.Warning("SimTower: states array must contain at least one entry (states[0] = empty)");
            return null;
        }

        var placement = coordinates.ToGlobal(config.Placement);
        if (Natives.EventObjects.Spawn(config, placement) is not { } obj)
            return null;

        var worldPos = placement.Position;
        obj.SetPosition(worldPos);
        obj.SetRotation(MathUtil.NormalizeRotation(config.Placement.Rotation));

        var tower = new SimTower(obj, coordinates, config.EObjId, states, stateUnk, radius, party, config.Lifetime, config.LayoutId);

        Plugin.Log.Info($"SimTower: spawned EObj 0x{config.EObjId:X} at slot {obj.Slot} ({worldPos.X:F2},{worldPos.Y:F2},{worldPos.Z:F2}) radius={radius:F1} states=[{string.Join(",", states)}]");
        return tower;
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        if (!IsAlive) return;
        var count = party.Find.InsideCircle(Position, radius).Count;
        if (lastCount == count) return;
        var first = lastCount == null;
        lastCount = count;
        var state = states[count >= states.Length ? states.Length - 1 : count];
        if (first) SetState(state);
        else EObjSetState(state, stateUnk);
    }
}
