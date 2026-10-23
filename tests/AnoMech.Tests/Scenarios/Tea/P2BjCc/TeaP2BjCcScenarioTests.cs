using System.Globalization;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea;
using AnoMech.Scenarios.Tea.P2BjCc;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: nisi on the supports (MT β, OT δ, H1 γ, H2 α); Final Judgment MT α, OT δ,
// H1 β, H2 γ, M1 γ, M2 α, R δ, C β; first water H2, first lightning C; chakrams N/S; enumerations
// M1 + C; ice on the OT.
//
//   17.95  Nisis on the supports; passes 1 at ~40, 2 at ~69.5-73, 3 at ~97-102, the line-up from ~114.
//   53.20  Water/lightning rounds at 53.20 / 82.90 / 112.65; each new tornado drains at +4.8s.
//   66.67  Mines armed; Shrapnel at 75.90 if one is left.
//   68.72  Enumerations and ice; the Gelid Gaol forms 1.57s after an ice voidzone reaches tornado 1.
//   78.93  Tornado 1 goes off if nothing froze it, tornado 2 at 108.65 if nothing burned it.
//  125.12  Propeller Wind (resolves 130.82); Gavel judges the nisis at 133.13, the statuses clear at 135.18.
//  154.50  Super Jump, landing on the farthest at 158.81; Apocalyptic Ray at 161.17; both robots go
//          down at 195.87, just before their enrage casts go off.
public class TeaP2BjCcScenarioTests
{
    private static readonly (PartyRole Role, NisiLetter Letter)[] PinnedNisi =
        [(MainTank, NisiLetter.Beta), (OffTank, NisiLetter.Delta), (RegenHealer, NisiLetter.Gamma), (ShieldHealer, NisiLetter.Alpha)];

    private static readonly (PartyRole Role, NisiLetter Letter)[] PinnedJudgment =
    [
        (MainTank, NisiLetter.Alpha), (OffTank, NisiLetter.Delta), (RegenHealer, NisiLetter.Beta), (ShieldHealer, NisiLetter.Gamma),
        (MeleeDpsA, NisiLetter.Gamma), (MeleeDpsB, NisiLetter.Alpha), (PhysRangedDps, NisiLetter.Delta), (CasterDps, NisiLetter.Beta),
    ];

    private static void Pin(TeaP2BjCcStateOverrides o)
    {
        o.NisiOnDps = false;
        foreach (var (role, letter) in PinnedNisi) o.Nisi[role] = letter;
        foreach (var (role, letter) in PinnedJudgment) o.Judgment[role] = letter;
        o.FirstWaterHealer = ShieldHealer;
        o.FirstLightning[CasterDps] = true;
        o.ChakramAxis = 0;
        o.Enumeration[MeleeDpsA] = true;
        o.Enumeration[CasterDps] = true;
        o.IceTank = OffTank;
    }

    private static NegativeRun<TeaP2BjCcScenario> BjCc(PartyRole player)
        => Negative<TeaP2BjCcScenario>(player).Overrides<TeaP2BjCcStateOverrides>(Pin);

    // Every damage-over-time tick on a multiple of 3s.
    private static NegativeRun<TeaP2BjCcScenario> OnTheBeat(PartyRole player)
        => Negative<TeaP2BjCcScenario>(player).Overrides<TeaP2BjCcStateOverrides>(o =>
        {
            Pin(o);
            o.TickClock = 0f;
        });

    private static ScenarioRun Run(PartyRole player, float? stopAt, Action<ScenarioProbe>? probe = null, Takeover[]? takeovers = null,
        Action<TeaP2BjCcStateOverrides>? also = null)
        => ScenarioRun.Execute(typeof(TeaP2BjCcScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = stopAt,
            Overrides = o =>
            {
                Pin((TeaP2BjCcStateOverrides)o);
                also?.Invoke((TeaP2BjCcStateOverrides)o);
            },
            Takeovers = takeovers ?? [],
            Probe = probe,
        });

    private static TeaP2BjCcState State(ScenarioProbe p) => p.Game.Catalog.Scenarios.OfType<TeaP2BjCcScenario>().Single().LastState!;

