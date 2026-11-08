using System.Collections.Concurrent;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// A button on a tank's bar that puts mitigation on the tank who presses it or on the party around
// them: what a press leaves on the presser (Grants) and on another party member near them
// (PartyGrants), and what the game's client checks before it lets one through (recast group and
// charges, level, a gauge cost, the limit break gauge), none of which the sim enforces itself.
internal sealed record TankButton(uint ActionId, string Name, byte Level, byte CooldownGroup, float Recast, int Charges,
    IReadOnlyList<(ushort StatusId, float Duration)> Grants, IReadOnlyList<(ushort StatusId, float Duration)> PartyGrants,
    bool AimsAtAnother, uint GaugeCost, bool IsLimitBreak)
{
    public bool IsInvuln => Grants.Any(g => Mitigation.ByStatusId[g.StatusId].Damage >= 1f);

    public override string ToString() => Name;
}

// Each tank job's buttons at a duty's level, from the Action sheet and from the statuses a real
// JobActions press leaves on the presser in a scratch run, so a button the sim learns, or a status
// Mitigation starts counting, is in the kit without a list here.
internal static class TankKit
{
    public static readonly JobId[] Jobs = [JobId.Paladin, JobId.Warrior, JobId.DarkKnight, JobId.Gunbreaker];

    // The tank role actions' ClassJobCategory (Rampart, Reprisal, Provoke, ...).
    private const uint TankRoleActions = 113;
    // Oath: the one job gauge a tank's mitigation costs. The sim seeds Paladin's full at the start
    // (JobActions.StartingResources) and the test runs no auto-attacks to refill it.
    private const byte OathCostType = 41;
    public const uint StartingOath = 100;

    private static readonly ConcurrentDictionary<(JobId, byte), IReadOnlyList<TankButton>> Kits = new();

    public static IReadOnlyList<TankButton> Of(JobId job, byte level) => Kits.GetOrAdd((job, level), _ => Build(job, level));

    private static IReadOnlyList<TankButton> Build(JobId job, byte level)
    {
        var parent = DataminingGameData.ClassJobParent((uint)job);
        var candidates = DataminingGameData.ActionIds
            .Select(DataminingGameData.Use)
            .OfType<DataminingGameData.ActionUse>()
            .Where(use => !use.IsPvP && use.Level <= level && JobActions.Knows(use.Id)
                          && (use.ClassJob == (int)job || use.ClassJob == (int)parent
                              || (use.IsRoleAction && use.ClassJobCategory == TankRoleActions)))
            .ToList();
        var buttons = new List<TankButton>();
        Probe(game =>
        {
            if (Natives.Data.ClassJob((uint)job) is { LimitBreak3: not 0 } row && DataminingGameData.Use(row.LimitBreak3) is { } limitBreak)
                candidates.Add(limitBreak);
            foreach (var use in candidates)
                if (Press(game, use) is { } button) buttons.Add(button);
        });
        // A button the level upgrades (Sentinel to Guardian) is one button: same recast, same statuses.
        return buttons
            .GroupBy(b => (b.CooldownGroup, b.IsLimitBreak, Statuses: string.Join(",", b.Grants.Select(g => g.StatusId).Order()),
                PartyStatuses: string.Join(",", b.PartyGrants.Select(g => g.StatusId).Order())))
            .Select(g => g.MaxBy(b => b.Level)!)
            .OrderBy(b => b.ActionId)
            .ToList();
    }

    // What one press leaves on the presser and on the party member nearest them, neither aimed at:
    // the statuses Mitigation counts, with their durations. A bot presses it, as SimPartyNpc does:
    // an action's gauge and combo effects are the local player's alone and reach for the game's
    // own memory.
    private static TankButton? Press(AnoMech.Core.Game.Game game, DataminingGameData.ActionUse use)
    {
        var party = game.World.Party;
        var presser = party.Get(Presser)!;
        if (Natives.Data.Action(use.Id) is not { } row) return null;
        var aimsAtAnother = !row.CanTargetSelf && row.CanTargetParty;
        var target = aimsAtAnother ? game.Player! : presser;
        var nearby = party.ActiveMembers()
            .Where(m => !ReferenceEquals(m, presser) && !ReferenceEquals(m, target))
            .MinBy(m => Vector3.Distance(m.Position, presser.Position))!;
        ClearStatuses(game);
        JobActions.ApplyEffects(presser, use.Id, (ulong)target.GameObjectId, new Random(0));
        var grants = Counted(presser);
        var partyGrants = Counted(nearby);
        ClearStatuses(game);
        if (grants.Count == 0 && partyGrants.Count == 0) return null;
        var isLimitBreak = LimitBreakHandler.IsLimitBreak(use.Id);
        return new TankButton(use.Id, row.Name, use.Level, use.CooldownGroup, isLimitBreak ? 0f : use.RecastSeconds, use.MaxCharges,
            grants, partyGrants, aimsAtAnother, use.PrimaryCostType == OathCostType ? use.PrimaryCostValue : 0, isLimitBreak);
    }

    private static List<(ushort StatusId, float Duration)> Counted(AnoMech.Core.SimObjects.SimCharacter member)
        => member.ActiveStatusSnapshot
            .Where(s => Mitigation.ByStatusId.ContainsKey(s.StatusId))
            .Select(s => (s.StatusId, s.RemainingTime))
            .ToList();

    private static void ClearStatuses(AnoMech.Core.Game.Game game)
    {
        foreach (var (_, member) in game.World.Party.FilledSlots())
            foreach (var status in member.ActiveStatusSnapshot)
                member.RemoveStatus(status.StatusId);
    }

    private const PartyRole Presser = PartyRole.OffTank;

    // A scratch run's first frame, with the party seated and a bot in the presser's seat.
    private static void Probe(Action<AnoMech.Core.Game.Game> press)
    {
        var done = false;
        var scenario = ScenarioCatalog.Create().Scenarios.First(s => s.SupportsMultiplayer).GetType();
        var run = ScenarioRun.Execute(scenario, 0, 1, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.MainTank,
            StopAt = 0.5f,
            WriteArtifactsOnFailure = false,
            Probe = p =>
            {
                if (done || p.Game.Player is null) return;
                done = true;
                press(p.Game);
            },
        });
        if (!done || run.Failure is not null)
            throw new InvalidOperationException($"The kit probe never pressed anything: {run}");
    }
}
