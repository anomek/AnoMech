using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.SimObjects;

public sealed class SimPlayer(Coordinates coordinates) : SimCharacter(coordinates), ISimPartyMember
{
    private const ushort StunStatusId = 896;  // "Down for the Count" (896) — IsPermanent + LockControl variant.
    // Out-of-combat speed buff; it only ends on entering battle, which the firewalled server never signals.
    private const ushort JogStatusId = 4209;

    // The real HP bar is only touched on a scenario KO (a 1-HP sliver), restored in RestoreHpBar.
    public void DropHpBar()
    {
        if (Proxy is { Exists: true } chara) chara.Health = 1;
    }

    public void RestoreHpBar()
    {
        if (Proxy is { Exists: true } chara && chara.Health < chara.MaxHealth) chara.Health = chara.MaxHealth;
    }

    // Real native MaxHealth before it was overridden; null if inactive.
    private uint? realMaxHealth;

    // Host-authoritative HP for a peer's own character; the real MaxHealth is captured once so
    // Despawn restores it no matter what a host sent.
    public void ApplyNetworkHp(uint currentHp, uint maxHp)
    {
        if (Proxy is not { Exists: true } chara || maxHp == 0) return;
        realMaxHealth ??= chara.MaxHealth;
        chara.MaxHealth = maxHp;
        chara.Health = Math.Min(currentHp, maxHp);
    }

    // Must run before RestoreHpBar: restore MaxHealth first, then clamp Health down.
    public void RestoreRealMaxHealth()
    {
        if (realMaxHealth is not { } original) return;
        if (Proxy is { Exists: true } chara)
        {
            chara.MaxHealth = original;
            if (chara.Health > original) chara.Health = original;
        }
        realMaxHealth = null;
    }

    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }

    // For stillness/movement mechanics: IsMoving = movement input, a jump, any action, or an
    // in-flight debug-bot MoveTo; IsActing also counts auto-attacks. Forced false while KO'd.
    public bool IsMoving { get; private set; }
    public bool IsActing { get; private set; }

    internal override IBattleCharaProxy Proxy => Natives.BattleCharas.LocalPlayer;

    private protected override PlayerMovement Movement => field ??= new PlayerMovement(this);

    // Only a debug bot presses it for you; a human tank presses their own.
    public bool UseInvuln()
    {
        if (!DebugBotControl.Enabled || Dead) return false;
        if (Mitigation.InvulnActionId(Proxy.ClassJob) is not { } actionId) return false;
        JobActions.ApplyEffects(this, actionId, (ulong)GameObjectId, Random.Shared);
        return true;
    }

    public void UseSprint(float duration)
    {
        if (DebugBotControl.Enabled && !Dead) SprintHandler.Apply(this, duration);
    }

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    public void PushInDirection(float heading, float distance, float speed) => Movement.PushInDirection(heading, distance, speed);

    public void PushInDirectionEased(float heading, float distance, float durationSeconds) => Movement.PushInDirectionEased(heading, distance, durationSeconds);

    // The input lock is re-derived every tick from Dead/Movement/statuses.
    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        if (Proxy is { Exists: true } chara) chara.RemoveStatus(JogStatusId);
        SampleActivity();
        SyncInputLock();
    }

    // The client's own prediction runs the whole cast; this only counts it as activity for
    // stillness mechanics.
    public bool IsLimitBreaking
    {
        get
        {
            var chara = Proxy;
            return chara.IsCasting && LimitBreakHandler.IsLimitBreak(chara.CastActionId);
        }
    }

    private void SampleActivity()
    {
        var hooks = Natives.PlayerInput;
        // Drained every frame, even while dead, so a stale press can't carry over.
        var actedThisFrame = hooks.PollActionUsed();
        if (Dead)
        {
            IsMoving = false;
            IsActing = false;
            return;
        }
        IsMoving = hooks.MovementInputActive || actedThisFrame || hooks.IsJumping || Movement.IsMoving;
        // A limit break casts for seconds, and an Acceleration Bomb landing in that window has
        // caught the player acting, exactly as it would in the fight.
        IsActing = IsMoving || hooks.IsAutoAttacking || IsLimitBreaking;
    }

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        DropHpBar(); // godmode preview skips this path
        AddStatus(StunStatusId);
        this.PlayKoActionTimeline();
        SyncInputLock(); // engage the lock now, not one frame later
    }

    public override void Despawn()
    {
        base.Despawn();
        StopMoving();
        // Order matters; see RestoreRealMaxHealth.
        RestoreRealMaxHealth();
        // Unconditional: also covers a godmode preview drop, where Dead is never set.
        RestoreHpBar();
        // PartyHud's sim shield would otherwise stay on the real character's HP bar.
        Proxy.ClearShield();
        if (Dead)
        {
            ResetActionTimelineNative();
            PlayActionTimelineNative(77); // revive
            Dead = false;
        }
        // Nothing ticks between a reset and the next scenario, so the lock must clear here.
        SyncInputLock();
    }

    // Real FFXIV ids: Confused and Sleep take control away in retail, so the local player is
    // locked out the way a bot doppel has no input.
    private const ushort StatusIdConfused = 0x503;
    private const ushort StatusIdSleep = 0x131E;
    private const ushort StatusIdBind = 0x9D6;

    private void SyncInputLock()
    {
        var hooks = Natives.PlayerInput;
        var asleep = !Dead && HasStatus(StatusIdSleep);
        var confused = !Dead && HasStatus(StatusIdConfused);
        var bound = !Dead && HasStatus(StatusIdBind);
        var incapacitated = asleep || confused;
        hooks.ZeroMovement = Dead || Movement.IsMoving || incapacitated || bound || HasAnyStatus(Natives.Data.StatusLocksMovement);
        hooks.DisableAllActions = Dead || incapacitated || HasAnyStatus(Natives.Data.StatusLocksActions);
        // A knockback slide still lets you turn, so this isn't folded into ZeroMovement.
        hooks.ZeroRotation = Dead || incapacitated;
        // Sleep pins the rotation it landed at; Confused re-pins every tick, since the
        // scenario's Follow already turned the player toward the ally it walks them into.
        if (asleep) hooks.LockedRotation ??= Rotation;
        else if (confused) hooks.LockedRotation = Rotation;
        else hooks.LockedRotation = null;
    }
}
