using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

public enum ArrowSoakMode
{
    DirectorEObjMod,
    SetSharedTimelineState,
    Despawn,
}

// null/default leaves the field randomized at scenario start.
public sealed class UmadP1TeleTrouncingStateOverrides
{
    // 106 through the dispatcher leaves our arrows untouched: the director never registered them.
    public ArrowSoakMode ArrowSoak { get; set; } = ArrowSoakMode.SetSharedTimelineState;
    // true = DPS gets the "different" arrow pairs; false = supports do.
    public bool? DpsGetsDifferent { get; set; }
    public bool? DpsGetsConfused { get; set; }
    // The matching category's directions, in PartyRole order.
    public TelePortentDirection[]? MatchingDirections { get; set; }
    // The different category's roles in cycle order: Up+Right, Right+Down, Down+Left, Left+Up.
    public PartyRole[]? DifferentCycleRoles { get; set; }
    // Per DifferentCycleRoles slot: true = its second direction expires at 7s.
    public bool[]? DifferentPolarity { get; set; }

    public PartyRole? ConfettiStackSupport { get; set; }
    public PartyRole? ConfettiStackDps { get; set; }

    // true = AveMaria (look toward the NW statue); false = IndolentWill (look away from the NE one).
    public bool? GazeInverted { get; set; }

    // true = Flagrant Fire III actually resolves as a stack (2 points, 4 each); false = spread.
    public bool? FireIsStack { get; set; }
    // Lie: the shown stack/spread icon is the opposite of the real outcome.
    public bool? FireIsLie { get; set; }
    public PartyRole? FireStackSupport { get; set; }
    public PartyRole? FireStackDps { get; set; }

    // Lie: 2 fake line telegraphs also appear (4 total).
    public bool? ThunderIsLie { get; set; }
    // true = the diagonal runs the mirrored way (NW-SE vs NE-SW).
    public bool? ThunderOrientationFlipped { get; set; }
    // 0 = real lines at diagonal slots {0,2}; 1 = slots {1,3}.
    public int? ThunderRealOffset { get; set; }

    // Debug knobs for the statue props: bind them to the instance content director (the real
    // packets' EventId) and/or force their SharedGroup active after spawn and after every beat.
    public bool PropsBindDirector { get; set; } = true;
    public bool PropsForceActive { get; set; } = false;
    // Spawns the props' own statue VFX files directly, bypassing the SharedGroup.
    public bool PropsStaticVfxTest { get; set; } = false;
    // ActorControl is the real server packet through the client's own dispatcher.
    public PropBeatMode PropsBeatMode { get; set; } = PropBeatMode.ActorControl;

    public bool HoldHaze { get; set; } = false;
}
