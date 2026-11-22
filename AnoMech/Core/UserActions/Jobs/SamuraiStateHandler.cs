using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Samurai's Sen (Setsu/Getsu/Ka bitfield) and Kaeshi (Tsubame-gaeshi mirror) — the non-scalar
// state a plain ResourceGauge can't hold. Sen count drives the Iaijutsu button
// (1→Higanbana, 2→Tenka Goken, 3→Midare Setsugekka) via GetAdjustedActionId; Kaeshi drives the
// Tsubame-gaeshi mirror. Kenki, statuses, and Meditation stay in the JobActions data table.
//
// This is the reference example for a per-job state handler: gate on job, switch on the executed
// action id, write the gauge struct through a JobGaugeEdit, and combo-gate anything the sheet marks a combo
// bonus (read PRE-advance combo via PlayerCombo — so state handlers run before ActionEffectHandler).
internal sealed unsafe class SamuraiStateHandler : IUserActionHandler
{
    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Samurai) return;
        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        switch (actionId)
        {
            // Sen generators — only on a valid combo finisher (off-combo grants no Sen):
            case 7480 when Combo(actionId): jgm->Samurai.SenFlags |= SenFlags.Setsu; break;   // Yukikaze
            case 7481 when Combo(actionId): jgm->Samurai.SenFlags |= SenFlags.Getsu; break;   // Gekko
            case 7482 when Combo(actionId): jgm->Samurai.SenFlags |= SenFlags.Ka;    break;   // Kasha
            case 7484 when Combo(actionId): jgm->Samurai.SenFlags |= SenFlags.Getsu; break;   // Mangetsu (AoE)
            case 7485 when Combo(actionId): jgm->Samurai.SenFlags |= SenFlags.Ka;    break;   // Oka (AoE)
            // Iaijutsu — consume all Sen and arm the matching Kaeshi mirror:
            case 7489: jgm->Samurai.SenFlags = SenFlags.None; jgm->Samurai.Kaeshi = KaeshiAction.Higanbana; break;   // Higanbana
            case 7488: jgm->Samurai.SenFlags = SenFlags.None; jgm->Samurai.Kaeshi = KaeshiAction.Goken; break;       // Tenka Goken
            case 7487: jgm->Samurai.SenFlags = SenFlags.None; jgm->Samurai.Kaeshi = KaeshiAction.Setsugekka; break;  // Midare Setsugekka
            case 36965: jgm->Samurai.SenFlags = SenFlags.None; jgm->Samurai.Kaeshi = KaeshiAction.Goken; break;      // Tendo Goken
            case 36966: jgm->Samurai.SenFlags = SenFlags.None; jgm->Samurai.Kaeshi = KaeshiAction.Setsugekka; break; // Tendo Setsugekka
            case 25781: jgm->Samurai.Kaeshi = KaeshiAction.Namikiri; break;   // Ogi Namikiri (no Sen)
            case 7495: jgm->Samurai.SenFlags = SenFlags.None; break;          // Hagakure (converts Sen → Kenki)
            // Kaeshi mirrors consume the armed Kaeshi:
            case 16485 or 16486 or 25782: jgm->Samurai.Kaeshi = default; break;   // Kaeshi: Goken / Setsugekka / Namikiri
        }
    }

    private static bool Combo(uint actionId) => PlayerCombo.IsActiveContinuation(actionId);
}