    private static SimEnemy? Robot(ScenarioProbe p, uint bnpcBaseId) => p.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == bnpcBaseId);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static IReadOnlyList<Dictionary<string, string>> Timeline() => ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;

    private static float At(Dictionary<string, string> row) => float.Parse(row["t"], CultureInfo.InvariantCulture);

    private static List<Dictionary<string, string>> Rows(string kind, uint id) => Timeline().Where(r => r["kind"] == kind && r["id"] == id.ToString()).ToList();

    private static string ActorOf(uint bnpcBaseId) => Rows("spawn", bnpcBaseId).First()["actor"];

    // In the OT's ice from 74.2s, the tick at 75s.
    [Test]
    public void StandingInTheIceDiesOnTheNextTick()
        => OnTheBeat(PhysRangedDps)
            .TeleportAt(74.2f, to: new(-12f, 7f))
            .ShouldKill(TheEnvironment, PhysRangedDps);

    // In the OT's missile puddle from 65.6s, the tick at 66s.
    [Test]
    public void StandingInAMissilePuddleDiesOnTheNextTick()
        => OnTheBeat(RegenHealer)
            .TeleportAt(65.6f, to: new(-2.6f, 18f))
            .ShouldKill(TheEnvironment, RegenHealer);

    // In the ice from 75.3s to 77.7s, between the ticks at 75s and 78s: Frostbite stays on, and the
    // tick at 78s kills.
    [Test]
    public void SteppingOutBeforeTheTickStillDies()
        => OnTheBeat(PhysRangedDps)
            .TeleportAt(75.3f, to: new(-12f, 7f))
            .TeleportAt(77.7f, to: new(14.8f, -4.8f))
            .ShouldKill(TheEnvironment, PhysRangedDps);

    [TestCase(0)]
    [TestCase(1)]
    public void PinnedRollClears(int strat)
    {
        var run = ScenarioRun.Execute(typeof(TeaP2BjCcScenario), strat, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            Overrides = o => Pin((TeaP2BjCcStateOverrides)o),
        });
        Assert.That(run.Passed, run.ToString);
    }

    // Every run reaches the last Super Jump, which lands on the phys ranged out east, and Apocalyptic
    // Ray; Brute Justice walks back to the MT, Cruise Chaser's Whirlwinds follow at 167.57 and
    // 175.69, and both robots go down at 195.87, just before their enrage casts go off, never off
    // full HP.
    [TestCase(0)]
    [TestCase(1)]
    public void EveryRunPlaysToTheSlowestKill(int strat)
    {
        float? landingFromBait = null;
        float? pastHitboxFromTank = null;
        var lowestHp = 1f;
        var leftAt = new float?[2];
        var run = ScenarioRun.Execute(typeof(TeaP2BjCcScenario), strat, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            Probe = p =>
            {
                var robots = new[] { BNpcBaseId.BruteJustice, BNpcBaseId.CruiseChaser }.Select(id => Robot(p, id)).ToArray();
                for (var k = 0; k < 2; k++)
                {
                    if (robots[k] is not { } robot) continue;
                    lowestHp = MathF.Min(lowestHp, robot.HpFraction);
                    if (p.Time > 10f && !robot.Targetable) leftAt[k] ??= p.Time;
                }
                if (p.Crossed(159.2f) && robots[0] is { } bruteJustice && p.Member(PhysRangedDps) is { } ranged)
                    landingFromBait = Vector2.Distance(Flat(bruteJustice.Position), Flat(ranged.Position));
                if (p.Crossed(175f) && robots[0] is { } walkedBack && p.Member(MainTank) is { } tank)
                    pastHitboxFromTank = Vector2.Distance(Flat(walkedBack.Position), Flat(tank.Position)) - walkedBack.HitboxRadius;
            },
        });
        Assert.That(run.Passed, run.ToString);
        var (bruteJusticeId, cruiseChaserId) = (ActorOf(BNpcBaseId.BruteJustice), ActorOf(BNpcBaseId.CruiseChaser));
        Assert.Multiple(() =>
        {
            Assert.That(landingFromBait, Is.LessThan(0.5f), run.ToString);
            Assert.That(pastHitboxFromTank, Is.LessThanOrEqualTo(3f));
            Assert.That(lowestHp, Is.EqualTo(1f));
            Assert.That(leftAt, Has.All.InRange(195.87f, 196.1f));
            Assert.That(Rows("cast", ActionId.Whirlwind).Where(r => r["actor"] == cruiseChaserId && At(r) > 160f).Select(At),
                Is.EqualTo(new[] { 167.57f, 175.69f }).Within(0.05f));
            Assert.That(Rows("cast", ActionId.FinalSentenceEnrage).Select(r => r["actor"]), Is.EqualTo(new[] { bruteJusticeId }));
            Assert.That(Rows("cast", ActionId.EternalDarknessP2).Select(r => r["actor"]), Is.EqualTo(new[] { cruiseChaserId }));
            Assert.That(Rows("cast", ActionId.FinalSentenceEnrage).Concat(Rows("cast", ActionId.EternalDarknessP2)).Select(At),
                Has.All.EqualTo(187.95f).Within(0.05f));
            Assert.That(Rows("ability", ActionId.FinalSentenceEnrage).Concat(Rows("ability", ActionId.EternalDarknessP2)), Is.Empty);
            Assert.That(Timeline().Where(r => r["kind"] == "ac" && r["id"] == "0xF" && At(r) >= 195.8f).Select(r => r["actor"]),
                Is.EquivalentTo(new[] { bruteJusticeId, cruiseChaserId }));
        });
    }

    // Gavel, Photon and Double Rocket Punch start as in real pulls, and Gavel's hit lands 1.02s after it
    // goes off.
    [Test]
    public void TheGavelBlockKeepsItsTimes()
    {
        var run = Run(RegenHealer, 150f);
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(Rows("cast", ActionId.Gavel).Select(At), Is.EqualTo(new[] { 128.13f }).Within(0.05f));
            Assert.That(Rows("cast", ActionId.Photon).Select(At).Last(), Is.EqualTo(140.30f).Within(0.05f));
            Assert.That(Rows("cast", ActionId.DoubleRocketPunch).Select(At), Is.EqualTo(new[] { 147.35f }).Within(0.05f));
            Assert.That(Rows("impact", ActionId.Gavel).Select(At).First(), Is.EqualTo(134.15f).Within(0.05f));
        });
    }

    // Hawk Blaster's and Photon's helpers take Cruise Chaser's name, Apocalyptic Ray's Brute Justice's.
    [Test]
    public void EachRobotsHelpersTakeItsName()
    {
        var run = Run(RegenHealer, 167f);
        Assert.That(run.Passed, run.ToString);
        var (cruiseChaser, bruteJustice) = (Natives.Data.BNpcName(BNpcNameId.CruiseChaser), Natives.Data.BNpcName(BNpcNameId.BruteJustice));
        Assert.Multiple(() =>
        {
            Assert.That(Rows("ability", ActionId.HawkBlasterP2).Select(r => r["actor_name"]).Distinct(), Is.EqualTo(new[] { cruiseChaser }));
            Assert.That(Rows("ability", ActionId.PhotonHit).Select(r => r["actor_name"]).Distinct(), Is.EqualTo(new[] { cruiseChaser }));
            Assert.That(Rows("ability", ActionId.ApocalypticRayHit), Has.Count.EqualTo(5));
            Assert.That(Rows("ability", ActionId.ApocalypticRayHit).Select(r => r["actor_name"]).Distinct(), Is.EqualTo(new[] { bruteJustice }));
        });
    }

    [Test]
    public void TheJumpAndTheRayLockAsLongAsTheRealOnes()
    {
        var run = Run(RegenHealer, 170f);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            foreach (var (id, count, animationLock) in new[]
                     {
                         (ActionId.SuperJump, 1, "0.20"), (ActionId.SuperJumpLanding, 1, "2.10"), (ActionId.ApocalypticRay, 1, "7.10"),
                         (ActionId.ApocalypticRayHit, 5, "1.10"),
                     })
            {
                var rows = Rows("ability", id);
                Assert.That(rows, Has.Count.EqualTo(count), $"{id}");
                Assert.That(rows.Select(r => r["anim_lock"]), Is.All.EqualTo(animationLock), $"{id}");
            }
        });
    }

    // The ray goes off at 161.17 and its lock holds Brute Justice on his landing spot until 168.27;
    // then he walks back to the MT.
    [Test]
    public void BruteJusticeHoldsTheRaySpotThroughItsLock()
    {
        Vector2? spot = null;
        var drift = 0f;
        float? movedBy = null, fromTankAtRelease = null, fromTankAfter = null;
        var run = Run(RegenHealer, 170f, p =>
        {
            if (Robot(p, BNpcBaseId.BruteJustice) is not { } bruteJustice || p.Member(MainTank) is not { } tank) return;
            var at = Flat(bruteJustice.Position);
            if (p.Crossed(161.17f)) spot = at;
            if (spot is not { } held) return;
            if (p.Time < 168.2f) drift = MathF.Max(drift, Vector2.Distance(at, held));
            if (p.Crossed(168.2f)) fromTankAtRelease = Vector2.Distance(at, Flat(tank.Position));
            if (!p.Crossed(169.3f)) return;
            movedBy = Vector2.Distance(at, held);
            fromTankAfter = Vector2.Distance(at, Flat(tank.Position));
        });
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(Rows("ability", ActionId.ApocalypticRay).Select(At), Is.EqualTo(new[] { 161.17f }).Within(0.05f));
            Assert.That(drift, Is.LessThanOrEqualTo(0.01f));
            Assert.That(movedBy, Is.GreaterThanOrEqualTo(3f));
            Assert.That(fromTankAfter, Is.LessThan(fromTankAtRelease ?? 0f));
        });
    }

    // The OT stands at the edge from 24s: Cruise Chaser follows it out, then holds still from Optical
    // Sight to Photon's end.
    [Test]
    public void CruiseChaserFollowsItsTankUntilItsCasts()
    {
        float? fromCentre = null;
        Vector2? held = null;
        var drift = 0f;
        Run(OffTank, 42f, p =>
        {
            if (Robot(p, BNpcBaseId.CruiseChaser) is not { } cruiseChaser) return;
            var at = Flat(cruiseChaser.Position);
            if (p.Crossed(29.4f)) fromCentre = at.Length();
            if (p.Time is < 29.5f or > 41.4f) return;
            held ??= at;
            drift = MathF.Max(drift, Vector2.Distance(at, held.Value));
        }, [new Takeover(24f, new Vector2(0f, 17.5f))]);
        Assert.Multiple(() =>
        {
            Assert.That(fromCentre, Is.GreaterThanOrEqualTo(5f));
            Assert.That(drift, Is.LessThanOrEqualTo(0.1f));
        });
    }

    // After pass 1 both halves hold a letter until the supports' first set runs out.
    [Test]
    public void TouchingADifferentNisiKillsBoth()
        => BjCc(MeleeDpsA)
            .FreezeAt(44.9f)
            .At(45f, p => p.Member(MeleeDpsA)!.SetPosition(p.Member(RegenHealer)!.Position))
            .ShouldKill(ActionId.FinalSentence, MeleeDpsA, RegenHealer);

    // Each casts Final Sentence on themselves as they touch, and dies as it lands 0.8s later.
    [Test]
    public void TouchingNisisSentenceThemselves()
    {
        var run = Run(MeleeDpsA, 47f, p =>
        {
            if (p.Crossed(45f)) p.Member(MeleeDpsA)!.SetPosition(p.Member(RegenHealer)!.Position);
        }, [new Takeover(44.9f, null)]);
        var sentences = Rows("ability", ActionId.FinalSentence);
        Assert.Multiple(() =>
        {
            Assert.That(sentences, Has.Count.EqualTo(2));
            Assert.That(sentences.Select(r => r["targets"]), Is.EqualTo(sentences.Select(r => r["actor"])));
            Assert.That(sentences.Select(At), Has.All.EqualTo(45f).Within(0.05f));
            Assert.That(run.Deaths.Select(d => d.Role), Is.EquivalentTo(new[] { MeleeDpsA, RegenHealer }));
            Assert.That(run.Deaths.Select(d => d.Time), Has.All.EqualTo(45.82f).Within(0.05f));
        });
    }

    // After a passed Gavel M2 holds α, and H2 and M1 hold γ on the same slot, until the statuses clear
    // at 135.18; touching them before then still sentences all three.
    [Test]
    public void TouchingADifferentNisiAfterGavelStillKills()
        => BjCc(MeleeDpsB)
            .FreezeAt(133.4f)
            .At(134.0f, p => p.Member(MeleeDpsB)!.SetPosition(p.Member(ShieldHealer)!.Position))
            .ShouldKill(ActionId.FinalSentence, MeleeDpsB, ShieldHealer, MeleeDpsA);

    // M2 never takes H1's γ, so it runs out on H1 and nobody holds it at Gavel.
    [Test]
    public void SkippingAPassWipesAtGavel()
        => BjCc(MeleeDpsB)
            .FreezeAt(39f)
            .ShouldKill(ActionId.FinalSentence, PerRole.All.ToArray());

    // As above: Brute Justice's failure line as Gavel lands, and everyone sentenced as the statuses
    // clear at 135.18.
    [Test]
    public void AFailedGavelSentencesEveryoneAsTheStatusesClear()
    {
        var run = Run(MeleeDpsB, 137f, null, [new Takeover(39f, null)]);
        Assert.Multiple(() =>
        {
            Assert.That(Rows("battle_talk", BattleTalkId.GavelFails).Select(At), Is.EqualTo(new[] { 133.13f }).Within(0.05f));
            Assert.That(Rows("battle_talk", BattleTalkId.GavelFalls), Is.Empty);
            Assert.That(run.Deaths.Select(d => d.Role), Is.EquivalentTo(PerRole.All));
            Assert.That(run.Deaths.Select(d => d.ActionId), Has.All.EqualTo(ActionId.FinalSentence));
            Assert.That(run.Deaths.Select(d => d.Time), Has.All.InRange(135.9f, 136.1f));
        });
    }

    [Test]
    public void LeavingTheMinesWipes()
        => Negative<TeaP2BjCcScenario>(OffTank)
            .Overrides<TeaP2BjCcStateOverrides>(o =>
            {
                Pin(o);
                o.IceTank = MainTank;
            })
            .FreezeAt(66.4f)
            .ShouldKill(ActionId.HiddenMineShrapnel, PerRole.All.ToArray());

    // The OT walks onto its own mine with no mitigation up.
    [Test]
    public void PoppingTheMinesRawKillsTheTank()
        => Negative<TeaP2BjCcScenario>(OffTank)
            .Overrides<TeaP2BjCcStateOverrides>(o =>
            {
                Pin(o);
                o.IceTank = MainTank;
            })
            .WithMitigationChecks()
            .TeleportAt(66.9f, to: new(-2.8f, 12.2f))
            .ShouldKill(ActionId.HiddenMine, OffTank);

    // M1's and C's Enumerations take three each: both go off on nobody.
    [Test]
    public void AnEnumerationOfThreeGoesOffOnNobody()
    {
        var run = Run(RegenHealer, 70f);
        Assert.That(run.Passed, run.ToString);
        Assert.That(Rows("ability", ActionId.Enumeration).Select(r => r["detail"]), Is.EqualTo(new[] { "hits=0", "hits=0" }));
    }

    // The MT joins M1, M2 and H1 in M1's Enumeration.
    [Test]
    public void AFourthInAnEnumerationKillsAllFour()
        => BjCc(RegenHealer)
            .MoveBotAt(68.5f, MainTank, new(-2f, -3.5f))
            .ShouldKill(ActionId.Enumeration, MeleeDpsA, MeleeDpsB, RegenHealer, MainTank);

    // H2 stays on D after the first water stack, on the new tornado as it wakes.
    [Test]
    public void AnyoneLeftOnANewTornadoIsDrained()
        => BjCc(ShieldHealer)
            .FreezeAt(53.6f)
            .ShouldKill(ActionId.DrainageP2, ShieldHealer);

    [Test]
    public void DrainageTakesWhoeverStandsBesideItsTarget()
        => BjCc(ShieldHealer)
            .FreezeAt(53.6f)
            .MoveBotAt(57.5f, RegenHealer, new(-13.75f, 4f))
            .ShouldKill(ActionId.DrainageP2, ShieldHealer, RegenHealer);

    // H2 10.6y from the first tornado as it wakes: nobody in reach, so it never drains.
    [Test]
    public void ANewTornadoSleepsPastItsReach()
    {
        var run = Run(ShieldHealer, 60f, null, [new Takeover(54f, new Vector2(-13.75f, 10.6f))]);
        Assert.That(run.Passed, run.ToString);
        Assert.That(Rows("ability", ActionId.DrainageP2), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void NoBotIsInReachOfASettlingTornado(int strat)
    {
        var tooClose = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            var run = ScenarioRun.Execute(typeof(TeaP2BjCcScenario), strat, Random.Shared.Next(), new ScenarioRunOptions
            {
                StopAt = 118f,
                Probe = p =>
                {
                    foreach (var (k, wakes) in new[] { (0, 58.0f), (1, 87.7f), (2, 117.45f) })
                    {
                        if (!p.Crossed(wakes) || State(p).Objects.Tornadoes.ElementAtOrDefault(k) is not { } rage) continue;
                        foreach (var role in PerRole.All)
                            if (p.Member(role) is { } member && member.IsAlive()
                                && Vector2.Distance(Flat(member.Position), Flat(rage.Position)) is var distance and < 10.5f)
                                tooClose.Add($"{role} {distance:F2}y from tornado {k + 1} at {wakes}");
                    }
                },
            });
            Assert.That(run.Passed, run.ToString);
        }
        Assert.That(tooClose, Is.Empty);
    }

    // H2 holds the first water this far in from D, so its tornado settles there, then leaves; the
    // bots keep out of its reach all the same.
    [TestCase(1f)]
    [TestCase(1.5f)]
    public void NoBotIsInReachOfAFirstTornadoOffItsSpot(float inside)
    {
        float? offSpot = null;
        var run = Run(ShieldHealer, 60f, p =>
        {
            if (p.Crossed(58f) && State(p).Objects.Tornadoes.FirstOrDefault() is { } rage)
                offSpot = Vector2.Distance(Flat(rage.Position), new Vector2(-13.75f, 0f));
        }, [new Takeover(50f, new Vector2(-13.75f + inside, 0f)), new Takeover(53.9f, new Vector2(5.6f, -1.2f))]);
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(offSpot, Is.EqualTo(inside).Within(0.05f));
            Assert.That(Rows("ability", ActionId.DrainageP2), Is.Empty);
        });
    }

    // The third water's holder steps 2.5y in from B just before it goes off, so its tornado settles
    // there; whoever rolls the third water, every bot keeps out of its reach.
    [Test]
    public void NoBotIsInReachOfAThirdTornadoOffItsSpot()
    {
        var b = new Vector2(13.75f, -8.25f);
        var inward = -Vector2.Normalize(b);
        var problems = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            float? offSpot = null;
            var run = Run(RegenHealer, 118f, p =>
            {
                if (p.Crossed(112.5f) && PerRole.All.Select(p.Member).FirstOrDefault(m => m?.HasStatus(StatusId.CompressedWater) == true) is { } holder)
                {
                    var at = b + inward * 2.5f;
                    holder.SetPosition(new Vector3(at.X, 0f, at.Y));
                }
                if (!p.Crossed(117.45f) || State(p).Objects.Tornadoes.ElementAtOrDefault(2) is not { } rage) return;
                offSpot = Vector2.Distance(Flat(rage.Position), b);
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member && Vector2.Distance(Flat(member.Position), Flat(rage.Position)) is var distance and < 10.5f)
                        problems.Add($"{role} {distance:F2}y from it");
            });
            if (!run.Passed) problems.Add(run.ToString());
            if (offSpot is not { } off || MathF.Abs(off - 2.5f) > 0.05f) problems.Add($"the tornado settled {offSpot:F2}y off B");
        }
        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(Rows("ability", ActionId.DrainageP2), Is.Empty);
        });
    }

    // The ice lands too far from the first tornado to freeze it, so it goes off.
    [Test]
    public void IceFarFromTheTornadoLeavesItToGoOff()
        => AssertTornadoGoesOff(0, 78.93f, Run(RegenHealer, 82f, null, [new Takeover(68.5f, new Vector2(12f, 12f), Bot: OffTank)]));

    // The MT baits the Flarethrower square to tornado 2 on Brute Justice's outer side, where nobody
    // stands, so the flames miss it.
    [Test]
    public void AnUnburnedSecondTornadoGoesOff()
        => AssertTornadoGoesOff(1, 108.65f, Run(RegenHealer, 112f, p =>
        {
            if (!p.Crossed(91.7f)) return;
            var from = Flat(Robot(p, BNpcBaseId.BruteJustice)!.Position);
            var toward = Vector2.Normalize(Flat(State(p).Objects.Tornadoes[1].Position) - from);
            var side = new Vector2(toward.Y, -toward.X);
            if (Vector2.Dot(side, from) < 0f) side = -side;
            var spot = from + side * 2f;
            p.Member(MainTank)!.SetPosition(new Vector3(spot.X, 0f, spot.Y));
        }));

    // From the tornado itself, on everyone.
    private static void AssertTornadoGoesOff(int k, float at, ScenarioRun run)
    {
        var goesOff = Rows("ability", ActionId.SevereContamination);
        Assert.Multiple(() =>
        {
            Assert.That(goesOff.Select(r => r["actor"]), Is.EqualTo(new[] { Rows("spawn", BNpcBaseId.LiquidRage)[k]["actor"] }));
            Assert.That(goesOff.Select(r => r["actor_name"]), Is.EqualTo(new[] { Natives.Data.BNpcName(BNpcNameId.LiquidRage) }));
            Assert.That(goesOff.Select(At), Has.All.EqualTo(at).Within(0.05f));
            Assert.That(run.Deaths.Select(d => d.Role), Is.EquivalentTo(PerRole.All), run.ToString);
            Assert.That(run.Deaths.Select(d => d.ActionId), Has.All.EqualTo(ActionId.SevereContamination), run.ToString);
            Assert.That(run.Deaths.Select(d => d.Time), Has.All.InRange(at + 0.35f, at + 0.65f), run.ToString);
        });
    }

    // 12.38y from tornado 1: past the first ice's reach, inside the grown ice's.
    [Test]
    public void TheGrownIceFreezesTheTornadoFrom12_4y()
        => AssertGaolForms(75.12f, [new Takeover(68.3f, new Vector2(-10.76f, -12.01f), Bot: MainTank)], o => o.IceTank = MainTank);

    // 9.8y from tornado 1: inside the first ice's reach.
    [Test]
    public void TheFirstIceFreezesFrom9_8y() => AssertGaolForms(71.22f, [new Takeover(68.3f, new Vector2(-13.75f, 9.8f), Bot: OffTank)]);

    // The OT's drop freezes through the first ice, the MT's through the grown ice.
    [TestCase(OffTank, 71.22f)]
    [TestCase(MainTank, 75.12f)]
    public void TheBotIceTankFreezesTheTornado(PartyRole iceTank, float gaolAt) => AssertGaolForms(gaolAt, [], o => o.IceTank = iceTank);

    private static void AssertGaolForms(float expected, Takeover[] takeovers, Action<TeaP2BjCcStateOverrides>? also = null)
    {
        float? gaolAt = null;
        var tornadoGone = false;
        var run = Run(RegenHealer, 80f, p =>
        {
            if (gaolAt is null && p.World.Children.OfType<SimEnemy>().Any(e => e.BNpcBaseId == BNpcBaseId.GelidGaol)) gaolAt = p.Time;
            if (p.Crossed(79.9f)) tornadoGone = State(p).Objects.Tornadoes.FirstOrDefault() is { } first && (first.FadingOut || !first.IsActive);
        }, takeovers, also);
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(gaolAt, Is.EqualTo(expected).Within(0.05f));
            Assert.That(tornadoGone);
        });
    }

    private static string[] Holding(ShownRun shown, string kind, ushort status) => shown.Of(kind, status).Select(r => r["actor"]).ToArray();

    // On any roll of either strat: each nisi on the player the roll gave it at 17.95s, each Final
    // Judgment on its player at 85.46s, and at Gavel every player's client showing the nisi their
    // judgment asks for, with Brute Justice's line for a passed Gavel.
    [Test]
    public void EveryNisiAndJudgmentShowsOnItsPlayerAndGavelPasses([Values(0, 1)] int strat)
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP2BjCcState? state = null;
            var shownAtGavel = new Dictionary<PartyRole, NisiLetter[]>();
            var shown = ShownRun.Execute(typeof(TeaP2BjCcScenario), strat, seed, new ScenarioRunOptions
            {
                StopAt = 135f,
                Probe = p =>
                {
                    state ??= State(p);
                    if (!p.Crossed(133.1f)) return;
                    foreach (var role in PerRole.All)
                        shownAtGavel[role] = Enum.GetValues<NisiLetter>().Where(l => p.Member(role)!.ShowsStatus(StatusId.FinalDecreeNisi(l))).ToArray();
                },
            });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            Assert.Multiple(() =>
            {
                foreach (var letter in Enum.GetValues<NisiLetter>())
                {
                    var nisi = shown.Of("status_gain", StatusId.FinalDecreeNisi(letter)).Where(r => ShownRun.Time(r) < 18.5f).ToList();
                    Assert.That(nisi.Select(r => r["actor"]), Is.EqualTo(state!.Nisi.Where(n => n.Value == letter).Select(n => shown.IdOf(n.Key))), $"{what}: {letter}'s first nisi");
                    Assert.That(nisi.Select(ShownRun.Time), Is.All.EqualTo(17.95f).Within(0.05f), what);
                    var judgments = shown.Of("status_gain", StatusId.FinalJudgment(letter)).Where(r => ShownRun.Time(r) < 86f).ToList();
                    Assert.That(judgments.Select(r => r["actor"]), Is.EquivalentTo(state.Judgment.Where(j => j.Value == letter).Select(j => shown.IdOf(j.Key))),
                        $"{what}: {letter}'s judgments");
                    Assert.That(judgments.Select(ShownRun.Time), Is.All.EqualTo(85.46f).Within(0.05f), what);
                }
                foreach (var role in PerRole.All)
                    Assert.That(shownAtGavel[role], Is.EqualTo(new[] { state!.Judgment[role] }), $"{what}: {role}'s nisi on their client at Gavel");
                Assert.That(shown.Of("battle_talk").Where(r => ShownRun.Time(r) > 130f).Select(r => r["id"]), Is.EqualTo(new[] { BattleTalkId.GavelFalls.ToString() }), what);
            });
        }
    }

    // On any roll: the first Compressed Water on its healer and the first Compressed Lightning on its
    // DPS at 24.11s, each with its marker.
    [Test]
    public void TheFirstWaterAndLightningMarkTheirHolders()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP2BjCcState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP2BjCcScenario), 0, seed, new ScenarioRunOptions { StopAt = 25f, Probe = p => state ??= State(p) });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var markers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22").Select(r => (r["actor"], r["p"].Split(',')[0])).ToList();
            Assert.Multiple(() =>
            {
                foreach (var (status, lockon, holder) in new[]
                         {
                             (StatusId.CompressedWater, LockonId.CompressedWater, state!.FirstWater), (StatusId.CompressedLightning, LockonId.CompressedLightning, state.FirstLightning),
                         })
                {
                    Assert.That(Holding(shown, "status_gain", status), Is.EqualTo(new[] { shown.IdOf(holder) }), $"seed {seed}: {status}");
                    Assert.That(shown.Of("status_gain", status).Select(ShownRun.Time), Is.All.EqualTo(24.11f).Within(0.05f), $"seed {seed}: {status}");
                    Assert.That(markers, Does.Contain((shown.IdOf(holder), $"0x{lockon:X}")), $"seed {seed}: {lockon}'s marker");
                }
            });
        }
    }

    private static readonly float[] ChakramBearings = [0f, 45f, 90f, 135f];

    // On each axis: both Steam Chakrams stand 21.5y out either end of it, pop in at 25.17s and cast at
    // 26.22s.
    [Test]
    public void TheChakramsStandOnTheirAxis([Values(0, 1, 2, 3)] int axis)
    {
        var shown = ShownRun.Execute(typeof(TeaP2BjCcScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 33f,
            Overrides = o => ((TeaP2BjCcStateOverrides)o).ChakramAxis = axis,
        });
        Assert.That(shown.Run.Passed, shown.Run.ToString);
        var chakrams = shown.Of("spawn", BNpcBaseId.SteamChakram).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(chakrams, Has.Count.EqualTo(2));
            foreach (var end in new[] { 0f, 180f })
            {
                var spot = Geometry.OnCompass(ChakramBearings[axis] + end, 21.5f);
                Assert.That(chakrams.Any(c => Vector2.Distance(new(ShownRun.Number(c, "x") - 100f, ShownRun.Number(c, "z") - 100f), Flat(spot)) < 0.05f), Is.True,
                    $"no chakram at {spot}");
            }
            foreach (var chakram in chakrams)
            {
                Assert.That(shown.Rows.Where(r => r["actor"] == chakram["actor"] && r["kind"] == "ac" && r["id"] == "0x24").Select(ShownRun.Time),
                    Is.EqualTo(new[] { 25.17f }).Within(0.05f), "its pop-in");
                Assert.That(shown.Rows.Where(r => r["actor"] == chakram["actor"] && r["kind"] == "cast" && r["id"] == ActionId.EyeOfTheChakram.ToString()).Select(ShownRun.Time),
                    Is.EqualTo(new[] { 26.22f }).Within(0.05f), "its cast");
            }
        });
    }

    // On any roll: the Enumeration markers on their two DPS and the ice's on its tank at 63.57s, each
    // going off where its player stands at 68.72s.
    [Test]
    public void TheEnumerationsAndTheIceMarkTheirPlayers()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP2BjCcState? state = null;
            var spots = new Dictionary<PartyRole, Vector2>();
            var shown = ShownRun.Execute(typeof(TeaP2BjCcScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 70f,
                Probe = p =>
                {
                    state ??= State(p);
                    if (p.Crossed(68.72f))
                        foreach (var role in PerRole.All) spots[role] = Flat(p.Member(role)!.Position);
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var markers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22").ToList();
            List<string> Marked(uint lockon) => markers.Where(r => r["p"].StartsWith($"0x{lockon:X},")).Select(r => r["actor"]).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(Marked(LockonId.Enumeration), Is.EquivalentTo(state!.Enumerations.Select(shown.IdOf)), $"seed {seed}: the Enumeration markers");
                Assert.That(Marked(LockonId.EarthMissileIce), Is.EqualTo(new[] { shown.IdOf(state.IceTank) }), $"seed {seed}: the ice marker");
                Assert.That(markers.Where(r => r["p"].StartsWith($"0x{LockonId.Enumeration:X},") || r["p"].StartsWith($"0x{LockonId.EarthMissileIce:X},")).Select(ShownRun.Time),
                    Is.All.EqualTo(63.57f).Within(0.05f), $"seed {seed}");
                foreach (var role in state.Enumerations)
                    Assert.That(shown.Of("ability", ActionId.Enumeration).Any(c => Vector2.Distance(new(ShownRun.Number(c, "x") - 100f, ShownRun.Number(c, "z") - 100f), spots[role]) < 0.05f),
                        Is.True, $"seed {seed}: {role}'s Enumeration where they stood");
                Assert.That(shown.Of("ability", ActionId.EarthMissileIce).SelectMany(ShownRun.Hits), Is.EqualTo(new[] { shown.IdOf(state.IceTank) }), $"seed {seed}: the ice on its tank");
            });
        }
    }

    // Brute Justice and Cruise Chaser never lose HP on the client; only the Plasma Shield does.
    [Test]
    public void OnlyThePlasmaShieldLosesHp()
    {
        var dropped = new HashSet<uint>();
        var shown = ShownRun.Execute(typeof(TeaP2BjCcScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            Probe = p =>
            {
                var client = (FakeBattleCharas)Natives.BattleCharas;
                foreach (var enemy in p.World.Children.OfType<SimEnemy>())
                    if (client.Find(enemy.EntityId) is { } actor && actor.Health < actor.MaxHealth)
                        dropped.Add(enemy.BNpcBaseId);
            },
        });
        Assert.That(shown.Run.Passed, shown.Run.ToString);
        Assert.That(dropped, Is.SubsetOf(new[] { BNpcBaseId.PlasmaShield }));
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        TeaP2BjCcState? host = null;
        TeaP2BjCcState? replayed = null;
        ScenarioRun.Execute(typeof(TeaP2BjCcScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 1f,
            Probe = probe =>
            {
                if (!probe.Crossed(0.5f)) return;
                var scenario = probe.Game.Catalog.Scenarios.OfType<TeaP2BjCcScenario>().Single();
                host = scenario.LastState;
                replayed = (TeaP2BjCcState?)scenario.StartReplay(scenario.BuildReplayStateMessage()!, 0, MainTank, probe.World);
            },
        });

        Assert.That(host, Is.Not.Null);
        Assert.That(replayed, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replayed!.NisiOnDps, Is.EqualTo(host!.NisiOnDps));
            Assert.That(replayed.Nisi, Is.EqualTo(host.Nisi));
            Assert.That(replayed.Judgment, Is.EqualTo(host.Judgment));
            Assert.That(replayed.FirstWater, Is.EqualTo(host.FirstWater));
            Assert.That(replayed.FirstLightning, Is.EqualTo(host.FirstLightning));
            Assert.That(replayed.ChakramAxis, Is.EqualTo(host.ChakramAxis));
            Assert.That(replayed.Enumerations, Is.EqualTo(host.Enumerations));
            Assert.That(replayed.IceTank, Is.EqualTo(host.IceTank));
            Assert.That(replayed.BotPacing, Is.EqualTo(host.BotPacing));
        });
    }

    [Test]
    public void ReplayStateRejectsBadRolls()
    {
        int[] nisi = [1, 3, 2, 0, -1, -1, -1, -1];
        int[] judgment = [0, 3, 1, 2, 2, 0, 3, 1];
        PartyRole[] enumerations = [MeleeDpsA, CasterDps];
        Assert.Multiple(() =>
        {
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, judgment, ShieldHealer, CasterDps, 0, enumerations, OffTank, 7), Is.Not.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(true, nisi, judgment, ShieldHealer, CasterDps, 0, enumerations, OffTank, 7), Is.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, [0, 3, 1, 1, 2, 0, 3, 1], ShieldHealer, CasterDps, 0, enumerations, OffTank, 7), Is.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, judgment, MainTank, CasterDps, 0, enumerations, OffTank, 7), Is.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, judgment, ShieldHealer, CasterDps, 4, enumerations, OffTank, 7), Is.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, judgment, ShieldHealer, CasterDps, 0, [MeleeDpsA, MeleeDpsA], OffTank, 7), Is.Null);
            Assert.That(TeaP2BjCcState.FromNetworkReplay(false, nisi, judgment, ShieldHealer, CasterDps, 0, enumerations, MeleeDpsA, 7), Is.Null);
        });
    }
}
