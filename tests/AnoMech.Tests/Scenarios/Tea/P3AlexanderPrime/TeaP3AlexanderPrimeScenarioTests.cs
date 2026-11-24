using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.P3AlexanderPrime;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned but which orb each DPS draws: the four DPS take the Plasmaspheres, so the
// crystals go to MT (wall E), OT (wall W), H1 (inner W) and H2 (inner E); Restraining Order on MT and
// R, Shared Sentence on M2, Aggravated Assault on M1 and C; Alexander Prime south for Inception and his
// Sacrament spot 1 (67.5°); Brute Justice
// north-west and the wormholes WNW/ESE; numbers 1 C, 2 M2, 3 H1, 4 MT, 5 OT, 6 M1, 7 R, 8 H2;
// Enumerations on M2 and R; every damage-over-time tick on a multiple of 3s.
//
//    7.21  Chastening Heat on OT, Divine Spears at 15.37 / 17.46 / 19.56; MT provokes at 20.9.
//   36.19  The orbs west and east, homing from 42.05; the crystals drop at 49.01 and the True Heart
//          shatters the wall pair at 54.86 and the inner pair at 55.79, the party waiting it out south.
//   60.37  Flarethrowers on MT, OT and the party; the debuffs at 66.84, the Plaints at 79.93, Sacrament
//          at 80.23, Alpha Swords from 86.24 and Super Jump on OT at 89.42.
//  135.53  The wormholes; the numbers' hits run 141.90 to 156.08, Repentance at 146.50 / 150.74 / 154.97.
//  155.98  Incinerating Heat, the Enumerations at 165.64, Mega Holy at 169.92 and 177.06.
//  198.01  Divine Judgment; Cruise Chaser down at 220.97, Brute Justice at 234.80, Alexander Prime at
//          272.14 with the success 0.13s later; its last hit, a tank's LB3 the way through, at 287.73.
public class TeaP3AlexanderPrimeScenarioTests
{
    private static readonly PartyRole[] PinnedNumbers =
        [CasterDps, MeleeDpsB, RegenHealer, MainTank, OffTank, MeleeDpsA, PhysRangedDps, ShieldHealer];

    private const float RaidwideLands = 287.91f;
    private const float Success = 289.52f - 17.25f;
    private const float WalkStarts = 54.55f;
    private const float HeartStep = 0.31f;

    private static void Pin(TeaP3AlexanderPrimeStateOverrides o)
    {
        foreach (var role in PerRole.All) o.Orb[role] = role.IsDps();
        o.RestrainingOrderTank = MainTank;
        o.Debuff[PhysRangedDps] = InceptionDebuff.RestrainingOrder;
        o.Debuff[MeleeDpsB] = InceptionDebuff.SharedSentence;
        o.Debuff[MeleeDpsA] = InceptionDebuff.AggravatedAssault;
        o.Debuff[CasterDps] = InceptionDebuff.AggravatedAssault;
        o.AlexanderPrimeNorth = false;
        o.SacramentSpot = 1;
        o.BruteJusticeNorthWest = true;
        o.PuddlesMirrored = false;
        for (var k = 0; k < PinnedNumbers.Length; k++) o.Number[PinnedNumbers[k]] = k + 1;
        o.Enumeration[MeleeDpsB] = true;
        o.Enumeration[PhysRangedDps] = true;
        o.TickClock = 0f;
    }

    private static NegativeRun<TeaP3AlexanderPrimeScenario> AlexanderPrime(PartyRole player)
        => Negative<TeaP3AlexanderPrimeScenario>(player).Overrides<TeaP3AlexanderPrimeStateOverrides>(Pin);

