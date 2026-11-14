using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Ucob.P4Adds.UcobP4AddsConstants;
using Actions = AnoMech.Scenarios.Ucob.UcobActions;
using ActionId = AnoMech.Scenarios.Ucob.UcobConstants.ActionId;
using ActionTimelineId = AnoMech.Scenarios.Ucob.UcobConstants.ActionTimelineId;
using BNpcBaseId = AnoMech.Scenarios.Ucob.UcobConstants.BNpcBaseId;
using BNpcNameId = AnoMech.Scenarios.Ucob.UcobConstants.BNpcNameId;
using EObjId = AnoMech.Scenarios.Ucob.UcobConstants.EObjId;
using LockonId = AnoMech.Scenarios.Ucob.UcobConstants.LockonId;
using StatusId = AnoMech.Scenarios.Ucob.UcobConstants.StatusId;
using TetherId = AnoMech.Scenarios.Ucob.UcobConstants.TetherId;

namespace AnoMech.Scenarios.Ucob.P4Adds;

// UCOB P4 "adds": Twintania and Nael deus Darnus guard a charging Bahamut Prime. Covers the
// first ~135s of the phase (two Hatch sets, four Twisters, two Nael triple quotes, both
// tankbuster pairs, Megaflares and Liquid Hells). The adds die on HP somewhere past two minutes
// in, so the scenario runs to the second Megaflare, which slower kills see.
//
// Tank flow: each tank keeps one add, the buster pair splits across them, and both adds swap
// tanks right after it. With two bot tanks the swaps are scripted; with a human tank they
// follow that tank's Provoke.
public sealed class UcobP4AddsScenario : IScenario
{
    public string Name => "Adds";
    public IPhase Phase => UcobZone.P4;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UcobP4AddsAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly UcobP4AddsSettingsWindow settingsWindow = new();

    private const float AutoAttackInterval = 3.05f;
    // UNVERIFIED: how long a fresh twister waits before it can be triggered.
    private const float TwisterArmDelay = 0.5f;
    // Twisters go inactive 5.7s after spawning, puddles after ~11.7s.
    private const float TwisterLifetime = 5.7f;
    private const float LiquidHellPuddleLifetime = 11.7f;
    // UNVERIFIED: Burns starts landing 0.8-2s after a puddle spawns.
    private const float LiquidHellPuddleArmDelay = 2f;
    private const float BurnsDeathDelay = 3f;
    private const float BurnsDuration = 15f;
    private const float TwisterBurstLinger = 1.5f;
    private const float OrbLingerAfterPop = 1.5f;
    private const int ThermionicBeamStack = 6;

    private sealed class HatchOrb(SimEnemy orb, SimCharacter target)
    {
        public SimEnemy Orb { get; } = orb;
        public SimCharacter Target { get; } = target;
        public bool Released { get; set; }
        public float? DespawnAt { get; set; }
    }

    private sealed class Twister(Vector3 position, SimEventObject? visual, CircleObstacle keepOut, float armAt, float expireAt)
    {
        public Vector3 Position { get; } = position;
        public SimEventObject? Visual { get; } = visual;
        public CircleObstacle KeepOut { get; } = keepOut;
        public float ArmAt { get; } = armAt;
        public float ExpireAt { get; set; } = expireAt;
        public bool Burst { get; set; }
    }

    private SimWorld world = null!;
    private SimParty party = null!;
    private UcobP4AddsState state = null!;

