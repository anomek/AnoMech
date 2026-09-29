using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad;

public sealed class UmadZone : IZone
{
    public static readonly UmadZone Instance = new();

    public static bool SuppressP1Scenery;

    // Each slot's resting state as the real client holds it when the phase's scenarios start.
    // Applied from the first frame, they land before the SGBs stream in, so nothing plays its
    // transition. Deactivating the SharedGroups as well was tried: the engine re-activates them,
    // a visible frame each time.
    private static readonly ushort[] P1ArenaStates =
    [
        0x10, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 1,
        1, 1, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
    ];
    private static readonly ushort[] P2ArenaStates =
    [
        0x40, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 1,
        1, 1, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
    ];
    private static readonly ushort[] P3ArenaStates =
    [
        0x200, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 1,
        1, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
    ];
    private static readonly ushort[] P5ArenaStates =
    [
        0x800, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 1,
        1, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 4, 4,
    ];

    // 77 is the zone-in weather between pulls; a pull switches to 78.
    public const float P1Haze = 1000f;
    public static readonly Phase P1 = new(Instance, "P1", 78, 20291, clientSetup: world => InitArena(world, P1ArenaStates, SuppressP1Scenery));
    public static readonly Phase P2 = new(Instance, "P2", 79, 20292, clientSetup: world => InitArena(world, P2ArenaStates), envScene: 7);
    public static readonly Phase P3 = new(Instance, "P3", 174, 20293, clientSetup: world => InitArena(world, P3ArenaStates), envScene: 8);
    public static readonly Phase P4 = new(Instance, "P4", 174, 20293, clientSetup: world => InitArena(world, P3ArenaStates));
    public static readonly Phase P5 = new(Instance, "P5", 175, 20294, clientSetup: world => InitArena(world, P5ArenaStates));

    public string Name => "Dancing Mad";
    public uint TerritoryId => 1363;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => 100;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets => Waymarks;
    public IReadOnlyList<Vector3> ColliderRemovalPoints => [new(0f, 0f, -10f)];

    public void Run(SimWorld world) { }

    // Replay-derived RSV/RSF data the server would deliver in a real duty; peers need it too, or
    // their BgParts point at unseeded paths and render black. Idempotent.
    public void RunClientSetup(SimWorld world) => UmadReplayData.Seed();

    // The state as both halves: the slot rests in it and plays it now, or once its SGB is ready.
    private static void InitArena(SimWorld world, ushort[] states, bool suppressHidden = false) => world.Events.Add(0f, () =>
    {
        for (byte slot = 0; slot < states.Length; slot++)
        {
            var state = states[slot];
            world.Map.AddEffect(((uint)state << 16) | state, slot, broadcast: false);
            if (suppressHidden && slot > 0 && state == 4) world.Map.SuppressArenaSlot(slot);
        }
    });

    private static readonly IReadOnlyList<WaymarkLayout> Waymarks =
    [
        new WaymarkLayout("Diamond Waymarks", (IReadOnlyList<Waymark>)[
            new(WaymarkSlot.A,     new Vector3(  0, 0, -12)),
            new(WaymarkSlot.B,     new Vector3( 12, 0,   0)),
            new(WaymarkSlot.C,     new Vector3(  0, 0,  12)),
            new(WaymarkSlot.D,     new Vector3(-12, 0,   0)),
            new(WaymarkSlot.One,   new Vector3( -6, 0,  -6)),
            new(WaymarkSlot.Two,   new Vector3(  6, 0,  -6)),
            new(WaymarkSlot.Three, new Vector3(  6, 0,   6)),
            new(WaymarkSlot.Four,  new Vector3( -6, 0,   6)),
        ]),
        new WaymarkLayout("DN Zenith Waymarks",
        [
            new(WaymarkSlot.A,     new Vector3(    0, 0,   -12)),
            new(WaymarkSlot.B,     new Vector3(   12, 0,     0)),
            new(WaymarkSlot.C,     new Vector3(    0, 0,    12)),
            new(WaymarkSlot.D,     new Vector3(  -12, 0,     0)),
            new(WaymarkSlot.One,   new Vector3(-8.765f, 0, -8.765f)),
            new(WaymarkSlot.Two,   new Vector3( 8.628f, 0, -8.765f)),
            new(WaymarkSlot.Three, new Vector3( 8.628f, 0,  8.628f)),
            new(WaymarkSlot.Four,  new Vector3(-8.765f, 0,  8.628f)),
        ]),
        // A/B/C/D on cardinals 12y out, 1-4 on the corners (±12, ±12).
        new WaymarkLayout("[LPDU] Big Box",
        [
            new(WaymarkSlot.A,     new Vector3(  0, 0, -12)),
            new(WaymarkSlot.B,     new Vector3( 12, 0,   0)),
            new(WaymarkSlot.C,     new Vector3(  0, 0,  12)),
            new(WaymarkSlot.D,     new Vector3(-12, 0,   0)),
            new(WaymarkSlot.One,   new Vector3(-12, 0, -12)),
            new(WaymarkSlot.Two,   new Vector3( 12, 0, -12)),
            new(WaymarkSlot.Three, new Vector3( 12, 0,  12)),
            new(WaymarkSlot.Four,  new Vector3(-12, 0,  12)),
        ]),
    ];
}
