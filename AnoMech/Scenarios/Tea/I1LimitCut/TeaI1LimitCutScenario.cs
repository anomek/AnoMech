using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

// The intermission after Living Liquid's death, to Brute Justice and Cruise Chaser turning
// targetable. Scenario time 0 is the death.
public sealed class TeaI1LimitCutScenario : IMultiplayerReplayable
{
    // The engine builds a spawned actor up to ~0.3s later and drops an instant cast made before then.
    private const float BlastHelperLead = 1f;
    // Real moves land 0-0.135s before their warp_end. A peer's run cycle for a move in the same
    // snapshot would cut the warp_end that reveals Cruise Chaser.
    private const float WarpMoveLead = 0.1f;
    private const float WarpBehind = 2f;
    // The client plays a slash along the model's facing, so Cruise Chaser turns to each bait by a
    // teleport on his own spot first; real turns land up to 0.14s before the hit.
    private const float TurnLead = 0.13f;

    public string Name => "Limit Cut";
    public IPhase Phase => TeaZone.I1;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaI1LimitCutAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;

    // The P1 track as far in as a typical Living Liquid kill; P2's starts with J Kick.
    public float BgmSecondsAtStart => 111.8f;

    private readonly TeaI1LimitCutSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaI1LimitCutState state = null!;
    private SimEnemy? bruteJustice;
    private readonly List<SimEnemy>[] blasts = Enumerable.Range(0, 10).Select(_ => new List<SimEnemy>()).ToArray();
    private readonly SimCharacter?[] baits = new SimCharacter?[8];

    public TeaI1LimitCutState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaI1LimitCutState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        foreach (var wave in blasts) wave.Clear();
        Array.Clear(baits);
        DiagnosticLog.Info($"[TeaI1LimitCut] Roll: first Hawk Blasters on axis {state.FirstBlasterAxis}, numbers "
                           + string.Join(" ", state.Numbers.Select((r, k) => $"{k + 1}={r}")) + ".");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaI1LimitCutState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(0.11f, () => world.Map.BattleTalk(BNpcNameId.CruiseChaser, BattleTalkId.DesignationBlassty, 4000));

        world.Events.Add(3.19f, () => state.Objects.CruiseChaser?.SetAnimationState(1, 1));
        world.Events.Add(3.19f, () => AttachNumber(0));
        world.Events.Add(3.22f, () => AttachNumber(1));
        world.Events.Add(3.25f, () => AttachNumber(2));
        world.Events.Add(3.28f, () => AttachNumber(3));
        world.Events.Add(3.31f, () => AttachNumber(4));
        world.Events.Add(3.34f, () => AttachNumber(5));
        world.Events.Add(3.37f, () => AttachNumber(6));
        world.Events.Add(3.40f, () => AttachNumber(7));

        HawkBlasterAt(5.27f, wave: 0);
        HawkBlasterAt(7.49f, wave: 1);
        HawkBlasterAt(9.69f, wave: 2);
        WarpBehindOddAt(11.55f, pair: 0);
        HawkBlasterAt(11.90f, wave: 3);
        NumberHitAt(12.64f, 0);
        HawkBlasterAt(14.05f, wave: 4);
        NumberHitAt(14.14f, 1);
        WarpBehindOddAt(16.16f, pair: 1);
        HawkBlasterAt(16.25f, wave: 5);
        NumberHitAt(17.31f, 2);
        HawkBlasterAt(18.49f, wave: 6);
        NumberHitAt(18.80f, 3);
        HawkBlasterAt(20.68f, wave: 7);
        WarpBehindOddAt(20.80f, pair: 2);
        NumberHitAt(21.95f, 4);
        HawkBlasterAt(22.83f, wave: 8);
        NumberHitAt(23.42f, 5);
        HawkBlasterAt(25.07f, wave: 9);
        WarpBehindOddAt(25.50f, pair: 3);
        NumberHitAt(26.58f, 6);
        NumberHitAt(28.05f, 7);

        world.Events.Add(30.09f, () => state.Objects.CruiseChaser?.SetAnimationState(1, 0));
        world.Events.Add(31.14f, () =>
        {
            world.Map.BattleTalk(BNpcNameId.BruteJustice, BattleTalkId.BruteJusticeArrives, 4000);
            bruteJustice?.SetPosition(new Placement(new Vector3(-6f, 0f, 0f), 0f));
            state.Objects.CruiseChaser?.SetPosition(new Placement(new Vector3(4f, 0f, 0f), 0f));
        });
        world.Events.Add(31.26f, () =>
        {
            ClearBlasts(8);
            ClearBlasts(9);
            bruteJustice?.Cast(TeaActions.JKick);
            state.Objects.CruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            world.Map.SetWeather(Weather.FairSkiesP2, transition: 1f);
            world.Map.AddEffect(0x00020004, 7);
        });
        world.Events.Add(34.29f, () =>
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
        Natives.TimelinePreload.Preload(BlastTimelines, "TeaI1LimitCut");
        instanceWorld.Events.Add(31.26f, () => instanceWorld.SwitchBgm(Bgm.P2));
    }

