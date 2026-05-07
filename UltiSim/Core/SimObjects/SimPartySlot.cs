namespace UltiSim.Core.SimObjects;

// Abstract layer between SimCharacter and the two party-slot types: SimPlayer
// (the real local player) and SimPartyMember (a spawned NPC doppel, via SimNpc).
// Carries PartyRole so any code that holds a party-slot reference can identify
// which slot it is without down-casting to the concrete type.
//
// The setter is internal so only engine code (SimParty.SetSlot, SimPartyMember's
// constructor) can assign the role; scenario code observes it read-only.
public abstract class SimPartySlot : SimCharacter
{
    public PartyRole Role { get; internal set; }
}
