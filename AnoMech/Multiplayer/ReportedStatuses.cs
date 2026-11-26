using System;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.UserActions;

namespace AnoMech.Multiplayer;

// A peer runs no scenario logic: the host decides who lives, from the statuses on its own copy
// of each character. Of the statuses a peer's press puts on a party member, only the ones the
// host acts on (mitigation, knockback immunity) and Sprint, which every other client shows, are
// sent to it.
internal static class ReportedStatuses
{
    public static bool Contains(ushort statusId)
        => Mitigation.ByStatusId.ContainsKey(statusId) || Array.IndexOf(EnemyActionContext.KnockbackImmunityStatuses, statusId) >= 0 || statusId == JobActions.SprintStatus;
}
