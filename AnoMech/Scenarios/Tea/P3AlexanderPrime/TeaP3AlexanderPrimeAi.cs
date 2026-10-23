using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

public sealed class TeaP3AlexanderPrimeAi : IScenarioAi<TeaP3AlexanderPrimeState>
{
    public string Name => "NAUR";

    private const float ArenaEdge = 18.8f;
    private const float Clearance = 1.2f;
    private const float WingRadius = 17.5f;
    private const float LastOrbWait = 54.5f;
    private const float ShattersOver = 57.0f;
    private const float LastOrbShowing = 40f;
    private const float OrbContact = 1f;
    private const float OrbGoesOff = 0.1f;
    private const float HeartLandsAt = 21.5f;
    private const float HeartFlightRead = 75.9f;
    private const float ChakramClear = 4.5f;
    private const float StayInside = 18.5f;
    private const float AlexanderPrimeBlock = 6f;
    private const float CruiseChaserBlock = 5f;
    private const float RingRadius = 1.6f;

    private static readonly Vector2 AlexanderPrimeSouth = new(0f, 21.5f);
    private static readonly Vector2 EastCardinalDot = new(18.0f, -0.4f);
    private static readonly Vector2 WestCardinalDot = new(-18.0f, 0f);
    private static readonly Vector2 SouthEastDot = new(13.0f, 12.9f);
    private static readonly Vector2 SouthWestDot = new(-13.0f, 12.9f);
    private static readonly Vector2[] OrbDots = [EastCardinalDot, SouthEastDot, WestCardinalDot, SouthWestDot];
    private static readonly Vector2[] OrbShatterWaits = [new(2.6f, 13.5f), new(5.4f, 13.0f), new(-2.6f, 13.5f), new(-5.4f, 13.0f)];
    private static readonly Vector2 MainTankShatterWait = new(-3.4f, 11.5f);
    private static readonly Vector2 OffTankShatterWait = new(-1.2f, 12.0f);
    private static readonly Vector2 FlareOneSpot = new(-17.7f, -4.3f);
    private static readonly Vector2 FlareOneRetreat = new(-13.8f, -11.5f);
    private static readonly Vector2 FlareTwoWait = new(-9.0f, 3.5f);
    private static readonly Vector2 FlareTwoSpot = new(-15.1f, 0.1f);
    private static readonly Vector2 FlareTwoRetreat = new(-6.0f, -5.5f);
    private static readonly Vector2 WestCrossing = new(-12f, 3f);
    private static readonly Vector2 PartyFlareCentre = new(-15.4f, 8.9f);
    private static readonly Vector2 NorthWestDot = new(-13f, -13f);
    private static readonly Vector2 NorthEastDot = new(13f, -13f);
    private static readonly Vector2 SouthWestBait = new(-12.4f, 12.3f);
    private static readonly Vector2 SouthEastBait = new(12.4f, 12.3f);
    private static readonly Vector2 FacingNorthWest = new(-1f, -1f);
    private static readonly Vector2 FacingSouthWest = new(-1f, 1f);
    private static readonly Vector2 AddStackCentre = new(2.5f, 6.6f);
    private static readonly Vector2 AddStackAfterCruiseChaser = new(-1.5f, 6.1f);
    private static readonly Vector2 AddStackAfterBruteJustice = new(0f, -3f);
    private static readonly Vector2 MainTankAtBruteJustice = new(-3.0f, 5.4f);
    private static readonly Vector2 OffTankAtBruteJustice = new(-2.8f, 6.3f);

