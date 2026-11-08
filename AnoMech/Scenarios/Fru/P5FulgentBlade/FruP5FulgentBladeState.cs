using System;
using System.Numerics;
using AnoMech.Core.Game;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.P5FulgentBlade;

// Three groups of two seams, a cardinal one and a diagonal one crossing at a corner of the safe
// path. Each seam splits into a light and a dark strip rolling apart 5y every 2s until they leave
// the arena. The corners sit a 90° turn apart around a pocket 7y off centre; the pattern is any
// rotation or mirror image of that layout.
public sealed class FruP5FulgentBladeState
{
    public const int GroupCount = 3;
    public const int SeamsPerGroup = 2;
    public const int MaxHitCount = 8;
    public const float Step = 5f;
    // A strip stops rolling once its helper would stand further out than this.
    private const float RollLimit = Geometry.ArenaRadius + Step / 2f;
    private const float PocketDistance = 7f;

    public static float HitAt(int group, int hit) => 18.6f + group * 4f + hit * 2.05f;

    // Which cardinal the pocket sits at, before the mirror: 0 = south, 1 = north, 2 = east, 3 = west.
    public int PositionIndex { get; }
    public int RotationIndex { get; }
    public bool Mirrored { get; }
    // Per group: whether the cardinal seam's dark strip rolls towards the centre; the diagonal's
    // dark strip always rolls the other way.
    private readonly bool[] cardinalDarkInward = new bool[GroupCount];

    // Which of the four furthest to Pandora's left / right each Akh Morn stack picks, counted from
    // that side.
    public int AkhMornLeftPick { get; }
    public int AkhMornRightPick { get; }

    public FruP5FulgentBladeState(Rng rng, FruP5FulgentBladeStateOverrides overrides)
    {
        PositionIndex = overrides.PositionIndex ?? rng.Next(4);
        RotationIndex = overrides.RotationIndex ?? rng.Next(4);
        Mirrored = overrides.Mirrored ?? rng.NextBool();
        for (var group = 0; group < GroupCount; group++)
            cardinalDarkInward[group] = overrides.CardinalDarkInward?[group] ?? rng.NextBool();
        AkhMornLeftPick = overrides.AkhMornLeftPick ?? rng.Next(4);
        AkhMornRightPick = overrides.AkhMornRightPick ?? rng.Next(4);
    }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): only what the Ai reads.
    private FruP5FulgentBladeState(int positionIndex, int rotationIndex, bool mirrored)
    {
        PositionIndex = positionIndex;
        RotationIndex = rotationIndex;
        Mirrored = mirrored;
    }

    public static FruP5FulgentBladeState? FromNetworkReplay(int positionIndex, int rotationIndex, bool mirrored)
        => positionIndex is >= 0 and < 4 && rotationIndex is >= 0 and < 4 ? new(positionIndex, rotationIndex, mirrored) : null;

    public float Rotation => RotationIndex * MathF.PI / 2f;

    public Vector3 Pocket => PositionIndex switch
    {
        0 => new Vector3(0f, 0f, PocketDistance),
        1 => new Vector3(0f, 0f, -PocketDistance),
        2 => new Vector3(PocketDistance, 0f, 0f),
        _ => new Vector3(-PocketDistance, 0f, 0f),
    };

    public Vector3 Origin => Place(Pocket);

    // Maps a point of the unmirrored layout into the arena.
    public Vector3 Place(Vector3 layout) => Mirrored ? new Vector3(-layout.X, layout.Y, layout.Z) : layout;

    private float Place(float yaw) => Mirrored ? -yaw : yaw;

    // The seam's spot closest to the centre, facing the way its light strip rolls.
    public Placement Seam(int group, int seam)
    {
        var (corner, cardinalYaw) = group switch
        {
            0 => (new Vector3(4.2f, 0f, 10f), MathF.PI),
            1 => (new Vector3(10f, 0f, -4.2f), 3f * MathF.PI / 2f),
            _ => (new Vector3(-4.2f, 0f, -10f), 0f),
        };
        var position = Place(Pocket + Vector3.Transform(corner, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rotation)));
        var normal = Place(Rotation + cardinalYaw + (seam == 0 ? 0f : 5f * MathF.PI / 4f));
        var forward = new Vector3(MathF.Sin(normal), 0f, MathF.Cos(normal));
        var distance = Vector3.Dot(position, forward);
        var darkInward = cardinalDarkInward[group] == (seam == 0);
        var lightForward = (distance < 0f) == darkInward ? -forward : forward;
        return new Placement(lightForward * Vector3.Dot(position, lightForward), MathF.Atan2(lightForward.X, lightForward.Z));
    }

    // Where a strip stands on its nth hit, or null once it has rolled out of the arena.
    public Placement? Strip(int group, int seam, bool isLight, int hit)
    {
        var seamPlacement = Seam(group, seam);
        var facing = isLight ? seamPlacement.Rotation : seamPlacement.Rotation + MathF.PI;
        var forward = new Vector3(MathF.Sin(facing), 0f, MathF.Cos(facing));
        var position = seamPlacement.Position + forward * (hit * Step);
        if (MathF.Abs(Vector3.Dot(position, forward)) > RollLimit) return null;
        return new Placement(position, facing);
    }
}
