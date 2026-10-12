using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AnoMech.Core;
using AnoMech.Network;

namespace AnoMech.Multiplayer;

// Home: the number of the server the list came from.
internal sealed record RelayGroupInfo(int Home, RelayGroupMember[] Members)
{
    public RelayGroupMember? Member(int number) => Members.FirstOrDefault(m => m.Number == number);
}

// Servers run as a group (AnoMech.Relay.Host --relays). A host picks the closest member, and since
// each session code names the member holding it (RelayWire.CodeServer), a joiner dials that member
// directly. The active server's list is kept in the configuration, so the others stay usable while
// it's down.
internal static class RelayGroups
{
    // How much closer another member has to be before a host leaves the active server for it, so
    // near-equal routes don't flip from one measurement to the next.
    internal const double NearTieMs = 10;

    private static string parsedJson = "";
    private static RelayGroupInfo? parsed;
    // Set once the active server has answered with its list since the plugin loaded: only then does
    // a code naming none of its members mean a typo rather than a list out of date.
    private static string confirmedOrigin = "";

    internal static RelayGroupInfo? Current
    {
        get
        {
            var config = Plugin.Config;
            if (config.RelayGroupOrigin.Length == 0 || config.RelayGroupOrigin != Configuration.OriginOf(config.ActiveRelayUrl())) return null;
            if (config.RelayGroupJson != parsedJson) (parsedJson, parsed) = (config.RelayGroupJson, Parse(config.RelayGroupJson));
            return parsed;
        }
    }

    internal static bool Confirmed => confirmedOrigin.Length != 0 && confirmedOrigin == Configuration.OriginOf(Plugin.Config.ActiveRelayUrl());

    // The active server's list has been fetched at least once, whether or not it names a group.
    internal static bool Known
    {
        get
        {
            var config = Plugin.Config;
            return config.RelayGroupOrigin.Length != 0 && config.RelayGroupOrigin == Configuration.OriginOf(config.ActiveRelayUrl());
        }
    }

    // A list that came over plain HTTP counts only for a server set up unencrypted: otherwise anyone
    // on the network path could send hosts and joiners to servers of their own.
    internal static void Learn(string serverUrl, RelayInfo info)
    {
        var config = Plugin.Config;
        var origin = Configuration.OriginOf(serverUrl);
        if (origin.Length == 0 || origin != Configuration.OriginOf(config.ActiveRelayUrl())) return;
        if (!info.Encrypted && !IsPlainAddress(serverUrl)) return;
        confirmedOrigin = origin;
        var json = FromInfo(info) is { } group ? JsonSerializer.Serialize(group, RelayGroup.Json) : "";
        if (config.RelayGroupOrigin == origin && config.RelayGroupJson == json) return;
        config.RelayGroupOrigin = origin;
        config.RelayGroupJson = json;
        config.Save();
    }

    internal static RelayGroupInfo? FromInfo(RelayInfo info)
    {
        if (info.Server is null && info.Group is null) return null;
        if (Checked(info.Server, info.Group) is { } group) return group;
        DiagnosticLog.Warn("[RelayGroups] The server's list of servers isn't usable -- ignoring it.");
        return null;
    }

    private static RelayGroupInfo? Parse(string json)
    {
        if (json.Length == 0) return null;
        try { return JsonSerializer.Deserialize<RelayGroupInfo>(json, RelayGroup.Json) is { } saved ? Checked(saved.Home, saved.Members) : null; }
        catch (JsonException) { return null; }
    }

    private static RelayGroupInfo? Checked(int? home, RelayGroupMember[]? members)
        => home is { } number && members is not null && RelayGroup.Validate(members) is null && members.Any(m => m.Number == number)
            ? new RelayGroupInfo(number, members)
            : null;

    private static bool IsPlainAddress(string url)
    {
        var text = url.Trim();
        return text.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    // Best first: the member picked in Settings, or else every member that answered a ping, closest
    // first. Just the active server until a ping is in.
    internal static List<RelayGroupMember> HostOrder(RelayGroupInfo group, string chosenUrl, Func<string, double?> pingOf)
    {
        if (group.Members.FirstOrDefault(m => SameServer(m.Url, chosenUrl)) is { } chosen) return [chosen];
        var home = group.Member(group.Home)!;
        var answered = group.Members.Where(m => pingOf(m.Url) is not null).OrderBy(m => pingOf(m.Url)).ToList();
        if (answered.Count == 0) return [home];
        if (answered.Contains(home) && pingOf(home.Url) <= pingOf(answered[0].Url) + NearTieMs)
        {
            answered.Remove(home);
            answered.Insert(0, home);
        }
        return answered;
    }

    // The active server is dialed as the player set it up, so its password and fallback rules apply.
    internal static string DialUrl(RelayGroupInfo group, RelayGroupMember member, string homeUrl)
        => member.Number == group.Home ? homeUrl : member.Url;

    // A code naming no member goes to the active server, which knows any member added since the
    // list was saved and sends the join on (RelayWire.MovedTo).
    internal static string JoinUrl(RelayGroupInfo? group, string code, string homeUrl)
        => group?.Member(RelayWire.CodeServer(code)) is { } member ? DialUrl(group, member, homeUrl) : homeUrl;

    internal static bool SameServer(string a, string b)
    {
        var origin = Configuration.OriginOf(a);
        return origin.Length != 0 && origin == Configuration.OriginOf(b);
    }

    internal static string? LocationOf(string? url)
        => url is null ? null : Current?.Members.FirstOrDefault(m => SameServer(m.Url, url))?.Location;
}
