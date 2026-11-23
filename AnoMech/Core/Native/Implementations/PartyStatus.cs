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

// A party event: +0x10 the content id of who acted, +0x22 what happened, +0x25 their name, +0x45
// another name. Kind 16 is someone starting a ready check, 17 a member answering it.
// The actor's account id at +0, which the client checks against its blacklist with their content id.
internal readonly record struct PartyEvent(byte Kind, ulong ActorAccountId, ulong ActorContentId, string ActorName)
{
    public const int Length = 0x68;
    public const int KindOffset = 0x22;
    public const byte ReadyCheckStarted = 16;
    public const byte ReadyCheckAnswered = 17;

    public static unsafe PartyEvent FromPacket(nint payload)
    {
        var at = (byte*)payload;
        return new PartyEvent(at[KindOffset], *(ulong*)at, *(ulong*)(at + 0x10), PartyMember.Utf8(at + 0x25));
    }
}

// What a held packet is, for the lift.
internal enum HeldKind { Other, Roster, MemberStatus, PartyList, PartyKey }

// A server packet the lift hands back to the client as it came. What: how the log names it.
internal sealed record HeldPacket(ushort Opcode, uint TargetId, byte[] Payload, string What = "", HeldKind Kind = HeldKind.Other);

// What the server said during the stay, handed to the client in the order it came: a held packet, or
// the party's part of the conditions, the online status or the player's status icon, each merged into
// what the client has at that point. ReadyCheckStarted: where the held ready check started.
// KeyNotReplayed: where the first key packet the lift can't replay came, standing for every later one.
internal abstract record LiftStep
{
    public sealed record Packet(HeldPacket Held) : LiftStep;
    public sealed record Conditions(PartyConditions Value) : LiftStep;
    public sealed record OnlineStatus(PartyOnlineStatus Value) : LiftStep;
    public sealed record StatusIcon(HeldActorControl Value) : LiftStep;
    public sealed record ReadyCheckStarted : LiftStep;
    public sealed record KeyNotReplayed(string Why) : LiftStep;
}

// The party watch's clock: UTC at load, carried on by the monotonic counter, so a system clock step
// never moves a held ready check's end.
internal static class PartyClock
{
    private static readonly DateTime StartUtc = DateTime.UtcNow;
    private static readonly long StartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

    public static DateTime UtcNow => StartUtc + System.Diagnostics.Stopwatch.GetElapsedTime(StartTimestamp);

    // A time on this clock as the system clock reads it now.
    public static DateTime ToSystemClock(DateTime at) => DateTime.UtcNow - (UtcNow - at);
}

// The party of one world as the server sends it (UpdatePartyPacket), read from its plain fields only:
// the dispatcher decodes its ids, home worlds, jobs and levels with keys only the client holds.
internal static unsafe class PartyListPacket
{
    public const int Capacity = UpdatePartyPacket.Capacity;
    public static readonly int Length = sizeof(UpdatePartyPacket);
    private static readonly int MemberLength = sizeof(UpdatePartyPacketMember);

    public static int MemberCount(byte[] payload)
    {
        fixed (byte* at = payload) return ((UpdatePartyPacket*)at)->MemberCount;
    }

    public static int LeaderIndex(byte[] payload)
    {
        fixed (byte* at = payload) return ((UpdatePartyPacket*)at)->LeaderIndex;
    }

    public static ReadOnlySpan<byte> Name(byte[] payload, int index) => payload.AsSpan(index * MemberLength, UpdatePartyPacketMember.NameLength);

    public static ulong AccountId(byte[] payload, int index)
    {
        fixed (byte* at = payload) return ((UpdatePartyPacketMember*)(at + index * MemberLength))->AccountId;
    }

    public static byte Valid(byte[] payload, int index)
    {
        fixed (byte* at = payload) return ((UpdatePartyPacketMember*)(at + index * MemberLength))->Valid;
    }
}

// The packets a handler of the client names when it runs for a live one (PartyOpcodeSources).
internal enum PartyPacket { PlayerStateFlags, OnlineStatus, ActorControl, Notice, Roster, PartyEvent, CrossWorldReadyCheck }

// A cross-world party's ready check: its start is the starter's content id (the client names them
// from its roster), an answer a member's content id at +0 and their answer at +8.
internal static class CrossWorldReadyCheck
{
    public const int StartLength = 8;
    public const int AnswerLength = 0x10;
}

// A ready check that started during the stay: its start and the answers since, as the server sent
// them. Starter: their content id. CrossWorld: a cross-world party's own packets, not a party event.
internal sealed record HeldReadyCheck(DateTime StartedAt, string From, IReadOnlyList<HeldPacket> Packets, ulong Starter, bool CrossWorld)
{
    // As long as the client gives a ready check it receives.
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    public TimeSpan LeftAt(DateTime now) => StartedAt + Window - now;
}

internal readonly record struct ReadyCheckAlert(string From, TimeSpan Left);

// An ActorControl as the dispatcher hands it to the client's handler: the actor, the category at
// +0 and p1-p4 at +4.
internal readonly record struct HeldActorControl(uint EntityId, ushort Category, uint P1, uint P2, uint P3, uint P4);

// At: on PartyClock in game.
internal readonly record struct PartyStatusEvent(DateTime At, string Text)
{
    public DateTime ShownAt => PartyClock.ToSystemClock(At);
}

// The opcodes the client's handlers named for live packets on one game version, each under its packet
// (PartyOpcodeSources), kept between sessions in the plugin's configuration.
internal sealed record HandledPartyOpcodes(ushort PlayerStateFlags, ushort OnlineStatus, ushort Notice, ushort[] Roster, ushort[] ActorControl,
    ushort PartyEvent, ushort CrossWorldReadyCheck)
{
    // Null when the configuration holds another game version's.
    public static HandledPartyOpcodes? SavedFor(Configuration config, string gameVersion) => config.PartyOpcodesGameVersion != gameVersion
        ? null
        : new(config.PlayerStateFlagsOpcode, config.OnlineStatusOpcode, config.PartyNoticeOpcode, config.PartyRosterOpcodes, config.PartyActorControlOpcodes,
            config.PartyEventOpcode, config.CrossWorldReadyCheckOpcode);

    public void SaveTo(Configuration config, string gameVersion)
    {
        config.PartyOpcodesGameVersion = gameVersion;
        config.PlayerStateFlagsOpcode = PlayerStateFlags;
        config.OnlineStatusOpcode = OnlineStatus;
        config.PartyNoticeOpcode = Notice;
        config.PartyRosterOpcodes = Roster;
        config.PartyActorControlOpcodes = ActorControl;
        config.PartyEventOpcode = PartyEvent;
        config.CrossWorldReadyCheckOpcode = CrossWorldReadyCheck;
    }
}

// A packet one of the client's handlers ran for, and its opcode.
internal readonly record struct NamedOpcode(PartyPacket Packet, ushort Opcode);

// What a call to one of the client's handlers names, given the packet being dispatched (`dispatching`,
// 0 outside a dispatch, as for the plugin's own calls): that packet, when the call was for it.
internal static class PartyHandlerCalls
{
    private const int IpcHeaderSize = 0x10;

