namespace AnoMech.Scenarios.Fru;

// All IDs and tunables for Futures Rewritten (Ultimate), in one flat class.
// One name per value. Values in decimal.
public static class FruConstants
{
    public const byte Level = 100;

    public static class WeatherId
    {
        // n4gw's combat background uses bg6_1; weather 1 enables the victory scene.
        public const byte Pandora = 108;
    }

    public static class BgmId
    {
        // Return to Oblivion (Scions & Sinners: Band), BGM_EX5_Ban_11.scd.
        public const ushort Pandora = 20099;
    }

    public static class MapEffect
    {
        // ContentDirectorManagedSG row 181: slots 0..53 include the earlier
        // platforms, ice, hourglasses, and mechanic VFX.
        public const byte SlotCount = 54;
        public const byte PandoraArena = 47; // sgbg_n4gw_a6_gmc01.sgb
        public const uint Show = 0x00010001;
        public const uint Hide = 0x00040004;
    }

    public static class BNpcBaseId
    {
        public const uint Pandora = 17839; // 0x45AF — P5 boss
        public const uint Helper = 9020; // 0x233C — canonical FRU helper
    }

    public static class BNpcNameId
    {
        public const uint Pandora = 13561;
    }

    public static class ActionId
    {
        public const uint PathOfDarknessFirst = 40118;
        public const uint FulgentBlade = 40306;
        public const uint PathOfLightFirst = 40307;
        public const uint PathOfLightRest = 40308;
        public const uint PathOfDarknessRest = 40309;
        public const uint AkhMornPandora = 40310;
        public const uint AkhMornPandoraAoe1 = 40311;
        public const uint AkhMornPandoraAoe2 = 40312;
    }

    public static class Vfx
    {
        // The line's native gold/purple particles retain their authored textures,
        // brightness and blending; no screen-space approximation or RGBA tint.
        // This asset has no default timeline: trigger 1 starts the persistent seam.
        public const string InitialSeam = "bg/ex3/01_nvt_n4/common/vfx/eff/b3560omn01_y1.avfx";
        public const uint InitialSeamTrigger = 1;
        // Timeline 3 runs the line's charging stages (TRG 5/6/7 clips at
        // authored frames 0/59/119), including their native gold/purple colors.
        public const uint SeamChargeTrigger = 3;

        // EObj 0x1EBBF7 -> ExportedSG 38454 -> sgvf_n4gw_b3560.sgb,
        // VFX instance 2: opposing gold/purple bouncing arrows (mark115_o).
        // Scenery VFX, not an actor effect; authored at world scale, with no binder.
        public const string PathArrows = "bg/ex3/01_nvt_n4/common/vfx/eff/b3560omn02_y1.avfx";
    }

    public static class Geometry
    {
        public const float ArenaRadius = 20f;
        public const float AkhMornRadius = 4f;
        public const float ExalineHalfWidth = 40f;
        public const float ExalineStep = 5f;
        // FRU-Sim uses 11.855 units for the game's 5-yalm wave depth.
        public const float ReferenceScale = ExalineStep / 11.855f;
    }
}
