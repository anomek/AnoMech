using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

// User-forced rolls for DsrP3NidhoggState; null leaves a field randomized.
public sealed class DsrP3NidhoggStateOverrides
{
    // Index into DsrP3NidhoggState.ArrowPatterns.
    public int? ArrowPattern { get; set; }
    public bool? FirstOutFirst { get; set; }
    public bool? SecondOutFirst { get; set; }
    // Index into DsrP3NidhoggState.CounterTowerShapes.
    public int? CounterTowerShape { get; set; }

    // Dive from Grace number, 1 to 3.
    public PerRoleSetting<int> Order { get; set; } = new();
    public PerRoleSetting<JumpKind> Jump { get; set; } = new();

    // Unlike most panels this checks solo too: one seat's jump can contradict the forced arrows.
    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (PerRole.SeatsActive)
            foreach (var order in new[] { 1, 2, 3 })
                conflicts.AtMost(DsrP3NidhoggState.OrderSize(order), PerRole.All.Where(r => Order[r] == order).ToList(), $"number {order}");
        if (!conflicts.Any && !DsrP3NidhoggState.CanDeal(this, PartyRole.MainTank))
            conflicts.Add(ArrowPattern == null
                ? "no Dive from Grace roll gives every pick its number and jump."
                : "the forced arrows can't give every pick its number and jump.");
        return conflicts;
    }
}
