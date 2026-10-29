namespace AnoMech.Relay.Tests;

// What /proc/self/maps shows of a relay's OpenSSL.
public class RelayOpenSslTests
{
    private const string Ssl = "/usr/lib/x86_64-linux-gnu/libssl.so.3";
    private const string Crypto = "/usr/lib/x86_64-linux-gnu/libcrypto.so.3";

    private static string Maps(bool sslReplaced = false, bool libcReplaced = false) => string.Join('\n',
        "55d0c0a00000-55d0c0a01000 r--p 00000000 08:01 2097      /home/relay/anomech-relay",
        $"7f2a1c000000-7f2a1c021000 r--p 00000000 08:01 1835128   {Ssl}",
        $"7f2a1c021000-7f2a1c0a1000 r-xp 00021000 08:01 1835128   {Ssl}{(sslReplaced ? " (deleted)" : "")}",
        $"7f2a1d000000-7f2a1d200000 r--p 00000000 08:01 1835130   {Crypto}",
        $"7f2a1e000000-7f2a1e021000 r--p 00000000 08:01 1835140   /usr/lib/x86_64-linux-gnu/libc.so.6{(libcReplaced ? " (deleted)" : "")}",
        "7ffd0d000000-7ffd0d021000 rw-p 00000000 00:00 0         [stack]",
        "7ffd0e000000-7ffd0e002000 r-xp 00000000 00:00 0");

    [Test]
    public void FindsTheOpenSslItRunsOnOnce()
    {
        Assert.That(RelayOpenSsl.Libraries(Maps()).Select(l => l.Path), Is.EqualTo(new[] { Ssl, Crypto }));
        Assert.That(RelayOpenSsl.Replaced(Maps()), Is.Empty);
    }

    // An update replaces the files; the copies already loaded show as deleted.
    [Test]
    public void TellsWhenAnUpdateReplacedIt()
    {
        Assert.That(RelayOpenSsl.Replaced(Maps(sslReplaced: true)), Is.EqualTo(new[] { Ssl }));
        Assert.That(RelayOpenSsl.Replaced(Maps(libcReplaced: true)), Is.Empty, "only OpenSSL's libraries count");
    }
}
