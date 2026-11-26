using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Scenarios.Uwu.UwuUtils;
using static AnoMech.Scenarios.Uwu.P3TitanGaols.UwuP3TitanGaolsState;
using Actions = AnoMech.Scenarios.Uwu.UwuActions;

namespace AnoMech.Scenarios.Uwu.P3TitanGaols;

// From Titan's first jump to his awakening, which only a bomb chain bursting every gaol earns.
public sealed class UwuP3TitanGaolsScenario : IScenario
{
    public string Name => "Titan Gaols";
    public IPhase Phase => UwuZone.Titan;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UwuP3TitanGaolsAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;

    private readonly UwuP3TitanGaolsSettingsWindow settingsWindow = new();

    private const float BombFadesAfterBurst = 0.3f;
    private const float PrisonerFreedAfter = 1.1f;
    private const float SludgeSpawnsAfter = 0.85f;
    private const float SludgeArmsAfter = 1.8f;
    // UNVERIFIED: harmless once it fades, as FRU's Holy puddles are.
    private const float SludgeFadesAfter = 14.8f;
    private const float SludgeDespawnsAfter = 18.05f;
    private const uint SludgeArg2Base = 0x50;
    // UNVERIFIED: retail kills at least 4.4y out; Titan, 4.55y hitbox, charges from 8.6y.
    private const float SludgeRadius = 5f;
    // Bots keep their centre this far out, so their body stays clear of it.
    private const float SludgeAvoidRadius = SludgeRadius + 1f;
    private const float TitanChargeTick = 3f;
    private const int ChargesToAwaken = 3;
    private const string ShrunkenFloorDeath = "Fell off the shrunken floor";
    private SimWorld world = null!;
    private SimParty party = null!;
    private UwuUtils utils = null!;
    private UwuP3TitanGaolsState state = null!;

    private SimEnemy? titan;
    private readonly SimEnemy?[] bombs = new SimEnemy?[6];
    private readonly Dictionary<SimEnemy, PartyRole> gaols = [];
    private readonly List<Sludge> sludges = [];
    private float? titanChargesAt;

    private sealed class Sludge(Vector2 centre)
    {
        public bool Armed { get; set; }

