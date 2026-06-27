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
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleScenario : IScenario
{
    public string Name => "UMAD P3 Black Hole (WIP)";
    public TargetInstance TargetInstance { get; } = new(
        TerritoryId: 1363,
        Origin: new Vector3(100.000f, 0f, 100.000f),
        PlayerPosition: new Vector3(100.000f, 0f, 116.000f),
        WeatherId: 174);
    public IReadOnlyList<Waymark> Waymarks { get; } = UmadWaymarks;
    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } = UmadConstants.WaymarkPresets;
    public ushort Bgm => 533;

    public IReadOnlyList<Vector3> ColliderRemovalPoints => [new(0, 0, -10)];

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
    private int PrimodialCrustsToResolve;
    private float CleanseCooldown;
    SimEnemy? CleanseHelper;
    
    public void Run(SimWorld worldParam, int? selectedAi)
    {
        UmadRsvStrings.Seed();
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP3BlackHoleState(party, settingsWindow.Overrides);
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
        Run_Kefka_400040E9_6();
        Run_Exdeath_400040E2();
        Run_Exdeath_400040E3();
        Run_Exdeath_400040E4_1();
        Run_Exdeath_400040E5_3();
        Run_Exdeath_400040E6_2();
        Run_Exdeath_400040E7_2();
        Run_Exdeath_400040E8_5();
        Run_Exdeath_400040E9_11();
        Run_Chaos_400040E0();
        Run_Chaos_400040E1();
        Run_Exdeath_400040D8_1();
        Run_Exdeath_400040D9_1();
        Run_Exdeath_400040DA_1();
        Run_Exdeath_400040DB_1();
        Run_Exdeath_400040DC_1();
        Run_Exdeath_400040DD_1();
        Run_Exdeath_400040DE_1();
        Run_Exdeath_400040DF_1();
        Run_Kefka_400040D7();
        Run_Kefka_400040D6();
        Run_Kefka_400040E9_12();
        Run_Chaos_400040E8_6();
        Run_Kefka_400040E7_3();
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
        world.Events.Add(time, () =>
        {
            var target = party.Find.Closest(exdeath!.Position);
            helper?.Cast(ActionId.ThunderIII_Resolve, targetId: target?.GameObjectId);
            // TODO: temporary disable until we have AI that can resolve it
            // damage.Resolve(target, ActionId.ThunderIII_Resolve, [DamageType.TankBuster, DamageType.Lightning], [(StatusId.LightningResistanceDownII, 3.96f)]);
        });
    }

    private void Run_Exdeath_4000414C()
    {
        SimEnemy? thunderHelper = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.190f)));
        SimEnemy? exdeath_4000414C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(8.000f, 0.000f, 0.000f), 0.000f)));
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
        world.Events.Add(143.61f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_PuddlesQM));
        world.Events.Add(150.66f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        world.Events.Add(153.69f, () => exdeath_4000414C?.Cast(ActionId.AutoAttack2, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        world.Events.Add(156.95f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_Raidwide));
        
        RunThunder(42.63f, exdeath_4000414C, thunderHelper);
        RunThunder(45.67f, exdeath_4000414C, thunderHelper);
        RunThunder(83.94f, exdeath_4000414C, thunderHelper);
        RunThunder(86.97f, exdeath_4000414C, thunderHelper);
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
               
            var activeDir = Direction.Cardinal[i]; 
            if (activeDir != state.BlackHoleDirections[wave])
                active.Add(activeDir.Apply(outer));
        }
        
        return active.Shuffle().Concat(passive).ToList();
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
        world.Events.Add(tetherTime, () => tether = world.TetherPassable(party.GetRandom()!, black_Hole_40004166!, TetherId.GrabbyTether));
        
        var despawnDelay = 2f;
        var shootCooldown = 5.1f;
        for (int i = 0; i < numberOfShots; i++)
        {
            var shotTime = firstShotTime + i * shootCooldown;
            world.Events.Add(shotTime - 0.1f, () => black_Hole_40004166?.Face(tether?.A));
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
        RunActiveBlackHole(BlackHolePositions[0][0], 25.17f, 25.17f, 32.27f, 1);
        RunActiveBlackHole(BlackHolePositions[0][1], 25.17f, 32.27f, 39.33f, 1);
        RunActiveBlackHole(BlackHolePositions[0][2], 25.17f, 32.27f, 39.33f, 1);
        
        for (var i = 0; i < 3; i++)
        {
            RunActiveBlackHole(BlackHolePositions[1][i], 55.70f, 57.98f, 62.83f, 3);
            RunActiveBlackHole(BlackHolePositions[2][i], 89.95f, 92.15f, 97.12f, 3);
        }
        
        RunActiveBlackHole(BlackHolePositions[3][0], 123.34f, 125.74f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][1], 123.34f, 125.74f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][2], 123.34f, 130.60f, 137.67f, 1);
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

    private void Run_Exdeath_400040E2()
    {
        SimEnemy? exdeath_400040E2 = null;
        // [-1158.08s] 261|Add|400040E2|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|564c9ef6032c55d5
        // [146.30s] 261|Change|400040E2|BNpcNameID|17A4|Name|Exdeath|b9748039fd178bfd
        // [146.30s] 03|400040E2|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||90.10|109.90|0.00|0.00|88fa07ea2f5f348d
        world.Events.Add(146.30f, () => exdeath_400040E2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f))));
        // [146.68s] 20|400040E2|Exdeath|BB0D|Blizzard III|E0000000||2.700|90.10|109.90|0.00|0.00|31f8b7418eb469f0
        world.Events.Add(146.68f, () => exdeath_400040E2?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-9.590f, -0.015f, -9.834f), castSeconds: 2.700f));
        // [149.68s] 21|400040E2|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||90.10|109.90|0.00|0.00|000088A4|0|0|00||01|BB0D|BB0D|1.100|7FFF|5a35d7ec09ff8758
        // [149.68s] 264|400040E2|BB0D|000088A4|1|90.410|90.166|-0.015|0.000|E0000000|031337ed9468313c
    }

    private void Run_Exdeath_400040E3()
    {
        SimEnemy? exdeath_400040E3 = null;
        // [-1158.08s] 261|Add|400040E3|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|af5d85afd40a0a7c
        // [146.30s] 261|Change|400040E3|BNpcNameID|17A4|Name|Exdeath|a8f91626c99eaa86
        // [146.30s] 03|400040E3|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||90.10|109.90|0.00|0.00|d7470240207a8b06
        world.Events.Add(146.30f, () => exdeath_400040E3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f))));
        // [146.68s] 20|400040E3|Exdeath|BB0D|Blizzard III|E0000000||2.700|90.10|109.90|0.00|0.00|9c2c38172d374141
        world.Events.Add(146.68f, () => exdeath_400040E3?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-10.872f, -0.015f, -9.651f), castSeconds: 2.700f));
        // [149.68s] 21|400040E3|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||90.10|109.90|0.00|0.00|000088A5|0|0|00||01|BB0D|BB0D|1.100|7FFF|95e28b4160078af6
        // [149.68s] 264|400040E3|BB0D|000088A5|1|89.128|90.349|-0.015|0.000|E0000000|15357c64075b10a5
    }

    private void Run_Exdeath_400040E4_1()
    {
        SimEnemy? exdeath_400040E4_1 = null;
        // [146.30s] 261|Change|400040E4|BNpcNameID|17A4|Heading|-1.5876|Name|Exdeath|924127bea8c77c06
        // [146.30s] 03|400040E4|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.74|100.65|0.00|-1.59|a8e96e824d820aa6
        world.Events.Add(146.30f, () => exdeath_400040E4_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.740f, 0.000f, 0.650f), -1.590f))));
        // [146.68s] 20|400040E4|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.74|100.65|0.00|-1.59|53ad86ed3bf26571
        world.Events.Add(146.68f, () => exdeath_400040E4_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(9.972f, -0.015f, 9.178f), castSeconds: 2.700f));
        // [149.68s] 21|400040E4|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.74|100.65|0.00|-1.59|000088A6|0|0|00||01|BB0D|BB0D|1.100|3F50|036d4da0c4598ed8
        // [149.68s] 264|400040E4|BB0D|000088A6|1|109.972|109.178|-0.015|-1.588|E0000000|f9c9b3875a2d153f
    }

    private void Run_Exdeath_400040E5_3()
    {
        SimEnemy? exdeath_400040E5_3 = null;
        // [146.30s] 261|Change|400040E5|BNpcNameID|17A4|Heading|1.5539|Name|Exdeath|b97bef97e65bb559
        // [146.30s] 03|400040E5|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.74|100.65|0.00|1.55|2f9d1211f5b8037b
        world.Events.Add(146.30f, () => exdeath_400040E5_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.740f, 0.000f, 0.650f), 1.550f))));
        // [146.68s] 20|400040E5|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.74|100.65|0.00|1.55|a945cdead050f1a6
        world.Events.Add(146.68f, () => exdeath_400040E5_3?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(10.246f, -0.015f, 9.728f), castSeconds: 2.700f));
        // [149.68s] 21|400040E5|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.74|100.65|0.00|1.55|000088A7|0|0|00||01|BB0D|BB0D|1.100|BF4F|5c88aab18069fc60
        // [149.68s] 264|400040E5|BB0D|000088A7|1|110.246|109.728|-0.015|1.554|E0000000|28f22961a30a69d8
    }

    private void Run_Exdeath_400040E6_2()
    {
        SimEnemy? exdeath_400040E6_2 = null;
        // [146.30s] 261|Change|400040E6|BNpcNameID|17A4|Name|Exdeath|c05dd66c49c15168
        // [146.30s] 03|400040E6|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|100.00|0.00|0.00|00dbd67708b7b675
        world.Events.Add(146.30f, () => exdeath_400040E6_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        // [146.68s] 20|400040E6|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|100.00|0.00|0.00|5f1cf4441e34eeef
        world.Events.Add(146.68f, () => exdeath_400040E6_2?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(9.178f, -0.015f, 9.911f), castSeconds: 2.700f));
        // [149.68s] 21|400040E6|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.00|000088A8|0|0|00||01|BB0D|BB0D|1.100|7FFF|f3a63d2b2cd275e4
        // [149.68s] 264|400040E6|BB0D|000088A8|1|109.178|109.911|-0.015|0.000|E0000000|d8a6031cccce35dd
    }

    private void Run_Exdeath_400040E7_2()
    {
        SimEnemy? exdeath_400040E7_2 = null;
        // [146.30s] 261|Change|400040E7|BNpcNameID|17A4|Name|Exdeath|38238309b1dfa96b
        // [146.30s] 03|400040E7|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|100.00|0.00|2.39|ef98e0115d045b0e
        world.Events.Add(146.30f, () => exdeath_400040E7_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 2.390f))));
        // [146.68s] 20|400040E7|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|100.00|0.00|2.39|ee288b4a8e961347
        world.Events.Add(146.68f, () => exdeath_400040E7_2?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-9.896f, -0.015f, -10.109f), castSeconds: 2.700f));
        // [149.68s] 21|400040E7|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|2.39|000088A9|0|0|00||01|BB0D|BB0D|1.100|E175|3e4b4e293e673262
        // [149.68s] 264|400040E7|BB0D|000088A9|1|90.104|89.891|-0.015|2.392|E0000000|95f722a0d0a927e8
        // [152.83s] 261|Change|400040E7|BNpcNameID|1BDB|CastGroundTargetX|90.1045|CastGroundTargetY|89.8909|CastGroundTargetZ|-0.0153|Heading|-0.7855|Name|Kefka|PosX|107.0711|PosY|92.9289|PosZ|0.0000|9a69c71c470f80a1
        // [152.83s] 261|Change|400040E7|BNpcNameID|1BDB|CastGroundTargetX|90.1045|CastGroundTargetY|89.8909|CastGroundTargetZ|-0.0153|Heading|-0.7855|Name|Kefka|PosX|107.0711|PosY|92.9289|PosZ|0.0000|e5a0040321112e4f
    }

    private void Run_Exdeath_400040E8_5()
    {
        SimEnemy? exdeath_400040E8_5 = null;
        // [146.30s] 261|Change|400040E8|BNpcNameID|17A4|Name|Exdeath|5c022cae8a20cb8f
        // [146.30s] 03|400040E8|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|100.00|0.00|0.79|424a2ba847ccb68b
        world.Events.Add(146.30f, () => exdeath_400040E8_5 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.790f))));
        // [146.68s] 20|400040E8|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|100.00|0.00|0.79|65fbf93fdc051049
        world.Events.Add(146.68f, () => exdeath_400040E8_5?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-9.743f, -0.015f, -9.316f), castSeconds: 2.700f));
        // [149.68s] 21|400040E8|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.79|000088AA|0|0|00||01|BB0D|BB0D|1.100|9FFF|f10ce4c1a88074a5
        // [149.68s] 264|400040E8|BB0D|000088AA|1|90.257|90.684|-0.015|0.785|E0000000|ce611e4574ec864c
        // [152.29s] 261|Change|400040E8|BNpcNameID|1E0B|CastGroundTargetX|90.2571|CastGroundTargetY|90.6843|CastGroundTargetZ|-0.0153|Heading|-2.2196|Name|Chaos|PosX|95.5477|PosY|94.7008|PosZ|0.0000|69a4cf292f9988a1
        // [152.29s] 261|Change|400040E8|BNpcNameID|1E0B|CastGroundTargetX|90.2571|CastGroundTargetY|90.6843|CastGroundTargetZ|-0.0153|Heading|-2.2196|Name|Chaos|PosX|95.5477|PosY|94.7008|PosZ|0.0000|95fd410a9523ace8
    }

    private void Run_Exdeath_400040E9_11()
    {
        SimEnemy? exdeath_400040E9_11 = null;
        // [146.30s] 261|Change|400040E9|BNpcNameID|17A4|Name|Exdeath|926e6848ca18f8a1
        // [146.30s] 03|400040E9|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|7391277c04d50b94
        world.Events.Add(146.30f, () => exdeath_400040E9_11 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
        // [146.68s] 20|400040E9|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|104.00|0.00|0.00|54f6d406da409e8b
        world.Events.Add(146.68f, () => exdeath_400040E9_11?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(10.491f, -0.015f, 9.880f), castSeconds: 2.700f));
        // [149.68s] 21|400040E9|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|104.00|0.00|0.00|000088AB|0|0|00||01|BB0D|BB0D|1.100|7FFF|d998384db5cc7be9
        // [149.68s] 264|400040E9|BB0D|000088AB|1|110.491|109.880|-0.015|0.000|E0000000|09e02623a6131206
    }

    private void Run_Chaos_400040E0()
    {
        SimEnemy? chaos_400040E0 = null;
        // [-1158.08s] 261|Add|400040E0|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|1e2a55bbc95d6f68
        // [147.09s] 271|400040E0|-2.2196|00|00|95.5477|94.7008|0.2000|127532ef39b42b1c
        world.Events.Add(147.09f, () => chaos_400040E0?.SetPosition(new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f)));
        // [146.83s] 261|Change|400040E0|BNpcNameID|1E0B|Heading|-2.2196|Name|Chaos|PosX|95.5477|PosY|94.7008|PosZ|0.0000|61ecf71148a4a31c
        // [146.83s] 03|400040E0|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||95.55|94.70|0.00|-2.22|f69e1c4aa16f11ce
        world.Events.Add(146.83f, () => chaos_400040E0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-4.450f, 0.000f, -5.300f), -2.220f))));
    }

    private void Run_Chaos_400040E1()
    {
        SimEnemy? chaos_400040E1 = null;
        // [-1158.08s] 261|Add|400040E1|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|cad52e7562bb811a
        // [147.09s] 271|400040E1|-2.2196|00|00|95.5477|94.7008|0.2000|7e6a4a502abd5856
        world.Events.Add(147.09f, () => chaos_400040E1?.SetPosition(new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f)));
        // [146.83s] 261|Change|400040E1|BNpcNameID|1E0B|Heading|-2.2196|Name|Chaos|PosX|95.5477|PosY|94.7008|PosZ|0.0000|aede0fb7f43afa2d
        // [146.83s] 03|400040E1|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||95.55|94.70|0.00|-2.22|3118d396e4ac51aa
        world.Events.Add(146.83f, () => chaos_400040E1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-4.450f, 0.000f, -5.300f), -2.220f))));
        // [152.13s] 22|400040E1|Chaos|BB03|Knock Down|10066D86|ShieldHealer|750603|FEE30000|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|205177|205177|9360|10000|||100.14|100.27|0.00|-2.86|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088C1|0|4|00||01|BB03|BB03|1.100|2591|b755834cc74794db
        world.Events.Add(152.13f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [152.13s] 22|400040E1|Chaos|BB03|Knock Down|100AF82E|MainTank|750003|5ADA4001|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|325133|325133|7800|10000|||101.93|97.79|0.00|0.48|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088C1|1|4|00||01|BB03|BB03|1.100|2591|b694f47153d639a8
        world.Events.Add(152.13f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [152.13s] 22|400040E1|Chaos|BB03|Knock Down|1009061B|MeleeDpsA|750603|AA5B4001|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|205177|205177|7060|10000|||100.95|101.93|0.00|-2.45|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088C1|2|4|00||01|BB03|BB03|1.100|2591|8c6b11b77bebf9a5
        world.Events.Add(152.13f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [152.13s] 22|400040E1|Chaos|BB03|Knock Down|100AE96C|RegenHealer|EC750005|1E214001|E80E|B7D0000|1B|BB038000|0|0|0|0|0|0|0|0|0|0|311227|325047|8900|10000|||99.14|98.82|0.00|-0.37|44|44|0|10000|||95.55|94.70|0.00|-2.22|000088C1|3|4|00||01|BB03|BB03|1.100|2591|f565a4ccafc6b084
        world.Events.Add(152.13f, () => chaos_400040E1?.Cast(ActionId.KnockDown, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [152.13s] 26|B7D|Magic Vulnerability Up|1.96|400040E1|Chaos|1009061B|MeleeDpsA|00|205177|44|958fb3e49de98b3c
        world.Events.Add(152.13f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [152.13s] 264|400040E1|BB03|000088C1|1|-0.015|-0.015|-0.015|-2.220|10066D86|146f92a3e557993b
        // [152.13s] 26|B7D|Magic Vulnerability Up|1.96|400040E1|Chaos|100AF82E|MainTank|00|325133|44|02fe0bcdc851c6b2
        world.Events.Add(152.13f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [152.13s] 26|B7D|Magic Vulnerability Up|1.96|400040E1|Chaos|100AE96C|RegenHealer|00|325047|44|164ef54c512939d8
        world.Events.Add(152.13f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [152.13s] 26|B7D|Magic Vulnerability Up|1.96|400040E1|Chaos|10066D86|ShieldHealer|00|205177|44|d8d5243e9291dba5
        world.Events.Add(152.13f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [154.10s] 30|B7D|Magic Vulnerability Up|0.00|400040E1|Chaos|1009061B|MeleeDpsA|00|205177|44|0d0ce49784fb14e8
        world.Events.Add(154.10f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [154.10s] 30|B7D|Magic Vulnerability Up|0.00|400040E1|Chaos|100AF82E|MainTank|00|325133|44|b623950315dfe2a6
        world.Events.Add(154.10f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [154.10s] 30|B7D|Magic Vulnerability Up|0.00|400040E1|Chaos|100AE96C|RegenHealer|00|325047|44|ca53eb2142e27c04
        world.Events.Add(154.10f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [154.10s] 30|B7D|Magic Vulnerability Up|0.00|400040E1|Chaos|10066D86|ShieldHealer|00|205177|44|54d2295d0c1b0e23
        world.Events.Add(154.10f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }


    private void Run_Exdeath_400040D8_1()
    {
        SimEnemy? exdeath_400040D8_1 = null;
        // [149.34s] 03|400040D8|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|52bee4d181520609
        world.Events.Add(149.34f, () => exdeath_400040D8_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040D8|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|cd649cb6b144a9b2
        world.Events.Add(149.73f, () => exdeath_400040D8_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-11.330f, -0.015f, -1.137f), castSeconds: 2.700f));
        // [152.71s] 21|400040D8|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088C8|0|0|00||01|BB0D|BB0D|1.100|7FFF|b3e77291fb03a81e
        // [152.71s] 264|400040D8|BB0D|000088C8|1|88.670|98.863|-0.015|0.000|E0000000|c006bf59d75d4d1a
    }

    private void Run_Exdeath_400040D9_1()
    {
        SimEnemy? exdeath_400040D9_1 = null;
        // [149.34s] 03|400040D9|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|19df72dfb8bdd4cb
        world.Events.Add(149.34f, () => exdeath_400040D9_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040D9|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|40289ec0408d57cc
        world.Events.Add(149.73f, () => exdeath_400040D9_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-10.811f, -0.015f, 0.481f), castSeconds: 2.700f));
        // [152.71s] 21|400040D9|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088C9|0|0|00||01|BB0D|BB0D|1.100|7FFF|2b19d1dc0de3c303
        // [152.71s] 264|400040D9|BB0D|000088C9|1|89.189|100.481|-0.015|0.000|E0000000|407f1d7206bf507d
    }

    private void Run_Exdeath_400040DA_1()
    {
        SimEnemy? exdeath_400040DA_1 = null;
        // [149.34s] 03|400040DA|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|cf1e8475af5c2bc8
        world.Events.Add(149.34f, () => exdeath_400040DA_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DA|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|bb834ade4930f105
        world.Events.Add(149.73f, () => exdeath_400040DA_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(13.176f, -0.015f, 2.251f), castSeconds: 2.700f));
        // [152.71s] 21|400040DA|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CA|0|0|00||01|BB0D|BB0D|1.100|7FFF|1b1c0a27eb5ff8e7
        // [152.71s] 264|400040DA|BB0D|000088CA|1|113.176|102.251|-0.015|0.000|E0000000|d0c0961aa9aa3a65
    }

    private void Run_Exdeath_400040DB_1()
    {
        SimEnemy? exdeath_400040DB_1 = null;
        // [149.34s] 03|400040DB|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|0f2a7ff4e8d60380
        world.Events.Add(149.34f, () => exdeath_400040DB_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DB|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|6bf7a7813a46ca8d
        world.Events.Add(149.73f, () => exdeath_400040DB_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(12.627f, -0.015f, 0.420f), castSeconds: 2.700f));
        // [152.71s] 21|400040DB|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CB|0|0|00||01|BB0D|BB0D|1.100|7FFF|d02f992b7e969258
        // [152.71s] 264|400040DB|BB0D|000088CB|1|112.627|100.420|-0.015|0.000|E0000000|22d14c4696c5d567
    }

    private void Run_Exdeath_400040DC_1()
    {
        SimEnemy? exdeath_400040DC_1 = null;
        // [149.34s] 03|400040DC|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|22c182591ee91068
        world.Events.Add(149.34f, () => exdeath_400040DC_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DC|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|adbec99629e74602
        world.Events.Add(149.73f, () => exdeath_400040DC_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(2.403f, -0.015f, 12.474f), castSeconds: 2.700f));
        // [152.71s] 21|400040DC|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CC|0|0|00||01|BB0D|BB0D|1.100|7FFF|186c0648f9312a70
        // [152.71s] 264|400040DC|BB0D|000088CC|1|102.403|112.474|-0.015|0.000|E0000000|c682199519d229e8
    }

    private void Run_Exdeath_400040DD_1()
    {
        SimEnemy? exdeath_400040DD_1 = null;
        // [149.34s] 03|400040DD|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|73d06fddb58f4b2b
        world.Events.Add(149.34f, () => exdeath_400040DD_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DD|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|80e3a2285c599d61
        world.Events.Add(149.73f, () => exdeath_400040DD_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-2.388f, -0.015f, -14.534f), castSeconds: 2.700f));
        // [152.71s] 21|400040DD|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CD|0|0|00||01|BB0D|BB0D|1.100|7FFF|c1e6eae8fe36454d
        // [152.71s] 264|400040DD|BB0D|000088CD|1|97.612|85.466|-0.015|0.000|E0000000|f33349587ba024d1
    }

    private void Run_Exdeath_400040DE_1()
    {
        SimEnemy? exdeath_400040DE_1 = null;
        // [149.34s] 03|400040DE|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|fd353c1df54ad619
        world.Events.Add(149.34f, () => exdeath_400040DE_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DE|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|29ddc05c5fc94567
        world.Events.Add(149.73f, () => exdeath_400040DE_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(-1.686f, -0.015f, -13.130f), castSeconds: 2.700f));
        // [152.71s] 21|400040DE|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CE|0|0|00||01|BB0D|BB0D|1.100|7FFF|de12ddcba3c46424
        // [152.71s] 264|400040DE|BB0D|000088CE|1|98.314|86.870|-0.015|0.000|E0000000|2866a351b48de195
    }

    private void Run_Exdeath_400040DF_1()
    {
        SimEnemy? exdeath_400040DF_1 = null;
        // [149.34s] 03|400040DF|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|90973910f7b33432
        world.Events.Add(149.34f, () => exdeath_400040DF_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [149.73s] 20|400040DF|Exdeath|BB0D|Blizzard III|E0000000||2.700|100.00|90.00|0.00|0.00|59e530b7866da6d9
        world.Events.Add(149.73f, () => exdeath_400040DF_1?.Cast(ActionId.BlizzardIII, targetLocation: new Vector3(2.190f, -0.015f, 11.589f), castSeconds: 2.700f));
        // [152.71s] 21|400040DF|Exdeath|BB0D|Blizzard III|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|90.00|0.00|0.00|000088CF|0|0|00||01|BB0D|BB0D|1.100|7FFF|e267b993fe5c6d5d
        // [152.71s] 264|400040DF|BB0D|000088CF|1|102.190|111.589|-0.015|0.000|E0000000|4a676afc0f1ec812
    }

    private void Run_Kefka_400040D7()
    {
        SimEnemy? kefka_400040D7 = null;
        // [-1158.34s] 03|400040D7|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|c8006d0ede2badd1
        // world.Events.Add(-1158.34f, () => kefka_400040D7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [-1158.34s] 261|Add|400040D7|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|cb7e88037534751a
        // [-1157.24s] 261|Change|400040D7|ModelStatus|0|42ab55ce0977baad
        // world.Events.Add(-1157.24f, () => kefka_400040D7?.SetVisible(true));
        // [150.48s] 271|400040D7|2.3562|00|00|92.9289|107.0711|0.0000|96c25aaa3ce2c3ab
        world.Events.Add(150.48f, () => kefka_400040D7?.SetPosition(new Placement(new Vector3(-7.071f, 0.000f, 7.071f), 2.356f)));
        // [150.71s] 20|400040D7|Kefka|BAF0|Stomp-a-Mole|400040D7|Kefka|1.200|92.93|107.07|0.00|2.36|3f132f26e7358fbf
        world.Events.Add(150.71f, () => kefka_400040D7?.Cast(ActionId.StompAMole, targetLocation: new Vector3(-7.088f, -0.015f, 7.042f), castSeconds: 1.200f, targetId: kefka_400040D7?.GameObjectId));
        // [152.18s] 22|400040D7|Kefka|BAF0|Stomp-a-Mole|100AC8F1|CasterDps|750603|65CE4002|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||94.05|106.46|-0.02|1.51|44|44|0|10000|||92.93|107.07|0.00|2.36|000088C2|0|2|00||01|BAF0|BAF0|1.100|DFFF|34a564a8e43e05ce
        // [152.18s] 22|400040D7|Kefka|BAF0|Stomp-a-Mole|10018AEA|MeleeDpsB|750603|72664002|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||94.74|107.47|0.00|-3.02|44|44|0|10000|||92.93|107.07|0.00|2.36|000088C2|1|2|00||01|BAF0|BAF0|1.100|DFFF|89c7b890603bcf35
        world.Events.Add(152.18f, () => kefka_400040D7?.Cast(ActionId.StompAMole, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [152.18s] 264|400040D7|BAF0|000088C2|1|-0.015|-0.015|-0.015|2.356|400040D7|bc2fd4320295b574
        // [152.18s] 26|B7D|Magic Vulnerability Up|3.00|400040D7|Kefka|10018AEA|MeleeDpsB|00|226668|44|840ec347a4f2d81d
        world.Events.Add(152.18f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [152.18s] 26|B7D|Magic Vulnerability Up|3.00|400040D7|Kefka|100AC8F1|CasterDps|00|227550|44|af50c037a24da19a
        world.Events.Add(152.18f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [155.21s] 30|B7D|Magic Vulnerability Up|0.00|400040D7|Kefka|10018AEA|MeleeDpsB|00|226668|44|e323c64f92f575e8
        world.Events.Add(155.21f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [155.21s] 30|B7D|Magic Vulnerability Up|0.00|400040D7|Kefka|100AC8F1|CasterDps|00|227550|44|762b6041cc5b1e0c
        world.Events.Add(155.21f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }

    private void Run_Kefka_400040D6()
    {
        SimEnemy? kefka_400040D6 = null;
        // [-1158.34s] 03|400040D6|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|90.00|0.00|0.00|d07eed45a4fd970f
        // world.Events.Add(-1158.34f, () => kefka_400040D6 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -10.000f), 0.000f))));
        // [-1158.34s] 261|Add|400040D6|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|7379f868dd49aac7
        // [-1157.24s] 261|Change|400040D6|ModelStatus|0|3172d3ab975f9697
        // world.Events.Add(-1157.24f, () => kefka_400040D6?.SetVisible(true));
        // [150.62s] 271|400040D6|-0.7855|00|00|107.0711|92.9289|0.0000|e50b4b7bcfc66abc
        world.Events.Add(150.62f, () => kefka_400040D6?.SetPosition(new Placement(new Vector3(7.071f, 0.000f, -7.071f), -0.785f)));
        // [152.00s] 20|400040D6|Kefka|BAF0|Stomp-a-Mole|400040D6|Kefka|1.200|107.07|92.93|0.00|-0.79|fe4326de83098878
        world.Events.Add(152.00f, () => kefka_400040D6?.Cast(ActionId.StompAMole, targetLocation: new Vector3(7.042f, -0.015f, -7.088f), castSeconds: 1.200f, targetId: kefka_400040D6?.GameObjectId));
        // [151.71s] 261|Change|400040D6|CastBuffID|47856|CastDurationCurrent|0.0161|CastDurationMax|1.2000|CastTargetID|400040D6|IsCasting1|1|IsCasting2|1|7a0087ae6be3c6b5
        // [153.47s] 22|400040D6|Kefka|BAF0|Stomp-a-Mole|100A7A8F|OffTank|750603|20DC4002|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||107.09|92.89|0.00|-1.58|44|44|0|10000|||107.07|92.93|0.00|-0.79|000088D5|0|2|00||01|BAF0|BAF0|1.100|5FFF|3b89b4b390477170
        // [153.47s] 22|400040D6|Kefka|BAF0|Stomp-a-Mole|100702A3|Player|750603|F1954001|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|205207|205207|5800|10000|||107.38|93.52|0.00|-2.43|44|44|0|10000|||107.07|92.93|0.00|-0.79|000088D5|1|2|00||01|BAF0|BAF0|1.100|5FFF|de427ec61f26d32c
        world.Events.Add(153.47f, () => kefka_400040D6?.Cast(ActionId.StompAMole, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [153.47s] 264|400040D6|BAF0|000088D5|1|-0.015|-0.015|-0.015|-0.785|400040D6|48e84bc267fc13c1
        // [153.47s] 26|B7D|Magic Vulnerability Up|3.00|400040D6|Kefka|100A7A8F|OffTank|00|217488|44|95572573b329f740
        world.Events.Add(153.47f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [153.47s] 26|B7D|Magic Vulnerability Up|3.00|400040D6|Kefka|100702A3|Player|00|205207|44|f134a51f767d7235
        world.Events.Add(153.47f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [153.03s] 261|Change|400040D6|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|f6177ead0076f70f
        // [156.50s] 30|B7D|Magic Vulnerability Up|0.00|400040D6|Kefka|100A7A8F|OffTank|00|217488|44|3eaea0aafe9ab456
        world.Events.Add(156.50f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [156.50s] 30|B7D|Magic Vulnerability Up|0.00|400040D6|Kefka|100702A3|Player|00|205207|44|305cb4a313229d83
        world.Events.Add(156.50f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }

    private void Run_Kefka_400040E9_12()
    {
        SimEnemy? kefka_400040E9_12 = null;
        // [151.91s] 271|400040E9|2.3562|00|00|92.9289|107.0711|0.0000|242cd3d347f25360
        world.Events.Add(151.91f, () => kefka_400040E9_12?.SetPosition(new Placement(new Vector3(-7.071f, 0.000f, 7.071f), 2.356f)));
        // [151.71s] 261|Change|400040E9|BNpcNameID|1BDB|CastGroundTargetX|110.4905|CastGroundTargetY|109.8801|CastGroundTargetZ|-0.0153|Heading|2.3562|Name|Kefka|PosX|92.9289|PosY|107.0711|PosZ|0.0000|6287b148ec20c8fd
        // [151.71s] 03|400040E9|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||92.93|107.07|0.00|2.36|554d83b81ea14baf
        world.Events.Add(151.71f, () => kefka_400040E9_12 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-7.070f, 0.000f, 7.070f), 2.360f))));
        // [153.29s] 20|400040E9|Kefka|BAF0|Stomp-a-Mole|400040E9|Kefka|1.200|92.93|107.07|0.00|2.36|27fd6ad1a49a6451
        world.Events.Add(153.29f, () => kefka_400040E9_12?.Cast(ActionId.StompAMole, targetLocation: new Vector3(-7.088f, -0.015f, 7.042f), castSeconds: 1.200f, targetId: kefka_400040E9_12?.GameObjectId));
        // [152.92s] 261|Change|400040E9|CastBuffID|47856|CastDurationCurrent|0.0170|CastDurationMax|1.2000|CastTargetID|400040E9|IsCasting1|1|IsCasting2|1|3ae89d393aa4d931
        // [154.77s] 22|400040E9|Kefka|BAF0|Stomp-a-Mole|1009061B|MeleeDpsA|750603|2B054002|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|149526|205177|6395|10000|||94.59|108.20|0.00|-2.35|44|44|0|10000|||92.93|107.07|0.00|2.36|000088DE|0|2|00||01|BAF0|BAF0|1.100|DFFF|51fbc859270a5890
        // [154.77s] 22|400040E9|Kefka|BAF0|Stomp-a-Mole|10066D86|ShieldHealer|750603|B65A4001|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|197585|205177|8410|10000|||95.22|106.54|0.00|-2.31|44|44|0|10000|||92.93|107.07|0.00|2.36|000088DE|1|2|00||01|BAF0|BAF0|1.100|DFFF|64d0748975b78e96
        world.Events.Add(154.77f, () => kefka_400040E9_12?.Cast(ActionId.StompAMole, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [154.77s] 264|400040E9|BAF0|000088DE|1|-0.015|-0.015|-0.015|2.356|400040E9|acff8d32d83fb137
        // [154.77s] 26|B7D|Magic Vulnerability Up|3.00|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|cc6c65c004f956ac
        world.Events.Add(154.77f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [154.77s] 26|B7D|Magic Vulnerability Up|3.00|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|545c705f2d21f754
        world.Events.Add(154.77f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [154.42s] 261|Change|400040E9|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|b3f2b6ca3d56edaa
        // [157.80s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|2d4351cdf934e78b
        world.Events.Add(157.80f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [157.80s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|34b248927aecfda2
        world.Events.Add(157.80f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
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

    private void Run_Kefka_400040E7_3()
    {
        SimEnemy? kefka_400040E7_3 = null;
        // [153.20s] 271|400040E7|-0.7855|00|00|107.0711|92.9289|0.0000|58dfb4b9df46eec7
        world.Events.Add(153.20f, () => kefka_400040E7_3?.SetPosition(new Placement(new Vector3(7.071f, 0.000f, -7.071f), -0.785f)));
        // [152.92s] 03|400040E7|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||107.07|92.93|0.00|-0.79|9c7ad75052833cd5
        world.Events.Add(152.92f, () => kefka_400040E7_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(7.070f, 0.000f, -7.070f), -0.790f))));
        // [154.63s] 20|400040E7|Kefka|BAF0|Stomp-a-Mole|400040E7|Kefka|1.200|107.07|92.93|0.00|-0.79|226daed647e316bd
        world.Events.Add(154.63f, () => kefka_400040E7_3?.Cast(ActionId.StompAMole, targetLocation: new Vector3(7.042f, -0.015f, -7.088f), castSeconds: 1.200f, targetId: kefka_400040E7_3?.GameObjectId));
        // [154.31s] 261|Change|400040E7|CastBuffID|47856|CastDurationCurrent|0.0168|CastDurationMax|1.2000|CastTargetID|400040E7|IsCasting1|1|IsCasting2|1|27efd493f0fb7092
        // [156.11s] 22|400040E7|Kefka|BAF0|Stomp-a-Mole|100AF82E|MainTank|750003|C33B4001|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|260660|325133|8000|10000|||107.68|92.91|0.00|-2.77|44|44|0|10000|||107.07|92.93|0.00|-0.79|000088EB|0|2|00||01|BAF0|BAF0|1.100|5FFF|4904ab2945c61790
        // [156.11s] 22|400040E7|Kefka|BAF0|Stomp-a-Mole|100AE96C|RegenHealer|750603|61394001|E80E|B7D0000|1B|BAF08000|0|0|0|0|0|0|0|0|0|0|259563|325047|9500|10000|||105.57|91.66|0.00|-2.83|44|44|0|10000|||107.07|92.93|0.00|-0.79|000088EB|1|2|00||01|BAF0|BAF0|1.100|5FFF|c0038e183c0a53ef
        world.Events.Add(156.11f, () => kefka_400040E7_3?.Cast(ActionId.StompAMole, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [156.11s] 264|400040E7|BAF0|000088EB|1|-0.015|-0.015|-0.015|-0.785|400040E7|d56f69bb7b4a2795
        // [156.11s] 26|B7D|Magic Vulnerability Up|3.00|400040E7|Kefka|100AF82E|MainTank|00|325133|44|64f4c7d1789232b0
        world.Events.Add(156.11f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [156.11s] 26|B7D|Magic Vulnerability Up|3.00|400040E7|Kefka|100AE96C|RegenHealer|00|325047|44|a12131b6e4d193d8
        world.Events.Add(156.11f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 3.000f));
        // [155.80s] 261|Change|400040E7|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|a429fad9025f73e8
        // [159.14s] 30|B7D|Magic Vulnerability Up|0.00|400040E7|Kefka|100AF82E|MainTank|00|325133|44|d5379a6a95bd67b6
        world.Events.Add(159.14f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [159.14s] 30|B7D|Magic Vulnerability Up|0.00|400040E7|Kefka|100AE96C|RegenHealer|00|325047|44|10ae5835bc7f93ae
        world.Events.Add(159.14f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }
}