    private static readonly PartyRole[] CrystalPriority = [MainTank, OffTank, MeleeDpsA, MeleeDpsB, RegenHealer, ShieldHealer, PhysRangedDps, CasterDps];
    private static readonly Vector2[] CrystalBox = [new(5.4f, -17.6f), new(-5.4f, -17.6f), new(-5.2f, -7.0f), new(5.2f, -7.0f)];
    private static readonly Vector2[] CrystalShatterWaits = [new(1.6f, 16.5f), new(-1.6f, 16.5f), new(-3.6f, 15.0f), new(3.6f, 15.0f)];
    private static readonly PartyRole[] PartyOrder = [RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
    private static readonly Vector2[] PartyFlareOffsets = [new(0f, 0f), new(1.1f, 0f), new(-1.1f, 0f), new(0.55f, -0.95f), new(-0.55f, -0.95f), new(0.55f, 0.95f)];

    private TeaP3AlexanderPrimeState state = null!;
    private SimWorld world = null!;
    private AiManager ai = null!;
    private readonly Dictionary<PartyRole, int> walks = [];
    private readonly Dictionary<PartyRole, Plan> plans = [];
    private readonly Dictionary<PartyRole, Vector2?> facings = [];
    private readonly Dictionary<PartyRole, int> orbDots = [];
    private readonly Dictionary<int, float> orbsReached = [];
    private Vector2 heartLaunch;
    private int sacramentSpotSeen;

    public void Run(TeaP3AlexanderPrimeState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        ai = new AiManager(world);
        walks.Clear();
        plans.Clear();
        facings.Clear();
        orbDots.Clear();
        orbsReached.Clear();
        heartLaunch = Vector2.Zero;
        sacramentSpotSeen = SpotToward(InceptionSide(AlexanderPrimeSouth));

        if (state.Practice == P3Practice.Wormhole)
            world.Events.Add(TeaP3AlexanderPrimePractice.Of(P3Practice.Wormhole).Start, () => PlaceBotsOn(SecondSetSpot));
        At(0.3f, () => GoAll(OpenerSpot));
        Use(3.3f, OffTank, Enmity.ProvokeActionId, () => state.Objects.AlexanderPrime);
        Use(20.9f, MainTank, Enmity.ProvokeActionId, () => state.Objects.AlexanderPrime);
        At(22.4f, () =>
        {
            Go(MainTank, new Vector2(-0.3f, -5.6f));
            Go(OffTank, new Vector2(0.6f, 4.8f));
        });
        At(32.0f, () => GoAll(MidRing));

        At(36.8f, TakeOrbAndCrystalSpots);
        At(49.25f, RunCrystalHoldersTowardAlexanderPrime);
        At(50.5f, WaitForEveryOrbThenWaitOutTheShatters);
        At(60.5f, () =>
        {
            Go(MainTank, InceptionSide(FlareOneRetreat));
            Go(OffTank, InceptionSide(FlareTwoSpot));
        });
        At(62.7f, () =>
        {
            Go(MainTank, InceptionSide(InceptionWaitSpot(MainTank)));
            Go(OffTank, InceptionSide(FlareTwoRetreat), InceptionSide(InceptionWaitSpot(OffTank)));
        });
        At(64.8f, () =>
        {
            foreach (var role in PartyOrder) Go(role, InceptionSide(InceptionWaitSpot(role)));
        });
        At(72.4f, NoteWhereTheHeartStopped);
        At(73.0f, ReadTheHeartsFlightAndSplit);
        At(80.35f, TakeSwordAndJumpSpots);
        At(88.5f, StepAwayFromTheJump);
        At(91.6f, () => GoAll(SecondSetSpot));
        Use(93.5f, MainTank, Enmity.ProvokeActionId, () => state.Objects.AlexanderPrime);
        Use(111.9f, OffTank, Enmity.ProvokeActionId, () => state.Objects.AlexanderPrime);
        At(112.3f, () => Go(OffTank, new Vector2(-0.7f, -7.0f)));
        At(123.5f, () => GoAll(MidRing));

        PlanWormhole();

        At(161.2f, TakeEnumerationSpots);
        At(166.3f, () => GoAll(MegaHolySpot));
        Use(169.4f, MainTank, Enmity.ProvokeActionId, () => state.Objects.AlexanderPrime);
        At(194.8f, () => GoAll(role => AddStackSpot(role, AddStackCentre)));
        At(199.0f, () => WhenRobotLeaves(() => state.Objects.CruiseChaser, () =>
        {
            Go(MainTank, MainTankAtBruteJustice);
            Go(OffTank, OffTankAtBruteJustice);
            foreach (var role in PartyOrder) Go(role, AddStackSpot(role, AddStackAfterCruiseChaser));
        }));
        At(199.0f, () => WhenRobotLeaves(() => state.Objects.BruteJustice, () => GoAll(role => AddStackSpot(role, AddStackAfterBruteJustice))));
        At(225f, LimitBreakForDivineJudgment);
    }

    private static Vector2 OpenerSpot(PartyRole role) => role switch
    {
        MainTank => new(-6.6f, 1.0f),
        OffTank => new(0f, -6.1f),
        RegenHealer => new(-0.7f, 6.0f),
        ShieldHealer => new(-2.3f, 7.6f),
        MeleeDpsA => new(-3.9f, 7.3f),
        MeleeDpsB => new(1.1f, 7.6f),
        PhysRangedDps => new(0.5f, 5.9f),
        _ => new(2.2f, 8.4f),
    };

    private static Vector2 MidRing(PartyRole role) => Polar(45f * (int)role, RingRadius);

    private static Vector2 SecondSetSpot(PartyRole role) => role switch
    {
        MainTank => new(-0.4f, -6.2f),
        OffTank => new(7.8f, 2.2f),
        _ => OpenerSpot(role),
    };

    private static Vector2 MegaHolySpot(PartyRole role) => role switch
    {
        MainTank => new(0.1f, -5.6f),
        OffTank => new(1.0f, -6.8f),
        RegenHealer => new(-1.6f, 4.7f),
        ShieldHealer => new(-2.8f, 5.6f),
        MeleeDpsA => new(-0.4f, 5.4f),
        MeleeDpsB => new(0.9f, 6.0f),
        PhysRangedDps => new(-0.6f, 7.1f),
        _ => new(-1.8f, 6.6f),
    };

    private static Vector2 AddStackSpot(PartyRole role, Vector2 centre) => centre + Polar(45f * (int)role, 0.9f);

    private static Vector2 InceptionWaitSpot(PartyRole role) => role switch
    {
        MainTank => new(-1.5f, -9.8f),
        OffTank => new(1.5f, -9.8f),
        RegenHealer => new(-3.2f, -2.2f),
        ShieldHealer => new(3.2f, -2.2f),
        MeleeDpsA => new(-2.0f, -4.6f),
        MeleeDpsB => new(2.0f, -4.6f),
        PhysRangedDps => new(-1.0f, -1.4f),
        _ => new(1.0f, -1.4f),
    };

    private void GoAll(Func<PartyRole, Vector2> spot)
    {
        foreach (var role in PerRole.All) Go(role, spot(role));
    }

    private int? OrbOf(PartyRole role)
    {
        for (var k = 0; k < state.OrbHolders.Count; k++)
            if (state.OrbHolders[k] == role) return k;
        return null;
    }

    private Vector2 OrbSpawnOf(int k) => state.Objects.Plasmaspheres[k] is { } orb ? Flat(orb.Position) : Vector2.Zero;

    private Vector2 InceptionSide(Vector2 southLayout) => state.AlexanderPrimeNorth ? southLayout with { Y = -southLayout.Y } : southLayout;

    private int DotAcrossFromOrb(int k)
    {
        var at = OrbSpawnOf(k);
        var pair = Enumerable.Range(0, state.OrbHolders.Count).Where(i => MathF.Sign(OrbSpawnOf(i).X) == MathF.Sign(at.X)).ToList();
        var nearerToAlexanderPrime = pair.MinBy(i => Vector2.Distance(OrbSpawnOf(i), InceptionSide(AlexanderPrimeSouth))) == k;
        return (at.X < 0f ? 0 : 2) + (nearerToAlexanderPrime ? 0 : 1);
    }

    private int CrystalIndex(PartyRole role)
    {
        var order = CrystalPriority.Where(r => OrbOf(r) is null).ToList();
        return Math.Clamp(order.IndexOf(role), 0, CrystalBox.Length - 1);
    }

    private Vector2 CrystalSpot(PartyRole role) => CrystalBox[CrystalIndex(role)];

    private Vector2 ShatterWaitSpot(PartyRole role)
    {
        if (role == MainTank) return MainTankShatterWait;
        if (role == OffTank) return OffTankShatterWait;
        if (OrbOf(role) is null) return CrystalShatterWaits[CrystalIndex(role)];
        if (orbDots.TryGetValue(role, out var dot)) return OrbShatterWaits[dot];
        return OrbShatterWaits[Member(role) is { Position.X: < 0f } ? 2 : 0];
    }

    private void TakeOrbAndCrystalSpots()
    {
        foreach (var role in PerRole.All.Where(r => OrbOf(r) is null)) Go(role, InceptionSide(CrystalSpot(role)));
        TakeOrbDotsOnceEveryOrbShows();
    }

    private void TakeOrbDotsOnceEveryOrbShows()
    {
        if (state.Objects.Plasmaspheres.Any(o => o is null) && world.Events.Elapsed < LastOrbShowing)
        {
            world.Events.Add(0.1f, TakeOrbDotsOnceEveryOrbShows);
            return;
        }
        foreach (var role in PerRole.All)
        {
            if (OrbOf(role) is not { } k || state.Objects.Plasmaspheres[k] is null) continue;
            var dot = orbDots[role] = DotAcrossFromOrb(k);
            Go(role, InceptionSide(OrbDots[dot]));
        }
    }

    private void RunCrystalHoldersTowardAlexanderPrime()
    {
        foreach (var role in PerRole.All.Where(r => OrbOf(r) is null))
        {
            var spot = CrystalSpot(role);
            var wait = InceptionSide(ShatterWaitSpot(role));
            if (spot.Y < -12f) Sprint(role, InceptionSide(new Vector2(MathF.Sign(spot.X) * 1.6f, -12f)), wait);
            else Sprint(role, wait);
        }
    }

    private bool OrbPopped(int k)
    {
        if (state.Objects.Plasmaspheres[k] is not { IsActive: true, Dead: false } orb) return true;
        if (!PerRole.All.Any(r => Member(r) is { } member && Vector2.Distance(Flat(member.Position), Flat(orb.Position)) <= OrbContact))
        {
            orbsReached.Remove(k);
            return false;
        }
        if (!orbsReached.TryGetValue(k, out var since)) orbsReached[k] = since = world.Events.Elapsed;
        return world.Events.Elapsed - since >= OrbGoesOff;
    }

    private void WaitForEveryOrbThenWaitOutTheShatters()
    {
        if (world.Events.Elapsed < LastOrbWait && Enumerable.Range(0, state.OrbHolders.Count).Count(k => !OrbPopped(k)) > 0)
        {
            world.Events.Add(0.1f, WaitForEveryOrbThenWaitOutTheShatters);
            return;
        }
        foreach (var role in state.OrbHolders) Go(role, InceptionSide(ShatterWaitSpot(role)));
        TakeFlarethrowerSpotsOnceTheHeartTurnsGold();
    }

    private void TakeFlarethrowerSpotsOnceTheHeartTurnsGold()
    {
        if (world.Events.Elapsed < ShattersOver
            && state.Objects.TrueHeart is not { IsActive: false }
            && state.Objects.TrueHeart?.ShowsStatus(StatusId.EnigmaCodexHeart) != true)
        {
            world.Events.Add(0.1f, TakeFlarethrowerSpotsOnceTheHeartTurnsGold);
            return;
        }
        foreach (var role in PerRole.All) GoToFlarethrowerSpot(role);
    }

    private void GoToFlarethrowerSpot(PartyRole role)
    {
        if (Member(role) is not { } member) return;
        if (role != MainTank) Go(role, InceptionSide(role == OffTank ? FlareTwoWait : PartyFlareSpot(role)));
        else if (Flat(member.Position).X > -10f) Sprint(role, InceptionSide(WestCrossing), InceptionSide(FlareOneSpot));
        else Sprint(role, InceptionSide(FlareOneSpot));
    }

    private static Vector2 PartyFlareSpot(PartyRole role) => PartyFlareCentre + PartyFlareOffsets[Array.IndexOf(PartyOrder, role)];

    private void NoteWhereTheHeartStopped()
    {
        if (state.Objects.TrueHeart is { IsActive: true } heart) heartLaunch = Flat(heart.Position);
    }

    private static int SpotToward(Vector2 at)
    {
        var bearing = (float.RadiansToDegrees(MathF.Atan2(at.X, -at.Y)) + 360f) % 360f;
        return ((int)MathF.Round((bearing - 22.5f) / 45f) % TeaP3AlexanderPrimeState.SacramentSpots + TeaP3AlexanderPrimeState.SacramentSpots)
               % TeaP3AlexanderPrimeState.SacramentSpots;
    }

    private void ReadTheHeartsFlightAndSplit()
    {
        if (state.Objects.TrueHeart is { IsActive: true } heart && heart.ShowsStatus(StatusId.EnigmaCodexHeart))
        {
            var flight = Flat(heart.Position) - heartLaunch;
            if (flight.LengthSquared() <= 0.01f && world.Events.Elapsed < HeartFlightRead)
            {
                world.Events.Add(0.1f, ReadTheHeartsFlightAndSplit);
                return;
            }
            if (flight.LengthSquared() > 0.01f)
            {
                var along = Vector2.Normalize(flight);
                var reach = Vector2.Dot(heartLaunch, along);
                var ahead = -reach + MathF.Sqrt(reach * reach - heartLaunch.LengthSquared() + HeartLandsAt * HeartLandsAt);
                sacramentSpotSeen = SpotToward(heartLaunch + along * ahead);
            }
        }
        var sacrament = 22.5f + 45f * sacramentSpotSeen;
        var left = sacrament - 90f;
        var right = sacrament + 90f;
        SprintIfFar(MainTank, Polar(left - 3.6f, WingRadius));
        SprintIfFar(OffTank, Polar(left + 3.6f, WingRadius));
        SprintIfFar(state.SharedSentence, Polar(left, WingRadius - 1.0f));
        SprintIfFar(state.RestrainingOrderDps, Polar(right, WingRadius));
        var assaulted = state.AggravatedAssault.ToList();
        SprintIfFar(assaulted[0], Polar(right - 3.6f, WingRadius));
        SprintIfFar(assaulted[1], Polar(right + 3.6f, WingRadius));
        SprintIfFar(RegenHealer, Polar(right - 2.2f, WingRadius - 1.6f));
        SprintIfFar(ShieldHealer, Polar(right + 2.2f, WingRadius - 1.6f));
    }

    private void TakeSwordAndJumpSpots()
    {
        SprintIfFar(RegenHealer, new Vector2(0f, -1.2f));
        SprintIfFar(ShieldHealer, new Vector2(0f, 2.3f));
        SprintIfFar(state.RestrainingOrderDps, new Vector2(-3.0f, 0.2f));
        SprintIfFar(OffTank, new Vector2(18.5f, 0f));
        var waymark = new[] { new Vector2(7.5f, -0.6f), new Vector2(8.4f, 0f), new Vector2(7.5f, 0.6f), new Vector2(8.6f, -1.1f) };
        var stack = new[] { MainTank, state.SharedSentence }.Concat(state.AggravatedAssault).ToList();
        for (var k = 0; k < stack.Count; k++) SprintIfFar(stack[k], waymark[k]);
    }

    private void StepAwayFromTheJump()
    {
        var clear = new[] { new Vector2(6.6f, 4.2f), new Vector2(7.6f, 3.6f), new Vector2(5.8f, 3.4f), new Vector2(7.0f, 5.0f) };
        var stack = new[] { MainTank, state.SharedSentence }.Concat(state.AggravatedAssault).ToList();
        for (var k = 0; k < stack.Count; k++) Go(stack[k], clear[k]);
    }

    private void SprintIfFar(PartyRole role, Vector2 spot)
    {
        if (Member(role) is not { } member) return;
        if (Vector2.Distance(Flat(member.Position), spot) > 15f) Sprint(role, spot);
        else Go(role, spot);
    }

    private PartyRole Number(int number) => state.Numbers[number - 1];

    private bool FourOne => state.BruteJusticeNorthWest;
    private bool Mirrored => state.PuddlesMirrored;
    private Vector2 SouthWestSlashSpot => Mirrored ? new(-14.0f, 12.8f) : new(-13.4f, 12.6f);
    private Vector2 SouthEastChargeSpot => Mirrored ? new(13.4f, 12.6f) : new(14.0f, 12.8f);
    private Vector2 FirstSoakWest => Mirrored ? new(-17.0f, 1.0f) : new(-17.0f, -5.0f);
    private Vector2 FirstSoakEast => Mirrored ? new(17.0f, -4.7f) : new(17.0f, 1.4f);
    private Vector2 SecondSoakWest => Mirrored ? new(-15.5f, 5.0f) : new(-15.0f, -2.6f);
    private Vector2 SecondSoakEast => Mirrored ? new(16.0f, -3.6f) : new(16.0f, 3.6f);
    private Vector2 ThirdSoakWest => Mirrored ? new(-11.3f, 2.8f) : new(-11.3f, -4.2f);
    private Vector2 ThirdSoakEast => Mirrored ? new(11.4f, -4.2f) : new(11.4f, 3.2f);
    private Vector2 OneWaitsForThirdSoak => Mirrored ? new(-18.3f, -1.0f) : new(-18.4f, -2.4f);
    private Vector2 TwoWaitsForThirdSoak => Mirrored ? new(18.9f, 1.5f) : new(18.9f, -1.0f);
    private Vector2 ThreeWaitsOnTheWestWall => Mirrored ? new(-18.2f, -4.2f) : new(-17.4f, 1.0f);
    private Vector2 FourWaitsOnTheEastWall => Mirrored ? new(18.2f, 4.5f) : new(18.0f, 2.0f);
    private Vector2 SevenWaitsForSecondSoak => Mirrored ? new(-17.5f, -3.0f) : new(-18.0f, 1.5f);
    private Vector2 EightWaitsForSecondSoak => Mirrored ? new(18.5f, 2.5f) : new(18.3f, -3.0f);

    private void PlanWormhole()
    {
        At(132.95f, TakeNumberSpots);
        At(133.75f, StepOutOfChakramLines);
        At(134.8f, StepOutOfChakramLines);
        Use(137.6f, Number(1), KnockbackImmunity.ActionId);
        Use(139.0f, Number(2), KnockbackImmunity.ActionId);
        At(140.65f, () =>
        {
            Go(Number(5), FirstSoakWest);
            Go(Number(6), FirstSoakEast);
            Go(Number(7), SevenWaitsForSecondSoak);
            Go(Number(8), EightWaitsForSecondSoak);
        });
        Use(142.0f, Number(3), KnockbackImmunity.ActionId);
        At(142.1f, () =>
        {
            if (FourOne) Go(Number(4), SouthEastChargeSpot);
            else GoFacing(Number(3), FacingSouthWest, false, SouthWestSlashSpot);
        });
        Use(143.4f, Number(4), KnockbackImmunity.ActionId);
        At(Mirrored ? 146.6f : 146.25f, () => Go(Number(3), ThreeWaitsOnTheWestWall));
        Use(146.4f, Number(5), KnockbackImmunity.ActionId);
        At(146.6f, () =>
        {
            GoFacing(Number(5), FacingNorthWest, true, NorthWestDot);
            Go(Number(6), NorthEastDot);
            Go(Number(7), SecondSoakWest);
            Go(Number(8), SecondSoakEast);
            Go(Number(1), OneWaitsForThirdSoak);
            Go(Number(2), TwoWaitsForThirdSoak);
        });
        Use(147.6f, Number(6), KnockbackImmunity.ActionId);
        At(147.7f, () => Go(Number(4), FourWaitsOnTheEastWall));
        Use(150.6f, Number(7), KnockbackImmunity.ActionId);
        At(150.85f, () =>
        {
            GoFacing(Number(7), FacingSouthWest, true, SouthWestSlashSpot);
            Go(Number(8), SouthEastChargeSpot);
        });
        Use(152.0f, Number(8), KnockbackImmunity.ActionId);
        At(152.0f, () =>
        {
            Go(Number(5), MidRing(Number(5)));
            Go(Number(6), MidRing(Number(6)));
        });
        At(152.3f, () =>
        {
            Go(Number(1), ThirdSoakWest);
            Go(Number(2), ThirdSoakEast);
        });
        At(155.1f, () =>
        {
            Go(Number(1), MidRing(Number(1)));
            Go(Number(2), MidRing(Number(2)));
        });
        At(156.25f, () =>
        {
            foreach (var number in new[] { 3, 4, 7, 8 }) Go(Number(number), MidRing(Number(number)));
        });
    }

    private void TakeNumberSpots()
    {
        GoFacing(Number(1), FacingNorthWest, false, NorthWestDot);
        Go(Number(2), NorthEastDot);
        if (FourOne) GoFacing(Number(3), FacingSouthWest, false, SouthWestSlashSpot);
        else GoFacing(Number(3), FacingSouthWest, false, SouthWestBait);
        Go(Number(4), FourOne ? SouthEastBait : SouthEastChargeSpot);
        Go(Number(5), new Vector2(-15.8f, -8.6f));
        Go(Number(6), new Vector2(14.8f, -7.2f));
        Go(Number(7), new Vector2(-17.3f, -6.0f));
        Go(Number(8), new Vector2(16.6f, -5.6f));
    }

    private void StepOutOfChakramLines()
    {
        var lines = state.Objects.Chakrams.Where(c => c.IsActive)
            .Select(c => (From: Flat(c.Position), Along: new Vector2(MathF.Sin(c.Rotation), MathF.Cos(c.Rotation)))).ToList();
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            var spot = plans.TryGetValue(role, out var plan) ? plan.Destination : Flat(member.Position);
            var clear = spot;
            foreach (var (from, along) in lines)
            {
                var ahead = Vector2.Dot(clear - from, along);
                if (ahead < 0f) continue;
                var side = clear - (from + along * ahead);
                if (side.Length() >= ChakramClear) continue;
                var away = side.LengthSquared() > 1e-4f ? Vector2.Normalize(side) : new Vector2(-along.Y, along.X);
                clear = from + along * ahead + away * ChakramClear;
            }
            if (clear.Length() > StayInside) clear = Vector2.Normalize(clear) * StayInside;
            if (Vector2.Distance(clear, spot) > 0.01f) Walk(role, AiManager.RunSpeed, facings.GetValueOrDefault(role), [clear]);
        }
    }

