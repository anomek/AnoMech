using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AnoMech.Scenarios.Umad.P3LimitCut;

public static class UmadP3LimitCutConstants
{
    public static class ActionId
    {
        // Proximity hit centred on the farthest player at cast start; Chaos lands there ~1.5s later.
        public const uint UmbraSmash = 0xBB00U;
        // The wind knockback (facing rule in UmadP3LimitCutScenario.PushByWind).
        public const uint VacuumWave = 0xBB13U;
        // Each clone's appearance hit, ~27k on a non-tank.
        public const uint UltimaBlaster = 0xBAE3U;
        // The numbered charge, rect 100 long x 6 wide.
        public const uint UltimaBlasterCharge = 0xBAE4U;
        // Never actually cast: the 40y push itself carries a wrong-facing player off the arena.
        public const uint StrayGusts = 0xBAF7U;
        // The sides come back 38.5s after the Umbra cast, having been stripped 44.3s before it.
        public const uint DecisiveBattleChaos = 0xC2E2U;
        public const uint DecisiveBattleExdeath = 0xC2E3U;
        // Kefka's visual beat 1.2s before Umbra Smash, with his trance status (param 0x22B).
        public const uint RingOfFire = 0x8E34U;
    }

    // Header.AnimationLock of each real ActionEffect. The engine default of 0.6s cut the clones'
    // 1.8s materialise short and left them frozen white and translucent.
    public static class AnimationLock
    {
        public const float UmbraSmash = 4.1f;
        public const float VacuumWave = 3.1f;
        public const float CloneAppear = 1.8f;
        public const float CloneCharge = 1.8f;
        public const float Cyclone = 1.1f;
        public const float ThunderCast = 3.1f;
        public const float ThunderHit = 1.1f;
        public const float DecisiveBattle = 3.1f;
        public const float AutoAttack = 0.1f;   // 18 of 22 autos in the window; the rest 0.29-3.17
        public const float Aetherlink = 3.1f;
        public const float RingOfFire = 0.6f;
    }

    public static class StatusId
    {
        public const ushort Headwind = 0x642;               // 1602, 68.00s from Bowels of Agony
        public const ushort Tailwind = 0x643;               // 1603, 68.00s
        public const ushort WindResistanceDownII = 0x41C;   // 1052, 0.96s per Cyclone hit
        // Kefka's P3 trance aura: param 0x1FF after Trance, 0x22B after Ring of Fire.
        public const ushort KefkaTrance = 0x8E1;
    }

    public static class BNpcBaseId
    {
        // BossMod's CloneP3: spawned at the arena centre ~86s before Umbra Smash in animation
        // state (0,1), the "hide" set whose idle shows nothing.
        public const uint KefkaClone = 19451;
    }

    public static class EObjId
    {
        public const uint FireCrystal = 2015290;
        public const uint WaterCrystal = 2015291;
        public const uint WindCrystal = 2015292;
    }

    // sph_lockon2_num01-08, given to all eight players at once as the sixth clone appears.
    public static readonly uint[] BlasterLockons = [336, 337, 338, 339, 437, 438, 439, 440];

    public static class Geometry
    {
        public const float ArenaRadius = 20f;
        // The cardinals and intercardinals at exactly 20y, in every pull.
        public const float SpotRadius = 20f;
        // 17.7-19.1y out in every clean pull: as far from the clone as the arena allows.
        public const float ChargeStandRadius = 19f;
        // From the EObj spawn packets; the cyclone helpers sit on the wind crystal.
        public static readonly Vector3 FireCrystal = new(-9.9f, 0f, -9.9f);
        public static readonly Vector3 WaterCrystal = new(9.9f, 0f, 9.9f);
        public static readonly Vector3 WindCrystal = new(9.9f, 0f, -9.9f);
        public const float FireCrystalRotation = 0.785f;
        public const float WaterCrystalRotation = -2.356f;
        public const float WindCrystalRotation = -0.785f;
        // (100,90) world, facing south.
        public static readonly Vector3 KefkaPerch = new(0f, 0f, -10f);
        // Every pull: Chaos held at an intercardinal ~9y out with Exdeath beside him, the party
        // ~5.5y out on the same diagonal, the bait at the opposite intercardinal.
        public const float ChaosHoldRadius = 9f;
        public const float ExdeathHoldRadius = 10.6f;
        public const float StackRadius = 5.5f;
        // Where the eight stood at every real cyclone: 4.3y past the centre away from Exdeath,
        // within 2.3y of one another.
        public const float CycloneStackRadius = 4.3f;
        public const float BaitRadius = 17.7f;
        // BossMod's ProximityAOEs(UmbraSmash, 20); the one real hit inside it was 312k at 17y.
        public const float UmbraLethalRadius = 20f;
        // Within 45 deg of the wind's direction is the 10y push (BossMod's cone; real players sat
        // 2.5 deg off at the median).
        public const float CorrectFacingCos = 0.7071f;

        // Spot index 0..7 -> heading (0 = S, 2 = E, 4 = N, 6 = W; FFXIV's atan2(dx, dz)).
        public static float SpotHeading(int spot) => spot * (MathF.PI / 4f);
        public static Vector3 OnCircle(float heading, float radius) => new(MathF.Sin(heading) * radius, 0f, MathF.Cos(heading) * radius);
        public static Vector3 SpotPosition(int spot) => OnCircle(SpotHeading(spot), SpotRadius);
        public static string SpotName(int spot) => SpotNames[((spot % 8) + 8) % 8];
        private static readonly string[] SpotNames = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"];
    }

