using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.UltimateRelativity.UltimateRelativityConstants;

namespace AnoMech.Scenarios.Fru.UltimateRelativity;

public sealed class FruUltimateRelativityScenario : IScenario
{
    public string Name => "Ultimate Relativity";
    public float Duration => EndTime;
    public IPhase Phase => FruZone.P3;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new UltimateRelativityAi()];
    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private UltimateRelativityPattern pattern = null!;
    private SimEnemy? oracle;
    private readonly SimEnemy?[] hourglasses = new SimEnemy?[8];
    private readonly SimEnemy?[] lasers = new SimEnemy?[8];
    private readonly Vector3[] laserDirections = new Vector3[8];
    private readonly List<ISimObject> owned = [];
    private readonly List<SimTether> tethers = [];
    private readonly List<SimOmen>[] markers = [[], []];
    private readonly Dictionary<PartyRole, Vector3> rewinds = [];
    private readonly Dictionary<(PartyRole Role, ushort Status), float> deadlines = [];
    private bool running;
    public void Run(SimWorld worldParam, int? selectedAi) => Run(worldParam, UltimateRelativityPattern.Random(Random.Shared));
    internal void Run(SimWorld worldParam, UltimateRelativityPattern chosen)
    {
        world = worldParam; damage = new(world.Party); pattern = chosen; running = true;
        oracle = null; Array.Clear(hourglasses); Array.Clear(lasers); Array.Clear(laserDirections);
        owned.Clear(); tethers.Clear(); markers[0].Clear(); markers[1].Clear(); rewinds.Clear(); deadlines.Clear();
        At(0.5f, () => { oracle = Spawn(Oracle, OracleName, Vector3.Zero, true); if (oracle == null) Finish(); });
        At(1, () => oracle?.Cast(Raidwide, castSeconds: 9.7f));
        At(10.7f, () => { foreach (var m in Members()) Hit(m, Raidwide, "Ultimate Relativity", false); });
        At(11.8f, Assign);
        At(15, () => {
            oracle?.Cast(Speed, castSeconds: 5.3f);
            for (var clock = 0; clock < 8; clock++)
                if (UltimateRelativityPattern.Wave(clock) is 0 or 2)
                {
                    var fast = UltimateRelativityPattern.Wave(clock) == 0;
                    var tether = world.Tether(hourglasses[clock], oracle, fast ? FastTether : SlowTether);
                    tethers.Add(tether); Own(tether);
                }
        });
        At(20.3f, () => {
            foreach (var tether in tethers) tether.Despawn();
            for (var clock = 0; clock < 8; clock++)
                if (UltimateRelativityPattern.Wave(clock) is 0 or 2)
                    Effect(UltimateRelativityPattern.Wave(clock) == 0 ? Quicken : Slow, pattern.Hourglass(clock), hourglasses[clock]);
        });
        for (var wave = 0; wave < 3; wave++)
        {
            var w = wave;
            At(FireTime(w), () => ResolveFire(w));
            At(LaserStart(w), () => StartLasers(w));
            if (w < 2) At(LaserHit(w, 0), () => SnapshotReturn(w));
            for (var shot = 0; shot < 10; shot++)
            {
                var s = shot;
                At(LaserHit(w, s), () => ResolveLasers(w, s));
            }
        }
        At(30.6f, () => ClearMarkers(0));
        At(42.8f, () => ClearMarkers(1));
        At(StunTime, () => {
            foreach (var m in Members()) { Status(m, StunStatus, FinalTime); m.StopMoving(); }
            world.Party.Player?.SetMechanicInputLock(true);
        });
        At(RewindTime, () => {
            foreach (var (role, position) in rewinds)
            {
                var m = world.Party.Get(role);
                if (!m.IsAlive()) continue;
                Remove(m!, ReturnStatus); Effect(ReturnIV, position, m);
                var offset = position - m!.Position;
                if (offset.LengthSquared() < 0.0001f) continue;
                var source = m.Position - offset;
                if (m is SimPlayer player) player.Knockback(source, offset.Length(), offset.Length() / 0.4f);
                else if (m is SimPartyNpc bot) bot.Knockback(source, offset.Length(), offset.Length() / 0.4f);
            }
        });
        At(FinalTime, () => { ResolveFinal(); Unlock(); });
        At(55.8f, () => oracle?.Cast(ShellCast, targetId: world.Party.Get(pattern.ShellTarget)?.GameObjectId, castSeconds: 2.8f));
        At(ShellTime, () => {
            var target = world.Party.Get(pattern.ShellTarget);
            if (!target.IsAlive()) { FailParty(ShellHit, "missing Shell Crusher target"); return; }
            oracle?.Cast(ShellHit, targetId: target!.GameObjectId, targetLocation: target.Position, castSeconds: 0);
            Stack(target!, 6, 8, 8, ShellHit, "Shell Crusher", Members());
        });
        At(Duration, Finish);
        ((UltimateRelativityAi)AiStrats[0]).Run(pattern, world, () => running && oracle is { IsActive: true });
    }
    private void At(float time, Action action) => world.Events.Add(time, () => { if (running) action(); });
    private T Own<T>(T obj) where T : ISimObject { owned.Add(obj); return obj; }
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private SimCharacter? Member(RelativityAssignment assignment) => world.Party.Get(pattern.Role(assignment));
    private SimEnemy? Spawn(uint id, uint name, Vector3 position, bool targetable = false)
    {
        var actor = world.SpawnEnemy(new EnemySpawnConfig(id, NameId: name, Level: FruConstants.Level,
            Targetable: targetable, EnemyList: targetable ? EnemyListMode.Always : EnemyListMode.Never,
            Placement: new(position, MathF.PI)));
        if (actor != null) Own(actor);
        return actor;
    }
    private void Hit(SimCharacter m, uint action, string cause, bool lethal)
    { if (m.IsAlive()) damage.ApplyDamage(m, lethal ? 1 : 0.35f, action, cause, lethal); }
    private void FailParty(uint action, string cause) { foreach (var m in Members()) Hit(m, action, cause, true); }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null)
    {
        var helper = Spawn(FruConstants.BNpcBaseId.Helper, OracleName, position);
        helper?.Cast(action, targetLocation: position, targetId: target?.GameObjectId, castSeconds: 0);
        // Return's authored trace needs its source alive beyond an ordinary hit.
        if (helper != null && action is not Return and not ReturnIV) world.Events.Add(3, helper.Despawn);
    }
    private void Status(SimCharacter member, ushort status, float deadline)
    {
        if (!member.IsAlive() || member is not ISimPartyMember slot) return;
        member.AddStatus(status, MathF.Max(0.2f, deadline - world.Events.Elapsed));
        deadlines[(slot.Role, status)] = deadline;
    }
    private void Remove(SimCharacter member, ushort status)
    {
        member.RemoveStatus(status);
        if (member is ISimPartyMember slot) deadlines.Remove((slot.Role, status));
    }
    private void Assign()
    {
        foreach (var a in UltimateRelativityPattern.Assignments)
        {
            var m = Member(a); if (!m.IsAlive()) continue;
            Status(m!, a == pattern.Ice ? IceStatus : FireStatus, FireTime(a == pattern.Ice ? 1 : UltimateRelativityPattern.FireOrder(a)));
            Status(m!, UltimateRelativityPattern.Eye(a) ? EyeStatus : UltimateRelativityPattern.Eruption(a) ? EruptionStatus : WaterStatus, FinalTime);
            Status(m!, WaitingStatus, LaserHit(UltimateRelativityPattern.EarlyReturn(a) ? 0 : 1, 0));
            m!.AddVfx(WaitingClock);
        }
        for (var wave = 0; wave < 3; wave++)
            if (Member(pattern.Darkness(wave)) is { } m) Status(m, DarknessStatus, FireTime(wave));
        for (var clock = 0; clock < 8; clock++)
        {
            hourglasses[clock] = Spawn(Hourglass, HourglassName, pattern.Hourglass(clock));
            lasers[clock] = Spawn(FruConstants.BNpcBaseId.Helper, OracleName, pattern.Hourglass(clock));
            // Hourglasses remain visible after firing, until scenario cleanup or reset.
            Own(world.SpawnMapEffect(pattern.HourglassSlot(clock), FruConstants.MapEffect.Show, FruConstants.MapEffect.Hide));
        }
        if (hourglasses.Any(h => h == null) || lasers.Any(h => h == null)) Finish();
    }
    private void ResolveFire(int wave)
    {
        var members = Members();
        var fires = UltimateRelativityPattern.Assignments.Where(a => pattern.Fire(a, wave)).Select(Member).ToArray();
        var target = Member(pattern.Darkness(wave));
        if (fires.Any(f => !f.IsAlive()) || !target.IsAlive()) { FailParty(Fire, "missing fire / Unholy Darkness target"); return; }
        // Snapshot all simultaneous centers before damage can kill a target.
        var centers = fires.Select(f => (Actor: f!, Position: f!.Position)).ToArray();
        foreach (var (actor, pos) in centers)
        {
            Remove(actor, FireStatus); Effect(Fire, pos, actor);
            foreach (var m in members)
                if (Vector3.Distance(m.Position, pos) <= 8) Hit(m, Fire, "Dark Fire III overlap", m != actor);
        }
        if (wave == 1)
        {
            var ice = Member(pattern.Ice);
            if (!ice.IsAlive()) { FailParty(Ice, "missing Dark Blizzard target"); return; }
            var pos = ice!.Position; Remove(ice, IceStatus); Effect(Ice, pos, ice);
            foreach (var m in members)
            {
                var distance = Vector3.Distance(m.Position, pos);
                if (distance >= 3 && distance <= 12) Hit(m, Ice, "Dark Blizzard III donut", true);
            }
        }
        Remove(target!, DarknessStatus); Effect(Darkness, target!.Position, target);
        Stack(target, 6, 5, 8, Darkness, "Unholy Darkness", members);
    }
    private void Stack(SimCharacter target, float radius, int min, int max, uint action, string cause, SimCharacter[] members)
    {
        var group = members.Where(m => m.IsAlive() && Vector3.Distance(m.Position, target.Position) <= radius).ToArray();
        if (!target.IsAlive() || group.Length < min || group.Length > max)
        { FailParty(action, $"{group.Length}/{min} in {cause}"); return; }
        foreach (var m in group) Hit(m, action, cause, false);
    }
    private void StartLasers(int wave)
    {
        for (var clock = 0; clock < 8; clock++)
        {
            if (UltimateRelativityPattern.Wave(clock) != wave) continue;
            var actor = hourglasses[clock];
            actor?.AddStatus(RotationStatus, LaserHit(wave, 0) - world.Events.Elapsed,
                pattern.Clockwise(clock) ? 0x15C : 0x10D, overrideStacks: true);
            actor?.Cast(MeltdownCast, castSeconds: LaserHit(wave, 0) - world.Events.Elapsed);
        }
    }
    private void ResolveLasers(int wave, int shot)
    {
        var members = Members();
        var hits = new List<(SimCharacter Actor, bool Lethal)>();
        for (var clock = 0; clock < 8; clock++)
        {
            if (UltimateRelativityPattern.Wave(clock) != wave) continue;
            var origin = pattern.Hourglass(clock);
            SimCharacter? bait = null;
            if (shot == 0)
            {
                bait = members.OrderBy(m => Vector3.DistanceSquared(m.Position, origin)).FirstOrDefault();
                if (bait == null) continue;
                var offset = bait.Position - origin;
                laserDirections[clock] = offset.LengthSquared() > 0.0001f ? Vector3.Normalize(offset) : -pattern.Direction(clock);
                hourglasses[clock]?.RemoveStatus(RotationStatus);
            }
            var dir = UltimateRelativityPattern.Rotate(laserDirections[clock], (pattern.Clockwise(clock) ? 1 : -1) * shot * MathF.PI / 12);
            var helper = lasers[clock];
            helper?.SetRotation(MathF.Atan2(dir.X, dir.Z));
            helper?.Cast(shot == 0 ? MeltdownFirst : MeltdownRest, castSeconds: 0);
            foreach (var m in members)
                if (UltimateRelativityPattern.InsideBeam(m.Position, origin, dir, shot == 0 ? 60 : 50)) hits.Add((m, m != bait));
        }
        foreach (var group in hits.GroupBy(h => h.Actor))
            Hit(group.Key, shot == 0 ? MeltdownFirst : MeltdownRest,
                shot == 0 ? "Sinbound Meltdown bait overlap" : "Sinbound Meltdown rotating beam", group.Count() > 1 || group.Any(h => h.Lethal));
    }
    private void SnapshotReturn(int wave)
    {
        foreach (var a in UltimateRelativityPattern.Assignments.Where(a => UltimateRelativityPattern.EarlyReturn(a) == (wave == 0)))
        {
            var m = Member(a); if (!m.IsAlive()) continue;
            rewinds[pattern.Role(a)] = m!.Position;
            m.RemoveVfx(WaitingClock); Remove(m, WaitingStatus); Status(m, ReturnStatus, StunTime);
            markers[wave].Add(Own(world.SpawnOmen(ReturnMarker, new(m.Position, 0), Vector3.One)));
            Effect(Return, m.Position, m);
        }
    }
    private void ClearMarkers(int wave) { foreach (var marker in markers[wave]) marker.Despawn(); markers[wave].Clear(); }
    private void ResolveFinal()
    {
        var members = Members();
        var eyes = UltimateRelativityPattern.Assignments.Where(UltimateRelativityPattern.Eye).Select(Member).ToArray();
        var eruptions = UltimateRelativityPattern.Assignments.Where(UltimateRelativityPattern.Eruption).Select(Member).ToArray();
        var water = Member(RelativityAssignment.MediumDps);
        if (eyes.Concat(eruptions).Append(water).Any(m => !m.IsAlive()))
        { FailParty(Water, "missing Return resolution target"); return; }
        var eyePositions = eyes.Select(e => (Actor: e!, Position: e!.Position)).ToArray();
        var eruptionPositions = eruptions.Select(e => (Actor: e!, Position: e!.Position)).ToArray();
        foreach (var (eye, pos) in eyePositions)
        {
            Remove(eye, EyeStatus); Effect(Shadoweye, pos, eye);
            foreach (var m in members)
            {
                if (m == eye) continue;
                var offset = pos - m.Position;
                var forward = new Vector3(MathF.Sin(m.Rotation), 0, MathF.Cos(m.Rotation));
                if (offset.LengthSquared() < 0.0001f || Vector3.Dot(forward, Vector3.Normalize(offset)) >= MathF.Sqrt(0.5f))
                    Hit(m, Shadoweye, "Shadoweye: look away before Return", true);
            }
        }
        foreach (var (actor, pos) in eruptionPositions)
        {
            Remove(actor, EruptionStatus); Effect(Eruption, pos, actor);
            foreach (var m in members)
                if (Vector3.Distance(m.Position, pos) <= 6) Hit(m, Eruption, "Dark Eruption after Return", m != actor);
        }
        Remove(water!, WaterStatus); Effect(Water, water!.Position, water);
        Stack(water, 6, 4, 4, Water, "Dark Water III after Return", members);
    }
    private void Unlock()
    {
        foreach (var role in UltimateRelativityPattern.Roles)
            if (world.Party.Get(role) is { } member) Remove(member, StunStatus);
        world.Party.Player?.SetMechanicInputLock(false);
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || oracle is not { IsActive: true }) return;
        var time = world.Events.Elapsed;
        for (var clock = 0; clock < 8; clock++)
        {
            var remaining = LaserHit(UltimateRelativityPattern.Wave(clock), 0) - time;
            if (remaining > 0) hourglasses[clock]?.FindStatus(RotationStatus)?.Reapply(MathF.Max(0.2f, remaining), 0);
        }
        foreach (var (key, deadline) in deadlines.ToArray())
        {
            var member = world.Party.Get(key.Role);
            if (!member.IsAlive() || time >= deadline)
            {
                member?.RemoveStatus(key.Status); deadlines.Remove(key);
                if (key.Status == WaitingStatus) member?.RemoveVfx(WaitingClock);
            }
            else member!.FindStatus(key.Status)?.Reapply(MathF.Max(0.2f, deadline - time), 0);
        }
        foreach (var m in Members())
            if (new Vector2(m.Position.X, m.Position.Z).Length() > 20) Hit(m, Raidwide, "arena boundary", true);
    }
    private void Finish()
    {
        running = false; Unlock(); deadlines.Clear(); rewinds.Clear();
        foreach (var role in UltimateRelativityPattern.Roles)
            if (world.Party.Get(role) is { } m)
            {
                m.RemoveVfx(WaitingClock);
                foreach (var status in Statuses) m.RemoveStatus(status);
                m.StopMoving();
            }
        foreach (var obj in owned) obj.Despawn();
    }
}
