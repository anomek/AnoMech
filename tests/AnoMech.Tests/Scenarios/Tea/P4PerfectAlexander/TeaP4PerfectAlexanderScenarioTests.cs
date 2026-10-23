using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea;
using AnoMech.Scenarios.Tea.P4PerfectAlexander;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Scenario time 0 is 12s before The Final Word.
//
//    12.00  The Final Word; its debuffs at 16.80 (10s). The light beacon waits north of A, the dark
//           beacon on C, the followers on C's north edge.
//    19.15  The first Ordained cast, checked at 23.21; 26.61 the Ordained statuses (5s), and the
//           followers march 20y at 5y/s from 27.41: light ones toward their beacon, dark ones
//           straight away from theirs, a dark one struck down as it passes 24y; the second Ordained
//           check at 34.32.
//    41.48  Optical Sight, hits at 46.57; the other one at 47.56, hits at 52.64. Stacks: MT H1 M1 PR
//           west, OT H2 M2 CA east.
//    68.73  Fate Calibration α; for real from 97.90: Ordained, 100.89 the defamation, 101.43 the
//           shared stack, 101.97 the aggravated hits and the first Sacrament, 102.00 Ordained.
//   113.21  Ordained Capital Punishment's hits at 119.31 / 120.38 / 121.45, Ordained Punishment 125.37.
//   143.61  Fate Calibration β; for real: 183.73 the Ordained statuses and tethers, 190.79 the
//           tethers' checks and the shared stack, 191.79 J Jumps, 197.90 Optical Sight, 202.82 the donut.
//   232.49  Almighty Judgment: tiles lit from 238.60, blasts 246.63 / 248.62 / 250.61.
//   315.06  Temporal Prison, jails from 325.06 every 5s.
public class TeaP4PerfectAlexanderScenarioTests
{
    private static NegativeRun<TeaP4PerfectAlexanderScenario> Run(PartyRole player, Action<TeaP4PerfectAlexanderStateOverrides>? tweak = null)
        => Negative<TeaP4PerfectAlexanderScenario>(player).Overrides<TeaP4PerfectAlexanderStateOverrides>(o => tweak?.Invoke(o));

    private static NegativeRun<TeaP4PerfectAlexanderScenario> Practice(P4Practice practice, PartyRole player,
        Action<TeaP4PerfectAlexanderStateOverrides>? tweak = null)
        => Run(player, o =>
        {
            o.Practice = practice;
            tweak?.Invoke(o);
        });

    private static void FinalWordPinned(TeaP4PerfectAlexanderStateOverrides o)
    {
        o.FinalWordMotionFirst = true;
        o.FinalWord[CasterDps] = FinalWordDebuff.LightBeacon;
        o.FinalWord[ShieldHealer] = FinalWordDebuff.DarkBeacon;
        foreach (var role in new[] { OffTank, MeleeDpsB, PhysRangedDps }) o.FinalWord[role] = FinalWordDebuff.LightFollower;
        foreach (var role in new[] { MainTank, RegenHealer, MeleeDpsA }) o.FinalWord[role] = FinalWordDebuff.DarkFollower;
    }

    [Test]
    public void DiesWalkingIntoWall()
        => Run(MeleeDpsA)
            .TeleportAt(5f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsA);

    // 6y off the dark beacon on C, the march ends 26y from it.
    [Test]
    public void DarkFollowerStartingTooFarFromItsBeaconIsStruckDown()
        => Run(MeleeDpsA, FinalWordPinned)
            .TeleportAt(25.5f, to: new(0, 8))
            .ShouldKill(ActionId.DivineRetributionDark, MeleeDpsA);

