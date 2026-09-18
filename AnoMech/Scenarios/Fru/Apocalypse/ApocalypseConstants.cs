namespace AnoMech.Scenarios.Fru.Apocalypse;

internal static class ApocalypseConstants
{
    public const uint Oracle = 17831, OracleName = 9832;
    public const uint Refrain = 40269, WaterCast = 40270, Water = 40271;
    public const uint ApocCast = 40296, ApocHit = 40297;
    public const uint SpiritCast = 40288, SpiritHit = 40289;
    public const uint EruptionCast = 40273, Eruption = 40274;
    public const uint DanceCast = 40181, DanceHit = 40182, Knockback = 40183, Pulsar = 40282;
    public const ushort WaterStatus = 2461;
    public const float RunSpeed = 6, ApocRadius = 9, RingRadius = 14, WaterRadius = 6;
    public const float SpiritRadius = 5, EruptionRadius = 6, DanceRadius = 8, KnockbackDistance = 21;
    // EObj 0x1EB0FF -> ExportedSG 25372 -> sgvf_n4gc_b2164.sgb.
    // Native AVFX timelines: straight, right arc, left arc, ring pulse.
    public const string LightVfx = "bg/ex3/01_nvt_n4/common/vfx/eff/b2164kido1_o.avfx";
    public const uint StraightTrigger = 1, ClockwiseTrigger = 2, CounterclockwiseTrigger = 3, PulseTrigger = 4;
    // Actor-bound waiting clock: native timeline/emitter/binder lifetime is -1.
    // Own it until the countdown starts (or death/reset), unlike finite lockons.
    public const string WaterWaitingClock = "vfx/common/eff/d1049_stlp_b0k1.avfx";
    // All three lockons have finite native timelines. Spawn fire-and-forget:
    // the client frees their VfxData even when scenario time is paused. Tracking
    // them as persistent would call the destructor again on reset or resolution.
    // Stack marker before debuff application; countdown before each water hit.
    public const string WaterMarker = "vfx/lockon/eff/com_share0c.avfx";
    public const string WaterClock = "vfx/lockon/eff/m0581trg_dice0h.avfx";
    public const string EruptionMarker = "vfx/lockon/eff/target_ae_s5f.avfx";
}
