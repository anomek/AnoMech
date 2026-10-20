namespace AnoMech.Core.Native.Interfaces;

// ParamEffect: what the status's param does (MovementSpeed reads it).
public sealed record StatusRow(ushort Id, string Name, bool LockMovement, bool LockActions, bool LockControl, byte MaxStacks, byte ParamEffect);
