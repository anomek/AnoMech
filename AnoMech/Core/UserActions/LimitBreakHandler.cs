using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Sends the party the server's limit break ActorControls: one as the cast starts, one when it
// resolves, which empties the gauge. A cast interrupted before the slidecast window never
// resolves, so it costs nothing. What the limit break does is an ordinary JobActions row.
internal sealed class LimitBreakHandler : IUserActionHandler
{
    private const uint LimitBreakCategory = 9;

    public static bool IsLimitBreak(uint actionId)
        => Natives.Data.Action(actionId)?.ActionCategory == LimitBreakCategory;

    // level 1-3; 0 when the job has none at that level.
    public static uint ActionId(uint classJob, int level)
    {
        if (Natives.Data.ClassJob(classJob) is not { } job) return 0;
        return level switch
        {
            1 => job.LimitBreak1,
            2 => job.LimitBreak2,
            3 => job.LimitBreak3,
            _ => 0,
        };
    }

    private static void StartCast(SimParty party, SimCharacter user, uint actionId)
    {
        foreach (var member in party.AllMembers()) member.ActorControl.LimitBreakCast(actionId, member == user);
    }

    // The client's handler empties the HUD gauge; Spend only keeps FilledBars in step.
    internal static void Resolve(SimParty party, SimCharacter user, uint actionId)
    {
        var group = Group((user as ISimPartyMember)?.Role, party.LimitBreak.FilledBars);
        foreach (var member in party.AllMembers()) member.ActorControl.LimitBreakResolve(actionId, member == user, group);
        party.LimitBreak.Spend();
    }

    // The server's 0x48 p3. Only tank LB1-3, physical-DPS LB3 and magic LB3 were observed; the
    // non-tank LB1/LB2 values are UNVERIFIED.
    private static uint Group(PartyRole? role, int level) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => level == 1 ? 18u : 37u,
        PartyRole.RegenHealer or PartyRole.ShieldHealer or PartyRole.CasterDps => 80u,
        _ => 36u,
    };

    public void OnCastStart(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action || !IsLimitBreak(actionId)) return;
        if (Plugin.GameInstance?.World.Party is not { Player: { } player } party) return;
        StartCast(party, player, actionId);
    }

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action || !IsLimitBreak(actionId)) return;
        if (Plugin.GameInstance?.World.Party is not { Player: { } player } party) return;
        Resolve(party, player, actionId);
        DiagnosticLog.Info($"[LimitBreak] {ActionLookup.Name(actionId)} ({actionId}) resolved -- the gauge is spent.");
    }
}