    // The handler got the packet's own value; it runs for other packets and the plugin's calls too.
    public static unsafe NamedOpcode? OnlineStatus(nint dispatching, ulong flags)
        => dispatching != 0 && *(ulong*)(dispatching + IpcHeaderSize) == flags ? Named(PartyPacket.OnlineStatus, dispatching) : null;

    // The client's duty recorder replays ActorControls it stored (isRecorded) while dispatching other
    // packets; only a live one's handler call carries the packet's own fields.
    public static unsafe NamedOpcode? ActorControl(nint dispatching, uint dispatchingActor, uint entityId, uint category, uint p1, bool isRecorded)
        => dispatching != 0 && !isRecorded && dispatchingActor == entityId
           && *(ushort*)(dispatching + IpcHeaderSize) == category && *(uint*)(dispatching + IpcHeaderSize + 4) == p1
            ? Named(PartyPacket.ActorControl, dispatching)
            : null;

    // A handler the dispatcher hands the payload in place, right after the packet's IPC header.
    public static NamedOpcode? Payload(nint dispatching, nint payload, PartyPacket packet)
        => dispatching != 0 && payload == dispatching + IpcHeaderSize ? Named(packet, dispatching) : null;

    // A ready check the client started. A cross-world party's start is the starter's content id and
    // nothing else; a party event begins with the actor's account id and names them at +0x10. So a
    // party member's content id first is a cross-world start, and only a packet without one can be a
    // party event, whose account id is then the one the client holds for that member, where it holds
    // one. Past a start's 8 bytes the client's buffer still holds whatever it last carried, so a start
    // that also reads as a party event names nothing. `memberAccount`: null for anyone outside the
    // client's party and its cross-world party, else the member's account id, 0 when not known.
    public static unsafe NamedOpcode? ReadyCheck(nint dispatching, Func<ulong, ulong?> memberAccount)
    {
        if (dispatching == 0) return null;
        var payload = (byte*)(dispatching + IpcHeaderSize);
        var first = *(ulong*)payload;
        var actorAccount = payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckStarted ? memberAccount(*(ulong*)(payload + 0x10)) : null;
        if (memberAccount(first) is not null) return actorAccount is null ? Named(PartyPacket.CrossWorldReadyCheck, dispatching) : null;
        return actorAccount is { } account && (account == 0 || account == first) ? Named(PartyPacket.PartyEvent, dispatching) : null;
    }

    private static unsafe NamedOpcode Named(PartyPacket packet, nint dispatching) => new(packet, *(ushort*)(dispatching + 2));
}

// A live packet's handler named its opcode, kept under Packet. Expected: the tracker already used that
// opcode for that packet. Retired: why the receiver's opcodes just went.
internal readonly record struct Observation(PartyPacket Packet, bool Expected, string? Retired);

// Where the tracker's opcodes come from: the client's protocol receiver (PartyPacketMethods), and what
// the client's own handlers did with live packets. A handler outranks the receiver: once one names a
// packet with an opcode the receiver's table doesn't give that packet, none of the receiver's opcodes
// are used for the rest of the session, as they all come from the same table; the next load checks the
// table against what was kept. The one exception is which of the two ready check starts a packet was,
// which a handler tells only by the packet's contents: where the table gives the opcode the other
// start, the table says. Without the receiver, the tracker has only what the handlers named, and no
// handler names member events, a member leaving or a cross-world ready check's answers. A packet of
// one opcode keeps the one its handler named last; the roster and the ActorControl forms keep every
// one. `keptChanged` runs whenever what's kept for this game version (Saved) changes, for the caller to
// save it. Not thread-safe; ServerPartyWatch locks.
internal sealed class PartyOpcodeSources(PartyStatusTracker tracker, Action? keptChanged = null)
{
    private readonly Dictionary<PartyPacket, List<ushort>> named = [];
    private PartyOpcodes? fromReceiver;

    public void Restore(HandledPartyOpcodes saved)
    {
        Name(PartyPacket.PlayerStateFlags, saved.PlayerStateFlags);
        Name(PartyPacket.OnlineStatus, saved.OnlineStatus);
        Name(PartyPacket.Notice, saved.Notice);
        Name(PartyPacket.PartyEvent, saved.PartyEvent);
        Name(PartyPacket.CrossWorldReadyCheck, saved.CrossWorldReadyCheck);
        foreach (var opcode in saved.Roster) Name(PartyPacket.Roster, opcode);
        foreach (var opcode in saved.ActorControl) Name(PartyPacket.ActorControl, opcode);
        Apply();
    }

    // Null once the receiver's opcodes are in use; otherwise why they aren't.
    public string? UseReceiver(PartyOpcodes known)
    {
        foreach (var (packet, opcodes) in named)
            foreach (var opcode in opcodes)
                if (!Gives(known, AsTableHasIt(known, packet, opcode), opcode))
                    return Disagreement(packet, opcode, known);
        var (partyEvent, crossWorld) = (Last(PartyPacket.PartyEvent), Last(PartyPacket.CrossWorldReadyCheck));
        named.Remove(PartyPacket.PartyEvent);
        named.Remove(PartyPacket.CrossWorldReadyCheck);
        Name(AsTableHasIt(known, PartyPacket.PartyEvent, partyEvent), partyEvent);
        Name(AsTableHasIt(known, PartyPacket.CrossWorldReadyCheck, crossWorld), crossWorld);
        fromReceiver = known;
        Apply();
        if (Last(PartyPacket.PartyEvent) != partyEvent || Last(PartyPacket.CrossWorldReadyCheck) != crossWorld) keptChanged?.Invoke();
        return null;
    }

    public Observation Observed(NamedOpcode? named) => named is { } seen ? Observed(seen.Packet, seen.Opcode) : default;

    public Observation Observed(PartyPacket packet, ushort opcode)
    {
        if (opcode == 0) return default;
        if (fromReceiver is { } table) packet = AsTableHasIt(table, packet, opcode);
        var expected = InUse(packet, opcode);
        var changed = Name(packet, opcode);
        string? retired = null;
        if (fromReceiver is { } known && !Gives(known, packet, opcode))
        {
            retired = Disagreement(packet, opcode, known);
            fromReceiver = null;
        }
        Apply();
        if (changed) keptChanged?.Invoke();
        return new Observation(packet, expected, retired);
    }

    // Only what the handlers named, each under its packet: the receiver is read again at each load.
    public HandledPartyOpcodes Saved() => new(Last(PartyPacket.PlayerStateFlags), Last(PartyPacket.OnlineStatus), Last(PartyPacket.Notice),
        All(PartyPacket.Roster), All(PartyPacket.ActorControl), Last(PartyPacket.PartyEvent), Last(PartyPacket.CrossWorldReadyCheck));

    // True when what's kept changed.
    private bool Name(PartyPacket packet, ushort opcode)
    {
        if (opcode == 0) return false;
        if (!named.TryGetValue(packet, out var opcodes)) named[packet] = opcodes = [];
        if (packet is PartyPacket.Roster or PartyPacket.ActorControl)
        {
            if (opcodes.Contains(opcode)) return false;
            opcodes.Add(opcode);
            return true;
        }
        if (opcodes is [var only] && only == opcode) return false;
        opcodes.Clear();
        opcodes.Add(opcode);
        return true;
    }

