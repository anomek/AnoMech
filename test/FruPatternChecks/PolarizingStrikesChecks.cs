using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.ParadiseRegained;
using static AnoMech.Scenarios.Fru.FruConstants;

internal static class PolarizingStrikesChecks
{
    public static void Run()
    {
        var count = 0;
        for (var rotation = 0; rotation < 3; rotation++)
        foreach (var east in new[] { false, true })
        foreach (var dark in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        foreach (var playerRole in Enum.GetValues<PartyRole>())
        {
            var world = NewParty(playerRole);
            var pattern = new ParadiseRegainedPattern(rotation, east, dark);
            new FruParadiseRegainedScenario().Run(world, pattern);
            for (var frame = 1; frame <= 55 * fps; frame++)
            {
                var time = frame / (float)fps;
                world.Events.Tick(1f / fps);
                foreach (var member in world.Party.Members)
                {
                    var target = member is SimPlayer
                        ? time >= 25.5f ? PolarizingStrikesPlan.PlayerPosition(playerRole, time)
                        : time >= 2f ? pattern.Position(playerRole, time >= 16.5f ? 2 : time >= 10f ? 1 : 0) : member.Position
                        : member.Moves.Count > 0 ? member.Moves[^1].Target : member.Position;
                    var delta = target - member.Position;
                    member.Position += delta.Length() <= 6f / fps ? delta : Vector3.Normalize(delta) * 6f / fps;
                    Check(member.Position.Length() < 20f, "Continuation movement stays inside arena");
                }
            }
            Check(world.Party.Player!.Moves.Count == 0, "Player is never moved during either sequence");
            var dead = world.Party.Members.FirstOrDefault(m => m.Dead);
            Check(dead == null, $"Combined run survives: rotation={rotation}, east={east}, dark={dark}, fps={fps}, player={playerRole}; {dead?.Role}: {dead?.Damage.LastOrDefault().Cause}");
            foreach (var member in world.Party.Members)
            {
                Check(member.Damage.Count(d => d.Action is ActionId.CruelPathOfLight or ActionId.CruelPathOfDarkness) == 4,
                    "Every role participates in four valid line stacks");
                Check(member.Damage.All(d => d.Action is not (ActionId.CruelPathOfLightEcho or ActionId.CruelPathOfDarknessEcho)), "Every role dodges all four echoes");
                Check(member.StatusHistory.Count == 1, "Each role leads exactly once");
                Check(member.Statuses.Count == 0, "Resistance statuses cleared on completion");
            }
            var enemies = world.Spawned.OfType<SimEnemy>().ToArray();
            var boss = enemies.Single(e => e.Config.BNpcBaseId == BNpcBaseId.Pandora);
            Check(boss.Casts.Count(c => c.Action == ActionId.PolarizingStrikes) == 1, "One Polarizing Strikes cast");
            Check(boss.Casts.Count(c => c.Action == ActionId.PolarizingPaths) == 3, "Three native Polarizing Paths casts");
            var lightHelpers = enemies.Where(e => e.Casts.Any(c => c.Action == ActionId.CruelPathOfLight)).ToArray();
            var darkHelpers = enemies.Where(e => e.Casts.Any(c => c.Action == ActionId.CruelPathOfDarkness)).ToArray();
            Check(lightHelpers.Length == 4 && darkHelpers.Length == 4, "Four native light/dark pairs");
            foreach (var helper in lightHelpers)
                Check(helper.Casts.Select(c => c.Action).SequenceEqual(new[] { ActionId.CruelPathOfLight, ActionId.CruelPathOfLightEcho }), "Native light hit then hit-only echo");
            foreach (var helper in darkHelpers)
                Check(helper.Casts.Select(c => c.Action).SequenceEqual(new[] { ActionId.CruelPathOfDarkness, ActionId.CruelPathOfDarknessEcho }), "Native dark hit then hit-only echo");
            Check(enemies.Where(e => e != boss).All(e => !e.IsActive), "All sequence helpers cleaned up");
            count++;
        }
        CheckFailuresAndReset();
        Console.WriteLine($"PASS: combined Paradise Regained + Polarizing Strikes; {count} pattern/role/frame-rate runs; four 4+4 stacks; nearest baits; all echoes; pair rotation; native casts/actions; manual player; cleanup.");
    }

    private static void CheckFailuresAndReset()
    {
        var members = Enum.GetValues<PartyRole>().Select(r => new ParadiseMember(r, PolarizingStrikesPlan.Position(r, 0, false))).ToArray();
        var light = PolarizingStrikesSnapshot.Capture(members, Vector3.Zero, MathF.PI, true)!;
        var dark = PolarizingStrikesSnapshot.Capture(members, Vector3.Zero, MathF.PI, false)!;
        Check(light.Bait == PartyRole.MainTank && dark.Bait == PartyRole.OffTank, "Native closest-per-side selection");
        var moved = members.Select(m => m with { Position = PolarizingStrikesPlan.Position(m.Role, 0, true) }).ToArray();
        Check(light.Targets(moved).Length == 0 && dark.Targets(moved).Length == 0, "Echo retains old aim after targets cross");
        members[4] = members[4] with { Position = new(-1, 0, 1) };
        Check(PolarizingStrikesSnapshot.Capture(members, Vector3.Zero, MathF.PI, true)!.Bait == PartyRole.MeleeDpsA, "Closer melee steals bait");
        Check(PolarizingStrikesSnapshot.Capture([], Vector3.Zero, MathF.PI, true) == null, "Empty side has no invented target");

        foreach (var failure in new[] { "echo", "repeat", "under", "missing" })
        {
            var world = NewParty(PartyRole.MainTank);
            var boss = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.Pandora))!;
            new PolarizingStrikesSequence(world, () => boss).Schedule();
            world.Events.Tick(35f);
            foreach (var member in world.Party.Members) member.Position = PolarizingStrikesPlan.Position(member.Role, 0, false);
            if (failure == "repeat") world.Party.Get(PartyRole.MainTank)!.AddStatus(StatusId.LightResistanceDown);
            if (failure == "under") world.Party.Get(PartyRole.RegenHealer)!.Position = new(0, 0, -10);
            if (failure == "missing") foreach (var m in world.Party.Members) m.Position = new(-6, 0, 6);
            world.Events.Tick(0.31f);
            if (failure == "echo")
            {
                world.Events.Tick(2.1f);
                Check(world.Party.Members.All(m => m.Dead), "Standing in old lines is lethal at echo");
            }
            else if (failure == "repeat") Check(world.Party.Get(PartyRole.MainTank)!.Dead, "Same-color resistance down is lethal");
            else if (failure == "under") Check(world.Party.Get(PartyRole.MainTank)!.Dead, "Underfilled line fails");
            else Check(world.Party.Members.All(m => m.Dead), "Missing side fails the party");
        }
        var reset = NewParty(PartyRole.MainTank);
        var resetBoss = reset.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.Pandora))!;
        new PolarizingStrikesSequence(reset, () => resetBoss).Schedule();
        reset.Events.Tick(30f);
        var actors = reset.Spawned.OfType<SimEnemy>().ToArray();
        reset.Events.Clear();
        reset.Despawn();
        reset.Events.Tick(30f);
        Check(actors.All(a => !a.IsActive) && actors.All(a => a.Casts.All(c => c.Action is not (ActionId.CruelPathOfLight or ActionId.CruelPathOfDarkness))), "Reset despawns helpers and cancels future strikes");
        var absent = NewParty(PartyRole.MainTank);
        new PolarizingStrikesSequence(absent, () => null).Schedule();
        absent.Events.Tick(60f);
        Check(absent.Spawned.Count == 0 && absent.Party.Members.All(m => m.Damage.Count == 0), "Missing boss produces no phantom attacks");
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