        // Touching it is enough: any part of the hitbox inside.
        public bool Touches(SimCharacter character) =>
            Armed && Vector2.Distance(Flat(character.Position), centre) <= SludgeRadius + character.HitboxRadius;
    }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        utils = new UwuUtils(world);
        state = new UwuP3TitanGaolsState(world.Rng, settingsWindow.Overrides, party.Player != null ? party.PlayerRole : null);
        titan = null;
        gaols.Clear();
        sludges.Clear();
        titanChargesAt = null;
        Array.Clear(bombs);

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UwuP3TitanGaolsState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, () => utils.SpawnArenaFloor());
        world.Events.Add(0f, SpawnTitan);

        world.Events.Add(3.24f, FaceJump); // timing UNVERIFIED
        world.Events.Add(3.44f, () => TitanTargetable(false));
        world.Events.Add(3.54f, () => titan?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(5.60f, LandOnEdge);
        world.Events.Add(5.67f, Geocrush);
        world.Events.Add(5.67f, () => UwuZone.Instance.TitanArena?.PlayBeat(1, 0x2));
        world.Events.Add(9.70f, () => world.EnforceArenaBoundary(FirstShrinkRadius, ShrunkenFloorDeath));
        world.Events.Add(11.02f, () => TitanTargetable(true));
        world.Events.Add(11.05f, () => titan?.Cast(ActionId.BoulderTitan, animationLock: 2.1f));

        world.Events.Add(13.21f, () => titan?.Cast(Actions.Upheaval));
        world.Events.Add(13.49f, SpawnUpheavalBombs);
        world.Events.Add(14.14f, () => BuryBombs(0, 5));
        world.Events.Add(16.23f, () => CastBursts(0, 5));
        world.Events.Add(19.31f, RockThrowAtGaolTargets);
        world.Events.Add(19.71f, () => ClearBurstBombs(0, 5));
        world.Events.Add(21.49f, () => CastLandslide(state.FirstLandslideBait));
        utils.LandslideLines(() => titan, 21.49f, LandslideType.Normal);
        world.Events.Add(21.52f, () => SpawnBomb(5, state.SixthBomb));
        world.Events.Add(22.16f, () => BuryBombs(5, 1));
        world.Events.Add(24.26f, Jail);
        world.Events.Add(24.26f, () => CastBursts(5, 1));
        world.Events.Add(25.49f, SpawnGaols);
        world.Events.Add(25.95f, () => CastLandslide(state.SecondLandslideBait));
        utils.LandslideLines(() => titan, 25.95f, LandslideType.Normal);
        world.Events.Add(26.30f, PopInGaols);
        world.Events.Add(26.40f, CastGraniteImpact);
        world.Events.Add(27.74f, () => ClearBurstBombs(5, 1));
        world.Events.Add(30.28f, Tumult);
        world.Events.Add(31.40f, Tumult);
        world.Events.Add(32.51f, Tumult);
        world.Events.Add(33.63f, Tumult);
        world.Events.Add(34.74f, Tumult);
        world.Events.Add(35.86f, Tumult);
        world.Events.Add(36.97f, Tumult);
        world.Events.Add(38.08f, Tumult);
        world.Events.Add(39.09f, AutoAttackMainTank);
        world.Events.Add(39.09f, FollowMainTank);
        world.Events.Add(42.10f, AutoAttackMainTank);
        world.Events.Add(44.12f, () => titan?.BattleTalk(BattleTalkId.AllMyFury, 4000));
        world.Events.Add(45.00f, FailUnlessTitanAwakened);
    }

    // A gaol bursts when a bomb's Burst or another gaol's Freefire hits it.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved.Action.ActionId is not (ActionId.Burst or ActionId.Freefire)) return;
        foreach (var gaol in resolved.NpcHits.Where(npc => npc.BNpcBaseId == BNpcBaseId.GraniteGaol))
            world.Events.Add(resolved.Action.Timing.DamageDelay, () => BreakGaol(gaol));
    }

    public void Tick(float delta, float elapsed)
    {
        KillWhoeverTouchesSludge();
        ChargeTitanWhileHeTouchesSludge(elapsed);
    }

    private SimCharacter? Get(PartyRole role) => party.Get(role);

    private static bool IsJailed(SimCharacter member) => member.HasStatus(StatusId.Fetters);

    private void SpawnTitan() =>
        titan = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Titan,
            NameId: BNpcNameId.Titan,
            Level: Level,
            Targetable: true,
            LayoutId: BNpcLayoutId.Titan,
            Placement: new Placement(Vector3.Zero, MathF.PI)));

    private void TitanTargetable(bool targetable)
    {
        titan?.SetTargetable(targetable);
        if (targetable && Get(PartyRole.MainTank) is { } tank) titan?.SetTarget(tank, follow: false);
    }

    // Titan turns to the cardinal he jumps to as he leaves, the tell the party reads.
    private void FaceJump()
    {
        if (titan == null) return;
        titan.TurnTo(Facing(titan.Position, AtBearing(state.FirstJumpBearing, JumpRadius)));
    }

    private void LandOnEdge()
    {
        var edge = AtBearing(state.FirstJumpBearing, JumpRadius);
        titan?.SetPosition(new Placement(edge, FacingCentre(edge)));
    }

    private void Geocrush() => titan?.Cast(Actions.GeocrushJump);

    private void Tumult() => titan?.Cast(Actions.Tumult);

    private void FailUnlessTitanAwakened()
    {
        if (titan?.HasStatus(StatusId.Woken) == false) world.Fail("Titan wasn't awakened");
    }

    private void AutoAttackMainTank() => titan?.Cast(Actions.TitanAutoAttack, Get(PartyRole.MainTank));

    // Still touching the first gaol's sludge where he stops, so the charges keep coming.
    private void FollowMainTank() => titan?.SetTarget(Get(PartyRole.MainTank));

    // The sixth bomb's spot is the knockback's landing spot, so the party knows to leave it.
    private void SpawnUpheavalBombs()
    {
        var at = state.UpheavalBombs;
        for (var i = 0; i < at.Count; i++) SpawnBomb(i, at[i]);
    }

    private void SpawnBomb(int index, Vector3 at) =>
        bombs[index] = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BombBoulder,
            NameId: BNpcNameId.BombBoulder,
            Level: Level,
            EnemyList: EnemyListMode.Never,
            Visibility: SpawnVisibility.HiddenUntilPopIn,
            DisplayFlags: 0x40028,
            Engaged: false,
            LayoutId: BNpcLayoutId.BombBoulder,
            Placement: new Placement(at, 0f)));

    private void BuryBombs(int from, int count)
    {
        for (var i = from; i < from + count; i++)
        {
            bombs[i]?.ActorControl.PopIn(0);
            bombs[i]?.Cast(Actions.Bury);
        }
    }

    private void CastBursts(int from, int count)
    {
        for (var i = from; i < from + count; i++)
            bombs[i]?.Cast(Actions.Burst);
    }

    private void ClearBurstBombs(int from, int count)
    {
        for (var i = from; i < from + count; i++)
        {
            if (bombs[i] is not { } bomb) continue;
            bombs[i] = null;
            world.Events.Add(BombFadesAfterBurst, bomb.FadeOut);
        }
    }

    // Titan throws at the last target, his helpers at the others.
    private void RockThrowAtGaolTargets()
    {
        for (var i = 0; i < state.GaolTargets.Count; i++)
        {
            if (Get(state.GaolTargets[i]) is not { } target || !target.IsAlive()) continue;
            if (i == state.GaolTargets.Count - 1) titan?.Cast(Actions.RockThrow, target);
            else titan?.CastFromHelper(Actions.RockThrowHelper, target);
        }
    }

    private void Jail()
    {
        foreach (var role in state.GaolTargets)
        {
            if (Get(role) is not { } target || !target.IsAlive()) continue;
            target.StopMoving();
            target.AddStatus(StatusId.Fetters);
            state.Jailed.Add(role);
        }
    }

    private void SpawnGaols()
    {
        foreach (var role in state.GaolTargets)
        {
            if (Get(role) is not { } target || !IsJailed(target)) continue;
            if (world.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: BNpcBaseId.GraniteGaol,
                    NameId: BNpcNameId.GraniteGaol,
                    Level: Level,
                    Visibility: SpawnVisibility.HiddenUntilPopIn,
                    LayoutId: BNpcLayoutId.GraniteGaol,
                    Placement: new Placement(target.Position, 0f))) is not { } gaol) continue;
            gaols[gaol] = role;
        }
    }

    private void PopInGaols()
    {
        foreach (var gaol in gaols.Keys)
        {
            gaol.ActorControl.PopIn(142);
            gaol.SetTargetable(true);
        }
    }

    private void CastGraniteImpact()
    {
        foreach (var gaol in gaols.Keys) gaol.Cast(Actions.GraniteImpactLong);
    }

    // Its Freefire may burst the next gaol (OnActionResolved); the prisoner is freed ~1.1s later.
    private void BreakGaol(SimEnemy gaol)
    {
        if (!gaols.Remove(gaol, out var role)) return;
        gaol.CancelCast(CastCancelReason.Cancelled);
        gaol.Cast(Actions.Freefire);
        var at = gaol.Position;
        world.Events.Add(PrisonerFreedAfter, () => Free(role));
        world.Events.Add(SludgeSpawnsAfter, () => SpawnSludge(at));
    }

    private void SpawnSludge(Vector3 at)
    {
        var model = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.Sludge,
            Placement = new Placement(at, 0f),
            EventId = DirectorEventId,
            TargetableStatus = 5,
            Arg2 = (SludgeArg2Base + (uint)sludges.Count) << 16 | 3,
        });
        var sludge = new Sludge(Flat(at));
        sludges.Add(sludge);
        var obstacle = world.Obstacles.Add(new CircleObstacle(Flat(at), SludgeAvoidRadius));
        world.Events.Add(SludgeArmsAfter, () => sludge.Armed = true);
        world.Events.Add(SludgeFadesAfter, () =>
        {
            model?.DirectorEObjMod(7);
            sludges.Remove(sludge);
            world.Obstacles.Remove(obstacle);
        });
        world.Events.Add(SludgeDespawnsAfter, () => model?.Despawn());
    }

    // Retail's Sludge is a DoT that kills on its holder's next server tick, 0-3s in; the sim kills on contact.
    private void KillWhoeverTouchesSludge()
    {
        foreach (var member in party.ActiveMembers().ToList())
            if (member.IsAlive() && sludges.Any(s => s.Touches(member)))
                member.Die(SimCharacterDeathExtensions.Environment, "Died to Sludge (stepped into a broken gaol's puddle)");
    }

    // A stack every 3s while he touches a sludge; the tick after the third wakes him.
    private void ChargeTitanWhileHeTouchesSludge(float elapsed)
    {
        if (titan == null || titan.HasStatus(StatusId.Woken)) return;
        if (!sludges.Any(s => s.Touches(titan)))
        {
            titanChargesAt = null;
            return;
        }
        titanChargesAt ??= elapsed + TitanChargeTick;
        if (elapsed < titanChargesAt) return;
        titanChargesAt += TitanChargeTick;
        if (Charges(titan) >= ChargesToAwaken) utils.Awaken(titan, false);
        else Charge(titan);
    }

    private void Free(PartyRole role)
    {
        state.Jailed.Remove(role);
        Get(role)?.RemoveStatus(StatusId.Fetters);
    }

    private void CastLandslide(PartyRole aimAt)
    {
        if (titan == null) return;
        var rotation = Get(aimAt) is { } target
            ? Facing(titan.Position, target.Position)
            : FacingCentre(titan.Position);
        titan.SetPosition(new Placement(titan.Position, rotation));
        titan.Cast(ActionId.LandslideTitanNormal, animationLock: 2.1f);
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
