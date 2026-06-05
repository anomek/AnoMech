using System;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.SimObjects;

public sealed unsafe class SimStatus : ISimObject
{
    private readonly SimCharacter target;
    private float duration;
    private float elapsed;

    public ushort StatusId { get; }
    public bool IsActive { get; private set; }
    public ushort Stacks { get; private set; }

    internal SimStatus(SimCharacter target, ushort statusId, float duration, ushort stacks)
    {
        this.target = target;
        this.duration = duration;
        StatusId = statusId;
        IsActive = true;
        Stacks = stacks;
        Statuses.AddStatusInit((Character*)target.BattleCharaPtr, statusId, stacks);
    }

    public void Reapply(float duration, int stacks)
    {
        // Only a positive duration refreshes the timer; a bare stack change
        // (default duration 0) preserves the existing countdown.
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

        if (duration > 0 && elapsed >= duration)
        {
            Despawn();
        }
        else
        {
            if (duration > 0f)
                elapsed += deltaSeconds;
            // this should be done only for player status, but just for simplicity doing it always
            // This also incidentally updates timers on debuffs, which is needed for party members
            Statuses.Apply((Character*)target.BattleCharaPtr, StatusId, duration - elapsed, Stacks);
        }
    }

    public void Despawn()
    {
        if (!IsActive) return;
        Statuses.Remove((Character*)target.BattleCharaPtr, StatusId);
        IsActive = false;
    }
}
