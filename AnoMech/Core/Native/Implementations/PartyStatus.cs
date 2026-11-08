using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnoMech.Core.Native.Implementations.Pointers;
using Dalamud.Game.ClientState.Conditions;

namespace AnoMech.Core.Native.Implementations;

internal readonly record struct PartyMember(ulong ContentId, string Name, ushort HomeWorld, ulong AccountId = 0)
{
    public const int NameLength = 32;

    public static unsafe string Utf8(byte* text)
    {
        var length = 0;
        while (length < NameLength && text[length] != 0) length++;
        return Encoding.UTF8.GetString(text, length);
    }
}

// The cross-world party as its roster packet lists it: the leader's content id, then eight member
// entries laid out like CrossRealmMember's first 0x58 bytes (account id at 0x08, content id at 0x10,
// home world at 0x2A, name at 0x33), an empty slot's content id 0. The server sends it when the
// party forms and on the client's request, not when someone joins or leaves.
internal sealed record PartyRoster(IReadOnlyList<PartyMember> Members, ulong LeaderContentId)
{
    public const int Length = 8 + 8 * EntryLength;
    private const int EntryLength = 0x58;

    public static readonly PartyRoster None = new([], 0);

    public PartyMember? Leader
    {
        get
        {
            foreach (var member in Members)
                if (member.ContentId == LeaderContentId) return member;
            return null;
        }
    }

    public static unsafe PartyRoster FromPacket(nint payload)
    {
        var members = new List<PartyMember>();
        for (var i = 0; i < 8; i++)
        {
            var entry = (byte*)payload + 8 + i * EntryLength;
            var contentId = *(ulong*)(entry + 0x10);
            if (contentId != 0) members.Add(new PartyMember(contentId, PartyMember.Utf8(entry + 0x33), *(ushort*)(entry + 0x2A), *(ulong*)(entry + 0x08)));
        }
        return new PartyRoster(members, *(ulong*)payload);
    }
}

// Something happened to a member of the cross-world party: +0 set once the listing is full, +1 what
// happened, +8 the member's account id, +0x10 content id, +0x1C home world, +0x22 name. The client
// prints "<name> joins the party" for kinds 1, 4 and 5, and with the full flag on anything but 2 (a
// member coming back), "All places have been filled".
internal readonly record struct PartyMemberEvent(bool ListingFull, byte Kind, PartyMember Member)
{
    public const int Length = 0x48;
    private const byte CameBack = 2;

    public bool Joined => Kind is 1 or 4 or 5;
    public bool FillsListing => ListingFull && Kind != CameBack;

    public static unsafe PartyMemberEvent FromPacket(nint payload)
    {
        var at = (byte*)payload;
        var member = new PartyMember(*(ulong*)(at + 0x10), PartyMember.Utf8(at + 0x22), *(ushort*)(at + 0x1C), *(ulong*)(at + 0x08));
        return new PartyMemberEvent(at[0] != 0, at[1], member);
    }
}

// The networked conditions that belong to the party: the server raises them for a Party Finder
// listing and a cross-world party, and only the server clears them.
internal readonly record struct PartyConditions(bool UsingPartyFinder, bool RecruitingWorldOnly, bool CrossWorldParty)
{
    public bool Recruiting => UsingPartyFinder || RecruitingWorldOnly;

    public static PartyConditions Read(Func<ConditionFlag, bool> isOn)
        => new(isOn(ConditionFlag.UsingPartyFinder), isOn(ConditionFlag.RecruitingWorldOnly), isOn(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));

    // PlayerStateFlags: bit n is the n-th networked Condition row in row order, low bit first.
    public static PartyConditions FromStateFlags(ReadOnlySpan<byte> bits, ConditionFlag[] networked)
    {
        var copy = bits.ToArray();
        return Read(flag => Array.IndexOf(networked, flag) is var bit and >= 0 && bit < copy.Length * 8 && (copy[bit >> 3] & (1 << (bit & 7))) != 0);
    }

    // The packet carries every networked condition, so all but the party's are copied from `current`.
    public void WriteStateFlags(Span<byte> bits, ConditionFlag[] networked, Func<ConditionFlag, bool> current)
    {
        bits.Clear();
        for (var bit = 0; bit < networked.Length && bit < bits.Length * 8; bit++)
        {
            var on = networked[bit] switch
            {
                ConditionFlag.UsingPartyFinder => UsingPartyFinder,
                ConditionFlag.RecruitingWorldOnly => RecruitingWorldOnly,
                ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance => CrossWorldParty,
                var other => current(other),
            };
            if (on) bits[bit >> 3] |= (byte)(1 << (bit & 7));
        }
    }
}

