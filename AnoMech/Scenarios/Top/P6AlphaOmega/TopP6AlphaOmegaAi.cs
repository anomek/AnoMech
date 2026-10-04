using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public sealed class TopP6AlphaOmegaAi : IScenarioAi<TopP6AlphaOmegaState>
{
    public string Name => "NAUR";

    private static readonly float[] PuddleBearings = [0f, 0f, 0f, 23f, 55f, 88f];
    private static readonly float[] PuddleRadii = [0f, 5.5f, 15f, 17f, 16f, 15f];
    private const float ProteanRing = 11.5f;
    private const float ClockApproachRing = 8f;
    private const float DiveTankRing = 7f;
    private const float DivePartyRing = 12f;
    private const float MeteorFlareRing = 18f;
    private const float HealerStepInRing = 8f;
    private const float SecondHealerStepInRing = 7f;
    private const float ClumpRadius = 2.5f;
    private const float OnTheSpot = 0.5f;

    private TopP6AlphaOmegaState state = null!;
    private Rng noise = null!;

    public void Run(TopP6AlphaOmegaState s, SimWorld simWorld)
    {
        state = s;
        noise = simWorld.Stream("top-p6-alpha-omega-ai");
        Bind(simWorld, state.Practice);
        var firstInvuln = state.WaveCannonInvulns[0];
        var secondInvuln = state.WaveCannonInvulns[1];

        Section(P6Practice.CosmoMemory);
        OpenUnderAlphaOmega();

        Section(P6Practice.CosmoArrowOne);
        BaitAutosBeforeCosmoArrowOne();
        DodgeCosmoArrowOne(21.57f, state.Arrows[0].InFirst);
        BaitAutosAfterCosmoDiveOne();

        Section(P6Practice.UnlimitedWaveCannonOne);
        var afterFirstPuddles = RunPuddlesAlongTheWall(55.70f, state.Exaflares[0], 56.9f, ClockApproach);

        Section(P6Practice.UnlimitedWaveCannonOne, P6Practice.WaveCannonOne);
        LeaveTheLastPuddleForWaveCannonOne(afterFirstPuddles);

        Section(P6Practice.WaveCannonOne);
        TakeWaveCannonOne(afterFirstPuddles);
        BaitAutosAfterWildCharge(91.0f);

        Section(P6Practice.CosmoArrowTwo);
        DodgeCosmoArrowTwo(98.57f, state.Arrows[1].InFirst);
        Invuln(117.67f, secondInvuln);
        LineUpForWildCharge(118.8f, secondInvuln);
        BaitAutosAfterWildCharge(125.0f);

        Section(P6Practice.UnlimitedWaveCannonTwo);
        var afterSecondPuddles = RunPuddlesAlongTheWall(132.37f, state.Exaflares[1], 133.4f, SecondDiveSpot);

        Section(P6Practice.UnlimitedWaveCannonTwo, P6Practice.CosmoDiveTwo);
        TakeCosmoDiveTwo(afterSecondPuddles);

        Section(P6Practice.CosmoDiveTwo);
        BaitAutosAfterCosmoDiveTwo(afterSecondPuddles);
        HealersDriftInAfterCosmoDiveTwo();

        Section(P6Practice.CosmoMeteor);
        HealersGatherForCosmoMeteor();
        TakeCosmoMeteor();
        ClumpUnderAlphaOmega(201.5f);

        StartPracticeHere();
    }

    private void OpenUnderAlphaOmega()
    {
        Go(0.5f, PartyRole.MainTank, new Vector2(0.5f, -7.5f));
        Go(0.5f, PartyRole.OffTank, new Vector2(8.5f, 10.5f));
        var spread = SpreadAcross(NonTanks.Length);
        for (var i = 0; i < NonTanks.Length; i++)
            Go(0.5f, NonTanks[i], Diagonal(-1, 1, 7.5f, spread[i]));
    }

    private void BaitAutosBeforeCosmoArrowOne() => Go(16.0f, PartyRole.OffTank, new Vector2(11.2f, 10.6f));

    private void DodgeCosmoArrowOne(float cast, bool inFirst)
    {
        PartyRole[] corner = [state.LeftDiveTank, .. NonTanks, state.RightDiveTank];
        var spread = SpreadAcross(corner.Length, 0.14f);
        for (var i = 0; i < corner.Length; i++)
            DodgeExasquaresInCorner(cast, inFirst, corner[i], -1, 1, spread[i]);

        var diveAt = cast + ArrowPulses[inFirst ? 5 : 4] + 1.3f;
        Go(diveAt, state.LeftDiveTank, TopCompass.Point(DiveTankRing, FirstDivePartyBearing + 135f));
        Go(diveAt, state.RightDiveTank, TopCompass.Point(DiveTankRing, FirstDivePartyBearing + 235f));
    }

    private const float FirstDivePartyBearing = 225f;

    private void DodgeExasquaresInCorner(float cast, bool inFirst, PartyRole role, int sx, int sz, float spread)
    {
        if (inFirst)
        {
            Go(cast + 1.0f, role, Diagonal(sx, sz, 6.5f, spread));
            Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 3.5f, spread));
            Go(cast + ArrowPulses[2] + 0.05f, role, Diagonal(sx, sz, 11.5f, spread));
            Go(cast + ArrowPulses[5] + 0.1f, role, Diagonal(sx, sz, 8.5f, spread));
            return;
        }
        Go(cast + 1.0f, role, Diagonal(sx, sz, 8.5f, spread));
        Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 11.5f, spread));
        Go(cast + ArrowPulses[2] + 0.1f, role, Diagonal(sx, sz, 8.5f, spread));
        Go(cast + ArrowPulses[3] + 0.1f, role, Diagonal(sx, sz, 11.5f, spread));
        Go(cast + ArrowPulses[4] + 0.1f, role, Diagonal(sx, sz, 8.5f, spread));
    }

    private void BaitAutosAfterCosmoDiveOne()
    {
        Go(45.5f, PartyRole.MainTank, new Vector2(0f, -6.5f));
        Go(45.5f, PartyRole.OffTank, TopCompass.Point(AutoBaitRing, OffTankDiveBearing(FirstDivePartyBearing) + 20f));
    }

    private float OffTankDiveBearing(float partyBearing)
        => partyBearing + (state.LeftDiveTank == PartyRole.OffTank ? 135f : 235f);

    private float RunPuddlesAlongTheWall(float cast, ExaflareSweep sweep, float gatherAt, Func<PartyRole, Vector2> afterwards)
    {
        var free = 45f * (sweep.StartOctant - sweep.Turn);
        var turn = sweep.Turn;
        At(cast, () => SeeTheExaflaresComing(cast, sweep));
        foreach (var role in PerRole.All)
            Go(gatherAt + Stagger(1.8f), role, Huddle(role, Vector2.Zero, 0.6f));
        for (var wave = 1; wave <= PuddleWaves; wave++)
        {
            var dropped = PuddleDrop(cast, wave);
            At(dropped, () => SeeThePuddlesDrop(dropped + PuddleLands));
            if (wave == PuddleWaves) break;
            var spot = TopCompass.Point(PuddleRadii[wave], free + turn * PuddleBearings[wave]);
            var nextWave = wave + 1;
            Func<PartyRole, Vector2> then = nextWave < PuddleWaves
                ? role => Huddle(role, TopCompass.Point(PuddleRadii[nextWave], free + turn * PuddleBearings[nextWave]), 0.6f)
                : afterwards;
            var nextDrop = PuddleDrop(cast, wave + 1);
            LookFirst(dropped + LookAfterDrop, PerRole.All, role => LooseHuddle(role, spot), role => Huddle(role, spot, 0.6f), nextDrop);
            LookAgain(dropped + LookAfterDrop + LookAgainAfter, PerRole.All, nextDrop, nextDrop + LookAfterDrop + MovePrompt, then);
        }
        return PuddleDrop(cast, PuddleWaves);
    }

    private static float PuddleDrop(float cast, int wave) => cast + FirstPuddle + PuddleEvery * (wave - 1);

    private Vector2 ClockApproach(PartyRole role) => TopCompass.Point(ClockApproachRing, ClockSpots[role]);

    private Vector2 SecondDiveSpot(PartyRole role)
    {
        var partyBearing = SecondDivePartyBearing();
        if (role == state.LeftDiveTank) return TopCompass.Point(DiveTankRing, partyBearing + 135f);
        if (role == state.RightDiveTank) return TopCompass.Point(DiveTankRing, partyBearing + 235f);
        return Huddle(role, TopCompass.Point(DivePartyRing, partyBearing), 0.7f);
    }

    private void LeaveTheLastPuddleForWaveCannonOne(float lastPuddle)
    {
        var protean = lastPuddle + ProteanLineUp;
        LookFirst(lastPuddle + LookAfterDrop, PerRole.All, ClockApproach, ClockApproach, protean);
        LookAgain(lastPuddle + LookAfterDrop + LookAgainAfter, PerRole.All, protean, protean + MovePrompt,
            role => TopCompass.Point(ProteanRing, ClockSpots[role]));
    }

    private void SeeTheExaflaresComing(float cast, ExaflareSweep sweep)
    {
        hazards.Clear();
        plannedWalks.Clear();
        stepGuides.Clear();
        for (var line = 0; line < ExaflareStarts.Length; line++)
            for (var blast = 0; blast < ExaflareBlasts; blast++)
                hazards.Add(new(TopCompass.Point(ExaflareFirstRadius - ExaflareStep * blast, 45f * sweep.Octant(line)), TopP6AlphaOmegaPuddleRun.ExaflareRadius,
                    cast + ExaflareStarts[line] + ExaflareFirstBlast + (blast == 0 ? 0f : ExaflareSecondBlast + ExaflareEvery * (blast - 1))));
    }

    private void SeeThePuddlesDrop(float lands)
    {
        foreach (var role in PerRole.All)
            if (world.Party.Get(role) is { } member && member.IsAlive())
                hazards.Add(new(Live(role), TopP6AlphaOmegaPuddleRun.PuddleRadius, lands));
    }

    private void LookFirst(float time, IReadOnlyList<PartyRole> roles, Func<PartyRole, Vector2> guide, Func<PartyRole, Vector2> nominal, float arriveBy)
    {
        if (!Covered)
        {
            foreach (var role in roles) Go(time, role, nominal(role));
            return;
        }
        world.Events.Add(time, () =>
        {
            foreach (var role in roles)
            {
                if (!Drives(role) || world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
                var spot = stepGuides[role] = guide(role);
                ChooseAndWalk(role, spot, arriveBy, time + LookAgainAfter + MovePrompt, spot, Hesitation());
            }
        });
    }

    private void LookAgain(float time, IReadOnlyList<PartyRole> roles, float arriveBy, float nextDeparts, Func<PartyRole, Vector2> then)
    {
        At(time, () =>
        {
            var now = world.Events.Elapsed;
            hazards.RemoveAll(h => h.Lands <= now);
            foreach (var role in roles)
            {
                if (!Drives(role) || world.Party.Get(role) is not { } member || !member.IsAlive() || !stepGuides.TryGetValue(role, out var guide)) continue;
                var planned = PlannedWalk(role, now);
                var current = ActualWalk(role, now);
                var afterwards = then(role);
                var reaction = ReactionTime();
                var plan = TopP6AlphaOmegaPuddleRun.Choose(current, now + reaction + MovePrompt, guide, arriveBy, nextDeparts, afterwards, hazards);
                var clear = TopP6AlphaOmegaPuddleRun.StaysClear(planned, nextDeparts, afterwards, hazards);
                var closer = Vector2.Distance(plan.Spot, guide) < Vector2.Distance(planned.To, guide) - 1f;
                if (clear && (!plan.Safe || !closer)) continue;
                WalkTo(role, plan.Spot, reaction + plan.Delay, current, now);
            }
        });
    }

    private void ChooseAndWalk(PartyRole role, Vector2 guide, float arriveBy, float nextDeparts, Vector2 afterwards, float hesitation)
    {
        var now = world.Events.Elapsed;
        hazards.RemoveAll(h => h.Lands <= now);
        var current = ActualWalk(role, now);
        var plan = TopP6AlphaOmegaPuddleRun.Choose(current, now + MovePrompt, guide, arriveBy, nextDeparts, afterwards, hazards);
        var hesitant = TopP6AlphaOmegaPuddleRun.Choose(current, now + hesitation + MovePrompt, guide, arriveBy, nextDeparts, afterwards, hazards);
        if (hesitant.Safe && Vector2.Distance(hesitant.Spot, plan.Spot) <= 1f)
            plan = hesitant;
        else
            hesitation = 0f;
        WalkTo(role, plan.Spot, hesitation + plan.Delay, current, now);
    }

    private void WalkTo(PartyRole role, Vector2 spot, float after, TopP6AlphaOmegaPuddleRun.Walk current, float now)
    {
        var order = walkOrders[role] = walkOrders.GetValueOrDefault(role) + 1;
        var departs = now + after + MovePrompt;
        plannedWalks[role] = new TopP6AlphaOmegaPuddleRun.Walk(current.At(departs), departs, spot, AiManager.RunSpeed);
        world.Events.Add(after, () =>
        {
            if (walkOrders[role] == order) ai.Move(MovePrompt, () => AiMove.Single(role, spot), jitter: SpotJitter);
        });
    }

    private TopP6AlphaOmegaPuddleRun.Walk PlannedWalk(PartyRole role, float now)
        => plannedWalks.TryGetValue(role, out var walk) && walk.Departs > now ? walk : ActualWalk(role, now);

    private TopP6AlphaOmegaPuddleRun.Walk ActualWalk(PartyRole role, float now)
    {
        var here = Live(role);
        if (plannedWalks.TryGetValue(role, out var walk) && walk.Departs <= now && Vector2.Distance(walk.At(now), here) < 1f)
            return new TopP6AlphaOmegaPuddleRun.Walk(here, now, walk.To, walk.Speed);
        return TopP6AlphaOmegaPuddleRun.Walk.Standing(here, AiManager.RunSpeed);
    }

    private float Hesitation()
        => noise.NextFloat(0f, 1f) < LongHesitationChance ? noise.NextFloat(0.25f, 0.6f) : noise.NextFloat(0f, 0.25f);

    private float ReactionTime() => noise.NextFloat(0f, 0.15f);

    private Vector2 LooseHuddle(PartyRole role, Vector2 centre)
        => centre + TopCompass.Point(noise.NextFloat(0.3f, 1.1f), Array.IndexOf(PerRole.All.ToArray(), role) * 45f + noise.NextFloat(-20f, 20f));

    private void TakeWaveCannonOne(float lastPuddle)
    {
        foreach (var role in PerRole.All)
            Go(lastPuddle + ProteanLineUp, role, TopCompass.Point(ProteanRing, ClockSpots[role]));
        Invuln(83.28f, state.WaveCannonInvulns[0]);
        LineUpForWildCharge(84.3f, state.WaveCannonInvulns[0]);
    }

    private float SecondDivePartyBearing()
    {
        var sweep = state.Exaflares[1];
        return 45f * (sweep.StartOctant - sweep.Turn) + sweep.Turn * 133f;
    }

    private void TakeCosmoDiveTwo(float lastPuddle)
    {
        var settled = lastPuddle + DiveSettled;
        LookFirst(lastPuddle + 0.07f, NonTanks, SecondDiveSpot, SecondDiveSpot, settled);
        LookAgain(lastPuddle + 0.07f + LookAgainAfter, NonTanks, settled, settled + MovePrompt, SecondDiveSpot);
        LookFirst(lastPuddle + 1.0f, TankSeats, SecondDiveSpot, SecondDiveSpot, settled);
        LookAgain(lastPuddle + 1.0f + LookAgainAfter, TankSeats, settled, settled + MovePrompt, SecondDiveSpot);
        WalkOnToTheSpotOnceTheyLand(settled, PerRole.All, SecondDiveSpot);
    }

    private void WalkOnToTheSpotOnceTheyLand(float time, IReadOnlyList<PartyRole> roles, Func<PartyRole, Vector2> spot)
        => At(time, () =>
        {
            var now = world.Events.Elapsed;
            foreach (var role in roles)
            {
                if (!Drives(role) || world.Party.Get(role) is not { } member || !member.IsAlive()) continue;
                var destination = spot(role);
                if (Vector2.Distance(PlannedWalk(role, now).To, destination) > OnTheSpot)
                    WalkTo(role, destination, 0f, ActualWalk(role, now), now);
            }
        });

    private void BaitAutosAfterCosmoDiveTwo(float lastPuddle)
    {
        var partyBearing = SecondDivePartyBearing();
        Go(159.3f, PartyRole.MainTank, TopCompass.Point(6.5f, partyBearing + 180f));
        Go(159.8f, PartyRole.OffTank, TopCompass.Point(AutoBaitRing, OffTankDiveBearing(partyBearing) + 20f));
    }

    private void HealersDriftInAfterCosmoDiveTwo()
    {
        var partyBearing = SecondDivePartyBearing();
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
            Go(159.7f + Stagger(1.5f), healer, Huddle(healer, TopCompass.Point(HealerStepInRing, partyBearing), 0.7f));
    }

    private void HealersGatherForCosmoMeteor()
    {
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
            Go(171.2f + Stagger(0.8f), healer, Huddle(healer, Vector2.Zero, 0.7f));
    }

    private void TakeCosmoMeteor()
    {
        foreach (var role in PerRole.All.Except([PartyRole.RegenHealer, PartyRole.ShieldHealer]))
            Go(173.0f + Stagger(1.2f), role, Huddle(role, Vector2.Zero, 0.7f));
        foreach (var role in PerRole.All)
            Go(177.8f + (role == PartyRole.CasterDps ? 0f : Stagger(0.5f)), role, MeteorSpot(role));
        Go(184.2f, state.MeteorMiddleHealer, MeteorSpot(state.MeteorMiddleHealer) * (5.6f / MeteorSpot(state.MeteorMiddleHealer).Length()));
        var otherHealer = state.MeteorMiddleHealer == PartyRole.RegenHealer ? PartyRole.ShieldHealer : PartyRole.RegenHealer;
        Go(190.8f, otherHealer, MeteorSpot(otherHealer) * (SecondHealerStepInRing / MeteorSpot(otherHealer).Length()));
        SplitFlaresFromStack(193.5f);
    }

    private static Vector2 MeteorSpot(PartyRole role) => role switch
    {
        PartyRole.PhysRangedDps => new Vector2(0f, -15.5f),
        PartyRole.MainTank => new Vector2(10f, -10f),
        PartyRole.ShieldHealer => new Vector2(14f, 0f),
        PartyRole.MeleeDpsB => new Vector2(10f, 10f),
        PartyRole.OffTank => new Vector2(0f, 14f),
        PartyRole.MeleeDpsA => new Vector2(-10.5f, 10.5f),
        PartyRole.RegenHealer => new Vector2(-12f, 0f),
        _ => new Vector2(-10.5f, -10.5f),
    };

    private void SplitFlaresFromStack(float time)
    {
        var flares = state.FlareTargets;
        var stackSouth = flares.Contains(PartyRole.PhysRangedDps);
        var stackBearing = stackSouth ? 180f : 0f;
        var stackSpot = TopCompass.Point(stackSouth ? 14.5f : 15f, stackBearing);
        var freeCardinals = new[] { 0f, 90f, 180f, 270f }.Where(c => c != stackBearing).ToList();
        var movingFlares = flares.Where(f => !(stackSouth && f == PartyRole.PhysRangedDps)).ToList();
        if (stackSouth) freeCardinals.Remove(0f);
        List<(PartyRole Role, float Cardinal)> chosen = [];
        At(time, () =>
        {
            chosen = NearestCardinals(movingFlares, freeCardinals);
            foreach (var (role, cardinal) in chosen)
                Go(0f, role, FlareSpot(cardinal));
        });
        for (var look = LookEvery; look <= 3f + 0.01f; look += LookEvery)
            At(time + look, () => chosen = YieldToFlaresNotOurs(chosen, freeCardinals));
        foreach (var role in PerRole.All.Where(r => !flares.Contains(r) && r != PartyRole.PhysRangedDps))
            Go(time, role, Huddle(role, stackSpot, 0.8f));
    }

    private static Vector2 FlareSpot(float cardinal) => TopCompass.Point(MeteorFlareRing, cardinal);

    private List<(PartyRole Role, float Cardinal)> YieldToFlaresNotOurs(List<(PartyRole Role, float Cardinal)> chosen, IReadOnlyList<float> cardinals)
    {
        foreach (var (theirs, theirCardinal) in chosen.Where(flare => !Drives(flare.Role)).ToList())
        {
            var at = Live(theirs);
            var heading = cardinals.MinBy(cardinal => Vector2.Distance(at, FlareSpot(cardinal)));
            if (heading == theirCardinal || Vector2.Distance(at, FlareSpot(heading)) + YieldMargin >= Vector2.Distance(at, FlareSpot(theirCardinal))) continue;
            var ours = chosen.FindIndex(flare => flare.Cardinal == heading && Drives(flare.Role));
            if (ours < 0) continue;
            var bot = chosen[ours].Role;
            chosen[ours] = (bot, theirCardinal);
            chosen[chosen.FindIndex(flare => flare.Role == theirs)] = (theirs, heading);
            Go(0f, bot, FlareSpot(theirCardinal));
        }
        return chosen;
    }

    private List<(PartyRole Role, float Cardinal)> NearestCardinals(IReadOnlyList<PartyRole> flares, IReadOnlyList<float> cardinals)
    {
        var best = new List<(PartyRole, float)>();
        var bestCost = float.MaxValue;
        foreach (var order in Permutations(cardinals.ToList()))
        {
            var cost = 0f;
            var pairs = new List<(PartyRole, float)>();
            for (var i = 0; i < flares.Count && i < order.Count; i++)
            {
                cost += Vector2.Distance(Live(flares[i]), FlareSpot(order[i]));
                pairs.Add((flares[i], order[i]));
            }
            if (cost >= bestCost) continue;
            bestCost = cost;
            best = pairs;
        }
        return best;
    }

    private static IEnumerable<List<float>> Permutations(List<float> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }
        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var tail in Permutations(rest))
                yield return [items[i], .. tail];
        }
    }

    private void ClumpUnderAlphaOmega(float time)
    {
        foreach (var role in PerRole.All)
            Go(time + Stagger(2.5f), role, Huddle(role, Vector2.Zero, ClumpRadius));
    }

    private static float[] SpreadAcross(int count, float step = 0.4f)
        => Enumerable.Range(0, count).Select(i => (i - (count - 1) / 2f) * step).ToArray();

    private SimWorld world = null!;
    private AiManager ai = null!;

    private P6Practice practice;
    private float practiceStart;
    private P6Practice[] sections = [];
    private readonly Dictionary<PartyRole, (float Time, Vector2 Spot)> startSpots = new();

    private void Bind(SimWorld simWorld, P6Practice practiced = P6Practice.WholePhase)
    {
        world = simWorld;
        ai = new AiManager(world);
        practice = practiced;
        practiceStart = TopP6AlphaOmegaPractice.Of(practiced).Start;
        sections = [practiced];
        startSpots.Clear();
        if (practiceStart > 0f) world.Events.Add(practiceStart, PlaceBotsForPractice);
    }

    private void Section(params P6Practice[] mechanics) => sections = mechanics;

    private bool Covered => practice == P6Practice.WholePhase || sections.Contains(practice);

    private void StartPracticeHere() => sections = [practice];

    private void PlaceBotsForPractice()
    {
        foreach (var (role, (_, spot)) in startSpots)
            if (world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive())
                bot.SetPosition(new Placement(new Vector3(spot.X, 0f, spot.Y), bot.Rotation));
    }

    private void At(float time, Action action)
    {
        if (Covered) world.Events.Add(time, action);
    }

    private void Invuln(float time, PartyRole tank)
    {
        if (Covered) ai.UseInvuln(time, tank);
    }

    private static readonly Dictionary<PartyRole, float> ClockSpots = new()
    {
        [PartyRole.MainTank] = 0f, [PartyRole.PhysRangedDps] = 45f, [PartyRole.ShieldHealer] = 90f, [PartyRole.MeleeDpsB] = 135f,
        [PartyRole.OffTank] = 180f, [PartyRole.MeleeDpsA] = 225f, [PartyRole.RegenHealer] = 270f, [PartyRole.CasterDps] = 315f,
    };

    private static readonly PartyRole[] TankSeats = [PartyRole.MainTank, PartyRole.OffTank];
    private static readonly PartyRole[] NonTanks =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    private static readonly Dictionary<PartyRole, PartyRole> SupportPartnerOf = new()
    {
        [PartyRole.PhysRangedDps] = PartyRole.MainTank, [PartyRole.MeleeDpsB] = PartyRole.ShieldHealer,
        [PartyRole.MeleeDpsA] = PartyRole.OffTank, [PartyRole.CasterDps] = PartyRole.RegenHealer,
    };

    private static readonly float[] ArrowPulses = [7.97f, 10.06f, 12.07f, 14.07f, 16.08f, 18.08f, 20.09f, 22.10f];
    private const int PuddleWaves = 6;
    private const float FirstPuddle = 10.033f;
    private const float PuddleEvery = 2.005f;
    private const float PuddleLands = 2.987f;
    private const float LookAfterDrop = 0.05f;
    private const float LookAgainAfter = 1f;
    private const float MovePrompt = 0.3f;
    private const float SpotJitter = 0.15f;
    private const float LongHesitationChance = 0.15f;
    private const float ProteanLineUp = 3.1f;
    private const float DiveSettled = 3f;
    private static readonly float[] ExaflareStarts = [0f, 1.025f, 2.005f, 3.03f];
    private const int ExaflareBlasts = 7;
    private const float ExaflareFirstRadius = 24f;
    private const float ExaflareStep = 8f;
    private const float ExaflareFirstBlast = 11.995f;
    private const float ExaflareSecondBlast = 1.114f;
    private const float ExaflareEvery = 0.998f;
    private readonly List<TopP6AlphaOmegaPuddleRun.Hazard> hazards = [];
    private readonly Dictionary<PartyRole, TopP6AlphaOmegaPuddleRun.Walk> plannedWalks = new();
    private readonly Dictionary<PartyRole, int> walkOrders = new();
    private readonly Dictionary<PartyRole, Vector2> stepGuides = new();
    private const float AutoBaitRing = 15.5f;
    private const float LookEvery = 0.6f;
    private const float YieldMargin = 1.5f;

    private static PartyRole OtherTank(PartyRole tank) => tank == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;

    private void LineUpForWildCharge(float time, PartyRole invulning)
    {
        Go(time, invulning, new Vector2(0f, 5.4f));
        Go(time, OtherTank(invulning), new Vector2(0f, 7.0f));
        var slots = new[] { new Vector2(0f, 9.3f), new Vector2(0.4f, 9.8f), new Vector2(-0.4f, 10.0f), new Vector2(0f, 10.5f), new Vector2(0.4f, 10.9f), new Vector2(-0.4f, 11.2f) };
        var i = 0;
        foreach (var role in NonTanks)
            Go(time, role, slots[i++]);
    }

    private void BaitAutosAfterWildCharge(float time)
    {
        Go(time, PartyRole.MainTank, new Vector2(0f, -6.5f));
        Go(time + 0.5f, PartyRole.OffTank, TopCompass.Point(AutoBaitRing, 138f));
    }

    private void DodgeCosmoArrowTwo(float cast, bool inFirst)
    {
        foreach (var (dps, support) in SupportPartnerOf)
        {
            var bearing = ClockSpots[dps];
            var sx = MathF.Sign(MathF.Sin(bearing * MathF.PI / 180f));
            var sz = -MathF.Sign(MathF.Cos(bearing * MathF.PI / 180f));
            DodgeExasquaresAsDps(cast, inFirst, dps, (int)sx, (int)sz);
            DodgeExasquaresAsSupport(cast, inFirst, support, (int)sx, (int)sz, ClockSpots[support]);
        }
    }

    private void DodgeExasquaresAsDps(float cast, bool inFirst, PartyRole role, int sx, int sz)
    {
        var proteanOne = cast + 17.14f;
        if (inFirst)
        {
            Go(cast + 1.0f, role, Diagonal(sx, sz, 6.5f, 0f));
            Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 3.5f, 0f));
            Go(cast + ArrowPulses[2] + 0.05f, role, Diagonal(sx, sz, 11.5f, 0f));
            Go(cast + ArrowPulses[5] + 0.1f, role, Diagonal(sx, sz, 8.5f, 0f));
            return;
        }
        Go(cast + 1.0f, role, Diagonal(sx, sz, 8.5f, 0f));
        Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 11.5f, 0f));
        Go(cast + ArrowPulses[2] + 0.1f, role, Diagonal(sx, sz, 8.5f, 0f));
        Go(cast + ArrowPulses[3] + 0.1f, role, Diagonal(sx, sz, 11f, 0f));
        Go(proteanOne + 0.05f, role, Diagonal(sx, sz, 8f, 0f));
    }

    private void DodgeExasquaresAsSupport(float cast, bool inFirst, PartyRole role, int sx, int sz, float cardinal)
    {
        var proteanOne = cast + 17.14f;
        var toCardinal = TopCompass.Point(1f, cardinal);
        var cx = (int)MathF.Round(toCardinal.X);
        var cz = (int)MathF.Round(toCardinal.Y);
        Vector2 Beside(float along, float outward) => cx != 0 ? new(cx * outward, sz * along) : new(sx * along, cz * outward);
        if (inFirst)
        {
            Go(cast + 1.0f, role, Beside(6.5f, 7f));
            Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 3.5f, 0f));
            Go(cast + ArrowPulses[2] + 0.05f, role, Beside(6f, 12f));
            Go(cast + ArrowPulses[4] + 0.1f, role, TopCompass.Point(13.5f, cardinal));
            Go(cast + ArrowPulses[5] + 0.1f, role, TopCompass.Point(8.5f, cardinal));
            return;
        }
        Go(cast + 1.0f, role, Beside(7f, 8f));
        Go(cast + ArrowPulses[0] + 0.1f, role, Diagonal(sx, sz, 11.5f, 0f));
        Go(cast + ArrowPulses[2] + 0.1f, role, Beside(5.5f, 9f));
        Go(cast + ArrowPulses[3] + 0.1f, role, TopCompass.Point(12f, cardinal));
        Go(proteanOne + 0.05f, role, TopCompass.Point(8f, cardinal));
    }

    private static Vector2 Diagonal(int sx, int sz, float a, float spread)
        => new(sx * a + spread * sz / MathF.Sqrt(2f), sz * a - spread * sx / MathF.Sqrt(2f));

    private static Vector2 Huddle(PartyRole role, Vector2 centre, float radius)
    {
        var index = Array.IndexOf(PerRole.All.ToArray(), role);
        return centre + TopCompass.Point(radius, index * 45f);
    }

    private void Go(float time, PartyRole role, Vector2 spot, float jitter = 0.15f)
    {
        if (Covered) ai.Move(time + MovePrompt, () => AiMove.Single(role, spot), jitter: jitter);
        else if (time < practiceStart && (!startSpots.TryGetValue(role, out var known) || time >= known.Time)) startSpots[role] = (time, spot);
    }

    private float Stagger(float spread) => noise.NextFloat(0f, spread);

    private bool Drives(PartyRole role)
        => world.Party.Get(role) is { } member && (member is SimPartyNpc || (ReferenceEquals(member, world.Party.Player) && DebugBotControl.Enabled));

    private Vector2 Live(PartyRole role)
        => world.Party.Get(role) is { } member ? new Vector2(member.Position.X, member.Position.Z) : Vector2.Zero;
}
