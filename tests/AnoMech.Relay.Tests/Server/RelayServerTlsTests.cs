using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AnoMech.Network;
using static AnoMech.Relay.Tests.TestWire;

namespace AnoMech.Relay.Tests;

// The relay serving wss:// itself.
public class RelayServerTlsTests
{
    private string directory = null!;
    private readonly QuietLog log = new();

    [SetUp]
    public void CreateDirectory() => directory = TestCertificates.NewDirectory();

    [TearDown]
    public void RemoveDirectory() => Directory.Delete(directory, recursive: true);

    private RelayServer Serve(RelayOptions? options = null)
    {
        options ??= new RelayOptions();
        options.BindAddress = IPAddress.Loopback;
        options.Port = 0;
        options.Tls = RelayCertificate.Load(Path.Combine(directory, "fullchain.pem"), Path.Combine(directory, "privkey.pem"));
        var server = new RelayServer(options, log);
        server.Start();
        return server;
    }

    [Test]
    public async Task PlayersHostAndJoinOverWss()
    {
        TestCertificates.WriteValid(directory);
        await using var server = Serve();
        var url = $"wss://127.0.0.1:{server.LocalEndPoint!.Port}";
        using var host = await ConnectAsync($"{url}/host");
        var code = (await GreetingAsync(host)).GetProperty("sessionCode").GetString()!;
        using var peer = await ConnectAsync($"{url}/session/{code}");
        Assert.That((await GreetingAsync(peer)).GetProperty("peerId").GetString(), Is.Not.Empty);
    }

    // Loopback without X-Forwarded-Proto counts as encrypted anyway; with it, only the relay's own
    // TLS can make this request count as encrypted.
    [Test]
    public async Task ItsOwnTlsIsWhatRequireTlsAsksFor()
    {
        TestCertificates.WriteValid(directory);
        var options = new RelayOptions { RequireTls = true };
        await using var server = Serve(options);
        Assert.That(options.Validate(), Is.Null, "no --trusted-proxy needed");
        var (info, _) = await HttpsAsync(server.LocalEndPoint!.Port, "GET /info HTTP/1.1\r\nHost: x\r\nX-Forwarded-Proto: http\r\n\r\n");
        Assert.That(info, Does.StartWith("HTTP/1.1 200 "));
    }

    [Test]
    public async Task PlainRequestAtTheTlsPortIsToldToUseWss()
    {
        TestCertificates.WriteValid(directory);
        await using var server = Serve();
        var response = await PlainHttpAsync(server.LocalEndPoint!.Port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n");
        Assert.That(response, Does.StartWith("HTTP/1.1 400 ").And.Contain("use wss://"));
        Assert.That(server.GetStats().Rejections.TlsHandshake, Is.EqualTo(1));
    }

    [Test]
    public async Task ReloadServesTheRenewalToNewConnections()
    {
        TestCertificates.WriteValid(directory, "first.example.test");
        await using var server = Serve();
        var port = server.LocalEndPoint!.Port;
        Assert.That((await HttpsAsync(port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n")).Served, Is.EqualTo("CN=first.example.test"));

        TestCertificates.WriteValid(directory, "second.example.test");
        server.ReloadCertificate();
        Assert.That((await HttpsAsync(port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n")).Served, Is.EqualTo("CN=second.example.test"));
        Assert.That(log.Lines, Has.Some.Contains("serving the renewed certificate for second.example.test"));
    }

    [Test]
    public async Task KeepsItsCertificateWhenTheNewFilesDontLoad()
    {
        TestCertificates.WriteValid(directory, "first.example.test");
        await using var server = Serve();
        File.WriteAllText(Path.Combine(directory, "privkey.pem"), "");
        server.ReloadCertificate();
        Assert.That(log.Lines, Has.Some.Contains("still serves the certificate it had"));
        Assert.That((await HttpsAsync(server.LocalEndPoint!.Port, "GET /info HTTP/1.1\r\nHost: x\r\n\r\n")).Served, Is.EqualTo("CN=first.example.test"));
    }

    [Test]
    public async Task StartingSaysWhenTheCertificateIsDueForRenewal()
    {
        TestCertificates.Write(directory, "relay.example.test", DateTimeOffset.UtcNow.AddDays(-80), DateTimeOffset.UtcNow.AddDays(5));
        await using var server = Serve();
        Assert.That(log.Lines, Has.Some.Contains("serving as relay.example.test"));
        Assert.That(log.Lines, Has.Some.Contains("expires in 5 days"));
    }

    private static async Task<ClientWebSocket> ConnectAsync(string url)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-AnoMech-Protocol", RelayWire.Version.ToString());
        socket.Options.SetRequestHeader("X-AnoMech-Peer-Secret", RelayWire.NewSecret());
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri(url), timeout.Token);
        return socket;
    }

    private static async Task<JsonElement> GreetingAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
        return JsonDocument.Parse(buffer.AsMemory(0, result.Count)).RootElement;
    }

    // The response, and the subject of the certificate the relay presented.
    private static async Task<(string Response, string Served)> HttpsAsync(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var tls = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "relay.example.test" }).WaitAsync(TimeSpan.FromSeconds(5));
        var served = tls.RemoteCertificate!.Subject;
        await tls.WriteAsync(Bytes(request));
        using var reader = new StreamReader(tls, Encoding.UTF8);
        return (await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5)), served);
    }

    private static async Task<string> PlainHttpAsync(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        await stream.WriteAsync(Bytes(request));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