// The online status bits that belong to the party (bit n is OnlineStatus row n): 26 Recruiting Party
// Members, 33-37 alliance and party leader or member, 38-39 their cross-world forms. Bit 26 is what
// the client checks for "using the Party Finder" (promoting, kicking, inviting, converting the party).
internal readonly record struct PartyOnlineStatus(ulong Bits)
{
    private const int RecruitingPartyMembers = 26;
    public const ulong Mask = (1UL << RecruitingPartyMembers) | (1UL << 33) | (1UL << 34) | (1UL << 35) | (1UL << 36) | (1UL << 37) | (1UL << 38) | (1UL << 39);

    public bool Recruiting => (Bits & (1UL << RecruitingPartyMembers)) != 0;

    public static PartyOnlineStatus FromFlags(ulong flags) => new(flags & Mask);

    // The party's bits from here, every other bit as `current` has it.
    public ulong MergeInto(ulong current) => (current & ~Mask) | Bits;
}

// A Party Finder notice as the server sent it. Every reader of the packet uses its first 16 bytes:
// the log message to print, the code at +6, a u64 at +8. Code 0 ends the client's own listing.
internal sealed record PartyNotice(uint TargetId, byte[] Payload)
{
    public const int Length = 16;
    public const byte ListingEnded = 0;

    public uint LogMessage => BitConverter.ToUInt32(Payload, 0);
    public byte Code => Payload[6];
}

// The client's switches on the opcode, read to name the opcode a case is reached from before any
// such packet has arrived: a listing can fill during the very first run. Each gives 0 when none or
// several opcodes reach the case, or when the code doesn't decode to a table inside the module (a
// patch that changed it would otherwise send the read anywhere).
internal static class SwitchCode
{
    private const uint MaxCases = 0x10000;

    // The zone dispatcher's switch:
    //   +0   add r13d, -bias                  41 83 C5 xx
    //   +4   mov rdi, r12                     49 8B FC
    //   +7   cmp r13d, last                   41 81 FD xx xx xx xx
    //   +14  ja default                       0F 87 xx xx xx xx
    //   +20  lea rdx, [image base]            48 8D 15 xx xx xx xx
    //   +27  movsxd rax, r13d                 49 63 C5
    //   +30  mov ecx, [rdx+rax*4+table]       8B 8C 82 xx xx xx xx
    //   +37  add rcx, rdx; jmp rcx            48 03 CA FF E1
    public static unsafe ushort JumpTableOpcodeOf(nint switchCode, nint caseStart, nint moduleBase, long moduleSize)
    {
        var code = (byte*)switchCode;
        var bias = -*(sbyte*)(code + 3);
        var last = *(uint*)(code + 10);
        if (last >= MaxCases) return 0;
        var imageBase = (nint)(code + 27) + *(int*)(code + 23);
        var tableStart = imageBase + *(int*)(code + 33);
        if (imageBase != moduleBase || tableStart < moduleBase || tableStart + 4 * ((long)last + 1) > moduleBase + moduleSize) return 0;
        var table = (uint*)tableStart;
        var opcode = 0;
        var reached = 0;
        for (var index = 0; index <= (int)last; index++)
        {
            if (imageBase + (nint)table[index] != caseStart) continue;
            opcode = index + bias;
            reached++;
        }
        return reached == 1 && opcode is > 0 and <= ushort.MaxValue ? (ushort)opcode : (ushort)0;
    }

