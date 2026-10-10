using AnoMech.Core.EnemyActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P5Celestriad;

// UMAD P5 "Celestriad": all 9 towers (3 each of Fire/Ice/Lightning) spawn once and stay for the
// whole mechanic; each of the 3 sets lights up 4 of them (2 single-element towers plus a doubled
// element's 2). Each party member is permanently debuffed with an element (two each) or left
// "free" (two more). A debuffed player's actual soak target cycles through all 3 elements across
// the 3 sets (UmadP5CelestriadState.ElementForSet), never their debuff element until the final
// set, so nobody soaks the same element twice. Free players always fill the doubled element's
// second active tower. Sets 0 and 2 (the 1st and 3rd soaks) each get a single Catastrophic
// Choice cast while their towers are lit, and that set resolves exactly when the cast completes;
// set 1 has no Catastrophic Choice and resolves independently in between, on UNVERIFIED timing.
public sealed class UmadP5CelestriadScenario : IMultiplayerReplayable
{
    public string Name => "Celestriad";
    public IPhase Phase => UmadZone.P5;
    public bool SupportsSolo => false;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP5CelestriadAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly UmadP5CelestriadSettingsWindow settingsWindow = new();

    private UmadP5CelestriadState state = null!;

    // Polled by the multiplayer host; null until Run has rolled the set.
    public UmadP5CelestriadState? LastState { get; private set; }
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka;
    private sealed record TowerInstance(
        CelestriadElement Element,
        int SubIndex,
        SimEventObject? Tower,
        SimEventObject? ActiveOverlay,
        SimEnemy? Marker);
    private readonly List<TowerInstance> towerInstances = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP5CelestriadState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        towerInstances.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5CelestriadState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnKefka);
        world.Events.Add(1.0f, () => kefka?.Cast(ActionId.Celestriad, animationLock: 3.1f));
        world.Events.Add(6.1f, ApplyDebuffs);
        world.Events.Add(6.1f, SpawnAllTowers);
        world.Events.Add(6.1f, () => ActivateTowers(0));
        world.Events.Add(10.18f, () => LaunchChoice(0));
        world.Events.Add(14.18f, () => ResolveSet(0));
        world.Events.Add(14.3f, () => DeactivateTowers(0));
        world.Events.Add(14.4f, () => ActivateTowers(1));
        world.Events.Add(20.5f, () => ResolveSet(1));
        world.Events.Add(20.6f, () => DeactivateTowers(1));
        world.Events.Add(20.6f, () => ActivateTowers(2));
        world.Events.Add(22.34f, () => LaunchChoice(2));
        world.Events.Add(26.34f, () => ResolveSet(2));
        world.Events.Add(26.44f, () => DeactivateTowers(2));
        world.Events.Add(29.44f, DespawnAllTowers);
        world.Events.Add(29.44f, () => kefka?.Despawn());
    }

    public void Tick(float delta, float elapsed) { }

    private void SpawnKefka()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    // Silent: no cast or animation on the player when the initial debuff lands, just the status.
    private void ApplyDebuffs()
    {
        foreach (var (role, element) in state.PlayerDebuffElement)
        {
            if (element is not { } e) continue;
            party.Get(role)?.AddStatus(e.VulnUpStatusId, 20f);
        }
    }

    // One cast per applicable set; that set resolves exactly when this cast completes.
    private void LaunchChoice(int set)
    {
        if (state.AeroVariant[set] is not { } choice || kefka is null) return;
        kefka.Cast(choice.CastActionId, animationLock: 3.8f);
    }


    private void SpawnAllTowers()
    {
        foreach (var tower in state.AllTowers)
        {
            var eobj = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                Placement = new Placement(tower.Position, 0f),
                TimelineState = EObjState.CelestriadTowerDormant,
            });
            var marker = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaHelper,
                NameId: BNpcNameId.Kefka,
                Level: 1,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.InvisibleHelper,
                Placement: new Placement(tower.Position, 0f)));
            // Keep this aligned with state.AllTowers: active-tower selections use those indices.
            towerInstances.Add(new TowerInstance(tower.Element, tower.SubIndex, eobj, null, marker));
        }
    }

    private void ActivateTowers(int set)
    {
        foreach (var towerIndex in state.SetActiveTowers[set])
        {
            var tower = state.AllTowers[towerIndex];
            var overlay = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                Placement = new Placement(tower.Position, 0f),
                TimelineState = EObjState.CelestriadTowerActive,
            });
            if (overlay is null) continue;
            towerInstances[towerIndex] = towerInstances[towerIndex] with { ActiveOverlay = overlay };
        }
    }

    private void DeactivateTowers(int set)
    {
        foreach (var towerIndex in state.SetActiveTowers[set])
        {
            towerInstances[towerIndex].ActiveOverlay?.Despawn();
            towerInstances[towerIndex] = towerInstances[towerIndex] with { ActiveOverlay = null };
        }
    }

    private void ResolveSet(int set)
    {
        foreach (var tower in towerInstances)
            if (tower.ActiveOverlay is not null)
                tower.Marker?.Cast(tower.Element.Tower);

        if (state.AeroVariant[set] is { } choice)
            kefka?.Cast(choice.Resolution);
    }

    private void DespawnAllTowers()
    {
        foreach (var instance in towerInstances)
        {
            instance.Tower?.Despawn();
            instance.ActiveOverlay?.Despawn();
            instance.Marker?.Despawn();
        }
        towerInstances.Clear();
    }

    public MpMessage? BuildReplayStateMessage()
    {
        if (LastState is not { } s) return null;
        return new UmadP5CelestriadAiReplayStateMessage(
            s.DoubleElement.Select(UmadP5CelestriadState.ElementIndex).ToArray(),
            s.PlayerDebuffElement.ToDictionary(kv => kv.Key, kv => UmadP5CelestriadState.ElementIndex(kv.Value)),
            s.SetActiveTowers.Select(set => set.ToArray()).ToArray(),
            s.AeroVariant.Select(UmadP5CelestriadState.ChoiceIndex).ToArray(),
            s.TowerElementOrder.Select(UmadP5CelestriadState.ElementIndex).ToArray());
    }

    // The Ai schedules onto world.Events, which already ticks on a peer, so there is no replay
    // clock of its own to keep.
    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UmadP5CelestriadAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        var shadowState = UmadP5CelestriadState.FromNetworkReplay(
            msg.DoubleElement, msg.PlayerDebuffElement, msg.SetActiveTowers, msg.AeroVariant, msg.TowerElementOrder);
        if (shadowState == null) return null;
        ((IScenarioAi<UmadP5CelestriadState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
