using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.SimObjects;

public sealed class SimPlayer(Coordinates coordinates) : SimCharacter(coordinates), ISimPartyMember
{
    private const ushort StunStatusId = 896;  // "Down for the Count" (896) — IsPermanent + LockControl variant.

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

    public void UseSprint(float duration)
    {
        if (DebugBotControl.Enabled && !Dead && !ActionsLocked) SprintHandler.Apply(this, duration);
    }

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    public void PushInDirection(float heading, float distance, float speed) => Movement.PushInDirection(heading, distance, speed);

    public void PushInDirectionEased(float heading, float distance, float durationSeconds) => Movement.PushInDirectionEased(heading, distance, durationSeconds);

    // The input lock is re-derived every tick from Dead/Movement/statuses.
    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        SampleActivity();
        SyncInputLock();
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
        // The press is one frame, but the whole cast bar counts: an Acceleration Bomb landing
        // mid-cast catches the player acting, exactly as it would in the fight.
        IsActing = IsMoving || hooks.IsAutoAttacking || Proxy.IsCasting;
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

    private void SyncInputLock()
    {
        var hooks = Natives.PlayerInput;
        hooks.ZeroMovement = Dead || Movement.IsMoving || MovementLocked;
        hooks.DisableAllActions = Dead || ActionsLocked;
    }
}
