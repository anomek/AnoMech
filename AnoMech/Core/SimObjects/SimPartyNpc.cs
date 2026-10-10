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
    // Bots press only party-wide mitigation, never their own, and only while mitigation is required.
    private bool UseAction(uint actionId)
    {
        if (!Mitigation.Required) return false;
        PlayAction(actionId);
        JobActions.ApplyEffects(this, actionId, (ulong)GameObjectId, Random.Shared);
        return true;
    }

    // level 1-3. The action id used; 0 if KO'd, mitigation is not required, or the job has no limit
    // break at that level.
    internal uint UseLimitBreak(int level)
    {
        if (!this.IsAlive() || ActionsLocked) return 0;
        var actionId = LimitBreakHandler.ActionId(ClassJob, level);
        if (actionId == 0 || !UseAction(actionId)) return 0;
        DiagnosticLog.Info($"[SimPartyNpc] {Role} (job {ClassJob}) uses LB{level} {ActionLookup.Name(actionId)}.");
        return actionId;
    }

    public void UseSprint(float duration)
    {
        if (!ActionsLocked) SprintHandler.Apply(this, duration);
    }

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    public void PushInDirection(float heading, float distance, float speed) => Movement.PushInDirection(heading, distance, speed);

    public void PushInDirectionEased(float heading, float distance, float durationSeconds) => Movement.PushInDirectionEased(heading, distance, durationSeconds);

    public override void Despawn()
    {
        base.Despawn();
    }

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        if (Proxy is not { Exists: true } chara) return;
        chara.ApplyDeadState();
        this.PlayKoActionTimeline();
    }
}
