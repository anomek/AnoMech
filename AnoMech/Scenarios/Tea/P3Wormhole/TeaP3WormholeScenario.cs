using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

// Alexander Prime's Wormhole Formation through to the Enumerations. Time 0 is 1s before Wormhole
// Formation's cast, under him with the off tank holding him.
public sealed class TeaP3WormholeScenario : IMultiplayerReplayable
{
    private const float SacramentRadius = 21.5f;
    private const float SuperJumpLands = 0.93f;
    // Alexander Prime turns to Incinerating Heat's target this long into its cast.
    private const float IncineratingHeatTurn = 0.23f;

    public string Name => "Wormhole";
    public IPhase Phase => TeaZone.P3;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new TeaP3WormholeAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    public float BgmSecondsAtStart => 115.2f;

    private readonly TeaP3WormholeSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private TeaP3WormholeState state = null!;

    private SimEnemy? alexanderPrime;
    private SimEnemy? bruteJustice;
    private SimEnemy? cruiseChaser;
    // A pair per size, each size spawned as the last one bursts.
    private readonly SimEventObject?[,] wormholes = new SimEventObject?[3, 2];
    private float rayFacing;

    public TeaP3WormholeState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new TeaP3WormholeState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        alexanderPrime = bruteJustice = cruiseChaser = null;
        var limitCut = new LimitCut(world, () => cruiseChaser, state.Numbers, TeaActions.SuperBlasstyChargeP3, state.PickRandom);
        Array.Clear(wormholes);
        DiagnosticLog.Info($"[TeaP3Wormhole] Roll: Brute Justice {(state.BruteJusticeNorthWest ? "NW" : "NE")}, "
                           + $"wormholes {(state.PuddlesMirrored ? "WSW/ENE" : "WNW/ESE")}, numbers {string.Join(" ", state.Numbers.Select((r, k) => $"{k + 1}={r}"))}, "
                           + $"enumerations {string.Join(" ", state.Enumerations)}.");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TeaP3WormholeState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(0f, GiveEnigmaCodex);
        world.Events.Add(0.6f, FaceTank);
        world.Events.Add(1.00f, () => alexanderPrime?.Cast(ActionId.WormholeFormation, animationLock: 3.1f));

