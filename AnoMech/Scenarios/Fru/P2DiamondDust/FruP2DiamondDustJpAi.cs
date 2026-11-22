using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustState;

namespace AnoMech.Scenarios.Fru.P2DiamondDust;

public sealed class FruP2DiamondDustJpAi : IScenarioAi<FruP2DiamondDustState>
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    private static readonly int[] SpreadClocks = [0, 4, 6, 2, 5, 3, 7, 1];
    private const float HolyRimRadius = 18f;
    private const float PreIceRadius = 17f;
    private const float FirstTwinSlideAt = 45.05f;
    private const float SecondTwinSlideAt = 47.4f;
    private const float SlideDistance = 32f;
    private const float SlideSeconds = 1f;
    private const float PuddleRadius = 6f;
    private const float NeedleCircleRadius = 5f;
    private const float NeedleLineHalfWidth = 2.5f;
    private const float EdgeRadius = 19f;
    private const float EdgeSearchOctants = 25f / 45f;
    private const float StartingSpreadAt = 0.5f;
    private const float PhaseOneSpreadRadius = 8f;

    private FruP2DiamondDustState state = null!;
    private SimWorld world = null!;
    private readonly List<(int Wave, Vector3 Position)> puddles = [];

    public void Run(FruP2DiamondDustState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        puddles.Clear();
        var ai = new AiManager(world);
        ai.Move(StartingSpreadAt, () => ByRole(PhaseOneSpread));
        ai.Move(15.3f, () => ByRole(KickSpot), arrivalTime: 20.6f);
        ai.Move(20.7f, () => ByRole(StoneSpot), arrivalTime: 23.3f);
        ai.Move(23.35f, () => ByRole(KnockbackSpot), jitter: 0f, arrivalTime: 27.4f);
        ai.Move(29.05f, () => ByRole(NeedleSafeEdgeSpot), jitter: 0f, arrivalTime: 30.05f);
        ai.Move(30.1f, () => ByRole(role => HolyDropSpot(role, 0)), arrivalTime: 31.4f);
        for (var wave = 0; wave < HolyWaves; wave++)
        {
            var w = wave;
            world.Events.Add(HolyAt(w) - FruHolyPuddles.SnapshotBeforeHit, () => RememberHolyPuddles(w));
            ai.Move(HolyAt(w), () => ByRole(role => HolyDropSpot(role, w + 1)), arrivalTime: HolyAt(w + 1));
        }
        ai.Move(HolyAt(HolyWaves), () => ByRole(PreIceSpot), arrivalTime: 39.9f);
        world.Events.Add(39.9f, FaceAwayFromGaze);
        world.Events.Add(IceStart + 0.2f, SlideInFrontWhenBesideTheMirror);
        world.Events.Add(FirstTwinSlideAt, () => SlideAcrossTheMirror(behind: state.Stillness));
        world.Events.Add(SecondTwinSlideAt, () => SlideAcrossTheMirror(behind: !state.Stillness));
        world.Events.Add(IceEnd, puddles.Clear);
        ai.Move(IceEnd + 0.3f, () => ByRole(_ => new Vector3(0, 0, 4)));
    }

    private static Vector3? PhaseOneSpread(PartyRole role) => Point(SpreadClocks[(int)role], PhaseOneSpreadRadius);

    private bool StonesWouldLandOnTheFirstCircles => (state.FirstIcicle + (state.SupportsMarked ? 1 : 0)) % 2 == 1;

    private int SpreadClock(PartyRole role)
        => (SpreadClocks[(int)role] + (StonesWouldLandOnTheFirstCircles ? 1 : 0)) % 8;

    private Vector3? KickSpot(PartyRole role)
        => Point(SpreadClock(role), (state.Axe ? 16 : 0) + (state.Marked(role) ? 3 : 1));

    private Vector3? StoneSpot(PartyRole role)
        => Point(SpreadClock(role), state.Marked(role) ? state.Axe ? 19 : 8 : state.Axe ? 4 : 1);

    private static bool KnocksBackIntoTheWestToNortheastHalf(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    private int FirstCircleBetweenWestAndNortheast => state.FirstIcicle <= 1 ? state.FirstIcicle : state.FirstIcicle + 4;

    private int KnockbackOctant(PartyRole role)
        => (FirstCircleBetweenWestAndNortheast + (KnocksBackIntoTheWestToNortheastHalf(role) ? 0 : 4)) % 8;

    private Vector3? KnockbackSpot(PartyRole role) => Point(NeedleSafeEdgeOctant(role), 3);

    private Vector3? NeedleSafeEdgeSpot(PartyRole role) => Point(NeedleSafeEdgeOctant(role), EdgeRadius);

    private float NeedleSafeEdgeOctant(PartyRole role)
    {
        var preferred = KnockbackOctant(role);
        var stars = Enum.GetValues<PartyRole>().Where(state.Marked).Select(r => StoneSpot(r)!.Value).ToList();
        return Enumerable.Range(-50, 101)
            .Select(step => preferred + step / 50f * EdgeSearchOctants)
            .OrderByDescending(octant => NeedleClearance(Point(octant, EdgeRadius), stars))
            .ThenBy(octant => MathF.Abs(octant - preferred))
            .First();
    }

    private static float NeedleClearance(Vector3 spot, List<Vector3> stars)
    {
        var clearance = float.PositiveInfinity;
        foreach (var star in stars)
        {
            var offset = spot - star;
            clearance = MathF.Min(clearance, offset.Length() - NeedleCircleRadius);
            for (var line = 0; line < 4; line++)
            {
                var along = Point(line, 1);
                var across = MathF.Abs(offset.X * along.Z - offset.Z * along.X);
                clearance = MathF.Min(clearance, across - NeedleLineHalfWidth);
            }
        }
        return clearance;
    }

    private int OctantsAroundTheRimForHoly(PartyRole role)
        => (((state.MirrorOctant - KnockbackOctant(role)) % 8 + 8) % 8) switch
        {
            1 => -2,
            2 or 5 or 6 => 2,
            _ => 3,
        };

    private Vector3 HolyDropSpot(PartyRole role, int step)
        => Point(KnockbackOctant(role) + OctantsAroundTheRimForHoly(role) * step / (float)HolyWaves, HolyRimRadius);

    private Vector3? PreIceSpot(PartyRole role)
        => Point(KnockbackOctant(role) + OctantsAroundTheRimForHoly(role), PreIceRadius);

    private void RememberHolyPuddles(int wave)
    {
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
            if (world.Party.Get(healer) is { } member && member.IsAlive()) puddles.Add((wave, member.Position));
    }

    private IEnumerable<Vector3> PuddlesLiveAt(float time)
        => puddles.Where(p => PuddleFadesAt(p.Wave) > time).Select(p => p.Position);

    private IEnumerable<SimCharacter> Bots()
        => world.Party.ActiveMembers().Where(world.Party.IsBotDriven).ToList();

    private void FaceAwayFromGaze()
    {
        foreach (var bot in Bots())
        {
            bot.StopMoving();
            bot.Face(2 * bot.Position - state.GazePosition);
        }
    }

    private void SlideInFrontWhenBesideTheMirror()
    {
        var now = world.Events.Elapsed;
        var slideBackAt = state.Stillness ? FirstTwinSlideAt : SecondTwinSlideAt;
        foreach (var bot in Bots())
        {
            if (!IsBesideTheMirrorButNotBehind(bot.Position)
                || SlideDestination(bot.Position, behind: false, now, slideBackAt) is not { } destination) continue;
            bot.IceSlideTo(destination, SlideSeconds);
        }
    }

    private bool IsBesideTheMirrorButNotBehind(Vector3 position)
        => Vector3.Dot(position, state.MirrorPosition) > 0 && !BehindMirror(position);

    private void SlideAcrossTheMirror(bool behind)
    {
        foreach (var bot in Bots())
        {
            if (BehindMirror(bot.Position) == behind
                || SlideDestination(bot.Position, behind, world.Events.Elapsed) is not { } destination) continue;
            bot.IceSlideTo(destination, SlideSeconds);
        }
    }

    private bool BehindMirror(Vector3 position)
    {
        var offset = position - state.MirrorPosition;
        return offset.LengthSquared() > 0.001f
            && Vector3.Dot(Vector3.Normalize(offset), Vector3.Normalize(state.MirrorPosition)) > MathF.Sqrt(0.5f);
    }

    private Vector3? SlideDestination(Vector3 from, bool behind, float puddlesAt, float? slideBackAt = null)
    {
        Vector3? best = null;
        var bestMargin = float.NegativeInfinity;
        var live = PuddlesLiveAt(puddlesAt).ToList();
        for (var degree = 0; degree < 720; degree++)
        {
            var end = from + Point(degree / 90f, SlideDistance);
            if (end.Length() > 19 || BehindMirror(end) != behind) continue;
            var clearance = live.Select(p => DistanceToSegment(p, from, end) - PuddleRadius).DefaultIfEmpty(20).Min();
            if (clearance < 0.35f) continue;
            var relative = end - state.MirrorPosition;
            var backDot = Vector3.Dot(Vector3.Normalize(relative), Vector3.Normalize(state.MirrorPosition));
            var coneMargin = (behind ? backDot - MathF.Sqrt(0.5f) : MathF.Sqrt(0.5f) - backDot) * relative.Length();
            var margin = MathF.Min(MathF.Min(20 - end.Length(), clearance), coneMargin);
            if (margin > bestMargin && (slideBackAt is not { } next || SlideDestination(end, true, next) != null))
            {
                bestMargin = margin;
                best = end;
            }
        }
        return best;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var line = to - from;
        var t = line.LengthSquared() > 0 ? Math.Clamp(Vector3.Dot(point - from, line) / line.LengthSquared(), 0, 1) : 0;
        return Vector3.Distance(point, from + t * line);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
