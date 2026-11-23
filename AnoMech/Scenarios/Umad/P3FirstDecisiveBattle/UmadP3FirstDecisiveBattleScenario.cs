using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Scenarios.Umad.P3LimitCut;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;
using A = AnoMech.Scenarios.Umad.P3FirstDecisiveBattle.UmadP3FirstDecisiveBattleActions;

namespace AnoMech.Scenarios.Umad.P3FirstDecisiveBattle;

public sealed class UmadP3FirstDecisiveBattleScenario : IScenario
{
    public string Name => "First Decisive Battle";
    public IPhase Phase => UmadZone.P3;
    public float BgmSecondsAtStart => 37.27f;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP3FirstDecisiveBattleAi()];
    public object SettingsOverrides => settings;
    private readonly UmadP3FirstDecisiveBattleStateOverrides settings = new();
    private SimWorld world = null!;
    private UmadP3FirstDecisiveBattleState state = null!;
    private SimEnemy? kefka, thunderAnchor;
    private readonly UmadP3LimitCutScenario limitCut = new();
    private bool limitCutActive;
    public bool HasPerPlayerSettings => true;
    public IReadOnlyList<string> SettingsConflicts => limitCut.SettingsConflicts;
    public void DrawPerPlayerSettings()
    {
        var cut = (UmadP3LimitCutStateOverrides)limitCut.SettingsOverrides;
        var number = cut.Number.Mine ?? 0;
        if (ImGui.Combo("Your Blaster number", ref number, new[] { "Random", "1", "2", "3", "4", "5", "6", "7", "8" }, 9))
            cut.Number.Mine = number == 0 ? null : number;
        ImGui.TextWrapped("Wind direction carries over from Bowels of Agony.");
    }
    private SimEventObject? fireCrystal, waterCrystal, windCrystal;
    private readonly SimEnemy?[] cycloneHelpers = new SimEnemy?[8];

    public void DrawSettings()
    {
        var cut = (UmadP3LimitCutStateOverrides)limitCut.SettingsOverrides;
        var first = (settings.BlasterStartSpot ?? -1) + 1;
        if (ImGui.Combo("Blaster first arrival", ref first, new[] { "Random", "S", "SE", "E", "NE", "N", "NW", "W", "SW" }, 9))
            settings.BlasterStartSpot = first == 0 ? null : first - 1;
        Choice("Blaster appearance order", settings.BlasterClockwise, value => settings.BlasterClockwise = value, "Clockwise", "Counter-clockwise");
        var corner = (settings.WindCorner ?? -1) + 1;
        if (ImGui.Combo("Wind crystal", ref corner, new[] { "Random", "NE", "SE", "SW", "NW" }, 5))
            settings.WindCorner = corner == 0 ? null : corner - 1;
        Choice("Fire / water", settings.Mirror, value => settings.Mirror = value, "Fire CCW of wind", "Fire CW of wind");
        Choice("First element", settings.FireFirst, value => settings.FireFirst = value, "Fire", "Water");
        Choice("Wind direction", settings.Headwind, value => settings.Headwind = value, "Headwind (back)", "Tailwind (front)");
        if (ImGui.Button("Reset all to random"))
        {
            settings.BlasterStartSpot = null; settings.BlasterClockwise = null; cut.Number.Clear();
            settings.WindCorner = null; settings.Mirror = null; settings.FireFirst = null; settings.FrontFirst = null; settings.Headwind = null;
        }
        ImGui.TextWrapped("Thunder III: black absorption tether / 7s = large circle; no tether / 5s = two nearest-player tankbusters. Start includes 2 seconds to regroup.");
        Choice("Implosion", settings.FrontFirst, value => settings.FrontFirst = value, "Front / back first", "Sides first");
        ImGui.TextWrapped("Yoruha: fixed positions relative to wind and water. D3 baits Umbra Smash. Face for Headwind / Tailwind; tank LB3 at 70% Vacuum Wave.");
        ImGui.TextWrapped("Thunder busters: first set MT invulns both; second set MT then ST. Human tanks use their own mitigation.");
        ImGui.TextWrapped("After grouping, pull each boss centre to arena centre first: tanks stand beyond centre by the boss hitbox plus their own radius.");
        ImGui.TextWrapped("When playing a tank, use LB3 yourself. Other roles: bot tanks use LB3 automatically.");
        ImGui.TextWrapped("Timeline is based on reference intervals; in-game timing and visuals need validation.");
    }

    private static void Choice(string label, bool? value, Action<bool?> set, string yes, string no)
    {
        var index = value switch { true => 1, false => 2, _ => 0 };
        if (ImGui.Combo(label, ref index, new[] { "Random", yes, no }, 3))
            set(index switch { 1 => true, 2 => false, _ => null });
    }

    public void RunInstanceEvents(SimWorld instanceWorld) => instanceWorld.Party.LimitBreak.Set(3f);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        limitCutActive = false;
        state = new UmadP3FirstDecisiveBattleState(world.Rng, settings);
        Spawn();
        if (selectedAi is { } index && index == 0)
            ((IScenarioAi<UmadP3FirstDecisiveBattleState>)AiStrats[index]).Run(state, world);
        world.Events.Add(2.2f, () =>
        {
            state.Chaos?.Cast(ActionId.DecisiveBattle_Chaos);
            state.Exdeath?.Cast(ActionId.DecisiveBattle_Exdeath);
        });
        world.Events.Add(5.2f, ResolveDecisiveBattle);
        world.Events.Add(5.3f, () => { state.Chaos?.Follow(world.Party.Get(PartyRole.OffTank)); state.Exdeath?.Follow(world.Party.Get(PartyRole.MainTank)); });
        world.Events.Add(19.7f, () => state.Chaos?.Cast(A.Bowels));
        world.Events.Add(24.7f, ApplyElements);
        world.Events.Add(36.9f, () =>
        {
            foreach (var member in world.Party.AllMembers())
            {
                member.RemoveStatus(StatusId.EpicHero);
                member.RemoveStatus(StatusId.FatedHero);
            }
            state.Chaos?.RemoveStatus(StatusId.EpicVillain);
            state.Exdeath?.RemoveStatus(StatusId.FatedVillain);
            world.Tether(state.Exdeath, thunderAnchor, 64, duration: 7f);
            state.Exdeath?.Cast(A.ThunderCircle);
        });
        world.Events.Add(44.2f, () => ResolveElements(state.FireFirst));
        world.Events.Add(45.2f, () => ResolveCrystal(state.FireFirst));

        world.Events.Add(48.4f, () =>
        {
            state.Exdeath?.Follow();
            state.Exdeath?.Cast(ActionId.ThunderIII_Cast);
        });
        world.Events.Add(53.4f, ResolveThunder);
        world.Events.Add(56.5f, ResolveThunder);
        world.Events.Add(56.9f, () => state.Exdeath?.Follow(world.Party.Get(PartyRole.MainTank)));
        world.Events.Add(61.3f, () =>
        {
            state.Chaos?.Follow();
            state.Chaos?.Cast(state.FrontFirst ? ActionId.LongitudinalImplosion : ActionId.LatitudinalImplosion);
        });
        world.Events.Add(67.0f, () => ResolveImplosion(state.FrontFirst));
        world.Events.Add(69.0f, () => ResolveImplosion(!state.FrontFirst));
        world.Events.Add(69.6f, () => state.Chaos?.Follow(world.Party.Get(PartyRole.OffTank)));
        world.Events.Add(71.2f, () => ResolveElements(!state.FireFirst));
        world.Events.Add(72.2f, () => ResolveCrystal(!state.FireFirst));
        world.Events.Add(73.4f, () =>
        {
            var cut = (UmadP3LimitCutStateOverrides)limitCut.SettingsOverrides;
            cut.StartSpot = settings.BlasterStartSpot; cut.Clockwise = settings.BlasterClockwise;
            limitCutActive = true;
            var spot = (3 - 2 * state.WindCorner + 8) % 8;
            var winds = state.Headwinds.ToDictionary(kv => kv.Key, kv => kv.Value ? Wind.Headwind : Wind.Tailwind);
            limitCut.RunContinuation(world, selectedAi, new(kefka, state.Chaos, state.Exdeath, windCrystal, winds, spot));
        });
    }

    private void Spawn()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP3, NameId: BNpcNameId.Kefka, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Always, LayoutId: BNpcLayoutId.KefkaP3,
            Placement: new Placement(new Vector3(0, 0, -10), 0)));
        state.Chaos = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100,
            LayoutId: BNpcLayoutId.ChaosP3, Targetable: true, EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(-10, 0, 0), MathF.PI / 2)));
        state.Exdeath = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100,
            LayoutId: BNpcLayoutId.Exdeath, Targetable: true, EnemyList: EnemyListMode.Always,
            Placement: new Placement(new Vector3(10, 0, 0), -MathF.PI / 2)));
        thunderAnchor = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.GravenImage, NameId: BNpcNameId.GravenImage,
            Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(new Vector3(0, 0, -25), 0)));
        for (var i = 0; i < cycloneHelpers.Length; i++)
            cycloneHelpers[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Chaos, NameId: BNpcNameId.Chaos, Level: 1,
                Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.HiddenUntilShown,
                Placement: new Placement(state.WindCrystal, 0)));
    }

    private void ResolveDecisiveBattle()
    {
        if (state.Chaos is not { } chaos || state.Exdeath is not { } exdeath) return;
        chaos.AddStatus(StatusId.EpicVillain);
        exdeath.AddStatus(StatusId.FatedVillain);
        var chaosParty = world.Party.ActiveMembers().OrderBy(m => Vector3.DistanceSquared(m.Position, chaos.Position)).Take(4).ToHashSet();
        foreach (var member in world.Party.ActiveMembers())
            member.AddStatus(chaosParty.Contains(member) ? StatusId.EpicHero : StatusId.FatedHero);
    }

    private void ApplyElements()
    {
        fireCrystal = Crystal(2015290, state.FireCrystal);
        waterCrystal = Crystal(2015291, state.WaterCrystal);
        var layout = new uint[] { EObjId.WindCrystalNeLayoutId, EObjId.WindCrystalSeLayoutId,
            EObjId.WindCrystalSwLayoutId, EObjId.WindCrystalNwLayoutId }[state.WindCorner];
        windCrystal = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.WindCrystal, LayoutId = layout, TargetableStatus = 5, Arg2 = EObjId.WindCrystalArg2,
            Placement = new Placement(state.WindCrystal, 0),
        });
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member) continue;
            member.AddStatus(state.Headwinds[role] ? StatusId.Headwind : StatusId.Tailwind, 68f);
            if (state.Fire.Contains(role)) member.AddStatus(A.Entropy, state.FireFirst ? 19.5f : 46.5f);
            if (state.Water.Contains(role)) member.AddStatus(A.Fluid, state.FireFirst ? 46.5f : 19.5f);
        }
    }

    private SimEventObject? Crystal(uint id, Vector3 position)
        => world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = id, TargetableStatus = 5, Placement = new Placement(position, 0) });

    private void ResolveElements(bool fire)
    {
        foreach (var role in fire ? state.Fire : state.Water)
            if (world.Party.Get(role) is { } member && member.IsAlive())
                state.Chaos?.CastFromHelper(fire ? A.Fire : A.Water, member, at: member.Placement());
    }

    private void ResolveCrystal(bool fire)
    {
        var position = fire ? state.FireCrystal : state.WaterCrystal;
        foreach (var target in world.Party.Find.ClosestN(position, 2))
            state.Chaos?.CastFromHelper(A.CrystalWithReaction(fire, QueueCyclones), target, at: target.Placement());
        if (fire) fireCrystal?.FadeOut();
        else waterCrystal?.FadeOut();
    }

    private void ResolveThunder()
    {
        if (state.Exdeath is not { } boss) return;
        boss.CastFromHelper(UmadActions.ThunderIII, world.Party.Find.Closest(boss.Position), at: boss.Placement());
    }

    private void ResolveImplosion(bool front)
    {
        if (state.Chaos is not { } boss) return;
        var heading = boss.Rotation + (front ? 0 : MathF.PI / 2);
        boss.CastFromHelper(UmadActions.ImplosionShockwave, at: new Placement(boss.Position, heading));
        boss.CastFromHelper(UmadActions.ImplosionShockwave, at: new Placement(boss.Position, heading + MathF.PI));
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        if (limitCutActive) { limitCut.OnActionResolved(resolved); return; }
        if (resolved.Action.ActionId != ActionId.VacuumWave) return;
        var winds = resolved.Hits.Count(m => A.ConsumesWind(m, resolved.Origin.Position));
        if (winds > 0) QueueCyclones(winds);
    }

    private void QueueCyclones(int count) => world.Events.Add(4.14f, () => FireCyclones(count));

    private void FireCyclones(int count)
    {
        var targets = world.Party.Find.ClosestN(state.WindCrystal, count);
        for (var i = 0; i < targets.Count; i++)
            cycloneHelpers[i]?.Cast(UmadActions.Cyclone, targets[i]);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (limitCutActive) { limitCut.OnStatusLost(lost); return; }
        if (lost is { StatusId: StatusId.Headwind or StatusId.Tailwind, Reason: StatusLossReason.Expired })
            state.Chaos?.CastFromHelper(UmadActions.StrayGusts, lost.Holder);
    }
}
