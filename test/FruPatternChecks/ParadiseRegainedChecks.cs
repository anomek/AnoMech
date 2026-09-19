using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.ParadiseRegained;
using static AnoMech.Scenarios.Fru.FruConstants;

internal static class ParadiseRegainedChecks
{
    public static void Run()
    {
        var runs = 0;
        for (var rotation = 0; rotation < 3; rotation++)
        foreach (var east in new[] { false, true })
        foreach (var dark in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        foreach (var playerRole in Enum.GetValues<PartyRole>())
        {
            var pattern = new ParadiseRegainedPattern(rotation, east, dark);
            Check(Enumerable.Range(0, 3).Select(pattern.TowerSlot).Order().SequenceEqual(new byte[] { 51, 52, 53 }), "Visit each native tower slot once");
            var world = new SimWorld();
            foreach (var role in Enum.GetValues<PartyRole>())
            {
                SimCharacter member = role == playerRole ? new SimPlayer() : new SimPartyNpc();
                member.Role = role;
                member.Position = new(0, 0, 16);
                world.Party.Members.Add(member);
            }
            new ParadiseRegainedAi().Run(pattern, world);
            var stage = 0;
            var nextHit = 0;
            var elapsed = 0f;
            // Match the engine order: events snapshot before that frame's movement.
            for (var frame = 1; frame <= 24 * fps; frame++)
            {
                elapsed = frame / (float)fps;
                world.Events.Tick(1f / fps);
                if (elapsed >= 16.5f) stage = 2;
                else if (elapsed >= 10f) stage = 1;
                if (nextHit < 3 && elapsed >= ParadiseRegainedPattern.TowerHitTime(nextHit))
                {
                    var members = world.Party.Members.Select(m => new ParadiseMember(m.Role, m.Position)).ToArray();
                    var tower = ParadiseRegainedSnapshot.InCircle(members, pattern.TowerPosition(nextHit), 3f);
                    Check(tower.Length == 2, $"Two tower occupants: rotation={rotation}, east={east}, dark={dark}, tower={nextHit}, fps={fps}");
                    if (nextHit == 0)
                        Check(tower.Order().SequenceEqual(new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer }), "Healers soak first");
                    if (nextHit < 2)
                    {
                        var tank = members.Single(m => m.Role == ParadiseRegainedPattern.CleaveTank(nextHit));
                        var facing = MathF.Atan2(tank.Position.X, tank.Position.Z);
                        var hit = ParadiseRegainedSnapshot.Capture(members, Vector3.Zero, facing, pattern.IsDark(nextHit));
                        Check(hit.CleaveTargets.SequenceEqual(new[] { tank.Role }), "Only the assigned tank takes the cleave");
                        Check(hit.BusterTarget == ParadiseRegainedPattern.BusterTank(nextHit), "Nearest/farthest bait selects the other tank");
                        Check(hit.BusterTargets.SequenceEqual(new[] { ParadiseRegainedPattern.BusterTank(nextHit) }), "Buster must be isolated");
                    }
                    nextHit++;
                }
                foreach (var member in world.Party.Members)
                {
                    // Simulate a correctly played user independently; production AI
                    // must never issue a movement command to that player.
                    var target = member is SimPlayer ? (elapsed >= 2f ? pattern.Position(playerRole, stage) : member.Position)
                        : member.Moves.Count > 0 ? member.Moves[^1].Target : member.Position;
                    var delta = target - member.Position;
                    member.Position += delta.Length() <= 6f / fps ? delta : Vector3.Normalize(delta) * (6f / fps);
                    Check(member.Position.Length() < 20f, "Movement remains inside arena");
                }
            }
            Check(nextHit == 3, "All three towers resolved");
            Check(world.Party.Player!.Moves.Count == 0, "Player movement remains manual for all roles");
            Check(world.Party.Members.OfType<SimPartyNpc>().All(bot => bot.Moves.Count == 3), "Exactly three bot movement stages");
            runs++;
        }

        var basic = new ParadiseRegainedPattern(0, true, true);
        var party = Enum.GetValues<PartyRole>().Select(r => new ParadiseMember(r, basic.Position(r, 1))).ToArray();
        var tankPosition = party[0].Position;
        var tankFacing = MathF.Atan2(tankPosition.X, tankPosition.Z);
        party[4] = party[4] with { Position = new(0, 0, 0.1f) };
        var failedBait = ParadiseRegainedSnapshot.Capture(party, Vector3.Zero, tankFacing, true);
        Check(failedBait.BusterTarget == PartyRole.MeleeDpsA, "A closer DPS steals the dark tank buster");
        party[4] = party[4] with { Position = new(0, 0, 18f) };
        Check(ParadiseRegainedSnapshot.Capture(party, Vector3.Zero, tankFacing, false).BusterTarget == PartyRole.MeleeDpsA,
            "A farther DPS steals the light tank buster");
        party[4] = party[4] with { Position = new(0, 0, 7f) };
        Check(ParadiseRegainedSnapshot.InCircle(party, new(0, 0, 7), 3f).Length == 3, "Over-soaking is detected");
        var under = party.Where(m => m.Role != PartyRole.RegenHealer && m.Role != PartyRole.MeleeDpsA).ToArray();
        Check(ParadiseRegainedSnapshot.InCircle(under, new(0, 0, 7), 3f).Length == 1, "Under-soaking is detected");
        Check(ParadiseRegainedSnapshot.Capture([], Vector3.Zero, 0, true).BusterTarget == null, "Empty party has no bait");
        var reset = new SimWorld();
        var deadBot = new SimPartyNpc { Dead = true };
        reset.Party.Members.Add(deadBot);
        new ParadiseRegainedAi().Run(basic, reset);
        reset.Events.Tick(11f);
        Check(deadBot.Moves.Count == 0, "Dead bots are not moved");
        reset.Events.Clear();
        deadBot.Dead = false;
        reset.Events.Tick(30f);
        Check(deadBot.Moves.Count == 0, "Reset cancels remaining movements");
        CheckScenario();
        Console.WriteLine($"PASS: Paradise Regained; {runs} pattern/role/frame-rate runs; native tower positions; 2-person soaks; alternating cleaves and near/far baits; player control; bad baits; under/over-soaks; reset.");
    }

