using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

public sealed class SimStatus : ISimObject
{
    private readonly SimCharacter target;
    private float duration;
    private float elapsed;

    public ushort StatusId { get; }
    public GameObjectId SourceObject { get; }
    public bool IsActive { get; private set; }
    public ushort Stacks { get; private set; }
    public float Remaining => duration > 0f ? MathF.Max(0f, duration - elapsed) : float.PositiveInfinity;

    // Distinguishes a re-added status from one refreshed in place: only the add goes through
    // the engine's gain path, which is what applies a param-driven look.
    private static int nextInstance;
    public int Instance { get; } = ++nextInstance;

    // 0 = permanent until removed, so a peer replicating it gets the same behaviour.
    public float RemainingTime => duration > 0f ? Math.Max(0f, duration - elapsed) : 0f;

    internal SimStatus(SimCharacter target, ushort statusId, float duration, ushort stacks, GameObjectId sourceObject = default)
    {
        this.target = target;
        this.duration = duration;
        StatusId = statusId;
        SourceObject = sourceObject;
        IsActive = true;
        Stacks = stacks;
        target.Proxy?.AddStatusInit(statusId, stacks, sourceObject);
    }

    public void Reapply(float duration, int stacks)
    {
        if (duration > 0f)
        {
            this.duration = duration;
            elapsed = 0f;
        }
        // Negative stacks decrement; clamp to 0 (callers handle removal at 0).
        Stacks = (ushort)Math.Max(0, Stacks + stacks);
    }

    public void Tick(float deltaSeconds)
    {
        if (!IsActive) return;

        // Advance before checking expiry: checking first let one tick write a negative
        // RemainingTime, which the native StatusManager showed as 20s for a frame.
        if (duration > 0f) elapsed += deltaSeconds;

        if (duration > 0f && elapsed >= duration)
        {
            Despawn();
            return;
        }

        target.Proxy?.ApplyStatus(StatusId, duration - elapsed, Stacks, SourceObject);
    }

    public void Despawn()
    {
        if (!IsActive) return;
        target.Proxy?.RemoveStatus(StatusId, SourceObject);
        IsActive = false;
    }
}
