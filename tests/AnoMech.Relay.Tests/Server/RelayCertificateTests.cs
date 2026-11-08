namespace AnoMech.Relay.Tests;

public class RelayCertificateTests
{
    private string directory = null!;

    [SetUp]
    public void CreateDirectory() => directory = TestCertificates.NewDirectory();

    [TearDown]
    public void RemoveDirectory() => Directory.Delete(directory, recursive: true);

    [Test]
    public void LoadsACertificateWithItsKey()
    {
        var (certificate, key) = TestCertificates.WriteValid(directory);
        var tls = RelayCertificate.Load(certificate, key);
        Assert.Multiple(() =>
        {
            Assert.That(tls.Names, Is.EqualTo(new[] { "relay.example.test" }));
            Assert.That(tls.Certificate.HasPrivateKey);
            Assert.That(tls.IsSelfSigned);
            Assert.That(tls.ChainLength, Is.EqualTo(1));
            Assert.That(tls.ExpiryWarning(DateTime.UtcNow), Is.Null);
        });
    }

    [Test]
    public void SaysWhichFileIsMissing()
    {
        var error = Assert.Throws<RelayCertificateException>(() =>
            RelayCertificate.Load(Path.Combine(directory, "fullchain.pem"), Path.Combine(directory, "privkey.pem")))!;
        Assert.That(error.Message, Does.Contain("fullchain.pem").And.Contain("doesn't exist"));
    }

    [Test]
    public void SaysWhenTheKeyIsAnotherCertificates()
    {
        var (certificate, _) = TestCertificates.WriteValid(Path.Combine(directory, "first"));
        var (_, otherKey) = TestCertificates.WriteValid(Path.Combine(directory, "second"));
        var error = Assert.Throws<RelayCertificateException>(() => RelayCertificate.Load(certificate, otherKey))!;
        Assert.That(error.Message, Does.Contain("doesn't go with the certificate"));
    }

    [Test]
    public void SaysWhenTheCertificateFileIsntOne()
    {
        var (certificate, key) = TestCertificates.WriteValid(directory);
        File.WriteAllText(certificate, "not a certificate");
        var error = Assert.Throws<RelayCertificateException>(() => RelayCertificate.Load(certificate, key))!;
        Assert.That(error.Message, Does.Contain(certificate).And.Contain("fullchain.pem"));
    }

    [Test]
    public void ServesARenewalOnceItsFilesChange()
    {
        var (certificate, key) = TestCertificates.WriteValid(directory, "first.example.test");
        var tls = RelayCertificate.Load(certificate, key);
        Assert.That(tls.Refresh(), Is.EqualTo(CertificateRefresh.Unchanged));
        TestCertificates.WriteValid(directory, "second.example.test");
        Assert.That(tls.Refresh(), Is.EqualTo(CertificateRefresh.Renewed));
        Assert.That(tls.Names, Is.EqualTo(new[] { "second.example.test" }));
    }

    // One look at broken files says nothing yet: certbot's update can be caught halfway.
    [Test]
    public void KeepsServingWhatItHadWhileChangedFilesDontLoad()
    {
        var (certificate, key) = TestCertificates.WriteValid(directory, "first.example.test");
        var tls = RelayCertificate.Load(certificate, key);
        File.WriteAllText(certificate, "-----BEGIN CERTIFICATE-----\nhalf\n");
        Assert.That(tls.Refresh(), Is.EqualTo(CertificateRefresh.Unchanged));
        Assert.That(tls.Refresh(), Is.EqualTo(CertificateRefresh.Failing));
        Assert.That(tls.Problem, Does.Contain(certificate));
        Assert.That(tls.Names, Is.EqualTo(new[] { "first.example.test" }));

        TestCertificates.WriteValid(directory, "second.example.test");
        Assert.That(tls.Refresh(), Is.EqualTo(CertificateRefresh.Renewed));
        Assert.That(tls.Problem, Is.Null);
    }

    // A renewal is due with a third of the lifetime left, late with a quarter, urgent with a tenth.
    [Test]
    public void WarnsOnceARenewalIsOverdue()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (certificate, key) = TestCertificates.Write(directory, "relay.example.test", start, start.AddDays(90));
        var tls = RelayCertificate.Load(certificate, key);
        DateTime Day(double day) => start.AddDays(day).UtcDateTime;
        Assert.Multiple(() =>
        {
            Assert.That(tls.ExpiryWarning(Day(60)), Is.Null);
            Assert.That(tls.ExpiryWarning(Day(70)), Does.Contain("expires in 20 days").And.Contain("hasn't been renewed"));
            Assert.That(tls.IsUrgent(Day(70)), Is.False);
            Assert.That(tls.IsUrgent(Day(85)), Is.True);
            Assert.That(tls.ExpiryWarning(Day(91)), Does.Contain("EXPIRED"));
            Assert.That(tls.ExpiryWarning(Day(-1)), Does.Contain("isn't valid until"));
        });
    }

    [Test]
    public void KnowsCertbotRenewsTheCertificatesItKeeps()
    {
        var config = Path.Combine(directory, "letsencrypt");
        var (certificate, key) = TestCertificates.WriteValid(Path.Combine(config, "live", "relay.example.test"));
        var tls = RelayCertificate.Load(certificate, key);
        Assert.That(tls.CertbotRenewalFile, Is.Null, "without a renewal configuration");

        Directory.CreateDirectory(Path.Combine(config, "renewal"));
        File.WriteAllText(Path.Combine(config, "renewal", "relay.example.test.conf"), "");
        Assert.Multiple(() =>
        {
            Assert.That(tls.CertbotRenewalFile, Does.EndWith("relay.example.test.conf"));
            Assert.That(tls.ExpiryWarning(tls.NotAfterUtc.AddDays(-5)), Does.Contain("certbot renew --dry-run"));
            Assert.That(RelayCertificate.LoadCertbot("relay.example.test", config).Names, Is.EqualTo(new[] { "relay.example.test" }));
        });
    }

    [Test]
    public async Task AHandshakeWithItWorks()
    {
        var (certificate, key) = TestCertificates.WriteValid(directory);
        var tls = RelayCertificate.Load(certificate, key);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (protocol, cipher, _) = await tls.HandshakeAsync(timeout.Token);
        Assert.That(protocol, Is.Not.EqualTo(System.Security.Authentication.SslProtocols.None));
        Assert.That(cipher, Is.Not.Empty);
    }
}
