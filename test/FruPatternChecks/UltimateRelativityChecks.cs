using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.UltimateRelativity;
using static AnoMech.Scenarios.Fru.UltimateRelativity.UltimateRelativityConstants;

internal static class UltimateRelativityChecks
{
    public static void Run()
    {
        Assignments();
        var runs = 0;
        foreach (var ice in new[] { false, true })
        for (var north = 0; north < 8; north++)
        for (var mask = 0; mask < 256; mask++)
        {
            var p = Pattern(ice, north, mask);
            var (world, scenario) = NewRun(p);
            var fps = new[] { 15, 30, 60 }[mask % 3];
            while (world.Events.Elapsed < 62)
            {
                Step(world, scenario, 1f / fps);
                Check(world.Party.Members.All(m => !m.Dead), $"{ice}/{north}/{mask}/{fps} at {world.Events.Elapsed:F2}: " +
                    string.Join("; ", world.Party.Members.Where(m => m.Dead).Select(m => $"{m.Role}={p.Assignment(m.Role)} {m.Position} {string.Join(",", m.Damage.Where(d => d.Lethal).Select(d => d.Cause))}")));
            }
            Check(world.Spawned.All(s => !s.IsActive), "All native effects are released");
            Check(world.Party.Members.All(m => Statuses.All(s => !m.HasStatus(s)) && !m.ActiveActorVfx.Contains(WaitingClock)), "All statuses and waiting clocks are released");
            Check(world.Spawned.OfType<SimOmen>().Count(s => s.Path == ReturnMarker) == 8, "Eight independent Return traces");
            var lasers = world.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).Count(c => c.Action is MeltdownFirst or MeltdownRest);
            Check(lasers == 80, "Eight hourglasses fire ten shots each");
            Check(Enumerable.Range(26, 8).All(i => world.Map.Effects.Contains((0x00010001u, (byte)i)) && world.Map.Effects.Contains((0x00040004u, (byte)i))), "All eight native hourglass slots activate and clear");
            runs++;
        }
        Console.WriteLine($"PASS: Ultimate Relativity {runs} full runs; every hourglass rotation mask, arena rotation and ice role at 15/30/60 FPS.");
        ManualPlayers();
        SnapshotAndFailureChecks();
    }
    private static void Assignments()
    {
        PartyRole[] dps = [PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
        PartyRole[] supports = [PartyRole.ShieldHealer, PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank];
        var cases = 0;
        foreach (var longDps in dps)
        foreach (var mediumDps in dps.Where(r => r != longDps))
        foreach (var shortSupport in supports)
        foreach (var mediumSupport in supports.Where(r => r != shortSupport))
        foreach (var ice in new[] { false, true })
        {
            var p = new UltimateRelativityPattern(longDps, mediumDps, shortSupport, mediumSupport, ice, 0, 255, [4, 4, 4], PartyRole.MainTank);
            Check(UltimateRelativityPattern.Assignments.Select(p.Role).Distinct().Count() == 8, "Every role assigned once");
            var shortDps = dps.Where(r => r != longDps && r != mediumDps).ToArray();
            var longSupports = supports.Where(r => r != shortSupport && r != mediumSupport).ToArray();
            Check(p.Role(RelativityAssignment.ShortDpsWest) == shortDps[0] && p.Role(RelativityAssignment.ShortDpsEast) == shortDps[1], "NA short-DPS west/east priority");
            Check(p.Role(RelativityAssignment.LongSupportWest) == longSupports[0] && p.Role(RelativityAssignment.LongSupportEast) == longSupports[1], "NA long-support west/east priority");
            Check(p.Role(p.Ice) == (ice ? longDps : shortSupport), "Ice substitutes for long DPS or short support");
            Check(Enumerable.Range(0, 3).SelectMany(w => UltimateRelativityPattern.Assignments.Where(a => p.Fire(a, w))).Distinct().Count() == 7, "Seven fire debuffs, exactly one ice");
            Check(Enumerable.Range(0, 3).Select(p.Darkness).Distinct().Count() == 3, "Unholy Darkness targets are distinct");
            for (var w = 0; w < 3; w++) Check(UltimateRelativityPattern.ValidDarkness(w).Contains(p.Darkness(w)), "Unholy Darkness excludes that wave's assigned fire group");
            var (world, scenario) = NewRun(p);
            while (world.Events.Elapsed < 62) Step(world, scenario, 1f / 30);
            Check(world.Party.Members.All(m => !m.Dead), "Every role assignment completes safely");
            cases++;
        }
        var seen = new[] { new HashSet<RelativityAssignment>(), new HashSet<RelativityAssignment>(), new HashSet<RelativityAssignment>() };
        var rng = new Random(19721);
        for (var i = 0; i < 1000; i++)
        {
            var p = UltimateRelativityPattern.Random(rng);
            for (var wave = 0; wave < 3; wave++) seen[wave].Add(p.Darkness(wave));
        }
        for (var wave = 0; wave < 3; wave++) Check(seen[wave].SetEquals(UltimateRelativityPattern.ValidDarkness(wave)), "Randomization reaches every legal Unholy Darkness target");
        for (var north = 0; north < 8; north++)
        for (var clock = 0; clock < 8; clock++)
        {
            var p = Pattern(north: north);
            var slot = p.HourglassSlot(clock);
            // Independently reconstruct native LGB placement plus the SGB -20 Z offset.
            var yaw = -(slot - 26) * MathF.PI / 4;
            var native = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw)) * (10.5f - 20);
            Check(Vector3.Distance(native, p.Hourglass(clock)) < 0.001f, "Hourglass beam anchors coincide with native scenery");
        }
        Console.WriteLine($"PASS: {cases} complete party-assignment runs, NA priorities, legal randomized stack targets and native hourglass placement.");
    }
    private static void ManualPlayers()
    {
        var runs = 0;
        foreach (var role in UltimateRelativityPattern.Roles)
        foreach (var ice in new[] { false, true })
        foreach (var mask in new[] { 0, 85, 170, 255 })
        foreach (var fps in new[] { 15, 60 })
        {
            var p = Pattern(ice, (int)role, mask);
            var (guide, guideScenario) = NewRun(p);
            var (world, scenario) = NewRun(p);
            var player = new SimPlayer { Role = role };
            world.Party.Members.RemoveAll(m => m.Role == role); world.Party.Members.Add(player);
            var guidePlayer = guide.Party.Get(role)!;
            while (world.Events.Elapsed < 62)
            {
                Step(guide, guideScenario, 1f / fps);
                // Supply ordinary movement/facing, but let production Return move the player.
                if (world.Events.Elapsed < RewindTime - 0.1f || world.Events.Elapsed > RewindTime + 0.8f)
                    player.Position = guidePlayer.Position;
                player.Rotation = guidePlayer.Rotation;
                Step(world, scenario, 1f / fps);
                Check(world.Party.Members.All(m => !m.Dead), $"Manual {role}/{ice}/{mask}/{fps} at {world.Events.Elapsed}");
                if (world.Events.Elapsed is > 52 and < 54.7f) Check(player.MechanicInputLock, "Return stuns the player during rewind");
            }
            Check(player.Moves.Count == 0 && !player.MechanicInputLock, "No player AI movement; Return releases input");
            runs++;
        }
        Console.WriteLine($"PASS: {runs} manual-player runs with actual Return movement and input-lock release.");
    }
    private static void SnapshotAndFailureChecks()
    {
        var p = Pattern();
        void Advance(SimWorld world, FruUltimateRelativityScenario scenario, float time)
        { while (world.Events.Elapsed < time) Step(world, scenario, 1f / 60); }
        void Hold(SimWorld world, RelativityAssignment a, Vector3 position)
        { var m = world.Party.Get(p.Role(a))!; m.StopMoving(); m.Position = position; }
        var (persistentWorld, persistentScenario) = NewRun(p);
        Advance(persistentWorld, persistentScenario, 60);
        Check(persistentWorld.Spawned.OfType<SimMapEffect>().Count(e => e.IsActive) == 8,
            "All eight hourglasses remain visible after every laser wave finishes");
        Advance(persistentWorld, persistentScenario, 62);
        Check(persistentWorld.Spawned.OfType<SimMapEffect>().All(e => !e.IsActive),
            "Persistent hourglasses are released at scenario completion");
        void Failure(float time, Action<SimWorld> disrupt, string cause)
        {
            var (world, scenario) = NewRun(p); Advance(world, scenario, time); disrupt(world);
            Advance(world, scenario, time + 1.5f);
            Check(world.Party.Members.Any(m => m.Damage.Any(d => d.Lethal && d.Cause.Contains(cause))), $"Expected {cause}; got {string.Join(";", world.Party.Members.SelectMany(m => m.Damage).Where(d => d.Lethal).Select(d => d.Cause))}");
            Advance(world, scenario, 62);
            Check(world.Spawned.All(s => !s.IsActive), "Failure run cleans up effects");
        }
        Failure(22.8f, w => Hold(w, RelativityAssignment.ShortDpsWest, Vector3.Zero), "Dark Fire");
        Failure(32.7f, w => Hold(w, RelativityAssignment.LongDps, new Vector3(0, 0, 5)), "Dark Blizzard");
        Failure(22.8f, w => Hold(w, p.Darkness(0), p.At(p.Darkness(0), 19)), "Unholy Darkness");
        Failure(27.6f, w => Hold(w, RelativityAssignment.LongSupportWest, Vector3.Zero), "bait overlap");
        Failure(39.7f, w => Hold(w, RelativityAssignment.ShortSupport, p.Hourglass(0) + new Vector3(-4, 0, -2)), "rotating beam");
        Failure(54.7f, w => w.Party.Get(p.Role(RelativityAssignment.MediumDps))!.Face(Vector3.Zero), "Shadoweye");
        Failure(54.7f, w => Hold(w, RelativityAssignment.ShortDpsWest, Vector3.Zero), "Dark Eruption");
        Failure(54.7f, w => Hold(w, RelativityAssignment.LongSupportEast, new Vector3(6, 0, -6)), "Dark Water");
        Failure(58.5f, w => Hold(w, RelativityAssignment.ShortSupport, new Vector3(15, 0, 0)), "Shell Crusher");
        Failure(20, w => Hold(w, RelativityAssignment.MediumDps, new Vector3(21, 0, 0)), "arena boundary");
        Failure(22.8f, w => w.Party.Get(p.Role(RelativityAssignment.ShortDpsWest))!.Dead = true, "missing fire");
        // Change the actual position saved by Return; the replay must retain it.
        var (savedWorld, savedScenario) = NewRun(p);
        Advance(savedWorld, savedScenario, 27.6f);
        var moved = p.Role(RelativityAssignment.ShortDpsWest);
        var saved = p.At(RelativityAssignment.ShortDpsWest, 9.1f);
        Hold(savedWorld, RelativityAssignment.ShortDpsWest, saved);
        Advance(savedWorld, savedScenario, 28);
        Check(savedWorld.Spawned.OfType<SimOmen>().Any(m => m.Path == ReturnMarker && Vector3.Distance(m.Placement.Position, saved) < 0.001f), "Trace records the actual saved position");
        Advance(savedWorld, savedScenario, 54);
        Check(Vector3.Distance(savedWorld.Party.Get(moved)!.Position, saved) < 0.001f, "Return goes to the recorded position, not the assigned waypoint");
        foreach (var time in new[] { 12f, 25, 29, 38, 52, 53.5f, 56 })
        {
            var (world, scenario) = NewRun(p); Advance(world, scenario, time);
            world.Events.Clear(); world.Despawn(); var count = world.Spawned.Count;
            Advance(world, scenario, 63);
            Check(world.Spawned.Count == count && world.Spawned.All(s => !s.IsActive), "Reset never respawns effects");
        }
        foreach (var time in new[] { 52f, 53.5f })
        {
            var (world, scenario) = NewRun(p); Advance(world, scenario, 50.5f);
            var bot = world.Party.Get(PartyRole.MainTank)!;
            var player = new SimPlayer { Role = PartyRole.MainTank, Position = bot.Position, Rotation = bot.Rotation };
            world.Party.Members.Remove(bot); world.Party.Members.Add(player);
            Advance(world, scenario, time);
            Check(player.MechanicInputLock, "Player is locked during the interrupted Return");
            world.Events.Clear(); world.Despawn();
            var position = player.Position;
            Advance(world, scenario, 62);
            Check(!player.MechanicInputLock && !player.IsForcedMoving && player.Position == position, "Reset releases player input and stops a partial rewind");
        }
        var failed = new SimWorld { FailEnemySpawns = true };
        var missing = new FruUltimateRelativityScenario(); missing.Run(failed, p); Step(failed, missing, 62);
        Check(failed.Spawned.Count == 0, "Missing boss safely aborts the scenario");
        Console.WriteLine("PASS: recorded Return positions, failed mechanics, seven interrupted reset windows and missing actors.");
    }
    private static UltimateRelativityPattern Pattern(bool ice = true, int north = 0, int mask = 255)
        => new(PartyRole.CasterDps, PartyRole.MeleeDpsB, PartyRole.ShieldHealer, PartyRole.RegenHealer, ice, north, mask, [0, 0, 0], PartyRole.MainTank);
    private static (SimWorld, FruUltimateRelativityScenario) NewRun(UltimateRelativityPattern p)
    {
        var world = new SimWorld();
        foreach (var role in UltimateRelativityPattern.Roles) world.Party.Members.Add(new SimPartyNpc { Role = role });
        var scenario = new FruUltimateRelativityScenario(); scenario.Run(world, p); return (world, scenario);
    }
    private static void Step(SimWorld world, FruUltimateRelativityScenario scenario, float delta)
    { world.Events.Tick(delta); foreach (var m in world.Party.Members) m.Advance(delta); scenario.Tick(delta, world.Events.Elapsed); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
