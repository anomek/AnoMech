using System;
using AnoMech.Core.SimObjects;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native;

// Pops the floating damage number over a party member via the native _FlyText
// addon (Dalamud's IFlyTextGui). Cosmetic only — HP/death live in DamageSolver.
// Two entry points so a scenario passes whatever it has: Show for a literal number
// (e.g. a value from logs), ShowFraction for the "hits for X% of your HP" case.
internal static unsafe class DamageNumbers
{
    // ABGR. Red, matching the game's own physical/unmitigated damage tint.
    private const uint DamageColorAbgr = 0xFF3030FFu;

    // Draw a literal damage number over the actor.
    public static void Show(SimCharacter target, uint amount, string label)
    {
        var bc = target.BattleCharaPtr;
        if (bc == null) return;
        var actorIndex = ((GameObject*)bc)->ObjectIndex;

        // Positional args: kind, actorIndex, val1, val2, text1, text2, color, icon, yOffset.
        // val1 is the number, text1 the label — putting the number in both prints it twice.
        Plugin.FlyText.AddFlyText(
            FlyTextKind.Damage,
            actorIndex,
            amount,
            0,
            new SeString(new TextPayload(label)),
            new SeString(),
            DamageColorAbgr,
            0,
            0);
    }

    // Convenience: size the number off the target's own MaxHealth ("X% of max HP").
    public static void ShowFraction(SimCharacter target, float fractionOfMaxHp, string label)
    {
        var bc = target.BattleCharaPtr;
        if (bc == null) return;
        Show(target, (uint)MathF.Round(fractionOfMaxHp * bc->MaxHealth), label);
    }
}
