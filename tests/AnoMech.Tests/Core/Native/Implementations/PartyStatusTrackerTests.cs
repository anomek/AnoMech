using System.Runtime.InteropServices;
using System.Text;
using AnoMech.Core.Native.Implementations;
using Dalamud.Game.ClientState.Conditions;

namespace AnoMech.Tests;

// The real party during a run: the firewall keeps the server's party packets from the client, the
// plugin shows what they say, and the lift puts the server's party state on the client. Packets are
// laid out as the client reads them: PlayerStateFlags' bit order from the Condition sheet, the online
// status packet's u64 of OnlineStatus rows, ActorControl's category and params, the Party Finder
// notice's 16 bytes, the cross-world roster's leader and eight 0x58-byte members. FakeClient applies
// them as the client's handlers do, and is recruiting while any of what its checks read says so:
// the Duty Finder reads the player's status icon and the online status bits. That a notice with code
// 0 ends the Party Finder's own listing, and where the roster keeps names, come from the client's
// code, not from a capture.
public class PartyStatusTrackerTests
{
    private const ushort PlayerStateFlagsOpcode = 0x0155;
    private const ushort OnlineStatusOpcode = 0x0399;
    private const ushort NoticeOpcode = 0x02F1;
    private const ushort RosterOpcode = 0x0187;
    private const ushort RosterAnswerOpcode = 0x0244;
    private const ushort ActorControlOpcode = 0x0311;
    private const ushort ActorControlTargetOpcode = 0x0313;
    private const ushort MemberEventOpcode = 0x0360;
    private const ushort MemberLeftOpcode = 0x01C9;
    private const ushort MemberUpdateOpcode = 0x01F7;
    private const ushort PartyEventOpcode = 0x0335;
    private const ushort CrossWorldReadyCheckOpcode = 0x03A0;
    private const ushort CrossWorldReadyCheckAnswerOpcode = 0x0290;
    private const ushort KeepAliveCategory = 24;
    private const uint LocalPlayer = 0x10000001;
    private const uint OtherPlayer = 0x10000002;
    private static readonly DateTime Clock = new(2026, 11, 8, 20, 15, 0);
    private static readonly ConditionFlag[] Networked = DataminingGameData.NetworkedConditions();

    // OnlineStatus sheet rows.
    private const int Busy = 12;
    private const int AwayFromKeyboard = 17;
    private const int WaitingForDutyFinder = 25;
    private const int RecruitingPartyMembers = 26;
    private const int Mentor = 27;
    private const int PartyLeader = 36;
    private const int CrossWorldPartyLeader = 38;
    private const int Online = 47;

    // LogMessage rows.
    private const uint AllPlacesFilled = 983;
    private const uint RecruitmentCommenced = 979;
    private const uint NamesSomeone = 7443;
    private static readonly Dictionary<uint, string?> LogMessages = new()
    {
        [AllPlacesFilled] = "Party recruitment ended. All places have been filled.",
        [RecruitmentCommenced] = "Party recruitment commenced.",
        [NamesSomeone] = null,
    };

    private const ulong Listing = 0x0001_2345_6789_ABCD;

    // World sheet rows, row 0 among them as the sheet has it; 99 stands for one the sheet has no name for.
    private static readonly Dictionary<ushort, string> Worlds = new() { [0] = "Dev", [21] = "Ravana", [74] = "Coeurl" };
    private static readonly Member Me = new(0x0040_0000_0000_0001, "Leader Me", 74);
    private static readonly Member Alpha = new(0x0040_0000_0000_0008, "Alpha Eight", 21);
    private static readonly Member Two = new(0x0040_0000_0000_0002, "Two Two", 74);
    private static readonly Member Three = new(0x0040_0000_0000_0003, "Three Three", 99);
    private static readonly Member Blocked = new(0x0040_0000_0000_0066, "Blocked Person", 21, AccountId: 0x1234_5678);
    private static readonly HashSet<ulong> BlacklistedAccounts = [Blocked.AccountId];
    private static readonly Member[] Members = [Me, Alpha, Two, Three, Blocked];

    public readonly record struct Member(ulong ContentId, string Name, ushort HomeWorld, ulong AccountId = 0);

    public enum Sent { Icon, Status, Conditions }

    private static IEnumerable<TestCaseData> EveryOrder()
    {
        Sent[][] orders =
        [
            [Sent.Icon, Sent.Status, Sent.Conditions],
            [Sent.Icon, Sent.Conditions, Sent.Status],
            [Sent.Status, Sent.Icon, Sent.Conditions],
            [Sent.Status, Sent.Conditions, Sent.Icon],
            [Sent.Conditions, Sent.Icon, Sent.Status],
            [Sent.Conditions, Sent.Status, Sent.Icon],
        ];
        foreach (var order in orders)
            yield return new TestCaseData((object)order).SetArgDisplayNames(string.Join(", ", order));
    }

