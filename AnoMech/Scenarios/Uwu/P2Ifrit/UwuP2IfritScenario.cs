using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Scenarios.Uwu.UwuUtils;
using static AnoMech.Scenarios.Uwu.P2Ifrit.UwuP2IfritState;

namespace AnoMech.Scenarios.Uwu.P2Ifrit;

// Vulcan Burst's knockback is left out (shielded).
public sealed class UwuP2IfritScenario : IScenario
{
    public string Name => "Ifrit";
    public IPhase Phase => UwuZone.Ifrit;
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UwuP2IfritAi()];

    private const int FlamingCrushMinStack = 5;
    private const float IncinerateHalfAngle = MathF.PI / 4f;
    private const uint NailMaxHp = 26870;
    private const float NailDrainFrom = 45f;
    private const float NailDrainTo = 69.46f;
    private const float HelperLifetime = 1.5f;
    private const float HellfireDamage = 0.65f;
    private const float VulcanBurstDamage = 0.15f;
    private const float IncinerateDamage = 0.4f;
    private const float InfernalSurgeDamage = 0.22f;

    private readonly UwuP2IfritSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private DamageSolver damage = null!;
    private UwuP2IfritState state = null!;

    private SimEnemy? ifrit;
    private bool ifritTanked;
    private bool followTankAfterMove;
    private readonly List<SimEnemy> helpers = [];
    private readonly List<SimEnemy> radiantPlumeCasters = [];
    private readonly Dictionary<float, SimEnemy?> nails = [];
    private readonly Dictionary<float, float> nailKillAt = [];
    private readonly SimEnemy?[] eruptionCasters = new SimEnemy?[4];
    private readonly Vector3?[] eruptionSpots = new Vector3?[4];
    private IReadOnlyList<SimCharacter> eruptionBaits = [];
    private readonly Dictionary<float, SimEnemy?> dashClones = [];
    private readonly SimEnemy?[] crossCasters = new SimEnemy?[2];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        utils = new UwuUtils(world);
        damage = new DamageSolver(party);
        state = new UwuP2IfritState(world.Rng, party, settingsWindow.Overrides);
        helpers.Clear();
        radiantPlumeCasters.Clear();
        nails.Clear();
        nailKillAt.Clear();
        dashClones.Clear();
        Array.Clear(eruptionCasters);
        Array.Clear(eruptionSpots);
        Array.Clear(crossCasters);
        eruptionBaits = [];
        ifritTanked = false;
        followTankAfterMove = false;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UwuP2IfritState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnIfrit);
        world.Events.Add(0f, StartPartyInTheMiddle);
        world.Events.Add(2.93f, () => Arrive(ifrit));
        world.Events.Add(5.12f, () => CastSelf(ifrit, ActionId.CrimsonCyclone, 2.7f));
        world.Events.Add(5.16f, CastRadiantPlumes);
        world.Events.Add(8.10f, () => ResolveCrimsonCyclone(ifrit, state.OpenerBearing, ActionId.CrimsonCyclone));
        world.Events.Add(9.13f, ResolveRadiantPlumes);
        world.Events.Add(9.10f, () => ifrit?.SetVisible(false));
        world.Events.Add(10.15f, PlaceIfritFacingSouth);
        world.Events.Add(10.24f, () => Arrive(ifrit));
        world.Events.Add(12.29f, () => TankIfrit(true));
        world.Events.Add(12.38f, () => CastSelf(ifrit, ActionId.Hellfire, 2.7f));
        world.Events.Add(15.37f, Hellfire);

        world.Events.Add(23.56f, VulcanBurst);
        world.Events.Add(26.38f, Incinerate);
        world.Events.Add(29.50f, Incinerate);
        world.Events.Add(33.60f, Incinerate);

        world.Events.Add(39.89f, SpawnNails);
        world.Events.Add(40.82f, () => SetNailsTargetable(true));
        world.Events.Add(41.50f, () => MoveIfrit(state.FromReference(IfritAtNailsReference)));
        world.Events.Add(45.76f, TetherInfernalFetters);
        world.Events.Add(46.03f, () => CastInfernoHowl(state.HowlFirst));
        world.Events.Add(47.99f, () => ResolveInfernoHowl(state.HowlFirst, 18f));

        world.Events.Add(51.16f, MarkEruptionBaits);
        world.Events.Add(51.16f, () => CastSelf(ifrit, ActionId.EruptionIfrit, 2.2f));
        world.Events.Add(51.16f, () => CastEruptions(0));
        world.Events.Add(53.12f, () => CastEruptions(1));
        world.Events.Add(53.65f, () => PlayEffect(ifrit, ActionId.EruptionIfrit, 2.4f));
        world.Events.Add(53.96f, () => SearingWind(state.HowlFirst));
        world.Events.Add(54.14f, () => ResolveEruptions(0));
        world.Events.Add(55.12f, () => CastEruptions(0));
        world.Events.Add(56.10f, () => ResolveEruptions(1));
        world.Events.Add(57.12f, () => CastEruptions(1));
        world.Events.Add(57.66f, () => KillNail(state.NailKillBearings[0]));
        world.Events.Add(58.10f, () => ResolveEruptions(0));
        world.Events.Add(59.97f, () => SearingWind(state.HowlFirst));
        world.Events.Add(60.10f, () => ResolveEruptions(1));
        world.Events.Add(62.55f, () => KillNail(state.NailKillBearings[1]));
        world.Events.Add(65.99f, () => SearingWind(state.HowlFirst));
        world.Events.Add(66.03f, () => KillNail(state.NailKillBearings[2]));
        world.Events.Add(69.46f, () => KillNail(state.NailKillBearings[3]));

        world.Events.Add(71.02f, () => TankIfrit(false));
        world.Events.Add(71.02f, () => Leave(ifrit));
        world.Events.Add(72.20f, () => ifrit?.SetVisible(false));
        world.Events.Add(75.25f, PlaceIfritFacingSouth);
        world.Events.Add(75.30f, () => Arrive(ifrit));
        world.Events.Add(75.30f, () => TankIfrit(true));
        world.Events.Add(75.38f, () => CastSelf(ifrit, ActionId.Hellfire, 2.7f));
        world.Events.Add(78.37f, Hellfire);
        world.Events.Add(79.50f, () => MoveIfrit(state.FromReference(IfritAtCornerReference)));

        world.Events.Add(84.74f, () => CastInfernoHowl(state.HowlSecond));
        world.Events.Add(86.70f, () => ResolveInfernoHowl(state.HowlSecond, 30f));
        world.Events.Add(89.87f, MarkEruptionBaits);
        world.Events.Add(89.87f, () => CastSelf(ifrit, ActionId.EruptionIfrit, 2.2f));
        world.Events.Add(89.87f, () => CastEruptions(0));
        world.Events.Add(91.83f, () => CastEruptions(1));
        world.Events.Add(92.36f, () => PlayEffect(ifrit, ActionId.EruptionIfrit, 2.4f));
        world.Events.Add(92.68f, () => SearingWind(state.HowlSecond));
        world.Events.Add(92.85f, () => ResolveEruptions(0));
        world.Events.Add(93.83f, () => CastEruptions(0));
        world.Events.Add(94.81f, () => ResolveEruptions(1));
        world.Events.Add(95.84f, () => CastEruptions(1));
        world.Events.Add(96.00f, () => SpawnDashClone(180f));
        world.Events.Add(96.00f, () => SpawnDashClone(90f));
        world.Events.Add(96.81f, () => ResolveEruptions(0));
        world.Events.Add(96.91f, () => CastDash(180f));
        world.Events.Add(96.91f, () => CastDash(90f));
        world.Events.Add(98.69f, () => SearingWind(state.HowlSecond));
        world.Events.Add(98.82f, () => ResolveEruptions(1));
        world.Events.Add(99.89f, () => ResolveDash(180f));
        world.Events.Add(99.89f, () => ResolveDash(90f));
        world.Events.Add(101.10f, () => DespawnDashClone(180f));
        world.Events.Add(101.10f, () => DespawnDashClone(90f));

        world.Events.Add(102.84f, () => CastInfernoHowl(state.HowlThird));
        world.Events.Add(104.70f, () => SearingWind(state.HowlSecond));
        world.Events.Add(104.79f, () => ResolveInfernoHowl(state.HowlThird, 30f));
        world.Events.Add(109.88f, () => Lockon(Get(state.FlamingCrushTargets[0]), LockonId.FlamingCrush));
        world.Events.Add(110.72f, () => SearingWind(state.HowlSecond));
        world.Events.Add(110.77f, () => SearingWind(state.HowlThird));
        world.Events.Add(115.00f, () => FlamingCrush(state.FlamingCrushTargets[0]));
        world.Events.Add(116.74f, () => SearingWind(state.HowlSecond));
        world.Events.Add(116.78f, () => SearingWind(state.HowlThird));

        world.Events.Add(119.02f, () => TankIfrit(false));
        world.Events.Add(119.02f, () => Leave(ifrit));
        world.Events.Add(120.30f, () => ifrit?.SetVisible(false));
        world.Events.Add(122.53f, () => SpawnDashClone(state.NailKillBearings[0], awakened: state.AwakenedDash == 0));
        world.Events.Add(122.53f, () => SpawnDashClone(state.NailKillBearings[1], awakened: state.AwakenedDash == 1));
        world.Events.Add(122.53f, () => SpawnDashClone(state.NailKillBearings[2], awakened: state.AwakenedDash == 2));
        world.Events.Add(122.53f, () => SpawnDashClone(state.NailKillBearings[3], awakened: state.AwakenedDash == 3));
        world.Events.Add(122.80f, () => SearingWind(state.HowlThird));
        world.Events.Add(123.43f, () => CastDash(state.NailKillBearings[0]));
        world.Events.Add(124.85f, () => CastDash(state.NailKillBearings[1]));
        world.Events.Add(126.24f, () => CastDash(state.NailKillBearings[2]));
        world.Events.Add(126.41f, () => ResolveDash(state.NailKillBearings[0]));
        world.Events.Add(127.61f, () => DespawnDashClone(state.NailKillBearings[0]));
        world.Events.Add(127.66f, () => CastDash(state.NailKillBearings[3]));
        world.Events.Add(127.83f, () => ResolveDash(state.NailKillBearings[1]));
        world.Events.Add(128.56f, () => AwakenedCross(0));
        world.Events.Add(128.82f, () => SearingWind(state.HowlThird));
        world.Events.Add(129.03f, () => DespawnDashClone(state.NailKillBearings[1]));
        world.Events.Add(129.22f, () => ResolveDash(state.NailKillBearings[2]));
        world.Events.Add(129.98f, () => AwakenedCross(1));
        world.Events.Add(130.42f, () => DespawnDashClone(state.NailKillBearings[2]));
        world.Events.Add(130.64f, () => ResolveDash(state.NailKillBearings[3]));
        world.Events.Add(131.37f, () => AwakenedCross(2));
        world.Events.Add(131.84f, () => DespawnDashClone(state.NailKillBearings[3]));
        world.Events.Add(132.79f, () => AwakenedCross(3));

        world.Events.Add(134.00f, PlaceIfritFacingSouth);
        world.Events.Add(134.05f, () => Arrive(ifrit));
        world.Events.Add(134.83f, () => SearingWind(state.HowlThird));
        world.Events.Add(134.83f, () => TankIfrit(true));
        world.Events.Add(135.50f, () => MoveIfrit(new Vector3(-8f, 0f, 0f)));
        world.Events.Add(138.98f, Incinerate);
        world.Events.Add(142.10f, Incinerate);
        world.Events.Add(146.19f, Incinerate);

        world.Events.Add(152.29f, MarkEruptionBaits);
        world.Events.Add(152.29f, () => CastSelf(ifrit, ActionId.EruptionIfrit, 2.2f));
        world.Events.Add(152.29f, () => CastEruptions(0));
        world.Events.Add(154.25f, () => CastEruptions(1));
        world.Events.Add(154.78f, () => PlayEffect(ifrit, ActionId.EruptionIfrit, 2.4f));
        world.Events.Add(155.27f, () => ResolveEruptions(0));
        world.Events.Add(156.26f, () => CastEruptions(0));
        world.Events.Add(157.23f, () => ResolveEruptions(1));
        world.Events.Add(158.26f, () => CastEruptions(1));
        world.Events.Add(159.15f, () => Lockon(Get(state.FlamingCrushTargets[1]), LockonId.FlamingCrush));
        world.Events.Add(159.24f, () => ResolveEruptions(0));
        world.Events.Add(161.24f, () => ResolveEruptions(1));
        world.Events.Add(164.27f, () => FlamingCrush(state.FlamingCrushTargets[1]));
        world.Events.Add(166.50f, KillIfrit);
        world.Events.Add(168.00f, DespawnAll);
    }

    // After a scripted drag Ifrit goes back to the MT.
    public void Tick(float delta, float elapsed)
    {
        var timeline = world.Events.Elapsed;
        if (timeline >= NailDrainFrom && timeline < NailDrainTo) DrainNails(timeline);
        if (!followTankAfterMove || ifrit is not { IsMoving: false } boss) return;
        followTankAfterMove = false;
        if (ifritTanked) boss.Follow(Get(PartyRole.MainTank));
    }

    private void TankIfrit(bool tanked)
    {
        ifritTanked = tanked;
        ifrit?.SetTargetable(tanked);
        if (tanked) ifrit?.Follow(Get(PartyRole.MainTank));
        else ifrit?.Follow();
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private SimEnemy? SpawnEnemy(uint baseId, uint nameId, Placement placement, bool targetable, bool visible, EnemyListMode enemyList) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: targetable,
            EnemyList: enemyList,
            IsVisible: visible,
            Placement: placement));

    private SimEnemy? SpawnHelper(Vector3 position, float rotation = 0f)
    {
        var helper = SpawnEnemy(BNpcBaseId.Dummy, BNpcNameId.Dummy, new Placement(position, rotation), false, true, EnemyListMode.Never);
        if (helper != null) helpers.Add(helper);
        return helper;
    }

    private void SpawnIfrit()
    {
        var edge = EdgeSpot(state.OpenerBearing);
        ifrit = SpawnEnemy(BNpcBaseId.Ifrit, BNpcNameId.Ifrit, new Placement(edge, FacingCentre(edge)), false, false, EnemyListMode.Always);
        for (var i = 0; i < eruptionCasters.Length; i++) eruptionCasters[i] = SpawnHelper(Vector3.Zero);
    }

    private static void Arrive(SimEnemy? enemy)
    {
        enemy?.SetVisible(true);
        enemy?.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    private static void Leave(SimEnemy? enemy)
    {
        enemy?.SetTargetable(false);
        enemy?.PlayActionTimeline(ActionTimelineId.WarpStart);
    }

    // Ifrit always lands in the middle facing south; the tank picks him up from there.
    private void PlaceIfritFacingSouth() => ifrit?.SetPosition(new Placement(Vector3.Zero, 0f));

    private void StartPartyInTheMiddle()
    {
        for (var slot = 0; slot < 8; slot++)
        {
            var angle = slot * MathF.PI / 4f;
            party.Get(slot)?.SetPosition(new Placement(new Vector3(MathF.Cos(angle) * 1.2f, 0f, MathF.Sin(angle) * 1.2f), MathF.PI));
        }
    }

    private void MoveIfrit(Vector2 to) => MoveIfrit(new Vector3(to.X, 0f, to.Y));

    private void MoveIfrit(Vector3 to)
    {
        if (ifrit == null) return;
        var mainTank = Get(PartyRole.MainTank);
        ifrit.Follow();
        ifrit.MoveTo(to, 4f, mainTank == null ? null : Facing(to, mainTank.Position));
        followTankAfterMove = true;
    }

    private void CastRadiantPlumes()
    {
        foreach (var at in state.RadiantPlumes)
        {
            if (SpawnHelper(at, MathF.PI) is not { } caster) continue;
            radiantPlumeCasters.Add(caster);
            caster.NativeCast(ActionId.RadiantPlumePuddle, ActionType.Action, 0f, 3.7f, false, rotation: MathF.PI, position: at);
        }
    }

    private void ResolveRadiantPlumes()
    {
        foreach (var caster in radiantPlumeCasters)
        {
            PlayEffect(caster, ActionId.RadiantPlumePuddle, 0.1f, at: caster.Position);
            damage.Resolve(caster, ActionId.RadiantPlumePuddle, [DamageType.Lethal], []);
        }
        radiantPlumeCasters.Clear();
    }

    private void ResolveCrimsonCyclone(SimEnemy? caster, float fromBearing, uint actionId)
    {
        var edge = EdgeSpot(fromBearing);
        var rotation = FacingCentre(edge);
        PlayEffect(caster, actionId, 2.1f, rotation);
        damage.Resolve(IPositioned.From(new Placement(edge, rotation)), actionId, [DamageType.Lethal], []);
    }

    private void Hellfire()
    {
        PlayEffect(ifrit, ActionId.Hellfire, 2.1f);
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, HellfireDamage, ActionId.Hellfire, "Raidwide", false);
    }

    private void VulcanBurst()
    {
        PlayEffect(ifrit, ActionId.VulcanBurst, 1.1f);
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, VulcanBurstDamage, ActionId.VulcanBurst, "Raidwide", false);
    }

    private void Incinerate()
    {
        if (ifrit == null || Get(PartyRole.MainTank) is not { } mainTank) return;
        var rotation = Facing(ifrit.Position, mainTank.Position);
        ifrit.SetPosition(new Placement(ifrit.Position, rotation));
        PlayEffect(ifrit, ActionId.Incinerate, 1.1f, rotation, mainTank.GameObjectId);
        var hits = damage.Resolve(ifrit, ActionId.Incinerate, [DamageType.TankBuster], [(StatusId.FireResistanceDownII, 5f)],
            size: IncinerateHalfAngle, extraRange: ifrit.HitboxRadius);
        foreach (var tank in hits.Where(h => h.IsAlive()))
            damage.ApplyDamage(tank, IncinerateDamage, ActionId.Incinerate, "Tankbuster", false);
    }

    private void SpawnNails()
    {
        foreach (var bearing in state.NailKillBearings)
        {
            var nail = SpawnEnemy(BNpcBaseId.InfernalNail, BNpcNameId.InfernalNail, new Placement(NailSpot(bearing), 0f), false, true, EnemyListMode.Always);
            nails[bearing] = nail;
            SetNailHp(nail, 1f);
        }
        nailKillAt[state.NailKillBearings[0]] = 57.66f;
        nailKillAt[state.NailKillBearings[1]] = 62.55f;
        nailKillAt[state.NailKillBearings[2]] = 66.03f;
        nailKillAt[state.NailKillBearings[3]] = 69.46f;
    }

    private void SetNailsTargetable(bool targetable)
    {
        foreach (var nail in nails.Values) nail?.SetTargetable(targetable);
    }

    private void DrainNails(float now)
    {
        foreach (var (bearing, nail) in nails)
            if (nailKillAt.TryGetValue(bearing, out var killAt))
                SetNailHp(nail, (killAt - now) / (killAt - NailDrainFrom));
    }

    private static void SetNailHp(SimEnemy? nail, float fraction) => nail?.SetHealth(NailMaxHp, fraction);

    private void KillNail(float bearing)
    {
        if (!nails.TryGetValue(bearing, out var nail) || nail == null) return;
        SetNailHp(nail, 0f);
        PlayEffect(nail, ActionId.InfernalSurge, 1.1f);
        foreach (var member in party.ActiveMembers().ToList())
        {
            damage.ApplyDamage(member, InfernalSurgeDamage, ActionId.InfernalSurge, "Raidwide", false);
            member.AddStatus(StatusId.VulnerabilityUp, 1f);
        }
        nails.Remove(bearing);
        world.Events.Add(HelperLifetime, nail.Despawn);
    }

    private void TetherInfernalFetters() =>
        world.Tether(Get(PartyRole.OffTank), Get(state.FettersDps), TetherId.InfernalFetters, 21f, StatusId.InfernalFetters);

    private void CastInfernoHowl(PartyRole healer)
    {
        if (Get(healer) is not { } target) return;
        ifrit?.NativeCast(ActionId.InfernoHowl, ActionType.Action, 0f, 1.7f, false, targetId: target.GameObjectId);
    }

    private void ResolveInfernoHowl(PartyRole healer, float searingWindSeconds)
    {
        if (Get(healer) is not { } target) return;
        PlayEffect(ifrit, ActionId.InfernoHowl, 1.1f, target: target.GameObjectId);
        target.AddStatus(StatusId.SearingWind, searingWindSeconds + 0.9f);
    }

    // Searing Wind pulses around its holder; it spares the holder, and only tanks live through it.
    private void SearingWind(PartyRole healer)
    {
        if (Get(healer) is not { } holder || !holder.IsAlive()) return;
        var caster = SpawnHelper(holder.Position);
        PlayEffect(caster, ActionId.SearingWind, 1.1f, target: holder.GameObjectId);
        damage.Resolve(holder, ActionId.SearingWind, [DamageType.Lethal], [], excludeTargets: [holder]);
        world.Events.Add(HelperLifetime, () => DespawnHelper(caster));
    }

    // The Searing Wind holder never baits Eruption.
    private void MarkEruptionBaits()
    {
        if (ifrit == null) return;
        eruptionBaits = party.Find.FarestN(ifrit.Position, 8).Where(m => !m.HasStatus(StatusId.SearingWind)).Take(2).ToList();
    }

    private void CastEruptions(int pair)
    {
        for (var i = 0; i < 2; i++)
        {
            var slot = pair * 2 + i;
            eruptionSpots[slot] = null;
            if (i >= eruptionBaits.Count || !eruptionBaits[i].IsAlive() || eruptionCasters[slot] is not { } caster) continue;
            var at = eruptionBaits[i].Position;
            eruptionSpots[slot] = at;
            caster.SetPosition(new Placement(at, MathF.PI));
            caster.NativeCast(ActionId.EruptionPuddle, ActionType.Action, 0f, 2.7f, false, rotation: MathF.PI, position: at);
        }
    }

    private void ResolveEruptions(int pair)
    {
        for (var i = 0; i < 2; i++)
        {
            var slot = pair * 2 + i;
            if (eruptionSpots[slot] is not { } at) continue;
            PlayEffect(eruptionCasters[slot], ActionId.EruptionPuddle, 0.1f, at: at);
            damage.Resolve(IPositioned.From(at), ActionId.EruptionPuddle, [DamageType.Lethal], []);
            eruptionSpots[slot] = null;
        }
    }

    private void SpawnDashClone(float fromBearing, bool awakened = false)
    {
        var edge = EdgeSpot(fromBearing);
        var clone = SpawnEnemy(BNpcBaseId.Ifrit, BNpcNameId.Ifrit, new Placement(edge, FacingCentre(edge)), false, false, EnemyListMode.Never);
        dashClones[fromBearing] = clone;
        Arrive(clone);
        if (awakened && clone != null) utils.Awaken(clone, false);
    }

    private void CastDash(float fromBearing) => CastSelf(dashClones.GetValueOrDefault(fromBearing), ActionId.CrimsonCyclone, 2.7f);

    private void ResolveDash(float fromBearing) =>
        ResolveCrimsonCyclone(dashClones.GetValueOrDefault(fromBearing), fromBearing, ActionId.CrimsonCyclone);

    private void DespawnDashClone(float fromBearing)
    {
        if (!dashClones.Remove(fromBearing, out var clone)) return;
        clone?.Despawn();
    }

    private void AwakenedCross(int dash)
    {
        if (state.AwakenedDash != dash) return;
        var lanes = state.AwakenedCrossLanes;
        for (var i = 0; i < lanes.Count && i < crossCasters.Length; i++)
        {
            var edge = EdgeSpot(lanes[i].Bearing);
            crossCasters[i] ??= SpawnHelper(edge);
            crossCasters[i]?.SetPosition(new Placement(edge, FacingCentre(edge)));
            ResolveCrimsonCyclone(crossCasters[i], lanes[i].Bearing, ActionId.CrimsonCycloneAwaken);
        }
    }

    private void FlamingCrush(PartyRole role)
    {
        if (Get(role) is not { } target || !target.IsAlive()) return;
        PlayEffect(ifrit, ActionId.FlamingCrush, 1.1f, target: target.GameObjectId);
        damage.Resolve(target, ActionId.FlamingCrush, [DamageType.Magic], [], stackMinTargets: FlamingCrushMinStack);
    }

    private void DespawnHelper(SimEnemy? helper)
    {
        if (helper == null) return;
        helpers.Remove(helper);
        helper.Despawn();
    }

    private void KillIfrit()
    {
        ifrit?.Despawn();
        ifrit = null;
    }

    private void DespawnAll()
    {
        KillIfrit();
        foreach (var nail in nails.Values) nail?.Despawn();
        foreach (var clone in dashClones.Values) clone?.Despawn();
        foreach (var helper in helpers) helper.Despawn();
        nails.Clear();
        dashClones.Clear();
        helpers.Clear();
        radiantPlumeCasters.Clear();
        Array.Clear(eruptionCasters);
        Array.Clear(crossCasters);
    }
}
