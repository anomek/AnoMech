using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantScenario : IMultiplayerReplayable
{
    public string Name => "Light Rampant";
    public IPhase Phase => FruZone.P2;
    public bool SupportsMultiplayer => true;
    // The strat reads each player's Lightsteeped stacks.
    public bool MirrorsHostStatuses => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP2LightRampantAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly FruP2LightRampantSettingsWindow settingsWindow = new();

    private const float PuddleShowsAfterHammer = 0.95f;
    private const float PuddleArmsAfterShowing = 1.5f;
    private const float PuddleFadesAfterShowing = 9.7f;
    private const float PuddleLastsAfterShowing = 12.85f;
    // Curse chains stretched shorter than this snap. UNVERIFIED: retail's limit lies between 23 and 25.7.
    private const float CurseMinLength = 25f;

    // Where Diamond Dust leaves the helpers, which cast from there until something moves them.
    private static readonly Placement[] HelperRests =
    [
        new(new(14.14f, 0, 14.14f), -2.79f), new(new(14.14f, 0, 14.14f), -2.07f),
        new(new(14.14f, 0, 14.14f), 2.83f), new(new(14.14f, 0, 14.14f), -1.06f),
        new(new(-14.14f, 0, 14.14f), 1.57f), new(new(-14.14f, 0, 14.14f), -3.10f),
        new(new(-14.14f, 0, 14.14f), 3.09f), new(new(-14.14f, 0, 14.14f), 2.19f),
    ];
    // The tower each of the first six helpers takes for Bright Hunger.
    private static readonly int[] HelperTowers = [0, 3, 1, 5, 2, 4];
    // Each tower's MapEffect slot, counted clockwise from the north one.
    private static readonly byte[] TowerSlotOffsets = [5, 3, 1, 4, 0, 2];
    private const int HammerHelper = 6;

    private SimWorld world = null!;
    private FruP2LightRampantState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it.
    public FruP2LightRampantState? LastState { get; private set; }
    private SimEnemy? usurper;
    private readonly List<SimEnemy?>[] orbs = [[], []];
    private readonly List<SimTether> links = [];
    private readonly FruHolyPuddles puddles = new();
    private readonly List<MapEffectTower> towers = [];
    private MapEffectTower? centerTower;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP2LightRampantState(world.Rng, world.Party.PlayerRole, settingsWindow.Overrides);
        LastState = state;
        foreach (var wave in orbs) wave.Clear();
        links.Clear();
        puddles.Clear();
        towers.Clear();
        centerTower = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<FruP2LightRampantState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(0.6f, () => usurper?.SetModelState(4));
        world.Events.Add(0.6f, () => usurper?.AddStatusParam(TransfigurationStatus, UsurperLightTransformation));
        world.Events.Add(1.73f, () => usurper?.Cast(ActionId.Unnamed, Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(2.79f, () => usurper?.SetPosition(new Placement(Vector3.Zero, MathF.PI)));
        world.Events.Add(3f, () => usurper?.Cast(Actions.LightRampant));
        world.Events.Add(3f, () => usurper?.VoiceLine(VoiceLineId.LightRampant));
        world.Events.Add(8.75f, AssignDebuffs);
        world.Events.Add(8.75f, () => Chain(StatusId.ChainsOfEverlastingLight, TetherId.ChainsOfEverlastingLight, CursesAt - 8.75f));
        world.Events.Add(11f, () => usurper?.SetTargetable(false));
        world.Events.Add(11f, ShowTowers);
        world.Events.Add(11.1f, () => usurper?.PlayActionTimeline(TimelineId.UsurperHide));
        for (var wave = 0; wave < HammerWaves; wave++)
        {
            var w = wave;
            var showsAt = HammerAt(w) + PuddleShowsAfterHammer;
            world.Events.Add(HammerAt(w) - FruHolyPuddles.SnapshotBeforeHit, SnapshotHammerTargets);
            world.Events.Add(HammerAt(w), () => LuminousHammer(w));
            world.Events.Add(showsAt, () => puddles.Show(world, w));
            world.Events.Add(showsAt + PuddleFadesAfterShowing, () => puddles.Fade(w));
            world.Events.Add(showsAt + PuddleLastsAfterShowing, () => puddles.Remove(w));
        }
        world.Events.Add(CursesAt, () => Chain(StatusId.CurseOfEverlastingLight, TetherId.CurseOfEverlastingLight, CursesEndAt - CursesAt)
            .ForEach(curse => curse.BreaksWhen(t => t.StretchLt(CurseMinLength))));
        world.Events.Add(TowerAt, BrightHunger);
        world.Events.Add(19.6f, () => SpawnOrbs(first: true));
        world.Events.Add(22.43f, () => SpawnOrbs(first: false));
        world.Events.Add(22.43f, () => Burst(first: true));
        world.Events.Add(PowerfulLightAt, PowerfulLight);
        world.Events.Add(25.4f, () => Burst(first: false));
        world.Events.Add(25.7f, ShowCenterTower);
        world.Events.Add(29.58f, () => RemoveOrbs(first: true));
        world.Events.Add(29.93f, () => usurper?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        world.Events.Add(32f, () => usurper?.Cast(state.Pairs ? ActionId.BanishPairsCast : ActionId.BanishSpreadCast, animationLock: 3.1f));
        world.Events.Add(32f, () => usurper?.VoiceLine(VoiceLineId.Banish));
        world.Events.Add(32.76f, () => RemoveOrbs(first: false));
        world.Events.Add(33.8f, CenterTower);
        world.Events.Add(37.1f, Banish);
        world.Events.Add(40.08f, () => usurper?.SetTargetable(true));
        world.Events.Add(40.23f, () => usurper?.Cast(ActionId.HouseOfLightCast, animationLock: 3.1f));
        world.Events.Add(45.95f, HouseOfLight);
    }

    private void SpawnActors()
    {
        usurper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.UsurperP2,
            NameId: BNpcNameId.Usurper,
            Level: Level,
            Targetable: true,
            Placement: new Placement(new Vector3(8.66f, 0, 10.06f), -2.43f),
            ModelCharaId: ModelCharaId.UsurperLight,
            InitialModeAttributeFlags: 0xFC,
            LayoutId: LayoutId.UsurperP2));
    }

    private void AssignDebuffs()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
            if (state.InitialStacks(role) is > 0 and var stacks && usurper is not null)
                member.AddStatus(StatusId.Lightsteeped, 36f, stacks, overrideStacks: true)?.ShownAsFrom(usurper);
            if (state.BaitsHammer(role)) member.AttachLockonVfx(LockonId.LuminousHammer);
        }
        foreach (var role in state.Weights)
            world.Party.Get(role)?.AddStatus(StatusId.WeightOfLight, 16f);
    }

    private List<SimTether> Chain(ushort statusId, ushort tetherId, float duration)
    {
        var chain = new List<SimTether>();
        for (var i = 0; i < 6; i++)
        {
            var (from, to) = state.ChainLink(i);
            world.Party.Get(from)?.AddStatus(statusId, duration);
            chain.Add(world.Tether(world.Party.Get(from), world.Party.Get(to), tetherId, duration));
        }
        links.AddRange(chain);
        return chain;
    }

    private void BreakChainsOnSnappedLink(TetherEnded ended)
    {
        if (ended.Reason != TetherEndReason.Expired && links.Contains(ended.Tether)) BreakChains();
    }

    // Clearing every link and debuff first keeps Refulgent Fate's own deaths from breaking them again.
    private void BreakChains()
    {
        foreach (var link in links) link.Despawn();
        links.Clear();
        foreach (var member in world.Party.AllMembers())
        {
            member.RemoveStatus(StatusId.ChainsOfEverlastingLight);
            member.RemoveStatus(StatusId.CurseOfEverlastingLight);
        }
        usurper?.CastFromHelper(Actions.RefulgentFate, at: HelperRests[0]);
    }

    private static byte TowerSlot(int tower) => (byte)(MapEffect.LightRampantNorthTower + TowerSlotOffsets[tower]);

    private void ShowTowers()
    {
        for (var i = 0; i < 6; i++)
        {
            world.Map.AddEffect(MapEffect.TowerShow, TowerSlot(i));
            world.Map.AddEffect(MapEffect.TowerBase, (byte)(MapEffect.LightRampantNorthTowerBase + TowerSlotOffsets[i]));
            towers.Add(new MapEffectTower(world.Map, TowerSlot(i), Tower(i), TowerRadius, [MapEffect.TowerOccupied[0]], MapEffect.TowerEmpty));
        }
    }

    private void SnapshotHammerTargets()
    {
        foreach (var role in state.HammerBaiters)
            if (world.Party.Get(role) is { } target && target.IsAlive()) puddles.Snapshot(target);
    }

    private void LuminousHammer(int wave)
    {
        var i = HammerHelper;
        foreach (var role in state.HammerBaiters)
        {
            if (world.Party.Get(role) is not { } target || !target.IsAlive()) continue;
            usurper?.CastFromHelper(Actions.LuminousHammer, target, at: HelperRests[i++]);
            puddles.Drop(wave, target, HammerAt(wave) + PuddleShowsAfterHammer + PuddleArmsAfterShowing);
        }
    }

    private void BrightHunger()
    {
        towers.Clear();
        for (var i = 0; i < 6; i++) world.Map.AddEffect(MapEffect.TowerHide, TowerSlot(i));
        for (var h = 0; h < 6; h++)
        {
            var tower = Tower(HelperTowers[h]);
            usurper?.CastFromHelper(Actions.BrightHunger, tower, at: new Placement(tower, 0f));
        }
    }

    private void SpawnOrbs(bool first)
    {
        foreach (var position in state.Orbs(first))
            orbs[first ? 0 : 1].Add(world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.HolyLight,
                NameId: BNpcNameId.HolyLight,
                Level: Level,
                Placement: new Placement(position, 0f),
                LayoutId: LayoutId.HolyLight)));
    }

    private void Burst(bool first)
    {
        foreach (var orb in orbs[first ? 0 : 1]) orb?.Cast(Actions.Burst);
    }

    private void RemoveOrbs(bool first)
    {
        foreach (var orb in orbs[first ? 0 : 1]) orb?.Despawn();
        orbs[first ? 0 : 1].Clear();
    }

    private void PowerfulLight()
    {
        var i = HammerHelper;
        foreach (var role in state.Weights)
            if (world.Party.Get(role) is { } target && target.IsAlive())
                usurper?.CastFromHelper(Actions.PowerfulLight, target, at: HelperRests[i++]);
    }

    private void ShowCenterTower()
    {
        world.Map.AddEffect(MapEffect.TowerShow, MapEffect.LightRampantCenterTower);
        world.Map.AddEffect(MapEffect.TowerBase, MapEffect.LightRampantCenterTowerBase);
        // UNVERIFIED what retail shows as players leave it: pulls only show it filling.
        centerTower = new MapEffectTower(world.Map, MapEffect.LightRampantCenterTower, Vector3.Zero, TowerRadius, MapEffect.TowerOccupied, null);
    }

    private void CenterTower()
    {
        centerTower = null;
        world.Map.AddEffect(MapEffect.TowerHide, MapEffect.LightRampantCenterTower);
        usurper?.CastFromHelper(Actions.BrightHungerCenter, Vector3.Zero, at: new Placement(Vector3.Zero, 0f));
    }

    // Pairs go on all supports or all DPS; spreads on everyone.
    private void Banish()
    {
        var targets = (state.Pairs
            ? world.Party.ActiveMembers().Where(m => m is ISimPartyMember p && p.Role.IsDps() != state.SupportPairs)
            : world.Party.ActiveMembers()).ToList();
        var i = HelperRests.Length - targets.Count;
        foreach (var target in targets)
            usurper?.CastFromHelper(state.Pairs ? Actions.BanishPairs : Actions.BanishSpread, target, at: BanishFrom(i++));
    }

    // The spot the Bright Hungers left each helper on.
    private static Placement BanishFrom(int helper) => helper switch
    {
        < 6 => new Placement(Tower(HelperTowers[helper]), 0f),
        7 => new Placement(Vector3.Zero, 0f),
        _ => HelperRests[helper],
    };

    private void HouseOfLight()
    {
        foreach (var target in world.Party.ActiveMembers().ToList())
            usurper?.CastFromHelper(Actions.HouseOfLight, at: new Placement(Vector3.Zero, 0f).Face(target.Position));
    }

    public void OnActionResolved(ActionResolved resolved) => PunishUnsoakedTowers(resolved);
    public void OnTetherEnded(TetherEnded ended) => BreakChainsOnSnappedLink(ended);
    public void OnStatusAdded(StatusAdded added) => Lightsteeped.ExplodeFifthStack(world, added);

    private void PunishUnsoakedTowers(ActionResolved resolved)
        => Towers.ResolveTower(world, resolved, ActionId.BrightHunger, Actions.InescapableIllumination,
            minSoakers: resolved.Action == Actions.BrightHungerCenter ? 4 : 1);

    public void Tick(float delta, float elapsed)
    {
        foreach (var tower in towers) tower.Tick(world.Party);
        centerTower?.Tick(world.Party);
        puddles.BleedWhoeverStandsIn(world, elapsed, "stepped in a Luminous Hammer puddle");
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new FruP2LightRampantAiReplayStateMessage(Enumerable.Range(0, 8).Select(s.Slot).ToArray(), s.NorthOrbsFirst, s.Pairs)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP2LightRampantAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP2LightRampantState.FromNetworkReplay(msg.Slots, msg.NorthOrbsFirst, msg.Pairs);
        if (shadowState == null) return null;
        ((IScenarioAi<FruP2LightRampantState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
