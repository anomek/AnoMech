using AnoMech.Multiplayer;
using AnoMech.Network;

namespace AnoMech.Tests;

public class ConfigurationTests
{
    [Test]
    public void PasswordSavedBeforeOriginsBindsToTheSavedRelay()
    {
        var config = new Configuration { RelayServerUrl = "relay.example.com", RelayAccessToken = "secret" };
        Assert.That(config.TokenForRelay("relay.example.com"), Is.EqualTo("secret"));
        Assert.That(config.RelayTokenOrigin, Is.EqualTo("wss://relay.example.com:443"));
    }

    [TestCase("wss://relay.example.com/path", "secret")]
    [TestCase("other.example.com", "")]
    [TestCase("relay.example.com:8443", "")]
    [TestCase("ws://relay.example.com:443", "")]
    [TestCase("not a url ::", "")]
    public void SavedPasswordOnlyReturnedForTheRelayItWasEnteredFor(string url, string expected)
    {
        var config = new Configuration { RelayServerUrl = "relay.example.com", RelayAccessToken = "secret" };
        Assert.That(config.TokenForRelay(url), Is.EqualTo(expected));
    }

    [Test]
    public void ActiveServerFollowsTheChoice()
    {
        var config = new Configuration { RelayServerUrl = " relay.example.com " };
        Assert.That(config.ActiveRelayUrl(), Is.EqualTo(DefaultRelay.Url));
        config.UseCustomRelay = true;
        Assert.That(config.ActiveRelayUrl(), Is.EqualTo("relay.example.com"));
    }

    [Test]
    public void PerInstallCredentialIsStable()
    {
        var config = new Configuration();
        var first = config.EnsurePeerSecret();
        Assert.That(RelayWire.IsValidSecret(first));
        Assert.That(config.EnsurePeerSecret(), Is.EqualTo(first));
    }

    [Test]
    public void InvalidSavedCredentialIsReplaced()
    {
        var config = new Configuration { PeerSecret = "tampered" };
        Assert.That(RelayWire.IsValidSecret(config.EnsurePeerSecret()));
        Assert.That(config.PeerSecret, Is.Not.EqualTo("tampered"));
    }
}
