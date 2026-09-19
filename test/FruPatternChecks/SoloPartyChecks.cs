using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.FulgentBlade;
using static AnoMech.Scenarios.Fru.FruConstants;

internal static class SoloPartyChecks
{
    public static void Run()
    {
        var cases = 0;
        for (var position = 0; position < 4; position++)
        for (var rotation = 0; rotation < 4; rotation++)
        for (var order = 0; order < 2; order++)
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var pattern = new FulgentBladePattern(position, rotation, order == 0);
            var plan = new FulgentBladePartyPlan(pattern);
            var boss = new SimEnemy { Rotation = MathF.Atan2(plan.TankFacing.X, plan.TankFacing.Z) };
            var facing = boss.Rotation;
            var world = new SimWorld();
            var party = world.Party;
            var player = new SimPlayer { Role = role, Position = plan.Dodge(5) };
            party.Members.Add(player);
            FulgentBladeAi.RunSolo(new(pattern, () => boss), world);
            world.Events.Tick(FulgentBladePartyPlan.StackTime - 0.01f);
            Check(party.Members.Count == 1 && world.FillRequests == 0, "Exawave dodges must remain solo");
            world.Events.Tick(0.02f);
            Check(ReferenceEquals(world.Party, party) && ReferenceEquals(party.Get(role), player),
                "Late support must retain the same party and player slot");
            Check(party.Members.Count == 8 && party.Members.Select(member => member.Role).Distinct().Count() == 8,
                "Solo stacks must fill exactly seven missing roles");
            Check(party.Members.OfType<SimPartyNpc>().All(bot => bot.Level == 100), "Late FRU bots must spawn at level 100");
            Check(player.Moves.Count == 0 && player.Position == plan.Dodge(5), "The player must move to their own stack manually");
            Check(boss.Rotation == facing && ReferenceEquals(boss.Target, party.Get(PartyRole.MainTank)) && boss.Following,
                "Acquire the live MT slot without turning the boss during its cast");

            var stacks = plan.Stacks(boss.Position, boss.Rotation);
            var leftBots = party.Members.OfType<SimPartyNpc>().Count(bot => bot.Position == stacks.Left);
            var rightBots = party.Members.OfType<SimPartyNpc>().Count(bot => bot.Position == stacks.Right);
            var playerLeft = FulgentBladePartyPlan.UsesLeftStack((int)role);
            Check(leftBots == (playerLeft ? 3 : 4) && rightBots == (playerLeft ? 4 : 3),
                "Leave exactly the player's spot free in the assigned light party");
            player.Position = playerLeft ? stacks.Left : stacks.Right;
            var right = new Vector3(MathF.Cos(boss.Rotation), 0, -MathF.Sin(boss.Rotation));
            Check(party.Members.Count(member => Vector3.Dot(member.Position - boss.Position, right) <= 0) == 4,
                "Correct player stacking must put four people on each boss-relative side");
            foreach (var member in party.Members)
                Check(party.Members.Count(other => Vector3.Distance(member.Position, other.Position) <= Geometry.AkhMornRadius) == 4,
                    "Every potential Akh Morn target needs exactly four members in range");
            foreach (var bot in party.Members.OfType<SimPartyNpc>())
            {
                Check(bot.Position.Length() < Geometry.ArenaRadius, "Late bots must spawn within the arena");
                for (var group = 0; group < 3; group++)
                for (var hit = 0; hit < 7; hit++)
                {
                    if (20.5f + group * 4f + hit * 2f < FulgentBladePartyPlan.StackTime) continue;
                    for (var wave = 0; wave < 4; wave++)
                    {
                        var strip = pattern.Wave(group, wave, hit);
                        var delta = bot.Position - strip.Position;
                        var depth = delta.X * MathF.Sin(strip.Rotation) + delta.Z * MathF.Cos(strip.Rotation);
                        var width = delta.X * MathF.Cos(strip.Rotation) - delta.Z * MathF.Sin(strip.Rotation);
                        Check(!(depth >= 0 && depth <= Geometry.ExalineStep && MathF.Abs(width) <= Geometry.ExalineHalfWidth),
                            "Late support must be safe from all remaining wave snapshots");
                    }
                }
            }
            world.Events.Tick(20f);
            Check(world.FillRequests == 1 && party.Members.Count == 8, "Support must spawn only once");
            cases++;
        }

        var example = new FulgentBladePattern(0, 0, true);
        foreach (var boss in new SimEnemy?[] { null, new() { IsActive = false }, new() })
        foreach (var dead in new[] { false, true })
        {
            var world = new SimWorld();
            world.Party.Members.Add(new SimPlayer { Dead = dead });
            FulgentBladeAi.RunSolo(new(example, () => boss), world);
            world.Events.Tick(40f);
            Check(world.FillRequests == (!dead && boss is { IsActive: true } ? 1 : 0),
                "No support should appear after the player dies or the boss disappears");
        }
        var reset = new SimWorld();
        reset.Party.Members.Add(new SimPlayer());
        FulgentBladeAi.RunSolo(new(example, () => new SimEnemy()), reset);
        reset.Events.Tick(30f);
        reset.Events.Clear();
        reset.Events.Tick(50f);
        Check(reset.FillRequests == 0 && reset.Party.Members.Count == 1, "Reset must cancel the delayed support spawn");
        Console.WriteLine($"PASS: solo Akh Morn support; {cases} pattern/role combinations; spawn timing; player preserved; level 100; 4+4 stacks; remaining waves; dead/missing boss; reset.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
