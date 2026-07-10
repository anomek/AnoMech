using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Exaflares;

// Bots for UMAD P5 exaflares: the 7 doppels weave the rolling walls, then spread for the final
// ExaflareSpread (the local player dodges themselves). Scheduled on the scenario's unscaled
// `timeline`, not the stock AiManager (which rides EventTimeScale), so they stay frame-locked to the
// fire; spread relaxation runs per-frame from the scenario's Tick via state.SpreadTick.
//
// DODGE GEOMETRY. The fire is two perpendicular diagonal families: left line k burns X-Z = -35+10k,
// right line k burns X+Z = -35+10k. Each wave fires a {1,4}/{2,5}/{3,6} pair, leaving a wide central
// safe lane (and an outer lane beyond the pair when it's off-centre). A radius-6 fire spans 6*sqrt2 in
// X±Z space, so the strip inset must clear that, not 6. Lingering fire is safe - only the snapshot kills.
//
// DODGE PLACEMENT. Each wave's fire runs along one diagonal axis (crit); the perp axis is where the
// previous wave's fire is still rolling as this wave lands. So a bot FREEZES its perp coordinate and
// moves only along crit into a safe lane (PlaceWave): that never crosses the still-firing previous
// wave, and the frozen perp was already made safe by that wave (which used this axis as its own crit) -
// safe by induction, no carried band state, no coin flips. Melee/tank pull crit toward the boss for
// uptime; others hold their spot, and a de-clump pass (DeClump) spreads bunched bots along the crit
// axis only (never the frozen perp). The opening formation thus carries through the whole fight.
//
// SPREAD. Per-frame force-relaxation (RelaxStep): repulsion from every agent within Comfort, a radial
// spring (inner roles in a melee annulus, outer roles to OuterRing), and arena keep-in.
//
// OPENING. Before wave 0's dodge (t~2.0) the doppels fan out from their tight spawn ring into the melee
// annulus (FanOut): each pushes out along its own ring angle to a random melee radius, so they enter the
// fight already spread and each solves its first lane from a distinct spot instead of the shared huddle.
public sealed class UmadP5ExaflaresAi : IScenarioAi<UmadP5ExaflaresState>
{
    public string Name => "Spread";

    // --- dodge geometry ---
    private const float LocusBase = -35f, LocusStep = 10f;       // locus(k) = -35 + 10k
    private const float FireRadius = 6f;                          // ExaflareHit EffectRange
    private static readonly float FireClearU = FireRadius * MathF.Sqrt(2f); // ~8.49 in X±Z space
    private static readonly float StripInset = FireClearU + 1.5f; // keep bots clear of a fired locus
    private const float OuterReach = 19f;                         // outer-lane reach in X±Z space (arena-bounded)
    private const float DistFill = 0.8f;                          // keep bots off a lane's edges (extra fire margin)
    private const float DodgeSpeed = 7f;                          // sprint-ish, so the back-solve makes the snapshot

    // --- dodge placement ---
    private const float MeleeComfort = 2.5f;                      // melee happily stack this close
    private const float SpreadComfort = 6f;                       // ranged/healers claim this much room
    private const float TargetJitter = 0.6f;                      // natural noise per pick (symmetry-break + run variety)
    private const int   DeClumpIters = 6;
    private const float DeClumpRate = 0.5f;

    // --- opening fan-out ---
    private const float FanOutAt = 0.1f;                          // after SpawnKefka (t=0), so the boss radius reads
    private const float FanSpeed = 6f;                            // settle well before wave 0's dodge at t=2.0
    private const float FanInnerMargin = 0.75f;                   // inner fan radius = NoGoRadius + this
    private const float FanAngleJitter = 0.5f;                    // rad, so the wheel isn't rigid (< ring spacing)

    // --- spread relaxation ---
    private const float SpreadKill = 5f;                          // ExaflareSpread EffectRange (kill radius)
    private const float Comfort = 9.75f;                          // relaxation target separation (kill 5y + 50% wider spread)
    private const float OuterRing = 13f;                          // healers / ranged ring
    private const float InnerFallback = 6.5f;                     // max-melee (inner annulus outer edge) if the boss can't be read
    private const float NoGoRadius = 3.5f;                        // 7y-diameter hard no-go at the boss
    private const float ArenaMax = 19f;
    private const float RelaxSpeed = 6f;
    private const float DeadZone = 0.25f;                         // below this desired step, leave the bot be
    private const float WRepel = 1.0f, WArena = 1.5f;
    // Radial spring weights: inner roles pull harder so the wider spacing doesn't shove tanks/melee
    // out of melee range; outer roles stay loose so repulsion can spread them.
    private const float WRadialInner = 0.5f, WRadialOuter = 0.30f;

