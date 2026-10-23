using System;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.SimObjects;

public sealed class SimPlayer : SimCharacter, ISimPartyMember
{
    private readonly PlayerMotion motion;

    public SimPlayer(Coordinates coordinates, ObstacleField obstacles)
        : base(coordinates, self => new PlayerMotion(coordinates, self.ActorControl, obstacles))
        => motion = (PlayerMotion)Motion;

    public PartyRole Role { get; set; }

    // For stillness/movement mechanics: IsMoving = movement input, a jump, any action, or an
    // in-flight debug-bot MoveTo; IsActing also counts auto-attacks. Forced false while KO'd.
    public bool IsMoving { get; private set; }
    public bool IsActing { get; private set; }

    // How many auto-attacks it has swung, for a mechanic that a swing breaks.
    public int AutoAttackSwings { get; private set; }

    internal void CountAutoAttackSwing() => AutoAttackSwings++;

    internal override IBattleCharaProxy Proxy => Natives.BattleCharas.LocalPlayer;

    public void UseSprint(float duration)
    {
        if (DebugBotControl.Enabled && !Dead && !ActionsLocked) SprintHandler.Apply(this, duration);
    }

    public byte ClassJob => Proxy.ClassJob;

    // The debug bot's press: what JobActions gives a real one, applied straight away as its Sprint
    // is.
    public bool UseAction(uint actionId, SimCharacter? target = null)
    {
        if (!DebugBotControl.Enabled || Dead || ActionsLocked || target is not null && !target.IsAlive()) return false;
        JobActions.ApplyEffects(this, actionId, (ulong)(target ?? this).GameObjectId, Random.Shared);
        if (target is SimEnemy enemy) EnemyHitHandler.Report(enemy, this);
        return true;
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        SyncStatusAffliction();
        SampleActivity();
    }

    // Compared against the client every tick, so a PlayerStateFlags from the real server that
    // clears it is undone on the next one.
    private void SyncStatusAffliction()
    {
        var status = AnyActiveStatusRow(row => row.LockMovement || row.LockActions);
        var control = AnyActiveStatusRow(row => row.LockControl);
        var conditions = Natives.PlayerConditions;
        if (conditions.StatusAffliction != status || conditions.ControlAffliction != control)
            conditions.SetAfflictions(status, control);
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
        IsMoving = hooks.MovementInputActive || actedThisFrame || hooks.IsJumping || motion.IsMoving || motion.IsHeld;
        // The press is one frame, but the whole cast bar counts: an Acceleration Bomb landing
        // mid-cast catches the player acting, exactly as it would in the fight.
        IsActing = IsMoving || hooks.IsAutoAttacking || Proxy.IsCasting;
    }

    public void OnKilled(bool byEnvironment)
    {
        GoDown(byEnvironment);
    }

    public override void Despawn()
    {
        base.Despawn();
        Revive();
        if (Natives.PlayerConditions is { StatusAffliction: true } or { ControlAffliction: true })
            Natives.PlayerConditions.SetAfflictions(false, false);
    }
}
