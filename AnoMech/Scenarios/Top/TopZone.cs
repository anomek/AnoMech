using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public sealed class TopZone : IZone
{
    public static readonly TopZone Instance = new();

    // Each slot's resting state as the real client holds it when the phase's scenarios start,
    // applied from the first frame so nothing plays its transition. Slots left out stay at the
    // layout default. Slots 1-8 are the eyes around the edge: the layout shows them all, the real
    // fight keeps them hidden until a mechanic opens one.
    private static readonly (byte Slot, ushort State)[] P1ArenaStates = [(0x00, 1), ..Hidden(0x01, 0x08), ..Hidden(0x0C, 0x13)];
    private static readonly (byte Slot, ushort State)[] P2ArenaStates =
        [(0x00, 1), ..Hidden(0x01, 0x08), (0x09, 4), (0x0A, 1), ..Hidden(0x0C, 0x13)];
    private static readonly (byte Slot, ushort State)[] P3IntermissionArenaStates =
        [(0x00, 1), (0x01, 1), ..Hidden(0x02, 0x08), (0x09, 4), (0x0A, 1), ..Hidden(0x0C, 0x13)];
    private static readonly (byte Slot, ushort State)[] P3ArenaStates =
        [(0x00, 1), ..Hidden(0x01, 0x0B), ..Hidden(0x0C, 0x13)];
    private static readonly (byte Slot, ushort State)[] P5ArenaStates =
        [(0x00, 1), ..Hidden(0x01, 0x0B), ..Hidden(0x0C, 0x14)];
    // The arena edge is down through P5's last cutscene; Alpha Omega raises it as it begins.
    private static readonly (byte Slot, ushort State)[] P6OpeningArenaStates =
        [(0x00, 4), ..Hidden(0x01, 0x0B), ..Hidden(0x0C, 0x14)];
    private static readonly (byte Slot, ushort State)[] P6ArenaStates =
        [(0x00, 1), ..Hidden(0x01, 0x0B), ..Hidden(0x0C, 0x14)];

    public static readonly Phase P1 = new(Instance, "P1", 77, BgmId.TopP1, clientSetup: world => InitArena(world, P1ArenaStates));
    public static readonly Phase P2 = new(Instance, "P2", 78, BgmId.TopP2, clientSetup: world => InitArena(world, P2ArenaStates));
    // Starts on P2's kill: P2's sky and track, until the scenario brings in P3's.
    public static readonly Phase P3Intermission = new(Instance, "P3", 78, BgmId.TopP2, clientSetup: world => InitArena(world, P3IntermissionArenaStates));
    public static readonly Phase P3 = new(Instance, "P3", 79, BgmId.TopP3, clientSetup: world => InitArena(world, P3ArenaStates));
    // Starts on P3's last moments, so it opens in P3's sky and arena, and P3's track plays on.
    public static readonly Phase P4 = new(Instance, "P4", 79, BgmId.TopP3, clientSetup: world => InitArena(world, P3ArenaStates));
    public static readonly Phase P5 = new(Instance, "P5", 174, BgmId.TopP5, clientSetup: world => InitArena(world, P5ArenaStates));
    public static readonly Phase P6 = new(Instance, "P6", 175, BgmId.TopP6, init: RaiseArenaEdge, clientSetup: world => InitArena(world, P6OpeningArenaStates));
    // A start past Alpha Omega's opening, with the arena edge already up.
    public static readonly Phase P6Continued = new(Instance, "P6", 175, BgmId.TopP6, clientSetup: world => InitArena(world, P6ArenaStates));

    public string Name => "The Omega Protocol";
    public uint TerritoryId => 1122;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => 90;
    public ushort ItemLevel => 365;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Ring", Core.Game.WaymarkPresets.Ring(13.63f))];

    // TOP's strats call the caster R1 and the physical ranged R2.
    public IReadOnlyList<PartyRole> SeatOrder { get; } =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.CasterDps, PartyRole.PhysRangedDps,
    ];

    public void Run(SimWorld world) => world.EnforceArenaBoundary(Geometry.ArenaRadius);

    private static IEnumerable<(byte Slot, ushort State)> Hidden(byte first, byte last)
    {
        for (var slot = first; slot <= last; slot++) yield return (slot, 4);
    }

    // Cosmo Meteor's arena change. Nothing turns it back before the territory reloads.
    public const byte CosmoMeteorArenaSlot = 0x15;

    // The state as both halves: the slot rests in it and plays it now, or once its SGB is ready. A
    // run in a zone an earlier one took past Cosmo Meteor starts without its arena change.
    // UNVERIFIED: that it rests in 4 before Cosmo Meteor, as the other scenery slots do while off.
    private static void InitArena(SimWorld world, (byte Slot, ushort State)[] states) => world.Events.Add(0f, () =>
    {
        foreach (var (slot, state) in states)
            world.Map.AddEffect(((uint)state << 16) | state, slot, broadcast: false);
        if (world.Map.IsInInstance && Natives.MapEffects.StateOf(CosmoMeteorArenaSlot) is not (null or 0 or 4))
            world.Map.AddEffect(0x00040004, CosmoMeteorArenaSlot, broadcast: false);
    });

    private static void RaiseArenaEdge(SimWorld world) => world.Events.Add(0.04f, () => world.Map.AddEffect(0x00020001, 0x00));
}
