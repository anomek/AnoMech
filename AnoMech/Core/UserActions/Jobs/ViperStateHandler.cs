using AnoMech.Core.Native.Implementations;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Viper's oGCD follow-ups and their windows — the non-scalar part of the gauge a ResourceGauge can't
// hold — written as retail writes them. SerpentComboState holds a follow-up kind above its low two
// bits and the presses left in them: CS's SerpentCombo for Serpent's Tail, and kinds past its names
// for the Twinfang/Twinblood follow-ups. DreadCombo opens Vicewinder's coils and Vicepit's dens (the
// second one closes the window) and, past CS's names, steps through Reawaken's Generations. The
// gauges, the melee GCD combo and the other statuses stay in the JobActions data table.
internal sealed unsafe class ViperStateHandler : IUserActionHandler
{
    private const int CoilTwins = 7, DenTwins = 8, UncoiledTwins = 9;
    private const int TwinPresses = 2, TailPresses = 1;
    private const int ReawakenedStep = 7;
    // The Generations and Ouroboros are usable only while the gauge's Reawakened timer runs; the client
    // counts it down itself once set.
    private const ushort ReawakenedMs = 30000;

    private const ushort HuntersVenom = 3657, SwiftskinsVenom = 3658, FellhuntersVenom = 3659, FellskinsVenom = 3660;
    private const ushort PoisedForTwinfang = 3665, PoisedForTwinblood = 3666;
    private const float VenomSeconds = 30f, PoisedSeconds = 60f;

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Viper || Plugin.Game?.Player is not { } player) return;
        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;
        var g = &jgm->Viper;

        switch (actionId)
        {
            // Melee finishers arm Serpent's Tail:
            case 34610 or 34611 or 34612 or 34613: SetFollowUps(g, (int)SerpentCombo.DeathRattle, TailPresses); break;
            case 34618 or 34619: SetFollowUps(g, (int)SerpentCombo.LastLash, TailPresses); break;
            // Reawaken steps through the Generations, each arming its Legacy:
            case 34626: g->DreadCombo = (DreadCombo)ReawakenedStep; g->ReawakenedTimer = ReawakenedMs; break;   // Reawaken
            case 34631: g->ReawakenedTimer = 0; break;   // Ouroboros
            case 34627: Generation(g, SerpentCombo.FirstLegacy); break;
            case 34628: Generation(g, SerpentCombo.SecondLegacy); break;
            case 34629: Generation(g, SerpentCombo.ThirdLegacy); break;
            case 34630: Generation(g, SerpentCombo.FourthLegacy); break;
            case 34634 or 34635 or 34640 or 34641 or 34642 or 34643: SpendPress(g); break;   // Serpent's Tail follow-ups

            // Vicewinder / Vicepit open both coils / dens once each; each leaves two follow-ups:
            case 34620: Open(g, DreadCombo.Dreadwinder); break;    // Vicewinder
            case 34623: Open(g, DreadCombo.PitOfDread); break;     // Vicepit
            case 34621: Window(g, DreadCombo.HuntersCoil, DreadCombo.SwiftskinsCoil, CoilTwins); break;   // Hunter's Coil
            case 34622: Window(g, DreadCombo.SwiftskinsCoil, DreadCombo.HuntersCoil, CoilTwins); break;   // Swiftskin's Coil
            case 34624: Window(g, DreadCombo.HuntersDen, DreadCombo.SwiftskinsDen, DenTwins); break;      // Hunter's Den
            case 34625: Window(g, DreadCombo.SwiftskinsDen, DreadCombo.HuntersDen, DenTwins); break;      // Swiftskin's Den
            case 34633: SetFollowUps(g, UncoiledTwins, TwinPresses); break;   // Uncoiled Fury

            case 34636: Twin(g, player, HuntersVenom, SwiftskinsVenom, VenomSeconds); break;          // Twinfang Bite
            case 34637: Twin(g, player, SwiftskinsVenom, HuntersVenom, VenomSeconds); break;          // Twinblood Bite
            case 34638: Twin(g, player, FellhuntersVenom, FellskinsVenom, VenomSeconds); break;       // Twinfang Thresh
            case 34639: Twin(g, player, FellskinsVenom, FellhuntersVenom, VenomSeconds); break;       // Twinblood Thresh
            case 34644: Twin(g, player, PoisedForTwinfang, PoisedForTwinblood, PoisedSeconds); break; // Uncoiled Twinfang
            case 34645: Twin(g, player, PoisedForTwinblood, PoisedForTwinfang, PoisedSeconds); break; // Uncoiled Twinblood
        }
    }

    private static void SetFollowUps(ViperGauge* g, int kind, int presses)
        => g->SerpentComboState = (byte)((kind << 2) | presses);

    private static void SpendPress(ViperGauge* g)
    {
        var left = (g->SerpentComboState & 3) - 1;
        g->SerpentComboState = left > 0 ? (byte)((g->SerpentComboState & ~3) | left) : (byte)0;
    }

    private static void Generation(ViperGauge* g, SerpentCombo legacy)
    {
        SetFollowUps(g, (int)legacy, TailPresses);
        g->DreadCombo = legacy == SerpentCombo.FourthLegacy ? 0 : g->DreadCombo + 1;
    }

    private static void Open(ViperGauge* g, DreadCombo window)
    {
        g->DreadCombo = window;
        g->SerpentComboState = 0;
    }

    private static void Window(ViperGauge* g, DreadCombo branch, DreadCombo other, int twins)
    {
        g->DreadCombo = g->DreadCombo == other ? 0 : branch;
        SetFollowUps(g, twins, TwinPresses);
    }

    // The first follow-up spends its own buff for the partner's; anything else just ends both.
    private static void Twin(ViperGauge* g, SimPlayer player, ushort own, ushort partner, float partnerSeconds)
    {
        var handsOver = (g->SerpentComboState & 3) == TwinPresses && player.HasStatus(own);
        player.RemoveStatus(own);
        player.RemoveStatus(partner);
        if (handsOver) player.AddStatusParam(partner, 0, partnerSeconds);
        SpendPress(g);
    }
}
