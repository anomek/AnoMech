using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class Side
{
    public uint DeltaOversampledWaveCannonActionId { get; }
    public uint SwivelCannonActionId { get; }
    public ushort MonitorDebuffId { get; }
    public int Mul { get; }

    private Side(uint waveCannonActionId, uint swivelCannonActionId, ushort monitorDebuffId, int mul)
    {
        DeltaOversampledWaveCannonActionId = waveCannonActionId;
        SwivelCannonActionId = swivelCannonActionId;
        MonitorDebuffId = monitorDebuffId;
        Mul = mul;
    }

    public static readonly Side Right = new(ActionId.DeltaOversampledWaveCannonRight, ActionId.SwivelCannonR, StatusId.PlayerMonitorRight, -1);
    public static readonly Side Left = new(ActionId.DeltaOversampledWaveCannonLeft, ActionId.SwivelCannonL, StatusId.PlayerMonitorLeft, 1);
}

// Where the eye opens. The whole layout is the north one turned clockwise by Rotation.
public sealed record EyeDirection(byte EffectIndex, float Rotation)
{
    public static readonly EyeDirection North = new(1, 0f);
    public static readonly EyeDirection East = new(3, MathF.PI / 2f);
    public static readonly EyeDirection South = new(5, MathF.PI);
    public static readonly EyeDirection West = new(7, MathF.PI * 1.5f);

    public static IReadOnlyList<EyeDirection> All { get; } = [North, East, South, West];

    public Placement Turn(Placement north) => north.RotateAroundOrigin(Rotation);
}

// Only the model and its warp timelines; which way an arm turns is rolled apart from it.
public sealed class ArmModel
{
    public uint BaseId { get; }
    public uint NameId { get; }
    public ushort SpawnTimeline { get; }
    public ushort WarpOutTimeline { get; }

    private ArmModel(uint baseId, uint nameId, ushort spawnTimeline, ushort warpOutTimeline)
    {
        BaseId = baseId;
        NameId = nameId;
        SpawnTimeline = spawnTimeline;
        WarpOutTimeline = warpOutTimeline;
    }

    public static readonly ArmModel Left = new(BNpcBaseId.LeftArmUnit, BNpcNameId.LeftArmUnit, TimelineId.Spawn2, TimelineId.WarpOut2);
    public static readonly ArmModel Right = new(BNpcBaseId.RightArmUnit, BNpcNameId.RightArmUnit, TimelineId.Spawn, TimelineId.WarpOut);
}

// Mul is the sign of each shot's turn in heading terms: clockwise lowers the heading.
public sealed class ArmRotation
{
    public uint IconId { get; }
    public int Mul { get; }

    private ArmRotation(uint iconId, int mul)
    {
        IconId = iconId;
        Mul = mul;
    }

    public static readonly ArmRotation Clockwise = new(LockonId.RotateCw, -1);
    public static readonly ArmRotation CounterClockwise = new(LockonId.RotateCcw, 1);
}

public sealed class TopP5DeltaState
{
    public IReadOnlyList<PartyRole> TetherOrder { get; }
    public EyeDirection EyeSpawn { get; }
    public IReadOnlyList<uint> FistColors { get; }    // length 8, each half has two BNpcBaseId.RocketPunchYellow and two RocketPunchBlue
    // Arm i stands at Geometry.ArmUnitPlacements[i], turned with the eye.
    public IReadOnlyList<ArmModel> ArmModels { get; }        // length 6, three Left and three Right
    public IReadOnlyList<ArmRotation> ArmRotations { get; }  // length 6, each rolled on its own
    public Side SwivelCannonSide { get; }
    public Side OmegaMonitorSide { get; }
    public Side PlayerMonitorSide { get; }
    public int PlayerMonitorIndex { get; }   // 0..3

    public PartyRole PlayerMonitorRole => TetherOrder[PlayerMonitorIndex];
    // One on each blue pair: 0..1 and 2..3.
    public int NearWorldTetherIndex { get; }
    public int FarWorldTetherIndex { get; }

    // The seat that asked to eat Beyond Defence, and the seats that asked not to. Both are
    // resolved at run start; who actually gets hit is picked at t=35.69s from proximity.
    public PartyRole? ForcedBeyondDefenceRole { get; }
    public IReadOnlyCollection<PartyRole> BeyondDefenceExcluded { get; }

    // Nullable -- null means "not resolved yet" (set only once FireBeyondDefense runs at
    // t=35.69s), distinguishable from any real PartyRole including the enum's default. The
    // re-broadcast poll for TopP5DeltaBeyondDefenseUpdateMessage depends on that distinction.
    public PartyRole? BeyondDefenseTarget { get; set; }
    public PartyRole NearWorldRole { get; set; }
    public PartyRole FarWorldRole { get; set; }

