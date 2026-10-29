using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Scenarios.Tea;

// Puddles whose damage over time kills: stepping into one puts its status on, and the holder's next
// tick kills. In game it lifts on stepping out; here the mistake kills. A player's damage over time
// ticks every 3s on their own clock, set on entering; a party's clocks sit within 1.4s of each other.
public sealed class TeaDotPuddles
{
    private const float TickEvery = 3f;
    private const float TickSpread = 1.4f;

    private readonly List<Puddle> puddles = [];
    private readonly Dictionary<PartyRole, float> clocks = [];
    private SimWorld world = null!;
    private float checkedAt;

    // Harmful from ArmedAt to its fade.
    public sealed class Puddle(SimEventObject? visual, Vector3 at, float radius, ushort status, string death, float armedAt)
    {
        public Vector3 At { get; } = at;
        public float Radius { get; } = radius;
        public ushort Status { get; } = status;
        public string Death { get; } = death;
        public float ArmedAt { get; } = armedAt;
        public bool Faded { get; private set; }

        public void Fade()
        {
            visual?.DirectorEObjMod(7);
            Faded = true;
        }

        public void Despawn()
        {
            visual?.Despawn();
            Faded = true;
        }
    }

    // pinnedClock: every player's tick this far into each 3s.
    public void Start(SimWorld worldParam, float? pinnedClock)
    {
        world = worldParam;
        puddles.Clear();
        checkedAt = world.Events.Elapsed;
        var rng = world.Stream("tea-damage-ticks");
        var first = rng.NextSingle() * TickEvery;
        foreach (var role in PerRole.All) clocks[role] = pinnedClock ?? first + rng.NextSingle() * TickSpread;
    }

    // armedAfter: seconds from now before it starts to harm.
    public Puddle Add(SimEventObject? visual, Vector3 at, float radius, ushort status, string death, float armedAfter = 0f)
    {
        var puddle = new Puddle(visual, at with { Y = 0f }, radius, status, death, world.Events.Elapsed + armedAfter);
        puddles.Add(puddle);
        return puddle;
    }

    public void Tick()
    {
        var (from, to) = (checkedAt, world.Events.Elapsed);
        checkedAt = to;
        if (puddles.Count == 0) return;
        foreach (var (role, member) in world.Party.FilledSlots().Where(s => s.member.IsAlive()))
        {
            foreach (var puddle in puddles)
                if (!puddle.Faded && to >= puddle.ArmedAt && !member.HasStatus(puddle.Status)
                    && Vector2.Distance(Flat(member.Position), Flat(puddle.At)) <= puddle.Radius)
                    member.AddStatus(puddle.Status);
            var held = puddles.FirstOrDefault(p => member.HasStatus(p.Status));
            if (held is null || !TicksBetween(role, from, to) || Invulnerable(member)) continue;
            member.Die(SimCharacterDeathExtensions.Environment, held.Death);
        }
    }

    private bool TicksBetween(PartyRole role, float from, float to)
        => clocks.TryGetValue(role, out var clock) && MathF.Floor((to - clock) / TickEvery) > MathF.Floor((from - clock) / TickEvery);

    private static bool Invulnerable(SimCharacter member)
        => Mitigation.Effective(member.ActiveStatusSnapshot.Select(s => s.StatusId), DamageKind.Unique) >= 1f;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
