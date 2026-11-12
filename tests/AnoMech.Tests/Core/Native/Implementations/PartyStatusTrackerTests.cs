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
    private const ushort ActorControlOpcode = 0x0311;
    private const ushort ActorControlTargetOpcode = 0x0313;
    private const ushort MemberEventOpcode = 0x0360;
    private const ushort MemberLeftOpcode = 0x01C9;
    private const ushort MemberRemovedOpcode = 0x01CA;
    private const ushort DisbandedOpcode = 0x01CB;
    private const ushort MemberStatusOpcode = 0x01F7;
    private const ushort PartyListOpcode = 0x01CD;
    private const ushort PartyKeyOpcode = 0x01CE;
    // A party packet the lift doesn't replay.
    private const ushort UnreplayedPartyOpcode = 0x01CF;
    private const ulong LocalAccountId = 0x1111_0001;
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

    // The lowest set bit of the player's listing's SelectedCategory (ClientStructs' DutyCategory).
    private const int HighEndDuty = 6;
    private const int GoldSaucer = 8;
    private const int DeepDungeons = 13;

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
    private static readonly Member Two = new(0x0040_0000_0000_0002, "Two Two", 74, AccountId: 0x2222_2222);
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

    // After a patch the receiver may be one the plugin's table doesn't know. Starting the listing,
    // before the run, the client's handlers still see the conditions, the online status and the icon
    // go by.
    [Test]
    public void AListingThatFillsDuringARunIsOverWhenOnlyLivePacketsTaughtTheOpcodes()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false);
        using var live = new Packets();
        var sources = new PartyOpcodeSources(run.Tracker);
        var startedStatus = OnlineStatus(RecruitingPartyMembers, CrossWorldPartyLeader, Online);
        var conditions = live.Add(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.UsingPartyFinder, ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        var status = live.Add(OnlineStatusOpcode, startedStatus);
        var icon = live.Add(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers));
        sources.Observed(PartyHandlerCalls.Payload(conditions, conditions + 16, PartyPacket.PlayerStateFlags));
        sources.Observed(PartyHandlerCalls.OnlineStatus(status, BitConverter.ToUInt64(startedStatus)));
        sources.Observed(PartyHandlerCalls.ActorControl(icon, LocalPlayer, LocalPlayer, PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers, isRecorded: false));

        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.Lift();

        Assert.That(client.UsingPartyFinder, Is.False);
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
    }

    // The handlers run for the plugin's own calls too, for other packets with other values, and, for
    // ActorControl, for the ones the client's duty recorder replays while it handles another packet.
    // None of those teaches the run anything.
    [TestCase("the plugin's own online status")]
    [TestCase("online status flags of their own")]
    [TestCase("the plugin's own ActorControl")]
    [TestCase("another actor's ActorControl")]
    [TestCase("an ActorControl of another category")]
    [TestCase("an ActorControl of another value")]
    [TestCase("an ActorControl the duty recorder replays")]
    public void AHandlerCallForAnythingButTheDispatchedPacketTeachesNothing(string call)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false);
        using var live = new Packets();
        var sources = new PartyOpcodeSources(run.Tracker);
        var flags = OnlineStatus(CrossWorldPartyLeader, Mentor, Online);
        var icon = ActorControl(PartyStatusTracker.StatusIconCategory, Mentor);
        var statusPacket = live.Add(OnlineStatusOpcode, flags);
        var iconPacket = live.Add(ActorControlOpcode, icon);
        const ushort category = PartyStatusTracker.StatusIconCategory;

        sources.Observed(call switch
        {
            "the plugin's own online status" => PartyHandlerCalls.OnlineStatus(0, BitConverter.ToUInt64(flags)),
            "online status flags of their own" => PartyHandlerCalls.OnlineStatus(statusPacket, 1UL << Busy),
            "the plugin's own ActorControl" => PartyHandlerCalls.ActorControl(0, LocalPlayer, LocalPlayer, category, Mentor, false),
            "another actor's ActorControl" => PartyHandlerCalls.ActorControl(iconPacket, LocalPlayer, OtherPlayer, category, Mentor, false),
            "an ActorControl of another category" => PartyHandlerCalls.ActorControl(iconPacket, LocalPlayer, LocalPlayer, KeepAliveCategory, Mentor, false),
            "an ActorControl of another value" => PartyHandlerCalls.ActorControl(iconPacket, LocalPlayer, LocalPlayer, category, Busy, false),
            _ => PartyHandlerCalls.ActorControl(iconPacket, LocalPlayer, LocalPlayer, category, Mentor, true),
        });
        run.ServerSends(OnlineStatusOpcode, flags);
        run.ServerSends(ActorControlOpcode, icon);
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.HasOnlineStatus(RecruitingPartyMembers), Is.True, "the online status");
            Assert.That(client.StatusIcon, Is.EqualTo(RecruitingPartyMembers), "the status icon");
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

    // Someone joins the cross-world party during the run and starts a ready check. No roster comes with
    // a join: the client asks for one when it hears of the join, and until it has it, it doesn't count
    // the joiner in its party, and answers a ready check from someone else "not ready" for the player
    // itself. So the lift hands the client the join, and the check once the roster is in.
    [Test]
    public void AReadyCheckFromSomeoneWhoJoinedDuringTheRunOpensOnceTheClientKnowsThem()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));

        Assert.Multiple(() =>
        {
            Assert.That(client.ChatLog, Does.Contain("Alpha Eight joins the party."), "the join, as the client prints it");
            Assert.That(client.ReadyCheckPrompt, Is.False, "not before the client knows Alpha");
            Assert.That(client.AnswersSent, Is.Empty);
        });

        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.Frame(Clock.AddSeconds(58.2));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Alpha.Name));
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(11.8).Within(0.001));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(1), "Alpha's own ready");
            Assert.That(client.AnswersSent, Is.Empty, "nothing answered for the player");
            Assert.That(client.ChatLog, Has.None.StartsWith("Ready check complete."));
        });
    }

    // The first check runs out during the run; the second, 18 s before the lift at 58 s, is held.
    private static void JoinAndStartTwoReadyChecks(Run run, Member joiner)
    {
        run.ServerSends(MemberEventOpcode, MemberEvent(joiner, kind: 1, listingFull: true), at: Clock);
        foreach (var start in new[] { 5, 40 })
        {
            run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(joiner), at: Clock.AddSeconds(start));
            run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(joiner, 1), at: Clock.AddSeconds(start));
        }
    }

    // A party of one world has no cross-world roster; its ready check opens at the lift as before.
    [Test]
    public void AReadyCheckInAPartyOfOneWorldOpensAtTheLift()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(12));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
        Assert.That(client.ReadyCheckTimer, Is.EqualTo(18).Within(0.001));
    }

    // Someone joins a world-only listing during the run and starts a ready check, but no party list
    // naming them was held. The client counts itself alone and would answer it for the player: it ends
    // unanswered.
    [Test]
    public void AReadyCheckFromSomeoneWhoJoinedAPartyOfOneWorldDuringTheRunIsNeverAnsweredForThePlayer()
    {
        var client = new FakeClient { MainGroupCount = 0 };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(12));
        for (var at = 12.5; at <= 30; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.Received, Has.No.Member(PartyEventOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
    }

    // A client that never gets the roster never counts the joiner in its party. The check waits for it
    // until its end, then reaches the client all the same: the client answers "not ready" for the player,
    // as when a prompt runs out, and never earlier.
    [Test]
    public void AHeldReadyCheckTheClientCantTakeIsAnsweredByTheClientAtItsEnd()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        for (var at = 58.5; at < 70; at += 0.5) run.Frame(Clock.AddSeconds(at));
        Assert.That(client.AnswersSent, Is.Empty, "not while the player could still be asked");
        run.Frame(Clock.AddSeconds(70));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.False);
            Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredNotReady }));
            Assert.That(run.ReadyCheckEnds, Has.One.Matches<ReadyCheckStep>(end => end.Outcome == ReadyCheckOutcome.Opened && !end.Warn),
                "the client's own 0 on its timer");
        });
    }

    // A check the client then closes on the answers it already holds leaves 10 on its timer: no warning.
    [Test]
    public void AtItsEndACheckTheClientClosesOnItsAnswersDoesntWarn()
    {
        var client = RecruitingCrossWorldLeader();
        client.AnsweredEarlier(Me, status: 2);
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        for (var at = 58.5; at <= 70; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.ChatLog, Has.Some.StartsWith("Ready check complete."), "closed on the answers");
        Assert.That(run.ReadyCheckEnds, Has.One.Matches<ReadyCheckStep>(end => end.Outcome == ReadyCheckOutcome.Opened && !end.Warn));
    }

    // A check with under 2 s left still reaches the client, which asks the player.
    [Test]
    public void AReadyCheckWithUnderTwoSecondsLeftStillAsksThePlayer()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(28.5));
        run.Frame(Clock.AddSeconds(29));
        client.PlayerClicksReady();

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredReady }), "the player's own answer, in time");
        });
    }

    // However late the client can first take a check, it gets it with what's left, and at least the
    // moment its countdown needs: it answers for the player once that runs out, and only once.
    [TestCase(69.9, TestName = "AReadyCheckTheClientCanTakeOnlyAtItsEndIsAnsweredOnce(0.1 s before)")]
    [TestCase(70.0, TestName = "AReadyCheckTheClientCanTakeOnlyAtItsEndIsAnsweredOnce(at its end)")]
    public void AReadyCheckTheClientCanTakeOnlyAtItsEndIsAnsweredOnce(double canAskAt)
    {
        var client = new FakeClient { CanAsk = false };
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock.AddSeconds(40));
        run.Lift(Clock.AddSeconds(58));
        for (var at = 58.5; at < canAskAt; at += 0.5) run.Frame(Clock.AddSeconds(at));
        client.CanAsk = true;
        foreach (var at in new[] { canAskAt, 70.5, 71, 80 }) run.Frame(Clock.AddSeconds(at));

        Assert.Multiple(() =>
        {
            Assert.That(client.Received.Count(opcode => opcode == CrossWorldReadyCheckOpcode), Is.EqualTo(1));
            Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredNotReady }));
        });
    }

    // The party ends after the check started. Out of that kind of party the client ends a running check
    // without an answer, so the held one is never answered either.
    [TestCase(true, TestName = "AReadyCheckFromAPartyThatEndedDuringTheRunIsNeverAnswered(cross-world)")]
    [TestCase(false, TestName = "AReadyCheckFromAPartyThatEndedDuringTheRunIsNeverAnswered(one world)")]
    public void AReadyCheckFromAPartyThatEndedDuringTheRunIsNeverAnswered(bool crossWorld)
    {
        var client = new FakeClient { MainGroupCount = crossWorld ? 0 : 2 };
        using var run = new Run(client, roster: crossWorld ? new[] { Me, Two } : null);

        if (crossWorld)
        {
            run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
            run.ServerSends(DisbandedOpcode, new byte[8], at: Clock.AddSeconds(5));
        }
        else
        {
            run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
            run.ServerSends(PartyListOpcode, PartyList(0, Me), at: Clock.AddSeconds(5));
        }
        run.Lift(Clock.AddSeconds(12));
        for (var at = 12.5; at <= 40; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.False);
            Assert.That(client.AnswersSent, Is.Empty);
            Assert.That(run.SocialRefreshed, Is.False, "the party's end reached the client");
        });
    }

    // Someone joins a party of one world during the run and starts a ready check. The party list the
    // server sent with the join reaches the client first, so it counts itself in a party and asks the
    // player, as it would have then.
    [Test]
    public void AReadyCheckFromSomeoneWhoJoinedAPartyOfOneWorldDuringTheRunAsksThePlayer()
    {
        var client = new FakeClient { MainGroupCount = 1 };
        using var run = new Run(client);

        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two), at: Clock);
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock.AddSeconds(2));
        run.Lift(Clock.AddSeconds(12));

        Assert.Multiple(() =>
        {
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Two.ContentId }));
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(20).Within(0.001));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // A member starts a ready check, then leaves, and someone else takes their place. The client would
    // have ended the check, unanswered, the moment it was out of the party: it's never shown.
    [Test]
    public void AReadyCheckFromAPartyTheClientLeftDuringTheRunIsNeverShownThoughItRefilled()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me), at: Clock.AddSeconds(4));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Three), at: Clock.AddSeconds(7));
        run.Lift(Clock.AddSeconds(10));
        for (var at = 10.5; at <= 40; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.Multiple(() =>
        {
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Three.ContentId }));
            Assert.That(client.Received, Has.No.Member(PartyEventOpcode));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // The party of one world grows after the check started: the client stays in it, and asks the player.
    [Test]
    public void AReadyCheckOutlivesAPartyOfOneWorldGrowing()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two, Three), at: Clock.AddSeconds(3));
        run.Lift(Clock.AddSeconds(10));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
    }

    // A newer check replaces an older one: only what came after the newer one's start can end it.
    [Test]
    public void ANewerReadyCheckIsEndedOnlyByWhatCameAfterItsStart()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me), at: Clock.AddSeconds(2));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Three), at: Clock.AddSeconds(4));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), at: Clock.AddSeconds(6));
        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(7)), Is.EqualTo(new ReadyCheckAlert(Three.Name, TimeSpan.FromSeconds(29))), "shown again");
        run.Lift(Clock.AddSeconds(10));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name));
    }

    // A party list the client ran sets its party's member count as sent, whatever the keys: a lookup a
    // patch broke after it leaves the check to the client's own test.
    [Test]
    public void AReadyCheckOutlivesAPartyListWhoseDecodingCantBeChecked()
    {
        var client = new FakeClient { MainGroupCount = 3, MainGroupHasThrows = true };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two, Three), at: Clock.AddSeconds(5));
        run.Lift(Clock.AddSeconds(8));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
    }

    // While in the sim, the plugin stops showing a held check once a step after its start takes the
    // client out of that kind of party, as far as the server's packets tell: the lift ends it there.
    [TestCase("a party of one world down to the player")]
    [TestCase("the player removed from a cross-world party")]
    [TestCase("the player leaving a cross-world party")]
    [TestCase("a cross-world party disbanded")]
    public void AReadyCheckThePartyEndedForIsNoLongerShownDuringTheRun(string ended)
    {
        var oneWorld = ended == "a party of one world down to the player";
        using var run = new Run(new FakeClient { MainGroupCount = oneWorld ? 2 : 0 }, roster: oneWorld ? null : new[] { Me, Two });
        if (oneWorld) run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        else run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(1)), Is.Not.Null);

        var (opcode, payload) = ended switch
        {
            "a party of one world down to the player" => (PartyListOpcode, PartyList(0, Me, Me)),
            "the player removed from a cross-world party" => (MemberRemovedOpcode, MemberRemoved(Me)),
            "the player leaving a cross-world party" => (MemberLeftOpcode, MemberLeft(Me)),
            _ => (DisbandedOpcode, new byte[8]),
        };
        run.ServerSends(opcode, payload, at: Clock.AddSeconds(2));
        if (oneWorld) run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Three), at: Clock.AddSeconds(3));

        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(4)), Is.Null);
    }

    // Someone else leaving or removed, or a party of one world's list, leaves a cross-world check to answer.
    [Test]
    public void ACrossWorldReadyCheckStaysShownWhenSomeoneElseLeaves()
    {
        using var run = new Run(new FakeClient(), roster: [Me, Two, Three, Alpha]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(MemberLeftOpcode, MemberLeft(Three), at: Clock.AddSeconds(2));
        run.ServerSends(MemberRemovedOpcode, MemberRemoved(Alpha), at: Clock.AddSeconds(2));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me), at: Clock.AddSeconds(3));

        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(4)), Is.Not.Null);
    }

    // A party list after the check's start that the client couldn't take leaves its party unknown: the
    // check goes unanswered rather than be shown on a party the client may have left.
    [Test]
    public void AReadyCheckFollowedByAPartyListTheClientCouldntTakeIsNeverShown()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);
        run.Tracker.ConfirmedPartyKeyOpcode = 0;

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyKeyOpcode, PartyKey(0x0202), at: Clock.AddSeconds(1));
        run.ServerSends(PartyListOpcode, PartyList(0x0202, Me, Me), at: Clock.AddSeconds(2));
        run.ServerSends(PartyListOpcode, PartyList(0x0202, Me, Me, Three), at: Clock.AddSeconds(3));
        run.Lift(Clock.AddSeconds(10));
        for (var at = 10.5; at <= 40; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.Received, Has.No.Member(PartyEventOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
    }

    // The starter leaves the cross-world party after starting the check. The client stays in that party,
    // so the check runs on to its end, where the client answers it as it would have then.
    [Test]
    public void AReadyCheckWhoseStarterLeftACrossWorldPartyRunsToItsEnd()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two), at: Clock.AddSeconds(3));
        run.Lift(Clock.AddSeconds(10));
        for (var at = 10.5; at < 30; at += 0.5) run.Frame(Clock.AddSeconds(at));
        Assert.That(client.AnswersSent, Is.Empty, "not while the player could still be asked");
        run.Frame(Clock.AddSeconds(30));

        Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredNotReady }));
    }

    // A party of one world's check the client can't put to the player before its end reaches it all the
    // same, which answers "not ready" for the player as it would have then.
    [Test]
    public void APartyOfOneWorldsCheckTheClientCantTakeIsAnsweredAtItsEnd()
    {
        var client = new FakeClient { MainGroupCount = 2, CanAsk = false };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(25));
        for (var at = 25.5; at < 30; at += 0.5) run.Frame(Clock.AddSeconds(at));
        Assert.That(client.AnswersSent, Is.Empty, "not while the player could still be asked");
        run.Frame(Clock.AddSeconds(30));

        Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredNotReadyInOneWorld }));
    }

    // The party empties during the run and refills before a ready check: the client asks the player.
    [Test]
    public void AReadyCheckAfterAPartyOfOneWorldEmptiedAndRefilledAsksThePlayer()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        run.ServerSends(PartyListOpcode, PartyList(0, Me), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Three), at: Clock.AddSeconds(3));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), at: Clock.AddSeconds(5));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Three.ContentId }));
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name));
            Assert.That(run.SocialRefreshed, Is.False);
        });
    }

    // Where the client's party can't be read during the lift, a held check is dropped, never shown.
    [Test]
    public void AReadyCheckWhoseKindOfPartyCantBeReadDuringTheLiftIsDropped()
    {
        var client = new FakeClient { MainGroupCount = 2, StillInPartyThrows = true };
        using var run = new Run(client);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two), at: Clock.AddSeconds(1));
        run.Lift(Clock.AddSeconds(10));

        Assert.That(client.Received, Has.No.Member(PartyEventOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
        Assert.That(run.LiftWarnings, Has.Exactly(1).Matches<string>(warning => warning.Contains("threw")), "the log warns");
    }

    // At its end a check the client puts to the player keeps the client's own 30 s unless its timer is
    // cut, so where a patch moved the timer the log warns.
    [Test]
    public void AtItsEndAPromptWhoseTimerCantBeCutWarns()
    {
        var client = new FakeClient { CanAsk = false, TimerMoved = true };
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(25));
        client.CanAsk = true;
        run.Frame(Clock.AddSeconds(30));

        Assert.That(run.ReadyCheckEnds, Has.One.Matches<ReadyCheckStep>(end => end.Outcome == ReadyCheckOutcome.Opened && end.Warn));
    }

    // Where the client answered for the player instead, its timer reads one of its own values: no warning.
    [Test]
    public void AtItsEndACheckTheClientAnswersForThePlayerDoesntWarn()
    {
        var client = new FakeClient { CanAsk = false };
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(25));
        run.Frame(Clock.AddSeconds(30));

        Assert.That(client.AnswersSent, Is.EqualTo(new[] { FakeClient.AnsweredNotReady }));
        Assert.That(run.ReadyCheckEnds, Has.One.Matches<ReadyCheckStep>(end => end.Outcome == ReadyCheckOutcome.Opened && !end.Warn));
    }

    // Once the client has its party back, a ready check from the joiner after the run asks the player.
    [Test]
    public void AReadyCheckAfterTheRunFromSomeoneWhoJoinedDuringItCanBeAnswered()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.Lift();
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.ServerSendsLive(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Alpha));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Alpha.Name));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // A ready check that reaches the client after the lift, while a held one still waits, is the one
    // shown: the held one's start would reset it.
    [Test]
    public void AReadyCheckAfterTheLiftReplacesOneStillWaiting()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.ServerSendsLive(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Alpha));
        for (var at = 58.5; at <= 75; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.Multiple(() =>
        {
            Assert.That(client.Received.Count(opcode => opcode == CrossWorldReadyCheckOpcode), Is.EqualTo(1), "the live start only");
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(HeldReadyCheck.Window.TotalSeconds - 17).Within(0.001), "the live check's own time");
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    [Test]
    public void ThePlayersOwnReadyCheckIsNeverResetByOneStillWaiting()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        client.PlayerStartsReadyCheck();
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        for (var at = 58.5; at <= 75; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.Received, Has.No.Member(CrossWorldReadyCheckOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
    }

    [Test]
    public void ANewRunDropsAReadyCheckStillWaiting()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        run.Arm();
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        for (var at = 58.5; at <= 75; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.Received, Has.No.Member(CrossWorldReadyCheckOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
    }

    // Someone who joined during the run answers a member's check. The client drops an answer from
    // anyone it doesn't know, so the check waits for the joiner's roster too.
    [Test]
    public void AnAnswerFromSomeoneWhoJoinedDuringTheRunStillCounts()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1), at: Clock);
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock.AddSeconds(40));
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Two, 1), at: Clock.AddSeconds(40));
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Alpha, 1), at: Clock.AddSeconds(41));
        run.Lift(Clock.AddSeconds(58));
        run.ServerAnswersRosterRequests(Me, Me, Two, Alpha);
        run.Frame(Clock.AddSeconds(58.2));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(2), "Two's ready and Alpha's");
        });
    }

    // The client had an answer of the player's from an earlier check. A check it can't ask the player
    // about would close on it at once, as 1/1; held until it can, it asks.
    [Test]
    public void AnEarlierAnswerOfThePlayersDoesntCloseAHeldReadyCheck()
    {
        var client = RecruitingCrossWorldLeader();
        client.AnsweredEarlier(Me, status: 3);
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.Frame(Clock.AddSeconds(58.2));

        Assert.Multiple(() =>
        {
            Assert.That(client.ChatLog, Has.None.StartsWith("Ready check complete."));
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // The roster comes in as the player walks out of the inn: the check waits until the client would ask
    // them, rather than have it answer for them.
    [Test]
    public void AHeldReadyCheckWaitsUntilThePlayerCanBeAsked()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        client.CanAsk = false;
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.Frame(Clock.AddSeconds(58.2));
        Assert.That(client.Received, Has.No.Member(CrossWorldReadyCheckOpcode), "between areas");
        client.CanAsk = true;
        run.Frame(Clock.AddSeconds(61));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(9).Within(0.001));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // With little time left the check doesn't wait for a name and run out.
    [Test]
    public void AReadyCheckWithLittleTimeLeftOpensWithoutWaitingForNames()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(67));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckTimer, Is.EqualTo(3).Within(0.001));
    }

    // Without the client's own party check, the plugin can't tell whether the client would ask the
    // player or answer for them: nothing is handed to it, even once the check reads again.
    [Test]
    public void AReadyCheckIsNeverHandedOverWhenTheClientsPartyCantBeRead()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        client.CountsItselfThrows = true;
        run.Lift(Clock.AddSeconds(58));
        client.CountsItselfThrows = false;
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        for (var at = 58.5; at <= 75; at += 0.5) run.Frame(Clock.AddSeconds(at));

        Assert.That(client.Received, Has.No.Member(CrossWorldReadyCheckOpcode));
        Assert.That(client.AnswersSent, Is.Empty);
    }

    // A patch can break ClientStructs' member lookup; only the names hang on it.
    [Test]
    public void AReadyCheckStillOpensWhenTheClientsMemberLookupIsBroken()
    {
        var client = new FakeClient { KnowsMemberThrows = true };
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        run.Frame(Clock.AddSeconds(12));

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
    }

    // Members the client already knows answer while the check waits for the starter's roster. The start
    // the lift then hands the client clears the answers it has, so theirs are handed to it again.
    [Test]
    public void AnswersWhileAReadyCheckWaitsStillCount()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        run.ServerSendsLive(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Two, 1));
        run.ServerAnswersRosterRequests(Me, Me, Two, Alpha);
        run.Frame(Clock.AddSeconds(58.2));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Alpha.Name), "the check waited for the roster to name Alpha");
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(2), "Alpha's held ready and Two's after the lift");
        });
    }

    // A client already in a party with others asks the player, but names and marks only a starter it
    // knows: the check waits a little for the joiner's roster, then opens without their name.
    [Test]
    public void AReadyCheckWhoseStarterTheClientDoesntLearnInTimeOpensWithoutTheirName()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        JoinAndStartTwoReadyChecks(run, Alpha);
        run.Lift(Clock.AddSeconds(58));
        run.Frame(Clock.AddSeconds(59.5));
        Assert.That(client.ReadyCheckPrompt, Is.False, "within the wait for the roster");
        run.Frame(Clock.AddSeconds(60));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.Null);
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(10).Within(0.001));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // The leader removes the player from the party during the run. Once out of the sim, the client prints
    // its own line and leaves the party, as it would have then.
    [Test]
    public void APlayerRemovedFromThePartyDuringTheRunIsOutOfItAfterTheRun()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Alpha, Me]);

        run.ServerSends(MemberRemovedOpcode, MemberRemoved(Me));
        Assert.That(client.Party, Is.Not.Empty, "the client hears nothing until the run ends");
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "You were removed from the party." }));
            Assert.That(client.ChatLog, Is.EqualTo(new[] { "You leave the party." }));
            Assert.That(client.Party, Is.Empty);
        });
    }

    // The receiver names the removal too, before the client has handled one on a new game version.
    [Test]
    public void OnANewGameVersionAPlayerRemovedDuringTheFirstRunIsOutOfThePartyAfterIt()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Alpha, Me]);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);

        run.ServerSends(MemberRemovedOpcode, MemberRemoved(Me));
        run.Lift();

        Assert.That(client.Party, Is.Empty);
        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "You were removed from the party." }));
    }

    // The leader disbands the party during the run: once out of the sim, the client prints its own line
    // and leaves the party, as it would have then.
    [Test]
    public void APartyDisbandedDuringTheRunIsGoneAfterTheRun()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Alpha, Me]);

        run.ServerSends(DisbandedOpcode, new byte[8]);
        Assert.That(client.Party, Is.Not.Empty, "the client hears nothing until the run ends");
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "The party was disbanded." }));
            Assert.That(client.ChatLog, Is.EqualTo(new[] { "The cross-world party has been disbanded." }));
            Assert.That(client.Party, Is.Empty);
        });
    }

    [Test]
    public void OnANewGameVersionAPartyDisbandedDuringTheFirstRunIsGoneAfterIt()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Alpha, Me]);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);

        run.ServerSends(DisbandedOpcode, new byte[8]);
        run.Lift();

        Assert.That(client.Party, Is.Empty);
    }

    [Test]
    public void AMemberRemovedFromThePartyDuringTheRunIsGoneAfterIt()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two, Three]);

        run.ServerSends(MemberRemovedOpcode, MemberRemoved(Two));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) was removed from the party." }));
            Assert.That(client.ChatLog, Is.EqualTo(new[] { "Two Two was removed from the party." }));
            Assert.That(client.Party.Select(m => m.ContentId), Is.EqualTo(new[] { Me.ContentId, Three.ContentId }));
        });
    }

    // What doesn't read as a join, a leave or a removal of someone in the party never reaches the client:
    // after a patch, the receiver's table could name another packet.
    [Test]
    public void WhatDoesntReadAsAPartyChangeIsNeverHandedToTheClient()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(MemberEventOpcode, new byte[PartyMemberEvent.Length]);
        run.ServerSends(MemberLeftOpcode, MemberLeft(Alpha));
        run.ServerSends(MemberRemovedOpcode, MemberRemoved(Three));
        run.Lift();

        Assert.That(client.Received, Is.Empty);
    }

    [Test]
    public void ADisbandOutsideAPartyIsNeverHandedToTheClient()
    {
        var client = new FakeClient();
        using var run = new Run(client);

        run.ServerSends(DisbandedOpcode, new byte[8]);
        run.Lift();

        Assert.That(client.Received, Is.Empty);
    }

    // However the clock moved, the client never gets more than its own 30 s to answer.
    [Test]
    public void AHeldReadyCheckNeverGetsMoreTimeThanTheClientGives()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock.AddHours(1));
        run.Lift(Clock);

        Assert.That(client.ReadyCheckPrompt, Is.True);
        Assert.That(client.ReadyCheckTimer, Is.EqualTo(HeldReadyCheck.Window.TotalSeconds));
    }

    // Joins and leaves reach the client at the lift as the server sent them, in order with the notices.
    [Test]
    public void JoinsAndLeavesReachTheClientInTheOrderTheyCame()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
        run.ServerSends(NoticeOpcode, Notice(RecruitmentCommenced, 1));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(MemberEventOpcode, MemberEvent(Three, kind: 1, listingFull: true));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.Received, Is.EqualTo(new[] { MemberEventOpcode, NoticeOpcode, MemberLeftOpcode, MemberEventOpcode }));
            Assert.That(client.ChatLog, Is.EqualTo(new[] { "Alpha Eight joins the party.", LogMessages[RecruitmentCommenced], "Two Two has left the party.", "Three Three joins the party.", LogMessages[AllPlacesFilled] }));
            Assert.That(client.Party.Select(m => m.ContentId), Is.EqualTo(new[] { Me.ContentId }), "Two's leave");
            Assert.That(client.RosterRequests, Is.EqualTo(2));
        });
    }

    // A roster the server sent during the run reaches the client as it came: its cross-world party is the
    // server's at once.
    [Test]
    public void ARosterSentDuringTheRunIsTheClientsPartyAfterIt()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(RosterOpcode, Roster(Two, Me, Two, Alpha));
        run.Lift();

        Assert.That(client.Party.Select(m => m.ContentId), Is.EqualTo(new[] { Me.ContentId, Two.ContentId, Alpha.ContentId }));
    }

    // Members' status holds all eight of a full party, the last as much as the first.
    [Test]
    public void MembersStatusReachesTheClientForAFullParty()
    {
        Member[] party = [Me, Alpha, Two, Three, Blocked, .. Enumerable.Range(0, 3).Select(i => new Member(0x0040_0000_0000_0020 + (ulong)i, $"Member {(char)('A' + i)}", 74))];
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: party);
        var status = MembersStatus(party);

        run.ServerSends(MemberStatusOpcode, status);
        run.Lift();

        Assert.That(client.MembersStatus[party[7].ContentId], Is.EqualTo(status[0xA8..0xC0]));
    }

    [Test]
    public void MembersStatusSentDuringTheRunReachesTheClient()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);
        var status = MembersStatus(Me, Two);

        run.ServerSends(MemberStatusOpcode, status);
        run.Lift();

        Assert.That(client.MembersStatus[Two.ContentId], Is.EqualTo(status[0x18..0x30]));
    }

    // A roster, members' status or party list is handed back only if it reads as the player's party:
    // after a patch the receiver's table could name another packet.
    [TestCase("a roster without the player")]
    [TestCase("a roster led by someone outside it")]
    [TestCase("a roster with a member who has no name")]
    [TestCase("members' status naming someone outside the party")]
    [TestCase("members' status naming no one")]
    [TestCase("a party list without the player")]
    [TestCase("a party list of more than a party holds")]
    [TestCase("a party list led by someone outside it")]
    [TestCase("a party list with someone past its count")]
    [TestCase("a party list with a member who has no name")]
    [TestCase("a party list with a member the client takes as invalid")]
    [TestCase("a party list with a name past its count")]
    [TestCase("a party list with a leader and no members")]
    [TestCase("an empty roster with a leader")]
    public void WhatDoesntReadAsThePlayersPartyIsNeverHandedToTheClient(string held)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);
        var (opcode, payload) = held switch
        {
            "a roster without the player" => (RosterOpcode, Roster(Two, Two, Alpha)),
            "a roster led by someone outside it" => (RosterOpcode, Roster(Three, Me, Two)),
            "a roster with a member who has no name" => (RosterOpcode, With(Roster(Me, Me, Two), 8 + 0x58 + 0x33, 0)),
            "members' status naming someone outside the party" => (MemberStatusOpcode, MembersStatus(Me, Three)),
            "members' status naming no one" => (MemberStatusOpcode, MembersStatus()),
            "a party list without the player" => (PartyListOpcode, PartyList(0, Two, Two, Three)),
            "a party list of more than a party holds" => (PartyListOpcode, With(PartyList(0, Me, Me, Two, Three, Alpha, Me, Two, Three, Alpha), 0xE51, 9)),
            "a party list led by someone outside it" => (PartyListOpcode, With(PartyList(0, Me, Me, Two), 0xE50, 2)),
            "a party list with someone past its count" => (PartyListOpcode, With(PartyList(0, Me, Me, Two), 2 * 0x1C8 + 0x28, 1)),
            "a party list with a member who has no name" => (PartyListOpcode, With(PartyList(0, Me, Me, Two), 0x1C8, 0)),
            "a party list with a name past its count" => (PartyListOpcode, With(PartyList(0, Me, Me, Two), 2 * 0x1C8, (byte)'X')),
            "a party list with a leader and no members" => (PartyListOpcode, With(PartyList(0, Me), 0xE50, 1)),
            "an empty roster with a leader" => (RosterOpcode, Roster(Two)),
            _ => (PartyListOpcode, With(PartyList(0, Me, Me, Two), 0x1C8 + 0x54, 0)),
        };

        run.ServerSends(opcode, payload);
        run.Lift();

        Assert.That(client.Received, Is.Empty);
        Assert.That(run.SocialRefreshed, Is.True, "for what the client didn't get");
    }

    // The client decodes a party list's ids with its dispatcher's keys. A list that doesn't hold the
    // player once decoded was sent with other keys: no later list is handed over, and upstream's Social
    // refresh asks the server for the party again.
    [Test]
    public void AfterAPartyListTheClientDidntDecodeNoLaterOneIsHandedOver()
    {
        var client = new FakeClient { MainGroupCount = 2, PartyListKey = 0x5555 };
        using var run = new Run(client);

        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two, Three));
        run.Lift();

        Assert.That(client.Received, Is.EqualTo(new[] { PartyListOpcode }));
        Assert.That(run.SocialRefreshed, Is.True);
    }

    // The server rekeys the party list's decoding at times. The key reaches the client where it came, so
    // each list decodes as it would have then.
    [Test]
    public void APartyListKeyReachesTheClientWhereItCame()
    {
        var client = new FakeClient { MainGroupCount = 2, PartyListKey = 0x0101 };
        using var run = new Run(client);

        run.ServerSends(PartyListOpcode, PartyList(0x0101, Me, Me, Two));
        run.ServerSends(PartyKeyOpcode, PartyKey(0x0202));
        run.ServerSends(PartyListOpcode, PartyList(0x0202, Me, Me, Two, Three));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Two.ContentId, Three.ContentId }));
            Assert.That(run.SocialRefreshed, Is.False);
        });
    }

    // A key is replayed only on an opcode a live packet confirmed as the key's: anything else there may be
    // another packet a patch moved. Without the key, no party list after it decodes.
    [Test]
    public void AKeyNoLivePacketConfirmedIsNeverHandedToTheClient()
    {
        var client = new FakeClient { MainGroupCount = 2, PartyListKey = 0x0101 };
        using var run = new Run(client);
        run.Tracker.ConfirmedPartyKeyOpcode = 0;

        run.ServerSends(PartyListOpcode, PartyList(0x0101, Me, Me, Two));
        run.ServerSends(PartyKeyOpcode, PartyKey(0x0202));
        run.ServerSends(PartyListOpcode, PartyList(0x0202, Me, Me, Two, Three));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.Received, Is.EqualTo(new[] { PartyListOpcode }), "the list before it only");
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Two.ContentId }));
            Assert.That(run.SocialRefreshed, Is.True);
        });
    }

    [Test]
    public void PastTheCapAKeyAndThePartyListsAfterItAreNotHandedOver()
    {
        var client = new FakeClient { MainGroupCount = 2 };
        using var run = new Run(client);

        for (var key = 1UL; key <= 65; key++) run.ServerSends(PartyKeyOpcode, PartyKey(key));
        run.ServerSends(PartyListOpcode, PartyList(65, Me, Me, Two));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.Received.Count(opcode => opcode == PartyKeyOpcode), Is.EqualTo(64));
            Assert.That(client.Received, Has.No.Member(PartyListOpcode));
            Assert.That(run.Tracker.KeysNotReplayed, Is.EqualTo(1));
            Assert.That(run.SocialRefreshed, Is.True);
        });
    }

    // A ClientStructs lookup a patch broke: the list it would check counts as not decoded.
    [Test]
    public void APartyListWhoseDecodingCantBeCheckedStopsTheLaterOnes()
    {
        var client = new FakeClient { MainGroupCount = 2, MainGroupHasThrows = true };
        using var run = new Run(client);

        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two, Three));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.Lift();

        Assert.That(client.Received, Is.EqualTo(new[] { PartyListOpcode, NoticeOpcode }));
        Assert.That(run.SocialRefreshed, Is.True);
        Assert.That(run.LiftWarnings, Has.Exactly(1).Matches<string>(warning => warning.Contains("threw")), "the log warns");
    }

    // A handler a patch broke costs only its own step: the rest still reaches the client.
    [Test]
    public void AStepTheClientCantTakeCostsOnlyItself()
    {
        var client = RecruitingCrossWorldLeader();
        client.OnlineStatusThrows = true;
        using var run = new Run(client);

        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.Lift();

        Assert.That(client.PartyFinderListing, Is.Zero);
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
        Assert.That(run.LiftWarnings, Has.Exactly(1).Matches<string>(warning => warning.Contains("threw")), "the log warns");
    }

    // The server tells the client it's out of the cross-world party with an empty roster.
    [Test]
    public void AnEmptyRosterSentDuringTheRunEmptiesTheClientsParty()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(RosterOpcode, Roster(default));
        run.Lift();

        Assert.That(client.Party, Is.Empty);
    }

    // Past the cap a party state merges only with its kind since the last packet: a notice between two
    // icons still sees the first, as the client would have.
    [Test]
    public void PastTheCapAStateNeverMergesAcrossAPacket()
    {
        var client = RecruitingCrossWorldLeader();
        client.ListingCategory = DeepDungeons;
        using var run = new Run(client);

        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Online));
        for (var i = 0; i < 64; i++) run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, (uint)(i % 2 == 0 ? Busy : Mentor)));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, RecruitingPartyMembers));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.Lift();

        Assert.That(client.ListingRequests, Is.EqualTo(1), "the notice saw the icon still recruiting");
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
    }

    // On a new game version the receiver's table names a party of one world's list and its key: through
    // them a joiner's ready check asks the player, with no Social refresh.
    [Test]
    public void OnANewGameVersionAPartyOfOneWorldsListReachesTheClient()
    {
        var client = new FakeClient { MainGroupCount = 1, PartyListKey = 0x0101 };
        using var run = new Run(client, learned: false);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);
        run.Tracker.ConfirmedPartyKeyOpcode = PartyKeyOpcode;

        run.ServerSends(PartyKeyOpcode, PartyKey(0x0202), at: Clock);
        run.ServerSends(PartyListOpcode, PartyList(0x0202, Me, Me, Two), at: Clock);
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock.AddSeconds(2));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(client.MainGroup, Is.EqualTo(new[] { Me.ContentId, Two.ContentId }));
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(run.SocialRefreshed, Is.False);
        });
    }

    [Test]
    public void OnANewGameVersionMembersStatusReachesTheClient()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);
        var status = MembersStatus(Me, Two);

        run.ServerSends(MemberStatusOpcode, status);
        run.Lift();

        Assert.That(client.MembersStatus[Two.ContentId], Is.EqualTo(status[0x18..0x30]));
        Assert.That(run.SocialRefreshed, Is.False);
    }

    // Without the receiver's table the plugin can't tell every party packet the client missed: the Social
    // refresh runs even when what was held all reached the client.
    [Test]
    public void WithoutTheReceiverTheSocialRefreshAlwaysRuns()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        Assert.That(sources.UseReceiver(TheTable), Is.Null);
        sources.Observed(PartyPacket.Roster, RosterOpcode);
        Assert.That(sources.Observed(PartyPacket.Notice, 0x0123).Retired, Is.Not.Null);

        run.ServerSends(RosterOpcode, Roster(Two, Me, Two, Alpha));
        run.Lift();

        Assert.That(client.Party.Select(m => m.ContentId), Is.EqualTo(new[] { Me.ContentId, Two.ContentId, Alpha.ContentId }), "the roster its handler named");
        Assert.That(run.SocialRefreshed, Is.True);
    }

    // After a run, upstream's Social refresh asks the server for the party again. When every party packet
    // the run held reached the client's own handlers, the client has asked for what it needed itself, as
    // it would have then: the refresh, which would only ask again, is skipped.
    [TestCase("a cross-world join and members' status", 1, 0)]
    [TestCase("a cross-world join that fills the listing", 1, 1)]
    [TestCase("a disband", 0, 1)]
    [TestCase("someone else leaving", 0, 0)]
    [TestCase("a party list of one world and a ready check", 0, 0)]
    public void SocialIsntRefreshedWhenTheClientGotEveryPartyPacket(string held, int rosterRequests, int partyListRequests)
    {
        var oneWorld = held == "a party list of one world and a ready check";
        var client = oneWorld ? new FakeClient { MainGroupCount = 1 } : RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: oneWorld ? null : new[] { Me, Two });
        switch (held)
        {
            case "a cross-world join and members' status":
                run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
                run.ServerSends(MemberStatusOpcode, MembersStatus(Me, Two, Alpha));
                break;
            case "a cross-world join that fills the listing":
                run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
                break;
            case "a disband":
                run.ServerSends(DisbandedOpcode, new byte[8]);
                break;
            case "someone else leaving":
                run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
                break;
            default:
                run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two));
                run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted));
                break;
        }
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(run.SocialRefreshed, Is.False);
            Assert.That(client.RosterRequests, Is.EqualTo(rosterRequests), "roster requests");
            Assert.That(client.PartyListRequests, Is.EqualTo(partyListRequests), "party list requests");
        });
    }

    // Otherwise the refresh runs as upstream has it.
    [TestCase("no party packet")]
    [TestCase("a party packet the lift doesn't replay")]
    [TestCase("more joins and leaves than the lift replays")]
    [TestCase("a party list the client didn't decode")]
    [TestCase("party packets, without the receiver's table")]
    [TestCase("a party packet the client's dispatcher refused")]
    public void SocialIsStillRefreshedAfterARunThatHeld(string held)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);
        switch (held)
        {
            case "no party packet":
                run.ServerSends(0x0333, new byte[64]);
                break;
            case "a party packet the lift doesn't replay":
                run.ServerSends(UnreplayedPartyOpcode, new byte[64]);
                break;
            case "more joins and leaves than the lift replays":
                for (var i = 0; i < 33; i++)
                {
                    run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
                    run.ServerSends(MemberLeftOpcode, MemberLeft(Alpha));
                }
                break;
            case "a party list the client didn't decode":
                client.PartyListKey = 0x5555;
                run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two));
                break;
            case "party packets, without the receiver's table":
                run.Tracker.PartyViewOpcodes = [];
                run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
                break;
            default:
                client.InjectFails = true;
                run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
                break;
        }
        run.Lift();

        Assert.That(run.SocialRefreshed, Is.True);
    }

    public enum Ended { Notice, Icon, Status }

    private static IEnumerable<TestCaseData> EveryEndOrder()
    {
        Ended[][] orders =
        [
            [Ended.Notice, Ended.Icon, Ended.Status],
            [Ended.Notice, Ended.Status, Ended.Icon],
            [Ended.Icon, Ended.Notice, Ended.Status],
            [Ended.Status, Ended.Notice, Ended.Icon],
            [Ended.Icon, Ended.Status, Ended.Notice],
            [Ended.Status, Ended.Icon, Ended.Notice],
        ];
        foreach (var order in orders)
        {
            var noticeLast = order[^1] == Ended.Notice;
            yield return new TestCaseData(order, DeepDungeons, noticeLast ? 0 : 1).SetArgDisplayNames($"Deep Dungeons: {string.Join(", ", order)}");
            yield return new TestCaseData(order, HighEndDuty, 0).SetArgDisplayNames($"high-end duty: {string.Join(", ", order)}");
        }
    }

    // The server ends the player's listing during the run with a notice, their status icon and the online
    // status, in whatever order. For a Deep Dungeons or Gold Saucer listing, the client sends a request of
    // its own when it handles the notice while it still reads as recruiting. Each reaches the client
    // where it came, so it sends that request exactly when it would have then.
    [TestCaseSource(nameof(EveryEndOrder))]
    public void TheEndOfAListingReachesTheClientInTheOrderItCame(Ended[] order, int category, int requests)
    {
        var client = RecruitingCrossWorldLeader();
        client.ListingCategory = category;
        using var run = new Run(client);

        foreach (var ended in order)
        {
            switch (ended)
            {
                case Ended.Notice: run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded)); break;
                case Ended.Icon: run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor)); break;
                case Ended.Status: run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online)); break;
            }
        }
        run.Lift();

        Assert.That(client.ListingRequests, Is.EqualTo(requests));
        Assert.That(client.PartyFinderListing, Is.Zero);
    }

    [Test]
    public void PastTheCapTheLatestJoinsAndLeavesAreDroppedAndCounted()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me]);

        for (var i = 0; i < 35; i++)
        {
            run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
            run.ServerSends(MemberLeftOpcode, MemberLeft(Alpha));
        }
        run.Lift();

        Assert.That(client.Received, Has.Count.EqualTo(64));
        Assert.That(run.Tracker.MembershipDropped, Is.EqualTo(6));
    }

    // Each roster holds the whole party: past the cap the oldest are dropped, and the client still ends
    // with the latest.
    [Test]
    public void PastTheCapTheOldestRostersAreDroppedAndCounted()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, roster: [Me, Two]);

        for (var i = 0; i < 70; i++) run.ServerSends(RosterOpcode, i % 2 == 0 ? Roster(Me, Me, Two, Alpha) : Roster(Me, Me, Two));
        run.ServerSends(RosterOpcode, Roster(Me, Me, Three));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.Party.Select(m => m.ContentId), Is.EqualTo(new[] { Me.ContentId, Three.ContentId }));
            Assert.That(client.Received, Has.Count.EqualTo(64));
            Assert.That(run.Tracker.WholePartyDropped, Is.EqualTo(7));
        });
    }

    // Past the cap a party state takes the place of the last of its kind: the client still ends as the
    // server last set it.
    [Test]
    public void PastTheCapPartyStatesAreMergedAndCounted()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client);

        for (var i = 0; i < 70; i++) run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, (uint)(i % 2 == 0 ? Busy : Mentor)));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, AwayFromKeyboard));
        run.Lift();

        Assert.That(client.StatusIcon, Is.EqualTo(AwayFromKeyboard));
        Assert.That(run.Tracker.StatesMerged, Is.EqualTo(7));
    }

    [Test]
    public void AReadyCheckFromSomeoneThePluginCantNameIsStillShown()
    {
        using var run = new Run(new FakeClient(), rosterRead: false);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "A party member started a ready check." }));
        Assert.That(run.Tracker.ReadyCheckAt(Clock), Is.Not.Null);
    }

    // The client checks a party event's actor against its blacklist, by account as well, and doesn't
    // name a blocked player.
    [Test]
    public void AReadyCheckFromABlockedPlayerDoesntNameThem()
    {
        using var run = new Run(new FakeClient(), roster: [Me, Blocked]);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Blocked, PartyEvent.ReadyCheckStarted), at: Clock);

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "A blacklisted player started a ready check." }));
        Assert.That(run.Tracker.ReadyCheckAt(Clock)?.From, Is.EqualTo("A blacklisted player"));
    }

    [Test]
    public void AReadyCheckThatRanOutDuringTheRunStaysClosed()
    {
        var client = new FakeClient();
        using var run = new Run(client, roster: [Me, Two]);

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckAnswered), at: Clock.AddSeconds(31));

        Assert.That(run.Tracker.ReadyCheckAt(Clock.AddSeconds(30)), Is.Null, "the alert");
        run.Lift(Clock.AddSeconds(31));
        run.Frame(Clock.AddSeconds(40));
        Assert.That(client.ReadyCheckFrom, Is.Null);
        Assert.That(client.AnswersSent, Is.Empty);
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

    // Ready checks happen outside runs too, and the client starting one it received names the packet it
    // came in, which the next run then puts back at the lift. A cross-world start is the starter's
    // content id; a party event begins with the actor's account id and names them at +0x10. The
    // client's buffer keeps, past a start's 8 bytes, whatever it last carried.
    [TestCase("a party event from a member", true, false)]
    [TestCase("a party event from a member whose account id the client doesn't hold", true, false)]
    [TestCase("a cross-world start from a member", false, true)]
    [TestCase("the plugin's own start", false, false)]
    [TestCase("another kind of party event", false, false)]
    [TestCase("a party event from outside the party", false, false)]
    [TestCase("a party event whose account id isn't the member's", false, false)]
    [TestCase("a cross-world start from outside the party", false, false)]
    [TestCase("a party event with a member's content id first", false, false)]
    [TestCase("a cross-world start in the buffer a member's ready check was just in", false, false)]
    [TestCase("a cross-world start from outside the party in the buffer a member's ready check was just in", false, false)]
    [TestCase("a cross-world start in the buffer an ActorControl was just in", false, true)]
    public void AReadyCheckTheClientReceivesTeachesThePacketItCameIn(string received, bool partyEventKnown, bool crossWorldKnown)
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        using var live = new Packets();
        using var buffer = new Packets(reused: true);
        var sources = new PartyOpcodeSources(run.Tracker);
        var membersReadyCheck = PartyEventPacket(Two, PartyEvent.ReadyCheckStarted);
        var dispatched = received switch
        {
            "a party event from a member" => live.Add(PartyEventOpcode, membersReadyCheck),
            "a party event from a member whose account id the client doesn't hold"
                => live.Add(PartyEventOpcode, WithAccountId(PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), 0x3333_3333)),
            "a cross-world start from a member" => live.Add(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two)),
            "the plugin's own start" => (nint)0,
            "another kind of party event" => live.Add(PartyEventOpcode, PartyEventPacket(Two, kind: 3)),
            "a party event from outside the party" => live.Add(PartyEventOpcode, PartyEventPacket(Alpha, PartyEvent.ReadyCheckStarted)),
            "a party event whose account id isn't the member's"
                => live.Add(PartyEventOpcode, WithAccountId(PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), 0x9999_9999)),
            "a cross-world start from outside the party" => live.Add(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Alpha)),
            "a party event with a member's content id first"
                => live.Add(PartyEventOpcode, WithAccountId(PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), Me.ContentId)),
            "a cross-world start in the buffer a member's ready check was just in" => AfterIn(buffer, PartyEventOpcode, membersReadyCheck, Two),
            "a cross-world start from outside the party in the buffer a member's ready check was just in"
                => AfterIn(buffer, PartyEventOpcode, membersReadyCheck, Alpha),
            _ => AfterIn(buffer, ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor), Two),
        };
        sources.Observed(PartyHandlerCalls.ReadyCheck(dispatched, MemberAccount));

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        var partyEventReplayed = client.ReadyCheckFrom == Two.Name;
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Three), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        var crossWorldReplayed = client.ReadyCheckFrom == Three.Name;

        Assert.That((partyEventReplayed, crossWorldReplayed), Is.EqualTo((partyEventKnown, crossWorldKnown)));
    }

    // `starter`'s cross-world start, dispatched from the buffer right after `before`.
    private static nint AfterIn(Packets buffer, ushort opcode, byte[] before, Member starter)
    {
        buffer.Add(opcode, before);
        return buffer.Add(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(starter));
    }

    private static byte[] WithAccountId(byte[] partyEvent, ulong accountId)
    {
        BitConverter.GetBytes(accountId).CopyTo(partyEvent, 0);
        return partyEvent;
    }

    // With the receiver in use, which start a ready check came in is the table's to say: a start the
    // packet's contents named the other one is kept as the table has it, and the table stays.
    [TestCase(nameof(PartyPacket.PartyEvent), CrossWorldReadyCheckOpcode)]
    [TestCase(nameof(PartyPacket.CrossWorldReadyCheck), PartyEventOpcode)]
    public void AReadyCheckStartTheTableGivesTheOtherPacketIsKeptAsTheTableHasIt(string named, ushort opcode)
    {
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        var session = new Session(run.Tracker);
        Assert.That(session.Sources.UseReceiver(TheTable), Is.Null);
        session.Sources.Observed(Enum.Parse<PartyPacket>(named), opcode);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) left the party." }), "the receiver's member left, still in use");

        using var next = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        new PartyOpcodeSources(next.Tracker).Restore(session.Kept);
        next.ServerSends(opcode, opcode == PartyEventOpcode ? PartyEventPacket(Two, PartyEvent.ReadyCheckStarted) : CrossWorldReadyCheckStart(Two), at: Clock);
        Assert.That(next.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { opcode == PartyEventOpcode ? "Two Two started a ready check." : "Two Two (Coeurl) started a ready check." }),
            "the next session, without the receiver");
    }

    // A session without the receiver may have kept a ready check under the other start. Once the
    // receiver is back, the table says which start it is: the table is used, and the right start is
    // saved for the next session.
    [TestCase(nameof(PartyPacket.PartyEvent), CrossWorldReadyCheckOpcode)]
    [TestCase(nameof(PartyPacket.CrossWorldReadyCheck), PartyEventOpcode)]
    public void AReadyCheckKeptAsTheOtherStartIsKeptAsTheTableHasItOnceTheReceiverIsBack(string keptAs, ushort opcode)
    {
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        var misfiled = keptAs == nameof(PartyPacket.PartyEvent)
            ? new HandledPartyOpcodes(0, 0, 0, [], [], opcode, 0)
            : new HandledPartyOpcodes(0, 0, 0, [], [], 0, opcode);
        var session = new Session(run.Tracker, misfiled);
        session.Sources.UseReceiver(TheTable);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) left the party." }), "the receiver in use");

        using var next = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        new PartyOpcodeSources(next.Tracker).Restore(session.Kept);
        next.ServerSends(opcode, opcode == PartyEventOpcode ? PartyEventPacket(Two, PartyEvent.ReadyCheckStarted) : CrossWorldReadyCheckStart(Two), at: Clock);
        Assert.That(next.Tracker.Events.Select(e => e.Text),
            Is.EqualTo(new[] { opcode == PartyEventOpcode ? "Two Two started a ready check." : "Two Two (Coeurl) started a ready check." }), "the next session, without the receiver");
    }

    // Without the receiver, a cross-world start right after a ready check in the same buffer names
    // nothing, so the party event's opcode that ready check taught stays.
    [TestCase("a member's start after a ready check from a member whose account id the client holds")]
    [TestCase("a member's start after a ready check from a member whose account id the client doesn't hold")]
    [TestCase("an outsider's start after a member's ready check")]
    public void ACrossWorldStartRightAfterAReadyCheckLeavesThePartyEventsOpcode(string order)
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        using var buffer = new Packets(reused: true);
        var sources = new PartyOpcodeSources(run.Tracker);
        var readyCheck = order.Contains("doesn't hold")
            ? WithAccountId(PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), 0x3333_3333)
            : PartyEventPacket(Two, PartyEvent.ReadyCheckStarted);
        sources.Observed(PartyHandlerCalls.ReadyCheck(buffer.Add(PartyEventOpcode, readyCheck), MemberAccount));
        sources.Observed(PartyHandlerCalls.ReadyCheck(buffer.Add(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(order.StartsWith("an outsider") ? Alpha : Two)), MemberAccount));

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));

        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
    }

    // The client reuses one buffer packet after packet and writes only each packet's own bytes. A
    // cross-world start right after a member's ready check names nothing, so neither this session nor
    // the next loses the receiver.
    [Test]
    public void ACrossWorldStartRightAfterAMembersReadyCheckLeavesTheReceiverInUse()
    {
        using var buffer = new Packets(reused: true);
        var kept = KeptBy(receiver: true, sources =>
        {
            sources.Observed(PartyHandlerCalls.ReadyCheck(buffer.Add(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted)), MemberAccount));
            sources.Observed(PartyHandlerCalls.ReadyCheck(buffer.Add(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two)), MemberAccount));
        });
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(kept);
        sources.UseReceiver(TheTable);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name), "a party event's");
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Three), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name), "a cross-world party's");
            Assert.That(run.Tracker.Events.Select(e => e.Text), Does.Contain("Two Two (Coeurl) left the party."), "the receiver's member left");
        });
    }

    // Without the receiver, the ready checks the client handled outside a run are known during one.
    [Test]
    public void WithoutTheReceiverTheReadyChecksTheClientHandledAreKnownDuringARun()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Observed(PartyPacket.PartyEvent, PartyEventOpcode);
        sources.Observed(PartyPacket.CrossWorldReadyCheck, CrossWorldReadyCheckOpcode);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name), "a cross-world party's");

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name), "a party event's");
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

        run.ServerSends(MemberStatusOpcode, new byte[0xC8]);
        run.ServerSends(MemberStatusOpcode, new byte[0xC8]);
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

    // Without the receiver, every opcode the roster's handler names is a roster.
    [Test]
    public void WithoutTheReceiverEveryRosterOpcodeAHandlerNamedCounts()
    {
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Observed(PartyPacket.Roster, RosterOpcode);
        sources.Observed(PartyPacket.Roster, 0x0244);

        run.ServerSends(RosterOpcode, Roster(Me, Me, Two, Alpha));
        run.ServerSends(0x0244, Roster(Me, Me, Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", "Alpha Eight (Ravana) left the party." }));
    }

    // Starting a listing brings no Party Finder notice, so the first one may well arrive during a
    // run: its opcode comes from the client's protocol receiver.
    [Test]
    public void TheFirstPartyFinderNoticeEverHeldDuringARunStillReachesTheClient()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.Lift();

        Assert.That(client.PartyFinderListing, Is.Zero, "the Party Finder's own listing");
        Assert.That(client.ChatLog, Is.EqualTo(new[] { LogMessages[AllPlacesFilled] }));
    }

    // Before any party packet has reached the client on a new game version, the receiver names them: a
    // listing that fills as someone joins, and a cross-world ready check from them, during the first run.
    [Test]
    public void OnANewGameVersionTheFirstRunSeesAListingFillAndACrossWorldReadyCheck()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me]);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);

        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.ServerSends(ActorControlTargetOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Alpha), at: Clock);
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Alpha, 1), at: Clock.AddSeconds(2));
        run.Lift(Clock.AddSeconds(10));
        run.ServerAnswersRosterRequests(Me, Me, Alpha);
        run.Frame(Clock.AddSeconds(10.2));

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[]
            {
                "Alpha Eight (Ravana) joined the party.",
                LogMessages[AllPlacesFilled],
                "Alpha Eight (Ravana) started a ready check.",
            }));
            Assert.That(client.UsingPartyFinder, Is.False);
            Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
            Assert.That(client.ReadyCheckPrompt, Is.True);
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Alpha.Name));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(1));
            Assert.That(client.ReadyCheckTimer, Is.EqualTo(19.8).Within(0.001));
            Assert.That(client.AnswersSent, Is.Empty);
        });
    }

    // The rest of what the receiver names: a new leader's roster, someone leaving, and a ready check in
    // a party of one world.
    [Test]
    public void OnANewGameVersionTheFirstRunAlsoSeesRostersLeavesAndReadyChecks()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        Assert.That(new PartyOpcodeSources(run.Tracker).UseReceiver(TheTable), Is.Null);

        run.ServerSends(RosterOpcode, Roster(Three, Me, Two, Three));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Three, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[]
            {
                "Three Three is now the party leader.",
                "Two Two (Coeurl) left the party.",
                "Three Three started a ready check.",
            }));
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name));
        });
    }

    // What the client's handlers did with live packets on this game version outranks the receiver:
    // one that disagrees means the receiver's table doesn't fit the build, and none of it is used.
    [Test]
    public void WhatTheClientsHandlersNamedOutranksTheReceiver()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(new HandledPartyOpcodes(0, 0x0123, 0, [], [], 0, 0));

        Assert.That(sources.UseReceiver(TheTable), Is.Not.Null);
        run.ServerSends(0x0123, OnlineStatus(CrossWorldPartyLeader, Online));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(client.HasOnlineStatus(RecruitingPartyMembers), Is.False, "the online status, on the opcode its handler named");
            Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
            Assert.That(client.ReadyCheckFrom, Is.Null);
            Assert.That(run.Tracker.Events, Is.Empty);
        });
    }

    // The second session on a game version: what the handlers named before agrees with the receiver,
    // which stays in use.
    [Test]
    public void OpcodesSavedOnThisGameVersionThatAgreeKeepTheReceiver()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(new HandledPartyOpcodes(PlayerStateFlagsOpcode, OnlineStatusOpcode, NoticeOpcode, [RosterOpcode], [ActorControlOpcode, ActorControlTargetOpcode],
            PartyEventOpcode, CrossWorldReadyCheckOpcode));

        Assert.That(sources.UseReceiver(TheTable), Is.Null);
        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1));
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Two, 1), at: Clock.AddSeconds(2));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", "Two Two (Coeurl) started a ready check." }));
            Assert.That(client.ReadyCheckAnswers, Is.EqualTo(1));
        });
    }

    // On a revision the plugin doesn't know, what the handlers named on this game version is all there
    // is, kept from the last session.
    [Test]
    public void WithoutTheReceiverTheOpcodesSavedOnThisGameVersionAreUsed()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        new PartyOpcodeSources(run.Tracker).Restore(new HandledPartyOpcodes(PlayerStateFlagsOpcode, OnlineStatusOpcode, NoticeOpcode, [RosterOpcode], [ActorControlOpcode],
            PartyEventOpcode, CrossWorldReadyCheckOpcode));

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.ServerSends(RosterOpcode, Roster(Two, Me, Two, Three));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.PartyFinderListing, Is.Zero);
            Assert.That(client.UsingPartyFinder, Is.False);
            Assert.That(run.Tracker.Events.Select(e => e.Text), Does.Contain("Two Two (Coeurl) is now the party leader."));
        });

        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name), "a party event's");

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Three), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name), "a cross-world party's");
    }

    // Any packet's handler naming an opcode the receiver's table doesn't give that packet means the
    // table doesn't fit the build: during a run, packets on its opcodes then go by as any other would,
    // and only what the handlers named is used.
    private static IEnumerable<TestCaseData> Contradictions()
    {
        yield return new TestCaseData(nameof(PartyPacket.PlayerStateFlags)).SetArgDisplayNames("the conditions on an opcode of their own");
        yield return new TestCaseData(nameof(PartyPacket.OnlineStatus)).SetArgDisplayNames("the online status on an opcode of its own");
        yield return new TestCaseData(nameof(PartyPacket.ActorControl)).SetArgDisplayNames("an ActorControl outside the receiver's forms");
        yield return new TestCaseData(nameof(PartyPacket.Notice)).SetArgDisplayNames("a notice on an opcode of its own");
        yield return new TestCaseData(nameof(PartyPacket.Roster)).SetArgDisplayNames("a roster on an opcode of its own");
        yield return new TestCaseData(nameof(PartyPacket.PartyEvent)).SetArgDisplayNames("a ready check on another opcode");
        yield return new TestCaseData(nameof(PartyPacket.CrossWorldReadyCheck)).SetArgDisplayNames("a cross-world ready check on another opcode");
    }

    [TestCaseSource(nameof(Contradictions))]
    public void ARetiredReceiversOpcodesNameNothingDuringARun(string handled)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        Assert.That(sources.UseReceiver(TheTable), Is.Null);
        sources.Observed(PartyPacket.ActorControl, ActorControlOpcode);
        run.Tracker.ConfirmedPartyKeyOpcode = PartyKeyOpcode;

        Assert.That(sources.Observed(Enum.Parse<PartyPacket>(handled), 0x0123).Retired, Is.Not.Null);
        run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.ServerSends(ActorControlTargetOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Busy));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(RosterOpcode, Roster(Two, Me, Two));
        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(MemberStatusOpcode, MembersStatus(Me, Two));
        run.ServerSends(PartyKeyOpcode, PartyKey(0x0202));
        run.ServerSends(PartyListOpcode, PartyList(0, Me, Me, Two));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(client.Received, Has.No.Member(MemberStatusOpcode).And.No.Member(PartyKeyOpcode).And.No.Member(PartyListOpcode));
            Assert.That(run.SocialRefreshed, Is.True);
            Assert.That(run.Tracker.Events, Is.Empty);
            Assert.That(client[ConditionFlag.UsingPartyFinder], Is.True, "the conditions");
            Assert.That(client.HasOnlineStatus(RecruitingPartyMembers), Is.True, "the online status");
            Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
            Assert.That(client.ReadyCheckFrom, Is.Null);
            Assert.That(client.StatusIcon, Is.EqualTo(Mentor), "the ActorControl form its handler named stays, and only it");
        });
    }

    // A ready check start the client's handler named outlives a retired receiver; the receiver's
    // answer opcode doesn't, so what arrives on it isn't replayed as an answer.
    [Test]
    public void AfterTheReceiverIsRetiredOnlyTheReadyCheckPacketsAHandlerNamedAreReplayed()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        Assert.That(sources.UseReceiver(TheTable), Is.Null);
        sources.Observed(PartyPacket.CrossWorldReadyCheck, CrossWorldReadyCheckOpcode);
        Assert.That(sources.Observed(PartyPacket.OnlineStatus, 0x0123).Retired, Is.Not.Null);

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.ServerSends(CrossWorldReadyCheckAnswerOpcode, CrossWorldReadyCheckAnswer(Me, 1), at: Clock.AddSeconds(2));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name));
            Assert.That(client.ReadyCheckAnswers, Is.Zero);
        });
    }

    // Apart from the two ready check starts, what a handler named is kept under the packet that handler
    // is for, whatever the receiver's table gives that opcode, and the next session takes it for that
    // packet.
    [TestCase(nameof(PartyPacket.Roster), NoticeOpcode)]
    [TestCase(nameof(PartyPacket.Notice), RosterOpcode)]
    public void WhatAHandlerNamedIsKeptUnderItsOwnPacket(string handled, ushort opcode)
    {
        var packet = Enum.Parse<PartyPacket>(handled);
        var kept = KeptBy(receiver: true, sources => sources.Observed(packet, opcode));
        var client = RecruitingCrossWorldLeader();
        client.Handlers[opcode] = packet == PartyPacket.Roster ? Handler.Roster : Handler.Notice;
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        new PartyOpcodeSources(run.Tracker).Restore(kept);

        run.ServerSends(opcode, packet == PartyPacket.Roster ? Roster(Two, Me, Two) : Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events.Select(e => e.Text).Contains("Two Two (Coeurl) is now the party leader."), Is.EqualTo(packet == PartyPacket.Roster), "a roster");
            Assert.That(client.PartyFinderListing == 0, Is.EqualTo(packet == PartyPacket.Notice), "a notice");
        });
    }

    // The receiver is read again at each load, so the next session on this game version has only what
    // the client's handlers confirmed.
    [Test]
    public void OnlyWhatTheClientsHandlersConfirmedIsKeptForTheNextSession()
    {
        var kept = KeptBy(receiver: true, sources =>
        {
            sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
            sources.Observed(PartyPacket.CrossWorldReadyCheck, CrossWorldReadyCheckOpcode);
            sources.Observed(PartyPacket.PartyEvent, PartyEventOpcode);
        });
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
        new PartyOpcodeSources(run.Tracker).Restore(kept);

        run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Online));
        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(MemberEventOpcode, MemberEvent(Alpha, kind: 1, listingFull: true));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(client.HasOnlineStatus(RecruitingPartyMembers), Is.False, "the online status");
            Assert.That(client.PartyFinderListing, Is.EqualTo(Listing), "the notice, which only the receiver gave");
            Assert.That(client.ReadyCheckFrom, Is.EqualTo(Two.Name), "a party event's ready check");
            Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two started a ready check." }));
        });

        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Three), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.That(client.ReadyCheckFrom, Is.EqualTo(Three.Name), "a cross-world party's ready check");
    }

    // ServerPartyWatch rewrites the configuration file whenever what's kept changes, so a packet that
    // teaches nothing new leaves it alone.
    [Test]
    public void TheConfigurationIsRewrittenOnlyWhenWhatsKeptChanges()
    {
        using var run = new Run(new FakeClient(), learned: false);
        var session = new Session(run.Tracker);
        var saves = new List<int>();
        void After(Action step)
        {
            step();
            saves.Add(session.Saves);
        }

        After(() => session.Sources.UseReceiver(TheTable));
        After(() => session.Sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode));
        After(() => session.Sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode));
        After(() => session.Sources.Observed(PartyPacket.ActorControl, ActorControlOpcode));
        After(() => session.Sources.Observed(PartyPacket.ActorControl, ActorControlOpcode));
        After(() => session.Sources.Observed(PartyPacket.OnlineStatus, 0x0123));

        Assert.That(saves, Is.EqualTo(new[] { 0, 1, 1, 2, 2, 3 }), "the receiver, an opcode, it again, a form, it again, another opcode in the first's place");
    }

    // What the handlers named is kept in the configuration with its game version, and comes back only on
    // that version.
    [Test]
    public void WhatTheHandlersNamedComesBackOnTheSameGameVersionOnly()
    {
        var config = new Configuration();
        KeptBy(receiver: false, sources =>
        {
            sources.Observed(PartyPacket.PlayerStateFlags, PlayerStateFlagsOpcode);
            sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
            sources.Observed(PartyPacket.Notice, NoticeOpcode);
            sources.Observed(PartyPacket.Roster, RosterOpcode);
            sources.Observed(PartyPacket.ActorControl, ActorControlOpcode);
            sources.Observed(PartyPacket.PartyEvent, PartyEventOpcode);
            sources.Observed(PartyPacket.CrossWorldReadyCheck, CrossWorldReadyCheckOpcode);
        }).SaveTo(config, "2026.11.03.0000.0000");

        foreach (var (version, known) in new[] { ("2026.11.03.0000.0000", true), ("2026.11.17.0000.0000", false) })
        {
            var client = RecruitingCrossWorldLeader();
            using var run = new Run(client, learned: false, roster: [Me, Two, Three]);
            var sources = new PartyOpcodeSources(run.Tracker);
            if (HandledPartyOpcodes.SavedFor(config, version) is { } saved) sources.Restore(saved);

            run.ServerSends(PlayerStateFlagsOpcode, StateFlags(ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance));
            run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Mentor, Online));
            run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
            run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
            run.ServerSends(RosterOpcode, Roster(Two, Me, Two, Three));
            run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
            run.Lift(Clock.AddSeconds(10));
            var partyEventCheck = client.ReadyCheckFrom;
            run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Three), at: Clock);
            run.Lift(Clock.AddSeconds(10));

            Assert.Multiple(() =>
            {
                Assert.That(client.UsingPartyFinder, Is.EqualTo(!known), version);
                Assert.That(client[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance], Is.True, $"{version}: the conditions as their packet has them");
                Assert.That(client.HasOnlineStatus(CrossWorldPartyLeader), Is.True, $"{version}: the online status as its packet has it");
                Assert.That(client.ChatLog, Is.EqualTo(known ? new[] { LogMessages[AllPlacesFilled] } : Array.Empty<string>()), $"{version}: the notice's own line");
                Assert.That(client.PartyFinderListing, Is.EqualTo(known ? 0 : Listing), version);
                Assert.That(partyEventCheck, Is.EqualTo(known ? Two.Name : null), version);
                Assert.That(client.ReadyCheckFrom, Is.EqualTo(known ? Three.Name : null), version);
                var shown = run.Tracker.Events.Select(e => e.Text);
                if (known)
                    Assert.That(shown, Is.SupersetOf(new[] { "Two Two (Coeurl) is now the party leader.", "Two Two started a ready check.", "Three Three started a ready check." }), version);
                else Assert.That(shown, Is.Empty, version);
            });
        }
    }

    // With the receiver, every opcode the handlers confirmed comes back from the configuration under
    // its own packet, so the next load on this game version uses the table again.
    [Test]
    public void OpcodesConfirmedWithTheReceiverStillAgreeWithItOnTheNextLoad()
    {
        var config = new Configuration();
        KeptBy(receiver: true, sources =>
        {
            sources.Observed(PartyPacket.PlayerStateFlags, PlayerStateFlagsOpcode);
            sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
            sources.Observed(PartyPacket.Notice, NoticeOpcode);
            sources.Observed(PartyPacket.Roster, RosterOpcode);
            sources.Observed(PartyPacket.ActorControl, ActorControlOpcode);
            sources.Observed(PartyPacket.ActorControl, ActorControlTargetOpcode);
            sources.Observed(PartyPacket.PartyEvent, PartyEventOpcode);
            sources.Observed(PartyPacket.CrossWorldReadyCheck, CrossWorldReadyCheckOpcode);
        }).SaveTo(config, "2026.11.03.0000.0000");
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(HandledPartyOpcodes.SavedFor(config, "2026.11.03.0000.0000")!);
        sources.UseReceiver(TheTable);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) left the party." }), "the receiver's member left");
    }

    // A ready check on an opcode the receiver's table gives neither start means the table doesn't fit:
    // the next session on this game version keeps it out and takes the handler's opcode.
    [TestCase(nameof(PartyPacket.PartyEvent))]
    [TestCase(nameof(PartyPacket.CrossWorldReadyCheck))]
    public void AReadyCheckOnAnOpcodeTheTableDoesntGiveKeepsItOutNextSession(string handled)
    {
        var packet = Enum.Parse<PartyPacket>(handled);
        var kept = KeptBy(receiver: true, sources => sources.Observed(packet, 0x0123));
        var client = new FakeClient();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(kept);
        sources.UseReceiver(TheTable);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(PartyEventOpcode, PartyEventPacket(Two, PartyEvent.ReadyCheckStarted), at: Clock);
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events, Is.Empty, "nothing on the table's opcodes");
            Assert.That(client.ReadyCheckFrom, Is.Null);
        });

        run.ServerSends(0x0123, packet == PartyPacket.PartyEvent ? PartyEventPacket(Two, PartyEvent.ReadyCheckStarted) : CrossWorldReadyCheckStart(Two), at: Clock);
        Assert.That(run.Tracker.Events.Select(e => e.Text),
            Is.EqualTo(new[] { packet == PartyPacket.PartyEvent ? "Two Two started a ready check." : "Two Two (Coeurl) started a ready check." }), "the handler's opcode");
    }

    // A table refused at load stays out however many live packets agree with it later.
    [Test]
    public void ARefusedTableStaysOutWhenALaterPacketAgreesWithIt()
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(new HandledPartyOpcodes(0, 0x0123, 0, [], [], 0, 0));
        sources.UseReceiver(TheTable);
        sources.Observed(PartyPacket.PlayerStateFlags, PlayerStateFlagsOpcode);

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.ServerSends(CrossWorldReadyCheckOpcode, CrossWorldReadyCheckStart(Two), at: Clock);
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events, Is.Empty);
            Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
            Assert.That(client.ReadyCheckFrom, Is.Null);
        });
    }

    // Every opcode kept from the last session has to agree with the table for it to be used.
    [TestCase("the conditions agree, the online status doesn't")]
    [TestCase("a later ActorControl form agrees, an earlier one doesn't")]
    public void AnyKeptOpcodeThatDisagreesKeepsTheTableOut(string kept)
    {
        var client = RecruitingCrossWorldLeader();
        using var run = new Run(client, learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(kept.StartsWith("the conditions")
            ? new HandledPartyOpcodes(PlayerStateFlagsOpcode, 0x0123, 0, [], [], 0, 0)
            : new HandledPartyOpcodes(0, 0, 0, [], [0x0123, ActorControlOpcode], 0, 0));
        sources.UseReceiver(TheTable);

        run.ServerSends(NoticeOpcode, Notice(AllPlacesFilled, PartyNotice.ListingEnded));
        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));
        run.Lift(Clock.AddSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(run.Tracker.Events, Is.Empty);
            Assert.That(client.PartyFinderListing, Is.EqualTo(Listing));
        });
    }

    // Without the receiver, the next session on this game version has every roster opcode the handler
    // named.
    [Test]
    public void EveryRosterOpcodeIsKeptForTheNextSession()
    {
        var kept = KeptBy(receiver: false, sources =>
        {
            sources.Observed(PartyPacket.Roster, RosterOpcode);
            sources.Observed(PartyPacket.Roster, 0x0244);
        });
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        new PartyOpcodeSources(run.Tracker).Restore(kept);

        run.ServerSends(RosterOpcode, Roster(Me, Me, Two, Alpha));
        run.ServerSends(0x0244, Roster(Me, Me, Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Alpha Eight (Ravana) joined the party.", "Alpha Eight (Ravana) left the party." }));
    }

    // Without the receiver, the next session on this game version has every ActorControl form the
    // handler named.
    [Test]
    public void EveryActorControlFormIsKeptForTheNextSession()
    {
        var kept = KeptBy(receiver: false, sources =>
        {
            sources.Observed(PartyPacket.ActorControl, ActorControlOpcode);
            sources.Observed(PartyPacket.ActorControl, ActorControlTargetOpcode);
        });
        var client = new FakeClient();
        using var run = new Run(client, learned: false);
        new PartyOpcodeSources(run.Tracker).Restore(kept);

        run.ServerSends(ActorControlTargetOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Busy));
        run.Lift();
        Assert.That(client.StatusIcon, Is.EqualTo(Busy));

        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.Lift();
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));
    }

    // A packet of one opcode takes the one its handler named last, in this session and the next.
    [Test]
    public void APacketOfOneOpcodeTakesTheOneItsHandlerNamedLast()
    {
        HandledPartyOpcodes kept;
        var client = RecruitingCrossWorldLeader();
        using (var run = new Run(client, learned: false))
        {
            var session = new Session(run.Tracker);
            session.Sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
            session.Sources.Observed(PartyPacket.OnlineStatus, 0x0123);
            session.Sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
            run.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Online));
            run.Lift();
            kept = session.Kept;
        }
        var next = RecruitingCrossWorldLeader();
        using var nextRun = new Run(next, learned: false);
        new PartyOpcodeSources(nextRun.Tracker).Restore(kept);
        nextRun.ServerSends(OnlineStatusOpcode, OnlineStatus(CrossWorldPartyLeader, Online));
        nextRun.Lift();

        Assert.Multiple(() =>
        {
            Assert.That(client.HasOnlineStatus(RecruitingPartyMembers), Is.False, "this session");
            Assert.That(next.HasOnlineStatus(RecruitingPartyMembers), Is.False, "the next session");
        });
    }

    // A handler naming the table's opcode again after a stray one: the next session's kept opcodes agree
    // with the table again, and it's used.
    [Test]
    public void AfterAStrayOpcodeTheTablesOwnAgainBringsTheReceiverBackNextSession()
    {
        var kept = KeptBy(receiver: true, sources =>
        {
            sources.Observed(PartyPacket.OnlineStatus, 0x0123);
            sources.Observed(PartyPacket.OnlineStatus, OnlineStatusOpcode);
        });
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        var sources = new PartyOpcodeSources(run.Tracker);
        sources.Restore(kept);
        sources.UseReceiver(TheTable);

        run.ServerSends(MemberLeftOpcode, MemberLeft(Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(new[] { "Two Two (Coeurl) left the party." }));
    }

    // Without the receiver, every ActorControl form the client's handler names is one.
    [Test]
    public void WithoutTheReceiverEveryActorControlFormTheClientNamesIsTaken()
    {
        var client = new FakeClient();
        using var run = new Run(client, learned: false);
        var sources = new PartyOpcodeSources(run.Tracker);
        foreach (var opcode in new[] { ActorControlOpcode, ActorControlTargetOpcode })
            sources.Observed(PartyPacket.ActorControl, opcode);

        run.ServerSends(ActorControlOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Mentor));
        run.Lift();
        Assert.That(client.StatusIcon, Is.EqualTo(Mentor));

        run.ServerSends(ActorControlTargetOpcode, ActorControl(PartyStatusTracker.StatusIconCategory, Busy));
        run.Lift();
        Assert.That(client.StatusIcon, Is.EqualTo(Busy));
    }

    // The client's receiver for the revision PartyPacketMethods knows, the party's packets at this
    // test's opcodes and every other method at one of its own.
    private static readonly Lazy<ProtocolReceiver> TheClientsReceiver = new(() =>
    {
        var known = PartyPacketMethods.ByRevision[775];
        var party = new Dictionary<int, ushort>
        {
            [known.PlayerStateFlags] = PlayerStateFlagsOpcode,
            [known.OnlineStatus] = OnlineStatusOpcode,
            [known.ActorControl[0]] = ActorControlOpcode,
            [known.ActorControl[1]] = 0x0312,
            [known.ActorControl[2]] = ActorControlTargetOpcode,
            [known.Notice] = NoticeOpcode,
            [known.Roster] = RosterOpcode,
            [known.MemberEvent] = MemberEventOpcode,
            [known.MemberLeft] = MemberLeftOpcode,
            [known.MemberRemoved] = MemberRemovedOpcode,
            [known.Disbanded] = DisbandedOpcode,
            [known.MemberStatus] = MemberStatusOpcode,
            [known.PartyList] = PartyListOpcode,
            [known.PartyKey] = PartyKeyOpcode,
            [known.PartyEvent] = PartyEventOpcode,
            [known.CrossWorldReadyCheck] = CrossWorldReadyCheckOpcode,
            [known.CrossWorldReadyCheckAnswer] = CrossWorldReadyCheckAnswerOpcode,
        };
        var places = Enumerable.Range(0, 775).ToDictionary(place => party.TryGetValue(place, out var opcode) ? opcode : (ushort)(0x0400 + place), place => place);
        return ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places).Image(), out var why) ?? throw new InvalidOperationException(why);
    });

    private static PartyOpcodes TheTable => PartyPacketMethods.For(TheClientsReceiver.Value)!;

    // The party as ServerPartyWatch looks it up: each member's account id, 0 where the client has none.
    private static ulong? MemberAccount(ulong contentId)
        => contentId == Me.ContentId ? Me.AccountId : contentId == Two.ContentId ? Two.AccountId : contentId == Three.ContentId ? 0 : null;

    // What a session on this game version left saved for the next one, after `named` ran on its opcode
    // sources.
    private static HandledPartyOpcodes KeptBy(bool receiver, Action<PartyOpcodeSources> named, HandledPartyOpcodes? restored = null)
    {
        using var run = new Run(new FakeClient(), learned: false);
        var session = new Session(run.Tracker, restored);
        if (receiver) Assert.That(session.Sources.UseReceiver(TheTable), Is.Null);
        named(session.Sources);
        return session.Kept;
    }

    // A session's opcode sources and what ServerPartyWatch would have saved from them: what it was
    // restored from until what's kept changes.
    private sealed class Session
    {
        public PartyOpcodeSources Sources { get; }
        public HandledPartyOpcodes Kept { get; private set; }
        public int Saves { get; private set; }

        public Session(PartyStatusTracker tracker, HandledPartyOpcodes? restored = null)
        {
            Kept = restored ?? new HandledPartyOpcodes(0, 0, 0, [], [], 0, 0);
            Sources = new PartyOpcodeSources(tracker, Save);
            if (restored != null) Sources.Restore(restored);
        }

        private void Save()
        {
            Kept = Sources.Saved();
            Saves++;
        }
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

        run.Tracker.Begin(PartyRoster.None, default, default, Me.ContentId, LocalPlayer, LocalAccountId);

        Assert.That(run.Tracker.Events, Is.Empty);
        Assert.That(run.Tracker.RecruitmentEnded, Is.False);
        var fromServer = run.Tracker.TakeFromServer(Clock);
        Assert.That(fromServer.Steps, Is.Empty);
        Assert.That(fromServer.StatusIcon, Is.Null);
    }

    // A handler handed a payload names the dispatched packet only when the payload is that packet's
    // own: an alliance roster hands the roster parser its group 0 further in, and the plugin's own
    // calls hand the handlers buffers of their own.
    [TestCase("the roster's own payload", true)]
    [TestCase("a payload outside a dispatch", false)]
    [TestCase("an alliance's group 0, 8 bytes further in", false)]
    [TestCase("another packet's payload", false)]
    public void OnlyTheDispatchedPacketsOwnPayloadTeachesIt(string handed, bool taught)
    {
        using var run = new Run(new FakeClient(), learned: false, roster: [Me, Two]);
        using var live = new Packets();
        var roster = live.Add(RosterOpcode, Roster(Me, Me, Two));
        var notice = live.Add(NoticeOpcode, Notice(RecruitmentCommenced, 1));
        new PartyOpcodeSources(run.Tracker).Observed(handed switch
        {
            "the roster's own payload" => PartyHandlerCalls.Payload(roster, roster + 16, PartyPacket.Roster),
            "a payload outside a dispatch" => PartyHandlerCalls.Payload(0, roster + 16, PartyPacket.Roster),
            "an alliance's group 0, 8 bytes further in" => PartyHandlerCalls.Payload(roster, roster + 16 + 8, PartyPacket.Roster),
            _ => PartyHandlerCalls.Payload(notice, roster + 16, PartyPacket.Roster),
        });

        run.ServerSends(RosterOpcode, Roster(Two, Me, Two));
        run.ServerSends(NoticeOpcode, Roster(Two, Me, Two));

        Assert.That(run.Tracker.Events.Select(e => e.Text), Is.EqualTo(taught ? new[] { "Two Two (Coeurl) is now the party leader." } : Array.Empty<string>()));
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

    // A party of one world's list, each content id encoded with the server's key, the player's account id
    // the one the run armed with.
    private static byte[] PartyList(ulong key, Member leader, params Member[] members)
    {
        var payload = new byte[PartyListPacket.Length];
        for (var i = 0; i < members.Length; i++)
        {
            var at = i * 0x1C8;
            Encoding.UTF8.GetBytes(members[i].Name).CopyTo(payload, at);
            BitConverter.GetBytes(members[i].ContentId == Me.ContentId ? LocalAccountId : members[i].AccountId).CopyTo(payload, at + 0x28);
            BitConverter.GetBytes(members[i].ContentId ^ key).CopyTo(payload, at + 0x30);
            payload[at + 0x54] = 1;
        }
        payload[0xE50] = (byte)Math.Max(0, Array.IndexOf(members, leader));
        payload[0xE51] = (byte)members.Length;
        return payload;
    }

    private static byte[] PartyKey(ulong key)
    {
        var payload = new byte[0x10];
        BitConverter.GetBytes(key).CopyTo(payload, 0);
        return payload;
    }

    // A cross-world party's members' status: eight entries, each a member's content id first, and
    // something of the member's to show.
    private static byte[] MembersStatus(params Member[] members)
    {
        var payload = new byte[8 * 0x18];
        for (var i = 0; i < members.Length; i++)
        {
            BitConverter.GetBytes(members[i].ContentId).CopyTo(payload, i * 0x18);
            payload[i * 0x18 + 0x10] = (byte)(i + 1);
        }
        return payload;
    }

    private static byte[] With(byte[] payload, int at, byte value)
    {
        payload[at] = value;
        return payload;
    }

    private static byte[] MemberRemoved(Member member)
    {
        var payload = new byte[0x10];
        BitConverter.GetBytes(member.ContentId).CopyTo(payload, 8);
        return payload;
    }

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
        BitConverter.GetBytes(actor.AccountId).CopyTo(payload, 0);
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
    // what ServerPartyWatch puts on it at the lift, and the frames after, when a held ready check waits
    // for the client and the server's packets reach the client again.
    private sealed class Run : IDisposable
    {
        private readonly FakeClient client;
        private readonly Packets packets = new();
        private readonly Member? leader;
        private readonly bool rosterRead;
        private readonly HeldReadyCheckReplay readyCheck = new();
        private DateTime? lastFrame;

        public PartyStatusTracker Tracker { get; } = new(Networked, id => LogMessages.GetValueOrDefault(id), id => Worlds.GetValueOrDefault(id),
            (accountId, _) => BlacklistedAccounts.Contains(accountId));

        // The roster the client shows at arm, its own cross-world party; its first member leads unless
        // `leader` says otherwise.
        public Run(FakeClient client, bool learned = true, Member[]? roster = null, Member? leader = null, bool rosterRead = true)
        {
            this.client = client;
            this.leader = leader;
            this.rosterRead = rosterRead;
            if (learned)
            {
                Tracker.PlayerStateFlagsOpcode = PlayerStateFlagsOpcode;
                Tracker.OnlineStatusOpcode = OnlineStatusOpcode;
                Tracker.NoticeOpcode = NoticeOpcode;
                Tracker.RosterOpcodes = [RosterOpcode];
                Tracker.ActorControlOpcodes = [ActorControlOpcode, ActorControlTargetOpcode];
                Tracker.MemberEventOpcode = MemberEventOpcode;
                Tracker.MemberLeftOpcode = MemberLeftOpcode;
                Tracker.MemberRemovedOpcode = MemberRemovedOpcode;
                Tracker.DisbandedOpcode = DisbandedOpcode;
                Tracker.MemberStatusOpcode = MemberStatusOpcode;
                Tracker.PartyListOpcode = PartyListOpcode;
                Tracker.PartyKeyOpcode = PartyKeyOpcode;
                // As a live key confirms it.
                Tracker.ConfirmedPartyKeyOpcode = PartyKeyOpcode;
                Tracker.PartyViewOpcodes = [NoticeOpcode, RosterOpcode, MemberEventOpcode, MemberLeftOpcode, MemberRemovedOpcode, DisbandedOpcode, MemberStatusOpcode,
                    PartyListOpcode, UnreplayedPartyOpcode];
                Tracker.PartyEventOpcode = PartyEventOpcode;
                Tracker.CrossWorldReadyCheckOpcode = CrossWorldReadyCheckOpcode;
                Tracker.CrossWorldReadyCheckAnswerOpcode = CrossWorldReadyCheckAnswerOpcode;
            }
            if (roster != null) client.Party = [.. roster];
            // ServerPartyWatch's InitiateReadyCheck hook.
            client.ReadyCheckReset = replay => Ended(readyCheck.AgentReset(replay, Clock));
            Arm();
        }

        // ServerPartyWatch.Begin, at arm: the client's cross-world party as it is then.
        public void Arm()
        {
            Ended(readyCheck.Drop("a new run began", Clock));
            var members = client.Party.Select(m => new PartyMember(m.ContentId, m.Name, m.HomeWorld, m.AccountId)).ToArray();
            var leaderId = leader is { } first && members.Any(m => m.ContentId == first.ContentId) ? first.ContentId : members.FirstOrDefault().ContentId;
            Tracker.Begin(rosterRead ? new PartyRoster(members, leaderId) : null, PartyConditions.Read(flag => client[flag]), PartyOnlineStatus.FromFlags(client.OnlineStatusFlags), Me.ContentId, LocalPlayer,
                LocalAccountId);
        }

        // During the run. `about`: the actor the packet is addressed to.
        public void ServerSends(ushort opcode, byte[] payload, uint about = LocalPlayer, DateTime? at = null)
            => Tracker.Observe(packets.Add(opcode, payload), about, at ?? Clock);

        // Whether upstream's Social refresh ran at the last lift.
        public bool SocialRefreshed { get; private set; }

        // How each held ready check ended, as ServerPartyWatch logs it.
        public List<ReadyCheckStep> ReadyCheckEnds { get; } = [];

        // What the last lift warned of.
        public List<string> LiftWarnings { get; private set; } = [];

        // TryLift, and the frame it runs in.
        public void Lift(DateTime? at = null)
        {
            var now = at ?? Clock;
            var state = Tracker.TakeFromServer(now);
            var (_, held, handedOver, warnings) = PartyLift.Apply(state, client, Networked, Tracker.LocalContentId, now);
            LiftWarnings = warnings;
            if (held != null) readyCheck.Hold(held, client);
            SocialRefreshed = PartyResync.Reason(state.Held, handedOver, Tracker.PartyViewOpcodes) != null;
            Frame(now);
        }

        // A frame after the lift: the client's ready check counts down the time since the last frame,
        // then ZoneControl ticks the held ready check. In game the tick comes first, and the client
        // counts down only that frame's few ms.
        public void Frame(DateTime at)
        {
            client.Update(lastFrame is { } last && at > last ? (at - last).TotalSeconds : 0);
            lastFrame = at;
            Ended(readyCheck.Tick(at));
        }

        private void Ended(ReadyCheckStep? step)
        {
            if (step is { } end) ReadyCheckEnds.Add(end);
        }

        // After the lift the firewall is down, and the server's packets reach the client as they come.
        public void ServerSendsLive(ushort opcode, byte[] payload, uint about = LocalPlayer)
        {
            var packet = packets.Add(opcode, payload);
            readyCheck.HeardLive(Tracker, packet, about);
            client.Receive(new HeldPacket(opcode, about, payload), replay: false);
        }

        // The server answers each roster request the client sent with the cross-world party as it is now.
        public void ServerAnswersRosterRequests(Member partyLeader, params Member[] party)
        {
            while (client.TakeRosterRequest()) ServerSendsLive(RosterOpcode, Roster(partyLeader, party));
        }

        public void Dispose() => packets.Dispose();
    }

    // Each packet in pages of its own, readable past its payload as the client's receive buffer is; or,
    // `reused`, all in one buffer that, like the client's, gets only each packet's own bytes.
    private sealed class Packets(bool reused = false) : IDisposable
    {
        private const int HeaderAt = 0x40;
        private const int ReusedSize = 0x1000;
        private readonly List<nint> regions = [];

        public nint Add(ushort opcode, byte[] payload)
        {
            var region = reused && regions.Count > 0 ? regions[0] : 0;
            if (region == 0)
            {
                region = VirtualAlloc(0, (nuint)(HeaderAt + 16 + Math.Max(payload.Length, reused ? ReusedSize : 0)), MemReserve | MemCommit, PageReadWrite);
                Assert.That(region, Is.Not.Zero);
                regions.Add(region);
            }
            Assert.That(payload.Length, Is.LessThanOrEqualTo(reused ? ReusedSize : payload.Length));
            var at = region + HeaderAt;
            Marshal.WriteInt16(at, 0, 0x14);
            Marshal.WriteInt16(at, 2, (short)opcode);
            Marshal.Copy(payload, 0, at + 16, payload.Length);
            return at;
        }

        public void Dispose()
        {
            foreach (var region in regions) VirtualFree(region, 0, MemRelease);
        }
    }

    // The client as 2026.11.03's code handles the party: its cross-world party (InfoProxyCrossRealm's
    // group 0) changes only with a roster, which its join handler asks the server for; a cross-world
    // ready check someone else started asks the player only while that party has someone besides them,
    // and otherwise answers "not ready" for them; an answer counts only from someone in that party, and
    // with no prompt waiting on the player, the check closes once everyone in it has answered. A party of
    // one world's list (UpdateParty) is decoded with the dispatcher's key, which only the key packet
    // changes. Each frame a running check counts down while above 0, ending unanswered once the client is
    // out of that kind of party, and answering "not ready" for the player when it runs out. Not modelled:
    // a disband, and notices of codes 5-7, also reaching the Party Finder's listing-end request.
    public enum Handler { None, Notice, Roster, MemberEvent, MemberLeft, MemberRemoved, Disbanded, MemberStatus, PartyList, PartyKey, CrossWorldStart, CrossWorldAnswer, PartyEvent }

    private sealed class FakeClient : IPartyClient
    {
        private const byte Awaiting = 1;
        private const byte Ready = 2;
        private const byte NotReady = 3;
        private const byte NotPresent = 4;
        // What a cross-world client sends for the player who isn't ready, and a party of one world's; and a
        // cross-world client for one who is (UNVERIFIED: only "not ready" was read from its code).
        public const byte AnsweredNotReady = 2;
        public const byte AnsweredNotReadyInOneWorld = 0;
        public const byte AnsweredReady = 1;
        private const double OwnCrossWorldStartSeconds = 35;
        // What the client leaves the timer at when it answers for a player it can't ask (CanAsk false
        // stands for combat here); for one it doesn't count in a party, the reset's 0 stays.
        private const double AnsweredForThePlayerSeconds = 35;

        private readonly HashSet<ConditionFlag> on = [];
        // AgentReadyCheck's answers, by content id; only the prompt clears them.
        private readonly Dictionary<ulong, byte> answers = [];
        private bool checkRunning;
        private bool replaying;
        private int rosterRequests;
        // Whether the check someone else started is a cross-world party's, as the client took it then.
        private bool crossWorldCheck;
        // The Party Finder sends its listing-end request once.
        private bool listingEndRequested;

        public ulong OnlineStatusFlags { get; private set; }
        // The OnlineStatus row the player's character shows.
        public uint StatusIcon { get; set; }
        public List<Member> Party { get; set; } = [];
        // The game's ready check: whether it's asking the player, who the chat says started it, its
        // seconds left, and the answers it took.
        public bool ReadyCheckPrompt { get; private set; }
        public string? ReadyCheckFrom { get; private set; }
        public double ReadyCheckTimer { get; set; }
        public int ReadyCheckAnswers { get; private set; }
        // What the client told the server about the player's readiness.
        public List<byte> AnswersSent { get; } = [];
        public ulong PartyFinderListing { get; set; }
        public List<string> ChatLog { get; } = [];
        public List<byte[]> NoticesHandled { get; } = [];
        public List<uint> NoticeTargets { get; } = [];
        public List<ushort> Received { get; } = [];
        // The party list requests (client IPC 0x94 on 11.03) its handlers sent.
        public int PartyListRequests { get; private set; }
        // GroupManager's main group, as the client decoded its last party list, and the dispatcher's key it
        // decodes one with.
        public List<ulong> MainGroup { get; } = [];
        public ulong PartyListKey { get; set; }
        // The player's own listing's category, and the requests the Party Finder sent as it ended.
        public int ListingCategory { get; set; }
        public int ListingRequests { get; private set; }
        // Each known member's entry of the last members' status.
        public Dictionary<ulong, byte[]> MembersStatus { get; } = [];
        // A patch broke the client's dispatch of a replay, or one of ClientStructs' lookups.
        public bool InjectFails { get; set; }
        public bool MainGroupHasThrows { get; set; }
        public bool StillInPartyThrows { get; set; }
        public bool OnlineStatusThrows { get; set; }
        // A patch moved AgentReadyCheck's timer: the plugin reads and writes another float.
        public bool TimerMoved { get; set; }
        public int PacketsHandled { get; private set; }
        // Which of its handlers each opcode goes to.
        public Dictionary<ushort, Handler> Handlers { get; } = new()
        {
            [NoticeOpcode] = Handler.Notice,
            [RosterOpcode] = Handler.Roster,
            [MemberEventOpcode] = Handler.MemberEvent,
            [MemberLeftOpcode] = Handler.MemberLeft,
            [MemberRemovedOpcode] = Handler.MemberRemoved,
            [MemberStatusOpcode] = Handler.MemberStatus,
            [PartyListOpcode] = Handler.PartyList,
            [PartyKeyOpcode] = Handler.PartyKey,
            [DisbandedOpcode] = Handler.Disbanded,
            [CrossWorldReadyCheckOpcode] = Handler.CrossWorldStart,
            [CrossWorldReadyCheckAnswerOpcode] = Handler.CrossWorldAnswer,
            [PartyEventOpcode] = Handler.PartyEvent,
        };
        // AgentReadyCheck.InitiateReadyCheck, which resets the agent; true for the plugin's replay.
        public Action<bool>? ReadyCheckReset { get; set; }

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

        public bool CountsItselfInAParty() => CountsItselfThrows ? throw new InvalidOperationException("a patch moved it") : InParty();

        private bool InParty() => Party.Count(m => m.ContentId != 0) > 1 || MainGroupCount > 1;

        // A party of one world's members, as GroupManager's main group counts them.
        public int MainGroupCount { get; set; }

        // ClientStructs can't call the client's party check after a patch.
        public bool CountsItselfThrows { get; set; }

        public bool KnowsMember(ulong contentId) => KnowsMemberThrows ? throw new InvalidOperationException("a patch moved it") : contentId != 0 && Party.Any(m => m.ContentId == contentId);

        // False between areas, in combat, in a cutscene: the client answers a ready check for the player.
        public bool CanAsk { get; set; } = true;

        public bool CanAskThePlayer() => CanAsk;

        public bool StillInPartyOf(bool crossWorld) => StillInPartyThrows ? throw new InvalidOperationException("a patch moved it") : InPartyOf(crossWorld);

        public bool MainGroupHas(ulong contentId) => MainGroupHasThrows ? throw new InvalidOperationException("a patch moved it") : MainGroup.Contains(contentId);

        private bool InPartyOf(bool crossWorld) => crossWorld ? CrossRealmParty : InParty();

        // InfoProxyCrossRealm.IsCrossRealmParty: never while the main group holds others.
        private bool CrossRealmParty => Party.Count > 0 && MainGroupCount <= 1;

        // ClientStructs can't call the client's member lookup after a patch.
        public bool KnowsMemberThrows { get; set; }

        public bool TakeRosterRequest()
        {
            if (rosterRequests == 0) return false;
            rosterRequests--;
            return true;
        }

        public int RosterRequests => rosterRequests;

        // The player starts a ready check of their own.
        public void PlayerStartsReadyCheck() => ReadyCheckReset?.Invoke(false);

        // A ready check before the run left this member's answer in the agent.
        public void AnsweredEarlier(Member member, byte status) => answers[member.ContentId] = status;

        // The player answers the prompt.
        public void PlayerClicksReady()
        {
            if (!ReadyCheckPrompt) return;
            ReadyCheckPrompt = false;
            AnswersSent.Add(AnsweredReady);
        }

        // AgentReadyCheck's update, each frame, for a check someone else started.
        public void Update(double seconds)
        {
            if (!checkRunning || ReadyCheckTimer <= 0) return;
            if (!InPartyOf(crossWorldCheck))
            {
                ResetAgent(replay: false);
                return;
            }
            ReadyCheckTimer = Math.Max(0, ReadyCheckTimer - seconds);
            if (ReadyCheckTimer > 0 || !ReadyCheckPrompt) return;
            ReadyCheckPrompt = false;
            AnswersSent.Add(NotReadyAnswer);
        }

        // AgentReadyCheck.InitiateReadyCheck, which ServerPartyWatch hooks.
        private void ResetAgent(bool replay)
        {
            ReadyCheckReset?.Invoke(replay);
            checkRunning = false;
            ReadyCheckPrompt = false;
            ReadyCheckTimer = 0;
        }

        // In the form of the party the client is in as it sends.
        private byte NotReadyAnswer => CrossRealmParty ? AnsweredNotReady : AnsweredNotReadyInOneWorld;

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

        public void HandleActorControl(uint entityId, ushort category, uint p1)
        {
            PacketsHandled++;
            if (entityId == LocalPlayer && category == PartyStatusTracker.StatusIconCategory) StatusIcon = p1;
        }

        // A server packet through the client's dispatcher.
        public void Receive(HeldPacket packet, bool replay)
        {
            replaying = replay;
            try
            {
                PacketsHandled++;
                Received.Add(packet.Opcode);
                switch (Handlers.GetValueOrDefault(packet.Opcode, Handler.None))
                {
                    case Handler.Notice: HandleNotice(packet.TargetId, packet.Payload); break;
                    case Handler.Roster: HandleRoster(packet.Payload); break;
                    case Handler.MemberEvent: HandleMemberEvent(packet.Payload); break;
                    case Handler.MemberLeft: HandleMemberLeft(BitConverter.ToUInt64(packet.Payload, 0)); break;
                    case Handler.MemberRemoved: HandleMemberRemoved(BitConverter.ToUInt64(packet.Payload, 8)); break;
                    case Handler.Disbanded:
                        ChatLog.Add("The cross-world party has been disbanded.");
                        LeaveCrossWorldParty();
                        break;
                    case Handler.PartyList: HandlePartyList(packet.Payload); break;
                    case Handler.MemberStatus: HandleMemberStatus(packet.Payload); break;
                    case Handler.PartyKey: PartyListKey = BitConverter.ToUInt64(packet.Payload, 0); break;
                    case Handler.CrossWorldStart: HandleCrossWorldStart(BitConverter.ToUInt64(packet.Payload, 0)); break;
                    case Handler.CrossWorldAnswer: HandleCrossWorldAnswer(BitConverter.ToUInt64(packet.Payload, 0), packet.Payload[8]); break;
                    case Handler.PartyEvent: HandlePartyEvent(packet.Payload); break;
                }
            }
            finally
            {
                replaying = false;
            }
        }

        // A listing's end: for a Gold Saucer or Deep Dungeons listing, the Party Finder sends a request of
        // its own, once, while the client still reads as recruiting.
        private void HandleNotice(uint targetId, byte[] payload)
        {
            NoticesHandled.Add(payload);
            NoticeTargets.Add(targetId);
            if (LogMessages.GetValueOrDefault(BitConverter.ToUInt32(payload, 0)) is { } text) ChatLog.Add(text);
            if (payload[6] != PartyNotice.ListingEnded) return;
            if (!listingEndRequested && ListingCategory is GoldSaucer or DeepDungeons && (StatusIcon == RecruitingPartyMembers || HasOnlineStatus(RecruitingPartyMembers)))
            {
                ListingRequests++;
                listingEndRequested = true;
            }
            PartyFinderListing = 0;
        }

        // UpdateParty: the main group as the list has it, each content id decoded with the client's key.
        private void HandlePartyList(byte[] payload)
        {
            MainGroup.Clear();
            MainGroupCount = payload[0xE51];
            for (var i = 0; i < MainGroupCount; i++) MainGroup.Add(BitConverter.ToUInt64(payload, i * 0x1C8 + 0x30) ^ PartyListKey);
        }

        // Members' status: the entry of each member the client knows.
        private void HandleMemberStatus(byte[] payload)
        {
            for (var at = 0; at + 0x18 <= payload.Length; at += 0x18)
            {
                var contentId = BitConverter.ToUInt64(payload, at);
                if (Knows(contentId)) MembersStatus[contentId] = payload[at..(at + 0x18)];
            }
        }

        // Out of a cross-world party, the client asks the server for its party list again.
        private void LeaveCrossWorldParty()
        {
            if (!CrossRealmParty) return;
            Party.Clear();
            PartyListRequests++;
        }

        private void HandleRoster(byte[] payload)
        {
            Party = [];
            for (var i = 0; i < 8; i++)
            {
                var entry = 8 + i * 0x58;
                var contentId = BitConverter.ToUInt64(payload, entry + 0x10);
                if (contentId != 0) Party.Add(new Member(contentId, Utf8(payload, entry + 0x33), BitConverter.ToUInt16(payload, entry + 0x2A)));
            }
        }

        // In a cross-world party, prints the join and asks the server for the roster whatever the kind;
        // when the join filled the listing, prints that and asks for the party list.
        private void HandleMemberEvent(byte[] payload)
        {
            var kind = payload[1];
            if (Party.Count > 0 && kind is 1 or 4 or 5 && BitConverter.ToUInt64(payload, 0x10) != Me.ContentId) ChatLog.Add($"{Utf8(payload, 0x22)} joins the party.");
            if (payload[0] != 0 && kind != 2)
            {
                ChatLog.Add(LogMessages[AllPlacesFilled]!);
                PartyListRequests++;
            }
            if (Party.Count > 0) rosterRequests++;
        }

        // The player leaving: its line, and out of the cross-world party. Anyone else: their line, and
        // out of the client's party.
        private void HandleMemberLeft(ulong contentId)
        {
            if (contentId == Me.ContentId)
            {
                ChatLog.Add("You leave the party.");
                LeaveCrossWorldParty();
                return;
            }
            if (Party.FirstOrDefault(m => m.ContentId == contentId) is { ContentId: not 0 } leaver) ChatLog.Add($"{leaver.Name} has left the party.");
            Party.RemoveAll(m => m.ContentId == contentId);
        }

        // The player removed: its line, and out of the cross-world party. Anyone else: their line, and out
        // of the client's party.
        private void HandleMemberRemoved(ulong contentId)
        {
            if (contentId == Me.ContentId)
            {
                ChatLog.Add("You leave the party.");
                LeaveCrossWorldParty();
                return;
            }
            if (Party.FirstOrDefault(m => m.ContentId == contentId) is { ContentId: not 0 } removed) ChatLog.Add($"{removed.Name} was removed from the party.");
            Party.RemoveAll(m => m.ContentId == contentId);
        }

        // The player's own start comes back without a prompt, setting the starter's own 35 s
        // (UNVERIFIED in game).
        private void HandleCrossWorldStart(ulong starter)
        {
            if (starter == Me.ContentId)
            {
                ReadyCheckTimer = OwnCrossWorldStartSeconds;
                return;
            }
            ResetAgent(replaying);
            checkRunning = true;
            crossWorldCheck = CrossRealmParty;
            if (InParty() && CanAsk)
            {
                ReadyCheckPrompt = true;
                ReadyCheckTimer = HeldReadyCheck.Window.TotalSeconds;
                ReadyCheckAnswers = 0;
                answers.Clear();
            }
            else
            {
                AnswersSent.Add(NotReadyAnswer);
                if (!CanAsk) ReadyCheckTimer = AnsweredForThePlayerSeconds;
            }
            ReadyCheckFrom = null;
            if (Party.FirstOrDefault(m => m.ContentId == starter) is not { ContentId: not 0 } named) return;
            answers[starter] = Ready;
            ReadyCheckFrom = named.Name;
        }

        private bool Knows(ulong contentId) => contentId != 0 && Party.Any(m => m.ContentId == contentId);

        // An answer counts only from someone in its party; then, whoever answered, the check closes once
        // no prompt waits on the player and everyone in its party has an answer, an old one included.
        private void HandleCrossWorldAnswer(ulong member, byte answer)
        {
            if (Knows(member))
            {
                answers[member] = answer switch { 1 => Ready, 3 => NotPresent, _ => NotReady };
                ReadyCheckAnswers++;
            }
            if (!checkRunning || ReadyCheckPrompt || Party.Any(m => answers.GetValueOrDefault(m.ContentId, Awaiting) == Awaiting)) return;
            checkRunning = false;
            ReadyCheckTimer = 10;
            var ready = Party.Count(m => answers[m.ContentId] == Ready);
            ChatLog.Add($"Ready check complete. Ready: {ready}/{Party.Count}, Not Ready: {Party.Count - ready}/{Party.Count}");
        }

        // The party event names who started it.
        private void HandlePartyEvent(byte[] payload)
        {
            if (payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckStarted)
            {
                ResetAgent(replaying);
                if (BitConverter.ToUInt64(payload, 0x10) == Me.ContentId) return;
                checkRunning = true;
                crossWorldCheck = CrossRealmParty;
                if (!InParty() || !CanAsk)
                {
                    AnswersSent.Add(NotReadyAnswer);
                    if (!CanAsk) ReadyCheckTimer = AnsweredForThePlayerSeconds;
                    return;
                }
                ReadyCheckPrompt = true;
                ReadyCheckFrom = Utf8(payload, 0x25);
                ReadyCheckTimer = HeldReadyCheck.Window.TotalSeconds;
                ReadyCheckAnswers = 0;
            }
            else if (payload[PartyEvent.KindOffset] == PartyEvent.ReadyCheckAnswered) ReadyCheckAnswers++;
        }

        private static string Utf8(byte[] bytes, int at)
        {
            var end = Array.IndexOf(bytes, (byte)0, at);
            return Encoding.UTF8.GetString(bytes, at, (end < 0 ? bytes.Length : end) - at);
        }

        bool IPartyClient.Condition(ConditionFlag flag) => this[flag];

        void IPartyClient.HandleStateFlags(ReadOnlySpan<byte> bits) => HandleStateFlags(bits.ToArray());

        ulong? IPartyClient.OnlineStatusFlags => OnlineStatusFlags;

        void IPartyClient.SetOnlineStatusFlags(ulong flags)
        {
            if (OnlineStatusThrows) throw new InvalidOperationException("a patch moved it");
            HandleOnlineStatus(flags);
        }

        void IPartyClient.HandleActorControl(HeldActorControl control) => HandleActorControl(control.EntityId, control.Category, control.P1);

        bool IPartyClient.Inject(HeldPacket packet, string what)
        {
            if (InjectFails) return false;
            Receive(packet, replay: true);
            return true;
        }

        float? IPartyClient.ReadyCheckTimer
        {
            get => TimerMoved ? 5f : (float)ReadyCheckTimer;
            set
            {
                if (!TimerMoved && value is { } seconds) ReadyCheckTimer = seconds;
            }
        }
    }

    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);
}