    public TopP5DeltaState(Rng rng, TopP5DeltaStateOverrides overrides, PartyRole playerRole)
    {
        var roles = ShuffleRoles(rng);

        var tethers = Requests(overrides.Tether, playerRole);
        var monitors = Requests(overrides.Monitor, playerRole);
        var hellos = Requests(overrides.HelloWorld, playerRole);
        var beyond = Requests(overrides.BeyondDefence, playerRole);

        SeatTethers(rng, roles, tethers, monitors, hellos, beyond);
        SeparateHelloWorldPairs(roles, tethers, monitors, hellos, beyond);
        if (overrides.TetherOrder is { } order) roles = order.ToArray();
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

        EyeSpawn = overrides.EyeSpawn ?? EyeDirection.All[rng.Next(EyeDirection.All.Count)];
        var models = ShuffleInPlace(new[] { ArmModel.Left, ArmModel.Left, ArmModel.Left, ArmModel.Right, ArmModel.Right, ArmModel.Right }, rng);
        var rotations = Enumerable.Range(0, 6).Select(_ => rng.Next(2) == 0 ? ArmRotation.Clockwise : ArmRotation.CounterClockwise).ToList();
        ArmModels = overrides.ArmModels ?? models;
        ArmRotations = overrides.ArmRotations ?? rotations;

        var colors = new uint[8];
        var first = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        var second = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        Array.Copy(first, 0, colors, 0, 4);
        Array.Copy(second, 0, colors, 4, 4);
        FistColors = overrides.FistColors ?? colors;

        SwivelCannonSide = overrides.SwivelCannonSide ?? RandomSide(rng);
        var omegaMonitor = RandomSide(rng);
        var playerMonitor = RandomSide(rng);
        OmegaMonitorSide = overrides.OmegaMonitorSide ?? omegaMonitor;
        PlayerMonitorSide = overrides.PlayerMonitorSide ?? playerMonitor;

        PlayerMonitorIndex = PickCloseSlot(rng, roles, monitors);

        // The one asked for is placed first, so the other comes from the remaining pair.
        int near, far;
        if (!wantsNear.ContainsValue(true) && wantsFar.ContainsValue(true))
        {
            far = PickCloseSlot(rng, roles, wantsFar);
            near = PickCloseSlot(rng, roles, wantsNear, far, far ^ 1);
        }
        else
        {
            near = PickCloseSlot(rng, roles, wantsNear);
            far = PickCloseSlot(rng, roles, wantsFar, near, near ^ 1);
        }
        NearWorldTetherIndex = near;
        FarWorldTetherIndex  = far;
        NearWorldRole = TetherOrder[near];
        FarWorldRole  = TetherOrder[far];

        // Only one player eats it, so the earliest seat that asked for it wins.
        ForcedBeyondDefenceRole = beyond.Where(r => r.Value).OrderBy(r => (int)r.Key).Select(r => (PartyRole?)r.Key).FirstOrDefault();
        BeyondDefenceExcluded = beyond.Where(r => !r.Value).Select(r => r.Key).ToList();
    }

    public int BeyondDefenseIndex()
    {
        return TetherOrder.Select((role, i) => (role, i))
                          .Where(t => t.role == BeyondDefenseTarget)
                          .Select(t => t.i)
                          .FirstOrDefault(0);
    }

    private static Dictionary<PartyRole, T> Requests<T>(PerRoleSetting<T> setting, PartyRole playerRole) where T : struct
        => setting.Resolve(playerRole).ToDictionary(x => x.Role, x => x.Value);

