using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P5FulgentBlade.FruP5FulgentBladeState;
using Actions = AnoMech.Scenarios.Fru.FruActions;

namespace AnoMech.Scenarios.Fru.P5FulgentBlade;

// Pandora's exalines: three groups of two seams, each seam splitting into a light and a dark strip
// telegraphed by its first cast's omen and then rolling on as instant hits.
// Akh Morn closes it on one member of each light party.
public sealed class FruP5FulgentBladeScenario : IMultiplayerReplayable
{
    public string Name => "Fulgent Blade";
    public IPhase Phase => FruZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP5FulgentBladeAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly FruP5FulgentBladeSettingsWindow settingsWindow = new();

    private const uint SeamAppearState = 1;
    private const uint SeamAppearMask = 0x2;
    private const uint SeamArmState = 32;
    private const uint SeamArmMask = 0x10;
    private const uint SeamFireState = 4;
    private const uint SeamFireMask = 0x8;
    private const uint SeamArg2Base = 0x40;

    private SimWorld world = null!;
    private FruP5FulgentBladeState state = null!;
    // Polled by the multiplayer host; null until Run has rolled it.
    public FruP5FulgentBladeState? LastState { get; private set; }
    private SimEnemy? pandora;
    private readonly SimEventObject?[,] seams = new SimEventObject?[GroupCount, SeamsPerGroup];
    private SimCharacter? akhMornLeftTarget;
    private SimCharacter? akhMornRightTarget;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new FruP5FulgentBladeState(world.Rng, settingsWindow.Overrides);
        LastState = state;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<FruP5FulgentBladeState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(1f, () => pandora?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false));
        world.Events.Add(1.5f, () => pandora?.Cast(Actions.FulgentBlade));
        world.Events.Add(1.5f, () => pandora?.KeepFacing(world.Party.Get(PartyRole.MainTank)));
        world.Events.Add(1.5f, SpawnSeams);
        world.Events.Add(9.45f, () => BeatAllSeams(SeamAppearState, SeamAppearMask));
        world.Events.Add(10.65f, SwingAtTanks);
        world.Events.Add(11.35f, AutoAttackTanks);
        world.Events.Add(11.45f, () => BeatSeams(0, SeamArmState, SeamArmMask));
        world.Events.Add(11.55f, () => StartStrips(0));
        world.Events.Add(13.8f, SwingAtTanks);
        world.Events.Add(14.55f, AutoAttackTanks);
        world.Events.Add(15.45f, () => BeatSeams(1, SeamArmState, SeamArmMask));
        world.Events.Add(15.6f, () => StartStrips(1));
        world.Events.Add(16.9f, SwingAtTanks);
        world.Events.Add(17.7f, AutoAttackTanks);
        world.Events.Add(18.5f, () => BeatSeams(0, SeamFireState, SeamFireMask));
        world.Events.Add(19.45f, () => BeatSeams(2, SeamArmState, SeamArmMask));
        world.Events.Add(19.55f, () => StartStrips(2));
        world.Events.Add(20.05f, SwingAtTanks);
        world.Events.Add(20.75f, () => RemoveSeams(0));
        world.Events.Add(20.8f, AutoAttackTanks);
        world.Events.Add(22.55f, () => BeatSeams(1, SeamFireState, SeamFireMask));
        world.Events.Add(23.3f, SwingAtTanks);
        world.Events.Add(23.95f, AutoAttackTanks);
        world.Events.Add(24.75f, () => RemoveSeams(1));
        world.Events.Add(26.35f, () => pandora?.Cast(ActionId.AkhMornPandora, animationLock: 3.1f));
        world.Events.Add(26.5f, () => BeatSeams(2, SeamFireState, SeamFireMask));
        world.Events.Add(28.75f, () => RemoveSeams(2));
        for (var group = 0; group < GroupCount; group++)
            for (var hit = 1; hit < MaxHitCount; hit++)
            {
                var g = group;
                var h = hit;
                world.Events.Add(HitAt(g, h), () => RollStrips(g, h));
            }
        // TODO: solo has nobody to share the light-party stacks with; spawning support bots
        // mid-run isn't possible, so solo practice ends with the exalines.
        if (!solo)
        {
            world.Events.Add(34.05f, PickAkhMornTargets);
            world.Events.Add(34.45f, AkhMornOnLightParties);
        }
    }

    private void SpawnActors()
    {
        pandora = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Pandora,
            NameId: BNpcNameId.Pandora,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(Vector3.Zero, MathF.PI),
            LayoutId: LayoutId.PandoraP5));
    }

    private void SpawnSeams()
    {
        for (var group = 0; group < GroupCount; group++)
            for (var seam = 0; seam < SeamsPerGroup; seam++)
                seams[group, seam] = world.SpawnEventObject(new EventObjectSpawnConfig
                {
                    EObjId = EObjId.ExalineSeam,
                    Placement = state.Seam(group, seam),
                    EventId = DirectorEventId,
                    TargetableStatus = 5,
                    TimelineState = 4,
                    Arg2 = (SeamArg2Base + (uint)(group * SeamsPerGroup + seam)) << 16 | 3,
                });
    }

    private void BeatAllSeams(uint beatState, uint mask)
    {
        for (var group = 0; group < GroupCount; group++)
            BeatSeams(group, beatState, mask);
    }

    private void BeatSeams(int group, uint beatState, uint mask)
    {
        for (var seam = 0; seam < SeamsPerGroup; seam++)
            seams[group, seam]?.PlayBeat(beatState, mask);
    }

    private void RemoveSeams(int group)
    {
        for (var seam = 0; seam < SeamsPerGroup; seam++)
        {
            seams[group, seam]?.Despawn();
            seams[group, seam] = null;
        }
    }

    private void StartStrips(int group)
    {
        for (var seam = 0; seam < SeamsPerGroup; seam++)
        {
            pandora?.CastFromHelper(Actions.PathOfDarknessFirst, at: state.Strip(group, seam, false, 0)!.Value);
            pandora?.CastFromHelper(Actions.PathOfLightFirst, at: state.Strip(group, seam, true, 0)!.Value);
        }
    }

    private void RollStrips(int group, int hit)
    {
        for (var seam = 0; seam < SeamsPerGroup; seam++)
            for (var light = 0; light < 2; light++)
            {
                if (state.Strip(group, seam, light == 1, hit) is not { } placement) continue;
                pandora?.CastFromHelper(light == 1 ? Actions.PathOfLightRest : Actions.PathOfDarknessRest, at: placement);
            }
    }

    private void SwingAtTanks() => pandora?.Cast(ActionId.PandoraAttack, animationLock: 0.2f);

    // The hitting helpers stand on Pandora, facing her way.
    private void AutoAttackTanks()
    {
        if (world.Party.Get(PartyRole.MainTank) is { } mainTank) pandora?.CastFromHelper(Actions.PandoraAutoAttack, mainTank);
        if (world.Party.Get(PartyRole.OffTank) is { } offTank) pandora?.CastFromHelper(Actions.PandoraAutoAttack, offTank);
    }

    // As the bar ends: one random member of the four furthest to Pandora's left, and one of the
    // four furthest to her right.
    private void PickAkhMornTargets()
    {
        if (pandora is null) return;
        var byBearing = world.Party.Find.ByBearing(pandora.Placement());
        if (byBearing.Count == 0) return;
        var half = Math.Min(4, byBearing.Count);
        akhMornRightTarget = byBearing[Math.Min(state.AkhMornRightPick, half - 1)];
        akhMornLeftTarget = byBearing[byBearing.Count - 1 - Math.Min(state.AkhMornLeftPick, half - 1)];
    }

    private void AkhMornOnLightParties()
    {
        if (akhMornLeftTarget is { } left) pandora?.CastFromHelper(Actions.AkhMornPandoraLeft, left);
        if (akhMornRightTarget is { } right) pandora?.CastFromHelper(Actions.AkhMornPandoraRight, right);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new FruP5FulgentBladeAiReplayStateMessage(s.PositionIndex, s.RotationIndex, s.Mirrored) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not FruP5FulgentBladeAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = FruP5FulgentBladeState.FromNetworkReplay(msg.PositionIndex, msg.RotationIndex, msg.Mirrored);
        if (shadowState == null) return null;
        ((IScenarioAi<FruP5FulgentBladeState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
