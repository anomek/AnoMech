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
// Choice cast while their towers are lit. A set's towers go off about 0.6s after that cast
// lands, and the Choice's Quake or Tornado 0.2-0.3s after the towers.
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
    // Aligned with state.AllTowers: active-tower selections use those indices.
    private readonly List<SimEventObject?> towers = [];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP5CelestriadState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        towers.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5CelestriadState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnKefka);
        world.Events.Add(1.0f, () => kefka?.Cast(ActionId.Celestriad, animationLock: 3.1f));
        world.Events.Add(6.1f, ApplyDebuffs);
        world.Events.Add(6.22f, SpawnAllTowers);
        world.Events.Add(9.05f, () => LightTowers(0));
        world.Events.Add(10.17f, () => LaunchChoice(0));
        world.Events.Add(15.06f, () => SpendTowers(0));
        world.Events.Add(15.06f, () => ResolveTowers(0));
        world.Events.Add(15.06f, () => LightTowers(1));
        world.Events.Add(15.26f, () => ResolveChoice(0));
        world.Events.Add(21.01f, () => SpendTowers(1));
        world.Events.Add(21.01f, () => LightTowers(2));
        world.Events.Add(21.11f, () => ResolveTowers(1));
        world.Events.Add(22.30f, () => LaunchChoice(2));
        world.Events.Add(27.10f, () => SpendTowers(2));
        world.Events.Add(27.10f, () => ResolveTowers(2));
        world.Events.Add(27.39f, () => ResolveChoice(2));
        world.Events.Add(29.44f, DespawnAll);
    }

    // UNVERIFIED delay: no pull leaves a Celestriad tower unsoaked.
    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.CelestriadFireIII, UmadActions.StardustFireIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadBlizzardIII, UmadActions.StardustBlizzardIII, minSoakers: 1);
        Towers.ResolveTower(world, resolved, ActionId.CelestriadThunderIII, UmadActions.StardustThunderIII, minSoakers: 1);
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

    private void LaunchChoice(int set)
    {
        if (state.AeroVariant[set] is not { } choice || kefka is null) return;
        kefka.Cast(choice.CastActionId, animationLock: 3.8f);
    }

    private void ResolveChoice(int set)
    {
        if (state.AeroVariant[set] is { } choice)
            kefka?.CastFromHelper(choice.Resolution, at: new Placement(Vector3.Zero, MathF.PI));
    }

    private void SpawnAllTowers()
    {
        foreach (var tower in state.AllTowers)
        {
            towers.Add(world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                LayoutId = EObjId.CelestriadTowerLayoutIds[tower.RingIndex],
                EventId = EObjId.PropEventId,
                TargetableStatus = 5,
                Arg2 = EObjId.CelestriadTowerArg2 + ((uint)tower.RingIndex << 16),
                Placement = new Placement(tower.Position, MathF.Atan2(-tower.Position.X, -tower.Position.Z)),
                TimelineState = EObjState.CelestriadTowerDormant,
            }));
        }
    }

    private void LightTowers(int set)
    {
        foreach (var towerIndex in state.SetActiveTowers[set])
            towers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerLit, EObjState.CelestriadTowerLitMask);
    }

    private void SpendTowers(int set)
    {
        foreach (var towerIndex in state.SetActiveTowers[set])
            towers[towerIndex]?.PlayBeat(EObjState.CelestriadTowerSpent, EObjState.CelestriadTowerSpentMask);
    }

    private void ResolveTowers(int set)
    {
        foreach (var towerIndex in state.SetActiveTowers[set])
        {
            var tower = state.AllTowers[towerIndex];
            kefka?.CastFromHelper(tower.Element.Tower, at: new Placement(tower.Position, 0f));
        }
    }

    private void DespawnAll()
    {
        foreach (var tower in towers) tower?.Despawn();
        towers.Clear();
        kefka?.Despawn();
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
