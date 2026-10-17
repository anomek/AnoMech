using System;
using System.Numerics;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Scenarios.Uwu;

public record UwuUtilsRecords
{
    public uint ActionId { get; init; }
    public ActionType ActionType { get; init; }
    public float OmenDelay { get; init; }
    public float CastTime { get; init; }
    public bool Interruptible { get; init; }
    public float? Rotation { get; init; }
    public Vector3? Position { get; init; }
    public GameObjectId? Target { get; init; }
    public GameObjectId? BallistaTarget { get; init; }
}

public record ActionEffectInfo
{
    public uint ActionId { get; init; }
    public float AnimationLock { get; init; }
    public ushort SpellId { get; init; }
    public byte AnimationVariaton { get; init; }
    public ActionType ActionType { get; init; }
    public byte Flags { get; init; }
    public float? Rotation { get; init; }
    public Vector3? Position { get; init; }
    public GameObjectId? AnimationTarget { get; init; }
    public GameObjectId? ActionTarget { get; init; }
    public GameObjectId? BallistaTarget { get; init; }
}

public record DynamicInfo
{
    public Func<float>? CastRotation { get; init; }
    public Func<Vector3>? CastPosition { get; init; }
    public Func<float>? ActionEffectRotation { get; init; }
    public Func<Vector3>? ActionEffectPosition { get; init; }
    public Func<SimEnemy?>? CastTarget { get; init; }
    public Func<SimEnemy?>? CastBallistaTarget { get; init; }
    public Func<SimEnemy?>? ActionEffectAnimationTarget { get; init; }
    public Func<SimEnemy?>? ActionEffectActionTarget { get; init; }
    public Func<SimEnemy?>? ActionEffectBallistaTarget { get; init; }

    public float? GetValue(Func<float>? func) => func != null ? func() : null;
    public Vector3? GetValue(Func<Vector3>? func) => func != null ? func() : null;
    public GameObjectId? GetValue(Func<SimEnemy?>? func) => func != null ? func()!.GameObjectId : null;
}