    // A ready check names its opcode for certain, and which of the two starts it was only by what the
    // packet holds; the receiver's table, where it gives that opcode the other start, says which.
    private static PartyPacket AsTableHasIt(PartyOpcodes known, PartyPacket packet, ushort opcode) => packet switch
    {
        PartyPacket.PartyEvent when opcode != 0 && opcode == known.CrossWorldReadyCheck && opcode != known.PartyEvent => PartyPacket.CrossWorldReadyCheck,
        PartyPacket.CrossWorldReadyCheck when opcode != 0 && opcode == known.PartyEvent && opcode != known.CrossWorldReadyCheck => PartyPacket.PartyEvent,
        _ => packet,
    };

    private void Apply()
    {
        if (fromReceiver is { } known)
        {
            tracker.PlayerStateFlagsOpcode = known.PlayerStateFlags;
            tracker.OnlineStatusOpcode = known.OnlineStatus;
            tracker.NoticeOpcode = known.Notice;
            tracker.PartyEventOpcode = known.PartyEvent;
            tracker.RosterOpcodes = known.Roster == 0 ? [] : [known.Roster];
            tracker.ActorControlOpcodes = known.ActorControl;
            tracker.MemberEventOpcode = known.MemberEvent;
            tracker.MemberLeftOpcode = known.MemberLeft;
            tracker.MemberRemovedOpcode = known.MemberRemoved;
            tracker.DisbandedOpcode = known.Disbanded;
            tracker.MemberStatusOpcode = known.MemberStatus;
            tracker.PartyListOpcode = known.PartyList;
            tracker.PartyKeyOpcode = known.PartyKey;
            tracker.PartyViewOpcodes = known.PartyView;
            tracker.CrossWorldReadyCheckOpcode = known.CrossWorldReadyCheck;
            tracker.CrossWorldReadyCheckAnswerOpcode = known.CrossWorldReadyCheckAnswer;
            return;
        }
        tracker.PlayerStateFlagsOpcode = Last(PartyPacket.PlayerStateFlags);
        tracker.OnlineStatusOpcode = Last(PartyPacket.OnlineStatus);
        tracker.NoticeOpcode = Last(PartyPacket.Notice);
        tracker.PartyEventOpcode = Last(PartyPacket.PartyEvent);
        tracker.RosterOpcodes = All(PartyPacket.Roster);
        tracker.ActorControlOpcodes = All(PartyPacket.ActorControl);
        tracker.MemberEventOpcode = 0;
        tracker.MemberLeftOpcode = 0;
        tracker.MemberRemovedOpcode = 0;
        tracker.DisbandedOpcode = 0;
        tracker.MemberStatusOpcode = 0;
        tracker.PartyListOpcode = 0;
        tracker.PartyKeyOpcode = 0;
        tracker.PartyViewOpcodes = [];
        tracker.CrossWorldReadyCheckOpcode = Last(PartyPacket.CrossWorldReadyCheck);
        tracker.CrossWorldReadyCheckAnswerOpcode = 0;
    }

    private ushort Last(PartyPacket packet) => named.TryGetValue(packet, out var opcodes) && opcodes.Count > 0 ? opcodes[^1] : (ushort)0;

    private ushort[] All(PartyPacket packet) => named.TryGetValue(packet, out var opcodes) ? [.. opcodes] : [];

    private bool InUse(PartyPacket packet, ushort opcode) => packet switch
    {
        PartyPacket.PlayerStateFlags => tracker.PlayerStateFlagsOpcode == opcode,
        PartyPacket.OnlineStatus => tracker.OnlineStatusOpcode == opcode,
        PartyPacket.Notice => tracker.NoticeOpcode == opcode,
        PartyPacket.PartyEvent => tracker.PartyEventOpcode == opcode,
        PartyPacket.CrossWorldReadyCheck => tracker.CrossWorldReadyCheckOpcode == opcode,
        PartyPacket.Roster => Array.IndexOf(tracker.RosterOpcodes, opcode) >= 0,
        _ => Array.IndexOf(tracker.ActorControlOpcodes, opcode) >= 0,
    };

    private static bool Gives(PartyOpcodes known, PartyPacket packet, ushort opcode) => packet switch
    {
        PartyPacket.PlayerStateFlags => known.PlayerStateFlags == opcode,
        PartyPacket.OnlineStatus => known.OnlineStatus == opcode,
        PartyPacket.Notice => known.Notice == opcode,
        PartyPacket.PartyEvent => known.PartyEvent == opcode,
        PartyPacket.CrossWorldReadyCheck => known.CrossWorldReadyCheck == opcode,
        PartyPacket.Roster => known.Roster == opcode,
        _ => Array.IndexOf(known.ActorControl, opcode) >= 0,
    };

    private static string Disagreement(PartyPacket packet, ushort opcode, PartyOpcodes known)
    {
        var table = packet switch
        {
            PartyPacket.PlayerStateFlags => Opcode(known.PlayerStateFlags),
            PartyPacket.OnlineStatus => Opcode(known.OnlineStatus),
            PartyPacket.Notice => Opcode(known.Notice),
            PartyPacket.PartyEvent => Opcode(known.PartyEvent),
            PartyPacket.CrossWorldReadyCheck => Opcode(known.CrossWorldReadyCheck),
            PartyPacket.Roster => Opcode(known.Roster),
            _ => $"[{string.Join(", ", known.ActorControl.Select(Opcode))}]",
        };
        return $"the client handled {packet} as 0x{opcode:X4}, where the receiver's table has {table}";
    }

    private static string Opcode(ushort opcode) => opcode == 0 ? "none" : $"0x{opcode:X4}";
}

// What the server said about the party during a stay, for the lift. Steps: what the lift hands the
// client, in the order it came. ReadyCheck: one still open. Held: every opcode the filter kept back,
// with its count.
internal sealed record ServerPartyState(IReadOnlyList<LiftStep> Steps, HeldReadyCheck? ReadyCheck, IReadOnlyList<KeyValuePair<ushort, int>> Held)
{
    public HeldActorControl? StatusIcon => Steps.OfType<LiftStep.StatusIcon>().LastOrDefault()?.Value;
}

// The client as the lift puts the server's word on it: ServerPartyWatch's is the game.
internal interface IPartyClient
{
    bool Condition(ConditionFlag flag);
    // Through the client's PlayerStateFlags handler.
    void HandleStateFlags(ReadOnlySpan<byte> bits);
    // Null with no InfoModule to hold them.
    ulong? OnlineStatusFlags { get; }
    void SetOnlineStatusFlags(ulong flags);
    void HandleActorControl(HeldActorControl control);
    // Through the client's own dispatcher.
    bool Inject(HeldPacket packet, string what);
    // InfoProxyCrossRealm.IsLocalPlayerInParty. The client asks the player about a ready check someone
    // else started only while this holds; otherwise it answers "not ready" for them, to the server.
    bool CountsItselfInAParty();
    // InfoProxyCrossRealm.GetMemberByContentId: a cross-world check's starter the client knows is
    // marked ready and named in its chat.
    bool KnowsMember(ulong contentId);
    // The rest of what the client's ready check start needs to ask the player rather than answer for
    // them: a player character, not between areas, in combat, in a cutscene or bound by a duty.
    bool CanAskThePlayer();
    // AgentReadyCheck's own test, each frame of a running check, that the client is still in that kind
    // of party: InfoProxyCrossRealm.IsCrossRealmParty for a cross-world check, else an alliance or
    // IsLocalPlayerInParty. Failing it, the client ends the check without an answer.
    bool StillInPartyOf(bool crossWorld);
    // GroupManager's main group holds this member, as the client decoded its party list.
    bool MainGroupHas(ulong contentId);
    // AgentReadyCheck's countdown; null with no agent.
    float? ReadyCheckTimer { get; set; }
}

