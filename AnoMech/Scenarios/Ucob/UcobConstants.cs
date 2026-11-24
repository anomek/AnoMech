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
        // Kill radius, not the visible edge: players can stand against the edge at 21-21.3y, and
        // only knockbacks carry anyone past it.
        public const float P4ArenaRadius = 22f;
        public const float NaelHitboxRadius = 2.55f;
        public const float TwintaniaHitboxRadius = 3.96f;
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
        public const uint Twintania = 0x1FDF;
        public const uint Oviform = 0x1FE0;
        public const uint NaelDeusDarnus = 0x1FE1;
        public const uint BahamutPrime = 0x1FE8;
        // The fight's generic invisible caster (x42 in the instance): AOE source for
        // everything the boss doesn't cast from its own body.
    }

    public class BNpcNameId
    {
        public const uint Twintania = 1482;
        public const uint Oviform = 2210;
        public const uint NaelDeusDarnus = 2612;
        public const uint BahamutPrime = 3210;
    }

    public class StatusId
    {
        public const ushort Burns = 250;
        public const ushort DamageUp = 290;
        // Held by anyone standing in a link; a Hatch that lands on a target without it is what
        // triggers Deep Hatch.
        public const ushort Neurolink = 344;
        public const ushort SlashingResistanceDownII = 1272;
        public const ushort ManaHypersensitivity = 1434;
        public const ushort PiercingResistanceDownII = 1435;
        public const ushort LohsDaih = 1437;
    }

    public class LockonId
    {
        public const uint Hatch = 0x76;
    }

    public class TetherId
    {
        public const ushort GuardingBahamut = 0x43;
    }

    public class KnockbackId
    {
        public const uint TwisterBurst = 0x6F;
    }

    public class ActionTimelineId
    {
        public const ushort WarpEnd = 7747;
        public const ushort WarpEnd2 = 7748;
        public const ushort BahamutDivineJudgmentPose = 3216;
    }

    // Server-spawned event objects. Each flips to state 1 right after spawning and back to 0
    // just before it is removed.
    public class EObjId
    {
        public const uint LiquidHellPuddle = 2001150;
        public const uint Neurolink = 2001151;
        public const uint Twister = 2001168;
    }

    // Lohs Daih is a Transfiguration status: its param is the Transformation row the boss
    // takes on, not a stack count (param 1 turns him into a toad).
    public const int LohsDaihTransformation = 73;

    public class ActionId
    {
        public const uint TwintaniaAttack = 0x26A7;
        public const uint Plummet = 0x26A8;
        public const uint DeathSentence = 0x26A9;
        public const uint Twister = 0x26AA;
        public const uint TwisterBurst = 0x26AB;
        public const uint LiquidHell = 0x26AD;
        public const uint Generate = 0x26AE;
        public const uint Hatch = 0x26AF;
        public const uint DeepHatch = 0x26B0;
        public const uint NaelAttack = 0x26B4;
        public const uint BahamutsClaw = 0x26B5;
        public const uint Ravensbeak = 0x26B6;
        public const uint Megaflare = 0x26BA;
        public const uint IronChariot = 0x26BB;
        public const uint LunarDynamo = 0x26BC;
        public const uint ThermionicBeam = 0x26BD;
        public const uint RavenDive = 0x26BE;
        public const uint BahamutsFavor = 0x26E8;
        // The tanks' role action, pressed by a human tank to take an add.
        public const uint Provoke = 7533;
        public const uint AutoAttack = 9936;
        public const uint Exaflare = 9967;       // 0x26EF, boss-body cast, no AOE of its own
        public const uint ExaflareFirst = 9968;  // 0x26F0, helper, 4.0s cast, arrow omen, 6y circle
        public const uint ExaflareRest = 9969;   // 0x26F1, helper, instant, 6y circle
    }
}
