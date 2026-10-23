using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.P1LivingLiquid;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: both tornado Ts open to the north (tornadoes W (-15,0), E (15,0), S (0,15)),
// W and E Pressurize both times with W's orb aimed at Liquid Hand, W tethers Drainage to MT and E to
// OT, Throttle on everyone but the healers, every doll at the median pace. The dolls stand NW (C),
// SW (M1), SE (M2), NE (R).
//
//   10.15  Fluid Swing on MT; Cascade cast 14.27, resolving at 18.27.
//   19.66  Liquid Hand at the centre and the tornadoes; their water kills 2.1s after stepping within 7.5y.
//   29.35  The Hand's tell: Parting with the bosses 15.5y+ apart, resolved at 34.46 (within 7.1y). MT provokes the Hand
//          at 29.85 and has it 1.3s later; OT provokes it back at 40.5.
//   32.44  Tornado proteans cast at whoever is closest to each tornado, resolving at 35.44; at 37.53
//          the unseen ones hit whoever is closest then.
//   32.63  Dolls at 7y and full HP; Exhaust at 38.56, 49.19 and 59.83, a second one inside 5s kills. Hit
//          first at 35.7 (M1, M2), 35.45 (R) and 36.74 (C), each falls under 25% at 42.5, 45.85 and 47.74;
//          M1's shows 29.4% from 41.7 and 20.8% from 42.5.
//          From the first Exhaust a doll within 4.6y of either boss is absorbed, killing everyone at 25%+.
//   41.18  Emboli on W and E, rolling from 46.0; touching one sets off Outburst on everyone.
//   65.79  Living Liquid's proteans, unseen waves at 70.83 and 73.90 with Sluices on the farthest four.
//   78.53  Drainage tethers, resolving at 85.65 on whoever holds each.
//   95.38  Throttle for 10s, cleansed by the healers; Fluid Swing at 132.95, then autos until Living Liquid
//          dies at 136.40, the second set and the orbs fading out with it.
public class TeaP1LivingLiquidScenarioTests
{
    private const ushort Holmgang = 409;

