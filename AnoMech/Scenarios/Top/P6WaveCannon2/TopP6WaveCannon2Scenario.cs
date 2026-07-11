using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P6WaveCannon2;

public sealed class TopP6WaveCannon2Scenario : IScenario
{
    public string Name => "Exasquares/WC2";
    public IPhase Phase => TopZone.P6;
    public bool SupportsSolo => true;

    public void DrawSettings() => settingsWindow.Draw();
    private readonly TopP6WaveCannon2SettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP6WaveCannon2Ai()];

    private TopUtils topUtils = null!;

    private TopP6WaveCannon2State state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP6WaveCannon2State(party, settingsWindow.Overrides);
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP6WaveCannon2State>)AiStrats[idx]).Run(state, world);
        topUtils = new TopUtils(world);
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);

        world.Events.Add(1f, () => state.ProteanOrder.ForEach(c => c.AddStatus(StatusId.BrilliantDynamis)));
        Run_Alpha_Omega_4000A771(solo);
        Run_Alpha_Omega_4000A40B();
        if (!solo) Run_Alpha_Omega_4000A40C();
        
    }

    public void Tick(float delta, float elapsed) { }

    private void Run_Alpha_Omega_4000A771(bool solo)
    {
        SimEnemy? alpha_Omega_4000A771 = null;
        world.Events.Add(0f, () => alpha_Omega_4000A771 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.AlphaOmega, NameId: BNpcNameId.AlphaOmega, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), -MathF.PI))));
        world.Events.Add(0.5f, () => alpha_Omega_4000A771?.AddStatus(StatusId.CodeMi));
        world.Events.Add(1.90f, () => alpha_Omega_4000A771?.Cast(ActionId.CosmoArrow, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), targetId: alpha_Omega_4000A771?.GameObjectId));
        if (solo) return;
        world.Events.Add(16.03f, () => alpha_Omega_4000A771?.Cast(ActionId.WaveCannon_7BA9, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), targetId: alpha_Omega_4000A771?.GameObjectId));
        world.Events.Add(27.30f, () => alpha_Omega_4000A771?.Face(party.Get(state.WildChargeTarget)!));
        world.Events.Add(27.40f, () => alpha_Omega_4000A771?.Cast(ActionId.WaveCannonWildCharge, castSeconds: 0f, targetId:party.Get(state.WildChargeTarget)?.GameObjectId));
        world.Events.Add(27.40f, () => damage.Resolve(alpha_Omega_4000A771, ActionId.WaveCannonWildCharge, [DamageType.Magic], [], stackMinTargets: 8, wildChargeTargets: 2, wildChargeDamageType: [DamageType.TankBuster]));
    }
    
    
    record Wave(float Line, float Dir);
    
    private static List<Wave> Init(bool[] early, float[] initial, List<Wave> current)
    {
        var isEarly = current.Count == 0;
        var l = Progress(current);
        for (int i = 0; i < initial.Length; i++)
        {
            if (early[i] != isEarly) continue;
            l.Add(new (initial[i] + 7.5f, 1));
            l.Add(new (initial[i] - 7.5f, -1));
        }
        l.RemoveAll(v => v.Line is > 20f or < -20f);
        return l;
    }
    
    private static List<Wave> Progress(List<Wave> current)
    {
       return current
           .Select(wave => wave with {Line = wave.Line + 5 * wave.Dir})
           .Where(wave => wave.Line is <= 20f and >= -20f)
           .ToList();
    }
    
    private void Run_Alpha_Omega_4000A40B()
    {
        bool[] early =  [false, !state.InFirst, true];
        float[] initial = [state.InFirst ? -15 : 0, 15, state.InFirst ? 0 : -15];
        
        for (int i = 0; i < 12; i++)
        {
            SimEnemy? alpha_Omega_4000A40B = null;
            world.Events.Add(0f, () => alpha_Omega_4000A40B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.AlphaOmega, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.200f, -0.000f, 19.320f), 3.140f))));
            
            var offset = initial[i/4];
            var e = early[i/4];
            var pos = i % 2 == 0 ? new Placement(new(-20f, 0, offset), MathF.PI/2) : new Placement(new(offset, 0, -20), 0);
            var actionId = i % 4 > 1 ? ActionId.Inhale : ActionId.CosmoArrowOmen;
            
            if (e)
            {
                world.Events.Add(1.82f, () => alpha_Omega_4000A40B?.SetPosition(pos));
                world.Events.Add(1.90f, () => alpha_Omega_4000A40B?.Cast(actionId, targetId: alpha_Omega_4000A40B?.GameObjectId));
                if (actionId == ActionId.CosmoArrowOmen)
                    world.Events.Add(9.90f, () => damage.Resolve(alpha_Omega_4000A40B, ActionId.CosmoArrowOmen, [DamageType.Lethal], []));
            }
            else 
            {
                world.Events.Add(3.82f, () => alpha_Omega_4000A40B?.SetPosition(pos));
                world.Events.Add(3.91f, () => alpha_Omega_4000A40B?.Cast(actionId, targetId: alpha_Omega_4000A40B?.GameObjectId));
                if (actionId == ActionId.CosmoArrowOmen)
                    world.Events.Add(11.91f, () => damage.Resolve(alpha_Omega_4000A40B, ActionId.CosmoArrowOmen, [DamageType.Lethal], []));
            }
            
            var list = Init(early, initial, []);
            for (int k = 0; k < 7; k++)
            {
                var newI = 11-i;
                if (newI < list.Count * 2)
                {
                    offset = list[newI/2].Line;
                    var pos2 = newI % 2 == 0 ? new Placement(new(-20f, 0, offset), MathF.PI/2) : new Placement(new(offset, 0, -20), 0);
                    var delta = k * 2f;
                    
                    world.Events.Add(11.82f + delta, () => alpha_Omega_4000A40B?.SetPosition(pos2));
                    world.Events.Add(11.91f + delta, () => alpha_Omega_4000A40B?.Cast(ActionId.CosmoArrowDamage, castSeconds: 0f));
                    world.Events.Add(11.91f + delta, () => damage.Resolve(alpha_Omega_4000A40B,ActionId.CosmoArrowDamage, [DamageType.Lethal], []));
                }
                list = k == 0 ? Init(early, initial, list) : Progress(list);
            }
        }
    }

    private void Run_Alpha_Omega_4000A40C()
    {
        for (int index = 0; index < 4; index++)
        {
            SimEnemy? alpha_Omega_4000A40C = null;
            var i = index;
            world.Events.Add(0f, () => alpha_Omega_4000A40C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.OmegaHelper, NameId: BNpcNameId.AlphaOmega, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0f, -0.000f, 0.000f), 1.570f))));
            world.Events.Add(19.00f, () => alpha_Omega_4000A40C?.Face(state.ProteanOrder.Get(i)));
            world.Events.Add(19.07f, () => alpha_Omega_4000A40C?.Cast(ActionId.WaveCannonProtean, castSeconds: 0f, targetId: state.ProteanOrder.Get(i)?.GameObjectId));
            world.Events.Add(19.07f, () => damage.Resolve(alpha_Omega_4000A40C, ActionId.WaveCannonProtean, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 2.5f)]));
            world.Events.Add(21.00f, () => alpha_Omega_4000A40C?.Face(state.ProteanOrder.Get(i + 4)));
            world.Events.Add(21.07f, () => alpha_Omega_4000A40C?.Cast(ActionId.WaveCannonProtean, castSeconds: 0f, targetId: state.ProteanOrder.Get(i + 4)?.GameObjectId));
            world.Events.Add(21.07f, () => damage.Resolve(alpha_Omega_4000A40C, ActionId.WaveCannonProtean, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 2.5f)]));
        }
        
    }
}
