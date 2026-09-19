using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.DiamondDust;
using static AnoMech.Scenarios.Fru.DiamondDust.DiamondDustConstants;

internal static class DiamondDustChecks
{
    public static void Run()
    {
        Assignments();
        Settings();
        var runs = 0;
        for (var first = 0; first < 4; first++)
        foreach (var marked in new[] { false, true })
        foreach (var axe in new[] { false, true })
        for (var reflection = 0; reflection < 8; reflection++)
        foreach (var stillness in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
        {
            var pattern = new DiamondDustPattern(first, marked, axe, reflection, stillness, (reflection + 3) % 8);
            var (world, scenario) = NewRun(pattern);
            while (world.Events.Elapsed < 55)
            {
                Step(world, scenario, 1f / fps);
                foreach (var member in world.Party.Members)
                    Check(!member.Dead, $"{first}/{marked}/{axe}/{reflection}/{stillness}/{fps} at {world.Events.Elapsed:F2}: {member.Role}: {string.Join(", ", member.Damage.Where(d => d.Lethal).Select(d => d.Cause))} pos {member.Position}");
            }
            Check(world.Spawned.All(s => !s.IsActive), "All native actors, puddles and ice are released on completion");
            var voice = world.Spawned.OfType<SimVoiceLine>().Single();
            Check(voice.VoiceId == (stillness ? 8205521u : 8205522u)
                && MathF.Abs(voice.StartTime - ComboTime) <= 1.1f / fps,
                "Correct native voice plays exactly once at the Twin combo cast");
            Check(world.Party.Members.All(m => !m.HasStatus(ThinIce)), "Thin Ice removed on completion");
            runs++;
        }
        Console.WriteLine($"PASS: Diamond Dust, {runs} complete NAUR bot runs at 15/30/60 FPS.");
        ManualPlayers();
        FailuresAndReset();
    }
    private static void Assignments()
    {
        int[] original = [0, 2, 6, 4, 5, 3, 7, 1];
        for (var first = 0; first < 4; first++)
        foreach (var supports in new[] { false, true })
        {
            var p = new DiamondDustPattern(first, supports, false, 1, false, 0);
            Check(DiamondDustPattern.Roles.Select(p.Clock).Distinct().Count() == 8, "Partner swaps preserve eight distinct clocks");
            foreach (var role in DiamondDustPattern.Roles)
            {
                var support = (int)role < 4;
                var marked = support == supports;
                var expectedParity = (first + (marked ? 1 : 0)) % 2;
                var clock = original[(int)role];
                if (clock % 2 != expectedParity) clock = (clock + (support ? 7 : 1)) % 8;
                Check(p.Clock(role) == clock, "NAUR P1 clocks, supports CCW / DPS CW partner swap");
                var kb = p.KnockbackSpot(role);
                Check(MathF.Abs(kb.Length() - 6) < 0.001f, "Six-yalm knockback setup");
                var redPurple = kb.X < -0.01f || kb.Z < -5.99f;
                Check(redPurple == ((int)role % 2 == 0), "G1 red/purple and G2 blue/yellow");
            }
        }
    }
    private static void Settings()
    {
        var random = new Random(734);
        for (var kick = 0; kick <= 2; kick++)
        {
            var settings = new DiamondDustSettings { Kick = kick };
            var variants = Enumerable.Range(0, 128).Select(_ => settings.CreatePattern(random)).ToArray();
            Check(kick == 0 ? variants.Any(p => p.Axe) && variants.Any(p => !p.Axe)
                : variants.All(p => p.Axe == (kick == 1)), "Random or fixed opening kick is honored");
            Check(variants.Select(p => p.FirstIcicle).Distinct().Count() == 4
                && variants.Select(p => p.ReflectionPosition).Distinct().Count() == 8
                && variants.Select(p => p.GazePosition).Distinct().Count() == 8
                && variants.Select(p => p.Marked(PartyRole.MainTank)).Distinct().Count() == 2
                && variants.Select(p => p.Stillness).Distinct().Count() == 2
                && variants.Select(p => p.Cursed).Distinct().Count() == 2,
                "Every other mechanic remains randomized for each opening kick setting");
            settings.Reset();
            Check(settings.Kick == 0, "Auto restores a random opening kick");
        }
        Console.WriteLine("PASS: opening kick choices; other mechanics stay randomized.");
    }
    private static void ManualPlayers()
    {
        var runs = 0;
        foreach (var role in DiamondDustPattern.Roles)
        foreach (var still in new[] { false, true })
        foreach (var cursed in new[] { false, true })
        foreach (var fps in new[] { 15, 60 })
        {
            var pattern = new DiamondDustPattern(1, (int)role % 2 == 0, (int)role % 2 != 0, cursed ? 1 : 2, still, 4);
            var (reference, referenceScenario) = NewRun(pattern);
            var (world, scenario) = NewRun(pattern);
            var player = new SimPlayer { Role = role };
            world.Party.Members.RemoveAll(m => m.Role == role);
            world.Party.Members.Add(player);
            var guide = reference.Party.Get(role)!;
            var lastSlideCount = 0;
            Vector3 nativeSlideStart = default;
            Vector3? nativeSlideEnd = null;
            var nativeSlideTime = 0f;
            var dt = 1f / fps;
            while (world.Events.Elapsed < 55)
            {
                Step(reference, referenceScenario, dt);
                world.Events.Tick(dt);
                foreach (var member in world.Party.Members) member.Advance(dt);
                player.Rotation = guide.Rotation;
                var time = world.Events.Elapsed;
                // Model client position samples independently of production Movement.
                // This is not a native input test: the native distance parameter is
                // checked below, and the harness supplies the resulting slide path.
                if (nativeSlideEnd is { } end)
                {
                    nativeSlideTime += dt;
                    var progress = Math.Clamp(nativeSlideTime / (32f / SlideSpeed), 0, 1);
                    player.Position = Vector3.Lerp(nativeSlideStart, end, progress);
                    if (progress >= 1) nativeSlideEnd = null;
                }
                if (time >= GazeTime && time < IceEnd)
                {
                    Check(player.HasStatus(ThinIce) && player.StatusParams[ThinIce] == 0x140,
                        "Native Thin Ice must request FRU's 32-yalm distance and preserve it on refresh");
                    Check(!player.IsForcedMoving && !player.MechanicInputLock,
                        "Simulator must leave native ice movement unlocked");
                }
                if (time < GazeTime || time > IceEnd)
                {
                    // Recorded normal walking supplied as external user input.
                    if (!player.IsForcedMoving) player.Position = guide.Position;
                }
                else if (guide.Slides.Count > lastSlideCount)
                {
                    Check(nativeSlideEnd == null, "The previous client slide has landed");
                    nativeSlideStart = player.Position;
                    nativeSlideEnd = nativeSlideStart + Vector3.Normalize(guide.Slides[^1] - nativeSlideStart)
                        * (player.StatusParams[ThinIce] * 0.1f);
                    nativeSlideTime = 0;
                    lastSlideCount = guide.Slides.Count;
                }
                scenario.Tick(dt, time);
                Check(world.Party.Members.All(m => !m.Dead), $"Manual {role}/{still}/{cursed}/{fps} at {time:F2}: {string.Join(", ", world.Party.Members.SelectMany(m => m.Damage.Where(d => d.Lethal).Select(d => m.Role + ": " + d.Cause)))}");
            }
            Check(player.Moves.Count == 0, "AI never issues ordinary movement to the player");
            Check(player.Slides.Count == 0, "Scenario never adds a simulated slide on top of native player movement");
            Check(lastSlideCount == guide.Slides.Count && nativeSlideEnd == null, "Client slide paths reach the required safe positions");
            Check(!player.IsForcedMoving && !player.HasStatus(ThinIce), "Manual player finishes without a pending slide or ice debuff");
            runs++;
        }
        Console.WriteLine($"PASS: {runs} manual-player runs; every role, both combos, cursed recovery, native distance parameter and no competing simulated slides.");
    }
    private static void FailuresAndReset()
    {
        var pattern = new DiamondDustPattern(0, false, true, 2, true, 4);
        void Failure(float time, Action<SimWorld> breakMechanic, uint expected, string label)
        {
            var (world, scenario) = NewRun(pattern);
            while (world.Events.Elapsed < time) Step(world, scenario, 1f / 60);
            Check(world.Party.Members.All(m => !m.Dead), "Failure setup survives: " + label);
            breakMechanic(world);
            for (var i = 0; i < 100; i++) Step(world, scenario, 1f / 60);
            Check(world.Party.Members.Any(m => m.Damage.Any(d => d.Lethal && d.Action == expected)), "Reject " + label);
        }
        void Park(SimWorld w, PartyRole role, Vector3 position)
        { var m = w.Party.Get(role)!; m.StopMoving(); m.Position = position; }
        Failure(20.8f, w => Park(w, PartyRole.MainTank, Vector3.Zero), Axe, "inside Axe Kick");
        Failure(21.55f, w => Park(w, PartyRole.OffTank, w.Party.Get(PartyRole.MainTank)!.Position), Protean, "overlapping closest-player cones");
        Failure(23.2f, w => Park(w, PartyRole.MeleeDpsB, w.Party.Get(PartyRole.MeleeDpsA)!.Position), Stone, "overlapping marked spreads");
        Failure(27.4f, w => Park(w, PartyRole.MainTank, new(0, 0, -9)), HeavenlyStrike, "knockback into the wall");
        Failure(30.3f, w => Park(w, PartyRole.MainTank, pattern.StoneSpot(PartyRole.MeleeDpsA)), NeedleCross, "Frigid Needle star");
        Failure(31.55f, w => Park(w, PartyRole.MainTank, w.Party.Get(PartyRole.OffTank)!.Position), Holy, "three/five-person healer stacks");
        Failure(31.55f, w => w.Party.Get(PartyRole.RegenHealer)!.Dead = true, Holy, "missing healer");
        Failure(33, w => Park(w, PartyRole.MainTank, pattern.HolySpot(PartyRole.MainTank, 0)), Holy, "lingering puddle");
        Failure(40, w => w.Party.Get(PartyRole.MainTank)!.Face(pattern.GazePosition), Gaze, "looking into Shining Armor");
        Failure(46.5f, w => Park(w, PartyRole.MainTank, Vector3.Zero), StillnessFirst, "wrong side of Twin Stillness");
        foreach (var time in new[] { 16f, 26f, 35f, 41f, 44f, 47f })
        {
            var (world, scenario) = NewRun(pattern);
            while (world.Events.Elapsed < time) Step(world, scenario, 1f / 60);
            var spawned = world.Spawned.ToArray();
            world.Events.Clear();
            world.Despawn();
            for (var i = 0; i < 120; i++) Step(world, scenario, 1f / 60);
            Check(spawned.All(s => !s.IsActive) && world.Spawned.Count == 0, "Reset removes all owned effects and prevents respawns");
            Check(world.Party.Members.All(m => !m.HasStatus(ThinIce)), "Reset clears Thin Ice");
            if (time > GazeTime)
            {
                Check(world.Map.Effects.Contains((0x00010020u, (byte)23)), "Freeze uses mode 1 and the ice animation action flag");
                Check(world.Map.Effects.Last() == (0x00010040u, (byte)23), "Reset restores the normal P2 floor");
            }
        }
        var missing = new SimWorld { FailEnemySpawns = true };
        var absentScenario = new FruDiamondDustScenario();
        absentScenario.Run(missing, pattern);
        Step(missing, absentScenario, 60);
        Check(missing.Spawned.Count == 0, "Missing native boss exits safely");
        Console.WriteLine("PASS: incorrect baits/stacks, missing healer, puddles, gaze, cleaves, wall, active-mechanic reset and missing actors.");
    }
    private static (SimWorld, FruDiamondDustScenario) NewRun(DiamondDustPattern pattern)
    {
        var world = new SimWorld();
        foreach (var role in DiamondDustPattern.Roles) world.Party.Members.Add(new SimPartyNpc { Role = role });
        var scenario = new FruDiamondDustScenario();
        scenario.Run(world, pattern);
        return (world, scenario);
    }
    private static void Step(SimWorld world, FruDiamondDustScenario scenario, float delta)
    {
        world.Events.Tick(delta);
        foreach (var member in world.Party.Members) member.Advance(delta);
        scenario.Tick(delta, world.Events.Elapsed);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
