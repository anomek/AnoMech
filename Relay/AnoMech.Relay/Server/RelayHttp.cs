using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Relay;

internal sealed class RelayHttpRequest
{
    public required string Method { get; init; }
    // Query stripped, leading/trailing slashes trimmed: "host", "session/ABCD23", "admin/stats".
    public required string Path { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
    public required IPAddress RemoteAddress { get; init; }
    // Bytes that arrived in the same reads as the header block.
    public required byte[] Buffered { get; init; }

    public string? Header(string name) => Headers.GetValueOrDefault(name);

    // Buffered must be empty: a client may not send frames before it has seen the 101, and
    // anything already read here would otherwise be lost to the WebSocket built on the stream.
    public bool IsWebSocketRequest =>
        Method == "GET"
        && string.Equals(Header("Upgrade"), "websocket", StringComparison.OrdinalIgnoreCase)
        && (Header("Connection") ?? "").Split(',').Any(t => t.Trim().Equals("upgrade", StringComparison.OrdinalIgnoreCase))
        && Header("Sec-WebSocket-Version") == "13"
        && Header("Sec-WebSocket-Key") is { } key && IsWebSocketKey(key)
        && Buffered.Length == 0;

    private static bool IsWebSocketKey(string key)
    {
        Span<byte> decoded = stackalloc byte[24];
        return Convert.TryFromBase64String(key, decoded, out var written) && written == 16;
    }
}

// Just enough HTTP/1.1 for the relay: one request per connection, answered and closed, or
// upgraded to a WebSocket. Owned sockets instead of HttpListener because HTTP.sys only lets an
// unelevated process bind loopback without a URL ACL grant -- which rules out embedding the
// relay in the game process for anyone to reach.
internal static class RelayHttp
{
    public const int MaxHeaderBytes = 16 * 1024;
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    // Null for anything malformed or oversized, which the caller answers with a 400.
    public static async Task<RelayHttpRequest?> ReadRequestAsync(Stream stream, IPAddress remoteAddress, CancellationToken token)
    {
        var buffer = new byte[MaxHeaderBytes];
        var length = 0;
        int end;
        while ((end = buffer.AsSpan(0, length).IndexOf(HeaderTerminator)) < 0)
        {
            if (length == buffer.Length) return null;
            var read = await stream.ReadAsync(buffer.AsMemory(length), token);
            if (read == 0) return null;
            length += read;
        }

        var lines = Encoding.Latin1.GetString(buffer, 0, end).Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length != 3 || !requestLine[1].StartsWith('/') || !requestLine[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            return null;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            // Rejects obsolete line folding too: a continuation line starts with whitespace.
            var colon = line.IndexOf(':');
            if (colon <= 0 || line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0) return null;
            var name = line[..colon];
            var value = line[(colon + 1)..].Trim(' ', '\t');
            // Repeats join the way HttpListener's Headers[name] did, which is what a proxy's
            // split X-Forwarded-For chain needs.
            headers[name] = headers.TryGetValue(name, out var existing) ? $"{existing},{value}" : value;
        }

        var target = requestLine[1];
        if (target.IndexOf('?') is var query and >= 0) target = target[..query];
        return new RelayHttpRequest
        {
            Method = requestLine[0], Path = target.Trim('/'), Headers = headers, RemoteAddress = remoteAddress,
            Buffered = buffer[(end + HeaderTerminator.Length)..length],
        };
    }

    public static async Task<string> ReadBodyAsync(Stream stream, RelayHttpRequest request, int maxBytes, CancellationToken token)
    {
        if (request.Header("Transfer-Encoding") != null) throw new InvalidDataException("chunked bodies aren't supported");
        if (!int.TryParse(request.Header("Content-Length"), out var length) || length < 0 || length > maxBytes)
            throw new InvalidDataException("missing or oversized Content-Length");
        var body = new byte[length];
        var have = Math.Min(request.Buffered.Length, length);
        request.Buffered.AsSpan(0, have).CopyTo(body);
        while (have < length)
        {
            var read = await stream.ReadAsync(body.AsMemory(have), token);
            if (read == 0) throw new EndOfStreamException("body ended early");
            have += read;
        }
        return Encoding.UTF8.GetString(body);
    }

    public static Task WriteStatusAsync(Stream stream, int status, CancellationToken token)
        => WriteAsync(stream, $"HTTP/1.1 {status} {Reason(status)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", [], token);

    public static Task WriteJsonAsync(Stream stream, string json, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(json);
        return WriteAsync(stream, $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n", body, token);
    }

    public static async Task<WebSocket> AcceptWebSocketAsync(Stream stream, RelayHttpRequest request, CancellationToken token)
    {
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(request.Header("Sec-WebSocket-Key") + WebSocketGuid)));
        await WriteAsync(stream, $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n", [], token);
        return WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = WebSocket.DefaultKeepAliveInterval });
    }

    private static async Task WriteAsync(Stream stream, string head, byte[] body, CancellationToken token)
    {
        var headBytes = Encoding.ASCII.GetBytes(head);
        var message = new byte[headBytes.Length + body.Length];
        headBytes.CopyTo(message, 0);
        body.CopyTo(message, headBytes.Length);
        await stream.WriteAsync(message, token);
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        426 => "Upgrade Required",
        429 => "Too Many Requests",
        503 => "Service Unavailable",
        _ => "Error",
    };
}
