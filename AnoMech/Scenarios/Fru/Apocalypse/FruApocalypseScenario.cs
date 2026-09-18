using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.Apocalypse.ApocalypseConstants;

namespace AnoMech.Scenarios.Fru.Apocalypse;

public sealed class FruApocalypseScenario : IScenario
{
    public string Name => "Apocalypse";
    public IPhase Phase => FruZone.P3;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new ApocalypseAi()];
    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private ApocalypsePattern pattern = null!;
    private SimEnemy? oracle;
    private readonly List<ISimObject> owned = [];
    private Vector3 jumpFrom, jumpTo;
    private SimCharacter? jumpTarget;
    private bool running, jumping;
    public void Run(SimWorld worldParam, int? selectedAi) => Run(worldParam, ApocalypsePattern.Randomize());
    internal void Run(SimWorld worldParam, ApocalypsePattern chosenPattern)
    {
        world = worldParam;
        damage = new(world.Party);
        pattern = chosenPattern;
        oracle = null;
        owned.Clear();
        running = true;
        jumping = false;
        jumpTarget = null;
        At(0.5f, () => {
            oracle = Spawn(Oracle, Vector3.Zero, true);
            if (oracle == null) { running = false; return; }
            oracle?.SetRotation(MathF.PI);
            oracle?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        });
        CastAt(2.1f, Refrain, 1.7f);
        CastAt(7.2f, WaterCast, 4.7f);
        At(7.2f, () => {
            foreach (var role in ApocalypsePattern.Roles)
                if (pattern.Duration(role) != 0 && world.Party.Get(role) is { } member && member.IsAlive()) member.AddVfx(WaterMarker, persistent: false);
        });
        At(12.8f, () => {
            foreach (var role in ApocalypsePattern.Roles)
                if (pattern.Duration(role) is > 0 and var duration && world.Party.Get(role) is { } member && member.IsAlive())
                {
                    member.AddStatus(WaterStatus, ApocalypsePattern.WaterTime(duration) - 12.8f);
                    member.AddVfx(WaterWaitingClock);
                }
        });
        CastAt(15.3f, ApocCast, 3.7f);
        CastAt(21.5f, SpiritCast, 2.6f);
        foreach (var duration in new[] { 1, 2, 3 })
        {
            var order = duration;
            At(ApocalypsePattern.WaterTime(order) - 5.1f, () => {
                foreach (var role in ApocalypsePattern.Roles)
                    if (pattern.Duration(role) == order && world.Party.Get(role) is { } member)
                    {
                        member.RemoveVfx(WaterWaitingClock);
                        if (member.IsAlive()) member.AddVfx(WaterClock, persistent: false);
                    }
            });
            At(ApocalypsePattern.WaterTime(order), () => WaterHit(order));
        }
        At(25.5f, SpiritTaker);
        CastAt(30.9f, EruptionCast, 4.7f);
        At(30.9f, () => { foreach (var member in Members()) member.AddVfx(EruptionMarker, persistent: false); });
        At(35.7f, Eruptions);
        for (var wave = 0; wave < 6; wave++)
        {
            var index = wave;
            At(22 + index * 2, () => WarnWave(index));
            At(ApocalypsePattern.WaveTime(index), () => {
                if (oracle is not { IsActive: true }) return;
                foreach (var pos in pattern.Explosions(index))
                {
                    Effect(ApocHit, pos);
                    foreach (var member in Members())
                        if (Vector3.Distance(member.Position, pos) <= ApocRadius) Hit(member, ApocHit, "Apocalypse", true);
                }
            });
        }
        CastAt(38.5f, DanceCast, 4.7f);
        At(43.2f, Dance);
        At(44.2f, () => {
            if (oracle is not { IsActive: true }) return;
            // The reference snapshots damage at 43.2, then animates toward the
            // bait target's later position at 44.2. Keep those two samples separate.
            var offset = (jumpTarget.IsAlive() ? jumpTarget!.Position : jumpTo) - oracle.Position;
            jumpFrom = oracle.Position;
            jumpTo = offset.LengthSquared() > 0.001f ? jumpFrom + Vector3.Normalize(offset) * MathF.Max(0, offset.Length() - 6.3f) : jumpFrom;
            if (offset.LengthSquared() > 0.001f) oracle.SetRotation(MathF.Atan2(offset.X, offset.Z));
            jumping = true;
        });
        At(47.6f, () => {
            if (oracle is not { IsActive: true }) return;
            oracle.Cast(Knockback, castSeconds: 0);
            foreach (var member in Members())
            {
                Hit(member, Knockback, "Darkest Dance knockback", false);
                if (member is SimPartyNpc bot) bot.Knockback(oracle.Position, KnockbackDistance, KnockbackDistance / 0.7f);
                else if (member is SimPlayer player) player.Knockback(oracle.Position, KnockbackDistance, KnockbackDistance / 0.7f);
            }
        });
        CastAt(51.2f, Pulsar, 4.7f);
        At(55.9f, () => { foreach (var member in Members()) Hit(member, Pulsar, "Shockwave Pulsar", false); });
        At(57, Finish);
        ((ApocalypseAi)AiStrats[0]).Run(pattern, world, () => running ? oracle : null);
    }
    private void At(float time, Action action) => world.Events.Add(time, () => { if (running) action(); });
    private void CastAt(float time, uint action, float duration)
        => At(time, () => { if (oracle is { IsActive: true }) oracle.Cast(action, castSeconds: duration); });
    private SimEnemy? Spawn(uint id, Vector3 position, bool targetable = false)
    {
        var actor = world.SpawnEnemy(new EnemySpawnConfig(id, NameId: OracleName, Level: FruConstants.Level,
            Targetable: targetable, EnemyList: targetable ? EnemyListMode.Always : EnemyListMode.Never,
            Placement: new(position, 0)));
        if (actor != null) owned.Add(actor);
        return actor;
    }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null)
    {
        var helper = Spawn(FruConstants.BNpcBaseId.Helper, position);
        helper?.Cast(action, targetLocation: position, targetId: target?.GameObjectId, castSeconds: 0);
        // Retain the source through the authored effect, then release its slot.
        if (helper != null) world.Events.Add(3, helper.Despawn);
    }
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private void Hit(SimCharacter member, uint action, string cause, bool lethal)
    { if (member.IsAlive()) damage.ApplyDamage(member, lethal ? 1 : 0.35f, action, cause, lethal); }
    private void WaterHit(int duration)
    {
        if (oracle is not { IsActive: true }) return;
        var members = Members();
        var targets = ApocalypsePattern.Roles.Where(r => pattern.Duration(r) == duration).Select(world.Party.Get).ToArray();
        if (targets.Any(t => !t.IsAlive()))
        { foreach (var member in members) Hit(member, Water, "missing Dark Water target", true); return; }
        var centers = targets.Select(t => t!.Position).ToArray();
        for (var i = 0; i < 2; i++)
        {
            var occupants = members.Where(m => Vector3.Distance(m.Position, centers[i]) <= WaterRadius).ToArray();
            Effect(Water, centers[i], targets[i]);
            foreach (var member in occupants) Hit(member, Water, $"{occupants.Length}/4 in Dark Water stack",
                occupants.Length != 4 || Vector3.Distance(member.Position, centers[1 - i]) <= WaterRadius);
            targets[i]!.RemoveStatus(WaterStatus);
        }
    }
    private void SpiritTaker()
    {
        if (oracle is not { IsActive: true }) return;
        var members = Members();
        if (members.Length == 0) return;
        var target = members[Random.Shared.Next(members.Length)];
        // The native jump/flip effect runs on Oracle; keep the arena-centered
        // anchor for subsequent casts, as in the reference sequence.
        oracle.Cast(SpiritHit, targetId: target.GameObjectId, castSeconds: 0);
        foreach (var member in members)
            if (Vector3.Distance(member.Position, target.Position) <= SpiritRadius)
                Hit(member, SpiritHit, "Spirit Taker spread", member != target);
    }
    private void Eruptions()
    {
        if (oracle is not { IsActive: true }) return;
        var sources = Members().Select(m => (Member: m, Position: m.Position)).ToArray();
        foreach (var source in sources)
        {
            Effect(Eruption, source.Position, source.Member);
            foreach (var target in sources)
                if (Vector3.Distance(source.Position, target.Position) <= EruptionRadius)
                    Hit(target.Member, Eruption, "Dark Eruption spread", target.Member != source.Member);
        }
    }
    private void Dance()
    {
        if (oracle is not { IsActive: true }) return;
        var members = Members();
        var target = members.OrderByDescending(m => Vector3.DistanceSquared(m.Position, oracle.Position)).FirstOrDefault();
        if (target == null) return;
        var position = target.Position;
        jumpTarget = target;
        oracle.Cast(DanceHit, targetId: target.GameObjectId, castSeconds: 0);
        foreach (var member in members)
            if (Vector3.Distance(member.Position, position) <= DanceRadius)
                Hit(member, DanceHit, "Darkest Dance farthest tank bait / splash",
                    member != target || member is not ISimPartyMember slot || slot.Role is not (PartyRole.MainTank or PartyRole.OffTank));
        jumpFrom = oracle.Position;
        var offset = position - jumpFrom;
        jumpTo = position;
        if (offset.LengthSquared() > 0.001f) oracle.SetRotation(MathF.Atan2(offset.X, offset.Z));
    }
    private void WarnWave(int wave)
    {
        if (oracle is not { IsActive: true }) return;
        foreach (var position in pattern.Explosions(wave))
        {
            NativeLight(position, 0, PulseTrigger);
            if (wave == 5) continue;
            if (position.LengthSquared() < 0.01f)
            {
                var direction = pattern.StartingDirection;
                var yaw = MathF.Atan2(direction.X, direction.Z);
                NativeLight(position, yaw, StraightTrigger);
                NativeLight(position, yaw + MathF.PI, StraightTrigger);
            }
            else
            {
                var tangent = new Vector3(-position.Z, 0, position.X) * (pattern.Clockwise ? 1 : -1);
                NativeLight(position, MathF.Atan2(tangent.X, tangent.Z), pattern.Clockwise ? ClockwiseTrigger : CounterclockwiseTrigger);
            }
        }
    }
    private void NativeLight(Vector3 position, float yaw, uint trigger)
    {
        // Each native segment travels fourteen yalms straight or 45 degrees
        // around the ring over 60 authored frames. Keep its source stationary;
        // moving an EObj as well would apply the motion twice.
        var effect = world.SpawnOmen(LightVfx, new(position, yaw), Vector3.One);
        effect.Trigger(trigger);
        owned.Add(effect);
        world.Events.Add(3, effect.Despawn);
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || oracle is not { IsActive: true }) return;
        var time = world.Events.Elapsed;
        foreach (var role in ApocalypsePattern.Roles)
            if (pattern.Duration(role) is > 0 and var duration && world.Party.Get(role) is { } member)
            {
                var remaining = ApocalypsePattern.WaterTime(duration) - time;
                if (!member.IsAlive() || !member.HasStatus(WaterStatus)) member.RemoveVfx(WaterWaitingClock);
                else if (remaining > 0) member.FindStatus(WaterStatus)?.Reapply(MathF.Max(0.2f, remaining), 0);
            }
        if (jumping)
        {
            var t = Math.Clamp((time - 44.2f) / 0.5f, 0, 1);
            oracle.SetPosition(Vector3.Lerp(jumpFrom, jumpTo, t * t * (3 - 2 * t)));
            if (t >= 1) jumping = false;
        }
    }
    private void Finish()
    {
        running = jumping = false;
        foreach (var actor in owned) actor.Despawn();
        foreach (var role in ApocalypsePattern.Roles)
        {
            world.Party.Get(role)?.RemoveStatus(WaterStatus);
            world.Party.Get(role)?.RemoveVfx(WaterWaitingClock);
        }
    }
}
