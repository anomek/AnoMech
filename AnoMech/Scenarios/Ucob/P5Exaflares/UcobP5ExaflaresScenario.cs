using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Ucob.UcobConstants;

namespace AnoMech.Scenarios.Ucob.P5Exaflares;

// UCOB P5 "Exaflares": Bahamut Prime lays six parallel lanes across the arena, fired as three
// pairs 3s apart, each lane erupting six times in 8y steps. Runs solo or with bots
// (UcobP5ExaflaresAi). The geometry and timings are measured from a clear log; see
// UcobP5ExaflaresState for the provenance.
//
// Each lane's first eruption is the release of its own arrow-omen cast (ExaflareFirst), so the
// telegraph and hit 1 share one helper and cannot drift apart; the five follow-ups are instant
// ExaflareRest casts from a helper spawned at each step. The flame itself is spawned by hand
// (VfxPath.ExaflareEruption) because these actions carry no VFX the action effect could play. A hit snapshots who is standing in it
// and holds the kill one application delay later, so the KO lands on the visible bloom rather
// than on the invisible snapshot instant. Lingering flame is decorative: only the snapshot kills.
//
// The timeline runs on a scenario-local Stopwatch (`timeline`), not the engine's ms-truncated
// UpdateDelta, so the arrow cast bars and the rolling hits stay locked together and ignore the
// Speed buttons.
public sealed class UcobP5ExaflaresScenario : IScenario
{
    public string Name => "Exaflares";
    public IPhase Phase => UcobZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UcobP5ExaflaresAi()];

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UcobP5ExaflaresSettingsWindow settingsWindow = new();

    // Snapshot -> kill application delay: sets only the instant a caught player dies, so the KO
    // reads off the bloom instead of the snapshot. Same hold the UMAD exaflares use.
    private const float KillDelay = 0.6f;
    // Keep an eruption's helper alive this long so its flame isn't cut mid-animation.
    private const float HitVfxSeconds = 3f;
    private const float DespawnAfterLastHit = 4f;

    private readonly EventScheduler timeline = new();
    private readonly Stopwatch wallClock = new();
    private double lastWall;
    private const double FrameGapCapSeconds = 0.25; // skip pause / alt-tab / hitch frames

    private UcobP5ExaflaresState state = null!;

    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private SimEnemy? bahamut;
    private readonly List<SimEnemy> helpers = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        damage = new DamageSolver(worldParam.Party);
        helpers.Clear();

        // Re-arm the scenario clock for this run (the scenario object is reused).
        timeline.Clear();
        wallClock.Restart();
        lastWall = 0;

        state = new UcobP5ExaflaresState(settingsWindow.Overrides, timeline);

        // Bots schedule on the scenario `timeline` (after Clear, so their adds are absolute).
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UcobP5ExaflaresState>)AiStrats[idx]).Run(state, world);

        timeline.Add(0f, SpawnBahamut);
        timeline.Add(UcobP5ExaflaresState.BossCastAt,
            () => bahamut?.Cast(ActionId.Exaflare, castSeconds: UcobP5ExaflaresState.BossCastSeconds));
        foreach (var line in state.Lines) LaunchLine(line);
        timeline.Add(state.LastHitAt + DespawnAfterLastHit, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        // Advance the timeline by real wall time, capping pause/hitch gaps so a freeze can't
        // fast-forward it. This is what keeps the scenario drift-free.
        var now = wallClock.Elapsed.TotalSeconds;
        var wallDelta = now - lastWall;
        lastWall = now;
        if (wallDelta > 0 && wallDelta <= FrameGapCapSeconds)
            timeline.Tick((float)wallDelta);
    }

    private void SpawnBahamut()
    {
        bahamut = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BahamutPrime,
            NameId: BNpcNameId.BahamutPrime,
            Level: UcobConstants.Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            IsVisible: true,
            Placement: new Placement(Vector3.Zero, 0f),
            ModelCharaId: ModelCharaId.GoldenBahamut));
    }

    // One lane. The arrow omen rides ExaflareFirst's own cast at the lane's first step and its
    // release is that step's eruption; every later step is an instant cast from its own helper.
    private void LaunchLine(ExaflareLine line)
    {
        SimEnemy? head = null;
        timeline.Add(line.TelegraphAt, () =>
        {
            head = SpawnHelper(line.Start, line.Rotation);
            head?.Cast(ActionId.ExaflareFirst, castSeconds: UcobP5ExaflaresState.TelegraphSeconds);
            timeline.Add(UcobP5ExaflaresState.TelegraphSeconds + HitVfxSeconds, () => head?.Despawn());
        });

        for (var i = 0; i < line.Hits.Count; i++)
        {
            var hit = line.Hits[i];
            var isFirst = i == 0;
            timeline.Add(hit.Time, () =>
            {
                var actionId = isFirst ? ActionId.ExaflareFirst : ActionId.ExaflareRest;
                SimEnemy? source = head;
                if (!isFirst)
                {
                    source = SpawnHelper(hit.Position, line.Rotation);
                    source?.Cast(actionId, castSeconds: 0f, animationLock: 0f);
                    timeline.Add(HitVfxSeconds, () => source?.Despawn());
                }
                source?.AddVfx(VfxPath.ExaflareEruption, persistent: false);

                var caught = damage.Resolve(source, actionId, [DamageType.Lethal], [], killTargets: false);
                timeline.Add(KillDelay, () =>
                {
                    foreach (var c in caught)
                        damage.ApplyDamage(c, 1f, actionId, "exaflare snapshot", lethal: true);
                });
            });
        }
    }

    private SimEnemy? SpawnHelper(Vector3 position, float rotation)
    {
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Helper,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            // Drawn on purpose: the eruption is an ActionTimeline on the helper, and a
            // DisableDraw'd actor plays none. BNpcBase 0x18D6's ModelChara has no mesh,
            // so "visible" still shows nothing but the fire.
            IsVisible: true,
            Placement: new Placement(position, rotation)));
        if (helper != null) helpers.Add(helper);
        return helper;
    }

    private void DespawnAll()
    {
        bahamut?.Despawn();
        foreach (var helper in helpers) helper.Despawn();
        helpers.Clear();
    }
}