    // Hawk Blaster's explosion is its AnimationEnd gimmick timeline (LoadType 0), which an actor
    // drops unless it is already loaded.
    private static readonly (ushort Id, string Key)[] BlastTimelines =
    [
        (7587, "mon_sp/gimmick/d2az_boss_gimmick06"),
    ];

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
            Placement: new Placement(new Vector3(3f, 0f, 0f), 0f)));
        bruteJustice = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BruteJustice, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(-3f, 0f, 0f), 0f)));
    }

    // Dead players get none; nothing removes a marker, it plays out on its own.
    private void AttachNumber(int k)
    {
        if (Numbered(k) is { } member) member.AttachLockonVfx(LimitCutLockons[k]);
    }

    private void HawkBlasterAt(float at, int wave)
    {
        world.Events.Add(at - BlastHelperLead, () => SpawnBlasters(wave));
        world.Events.Add(at, () => HawkBlaster(wave));
    }

    // Waves 1-4 and 6-9 are a pair 14y out, each 45° clockwise of the last, the second run starting
    // over from wave 1's axis; waves 5 and 10 the centre.
    private Vector3[] BlastSpots(int wave)
    {
        if (wave is 4 or 9) return [Vector3.Zero];
        var heading = (state.FirstBlasterAxis + wave % 5) * 45f;
        return [Geometry.OnCompass(heading, Geometry.BlasterRadius), Geometry.OnCompass(heading + 180f, Geometry.BlasterRadius)];
    }

    // The real fight fires every blast from two helpers; each explosion gets its own, lasting
    // through the next wave for its timeline to play out. The real helpers carry Cruise Chaser's
    // name when they fire.
    private void SpawnBlasters(int wave)
    {
        foreach (var at in BlastSpots(wave))
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.CruiseChaser, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(at, 0f))) is { } helper)
                blasts[wave].Add(helper);
    }

    private void HawkBlaster(int wave)
    {
        ClearBlasts(wave - 2);
        foreach (var helper in blasts[wave]) helper.Cast(TeaActions.HawkBlaster, helper.Position);
    }

    private void ClearBlasts(int wave)
    {
        if (wave < 0) return;
        foreach (var helper in blasts[wave]) helper.Despawn();
        blasts[wave].Clear();
    }

    private void WarpBehindOddAt(float at, int pair)
    {
        world.Events.Add(at - WarpMoveLead, () => MoveBehindOdd(pair));
        world.Events.Add(at, () => state.Objects.CruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpEnd));
    }

    // 2y behind the odd number, facing where they face. With no odd number alive it warps in place.
    private void MoveBehindOdd(int pair)
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser || Numbered(2 * pair) is not { } odd) return;
        var back = new Vector3(MathF.Sin(odd.Rotation), 0f, MathF.Cos(odd.Rotation)) * -WarpBehind;
        cruiseChaser.SetPosition(new Placement(odd.Position + back, odd.Rotation));
    }

    private void NumberHitAt(float at, int k)
    {
        world.Events.Add(at - TurnLead, () => TurnToBait(k));
        world.Events.Add(at, () => FireAtBait(k));
    }

    // The hit goes along this facing even if the bait moves or dies before it.
    private void TurnToBait(int k)
    {
        baits[k] = HitTarget(k);
        if (state.Objects.CruiseChaser is { } cruiseChaser && baits[k] is { } bait)
            cruiseChaser.SetRotation(cruiseChaser.Placement().Face(bait.Position).Rotation);
    }

    private void FireAtBait(int k)
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser || baits[k] is not { } bait) return;
        cruiseChaser.Cast(k % 2 == 0 ? TeaActions.AlphaSword(bait) : TeaActions.SuperBlasstyCharge(bait));
    }

    private SimCharacter? Numbered(int k) => party.Get(state.Numbers[k]) is { } member && member.IsAlive() ? member : null;

    // A dead number's hit goes to another living player, at random.
    private SimCharacter? HitTarget(int k)
    {
        if (Numbered(k) is { } numbered) return numbered;
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
        var target = settingsWindow.Overrides.DeadNumbersHitGoesTo is { } role && party.Get(role) is { } forced && forced.IsAlive()
            ? forced
            : alive.Length == 0 ? null : state.PickRandom(alive);
        DiagnosticLog.Info($"[TeaI1LimitCut] Number {k + 1} is dead; its hit goes to {(target as ISimPartyMember)?.Role.ToString() ?? "nobody"}.");
        return target;
    }
}
