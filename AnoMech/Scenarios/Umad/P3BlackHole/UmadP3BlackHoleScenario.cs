// Rehomed from tools/parser.py --code output into the canonical scenario layout.
// Player id -> role (first-seen order inside the window):
//   1001875C MT, 10056A3A OT, 10019262 H1, 10018A1B H2,
//   10018BF0 M1, 10018DC8 M2, 100188C6 R1, 1004E71C C.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleScenario : IScenario
{
    public string Name => "Black Hole";
    public IPhase Phase => UmadZone.P3;

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UmadP3BlackHoleSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP3BlackHoleAi(),
    ];

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private List<Vector3>[] BlackHolePositions = null!;
    private const float BlackHoleAvoidRadius = 1.5f; // damage radius is 1.25; small clearance
    private int PrimodialCrustsToResolve;
    private float CleanseCooldown;
    SimEnemy? CleanseHelper;
    
    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP3BlackHoleState(world, settingsWindow.Overrides);
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP3BlackHoleState>)AiStrats[idx]).Run(state, world);
        damage = new DamageSolver(party);
        damage.SetStatuses(DamageType.Lightning, StatusId.LightningResistanceDownII);
        damage.SetStatuses(DamageType.Earth, StatusId.EarthResistanceDownII);
        damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        
        PrimodialCrustsToResolve = 0;

       
        BlackHolePositions = Enumerable.Range(0, 4).Select(GenerateBlackHolePositions).ToArray();
        
        Run_Chaos_4000414D();
        Run_Exdeath_4000414C();
        Run_Chaos_400040E9_1();
        Run_Kefka_40004141();
        Run_EventObj_1EC03D_40004163();
        Run_Kefka_400040E5_1();
        Run_Kefka_400040E6_1();
        Run_Kefka_400040E7_1();
        Run_Black_Hole_40004166();
        Run_Black_Hole_40004169();
        Run_BlackHoleObstacles();
        Run_Kefka_400040E9_6();
        Run_Exdeath_400040E2();
        Run_Chaos_400040E1();
        Run_Exdeath_400040D8_1();
        Run_Kefka_400040D6();
        Run_Chaos_400040E8_6();
        Run_InstanceEvents();
        Run_OtherDebuffs();
        Run_PlayerLockons();
        // [64.06s] 03|400040E9|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|7fb12caee07dda16
        world.Events.Add(0f, () => CleanseHelper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
    }

    private void Run_InstanceEvents()
    {
        // [5.88s] 33|800375D2|80000027|1B|02|1BDB|40004141|5fbef31e42584d90
        world.Events.Add(5.88f, () => world.Map.DirectorUpdate(0x80000027U, 0x1BU, 0x2U, 0x1BDBU, 0x40004141U));
        // [21.34s] 33|800375D2|80000027|1C|02|1BDB|40004141|e71e71b7e76b4bbe
        world.Events.Add(21.34f, () => world.Map.DirectorUpdate(0x80000027U, 0x1CU, 0x2U, 0x1BDBU, 0x40004141U));
        // [77.20s] 33|800375D2|80000027|1D|02|1BDB|40004141|86396579207228cc
        world.Events.Add(77.20f, () => world.Map.DirectorUpdate(0x80000027U, 0x1DU, 0x2U, 0x1BDBU, 0x40004141U));
        // [145.43s] 33|800375D2|80000027|1E|02|1BDB|40004141|1d765ff16920a4c5
        world.Events.Add(145.43f, () => world.Map.DirectorUpdate(0x80000027U, 0x1EU, 0x2U, 0x1BDBU, 0x40004141U));
        // [156.68s] 257|800375D2|00020001|22|||b09b1b24cd776e35
        world.Events.Add(156.68f, () => world.Map.AddEffect(packetFlags: 0x00020001U, index: (byte)0x22));
    }

    private void Run_OtherDebuffs()
    {
        // [8.37s] 26|644|Accretion|14.00|E0000000||10066D86|ShieldHealer|00|205177||b8d3fdddc1c1161c
        world.Events.Add(8.37f, () =>
        {
            state.Roles.Get(0)?.AddStatus(StatusId.FirstInLine);
            state.Roles.Get(0)?.AddStatus(StatusId.PrimordialCrust, 72);
            state.Roles.Get(1)?.AddStatus(StatusId.SecondInLine);
            state.Roles.Get(1)?.AddStatus(StatusId.PrimordialCrust, 106);
            state.Roles.Get(2)?.AddStatus(StatusId.ThirdInLine);
            state.Roles.Get(2)?.AddStatus(StatusId.PrimordialCrust, 139);
            state.Roles.Get(3)?.AddStatus(StatusId.FirstInLine);
            state.Roles.Get(3)?.AddStatus(StatusId.PrimordialCrust, 72);
            state.Roles.Get(3)?.AddStatus(StatusId.Accretion, 14.000f);
            state.Roles.Get(4)?.AddStatus(StatusId.FirstInLine);
            state.Roles.Get(4)?.AddStatus(StatusId.PrimordialCrust, 72);
            state.Roles.Get(5)?.AddStatus(StatusId.SecondInLine);
            state.Roles.Get(5)?.AddStatus(StatusId.PrimordialCrust, 106);
            state.Roles.Get(6)?.AddStatus(StatusId.ThirdInLine);
            state.Roles.Get(6)?.AddStatus(StatusId.PrimordialCrust, 139);
            state.Roles.Get(7)?.AddStatus(StatusId.SecondInLine);
            state.Roles.Get(7)?.AddStatus(StatusId.PrimordialCrust, 106);
            state.Roles.Get(7)?.AddStatus(StatusId.Accretion, 14.000f);
        });
        party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EpicHero);
        party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EpicHero);
        party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EpicHero);
        party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EpicHero);
        party.Get(PartyRole.OffTank)?.AddStatus(StatusId.FatedHero);
        party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.FatedHero);
        party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.FatedHero);
        party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.FatedHero);
    }

    private void Run_PlayerLockons()
    {
        world.Events.Add(147.09f, () => state.StackTargets.Get(0)?.AttachLockonVfx(LockonId.Stack, persistent: false));
        world.Events.Add(152.58f, () => state.StackTargets.Get(1)?.AttachLockonVfx(LockonId.Stack, persistent: false));
    }

    public void Tick(float delta, float elapsed)
    {
       CleanseCooldown -= elapsed;
       CleanseCooldown = float.Max(CleanseCooldown, 0f);
       if (CleanseCooldown == 0f && PrimodialCrustsToResolve > 0)
       {
           PrimodialCrustsToResolve--;
           CleanseCooldown = .5f;
           CleanseHelper?.Cast(ActionId.Earthquake_Cleanse);
           damage.Resolve(CleanseHelper, ActionId.Earthquake_Cleanse, [DamageType.Earth], [(StatusId.EarthResistanceDownII, 1.960f)]);
       } 
       
       int wave = elapsed switch
       {
            >28.17f and <41.33f => 0,
            >58.79f and <75.07f => 1,
            >92.95f and <109.12f => 2,
            > 126.24f and <139.83f => 3,
            _ => -1
       };
       if (wave != -1)
       {
           foreach (var bh in BlackHolePositions[wave])
           {
               foreach(var player in party.ActiveMembers())
               {
                  if (player.Placement().DistanceSq(bh) < 1.25f * 1.25f)
                  {
                     player.AddStatus(StatusId.DamageDown, 180f); 
                  }
               }
           }
       }
    }

    private void RunEdict(SimEnemy? enemy, float offset)
    {
        
        world.Events.Add(offset - 0.5f, () => enemy?.Follow());
        world.Events.Add(offset - 0.5f, () => enemy?.Face(state.EdictTargets.Get(0)));
        world.Events.Add(offset, () => enemy?.Cast(ActionId.DamningEdict, omenDelay: 4.1f));
        world.Events.Add(offset + 5, () => damage.Resolve(enemy, ActionId.DamningEdict, [DamageType.Lethal], []));
        world.Events.Add(offset + 6, () => enemy?.Follow(party.Get(PartyRole.MainTank)));
    }
    
    private void Run_Chaos_4000414D()
    {
        SimEnemy? chaos_4000414D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(-8.000f, 0.000f, 0.000f), 0.000f)));
        state.ScenarioObjects.Chaos = chaos_4000414D;
        world.Events.Add(0.1f, () => chaos_4000414D?.AddStatus(StatusId.EpicVillain));
        world.Events.Add(0.98f, () => chaos_4000414D?.Cast(ActionId.Earthquake));
        world.Events.Add(1f, () => chaos_4000414D?.Follow(party.Get(PartyRole.MainTank)));
        world.Events.Add(12.07f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(15.09f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(18.13f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(21.16f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(24.19f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(27.22f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(30.25f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(32.40f, () => chaos_4000414D?.Cast(ActionId.Aetherlink_Chaos));
        world.Events.Add(33.29f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(36.34f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(39.37f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(42.41f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        RunEdict(chaos_4000414D, 45.58f);
        world.Events.Add(53.48f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(56.51f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(59.54f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        RunEdict(chaos_4000414D, 72.87f);
        world.Events.Add(80.90f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(83.94f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(86.97f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(90.00f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(93.02f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(99.18f, () => chaos_4000414D?.Cast(ActionId.Aetherlink_Chaos));
        world.Events.Add(113.38f, () => chaos_4000414D?.Cast(state.ImplosionAttack));
        world.Events.Add(124.41f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(141.47f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(144.50f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(147.09f, () => chaos_4000414D?.Cast(ActionId.KnockDown_Cast));
        world.Events.Add(152.53f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(155.57f, () => chaos_4000414D?.Cast(ActionId.AutoAttack1, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        world.Events.Add(157.22f, () => chaos_4000414D?.Cast(ActionId.BigBang_Cast));
        
        for (int i = 0; i < 2; i++)
        {
            SimEnemy? implosionHelper = null;
            var rotationAdjustment = i * MathF.PI + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0 : MathF.PI / 2);
            world.Events.Add(0f, () => implosionHelper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f))));
            world.Events.Add(119.00f, () => implosionHelper?.SetPosition(chaos_4000414D!.Placement().AddToRotation(rotationAdjustment)));
            world.Events.Add(119.09f, () => implosionHelper?.Cast(ActionId.Shockwave, castSeconds: 0f));
            world.Events.Add(119.09f, () => damage.Resolve(implosionHelper, ActionId.Shockwave, [DamageType.Lethal], [], size: MathF.PI / 4));
            world.Events.Add(121.01f, () => implosionHelper?.SetPosition(implosionHelper.Placement().AddToRotation(MathF.PI / 2)));
            world.Events.Add(121.11f, () => implosionHelper?.Cast(ActionId.Shockwave, castSeconds: 0f));
            world.Events.Add(121.11f, () => damage.Resolve(implosionHelper, ActionId.Shockwave, [DamageType.Lethal], [], size: MathF.PI / 4));
        }
    }
    
    private void RunThunder(float time, SimEnemy? exdeath, SimEnemy? helper)
    {
        world.Events.Add(time - 0.2f, () => exdeath?.Follow());
        world.Events.Add(time, () =>
        {
            var target = party.Find.Closest(exdeath!.Position);
            helper?.Cast(ActionId.ThunderIII_Resolve, targetId: target?.GameObjectId);
            damage.Resolve(target, ActionId.ThunderIII_Resolve, [DamageType.TankBuster, DamageType.Lightning], [(StatusId.LightningResistanceDownII, 3.96f)]);
        });
        world.Events.Add(time + 3f, () =>
        {
            var target = party.Find.Closest(exdeath!.Position);
            helper?.Cast(ActionId.ThunderIII_Resolve, targetId: target?.GameObjectId);
            damage.Resolve(target, ActionId.ThunderIII_Resolve, [DamageType.TankBuster, DamageType.Lightning], [(StatusId.LightningResistanceDownII, 3.96f)]);
        });
        world.Events.Add(time + 3.5f, () => exdeath?.Follow(party.Get(PartyRole.OffTank)));
    }

    private void Run_Exdeath_4000414C()
    {
        SimEnemy? thunderHelper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.190f)));
        SimEnemy? exdeath_4000414C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(8.000f, 0.000f, 0.000f), 0.000f)));
        state.ScenarioObjects.Exdeath = exdeath_4000414C;
        world.Events.Add(0.1f, () => exdeath_4000414C?.AddStatus(StatusId.FatedVillain));
        world.Events.Add(1f, () => exdeath_4000414C?.Follow(party.Get(PartyRole.OffTank)));
        world.Events.Add(12.07f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(15.09f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(18.13f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(21.16f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(22.18f, () => exdeath_4000414C?.Cast(ActionId.BlackHole));
        world.Events.Add(27.22f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(30.25f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(32.40f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_Exdeath));
        world.Events.Add(33.29f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(36.34f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(37.59f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_Cast));
        world.Events.Add(44.37f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(47.41f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(50.45f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(53.48f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(56.51f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(59.54f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(77.78f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(78.85f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_Cast));
        world.Events.Add(85.85f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(88.89f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(91.91f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(99.18f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_Exdeath));
        world.Events.Add(113.38f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole));
        world.Events.Add(122.45f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(125.48f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(141.51f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        world.Events.Add(143.61f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_Cast));
        world.Events.Add(150.66f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        world.Events.Add(153.69f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        world.Events.Add(156.95f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_Raidwide));
        
        RunThunder(42.63f, exdeath_4000414C, thunderHelper);
        RunThunder(83.94f, exdeath_4000414C, thunderHelper);
    }

    private void Run_Chaos_400040E9_1()
    {
        SimEnemy? chaos_400040E9_1 = null;
        world.Events.Add(0.75f, () => chaos_400040E9_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(0.98f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_Visual));
        world.Events.Add(12.29f, () => chaos_400040E9_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_Cleanse, targetId: state.Roles.Get(3)?.GameObjectId));
        world.Events.Add(12.38f, () => damage.Resolve(chaos_400040E9_1, ActionId.Earthquake_Cleanse, [DamageType.Earth], [(StatusId.EarthResistanceDownII, 1.96f)], excludeTargets: [state.Roles.Get(3)!]));
        world.Events.Add(12.38f, () => state.Roles.Get(3)?.RemoveStatus(StatusId.Accretion));
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_Cleanse, targetId: state.Roles.Get(7)?.GameObjectId));
        world.Events.Add(16.30f, () => damage.Resolve(chaos_400040E9_1, ActionId.Earthquake_Cleanse, [DamageType.Earth], [(StatusId.EarthResistanceDownII, 1.96f)], excludeTargets: [state.Roles.Get(7)!]));
        world.Events.Add(16.30f, () => state.Roles.Get(7)?.RemoveStatus(StatusId.Accretion));
    }

    private void Run_Kefka_40004141()
    {
        var kefkaPos = new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0); 
        SimEnemy? kefka_40004141 = null;
        world.Events.Add(0f, () => kefka_40004141 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaP3, NameId: BNpcNameId.Kefka, Level: 100, Targetable: false, EnemyList: EnemyListMode.Always, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(5.88f, () => kefka_40004141?.AddStatus(StatusId.Max, stacks: (ushort)506, overrideStacks: true));
        world.Events.Add(5.88f, () => kefka_40004141?.SetModelState((byte)0x05));
        world.Events.Add(5.88f, () => kefka_40004141?.SetPosition(kefkaPos));
        world.Events.Add(11.83f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(12.39f, () => kefka_40004141?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(13.82f, () => kefka_40004141?.SetVisible(false));
        world.Events.Add(14.16f, () => kefka_40004141?.SetPosition(state.KefkaPosition[0].Apply(kefkaPos)));
        world.Events.Add(14.39f, () => kefka_40004141?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(14.91f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(16.39f, () => kefka_40004141?.Cast(state.SlapAttacks[0]));
        
        world.Events.Add(42.83f, () => kefka_40004141?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(44.15f, () => kefka_40004141?.SetVisible(false));
        world.Events.Add(44.60f, () => kefka_40004141?.SetPosition(state.KefkaPosition[1].Apply(kefkaPos)));
        world.Events.Add(44.83f, () => kefka_40004141?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(46.53f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(46.83f, () => kefka_40004141?.Cast(state.SlapAttacks[1]));
        
        world.Events.Add(70.25f, () => kefka_40004141?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(71.80f, () => kefka_40004141?.SetVisible(false));
        world.Events.Add(72.02f, () => kefka_40004141?.SetPosition(state.KefkaPosition[2].Apply(kefkaPos)));
        world.Events.Add(72.25f, () => kefka_40004141?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(73.93f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(74.25f, () => kefka_40004141?.Cast(ActionId.LookUponMeAndDespair));
        world.Events.Add(79.38f, () => kefka_40004141?.SetModelState((byte)0x07));
        world.Events.Add(81.39f, () => kefka_40004141?.Cast(ActionId.StandUp_ToWall));
        world.Events.Add(82.20f, () => kefka_40004141?.SetModelState((byte)0x05));
        
        world.Events.Add(106.81f, () => kefka_40004141?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(108.17f, () => kefka_40004141?.SetVisible(false));
        world.Events.Add(108.56f, () => kefka_40004141?.SetPosition(state.KefkaPosition[3].Apply(kefkaPos)));
        world.Events.Add(108.81f, () => kefka_40004141?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(114.53f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(114.81f, () => kefka_40004141?.Cast(state.SlapAttacks[2]));
        
        world.Events.Add(128.26f, () => kefka_40004141?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(129.77f, () => kefka_40004141?.SetVisible(false));
        world.Events.Add(130.02f, () => kefka_40004141?.SetPosition(state.KefkaPosition[4].Apply(kefkaPos)));
        world.Events.Add(130.26f, () => kefka_40004141?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(132.02f, () => kefka_40004141?.SetVisible(true));
        
        world.Events.Add(132.26f, () => kefka_40004141?.Cast(ActionId.LookUponMeAndDespair2));
        world.Events.Add(137.40f, () => kefka_40004141?.SetModelState((byte)0x07));
        world.Events.Add(139.41f, () => kefka_40004141?.Cast(ActionId.StandUp_Levitate));
        world.Events.Add(140.22f, () => kefka_40004141?.SetModelState((byte)0x06));
        
        world.Events.Add(145.52f, () => kefka_40004141?.Cast(ActionId.StompAMole_Cast));
        world.Events.Add(152.13f, () => kefka_40004141?.SetModelState((byte)0x05));
    }

    private void Run_EventObj_1EC03D_40004163()
    {
        SimEventObject? eventObj_1EC03D_40004163 = null;
        world.Events.Add(6.04f, () => eventObj_1EC03D_40004163 = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.EarthCore, Placement = new Placement(new Vector3(0f, 0f, 4f), 0.000f), SpawnVisible = false }));
        world.Events.Add(6.13f, () => eventObj_1EC03D_40004163?.SetVisible(true));
        world.Events.Add(140.13f, () => eventObj_1EC03D_40004163?.Despawn());
    }


    private void Run_Kefka_400040E5_1()
    {
        SimEnemy? kefka_400040E5_1 = null;
        world.Events.Add(16.17f, () => kefka_400040E5_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(16.39f, () => kefka_400040E5_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        world.Events.Add(23.25f, () => kefka_400040E5_1?.Cast(ActionId.SlapHappy_FinalSlap));
        world.Events.Add(24.75f, () => damage.Resolve(kefka_400040E5_1, ActionId.SlapHappy_FinalSlap, [DamageType.Lethal], []));
        world.Events.Add(53.71f, () => kefka_400040E5_1?.Cast(ActionId.SlapHappy_FinalSlap));
        world.Events.Add(55.21f, () => damage.Resolve(kefka_400040E5_1, ActionId.SlapHappy_FinalSlap, [DamageType.Lethal], []));
        world.Events.Add(121.69f, () => kefka_400040E5_1?.Cast(ActionId.SlapHappy_FinalSlap));
        world.Events.Add(123.19f, () => damage.Resolve(kefka_400040E5_1, ActionId.SlapHappy_FinalSlap, [DamageType.Lethal], []));
    }
    
    private void RunSlapAttack(float time, SimEnemy? enemy, int slapIndex, int kefkaIndex, int row)
    {
        var mul = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1 : 1;
        var dir = state.KefkaPosition[kefkaIndex];
        var pos = dir.Apply(new Placement(new(10 * mul, 0, -10 + row*10), 0));
        world.Events.Add(time - 1f, () => enemy?.SetPosition(pos)); 
        world.Events.Add(time + row * 0.65f, () => enemy?.Cast(ActionId.SlapHappy_Slap));
        world.Events.Add(time + row * 0.65f, () => damage.Resolve(enemy, ActionId.SlapHappy_Slap, [DamageType.Lethal], []));
    }

    private void Run_Kefka_400040E6_1()
    {
        for (int i = 0; i < 3; i++)
        {
            SimEnemy? kefka_400040E6_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(14.140f, 0.000f, 0.000f), -0.790f)));
            RunSlapAttack(22.14f, kefka_400040E6_1, 0, 0, i);
            RunSlapAttack(52.59f, kefka_400040E6_1, 1, 1, i);
            RunSlapAttack(120.57f, kefka_400040E6_1, 2, 3, i);
        }
    }

    private void RunSlapCone(float time, SimEnemy? enemy, int index, int row)
    {
        var targets = state.ConeTargets[index];
        if (row < targets.List.Length)
        {
            var target = targets.Get(row);
            var actionId = targets.List.Length == 1 ? ActionId.ShockingImpact : ActionId.ShockwaveCone;
            var size = MathF.PI / 6;      // half cone 30 degrees, estimated based on animation
            var stackTargets = targets.List.Length == 1 ? 8 : 0;
            world.Events.Add(time - 0.2f, () => enemy?.Face(target));
            world.Events.Add(time, () => enemy?.Cast(actionId, targetId: target?.GameObjectId));
            world.Events.Add(time, () => damage.Resolve(enemy, actionId, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.96f)], stackMinTargets: stackTargets, size: size));
        }
    }
    
    private void Run_Kefka_400040E7_1()
    {
        for (int i = 0; i < 3; i++)
        {
            SimEnemy? kefka_400040E7_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0, 0, 0), 0)));
            RunSlapCone(24.81f, kefka_400040E7_1, 0, i);
            RunSlapCone(55.26f, kefka_400040E7_1, 1, i);
            RunSlapCone(123.25f, kefka_400040E7_1, 2, i);
        }
    }
    
    private List<Vector3> GenerateBlackHolePositions(int wave)
    {
        var inner = new Vector3(0f, 0f, -9f);
        var mid = new Vector3(state.MiniBlackHoleChirality * 5.17f, 0, 12.47f);
        var outer = new Vector3(0f, 0f, -17f);
        
        var passive = new List<Vector3>();
        var active = new List<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            var dir = Direction.Cardinal[i].Rotate(state.MiniBlackHoleInitialAngle + wave);
            passive.Add(dir.Apply(inner));
            passive.Add(dir.Apply(mid));
               
            if (i > 0)
            {
                var activeDir = state.BlackHoleDirections[wave].Rotate(2*i);
                active.Add(activeDir.Apply(outer));
            }
        }
        return active.Concat(passive).ToList();
    }
    
    private void Run_Black_Hole_40004169()
    {
        for (int i = 0; i < 8; i++)
        {
            var index = i + 3; // skip active bh
            SimEnemy? black_Hole_40004169 = null;
            world.Events.Add(25.17f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(BlackHolePositions[0][index], 0.390f))));
            world.Events.Add(41.33f, () => black_Hole_40004169?.Despawn());
            
            world.Events.Add(55.79f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(BlackHolePositions[1][index], 1.180f))));
            world.Events.Add(75.07f, () => black_Hole_40004169?.Despawn());
            
            world.Events.Add(89.95f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(BlackHolePositions[2][index], 0.390f))));
            world.Events.Add(109.12f, () => black_Hole_40004169?.Despawn());
            
            world.Events.Add(123.34f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(BlackHolePositions[3][index], 1.180f))));
            world.Events.Add(139.83f, () => black_Hole_40004169?.Despawn());
        }
    }
    
    private void RunActiveBlackHole(Vector3 pos, float spawnTime, float tetherTime, float firstShotTime, int numberOfShots)
    {
        SimEnemy? black_Hole_40004166 = null;
        SimTether? tether = null;
        world.Events.Add(spawnTime, () => black_Hole_40004166 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new(pos, -3.140f))));
        world.Events.Add(tetherTime, () => tether = world.Tether(black_Hole_40004166!, End.Passable(), TetherId.GrabbyTether));
        
        var despawnDelay = 2f;
        var shootCooldown = 5.1f;
        for (int i = 0; i < numberOfShots; i++)
        {
            var shotTime = firstShotTime + i * shootCooldown;
            world.Events.Add(shotTime - 0.1f, () => black_Hole_40004166?.Face(tether?.B));
            world.Events.Add(shotTime, () => black_Hole_40004166?.Cast(ActionId.Nothingness));
            world.Events.Add(shotTime , () => ResolveNothingness(damage.Resolve(black_Hole_40004166, ActionId.Nothingness, [DamageType.Lethal], [], killTargets: false)));
        }
        world.Events.Add(firstShotTime + shootCooldown * (numberOfShots - 1), () => tether?.Despawn());
        world.Events.Add(firstShotTime + shootCooldown * (numberOfShots - 1) + despawnDelay, () => black_Hole_40004166?.Despawn());
    }

    private void ResolveNothingness(IReadOnlyList<SimCharacter> targets)
    {
        foreach (var simCharacter in targets)
        {
           if (simCharacter.HasStatus(StatusId.MeanestExistence))
           {
               if (simCharacter.HasStatus(StatusId.PrimordialCrust))
               {
                   simCharacter.RemoveStatus(StatusId.PrimordialCrust);
                   simCharacter.RemoveStatus(StatusId.FirstInLine);
                   simCharacter.RemoveStatus(StatusId.SecondInLine);
                   simCharacter.RemoveStatus(StatusId.ThirdInLine);
                   PrimodialCrustsToResolve++;
               }
               else
               {
                   simCharacter.Die("Hit by Nothingness while having Meanest Existence debuff");
               }
           }
           else if (simCharacter.HasStatus(StatusId.Unbecoming))
           {
               simCharacter.RemoveStatus(StatusId.Unbecoming);
               simCharacter.AddStatus(StatusId.MeanestExistence);
           }
           else
           {
               simCharacter.AddStatus(StatusId.Unbecoming);
           }
        }
    }

    private void Run_Black_Hole_40004166()
    {
        // Point the tether ordering at each wave's Kefka direction so the AI reads
        // state.ScenarioObjects.Tethers already sorted clockwise from it.
        world.Events.Add(25.17f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[0]);
        world.Events.Add(55.70f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[1]);
        world.Events.Add(89.95f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[2]);
        world.Events.Add(123.34f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[3]);

        RunActiveBlackHole(BlackHolePositions[0][2], 25.17f, 25.17f, 32.27f, 1);
        RunActiveBlackHole(BlackHolePositions[0][1], 25.17f, 32.27f, 39.33f, 1);
        RunActiveBlackHole(BlackHolePositions[0][0], 25.17f, 32.27f, 39.33f, 1);
        
        for (var i = 0; i < 3; i++)
        {
            RunActiveBlackHole(BlackHolePositions[1][i], 55.70f, 57.98f, 62.83f, 3);
            RunActiveBlackHole(BlackHolePositions[2][i], 89.95f, 92.15f, 97.12f, 3);
        }
        
        RunActiveBlackHole(BlackHolePositions[3][0], 123.34f, 125.74f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][1], 123.34f, 125.74f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][2], 123.34f, 130.60f, 137.67f, 1);
    }

    private void Run_BlackHoleObstacles()
    {
        // Black holes appear, then 1.5s later (1.5s before their damage window opens)
        // bots steer around them; obstacles clear when the wave despawns.
        RunBlackHoleObstacleWave(0, 26.67f, 41.33f);
        RunBlackHoleObstacleWave(1, 57.29f, 75.07f);
        RunBlackHoleObstacleWave(2, 91.45f, 109.12f);
        RunBlackHoleObstacleWave(3, 124.84f, 139.83f);
    }

    private void RunBlackHoleObstacleWave(int wave, float avoidStart, float avoidEnd)
    {
        world.Events.Add(avoidStart, () =>
        {
            foreach (var bh in BlackHolePositions[wave])
                world.Obstacles.Add(new CircleObstacle(new Vector2(bh.X, bh.Z), BlackHoleAvoidRadius));
        });
        world.Events.Add(avoidEnd, () => world.Obstacles.Clear());
    }


    private void Run_Kefka_400040E9_6()
    {
        SimEnemy? kefka_400040E9_6 = null;
        world.Events.Add(72.02f, () => kefka_400040E9_6 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.790f))));
        world.Events.Add(73.25f, () => kefka_400040E9_6?.SetPosition(state.KefkaPosition[2].Apply(new Placement(new Vector3(0.000f, 0.000f, -20.000f), 0))));
        world.Events.Add(74.29f, () => kefka_400040E9_6?.Cast(ActionId.LookUponMeAndDespair_Omen, omenDelay: 4.2f));
        world.Events.Add(79.29f, () => damage.Resolve(kefka_400040E9_6, ActionId.LookUponMeAndDespair_Omen, [DamageType.Lethal], []));
        
        world.Events.Add(131.26f, () => kefka_400040E9_6?.SetPosition(state.KefkaPosition[4].Apply(new Placement(new Vector3(0.000f, 0.000f, -20.000f), 0))));
        world.Events.Add(132.35f, () => kefka_400040E9_6?.Cast(ActionId.LookUponMeAndDespair_Omen, omenDelay: 4.2f));
        world.Events.Add(137.35f, () => damage.Resolve(kefka_400040E9_6, ActionId.LookUponMeAndDespair_Omen, [DamageType.Lethal], []));
    }

    private void RunBlizzard(SimEnemy? enemy, float offset, PartyRole role)
    {
        Vector3 target = new(); 
        world.Events.Add(offset, () => 
        {
            target = party.Get(role)?.Position ?? target;
            enemy?.Cast(ActionId.BlizzardIII, targetLocation: target);
        });
        world.Events.Add(offset + 3, () => damage.Resolve(IPositioned.From(target), ActionId.BlizzardIII, [DamageType.Lethal], []));
        
    }
    
    private void Run_Exdeath_400040E2()
    {
        for (int i = 0; i < 8; i++)
        {
            SimEnemy? exdeath_400040E2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f)));
            RunBlizzard(exdeath_400040E2, 146.68f, (PartyRole)i);
        }
    }

    private void Run_Chaos_400040E1()
    {
        SimEnemy? chaos_400040E1 = null;
        world.Events.Add(147.09f, () => chaos_400040E1?.SetPosition(new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f)));
        world.Events.Add(146.83f, () => chaos_400040E1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-4.450f, 0.000f, -5.300f), -2.220f))));
        world.Events.Add(152.13f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: state.StackTargets.Get(0)?.GameObjectId));
        world.Events.Add(152.13f, () => damage.Resolve(state.StackTargets.Get(0), ActionId.KnockDown, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.960f)], stackMinTargets: 4));
        world.Events.Add(157.67f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: state.StackTargets.Get(1)?.GameObjectId));
        world.Events.Add(157.67f, () => damage.Resolve(state.StackTargets.Get(1), ActionId.KnockDown, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 1.960f)], stackMinTargets: 4));
    }


    private void Run_Exdeath_400040D8_1()
    {
        for(int i = 0; i < 8; i++)
        {
            SimEnemy? exdeath_400040D8_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f)));
            RunBlizzard(exdeath_400040D8_1, 149.73f,(PartyRole)i);
        }
    }

    private void RunStomp(float offset, int mul)
    {
        SimEnemy? kefka_400040D7 = null;
        world.Events.Add(0f, () => kefka_400040D7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        world.Events.Add(offset - 1, () => kefka_400040D7?.SetPosition(state.KefkaPosition[4].Apply(new Placement(new Vector3(mul * 10, 0, 0), 0))));
        world.Events.Add(offset, () => kefka_400040D7?.Cast(ActionId.StompAMole));
        world.Events.Add(offset + 1.5f, () =>
        {
            if (damage.Resolve(kefka_400040D7, ActionId.StompAMole, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, 3)], stackMinTargets: 2).Count == 0)
            {
               kefka_400040D7?.Cast(ActionId.UnmitigatedImpact); 
               damage.Resolve(kefka_400040D7, ActionId.UnmitigatedImpact, [DamageType.Lethal], []);
            }
        });
    }

    private void Run_Kefka_400040D6()
    {
        RunStomp(150.71f, -1);
        RunStomp(152.00f, 1);
        RunStomp(153.29f, -1);
        RunStomp(154.63f, 1);
    }

    private void Run_Chaos_400040E8_6()
    {
        SimEnemy? chaos_400040E8_6 = null;
        // [152.58s] 271|400040E8|-2.2196|00|00|95.5477|94.7008|0.2000|7371bf2a5c9e21cf
        world.Events.Add(152.58f, () => chaos_400040E8_6?.SetPosition(new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f)));
        // [152.41s] 03|400040E8|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||95.55|94.70|0.00|-2.22|36c380f0f2c22b73
        world.Events.Add(152.41f, () => chaos_400040E8_6 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-4.450f, 0.000f, -5.300f), -2.220f))));
        // [157.67s] 22|400040E8|Chaos|BB03|Knock Down|10018AEA|MeleeDpsB|750603|D9DA4001|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|203721|226668|10000|10000|||100.72|99.81|0.00|-2.89|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088F5|0|4|00||01|BB03|BB03|1.100|2591|c160363818571ec0
        world.Events.Add(157.67f, () => chaos_400040E8_6?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [157.67s] 22|400040E8|Chaos|BB03|Knock Down|100AC8F1|CasterDps|750603|CB7D4001|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|220857|227550|10000|10000|||100.23|99.86|0.00|-2.98|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088F5|1|4|00||01|BB03|BB03|1.100|2591|f84a0cf5444efb65
        world.Events.Add(157.67f, () => chaos_400040E8_6?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [157.67s] 22|400040E8|Chaos|BB03|Knock Down|100702A3|Player|750603|69A4002|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|205207|205207|5300|10000|||101.70|99.90|0.00|-2.99|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088F5|2|4|00||01|BB03|BB03|1.100|2591|d19f67482d62e5c2
        world.Events.Add(157.67f, () => chaos_400040E8_6?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [157.67s] 22|400040E8|Chaos|BB03|Knock Down|100A7A8F|OffTank|750603|25134002|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|174334|217488|10000|10000|||102.05|99.66|0.00|2.02|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088F5|3|4|00||01|BB03|BB03|1.100|2591|f7ccbbb98d551a3d
        world.Events.Add(157.67f, () => chaos_400040E8_6?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [157.67s] 264|400040E8|BB03|000088F5|1|-0.015|-0.015|-0.015|-2.220|10018AEA|9a97441abbbfd96a
        // [157.67s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Chaos|100A7A8F|OffTank|00|217488|44|54f645505c17fa23
        world.Events.Add(157.67f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [157.67s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Chaos|100702A3|Player|00|205207|44|cf3efedf4042d732
        world.Events.Add(157.67f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [157.67s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Chaos|10018AEA|MeleeDpsB|00|226668|44|50a17351eacef49b
        world.Events.Add(157.67f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [157.67s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Chaos|100AC8F1|CasterDps|00|227550|44|227a758b9a274952
        world.Events.Add(157.67f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [159.63s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Chaos|100A7A8F|OffTank|00|217488|44|c183a023713c6598
        world.Events.Add(159.63f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [159.63s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Chaos|100702A3|Player|00|205207|44|fef45bd6e3d43914
        world.Events.Add(159.63f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [159.63s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Chaos|10018AEA|MeleeDpsB|00|226668|44|c31a0ed01d93c9b4
        world.Events.Add(159.63f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [159.63s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Chaos|100AC8F1|CasterDps|00|227550|44|8ea67f20190b40b7
        world.Events.Add(159.63f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }
}
