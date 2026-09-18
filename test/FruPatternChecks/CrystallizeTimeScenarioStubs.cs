using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.SimObjects;

public interface ISimPartyMember { PartyRole Role { get; } }
public partial class SimCharacter
{
    public List<(string Path, bool Persistent)> ActorVfx { get; } = [];
    public HashSet<string> ActiveActorVfx { get; } = [];
    public void AddVfx(string path, float duration = 0, bool persistent = true)
    { ActorVfx.Add((path, persistent)); if (persistent) ActiveActorVfx.Add(path); }
    public void RemoveVfx(string path) => ActiveActorVfx.Remove(path);
    public sealed class StatusHandle { public void Reapply(float duration, int stacks) { } }
    public StatusHandle? FindStatus(ushort status) => HasStatus(status) ? new() : null;
    public (Vector3 Position, float Speed)? ForcedMove { get; set; }
    public int StopAtMoveCount { get; private set; }
    public void StopMoving() { ForcedMove = null; StopAtMoveCount = Moves.Count; }
    public void Knockback(Vector3 source, float distance, float speed)
    {
        if (!this.IsAlive()) return;
        var away = Position - source;
        ForcedMove = (Position + (away.LengthSquared() > 0 ? Vector3.Normalize(away) : Vector3.UnitZ) * distance, speed);
        StopAtMoveCount = Moves.Count;
    }
    public void Advance(float delta)
    {
        if (!this.IsAlive()) return;
        var move = ForcedMove ?? (Moves.Count > StopAtMoveCount ? (Moves[^1].Target, Moves[^1].Speed) : (Position, 0));
        var offset = move.Item1 - Position;
        var step = move.Item2 * delta;
        if (offset.Length() <= step) { Position = move.Item1; ForcedMove = null; }
        else if (step > 0) Position += Vector3.Normalize(offset) * step;
    }
}
public sealed partial class SimPlayer
{
    public bool MechanicInputLock { get; private set; }
    public void SetMechanicInputLock(bool locked) => MechanicInputLock = locked;
}
public sealed partial class SimEnemy
{
    public float VisualHeight { get; private set; }
    public void SetVisualHeight(float height) => VisualHeight = height;
    public List<(ushort Start, ushort Loop)> Timelines { get; } = [];
    public void PlayActionTimeline(ushort timeline, ushort loop = 0) => Timelines.Add((timeline, loop));
    public uint Health { get; private set; }
    public uint MaxHealth { get; private set; }
    public void SetHealth(uint current, uint maximum) { Health = Math.Min(current, maximum); MaxHealth = maximum; }
    public float Scale { get; private set; } = 1;
    public void SetScale(float scale) => Scale = scale;
    public bool Visible { get; private set; } = true;
    public bool Targetable { get; private set; }
    public void SetVisible(bool visible) => Visible = visible;
    public void SetTargetable(bool targetable) => Targetable = targetable;
}
public class EventObjectSpawnConfig
{
    public uint EObjId { get; init; }
    public Placement Placement { get; init; }
    public ushort TimelineState { get; init; }
}
public sealed class SimEventObject(EventObjectSpawnConfig config) : ISimObject
{
    public EventObjectSpawnConfig Config => config;
    public bool IsActive { get; private set; } = true;
    public void Despawn() => IsActive = false;
    public void Tick(float deltaSeconds) { }
}
public sealed class SimTether(ushort id) : ISimObject
{
    public ushort Id => id;
    public bool IsActive { get; private set; } = true;
    public void Despawn() => IsActive = false;
    public void Tick(float deltaSeconds) { }
}
public sealed partial class SimWorld
{
    public SimOmen SpawnOmen(string path, Placement placement, Vector3 scale)
    { var result = new SimOmen(path, placement, scale); Spawned.Add(result); return result; }
    public SimEventObject SpawnEventObject(EventObjectSpawnConfig config)
    { var result = new SimEventObject(config); Spawned.Add(result); return result; }
    public SimTether Tether(SimCharacter? from, SimCharacter? to, ushort id)
    { var result = new SimTether(id); Spawned.Add(result); return result; }
}
public sealed class SimOmen(string path, Placement placement, Vector3 scale) : ISimObject
{
    public string Path => path;
    public Placement Placement => placement;
    public Vector3 Scale => scale;
    public bool IsActive { get; private set; } = true;
    public void Despawn() => IsActive = false;
    public void Tick(float deltaSeconds) { }
}
