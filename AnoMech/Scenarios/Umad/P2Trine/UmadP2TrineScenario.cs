using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;
using A = AnoMech.Scenarios.Umad.P2Trine.UmadP2TrineActions;

namespace AnoMech.Scenarios.Umad.P2Trine;

public sealed class UmadP2TrineScenario : IScenario
{
    public string Name => "Trine";
    public IPhase Phase => UmadZone.P2;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP2TrineAi()];
    public object SettingsOverrides => settings;
    private readonly UmadP2TrineStateOverrides settings = new();
    private SimWorld world = null!;
    private UmadP2TrineState state = null!;
    private SimEnemy? kefka;
    private readonly SimEventObject?[] triangles = new SimEventObject?[7];
    internal static readonly float[] WaveAt = [15.8f, 17.8f, 19.9f];
    internal const float BusterAt = 20.3f;

    public void DrawSettings()
    {
        var pattern = (settings.Pattern ?? -1) + 1;
        if (ImGui.Combo("Triangle order", ref pattern, new[] { "Random", "Guide pattern 1", "Guide pattern 2", "Guide pattern 3" }, 4))
            settings.Pattern = pattern == 0 ? null : pattern - 1;
        var wing = settings.LeftWing switch { true => 1, false => 2, _ => 0 };
        if (ImGui.Combo("Wings", ref wing, new[] { "Random", "Left attack", "Right attack" }, 3))
            settings.LeftWing = wing switch { 1 => true, 2 => false, _ => null };
        ImGui.TextWrapped("Yoruha: dodge the wing, then enter cleared first-wave triangles. MT closest, ST furthest. Tanks prefer north / west; everyone else south / east.");
        ImGui.TextWrapped("Supports the three guide layouts. Exact in-game visual timing is still awaiting validation.");
    }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new UmadP2TrineState(world.Rng, settings);
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.GodKefka, NameId: BNpcNameId.Kefka, Level: 100,
            Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible,
            LayoutId: BNpcLayoutId.GodKefka, Placement: new Placement(Vector3.Zero, 0f)));
        if (selectedAi is 0) ((IScenarioAi<UmadP2TrineState>)AiStrats[0]).Run(state, world);
        world.Events.Add(0f, () => kefka?.Cast(A.CastId));
        world.Events.Add(4.5f, () => SpawnWave(0));
        world.Events.Add(4.9f, () => ShowWave(0));
        world.Events.Add(5.1f, () => AnimateWave(0));
        world.Events.Add(6.1f, () => kefka?.Cast(A.Wing(state.LeftWing)));
        world.Events.Add(6.5f, () => SpawnWave(1));
        world.Events.Add(6.9f, () => ShowWave(1));
        world.Events.Add(7.1f, () => AnimateWave(1));
        world.Events.Add(8.6f, () => SpawnWave(2));
        world.Events.Add(9.0f, () => ShowWave(2));
        world.Events.Add(9.2f, () => AnimateWave(2));
        world.Events.Add(15.8f, () => ExplodeWave(0));
        world.Events.Add(16.3f, () => kefka?.Cast(A.BusterCastId));
        world.Events.Add(17.8f, () => ExplodeWave(1));
        world.Events.Add(19.9f, () => ExplodeWave(2));
        world.Events.Add(BusterAt, ResolveBuster);
        world.Events.Add(22.5f, () =>
        {
            foreach (var prop in triangles) prop?.Despawn();
        });
    }

    private void SpawnWave(int wave)
    {
        for (var i = 0; i < triangles.Length; i++)
        {
            if (state.WaveOf(i) != wave) continue;
            var triangle = UmadP2TrineState.Triangles[i];
            triangles[i] = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = triangle.ObjectId, TargetableStatus = 5,
                Placement = new Placement(new Vector3(triangle.Center.X, 0f, triangle.Center.Y), 0f),
            });
        }
    }

    private void ShowWave(int wave)
    {
        for (var i = 0; i < triangles.Length; i++)
            if (state.WaveOf(i) == wave) triangles[i]?.PlayBeat(1, 2);
    }

    private void AnimateWave(int wave)
    {
        for (var i = 0; i < triangles.Length; i++)
            if (state.WaveOf(i) == wave) triangles[i]?.PlayBeat(0x10, 0x20);
    }

    private void ExplodeWave(int wave)
    {
        foreach (var vertex in state.WaveVertices(wave))
            kefka?.CastFromHelper(A.Blast, at: new Placement(new Vector3(vertex.X, 0f, vertex.Y), 0f));
        for (var i = 0; i < triangles.Length; i++)
            if (state.WaveOf(i) == wave) triangles[i]?.FadeOut();
    }

    private void ResolveBuster()
    {
        if (kefka is null) return;
        var members = world.Party.ActiveMembers().OrderBy(m => Vector3.DistanceSquared(m.Position, kefka.Position)).ToArray();
        if (members.Length == 0) return;
        var targets = new[] { members[0], members[^1] }.Distinct().ToArray();
        foreach (var target in targets)
            kefka.CastFromHelper(A.Buster, target, at: target.Placement());
    }
}

