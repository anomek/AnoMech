using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.SimObjects;

public sealed unsafe class SimPartyNpc : SimNpc, ISimPartyMember
{
    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }
    public byte ClassJob { get; }
    public string DisplayName { get; }

    internal SimPartyNpc(int index, Coordinates coordinates, PartyRole role, byte classJob, string name) : base(index, coordinates)
    {
        Role = role;
        ClassJob = classJob;
        DisplayName = name;
    }

    public void Knockback(Vector3 source, float distance, float speed) => Movement.Knockback(source, distance, speed);

    public override void Despawn()
    {
        base.Despawn();
    }

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        var bc = BattleCharaPtr;
        if (bc == null) return;
        ApplyDeadState(bc);
        this.PlayKoActionTimeline();
    }

    private static void ApplyDeadState(BattleChara* bc)
    {
        bc->Health = 0;
        bc->Mana = 0;
        bc->Mode = CharacterModes.Dead;
    }
}
