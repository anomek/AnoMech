using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P6AlphaOmega.TopP6AlphaOmegaConstants;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// The gauge and everyone's Dynamis as a practice finds them: the phase's limit breaks, fills and
// Brilliant Dynamis before the start, played out frame by frame by the run's own rules with every
// seat pressing on time. What they leave still to come (a press waiting on the gauge, one on its
// way down, a refund) is left in Owed for the run to finish.
public sealed class TopP6AlphaOmegaFastForward(float start)
{
    public enum Step { Fill, Dynamis, ThirdBar, Press, Land, Refund }

    public readonly record struct Pending(float At, Step Step, PartyRole Role = default, LimitBreakKind Kind = default, float Units = 0f,
        float Waited = 0f);

    private const float FrameSeconds = 1f / 60f;
    private readonly List<Pending> owed = [];
    private readonly HashSet<PartyRole> spark = [];

    public TopP6AlphaOmegaGauge Gauge { get; } = new();
    public float LandsAt { get; private set; }
    public bool DynamisGiven { get; private set; }
    public IReadOnlySet<PartyRole> Spark => spark;
    public IReadOnlyList<Pending> Owed => owed;

    public void Fill(float at, float units) => owed.Add(new(at, Step.Fill, Units: units));
    public void Dynamis(float at) => owed.Add(new(at, Step.Dynamis));
    public void ThirdBar(float at) => owed.Add(new(at, Step.ThirdBar));
    public void Press(float at, PartyRole role, LimitBreakKind kind) => owed.Add(new(at, Step.Press, role, kind));

    public void Run()
    {
        for (var now = 0f; now < start; now += FrameSeconds)
        {
            while (NextDue(now) is { } due)
            {
                owed.Remove(due);
                Play(due, now);
            }
            Gauge.Tick(now);
        }
    }

    // First in time, then in the order it was added, as the event queue fires them.
    private Pending? NextDue(float now)
    {
        Pending? next = null;
        foreach (var pending in owed)
            if (pending.At <= now && (next == null || pending.At < next.Value.At)) next = pending;
        return next;
    }

    private void Play(Pending pending, float now)
    {
        switch (pending.Step)
        {
            case Step.Fill:
                Gauge.Add(pending.Units);
                break;
            case Step.Dynamis:
                DynamisGiven = true;
                break;
            case Step.ThirdBar:
                Gauge.ReleaseThirdBar();
                break;
            case Step.Press when !Gauge.Full || now < LandsAt:
                if (pending.Waited < LimitBreakWaitLimit)
                    owed.Add(pending with { At = now + LimitBreakWaitStep, Waited = pending.Waited + LimitBreakWaitStep });
                break;
            case Step.Press:
                LandsAt = now + TopP6AlphaOmegaScenario.LandsAfter(pending.Kind);
                owed.Add(pending with { At = LandsAt, Step = Step.Land });
                break;
            case Step.Land when Gauge.Full:
                Gauge.Spend(LimitBreak.GaugeFull, now);
                owed.Add(pending with { At = now + TopP6AlphaOmegaScenario.RefundDelay(pending.Kind), Step = Step.Refund, Units = LimitBreak.GaugeFull });
                break;
            case Step.Refund when DynamisGiven && spark.Add(pending.Role):
                Gauge.Add(pending.Units);
                break;
        }
    }
}
