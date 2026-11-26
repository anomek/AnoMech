using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

// The intermission after Living Liquid's death, to Brute Justice and Cruise Chaser turning
// targetable. Scenario time 0 is the death.
public sealed class TeaI1LimitCutScenario : IMultiplayerReplayable
{
    public string Name => "Limit Cut";
    public IPhase Phase => TeaZone.I1;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaI1LimitCutAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;

    // The P1 track as far in as a typical Living Liquid kill.
    public float BgmSecondsAtStart => 111.8f;

    private readonly TeaI1LimitCutSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaI1LimitCutState state = null!;
    private SimEnemy? bruteJustice;

    public TeaI1LimitCutState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaI1LimitCutState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        var limitCut = new LimitCut(world, () => state.Objects.CruiseChaser, state.Numbers, TeaActions.SuperBlasstyCharge, DeadNumbersHitGoesTo);
        DiagnosticLog.Info($"[TeaI1LimitCut] Roll: first Hawk Blasters on axis {state.FirstBlasterAxis}, numbers "
                           + string.Join(" ", state.Numbers.Select((r, k) => $"{k + 1}={r}")) + ".");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaI1LimitCutState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(1.11f, () => state.Objects.CruiseChaser?.BattleTalk(BattleTalkId.DesignationBlassty, 4000));

        limitCut.MarkNumbersAt(4.19f);

        HawkBlasterAt(6.27f, wave: 0);
        HawkBlasterAt(8.49f, wave: 1);
        HawkBlasterAt(10.69f, wave: 2);
        limitCut.WarpBehindAt(12.55f, number: 1);
        HawkBlasterAt(12.90f, wave: 3);
        limitCut.HitAt(13.64f, number: 1);
        HawkBlasterAt(15.05f, wave: 4);
        limitCut.HitAt(15.14f, number: 2);
        limitCut.WarpBehindAt(17.16f, number: 3);
        HawkBlasterAt(17.25f, wave: 5);
        limitCut.HitAt(18.31f, number: 3);
        HawkBlasterAt(19.49f, wave: 6);
        limitCut.HitAt(19.80f, number: 4);
        HawkBlasterAt(21.68f, wave: 7);
        limitCut.WarpBehindAt(21.80f, number: 5);
        limitCut.HitAt(22.95f, number: 5);
        HawkBlasterAt(23.83f, wave: 8);
        limitCut.HitAt(24.42f, number: 6);
        HawkBlasterAt(26.07f, wave: 9);
        limitCut.WarpBehindAt(26.50f, number: 7);
        limitCut.HitAt(27.58f, number: 7);
        limitCut.HitAt(29.05f, number: 8);

        limitCut.EndAt(31.09f);
        world.Events.Add(32.14f, () =>
        {
            bruteJustice?.BattleTalk(BattleTalkId.BruteJusticeArrives, 4000);
            bruteJustice?.SetPosition(new Placement(new Vector3(-6f, 0f, 0f), 0f));
            state.Objects.CruiseChaser?.SetPosition(new Placement(new Vector3(4f, 0f, 0f), 0f));
        });
        world.Events.Add(32.26f, () =>
        {
            bruteJustice?.Cast(TeaActions.JKick);
            state.Objects.CruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            world.Map.SetWeather(Weather.FairSkiesP2, transition: 1f);
            world.Map.AddEffect(0x00020004, 7);
        });
        world.Events.Add(35.29f, () =>
        {
            foreach (var boss in new[] { bruteJustice, state.Objects.CruiseChaser })
            {
                boss?.SetTargetable(true);
                boss?.SetVisibleInEnemyList(true);
            }
        });
    }

    // P2's music starts with J Kick.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        instanceWorld.Events.Add(32.26f, () => instanceWorld.SwitchBgm(Bgm.P2));
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TeaI1LimitCutAiReplayStateMessage(s.Numbers.ToArray(), s.FirstBlasterAxis) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaI1LimitCutAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaI1LimitCutState.FromNetworkReplay(msg.Numbers, msg.FirstBlasterAxis) is not { } shadowState) return null;
        ((IScenarioAi<TeaI1LimitCutState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Cruise Chaser may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaI1LimitCutState shadow) return;
        shadow.Objects.CruiseChaser ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
    }

    // Both exist unseen from the pull on: Cruise Chaser shows itself with its first warp, Brute
    // Justice with J Kick.
    private void SpawnActors()
    {
        state.Objects.CruiseChaser = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.CruiseChaser, NameId: BNpcNameId.CruiseChaser, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(3f, 0f, 0f), 0f), LayoutId: BNpcLayoutId.CruiseChaser));
        bruteJustice = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BruteJustice, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(-3f, 0f, 0f), 0f), LayoutId: BNpcLayoutId.BruteJustice));
    }

    private void HawkBlasterAt(float at, int wave) => world.Events.Add(at, () => HawkBlaster(wave));

    // Waves 1-4 and 6-9 are a pair 14y out, each 45° clockwise of the last, the second run starting
    // over from wave 1's axis; waves 5 and 10 the centre.
    private Vector3[] BlastSpots(int wave)
    {
        if (wave is 4 or 9) return [Vector3.Zero];
        var heading = (state.FirstBlasterAxis + wave % 5) * 45f;
        return [Geometry.OnCompass(heading, Geometry.BlasterRadius), Geometry.OnCompass(heading + 180f, Geometry.BlasterRadius)];
    }

    // The helpers stay in the centre; each blast's spot is the action's ground target.
    private void HawkBlaster(int wave)
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return;
        foreach (var spot in BlastSpots(wave))
            cruiseChaser.CastFromHelper(TeaActions.HawkBlaster, spot, at: new Placement(Vector3.Zero, 0f));
    }

    // Another living player, at random.
    private SimCharacter DeadNumbersHitGoesTo(IReadOnlyList<SimCharacter> alive)
        => settingsWindow.Overrides.DeadNumbersHitGoesTo is { } role && party.Get(role) is { } forced && forced.IsAlive()
            ? forced
            : state.PickRandom(alive.ToArray());
}
