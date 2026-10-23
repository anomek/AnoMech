using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I2TemporalStasis;

// From the last robot leaving at the end of P2 until Alexander Prime turns targetable to start P3.
// Scenario time 0 is that robot going untargetable; Temporal Stasis' cast starts 7.55s later.
public sealed class TeaI2TemporalStasisScenario : IMultiplayerReplayable
{
    // UNVERIFIED: a pair 4.93y apart lived and one 5.34y apart died.
    private const float HouseArrestWithin = 5f;
    private const float RestrainingOrderBeyond = 30f;
    private const float RobotX = 6f;
    private const float FrozenSeconds = 9f;
    // The engine builds a spawned actor up to ~0.3s later and drops an instant cast made before then.
    private const float HelperLead = 1f;
    // Brute Justice picks his first bait with Cruise Chaser's and fires once his turn faces them, no sooner
    // than this after; the second follows the first by SecondFlarethrower whatever the turn.
    private const float FirstFlarethrowerEarliest = 0.16f;
    private const float SecondFlarethrower = 2.32f;
    // UNVERIFIED: when he starts turning to the second's bait; he is already turning this long after the first.
    private const float SecondFlarethrowerTurn = 1.9f;

    public string Name => "Temporal Stasis";
    public IPhase Phase => TeaZone.I2;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaI2TemporalStasisAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;

    // P2's track as the slowest real kill leaves it; Temporal Stasis' end changes it.
    public float BgmSecondsAtStart => 193.87f;

    private readonly TeaI2TemporalStasisSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaI2TemporalStasisState state = null!;

    private SimEnemy? alexanderPrime;
    private SimEnemy? bruteJustice;
    private SimEnemy? cruiseChaser;
    private readonly List<SimEnemy> helpers = [];
    private readonly List<SimTether> tethers = [];
    private readonly HashSet<SimCharacter> swordHit = [];
    private readonly HashSet<SimCharacter> flameHit = [];
    private SimCharacter? swordBait;
    private SimCharacter? flameBait;

