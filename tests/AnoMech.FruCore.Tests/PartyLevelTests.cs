using AnoMech.Core.Game.Party;

[TestFixture]
public sealed class PartyLevelTests
{
    [Test]
    public void DutyLevelPreservesPlayerSlotAndPresetDefaults()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var party = PartyPresets.ForRole(role, levelOverride: 100);
            Assert.That(party[(int)role], Is.Null);
            Assert.That(party.Count(p => p?.Level == 100), Is.EqualTo(7));
            Assert.That(PartyPresets.ForRole(role).Where(p => p != null).All(p => p!.Level == 90), Is.True);
            Assert.That(PartyPresets.ForRole(role, levelOverride: 70).Count(p => p?.Level == 70), Is.EqualTo(7));
        }
    }
}
