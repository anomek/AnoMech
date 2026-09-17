using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

// An owned continuation, not an IScenario or a second entry in the selector.
internal sealed class PolarizingStrikesSequence(SimWorld world, Func<SimEnemy?> pandora)
{
    private readonly DamageSolver damage = new(world.Party);
    private readonly SimEnemy?[,] helpers = new SimEnemy?[4, 2];
    private readonly PolarizingLine?[,] lines = new PolarizingLine?[4, 2];

    public void Schedule()
    {
        world.Events.Add(PolarizingStrikesPlan.Start, SpawnHelpers);
        world.Events.Add(PolarizingStrikesPlan.Start + 0.5f, () => Move(PolarizingStrikesPlan.Preposition));
        world.Events.Add(PolarizingStrikesPlan.Start + 3.5f, () => CastBoss(ActionId.PolarizingStrikes, 6.7f));
        for (var round = 0; round < 4; round++)
        {
            var index = round;
            world.Events.Add(PolarizingStrikesPlan.InTime(index), () => Move(r => PolarizingStrikesPlan.Position(r, index, false)));
            world.Events.Add(PolarizingStrikesPlan.StrikeTime(index), () => Strike(index));
            world.Events.Add(PolarizingStrikesPlan.OutTime(index), () => Move(r => PolarizingStrikesPlan.Position(r, index, true)));
            world.Events.Add(PolarizingStrikesPlan.EchoTime(index), () => Echo(index));
            if (index < 3)
                world.Events.Add(PolarizingStrikesPlan.PathsCastTime(index), () => CastBoss(ActionId.PolarizingPaths, 2.4f));
        }
        world.Events.Add(PolarizingStrikesPlan.CleanupTime, Cleanup);
    }

    private void SpawnHelpers()
    {
        if (pandora() is not { IsActive: true } boss) return;
        boss.SetRotation(MathF.PI);
        for (var round = 0; round < 4; round++)
        for (var side = 0; side < 2; side++)
            helpers[round, side] = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.Helper,
                NameId: BNpcNameId.Pandora, Level: Level, Targetable: false, EnemyList: EnemyListMode.Never,
                Placement: new Placement(boss.Position, 0f)));
    }

    private void Move(Func<PartyRole, Vector3> destination)
    {
        if (pandora() is not { IsActive: true }) return;
        foreach (var member in world.Party.ActiveMembers())
            if (member is SimPartyNpc bot) bot.MoveTo(destination(bot.Role), ParadiseRegainedPattern.RunSpeed);
    }

    private void CastBoss(uint action, float seconds)
    {
        if (pandora() is { IsActive: true } boss) boss.Cast(action, castSeconds: seconds);
    }

    private ParadiseMember[] Members()
        => Enum.GetValues<PartyRole>().Select(role => (Role: role, Member: world.Party.Get(role)))
            .Where(pair => pair.Member.IsAlive()).Select(pair => new ParadiseMember(pair.Role, pair.Member!.Position)).ToArray();

    private void Strike(int round)
    {
        if (pandora() is not { IsActive: true } boss) return;
        var members = Members();
        var light = PolarizingStrikesSnapshot.Capture(members, boss.Position, boss.Rotation, true);
        var dark = PolarizingStrikesSnapshot.Capture(members, boss.Position, boss.Rotation, false);
        lines[round, 0] = light;
        lines[round, 1] = dark;
        if (light == null || dark == null)
        {
            foreach (var member in members) Hit(member.Role, ActionId.PolarizingStrikes, "no target on one side", true);
            return;
        }
        var targets = new[] { light.Targets(members), dark.Targets(members) };
        for (var side = 0; side < 2; side++)
        {
            var line = lines[round, side]!;
            var action = side == 0 ? ActionId.CruelPathOfLight : ActionId.CruelPathOfDarkness;
            var resistance = side == 0 ? StatusId.LightResistanceDown : StatusId.DarkResistanceDown;
            if (helpers[round, side] is { IsActive: true } source)
            {
                source.SetPosition(line.Origin);
                source.SetRotation(line.Rotation);
                // The real action starts n4gw_b_g06/g07, including its authored
                // lingering trail. Never draw a substitute line or tint it.
                source.Cast(action, castSeconds: 0f);
            }
            foreach (var role in targets[side])
            {
                var overlap = targets[1 - side].Contains(role);
                var repeatedColor = world.Party.Get(role)?.HasStatus(resistance) == true;
                var lethal = targets[side].Length != 4 || overlap || repeatedColor;
                Hit(role, action, overlap ? "overlapping line stacks" : repeatedColor ? "same-color resistance down"
                    : $"{targets[side].Length}/4 players in line stack", lethal);
            }
            // Only the front bait receives resistance down; that pair then
            // crosses behind the opposite group while the next pair leads.
            var bait = world.Party.Get(line.Bait);
            // Cleanup owns the lifetime, so slowing the event clock cannot
            // expire a resistance status before the remaining rounds.
            if (bait.IsAlive()) bait!.AddStatus(resistance);
        }
    }

    private void Echo(int round)
    {
        if (pandora() is not { IsActive: true }) return;
        var members = Members();
        for (var side = 0; side < 2; side++)
        {
            if (lines[round, side] is not { } line) continue;
            var action = side == 0 ? ActionId.CruelPathOfLightEcho : ActionId.CruelPathOfDarknessEcho;
            // Retail repeat actions are hit-only timelines. The original native
            // AVFX owns the lingering visual; do not restart its first strike.
            if (helpers[round, side] is { IsActive: true } source) source.Cast(action, castSeconds: 0f);
            foreach (var role in line.Targets(members)) Hit(role, action, "line echo", true);
        }
    }

    private void Hit(PartyRole role, uint action, string cause, bool lethal)
    {
        var member = world.Party.Get(role);
        if (member.IsAlive()) damage.ApplyDamage(member!, lethal ? 1f : 0.5f, action, cause, lethal);
    }

    private void Cleanup()
    {
        foreach (var helper in helpers) helper?.Despawn();
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            world.Party.Get(role)?.RemoveStatus(StatusId.LightResistanceDown);
            world.Party.Get(role)?.RemoveStatus(StatusId.DarkResistanceDown);
        }
        Array.Clear(lines);
        Array.Clear(helpers);
    }
}
