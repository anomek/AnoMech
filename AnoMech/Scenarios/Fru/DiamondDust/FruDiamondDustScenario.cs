using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.DiamondDust.DiamondDustConstants;

namespace AnoMech.Scenarios.Fru.DiamondDust;

public sealed partial class FruDiamondDustScenario : IScenario
{
    public string Name => "Diamond Dust";
    public float Duration => 54f;
    public IPhase Phase => FruZone.P2;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new DiamondDustAi()];
    internal DiamondDustSettings PatternSettings { get; } = new();
    public void DrawSettings() => DrawPatternSettings();
    partial void DrawPatternSettings();
    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private DiamondDustPattern pattern = null!;
    private SimEnemy? boss, reflection;
    private readonly List<ISimObject> owned = [];
    private readonly List<(Vector3 Position, float ArmAt)> puddles = [];
    private readonly List<Vector3> stars = [];
    private readonly Dictionary<SimCharacter, Vector3> previousPositions = [];
    private SimMapEffect? ice;
    private bool running, frozen;

    public void Run(SimWorld worldParam, int? selectedAi) => Run(worldParam, PatternSettings.CreatePattern(Random.Shared));
    internal void Run(SimWorld worldParam, DiamondDustPattern chosen)
    {
        world = worldParam;
        damage = new(world.Party);
        pattern = chosen;
        owned.Clear(); puddles.Clear(); stars.Clear(); previousPositions.Clear();
        boss = reflection = null;
        ice = null;
        running = true;
        frozen = false;
        At(0.5f, () => {
            boss = Spawn(Usurper, new Vector3(0, 0, 5), targetable: true);
            if (boss == null) running = false;
            boss?.SetRotation(MathF.PI);
        });
        At(2, () => boss?.Cast(MirrorImage, castSeconds: 3));
        At(5, () => {
            reflection = Spawn(Reflection, Vector3.Zero);
            if (reflection == null) { Finish(); return; }
            reflection.Face(Vector3.Zero + Vector3.UnitZ);
        });
        At(7.1f, () => boss?.Cast(Raidwide, castSeconds: 5));
        At(12.1f, () => { foreach (var member in Members()) Hit(member, Raidwide, "Diamond Dust", false); });
        At(14.9f, () => reflection?.Cast(pattern.Axe ? Axe : Scythe, castSeconds: 6));
        At(15.2f, () => {
            boss?.SetTargetable(false);
            boss?.SetVisible(false);
            foreach (var role in DiamondDustPattern.Roles)
                if (pattern.Marked(role) && world.Party.Get(role) is { } member && member.IsAlive())
                    member.AddVfx(StoneMarker, persistent: false);
        });
        At(KickTime, () => {
            foreach (var member in Members())
                if (pattern.Axe ? member.Position.Length() <= 16 : member.Position.Length() >= 4)
                    Hit(member, pattern.Axe ? Axe : Scythe, pattern.Axe ? "Axe Kick: out" : "Scythe Kick: in", true);
        });
        At(KickTime + 0.8f, Proteans);
        At(StoneTime, Stones);
        float[] icicleTimes = [23.7f, KnockbackTime, HolyTime(0)];
        for (var wave = 0; wave < 3; wave++)
        {
            var index = wave;
            At(icicleTimes[wave] - 9, () => {
                foreach (var position in pattern.Icicles(index)) Effect(Icicle, position, cast: 9);
            });
            At(icicleTimes[wave], () => {
                foreach (var member in Members())
                    if (pattern.Icicles(index).Any(p => Vector3.Distance(member.Position, p) <= 10))
                        Hit(member, Icicle, $"Icicle Impact {index + 1}", true);
            });
        }
        At(24.2f, () => {
            reflection?.SetPosition(pattern.ReflectionPosition);
            reflection?.Face(Vector3.Zero);
            boss?.SetPosition(Vector3.Zero);
            boss?.SetVisible(true);
        });
        At(25.4f, () => {
            foreach (var position in stars)
            {
                Effect(NeedleCircle, position, cast: 5);
                Effect(NeedleCross, position, cast: 5);
                Effect(NeedleCross, position, cast: 5, rotation: MathF.PI / 4);
            }
        });
        At(25.8f, () => reflection?.Cast(HolyCast, castSeconds: 5));
        At(KnockbackTime, () => {
            boss?.Cast(HeavenlyStrike, castSeconds: 0);
            foreach (var member in Members())
            {
                Hit(member, HeavenlyStrike, "Heavenly Strike", false);
                if (member is SimPlayer player) player.Knockback(Vector3.Zero, 12, 20);
                else if (member is SimPartyNpc bot) bot.Knockback(Vector3.Zero, 12, 20);
            }
        });
        At(KnockbackTime + 0.8f, () => boss?.SetVisible(false));
        At(StarTime, () => {
            foreach (var member in Members())
                foreach (var star in stars)
                {
                    var offset = member.Position - star;
                    if (offset.Length() <= 5 || MathF.Abs(offset.X) <= 2.5f || MathF.Abs(offset.Z) <= 2.5f
                        || MathF.Abs(offset.X + offset.Z) / MathF.Sqrt(2) <= 2.5f
                        || MathF.Abs(offset.X - offset.Z) / MathF.Sqrt(2) <= 2.5f)
                        Hit(member, NeedleCross, "Frigid Needle stars", true);
                }
        });
        for (var wave = 0; wave < 4; wave++) At(HolyTime(wave), HolyStacks);
        At(GazeTime - 7.2f, () => {
            boss?.SetPosition(pattern.GazePosition);
            boss?.Face(Vector3.Zero);
            boss?.SetVisible(true);
            boss?.PlayActionTimeline(7747); // Native warp end, the gaze's reference cue.
        });
        At(GazeTime, GazeAndIce);
        At(ComboTime, () => {
            boss?.SetVisible(false);
            owned.Add(world.SpawnVoiceLine(pattern.Stillness ? StillnessVoice : SilenceVoice));
            reflection?.Cast(pattern.Stillness ? StillnessFirst : SilenceFirst, castSeconds: 3.5f);
        });
        At(FirstHit, () => TwinHit(first: true));
        At(SecondHit, () => {
            reflection?.Cast(pattern.Stillness ? StillnessSecond : SilenceSecond, castSeconds: 0);
            TwinHit(first: false);
        });
        At(IceEnd, Thaw);
        At(52.3f, () => {
            reflection?.SetVisible(false);
            boss?.SetPosition(Vector3.Zero);
            boss?.SetRotation(MathF.PI);
            boss?.SetVisible(true);
            boss?.SetTargetable(true);
        });
        At(Duration, Finish);
        ((DiamondDustAi)AiStrats[0]).Run(pattern, world, () => running && boss is { IsActive: true },
            () => puddles.Select(p => p.Position).ToArray());
    }

    private void At(float time, Action action) => world.Events.Add(time, () => { if (running) action(); });
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private void Hit(SimCharacter member, uint action, string cause, bool lethal)
    { if (member.IsAlive()) damage.ApplyDamage(member, lethal ? 1 : 0.35f, action, cause, lethal); }
    private SimEnemy? Spawn(uint id, Vector3 position, bool targetable = false, uint model = 0)
    {
        var actor = world.SpawnEnemy(new EnemySpawnConfig(id,
            NameId: id == Reflection ? ReflectionName : UsurperName, Level: FruConstants.Level,
            Targetable: targetable, EnemyList: id is Usurper or Reflection ? EnemyListMode.OnlyWhenVisible : EnemyListMode.Never,
            Placement: new(position, 0), ModelCharaId: model));
        if (actor != null) owned.Add(actor);
        return actor;
    }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null, float cast = 0, float rotation = 0,
        uint actor = FruConstants.BNpcBaseId.Helper)
    {
        var helper = Spawn(actor, position);
        helper?.SetRotation(rotation);
        helper?.Cast(action, targetLocation: position, targetId: target?.GameObjectId, castSeconds: cast);
        if (helper != null) world.Events.Add(cast + 3, helper.Despawn);
    }
    private void Proteans()
    {
        var members = Members();
        // Four closest actors at the snapshot, not the preassigned bot roles.
        foreach (var target in members.OrderBy(m => m.Position.LengthSquared()).Take(4))
        {
            var direction = target.Position.LengthSquared() > 0.001f ? Vector3.Normalize(target.Position) : Vector3.UnitZ;
            Effect(Protean, Vector3.Zero, target, rotation: MathF.Atan2(direction.X, direction.Z));
            foreach (var member in members)
            {
                var offset = member.Position;
                if (offset.LengthSquared() < 0.001f || Vector3.Dot(Vector3.Normalize(offset), direction) >= MathF.Cos(MathF.PI / 12))
                    Hit(member, Protean, "House of Light cone bait / overlap", member != target);
            }
        }
    }
    private void Stones()
    {
        var members = Members();
        foreach (var role in DiamondDustPattern.Roles)
        {
            if (!pattern.Marked(role) || world.Party.Get(role) is not { } target || !target.IsAlive()) continue;
            var position = target.Position;
            stars.Add(position);
            Effect(Stone, position, target);
            foreach (var member in members)
                if (Vector3.Distance(member.Position, position) <= 5)
                    Hit(member, Stone, "Frigid Stone spread", member != target);
        }
    }
    private void HolyStacks()
    {
        var members = Members();
        var healers = new[] { world.Party.Get(PartyRole.RegenHealer), world.Party.Get(PartyRole.ShieldHealer) };
        if (healers.Any(h => !h.IsAlive()))
        { foreach (var member in members) Hit(member, Holy, "missing Sinbound Holy healer", true); return; }
        var centers = healers.Select(h => h!.Position).ToArray();
        for (var i = 0; i < 2; i++)
        {
            Effect(Holy, centers[i], healers[i]);
            var stack = members.Where(m => Vector3.Distance(m.Position, centers[i]) <= 6).ToArray();
            foreach (var member in stack)
                Hit(member, Holy, $"{stack.Length}/4 in Sinbound Holy",
                    stack.Length != 4 || Vector3.Distance(member.Position, centers[1 - i]) <= 6);
            var puddle = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = HolyPuddle, Placement = new(centers[i], 0), TimelineState = 1 });
            if (puddle != null) owned.Add(puddle);
            // Allow the party to leave the freshly placed six-yalm puddle.
            puddles.Add((centers[i], world.Events.Elapsed + 1.25f));
        }
        foreach (var member in members)
            if (centers.All(c => Vector3.Distance(member.Position, c) > 6))
                Hit(member, Holy, "outside Sinbound Holy stacks", true);
    }
    private void GazeAndIce()
    {
        Effect(Gaze, pattern.GazePosition);
        Effect(Frost, pattern.ReflectionPosition);
        // The two native bodies trade places: the outer Usurper takes light
        // form, and the inner Reflection takes ice form for the twin cleaves.
        // Keep old actors allocated briefly so native teardown cannot reuse a
        // still-referenced slot in the same frame as the visual handoff.
        var previousBoss = boss;
        var previousReflection = reflection;
        boss = Spawn(Usurper, pattern.GazePosition, model: LightModel);
        reflection = Spawn(Reflection, pattern.ReflectionPosition, model: IceModel);
        previousBoss?.SetVisible(false);
        previousReflection?.SetVisible(false);
        if (previousBoss != null) world.Events.Add(3, previousBoss.Despawn);
        if (previousReflection != null) world.Events.Add(3, previousReflection.Despawn);
        if (boss == null || reflection == null) { Finish(); return; }
        boss.Face(Vector3.Zero);
        reflection.Face(Vector3.Zero);
        foreach (var member in Members())
        {
            var forward = new Vector3(MathF.Sin(member.Rotation), 0, MathF.Cos(member.Rotation));
            var toGaze = pattern.GazePosition - member.Position;
            if (toGaze.LengthSquared() > 0.001f && Vector3.Dot(forward, Vector3.Normalize(toGaze)) >= MathF.Sqrt(0.5f))
                Hit(member, Gaze, "Shining Armor: look away", true);
            // The native client owns the player's ice movement. Param is a
            // distance, not a stack count: default 1 produces a 0.1-yalm slide.
            if (member.IsAlive()) member.AddStatus(ThinIce, IceEnd - GazeTime, ThinIceDistanceParam, overrideStacks: true);
        }
        frozen = true;
        previousPositions.Clear();
        foreach (var member in Members()) previousPositions[member] = member.Position;
        // Mode 1, low action flags 0x20/0x40 select state-table entries 6/7:
        // stage2_ice_a / stage2_nomal_a. The high word is NOT a timeline index.
        // Slot 25 is a separate ice-piece controller, not the slippery floor.
        ice = world.SpawnMapEffect(23, FreezeFloor, ThawFloor);
        owned.Add(ice);
    }
    private void TwinHit(bool first)
    {
        var backSafe = first == pattern.Stillness;
        var action = first ? pattern.Stillness ? StillnessFirst : SilenceFirst : pattern.Stillness ? StillnessSecond : SilenceSecond;
        foreach (var member in Members())
            if (Vector3.Distance(member.Position, pattern.ReflectionPosition) <= (backSafe ? 30 : 40)
                && pattern.BehindReflection(member.Position) != backSafe)
                Hit(member, action, backSafe ? "Twin combo: behind Shiva" : "Twin combo: in front of Shiva", true);
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || boss is not { IsActive: true }) return;
        var time = world.Events.Elapsed;
        foreach (var member in Members())
        {
            if (frozen) member.FindStatus(ThinIce)?.Reapply(MathF.Max(0.2f, IceEnd - time), 0);
            if (member.Position.Length() > 20) Hit(member, HeavenlyStrike, "arena boundary", true);
            var previous = previousPositions.GetValueOrDefault(member, member.Position);
            foreach (var puddle in puddles)
                if (time >= puddle.ArmAt && DiamondDustPattern.DistanceToSegment(puddle.Position, previous, member.Position) <= 6)
                    Hit(member, Holy, "Sinbound Holy puddle", true);
            previousPositions[member] = member.Position;
        }
    }
    private void Thaw()
    {
        frozen = false;
        ice?.Despawn();
        puddles.Clear();
        foreach (var obj in owned.OfType<SimEventObject>()) obj.Despawn();
        foreach (var role in DiamondDustPattern.Roles) world.Party.Get(role)?.RemoveStatus(ThinIce);
    }
    private void Finish()
    {
        Thaw();
        running = false;
        foreach (var actor in owned) actor.Despawn();
    }
}
