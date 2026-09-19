using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Fru.Apocalypse.ApocalypseConstants;

namespace AnoMech.Scenarios.Fru.Apocalypse;

// NA priority: MT > OT > H1 > H2 / M1 > M2 > R1 > R2.
// Static (Freepoc) spreads return to original roles after Spirit Taker.
// Timings and route coordinates adapted from WCGH/FRU-Sim (GPL-3.0),
// commit 2a77c857ce1bb6eb472a59a95c7544faba01c55a, scenes/p3.
internal sealed class ApocalypsePattern
{
    internal static readonly PartyRole[] Roles = [PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
    private readonly int[] water;
    private readonly int[] slots = Enumerable.Range(0, 8).ToArray();
    public int Rotation { get; }
    public bool Clockwise { get; }
    public const float Scale = 20f / 47.4f;
    public ApocalypsePattern(int rotation, bool clockwise, IReadOnlyList<int> assignments)
    {
        if (rotation is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(rotation));
        if (assignments.Count != 8 || assignments.Any(x => x is < 0 or > 3)
            || Enumerable.Range(0, 4).Any(x => assignments.Count(a => a == x) != 2))
            throw new ArgumentException("Each water duration (including none) must occur twice.", nameof(assignments));
        Rotation = rotation;
        Clockwise = clockwise;
        water = assignments.ToArray();
        var support = Adjusters(0);
        var dps = Adjusters(4);
        for (var i = 0; i < support.Length; i++)
            (slots[support[i]], slots[dps[i]]) = (slots[dps[i]], slots[support[i]]);
    }
    public static ApocalypsePattern Randomize()
    {
        int[] durations = [0, 0, 1, 1, 2, 2, 3, 3];
        Random.Shared.Shuffle(durations);
        return new(Random.Shared.Next(4), Random.Shared.Next(2) == 0, durations);
    }
    private int[] Adjusters(int start) => Enumerable.Range(0, 4)
        .Where(d => Enumerable.Range(start, 4).Count(i => water[i] == d) == 2)
        .Select(d => Enumerable.Range(start, 4).First(i => water[i] == d)).ToArray();
    public int Index(PartyRole role) => Array.IndexOf(Roles, role);
    public int Duration(PartyRole role) => water[Index(role)];
    public int Slot(PartyRole role, bool adjusted = true) => adjusted ? slots[Index(role)] : Index(role);
    public bool SupportGroup(PartyRole role) => Slot(role) < 4;
    public static float WaterTime(int duration) => duration switch { 1 => 23.1f, 2 => 41.9f, 3 => 50.9f, _ => throw new ArgumentOutOfRangeException(nameof(duration)) };
    public static float WaveTime(int wave) => 33.3f + 2 * wave;
    private float SpreadRotation => (Clockwise ? new[] { -45, 0, -135, -90 } : new[] { -135, -90, -45, 0 })[Rotation];
    // Godot's north is +X; convert to native north (-Z), preserving CW rotation.
    internal static Vector3 Reference(float x, float z, float degrees = 0)
    {
        var angle = degrees * MathF.PI / 180;
        return new Vector3(x * MathF.Sin(angle) + z * MathF.Cos(angle), 0,
            -x * MathF.Cos(angle) + z * MathF.Sin(angle)) * Scale;
    }
    public Vector3 Setup(PartyRole role, bool adjusted = true, bool spread = false)
    {
        var slot = Slot(role, adjusted);
        var support = slot < 4;
        var local = slot % 4;
        var x = (local % 2 == 0 ? 1 : -1) * (support ? 1 : -1) * (spread ? 8 : 4);
        var z = (support ? -1 : 1) * (spread ? (local < 2 ? 9 : 27) : (local < 2 ? 8 : 16));
        return Reference(x, z);
    }
    public Vector3 FirstStack(PartyRole role) => Reference(0, SupportGroup(role) ? -12 : 12);
    public Vector3 Spread(PartyRole role)
    {
        var slot = Slot(role, false);
        var side = slot < 4 ? 1 : -1;
        var local = slot % 4;
        var (x, z) = local switch
        {
            0 => Clockwise ? (23f, 0f) : (16.23f, 16.23f),
            1 => Clockwise ? (16.23f, -16.23f) : (23f, 0f),
            2 => (44f, 8f),
            _ => (44f, -8f)
        };
        return Reference(x * side, z * side, SpreadRotation);
    }
    public Vector3 PostEruption(PartyRole role) => Reference((Slot(role, false) < 4 ? 1 : -1)
        * (Slot(role, false) % 4 < 2 ? 10 : 33), 0, SpreadRotation);
    public Vector3 SecondStack(PartyRole role) => Reference(SupportGroup(role) ? 10 : -10, 0, SpreadRotation);
    public Vector3 TankBait(bool near)
    {
        var angle = (Clockwise ? new[] { -45, 0, -135, -90 } : new[] { -45, 0, 45, 90 })[Rotation];
        if (Slot(PartyRole.OffTank) >= 4) angle += 180;
        return Reference(near ? 10 : 30, near ? -10 : -30, angle);
    }
    public IEnumerable<Vector3> Explosions(int wave)
    {
        if (wave < 2) yield return Vector3.Zero;
        for (var step = Math.Max(0, wave - 2); step <= wave; step++)
        {
            var angle = (Rotation * 45 + (Clockwise ? 45 : -45) * step) * MathF.PI / 180;
            var pos = new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle)) * RingRadius;
            yield return pos;
            yield return -pos;
        }
    }
    public Vector3 StartingDirection => new(MathF.Sin(Rotation * MathF.PI / 4), 0, -MathF.Cos(Rotation * MathF.PI / 4));
    public Vector3 ReturnStack(PartyRole role, Vector3 boss)
    {
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var right = new Vector3(inward.Z, 0, -inward.X);
        // Keep the adjusted water groups eight yalms apart, on the same sides
        // as their knockback rays: supports left, DPS right when facing the boss
        // from arena center. Flexed roles follow their assigned water group.
        return boss + inward * 2 + right * (SupportGroup(role) ? -4 : 4);
    }
    public Vector3 KnockbackStack(PartyRole role, Vector3 boss)
    {
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var angle = (SupportGroup(role) ? 30 : -30) * MathF.PI / 180;
        var dir = new Vector3(inward.X * MathF.Cos(angle) - inward.Z * MathF.Sin(angle), 0,
            inward.X * MathF.Sin(angle) + inward.Z * MathF.Cos(angle));
        return boss + dir * 2;
    }
}