    // A switch through a byte table of case numbers, as the party handler has them:
    //   +0   sub eax, first                   2D xx xx xx xx
    //   +5   cmp eax, count                   3D xx xx xx xx
    //   +10  ja default                       0F 87 xx xx xx xx
    //   +16  lea rdx, [image base]            48 8D 15 xx xx xx xx
    //   +23  cdqe                             48 98
    //   +25  movzx eax, [rdx+rax+numbers]     0F B6 84 02 xx xx xx xx
    //   +33  mov ecx, [rdx+rax*4+offsets]     8B 8C 82 xx xx xx xx
    //   +40  add rcx, rdx; jmp rcx            48 03 CA FF E1
    public static unsafe ushort ByteTableOpcodeOf(nint switchCode, nint caseStart, nint moduleBase, long moduleSize)
    {
        var at = (byte*)switchCode;
        var first = *(int*)(at + 1);
        var count = *(uint*)(at + 6);
        if (count >= MaxCases) return 0;
        var image = (nint)(at + 23) + *(int*)(at + 19);
        var numbers = image + *(int*)(at + 29);
        var offsets = image + *(int*)(at + 36);
        if (image != moduleBase || !Inside(numbers, (long)count + 1, moduleBase, moduleSize)) return 0;
        var found = 0;
        var matches = 0;
        for (var i = 0; i <= (int)count; i++)
        {
            var entry = offsets + 4 * ((byte*)numbers)[i];
            if (!Inside(entry, 4, moduleBase, moduleSize)) return 0;
            if (image + (nint)(*(uint*)entry) != caseStart) continue;
            found = first + i;
            matches++;
        }
        return matches == 1 && found is > 0 and <= ushort.MaxValue ? (ushort)found : (ushort)0;
    }

    // The one opcode whose case in the dispatcher's switch (JumpTableOpcodeOf's form) calls
    // `function` within its first bytes (call rel32: E8 xx xx xx xx).
    public static unsafe ushort JumpTableOpcodeCalling(nint switchCode, nint function, nint moduleBase, long moduleSize)
    {
        const int CaseBytes = 16;
        var at = (byte*)switchCode;
        var first = -*(sbyte*)(at + 3);
        var count = *(uint*)(at + 10);
        if (count >= MaxCases) return 0;
        var image = (nint)(at + 27) + *(int*)(at + 23);
        var cases = image + *(int*)(at + 33);
        if (image != moduleBase || !Inside(cases, 4 * ((long)count + 1), moduleBase, moduleSize)) return 0;
        var found = 0;
        var matches = 0;
        for (var i = 0; i <= (int)count; i++)
        {
            var caseStart = (byte*)(image + (nint)((uint*)cases)[i]);
            if (!Inside((nint)caseStart, CaseBytes + 4, moduleBase, moduleSize)) continue;
            for (var b = 0; b < CaseBytes; b++)
            {
                if (caseStart[b] != 0xE8 || (nint)(caseStart + b + 5) + *(int*)(caseStart + b + 1) != function) continue;
                found = first + i;
                matches++;
                break;
            }
        }
        return matches == 1 && found is > 0 and <= ushort.MaxValue ? (ushort)found : (ushort)0;
    }

    // The one opcode whose case in a byte-table switch (ByteTableOpcodeOf's form) calls `function`
    // within its first bytes.
    public static unsafe ushort ByteTableOpcodeCalling(nint switchCode, nint function, nint moduleBase, long moduleSize)
    {
        const int CaseBytes = 16;
        var at = (byte*)switchCode;
        var first = *(int*)(at + 1);
        var count = *(uint*)(at + 6);
        if (count >= MaxCases) return 0;
        var image = (nint)(at + 23) + *(int*)(at + 19);
        var numbers = image + *(int*)(at + 29);
        var offsets = image + *(int*)(at + 36);
        if (image != moduleBase || !Inside(numbers, (long)count + 1, moduleBase, moduleSize)) return 0;
        var found = 0;
        var matches = 0;
        for (var i = 0; i <= (int)count; i++)
        {
            var entry = offsets + 4 * ((byte*)numbers)[i];
            if (!Inside(entry, 4, moduleBase, moduleSize)) return 0;
            var caseStart = (byte*)(image + (nint)(*(uint*)entry));
            if (!Inside((nint)caseStart, CaseBytes + 4, moduleBase, moduleSize)) continue;
            for (var b = 0; b < CaseBytes; b++)
            {
                if (caseStart[b] != 0xE8 || (nint)(caseStart + b + 5) + *(int*)(caseStart + b + 1) != function) continue;
                found = first + i;
                matches++;
                break;
            }
        }
        return matches == 1 && found is > 0 and <= ushort.MaxValue ? (ushort)found : (ushort)0;
    }

