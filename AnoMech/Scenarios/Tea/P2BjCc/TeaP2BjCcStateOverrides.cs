using System.Linq;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P2BjCc;

public sealed class TeaP2BjCcStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // Null = random.
    public bool? NisiOnDps { get; set; }
    public PartyRole? FirstWaterHealer { get; set; }
    // 0..3 = N/S, NE/SW, E/W, SE/NW.
    public int? ChakramAxis { get; set; }
    public PartyRole? IceTank { get; set; }

    // Headless runs pin the rest of the roll. Internal, so settings sync and the summary skip them.
    // Every player's damage over time ticks this far into each 3s.
    internal float? TickClock { get; set; }
    // The Plasma Shield stays full until then and breaks there.
    internal float? ShieldBreaksAt { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // A starting nisi is only for the half that gets them: a support asking for one makes it the supports'.
    public PerRoleSetting<NisiLetter> Nisi { get; set; } = new();
    public PerRoleSetting<NisiLetter> Judgment { get; set; } = new();
    public PerRoleSetting<bool> FirstLightning { get; set; } = new();
    public PerRoleSetting<bool> Enumeration { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        var nisiSeats = PerRole.All.Where(r => Nisi[r] is not null).ToList();
        if (nisiSeats.Any(r => r.IsDps()) && nisiSeats.Any(r => !r.IsDps()))
            conflicts.Add($"{SettingsConflicts.Seats(nisiSeats)} asked for starting nisis, but they go to the four supports or the four DPS, not both.");
        if (NisiOnDps is { } onDps && nisiSeats.Any(r => r.IsDps() != onDps))
            conflicts.Add($"{SettingsConflicts.Seats(nisiSeats.Where(r => r.IsDps() != onDps))} asked for a starting nisi their half doesn't get.");
        foreach (var letter in System.Enum.GetValues<NisiLetter>())
        {
            conflicts.AtMost(1, nisiSeats.Where(r => Nisi[r] == letter).ToList(), $"nisi {letter}");
            conflicts.AtMost(1, PerRole.All.Where(r => !r.IsDps() && Judgment[r] == letter).ToList(), $"a support's Final Judgment {letter}");
            conflicts.AtMost(1, PerRole.All.Where(r => r.IsDps() && Judgment[r] == letter).ToList(), $"a DPS's Final Judgment {letter}");
        }
        conflicts.AtMost(1, PerRole.All.Where(r => FirstLightning[r] == true).ToList(), "the first Compressed Lightning");
        conflicts.AtMost(2, PerRole.All.Where(r => Enumeration[r] == true).ToList(), "an Enumeration");
        var supportsAsking = PerRole.All.Where(r => !r.IsDps() && (FirstLightning[r] == true || Enumeration[r] == true)).ToList();
        if (supportsAsking.Count > 0)
            conflicts.Add($"{SettingsConflicts.Seats(supportsAsking)} asked for a DPS-only marker.");
        return conflicts;
    }
}