    // Someone joins during the run and takes the listing's last place: the server ends it with the
    // player's status icon, the online status and the conditions, in whatever order, and no Party
    // Finder notice. The client keeps "recruiting" in each.
    [TestCaseSource(nameof(EveryOrder))]
    public void AListingThatFillsDuringARunIsOverOnceTheRunEnds(Sent[] order)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        foreach (var sent in order)
        {
            switch (sent)
            {
                case Sent.Icon: run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor)); break;
                case Sent.Status: run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online)); break;
                case Sent.Conditions: run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance)); break;
            }
        }

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Party Finder recruitment ended." }));
        Assert.That(run.Tracker.RecruitmentEnded, Is.True, "the window's alert");
        Assert.That(client.UsingPartyFinder, Is.True, "the client hears nothing until the run ends");

        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.UsingPartyFinder, Is.False);
            Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
            Assert.That(client.HasOnlineStatus(CrossWorldPartyLeader), Is.True);
            Assert.That(client[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance], Is.True);
        });
    }

    // After a patch the plugin's own signatures may match nothing. Starting the listing, before the
    // run, the client's handlers (which ClientStructs keeps current) still see the online status and
    // the icon go by.
    [Test]
    public void AListingThatFillsDuringARunIsOverWhenOnlyLivePacketsTaughtTheOpcodes()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false);
        run.Tracker.PlayerStateFlagsOpcode = PlayerStateFlagsOpcode;
        using var live = new Packets();
        var startedStatus = OnlineStatus(RecruitingPartyMembers, CrossWorldPartyLeader, Online);
        run.Tracker.LearnOnlineStatus(live.Add(OnlineStatusOpcode, startedStatus), BitConverter.ToUInt64(startedStatus));
        var startedIcon = live.Add(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers));
        run.Tracker.LearnActorControl(startedIcon, LocalPlayer, LocalPlayer, PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers);

        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.Lift();

        Assert.That(client.UsingPartyFinder, Is.False);
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
    }

    // The handlers run for the plugin's own calls too, and for other packets with other values.
    [Test]
    public void OnlyAHandlerCallCarryingTheDispatchedPacketTeachesItsOpcode()
    {
        using var run = new Run(new FakeClient(), learned: false);
        using var live = new Packets();
        var status = live.Add(OnlineStatusOpcode, OnlineStatus(RecruitingPartyMembers, Online));
        var icon = live.Add(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers));
        const ushort iconCategory = PartyStatusTracker.StatusIconCategory;

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.LearnOnlineStatus(0, 1UL << RecruitingPartyMembers | 1UL << Online), Is.False, "the plugin's own call");
            Assert.That(run.Tracker.LearnOnlineStatus(status, 1UL << Busy), Is.False, "other flags");
            Assert.That(run.Tracker.LearnActorControl(0, LocalPlayer, LocalPlayer, iconCategory, RecruitingPartyMembers), Is.False, "the plugin's own call");
            Assert.That(run.Tracker.LearnActorControl(icon, LocalPlayer, OtherPlayer, iconCategory, RecruitingPartyMembers), Is.False, "another actor");
            Assert.That(run.Tracker.LearnActorControl(icon, LocalPlayer, LocalPlayer, KeepAliveCategory, RecruitingPartyMembers), Is.False, "another category");
            Assert.That(run.Tracker.LearnActorControl(icon, LocalPlayer, LocalPlayer, iconCategory, Mentor), Is.False, "another value");
            Assert.That(run.Tracker.OnlineStatusOpcode, Is.Zero);
            Assert.That(run.Tracker.ActorControlOpcodes, Is.Empty);
        });
    }

    // A ready check during the run: the plugin counts down the time to answer it, and leaving the sim
    // in time opens it in the game with only that time, answers included.
    [Test]
    public void AReadyCheckDuringARunCanStillBeAnsweredIfTheRunEndsInTime()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckAnswered), at: Clock.AddSeconds(2));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, kind: 3), at: Clock.AddSeconds(3));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two started a ready check." }));
        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(12)), Is.EqualTo(new ReadyCheckAlert("Two Two", TimeSpan.FromSeconds(18))));
        Assert.That(client.ReadyCheckFrom, Is.Null, "the client hears nothing until the run ends");

        run.Lift(Clock.AddSeconds(12));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(1));
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(18).Within(0.001));
            Assert.That(client.PacketsHandled, Is.EqualTo(2), "the start and the answer, nothing else the packet carried");
        });
    }

    // A cross-world party's ready check comes in packets of its own: the start names nobody, so the
    // plugin names the starter from the party as it knows it.
    [Test]
    public void ACrossWorldPartysReadyCheckDuringARunCanStillBeAnsweredIfTheRunEndsInTime()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two, Three]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Three, answer: 1), at: Clock.AddSeconds(4));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) started a ready check." }));
        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(20)), Is.EqualTo(new ReadyCheckAlert("Two Two (Coeurl)", TimeSpan.FromSeconds(10))));

        run.Lift(Clock.AddSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(1));
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(10).Within(0.001));
        });
    }

    [Test]
    public void AReadyCheckFromSomeoneThePluginCantNameIsStillShown()
    {
        using var run = new Run(new FakeClient(), rosterRead: false);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "A party member started a ready check." }));
        Assert.That(run.Tracker.ReadyCheckAt(Clock), Is.Not.Null);
    }

    [Test]
    public void AReadyCheckThatRanOutDuringTheRunStaysClosed()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckAnswered), at: Clock.AddSeconds(31));

        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(30)), Is.Null, "the alert");
        run.Lift(Clock.AddSeconds(29));
        Assert.That(client.ReadyCheckFrom, Is.Null, "too little left to answer");
    }

    // The packet carries other party events too, and an answer belongs to a check.
    [Test]
    public void OnlyAReadyCheckStartedDuringTheRunIsKept()
    {
        var client = new FakeClient();
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, kind: 3));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckAnswered));
        run.Lift();

        Assert.That(run.Tracker.Events, Is.Empty);
        Assert.That(client.PacketsHandled, Is.Zero);
    }

    // Ready checks happen outside runs too; the client starting one it received names the opcode.
    [Test]
    public void ThePartyEventOpcodeIsLearnedFromAReadyCheckTheClientReceives()
    {
        using var run = new Run(new FakeClient(), learned: false);
        using var live = new Packets();

        run.Tracker.CrossWorldReadyCheckOpcode = CrossWorldReadyCheckOpcode;
        var crossWorldStart = new byte[PartyEvent.Length];
        crossWorldStart[PartyEvent.KindOffset] = PartyEvent.ReadyCheckStarted;

        Assert.That(run.Tracker.LearnPartyEvent(0), Is.False, "the plugin's own start");
        Assert.That(run.Tracker.LearnPartyEvent(live.Add(PartyEventOpcode, PartyEventPacket(Two, kind: 3))), Is.False, "another kind");
        Assert.That(run.Tracker.LearnPartyEvent(live.Add(CrossWorldReadyCheckOpcode, crossWorldStart)), Is.False, "the cross-world start, past its payload");
        Assert.That(run.Tracker.LearnPartyEvent(live.Add(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted))), Is.True);
        Assert.That(run.Tracker.LearnPartyEvent(live.Add(0x0399, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted))), Is.False, "a known one stays");
        Assert.That(run.Tracker.PartyEventOpcode, Is.EqualTo(PartyEventOpcode));
    }

    // The cross-world start's case in the party handler's byte-table switch calls the function it's
    // handed to.
    [Test]
    public void ThePartyHandlerNamesTheOpcodeWhoseCaseCallsAFunction()
    {
        using var handler = new FakePartyHandler(first: 0x0355, count: 0x91);
        handler.WriteCaseCalling(FakePartyHandler.ReadyCheckCase, FakePartyHandler.ReadyCheckStart);
        handler.Route(CrossWorldReadyCheckOpcode, caseNumber: 5, FakePartyHandler.ReadyCheckCase);
        handler.Route(MemberEventOpcode, caseNumber: 3, FakePartyHandler.MemberEventCase);

        Assert.That(handler.ByteTableOpcodeCalling(FakePartyHandler.ReadyCheckStart), Is.EqualTo(CrossWorldReadyCheckOpcode));
        handler.Route(0x0358, caseNumber: 5, FakePartyHandler.ReadyCheckCase);
        Assert.That(handler.ByteTableOpcodeCalling(FakePartyHandler.ReadyCheckStart), Is.Zero, "two opcodes");
    }

    // A ready check can come before any has outside a run: the opcode is the one whose dispatcher
    // case calls the party event handler.
    [Test]
    public void TheDispatcherNamesTheOpcodeWhoseCaseCallsAHandler()
    {
        using var dispatcher = new FakeDispatcher();
        dispatcher.WriteCaseCalling(FakeDispatcher.NoticeCase, FakeDispatcher.Handler);
        dispatcher.Route(PartyEventOpcode, FakeDispatcher.NoticeCase);
        dispatcher.Route(NoticeOpcode, FakeDispatcher.OtherCase);

        Assert.That(dispatcher.OpcodeCalling(FakeDispatcher.Handler), Is.EqualTo(PartyEventOpcode));
        dispatcher.Route(RosterOpcode, FakeDispatcher.NoticeCase);
        Assert.That(dispatcher.OpcodeCalling(FakeDispatcher.Handler), Is.Zero, "two opcodes");
    }

    // Other players' icons come in the same packet, and it carries much more than icons.
    [Test]
    public void OnlyThePlayersOwnStatusIconIsPutBack()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor), about: OtherPlayer);
        run.ServerSends(ActorControlOpcode, ActorControl(KeepAliveCategory, Mentor));
        run.Lift();

        Assert.That(client.StatusIcon, Is.EqualTo(RecruitingPartyMembers));
        Assert.That(client.PacketsHandled, Is.Zero);
    }

    // Recruitment can resume and end again during a run, in either ActorControl form.
    [Test]
    public void TheStatusIconEndsAsTheServerLastSetIt()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        run.ServerSends(ActorControlTargetOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.Lift();

        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
    }

    // The Party Finder's own listing ends with the server's notice (code 0), replayed as it came.
    [Test]
    public void AListingTheServerEndsWithANoticeEndsInThePartyFinderToo()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { LogMessages[AllPlacesFilled] }));
        Assert.That(run.Tracker.RecruitmentEnded, Is.True, "the window's alert");
        Assert.That(client.PartyFinderListing, Is.EqualTo(Listing), "the client hears nothing until the run ends");

        run.Lift();

        Assert.That(client.PartyFinderListing, Is.Zero);
        Assert.That(client.ChatLog, Is.EqualTo(new[] { LogMessages[AllPlacesFilled] }));
    }

    // Someone takes the last place of a one-place listing: the member event says who and that the
    // listing is full, the member updates around it say nothing the plugin shows.
    [TestCase(true, TestName = "WhileInTheSimThePluginShowsWhoFilledTheParty(member event first)")]
    [TestCase(false, TestName = "WhileInTheSimThePluginShowsWhoFilledTheParty(member event last)")]
    public void WhileInTheSimThePluginShowsWhoFilledTheParty(bool memberEventFirst)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        run.ServerSends(MemberUpdateOpcode, new byte[0xC8]);
        run.ServerSends(MemberUpdateOpcode, new byte[0xC8]);
        if (memberEventFirst) run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        if (!memberEventFirst) run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.Lift();

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", LogMessages[AllPlacesFilled] }));
        Assert.That(run.Tracker.RecruitmentEnded, Is.True, "the window's alert");
        Assert.That(client.UsingPartyFinder, Is.False);
    }

    // The client announces kinds 1, 4 and 5 as joins; 2 is a member coming back.
    [TestCase(1, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    [TestCase(0, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    public void AMemberEventIsShownWhenTheClientWouldAnnounceAJoin(int kind, bool shown)
    {
        using var run = new Run(new FakeClient(), roster: [Me]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, (byte)kind));
        run.ServerSends(MemberEventOpcode, MemberEvent(Me, (byte)kind));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(shown ? new[] { "Alpha Eight (Ravana) joined the party." } : Array.Empty<string>()));
    }

    [Test]
    public void ALeaverIsNamedFromThePartyAsItWasOrAsItGrew()
    {
        using var run = new Run(new FakeClient(), roster: [Me, Two]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Alpha));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Three));
        run.ServerSends(RosterOpcode, Roster(Me, Me));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[]
        {
            "Alpha Eight (Ravana) joined the party.",
            "Two Two (Coeurl) left the party.",
            "Alpha Eight (Ravana) left the party.",
            "A party member left the party.",
        }));
    }

    // The client lists a blacklisted player as "Unknown"; the plugin doesn't name them either.
    [Test]
    public void ABlacklistedPlayerIsNotNamed()
    {
        using var run = new Run(new FakeClient(), roster: [Me]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Blocked, kind: 1));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Blocked));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "A blacklisted player joined the party.", "A blacklisted player left the party." }));
    }

    [Test]
    public void AJoinerIsShownOnceThoughALaterRosterListsThem()
    {
        using var run = new Run(new FakeClient(), roster: [Me]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
        run.ServerSends(RosterOpcode, Roster(Me, Me, Alpha));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party." }));
    }

    [TestCaseSource(nameof(RosterChanges))]
    public void WhileInTheSimThePluginShowsWhatHappenedToTheParty(Member[] before, Member leaderBefore, Member[] after, Member leaderAfter, string[] shown)
    {
        using var run = new Run(new FakeClient(), roster: before, leader: leaderBefore);

        run.ServerSends(RosterOpcode, Roster(leaderAfter, after));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(shown));
        Assert.That(run.Tracker.Events.Select(e => e.At), Has.All.EqualTo(Clock));
    }

    private static IEnumerable<TestCaseData> RosterChanges()
    {
        yield return new TestCaseData(Array.Empty<Member>(), default(Member), new[] { Me, Alpha }, Me, new[] { "Alpha Eight (Ravana) joined the party." })
            .SetArgDisplayNames("the first to join a recruiting leader");
        yield return new TestCaseData(new[] { Me, Two, Three }, Me, new[] { Me, Three }, Me, new[] { "Two Two (Coeurl) left the party." })
            .SetArgDisplayNames("a member leaves");
        yield return new TestCaseData(new[] { Me, Two, Three }, Me, new[] { Me, Two, Three }, Three, new[] { "Three Three is now the party leader." })
            .SetArgDisplayNames("leadership passes to someone from a world with no name");
        yield return new TestCaseData(new[] { Two, Me, Three }, Two, new[] { Me, Three }, Me, new[] { "Two Two (Coeurl) left the party.", "You are now the party leader." })
            .SetArgDisplayNames("the leader leaves and leadership falls to the player");
        yield return new TestCaseData(new[] { Me, Two, Three }, Me, Array.Empty<Member>(), default(Member), new[] { "You are no longer in a party." })
            .SetArgDisplayNames("the party disbands");
        yield return new TestCaseData(new[] { Me, Two }, Me, new[] { Two, Me }, Me, Array.Empty<string>())
            .SetArgDisplayNames("the same roster again, in another order");
    }

    // With no party to compare it to, the first roster can't say who changed.
    [Test]
    public void APartyTheClientCouldNotReadAtArmIsShownFromItsNextChange()
    {
        using var run = new Run(new FakeClient(), rosterRead: false);

        run.ServerSends(RosterOpcode, Roster(Me, Me, Two, Three));
        run.ServerSends(RosterOpcode, Roster(Me, Me, Three, Alpha));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", "Two Two (Coeurl) left the party." }));
    }

    // The client gets the roster both as an update and as the answer to its own request.
    [Test]
    public void EitherRosterPacketCounts()
    {
        using var run = new Run(new FakeClient(), roster: [Me, Two]);

        run.ServerSends(RosterOpcode, Roster(Me, Me, Two, Alpha));
        run.ServerSends(RosterAnswerOpcode, Roster(Me, Me, Alpha));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", "Two Two (Coeurl) left the party." }));
    }

    // Starting a listing brings no Party Finder notice, so the first one may well arrive during a
    // run: its opcode comes from the client's own dispatcher.
    [Test]
    public void TheFirstPartyFinderNoticeEverHeldDuringARunStillReachesTheClient()
    {
        using var dispatcher = new FakeDispatcher();
        dispatcher.Route(PlayerStateFlagsOpcode, FakeDispatcher.OtherCase);
        dispatcher.Route(NoticeOpcode, FakeDispatcher.NoticeCase);
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, noticeSeen: false);
        run.Tracker.NoticeOpcode = dispatcher.OpcodeOf(FakeDispatcher.NoticeCase);

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.Lift();

        Assert.That(client.PartyFinderListing, Is.Zero, "the Party Finder's own listing");
        Assert.That(client.ChatLog, Is.EqualTo(new[] { LogMessages[AllPlacesFilled] }));
    }

    [Test]
    public void TheDispatcherNamesNoOpcodeForACaseSeveralOrNoneReach()
    {
        using var dispatcher = new FakeDispatcher();
        dispatcher.Route(RosterOpcode, FakeDispatcher.OtherCase);
        dispatcher.Route(RosterAnswerOpcode, FakeDispatcher.OtherCase);

        Assert.That(dispatcher.OpcodeOf(FakeDispatcher.OtherCase), Is.Zero, "two opcodes");
        Assert.That(dispatcher.OpcodeOf(FakeDispatcher.NoticeCase), Is.Zero, "none");
    }

    // Code a patch changed could decode to a table anywhere; it's never read outside the module.
    [Test]
    public void ASwitchThatDoesNotDecodeToATableInTheModuleNamesNoOpcode()
    {
        using var dispatcher = new FakeDispatcher();
        dispatcher.Route(NoticeOpcode, FakeDispatcher.NoticeCase);

        Assert.That(dispatcher.OpcodeOf(FakeDispatcher.NoticeCase), Is.EqualTo(NoticeOpcode));
        Assert.That(SwitchCode.JumpTableOpcodeOf(dispatcher.Switch, dispatcher.Case(FakeDispatcher.NoticeCase), dispatcher.Image, 0x1800), Is.Zero, "table past the module's end");
        Assert.That(SwitchCode.JumpTableOpcodeOf(dispatcher.Switch, dispatcher.Case(FakeDispatcher.NoticeCase), dispatcher.Image + 0x10, 0x4000), Is.Zero, "another image base");
    }

    // The party handler's own switch: a range through a byte table of case numbers, and a lone
    // compare. Joins and leaves arrive during the run too.
    [Test]
    public void ThePartyHandlerNamesTheMemberOpcodesBeforeAnyArrive()
    {
        using var handler = new FakePartyHandler(first: 0x0355, count: 0x91);
        handler.Route(MemberEventOpcode, caseNumber: 3, FakePartyHandler.MemberEventCase);
        handler.Route(0x0357, caseNumber: 4, FakePartyHandler.OtherCase);
        handler.Route(0x0358, caseNumber: 4, FakePartyHandler.OtherCase);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ByteTableOpcodeOf(FakePartyHandler.MemberEventCase), Is.EqualTo(MemberEventOpcode));
            Assert.That(handler.ByteTableOpcodeOf(FakePartyHandler.OtherCase), Is.Zero, "two opcodes");
            Assert.That(handler.ByteTableOpcodeOf(FakePartyHandler.MemberLeftCase), Is.Zero, "none");
            Assert.That(SwitchCode.ByteTableOpcodeOf(handler.Switch, handler.Case(FakePartyHandler.MemberEventCase), handler.Image, 0x1050), Is.Zero, "table past the module's end");
            Assert.That(SwitchCode.CompareOpcodeOf(handler.Compare, handler.Case(FakePartyHandler.MemberLeftCase)), Is.EqualTo(MemberLeftOpcode));
            Assert.That(SwitchCode.CompareOpcodeOf(handler.Compare, handler.Case(FakePartyHandler.OtherCase)), Is.Zero, "another case");
        });
    }

    [Test]
    public void APartyThatTurnsCrossWorldDuringARunIsShownAsSuch()
    {
        var client = new FakeClient { [ConditionFlag.UsingPartyFinder] = true, PartyFinderListing = Listing };
        client.SetOnlineStatus(RecruitingPartyMembers, Online);
        using var run = new Run(client);

        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.UsingPartyFinder, ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(RecruitingPartyMembers, CrossWorldPartyLeader, Online));
        run.Lift();

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "The party is now a cross-world party." }));
        Assert.That(run.Tracker.RecruitmentEnded, Is.False);
        Assert.That(client.UsingPartyFinder, Is.True, "still recruiting for the rest of the party");
        Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
        Assert.That(client.HasOnlineStatus(CrossWorldPartyLeader), Is.True);
    }

    // Both packets carry far more than the party; only the party's part is the server's to set at
    // the lift. The rest would need their own packets, which the firewall dropped.
    [Test]
    public void OnlyThePartysStateChangesAtTheLift()
    {
        var client = new FakeClient { [ConditionFlag.UsingPartyFinder] = true, [ConditionFlag.BoundByDuty] = true };
        client.SetOnlineStatus(RecruitingPartyMembers, PartyLeader, Busy, Online);
        var conditionsBefore = Networked.Where(f => !IsParty(f)).ToDictionary(f => f, f => client[f]);
        var statusBefore = client.OnlineStatusFlags & ~PartyOnlineStatus.Mask;
        using var run = new Run(client);

        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.InDutyQueue, ConditionFlag.WaitingForDutyFinder, ConditionFlag.InCombat, ConditionFlag.Casting));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(PartyLeader, AwayFromKeyboard, WaitingForDutyFinder));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(Networked.Where(f => !IsParty(f)).ToDictionary(f => f, f => client[f]), Is.EqualTo(conditionsBefore));
            Assert.That(client.OnlineStatusFlags & ~PartyOnlineStatus.Mask, Is.EqualTo(statusBefore));
            Assert.That(client.UsingPartyFinder, Is.False);
        });
    }

    [Test]
    public void NoticesReachTheClientAsTheServerSentThem()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);
        byte[][] sent = [Notice(RecruitmentCommenced, 1), Notice(NamesSomeone, 5, param: 0x0040_0000_0000_0042), Notice(AllPlacesFilled, PartyNotice.ListingEnded)];

        foreach (var notice in sent) run.ServerSends(NoticeOpcode, notice);
        run.Lift();

        Assert.That(client.NoticesHandled, Is.EqualTo(sent).AsCollection);
        Assert.That(client.NoticeTargets, Has.All.EqualTo(LocalPlayer));
        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { LogMessages[RecruitmentCommenced], LogMessages[AllPlacesFilled] }),
            "a message that names someone is replayed but not shown in the plugin");
    }

    [Test]
    public void PastTheCapTheLatestNoticesAreDroppedAndCounted()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        for (var i = 0; i < 70; i++) run.ServerSends(NoticeOpcode, Notice(RecruitmentCommenced, 1, param: (ulong)i));
        run.Lift();

        Assert.That(client.NoticesHandled.Select(n => BitConverter.ToUInt64(n, 8)), Is.EqualTo(Enumerable.Range(0, 64).Select(i => (ulong)i)));
        Assert.That(run.Tracker.NoticesDropped, Is.EqualTo(6));
    }

    [Test]
    public void ARunTheServerSaidNothingAboutLeavesTheClientAsItWas()
    {
        var client = RecruitingCrossWorldLeader();
        var flags = client.OnlineStatusFlags;
        using var run = new Run(client);

        run.ServerSends(0x0333, new byte[64]);
        run.Lift();

        Assert.That(run.Tracker.Events, Is.Empty);
        Assert.That(run.Tracker.RecruitmentEnded, Is.False);
        Assert.That(client.UsingPartyFinder, Is.True);
        Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
        Assert.That(client.OnlineStatusFlags, Is.EqualTo(flags));
        Assert.That(client.PacketsHandled, Is.Zero);
    }

    // Before its opcode is known, a party packet can't be told from the rest.
    [Test]
    public void UntilTheirOpcodesAreKnownPartyPacketsGoUnnoticed()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false);

        run.ServerSends(PlayerStateFlagsOpcode, StateFlags());
        run.ServerSends(OnlineStatusOpcode, OnlineStatus());
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(RosterOpcode, Roster(Me, Me, Alpha));
        run.Lift();

        Assert.That(run.Tracker.Events, Is.Empty);
        Assert.That(client.UsingPartyFinder, Is.True);
        Assert.That(client.StatusIcon, Is.EqualTo(RecruitingPartyMembers));
        Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
    }

    [Test]
    public void ANewRunStartsWithNoPartyChangesShown()
    {
        using var run = new Run(RecruitingCrossWorldLeader());
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));

        run.Tracker.Begin(PartyRoster.None, default, default, Me.ContentId, LocalPlayer);

        Assert.That(run.Tracker.Events, Is.Empty);
        Assert.That(run.Tracker.RecruitmentEnded, Is.False);
        var fromServer = run.Tracker.TakeFromServer(Clock);
        Assert.That(fromServer.Notices, Is.Empty);
        Assert.That(fromServer.StatusIcon, Is.Null);
    }

    // The opcodes are learned from what reaches the client's handlers: a live packet sits behind its
    // IPC header, while the plugin's own calls hand the handler a buffer on the stack.
    [Test]
    public void AnOpcodeIsLearnedOnlyFromALivePacket()
    {
        using var heap = new Packets();
        var live = heap.Add(NoticeOpcode, new byte[16]) + 16;
        var noHeader = heap.Add(NoticeOpcode, new byte[16], magic: 0) + 16;

        Assert.Multiple(() =>
        {
            Assert.That(PartyStatusTracker.TryReadLiveOpcode(live, 0, 0, out var opcode), Is.True);
            Assert.That(opcode, Is.EqualTo(NoticeOpcode));
            Assert.That(PartyStatusTracker.TryReadLiveOpcode(live, (nuint)live - 64, (nuint)live + 64, out _), Is.False, "a buffer on the stack");
            Assert.That(PartyStatusTracker.TryReadLiveOpcode(noHeader, 0, 0, out _), Is.False, "no IPC header");
        });
    }

    // Reading a header the payload doesn't share a page with could fault, which .NET can't catch.
    [Test]
    public void APayloadAtTheStartOfAPageIsNotReadBehind()
    {
        var page = Environment.SystemPageSize;
        var region = VirtualAlloc(0, (nuint)(2 * page), MemReserve | MemCommit, PageReadWrite);
        Assert.That(region, Is.Not.Zero);
        try
        {
            Assert.That(VirtualProtect(region, (nuint)page, PageNoAccess, out _), Is.True);
            Assert.That(PartyStatusTracker.TryReadLiveOpcode(region + page + 8, 0, 0, out _), Is.False);
        }
        finally
        {
            VirtualFree(region, 0, MemRelease);
        }
    }

    private static FakeClient RecruitingCrossWorldLeader()
    {
        var client = new FakeClient
        {
            [ConditionFlag.UsingPartyFinder] = true,
            [ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance] = true,
            PartyFinderListing = Listing,
            StatusIcon = RecruitingPartyMembers,
        };
        client.SetOnlineStatus(RecruitingPartyMembers, CrossWorldPartyLeader, Online);
        return client;
    }

    private static bool IsParty(ConditionFlag flag)
        => flag is ConditionFlag.UsingPartyFinder or ConditionFlag.RecruitingWorldOnly or ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance;

    private static byte[] StateFlags(params ConditionFlag[] on)
    {
        var bits = new byte[16];
        foreach (var flag in on)
        {
            var bit = Array.IndexOf(Networked, flag);
            bits[bit >> 3] |= (byte)(1 << (bit & 7));
        }
        return bits;
    }

    private static byte[] OnlineStatus(params int[] rows) => BitConverter.GetBytes(rows.Aggregate(0UL, (flags, row) => flags | 1UL << row));

    private static byte[] ActorControl(ushort category, uint p1)
    {
        var payload = new byte[0x18];
        BitConverter.GetBytes(category).CopyTo(payload, 0);
        BitConverter.GetBytes(p1).CopyTo(payload, 4);
        return payload;
    }

    private static byte[] MemberEvent(Member member, byte kind, bool listingFull = false)
    {
        var payload = new byte[PartyMemberEvent.Length];
        payload[0] = listingFull ? (byte)1 : (byte)0;
        payload[1] = kind;
        BitConverter.GetBytes(member.AccountId).CopyTo(payload, 0x08);
        BitConverter.GetBytes(member.ContentId).CopyTo(payload, 0x10);
        BitConverter.GetBytes(member.HomeWorld).CopyTo(payload, 0x1C);
        Encoding.UTF8.GetBytes(member.Name).CopyTo(payload, 0x22);
        return payload;
    }

    private static byte[] MemberLeft(Member member) => BitConverter.GetBytes(member.ContentId);

    private static byte[] CrossWorldReadyCheckStart(Member starter) => BitConverter.GetBytes(starter.ContentId);

    private static byte[] CrossWorldReadyCheckAnswer(Member member, byte answer)
    {
        var payload = new byte[CrossWorldReadyCheck.AnswerLength];
        BitConverter.GetBytes(member.ContentId).CopyTo(payload, 0);
        payload[8] = answer;
        return payload;
    }

    private static byte[] PartyEventPacket(Member actor, byte kind)
    {
        var payload = new byte[PartyEvent.Length];
        BitConverter.GetBytes(actor.ContentId).CopyTo(payload, 0x10);
        payload[PartyEvent.KindOffset] = kind;
        Encoding.UTF8.GetBytes(actor.Name).CopyTo(payload, 0x25);
        return payload;
    }

    private static byte[] Roster(Member leader, params Member[] members)
    {
        var payload = new byte[PartyRoster.Length];
        BitConverter.GetBytes(leader.ContentId).CopyTo(payload, 0);
        for (var i = 0; i < members.Length; i++)
        {
            var entry = 8 + i * 0x58;
            BitConverter.GetBytes(members[i].ContentId).CopyTo(payload, entry + 0x10);
            BitConverter.GetBytes(members[i].HomeWorld).CopyTo(payload, entry + 0x2A);
            Encoding.UTF8.GetBytes(members[i].Name).CopyTo(payload, entry + 0x33);
        }
        return payload;
    }

    private static byte[] Notice(uint logMessage, byte code, ulong param = 0)
    {
        var payload = new byte[PartyNotice.Length];
        BitConverter.GetBytes(logMessage).CopyTo(payload, 0);
        payload[6] = code;
        BitConverter.GetBytes(param).CopyTo(payload, 8);
        return payload;
    }

    // ZoneControl's part: the client's party state at arm, the packets the firewall keeps from it,
    // and what ServerPartyWatch puts on it at the lift.
    private sealed class Run : IDisposable
    {
        private readonly FakeClient client;
        private readonly Packets packets = new();

        public PartyStatusTracker Tracker { get; } = new(Networked, id => LogMessages.GetValueOrDefault(id), id => Worlds.GetValueOrDefault(id),
            (accountId, _) => BlacklistedAccounts.Contains(accountId));

        // The roster the client shows at arm; its first member leads unless `leader` says otherwise.
        public Run(FakeClient client, bool learned = true, Member[]? roster = null, Member? leader = null, bool rosterRead = true, bool noticeSeen = true)
        {
            this.client = client;
            if (learned)
            {
                Tracker.PlayerStateFlagsOpcode = PlayerStateFlagsOpcode;
                Tracker.OnlineStatusOpcode = OnlineStatusOpcode;
                if (noticeSeen) Tracker.NoticeOpcode = NoticeOpcode;
                Tracker.RosterOpcodes = [RosterOpcode, RosterAnswerOpcode];
                Tracker.ActorControlOpcodes = [ActorControlOpcode, ActorControlTargetOpcode];
                Tracker.MemberEventOpcode = MemberEventOpcode;
                Tracker.MemberLeftOpcode = MemberLeftOpcode;
                Tracker.PartyEventOpcode = PartyEventOpcode;
                Tracker.CrossWorldReadyCheckOpcode = CrossWorldReadyCheckOpcode;
                Tracker.CrossWorldReadyCheckAnswerOpcode = CrossWorldReadyCheckAnswerOpcode;
            }
            var members = (roster ?? []).Select(m => new PartyMember(m.ContentId, m.Name, m.HomeWorld, m.AccountId)).ToArray();
            var leaderId = leader?.ContentId ?? members.FirstOrDefault().ContentId;
            Tracker.Begin(rosterRead ? new PartyRoster(members, leaderId) : null, PartyConditions.Read(flag => client[flag]), PartyOnlineStatus.FromFlags(client.OnlineStatusFlags), Me.ContentId, LocalPlayer);
        }

        // `about`: the actor the packet is addressed to.
        public void ServerSends(ushort opcode, byte[] payload, uint about = LocalPlayer, DateTime? at = null)
            => Tracker.Observe(packets.Add(opcode, payload), about, at ?? Clock);

        public void Lift(DateTime? at = null)
        {
            var now = at ?? Clock;
            var (conditions, status, statusIcon, notices, readyCheck, _) = Tracker.TakeFromServer(now);
            if (conditions is { } serverConditions)
            {
                var bits = new byte[16];
                serverConditions.WriteStateFlags(bits, Networked, flag => client[flag]);
                client.HandleStateFlags(bits);
            }
            if (status is { } serverStatus) client.HandleOnlineStatus(serverStatus.MergeInto(client.OnlineStatusFlags));
            if (statusIcon is { } icon) client.HandleActorControl(icon.EntityId, icon.Category, icon.P1);
            foreach (var notice in notices) client.HandleNotice(notice.TargetId, notice.Payload);
            if (readyCheck is not { } check) return;
            foreach (var packet in check.Packets) client.HandleReadyCheckPacket(packet.Opcode, packet.Payload);
            if (client.ReadyCheckTimer == HeldReadyCheck.Window.TotalSeconds) client.ReadyCheckTimer = check.LeftAt(now).TotalSeconds;
        }

        public void Dispose() => packets.Dispose();
    }

    // An image with the dispatcher's switch encoded as the client has it (SwitchCode), its table of
    // case offsets, and the cases.
    private sealed class FakeDispatcher : IDisposable
    {
        public const uint NoticeCase = 0x2000;
        public const uint OtherCase = 0x2010;
        public const uint Handler = 0x3800;
        private const uint DefaultCase = 0x3000;
        private const int Bias = 0x65;
        private const uint Last = 0x382;
        private const uint SwitchAt = 0x100;
        private const uint TableAt = 0x1000;
        private const int Size = 0x4000;
        private readonly nint image;

        public FakeDispatcher()
        {
            image = VirtualAlloc(0, Size, MemReserve | MemCommit, PageReadWrite);
            Assert.That(image, Is.Not.Zero);
            var code = new List<byte> { 0x41, 0x83, 0xC5, unchecked((byte)-Bias), 0x49, 0x8B, 0xFC, 0x41, 0x81, 0xFD };
            code.AddRange(BitConverter.GetBytes(Last));
            code.AddRange([0x0F, 0x87]);
            code.AddRange(BitConverter.GetBytes((int)(DefaultCase - (SwitchAt + 20))));
            code.AddRange([0x48, 0x8D, 0x15]);
            code.AddRange(BitConverter.GetBytes(-(int)(SwitchAt + 27)));
            code.AddRange([0x49, 0x63, 0xC5, 0x8B, 0x8C, 0x82]);
            code.AddRange(BitConverter.GetBytes(TableAt));
            code.AddRange([0x48, 0x03, 0xCA, 0xFF, 0xE1]);
            Marshal.Copy(code.ToArray(), 0, Switch, code.Count);
            for (var index = 0; index <= Last; index++) Marshal.WriteInt32(image + (nint)TableAt + 4 * index, (int)DefaultCase);
        }

        public nint Image => image;

        public nint Switch => image + (nint)SwitchAt;

        public nint Case(uint offset) => image + (nint)offset;

        public ushort OpcodeOf(uint caseOffset) => SwitchCode.JumpTableOpcodeOf(Switch, Case(caseOffset), image, Size);

        public ushort OpcodeCalling(uint functionOffset) => SwitchCode.JumpTableOpcodeCalling(Switch, Case(functionOffset), image, Size);

        // As the client's party event case: lea rdx, [rdi+10h]; mov ecx, r14d; call handler.
        public void WriteCaseCalling(uint caseOffset, uint functionOffset)
        {
            byte[] code = [0x48, 0x8D, 0x57, 0x10, 0x41, 0x8B, 0xCE, 0xE8, .. BitConverter.GetBytes((int)(functionOffset - (caseOffset + 12)))];
            Marshal.Copy(code, 0, Case(caseOffset), code.Length);
        }

        public void Route(ushort opcode, uint caseOffset) => Marshal.WriteInt32(image + (nint)TableAt + 4 * (opcode - Bias), (int)caseOffset);

        public void Dispose() => VirtualFree(image, 0, MemRelease);
    }

    // An image with the party handler's two switch forms encoded as the client has them
    // (SwitchCode): a byte-table range with its case numbers and offsets, and a lone compare that
    // sends MemberLeftOpcode to its case.
    private sealed class FakePartyHandler : IDisposable
    {
        public const uint MemberEventCase = 0x2000;
        public const uint MemberLeftCase = 0x2010;
        public const uint OtherCase = 0x2020;
        public const uint ReadyCheckCase = 0x2030;
        public const uint ReadyCheckStart = 0x3800;
        private const uint DefaultCase = 0x3000;
        private const uint SwitchAt = 0x100;
        private const uint CompareAt = 0x200;
        private const uint NumbersAt = 0x1000;
        private const uint OffsetsAt = 0x1400;
        private const int Size = 0x4000;
        private readonly nint image;
        private readonly int first;

        public FakePartyHandler(int first, uint count)
        {
            this.first = first;
            image = VirtualAlloc(0, Size, MemReserve | MemCommit, PageReadWrite);
            Assert.That(image, Is.Not.Zero);
            var code = new List<byte> { 0x2D };
            code.AddRange(BitConverter.GetBytes(first));
            code.Add(0x3D);
            code.AddRange(BitConverter.GetBytes(count));
            code.AddRange([0x0F, 0x87]);
            code.AddRange(BitConverter.GetBytes((int)(DefaultCase - (SwitchAt + 16))));
            code.AddRange([0x48, 0x8D, 0x15]);
            code.AddRange(BitConverter.GetBytes(-(int)(SwitchAt + 23)));
            code.AddRange([0x48, 0x98, 0x0F, 0xB6, 0x84, 0x02]);
            code.AddRange(BitConverter.GetBytes(NumbersAt));
            code.AddRange([0x8B, 0x8C, 0x82]);
            code.AddRange(BitConverter.GetBytes(OffsetsAt));
            code.AddRange([0x48, 0x03, 0xCA, 0xFF, 0xE1]);
            Marshal.Copy(code.ToArray(), 0, Switch, code.Count);
            // Every opcode in the range to case 0, the default.
            Marshal.WriteInt32(image + (nint)OffsetsAt, (int)DefaultCase);

            var compare = new List<byte> { 0x3D };
            compare.AddRange(BitConverter.GetBytes((uint)MemberLeftOpcode));
            compare.AddRange([0x0F, 0x87]);
            compare.AddRange(BitConverter.GetBytes((int)(DefaultCase - (CompareAt + 11))));
            compare.AddRange([0x0F, 0x84]);
            compare.AddRange(BitConverter.GetBytes((int)(MemberLeftCase - (CompareAt + 17))));
            Marshal.Copy(compare.ToArray(), 0, Compare, compare.Count);
        }

        public nint Image => image;

        public nint Switch => image + (nint)SwitchAt;

        public nint Compare => image + (nint)CompareAt;

        public nint Case(uint offset) => image + (nint)offset;

        public ushort ByteTableOpcodeOf(uint caseOffset) => SwitchCode.ByteTableOpcodeOf(Switch, Case(caseOffset), image, Size);

        public ushort ByteTableOpcodeCalling(uint functionOffset) => SwitchCode.ByteTableOpcodeCalling(Switch, Case(functionOffset), image, Size);

        // As the party handler's cross-world ready check case: lea rdx, [r14+10h]; mov rcx, r12; call start.
        public void WriteCaseCalling(uint caseOffset, uint functionOffset)
        {
            byte[] code = [0x49, 0x8D, 0x56, 0x10, 0x49, 0x8B, 0xCC, 0xE8, .. BitConverter.GetBytes((int)(functionOffset - (caseOffset + 12)))];
            Marshal.Copy(code, 0, Case(caseOffset), code.Length);
        }

        public void Route(ushort opcode, byte caseNumber, uint caseOffset)
        {
            Marshal.WriteByte(image + (nint)NumbersAt + (opcode - first), caseNumber);
            Marshal.WriteInt32(image + (nint)OffsetsAt + 4 * caseNumber, (int)caseOffset);
        }

        public void Dispose() => VirtualFree(image, 0, MemRelease);
    }

    // Each packet a little way into its own pages, so its header shares a page with its payload.
    private sealed class Packets : IDisposable
    {
        private const int HeaderAt = 0x40;
        private readonly List<nint> regions = [];

        public nint Add(ushort opcode, byte[] payload, ushort magic = 0x14)
        {
            var region = VirtualAlloc(0, (nuint)(HeaderAt + 16 + payload.Length), MemReserve | MemCommit, PageReadWrite);
            Assert.That(region, Is.Not.Zero);
            regions.Add(region);
            var at = region + HeaderAt;
            Marshal.WriteInt16(at, 0, (short)magic);
            Marshal.WriteInt16(at, 2, (short)opcode);
            Marshal.Copy(payload, 0, at + 16, payload.Length);
            return at;
        }

        public void Dispose()
        {
            foreach (var region in regions) VirtualFree(region, 0, MemRelease);
        }
    }

    private sealed class FakeClient
    {
        private readonly HashSet<ConditionFlag> on = [];

        public ulong OnlineStatusFlags { get; private set; }
        // The OnlineStatus row the player's character shows.
        public uint StatusIcon { get; set; }
        // The game's ready check window: who started it, its seconds left, and the answers it shows.
        public string? ReadyCheckFrom { get; private set; }
        public double ReadyCheckTimer { get; set; }
        public int ReadyCheckAnswers { get; private set; }
        public ulong PartyFinderListing { get; set; }
        public List<string> ChatLog { get; } = [];
        public List<byte[]> NoticesHandled { get; } = [];
        public List<uint> NoticeTargets { get; } = [];
        public int PacketsHandled { get; private set; }

        public bool this[ConditionFlag flag]
        {
            get => on.Contains(flag);
            set
            {
                if (value) on.Add(flag);
                else on.Remove(flag);
            }
        }

        // Anything the client reads as "recruiting": the Duty Finder checks the status icon and the
        // online status bit, party commands the bit.
        public bool UsingPartyFinder => this[ConditionFlag.UsingPartyFinder] || this[ConditionFlag.RecruitingWorldOnly]
                                        || HasOnlineStatus(RecruitingPartyMembers) || StatusIcon == RecruitingPartyMembers;

        public bool HasOnlineStatus(int row) => (OnlineStatusFlags & 1UL << row) != 0;

        public void SetOnlineStatus(params int[] rows) => OnlineStatusFlags = rows.Aggregate(0UL, (flags, row) => flags | 1UL << row);

        public void HandleStateFlags(byte[] bits)
        {
            PacketsHandled++;
            for (var bit = 0; bit < Networked.Length && bit < bits.Length * 8; bit++)
                this[Networked[bit]] = (bits[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        public void HandleOnlineStatus(ulong flags)
        {
            PacketsHandled++;
            OnlineStatusFlags = flags;
        }

        // The party event names who started it; the cross-world start only has their content id,
        // which the client names from its roster.
        public void HandleReadyCheckPacket(ushort opcode, byte[] payload)
        {
            PacketsHandled++;
            if (opcode == CrossWorldReadyCheckOpcode)
                OpenReadyCheck(Members.First(m => m.ContentId == BitConverter.ToUInt64(payload, 0)).Name);
            else if (opcode == CrossWorldReadyCheckAnswerOpcode) ReadyCheckAnswers++;
            else if (payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckStarted)
                OpenReadyCheck(Encoding.UTF8.GetString(payload, 0x25, Array.IndexOf(payload, (byte)0, 0x25) - 0x25));
            else if (payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckAnswered) ReadyCheckAnswers++;
        }

        private void OpenReadyCheck(string from)
        {
            ReadyCheckFrom = from;
            ReadyCheckTimer = HeldReadyCheck.Window.TotalSeconds;
            ReadyCheckAnswers = 0;
        }

        public void HandleActorControl(uint entityId, ushort category, uint p1)
        {
            PacketsHandled++;
            if (entityId == LocalPlayer && category == PartyStatusTracker.StatusIconCategory) StatusIcon = p1;
        }

        public void HandleNotice(uint targetId, byte[] payload)
        {
            PacketsHandled++;
            NoticesHandled.Add(payload);
            NoticeTargets.Add(targetId);
            if (LogMessages.GetValueOrDefault(BitConverter.ToUInt32(payload, 0)) is { } text) ChatLog.Add(text);
            if (payload[6] == PartyNotice.ListingEnded) PartyFinderListing = 0;
        }
    }

    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private const uint PageNoAccess = 0x01;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(nint address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);
}