    // One opcode checked on its own: cmp eax, opcode (3D xx xx xx xx); ja elsewhere (0F 87 xx xx xx
    // xx); je case (0F 84 xx xx xx xx).
    public static unsafe ushort CompareOpcodeOf(nint compareCode, nint caseStart)
    {
        var at = (byte*)compareCode;
        var target = (nint)(at + 17) + *(int*)(at + 13);
        var opcode = *(uint*)(at + 1);
        return target == caseStart && opcode is > 0 and <= ushort.MaxValue ? (ushort)opcode : (ushort)0;
    }

    private static bool Inside(nint start, long length, nint moduleBase, long moduleSize)
        => start >= moduleBase && start + length <= moduleBase + moduleSize;
}

// A party event: +0x10 the content id of who acted, +0x22 what happened, +0x25 their name, +0x45
// another name. Kind 16 is someone starting a ready check, 17 a member answering it.
internal readonly record struct PartyEvent(byte Kind, ulong ActorContentId, string ActorName)
{
    public const int Length = 0x68;
    public const int KindOffset = 0x22;
    public const byte ReadyCheckStarted = 16;
    public const byte ReadyCheckAnswered = 17;

    public static unsafe PartyEvent FromPacket(nint payload)
    {
        var at = (byte*)payload;
        return new PartyEvent(at[KindOffset], *(ulong*)(at + 0x10), PartyMember.Utf8(at + 0x25));
    }
}

internal sealed record HeldPacket(ushort Opcode, uint TargetId, byte[] Payload);

// A cross-world party's ready check: its start is the starter's content id (the client names them
// from its roster), an answer a member's content id at +0 and their answer at +8.
internal static class CrossWorldReadyCheck
{
    public const int StartLength = 8;
    public const int AnswerLength = 0x10;
}

// A ready check that started during the stay: its start and the answers since, as the server sent
// them.
internal sealed record HeldReadyCheck(DateTime StartedAt, string From, IReadOnlyList<HeldPacket> Packets)
{
    // As long as the client gives a ready check it receives.
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    public TimeSpan LeftAt(DateTime now) => StartedAt + Window - now;
}

internal readonly record struct ReadyCheckAlert(string From, TimeSpan Left);

// An ActorControl as the dispatcher hands it to the client's handler: the actor, the category at
// +0 and p1-p4 at +4.
internal readonly record struct HeldActorControl(uint EntityId, ushort Category, uint P1, uint P2, uint P3, uint P4);

internal readonly record struct PartyStatusEvent(DateTime At, string Text);

// What the server said about the party during a stay, for the lift. ReadyCheck: one still open,
// to replay. Held: every opcode the filter kept back, with its count.
internal sealed record ServerPartyState(
    PartyConditions? Conditions,
    PartyOnlineStatus? Status,
    HeldActorControl? StatusIcon,
    IReadOnlyList<PartyNotice> Notices,
    HeldReadyCheck? ReadyCheck,
    IReadOnlyList<KeyValuePair<ushort, int>> Held);