        world.Events.Add(8.07f, AlexanderPrimeLeaves);
        world.Events.Add(10.27f, WormholeLayout);
        world.Events.Add(10.85f, () =>
        {
            bruteJustice?.SetTarget(LivingOrNext(PartyRole.MainTank), follow: false);
            cruiseChaser?.SetTarget(LivingOrNext(PartyRole.MainTank), follow: false);
        });
        world.Events.Add(11.46f, () => cruiseChaser?.Cast(ActionId.LimitCutP3, animationLock: 3.1f));
        world.Events.Add(13.47f, () => bruteJustice?.Cast(ActionId.LinkUp, animationLock: 2.1f));
        world.Events.Add(14.17f, () => cruiseChaser?.SetAnimationState(1, 1));
        world.Events.Add(16.47f, () => alexanderPrime?.Cast(ActionId.VoidOfRepentance, animationLock: 2.1f));
        world.Events.Add(16.63f, () => cruiseChaser?.PlayActionTimeline(ActionTimelineId.WarpStart));
        limitCut.MarkNumbersAt(17.25f);
        world.Events.Add(17.54f, SpawnChakrams);
        world.Events.Add(18.26f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams)
            {
                chakram.ActorControl.PopIn();
                chakram.SetVisibleInEnemyList(true);
            }
        });
        world.Events.Add(19.39f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams.Where(c => c.IsActive)) chakram.Cast(TeaActions.EyeOfTheChakram);
        });
        world.Events.Add(20.33f, () => SpawnWormholes(0, EObjId.Wormhole1, 0x400003u));
        world.Events.Add(20.38f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams.Where(c => c.IsActive))
                chakram.SetTarget(LivingOrNext(PartyRole.MainTank), follow: false);
        });
        world.Events.Add(21.30f, () => WormholesBeat(0, 1, 0x2));
        world.Events.Add(22.60f, () => bruteJustice?.Cast(ActionId.SuperJump, animationLock: 0.2f));
        limitCut.WarpBehindAt(25.62f, number: 1);
        world.Events.Add(26.70f, SuperJumpOnFarthest);
        limitCut.HitAt(26.70f, number: 1);
        world.Events.Add(27.55f, () =>
        {
            foreach (var chakram in state.Objects.Chakrams) chakram.Despawn();
        });
        world.Events.Add(27.61f, () => alexanderPrime?.Cast(TeaActions.SacramentWormhole));
        limitCut.HitAt(28.17f, number: 2);
        world.Events.Add(28.79f, AimApocalypticRay);
        world.Events.Add(29.11f, () => bruteJustice?.Cast(ActionId.ApocalypticRay, animationLock: 7.1f));
        foreach (var t in (float[])[29.74f, 30.80f, 31.87f, 32.94f, 34.01f])
            world.Events.Add(t, RayHit);
        limitCut.WarpBehindAt(29.85f, number: 3);
        limitCut.HitAt(30.93f, number: 3);
        world.Events.Add(31.18f, () => WormholesBeat(0, 4, 0x8));
        world.Events.Add(31.30f, () => Repent(TeaActions.Repentance1));
        world.Events.Add(31.55f, () => SpawnWormholes(1, EObjId.Wormhole2, 0x420003u));
        world.Events.Add(32.20f, () => WormholesBeat(1, 1, 0x2));
        limitCut.HitAt(32.40f, number: 4);
        world.Events.Add(32.40f, () => DespawnWormholes(0));
        limitCut.WarpBehindAt(34.10f, number: 5);
        limitCut.HitAt(35.18f, number: 5);
        world.Events.Add(35.40f, () => WormholesBeat(1, 4, 0x8));
        world.Events.Add(35.54f, () => Repent(TeaActions.Repentance2));
        world.Events.Add(35.70f, () => SpawnWormholes(2, EObjId.Wormhole3, 0x400003u));
        world.Events.Add(36.40f, () => WormholesBeat(2, 1, 0x2));
        limitCut.HitAt(36.65f, number: 6);
        world.Events.Add(36.70f, () => DespawnWormholes(1));
        limitCut.WarpBehindAt(38.33f, number: 7);
        world.Events.Add(39.26f, () => bruteJustice?.Cast(ActionId.MissileCommand, animationLock: 3.1f));
        limitCut.HitAt(39.41f, number: 7);
        world.Events.Add(39.64f, () => WormholesBeat(2, 4, 0x8));
        world.Events.Add(39.77f, () => Repent(TeaActions.Repentance3));
        world.Events.Add(40.78f, IncineratingHeat);
        limitCut.HitAt(40.88f, number: 8);
        world.Events.Add(40.90f, () => DespawnWormholes(2));
        limitCut.EndAt(42.58f);
        world.Events.Add(42.58f, () => cruiseChaser?.SetVisibleInEnemyList(false));
        world.Events.Add(45.31f, MarkEnumerations);
        world.Events.Add(45.44f, () =>
        {
            bruteJustice?.PlayActionTimeline(ActionTimelineId.WarpStart);
            bruteJustice?.SetVisibleInEnemyList(false);
        });
        world.Events.Add(46.77f, () => bruteJustice?.SetTarget(null));
        world.Events.Add(47.95f, () => alexanderPrime?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(49.99f, AlexanderPrimeAppears);
        world.Events.Add(50.44f, ResolveEnumerations);
        world.Events.Add(50.58f, FaceTank);
    }

    // A wormhole nobody soaks goes off on everyone.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved is { Action.ActionId: ActionId.Repentance1 or ActionId.Repentance2 or ActionId.Repentance3, Hits.Count: 0 })
            alexanderPrime?.CastFromHelper(TeaActions.Confession, at: resolved.Origin);
    }

    public void Tick(float delta, float elapsed) { }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new TeaP3WormholeAiReplayStateMessage(s.BruteJusticeNorthWest, s.PuddlesMirrored, s.Numbers.ToArray(), s.Enumerations.ToArray())
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TeaP3WormholeAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (TeaP3WormholeState.FromNetworkReplay(msg.BruteJusticeNorthWest, msg.PuddlesMirrored, msg.Numbers, msg.Enumerations)
            is not { } shadowState) return null;
        ((IScenarioAi<TeaP3WormholeState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // The bosses and the chakrams may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not TeaP3WormholeState shadow) return;
        var objects = shadow.Objects;
        objects.AlexanderPrime ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.AlexanderPrime);
        objects.BruteJustice ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.BruteJustice);
        objects.CruiseChaser ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);
        foreach (var chakram in peerEnemies.Values.Where(e => e.BNpcBaseId == BNpcBaseId.SteamChakram && e.IsActive))
            if (!objects.Chakrams.Contains(chakram)) objects.Chakrams.Add(chakram);
    }

    // Alexander Prime up in the centre, the robots hidden where Inception left them.
    private void SpawnActors()
    {
        state.Objects.AlexanderPrime = alexanderPrime = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.AlexanderPrime, NameId: BNpcNameId.AlexanderPrime, Level: Level,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, MathF.PI), LayoutId: BNpcLayoutId.AlexanderPrime, ModelState: 0x15));
        state.Objects.BruteJustice = bruteJustice = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BruteJustice, NameId: BNpcNameId.BruteJustice, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(19.43f, 0f, 0f), MathF.PI / 2f), LayoutId: BNpcLayoutId.BruteJustice));
        state.Objects.CruiseChaser = cruiseChaser = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.CruiseChaser, NameId: BNpcNameId.CruiseChaser, Level: Level,
            Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Vector3.Zero, 0f), LayoutId: BNpcLayoutId.CruiseChaser));
    }

    // The True Heart's gift from Inception, which lasts the rest of the phase.
    private void GiveEnigmaCodex()
    {
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive())) member.AddStatus(StatusId.EnigmaCodex);
    }

    // -- Alexander Prime -----------------------------------------------------------

    private SimCharacter? LivingOrNext(PartyRole role)
    {
        if (party.Get(role) is { } member && member.IsAlive()) return member;
        return PerRole.All.Select(party.Get).FirstOrDefault(m => m.IsAlive());
    }

    private void FaceTank()
    {
        if (alexanderPrime is not { Targetable: true } boss || LivingOrNext(PartyRole.OffTank) is not { } tank) return;
        boss.SetTarget(tank, follow: false);
        boss.KeepFacing(tank);
    }

    private void AlexanderPrimeAppears()
    {
        if (alexanderPrime is not { } boss) return;
        boss.SetPosition(new Placement(Vector3.Zero, 0f));
        boss.SetTargetable(true);
        boss.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    // He stays in the enemy list and keeps his target while away.
    private void AlexanderPrimeLeaves()
    {
        if (alexanderPrime is not { } boss) return;
        boss.KeepFacing(null);
        boss.SetTargetable(false);
        boss.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    private SimCharacter? Living(PartyRole role) => party.Get(role) is { } member && member.IsAlive() ? member : null;

    // -- Wormhole -------------------------------------------------------------------------

    // Brute Justice lands on his farthest player. The hit goes out from where he stood; the server
    // moves him onto the spot as it lands.
    private void SuperJumpOnFarthest()
    {
        if (bruteJustice is not { } boss || party.Find.Farest(boss.Position) is not { } bait) return;
        var landing = new Placement(bait.Position, boss.Rotation);
        boss.Cast(TeaActions.SuperJumpHit, bait);
        world.Events.Add(SuperJumpLands, () => boss.SetPosition(landing));
    }

    // Alexander Prime south; Brute Justice and Cruise Chaser on the north intercardinals, either way round.
    private void WormholeLayout()
    {
        var west = new Placement(new Vector3(-15.2f, 0f, -15.2f), MathF.PI / 4f);
        var east = new Placement(new Vector3(15.2f, 0f, -15.2f), -MathF.PI / 4f);
        alexanderPrime?.SetPosition(new Placement(new Vector3(0f, 0f, SacramentRadius), MathF.PI));
        bruteJustice?.SetPosition(state.BruteJusticeNorthWest ? west : east);
        cruiseChaser?.SetPosition(state.BruteJusticeNorthWest ? east : west);
        foreach (var boss in new[] { alexanderPrime, bruteJustice, cruiseChaser })
            boss?.PlayActionTimeline(ActionTimelineId.WarpEnd);
        foreach (var robot in new[] { bruteJustice, cruiseChaser })
            robot?.SetVisibleInEnemyList(true);
    }

    // North, and on Brute Justice's side, each aimed at a player of its own picking.
    private void SpawnChakrams()
    {
        state.Objects.Chakrams.Clear();
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        foreach (var from in new[] { new Vector3(0f, 0f, -21.5f), new Vector3(state.BruteJusticeNorthWest ? -21.5f : 21.5f, 0f, 0f) })
        {
            var aim = alive.Count > 0 ? state.PickRandom(alive).Position : Vector3.Zero;
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.SteamChakram, NameId: BNpcNameId.SteamChakram, Level: Level,
                    Targetable: false, EnemyList: EnemyListMode.Manual, Visibility: SpawnVisibility.HiddenUntilPopIn,
                    Placement: new Placement(from, MathF.Atan2(aim.X - from.X, aim.Z - from.Z)),
                    LayoutId: BNpcLayoutId.SteamChakram)) is { } chakram)
                state.Objects.Chakrams.Add(chakram);
        }
    }

    public static Vector3[] WormholeSpots(bool mirrored)
        => mirrored ? [new(-11.09f, 0f, 4.59f), new(11.09f, 0f, -4.59f)] : [new(-11.09f, 0f, -4.59f), new(11.09f, 0f, 4.59f)];

    // Each size is a new pair of objects in the same spots.
    private void SpawnWormholes(int size, uint eobjId, uint arg2)
    {
        var spots = WormholeSpots(state.PuddlesMirrored);
        for (var k = 0; k < 2; k++)
            wormholes[size, k] = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = eobjId,
                Placement = new Placement(spots[k], 0f),
                EventId = EObjId.DirectorEventId,
                EntityId = EObjId.WormholesEntityId + (uint)(2 * size + k),
                TargetableStatus = 5,
                Arg2 = arg2 + ((uint)k << 16),
                TimelineState = 4,
            });
    }

    private void WormholesBeat(int size, uint beat, uint mask)
    {
        for (var k = 0; k < 2; k++) wormholes[size, k]?.PlayBeat(beat, mask);
    }

    private void DespawnWormholes(int size)
    {
        for (var k = 0; k < 2; k++)
        {
            wormholes[size, k]?.Despawn();
            wormholes[size, k] = null;
        }
    }

    // Each wormhole takes its soak from a helper on its centre.
    private void Repent(EnemyAction repentance)
    {
        foreach (var spot in WormholeSpots(state.PuddlesMirrored))
            alexanderPrime?.CastFromHelper(repentance, at: new Placement(spot, 0f));
    }

    // At whoever is farthest as he turns; the five rays keep that line.
    private void AimApocalypticRay()
    {
        if (bruteJustice is not { } boss || party.Find.Farest(boss.Position) is not { } aim) return;
        rayFacing = boss.Placement().Face(aim.Position).Rotation;
        boss.TurnTo(rayFacing);
    }

    private void RayHit() => bruteJustice?.CastFromHelper(TeaActions.ApocalypticRayHit, at: new Placement(bruteJustice.Position, rayFacing));

    private void IncineratingHeat()
    {
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToList();
        if (alexanderPrime is not { } boss || alive.Count == 0) return;
        var target = state.PickRandom(alive);
        target.AttachLockonVfx(LockonId.IncineratingHeat);
        boss.Cast(TeaActions.IncineratingHeat, target);
        world.Events.Add(IncineratingHeatTurn, () => boss.SetRotation(boss.Placement().Face(target.Position).Rotation));
    }

    private void MarkEnumerations()
    {
        foreach (var role in state.Enumerations) Living(role)?.AttachLockonVfx(LockonId.Enumeration);
    }

    private void ResolveEnumerations()
    {
        foreach (var member in state.Enumerations.Select(Living).OfType<SimCharacter>())
            bruteJustice?.CastFromHelper(TeaActions.Enumeration, member.Position, at: new Placement(member.Position, 0f));
    }
}
