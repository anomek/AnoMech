using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Ucob.P4Adds;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Ucob.UcobConstants;

namespace AnoMech.Tests;

// The adds engage at 13.1 with Twintania on the main tank and Nael on the off tank. A human tank
// swaps them with Provoke; nothing else moves them before the first buster pair.
public class UcobP4AddsScenarioTests
{
    private const float PressAt = 15f;

    private static List<string> AnnouncementsAfterPress(PartyRole player, uint actionId, uint addBaseId)
    {
        var announced = new List<string>();
        var run = ScenarioRun.Execute(typeof(UcobP4AddsScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            Takeovers = [new Takeover(14f, null)],
            StopAt = PressAt + 1f,
            Probe = p =>
            {
                if (p.Crossed(1f)) p.World.Announced += (text, speaker) => { if (speaker is null) announced.Add(text); };
                if (!p.Crossed(PressAt)) return;
                var add = p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == addBaseId);
                ((FakeUserActions)Natives.UserActions).Act(
                    new PlayerAction(PlayerActionStep.Resolved, ActionType.Action, actionId, add.GameObjectId));
            },
        });
        Assert.That(run.Failure, Is.Null, run.ToString);
        return announced;
    }

    [Test]
    public void ProvokingNaelTakesHerAndHandsTwintaniaToTheOtherTank()
        => Assert.That(AnnouncementsAfterPress(PartyRole.MainTank, ActionId.Provoke, BNpcBaseId.NaelDeusDarnus), Is.EquivalentTo(new[]
        {
            "Tank swap: Twintania is now on the off tank.",
            "Tank swap: Nael deus Darnus is now on the main tank.",
        }));

    [Test]
    public void ProvokingTheAddAlreadyHeldSwapsNothing()
        => Assert.That(AnnouncementsAfterPress(PartyRole.MainTank, ActionId.Provoke, BNpcBaseId.Twintania), Is.Empty);

    [Test]
    public void AnotherActionOnNaelSwapsNothing()
        => Assert.That(AnnouncementsAfterPress(PartyRole.MainTank, ActionId.IronChariot, BNpcBaseId.NaelDeusDarnus), Is.Empty);
}
