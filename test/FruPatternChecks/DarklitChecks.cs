using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.DarklitDragonsong;
using Ct = AnoMech.Scenarios.Fru.CrystallizeTime.CrystallizeTimeConstants;
using Lr = AnoMech.Scenarios.Fru.LightRampant.LightRampantConstants;
using Apoc = AnoMech.Scenarios.Fru.Apocalypse.ApocalypseConstants;

internal static class DarklitChecks
{
    public static void Run()
    {
        var runs = 0;
        foreach (var tank in new[] { PartyRole.MainTank, PartyRole.OffTank })
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
        for (var a = 4; a < 8; a++) for (var b = a + 1; b < 8; b++)
        for (var shape = 0; shape < 3; shape++)
        for (var tw = 0; tw < 4; tw++) for (var bw = 0; bw < 4; bw++)
        {
            var p = new DarklitPattern(tank, healer, (PartyRole)a, (PartyRole)b, shape, tw, bw, (PartyRole)(runs % 8), runs % 2 == 0);
            Check(Enumerable.Range(0, 8).Select(i => p.Slot(i)).Distinct().Count() == 8, "Unique roles");
            Check(p.North(p.TetherWater) != p.North(p.BaitWater), "Exactly one water per north/south stack");
            Check(p.Slot(0) == healer, "Tethered healer stays NW anchor");
            PartyRole[] baitPriority = [PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
            Check(p.Slot(7,false) == baitPriority.First(r=>(int)r!=a&&(int)r!=b), "NA cone-bait DPS use R2 before R1 west priority");
            if(shape==0) {
                PartyRole[] linePriority = [PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
                Check(p.Slot(3) == linePriority.First(r=>(int)r==a||(int)r==b), "NA tether lineup uses R1 before R2 west priority");
            }
            var swapped = Enumerable.Range(0, 8).Where(i => p.Slot(i) != p.Slot(i, false)).ToArray();
            Check(swapped.Length == ((tw < 2) == (bw < 2) ? 2 : 0) && swapped.All(i => i >= 4), "Only two non-tethers flex when waters share a side");
            for (var i = 0; i < 4; i++) Check(p.North(p.Link(i)) != p.North(p.Link(i + 1)), "Every chain spans the north/south bowtie");
            Complete(p, new[] { 15, 30, 60 }[runs % 3]); runs++;
        }
        Console.WriteLine($"PASS: Darklit {runs} full assignment runs; all tether parties, shapes and water swaps at 15/30/60 FPS.");
        var rotations = 0;
        for (var shape = 0; shape < 3; shape++) for (var tw = 0; tw < 4; tw++) for (var bw = 0; bw < 4; bw++)
        foreach (var east in new[] { false, true }) foreach (var spirit in DarklitPattern.Roles)
        { Complete(Pattern(shape, tw, bw, east, spirit), 30); rotations++; }
        Console.WriteLine($"PASS: {rotations} Darklit runs across both wings, every Spirit Taker target and water/chain pattern.");
        Transformation(); Manual(); Failures();
    }
    private static DarklitPattern Pattern(int shape = 0, int tw = 0, int bw = 0, bool east = false, PartyRole spirit = PartyRole.MeleeDpsA)
        => new(PartyRole.MainTank, PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.CasterDps, shape, tw, bw, spirit, east);
    private static (SimWorld World, FruDarklitDragonsongScenario Scenario) NewRun(DarklitPattern p, PartyRole? playerRole = null, bool playerTakesBoth = false, bool guide = false)
    {
        var w = new SimWorld(); foreach (var role in DarklitPattern.Roles) w.Party.Members.Add(new SimPartyNpc { Role = role });
        if (playerRole is { } humanRole) {
            w.Party.Members.RemoveAll(m => m.Role == humanRole);
            w.Party.Members.Add(new SimPlayer { Role = humanRole });
        }
        var scenario = new FruDarklitDragonsongScenario { PlayerTakesSomberDance = playerTakesBoth };
        scenario.Run(w, p);
        // Resolve the same assignment as the manual run, then let AI provide a guide path.
        if (guide && playerRole is { } guided) {
            w.Party.Members.RemoveAll(m => m.Role == guided);
            w.Party.Members.Add(new SimPartyNpc { Role = guided });
        }
        return (w, scenario);
    }
    private static void Step(SimWorld w, FruDarklitDragonsongScenario s, float dt)
    {
        w.Events.Tick(dt); foreach (var m in w.Party.Members) m.Advance(dt); s.Tick(dt, w.Events.Elapsed);
    }
    private static void Alive(SimWorld w, DarklitPattern p)
        => Check(w.Party.Members.All(m => !m.Dead), $"Darklit wing={p.EastWing} spirit={p.SpiritTarget} at {w.Events.Elapsed:F2}: " + string.Join("; ", w.Party.Members.Where(m => m.Dead).Select(m => $"{m.Role}/{p.Index(m.Role)} {m.Position}: {string.Join(',', m.Damage.Where(d => d.Lethal).Select(d => d.Cause))}")));
    private static void Complete(DarklitPattern p, int fps)
    {
        var (w, s) = NewRun(p);
        while (w.Events.Elapsed < 64) { Step(w, s, 1f / fps); Alive(w, p); }
        Check(w.Spawned.All(o => !o.IsActive), "All owned native resources released");
        Check(w.Party.Members.All(m => !m.HasStatus(Ct.WaterStatus) && !m.HasStatus(Lr.Lightsteeped) && !m.HasStatus(Lr.Curse) && !m.ActiveActorVfx.Contains(Apoc.WaterWaitingClock)), "Statuses and waiting clocks released");
        Check(new byte[] {42,43,46}.All(i => w.Map.Effects.Contains((0x00010001u,i)) && w.Map.Effects.Contains((0x00040004u,i))), "Towers and fragment scenery activate and clear");
        var casts = w.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).ToArray();
        Check(casts.Count(c => c.Action == DarklitConstants.AkhRhaiCast) == 8 && casts.Count(c => c.Action == DarklitConstants.AkhRhaiHit) == 80,
            "Eight recorded puddles: initial cast release followed by ten hit-only pulses");
        Check(casts.Count(c => c.Action == DarklitConstants.PathHit) == 4, "Four proteans");
        Check(casts.Any(c => c.Action == (p.EastWing ? DarklitConstants.WingRight : DarklitConstants.WingLeft)), "Native wing warning matches the cleaved side of north-facing Usurper");
        BothHits(w, PartyRole.MainTank);
        AkhMornHits(w);
        Check(casts.Count(c => c.Action is Ct.AkhHitUsurper or Ct.AkhHitOracle) == 8, "Four double Akh Morn hits");
    }
    private static void BothHits(SimWorld w, PartyRole tank)
    {
        foreach (var action in new[] { DarklitConstants.SomberFar, DarklitConstants.SomberNear }) {
            var hits = w.Party.Members.Where(m => m.Damage.Any(d => d.Action == action)).ToArray();
            Check(hits.Length == 1 && hits[0].Role == tank && hits[0].Damage.All(d => !d.Lethal), "Only the assigned tank takes each Somber hit and survives both");
            var cast = w.Spawned.OfType<SimEnemy>().SelectMany(e => e.Casts).Single(c => c.Action == action);
            Check(cast.Target == w.Party.Get(tank)!.GameObjectId, "Both native Somber actions target the assigned tank");
        }
    }
    private static void AkhMornHits(SimWorld w)
    {
        foreach (var member in w.Party.Members) {
            var action = member.Role == PartyRole.OffTank ? Ct.AkhHitOracle : Ct.AkhHitUsurper;
            var hits = member.Damage.Where(d => d.Action is Ct.AkhHitOracle or Ct.AkhHitUsurper).ToArray();
            Check(hits.Length == 4 && hits.All(d => d.Action == action && !d.Lethal),
                "All four Akh Morns: OT alone on Oracle, seven on MT, no cross-stack hits");
        }
    }
    private static void Transformation()
    {
        foreach (var fps in new[] { 15, 60 }) {
            var (w,s) = NewRun(Pattern());
            void Until(float time) { while(w.Events.Elapsed < time) Step(w,s,1f/fps); }
            Until(8.7f);
            var original = w.Spawned.OfType<SimEnemy>().Single(e => e.Config.BNpcBaseId == Ct.Usurper);
            Check(original.Config.ModelCharaId == 4375 && original.Timelines.Count == 0,
                "Hooded Usurper remains after baits lock, until just before Akh Rhai releases");
            Until(8.9f);
            var dragon = w.Spawned.OfType<SimEnemy>().Single(e => e.Config.ModelCharaId == Ct.UsurperDragonModel);
            Check(original.Timelines.Single().Start == 4574 && !original.Targetable && !original.ManualEnemyListVisible,
                "Outgoing hooded actor runs Redress and retires from targeting and enemy list");
            Check(dragon.Config.SpawnTimeline == 4562 && dragon.ManualEnemyListVisible && dragon.Position == original.Position,
                "Dragon model runs the matched native Redress spin in the same position");
            Until(14.1f);
            Check(!original.IsActive && dragon.IsActive && !dragon.Casts.Any(),
                "Full Redress sequence completes before a cast can interrupt it");
            Until(16.5f);
            Check(!original.IsActive && dragon.IsActive && dragon.Casts.Any(c => c.Action == DarklitConstants.Raidwide),
                "Outgoing effects finish; subsequent mechanics use the dragon model");
        }
        foreach (var time in new[] { 8.7f, 8.9f, 11f, 14.1f }) {
            var (w,s) = NewRun(Pattern());
            while(w.Events.Elapsed < time) Step(w,s,1f/60);
            var actors = w.Spawned.ToArray(); w.Events.Clear(); w.Despawn();
            while(w.Events.Elapsed < 20) Step(w,s,1f/60);
            Check(actors.All(a => !a.IsActive) && w.Spawned.Count == 0, "Reset around transformation releases both models and never respawns them");
        }
        var (failed,fs) = NewRun(Pattern());
        while(failed.Events.Elapsed < 8.7f) Step(failed,fs,1f/60);
        failed.FailEnemySpawns = true;
        while(failed.Events.Elapsed < 9f) Step(failed,fs,1f/60);
        Check(failed.Spawned.All(a => !a.IsActive), "Failed dragon model creation cleans up the original safely");
        Console.WriteLine("PASS: Darklit original-to-dragon timeline, native effect ownership, interrupted resets and failed transformation spawn.");
    }
    private static void Manual()
    {
        var runs = 0;
        foreach (var playerTakesBoth in new[] { false, true })
        foreach (var role in DarklitPattern.Roles) foreach (var east in new[] { false, true })
        foreach (var shape in new[] {0,1,2}) foreach (var fps in new[] {15,60})
        {
            var p = Pattern(shape, 0, 0, east, role); var (w,s) = NewRun(p, role, playerTakesBoth); var (guide,gs) = NewRun(p, role, playerTakesBoth, guide: true);
            var player = w.Party.Player!;
            while (w.Events.Elapsed < 64) {
                Step(guide,gs,1f/fps); player.Position = guide.Party.Get(role)!.Position;
                Step(w,s,1f/fps); Alive(w,p);
            }
            var tank = playerTakesBoth && role is PartyRole.MainTank or PartyRole.OffTank ? role
                : role == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;
            BothHits(w, tank);
            AkhMornHits(w);
            Check(player.Moves.Count == 0 && !player.MechanicInputLock, "Manual player remains under player control"); runs++;
        }
        Console.WriteLine($"PASS: Darklit {runs} manual-player runs across every role, chain shape and wing; default bot and opt-in player Somber baits.");
    }
    private static void Failures()
    {
        var p = Pattern();
        void Advance(SimWorld w, FruDarklitDragonsongScenario s, float time) { while(w.Events.Elapsed < time) Step(w,s,1f/60); }
        void Hold(SimCharacter m, Vector3 pos) { m.StopMoving(); m.Position = pos; }
        void Failure(float time, Action<SimWorld> change, string cause, float end = 64, DarklitPattern? chosen = null) {
            var (w,s)=NewRun(chosen ?? p); Advance(w,s,time); change(w); Advance(w,s,end);
            Check(w.Party.Members.SelectMany(m=>m.Damage).Any(d=>d.Lethal&&d.Cause.Contains(cause)), "Expected failure: "+cause+"; observed: "+string.Join(',',w.Party.Members.SelectMany(m=>m.Damage).Where(d=>d.Lethal).Select(d=>d.Cause)));
        }
        Failure(9.2f, w=>Hold(w.Party.Get(PartyRole.MainTank)!, Vector3.Zero), "Akh Rhai",9.4f);
        Failure(32.2f, w=>Hold(w.Party.Get(p.Slot(0))!, new(0,0,-12.5f)), "Bright Hunger",32.4f);
        Failure(32.2f, w=>Hold(w.Party.Get(p.Slot(5))!, w.Party.Get(p.Slot(4))!.Position), "protean overlap",32.4f);
        Failure(30, w=>Hold(w.Party.Get(p.Slot(0))!, w.Party.Get(p.Slot(2))!.Position), "chain broke",30.1f);
        Failure(35.4f, w=>Hold(w.Party.Get(p.Slot(4))!, w.Party.Get(p.SpiritTarget)!.Position), "Spirit Taker",36.8f);
        Failure(40.4f, w=>Hold(w.Party.Get(p.Slot(4))!, new(10,0,0)), "Dark Water",40.6f);
        Failure(40.4f, w=> { foreach(var role in DarklitPattern.Roles.Where(p.North)) Hold(w.Party.Get(role)!, new(-6.5f,0,-7.3f)); }, "Hallowed Wings",40.6f);
        Failure(35.4f, w=>Hold(w.Party.Get(p.Slot(4))!, Ct.FragmentPosition), "Fragment of Fate",36.8f, Pattern(spirit:p.Slot(4)));
        Failure(44, w=>Hold(w.Party.Get(p.Slot(4))!, new(19.5f,0,0)), "assigned tank for both hits",44.9f);
        Failure(47.5f, w=> {
            var pos=w.Spawned.OfType<SimEnemy>().Single(e=>e.Config.BNpcBaseId==Ct.Oracle).Position;
            Hold(w.Party.Get(PartyRole.MainTank)!,pos+new Vector3(0,0,3));
            Hold(w.Party.Get(p.Slot(4))!,pos);
        }, "assigned tank for both hits",47.7f);
        // An unassigned tank cannot take over hit two, either.
        Failure(47.5f, w=> {
            var pos=w.Spawned.OfType<SimEnemy>().Single(e=>e.Config.BNpcBaseId==Ct.Oracle).Position;
            Hold(w.Party.Get(PartyRole.MainTank)!,pos+new Vector3(0,0,3));
            Hold(w.Party.Get(PartyRole.OffTank)!,pos);
        }, "assigned tank for both hits",47.7f);
        Failure(58.5f, w=>Hold(w.Party.Get(PartyRole.MeleeDpsA)!, new(15,0,0)), "Akh Morn",58.7f);
        Failure(58.5f, w=>Hold(w.Party.Get(PartyRole.OffTank)!, w.Party.Get(PartyRole.MainTank)!.Position), "Akh Morn",58.7f);
        // The old 4+4 split must fail, as must a player joining OT on a later hit.
        Failure(58.5f, w=> {
            foreach (var role in new[] { PartyRole.ShieldHealer, PartyRole.MeleeDpsB, PartyRole.CasterDps })
                Hold(w.Party.Get(role)!, w.Party.Get(PartyRole.OffTank)!.Position);
        }, "Akh Morn",58.7f);
        Failure(59.1f, w=>Hold(w.Party.Get(PartyRole.CasterDps)!, w.Party.Get(PartyRole.OffTank)!.Position), "Akh Morn",59.3f);
        Failure(7.3f, w=>Hold(w.Party.Get(PartyRole.MainTank)!, new(21,0,0)), "arena boundary",7.4f);
        foreach (var time in new[] {7f, 23, 30, 36, 41, 46, 59}) {
            var (w,s)=NewRun(p); Advance(w,s,time); w.Events.Clear(); w.Despawn(); var count=w.Spawned.Count;
            Advance(w,s,70); Check(w.Spawned.Count==count&&w.Spawned.All(o=>!o.IsActive), "Interrupted reset never respawns resources");
        }
        var missing = new SimWorld { FailEnemySpawns = true };
        var scenario = new FruDarklitDragonsongScenario(); scenario.Run(missing,p); Advance(missing,scenario,64);
        Check(missing.Spawned.All(o=>!o.IsActive), "Missing bosses abort safely");
        var (markers,ms)=NewRun(p); Advance(markers,ms,16.5f);
        foreach(var role in new[]{p.TetherWater,p.BaitWater}) Check(markers.Party.Get(role)!.ActorVfx.Contains((Apoc.WaterMarker,false)), "Initial finite stack markers");
        Advance(markers,ms,22);
        foreach(var role in new[]{p.TetherWater,p.BaitWater}) Check(markers.Party.Get(role)!.ActiveActorVfx.Contains(Apoc.WaterWaitingClock), "Persistent waiting clock before countdown");
        Advance(markers,ms,35.6f);
        foreach(var role in new[]{p.TetherWater,p.BaitWater}) {
            var m=markers.Party.Get(role)!;
            Check(!m.ActiveActorVfx.Contains(Apoc.WaterWaitingClock)&&m.ActorVfx.Contains((Apoc.WaterClock,false)), "Countdown replaces waiting clock with finite marker");
        }
        Console.WriteLine("PASS: Darklit failed baits/stacks/chains, arena boundary, interrupted resets and missing actors.");
    }
    private static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
}
