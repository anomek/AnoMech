using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P3Apocalypse.FruP3ApocalypseState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

public sealed class FruP3ApocalypseScenario : IMultiplayerReplayable
{
    public string Name => "Apocalypse";
    public IPhase Phase => FruZone.P3;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP3ApocalypseAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly FruP3ApocalypseSettingsWindow settingsWindow = new();

    private SimWorld world = null!;
    private FruP3ApocalypseState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it.
    public FruP3ApocalypseState? LastState { get; private set; }
    private SimEnemy? oracle;

    private readonly List<SimEventObject?> centerLights = [];
    private readonly SimEventObject?[] spokeLights = new SimEventObject?[8];
    private Vector3 danceLanding;

    private const uint LightBeatState = 4;
    // Bit n plays the light SGB's timeline n: shine_front_a1, front_a2, off, front_a3, right_a1,
    // right_a2, rleft_a1, rleft_a2. A spoke light curls toward its own right or left.
    private const uint CenterLightMask = 0x1;
    private const uint ClockwiseSpokeLightMask = 0x10;
    private const uint ClockwiseLastSpokeLightMask = 0x20;
    private const uint CounterclockwiseSpokeLightMask = 0x40;
    private const uint CounterclockwiseLastSpokeLightMask = 0x80;
    private const uint LightArg2Base = 0x40;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP3ApocalypseState(world.Rng, world.Party.PlayerRole, settingsWindow.Overrides);
        LastState = state;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<FruP3ApocalypseState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(1f, () => oracle?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
        world.Events.Add(2.1f, () => oracle?.VoiceLine(VoiceLineId.SpellInWaitingRefrain));
        world.Events.Add(2.1f, () => oracle?.Cast(ActionId.SpellInWaitingRefrain, animationLock: 3.1f));
        world.Events.Add(4.9f, () => oracle?.AddStatus(StatusId.SpellInWaitingRefrain, sourceObject: oracle.GameObjectId));
        world.Events.Add(7.2f, () => oracle?.Cast(ActionId.DarkWaterCast, animationLock: 3.1f));
        world.Events.Add(7.2f, () => MarkWaterHolders(LockonId.Stack, 1, 2, 3));
        world.Events.Add(12.3f, TargetWaterHolders);
        world.Events.Add(WaterAppliedAt, ApplyDarkWater);
        world.Events.Add(13f, () => oracle?.RemoveStatus(StatusId.SpellInWaitingRefrain, oracle.GameObjectId));
        world.Events.Add(15.3f, OracleBattleTalk);
        world.Events.Add(15.3f, () => oracle?.Cast(ActionId.ApocalypseCast, animationLock: 2.1f));
        world.Events.Add(15.8f, SpawnLights);
        world.Events.Add(WaterMarkerAt(1), () => MarkWaterHolders(LockonId.WaterCountdown, 1));
        world.Events.Add(20.1f, () => world.Map.AddEffect(MapEffect.OracleFloorApocalypse, MapEffect.OracleFloor));
        world.Events.Add(21.5f, () => oracle?.Cast(ActionId.SpiritTakerCast, animationLock: 0.2f));
        for (var beat = 0; beat < LightBeatCount; beat++)
        {
            var b = beat;
            world.Events.Add(LightBeatAt(b), () => BeatLights(b));
        }
        world.Events.Add(WaterAt(1), () => DarkWater(1));
        world.Events.Add(24.8f, () => oracle?.Cast(Actions.SpiritTaker, world.Party.Get(state.SpiritTakerTarget)));
        world.Events.Add(27.6f, () => oracle?.Cast(ActionId.OracleReposition, animationLock: 1.1f));
        world.Events.Add(28.7f, () => oracle?.SetRotation(MathF.PI));
        world.Events.Add(30.9f, () => oracle?.Cast(ActionId.DarkEruptionCast, animationLock: 3.1f));
        world.Events.Add(30.9f, () => world.Party.ActiveMembers().ToList().ForEach(m => m.AttachLockonVfx(LockonId.Spread)));
        for (var wave = 0; wave < WaveCount; wave++)
        {
            var w = wave;
            world.Events.Add(WaveAt(w), () => Explode(w));
        }
        world.Events.Add(WaterMarkerAt(2), () => MarkWaterHolders(LockonId.WaterCountdown, 2));
        world.Events.Add(36f, DarkEruption);
        world.Events.Add(38.5f, () => oracle?.Cast(ActionId.DarkestDanceCast, animationLock: 0.2f));
        world.Events.Add(WaterAt(2), () => DarkWater(2));
        world.Events.Add(44f, DarkestDance);
        world.Events.Add(45.1f, () => oracle?.SetPosition(danceLanding));
        world.Events.Add(45.25f, () => world.Map.AddEffect(MapEffect.OracleFloorAfterApocalypse, MapEffect.OracleFloor));
        world.Events.Add(45.5f, RemoveLights);
        world.Events.Add(WaterMarkerAt(3), () => MarkWaterHolders(LockonId.WaterCountdown, 3));
        world.Events.Add(46.8f, () => oracle?.Cast(Actions.DarkestDanceKnockback));
        world.Events.Add(WaterAt(3), () => DarkWater(3));
        world.Events.Add(51.1f, () => oracle?.VoiceLine(VoiceLineId.ShockwavePulsar));
        world.Events.Add(51.2f, () => oracle?.Cast(Actions.ShockwavePulsar));
    }

