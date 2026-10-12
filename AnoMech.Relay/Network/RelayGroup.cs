using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AnoMech.Network;

// One relay of several run together, one per region say. Every member starts with the same list
// (AnoMech.Relay.Host --relays) and serves it from /info, so a client that knows one member knows
// them all. Number decides which session codes the member issues (RelayWire.CodeServer); changing
// it would send codes already handed out to the wrong relay.
public sealed record RelayGroupMember(int Number, string Url, string Location);

public static class RelayGroup
{
    public const int MaxLocationLength = 64;
    // A join for another member's code is turned away naming that member's address, in a close
    // reason WebSocket caps at 123 bytes.
    public const int MaxUrlBytes = 100;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Throws JsonException when the text isn't a list of members.
    public static List<RelayGroupMember> Parse(string json)
        => JsonSerializer.Deserialize<List<RelayGroupMember>>(json, Json) ?? throw new JsonException("Expected a list of relays.");

    // Null when the list is usable, otherwise what's wrong with it.
    public static string? Validate(IReadOnlyList<RelayGroupMember> members)
    {
        if (members.Count is 0 or > RelayWire.CodeServers) return $"a group has 1 to {RelayWire.CodeServers} relays.";
        var origins = new HashSet<string>();
        foreach (var member in members)
        {
            if (member is null) return "an entry is empty.";
            if (member.Number is < 0 or >= RelayWire.CodeServers)
                return $"relay number {member.Number} isn't between 0 and {RelayWire.CodeServers - 1}.";
            if (members.Count(m => m?.Number == member.Number) > 1) return $"relay number {member.Number} is listed twice.";
            if (!IsRelayAddress(member.Url))
                return $"relay {member.Number}'s url has to be a ws:// or wss:// address of at most {MaxUrlBytes} bytes.";
            if (!origins.Add(RelayWire.Origin(member.Url))) return $"{member.Url} is listed twice.";
            if (string.IsNullOrWhiteSpace(member.Location) || member.Location.Length > MaxLocationLength || member.Location.Any(char.IsControl))
                return $"relay {member.Number}'s location has to be 1 to {MaxLocationLength} characters.";
        }
        return null;
    }

    public static bool IsRelayAddress(string? url)
        => url is not null && Encoding.UTF8.GetByteCount(url) <= MaxUrlBytes
           && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "ws" or "wss"
           && uri.Host.Length > 0 && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