    // Slots 0-1 = close inner, 2-3 = close outer, 4-5 = far inner, 6-7 = far outer. Every seat
    // that asked for a band is swapped into it in seat order; a seat arriving at a band whose
    // slots are all claimed keeps whatever the shuffle gave it.
    private static void SeatTethers(
        Rng rng, PartyRole[] roles,
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

    // Near and Far are never on the same blue pair. When the two seats that asked for them share
    // one, whichever isn't held to its band trades places with an unheld seat of the other pair.
    private static void SeparateHelloWorldPairs(
        PartyRole[] roles,
        Dictionary<PartyRole, PlayerTetherAssignment> tethers,
        Dictionary<PartyRole, bool> monitors,
        Dictionary<PartyRole, HelloWorldOption> hellos,
        Dictionary<PartyRole, bool> beyond)
    {
        int SlotOf(HelloWorldOption option) => hellos.Where(h => h.Value == option).OrderBy(h => (int)h.Key)
                                                     .Select(h => Array.IndexOf(roles, h.Key)).DefaultIfEmpty(-1).First();
        bool Held(int slot) => EffectiveTether(roles[slot], tethers, monitors, hellos, beyond)
            is PlayerTetherAssignment.CloseInner or PlayerTetherAssignment.CloseOuter;

        var near = SlotOf(HelloWorldOption.Near);
        var far = SlotOf(HelloWorldOption.Far);
        if (near is < 0 or >= 4 || far is < 0 or >= 4 || near / 2 != far / 2) return;
        var otherPair = near / 2 == 0 ? new[] { 2, 3 } : new[] { 0, 1 };
        foreach (var mover in new[] { far, near }.Where(s => !Held(s)))
            foreach (var slot in otherPair.Where(s => !Held(s)))
            {
                (roles[mover], roles[slot]) = (roles[slot], roles[mover]);
                return;
            }
        DiagnosticLog.Info("[TopP5Delta] The Near and Far seats are both held to one blue pair; the Far request goes unmet.");
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

    internal static int[]? SlotsFor(PlayerTetherAssignment assignment) => assignment switch
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
    private static int PickCloseSlot(Rng rng, PartyRole[] roles, Dictionary<PartyRole, bool> want, params int[] taken)
    {
        var free = Enumerable.Range(0, 4).Where(i => !taken.Contains(i)).ToList();
        if (free.Count == 0) free = [0, 1, 2, 3];
        var claimed = free.Where(i => want.GetValueOrDefault(roles[i])).OrderBy(i => (int)roles[i]).ToList();
        if (claimed.Count > 0) return claimed[0];
        var allowed = free.Where(i => want.GetValueOrDefault(roles[i], true)).ToList();
        return allowed.Count > 0 ? allowed[rng.Next(allowed.Count)] : free[rng.Next(free.Count)];
    }

    // Network-replay constructor: reconstructs the fields TopP5DeltaAi reads. BeyondDefenseTarget
    // starts null -- not knowable at run start, set later via TopP5DeltaBeyondDefenseUpdateMessage
    // (same pattern as Umad P2 Forsaken's P2LockonsUpdateMessage). Side travels as a bool, the
    // eye as its index in EyeDirection.All. ArmModels/NearWorldTetherIndex/Beyond Defence
    // requests are harmless placeholders -- only the scenario's own host-only resolution reads them.
    private TopP5DeltaState(
        PartyRole[] tetherOrder, uint[] fistColors, int playerMonitorIndex,
        bool playerMonitorSideIsLeft, bool omegaMonitorSideIsLeft, int eyeSpawn,
        bool swivelCannonSideIsLeft, bool[] armRotatesClockwise, PartyRole farWorldRole,
        PartyRole nearWorldRole, int farWorldTetherIndex)
    {
        TetherOrder = tetherOrder;
        EyeSpawn = EyeDirection.All[eyeSpawn];
        FistColors = fistColors;
        ArmModels = [];
        ArmRotations = armRotatesClockwise.Select(clockwise => clockwise ? ArmRotation.Clockwise : ArmRotation.CounterClockwise).ToList();
        SwivelCannonSide = swivelCannonSideIsLeft ? Side.Left : Side.Right;
        OmegaMonitorSide = omegaMonitorSideIsLeft ? Side.Left : Side.Right;
        PlayerMonitorSide = playerMonitorSideIsLeft ? Side.Left : Side.Right;
        PlayerMonitorIndex = playerMonitorIndex;
        NearWorldTetherIndex = 0;
        FarWorldTetherIndex = farWorldTetherIndex;
        ForcedBeyondDefenceRole = null;
        BeyondDefenceExcluded = [];
        NearWorldRole = nearWorldRole;
        FarWorldRole = farWorldRole;
    }

    public static TopP5DeltaState? FromNetworkReplay(
        PartyRole[]? tetherOrder, uint[]? fistColors, int playerMonitorIndex,
        bool playerMonitorSideIsLeft, bool omegaMonitorSideIsLeft, int eyeSpawn,
        bool swivelCannonSideIsLeft, bool[]? armRotatesClockwise, PartyRole farWorldRole,
        PartyRole nearWorldRole, int farWorldTetherIndex)
    {
        if (tetherOrder is not { Length: 8 } || tetherOrder.Distinct().Count() != 8 || !tetherOrder.All(Enum.IsDefined)
            || fistColors is not { Length: 8 } || !fistColors.All(c => c is BNpcBaseId.RocketPunchYellow or BNpcBaseId.RocketPunchBlue)
            || armRotatesClockwise is not { Length: 6 }
            || eyeSpawn < 0 || eyeSpawn >= EyeDirection.All.Count
            || playerMonitorIndex is < 0 or >= 4 || farWorldTetherIndex is < 0 or >= 4
            || !Enum.IsDefined(farWorldRole) || !Enum.IsDefined(nearWorldRole))
            return null;
        return new(tetherOrder, fistColors, playerMonitorIndex, playerMonitorSideIsLeft, omegaMonitorSideIsLeft,
                   eyeSpawn, swivelCannonSideIsLeft, armRotatesClockwise, farWorldRole, nearWorldRole,
                   farWorldTetherIndex);
    }

    private static Side RandomSide(Rng rng) => rng.Next(2) == 0 ? Side.Left : Side.Right;

    private static PartyRole[] ShuffleRoles(Rng rng)
    {
        var roles = (PartyRole[])Enum.GetValues(typeof(PartyRole));
        for (int i = roles.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (roles[i], roles[j]) = (roles[j], roles[i]);
        }

        return roles;
    }

    private static T[] ShuffleInPlace<T>(T[] values, Rng rng)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }
}
