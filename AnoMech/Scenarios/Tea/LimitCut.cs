using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea;

// Cruise Chaser's limit cut: numbers 1-8 on the party, Alpha Sword from behind each odd number, the
// charge at each even one from where he last slashed. Numbers count from 1.
public sealed class LimitCut(
    SimWorld world, Func<SimEnemy?> cruiseChaser, IReadOnlyList<PartyRole> numbers, EnemyAction charge,
    Func<IReadOnlyList<SimCharacter>, SimCharacter?> deadNumberHitGoesTo)
{
    // Real moves land 0-0.135s before their warp_end. A peer's run cycle for a move in the same
    // snapshot would cut the warp_end that reveals Cruise Chaser.
    private const float WarpMoveLead = 0.1f;
    private const float WarpBehind = 2f;
    // The client plays a hit along the model's facing, so Cruise Chaser turns to each number by a
    // teleport on his own spot first; real turns land up to 0.14s before the hit.
    private const float TurnLead = 0.13f;
    private const float MarkerSpacing = 0.03f;

    private readonly SimCharacter?[] baits = new SimCharacter?[8];

    // Dead players get none; nothing removes a marker, it plays out on its own.
    public void MarkNumbersAt(float at, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        world.Events.Add(at, () => cruiseChaser()?.SetAnimationState(1, 1), file, line);
        for (var number = 1; number <= 8; number++)
        {
            var n = number;
            world.Events.Add(at + MarkerSpacing * (n - 1), () => Numbered(n)?.AttachLockonVfx(LimitCutLockons[n - 1]), file, line);
        }
    }

    // `at` is the warp_end. 2y behind the odd number, facing where they face; with that number dead
    // he warps in place.
    public void WarpBehindAt(float at, int number, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        world.Events.Add(at - WarpMoveLead, () => MoveBehind(number), file, line);
        world.Events.Add(at, () => cruiseChaser()?.PlayActionTimeline(ActionTimelineId.WarpEnd), file, line);
    }

    public void HitAt(float at, int number, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        world.Events.Add(at - TurnLead, () => TurnToBait(number), file, line);
        world.Events.Add(at, () => Hit(number), file, line);
    }

    // The limit cut's vulnerabilities go with Cruise Chaser's numbers.
    public void EndAt(float at, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => world.Events.Add(at, () =>
        {
            cruiseChaser()?.SetAnimationState(1, 0);
            foreach (var member in world.Party.ActiveMembers())
            {
                member.RemoveStatus(StatusId.MagicVulnerabilityUp);
                member.RemoveStatus(StatusId.PhysicalVulnerabilityUp);
            }
        }, file, line);

    private SimCharacter? Numbered(int number)
        => world.Party.Get(numbers[number - 1]) is { } member && member.IsAlive() ? member : null;

    private void MoveBehind(int number)
    {
        if (cruiseChaser() is not { } boss || Numbered(number) is not { } odd) return;
        var back = new Vector3(MathF.Sin(odd.Rotation), 0f, MathF.Cos(odd.Rotation)) * -WarpBehind;
        boss.SetPosition(new Placement(odd.Position + back, odd.Rotation));
    }

    // The hit goes along this facing even if the bait moves or dies before it.
    private void TurnToBait(int number)
    {
        var bait = baits[number - 1] = Numbered(number) ?? StandInFor(number);
        if (cruiseChaser() is { } boss && bait is not null)
            boss.SetRotation(boss.Placement().Face(bait.Position).Rotation);
    }

    private SimCharacter? StandInFor(int number)
    {
        var alive = world.Party.ActiveMembers().ToList();
        var target = alive.Count == 0 ? null : deadNumberHitGoesTo(alive);
        DiagnosticLog.Info($"[TeaLimitCut] Number {number} is dead; its hit goes to {(target as ISimPartyMember)?.Role.ToString() ?? "nobody"}.");
        return target;
    }

    private void Hit(int number)
    {
        if (cruiseChaser() is not { } boss || baits[number - 1] is null) return;
        boss.Cast(number % 2 == 1 ? TeaActions.AlphaSword : charge);
    }
}
