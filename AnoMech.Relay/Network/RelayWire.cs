using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AnoMech.Network;

// Shared by the relay and the plugin. The relay only frames and routes: it never decompresses or
// parses a forwarded body, so every check on message content is the receiving client's.
public static class RelayWire
{
    public const int Version = 1;
    public const int PrefixBytes = 22;

    // Decoded size caps, applied by the receiver.
    public const int MaxMessageBytes = 8 * 1024 * 1024;       // from the host
    public const int MaxPeerMessageBytes = 64 * 1024;         // from a peer; none comes near this

    // What a host accepts from any one peer, decoded. A peer sends a pose per frame and little
    // else, so these sit far above real use while stopping one sender from swamping the host.
    public const int PeerMessagesPerSecond = 3_000;
    public const int PeerBytesPerSecond = 1024 * 1024;

    // relayControl is the only body the relay reads, so it has to arrive uncompressed and small.
    public const int MaxControlBytes = 1024;
    public const string ControlType = "relayControl";
    public const string NoticeType = "relayNotice";

    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static bool IsValidSecret(string? secret)
    {
        if (secret is not { Length: 64 }) return false;
        foreach (var c in secret)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    public static Guid PeerId(string secret)
    {
        if (!IsValidSecret(secret)) throw new InvalidDataException("Invalid peer credential.");
        return new Guid(SHA256.HashData(Convert.FromHexString(secret)).AsSpan(0, 16));
    }

    // What a client presents to one relay in place of its install secret. Keyed to that relay's
    // address, so a relay operator, or anyone reading a ws:// connection, can't replay it on
    // another relay to act as this player there; the public id differs per relay as well.
    public static string RelayCredential(string installSecret, string relayAddress)
    {
        string origin;
        try { origin = Origin(relayAddress); }
        catch (Exception) { origin = relayAddress.Trim().ToLowerInvariant(); }
        return Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(installSecret), Encoding.UTF8.GetBytes(origin)));
    }

    // Where a saved relay password may be sent. A bare address is dialed as wss:// first, and a
    // password is only ever sent over wss://, so that's the origin a bare address binds to.
    public static string Origin(string address)
    {
        var text = address.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "wss://" + text;
        var uri = new Uri(text, UriKind.Absolute);
        var scheme = uri.Scheme switch
        {
            "https" or "wss" => "wss",
            "http" or "ws" => "ws",
            _ => throw new ArgumentException("Not a relay address."),
        };
        var port = uri.IsDefaultPort ? (scheme == "wss" ? 443 : 80) : uri.Port;
        return $"{scheme}://{uri.IdnHost.ToLowerInvariant()}:{port}";
    }

    // The relay writes this prefix on every frame it forwards. A connection id of 0 with an
    // empty sender marks a notice from the relay itself.
    public static byte[] Envelope(bool fromHost, uint connectionId, Guid sender, bool compressed, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[PrefixBytes + payload.Length];
        frame[0] = (byte)(fromHost ? 1 : 0);
        BitConverter.TryWriteBytes(frame.AsSpan(1, 4), connectionId);
        sender.TryWriteBytes(frame.AsSpan(5, 16));
        frame[21] = (byte)(compressed ? 1 : 0);
        payload.CopyTo(frame.AsSpan(PrefixBytes));
        return frame;
    }

    // Reads only the first property, so the relay's cost is the same however large the frame.
    public static bool IsControl(ReadOnlySpan<byte> json)
    {
        if (json.Length > MaxControlBytes) return false;
        try
        {
            var reader = new Utf8JsonReader(json);
            return reader.Read() && reader.TokenType == JsonTokenType.StartObject
                && reader.Read() && reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("t")
                && reader.Read() && reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(ControlType);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return false; }
    }

    public static byte[] Decode(byte[] payload, bool compressed, TrafficBudget budget, int maxBytes = MaxMessageBytes)
    {
        budget.Message();
        if (!compressed)
        {
            if (payload.Length > maxBytes) throw new InvalidDataException("Message too large.");
            budget.Bytes(payload.Length);
            return payload;
        }
        using var input = new MemoryStream(payload);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = brotli.Read(chunk, 0, chunk.Length)) > 0)
        {
            budget.Bytes(read);
            if (output.Length + read > maxBytes) throw new InvalidDataException("Message too large.");
            output.Write(chunk, 0, read);
        }
        return output.ToArray();
    }

    // Peer message types; anything else must come from the host. Keep in step with
    // MultiplayerManager.ClaimedPeerId.
    private static bool IsPeerMessage(string type) => type is "hello" or "claim" or "release" or "pose" or "pong"
        or "startCheckResponse" or "startAbort" or "sessionEnded" or "resetRequest" or "leaveRequest"
        or "selfMitigation" or "peerAppliedEnemyStatus" or "peerAppliedRoleStatus" or "peerLimitBreak" or "peerClearedStatus";

    // Before typed deserialization. A host message only needs its type here: the host already
    // runs the whole simulation, and deserialization refuses anything malformed. A peer message
    // must be a peer type naming its authenticated sender exactly once at the top level (the
    // deserializer keeps the last of repeated properties, so a check on the first could be
    // fooled). Nested values are skipped rather than walked, so this stays cheap.
    public static string Validate(ReadOnlySpan<byte> json, bool fromHost, Guid sender)
    {
        var reader = new Utf8JsonReader(json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject
            || !reader.Read() || reader.TokenType != JsonTokenType.PropertyName || !reader.ValueTextEquals("t")
            || !reader.Read() || reader.TokenType != JsonTokenType.String || reader.ValueSpan.Length > 64)
            throw new InvalidDataException("Missing message type.");
        var type = reader.GetString()!;
        if (fromHost) return type;
        if (!IsPeerMessage(type)) throw new InvalidDataException("Host message sent by a peer.");

        var names = new List<string>(8) { "t" };
        Guid? claimed = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            foreach (var seen in names)
                if (string.Equals(seen, name, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Duplicate property.");
            names.Add(name);
            if (!reader.Read()) break;
            if (name.Equals("PeerId", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var id)) throw new InvalidDataException("Invalid identity.");
                claimed = id;
            }
            else reader.Skip();
        }
        if (reader.TokenType != JsonTokenType.EndObject) throw new InvalidDataException("Truncated message.");
        if (claimed != sender) throw new InvalidDataException("Claimed identity doesn't match the sender.");
        return type;
    }
}

// Distinct from a malformed message: going over a rate says nothing about the message itself.
public sealed class TrafficLimitException(string message) : Exception(message);

public sealed class TrafficBudget(int messages = int.MaxValue, long bytes = long.MaxValue)
{
    private long window = Stopwatch.GetTimestamp();
    private int messageCount;
    private long byteCount;

    private void Advance()
    {
        var now = Stopwatch.GetTimestamp();
        if (now - window < Stopwatch.Frequency) return;
        window = now;
        messageCount = 0;
        byteCount = 0;
    }

    public void Message()
    {
        Advance();
        if (++messageCount > messages) throw new TrafficLimitException("Message rate exceeded.");
    }

    public void Bytes(int count)
    {
        Advance();
        if ((byteCount += count) > bytes) throw new TrafficLimitException("Uncompressed byte rate exceeded.");
    }
}