    private void TakeEnumerationSpots()
    {
        var marked = state.Enumerations.ToList();
        var melee = marked.Where(r => r is MeleeDpsA or MeleeDpsB).ToList();
        var east = melee.Count == 1 ? melee[0] : marked.Min();
        var west = marked.First(r => r != east);
        Go(east, new Vector2(4.4f, -0.3f));
        Go(MainTank, new Vector2(5.2f, 0.5f));
        Go(OffTank, new Vector2(5.2f, -1.1f));
        Go(west, new Vector2(-5.2f, -0.3f));
        Go(RegenHealer, new Vector2(-6.0f, 0.5f));
        Go(ShieldHealer, new Vector2(-6.0f, -1.1f));
        var free = TeaP3AlexanderPrimeState.Dps.Except(marked).ToList();
        Go(free[0], new Vector2(0f, -6.0f));
        Go(free[1], new Vector2(-0.8f, -7.4f));
    }

    private void LimitBreakForDivineJudgment()
    {
        if (world.Events.Elapsed > 330f) return;
        if (state.Objects.AlexanderPrime is { IsActive: true, IsCasting: true, CastActionId: ActionId.DivineJudgment })
        {
            world.Events.Add(0.25f, LimitBreakForDivineJudgment);
            return;
        }
        world.Events.Add(7.3f, () =>
        {
            if (Member(MainTank) is { } mainTank && Member(OffTank) is { } offTank && world.Party.IsBotDriven(mainTank) && world.Party.IsBotDriven(offTank))
                world.Party.UseLimitBreak(MainTank);
        });
    }

