using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.LightRampant.LightRampantConstants;

namespace AnoMech.Scenarios.Fru.LightRampant;

public sealed class FruLightRampantScenario : IScenario
{
    public string Name => "Light Rampant";
    public float Duration => 48f;
    public IPhase Phase => FruZone.P2;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new LightRampantAi()];
    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private LightRampantPattern pattern = null!;
    private SimEnemy? boss;
    private readonly List<ISimObject> owned = [];
    private readonly List<SimTether> tethers = [];
    private readonly List<SimMapEffect> towers = [];
    private readonly List<(Vector3 Position, float ArmAt, float EndAt, SimEventObject? Visual)> puddles = [];
    private readonly Dictionary<SimCharacter, Vector3> previous = [];
    private bool running, assigned, cursed;
    public void Run(SimWorld worldParam, int? selectedAi) => Run(worldParam, LightRampantPattern.Random(Random.Shared));
    internal void Run(SimWorld worldParam, LightRampantPattern chosen)
    {
        world = worldParam; damage = new(world.Party); pattern = chosen;
        owned.Clear(); tethers.Clear(); towers.Clear(); puddles.Clear(); previous.Clear();
        boss = null; running = true; assigned = cursed = false;
        At(0.5f, () => {
            boss = Spawn(Usurper, Vector3.Zero, true);
            if (boss == null) { Finish(); return; }
            boss.SetRotation(MathF.PI);
        });
        At(3, () => boss?.Cast(Raidwide, castSeconds: 5));
        At(8, () => { foreach (var member in Members()) Hit(member, Raidwide, "Light Rampant", false); });
        At(8.75f, Assign);
        At(11, () => {
            boss?.SetVisible(false); boss?.SetTargetable(false);
            for (var i = 0; i < 6; i++) ShowTower(i);
        });
        for (var wave = 0; wave < 5; wave++) At(15.6f + wave * 1.6f, DropPuddles);
        At(TowerTime, () => {
            ClearTowers();
            for (var i = 0; i < 6; i++) ResolveTower(LightRampantPattern.Tower(i), 1);
            if (running) SetChains(true);
        });
        At(20, () => SpawnOrbs(true));
        At(23, () => SpawnOrbs(false));
        At(PowerfulTime, () => { PowerfulStacks(); if (running) ShowTower(6); });
        At(FirstOrbTime, () => OrbHit(true));
        At(27, ClearChains);
        At(SecondOrbTime, () => { OrbHit(false); boss?.SetVisible(true); boss?.SetTargetable(true); });
        At(32, () => boss?.Cast(pattern.Pairs ? BanishPairs : BanishSpread, castSeconds: 5));
        At(CenterTime, () => { ClearTowers(); ResolveTower(Vector3.Zero, 4); });
        At(BanishTime, Banish);
        At(40, () => boss?.Cast(HouseCast, castSeconds: 5));
        At(HouseTime, House);
        At(Duration, Finish);
        ((LightRampantAi)AiStrats[0]).Run(pattern, world, () => running && boss is { IsActive: true }, Stacks);
    }
    private void At(float time, Action action) => world.Events.Add(time, () => { if (running) action(); });
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(Lightsteeped)?.Stacks ?? 0;
    private void Hit(SimCharacter member, uint action, string cause, bool lethal)
    { if (member.IsAlive()) damage.ApplyDamage(member, lethal ? 1 : 0.35f, action, cause, lethal); }
    private void Wipe(uint action, string cause)
    {
        Effect(action, Vector3.Zero);
        foreach (var member in Members()) Hit(member, action, cause, true);
        Finish();
    }
    private void AddLight(SimCharacter member)
    {
        if (!member.IsAlive()) return;
        member.AddStatus(Lightsteeped, MathF.Max(1, 48 - world.Events.Elapsed));
        if (member.FindStatus(Lightsteeped) is { Stacks: >= 5 }) Wipe(Lightsteep, "five Lightsteeped stacks");
    }
    private SimEnemy? Spawn(uint id, Vector3 position, bool bossActor = false)
    {
        var actor = world.SpawnEnemy(new EnemySpawnConfig(id, NameId: bossActor ? UsurperName : 0,
            Level: FruConstants.Level, Targetable: bossActor,
            EnemyList: bossActor ? EnemyListMode.OnlyWhenVisible : EnemyListMode.Never,
            Placement: new(position, 0), ModelCharaId: bossActor ? LightModel : 0));
        if (actor != null) owned.Add(actor);
        return actor;
    }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null, float rotation = 0)
    {
        var helper = Spawn(FruConstants.BNpcBaseId.Helper, position);
        helper?.SetRotation(rotation);
        helper?.Cast(action, targetLocation: position, targetId: target?.GameObjectId, castSeconds: 0);
        if (helper != null) world.Events.Add(3, helper.Despawn);
    }
    private void Assign()
    {
        assigned = true;
        foreach (var role in LightRampantPattern.Roles)
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
            var count = pattern.InitialStacks(role);
            if (count > 0) member.AddStatus(Lightsteeped, 39.25f, count, overrideStacks: true);
            if (pattern.Puddle(role)) member.AddVfx(PuddleMarker);
        }
        foreach (var role in pattern.Weights) world.Party.Get(role)?.AddStatus(Weight, PowerfulTime - 8.75f);
        SetChains(false);
    }
    private void SetChains(bool dangerous)
    {
        ClearChains(); cursed = dangerous;
        for (var i = 0; i < 6; i++)
        {
            var member = world.Party.Get(pattern.Slot(i));
            member?.AddStatus(dangerous ? Curse : Chains, (dangerous ? 27 : TowerTime) - world.Events.Elapsed);
            var tether = world.Tether(member, world.Party.Get(pattern.Slot((i + 1) % 6)), dangerous ? CurseTether : ChainsTether);
            tethers.Add(tether); owned.Add(tether);
        }
    }
    private void ClearChains()
    {
        cursed = false;
        foreach (var tether in tethers) tether.Despawn();
        tethers.Clear();
        foreach (var role in LightRampantPattern.Roles)
        { world.Party.Get(role)?.RemoveStatus(Chains); world.Party.Get(role)?.RemoveStatus(Curse); }
    }
    private void ShowTower(int index)
    {
        // ContentDirectorManagedSG 181: solo towers 9..14 (b1844), center 21 (b1846).
        // Both use mode 1 activation and the standard mode 4 hide action.
        var tower = world.SpawnMapEffect((byte)(index == 6 ? 21 : 9 + index), 0x00010001, 0x00040004);
        towers.Add(tower); owned.Add(tower);
    }
    private void ClearTowers() { foreach (var tower in towers) tower.Despawn(); towers.Clear(); }
    private void ResolveTower(Vector3 position, int required)
    {
        if (!running) return;
        var occupants = Members().Where(m => Vector3.Distance(m.Position, position) <= 4).ToArray();
        Effect(BrightHunger, position);
        if (occupants.Length != required) { Wipe(BrightHunger, $"{occupants.Length}/{required} in Bright Hunger tower"); return; }
        foreach (var member in occupants) { Hit(member, BrightHunger, "Bright Hunger", false); AddLight(member); }
    }
    private void DropPuddles()
    {
        var members = Members();
        foreach (var index in new[] { 6, 7 })
        {
            var target = world.Party.Get(pattern.Slot(index));
            if (!target.IsAlive()) { Wipe(DeathExplosion, "missing Luminous Hammer bait"); return; }
            target!.RemoveVfx(PuddleMarker);
            var position = target.Position;
            Effect(LuminousHammer, position, target);
            foreach (var member in members)
                if (Vector3.Distance(member.Position, position) <= 6)
                    Hit(member, LuminousHammer, "Luminous Hammer bait overlap", member != target);
            var visual = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = HolyPuddle, Placement = new(position, 0), TimelineState = 1 });
            if (visual != null) owned.Add(visual);
            puddles.Add((position, world.Events.Elapsed + 1.2f, world.Events.Elapsed + 11, visual));
        }
    }
    private void PowerfulStacks()
    {
        var targets = pattern.Weights.Select(r => world.Party.Get(r)).ToArray();
        if (targets.Any(t => !t.IsAlive())) { Wipe(DeathExplosion, "missing Powerful Light target"); return; }
        var centers = targets.Select(t => t!.Position).ToArray();
        var members = Members();
        foreach (var target in targets) target!.RemoveStatus(Weight);
        for (var i = 0; i < 2; i++)
        {
            Effect(PowerfulLight, centers[i], targets[i]);
            var group = members.Where(m => Vector3.Distance(m.Position, centers[i]) <= 5).ToArray();
            if (group.Length != 4 || group.Any(m => Vector3.Distance(m.Position, centers[1 - i]) <= 5))
            { Wipe(PowerfulLight, $"{group.Length}/4 in Powerful Light / overlapping stacks"); return; }
        }
        foreach (var member in members)
        {
            if (centers.All(c => Vector3.Distance(member.Position, c) > 5))
            { Wipe(PowerfulLight, "outside Powerful Light stacks"); return; }
            Hit(member, PowerfulLight, "Powerful Light", false); AddLight(member);
        }
    }
    private void SpawnOrbs(bool first)
    {
        var hit = first ? FirstOrbTime : SecondOrbTime;
        foreach (var position in pattern.Orbs(first))
        {
            var orb = Spawn(HolyLight, position);
            if (orb == null) continue;
            world.Events.Add(MathF.Max(0, hit - 5 - world.Events.Elapsed), () => { if (running) orb.Cast(HolyLightBurst, castSeconds: 5); });
            world.Events.Add(hit + 1 - world.Events.Elapsed, orb.Despawn);
        }
    }
    private void OrbHit(bool first)
    {
        foreach (var member in Members())
            if (pattern.Orbs(first).Any(p => Vector3.Distance(member.Position, p) <= 11))
                Hit(member, HolyLightBurst, "Holy Light orb", true);
    }
    private void Banish()
    {
        var members = Members();
        // Pairs target either all supports or all DPS; spreads target everyone.
        var targets = pattern.Pairs ? LightRampantPattern.Roles.Where(r => r.IsDps() != pattern.SupportPairs).Select(r => world.Party.Get(r)).ToArray() : members;
        if (targets.Any(t => !t.IsAlive())) { Wipe(DeathExplosion, "missing Banish target"); return; }
        var centers = targets.Select(t => t!.Position).ToArray();
        var action = pattern.Pairs ? BanishPairsHit : BanishSpreadHit;
        for (var i = 0; i < targets.Length; i++)
        {
            Effect(action, centers[i], targets[i]);
            var group = members.Where(m => Vector3.Distance(m.Position, centers[i]) <= 5).ToArray();
            if (group.Length != (pattern.Pairs ? 2 : 1))
            { Wipe(action, pattern.Pairs ? "Banish: pair up" : "Banish: spread"); return; }
        }
        foreach (var member in members)
        {
            if (centers.Count(c => Vector3.Distance(member.Position, c) <= 5) != 1)
            { Wipe(action, "Banish overlap / missing partner"); return; }
            Hit(member, action, "Banish", false);
        }
    }
    private void House()
    {
        var members = Members();
        foreach (var target in members)
        {
            var dir = target.Position.LengthSquared() > 0.001f ? Vector3.Normalize(target.Position) : Vector3.UnitZ;
            Effect(HouseHit, Vector3.Zero, target, MathF.Atan2(dir.X, dir.Z));
            foreach (var member in members)
                if (member.Position.LengthSquared() < 0.001f || Vector3.Dot(Vector3.Normalize(member.Position), dir) >= MathF.Cos(MathF.PI / 6))
                    Hit(member, HouseHit, "House of Light cone overlap", member != target);
        }
        foreach (var member in members) AddLight(member);
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || boss is not { IsActive: true }) return;
        var time = world.Events.Elapsed;
        if (assigned && LightRampantPattern.Roles.Any(r => !world.Party.Get(r).IsAlive()))
        { Wipe(DeathExplosion, "Inescapable Illumination: party member died"); return; }
        if (cursed)
            for (var i = 0; i < 6; i++)
            {
                var a = world.Party.Get(pattern.Slot(i)); var b = world.Party.Get(pattern.Slot((i + 1) % 6));
                if (a != null && b != null && Vector3.Distance(a.Position, b.Position) < 25)
                { Wipe(RefulgentFate, "Curse of Everlasting Light: chain too short"); return; }
            }
        foreach (var puddle in puddles.Where(p => time >= p.EndAt)) puddle.Visual?.Despawn();
        puddles.RemoveAll(p => time >= p.EndAt);
        foreach (var member in Members())
        {
            if (member.Position.Length() > 20) Hit(member, Raidwide, "arena boundary", true);
            var last = previous.GetValueOrDefault(member, member.Position);
            foreach (var puddle in puddles)
                if (time >= puddle.ArmAt && DistanceToSegment(puddle.Position, last, member.Position) <= 6)
                    Hit(member, LuminousHammer, "Luminous Hammer puddle", true);
            previous[member] = member.Position;
        }
    }
    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        var segment = end - start;
        var t = segment.LengthSquared() > 0.0001f ? Math.Clamp(Vector3.Dot(point - start, segment) / segment.LengthSquared(), 0, 1) : 0;
        return Vector3.Distance(point, start + t * segment);
    }
    private void Finish()
    {
        running = assigned = false; ClearChains(); ClearTowers(); puddles.Clear(); previous.Clear();
        foreach (var role in LightRampantPattern.Roles)
            if (world.Party.Get(role) is { } member)
            {
                member.RemoveStatus(Weight); member.RemoveStatus(Lightsteeped); member.RemoveVfx(PuddleMarker);
                if (member is SimPartyNpc bot) bot.StopMoving();
            }
        foreach (var obj in owned) obj.Despawn();
    }
}
