using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Scenarios.Umad.P3LimitCut;

// The live actors the AI reads (Exdeath for the wind facing, the bosses for the stack).
public sealed class UmadP3LimitCutScenarioObjects
{
    public SimEnemy? Kefka { get; set; }
    public SimEnemy? Chaos { get; set; }
    public SimEnemy? Exdeath { get; set; }
    public SimEnemy?[] Clones { get; } = new SimEnemy?[8];
}

// Per-run randomization: any start spot, either direction, the charges walking the circle the
// other way from the same start, the numbers a plain permutation, the wind crystal (and so the
// boss spot) any intercardinal. Headwind/Tailwind splits 4/4, 5/3, 3/5 or 2/6 rather than a coin per player, which
// would reach 6/2 far too often.
public sealed class UmadP3LimitCutState
{
    private readonly Rng rng = Rng.Detached;

    public UmadP3LimitCutScenarioObjects Objects { get; } = new();
    internal bool IsContinuation { get; set; }
    public PartyRole ChaosTank { get; internal set; } = PartyRole.MainTank;
    public PartyRole ExdeathTank { get; internal set; } = PartyRole.OffTank;
    public int StartSpot { get; }
    public bool Clockwise { get; }
    // Numbers[k] holds Blaster k+1.
    public IReadOnlyList<PartyRole> Numbers { get; }
    public IReadOnlyDictionary<PartyRole, Wind> Winds { get; }
    public int BossSpot { get; }
    public PartyRole BaitRole { get; }
    public ThunderIIIAssignment ThunderPlan { get; }

    // Placement walks the spot index by this each clone; the charges walk it by the opposite.
    // Clockwise as seen on the map (S -> SW -> W -> NW -> N) is a decreasing heading.
    public int PlacementStep => Clockwise ? -1 : 1;
    public int PlacementSpot(int k) => Mod8(StartSpot + k * PlacementStep);
    public int ChargeSpot(int k) => Mod8(StartSpot - k * PlacementStep);

    // Opposite its clone's charge spot (the charge falls off with distance; straight across is
    // 38y), half a step along the charge walk so as not to stand where the clone four numbers
    // later charges from (every clean pull: -23 deg CW, +22 deg CCW).
    public float SafeHeading(int k) => SpotHeading(ChargeSpot(k)) + MathF.PI - PlacementStep * (MathF.PI / 8f);

    // The cardinals and intercardinals at 20y. Spot 0..7 -> heading 0 = S, 2 = E, 4 = N, 6 = W.
    public static float SpotHeading(int spot) => spot * (MathF.PI / 4f);
    public static Vector3 SpotPosition(int spot) => OnCircle(SpotHeading(spot), 20f);
    public static string SpotName(int spot) => SpotNames[Mod8(spot)];
    public static Vector3 OnCircle(float heading, float radius) => new(MathF.Sin(heading) * radius, 0f, MathF.Cos(heading) * radius);
    private static readonly string[] SpotNames = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"];

    public int NumberOf(PartyRole role)
    {
        for (var k = 0; k < Numbers.Count; k++)
            if (Numbers[k] == role) return k + 1;
        return 0;
    }

    // The run's own dice, for the one choice made mid-run: who a dead number's clone charges.
    public T PickRandom<T>(T[] values) => rng.NextObj(values);

    public UmadP3LimitCutState(Rng rng, SimParty party, UmadP3LimitCutStateOverrides overrides, IReadOnlyDictionary<PartyRole, Wind>? carriedWinds = null)
    {
        this.rng = rng;
        StartSpot = overrides.StartSpot ?? rng.NextInt(8);
        Clockwise = overrides.Clockwise ?? rng.NextBool();
        BossSpot = overrides.BossSpot ?? rng.NextObj(1, 3, 5, 7);
        BaitRole = overrides.BaitRole ?? PartyRole.PhysRangedDps;
        ThunderPlan = overrides.ThunderPlan;

        Numbers = BuildNumbers(party.PlayerRole, overrides);
        Winds = carriedWinds ?? BuildWinds(party.PlayerRole, overrides);
    }

