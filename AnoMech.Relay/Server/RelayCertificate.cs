using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Relay;

// A problem with the certificate, worded for the hoster who has to fix it.
public sealed class RelayCertificateException(string message, Exception? inner = null) : Exception(message, inner);

public enum CertificateRefresh
{
    // The files are as they were.
    Unchanged,
    // New files loaded: new connections get them.
    Renewed,
    // The files changed but don't load; the previous certificate is still served.
    Failing,
}

// The certificate the relay serves wss:// with, read from PEM files as certbot writes them
// (fullchain.pem and privkey.pem) and read again when they change, so a renewal is served
// without a restart.
public sealed class RelayCertificate
{
    private sealed record Loaded(X509Certificate2 Certificate, SslStreamCertificateContext Context, int ChainLength, byte[] Fingerprint);

    private const string SubjectAlternativeName = "2.5.29.17";
    private const string Organization = "2.5.4.10";

    private readonly object gate = new();
    private volatile Loaded current;

    private RelayCertificate(string certificateFile, string keyFile, Loaded loaded)
    {
        CertificateFile = certificateFile;
        KeyFile = keyFile;
        current = loaded;
    }

    public string CertificateFile { get; }
    public string KeyFile { get; }
    public X509Certificate2 Certificate => current.Certificate;
    // Certificates sent with it: its own and the intermediates that lead to a root.
    public int ChainLength => current.ChainLength;
    internal SslStreamCertificateContext Context => current.Context;
    // Looks in a row at changed files that didn't load.
    public int FailedLooks { get; private set; }
    // Why they didn't, while they don't.
    public string? Problem { get; private set; }

    public DateTime NotBeforeUtc => Certificate.NotBefore.ToUniversalTime();
    public DateTime NotAfterUtc => Certificate.NotAfter.ToUniversalTime();

