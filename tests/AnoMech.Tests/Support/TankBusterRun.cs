using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// Which client holds which seat: the host one, each peer one. A seat a client holds is a human's
// or its debug bot's; every other seat is one of the host's bots.
internal sealed record TankLayout(string Name, PartyRole HostSeat, IReadOnlyList<PartyRole> PeerSeats)
{
    public static readonly TankLayout HostMtPeerOt = new("host MT + peer OT", MainTank, [OffTank]);
    public static readonly TankLayout HostOtPeerMt = new("host OT + peer MT", OffTank, [MainTank]);
    public static readonly TankLayout PeersHoldTanks = new("host M1 + peers MT, OT", MeleeDpsA, [MainTank, OffTank]);

    public static readonly TankLayout[] All = [HostMtPeerOt, HostOtPeerMt, PeersHoldTanks];

    // Each with a seat no tank sits in, for a human whose hits only the party's mitigation covers:
    // a tank on the host, on a peer for another peer or the host, or both tanks the host's bots.
    public static readonly TankLayout[] WithBystanders =
    [
        new("host MT + peers OT, H1", MainTank, [OffTank, RegenHealer]),
        new("host OT + peers MT, H1", OffTank, [MainTank, RegenHealer]),
        PeersHoldTanks,
        new("host M1 + peer H1", MeleeDpsA, [RegenHealer]),
    ];

    public static readonly PartyRole[] Tanks = [MainTank, OffTank];

    public IEnumerable<PartyRole> Seats => PeerSeats.Prepend(HostSeat);

    public IEnumerable<PartyRole> NonTankSeats => Seats.Where(seat => !Tanks.Contains(seat));

    // 0 is the host, then the peers in order, as MultiplayerRun numbers its clients.
    public int ClientOf(PartyRole seat) => seat == HostSeat ? 0 : 1 + PeerSeats.ToList().IndexOf(seat);

    public string Who(PartyRole seat) => seat == HostSeat ? "host" : PeerSeats.Count > 1 ? $"peer {ClientOf(seat)}" : "peer";
}

// A hit the host judged on mitigation: whether the seat counted as a human's, what its own copy of
// the seat had up, and the statuses that made it.
internal sealed record MitigationCheck(float Time, PartyRole Seat, uint ActionId, DamageKind Kind, float Required, bool Judged,
    float Effective, IReadOnlyList<ushort> Statuses)
{
    public override string ToString()
        => $"{Natives.Data.ActionName(ActionId)} ({ActionId}) on {Seat} at {Time:F2}s: needs {Required:P0} {Kind}, "
           + $"{(Judged ? "judged" : "a bot's, not judged")} with {Effective:P1} [{string.Join(",", Statuses.Where(Mitigation.ByStatusId.ContainsKey))}]";
}

// One session of the tank-buster matrix and everything the host judged in it. Every client plays
// with user actions and tank mitigation on, the plugin's defaults.
internal sealed class TankBusterRun
{
    // A good home line: a peer's report reaches the host a little after its press, never late or lost.
    private static readonly ConnectionProfile PeerLink = TestRelay.Perfect with
    {
        Name = "40 ms each way", UploadLatency = 0.04, DownloadLatency = 0.04,
    };

    public required MultiplayerRun Run { get; init; }
    public required IReadOnlyList<MitigationCheck> Checks { get; init; }
    public IReadOnlyDictionary<PartyRole, StratWalk> Walks { get; init; } = new Dictionary<PartyRole, StratWalk>();
    // The host's run clock when the party's gauge first held three bars; null if it never did.
    public float? LimitBreakReadyAt { get; init; }
    // Presses the player's client refused: dead, stunned, or the module off.
    public IReadOnlyList<string> Refused { get; init; } = [];
    // The party's limit break bars on the host, each time they changed, on its run clock.
    public IReadOnlyList<(float Time, int Bars)> HostGauge { get; init; } = [];

    // Every seat its client holds played by that client's debug bot, recording each one's walk.
    public static TankBusterRun Reference(Type scenarioType, int strat, int seed, TankLayout layout)
    {
        var walks = layout.Seats.ToDictionary(seat => seat, _ => new StratWalk());
        float? limitBreakReadyAt = null;
        var run = Play(scenarioType, strat, seed, layout, humans: new Dictionary<PartyRole, JobId>(), bystanders: [], inputs: [], out var checks,
            client =>
            {
                if (client.IsHost && limitBreakReadyAt is null && client.Game.ActiveScenario is not null
                    && client.Game.World.Party.LimitBreak.FilledBars >= 3)
                    limitBreakReadyAt = client.Game.Events.Elapsed;
                if (walks.TryGetValue(client.Seat, out var walk)) walk.Record(client);
            });
        return new TankBusterRun { Run = run, Checks = checks, Walks = walks, LimitBreakReadyAt = limitBreakReadyAt };
    }

