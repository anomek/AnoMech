using System;
using System.Collections.Generic;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Umad.P5Exaflares;

// Per-run randomization: resolves each side's ExaFlareOrder into a concrete length-6 list of line
// indices (1-6) in launch order - the three waves fire pairs ([0],[1]) then ([2],[3]) then ([4],[5]).
public sealed class UmadP5ExaflaresState
{
    public IReadOnlyList<int> LeftOrder { get; }
    public IReadOnlyList<int> RightOrder { get; }

    // Handoff to the bot strat. `Timeline` is the scenario's unscaled clock (bots schedule dodges on
    // it, not the EventTimeScale-scaled AiManager, so they stay frame-locked). `SpreadTick` is the
    // per-frame relaxation step the strat registers; the scenario's Tick drives it. Both unused in solo.
    public EventScheduler Timeline { get; }
    public Action<float>? SpreadTick { get; set; }

    private readonly Rng rng = new();

    public UmadP5ExaflaresState(UmadP5ExaflaresStateOverrides overrides, EventScheduler timeline)
    {
        Timeline = timeline;
        LeftOrder = Resolve(overrides.LeftOrder);
        RightOrder = Resolve(overrides.RightOrder);
    }

    private IReadOnlyList<int> Resolve(ExaFlareOrder order)
    {
        if (order == ExaFlareOrder.Random)
            order = rng.NextObj(
                ExaFlareOrder.Line14_25_36, ExaFlareOrder.Line14_36_25,
                ExaFlareOrder.Line25_14_36, ExaFlareOrder.Line25_36_14,
                ExaFlareOrder.Line36_14_25, ExaFlareOrder.Line36_25_14);

        return order switch
        {
            ExaFlareOrder.Line14_25_36 => [1, 4, 2, 5, 3, 6],
            ExaFlareOrder.Line14_36_25 => [1, 4, 3, 6, 2, 5],
            ExaFlareOrder.Line25_14_36 => [2, 5, 1, 4, 3, 6],
            ExaFlareOrder.Line25_36_14 => [2, 5, 3, 6, 1, 4],
            ExaFlareOrder.Line36_14_25 => [3, 6, 1, 4, 2, 5],
            ExaFlareOrder.Line36_25_14 => [3, 6, 2, 5, 1, 4],
            _ => [1, 4, 2, 5, 3, 6],
        };
    }
}
