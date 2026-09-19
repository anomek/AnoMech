using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.DiamondDust.DiamondDustConstants;

namespace AnoMech.Scenarios.Fru.DiamondDust;

internal sealed class DiamondDustAi : IScenarioAi
{
    public string Name => "NA · Partner Swap";
    public string Group => "NA";
    internal void Run(DiamondDustPattern pattern, SimWorld world, Func<bool> active, Func<IReadOnlyList<Vector3>> puddles)
    {
        void Each(Action<SimPartyNpc, PartyRole> action)
        {
            if (!active()) return;
            foreach (var role in DiamondDustPattern.Roles)
                if (world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive()) action(bot, role);
        }
        void Move(float time, Func<PartyRole, Vector3> position, float speed = RunSpeed)
            => world.Events.Add(time, () => Each((bot, role) => bot.MoveTo(position(role), speed)));
        void Slide(float time, bool behind, bool recovery = false)
            => world.Events.Add(time, () => Each((bot, _) => {
                if (recovery ? !pattern.Cursed || Vector3.Dot(bot.Position, pattern.ReflectionPosition) <= 0
                    : pattern.BehindReflection(bot.Position) == behind) return;
                if (pattern.SlideDestination(bot.Position, behind, puddles(), prepareNextSlide: recovery) is { } destination)
                    bot.Slide(destination - bot.Position, SlideDistance, SlideSpeed);
            }));
        Move(15.3f, pattern.KickSpot);
        Move(KickTime + 0.1f, pattern.StoneSpot);
        Move(StoneTime + 0.1f, pattern.KnockbackSpot);
        // Finish the knockback and stars before advancing around the rim.
        // In the cursed pattern, start CW immediately after the stars. The
        // first puddle must clear Shiva's axis or it blocks the later slide.
        if (pattern.Cursed) Move(StarTime + 0.1f, role => pattern.HolySpot(role, 0));
        for (var wave = 0; wave < 4; wave++)
        {
            var step = wave + 1;
            Move(HolyTime(wave) + 0.1f, role => pattern.HolySpot(role, step));
        }
        Move(37.9f, pattern.IceSpot);
        world.Events.Add(39.9f, () => Each((bot, _) => {
            bot.StopMoving();
            bot.Face(2 * bot.Position - pattern.GazePosition);
        }));
        Slide(GazeTime + 0.2f, false, recovery: true);
        Slide(ComboTime + 0.1f, pattern.Stillness);
        Slide(FirstHit + 0.1f, !pattern.Stillness);
        Move(IceEnd + 0.3f, _ => new Vector3(0, 0, 4));
    }
}
