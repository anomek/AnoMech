using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

public sealed unsafe class SimPlayer(Coordinates coordinates) : SimCharacter(coordinates), ISimPartyMember
{
    private const ushort StunStatusId = 896;  // "Down for the Count" (896) — IsPermanent + LockControl variant.

    // The real HP bar is only touched on a scenario KO (a 1-HP sliver), restored in RestoreHpBar.
    public void DropHpBar()
    {
        var bc = BattleCharaPtr;
        if (bc != null) bc->Health = 1;
    }

    public void RestoreHpBar()
    {
        var bc = BattleCharaPtr;
        if (bc != null && bc->Health < bc->MaxHealth) bc->Health = bc->MaxHealth;
    }

    // Real native MaxHealth before it was overridden; null if inactive.
    private uint? realMaxHealth;

    // The same pool bot tanks get (IScenario.TankMaxHealth).
    public void OverrideMaxHealthForTankRole(uint tankMaxHealth)
    {
        var bc = BattleCharaPtr;
        if (bc == null || realMaxHealth != null) return;
        realMaxHealth = bc->MaxHealth;
        bc->MaxHealth = tankMaxHealth;
        bc->Health = tankMaxHealth;
    }

    // Host-authoritative HP for a peer's own character; the real MaxHealth is captured once so
    // Despawn restores it no matter what a host sent.
    public void ApplyNetworkHp(uint currentHp, uint maxHp)
    {
        var bc = BattleCharaPtr;
        if (bc == null || maxHp == 0) return;
        realMaxHealth ??= bc->MaxHealth;
        bc->MaxHealth = maxHp;
        bc->Health = Math.Min(currentHp, maxHp);
    }

    // Must run before RestoreHpBar: restore MaxHealth first, then clamp Health down.
    public void RestoreRealMaxHealth()
    {
        var bc = BattleCharaPtr;
        if (realMaxHealth is not { } original) return;
        if (bc != null)
        {
            bc->MaxHealth = original;
            if (bc->Health > original) bc->Health = original;
        }
        realMaxHealth = null;
    }

    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }
    private bool mechanicInputLock;
    public void SetMechanicInputLock(bool locked)
    {
        mechanicInputLock = locked;
        SyncInputLock();
    }

    // For stillness/movement mechanics: IsMoving = movement input, a jump, any action, or an
    // in-flight debug-bot MoveTo; IsActing also counts auto-attacks. Forced false while KO'd.
    public bool IsMoving { get; private set; }
    public bool IsActing { get; private set; }

    internal override BattleChara* BattleCharaPtr => (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);

    private protected override PlayerMovement Movement => field ??= new PlayerMovement(this);

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

    // The client's own prediction runs the whole cast; this only counts it as activity for
    // stillness mechanics and keeps a second press from queueing behind it.
    public bool IsLimitBreaking
    {
        get
        {
            var bc = BattleCharaPtr;
            return bc != null && bc->CastInfo.IsCasting && LimitBreakHandler.IsLimitBreak(bc->CastInfo.ActionId);
        }
    }

    private void SampleActivity()
    {
        var hooks = Plugin.PlayerInputHooks;
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
        mechanicInputLock = false;
        base.Despawn();
        StopMoving();
        // Order matters; see RestoreRealMaxHealth.
        RestoreRealMaxHealth();
        // Unconditional: also covers a godmode preview drop, where Dead is never set.
        RestoreHpBar();
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
        var hooks = Plugin.PlayerInputHooks;
        var asleep = !Dead && HasStatus(StatusIdSleep);
        var confused = !Dead && HasStatus(StatusIdConfused);
        var bound = !Dead && HasStatus(StatusIdBind);
        var incapacitated = asleep || confused || mechanicInputLock;
        hooks.ZeroMovement = Dead || Movement.IsMoving || incapacitated || bound;
        hooks.DisableAllActions = Dead || incapacitated;
        // A knockback slide still lets you turn, so this isn't folded into ZeroMovement.
        hooks.ZeroRotation = Dead || incapacitated;
        // Sleep pins the rotation it landed at; Confused re-pins every tick, since the
        // scenario's Follow already turned the player toward the ally it walks them into.
        if (asleep) hooks.LockedRotation ??= Rotation;
        else if (confused) hooks.LockedRotation = Rotation;
        else hooks.LockedRotation = null;
    }
}
