using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Dancer's dance-step minigame — the 4-slot step sequence + progress index that a scalar gauge
// can't hold. Standard/Technical Step seed the sequence; each step action advances StepIndex; the
// Finish clears it. GetAdjustedActionId shows the player the single step button as DanceSteps[StepIndex],
// so an executed step resolves to Emboite/Entrechat/Jete/Pirouette. Feathers, Esprit, and the
// Standard/Technical Step + Finish statuses stay in the JobActions data table.
internal sealed unsafe class DancerStateHandler : IUserActionHandler
{
    private readonly System.Random _rng = new();

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        if (PlayerJob.Current != JobId.Dancer) return;
        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;

        switch (actionId)
        {
            case 15997: SeedDance(jgm, 2); break;   // Standard Step  → 2 steps, slots 2-3 blank
            case 15998: SeedDance(jgm, 4); break;   // Technical Step → 4 steps
            case 15999 or 16000 or 16001 or 16002:  // Emboite / Entrechat / Jete / Pirouette
                if (jgm->Dancer.StepIndex < 4) jgm->Dancer.StepIndex++;
                break;
            // Standard / Technical Finish, and the forms they resolve to by steps danced:
            case 16003 or 16004 or >= 16191 and <= 16196: ClearDance(jgm); break;
        }
    }

    // Fill the first `count` slots with a random DanceStep (Emboite..Pirouette), the rest with
    // Finish(0); reset progress. Approximate the real game's "no step twice in a row" by re-rolling
    // against the previous slot. Generated `DanceSteps` is an [UnscopedRef] Span<byte>, so its
    // indexer returns a writable ref into the gauge memory.
    // VERIFY in-game: DanceSteps[i] = (byte) writes reach the Step Gauge display.
    private void SeedDance(JobGaugeManager* jgm, int count)
    {
        byte prev = 0;
        for (var i = 0; i < 4; i++)
        {
            byte step = 0;
            if (i < count)
            {
                do step = (byte)(_rng.Next(4) + (int)DanceStep.Emboite);
                while (step == prev);
                prev = step;
            }
            jgm->Dancer.DanceSteps[i] = step;
        }
        jgm->Dancer.StepIndex = 0;
    }

    private static void ClearDance(JobGaugeManager* jgm)
    {
        for (var i = 0; i < 4; i++) jgm->Dancer.DanceSteps[i] = (byte)DanceStep.Finish;
        jgm->Dancer.StepIndex = 0;
    }
}
