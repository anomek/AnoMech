using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AnoMech.Relay.Tests;

// Self-signed certificates written as PEM files, named as certbot names them.
internal static class TestCertificates
{
    public static string NewDirectory() => Directory.CreateTempSubdirectory("anomech-relay-tls-").FullName;

    // fullchain.pem and privkey.pem for `name` in `directory`.
    public static (string Certificate, string Key) Write(string directory, string name, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        Directory.CreateDirectory(directory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(name);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        var certificateFile = Path.Combine(directory, "fullchain.pem");
        var keyFile = Path.Combine(directory, "privkey.pem");
        File.WriteAllText(certificateFile, certificate.ExportCertificatePem());
        File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
        return (certificateFile, keyFile);
    }

    public static (string Certificate, string Key) WriteValid(string directory, string name = "relay.example.test")
        => Write(directory, name, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(89));
}
