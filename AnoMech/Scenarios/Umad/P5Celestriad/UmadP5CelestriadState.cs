using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Scenarios.Umad.P5Celestriad.UmadP5CelestriadConstants;

namespace AnoMech.Scenarios.Umad.P5Celestriad;

// Stable element identities. Physical sector order is rolled separately for each run.
public sealed record CelestriadElement(
    uint TowerSoakedActionId,
    uint TowerFailedActionId,
    DamageType DamageType,
    ushort VulnUpStatusId,
    uint TowerEObjId)
{
    public static readonly CelestriadElement Fire =
        new(CelestriadActionId.FireIII, CelestriadActionId.StardustFireIII,
            DamageType.Fire, CelestriadStatusId.FireResistanceDownII, CelestriadTowerEObjId.Fire);
    public static readonly CelestriadElement Lightning =
        new(CelestriadActionId.ThunderIII, CelestriadActionId.StardustThunderIII,
            DamageType.Lightning, UmadConstants.StatusId.LightningResistanceDownII, CelestriadTowerEObjId.Lightning);
    public static readonly CelestriadElement Ice =
        new(CelestriadActionId.BlizzardIII, CelestriadActionId.StardustBlizzardIII,
            DamageType.Ice, CelestriadStatusId.IceResistanceDownII, CelestriadTowerEObjId.Ice);
}

public sealed record CatastrophicChoice(uint CastActionId, uint ResolveActionId)
{
    public static readonly CatastrophicChoice Aero =
        new(CelestriadActionId.CatastrophicChoiceAero, CelestriadActionId.CatastrophicChoiceAeroResolution);
    public static readonly CatastrophicChoice Earth =
        new(CelestriadActionId.CatastrophicChoiceEarth, CelestriadActionId.CatastrophicChoiceEarthResolution);
}

// One of the 9 fixed towers, spawned once for the whole mechanic; position never changes.
public sealed record CelestriadTower(CelestriadElement Element, int SubIndex, Vector3 Position);

// Per-run randomization: the three sectors' elements, each party role's permanent debuff
// (an element or "free"), which element doubles up on each set, which ring towers are active,
// and (sets 0 and 2 only, the 1st and 3rd soaks) whether that set's single Catastrophic Choice
// is Aero (green, safe toward centre) or Earth (brown, safe away from centre). ElementForSet
// derives each role's actual per-set soak target from its debuff.
//
// Deliberately has no notion of which specific player goes to which specific active tower
// within a doubled element's pair, or who's "responsible" for a tower failing: that's a
// strategy decision (the AI's job, see UmadP5CelestriadAi) and a scenario-ruleset resolution
// decision (the scenario's job, see UmadP5CelestriadScenario.ResolveSet), not a fact of the
// randomization itself. State only ever exposes what's actually random.
public sealed class UmadP5CelestriadState
{
    private readonly Rng rng = new();

    // Clockwise sector shift from a debuffed player's own element in this run's layout:
    // set 0 -> next sector, set 1 -> sector after that, set 2 -> own element again.
    // Since this is a fixed shift of a 3-element cycle, it's automatically a bijection each set
    // (exactly one debuff group per element) and never repeats an element across the 3 sets.
    private static readonly int[] SetOffset = { 1, 2, 0 };

    // Stable tower/wire IDs only. Strategy rotation must use TowerElementOrder.
    private static readonly CelestriadElement[] Elements =
        { CelestriadElement.Fire, CelestriadElement.Lightning, CelestriadElement.Ice };

    private const int SetCount = 3;
    private const int SubTowersPerElement = 3;

    public IReadOnlyDictionary<PartyRole, CelestriadElement?> PlayerDebuffElement { get; }
    public IReadOnlyList<CelestriadElement> DoubleElement { get; }
    // Clockwise sectors: north-east (20/60/100 degrees), south (140/180/220),
    // north-west (260/300/340). Rolled once; all three activation waves share it.
    public IReadOnlyList<CelestriadElement> TowerElementOrder { get; }
    public IReadOnlyList<CelestriadTower> AllTowers { get; }
    // Each entry is an index into AllTowers. AllTowers has a fixed order for the lifetime of this state.
    public IReadOnlyList<IReadOnlyList<int>> SetActiveTowers { get; }
    public IReadOnlyList<CatastrophicChoice?> AeroVariant { get; }

