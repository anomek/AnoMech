namespace AnoMech.Multiplayer;

// The server multiplayer uses unless the player picks their own in Settings. Dialed as wss://
// outright: it serves TLS, so the unencrypted fallback a bare address gets could only downgrade it.
public static class DefaultRelay
{
    public const string Address = "sim.everscloset.com";
    public const string Url = "wss://" + Address;
    public const string Location = "US East (Virginia)";
}
