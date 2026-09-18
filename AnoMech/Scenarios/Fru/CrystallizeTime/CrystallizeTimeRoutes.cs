using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

// Route coordinates adapted from WCGH/FRU-Sim ct_positions.gd at 2a77c85.
// Coordinates are converted from its north/east axes to game east/south yalms.
// Only movement destinations: none of these tables draw an overlay or VFX.
internal readonly record struct CrystallizeTimeDestination(Vector3 Position, CrystallizeTimeAssignment? Puddle = null);

internal static class CrystallizeTimeRoutes
{
    private static CrystallizeTimeDestination At(float north, float east) => new(new(east * CrystallizeTimePattern.ReferenceScale, 0, -north * CrystallizeTimePattern.ReferenceScale));
    private static CrystallizeTimeDestination Puddle(CrystallizeTimeAssignment source) => new(default, source);

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> PRE_HG_1_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-36.3f, -28.4f),
        [CrystallizeTimeAssignment.AeroEast] = At(-36.3f, 28.4f),
        [CrystallizeTimeAssignment.IceWest] = At(0f, -30f),
        [CrystallizeTimeAssignment.IceEast] = At(0f, 30f),
        [CrystallizeTimeAssignment.Eruption] = At(36.3f, -28.4f),
        [CrystallizeTimeAssignment.Ice] = At(-36f, 28.6f),
        [CrystallizeTimeAssignment.Darkness] = At(-36.15f, 28.12f),
        [CrystallizeTimeAssignment.Water] = At(-36.5f, 28.5f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> PRE_HG_1_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-36.3f, -28.4f),
        [CrystallizeTimeAssignment.AeroEast] = At(-36.3f, 28.4f),
        [CrystallizeTimeAssignment.IceWest] = At(0f, -30f),
        [CrystallizeTimeAssignment.IceEast] = At(0f, 30f),
        [CrystallizeTimeAssignment.Eruption] = At(36.3f, 28.4f),
        [CrystallizeTimeAssignment.Ice] = At(-36f, -28.2f),
        [CrystallizeTimeAssignment.Darkness] = At(-36.15f, -28.68f),
        [CrystallizeTimeAssignment.Water] = At(-36.5f, -28.3f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_1_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-39.6f, -22.8f),
        [CrystallizeTimeAssignment.AeroEast] = At(-39.6f, 22.8f),
        [CrystallizeTimeAssignment.Ice] = At(-35.6f, 20.5f),
        [CrystallizeTimeAssignment.Darkness] = At(-35.75f, 20.02f),
        [CrystallizeTimeAssignment.Water] = At(-36.1f, 20.4f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_1_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-39.6f, -22.8f),
        [CrystallizeTimeAssignment.AeroEast] = At(-39.6f, 22.8f),
        [CrystallizeTimeAssignment.Ice] = At(-35.6f, -20.1f),
        [CrystallizeTimeAssignment.Darkness] = At(-35.75f, -20.58f),
        [CrystallizeTimeAssignment.Water] = At(-36.1f, -20.2f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> PUDDLE_DODGE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.IceWest] = At(4f, -36f),
        [CrystallizeTimeAssignment.IceEast] = At(4f, 36f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_KB_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-42.71f, -16.77f),
        [CrystallizeTimeAssignment.AeroEast] = At(-42.71f, 16.77f),
        [CrystallizeTimeAssignment.IceWest] = At(30.8f, -30.8f),
        [CrystallizeTimeAssignment.IceEast] = At(-7.5f, 45.3f),
        [CrystallizeTimeAssignment.Eruption] = At(31.1f, -30.6f),
        [CrystallizeTimeAssignment.Ice] = At(30.95f, -31.08f),
        [CrystallizeTimeAssignment.Darkness] = At(30.6f, -30.7f),
        [CrystallizeTimeAssignment.Water] = At(30.5f, -31f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_KB_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-42.71f, -16.77f),
        [CrystallizeTimeAssignment.AeroEast] = At(-42.71f, 16.77f),
        [CrystallizeTimeAssignment.IceWest] = At(-7.5f, -45.3f),
        [CrystallizeTimeAssignment.IceEast] = At(30.8f, 30.8f),
        [CrystallizeTimeAssignment.Eruption] = At(31.1f, 31f),
        [CrystallizeTimeAssignment.Ice] = At(30.95f, 30.52f),
        [CrystallizeTimeAssignment.Darkness] = At(30.6f, 30.9f),
        [CrystallizeTimeAssignment.Water] = At(30.5f, 30.6f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_2_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-24.2f, -16.77f),
        [CrystallizeTimeAssignment.AeroEast] = At(-24.2f, 16.77f),
        [CrystallizeTimeAssignment.IceEast] = At(30.8f, 30.8f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_2_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-24.2f, -16.77f),
        [CrystallizeTimeAssignment.AeroEast] = At(-24.2f, 16.77f),
        [CrystallizeTimeAssignment.IceWest] = At(30.8f, -30.8f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_UD_E = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.IceWest] = At(38.9f, -4f),
        [CrystallizeTimeAssignment.IceEast] = At(39.2f, -3.8f),
        [CrystallizeTimeAssignment.Eruption] = At(43f, -15.1f),
        [CrystallizeTimeAssignment.Ice] = At(43f, -15.1f),
        [CrystallizeTimeAssignment.Darkness] = At(39.05f, -4.28f),
        [CrystallizeTimeAssignment.Water] = At(38.7f, -3.9f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_UD_W = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.IceWest] = At(38.9f, 4f),
        [CrystallizeTimeAssignment.IceEast] = At(39.2f, 4.2f),
        [CrystallizeTimeAssignment.Eruption] = At(39.05f, 3.72f),
        [CrystallizeTimeAssignment.Ice] = At(38.7f, 4.1f),
        [CrystallizeTimeAssignment.Darkness] = At(43f, 15.1f),
        [CrystallizeTimeAssignment.Water] = At(43f, 15.1f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EARLY_SOAK_E = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-38.9f, -4f),
        [CrystallizeTimeAssignment.AeroEast] = At(-38.6f, -3.8f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EARLY_SOAK_W = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-38.9f, 4f),
        [CrystallizeTimeAssignment.AeroEast] = At(-38.6f, 4.2f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_3_E = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Eruption] = Puddle(CrystallizeTimeAssignment.IceWest),
        [CrystallizeTimeAssignment.Ice] = At(-10f, -29.6f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_HG_3_W = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Darkness] = Puddle(CrystallizeTimeAssignment.IceEast),
        [CrystallizeTimeAssignment.Water] = At(-10f, 29.6f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_2_E = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(0f, 20f),
        [CrystallizeTimeAssignment.AeroEast] = At(0.3f, 20.2f),
        [CrystallizeTimeAssignment.IceWest] = At(0.15f, 19.72f),
        [CrystallizeTimeAssignment.IceEast] = At(-0.2f, 20.1f),
        [CrystallizeTimeAssignment.Darkness] = Puddle(CrystallizeTimeAssignment.IceEast),
        [CrystallizeTimeAssignment.Water] = Puddle(CrystallizeTimeAssignment.AeroEast),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_2_W = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(0f, -20f),
        [CrystallizeTimeAssignment.AeroEast] = At(0.3f, -19.8f),
        [CrystallizeTimeAssignment.IceWest] = At(0.15f, -20.28f),
        [CrystallizeTimeAssignment.IceEast] = At(-0.2f, -19.9f),
        [CrystallizeTimeAssignment.Eruption] = Puddle(CrystallizeTimeAssignment.IceWest),
        [CrystallizeTimeAssignment.Ice] = Puddle(CrystallizeTimeAssignment.AeroWest),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_3_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(21.5f, -12f),
        [CrystallizeTimeAssignment.AeroEast] = At(21.8f, -11.8f),
        [CrystallizeTimeAssignment.IceWest] = At(21.65f, -12.28f),
        [CrystallizeTimeAssignment.IceEast] = At(21.3f, -11.9f),
        [CrystallizeTimeAssignment.Darkness] = At(22.3f, 11.4f),
        [CrystallizeTimeAssignment.Water] = Puddle(CrystallizeTimeAssignment.AeroEast),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_3_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(21.5f, 12f),
        [CrystallizeTimeAssignment.AeroEast] = At(21.8f, 12.2f),
        [CrystallizeTimeAssignment.IceWest] = At(21.65f, 11.72f),
        [CrystallizeTimeAssignment.IceEast] = At(21.3f, 12.1f),
        [CrystallizeTimeAssignment.Eruption] = At(22.3f, -11.4f),
        [CrystallizeTimeAssignment.Ice] = Puddle(CrystallizeTimeAssignment.AeroWest),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_3_SE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-21.5f, 12f),
        [CrystallizeTimeAssignment.AeroEast] = At(-21.2f, 12.2f),
        [CrystallizeTimeAssignment.IceWest] = At(-21.35f, 11.72f),
        [CrystallizeTimeAssignment.IceEast] = At(-21.7f, 12.1f),
        [CrystallizeTimeAssignment.Eruption] = At(-22.3f, -11.4f),
        [CrystallizeTimeAssignment.Ice] = Puddle(CrystallizeTimeAssignment.AeroWest),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_3_SW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-21.5f, -12f),
        [CrystallizeTimeAssignment.AeroEast] = At(-21.2f, -11.8f),
        [CrystallizeTimeAssignment.IceWest] = At(-21.35f, -12.28f),
        [CrystallizeTimeAssignment.IceEast] = At(-21.7f, -11.9f),
        [CrystallizeTimeAssignment.Darkness] = At(-22.3f, 17.4f),
        [CrystallizeTimeAssignment.Water] = Puddle(CrystallizeTimeAssignment.AeroEast),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_4_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(27.1f, -19.1f),
        [CrystallizeTimeAssignment.AeroEast] = At(27.4f, -18.9f),
        [CrystallizeTimeAssignment.IceWest] = At(27.25f, -19.38f),
        [CrystallizeTimeAssignment.IceEast] = At(26.9f, -19f),
        [CrystallizeTimeAssignment.Eruption] = At(26.8f, -19.3f),
        [CrystallizeTimeAssignment.Darkness] = At(26.95f, -18.82f),
        [CrystallizeTimeAssignment.Water] = At(-2f, -2f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_4_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(27.1f, 19.1f),
        [CrystallizeTimeAssignment.AeroEast] = At(27.4f, 19.3f),
        [CrystallizeTimeAssignment.IceWest] = At(27.25f, 18.82f),
        [CrystallizeTimeAssignment.IceEast] = At(26.9f, 19.2f),
        [CrystallizeTimeAssignment.Eruption] = At(26.8f, 18.9f),
        [CrystallizeTimeAssignment.Ice] = At(-2f, 2f),
        [CrystallizeTimeAssignment.Darkness] = At(26.95f, 19.38f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_4_SE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-27.1f, 19.1f),
        [CrystallizeTimeAssignment.AeroEast] = At(-26.8f, 19.3f),
        [CrystallizeTimeAssignment.IceWest] = At(-26.95f, 18.82f),
        [CrystallizeTimeAssignment.IceEast] = At(-27.3f, 19.2f),
        [CrystallizeTimeAssignment.Eruption] = At(-27.4f, 18.9f),
        [CrystallizeTimeAssignment.Darkness] = At(-27.25f, 19.38f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_EXA_4_SW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.AeroWest] = At(-27.1f, -19.1f),
        [CrystallizeTimeAssignment.AeroEast] = At(-26.8f, -18.9f),
        [CrystallizeTimeAssignment.IceWest] = At(-26.95f, -19.38f),
        [CrystallizeTimeAssignment.IceEast] = At(-27.3f, -19f),
        [CrystallizeTimeAssignment.Eruption] = At(-27.4f, -19.3f),
        [CrystallizeTimeAssignment.Darkness] = At(-27.25f, -18.82f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_SOAK_TARGET_NW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Eruption] = At(22.3f, -12.4f),
        [CrystallizeTimeAssignment.Ice] = At(-2f, -20f),
        [CrystallizeTimeAssignment.Water] = At(-2f, -2f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_SOAK_TARGET_NE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Darkness] = At(22.3f, 12.4f),
        [CrystallizeTimeAssignment.Ice] = At(-2f, 2f),
        [CrystallizeTimeAssignment.Water] = At(-2f, 20f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_SOAK_TARGET_SE = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Darkness] = At(-22.3f, 11.4f),
        [CrystallizeTimeAssignment.Ice] = At(-26.9f, 19f),
        [CrystallizeTimeAssignment.Water] = At(-33f, 0f),
    };

    public static readonly IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> POST_SOAK_TARGET_SW = new Dictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination>
    {
        [CrystallizeTimeAssignment.Eruption] = At(-22.3f, -12.4f),
        [CrystallizeTimeAssignment.Water] = At(-26.9f, -19.2f),
        [CrystallizeTimeAssignment.Ice] = At(-33f, 0f),
    };
}
