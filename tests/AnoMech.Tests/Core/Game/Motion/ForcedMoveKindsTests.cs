using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

// Every forced move there is, on the kinds of character it can land on, has to move them: each
// motion picks its own delivery, and a kind one of them doesn't handle does nothing without a word.
public class ForcedMoveKindsTests
{
    private const PartyRole Player = PartyRole.MainTank;
    private const PartyRole Bot = PartyRole.OffTank;
    private const float At = 3f;
    private const float Settled = 6f;
    private const float Distance = 10f;
    private const float Tolerance = 1f;
    // Direction 6: the one kind of row the server sends as a push.
    private const uint ServerPushRow = 94;

    private sealed record Kind(Type Move, Action<SimCharacter, Vector3, SimCharacter> Order);

    private static readonly Kind[] Kinds =
    [
        new(typeof(ForcedMove.Knockback), (who, to, _) => who.Knockback(who.Position - Vector3.Normalize(to - who.Position), ServerPushRow)),
        new(typeof(ForcedMove.Push), (who, to, _) => who.Push(MathF.Atan2(to.X - who.Position.X, to.Z - who.Position.Z), Distance, 1f)),
        new(typeof(ForcedMove.PushAway), (who, to, _) => who.Push(who.Position - Vector3.Normalize(to - who.Position), Distance, 1f)),
        new(typeof(ForcedMove.Carry), (who, to, _) => who.CarryTo(to)),
        new(typeof(ForcedMove.IceSlide), (who, to, _) =>
        {
            who.AddStatusParam(StatusId.ThinIce, (int)(Distance * 10), 3f);
            who.IceSlideTo(to, 1.6f);
        }),
        new(typeof(ForcedMove.Teleport), (who, to, _) => who.TeleportTo(new Placement(to, 0f))),
        new(typeof(ForcedMove.Follow), (who, to, target) => who.ForceFollow(target)),
    ];

    [Test]
    public void EveryKindOfForcedMoveIsCovered()
    {
        var all = typeof(ForcedMove).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => t.IsSubclassOf(typeof(ForcedMove)));
        Assert.That(Kinds.Select(k => k.Move), Is.EquivalentTo(all));
    }

    private static IEnumerable<TestCaseData> Cases()
        => from kind in Kinds
           from seat in new[] { Player, Bot }
           select new TestCaseData(kind, seat).SetArgDisplayNames(kind.Move.Name, seat == Player ? "the debug-driven player" : "a bot");

    [TestCaseSource(nameof(Cases))]
    public void AForcedMoveReachesItsCharacter(object kind, PartyRole seat)
    {
        var order = ((Kind)kind).Order;
        Vector3 start = default, to = default, end = default;
        ScenarioRun.Execute(typeof(FruP2DiamondDustScenario), 0, 1, new ScenarioRunOptions
        {
            PlayerRole = Player,
            StopAt = Settled + 0.5f,
            WriteArtifactsOnFailure = false,
            Probe = probe =>
            {
                var who = probe.Member(seat)!;
                if (probe.Crossed(At))
                {
                    start = who.Position;
                    to = start + Vector3.Normalize(new Vector3(-start.X, 0, -start.Z)) * Distance;
                    order(who, to, probe.Member(seat == Player ? Bot : Player)!);
                }
                if (probe.Crossed(Settled)) end = who.Position;
            },
        });
        Assert.That(Vector3.Distance(start, end), Is.GreaterThan(Tolerance), "it never moved");
        if (((Kind)kind).Move != typeof(ForcedMove.Follow) && ((Kind)kind).Move != typeof(ForcedMove.Knockback))
            Assert.That(Vector3.Distance(end, to), Is.LessThan(Tolerance), $"from {start} to {end}, not {to}");
    }
}
