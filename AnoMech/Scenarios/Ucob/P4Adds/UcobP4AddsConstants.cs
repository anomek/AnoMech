using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Ucob.P4Adds;

public static class UcobP4AddsConstants
{
    public static class Geometry
    {
        // P1 drops a Neurolink under Twintania at each HP threshold, and the NA strat parks her on
        // waymarks 1/2/3 for them, so the links sit on the Aether Markers.
        public static readonly IReadOnlyList<Vector3> Neurolinks =
        [
            MarkerOffset(WaymarkSlot.One),
            MarkerOffset(WaymarkSlot.Two),
            MarkerOffset(WaymarkSlot.Three),
        ];

        // UNVERIFIED: not in any sheet; bracketed by where the Neurolink status comes and goes.
        public const float NeurolinkRadius = 2f;

        public static readonly Vector3 BahamutSpawn = new(0f, 0f, -24f);
        public static readonly Vector3 TwintaniaSpawn = new(-5.5f, 0f, -11f);
        public static readonly Vector3 NaelSpawn = new(5.5f, 0f, -11f);

        // Self-centred AOEs reach EffectRange past the caster's hitbox edge, so a 6y Iron
        // Chariot hits out to 8.55y from Nael's centre.
        public const float ChariotRadius = 6f + UcobConstants.Geometry.NaelHitboxRadius;
        // UNVERIFIED: Burns lands on players about 1-3.3y from a puddle's centre.
        public const float LiquidHellPuddleRadius = 3.5f;

        // Oviforms set off ~1.3s after Generate finishes, travel at ~4.7y/s, and detonate ~3y
        // short of their target.
        public const float HatchPopDistance = 3f;
        public const float OviformSpeed = 4.7f;

        // UNVERIFIED: the touch radius isn't in any sheet.
        public const float TwisterTriggerRadius = 1.5f;
        public const float TwisterKeepOutRadius = 2.4f;

        private static Vector3 MarkerOffset(WaymarkSlot slot) =>
            UcobConstants.AetherWaymarks.First(w => w.Slot == slot).Offset;
    }

    public const ushort EObjActiveState = 1;

    // Bahamut switches to this model state at the start of P4, just before his mon_sp016 pose.
    public const byte BahamutChargingModelState = 4;

    public static class Text
    {
        public const string NaelName = "Nael deus Darnus";
        public const string StandGuard = "O Bahamut! We shall stand guard as you make ready your divine judgment!";
    }
}
