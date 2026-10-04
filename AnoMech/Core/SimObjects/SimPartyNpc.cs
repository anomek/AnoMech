using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.SimObjects;

public sealed class SimPartyNpc : SimNpc, ISimPartyMember
{
    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }
    public byte ClassJob { get; }
    public string DisplayName { get; }

    internal SimPartyNpc(IBattleCharaProxy proxy, Coordinates coordinates, PartyRole role, byte classJob, string name) : base(proxy, coordinates)
    {
        Role = role;
        ClassJob = classJob;
        DisplayName = name;
    }

    // A bot's button press: the animation, then the same JobActions effects a player's press applies.
    private void UseAction(uint actionId)
    {
        PlayAction(actionId);
        JobActions.ApplyEffects(this, actionId, (ulong)GameObjectId, Random.Shared);
    }

    // level 1-3. False if KO'd or the job has no limit break at that level.
    internal bool UseLimitBreak(int level)
    {
        if (!this.IsAlive()) return false;
        var actionId = LimitBreakHandler.ActionId(ClassJob, level);
        if (actionId == 0) return false;
        DiagnosticLog.Info($"[SimPartyNpc] {Role} (job {ClassJob}) uses LB{level} {ActionLookup.Name(actionId)}.");
        UseAction(actionId);
        return true;
    }

    // False for a non-tank job.
    public bool UseInvuln()
    {
        if (Mitigation.InvulnActionId(ClassJob) is not { } actionId) return false;
        UseAction(actionId);
        return true;
    }

    public void UseSprint(float duration) => SprintHandler.Apply(this, duration);

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    public void PushInDirection(float heading, float distance, float speed) => Movement.PushInDirection(heading, distance, speed);

    public void PushInDirectionEased(float heading, float distance, float durationSeconds) => Movement.PushInDirectionEased(heading, distance, durationSeconds);

    public void WalkInDirection(float heading, float distance, float speed) => Movement.WalkInDirection(heading, distance, speed);

    public override void Despawn()
    {
        base.Despawn();
    }

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        AbortPlayedAction();
        if (Proxy is not { Exists: true } chara) return;
        chara.ApplyDeadState();
        this.PlayKoActionTimeline();
    }
}
