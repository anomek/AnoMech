using System;
using AnoMech.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

public sealed class SimStatus : ISimObject
{
    private readonly SimCharacter target;
    private float duration;
    private float elapsed;

    public ushort StatusId { get; }
    public GameObjectId SourceObject { get; }

    // Who the client is told applied it; unlike SourceObject, not part of the status's identity.
    internal GameObjectId? ShownSource { get; set; }
    public bool IsActive { get; private set; }
    public ushort Stacks { get; private set; }

    // The StatusManager slot the client shows it in; -1 for a status the client never sees.
    internal int Slot { get; }

    // In effect for the sim but left out of the status list until Reveal, like a status the server
    // only shows the client with the action's EffectResult.
    internal bool Hidden { get; private set; }

    // 0 = permanent until removed.
    public float RemainingTime => duration > 0f ? Math.Max(0f, duration - elapsed) : 0f;

    internal SimStatus(SimCharacter target, ushort statusId, float duration, ushort stacks, GameObjectId sourceObject, int slot, bool hidden)
    {
        this.target = target;
        this.duration = duration;
        StatusId = statusId;
        SourceObject = sourceObject;
        IsActive = true;
        Stacks = stacks;
        Slot = slot;
        Hidden = hidden;
    }

    // Shows `applier` as the status's source, e.g. a boss's own transformation.
    public SimStatus ShownAsFrom(SimCharacter applier)
    {
        ShownSource = applier.GameObjectId;
        return this;
    }

    // While it lasts, every other status on its holder keeps its remaining time; its own runs on
    // (TEA's Temporal Displacement).
    internal bool HoldsOtherTimers { get; private set; }

    public SimStatus HoldingOtherTimers()
    {
        HoldsOtherTimers = true;
        return this;
    }

    internal void Reveal()
    {
        if (!Hidden) return;
        Hidden = false;
        if (IsActive && Slot >= 0) target.StatusListChanged();
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
        if (!IsActive || duration <= 0f || (target.StatusTimersHeld && !HoldsOtherTimers)) return;
        elapsed += deltaSeconds;
        if (elapsed >= duration) End(StatusLossReason.Expired);
    }

    public void Despawn() => End(StatusLossReason.Removed);

    // A null reason raises nothing: the character itself is going away, or leaving the run.
    internal void End(StatusLossReason? reason)
    {
        if (!IsActive) return;
        IsActive = false;
        if (Slot >= 0 && !Hidden)
        {
            target.StatusListChanged();
            target.ActorControl.LoseEffect(StatusId, Stacks, SourceObject);
        }
        if (reason is { } why) target.SimEvents?.Raise(new StatusLost(target, StatusId, Stacks, why));
    }
}
