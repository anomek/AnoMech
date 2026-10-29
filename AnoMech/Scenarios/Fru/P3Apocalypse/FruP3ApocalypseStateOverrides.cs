using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

// As FruP3ApocalypseState.WaterDurations counts them: none, then its three durations.
public enum ApocalypseWater { None, Short, Medium, Long }

// User-forced rolls for FruP3ApocalypseState; null leaves a field randomized.
public sealed class FruP3ApocalypseStateOverrides
{
    // Eighths of a turn from north, 0-3.
    public int? Rotation { get; set; }
    public bool? Clockwise { get; set; }
    public PartyRole? SpiritTakerTarget { get; set; }
    // Indexed by PartyRole: 0 = no Dark Water, else its duration slot 1-3; two of each. Overrides
    // DarkWater.
    public IReadOnlyList<int>? WaterDurations { get; set; }

    public PerRoleSetting<ApocalypseWater> DarkWater { get; set; } = new();

    public const int SeatsPerWater = 2;

    public static string Label(ApocalypseWater water) => water switch
    {
        ApocalypseWater.None => "no Dark Water",
        ApocalypseWater.Short => "a short Dark Water",
        ApocalypseWater.Medium => "a medium Dark Water",
        _ => "a long Dark Water",
    };

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var water in Enum.GetValues<ApocalypseWater>())
            conflicts.AtMost(SeatsPerWater, PerRole.All.Where(r => DarkWater[r] == water).ToList(), Label(water));
        return conflicts;
    }
}
