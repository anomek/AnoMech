using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Umad.UmadConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Umad.P5Flood;

// UMAD P5 "Flood", the first exawave mechanic; its own scenario since it isn't adjacent to
// Exaflares in the real fight. Kefka casts FloodCast (a pure windup) while two crossing diagonal
// lines each march across the arena through 4 fixed points over 4 ticks (~1.02s apart), one
// diagonal per tick, alternating; each tick also lands ChaoticFlood, a 6y stack on a random
// non-tank.
public sealed class UmadP5FloodScenario : IMultiplayerReplayable
{
    public string Name => "Flood";
    public IPhase Phase => UmadZone.P5;
    // The real track starts 39.83s before the Flood cast.
    public float BgmSecondsAtStart => 38.83f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP5FloodAi()];

    private const byte Level = 100;

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly UmadP5FloodSettingsWindow settingsWindow = new();

    private UmadP5FloodState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka;

    // Kefka spawns at t=0; anything it does in its first 0.5s is dropped.
    private const float FloodCastStart = 1.0f;
    // internal: UmadP5FloodAi schedules against these, and hand-copied duplicates rotted once.
    internal const float FirstTelegraphAt = 1.1f;
    internal const float TelegraphStagger = 1.02f;
    // Waves land 6.05s after the pair's telegraph cast start; the tick's Chaotic Flood 0.12s
    // before the waves.
    internal const float ResolveDelayAfterTelegraph = 6.05f;
    private const float StackLeadBeforeWaves = 0.12f;
    internal const int TickCount = 4;
    // The last wave's travel-and-fade window, sized generously.
    private const float DespawnBuffer = 6.0f;

    // From the real ActionEffect headers: helper actions 1.1s, FloodCast 3.1s. Sent as 0, the
    // caster dropped out of its action state the frame the effect fired and the 3s caster-bound
    // wave VFX was cut.
    private const float HelperAnimationLock = 1.1f;
    private const float FloodCastAnimationLock = 3.1f;

    // Each diagonal has a fixed +-45 deg rotation, and its 4 anchors sit at along=-20 (the wall),
    // offset +-5 or +-15 from the centreline, so the near and far rect on one side tile edge to
    // edge. The 40 x 10 rect (anchor at the back edge) runs straight through the arena. Its helper
    // stands at the lane's midpoint and casts onto the anchor: the wave VFX plays on the helper,
    // and played from the wall half its 40y sweep would fall outside the arena. The near
    // point on one side always resolves with the far point on the other, then the remaining
    // pair; NW-SE mirrors NE-SW in X. Both hit VFX (z5r2_b2_g02/g04_c0v) are action vfx the
    // native ActionEffectHandler renders on its own once the caster has a real DrawObject.

    private const float Along = 20f;
    private const float NearOffset = 5f;
    private const float FarOffset = 15f;
    private const float NeSwRotation = MathF.PI / 4f;
    private const float NwSeRotation = -MathF.PI / 4f;
    private static readonly float InvSqrt2 = 1f / MathF.Sqrt(2f);

    // Inverse of along=(x+z)*InvSqrt2, perp=(x-z)*InvSqrt2; NW-SE negates X.
    private static Vector3 NeSwPoint(float perp) => new((-Along + perp) * InvSqrt2, 0f, (-Along - perp) * InvSqrt2);
    private static Vector3 NwSePoint(float perp) => new(-(-Along + perp) * InvSqrt2, 0f, (-Along - perp) * InvSqrt2);

    private static Vector3 LaneMiddle(Vector3 anchor, float rotation)
        => anchor + new Vector3(MathF.Sin(rotation), 0f, MathF.Cos(rotation)) * Along;

    // [A, C, B, D] so MarchPairs' Forward (0&2, 1&3) gives the real cross-pairing (A,B) then
    // (C,D); Reversed swaps which goes first. [Near, Far, -Near, -Far] pairs the two near points
    // into a solid band over the centreline and wipes the party on tick 1.
    private static readonly Vector3[] NeSwMarch =
    [
        NeSwPoint(NearOffset),   // A: perp=+5
        NeSwPoint(FarOffset),    // C: perp=+15
        NeSwPoint(-FarOffset),   // B: perp=-15
        NeSwPoint(-NearOffset),  // D: perp=-5
    ];
    private static readonly Vector3[] NwSeMarch =
    [
        NwSePoint(NearOffset),   // E (mirrors A)
        NwSePoint(FarOffset),    // H (mirrors C)
        NwSePoint(-FarOffset),   // F (mirrors B)
        NwSePoint(-NearOffset),  // G (mirrors D)
    ];

    public UmadP5FloodState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP5FloodState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        var solo = !selectedAi.HasValue;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5FloodState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnKefka);
        world.Events.Add(FloodCastStart, () => kefka?.Cast(ActionId.FloodCast, animationLock: FloodCastAnimationLock));

        var neSw = MarchPairs(NeSwMarch, state.NeSwReversed);
        var nwSe = MarchPairs(NwSeMarch, state.NwSeReversed);
        // The leading diagonal gets ticks 0&2, the other 1&3; rotation is fixed per diagonal.
        var (first, second, firstRot, secondRot) = state.NeSwFirst
            ? (neSw, nwSe, NeSwRotation, NwSeRotation)
            : (nwSe, neSw, NwSeRotation, NeSwRotation);
        Span<(Vector3 a, Vector3 b, float rotation)> ticks =
        [
            (first.pair1.Item1, first.pair1.Item2, firstRot),
            (second.pair1.Item1, second.pair1.Item2, secondRot),
            (first.pair2.Item1, first.pair2.Item2, firstRot),
            (second.pair2.Item1, second.pair2.Item2, secondRot),
        ];

        for (var tick = 0; tick < TickCount; tick++)
        {
            var tTelegraph = FirstTelegraphAt + tick * TelegraphStagger;
            var tResolve = tTelegraph + ResolveDelayAfterTelegraph;
            var (a, b, rotation) = ticks[tick];
            world.Events.Add(tTelegraph, () => TelegraphTick(a, b, rotation));
            if (!solo) world.Events.Add(tResolve - StackLeadBeforeWaves, ResolveChaoticFlood);
            world.Events.Add(tResolve, () => ResolveWaves(a, b, rotation));
        }

        var lastResolve = FirstTelegraphAt + (TickCount - 1) * TelegraphStagger + ResolveDelayAfterTelegraph;
        world.Events.Add(lastResolve + DespawnBuffer, DespawnAll);
    }

    // Host and peer alike: a peer's wave helpers replay the same gimmick timelines.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(WaveTimelines, "UmadP5Flood");
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new UmadP5FloodAiReplayStateMessage(s.NeSwReversed, s.NwSeReversed, s.NeSwFirst, s.StartQuadrant, s.RotationClockwise)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UmadP5FloodAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = UmadP5FloodState.FromNetworkReplay(msg.NeSwReversed, msg.NwSeReversed, msg.NeSwFirst, msg.StartQuadrant, msg.RotationClockwise);
        if (shadowState == null) return null;
        ((IScenarioAi<UmadP5FloodState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // pair1 = points[0]&points[2] (cross-paired outer/inner), pair2 = points[1]&points[3].
    // Reversed walks the march array backwards, which naturally starts pairing from the other end.
    private static ((Vector3, Vector3) pair1, (Vector3, Vector3) pair2) MarchPairs(Vector3[] points, bool reversed)
    {
        var p = reversed ? [points[3], points[2], points[1], points[0]] : points;
        return ((p[0], p[2]), (p[1], p[3]));
    }

    private void SpawnKefka()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            LayoutId: BNpcLayoutId.KefkaP5));
    }

    // The cast alone draws the telegraph (the native ActorCastPacket path spawns the sheet omen);
    // an explicit SpawnOmen on top drew a second one.
    private void TelegraphTick(Vector3 a, Vector3 b, float rotation)
    {
        kefka?.CastFromHelper(ActionId.FloodTelegraph, a, at: new Placement(LaneMiddle(a, rotation), rotation), animationLock: HelperAnimationLock);
        kefka?.CastFromHelper(ActionId.FloodTelegraph, b, at: new Placement(LaneMiddle(b, rotation), rotation), animationLock: HelperAnimationLock);
    }

    private void ResolveWaves(Vector3 a, Vector3 b, float rotation)
    {
        kefka?.CastFromHelper(UmadActions.FloodWave, a, at: new Placement(LaneMiddle(a, rotation), rotation));
        kefka?.CastFromHelper(UmadActions.FloodWave, b, at: new Placement(LaneMiddle(b, rotation), rotation));
    }

    // Cast from arena centre, where the real caster stands, onto a fresh non-tank each tick.
    private void ResolveChaoticFlood()
    {
        SimCharacter? target = null;
        var role = state.PickStackTarget();
        for (var attempt = 0; attempt < 8 && target is null; attempt++)
        {
            var candidate = party.Get(role);
            if (candidate is { } c && c.IsAlive()) target = c;
            else role = state.PickStackTarget();
        }
        if (target is null) return;
        kefka?.CastFromHelper(UmadActions.ChaoticFlood, target, at: new Placement(Vector3.Zero, MathF.PI));
    }

    // The gimmick timelines this mechanic plays (LoadType 0: not resident, not in Kefka's own
    // set); every run put 10690 in the base slot with its clock stuck at 0.00 before dropping it.
    private static readonly (ushort Id, string Key)[] WaveTimelines =
    [
        (10690, "mon_sp/gimmick/z5r2_boss2_gimmick02"),                    // FloodAOE AnimationEnd: the wave
        (10692, "mon_sp/gimmick/z5r2_boss2_gimmick04"),                    // ChaoticFlood AnimationEnd: the stack burst
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),      // FloodTelegraph AnimationEnd
    ];

    private void DespawnAll() => kefka?.Despawn();
}
