using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea;
using AnoMech.Scenarios.Tea.P2BjCc;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Tests;

// The P2 bots act on what they see: a human partner off its spot, a late pass, a frozen holder,
// the shield going down, a lossy link; and they never all set off on the same frame.
public class TeaP2BjCcAiTests
{
    private static readonly int[] Seeds = [11, 23, 37, 41, 59, 67];
    private static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
    private static readonly Vector2 OffTankAfterMines = new(1.8f, 11.1f);
    private static readonly Vector2 OffTheMainTankSpot = new(-4.5f, -16.2f);

    private static NisiLetter? NisiOf(SimCharacter member)
        => Enum.GetValues<NisiLetter>().Where(l => member.HasStatus(StatusId.FinalDecreeNisi(l))).Cast<NisiLetter?>().FirstOrDefault();

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static ScenarioRun Run(int strat, int seed, PartyRole player, float stopAt, Action<TeaP2BjCcStateOverrides> overrides,
        Action<ScenarioProbe>? probe = null, Takeover[]? takeovers = null)
        => ScenarioRun.Execute(typeof(TeaP2BjCcScenario), strat, seed, new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = stopAt,
            Overrides = o => overrides((TeaP2BjCcStateOverrides)o),
            Takeovers = takeovers ?? [],
            Probe = probe,
        });

    // The human OT comes off the mines, then steps 3y east, and 2.5y south if still bare 0.95s on:
    // its PR follows it and lands the pass-2 copy.
    [TestCase(0)]
    [TestCase(1)]
    public void AHumanPartnerOffItsSpotStillGetsPassed(int strat)
    {
        foreach (var seed in Seeds)
        {
            float? human = null;
            var bare = false;
            float? copied = null;
            var run = Run(strat, seed, OffTank, 90f, o =>
            {
                o.NisiOnDps = false;
                o.IceTank = MainTank;
            }, p =>
            {
                if (p.Member(OffTank) is not { } ot) return;
                if (human is null && p.Time > 67f && Vector2.Distance(Flat(ot.Position), OffTankAfterMines) < 0.6f)
                {
                    human = p.Time;
                    bare = NisiOf(ot) is null;
                    DebugBotControl.Enabled = false;
                    ot.StopMoving();
                    ot.SetPosition(ot.Position + new Vector3(3f, 0f, 0f));
                }
                if (human is { } since && p.Crossed(since + 0.95f) && NisiOf(ot) is null) ot.SetPosition(ot.Position + new Vector3(0f, 0f, 2.5f));
                if (human is not null && copied is null && NisiOf(ot) is not null) copied = p.Time;
            });
            Assert.That(run.Passed, run.ToString);
            Assert.Multiple(() =>
            {
                Assert.That(human, Is.Not.Null, $"seed {seed}: the OT never came off the mines");
                Assert.That(bare, $"seed {seed}: the OT already held a letter");
                Assert.That(copied, Is.Not.Null.And.LessThan(75.6f), $"seed {seed}");
            });
        }
    }

    // The human M1 reaches its MT for pass 1 only at 43.0s: the MT keeps its spot until the copy lands.
    [TestCase(0)]
    [TestCase(1)]
    public void ASupportWaitsForItsPass(int strat)
    {
        foreach (var seed in Seeds)
        {
            Vector2? spot = null;
            var drift = 0f;
            float? copied = null;
            var run = Run(strat, seed, MeleeDpsA, 46f, o =>
            {
                o.NisiOnDps = true;
                o.FirstLightning[CasterDps] = true;
            }, p =>
            {
                if (p.Member(MainTank) is not { } mt || p.Member(MeleeDpsA) is not { } m1) return;
                if (p.Crossed(39.5f)) spot = Flat(mt.Position);
                if (p.Crossed(43f)) m1.SetPosition(mt.Position + new Vector3(0.3f, 0f, 0f));
                if (copied is null && spot is { } at) drift = MathF.Max(drift, Vector2.Distance(Flat(mt.Position), at));
                if (copied is null && p.Time >= 43f && NisiOf(mt) is not null) copied = p.Time;
            }, [new Takeover(37.5f, null)]);
            Assert.That(run.Passed, run.ToString);
            Assert.Multiple(() =>
            {
                Assert.That(copied, Is.Not.Null.And.LessThan(43.3f), $"seed {seed}");
                Assert.That(drift, Is.LessThan(1f), $"seed {seed}: the MT wandered off before its pass");
            });
        }
    }

    // Letters on the DPS; the human DPS freezes at 95s wherever it stands: while the others pass,
    // no bot showing another letter comes nearer than real players' closest (1.65y).
    [TestCase(0)]
    [TestCase(1)]
    public void BotsKeepClearOfAFrozenHumanHolder(int strat)
    {
        foreach (var player in Dps)
        foreach (var seed in Seeds.Take(3))
        {
            var closest = float.MaxValue;
            var run = Run(strat, seed, player, 104f, o => o.NisiOnDps = true, p =>
            {
                if (p.Time < 95f || p.Member(player) is not { } me || NisiOf(me) is not { } mine) return;
                foreach (var role in PerRole.All)
                    if (role != player && p.Member(role) is { } other && NisiOf(other) is { } theirs && theirs != mine)
                        closest = MathF.Min(closest, Vector2.Distance(Flat(other.Position), Flat(me.Position)));
            }, [new Takeover(95f, null)]);
            Assert.That(run.Deaths, Is.Empty, run.ToString);
            Assert.That(closest, Is.GreaterThanOrEqualTo(1.65f), $"{player} seed {seed}");
        }
    }

    // One roll played twice, the shield breaking at 94.3s and then at 97.3s: no DPS takes its pass-3
    // letter before the break, and the first copy moves with it, where a clock-driven pass stays put.
    [TestCase(0)]
    [TestCase(1)]
    public void TheThirdPassStartsFromTheShieldBreak(int strat)
    {
        foreach (var seed in Seeds.Take(3))
        {
            var early = ThirdPassCopies(strat, seed, 94.3f);
            var late = ThirdPassCopies(strat, seed, 97.3f);
            Assert.Multiple(() =>
            {
                Assert.That(early.Min(), Is.GreaterThan(94.3f + 0.3f), $"seed {seed}");
                Assert.That(late.Min(), Is.GreaterThan(97.3f + 0.3f), $"seed {seed}");
                Assert.That(late.Min() - early.Min(), Is.EqualTo(3f).Within(1f), $"seed {seed}");
            });
        }
    }

    private static List<float> ThirdPassCopies(int strat, int seed, float down)
    {
        var received = new List<float>();
        var holding = new Dictionary<PartyRole, NisiLetter?>();
        var run = Run(strat, seed, MainTank, 107f, o =>
        {
            o.NisiOnDps = false;
            o.ShieldBreaksAt = down;
        }, p =>
        {
            foreach (var dps in Dps)
            {
                if (p.Member(dps) is not { } member) continue;
                var letter = NisiOf(member);
                if (p.Time > 93f && letter is not null && holding.GetValueOrDefault(dps) is null) received.Add(p.Time);
                holding[dps] = letter;
            }
        });
        Assert.That(run.Deaths, Is.Empty, run.ToString);
        Assert.That(received, Has.Count.EqualTo(Dps.Length), $"seed {seed}, shield down at {down}s");
        return received;
    }

    // At the group moves the roles set off spread over at least 0.3s, never three on one frame.
    [TestCase(0)]
    [TestCase(1)]
    public void BigMovesNeverStartOnOneFrame(int strat)
    {
        (string What, float From, float Until)[] windows =
            [("edge out", 25.17f, 29f), ("edge back", 32.22f, 34.5f), ("after W/L 1", 53.2f, 56.5f), ("after Gavel", 135.18f, 142f)];
        foreach (var seed in Seeds)
        {
            var starts = windows.Select(_ => new Dictionary<PartyRole, (int Frame, Vector2 From)>()).ToArray();
            var trips = windows.Select(_ => new Dictionary<PartyRole, float>()).ToArray();
            var last = new Dictionary<PartyRole, Vector2>();
            var moving = new Dictionary<PartyRole, bool>();
            var frame = 0;
            var run = Run(strat, seed, MainTank, 143f, _ => { }, p =>
            {
                frame++;
                foreach (var role in PerRole.All)
                {
                    if (p.Member(role) is not { } member) continue;
                    var at = Flat(member.Position);
                    var stepping = last.TryGetValue(role, out var before) && Vector2.Distance(before, at) > 1e-4f;
                    for (var k = 0; k < windows.Length; k++)
                    {
                        if (p.Time < windows[k].From || p.Time > windows[k].Until) continue;
                        if (stepping && !moving.GetValueOrDefault(role) && !starts[k].ContainsKey(role)) starts[k][role] = (frame, before);
                        if (starts[k].TryGetValue(role, out var start)) trips[k][role] = MathF.Max(trips[k].GetValueOrDefault(role), Vector2.Distance(start.From, at));
                    }
                    moving[role] = stepping;
                    last[role] = at;
                }
            });
            Assert.That(run.Passed, run.ToString);
            for (var k = 0; k < windows.Length; k++)
            {
                var big = starts[k].Where(s => trips[k].GetValueOrDefault(s.Key) >= 3f).Select(s => s.Value.Frame).ToList();
                var who = string.Join(", ", starts[k].Where(s => trips[k].GetValueOrDefault(s.Key) >= 3f).Select(s => $"{s.Key}@{s.Value.Frame * ScenarioRun.FrameSeconds:F3}"));
                Assert.That(big, Has.Count.GreaterThanOrEqualTo(3), $"seed {seed} {windows[k].What}");
                Assert.Multiple(() =>
                {
                    Assert.That(big.GroupBy(f => f).Max(g => g.Count()), Is.LessThanOrEqualTo(2), $"seed {seed} {windows[k].What}: three roles set off on one frame ({who})");
                    Assert.That((big.Max() - big.Min()) * ScenarioRun.FrameSeconds, Is.GreaterThanOrEqualTo(0.3f), $"seed {seed} {windows[k].What}");
                });
            }
        }
    }

    // The human MT stands 4.5y west of its N spot: at W/L 1 the caster carries its lightning to it
    // there; at W/L 2 the MT holds the lightning there and the first-water healer comes to it.
    [TestCase(48f, 56f, 0)]
    [TestCase(78f, 86f, 0)]
    [TestCase(78f, 86f, 1)]
    public void TheLightningFindsAPartnerOffItsSpot(float freeze, float stopAt, int strat)
    {
        foreach (var seed in Seeds)
        {
            var run = Run(strat, seed, MainTank, stopAt, o =>
            {
                o.NisiOnDps = true;
                o.FirstLightning[CasterDps] = true;
                o.FirstWaterHealer = ShieldHealer;
                o.IceTank = OffTank;
            }, takeovers: [new Takeover(freeze, OffTheMainTankSpot)]);
            Assert.That(run.Passed, run.ToString);
        }
    }

    // Every seat a bot on its own client, the peers on bad Wi-Fi: each sees the others late and in
    // jumps, and the party still clears without a wrong letter ever touching.
    [TestCase(0, 7)]
    [TestCase(0, 19)]
    [TestCase(1, 19)]
    [TestCase(1, 28)]
    public void TheBotsClearOverALossyLink(int strat, int seed)
    {
        var run = MultiplayerRun.Play(typeof(TeaP2BjCcScenario), strat, seed, new PlayerSeat(MeleeDpsA),
            PerRole.All.Where(r => r != MeleeDpsA).Select(r => new PlayerSeat(r)).ToList(), TestRelay.Perfect, BadConnections.BadWifiPeer);
        Assert.Multiple(() =>
        {
            Assert.That(run.Failure, Is.Null);
            Assert.That(run.Deaths, Is.Empty, () => string.Join(", ", run.Deaths.Select(d => $"{d.Role}: {d.Cause} at {d.Time:F1}s")));
            Assert.That(run.Host.Log.Lines.Where(l => l.Contains("[TeaP2BjCc]") && l.Contains(" touched ")), Is.Empty);
        });
    }

    // Rolls that once wiped an all-bot run: each still clears.
    [TestCase(0, 460820817)]
    [TestCase(0, 935979384)]
    [TestCase(0, 723934152)]
    [TestCase(1, 526472222)]
    [TestCase(0, 2074330564)]
    [TestCase(0, 1822919285)]
    [TestCase(0, 396376638)]
    [TestCase(0, 1632487847)]
    [TestCase(1, 1382553173)]
    [TestCase(1, 221757496)]
    [TestCase(0, 1025691972)]
    [TestCase(0, 1334437604)]
    [TestCase(0, 972149893)]
    [TestCase(0, 529587710)]
    [TestCase(1, 1462524853)]
    [TestCase(1, 2120430983)]
    [TestCase(0, 2016264920)]
    [TestCase(0, 1157417384)]
    [TestCase(0, 1203762027)]
    [TestCase(1, 1859515826)]
    [TestCase(0, 1155794775)]
    [TestCase(0, 958371647)]
    [TestCase(0, 17057079)]
    [TestCase(1, 479940889)]
    [TestCase(1, 2005578551)]
    public void OnceFailingRollsClear(int strat, int seed)
    {
        var run = ScenarioRun.Execute(typeof(TeaP2BjCcScenario), strat, seed);
        Assert.That(run.Passed, run.ToString);
    }
}