    // --- exaflare timing (mirrors the scenario) ---
    private const float FirstHitDelay = 4.582f;
    private const float BloomDelay = 14f / 30f;
    private const float ArriveLead = 0.4f;                        // be parked this long before a wave's first snapshot
    private static readonly float[] WaveStart = { 3.0f, 5.5f, 8.0f, 10.5f, 13.0f, 15.5f };

    private UmadP5ExaflaresState state = null!;
    private SimWorld world = null!;
    private EventScheduler timeline = null!;
    private readonly Random rng = new();
    private readonly bool[] relaxMoving = new bool[8];
    private bool spreadPhase;
    private float innerRing = InnerFallback;

    public void Run(UmadP5ExaflaresState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        timeline = state.Timeline;

        ScheduleInitialFanOut();
        for (int n = 0; n < 6; n++) ScheduleWaveDodge(n);

        // Spread phase: opens after wave 6's last snapshot (~22.65), closes at the spread snapshot
        // (25.09); relaxation runs across that window from the scenario's Tick.
        timeline.Add(22.75f, () => { spreadPhase = true; innerRing = ResolveInnerRing(); Array.Clear(relaxMoving); });
        timeline.Add(25.09f, () => spreadPhase = false);
        state.SpreadTick = RelaxStep;
    }

    // Schedule wave n's dodge. The whole group is placed at fire-time (PlaceWave), departing so each
    // arrives before the wave's first snapshot and leaves only after wave n-2 cleared.
    private void ScheduleWaveDodge(int n)
    {
        float startT = WaveStart[n];
        float firstSnapshot = startT + FirstHitDelay;               // the wave's first eruption/snapshot instant
        float arriveBy = firstSnapshot - ArriveLead;
        float safeMove = n == 0 ? 2.0f : startT + 1.7f + BloomDelay; // after wave n-2's last snapshot; n==0: big initial run, no prior fire
        float budget = arriveBy - safeMove;

        timeline.Add(safeMove, () => PlaceWave(n, budget));
    }

    // Place the group for wave n. The fire runs along one diagonal axis (crit); the other axis (perp)
    // is where the previous wave's fire is still rolling. So each bot FREEZES its perp coordinate and
    // moves only along crit into a safe lane - that never crosses the still-firing previous wave, and
    // the perp value was already made safe by that wave (which used this axis as its crit). Melee/tank
    // pull crit toward the boss for uptime; a de-clump pass spreads the rest along the lane. Each
    // departure is back-solved so the bot holds position until it must leave to make the snapshot.
    private void PlaceWave(int n, float budget)
    {
        bool leftWave = (n % 2) == 0;
        var lanes = UsableBands(n);                                  // safe crit lanes (central + maybe outer)
        if (lanes.Count == 0) return;
        int playerSlot = (int)world.Party.PlayerRole;

        var picks = new List<Pick>(7);
        for (int slot = 0; slot < 8; slot++)
        {
            if (slot == playerSlot) continue;                       // the player dodges themselves
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;

            bool inner = IsInnerRole((PartyRole)slot);
            var (crit, perp) = ToCritPerp(bot.Position, leftWave);
            float want = (inner ? 0f : crit) + Jitter();            // melee hug the boss; others hold their spot
            picks.Add(new Pick(bot, PlaceInLanes(lanes, want), perp, inner));
        }

        DeClump(picks, lanes);

        foreach (var pk in picks)
        {
            var dest = FromCritPerp(pk.Crit, pk.Perp, leftWave);
            float delay = budget - FlatDist(pk.Bot.Position, dest) / DodgeSpeed; // leave late enough to arrive on time
            if (delay > 0f) timeline.Add(delay, () => pk.Bot.MoveTo(dest, DodgeSpeed));
            else pk.Bot.MoveTo(dest, DodgeSpeed);                   // farther than the budget allows: best effort
        }
    }

