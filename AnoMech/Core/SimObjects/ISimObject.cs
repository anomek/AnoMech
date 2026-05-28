using System.Collections.Generic;

namespace AnoMech.Core.SimObjects;

// Game-object rules for AnoMech.Core.SimObjects:
// 1. SimObjects may tick to update state.
// 2. SimObjects own their children and cascade Tick and Despawn.
// 3. SimObjects must be created via their parent's spawn API
//    (e.g. SimWorld.SpawnEnemy), never constructed directly by consumers.
// 4. Tick and Despawn must be safe to call repeatedly, including after Despawn.
// 5. A container that ticks children must, immediately after the tick pass and
//    before anything reads the collection, reap them: Despawn() and drop every
//    child whose IsActive is false (see SimReap). Children self-report liveness;
//    parents reclaim.
// 6. Helpers (status writers, VFX bridges, native function wrappers, HUD
//    mirrors) live in AnoMech.Core, NOT here.
public interface ISimObject
{
    void Tick(float deltaSeconds);

    // True while this object is still doing its job and its container should keep
    // it. When false after a Tick, the container Despawns it and drops it from its
    // collection. This is a presence concept, distinct from a character's IsAlive:
    // a dead-but-present party doppel (KO'd, lying on the floor) is still IsActive.
    bool IsActive { get; }

    void Despawn();
}


public static class SimObjectExtensions
{
    public static void Update<T>(this List<T> children, float tick) where T : ISimObject
    {
        children.ForEach(child => child.Tick(tick));
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i].IsActive) continue;
            children[i].Despawn();
            children.RemoveAt(i);
        }
    }
    
    public static void Despawn<T>(this List<T> children) where T : ISimObject
    {
        children.ForEach(child => child.Despawn());
        children.Clear();
    }
}
