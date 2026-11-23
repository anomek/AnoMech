using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3FirstDecisiveBattle;
using static AnoMech.Tests.NegativeRun;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

public class UmadP3FirstDecisiveBattleTests
{
    [Test]
    public void BossSpawnsDoNotRotateWithCrystalsAndPreparationHasNoCasts()
    {
        for (var corner = 0; corner < 4; corner++)
        {
            ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                StopAt = 1.9f,
                Overrides = o => ((UmadP3FirstDecisiveBattleStateOverrides)o).WindCorner = corner,
                Probe = p =>
                {
                    var bosses = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().ToArray();
                    var chaos = bosses.Single(b => b.BNpcBaseId == 19508);
                    var exdeath = bosses.Single(b => b.BNpcBaseId == 19509);
                    Assert.That(chaos.Position, Is.EqualTo(new Vector3(-10, 0, 0)));
                    Assert.That(exdeath.Position, Is.EqualTo(new Vector3(10, 0, 0)));
                    Assert.That(chaos.IsCasting || exdeath.IsCasting, Is.False);
                },
            });
        }
    }

    [Test]
    public void RandomRollsCoverAllLayoutsAndUseOneSharedWindDirection()
    {
        var combinations = new HashSet<string>();
        var winds = new HashSet<bool>();
        for (var seed = 0; seed < 1000; seed++)
        {
            var s = new UmadP3FirstDecisiveBattleState(new Rng(seed), new());
            Assert.That(s.Headwinds.Values.Distinct().Count(), Is.EqualTo(1));
            Assert.That(s.Fire.Intersect(s.Water), Is.Empty);
            combinations.Add($"{s.WindCorner}/{s.Mirror}/{s.FireFirst}/{s.FrontFirst}");
            winds.Add(s.Headwinds.Values.First());
        }
        Assert.That(combinations, Has.Count.EqualTo(32));
        Assert.That(winds, Has.Count.EqualTo(2));
    }

    [Test]
    public void EnhancedThunderHasNativeAbsorptionTether()
    {
        var path = Path.GetTempFileName();
        try
        {
            ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions { StopAt = 39f, TimelinePath = path });
            Assert.That(File.ReadAllLines(path).Any(r => r.Contains(",ac,") && r.Contains(",0x23,") && r.Contains("0x40")), Is.True);
        }
        finally { File.Delete(path); }
    }
    [TestCase(PartyRole.OffTank, 19508u, -18f)]
    [TestCase(PartyRole.MainTank, 19509u, 18f)]
    public void HumanTankCanPullBossDuringIdleGap(PartyRole role, uint id, float x)
    {
        Vector3 before = default, after = default;
        ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            PlayerRole = role, StopAt = 18f, Takeovers = [new Takeover(10f, new Vector2(x, 0))],
            Probe = p =>
            {
                var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == id);
                if (p.Crossed(9.9f)) before = boss.Position;
                if (p.Crossed(17.5f)) after = boss.Position;
            },
        });
        Assert.That(Vector3.Distance(before, after), Is.GreaterThan(0.5f));
    }

    [TestCase(PartyRole.MainTank, 19509u, 37.5f, 40f, 43f)]
    [TestCase(PartyRole.MainTank, 19509u, 49f, 50f, 56.5f)]
    [TestCase(PartyRole.OffTank, 19508u, 62f, 63f, 69.2f)]
    public void TankMovementCannotDragBossDuringCastOrImplosion(PartyRole role, uint id, float sample, float move, float end)
    {
        Vector3 before = default, after = default;
        float rotationBefore = 0, rotationAfter = 0;
        ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            PlayerRole = role, StopAt = end + 0.1f, Takeovers = [new Takeover(move, new Vector2(-18, 0))],
            Probe = p =>
            {
                var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == id);
                if (p.Crossed(sample)) { before = boss.Position; rotationBefore = boss.Rotation; }
                if (p.Crossed(end)) { after = boss.Position; rotationAfter = boss.Rotation; }
            },
        });
        Assert.That(Vector3.Distance(before, after), Is.LessThan(0.01f));
        Assert.That(rotationAfter, Is.EqualTo(rotationBefore).Within(0.001f));
    }

    [Test]
    public void FirstBattleIncludesAllEightBlasterAppearancesAndCharges()
    {
        var path = Path.GetTempFileName();
        try
        {
            var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions { TimelinePath = path });
            var rows = File.ReadAllLines(path);
            Assert.That(rows.Count(r => r.Contains(",ability,") && r.Contains(",47844,")), Is.EqualTo(8), "Ultima Blaster charges must belong to the complete first battle");
            Assert.That(rows.Count(r => r.Contains(",ability,") && r.Contains(",47843,")), Is.EqualTo(8));
            foreach (var id in AnoMech.Scenarios.Umad.UmadConstants.LockonId.LimitCutNumbers)
                Assert.That(rows.Any(r => (r.Contains(",ac,") || r.Contains(",ac_self,")) && r.Contains(",0x22,") && r.Contains($"0x{id:X}")), Is.True, $"Missing number marker {id}");
            Assert.That(run.Elapsed, Is.InRange(115f, 118f));
            Assert.That(run.Passed, run.ToString);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void ContinuationKeepsBossesCrystalAndWindAndStopsBeforeSecondBattle()
    {
        var path = Path.GetTempFileName();
        var before = new Dictionary<uint, uint>();
        var after = new Dictionary<uint, uint>();
        float windBefore = 0, windAfter = 0;
        try
        {
            var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                TimelinePath = path,
                Probe = p =>
                {
                    if (!p.Crossed(73.2f) && !p.Crossed(73.6f)) return;
                    var target = p.Time < 73.4f ? before : after;
                    foreach (var id in new uint[] {19504, 19508, 19509})
                    {
                        var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == id);
                        target[id] = boss.EntityId;
                    }
                    var crystal = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEventObject>().Single(o => o.EObjRowId == AnoMech.Scenarios.Umad.UmadConstants.EObjId.WindCrystal);
                    target[0] = (uint)crystal.GetHashCode();
                    var count = Enum.GetValues<PartyRole>().Count(r => p.Member(r)!.HasStatus(1602) || p.Member(r)!.HasStatus(1603));
                    if (p.Time < 73.4f) windBefore = count; else windAfter = count;
                },
            });
            Assert.That(after, Is.EquivalentTo(before));
            Assert.That(windBefore, Is.EqualTo(8)); Assert.That(windAfter, Is.EqualTo(windBefore));
            var rows = File.ReadAllLines(path);
            Assert.That(rows.Count(r => r.Contains(",cast,") && r.Contains(",47891,")), Is.EqualTo(1));
            Assert.That(rows.Where(r => r.Contains(",cast,") && (r.Contains(",49890,") || r.Contains(",49891,"))).All(r => float.Parse(r.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) < 10f), Is.True);
            Assert.That(run.Passed, run.ToString);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void EveryBlasterStartAndDirectionClearsTheCombinedBattle()
    {
        for (var start = 0; start < 8; start++)
        foreach (var clockwise in new[] { false, true })
        {
            var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                Overrides = o => { var s = (UmadP3FirstDecisiveBattleStateOverrides)o; s.BlasterStartSpot = start; s.BlasterClockwise = clockwise; },
            });
            Assert.That(run.Passed, $"start={start} clockwise={clockwise}: {run}");
        }
    }

    [Test]
    public void SidewaysWindDoesNotAddAPhantomCycloneAfterContinuation()
    {
        var path = Path.GetTempFileName();
        try
        {
            ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                StopAt = 94f, TimelinePath = path,
                Probe = p =>
                {
                    if (!p.Crossed(89f)) return;
                    var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == 19509);
                    var member = p.Member(PartyRole.CasterDps)!;
                    var toward = boss.Position - member.Position;
                    member.SetRotation(MathF.Atan2(toward.X, toward.Z) + MathF.PI / 2);
                    member.AddStatus(member.HasStatus(1602) ? (ushort)1602 : (ushort)1603, 100f);
                },
            });
            var id = AnoMech.Scenarios.Umad.UmadConstants.ActionId.Cyclone;
            Assert.That(File.ReadAllLines(path).Count(r => r.Contains(",ability,") && r.Contains($",{id},")), Is.EqualTo(7));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void TanksPullBothBossCentresToArenaCentreAfterGrouping()
    {
        var checkedCentre = false;
        ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            StopAt = 18f,
            Probe = p =>
            {
                if (!p.Crossed(17f)) return;
                checkedCentre = true;
                foreach (var id in new uint[] {19508, 19509})
                {
                    var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == id);
                    Assert.That(boss.Position.Length(), Is.LessThan(0.1f), $"Boss {id} must reach centre, not stop at its hitbox edge");
                }
                Assert.That(p.Member(PartyRole.MainTank)!.Position.Length(), Is.GreaterThan(1f));
                Assert.That(p.Member(PartyRole.OffTank)!.Position.Length(), Is.GreaterThan(1f));
            },
        });
        Assert.That(checkedCentre, Is.True);
    }

    [Test]
    public void SecondThunderSetTargetsMainTankThenOffTank()
    {
        PartyRole? first = null, second = null;
        var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            Probe = p =>
            {
                if (!p.Crossed(111.9f) && !p.Crossed(114.9f)) return;
                var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == 19509);
                var closest = Enum.GetValues<PartyRole>().OrderBy(r => Vector3.DistanceSquared(p.Member(r)!.Position, boss.Position)).First();
                if (p.Time < 113f) first = closest; else second = closest;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(first, Is.EqualTo(PartyRole.MainTank));
        Assert.That(second, Is.EqualTo(PartyRole.OffTank));
    }

    [Test]
    public void FirstThunderSetKeepsMainTankForBothHitsAndHumanInvulnSurvives()
    {
        var checks = 0;
        var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.MainTank, UserActionsEnabled = true, StopAt = 57f,
            Takeovers = [new Takeover(52f, null)],
            Probe = p =>
            {
                if (p.Crossed(52.5f)) p.Member(PartyRole.MainTank)!.AddStatus(82, 8f);
                if (!p.Crossed(53.3f) && !p.Crossed(56.4f)) return;
                var boss = p.World.Children.OfType<AnoMech.Core.SimObjects.SimEnemy>().Single(b => b.BNpcBaseId == 19509);
                var closest = Enum.GetValues<PartyRole>().OrderBy(r => Vector3.DistanceSquared(p.Member(r)!.Position, boss.Position)).First();
                Assert.That(closest, Is.EqualTo(PartyRole.MainTank));
                checks++;
            },
        });
        Assert.That(checks, Is.EqualTo(2));
        Assert.That(run.Passed, run.ToString);
    }

    internal static Type ScenarioType()
    {
        var scenario = ScenarioCatalog.Create().Scenarios.FirstOrDefault(s => s.Name == "First Decisive Battle");
        Assert.That(scenario, Is.Not.Null, "P3 First Decisive Battle is not registered");
        return scenario!.GetType();
    }

    [Test]
    public void YoruhaPracticeIsRegistered()
    {
        var type = ScenarioType();
        var scenario = (IScenario)Activator.CreateInstance(type)!;
        Assert.Multiple(() =>
        {
            Assert.That(scenario.Phase.Name, Is.EqualTo("P3"));
            Assert.That(scenario.AiStrats.Single().Name, Is.EqualTo("Yoruha"));
            Assert.That(scenario.AiStrats.Single().Group, Is.EqualTo("JP"));
        });
    }

    [Test]
    public void EveryReferenceRotationAndMirrorStaysInsideArena()
    {
        for (var corner = 0; corner < 4; corner++)
        foreach (var mirror in new[] { false, true })
        foreach (var fireFirst in new[] { false, true })
        foreach (var frontFirst in new[] { false, true })
        foreach (var headwind in new[] { false, true })
        {
            var escaped = new List<string>();
            var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                Overrides = o =>
                {
                    var s = (UmadP3FirstDecisiveBattleStateOverrides)o;
                    s.WindCorner = corner;
                    s.Mirror = mirror;
                    s.FireFirst = fireFirst;
                    s.FrontFirst = frontFirst;
                    s.Headwind = headwind;
                },
                Probe = p =>
                {
                    foreach (var role in Enum.GetValues<PartyRole>())
                        if (p.Member(role) is { } m && new Vector2(m.Position.X, m.Position.Z).Length() > 20.1f)
                            if (escaped.Count == 0) escaped.Add($"{role} at {p.World.Events.Elapsed:F2}");
                },
            });
            Assert.Multiple(() =>
            {
                Assert.That(run.Passed, $"corner={corner} mirror={mirror} fireFirst={fireFirst} frontFirst={frontFirst}: {run}");
                Assert.That(escaped, Is.Empty, $"corner={corner} mirror={mirror} fireFirst={fireFirst} frontFirst={frontFirst}");
            });
        }
    }

    [Test]
    public void StayingInThunderCircleKills()
        => Negative<UmadP3FirstDecisiveBattleScenario>(PartyRole.CasterDps)
            .Overrides<UmadP3FirstDecisiveBattleStateOverrides>(o => { o.WindCorner = 0; o.Mirror = false; })
            .TeleportAt(42f, new(-10, 10))
            .ShouldKill(47890, PartyRole.CasterDps);

    [Test]
    public void MissingFirstImplosionDodgeKills()
        => Negative<UmadP3FirstDecisiveBattleScenario>(PartyRole.RegenHealer)
            .Overrides<UmadP3FirstDecisiveBattleStateOverrides>(o =>
            {
                o.WindCorner = 0;
                o.Mirror = false;
                o.FrontFirst = true;
            })
            .FreezeAt(59.4f)
            .ShouldKill(AnoMech.Scenarios.Umad.UmadConstants.ActionId.Shockwave, PartyRole.RegenHealer);

    [Test]
    public void FailingToBaitUmbraAwayKillsStack()
        => Negative<UmadP3FirstDecisiveBattleScenario>(PartyRole.PhysRangedDps)
            .Overrides<UmadP3FirstDecisiveBattleStateOverrides>(o => { o.WindCorner = 0; o.Mirror = false; })
            .TeleportAt(77.6f, new(5, -5))
            .ShouldKill(AnoMech.Scenarios.Umad.UmadConstants.ActionId.UmbraSmash, PerRole.All);
    [Test]
    public void WrongVacuumFacingPushesOutsideArena()
    {
        var furthest = 0f;
        ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Overrides = o =>
            {
                var s = (UmadP3FirstDecisiveBattleStateOverrides)o;
                s.WindCorner = 0;
                s.Mirror = false;
            },
            StopAt = 91f,
            Takeovers = [new Takeover(87f, null)],
            Probe = p =>
            {
                var member = p.Member(PartyRole.CasterDps)!;
                if (p.Crossed(88.9f))
                {
                    var boss = new Vector3(10, 0, -10);
                    var look = member.HasStatus(1602) ? boss - member.Position : member.Position - boss;
                    member.SetRotation(MathF.Atan2(look.X, look.Z));
                }
                if (p.Time > 89.5f) furthest = MathF.Max(furthest, new Vector2(member.Position.X, member.Position.Z).Length());
            },
        });
        Assert.That(furthest, Is.GreaterThan(20f));
    }

    [Test]
    public void BotTankLbProtectsHumanDuringFinalCyclones()
    {
        var lbSeen = false;
        var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            StopAt = 96f,
            PlayerRole = PartyRole.CasterDps,
            UserActionsEnabled = true,
            Takeovers = [new Takeover(91.8f, null)],
            Probe = p =>
            {
                if (p.Crossed(92f))
                    lbSeen = new ushort[] { 196, 863, 864, 1931 }.Any(p.Member(PartyRole.CasterDps)!.HasStatus);
            },
        });
        Assert.Multiple(() =>
        {
            Assert.That(lbSeen, Is.True, "Tank LB3 was not applied");
            Assert.That(run.Passed, run.ToString);
        });
    }

    [Test]
    public void MissingTankLbDiesToOverlappingCyclones()
        => Negative<UmadP3FirstDecisiveBattleScenario>(PartyRole.CasterDps)
            .WithMitigationChecks()
            .FreezeAt(91.8f)
            .At(92f, p =>
            {
                foreach (var id in new ushort[] { 196, 863, 864, 1931 })
                    p.Member(PartyRole.CasterDps)!.RemoveStatus(id);
            })
            .ShouldKill(AnoMech.Scenarios.Umad.UmadConstants.ActionId.Cyclone, PartyRole.CasterDps);

    [Test]
    public void OneHundredSeededRollsClear()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var run = ScenarioRun.Execute(ScenarioType(), 0, seed);
            Assert.That(run.Passed, run.ToString);
        }
    }
    [Test]
    public void CrystalBaitTargetsDoNotFeedWindCrystal()
    {
        var prematureCyclone = false;
        var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
        {
            Probe = p =>
            {
                if (p.Time < 82f)
                    prematureCyclone |= Enum.GetValues<PartyRole>().Any(r => p.Member(r)?.HasStatus(1052) == true);
            },
        });
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(prematureCyclone, Is.False, "Crystal main targets do not lose wind and must not trigger Cyclone.");
        });
    }
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(10)]
    [TestCase(42)]
    [TestCase(12345)]
    public void SeededTlb3PracticeClears(int seed)
    {
        var run = ScenarioRun.Execute(ScenarioType(), 0, seed);
        Assert.That(run.Passed, run.ToString);
    }
}


