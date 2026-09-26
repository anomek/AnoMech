using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;

namespace AnoMech.SafetyTests.Harness;

// When loading a sim zone is safe, written from the game's point of view rather than copied from
// ZoneSession.StartBlockedReason: the harness judges every real load against this, so a gate that
// lets through anything here fails the tests. The plugin may be stricter; it may not be looser.
internal static class StartSpecification
{
    public const double ZoneSettleSeconds = 3;
    public const double ActionSettleSeconds = 3;

    // States the server may still act on after they end: an event's zone change, a queue pop, a
    // cast's effect, a pending world visit.
    public static readonly HashSet<ConditionFlag> ServerActingSoonFlags =
    [
        ConditionFlag.Casting, ConditionFlag.Casting87, ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51,
        ConditionFlag.Occupied, ConditionFlag.Occupied30, ConditionFlag.Occupied33, ConditionFlag.Occupied38,
        ConditionFlag.Occupied39, ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.OccupiedSummoningBell, ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78, ConditionFlag.TradeOpen, ConditionFlag.LoggingOut, ConditionFlag.SystemError,
        ConditionFlag.WaitingForDuty, ConditionFlag.WaitingForDutyFinder, ConditionFlag.InDutyQueue,
        ConditionFlag.ReadyingVisitOtherWorld, ConditionFlag.WaitingToVisitOtherWorld,
    ];

    // Every state in which the character is not freely standing in the inn: the saved position
    // or the server's next move could be wrong.
    public static readonly HashSet<ConditionFlag> BusyFlags =
    [
        .. ServerActingSoonFlags,
        ConditionFlag.Crafting, ConditionFlag.ExecutingCraftingAction, ConditionFlag.PreparingToCraft,
        ConditionFlag.MeldingMateria, ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction,
        ConditionFlag.Fishing, ConditionFlag.Unconscious, ConditionFlag.InCombat, ConditionFlag.BoundByDuty,
        ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95, ConditionFlag.DutyRecorderPlayback,
        ConditionFlag.InDeepDungeon, ConditionFlag.RegisteringForRaceOrMatch, ConditionFlag.WaitingForRaceOrMatch,
        ConditionFlag.RegisteringForTripleTriadMatch, ConditionFlag.WaitingForTripleTriadMatch,
        ConditionFlag.WaitingForTripleTriadMatch83, ConditionFlag.ChocoboRacing, ConditionFlag.PlayingMiniGame,
        ConditionFlag.PlayingLordOfVerminion, ConditionFlag.ParticipatingInCustomMatch, ConditionFlag.Performing,
        ConditionFlag.Mounted, ConditionFlag.Mounting, ConditionFlag.Mounting71, ConditionFlag.MountOrOrnamentTransition,
        ConditionFlag.MountImmobile, ConditionFlag.RidingPillion, ConditionFlag.InFlight, ConditionFlag.UsingChocoboTaxi,
        ConditionFlag.OperatingSiegeMachine, ConditionFlag.PilotingMech, ConditionFlag.CarryingObject,
        ConditionFlag.CarryingItem, ConditionFlag.BeingMoved, ConditionFlag.Swimming, ConditionFlag.Diving,
        ConditionFlag.Jumping, ConditionFlag.Jumping61,
    ];

    // Null when a fresh sim load is safe now.
    public static string? Violation(StartSpec s, bool previousLiftPending)
    {
        if (!s.LoggedIn) return "the client was not logged in";
        if (!s.PlayerPresent) return "there was no local player";
        if (!s.DalamudSaysInn) return "the character was not in an inn";
        if (!s.ClientMatchesServer) return "the client and the server disagreed about the territory";
        if (s.ZoneChangePending) return "a zone change was pending";
        if (s.Casting) return "the character was casting";
        if (s.Flags.Intersect(BusyFlags).ToList() is { Count: > 0 } busy) return $"the character was busy ({string.Join(", ", busy)})";
        if (s.SecondsSinceZoneIn < ZoneSettleSeconds) return $"the zone-in was {s.SecondsSinceZoneIn:F2}s old";
        if (s.SecondsSinceServerActing < ActionSettleSeconds) return $"a server-acting state ended {s.SecondsSinceServerActing:F2}s before";
        if (previousLiftPending) return "the previous stay's firewall lift was still pending";
        return null;
    }
}
