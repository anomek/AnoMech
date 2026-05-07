using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using UltiSim.Core.SimObjects;
using GroupPartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;

namespace UltiSim.Core;

internal sealed unsafe class PartyHud
{
    private const int MaxSlots = 8;

    public void Refresh(SimParty party)
    {
        if (party.AllMembers().Count() < 2) return;

        var gm = GroupManager.Instance();
        if (gm == null) return;
        ref var grp = ref gm->MainGroup;

        var slot = 1;
        foreach (var member in party.AllMembers())
        {
            var bc = member.BattleCharaPtr;
            if (bc == null) continue;
            
            var index = member is SimNpc ? slot++ : 0;
            WriteSlot(ref grp.PartyMembers[index], bc);
        }

        grp.MemberCount = (byte)slot;
        grp.PartyLeaderIndex = 0;
        if (grp.PartyId == 0) grp.PartyId = 0xFFFFFFFFu;
    }

    public void Clear()
    {
        var gm = GroupManager.Instance();
        if (gm == null) return;
        ref var grp = ref gm->MainGroup;
        grp.MemberCount = 0;
        grp.PartyLeaderIndex = 0;
        grp.PartyId = 0;
        grp.PartyId_2 = 0;
    }

    private static void WriteSlot(ref GroupPartyMember slot, BattleChara* bc)
    {
        var obj = (GameObject*)bc;
        slot.Position = obj->Position;
        slot.EntityId = obj->EntityId;
        slot.ContentId = 0xFF00000000000000UL | obj->EntityId;
        slot.AccountId = 0xFF00000000000000UL | obj->EntityId;
        slot.CurrentHP = bc->Health;
        slot.MaxHP = bc->MaxHealth;
        slot.CurrentMP = (ushort)bc->Mana;
        slot.MaxMP = (ushort)bc->MaxMana;
        slot.TerritoryType = (ushort)Plugin.ClientState.TerritoryType;
        slot.HomeWorld = bc->HomeWorld;
        slot.ClassJob = bc->ClassJob;
        slot.Level = bc->Level;
        slot.Sex = bc->DrawData.CustomizeData.Sex;
        slot.Flags = 0x05;
        // Damage-shield overlay (small bar over the HP bar). We never simulate
        // shields, so explicitly zero — leaving it untouched lets stale or
        // uninitialized values render a phantom shield strip on every member.
        slot.DamageShield = 0;

        for (int i = 0; i < 64; i++)
        {
            var b = obj->Name[i];
            slot.Name[i] = b;
            if (b == 0) break;
        }
    }
}
