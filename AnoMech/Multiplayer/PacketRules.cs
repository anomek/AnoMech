using System.Collections.Generic;
using static AnoMech.Multiplayer.Delivery;

namespace AnoMech.Multiplayer;

// Who the server shows a control to.
internal enum Delivery
{
    Everyone,
    // ActorControlSelf: that seat's own client alone.
    SeatOwner,
    // ActorControlSelf every client gets on its own player.
    EachOwnPlayer,
    // A player's own movement, which reaches the others as its poses; a bot's reaches everyone.
    Movement,
    // Each client makes its own for the seats it spawns.
    ZoneIn,
}

// ActorArg: the argument carrying an actor's entity id. OwnPlayer: a peer's own sim still sends it
// to its own player (its own presses, movement, forced moves and falls); everything else there is
// the host's.
internal sealed record ControlRule(Delivery Delivery, int ActorArg = -1, bool OwnPlayer = false);

// Every ActorControl, ActorControlTarget and director update category the sim sends: the host
// relays only these, and a peer takes only these from it. PacketRulesTests fails on one the sim
// sends or ActorControl names without a rule here.
internal static class PacketRules
{
    public static readonly IReadOnlyDictionary<uint, ControlRule> Controls = new Dictionary<uint, ControlRule>
    {
        [0x00] = new(Everyone),                                 // weapon
        [0x01] = new(SeatOwner),                                // auto-attack
        [0x02] = new(Everyone),                                 // mode
        [0x04] = new(Everyone),                                 // combat
        [0x0E] = new(Everyone),                                 // death animation
        [0x0F] = new(Everyone, OwnPlayer: true),                // cancel cast
        [0x14] = new(Everyone, OwnPlayer: true),                // gain effect
        [0x15] = new(Everyone, ActorArg: 2, OwnPlayer: true),   // lose effect
        [0x22] = new(Everyone, ActorArg: 1),                    // head marker
        [0x23] = new(Everyone, ActorArg: 2),                    // tether
        [0x24] = new(Everyone),                                 // pop-in
        [0x27] = new(Everyone),                                 // corpse fade
        [0x2F] = new(Everyone),                                 // clear tether
        [0x30] = new(Everyone),                                 // activate
        [0x31] = new(Everyone),                                 // mode attribute flags
        [0x36] = new(Everyone),                                 // targetable
        [0x3C] = new(Everyone),                                 // wipe reset
        [0x3E] = new(Everyone),                                 // animation state
        [0x3F] = new(Everyone),                                 // model state
        [0x46] = new(Everyone),                                 // voice line
        [0x47] = new(Everyone, OwnPlayer: true),                // limit break cast
        [0x48] = new(Everyone, OwnPlayer: true),                // limit break resolve
        [0x50] = new(Everyone),                                 // wall death
        [0x52] = new(Everyone),                                 // confusion
        [0x62] = new(Everyone),                                 // helper name
        [0x6A] = new(Everyone),                                 // director EObj mod
        [0xB7] = new(EachOwnPlayer),                            // ground VFX
        [0xC8] = new(ZoneIn, OwnPlayer: true),                  // zone-in
        [0xDC] = new(Movement, OwnPlayer: true),                // gimmick jump
        [0xDF] = new(Movement, OwnPlayer: true),                // slide
        [0xE1] = new(Movement, OwnPlayer: true),                // push
        [0xED] = new(SeatOwner, OwnPlayer: true),               // fell out of the arena
        [0xF1] = new(Movement, OwnPlayer: true),                // warp
        [0x107] = new(SeatOwner, OwnPlayer: true),              // fall state
        [0x197] = new(Everyone),                                // action timeline
        [0x199] = new(Everyone),                                // EObj set state
        [0x19D] = new(Everyone),                                // EObj animation beat
        [0x1A1] = new(Everyone, ActorArg: 0),                   // helper owner
        [0x1F9] = new(EachOwnPlayer),                           // limit break gauge
        [0x25F] = new(Everyone, ActorArg: 0),                   // fade out
        [0x5F9] = new(Everyone),                                // revive
        [0x5FB] = new(Everyone),                                // transform
        [0x602] = new(Movement, OwnPlayer: true),               // forced march step
        [0x603] = new(Movement, OwnPlayer: true),               // forced march end
    };

    public static readonly IReadOnlyDictionary<uint, ControlRule> TargetControls = new Dictionary<uint, ControlRule>
    {
        [0x32] = new(Everyone),                                 // set target
        [0x1F6] = new(EachOwnPlayer, ActorArg: 1),              // sign
    };

    // ProcessDirectorUpdate is where the server's own instance-state packets land, so only these
    // are taken from the host.
    public static readonly IReadOnlySet<uint> DirectorCategories = new HashSet<uint> { 0x80000004, 0x8000000D, 0x80000027, 0x80000028 };
}
