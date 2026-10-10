using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Ucob.UcobConstants;
using Actions = AnoMech.Scenarios.Ucob.UcobActions;

namespace AnoMech.Scenarios.Ucob.P5Exaflares;

// UCOB P5 "Exaflares": Bahamut Prime lays six parallel lanes across the arena, fired as three
// pairs 3s apart, each lane erupting six times in 8y steps. Runs solo or with bots
// (UcobP5ExaflaresAi). The geometry and timings are measured from a clear log; see
// UcobP5ExaflaresState for the provenance.
//
// Each lane's first eruption is the release of its own arrow-omen cast (ExaflareFirst), so the
// telegraph and hit 1 share one helper and cannot drift apart; the five follow-ups are instant
// ExaflareRest casts from a helper spawned at each step. The flame itself is spawned by hand
// (VfxPath.ExaflareEruption) because these actions carry no VFX the action effect could play.
// Lingering flame is decorative: only the snapshot kills.
public sealed class UcobP5ExaflaresScenario : IMultiplayerReplayable
{
    public string Name => "Exaflares";
    public IPhase Phase => UcobZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UcobP5ExaflaresAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly UcobP5ExaflaresSettingsWindow settingsWindow = new();

    // Keep an eruption's helper alive this long so its flame isn't cut mid-animation.
    private const float HitVfxSeconds = 3f;
    private const float DespawnAfterLastHit = 4f;

    private UcobP5ExaflaresState state = null!;

    // Polled by the multiplayer host; null until Run has rolled the pattern.
    public UcobP5ExaflaresState? LastState { get; private set; }
    private SimWorld world = null!;
    private SimEnemy? bahamut;
    private readonly List<SimEnemy> helpers = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        helpers.Clear();

        state = new UcobP5ExaflaresState(world.Rng, settingsWindow.Overrides);
        LastState = state;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UcobP5ExaflaresState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnBahamut);
        world.Events.Add(UcobP5ExaflaresState.BossCastAt,
            () => bahamut?.Cast(ActionId.Exaflare));
        foreach (var line in state.Lines) LaunchLine(line);
        world.Events.Add(state.LastHitAt + DespawnAfterLastHit, DespawnAll);
    }

    private void SpawnBahamut()
    {
        bahamut = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BahamutPrime,
            NameId: BNpcNameId.BahamutPrime,
            Level: UcobConstants.Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, 0f),
            ModelCharaId: ModelCharaId.GoldenBahamut));
    }

    // One lane. The arrow omen rides ExaflareFirst's own cast at the lane's first step and its
    // release is that step's eruption; every later step is an instant cast from its own helper.
    private void LaunchLine(ExaflareLine line)
    {
        SimEnemy? head = null;
        world.Events.Add(line.TelegraphAt, () =>
        {
            head = SpawnHelper(line.Start, line.Rotation);
            head?.Cast(Actions.ExaflareFirst);
        });

        for (var i = 0; i < line.Hits.Count; i++)
        {
            var hit = line.Hits[i];
            var isFirst = i == 0;
            SimEnemy? source = null;
            world.Events.Add(hit.Time, () =>
            {
                source = isFirst ? head : SpawnHelper(hit.Position, line.Rotation);
                source?.AddVfx(VfxPath.ExaflareEruption, persistent: false);
                if (!isFirst) source?.Cast(Actions.ExaflareRest);
            });
            world.Events.Add(hit.Time + HitVfxSeconds, () => source?.Despawn());
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
            Visibility: SpawnVisibility.InvisibleHelper,
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

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new UcobP5ExaflaresAiReplayStateMessage(s.Direction.RadiansFromNorth, s.LaneOrder.ToArray())
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UcobP5ExaflaresAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = UcobP5ExaflaresState.FromNetworkReplay(msg.DirectionRadians, msg.LaneOrder);
        if (shadowState == null) return null;
        ((IScenarioAi<UcobP5ExaflaresState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