    private void SpawnActors()
    {
        oracle = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OracleP3,
            NameId: BNpcNameId.Oracle,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            LayoutId: LayoutId.OracleP3,
            Appearance: Appearance.Oracle));
    }

    private void MarkWaterHolders(uint lockonId, params int[] durations)
    {
        foreach (var duration in durations)
            foreach (var role in state.WaterHolders(duration))
                if (world.Party.Get(role) is { } member && member.IsAlive()) member.AttachLockonVfx(lockonId);
    }

    private void TargetWaterHolders()
    {
        foreach (var holder in Enumerable.Range(1, 3).SelectMany(state.WaterHolders).Select(world.Party.Get))
            if (holder is not null && holder.IsAlive())
                oracle?.CastFromHelper(ActionId.DarkWaterTarget, holder, animationLock: 1.1f);
    }

    private void ApplyDarkWater()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (state.WaterDuration(role) is > 0 and var duration && world.Party.Get(role) is { } member && member.IsAlive())
            {
                member.AddStatus(StatusId.DarkWaterWaiting, WaterAt(duration) - WaterAppliedAt);
                member.AddStatus(StatusId.DarkWaterMarkerDelay, WaterMarkerAt(duration) - WaterAppliedAt);
            }
    }

    private void OracleBattleTalk()
    {
        if (oracle is { } boss) world.Map.DirectorUpdate(0x80000027U, 0xBU, 0x2U, BNpcNameId.Oracle, boss.EntityId);
    }

    // Center lights face the first spokes; UNVERIFIED for Rotation 1.
    private void SpawnLights()
    {
        var centerRotation = -state.Rotation * MathF.PI / 4;
        centerLights.Clear();
        centerLights.Add(SpawnLight(0, new Placement(Vector3.Zero, centerRotation)));
        centerLights.Add(SpawnLight(1, new Placement(Vector3.Zero, centerRotation + MathF.PI)));
        var spokeTangent = state.Clockwise ? MathF.PI / 2 : -MathF.PI / 2;
        for (var spoke = 0; spoke < spokeLights.Length; spoke++)
            spokeLights[spoke] = SpawnLight(2 + spoke, new Placement(SpokePosition(spoke), spokeTangent - spoke * MathF.PI / 4));
    }

    private SimEventObject? SpawnLight(int index, Placement placement) => world.SpawnEventObject(new EventObjectSpawnConfig
    {
        EObjId = EObjId.ApocalypseLight,
        Placement = placement,
        EventId = DirectorEventId,
        TargetableStatus = 5,
        TimelineState = 4,
        Arg2 = (LightArg2Base + (uint)index) << 16 | 3,
    });

    private void BeatLights(int beat)
    {
        if (beat < 2)
            foreach (var light in centerLights) light?.PlayBeat(LightBeatState, CenterLightMask);
        var last = beat == LightBeatCount - 1;
        var mask = state.Clockwise
            ? last ? ClockwiseLastSpokeLightMask : ClockwiseSpokeLightMask
            : last ? CounterclockwiseLastSpokeLightMask : CounterclockwiseSpokeLightMask;
        foreach (var step in LitSteps(beat))
        {
            spokeLights[state.Spoke(step)]?.PlayBeat(LightBeatState, mask);
            spokeLights[(state.Spoke(step) + 4) % 8]?.PlayBeat(LightBeatState, mask);
        }
    }

    private void RemoveLights()
    {
        foreach (var light in centerLights.Concat(spokeLights)) light?.Despawn();
        centerLights.Clear();
        Array.Clear(spokeLights);
    }

    private void DarkWater(int duration)
    {
        foreach (var holder in state.WaterHolders(duration).Select(world.Party.Get))
            if (holder is not null && holder.IsAlive())
                oracle?.CastFromHelper(Actions.DarkWater, holder);
    }

    private void Explode(int wave)
    {
        foreach (var position in state.Explosions(wave))
            oracle?.CastFromHelper(Actions.Apocalypse, at: new Placement(position, 0f));
    }

    // Retail casts the targeted helper actions from where the Oracle stands.
    private void DarkEruption()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
            oracle?.CastFromHelper(Actions.DarkEruption, member);
    }

    private void DarkestDance()
    {
        if (oracle is not { } boss) return;
        var target = world.Party.ActiveMembers().MaxBy(m => Vector3.DistanceSquared(m.Position, boss.Position));
        if (target is null) return;
        boss.Cast(Actions.DarkestDance, target);
        danceLanding = DanceLanding(boss.Position, target.Position);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new FruP3ApocalypseAiReplayStateMessage(s.WaterDurations.ToArray(), s.Rotation, s.Clockwise) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP3ApocalypseAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP3ApocalypseState.FromNetworkReplay(msg.WaterDurations, msg.Rotation, msg.Clockwise);
        if (shadowState == null) return null;
        ((IScenarioAi<FruP3ApocalypseState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
