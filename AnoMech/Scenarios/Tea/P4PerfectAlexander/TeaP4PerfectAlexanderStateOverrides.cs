using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

public sealed class TeaP4PerfectAlexanderStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    public P4Practice Practice { get; set; }
    // Null = random.
    public bool? FinalWordMotionFirst { get; set; }
    public bool? OpticalSpreadFirst { get; set; }
    // 1 or 2: the second or third Perfect Alexander clone from the west.
    public int? AlphaSafeClone { get; set; }
    public bool? AlphaFirstMotion { get; set; }
    public bool? AlphaSecondMotion { get; set; }
    public bool? BetaOpticalStack { get; set; }
    // 1 or 2: the second or third clone from the west holds Fate Calibration β's Optical Sight.
    public int? BetaOpticalClone { get; set; }
    // 0, 1 or 3: the clone that jumps the east, south or west bait shows the donut.
    public int? BetaDonutClone { get; set; }

    // Headless runs pin the rest of the roll. Internal, so settings sync and the summary skip them.
    internal IReadOnlyList<PartyRole>? OpticalStackTargets { get; set; }
    internal IReadOnlyList<int>?[] JudgmentOrders { get; } = new IReadOnlyList<int>?[2];
    internal IReadOnlyList<PartyRole>? GraceTargets { get; set; }
    internal int? BotSeed { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    public PerRoleSetting<FinalWordDebuff> FinalWord { get; set; } = new();
    public PerRoleSetting<bool> OpticalStack { get; set; } = new();
    public PerRoleSetting<AlphaSentence> Alpha { get; set; } = new();
    public PerRoleSetting<BetaRole> Beta { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        conflicts.AtMost(1, Claiming(FinalWord, FinalWordDebuff.LightBeacon), "the light beacon");
        conflicts.AtMost(1, Claiming(FinalWord, FinalWordDebuff.DarkBeacon), "the dark beacon");
        conflicts.AtMost(3, Claiming(FinalWord, FinalWordDebuff.LightFollower), "a light follower's debuff");
        conflicts.AtMost(3, Claiming(FinalWord, FinalWordDebuff.DarkFollower), "a dark follower's debuff");
        conflicts.AtMost(2, PerRole.All.Where(r => OpticalStack[r] == true).ToList(), "an Optical Sight stack");
        conflicts.AtLeast(2, PerRole.All.Where(r => OpticalStack[r] == false).ToList(), 8, "an Optical Sight stack");
        conflicts.AtMost(1, Claiming(Alpha, AlphaSentence.SharedSentence), "Fate Calibration α's shared sentence");
        conflicts.AtMost(1, Claiming(Alpha, AlphaSentence.Defamation), "Fate Calibration α's defamation");
        conflicts.AtMost(3, Claiming(Alpha, AlphaSentence.AggravatedAssault), "an aggravated assault");
        conflicts.AtMost(3, Claiming(Alpha, AlphaSentence.Nothing), "Fate Calibration α without a sentence");
        foreach (var role in Enum.GetValues<BetaRole>())
            conflicts.AtMost(1, Claiming(Beta, role), $"Fate Calibration β's {role}");
        return conflicts;
    }

    private static List<PartyRole> Claiming<T>(PerRoleSetting<T> setting, T value) where T : struct
        => PerRole.All.Where(r => setting[r] is { } v && EqualityComparer<T>.Default.Equals(v, value)).ToList();
}
