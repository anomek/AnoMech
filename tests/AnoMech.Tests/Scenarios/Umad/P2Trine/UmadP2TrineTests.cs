using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P2Trine;
using static AnoMech.Tests.NegativeRun;
using AnoMech.Scenarios;
namespace AnoMech.Tests;

public class UmadP2TrineTests
{
    [Test]
    public void EveryTriangleIsShownBeforeItsCountdownStarts()
    {
        var path = Path.GetTempFileName();
        try
        {
            ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions { StopAt = 10f, TimelinePath = path });
            var rows = File.ReadAllLines(path);
            var shows = rows.Where(r => r.Contains(",ac,") && r.Contains(",0x19D,") && r.Contains("\"0x1,0x2\"")).ToArray();
            var countdowns = rows.Where(r => r.Contains(",ac,") && r.Contains(",0x19D,") && r.Contains("\"0x10,0x20\"")).ToArray();
            Assert.That(shows, Has.Length.EqualTo(7), "Hidden Trine VFX must be enabled before arming the countdown");
            Assert.That(countdowns, Has.Length.EqualTo(7));
            foreach (var countdown in countdowns)
            {
                var actor = countdown.Split(',')[2];
                Assert.That(rows.Take(Array.IndexOf(rows, countdown)).Any(r => shows.Contains(r) && r.Split(',')[2] == actor), Is.True);
            }
        }
        finally { File.Delete(path); }
    }
    internal static Type ScenarioType()
    {
        var scenario = ScenarioCatalog.Create().Scenarios.FirstOrDefault(s => s.Name == "Trine");
        Assert.That(scenario, Is.Not.Null, "P2 Trine is not registered");
        return scenario!.GetType();
    }

    [Test]
    public void YoruhaTrineIsRegistered()
    {
        var scenario = (IScenario)Activator.CreateInstance(ScenarioType())!;
        Assert.That(scenario.Phase.Name, Is.EqualTo("P2"));
        Assert.That(scenario.AiStrats.Single().Name, Is.EqualTo("Yoruha"));
        Assert.That(scenario.AiStrats.Single().Group, Is.EqualTo("JP"));
    }

    [Test]
    public void AllGuidePatternsAndWingsClearInsideArenaWithCorrectTankBaits()
    {
        for (var pattern = 0; pattern < 3; pattern++)
        foreach (var left in new[] { false, true })
        {
            var escaped = false;
            PartyRole? closest = null, furthest = null;
            var separated = false;
            var run = ScenarioRun.Execute(ScenarioType(), 0, 42, new ScenarioRunOptions
            {
                Overrides = o =>
                {
                    var s = (UmadP2TrineStateOverrides)o;
                    s.Pattern = pattern;
                    s.LeftWing = left;
                },
                Probe = p =>
                {
                    var members = Enum.GetValues<PartyRole>().Select(r => (Role: r, Member: p.Member(r)!)).ToArray();
                    escaped |= members.Any(m => new Vector2(m.Member.Position.X, m.Member.Position.Z).Length() > 20.01f);
                    if (!p.Crossed(20.25f)) return;
                    var sorted = members.OrderBy(m => m.Member.Position.LengthSquared()).ToArray();
                    closest = sorted[0].Role;
                    furthest = sorted[^1].Role;
                    var mt = p.Member(PartyRole.MainTank)!.Position;
                    var st = p.Member(PartyRole.OffTank)!.Position;
                    separated = Vector3.Distance(mt, st) > 7f &&
                        members.Where(m => (int)m.Role >= 2).All(m =>
                            Vector3.Distance(m.Member.Position, mt) > 7f && Vector3.Distance(m.Member.Position, st) > 7f);
                },
            });
            Assert.Multiple(() =>
            {
                Assert.That(run.Passed, $"pattern={pattern} left={left}: {run}");
                Assert.That(escaped, Is.False);
                Assert.That(closest, Is.EqualTo(PartyRole.MainTank));
                Assert.That(furthest, Is.EqualTo(PartyRole.OffTank));
                Assert.That(separated, Is.True);
            });
        }
    }

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
    public void CenterTriangleAlwaysThirdAndBlastCountsMatchObservedWaves()
    {
        for (var pattern = 0; pattern < 3; pattern++)
        {
            var state = new UmadP2TrineState(new Rng(0), new() { Pattern = pattern });
            Assert.Multiple(() =>
            {
                Assert.That(state.WaveOf(6), Is.EqualTo(2));
                Assert.That(new[] { state.WaveVertices(0).Count, state.WaveVertices(1).Count, state.WaveVertices(2).Count }, Is.EqualTo(new[] { 9, 3, 9 }));
            });
        }
    }

    private static NegativeRun<UmadP2TrineScenario> Trine(PartyRole role)
        => Negative<UmadP2TrineScenario>(role).Overrides<UmadP2TrineStateOverrides>(o =>
        {
            o.Pattern = 0;
            o.LeftWing = true;
        });

    [Test]
    public void FirstTriangleVertexKills()
        => Trine(PartyRole.CasterDps).TeleportAt(15.5f, new(-17.3205f, -10f)).ShouldKill(47840, PartyRole.CasterDps);

    [Test]
    public void SecondWaveStillExplodesAfterFirstClears()
        => Trine(PartyRole.CasterDps).TeleportAt(17.5f, new(8.660254f, -5f)).ShouldKill(47840, PartyRole.CasterDps);

    [Test]
    public void CenterIsNotSafeBeforeThirdWave()
        => Trine(PartyRole.CasterDps).TeleportAt(19.7f, Vector2.Zero).ShouldKill(47840, PartyRole.CasterDps);

    [TestCase(true, 8f, 47821u)]
    [TestCase(false, -8f, 47822u)]
    public void WrongWingHalfKills(bool left, float x, uint action)
        => Negative<UmadP2TrineScenario>(PartyRole.CasterDps)
            .Overrides<UmadP2TrineStateOverrides>(o => { o.Pattern = 0; o.LeftWing = left; })
            .TeleportAt(8.5f, new(x, 0)).ShouldKill(action, PartyRole.CasterDps);

    [Test]
    public void NonTankTakingNearestBusterDies()
        => Trine(PartyRole.CasterDps).TeleportAt(20.1f, Vector2.Zero).ShouldKill(47823, PartyRole.CasterDps);

    [Test]
    public void NonTankTakingFurthestBusterDies()
        => Trine(PartyRole.CasterDps).TeleportAt(20.1f, new(19.8f, 0f)).ShouldKill(47823, PartyRole.CasterDps);

    [Test]
    public void PartyMemberStandingOnMainTankDiesToSplash()
        => Trine(PartyRole.CasterDps).FreezeAt(20.1f)
            .At(20.1f, p => p.Member(PartyRole.CasterDps)!.SetPosition(p.Member(PartyRole.MainTank)!.Position))
            .ShouldKill(47823, PartyRole.CasterDps);
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(42)]
    public void SeededPracticeClears(int seed)
    {
        var run = ScenarioRun.Execute(ScenarioType(), 0, seed);
        Assert.That(run.Passed, run.ToString);
    }
}



