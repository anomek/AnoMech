using AnoMech.Multiplayer;
using AnoMech.Network;

namespace AnoMech.Tests;

public class RelayGroupsTests
{
    private const string Home = "wss://us.example.com";

    private static readonly RelayGroupInfo Group = new(0,
    [
        new(0, "wss://us.example.com", "US East"),
        new(1, "wss://de.example.com", "EU Central"),
        new(2, "wss://jp.example.com", "Asia East"),
    ]);

    private static string[] HostOrder(double? us, double? de, double? jp, string chosen = "")
        => RelayGroups.HostOrder(Group, chosen, url => url.Contains("//us.") ? us : url.Contains("//de.") ? de : jp)
            .Select(m => m.Location).ToArray();

    [Test]
    public void HostPicksTheClosestServer()
        => Assert.That(HostOrder(95, 18, 240), Is.EqualTo(new[] { "EU Central", "US East", "Asia East" }));

    [Test]
    public void ActiveServerKeepsANearTie()
        => Assert.That(HostOrder(25, 18, 240), Is.EqualTo(new[] { "US East", "EU Central", "Asia East" }));

    [Test]
    public void ServersThatDidNotAnswerAreLeftOut()
        => Assert.That(HostOrder(null, 18, null), Is.EqualTo(new[] { "EU Central" }));

    [Test]
    public void ActiveServerUntilAPingIsIn()
        => Assert.That(HostOrder(null, null, null), Is.EqualTo(new[] { "US East" }));

    [Test]
    public void ServerPickedInSettingsWins()
    {
        Assert.That(HostOrder(95, 18, 240, "wss://jp.example.com:443"), Is.EqualTo(new[] { "Asia East" }));
        Assert.That(HostOrder(95, 18, 240, "wss://gone.example.com"), Is.EqualTo(new[] { "EU Central", "US East", "Asia East" }),
            "a pick no longer in the group is ignored");
    }

    [Test]
    public void JoinGoesStraightToTheServerHoldingTheCode()
    {
        Assert.That(RelayGroups.JoinUrl(Group, RelayWire.NewCode(1), Home), Is.EqualTo("wss://de.example.com"));
        Assert.That(RelayGroups.JoinUrl(Group, RelayWire.NewCode(0), "us.example.com"), Is.EqualTo("us.example.com"),
            "the active server is dialed as the player set it up");
        Assert.That(RelayGroups.JoinUrl(Group, RelayWire.NewCode(7), Home), Is.EqualTo(Home),
            "a server missing from the list is reached through the active one");
        Assert.That(RelayGroups.JoinUrl(null, RelayWire.NewCode(7), Home), Is.EqualTo(Home));
    }

    [Test]
    public void GroupIsTakenOnlyFromAUsableList()
    {
        var members = Group.Members;
        Assert.That(RelayGroups.FromInfo(new RelayInfo(RelayWire.Version, false, 1, members))?.Home, Is.EqualTo(1));
        Assert.That(RelayGroups.FromInfo(new RelayInfo(RelayWire.Version, false, null, null)), Is.Null, "not in a group");
        Assert.That(RelayGroups.FromInfo(new RelayInfo(RelayWire.Version, false, 5, members)), Is.Null, "a list without the server itself");
        Assert.That(RelayGroups.FromInfo(new RelayInfo(RelayWire.Version, false, 0, [members[0], members[0] with { Url = "wss://x.example.com" }])), Is.Null,
            "a number listed twice");
    }

    [Test]
    public void ListIsKeptForItsServerAndTrustedOnlyOverEncryption()
    {
        var config = new Configuration();
        DalamudServices.Install(nameof(Plugin.Config), config);
        var info = new RelayInfo(RelayWire.Version, false, 0, [new(0, DefaultRelay.Url, DefaultRelay.Location), new(1, "wss://de.example.com", "EU Central (Germany)")]);

        Assert.That(RelayGroups.Known, Is.False, "nothing fetched yet");
        RelayGroups.Learn(DefaultRelay.Url, info);
        Assert.That(RelayGroups.Current, Is.Null, "a list over plain http could have been swapped on the way");
        Assert.That(RelayGroups.Known, Is.False);
        RelayGroups.Learn(DefaultRelay.Url, info with { Encrypted = true });
        Assert.That(RelayGroups.Current?.Members, Has.Length.EqualTo(2));
        Assert.That(RelayGroups.Confirmed);

        config.UseCustomRelay = true;
        config.RelayServerUrl = "ws://192.0.2.7:7890";
        Assert.That(RelayGroups.Current, Is.Null, "another server's list doesn't apply");
        RelayGroups.Learn(config.RelayServerUrl, info with { Group = [new(0, "ws://192.0.2.7:7890", "LAN")] });
        Assert.That(RelayGroups.Current?.Members.Single().Location, Is.EqualTo("LAN"), "a server set up unencrypted is taken at its word");

        config.UseCustomRelay = false;
        Assert.That(RelayGroups.Current, Is.Null, "one list is kept, for the server last asked");
        RelayGroups.Learn(DefaultRelay.Url, info with { Encrypted = true });
        RelayGroups.Learn(DefaultRelay.Url, new RelayInfo(RelayWire.Version, false, null, null) { Encrypted = true });
        Assert.That(RelayGroups.Current, Is.Null, "a server that left its group");
        Assert.That(RelayGroups.Known, "but its answer is known: a single location");
    }

    [TestCase("203.0.113.9", true)]
    [TestCase("2001:db8::1", true)]
    [TestCase("127.0.0.1", false)]
    [TestCase("10.1.2.3", false)]
    [TestCase("172.20.0.1", false)]
    [TestCase("192.168.1.1", false)]
    [TestCase("169.254.1.1", false)]
    [TestCase("100.64.0.1", false)]
    [TestCase("::1", false)]
    [TestCase("fe80::1", false)]
    [TestCase("fd00::1", false)]
    [TestCase("::ffff:192.168.1.1", false)]
    public void PingsOnlyPublicAddresses(string address, bool isPublic)
        => Assert.That(RelayPings.IsPublic(System.Net.IPAddress.Parse(address)), Is.EqualTo(isPublic));
}
