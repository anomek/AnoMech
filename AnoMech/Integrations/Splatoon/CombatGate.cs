namespace AnoMech.Integrations.Splatoon;

// Decides, frame by frame, what the InCombat condition should read while a scenario runs.
// A restart ends one encounter and begins the next on the same frame, but Splatoon only resets
// its scripts on the condition's falling edge, which it polls once per frame: the flag has to
// read false for a few frames in between or the restart is invisible to it.
internal sealed class CombatGate
{
    public const int GapFrames = 3;

    private bool pending;
    private int gapLeft;

    public bool Engaged { get; private set; }

    // True from Begin until End: the caller holds the encounter conditions for this whole span.
    public bool Owns => pending || Engaged;

    public void Begin()
    {
        pending = true;
        Engaged = false;
        gapLeft = GapFrames;
    }

    // Returns whether the gate held the conditions, i.e. whether the caller must clear them.
    public bool End()
    {
        var owned = Owns;
        pending = false;
        Engaged = false;
        gapLeft = 0;
        return owned;
    }

    // The InCombat value to write this frame, or null to leave the condition alone.
    public bool? Tick()
    {
        if (Engaged) return true;
        if (!pending) return null;
        if (gapLeft > 0)
        {
            gapLeft--;
            return false;
        }
        pending = false;
        Engaged = true;
        return true;
    }
}
