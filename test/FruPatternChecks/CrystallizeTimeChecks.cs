using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.CrystallizeTime;

internal static class CrystallizeTimeChecks
{
    public static void Run()
    {
        var runs = 0;
        foreach (var slowNorthwest in new[] { true, false })
        foreach (var corner in Enum.GetValues<CrystallizeTimeCorner>())
        foreach (var fps in new[] { 15, 30, 60 })
        for (var seed = 0; seed < 16; seed++)
        {
            var world = new SimWorld();
            var roles = Enum.GetValues<PartyRole>();
            var assignment = CrystallizeTimePattern.Randomize(new Random(seed), false);
            var pattern = new CrystallizeTimePattern(slowNorthwest, corner, assignment.Roles, assignment.Quietus, (PartyRole)(seed % 8));
            foreach (var role in roles) world.Party.Members.Add(new SimPartyNpc { Role = role });
            var scenario = new FruCrystallizeTimeScenario();
            scenario.Run(world, pattern);
            for (var frame = 1; frame <= 68 * fps; frame++)
            {
                world.Events.Tick(1f / fps);
                foreach (var member in world.Party.Members) member.Advance(1f / fps);
                scenario.Tick(1f / fps, frame / (float)fps);
                Check(world.Spawned.OfType<SimEnemy>().Where(e => e.IsActive && e.Config.BNpcBaseId == CrystallizeTimeConstants.Usurper)
                    .All(e => e.Visible), "Usurper remains drawable so native animations control the teleport fades");
                foreach (var member in world.Party.Members)
                {
                    var fail = member.Damage.FirstOrDefault(d => d.Lethal);
                    Check(!member.Dead, $"CT seed={seed} slowNW={slowNorthwest} {corner} fps={fps} time={world.Events.Elapsed:F2} {pattern.Assignment(member.Role)} {member.Position}: {fail.Action} {fail.Cause}");
                    Check(member.Position.Length() <= 20, $"CT outside arena {world.Events.Elapsed:F2} {pattern.Assignment(member.Role)} {member.Position}");
                }
            }
            Check(world.Spawned.All(s => !s.IsActive), "CT owns and cleans all actors, native scenery, tethers and puddles");
            Check(world.Map.Effects.Count(e => e == (AnoMech.Scenarios.Fru.FruConstants.MapEffect.Show, (byte)46)) == 1
                && world.Map.Effects.Contains((AnoMech.Scenarios.Fru.FruConstants.MapEffect.Hide, (byte)46)),
                "CT shows the native north crystal and hides it on completion");
            Check(world.Party.Members.All(m => m.Statuses.Count == 0), "CT cleans statuses");
            Check(world.Party.Members.All(m => m.ActorVfx.Count(v => v.Path == CrystallizeTimeConstants.ReturnClockVfx && v.Persistent) == 1
                && m.ActiveActorVfx.Count == 0), "All eight members receive and clean the native rewind clock");
            Check(world.Spawned.OfType<SimOmen>().Count(o => o.Path == CrystallizeTimeConstants.ReturnMarkerVfx) == 8,
                "Eight native saved-position floor traces");
            Check(world.Spawned.OfType<SimEventObject>().Count() == 4, "Four native cleansing puddles");
            var heads = world.Spawned.OfType<SimEnemy>().Where(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon).ToArray();
            Check(heads.Length == 2 && heads.All(e => e.Scale == CrystallizeTimeConstants.DragonAfterFirstHitScale),
                "Dragon interception shrinks both existing actors without deleting/reusing their native slots");
            foreach (var member in world.Party.Members)
                Check(member.Damage.Count(d => d.Action is CrystallizeTimeConstants.AkhHitUsurper or CrystallizeTimeConstants.AkhHitOracle) == 4,
                    "All roles take four successful 7-1 Akh Morn hits");
            var casts = world.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).ToArray();
            Check(casts.Count(c => c.Action == CrystallizeTimeConstants.Maelstrom) == 6, "Six native hourglass casts");
            Check(casts.Count(c => c.Action is CrystallizeTimeConstants.TidalFirst or CrystallizeTimeConstants.TidalRest) == 8, "Eight native tidal lines");
            Check(casts.Count(c => c.Action == CrystallizeTimeConstants.Return) == 8 && casts.Count(c => c.Action == CrystallizeTimeConstants.ReturnIV) == 8,
                "Eight native Return traces and returns");
            runs++;
        }
        CheckPlayerControl();
        CheckWingsPresentation();
        CheckPatternSelection();
        CheckRewindMarkers();
        CheckFragmentScenery();
        CheckDragonStages();
        CheckDragonInitialCooldown();
        CheckDragonRetrigger();
        CheckPuddleCreatorExclusion();
        CheckSpiritSpread();
        CheckFailures();
        Console.WriteLine($"PASS: Crystallize Time; {runs} full bot runs; 320 manually controlled player runs; all player patterns/roles; negative damage/cleanse/rewind/stack/reset checks; NA 7-1 Akh Morn.");
    }

    private static (SimWorld World, FruCrystallizeTimeScenario Scenario) NewRun(CrystallizeTimePattern pattern, PartyRole? playerRole = null)
    {
        var world = new SimWorld();
        foreach (var role in Enum.GetValues<PartyRole>())
            world.Party.Members.Add(role == playerRole ? new SimPlayer { Role = role } : new SimPartyNpc { Role = role });
        var scenario = new FruCrystallizeTimeScenario();
        scenario.Run(world, pattern);
        return (world, scenario);
    }
    private static void Step(SimWorld world, FruCrystallizeTimeScenario scenario, float delta = 1f / 60)
    {
        world.Events.Tick(delta);
        foreach (var member in world.Party.Members) member.Advance(delta);
        scenario.Tick(delta, world.Events.Elapsed);
    }
    private static void Until((SimWorld World, FruCrystallizeTimeScenario Scenario) run, float time)
    { while (run.World.Events.Elapsed < time) Step(run.World, run.Scenario); }
    private static void Put(SimCharacter member, Vector3 position) { member.StopMoving(); member.Position = position; }

    private static void CheckRewindMarkers()
    {
        var run = NewRun(CrystallizeTimePattern.Randomize(new Random(0), false));
        Until(run, 6.4f);
        Check(run.World.Party.Members.All(m => m.ActorVfx.Count == 0), "No clocks before debuff assignment");
        foreach (var time in new[] { 6.7f, 12f, 25f, 34.4f })
        {
            Until(run, time);
            Check(run.World.Party.Members.All(m => m.HasStatus(CrystallizeTimeConstants.ReturnWaitingStatus)
                && m.ActiveActorVfx.Contains(CrystallizeTimeConstants.ReturnClockVfx) && m.ActorVfx.Count == 1),
                "Clock starts with the waiting debuff and persists without respawning");
        }
        var dead = run.World.Party.Get(PartyRole.CasterDps)!;
        dead.Dead = true;
        Until(run, 34.7f);
        Check(dead.ActiveActorVfx.Count == 0, "Death removes the clock");
        Check(!run.World.Spawned.OfType<SimOmen>().Any(), "No saved-position floor traces before the snapshot");
        var dying = run.World.Party.Get(PartyRole.PhysRangedDps)!;
        dying.Dead = true;
        Step(run.World, run.Scenario);
        Check(dying.ActiveActorVfx.Count == 0, "Death removes an active countdown");
        Until(run, 39.5f);
        Check(run.World.Party.Members.Where(m => !m.Dead).All(m => m.ActiveActorVfx.Contains(CrystallizeTimeConstants.ReturnClockVfx)),
            "Clocks remain until just before positions are saved");
        var positions = run.World.Party.Members.Where(m => !m.Dead).Select(m => m.Position).ToArray();
        Until(run, 39.7f);
        Check(run.World.Party.Members.All(m => m.ActiveActorVfx.Count == 0), "Snapshot removes every countdown");
        var markers = run.World.Spawned.OfType<SimOmen>().ToArray();
        Check(markers.Length == positions.Length && markers.All(m => m.Path == CrystallizeTimeConstants.ReturnMarkerVfx && m.Scale == Vector3.One && m.IsActive),
            "Each living member gets an unmodified native world-space trace");
        Check(markers.Select(m => m.Placement.Position).SequenceEqual(positions), "Floor traces use actual snapshot positions");
        Until(run, 42f);
        Check(markers.All(m => m.IsActive) && markers.Select(m => m.Placement.Position).SequenceEqual(positions),
            "Saved traces stay fixed while the party moves to spread");
        Check(run.World.Party.Members.Where(m => !m.Dead).Select(m => m.Position).Zip(positions).Any(p => Vector3.Distance(p.First, p.Second) > 1),
            "Party actually moves away from the saved traces");
        Until(run, 42.8f);
        Check(markers.All(m => !m.IsActive), "Trace cleanup matches the reference Wings transition");
        var earlyDeath = NewRun(CrystallizeTimePattern.Randomize(new Random(0), false));
        var absent = earlyDeath.World.Party.Get(PartyRole.CasterDps)!;
        absent.Dead = true;
        Until(earlyDeath, 6.7f);
        Check(absent.ActorVfx.Count == 0, "Dead members do not receive a clock at assignment");
        earlyDeath.World.Despawn();
        foreach (var stopTime in new[] { 7f, 35f, 40f })
        {
            var reset = NewRun(CrystallizeTimePattern.Randomize(new Random(0), false));
            Until(reset, stopTime);
            var objects = reset.World.Spawned.ToArray();
            reset.World.Despawn();
            Check(objects.All(o => !o.IsActive) && reset.World.Party.Members.All(m => m.ActiveActorVfx.Count == 0),
                "Reset owns both clock and trace lifetimes");
        }
    }

    private static void CheckFragmentScenery()
    {
        var pattern = CrystallizeTimePattern.Randomize(new Random(0), false);
        var run = NewRun(pattern);
        Until(run, 1.2f);
        var anchor = run.World.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Fragment);
        Check(anchor.Position == new Vector3(0, 0, -16), "Fragment anchor matches the native north crystal's placement");
        Check(anchor.Config.Targetable && !anchor.Config.IsHostile && anchor.Config.EnemyList == EnemyListMode.Never
            && anchor.Health == CrystallizeTimeConstants.FragmentMaxHealth && anchor.MaxHealth == anchor.Health,
            "Fragment is a friendly target with native HP, outside the enemy list");
        var visions = run.World.Spawned.OfType<SimEnemy>().Where(e => e.Config.BNpcBaseId is
            CrystallizeTimeConstants.VisionOfRyne or CrystallizeTimeConstants.VisionOfGaia).ToArray();
        Check(visions.Length == 2 && visions.All(v => Vector2.Distance(new(v.Position.X, v.Position.Z), new(anchor.Position.X, anchor.Position.Z)) < 1
            && v.Position.Y == anchor.Position.Y && v.VisualHeight == CrystallizeTimeConstants.FragmentVisionHeight
            && !v.Config.Targetable && !v.Config.IsHostile), "Both native Ryne/Gaia actors are inside the crystal");
        Check(run.World.Map.Effects.Contains((AnoMech.Scenarios.Fru.FruConstants.MapEffect.Show, (byte)46))
            && !run.World.Map.Effects.Contains((AnoMech.Scenarios.Fru.FruConstants.MapEffect.Show, (byte)41)),
            "Fragment activates crystal scenery rather than the unrelated center circle");
        Until(run, 55.6f);
        Check(visions.All(v => v.IsActive && v.Position.Y == anchor.Position.Y
            && v.Position.Y + v.VisualHeight == anchor.Position.Y + CrystallizeTimeConstants.FragmentVisionHeight),
            "Both visions request native draw elevation through rewind while their actors remain grounded");
        run.World.Events.Clear();
        run.World.Despawn();
        Check(run.World.Map.Effects.Contains((AnoMech.Scenarios.Fru.FruConstants.MapEffect.Hide, (byte)46)),
            "Early reset hides the native crystal");
        Check(visions.All(v => !v.IsActive) && !anchor.IsActive, "Reset removes both visions and the fragment HP actor");

        // Eruption at the crystal must fail; the same radius at arena center
        // must not hit an imaginary crystal left at the old anchor position.
        foreach (var hitCrystal in new[] { true, false })
        {
            run = NewRun(pattern);
            Until(run, 20.4f);
            Put(run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Eruption))!, hitCrystal ? new(0, 0, -16) : Vector3.Zero);
            Until(run, 20.7f);
            anchor = run.World.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Fragment);
            Check(anchor.Health == (hitCrystal ? 0u : CrystallizeTimeConstants.FragmentMaxHealth),
                "Fragment HP follows the existing fatal crystal-hit rule");
            Check(run.World.Party.Members.Any(m => m.Damage.Any(d => d.Lethal && d.Cause == "Fragment of Fate was hit")) == hitCrystal,
                "Fragment protection resolves at the visible crystal, not arena center");
            run.World.Despawn();
        }
    }

    private static void CheckPatternSelection()
    {
        PartyRole[] priority = [PartyRole.ShieldHealer, PartyRole.RegenHealer, PartyRole.OffTank, PartyRole.MainTank,
            PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
        var blueAssignments = new HashSet<CrystallizeTimeAssignment>();
        foreach (var role in Enum.GetValues<PartyRole>())
        foreach (var choice in Enum.GetValues<CrystallizeTimePlayerPattern>())
        for (var seed = 0; seed < 64; seed++)
        {
            var pattern = CrystallizeTimePattern.Randomize(new Random(seed), false, role, choice);
            var assignment = pattern.Assignment(role);
            Check(choice switch {
                CrystallizeTimePlayerPattern.Random => true,
                CrystallizeTimePlayerPattern.RedIce => assignment is CrystallizeTimeAssignment.IceWest or CrystallizeTimeAssignment.IceEast,
                CrystallizeTimePlayerPattern.RedAero => assignment is CrystallizeTimeAssignment.AeroWest or CrystallizeTimeAssignment.AeroEast,
                CrystallizeTimePlayerPattern.BlueDark => assignment == CrystallizeTimeAssignment.Darkness,
                CrystallizeTimePlayerPattern.BlueStack => assignment is CrystallizeTimeAssignment.Ice or CrystallizeTimeAssignment.Water or CrystallizeTimeAssignment.Eruption,
                _ => false,
            }, $"Selected CT debuffs {choice} for {role}");
            Check(pattern.Roles.Distinct().Count() == 8 && pattern.Roles.Contains(role), "Selection preserves all eight roles");
            foreach (var pair in new[] { (CrystallizeTimeAssignment.AeroWest, CrystallizeTimeAssignment.AeroEast), (CrystallizeTimeAssignment.IceWest, CrystallizeTimeAssignment.IceEast) })
                Check(Array.IndexOf(priority, pattern.Role(pair.Item1)) < Array.IndexOf(priority, pattern.Role(pair.Item2)),
                    "Forced player debuffs retain NA west/east priority");
            if (choice == CrystallizeTimePlayerPattern.BlueStack) blueAssignments.Add(assignment);
            if (choice == CrystallizeTimePlayerPattern.Random)
            {
                var original = CrystallizeTimePattern.Randomize(new Random(seed), false);
                Check(pattern.Roles.SequenceEqual(original.Roles) && pattern.Quietus.SequenceEqual(original.Quietus)
                    && pattern.Corner == original.Corner && pattern.SlowNorthwest == original.SlowNorthwest
                    && pattern.JumpTarget == original.JumpTarget, "Random preserves existing assignment generation");
            }
        }
        Check(blueAssignments.SetEquals([CrystallizeTimeAssignment.Ice, CrystallizeTimeAssignment.Water, CrystallizeTimeAssignment.Eruption]),
            "Grouped blue choice can produce all three assignments");

        // Exercise the actual Start entry point with every player role. Selection
        // is captured at Start; a setting change during a run applies next time.
        foreach (var role in Enum.GetValues<PartyRole>())
        foreach (var choice in Enum.GetValues<CrystallizeTimePlayerPattern>().Where(c => c != CrystallizeTimePlayerPattern.Random))
        {
            var world = new SimWorld();
            foreach (var slot in Enum.GetValues<PartyRole>())
                world.Party.Members.Add(slot == role ? new SimPlayer { Role = slot } : new SimPartyNpc { Role = slot });
            var scenario = new FruCrystallizeTimeScenario { SelectedPlayerPattern = choice };
            scenario.Run(world, selectedAi: 0);
            var nextChoice = choice == CrystallizeTimePlayerPattern.BlueDark ? CrystallizeTimePlayerPattern.RedAero : CrystallizeTimePlayerPattern.BlueDark;
            scenario.SelectedPlayerPattern = nextChoice;
            Until((world, scenario), 6.7f);
            CheckPlayerStatuses(world.Party.Player!, choice);
            world.Despawn();
            world.Events.Clear();
            scenario.Run(world, selectedAi: 0);
            Until((world, scenario), 6.7f);
            CheckPlayerStatuses(world.Party.Player!, nextChoice);
            world.Despawn();
        }
    }

    private static void CheckPlayerStatuses(SimPlayer player, CrystallizeTimePlayerPattern choice)
    {
        var red = choice is CrystallizeTimePlayerPattern.RedIce or CrystallizeTimePlayerPattern.RedAero;
        Check(player.HasStatus(red ? CrystallizeTimeConstants.ClawStatus : CrystallizeTimeConstants.FangStatus), "Selected claw/fang reaches the player");
        Check(choice switch {
            CrystallizeTimePlayerPattern.RedIce => player.HasStatus(CrystallizeTimeConstants.IceStatus),
            CrystallizeTimePlayerPattern.RedAero => player.HasStatus(CrystallizeTimeConstants.AeroStatus),
            CrystallizeTimePlayerPattern.BlueDark => player.HasStatus(CrystallizeTimeConstants.DarknessStatus),
            CrystallizeTimePlayerPattern.BlueStack => player.HasStatus(CrystallizeTimeConstants.IceStatus) || player.HasStatus(CrystallizeTimeConstants.WaterStatus)
                || player.HasStatus(CrystallizeTimeConstants.EruptionStatus),
            _ => false,
        }, "Selected elemental debuff reaches the player through scenario Start");
    }

    private static void CheckDragonStages()
    {
        var run = NewRun(CrystallizeTimePattern.Randomize(new Random(0), false));
        Until(run, 9.9f);
        Check(!run.World.Spawned.OfType<SimEnemy>().Any(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon),
            "Dragon heads are absent during the scenario opening");
        Until(run, 10.1f);
        Check(run.World.Spawned.OfType<SimTether>().Any()
            && !run.World.Spawned.OfType<SimEnemy>().Any(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon),
            "Tethers appear on schedule without revealing stationary dragon heads");
        Until(run, 14.2f);
        Check(!run.World.Spawned.OfType<SimEnemy>().Any(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon),
            "Heads remain absent until their appearance lead-in");
        var fragmentEffects = run.World.Map.Effects.Where(e => e.Index == CrystallizeTimeConstants.FragmentSlot).ToArray();
        Until(run, 14.4f);
        Check(run.World.Map.Effects.Where(e => e.Index == CrystallizeTimeConstants.FragmentSlot).SequenceEqual(fragmentEffects),
            "Head appearance does not request a burst from the Fragment of Fate");
        var crystal = run.World.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Fragment);
        Check(crystal.Health == CrystallizeTimeConstants.FragmentMaxHealth
            && !run.World.Map.Effects.Contains((AnoMech.Scenarios.Fru.FruConstants.MapEffect.Hide, CrystallizeTimeConstants.FragmentSlot)),
            "Head appearance keeps the crystal intact and does not apply damage");
        var heads = run.World.Spawned.OfType<SimEnemy>().Where(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon).ToArray();
        Check(heads.Length == 2 && heads.All(h => h.Visible && h.Scale == 2f && h.Config.Scale == 2f),
            "Both native heads start large before any interception");
        Check(heads.All(h => h.Config.SpawnTimeline == 4562),
            "Each head requests the native finite appearance burst through its spawn timeline");
        Check(heads.All(h => h.ActorVfx.Count == 0),
            "Appearance does not attach a persistent pulse to either head");
        var initialPositions = heads.Select(h => h.Position).ToArray();
        var initialFacings = heads.Select(h => h.Rotation).ToArray();
        Until(run, 15.2f);
        Check(heads.Where((h, side) => h.Position == initialPositions[side]).Count() == 2,
            "Heads have a brief stationary native appearance before movement");
        Until(run, 15.5f);
        Check(heads.Where((h, side) => Vector3.Distance(h.Position, initialPositions[side]) > 0.1f
            && MathF.Abs(h.Rotation - initialFacings[side]) < 0.1f).Count() == 2,
            "Both heads move promptly after appearing without snapping to a different facing");
        var shrunk = new HashSet<SimEnemy>();
        var vanished = new HashSet<SimEnemy>();
        var previousPuddles = 0;
        while (run.World.Events.Elapsed < 36)
        {
            Step(run.World, run.Scenario);
            foreach (var head in heads)
            {
                Check(head.ActorVfx.Count == 0 && head.Timelines.Count == 0,
                    "Movement and interceptions do not attach a pulse or replay the appearance timeline");
                if (head.Scale == 1f && head.Visible) shrunk.Add(head);
                if (!head.Visible)
                {
                    Check(shrunk.Contains(head), "Head spends time small and visible between its two hits");
                    Check(head.Casts.Count(c => c.Action == CrystallizeTimeConstants.DragonDisappear) == 1,
                        "Second hit dispatches the native disappearance once");
                    vanished.Add(head);
                }
            }
            var puddles = run.World.Spawned.OfType<SimEventObject>().Count();
            Check(puddles == shrunk.Count + vanished.Count, "Each pop advances exactly one head stage and drops one puddle");
            Check(puddles >= previousPuddles && puddles <= 4, "No further pops after the second hit");
            previousPuddles = puddles;
        }
        Check(shrunk.Count == 2 && vanished.Count == 2, "Both heads complete large, small, disappeared stages");
        Check(run.World.Spawned.OfType<SimEnemy>().Count(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon) == 2,
            "Size changes retain the original two native actors");
        run.World.Despawn();
        var reset = NewRun(CrystallizeTimePattern.Randomize(new Random(0), false));
        Until(reset, 14.4f);
        var resetHeads = reset.World.Spawned.OfType<SimEnemy>().Where(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon).ToArray();
        reset.World.Despawn();
        Check(resetHeads.Length == 2 && resetHeads.All(h => !h.IsActive && h.ActiveActorVfx.Count == 0),
            "Early reset removes both appearance actors without leaving persistent effects");
    }

    private static void CheckDragonInitialCooldown()
    {
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var pattern = CrystallizeTimePattern.Randomize(new Random(0), false);
            var west = pattern.Role(CrystallizeTimeAssignment.AeroWest);
            var east = pattern.Role(CrystallizeTimeAssignment.AeroEast);
            var run = NewRun(pattern, west);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Until(run, CrystallizeTimeConstants.DragonSpawnTime + 0.1f);
                var heads = run.World.Spawned.OfType<SimEnemy>().Where(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Dragon).ToArray();
                var initialPositions = heads.Select(h => h.Position).ToArray();
                var initialHits = run.World.Party.Members.Sum(m => m.Damage.Count(d => d.Action == CrystallizeTimeConstants.DragonHit));
                run.World.Events.Clear();
                run.World.Events.Tick(CrystallizeTimeConstants.DragonMovementStart);
                var deadline = CrystallizeTimeConstants.DragonMovementStart + CrystallizeTimeConstants.DragonHitCooldown;
                while (run.World.Events.Elapsed < deadline - 1f / fps)
                {
                    TickDragonContacts(run, 0, (west, 0, Vector3.Zero), (east, 1, Vector3.Zero));
                    Check(!run.World.Spawned.OfType<SimEventObject>().Any(), "Neither head pops during its initial cooldown");
                    Check(heads.All(h => h.Visible && h.Scale == CrystallizeTimeConstants.DragonInitialScale),
                        "Initial overlap leaves both heads large and visible");
                    Check(run.World.Party.Members.Sum(m => m.Damage.Count(d => d.Action == CrystallizeTimeConstants.DragonHit)) == initialHits,
                        "Initial overlap causes no dragon hit or splash");
                    Check(run.World.Party.Get(west)!.HasStatus(CrystallizeTimeConstants.ClawStatus)
                        && run.World.Party.Get(east)!.HasStatus(CrystallizeTimeConstants.ClawStatus),
                        "Initial overlap does not consume Wyrmclaw");
                    run.World.Events.Tick(1f / fps);
                }
                Check(heads.Where((h, side) => Vector3.Distance(h.Position, initialPositions[side]) > 1).Count() == 2,
                    "Both heads keep moving during their initial cooldown");
                run.World.Events.Tick(deadline - 0.001f - run.World.Events.Elapsed);
                TickDragonContacts(run, 0, (west, 0, Vector3.Zero), (east, 1, Vector3.Zero));
                Check(!run.World.Spawned.OfType<SimEventObject>().Any(), "Initial cooldown lasts the full two seconds");
                run.World.Events.Tick(deadline + 0.01f - run.World.Events.Elapsed);
                TickDragonContacts(run, 0, (west, 0, Vector3.Zero), (east, 1, Vector3.Zero));
                Check(run.World.Spawned.OfType<SimEventObject>().Count() == 2
                    && heads.All(h => h.Scale == CrystallizeTimeConstants.DragonAfterFirstHitScale),
                    "Both heads accept their first contact after the initial cooldown");
                run.World.Despawn();
                run.World.Events.Clear();
                if (attempt == 0) run.Scenario.Run(run.World, pattern);
            }
        }
    }

    private static void CheckDragonRetrigger()
    {
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var pattern = CrystallizeTimePattern.Randomize(new Random(0), false);
            var first = pattern.Role(CrystallizeTimeAssignment.AeroWest);
            var second = pattern.Role(CrystallizeTimeAssignment.IceWest);
            (SimWorld World, FruCrystallizeTimeScenario Scenario) Prepare()
            {
                var result = NewRun(pattern, first);
                Until(result, CrystallizeTimeConstants.DragonSpawnTime + 0.1f);
                // Keep production contact/movement logic, isolate it from the
                // other scheduled mechanics and supply deliberate player input.
                result.World.Events.Clear();
                result.World.Events.Tick(19.5f);
                return result;
            }
            int Pops(SimWorld world) => world.Spawned.OfType<SimEventObject>().Count();
            var delta = 1f / fps;
            var run = Prepare();
            TickDragonContacts(run, 0, (first, 0, Vector3.Zero));
            Check(Pops(run.World) == 1, "First dragon contact pops once");
            for (var frame = 0; frame < 3 * fps; frame++)
            {
                // Remain within the original contact radius even when the
                // smaller head no longer covers this position.
                var offset = frame < fps ? new Vector3(1.5f, 0, 0) : Vector3.Zero;
                TickDragonContacts(run, delta, (first, 0, offset));
                Check(Pops(run.World) == 1 && !run.World.Party.Get(first)!.Dead,
                    "Continuous overlap cannot double-pop or kill the first interceptor after cooldown");
            }
            TickDragonContacts(run, delta, (second, 0, Vector3.Zero));
            Check(Pops(run.World) == 2 && !run.World.Party.Get(second)!.Dead,
                "A different interceptor can consume the second pop after cooldown");
            run.World.Despawn();

            run = Prepare();
            TickDragonContacts(run, 0, (first, 0, Vector3.Zero));
            for (var frame = 0; frame < (int)(1.9f * fps); frame++)
            {
                TickDragonContacts(run, delta, (second, 0, Vector3.Zero));
                Check(Pops(run.World) == 1, "Cooldown also prevents an immediate pop by another member");
            }
            for (var frame = 0; frame < (int)(0.3f * fps) + 1; frame++)
                TickDragonContacts(run, delta, (second, 0, Vector3.Zero));
            Check(Pops(run.World) == 2, "Head accepts its second hit once the two-second cooldown expires");
            run.World.Despawn();

            run = Prepare();
            TickDragonContacts(run, 0, (first, 0, Vector3.Zero));
            TickDragonContacts(run, delta); // Leave the original contact area.
            for (var frame = 0; frame < (int)(1.8f * fps); frame++)
            {
                TickDragonContacts(run, delta, (first, 0, Vector3.Zero));
                Check(Pops(run.World) == 1 && !run.World.Party.Get(first)!.Dead, "Leaving and re-entering does not bypass cooldown");
            }
            for (var frame = 0; frame < fps / 2; frame++)
                TickDragonContacts(run, delta, (first, 0, Vector3.Zero));
            Check(Pops(run.World) == 2 && run.World.Party.Get(first)!.Damage.Any(d => d.Cause == "dragon intercepted without Wyrmclaw"),
                "A fresh contact after cooldown retains the invalid-soak failure rule");
            run.World.Despawn();

            run = Prepare();
            TickDragonContacts(run, 0, (first, 0, Vector3.Zero), (pattern.Role(CrystallizeTimeAssignment.AeroEast), 1, Vector3.Zero));
            Check(Pops(run.World) == 2, "The two heads have independent cooldowns");
            run.World.Despawn(); run.World.Events.Clear();
            run.Scenario.Run(run.World, pattern);
            Until(run, CrystallizeTimeConstants.DragonSpawnTime + 0.1f);
            run.World.Events.Clear(); run.World.Events.Tick(19.5f);
            TickDragonContacts(run, 0, (first, 0, Vector3.Zero));
            Check(Pops(run.World) == 1, "Restart clears both cooldowns and previous-contact state");
            run.World.Despawn();
        }
    }

    private static void CheckPuddleCreatorExclusion()
    {
        foreach (var fps in new[] { 15, 30, 60 })
        foreach (var side in new[] { 0, 1 })
        foreach (var secondPop in new[] { false, true })
        foreach (var manual in new[] { false, true })
        {
            var pattern = CrystallizeTimePattern.Randomize(new Random(0), false);
            var first = pattern.Role(side == 0 ? CrystallizeTimeAssignment.AeroWest : CrystallizeTimeAssignment.AeroEast);
            var second = pattern.Role(side == 0 ? CrystallizeTimeAssignment.IceWest : CrystallizeTimeAssignment.IceEast);
            var creatorRole = secondPop ? second : first;
            var run = NewRun(pattern, manual ? creatorRole : null);
            Until(run, CrystallizeTimeConstants.DragonSpawnTime + 0.1f);
            run.World.Events.Clear();
            run.World.Events.Tick(19.5f);
            TickDragonContacts(run, 0, (first, side, Vector3.Zero));
            if (secondPop) TickDragonContacts(run, 2.1f, (second, side, Vector3.Zero));
            var puddle = run.World.Spawned.OfType<SimEventObject>().Last();
            var creator = run.World.Party.Get(creatorRole)!;
            // Place the creator first to catch implementations that let an
            // excluded overlapping member prevent another member being selected.
            run.World.Party.Members.Remove(creator);
            run.World.Party.Members.Insert(0, creator);
            void TickPuddle(float delta, bool overlap = true)
            {
                run.World.Events.Tick(delta);
                foreach (var member in run.World.Party.Members) Put(member, new(0, 0, 19));
                if (overlap) Put(creator, puddle.Config.Placement.Position);
                run.Scenario.Tick(delta, run.World.Events.Elapsed);
            }
            for (var frame = 0; frame < 3 * fps; frame++)
            {
                TickPuddle(1f / fps);
                Check(puddle.IsActive, "Lingering interceptor cannot consume their own armed puddle");
            }
            TickPuddle(1f / fps, false);
            TickPuddle(1f / fps);
            Check(puddle.IsActive, "Leaving and re-entering does not make the creator eligible for their puddle");
            var recipient = run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Water))!;
            Check(recipient.HasStatus(CrystallizeTimeConstants.FangStatus), "Puddle recipient starts with Fang");
            Put(recipient, puddle.Config.Placement.Position);
            run.Scenario.Tick(0, run.World.Events.Elapsed);
            Check(!puddle.IsActive && !recipient.HasStatus(CrystallizeTimeConstants.FangStatus),
                "Another member can cleanse while the excluded creator remains on the puddle");
            run.World.Despawn();
        }
    }

    private static void TickDragonContacts((SimWorld World, FruCrystallizeTimeScenario Scenario) run,
        float delta, params (PartyRole Role, int Side, Vector3 Offset)[] contacts)
    {
        run.World.Events.Tick(delta);
        foreach (var member in run.World.Party.Members) Put(member, new(0, 0, 19));
        foreach (var contact in contacts)
            Put(run.World.Party.Get(contact.Role)!, FruCrystallizeTimeScenario.DragonPosition(contact.Side, run.World.Events.Elapsed) + contact.Offset);
        run.Scenario.Tick(delta, run.World.Events.Elapsed);
    }

    private static void CheckSpiritSpread()
    {
        var roles = Enum.GetValues<PartyRole>();
        Vector3[] playerPositions = [Vector3.Zero, new(0, 0, -16), new(12, 0, 0), new(-12, 0, 0), new(0, 0, 12)];
        var runs = 0;
        foreach (var corner in Enum.GetValues<CrystallizeTimeCorner>())
        foreach (var target in roles)
        foreach (var fps in new[] { 15, 30, 60 })
        foreach (var playerPosition in playerPositions)
        {
            var pattern = new CrystallizeTimePattern(true, corner, roles, roles[..3], target);
            var run = NewRun(pattern);
            Until(run, 40.5f);
            // Use bots for the earlier mechanics, then hand this role to a
            // stationary player to isolate the jump's movement decisions.
            var player = new SimPlayer { Role = target, Position = playerPosition };
            run.World.Party.Members.Remove(run.World.Party.Get(target)!);
            run.World.Party.Members.Add(player);
            while (run.World.Events.Elapsed < 43.3f) Step(run.World, run.Scenario, 1f / fps);
            var bots = run.World.Party.Members.OfType<SimPartyNpc>().ToArray();
            foreach (var bot in bots)
            {
                Check(Vector3.Distance(bot.Position, player.Position) > 5,
                    $"Spirit spread avoids player: {corner} {target} {fps} {playerPosition} {bot.Role} {bot.Position}");
                Check(Vector3.Distance(bot.Position, CrystallizeTimeConstants.FragmentPosition) > 5,
                    "Spirit spread avoids crystal before jump snapshot");
                Check(bots.All(other => other == bot || Vector3.Distance(bot.Position, other.Position) > 5),
                    $"Spirit spread separates bots: {corner} {target} {fps} {playerPosition} {bot.Role} {bot.Position}");
            }
            Until(run, 44.1f);
            Check(run.World.Party.Members.All(m => !m.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.SpiritHit)),
                "Spirit jump on stationary player does not clip bots");
            Check(player.Position == playerPosition && player.Moves.Count == 0, "Spirit spread leaves player control alone");
            runs++;
        }

        foreach (var corner in Enum.GetValues<CrystallizeTimeCorner>())
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var pattern = new CrystallizeTimePattern(true, corner, roles, roles[..3], PartyRole.MainTank);
            var run = NewRun(pattern);
            Until(run, 40.5f);
            var player = new SimPlayer { Role = PartyRole.MainTank, Position = Vector3.Zero };
            run.World.Party.Members.Remove(run.World.Party.Get(player.Role)!);
            run.World.Party.Members.Add(player);
            Until(run, 41.2f);
            var displaced = run.World.Party.Members.OfType<SimPartyNpc>().First();
            player.Position = displaced.Moves[^1].Target;
            var occupied = player.Position;
            while (run.World.Events.Elapsed < 43.3f) Step(run.World, run.Scenario, 1f / fps);
            Check(displaced.Moves[^1].Target != occupied && Vector3.Distance(displaced.Position, occupied) > 5,
                "Bot yields its reserved spread spot when the player moves into it");
            Until(run, 44.1f);
            Check(run.World.Party.Members.All(m => !m.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.SpiritHit)),
                "Adaptive spread survives player relocation");
            Check(player.Moves.Count == 0, "Adaptive spread never moves the player");
        }
        Console.WriteLine($"PASS: Spirit Taker avoidance; {runs} stationary-player runs; 12 player-relocation runs; crystal clearance; bot separation; player control.");
    }

    private static void CheckWingsPresentation()
    {
        foreach (var corner in Enum.GetValues<CrystallizeTimeCorner>())
        {
            var assignment = CrystallizeTimePattern.Randomize(new Random(7), false);
            var pattern = new CrystallizeTimePattern(true, corner, assignment.Roles, assignment.Quietus, PartyRole.MainTank);
            var run = NewRun(pattern);
            Until(run, 41.7f);
            var boss = run.World.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Usurper);
            Check(boss.Timelines.Contains((CrystallizeTimeConstants.WingsWindupTimeline, CrystallizeTimeConstants.WingsWindupLoop)),
                "Native wings windup starts before the frozen-player section");
            Until(run, 42.8f);
            Check(boss.Config.ModelCharaId == CrystallizeTimeConstants.UsurperDragonModel
                && boss.Casts.Count(c => c.Action == CrystallizeTimeConstants.WingsFirst && c.CastSeconds == 4.8f) == 1,
                "Dragon-armored Usurper casts Wings with its native teleport-out release");
            foreach (var second in new[] { false, true })
            {
                Until(run, second ? 54f : 49.4f);
                Check(boss.Position == pattern.WaveOrigin(second, 0)
                    && MathF.Abs(boss.Rotation - MathF.Atan2(pattern.WaveDirection(second).X, pattern.WaveDirection(second).Z)) < 0.001f,
                    "Native Wings animation uses the actual knockback origin and facing");
                Check(boss.Visible && boss.Timelines[^2] == (CrystallizeTimeConstants.WingsShowTimeline, CrystallizeTimeConstants.WingsShowLoop)
                    && boss.Timelines[^1] == (CrystallizeTimeConstants.WingsSwingTimeline, (ushort)0),
                    "Native teleport-in and ready loop lead into each wing swing");
                Check(boss.Timelines.Count(t => t.Start == CrystallizeTimeConstants.WingsSwingTimeline) == (second ? 2 : 1)
                    && boss.Casts.All(c => c.Action != CrystallizeTimeConstants.WingsHit),
                    "Each wing swing plays once with no competing action-effect dispatch");
                Check(run.World.Party.Members.All(m => m.HasStatus(CrystallizeTimeConstants.StunStatus)),
                    "Both native wing swings occur during the frozen-player section");
                Check(run.World.Party.Members.Sum(m => m.Damage.Count(d => d.Action == CrystallizeTimeConstants.WingsHit)) == (second ? 4 : 0),
                    "Animation lead-in does not apply knockback damage early");
                if (!second)
                {
                    Until(run, 52.9f);
                    Check(boss.Timelines.Last() == (CrystallizeTimeConstants.WingsSwingTimeline, (ushort)0)
                        && boss.Position == pattern.WaveOrigin(false, 0),
                        "The first swing remains uninterrupted until relocation");
                    Until(run, 53.05f);
                    Check(boss.Visible && boss.Timelines.Last() == (CrystallizeTimeConstants.WingsHideTimeline, (ushort)0)
                        && boss.Position == pattern.WaveOrigin(false, 0),
                        "Native teleport-out follows the first wing recovery before relocation");
                    Until(run, 53.2f);
                    Check(boss.Visible && boss.Position == pattern.WaveOrigin(true, 0)
                        && boss.Timelines.Last() == (CrystallizeTimeConstants.WingsShowTimeline, CrystallizeTimeConstants.WingsShowLoop),
                        "Second-edge teleport-in retains its original 53.1 timing");
                }
            }
            Until(run, 55.6f);
            Check(run.World.Party.Members.Sum(m => m.Damage.Count(d => d.Action == CrystallizeTimeConstants.WingsHit)) == 8
                && run.World.Party.Members.All(m => !m.HasStatus(CrystallizeTimeConstants.StunStatus)),
                "Both four-player impacts finish and the party unfreezes before Akh Morn movement");
            Check(boss.Casts.All(c => c.Action != CrystallizeTimeConstants.WingsSecond)
                && boss.Timelines.Count(t => t.Start == CrystallizeTimeConstants.WingsHideTimeline) == 1
                && boss.Timelines.Count(t => t.Start == CrystallizeTimeConstants.WingsShowTimeline) == 2,
                "One edge departure and two arrivals play without an extra disappearance cast");
        }
    }

    private static void CheckPlayerControl()
    {
        // A separate all-bot rehearsal supplies intended user inputs. Only this
        // test driver moves the player; production must issue no MoveTo to them.
        foreach (var northwest in new[] { true, false })
        foreach (var corner in Enum.GetValues<CrystallizeTimeCorner>())
        foreach (var role in Enum.GetValues<PartyRole>())
        foreach (var preference in Enum.GetValues<CrystallizeTimePlayerPattern>())
        {
            var assignment = CrystallizeTimePattern.Randomize(new Random(100 + (int)role), false, role, preference);
            var pattern = new CrystallizeTimePattern(northwest, corner, assignment.Roles, assignment.Quietus, role);
            var rehearsal = NewRun(pattern);
            var run = NewRun(pattern, role);
            var player = run.World.Party.Player!;
            var pilot = rehearsal.World.Party.Get(role)!;
            const float delta = 1f / 30;
            for (var frame = 0; frame < 68 * 30; frame++)
            {
                run.World.Events.Tick(delta);
                rehearsal.World.Events.Tick(delta);
                foreach (var member in run.World.Party.Members) member.Advance(delta);
                if (player.ForcedMove == null && !player.MechanicInputLock && pilot.Moves.Count > pilot.StopAtMoveCount)
                {
                    var offset = pilot.Moves[^1].Target - player.Position;
                    player.Position += offset.Length() <= 6 * delta ? offset : Vector3.Normalize(offset) * (6 * delta);
                }
                foreach (var member in rehearsal.World.Party.Members) member.Advance(delta);
                run.Scenario.Tick(delta, run.World.Events.Elapsed);
                rehearsal.Scenario.Tick(delta, rehearsal.World.Events.Elapsed);
                Check(run.World.Party.Members.All(m => !m.Dead), $"Manual CT {role} {preference} {northwest} {corner} {run.World.Events.Elapsed:F2}: " +
                    string.Join(';', run.World.Party.Members.Where(m => m.Dead).Select(m => $"{m.Role} {m.Damage.LastOrDefault().Cause}")));
            }
            Check(player.Moves.Count == 0, "CT never issues normal movement to the player");
            Check(!player.MechanicInputLock && player.Statuses.Count == 0, "Return releases player input and statuses");
        }
    }
    private static void CheckFailures()
    {
        var roles = Enum.GetValues<PartyRole>();
        var pattern = new CrystallizeTimePattern(true, CrystallizeTimeCorner.NE, roles, roles[..3], PartyRole.MainTank);
        var run = NewRun(pattern);
        Until(run, 17.5f);
        Put(run.World.Party.Get(PartyRole.MainTank)!, CrystallizeTimePattern.Hourglass(0));
        Until(run, 17.8f);
        Check(run.World.Party.Get(PartyRole.MainTank)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.Maelstrom), "Hourglass failure kills");

        run = NewRun(pattern);
        Until(run, 18.5f);
        Put(run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Ice))!, new(0, 0, -19));
        Until(run, 18.8f);
        Check(run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Water))!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.Water), "Underfilled water stack fails");

        run = NewRun(pattern);
        Until(run, 20.4f);
        Put(run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Ice))!,
            run.World.Party.Get(pattern.Role(CrystallizeTimeAssignment.Water))!.Position + new Vector3(4, 0, 0));
        Until(run, 20.7f);
        Check(run.World.Party.Members.Any(m => m.Damage.Any(d => d.Lethal && d.Action is CrystallizeTimeConstants.Ice or CrystallizeTimeConstants.Aero)), "Bad fireworks positioning fails");

        run = NewRun(pattern);
        Until(run, 28.8f);
        Put(run.World.Party.Get(PartyRole.MainTank)!, new(5, 0, 0));
        Until(run, 29.1f);
        Check(run.World.Party.Get(PartyRole.MainTank)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.TidalRest), "Tidal strip fails");

        run = NewRun(pattern);
        Until(run, 39.4f);
        var tank = run.World.Party.Get(PartyRole.MainTank)!;
        var dps = run.World.Party.Get(PartyRole.MeleeDpsA)!;
        Put(dps, tank.Position + new Vector3(2, 0, 0));
        Until(run, 50);
        Check(dps.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.WingsHit), "Non-tank leading Hallowed Wings fails");

        run = NewRun(pattern);
        Until(run, 39.4f);
        foreach (var member in run.World.Party.Members)
            Put(member, member.Role switch {
                PartyRole.MainTank => new(12, 0, -11), PartyRole.OffTank => new(11, 0, -12),
                PartyRole.RegenHealer or PartyRole.ShieldHealer => new(10, 0, -10), _ => new(8, 0, -8) });
        Until(run, 54.6f);
        Check(run.World.Party.Get(PartyRole.OffTank)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.WingsHit),
            "The same first-four group cannot take both Wings hits");

        run = NewRun(pattern);
        Until(run, 43.95f);
        var oracle = run.World.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == CrystallizeTimeConstants.Oracle);
        Put(run.World.Party.Get(PartyRole.CasterDps)!, oracle.Position);
        Until(run, 44.1f);
        Check(run.World.Party.Get(PartyRole.CasterDps)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.SpiritHit), "Spirit Taker overlap fails");

        run = NewRun(pattern);
        Until(run, 46.4f);
        run.World.Party.Get(PartyRole.CasterDps)!.AddStatus(CrystallizeTimeConstants.FangStatus);
        Until(run, 46.6f);
        Check(run.World.Party.Get(PartyRole.CasterDps)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.CleanseFailure), "Uncleansed Fang expires lethally");

        run = NewRun(pattern);
        Until(run, 61.7f);
        Put(run.World.Party.Get(PartyRole.CasterDps)!, new(18, 0, 0));
        Until(run, 62);
        Check(run.World.Party.Get(PartyRole.OffTank)!.Damage.Any(d => d.Lethal && d.Action == CrystallizeTimeConstants.AkhHitOracle), "Six-person Akh Morn stack fails");

        foreach (var time in new[] { 12f, 25f, 40f, 47f, 50f })
        {
            run = NewRun(pattern);
            Until(run, time);
            var actors = run.World.Spawned.ToArray();
            run.World.Events.Clear(); run.World.Despawn();
            run.World.Events.Tick(100);
            Check(actors.All(a => !a.IsActive) && run.World.Spawned.Count == 0, "Reset removes native objects and cancels callbacks");
        }
        var missing = new SimWorld { FailEnemySpawns = true };
        new FruCrystallizeTimeScenario().Run(missing, pattern);
        missing.Events.Tick(68);
        Check(missing.Spawned.Count == 0, "Missing bosses do not run an invisible mechanic");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
