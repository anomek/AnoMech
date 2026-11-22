using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Ucob;

public class UcobConstants
{
    public const byte Level = 70;

    public static class Geometry
    {
        // The edge moves between phases: earlier phases stop knockbacks at ~24.5.
        public const float P5ArenaRadius = 21.8f;
    }

    // "Aether Markers": A/B/C on the rim, D and 1-4 inside. Scenario-local == world here,
    // since the arena centre is the zone origin.
    public static IReadOnlyList<Waymark> AetherWaymarks =>
    [
        new(WaymarkSlot.A,     new Vector3( 20.986f, 0f, -11.020f)),
        new(WaymarkSlot.B,     new Vector3(  9.315f, 0f,  21.971f)),
        new(WaymarkSlot.C,     new Vector3(-19.999f, 0f,  11.772f)),
        new(WaymarkSlot.D,     new Vector3(      0f, 0f,   9.000f)),
        new(WaymarkSlot.One,   new Vector3(      0f, 0f,  -8.000f)),
        new(WaymarkSlot.Two,   new Vector3( -8.000f, 0f,   5.000f)),
        new(WaymarkSlot.Three, new Vector3(  8.000f, 0f,   5.000f)),
        new(WaymarkSlot.Four,  new Vector3(      0f, 0f,   0.000f)),
    ];

    // The arena's SharedGroup EObjs (UcobZone), by the layout each binds to; the pieces are only
    // bound, never played.
    public class ArenaLayoutId
    {
        public const uint DeathWall = 0x6C7D48;
        public const uint P5Arena = 0x6D0C52;
        public const uint Piece1 = 0x6C641C;
        public const uint Piece2 = 0x6C0E8A;
        public const uint Piece3 = 0x6C0D4C;
        public const uint Piece4 = 0x6C0D4B;
    }

    public class BNpcBaseId
    {
        public const uint BahamutPrime = 0x1FE8;
        // The fight's generic invisible caster (x42 in the instance): AOE source for
        // everything the boss doesn't cast from its own body.
    }

    public class BNpcNameId
    {
        public const uint BahamutPrime = 3210;
    }

    public class StatusId
    {
        public const ushort LohsDaih = 1437;
    }

    // Lohs Daih is a Transfiguration status: its param is the Transformation row the boss
    // takes on, not a stack count (param 1 turns him into a toad).
    public const int LohsDaihTransformation = 73;

    public class ActionId
    {
        public const uint AutoAttack = 9936;
        public const uint Exaflare = 9967;       // 0x26EF, boss-body cast, no AOE of its own
        public const uint ExaflareFirst = 9968;  // 0x26F0, helper, 4.0s cast, arrow omen, 6y circle
        public const uint ExaflareRest = 9969;   // 0x26F1, helper, instant, 6y circle
    }
}