    private SimEnemy? bahamut;
    private SimCharacter? liquidHellTarget;
    private readonly List<HatchOrb> orbs = new();
    private readonly List<Twister> twisters = new();
    private readonly List<(Vector3 Center, float ArmAt, float ExpireAt)> puddles = new();
    private readonly List<(SimCharacter Member, float DiesAt)> burning = new();
    private bool engaged;
    private bool bossesHeld;
    private float clock;
    private float twintaniaSwing;
    private float naelSwing;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UcobP4AddsState(world.Rng, party, settingsWindow.Overrides);
        bahamut = null;
        liquidHellTarget = null;
        orbs.Clear();
        twisters.Clear();
        puddles.Clear();
        burning.Clear();
        engaged = false;
        bossesHeld = false;
        clock = 0f;
        twintaniaSwing = 0f;
        naelSwing = 0f;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UcobP4AddsState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.5f, SpawnNeurolinks);
        world.Events.Add(0.5f, SpawnBahamut);
        world.Events.Add(1.0f, () => bahamut?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(2.49f, () => bahamut?.SetModelState(BahamutChargingModelState));
        world.Events.Add(2.56f, () => bahamut?.PlayActionTimeline(ActionTimelineId.BahamutDivineJudgmentPose));
        world.Events.Add(4.5f, SpawnTwintaniaAndNael);
        world.Events.Add(5.0f, () => state.Twintania?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        world.Events.Add(5.0f, () => state.Nael?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(7.0f, TetherAddsToBahamut);
        world.Events.Add(8.0f, () => NaelSays(Text.StandGuard));
        world.Events.Add(11.56f, BahamutsFavor);
        world.Events.Add(13.1f, Engage);

        world.Events.Add(21.14f, PlummetAndClaw);
        world.Events.Add(24.39f, () => LiquidHell(pickTarget: true));
        world.Events.Add(25.54f, () => LiquidHell(pickTarget: false));
        world.Events.Add(26.70f, () => LiquidHell(pickTarget: false));
        world.Events.Add(27.90f, () => LiquidHell(pickTarget: false));
        world.Events.Add(29.08f, () => LiquidHell(pickTarget: false));

        world.Events.Add(30.18f, () => MarkHatchTargets(0));
        world.Events.Add(30.28f, () => state.Twintania?.Cast(ActionId.Generate));
        world.Events.Add(30.50f, () => SpawnOviforms(0));
        world.Events.Add(34.55f, ReleaseOviforms);
        world.Events.Add(35.43f, CastTwister);
        world.Events.Add(36.71f, () => SnapshotTwisters(0));
        world.Events.Add(37.76f, () => SpawnTwisters(0));

        world.Events.Add(40.21f, () => BeginQuote(0));
        world.Events.Add(45.31f, () => ResolveNaelMove(0, 0));
        world.Events.Add(48.41f, () => ResolveNaelMove(0, 1));
        world.Events.Add(51.51f, () => ResolveNaelMove(0, 2));
        world.Events.Add(51.80f, ReleaseBosses);

        world.Events.Add(52.58f, CastTwister);
        world.Events.Add(53.86f, () => SnapshotTwisters(1));
        world.Events.Add(54.93f, () => SpawnTwisters(1));
        world.Events.Add(60.55f, () => state.Nael?.Cast(Actions.Megaflare));
        world.Events.Add(69.69f, () => state.Nael?.Cast(Actions.Ravensbeak, party.Get(state.NaelTank)));
        world.Events.Add(69.73f, () => state.Twintania?.Cast(Actions.DeathSentence, party.Get(state.TwintaniaTank)));
        world.Events.Add(75.60f, () => BotTanksGiveNaelTo(PartyRole.MainTank));
        world.Events.Add(76.50f, () => BotTanksGiveTwintaniaTo(PartyRole.OffTank));
        world.Events.Add(77.84f, PlummetAndClaw);

        world.Events.Add(87.03f, () => LiquidHell(pickTarget: true));
        world.Events.Add(88.19f, () => LiquidHell(pickTarget: false));
        world.Events.Add(89.35f, () => LiquidHell(pickTarget: false));
        world.Events.Add(90.50f, () => LiquidHell(pickTarget: false));
        world.Events.Add(91.66f, () => LiquidHell(pickTarget: false));

        world.Events.Add(92.78f, () => MarkHatchTargets(1));
        world.Events.Add(92.87f, () => state.Twintania?.Cast(ActionId.Generate));
        world.Events.Add(93.11f, () => SpawnOviforms(1));
        world.Events.Add(97.15f, ReleaseOviforms);
        world.Events.Add(98.06f, CastTwister);
        world.Events.Add(99.34f, () => SnapshotTwisters(2));
        world.Events.Add(100.40f, () => SpawnTwisters(2));

        world.Events.Add(102.78f, () => BeginQuote(1));
        world.Events.Add(107.88f, () => ResolveNaelMove(1, 0));
        world.Events.Add(110.98f, () => ResolveNaelMove(1, 1));
        world.Events.Add(114.08f, () => ResolveNaelMove(1, 2));
        world.Events.Add(114.40f, ReleaseBosses);

        world.Events.Add(115.20f, CastTwister);
        world.Events.Add(116.48f, () => SnapshotTwisters(3));
        world.Events.Add(117.55f, () => SpawnTwisters(3));
        world.Events.Add(125.52f, () => state.Twintania?.Cast(Actions.DeathSentence, party.Get(state.TwintaniaTank)));
        world.Events.Add(126.24f, () => state.Nael?.Cast(Actions.Ravensbeak, party.Get(state.NaelTank)));
        world.Events.Add(130.00f, () => BotTanksGiveTwintaniaTo(PartyRole.MainTank));
        world.Events.Add(130.90f, () => BotTanksGiveNaelTo(PartyRole.OffTank));
        world.Events.Add(137.33f, () => state.Nael?.Cast(Actions.Megaflare));
        world.Events.Add(146.0f, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        clock = elapsed;
        SwingAutoAttacks(delta);
        BurnPuddleStandersInLiquidHell();
        UpdateNeurolinkStatus();
        UpdateOrbs();
        UpdateTwisters();
    }

    // With a human tank, swaps follow Provoke instead of the script: the add they provoke comes
    // to them and the other add goes to the other tank.
    public void OnPlayerActed(PlayerActed acted)
    {
        if (!engaged || acted.ActionId != ActionId.Provoke || acted.Actor is not ISimPartyMember { Role: var role } || !role.IsTank()) return;
        var coTank = role == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;

        if (acted.Target == state.Nael && state.NaelTank != role)
            AssignTanks(coTank, role);
        else if (acted.Target == state.Twintania && state.TwintaniaTank != role)
            AssignTanks(role, coTank);
    }

    private void SpawnNeurolinks()
    {
        foreach (var link in Geometry.Neurolinks)
            SpawnActiveEObj(EObjId.Neurolink, link);
    }

    private void SpawnBahamut()
    {
        bahamut = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BahamutPrime,
            NameId: BNpcNameId.BahamutPrime,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(Geometry.BahamutSpawn, 0f)));
    }

    private void SpawnTwintaniaAndNael()
    {
        state.Twintania = SpawnAdd(BNpcBaseId.Twintania, BNpcNameId.Twintania, Geometry.TwintaniaSpawn);
        state.Nael = SpawnAdd(BNpcBaseId.NaelDeusDarnus, BNpcNameId.NaelDeusDarnus, Geometry.NaelSpawn);
    }

    private SimEnemy? SpawnAdd(uint baseId, uint nameId, Vector3 position) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Manual,
            Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(position, 0f)));

    private void TetherAddsToBahamut()
    {
        if (bahamut == null) return;
        world.Tether(state.Twintania, bahamut, TetherId.GuardingBahamut, duration: 4.6f);
        world.Tether(state.Nael, bahamut, TetherId.GuardingBahamut, duration: 4.6f);
    }

    private void BahamutsFavor()
    {
        bahamut?.Cast(ActionId.BahamutsFavor);
        state.Twintania?.AddStatus(StatusId.DamageUp);
        state.Nael?.AddStatus(StatusId.DamageUp);
    }

    private void Engage()
    {
        foreach (var boss in new[] { state.Twintania, state.Nael })
        {
            boss?.SetTargetable(true);
            boss?.SetVisibleInEnemyList(true);
        }
        engaged = true;
        FollowHolders();
    }

    private void GiveNaelTo(PartyRole role) => AssignTanks(state.TwintaniaTank, role);

    private void GiveTwintaniaTo(PartyRole role) => AssignTanks(role, state.NaelTank);

    private void AssignTanks(PartyRole twintaniaTank, PartyRole naelTank)
    {
        if (twintaniaTank != state.TwintaniaTank) AnnounceSwap(twintaniaTank, "Twintania");
        if (naelTank != state.NaelTank) AnnounceSwap(naelTank, Text.NaelName);
        state.AssignTanks(twintaniaTank, naelTank);
        if (!bossesHeld) FollowHolders();
    }

    private void BotTanksGiveNaelTo(PartyRole role)
    {
        if (!AnyHumanTank) GiveNaelTo(role);
    }

    private void BotTanksGiveTwintaniaTo(PartyRole role)
    {
        if (!AnyHumanTank) GiveTwintaniaTo(role);
    }

    private bool AnyHumanTank => IsHuman(PartyRole.MainTank) || IsHuman(PartyRole.OffTank);

    private bool IsHuman(PartyRole role) => party.Get(role) is { } member && !party.IsBotDriven(member);

    private void FollowHolders()
    {
        state.Twintania?.SetTarget(party.Get(state.TwintaniaTank));
        state.Nael?.SetTarget(party.Get(state.NaelTank));
    }

    // Nael plants for her quote; releasing keeps a tank running out for Chariot from dragging
    // the AOE along behind them.
    private void BeginQuote(int index)
    {
        bossesHeld = true;
        state.Twintania?.Follow(null);
        state.Nael?.Follow(null);
        var quote = state.Quotes[index];
        NaelSays(state.SimplifiedQuotes ? quote.Simplified : quote.Text);
    }

    private void ReleaseBosses()
    {
        bossesHeld = false;
        FollowHolders();
    }

    private void AnnounceSwap(PartyRole role, string boss)
    {
        if (!AnyHumanTank) return;
        var who = role == PartyRole.MainTank ? "the main tank" : "the off tank";
        world.Announce($"Tank swap: {boss} is now on {who}.");
    }

    private void NaelSays(string text) => world.Announce(text, Text.NaelName);

    private void SwingAutoAttacks(float delta)
    {
        if (!engaged) return;
        twintaniaSwing = Swing(state.Twintania, state.TwintaniaTank, Actions.TwintaniaAttack, twintaniaSwing - delta);
        naelSwing = Swing(state.Nael, state.NaelTank, Actions.NaelAttack, naelSwing - delta);
    }

    // A cast holds the next swing back a full interval, so a buster isn't chased by an auto
    // before the tanks can swap.
    private float Swing(SimEnemy? boss, PartyRole holder, EnemyAction attack, float untilNext)
    {
        if (boss is not { IsActive: true }) return 0f;
        if (boss.AnimationLock) return AutoAttackInterval;
        if (untilNext > 0f) return untilNext;
        if (party.Get(holder) is not { } tank || !tank.IsAlive()) return 0f;
        boss.Cast(attack, tank);
        return AutoAttackInterval;
    }

    private void PlummetAndClaw()
    {
        if (party.Get(state.TwintaniaTank) is { } twinTank && twinTank.IsAlive())
            state.Twintania?.Cast(Actions.Plummet, twinTank);
        if (party.Get(state.NaelTank) is { } naelTank && naelTank.IsAlive())
            state.Nael?.Cast(Actions.BahamutsClaw, naelTank);
    }

    // The farthest player from Twintania at the first drop gets all five puddles, each dropped
    // where they stand at that instant.
    private void LiquidHell(bool pickTarget)
    {
        if (state.Twintania is not { } twin) return;
        if (pickTarget) liquidHellTarget = party.Find.Farest(twin.Position);
        if (liquidHellTarget is not { } target || !target.IsAlive()) return;

        var at = target.Position;
        twin.Cast(Actions.LiquidHell, at);
        SpawnActiveEObj(EObjId.LiquidHellPuddle, at, LiquidHellPuddleLifetime);
        puddles.Add((at, clock + LiquidHellPuddleArmDelay, clock + LiquidHellPuddleLifetime));
    }

    private void BurnPuddleStandersInLiquidHell()
    {
        puddles.RemoveAll(p => clock >= p.ExpireAt);
        foreach (var puddle in puddles)
        {
            if (clock < puddle.ArmAt) continue;
            foreach (var member in party.Find.InsideCircle(puddle.Center, Geometry.LiquidHellPuddleRadius))
            {
                if (burning.Exists(b => b.Member == member)) continue;
                member.AddStatus(StatusId.Burns, BurnsDuration);
                burning.Add((member, clock + BurnsDeathDelay));
            }
        }

        for (var i = burning.Count - 1; i >= 0; i--)
        {
            if (clock < burning[i].DiesAt) continue;
            burning[i].Member.DieOfStatus(StatusId.Burns,
                $"stood in a Liquid Hell puddle; puddles turn deadly {LiquidHellPuddleArmDelay:0.#}s after they drop, so keep moving away from them");
            burning.RemoveAt(i);
        }
    }

    private void MarkHatchTargets(int set)
    {
        foreach (var role in state.Hatches[set].Targets)
            if (party.Get(role) is { } member && member.IsAlive())
                member.AttachLockonVfx(LockonId.Hatch);
    }

    private void SpawnOviforms(int set)
    {
        if (state.Twintania is not { } twin) return;
        foreach (var role in state.Hatches[set].Targets)
        {
            if (party.Get(role) is not { } target || !target.IsAlive()) continue;
            var orb = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Oviform,
                NameId: BNpcNameId.Oviform,
                Level: UcobConstants.Level,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Placement: new Placement(twin.Position, twin.Rotation)));
            if (orb == null) continue;
            orb.SetTarget(target, follow: false);
            orbs.Add(new HatchOrb(orb, target));
        }
    }

    private void ReleaseOviforms()
    {
        foreach (var orb in orbs.Where(o => !o.Released))
        {
            orb.Released = true;
            orb.Orb.Follow(orb.Target, Geometry.OviformSpeed);
        }
    }

    private void UpdateOrbs()
    {
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var orb = orbs[i];
            if (orb.DespawnAt is { } at)
            {
                if (clock < at) continue;
                orb.Orb.Despawn();
                orbs.RemoveAt(i);
                continue;
            }
            if (!orb.Released) continue;
            if (!orb.Target.IsAlive())
            {
                orb.Orb.Despawn();
                orbs.RemoveAt(i);
                continue;
            }
            if (FlatDistance(orb.Orb.Position, orb.Target.Position) > Geometry.HatchPopDistance) continue;

            PopOviform(orb);
            orb.DespawnAt = clock + OrbLingerAfterPop;
        }
    }

    private void UpdateNeurolinkStatus()
    {
        foreach (var member in party.ActiveMembers())
        {
            var inside = Geometry.Neurolinks.Any(link => FlatDistance(link, member.Position) <= Geometry.NeurolinkRadius);
            var has = member.HasStatus(StatusId.Neurolink);
            if (inside && !has) member.AddStatus(StatusId.Neurolink);
            else if (!inside && has) member.RemoveStatus(StatusId.Neurolink);
        }
    }

    private void PopOviform(HatchOrb orb)
    {
        orb.Orb.Follow(null);
        orb.Orb.Cast(orb.Target.HasStatus(StatusId.Neurolink) ? Actions.Hatch : Actions.DeepHatch, orb.Target);
    }

    private void CastTwister() => state.Twintania?.Cast(ActionId.Twister);

    private void RemoveTwister(Twister twister)
    {
        state.LiveTwisters.RemoveAll(t => FlatDistance(t, twister.Position) < 0.1f);
        twister.Visual?.Despawn();
        world.Obstacles.Remove(twister.KeepOut);
    }

    private SimEventObject? SpawnActiveEObj(uint eObjId, Vector3 position, float lifetime = 0f) =>
        world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = eObjId,
            Placement = new Placement(position, 0f),
            TimelineState = EObjActiveState,
            Lifetime = lifetime,
        });

    // Retail drops four twisters under random players, placed where they stood about 3/4 of
    // the way through Twister's cast.
    private void SnapshotTwisters(int set)
    {
        var snapshot = state.TwisterSnapshots[set];
        snapshot.Clear();
        foreach (var role in state.TwisterTargets[set])
            if (party.Get(role) is { } member && member.IsAlive())
                snapshot.Add((role, member.Position));
        state.LiveTwisters.AddRange(snapshot.Select(s => s.Position));
    }

    private void SpawnTwisters(int set)
    {
        foreach (var (_, position) in state.TwisterSnapshots[set])
        {
            var visual = SpawnActiveEObj(EObjId.Twister, position);
            var keepOut = world.Obstacles.Add(new CircleObstacle(
                new Vector2(position.X, position.Z), Geometry.TwisterKeepOutRadius));
            twisters.Add(new Twister(position, visual, keepOut, clock + TwisterArmDelay, clock + TwisterLifetime));
        }
    }

    private void UpdateTwisters()
    {
        for (var i = twisters.Count - 1; i >= 0; i--)
        {
            var twister = twisters[i];
            if (clock >= twister.ExpireAt)
            {
                RemoveTwister(twister);
                twisters.RemoveAt(i);
                continue;
            }
            if (twister.Burst || clock < twister.ArmAt) continue;

            var victim = party.Find.Closest(twister.Position);
            if (victim == null || FlatDistance(victim.Position, twister.Position) > Geometry.TwisterTriggerRadius)
                continue;

            twister.Burst = true;
            twister.ExpireAt = clock + TwisterBurstLinger;
            state.Twintania?.CastFromHelper(Actions.TwisterBurst, victim, at: new Placement(twister.Position, 0f));
        }
    }

    private void ResolveNaelMove(int quote, int step)
    {
        if (state.Nael is not { } nael) return;
        switch (state.Quotes[quote].Moves[step])
        {
            case NaelMove.Chariot:
                nael.Cast(Actions.IronChariot);
                break;
            case NaelMove.Dynamo:
                nael.Cast(Actions.LunarDynamo);
                break;
            case NaelMove.Beam:
                if (AliveOrAnyone(state.BeamTargets[quote]) is { } stackTarget)
                    nael.Cast(Actions.ThermionicBeam(Math.Min(ThermionicBeamStack, party.ActiveMembers().Count())), stackTarget);
                break;
            case NaelMove.Dive:
                if (AliveOrAnyone(state.DiveTargets[quote]) is { } diveTarget)
                    nael.Cast(Actions.RavenDive, diveTarget);
                break;
        }
    }

    private SimCharacter? AliveOrAnyone(PartyRole preferred)
    {
        if (party.Get(preferred) is { } member && member.IsAlive()) return member;
        return party.Find.RandomMember(world.Rng);
    }

    private void DespawnAll()
    {
        engaged = false;
        puddles.Clear();
        foreach (var orb in orbs) orb.Orb.Despawn();
        orbs.Clear();
        foreach (var twister in twisters) RemoveTwister(twister);
        state.LiveTwisters.Clear();
        twisters.Clear();
        state.Twintania?.Despawn();
        state.Nael?.Despawn();
        bahamut?.Despawn();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