// The party's status from the server during a stay. The sim owns the party conditions then, so the
// receive filter keeps these packets from the client like any other; the plugin shows what they
// say, and the lift puts the server's word on the client: party conditions, online status bits, the
// player's status icon (ActorControl 504, the OnlineStatus row the character shows), and the Party
// Finder notices themselves, replayed. The client keeps "recruiting" in each (the Duty Finder reads
// the icon and the bits, party commands the bits), so a listing that ends during a run has to end
// in all of them. Who joins and leaves, and the roster, are only shown: the lift's Social refresh
// brings the client its own. A blacklisted player is shown without their name, as the client does.
internal sealed class PartyStatusTracker(
    ConditionFlag[] networked,
    Func<uint, string?> logMessageText,
    Func<ushort, string?> worldName,
    Func<ulong, ulong, bool> isBlacklisted)
{
    private const int IpcHeaderSize = 0x10;
    private const ushort IpcMagic = 0x14;
    private const string RecruitmentEndedText = "Party Finder recruitment ended.";
    private const string BlacklistedPlayer = "A blacklisted player";
    private const string SomeMember = "A party member";
    // LogMessage "Party recruitment ended. All places have been filled."
    private const uint AllPlacesFilled = 983;
    // Generous: the server sends one per listing change.
    private const int MaxNotices = 64;
    public const ushort StatusIconCategory = 504;
    // Less than this left isn't enough to answer a ready check replayed at the lift.
    public static readonly TimeSpan ReadyCheckMinimumLeft = TimeSpan.FromSeconds(2);

    // Learned from live packets (TryReadLiveOpcode), since opcodes change with every game version;
    // 0 until then.
    public ushort PlayerStateFlagsOpcode { get; set; }
    public ushort OnlineStatusOpcode { get; set; }
    public ushort NoticeOpcode { get; set; }
    // The roster arrives as an update and as the answer to the client's own request.
    public ushort[] RosterOpcodes { get; set; } = [];
    // Every ActorControl form; the status icon comes in one of them.
    public ushort[] ActorControlOpcodes { get; set; } = [];
    // The cross-world party's member events (PartyMemberEvent) and its "member left" (the content id
    // at +0).
    public ushort MemberEventOpcode { get; set; }
    public ushort MemberLeftOpcode { get; set; }
    // Party events (PartyEvent): ready checks among them. A cross-world party's ready check comes
    // in two packets of its own instead (CrossWorldReadyCheck).
    public ushort PartyEventOpcode { get; set; }
    public ushort CrossWorldReadyCheckOpcode { get; set; }
    public ushort CrossWorldReadyCheckAnswerOpcode { get; set; }

    // The detour may not run on the framework thread, which reads these.
    private readonly object sync = new();
    private readonly List<PartyStatusEvent> events = [];
    private readonly List<PartyNotice> notices = [];
    // Null while unknown: the next roster is then taken as it is, since nothing says what changed.
    private PartyRoster? roster;
    private ulong localContentId;
    private uint localEntityId;
    private PartyConditions conditions;
    private PartyOnlineStatus status;
    private PartyConditions? conditionsFromServer;
    private PartyOnlineStatus? statusFromServer;
    private HeldActorControl? statusIconFromServer;
    private HeldReadyCheck? readyCheck;
    private readonly Dictionary<ushort, int> held = [];
    private bool listingEndNoticed;
    private bool recruitmentEnded;

    public int NoticesDropped { get; private set; }

    public IReadOnlyList<PartyStatusEvent> Events
    {
        get { lock (sync) return events.ToArray(); }
    }

    // The listing ended during the stay, however the server said so.
    public bool RecruitmentEnded
    {
        get { lock (sync) return recruitmentEnded; }
    }

    // Either can come first, so recruiting is over once neither says otherwise.
    private bool Recruiting => conditions.Recruiting || status.Recruiting;

    // At arm, before the sim takes the conditions over.
    public void Begin(PartyRoster? rosterNow, PartyConditions conditionsNow, PartyOnlineStatus statusNow, ulong localPlayerContentId, uint localPlayerEntityId)
    {
        lock (sync)
        {
            roster = rosterNow;
            localContentId = localPlayerContentId;
            localEntityId = localPlayerEntityId;
            conditions = conditionsNow;
            status = statusNow;
            conditionsFromServer = null;
            statusFromServer = null;
            statusIconFromServer = null;
            readyCheck = null;
            held.Clear();
            listingEndNoticed = false;
            recruitmentEnded = false;
            events.Clear();
            notices.Clear();
            NoticesDropped = 0;
        }
    }

    // A packet the receive filter keeps from the client: true when it was the party's.
    public unsafe bool Observe(nint packet, uint targetId, DateTime at)
    {
        var opcode = *(ushort*)(packet + 2);
        var payload = packet + IpcHeaderSize;
        if (opcode == 0) return false;
        lock (sync) held[opcode] = held.GetValueOrDefault(opcode) + 1;
        if (Array.IndexOf(ActorControlOpcodes, opcode) >= 0)
        {
            if (*(ushort*)payload != StatusIconCategory) return false;
            var p = (uint*)(payload + 4);
            lock (sync)
            {
                if (targetId != localEntityId) return false;
                statusIconFromServer = new HeldActorControl(targetId, StatusIconCategory, p[0], p[1], p[2], p[3]);
            }
            return true;
        }
        if (opcode == PlayerStateFlagsOpcode)
        {
            var next = PartyConditions.FromStateFlags(new ReadOnlySpan<byte>((void*)payload, PacketDispatcherPointers.PlayerStateFlagsLength), networked);
            lock (sync) NoteConditions(next, at);
            return true;
        }
        if (opcode == OnlineStatusOpcode)
        {
            var next = PartyOnlineStatus.FromFlags(*(ulong*)payload);
            lock (sync) NoteStatus(next, at);
            return true;
        }
        if (opcode == NoticeOpcode)
        {
            var notice = new PartyNotice(targetId, new ReadOnlySpan<byte>((void*)payload, PartyNotice.Length).ToArray());
            var text = logMessageText(notice.LogMessage);
            lock (sync) NoteNotice(notice, text, at);
            return true;
        }
        if (Array.IndexOf(RosterOpcodes, opcode) >= 0)
        {
            var next = PartyRoster.FromPacket(payload);
            lock (sync) NoteRoster(next, at);
            return true;
        }
        if (opcode == MemberEventOpcode)
        {
            var memberEvent = PartyMemberEvent.FromPacket(payload);
            var filled = memberEvent.FillsListing ? logMessageText(AllPlacesFilled) : null;
            lock (sync) NoteMemberEvent(memberEvent, filled, at);
            return true;
        }
        if (opcode == MemberLeftOpcode)
        {
            var contentId = *(ulong*)payload;
            lock (sync) NoteMemberLeft(contentId, at);
            return true;
        }
        if (opcode == PartyEventOpcode)
        {
            var partyEvent = PartyEvent.FromPacket(payload);
            if (partyEvent.Kind is not (PartyEvent.ReadyCheckStarted or PartyEvent.ReadyCheckAnswered)) return false;
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, PartyEvent.Length).ToArray());
            lock (sync)
            {
                if (partyEvent.Kind == PartyEvent.ReadyCheckStarted)
                    StartReadyCheck(Describe(new PartyMember(partyEvent.ActorContentId, partyEvent.ActorName, 0)), copy, at);
                else AnswerReadyCheck(copy, at);
            }
            return true;
        }
        if (opcode == CrossWorldReadyCheckOpcode)
        {
            var contentId = *(ulong*)payload;
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, CrossWorldReadyCheck.StartLength).ToArray());
            lock (sync) StartReadyCheck(roster?.Members.FirstOrDefault(m => m.ContentId == contentId) is { ContentId: not 0 } starter ? Describe(starter) : SomeMember, copy, at);
            return true;
        }
        if (opcode == CrossWorldReadyCheckAnswerOpcode)
        {
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, CrossWorldReadyCheck.AnswerLength).ToArray());
            lock (sync) AnswerReadyCheck(copy, at);
            return true;
        }
        return false;
    }

    // The ready check that started during the stay and how long is left to answer it, while any is.
    public ReadyCheckAlert? ReadyCheckAt(DateTime now)
    {
        lock (sync) return readyCheck is { } check && check.LeftAt(now) > TimeSpan.Zero ? new ReadyCheckAlert(check.From, check.LeftAt(now)) : null;
    }

    // For the lift: the conditions, status and status icon the server last sent during the stay
    // (null if none), every notice, oldest first, and a ready check there's still time to answer.
    public ServerPartyState TakeFromServer(DateTime now)
    {
        lock (sync)
        {
            var openCheck = readyCheck is { } check && check.LeftAt(now) >= ReadyCheckMinimumLeft ? check : null;
            var taken = new ServerPartyState(conditionsFromServer, statusFromServer, statusIconFromServer, notices.ToArray(), openCheck,
                held.OrderBy(h => h.Key).ToArray());
            conditionsFromServer = null;
            statusFromServer = null;
            statusIconFromServer = null;
            readyCheck = null;
            notices.Clear();
            held.Clear();
            return taken;
        }
    }

    // Learned as the client's own handlers run for a live packet: `dispatching` is the packet the
    // receive hook is handing the client (0 outside a dispatch, as for the plugin's own calls), and
    // the handler's arguments have to be that packet's content. The handlers belong to
    // ClientStructs, which keeps them current through game patches.
    public unsafe bool LearnOnlineStatus(nint dispatching, ulong flags)
    {
        if (dispatching == 0 || *(ulong*)(dispatching + IpcHeaderSize) != flags) return false;
        var opcode = *(ushort*)(dispatching + 2);
        if (opcode == 0 || opcode == OnlineStatusOpcode) return false;
        OnlineStatusOpcode = opcode;
        return true;
    }

    // The client starts a ready check it received only for a party event of that kind, or for a
    // cross-world party's start, whose payload is shorter than the kind's offset: that one, if known,
    // is never taken, and a known opcode isn't replaced by a guess.
    public unsafe bool LearnPartyEvent(nint dispatching)
    {
        if (dispatching == 0 || *(byte*)(dispatching + IpcHeaderSize + PartyEvent.KindOffset) != PartyEvent.ReadyCheckStarted) return false;
        var opcode = *(ushort*)(dispatching + 2);
        if (opcode == 0 || PartyEventOpcode != 0 || opcode == CrossWorldReadyCheckOpcode) return false;
        PartyEventOpcode = opcode;
        return true;
    }

    public unsafe bool LearnActorControl(nint dispatching, uint dispatchingActor, uint entityId, uint category, uint p1)
    {
        if (dispatching == 0 || dispatchingActor != entityId) return false;
        var payload = dispatching + IpcHeaderSize;
        if (*(ushort*)payload != category || *(uint*)(payload + 4) != p1) return false;
        var opcode = *(ushort*)(dispatching + 2);
        if (opcode == 0 || Array.IndexOf(ActorControlOpcodes, opcode) >= 0) return false;
        ActorControlOpcodes = [.. ActorControlOpcodes, opcode];
        return true;
    }

    // The dispatcher hands a handler the payload in place, right after its 16-byte IPC header (magic,
    // then opcode). The plugin's own handler calls build theirs on the stack, and a header on another
    // page than the payload isn't read.
    public static unsafe bool TryReadLiveOpcode(nint payload, nuint stackLow, nuint stackHigh, out ushort opcode)
    {
        opcode = 0;
        if ((nuint)payload >= stackLow && (nuint)payload < stackHigh) return false;
        if (payload % Environment.SystemPageSize < IpcHeaderSize) return false;
        var header = (ushort*)(payload - IpcHeaderSize);
        if (header[0] != IpcMagic) return false;
        opcode = header[1];
        return opcode != 0;
    }

    private void NoteConditions(PartyConditions next, DateTime at)
    {
        var wasRecruiting = Recruiting;
        if (!conditions.CrossWorldParty && next.CrossWorldParty) events.Add(new PartyStatusEvent(at, "The party is now a cross-world party."));
        conditions = next;
        conditionsFromServer = next;
        NoteRecruiting(wasRecruiting, at);
    }

    private void NoteStatus(PartyOnlineStatus next, DateTime at)
    {
        var wasRecruiting = Recruiting;
        status = next;
        statusFromServer = next;
        NoteRecruiting(wasRecruiting, at);
    }

    private void NoteRecruiting(bool wasRecruiting, DateTime at)
    {
        if (wasRecruiting == Recruiting) return;
        recruitmentEnded = !Recruiting;
        if (Recruiting) events.Add(new PartyStatusEvent(at, "Party Finder recruitment started."));
        else if (!listingEndNoticed) events.Add(new PartyStatusEvent(at, RecruitmentEndedText));
    }

    private void NoteRoster(PartyRoster next, DateTime at)
    {
        var known = roster;
        roster = next;
        if (known is not { } before) return;
        if (next.Members.Count == 0)
        {
            if (before.Members.Any(m => !IsLocal(m))) events.Add(new PartyStatusEvent(at, "You are no longer in a party."));
            return;
        }
        foreach (var member in next.Members.Where(m => !IsLocal(m) && before.Members.All(b => b.ContentId != m.ContentId)))
            events.Add(new PartyStatusEvent(at, $"{Describe(member)} joined the party."));
        foreach (var member in before.Members.Where(b => !IsLocal(b) && next.Members.All(m => m.ContentId != b.ContentId)))
            events.Add(new PartyStatusEvent(at, $"{Describe(member)} left the party."));
        if (before.Members.Count > 0 && before.LeaderContentId != next.LeaderContentId && next.Leader is { } leader)
            events.Add(new PartyStatusEvent(at, IsLocal(leader) ? "You are now the party leader." : $"{Describe(leader)} is now the party leader."));
    }

    // The joiner is added to the roster, so a roster later in the stay doesn't announce them again
    // and their leaving names them.
    private void NoteMemberEvent(PartyMemberEvent memberEvent, string? filled, DateTime at)
    {
        var member = memberEvent.Member;
        if (memberEvent.Joined && !IsLocal(member))
        {
            events.Add(new PartyStatusEvent(at, $"{Describe(member)} joined the party."));
            if (roster is { } known && known.Members.All(m => m.ContentId != member.ContentId))
                roster = known with { Members = [.. known.Members, member] };
        }
        if (memberEvent.FillsListing) NoteListingFilled(filled, at);
    }

    private void NoteMemberLeft(ulong contentId, DateTime at)
    {
        if (contentId == localContentId)
        {
            events.Add(new PartyStatusEvent(at, "You are no longer in a party."));
            return;
        }
        PartyMember? leaver = null;
        if (roster is { } known)
        {
            foreach (var member in known.Members)
                if (member.ContentId == contentId) leaver = member;
            roster = known with { Members = known.Members.Where(m => m.ContentId != contentId).ToArray() };
        }
        events.Add(new PartyStatusEvent(at, $"{(leaver is { } who ? Describe(who) : SomeMember)} left the party."));
    }

    private void StartReadyCheck(string from, HeldPacket start, DateTime at)
    {
        readyCheck = new HeldReadyCheck(at, from, [start]);
        events.Add(new PartyStatusEvent(at, $"{from} started a ready check."));
    }

    // Answers to a check that's over are of no use to the lift.
    private void AnswerReadyCheck(HeldPacket answer, DateTime at)
    {
        if (readyCheck is { } check && check.LeftAt(at) > TimeSpan.Zero)
            readyCheck = check with { Packets = [.. check.Packets, answer] };
    }

    // The client's own words for a full listing, once, in place of the plainer line.
    private void NoteListingFilled(string? text, DateTime at)
    {
        recruitmentEnded = true;
        if (string.IsNullOrWhiteSpace(text) || events.Any(e => e.Text == text)) return;
        listingEndNoticed = true;
        events.RemoveAll(e => e.Text == RecruitmentEndedText);
        events.Add(new PartyStatusEvent(at, text));
    }

    private bool IsLocal(PartyMember member) => member.ContentId == localContentId;

    // World 0 is a packet that doesn't say (the sheet's row 0 is a dev world, with a name).
    private string Describe(PartyMember member)
    {
        if (isBlacklisted(member.AccountId, member.ContentId)) return BlacklistedPlayer;
        return member.HomeWorld != 0 && worldName(member.HomeWorld) is { Length: > 0 } world ? $"{member.Name} ({world})" : member.Name;
    }

    // The server's own words say why a listing ended, and replace the plainer line.
    private void NoteNotice(PartyNotice notice, string? text, DateTime at)
    {
        if (notices.Count < MaxNotices) notices.Add(notice);
        else NoticesDropped++;
        if (notice.Code == PartyNotice.ListingEnded)
        {
            listingEndNoticed = true;
            recruitmentEnded = true;
            events.RemoveAll(e => e.Text == RecruitmentEndedText);
        }
        if (!string.IsNullOrWhiteSpace(text)) events.Add(new PartyStatusEvent(at, text));
    }
}