    // The element this role should physically soak at this set. NOT the same as their permanent
    // debuff except in set 2. Free (undebuffed) players always fill in for the doubled element.
    public CelestriadElement ElementForSet(PartyRole role, int set) =>
        PlayerDebuffElement[role] is { } own
            ? TowerElementOrder[(TowerElementOrder.ToList().IndexOf(own) + SetOffset[set]) % TowerElementOrder.Count]
            : DoubleElement[set];

    public UmadP5CelestriadState(SimParty party, UmadP5CelestriadStateOverrides overrides)
    {
        // Each element doubles exactly once across the 3 sets: a shuffled permutation
        // guarantees that instead of leaving it to independent per-set coin flips.
        DoubleElement = overrides.DoubleOrder switch
        {
            CelestriadDoubleOrder.FireIceLightning => [CelestriadElement.Fire, CelestriadElement.Ice, CelestriadElement.Lightning],
            CelestriadDoubleOrder.FireLightningIce => [CelestriadElement.Fire, CelestriadElement.Lightning, CelestriadElement.Ice],
            CelestriadDoubleOrder.IceFireLightning => [CelestriadElement.Ice, CelestriadElement.Fire, CelestriadElement.Lightning],
            CelestriadDoubleOrder.IceLightningFire => [CelestriadElement.Ice, CelestriadElement.Lightning, CelestriadElement.Fire],
            CelestriadDoubleOrder.LightningFireIce => [CelestriadElement.Lightning, CelestriadElement.Fire, CelestriadElement.Ice],
            CelestriadDoubleOrder.LightningIceFire => [CelestriadElement.Lightning, CelestriadElement.Ice, CelestriadElement.Fire],
            _ => rng.Shuffle(CelestriadElement.Fire, CelestriadElement.Ice, CelestriadElement.Lightning),
        };

        PlayerDebuffElement = AssignDebuffs(party, overrides);

        TowerElementOrder = rng.Shuffle(CelestriadElement.Fire, CelestriadElement.Lightning, CelestriadElement.Ice);
        AllTowers = BuildAllTowers();

        var setActive = new List<IReadOnlyList<int>>(3);
        var aero = new List<CatastrophicChoice?>(3);
        for (var set = 0; set < 3; set++)
        {
            var active = new List<int>(4);
            foreach (var element in Elements)
            {
                var isDouble = element == DoubleElement[set];
                // Which sub-towers light up is random; sorted ascending so a doubled element's
                // pair always lists in a stable, deterministic clockwise order for whoever reads
                // "first" vs "second" out of it (the AI, when deciding who goes where).
                var subs = rng.Shuffle(0, 1, 2).Take(isDouble ? 2 : 1).OrderBy(s => s).ToArray();
                var elementStart = Array.IndexOf(Elements, element) * 3;
                active.AddRange(subs.Select(s => elementStart + s));
            }
            setActive.Add(active);
            aero.Add(ResolveAero(set, overrides));
        }
        SetActiveTowers = setActive;
        AeroVariant = aero;
    }

    // -1 none, 0 Aero, 1 Earth -- the wire form of AeroVariant, both ways.
    public static int ChoiceIndex(CatastrophicChoice? choice)
        => choice is null ? -1 : choice == CatastrophicChoice.Aero ? 0 : 1;

    private static CatastrophicChoice? Choice(int index)
        => index < 0 ? null : index == 0 ? CatastrophicChoice.Aero : CatastrophicChoice.Earth;

    // Stable element/wire identity, independent of sector position; -1 for a free player.
    public static int ElementIndex(CelestriadElement? element)
        => element is null ? -1 : Array.IndexOf(Elements, element);

