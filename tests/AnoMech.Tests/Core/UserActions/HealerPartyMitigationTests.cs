using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class HealerPartyMitigationTests
{
    private const uint Kerachole = 24298, Holos = 24310, PlenaryIndulgence = 7433, DivineCaress = 37011, Temperance = 16536, Aquaveil = 25861;

    [TestCase(Kerachole, (ushort)2618)]
    [TestCase(Kerachole, (ushort)2938)]
    [TestCase(Holos, (ushort)3003)]
    [TestCase(Holos, (ushort)3365)]
    [TestCase(PlenaryIndulgence, (ushort)1219)]
    [TestCase(DivineCaress, (ushort)3903)]
    public void APartyActionReachesEveryoneNearby(uint actionId, ushort statusId)
    {
        var (near, holders) = Press(actionId, statusId, ShieldHealer, target: null);
        Assert.That(near, Has.Count.GreaterThan(1));
        Assert.That(holders, Is.SupersetOf(near));
    }

    [Test]
    public void TemperanceCoversThePartyAndGivesTheWhiteMageItsOwn()
    {
        var (near, holders) = Press(Temperance, 1873, RegenHealer, target: null);
        Assert.That(holders, Is.SupersetOf(near.Where(r => r != RegenHealer)));
        Assert.That(holders, Does.Not.Contain(RegenHealer));
        Assert.That(Press(Temperance, 1872, RegenHealer, target: null).Holders, Is.EqualTo(new[] { RegenHealer }));
    }

    [Test]
    public void AquaveilLandsOnItsTargetOnly()
    {
        var (_, holders) = Press(Aquaveil, 2708, RegenHealer, target: MainTank);
        Assert.That(holders, Is.EqualTo(new[] { MainTank }));
    }

    [TestCase((ushort)2618)]
    [TestCase((ushort)3003)]
    [TestCase((ushort)1219)]
    [TestCase((ushort)1873)]
    public void TheMitigationCountsTenPercent(ushort statusId)
        => Assert.That(Mitigation.Effective([statusId], DamageKind.Magic), Is.EqualTo(0.10f).Within(1e-5f));

    // Pressed by a bot partway into a run; who holds the status right after, and who stood within
    // 25y of the caster (well inside every one of these actions' reach).
    private static (List<PartyRole> Near, List<PartyRole> Holders) Press(uint actionId, ushort statusId, PartyRole caster, PartyRole? target)
    {
        List<PartyRole>? near = null, holders = null;
        var landedAtOnce = 0;
        ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 0, 1, new ScenarioRunOptions
        {
            StopAt = 4.5f,
            UserActionsEnabled = true,
            Probe = p =>
            {
                var source = p.Member(caster)!;
                var members = Enum.GetValues<PartyRole>().Select(r => (Role: r, Member: p.Member(r))).Where(m => m.Member != null).ToList();
                if (p.Crossed(3f))
                {
                    var aim = target is { } role ? (ulong)p.Member(role)!.GameObjectId : 0UL;
                    JobActions.ApplyEffects(source, actionId, aim, new Random(1));
                    near = members.Where(m => Distance(source, m.Member!) < 25f).Select(m => m.Role).ToList();
                    landedAtOnce = members.Count(m => m.Member!.HasStatus(statusId));
                }
                if (p.Crossed(4f))
                    holders = members.Where(m => m.Member!.HasStatus(statusId)).Select(m => m.Role).ToList();
            },
        });
        Assert.That(holders, Is.Not.Null, "the probe never ran");
        Assert.That(landedAtOnce, Is.LessThanOrEqualTo(1), "the status rippled out instead of landing on everyone at once");
        return (near!, holders!);
    }

    private static float Distance(SimCharacter a, SimCharacter b)
    {
        var dx = a.Position.X - b.Position.X;
        var dz = a.Position.Z - b.Position.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
