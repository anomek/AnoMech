using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.LightRampant;
using static AnoMech.Scenarios.Fru.LightRampant.LightRampantConstants;

internal static class LightRampantChecks
{
    public static void Run()
    {
        Assignments();
        var runs = 0;
        for (var a = 0; a < 8; a++)
        for (var b = a + 1; b < 8; b++)
        foreach (var north in new[] { false, true })
        foreach (var pairs in new[] { false, true })
        for (var weight = 0; weight < 6; weight++)
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var p = new LightRampantPattern((PartyRole)a, (PartyRole)b, 0x55, weight, north, pairs, supportPairs: weight % 2 == 0);
            var (world, scenario) = NewRun(p);
            while (world.Events.Elapsed < 49)
            {
                Step(world, scenario, 1f / fps);
                Check(world.Party.Members.All(m => !m.Dead), $"{a}/{b}/{north}/{pairs}/{fps} at {world.Events.Elapsed:F2}: " +
                    string.Join("; ", world.Party.Members.Where(m => m.Dead).Select(m => $"{m.Role} {m.Position} {string.Join(",", m.Damage.Where(d => d.Lethal).Select(d => d.Cause))}")));
                if (world.Events.Elapsed is > 45.1f and < 45.3f)
                    Check(world.Party.Members.All(m => m.StatusParams.GetValueOrDefault(Lightsteeped) == 4), "Final proteans leave four Lightsteeped stacks");
                if (world.Events.Elapsed is > 34 and < 34.3f)
                    Check(world.Party.Members.All(m => m.StatusParams.GetValueOrDefault(Lightsteeped) == 3), "All eight have three Lightsteeped after the center tower");
            }
            Check(world.Spawned.All(s => !s.IsActive), "All owned effects are cleaned up");
            Check(world.Party.Members.All(m => !m.HasStatus(Lightsteeped) && !m.HasStatus(Curse) && !m.HasStatus(Weight)), "Debuffs cleaned up");
            Check(Enumerable.Range(9, 6).Append(21).All(i => world.Map.Effects.Contains((0x00010001u, (byte)i))
                && world.Map.Effects.Contains((0x00040004u, (byte)i))), "Native solo and center tower slots activate and clear");
            Check(world.Spawned.OfType<SimEventObject>().Count() == 10, "Exactly five puddles per bait target");
            runs++;
        }
        Console.WriteLine($"PASS: Light Rampant {runs} full NA bot runs at 15/30/60 FPS.");
        ManualPlayers();
        CenterStackChanges();
        FailuresAndReset();
    }
    private static void ManualPlayers()
    {
        var runs = 0;
        foreach (var role in LightRampantPattern.Roles)
        foreach (var bait in new[] { false, true })
        foreach (var north in new[] { false, true })
        foreach (var pairs in new[] { false, true })
        {
            var a = bait ? role : (PartyRole)(((int)role + 1) % 8);
            var b = (PartyRole)(((int)a + 1) % 8);
            var p = new LightRampantPattern(a, b, 0xAA, (int)role % 6, north, pairs);
            var (guide, guideScenario) = NewRun(p);
            var (world, scenario) = NewRun(p);
            var player = new SimPlayer { Role = role };
            world.Party.Members.RemoveAll(m => m.Role == role);
            world.Party.Members.Add(player);
            while (world.Events.Elapsed < 49)
            {
                Step(guide, guideScenario, 1f / 30);
                // Supply manual client movement from the previous complete bot route.
                // Production AI must never issue a MoveTo to SimPlayer.
                player.Position = guide.Party.Get(role)!.Position;
                Step(world, scenario, 1f / 30);
                Check(world.Party.Members.All(m => !m.Dead), $"Manual {role}/{bait}/{north}/{pairs} at {world.Events.Elapsed}");
            }
            Check(player.Moves.Count == 0, "Human player movement is never automated");
            runs++;
        }
        Console.WriteLine($"PASS: {runs} manual-player route checks across all eight roles.");
    }
    private static void CenterStackChanges()
    {
        for (var mask = 0; mask < 256; mask++)
        {
            if (BitOperations.PopCount((uint)mask) != 4) continue;
            var p = new LightRampantPattern(PartyRole.RegenHealer, PartyRole.PhysRangedDps, mask, mask % 6, true, false);
            var (world, scenario) = NewRun(p);
            while (world.Events.Elapsed < 29.6f) Step(world, scenario, 1f / 30);
            var two = world.Party.Members.First(m => m.StatusParams[Lightsteeped] == 2);
            var three = world.Party.Members.First(m => m.StatusParams[Lightsteeped] == 3);
            two.AddStatus(Lightsteeped, 20, 3, overrideStacks: true);
            three.AddStatus(Lightsteeped, 20, 2, overrideStacks: true);
            while (world.Events.Elapsed < 33.6f) Step(world, scenario, 1f / 30);
            Check(two.Position.Length() > 4 && three.Position.Length() < 4, "Center tower follows live stack counts rather than initial assignments");
            while (world.Events.Elapsed < 49) Step(world, scenario, 1f / 30);
            Check(world.Party.Members.All(m => !m.Dead), "Changed-stack center tower and remaining sequence are safe");
        }
        Console.WriteLine("PASS: all 70 initial stack masks, with center soakers swapped using live debuffs.");
    }
    private static void FailuresAndReset()
    {
        var p = new LightRampantPattern(PartyRole.MainTank, PartyRole.OffTank, 0x55, 0, true, true);
        void Fail(float time, Action<SimWorld> disrupt, string cause)
        {
            var (world, scenario) = NewRun(p);
            while (world.Events.Elapsed < time) Step(world, scenario, 1f / 60);
            disrupt(world);
            for (var i = 0; i < 120; i++) Step(world, scenario, 1f / 60);
            Check(world.Party.Members.Any(m => m.Damage.Any(d => d.Lethal && d.Cause.Contains(cause))), $"Expected failure: {cause}; actual: {string.Join("; ", world.Party.Members.SelectMany(m => m.Damage).Where(d => d.Lethal).Select(d => d.Cause))}");
            Check(world.Spawned.All(s => !s.IsActive), $"Failure cleans up all effects: {cause}");
        }
        void Hold(SimWorld world, PartyRole role, Vector3 position)
        { var actor = world.Party.Get(role)!; actor.StopMoving(); actor.Position = position; }
        Fail(18.85f, w => Hold(w, p.Slot(0), Vector3.Zero), "Bright Hunger tower");
        Fail(19.2f, w => Hold(w, p.Slot(0), w.Party.Get(p.Slot(1))!.Position), "chain too short");
        Fail(15.8f, w => Hold(w, p.Slot(6), p.PuddleSpot(p.Slot(6), 0)), "puddle");
        Fail(24.9f, w => Hold(w, p.Slot(6), new Vector3(0, 0, -19)), "Powerful Light");
        Fail(26.6f, w => Hold(w, p.Slot(6), p.Orbs(true)[0]), "Holy Light orb");
        Fail(33.6f, w => Hold(w, LightRampantPattern.Roles.First(r => (p.InitialMask & (1 << (int)r)) == 0), p.MiddleWait(PartyRole.MainTank)), "Bright Hunger tower");
        Fail(37, w => Hold(w, PartyRole.MainTank, Vector3.Zero), "Banish");
        Fail(44.9f, w => Hold(w, PartyRole.MainTank, LightRampantPattern.ClockSpot(PartyRole.OffTank)), "cone overlap");
        Fail(44.9f, w => w.Party.Get(PartyRole.MainTank)!.AddStatus(Lightsteeped, 4, 4, overrideStacks: true), "five Lightsteeped");
        Fail(12, w => w.Party.Get(PartyRole.MainTank)!.Dead = true, "party member died");
        foreach (var time in new[] { 12f, 21, 26, 33, 41 })
        {
            var (world, scenario) = NewRun(p);
            while (world.Events.Elapsed < time) Step(world, scenario, 1f / 30);
            world.Events.Clear(); world.Despawn();
            var count = world.Spawned.Count;
            for (var i = 0; i < 180; i++) Step(world, scenario, 1f / 30);
            Check(world.Spawned.Count == count && world.Spawned.All(s => !s.IsActive), "Reset cannot respawn effects");
        }
        var absent = new SimWorld { FailEnemySpawns = true };
        var missingScenario = new FruLightRampantScenario(); missingScenario.Run(absent, p);
        Step(absent, missingScenario, 60);
        Check(absent.Spawned.Count == 0, "Missing boss aborts the sequence");
        Console.WriteLine("PASS: Light Rampant failure snapshots, cleanup, interrupted resets and missing-boss handling.");
    }
    private static void Assignments()
    {
        int[] priority = [6, 2, 7, 3, 4, 0, 5, 1];
        for (var a = 0; a < 8; a++)
        for (var b = a + 1; b < 8; b++)
        for (var weight = 0; weight < 6; weight++)
        for (var mask = 0; mask < 256; mask++)
        {
            if (BitOperations.PopCount((uint)mask) != 4) continue;
            var p = new LightRampantPattern((PartyRole)a, (PartyRole)b, mask, weight, true, true);
            Check(Enumerable.Range(0, 8).Select(p.Slot).Distinct().Count() == 8, "Assignments contain each role once");
            Check(p.Slot(6) == (PartyRole)(Array.IndexOf(priority, a) < Array.IndexOf(priority, b) ? a : b), "West puddle priority");
            Check(p.Weights.Select(p.NorthGroup).Distinct().Count() == 2, "The two Weight targets belong to opposite groups");
            Check(LightRampantPattern.Roles.Count(r => p.InitialStacks(r) + (p.Puddle(r) ? 0 : 1) + 1 == 2) == 4, "Exactly four two-stack center soakers");
            var flex = a >= 4 && b >= 4 ? p.Slot(5) : a < 4 && b < 4 ? p.Slot(2) : (PartyRole?)null;
            if (flex != null) Check(flex == (a >= 4 ? PartyRole.OffTank : PartyRole.PhysRangedDps), "NA flex is the end of the four-person line");
        }
        Console.WriteLine("PASS: all 11,760 puddle / chain-stack / initial-Lightsteeped assignments.");
    }
    private static (SimWorld, FruLightRampantScenario) NewRun(LightRampantPattern p)
    {
        var world = new SimWorld();
        foreach (var role in LightRampantPattern.Roles) world.Party.Members.Add(new SimPartyNpc { Role = role });
        var scenario = new FruLightRampantScenario(); scenario.Run(world, p); return (world, scenario);
    }
    private static void Step(SimWorld world, FruLightRampantScenario scenario, float delta)
    {
        world.Events.Tick(delta);
        foreach (var member in world.Party.Members) member.Advance(delta);
        scenario.Tick(delta, world.Events.Elapsed);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
