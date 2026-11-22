using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P6WaveCannon2;

public sealed class TopP6WaveCannon2Scenario : IMultiplayerReplayable
{
    public string Name => "Exasquares/WC2";
    public IPhase Phase => TopZone.P6;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly TopP6WaveCannon2SettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP6WaveCannon2Ai()];


    private TopP6WaveCannon2State state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? alphaOmega;

    // Exposed so MultiplayerManager can read the AI-relevant subset (InFirst) after a host
    // Start and broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP6WaveCannon2State? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP6WaveCannon2State(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP6WaveCannon2State>)AiStrats[idx]).Run(state, world);
        world.Events.Add(1f, () => state.ProteanOrder.ForEach(c => c.AddStatus(StatusId.BrilliantDynamis)));
        Run_Alpha_Omega_4000A771(solo);
        Run_Alpha_Omega_4000A40B();
        if (!solo) Run_Alpha_Omega_4000A40C();
        
    }

    public void Tick(float delta, float elapsed) { }

    private void Run_Alpha_Omega_4000A771(bool solo)
    {
        world.Events.Add(0f, () => alphaOmega = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.AlphaOmega, NameId: BNpcNameId.AlphaOmega, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.AlphaOmega, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), -MathF.PI))));
        world.Events.Add(0.5f, () => alphaOmega?.AddStatus(StatusId.CodeMi));
        world.Events.Add(1.90f, () => alphaOmega?.Cast(Actions.CosmoArrow, new Vector3(-0.008f, -0.015f, -0.008f)));
        if (solo) return;
        world.Events.Add(16.03f, () => alphaOmega?.Cast(Actions.AlphaOmegaWaveCannon, new Vector3(-0.008f, -0.015f, -0.008f)));
        world.Events.Add(27.40f, () =>
        {
            if (party.Get(state.WildChargeTarget) is { } target)
                alphaOmega?.Cast(Actions.WaveCannonWildCharge, target);
        });
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
            var offset = initial[i/4];
            var e = early[i/4];
            var pos = i % 2 == 0 ? new Placement(new(-20f, 0, offset), MathF.PI/2) : new Placement(new(offset, 0, -20), 0);
            var inhale = i % 4 > 1;

            world.Events.Add(e ? 1.90f : 3.91f, () => alphaOmega?.CastFromHelper(inhale ? Actions.Inhale : Actions.CosmoArrowOmen, at: pos));
            
            var list = Init(early, initial, []);
            for (int k = 0; k < 7; k++)
            {
                var newI = 11-i;
                if (newI < list.Count * 2)
                {
                    offset = list[newI/2].Line;
                    var pos2 = newI % 2 == 0 ? new Placement(new(-20f, 0, offset), MathF.PI/2) : new Placement(new(offset, 0, -20), 0);
                    var delta = k * 2f;
                    
                    world.Events.Add(11.91f + delta, () => alphaOmega?.CastFromHelper(Actions.CosmoArrowLine, at: pos2));
                }
                list = k == 0 ? Init(early, initial, list) : Progress(list);
            }
        }
    }

    private void Run_Alpha_Omega_4000A40C()
    {
        for (int index = 0; index < 4; index++)
        {
            var i = index;
            world.Events.Add(19.07f, () => CastProtean(state.ProteanOrder.Get(i)));
            world.Events.Add(21.07f, () => CastProtean(state.ProteanOrder.Get(i + 4)));
        }
    }

    private void CastProtean(SimCharacter? target)
    {
        if (target is not null) alphaOmega?.CastFromHelper(Actions.WaveCannonProtean, target);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP6WaveCannon2AiReplayStateMessage(s.InFirst) : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP6WaveCannon2AiReplayStateMessage msg) return null;
        var shadowState = TopP6WaveCannon2State.FromNetworkReplay(msg.InFirst);
        ((IScenarioAi<TopP6WaveCannon2State>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
