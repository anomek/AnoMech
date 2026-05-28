using System;
using System.Numerics;

namespace AnoMech.Core.Game;

// Translates between scenario-local coordinates (the SimXxx public API) and
// world/global coordinates (the engine's GameObject->Position). Local zero is
// the scenario origin; the transform is a pure translation, no rotation. Y is
// part of the offset, same as X/Z; Rotation passes through untouched.
//
// The origin is read live on every call (not snapshotted at construction) so a
// character that outlives a single scenario run — SimPlayer is a permanent
// fixture on SimWorld, and ScenarioOrigin is reset to default on teardown —
// always converts against whatever origin the current scenario set.
public sealed class Coordinates
{
    private readonly Func<Vector3> origin;
    public Coordinates(Func<Vector3> origin) => this.origin = origin;

    public Vector3 ToGlobal(Vector3 local) => origin() + local;
    public Vector3 ToLocal(Vector3 global) => global - origin();
    public Placement ToGlobal(Placement local) => new(ToGlobal(local.Position), local.Rotation);
    public Placement ToLocal(Placement global) => new(ToLocal(global.Position), global.Rotation);
}
