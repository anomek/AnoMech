using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

// OneSide puts both line stacks on one light party, so the flex has to happen.
public enum StackSplit { OnePerSide, OneSide }

// The guide has the melee LB3 open the phase, or saved for Blue Screen's cast.
public enum LimitBreakTiming { PhaseStart, BlueScreen, Off }

public sealed class TopP4BlueScreenStateOverrides
{
    // null = random.
    public StackSplit? FirstStacks { get; set; }
    public StackSplit? SecondStacks { get; set; }
    public StackSplit? ThirdStacks { get; set; }
    // null = the guide's melee priority.
    public PartyRole? MeleeLimitBreakBy { get; set; }
    // null = at the phase start.
    public LimitBreakTiming? MeleeLimitBreakTiming { get; set; }
}
