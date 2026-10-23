using System.Globalization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.P3AlexanderPrime;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// A peer's own tank LB3 in TEA lands on its client one recipient at a time, and each landing reaches
// the host as a report of its own, which the host's copy of that seat takes as it arrives.
public class PeerTankLimitBreakTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private static readonly PlayerSeat Peer = new(PartyRole.MainTank, BotControlled: false);
    // 7.2s after Divine Judgment's success.
    private const double PressedAt = 272.27 + 7.2;
    private const double OnCaster = 1.27, EachNext = 0.145;
    private static readonly ushort[] TankLimitBreakStatuses = [196, 863, 864, 1931];

    [Test]
    public void APeersLimitBreakReachesTheHostOneLandingAtATime()
    {
        var pressed = double.NaN;
        var run = MultiplayerRun.Play(typeof(TeaP3AlexanderPrimeScenario), 0, 1, Host, [Peer], immortal: true,
            inputs: [(PressedAt, 1, client => pressed = Press(client))]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "the press the fight ended before");
        var peer = run.Peers[0];
        var sent = peer.StreamSent.Where(s => IsLanding(s.Message)).Select(s => s.At).ToList();
        var taken = run.Host.StreamTaken.Where(t => t.From == peer.PeerId && IsLanding(t.Message))
            .Select(t => (t.At, Report: (PeerAppliedRoleStatusMessage)t.Message)).ToList();
        var lands = run.Host.Fake.Timeline.Rows
            .Where(r => r["kind"] == "status_gain" && TankLimitBreakStatuses.Contains(ushort.Parse(r["id"], CultureInfo.InvariantCulture)))
            .Select(r => Landing(run.Host, r))
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(sent, Has.Count.EqualTo(PerRole.All.Length));
            Assert.That(taken, Has.Count.EqualTo(sent.Count));
            Assert.That(taken.Select(t => t.Report.Roles.Count), Is.All.EqualTo(1));
            Assert.That(taken.Select(t => t.Report.Roles[0]), Is.EquivalentTo(PerRole.All));
            Assert.That(lands, Has.Count.EqualTo(PerRole.All.Length));
            if (taken.Count != sent.Count || taken.Count == 0 || taken.Any(t => t.Report.Roles.Count != 1)) return;
            Assert.That(taken[0].Report.Roles[0], Is.EqualTo(Peer.Role));
            for (var k = 0; k < taken.Count; k++)
            {
                Assert.That(sent[k] - pressed, Is.EqualTo(OnCaster + EachNext * k).Within(0.02), $"report {k} sent");
                var seat = taken[k].Report.Roles[0].ToString();
                var land = lands.Where(l => l.Seat == seat).ToList();
                Assert.That(land, Has.Count.EqualTo(1), $"{seat}'s landings on the host");
                if (land.Count == 1) Assert.That(land[0].At - taken[k].At, Is.EqualTo(0.0).Within(0.04), $"{seat}'s landing on the host");
            }
        });
    }

    private static bool IsLanding(MpMessage message)
        => message is PeerAppliedRoleStatusMessage report && TankLimitBreakStatuses.Contains(report.StatusId);

    private static (double At, string Seat) Landing(TestClient client, Dictionary<string, string> row)
    {
        var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
        return (at, client.NameAt(uint.Parse(row["actor"], NumberStyles.HexNumber), at));
    }

    // What the peer's client does for its LB3: tells the party, then runs the action's JobActions
    // effects and the resolve that spends the gauge.
    private static double Press(TestClient client)
    {
        var tank = client.Game.World.Party.Player!;
        var limitBreak = LimitBreakHandler.ActionId(tank.ClassJob, 3);
        var target = (ulong)tank.GameObjectId;
        client.Fake.UserActions.Act(new PlayerAction(PlayerActionStep.Resolved, ActionType.Action, limitBreak, target));
        JobActions.ApplyEffects(tank, limitBreak, target, Random.Shared);
        LimitBreakHandler.Resolve(client.Game.World.Party, tank, limitBreak);
        return client.Fake.Timeline.Clock();
    }
}
