using System.Linq;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using GroupPartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;

namespace AnoMech.Core.Game.Party;

// Drives the in-game _PartyList addon for spawned doppels by writing into
// MainGroup.PartyMembers each Refresh. The addon only enters its render-members
// path when MemberCount > 0, so this write is what makes doppel rows appear at
// all. Position, name, base HP/MP, class-job, and the per-slot StatusManager
// snapshot all flow through here; the addon agent then resolves each slot's
// BattleChara via LookupBattleCharaByEntityId (succeeds because every spawned
// BC is registered in CharacterManager._battleCharas; scenarios are inn-gated
// upstream) and drives status icons + timer text natively from that resolution.
internal sealed unsafe class PartyHud
{
    private const int MaxSlots = 8;

    // Snapshot of real MainGroup taken on the first Refresh of a sim run, restored
    // verbatim on Clear so leaving a sim doesn't strand the player with stale or
    // zeroed party state. Null when no sim is active.
    private MainGroupSnapshot? realPartySnapshot;
    // Identifying fields of what we wrote into each MainGroup slot last frame.
    // ReconcileEngineWrites uses these to spot slots the engine has changed
    // since (real joins/leaves during sim) and folds those into realPartySnapshot.
    private readonly (uint EntityId, ulong ContentId)[] lastWrittenSlots = new (uint, ulong)[MaxSlots];
    private byte lastWrittenMemberCount;
    private bool hasLastWritten;

    public void Refresh(SimParty party)
    {
        if (party.AllMembers().Count() < 2) return;

        var gm = GroupManager.Instance();
        if (gm == null) return;
        ref var grp = ref gm->MainGroup;

        // First Refresh of the sim run: capture the engine's real-party state.
        // Subsequent frames: fold any engine-driven slot changes (real joins /
        // leaves that happened between our writes) back into the snapshot so the
        // restore on Clear reflects the live party, not the pre-sim party.
        if (realPartySnapshot == null)
            realPartySnapshot = SnapshotMainGroup(ref grp);
        else
            ReconcileEngineWrites(ref grp);

        var slot = 1;
        foreach (var member in party.AllMembers())
        {
            var bc = member.BattleCharaPtr;
            if (bc == null) continue;
            var index = member is SimNpc ? slot++ : 0;
            WriteSlot(ref grp.PartyMembers[index], bc);
            // The local player's slot 0 must keep its real ContentId/AccountId so
            // AgentReadyCheck (matches incoming packets by ContentId) and other
            // engine-side party lookups still resolve the local player correctly.
            if (index == 0 && realPartySnapshot is { } snap && snap.Members[0].ContentId != 0)
            {
                grp.PartyMembers[0].ContentId = snap.Members[0].ContentId;
                grp.PartyMembers[0].AccountId = snap.Members[0].AccountId;
            }
        }

        grp.MemberCount = (byte)slot;
        grp.PartyLeaderIndex = 0;
        if (grp.PartyId == 0) grp.PartyId = 0xFFFFFFFFu;

        for (int i = 0; i < MaxSlots; i++)
        {
            ref var s = ref grp.PartyMembers[i];
            lastWrittenSlots[i] = (s.EntityId, s.ContentId);
        }
        lastWrittenMemberCount = grp.MemberCount;
        hasLastWritten = true;
    }

    public void Clear()
    {
        hasLastWritten = false;

        // No snapshot means we never wrote to MainGroup this session, so the
        // engine's party state is intact — leave it alone. Zeroing here would
        // wipe the real party on plugin disable when no sim ran (or after a
        // prior Clear already restored and nulled the snapshot).
        if (realPartySnapshot is not { } snap) return;

        var gm = GroupManager.Instance();
        if (gm == null) { realPartySnapshot = null; return; }
        ref var grp = ref gm->MainGroup;

        // Restore real-party state captured at sim start (folded with any
        // join/leave deltas detected during the run). The addon's natural
        // update path redraws the rows from this restored MainGroup on the
        // next frame.
        RestoreMainGroup(ref grp, snap);
        realPartySnapshot = null;
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
        slot.Flags = 0x5;
        slot.DamageShield = 0;
        slot.StatusManager = bc->StatusManager;

        for (int i = 0; i < 64; i++)
        {
            var b = obj->Name[i];
            slot.Name[i] = b;
            if (b == 0) break;
        }
    }

    private struct MainGroupSnapshot
    {
        public GroupPartyMember[] Members;
        public byte MemberCount;
        public uint PartyLeaderIndex;
        public long PartyId;
        public long PartyId_2;
        public byte AllianceFlags;
    }

    private static MainGroupSnapshot SnapshotMainGroup(ref GroupManager.Group grp)
    {
        var snap = new MainGroupSnapshot
        {
            Members = new GroupPartyMember[MaxSlots],
            MemberCount = grp.MemberCount,
            PartyLeaderIndex = grp.PartyLeaderIndex,
            PartyId = grp.PartyId,
            PartyId_2 = grp.PartyId_2,
            AllianceFlags = grp.AllianceFlags,
        };
        for (int i = 0; i < MaxSlots; i++) snap.Members[i] = grp.PartyMembers[i];
        return snap;
    }

    private static void RestoreMainGroup(ref GroupManager.Group grp, MainGroupSnapshot snap)
    {
        for (int i = 0; i < MaxSlots; i++) grp.PartyMembers[i] = snap.Members[i];
        grp.MemberCount = snap.MemberCount;
        grp.PartyLeaderIndex = snap.PartyLeaderIndex;
        grp.PartyId = snap.PartyId;
        grp.PartyId_2 = snap.PartyId_2;
        grp.AllianceFlags = snap.AllianceFlags;
    }

    // Doppel ContentIds are synthesised as 0xFF00000000000000UL | EntityId in
    // WriteSlot; real engine-written ContentIds never hit that top byte, so it
    // works as a sentinel to tell our own writes apart from the engine's.
    private const ulong DoppelContentIdSentinel = 0xFF00000000000000UL;

    private static bool IsDoppelContentId(ulong contentId)
        => (contentId & DoppelContentIdSentinel) == DoppelContentIdSentinel;

    // Detect slots the engine modified between our last write and now. A real
    // ContentId means someone joined (or the engine re-arranged); zero means
    // they left. Fold the change into realPartySnapshot so the eventual restore
    // reflects the live party, not the pre-sim party.
    private void ReconcileEngineWrites(ref GroupManager.Group grp)
    {
        if (!hasLastWritten || realPartySnapshot is not { } snap) return;

        bool dirty = false;
        for (int i = 0; i < MaxSlots; i++)
        {
            ref var current = ref grp.PartyMembers[i];
            var last = lastWrittenSlots[i];
            if (current.EntityId == last.EntityId && current.ContentId == last.ContentId)
                continue;

            if (current.ContentId == 0)
            {
                if (snap.Members[i].ContentId != 0)
                {
                    snap.Members[i] = default;
                    dirty = true;
                }
            }
            else if (!IsDoppelContentId(current.ContentId))
            {
                snap.Members[i] = current;
                dirty = true;
            }
        }

        if (grp.MemberCount != lastWrittenMemberCount)
        {
            byte count = 0;
            for (int i = 0; i < MaxSlots; i++)
                if (snap.Members[i].ContentId != 0) count++;
            snap.MemberCount = count;
            dirty = true;
        }

        if (dirty) realPartySnapshot = snap;
    }
}