    // A forced number takes its slot outright; a second seat asking for the same one keeps the
    // fight's own roll, since the eight numbers are a permutation.
    private List<PartyRole> BuildNumbers(PartyRole localPlayerRole, UmadP3LimitCutStateOverrides overrides)
    {
        var numbers = rng.Shuffle(AllRoles).ToList();
        var taken = new bool[8];
        foreach (var (role, wanted) in overrides.Number.Resolve(localPlayerRole))
        {
            if (wanted is < 1 or > 8) continue;
            var index = wanted - 1;
            if (taken[index])
            {
                DiagnosticLog.Warn($"[UmadP3LimitCut] {role} asked for number {wanted}, already forced for {numbers[index]} -- leaving {role} to the roll.");
                continue;
            }
            var current = numbers.IndexOf(role);
            if (current != index) (numbers[current], numbers[index]) = (numbers[index], numbers[current]);
            taken[index] = true;
        }
        return numbers;
    }

    // Forced winds are honoured first; the rest fill toward the empirical headwind count, which
    // forcing can pull away from (four Headwinds forced leaves nothing to balance).
    private Dictionary<PartyRole, Wind> BuildWinds(PartyRole localPlayerRole, UmadP3LimitCutStateOverrides overrides)
    {
        var headwindTarget = rng.NextInt(100) switch
        {
            < 39 => 4,
            < 71 => 5,
            < 96 => 3,
            _ => 2,
        };
        var winds = new Dictionary<PartyRole, Wind>();
        foreach (var (role, wind) in overrides.Wind.Resolve(localPlayerRole)) winds[role] = wind;
        var forcedHeadwinds = winds.Count(kv => kv.Value == Wind.Headwind);
        var remaining = rng.Shuffle(AllRoles.Where(r => !winds.ContainsKey(r)).ToArray()).ToList();
        var stillHeadwind = Math.Clamp(headwindTarget - forcedHeadwinds, 0, remaining.Count);
        for (var i = 0; i < remaining.Count; i++)
            winds[remaining[i]] = i < stillHeadwind ? Wind.Headwind : Wind.Tailwind;
        return winds;
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private UmadP3LimitCutState(int startSpot, bool clockwise, IReadOnlyList<PartyRole> numbers, IReadOnlyDictionary<PartyRole, Wind> winds, int bossSpot, PartyRole baitRole, ThunderIIIAssignment thunderPlan)
    {
        StartSpot = startSpot;
        Clockwise = clockwise;
        Numbers = numbers;
        Winds = winds;
        BossSpot = bossSpot;
        BaitRole = baitRole;
        ThunderPlan = thunderPlan;
    }

    public static UmadP3LimitCutState? FromNetworkReplay(int startSpot, bool clockwise, IReadOnlyList<PartyRole> numbers, IReadOnlyList<PartyRole> headwinds, int bossSpot, PartyRole baitRole, ThunderIIIAssignment thunderPlan)
    {
        if (startSpot is < 0 or > 7 || bossSpot is < 0 or > 7) return null;
        if (numbers.Count != 8 || numbers.Distinct().Count() != 8 || numbers.Any(r => !AllRoles.Contains(r))) return null;
        if (headwinds.Any(r => !AllRoles.Contains(r)) || !AllRoles.Contains(baitRole)) return null;
        if (!Enum.IsDefined(thunderPlan)) return null;
        return new(startSpot, clockwise, numbers, AllRoles.ToDictionary(r => r, r => headwinds.Contains(r) ? Wind.Headwind : Wind.Tailwind), bossSpot, baitRole, thunderPlan);
    }

    private static int Mod8(int v) => ((v % 8) + 8) % 8;

    private static readonly PartyRole[] AllRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];
}
