using Dalamud.Configuration;
using System;
using AnoMech.Network;

namespace AnoMech;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    
    public bool OpenSimMenuOnInn { get; set; } = true;
    public bool OpenSimMenuOnSupportedInstanceSolo { get; set; } = false;
    public bool EnableEventLogging { get; set; } = false;
    public bool SuppressBgm { get; set; } = true;
    public bool EnableMechanicResultMarks { get; set; } = false;
    public bool AutoCollapseWhileRunning { get; set; } = false;
    public string LastSelectedScenario { get; set; } = "";

    // Multiplayer relay address (see AnoMech.Relay.Host/README.md) -- remembered across
    // sessions so the user only has to type it once.
    public string RelayServerUrl { get; set; } = "";

    // Optional shared secret some relays require to connect. Bound to the relay it was entered
    // for (RelayTokenOrigin) and only ever sent there, so pointing the plugin at another relay
    // can't hand it over; never logged.
    public string RelayAccessToken { get; set; } = "";
    public string RelayTokenOrigin { get; set; } = "";

    public int EmbeddedRelayPort { get; set; } = 7890;

    public string TokenForRelay(string url)
    {
        // A password saved before it was tied to a relay belongs to the one saved alongside it.
        if (RelayTokenOrigin.Length == 0 && RelayAccessToken.Length != 0) RelayTokenOrigin = OriginOf(RelayServerUrl);
        var origin = OriginOf(url);
        return origin.Length != 0 && origin == RelayTokenOrigin ? RelayAccessToken : "";
    }

    public static string OriginOf(string url)
    {
        try { return RelayWire.Origin(url); }
        catch (Exception) { return ""; }
    }

    // Stable per-install multiplayer identity. Each relay gets a credential derived from it
    // (RelayWire.RelayCredential); the relay turns that into the public id it stamps on
    // everything we send, so nobody else can act as us. Reused across Host/Join so a peer who
    // drops and rejoins the same session is the same player and keeps their role.
    public string PeerSecret { get; set; } = "";

    public string EnsurePeerSecret()
    {
        if (RelayWire.IsValidSecret(PeerSecret)) return PeerSecret;
        PeerSecret = RelayWire.NewSecret();
        Save();
        return PeerSecret;
    }
    // Resolve the player's own actions client-side, since the sim firewall blocks
    // the server responses that normally grant them. Sprint and limit breaks are always
    // resolved; this gates everything else (shared actions + per-job kits). A multiplayer
    // host's choice holds for its whole session.
    public bool EnableUserActions { get; set; } = true;

    // Seconds before a player cast finishes during which it can no longer be
    // interrupted by move/jump/cancel — the slidecast window the server's
    // ActionEffect ack opens (measured ~0.5s from replay data). Gated by
    // EnableUserActions.
    public float CastInterruptThreshold { get; set; } = 0.5f;

    // Survivable hits kill a human player whose mitigation is below the hit's threshold; off, they always survive. Gated by EnableUserActions.
    public bool EnableTankMitigation { get; set; } = true;

    // Firewall opcode config — updated automatically by OpcodeUpdater on game version change.
    public uint[] ZoneDownOpcodes { get; set; } = [];
    public string ZoneFirewallGameVersion { get; set; } = "";

    // Safe mode (incoming packet firewall):
    //   true  — only ZoneDownOpcodes pass; cuts you off from server traffic
    //           (no party join/leave updates, no ready checks, no duty pops).
    //   false — all incoming packets pass to the engine. You'll see popups
    //           and party updates, but it's easier to break the sim zone.
    // The send-side firewall stays on either way: nothing the client does in
    // the sim zone leaks back to the server.
    public bool SafeMode { get; set; } = true;

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
