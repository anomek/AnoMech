using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

public sealed unsafe class SimPlayer(Coordinates coordinates) : SimCharacter(coordinates), ISimPartyMember
{
    private const ushort StunStatusId = 896;  // "Down for the Count" (896) — IsPermanent + LockControl variant.
    
    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }

    // Player activity for stillness/movement mechanics (e.g. Pyretic, Acceleration Bomb).
    // IsMoving = locomotion input (the engine's own RMIWalk movement sample, the same signal
    // bossmod keys off) OR jumping OR using any action — all three "break" a don't-move mechanic
    // in real FFXIV, so all three count here. IsActing = IsMoving OR auto-attacking, i.e. the
    // strictly-broader "is the player doing something" trigger. Both are re-sampled each tick and
    // forced false while KO'd. Scenarios read these on Party.Player at the mechanic's resolve time.
    public bool IsMoving { get; private set; }
    public bool IsActing { get; private set; }

    internal override BattleChara* BattleCharaPtr => (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);

    private protected override PlayerMovement Movement => field ??= new PlayerMovement(this);

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    // The player's input lock is a pure function of its own state, re-derived
    // every tick: movement is frozen while KO'd or being force-slid by a
    // knockback; actions are blocked only while KO'd. base.Tick advances Movement
    // first, so a slide that arrives this frame has already cleared IsMoving.
    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        SampleActivity();
        SyncInputLock();
    }

    private void SampleActivity()
    {
        var hooks = Plugin.PlayerInputHooks;
        // Drain the action latch every frame — even while dead — so a stale press can't carry over.
        var actedThisFrame = hooks.PollActionUsed();
        if (Dead)
        {
            IsMoving = false;
            IsActing = false;
            return;
        }
        IsMoving = hooks.MovementInputActive || actedThisFrame || hooks.IsJumping;
        IsActing = IsMoving || hooks.IsAutoAttacking;
    }

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        AddStatus(StunStatusId);
        this.PlayKoActionTimeline();
        SyncInputLock(); // engage the lock now, not one frame later
    }

    public override void Despawn()
    {
        base.Despawn();
        StopMoving();
        if (Dead)
        {
            ResetActionTimeline();
            PlayActionTimeline(77); // revive
            Dead = false;
        }
        // No SimPlayer ticks between a reset and the next scenario, so the lock
        // must be cleared here — otherwise a die-then-reset leaves the player
        // input-locked in the inn.
        SyncInputLock();
    }

    private void SyncInputLock()
    {
        var hooks = Plugin.PlayerInputHooks;
        hooks.ZeroMovement = Dead || Movement.IsMoving;
        hooks.DisableAllActions = Dead;
    }
}