    private static void CheckScenario()
    {
        var scenario = new FruParadiseRegainedScenario();
        var runs = 0;
        for (var rotation = 0; rotation < 3; rotation++)
        foreach (var east in new[] { false, true })
        foreach (var dark in new[] { false, true })
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var pattern = new ParadiseRegainedPattern(rotation, east, dark);
            var world = NewParty(role);
            scenario.Run(world, pattern);
            for (var frame = 1; frame <= 26 * 60; frame++)
            {
                var elapsed = frame / 60f;
                world.Events.Tick(1f / 60f);
                var stage = elapsed >= 16.5f ? 2 : elapsed >= 10f ? 1 : 0;
                foreach (var member in world.Party.Members)
                {
                    var target = member is SimPlayer ? (elapsed >= 2f ? pattern.Position(role, stage) : member.Position)
                        : member.Moves.Count > 0 ? member.Moves[^1].Target : member.Position;
                    var delta = target - member.Position;
                    member.Position += delta.Length() <= 0.1f ? delta : Vector3.Normalize(delta) * 0.1f;
                }
                if (frame == 8 * 60) Check(world.Map.Effects.Count == 1, "Only tower 1 appears before 9.5s");
                if (frame == 12 * 60) Check(world.Map.Effects.Count == 2, "Only towers 1 and 2 appear before 13s");
                if (frame == 15 * 60) Check(world.Map.Effects.Count == 3, "All three towers precede first snapshot");
            }
            Check(world.Party.Members.All(m => !m.Dead), "Correct movement survives the production scenario");
            Check(world.Party.Members.Sum(m => m.Damage.Count) == 10, "Three pairs soak, with two tank hits per wing");
            Check(world.Party.Player!.Moves.Count == 0, "Scenario never moves player");
            var enemies = world.Spawned.OfType<SimEnemy>().ToArray();
            Check(enemies.Length == 13 && enemies.All(e => e.Config.Level == 100), "Level-100 Pandora, four tower/buster helpers, eight continuation helpers");
            var boss = enemies.Single(e => e.Config.BNpcBaseId == BNpcBaseId.Pandora);
            Check(boss.Casts.Select(c => c.Action).SequenceEqual(new[] { ActionId.ParadiseRegained,
                dark ? ActionId.WingsDarkThenLight : ActionId.WingsLightThenDark,
                dark ? ActionId.WingsCleaveDark : ActionId.WingsCleaveLight,
                dark ? ActionId.WingsCleaveLight : ActionId.WingsCleaveDark }), "Native cast and cleave actions follow the selected order");
            Check(boss.Casts[0].CastSeconds == 3.7f && boss.Casts[1].CastSeconds == 6.8f, "Reference cast durations");
            Check(boss.Target?.Role == PartyRole.OffTank && !boss.Following, "Boss swaps to OT and stays centered");
            Check(boss.Rotation == MathF.PI && boss.IsActive, "Continuation starts with the same boss facing north");
            Check(enemies.Skip(1).Take(4).All(e => !e.IsActive), "Tower/buster helpers cleaned up before continuation");
            Check(enemies.Skip(5).All(e => e.IsActive), "Continuation helpers ready before Polarizing Strikes");
            var busters = enemies.SelectMany(e => e.Casts).Where(c => c.Action is ActionId.WingsBusterDark or ActionId.WingsBusterLight).ToArray();
            Check(busters.Length == 2 && busters[0].Target == world.Party.Get(PartyRole.OffTank)!.GameObjectId
                && busters[1].Target == world.Party.Get(PartyRole.MainTank)!.GameObjectId, "Native busters receive the snapshotted target IDs");
            Check(world.Map.Effects.Take(3).SequenceEqual(Enumerable.Range(0, 3).Select(i => (MapEffect.ParadiseTowerShow, pattern.TowerSlot(i)))), "Native map slots follow randomized tower order");
            Check(world.Map.Effects.Skip(3).SequenceEqual(Enumerable.Range(0, 3).Select(i => (MapEffect.Hide, pattern.TowerSlot(i)))), "Each tower is dismissed at resolve");
            Check(world.Spawned.OfType<SimMapEffect>().All(t => !t.IsActive), "No active tower left at completion");
            runs++;
        }

