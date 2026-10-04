using System.Collections.Generic;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

internal unsafe delegate int GaugeReader(JobGaugeManager* jgm);
internal unsafe delegate void GaugeWriter(JobGaugeManager* jgm, int value);

// A capped gauge on a job's gauge struct (GNB cartridges, WAR beast, …). Read/Write
// localize the per-job field — its type and offset differ per job. Add clamps into
// [0, Max]. Subclass for gauges that need custom logic (RDM unbalanced mana, dynamic Max).
internal unsafe class ResourceGauge
{
    public required GaugeReader Read;
    public required GaugeWriter Write;
    public required int Max;

    public virtual void Add(JobGaugeManager* jgm, int amount)
    {
        var value = Read(jgm) + amount;
        Write(jgm, value < 0 ? 0 : value > Max ? Max : value);
    }
}

// Per-action client-side effects that stand in for the firewalled server response.
// Each action maps to a list of composable IActionEffects (see ActionEffect.cs):
// Gauge writes (generation, or combo-step deltas — negative amounts clamp to 0), Status
// grants (Status = the caster, TargetStatus / EnemyStatus = whoever the action hits),
// wrapped by Combo(...) (combo-gated) or Random(...) (procs). A separate
// StatusClearedOnAction table removes statuses by action. See reference-resource-generation.
internal static unsafe class JobActions
{
    private static readonly ResourceGauge GnbCartridge = new()
    {
        Read = jgm => jgm->Gunbreaker.Ammo,
        Write = (jgm, v) => jgm->Gunbreaker.Ammo = (byte)v,
        Max = 3,
    };

    // Not a resource — the Gnashing Fang combo step (0→1→2), which drives the button swap
    // Gnashing Fang→Savage Claw→Wicked Talon via the game's GetAdjustedActionId. Clamped
    // [0,2], so a finish/break is just -2. (No 30s auto-expiry yet — deferred gauge-TTL.)
    private static readonly ResourceGauge GnbComboStep = new()
    {
        Read = jgm => jgm->Gunbreaker.AmmoComboStep,
        Write = (jgm, v) => jgm->Gunbreaker.AmmoComboStep = (byte)v,
        Max = 2,
    };

    // Same gauge-driven button-swap pattern as GnbComboStep for the other two route jobs:
    // GetAdjustedActionId reads the step byte, not am->Combo — confirmed by GNB, whose route
    // carries an identical sheet ActionCombo chain yet still needs the gauge to advance the swap.
    // Confiteor→Blade of Faith→Blade of Truth→Blade of Valor: step 0→1→2→3.
    private static readonly ResourceGauge PldConfiteorStep = new()
    {
        Read = jgm => jgm->Paladin.ConfiteorComboStep,
        Write = (jgm, v) => jgm->Paladin.ConfiteorComboStep = (byte)v,
        Max = 3,
    };

    // Scarlet Delirium→Comeuppance→Torcleaver (under Delirium): step 0→1→2. Field is ushort.
    private static readonly ResourceGauge DrkDeliriumStep = new()
    {
        Read = jgm => jgm->DarkKnight.DeliriumStep,
        Write = (jgm, v) => jgm->DarkKnight.DeliriumStep = (ushort)v,
        Max = 2,
    };

