using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class Side
{
    public uint DeltaOversampledWaveCannonActionId { get; }
    public uint SwivelCannonActionId { get; }
    public ushort MonitorDebuffId { get; }
    public int Mul { get; }
    public uint ArmUnitId { get; }
    public uint ArmUnitNameId { get; }
    public uint RotateLockonId { get; }

    private Side(uint waveCannonActionId, uint swivelCannonActionId, ushort monitorDebuffId, int mul, uint armUnitId, uint armUnitNameId, uint rotateLockonId)
    {
        DeltaOversampledWaveCannonActionId = waveCannonActionId;
        SwivelCannonActionId = swivelCannonActionId;
        MonitorDebuffId = monitorDebuffId;
        Mul = mul;
        ArmUnitId = armUnitId;
        ArmUnitNameId = armUnitNameId;
        RotateLockonId = rotateLockonId;
    }

    public static readonly Side Right = new(ActionId.OversampledWaveCannonRight, ActionId.SwivelCannonR, StatusId.PlayerMonitorRight,  -1, BNpcBaseId.RightArmUnit, BNpcNameId.RightArmUnit, LockonId.RotateCw);
    public static readonly Side Left = new(ActionId.OversampledWaveCannonLeft, ActionId.SwivelCannonL, StatusId.PlayerMonitorLeft, 1, BNpcBaseId.LeftArmUnit, BNpcNameId.LeftArmUnit, LockonId.RotateCcw);
}

public record NorthSouth(float Mul, byte EffectIndex)
{
    public static readonly NorthSouth North = new(1, 1);
    public static readonly NorthSouth South = new(-1, 5);
}

public sealed class TopP5DeltaState
{
    public IReadOnlyList<PartyRole> TetherOrder { get; }
    public NorthSouth EyeSpawn { get; }
    public IReadOnlyList<int> FistRotations { get; }  // length 6, three +1 and three -1
    public IReadOnlyList<uint> FistColors { get; }    // length 8, each half has two BNpcBaseId.RocketPunchYellow and two RocketPunchBlue
    public IReadOnlyList<Side> ArmHandedness { get; } // length 6, three Left and three Right
    public Side SwivelCannonSide { get; }
    public Side OmegaMonitorSide { get; }
    public Side PlayerMonitorSide { get; }
    public int PlayerMonitorIndex { get; }   // 0..3
    
    public PartyRole PlayerMonitorRole => TetherOrder[PlayerMonitorIndex];
    public int NearWorldTetherIndex { get; } // 0..3, distinct from FarWorldTetherIndex
    public int FarWorldTetherIndex { get; }  // 0..3, distinct from NearWorldTetherIndex

    // The seat that asked to eat Beyond Defence, and the seats that asked not to. Both are
    // resolved at run start; who actually gets hit is picked at t=35.3s from proximity.
    public PartyRole? ForcedBeyondDefenceRole { get; }
    public IReadOnlyCollection<PartyRole> BeyondDefenceExcluded { get; }

    // Null until FireBeyondDefenseAoe resolves it at t=35.3s.
    public PartyRole? BeyondDefenseTarget { get; set; }
    public PartyRole NearWorldRole { get; set; }
    public PartyRole FarWorldRole { get; set; }
    public bool PunchExplosionUnmitigated { get; set; }
    public List<Vector3>? PunchTargets { get; set; }

    public int BeyondDefenseIndex()
    {
        return TetherOrder.Select((role, i) => (role, i))
                          .Where(t => t.role == BeyondDefenseTarget)
                          .Select(t => t.i)
                          .FirstOrDefault(0);
    }

    public TopP5DeltaState(TopP5DeltaStateOverrides overrides, PartyRole playerRole)
    {
        var rng = new Random();

        var roles = ShuffleRoles(rng);

        var tethers = Requests(overrides.Tether, playerRole);
        var monitors = Requests(overrides.Monitor, playerRole);
        var hellos = Requests(overrides.HelloWorld, playerRole);
        var beyond = Requests(overrides.BeyondDefence, playerRole);

        SeatTethers(rng, roles, tethers, monitors, hellos, beyond);
        TetherOrder = roles;

        // Wanting Near or Far is a claim on that one slot; No refuses both.
        var wantsNear = new Dictionary<PartyRole, bool>();
        var wantsFar = new Dictionary<PartyRole, bool>();
        foreach (var (role, option) in hellos)
            switch (option)
            {
                case HelloWorldOption.Near: wantsNear[role] = true;  wantsFar[role] = false; break;
                case HelloWorldOption.Far:  wantsNear[role] = false; wantsFar[role] = true;  break;
                case HelloWorldOption.No:   wantsNear[role] = false; wantsFar[role] = false; break;
            }

        EyeSpawn = overrides.EyeSpawn ?? (rng.Next(2) == 0 ? NorthSouth.North : NorthSouth.South);
        FistRotations = ShuffleInPlace(new[] { 1, 1, 1, -1, -1, -1 }, rng);
        ArmHandedness = ShuffleSides(rng);

        var colors = new uint[8];
        var first = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        var second = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        Array.Copy(first, 0, colors, 0, 4);
        Array.Copy(second, 0, colors, 4, 4);
        FistColors = colors;

        SwivelCannonSide = overrides.SwivelCannonSide ?? RandomSide(rng);
        OmegaMonitorSide = RandomSide(rng);
        PlayerMonitorSide = RandomSide(rng);

        PlayerMonitorIndex = PickCloseSlot(rng, roles, monitors);

        var near = PickCloseSlot(rng, roles, wantsNear);
        var far  = PickCloseSlot(rng, roles, wantsFar, near);
        NearWorldTetherIndex = near;
        FarWorldTetherIndex  = far;
        NearWorldRole = TetherOrder[near];
        FarWorldRole  = TetherOrder[far];

        // Only one player eats it, so the earliest seat that asked for it wins.
        ForcedBeyondDefenceRole = beyond.Where(r => r.Value).OrderBy(r => (int)r.Key).Select(r => (PartyRole?)r.Key).FirstOrDefault();
        BeyondDefenceExcluded = beyond.Where(r => !r.Value).Select(r => r.Key).ToList();
    }

