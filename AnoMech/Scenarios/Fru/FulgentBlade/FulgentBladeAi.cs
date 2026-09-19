using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.FulgentBlade;

internal sealed record FulgentBladeAiState(FulgentBladePattern Pattern, Func<SimEnemy?> Pandora);

internal sealed class FulgentBladeAi : IScenarioAi<FulgentBladeAiState>
{
    public string Name => "Standard — six dodges / light parties";

    public static void RunSolo(FulgentBladeAiState state, SimWorld world)
    {
        var plan = new FulgentBladePartyPlan(state.Pattern);
        world.Events.Add(FulgentBladePartyPlan.StackTime, () =>
        {
            if (state.Pandora() is not { IsActive: true } boss
                || world.Party.Player is not { IsActive: true, Dead: false }) return;
            var stacks = plan.Stacks(boss.Position, boss.Rotation);
            world.FillMissingPartyMembers(role => new Placement(
                FulgentBladePartyPlan.UsesLeftStack((int)role) ? stacks.Left : stacks.Right,
                0f).Face(boss.Position), levelOverride: FruConstants.Level);
            // The cast already locked facing. Target the newly filled MT slot
            // now; normal following resumes after the cast/animation lock.
            boss.SetTarget(world.Party.Get(PartyRole.MainTank), follow: true);
        });
    }

    public void Run(FulgentBladeAiState state, SimWorld world)
    {
        var plan = new FulgentBladePartyPlan(state.Pattern);
        world.Events.Add(FulgentBladePartyPlan.MainTankSetupTime, () =>
        {
            if (state.Pandora() is not { IsActive: true }) return;
            foreach (var member in world.Party.ActiveMembers())
                if (member is SimPartyNpc { Role: PartyRole.MainTank } tank)
                    tank.MoveTo(plan.MainTankSetup, FulgentBladePartyPlan.RunSpeed);
        });
        world.Events.Add(FulgentBladePartyPlan.PrepositionTime, () => MoveBots(_ => plan.Preposition));
        for (var step = 0; step < FulgentBladePartyPlan.DodgeCount; step++)
        {
            var index = step;
            world.Events.Add(FulgentBladePartyPlan.DodgeTime(step), () =>
            {
                var boss = state.Pandora();
                MoveBots(bot => bot.Role == PartyRole.MainTank && boss is { IsActive: true }
                    ? plan.MainTankDodge(index, boss.Position) : plan.Dodge(index));
            });
        }
        world.Events.Add(FulgentBladePartyPlan.StackTime, () =>
        {
            if (state.Pandora() is not { IsActive: true } boss) return;
            var stacks = plan.Stacks(boss.Position, boss.Rotation);
            MoveBots(bot => FulgentBladePartyPlan.UsesLeftStack((int)bot.Role) ? stacks.Left : stacks.Right);
        });

        void MoveBots(Func<SimPartyNpc, Vector3> destination)
        {
            foreach (var member in world.Party.ActiveMembers())
            {
                // Explicitly exclude SimPlayer: selecting a role never gives
                // its movement to this AI, including when that role is MT.
                if (member is SimPartyNpc bot)
                    bot.MoveTo(destination(bot), FulgentBladePartyPlan.RunSpeed);
            }
        }
    }
}