    // `humans` are played by people on the jobs given, walking the reference's walks and pressing
    // `presses`, `bystanders` by people who walk theirs and press nothing; every other seat a client
    // holds is its debug bot.
    public static TankBusterRun Humans(Type scenarioType, int strat, int seed, TankLayout layout, IReadOnlyDictionary<PartyRole, JobId> humans,
        IReadOnlyList<PartyRole> bystanders, TankBusterRun reference, IReadOnlyList<PressPlan.Press> presses)
    {
        var refused = new List<string>();
        var gauge = new List<(float Time, int Bars)>();
        var inputs = presses.Select(p => (At: (double)p.At, Client: layout.ClientOf(p.Seat),
            Input: (Action<TestClient>)(client => Press(client, p.Button, refused)))).ToList();
        var run = Play(scenarioType, strat, seed, layout, humans, bystanders, inputs, out var checks,
            client =>
            {
                if (humans.ContainsKey(client.Seat) || bystanders.Contains(client.Seat)) reference.Walks[client.Seat].Replay(client);
                if (client.IsHost && client.Game.ActiveScenario is not null
                    && (gauge.Count == 0 || gauge[^1].Bars != client.Game.World.Party.LimitBreak.FilledBars))
                    gauge.Add((client.Game.Events.Elapsed, client.Game.World.Party.LimitBreak.FilledBars));
            });
        return new TankBusterRun { Run = run, Checks = checks, Refused = refused, HostGauge = gauge };
    }

    private static MultiplayerRun Play(Type scenarioType, int strat, int seed, TankLayout layout, IReadOnlyDictionary<PartyRole, JobId> humans,
        IReadOnlyList<PartyRole> bystanders, IReadOnlyList<(double At, int Client, Action<TestClient> Input)> inputs, out List<MitigationCheck> checks,
        Action<TestClient> beforeFrame)
    {
        var seen = new List<MitigationCheck>();
        void Seen(MitigationNeed need)
        {
            if (Plugin.Multiplayer is not { IsHost: true } || need.Target is not ISimPartyMember member) return;
            var statuses = need.Target.ActiveStatusSnapshot.Select(s => s.StatusId).ToList();
            seen.Add(new MitigationCheck(Plugin.Game.World.Events.Elapsed, member.Role, need.ActionId, need.Kind, need.Required,
                need.Judged, Mitigation.Effective(statuses, need.Kind), statuses));
        }

        PlayerSeat Seat(PartyRole role) => new(role, BotControlled: !humans.ContainsKey(role) && !bystanders.Contains(role));
        DamageCheck.NeedsMitigation += Seen;
        try
        {
            var run = MultiplayerRun.Play(scenarioType, strat, seed, Seat(layout.HostSeat), layout.PeerSeats.Select(Seat).ToList(),
                peerLink: PeerLink, inputs: inputs, beforeFrame: beforeFrame,
                configure: client =>
                {
                    client.Fake.UserActions.Enabled = true;
                    if (humans.TryGetValue(client.Seat, out var job)) client.Fake.BattleCharas.Player.ClassJob = (byte)job;
                });
            checks = seen;
            return run;
        }
        finally
        {
            DamageCheck.NeedsMitigation -= Seen;
        }
    }

    // The player's own press, as their client takes it (FakeLocalPlayerInput).
    private static void Press(TestClient client, TankButton button, List<string> refused)
    {
        var player = client.Game.Player;
        var target = button.AimsAtAnother
            ? client.Game.World.Party.ActiveMembers().FirstOrDefault(m => !ReferenceEquals(m, player))
            : player;
        if (player is not { Dead: false, ActionsLocked: false } || target is null || !Natives.UserActions.Enabled
            || !client.Fake.PlayerInput.Press(button.ActionId, (ulong)target.GameObjectId))
            refused.Add($"{client.Name} ({client.Seat}) couldn't press {button} at {client.Game.Events.Elapsed:F2}s");
    }
}