    private static void Pin(TeaP1LivingLiquidStateOverrides o)
    {
        o.FirstTornadoesOpenSide = 0;
        o.SecondTornadoesOpenSide = 0;
        o.FirstCalmTornado = TornadoT.South;
        o.SecondCalmTornado = TornadoT.South;
        o.FirstHandOrbFromEarlier = true;
        o.SecondHandOrbFromEarlier = true;
        o.UntetheredTornado = TornadoT.South;
        o.MainTankHoldsEarlierTether = true;
        o.Throttled = [MainTank, OffTank, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
        o.DollRoll = new DollRoll(0.5f, 0.5f, 0.5f, 0.5f);
    }

    private static NegativeRun<TeaP1LivingLiquidScenario> LivingLiquid(PartyRole player)
        => Negative<TeaP1LivingLiquidScenario>(player).Overrides<TeaP1LivingLiquidStateOverrides>(Pin);

    private static ScenarioRun Run(PartyRole player, float stopAt, Action<ScenarioProbe>? probe = null, params Takeover[] takeovers)
        => Execute(player, stopAt, probe, takeovers, writeArtifactsOnFailure: true);

    // A death is the expected outcome, so it leaves no failure artifacts.
    private static ScenarioRun RunToDeath(PartyRole player, float stopAt, Action<ScenarioProbe>? probe = null, params Takeover[] takeovers)
        => Execute(player, stopAt, probe, takeovers, writeArtifactsOnFailure: false);

    private static ScenarioRun Execute(PartyRole player, float stopAt, Action<ScenarioProbe>? probe, Takeover[] takeovers, bool writeArtifactsOnFailure)
        => ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = stopAt,
            Overrides = o => Pin((TeaP1LivingLiquidStateOverrides)o),
            Takeovers = takeovers,
            Probe = probe,
            WriteArtifactsOnFailure = writeArtifactsOnFailure,
        });

    private static SimEnemy Enemy(ScenarioProbe p, uint bnpcBaseId)
        => p.World.Children.OfType<SimEnemy>().First(e => e.IsActive && e.BNpcBaseId == bnpcBaseId);

    // What the run sent the client for this actor, oldest first.
    private static IEnumerable<Dictionary<string, string>> Sent(SimCharacter actor, string kind, string? id = null) => Sent(Id(actor), kind, id);

    private static IEnumerable<Dictionary<string, string>> Sent(string actor, string kind, string? id = null)
        => ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows
            .Where(r => r["kind"] == kind && r["actor"] == actor && (id is null || r["id"] == id));

    // Who the actor's last action before now went at.
    private static string LastActionTarget(SimCharacter actor) => Sent(actor, "ability").LastOrDefault()?["target"] ?? "";

    private static string Id(SimCharacter? actor) => actor?.EntityId.ToString("X8") ?? "";

    private static float At(Dictionary<string, string> row) => float.Parse(row["t"], System.Globalization.CultureInfo.InvariantCulture);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    // Before the dolls start to chase.
    private static SimEnemy DollAt(ScenarioProbe p, float bearing)
        => p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.JagdDoll)
            .MinBy(e => Vector3.Distance(e.Position, Geometry.OnCompass(bearing, 7f)))!;

    [Test]
    public void DiesWalkingIntoWall()
        => LivingLiquid(MeleeDpsB)
            .TeleportAt(5f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsB);

    // Halfway between Living Liquid and the MT it swings at.
    [Test]
    public void NonTankInFluidSwingDies()
        => LivingLiquid(MeleeDpsA)
            .FreezeAt(9.9f)
            .At(10f, p => p.Member(MeleeDpsA)!.SetPosition(Vector3.Lerp(Enemy(p, BNpcBaseId.LivingLiquid).Position, p.Member(MainTank)!.Position, 0.5f)))
            .ShouldKill(ActionId.FluidSwing, MeleeDpsA);

    [Test]
    public void OffTankInTheMainTanksFluidSwingDies()
        => LivingLiquid(OffTank)
            .FreezeAt(9.9f)
            .At(10f, p => p.Member(OffTank)!.SetPosition(Vector3.Lerp(Enemy(p, BNpcBaseId.LivingLiquid).Position, p.Member(MainTank)!.Position, 0.5f)))
            .ShouldKill(ActionId.FluidSwing, OffTank);

    // Between the NW and NE dolls, 5y from each: the second Exhaust sets H1 off, taking whoever is
    // within 8y of it then.
    [Test]
    public void StandingInTwoExhaustsGoesOff()
    {
        var run = RunToDeath(RegenHealer, 42f, null, new Takeover(38.3f, new Vector2(1.9f, -4.6f)));
        var blast = run.Deaths.Where(d => d.ActionId == ActionId.ExhaustOverload).ToList();
        Assert.That(blast.Select(d => d.Role), Does.Contain(RegenHealer), run.ToString);
        Assert.That(run.Deaths.Except(blast), Is.Empty, run.ToString);
        Assert.That(blast.First(d => d.Role == RegenHealer).Time, Is.EqualTo(39.05f).Within(0.05f), "with the Exhaust, not after it");
    }

    // C stays on its bait spot for the W tornado's cone instead of stepping out.
    [Test]
    public void StayingInTheBaitedTornadoConeDies()
        => LivingLiquid(CasterDps)
            .TeleportAt(32f, to: new(-10.6f, -7.62f))
            .ShouldKill(ActionId.ProteanWaveTornadoVisible, CasterDps);

    // MT stays where the S tornado's cone was aimed, under Holmgang.
    [Test]
    public void InvulnedMainTankTakingTheVisibleTornadoProteanLives()
    {
        var mainTankId = "";
        bool? waterDown = null;
        var run = Run(MainTank, 38.5f, p =>
        {
            if (p.Crossed(32f))
            {
                mainTankId = Id(p.Member(MainTank));
                p.Member(MainTank)!.AddStatus(Holmgang, 10f);
            }
            if (p.Crossed(35.6f)) waterDown = p.Member(MainTank)!.HasStatus(StatusId.WaterResistanceDownII);
        }, new Takeover(32.3f, null));
        Assert.That(run.Passed, run.ToString);
        var hit = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows
            .Any(r => r["kind"] == "impact" && r["id"] == ActionId.ProteanWaveTornadoVisible.ToString() && r["target"] == mainTankId);
        Assert.Multiple(() =>
        {
            Assert.That(hit, "the S tornado's cone missed MT");
            Assert.That(waterDown, Is.True, "no Water Resistance Down II from the cone");
        });
    }

    // MT stays on Living Liquid's own cone, which an invuln doesn't save.
    [Test]
    public void VisibleBossProteanStillKillsAnInvulnedTank()
        => LivingLiquid(MainTank)
            .At(65.5f, p => p.Member(MainTank)!.AddStatus(Holmgang, 10f))
            .FreezeAt(65.7f)
            .ShouldKill(ActionId.ProteanWaveBossVisible, MainTank);

    // Fluid Swing leaves Water Resistance Down II; the unseen tornado protean is the second water hit.
    [Test]
    public void MainTankWithoutAnInvulnDiesToSecondWaterHit()
        => LivingLiquid(MainTank)
            .WithMitigationChecks()
            .FreezeAt(37.4f)
            .ShouldKill(ActionId.ProteanWaveTornadoInvisible, MainTank);

    // M1 carries Water Resistance Down II into the first Splash, with tank mitigation checks off.
    [Test]
    public void NonTankOnWaterResistanceDownDiesToSplash()
        => LivingLiquid(MeleeDpsA)
            .At(80f, p => p.Member(MeleeDpsA)!.AddStatus(StatusId.WaterResistanceDownII, 4f))
            .ShouldKill(ActionId.Splash, MeleeDpsA);

    [Test]
    public void BotOnWaterResistanceDownDiesToSplash()
    {
        var run = RunToDeath(RegenHealer, 81.5f, p =>
        {
            if (p.Crossed(80f)) p.Member(MeleeDpsB)!.AddStatus(StatusId.WaterResistanceDownII, 4f);
        });
        Assert.That(run.Deaths, Has.Count.EqualTo(1), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths[0].Role, Is.EqualTo(MeleeDpsB));
            Assert.That(run.Deaths[0].ActionId, Is.EqualTo(ActionId.Splash));
        });
    }

    // A tank needs an invuln for it, which only tank mitigation checks ask of a human.
    [Test]
    public void TankOnWaterResistanceDownLivesSplashWithoutMitigationChecks()
    {
        var run = Run(MainTank, 81.5f, p =>
        {
            if (p.Crossed(80f)) p.Member(MainTank)!.AddStatus(StatusId.WaterResistanceDownII, 4f);
        });
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void HolmgangOutlastsSecondWaterHit()
    {
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 38f,
            Overrides = o => Pin((TeaP1LivingLiquidStateOverrides)o),
            Takeovers = [new Takeover(37.4f, null)],
            Probe = p => { if (p.Crossed(33f)) p.Member(MainTank)!.AddStatus(Holmgang, 10f); },
            WriteArtifactsOnFailure = true,
        });
        Assert.That(run.Passed, run.ToString);
    }

    // Dropsy names no action, so the death is told apart by its cause.
    [Test]
    public void StayingInTornadoWaterDies()
    {
        var run = RunToDeath(PhysRangedDps, 30f, takeovers: [new Takeover(25f, new Vector2(-12, 0))]);
        Assert.That(run.Deaths, Has.Count.EqualTo(1), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths[0].Role, Is.EqualTo(PhysRangedDps));
            Assert.That(run.Deaths[0].Cause, Does.Contain("Dropsy"));
            Assert.That(run.Deaths[0].Time, Is.EqualTo(27.1f).Within(0.1f));
        });
    }

    // Out at 1.2s: Dropsy stays on, and the KO lands 2.1s after it did.
    [Test]
    public void SteppingOutOfTornadoWaterStillDies()
    {
        var run = RunToDeath(PhysRangedDps, 30f, takeovers: [new Takeover(25f, new Vector2(-12, 0)), new Takeover(26.2f, new Vector2(-4, -9))]);
        Assert.That(run.Deaths, Has.Count.EqualTo(1), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths[0].Role, Is.EqualTo(PhysRangedDps));
            Assert.That(run.Deaths[0].Cause, Does.Contain("Dropsy"));
            Assert.That(run.Deaths[0].Time, Is.EqualTo(27.1f).Within(0.1f));
        });
    }

    [Test]
    public void InvulnOutlastsTornadoWater()
    {
        var run = Run(PhysRangedDps, 30f, p =>
        {
            if (p.Crossed(24.9f)) p.Member(PhysRangedDps)!.AddStatus(Holmgang, 10f);
        }, new Takeover(25f, new Vector2(-12, 0)));
        Assert.That(run.Passed, run.ToString);
    }

    // Out at 1.6s under a 4s Holmgang.
    [Test]
    public void DropsyLiftsOnSteppingOutUnderAnInvuln()
    {
        bool? dropsy = null;
        var run = Run(PhysRangedDps, 30f, p =>
        {
            if (p.Crossed(24.9f)) p.Member(PhysRangedDps)!.AddStatus(Holmgang, 4f);
            if (p.Crossed(27f)) dropsy = p.Member(PhysRangedDps)!.HasStatus(StatusId.Dropsy);
        }, new Takeover(25f, new Vector2(-12, 0)), new Takeover(26.6f, new Vector2(-4, -9)));
        Assert.That(run.Passed, run.ToString);
        Assert.That(dropsy, Is.False, "Dropsy stayed on after stepping out");
    }

    // A 3s Holmgang runs out in the water: the KO at 27.1s lands on the invuln, and the next at 29.2s kills.
    [Test]
    public void AnInvulnEndingInTheWaterOnlyDelaysTheKo()
    {
        var run = RunToDeath(PhysRangedDps, 30f, p =>
        {
            if (p.Crossed(24.9f)) p.Member(PhysRangedDps)!.AddStatus(Holmgang, 3f);
        }, new Takeover(25f, new Vector2(-12, 0)));
        Assert.That(run.Deaths, Has.Count.EqualTo(1), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths[0].Role, Is.EqualTo(PhysRangedDps));
            Assert.That(run.Deaths[0].Cause, Does.Contain("Dropsy"));
            Assert.That(run.Deaths[0].Time, Is.EqualTo(29.2f).Within(0.1f));
        });
    }

    // R steps into the W tornado's water as it fades at the second Cascade.
    [Test]
    public void FadingTornadoWaterGivesNoDropsy()
    {
        bool? dropsy = null;
        var run = Run(PhysRangedDps, 89.5f, p =>
        {
            if (p.Crossed(87.5f)) dropsy = p.Member(PhysRangedDps)!.HasStatus(StatusId.Dropsy);
        }, new Takeover(86.9f, new Vector2(-12, 0)));
        Assert.That(run.Passed, run.ToString);
        Assert.That(dropsy, Is.False);
    }

    // Beside the W tornado as its Embolus appears.
    [Test]
    public void TouchingAnEmbolusSetsOffOutburst()
        => LivingLiquid(PhysRangedDps)
            .TeleportAt(41f, to: new(-13.7f, 0))
            .ShouldKill(ActionId.Outburst, PerRole.All.ToArray());

    // On the beam from the W tornado to MT, so the tether moves to M2: just outside the water, and over
    // 6y from OT, whom M2's Drainage would kill too.
    [Test]
    public void NonTankTakingDrainageDies()
        => LivingLiquid(MeleeDpsB)
            .FreezeAt(79f)
            .At(79.05f, p => p.Member(MeleeDpsB)!.SetPosition(new Placement(Vector3.Lerp(new Vector3(-15, 0, 0), p.Member(MainTank)!.Position, 0.44f), 0f)))
            .ShouldKill(ActionId.Drainage, MeleeDpsB);

    // OT 2y past MT on the line from the W tornado, so it stays off MT's beam.
    private static void OffTankBesideMainTank(ScenarioProbe p)
    {
        var mainTank = p.Member(MainTank)!.Position;
        var away = Vector3.Normalize(mainTank - new Vector3(-15, 0, 0));
        p.Member(OffTank)!.SetPosition(new Placement(mainTank + away * 2f, 0f));
    }

    [Test]
    public void TankInTheOtherTanksDrainageDies()
        => LivingLiquid(OffTank)
            .FreezeAt(85f)
            .At(85.1f, OffTankBesideMainTank)
            .ShouldKill(ActionId.Drainage, MainTank, OffTank);

    // The death names each tank's own invuln: Holmgang for the WAR MT, Hallowed Ground for the PLD OT.
    [Test]
    public void TanksShortOfAnInvulnAreToldTheirOwn()
    {
        var run = RunToDeath(OffTank, 87f, p =>
        {
            if (p.Crossed(85.1f)) OffTankBesideMainTank(p);
        }, new Takeover(85f, null));
        var causes = run.Deaths.Where(d => d.ActionId == ActionId.Drainage).ToDictionary(d => d.Role, d => d.Cause);
        Assert.That(causes.Keys, Is.EquivalentTo(new[] { MainTank, OffTank }), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(causes[MainTank], Does.Contain("Holmgang"));
            Assert.That(causes[OffTank], Does.Contain("Hallowed Ground"));
        });
    }

    // C stays in the W tornado's cone: no invuln is named to a non-tank.
    [Test]
    public void NonTankInTheBaitedTornadoConeIsToldNoInvuln()
    {
        var run = RunToDeath(CasterDps, 38f, takeovers: [new Takeover(32f, new Vector2(-10.6f, -7.62f))]);
        var death = run.Deaths.SingleOrDefault(d => d.Role == CasterDps && d.ActionId == ActionId.ProteanWaveTornadoVisible);
        Assert.That(death, Is.Not.Null, run.ToString);
        Assert.That(death!.Cause, Does.Not.Contain("no "));
    }

    [Test]
    public void BothTanksUnderAnInvulnLiveEachOthersDrainage()
    {
        var run = Run(OffTank, 87f, p =>
        {
            if (p.Crossed(84.9f))
            {
                p.Member(MainTank)!.AddStatus(Holmgang, 10f);
                p.Member(OffTank)!.AddStatus(Holmgang, 10f);
            }
            if (p.Crossed(85.1f)) OffTankBesideMainTank(p);
        }, new Takeover(85f, null));
        Assert.That(run.Passed, run.ToString);
    }

    // On MT's beam from the W tornado where M2 takes it, so OT holds both tethers over 6y from MT.
    private static void OffTankOnMainTanksBeam(ScenarioProbe p)
        => p.Member(OffTank)!.SetPosition(new Placement(Vector3.Lerp(new Vector3(-15, 0, 0), p.Member(MainTank)!.Position, 0.44f), 0f));

    private static int DrainageTethersOn(ScenarioProbe p, PartyRole role)
        => p.World.Children.OfType<SimTether>().Count(t => t.IsActive && t.TetherId == TetherId.Drainage && t.B == p.Member(role));

    // The death names the double hold and the PLD OT's invuln.
    [Test]
    public void TankHoldingBothDrainageTethersDies()
    {
        var held = 0;
        var run = RunToDeath(OffTank, 87f, p =>
        {
            if (p.Crossed(79.05f)) OffTankOnMainTanksBeam(p);
            if (p.Crossed(85.6f)) held = DrainageTethersOn(p, OffTank);
        }, new Takeover(79f, null));
        var death = run.Deaths.FirstOrDefault();
        Assert.That(death, Is.Not.Null, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That((death!.Role, death.ActionId), Is.EqualTo((OffTank, ActionId.Drainage)), run.ToString);
            Assert.That(run.Deaths.Where(d => d.ActionId == ActionId.Drainage).Select(d => d.Role), Is.EquivalentTo(new[] { OffTank }), run.ToString);
            Assert.That(death.Cause, Does.Contain("held both Drainage tethers").And.Contain("Hallowed Ground"));
            Assert.That(held, Is.EqualTo(2));
        });
    }

    [Test]
    public void TankHoldingBothDrainageTethersLivesUnderAnInvuln()
    {
        var held = 0;
        var run = Run(OffTank, 87f, p =>
        {
            if (p.Crossed(79.05f)) OffTankOnMainTanksBeam(p);
            if (p.Crossed(84.9f)) p.Member(OffTank)!.AddStatus(Holmgang, 10f);
            if (p.Crossed(85.6f)) held = DrainageTethersOn(p, OffTank);
        }, new Takeover(79f, null));
        Assert.That(run.Passed, run.ToString);
        Assert.That(held, Is.EqualTo(2));
    }

    // Not onto the other tank as a second tether. Given to H1, whose beam bots may then take.
    [TestCase(MainTank, OffTank)]
    [TestCase(OffTank, MainTank)]
    public void DeadTanksDrainageTetherGoesToAnotherPlayer(PartyRole deadTank, PartyRole survivor)
    {
        var held = new Dictionary<PartyRole, int>();
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = CasterDps,
            StopAt = 87f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).DeadTanksDrainageGoesTo = RegenHealer;
            },
            Probe = p =>
            {
                if (p.Crossed(78f)) p.Member(deadTank)!.Die(ActionId.FluidSwing);
                if (p.Crossed(78.6f))
                    foreach (var role in PerRole.All) held[role] = DrainageTethersOn(p, role);
            },
        });
        Assert.That(run.Deaths.FirstOrDefault()?.Role, Is.EqualTo(deadTank), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(held[survivor], Is.EqualTo(1));
            Assert.That(held[deadTank], Is.Zero);
            Assert.That(held.Values.Sum(), Is.EqualTo(2));
            Assert.That(run.Deaths.Where(d => d.Cause.Contains("held both")), Is.Empty, run.ToString);
        });
    }

    // H1 8y out from Living Liquid on the line to MT, one of the four closest its helper cones go at.
    [Test]
    public void BystanderBehindAnUnseenBossProteanBaitDies()
        => LivingLiquid(RegenHealer)
            .At(70.7f, p =>
            {
                var livingLiquid = Enemy(p, BNpcBaseId.LivingLiquid).Position;
                var toMainTank = Vector3.Normalize(p.Member(MainTank)!.Position - livingLiquid);
                p.Member(RegenHealer)!.SetPosition(new Placement(livingLiquid + toMainTank * 8f, 0f));
            })
            .ShouldKill(ActionId.ProteanWaveHelperInvisible, RegenHealer);

    // Throttled again after the healers' last Esuna; the KO lands as it runs out.
    [Test]
    public void ThrottleRunningOutKills()
    {
        var run = RunToDeath(PhysRangedDps, 110f, p =>
        {
            if (p.Crossed(104f)) p.Member(PhysRangedDps)!.AddStatus(StatusId.Throttle, 3f);
        });
        Assert.That(run.Deaths, Has.Count.EqualTo(1), run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths[0].Role, Is.EqualTo(PhysRangedDps));
            Assert.That(run.Deaths[0].ActionId, Is.EqualTo(ActionId.LiquidGaol));
            Assert.That(run.Deaths[0].Time, Is.EqualTo(107f).Within(0.05f));
        });
    }

    [Test]
    public void EsunaCleansesThrottleJustOverASecondAfterThePress()
    {
        bool? heldAfterPress = null, cleansed = null;
        var run = Run(RegenHealer, 97f, p =>
        {
            var target = p.Member(MainTank)!;
            if (p.Crossed(95.6f))
                JobActions.ApplyEffects(p.Member(RegenHealer)!, Esuna.ActionId, (ulong)target.GameObjectId, Random.Shared);
            if (p.Crossed(96.6f)) heldAfterPress = target.FindStatus(StatusId.Throttle) != null;
            if (p.Crossed(96.8f)) cleansed = target.FindStatus(StatusId.Throttle) == null;
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(heldAfterPress, Is.True, "Throttle came off too soon after the press");
            Assert.That(cleansed, Is.True, "Esuna left MT's Throttle");
        });
    }

    // Each healer cleanses itself first, then H1 works top down and H2 bottom up, each skipping the other.
    [Test]
    public void HealersCleanseThemselvesThenTopDownAndBottomUp()
    {
        var cleansed = new Dictionary<PartyRole, float>();
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 106f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).Throttled = [MainTank, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, CasterDps];
            },
            Probe = p =>
            {
                if (p.Time < 95.5f) return;
                foreach (var role in PerRole.All)
                    if (!cleansed.ContainsKey(role) && p.Member(role)!.FindStatus(StatusId.Throttle) == null) cleansed[role] = p.Time;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(cleansed.Where(kv => kv.Value < 105f).OrderBy(kv => kv.Value).Select(kv => kv.Key).Where(r => r is not (MeleeDpsB or PhysRangedDps)),
            Is.EqualTo(new[] { RegenHealer, ShieldHealer, MainTank, CasterDps, OffTank, MeleeDpsA }));
    }

    // MT and OT are clean before the first press, so H1's next in line top down would be H2, who cleanses itself.
    [Test]
    public void RegenHealerSkipsTheShieldHealer()
    {
        float? meleeCleansed = null;
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 106f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).Throttled = [MainTank, OffTank, ShieldHealer, MeleeDpsA, MeleeDpsB, CasterDps];
            },
            Probe = p =>
            {
                if (p.Crossed(95.6f))
                {
                    p.Member(MainTank)!.RemoveStatus(StatusId.Throttle);
                    p.Member(OffTank)!.RemoveStatus(StatusId.Throttle);
                }
                if (p.Time > 95.6f && meleeCleansed is null && p.Member(MeleeDpsA)!.FindStatus(StatusId.Throttle) == null) meleeCleansed = p.Time;
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(meleeCleansed, Is.LessThan(98f), "H1's first Esuna did not go to M1");
    }

    [Test]
    public void BotProvokePlaysOnTheHand()
    {
        string? target = null, hand = null;
        var run = Run(RegenHealer, 30.5f, p =>
        {
            if (!p.Crossed(30.2f) || p.Member(MainTank) is not { } mainTank) return;
            target = Sent(mainTank, "ability", Enmity.ProvokeActionId.ToString()).LastOrDefault()?["target"];
            hand = Id(Enemy(p, BNpcBaseId.LiquidHand));
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(target, Is.Not.Null.And.EqualTo(hand));
    }

    [Test]
    public void TornadoesAndEmboliFadeBeforeTheyGo()
    {
        var fadingAtCascade = 0;
        var orbs = new HashSet<SimEnemy>();
        var run = Run(RegenHealer, 88.5f, p =>
        {
            foreach (var orb in p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.Embolus)) orbs.Add(orb);
            if (p.Crossed(86.9f))
                fadingAtCascade = p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.LiquidRage && e.FadingOut);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(fadingAtCascade, Is.EqualTo(3));
        Assert.That(orbs.Where(o => !o.IsActive).ToList(), Is.Not.Empty.And.All.Matches<SimEnemy>(o => o.FadingOut));
    }

    [Test]
    public void SecondTornadoesAndEmboliEndWithTheRun()
    {
        var left = -1;
        var run = Run(RegenHealer, 142f, p =>
        {
            if (!p.Crossed(139.7f)) return;
            left = p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && (e.BNpcBaseId == BNpcBaseId.LiquidRage || e.BNpcBaseId == BNpcBaseId.Embolus))
                   + p.World.Children.OfType<SimEventObject>().Count(e => e.IsActive && e.EObjRowId == EObjId.VoidzoneLiquidRage);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(left, Is.Zero);
    }

    [Test]
    public void SplashesAndTheLastFluidSwingGoOffOnTime()
    {
        var livingLiquidId = "";
        var run = Run(RegenHealer, 134f, p =>
        {
            if (p.Crossed(1f)) livingLiquidId = Id(Enemy(p, BNpcBaseId.LivingLiquid));
        });
        Assert.That(run.Passed, run.ToString);
        var splashes = Sent(livingLiquidId, "ability", ActionId.Splash.ToString()).Select(At).Where(t => t < 90f).ToList();
        var swings = Sent(livingLiquidId, "ability", ActionId.FluidSwing.ToString()).Select(At).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(splashes, Is.EqualTo(new[] { 80.11f, 81.20f, 82.27f, 83.40f, 84.52f, 85.65f }).Within(0.02f));
            Assert.That(swings.LastOrDefault(), Is.EqualTo(132.95f).Within(0.02f));
        });
    }

    // From the last Fluid Swing until Living Liquid dies at the slowest real kill, as the hazards go.
    [Test]
    public void BothBossesAutoAttackUntilLivingLiquidDies()
    {
        string livingLiquidId = "", handId = "";
        int fadingBefore = -1, fadingAfter = -1;
        int Fading(ScenarioProbe p) => p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.LiquidRage && e.FadingOut);
        var run = Run(RegenHealer, 137f, p =>
        {
            if (p.Crossed(20f))
            {
                livingLiquidId = Id(Enemy(p, BNpcBaseId.LivingLiquid));
                handId = Id(Enemy(p, BNpcBaseId.LiquidHand));
            }
            if (p.Crossed(136.3f)) fadingBefore = Fading(p);
            if (p.Crossed(136.5f)) fadingAfter = Fading(p);
        });
        Assert.That(run.Passed, run.ToString);
        var livingLiquidAutos = Sent(livingLiquidId, "ability", ActionId.LivingLiquidAttack.ToString()).Select(At).Where(t => t > 132.95f).ToList();
        var handAutos = Sent(handId, "ability", ActionId.LiquidHandAttack.ToString()).Select(At).Where(t => t > 132.95f).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(livingLiquidAutos, Is.EqualTo(new[] { 133.41f }).Within(0.02f), "Living Liquid's autos");
            Assert.That(handAutos, Is.EqualTo(new[] { 133.47f }).Within(0.02f), "Liquid Hand's autos");
            Assert.That(fadingBefore, Is.Zero, "the second set faded before Living Liquid died");
            Assert.That(fadingAfter, Is.EqualTo(3), "the second set outlived Living Liquid");
        });
    }

    // R steps onto an orb just after Living Liquid dies.
    [Test]
    public void EmboliStopAndSetOffNothingOnceLivingLiquidDies()
    {
        SimEnemy? orb = null;
        Vector3? steppedOn = null, later = null;
        var run = Run(PhysRangedDps, 139f, p =>
        {
            if (p.Crossed(136.5f))
            {
                orb = Enemy(p, BNpcBaseId.Embolus);
                steppedOn = orb.Position;
                p.Member(PhysRangedDps)!.SetPosition(new Placement(orb.Position, 0f));
            }
            if (p.Crossed(137.4f)) later = orb!.Position;
        }, new Takeover(136.45f, null));
        Assert.That(run.Passed, run.ToString);
        Assert.That(Vector3.Distance(steppedOn!.Value, later!.Value), Is.LessThan(0.01f), "the orb kept rolling");
    }

    // Each voidzone needs its own entity id for the state 7 at the second Cascade to reach it.
    [Test]
    public void TornadoesAreUntargetableAndEachVoidzoneHasItsOwnEntityId()
    {
        var targetable = new List<bool>();
        var entityIds = new List<uint>();
        var run = Run(RegenHealer, 93f, p =>
        {
            if (!p.Crossed(20f) && !p.Crossed(92.5f)) return;
            targetable.AddRange(p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.LiquidRage).Select(e => e.Targetable));
            entityIds.AddRange(p.World.Children.OfType<SimEventObject>().Where(e => e.IsActive && e.EObjRowId == EObjId.VoidzoneLiquidRage)
                .Select(e => e.SpawnConfig!.EntityId));
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(targetable, Has.Count.EqualTo(6).And.All.False);
        Assert.That(entityIds, Has.Count.EqualTo(6).And.None.EqualTo(0u));
        Assert.That(entityIds, Is.Unique);
    }

    [Test]
    public void HelpersCarryAlexanderPrimesName()
    {
        var names = new List<uint>();
        var run = Run(RegenHealer, 1.5f, p =>
        {
            if (p.Crossed(1f))
                names.AddRange(p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.Helper).Select(e => e.SpawnConfig!.NameId));
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(names, Has.Count.EqualTo(11).And.All.EqualTo(BNpcNameId.AlexanderPrime));
    }

    [Test]
    public void DollsFallUnderAQuarterAtTheirTakersPace()
    {
        var expected = new (string Taker, float Bearing, float Under)[] { ("C", 337.5f, 47.74f), ("M1", 247.5f, 42.5f), ("M2", 157.5f, 42.5f), ("R", 67.5f, 45.85f) };
        var dolls = new SimEnemy[4];
        var spawnHp = new float[4];
        var under = new float?[4];
        var run = Run(RegenHealer, 49f, p =>
        {
            if (p.Crossed(32.9f))
                for (var k = 0; k < 4; k++)
                {
                    dolls[k] = DollAt(p, expected[k].Bearing);
                    spawnHp[k] = dolls[k].HpFraction;
                }
            if (p.Time < 33f) return;
            for (var k = 0; k < 4; k++)
                if (under[k] is null && dolls[k].HpFraction < 0.25f) under[k] = p.Time;
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(spawnHp, Has.All.EqualTo(1f).Within(0.001f));
        for (var k = 0; k < 4; k++)
            Assert.That(under[k], Is.EqualTo(expected[k].Under).Within(0.05f), $"{expected[k].Taker}'s doll");
    }

    // M1's doll, three seconds into its taker's hits, beside Living Liquid.
    [Test]
    public void DollFedAtAQuarterOrMoreKillsEveryone()
        => LivingLiquid(RegenHealer)
            .At(38.7f, p => DollAt(p, 247.5f).SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(2f, 0f, 0f), 0f)))
            .ShouldKill(ActionId.ReducibleComplexity, PerRole.All.ToArray());

    // M1's doll at 29.4%, the hit before it crosses 25%.
    [Test]
    public void DollFedJustOverAQuarterKillsEveryone()
    {
        SimEnemy? doll = null;
        LivingLiquid(RegenHealer)
            .At(32.9f, p => doll = DollAt(p, 247.5f))
            .At(42.2f, p => doll!.SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(2f, 0f, 0f), 0f)))
            .ShouldKill(ActionId.ReducibleComplexity, PerRole.All.ToArray());
    }

    [Test]
    public void DollFedJustUnderAQuarterKillsNobody()
    {
        SimEnemy? doll = null;
        var fed = false;
        var run = Run(RegenHealer, 47f, p =>
        {
            if (p.Crossed(32.9f)) doll = DollAt(p, 247.5f);
            if (p.Crossed(42.7f)) doll!.SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(2f, 0f, 0f), 0f));
            if (p.Crossed(43f)) fed = !doll!.Targetable;
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(fed, "M1's doll was not fed");
    }

    // M1's doll at 20.8%, held still just inside the reach on Living Liquid's far side from the Hand.
    [Test]
    public void DollFedFromJustInsideTheReach()
    {
        SimEnemy? doll = null;
        bool? fed = null, toLivingLiquid = null;
        var run = Run(RegenHealer, 47f, p =>
        {
            if (p.Crossed(32.9f)) doll = DollAt(p, 247.5f);
            if (p.Time >= 42.7f && p.Time <= 42.8f)
            {
                var livingLiquid = Enemy(p, BNpcBaseId.LivingLiquid).Position;
                var away = Vector2.Normalize(Flat(livingLiquid - Enemy(p, BNpcBaseId.LiquidHand).Position)) * 4.55f;
                doll!.Follow(null);
                doll.SetPosition(new Placement(livingLiquid + new Vector3(away.X, 0f, away.Y), 0f));
            }
            if (!p.Crossed(42.75f)) return;
            fed = !doll!.Targetable;
            var feed = p.World.Children.OfType<SimTether>().FirstOrDefault(t => t.IsActive && t.TetherId == TetherId.DollFeed && t.A == doll);
            toLivingLiquid = feed?.B == Enemy(p, BNpcBaseId.LivingLiquid);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(fed, Is.True, "M1's doll was not fed");
            Assert.That(toLivingLiquid, Is.True, "M1's doll was not fed to Living Liquid");
        });
    }

    // Real feeds: Dead at the blast, the death animation ~2.0s later, the corpse fading ~8.05s after
    // it and gone ~1.76s after that.
    [Test]
    public void FedDollGoesDownWithItsBlast()
    {
        SimEnemy? doll = null;
        var dollId = "";
        float? fedAt = null, defeatedAt = null, goneAt = null;
        var run = Run(RegenHealer, 57f, p =>
        {
            if (p.Crossed(32.9f)) dollId = Id(doll = DollAt(p, 247.5f));
            if (p.Crossed(42.7f)) doll!.SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(2f, 0f, 0f), 0f));
            if (doll is null || p.Time < 42.7f) return;
            if (fedAt is null && !doll.Targetable) fedAt = p.Time;
            if (defeatedAt is null && doll.Dead) defeatedAt = p.Time;
            if (goneAt is null && !doll.IsActive) goneAt = p.Time;
        });
        Assert.That(run.Passed, run.ToString);
        var dead = $"0x{(uint)CharacterModes.Dead:X}";
        var modes = Sent(dollId, "ac", "0x2").Where(m => m["p"] == dead || m["p"].StartsWith($"{dead},", StringComparison.Ordinal)).ToList();
        var deathAnimation = Sent(dollId, "ac", "0xE").Select(At).ToList();
        var corpseFade = Sent(dollId, "ac", "0x27").Select(At).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(defeatedAt - fedAt, Is.EqualTo(1.1f).Within(0.05f), "feed to blast");
            Assert.That(modes.Select(m => m["p"]), Is.EqualTo(new[] { $"{dead},0x2", dead }), "Dead with param 2 at the blast, then plain Dead");
            Assert.That(At(modes[0]), Is.EqualTo(defeatedAt!.Value).Within(0.05f), "Dead (2) at the blast");
            Assert.That(deathAnimation.Single() - defeatedAt, Is.EqualTo(2.1f).Within(0.05f), "blast to death animation");
            Assert.That(At(modes[1]), Is.EqualTo(deathAnimation.Single()).Within(0.001f), "plain Dead with the animation");
            Assert.That(corpseFade.Single() - deathAnimation.Single(), Is.EqualTo(8f).Within(0.05f), "death animation to corpse fade");
            Assert.That(goneAt - corpseFade.Single(), Is.EqualTo(1.7f).Within(0.05f), "corpse fade to despawn");
        });
    }

    [Test]
    public void DollFedJustOverAQuarterGoesDownWithItsBlast()
    {
        SimEnemy? doll = null;
        var defeated = false;
        RunToDeath(RegenHealer, 45f, p =>
        {
            if (p.Crossed(32.9f)) doll = DollAt(p, 247.5f);
            if (p.Crossed(42.2f)) doll!.SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(2f, 0f, 0f), 0f));
            if (doll is { Dead: true }) defeated = true;
        });
        Assert.That(defeated, "the doll outlived its Reducible Complexity");
    }

    // A tank who never provokes leaves the Hand on the OT, far from Living Liquid, for Parting.
    [Test]
    public void MainTankWhoNeverProvokesLosesPartingForEveryone()
        => LivingLiquid(MainTank)
            .FreezeAt(29.5f)
            .ShouldKill(ActionId.HandOfParting, PerRole.All.ToArray());

    [Test]
    public void ProvokeTurnsTheHandAfterTheServerDelay()
    {
        bool? autoBeforeTurnOnOffTank = null, autoAfterTurnOnMainTank = null;
        var run = Run(MainTank, 28f, p =>
        {
            if (p.Time < 20f) return;
            var hand = Enemy(p, BNpcBaseId.LiquidHand);
            if (p.Crossed(24.0f))
                JobActions.ApplyEffects(p.Member(MainTank)!, Enmity.ProvokeActionId, (ulong)hand.GameObjectId, Random.Shared);
            if (p.Crossed(24.35f)) autoBeforeTurnOnOffTank = LastActionTarget(hand) == Id(p.Member(OffTank));
            if (p.Crossed(27.45f)) autoAfterTurnOnMainTank = LastActionTarget(hand) == Id(p.Member(MainTank));
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(autoBeforeTurnOnOffTank, Is.True, "the 24.29 auto");
            Assert.That(autoAfterTurnOnMainTank, Is.True, "the 27.38 auto");
        });
    }

    // M1 hits R's doll (NE) before R does, so it swings at M1.
    [Test]
    public void DollTurnsOnWhoeverHitsItFirst()
    {
        var swungAtM1 = false;
        var run = Run(MeleeDpsA, 40f, p =>
        {
            if (p.Time < 33.5f) return;
            var doll = p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.JagdDoll)
                .MinBy(e => Vector3.Distance(e.Position, Geometry.OnCompass(67.5f, 7f)))!;
            if (p.Crossed(34f)) doll.NoteAttack(p.Member(MeleeDpsA)!);
            if (p.Crossed(39.8f)) swungAtM1 = LastActionTarget(doll) == Id(p.Member(MeleeDpsA));
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(swungAtM1, "R's doll did not turn on M1");
    }

    [Test]
    public void BotsFeedEveryDoll([Values(0, 45, 90, 135, 180, 225, 270, 315)] int openSide, [Values(0.1f, 0.5f, 0.9f)] float pace)
    {
        var hungry = -1;
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 60f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).FirstTornadoesOpenSide = openSide;
                ((TeaP1LivingLiquidStateOverrides)o).DollRoll = new DollRoll(pace, pace, pace, pace);
            },
            Probe = p =>
            {
                if (p.Crossed(59.9f))
                    hungry = p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.Targetable && e.BNpcBaseId == BNpcBaseId.JagdDoll);
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(hungry, Is.Zero, "dolls left unfed");
    }

    // The party holds the middle with Living Liquid for about two GCDs after the split, stepping out of the
    // bosses' way as it sees the tornadoes, then leaves in a stagger: the healers and ranged first, the melee
    // and OT last, the OT taking the Hand's far side first. Living Liquid first moves 1.5y between about
    // 22.6s and 25.4s.
    [Test]
    public void BotsHoldTheMiddleForTwoGcdsAfterTheSplit([Values(0, 45, 90, 135, 180, 225, 270, 315)] int openSide)
    {
        var holdUntil = new Dictionary<PartyRole, float>
        {
            [RegenHealer] = 24f, [ShieldHealer] = 24f, [PhysRangedDps] = 24f, [CasterDps] = 24f,
            [OffTank] = 25.4f, [MeleeDpsA] = 26f, [MeleeDpsB] = 26f,
        };
        Vector3? livingLiquidAtSplit = null;
        float? livingLiquidLeft = null;
        var holding = new Dictionary<PartyRole, Vector3>();
        var held = new Dictionary<PartyRole, float>();
        var middle = new Dictionary<PartyRole, float>();
        var settled = new Dictionary<PartyRole, Vector3>();
        var left = new Dictionary<PartyRole, float>();
        var drift = new Dictionary<PartyRole, float>();
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 30.15f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).FirstTornadoesOpenSide = openSide;
            },
            Probe = p =>
            {
                if (p.Time < 19.7f) return;
                var livingLiquid = Enemy(p, BNpcBaseId.LivingLiquid).Position;
                livingLiquidAtSplit ??= livingLiquid;
                if (livingLiquidLeft is null && Flat(livingLiquid - livingLiquidAtSplit.Value).Length() > 1.5f) livingLiquidLeft = p.Time;
                foreach (var (role, until) in holdUntil)
                {
                    if (p.Crossed(22.7f)) holding[role] = p.Member(role)!.Position;
                    if (p.Crossed(until))
                    {
                        held[role] = Flat(p.Member(role)!.Position - holding[role]).Length();
                        middle[role] = Flat(p.Member(role)!.Position).Length();
                    }
                    if (p.Crossed(29.7f))
                    {
                        settled[role] = p.Member(role)!.Position;
                        left[role] = Flat(settled[role] - holding[role]).Length();
                    }
                    if (p.Crossed(30.1f)) drift[role] = Flat(p.Member(role)!.Position - settled[role]).Length();
                }
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(livingLiquidLeft, Is.InRange(22.6f, 25.4f), "when Living Liquid left the middle");
            foreach (var (role, until) in holdUntil)
            {
                Assert.That(held[role], Is.LessThan(0.05f), $"{role} left its hold before {until}");
                Assert.That(middle[role], Is.LessThan(9f), $"{role} held outside the middle");
                Assert.That(left[role], Is.GreaterThan(0.5f), $"{role} still on its hold at 29.7");
                Assert.That(drift[role], Is.LessThan(0.05f), $"{role} still walking at 29.7");
            }
        });
    }

    // From the split until the dolls turn targetable no hitbox touches a boss's but its tank's, and the MT
    // walks Living Liquid out without cutting across it.
    [Test]
    public void BotsWalkAroundTheBossesAfterTheSplit([Values(0, 45, 90, 135, 180, 225, 270, 315)] int openSide)
    {
        var touches = new Dictionary<string, float>();
        float? fromLivingLiquid = null, closest = null;
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 33.5f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).FirstTornadoesOpenSide = openSide;
            },
            Probe = p =>
            {
                if (p.Time < 19.7f) return;
                foreach (var boss in p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId is BNpcBaseId.LivingLiquid or BNpcBaseId.LiquidHand))
                foreach (var role in PerRole.All)
                {
                    var member = p.Member(role)!;
                    var apart = Flat(member.Position - boss.Position).Length();
                    var livingLiquid = boss.BNpcBaseId == BNpcBaseId.LivingLiquid;
                    if (role == MainTank && livingLiquid)
                    {
                        if (p.Time is < 22.8f or > 26.5f) continue;
                        fromLivingLiquid ??= apart;
                        closest = MathF.Min(closest ?? apart, apart);
                        continue;
                    }
                    if ((role == MainTank && p.Time >= 31.15f) || (role == OffTank && !livingLiquid && p.Time >= 29.85f)) continue;
                    if (apart < boss.HitboxRadius + member.HitboxRadius) touches.TryAdd($"{role} on {(livingLiquid ? "Living Liquid" : "Liquid Hand")}", p.Time);
                }
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(touches.Select(t => $"{t.Key} at {t.Value:F2}"), Is.Empty);
            Assert.That(closest, Is.GreaterThan(fromLivingLiquid!.Value - 0.6f), "the MT cut across Living Liquid");
        });
    }

    // A human MT who parks Living Liquid about 3.5y east of the centre instead of on it.
    [Test]
    public void BotsFeedEveryDollWithLivingLiquidOffCentre([Values(0.1f, 0.9f)] float pace)
    {
        var hungry = -1;
        var run = ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 60f,
            Overrides = o =>
            {
                Pin((TeaP1LivingLiquidStateOverrides)o);
                ((TeaP1LivingLiquidStateOverrides)o).DollRoll = new DollRoll(pace, pace, pace, pace);
            },
            Takeovers = [new Takeover(41.3f, new Vector2(7.5f, -5f))],
            Probe = p =>
            {
                if (p.Crossed(59.9f))
                    hungry = p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.Targetable && e.BNpcBaseId == BNpcBaseId.JagdDoll);
            },
        });
        Assert.That(run.Deaths.Where(d => d.ActionId is ActionId.ReducibleComplexity or ActionId.Exhaust or ActionId.ExhaustOverload), Is.Empty, run.ToString);
        Assert.That(hungry, Is.Zero, "dolls left unfed");
    }

    [Test]
    public void HandOfPartingWithTheBossesApartKills()
        => LivingLiquid(RegenHealer)
            .At(34.3f, p => Enemy(p, BNpcBaseId.LiquidHand).SetPosition(new Placement(new Vector3(0, 0, -15), 0f)))
            .ShouldKill(ActionId.HandOfParting, PerRole.All.ToArray());

    // Liquid Hand held this far east of Living Liquid until Parting resolves.
    private static void HoldHandBesideLivingLiquid(ScenarioProbe p, float yalms)
    {
        if (p.Time is < 34.3f or > 34.5f) return;
        Enemy(p, BNpcBaseId.LiquidHand).SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(yalms, 0, 0), 0f));
    }

    [Test]
    public void HandOfPartingPassesWithTheBossesSevenYardsApart()
    {
        var run = Run(RegenHealer, 35.5f, p => HoldHandBesideLivingLiquid(p, 7f));
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void HandOfPartingWithTheBossesEightYardsApartKills()
    {
        var run = RunToDeath(RegenHealer, 35.5f, p => HoldHandBesideLivingLiquid(p, 8f));
        Assert.That(run.Deaths.Where(d => d.ActionId == ActionId.HandOfParting).Select(d => d.Role), Is.EquivalentTo(PerRole.All), run.ToString);
    }

    [Test]
    public void HandOfPartingPassesWithTheBossesFiveYardsApart()
    {
        var run = Run(RegenHealer, 35.5f, p =>
        {
            if (p.Crossed(34.4f))
                Enemy(p, BNpcBaseId.LiquidHand).SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(5, 0, 0), 0f));
        });
        Assert.That(run.Passed, run.ToString);
    }

    // A Prayer tell at the second Hand, then the Hand put this far from Living Liquid as it resolves.
    private static void SecondPrayerApart(ScenarioProbe p, float yalms)
    {
        if (p.Crossed(117.9f) || p.Crossed(123.0f))
            Enemy(p, BNpcBaseId.LiquidHand).SetPosition(new Placement(
                Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(p.Time < 123.0f ? 4 : yalms, 0, 0), 0f));
    }

    [Test]
    public void HandOfPrayerPassesWithTheBossesEighteenYardsApart()
    {
        var run = Run(RegenHealer, 124.5f, p => SecondPrayerApart(p, 18f));
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void HandOfPrayerWithTheBossesSeventeenYardsApartKills()
        => LivingLiquid(RegenHealer)
            .At(117.9f, p => SecondPrayerApart(p, 17f))
            .At(123.0f, p => SecondPrayerApart(p, 17f))
            .ShouldKill(ActionId.HandOfPrayer, PerRole.All.ToArray());

    // A Prayer tell at the second Hand: the tanks drag the bosses apart instead of together.
    [Test]
    public void BotsSplitTheBossesForPrayer()
    {
        byte tell = 0;
        var run = Run(RegenHealer, 124.5f, p =>
        {
            if (p.Crossed(117.9f))
                Enemy(p, BNpcBaseId.LiquidHand).SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(4, 0, 0), 0f));
            if (p.Crossed(118.5f)) tell = Enemy(p, BNpcBaseId.LiquidHand).ModelState;
        });
        Assert.That(tell, Is.EqualTo(19), "Liquid Hand's tell");
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void HandTellsPrayerWithTheBossesClose()
    {
        byte tell = 0;
        var run = Run(RegenHealer, 29.6f, p =>
        {
            var hand = Enemy(p, p.Time < 19.7f ? BNpcBaseId.LivingLiquid : BNpcBaseId.LiquidHand);
            if (p.Crossed(29.2f)) hand.SetPosition(new Placement(Enemy(p, BNpcBaseId.LivingLiquid).Position + new Vector3(4, 0, 0), 0f));
            if (p.Crossed(29.5f)) tell = hand.ModelState;
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(tell, Is.EqualTo(19), "Liquid Hand's tell");
    }

    private static TeaP1LivingLiquidState State(ScenarioProbe p) => p.Game.Catalog.Scenarios.OfType<TeaP1LivingLiquidScenario>().Single().LastState!;

    private static Vector2 Local(Dictionary<string, string> row) => new(ShownRun.Number(row, "x") - 100f, ShownRun.Number(row, "z") - 100f);

    // On any roll: each doll tethered to the player who took it, then fed under a quarter of its HP on
    // the client, tethered to the boss that takes it, and down within 3s of its blast.
    [Test]
    public void EveryDollIsFedUnderAQuarterOnScreen()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            var dolls = new string[4];
            var hpAtBlast = new Dictionary<string, float>();
            var seen = 0;
            var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 72f,
                Probe = p =>
                {
                    if (p.Crossed(33f))
                        for (var k = 0; k < dolls.Length; k++) dolls[k] = Id(State(p).Objects.Dolls[k]);
                    var rows = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;
                    for (; seen < rows.Count; seen++)
                        if (rows[seen]["kind"] == "ability" && rows[seen]["id"] == ActionId.ReducibleComplexity.ToString() && dolls.Contains(rows[seen]["actor"])
                            && ((FakeBattleCharas)Natives.BattleCharas).Find(uint.Parse(rows[seen]["actor"], System.Globalization.NumberStyles.HexNumber)) is { } doll)
                            hpAtBlast[rows[seen]["actor"]] = doll.Health / (float)doll.MaxHealth;
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            Assert.Multiple(() =>
            {
                foreach (var doll in dolls)
                {
                    var what = $"seed {seed}, doll {Array.IndexOf(dolls, doll)}";
                    var mine = shown.Rows.Where(r => r["actor"] == doll).ToList();
                    var tethers = mine.Where(r => r["kind"] == "ac" && r["id"] == "0x23").Select(r => r["p"].Split(',')).ToList();
                    var blasts = mine.Where(r => r["kind"] == "ability" && r["id"] == ActionId.ReducibleComplexity.ToString()).ToList();
                    Assert.That(tethers.Select(t => Convert.ToUInt32(t[1], 16)), Is.EqualTo(new uint[] { TetherId.DollFixate, TetherId.DollFeed }), $"{what}: fixate, then feed");
                    Assert.That(tethers.Count > 0 && shown.SeatOf(tethers[0][2][2..]) is not null, Is.True, $"{what}: fixated on a player");
                    Assert.That(blasts, Has.Count.EqualTo(1), what);
                    Assert.That(hpAtBlast.GetValueOrDefault(doll, 1f), Is.LessThan(0.25f), $"{what}: its HP on the client as it went off");
                    if (blasts.Count == 1)
                        Assert.That(mine.Any(r => r["kind"] == "ac" && r["id"] == "0xE" && ShownRun.Time(r) > ShownRun.Time(blasts[0]) && ShownRun.Time(r) <= ShownRun.Time(blasts[0]) + 3f),
                            Is.True, $"{what}: down within 3s of its blast");
                }
            });
        }
    }

    // The Liquid Hand shows which Hand it will cast, from 29.35s and 118.04s, and casts that one: Prayer's
    // tell (model state 19) for Prayer, Parting's (20) for Parting, on any roll.
    [Test]
    public void TheHandCastsTheMechanicItsTellShowed()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            string? hand = null;
            var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 125f,
                Probe = p => hand ??= State(p).Objects.LiquidHand is { } liquidHand ? Id(liquidHand) : null,
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var mine = shown.Rows.Where(r => r["actor"] == hand).ToList();
            var tells = mine.Where(r => r["kind"] == "ac" && r["id"] == "0x3F").ToList();
            var hands = mine.Where(r => r["kind"] == "ability" && r["id"] is var id && (id == ActionId.HandOfPrayer.ToString() || id == ActionId.HandOfParting.ToString())).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(tells.Select(ShownRun.Time), Is.EqualTo(new[] { 29.35f, 34.33f, 118.04f, 122.99f }).Within(0.05f), $"seed {seed}: the tells on and off");
                Assert.That(hands, Has.Count.EqualTo(2), $"seed {seed}");
                if (tells.Count != 4 || hands.Count != 2) return;
                for (var k = 0; k < 2; k++)
                {
                    Assert.That(tells[2 * k + 1]["p"], Is.Empty, $"seed {seed}: tell {k + 1} cleared");
                    Assert.That(hands[k]["id"], Is.EqualTo((tells[2 * k]["p"] == "0x13" ? ActionId.HandOfPrayer : ActionId.HandOfParting).ToString()),
                        $"seed {seed}: Hand {k + 1} after tell {tells[2 * k]["p"]}");
                }
            });
        }
    }

    // On any roll: each tethered tornado's Drainage tether on the tank the roll gave it from 78.53s, the
    // third tornado untethered, and each Drainage at 85.65s on its own tank alone.
    [Test]
    public void EachDrainageTetherRunsToItsTank()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP1LivingLiquidState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 87f,
                Probe = p => state ??= State(p),
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var spawned = shown.Of("spawn", BNpcBaseId.LiquidRage).Take(3).ToList();
            var rages = Enumerable.Range(0, 3).Select(i => spawned.MinBy(r => Vector2.Distance(Local(r), state!.FirstTornadoes.Tornado(i)))!["actor"]).ToArray();
            var tanks = state!.MainTankHoldsEarlierTether ? new[] { MainTank, OffTank } : [OffTank, MainTank];
            var sources = Enumerable.Range(0, 3).Where(i => i != state.UntetheredTornado).Select(i => rages[i]).ToList();
            var tethers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x23" && r["p"].Split(',')[1] == $"0x{TetherId.Drainage:X}").ToList();
            var drainages = shown.Of("ability", ActionId.Drainage).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(tethers.Select(r => (r["actor"], r["p"].Split(',')[2])), Is.EqualTo(sources.Select((rage, j) => (rage, "0x" + shown.IdOf(tanks[j])))), $"seed {seed}");
                Assert.That(tethers.Select(ShownRun.Time), Is.All.EqualTo(78.53f).Within(0.05f), $"seed {seed}");
                Assert.That(drainages.Select(r => (r["actor"], ShownRun.Hits(r).Single())), Is.EquivalentTo(sources.Select((rage, j) => (rage, shown.IdOf(tanks[j])))),
                    $"seed {seed}: each Drainage on its tank alone");
                Assert.That(drainages.Select(ShownRun.Time), Is.All.EqualTo(85.65f).Within(0.05f), $"seed {seed}");
            });
        }
    }

    // On any roll: Throttle on the six the roll gave it at 95.38s, each taken off by an Esuna on them,
    // before any runs out. The player sits out a healer's seat: a debug bot there applies its Esuna
    // straight, and its own client shows no action.
    [Test]
    public void EveryThrottleShowsOnItsPlayerAndEsunaTakesItOff()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP1LivingLiquidState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 107f,
                PlayerRole = MeleeDpsA,
                Probe = p => state ??= State(p),
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var throttled = state!.Throttled.Select(shown.IdOf).ToArray();
            var gains = shown.Of("status_gain", StatusId.Throttle).ToList();
            var losses = shown.Of("status_lose", StatusId.Throttle).ToList();
            var esunas = shown.Of("ability", Esuna.ActionId).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(gains.Select(r => r["actor"]), Is.EquivalentTo(throttled), $"seed {seed}");
                Assert.That(gains.Select(ShownRun.Time), Is.All.EqualTo(95.38f).Within(0.05f), $"seed {seed}");
                Assert.That(losses.Select(r => r["actor"]), Is.EquivalentTo(throttled), $"seed {seed}: every Throttle taken off");
                foreach (var loss in losses)
                {
                    Assert.That(ShownRun.Time(loss), Is.LessThan(95.38f + 10f), $"seed {seed}: {shown.SeatOf(loss["actor"])}'s ran out");
                    Assert.That(esunas.Any(e => e["target"] == loss["actor"] && ShownRun.Time(e) < ShownRun.Time(loss) && ShownRun.Time(e) > ShownRun.Time(loss) - 1.5f), Is.True,
                        $"seed {seed}: {shown.SeatOf(loss["actor"])}'s Throttle off with no Esuna on them");
                }
            });
        }
    }

    // Each set of tornadoes stands where its T says, 15y out on the three arms but the open one, for
    // every opening.
    [Test]
    public void TheTornadoesStandOnTheirT([Values(0, 45, 90, 135, 180, 225, 270, 315)] int openSide)
    {
        var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 93f,
            Overrides = o =>
            {
                ((TeaP1LivingLiquidStateOverrides)o).FirstTornadoesOpenSide = openSide;
                ((TeaP1LivingLiquidStateOverrides)o).SecondTornadoesOpenSide = (openSide + 90) % 360;
            },
        });
        Assert.That(shown.Run.Passed, shown.Run.ToString);
        var rages = shown.Of("spawn", BNpcBaseId.LiquidRage).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(rages, Has.Count.EqualTo(6));
            foreach (var (set, t, at) in new[] { (rages.Take(3), new TornadoT(openSide), 19.66f), (rages.Skip(3), new TornadoT((openSide + 90) % 360), 92.10f) })
            {
                var spots = set.Select(Local).ToList();
                Assert.That(set.Select(ShownRun.Time), Is.All.EqualTo(at).Within(0.05f));
                for (var i = 0; i < 3; i++)
                    Assert.That(spots.Min(s => Vector2.Distance(s, t.Tornado(i))), Is.LessThan(0.05f), $"open {t.OpenSide}: no tornado at {t.Tornado(i)}");
            }
        });
    }

    // Living Liquid and the Hand never lose HP on the client; only the dolls do.
    [Test]
    public void OnlyTheDollsLoseHp()
    {
        var dropped = new HashSet<uint>();
        var shown = ShownRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
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
        Assert.That(dropped, Is.SubsetOf(new[] { BNpcBaseId.JagdDoll }));
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        TeaP1LivingLiquidState? host = null;
        TeaP1LivingLiquidState? replayed = null;
        ScenarioRun.Execute(typeof(TeaP1LivingLiquidScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 1f,
            Probe = probe =>
            {
                if (!probe.Crossed(0.5f)) return;
                var scenario = probe.Game.Catalog.Scenarios.OfType<TeaP1LivingLiquidScenario>().Single();
                host = scenario.LastState;
                replayed = (TeaP1LivingLiquidState?)scenario.StartReplay(scenario.BuildReplayStateMessage()!, 0, MainTank, probe.World);
            },
        });

        Assert.That(host, Is.Not.Null);
        Assert.That(replayed, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replayed!.FirstTornadoes, Is.EqualTo(host!.FirstTornadoes));
            Assert.That(replayed.SecondTornadoes, Is.EqualTo(host.SecondTornadoes));
            Assert.That(replayed.FirstCalmTornado, Is.EqualTo(host.FirstCalmTornado));
            Assert.That(replayed.SecondCalmTornado, Is.EqualTo(host.SecondCalmTornado));
            Assert.That(replayed.FirstHandOrbFromEarlier, Is.EqualTo(host.FirstHandOrbFromEarlier));
            Assert.That(replayed.SecondHandOrbFromEarlier, Is.EqualTo(host.SecondHandOrbFromEarlier));
            Assert.That(replayed.UntetheredTornado, Is.EqualTo(host.UntetheredTornado));
            Assert.That(replayed.MainTankHoldsEarlierTether, Is.EqualTo(host.MainTankHoldsEarlierTether));
            Assert.That(replayed.Throttled, Is.EqualTo(host.Throttled));
        });
    }

    [Test]
    public void ReplayStateRejectsBadRolls()
    {
        PartyRole[] throttled = [MainTank, OffTank, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
        Assert.Multiple(() =>
        {
            Assert.That(TeaP1LivingLiquidState.FromNetworkReplay(30, 0, 0, 0, true, true, 0, true, throttled), Is.Null);
            Assert.That(TeaP1LivingLiquidState.FromNetworkReplay(0, 0, 3, 0, true, true, 0, true, throttled), Is.Null);
            Assert.That(TeaP1LivingLiquidState.FromNetworkReplay(0, 0, 0, 0, true, true, 0, true, throttled[..5]), Is.Null);
            Assert.That(TeaP1LivingLiquidState.FromNetworkReplay(315, 45, 2, 1, false, true, 1, false, throttled), Is.Not.Null);
        });
    }
}