        var basic = new ParadiseRegainedPattern(0, true, true);
        foreach (var occupants in new[] { 1, 3 })
        {
            var world = NewParty(PartyRole.RegenHealer);
            scenario.Run(world, basic);
            world.Events.Tick(14f);
            foreach (var member in world.Party.Members) member.Position = basic.Position(member.Role, 1);
            world.Party.Get(occupants == 1 ? PartyRole.RegenHealer : PartyRole.MeleeDpsA)!.Position = occupants == 1 ? new(0, 0, 12) : new(0, 0, 7);
            world.Events.Tick(1.71f);
            Check(world.Party.Members.All(m => m.Dead), "Bad tower occupancy wipes the party");
            Check(world.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).Any(c => c.Action == ActionId.ParadiseTowerFailure), "Failure plays native unmitigated explosion");
        }
        var reset = NewParty(PartyRole.MainTank);
        scenario.Run(reset, basic);
        reset.Events.Tick(10f);
        Check(reset.Map.Effects.Count == 2, "Two active towers before early reset");
        reset.Events.Clear();
        reset.Despawn();
        reset.Despawn();
        reset.Events.Tick(30f);
        Check(reset.Map.Effects.Count == 4 && reset.Map.Effects.Skip(2).All(e => e.State == MapEffect.Hide), "Early reset dismisses native towers exactly once");
        var missing = NewParty(PartyRole.MainTank);
        missing.FailEnemySpawns = true;
        scenario.Run(missing, basic);
        missing.Events.Tick(30f);
        Check(missing.Spawned.OfType<SimMapEffect>().All(t => !t.IsActive), "Missing boss does not leak towers");
        Check(missing.Party.Members.All(m => m.Damage.Count == 0), "Missing boss does not cause phantom damage");
        Console.WriteLine($"PASS: production Paradise Regained scenario; {runs} full runs; native cast/target/map calls; helper cleanup; failed towers; missing actors; early reset.");
    }

    private static SimWorld NewParty(PartyRole playerRole)
    {
        var world = new SimWorld();
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            SimCharacter member = role == playerRole ? new SimPlayer() : new SimPartyNpc();
            member.Role = role;
            member.Position = new(0, 0, 16);
            world.Party.Members.Add(member);
        }
        return world;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