    // 6y off its beacon it passes 24y 3.6s into the march, and is struck down there, not at the end.
    [Test]
    public void TheDarkStrikeLandsAsTheMarchCrossesTheLimit()
    {
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsA,
            StopAt = 33f,
            Takeovers = [new Takeover(25.5f, new Vector2(0, 8))],
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
        });
        var death = run.Deaths.Single(d => d.Role == MeleeDpsA);
        Assert.That(death.ActionId, Is.EqualTo(ActionId.DivineRetributionDark), run.ToString);
        Assert.That(death.Time, Is.InRange(31.4f, 31.8f), run.ToString);
    }

    [Test]
    public void LightFollowerStartingNearItsBeaconMarchesIntoIt()
        => Run(PhysRangedDps, FinalWordPinned)
            .TeleportAt(25.5f, to: new(0, 2))
            .ShouldKill(ActionId.DivineRetributionLight, PhysRangedDps);

    // A light follower already on its beacon when the Ordained statuses land dies before the march sets off.
    [Test]
    public void LightFollowerOnItsBeaconDiesBeforeTheMarch()
    {
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = PhysRangedDps,
            StopAt = 27.4f,
            Takeovers = [new Takeover(25.5f, new Vector2(0.1f, -13.4f))],
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
        });
        Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EquivalentTo(new[] { (PhysRangedDps, (uint?)ActionId.DivineRetributionLight) }),
            run.ToString);
    }

    // The light beacon stands still through Ordained Motion; its followers go down with it, before any march.
    [Test]
    public void FollowersGoDownWithTheirBeacon()
    {
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = CasterDps,
            StopAt = 26f,
            Takeovers = [new Takeover(21.5f, null)],
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
        });
        var followers = run.Deaths.Where(d => d.ActionId == ActionId.DivineRetributionLight).Select(d => d.Role);
        Assert.That(followers, Is.EquivalentTo(new[] { OffTank, MeleeDpsB, PhysRangedDps }), run.ToString);
    }

    // Motion first, so the second check is Stillness; a step after the march breaks it.
    [Test]
    public void MovingBeforeOrdainedStillnessKills()
        => Run(RegenHealer, FinalWordPinned)
            .TeleportAt(33.2f, to: new(3, -6))
            .ShouldKill(ActionId.OrdainedStillnessFail, RegenHealer);

    // An instant action 0.8s before the Stillness check breaks it like a step does.
    [Test]
    public void ActingBeforeOrdainedStillnessKills()
        => Run(RegenHealer, FinalWordPinned)
            .At(33.5f, p => p.PressAction())
            .ShouldKill(ActionId.OrdainedStillnessFail, RegenHealer);

    [Test]
    public void AnAutoAttackSwingBeforeOrdainedStillnessKills()
        => Run(RegenHealer, FinalWordPinned)
            .At(33.5f, p => ((SimPlayer)p.Member(RegenHealer)!).CountAutoAttackSwing())
            .ShouldKill(ActionId.OrdainedStillnessFail, RegenHealer);

    // An action let go 2.3s before the check is fine, though its lock runs on into the window.
    [Test]
    public void AnEarlierActionsLockIsFine()
    {
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = RegenHealer,
            StopAt = 36f,
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
            Probe = p =>
            {
                if (p.Crossed(32.0f)) ((ISimPartyMember)p.Member(MainTank)!).UseAction(KnockbackImmunity.ArmsLengthActionId);
            },
        });
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void ABotActingBeforeOrdainedStillnessDies()
        => Run(RegenHealer, FinalWordPinned)
            .At(33.5f, p => ((ISimPartyMember)p.Member(MainTank)!).UseAction(KnockbackImmunity.ArmsLengthActionId))
            .ShouldKill(ActionId.OrdainedStillnessFail, MainTank);

    // The last action 2.4s before the check, as real parties end theirs, is still allowed.
    [Test]
    public void ActingBeforeTheStillnessWindowIsFine()
    {
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = RegenHealer,
            StopAt = 36f,
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
            Probe = p =>
            {
                if (p.Crossed(31.9f)) p.PressAction();
            },
        });
        Assert.That(run.Passed, run.ToString);
    }

    // The main tank as the dark beacon by C stands out of Perfect Alexander's reach.
    [Test]
    public void TheDarkBeaconSpotIsOutOfReachOfTheAutos()
    {
        string? boss = null, mainTank = null;
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = RegenHealer,
            StopAt = 26f,
            Overrides = o =>
            {
                var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                overrides.FinalWordMotionFirst = true;
                overrides.FinalWord[MainTank] = FinalWordDebuff.DarkBeacon;
            },
            Probe = p =>
            {
                boss ??= p.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.PerfectAlexander)?.EntityId.ToString("X8");
                mainTank ??= p.Member(MainTank)?.EntityId.ToString("X8");
            },
        });
        Assert.That(run.Passed, run.ToString);
        var autos = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows
            .Where(r => r["kind"] == "ability" && r["actor"] == boss && r["id"] == ActionId.PerfectAlexanderAttack.ToString() && r["target"] == mainTank)
            .Select(r => float.Parse(r["t"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        Assert.That(autos.Where(t => t < 16f), Is.Not.Empty, "it autos the main tank before the Final Word sends it south");
        Assert.That(autos.Where(t => t is > 21f and < 26f), Is.Empty);
    }

    [Test]
    public void StandingStillThroughOrdainedMotionKills()
        => Run(RegenHealer, FinalWordPinned)
            .FreezeAt(21.5f)
            .ShouldKill(ActionId.OrdainedMotionFail, RegenHealer);

    // The marched followers end up 20y north of their spots.
    [Test]
    public void TheMarchCarriesFollowersTwentyYalms()
    {
        Vector3? before = null, after = null;
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 33f,
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
            Probe = p =>
            {
                if (p.Crossed(27.3f)) before = p.Member(PhysRangedDps)!.Position;
                if (p.Crossed(31.6f)) after = p.Member(PhysRangedDps)!.Position;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(Vector3.Distance(before!.Value, after!.Value), Is.EqualTo(20f).Within(0.3f));
        Assert.That(after!.Value.Z, Is.LessThan(before!.Value.Z - 19f));
    }

    // The player's own march goes out as the step and stop the client walks.
    [Test]
    public void ThePlayersOwnMarchWalksItThere()
    {
        Vector3? before = null, after = null;
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 33f,
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
            Probe = p =>
            {
                if (p.Crossed(27.3f)) before = p.Member(MainTank)!.Position;
                if (p.Crossed(31.6f)) after = p.Member(MainTank)!.Position;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(Vector3.Distance(before!.Value, after!.Value), Is.EqualTo(20f).Within(0.3f));
        Assert.That(after!.Value.Z, Is.LessThan(before!.Value.Z - 19f));
    }

    // The stack first, on the player and M1; alone at the north wall, Faithlessness follows.
    [Test]
    public void StackTakenAloneSetsOffFaithlessness()
        => Run(RegenHealer, o =>
            {
                FinalWordPinned(o);
                o.OpticalSpreadFirst = false;
                o.OpticalStackTargets = [RegenHealer, MeleeDpsB];
            })
            .TeleportAt(44f, to: new(0, -17))
            .ShouldKill(ActionId.CollectiveReprobation, RegenHealer)
            .ShouldKill(ActionId.Faithlessness, AllBut(RegenHealer));

    // Spread first; H1 on the main tank's spread spot takes both hits, and so does the main tank.
    [Test]
    public void OverlappingSpreadsKillBoth()
        => Run(RegenHealer, o =>
            {
                FinalWordPinned(o);
                o.OpticalSpreadFirst = true;
            })
            .TeleportAt(45.5f, to: new(0, -12))
            .ShouldKill(ActionId.IndividualReprobation, RegenHealer, MainTank);

    private static void AlphaPinned(TeaP4PerfectAlexanderStateOverrides o, PartyRole player, AlphaSentence sentence)
    {
        o.Practice = P4Practice.FateCalibrationAlpha;
        o.AlphaSafeClone = 1;
        o.AlphaFirstMotion = false;
        o.AlphaSecondMotion = false;
        o.Alpha[player] = sentence;
    }

    // With the second Alexander safe the shared stack waits at (4.3,17.1); an aggravated player in it
    // carries the stack's vulnerability into its own hit.
    [Test]
    public void AggravatedPlayerInTheSharedStackDies()
        => Run(RegenHealer, o => AlphaPinned(o, RegenHealer, AlphaSentence.AggravatedAssault))
            .TeleportAt(99f, to: new(4.3f, 17.6f))
            .ShouldKill(ActionId.PlaintOfSeverity, RegenHealer);

    [Test]
    public void SomeoneInTheDefamationDies()
        => Run(MeleeDpsB, o => AlphaPinned(o, MeleeDpsB, AlphaSentence.Nothing))
            .TeleportAt(99f, to: new(-4f, -12f))
            .ShouldKill(ActionId.PlaintOfObloquy, MeleeDpsB);

    // (0,15) lies in the third Alexander's cross.
    [Test]
    public void StandingInASacramentCrossDies()
        => Run(MeleeDpsB, o => AlphaPinned(o, MeleeDpsB, AlphaSentence.Nothing))
            .TeleportAt(99f, to: new(0f, 15f))
            .ShouldKill(ActionId.Sacrament, MeleeDpsB);

    [Test]
    public void MovingBeforeTheFutureStillnessKills()
        => Run(MeleeDpsB, o => AlphaPinned(o, MeleeDpsB, AlphaSentence.Nothing))
            .TeleportAt(97f, to: new(4f, 16f))
            .ShouldKill(ActionId.OrdainedStillnessFail, MeleeDpsB);

    [Test]
    public void NonTankInCapitalPunishmentDies()
        => Practice(P4Practice.CapitalPunishmentOne, RegenHealer)
            .TeleportAt(118f, to: new(0, -9.3f))
            .ShouldKill(ActionId.OrdainedCapitalPunishmentHit, RegenHealer);

    // The off tank never provokes: Perfect Alexander keeps the main tank through Ordained
    // Punishment's vulnerability, and its next auto kills.
    [Test]
    public void MissedSwapAfterOrdainedPunishmentKillsTheTank()
        => Run(OffTank)
            .FreezeAt(122f)
            .ShouldKill(ActionId.PerfectAlexanderAttack, MainTank);

    [Test]
    public void StandingNextToOrdainedPunishmentDies()
        => Practice(P4Practice.CapitalPunishmentOne, RegenHealer)
            .TeleportAt(123.5f, to: new(-10f, -4.5f))
            .ShouldKill(ActionId.OrdainedPunishment, RegenHealer);

    private static void BetaPinned(TeaP4PerfectAlexanderStateOverrides o)
    {
        o.Practice = P4Practice.FateCalibrationBeta;
        o.BetaOpticalStack = true;
        o.BetaOpticalClone = 2;
        o.BetaDonutClone = 0;
        o.Beta[MainTank] = BetaRole.LightFar;
        o.Beta[RegenHealer] = BetaRole.DarkFar;
        o.Beta[ShieldHealer] = BetaRole.LightNear;
        o.Beta[MeleeDpsA] = BetaRole.DarkNear;
        o.Beta[OffTank] = BetaRole.LightShared;
        o.Beta[CasterDps] = BetaRole.DarkUntethered;
        o.Beta[MeleeDpsB] = BetaRole.LightBeacon;
        o.Beta[PhysRangedDps] = BetaRole.DarkBeacon;
    }

    [Test]
    public void HouseArrestPartnersApartBothDie()
        => Run(ShieldHealer, BetaPinned)
            .TeleportAt(189.5f, to: new(0, -5))
            .ShouldKill(ActionId.PlaintOfSurety, ShieldHealer, MeleeDpsA);

    // Next to the dark beacon, the east bait.
    [Test]
    public void StandingInSomeoneElsesJJumpDies()
        => Run(MainTank, BetaPinned)
            .TeleportAt(191f, to: new(13.5f, -1f))
            .ShouldKill(ActionId.JJump, MainTank);

    [Test]
    public void StayingOutsideRadiantSacramentDies()
        => Run(MainTank, BetaPinned)
            .TeleportAt(197.95f, to: new(-15f, 0f))
            .ShouldKill(ActionId.RadiantSacrament, MainTank);

    // The centre's set first, waymark 3's last: start on 3, step to the centre once the first set
    // goes off. Frozen on 3, the third blast lands on the player.
    [Test]
    public void StayingOnTheStartingTileDies()
        => Practice(P4Practice.AlmightyJudgmentOne, RegenHealer, o => o.JudgmentOrders[0] = [0, 1, 2])
            .FreezeAt(246.7f)
            .ShouldKill(ActionId.AlmightyJudgmentBlast, RegenHealer);

    [Test]
    public void SteppingDiagonallyIntoTheSecondSetDies()
        => Practice(P4Practice.AlmightyJudgmentOne, RegenHealer, o => o.JudgmentOrders[0] = [0, 1, 2])
            .TeleportAt(247f, to: new(8, 8))
            .ShouldKill(ActionId.AlmightyJudgmentBlast, RegenHealer);

    // The first jail (north-east) is H1's; frozen in the middle, it lands on nobody.
    [Test]
    public void AJailLandingOnNobodyEndsTheFight()
        => Practice(P4Practice.TemporalPrison, RegenHealer)
            .FreezeAt(318f)
            .ShouldKill(ActionId.TemporalPrison, PerRole.All);

    private static IEnumerable<TestCaseData> Practices()
        => Enum.GetValues<P4Practice>().Where(p => p != P4Practice.WholePhase).Select(p => new TestCaseData(p).SetArgDisplayNames(p.ToString()));

    // The second clone's Optical Sight mirrors the projections; the party keeps its spots, and the
    // south bait's jumper is the third clone.
    [TestCase(0, 0)]
    [TestCase(1, 2)]
    [TestCase(3, 3)]
    public void SecondCloneOpticalSightClears(int bait, int donut)
    {
        var state = StateWith(o =>
        {
            o.BetaOpticalClone = 1;
            o.BetaDonutClone = bait;
        });
        Assert.That(state.BetaDonutClone, Is.EqualTo(donut));
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                Overrides = o =>
                {
                    var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                    overrides.Practice = P4Practice.FateCalibrationBeta;
                    overrides.BetaOpticalClone = 1;
                    overrides.BetaDonutClone = bait;
                },
            });
            Assert.That(run.Passed, run.ToString);
        }
    }

    // SunnyDay runs only the whole phase.
    [TestCaseSource(nameof(Practices))]
    public void EachPracticeClearsWithTheStrat(P4Practice practice)
    {
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed,
                new ScenarioRunOptions { Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = practice });
            Assert.That(run.Passed, run.ToString);
        }
    }

    // The jails fill in order with the slowest kill, and the last player stays free.
    [Test]
    public void SlowestKillReachesTheSixthJail()
    {
        var jailed = new List<PartyRole>();
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 5, new ScenarioRunOptions
        {
            Overrides = o =>
            {
                var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                overrides.Practice = P4Practice.TemporalPrison;
            },
            Probe = p =>
            {
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member && member.HasStatus(StatusId.TemporalPrison) && !jailed.Contains(role)) jailed.Add(role);
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(jailed, Is.EqualTo(TeaP4PerfectAlexanderAi.Prisoners.Take(6)));
    }

    // Its HP stays full until it dies, at the slowest real kill.
    [Test]
    public void PerfectAlexanderDiesAtFullHpAtTheSlowestKill()
    {
        var lowestHpBefore = 1f;
        float? diedAt = null;
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 3, new ScenarioRunOptions
        {
            Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = P4Practice.TemporalPrison,
            Probe = p =>
            {
                if (p.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.PerfectAlexander) is not { } boss) return;
                if (boss.HpFraction > 0f) lowestHpBefore = MathF.Min(lowestHpBefore, boss.HpFraction);
                else diedAt ??= p.Time;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(lowestHpBefore, Is.EqualTo(1f));
        Assert.That(diedAt, Is.EqualTo(353.8f).Within(0.05f));
    }

    // After the first Ordained Punishment the off tank holds Perfect Alexander, and the main tank
    // still carries its vulnerability.
    [Test]
    public void FateCalibrationBetaStartsWhereThePhaseWouldLeaveIt()
    {
        SimEnemy? target = null;
        var vulnerable = false;
        var hp = 0f;
        var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, 2, new ScenarioRunOptions
        {
            StopAt = 130f,
            Overrides = o =>
            {
                var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                overrides.Practice = P4Practice.FateCalibrationBeta;
            },
            Probe = p =>
            {
                if (!p.Crossed(129f)) return;
                target = p.World.Children.OfType<SimEnemy>().First(e => e.BNpcBaseId == BNpcBaseId.PerfectAlexander);
                vulnerable = p.Member(MainTank)!.HasStatus(StatusId.PhysicalVulnerabilityUp);
                hp = target.HpFraction;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(target, Is.Not.Null);
        Assert.That(vulnerable, Is.True);
        Assert.That(hp, Is.EqualTo(1f));
    }

    [Test]
    public void PracticeStartsOnItsPlaceInTheTrack()
    {
        var scenario = new TeaP4PerfectAlexanderScenario();
        var overrides = (TeaP4PerfectAlexanderStateOverrides)scenario.SettingsOverrides;
        Assert.That(scenario.Phase, Is.SameAs(TeaZone.P4));
        Assert.That(scenario.BgmSecondsAtStart, Is.EqualTo(49.94f).Within(1e-3f));
        overrides.Practice = P4Practice.FateCalibrationBeta;
        Assert.That(scenario.BgmSecondsAtStart, Is.EqualTo(177.94f).Within(1e-3f));
    }

    [Test]
    public void SeatSettingsTheFightCannotProduceAreRefused()
    {
        PerRole.ForceSeats = true;
        try
        {
            var overrides = new TeaP4PerfectAlexanderStateOverrides();
            Assert.That(overrides.Validate().Any, Is.False);
            overrides.FinalWord[MainTank] = FinalWordDebuff.LightBeacon;
            overrides.FinalWord[OffTank] = FinalWordDebuff.LightBeacon;
            overrides.Alpha[MainTank] = AlphaSentence.Defamation;
            overrides.Alpha[RegenHealer] = AlphaSentence.Defamation;
            overrides.Beta[MainTank] = BetaRole.DarkBeacon;
            overrides.Beta[OffTank] = BetaRole.DarkBeacon;
            Assert.That(overrides.Validate().Problems, Has.Count.EqualTo(3));
        }
        finally
        {
            PerRole.ForceSeats = false;
        }
    }

    // The second check of FinalWordPinned, Stillness.
    private const float StillnessCheck = 34.32f;
    // The first, Motion.
    private const float MotionCheck = 23.21f;

    private static ScenarioRun Stillness(PartyRole player, Action<ScenarioProbe> probe, params Takeover[] takeovers)
        => ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = 36f,
            WriteArtifactsOnFailure = false,
            Overrides = o => FinalWordPinned((TeaP4PerfectAlexanderStateOverrides)o),
            Takeovers = takeovers,
            Probe = probe,
        });

    private static (PartyRole, uint?)[] Death(PartyRole role, uint actionId) => [(role, actionId)];

    private static IEnumerable<(PartyRole, uint?)> Deaths(ScenarioRun run) => run.Deaths.Select(d => (d.Role, d.ActionId));

    // The window opens 2.0s before the check: an instant action let go 1.9s before breaks it, one let go
    // 2.1s before doesn't.
    [TestCase(1.9f, true)]
    [TestCase(2.1f, false)]
    public void AnActionLetGoInStillnessWindowKills(float beforeTheCheck, bool dies)
    {
        var run = Stillness(RegenHealer, p => { if (p.Crossed(StillnessCheck - beforeTheCheck)) p.PressAction(); });
        Assert.That(Deaths(run), dies ? Is.EqualTo(Death(RegenHealer, ActionId.OrdainedStillnessFail)) : Is.Empty, run.ToString);
    }

    // Turning on the spot counts as moving: a quarter turn a second before the check breaks it, one
    // 2.2s before doesn't, as a real 177-degree turn 2.18s before passed.
    [TestCase(1.0f, true)]
    [TestCase(2.2f, false)]
    public void TurningOnTheSpotInStillnessWindowKills(float beforeTheCheck, bool dies)
    {
        var run = Stillness(RegenHealer, p =>
        {
            if (p.Crossed(StillnessCheck - beforeTheCheck)) p.Member(RegenHealer)!.SetRotation(p.Member(RegenHealer)!.Rotation + MathF.PI / 2f);
        }, new Takeover(31.8f, null));
        Assert.That(Deaths(run), dies ? Is.EqualTo(Death(RegenHealer, ActionId.OrdainedStillnessFail)) : Is.Empty, run.ToString);
    }

    // Holding a direction keeps the player walking as far as the game can tell, though a wall or a
    // freeze leaves them in place.
    [Test]
    public void HoldingADirectionThroughOrdainedStillnessKills()
    {
        var run = Stillness(RegenHealer, p => { if (p.Time >= StillnessCheck - 1f) p.HoldMovementInput(); }, new Takeover(31.8f, null));
        Assert.That(Deaths(run), Is.EqualTo(Death(RegenHealer, ActionId.OrdainedStillnessFail)), run.ToString);
    }

    // Motion wants a step in its last 0.3s: a player still since 0.4s before the check fails it, one who
    // stopped 0.2s before passes.
    [TestCase(0.4f, true)]
    [TestCase(0.2f, false)]
    public void StoppingBeforeOrdainedMotionKills(float beforeTheCheck, bool dies)
    {
        var run = Stillness(RegenHealer, _ => { }, new Takeover(MotionCheck - beforeTheCheck, null));
        Assert.That(Deaths(run), dies ? Is.EqualTo(Death(RegenHealer, ActionId.OrdainedMotionFail)) : Is.Empty, run.ToString);
    }

    // Every player's client shows each of the four Ordained checks as passed, on any roll: the right
    // one of Motion and Stillness, cast at each of them, and never a fail.
    [Test]
    public void EveryOrdainedCheckShowsEachPlayerTheirPass()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP4PerfectAlexanderState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 104f,
                Probe = p => state ??= LastState(p),
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            uint[] verdicts = [ActionId.OrdainedMotionSuccess, ActionId.OrdainedStillnessSuccess, ActionId.OrdainedMotionFail, ActionId.OrdainedStillnessFail];
            var shownVerdicts = shown.Of("ability").Where(r => verdicts.Contains(uint.Parse(r["id"]))).ToList();
            Assert.Multiple(() =>
            {
                foreach (var (at, motion) in new[]
                         {
                             (MotionCheck, state!.FinalWordMotionFirst), (StillnessCheck, !state.FinalWordMotionFirst),
                             (97.97f, state.AlphaFirstMotion), (102.00f, state.AlphaSecondMotion),
                         })
                {
                    var these = shownVerdicts.Where(r => MathF.Abs(ShownRun.Time(r) - at) < 0.1f).ToList();
                    var pass = motion ? ActionId.OrdainedMotionSuccess : ActionId.OrdainedStillnessSuccess;
                    Assert.That(these.Select(r => r["id"]), Is.All.EqualTo(pass.ToString()), $"seed {seed}: the check at {at}");
                    Assert.That(these.Select(r => r["target"]), Is.EquivalentTo(shown.Seats.Keys), $"seed {seed}: the check at {at}");
                }
                Assert.That(shownVerdicts, Has.Count.EqualTo(4 * PerRole.All.Length), $"seed {seed}: verdicts outside the four checks");
            });
        }
    }

    // On any roll: each Final Word debuff on its rolled player at 16.80s for 10s, turning at 26.61s into
    // the Ordained ones, each follower told to march, and every march let go as the Ordained statuses
    // end at 31.61s.
    [Test]
    public void TheFinalWordsShowOnTheirRolledPlayersAndOrdainTheMarch()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP4PerfectAlexanderState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 33f,
                Probe = p => state ??= LastState(p),
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var what = $"seed {seed}";
            string[] Ids(IEnumerable<PartyRole> roles) => roles.Select(shown.IdOf).ToArray();
            Assert.Multiple(() =>
            {
                foreach (var (debuff, status) in new[]
                         {
                             (FinalWordDebuff.LightBeacon, StatusId.FinalWordContactRegulation), (FinalWordDebuff.LightFollower, StatusId.FinalWordContactProhibition),
                             (FinalWordDebuff.DarkBeacon, StatusId.FinalWordEscapeDetection), (FinalWordDebuff.DarkFollower, StatusId.FinalWordEscapeProhibition),
                         })
                {
                    var gains = shown.Of("status_gain", status).ToList();
                    Assert.That(gains.Select(r => r["actor"]), Is.EquivalentTo(Ids(PerRole.All.Where(r => state!.FinalWordOf(r) == debuff))), $"{what}: {debuff}");
                    Assert.That(gains.Select(ShownRun.Time), Is.All.EqualTo(16.80f).Within(0.05f), $"{what}: {debuff}");
                    Assert.That(gains.Select(r => r["detail"]), Is.All.StartsWith("dur=10.0"), $"{what}: {debuff}");
                    Assert.That(shown.Of("status_lose", status).Select(ShownRun.Time), Is.All.EqualTo(26.61f).Within(0.05f), $"{what}: {debuff} off");
                }
                var light = shown.IdOf(state!.LightBeacon);
                var dark = shown.IdOf(state.DarkBeacon);
                Assert.That(shown.Of("status_gain", StatusId.ContactRegulationOrdained).Select(r => r["actor"]), Is.EqualTo(new[] { light }), what);
                Assert.That(shown.Of("status_gain", StatusId.EscapeDetectionOrdained).Select(r => r["actor"]), Is.EqualTo(new[] { dark }), what);
                Assert.That(shown.Of("status_gain", StatusId.ContactProhibitionOrdained).Select(r => r["actor"]), Is.EquivalentTo(Ids(state.LightFollowers)), $"{what}: the light followers");
                Assert.That(shown.Of("status_gain", StatusId.EscapeProhibitionOrdained).Select(r => r["actor"]), Is.EquivalentTo(Ids(state.DarkFollowers)), $"{what}: the dark followers");
                var marches = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == $"0x{MarchControl.Category:X}").ToList();
                Assert.That(marches.Where(r => r["p"].Length > 0 && ShownRun.Time(r) < 27f).Select(r => (r["actor"], r["p"])),
                    Is.EquivalentTo(Ids(state.LightFollowers).Select(f => (f, $"0x{MarchControl.Contact:X}"))
                        .Concat(Ids(state.DarkFollowers).Select(f => (f, $"0x{MarchControl.Escape:X}")))), $"{what}: told to march");
                Assert.That(marches.Where(r => r["p"].Length == 0).Select(r => r["actor"]), Is.EquivalentTo(Ids(state.LightFollowers.Concat(state.DarkFollowers))),
                    $"{what}: let go");
                Assert.That(marches.Where(r => r["p"].Length == 0).Select(ShownRun.Time), Is.All.EqualTo(31.61f).Within(0.05f), $"{what}: let go");
            });
        }
    }

    // Perfect Alexander keeps his HP on the client until he falls at the slowest kill; only the
    // projections show any loss before.
    [Test]
    public void OnlyTheProjectionsLoseHpBeforePerfectAlexanderFalls()
    {
        var dropped = new Dictionary<uint, float>();
        var shown = ShownRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            Probe = p =>
            {
                var client = (FakeBattleCharas)Natives.BattleCharas;
                foreach (var enemy in p.World.Children.OfType<SimEnemy>())
                    if (client.Find(enemy.EntityId) is { } actor && actor.Health < actor.MaxHealth)
                        dropped.TryAdd(enemy.BNpcBaseId, p.Time);
            },
        });
        Assert.That(shown.Run.Passed, shown.Run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(dropped.Keys, Is.SubsetOf(new[] { BNpcBaseId.FateProjection, BNpcBaseId.PerfectAlexander }));
            if (dropped.TryGetValue(BNpcBaseId.PerfectAlexander, out var fell)) Assert.That(fell, Is.GreaterThanOrEqualTo(TeaP4PerfectAlexanderState.KillAt));
        });
    }

    // On any roll: both Optical Sights' markers, in the rolled order, at 41.48s and 47.56s: the spread's
    // on everyone, the stack's on its two rolled targets.
    [Test]
    public void TheOpticalSightMarkersShowOnTheirPlayers()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP4PerfectAlexanderState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions { StopAt = 54f, Probe = p => state ??= LastState(p) });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var markers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22" && ShownRun.Time(r) is > 40f and < 50f).ToList();
            List<string> At(float t, uint lockon) => markers.Where(r => MathF.Abs(ShownRun.Time(r) - t) < 0.05f && r["p"].StartsWith($"0x{lockon:X},")).Select(r => r["actor"]).ToList();
            var (first, second) = state!.OpticalSpreadFirst ? (41.48f, 47.56f) : (47.56f, 41.48f);
            Assert.Multiple(() =>
            {
                Assert.That(At(first, LockonId.OpticalSightSpread), Is.EquivalentTo(shown.Seats.Keys), $"seed {seed}: the spread's markers");
                Assert.That(At(second, LockonId.OpticalSightStack), Is.EquivalentTo(state.OpticalStackTargets.Select(shown.IdOf)), $"seed {seed}: the stack's markers");
                Assert.That(markers, Has.Count.EqualTo(PerRole.All.Length + state.OpticalStackTargets.Count), $"seed {seed}: markers besides");
            });
        }
    }

    // On any roll: Fate Projection α's projections spawn at 66.98s each where its player stands, in the
    // rolled order, and each is tethered to its player within a second.
    [Test]
    public void EveryAlphaProjectionIsItsPlayersImageTetheredToThem()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP4PerfectAlexanderState? state = null;
            var spots = new Dictionary<PartyRole, Vector2>();
            var shown = ShownRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 69f,
                Probe = p =>
                {
                    state ??= LastState(p);
                    if (p.Crossed(66.98f))
                        foreach (var role in PerRole.All) spots[role] = new(p.Member(role)!.Position.X, p.Member(role)!.Position.Z);
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var projections = shown.Of("spawn", BNpcBaseId.FateProjection).Where(r => ShownRun.Time(r) < 68f).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(projections, Has.Count.EqualTo(state!.AlphaClones.Count), $"seed {seed}");
                for (var k = 0; k < Math.Min(projections.Count, state.AlphaClones.Count); k++)
                {
                    var (projection, owner) = (projections[k], state.AlphaClones[k]);
                    var at = new Vector2(ShownRun.Number(projection, "x") - 100f, ShownRun.Number(projection, "z") - 100f);
                    Assert.That(Vector2.Distance(at, spots[owner]), Is.LessThan(0.05f), $"seed {seed}: projection {k} where {owner} stood");
                    Assert.That(ShownRun.Time(projection), Is.EqualTo(66.98f).Within(0.05f), $"seed {seed}: projection {k}");
                    var tethered = shown.Rows.FirstOrDefault(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x23" && r["p"].Split(',') is var p
                                                               && p[1] == $"0x{TetherId.FateProjection:X}"
                                                               && new[] { r["actor"], p[2][2..] }.Order().SequenceEqual(new[] { shown.IdOf(owner), projection["actor"] }.Order()));
                    Assert.That(tethered is null ? (float?)null : ShownRun.Time(tethered) - ShownRun.Time(projection), Is.InRange(0f, 1f), $"seed {seed}: {owner}'s tether to projection {k}");
                }
            });
        }
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        var source = StateWith(_ => { });
        var message = new AnoMech.Multiplayer.TeaP4PerfectAlexanderAiReplayStateMessage(source.Practice, source.LightBeacon,
            source.DarkBeacon, source.LightFollowers.ToArray(), source.FinalWordMotionFirst, source.OpticalSpreadFirst,
            source.OpticalStackTargets.ToArray(), source.AlphaClones.ToArray(), source.AlphaFirstMotion, source.AlphaSecondMotion,
            source.AlphaSafeClone, source.BetaClones.ToArray(), source.BetaOpticalStack, source.BetaOpticalClone, source.BetaDonutClone,
            source.JudgmentOrders.Select(o => o.ToArray()).ToArray(), source.GraceTargets.ToArray(), source.BotSeed);
        var shadow = TeaP4PerfectAlexanderState.FromNetworkReplay(message.Practice, message.LightBeacon, message.DarkBeacon,
            message.LightFollowers, message.FinalWordMotionFirst, message.OpticalSpreadFirst, message.OpticalStackTargets, message.AlphaClones,
            message.AlphaFirstMotion, message.AlphaSecondMotion, message.AlphaSafeClone, message.BetaClones, message.BetaOpticalStack,
            message.BetaOpticalClone, message.BetaDonutClone, message.JudgmentOrders, message.GraceTargets, message.BotSeed);
        Assert.That(shadow, Is.Not.Null);
        Assert.That(shadow!.DarkFollowers, Is.EquivalentTo(source.DarkFollowers));
        Assert.That(shadow.AlphaClones, Is.EqualTo(source.AlphaClones));
        Assert.That(shadow.BetaClones, Is.EqualTo(source.BetaClones));
        Assert.That(shadow.BetaOpticalClone, Is.EqualTo(source.BetaOpticalClone));
        Assert.That(shadow.BetaDonutClone, Is.EqualTo(source.BetaDonutClone));
        Assert.That(shadow.JudgmentOrders.SelectMany(o => o), Is.EqualTo(source.JudgmentOrders.SelectMany(o => o)));
        Assert.That(shadow.BotSeed, Is.EqualTo(source.BotSeed), "a peer's bots pace their seats as the host's would");
    }

    [Test]
    public void MalformedReplayStateIsRefused()
    {
        PartyRole[] all = [MainTank, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
        TeaP4PerfectAlexanderState? Replay(PartyRole darkBeacon = OffTank, PartyRole[]? lights = null, PartyRole[]? alpha = null,
            int safe = 1, int optical = 2, int donut = 0, int[][]? orders = null)
            => TeaP4PerfectAlexanderState.FromNetworkReplay(P4Practice.WholePhase, MainTank, darkBeacon, lights ?? [RegenHealer, ShieldHealer, MeleeDpsA],
                true, true, [MainTank, CasterDps], alpha ?? all, false, true, safe, all, true, optical, donut, orders ?? [[0, 1, 2], [2, 1, 0]], [MainTank, OffTank],
                botSeed: 7);

        Assert.That(Replay(), Is.Not.Null);
        Assert.That(Replay(darkBeacon: MainTank), Is.Null);
        Assert.That(Replay(lights: [MainTank, ShieldHealer, MeleeDpsA]), Is.Null);
        Assert.That(Replay(alpha: [MainTank, MainTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps]), Is.Null);
        Assert.That(Replay(safe: 3), Is.Null);
        Assert.That(Replay(donut: 2), Is.Null);
        Assert.That(Replay(optical: 1, donut: 2), Is.Not.Null);
        Assert.That(Replay(optical: 1, donut: 1), Is.Null);
        Assert.That(Replay(optical: 3), Is.Null);
        Assert.That(Replay(orders: [[0, 1, 1], [2, 1, 0]]), Is.Null);
    }

    // Real projections walk different paths pull to pull, the Defamation one's included.
    [Test]
    public void AlphaProjectionsWalkPathsDealtPerRun()
    {
        var states = Enumerable.Range(0, 40)
            .Select(seed => new TeaP4PerfectAlexanderState(new Rng(seed), SimParty.Empty, new TeaP4PerfectAlexanderStateOverrides()))
            .ToList();
        Assert.That(states.All(s => s.AlphaPathOrder.Order().SequenceEqual(Enumerable.Range(0, 8))), "each run deals all eight paths once");
        Assert.That(states.Select(s => s.AlphaPathOrder[6]).Distinct().Count(), Is.GreaterThan(4), "the Defamation projection's path");
    }

    // The bots' pacing is a roll of its own, made after every other: forcing the rest still leaves it
    // to the run, pinning it changes no other roll, and only pinning it fixes it.
    [Test]
    public void BotPacingIsRolledPerRunUnlessPinned()
    {
        static void ForceRolls(TeaP4PerfectAlexanderStateOverrides o)
        {
            o.Practice = P4Practice.FateCalibrationBeta;
            o.FinalWordMotionFirst = true;
            o.OpticalSpreadFirst = true;
            o.OpticalStackTargets = [MainTank, OffTank];
            o.AlphaSafeClone = 1;
            o.AlphaFirstMotion = true;
            o.AlphaSecondMotion = false;
            o.BetaOpticalStack = false;
            o.BetaOpticalClone = 2;
            o.BetaDonutClone = 0;
            o.JudgmentOrders[0] = [0, 1, 2];
            o.JudgmentOrders[1] = [2, 1, 0];
            o.GraceTargets = [MainTank, OffTank];
        }
        TeaP4PerfectAlexanderState Roll(int seed, int? botSeed)
        {
            var overrides = new TeaP4PerfectAlexanderStateOverrides();
            ForceRolls(overrides);
            overrides.BotSeed = botSeed;
            return new TeaP4PerfectAlexanderState(new Rng(seed), SimParty.Empty, overrides);
        }

        Assert.That(Enumerable.Range(0, 20).Select(seed => Roll(seed, null).BotSeed).Distinct().Count(), Is.GreaterThan(15));
        Assert.That(Enumerable.Range(0, 5).Select(seed => Roll(seed, 42).BotSeed), Is.All.EqualTo(42));
        foreach (var seed in Enumerable.Range(0, 5))
        {
            var (rolled, pinned) = (Roll(seed, null), Roll(seed, 42));
            Assert.That(pinned.AlphaClones, Is.EqualTo(rolled.AlphaClones));
            Assert.That(pinned.BetaClones, Is.EqualTo(rolled.BetaClones));
            Assert.That(pinned.AlphaPathOrder, Is.EqualTo(rolled.AlphaPathOrder));
            Assert.That(pinned.LightFollowers, Is.EqualTo(rolled.LightFollowers));
        }
    }

    // Bots go around Perfect Alexander's middle rather than through it; they only stop in it where their
    // spot is (a stack there). β's march goes where it carries them.
    [TestCase(P4Practice.FateCalibrationAlpha, 69.8f, 108.4f)]
    [TestCase(P4Practice.FateCalibrationBeta, 144.75f, 208.4f)]
    public void BotsWalkAroundPerfectAlexandersMiddle(P4Practice practice, float from, float until)
    {
        const float marchFrom = 184.5f, marchUntil = 188.8f;
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var visits = new Dictionary<PartyRole, (float Since, float Stood, Vector2 Last)>();
            var passes = new List<string>();
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = until,
                Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = practice,
                Probe = p =>
                {
                    if (p.Time < from) return;
                    foreach (var role in PerRole.All)
                    {
                        if (p.Member(role) is not { } member) continue;
                        var at = Flat(member.Position);
                        var inside = at.Length() < 3f;
                        if (visits.TryGetValue(role, out var visit))
                        {
                            if (inside)
                                visits[role] = (visit.Since, visit.Stood + (Vector2.Distance(at, visit.Last) < 0.01f ? ScenarioRun.FrameSeconds : 0f), at);
                            else
                            {
                                visits.Remove(role);
                                if (visit.Since > from + 0.1f && visit.Stood < 1f && visit.Since is not (>= marchFrom and <= marchUntil))
                                    passes.Add($"{role} at {visit.Since:F1}");
                            }
                        }
                        else if (inside) visits[role] = (p.Time, 0f, at);
                    }
                },
            });
            Assert.That(run.Passed, run.ToString);
            Assert.That(passes, Is.Empty, $"seed {seed}: bots that walked through the middle");
        }
    }

    // Through each demo each bot follows its own projection a few yalms off rather than on it, keeping
    // around Perfect Alexander's edge and in its reach.
    [TestCase(P4Practice.FateCalibrationAlpha, 69.8f, 85f, 7.5f)]
    [TestCase(P4Practice.FateCalibrationBeta, 144.75f, 153.6f, 8.3f)]
    public void BotsTrailTheirOwnProjectionThroughTheDemo(P4Practice practice, float from, float until, float nearest)
    {
        var behind = new List<float>();
        var followed = new List<bool>();
        var around = new List<float>();
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var gaps = PerRole.All.ToDictionary(r => r, _ => new List<float>());
            var radii = PerRole.All.ToDictionary(r => r, _ => new List<float>());
            var onTop = PerRole.All.ToDictionary(r => r, _ => (Since: float.NaN, Longest: 0f));
            var walked = 0f;
            var first = new Dictionary<PartyRole, Vector2>();
            var last = new Dictionary<PartyRole, Vector2>();
            var projectionLast = new Dictionary<PartyRole, Vector2>();
            var farthest = 0f;
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = until + 0.5f,
                Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = practice,
                Probe = p =>
                {
                    if (p.Time < from || p.Time > until) return;
                    foreach (var role in PerRole.All)
                    {
                        if (p.Member(role) is not { } member) continue;
                        var at = Flat(member.Position);
                        if (last.TryGetValue(role, out var before)) walked += Vector2.Distance(before, at);
                        first.TryAdd(role, at);
                        last[role] = at;
                        farthest = MathF.Max(farthest, at.Length());
                        radii[role].Add(at.Length());
                        if (OwnProjection(p.World, member) is not { } projection) continue;
                        projectionLast[role] = Flat(projection.Position);
                        if (p.Time <= from + 1f) continue;
                        var gap = Vector2.Distance(at, projectionLast[role]);
                        gaps[role].Add(gap);
                        var (since, longest) = onTop[role];
                        onTop[role] = gap >= 1.5f ? (float.NaN, longest)
                            : float.IsNaN(since) ? (p.Time, longest) : (since, MathF.Max(longest, p.Time - since));
                    }
                },
            });
            Assert.That(run.Passed, run.ToString);
            Assert.That(farthest, Is.LessThanOrEqualTo(12.5f), $"seed {seed}: out of Perfect Alexander's reach");
            Assert.That(walked, Is.GreaterThanOrEqualTo(40f), $"seed {seed}: yalms the party walked after its projections");
            foreach (var (role, (_, longest)) in onTop)
                Assert.That(longest, Is.LessThanOrEqualTo(2f), $"seed {seed}: {role} on top of its projection");
            foreach (var (role, ended) in projectionLast.Where(p => Vector2.Distance(first[p.Key], p.Value) >= 8f))
                followed.Add(Vector2.Distance(first[role], ended) - Vector2.Distance(last[role], ended) >= 3f);
            behind.AddRange(gaps.Values.Where(g => g.Count > 0).Select(g => g.Average()));
            around.AddRange(radii.Values.Where(r => r.Count > 0).Select(r => r.Order().ElementAt(r.Count / 2)));
        }
        Assert.That(behind.Order().ElementAt(behind.Count / 2), Is.InRange(2.5f, 8f), "the party's median distance from its projections");
        Assert.That(followed.Count(f => f), Is.GreaterThanOrEqualTo(followed.Count * 7 / 10), "bots that closed on a projection gone 8y off");
        Assert.That(around.Order().ElementAt(around.Count / 2), Is.InRange(nearest, 10.5f), "the party's median distance from Perfect Alexander's centre");
        Assert.That(around.Min(), Is.GreaterThanOrEqualTo(4.5f), "the bot that stayed closest to Perfect Alexander's centre");
    }

    // The tank not holding Perfect Alexander stands anywhere around its edge, a different spot each
    // run, never in its middle.
    [Test]
    public void TheSpareTankStandsAroundPerfectAlexandersEdge()
    {
        var sides = new HashSet<int>();
        foreach (var seed in Enumerable.Range(1, 2 * TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            foreach (var (practice, spare, time) in new[] { (P4Practice.FateCalibrationAlpha, OffTank, 56f), (P4Practice.FateCalibrationBeta, MainTank, 129f) })
            {
                Vector2? spot = null;
                var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
                {
                    PlayerRole = CasterDps,
                    StopAt = time + 0.5f,
                    Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = practice,
                    Probe = p =>
                    {
                        if (p.Crossed(time)) spot = Flat(p.Member(spare)!.Position);
                    },
                });
                Assert.That(run.Passed, run.ToString);
                Assert.That(spot, Is.Not.Null);
                Assert.That(spot!.Value.Length(), Is.InRange(4.9f, 11f), $"seed {seed}: {spare} at {spot}");
                sides.Add((int)((MathF.Atan2(spot.Value.X, -spot.Value.Y) / MathF.Tau * 8f + 8.5f) % 8f));
            }
        }
        Assert.That(sides.Count, Is.GreaterThanOrEqualTo(5), "sides of Perfect Alexander the spare tank stood on");
    }

    // Fate Calibration α: ranged, healers and tanks leave Perfect Alexander for their spots first,
    // the melee later, before it goes untargetable at 93.72; each run's departures spread over
    // seconds.
    [Test]
    public void FateCalibrationAlphaDeparturesAreStaggeredByRole()
    {
        var melee = new List<float>();
        var others = new List<float>();
        var spreads = new List<float>();
        foreach (var seed in Enumerable.Range(1, 2 * TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var left = new Dictionary<PartyRole, float>();
            var track = PerRole.All.ToDictionary(r => r, _ => new List<(float Time, Vector2 Spot)>());
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 97f,
                Overrides = o =>
                {
                    var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                    overrides.Practice = P4Practice.FateCalibrationAlpha;
                    overrides.AlphaFirstMotion = false;
                },
                Probe = p =>
                {
                    if (p.Time < 85.05f) return;
                    foreach (var role in PerRole.All)
                        if (p.Member(role) is { } member)
                            track[role].Add((p.Time, Flat(member.Position)));
                },
            });
            Assert.That(run.Passed, run.ToString);
            foreach (var role in PerRole.All)
            {
                var path = track[role];
                var spot = path[^1].Spot;
                var start = Vector2.Distance(path[0].Spot, spot);
                var leaving = path.First(s => Vector2.Distance(s.Spot, spot) < start - 1f || Vector2.Distance(s.Spot, spot) < 0.5f).Time;
                left[role] = leaving;
                (role is MeleeDpsA or MeleeDpsB ? melee : others).Add(leaving);
            }
            Assert.That(left.Where(l => l.Key is MeleeDpsA or MeleeDpsB).Select(l => l.Value), Has.All.LessThanOrEqualTo(93.8f), $"seed {seed}: melee leaving after it went untargetable");
            spreads.Add(left.Values.Max() - left.Values.Min());
        }
        var othersMedian = others.Order().ElementAt(others.Count / 2);
        var meleeMedian = melee.Order().ElementAt(melee.Count / 2);
        Assert.That(othersMedian, Is.InRange(86.5f, 90f), "ranged, healers and tanks leave at A+74.5..78");
        Assert.That(meleeMedian, Is.GreaterThanOrEqualTo(othersMedian + 1f), "the melee leave later");
        Assert.That(spreads.Order().ElementAt(spreads.Count / 2), Is.GreaterThanOrEqualTo(2.5f), "seconds between a run's first and last to leave");
    }

    // Ordained Motion in Fate Calibration α: the bots keep stepping about their spots from before the
    // check through it, never still for the 0.3s it watches; Stillness they stand through.
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void BotsKeepMovingThroughOrdainedMotion(bool first, bool second)
    {
        const float firstCheck = 97.97f, secondCheck = 102f;
        var windows = new List<(float From, float Until)>();
        if (first) windows.Add((firstCheck - 2f, first && second ? secondCheck : firstCheck));
        if (second && !first) windows.Add((secondCheck - 2f, secondCheck));
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var track = PerRole.All.ToDictionary(r => r, _ => new List<(float At, Vector3 Position)>());
            var stalls = new List<string>();
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 103.5f,
                Overrides = o =>
                {
                    var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                    overrides.Practice = P4Practice.FateCalibrationAlpha;
                    overrides.AlphaFirstMotion = first;
                    overrides.AlphaSecondMotion = second;
                },
                Probe = p =>
                {
                    foreach (var role in PerRole.All)
                    {
                        if (p.Member(role) is not { } member) continue;
                        var samples = track[role];
                        samples.Add((p.Time, member.Position));
                        samples.RemoveAll(s => s.At < p.Time - 0.3f);
                        if (!windows.Any(w => p.Time >= w.From && p.Time <= w.Until)) continue;
                        if (samples.Max(s => Vector3.Distance(s.Position, member.Position)) < 0.05f) stalls.Add($"{role} at {p.Time:F2}");
                    }
                },
            });
            Assert.That(run.Passed, run.ToString);
            Assert.That(stalls, Is.Empty, $"seed {seed}: bots standing still through Ordained Motion");
        }
    }

    // A bot put off its spot just before Ordained Motion (a walk cut short, a late arrival) walks back
    // and keeps moving through both checks; the aggravated ones keep clear of the shared stack as it
    // comes back.
    [TestCase(1, 12f, 9f)]
    [TestCase(1, 9.5f, 6f)]
    [TestCase(2, 2f, 8f)]
    [TestCase(2, 0f, 4f)]
    public void ABotPutOffItsSpotWalksBackAndKeepsMovingThroughMotion(int safe, float x, float z)
    {
        foreach (var seed in Enumerable.Range(1, 4))
        {
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                PlayerRole = CasterDps,
                StopAt = 103.5f,
                Takeovers = [new Takeover(94.9f, new Vector2(x, z), Bot: MeleeDpsA)],
                Overrides = o =>
                {
                    var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                    overrides.Practice = P4Practice.FateCalibrationAlpha;
                    overrides.AlphaFirstMotion = true;
                    overrides.AlphaSecondMotion = true;
                    overrides.AlphaSafeClone = safe;
                    overrides.Alpha[MeleeDpsA] = AlphaSentence.SharedSentence;
                },
            });
            Assert.That(run.Passed, $"seed {seed}: {run}");
        }
    }

    // Over a lossy link a bot peer plays its seat on its own client: it follows its own projection
    // through the demo and keeps moving through both Ordained Motion checks, short stops included.
    [TestCase(2)]
    [TestCase(3)]
    public void BotPeersTrailAndKeepMovingThroughMotionOverALossyLink(int seed)
    {
        var host = new PlayerSeat(MeleeDpsA);
        var peers = PerRole.All.Where(r => r != MeleeDpsA).Select(r => new PlayerSeat(r)).ToList();
        var samples = new List<(Vector2 At, Vector2? Projection)>();
        var steps = new List<(float Time, Vector2 At)>();
        var demo = Enumerable.Range(0, 61).Select(k => (At: 14.8 + 0.25 * k, Client: peers.Count, Input: (Action<TestClient>)(client =>
        {
            if (client.Game.World.Party.Get(CasterDps) is not { } own) return;
            samples.Add((Flat(own.Position), OwnProjection(client.Game.World, own) is { } projection ? Flat(projection.Position) : null));
        })));
        var checks = Enumerable.Range(0, 161).Select(k => (At: 40.5 + 0.05 * k, Client: peers.Count, Input: (Action<TestClient>)(client =>
        {
            if (client.Game.World.Party.Get(CasterDps) is { } own) steps.Add((client.Game.Events.Elapsed, Flat(own.Position)));
        })));
        var run = MultiplayerRun.Play(typeof(TeaP4PerfectAlexanderScenario), 0, seed, host, peers, peerLink: BadConnections.BadWifiPeer, inputs: demo.Concat(checks),
            configure: client =>
            {
                if (!client.IsHost) return;
                var overrides = (TeaP4PerfectAlexanderStateOverrides)client.Game.Catalog.Scenarios.OfType<TeaP4PerfectAlexanderScenario>().Single().SettingsOverrides;
                overrides.Practice = P4Practice.FateCalibrationAlpha;
                overrides.AlphaFirstMotion = true;
                overrides.AlphaSecondMotion = true;
            });
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.Deaths.Where(d => d.Role == CasterDps), Is.Empty, () => string.Join(", ", run.Deaths.Select(d => $"{d.Role}: {d.Cause} at {d.Time:F1}s")));
        foreach (var check in new[] { 97.97f, 102f })
        {
            var near = steps.Where(s => s.Time >= check - 0.5f && s.Time <= check + 0.1f).ToList();
            Assert.That(near.Zip(near.Skip(1), (a, b) => Vector2.Distance(a.At, b.At)).Sum(), Is.GreaterThanOrEqualTo(0.2f),
                $"yalms the peer's bot moved on its own client around the check at {check}");
        }
        var walked = samples.Zip(samples.Skip(1), (a, b) => Vector2.Distance(a.At, b.At)).Sum();
        var gaps = samples.Where(s => s.Projection is not null).Select(s => Vector2.Distance(s.At, s.Projection!.Value)).ToList();
        Assert.That(walked, Is.GreaterThanOrEqualTo(4f), "yalms the peer's bot walked after its projection");
        Assert.That(gaps, Is.Not.Empty, "the peer's bot never saw its projection");
        Assert.That(gaps.Average(), Is.LessThan(9f), "the peer's bot's distance from its projection");
    }

    // After the march the darks bait the J Jumps at the walls: the dark beacon east, the far one south
    // and the untethered one west, each farthest from the clone that jumps it.
    [Test]
    public void TheDarksBaitTheJJumpsAtTheWall()
    {
        var walls = new Dictionary<BetaRole, Vector2>
        {
            [BetaRole.DarkBeacon] = new(18.4f, 0f),
            [BetaRole.DarkFar] = new(-0.2f, 18.2f),
            [BetaRole.DarkUntethered] = new(-18.3f, 0.2f),
        };
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var baits = new Dictionary<BetaRole, Vector2>();
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 192.5f,
                Overrides = o => ((TeaP4PerfectAlexanderStateOverrides)o).Practice = P4Practice.FateCalibrationBeta,
                Probe = p =>
                {
                    if (!p.Crossed(191.79f) || LastState(p) is not { } state) return;
                    foreach (var part in walls.Keys)
                        if (p.Member(state.BetaHolder(part)) is { } bait)
                            baits[part] = Flat(bait.Position);
                },
            });
            Assert.That(run.Passed, run.ToString);
            foreach (var (part, wall) in walls)
            {
                Assert.That(baits.ContainsKey(part), $"seed {seed}: no {part} at the J Jumps");
                Assert.That(Vector2.Distance(baits[part], wall), Is.LessThanOrEqualTo(1f), $"seed {seed}: {part} at {baits[part]}");
            }
        }
    }

    // NAUR's spread in Fate Calibration β: the light beacon north-west, the darks on their sides,
    // north, north-east, a healer's middle and the inner north spot.
    private static readonly Vector2[] NaurSpreadSpots =
        [new(-11.8f, -9.9f), new(0f, -15.5f), new(10.5f, -10.3f), new(13.9f, 5.4f), new(-0.1f, 14.4f), new(-13.1f, 6f), new(3.2f, -1f), new(-3.4f, -7.3f)];

    private static IEnumerable<TestCaseData> SpreadTakers()
        => from spot in NaurSpreadSpots
           from arrives in new[] { 194f, 196f }
           select new TestCaseData(spot, arrives).SetArgDisplayNames($"{spot.X}_{spot.Y}", $"arrives{arrives}");

    // Whatever their part, a player who stands on any of them, even 1.9s before the hits, finds no bot
    // within a spread's 6y: the bots look where everyone is and keep clear.
    [TestCaseSource(nameof(SpreadTakers))]
    public void BotsSpreadClearOfAPlayerOnAnyNaurSpot(Vector2 spot, float arrives)
    {
        foreach (var part in Enum.GetValues<BetaRole>())
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("SpreadSeeds", 1)))
        {
            var player = part is BetaRole.LightFar or BetaRole.LightShared or BetaRole.DarkNear or BetaRole.DarkFar ? RegenHealer : MeleeDpsA;
            var closest = float.MaxValue;
            var run = ScenarioRun.Execute(typeof(TeaP4PerfectAlexanderScenario), 0, seed, new ScenarioRunOptions
            {
                PlayerRole = player,
                StopAt = 198.6f,
                Takeovers = [new Takeover(arrives, spot)],
                Overrides = o =>
                {
                    var overrides = (TeaP4PerfectAlexanderStateOverrides)o;
                    overrides.Practice = P4Practice.FateCalibrationBeta;
                    overrides.BetaOpticalStack = false;
                    overrides.Beta[player] = part;
                },
                Probe = p =>
                {
                    if (p.Time is < 197.5f or > 197.95f || p.Member(player) is not { } human) return;
                    foreach (var bot in PerRole.All.Where(r => r != player).Select(p.Member).OfType<SimCharacter>())
                        closest = MathF.Min(closest, Vector2.Distance(Flat(human.Position), Flat(bot.Position)));
                },
            });
            Assert.That(run.Passed, $"{part}, seed {seed}: {run}");
            Assert.That(closest, Is.GreaterThan(6f), $"{part}, seed {seed}: a bot by the player");
        }
    }

    private static TeaP4PerfectAlexanderState? LastState(ScenarioProbe p)
        => ((TeaP4PerfectAlexanderScenario)p.Game.Catalog.Scenarios.Single(s => s is TeaP4PerfectAlexanderScenario)).LastState;

    private static SimEnemy? OwnProjection(SimWorld world, SimCharacter member)
        => world.Children.OfType<SimTether>()
            .FirstOrDefault(t => t.IsActive && t.TetherId == TetherId.FateProjection && t.A == member)?.B as SimEnemy;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static TeaP4PerfectAlexanderState StateWith(Action<TeaP4PerfectAlexanderStateOverrides> tweak)
    {
        var overrides = new TeaP4PerfectAlexanderStateOverrides();
        tweak(overrides);
        return new TeaP4PerfectAlexanderState(new Rng(11), SimParty.Empty, overrides);
    }
}
