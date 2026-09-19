using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using Ct = AnoMech.Scenarios.Fru.CrystallizeTime.CrystallizeTimeConstants;
using Lr = AnoMech.Scenarios.Fru.LightRampant.LightRampantConstants;
using Apoc = AnoMech.Scenarios.Fru.Apocalypse.ApocalypseConstants;
using static AnoMech.Scenarios.Fru.DarklitDragonsong.DarklitConstants;

namespace AnoMech.Scenarios.Fru.DarklitDragonsong;

public sealed partial class FruDarklitDragonsongScenario : IScenario
{
    public string Name => "Darklit Dragonsong";
    public float Duration => EndTime;
    public IPhase Phase => FruZone.P4;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new DarklitAi()];
    internal bool PlayerTakesSomberDance;
    private PartyRole somberTank;
    public void DrawSettings() => DrawSomberSettings();
    partial void DrawSomberSettings();
    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private DarklitPattern pattern = null!;
    private SimEnemy? usurper, oracle, fragment;
    private readonly List<ISimObject> owned = [];
    private readonly List<SimTether> chains = [];
    private readonly List<SimMapEffect> towers = [];
    private readonly List<(Vector3 Position, SimEnemy Actor)> puddles = [];
    private readonly Dictionary<(PartyRole Role, ushort Status), float> deadlines = [];
    private bool running, baitEast, jumping;
    private Vector3 spiritPosition, farPosition, jumpStart, jumpEnd;
    private float jumpTime;
    private PartyRole farTarget;
    public void Run(SimWorld worldParam, int? selectedAi) => Run(worldParam, DarklitPattern.Random(Random.Shared));
    internal void Run(SimWorld worldParam, DarklitPattern chosen)
    {
        world = worldParam; damage = new(world.Party); pattern = chosen;
        // Default to a bot, even when the player occupies the MT slot.
        // Player participation is opt-in and requires a tank role.
        somberTank = PlayerTakesSomberDance && world.Party.Get(PartyRole.MainTank) is SimPlayer
            ? PartyRole.MainTank
            : PlayerTakesSomberDance && world.Party.Get(PartyRole.OffTank) is SimPlayer
                ? PartyRole.OffTank
                : world.Party.Get(PartyRole.MainTank) is SimPartyNpc ? PartyRole.MainTank : PartyRole.OffTank;
        owned.Clear(); chains.Clear(); towers.Clear(); puddles.Clear(); deadlines.Clear();
        running = true; jumping = false; baitEast = false; usurper = oracle = fragment = null;
        At(0.5f, SpawnBosses);
        At(1.1f, () => Own(world.SpawnMapEffect(Ct.FragmentSlot, FruConstants.MapEffect.Show, FruConstants.MapEffect.Hide)));
        At(6.6f, () => {
            foreach (var m in Members()) {
                var actor = Spawn(FruConstants.BNpcBaseId.Helper, Ct.UsurperName, m.Position);
                if (actor == null) { Finish(); return; }
                puddles.Add((m.Position, actor));
                actor.Cast(AkhRhaiCast, targetLocation: m.Position, castSeconds: 2.7f);
            }
        });
        for (var hit = 0; hit < 11; hit++) {
            var repeat = hit > 0;
            At(9.3f + hit * 0.4f, () => {
                foreach (var (position, actor) in puddles) {
                    // Let the initial cast release its column VFX. Replacing it
                    // with a hit-only action on the same frame cancels that VFX.
                    if (repeat) actor.Cast(AkhRhaiHit, targetLocation: position, castSeconds: 0);
                    Circle(position, 4, AkhRhaiHit, "Akh Rhai puddle");
                }
            });
        }
        // Begin Redress half a second before the first Akh Rhai hit at 9.3s.
        // The full 154-frame Redress animation must finish before Darklit casts.
        At(TransformTime, TransformUsurper);
        At(13.7f, () => { foreach (var p in puddles) p.Actor.Despawn(); });
        At(14.2f, () => { oracle?.SetVisible(true); oracle?.SetTargetable(true); });
        At(16.4f, () => {
            usurper?.Cast(Raidwide, castSeconds: 4.7f); oracle?.Cast(OracleRaidwide, castSeconds: 4.7f);
            foreach (var role in Waters()) world.Party.Get(role)?.AddVfx(Apoc.WaterMarker, persistent: false);
        });
        At(21.2f, () => { foreach (var m in Members()) Hit(m, Raidwide, "Darklit Dragonsong", false); });
        At(21.6f, () => {
            foreach (var role in Waters()) if (world.Party.Get(role) is { } m && m.IsAlive()) {
                Status(m, Ct.WaterStatus, WaterTime); m.AddVfx(Apoc.WaterWaitingClock);
            }
        });
        At(22.3f, () => {
            foreach (var m in Members()) Status(m, Lr.Lightsteeped, 34.3f, 3);
            SetChains(false);
        });
        At(24.6f, () => {
            usurper?.Cast(PathCast, castSeconds: 7.7f);
            // Native two-person towers, b1845, world positions (100,0,92/108).
            for (byte slot = 42; slot <= 43; slot++) towers.Add(Own(world.SpawnMapEffect(slot, FruConstants.MapEffect.Show, FruConstants.MapEffect.Hide)));
        });
        At(ChainStart, () => SetChains(true));
        At(TowerTime, ResolveTowersAndCones);
        At(32.6f, () => oracle?.Cast(Ct.SpiritTaker, castSeconds: 2.7f));
        At(SpiritSnapshot, () => {
            var target = world.Party.Get(pattern.SpiritTarget);
            if (!target.IsAlive()) { Wipe(Ct.SpiritHit, "missing Spirit Taker target"); return; }
            spiritPosition = target!.Position;
            foreach (var role in Waters()) if (world.Party.Get(role) is { } m) {
                m.RemoveVfx(Apoc.WaterWaitingClock);
                if (m.IsAlive()) m.AddVfx(Apoc.WaterClock, persistent: false);
            }
        });
        // Usurper faces north: her right wing is the east half. Native
        // ActionCastVFX 151/152 supplies the corresponding wing's warning.
        At(35.7f, () => usurper?.Cast(pattern.EastWing ? WingRight : WingLeft, castSeconds: 4.8f));
        At(36.1f, () => Jump(Ct.SpiritHit, world.Party.Get(pattern.SpiritTarget), spiritPosition));
        At(SpiritTime, () => Circle(spiritPosition, 5, Ct.SpiritHit, "Spirit Taker spread", pattern.SpiritTarget));
        At(38.8f, () => oracle?.Cast(SomberCast, castSeconds: 4.7f));
        At(WaterTime, ResolveWaterAndWing);
        At(ChainEnd, ClearChains);
        At(FarSnapshot, () => {
            var target = Members().OrderByDescending(m => Vector3.DistanceSquared(m.Position, oracle!.Position)).FirstOrDefault();
            if (target is not ISimPartyMember slot) { Finish(); return; }
            farTarget = slot.Role; farPosition = target.Position;
        });
        At(FarTime, () => {
            var target = world.Party.Get(farTarget);
            Jump(SomberFar, target, target?.Position ?? farPosition);
            ResolveDance(farTarget, farPosition, SomberFar);
        });
        At(NearTime, () => {
            var target = Members().OrderBy(m => Vector3.DistanceSquared(m.Position, oracle!.Position)).FirstOrDefault();
            if (target is not ISimPartyMember slot) { Wipe(SomberNear, "missing Somber Dance target"); return; }
            var pos = target.Position; Jump(SomberNear, target, pos); ResolveDance(slot.Role, pos, SomberNear);
        });
        At(51.9f, () => { usurper?.SetVisible(false); oracle?.SetVisible(false); });
        At(52.2f, () => {
            usurper?.SetPosition(DarklitPattern.Convert(0, -6)); oracle?.SetPosition(DarklitPattern.Convert(0, 6));
            usurper?.SetVisible(true); oracle?.SetVisible(true);
        });
        At(53.2f, () => {
            usurper?.Cast(Ct.AkhMornUsurper, targetId: world.Party.Get(PartyRole.MainTank)?.GameObjectId, castSeconds: 3.7f);
            oracle?.Cast(Ct.AkhMornOracle, targetId: world.Party.Get(PartyRole.OffTank)?.GameObjectId, castSeconds: 3.7f);
        });
        for (var hit = 0; hit < 4; hit++) At(58.6f + hit * 0.6f, ResolveAkhMorn);
        At(Duration, Finish);
        ((DarklitAi)AiStrats[0]).Run(pattern, world, () => running && usurper is { IsActive: true }, () => oracle, () => baitEast, somberTank);
    }
    private void At(float time, Action action) => world.Events.Add(time, () => { if (running && (usurper == null || usurper.IsActive)) action(); });
    private T Own<T>(T obj) where T : ISimObject { owned.Add(obj); return obj; }
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private PartyRole[] Waters() => [pattern.TetherWater, pattern.BaitWater];
    private SimEnemy? Spawn(uint id, uint name, Vector3 position, bool targetable = false, bool hostile = true, uint model = 0, ushort spawnTimeline = 0)
    {
        var actor = world.SpawnEnemy(new EnemySpawnConfig(id, NameId: name, Level: FruConstants.Level,
            Targetable: targetable, EnemyList: id == Ct.Usurper ? EnemyListMode.Manual : targetable && hostile ? EnemyListMode.Always : EnemyListMode.Never,
            IsHostile: hostile, ModelCharaId: model, SpawnTimeline: spawnTimeline, Placement: new(position, MathF.PI)));
        if (actor != null) { Own(actor); if (id == Ct.Usurper) actor.SetVisibleInEnemyList(targetable); }
        return actor;
    }
    private void SpawnBosses()
    {
        usurper = Spawn(Ct.Usurper, Ct.UsurperName, Vector3.Zero, true, model: OriginalUsurperModel);
        oracle = Spawn(Ct.Oracle, Ct.OracleName, DarklitPattern.Convert(-10.5f, 0));
        fragment = Spawn(Ct.Fragment, Ct.FragmentName, Ct.FragmentPosition, true, false);
        if (usurper == null || oracle == null || fragment == null) { Finish(); return; }
        oracle.SetVisible(false); fragment.SetHealth(Ct.FragmentMaxHealth, Ct.FragmentMaxHealth);
        usurper.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        oracle.SetTarget(world.Party.Get(PartyRole.OffTank), follow: false);
        foreach (var (id, name, offset) in new[] { (Ct.VisionOfRyne, Ct.RyneName, -0.65f), (Ct.VisionOfGaia, Ct.GaiaName, 0.65f) }) {
            var vision = Spawn(id, name, Ct.FragmentPosition + new Vector3(offset, 0, 0), hostile: false);
            vision?.SetVisualHeight(Ct.FragmentVisionHeight); vision?.SetRotation(offset < 0 ? MathF.PI / 2 : -MathF.PI / 2);
        }
    }
    private void TransformUsurper()
    {
        if (usurper is not { IsActive: true } original) { Finish(); return; }
        var transformed = Spawn(Ct.Usurper, Ct.UsurperName, original.Position, true,
            model: Ct.UsurperDragonModel, spawnTimeline: TransformInTimeline);
        if (transformed == null) { Finish(); return; }
        transformed.SetRotation(original.Rotation);
        transformed.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        original.SetTargetable(false);
        original.SetVisibleInEnemyList(false);
        original.PlayActionTimeline(TransformOutTimeline);
        usurper = transformed;
        // Keep the outgoing actor alive for the entire native Redress sequence.
        // Both actors are world-owned, so an interrupted reset releases either.
        world.Events.Add(TransformCleanupTime - TransformTime, original.Despawn);
    }
    private void Hit(SimCharacter member, uint action, string cause, bool lethal)
    { if (member.IsAlive()) damage.ApplyDamage(member, lethal ? 1 : 0.35f, action, cause, lethal); }
    private void Wipe(uint action, string cause) { foreach (var m in Members()) Hit(m, action, cause, true); }
    private void CheckFragment(Vector3 position, float radius, uint action)
    {
        if (Vector3.Distance(position, Ct.FragmentPosition) > radius) return;
        fragment?.SetHealth(0, Ct.FragmentMaxHealth); Wipe(action, "Fragment of Fate was hit");
    }
    private void Circle(Vector3 position, float radius, uint action, string cause, PartyRole? allowed = null)
    {
        CheckFragment(position, radius, action);
        foreach (var m in Members()) if (Vector3.Distance(m.Position, position) <= radius)
            Hit(m, action, cause, m is not ISimPartyMember slot || slot.Role != allowed);
    }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null, Vector3? direction = null)
    {
        var helper = Spawn(FruConstants.BNpcBaseId.Helper, Ct.UsurperName, position);
        if (direction is { } dir) helper?.SetRotation(MathF.Atan2(dir.X, dir.Z));
        helper?.Cast(action, targetId: target?.GameObjectId, castSeconds: 0);
        if (helper != null) world.Events.Add(3, helper.Despawn);
    }
    private void Status(SimCharacter m, ushort id, float end, int param = 1)
    {
        if (!m.IsAlive() || m is not ISimPartyMember slot) return;
        m.AddStatus(id, end - world.Events.Elapsed, param, overrideStacks: true); deadlines[(slot.Role, id)] = end;
    }
    private void ClearChains()
    {
        foreach (var c in chains) c.Despawn(); chains.Clear();
        foreach (var m in Members()) { m.RemoveStatus(Lr.Chains); m.RemoveStatus(Lr.Curse); }
    }
    private void SetChains(bool locked)
    {
        ClearChains();
        for (var i = 0; i < 4; i++) {
            var m = world.Party.Get(pattern.Link(i)); var other = world.Party.Get(pattern.Link(i + 1));
            if (!m.IsAlive() || !other.IsAlive()) { Wipe(Lr.RefulgentFate, "missing chain target"); return; }
            Status(m!, locked ? Lr.Curse : Lr.Chains, locked ? ChainEnd : ChainStart);
            chains.Add(Own(world.Tether(m, other, locked ? Lr.CurseTether : Lr.ChainsTether)));
        }
    }
    private void AddLight(SimCharacter m)
    {
        var count = (m.FindStatus(Lr.Lightsteeped)?.Stacks ?? 0) + 1;
        Status(m, Lr.Lightsteeped, 34.3f, count);
        if (count >= 5) Wipe(Lr.Lightsteep, "five Lightsteeped stacks");
    }
    private void ResolveTowersAndCones()
    {
        var members = Members();
        var baits = members.OrderBy(m => m.Position.LengthSquared()).Take(4).ToArray();
        var directions = baits.Select(m => m.Position.LengthSquared() > 0.0001f ? Vector3.Normalize(m.Position) : Vector3.UnitZ).ToArray();
        foreach (var tower in towers) tower.Despawn();
        foreach (var z in new[] { -8f, 8f }) {
            var pos = new Vector3(0, 0, z); Effect(Lr.BrightHunger, pos);
            var occupants = members.Where(m => Vector3.Distance(m.Position, pos) <= 4).ToArray();
            if (occupants.Length != 2) { Wipe(Lr.BrightHunger, $"{occupants.Length}/2 in Bright Hunger tower"); return; }
            foreach (var m in occupants) { Hit(m, Lr.BrightHunger, "Bright Hunger", false); AddLight(m); }
        }
        for (var i = 0; i < baits.Length; i++) {
            Effect(PathHit, Vector3.Zero, direction: directions[i]);
            foreach (var m in members.Where(m => DarklitPattern.InsideCone(m.Position, Vector3.Zero, directions[i]))) {
                Hit(m, PathHit, "Path of Light protean overlap", m != baits[i]); AddLight(m);
            }
        }
    }
    private void Jump(uint action, SimCharacter? target, Vector3 position)
    {
        if (!target.IsAlive() || oracle is not { IsActive: true }) { Wipe(action, "missing jump target"); return; }
        jumpStart = oracle.Position; var offset = position - jumpStart;
        jumpEnd = offset.Length() <= 15 / 2.358f ? jumpStart : position - Vector3.Normalize(offset) * (15 / 2.358f);
        jumpTime = world.Events.Elapsed; jumping = true;
        oracle.Cast(action, targetId: target!.GameObjectId, targetLocation: position, castSeconds: 0);
    }
    private void ResolveWaterAndWing()
    {
        var members = Members();
        foreach (var role in Waters()) {
            var target = world.Party.Get(role);
            if (!target.IsAlive()) { Wipe(Ct.Water, "missing Dark Water target"); return; }
            target!.RemoveStatus(Ct.WaterStatus); target.RemoveVfx(Apoc.WaterWaitingClock);
            Effect(Ct.Water, target.Position, target); CheckFragment(target.Position, 6, Ct.Water);
            Stack(target, 6, Ct.Water, "Dark Water III", members);
        }
        foreach (var m in members) if (pattern.EastWing ? m.Position.X >= 0 : m.Position.X <= 0)
            Hit(m, pattern.EastWing ? WingRight : WingLeft, "Hallowed Wings", true);
        var x = oracle?.Position.X ?? 0;
        baitEast = x < -4 / 2.358f || MathF.Abs(x) < 4 / 2.358f && pattern.EastWing;
    }
    private void ResolveDance(PartyRole role, Vector3 position, uint action)
    {
        if (role != somberTank) { Wipe(action, "Somber Dance must be baited by the assigned tank for both hits"); return; }
        Circle(position, 8, action, "Somber Dance tankbuster overlap", role);
    }
    private void Stack(SimCharacter target, float radius, uint action, string cause, SimCharacter[] members, int required = 4)
    {
        var group = members.Where(m => m.IsAlive() && Vector3.Distance(m.Position, target.Position) <= radius).ToArray();
        if (group.Length != required) { Wipe(action, $"{group.Length}/{required} in {cause}"); return; }
        foreach (var m in group) Hit(m, action, cause, false);
    }
    private void ResolveAkhMorn()
    {
        var members = Members();
        foreach (var role in new[] { PartyRole.MainTank, PartyRole.OffTank }) {
            var target = world.Party.Get(role); var action = role == PartyRole.MainTank ? Ct.AkhHitUsurper : Ct.AkhHitOracle;
            if (!target.IsAlive()) { Wipe(action, "missing Akh Morn tank"); return; }
            Effect(action, target!.Position, target); CheckFragment(target.Position, 4, action);
            // Seven share with MT; OT survives the other stack alone.
            Stack(target, 4, action, "Akh Morn", members, role == PartyRole.OffTank ? 1 : 7);
        }
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || usurper is not { IsActive: true }) return;
        if (oracle is not { IsActive: true } || fragment is not { IsActive: true }) { Finish(); return; }
        var time = world.Events.Elapsed;
        if (jumping) {
            var t = Math.Clamp((time - jumpTime) / 0.5f, 0, 1);
            oracle.SetPosition(Vector3.Lerp(jumpStart, jumpEnd, t * t * (3 - 2 * t))); if (t >= 1) jumping = false;
        }
        foreach (var (key, end) in deadlines.ToArray()) {
            var m = world.Party.Get(key.Role);
            if (!m.IsAlive() || time >= end) { m?.RemoveStatus(key.Status); deadlines.Remove(key); }
            else m!.FindStatus(key.Status)?.Reapply(MathF.Max(0.2f, end - time), 0);
        }
        foreach (var role in Waters()) if (world.Party.Get(role) is { } m && (!m.IsAlive() || !m.HasStatus(Ct.WaterStatus))) m.RemoveVfx(Apoc.WaterWaitingClock);
        if (time >= ChainStart && time < ChainEnd) for (var i = 0; i < 4; i++) {
            var a = world.Party.Get(pattern.Link(i)); var b = world.Party.Get(pattern.Link(i + 1));
            var distance = Vector3.Distance(a?.Position ?? Vector3.Zero, b?.Position ?? Vector3.Zero);
            if (!a.IsAlive() || !b.IsAlive() || distance < ChainMin || distance > ChainMax) {
                Wipe(Lr.RefulgentFate, "Everlasting Light chain broke"); break;
            }
        }
        foreach (var m in Members()) if (new Vector2(m.Position.X, m.Position.Z).Length() > 20) Hit(m, Raidwide, "arena boundary", true);
    }
    private void Finish()
    {
        running = jumping = false; ClearChains(); deadlines.Clear();
        foreach (var role in DarklitPattern.Roles) if (world.Party.Get(role) is { } m) {
            m.RemoveVfx(Apoc.WaterWaitingClock);
            foreach (var status in new[] { Ct.WaterStatus, Lr.Lightsteeped, Lr.Chains, Lr.Curse }) m.RemoveStatus(status);
            m.StopMoving();
        }
        foreach (var obj in owned) obj.Despawn();
    }
}