    private static readonly ResourceGauge WarBeast = new()
    {
        Read = jgm => jgm->Warrior.BeastGauge,
        Write = (jgm, v) => jgm->Warrior.BeastGauge = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge DrkBlood = new()
    {
        Read = jgm => jgm->DarkKnight.Blood,
        Write = (jgm, v) => jgm->DarkKnight.Blood = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge DrgFocus = new()
    {
        Read = jgm => jgm->Dragoon.FirstmindsFocusCount,
        Write = (jgm, v) => jgm->Dragoon.FirstmindsFocusCount = (byte)v,
        Max = 2,
    };

    private static readonly ResourceGauge NinNinki = new()
    {
        Read = jgm => jgm->Ninja.Ninki,
        Write = (jgm, v) => jgm->Ninja.Ninki = (byte)v,
        Max = 100,
    };

    // Second NIN gauge: Armor Crush stores 2 (cap 5), Aeolian Edge spends 1.
    private static readonly ResourceGauge NinKazematoi = new()
    {
        Read = jgm => jgm->Ninja.Kazematoi,
        Write = (jgm, v) => jgm->Ninja.Kazematoi = (byte)v,
        Max = 5,
    };

    private static readonly ResourceGauge RprSoul = new()
    {
        Read = jgm => jgm->Reaper.Soul,
        Write = (jgm, v) => jgm->Reaper.Soul = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge RprShroud = new()
    {
        Read = jgm => jgm->Reaper.Shroud,
        Write = (jgm, v) => jgm->Reaper.Shroud = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge SamKenki = new()
    {
        Read = jgm => jgm->Samurai.Kenki,
        Write = (jgm, v) => jgm->Samurai.Kenki = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge MchHeat = new()
    {
        Read = jgm => jgm->Machinist.Heat,
        Write = (jgm, v) => jgm->Machinist.Heat = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge MchBattery = new()
    {
        Read = jgm => jgm->Machinist.Battery,
        Write = (jgm, v) => jgm->Machinist.Battery = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge RdmWhite = new()
    {
        Read = jgm => jgm->RedMage.WhiteMana,
        Write = (jgm, v) => jgm->RedMage.WhiteMana = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge RdmBlack = new()
    {
        Read = jgm => jgm->RedMage.BlackMana,
        Write = (jgm, v) => jgm->RedMage.BlackMana = (byte)v,
        Max = 100,
    };

    // Filled +1 by each enchanted-melee hit; at 3 the game swaps Verthunder/Veraero(II/III)
    // → Verflare/Verholy (GetAdjustedActionId reads this), which then spend all 3.
    private static readonly ResourceGauge RdmManaStacks = new()
    {
        Read = jgm => jgm->RedMage.ManaStacks,
        Write = (jgm, v) => jgm->RedMage.ManaStacks = (byte)v,
        Max = 3,
    };

    // Aetherflow the action refills to full; Add(+3) clamped to Max=3 gives that.
    private static readonly ResourceGauge SchAetherflow = new()
    {
        Read = jgm => jgm->Scholar.Aetherflow,
        Write = (jgm, v) => jgm->Scholar.Aetherflow = (byte)v,
        Max = 3,
    };

    private static readonly ResourceGauge VprRattlingCoil = new()
    {
        Read = jgm => jgm->Viper.RattlingCoilStacks,
        Write = (jgm, v) => jgm->Viper.RattlingCoilStacks = (byte)v,
        Max = 3,
    };

    private static readonly ResourceGauge VprSerpentOffering = new()
    {
        Read = jgm => jgm->Viper.SerpentOffering,
        Write = (jgm, v) => jgm->Viper.SerpentOffering = (byte)v,
        Max = 100,
    };

    private static readonly ResourceGauge VprAnguineTribute = new()
    {
        Read = jgm => jgm->Viper.AnguineTribute,
        Write = (jgm, v) => jgm->Viper.AnguineTribute = (byte)v,
        Max = 5,
    };

    // Blood Lily; the healing Lily it's fed from fills passively (a timer, not modelled).
    private static readonly ResourceGauge WhmBloodLily = new()
    {
        Read = jgm => jgm->WhiteMage.BloodLily,
        Write = (jgm, v) => jgm->WhiteMage.BloodLily = (byte)v,
        Max = 3,
    };

    private static readonly ResourceGauge DncFeathers = new()
    {
        Read = jgm => jgm->Dancer.Feathers,
        Write = (jgm, v) => jgm->Dancer.Feathers = (byte)v,
        Max = 4,
    };

    private static readonly ResourceGauge WhmLily = new()
    {
        Read = jgm => jgm->WhiteMage.Lily,
        Write = (jgm, v) => jgm->WhiteMage.Lily = (byte)v,
        Max = 3,
    };

    private static readonly ResourceGauge SgeAddersgall = new()
    {
        Read = jgm => jgm->Sage.Addersgall,
        Write = (jgm, v) => jgm->Sage.Addersgall = (byte)v,
        Max = 3,
    };

    // Extra scalar gauges: some are spender-only (ApplyCost targets, no in-sim generation yet, so
    // cost no-ops at 0); DncEsprit / PctPalette / PctPaint / BlmPolyglot below now also generate.
    private static readonly ResourceGauge MnkChakra = new() { Read = jgm => jgm->Monk.Chakra, Write = (jgm, v) => jgm->Monk.Chakra = (byte)v, Max = 5 };
    private static readonly ResourceGauge DncEsprit = new() { Read = jgm => jgm->Dancer.Esprit, Write = (jgm, v) => jgm->Dancer.Esprit = (byte)v, Max = 100 };
    private static readonly ResourceGauge BrdSoulVoice = new() { Read = jgm => jgm->Bard.SoulVoice, Write = (jgm, v) => jgm->Bard.SoulVoice = (byte)v, Max = 100 };
    private static readonly ResourceGauge SamMeditation = new() { Read = jgm => jgm->Samurai.MeditationStacks, Write = (jgm, v) => jgm->Samurai.MeditationStacks = (byte)v, Max = 3 };
    private static readonly ResourceGauge RprLemureShroud = new() { Read = jgm => jgm->Reaper.LemureShroud, Write = (jgm, v) => jgm->Reaper.LemureShroud = (byte)v, Max = 5 };
    private static readonly ResourceGauge RprVoidShroud = new() { Read = jgm => jgm->Reaper.VoidShroud, Write = (jgm, v) => jgm->Reaper.VoidShroud = (byte)v, Max = 8 };
    private static readonly ResourceGauge SgeAddersting = new() { Read = jgm => jgm->Sage.Addersting, Write = (jgm, v) => jgm->Sage.Addersting = (byte)v, Max = 3 };
    // The client swaps in the Eukrasian spells off this gauge byte, not off the Eukrasia status.
    private static readonly ResourceGauge SgeEukrasia = new() { Read = jgm => jgm->Sage.Eukrasia, Write = (jgm, v) => jgm->Sage.Eukrasia = (byte)v, Max = 1 };
    private static readonly ResourceGauge PctPalette = new() { Read = jgm => jgm->Pictomancer.PalleteGauge, Write = (jgm, v) => jgm->Pictomancer.PalleteGauge = (byte)v, Max = 100 };
    private static readonly ResourceGauge PctPaint = new() { Read = jgm => jgm->Pictomancer.Paint, Write = (jgm, v) => jgm->Pictomancer.Paint = (byte)v, Max = 5 };
    private static readonly ResourceGauge BlmPolyglot = new() { Read = jgm => jgm->BlackMage.PolyglotStacks, Write = (jgm, v) => jgm->BlackMage.PolyglotStacks = (byte)v, Max = 3 };
    private static readonly ResourceGauge PldOath = new() { Read = jgm => jgm->Paladin.OathGauge, Write = (jgm, v) => jgm->Paladin.OathGauge = (byte)v, Max = 100 };

    // Gauges with time-based behavior, ticked by TimedGaugeHandler.
    public static readonly TimedGauge[] TimedGauges =
    [
        new(JobId.WhiteMage, WhmLily, 20f, decay: false, writeTimer: (jgm, v) => jgm->WhiteMage.LilyTimer = (short)v),        // Healing Lily: +1 every 20s
        new(JobId.Sage, SgeAddersgall, 20f, decay: false, writeTimer: (jgm, v) => jgm->Sage.AddersgallTimer = (short)v), // Addersgall: +1 every 20s
        new(JobId.Gunbreaker, GnbComboStep, 30f, decay: true),    // Gnashing Fang combo: resets to 0 after 30s (no visual timer field)
        new(JobId.Paladin, PldConfiteorStep, 30f, decay: true), // Confiteor route: safety reset if abandoned mid-combo
        new(JobId.DarkKnight, DrkDeliriumStep, 30f, decay: true),  // Scarlet Delirium route: safety reset if abandoned mid-combo
        new(JobId.BlackMage, BlmPolyglot, 30f, decay: false),     // Polyglot: +1/30s (real gen needs active Enochian; approximated as always-on in-sim)
    ];

    private static readonly Dictionary<uint, IActionEffect[]> Actions = new()
    {
        // GNB
        [16145] = [Combo(Gauge(GnbCartridge, 1)), Gauge(GnbComboStep, -2)],   // Solid Barrel (+ breaks Gnashing Fang)
        [16149] = [Combo(Gauge(GnbCartridge, 1)), Gauge(GnbComboStep, -2)],   // Demon Slaughter (+ breaks)
        [16164] = [Gauge(GnbCartridge, 3), Status(3840, 30f)],   // Bloodfest → 3 cartridges + Ready to Reign
        // Gnashing Fang route (step 0→1→2→0) + the Ready-to status that lights the Continuation:
        [16146] = [Gauge(GnbComboStep, 1), Status(1842, 10f)],   // Gnashing Fang → Ready to Rip
        [16147] = [Gauge(GnbComboStep, 1), Status(1843, 10f)],   // Savage Claw → Ready to Tear
        [16150] = [Gauge(GnbComboStep, -2), Status(1844, 10f)],  // Wicked Talon → Ready to Gouge
        // GNB's other combos break Gnashing Fang (can't run two at once):
        [16137] = [Gauge(GnbComboStep, -2)],   // Keen Edge
        [16139] = [Gauge(GnbComboStep, -2)],   // Brutal Shell
        [16141] = [Gauge(GnbComboStep, -2)],   // Demon Slice
        [36937] = [Gauge(GnbComboStep, -2)],   // Reign of Beasts
        [36938] = [Gauge(GnbComboStep, -2)],   // Noble Blood
        [36939] = [Gauge(GnbComboStep, -2)],   // Lion Heart
        // buffs / Continuation procs:
        [16138] = [Status(1831, 20f), Status(3886, 30f)],   // No Mercy (+ Ready to Break)
        [16162] = [Status(2686, 10f)],                      // Burst Strike → Ready to Blast
        [16163] = [Status(3839, 10f)],                      // Fated Circle → Ready to Raze
        // mitigation:
        [16152] = [Status(1836, 10f)],                      // Superbolide
        [16148] = [Status(3838, 15f)], [36935] = [Status(3838, 15f)],   // Nebula / Great Nebula → Great Nebula
        [16140] = [Status(1832, 20f)],                      // Camouflage
        [16160] = [TargetStatus(1839, 15f)],                // Heart of Light
        [16161] = [TargetStatus(2683, 8f), TargetStatus(2684, 4f), TargetStatus(2685, 20f)],   // Heart of Stone → Heart of Corundum + Clarity + Catharsis
        [25758] = [TargetStatus(2683, 8f), TargetStatus(2684, 4f), TargetStatus(2685, 20f)],   // Heart of Corundum

        // Tank role
        [7531] = [Status(1191, 20f)],   // Rampart
        [7535] = [EnemyStatus(1193, 15f)],   // Reprisal

        // Tank LB3
        [199]   = [TargetStatus(196, 8f)],    // Last Bastion
        [4240]  = [TargetStatus(863, 8f)],    // Land Waker
        [4241]  = [TargetStatus(864, 8f)],    // Dark Force
        [17105] = [TargetStatus(1931, 8f)],   // Gunmetal Soul

        // WAR
        [37] = [Combo(Gauge(WarBeast, 10))],   // Maim
        [42] = [Combo(Gauge(WarBeast, 20))],   // Storm's Path
        [45] = [Combo(Gauge(WarBeast, 10), Status(2677, 30f))],   // Storm's Eye (+ Surging Tempest)
        [52] = [Gauge(WarBeast, 50), Status(1897, 30f)],          // Infuriate (+ Nascent Chaos)
        [16462] = [Combo(Status(2677, 30f), Gauge(WarBeast, 20))],  // Mythril Tempest → Surging Tempest + 20 Beast
        [7389]  = [Status(1177, 15f, 3), Status(2624, 30f)],  // Inner Release (3 stk) + Primal Rend Ready
        [25753] = [Status(3834, 20f)],         // Primal Rend → Primal Ruination Ready
        // mitigation:
        [43]    = [Status(409, 10f)],          // Holmgang
        [44]    = [Status(3832, 15f)], [36923] = [Status(3832, 15f)],   // Vengeance / Damnation → Damnation
        [40]    = [Status(87, 10f)],           // Thrill of Battle
        [7388]  = [TargetStatus(1457, 30f), TargetStatus(2108, 15f)],   // Shake It Off (+ Over Time)
        [3551]  = [Status(2678, 8f), Status(2679, 4f), Status(2680, 20f)],   // Raw Intuition → Bloodwhetting + Stem the Flow + Stem the Tide
        [25751] = [Status(2678, 8f), Status(2679, 4f), Status(2680, 20f)],   // Bloodwhetting
        [16464] = [TargetStatus(1858, 8f), Status(2679, 4f), Status(2680, 20f)],   // Nascent Flash → Nascent Glint on the target

        // DRK
        [3632]  = [Combo(Gauge(DrkBlood, 20))],   // Souleater
        [16468] = [Combo(Gauge(DrkBlood, 20))],   // Stalwart Soul
        [7390]  = [Status(3836, 15f, 3), Status(742, 15f, 3)], // Delirium (3 stk) + Blood Weapon (3 stk)
        [16470] = [Status(751, 30f)], [16469] = [Status(751, 30f)], [16467] = [Status(751, 30f)], [16466] = [Status(751, 30f)], // Edge/Flood → Darkside
        [16472] = [Status(3837, 30f)],            // Living Shadow → Scorn
        [7393]  = [TargetStatus(1178, 7f), Status(752, 0f)],   // The Blackest Night (+ Dark Arts, no-expiry)
        // mitigation:
        [3638]  = [Status(810, 10f)],             // Living Dead
        [3636]  = [Status(3835, 15f), Status(3902, 20f)], [36927] = [Status(3835, 15f), Status(3902, 20f)],   // Shadow Wall / Shadowed Vigil (+ Vigilant)
        [3634]  = [Status(746, 10f)],             // Dark Mind
        [16471] = [TargetStatus(1894, 15f)],      // Dark Missionary
        [25754] = [TargetStatus(2682, 10f)],      // Oblation
        // Scarlet Delirium route (under Delirium): step drives Scarlet Delirium→Comeuppance→Torcleaver swap.
        [36928] = [Gauge(DrkDeliriumStep, 1)],    // Scarlet Delirium → step 1 (Comeuppance)
        [36929] = [Gauge(DrkDeliriumStep, 1)],    // Comeuppance → step 2 (Torcleaver)
        [36930] = [Gauge(DrkDeliriumStep, -2)],   // Torcleaver → ends route

        // DRG
        [16479] = [Gauge(DrgFocus, 1)],   // Raiden Thrust
        [25770] = [Gauge(DrgFocus, 1)],   // Draconian Fury
        [85]    = [Status(1864, 20f)],   // Lance Charge
        [3557]  = [Status(786, 20f)],    // Battle Litany
        [83]    = [Status(116, 5f)],     // Life Surge
        [36955] = [Combo(Status(2720, 30f))], [87] = [Combo(Status(2720, 30f))], [7397] = [Combo(Status(2720, 30f))], // Spiral Blow/Disembowel/Sonic Thrust → Power Surge (combo bonus)
        [36952] = [Combo(Status(1863, 30f))], [16477] = [Combo(Status(1863, 30f))],  // Drakesbane/Coerthan Torment → Draconian Fire (combo bonus)
        [16478] = [Status(1243, 15f)], [92] = [Status(1243, 15f)],     // High Jump/Jump → Dive Ready
        [94]    = [Status(1870, 15f)],   // Elusive Jump → Enhanced Piercing Talon
        [96]    = [Status(3845, 30f)],   // Dragonfire Dive → Dragon's Flight
        [3555]  = [Status(3177, 20f), Status(3844, 20f)],  // Geirskogul → Life of the Dragon + Nastrond Ready
        [16480] = [Status(3846, 20f)],   // Stardiver → Starcross Ready

        // NIN
        [2240]  = [Gauge(NinNinki, 5)],          // Spinning Edge
        [2242]  = [Combo(Gauge(NinNinki, 5))],   // Gust Slash
        [2247]  = [Gauge(NinNinki, 5)],          // Throwing Dagger
        [2254]  = [Gauge(NinNinki, 5)],          // Death Blossom
        [2255]  = [Combo(Gauge(NinNinki, 15)), Gauge(NinKazematoi, -1)],  // Aeolian Edge (spends 1 Kazematoi)
        [3563]  = [Combo(Gauge(NinNinki, 15), Gauge(NinKazematoi, 2))],   // Armor Crush (+2 Kazematoi, combo)
        [16488] = [Combo(Gauge(NinNinki, 5))],   // Hakke Mujinsatsu
        [16489] = [Gauge(NinNinki, 50), Status(2689, 30f)], // Meisui (+ Meisui status)
        [25774] = [Gauge(NinNinki, 10)],         // Phantom Kamaitachi
        [25777] = [Gauge(NinNinki, 5)],          // Forked Raiju
        [25778] = [Gauge(NinNinki, 5)],          // Fleeting Raiju
        [36957] = [Gauge(NinNinki, 40), Status(3850, 30f)], // Dokumori (+ Higi)
        [2264]  = [Status(497, 15f)],                     // Kassatsu
        [7403]  = [Status(1186, 6f), Status(3851, 30f)],  // Ten Chi Jin + Tenri Jindo Ready
        [2267]  = [Status(2690, 30f, 3)],                 // Raiton → Raiju Ready (3 stk)
        [16493] = [Status(1954, 30f, 5), Status(2723, 45f)], // Bunshin (5 stk) + Phantom Kamaitachi Ready
        [2271]  = [Status(3848, 20f)],                    // Suiton → Shadow Walker

        // RPR
        [24373] = [Gauge(RprSoul, 10)],          // Slice
        [24374] = [Combo(Gauge(RprSoul, 10))],   // Waxing Slice
        [24375] = [Combo(Gauge(RprSoul, 10))],   // Infernal Slice
        [24376] = [Gauge(RprSoul, 10)],          // Spinning Scythe
        [24377] = [Combo(Gauge(RprSoul, 10))],   // Nightmare Scythe
        [24378] = [Gauge(RprSoul, 10)],          // Shadow of Death
        [24379] = [Gauge(RprSoul, 10)],          // Whorl of Death
        [24380] = [Gauge(RprSoul, 50)],          // Soul Slice
        [24381] = [Gauge(RprSoul, 50)],          // Soul Scythe
        [24386] = [Gauge(RprSoul, 10)],          // Harpe
        [24388] = [Gauge(RprSoul, 10)],          // Harvest Moon
        [24382] = [Gauge(RprShroud, 10), Status(2589, 60f)],  // Gibbet (+ Enhanced Gallows)
        [24383] = [Gauge(RprShroud, 10), Status(2588, 60f)],  // Gallows (+ Enhanced Gibbet)
        [24384] = [Gauge(RprShroud, 10)],        // Guillotine
        [36970] = [Gauge(RprShroud, 10), Status(2589, 60f)],  // Executioner's Gibbet (+ Enhanced Gallows)
        [36971] = [Gauge(RprShroud, 10), Status(2588, 60f)],  // Executioner's Gallows (+ Enhanced Gibbet)
        [36972] = [Gauge(RprShroud, 10)],        // Executioner's Guillotine
        // status grants:
        [24389] = [Status(2587, 30f)], [24392] = [Status(2587, 30f)], [24390] = [Status(2587, 30f)], [24391] = [Status(2587, 30f)], // → Soul Reaver
        [24395] = [Gauge(RprVoidShroud, 1), Status(2591, 30f)], [24396] = [Gauge(RprVoidShroud, 1), Status(2590, 30f)], // Void/Cross Reaping → +1 Void Shroud + Enhanced Cross/Void
        [24397] = [Gauge(RprVoidShroud, 1)],   // Grim Reaping → +1 Void Shroud
        [24394] = [Gauge(RprLemureShroud, 5), Status(2593, 30f), Status(3857, 30f)],  // Enshroud → 5 Lemure Shroud + Enshrouded + Oblatio
        [24393] = [Status(3858, 30f, 2)],                  // Gluttony → Executioner (2 stk)
        [24387] = [Status(2594, 0f)],                      // Soulsow (no-expiry)
        [24401] = [Status(2845, 10f), Status(2595, 10f)], [24402] = [Status(2845, 10f), Status(2595, 10f)], // Ingress/Egress → Enhanced Harpe + Threshold
        [24385] = [Status(3905, 30f), Status(3859, 30f)],  // Plentiful Harvest → Ideal Host + Perfectio Occulta
        [24398] = [Status(3860, 30f)],                     // Communio → Perfectio Parata
        [24405] = [Status(2599, 20f), Status(2601, 6f)],   // Arcane Circle + Bloodsown Circle

        // SAM — Kenki only; Sen is a flag set, handled elsewhere if ever
        [36963] = [Gauge(SamKenki, 5)],          // Gyofu (upgrades from Hakaze)
        [7478]  = [Combo(Gauge(SamKenki, 5), Status(1298, 40f))],   // Jinpu (+ Fugetsu)
        [7479]  = [Combo(Gauge(SamKenki, 5), Status(1299, 40f))],   // Shifu (+ Fuka)
        [7480]  = [Combo(Gauge(SamKenki, 15))],  // Yukikaze
        [7481]  = [Combo(Gauge(SamKenki, 10))],  // Gekko
        [7482]  = [Combo(Gauge(SamKenki, 10))],  // Kasha
        [25780] = [Gauge(SamKenki, 10)],         // Fuko (upgrades from Fuga)
        [7484]  = [Combo(Gauge(SamKenki, 10), Status(1298, 40f))],  // Mangetsu (+ Fugetsu)
        [7485]  = [Combo(Gauge(SamKenki, 10), Status(1299, 40f))],  // Oka (+ Fuka)
        [7486]  = [Gauge(SamKenki, 10)],         // Enpi
        [16482] = [Gauge(SamKenki, 50), Status(2959, 30f), Status(3855, 30f)], // Ikishoten (+ Ogi Namikiri Ready + Zanshin Ready)
        [7493]  = [Status(1236, 15f)],           // Hissatsu: Yaten → Enhanced Enpi
        [7499]  = [Status(1233, 20f, 3), Status(3856, 30f)], // Meikyo Shisui (3 stk) + Tendo
        [7488]  = [Status(3852, 30f), Gauge(SamMeditation, 1)], [7487] = [Status(3852, 30f), Gauge(SamMeditation, 1)], [36965] = [Status(3852, 30f), Gauge(SamMeditation, 1)], [36966] = [Status(3852, 30f), Gauge(SamMeditation, 1)], // iaijutsu → Tsubame-gaeshi Ready + Meditation
        [7489]  = [Gauge(SamMeditation, 1)], [25781] = [Gauge(SamMeditation, 1)], // Higanbana / Ogi Namikiri → +1 Meditation

        // MCH — max-level (Heated) ids; combo prereqs upgrade, resolved via PlayerCombo
        [7411]  = [Gauge(MchHeat, 5)],                                 // Heated Split Shot
        [7412]  = [Combo(Gauge(MchHeat, 5))],                          // Heated Slug Shot
        [7413]  = [Combo(Gauge(MchHeat, 5), Gauge(MchBattery, 10))],   // Heated Clean Shot
        [2870]  = [Gauge(MchHeat, 5)],                                 // Spread Shot
        [25786] = [Gauge(MchHeat, 10)],                               // Scattergun
        [16500] = [Gauge(MchBattery, 20)],                            // Air Anchor
        [25788] = [Gauge(MchBattery, 20), Status(3865, 30f)],         // Chain Saw (+ Excavator Ready)
        [36981] = [Gauge(MchBattery, 20)],                            // Excavator
        [2876]  = [Status(851, 5f)],                                  // Reassemble → Reassembled
        [17209] = [Status(2688, 10f, 5)],                             // Hypercharge → Overheated (5 stk)
        [7414]  = [Status(3864, 30f), Status(3866, 30f)],             // Barrel Stabilizer → Hypercharged + Full Metal Machinist
        [2878]  = [Status(1946, 10f)],                                // Wildfire → self-status (enables Detonator)

        // RDM — all flat: mana is "Additional Effect", never combo-gated
        [37004] = [Gauge(RdmWhite, 2), Gauge(RdmBlack, 2)],   // Jolt III
        [37006] = [Gauge(RdmWhite, 3), Gauge(RdmBlack, 3)],   // Grand Impact
        [16526] = [Gauge(RdmWhite, 3), Gauge(RdmBlack, 3)],   // Impact
        [7511]  = [Gauge(RdmWhite, 5)],                       // Verstone
        [7510]  = [Gauge(RdmBlack, 5)],                       // Verfire
        [25856] = [Gauge(RdmWhite, 6), Random(0.5f, Status(1235, 30f))],    // Veraero III (+ Verstone Ready, 50%; guaranteed under Acceleration not modelled)
        [25855] = [Gauge(RdmBlack, 6), Random(0.5f, Status(1234, 30f))],    // Verthunder III (+ Verfire Ready, 50%; guaranteed under Acceleration not modelled)
        [16525] = [Gauge(RdmWhite, 7)],                       // Veraero II
        [16524] = [Gauge(RdmBlack, 7)],                       // Verthunder II
        [7526]  = [Gauge(RdmWhite, 11), Gauge(RdmManaStacks, -3), Random(0.2f, Status(1235, 30f))],   // Verholy (+11 W, -3 stacks, 20% Verstone Ready)
        [7525]  = [Gauge(RdmBlack, 11), Gauge(RdmManaStacks, -3), Random(0.2f, Status(1234, 30f))],   // Verflare (+11 B, -3 stacks, 20% Verfire Ready)
        [16530] = [Gauge(RdmWhite, 4), Gauge(RdmBlack, 4)],   // Scorch
        [25858] = [Gauge(RdmWhite, 4), Gauge(RdmBlack, 4)],   // Resolution
        [7505]  = [Random(0.5f, Status(1234, 30f))],   // Verthunder → Verfire Ready (50%)
        [7507]  = [Random(0.5f, Status(1235, 30f))],   // Veraero → Verstone Ready (50%)
        [7518]  = [Status(1238, 20f), Status(3877, 30f)],  // Acceleration + Grand Impact Ready
        [7520]  = [Status(1239, 20f), Status(3876, 30f)],  // Embolden + Thorned Flourish
        [7521]  = [Status(1971, 30f), Status(3875, 30f, 3), Status(3878, 30f)], // Manafication → range + Magicked Swordplay (3 stk) + Prefulgence Ready
        // Enchanted melee each grant 1 Mana Stack (at 3, Verthunder/Veraero → Verflare/Verholy):
        [7527] = [Gauge(RdmManaStacks, 1)], [7528] = [Gauge(RdmManaStacks, 1)], [7529] = [Gauge(RdmManaStacks, 1)], // Enchanted Riposte/Zwerchhau/Redoublement
        [7530] = [Gauge(RdmManaStacks, 1)], [37002] = [Gauge(RdmManaStacks, 1)], [37003] = [Gauge(RdmManaStacks, 1)], // Enchanted Moulinet/Deux/Trois
        // TODO: unbalanced mana

        // SCH — Aetherflow only; the fairy gauge fills passively (not modelled)
        [166]  = [Gauge(SchAetherflow, 3)],   // Aetherflow
        [3587] = [Gauge(SchAetherflow, 3)],   // Dissipation
        [7436]  = [Status(3882, 30f)],        // Chain Stratagem → Impact Imminent
        [16542] = [Status(1896, 15f)],        // Recitation
        [37014] = [Status(3884, 20f), Status(4327, 20f)],   // Seraphism → self-status (Manifestation/Accession/Seraphic Halo swaps)

        // VPR
        [34620] = [Gauge(VprRattlingCoil, 1)],      // Vicewinder
        [34623] = [Gauge(VprRattlingCoil, 1)],      // Vicepit
        [34647] = [Gauge(VprRattlingCoil, 1), Status(3671, 30f)],   // Serpent's Ire (+ Ready to Reawaken)
        [34610] = [Gauge(VprSerpentOffering, 10), Status(3647, 60f)],  // Flanksting Strike (+ Hindstung Venom)
        [34611] = [Gauge(VprSerpentOffering, 10), Status(3648, 60f)],  // Flanksbane Fang (+ Hindsbane Venom)
        [34612] = [Gauge(VprSerpentOffering, 10), Status(3646, 60f)],  // Hindsting Strike (+ Flanksbane Venom)
        [34613] = [Gauge(VprSerpentOffering, 10), Status(3645, 60f)],  // Hindsbane Fang (+ Flankstung Venom)
        [34618] = [Gauge(VprSerpentOffering, 10), Status(3650, 60f)],  // Jagged Maw (+ Grimskin's Venom)
        [34619] = [Gauge(VprSerpentOffering, 10), Status(3649, 60f)],  // Bloodied Maw (+ Grimhunter's Venom)
        [34621] = [Gauge(VprSerpentOffering, 5), Status(3668, 40f), Status(3657, 30f)],   // Hunter's Coil (+ Hunter's Instinct + Hunter's Venom)
        [34622] = [Gauge(VprSerpentOffering, 5), Status(3669, 40f), Status(3658, 30f)],   // Swiftskin's Coil (+ Swiftscaled + Swiftskin's Venom)
        [34624] = [Gauge(VprSerpentOffering, 5), Status(3668, 40f), Status(3659, 30f)],   // Hunter's Den (+ Hunter's Instinct + Fellhunter's Venom)
        [34625] = [Gauge(VprSerpentOffering, 5), Status(3669, 40f), Status(3660, 30f)],   // Swiftskin's Den (+ Swiftscaled + Fellskin's Venom)
        [34626] = [Gauge(VprAnguineTribute, 5), Status(3670, 30f)],  // Reawaken (+ Reawakened)
        [34606] = [Status(3772, 60f)], [34614] = [Status(3772, 60f)],  // Steel Fangs/Maw → Honed Reavers
        [34607] = [Status(3672, 60f)], [34615] = [Status(3672, 60f)],  // Reaving Fangs/Maw → Honed Steel
        [34608] = [Status(3668, 40f)], [34616] = [Status(3668, 40f)],  // Hunter's Sting/Bite → Hunter's Instinct
        [34609] = [Status(3669, 40f)], [34617] = [Status(3669, 40f)],  // Swiftskin's Sting/Bite → Swiftscaled
        [34633] = [Status(3665, 60f)], [34644] = [Status(3666, 60f)],  // Uncoiled Fury/Twinfang → Poised

        // WHM — Blood Lily only; casting these needs a Lily (passive gauge, not fed here)
        [16531] = [Gauge(WhmBloodLily, 1)],   // Afflatus Solace
        [16534] = [Gauge(WhmBloodLily, 1)],   // Afflatus Rapture
        [136]   = [Status(157, 15f), Status(3879, 30f, 3)],   // Presence of Mind → self-haste + Sacred Sight (3 stk)
        [16536] = [Status(3881, 30f)],   // Temperance → Divine Grace (enables Divine Caress)
        [120]   = [Random(0.15f, Status(155, 15f))],   // Cure → Freecure (15%)

        // DNC — Feathers only (50% procs); Esprit is buff-gated, not modelled
        // Esprit +5 per own damage GCD (base Esprit-trait rate; dance-partner contribution not modelled), +50 from Tillana.
        [15991] = [Random(0.5f, Gauge(DncFeathers, 1)), Gauge(DncEsprit, 5)],   // Reverse Cascade
        [15992] = [Random(0.5f, Gauge(DncFeathers, 1)), Gauge(DncEsprit, 5)],   // Fountainfall
        [15995] = [Random(0.5f, Gauge(DncFeathers, 1)), Gauge(DncEsprit, 5)],   // Rising Windmill
        [15996] = [Random(0.5f, Gauge(DncFeathers, 1)), Gauge(DncEsprit, 5)],   // Bloodshower
        [25790] = [Gauge(DncEsprit, 50)],   // Tillana → +50 Esprit
        [15989] = [Random(0.5f, Status(2693, 30f)), Gauge(DncEsprit, 5)], [15993] = [Random(0.5f, Status(2693, 30f)), Gauge(DncEsprit, 5)],   // Cascade/Windmill → Silken Symmetry (50%)
        [15990] = [Combo(Random(0.5f, Status(2694, 30f))), Gauge(DncEsprit, 5)], [15994] = [Combo(Random(0.5f, Status(2694, 30f))), Gauge(DncEsprit, 5)],   // Fountain/Bladeshower → Silken Flow (50%, combo bonus)
        [16007] = [Random(0.5f, Status(1820, 30f))], [16008] = [Random(0.5f, Status(1820, 30f))],   // Fan Dance I/II → Threefold Fan Dance (50%)
        [16011] = [Status(1825, 20f), Status(2700, 20f)],               // Devilment + Flourishing Starfall
        [16013] = [Status(3017, 30f), Status(3018, 30f), Status(1820, 30f), Status(2699, 30f), Status(3868, 30f)], // Flourish (5 procs)
        [15997] = [Status(1818, 15f)], [15998] = [Status(1819, 15f)],   // Standard/Technical Step
        [16003] = [Status(1821, 60f), Status(3867, 30f)],               // Standard Finish → dmg + Last Dance Ready
        [16004] = [Status(1822, 20f), Status(2698, 30f), Status(3869, 30f)], // Technical Finish → dmg + Flourishing Finish + Dance of Dawn Ready
        [36984] = [Status(1821, 60f), Status(3867, 30f)],               // Finishing Move → Standard Finish + Last Dance Ready

        // PLD
        [20]    = [Status(76, 20f), Status(3847, 30f)],      // Fight or Flight → dmg buff + Goring Blade Ready
        [3539]  = [Combo(Status(1902, 30f), Status(2673, 30f))],    // Royal Authority → Atonement Ready + Divine Might (combo bonus)
        [16457] = [Combo(Status(2673, 30f))],                       // Prominence → Divine Might (combo bonus)
        [7383]  = [Status(1368, 30f, 4), Status(3019, 30f)], // Requiescat (4 stk) + Confiteor Ready
        [36921] = [Status(1368, 30f, 4), Status(3019, 30f)], // Imperator (Lv100 upgrade)
        [16460] = [Status(3827, 30f)],                       // Atonement → Supplication Ready
        [36918] = [Status(3828, 30f)],                       // Supplication → Sepulchre Ready
        // Confiteor route: step drives the Confiteor→Blade of Faith→Truth→Valor button swap.
        [16459] = [Gauge(PldConfiteorStep, 1)],              // Confiteor → step 1 (Blade of Faith)
        [25748] = [Gauge(PldConfiteorStep, 1)],              // Blade of Faith → step 2 (Blade of Truth)
        [25749] = [Gauge(PldConfiteorStep, 1)],              // Blade of Truth → step 3 (Blade of Valor)
        [25750] = [Status(3831, 30f), Gauge(PldConfiteorStep, -3)], // Blade of Valor → Blade of Honor Ready (+ ends route)
        // mitigation:
        [30]    = [Status(82, 10f)],                         // Hallowed Ground
        [17]    = [Status(3829, 15f), Status(3830, 15f)], [36920] = [Status(3829, 15f), Status(3830, 15f)],   // Sentinel / Guardian → Guardian + Guardian's Will
        [22]    = [Status(77, 10f)],                         // Bulwark
        [3540]  = [TargetStatus(1362, 30f)],                 // Divine Veil
        [7382]  = [TargetStatus(1174, 8f)],                  // Intervention
        [7385]  = [Status(1175, 18f)],                       // Passage of Arms
        [3542]  = [Status(2674, 8f), Status(2675, 4f), Status(2676, 12f)],   // Sheltron → Holy Sheltron + Knight's Resolve + Knight's Benediction
        [25746] = [Status(2674, 8f), Status(2675, 4f), Status(2676, 12f)],   // Holy Sheltron

        // MNK
        [7395]  = [Status(1181, 20f), Status(3843, 20f)],   // Riddle of Fire + Fire's Rumination
        [25766] = [Status(2687, 15f), Status(3842, 15f)],   // Riddle of Wind + Wind's Rumination
        [7396]  = [Status(1185, 20f), Status(1182, 20f)],   // Brotherhood + Meditative Brotherhood
        [69]    = [Status(110, 20f, 3)],                    // Perfect Balance (3 stk)
        [4262]  = [Status(2513, 30f)],                      // Form Shift → Formless Fist
        [36950] = [Status(2513, 30f)],                      // Fire's Reply → Formless Fist
        [25764] = [Status(2513, 30f)],                      // Masterful Blitz → Formless Fist
        [25765] = [Status(2513, 30f)], [25768] = [Status(2513, 30f)], [25769] = [Status(2513, 30f)], [36948] = [Status(2513, 30f)], // resolved blitz finishers → Formless Fist
        [7394]  = [Status(3841, 30f)],   // Riddle of Earth → Earth's Rumination
        [36940] = [Gauge(MnkChakra, 1)], [36941] = [Gauge(MnkChakra, 1)], [36942] = [Gauge(MnkChakra, 1)], [36943] = [Gauge(MnkChakra, 1)], // Meditation opens 1 Chakra
        [56] = [Status(107, 30f)], [66] = [Status(107, 30f)], [36947] = [Status(107, 30f)], [70] = [Status(107, 30f)],       // Opo-opo Form
        [53] = [Status(108, 30f)], [74] = [Status(108, 30f)], [36945] = [Status(108, 30f)], [62] = [Status(108, 30f)], [25767] = [Status(108, 30f)], // Raptor Form
        [54] = [Status(109, 30f)], [61] = [Status(109, 30f)], [36946] = [Status(109, 30f)], [16473] = [Status(109, 30f)],    // Coeurl Form

        // BRD
        [16495] = [Random(0.35f, Status(3861, 30f))],     // Burst Shot → Hawk's Eye (35%)
        [3560]  = [Random(0.35f, Status(3861, 30f))],     // Iron Jaws → Hawk's Eye (35%)
        [7406]  = [Random(0.35f, Status(3861, 30f))], [7407] = [Random(0.35f, Status(3861, 30f))], [25783] = [Random(0.35f, Status(3861, 30f))], // Caustic Bite/Stormbite/Ladonsbite → Hawk's Eye (35%)
        [107]   = [Status(1407, 10f), Status(3862, 30f)], // Barrage → Barrage + Resonant Arrow Ready
        [16496] = [Status(2692, 10f)],                    // Apex Arrow → Blast Arrow Ready
        [25785] = [Status(2722, 20f), Status(3863, 30f)], // Radiant Finale → Radiant Finale + Radiant Encore Ready
        [3558]  = [Status(3137, 45f)],                    // Empyreal Arrow → Repertoire
        [101]   = [Status(125, 20f)],                     // Raging Strikes
        [118]   = [Status(141, 20f)],                     // Battle Voice
        [114]   = [Status(2217, 45f)],                    // Mage's Ballad
        [116]   = [Status(2218, 45f)],                    // Army's Paeon
        [3559]  = [Status(2216, 45f)],                    // the Wanderer's Minuet

        // BLM
        [141]  = [Random(0.4f, Status(165, 30f)), Status(3870, 30f)],   // Fire → Firestarter (40%) + Thunderhead (stance entry)
        [3573] = [Status(737, 20f)],   // Ley Lines
        [158]  = [Status(3870, 30f)],  // Manafont → Thunderhead
        [142]  = [Status(3870, 30f)], [149]  = [Status(3870, 30f)], [152] = [Status(3870, 30f)], [154] = [Status(3870, 30f)], // Blizzard / Transpose / Fire III / Blizzard III → Thunderhead (stance entry)
        [25796] = [Gauge(BlmPolyglot, 1)],  // Amplifier → +1 Polyglot (spent by Foul/Xenoglossy via CostGauges)

        // SMN
        [25801] = [Status(2703, 20f), Status(3873, 30f)],  // Searing Light + Ruby's Glimmer
        [36992] = [Status(3874, 30f)],                     // Summon Solar Bahamut → Refulgent Lux
        [16508] = [Status(2701, 60f)],                     // Energy Drain → Further Ruin
        [16510] = [Status(2701, 60f)],                     // Energy Siphon → Further Ruin
        [25838] = [Status(2724, 0f)],                      // Summon Ifrit II → Ifrit's Favor
        [25840] = [Status(2725, 0f)],                      // Summon Garuda II → Garuda's Favor
        [25824] = [Status(2853, 0f)],                      // Topaz Rite → Titan's Favor
        [25833] = [Status(2853, 0f)],                      // Topaz Catastrophe → Titan's Favor
        [25835] = [Status(4403, 0f)],                      // Crimson Cyclone → Crimson Strike Ready
        [25831] = [Status(3229, 15f)],                     // Summon Phoenix → Firebird Trance
        [7427]  = [Status(3228, 15f)],                     // Summon Bahamut → Dreadwyrm Trance
        [3581]  = [Status(3228, 15f)],                     // Dreadwyrm Trance

        // AST
        [16552] = [Status(1878, 20f), Status(3893, 30f)],  // Divination + Divining
        [3606]  = [Status(841, 15f)],                      // Lightspeed
        [16559] = [Status(3895, 30f)],   // Neutral Sect → Suntouched (enables Sun Sign)
        [25874] = [Status(2718, 15f)],   // Macrocosmos (enables Microcosmos)
        [3594]  = [Random(0.15f, Status(815, 15f))],   // Benefic → Enhanced Benefic II (15%)
        [7439]  = [Status(1224, 10f)],   // Earthly Star → Earthly Dominance (enables Stellar Detonation, first 10s)

        // PCT
        [34650] = [Status(3675, 30f)], [34656] = [Status(3675, 30f)], [34653] = [Status(3675, 30f)], [34659] = [Status(3675, 30f)], // → Aetherhues
        [34651] = [Status(3676, 30f)], [34657] = [Status(3676, 30f)], [34654] = [Status(3676, 30f)], [34660] = [Status(3676, 30f)], // → Aetherhues II
        [34652] = [Gauge(PctPalette, 25), Gauge(PctPaint, 1)], [34658] = [Gauge(PctPalette, 25), Gauge(PctPaint, 1)], // Water in Blue (I/II): +25 palette, +1 paint
        [34655] = [Gauge(PctPaint, 1)], [34661] = [Gauge(PctPaint, 1)], [34688] = [Gauge(PctPaint, 1)], // Thunder in Magenta (I/II) + Rainbow Drip: +1 paint
        [34662] = [Gauge(PctPaint, -1)], [34663] = [Gauge(PctPaint, -1)],   // Holy in White / Comet in Black: spend 1 paint
        [34683] = [Status(3674, 0f, 3), Status(3691, 0f)],  // Subtractive Palette (3 stk) + Monochrome Tones
        [34674] = [Status(3680, 30f, 3)],                   // Striking Muse → Hammer Time (3 stk)
        [34675] = [Status(3685, 20f), Status(3690, 30f), Status(3689, 30f), Status(3688, 30f, 5), Status(3681, 20f)], // Starry Muse suite
        [34671] = [Status(4103, 0f)],                       // Winged Muse → Moogle Portrait
        [34673] = [Status(4104, 0f)],                       // Fanged Muse → Madeen Portrait
        [34685] = [Status(3686, 10f)],                      // Tempera Coat → self-status (enables Tempera Grassa)

        // SGE
        [24290] = [Status(2606, 0f), Gauge(SgeEukrasia, 1)], // Eukrasia (toggle, no-expiry)
        [24291] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Diagnosis
        [24292] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Prognosis
        [24293] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Dosis
        [24308] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Dosis II
        [24314] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Dosis III
        [37032] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Dyskrasia
        [37034] = [Gauge(SgeEukrasia, -1)],               // Eukrasian Prognosis II
        [37035] = [Status(3898, 20f), Status(3899, 20f)], // Philosophia + Eudaimonia
        [24309] = [Gauge(SgeAddersgall, 1)],              // Rhizomata → +1 Addersgall (passive fill in TimedGauges)
    };

    // What one landed auto-attack swing does, per job.
    private static readonly Dictionary<JobId, IActionEffect[]> AutoAttacks = new()
    {
        [JobId.Paladin] = [Gauge(PldOath, 5)],   // Oath Mastery
    };

    // The resources the player holds when a scenario starts. Phases start mid-fight, so the
    // server-built resources a player would walk in with are seeded here.
    private static readonly Dictionary<JobId, IActionEffect[]> StartingResources = new()
    {
        [JobId.Paladin] = [SetGauge(PldOath, 100)],
    };

    // status → predicates that clear it. OR-semantics: any match removes the status. The
    // dispatcher runs this BEFORE the action's effects, so a line action re-grants after
    // its own weaponskill clear.
    private static readonly Dictionary<ushort, IActionPredicate[]> StatusClearedOnAction = new()
    {
        [1842] = [ActionId(16156), AnyWeaponskill()],   // Ready to Rip:   Jugular Rip   or any WS
        [1843] = [ActionId(16157), AnyWeaponskill()],   // Ready to Tear:  Abdomen Tear  or any WS
        [1844] = [ActionId(16158), AnyWeaponskill()],   // Ready to Gouge: Eye Gouge     or any WS
        [2686] = [ActionId(25759), AnyWeaponskill()],   // Ready to Blast: Hypervelocity or any WS
        [3839] = [ActionId(36936), AnyWeaponskill()],   // Ready to Raze:  Fated Brand   or any WS
        [3840] = [ActionId(36937)],                     // Ready to Reign: Reign of Beasts
        [3886] = [ActionId(16153)],                     // Ready to Break: Sonic Break
        // PLD
        [3847] = [ActionId(3538)], [1902] = [ActionId(16460)], [2673] = [ActionId(7384), ActionId(16458)],
        [3827] = [ActionId(36918)], [3828] = [ActionId(36919)], [3019] = [ActionId(16459)], [3831] = [ActionId(36922)],
        // WAR
        [1177] = [ActionId(3549), ActionId(3550)], [1897] = [ActionId(16465), ActionId(16463)],
        [2624] = [ActionId(25753)], [3834] = [ActionId(36925)],
        [87] = [ActionId(7388)], [3832] = [ActionId(7388)], [2678] = [ActionId(7388)],   // Shake It Off dispels Thrill of Battle / Damnation / Bloodwhetting
        // DRK
        [3836] = [ActionId(36928), ActionId(36929), ActionId(36930), ActionId(36931)], [742] = [AnyGcd()],
        [3837] = [ActionId(36932)], [752] = [ActionId(16470), ActionId(16469), ActionId(16467), ActionId(16466)],
        // DRG
        [116] = [AnyWeaponskill()], [1863] = [ActionId(16479), ActionId(25770)], [1243] = [ActionId(7399)],
        [1870] = [ActionId(90)], [3845] = [ActionId(36953)], [3844] = [ActionId(7400)], [3846] = [ActionId(36956)],
        // MNK
        [2513] = [AnyWeaponskill()], [3843] = [ActionId(36950)], [3842] = [ActionId(36949)], [3841] = [ActionId(36944)],
        // NIN
        [3851] = [ActionId(36961)], [2690] = [AnyWeaponskill()], [2689] = [ActionId(7402), ActionId(36960)],
        [2723] = [ActionId(25774)], [3850] = [ActionId(36959), ActionId(36960)], [3848] = [ActionId(36958), ActionId(2258), ActionId(16489)],
        // SAM
        [1236] = [ActionId(7486)], [2959] = [ActionId(25781)], [3855] = [ActionId(36964)], [3856] = [ActionId(36966), ActionId(36965)],
        [1233] = [AnyWeaponskill()], [3852] = [ActionId(16483)],
        // RPR
        [2587] = [ActionId(24382), ActionId(24383), ActionId(24384)], [2588] = [ActionId(24382), ActionId(36970)],
        [2589] = [ActionId(24383), ActionId(36971)], [2590] = [ActionId(24395)], [2591] = [ActionId(24396)],
        [2593] = [ActionId(24398)], [3857] = [ActionId(36969)], [3858] = [ActionId(36970), ActionId(36971), ActionId(36972)],
        [2594] = [ActionId(24388)], [2845] = [ActionId(24386)], [2595] = [ActionId(24403)], [3905] = [ActionId(24394)],
        [3859] = [ActionId(24398)], [3860] = [ActionId(36973)],
        // RDM
        [1234] = [ActionId(7510)], [1235] = [ActionId(7511)], [1238] = [ActionId(25855), ActionId(25856), ActionId(16526)],
        [3877] = [ActionId(37006)], [3876] = [ActionId(37005)], [3878] = [ActionId(37007)],
        // MCH
        [851] = [AnyWeaponskill()], [2688] = [ActionId(36978), ActionId(16497)],
        [3864] = [ActionId(17209)], [3866] = [ActionId(36982)], [3865] = [ActionId(36981)], [1946] = [ActionId(16766)], // Wildfire consumed by Detonator
        // BRD
        [3861] = [ActionId(7409), ActionId(16494), ActionId(36974)], [1407] = [ActionId(7409), ActionId(16494), ActionId(36974)],
        [3862] = [ActionId(36976)], [2692] = [ActionId(25784)], [3863] = [ActionId(36977)], [3137] = [ActionId(7404)],
        // BLM
        [165] = [ActionId(152)], [3870] = [ActionId(36986), ActionId(36987), ActionId(153), ActionId(7420), ActionId(144), ActionId(7447)],
        // SMN
        [2701] = [ActionId(7426)], [3873] = [ActionId(36991)], [3874] = [ActionId(36997)],
        [2724] = [ActionId(25835)], [2725] = [ActionId(25837)], [2853] = [ActionId(25836)], [4403] = [ActionId(25885)],
        // AST
        [3893] = [ActionId(37029)],
        [3895] = [ActionId(37031)], [2718] = [ActionId(25875)], [815] = [ActionId(3610)], [1224] = [ActionId(8324)], // Sun Sign / Microcosmos / Benefic II / Stellar Detonation consumers
        // DNC
        [2693] = [ActionId(15991), ActionId(15995)], [2694] = [ActionId(15992), ActionId(15996)],
        [3017] = [ActionId(15991), ActionId(15995)], [3018] = [ActionId(15992), ActionId(15996)],
        [1820] = [ActionId(16009)], [2699] = [ActionId(25791)], [2700] = [ActionId(25792)], [2698] = [ActionId(25790)],
        [3867] = [ActionId(36983)], [3868] = [ActionId(36984)], [3869] = [ActionId(36985)],
        [1818] = [ActionId(16003)], [1819] = [ActionId(16004)],
        // WHM / SCH / SGE
        [3879] = [ActionId(37009)],
        [3881] = [ActionId(37011)],   // Divine Grace: consumed by Divine Caress
        [155]  = [ActionId(135)],     // Freecure: consumed by Cure II

        [3882] = [ActionId(37012)], [1896] = [ActionId(185), ActionId(37013), ActionId(3583), ActionId(7434), ActionId(37015), ActionId(37016)],
        [2606] = [ActionId(24291), ActionId(24292), ActionId(24293), ActionId(24308), ActionId(24314), ActionId(37032), ActionId(37034)],
        // VPR
        [3772] = [ActionId(34607), ActionId(34615)], [3672] = [ActionId(34606), ActionId(34614)],
        [3645] = [ActionId(34610)], [3646] = [ActionId(34611)], [3647] = [ActionId(34612)], [3648] = [ActionId(34613)],
        [3649] = [ActionId(34618)], [3650] = [ActionId(34619)], [3657] = [ActionId(34636)], [3658] = [ActionId(34637)],
        [3659] = [ActionId(34638)], [3660] = [ActionId(34639)], [3665] = [ActionId(34644)], [3666] = [ActionId(34645)],
        [3670] = [ActionId(34631)], [3671] = [ActionId(34626)],
        // PCT
        [3675] = [ActionId(34651), ActionId(34657), ActionId(34654), ActionId(34660)],
        [3676] = [ActionId(34652), ActionId(34658), ActionId(34655), ActionId(34661)],
        [3674] = [ActionId(34653), ActionId(34659), ActionId(34654), ActionId(34660), ActionId(34655), ActionId(34661)],
        [3691] = [ActionId(34663)], [3690] = [ActionId(34683)], [3680] = [ActionId(34678), ActionId(34679), ActionId(34680)],
        [3681] = [ActionId(34681)], [3679] = [ActionId(34688)], [4103] = [ActionId(34676)], [4104] = [ActionId(34677)], [3686] = [ActionId(34686)],
        // stack-decrement / any-GCD consumers (rely on the AddStatus(-1) consume above):
        [1368] = [ActionId(7384), ActionId(16458), ActionId(16459), ActionId(25748), ActionId(25749), ActionId(25750)], // Requiescat: empowered casts / Blades
        [1954] = [AnyWeaponskill()],   // Bunshin: one stack per weaponskill
        [3875] = [ActionId(7527), ActionId(7528), ActionId(7529), ActionId(7530), ActionId(37002), ActionId(37003)],  // Magicked Swordplay: enchanted melee
        [110]  = [AnyWeaponskill()],   // Perfect Balance: one stack per weaponskill
        [3688] = [AnyGcd()],           // Hyperphantasia: one stack per pictomancy GCD
    };

    public static void ApplyEffects(SimCharacter caster, uint actionId, ulong targetId, System.Random rng)
    {
        if (!Actions.TryGetValue(actionId, out var effects)) return;
        var ctx = new ActionContext(actionId, targetId, caster, rng);
        foreach (var effect in effects) effect.Apply(ctx);
    }

    public static void ApplyAutoAttack(SimCharacter caster, uint actionId, ulong targetId, System.Random rng)
        => ApplyJobEffects(AutoAttacks, caster, actionId, targetId, rng);

    public static void ApplyStartingResources(SimCharacter caster, System.Random rng)
        => ApplyJobEffects(StartingResources, caster, 0, 0, rng);

    private static void ApplyJobEffects(Dictionary<JobId, IActionEffect[]> table, SimCharacter caster, uint actionId, ulong targetId, System.Random rng)
    {
        if (!table.TryGetValue(PlayerJob.Current, out var effects)) return;
        var ctx = new ActionContext(actionId, targetId, caster, rng);
        foreach (var effect in effects) effect.Apply(ctx);
    }

    // Consumes one stack of every status the given action clears — `AddStatus(id, 0, -1)`
    // decrements a stacking buff (Requiescat, Meikyo…) and despawns a plain proc (Stacks 0
    // → 0 → removed), so both cases fall out of one call.
    public static void ClearStatuses(SimPlayer player, uint actionId)
    {
        foreach (var (statusId, predicates) in StatusClearedOnAction)
            foreach (var predicate in predicates)
                if (predicate.Matches(actionId)) { player.AddStatus(statusId, 0f, -1); break; }
    }

    // (job, Action-sheet PrimaryCostType) → the gauge(s) an action spends; PrimaryCostValue is
    // the amount. Covers every scalar job gauge, so spenders need NO per-action rows. Keyed on
    // job too because cost-type ids are NOT globally unique: 30 (Aetherflow) is shared by SCH and
    // SMN, 62 (Battery/Faerie) by MCH and SCH. Keying on cost-type alone routed one job's spend
    // into the other job's overlapping gauge field (SMN Fester corrupted the summon timer). Only
    // the job that owns each scalar gauge is listed; the other job's same-cost-type spender no-ops.
    // Non-scalar costs (SAM Sen 40, MNK nadi 79, PCT canvas 94, SMN attunement 71 bitfield) and
    // non-gauge costs (MP/GP/CP, status/HP, AST cards) are intentionally absent → left alone.
    private static readonly Dictionary<(JobId Job, uint CostType), ResourceGauge[]> CostGauges = new()
    {
        [(JobId.Warrior, 22u)] = [WarBeast], [(JobId.DarkKnight, 25u)] = [DrkBlood], [(JobId.Ninja, 27u)] = [NinNinki], [(JobId.Monk, 28u)] = [MnkChakra],
        [(JobId.Scholar, 30u)] = [SchAetherflow], [(JobId.Samurai, 39u)] = [SamKenki], [(JobId.RedMage, 43u)] = [RdmWhite, RdmBlack],  // RDM: N of each colour
        [(JobId.Dancer, 53u)] = [DncFeathers], [(JobId.Dancer, 54u)] = [DncEsprit], [(JobId.Gunbreaker, 55u)] = [GnbCartridge],
        [(JobId.WhiteMage, 56u)] = [WhmBloodLily], [(JobId.WhiteMage, 57u)] = [WhmLily], [(JobId.Bard, 59u)] = [BrdSoulVoice],
        [(JobId.Machinist, 61u)] = [MchHeat], [(JobId.Machinist, 62u)] = [MchBattery], [(JobId.Samurai, 63u)] = [SamMeditation],
        [(JobId.Reaper, 64u)] = [RprSoul], [(JobId.Reaper, 65u)] = [RprShroud], [(JobId.Reaper, 66u)] = [RprLemureShroud], [(JobId.Reaper, 67u)] = [RprVoidShroud],
        [(JobId.Sage, 68u)] = [SgeAddersgall], [(JobId.Sage, 69u)] = [SgeAddersting], [(JobId.Dragoon, 75u)] = [DrgFocus],
        [(JobId.Viper, 87u)] = [VprRattlingCoil], [(JobId.Viper, 88u)] = [VprSerpentOffering], [(JobId.Viper, 89u)] = [VprAnguineTribute], [(JobId.Viper, 90u)] = [VprAnguineTribute], [(JobId.Pictomancer, 91u)] = [PctPalette],
        [(JobId.BlackMage, 23u)] = [BlmPolyglot],   // Foul / Xenoglossy spend 1 Polyglot
        [(JobId.Paladin, 41u)] = [PldOath],         // Sheltron / Holy Sheltron / Intervention / Cover
    };

    // Generic spender pass: subtract an action's gauge cost, read straight from the sheet.
    public static void ApplyCost(uint actionId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (!sheet.TryGetRow(actionId, out var row)) return;
        if (!CostGauges.TryGetValue((PlayerJob.Current, (uint)row.PrimaryCostType), out var gauges)) return;
        int amount = row.PrimaryCostValue;
        if (amount <= 0) return;
        var jgm = JobGaugeManager.Instance();
        if (jgm == null) return;
        foreach (var gauge in gauges) gauge.Add(jgm, -amount);
    }

    private static IActionEffect Gauge(ResourceGauge gauge, int amount) => new GaugeEffect(gauge, amount);
    private static IActionEffect SetGauge(ResourceGauge gauge, int value) => new SetGaugeEffect(gauge, value);
    private static IActionEffect Status(ushort statusId, float duration, int stacks = 0) => new StatusEffect(statusId, duration, stacks);
    private static IActionEffect TargetStatus(ushort statusId, float duration, int stacks = 0) => new TargetStatusEffect(statusId, duration, stacks);
    private static IActionEffect EnemyStatus(ushort statusId, float duration, int stacks = 0) => new EnemyStatusEffect(statusId, duration, stacks);
    private static IActionEffect Combo(params IActionEffect[] inner) => new ComboEffect(inner);
    private static IActionEffect Random(float chance, params IActionEffect[] inner) => new RandomEffect(chance, inner);

    private static IActionPredicate ActionId(uint id) => new ActionIdPredicate(id);
    private static IActionPredicate AnyWeaponskill() => new AnyWeaponskillPredicate();
    private static IActionPredicate AnyGcd() => new AnyGcdPredicate();
}