    private static Dictionary<PartyRole, T> Requests<T>(PerRoleSetting<T> setting, PartyRole playerRole) where T : struct
        => setting.Resolve(playerRole).ToDictionary(x => x.Role, x => x.Value);

    // Slots 0-1 = close inner, 2-3 = close outer, 4-5 = far inner, 6-7 = far outer. Every seat
    // that asked for a band is swapped into it in seat order; a seat arriving at a band whose
    // slots are all claimed keeps whatever the shuffle gave it.
    private static void SeatTethers(
        Random rng, PartyRole[] roles,
        Dictionary<PartyRole, PlayerTetherAssignment> tethers,
        Dictionary<PartyRole, bool> monitors,
        Dictionary<PartyRole, HelloWorldOption> hellos,
        Dictionary<PartyRole, bool> beyond)
    {
        var claimed = new bool[8];
        foreach (var role in PerRole.All)
        {
            if (SlotsFor(EffectiveTether(role, tethers, monitors, hellos, beyond)) is not { } valid) continue;
            var current = Array.IndexOf(roles, role);
            if (Array.IndexOf(valid, current) >= 0) { claimed[current] = true; continue; }
            var free = valid.Where(s => !claimed[s]).ToArray();
            if (free.Length == 0)
            {
                DiagnosticLog.Info($"[TopP5Delta] {role}'s tether band is already full; leaving them where the roll put them.");
                continue;
            }
            var target = free[rng.Next(free.Length)];
            (roles[current], roles[target]) = (roles[target], roles[current]);
            claimed[target] = true;
        }
    }

    // A seat's other choices constrain its band: Beyond Defence is taken close inner, and
    // Monitor or a Hello World tether can only be taken from the close group.
    private static PlayerTetherAssignment EffectiveTether(
        PartyRole role,
        Dictionary<PartyRole, PlayerTetherAssignment> tethers,
        Dictionary<PartyRole, bool> monitors,
        Dictionary<PartyRole, HelloWorldOption> hellos,
        Dictionary<PartyRole, bool> beyond)
    {
        var tether = tethers.GetValueOrDefault(role, PlayerTetherAssignment.Auto);
        if (beyond.GetValueOrDefault(role)) return PlayerTetherAssignment.CloseInner;
        var needsClose = monitors.GetValueOrDefault(role)
                         || hellos.GetValueOrDefault(role, HelloWorldOption.Auto) is HelloWorldOption.Near or HelloWorldOption.Far;
        if (needsClose && tether is PlayerTetherAssignment.Auto or PlayerTetherAssignment.FarAny
                or PlayerTetherAssignment.FarInner or PlayerTetherAssignment.FarOuter)
            return PlayerTetherAssignment.CloseAny;
        return tether;
    }

    private static int[]? SlotsFor(PlayerTetherAssignment assignment) => assignment switch
    {
        PlayerTetherAssignment.CloseInner => [0, 1],
        PlayerTetherAssignment.CloseOuter => [2, 3],
        PlayerTetherAssignment.CloseAny   => [0, 1, 2, 3],
        PlayerTetherAssignment.FarInner   => [4, 5],
        PlayerTetherAssignment.FarOuter   => [6, 7],
        PlayerTetherAssignment.FarAny     => [4, 5, 6, 7],
        _                                 => null,
    };

    // One of the four close slots for a job only one player can have. A seat that asked for it
    // takes it (earliest seat first); otherwise it goes to a slot nobody refused.
    private static int PickCloseSlot(Random rng, PartyRole[] roles, Dictionary<PartyRole, bool> want, params int[] taken)
    {
        var free = Enumerable.Range(0, 4).Where(i => !taken.Contains(i)).ToList();
        if (free.Count == 0) free = [0, 1, 2, 3];
        var claimed = free.Where(i => want.GetValueOrDefault(roles[i])).OrderBy(i => (int)roles[i]).ToList();
        if (claimed.Count > 0) return claimed[0];
        var allowed = free.Where(i => want.GetValueOrDefault(roles[i], true)).ToList();
        return allowed.Count > 0 ? allowed[rng.Next(allowed.Count)] : free[rng.Next(free.Count)];
    }

    private static Side RandomSide(Random rng) => rng.Next(2) == 0 ? Side.Left : Side.Right;

    private static PartyRole[] ShuffleRoles(Random rng)
    {
        var roles = (PartyRole[])Enum.GetValues(typeof(PartyRole));
        for (int i = roles.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (roles[i], roles[j]) = (roles[j], roles[i]);
        }

        return roles;
    }

    private static Side[] ShuffleSides(Random rng)
    {
        var sides = new[] { Side.Left, Side.Left, Side.Left, Side.Right, Side.Right, Side.Right };
        for (int i = sides.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (sides[i], sides[j]) = (sides[j], sides[i]);
        }

        return sides;
    }

    private static T[] ShuffleInPlace<T>(T[] values, Random rng)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }
}
