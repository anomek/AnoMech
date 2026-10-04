using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Top.TopConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

public sealed class TopP1ProgramLoopScenario : IMultiplayerReplayable
{
    public string Name => "Program Loop";
    public IPhase Phase => TopZone.P1;
    public bool SupportsMultiplayer => true;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public bool HasLocalSettings => true;
    public void DrawLocalSettings(PartyRole? localRole) => TopPartyList.Draw(localRole);
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP1ProgramLoopSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP1ProgramLoopAi(automarkers: false), new TopP1ProgramLoopAi(automarkers: true)];

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (6545, "mon_sp/gimmick/z3oe_boss_gimmick03"),
        (6547, "mon_sp/gimmick/z3oe_boss_gimmick04"),
        (10713, "mon_sp/gimmick/z3oz_boss_gimmick01"),
    ];

    private static readonly ushort[] InLineStatuses = [StatusId.FirstInLine, StatusId.SecondInLine, StatusId.ThirdInLine, StatusId.FourthInLine];

    // Real hits as a share of the full max HP, with the party's usual mitigation in them.
    private static readonly (float Other, float Tank) TowerHit = (0.42f, 0.18f);
    private static readonly (float Other, float Tank) BlasterHit = (0.41f, 0.17f);
    private static readonly (float Other, float Tank) ObliterationHit = (0.79f, 0.34f);

    private static readonly Vector3 BeetleSpot = new(0.02f, 0f, 0.27f);
    private const float BeetleWalkSpeed = 4.2f;
    private const ushort TowerEmpty = 8;
    private const ushort TowerOccupied = 16;
    // Radians per second.
    private const float BeetleTurnSpeed = 8f;

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP1ProgramLoopState state = null!;
    private DamageSolver damage = null!;
    private TopP1ProgramLoopHpPenalty hpPenalty = null!;

    public TopP1ProgramLoopState? LastState { get; private set; }

    private SimEnemy? beetle;
    private TopHelpers? helpers;
    private readonly List<SimTether> tethers = [];
    private readonly List<SimEventObject?>[] towerObjects = [[], [], [], []];
    private readonly List<(SimEventObject Disc, Vector3 At)> towerDiscs = [];
    private readonly HashSet<SimCharacter> loopersCleansed = [];
    private List<SimCharacter> blasterHits = [];
    private List<SimCharacter> obliterationHits = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP1ProgramLoopState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        tethers.Clear();
        foreach (var set in towerObjects) set.Clear();
        towerDiscs.Clear();
        loopersCleansed.Clear();
        blasterHits = [];
        obliterationHits = [];
        beetle = null;
        helpers = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP1ProgramLoopState>)AiStrats[idx]).Run(state, world);
        damage = new DamageSolver(party);
        hpPenalty = new TopP1ProgramLoopHpPenalty(StatusId.HPPenalty, 0.01f);

        world.Events.Add(0.1f, SpawnBeetle);
        world.Events.Add(0.1f, () => helpers = new TopHelpers(world, BNpcNameId.OmegaBeetle, 8, "TopP1ProgramLoop"));
        world.Events.Add(0.20f, () => world.Map.BattleTalk(BNpcNameId.OmegaBeetle, BattleTalkId.InitiatingDirectAnalysis, 6000));
        world.Events.Add(0.42f, () => beetle?.MoveTo(BeetleSpot, BeetleWalkSpeed));
        world.Events.Add(3.20f, () => AutoAttack(0f));
        world.Events.Add(6.22f, () => AutoAttack(0.13f));
        world.Events.Add(9.25f, () => AutoAttack(0.18f));
        world.Events.Add(10.32f, () => BeetleCast(ActionId.ProgramLoop, castSeconds: 3.7f, animationLock: 2.1f, fireDelay: 0.27f));
        world.Events.Add(15.05f, ApplyLoopDebuffs);
        world.Events.Add(16.34f, () => world.Map.AddEffect(0x00020001, 0x09));
        world.Events.Add(17.32f, () => AutoAttack(0.24f));
        world.Events.Add(18.63f, () => SpawnTowers(0));
        world.Events.Add(20.36f, SpawnTethers);
        world.Events.Add(20.44f, () => BeetleCast(ActionId.Blaster, castSeconds: 7.6f, animationLock: 0.25f, fireDelay: 0.29f));
        world.Events.Add(26.47f, HealParty);
        world.Events.Add(27.66f, () => SpawnTowers(1));
        world.Events.Add(28.47f, () => ResolveTowers(0));
        world.Events.Add(28.55f, () => BeetleCast(ActionId.BlasterRepeat, castSeconds: 0f, animationLock: 9f));
        world.Events.Add(28.56f, ResolveBlasters);
        world.Events.Add(29.12f, ApplyObliterationDamageDown);
        world.Events.Add(29.18f, ApplyHpPenalty);
        world.Events.Add(31.05f, () => ExpireLooper(1));
        world.Events.Add(31.35f, () => WalkLostMembersIntoWall(1));
        world.Events.Add(35.47f, HealParty);
        world.Events.Add(36.68f, () => SpawnTowers(2));
        world.Events.Add(37.47f, () => ResolveTowers(1));
        world.Events.Add(37.51f, () => BeetleCast(ActionId.BlasterRepeat, castSeconds: 0f, animationLock: 9f));
        world.Events.Add(37.52f, ResolveBlasters);
        world.Events.Add(38.12f, ApplyObliterationDamageDown);
        world.Events.Add(38.16f, ApplyHpPenalty);
        world.Events.Add(40.05f, () => ExpireLooper(2));
        world.Events.Add(40.35f, () => WalkLostMembersIntoWall(2));
        world.Events.Add(44.48f, HealParty);
        world.Events.Add(45.67f, () => SpawnTowers(3));
        world.Events.Add(46.48f, () => ResolveTowers(2));
        world.Events.Add(46.48f, () => BeetleCast(ActionId.BlasterRepeat, castSeconds: 0f, animationLock: 9f));
        world.Events.Add(46.49f, ResolveBlasters);
        world.Events.Add(47.12f, ApplyHpPenalty);
        world.Events.Add(47.13f, ApplyObliterationDamageDown);
        world.Events.Add(49.05f, () => ExpireLooper(3));
        world.Events.Add(49.35f, () => WalkLostMembersIntoWall(3));
        world.Events.Add(53.48f, HealParty);
        world.Events.Add(55.44f, () => BeetleCast(ActionId.BlasterLast, castSeconds: 0f, animationLock: 3.1f));
        world.Events.Add(55.44f, ResolveBlasters);
        world.Events.Add(55.45f, DespawnTethers);
        world.Events.Add(55.48f, () => ResolveTowers(3));
        world.Events.Add(56.09f, ApplyHpPenalty);
        world.Events.Add(56.13f, ApplyObliterationDamageDown);
        world.Events.Add(58.05f, () => ExpireLooper(4));
        world.Events.Add(58.35f, () => WalkLostMembersIntoWall(4));
    }

    public void Tick(float delta, float elapsed)
    {
        hpPenalty.Tick(party);
        PassTethersFromTheDead();
        TurnBeetleToTank(delta);
        LightTowers();
    }

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP1ProgramLoop");
        instanceWorld.SetPartyListOrder(TopPartyList.Order(instanceWorld.Party.PlayerRole));
    }

    // It waits north of the middle after every reset and targets its main tank from the pull.
    private void SpawnBeetle()
    {
        beetle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaBeetle,
            NameId: BNpcNameId.OmegaBeetle,
            Level: Level,
            Targetable: true,
            Placement: new Placement(new Vector3(0f, 0f, -10f), 0f)));
        if (beetle is { IsActive: true } && party.Get(PartyRole.MainTank) is { } tank) beetle.SetTarget(tank, follow: false);
    }

    // In the middle, the beetle turns to its main tank whenever no cast or action holds it.
    private void TurnBeetleToTank(float delta)
    {
        if (beetle == null || Vector2.DistanceSquared(new Vector2(beetle.Position.X, beetle.Position.Z), new Vector2(BeetleSpot.X, BeetleSpot.Z)) > 0.01f) return;
        TopBoss.TurnToMainTank(party, beetle, BeetleTurnSpeed, delta);
    }

    // The beetle's own actions name itself as their target.
    private void BeetleCast(uint actionId, float castSeconds, float animationLock, float? fireDelay = null)
        => beetle?.Cast(actionId, castSeconds: castSeconds, targetId: beetle.GameObjectId, animationLock: animationLock, fireDelay: fireDelay);

    // The healers have everyone back to full 2s before a tower goes off, short of what the HP
    // Penalty still caps.
    private void HealParty()
    {
        foreach (var member in party.ActiveMembers().Where(member => member.IsAlive()))
            member.SetHealth(member.MaxHealth);
    }

    // The beetle's auto-attack on its main target, which it only reaches from its hitbox's edge
    // plus melee range. It swings facing the tank, with any of its three swings.
    private void AutoAttack(float share)
    {
        if (beetle is not { IsActive: true, IsCasting: false, AnimationLock: false } || party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return;
        if (Vector3.Distance(beetle.Position, tank.Position) > BeetleAutoAttackReach) return;
        beetle.SetTarget(tank, follow: false);
        beetle.Face(tank.Position);
        beetle.Cast(ActionId.AutoAttackP1, castSeconds: 0f, targetId: tank.GameObjectId, animationVariation: (byte)state.Rng.NextInt(3), animationLock: 0.1f);
        world.Events.Add(0.62f, () =>
        {
            if (tank.IsAlive()) ApplyHpDamage(tank, (uint)(share * hpPenalty.FullMaxHealth(tank)), ActionId.AutoAttackP1);
        });
    }

    private const float BeetleAutoAttackReach = 15f;

    // The fight gives every seat an In Line number and a Looper that runs out after their set.
    // The real status params are 0; left at 1, since a zero-stack status never reaches peers.
    private void ApplyLoopDebuffs()
    {
        for (var number = 1; number <= 4; number++)
            foreach (var role in state.WithNumber(number))
            {
                if (party.Get(role) is not { } member) continue;
                member.AddStatus(InLineStatuses[number - 1]);
                member.AddStatus(StatusId.Looper, 7f + 9f * number);
            }
    }

    private void SpawnTowers(int set)
    {
        var towerSet = state.Towers[set];
        foreach (var cardinal in towerSet.Cardinals)
        {
            var placement = new Placement(towerSet.Position(cardinal), 0f);
            var layoutId = towerSet.LayoutId(cardinal);
            var disc = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.TowerSolo, Placement = placement, LayoutId = layoutId, EventId = EObjId.DirectorEventId, TimelineState = TowerEmpty });
            if (disc != null) towerDiscs.Add((disc, placement.Position));
            towerObjects[set].Add(disc);
            towerObjects[set].Add(world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.TowerTimer, Placement = placement, LayoutId = layoutId, EventId = EObjId.DirectorEventId, TimelineState = 2 }));
        }
    }

    // Only a Looper holder lights a tower, as only a Looper holder takes its hit.
    private void LightTowers()
    {
        foreach (var (disc, at) in towerDiscs)
        {
            if (!disc.IsAlive) continue;
            var state = party.Find.InsideCircle(at, Geometry.TowerRadius).Any(member => member.HasStatus(StatusId.Looper)) ? TowerOccupied : TowerEmpty;
            if (disc.CurrentState != state) disc.SetState(state);
        }
    }

    // A tower's hit lands on every Looper holder inside it; anyone inside without one is listed as a miss.
    private void ResolveTowers(int set)
    {
        towerObjects[set].ForEach(tower => tower?.Despawn());
        obliterationHits = [];
        var towerSet = state.Towers[set];
        foreach (var cardinal in towerSet.Cardinals)
        {
            var tower = new Placement(towerSet.Position(cardinal), 0f);
            var inTower = party.Find.InsideActionAoe(ActionId.StorageViolationSolo, tower);
            var soakers = inTower.Where(member => member.HasStatus(StatusId.Looper)).ToList();
            if (soakers.Count == 0)
            {
                HelperEffect(tower, ActionId.StorageViolationObliteration, null);
                obliterationHits.AddRange(ResolveRuinHit(TopPositioned.From(tower), ActionId.StorageViolationObliteration, ObliterationHit));
                continue;
            }
            HelperEffect(tower, ActionId.StorageViolationSolo, soakers[0]);
            foreach (var soaker in soakers)
            {
                soaker.RemoveStatus(StatusId.Looper);
                foreach (var inLine in InLineStatuses) soaker.RemoveStatus(inLine);
                loopersCleansed.Add(soaker);
            }
            var missed = inTower.Except(soakers).ToArray();
            foreach (var member in missed)
                member.Proxy?.ShowMissFlyText(ActionLookup.Name(ActionId.StorageViolationSolo));
            ResolveRuinHit(TopPositioned.From(tower), ActionId.StorageViolationSolo, TowerHit, missed);
        }
    }

    // The real headers: the helper at the tower's centre is its own animation target.
    private void HelperEffect(Placement at, uint actionId, SimCharacter? target)
    {
        if (helpers?.Next(at) is not { } helper) return;
        helper.NativeActionEffect(actionId, 1.1f, (ushort)actionId, 0, ActionType.Action, 0, rotation: at.Rotation, position: at.Position,
            animationTargetId: helper.GameObjectId, actionTargetId: target?.GameObjectId);
    }

    private void ApplyObliterationDamageDown()
        => obliterationHits.Where(hit => hit.IsAlive()).Distinct().ToList().ForEach(hit => hit.AddStatus(StatusId.DamageDown, 180f));

    private void SpawnTethers()
    {
        foreach (var role in state.FirstTetherHolders)
            tethers.Add(world.Tether(End.Passable(party.Get(role)), beetle, TetherId.PassableTether));
    }

    // A holder's death passes its tether at once to a random living player holding none; with
    // nobody left, it drops.
    private void PassTethersFromTheDead()
    {
        for (var i = tethers.Count - 1; i >= 0; i--)
        {
            if (tethers[i].A.IsAlive()) continue;
            tethers[i].Despawn();
            var free = party.ActiveMembers().Where(member => tethers.All(tether => tether.A != member)).ToList();
            if (free.Count == 0)
            {
                tethers.RemoveAt(i);
                continue;
            }
            tethers[i] = world.Tether(End.Passable(free[state.Rng.NextInt(free.Count)]), beetle, TetherId.PassableTether);
        }
    }

    // Everyone a tether's explosion reaches, holder or not, also loses max HP.
    private void ResolveBlasters()
    {
        PassTethersFromTheDead();
        blasterHits = [];
        foreach (var holder in tethers.Select(tether => tether.A).OfType<SimCharacter>().ToList())
        {
            helpers?.Next(beetle?.Placement() ?? default)?.Cast(ActionId.BlasterAoe, castSeconds: 0f, targetId: holder.GameObjectId, animationLock: 1.1f);
            blasterHits.AddRange(ResolveRuinHit(holder, ActionId.BlasterAoe, BlasterHit));
            foreach (var bystander in damage.Resolve(holder, ActionId.BlasterAoe, [DamageType.Lethal], [], excludeTargets: [holder], killTargets: false))
                KillBystander(bystander);
        }
    }

    // Anyone the Blaster catches but its holder dies: the real fight dooms them on a second Ruin, or
    // HP Penalty leaves them nothing for their next hit.
    private void KillBystander(SimCharacter bystander)
        => world.Events.Add(0.62f, () => bystander.Die(ActionId.BlasterAoe, "another player's tether"));

    private void ApplyHpPenalty() => blasterHits.Where(hit => hit.IsAlive()).Distinct().ToList().ForEach(hit => hit.AddStatus(StatusId.HPPenalty, 9f));

    private void DespawnTethers()
    {
        tethers.ForEach(tether => tether.Despawn());
        tethers.Clear();
    }

    // An unsoaked Looper runs out into Memory Loss: the holder loses control and, 0.3s later,
    // runs straight ahead until the wall kills them.
    private void ExpireLooper(int number)
    {
        foreach (var role in state.WithNumber(number))
        {
            if (party.Get(role) is not { } member || loopersCleansed.Contains(member) || !member.IsAlive()) continue;
            member.RemoveStatus(StatusId.Looper);
            foreach (var inLine in InLineStatuses) member.RemoveStatus(inLine);
            member.AddStatus(StatusId.DamageDown, 180f);
            member.AddStatus(StatusId.MemoryLoss, 15f);
            // A Memory Loss walk can outlast the timeline; this keeps the run open until it ends.
            world.Events.Add(15f, () => member.RemoveStatus(StatusId.MemoryLoss));
            world.Announce($"{role}'s Looper ran out: Memory Loss walks them into the wall.");
        }
    }

    private void WalkLostMembersIntoWall(int number)
    {
        foreach (var role in state.WithNumber(number))
        {
            if (party.Get(role) is not { } member || !member.IsAlive() || !member.HasStatus(StatusId.MemoryLoss)) continue;
            if (member is not ISimPartyMember walker) continue;
            walker.WalkInDirection(member.Rotation, DistanceToWall(member) + 1f, AiManager.RunSpeed);
        }
    }

    private static float DistanceToWall(SimCharacter member)
    {
        var position = new Vector2(member.Position.X, member.Position.Z);
        var heading = new Vector2(MathF.Sin(member.Rotation), MathF.Cos(member.Rotation));
        var along = Vector2.Dot(position, heading);
        var radius = Geometry.ArenaRadius;
        return -along + MathF.Sqrt(MathF.Max(0f, along * along - position.LengthSquared() + radius * radius));
    }

    // Every hit is real HP off the full pool and leaves Twice-come Ruin, which a second one turns
    // into Doom. Returns everyone the hit reached.
    private List<SimCharacter> ResolveRuinHit(IPositioned source, uint actionId, (float Other, float Tank) hitShare, SimCharacter[]? exclude = null)
    {
        var hits = damage.Resolve(source, actionId, [DamageType.Magic], [], excludeTargets: exclude).ToList();
        foreach (var hit in hits)
        {
            var share = hit is ISimPartyMember { Role: var role } && role.IsTank() ? hitShare.Tank : hitShare.Other;
            ApplyHpDamage(hit, (uint)(share * hpPenalty.FullMaxHealth(hit)), actionId);
            if (hit.IsAlive()) TopActions.ComeRuin.Land(hit, 2, 10.96f, actionId);
        }
        return hits;
    }

    private void ApplyHpDamage(SimCharacter target, uint amount, uint actionId)
    {
        if (target is not ISimPartyMember || target.Proxy is not { Exists: true } chara || !target.IsAlive()) return;
        var lethal = amount >= chara.Health;
        // Shown with the HP change; the real client shows the number at the hit, before the result lands.
        damage.ApplyDamage(target, amount / (float)chara.MaxHealth, actionId, $"{amount} damage on {chara.Health} HP", lethal);
        if (!lethal) chara.Health -= amount;
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP1ProgramLoopAiReplayStateMessage(
            s.InLine.List,
            s.Towers.SelectMany(set => new[] { set.CardinalA, set.CardinalB }).ToArray(),
            s.Towers.Select(set => set.Shift).ToArray(),
            s.FirstTetherHolders.ToArray())
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP1ProgramLoopAiReplayStateMessage msg) return null;
        if (TopP1ProgramLoopState.FromNetworkReplay(replayWorld.Party, msg.InLine, msg.TowerCardinals, msg.TowerShifts, msg.FirstTetherHolders) is not { } shadowState)
            return null;
        ((IScenarioAi<TopP1ProgramLoopState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
