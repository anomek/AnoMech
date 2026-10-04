using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios;

// A scenario that keeps the party's one limit break gauge, as the real fight does: the host's
// gauge is every peer's, and each human's limit break reaches the host as it lands.
public interface IPartyLimitBreakScenario
{
    void OnPartyLimitBreak(PartyRole role, uint actionId, LimitBreakAim aim);
}