// Done: what the lift put on the client, in order. HandedOver: per opcode, the held packets the client
// took. Warnings: what a patch broke.
internal sealed record LiftResult(List<string> Done, PendingReadyCheck? ReadyCheck, IReadOnlyDictionary<ushort, int> HandedOver, List<string> Warnings);

// The lift: everything the stay held for the party, handed to the client in the order it came through
// the handlers its packets go to, and a held ready check left waiting for the client
// (PendingReadyCheck). It all goes in this one call, before any live packet: what the replays make the
// client ask the server for leaves together, as from a client catching up after a stall. Pacing it
// over later frames couldn't put it back on time and would let live packets in between. From where it
// started, the held check ends as the client would have ended it: unanswered, at the first step that
// leaves the client out of that kind of party.
internal static class PartyLift
{
    public static LiftResult Apply(ServerPartyState state, IPartyClient client, ConditionFlag[] networked, ulong localContentId, DateTime now)
    {
        var done = new List<string>();
        var warnings = new List<string>();
        var handedOver = new Dictionary<ushort, int>();
        var bits = new byte[PacketDispatcherPointers.PlayerStateFlagsLength];
        var partyListFailed = false;
        var check = state.ReadyCheck;
        var checkRunning = false;
        string? checkEndedAt = null;
        foreach (var step in state.Steps)
        {
            if (step is LiftStep.ReadyCheckStarted)
            {
                checkRunning = check != null;
                continue;
            }
            // Whether the client ran a party list: its handler takes the member count as sent, whatever the keys.
            var injected = false;
            // A handler or lookup a patch broke costs only its own step; a party list then counts as not decoded.
            try
            {
                switch (step)
                {
                    case LiftStep.Conditions { Value: var conditions }:
                        if (conditions == PartyConditions.Read(client.Condition)) break;
                        conditions.WriteStateFlags(bits, networked, client.Condition);
                        client.HandleStateFlags(bits);
                        done.Add($"conditions {conditions}");
                        break;
                    case LiftStep.OnlineStatus { Value: var status }:
                    {
                        if (client.OnlineStatusFlags is not { } before) break;
                        var flags = status.MergeInto(before);
                        if (flags == before) break;
                        client.SetOnlineStatusFlags(flags);
                        done.Add($"online status 0x{before:X} -> 0x{flags:X}");
                        break;
                    }
                    case LiftStep.StatusIcon { Value: var icon }:
                        client.HandleActorControl(icon);
                        done.Add($"status icon {icon.P1}");
                        break;
                    case LiftStep.KeyNotReplayed { Why: var why }:
                        partyListFailed = true;
                        done.Add($"a party list key not replayed ({why}), so no later party list was");
                        break;
                    case LiftStep.Packet { Held: var packet }:
                        if (packet.Kind == HeldKind.PartyList && partyListFailed || !client.Inject(packet, $"held {packet.What}"))
                        {
                            done.Add($"{packet.What} not replayed");
                            break;
                        }
                        injected = true;
                        // The client decodes it with its own keys, so only what it then holds shows it worked.
                        if (packet.Kind == HeldKind.PartyList && PartyListPacket.MemberCount(packet.Payload) > 0 && !client.MainGroupHas(localContentId))
                        {
                            partyListFailed = true;
                            done.Add($"{packet.What} didn't decode to a party with the player in it, so no later one was replayed");
                            break;
                        }
                        handedOver[packet.Opcode] = handedOver.GetValueOrDefault(packet.Opcode) + 1;
                        done.Add(packet.What);
                        break;
                }
            }
            catch (Exception e)
            {
                warnings.Add($"{What(step)} threw ({e.Message})");
                if (step is LiftStep.Packet { Held.Kind: HeldKind.PartyList }) partyListFailed = true;
            }
            if (!checkRunning || checkEndedAt != null) continue;
            // A party list the client didn't run leaves its party unknown, and the check unanswered.
            checkEndedAt = step is LiftStep.Packet { Held.Kind: HeldKind.PartyList } && !injected
                ? $"{What(step)}, which the client didn't take, so its party can't be told"
                : EndsCheck(client, check!, step, warnings);
        }
        if (checkEndedAt != null) done.Add($"the held ready check ended at {checkEndedAt}");
        return new LiftResult(done, check != null && checkEndedAt == null ? new PendingReadyCheck(check, localContentId, now) : null, handedOver, warnings);
    }

    // Where, at this step, the client would have ended the held check; null while it's in that kind of party.
    private static string? EndsCheck(IPartyClient client, HeldReadyCheck check, LiftStep step, List<string> warnings)
    {
        try
        {
            return client.StillInPartyOf(check.CrossWorld) ? null : $"{What(step)}, which left the client out of that kind of party";
        }
        catch (Exception e)
        {
            warnings.Add($"reading the client's party after {What(step)} threw ({e.Message}), so the held ready check was dropped");
            return $"{What(step)}, where the client's party couldn't be read";
        }
    }

    private static string What(LiftStep step) => step switch
    {
        LiftStep.Packet { Held: var packet } => packet.What,
        LiftStep.Conditions => "the conditions",
        LiftStep.OnlineStatus => "the online status",
        LiftStep.StatusIcon => "the status icon",
        _ => "a party list key not replayed",
    };
}

// Upstream's Social refresh after a run asks the server for the party lists again. When the lift handed
// the client every party packet the stay held, the client has already asked for what it needed itself,
// as a vanilla client would have, and the refresh would only ask twice.
internal static class PartyResync
{
    // Null to skip the refresh; otherwise why it still runs.
    public static string? Reason(IReadOnlyList<KeyValuePair<ushort, int>> held, IReadOnlyDictionary<ushort, int> handedOver, IReadOnlyCollection<ushort> partyView)
    {
        if (partyView.Count == 0) return "the receiver's table isn't in use";
        var view = held.Where(h => partyView.Contains(h.Key)).ToArray();
        if (view.Length == 0) return "the stay held no party packet";
        var missed = view.Where(h => handedOver.GetValueOrDefault(h.Key) < h.Value).Select(h => $"0x{h.Key:X4}").ToArray();
        return missed.Length == 0 ? null : $"party packets the client didn't get: {string.Join(", ", missed)}";
    }
}

internal enum ReadyCheckOutcome { Waiting, Opened, Dropped }

internal readonly record struct ReadyCheckStep(ReadyCheckOutcome Outcome, string Note = "", bool Warn = false);

