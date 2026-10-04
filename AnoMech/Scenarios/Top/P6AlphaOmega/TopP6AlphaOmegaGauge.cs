using System;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// The party's limit break gauge as Alpha Omega moves it: a tick every few seconds once a limit
// break has landed, the mechanics' fills, and a third bar that can be held a tick short of full.
public sealed class TopP6AlphaOmegaGauge
{
    public float Units { get; private set; } = LimitBreak.GaugeFull;
    public bool ThirdBarHeld { get; private set; } = true;
    public float NextTick { get; private set; } = float.MaxValue;

    public bool Full => Units >= LimitBreak.GaugeFull;

    public void Spend(float units, float now)
    {
        Units = MathF.Max(0f, Units - units);
        NextTick = now + LimitBreak.GaugeFirstTick;
    }

    public void Add(float units)
        => Units = MathF.Min(ThirdBarHeld ? LimitBreak.GaugeFull - LimitBreak.GaugeTick : LimitBreak.GaugeFull, Units + units);

    public void ReleaseThirdBar()
    {
        ThirdBarHeld = false;
        Add(LimitBreak.GaugeTick);
    }

    // At most one tick a call, as the run checks once a frame.
    public bool Tick(float now)
    {
        if (now < NextTick) return false;
        NextTick += LimitBreak.GaugeTickSeconds;
        Add(LimitBreak.GaugeTick);
        return true;
    }
}
