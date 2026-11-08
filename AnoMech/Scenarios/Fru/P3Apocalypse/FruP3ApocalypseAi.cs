using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P3Apocalypse.FruP3ApocalypseState;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

public sealed class FruP3ApocalypseAi : IScenarioAi<FruP3ApocalypseState>
{
    public string Name => "NA priority / static spreads / OT bait";
    public string Group => "NA";

    private const float Scale = 20f / 47.4f;

    private FruP3ApocalypseState state = null!;
    private SimWorld world = null!;
    private readonly int[] slots = new int[8];

    public void Run(FruP3ApocalypseState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        AdjustWaterGroups();
        var ai = new AiManager(world);
        ai.Move(2.2f, () => ByRole(role => Setup(role, adjusted: false)), arrivalTime: 12.3f);
        ai.Move(16.9f, () => ByRole(role => Setup(role)), arrivalTime: 20.2f);
        ai.Move(20.2f, () => ByRole(FirstStack), arrivalTime: WaterAt(1));
        ai.Move(22.85f, () => ByRole(role => Setup(role, spread: true)), arrivalTime: 24.8f);
        ai.Move(24.85f, () => ByRole(role => Setup(role, adjusted: false, spread: true)), arrivalTime: 28f);
        ai.Move(28f, () => ByRole(Spread), arrivalTime: WaveAt(0));
        ai.Move(36.05f, () => ByRole(PostEruption));
        ai.Move(37f, () => ByRole(SecondStack), arrivalTime: WaveAt(3));
        ai.Move(41.7f, () => ByRole(role => role == PartyRole.OffTank ? TankBait(near: true) : null));
        ai.Move(42f, () => ByRole(role => role == PartyRole.OffTank ? TankBait(near: false) : null));
        ai.Move(42f, () => ByRole(role => role == PartyRole.OffTank ? null : Vector3.Zero), arrivalTime: 44f);
        ai.Move(44.3f, () => ByRole(role => KnockbackStack(role, DanceLandingShortOfTheFarBait())), jitter: 0f);
        ai.Move(45.6f, () => ByRole(role => KnockbackStack(role, OraclePosition())), jitter: 0f);
        ai.Move(48.4f, () => ByRole(ReturnStack), jitter: 0f);
    }

    private void AdjustWaterGroups()
    {
        for (var i = 0; i < slots.Length; i++) slots[i] = i;
        var supports = Adjusters(0);
        var dps = Adjusters(4);
        for (var i = 0; i < supports.Length; i++)
            (slots[supports[i]], slots[dps[i]]) = (slots[dps[i]], slots[supports[i]]);
    }

    private int[] Adjusters(int start) => Enumerable.Range(0, 4)
        .Where(duration => Enumerable.Range(start, 4).Count(i => state.WaterDurations[i] == duration) == 2)
        .Select(duration => Enumerable.Range(start, 4).First(i => state.WaterDurations[i] == duration))
        .ToArray();

    private int Slot(PartyRole role, bool adjusted = true) => adjusted ? slots[(int)role] : (int)role;
    private bool SupportGroup(PartyRole role) => Slot(role) < 4;

    private float SpreadRotation => (state.Clockwise ? new[] { -45, 0, -135, -90 } : new[] { -135, -90, -45, 0 })[state.Rotation];

    private static Vector3 Reference(float x, float z, float degrees = 0)
    {
        var angle = degrees * MathF.PI / 180;
        return new Vector3(x * MathF.Sin(angle) + z * MathF.Cos(angle), 0, -x * MathF.Cos(angle) + z * MathF.Sin(angle)) * Scale;
    }

    private Vector3? Setup(PartyRole role, bool adjusted = true, bool spread = false)
    {
        var slot = Slot(role, adjusted);
        var support = slot < 4;
        var local = slot % 4;
        var x = (local % 2 == 0 ? 1 : -1) * (support ? 1 : -1) * (spread ? 8 : 4);
        var z = (support ? -1 : 1) * (spread ? (local < 2 ? 9 : 27) : (local < 2 ? 8 : 16));
        return Reference(x, z);
    }

    private Vector3? FirstStack(PartyRole role) => Reference(0, SupportGroup(role) ? -12 : 12);

    private Vector3? Spread(PartyRole role)
    {
        var slot = Slot(role, adjusted: false);
        var side = slot < 4 ? 1 : -1;
        var (x, z) = (slot % 4) switch
        {
            0 => state.Clockwise ? (23f, 0f) : (16.23f, 16.23f),
            1 => state.Clockwise ? (16.23f, -16.23f) : (23f, 0f),
            2 => (44f, 8f),
            _ => (44f, -8f),
        };
        return Reference(x * side, z * side, SpreadRotation);
    }

    private Vector3? PostEruption(PartyRole role)
    {
        var slot = Slot(role, adjusted: false);
        return Reference((slot < 4 ? 1 : -1) * (slot % 4 < 2 ? 10 : 33), 0, SpreadRotation);
    }

    private Vector3? SecondStack(PartyRole role) => Reference(SupportGroup(role) ? 10 : -10, 0, SpreadRotation);

    private Vector3 TankBait(bool near)
    {
        var angle = (state.Clockwise ? new[] { -45, 0, -135, -90 } : new[] { -45, 0, 45, 90 })[state.Rotation];
        if (Slot(PartyRole.OffTank) >= 4) angle += 180;
        return Reference(near ? 10 : 30, near ? -10 : -30, angle);
    }

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP3)?.Position ?? Vector3.Zero;

    private Vector3 DanceLandingShortOfTheFarBait() => DanceLanding(Vector3.Zero, TankBait(near: false));

    private Vector3? KnockbackStack(PartyRole role, Vector3 boss)
    {
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var angle = (SupportGroup(role) ? 30 : -30) * MathF.PI / 180;
        var direction = new Vector3(inward.X * MathF.Cos(angle) - inward.Z * MathF.Sin(angle), 0,
            inward.X * MathF.Sin(angle) + inward.Z * MathF.Cos(angle));
        return boss + direction * 2;
    }

    private Vector3? ReturnStack(PartyRole role)
    {
        var boss = OraclePosition();
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var right = new Vector3(inward.Z, 0, -inward.X);
        return boss + inward * 2 + right * (SupportGroup(role) ? -4 : 4);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
