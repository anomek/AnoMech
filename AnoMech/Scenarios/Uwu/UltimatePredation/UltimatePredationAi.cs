using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu.UltimatePredation;

public class UltimatePredationAi : IScenarioAi<UltimatePredationState>
{
    public string Name => "NAUR";

    private UltimatePredationState state = null!;

    public void Run(UltimatePredationState state, SimWorld world)
    {
        this.state = state;

        var ai = new AiManager(world);
        var safeCardinal = GetSafeCardinal(state);

#if DEBUG
        Plugin.Log.Debug($"[UltimatePredationAi] safeCardinal = {safeCardinal.Position2}");
#endif

        var safeFirstSet = GetSafeForFirstSet(state, safeCardinal);
        var safeSecondSet = GetSafeForSecondSet(state, safeCardinal);

        ai.Move(15f, () => AiMove.All(safeCardinal.Position2), jitter: 0.25f);
        ai.Move(19f, () => AiMove.All(safeFirstSet.Position2), jitter: 0.125f);
        ai.Move(21f, () => AiMove.All(safeSecondSet.Position2), jitter: 0);
        ai.Move(26f, PostDodges, jitter: 0.25f);
        ai.Move(37.5f, EruptionBaits1, jitter: 0.25f);
        ai.Move(39.25f, EruptionBaits2, jitter: 0.25f);
        ai.Move(41.45f, EruptionBaits3, jitter: 0.25f);
        ai.Move(43.25f, EruptionBaits4, jitter: 0.25f);
        ai.Move(45.35f, () => AiMove.All(new(0, -8.5f)), jitter: 0.25f);
        ai.Move(54f, () => AiMove.All(new(0, -18)), jitter: 0.25f);
        ai.Move(55.75f, UltimaLandslideDodge, jitter: 0.125f);
        ai.Move(58f, () => AiMove.All(new(0, -18)), jitter: 0.25f);
        ai.Move(60f, PartySplit, jitter: 0.25f);
        ai.Move(73.5f, PartySplitFeatherRain, jitter: 0.5f);
        ai.Move(77f, PartySplit, jitter: 0.25f);
    }

    private Placement GetSafeCardinal(UltimatePredationState state)
    {
        var cardinals = new List<SafePosition>
        {
            new(new(0, 0, -18), Geometry.LookAtCenterRotation[DirectionEnum.N]),
            new(new(0, 0, 18), Geometry.LookAtCenterRotation[DirectionEnum.S]),
            new(new(-18, 0, 0), Geometry.LookAtCenterRotation[DirectionEnum.W]),
            new(new(18, 0, 0), Geometry.LookAtCenterRotation[DirectionEnum.E]),
        };

        var finder = new CharacterFind<SafePosition>(cardinals);

        var unsafeGaruda = finder.InsideCircle(state.GarudaPlacement.Position, Geometry.WickedTornadoOuterRadius);
        var unsafeTitan = finder.InsideCircle(state.TitanPlacement.Position, 10); // Arbitrary value, just to be sure we're not next to Titan

        cardinals.RemoveAll(x => unsafeGaruda.Contains(x) || unsafeTitan.Contains(x));
        return state.Rng.NextObj(cardinals.ToArray()).Placement();
    }

    private Placement GetSafeForFirstSet(UltimatePredationState state, Placement safeCardinal)
    {
        var left = GetSafeInSide(safeCardinal, 4, true, DeleteUnsafeFirstSet);
        var right = GetSafeInSide(safeCardinal, 4, false, DeleteUnsafeFirstSet);

        SafePosition position;

        if (left != null && right != null)
        {
            position = state.Rng.NextBool() ? left : right;
        }
        else if (left != null && right == null)
        {
            position = left;
        }
        else if (left == null && right != null)
        {
            position = right;
        }
        else
        {
            throw new Exception("[UltimatePredationAi.GetSafeForFirstSet] Was not able to find a safe position.");
        }

        return position.Placement();
    }

    private Placement GetSafeForSecondSet(UltimatePredationState state, Placement safeCardinal)
    {
        var left = GetSafeInSide(safeCardinal, 6, true, DeleteUnsafeSecondSet);
        var right = GetSafeInSide(safeCardinal, 6, false, DeleteUnsafeSecondSet);

        SafePosition position;

        if (left != null && right != null)
        {
            position = state.Rng.NextBool() ? left : right;
        }
        else if (left != null && right == null)
        {
            position = left;
        }
        else if (left == null && right != null)
        {
            position = right;
        }
        else
        {
            throw new Exception("[UltimatePredationAi.GetSafeForSecondSet] Was not able to find a safe position.");
        }

        return position.Placement();
    }

    private SafePosition? GetSafeInSide(Placement placement, float finalOffset, bool negative, Action<List<SafePosition>> deleteUnsafe)
    {
        const int Steps = 10;
        var stepDistance = finalOffset / Steps;

        if (negative)
        {
            stepDistance *= -1;
        }

        var positions = Enumerable.Range(1, Steps + 1)
            .Select(x => SafePosition.FromPlacement(placement.MoveRight(x * stepDistance)))
            .ToList();

        deleteUnsafe(positions);

        if (positions.Count == 0)
        {
            return null;
        }

        var index = int.Clamp(positions.Count / 2, 0, positions.Count - 1);

#if DEBUG
        var str = string.Join('\n', positions);

        Plugin.Log.Debug($"[UltimatePredationAi.GetSafeInSide]\n" +
            $"{str}\n" +
            $"Chosen: {positions[index]} (Index {index})");
#endif

        return positions[index];
    }

