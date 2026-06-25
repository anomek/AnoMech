using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P4KefkaSays;

// Auto-generated body from tools/parser.py --code, rehomed into the canonical layout.
// Player id -> role (first-seen order in the window):
//   10066D86 MT, 100AC8F1 OT, 100AE96C H1, 100702A3 H2,
//   10018AEA M1, 100AF82E M2, 100A7A8F R1, 1009061B C.
public sealed class UmadP4KefkaSaysScenario : IScenario
{
    public string Name => "UMAD P4 Kefka Says";
    public TargetInstance TargetInstance { get; } = new(
        TerritoryId: 1363,
        Origin: new Vector3(100.000f, 0f, 100.000f),
        PlayerPosition: new Vector3(100.000f, 0f, 116.000f),
        WeatherId: 174);
    public IReadOnlyList<Waymark> Waymarks { get; } = UmadWaymarks;
    public ushort Bgm => 20293;
    
    public IReadOnlyList<Vector3> ColliderRemovalPoints => [new(0, 0, -10)];

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UmadP4KefkaSaysSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP4KefkaSaysAi(),
    ];

    private UmadP4KefkaSaysState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private SimEnemy[] detonationHelpers = [];  // invisible KefkaHelper that casts DeathSurge on Allagan Field detonation
    private int detonatioHelperIndex;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        UmadRsvStrings.Seed();
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP4KefkaSaysState(party, settingsWindow.Overrides);
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP4KefkaSaysState>)AiStrats[idx]).Run(state, world);
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        damage.SetStatuses(DamageType.Black, StatusId.BlackWound);
        damage.SetStatuses(DamageType.White, StatusId.WhiteWound);

        Run_Kefka_40004142();
        Run_Neo_Exdeath_400041A4();
        Run_Chaos_400041A5();
        Run_Kefka_400040E5_1();
        Run_Kefka_400040E6_1();
        Run_Chaos_400040E3_1();
        Run_Neo_Exdeath_400040E7_1();
        Run_Neo_Exdeath_400040E8_2();
        Run_Neo_Exdeath_400040E9_2();
        Run_Chaos_400040E2_2();
        Run_Neo_Exdeath_400040E9_5();
        Run_InstanceEvents();
        Run_OtherDebuffs();
        Run_AccelerationBomb();
    }

    private void Run_InstanceEvents()
    {
        // [1.36s] 33|800375D2|80000027|1F|02|1BDB|40004142|5f5214a2bc97cd97
        world.Events.Add(1.36f, () => world.Map.DirectorUpdate(0x80000027U, 0x1FU, 0x2U, 0x1BDBU, 0x40004142U));
        // [9.50s] 33|800375D2|80000027|20|02|1BDB|40004142|f8e7da06e555fe6f
        world.Events.Add(9.50f, () => world.Map.DirectorUpdate(0x80000027U, 0x20U, 0x2U, 0x1BDBU, 0x40004142U));
        // [98.80s] 33|800375D2|80000004|4AF|00|00|00|34a483b9efd501fd
        world.Events.Add(98.80f, () => world.Map.DirectorUpdate(0x80000004U, 0x4AFU));
        // [103.36s] 33|800375D2|80000027|04|02|1BDB|40004142|33182e2038e021ee
        world.Events.Add(103.36f, () => world.Map.DirectorUpdate(0x80000027U, 0x4U, 0x2U, 0x1BDBU, 0x40004142U));
        // [119.94s] 33|800375D2|80000027|21|02|1BDB|40004142|2b29864def0a7636
        world.Events.Add(119.94f, () => world.Map.DirectorUpdate(0x80000027U, 0x21U, 0x2U, 0x1BDBU, 0x40004142U));
    }

    private void Run_OtherDebuffs()
    { 
        detonatioHelperIndex = 0;
        detonationHelpers = Enumerable
            .Range(0, 4)
            .Select(_ => world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0f, 0f, 0f), 0f)))!)
            .ToArray();

        ushort[] debuffs = [StatusId.AccelerationBomb, StatusId.AccelerationBomb, StatusId.ForkedLightning, StatusId.CompressedWater];
        
        float d = state.Wave1First ? 51f : 76f;
        float[] durationw1 = [51f, 76f, d, d];
        world.Events.Add(20.36f, () => state.Wave1.ForEach((i, c) => c.AddStatus(debuffs[i%4], durationw1[i%4])));
        world.Events.Add(20.36f, () => state.Wave1.Get(0)?.AddStatus(StatusId.CursedShriek, 60f));
        world.Events.Add(20.36f, () => state.Wave1.Get(4)?.AddStatus(StatusId.CursedShriek, 60f));
        
        world.Events.Add(27.28f, () => party.ForEachActive(c => c.AddStatus(state.ChaosMysteries[0].Cast.Status, state.ChaosMysteries[0].Cast.DurationFirst)));
        
        d = state.Wave1First ? 61f : 36f;
        float[] durationw2 = [61f, 36f, d, d];
        world.Events.Add(35.28f, () => state.Wave2.ForEach((i, c) => c.AddStatus(debuffs[i%4], durationw2[i%4])));
        world.Events.Add(35.28f, () => state.Wave2.Get(0)?.AddStatus(StatusId.CursedShriek, 69f));
        world.Events.Add(35.28f, () => state.Wave2.Get(4)?.AddStatus(StatusId.CursedShriek, 69f));
        
        world.Events.Add(40.99f, () => party.ForEachActive(c => c.AddStatus(state.ChaosMysteries[1].Cast.Status, state.ChaosMysteries[1].Cast.DurationSecond)));
        
        ushort[] debuffsw3 = [StatusId.BeyondDeath, StatusId.AllaganField];
        world.Events.Add(51.67f, () => state.Wave3.ForEach((i, c) => c.AddStatus(debuffsw3[i%2], 15f)));
        world.Events.Add(51.67f, () => state.Wave3.ForEach((i, c) => c.AddStatus(state.Wounds[i]? StatusId.WhiteWound : StatusId.BlackWound)));
        world.Events.Add(66.34f, () => party.ForEachActive(c =>
        {
            if (c.HasStatus(state.BeyondDeathStatus))  c.Die("Beyond Death not cleansed");
            if (c.HasStatus(state.AllaganFieldStatus))
            {
                var helper = detonationHelpers[detonatioHelperIndex++ % 4];
                helper.SetPosition(c.Position);
                world.Events.Add(0.1f, () => helper.Cast(ActionId.DeathSurge)); 
            }
        }));
        world.Events.Add(66.64f, () => party.ForEachActive(c => c.RemoveStatus(StatusId.WhiteWound)));
        world.Events.Add(66.64f, () => party.ForEachActive(c => c.RemoveStatus(StatusId.BlackWound)));
    }

    private void Run_AccelerationBomb()
    {
        // Acceleration Bomb is a "don't move" debuff, but Kefka Says: that wave's lie can flip it
        // to "must move". At detonation the local player dies if they did the wrong thing. The
        // player's bomb is from whichever wave tagged their role at an AccelerationBomb slot
        // (debuffs[0]/[1]); Wave2 (35.28) re-stamps over Wave1 (20.36), so Wave2 wins. Each bomb's
        // lie is its source wave's truth (Wave1 -> Wave1True, Wave2 -> Wave2True). Times mirror
        // Run_OtherDebuffs (apply + duration).
        int w1 = Array.IndexOf(state.Wave1.List, party.PlayerRole) % 4;
        int w2 = Array.IndexOf(state.Wave2.List, party.PlayerRole) % 4;

        if (w2 == 0)      world.Events.Add(96.28f, () => ResolveAccelerationBomb(state.Wave2True)); // 35.28+61
        else if (w2 == 1) world.Events.Add(71.28f, () => ResolveAccelerationBomb(state.Wave2True)); // 35.28+36
        else if (w1 == 0) world.Events.Add(71.36f, () => ResolveAccelerationBomb(state.Wave1True)); // 20.36+51
        else if (w1 == 1) world.Events.Add(96.36f, () => ResolveAccelerationBomb(state.Wave1True)); // 20.36+76
    }

    private void ResolveAccelerationBomb(bool real)
    {
        var player = party.Player;
        if (player == null || !player.IsAlive()) return;
        // real (honest) -> must be still, die if acting; fake (lie) -> must move, die if still.
        if (real ? player.IsActing : !player.IsActing)
            player.Die(real ? "Moved during Acceleration Bomb"
                            : "Stood still during fake Acceleration Bomb");
    }

    public void Tick(float delta, float elapsed)
    {
        var status = state.AllaganFieldStatus;
        state.Wave3.ForEach(c =>
        {
            if (c.HasStatus(status) && !c.IsAlive())
            {
                c.RemoveStatus(status);
                var helper = detonationHelpers[detonatioHelperIndex++ % 4];
                helper.SetPosition(c.Position);
                world.Events.Add(0.1f, () => helper.Cast(ActionId.DeathSurge)); 
                party.WipeAllPlayers("Allagan Field detonation");  // kill everyone
            }
        });
    }

    private void Run_Kefka_40004142()
    {
        SimEnemy? kefka_40004142 = null;
        world.Events.Add(0f, () => kefka_40004142 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Kefka, NameId: BNpcNameId.Kefka, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 3.140f))));
        world.Events.Add(1.36f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(1.45f, () => kefka_40004142?.Cast(ActionId.KefkaSays));
        world.Events.Add(9.41f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(9.59f, () => kefka_40004142?.Cast(ActionId.KefkaPoof, castSeconds: 0f));
        world.Events.Add(10.93f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[0].Blizzard.Lockon, persistent: false));
        world.Events.Add(10.93f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[0].Lightning.Lockon, persistent: false));
        world.Events.Add(11.02f, () => kefka_40004142?.Cast(ActionId.MysteryMagic));
        world.Events.Add(17.45f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(20.49f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(23.53f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(25.85f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[1].Blizzard.Lockon, persistent: false));
        world.Events.Add(25.85f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[1].Lightning.Lockon, persistent: false));
        world.Events.Add(25.94f, () => kefka_40004142?.Cast(ActionId.MysteryMagic));
        world.Events.Add(31.57f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(34.61f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(37.64f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(40.68f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(40.99f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[2].Blizzard.Lockon, persistent: false));
        world.Events.Add(40.99f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[2].Lightning.Lockon, persistent: false));
        world.Events.Add(41.08f, () => kefka_40004142?.Cast(ActionId.MysteryMagic));
        world.Events.Add(48.76f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        
        // world.Events.Add(49.25f, () => kefka_40004142?.Cast(ActionId.KefkaRest, castSeconds: 0));
        world.Events.Add(53.05f, () => kefka_40004142?.Cast(ActionId.KefkaRest, castSeconds: 0));
        world.Events.Add(53.05f, () => kefka_40004142?.SetModelState((byte)0x04));
        world.Events.Add(66.10f, () => kefka_40004142?.Cast(ActionId.KefkaUnrest));
        world.Events.Add(66.91f, () => kefka_40004142?.SetModelState((byte)0x00));
        
        world.Events.Add(68.20f, () => kefka_40004142?.Cast(ActionId.ManaCharge));
        world.Events.Add(72.00f, () => kefka_40004142?.AddStatus(StatusId.ManaCharge));
        world.Events.Add(73.20f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        
        world.Events.Add(74.37f, () => kefka_40004142?.AddStatus(StatusId.ThunderCharged));
        world.Events.Add(74.37f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[3].Lightning.Lockon, persistent: false));
        world.Events.Add(74.45f, () => kefka_40004142?.Cast(ActionId.ThrummingThunderIII_Cast));
        world.Events.Add(81.24f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(84.28f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(84.60f, () => kefka_40004142?.Cast(ActionId.UltimaUpsurge));
        
        world.Events.Add(92.33f, () => kefka_40004142?.AddStatus(StatusId.BlizzardCharged));
        world.Events.Add(92.33f, () => kefka_40004142?.AttachLockonVfx(state.Mystery[3].Blizzard.Lockon, persistent: false));
        world.Events.Add(92.41f, () => kefka_40004142?.Cast(ActionId.BlizzardIIIBlowout_Cast));
        world.Events.Add(98.13f, () => kefka_40004142?.RemoveStatus(StatusId.ManaCharge));
        
        world.Events.Add(100.41f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(103.45f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(103.54f, () => kefka_40004142?.AttachLockonVfx(state.ManaReleaseBlizzardLockon, persistent: false));
        world.Events.Add(103.54f, () => kefka_40004142?.AttachLockonVfx(state.ManaReleaseLightningLockon, persistent: false));
        world.Events.Add(103.63f, () => kefka_40004142?.Cast(ActionId.ManaRelease));
        world.Events.Add(113.51f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(116.51f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(119.54f, () => kefka_40004142?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(119.90f, () => kefka_40004142?.SetTargetable(false));
        world.Events.Add(120.03f, () => kefka_40004142?.Cast(ActionId.LightOfJudgment_Enrage));
    }

    private void Run_Neo_Exdeath_400041A4()
    {
        SimEnemy? neo_Exdeath_400041A4 = null;
        world.Events.Add(0f, () => neo_Exdeath_400041A4 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.NeoExdeath, NameId: BNpcNameId.NeoExdeath, Level: 100, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(20.000f, 0.000f, 0.000f), -1.570f))));
        world.Events.Add(6.32f, () => neo_Exdeath_400041A4?.SetPosition(new Placement(new Vector3(14.142f, 0.000f, -14.142f), -0.785f)));
        world.Events.Add(6.46f, () => neo_Exdeath_400041A4?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(6.46f, () => neo_Exdeath_400041A4?.SetVisible(true));
        
        world.Events.Add(11.28f, () => neo_Exdeath_400041A4?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave1TrueVal, overrideStacks: true));
        world.Events.Add(11.37f, () => neo_Exdeath_400041A4?.Cast(ActionId.GrandCross));
        world.Events.Add(21.37f, () => neo_Exdeath_400041A4?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(26.21f, () => neo_Exdeath_400041A4?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave2TrueVal, overrideStacks: true));
        world.Events.Add(26.30f, () => neo_Exdeath_400041A4?.Cast(ActionId.GrandCross));
        world.Events.Add(36.21f, () => neo_Exdeath_400041A4?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(41.17f, () => neo_Exdeath_400041A4?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave3TrueVal, overrideStacks: true));
        world.Events.Add(41.26f, () => neo_Exdeath_400041A4?.Cast(ActionId.GrandCross));
        world.Events.Add(51.26f, () => neo_Exdeath_400041A4?.RemoveStatus(StatusId.KefkaLiesVfx));
        
        world.Events.Add(53.28f, () => neo_Exdeath_400041A4?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(55.25f, () => neo_Exdeath_400041A4?.SetPosition(state.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
        world.Events.Add(55.58f, () => neo_Exdeath_400041A4?.PlayActionTimeline(TimelineId.Spawn));
        
        world.Events.Add(57.30f, () => neo_Exdeath_400041A4?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave4TrueVal, overrideStacks: true));
        world.Events.Add(57.39f, () => neo_Exdeath_400041A4?.Cast(state.Antilights[0].ResolveFloodAction));
        world.Events.Add(63.39f, () => neo_Exdeath_400041A4?.RemoveStatus(StatusId.KefkaLiesVfx));
        
        world.Events.Add(65.52f, () => neo_Exdeath_400041A4?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(65.52f, () => neo_Exdeath_400041A4?.SetVisible(false));
    }

    private void Run_Chaos_400041A5()
    {
        SimEnemy? chaos_400041A5 = null;
        world.Events.Add(0f, () => chaos_400041A5 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Chaos, NameId: BNpcNameId.Chaos, Level: 100, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(-18.000f, 0.000f, 0.000f), 1.570f))));
        world.Events.Add(6.37f, () => chaos_400041A5?.SetPosition(new Placement(new Vector3(-12.728f, 0.000f, -12.728f), 0.785f)));
        world.Events.Add(6.46f, () => chaos_400041A5?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(6.46f, () => chaos_400041A5?.SetVisible(true));
        
        world.Events.Add(16.42f, () => chaos_400041A5?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.ChaosMysteries[0].StatusValue, overrideStacks: true));
        world.Events.Add(16.51f, () => chaos_400041A5?.Cast(state.ChaosMysteries[0].Cast.Action));
        world.Events.Add(26.51f, () => chaos_400041A5?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(31.35f, () => chaos_400041A5?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.ChaosMysteries[1].StatusValue, overrideStacks: true));
        world.Events.Add(31.43f, () => chaos_400041A5?.Cast(state.ChaosMysteries[1].Cast.Action));
        world.Events.Add(41.43f, () => chaos_400041A5?.RemoveStatus(StatusId.KefkaLiesVfx));
        
        world.Events.Add(43.49f, () => chaos_400041A5?.PlayActionTimeline(TimelineId.WarpOut));
    }

    private void Run_Kefka_400040E5_1()
    {
        
        Placement[] placements = [
            new(new(24.75f,0f, -3.54f), -MathF.PI/4), 
            new(new(17.68f, 0f, -10.61f), -MathF.PI/4),
            new(new(10.61f, 0f, -17.68f), -MathF.PI/4),
            new(new(3.54f, 0, -24.75f), -MathF.PI/4)
        ];
        
        
        for (int i = 0; i < 4; i++)
        {
            SimEnemy? kefka_400040E6_1 = null;
            var rotation = MathF.PI / 2 * i + MathF.PI / 4;
            float[] lightTiming = [11.02f, 25.94f, 41.08f, 74.45f, 110.63f];
            world.Events.Add(8.78f, () => kefka_400040E6_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), rotation))));
           
            for (int k = 0; k < 5; k++)
            {
                var mystery = state.Mystery[k];
                var real = (i + mystery.LightningOffset) % 2 == 0;
                var actionId = real ? mystery.Lightning.DamageAction : mystery.Lightning.OmenAction;
                
                var location = placements[i].MulX(mystery.LightningOrientation).MulRot(mystery.LightningOrientation);
                world.Events.Add(lightTiming[k] - 1f, () => kefka_400040E6_1?.SetPosition(location));
                if (actionId != 0)
                    world.Events.Add(lightTiming[k], () => kefka_400040E6_1?.Cast(actionId));
                if (real)
                    world.Events.Add(lightTiming[k] + 5, () => damage.Resolve(kefka_400040E6_1, actionId, [DamageType.Lethal], []));
            }
        }
    }

    private void Run_Kefka_400040E6_1()
    {
        for (int i = 0; i < 4; i++)
        {
            SimEnemy? kefka_400040E6_1 = null;
            var rotation = MathF.PI / 2 * i + MathF.PI / 4;
            float[] blizzTiming = [11.02f, 25.94f, 41.08f, 92.41f, 110.63f];
            world.Events.Add(10.78f, () => kefka_400040E6_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), rotation))));
           
            for (int k = 0; k < 5; k++)
            {
                var mystery = state.Mystery[k];
                var real = (i + mystery.BlizzardOffset) % 2 == 0;
                var actionId = real ? mystery.Blizzard.DamageAction : mystery.Blizzard.OmenAction;
                if (actionId != 0)
                    world.Events.Add(blizzTiming[k], () => kefka_400040E6_1?.Cast(actionId));
                if (real)
                    world.Events.Add(blizzTiming[k] + 5, () => damage.Resolve(kefka_400040E6_1, actionId, [DamageType.Lethal], [], size: MathF.PI / 4));
            }
        }
    }

    private void Run_Chaos_400040E3_1()
    {
        SimEnemy? chaos_400040E3_1 = null;
        world.Events.Add(16.17f, () => chaos_400040E3_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(16.42f, () => chaos_400040E3_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        world.Events.Add(16.51f, () => chaos_400040E3_1?.Cast(state.ChaosMysteries[0].Cast.Visual));
        world.Events.Add(31.43f, () => chaos_400040E3_1?.Cast(state.ChaosMysteries[1].Cast.Visual));
    }


    private void Run_Neo_Exdeath_400040E7_1()
    {
        SimEnemy? neo_Exdeath_400040E7_1 = null;
        world.Events.Add(56.98f, () => neo_Exdeath_400040E7_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.NeoExdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(new Vector3(14.140f, 0.000f, 14.140f), -2.360f))));
        world.Events.Add(57.30f, () => neo_Exdeath_400040E7_1?.SetPosition(state.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
        world.Events.Add(57.39f, () => neo_Exdeath_400040E7_1?.Cast(ActionId.EdgeOfDeath));
        world.Events.Add(57.39f + 5.5f, () => damage.Resolve(neo_Exdeath_400040E7_1, ActionId.EdgeOfDeath, [DamageType.Lethal], []));
    }

    private void ResolveAntilight(SimEnemy? enemy, MysteryAntilight mystery)
    {
        world.Events.Add(57.39f, () => enemy?.Cast(mystery.Antilight.Action));
        world.Events.Add(57.39f + 5.5f, () =>
        {
            var dead = damage.Resolve(enemy, mystery.Antilight.Action, [mystery.ResolvedDamageType], [(mystery.Antilight.Status, 0f)], removeStatus: [StatusId.BlackWound, StatusId.WhiteWound], killTargets: false);
            foreach (var simCharacter in dead)
            {
                if (simCharacter.HasStatus(state.BeyondDeathStatus))
                {
                    simCharacter.RemoveStatus(state.BeyondDeathStatus);
                    simCharacter.RemoveStatus(StatusId.BlackWound);
                    simCharacter.RemoveStatus(StatusId.WhiteWound);
                }    
                else
                {
                    simCharacter.Die("Died to Antilight");
                }
            }
        });
    }
    
    private void Run_Neo_Exdeath_400040E8_2()
    {
        SimEnemy? neo_Exdeath_400040E8_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.NeoExdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(new Vector3(7.420f, 0.000f, 20.860f), -2.360f)));
        world.Events.Add(57.30f, () => neo_Exdeath_400040E8_2?.SetPosition(state.NeoExdeathDirection.Apply(new Placement(new Vector3(-9.5f, 0.000f, -20f), 0f))));
        ResolveAntilight(neo_Exdeath_400040E8_2, state.Antilights[0]);
    }

    private void Run_Neo_Exdeath_400040E9_2()
    {
        SimEnemy? neo_Exdeath_400040E9_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.NeoExdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(20.860f, 0.000f, 7.420f), -2.360f)));
        world.Events.Add(57.30f, () => neo_Exdeath_400040E9_2?.SetPosition(state.NeoExdeathDirection.Apply(new Placement(new Vector3(9.5f, 0.000f, -20f), 0f))));
        ResolveAntilight(neo_Exdeath_400040E9_2, state.Antilights[1]);
    }

    private void Run_Chaos_400040E2_2()
    {
        for (int i = 0; i < 8; i++)
        {
            SimEnemy? chaos_400040E2_2 = null;
            PartyRole role = (PartyRole)i;
            world.Events.Add(87.16f, () => chaos_400040E2_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.700f, 0.000f, 0.280f), -1.960f))));
            world.Events.Add(87.28f, () => chaos_400040E2_2?.SetPosition(party.Get(role)!.Placement()));
            world.Events.Add(87.36f, () => chaos_400040E2_2?.Cast(state.InfernoMystery.Solution, omenDelay: 4f));
            world.Events.Add(92.36f, () => damage.Resolve(chaos_400040E2_2, state.InfernoMystery.Solution, [DamageType.Lethal], [], size: 6f));
            world.Events.Add(109.98f, () => chaos_400040E2_2?.SetPosition(party.Get(role)!.Placement()));
            world.Events.Add(110.07f, () => chaos_400040E2_2?.Cast(state.TsunamiMystery.Solution, omenDelay: 4f));
            world.Events.Add(115.07f, () => damage.Resolve(chaos_400040E2_2, state.TsunamiMystery.Solution, [DamageType.Lethal], [], size: 6f));
        }
    }

    private void Run_Neo_Exdeath_400040E9_5()
    {
        for (int i = 0; i < 4; i++)
        {
            var targetId = i * 2 + 1 + (i+1) % 2;
            var shriekTargetId = i * 4;
            
            SimEnemy? neo_Exdeath_400040E9_5 = null;
            var actionId = i % 2 == 0 ? ActionId.DeathBolt : ActionId.DeathWave;
            var minTargets1 = (i % 2 == 0) ^ state.ElemTrue[0] ? 3 : 1;
            var minTargets2 = (i % 2 == 0) ^ state.ElemTrue[1] ? 3 : 1;
            
            
            world.Events.Add(70.09f, () => neo_Exdeath_400040E9_5 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.NeoExdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-0.210f, 0.000f, 0.290f), 2.960f))));
            world.Events.Add(71.28f, () => neo_Exdeath_400040E9_5?.SetPosition(state.ElemRoles[0].Get(targetId)!.Placement()));
            world.Events.Add(71.37f, () => neo_Exdeath_400040E9_5?.Cast(actionId));
            world.Events.Add(71.37f, () => damage.Resolve(neo_Exdeath_400040E9_5, actionId, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.96f)], stackMinTargets: minTargets1));
            
            if (i < 2)
            {
                world.Events.Add(80.39f, () => neo_Exdeath_400040E9_5?.SetPosition(state.Wave1.Get(shriekTargetId)!.Position));
                world.Events.Add(80.49f, () => neo_Exdeath_400040E9_5?.Cast(ActionId.DeathShriek));
                world.Events.Add(80.49f, () => damage.ResolveGaze(state.Wave1.Get(shriekTargetId), lookAway: state.Wave1True));
            }
            
            world.Events.Add(96.39f, () => neo_Exdeath_400040E9_5?.SetPosition(state.ElemRoles[1].Get(targetId)!.Position));
            world.Events.Add(96.48f, () => neo_Exdeath_400040E9_5?.Cast(actionId));
            world.Events.Add(96.48f, () => damage.Resolve(neo_Exdeath_400040E9_5, actionId, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.96f)], stackMinTargets: minTargets2));
        
            if (i < 2)
            {
                world.Events.Add(104.29f, () => neo_Exdeath_400040E9_5?.SetPosition(state.Wave2.Get(shriekTargetId)!.Position));
                world.Events.Add(104.39f, () => neo_Exdeath_400040E9_5?.Cast(ActionId.DeathShriek)); 
                world.Events.Add(104.39f, () => damage.ResolveGaze(state.Wave2.Get(shriekTargetId), lookAway: state.Wave2True));
            }
        }
    }
}
