using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.Apocalypse;
using static AnoMech.Scenarios.Fru.Apocalypse.ApocalypseConstants;

internal static class ApocalypseChecks
{
    public static void Run()
    {
        AssignmentChecks();
        PlayerLongWaterFlexChecks();
        FlexedStackChecks();
        var runs = 0;
        for (var rotation = 0; rotation < 4; rotation++)
        foreach (var cw in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        for (var seed = 0; seed < 16; seed++)
        {
            int[] durations = [0, 0, 1, 1, 2, 2, 3, 3];
            new Random(seed).Shuffle(durations);
            var pattern = new ApocalypsePattern(rotation, cw, durations);
            var (world, scenario) = NewRun(pattern);
            var beforeKnockbackMoves = new Dictionary<PartyRole, int>();
            world.Events.Add(47.5f, () => {
                foreach (var member in world.Party.Members)
                    beforeKnockbackMoves[member.Role] = member.Moves.Count;
            });
            world.Events.Add(48.35f, () => {
                var boss = world.Spawned.OfType<SimEnemy>().First().Position;
                foreach (var member in world.Party.Members)
                {
                    Check(member.Moves.Count == beforeKnockbackMoves[member.Role], "Return movement does not interrupt the knockback");
                    Check(MathF.Abs(Vector3.Distance(member.Position, boss) - 23) < 0.01f, "Bots complete the full 21-yalm knockback from their two-yalm setup");
                }
            });
            world.Events.Add(52, () => {
                var boss = world.Spawned.OfType<SimEnemy>().First().Position;
                var inward = Vector3.Normalize(-boss);
                var side = new Vector3(inward.Z, 0, -inward.X);
                foreach (var member in world.Party.Members)
                {
                    var offset = member.Position - boss;
                    Check(Vector3.Distance(member.Position, boss) < 4.5f, "Bots return close to the boss after knockback");
                    Check(MathF.Abs(Vector3.Dot(offset, inward) - 2) < 0.01f, "Both return stacks stay on the arena-facing side of the boss");
                    Check(MathF.Abs(Vector3.Dot(offset, side) - (pattern.SupportGroup(member.Role) ? -4 : 4)) < 0.01f, "Adjusted water groups return on their respective sides");
                }
            });
            var playerRole = (PartyRole)(seed % 8);
            var trace = new List<Vector3>();
            for (var frame = 0; frame < 58 * fps; frame++)
            {
                if (fps == 30 && seed < 8) trace.Add(world.Party.Get(playerRole)!.Position);
                Step(world, scenario, 1f / fps);
                if (world.Events.Elapsed >= 43.2f && world.Events.Elapsed < 43.3f)
                {
                    var tank = world.Party.Get(PartyRole.OffTank)!;
                    foreach (var member in world.Party.Members.Where(m => m.Role != PartyRole.OffTank))
                    {
                        Check(member.Position.Length() < 0.01f, "Non-baiting bots reach the middle before Darkest Dance");
                        Check(Vector3.Distance(member.Position, tank.Position) > DanceRadius, "Center group clears the baited tank buster");
                        Check(member.Damage.All(d => d.Action != DanceHit), "Only OT receives the baited tank buster");
                    }
                    Check(tank.Damage.Count(d => d.Action == DanceHit) == 1, "OT retains the farthest-player bait");
                }
                foreach (var member in world.Party.Members)
                {
                    var fail = member.Damage.FirstOrDefault(d => d.Lethal);
                    Check(!member.Dead, $"Apoc rot={rotation} cw={cw} fps={fps} seed={seed} t={world.Events.Elapsed:F2} {member.Role} at {member.Position}: {fail.Action} {fail.Cause}");
                    Check(member.Position.Length() <= 20, $"Apoc outside arena t={world.Events.Elapsed:F2}: {member.Role} {member.Position}");
                }
            }
            Check(world.Spawned.All(s => !s.IsActive), "Scenario releases all native actors and lights");
            Check(world.Party.Members.All(m => m.Statuses.Count == 0 && m.ActiveActorVfx.Count == 0), "Scenario clears water and markers");
            Check(world.Party.Members.All(m => m.Damage.Count(d => d.Action == Water) == 3), "Each member shares three four-person water stacks, including during the return to the boss");
            var casts = world.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).ToArray();
            Check(casts.Count(c => c.Action == ApocHit) == 32, "Six waves have 3/5/6/6/6/6 explosions");
            Check(casts.Count(c => c.Action == Water) == 6 && casts.Count(c => c.Action == Eruption) == 8, "Six native stacks and eight spreads");
            Check(casts.Count(c => c.Action == DanceHit) == 1 && casts.Count(c => c.Action == Knockback) == 1, "One bait and one knockback");
            var lights = world.Spawned.OfType<SimOmen>().Where(e => e.Path == LightVfx).ToArray();
            Check(lights.Count(e => e.StartTrigger == PulseTrigger) == 32, "Every warning circle uses its native ring pulse");
            Check(lights.Count(e => e.StartTrigger == StraightTrigger) == 4, "Two pairs travel out from center");
            Check(lights.Count(e => e.StartTrigger == (cw ? ClockwiseTrigger : CounterclockwiseTrigger)) == 24, "Native arcs follow the chosen rotation");
            Check(lights.All(e => e.Scale == Vector3.One), "Native lights retain authored dimensions");
            if (trace.Count > 0)
            {
                var (manual, manualScenario) = NewRun(pattern, playerRole);
                foreach (var position in trace)
                {
                    // External recorded inputs stand in for a human following
                    // the route. Production AI must never issue this movement.
                    manual.Party.Get(playerRole)!.Position = position;
                    Step(manual, manualScenario, 1f / fps);
                    Check(manual.Party.Members.All(m => !m.Dead), "Manual player route works in every party role and rotation");
                }
                Check(manual.Party.Get(playerRole)!.Moves.Count == 0, "Player route remains entirely external to AI");
            }
            runs++;
        }
        FailuresAndReset();
        CheckWaterMarkerPhases();
        CheckPausedMarkerOwnership();
        Console.WriteLine($"PASS: Apocalypse; 2520 water assignments; {runs} bot runs; 64 manual-player runs; native requests, stacks, spreads, tank bait, knockback, cleanup and failure checks.");
    }
    private static void AssignmentChecks()
    {
        var count = 0;
        var a = new int[8];
        void Visit(int index)
        {
            if (index != 8)
            {
                for (var duration = 0; duration < 4; duration++)
                    if (a.Take(index).Count(d => d == duration) < 2) { a[index] = duration; Visit(index + 1); }
                return;
            }
            var p = new ApocalypsePattern(0, true, a);
            Check(ApocalypsePattern.Roles.Select(r => p.Slot(r)).Distinct().Count() == 8, "Swaps preserve every party slot");
            foreach (var group in new[] { true, false })
                Check(ApocalypsePattern.Roles.Where(r => p.SupportGroup(r) == group).Select(p.Duration).Order().SequenceEqual(new[] { 0, 1, 2, 3 }), "One of every duration on each side");
            count++;
        }
        Visit(0);
        Check(count == 2520, "All unique water assignments checked");
        // Independent north-start fixtures from apoc_lights.gd, in octant order.
        int[][] octants = [[0, 4], [0, 4, 1, 5], [0, 4, 1, 5, 2, 6], [1, 5, 2, 6, 3, 7], [2, 6, 3, 7, 4, 0], [3, 7, 4, 0, 5, 1]];
        var pattern = new ApocalypsePattern(0, true, [0, 1, 2, 3, 0, 1, 2, 3]);
        for (var wave = 0; wave < 6; wave++)
        {
            var expected = octants[wave].Select(o => new Vector3(MathF.Sin(o * MathF.PI / 4), 0, -MathF.Cos(o * MathF.PI / 4)) * 14).ToList();
            if (wave < 2) expected.Add(Vector3.Zero);
            var actual = pattern.Explosions(wave).ToArray();
            Check(actual.Length == expected.Count && expected.All(e => actual.Any(v => Vector3.Distance(e, v) < 0.001f)), "Explosion centers match independent reference wave table");
        }
    }
    private static void PlayerLongWaterFlexChecks()
    {
        for (var rotation = 0; rotation < 4; rotation++)
        foreach (var clockwise in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        {
            // PLD/OT and SCH have long water, so OT flexes to DPS.
            // WAR has no water and stays with supports. R1 swaps with OT.
            var pattern = new ApocalypsePattern(rotation, clockwise, [0, 3, 1, 3, 0, 1, 2, 2]);
            var (rehearsal, rehearsalScenario) = NewRun(pattern);
            var opening = new List<Vector3>();
            for (var frame = 0; frame < 45 * fps + 1; frame++)
            {
                opening.Add(rehearsal.Party.Get(PartyRole.OffTank)!.Position);
                Step(rehearsal, rehearsalScenario, 1f / fps);
            }
            var (world, scenario) = NewRun(pattern, PartyRole.OffTank);
            var player = world.Party.Get(PartyRole.OffTank)!;
            for (var frame = 0; frame < 52 * fps; frame++)
            {
                var time = world.Events.Elapsed;
                if (time < 45)
                    player.Position = opening[frame];
                else if (time < 47.6f || time >= 48.4f)
                {
                    // Independent human route: face the boss from center and
                    // use the RIGHT (DPS) side, never a production stack helper.
                    var boss = world.Spawned.OfType<SimEnemy>().First().Position;
                    var facing = Vector3.Normalize(boss);
                    var right = new Vector3(-facing.Z, 0, facing.X);
                    var target = time < 47.6f ? boss - facing * MathF.Sqrt(3) + right
                        : boss - facing * 2 + right * 4;
                    var offset = target - player.Position;
                    var distance = offset.Length();
                    if (distance > 0) player.Position += offset * (MathF.Min(distance, RunSpeed / fps) / distance);
                }
                Step(world, scenario, 1f / fps);
                Check(world.Party.Members.All(m => !m.Dead),
                    $"PLD long-water flex right survives: rot={rotation} cw={clockwise} fps={fps} t={world.Events.Elapsed:F2}");
            }
            var oracle = world.Spawned.OfType<SimEnemy>().First().Position;
            var direction = Vector3.Normalize(oracle);
            var playerRight = new Vector3(-direction.Z, 0, direction.X);
            Check(Vector3.Dot(world.Party.Get(PartyRole.MainTank)!.Position - oracle, playerRight) < -3.9f,
                "Non-flexing WAR stays on the player's left with supports");
            Check(Vector3.Dot(player.Position - oracle, playerRight) > 3.9f, "Flexing PLD returns on the player's right with DPS");
            Check(player.Damage.Count(d => d.Action == Water) == 3 && player.Moves.Count == 0,
                "Manual PLD shares all three stacks without AI movement");
        }
        Console.WriteLine("PASS: Apocalypse PLD long-water flex right; 24 independent manual final-stack routes; WAR stays support-side.");
    }
    private static void FlexedStackChecks()
    {
        // Independent fixtures: expected support-side members after NA flex.
        // Cover each water duration and two simultaneous swap pairs.
        (int[] Durations, int[] Support)[] cases = [
            ([1, 1, 0, 2, 2, 0, 3, 3], [1, 2, 3, 6]), // MT <-> R1
            ([0, 2, 2, 1, 0, 1, 3, 3], [0, 2, 3, 6]), // OT <-> R1
            ([0, 1, 3, 3, 0, 1, 2, 2], [0, 1, 3, 6]), // H1 <-> R1
            ([0, 0, 1, 1, 2, 2, 3, 3], [1, 3, 4, 6])  // MT <-> M1, H1 <-> R1
        ];
        foreach (var fixture in cases)
        for (var rotation = 0; rotation < 4; rotation++)
        foreach (var clockwise in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var support = fixture.Support.Select(i => ApocalypsePattern.Roles[i]).ToHashSet();
            var pattern = new ApocalypsePattern(rotation, clockwise, fixture.Durations);
            var (world, scenario) = NewRun(pattern);
            foreach (var duration in new[] { 1, 2, 3 })
            {
                var order = duration;
                world.Events.Add(ApocalypsePattern.WaterTime(order) - 0.05f, () => {
                    foreach (var target in world.Party.Members.Where(m => pattern.Duration(m.Role) == order))
                    {
                        var expected = ApocalypsePattern.Roles.Where(r => support.Contains(r) == support.Contains(target.Role)).ToHashSet();
                        var actual = world.Party.Members.Where(m => Vector3.Distance(m.Position, target.Position) <= WaterRadius).Select(m => m.Role);
                        Check(expected.SetEquals(actual), $"Water {order} contains the four independently expected flexed roles");
                    }
                });
            }
            while (world.Events.Elapsed < 52) Step(world, scenario, 1f / fps);
            var boss = world.Spawned.OfType<SimEnemy>().First().Position;
            var inward = Vector3.Normalize(-boss);
            var side = new Vector3(inward.Z, 0, -inward.X);
            foreach (var member in world.Party.Members)
            {
                Check(!member.Dead && member.Damage.Count(d => d.Action == Water) == 3, "Flexed bots survive and share all three water stacks");
                var expectedSide = support.Contains(member.Role) ? -4 : 4;
                Check(MathF.Abs(Vector3.Dot(member.Position - boss, side) - expectedSide) < 0.01f,
                    "Return to boss preserves the independently expected flexed side");
            }
        }
        Console.WriteLine("PASS: Apocalypse flex; 96 runs with explicit short/medium/long-water and double-swap fixtures; all three stacks and post-knockback sides.");
    }
    private static (SimWorld, FruApocalypseScenario) NewRun(ApocalypsePattern pattern, PartyRole? player = null)
    {
        var world = new SimWorld();
        foreach (var role in ApocalypsePattern.Roles)
            world.Party.Members.Add(role == player ? new SimPlayer { Role = role } : new SimPartyNpc { Role = role });
        var scenario = new FruApocalypseScenario();
        scenario.Run(world, pattern);
        return (world, scenario);
    }
    private static void Step(SimWorld world, FruApocalypseScenario scenario, float delta = 1f / 60)
    {
        world.Events.Tick(delta);
        foreach (var member in world.Party.Members) member.Advance(delta);
        scenario.Tick(delta, world.Events.Elapsed);
    }
    private static void Until(SimWorld world, FruApocalypseScenario scenario, float time)
    { while (world.Events.Elapsed < time) Step(world, scenario); }
    private static void FailuresAndReset()
    {
        var p = new ApocalypsePattern(0, true, [0, 1, 2, 3, 0, 1, 2, 3]);
        foreach (var role in ApocalypsePattern.Roles)
        {
            var (world, scenario) = NewRun(p, role);
            Until(world, scenario, 56);
            Check(world.Party.Get(role)!.Moves.Count == 0, "AI never moves player role");
        }
        foreach (var time in new[] { 10f, 24f, 34f, 46f, 48f })
        {
            var (world, scenario) = NewRun(p);
            Until(world, scenario, time);
            world.Events.Clear(); world.Despawn();
            Until(world, scenario, 60);
            Check(world.Spawned.Count == 0 && world.Party.Members.All(m => m.ActiveActorVfx.Count == 0 && m.Statuses.Count == 0), "Reset cancels events and cleans effects");
        }
        var (bad, badScenario) = NewRun(p);
        Until(bad, badScenario, 22.9f);
        bad.Party.Get(PartyRole.MainTank)!.StopMoving();
        bad.Party.Get(PartyRole.MainTank)!.Position = Vector3.Zero;
        Until(bad, badScenario, 23.2f);
        Check(bad.Party.Members.Any(m => m.Damage.Any(d => d.Action == Water && d.Lethal)), "Underfilled or overlapping stacks fail");
        foreach (var (time, action) in new[] { (33.2f, ApocHit), (35.6f, Eruption), (43.1f, DanceHit) })
        {
            var (world, scenario) = NewRun(p);
            Until(world, scenario, time);
            var member = world.Party.Get(PartyRole.MeleeDpsA)!;
            member.StopMoving();
            member.Position = action == ApocHit ? Vector3.Zero : action == Eruption
                ? world.Party.Get(PartyRole.MeleeDpsB)!.Position : new(0, 0, 19);
            Until(world, scenario, time + 0.2f);
            Check(member.Damage.Any(d => d.Action == action && d.Lethal), "Bad explosion/spread/non-tank bait is lethal");
        }
        var (deadTarget, deadScenario) = NewRun(p);
        Until(deadTarget, deadScenario, 22.9f);
        deadTarget.Party.Get(PartyRole.OffTank)!.Dead = true;
        Until(deadTarget, deadScenario, 23.2f);
        Check(deadTarget.Party.Members.Where(m => m.Role != PartyRole.OffTank).All(m => m.Damage.Any(d => d.Action == Water && d.Lethal)), "Missing water target fails the party");
        var (missing, missingScenario) = NewRun(p);
        missing.FailEnemySpawns = true;
        Until(missing, missingScenario, 58);
        Check(missing.Spawned.Count == 0, "Missing boss never creates mechanics");
    }

    private static void CheckWaterMarkerPhases()
    {
        // Assert actual resource paths, independently of the production constants,
        // so swapping the stack and countdown resources cannot pass unnoticed.
        const string stack = "vfx/lockon/eff/com_share0c.avfx";
        const string countdown = "vfx/lockon/eff/m0581trg_dice0h.avfx";
        const string waiting = "vfx/common/eff/d1049_stlp_b0k1.avfx";
        var pattern = new ApocalypsePattern(0, true, [0, 1, 2, 3, 0, 1, 2, 3]);
        var (world, scenario) = NewRun(pattern);
        Until(world, scenario, 7.1f);
        Check(world.Party.Members.All(m => m.ActorVfx.Count == 0), "No water markers before the application cast");
        Until(world, scenario, 7.3f);
        foreach (var member in world.Party.Members)
        {
            var expected = pattern.Duration(member.Role) == 0 ? 0 : 1;
            Check(member.ActorVfx.Count(v => v.Path == stack && !v.Persistent) == expected,
                "Water recipients get the stack marker before debuff application");
            Check(member.ActorVfx.All(v => v.Path != countdown), "Opening markers do not use the countdown");
        }
        Until(world, scenario, 12.7f);
        Check(world.Party.Members.All(m => !m.ActiveActorVfx.Contains(waiting)), "Waiting clocks do not precede debuff application");
        Until(world, scenario, 12.9f);
        foreach (var member in world.Party.Members)
        {
            var hasWater = pattern.Duration(member.Role) != 0;
            Check(member.ActiveActorVfx.Contains(waiting) == hasWater && member.HasStatus(WaterStatus) == hasWater,
                "Only water recipients get an owned waiting clock with their debuff");
        }
        foreach (var duration in new[] { 1, 2, 3 })
        {
            var start = ApocalypsePattern.WaterTime(duration) - 5.1f;
            Until(world, scenario, start - 0.1f);
            foreach (var member in world.Party.Members.Where(m => pattern.Duration(m.Role) == duration))
            {
                Check(member.ActorVfx.All(v => v.Path != countdown), "Water countdown waits for its own final five-second window");
                Check(member.ActiveActorVfx.Contains(waiting), "Waiting clock persists until its recipient's countdown");
            }
            Until(world, scenario, start + 0.1f);
            foreach (var member in world.Party.Members)
            {
                var order = pattern.Duration(member.Role);
                Check(member.ActiveActorVfx.Contains(waiting) == (order > duration), "Waiting clock hands off independently to each pair's countdown");
                Check(member.ActorVfx.Count(v => v.Path == waiting && v.Persistent) == (order == 0 ? 0 : 1),
                    "Waiting clock is spawned once, without replaying or respawning");
                Check(member.ActorVfx.Count(v => v.Path == countdown && !v.Persistent) == (order > 0 && order <= duration ? 1 : 0),
                    "Each water pair receives exactly one countdown in its own resolution window");
                Check(member.ActorVfx.Count(v => v.Path == stack) == (order == 0 ? 0 : 1),
                    "Stack marker is not replayed before water resolution");
            }
        }
        foreach (var role in new[] { PartyRole.ShieldHealer, PartyRole.CasterDps })
        {
            var (cleanup, cleanupScenario) = NewRun(pattern, role);
            Until(cleanup, cleanupScenario, 13);
            var member = cleanup.Party.Get(role)!;
            Check(member.ActiveActorVfx.Contains(waiting), "Manual player receives the waiting clock");
            member.Dead = true;
            Step(cleanup, cleanupScenario);
            Check(!member.ActiveActorVfx.Contains(waiting), "Death removes the manual player's waiting clock");
            var bot = cleanup.Party.Get(PartyRole.MeleeDpsB)!;
            bot.RemoveStatus(WaterStatus);
            Step(cleanup, cleanupScenario);
            Check(!bot.ActiveActorVfx.Contains(waiting), "Losing the water debuff removes its waiting clock");
            cleanup.Events.Clear();
            cleanup.Despawn();
            Check(cleanup.Party.Members.All(m => m.ActiveActorVfx.Count == 0), "Reset clears the other players' owned waiting clocks");
        }
        Console.WriteLine("PASS: Apocalypse water VFX; opening stack, waiting clocks, three countdown handoffs, death/debuff loss/reset cleanup.");
    }
    private static void CheckPausedMarkerOwnership()
    {
        var pattern = new ApocalypsePattern(0, true, [0, 1, 2, 3, 0, 1, 2, 3]);
        string[] finiteMarkers = [WaterMarker, WaterClock, EruptionMarker];
        // Stop at each marker window, including the long-water clock present
        // after Darkest Dance deaths. Native VFX keep aging while Events/Tick
        // are paused. We must hold no managed removal handle at these points.
        foreach (var time in new[] { 8f, 19f, 31f, 38f, 48f, 58f })
        {
            var (world, scenario) = NewRun(pattern);
            Until(world, scenario, time);
            var spawned = world.Party.Members.SelectMany(m => m.ActorVfx).Where(v => finiteMarkers.Contains(v.Path)).ToArray();
            Check(spawned.Length > 0 && spawned.All(v => !v.Persistent), "Finite native markers are game-owned before a pause");
            Check(world.Party.Members.All(m => !m.ActiveActorVfx.Overlaps(finiteMarkers)), "Reset has no owned marker handle to destroy after native completion");
            Check(world.Party.Members.All(m => !m.RemovedActorVfx.Any(finiteMarkers.Contains)), "Resolution/completion never manually removes a self-expiring marker");
            world.Events.Clear();
            world.Despawn();
            world.Despawn();
            Check(world.Party.Members.All(m => m.ActiveActorVfx.Count == 0), "Repeated reset leaves no owned markers");
        }
        Console.WriteLine("PASS: Apocalypse finite marker ownership at six pause/reset windows; no persistent handles or explicit marker removal.");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
