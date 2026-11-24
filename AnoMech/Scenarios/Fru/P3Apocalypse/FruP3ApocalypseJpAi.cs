using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P3Apocalypse.FruP3ApocalypseState;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

public sealed class FruP3ApocalypseJpAi : IScenarioAi<FruP3ApocalypseState>
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    private static readonly PartyRole[] ZOrder =
    [
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.PhysRangedDps, PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    private const float NorthNorthwest = 7.5f;
    private const float BoxCenterRadius = 11f;
    private const float BoxHalfSize = 2.25f;
    private const float SpiritTakerFront = 6f;
    private const float SpiritTakerBack = 15.5f;
    private const float SpiritTakerHalfWidth = 4.75f;
    private const float NearSpreadRadius = 10f;
    private const float EdgeSpreadRadius = 19f;
    private const float EdgeSpreadOctants = 12.5f / 45f;
    private const float SafePathRadius = 16f;
    private const float CenterStackRadius = 4.5f;
    private const float DanceBaitRadius = 18f;

    private FruP3ApocalypseState state = null!;
    private SimWorld world = null!;
    private readonly int[] slots = new int[8];

    public void Run(FruP3ApocalypseState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        SwapHigherPriorityDuplicates();
        var ai = new AiManager(world);
        ai.Move(2.2f, () => ByRole(role => BoxSpot(role, afterSwaps: false)), arrivalTime: 12.3f);
        ai.Move(16.9f, () => ByRole(role => BoxSpot(role, afterSwaps: true)), arrivalTime: 20.2f);
        ai.Move(20.2f, () => ByRole(FirstWaterStack), arrivalTime: WaterAt(1));
        ai.Move(22.85f, () => ByRole(SpiritTakerSpread), arrivalTime: 24.8f);
        ai.Move(28f, () => ByRole(ApocalypseSpread), arrivalTime: WaveAt(0));
        ai.Move(36.05f, () => ByRole(role => IsOnTheEdge(role) ? OntoTheReferenceSpoke(role) : CenterStack(role)));
        ai.Move(36.85f, () => ByRole(role => IsOnTheEdge(role) ? CenterStack(role) : null));
        ai.Move(41.7f, () => ByRole(role => role == PartyRole.OffTank ? DarkestDanceBait() : null));
        ai.Move(42f, () => ByRole(role => role == PartyRole.OffTank ? null : Vector3.Zero), arrivalTime: 44f);
        ai.Move(45.15f, () => ByRole(KnockbackStack), jitter: 0f);
        ai.Move(48.4f, () => ByRole(ReturnStack), jitter: 0f);
    }

    private void SwapHigherPriorityDuplicates()
    {
        for (var i = 0; i < slots.Length; i++) slots[i] = i;
        var supports = HigherPriorityDuplicates(0);
        var dps = HigherPriorityDuplicates(4);
        for (var i = 0; i < supports.Length; i++)
            (slots[supports[i]], slots[dps[i]]) = (slots[dps[i]], slots[supports[i]]);
    }

    private int[] HigherPriorityDuplicates(int start) => Enumerable.Range(0, 4)
        .Where(duration => Enumerable.Range(start, 4).Count(i => state.WaterDurations[i] == duration) == 2)
        .Select(duration => Enumerable.Range(start, 4).First(i => state.WaterDurations[i] == duration))
        .ToArray();

    private bool InTankHealerGroup(PartyRole role, bool afterSwaps = true) => (afterSwaps ? slots[(int)role] : (int)role) < 4;

    private int ZRank(PartyRole role, bool afterSwaps = true)
    {
        var group = InTankHealerGroup(role, afterSwaps);
        return ZOrder.Where(r => InTankHealerGroup(r, afterSwaps) == group).ToList().IndexOf(role);
    }

    private static Vector3 AtOctant(float octant, float radius)
        => new(MathF.Sin(octant * MathF.PI / 4) * radius, 0, -MathF.Cos(octant * MathF.PI / 4) * radius);

    private static Vector3 FacingTheCenter(float octant, float outward, float clockwiseSide)
    {
        var angle = octant * MathF.PI / 4;
        var radial = new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle));
        var clockwise = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        return radial * outward + clockwise * clockwiseSide;
    }

    private static float GroupOctant(bool tankHealers) => tankHealers ? NorthNorthwest : NorthNorthwest - 4;

    private Vector3? BoxSpot(PartyRole role, bool afterSwaps)
    {
        var rank = ZRank(role, afterSwaps);
        return FacingTheCenter(GroupOctant(InTankHealerGroup(role, afterSwaps)),
            BoxCenterRadius + (rank < 2 ? -BoxHalfSize : BoxHalfSize),
            rank % 2 == 0 ? BoxHalfSize : -BoxHalfSize);
    }

    private Vector3? FirstWaterStack(PartyRole role) => FacingTheCenter(GroupOctant(InTankHealerGroup(role)), BoxCenterRadius, 0);

    private Vector3? SpiritTakerSpread(PartyRole role)
    {
        var rank = ZRank(role);
        return FacingTheCenter(GroupOctant(InTankHealerGroup(role)),
            rank < 2 ? SpiritTakerFront : SpiritTakerBack,
            rank % 2 == 0 ? SpiritTakerHalfWidth : -SpiritTakerHalfWidth);
    }

    private int AgainstTheLights => state.Clockwise ? -1 : 1;

    private int TankHealerLight => state.Rotation <= 1 ? state.Rotation : state.Rotation + 4;

    private int ReferenceSpoke(PartyRole role) => TankHealerLight + AgainstTheLights + (InTankHealerGroup(role) ? 0 : 4);

    private Vector3? ApocalypseSpread(PartyRole role)
    {
        var reference = ReferenceSpoke(role);
        var further = reference + AgainstTheLights;
        var furtherIsClockwise = AgainstTheLights > 0;
        return ZRank(role) switch
        {
            0 => AtOctant(furtherIsClockwise ? further : reference, NearSpreadRadius),
            1 => AtOctant(furtherIsClockwise ? reference : further, NearSpreadRadius),
            2 => AtOctant(reference + EdgeSpreadOctants, EdgeSpreadRadius),
            _ => AtOctant(reference - EdgeSpreadOctants, EdgeSpreadRadius),
        };
    }

    private bool IsOnTheEdge(PartyRole role) => ZRank(role) >= 2;

    private Vector3? OntoTheReferenceSpoke(PartyRole role) => AtOctant(ReferenceSpoke(role), SafePathRadius);

    private Vector3? CenterStack(PartyRole role) => AtOctant(ReferenceSpoke(role), CenterStackRadius);

    private Vector3 DarkestDanceBait()
        => AtOctant(ReferenceSpoke(PartyRole.OffTank) + AgainstTheLights, DanceBaitRadius);

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP3)?.Position ?? Vector3.Zero;

    private Vector3? KnockbackStack(PartyRole role)
    {
        var boss = OraclePosition();
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var angle = (InTankHealerGroup(role) ? 30 : -30) * MathF.PI / 180;
        var direction = new Vector3(inward.X * MathF.Cos(angle) - inward.Z * MathF.Sin(angle), 0,
            inward.X * MathF.Sin(angle) + inward.Z * MathF.Cos(angle));
        return boss + direction * 2;
    }

    private Vector3? ReturnStack(PartyRole role)
    {
        var boss = OraclePosition();
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var right = new Vector3(inward.Z, 0, -inward.X);
        return boss + inward * 2 + right * (InTankHealerGroup(role) ? -4 : 4);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
