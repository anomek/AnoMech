using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios;

// Non-generic surface so ScenarioSettingsSummary and ScenarioSettingsSync can handle any
// per-player setting without knowing its value type.
public interface IPerRoleSetting
{
    // One line for the lobby summary, or null when nothing is forced.
    string? Describe();

    // Drops values this build has no member for; a settings blob arrives from the host.
    void Sanitize();

    void Clear();
}

// Whether the eight seats are real right now. They are only meaningful while this client hosts
// a session: solo has one player whose seat follows their job and isn't known until the run
// starts, and a peer never builds scenario state at all.
public static class PerRole
{
    public static bool SeatsActive => ForceSeats || Plugin.MultiplayerInstance is { IsHost: true, SessionCode: not null };

    // Lets headless runs set every seat's overrides without hosting a session.
    internal static bool ForceSeats { get; set; }

    public static readonly PartyRole[] All =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];
}

// A setting every player has their own copy of -- your number, your wind, your tether -- as
// opposed to one roll the whole sim shares (which clone starts, which way they walk). The two
// kinds used to be conflated: a single value plus an "applies to" seat picker, so forcing one
// player's number silently unforced everyone else's.
//
// Solo and hosting are stored apart, so moving between them doesn't overwrite either.
public sealed class PerRoleSetting<T> : IPerRoleSetting where T : struct
{
    // The local player's own value, used when not hosting.
    public T? Mine { get; set; }

    // Indexed by PartyRole; null = that seat is left to the fight's own roll.
    public T?[] Seats { get; set; } = new T?[8];

    public T? this[PartyRole role]
    {
        get => (int)role >= 0 && (int)role < Seats.Length ? Seats[(int)role] : null;
        set { if ((int)role >= 0 && (int)role < Seats.Length) Seats[(int)role] = value; }
    }

    public bool AnySet => Mine.HasValue || Seats.Any(v => v.HasValue);

    public void Clear()
    {
        Mine = null;
        Seats = new T?[8];
    }

    // What the run applies, in seat order. Callers resolve conflicts themselves (two people
    // cannot both be number 3) and should say so in the log rather than silently dropping one.
    public IReadOnlyList<(PartyRole Role, T Value)> Resolve(PartyRole localPlayerRole)
    {
        if (!PerRole.SeatsActive)
            return Mine is { } mine ? [(localPlayerRole, mine)] : [];
        var result = new List<(PartyRole, T)>(8);
        for (var i = 0; i < 8 && i < Seats.Length; i++)
            if (Seats[i] is { } value) result.Add(((PartyRole)i, value));
        return result;
    }

    // The value for one seat as the run will see it, for a UI that wants to grey out a
    // dependent row.
    public T? Effective(PartyRole role) => PerRole.SeatsActive ? this[role] : Mine;

    public void Set(PartyRole role, T? value)
    {
        if (PerRole.SeatsActive) this[role] = value;
        else Mine = value;
    }

    public string? Describe()
    {
        if (!AnySet) return null;
        if (!PerRole.SeatsActive)
            return Mine is { } mine ? ScenarioSettingsSummary.FormatValue(mine) : null;
        var parts = SettingsGrid.Seats
            .Where(r => this[r].HasValue)
            .Select(r => $"{SettingsGrid.RoleLabel(r)} {ScenarioSettingsSummary.FormatValue(this[r]!.Value)}")
            .ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public void Sanitize()
    {
        if (Seats.Length != 8)
            Seats = Seats.Concat(new T?[8]).Take(8).ToArray();
        if (!typeof(T).IsEnum) return;
        if (Mine is { } mine && !Enum.IsDefined(typeof(T), mine)) Mine = null;
        for (var i = 0; i < 8; i++)
            if (Seats[i] is { } value && !Enum.IsDefined(typeof(T), value)) Seats[i] = null;
    }
}
