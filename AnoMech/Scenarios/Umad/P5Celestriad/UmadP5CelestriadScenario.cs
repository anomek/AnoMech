using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Scenarios.Umad.P5Celestriad.UmadP5CelestriadConstants;

namespace AnoMech.Scenarios.Umad.P5Celestriad;

// UMAD P5 "Celestriad": all 9 towers (3 each of Fire/Ice/Lightning) spawn once and stay for the
// whole mechanic; each of the 3 sets lights up 4 of them (2 single-element towers plus a doubled
// element's 2). Each party member is permanently debuffed with an element (two each) or left
// "free" (two more). A debuffed player's actual soak target cycles through all 3 elements across
// the 3 sets (UmadP5CelestriadState.ElementForSet), never their debuff element until the final
// set, so nobody soaks the same element twice. Free players always fill the doubled element's
// second active tower. Sets 0 and 2 (the 1st and 3rd soaks) each get a single Catastrophic
// Choice cast while their towers are lit, and that set resolves exactly when the cast completes;
// set 1 has no Catastrophic Choice and resolves independently in between.
//
// See UmadP5CelestriadConstants for what's replay-confirmed vs. still an estimate.
public sealed class UmadP5CelestriadScenario : IScenario
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

    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
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
        state = new UmadP5CelestriadState(party, settingsWindow.Overrides);
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Lightning, StatusId.LightningResistanceDownII);
        damage.SetStatuses(DamageType.Fire, CelestriadStatusId.FireResistanceDownII);
        damage.SetStatuses(DamageType.Ice, CelestriadStatusId.IceResistanceDownII);
        towerInstances.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5CelestriadState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnKefka);
        world.Events.Add(CelestriadTiming.CelestriadCastAt,
            () => kefka?.Cast(CelestriadActionId.Celestriad, castSeconds: CelestriadTiming.CelestriadCastTime));
        world.Events.Add(CelestriadTiming.DebuffApplyAt, ApplyDebuffs);
        world.Events.Add(CelestriadTiming.TowerStart[0], SpawnAllTowers);

        for (var set = 0; set < 3; set++)
        {
            var s = set;
            world.Events.Add(CelestriadTiming.TowerStart[s], () => ActivateTowers(s));
            if (CelestriadTiming.CcAt[s] is { } cc) world.Events.Add(cc, () => LaunchChoice(s));
            world.Events.Add(CelestriadTiming.ResolveAt[s], () => ResolveSet(s));
            world.Events.Add(CelestriadTiming.DeactivateAt[s], () => DeactivateTowers(s));
        }

        var teardownAt = CelestriadTiming.DeactivateAt[2] + CelestriadTiming.TowerDespawnBuffer;
        world.Events.Add(teardownAt, DespawnAllTowers);
        world.Events.Add(teardownAt, () => kefka?.Despawn());
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
            IsVisible: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    // Silent: no cast or animation on the player when the initial debuff lands, just the status.
    private void ApplyDebuffs()
    {
        foreach (var (role, element) in state.PlayerDebuffElement)
        {
            if (element is not { } e) continue;
            party.Get(role)?.AddStatus(e.VulnUpStatusId, CelestriadTiming.DebuffDuration);
        }
    }

    // One cast per applicable set; that set resolves exactly when this cast completes.
    private void LaunchChoice(int set)
    {
        if (state.AeroVariant[set] is not { } choice || kefka is null) return;
        kefka.Cast(choice.CastActionId, castSeconds: CelestriadTiming.CatastrophicChoiceCastTime);
    }


    private void SpawnAllTowers()
    {
        foreach (var tower in state.AllTowers)
        {
            var eobj = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = tower.Element.TowerEObjId,
                Placement = new Placement(tower.Position, 0f),
                TimelineState = CelestriadTowerEObjId.DormantState,
            });
            var marker = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaHelper,
                NameId: BNpcNameId.Kefka,
                Level: 1,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: false,
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
                TimelineState = CelestriadTowerEObjId.ActiveState,
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
        {
            if (tower.ActiveOverlay is null) continue;

            var soakers = damage.Resolve(tower.Marker,
                tower.Element.TowerSoakedActionId, [tower.Element.DamageType],
                [(tower.Element.VulnUpStatusId, CelestriadTiming.DebuffDuration)],
                stackMinTargets: 2);
            if (soakers.Count == 0)
            {
                damage.Resolve(tower.Marker, tower.Element.TowerFailedActionId, [], [(StatusId.DamageDown, CelestriadTiming.DamageDownDuration)]);
                tower.Marker?.Cast(tower.Element.TowerFailedActionId);
            }
            else
            {
                tower.Marker?.Cast(tower.Element.TowerSoakedActionId);
            }
        }

        if (state.AeroVariant[set] is { } choice && kefka is not null)
        {
            damage.Resolve(kefka, choice.ResolveActionId, [DamageType.Lethal], [], size : 10f);
        }
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
}