    private IReadOnlyList<CelestriadTower> BuildAllTowers()
    {
        var towers = new List<CelestriadTower>(Elements.Length * SubTowersPerElement);
        foreach (var element in Elements)
            for (var sub = 0; sub < SubTowersPerElement; sub++)
                towers.Add(new CelestriadTower(element, sub, TowerPosition(element, sub)));
        return towers;
    }

    // Two seats carry each element and two carry none. Forced seats draw from that pool first;
    // one asking for a debuff the pool has run out of keeps the roll rather than displacing
    // someone.
    private IReadOnlyDictionary<PartyRole, CelestriadElement?> AssignDebuffs(SimParty party, UmadP5CelestriadStateOverrides overrides)
    {
        var pool = new List<CelestriadElement?>
        {
            CelestriadElement.Fire, CelestriadElement.Fire,
            CelestriadElement.Ice, CelestriadElement.Ice,
            CelestriadElement.Lightning, CelestriadElement.Lightning,
            null, null,
        };
        var assigned = new Dictionary<PartyRole, CelestriadElement?>();
        foreach (var (role, wanted) in overrides.Debuff.Resolve(party.PlayerRole))
        {
            var element = ElementFor(wanted);
            var index = pool.FindIndex(e => e == element);
            if (index < 0)
            {
                DiagnosticLog.Warn($"[UmadP5Celestriad] {role} asked for {UmadP5CelestriadStateOverrides.Label(wanted)}, "
                    + $"already held by {UmadP5CelestriadStateOverrides.SeatsPerDebuff} seats -- leaving {role} to the roll.");
                continue;
            }
            pool.RemoveAt(index);
            assigned[role] = element;
        }

        var next = 0;
        foreach (var role in RoleList.Random(party).List)
        {
            if (assigned.ContainsKey(role) || next >= pool.Count) continue;
            assigned[role] = pool[next++];
        }
        return assigned;
    }

    private static CelestriadElement? ElementFor(CelestriadDebuff debuff) => debuff switch
    {
        CelestriadDebuff.Fire => CelestriadElement.Fire,
        CelestriadDebuff.Ice => CelestriadElement.Ice,
        CelestriadDebuff.Lightning => CelestriadElement.Lightning,
        _ => null,
    };

    private CatastrophicChoice? ResolveAero(int set, UmadP5CelestriadStateOverrides overrides) => set switch
    {
        0 => overrides.Set1 switch
        {
            CatastrophicVariantOverride.Aero => CatastrophicChoice.Aero,
            CatastrophicVariantOverride.Earth => CatastrophicChoice.Earth,
            _ => rng.NextBool() ? CatastrophicChoice.Aero : CatastrophicChoice.Earth,
        },
        2 => overrides.Set3 switch
        {
            CatastrophicVariantOverride.Aero => CatastrophicChoice.Aero,
            CatastrophicVariantOverride.Earth => CatastrophicChoice.Earth,
            _ => rng.NextBool() ? CatastrophicChoice.Aero : CatastrophicChoice.Earth,
        },
        _ => null, // set 1 (index 1, the "second" soak) has no Catastrophic Choice
    };

    // 9 towers 40 degrees apart clockwise from north, grouped as 3 contiguous per-element blocks
    // (not interleaved) starting 20 degrees off north, confirmed against the real EObj spawn
    // positions (see UmadP5CelestriadConstants). Only the sector's element changes per run;
    // the nine positions and each element's three contiguous sub-towers stay intact.
    public Vector3 TowerPosition(CelestriadElement element, int subIndex)
    {
        var ringIndex = TowerElementOrder.ToList().IndexOf(element) * 3 + subIndex;
        var angle = MathF.PI / 9f + ringIndex * (MathF.PI * 2f / 9f);
        return new Vector3(MathF.Sin(angle) * CelestriadGeometry.RingRadius, 0f, -MathF.Cos(angle) * CelestriadGeometry.RingRadius);
    }
}
