namespace AnoMech.Core.Game;

// Which of the server's cast-cancel variants to send; each prints its own chat line.
public enum CastCancelReason
{
    // The caster was killed mid-cast.
    Interrupted,

    // The encounter called the cast off (a phase transition, a broken add).
    Cancelled,

    // The local player moved, jumped or pressed cancel.
    SelfCancelled,

    // No chat line.
    Silent,
}