// A ready check held through the stay, opened in the client once it can take it: once the client
// counts itself in a party and can ask the player. Before then the client would answer "not ready" for
// the player, to the server, and the answers held with the check would close it at once. A member who
// joined a cross-world party during the stay is only in the client's party once the roster their
// replayed join asks for arrives. At its end a check still waiting is handed over all the same, so the
// client answers it as it would have then. Framework thread only.
internal sealed class PendingReadyCheck(HeldReadyCheck check, ulong localContentId, DateTime liftedAt)
{
    // Once the client counts itself in a party, how long a cross-world check waits for it to know the
    // starter and everyone who answered: it names and marks only a starter it knows, and drops an
    // answer from anyone else.
    public static readonly TimeSpan StarterGrace = TimeSpan.FromSeconds(2);

    private readonly List<HeldPacket> liveAnswers = [];
    private string? dropped;

    public HeldReadyCheck Check => check;

    // An answer that reached the client after the lift: the start the replay hands the client clears the
    // answers it has.
    public void HeardLive(HeldPacket answer) => liveAnswers.Add(answer);

    // The client started another ready check, or a new run began; the next tick drops this one.
    public void Drop(string why) => dropped ??= why;

    // What was held, for the log: who each answer names, and what they answered.
    public string Described() => $"the start and {check.Packets.Count - 1} answers ({string.Join(", ", check.Packets.Skip(1).Select(Answer))})";

    public ReadyCheckStep Tick(DateTime now, IPartyClient client)
    {
        if (dropped != null) return new(ReadyCheckOutcome.Dropped, dropped);
        var left = check.LeftAt(now);
        // Out of that kind of party, the client would have ended the check itself, without an answer.
        if (left <= TimeSpan.Zero)
            return client.StillInPartyOf(check.CrossWorld)
                ? Open(client, now, TimeSpan.Zero, ", at its end")
                : new(ReadyCheckOutcome.Dropped, "its time ran out with the client no longer in that kind of party, where the client ends a check without an answer");
        if (!client.CanAskThePlayer() || !client.CountsItselfInAParty()) return new(ReadyCheckOutcome.Waiting);
        var knowsEveryone = false;
        if (check.CrossWorld)
        {
            knowsEveryone = Knows(client, check.Starter)
                            && check.Packets.Skip(1).Concat(liveAnswers).All(answer => Knows(client, BitConverter.ToUInt64(answer.Payload, 0)));
            // Never so long that too little would be left to answer.
            var graceLeft = StarterGrace - (now - liftedAt);
            if (!knowsEveryone && graceLeft > TimeSpan.Zero && left - graceLeft >= PartyStatusTracker.ReadyCheckMinimumLeft) return new(ReadyCheckOutcome.Waiting);
        }
        return Open(client, now, left,
            !check.CrossWorld ? "" : knowsEveryone ? ", the client knowing the starter and everyone who answered" : ", the client not knowing the starter or someone who answered");
    }

    private ReadyCheckStep Open(IPartyClient client, DateTime now, TimeSpan left, string how)
    {
        var packets = check.Packets.Concat(liveAnswers).ToArray();
        var opened = packets.Count(packet => client.Inject(packet, "held ready check"));
        var (timer, warn) = SetTimer(client, left);
        return new(ReadyCheckOutcome.Opened, $"{(now - liftedAt).TotalSeconds:F1}s after the lift{how}; {opened} of {packets.Length} packets, {liveAnswers.Count} of them answers after the lift; {timer}",
            warn);
    }