    private void WhenRobotLeaves(Func<SimEnemy?> robot, Action then)
    {
        if (robot() is { IsActive: true, Targetable: true })
        {
            world.Events.Add(0.25f, () => WhenRobotLeaves(robot, then));
            return;
        }
        if (world.Events.Elapsed < 199.5f)
        {
            world.Events.Add(0.25f, () => WhenRobotLeaves(robot, then));
            return;
        }
        world.Events.Add(0.5f, then);
    }

    private sealed record Plan(Vector2 From, float Start, List<(Vector2 At, float Eta)> Points)
    {
        public float End => Points.Count > 0 ? Points[^1].Eta : Start;
        public Vector2 Destination => Points.Count > 0 ? Points[^1].At : From;
    }

    private void Go(PartyRole role, params Vector2[] stops) => Walk(role, AiManager.RunSpeed, null, stops);

    private void At(float time, Action action, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP3AlexanderPrimePractice.Covers(state.Practice, time)) world.Events.Add(time, action, file, line);
    }

    private void Use(float time, PartyRole role, uint actionId, Func<SimCharacter?>? target = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP3AlexanderPrimePractice.Covers(state.Practice, time)) ai.UseAction(time, role, actionId, target, file, line);
    }

    private void Use(float time, PartyRole role, Func<byte, uint> actionForJob, Func<SimCharacter?>? target = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP3AlexanderPrimePractice.Covers(state.Practice, time)) ai.UseAction(time, role, actionForJob, target, file, line);
    }

    private void PlaceBotsOn(Func<PartyRole, Vector2> spot)
    {
        foreach (var role in PerRole.All)
            if (Member(role) is { } bot && world.Party.IsBotDriven(bot))
                bot.SetPosition(new Placement(new Vector3(spot(role).X, 0f, spot(role).Y), bot.Rotation));
    }

    private void Sprint(PartyRole role, params Vector2[] stops) => Walk(role, AiManager.SprintSpeed, null, stops);

    private void GoFacing(PartyRole role, Vector2 facing, bool sprint, params Vector2[] stops)
        => Walk(role, sprint ? AiManager.SprintSpeed : AiManager.RunSpeed, facing, stops);

    private void Walk(PartyRole role, float speed, Vector2? facing, Vector2[] stops)
    {
        if (Member(role) is not { } member || stops.Length == 0) return;
        var now = world.Events.Elapsed;
        var from = Flat(member.Position);
        var path = Route(from, stops, StandingSpots(role, now), Blockers());
        var timed = Timed(from, path, now, speed);
        var walk = walks[role] = walks.GetValueOrDefault(role) + 1;
        plans[role] = new Plan(from, now, timed);
        facings[role] = facing;
        if (speed > AiManager.RunSpeed && timed.Count > 0) (member as ISimPartyMember)?.UseSprint(timed[^1].Eta - now + 1f);
        float? finalRotation = facing is { } toward ? MathF.Atan2(toward.X, toward.Y) : null;
        for (var k = 0; k < timed.Count; k++)
        {
            var to = new Vector3(timed[k].At.X, 0f, timed[k].At.Y);
            var rotation = k == timed.Count - 1 ? finalRotation : null;
            var legStart = k == 0 ? 0f : timed[k - 1].Eta - now;
            if (legStart <= 0f) member.MoveTo(to, speed, rotation);
            else
                world.Events.Add(legStart, () =>
                {
                    if (member.IsAlive() && walks.GetValueOrDefault(role) == walk) member.MoveTo(to, speed, rotation);
                });
        }
    }

    private List<Vector2> StandingSpots(PartyRole role, float now)
    {
        var spots = new List<Vector2>();
        foreach (var other in PerRole.All)
        {
            if (other == role || Member(other) is not { } member) continue;
            spots.Add(plans.TryGetValue(other, out var plan) && plan.End > now ? plan.Destination : Flat(member.Position));
        }
        return spots;
    }

    private List<(Vector2 Centre, float Radius)> Blockers()
    {
        var now = world.Events.Elapsed;
        var blockers = new List<(Vector2 Centre, float Radius)>();
        if (state.Objects.AlexanderPrime is { IsActive: true, Targetable: true } alexanderPrime)
            blockers.Add((Flat(alexanderPrime.Position), Flat(alexanderPrime.Position).Length() < 1f ? AlexanderPrimeBlock : alexanderPrime.HitboxRadius));
        if (now is >= 77.9f and < 89.5f) blockers.Add((Vector2.Zero, CruiseChaserBlock));
        foreach (var robot in new[] { state.Objects.BruteJustice, state.Objects.CruiseChaser })
            if (robot is { IsActive: true, Targetable: true }) blockers.Add((Flat(robot.Position), robot.HitboxRadius));
        return blockers;
    }

    private static List<(Vector2 At, float Eta)> Timed(Vector2 from, IReadOnlyList<Vector2> path, float start, float speed)
    {
        var timed = new List<(Vector2 At, float Eta)>();
        var eta = start;
        foreach (var point in path)
        {
            eta += Vector2.Distance(from, point) / speed;
            timed.Add((point, eta));
            from = point;
        }
        return timed;
    }

    private static List<Vector2> Route(Vector2 from, IReadOnlyList<Vector2> stops, IReadOnlyList<Vector2> spots, IReadOnlyList<(Vector2 Centre, float Radius)> circles)
    {
        var path = new List<Vector2>();
        var at = from;
        foreach (var stop in stops)
        {
            path.AddRange(PlanLeg(at, stop, spots, circles));
            at = stop;
        }
        return path;
    }

    private const float Cell = 0.5f;
    private static readonly int Cells = (int)(2f * ArenaEdge / Cell) + 1;

    private static List<Vector2> PlanLeg(Vector2 from, Vector2 to, IReadOnlyList<Vector2> spots, IReadOnlyList<(Vector2 Centre, float Radius)> circles)
    {
        var blockingSpots = spots.Where(o => Vector2.Distance(o, to) >= Clearance && Vector2.Distance(o, from) >= Clearance).ToList();
        var blockingCircles = circles.Where(c => Vector2.Distance(c.Centre, to) > c.Radius && Vector2.Distance(c.Centre, from) > c.Radius).ToList();
        if (SegmentClear(from, to, blockingSpots, blockingCircles)) return [to];
        var blocked = new bool[Cells, Cells];
        for (var i = 0; i < Cells; i++)
        for (var j = 0; j < Cells; j++)
        {
            var point = CellPoint(i, j);
            blocked[i, j] = point.Length() > ArenaEdge
                            || blockingSpots.Any(o => Vector2.Distance(point, o) < Clearance)
                            || blockingCircles.Any(c => Vector2.Distance(point, c.Centre) < c.Radius);
        }
        var start = CellOf(from);
        var goal = CellOf(to);
        blocked[start.I, start.J] = false;
        blocked[goal.I, goal.J] = false;
        var cost = new Dictionary<(int I, int J), float> { [start] = 0f };
        var came = new Dictionary<(int I, int J), (int I, int J)>();
        var open = new PriorityQueue<(int I, int J), float>();
        open.Enqueue(start, 0f);
        var found = false;
        while (open.TryDequeue(out var cell, out _))
        {
            if (cell == goal)
            {
                found = true;
                break;
            }
            for (var di = -1; di <= 1; di++)
            for (var dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0) continue;
                var next = (I: cell.I + di, J: cell.J + dj);
                if (next.I < 0 || next.J < 0 || next.I >= Cells || next.J >= Cells || blocked[next.I, next.J]) continue;
                var step = cost[cell] + (di != 0 && dj != 0 ? 1.4142f : 1f) * Cell;
                if (cost.TryGetValue(next, out var known) && known <= step) continue;
                cost[next] = step;
                came[next] = cell;
                open.Enqueue(next, step + Vector2.Distance(CellPoint(next.I, next.J), to));
            }
        }
        if (!found || start == goal) return [to];
        var cells = new List<Vector2>();
        for (var c = goal; c != start; c = came[c]) cells.Add(CellPoint(c.I, c.J));
        cells.Reverse();
        cells[^1] = to;
        var path = new List<Vector2>();
        var anchor = from;
        var k = 0;
        while (k < cells.Count)
        {
            var farthest = k;
            for (var m = cells.Count - 1; m > k; m--)
                if (SegmentClear(anchor, cells[m], blockingSpots, blockingCircles))
                {
                    farthest = m;
                    break;
                }
            path.Add(cells[farthest]);
            anchor = cells[farthest];
            k = farthest + 1;
        }
        return path;
    }

    private static (int I, int J) CellOf(Vector2 p)
        => (Math.Clamp((int)MathF.Round((p.X + ArenaEdge) / Cell), 0, Cells - 1), Math.Clamp((int)MathF.Round((p.Y + ArenaEdge) / Cell), 0, Cells - 1));

    private static Vector2 CellPoint(int i, int j) => new(i * Cell - ArenaEdge, j * Cell - ArenaEdge);

    private static bool SegmentClear(Vector2 from, Vector2 to, IReadOnlyList<Vector2> spots, IReadOnlyList<(Vector2 Centre, float Radius)> circles)
    {
        foreach (var o in spots)
            if (DistanceToSegment(o, from, to) < Clearance) return false;
        foreach (var (centre, radius) in circles)
            if (DistanceToSegment(centre, from, to) < radius) return false;
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var leg = to - from;
        var lengthSquared = leg.LengthSquared();
        var t = lengthSquared < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(point - from, leg) / lengthSquared, 0f, 1f);
        return Vector2.Distance(from + leg * t, point);
    }

    private SimCharacter? Member(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private static Vector2 Polar(float degrees, float radius) => Flat(Geometry.OnCompass(degrees, radius));

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
