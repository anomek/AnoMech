using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

// User-forced rolls for FruP4DarklitDragonsongState; null leaves a field randomized.
public sealed class FruP4DarklitDragonsongStateOverrides
{
    public bool? EastWing { get; set; }
    public PartyRole? ChainedTank { get; set; }
    public PartyRole? ChainedHealer { get; set; }
    // R, C, M1, M2 shuffled: the first two are chained.
    public IReadOnlyList<PartyRole>? DpsOrder { get; set; }
    // 0 bowtie, 1 box, 2 hourglass.
    public int? Shape { get; set; }
    // Lineup slot 0-3 of the chained and of the free Dark Water.
    public int? TetherWaterIndex { get; set; }
    public int? FreeWaterIndex { get; set; }
    public PartyRole? SpiritTakerTarget { get; set; }
}
