using System.Text.Json;
using AnoMech.Network;

namespace AnoMech.Relay.Tests;

public class RelayGroupTests
{
    private static RelayGroupMember Member(int number, string? url = null, string location = "Somewhere")
        => new(number, url ?? $"wss://relay{number}.example.com", location);

    [Test]
    public void GroupFileIsAListOfRelays()
    {
        var group = RelayGroup.Parse("""
            [
              { "number": 0, "url": "wss://sim.example.com", "location": "US East (Virginia)" },
              { "number": 1, "url": "wss://de.sim.example.com", "location": "EU Central (Germany)" }
            ]
            """);
        Assert.That(group, Is.EqualTo(new[]
        {
            new RelayGroupMember(0, "wss://sim.example.com", "US East (Virginia)"),
            new RelayGroupMember(1, "wss://de.sim.example.com", "EU Central (Germany)"),
        }));
        Assert.That(RelayGroup.Validate(group), Is.Null);
        Assert.Throws<JsonException>(() => RelayGroup.Parse("""{ "number": 0 }"""));
    }

    [TestCase("empty")]
    [TestCase("too many")]
    [TestCase("number too high")]
    [TestCase("negative number")]
    [TestCase("duplicate number")]
    [TestCase("not a relay address")]
    [TestCase("address without a scheme")]
    [TestCase("address with a query")]
    [TestCase("address too long")]
    [TestCase("same relay twice")]
    [TestCase("no location")]
    [TestCase("location with a line break")]
    [TestCase("empty entry")]
    public void UnusableGroupsAreRefused(string problem)
    {
        List<RelayGroupMember> group = problem switch
        {
            "empty" => [],
            "too many" => Enumerable.Range(0, RelayWire.CodeServers + 1).Select(n => Member(n % RelayWire.CodeServers, $"wss://r{n}.example.com")).ToList(),
            "number too high" => [Member(0), Member(RelayWire.CodeServers)],
            "negative number" => [Member(-1)],
            "duplicate number" => [Member(0, "wss://a.example.com"), Member(0, "wss://b.example.com")],
            "not a relay address" => [Member(0, "https://a.example.com")],
            "address without a scheme" => [Member(0, "a.example.com")],
            "address with a query" => [Member(0, "wss://a.example.com/?x=1")],
            "address too long" => [Member(0, "wss://" + new string('a', RelayGroup.MaxUrlBytes) + ".com")],
            "same relay twice" => [Member(0, "wss://a.example.com"), Member(1, "wss://A.example.com:443")],
            "no location" => [Member(0, location: " ")],
            "location with a line break" => [Member(0, location: "East\nWest")],
            _ => [Member(0), null!],
        };
        Assert.That(RelayGroup.Validate(group), Is.Not.Null);
    }
}
