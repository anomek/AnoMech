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
        SyncInputLock();
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