    private static ScenarioRun Run(PartyRole player, float? stopAt = null, Action<ScenarioProbe>? probe = null, bool alexanderPrimeNorth = false,
        params Takeover[] takeovers)
        => ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = stopAt,
            Overrides = o =>
            {
                Pin((TeaP3AlexanderPrimeStateOverrides)o);
                ((TeaP3AlexanderPrimeStateOverrides)o).AlexanderPrimeNorth = alexanderPrimeNorth;
            },
            Takeovers = takeovers,
            Probe = probe,
        });

    private static TeaP3AlexanderPrimeState State(ScenarioProbe p)
        => p.Game.Catalog.Scenarios.OfType<TeaP3AlexanderPrimeScenario>().Single().LastState!;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static IReadOnlyList<Dictionary<string, string>> Rows() => ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;

    private static List<Dictionary<string, string>> Rows(string kind, uint id)
        => Rows().Where(r => r["kind"] == kind && r["id"] == id.ToString()).ToList();

    // Real max HP at the fight's item level.
    private static float HitPoints(PartyRole role) => role switch
    {
        MainTank or OffTank => 46500f,
        MeleeDpsA or MeleeDpsB => 32300f,
        PhysRangedDps => 32200f,
        _ => 29300f,
    };

    private static float ShatterDamage(float distance) => MathF.Max(3300f, 52900f - 2370f * distance);

    // How far `seconds` is from the nearest step of the heart's walk.
    private static float OffStep(float seconds) => MathF.Abs(seconds - HeartStep * MathF.Round(seconds / HeartStep));

    [Test]
    public void PinnedRollClearsAtDivineJudgmentsSuccess()
    {
        var run = Run(PhysRangedDps);
        Assert.That(run.Passed, run.ToString);
        Assert.That(run.Elapsed, Is.InRange(RaidwideLands, RaidwideLands + 1.5f), run.ToString);
    }

    // Inception mirrored: the crystals south, the True Heart walking north, the party's flame north.
    [Test]
    public void PinnedRollWithAlexanderPrimeNorthClears()
    {
        var run = Run(PhysRangedDps, alexanderPrimeNorth: true);
        Assert.That(run.Passed, run.ToString);
        Assert.That(run.Elapsed, Is.InRange(RaidwideLands, RaidwideLands + 1.5f), run.ToString);
    }

    [Test]
    public void DiesWalkingIntoWall()
        => AlexanderPrime(MeleeDpsB)
            .TeleportAt(4f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsB);

    // Chastening Heat's Magic Vulnerability Up is still on OT for all three spears.
    [Test]
    public void TankWithoutAnInvulnDiesToDivineSpear()
        => AlexanderPrime(OffTank)
            .WithMitigationChecks()
            .FreezeAt(10f)
            .ShouldKill(ActionId.DivineSpear, OffTank);

    [Test]
    public void HolmgangOutlastsTheDivineSpears()
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = OffTank,
            StopAt = 21f,
            UserActionsEnabled = true,
            Overrides = o => Pin((TeaP3AlexanderPrimeStateOverrides)o),
            Takeovers = [new Takeover(10f, null)],
            Probe = p => { if (p.Crossed(14.5f)) p.Member(OffTank)!.AddStatus(409, 10f); },
        });
        Assert.That(run.Passed, run.ToString);
    }

    // Each DPS stands where its orb went off as the zone forms: Heavy (20) holds it to 80% of a run or
    // 110/130 of a sprint until it walks out.
    [Test]
    public void AnOrbsZoneSlowsWhoeverStandsInIt()
    {
        var heavy = new Dictionary<PartyRole, ushort>();
        var fastest = 0f;
        var last = new Dictionary<PartyRole, (float Time, Vector2 At, bool Heavy)>();
        PartyRole[] bots = [MeleeDpsA, MeleeDpsB, CasterDps];
        var run = Run(PhysRangedDps, 70f, p =>
        {
            foreach (var role in bots)
            {
                var member = p.Member(role)!;
                var status = member.FindStatus(StatusId.Heavy);
                if (status is not null) heavy[role] = status.Stacks;
                if (last.TryGetValue(role, out var before) && before.Heavy && status is not null && p.Time > before.Time)
                    fastest = MathF.Max(fastest, Vector2.Distance(Flat(member.Position), before.At) / (p.Time - before.Time));
                last[role] = (p.Time, Flat(member.Position), status is not null);
            }
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(heavy.Keys, Is.EquivalentTo(bots), "an orb's player never stood in its zone");
            Assert.That(heavy.Values, Is.All.EqualTo(20));
            Assert.That(fastest, Is.GreaterThan(1f), "nobody walked while Heavy");
            Assert.That(fastest, Is.LessThanOrEqualTo(AiManager.SprintSpeed * 110f / 130f + 0.01f));
        });
    }

    // The zone reaches 6y from where its orb went off: H2, waiting south once its crystal is down, is put
    // just inside or just outside the first one as it forms, on the side away from the other orbs.
    [TestCase(5.8f, true)]
    [TestCase(6.2f, false)]
    public void AnOrbsZoneReachesSixYalms(float distance, bool heavy)
    {
        string[] orbs = [];
        int? popped = null;
        float? formedAt = null, placedAt = null, clearOfOthers = null;
        bool? wasHeavy = null;
        var seen = 0;
        Run(ShieldHealer, 53f, p =>
        {
            if (p.Crossed(41f)) orbs = State(p).Objects.Plasmaspheres.Select(o => o!.EntityId.ToString("X8")).ToArray();
            for (var rows = Rows(); popped is null && seen < rows.Count; seen++)
                if (rows[seen]["kind"] == "ability" && rows[seen]["id"] == ActionId.Aetheroplasm.ToString() && Array.IndexOf(orbs, rows[seen]["actor"]) is var k and >= 0)
                {
                    popped = k;
                    formedAt = p.Time + 0.5f;
                }
            var plasmaspheres = State(p).Objects.Plasmaspheres;
            if (popped is { } first && placedAt is null && p.Time >= formedAt + 0.1f)
            {
                var centre = Flat(plasmaspheres[first]!.Position);
                var others = plasmaspheres.Where((o, i) => i != first && o is { IsActive: true }).Select(o => Flat(o!.Position)).ToList();
                var spot = Enumerable.Range(0, 16).Select(i => centre + distance * new Vector2(MathF.Cos(i * MathF.PI / 8f), MathF.Sin(i * MathF.PI / 8f)))
                    .Where(s => s.Length() < 17f).MaxBy(s => others.Count == 0 ? float.MaxValue : others.Min(o => Vector2.Distance(o, s)));
                clearOfOthers = others.Count == 0 ? float.MaxValue : others.Min(o => Vector2.Distance(o, spot));
                p.Member(ShieldHealer)!.SetPosition(new Vector3(spot.X, 0f, spot.Y));
                placedAt = p.Time;
            }
            if (placedAt is { } at && wasHeavy is null && p.Time >= at + 0.1f)
                wasHeavy = p.Member(ShieldHealer)!.HasStatus(StatusId.Heavy);
        }, takeovers: [new Takeover(49.5f, new Vector2(0f, 17f))]);
        Assert.Multiple(() =>
        {
            Assert.That(clearOfOthers, Is.GreaterThan(7f), "too near another orb's zone or blast");
            Assert.That(wasHeavy, Is.EqualTo(heavy));
        });
    }

    // Onto whichever orb has nobody else within its blast.
    [Test]
    public void StrangerSettingOffAnOrbDies()
        => AlexanderPrime(MainTank)
            .FreezeAt(45.9f)
            .At(46f, p =>
            {
                var others = PerRole.All.Where(r => r != MainTank).Select(r => Flat(p.Member(r)!.Position)).ToList();
                var orb = State(p).Objects.Plasmaspheres.OfType<SimEnemy>()
                    .MaxBy(o => others.Min(m => Vector2.Distance(m, Flat(o.Position))))!;
                p.Member(MainTank)!.SetPosition(orb.Position);
            })
            .ShouldKill(ActionId.Aetheroplasm, MainTank);

    // Orb 1's player waits at `spot` on the east side instead of on the south-east dot, outside orb 0's
    // blast. The run's orb 0 and orb 1 entity ids, holders and how far apart they went off come back with it.
    private static (ScenarioRun Run, string[] Orbs, PartyRole[] Holders, float PopsApart) OrbOneWaitsAt(Vector3 spot)
    {
        var orbs = new string[2];
        var holders = new PartyRole[2];
        var pops = new Vector2?[2];
        var seen = 0;
        var run = Run(RegenHealer, 58f, p =>
        {
            if (p.Crossed(41f))
            {
                for (var k = 0; k < 2; k++)
                {
                    orbs[k] = State(p).Objects.Plasmaspheres[k]!.EntityId.ToString("X8");
                    holders[k] = State(p).OrbHolders[k];
                }
                p.Member(holders[1])!.SetPosition(spot);
            }
            for (var rows = Rows(); seen < rows.Count; seen++)
                if (rows[seen]["kind"] == "ability" && rows[seen]["id"] == ActionId.Aetheroplasm.ToString() && Array.IndexOf(orbs, rows[seen]["actor"]) is var k and >= 0)
                    pops[k] = Flat(State(p).Objects.Plasmaspheres[k]!.Position);
        });
        return (run, orbs, holders, pops is [{ } first, { } second] ? Vector2.Distance(first, second) : float.NaN);
    }

    private static List<Dictionary<string, string>> Rows(uint abilityId) => Rows("ability", abilityId);

    // 9.6y apart: the two who set the orbs off die 0.35s after the second goes off; everyone else lives.
    [Test]
    public void OrbsGoingOffOverEachOtherKillTheirPlayers()
    {
        var (run, orbs, holders, _) = OrbOneWaitsAt(new Vector3(16f, 0f, 9f));
        var pops = Rows(ActionId.Aetheroplasm).Where(r => orbs.Contains(r["actor"])).Select(r => Number(r["t"])).ToList();
        var burst = Rows(ActionId.ElectromagneticBurst);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.Where(d => d.ActionId == ActionId.ElectromagneticBurst).Select(d => d.Role), Is.EquivalentTo(holders), run.ToString);
            Assert.That(run.Deaths.Select(d => d.Role), Is.EquivalentTo(holders), run.ToString);
            Assert.That(burst, Has.Count.EqualTo(1));
            Assert.That(burst.Select(r => r["detail"]).Single(), Does.StartWith($"hits={PerRole.All.Length}"));
            Assert.That(Number(burst.Single()["t"]) - pops.Max(), Is.EqualTo(0.35f).Within(0.02f));
        });
    }

    // Two 6y blasts overlap under 12y apart.
    [Test]
    public void OrbsElevenAndAHalfApartStillBurst()
    {
        var (run, _, holders, apart) = OrbOneWaitsAt(new Vector3(15.5f, 0f, 11.2f));
        Assert.Multiple(() =>
        {
            Assert.That(apart, Is.InRange(11.2f, 11.8f));
            Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EquivalentTo(holders.Select(h => (h, (uint?)ActionId.ElectromagneticBurst))), run.ToString);
        });
    }

    [Test]
    public void OrbsTwelveAndAHalfApartDoNot()
    {
        var (run, _, _, apart) = OrbOneWaitsAt(new Vector3(15.5f, 0f, 12.2f));
        Assert.Multiple(() =>
        {
            Assert.That(apart, Is.InRange(12.2f, 12.8f));
            Assert.That(Rows(ActionId.ElectromagneticBurst), Is.Empty);
            Assert.That(run.Passed, run.ToString);
        });
    }

    // Whichever orb has nobody else within its blast: its own player steps onto it.
    private static PartyRole OntoALoneOrb(ScenarioProbe p)
    {
        var state = State(p);
        var k = Enumerable.Range(0, state.OrbHolders.Count).MaxBy(i => PerRole.All.Where(r => r != state.OrbHolders[i])
            .Min(r => Vector2.Distance(Flat(p.Member(r)!.Position), Flat(state.Objects.Plasmaspheres[i]!.Position))));
        p.Member(state.OrbHolders[k])!.SetPosition(state.Objects.Plasmaspheres[k]!.Position);
        return state.OrbHolders[k];
    }

    // Before its first shrink at 43.96 the orb hits its DPS player past their HP.
    [Test]
    public void AnOrbSetOffBeforeItShrinksKillsItsPlayer()
    {
        PartyRole? holder = null;
        var run = Run(RegenHealer, 47f, p =>
        {
            if (p.Crossed(43f)) holder = OntoALoneOrb(p);
        });
        Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EqualTo(new[] { (holder!.Value, (uint?)ActionId.Aetheroplasm) }), run.ToString);
    }

    // A tank takes the unshrunk orb as a tank's hit; anyone else in its blast steps clear of it.
    [Test]
    public void ATankSetsOffItsOrbBeforeItShrinks()
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 44f,
            Overrides = o =>
            {
                var overrides = (TeaP3AlexanderPrimeStateOverrides)o;
                Pin(overrides);
                overrides.Orb[MainTank] = true;
                overrides.Orb[CasterDps] = false;
            },
            Takeovers = [new Takeover(43f, null)],
            Probe = p =>
            {
                if (!p.Crossed(43f)) return;
                var orb = State(p).Objects.Plasmaspheres[State(p).OrbHolders.ToList().IndexOf(MainTank)]!;
                foreach (var role in PerRole.All.Where(r => r != MainTank && Vector2.Distance(Flat(p.Member(r)!.Position), Flat(orb.Position)) < 7f))
                    p.Member(role)!.SetPosition(Vector3.Zero);
                p.Member(MainTank)!.SetPosition(orb.Position);
            },
        });
        Assert.Multiple(() =>
        {
            Assert.That(Rows(ActionId.Aetheroplasm), Has.Count.EqualTo(1));
            Assert.That(run.Passed, run.ToString);
        });
    }

    [Test]
    public void AnOrbSetOffAfterItsFirstShrinkSparesItsPlayer()
    {
        var run = Run(RegenHealer, 47f, p =>
        {
            if (p.Crossed(45f)) OntoALoneOrb(p);
        });
        Assert.Multiple(() =>
        {
            Assert.That(Rows(ActionId.Aetheroplasm), Has.Count.EqualTo(1));
            Assert.That(run.Passed, run.ToString);
        });
    }

    // Rows carry world positions, the scenario's own from the arena's centre.
    private static Vector2 At(Dictionary<string, string> row) => new(Number(row["x"]) - Origin.X, Number(row["z"]) - Origin.Z);

    private static readonly Vector3 Origin = AnoMech.Scenarios.Tea.TeaZone.P3.Zone.Origin;

    private static List<string> CrystalsNear(Vector2 at) => Rows("spawn", BNpcBaseId.JudgmentCrystal)
        .Where(r => Vector2.Distance(At(r), at) < 0.5f).Select(r => r["actor"]).ToList();

    // MT drops the east wall crystal 5.9y from OT's, the two zones overlapping, then waits the shatters
    // out by Alexander Prime: both go off on their own 0.86s after they spawn, telegraphed for 5.5s,
    // and the heart takes only the inner pair. 29y away, MT takes 3.3k from each.
    [Test]
    public void CrystalsDroppedTooCloseTogetherGoOffOnTheirOwn()
    {
        var run = Run(MainTank, 58f, null, false, new Takeover(48f, new(0.5f, -17.6f)), new Takeover(49.3f, new(-3.4f, 11.5f)));
        var crystals = Rows("spawn", BNpcBaseId.JudgmentCrystal).Select(r => r["actor"]).ToList();
        var wall = CrystalsNear(new(0.5f, -17.6f)).Concat(CrystalsNear(new(-5.4f, -17.6f))).ToList();
        var casts = Rows("cast", ActionId.TetrashatterFail);
        var blasts = Rows(ActionId.TetrashatterFail);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(wall, Has.Count.EqualTo(2));
            Assert.That(casts.Select(r => r["actor"]), Is.EquivalentTo(wall));
            Assert.That(blasts.Select(r => r["actor"]), Is.EquivalentTo(wall));
            Assert.That(Rows(ActionId.Tetrashatter).Select(r => r["actor"]), Is.EquivalentTo(crystals.Except(wall)));
            foreach (var cast in casts)
            {
                Assert.That(Number(cast["t"]), Is.EqualTo(50.72f).Within(0.02f));
                Assert.That(cast["detail"], Does.StartWith("cast=5.20s"));
            }
            foreach (var blast in blasts)
            {
                var at = Number(blast["t"]);
                Assert.That(at, Is.EqualTo(50.72f + 5.5f).Within(0.03f));
                var impact = Rows().FirstOrDefault(r => r["kind"] == "impact" && r["actor"] == blast["actor"] && r["id"] == blast["id"] && Number(r["t"]) >= at);
                Assert.That(impact, Is.Not.Null);
                if (impact is not null) Assert.That(Number(impact["t"]) - at, Is.EqualTo(0.91f).Within(0.03f));
            }
        });
    }

    // H2 drops the inner east crystal 5.8y from H1's and waits on the centre line: 13-14y from the
    // pair, either blast alone is past a healer's HP; 21y away, each comes to 3.3k.
    [TestCase(6f, true)]
    [TestCase(14f, false)]
    public void OverlappingCrystalsKillWhoeverWaitsNearThem(float z, bool dies)
    {
        var run = Run(ShieldHealer, 58f, null, false, new Takeover(48f, new(0.6f, -7f)), new Takeover(49.3f, new(0f, z)));
        Assert.Multiple(() =>
        {
            Assert.That(Rows("cast", ActionId.TetrashatterFail), Has.Count.EqualTo(2));
            Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EqualTo(dies ? new[] { (ShieldHealer, (uint?)ActionId.TetrashatterFail) } : []), run.ToString);
        });
    }

    // H1 and H2 drop the inner pair `apart`: the two 5y zones go off once they touch.
    [TestCase(9.99f, true)]
    [TestCase(10.05f, false)]
    public void CrystalsGoOffOnceTheirZonesTouch(float apart, bool goOff)
    {
        var run = Run(ShieldHealer, 52f, null, false, new Takeover(48.5f, new(-5f, -7f), Bot: RegenHealer), new Takeover(48.5f, new(apart - 5f, -7f)));
        var inner = Rows("spawn", BNpcBaseId.JudgmentCrystal).Select(At).Where(at => at.Y > -12f).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(inner, Has.Count.EqualTo(2));
            if (inner.Count == 2) Assert.That(Vector2.Distance(inner[0], inner[1]), Is.EqualTo(apart).Within(0.02f));
            Assert.That(Rows("cast", ActionId.TetrashatterFail), Has.Count.EqualTo(goOff ? 2 : 0), run.ToString);
        });
    }

    // The bots' four drops sit over 10.3y apart, clear of each other's zones, on either edge.
    [TestCase(false)]
    [TestCase(true)]
    public void TheBotsDropTheirCrystalsClearOfEachOther(bool alexanderPrimeNorth)
    {
        var run = Run(PhysRangedDps, 58f, null, alexanderPrimeNorth);
        var drops = Rows("spawn", BNpcBaseId.JudgmentCrystal).Select(At).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(drops, Has.Count.EqualTo(4));
            Assert.That(drops.SelectMany((a, i) => drops.Skip(i + 1).Select(b => Vector2.Distance(a, b))).Min(), Is.GreaterThan(10.3f));
            Assert.That(Rows("cast", ActionId.TetrashatterFail), Is.Empty);
            Assert.That(Rows(ActionId.Tetrashatter), Has.Count.EqualTo(4));
        });
    }

    private const string Overlapped = "Judgment Crystals dropped too close together";

    // H1 drops the inner west crystal 5.8y from H2's. With only the wall pair to take, the heart never
    // turns gold and goes into Alexander Prime on its last step at 57.96, and he stays unseen up to
    // Sacrament. His Plaints then kill Aggravated Assault and the stack, Surety lands 6 on its pair and
    // spares them, and Sacrament kills the two whose crystals overlapped; R, on a wing clear of it, lives.
    [Test]
    public void OverlappingCrystalsLoseTheRunAtSacrament()
    {
        string? alexanderPrime = null;
        var run = Run(PhysRangedDps, 86f, p => alexanderPrime ??= State(p).Objects.AlexanderPrime?.EntityId.ToString("X8"), false,
            new Takeover(48f, new(-0.6f, -7f), Bot: RegenHealer));
        var poweredUp = Rows("status_gain", StatusId.DamageUp);
        var warpsIn = Rows().Where(r => r["actor"] == alexanderPrime && r["kind"] == "ac" && r["id"] == "0x197"
                                        && r["p"] == $"0x{AnoMech.Core.Game.ActionTimelineId.WarpEnd:X}" && Number(r["t"]) is > 66.91f and < 81f);
        string Cause(PartyRole role) => run.Deaths.FirstOrDefault(d => d.Role == role)?.Cause ?? "";
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EquivalentTo(new (PartyRole, uint?)[]
            {
                (MeleeDpsA, ActionId.PlaintOfSeverity), (CasterDps, ActionId.PlaintOfSeverity),
                (MeleeDpsB, ActionId.PlaintOfSolidarity), (MainTank, ActionId.PlaintOfSolidarity), (OffTank, ActionId.PlaintOfSolidarity),
                (RegenHealer, ActionId.SacramentInception), (ShieldHealer, ActionId.SacramentInception),
            }), run.ToString);
            foreach (var role in new[] { MeleeDpsA, CasterDps, MeleeDpsB, MainTank, OffTank })
                Assert.That(Cause(role), Does.Contain($"the True Heart reached Alexander Prime ({Overlapped})"), $"{role}");
            foreach (var role in new[] { RegenHealer, ShieldHealer })
                Assert.That(Cause(role), Does.Contain("dropped their Judgment Crystal too close to another"), $"{role}");
            Assert.That(Rows("cast", ActionId.TetrashatterFail), Has.Count.EqualTo(2));
            Assert.That(Rows(ActionId.Tetrashatter), Has.Count.EqualTo(2));
            Assert.That(Rows("status_gain", StatusId.EnigmaCodexHeart), Is.Empty, "the heart turned gold");
            Assert.That(Rows(ActionId.TrueHeartSuccess), Is.Empty);
            Assert.That(Rows("status_gain", StatusId.EnigmaCodex), Is.Empty);
            Assert.That(poweredUp, Has.Count.EqualTo(1));
            if (poweredUp.Count == 1) Assert.That(Number(poweredUp[0]["t"]), Is.EqualTo(57.96f).Within(0.03f));
            Assert.That(Rows("status_gain", StatusId.VulnerabilityDownAlexanderPrime).Select(r => r["actor"]), Is.EqualTo(poweredUp.Select(r => r["actor"])));
            Assert.That(Rows(ActionId.PlaintOfSurety).Select(r => r["detail"]), Has.Exactly(1).StartWith("hits=2"));
            Assert.That(Rows(ActionId.PlaintOfSurety).Select(r => r["amounts"]), Is.EqualTo(new[] { "6 6" }));
            Assert.That(alexanderPrime, Is.Not.Null);
            Assert.That(warpsIn, Is.Empty, "Alexander Prime warped in where Sacrament would come from");
        });
    }

    // Without the gold heart the bots can but guess Sacrament's spot, and take the one just west of
    // where Alexander Prime stood for Inception. Rolled there, R, beside the healers, stands clear;
    // rolled 90 degrees off, Sacrament's line runs through that wing and R dies in it with them.
    [TestCase(4, false)]
    [TestCase(6, true)]
    public void WithoutTheGoldHeartTheBotsGuessSacramentsSpot(int spot, bool rangedDies)
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = PhysRangedDps,
            StopAt = 86f,
            Overrides = o =>
            {
                var overrides = (TeaP3AlexanderPrimeStateOverrides)o;
                Pin(overrides);
                overrides.SacramentSpot = spot;
            },
            Takeovers = [new Takeover(48f, new(-0.6f, -7f), Bot: RegenHealer)],
        });
        PartyRole[] sacrament = rangedDies ? [RegenHealer, ShieldHealer, PhysRangedDps] : [RegenHealer, ShieldHealer];
        Assert.That(run.Deaths.Where(d => d.ActionId == ActionId.SacramentInception).Select(d => d.Role), Is.EquivalentTo(sacrament), run.ToString);
    }

    // H1 walks into the wall before the drop: none of the three crystals down goes off for it.
    [Test]
    public void AMissingCrystalSetsNoneOff()
    {
        var run = Run(RegenHealer, 53f, null, false, new Takeover(47f, new(0f, 20.5f)));
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EqualTo(new[] { (RegenHealer, (uint?)TheEnvironment) }), run.ToString);
            Assert.That(run.Elapsed, Is.GreaterThan(CrystalsSpawn + 1f), run.ToString);
            Assert.That(Rows("spawn", BNpcBaseId.JudgmentCrystal), Has.Count.EqualTo(3));
            Assert.That(Rows("cast", ActionId.TetrashatterFail), Is.Empty);
        });
    }

    private const float CrystalsSpawn = 49.86f;

    // MT drops the east wall crystal 8.5y from Alexander Prime, where it waits: it goes off as the heart
    // sets out, before the heart gets to it.
    [Test]
    public void ACrystalDroppedByAlexanderPrimeGoesOffAsTheHeartSetsOut()
    {
        var run = Run(MainTank, 56f, null, false, new Takeover(48.9f, new(-4f, 14f)));
        var casts = Rows("cast", ActionId.TetrashatterFail);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(casts.Select(r => r["actor"]), Is.EqualTo(CrystalsNear(new(-4f, 14f))));
            if (casts.Count == 1) Assert.That(Number(casts[0]["t"]), Is.EqualTo(55.0f).Within(0.02f));
            Assert.That(Rows(ActionId.Tetrashatter), Has.Count.EqualTo(3));
        });
    }

    // H2 drops the inner east crystal out at 8.5y, past the heart's reach. 11.5y from Alexander Prime's
    // Sacrament spot it goes off as he moves there, after the heart would have flown; with the spot
    // rolled one on, 18.6y away, it stays.
    [TestCase(1, true)]
    [TestCase(2, false)]
    public void ACrystalTheHeartMissedGoesOffOnlyBySacramentsSpot(int spot, bool goesOff)
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = PhysRangedDps,
            StopAt = 73f,
            Overrides = o =>
            {
                var overrides = (TeaP3AlexanderPrimeStateOverrides)o;
                Pin(overrides);
                overrides.SacramentSpot = spot;
            },
            Takeovers = [new Takeover(48f, new(8.5f, -6.5f), Bot: ShieldHealer)],
        });
        var missed = CrystalsNear(new(8.5f, -6.5f));
        var casts = Rows("cast", ActionId.TetrashatterFail);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(missed, Has.Count.EqualTo(1));
            Assert.That(casts.Select(r => r["actor"]), Is.EqualTo(goesOff ? missed : []));
            if (casts.Count == 1) Assert.That(Number(casts[0]["t"]), Is.EqualTo(72.65f).Within(0.02f));
            Assert.That(Rows(ActionId.Tetrashatter).Select(r => r["actor"]), Has.None.EqualTo(missed.FirstOrDefault()));
            Assert.That(Rows(ActionId.Tetrashatter), Has.Count.EqualTo(3));
        });
    }

    // H1, a player on a peer, drops the inner west crystal 5.8y from H2's: the pair's blasts, the
    // powered-up Plaints and a Sacrament nothing showed reach the peer's game as the host's has them.
    [Test]
    public void APeerSeesCrystalsDroppedTooCloseTogetherAsTheHostDoes()
    {
        var run = MultiplayerRun.Play(typeof(TeaP3AlexanderPrimeScenario), 0, 1, new PlayerSeat(MeleeDpsA), [new PlayerSeat(RegenHealer, BotControlled: false)],
            immortal: true,
            inputs: [(40.0, 1, c => c.Fake.BattleCharas.Player.Position = Origin + new Vector3(-0.6f, 0f, -7f))],
            configure: c =>
            {
                if (c.IsHost) Pin((TeaP3AlexanderPrimeStateOverrides)c.Game.Catalog.Scenarios.OfType<TeaP3AlexanderPrimeScenario>().Single().SettingsOverrides);
            });
        var host = run.Host.Fake.Timeline.Rows;
        var gaps = ClientParity.Compare(run, run.Peers[0]);
        foreach (var gap in gaps.Take(40)) TestContext.Out.WriteLine($"  {gap}");
        Assert.Multiple(() =>
        {
            Assert.That(run.Failure, Is.Null);
            Assert.That(run.InputsLeft, Is.Zero);
            Assert.That(run.Relay.Rejected, Is.Empty, "messages the relay or a peer refused");
            Assert.That(host.Count(r => r["kind"] == "cast" && r["id"] == ActionId.TetrashatterFail.ToString()), Is.EqualTo(2));
            Assert.That(host.Count(r => r["kind"] == "ability" && r["id"] == ActionId.TrueHeartSuccess.ToString()), Is.Zero);
            Assert.That(host.Count(r => r["kind"] == "ability" && r["id"] == ActionId.SacramentInception.ToString()), Is.EqualTo(1));
            Assert.That(run.Peers.Prepend(run.Host).SelectMany(c => c.Log.Warnings).Where(w => w.Contains("[SimAssets]")), Is.Empty);
            Assert.That(gaps, Is.Empty, $"{gaps.Count} differences between what the host's and the peer's games were told");
        });
    }

    // H1 waits the shatters out in the middle, 14y from each inner crystal: about 20k from each, which
    // only adds up past a healer's HP.
    [Test]
    public void ShattersAddUpOnANonTank()
        => AlexanderPrime(RegenHealer)
            .TeleportAt(54f, to: new(0, 6))
            .ShouldKill(ActionId.Tetrashatter, RegenHealer);

    // OT waits them out further in, about 55k in all: past a tank's 46.5k without 16% mitigation.
    [Test]
    public void ATankWithoutMitigationDiesWaitingInTheMiddle()
        => AlexanderPrime(OffTank)
            .WithMitigationChecks()
            .TeleportAt(54f, to: new(0, 4))
            .ShouldKill(ActionId.Tetrashatter, OffTank);

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void ATankMitigatesThroughTheMiddle(bool mitigationChecks, bool rampart)
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = OffTank,
            StopAt = 57f,
            UserActionsEnabled = mitigationChecks,
            Overrides = o => Pin((TeaP3AlexanderPrimeStateOverrides)o),
            Takeovers = [new Takeover(54f, new(0f, 4f))],
            Probe = p => { if (rampart && p.Crossed(54.5f)) p.Member(OffTank)!.AddStatus(1191, 20f); },
        });
        Assert.That(run.Passed, run.ToString);
    }

    // Real non-tanks wait the shatters out by Alexander Prime, the tanks a little ahead of them: nobody's
    // four add up to their HP.
    [TestCase(false)]
    [TestCase(true)]
    public void BotsWaitOutTheShattersByAlexanderPrime(bool alexanderPrimeNorth)
    {
        var taken = PerRole.All.ToDictionary(r => r, _ => 0f);
        var shatters = 0;
        var seen = 0;
        var southward = new Dictionary<PartyRole, float>();
        var run = Run(PhysRangedDps, 58f, p =>
        {
            var rows = Rows();
            for (; seen < rows.Count; seen++)
            {
                if (rows[seen]["kind"] != "ability" || rows[seen]["id"] != ActionId.Tetrashatter.ToString()) continue;
                shatters++;
                var at = At(rows[seen]);
                foreach (var role in PerRole.All) taken[role] += ShatterDamage(Vector2.Distance(Flat(p.Member(role)!.Position), at));
            }
            if (p.Crossed(55.8f))
                foreach (var role in PerRole.All.Where(r => !r.IsTank()))
                    southward[role] = alexanderPrimeNorth ? -p.Member(role)!.Position.Z : p.Member(role)!.Position.Z;
        }, alexanderPrimeNorth);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(shatters, Is.EqualTo(4));
            foreach (var (role, z) in southward) Assert.That(z, Is.GreaterThanOrEqualTo(12.5f), $"{role} at the inner shatter");
            foreach (var role in PerRole.All) Assert.That(taken[role], Is.LessThan(HitPoints(role)), $"{role}'s four shatters");
        });
    }

    // From 54.55 at 10y a second, looking for crystals every 0.31s: the wall pair goes on the first step,
    // the inner pair on the fourth.
    [Test]
    public void TheHeartWalksTenYalmsASecondInSteps()
    {
        float? heartZ = null;
        var run = Run(PhysRangedDps, 58f, p =>
        {
            if (p.Crossed(55f)) heartZ = State(p).Objects.TrueHeart?.Position.Z;
        });
        var shatters = Rows(ActionId.Tetrashatter).Select(r => Number(r["t"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(heartZ, Is.EqualTo(-21f + 4.5f).Within(0.2f));
            Assert.That(shatters, Has.Count.EqualTo(4));
            foreach (var t in shatters) Assert.That(OffStep(t - WalkStarts), Is.LessThan(0.02f), $"shatter at {t:F3}");
            Assert.That(shatters.Min(), Is.EqualTo(WalkStarts + HeartStep).Within(0.02f));
            Assert.That(shatters.Max(), Is.EqualTo(WalkStarts + 4 * HeartStep).Within(0.02f));
        });
    }

    // The visual goes out on the fourth shatter, the heart turns gold 0.58s later, and each shatter
    // stacks it a Vulnerability Down 0.76s on.
    [Test]
    public void TheHeartTurnsGoldAndWeakensOnTheShattersBeat()
    {
        string? heart = null;
        var stacks = new List<(float Time, int Stacks)>();
        var run = Run(PhysRangedDps, 58f, p =>
        {
            if (State(p).Objects.TrueHeart is not { } trueHeart) return;
            heart ??= trueHeart.EntityId.ToString("X8");
            int now = trueHeart.FindStatus(StatusId.VulnerabilityDown)?.Stacks ?? 0;
            if (now != (stacks.Count > 0 ? stacks[^1].Stacks : 0)) stacks.Add((p.Time, now));
        });
        var shatters = Rows(ActionId.Tetrashatter).Select(r => Number(r["t"])).ToList();
        var visual = Rows(ActionId.TetrashatterVisual);
        var gold = Rows("status_gain", StatusId.EnigmaCodexHeart).Where(r => r["actor"] == heart).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(visual, Has.Count.EqualTo(1));
            Assert.That(Number(visual.Single()["t"]), Is.EqualTo(shatters.Max()).Within(0.02f));
            Assert.That(gold, Has.Count.EqualTo(1));
            Assert.That(Number(gold.Single()["t"]) - shatters.Max(), Is.EqualTo(0.58f).Within(0.03f));
            Assert.That(stacks.Select(s => s.Stacks), Is.EqualTo(new[] { 2, 4 }));
            foreach (var (time, count) in stacks)
                Assert.That(time - shatters.Order().ElementAt(count - 1), Is.EqualTo(0.76f).Within(0.03f), $"stack {count}");
        });
    }

    // H1 stays on the inner west crystal it dropped.
    [Test]
    public void StandingOnAShatteringCrystalDies()
        => AlexanderPrime(RegenHealer)
            .FreezeAt(49.1f)
            .ShouldKill(ActionId.Tetrashatter, RegenHealer);

    // H1 takes OT's flame beside OT, then goes back to the party for the third.
    [Test]
    public void SecondFlarethrowerKills()
        => AlexanderPrime(RegenHealer)
            .TeleportAt(62.3f, to: new(-14.6f, 1.2f))
            .TeleportAt(63.6f, to: new(-15.4f, 8.9f))
            .ShouldKill(ActionId.FlarethrowerP3, RegenHealer);

    // R joins MT's side; Shared Sentence's stack there just takes one more.
    [Test]
    public void RestrainingOrderPartnersTooCloseDie()
        => AlexanderPrime(PhysRangedDps)
            .TeleportAt(79.4f, to: new(-6f, -14f))
            .ShouldKill(ActionId.PlaintOfSurety, MainTank, PhysRangedDps);

    [Test]
    public void StandingInSacramentDies()
        => AlexanderPrime(RegenHealer)
            .TeleportAt(79f, to: new(0, 0))
            .ShouldKill(ActionId.SacramentInception, RegenHealer);

    private static IEnumerable<TestCaseData> SacramentSpots()
        => from spot in Enumerable.Range(0, TeaP3AlexanderPrimeState.SacramentSpots)
           from north in new[] { false, true }
           select new TestCaseData(spot, north).SetArgDisplayNames($"spot {spot}", north ? "Alexander Prime north" : "Alexander Prime south");

    // Shared Sentence takes the stack on the true left wing; the bots, reading where the heart flies
    // from where it stopped, join it there.
    [TestCaseSource(nameof(SacramentSpots))]
    public void TheBotsStackWithSharedSentenceOnTheTrueLeftWing(int spot, bool alexanderPrimeNorth)
    {
        var left = Geometry.OnCompass(22.5f + 45f * spot - 90f, 16.5f);
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 81.5f,
            Overrides = o =>
            {
                var overrides = (TeaP3AlexanderPrimeStateOverrides)o;
                Pin(overrides);
                overrides.AlexanderPrimeNorth = alexanderPrimeNorth;
                overrides.SacramentSpot = spot;
            },
            Takeovers = [new Takeover(77.5f, new(left.X, left.Z))],
        });
        Assert.That(run.Passed, run.ToString);
    }

    // With Alexander Prime north the healers keep their sides: the regen healer takes the sword true
    // north, the shield healer true south.
    [Test]
    public void TheHealersBaitTheSwordsOnTheSameSidesWhicheverEdgeAlexanderPrimeTakes()
    {
        Vector2? regen = null, shield = null;
        var run = Run(PhysRangedDps, 89f, p =>
        {
            if (!p.Crossed(86f)) return;
            regen = Flat(p.Member(RegenHealer)!.Position);
            shield = Flat(p.Member(ShieldHealer)!.Position);
        }, alexanderPrimeNorth: true);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(regen!.Value.Y, Is.LessThan(0f), "the regen healer");
            Assert.That(shield!.Value.Y, Is.GreaterThan(0f), "the shield healer");
        });
    }

    [Test]
    public void AShieldHealerBaitingTrueSouthWithAlexanderPrimeNorthSurvivesTheSwords()
    {
        var run = Run(ShieldHealer, 89f, null, true, new Takeover(85.5f, new(0f, 2.3f)));
        Assert.That(run.Passed, run.ToString);
    }

    // M1 still carries Severity's Physical Vulnerability Up and baits the jump out east instead of OT,
    // who has stepped back to the stack.
    [Test]
    public void SuperJumpOnAVulnerableNonTankKills()
        => AlexanderPrime(MeleeDpsA)
            .MoveBotAt(88.6f, OffTank, new(7.0f, -3.0f))
            .TeleportAt(88.6f, to: new(19f, 0f))
            .ShouldKill(ActionId.SuperJumpLanding, MeleeDpsA);

    // The client plays a slash along the model's facing: each sword goes out on Cruise Chaser himself,
    // his model on screen already turned along it.
    [Test]
    public void EachSwordFollowsCruiseChasersTurnToItsBait()
    {
        string? cruiseChaser = null;
        var run = Run(RegenHealer, 89f, p => cruiseChaser ??= State(p).Objects.CruiseChaser?.EntityId.ToString("X8"));
        Assert.That(run.Passed, run.ToString);
        var rows = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;
        var swords = rows.Where(r => r["actor"] == cruiseChaser && r["kind"] == "ability" && r["id"] == ActionId.AlphaSwordP3.ToString()).ToList();
        Assert.That(swords, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(swords.Select(s => s["targets"]).Distinct().Count(), Is.EqualTo(3), "each sword on its own player");
            Assert.That(swords.Select(s => s["target"]), Is.All.EqualTo(cruiseChaser));
            TeaShown.FacesEachHitOnScreen(rows, cruiseChaser!, [ActionId.AlphaSwordP3], "Cruise Chaser");
        });
    }

    private static float Number(string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static float Angle(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau));

    private static float FacingFrom(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    // Brute Justice's model turns round to each flame's bait at a boss's 480 degrees a second from the
    // wall's due east, the first turn at 60.25 and each next one 2.02s after the last flame, and he fires
    // on himself once facing them: MT, OT, then the party. The times land up to two frames late.
    [Test]
    public void EachFlarethrowerFollowsBruteJusticesTurnToItsBait() => FlarethrowersFollowTheirTurns(PhysRangedDps);

    // MT baits the first flame from farther round toward the north wall, a turn real clears take:
    // each later flame comes as much later as the turns before it took longer. Nobody dies to it.
    [Test]
    public void AWideFirstFlameTurnPushesTheLaterFlamesBack()
    {
        var bots = FlarethrowersFollowTheirTurns(PhysRangedDps);
        var wide = FlarethrowersFollowTheirTurns(MainTank, new Takeover(59.5f, new(-18.87f, -5.39f)));
        Assert.That(wide.Turns[0] - bots.Turns[0], Is.GreaterThan(0.2f), "MT's wider first turn");
        for (var k = 1; k < 3; k++)
            Assert.That(wide.Times[k] - bots.Times[k], Is.EqualTo((wide.Turns.Take(k + 1).Sum() - bots.Turns.Take(k + 1).Sum()) / (MathF.PI * 8f / 3f))
                .Within(2 * ScenarioRun.FrameSeconds), $"flame {k + 1} pushed back by every turn up to it");
    }

    // Each flame's turn in radians, and its time.
    private static (float[] Turns, float[] Times) FlarethrowersFollowTheirTurns(PartyRole player, params Takeover[] takeovers)
    {
        string? bruteJustice = null;
        var ids = new Dictionary<PartyRole, string>();
        var frames = new List<(float T, Dictionary<string, float> Facings)>();
        var run = Run(player, 66f, p =>
        {
            if (p.Time < 60f) return;
            var robot = State(p).Objects.BruteJustice!;
            bruteJustice = robot.EntityId.ToString("X8");
            foreach (var role in PerRole.All) ids[role] = p.Member(role)!.EntityId.ToString("X8");
            frames.Add((p.Time, PerRole.All.ToDictionary(r => p.Member(r)!.EntityId.ToString("X8"), r => FacingFrom(robot.Position, p.Member(r)!.Position))));
        }, false, takeovers);
        Assert.That(run.Passed, run.ToString);
        var rows = Rows().Where(r => r["actor"] == bruteJustice).ToList();
        var flames = rows.Where(r => r["kind"] == "ability" && r["id"] == ActionId.FlarethrowerP3.ToString()).ToList();
        Assert.That(flames, Has.Count.EqualTo(3));
        var (turns, times) = (new float[3], new float[3]);
        var facing = MathF.PI / 2f;
        var turnAt = 60.25f;
        for (var k = 0; k < flames.Count; k++)
        {
            var (flame, at, before) = (flames[k], turnAt, facing);
            var facings = frames.First(f => f.T >= at).Facings;
            var rot = Number(flame["rot"]);
            var flameAt = at + Angle(before, rot) / (MathF.PI * 8f / 3f);
            var bait = k switch { 0 => ids[MainTank], 1 => ids[OffTank], _ => flame["targets"].Split(' ').MinBy(t => Angle(facings[t], rot))! };
            Assert.Multiple(() =>
            {
                Assert.That(flame["target"], Is.EqualTo(bruteJustice));
                Assert.That(flame["targets"].Split(' '), Does.Contain(bait), $"flame {k + 1}'s bait");
                Assert.That(Angle(rot, facings[bait]), Is.LessThan(0.011f), $"flame {k + 1} aimed at its bait");
                Assert.That(Number(flame["t"]), Is.EqualTo(flameAt).Within(2 * ScenarioRun.FrameSeconds), $"flame {k + 1} once facing");
                TeaShown.TurnsRoundOnScreen(rows, bruteJustice!, at, Number(flame["t"]), before, rot, $"flame {k + 1}");
            });
            (turns[k], times[k]) = (Angle(before, rot), Number(flame["t"]));
            facing = rot;
            turnAt = flameAt + 2.02f;
        }
        Assert.That(flames[2]["targets"].Split(' '), Has.Length.GreaterThan(1), "the party's flame burns the stack");
        return (turns, times);
    }

    // Every number's hit, Alpha Sword or charge, goes out on Cruise Chaser himself, his model on screen
    // already turned along it.
    [Test]
    public void EachLimitCutHitFollowsCruiseChasersTurnToItsBait()
    {
        string? cruiseChaser = null;
        var ids = new Dictionary<PartyRole, string>();
        var run = Run(RegenHealer, 157f, p =>
        {
            cruiseChaser ??= State(p).Objects.CruiseChaser?.EntityId.ToString("X8");
            if (ids.Count == 0) foreach (var role in PerRole.All) ids[role] = p.Member(role)!.EntityId.ToString("X8");
        });
        Assert.That(run.Passed, run.ToString);
        var rows = Rows();
        var hits = rows.Where(r => r["actor"] == cruiseChaser && r["kind"] == "ability"
                                   && (r["id"] == ActionId.AlphaSword.ToString() || r["id"] == ActionId.SuperBlasstyChargeP3.ToString())).ToList();
        Assert.That(hits, Has.Count.EqualTo(8));
        Assert.Multiple(() =>
        {
            Assert.That(hits.Select(h => h["id"]), Is.EqualTo(Enumerable.Range(0, 8).Select(k => (k % 2 == 0 ? ActionId.AlphaSword : ActionId.SuperBlasstyChargeP3).ToString())));
            Assert.That(hits.Select(h => h["targets"]), Is.EqualTo(PinnedNumbers.Select(r => ids[r])), "each hit on its number alone");
            Assert.That(hits.Select(h => h["target"]), Is.All.EqualTo(cruiseChaser));
            TeaShown.FacesEachHitOnScreen(rows, cruiseChaser!, [ActionId.AlphaSword, ActionId.SuperBlasstyChargeP3], "Cruise Chaser");
        });
    }

    // OT, #5, steps between Cruise Chaser and M2 as M2 takes the charge for #2.
    [Test]
    public void StandingInAnotherNumbersChargeDies()
        => AlexanderPrime(OffTank)
            .FreezeAt(143.0f)
            .At(143.1f, p => p.Member(OffTank)!.SetPosition((State(p).Objects.CruiseChaser!.Position + p.Member(MeleeDpsB)!.Position) / 2f))
            .ShouldKill(ActionId.SuperBlasstyChargeP3, OffTank);

    // #5 waits on the west wall past the first soak, 11.7y from the wormhole.
    [Test]
    public void UnsoakedWormholeWipesTheParty()
        => AlexanderPrime(OffTank)
            .TeleportAt(141f, to: new(-18.5f, 4.5f))
            .ShouldKill(ActionId.Confession, PerRole.All);

    // #2 still carries the charge's Magic Vulnerability Up and steps into the east wormhole beside #6.
    [Test]
    public void SoakingOnMagicVulnerabilityDies()
        => AlexanderPrime(MeleeDpsB)
            .TeleportAt(146.2f, to: new(13.5f, 3f))
            .ShouldKill(ActionId.Repentance1, MeleeDpsB);

    // #1 stays north instead of joining the stack: either C is the stack and alone, or the seven without
    // C are short of eight.
    [Test]
    public void IncineratingHeatShortOfEightKills()
        => AlexanderPrime(CasterDps)
            .TeleportAt(158f, to: new(0, -15f))
            .ShouldKillSomeone(ActionId.IncineratingHeat);

    [Test]
    public void CruiseChaserSurvivingEternalDarknessWipes()
        => Negative<TeaP3AlexanderPrimeScenario>(MainTank)
            .Overrides<TeaP3AlexanderPrimeStateOverrides>(o =>
            {
                Pin(o);
                o.CruiseChaserSurvives = true;
            })
            .ShouldKill(ActionId.EternalDarknessP3, PerRole.All);

    [Test]
    public void AlexanderPrimeSurvivingDivineJudgmentWipes()
        => Negative<TeaP3AlexanderPrimeScenario>(MainTank)
            .Overrides<TeaP3AlexanderPrimeStateOverrides>(o =>
            {
                Pin(o);
                o.AlexanderPrimeSurvives = true;
            })
            .ShouldKill(ActionId.DivineJudgmentEnrage, PerRole.All);

    // H1's crystal (inner west) bleeds from 54.69s until the heart shatters the inner pair at 55.79s;
    // still on it at a tick at 55.5s, H1 bleeds out before the shatter.
    [Test]
    public void StayingOnYourCrystalAtATickBleedsOut()
        => Negative<TeaP3AlexanderPrimeScenario>(RegenHealer)
            .Overrides<TeaP3AlexanderPrimeStateOverrides>(o =>
            {
                Pin(o);
                o.TickClock = 1.5f;
            })
            .FreezeAt(49.3f)
            .ShouldKill(TheEnvironment, RegenHealer);

    // He falls at 272.14s and the last hit lands at 287.73s; the player as main tank presses no LB3.
    [Test]
    public void DivineJudgmentWithoutATankLimitBreakKills()
        => AlexanderPrime(MainTank)
            .WithMitigationChecks()
            .FreezeAt(274f)
            .ShouldKill(ActionId.DivineJudgmentRaidwide, MainTank);

    private static readonly string[] TankLimitBreaks = ["199", "4240", "4241", "17105"];
    private static readonly string[] TankLimitBreakStatuses = ["196", "863", "864", "1931"];

    // The bot main tank's LB3 on the countdown covers a player who froze after the fall: pressed on
    // three, its status lands on the tank 1.27s on, then on each of the others 0.145s after the one
    // before, and lasts through the hit 15.46s after the success.
    [Test]
    public void TheBotTanksLimitBreakCarriesThePartyThroughDivineJudgment()
    {
        string? mainTank = null;
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = RegenHealer,
            UserActionsEnabled = true,
            StopAt = 290f,
            WriteArtifactsOnFailure = false,
            Overrides = o => Pin((TeaP3AlexanderPrimeStateOverrides)o),
            Takeovers = [new Takeover(274f, null)],
            Probe = p => mainTank ??= p.Member(MainTank)!.EntityId.ToString("X8"),
        });
        var limitBreaks = Rows().Where(r => r["kind"] == "ability" && TankLimitBreaks.Contains(r["id"])).ToList();
        var lands = Rows().Where(r => r["kind"] == "status_gain" && TankLimitBreakStatuses.Contains(r["id"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(limitBreaks, Has.Count.EqualTo(1));
            Assert.That(lands.Select(r => r["actor"]).Distinct().Count(), Is.EqualTo(PerRole.All.Length));
            Assert.That(lands, Has.Count.EqualTo(PerRole.All.Length));
            if (limitBreaks.Count != 1 || lands.Count == 0) return;
            Assert.That(limitBreaks.Single()["actor"], Is.EqualTo(mainTank));
            var pressed = Number(limitBreaks.Single()["t"]);
            Assert.That(pressed - Success, Is.InRange(7.1f, 7.5f));
            Assert.That(lands[0]["actor"], Is.EqualTo(mainTank));
            for (var k = 0; k < lands.Count; k++)
                Assert.That(Number(lands[k]["t"]) - pressed, Is.EqualTo(1.27f + 0.145f * k).Within(0.02f), $"landing {k}");
        });
    }

    // What the client's LB3 does for the player: the action's JobActions effects, then the resolve
    // that spends the gauge.
    private static void PressLimitBreak(ScenarioProbe p, PartyRole role)
    {
        var tank = (SimPlayer)p.Member(role)!;
        var limitBreak = LimitBreakHandler.ActionId(tank.ClassJob, 3);
        JobActions.ApplyEffects(tank, limitBreak, (ulong)tank.GameObjectId, Random.Shared);
        LimitBreakHandler.Resolve(p.Game.World.Party, tank, limitBreak);
    }

    // At the earliest real press that lived and at the median one: the tank's own status, the first to
    // land, outlasts the hit.
    [TestCase(6.4f)]
    [TestCase(7.2f)]
    public void AMainTankPressingLimitBreakOnTheCountdownLivesThroughDivineJudgment(float pressedAfterSuccess)
    {
        var run = ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            UserActionsEnabled = true,
            StopAt = 290f,
            Overrides = o => Pin((TeaP3AlexanderPrimeStateOverrides)o),
            Takeovers = [new Takeover(274f, null)],
            Probe = p => { if (p.Crossed(Success + pressedAfterSuccess)) PressLimitBreak(p, MainTank); },
        });
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(Rows(ActionId.DivineJudgmentRaidwide), Has.Count.EqualTo(1));
        });
    }

    // Pressed before four, the tank's own status, the first on and so the first off, is gone by the hit.
    [Test]
    public void ALimitBreakPressedBeforeFourRunsOutOnItsTank()
        => AlexanderPrime(MainTank)
            .WithMitigationChecks()
            .FreezeAt(274f)
            .At(Success + 6.1f, p => PressLimitBreak(p, MainTank))
            .ShouldKill(ActionId.DivineJudgmentRaidwide, MainTank);

    // The countdown from a helper named Alexander, its lines from the older row, and the last blow from
    // the hidden Alexander 15.46s after the success, leaving everyone Down for the Count.
    [Test]
    public void DivineJudgmentCountsDownToTheHiddenAlexandersBlow()
    {
        var run = Run(PhysRangedDps);
        var spawns = Rows().Where(r => r["kind"] == "spawn").ToList();
        var named = spawns.Where(r => r["detail"].Contains($"bnpcname={BNpcNameId.Alexander} ")).Select(r => r["actor"]).ToList();
        var hidden = spawns.Single(r => r["id"] == BNpcBaseId.Alexander.ToString())["actor"];
        var countdown = Rows("cast", ActionId.DivineJudgmentCountdown);
        var countdownLands = Rows(ActionId.DivineJudgmentCountdown);
        var blow = Rows(ActionId.DivineJudgmentRaidwide);
        var downs = Rows("status_gain", StatusId.DownForTheCount);
        (uint Line, float After, string Ms)[] lines =
        [
            (BattleTalkId.ExecutingJudgment, 0.75f, "4000"), (BattleTalkId.FiveSeconds, 5.69f, "1000"), (BattleTalkId.FourSeconds, 6.70f, "1000"),
            (BattleTalkId.ThreeSeconds, 7.60f, "1000"), (BattleTalkId.TwoSeconds, 8.69f, "1000"), (BattleTalkId.OneSecond, 9.62f, "1000"),
        ];
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(named, Has.Count.EqualTo(2).And.Contains(hidden));
            Assert.That(countdown, Has.Count.EqualTo(1));
            Assert.That(named, Contains.Item(countdown.Single()["actor"]));
            Assert.That(countdown.Single()["actor"], Is.Not.EqualTo(hidden));
            Assert.That(Number(countdown.Single()["t"]) - Success, Is.EqualTo(0.75f).Within(0.03f));
            Assert.That(countdown.Single()["detail"], Does.StartWith("cast=4.70s"));
            Assert.That(countdownLands.Single()["anim_lock"], Is.EqualTo("13.40"));
            foreach (var (line, after, ms) in lines)
            {
                var talk = Rows("battle_talk", line);
                Assert.That(talk, Has.Count.EqualTo(1), $"line {line}");
                if (talk.Count != 1) continue;
                Assert.That(Number(talk[0]["t"]) - Success, Is.EqualTo(after).Within(0.05f), $"line {line}");
                Assert.That(talk[0]["detail"], Is.EqualTo($"speaker={BNpcNameId.AlexanderCountdown} duration_ms={ms}"), $"line {line}");
            }
            Assert.That(blow, Has.Count.EqualTo(1));
            Assert.That(blow.Single()["actor"], Is.EqualTo(hidden));
            Assert.That(blow.Single()["anim_lock"], Is.EqualTo("3.10"));
            Assert.That(Number(blow.Single()["t"]) - Success, Is.EqualTo(15.46f).Within(0.03f));
            Assert.That(downs, Has.Count.EqualTo(PerRole.All.Length));
            foreach (var down in downs)
            {
                Assert.That(Number(down["t"]) - Number(blow.Single()["t"]), Is.EqualTo(0.18f).Within(0.03f));
                Assert.That(down["detail"], Does.StartWith("dur=57.0"));
            }
        });
    }

    private static readonly float[] AlexanderPrimeSwings =
        [14.25f, 21.57f, 24.60f, 92.57f, 95.60f, 103.66f, 106.69f, 113.03f, 116.07f, 165.82f, 168.85f, 175.89f, 182.94f, 185.97f];

    // None before the first Chastening Heat, and each lands even 2.0s after an action whose lock runs 2.1s.
    [Test]
    public void AlexanderPrimeSwingsWhenTheRealOneDoes()
    {
        string? alexanderPrime = null;
        var run = Run(PhysRangedDps, 200f, p => alexanderPrime ??= State(p).Objects.AlexanderPrime?.EntityId.ToString("X8"));
        var swings = Rows(ActionId.AlexanderPrimeAttack).Where(r => r["actor"] == alexanderPrime).Select(r => Number(r["t"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(swings, Has.Count.EqualTo(AlexanderPrimeSwings.Length));
            foreach (var t in AlexanderPrimeSwings) Assert.That(swings.Min(s => MathF.Abs(s - t)), Is.LessThan(0.05f), $"the swing at {t}");
        });
    }

    [Test]
    public void TheJumpsTheRayAndTheLastBlowLockAsLongAsTheRealOnes()
    {
        var run = Run(PhysRangedDps);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            foreach (var (id, count, animationLock) in new[]
                     {
                         (ActionId.SuperJump, 2, "0.20"), (ActionId.SuperJumpLanding, 2, "2.10"), (ActionId.ApocalypticRay, 1, "7.10"),
                         (ActionId.ApocalypticRayHit, 5, "1.10"), (ActionId.DivineJudgmentRaidwide, 1, "3.10"),
                     })
            {
                var rows = Rows(id);
                Assert.That(rows, Has.Count.EqualTo(count), $"{id}");
                Assert.That(rows.Select(r => r["anim_lock"]), Is.All.EqualTo(animationLock), $"{id}");
            }
        });
    }

    // Each hit's EffectResult trails its ActionEffect as in real pulls: a cast's ActionEffect comes 0.3s
    // after its bar ends.
    [Test]
    public void EachHitLandsAsLongAfterItsActionEffectAsInRealPulls()
    {
        var run = Run(PhysRangedDps);
        var rows = Rows();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            foreach (var (id, lag) in new[]
                     {
                         (ActionId.ChasteningHeat, 0.80f), (ActionId.DivineSpear, 0.80f), (ActionId.SuperJumpLanding, 0.98f), (ActionId.Aetheroplasm, 0.31f),
                         (ActionId.Tetrashatter, 0.80f), (ActionId.MegaHoly, 0.89f), (ActionId.IncineratingHeat, 0.80f), (ActionId.JStorm, 1.16f),
                         (ActionId.DivineJudgmentRaidwide, 0.18f),
                     })
            {
                var abilities = Rows(id);
                Assert.That(abilities, Is.Not.Empty, $"{id}");
                foreach (var ability in abilities)
                {
                    var at = Number(ability["t"]);
                    var impact = rows.FirstOrDefault(r => r["kind"] == "impact" && r["actor"] == ability["actor"] && r["id"] == ability["id"] && Number(r["t"]) >= at);
                    Assert.That(impact, Is.Not.Null, $"{id} at {at:F2}");
                    if (impact is not null) Assert.That(Number(impact["t"]) - at, Is.EqualTo(lag).Within(0.03f), $"{id} at {at:F2}");
                }
            }
        });
    }

    [Test]
    public void TheSteamChakramsPopInAsTheRealOnesDo()
    {
        var run = Run(PhysRangedDps, 136f);
        var chakrams = Rows("spawn", BNpcBaseId.SteamChakram);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(chakrams, Has.Count.EqualTo(2));
            foreach (var spawn in chakrams)
            {
                var popIn = Rows().First(r => r["kind"] == "ac" && r["actor"] == spawn["actor"] && r["id"] == "0x24");
                Assert.That(Number(popIn["t"]) - Number(spawn["t"]), Is.EqualTo(0.72f).Within(0.03f));
            }
        });
    }

    // A live orb's hitbox can grow while it homes in, as in game; it still makes it onto its player.
    [Test]
    public void OrbsReachTheirPlayersWhenTheirHitboxesGrowMidFlight()
    {
        var run = Run(MainTank, stopAt: 57f, probe: p =>
        {
            if (!p.Crossed(43f)) return;
            foreach (var orb in p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.Plasmasphere))
                ((FakeBattleCharas)Natives.BattleCharas).Find(orb.EntityId)!.HitboxRadius += 1.7f;
        });
        Assert.That(run.Deaths.Where(d => d.ActionId == ActionId.Burst), Is.Empty, run.ToString);
        Assert.That(run.Passed, run.ToString);
    }

    private static IEnumerable<TestCaseData> Practices()
        => Enum.GetValues<P3Practice>().Where(p => p != P3Practice.WholePhase).Select(p => new TestCaseData(p).SetArgDisplayNames(p.ToString()));

    private static ScenarioRun Practise(P3Practice practice, int seed, float? stopAt = null, Action<ScenarioProbe>? probe = null)
        => ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = stopAt,
            Overrides = o => ((TeaP3AlexanderPrimeStateOverrides)o).Practice = practice,
            Probe = probe,
        });

    // SunnyDay runs only the whole phase.
    [TestCaseSource(nameof(Practices))]
    public void EachPracticeClearsWithTheStrat(P3Practice practice)
    {
        foreach (var seed in Enumerable.Range(1, TestContext.Parameters.Get("PracticeSeeds", 8)))
        {
            var run = Practise(practice, seed);
            Assert.That(run.Passed, run.ToString);
        }
    }

    // Inception alone ends with Alexander Prime back, before the second Chastening Heat.
    [Test]
    public void InceptionPracticeStopsBeforeWormhole()
    {
        var run = Practise(P3Practice.Inception, 3);
        Assert.That(run.Passed, run.ToString);
        Assert.That(run.Elapsed, Is.LessThan(96.67f), run.ToString);
    }

    // Wormhole picks up at the regroup for the second Chastening Heat: Alexander Prime up in the centre
    // at full HP, nothing of Inception left, and the robots back for Wormhole.
    [Test]
    public void WormholePracticeStartsUnderAlexanderPrime()
    {
        bool? targetable = null;
        var hp = 0f;
        var leftovers = -1;
        var robotsUp = false;
        var run = Practise(P3Practice.Wormhole, 3, stopAt: 128f, probe: p =>
        {
            var enemies = p.World.Children.OfType<SimEnemy>().Where(e => e.IsActive).ToList();
            if (p.Crossed(95f))
            {
                var prime = enemies.Single(e => e.BNpcBaseId == BNpcBaseId.AlexanderPrime);
                targetable = prime.Targetable;
                hp = prime.HpFraction;
                leftovers = enemies.Count(e => e.BNpcBaseId is BNpcBaseId.Plasmasphere or BNpcBaseId.JudgmentCrystal or BNpcBaseId.TrueHeart);
            }
            if (p.Crossed(127f))
                robotsUp = enemies.Any(e => e.BNpcBaseId == BNpcBaseId.BruteJustice) && enemies.Any(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(targetable, Is.True, "Alexander Prime targetable at 95s");
            Assert.That(hp, Is.EqualTo(1f), "Alexander Prime's HP");
            Assert.That(leftovers, Is.Zero, "Inception's orbs, crystals or heart");
            Assert.That(robotsUp, Is.True, "Brute Justice and Cruise Chaser for Wormhole");
        });
    }

    // Its player's view of each orb on any roll and either side: tethered to them from 36.45s to 42.89s,
    // then going off once, on them, the client last told of it by them as it does; down within 2s and
    // its zone under them within a second. None is left to burst.
    [Test]
    public void EveryOrbVisiblyGoesOffOnItsOwnPlayer()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            var orbs = new string[TeaP3AlexanderPrimeState.Orbs];
            var holders = new string[TeaP3AlexanderPrimeState.Orbs];
            var apart = new Dictionary<string, (float Told, Vector2 Player)>();
            var seen = 0;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 60f,
                Probe = p =>
                {
                    if (p.Crossed(36.3f))
                        for (var k = 0; k < orbs.Length; k++)
                        {
                            orbs[k] = TeaShown.Id(State(p).Objects.Plasmaspheres[k]);
                            holders[k] = TeaShown.Id(p.Member(State(p).OrbHolders[k]));
                        }
                    var rows = Rows();
                    for (; seen < rows.Count; seen++)
                    {
                        if (rows[seen]["kind"] != "ability" || rows[seen]["id"] != ActionId.Aetheroplasm.ToString()
                            || Array.IndexOf(orbs, rows[seen]["actor"]) is not (var k and >= 0)) continue;
                        var holder = Flat(p.Member(State(p).OrbHolders[k])!.Position);
                        var told = rows.Take(seen).Last(r => r["actor"] == orbs[k] && r["kind"] is "move" or "set_pos" or "spawn");
                        apart[orbs[k]] = (Vector2.Distance(new(ShownRun.Number(told, "x") - 100f, ShownRun.Number(told, "z") - 100f), holder), holder);
                    }
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var zones = shown.Of("eobj_spawn", EObjId.VoidzonePlasmasphere).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(shown.Of("cast", ActionId.Burst).Concat(shown.Of("ability", ActionId.Burst)), Is.Empty, $"seed {seed}: an orb burst");
                Assert.That(zones, Has.Count.EqualTo(TeaP3AlexanderPrimeState.Orbs), $"seed {seed}: the orbs' zones");
                for (var k = 0; k < orbs.Length; k++)
                {
                    var what = $"seed {seed}, orb {k} on {shown.SeatOf(holders[k])}";
                    var mine = shown.Rows.Where(r => r["actor"] == orbs[k]).ToList();
                    var pops = mine.Where(r => r["kind"] == "ability" && r["id"] == ActionId.Aetheroplasm.ToString()).ToList();
                    Assert.That(pops, Has.Count.EqualTo(1), what);
                    if (pops.Count != 1) continue;
                    var pop = pops[0];
                    var at = ShownRun.Time(pop);
                    Assert.That(ShownRun.Hits(pop), Does.Contain(holders[k]), what);
                    Assert.That(apart[orbs[k]].Told, Is.LessThan(OrbToldTouching), $"{what}: the client last told of its orb this far from its player");
                    var tether = mine.FirstOrDefault(r => r["kind"] == "ac" && r["id"] == "0x23" && r["p"].Split(',') is var p && p[1] == $"0x{TetherId.Plasmasphere:X}" && p[2] == $"0x{holders[k]}");
                    Assert.That(tether is null ? (float?)null : ShownRun.Time(tether), Is.EqualTo(36.45f).Within(0.05f), $"{what}: its tether to its player");
                    Assert.That(mine.Where(r => r["kind"] == "ac" && r["id"] == "0x2F").Select(ShownRun.Time).FirstOrDefault(), Is.EqualTo(42.89f).Within(0.05f), $"{what}: its tether down");
                    Assert.That(mine.Any(r => (r["kind"] == "despawn" || r["kind"] == "ac" && r["id"] == "0xE") && ShownRun.Time(r) > at && ShownRun.Time(r) <= at + 2f), Is.True,
                        $"{what}: down within 2s of going off");
                    var zone = zones.Where(z => ShownRun.Time(z) >= at && ShownRun.Time(z) <= at + 1f)
                        .MinBy(z => Vector2.Distance(new(ShownRun.Number(z, "x") - 100f, ShownRun.Number(z, "z") - 100f), apart[orbs[k]].Player));
                    Assert.That(zone, Is.Not.Null, $"{what}: its zone");
                    if (zone is not null)
                        Assert.That(Vector2.Distance(new(ShownRun.Number(zone, "x") - 100f, ShownRun.Number(zone, "z") - 100f), apart[orbs[k]].Player),
                            Is.LessThan(1.1f), $"{what}: its zone this far from its player");
                }
            });
        }
    }

    // The sim sets an orb off within a yalm of its player; the client heard where it was at most one
    // ActorMove, 0.2s at the orbs' 3.8y/s, earlier.
    private const float OrbToldTouching = 1.8f;

    // Orb 0 held off its player until the orbs burst: it goes off on its own at 55.1s, aimed at them,
    // over the whole party, and only they die of it.
    [Test]
    public void AnOrbNobodyReachesBurstsOnItsPlayer()
    {
        PartyRole? holder = null;
        string? orb = null;
        var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = PhysRangedDps,
            StopAt = 58f,
            WriteArtifactsOnFailure = false,
            Overrides = o => Pin((TeaP3AlexanderPrimeStateOverrides)o),
            Probe = p =>
            {
                if (p.Time < 42f || p.Time > 55.05f || State(p).Objects.Plasmaspheres[0] is not { IsActive: true } sphere) return;
                holder ??= State(p).OrbHolders[0];
                orb ??= TeaShown.Id(sphere);
                sphere.SetPosition(new Placement(new Vector3(0f, 0f, -19f), 0f));
            },
        });
        var bursts = shown.Of("ability", ActionId.Burst).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(bursts.Select(r => (r["actor"], r["target"])), Is.EqualTo(new[] { (orb, shown.IdOf(holder!.Value)) }));
            Assert.That(bursts.Select(ShownRun.Time), Is.All.EqualTo(55.1f).Within(0.05f));
            Assert.That(bursts.SelectMany(ShownRun.Hits), Is.EquivalentTo(shown.Seats.Keys));
            Assert.That(shown.Run.Deaths.Select(d => (d.Role, d.ActionId)), Is.EqualTo(new[] { (holder!.Value, (uint?)ActionId.Burst) }), shown.Run.ToString);
        });
    }

    // The client plays each slash along Cruise Chaser's facing: on any roll and either side, his model
    // faces along each of his three swords as it goes out, and the three cut three ways onto three players.
    [Test]
    public void CruiseChasersThreeSwordsCutThreeWays([Values] bool alexanderPrimeNorth)
    {
        foreach (var seed in TeaShown.Seeds())
        {
            string? cruiseChaser = null;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 89f,
                Overrides = o => ((TeaP3AlexanderPrimeStateOverrides)o).AlexanderPrimeNorth = alexanderPrimeNorth,
                Probe = p => cruiseChaser ??= State(p).Objects.CruiseChaser is { } chaser ? TeaShown.Id(chaser) : null,
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            TeaShown.SwordsCutThreeWays(shown.Rows, cruiseChaser!, ActionId.AlphaSwordP3, $"seed {seed}");
        }
    }


    // On any roll: the four crystal players marked at 43.96s, each crystal dropped where its player
    // stood at 49.01s, all four shattered by the heart, which turns gold and succeeds at 75.95s.
    [Test]
    public void EveryCrystalLandsUnderItsPlayerAndTheHeartShattersIt()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            var dropped = new Dictionary<string, Vector2>();
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 77f,
                Probe = p =>
                {
                    if (!p.Crossed(49.01f)) return;
                    foreach (var role in State(p).CrystalHolders) dropped[TeaShown.Id(p.Member(role))] = Flat(p.Member(role)!.Position);
                },
            });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var crystals = shown.Of("spawn", BNpcBaseId.JudgmentCrystal).ToList();
            var heart = shown.Of("spawn", BNpcBaseId.TrueHeart).Single()["actor"];
            Assert.Multiple(() =>
            {
                Assert.That(shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22" && r["p"].StartsWith($"0x{LockonId.JudgmentCrystal:X},"))
                    .Select(r => r["actor"]), Is.EquivalentTo(dropped.Keys), $"{what}: the crystal markers");
                Assert.That(shown.Of("ability", ActionId.JudgmentCrystalAoe).SelectMany(ShownRun.Hits).Distinct(), Is.SupersetOf(dropped.Keys), what);
                Assert.That(crystals, Has.Count.EqualTo(4), what);
                foreach (var crystal in crystals)
                {
                    var spot = new Vector2(ShownRun.Number(crystal, "x") - 100f, ShownRun.Number(crystal, "z") - 100f);
                    Assert.That(dropped.Values.Min(d => Vector2.Distance(d, spot)), Is.LessThan(0.05f), $"{what}: a crystal where nobody stood");
                    Assert.That(shown.Rows.Count(r => r["kind"] == "ability" && r["id"] == ActionId.Tetrashatter.ToString() && r["actor"] == crystal["actor"]),
                        Is.EqualTo(1), $"{what}: the crystal at {spot} shattered");
                }
                Assert.That(shown.Of("cast", ActionId.TetrashatterFail).Concat(shown.Of("ability", ActionId.TetrashatterFail)), Is.Empty, what);
                Assert.That(shown.Rows.Where(r => r["kind"] == "status_gain" && r["actor"] == heart && r["id"] == StatusId.EnigmaCodexHeart.ToString()), Is.Not.Empty, $"{what}: the gold heart");
                Assert.That(shown.Rows.Where(r => r["kind"] == "ability" && r["actor"] == heart && r["id"] == ActionId.TrueHeartSuccess.ToString()).Select(ShownRun.Time),
                    Is.EqualTo(new[] { 75.95f }).Within(0.35f), what);
            });
        }
    }

    // Each Inception debuff shows on the players the roll gave it, from 66.84s to 79.82s, with the
    // Restraining Order pair tethered for as long.
    [Test]
    public void InceptionDebuffsShowOnTheirRolledPlayers()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP3AlexanderPrimeState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 81f,
                Probe = p => state ??= State(p),
            });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            List<string> On(ushort status, string kind) => shown.Of(kind, status).Select(r => r["actor"]).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(On(StatusId.RestrainingOrder, "status_gain"), Is.EquivalentTo(new[] { shown.IdOf(state!.RestrainingOrderTank), shown.IdOf(state.RestrainingOrderDps) }), what);
                Assert.That(On(StatusId.SharedSentence, "status_gain"), Is.EquivalentTo(new[] { shown.IdOf(state!.SharedSentence) }), what);
                Assert.That(On(StatusId.AggravatedAssault, "status_gain"), Is.EquivalentTo(state!.AggravatedAssault.Select(shown.IdOf)), what);
                foreach (var status in new[] { StatusId.RestrainingOrder, StatusId.SharedSentence, StatusId.AggravatedAssault })
                {
                    Assert.That(shown.Of("status_gain", status).Select(ShownRun.Time), Is.All.EqualTo(66.84f).Within(0.05f), $"{what}: {status} on");
                    Assert.That(shown.Of("status_lose", status).Select(ShownRun.Time), Is.All.EqualTo(79.82f).Within(0.05f), $"{what}: {status} off");
                    Assert.That(On(status, "status_lose"), Is.EquivalentTo(On(status, "status_gain")), $"{what}: {status} off");
                }
                var tethers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x23" && r["p"].StartsWith($"0x0,0x{TetherId.RestrainingOrder:X},")).ToList();
                Assert.That(tethers.Select(r => (r["actor"], r["p"].Split(',')[2])), Is.EqualTo(new[] { (shown.IdOf(state!.RestrainingOrderDps), "0x" + shown.IdOf(state.RestrainingOrderTank)) }), what);
                Assert.That(tethers.Select(ShownRun.Time), Is.All.EqualTo(66.84f).Within(0.05f), what);
            });
        }
    }

    // On any roll: each number's marker on the player the roll gave it, and Cruise Chaser's eight hits
    // in number order, each on its number alone with his model facing along it.
    [Test]
    public void EveryNumberIsMarkedOnItsPlayerAndHitInTurn()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP3AlexanderPrimeState? state = null;
            string? cruiseChaser = null;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 157f,
                Probe = p =>
                {
                    state ??= State(p);
                    cruiseChaser ??= State(p).Objects.CruiseChaser is { } chaser ? TeaShown.Id(chaser) : null;
                },
            });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var numbers = state!.Numbers.Select(shown.IdOf).ToList();
            var markers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22").Select(r => r["p"].Split(','))
                .Where(p => LimitCutLockons.Contains(Convert.ToUInt32(p[0], 16))).ToList();
            var rows = shown.Rows.Where(r => r["actor"] == cruiseChaser).ToList();
            var hits = rows.Where(r => r["kind"] == "ability" && (r["id"] == ActionId.AlphaSword.ToString() || r["id"] == ActionId.SuperBlasstyChargeP3.ToString())).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(markers.Select(p => (Array.IndexOf(LimitCutLockons, Convert.ToUInt32(p[0], 16)), p[1][2..])),
                    Is.EquivalentTo(numbers.Select((id, k) => (k, id))), $"{what}: the number markers");
                Assert.That(hits.Select(h => ShownRun.Hits(h).Single()), Is.EqualTo(numbers), $"{what}: each hit on its number alone, in turn");
                TeaShown.FacesEachHitOnScreen(shown.Rows, cruiseChaser!, [ActionId.AlphaSword, ActionId.SuperBlasstyChargeP3], what);
            });
        }
    }

    // Alexander Prime and the robots never lose HP on the client: only the True Heart, the orbs and the
    // crystals do, in the whole phase.
    [Test]
    public void OnlyTheHeartOrbsAndCrystalsLoseHp()
    {
        var dropped = new HashSet<uint>();
        var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
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
        Assert.That(dropped, Is.SubsetOf(new[] { BNpcBaseId.TrueHeart, BNpcBaseId.Plasmasphere, BNpcBaseId.JudgmentCrystal }));
    }

    // On any roll: Alexander Prime's Sacrament goes off from the rolled spot 21.5y out, his model facing
    // the centre.
    [Test]
    public void AlexanderPrimeTakesTheRolledSacramentSpot()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP3AlexanderPrimeState? state = null;
            string? boss = null;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 87f,
                Probe = p =>
                {
                    state ??= State(p);
                    boss ??= State(p).Objects.AlexanderPrime is { } prime ? TeaShown.Id(prime) : null;
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var spot = Flat(Geometry.OnCompass(22.5f + 45f * state!.SacramentSpot, 21.5f));
            var sacrament = shown.Rows.Where(r => r["actor"] == boss && r["kind"] == "ability" && r["id"] == ActionId.SacramentInception.ToString() && ShownRun.Time(r) < 87f).ToList();
            Assert.That(sacrament, Has.Count.EqualTo(1), $"seed {seed}: his Sacrament");
            Assert.Multiple(() =>
            {
                Assert.That(Vector2.Distance(new(ShownRun.Number(sacrament[0], "x") - 100f, ShownRun.Number(sacrament[0], "z") - 100f), spot), Is.LessThan(0.05f),
                    $"seed {seed}: off spot {state.SacramentSpot}");
                Assert.That(TeaShown.Angle(TeaShown.FacingOnScreen(shown.Rows, boss!, sacrament[0]), MathF.Atan2(-spot.X, -spot.Y)), Is.LessThan(MathF.PI / 18f),
                    $"seed {seed}: facing away from the centre");
            });
        }
    }

    // On any roll: the Enumeration markers on their two rolled players at 160.51s.
    [Test]
    public void TheEnumerationsMarkTheirRolledPlayers()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaP3AlexanderPrimeState? state = null;
            var shown = ShownRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, seed, new ScenarioRunOptions { StopAt = 167f, Probe = p => state ??= State(p) });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var marked = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22" && r["p"].StartsWith($"0x{LockonId.Enumeration:X},")).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(marked.Select(r => r["actor"]), Is.EquivalentTo(state!.Enumerations.Select(shown.IdOf)), $"seed {seed}");
                Assert.That(marked.Select(ShownRun.Time), Is.All.EqualTo(160.51f).Within(0.05f), $"seed {seed}");
            });
        }
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        TeaP3AlexanderPrimeState? host = null;
        TeaP3AlexanderPrimeState? replayed = null;
        ScenarioRun.Execute(typeof(TeaP3AlexanderPrimeScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 1f,
            Probe = probe =>
            {
                if (!probe.Crossed(0.5f)) return;
                var scenario = probe.Game.Catalog.Scenarios.OfType<TeaP3AlexanderPrimeScenario>().Single();
                host = scenario.LastState;
                replayed = (TeaP3AlexanderPrimeState?)scenario.StartReplay(scenario.BuildReplayStateMessage()!, 0, MainTank, probe.World);
            },
        });

        Assert.That(host, Is.Not.Null);
        Assert.That(replayed, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replayed!.OrbHolders, Is.EqualTo(host!.OrbHolders));
            Assert.That(replayed.RestrainingOrderTank, Is.EqualTo(host.RestrainingOrderTank));
            Assert.That(replayed.RestrainingOrderDps, Is.EqualTo(host.RestrainingOrderDps));
            Assert.That(replayed.SharedSentence, Is.EqualTo(host.SharedSentence));
            Assert.That(replayed.AlexanderPrimeNorth, Is.EqualTo(host.AlexanderPrimeNorth));
            Assert.That(replayed.SacramentSpot, Is.EqualTo(host.SacramentSpot));
            Assert.That(replayed.BruteJusticeNorthWest, Is.EqualTo(host.BruteJusticeNorthWest));
            Assert.That(replayed.PuddlesMirrored, Is.EqualTo(host.PuddlesMirrored));
            Assert.That(replayed.Numbers, Is.EqualTo(host.Numbers));
            Assert.That(replayed.Enumerations, Is.EqualTo(host.Enumerations));
            Assert.That(replayed.Practice, Is.EqualTo(host.Practice));
        });
    }

    [Test]
    public void ReplayStateRejectsBadRolls()
    {
        PartyRole[] orbs = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
        PartyRole[] enumerations = [MeleeDpsB, PhysRangedDps];
        TeaP3AlexanderPrimeState? Roll(PartyRole[]? orbHolders = null, PartyRole tank = MainTank, PartyRole restrained = PhysRangedDps,
            PartyRole shared = MeleeDpsB, int spot = 1, PartyRole[]? numbers = null, PartyRole[]? marked = null,
            P3Practice practice = P3Practice.Wormhole)
            => TeaP3AlexanderPrimeState.FromNetworkReplay(practice, orbHolders ?? orbs, tank, restrained, shared, false, spot, true, false,
                numbers ?? PinnedNumbers, marked ?? enumerations);

        Assert.Multiple(() =>
        {
            Assert.That(Roll(), Is.Not.Null);
            Assert.That(Roll(practice: (P3Practice)7), Is.Null);
            Assert.That(Roll(orbHolders: [MeleeDpsA, MeleeDpsA, PhysRangedDps, CasterDps]), Is.Null);
            Assert.That(Roll(orbHolders: orbs[..3]), Is.Null);
            Assert.That(Roll(tank: RegenHealer), Is.Null);
            Assert.That(Roll(restrained: OffTank), Is.Null);
            Assert.That(Roll(shared: PhysRangedDps), Is.Null);
            Assert.That(Roll(spot: 8), Is.Null);
            Assert.That(Roll(numbers: PinnedNumbers[..7]), Is.Null);
            Assert.That(Roll(numbers: [.. PinnedNumbers[..7], CasterDps]), Is.Null);
            Assert.That(Roll(marked: [MeleeDpsB, MainTank]), Is.Null);
        });
    }
}
