using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongScenario : IMultiplayerReplayable
{
    public string Name => "Darklit Dragonsong";
    public IPhase Phase => FruZone.P4;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP4DarklitDragonsongAi(playerBaitsSomberDance: false), new FruP4DarklitDragonsongAi(playerBaitsSomberDance: true)];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly FruP4DarklitDragonsongSettingsWindow settingsWindow = new();

    private static readonly Vector3 FragmentPosition = new(0, 0, -16);
    // Curse chains stretched shorter than this snap. UNVERIFIED: retail's limit lies between 9.4 and 9.8.
    private const float CurseMinLength = 9.5f;
    // It takes every one of these and breaks on any other hit.
    private static readonly uint[] FragmentSurvives = [ActionId.DarklitDragonsong, ActionId.HallowedWingsLeft, ActionId.HallowedWingsRight];

    private static readonly Vector3 OracleCenterSpot = new(0, 0, 5);
    private static readonly Vector3 UsurperAkhMornSpot = new(-3.55f, 0, 0);
    private static readonly Vector3 OracleAkhMornSpot = new(3.53f, 0, 0);

    private SimWorld world = null!;
    private FruP4DarklitDragonsongState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it.
    public FruP4DarklitDragonsongState? LastState { get; private set; }
    private SimEnemy? usurper, oracle, wyrm, fragment, gaia, ryne;
    private readonly List<Vector3> akhRhaiPuddles = [];
    private readonly List<SimTether> links = [];
    private bool fragmentBroken;
    private readonly List<MapEffectTower> towers = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP4DarklitDragonsongState(world.Rng, world.Party.PlayerRole, settingsWindow.Overrides);
        LastState = state;
        links.Clear();
        fragmentBroken = false;
        towers.Clear();
        akhRhaiPuddles.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<FruP4DarklitDragonsongState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(1f, () => usurper?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
        world.Events.Add(1f, () => oracle?.SetTarget(world.Party.Get(PartyRole.OffTank), follow: false));
        world.Events.Add(1.5f, () => usurper?.ActorControl.Transform(UsurperDragonBNpcState));
        world.Events.Add(1.5f, () => usurper?.AddStatusParam(TransfigurationStatus, UsurperDragonTransformation));
        world.Events.Add(1.5f, () => usurper?.SetModelState(4));
        world.Events.Add(1.8f, SpawnGreatWyrm);
        world.Events.Add(2.4f, () => wyrm?.SetTarget(world.Party.Get(PartyRole.OffTank), follow: false));
        world.Events.Add(6.6f, AkhRhai);
        world.Events.Add(6.6f, DrachenArmor);
        world.Events.Add(6.6f, EdgeOfOblivion);
        for (var hit = 0; hit < 10; hit++)
            world.Events.Add(9.15f + hit * 0.59f, RepeatAkhRhai);
        world.Events.Add(12.6f, () => oracle?.PlayActionTimeline(TimelineId.OracleShow));
        world.Events.Add(12.6f, () => oracle?.SetTargetable(true));
        world.Events.Add(14.8f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(14.8f, () => oracle?.Face(OracleCenterSpot));
        world.Events.Add(14.8f, () => oracle?.Cast(ActionId.OracleReposition, animationLock: 1.1f));
        world.Events.Add(15.4f, () => oracle?.SetPosition(OracleCenterSpot));
        world.Events.Add(16f, () => oracle?.SetRotation(0f));
        world.Events.Add(16f, () => BattleTalk(usurper, 0x6, UsurperBattleTalk));
        world.Events.Add(16f, () => MarkWaters(LockonId.Stack));
        world.Events.Add(16.1f, () => usurper?.Cast(Actions.DarklitDragonsong));
        world.Events.Add(16.1f, () => oracle?.Cast(ActionId.DarklitDragonsongOracle, animationLock: 3.1f));
        world.Events.Add(18f, () => gaia?.PlayActionTimeline(TimelineId.GaiaDarklit));
        world.Events.Add(18f, () => ryne?.PlayActionTimeline(TimelineId.RyneDarklit));
        world.Events.Add(19.2f, () => gaia?.SetModelState(0x1F));
        world.Events.Add(19.2f, () => ryne?.SetModelState(0x05));
        world.Events.Add(21.1f, () => Waters().ForEach(m => m.AddStatus(StatusId.DarkWaterWaiting, DarkWaterAt - 21.1f)));
        world.Events.Add(21.1f, () => Waters().ForEach(m => m.AddStatus(StatusId.DarkWaterMarkerDelay, 35.1f - 21.1f)));
        world.Events.Add(ChainsAt, () => world.Party.ActiveMembers().ToList().ForEach(m => m.AddStatus(StatusId.Lightsteeped, 12f, 3, overrideStacks: true)));
        world.Events.Add(ChainsAt, () => Chain(StatusId.RefulgentChain, TetherId.ChainsOfEverlastingLight, CursesAt - ChainsAt));
        world.Events.Add(24.1f, ShowTowers);
        world.Events.Add(24.1f, () => BattleTalk(gaia, 0x7, BNpcNameId.Gaia));
        world.Events.Add(24.2f, () => usurper?.Cast(ActionId.PathOfLightCast, animationLock: 2.1f));
        world.Events.Add(CursesAt, () => Chain(StatusId.RefulgentFate, TetherId.CurseOfEverlastingLight, CursesEndAt - CursesAt)
            .ForEach(curse => curse.BreaksWhen(t => t.StretchLt(CurseMinLength))));
        world.Events.Add(29.1f, () => BattleTalk(ryne, 0x8, BNpcNameId.Ryne));
        world.Events.Add(TowersAt, BrightHunger);
        world.Events.Add(32.2f, () => oracle?.Cast(ActionId.SpiritTakerCast, animationLock: 0.2f));
        world.Events.Add(33.05f, PathOfLight);
        world.Events.Add(35f, () => BattleTalk(gaia, 0x9, BNpcNameId.Gaia));
        world.Events.Add(35.1f, () => MarkWaters(LockonId.WaterCountdown));
        world.Events.Add(35.3f, HallowedWings);
        world.Events.Add(35.6f, SpiritTaker);
        world.Events.Add(38.4f, () => oracle?.Cast(ActionId.SomberDanceCast, animationLock: 0.2f));
        world.Events.Add(DarkWaterAt, DarkWater);
        world.Events.Add(43.8f, () => SomberDance(Actions.SomberDanceFar, farthest: true));
        world.Events.Add(45.4f, EdgeOfOblivion);
        world.Events.Add(47f, () => SomberDance(Actions.SomberDanceNear, farthest: false));
        world.Events.Add(51.55f, () => usurper?.Face(UsurperAkhMornSpot));
        world.Events.Add(51.55f, () => oracle?.Face(OracleAkhMornSpot));
        world.Events.Add(51.55f, () => usurper?.Cast(ActionId.Unnamed, animationLock: 1.1f));
        world.Events.Add(51.55f, () => oracle?.Cast(ActionId.OracleReposition, animationLock: 1.1f));
        world.Events.Add(52.2f, () => usurper?.SetPosition(UsurperAkhMornSpot));
        world.Events.Add(52.2f, () => oracle?.SetPosition(OracleAkhMornSpot));
        world.Events.Add(52.4f, () => wyrm?.SetPosition(new Placement(UsurperAkhMornSpot, -0.6f)));
        world.Events.Add(52.65f, () => oracle?.SetRotation(MathF.PI / 4));
        world.Events.Add(52.8f, () => usurper?.SetRotation(-MathF.PI / 4));
        world.Events.Add(52.8f, () => BattleTalk(usurper, 0x35, BNpcNameId.UsurperAndOracle));
        world.Events.Add(52.9f, () => usurper?.Cast(ActionId.AkhMornUsurper, animationLock: 4.1f));
        world.Events.Add(52.9f, () => usurper?.VoiceLine(VoiceLineId.AkhMorn));
        world.Events.Add(52.9f, () => oracle?.Cast(ActionId.AkhMornOracle, animationLock: 4.1f));
        for (var hit = 0; hit < 4; hit++)
            world.Events.Add(57.75f + 1.05f * hit, AkhMorn);
    }

    // TODO: the Fragment's HP bar empties when it's hit; a spawned enemy's HP can't be set.
    private void SpawnActors()
    {
        usurper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.UsurperP4,
            NameId: BNpcNameId.Usurper,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            InitialModeAttributeFlags: 0xFC,
            LayoutId: LayoutId.UsurperP4));
        oracle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OracleP4,
            NameId: BNpcNameId.Oracle,
            Level: Level,
            EnemyList: EnemyListMode.OnlyWhenVisible,
            Visibility: SpawnVisibility.HiddenUntilShown,
            Placement: new Placement(new Vector3(3.54f, 0, 0), MathF.PI / 4),
            LayoutId: LayoutId.OracleP4,
            Appearance: Appearance.Oracle));
        fragment = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.FragmentOfFate,
            NameId: BNpcNameId.FragmentOfFate,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Never,
            Placement: new Placement(FragmentPosition, 0f),
            DisplayFlags: 0x40003,
            LayoutId: LayoutId.FragmentOfFate,
            IsHostile: false,
            Statuses: [new(StatusId.Incurable)]));
        gaia = SpawnVision(BNpcBaseId.VisionOfGaia, BNpcNameId.Gaia, Appearance.VisionOfGaia, new Placement(new Vector3(-0.71f, 0, -16), 1.22f),
            0x4020B, LayoutId.VisionOfGaia, 0x79);
        ryne = SpawnVision(BNpcBaseId.VisionOfRyne, BNpcNameId.Ryne, Appearance.VisionOfRyne, new Placement(new Vector3(0.71f, 0, -16), -0.84f),
            0x4020A, LayoutId.VisionOfRyne, 0x07);

    }

    // Spawned as they stand once shown: retail's DisplayFlags less the hidden-until-shown bit.
    private SimEnemy? SpawnVision(uint bnpcBaseId, uint nameId, NpcAppearance appearance, Placement placement, uint displayFlags, uint layoutId,
        byte modelState) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: bnpcBaseId,
        NameId: nameId,
        Level: Level,
        EnemyList: EnemyListMode.Never,
        Placement: placement,
        DisplayFlags: displayFlags,
        LayoutId: layoutId,
        IsHostile: false,
        Appearance: appearance,
        Statuses: [new(StatusId.Levitation, 200)],
        ModelState: modelState));

    private void SpawnGreatWyrm() => wyrm = world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: BNpcBaseId.GreatWyrm,
        NameId: BNpcNameId.GreatWyrm,
        Level: Level,
        EnemyList: EnemyListMode.Never,
        Placement: new Placement(Vector3.Zero, MathF.PI)));


    private void BattleTalk(SimEnemy? speaker, uint line, uint nameId)
    {
        if (speaker is not null) world.Map.DirectorUpdate(0x80000027U, line, 0x2U, nameId, speaker.EntityId);
    }

    private void EdgeOfOblivion()
    {
        fragment?.Cast(Actions.EdgeOfOblivion);
        fragment?.CastFromHelper(Actions.EdgeOfOblivionAnimation);
    }

    private void DrachenArmor() => usurper?.CastFromHelper(ActionId.DrachenArmor, animationLock: 2.1f);

    private void BreakFragmentOnAvoidableHit(ActionResolved resolved)
    {
        if (fragmentBroken || fragment is null || !resolved.NpcHits.Contains(fragment) || FragmentSurvives.Contains(resolved.Action.ActionId)) return;
        fragmentBroken = true;
        world.Fail($"{Natives.Data.ActionName(resolved.Action.ActionId)} hit the Fragment of Fate");
    }

    private List<SimCharacter> Waters()
        => state.Waters.Select(world.Party.Get).OfType<SimCharacter>().Where(m => m.IsAlive()).ToList();

    private void MarkWaters(uint lockonId) => Waters().ForEach(m => m.AttachLockonVfx(lockonId));

    private void AkhRhai()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
        {
            var position = member.Position;
            akhRhaiPuddles.Add(position);
            usurper?.CastFromHelper(ActionId.AkhRhaiCast, position, at: new Placement(position, 0f), animationLock: 3.1f);
        }
    }

    private void RepeatAkhRhai()
    {
        foreach (var position in akhRhaiPuddles)
            usurper?.CastFromHelper(Actions.AkhRhai, position, at: new Placement(position, 0f));
    }

    private List<SimTether> Chain(ushort statusId, ushort tetherId, float duration)
    {
        var chain = new List<SimTether>();
        for (var i = 0; i < 4; i++)
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
            member.RemoveStatus(StatusId.RefulgentChain);
            member.RemoveStatus(StatusId.RefulgentFate);
        }
        usurper?.CastFromHelper(Actions.RefulgentFate);
    }

    private static Vector3 Tower(int index) => new(0, 0, index == 0 ? -8f : 8f);

    private void ShowTowers()
    {
        for (var i = 0; i < 2; i++)
        {
            var slot = (byte)(MapEffect.DarklitFirstTower + i);
            world.Map.AddEffect(MapEffect.TowerShow, slot);
            world.Map.AddEffect(MapEffect.TowerBase, (byte)(MapEffect.DarklitFirstTowerBase + i));
            towers.Add(new MapEffectTower(world.Map, slot, Tower(i), TowerRadius, MapEffect.TowerOccupied[..2], MapEffect.DarklitTowerEmpty));
        }
    }

    private void BrightHunger()
    {
        towers.Clear();
        for (var i = 0; i < 2; i++)
        {
            world.Map.AddEffect(MapEffect.TowerHide, (byte)(MapEffect.DarklitFirstTower + i));
            usurper?.CastFromHelper(Actions.BrightHungerDarklit, Tower(i), at: new Placement(Tower(i), 0f));
        }
    }

    // Path of Light's four cones go to whoever stands nearest the center.
    private void PathOfLight()
    {
        foreach (var bait in world.Party.ActiveMembers().OrderBy(m => m.Position.LengthSquared()).Take(4).ToList())
            usurper?.CastFromHelper(Actions.PathOfLight, at: new Placement(Vector3.Zero, 0f).Face(bait.Position));
    }

    // Aimed at a point behind the north-facing Usurper on the wing's side: the sheet's 80y rect runs
    // north from there over that half of the arena.
    private void HallowedWings()
    {
        if (state.EastWing) usurper?.Cast(Actions.HallowedWingsRight, new Vector3(20, 0, 40));
        else usurper?.Cast(Actions.HallowedWingsLeft, new Vector3(-20, 0, 40));
    }

    private void SpiritTaker()
    {
        if (oracle is not { } boss || world.Party.Get(state.SpiritTakerTarget) is not { } target || !target.IsAlive()) return;
        boss.Cast(Actions.SpiritTaker, target);
        JumpTowards(target.Position, 0.67f);
    }

    private void JumpTowards(Vector3 position, float delay)
    {
        if (oracle is not { } boss) return;
        var landing = JumpLanding(boss.Position, position);
        world.Events.Add(delay, () => boss.SetPosition(landing));
    }

    private void DarkWater()
    {
        foreach (var holder in Waters())
            oracle?.CastFromHelper(Actions.DarkWater, holder, at: new Placement(holder.Position, 0f));
    }

    private void SomberDance(EnemyAction action, bool farthest)
    {
        if (oracle is not { } boss) return;
        var byDistance = world.Party.ActiveMembers().OrderBy(m => Vector3.DistanceSquared(m.Position, boss.Position)).ToList();
        if (byDistance.Count == 0) return;
        var target = farthest ? byDistance[^1] : byDistance[0];
        boss.Cast(action, target);
        JumpTowards(target.Position, 1.1f);
    }

    private void AkhMorn()
    {
        CastAkhMorn(usurper, Actions.AkhMornUsurperHit, PartyRole.MainTank);
        CastAkhMorn(oracle, Actions.AkhMornOracleHit, PartyRole.OffTank);
    }

    private void CastAkhMorn(SimEnemy? caster, EnemyAction action, PartyRole role)
    {
        if (world.Party.Get(role) is not { } tank || !tank.IsAlive()) return;
        caster?.CastFromHelper(action, tank, at: new Placement(tank.Position, 0f));
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        BreakFragmentOnAvoidableHit(resolved);
        // UNVERIFIED: no pull leaves a Darklit tower unsoaked; Light Rampant's punishment.
        Towers.ResolveTower(world, resolved, ActionId.BrightHunger, Actions.InescapableIllumination, minSoakers: 2);
    }
    public void OnTetherEnded(TetherEnded ended) => BreakChainsOnSnappedLink(ended);
    public void OnStatusAdded(StatusAdded added) => Lightsteeped.ExplodeFifthStack(world, added);

    public void Tick(float delta, float elapsed)
    {
        foreach (var tower in towers) tower.Tick(world.Party);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { SomberTank: { } somberTank } s
            ? new FruP4DarklitDragonsongAiReplayStateMessage(s.LineupOrder.ToArray(), s.TetherWaterIndex, s.FreeWaterIndex, s.EastWing, somberTank)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP4DarklitDragonsongAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP4DarklitDragonsongState.FromNetworkReplay(msg.LineupOrder, msg.TetherWaterIndex, msg.FreeWaterIndex, msg.EastWing, msg.SomberTank);
        if (shadowState == null) return null;
        ((IScenarioAi<FruP4DarklitDragonsongState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