    // Only the names and ready marks hang on it, so a lookup a patch broke counts as not knowing them.
    private static bool Knows(IPartyClient client, ulong contentId)
    {
        try
        {
            return client.KnowsMember(contentId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // The check started that long ago, so it gets only what's left: at least the moment the client
    // needs to count it down and answer for the player, at most its own 30 s. Left alone unless the
    // timer reads what the client just opened it with: a patch may have moved it, or the client may
    // not have opened the window. At its end the client may have answered for the player instead of
    // asking, which leaves the timer at 0, 10 or 35.
    private static (string Note, bool Warn) SetTimer(IPartyClient client, TimeSpan left)
    {
        if (client.ReadyCheckTimer is not { } timer) return ("no ready check agent", true);
        if (timer != (float)HeldReadyCheck.Window.TotalSeconds)
            return ($"the timer read {timer} after the replay, not {HeldReadyCheck.Window.TotalSeconds}, and was left as is", left > TimeSpan.Zero || timer is not (0f or 10f or 35f));
        var seconds = Math.Clamp(left.TotalSeconds, ShortestTimer, HeldReadyCheck.Window.TotalSeconds);
        client.ReadyCheckTimer = (float)seconds;
        return ($"{seconds:F1}s left", false);
    }

    // The client counts a check's timer down only while it's above zero.
    private const double ShortestTimer = 0.001;

    // A cross-world answer is a member's content id and their answer at +8; a party event names its
    // actor at +0x10.
    private string Answer(HeldPacket packet)
    {
        var who = BitConverter.ToUInt64(packet.Payload, check.CrossWorld ? 0 : 0x10);
        var named = who == localContentId ? "you" : who == check.Starter ? "the starter" : "another member";
        return check.CrossWorld ? $"{named}: {packet.Payload[8]}" : named;
    }
}

// The held ready check from the lift until the client opens it or it's dropped, and what drops it.
// ServerPartyWatch feeds it the client's own events. Framework thread only.
internal sealed class HeldReadyCheckReplay
{
    private PendingReadyCheck? pending;
    private IPartyClient? client;

    public HeldReadyCheck? Check => pending?.Check;

    public void Hold(PendingReadyCheck check, IPartyClient on)
    {
        pending = check;
        client = on;
    }

    // AgentReadyCheck's reset (ClientStructs' InitiateReadyCheck): for a check the client received,
    // for the player's own, and when the agent loses its party. Any but the plugin's own replay ends
    // the wait, as the held check's start would reset the newer one.
    public ReadyCheckStep? AgentReset(bool replaying, DateTime now) => replaying ? null : Drop("the client started another ready check", now);

    public ReadyCheckStep? Drop(string why, DateTime now)
    {
        pending?.Drop(why);
        return Tick(now);
    }

    // A live packet on its way to the client: an answer to the held check is kept for it.
    public void HeardLive(PartyStatusTracker tracker, nint packet, uint actor)
    {
        if (pending is { } check && tracker.ReadyCheckAnswer(packet, actor, check.Check.CrossWorld) is { } answer) check.HeardLive(answer);
    }

    // Null while it waits, or with none held; otherwise how it ended. Nothing more is injected once
    // reading the client throws.
    public ReadyCheckStep? Tick(DateTime now)
    {
        if (pending is not { } check || client is not { } on) return null;
        ReadyCheckStep step;
        try
        {
            step = check.Tick(now, on);
        }
        catch (Exception e)
        {
            step = new ReadyCheckStep(ReadyCheckOutcome.Dropped, $"reading the client's party threw ({e.Message})", Warn: true);
        }
        if (step.Outcome == ReadyCheckOutcome.Waiting) return null;
        pending = null;
        client = null;
        return step;
    }
}

// The party's status from the server during a stay. The sim owns the party conditions then, so the
// receive filter keeps these packets from the client like any other; the plugin shows what they
// say, and the lift hands the client everything the server said about the party, in the order it
// came: the party's part of the conditions, online status bits and the player's status icon
// (ActorControl 504, the OnlineStatus row the character shows), and the packets themselves, replayed
// (Party Finder notices, a cross-world party's joins, leaves, removals, disband, rosters and members'
// status, a party of one world's list and the key its decoding takes). The client keeps "recruiting"
// in each of the three states (the Duty Finder reads the icon and the bits, party commands the bits),
// so a listing that ends during a run has to end in all of them. No roster comes with a cross-world
// join: the client counts a joiner in its party only once the roster its join handler asks the server
// for arrives. A packet is replayed only if it reads as what its opcode is taken for. A blacklisted
// player is shown without their name, as the client does.
internal sealed class PartyStatusTracker(
    ConditionFlag[] networked,
    Func<uint, string?> logMessageText,
    Func<ushort, string?> worldName,
    Func<ulong, ulong, bool> isBlacklisted)
{
    private const int IpcHeaderSize = 0x10;
    private const string RecruitmentEndedText = "Party Finder recruitment ended.";
    private const string BlacklistedPlayer = "A blacklisted player";
    private const string SomeMember = "A party member";
    // LogMessage "Party recruitment ended. All places have been filled."
    private const uint AllPlacesFilled = 983;
    // Generous: the server sends one per listing change.
    private const int MaxNotices = 64;
    // Generous: one per join, leave, removal, return or disband.
    private const int MaxMembership = 64;
    // Generous: a whole party each, so past the cap the oldest of its kind goes.
    private const int MaxWholeParty = 64;
    // Generous: the party's states, each kept where it came.
    private const int MaxStates = 64;
    private const int MemberStatusEntryLength = 0x18;
    private const int MemberStatusLength = 8 * MemberStatusEntryLength;
    // The dispatcher reads only these of the key packet.
    private const int PartyKeyLength = 0x10;
    // Generous: the server rekeys rarely.
    private const int MaxKeys = 64;
    private const int RosterEntryNameOffset = 8 + 0x33;
    private const int RosterEntryLength = 0x58;
    // A member leaving is their content id, all the client reads.
    private const int MemberLeftLength = 8;
    private const int MemberEventNameOffset = 0x22;
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);
    // A member's removal: the client reads only their content id, at +8.
    private const int MemberRemovedLength = 0x10;
    private const int RemovedMemberOffset = 8;
    // The client reads nothing of a disband.
    private const int DisbandedLength = 0x10;
    public const ushort StatusIconCategory = 504;
    // Less than this left isn't enough to answer a ready check replayed at the lift.
    public static readonly TimeSpan ReadyCheckMinimumLeft = TimeSpan.FromSeconds(2);

    // Opcodes change with every game version: these come from the client's protocol receiver and
    // from what its handlers do with live packets (ServerPartyWatch); 0 until known.
    public ushort PlayerStateFlagsOpcode { get; set; }
    public ushort OnlineStatusOpcode { get; set; }
    public ushort NoticeOpcode { get; set; }
    // The receiver's one roster packet; without the receiver, every opcode a handler named.
    public ushort[] RosterOpcodes { get; set; } = [];
    // Every ActorControl form; the status icon comes in one of them.
    public ushort[] ActorControlOpcodes { get; set; } = [];
    // The cross-world party's member events (PartyMemberEvent), its "member left" (the content id at
    // +0), its leader removing a member, and its end.
    public ushort MemberEventOpcode { get; set; }
    public ushort MemberLeftOpcode { get; set; }
    public ushort MemberRemovedOpcode { get; set; }
    public ushort DisbandedOpcode { get; set; }
    // A cross-world party's members' status, a party of one world's list (UpdateParty) and the packet
    // the dispatcher keys that list's decoding with.
    public ushort MemberStatusOpcode { get; set; }
    public ushort PartyListOpcode { get; set; }
    public ushort PartyKeyOpcode { get; set; }
    // The key packet's opcode once a live packet of it changed the dispatcher's keys (ServerPartyWatch).
    public ushort ConfirmedPartyKeyOpcode { get; set; }
    // Every opcode whose packet changes what the client knows of the party (PartyPacketMethods
    // .PartyView); empty without the receiver.
    public ushort[] PartyViewOpcodes { get; set; } = [];
    // Party events (PartyEvent): ready checks among them. A cross-world party's ready check comes
    // in two packets of its own instead (CrossWorldReadyCheck).
    public ushort PartyEventOpcode { get; set; }
    public ushort CrossWorldReadyCheckOpcode { get; set; }
    public ushort CrossWorldReadyCheckAnswerOpcode { get; set; }

    // The detour may not run on the framework thread, which reads these.
    private readonly object sync = new();
    private readonly List<PartyStatusEvent> events = [];
    // Everything the lift hands the client, in the order it came.
    private readonly List<LiftStep> steps = [];
    private int noticesKept;
    private int membershipKept;
    private int statesKept;
    private int keysKept;
    private bool keyNotReplayed;
    private readonly Dictionary<HeldKind, int> wholePartyKept = [];
    // Null while unknown: the next roster is then taken as it is, since nothing says what changed.
    private PartyRoster? roster;
    private ulong localContentId;
    private ulong localAccountId;
    private uint localEntityId;
    private PartyConditions conditions;
    private PartyOnlineStatus status;
    private HeldReadyCheck? readyCheck;
    // A kept step after its start ends it at the lift (PartyLift), as far as the packets tell.
    private bool readyCheckEnds;
    private readonly Dictionary<ushort, int> held = [];
    private bool listingEndNoticed;
    private bool recruitmentEnded;

    public int NoticesDropped { get; private set; }
    public int MembershipDropped { get; private set; }
    public int WholePartyDropped { get; private set; }
    public int StatesMerged { get; private set; }
    public int KeysNotReplayed { get; private set; }

    public ulong LocalContentId
    {
        get { lock (sync) return localContentId; }
    }

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
    public void Begin(PartyRoster? rosterNow, PartyConditions conditionsNow, PartyOnlineStatus statusNow, ulong localPlayerContentId, uint localPlayerEntityId,
        ulong localPlayerAccountId)
    {
        lock (sync)
        {
            roster = rosterNow;
            localContentId = localPlayerContentId;
            localAccountId = localPlayerAccountId;
            localEntityId = localPlayerEntityId;
            conditions = conditionsNow;
            status = statusNow;
            readyCheck = null;
            readyCheckEnds = false;
            held.Clear();
            listingEndNoticed = false;
            recruitmentEnded = false;
            events.Clear();
            ClearSteps();
            NoticesDropped = 0;
            MembershipDropped = 0;
            WholePartyDropped = 0;
            StatesMerged = 0;
            KeysNotReplayed = 0;
        }
    }

    private void ClearSteps()
    {
        steps.Clear();
        noticesKept = 0;
        membershipKept = 0;
        statesKept = 0;
        wholePartyKept.Clear();
        keysKept = 0;
        keyNotReplayed = false;
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
                AddState(new LiftStep.StatusIcon(new HeldActorControl(targetId, StatusIconCategory, p[0], p[1], p[2], p[3])));
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
            lock (sync) NoteNotice(notice, new HeldPacket(opcode, targetId, notice.Payload, $"Party Finder notice (code {notice.Code}, message {notice.LogMessage})"), text, at);
            return true;
        }
        if (Array.IndexOf(RosterOpcodes, opcode) >= 0)
        {
            var next = PartyRoster.FromPacket(payload);
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, PartyRoster.Length).ToArray(), $"roster ({next.Members.Count} members)",
                HeldKind.Roster);
            lock (sync)
            {
                if (ReadsAsRoster(next, copy.Payload)) KeepWholeParty(copy);
                NoteRoster(next, at);
            }
            return true;
        }
        if (opcode == MemberStatusOpcode)
        {
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, MemberStatusLength).ToArray(), "members' status", HeldKind.MemberStatus);
            lock (sync)
                if (ReadsAsMemberStatus(copy.Payload)) KeepWholeParty(copy);
            return true;
        }
        if (opcode == PartyListOpcode)
        {
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, PartyListPacket.Length).ToArray(), "", HeldKind.PartyList);
            copy = copy with { What = $"party list ({PartyListPacket.MemberCount(copy.Payload)} members)" };
            lock (sync)
            {
                if (!ReadsAsPartyList(copy.Payload)) return true;
                KeepWholeParty(copy);
                if (PartyListPacket.MemberCount(copy.Payload) <= 1) EndsHeldCheck(crossWorld: false);
            }
            return true;
        }
        // The client takes a packet as the key by its own opcode, so only one a live packet confirmed is
        // replayed: anything else there may be another packet a patch moved, which would reach its own
        // handler cut short. Missing a key, the client decodes every later party list wrongly.
        if (opcode == PartyKeyOpcode)
        {
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, PartyKeyLength).ToArray(), "party list key", HeldKind.PartyKey);
            lock (sync) KeepKey(copy);
            return true;
        }
        if (opcode == MemberEventOpcode)
        {
            var memberEvent = PartyMemberEvent.FromPacket(payload);
            var filled = memberEvent.FillsListing ? logMessageText(AllPlacesFilled) : null;
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, PartyMemberEvent.Length).ToArray(),
                $"member event (kind {memberEvent.Kind}{(memberEvent.ListingFull ? ", listing full" : "")})");
            lock (sync) NoteMemberEvent(memberEvent, filled, copy, at);
            return true;
        }
        if (opcode == MemberLeftOpcode)
        {
            var contentId = *(ulong*)payload;
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, MemberLeftLength).ToArray(), "member left");
            lock (sync) NoteMemberLeft(contentId, copy, at);
            return true;
        }
        if (opcode == MemberRemovedOpcode)
        {
            var contentId = *(ulong*)(payload + RemovedMemberOffset);
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, MemberRemovedLength).ToArray(), "member removed");
            lock (sync) NoteMemberRemoved(contentId, copy, at);
            return true;
        }
        if (opcode == DisbandedOpcode)
        {
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, DisbandedLength).ToArray(), "party disbanded");
            lock (sync) NoteDisbanded(copy, at);
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
                    StartReadyCheck(Describe(new PartyMember(partyEvent.ActorContentId, partyEvent.ActorName, 0, partyEvent.ActorAccountId)), copy, partyEvent.ActorContentId, crossWorld: false, at);
                else AnswerReadyCheck(copy, at);
            }
            return true;
        }
        if (opcode == CrossWorldReadyCheckOpcode)
        {
            var contentId = *(ulong*)payload;
            var copy = new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>((void*)payload, CrossWorldReadyCheck.StartLength).ToArray());
            lock (sync)
                StartReadyCheck(roster?.Members.FirstOrDefault(m => m.ContentId == contentId) is { ContentId: not 0 } starter ? Describe(starter) : SomeMember, copy, contentId,
                    crossWorld: true, at);
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

    // A live answer to a ready check of that kind, copied as a replay hands it back; null for any
    // other packet.
    public unsafe HeldPacket? ReadyCheckAnswer(nint packet, uint targetId, bool crossWorld)
    {
        var opcode = *(ushort*)(packet + 2);
        var payload = (byte*)(packet + IpcHeaderSize);
        if (opcode == 0) return null;
        if (crossWorld && opcode == CrossWorldReadyCheckAnswerOpcode)
            return new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>(payload, CrossWorldReadyCheck.AnswerLength).ToArray());
        if (!crossWorld && opcode == PartyEventOpcode && payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckAnswered)
            return new HeldPacket(opcode, targetId, new ReadOnlySpan<byte>(payload, PartyEvent.Length).ToArray());
        return null;
    }

    // The ready check that started during the stay and how long is left to answer it, while any is.
    public ReadyCheckAlert? ReadyCheckAt(DateTime now)
    {
        lock (sync) return readyCheck is { } check && !readyCheckEnds && check.LeftAt(now) > TimeSpan.Zero ? new ReadyCheckAlert(check.From, check.LeftAt(now)) : null;
    }

    // For the lift: everything to hand the client, oldest first, and a ready check that hasn't run out.
    public ServerPartyState TakeFromServer(DateTime now)
    {
        lock (sync)
        {
            var openCheck = readyCheck is { } check && check.LeftAt(now) > TimeSpan.Zero ? check : null;
            var taken = new ServerPartyState(steps.ToArray(), openCheck, held.OrderBy(h => h.Key).ToArray());
            readyCheck = null;
            readyCheckEnds = false;
            ClearSteps();
            held.Clear();
            return taken;
        }
    }

    private void NoteConditions(PartyConditions next, DateTime at)
    {
        var wasRecruiting = Recruiting;
        if (!conditions.CrossWorldParty && next.CrossWorldParty) events.Add(new PartyStatusEvent(at, "The party is now a cross-world party."));
        conditions = next;
        AddState(new LiftStep.Conditions(next));
        NoteRecruiting(wasRecruiting, at);
    }

    private void NoteStatus(PartyOnlineStatus next, DateTime at)
    {
        var wasRecruiting = Recruiting;
        status = next;
        AddState(new LiftStep.OnlineStatus(next));
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
    private void NoteMemberEvent(PartyMemberEvent memberEvent, string? filled, HeldPacket packet, DateTime at)
    {
        if (memberEvent.Member.ContentId != 0 && ReadsAsName(packet.Payload.AsSpan(MemberEventNameOffset, PartyMember.NameLength))) KeepMembership(packet);
        var member = memberEvent.Member;
        if (memberEvent.Joined && !IsLocal(member))
        {
            events.Add(new PartyStatusEvent(at, $"{Describe(member)} joined the party."));
            if (roster is { } known && known.Members.All(m => m.ContentId != member.ContentId))
                roster = known with { Members = [.. known.Members, member] };
        }
        if (memberEvent.FillsListing) NoteListingFilled(filled, at);
    }

    private void NoteMemberLeft(ulong contentId, HeldPacket packet, DateTime at)
    {
        if (ReadsAsMember(contentId) && KeepMembership(packet) && contentId == localContentId) EndsHeldCheck(crossWorld: true);
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

    // The client, handed this at the lift, prints its own line and, for the player, leaves the party.
    private void NoteMemberRemoved(ulong contentId, HeldPacket packet, DateTime at)
    {
        if (ReadsAsMember(contentId) && KeepMembership(packet) && contentId == localContentId) EndsHeldCheck(crossWorld: true);
        if (contentId == localContentId)
        {
            events.Add(new PartyStatusEvent(at, "You were removed from the party."));
            return;
        }
        PartyMember? removed = null;
        if (roster is { } known)
        {
            foreach (var member in known.Members)
                if (member.ContentId == contentId) removed = member;
            roster = known with { Members = known.Members.Where(m => m.ContentId != contentId).ToArray() };
        }
        events.Add(new PartyStatusEvent(at, $"{(removed is { } who ? Describe(who) : SomeMember)} was removed from the party."));
    }

    // The client, handed this at the lift, prints its own line and leaves the party.
    private void NoteDisbanded(HeldPacket packet, DateTime at)
    {
        if ((conditions.CrossWorldParty || roster is { Members.Count: > 0 }) && KeepMembership(packet)) EndsHeldCheck(crossWorld: true);
        if (roster is { } known) roster = known with { Members = [] };
        events.Add(new PartyStatusEvent(at, "The party was disbanded."));
    }

    private void StartReadyCheck(string from, HeldPacket start, ulong starter, bool crossWorld, DateTime at)
    {
        readyCheck = new HeldReadyCheck(at, from, [start], starter, crossWorld);
        readyCheckEnds = false;
        steps.RemoveAll(step => step is LiftStep.ReadyCheckStarted);
        steps.Add(new LiftStep.ReadyCheckStarted());
        events.Add(new PartyStatusEvent(at, $"{from} started a ready check."));
    }

    // Only what reads as the packet its opcode is taken for is handed back to the client: a receiver
    // table a patch shifted without changing its size must never make the client handle another packet,
    // cut short. A member is someone in the party as the plugin knows it, or the player.
    private bool ReadsAsMember(ulong contentId)
        => contentId != 0 && (contentId == localContentId || roster is { } known && known.Members.Any(m => m.ContentId == contentId));

    private static bool ReadsAsName(ReadOnlySpan<byte> name)
    {
        var end = name.IndexOf((byte)0);
        if (end <= 0) return false;
        foreach (var b in name[..end])
            if (b < 0x20) return false;
        try
        {
            StrictUtf8.GetString(name[..end]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool KeepMembership(HeldPacket packet)
    {
        if (membershipKept >= MaxMembership)
        {
            MembershipDropped++;
            return false;
        }
        membershipKept++;
        steps.Add(new LiftStep.Packet(packet));
        return true;
    }

    // The client out of that kind of party: a cross-world one it left or that ended, or a party of one
    // world down to the player.
    private void EndsHeldCheck(bool crossWorld)
    {
        if (readyCheck is { } check && check.CrossWorld == crossWorld) readyCheckEnds = true;
    }

    // Past the cap, or on an opcode no live packet confirmed, the first key that isn't replayed stands in
    // the steps for every later one.
    private void KeepKey(HeldPacket packet)
    {
        if (packet.Opcode == ConfirmedPartyKeyOpcode && keysKept < MaxKeys)
        {
            keysKept++;
            steps.Add(new LiftStep.Packet(packet));
            return;
        }
        KeysNotReplayed++;
        if (keyNotReplayed) return;
        keyNotReplayed = true;
        steps.Add(new LiftStep.KeyNotReplayed(packet.Opcode == ConfirmedPartyKeyOpcode ? "past the cap" : "no live packet confirmed its opcode"));
    }

    // Each holds the whole party, so past the cap the newest of its kind pushes out the oldest.
    private void KeepWholeParty(HeldPacket packet)
    {
        if (wholePartyKept.GetValueOrDefault(packet.Kind) >= MaxWholeParty)
        {
            steps.RemoveAt(steps.FindIndex(step => step is LiftStep.Packet { Held: var held } && held.Kind == packet.Kind));
            WholePartyDropped++;
        }
        else wholePartyKept[packet.Kind] = wholePartyKept.GetValueOrDefault(packet.Kind) + 1;
        steps.Add(new LiftStep.Packet(packet));
    }

    // Each state is kept where it came; past the cap, one takes the place of the last of its kind since
    // the last packet, if there is one.
    private void AddState(LiftStep state)
    {
        if (statesKept >= MaxStates)
        {
            for (var i = steps.Count - 1; i >= 0 && steps[i] is not LiftStep.Packet; i--)
            {
                if (steps[i].GetType() != state.GetType()) continue;
                steps[i] = state;
                StatesMerged++;
                return;
            }
        }
        statesKept++;
        steps.Add(state);
    }

    // A roster reads as this party's: every member named, the player among them (or no one at all),
    // and the leader one of them.
    private bool ReadsAsRoster(PartyRoster next, byte[] payload)
    {
        if (next.Members.Count == 0) return next.LeaderContentId == 0;
        for (var i = 0; i < 8; i++)
        {
            var entry = 8 + i * RosterEntryLength;
            if (BitConverter.ToUInt64(payload, entry + 0x10) != 0 && !ReadsAsName(payload.AsSpan(RosterEntryNameOffset + i * RosterEntryLength, PartyMember.NameLength)))
                return false;
        }
        return next.Members.Any(m => m.ContentId == localContentId) && (next.LeaderContentId == 0 || next.Members.Any(m => m.ContentId == next.LeaderContentId));
    }

    // Each of its eight entries begins with a member's content id: some, and only the party's.
    private bool ReadsAsMemberStatus(byte[] payload)
    {
        var any = false;
        for (var at = 0; at < payload.Length; at += MemberStatusEntryLength)
        {
            var contentId = BitConverter.ToUInt64(payload, at);
            if (contentId == 0) continue;
            if (!ReadsAsMember(contentId)) return false;
            any = true;
        }
        return any;
    }

    // A party list reads as this player's: at most a full party, a leader among them, each member
    // named and valid, nothing past them, and the player one of them; or no one at all, as when the
    // player leaves. Only its plain fields can be read.
    private bool ReadsAsPartyList(byte[] payload)
    {
        var count = PartyListPacket.MemberCount(payload);
        var leader = PartyListPacket.LeaderIndex(payload);
        if (count > PartyListPacket.Capacity || (count == 0 ? leader != 0 : leader >= count)) return false;
        var hasPlayer = false;
        for (var i = 0; i < PartyListPacket.Capacity; i++)
        {
            var account = PartyListPacket.AccountId(payload, i);
            if (i >= count)
            {
                if (account != 0 || PartyListPacket.Name(payload, i).IndexOfAnyExcept((byte)0) >= 0) return false;
                continue;
            }
            if (!ReadsAsName(PartyListPacket.Name(payload, i)) || (PartyListPacket.Valid(payload, i) & 1) == 0) return false;
            hasPlayer |= account != 0 && account == localAccountId;
        }
        return count == 0 || hasPlayer;
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
    private void NoteNotice(PartyNotice notice, HeldPacket packet, string? text, DateTime at)
    {
        if (noticesKept < MaxNotices)
        {
            noticesKept++;
            steps.Add(new LiftStep.Packet(packet));
        }
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