    public TeaI2TemporalStasisState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaI2TemporalStasisState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        alexanderPrime = bruteJustice = cruiseChaser = null;
        helpers.Clear();
        tethers.Clear();
        swordHit.Clear();
        flameHit.Clear();
        swordBait = null;
        flameBait = null;
        DiagnosticLog.Info($"[TeaI2TemporalStasis] Roll: Brute Justice {(state.BruteJusticeEast ? "east" : "west")}, "
                           + string.Join(" ", state.Debuffs.Select(kv => $"{kv.Key}={kv.Value}")) + ".");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaI2TemporalStasisState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnBosses);
        world.Events.Add(1.19f, () =>
        {
            world.Map.AddEffect(0x00020004, 7);
            world.Map.AddEffect(0x00020001, 2);
            world.Map.SetWeather(Weather.BeyondTime, transition: 1f);
        });
        world.Events.Add(3.17f, () => world.Map.BattleTalk(BNpcNameId.AlexanderPrime, BattleTalkId.AlexanderPrimeArrives, 4000));
        world.Events.Add(4.33f, () =>
        {
            foreach (var boss in new[] { alexanderPrime, bruteJustice, cruiseChaser }) boss?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        });
        world.Events.Add(7.55f, GiveDebuffs);
        world.Events.Add(7.55f, () => alexanderPrime?.Cast(TeaActions.TemporalStasis));
        world.Events.Add(16.70f, StopTime);
        world.Events.Add(18.45f, RemoveDebuffs);
        world.Events.Add(18.58f - HelperLead, () => SpawnHelpers(4));
        world.Events.Add(18.58f, Plaints);
        world.Events.Add(20.49f, TurnToNextSwordBait);
        world.Events.Add(20.49f, TurnToFirstFlameBait);
        world.Events.Add(20.62f, AlphaSword);
        world.Events.Add(20.69f, () => alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(21.56f, TurnToNextSwordBait);
        world.Events.Add(21.69f, AlphaSword);
        world.Events.Add(22.63f, TurnToNextSwordBait);
        world.Events.Add(22.76f, AlphaSword);
        world.Events.Add(23.83f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(25.07f, () => bruteJustice?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(25.60f, ResumeTime);
        world.Events.Add(25.8f, DespawnHelpers);
        world.Events.Add(26.80f, AlexanderPrimeReturns);
    }

    // Temporal Stasis' end brings the time-stop track; time moving again brings P3's.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(PlaintTimelines, "TeaI2TemporalStasis");
        instanceWorld.Events.Add(15.58f, () => instanceWorld.SwitchBgm(Bgm.TimeStop));
        instanceWorld.Events.Add(25.60f, () => instanceWorld.SwitchBgm(Bgm.P3));
    }

    // The Plaints play a gimmick timeline (LoadType 0) an actor drops unless it is already loaded.
    // UNVERIFIED in game.
    private static readonly (ushort Id, string Key)[] PlaintTimelines = [(5027, "mon_sp/gimmick/d2ac2_boss_gimmick05")];

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TeaI2TemporalStasisAiReplayStateMessage(s.BruteJusticeEast, TeaI2TemporalStasisState.Encode(s.Debuffs)) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaI2TemporalStasisAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaI2TemporalStasisState.FromNetworkReplay(msg.BruteJusticeEast, msg.Debuffs) is not { } shadowState) return null;
        ((IScenarioAi<TeaI2TemporalStasisState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The bosses may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaI2TemporalStasisState shadow) return;
        shadow.Objects.AlexanderPrime ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.AlexanderPrime);
        shadow.Objects.BruteJustice ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.BruteJustice);
        shadow.Objects.CruiseChaser ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
    }

    // All three wait unseen; Alexander Prime stands north, the robots either side of the centre.
    private void SpawnBosses()
    {
        var bruteJusticeX = state.BruteJusticeEast ? RobotX : -RobotX;
        state.Objects.AlexanderPrime = alexanderPrime = Spawn(BNpcBaseId.AlexanderPrime, BNpcNameId.AlexanderPrime, new Vector3(0f, 0f, -12f), EnemyListMode.Manual);
        state.Objects.BruteJustice = bruteJustice = Spawn(BNpcBaseId.BruteJustice, BNpcNameId.BruteJustice, new Vector3(bruteJusticeX, 0f, 0f), EnemyListMode.Never);
        state.Objects.CruiseChaser = cruiseChaser = Spawn(BNpcBaseId.CruiseChaser, BNpcNameId.CruiseChaser, new Vector3(-bruteJusticeX, 0f, 0f), EnemyListMode.Never);
    }

    private SimEnemy? Spawn(uint baseId, uint nameId, Vector3 at, EnemyListMode enemyList)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId, NameId: nameId, Level: Level,
            Targetable: false, EnemyList: enemyList, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(at, 0f)));

    private static ushort? StatusOf(StasisDebuff debuff) => debuff switch
    {
        StasisDebuff.AggravatedAssault => StatusId.AggravatedAssault,
        StasisDebuff.HouseArrest => StatusId.HouseArrest,
        StasisDebuff.RestrainingOrder => StatusId.RestrainingOrder,
        _ => null,
    };

    private SimCharacter? Holder(IEnumerable<PartyRole> half, StasisDebuff debuff)
        => half.Where(r => state.Debuffs.GetValueOrDefault(r) == debuff).Select(party.Get).FirstOrDefault(m => m.IsAlive());

    private (SimCharacter? Support, SimCharacter? Dps) Pair(StasisDebuff debuff)
        => (Holder(TeaI2TemporalStasisState.Supports, debuff), Holder(TeaI2TemporalStasisState.Dps, debuff));

    // No timer: they stay until the debuffs resolve. Each tether runs from the support to the DPS.
    private void GiveDebuffs()
    {
        foreach (var (role, debuff) in state.Debuffs)
            if (StatusOf(debuff) is { } statusId && party.Get(role) is { } member && member.IsAlive())
                member.AddStatus(statusId);
        foreach (var (debuff, tetherId) in new[] { (StasisDebuff.HouseArrest, TetherId.HouseArrest), (StasisDebuff.RestrainingOrder, TetherId.RestrainingOrder) })
            if (Pair(debuff) is ({ } support, { } dps))
                tethers.Add(world.Tether(support, dps, tetherId));
    }

    // Everyone freezes where they stand, every other status of theirs holding its time until time moves again.
    private void StopTime()
    {
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()).ToList())
            member.AddStatus(StatusId.TemporalDisplacement, FrozenSeconds)?.HoldingOtherTimers();
        world.Map.AddEffect(0x00200010, 1);
        world.Map.AddEffect(0x00800040, 2);
        world.Map.SetWeather(Weather.BeyondTimeStopped, transition: 1f);
    }

    private void ResumeTime()
    {
        foreach (var member in party.ActiveMembers().ToList()) member.RemoveStatus(StatusId.TemporalDisplacement);
        world.Map.AddEffect(0x00020001, 1);
        world.Map.AddEffect(0x00200001, 2);
        world.Map.SetWeather(Weather.BeyondTime, transition: 1f);
    }

    private void RemoveDebuffs()
    {
        foreach (var member in party.ActiveMembers().ToList())
        {
            member.RemoveStatus(StatusId.AggravatedAssault);
            member.RemoveStatus(StatusId.HouseArrest);
            member.RemoveStatus(StatusId.RestrainingOrder);
        }
        foreach (var tether in tethers) tether.Despawn();
        tethers.Clear();
    }

    // A Plaint of Surety on each tethered pair: House Arrest's partners must stand together and
    // Restraining Order's far apart, or both die. Aggravated Assault takes a Plaint of Severity.
    private void Plaints()
    {
        var free = new Queue<SimEnemy>(helpers.Where(h => h.IsActive));
        foreach (var debuff in new[] { StasisDebuff.HouseArrest, StasisDebuff.RestrainingOrder })
        {
            if (Pair(debuff) is not ({ } support, { } dps) || !free.TryDequeue(out var helper)) continue;
            var apart = Vector2.Distance(Flat(support.Position), Flat(dps.Position));
            var holds = debuff == StasisDebuff.HouseArrest ? apart <= HouseArrestWithin : apart >= RestrainingOrderBeyond;
            var why = debuff == StasisDebuff.HouseArrest
                ? $"House Arrest partners {apart:F1}y apart"
                : $"Restraining Order partners only {apart:F1}y apart";
            helper.Cast(TeaActions.PlaintOfSurety(support, dps, holds ? null : why));
        }
        foreach (var role in PerRole.All.Where(r => state.Debuffs.GetValueOrDefault(r) == StasisDebuff.AggravatedAssault))
            if (party.Get(role) is { } member && member.IsAlive() && free.TryDequeue(out var helper))
            {
                helper.SetPosition(new Placement(member.Position, 0f));
                helper.Cast(TeaActions.PlaintOfSeverity, member);
            }
    }

    // The client plays a slash along the model's facing, so Cruise Chaser turns to each bait by a
    // teleport on his own spot first.
    private void TurnToNextSwordBait()
    {
        if (cruiseChaser is not { } robot || NextClosest(robot, swordHit) is not { } bait) return;
        swordBait = bait;
        robot.SetRotation(robot.Placement().Face(bait.Position).Rotation);
    }

    private void AlphaSword()
    {
        if (cruiseChaser is { IsActive: true } robot && swordBait is { } bait) robot.Cast(TeaActions.AlphaSwordP3(bait));
        swordBait = null;
    }

    // Brute Justice turns to each of his baits by ActorMoves instead, and fires once the model faces them.
    private void TurnToFirstFlameBait()
    {
        if (bruteJustice is { IsActive: true } robot && TurnToNextFlameBait(robot) is { } turn)
            world.Events.Add(MathF.Max(FirstFlarethrowerEarliest, turn), FirstFlarethrower);
    }

    private void FirstFlarethrower()
    {
        Flarethrower();
        world.Events.Add(SecondFlarethrowerTurn, () =>
        {
            if (bruteJustice is { IsActive: true } robot) TurnToNextFlameBait(robot);
        });
        world.Events.Add(SecondFlarethrower, Flarethrower);
    }

    // Returns how long the model takes to turn; the sim's own facing changes at once.
    private float? TurnToNextFlameBait(SimEnemy robot)
    {
        if (NextClosest(robot, flameHit) is not { } bait) return null;
        flameBait = bait;
        var seconds = robot.SecondsToFace(bait.Position);
        robot.Face(bait.Position);
        return seconds;
    }

    private void Flarethrower()
    {
        if (bruteJustice is { IsActive: true } robot && flameBait is { } bait) robot.Cast(TeaActions.FlarethrowerStasis(bait));
        flameBait = null;
    }

    // Each goes to the closest living player that robot hasn't aimed one at yet.
    private SimCharacter? NextClosest(SimEnemy robot, HashSet<SimCharacter> hit)
    {
        if (!robot.IsActive) return null;
        var from = Flat(robot.Position);
        var target = party.ActiveMembers().Where(m => m.IsAlive() && !hit.Contains(m)).MinBy(m => Vector2.Distance(Flat(m.Position), from));
        if (target is not null) hit.Add(target);
        return target;
    }

    private void AlexanderPrimeReturns()
    {
        if (alexanderPrime is not { IsActive: true } boss) return;
        boss.SetPosition(new Placement(Vector3.Zero, 0f));
        boss.PlayActionTimeline(ActionTimelineId.WarpEnd);
        boss.SetTargetable(true);
        boss.SetVisibleInEnemyList(true);
    }

    private void SpawnHelpers(int count)
    {
        for (var i = 0; i < count; i++)
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.Helper, NameId: BNpcNameId.AlexanderPrime, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
                    Placement: new Placement(Vector3.Zero, 0f))) is { } helper)
                helpers.Add(helper);
    }

    private void DespawnHelpers()
    {
        foreach (var helper in helpers) helper.Despawn();
        helpers.Clear();
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
