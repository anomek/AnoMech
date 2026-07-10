using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Exaflares;

// UMAD P5 "exaflares" (ground-fire): two diagonal walls of fire roll across the arena, then a
// final spread. Runs solo or with bots (UmadP5ExaflaresAi).
//
// Rolling hits are animation-based (no per-tile marker, just the ExaflareOmen lane arrow): each
// eruption snapshots position as it goes off and holds the kill one application delay later.
// Lingering fire is safe - only the snapshot instant kills.
//
// The timeline runs on a scenario-local Stopwatch (`timeline`), not the engine's ms-truncated
// UpdateDelta, so events fire drift-free and ignore the Speed buttons.
public sealed class UmadP5ExaflaresScenario : IScenario
{
    public string Name => "UMAD P5 Exaflares";
    public TargetInstance TargetInstance { get; } = new(
        TerritoryId: 1363,
        Origin: new Vector3(100.000f, 0f, 100.000f),
        PlayerPosition: new Vector3(100.000f, 0f, 116.000f),
        WeatherId: 175); // P5 weather (P3-4 used 174)
    public IReadOnlyList<Waymark> Waymarks { get; } = UmadWaymarks;
    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } = UmadConstants.WaymarkPresets;
    public ushort Bgm => 20294; // P5 BGM (P3-4 used 20293)
    public byte Level => 100;
    public bool SupportsSolo => true;
    public IReadOnlyList<Vector3> ColliderRemovalPoints => [new(0, 0, -10)];

    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP5ExaflaresAi()];

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UmadP5ExaflaresSettingsWindow settingsWindow = new();

    private UmadP5ExaflaresState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private SimEnemy? kefka;
    // One spread helper per player — each member is its own AoE source.
    private readonly List<SimEnemy> spreadHelpers = new();
    // One entry per (spread source × member-in-range) application, snapshotted at ResolveSpread
    // and applied one delay later at ApplySpreadKill. Duplicates are intentional: a member caught
    // by two spreads (its own + an overlap) appears twice, and two coverings are lethal.
    private readonly List<SimCharacter> spreadHits = new();

    // Scenario-local clock, advanced by real Stopwatch time so events fire drift-free at 1x.
    private readonly EventScheduler timeline = new();
    private readonly Stopwatch wallClock = new();
    private double lastWall;
    private const double FrameGapCapSeconds = 0.25; // skip pause / alt-tab / hitch frames

    // Exaflare timing. AoE radii come from the Action sheet's EffectRange (circles), not set here.
    private const float ExaflareFirstHitDelay = 4.582f; // first rolling hit, after the line launch
    private const float ExaflareHitInterval   = 0.513f; // between rolling hits
    private const float ExaflareSourceLead    = 0.62f;  // origin eruption fires this far before hit 1
    private const int   ExaflareHitCount      = 6;
    // Visible bloom delay: when the eruption flame peaks (frame 14 @ 30fps). VFX-only — the kill is
    // not held to this (see ExaflareKillDelay).
    private const float BloomDelay = 14f / 30f; // 0.467s
    // Snapshot -> kill application delay (same as the spread's ApplySpreadKill). Sets only the instant
    // the caught player dies; snapshot, VFX and timeline are unaffected.
    private const float ExaflareKillDelay = 0.624f;
    // Full eruption length; keep the VFX helper alive this long so the lingering fire isn't cut.
    private const float ExaflareVfxDuration = 90f / 30f; // 3.0s
    // ExaflareOmen (lane arrow) cast time. Visual only — despawned just before completion so its
    // native release doesn't fire; the source eruption is spawned separately (LaunchExaflareLine).
    private const float OmenCastTime          = 3.7f;
    // Despawn the arrow this far before completion to suppress its native release.
    private const float ArrowReleaseSuppressLead = 0.05f;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        // Resolve RSV names so cast bars read real action names (server-delivered in-duty only; we run
        // inn-only). The boss casts reuse the Stray Apocalypse / Stray Entropy rows - see UmadConstants.
        UmadRsvStrings.Seed();

        world = worldParam;
        party = worldParam.Party;
        state = new UmadP5ExaflaresState(settingsWindow.Overrides, timeline);
        damage = new DamageSolver(party); // ApplyDamage deals % of max HP; godmode drop/heal handled in Game.Kill
        spreadHelpers.Clear();

        // Re-arm the scenario clock for this run (the scenario object is reused).
        timeline.Clear();
        wallClock.Restart();
        lastWall = 0;

        // Bots schedule on the scenario `timeline` (after Clear, so their adds are absolute).
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5ExaflaresState>)AiStrats[idx]).Run(state, world);

        timeline.Add(1f, InitArena); // set the arena to its P5 state once the zone has settled
        timeline.Add(0f, SpawnKefka);
        timeline.Add(3.0f, () => kefka?.Cast(ActionId.ChaosEnd1));

        // Rolling exaflares: left/right pairs every 2.5s, columns from the chosen order.
        LaunchExaflareLine(3.0f,  state.LeftOrder[0],  isLeft: true);
        LaunchExaflareLine(3.0f,  state.LeftOrder[1],  isLeft: true);
        LaunchExaflareLine(5.5f,  state.RightOrder[0], isLeft: false);
        LaunchExaflareLine(5.5f,  state.RightOrder[1], isLeft: false);
        LaunchExaflareLine(8.0f,  state.LeftOrder[2],  isLeft: true);
        LaunchExaflareLine(8.0f,  state.LeftOrder[3],  isLeft: true);
        LaunchExaflareLine(10.5f, state.RightOrder[2], isLeft: false);
        LaunchExaflareLine(10.5f, state.RightOrder[3], isLeft: false);
        LaunchExaflareLine(13.0f, state.LeftOrder[4],  isLeft: true);
        LaunchExaflareLine(13.0f, state.LeftOrder[5],  isLeft: true);
        LaunchExaflareLine(15.5f, state.RightOrder[4], isLeft: false);
        LaunchExaflareLine(15.5f, state.RightOrder[5], isLeft: false);

        timeline.Add(19.2f, () => kefka?.Cast(ActionId.ChaosEnd2, castSeconds: 4.70f)); // 4.70s cast; ends at 23.90
        timeline.Add(25.09f, ResolveSpread);   // spread VFX + snapshot, cast-end + 1.19s
        timeline.Add(25.71f, ApplySpreadKill); // held kill on the hit, snapshot + 0.624s
        timeline.Add(30.0f, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        // Advance the timeline by real wall time, capping pause/hitch gaps so a freeze can't
        // fast-forward it. This is what keeps the scenario drift-free.
        var now = wallClock.Elapsed.TotalSeconds;
        var wallDelta = now - lastWall;
        lastWall = now;
        if (wallDelta > 0 && wallDelta <= FrameGapCapSeconds)
        {
            timeline.Tick((float)wallDelta);
            state?.SpreadTick?.Invoke((float)wallDelta); // bot spread relaxation, also 1x (no-op in solo)
        }
    }

    // Set the arena to its P5 state (MapEffect table): 36 SGB slots, base 0x4 with P5 deviations, each
    // replayed as AddEffect((state<<16)|flags, slot).
    private void InitArena()
    {
        var s = new ushort[0x24];
        for (var i = 0; i < s.Length; i++) s[i] = 0x4;   // base default (hide/empty)
        s[0x11] = 0x1; s[0x12] = 0x1;                     // base lit holes
        s[0x00] = 0x40;                                   // P5
        s[0x14] = 0x200;                                  // P5 centerpiece ("nine holes")
        for (var i = 0x15; i <= 0x1C; i++) s[i] = 0x1;    // P5 nine holes
        for (var i = 0x1D; i <= 0x21; i++) s[i] = 0x2;    // P5 towers of rubble

        for (byte slot = 0; slot < s.Length; slot++)
        {
            var state = s[slot];
            var flags = (byte)(state & 0xFF);
            if (flags == 0) flags = 0x01;                 // e.g. 0x200: no action bit -> "show"
            world.Map.AddEffect(((uint)state << 16) | flags, slot);
        }
    }

    private void SpawnKefka()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            IsVisible: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    // One rolling line of fire. `lineIdx` 1-6 faces the source; `isLeft` = top-left wall. The arrow
    // telegraph appears at launch; the source eruption and rolling hits fire on `timeline`, evenly spaced.
    private void LaunchExaflareLine(float startT, int lineIdx, bool isLeft)
    {
        var initPos = isLeft
            ? new Vector3(-35f + 5f * lineIdx, 0f, -5f * lineIdx)
            : new Vector3(5f * lineIdx, 0f, -35f + 5f * lineIdx);
        var dPos = isLeft ? new Vector3(5f, 0f, 5f) : new Vector3(-5f, 0f, 5f);
        var heading = MathF.PI * (isLeft ? 1f : -1f) / 4f;

        // Lane telegraph (arrow): visual only. Despawn it just before completion so it doesn't fire
        // its own eruption - the source is spawned separately below so it lines up with the rolling hits.
        timeline.Add(startT, () =>
        {
            var arrow = SpawnHelper(initPos, heading);
            arrow?.Cast(ActionId.ExaflareOmen, castSeconds: OmenCastTime);
            timeline.Add(OmenCastTime - ArrowReleaseSuppressLead, () => arrow?.Despawn());
        });

        // Source eruption (origin tile): fires ExaflareSourceLead before the first hit. Visual only
        // (origin is off-arena). Ignites just after the arrow clears.
        var tEruptSource = startT + ExaflareFirstHitDelay - ExaflareSourceLead;
        SimEnemy? source = null;
        timeline.Add(tEruptSource, () =>
        {
            source = SpawnHelper(initPos, heading);
            source?.Cast(ActionId.ExaflareHit, castSeconds: 0f, animationLock: 0f);
            timeline.Add(ExaflareVfxDuration, () => source?.Despawn());
        });

        // Rolling hits (one helper per tile). At tErupt the eruption fires its VFX and snapshots
        // position; the held kill lands at tBloom, one application delay later.
        for (var i = 0; i < ExaflareHitCount; i++)
        {
            var pos = initPos + dPos * (i + 1);
            var tErupt = startT + ExaflareFirstHitDelay + ExaflareHitInterval * i; // VFX + snapshot
            var tBloom = tErupt + ExaflareKillDelay;                               // held kill, at damage apply

            SimEnemy? hit = null;
            IReadOnlyList<SimCharacter> caught = [];

            // Fire the VFX and snapshot together; the kill is held. Despawn after the full eruption.
            timeline.Add(tErupt, () =>
            {
                hit = SpawnHelper(pos, heading);
                hit?.Cast(ActionId.ExaflareHit, castSeconds: 0f, animationLock: 0f);
                caught = damage.Resolve(hit, ActionId.ExaflareHit, [DamageType.Lethal], [],
                    killTargets: false); // snapshot only
                timeline.Add(ExaflareVfxDuration, () => hit?.Despawn());
            });

            timeline.Add(tBloom, () => // apply the held damage on the bloom: rolling hit is a 100% one-shot
            {
                foreach (var c in caught)
                    damage.ApplyDamage(c, 1f, ActionId.ExaflareHit, "exaflare snapshot", lethal: true);
            });
        }
    }

    // Final spread (Stray Entropy): each alive member is its own AoE source, and every source hits
    // everyone within its 5y INCLUDING the caster — so a member is caught by its own spread plus any
    // overlapping one. A single covering is survivable; two (overlap) is lethal. Snapshot here,
    // apply one delay later.
    private void ResolveSpread()
    {
        spreadHits.Clear();
        for (int i = 0; i < 8; i++)
        {
            var member = party.Get(i);
            if (member is null || !member.IsAlive()) continue;
            var helper = SpawnHelper(member.Position, 0f);
            if (helper is null) continue;
            helper.Cast(ActionId.ExaflareSpread); // VFX ignites (the visible burst)
            spreadHelpers.Add(helper);
            // [DamageType.Lethal] + killTargets:false selects the in-range set only (no
            // exclude, so self is included). The 75% is applied by ApplySpreadKill, once
            // per source covering a member — overlap therefore stacks.
            var caught = damage.Resolve(helper, ActionId.ExaflareSpread, [DamageType.Lethal], [],
                killTargets: false);
            spreadHits.AddRange(caught);
        }
    }

    // Held spread application, one delay (0.624s) after the snapshot. spreadHits has one entry per
    // covering source, so an overlapped member appears twice: show a 75% flytext per covering, and
    // KO only on overlap (>= 2 coverings) — the last covering carries the kill.
    private void ApplySpreadKill()
    {
        foreach (var grp in spreadHits.GroupBy(c => c))
        {
            var count = grp.Count();
            for (var i = 0; i < count; i++)
                damage.ApplyDamage(grp.Key, 0.75f, ActionId.ExaflareSpread, "spread snapshot",
                    lethal: count >= 2 && i == count - 1);
        }
        spreadHits.Clear();
    }

    private SimEnemy? SpawnHelper(Vector3 position, float rotation) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaHelper,
            NameId: BNpcNameId.Kefka,
            Level: 1,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            IsVisible: false,
            Placement: new Placement(position, rotation)));

    private void DespawnAll()
    {
        kefka?.Despawn();
        foreach (var h in spreadHelpers) h.Despawn();
        spreadHelpers.Clear();
    }
}