    // Opening fan-out: push each doppel out of the tight spawn ring into the melee annulus before the
    // first dodge. Each bot keeps its own spawn-ring angle (so the ring's even spread carries over - no
    // pile-up) and moves out to a random melee radius with a little angular jitter. Run-to-run variety
    // comes from the spawn jitter plus the random radius/angle here; they settle long before wave 0.
    private void ScheduleInitialFanOut()
    {
        timeline.Add(FanOutAt, () =>
        {
            int playerSlot = (int)world.Party.PlayerRole;
            float outer = ResolveInnerRing();                       // max-melee around the boss
            float inner = MathF.Min(NoGoRadius + FanInnerMargin, outer - 0.5f);

            for (int slot = 0; slot < 8; slot++)
            {
                if (slot == playerSlot) continue;                   // the player isn't ours to move
                var bot = world.Party.Get(slot);
                if (bot is null || !bot.IsAlive()) continue;

                var cur = bot.Position;
                float baseAngle = MathF.Atan2(cur.X, cur.Z);        // its spawn-ring angle (localPos = sin,cos)
                float angle = baseAngle + ((float)rng.NextDouble() - 0.5f) * FanAngleJitter;
                float radius = inner + (float)rng.NextDouble() * (outer - inner);
                var target = new Vector3(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
                bot.MoveTo(target, FanSpeed);
            }
        });
    }

    // Wave n's usable safe lanes in its crit coordinate: the central lane always, the outer lane when
    // the fired pair is off-centre. Filled to keep bots off the lane edges.
    private List<(float lo, float hi)> UsableBands(int n)
    {
        var bands = new List<(float lo, float hi)> { Fill(Band(n, 0)) };
        if (OuterAvailable(n)) bands.Add(Fill(Band(n, 1)));
        return bands;
    }

    // Clamp a wanted crit value into the safe lane nearest to it (least travel onto safety). Always
    // lands inside a lane, so the result is clear of this wave's fire.
    private static float PlaceInLanes(List<(float lo, float hi)> lanes, float want)
    {
        float best = want; float bestD = float.MaxValue;
        foreach (var (lo, hi) in lanes)
        {
            float c = Math.Clamp(want, lo, hi);
            float d = MathF.Abs(c - want);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    // Spread bunched targets apart along the crit axis only (perp is frozen - the still-firing wave's
    // axis - so we never nudge anyone into it). Repulsion uses full 2D separation but moves crit alone;
    // two melee use a tight comfort (they stack), any pair with an outer role yields its wider spacing.
    private void DeClump(List<Pick> picks, List<(float lo, float hi)> lanes)
    {
        for (int iter = 0; iter < DeClumpIters; iter++)
            for (int i = 0; i < picks.Count; i++)
            {
                var pi = picks[i];
                float push = 0f;
                for (int j = 0; j < picks.Count; j++)
                {
                    if (j == i) continue;
                    var pj = picks[j];
                    float comfort = pi.Inner && pj.Inner ? MeleeComfort : SpreadComfort;
                    float dc = pi.Crit - pj.Crit, dp = pi.Perp - pj.Perp;
                    float dist = MathF.Sqrt(dc * dc + dp * dp);
                    if (dist >= comfort) continue;
                    float dir = dist > 1e-3f ? dc / dist : MathF.Sign(i - j); // crit component; tie-break by index
                    push += dir * (comfort - dist);
                }
                if (MathF.Abs(push) < 1e-3f) continue;
                pi.Crit = PlaceInLanes(lanes, pi.Crit + push * DeClumpRate); // re-clamp: stays in a safe lane
            }
    }

    private float Jitter() => ((float)rng.NextDouble() - 0.5f) * TargetJitter;

    // Split a world position into the wave's crit/perp diagonal coords. a = X-Z, b = X+Z; the crit axis
    // is the one this wave's fire runs along (a for a left wave, b for a right wave), perp the other.
    private static (float crit, float perp) ToCritPerp(Vector3 pos, bool leftWave)
    {
        float a = pos.X - pos.Z, b = pos.X + pos.Z;
        return leftWave ? (a, b) : (b, a);
    }

    private static Vector3 FromCritPerp(float crit, float perp, bool leftWave)
    {
        float a = leftWave ? crit : perp, b = leftWave ? perp : crit;
        return new Vector3((a + b) * 0.5f, 0f, (b - a) * 0.5f);
    }

    // A doppel's wave placement: the character, its safe-lane crit target, its frozen perp, and role.
    private sealed class Pick
    {
        public readonly SimCharacter Bot;
        public float Crit;
        public readonly float Perp;
        public readonly bool Inner;
        public Pick(SimCharacter bot, float crit, float perp, bool inner) { Bot = bot; Crit = crit; Perp = perp; Inner = inner; }
    }

    private static float FlatDist(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    // Force-relaxation step, driven every frame from the scenario's Tick during the spread window.
    // Moves only the doppels; the player is read as a repulsor but never moved.
    private void RelaxStep(float dt)
    {
        if (!spreadPhase) return;

        var agents = new List<(SimCharacter c, Vector2 p)>(8);
        for (int i = 0; i < 8; i++)
            if (world.Party.Get(i) is { } m && m.IsAlive())
                agents.Add((m, new Vector2(m.Position.X, m.Position.Z)));

        int playerSlot = (int)world.Party.PlayerRole;
        for (int slot = 0; slot < 8; slot++)
        {
            if (slot == playerSlot) continue;
            var bot = world.Party.Get(slot);
            if (bot is null || !bot.IsAlive()) continue;
            var p = new Vector2(bot.Position.X, bot.Position.Z);

            var desired = Vector2.Zero;

            // Repulsion from every other agent inside the comfort radius (yield to bots AND player).
            foreach (var (c, q) in agents)
            {
                if (ReferenceEquals(c, bot)) continue;
                var d = p - q;
                var dist = d.Length();
                if (dist >= Comfort) continue;
                var dir = dist > 1e-3f ? d / dist : Spoke(slot);
                desired += dir * (Comfort - dist) * WRepel;
            }

            // Radial shaping: inner roles free in the melee annulus [NoGoRadius..innerRing] (pull in
            // past it, push out inside the no-go); outer roles soft-sprung to OuterRing.
            bool inner = IsInnerRole((PartyRole)slot);
            float r = p.Length();
            var rdir = r > 1e-3f ? p / r : Spoke(slot);
            if (inner)
            {
                if (r > innerRing) desired += rdir * (innerRing - r) * WRadialInner;        // past max melee: pull in
                else if (r < NoGoRadius) desired += rdir * (NoGoRadius - r) * WRadialInner; // inside no-go: push out
            }
            else
            {
                desired += rdir * (OuterRing - r) * WRadialOuter;
            }
            if (r > ArenaMax) desired += rdir * (ArenaMax - r) * WArena;

            if (desired.Length() > DeadZone)
            {
                var step = desired.Length() > 5f ? desired * (5f / desired.Length()) : desired;
                var t = p + step;
                bot.MoveTo(new Vector3(t.X, 0f, t.Y), RelaxSpeed);
                relaxMoving[slot] = true;
            }
            else if (relaxMoving[slot])
            {
                bot.MoveTo(new Vector3(p.X, 0f, p.Y), RelaxSpeed); // settle once, then leave it idle (no anim stutter)
                relaxMoving[slot] = false;
            }
        }
    }

    // Inner ring = max-melee around the boss (fixed at arena centre): live hitbox + 3, floored.
    private float ResolveInnerRing()
    {
        var boss = world.Children.OfType<SimEnemy>()
                        .FirstOrDefault(e => e.IsAlive() && e.BNpcBaseId == BNpcBaseId.KefkaP5);
        float hitbox = boss?.HitboxRadius ?? 3.5f;
        return MathF.Max(hitbox + 3f, 6f);
    }

    private static bool IsInnerRole(PartyRole r) =>
        r.IsTank() || r == PartyRole.MeleeDpsA || r == PartyRole.MeleeDpsB;

    // An outer lane is usable only when the fired pair sits off-centre ({1,4} low or {3,6} high); a
    // centred pair ({2,5}) leaves only thin edge slivers, so those waves stay central-only.
    private bool OuterAvailable(int n)
    {
        var (lo, hi) = PairLoHi(n);
        return MathF.Abs((lo + hi) * 0.5f) > 1f;
    }

    // The two fired loci for wave n, sorted, in that wave's crit coordinate.
    private (float lo, float hi) PairLoHi(int n)
    {
        var order = (n % 2) == 0 ? state.LeftOrder : state.RightOrder;
        int pairBase = (n / 2) * 2;
        float l1 = Locus(order[pairBase]), l2 = Locus(order[pairBase + 1]);
        return (MathF.Min(l1, l2), MathF.Max(l1, l2));
    }

    // A safe band for wave n in its crit coordinate. idx 0 = central lane between the fired loci; idx 1
    // = outer lane beyond the pair (away from centre). Both inset by StripInset to clear the radius-6 fire.
    private (float lo, float hi) Band(int n, int idx)
    {
        var (lo, hi) = PairLoHi(n);
        if (idx == 0) return (lo + StripInset, hi - StripInset);          // central lane
        return (lo + hi) * 0.5f < 0f                                       // outer lane, arena-bounded
            ? (hi + StripInset, OuterReach)                                // pair sits low -> lane above it
            : (-OuterReach, lo - StripInset);                             // pair sits high -> lane below it
    }

    private static float Locus(int lineIdx) => LocusBase + LocusStep * lineIdx;

    // Shrink an interval toward its midpoint, leaving a margin so jitter + lateral scatter can't reach
    // the fire even at the edges.
    private static (float lo, float hi) Fill((float lo, float hi) s)
    {
        float mid = (s.lo + s.hi) * 0.5f, half = (s.hi - s.lo) * 0.5f * DistFill;
        return (mid - half, mid + half);
    }

    // Deterministic unit direction for degenerate cases (co-located agents / a bot dead-centre).
    private static Vector2 Spoke(int slot) => new(MathF.Cos(slot * 0.785f), MathF.Sin(slot * 0.785f));
}