    // The names players can reach the relay by, as the certificate lists them.
    public IReadOnlyList<string> Names
    {
        get
        {
            var names = Certificate.Extensions.Cast<X509Extension>().Where(e => e.Oid?.Value == SubjectAlternativeName)
                .SelectMany(e => new X509SubjectAlternativeNameExtension(e.RawData, e.Critical).EnumerateDnsNames()).ToList();
            return names.Count > 0 ? names : [Certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false)];
        }
    }

    // "Let's Encrypt (R11)", from the issuer's organization and name.
    public string Issuer
    {
        get
        {
            var name = Certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: true);
            var organization = Certificate.IssuerName.EnumerateRelativeDistinguishedNames()
                .Where(n => !n.HasMultipleElements && n.GetSingleElementType().Value == Organization).Select(n => n.GetSingleElementValue()).FirstOrDefault();
            return organization is { Length: > 0 } && organization != name ? $"{organization} ({name})" : name;
        }
    }

    public bool IsSelfSigned => Certificate.SubjectName.RawData.AsSpan().SequenceEqual(Certificate.IssuerName.RawData);

    // Throws RelayCertificateException with what to do about it.
    public static RelayCertificate Load(string certificateFile, string keyFile)
    {
        var (certificatePem, keyPem, fingerprint) = Read(certificateFile, keyFile);
        return new RelayCertificate(certificateFile, keyFile, Parse(certificatePem, keyPem, certificateFile, keyFile, fingerprint));
    }

    // certbot's files for a domain: <configDirectory>/live/<domain>/fullchain.pem and privkey.pem.
    public static RelayCertificate LoadCertbot(string domain, string configDirectory = "/etc/letsencrypt")
    {
        var lineage = Path.Combine(configDirectory, "live", domain);
        return Load(Path.Combine(lineage, "fullchain.pem"), Path.Combine(lineage, "privkey.pem"));
    }

    // Reads the files again. A change that doesn't load is retried at the next look before it
    // counts against them: certbot's update can be caught halfway, a new certificate beside the
    // old key.
    public CertificateRefresh Refresh()
    {
        lock (gate)
        {
            try
            {
                var (certificatePem, keyPem, fingerprint) = Read(CertificateFile, KeyFile);
                if (fingerprint.AsSpan().SequenceEqual(current.Fingerprint))
                {
                    (FailedLooks, Problem) = (0, null);
                    return CertificateRefresh.Unchanged;
                }
                current = Parse(certificatePem, keyPem, CertificateFile, KeyFile, fingerprint);
                (FailedLooks, Problem) = (0, null);
                return CertificateRefresh.Renewed;
            }
            catch (RelayCertificateException e)
            {
                FailedLooks++;
                Problem = e.Message;
                return FailedLooks >= 2 ? CertificateRefresh.Failing : CertificateRefresh.Unchanged;
            }
        }
    }

    // What the hoster should do about the certificate's dates as of `nowUtc`; null while there's
    // nothing to do. An ACME client renews a certificate with about a third of its lifetime
    // left, so one with a quarter left has missed its renewal.
    public string? ExpiryWarning(DateTime nowUtc)
    {
        var name = Names[0];
        if (nowUtc < NotBeforeUtc)
            return $"The certificate for {name} isn't valid until {NotBeforeUtc:yyyy-MM-dd HH:mm} UTC, so players can't connect yet. " +
                   "If that date is right, wait for it; if it's in the past, this machine's clock is wrong.";
        var left = NotAfterUtc - nowUtc;
        var lifetime = NotAfterUtc - NotBeforeUtc;
        if (left <= TimeSpan.Zero)
            return $"The certificate for {name} EXPIRED on {NotAfterUtc:yyyy-MM-dd HH:mm} UTC: players can't connect over wss:// until it's renewed. {RenewalAdvice()}";
        if (left > lifetime / 4) return null;
        return $"The certificate for {name} expires in {Describe(left)} ({NotAfterUtc:yyyy-MM-dd HH:mm} UTC) and hasn't been renewed. {RenewalAdvice()}";
    }

    // A tenth of its lifetime left: worth saying more often.
    public bool IsUrgent(DateTime nowUtc) => NotAfterUtc - nowUtc <= (NotAfterUtc - NotBeforeUtc) / 10;

    // Where certbot keeps how to renew this certificate, when it's one of certbot's.
    public string? CertbotRenewalFile
    {
        get
        {
            var lineage = Path.GetDirectoryName(Path.GetFullPath(CertificateFile));
            var live = lineage == null ? null : Path.GetDirectoryName(lineage);
            if (live == null || !string.Equals(Path.GetFileName(live), "live", StringComparison.Ordinal)) return null;
            var file = Path.Combine(Path.GetDirectoryName(live)!, "renewal", Path.GetFileName(lineage) + ".conf");
            return File.Exists(file) ? file : null;
        }
    }

    private string RenewalAdvice() => CertbotRenewalFile != null
        ? "certbot should have renewed it by now, so its renewal is failing: run `sudo certbot renew --dry-run` to see why, " +
          "and `systemctl list-timers | grep certbot` to check that its timer runs. The relay serves the renewed files within a minute."
        : "Renew it (with certbot: `sudo certbot renew`); the relay serves the new files within a minute.";

    private static string Describe(TimeSpan span) => span.TotalDays >= 2 ? $"{span.TotalDays:F0} days"
        : span.TotalHours >= 2 ? $"{span.TotalHours:F0} hours"
        : $"{Math.Max(1, span.TotalMinutes):F0} minutes";

    // A TLS handshake with the certificate over this machine's loopback, as a player's client
    // would make it: the protocol and cipher it agreed, and how the client judged the chain.
    public async Task<(SslProtocols Protocol, string Cipher, SslPolicyErrors Errors)> HandshakeAsync(CancellationToken token)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync(token);
            await using var server = new SslStream(accepted.GetStream());
            await server.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificateContext = Context }, token);
        }, token);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, token);
        var errors = SslPolicyErrors.None;
        await using var stream = new SslStream(client.GetStream(), false, (_, _, _, found) =>
        {
            errors = found;
            return true;
        });
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = Names[0].Replace("*", "relay") }, token);
        await serving;
        return (stream.SslProtocol, stream.NegotiatedCipherSuite.ToString(), errors);
    }

    private static (string Certificate, string Key, byte[] Fingerprint) Read(string certificateFile, string keyFile)
    {
        var certificate = ReadFile(certificateFile, "certificate");
        var key = ReadFile(keyFile, "private key");
        return (certificate, key, SHA256.HashData(Encoding.UTF8.GetBytes(certificate + "\n" + key)));
    }

    private static string ReadFile(string path, string what)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new RelayCertificateException($"The {what} file {path} doesn't exist. " + (InCertbotLive(path)
                ? "`sudo certbot certificates` lists the certificates certbot has and where their files are."
                : "Check the path."), e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new RelayCertificateException($"The relay's user ({Environment.UserName}) isn't allowed to read the {what} file {path}. " + (InCertbotLive(path)
                ? "certbot lets only root read its private keys. Give the relay's user access to them: " +
                  $"`sudo chgrp -R {Environment.UserName} /etc/letsencrypt/live /etc/letsencrypt/archive && " +
                  "sudo chmod -R g+rX /etc/letsencrypt/live /etc/letsencrypt/archive` (certbot gives a renewed key the old one's group " +
                  "and mode, so this lasts). In Docker, use the container user's group instead (1654 for the .NET images)."
                : "Give the relay's user read access to it."), e);
        }
        catch (IOException e)
        {
            throw new RelayCertificateException($"Couldn't read the {what} file {path}: {e.Message}", e);
        }
    }

    private static bool InCertbotLive(string path)
        => Path.GetFullPath(path).Replace('\\', '/').Contains("/letsencrypt/live/", StringComparison.Ordinal);

    private static Loaded Parse(string certificatePem, string keyPem, string certificateFile, string keyFile, byte[] fingerprint)
    {
        try
        {
            var chain = new X509Certificate2Collection();
            try
            {
                chain.ImportFromPem(certificatePem);
            }
            catch (CryptographicException e)
            {
                throw new RelayCertificateException($"{certificateFile} isn't a PEM certificate ({e.Message}). Point --tls-cert at certbot's fullchain.pem.", e);
            }
            if (chain.Count == 0)
                throw new RelayCertificateException($"{certificateFile} holds no certificate. Point --tls-cert at certbot's fullchain.pem.");

            X509Certificate2 certificate;
            try
            {
                certificate = X509Certificate2.CreateFromPem(certificatePem, keyPem);
            }
            catch (Exception e) when (e is CryptographicException or ArgumentException)
            {
                throw new RelayCertificateException($"The private key in {keyFile} doesn't go with the certificate in {certificateFile}, or isn't a " +
                                                    $"PEM private key ({e.Message}). Use fullchain.pem and privkey.pem from the same certbot folder.", e);
            }
            // SChannel can't serve a key that lives only in memory.
            if (OperatingSystem.IsWindows())
            {
                using var ephemeral = certificate;
                certificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
            }
            var intermediates = new X509Certificate2Collection(chain.Skip(1).ToArray());
            var context = SslStreamCertificateContext.Create(certificate, intermediates, offline: true);
            return new Loaded(certificate, context, chain.Count, fingerprint);
        }
        catch (Exception e) when (e is DllNotFoundException or TypeInitializationException or EntryPointNotFoundException)
        {
            throw new RelayCertificateException("TLS needs OpenSSL, and the relay couldn't load it (" + e.GetBaseException().Message + "). " +
                                                "Install it: `sudo apt install libssl3` on Debian or Ubuntu, `sudo dnf install openssl-libs` on Fedora or RHEL.", e);
        }
        catch (CryptographicException e)
        {
            throw new RelayCertificateException($"The certificate in {certificateFile} can't be served with the key in {keyFile}: {e.Message}", e);
        }
    }
}