    // Shown hits are fractions of max HP (a non-tank's 216k in the logs, ~30% party mitigation baked in).
    public static class Damage
    {
        public const float CloneAppear = 0.13f;
        // A second cyclone lands on the first's Wind Resistance Down II; the tank LB3's 80% is enough to live through it.
        public const float CycloneVulnMitigation = 0.80f;
        public const float CycloneSoloTankMitigation = 0.80f;
        // The charge falls off with distance from 20x max HP at the clone; an unmitigated
        // non-tank dies inside ~35y.
        public const float ChargeLethalRange = 35f;
        // A second hit inside it landed at x9-10 in all seven real cases.
        public const float MagicVulnerabilityUpSeconds = 2.96f;
        // Black Hole's buster: two hits 3.0s apart on whoever is closest to Exdeath, 929k raw
        // each against a 325k tank, the second unsurvivable short of an invuln while Lightning
        // Resistance Down II is still up.
        public const float ThunderIIIRequiredMitigation = 0.60f;
        public const float LightningResistanceDownSeconds = 3.96f;
        public const float WindResistanceDownSeconds = 0.96f;
    }

    // The real track starts 116.67s before the Umbra Smash cast.
    public const float BgmSecondsAtStart = 108.67f;

    // Offsets from the Umbra Smash cast start. Scenario time = these + UmbraCastAt.
    public static class Timing
    {
        public const float UmbraCastAt = 8.0f;
        public const float UmbraShownCast = 4.7f;
        public const float UmbraResolveAfterCast = 4.99f;
        public const float ChaosLandsAfterCast = 6.55f;
        public const float VacuumCastAt = UmbraCastAt + 0.128f;
        public const float VacuumShownCast = 7.7f;
        public const float VacuumResolveAfterUmbra = 8.10f;
        // The pushes land 0.80s after the wave fires, one player every 0.045s.
        public const float VacuumApplyDelay = 0.80f;
        public const float VacuumApplyStagger = 0.045f;
        public const float IconsAfterUmbra = 10.83f;
        public const float CyclonesAfterUmbra = 11.967f;
        public const float AetherlinkAfterUmbra = 13.379f;
        // The press; its status lands 1.34s later.
        public const float TankLimitBreakAfterUmbra = 5.6f;
        // Clone k is teleported to its spot, then fires its appearance ~0.1s later, ~2.0s apart.
        public static readonly float[] PlacementAfterUmbra = [0.803f, 2.808f, 4.815f, 6.821f, 8.830f, 10.836f, 12.840f, 14.846f];
        public static readonly float[] AppearAfterUmbra = [0.892f, 2.898f, 4.904f, 6.910f, 8.919f, 10.925f, 12.929f, 14.933f];
        // Charge k: teleport to the charge spot facing its number, then the rect ~0.1s later.
        public static readonly float[] ChargeSetPosAfterUmbra = [22.853f, 23.075f, 23.298f, 23.521f, 23.745f, 23.968f, 24.191f, 24.415f];
        public static readonly float[] ChargeAfterUmbra = [22.953f, 23.175f, 23.398f, 23.621f, 23.845f, 24.068f, 24.291f, 24.515f];
        // Cast 1.0s after the last charge, 4.7s bar, hits 5.08s and 8.11s after the cast start.
        public const float ThunderCastAfterUmbra = 25.49f;
        public const float ThunderShownCast = 4.7f;
        public const float ThunderHit1AfterUmbra = 30.57f;
        public const float ThunderHit2AfterUmbra = 33.60f;
        // Both bosses, 2.7s bars, sides re-applied 2.99s after the cast start.
        public const float DecisiveBattleCastAfterUmbra = 35.50f;
        public const float DecisiveBattleShownCast = 2.7f;
        public const float DecisiveBattleResolveAfterUmbra = 38.49f;
        // Kefka's reappearance beat; his "Max" cast, the next mechanic, follows 2.1s later.
        public const float KefkaReappearAfterUmbra = 42.581f;
        // Real auto-attack instants; both bosses stop swinging for their own casts.
        public static readonly float[] ChaosAutoAfterUmbra = [-4.096f, -1.072f, 10.066f, 13.095f, 18.162f, 21.189f, 24.214f, 29.285f, 32.311f, 35.335f, 41.381f, 44.407f];
        public static readonly float[] ExdeathAutoAfterUmbra = [-3.741f, -0.716f, 12.379f, 18.473f, 21.501f, 24.525f, 33.603f, 39.647f, 42.671f];
        // The instance director's own progression beats inside the window.
        public static readonly (float Offset, uint Arg)[] DirectorBeats = [(-6.012f, 0x18U), (0.981f, 0x2FU), (23.059f, 0x19U), (44.629f, 0x1AU)];
        public const uint DirectorCategory = 0x80000027U;
        public const uint DirectorArg3 = 0x1BDBU;
        public const uint DirectorKefkaId = 0x4000EFB9U;
        // Landed at Bowels of Agony 56.4s before Umbra Smash with 68.00s: 19.6s left at scenario
        // start, out 0.4s before the cyclones.
        public const float WindRemainingAtStart = 68.0f - 56.4f + UmbraCastAt;
        // ~40y/s for both the 10y and the 40y push (the log owner's client-exact positions).
        public const float KnockbackSpeed = 40f;
    }
}