    private void DeleteUnsafeFirstSet(List<SafePosition> list)
    {
        var finder = new CharacterFind<SafePosition>(list);

        var unsafeIfrit = finder.InsideActionAoe(ActionId.CrimsonCyclone, state.IfritPlacement);

        var unsafeLandslides = new List<IReadOnlyList<SafePosition>>();

        foreach (var rotation in Geometry.TitanLandslideOffsets)
        {
            var landslide = finder.InsideActionAoe(ActionId.LandslideLine, new(state.TitanPlacement.Position, state.TitanPlacement.Rotation + rotation));
            unsafeLandslides.Add(landslide);
        }

        var unsafeTitan = unsafeLandslides.SelectMany(x => x);

        list.RemoveAll(x => unsafeIfrit.Contains(x) || unsafeTitan.Contains(x));
    }

    private void DeleteUnsafeSecondSet(List<SafePosition> list)
    {
        var finder = new CharacterFind<SafePosition>(list);

        var unsafeUltima = finder.InsideCircle(state.UltimaPlacement.Position, 10); // ActionId.CeruleumVent has a 8 EffectRange, but for safety we do 10 here

        var unsafeIfrit = new List<IReadOnlyList<SafePosition>>
            {
                finder.InsideActionAoe(ActionId.CrimsonCycloneAwaken, Geometry.CrimsonCycloneAwakenPlacements[0]),
                finder.InsideActionAoe(ActionId.CrimsonCycloneAwaken, Geometry.CrimsonCycloneAwakenPlacements[1])
            }.SelectMany(x => x);

        var unsafeLandslides = new List<IReadOnlyList<SafePosition>>();

        foreach (var rotation in Geometry.TitanLandslideAwakenOffsets)
        {
            var landslide = finder.InsideActionAoe(ActionId.LandslideAwaken, new(state.TitanPlacement.Position, state.TitanPlacement.Rotation + rotation));
            unsafeLandslides.Add(landslide);
        }

        var unsafeTitan = unsafeLandslides.SelectMany(x => x);

        list.RemoveAll(x => unsafeUltima.Contains(x) || unsafeIfrit.Contains(x) || unsafeTitan.Contains(x));
    }

    private IAiMove PostDodges()
    {
        return AiMove.Create(
            new(-1.5f, -10),  // MT
            new(0, -8), // OT
            new(0, -8),   // H1
            new(0, -8),  // H2
            new(0, -8),   // M1
            new(0, -8), // M2
            new(0, 8),  // R1
            new(0, 8) // R2
            ).NaturalOrder();
    }

    private IAiMove EruptionBaits1()
    {
        return AiMove.Create(
            null, null, null, null, null, null,
            new(-4, 15),  // R1
            new(4, 15) // R2
            ).NaturalOrder();
    }

    private IAiMove EruptionBaits2()
    {
        return AiMove.Create(
            null, null, null, null, null, null,
            new(-10.5f, 8.5f),  // R1
            new(10.5f, 8.5f) // R2
            ).NaturalOrder();
    }

    private IAiMove EruptionBaits3()
    {
        return AiMove.Create(
            null, null, null, null, null, null,
            new(-15, -1),  // R1
            new(15, -1) // R2
            ).NaturalOrder();
    }

    private IAiMove EruptionBaits4()
    {
        return AiMove.Create(
            null, null, null, null, null, null,
            new(-13, -8.5f),  // R1
            new(13, -8.5f) // R2
            ).NaturalOrder();
    }

    private IAiMove UltimaLandslideDodge()
    {
        var positions = new Vector2[]
        {
            new(-4.5f, -17.5f),
            new(4.5f, -17.5f)
        };

        var titanPosition2 = new Vector2(state.ScenarioObjects.Titan!.Position.X, state.ScenarioObjects.Titan.Position.Z);

        var closest = positions
            .OrderBy(x => Vector2.DistanceSquared(x, titanPosition2))
            .First();

        return AiMove.All(closest);
    }

    private IAiMove PartySplit()
    {
        return AiMove.Create(
            new(5, -12f), // MT
            new(-5, -14f), // OT
            new(-5, -14f),   // H1
            new(-5, -14f),  // H2
            new(-5, -14f),   // M1
            new(-5, -14f), // M2
            new(-5, -14f),  // R1
            new(-5, -14f) // R2
            ).NaturalOrder();
    }

    private IAiMove PartySplitFeatherRain()
    {
        return AiMove.Create(
            new(5, -7.5f), // MT
            new(-5, -7.5f), // OT
            new(-5, -7.5f),   // H1
            new(-5, -7.5f),  // H2
            new(-5, -7.5f),   // M1
            new(-5, -7.5f), // M2
            new(-5, -7.5f),  // R1
            new(-5, -7.5f) // R2
            ).NaturalOrder();
    }

    private class SafePosition(Vector3 Position, float Rotation) : IPositioned
    {
        public Vector3 Position { get; } = Position;

        public float Rotation { get; } = Rotation;

        public static SafePosition FromPlacement(Placement placement)
        {
            return new(placement.Position, placement.Rotation);
        }

        public override string ToString()
        {
            return $"Position = {Position}; Rotation = {Rotation}";
        }
    }
}
