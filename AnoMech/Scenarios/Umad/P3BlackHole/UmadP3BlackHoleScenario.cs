// Rehomed from tools/parser.py --code output into the canonical scenario layout.
// Player id -> role (first-seen order inside the window):
//   1001875C MT, 10056A3A OT, 10019262 H1, 10018A1B H2,
//   10018BF0 M1, 10018DC8 M2, 100188C6 R1, 1004E71C C.
using System.Collections.Generic;
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
    public IReadOnlyList<Waymark> Waymarks { get; } = UmadUmadWaymarks;
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

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        UmadRsvStrings.Seed();
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP3BlackHoleState(party, settingsWindow.Overrides);
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP3BlackHoleState>)AiStrats[idx]).Run(state, world);

        Run_Chaos_4000414D();
        Run_Exdeath_4000414C();
        Run_Chaos_400040E8_0();
        Run_Exdeath_400040E9_0();
        Run_Exdeath_400040E8_1();
        Run_Chaos_400040E9_1();
        Run_Kefka_40004141();
        Run_EventObj_1EC03D_40004163();
        Run_Chaos_400040E5_0();
        Run_Chaos_400040E6_0();
        Run_Chaos_400040E7_0();
        Run_Kefka_400040E5_1();
        Run_Kefka_400040E6_1();
        Run_Kefka_400040E7_1();
        Run_Kefka_400040E8_2();
        Run_Kefka_400040E9_2();
        Run_Black_Hole_40004166();
        Run_Black_Hole_40004167();
        Run_Black_Hole_40004168();
        Run_Black_Hole_40004169();
        Run_Black_Hole_4000416A();
        Run_Black_Hole_4000416B();
        Run_Black_Hole_4000416C();
        Run_Black_Hole_4000416D();
        Run_Black_Hole_4000416E();
        Run_Black_Hole_4000416F();
        Run_Black_Hole_40004170();
        Run_Exdeath_400040E9_3();
        Run_Kefka_400040E9_4();
        Run_Black_Hole_40004177();
        Run_Black_Hole_40004178();
        Run_Black_Hole_40004179();
        Run_Black_Hole_4000417A();
        Run_Black_Hole_4000417B();
        Run_Black_Hole_4000417C();
        Run_Black_Hole_4000417D();
        Run_Black_Hole_4000417E();
        Run_Black_Hole_4000417F();
        Run_Black_Hole_40004180();
        Run_Black_Hole_40004181();
        Run_Chaos_400040E9_5();
        Run_Chaos_400040E8_3();
        Run_Kefka_400040E9_6();
        Run_Exdeath_400040E9_7();
        Run_Black_Hole_40004185();
        Run_Black_Hole_40004186();
        Run_Black_Hole_40004187();
        Run_Black_Hole_40004188();
        Run_Black_Hole_40004189();
        Run_Black_Hole_4000418A();
        Run_Black_Hole_4000418B();
        Run_Black_Hole_4000418C();
        Run_Black_Hole_4000418D();
        Run_Black_Hole_4000418E();
        Run_Black_Hole_4000418F();
        Run_Chaos_400040E9_8();
        Run_Kefka_400040E8_4();
        Run_Kefka_400040E9_9();
        Run_Chaos_400040E4_0();
        Run_Chaos_400040E5_2();
        Run_Black_Hole_40004194();
        Run_Black_Hole_40004195();
        Run_Black_Hole_40004196();
        Run_Black_Hole_40004197();
        Run_Black_Hole_40004198();
        Run_Black_Hole_40004199();
        Run_Black_Hole_4000419A();
        Run_Black_Hole_4000419B();
        Run_Black_Hole_4000419C();
        Run_Black_Hole_4000419D();
        Run_Black_Hole_4000419E();
        Run_Chaos_400040E9_10();
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
        Run_Kefka_400040D8_0();
        Run_Kefka_400040D9_0();
        Run_Kefka_400040DA_0();
        Run_Kefka_400040DB_0();
        Run_Kefka_400040DC_0();
        Run_Kefka_400040DD_0();
        Run_Kefka_400040DE_0();
        Run_Kefka_400040DF_0();
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
        Run_Graven_Image_4000414B();
        Run_InstanceEvents();
        Run_OtherDebuffs();
        Run_PlayerLockons();
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
        world.Events.Add(8.37f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.Accretion, 14.000f));
        // [8.37s] 26|154E|Primordial Crust|72.00|E0000000||10066D86|ShieldHealer|00|205177||40abadbd494724d8
        world.Events.Add(8.37f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.PrimordialCrust, 72.000f));
        // [8.37s] 26|BBC|First in Line|9999.00|E0000000||10066D86|ShieldHealer|00|205177||8b7b383ce23e0a63
        world.Events.Add(8.37f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.FirstInLine));
        // [8.37s] 26|154E|Primordial Crust|139.00|E0000000||100AF82E|MainTank|00|325133||0ea5a49eadf4a1e3
        world.Events.Add(8.37f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.PrimordialCrust, 139.000f));
        // [8.37s] 26|BBE|Third in Line|9999.00|E0000000||100AF82E|MainTank|00|325133||138333913388025f
        world.Events.Add(8.37f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.ThirdInLine));
        // [8.37s] 26|154E|Primordial Crust|106.00|E0000000||100AE96C|RegenHealer|00|325047||62011067419d46e7
        world.Events.Add(8.37f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.PrimordialCrust, 106.000f));
        // [8.37s] 26|BBD|Second in Line|9999.00|E0000000||100AE96C|RegenHealer|00|325047||810f4c84e5f69416
        world.Events.Add(8.37f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.SecondInLine));
        // [8.37s] 26|154E|Primordial Crust|72.00|E0000000||1009061B|MeleeDpsA|00|205177||9ad4bea7226f6a40
        world.Events.Add(8.37f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.PrimordialCrust, 72.000f));
        // [8.37s] 26|BBC|First in Line|9999.00|E0000000||1009061B|MeleeDpsA|00|205177||8ffafdef552fc17e
        world.Events.Add(8.37f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.FirstInLine));
        // [8.37s] 26|644|Accretion|14.00|E0000000||100702A3|Player|00|205207||5b9de7742bdd95d7
        world.Events.Add(8.37f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.Accretion, 14.000f));
        // [8.37s] 26|154E|Primordial Crust|106.00|E0000000||100702A3|Player|00|205207||c5c1976af56b8d6b
        world.Events.Add(8.37f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.PrimordialCrust, 106.000f));
        // [8.37s] 26|BBD|Second in Line|9999.00|E0000000||100702A3|Player|00|205207||ef9ca76883c5f78b
        world.Events.Add(8.37f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.SecondInLine));
        // [8.37s] 26|154E|Primordial Crust|139.00|E0000000||100A7A8F|OffTank|00|217488||ecd72da057e23e52
        world.Events.Add(8.37f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.PrimordialCrust, 139.000f));
        // [8.37s] 26|BBE|Third in Line|9999.00|E0000000||100A7A8F|OffTank|00|217488||0c1120e07dc4fc55
        world.Events.Add(8.37f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.ThirdInLine));
        // [8.37s] 26|154E|Primordial Crust|106.00|E0000000||10018AEA|MeleeDpsB|00|226668||3f98f3be444599c6
        world.Events.Add(8.37f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.PrimordialCrust, 106.000f));
        // [8.37s] 26|BBD|Second in Line|9999.00|E0000000||10018AEA|MeleeDpsB|00|226668||d9f28715ff0fd9ae
        world.Events.Add(8.37f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.SecondInLine));
        // [8.37s] 26|154E|Primordial Crust|72.00|E0000000||100AC8F1|CasterDps|00|227550||b620bfcba428750d
        world.Events.Add(8.37f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.PrimordialCrust, 72.000f));
        // [8.37s] 26|BBC|First in Line|9999.00|E0000000||100AC8F1|CasterDps|00|227550||5ad388bf32630f59
        world.Events.Add(8.37f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.FirstInLine));
        // [11.26s] 30|644|Accretion|0.00|E0000000||10066D86|ShieldHealer|00|205177||6512b700631905d8
        world.Events.Add(11.26f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.Accretion));
        // [15.18s] 30|644|Accretion|0.00|E0000000||100702A3|Player|00|205207||312d6fcf109ed87e
        world.Events.Add(15.18f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.Accretion));
        // [63.32s] 30|154E|Primordial Crust|0.00|E0000000||1009061B|MeleeDpsA|00|205177||7320f793f4e074f2
        world.Events.Add(63.32f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.PrimordialCrust));
        // [63.32s] 30|BBC|First in Line|0.00|E0000000||1009061B|MeleeDpsA|00|205177||b42df43f8192587a
        world.Events.Add(63.32f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.FirstInLine));
        // [68.41s] 30|154E|Primordial Crust|0.00|E0000000||10066D86|ShieldHealer|00|205177||04ffafa6a8a08744
        world.Events.Add(68.41f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.PrimordialCrust));
        // [68.41s] 30|BBC|First in Line|0.00|E0000000||10066D86|ShieldHealer|00|205177||5b07deaf8f6b8262
        world.Events.Add(68.41f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.FirstInLine));
        // [73.49s] 30|154E|Primordial Crust|0.00|E0000000||100AC8F1|CasterDps|00|227550||0e1191338feb11be
        world.Events.Add(73.49f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.PrimordialCrust));
        // [73.49s] 30|BBC|First in Line|0.00|E0000000||100AC8F1|CasterDps|00|227550||efd935639791da1d
        world.Events.Add(73.49f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.FirstInLine));
        // [97.62s] 30|154E|Primordial Crust|0.00|E0000000||10018AEA|MeleeDpsB|00|226668||abacaaf96a6533c3
        world.Events.Add(97.62f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.PrimordialCrust));
        // [97.62s] 30|BBD|Second in Line|0.00|E0000000||10018AEA|MeleeDpsB|00|226668||589d1332ee93fa84
        world.Events.Add(97.62f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.SecondInLine));
        // [102.75s] 30|154E|Primordial Crust|0.00|E0000000||100702A3|Player|00|205207||a7c89d7b8184bef9
        world.Events.Add(102.75f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.PrimordialCrust));
        // [102.75s] 30|BBD|Second in Line|0.00|E0000000||100702A3|Player|00|205207||ccd6a899972a8698
        world.Events.Add(102.75f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.SecondInLine));
        // [107.80s] 30|154E|Primordial Crust|0.00|E0000000||100AE96C|RegenHealer|00|325047||f7a695e6d5257b5f
        world.Events.Add(107.80f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.PrimordialCrust));
        // [107.80s] 30|BBD|Second in Line|0.00|E0000000||100AE96C|RegenHealer|00|325047||eccef9cf2bffd532
        world.Events.Add(107.80f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.SecondInLine));
        // [131.10s] 30|154E|Primordial Crust|0.00|E0000000||100AF82E|MainTank|00|325133||fd9586e13c85d795
        world.Events.Add(131.10f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.PrimordialCrust));
        // [131.10s] 30|BBE|Third in Line|0.00|E0000000||100AF82E|MainTank|00|325133||f3bc6e9328a1da44
        world.Events.Add(131.10f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.ThirdInLine));
        // [138.16s] 30|154E|Primordial Crust|0.00|E0000000||100A7A8F|OffTank|00|217488||2f33241e9a1267b4
        world.Events.Add(138.16f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.PrimordialCrust));
        // [138.16s] 30|BBE|Third in Line|0.00|E0000000||100A7A8F|OffTank|00|217488||5e1e96bf8cab2624
        world.Events.Add(138.16f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.ThirdInLine));
    }

    private void Run_PlayerLockons()
    {
        // [147.09s] 27|10066D86|ShieldHealer|0000|0000|00A1|10066D86|0000|0000|3f0ea41d01e62431
        world.Events.Add(147.09f, () => party.Get(PartyRole.ShieldHealer)?.AttachLockonVfx(LockonId.X_A1, persistent: false));
        // [152.58s] 27|10018AEA|MeleeDpsB|0000|0000|00A1|10018AEA|0000|0000|36244b9ade96a270
        world.Events.Add(152.58f, () => party.Get(PartyRole.MeleeDpsB)?.AttachLockonVfx(LockonId.X_A1, persistent: false));
    }

    public void Tick(float delta, float elapsed) { }

    private void Run_Chaos_4000414D()
    {
        SimEnemy? chaos_4000414D = null;
        // [-132.71s] 03|4000414D|Chaos|00|64|0000|00||7691|19508|37552669|37552669|10000|10000|||92.00|100.00|0.00|0.00|8cec0813d9d24627
        world.Events.Add(0f, () => chaos_4000414D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100, Targetable: true, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(new Vector3(-8.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(0.98f, () => chaos_4000414D?.Cast(ActionId.Earthquake, targetLocation: new Vector3(-8.522f, 0.198f, -2.785f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [0.64s] 261|Change|4000414D|CastBuffID|50545|CastDurationCurrent|0.0170|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|-1.3353|IsCasting1|1|IsCasting2|1|27d254f31ead2148
        // [5.97s] 21|4000414D|Chaos|C571|Earthquake|4000414D|Chaos|1B|C5718000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|18235771|37552669|10000|10000|||91.48|97.22|0.00|-1.34|18235771|37552669|10000|10000|||91.48|97.22|0.00|-1.34|00008400|0|1|00||01|C571|C571|3.100|4998|9f84fa2ccb108c83
        // [5.97s] 264|4000414D|C571|00008400|0||||-1.335|4000414D|c41b3232432cce70
        // [5.66s] 261|Change|4000414D|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|f5d84e66e89a2af7
        // [10.59s] 270|4000414D|1.2128|0000|005A|93.7361|98.0697|0.1983|fcb753b36096f876
        world.Events.Add(10.59f, () => chaos_4000414D?.SetPosition(new Vector3(-6.264f, 0.198f, -1.930f)));
        // [10.91s] 270|4000414D|1.2475|0000|005A|95.4757|98.7106|0.1983|b28781f66ce28769
        world.Events.Add(10.91f, () => chaos_4000414D?.SetPosition(new Vector3(-4.524f, 0.198f, -1.289f)));
        // [11.22s] 270|4000414D|1.4168|0000|005A|96.1165|98.8326|0.1983|01010ee233e3e288
        world.Events.Add(11.22f, () => chaos_4000414D?.SetPosition(new Vector3(-3.883f, 0.198f, -1.167f)));
        // [12.07s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|103955|325133|5000|10000|||103.18|100.62|0.00|-0.22|17697244|37552669|10000|10000|||96.06|98.82|0.00|1.38|00008431|0|1|00||01|C252|C252|0.100|B302|5a7d0c9a070eb3f3
        world.Events.Add(12.07f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [12.07s] 264|4000414D|C252|00008431|0||||1.252|100AF82E|7a2a37a998d8b0a8
        // [12.47s] 270|4000414D|1.3405|0000|005A|97.2762|99.1073|0.1983|a30d27948ec3af39
        world.Events.Add(12.47f, () => chaos_4000414D?.SetPosition(new Vector3(-2.724f, 0.198f, -0.893f)));
        // [12.78s] 270|4000414D|1.3578|0000|005A|98.4054|99.3514|0.1983|6ced23ef7c705bff
        world.Events.Add(12.78f, () => chaos_4000414D?.SetPosition(new Vector3(-1.595f, 0.198f, -0.649f)));
        // [13.09s] 270|4000414D|1.4149|0000|005A|100.6332|99.8092|0.1983|4d24fa464cbadc71
        world.Events.Add(13.09f, () => chaos_4000414D?.SetPosition(new Vector3(0.633f, 0.198f, -0.191f)));
        // [15.09s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|71660000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|130715|325133|5800|10000|||102.19|100.18|0.00|-1.82|17509532|37552669|10000|10000|||100.63|99.81|0.00|1.34|0000844B|0|1|00||01|C252|C252|0.100|B6A4|403c85b764a482b2
        world.Events.Add(15.09f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [15.09s] 264|4000414D|C252|0000844B|0||||1.341|100AF82E|ac5f60f6e85d15d4
        // [18.13s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|286671|325133|3000|10000|||102.08|100.18|0.00|-1.70|17148554|37552669|10000|10000|||100.63|99.81|0.00|1.34|00008466|0|1|00||01|C252|C252|0.100|EC8C|df81f1f82d139d38
        world.Events.Add(18.13f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [18.13s] 264|4000414D|C252|00008466|0||||2.664|100AF82E|81b6f283c18a7255
        // [19.78s] 270|4000414D|-2.4453|0000|005A|100.2670|99.3820|0.1983|6b775a3e3e71a0ab
        world.Events.Add(19.78f, () => chaos_4000414D?.SetPosition(new Vector3(0.267f, 0.198f, -0.618f)));
        // [21.16s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|2E4B0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|3800|10000|||94.74|92.79|0.00|-1.49|16910002|37552669|10000|10000|||100.27|99.38|0.00|-2.45|0000847D|0|1|00||01|C252|C252|0.100|1C77|4c3747c275e42469
        world.Events.Add(21.16f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [21.16s] 264|4000414D|C252|0000847D|0||||-2.443|100AF82E|642eacf015f93d02
        // [24.19s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|30474001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|4000|10000|||94.71|92.79|0.00|0.70|16634614|37552669|10000|10000|||100.27|99.38|0.00|-2.45|00008496|0|1|00||01|C252|C252|0.100|1C77|1ac8a4103f29c799
        world.Events.Add(24.19f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [24.19s] 264|4000414D|C252|00008496|0||||-2.443|100AF82E|72456faffbe4e783
        // [27.22s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|2BFC4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|150319|325133|4200|10000|||101.10|98.65|0.00|0.87|16374508|37552669|10000|10000|||100.27|99.38|0.00|3.03|000084B0|0|1|00||01|C252|C252|0.100|C18D|64fc9a1245207bfc
        world.Events.Add(27.22f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [27.22s] 264|4000414D|C252|000084B0|0||||1.609|100AF82E|756794aedd24918e
        // [30.25s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|F39C0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|283159|325133|4600|10000|||103.63|99.98|1.79|-1.75|15751145|37552669|10000|10000|||100.27|99.38|0.00|1.40|000084CE|0|1|00||01|C252|C252|0.100|B8E8|ff46f7a3a39f6b42
        world.Events.Add(30.25f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [30.25s] 264|4000414D|C252|000084CE|0||||1.397|100AF82E|6b52c869d14f9217
        // [32.40s] 21|4000414D|Chaos|C2E4|Aetherlink|4000414D|Chaos|1B|C2E48000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|15451929|37552669|10000|10000|||100.27|99.38|0.00|1.35|15451929|37552669|10000|10000|||100.27|99.38|0.00|1.35|000084DF|0|1|00||01|C2E4|C2E4|3.100|B57B|8107d025f85af083
        world.Events.Add(32.40f, () => chaos_4000414D?.Cast(ActionId.Aetherlink, castSeconds: 0f, targetId: chaos_4000414D?.GameObjectId));
        // [32.40s] 264|4000414D|C2E4|000084DF|0||||1.313|4000414D|dba452de03d25bae
        // [33.29s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|6B414001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|300640|325133|5400|10000|||103.29|100.18|-0.02|-1.83|16161084|37552669|10000|10000|||100.27|99.38|0.00|1.31|000084E6|0|1|00||01|C252|C252|2.206|B57B|86bc37be7c9a7a28
        world.Events.Add(33.29f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [33.29s] 264|4000414D|C252|000084E6|0||||1.313|100AF82E|4c2a231f9d9dd303
        // [36.34s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|597C4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|279518|325133|5600|10000|||101.07|99.02|-0.01|2.28|15752892|37552669|10000|10000|||100.27|99.38|0.00|2.24|000084F9|0|1|00||01|C252|C252|0.100|D108|69b5cb8f239cc3d8
        world.Events.Add(36.34f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [36.34s] 264|4000414D|C252|000084F9|0||||1.989|100AF82E|70c6f93753336c22
        // [39.37s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|66D34001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|255186|325133|6400|10000|||103.81|98.62|0.00|-1.37|15433748|37552669|10000|10000|||100.27|99.38|0.00|1.78|00008513|0|1|00||01|C252|C252|0.100|C885|bc17435ffd838f82
        world.Events.Add(39.37f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [39.37s] 264|4000414D|C252|00008513|0||||1.780|100AF82E|ac0ab91eb6c7f45f
        // [42.41s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|19104001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|265606|325133|6600|10000|||103.64|98.41|0.00|-2.27|15170137|37552669|10000|10000|||100.27|99.38|0.00|1.87|0000852A|0|1|00||01|C252|C252|0.100|CC5E|ddc1369b95d8b6cd
        world.Events.Add(42.41f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [42.41s] 264|4000414D|C252|0000852A|0||||1.874|100AF82E|6a88228428ba50dd
        // [45.45s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|63D4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|264992|325133|6800|10000|||102.96|98.03|0.00|-1.06|14915894|37552669|10000|10000|||100.27|99.38|0.00|2.04|00008543|0|1|00||01|C252|C252|0.100|D51B|9f0baa95bdd79dce
        world.Events.Add(45.45f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [45.45s] 264|4000414D|C252|00008543|0||||2.089|100AF82E|9d55aeb7fe2247a7
        // [45.58s] 20|4000414D|Chaos|BB01|Damning Edict|4000414D|Chaos|4.700|100.27|99.38|0.00|2.05|a4b923ec59431de3
        world.Events.Add(45.58f, () => chaos_4000414D?.Cast(ActionId.DamningEdict, targetLocation: new Vector3(0.267f, 0.198f, -0.618f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [45.24s] 261|Change|4000414D|CastBuffID|47873|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|2.0310|IsCasting1|1|IsCasting2|1|PosX|100.2670|PosY|99.3820|PosZ|0.0000|aadebe34bd8e1cba
        // [50.54s] 21|4000414D|Chaos|BB01|Damning Edict|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||14502176|37552669|10000|10000|||100.27|99.38|0.00|1.97|00008567|0|0|00||01|BB01|BB01|3.100|D066|3d5de251e83a51fd
        // [50.54s] 264|4000414D|BB01|00008567|1|-0.015|-0.015|-0.015|1.973|4000414D|051defebf446063b
        // [50.12s] 261|Change|4000414D|CastBuffID|0|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|1.9734|IsCasting1|0|IsCasting2|0|d22428f615e9ba6e
        // [53.48s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|EF370000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|7800|10000|||100.06|90.25|0.00|2.14|14064513|37552669|10000|10000|||100.27|99.38|0.00|1.97|0000858B|0|1|00||01|C252|C252|0.202|D066|212068a6073d1940
        world.Events.Add(53.48f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [53.48s] 264|4000414D|C252|0000858B|0||||1.973|100AF82E|59e35aeed5f54f57
        // [53.97s] 270|4000414D|3.1263|0000|005A|100.2670|98.6190|0.1983|2f7f64066b76aac9
        world.Events.Add(53.97f, () => chaos_4000414D?.SetPosition(new Vector3(0.267f, 0.198f, -1.381f)));
        // [56.51s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|CCFE0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|235205|325133|8600|10000|||100.42|90.59|0.00|0.05|13552183|37552669|10000|10000|||100.27|98.62|0.00|3.12|000085B0|0|1|00||01|C252|C252|0.100|FD67|a714392314a41c50
        world.Events.Add(56.51f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [56.51s] 264|4000414D|C252|000085B0|0||||3.078|100AF82E|99c4e049f6c08716
        // [59.54s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|28284001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|275906|325133|2800|10000|||100.72|99.44|0.00|-2.63|13007487|37552669|10000|10000|||100.27|98.62|0.00|0.49|000085CE|0|1|00||01|C252|C252|0.100|93F8|75a05c89d1d76b73
        world.Events.Add(59.54f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [59.54s] 264|4000414D|C252|000085CE|0||||0.490|100AF82E|959952bb5c927e38
        // [72.49s] 261|Change|4000414D|CastBuffID|47873|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|0.4139|IsCasting1|1|IsCasting2|1|PosY|98.6190|PosZ|0.0000|2a5c4c0ef2d0e191
        // [72.87s] 20|4000414D|Chaos|BB01|Damning Edict|4000414D|Chaos|4.700|100.27|98.62|0.00|0.49|ac44d4bf6c221598
        world.Events.Add(72.87f, () => chaos_4000414D?.Cast(ActionId.DamningEdict, targetLocation: new Vector3(0.267f, 0.198f, -1.381f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [77.82s] 21|4000414D|Chaos|BB01|Damning Edict|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||9025460|37552669|10000|10000|||100.27|98.62|0.00|-1.53|00008679|0|0|00||01|BB01|BB01|3.100|417C|5bf7f0cc2a010a4c
        // [77.82s] 264|4000414D|BB01|00008679|1|-0.015|-0.015|-0.015|-1.534|4000414D|cdea69e553872917
        // [77.57s] 261|Change|4000414D|CastBuffID|0|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|-1.5343|IsCasting1|0|IsCasting2|0|228ce68ddd120b2a
        // [80.90s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|F1730606|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|4800|10000|||108.43|95.63|0.00|-1.24|8760859|37552669|10000|10000|||100.27|98.62|0.00|-1.53|00008694|0|1|00||01|C252|C252|0.100|417C|79225ccf8e1a3d55
        world.Events.Add(80.90f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [80.90s] 264|4000414D|C252|00008694|0||||-1.534|100AF82E|51f38121c410dbdb
        // [83.94s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|547B0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|5000|10000|||104.86|102.66|0.00|-1.38|8369976|37552669|10000|10000|||100.27|98.62|0.00|0.85|000086B6|0|1|00||01|C252|C252|0.100|A249|9b1ca692804eb364
        world.Events.Add(83.94f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [83.94s] 264|4000414D|C252|000086B6|0||||0.842|100AF82E|f54c4f338178e635
        // [85.90s] 270|4000414D|1.7633|0000|005A|101.4572|98.3749|0.1983|b7383858ed8cf6cd
        world.Events.Add(85.90f, () => chaos_4000414D?.SetPosition(new Vector3(1.457f, 0.198f, -1.625f)));
        // [86.21s] 270|4000414D|1.7950|0000|005A|101.6708|98.3444|0.1983|19af1efd17b6321a
        world.Events.Add(86.21f, () => chaos_4000414D?.SetPosition(new Vector3(1.671f, 0.198f, -1.656f)));
        // [86.97s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|9A680000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|322625|325133|2200|10000|||109.84|96.48|0.00|-1.39|7980403|37552669|10000|10000|||101.67|98.34|0.00|1.80|000086D0|0|1|00||01|C252|C252|0.100|C92C|7231b7fdcacec448
        world.Events.Add(86.97f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [86.97s] 264|4000414D|C252|000086D0|0||||1.796|100AF82E|b52c04dc159d60ac
        // [90.00s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|9CB90000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|221868|325133|3000|10000|||98.39|102.31|0.00|-1.16|7650063|37552669|10000|10000|||101.67|98.34|0.00|-0.44|000086EB|0|1|00||01|C252|C252|0.100|6629|947b4de829aebe99
        world.Events.Add(90.00f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [90.00s] 264|4000414D|C252|000086EB|0||||-0.634|100AF82E|7dc4b5fb3f18eb06
        // [90.80s] 270|4000414D|-0.5933|0000|005A|100.9689|99.3820|0.1983|977273657a278c1f
        world.Events.Add(90.80f, () => chaos_4000414D?.SetPosition(new Vector3(0.969f, 0.198f, -0.618f)));
        // [91.11s] 270|4000414D|-0.5785|0000|005A|100.3280|100.3891|0.1983|f9bd8c073eacc1fd
        world.Events.Add(91.11f, () => chaos_4000414D?.SetPosition(new Vector3(0.328f, 0.198f, 0.389f)));
        // [91.42s] 270|4000414D|-0.4664|0000|005A|99.9008|100.9994|0.1983|34a33d797f1da384
        world.Events.Add(91.42f, () => chaos_4000414D?.SetPosition(new Vector3(-0.099f, 0.198f, 0.999f)));
        // [93.02s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|F1730006|944A0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|204713|325133|3200|10000|||99.24|99.67|0.00|-3.14|7344213|37552669|10000|10000|||99.90|101.00|0.00|-1.22|000086FD|0|1|00||01|C252|C252|0.100|0855|b415ae3c85cf24f0
        world.Events.Add(93.02f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [93.02s] 264|4000414D|C252|000086FD|0||||-2.937|100AF82E|f1c5b8a8d9d7277e
        // [99.18s] 21|4000414D|Chaos|C2E4|Aetherlink|4000414D|Chaos|1B|C2E48000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|6819900|37552669|10000|10000|||99.90|101.00|0.00|2.19|6819900|37552669|10000|10000|||99.90|101.00|0.00|2.19|00008729|0|1|00||01|C2E4|C2E4|3.100|D7B1|ce3359ec3b6e0cfb
        world.Events.Add(99.18f, () => chaos_4000414D?.Cast(ActionId.Aetherlink, castSeconds: 0f, targetId: chaos_4000414D?.GameObjectId));
        // [99.18s] 264|4000414D|C2E4|00008729|0||||2.152|4000414D|527a9a735090b1d6
        // [102.66s] 270|4000414D|2.0048|0000|005A|100.7248|100.6332|0.1983|1c503f8a8c528cd2
        world.Events.Add(102.66f, () => chaos_4000414D?.SetPosition(new Vector3(0.725f, 0.198f, 0.633f)));
        // [113.38s] 20|4000414D|Chaos|BAFE|Latitudinal Implosion|4000414D|Chaos|4.700|100.72|100.63|0.00|1.58|3417f14d9671e680
        world.Events.Add(113.38f, () => chaos_4000414D?.Cast(ActionId.LatitudinalImplosion, targetLocation: new Vector3(0.725f, 0.198f, 0.633f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [113.08s] 261|Change|4000414D|CastBuffID|47870|CastDurationCurrent|0.0145|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|1.5798|IsCasting1|1|IsCasting2|1|PosX|100.7247|PosY|100.6332|PosZ|0.0000|cd74d679ea5e6c06
        // [117.96s] 261|Change|4000414D|CastDurationCurrent|4.8817|Heading|1.5690|IsCasting1|0|68a36853b8382d66
        // [117.96s] 261|Change|4000414D|CastDurationCurrent|4.8817|Heading|1.5690|IsCasting1|0|9371908515f131ac
        // [118.34s] 21|4000414D|Chaos|BAFE|Latitudinal Implosion|4000414D|Chaos|1B|BAFE8000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|5325484|37552669|10000|10000|||100.72|100.63|0.00|1.57|5325484|37552669|10000|10000|||100.72|100.63|0.00|1.57|000087B6|0|1|00||01|BAFE|BAFE|6.100|BF4F|112ac5e7f65c2628
        // [118.34s] 264|4000414D|BAFE|000087B6|0||||1.554|4000414D|b5aca445628684ec
        // [124.41s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|37DA4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|271195|325133|6400|10000|||108.52|102.91|0.00|-0.63|4673318|37552669|10000|10000|||100.72|100.63|0.00|1.57|000087EC|0|1|00||01|C252|C252|0.100|BF4F|a8127d7e7bb1aea3
        world.Events.Add(124.41f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [124.41s] 264|4000414D|C252|000087EC|0||||1.554|100AF82E|3eac084b465d2097
        // [126.11s] 261|Change|4000414D|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|2.4502|IsCasting2|0|PosX|100.7247|PosY|100.6332|PosZ|0.0000|844f7d70835cd2ce
        // [126.11s] 261|Change|4000414D|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|2.4502|IsCasting2|0|PosX|100.7247|PosY|100.6332|PosZ|0.0000|2847d29547b100ef
        // [128.73s] 270|4000414D|2.4495|0000|005A|101.6708|99.4735|0.1983|f6f628d549687bf2
        world.Events.Add(128.73f, () => chaos_4000414D?.SetPosition(new Vector3(1.671f, 0.198f, -0.526f)));
        // [129.04s] 270|4000414D|2.3890|0000|005A|102.4948|98.5885|0.1983|3a624047424a60a6
        world.Events.Add(129.04f, () => chaos_4000414D?.SetPosition(new Vector3(2.495f, 0.198f, -1.412f)));
        // [129.36s] 270|4000414D|2.3890|0000|005A|102.8000|98.2833|0.1983|4ef1f47257783221
        world.Events.Add(129.36f, () => chaos_4000414D?.SetPosition(new Vector3(2.800f, 0.198f, -1.717f)));
        // [133.82s] 270|4000414D|2.2252|0000|005A|104.9057|96.6659|0.1983|79ab32f4a0a627bd
        world.Events.Add(133.82f, () => chaos_4000414D?.SetPosition(new Vector3(4.906f, 0.198f, -3.334f)));
        // [134.13s] 270|4000414D|2.1756|0000|005A|106.5537|95.3841|0.1983|613d331ff31576c9
        world.Events.Add(134.13f, () => chaos_4000414D?.SetPosition(new Vector3(6.554f, 0.198f, -4.616f)));
        // [141.29s] 270|4000414D|-1.5701|0000|005A|105.9739|95.4757|0.1983|5b67f36e4a85cfbc
        world.Events.Add(141.29f, () => chaos_4000414D?.SetPosition(new Vector3(5.974f, 0.198f, -4.524f)));
        // [141.47s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|F1730006|A7EB0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|6600|10000|||96.16|95.73|0.00|-2.20|3602850|37552669|10000|10000|||106.35|95.42|0.00|-1.46|00008868|0|1|00||01|C252|C252|0.100|4007|80b4f6dcef58197d
        world.Events.Add(141.47f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [141.47s] 264|4000414D|C252|00008868|0||||-1.570|100AF82E|f65fee66ec04df63
        // [141.60s] 270|4000414D|-1.5701|0000|005A|104.3564|95.4757|0.1983|1ae2d8553f9468db
        world.Events.Add(141.60f, () => chaos_4000414D?.SetPosition(new Vector3(4.356f, 0.198f, -4.524f)));
        // [141.91s] 270|4000414D|-1.6895|0000|005A|102.4643|95.4757|0.1983|c2737d9bdfed6cea
        world.Events.Add(141.91f, () => chaos_4000414D?.SetPosition(new Vector3(2.464f, 0.198f, -4.524f)));
        // [142.23s] 270|4000414D|-1.7592|0000|005A|100.8773|95.1705|0.1983|153b80520fc14a04
        world.Events.Add(142.23f, () => chaos_4000414D?.SetPosition(new Vector3(0.877f, 0.198f, -4.829f)));
        // [142.54s] 270|4000414D|-1.6787|0000|005A|98.8326|94.7737|0.1983|3250e7c53014cf20
        world.Events.Add(142.54f, () => chaos_4000414D?.SetPosition(new Vector3(-1.167f, 0.198f, -5.226f)));
        // [142.85s] 270|4000414D|-1.5523|0000|005A|97.7950|94.8043|0.1983|f1692d42334e11a5
        world.Events.Add(142.85f, () => chaos_4000414D?.SetPosition(new Vector3(-2.205f, 0.198f, -5.196f)));
        // [143.48s] 270|4000414D|-1.3011|0000|005A|97.2152|94.9568|0.1983|f0b57a1628c8afd9
        world.Events.Add(143.48f, () => chaos_4000414D?.SetPosition(new Vector3(-2.785f, 0.198f, -5.043f)));
        // [143.78s] 270|4000414D|-1.5715|0000|005A|96.8184|94.9568|0.1983|6c28fd888295d11a
        world.Events.Add(143.78f, () => chaos_4000414D?.SetPosition(new Vector3(-3.182f, 0.198f, -5.043f)));
        // [144.10s] 270|4000414D|-1.8171|0000|005A|96.2081|94.8653|0.1983|ed1ac8a9bc50359e
        world.Events.Add(144.10f, () => chaos_4000414D?.SetPosition(new Vector3(-3.792f, 0.198f, -5.135f)));
        // [144.41s] 270|4000414D|-2.1042|0000|005A|95.5367|94.6822|0.1983|41acdc0b7433f1f5
        world.Events.Add(144.41f, () => chaos_4000414D?.SetPosition(new Vector3(-4.463f, 0.198f, -5.318f)));
        // [144.50s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|3D4A0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|6800|10000|||87.57|91.84|0.00|-3.09|3353581|37552669|10000|10000|||96.34|94.88|0.00|-1.76|0000887C|0|1|00||01|C252|C252|0.100|2A44|00f2483da426b14f
        world.Events.Add(144.50f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [144.50s] 264|4000414D|C252|0000887C|0||||-2.104|100AF82E|930878e2c1001349
        // [147.09s] 20|4000414D|Chaos|BB02|Knock Down|4000414D|Chaos|4.700|95.54|94.68|0.00|-2.23|fbc56a7853783f24
        world.Events.Add(147.09f, () => chaos_4000414D?.Cast(ActionId.KnockDown_BB02, targetLocation: new Vector3(-4.463f, 0.198f, -5.318f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [146.83s] 261|Change|4000414D|CastBuffID|47874|CastDurationCurrent|0.0169|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|-2.2264|IsCasting1|1|IsCasting2|1|PosX|95.5367|PosY|94.6821|PosZ|0.0000|8e696a1b7cca9d8e
        // [152.04s] 21|4000414D|Chaos|BB02|Knock Down|4000414D|Chaos|1B|BB028000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|2748549|37552669|10000|10000|||95.54|94.68|0.00|-2.23|2748549|37552669|10000|10000|||95.54|94.68|0.00|-2.23|000088BF|0|1|00||01|BB02|BB02|3.100|2591|a8bcaa725cf41957
        // [152.04s] 264|4000414D|BB02|000088BF|0||||-2.220|4000414D|2d427c0ad1d2c67f
        // [151.80s] 261|Change|4000414D|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|1ec2251efc807a6b
        // [152.53s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730603|9A4C0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|7800|10000|||101.15|100.08|-0.01|0.46|2746844|37552669|10000|10000|||95.54|94.68|0.00|-2.23|000088C5|0|1|00||01|C252|C252|2.656|2591|6ae4218628de790b
        world.Events.Add(152.53f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [152.53s] 264|4000414D|C252|000088C5|0||||-2.220|100AF82E|300bcfc5a34773d1
        // [155.48s] 270|4000414D|1.7165|0000|005A|97.9171|94.3465|0.1983|c08715fc7694658d
        world.Events.Add(155.48f, () => chaos_4000414D?.SetPosition(new Vector3(-2.083f, 0.198f, -5.653f)));
        // [155.57s] 21|4000414D|Chaos|C252|unknown_c252|100AF82E|MainTank|730003|16C04001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|235634|325133|8000|10000|||107.68|92.91|0.00|2.45|2646973|37552669|10000|10000|||95.54|94.68|0.00|-2.65|000088E4|0|1|00||01|C252|C252|0.100|C5EF|cf43bc70ce64e9da
        world.Events.Add(155.57f, () => chaos_4000414D?.Cast(ActionId.KefkaAuto, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [155.57s] 264|4000414D|C252|000088E4|0||||1.717|100AF82E|caa72790dd15ee96
        // [155.79s] 270|4000414D|1.7165|0000|005A|99.2599|94.1329|0.1983|2e3ddee0ff296f96
        world.Events.Add(155.79f, () => chaos_4000414D?.SetPosition(new Vector3(-0.740f, 0.198f, -5.867f)));
        // [157.22s] 20|4000414D|Chaos|BB05|Big Bang|4000414D|Chaos|4.700|99.26|94.13|0.00|1.72|3a5cd6a4ab48a4df
        world.Events.Add(157.22f, () => chaos_4000414D?.Cast(ActionId.BigBang_BB05, targetLocation: new Vector3(-0.740f, 0.198f, -5.867f), castSeconds: 4.700f, targetId: chaos_4000414D?.GameObjectId));
        // [156.94s] 261|Change|4000414D|CastBuffID|47877|CastDurationCurrent|0.0169|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|1.7165|IsCasting1|1|IsCasting2|1|PosX|99.2599|PosY|94.1328|PosZ|0.0000|4a72d7d4ae7bff3b
        // [156.94s] 261|Change|4000414D|CastBuffID|47877|CastDurationCurrent|0.0169|CastDurationMax|4.7000|CastTargetID|4000414D|Heading|1.7165|IsCasting1|1|IsCasting2|1|PosX|99.2599|PosY|94.1328|PosZ|0.0000|90839ffb8a9b0887
    }

    private void Run_Exdeath_4000414C()
    {
        SimEnemy? exdeath_4000414C = null;
        // [-132.71s] 03|4000414C|Exdeath|00|64|0000|00||6052|19509|37552669|37552669|10000|10000|||108.00|100.00|0.00|0.00|b434973f7432f6bd
        world.Events.Add(0f, () => exdeath_4000414C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100, Targetable: true, EnemyList: EnemyListMode.Never, IsVisible: true, Placement: new Placement(new Vector3(8.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(5.84f, () => exdeath_4000414C?.SetPosition(new Vector3(-11.696f, 0.198f, 0.816f)));
        // [6.15s] 270|4000414C|1.4070|0000|005A|89.0974|100.9384|0.1983|0c09aeeeb59da2de
        world.Events.Add(6.15f, () => exdeath_4000414C?.SetPosition(new Vector3(-10.903f, 0.198f, 0.938f)));
        // [6.46s] 270|4000414C|1.5129|0000|005A|90.3791|101.1215|0.1983|1ecf5c31c77080a0
        world.Events.Add(6.46f, () => exdeath_4000414C?.SetPosition(new Vector3(-9.621f, 0.198f, 1.121f)));
        // [6.77s] 270|4000414C|1.5129|0000|005A|93.0647|101.2741|0.1983|b31d5f1663a3462c
        world.Events.Add(6.77f, () => exdeath_4000414C?.SetPosition(new Vector3(-6.935f, 0.198f, 1.274f)));
        // [7.08s] 270|4000414C|1.6227|0000|005A|93.8277|101.3046|0.1983|edba45f3cfcdb3d6
        world.Events.Add(7.08f, () => exdeath_4000414C?.SetPosition(new Vector3(-6.172f, 0.198f, 1.305f)));
        // [7.39s] 270|4000414C|1.6227|0000|005A|96.6353|101.1520|0.1983|6a99a3388b4aabc5
        world.Events.Add(7.39f, () => exdeath_4000414C?.SetPosition(new Vector3(-3.365f, 0.198f, 1.152f)));
        // [7.70s] 270|4000414C|1.6453|0000|005A|97.2762|101.1215|0.1983|9afc7df58ad08e31
        world.Events.Add(7.70f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.724f, 0.198f, 1.121f)));
        // [12.07s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710605|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|276321|325047|5800|10000|||103.56|100.66|0.00|-1.50|19044605|37552669|10000|10000|||97.28|101.12|0.00|1.65|00008430|0|1|00||01|C250|C250|0.100|C308|2900be234096958e
        world.Events.Add(12.07f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [12.07s] 264|4000414C|C250|00008430|0||||1.645|100AE96C|61a4cfd194920c0f
        // [14.56s] 26|7AD|Summon Order III|30.00|4000414C|Exdeath|40004162|Automaton Queen|01|206573|37552669|10edc863185e73d3
        // [15.09s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|92830000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|6400|10000|||102.43|100.39|0.00|-1.43|18620087|37552669|10000|10000|||97.28|101.12|0.00|1.71|0000844A|0|1|00||01|C250|C250|0.100|C5C7|604e9d4013b8119a
        world.Events.Add(15.09f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [15.09s] 264|4000414C|C250|0000844A|0||||1.713|100AE96C|75f70f1222efb392
        // [16.79s] 30|7AD|Summon Order III|0.00|4000414C|Exdeath|40004162|Automaton Queen|01|206573|37552669|7de86109a3b0d536
        // [17.50s] 26|7AE|Summon Order IV|30.00|4000414C|Exdeath|40004162|Automaton Queen|01|206573|37552669|a29ddf0404da0b65
        // [18.13s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710603|D5E90000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|7000|10000|||100.81|99.94|0.00|-1.65|18114060|37552669|10000|10000|||97.28|101.12|0.00|1.78|00008465|0|1|00||01|C250|C250|0.100|DC48|028f620f008501fa
        world.Events.Add(18.13f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [18.13s] 264|4000414C|C250|00008465|0||||2.265|100AE96C|3cda78d47a61598b
        // [19.51s] 30|7AE|Summon Order IV|0.00|4000414C|Exdeath|40004162|Automaton Queen|01|206573|37552669|9af4d5eef683dd99
        // [20.22s] 270|4000414C|-2.5283|0000|005A|97.0626|100.7858|0.1983|f0fb587a38c908e2
        world.Events.Add(20.22f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.937f, 0.198f, 0.786f)));
        // [20.53s] 270|4000414C|-2.5283|0000|005A|95.4757|98.5580|0.1983|ab4f7047d2c1def3
        world.Events.Add(20.53f, () => exdeath_4000414C?.SetPosition(new Vector3(-4.524f, 0.198f, -1.442f)));
        // [20.85s] 270|4000414C|-2.5242|0000|005A|94.3465|96.9710|0.1983|90349656ed73fb4e
        world.Events.Add(20.85f, () => exdeath_4000414C?.SetPosition(new Vector3(-5.653f, 0.198f, -3.029f)));
        // [21.16s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|86F80000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|7600|10000|||90.66|92.15|0.00|-2.25|17742806|37552669|10000|10000|||94.98|97.86|0.00|-2.53|0000847C|0|1|00||01|C250|C250|0.100|19C1|10d4038e6d8eae64
        world.Events.Add(21.16f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [21.16s] 264|4000414C|C250|0000847C|0||||-2.509|100AE96C|71d8f5ed28fbc7d5
        // [22.18s] 20|4000414C|Exdeath|BAFB|Black Hole|4000414C|Exdeath|2.700|94.35|96.97|0.00|-2.57|0979f7a87f8b6587
        world.Events.Add(22.18f, () => exdeath_4000414C?.Cast(ActionId.BlackHole, targetLocation: new Vector3(-5.654f, 0.198f, -3.029f), castSeconds: 2.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [21.90s] 261|Change|4000414C|CastBuffID|47867|CastDurationCurrent|0.0173|CastDurationMax|2.7000|CastTargetID|4000414C|Heading|-2.5776|IsCasting1|1|IsCasting2|1|PosX|94.3464|PosY|96.9711|PosZ|0.0000|d4636efba8e61d38
        // [25.17s] 21|4000414C|Exdeath|BAFB|Black Hole|4000414C|Exdeath|1B|BAFB8000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|17493723|37552669|10000|10000|||94.35|96.97|0.00|-2.68|17493723|37552669|10000|10000|||94.35|96.97|0.00|-2.68|000084A0|0|1|00||01|BAFB|BAFB|3.100|12AE|1cd24dd3e602fe30
        // [25.17s] 264|4000414C|BAFB|000084A0|0||||-2.683|4000414C|de4bc56931c2756c
        // [24.80s] 261|Change|4000414C|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|-2.6764|IsCasting1|0|IsCasting2|0|8683600a24d6b65e
        // [27.22s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|F8340000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|283066|325047|9000|10000|||99.44|99.78|0.00|1.10|17370795|37552669|10000|10000|||94.35|96.97|0.00|-2.68|000084AF|0|1|00||01|C250|C250|1.094|12AE|d1d6ca485479b699
        world.Events.Add(27.22f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [27.22s] 264|4000414C|C250|000084AF|0||||-2.683|100AE96C|337ce53317eabcc3
        // [30.25s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|2EA24001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|302053|325047|10000|10000|||99.44|99.78|0.00|-2.08|17191446|37552669|10000|10000|||94.35|96.97|0.00|1.07|000084CD|0|1|00||01|C250|C250|0.100|AB6A|dd7bfce836b9ca3b
        world.Events.Add(30.25f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [30.25s] 264|4000414C|C250|000084CD|0||||1.066|100AE96C|38212bee84662643
        // [32.40s] 21|4000414C|Exdeath|C2E5|Aetherlink|4000414C|Exdeath|1B|C2E58000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|16971355|37552669|10000|10000|||94.35|96.97|0.00|1.07|16971355|37552669|10000|10000|||94.35|96.97|0.00|1.07|000084DE|0|1|00||01|C2E5|C2E5|3.100|AB6A|a8f7c0c0af429848
        world.Events.Add(32.40f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_C2E5, castSeconds: 0f, targetId: exdeath_4000414C?.GameObjectId));
        // [32.40s] 264|4000414C|C2E5|000084DE|0||||1.066|4000414C|bd7715c925f02d68
        // [33.29s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710003|5ED34001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|304484|325047|10000|10000|||99.44|99.78|0.00|-2.08|16172433|37552669|10000|10000|||94.35|96.97|0.00|1.07|000084E5|0|1|00||01|C250|C250|2.206|AB6A|095d879061f5fc5b
        world.Events.Add(33.29f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [33.29s] 264|4000414C|C250|000084E5|0||||1.066|100AE96C|21bf1415bc10910b
        // [36.34s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710003|60E04001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|286997|325047|10000|10000|||97.17|99.26|0.00|-1.81|16019047|37552669|10000|10000|||94.35|96.97|0.00|0.97|000084F8|0|1|00||01|C250|C250|0.100|9C01|a42ea1f28f208e9c
        world.Events.Add(36.34f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [36.34s] 264|4000414C|C250|000084F8|0||||0.687|100AE96C|fe194a8dc8584d36
        // [37.59s] 20|4000414C|Exdeath|BB09|Thunder III|4000414C|Exdeath|4.700|94.35|96.97|0.00|-0.07|be5b110f762ac313
        world.Events.Add(37.59f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_BB09, targetLocation: new Vector3(-5.654f, 0.198f, -3.029f), castSeconds: 4.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [37.16s] 261|Change|4000414C|CastBuffID|47881|CastDurationCurrent|0.0166|CastDurationMax|4.7000|CastTargetID|4000414C|Heading|-0.0679|IsCasting1|1|IsCasting2|1|c681e79f7ef94e5e
        // [42.54s] 21|4000414C|Exdeath|BB09|Thunder III|4000414C|Exdeath|1B|BB098000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|15471073|37552669|10000|10000|||94.35|96.97|0.00|-0.07|15471073|37552669|10000|10000|||94.35|96.97|0.00|-0.07|0000852B|0|1|00||01|BB09|BB09|3.100|7D3B|b032bf2e260d6468
        // [42.54s] 264|4000414C|BB09|0000852B|0||||-0.068|4000414C|7039893764e6ba5a
        // [42.17s] 261|Change|4000414C|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|d656687439502553
        // [44.37s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710105|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|9400|10000|||92.64|95.84|0.00|-2.02|15280280|37552669|10000|10000|||94.35|96.97|0.00|-0.07|00008539|0|1|00||01|C250|C250|1.314|7D3B|c99c56a2343c1229
        world.Events.Add(44.37f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [44.37s] 264|4000414C|C250|00008539|0||||-0.068|100AE96C|4ad2cd112c9495ef
        // [47.41s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|F1710106|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||94.07|97.97|0.00|0.54|15071045|37552669|10000|10000|||94.35|96.97|0.00|-1.67|00008550|0|1|00||01|C250|C250|0.100|7A83|8c3c51117a1a7ba9
        world.Events.Add(47.41f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [47.41s] 264|4000414C|C250|00008550|0||||-0.135|100AE96C|bc4f704d34bb2e94
        // [50.45s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|F1710006|B00E0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||92.27|96.98|-0.01|2.89|14756887|37552669|10000|10000|||94.35|96.97|0.00|-1.53|00008566|0|1|00||01|C250|C250|0.100|416E|2f3e5b370eb470b3
        world.Events.Add(50.45f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [50.45s] 264|4000414C|C250|00008566|0||||-1.536|100AE96C|1eadf6075bd286fb
        // [52.10s] 270|4000414C|2.5503|0000|005A|95.2315|95.6588|0.1983|da6ea03b42f9efb6
        world.Events.Add(52.10f, () => exdeath_4000414C?.SetPosition(new Vector3(-4.769f, 0.198f, -4.341f)));
        // [52.41s] 270|4000414C|2.6130|0000|005A|95.8724|94.5906|0.1983|1c415eaf69cfb153
        world.Events.Add(52.41f, () => exdeath_4000414C?.SetPosition(new Vector3(-4.128f, 0.198f, -5.409f)));
        // [52.73s] 270|4000414C|2.5801|0000|005A|96.0555|94.2244|0.1983|3822aeab5d1a7eac
        world.Events.Add(52.73f, () => exdeath_4000414C?.SetPosition(new Vector3(-3.945f, 0.198f, -5.776f)));
        // [53.48s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|F1710006|D0A00000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|9200|10000|||99.64|88.74|-0.02|0.37|14479444|37552669|10000|10000|||96.05|94.22|0.00|2.57|0000858A|0|1|00||01|C250|C250|0.100|E6B1|6239bd83dffd31c6
        world.Events.Add(53.48f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [53.48s] 264|4000414C|C250|0000858A|0||||2.521|100AE96C|b61eb9ea73b72115
        // [56.51s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710003|F1C4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|248722|325047|9400|10000|||98.73|92.63|0.00|-1.08|14200161|37552669|10000|10000|||96.06|94.22|0.00|2.21|000085AF|0|1|00||01|C250|C250|0.100|C4E7|7e2dcbd42e7a1b5f
        world.Events.Add(56.51f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [56.51s] 264|4000414C|C250|000085AF|0||||1.691|100AE96C|ed8e39ac0210d79b
        // [59.54s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|F7F30000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|254492|325047|10000|10000|||99.43|99.77|0.00|1.07|13605993|37552669|10000|10000|||96.06|94.22|0.00|0.54|000085CD|0|1|00||01|C250|C250|0.100|97AD|e17723277764fcba
        world.Events.Add(59.54f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [59.54s] 264|4000414C|C250|000085CD|0||||0.581|100AE96C|7ca8220bec171c73
        // [59.80s] 270|4000414C|0.5878|0000|005A|96.3912|94.7127|0.1983|dc54f68969ad1fc4
        world.Events.Add(59.80f, () => exdeath_4000414C?.SetPosition(new Vector3(-3.609f, 0.198f, -5.287f)));
        // [62.08s] 270|4000414C|0.6051|0000|005A|97.0626|95.7198|0.1983|b9335317e230e4ef
        world.Events.Add(62.08f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.937f, 0.198f, -4.280f)));
        // [62.39s] 270|4000414C|0.6193|0000|005A|97.9476|96.9405|0.1983|99e4cbb06b86a01c
        world.Events.Add(62.39f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.052f, 0.198f, -3.059f)));
        // [62.70s] 270|4000414C|0.6193|0000|005A|99.3820|98.9852|0.1983|9b30004da008856a
        world.Events.Add(62.70f, () => exdeath_4000414C?.SetPosition(new Vector3(-0.618f, 0.198f, -1.015f)));
        // [63.01s] 270|4000414C|0.7572|0000|005A|99.9008|99.6871|0.1983|955fa1f548c0b7ef
        world.Events.Add(63.01f, () => exdeath_4000414C?.SetPosition(new Vector3(-0.099f, 0.198f, -0.313f)));
        // [70.32s] 26|7AD|Summon Order III|30.00|4000414C|Exdeath|40004182|Automaton Queen|01|206573|37552669|3ba4004b9e2e3aff
        // [72.60s] 30|7AD|Summon Order III|0.00|4000414C|Exdeath|40004182|Automaton Queen|01|206573|37552669|97f87a15b2ae7260
        // [73.31s] 26|7AE|Summon Order IV|30.00|4000414C|Exdeath|40004182|Automaton Queen|01|206573|37552669|b17f0c24c9c26cac
        // [75.32s] 30|7AE|Summon Order IV|0.00|4000414C|Exdeath|40004182|Automaton Queen|01|206573|37552669|1c244302a74d159a
        // [75.68s] 270|4000414C|2.0341|0000|005A|101.5182|98.8937|0.1983|5c0bc9955bf7caf9
        world.Events.Add(75.68f, () => exdeath_4000414C?.SetPosition(new Vector3(1.518f, 0.198f, -1.106f)));
        // [75.99s] 270|4000414C|2.0028|0000|005A|102.9831|98.1918|0.1983|b7f2ff6b704ccdbd
        world.Events.Add(75.99f, () => exdeath_4000414C?.SetPosition(new Vector3(2.983f, 0.198f, -1.808f)));
        // [76.31s] 270|4000414C|2.0028|0000|005A|105.1804|97.1847|0.1983|2d921c8d60c8baea
        world.Events.Add(76.31f, () => exdeath_4000414C?.SetPosition(new Vector3(5.180f, 0.198f, -2.815f)));
        // [76.62s] 270|4000414C|1.9350|0000|005A|106.2790|96.7574|0.1983|874ed87ad47d8cc2
        world.Events.Add(76.62f, () => exdeath_4000414C?.SetPosition(new Vector3(6.279f, 0.198f, -3.243f)));
        // [76.93s] 270|4000414C|1.9078|0000|005A|108.1712|96.0555|0.1983|b5e9385e0eb5b817
        world.Events.Add(76.93f, () => exdeath_4000414C?.SetPosition(new Vector3(8.171f, 0.198f, -3.945f)));
        // [77.24s] 270|4000414C|1.8993|0000|005A|109.6971|95.5367|0.1983|e2620137a6a909f7
        world.Events.Add(77.24f, () => exdeath_4000414C?.SetPosition(new Vector3(9.697f, 0.198f, -4.463f)));
        // [77.78s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710603|DAB00000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|293648|325047|7600|10000|||115.68|93.49|0.00|-1.24|9813596|37552669|10000|10000|||109.67|95.55|0.00|1.90|00008678|0|1|00||01|C250|C250|0.100|CD62|903cee71d167c4ca
        world.Events.Add(77.78f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [77.78s] 264|4000414C|C250|00008678|0||||1.899|100AE96C|abfa5fb69b12ba4f
        // [78.31s] 270|4000414C|1.8792|0000|005A|110.3990|95.3231|0.1983|beda3c2f6ba186ff
        world.Events.Add(78.31f, () => exdeath_4000414C?.SetPosition(new Vector3(10.399f, 0.198f, -4.677f)));
        // [78.85s] 20|4000414C|Exdeath|BB09|Thunder III|4000414C|Exdeath|4.700|110.35|95.34|0.00|1.91|8fca185e057abcc4
        world.Events.Add(78.85f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_BB09, targetLocation: new Vector3(10.399f, 0.198f, -4.677f), castSeconds: 4.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [78.60s] 261|Change|4000414C|CastBuffID|47881|CastDurationCurrent|0.0164|CastDurationMax|4.7000|CastTargetID|4000414C|Heading|1.9142|IsCasting1|1|IsCasting2|1|PosX|110.3630|PosY|95.3339|PosZ|0.0000|fefd5b916b0baeee
        // [83.41s] 261|Change|4000414C|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|1.9316|IsCasting1|0|IsCasting2|0|PosX|110.3989|PosY|95.3230|PosZ|0.0000|caebe0dc3ce9225a
        // [83.85s] 21|4000414C|Exdeath|BB09|Thunder III|4000414C|Exdeath|1B|BB098000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|9502959|37552669|10000|10000|||110.40|95.32|0.00|1.93|9502959|37552669|10000|10000|||110.40|95.32|0.00|1.93|000086B1|0|1|00||01|BB09|BB09|3.100|CEB2|29d13525b8779191
        // [83.85s] 264|4000414C|BB09|000086B1|0||||1.932|4000414C|1861bdd024a103ec
        // [85.85s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|84D20000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|200296|325047|9000|10000|||107.06|100.26|0.00|-1.25|9265223|37552669|10000|10000|||110.40|95.32|0.00|1.93|000086C6|0|1|00||01|C250|C250|1.139|CEB2|612067e2a5c5de54
        world.Events.Add(85.85f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [85.85s] 264|4000414C|C250|000086C6|0||||1.932|100AE96C|c857e7394d30db00
        // [87.28s] 270|4000414C|-1.1069|0000|005A|108.2627|96.3912|0.1983|099c563ee57f9699
        world.Events.Add(87.28f, () => exdeath_4000414C?.SetPosition(new Vector3(8.263f, 0.198f, -3.609f)));
        // [87.59s] 270|4000414C|-1.1069|0000|005A|105.7602|97.6424|0.1983|9229bce93b04b251
        world.Events.Add(87.59f, () => exdeath_4000414C?.SetPosition(new Vector3(5.760f, 0.198f, -2.358f)));
        // [87.90s] 270|4000414C|-1.1069|0000|005A|105.0278|98.0086|0.1983|8ba6d10cc040a2e1
        world.Events.Add(87.90f, () => exdeath_4000414C?.SetPosition(new Vector3(5.028f, 0.198f, -1.991f)));
        // [88.89s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|81C80000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|236924|325047|8200|10000|||99.41|100.82|0.00|2.04|8950988|37552669|10000|10000|||105.03|98.01|0.00|-1.11|000086E0|0|1|00||01|C250|C250|0.100|52E6|966c47bfa5a022c6
        world.Events.Add(88.89f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [88.89s] 264|4000414C|C250|000086E0|0||||-1.107|100AE96C|3d7a16d7fe091153
        // [91.33s] 270|4000414C|-1.1701|0000|005A|104.6616|98.1612|0.1983|eec235979989b9a6
        world.Events.Add(91.33f, () => exdeath_4000414C?.SetPosition(new Vector3(4.662f, 0.198f, -1.839f)));
        // [91.64s] 270|4000414C|-1.2785|0000|005A|103.7155|98.5580|0.1983|770927ed1275a417
        world.Events.Add(91.64f, () => exdeath_4000414C?.SetPosition(new Vector3(3.716f, 0.198f, -1.442f)));
        // [91.91s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710005|A7F30000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|9400|10000|||95.24|100.82|0.00|-1.64|8577464|37552669|10000|10000|||104.25|98.33|0.00|-1.21|000086F7|0|1|00||01|C250|C250|0.100|4BE8|558e34561bad5810
        world.Events.Add(91.91f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [91.91s] 264|4000414C|C250|000086F7|0||||-1.279|100AE96C|d4d6bddff83d60d8
        // [91.96s] 270|4000414C|-1.4296|0000|005A|101.9455|99.0768|0.1983|b37af5af85739d57
        world.Events.Add(91.96f, () => exdeath_4000414C?.SetPosition(new Vector3(1.945f, 0.198f, -0.923f)));
        // [92.27s] 270|4000414C|-1.4296|0000|005A|99.5956|99.4125|0.1983|a038862b137937ae
        world.Events.Add(92.27f, () => exdeath_4000414C?.SetPosition(new Vector3(-0.404f, 0.198f, -0.588f)));
        // [92.58s] 270|4000414C|-1.5845|0000|005A|98.5275|99.5651|0.1983|75ec6b7bfae25f5b
        world.Events.Add(92.58f, () => exdeath_4000414C?.SetPosition(new Vector3(-1.472f, 0.198f, -0.435f)));
        // [93.11s] 270|4000414C|-1.7657|0000|005A|97.3678|99.3514|0.1983|dde6da612a210273
        world.Events.Add(93.11f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.632f, 0.198f, -0.649f)));
        // [93.42s] 270|4000414C|-1.9153|0000|005A|96.6353|99.0768|0.1983|897a4ca35c11fb27
        world.Events.Add(93.42f, () => exdeath_4000414C?.SetPosition(new Vector3(-3.365f, 0.198f, -0.923f)));
        // [93.73s] 270|4000414C|-2.1569|0000|005A|95.2620|98.3749|0.1983|b391a857d38c43df
        world.Events.Add(93.73f, () => exdeath_4000414C?.SetPosition(new Vector3(-4.738f, 0.198f, -1.625f)));
        // [94.04s] 270|4000414C|-2.3987|0000|005A|94.9874|98.1918|0.1983|dbd9cafd8a2c99e4
        world.Events.Add(94.04f, () => exdeath_4000414C?.SetPosition(new Vector3(-5.013f, 0.198f, -1.808f)));
        // [94.36s] 270|4000414C|-2.7140|0000|005A|93.7972|96.9100|0.1983|33b7f8c5f7eee3be
        world.Events.Add(94.36f, () => exdeath_4000414C?.SetPosition(new Vector3(-6.203f, 0.198f, -3.090f)));
        // [99.18s] 21|4000414C|Exdeath|C2E5|Aetherlink|4000414C|Exdeath|1B|C2E58000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|7970932|37552669|10000|10000|||93.80|96.91|0.00|-2.91|7970932|37552669|10000|10000|||93.80|96.91|0.00|-2.91|00008728|0|1|00||01|C2E5|C2E5|3.100|0944|aeed988680ba3071
        world.Events.Add(99.18f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_C2E5, castSeconds: 0f, targetId: exdeath_4000414C?.GameObjectId));
        // [99.18s] 264|4000414C|C2E5|00008728|0||||-2.914|4000414C|bb477163a7d7fff4
        // [113.38s] 20|4000414C|Exdeath|BD66|White Hole|4000414C|Exdeath|4.700|93.80|96.91|0.00|1.16|f711c2852c6f99c2
        world.Events.Add(113.38f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, targetLocation: new Vector3(-6.203f, 0.198f, -3.090f), castSeconds: 4.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [113.08s] 261|Change|4000414C|CastBuffID|48486|CastDurationCurrent|0.0145|CastDurationMax|4.7000|CastTargetID|4000414C|Heading|1.1642|IsCasting1|1|IsCasting2|1|PosX|93.7971|PosY|96.9100|PosZ|0.0000|1e4c1ffc89d08d7c
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|100702A3|Player|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|205207|205207|5500|10000|||92.98|98.35|0.00|2.20|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|0|8|00||01|BD66|BD66|3.100|AF6E|27c5b911152c305b
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|100AE96C|RegenHealer|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|9800|10000|||97.72|99.66|0.00|-2.19|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|1|8|00||01|BD66|BD66|3.100|AF6E|32a4c1f31225303c
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|10018AEA|MeleeDpsB|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||99.45|99.32|0.00|1.70|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|2|8|00||01|BD66|BD66|3.100|AF6E|f14ea75b7c34d9aa
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [117.96s] 261|Change|4000414C|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|8a8fca3a327c5b08
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|100A7A8F|OffTank|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||106.12|100.05|0.00|1.19|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|3|8|00||01|BD66|BD66|3.100|AF6E|a828e728c6ece383
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|100AF82E|MainTank|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|3800|10000|||106.25|100.48|0.00|-1.55|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|4|8|00||01|BD66|BD66|3.100|AF6E|7b1ad4c9b90e1eb4
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|10066D86|ShieldHealer|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|8730|10000|||106.68|99.82|0.00|0.84|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|5|8|00||01|BD66|BD66|3.100|AF6E|c74e458c4f324b2f
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|1009061B|MeleeDpsA|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|6425|10000|||107.77|98.71|0.00|-1.31|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|6|8|00||01|BD66|BD66|3.100|AF6E|4742d44db2f0a286
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [118.34s] 22|4000414C|Exdeath|BD66|White Hole|100AC8F1|CasterDps|3|0|1B|BD668000|0|0|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||107.37|99.09|0.00|-0.04|6083059|37552669|10000|10000|||93.80|96.91|0.00|1.16|000087B5|7|8|00||01|BD66|BD66|3.100|AF6E|45a3a9e6e6ca6323
        world.Events.Add(118.34f, () => exdeath_4000414C?.Cast(ActionId.WhiteHole, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [118.34s] 264|4000414C|BD66|000087B5|1|-0.015|-0.015|-0.015|1.164|4000414C|7492994dcc92d505
        // [121.64s] 270|4000414C|1.7779|0000|005A|94.9874|96.6659|0.1983|f07ae0217a15f549
        world.Events.Add(121.64f, () => exdeath_4000414C?.SetPosition(new Vector3(-5.013f, 0.198f, -3.334f)));
        // [121.96s] 270|4000414C|1.7779|0000|005A|97.7340|96.0860|0.1983|064f033570d0f9f2
        world.Events.Add(121.96f, () => exdeath_4000414C?.SetPosition(new Vector3(-2.266f, 0.198f, -3.914f)));
        // [122.27s] 270|4000414C|1.7779|0000|005A|100.4806|95.5062|0.1983|8b23f86f090ff0fc
        world.Events.Add(122.27f, () => exdeath_4000414C?.SetPosition(new Vector3(0.481f, 0.198f, -4.494f)));
        // [122.45s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|EC710605|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|9200|10000|||106.38|98.56|0.00|0.28|5652223|37552669|10000|10000|||97.76|96.08|0.00|1.76|000087D8|0|1|00||01|C250|C250|0.100|C86F|37e0a32184403beb
        world.Events.Add(122.45f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [122.45s] 264|4000414C|C250|000087D8|0||||1.778|100AE96C|1769c9a2166dc196
        // [122.58s] 270|4000414C|1.2124|0000|005A|102.4643|95.0789|0.1983|c159f6b8ef5c0c30
        world.Events.Add(122.58f, () => exdeath_4000414C?.SetPosition(new Vector3(2.464f, 0.198f, -4.921f)));
        // [122.89s] 270|4000414C|0.5552|0000|005A|103.5629|96.3607|0.1983|049fb5a14862ef81
        world.Events.Add(122.89f, () => exdeath_4000414C?.SetPosition(new Vector3(3.563f, 0.198f, -3.639f)));
        // [123.20s] 270|4000414C|0.4047|0000|005A|104.2038|97.8561|0.1983|04e09c7f45b2589d
        world.Events.Add(123.20f, () => exdeath_4000414C?.SetPosition(new Vector3(4.204f, 0.198f, -2.144f)));
        // [123.52s] 270|4000414C|0.2687|0000|005A|104.8752|99.4125|0.1983|58db0a6345db4e0e
        world.Events.Add(123.52f, () => exdeath_4000414C?.SetPosition(new Vector3(4.875f, 0.198f, -0.588f)));
        // [125.48s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710003|51DA4001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|254932|325047|8400|10000|||104.42|104.58|0.00|-2.03|5404685|37552669|10000|10000|||104.88|99.41|0.00|0.17|000087F2|0|1|00||01|C250|C250|0.100|7627|9744c57e038e8d85
        world.Events.Add(125.48f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [125.48s] 264|4000414C|C250|000087F2|0||||-0.242|100AE96C|f6e273badb722f89
        // [135.70s] 270|4000414C|2.4448|0000|005A|105.3025|98.9242|0.1983|9563cc5e9f3643b1
        world.Events.Add(135.70f, () => exdeath_4000414C?.SetPosition(new Vector3(5.302f, 0.198f, -1.076f)));
        // [136.01s] 270|4000414C|2.3139|0000|005A|105.8823|98.3749|0.1983|a71357d957e1d815
        world.Events.Add(136.01f, () => exdeath_4000414C?.SetPosition(new Vector3(5.882f, 0.198f, -1.625f)));
        // [136.33s] 270|4000414C|2.3139|0000|005A|107.9575|96.4827|0.1983|727caa22cf7a81c0
        world.Events.Add(136.33f, () => exdeath_4000414C?.SetPosition(new Vector3(7.957f, 0.198f, -3.517f)));
        // [136.64s] 270|4000414C|2.1309|0000|005A|108.3848|96.0860|0.1983|1901a81d1681f025
        world.Events.Add(136.64f, () => exdeath_4000414C?.SetPosition(new Vector3(8.385f, 0.198f, -3.914f)));
        // [141.11s] 270|4000414C|-1.8069|0000|005A|107.6218|95.9029|0.1983|cbf17a73ac1a3a20
        world.Events.Add(141.11f, () => exdeath_4000414C?.SetPosition(new Vector3(7.622f, 0.198f, -4.097f)));
        // [141.42s] 270|4000414C|-1.8069|0000|005A|104.8752|95.2315|0.1983|819f6bbfc55813f9
        world.Events.Add(141.42f, () => exdeath_4000414C?.SetPosition(new Vector3(4.875f, 0.198f, -4.769f)));
        // [141.51s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710003|39114001|0|0|0|0|0|0|0|0|0|0|0|0|0|0|296773|325047|8700|10000|||96.51|94.65|0.00|-1.38|3815955|37552669|10000|10000|||107.09|95.77|0.00|-1.83|00008869|0|1|00||01|C250|C250|0.100|3661|6123a3e952b52b78
        world.Events.Add(141.51f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [141.51s] 264|4000414C|C250|00008869|0||||-1.807|100AE96C|7d15da6050ef1f71
        // [141.74s] 270|4000414C|-1.6528|0000|005A|102.8915|94.9568|0.1983|32778279df1671e3
        world.Events.Add(141.74f, () => exdeath_4000414C?.SetPosition(new Vector3(2.891f, 0.198f, -5.043f)));
        // [142.05s] 270|4000414C|-1.7483|0000|005A|101.4877|94.8348|0.1983|0c92b30dc4ee9bb8
        world.Events.Add(142.05f, () => exdeath_4000414C?.SetPosition(new Vector3(1.488f, 0.198f, -5.165f)));
        // [143.61s] 20|4000414C|Exdeath|BB0F|Blizzard III|4000414C|Exdeath|2.700|101.49|94.83|0.00|-1.81|0a084f67e01fcb76
        world.Events.Add(143.61f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB0F, targetLocation: new Vector3(1.488f, 0.198f, -5.165f), castSeconds: 2.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [143.31s] 261|Change|4000414C|CastBuffID|47887|CastDurationMax|2.7000|CastTargetID|4000414C|Heading|-1.8205|IsCasting1|1|IsCasting2|1|PosX|101.4877|PosY|94.8347|PosZ|0.0000|65143eb141ac7d13
        // [146.60s] 21|4000414C|Exdeath|BB0F|Blizzard III|4000414C|Exdeath|1B|BB0F8000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|3356842|37552669|10000|10000|||101.49|94.83|0.00|-1.84|3356842|37552669|10000|10000|||101.49|94.83|0.00|-1.84|0000888B|0|1|00||01|BB0F|BB0F|3.100|3500|973883f02f7dc024
        // [146.60s] 264|4000414C|BB0F|0000888B|0||||-1.841|4000414C|e2fe70d52c06033c
        // [146.30s] 261|Change|4000414C|CastBuffID|0|CastDurationMax|0.0000|CastTargetID|E0000000|Heading|-1.8408|IsCasting1|0|IsCasting2|0|a67a3a5009810265
        // [149.90s] 270|4000414C|-2.7486|0000|005A|101.0299|93.7361|0.1983|55394baf23a38419
        world.Events.Add(149.90f, () => exdeath_4000414C?.SetPosition(new Vector3(1.030f, 0.198f, -6.264f)));
        // [150.22s] 270|4000414C|-2.4786|0000|005A|100.6332|92.8206|0.1983|05f0fcfc7c33ecb8
        world.Events.Add(150.22f, () => exdeath_4000414C?.SetPosition(new Vector3(0.633f, 0.198f, -7.179f)));
        // [150.30s] 26|7AD|Summon Order III|30.00|4000414C|Exdeath|400041A0|Automaton Queen|01|206573|37552669|1b6472f51a1a3a9f
        // [150.66s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|F1710606|42AE0000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|8300|10000|||98.51|91.85|0.00|0.24|2882926|37552669|10000|10000|||100.72|93.02|0.00|-2.51|000088B6|0|1|00||01|C250|C250|0.100|3781|b7c347cfb872097c
        world.Events.Add(150.66f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [150.66s] 264|4000414C|C250|000088B6|0||||-1.779|100AE96C|857489de632848a3
        // [152.71s] 30|7AD|Summon Order III|0.00|4000414C|Exdeath|400041A0|Automaton Queen|01|206573|37552669|b524747fb0281ed4
        // [153.43s] 26|7AE|Summon Order IV|30.00|4000414C|Exdeath|400041A0|Automaton Queen|01|206573|37552669|00e6c7564a9ebde5
        // [153.69s] 21|4000414C|Exdeath|C250|unknown_c250|100AE96C|RegenHealer|710603|B7E80000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|271897|325047|8900|10000|||99.41|97.45|0.00|-2.97|2580195|37552669|10000|10000|||100.63|92.82|0.00|-0.21|000088D7|0|1|00||01|C250|C250|0.100|7861|e0678906c0b2e0de
        world.Events.Add(153.69f, () => exdeath_4000414C?.Cast(ActionId.UnknownC250, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [153.69s] 264|4000414C|C250|000088D7|0||||-0.187|100AE96C|b4c058cf17e1eec7
        // [155.44s] 30|7AE|Summon Order IV|0.00|4000414C|Exdeath|400041A0|Automaton Queen|01|206573|37552669|767e5238a61847cc
        // [156.95s] 20|4000414C|Exdeath|BB11|Blizzard III|4000414C|Exdeath|3.700|100.63|92.82|0.00|1.91|82404e7a012feb9e
        world.Events.Add(156.95f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, targetLocation: new Vector3(0.633f, 0.198f, -7.179f), castSeconds: 3.700f, targetId: exdeath_4000414C?.GameObjectId));
        // [156.95s] 35|4000414C|Exdeath|4000414B|Graven Image|0000|0000|0040|4000414B|000F|0000|13872b450f555f83
        // [156.76s] 261|Change|4000414C|CastBuffID|47889|CastDurationCurrent|0.0182|CastDurationMax|3.7000|CastTargetID|4000414C|Heading|2.2094|IsCasting1|1|IsCasting2|1|PosX|100.6332|PosY|92.8206|PosZ|0.0000|a4325ed0a4e27381
        // [157.59s] 261|Change|4000414C|CastDurationCurrent|0.9492|Heading|-3.1252|PosX|100.6332|PosY|92.8206|PosZ|0.0000|0d2693937f4d4ee9
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|100AE96C|RegenHealer|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|247590|325047|10000|10000|||99.12|87.45|0.00|-2.57|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|0|8|00||01|BB11|BB11|4.100|00AB|a6379ede8de03938
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|10018AEA|MeleeDpsB|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|112097|226668|10000|10000|||100.72|99.81|0.00|-2.89|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|1|8|00||01|BB11|BB11|4.100|00AB|d7527273948d5f4b
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|100AF82E|MainTank|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|126701|325133|9000|10000|||94.52|88.10|0.00|-0.95|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|2|8|00||01|BB11|BB11|4.100|00AB|89c68e13bdb5938e
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|100AC8F1|CasterDps|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|120190|227550|10000|10000|||91.87|94.08|0.00|2.47|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|3|8|00||01|BB11|BB11|4.100|00AB|97783e301048d740
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|10066D86|ShieldHealer|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|169275|205177|8805|10000|||88.23|96.48|1.51|2.04|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|4|8|00||01|BB11|BB11|4.100|00AB|172a7d3b9ee0f78e
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|100A7A8F|OffTank|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|67939|217488|10000|10000|||101.61|112.14|0.00|-0.97|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|5|8|00||01|BB11|BB11|4.100|00AB|afdea726aa00df82
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|100702A3|Player|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|89572|205207|5200|10000|||106.61|110.89|0.00|-0.44|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|6|8|00||01|BB11|BB11|4.100|00AB|ae71bae8b94327db
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [160.92s] 22|4000414C|Exdeath|BB11|Blizzard III|1009061B|MeleeDpsA|1B|BB118000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|112838|205177|5165|10000|||108.44|111.33|0.00|2.52|1551391|37552669|10000|10000|||100.63|92.82|0.00|-3.13|00008910|7|8|00||01|BB11|BB11|4.100|00AB|cc177d7f2c145ba9
        world.Events.Add(160.92f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_BB11, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [160.92s] 264|4000414C|BB11|00008910|1|-0.015|-0.015|-0.015|-3.125|4000414C|4f25b0b04ce2d7e9
        // [160.71s] 261|Change|4000414C|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|08737be89e8bbf65
        // [160.97s] 26|D98|Deep Freeze|3.00|4000414C|Exdeath|10018AEA|MeleeDpsB|00|226668|37552669|4f3df60114779cf7
        world.Events.Add(160.97f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.DeepFreeze, 3.000f));
    }


    private void Run_Exdeath_400040E8_1()
    {
        SimEnemy? exdeath_400040E8_1 = null;
        // [0.75s] 03|400040E8|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||90.10|109.90|0.00|0.00|92e652f44c745529
        world.Events.Add(0.75f, () => exdeath_400040E8_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f))));
        // [1.07s] 21|400040E8|Exdeath|BB0C|Thunder III|100AF82E|MainTank|550003|C67B4002|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|325133|325133|2800|10000|||85.89|98.56|0.00|-3.05|44|44|0|10000|||90.10|109.90|0.00|0.00|000083D9|0|1|00||01|BB0C|BB0C|1.100|7FFF|2e419e3188ddc6fe
        world.Events.Add(1.07f, () => exdeath_400040E8_1?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [1.07s] 264|400040E8|BB0C|000083D9|0||||0.000|100AF82E|0d66b27021f1c085
        // [1.07s] 26|BB6|Lightning Resistance Down II|3.96|400040E8|Exdeath|100AF82E|MainTank|00|325133|44|54b9ab62af1c0641
        world.Events.Add(1.07f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
        // [4.10s] 21|400040E8|Exdeath|BB0C|Thunder III|100AE96C|RegenHealer|550203|D4B84001|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|357551|357551|8200|10000|||85.62|101.25|-0.02|1.67|44|44|0|10000|||90.10|109.90|0.00|0.00|000083F2|0|1|00||01|BB0C|BB0C|1.100|7FFF|cdc7732c8ba26490
        world.Events.Add(4.10f, () => exdeath_400040E8_1?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [4.10s] 264|400040E8|BB0C|000083F2|0||||0.000|100AE96C|e57fe657f409eaa8
        // [4.10s] 26|BB6|Lightning Resistance Down II|3.96|400040E8|Exdeath|100AE96C|RegenHealer|00|357551|44|f40e72e0c1d98b1c
        world.Events.Add(4.10f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
        // [5.03s] 30|BB6|Lightning Resistance Down II|0.00|400040E8|Exdeath|100AF82E|MainTank|00|325133|44|23b31915bd7e6e8e
        world.Events.Add(5.03f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.LightningResistanceDownII));
        // [8.05s] 30|BB6|Lightning Resistance Down II|0.00|400040E8|Exdeath|100AE96C|RegenHealer|00|325047|44|cd4e4be62488971d
        world.Events.Add(8.05f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.LightningResistanceDownII));
        // [16.08s] 261|Change|400040E8|BNpcNameID|1BDB|Heading|-0.7855|Name|Kefka|PosX|100.0000|PosY|114.1421|PosZ|0.0000|d12d225b50c2e4c5
        // [16.08s] 261|Change|400040E8|BNpcNameID|1BDB|Heading|-0.7855|Name|Kefka|PosX|100.0000|PosY|114.1421|PosZ|0.0000|fc92497e049a6033
    }

    private void Run_Chaos_400040E9_1()
    {
        SimEnemy? chaos_400040E9_1 = null;
        // [0.89s] 271|400040E9|0.0000|00|00|100.0000|100.0000|0.0000|9328a62a3d585ff3
        world.Events.Add(0.89f, () => chaos_400040E9_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [0.98s] 20|400040E9|Exdeath|C572|Earthquake|400040E9|Exdeath|4.700|90.10|109.90|0.00|0.00|071c0a2d11eb3d31
        world.Events.Add(0.98f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 4.700f, targetId: chaos_400040E9_1?.GameObjectId));
        // [0.75s] 03|400040E9|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|100.00|0.00|0.00|2690c08f8bd9817d
        world.Events.Add(0.75f, () => chaos_400040E9_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|100702A3|Player|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|205207|205207|7100|10000|||100.51|99.78|0.00|-1.51|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|0|8|00||01|C572|C572|1.100|7FFF|3e5ac8746819fa9a
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|100A7A8F|OffTank|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||100.89|100.22|0.00|-1.55|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|1|8|00||01|C572|C572|1.100|7FFF|71aaca8d4ad06f19
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|1009061B|MeleeDpsA|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|7695|10000|||101.18|100.85|0.00|-2.67|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|2|8|00||01|C572|C572|1.100|7FFF|65aedfc171dd52de
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|10066D86|ShieldHealer|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|7175|10000|||99.05|101.95|0.00|-1.84|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|3|8|00||01|C572|C572|1.100|7FFF|9782c677a6c95ac8
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|10018AEA|MeleeDpsB|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||97.54|99.80|0.00|-1.89|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|4|8|00||01|C572|C572|1.100|7FFF|b908af9a6758eeda
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|100AC8F1|CasterDps|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||98.14|98.65|-0.01|-2.25|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|5|8|00||01|C572|C572|1.100|7FFF|e19cc3be0cc58f4a
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|100AF82E|MainTank|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|253530|325133|4600|10000|||101.90|98.95|0.00|1.15|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|6|8|00||01|C572|C572|1.100|7FFF|c8617d2f3b53a367
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [5.97s] 22|400040E9|Chaos|C572|Earthquake|100AE96C|RegenHealer|4A|10000|1B|C5728000|0|0|0|0|0|0|0|0|0|0|0|0|357551|357551|7400|10000|||94.66|102.64|0.00|1.89|44|44|0|10000|||100.00|100.00|0.00|0.00|00008401|7|8|00||01|C572|C572|1.100|7FFF|b7fcad34888f74e2
        world.Events.Add(5.97f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_C572, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [5.97s] 264|400040E9|C572|00008401|1|-0.015|-0.015|-0.015|0.000|400040E9|5bc577b2ed86270e
        // [5.66s] 261|Change|400040E9|CastBuffID|0|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|dc4c71439d2778b2
        // [12.29s] 271|400040E9|0.0000|00|00|100.0000|104.0000|0.0000|8a51601a2b57ea9f
        world.Events.Add(12.29f, () => chaos_400040E9_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450603|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|124779|205177|9365|10000|||101.71|101.22|0.00|-1.68|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|0|7|00||01|BAFA|BAFA|1.100|7FFF|a1390303b931cec6
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450603|DA50000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|111355|205207|7100|10000|||100.24|100.05|0.00|-1.23|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|1|7|00||01|BAFA|BAFA|1.100|7FFF|e106b363bfea0308
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450603|18DB0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|104817|227550|10000|10000|||99.69|99.87|-0.02|-1.85|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|2|7|00||01|BAFA|BAFA|1.100|7FFF|fc80b31c249dcff6
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450603|2D990000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|128683|226668|10000|10000|||98.95|99.75|0.00|1.21|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|3|7|00||01|BAFA|BAFA|1.100|7FFF|33400dde734980fb
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450603|13330000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|103010|217488|10000|10000|||100.18|99.40|0.00|-1.01|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|4|7|00||01|BAFA|BAFA|1.100|7FFF|39c9302846b0ffc8
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450603|17E0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|276321|325047|5800|10000|||103.56|100.66|0.00|-1.50|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|5|7|00||01|BAFA|BAFA|1.100|7FFF|0c81af432aae5707
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [12.38s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450603|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|107206|325133|5200|10000|||104.93|101.02|0.00|1.16|44|44|0|10000|||100.00|104.00|0.00|0.00|00008435|6|7|00||01|BAFA|BAFA|1.100|7FFF|38536f1ece4a5e8a
        world.Events.Add(12.38f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [12.38s] 264|400040E9|BAFA|00008435|1|-0.015|-0.015|-0.015|0.000|400040E9|2597715bf8f7fa74
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|a152e65a0a56b687
        world.Events.Add(12.38f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|248e659dd393de74
        world.Events.Add(12.38f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|b621d9e4c09a5790
        world.Events.Add(12.38f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|95f7376b4afd3928
        world.Events.Add(12.38f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|f4b4b28e90e3ab30
        world.Events.Add(12.38f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|83fa9096086c558d
        world.Events.Add(12.38f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [12.38s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|fc8dd80d5e343f03
        world.Events.Add(12.38f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|aa94c453c01601d6
        world.Events.Add(14.34f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|a15323265083df8b
        world.Events.Add(14.34f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|1ba5b52b3d73fd68
        world.Events.Add(14.34f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|d555fc5a1494671d
        world.Events.Add(14.34f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|ca99351b7dbf3c08
        world.Events.Add(14.34f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|6d0cab122e56ccf5
        world.Events.Add(14.34f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [14.34s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|39a7e10f30908a10
        world.Events.Add(14.34f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450603|2354001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|9435|10000|||101.61|101.34|0.00|-2.57|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|0|7|00||01|BAFA|BAFA|1.100|7FFF|6653aae801b5ded1
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450603|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|6470|10000|||99.66|100.54|-0.02|-1.33|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|1|7|00||01|BAFA|BAFA|1.100|7FFF|c27eca0e21ff8dd5
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|E5A4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||99.69|99.87|0.00|1.64|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|2|7|00||01|BAFA|BAFA|1.100|7FFF|184772617f341767
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|A1E40000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|287540|325047|6800|10000|||102.43|100.39|0.00|-1.43|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|3|7|00||01|BAFA|BAFA|1.100|7FFF|bba1ab8c23bf1ca1
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|9E520000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|296103|325133|6000|10000|||102.16|100.18|0.00|-2.50|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|4|7|00||01|BAFA|BAFA|1.100|7FFF|079761551837f865
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|17034001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||100.56|99.56|0.00|-1.13|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|5|7|00||01|BAFA|BAFA|1.100|7FFF|351e47ef5bd4d4bb
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [16.30s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|17A34001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||100.62|98.73|0.00|1.01|44|44|0|10000|||100.00|104.00|0.00|0.00|00008454|6|7|00||01|BAFA|BAFA|1.100|7FFF|2020cd76e9cc68b2
        world.Events.Add(16.30f, () => chaos_400040E9_1?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [16.30s] 264|400040E9|BAFA|00008454|1|-0.015|-0.015|-0.015|0.000|400040E9|390727e1935e6a7f
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|70acebf1f11d1c52
        world.Events.Add(16.30f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|6b9379335305e4de
        world.Events.Add(16.30f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|1a1c3f6717cb128e
        world.Events.Add(16.30f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|7ce4bd02d8f90154
        world.Events.Add(16.30f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|f3e9a5ff1fbc7390
        world.Events.Add(16.30f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|48524cd5390eefca
        world.Events.Add(16.30f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [16.30s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|795c9fa8b57e122f
        world.Events.Add(16.30f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|1dd26ec021c4c86f
        world.Events.Add(18.26f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|449e8db6cdafac71
        world.Events.Add(18.26f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|6658db111121388b
        world.Events.Add(18.26f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|8b89cf9a69a4a23e
        world.Events.Add(18.26f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|a6b340c2b74a0426
        world.Events.Add(18.26f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|4d6b9ad67814e351
        world.Events.Add(18.26f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [18.26s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|9549b520f45bb4ed
        world.Events.Add(18.26f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [24.35s] 261|Change|400040E9|BNpcNameID|1BDB|Name|Kefka|702e94681f5a90c3
    }

    private void Run_Kefka_40004141()
    {
        SimEnemy? kefka_40004141 = null;
        // [-172.57s] 272|40004141|E0000000|0000|00|5ce7cdf251f35279
        // [-172.87s] 03|40004141|Kefka|00|64|0000|00||7131|19504|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.00|c82d564f4db6825d
        world.Events.Add(0f, () => kefka_40004141 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaP3, NameId: BNpcNameId.Kefka, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(5.88f, () => kefka_40004141?.AddStatus(StatusId.Unknown9E8, stacks: (ushort)506, overrideStacks: true));
        // [5.88s] 273|40004141|003F|5|0|0|0|347c7a7c017c9ded
        world.Events.Add(5.88f, () => kefka_40004141?.SetModelState((byte)0x05));
        // [5.88s] 271|40004141|0.0000|00|00|100.0000|100.0000|0.0000|c16d540c8411d734
        world.Events.Add(5.88f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [5.55s] 261|Change|40004141|Heading|0.0000|ModelStatus|18432|PCTargetID|0|PosX|100.0000|PosY|100.0000|PosZ|0.0000|Radius|22.5000|TransformationId|506|WeaponId|5|60a3278fb61ccfc9
        // No engine API for TransformationId (boss form is driven by BNpcBase / status param, see Statuses.cs); skipped.
        // world.Events.Add(5.55f, () => kefka_40004141?.SetTransformationId((short)506));
        // [5.97s] 273|40004141|0197|11D4|0|0|0|6a7f44e33ef77400
        // [12.11s] 273|40004141|0197|1E3A|0|0|0|a94267eb3b90f507
        // [11.83s] 261|Change|40004141|ModelStatus|0|PCTargetID|1009061B|0de4062f6213983d
        world.Events.Add(11.83f, () => kefka_40004141?.SetVisible(true));
        // [14.16s] 271|40004141|2.3562|00|00|100.0000|100.0000|0.0000|3248ef5dddbe7c44
        world.Events.Add(14.16f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 2.356f)));
        // [13.82s] 261|Change|40004141|Heading|2.3562|ModelStatus|16384|PosX|100.0000|PosY|100.0000|PosZ|0.0000|c9447dbb042ee93c
        world.Events.Add(13.82f, () => kefka_40004141?.SetVisible(false));
        // [14.25s] 273|40004141|0197|1E44|0|0|0|04d181dfc58c5177
        // [14.91s] 261|Change|40004141|ModelStatus|0|NPCTargetID|10066D86|PCTargetID|10066D86|217061f360b903b2
        world.Events.Add(14.91f, () => kefka_40004141?.SetVisible(true));
        // [16.39s] 20|40004141|Kefka|BAE6|Slap Happy|40004141|Kefka|4.700|100.00|100.00|0.00|2.36|cae99ccdee573a87
        world.Events.Add(16.39f, () => kefka_40004141?.Cast(ActionId.SlapHappy, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 4.700f, targetId: kefka_40004141?.GameObjectId));
        // [16.08s] 261|Change|40004141|CastBuffID|47846|CastDurationMax|4.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|0e3d9b08f5faedc8
        // [21.38s] 21|40004141|Kefka|BAE6|Slap Happy|40004141|Kefka|1B|BAE68000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|2.36|56331828|56331828|10000|10000|||100.00|100.00|0.00|2.36|00008481|0|1|00||01|BAE6|BAE6|6.100|DFFF|260dfa7ff3b7f267
        // [21.38s] 264|40004141|BAE6|00008481|0||||2.356|40004141|3b43e2eaaa239715
        // [21.12s] 261|Change|40004141|CastBuffID|0|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|560b405c87938ecb
        // [42.54s] 273|40004141|0197|1E3A|0|0|0|a54bef4df2b20755
        // [44.60s] 271|40004141|1.5708|00|00|100.0000|100.0000|0.0000|3e332ee09f9618fe
        world.Events.Add(44.60f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 1.571f)));
        // [44.15s] 261|Change|40004141|Heading|1.5708|ModelStatus|16384|9644f428b82aa60a
        world.Events.Add(44.15f, () => kefka_40004141?.SetVisible(false));
        // [44.69s] 273|40004141|0197|1E44|0|0|0|5ae08a1fe74753f5
        // [46.83s] 20|40004141|Kefka|BAE6|Slap Happy|40004141|Kefka|4.700|100.00|100.00|0.00|1.57|2097d0dd233896ea
        world.Events.Add(46.83f, () => kefka_40004141?.Cast(ActionId.SlapHappy, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 4.700f, targetId: kefka_40004141?.GameObjectId));
        // [46.53s] 261|Change|40004141|CastBuffID|47846|CastDurationCurrent|0.0338|CastDurationMax|4.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|ModelStatus|0|25064fcacf73abad
        world.Events.Add(46.53f, () => kefka_40004141?.SetVisible(true));
        // [51.79s] 21|40004141|Kefka|BAE6|Slap Happy|40004141|Kefka|1B|BAE68000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|1.57|56331828|56331828|10000|10000|||100.00|100.00|0.00|1.57|00008577|0|1|00||01|BAE6|BAE6|6.100|BFFF|e212d7009a6952b0
        // [51.79s] 264|40004141|BAE6|00008577|0||||1.571|40004141|ff4e6888dd525a95
        // [51.51s] 261|Change|40004141|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|0005df19c0b20f81
        // [69.97s] 273|40004141|0197|1E3A|0|0|0|fee4dd403541d063
        // [72.02s] 271|40004141|0.7854|00|00|100.0000|100.0000|0.0000|486d4376c1b1c5bc
        world.Events.Add(72.02f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.785f)));
        // [71.80s] 261|Change|40004141|Heading|0.7854|ModelStatus|16384|3b62695f6bcada3a
        world.Events.Add(71.80f, () => kefka_40004141?.SetVisible(false));
        // [72.11s] 273|40004141|0197|1E44|0|0|0|758d1ed2b330b57a
        // [74.25s] 20|40004141|Kefka|BAEC|Look upon Me and Despair|40004141|Kefka|3.700|100.00|100.00|0.00|0.79|94374a3931857975
        world.Events.Add(74.25f, () => kefka_40004141?.Cast(ActionId.LookUponMeAndDespair, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 3.700f, targetId: kefka_40004141?.GameObjectId));
        // [73.93s] 261|Change|40004141|CastBuffID|47852|CastDurationCurrent|0.0130|CastDurationMax|3.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|ModelStatus|0|8b49c92f78cc0d20
        world.Events.Add(73.93f, () => kefka_40004141?.SetVisible(true));
        // [78.22s] 21|40004141|Kefka|BAEC|Look upon Me and Despair|40004141|Kefka|49|78000|1B|BAEC8000|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|0000867F|0|1|00||01|BAEC|BAEC|3.100|9FFF|5f4ab2d727e335c2
        // [78.22s] 264|40004141|BAEC|0000867F|0||||0.785|40004141|a252737cec15fa28
        // [77.95s] 261|Change|40004141|CastDurationCurrent|3.9757|IsCasting1|0|8671672f6e014cf8
        // [79.38s] 273|40004141|003F|7|0|0|0|045244770be31955
        world.Events.Add(79.38f, () => kefka_40004141?.SetModelState((byte)0x07));
        // [79.07s] 261|Change|40004141|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting2|0|WeaponId|7|1fc08862d45e6c26
        // [81.39s] 21|40004141|Kefka|C4BA|unknown_c4ba|40004141|Kefka|49|58000|1B|C4BA8000|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|0000869B|0|1|00||01|C4BA|C4BA|4.100|9FFF|8a26eaded2053da6
        world.Events.Add(81.39f, () => kefka_40004141?.Cast(ActionId.UnknownC4ba, castSeconds: 0f, targetId: kefka_40004141?.GameObjectId));
        // [81.39s] 264|40004141|C4BA|0000869B|0||||0.785|40004141|549d2139bbe14262
        // [82.20s] 273|40004141|003F|5|0|0|0|d2f5b7d6284d8ff8
        world.Events.Add(82.20f, () => kefka_40004141?.SetModelState((byte)0x05));
        // [81.81s] 261|Change|40004141|WeaponId|5|30320bc8f807ecde
        // [106.50s] 273|40004141|0197|1E3A|0|0|0|a53480b6f545296c
        // [108.56s] 271|40004141|-2.3563|00|00|100.0000|100.0000|0.0000|6de87f6f5eb4bda9
        world.Events.Add(108.56f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.356f)));
        // [108.17s] 261|Change|40004141|Heading|-2.3563|ModelStatus|16384|PosX|100.0000|PosY|100.0000|PosZ|0.0000|fb9df35501d3fe42
        world.Events.Add(108.17f, () => kefka_40004141?.SetVisible(false));
        // [108.65s] 273|40004141|0197|1E44|0|0|0|08371dd0e79c6202
        // [114.81s] 20|40004141|Kefka|BAE7|Slap Happy|40004141|Kefka|4.700|100.00|100.00|0.00|-2.36|226ca9eb0b538ea6
        world.Events.Add(114.81f, () => kefka_40004141?.Cast(ActionId.SlapHappy_BAE7, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 4.700f, targetId: kefka_40004141?.GameObjectId));
        // [114.53s] 261|Change|40004141|CastBuffID|47847|CastDurationCurrent|0.0164|CastDurationMax|4.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|ModelStatus|0|e59ebd3b25a2c7a9
        world.Events.Add(114.53f, () => kefka_40004141?.SetVisible(true));
        // [119.81s] 21|40004141|Kefka|BAE7|Slap Happy|40004141|Kefka|1B|BAE78000|0|0|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|-2.36|56331828|56331828|10000|10000|||100.00|100.00|0.00|-2.36|000087C6|0|1|00||01|BAE7|BAE7|6.100|1FFF|62b9fbc93fcbfc3f
        // [119.81s] 264|40004141|BAE7|000087C6|0||||-2.356|40004141|0699f4a7cc1fdd64
        // [119.47s] 261|Change|40004141|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|846e68d1409f49c2
        // [127.98s] 273|40004141|0197|1E3A|0|0|0|e675ce5d82154321
        // [130.02s] 271|40004141|0.7854|00|00|100.0000|100.0000|0.0000|d4aa0a25cf5d6ca0
        world.Events.Add(130.02f, () => kefka_40004141?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.785f)));
        // [130.11s] 273|40004141|0197|1E44|0|0|0|58df69db1ec602f0
        // [129.77s] 261|Change|40004141|Heading|0.7854|ModelStatus|16384|PosX|100.0000|PosY|100.0000|PosZ|0.0000|8f12d9ea7708829f
        world.Events.Add(129.77f, () => kefka_40004141?.SetVisible(false));
        // [132.26s] 20|40004141|Kefka|BAED|Look upon Me and Despair|40004141|Kefka|3.700|100.00|100.00|0.00|0.79|6907c30a53ca9fa8
        world.Events.Add(132.26f, () => kefka_40004141?.Cast(ActionId.LookUponMeAndDespair_BAED, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 3.700f, targetId: kefka_40004141?.GameObjectId));
        // [132.02s] 261|Change|40004141|CastBuffID|47853|CastDurationCurrent|0.0160|CastDurationMax|3.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|ModelStatus|0|26b103b6a67f1c8d
        world.Events.Add(132.02f, () => kefka_40004141?.SetVisible(true));
        // [136.24s] 21|40004141|Kefka|BAED|Look upon Me and Despair|40004141|Kefka|49|78000|1B|BAED8000|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|0000883F|0|1|00||01|BAED|BAED|3.100|9FFF|6baa0844edcecd18
        // [136.24s] 264|40004141|BAED|0000883F|0||||0.785|40004141|e39d8c2121f6e994
        // [135.91s] 261|Change|40004141|CastDurationCurrent|3.9658|IsCasting1|0|187ba45013a8bef1
        // [137.40s] 273|40004141|003F|7|0|0|0|d68d2f42e785901d
        world.Events.Add(137.40f, () => kefka_40004141?.SetModelState((byte)0x07));
        // [137.14s] 261|Change|40004141|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting2|0|WeaponId|7|0a748ae7eeb3ccc9
        // [139.41s] 21|40004141|Kefka|C533|unknown_c533|40004141|Kefka|49|68000|1B|C5338000|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|00008856|0|1|00||01|C533|C533|4.100|9FFF|20516e9d6f224310
        world.Events.Add(139.41f, () => kefka_40004141?.Cast(ActionId.UnknownC533, castSeconds: 0f, targetId: kefka_40004141?.GameObjectId));
        // [139.41s] 264|40004141|C533|00008856|0||||0.785|40004141|072a0a25f6993eef
        // [140.22s] 273|40004141|003F|6|0|0|0|818696fea68edbb1
        world.Events.Add(140.22f, () => kefka_40004141?.SetModelState((byte)0x06));
        // [139.93s] 261|Change|40004141|WeaponId|6|7e9287873a6d6258
        // [145.52s] 20|40004141|Kefka|BAEF|Stomp-a-Mole|40004141|Kefka|4.700|100.00|100.00|0.00|0.79|d9e613f335c43f86
        world.Events.Add(145.52f, () => kefka_40004141?.Cast(ActionId.StompAMole_BAEF, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 4.700f, targetId: kefka_40004141?.GameObjectId));
        // [145.19s] 261|Change|40004141|CastBuffID|47855|CastDurationCurrent|0.0166|CastDurationMax|4.7000|CastTargetID|40004141|IsCasting1|1|IsCasting2|1|6c854b14c0b9281b
        // [150.48s] 21|40004141|Kefka|BAEF|Stomp-a-Mole|40004141|Kefka|49|58000|1B|BAEF8000|0|0|0|0|0|0|0|0|0|0|0|0|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|56331828|56331828|10000|10000|||100.00|100.00|0.00|0.79|000088B3|0|1|00||01|BAEF|BAEF|8.100|9FFF|e61e985dc9ac3b63
        // [150.48s] 264|40004141|BAEF|000088B3|0||||0.785|40004141|383958199ac37f50
        // [150.17s] 261|Change|40004141|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|c9db342143a652b4
        // [152.13s] 273|40004141|003F|5|0|0|0|035f184b13be0804
        world.Events.Add(152.13f, () => kefka_40004141?.SetModelState((byte)0x05));
        // [151.80s] 261|Change|40004141|WeaponId|5|30c9df1aacf76282
    }

    private void Run_EventObj_1EC03D_40004163()
    {
        SimEventObject? eventObj_1EC03D_40004163 = null;
        // [6.04s] 261|Add|40004163|BNpcID|1EC03D|CastBuffID|6649196|CastDurationCurrent|0.0000|CastDurationMax|228278500000000000000.0000|CastGroundTargetX|10926400000000.0000|CastGroundTargetY|0.0000|CastGroundTargetZ|10949250000000.0000|CastTargetID|3000000|CurrentMP|4456451|Heading|0.0000|IsCasting1|1|IsCasting2|116|Job|235|Level|225|ModelStatus|2048|NPCTargetID|B81C0000|PosX|100.0000|PosY|104.0000|Radius|0.5000|TransformationId|32758|Type|7|WeaponId|7|712762e370e33c55
        world.Events.Add(6.04f, () => eventObj_1EC03D_40004163 = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.EventObj1EC03D, Placement = new Placement(new Vector3(0f, 0f, 0f), 0.000f), SpawnVisible = false }));
        // [6.13s] 261|Change|40004163|MaxMP|1|ModelStatus|0|a5fafa0f3c54c552
        world.Events.Add(6.13f, () => eventObj_1EC03D_40004163?.SetVisible(true));
        // [140.13s] 261|Remove|40004163|ef8244ef227fd7ea
        world.Events.Add(140.13f, () => eventObj_1EC03D_40004163?.Despawn());
    }


    private void Run_Kefka_400040E5_1()
    {
        SimEnemy? kefka_400040E5_1 = null;
        // [16.39s] 271|400040E5|0.0000|00|00|100.0000|100.0000|0.0000|81486914bd51ba73
        world.Events.Add(16.39f, () => kefka_400040E5_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [16.17s] 03|400040E5|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|100.00|0.00|0.00|72bab1163c9517ab
        world.Events.Add(16.17f, () => kefka_400040E5_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        // [23.25s] 20|400040E5|Kefka|BAE9|Slap Happy|400040E5|Kefka|1.200|100.00|100.00|0.00|0.00|a15147eb84ad9107
        world.Events.Add(23.25f, () => kefka_400040E5_1?.Cast(ActionId.SlapHappy_BAE9, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 1.200f, targetId: kefka_400040E5_1?.GameObjectId));
        // [22.95s] 261|Change|400040E5|CastBuffID|47849|CastDurationCurrent|0.0169|CastDurationMax|1.2000|CastTargetID|400040E5|IsCasting1|1|IsCasting2|1|0bc60b79a14ff2f7
        // [24.72s] 21|400040E5|Kefka|BAE9|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.00|0000849A|0|0|00||01|BAE9|BAE9|1.100|7FFF|50605fd5a9bc4385
        // [24.72s] 264|400040E5|BAE9|0000849A|1|-0.015|-0.015|-0.015|0.000|400040E5|365aa619e4feb2e6
        // [24.35s] 261|Change|400040E5|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|aac26e24f73f48b6
        // [118.63s] 261|Change|400040E5|BNpcNameID|1E0B|Heading|3.1247|Name|Chaos|PosX|100.7376|PosY|100.6464|PosZ|0.0000|c19d337a0d0e7bf0
    }

    private void Run_Kefka_400040E6_1()
    {
        SimEnemy? kefka_400040E6_1 = null;
        // [16.39s] 271|400040E6|-0.7855|00|00|114.1421|100.0000|0.0000|e0f344f1cd7b5f7c
        world.Events.Add(16.39f, () => kefka_400040E6_1?.SetPosition(new Placement(new Vector3(14.142f, 0.000f, 0.000f), -0.785f)));
        // [16.17s] 03|400040E6|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||114.14|100.00|0.00|-0.79|94832152c1ca84dd
        world.Events.Add(16.17f, () => kefka_400040E6_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(14.140f, 0.000f, 0.000f), -0.790f))));
        // [23.47s] 21|400040E6|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||114.14|100.00|0.00|-0.79|00008491|0|0|00||01|BAE8|BAE8|1.100|5FFF|976ff3d1b978f204
        world.Events.Add(23.47f, () => kefka_400040E6_1?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [23.47s] 264|400040E6|BAE8|00008491|1|-0.015|-0.015|-0.015|-0.785|400040E6|aacc349cb6411922
        // [46.83s] 271|400040E6|0.0000|00|00|100.0000|100.0000|0.0000|d51ae59d75e7af45
        world.Events.Add(46.83f, () => kefka_400040E6_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [53.71s] 20|400040E6|Kefka|BAE9|Slap Happy|400040E6|Kefka|1.200|100.00|100.00|0.00|0.00|63d3f063a783fc9f
        world.Events.Add(53.71f, () => kefka_400040E6_1?.Cast(ActionId.SlapHappy_BAE9, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 1.200f, targetId: kefka_400040E6_1?.GameObjectId));
        // [53.40s] 261|Change|400040E6|CastBuffID|47849|CastDurationCurrent|0.0168|CastDurationMax|1.2000|CastTargetID|400040E6|IsCasting1|1|IsCasting2|1|81bb36be50ba6c61
        // [55.17s] 21|400040E6|Kefka|BAE9|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.00|0000859D|0|0|00||01|BAE9|BAE9|1.100|7FFF|739b2d26c07a6bdb
        // [55.17s] 264|400040E6|BAE9|0000859D|1|-0.015|-0.015|-0.015|0.000|400040E6|9cc7df4a1c0ff8b9
        // [54.76s] 261|Change|400040E6|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|b4be9d55708ff877
        // [121.69s] 20|400040E6|Kefka|BAE9|Slap Happy|400040E6|Kefka|1.200|100.00|100.00|0.00|0.00|7b350d7e866d952d
        world.Events.Add(121.69f, () => kefka_400040E6_1?.Cast(ActionId.SlapHappy_BAE9, targetLocation: new Vector3(-0.008f, -0.015f, -0.008f), castSeconds: 1.200f, targetId: kefka_400040E6_1?.GameObjectId));
        // [121.44s] 261|Change|400040E6|CastBuffID|47849|CastDurationCurrent|0.0170|CastDurationMax|1.2000|CastTargetID|400040E6|IsCasting1|1|IsCasting2|1|251e50d1efdcfaad
        // [123.16s] 21|400040E6|Kefka|BAE9|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.00|000087DF|0|0|00||01|BAE9|BAE9|1.100|7FFF|b84bcde64e858891
        // [123.16s] 264|400040E6|BAE9|000087DF|1|-0.015|-0.015|-0.015|0.000|400040E6|9045a6d52e7b1d15
        // [122.93s] 261|Change|400040E6|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|bc36a6e8506b35a1
    }

    private void Run_Kefka_400040E7_1()
    {
        SimEnemy? kefka_400040E7_1 = null;
        // [16.39s] 271|400040E7|-0.7855|00|00|107.0711|107.0711|0.0000|cf30899ecf75eda9
        world.Events.Add(16.39f, () => kefka_400040E7_1?.SetPosition(new Placement(new Vector3(7.071f, 0.000f, 7.071f), -0.785f)));
        // [16.17s] 03|400040E7|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||107.07|107.07|0.00|-0.79|feb7763200044def
        world.Events.Add(16.17f, () => kefka_400040E7_1 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(7.070f, 0.000f, 7.070f), -0.790f))));
        // [22.76s] 21|400040E7|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||107.07|107.07|0.00|-0.79|0000848B|0|0|00||01|BAE8|BAE8|1.100|5FFF|d18a2aefffd9ef3c
        world.Events.Add(22.76f, () => kefka_400040E7_1?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [22.76s] 264|400040E7|BAE8|0000848B|1|-0.015|-0.015|-0.015|-0.785|400040E7|8a977bbfe458762f
        // [46.83s] 271|400040E7|0.0000|00|00|110.0000|110.0000|0.0000|f1f5c3162b9c4449
        world.Events.Add(46.83f, () => kefka_400040E7_1?.SetPosition(new Placement(new Vector3(10.000f, 0.000f, 10.000f), 0.000f)));
        // [53.88s] 21|400040E7|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||110.00|110.00|0.00|0.00|0000858D|0|0|00||01|BAE8|BAE8|1.100|7FFF|8fb17bae44730cac
        world.Events.Add(53.88f, () => kefka_400040E7_1?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [53.88s] 264|400040E7|BAE8|0000858D|1|-0.015|-0.015|-0.015|0.000|400040E7|97117b8f58ba0185
        // [114.81s] 271|400040E7|-0.7855|00|00|85.8579|100.0000|0.0000|0359c4611f11c102
        world.Events.Add(114.81f, () => kefka_400040E7_1?.SetPosition(new Placement(new Vector3(-14.142f, 0.000f, 0.000f), -0.785f)));
        // [121.87s] 21|400040E7|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||85.86|100.00|0.00|-0.79|000087D7|0|0|00||01|BAE8|BAE8|1.100|5FFF|b4a8f46c5e916d49
        world.Events.Add(121.87f, () => kefka_400040E7_1?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [121.87s] 264|400040E7|BAE8|000087D7|1|-0.015|-0.015|-0.015|-0.785|400040E7|df81d5e76b43d8b1
        // [123.16s] 271|400040E7|0.0000|00|00|100.0000|100.0000|0.0000|079b17728b458bdd
        world.Events.Add(123.16f, () => kefka_400040E7_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [123.25s] 271|400040E7|2.3920|00|00|100.0000|100.0000|0.0000|d3121e2947638c1d
        world.Events.Add(123.25f, () => kefka_400040E7_1?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 2.392f)));
        // [123.25s] 22|400040E7|Kefka|BAEB|Shockwave|1009061B|MeleeDpsA|750603|CF2D4001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|205177|205177|5910|10000|||107.65|91.20|0.00|-0.74|44|44|0|10000|||85.86|100.00|0.00|-0.79|000087E0|0|2|00||01|BAEB|BAEB|1.100|E175|4b320e35f56bf928
        world.Events.Add(123.25f, () => kefka_400040E7_1?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [123.25s] 22|400040E7|Kefka|BAEB|Shockwave|10066D86|ShieldHealer|750603|9B024001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|205177|205177|9160|10000|||107.68|91.29|0.00|-0.11|44|44|0|10000|||85.86|100.00|0.00|-0.79|000087E0|1|2|00||01|BAEB|BAEB|1.100|E175|91cf16c5bc89b1e4
        world.Events.Add(123.25f, () => kefka_400040E7_1?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [123.25s] 264|400040E7|BAEB|000087E0|1|-0.015|-0.015|-0.015|2.392|400040E7|a029f8e47fb4f6e2
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E7|Kefka|1009061B|MeleeDpsA|00|205177|44|4386dcb376f67538
        world.Events.Add(123.25f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E7|Kefka|10066D86|ShieldHealer|00|205177|44|9b3d56e70f85b0a5
        world.Events.Add(123.25f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E7|Kefka|1009061B|MeleeDpsA|00|205177|44|441d29dcc01a2721
        world.Events.Add(125.21f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E7|Kefka|10066D86|ShieldHealer|00|205177|44|223b95091d47bac8
        world.Events.Add(125.21f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
    }

    private void Run_Kefka_400040E8_2()
    {
        SimEnemy? kefka_400040E8_2 = null;
        // [16.39s] 271|400040E8|-0.7855|00|00|100.0000|114.1421|0.0000|7fc6ae88ebc835bc
        world.Events.Add(16.39f, () => kefka_400040E8_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 14.142f), -0.785f)));
        // [16.17s] 03|400040E8|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|114.14|0.00|-0.79|f5e6a529dfaddfbc
        world.Events.Add(16.17f, () => kefka_400040E8_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 14.140f), -0.790f))));
        // [22.14s] 21|400040E8|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|114.14|0.00|-0.79|00008487|0|0|00||01|BAE8|BAE8|1.100|5FFF|e9b4f74c743a9c3e
        world.Events.Add(22.14f, () => kefka_400040E8_2?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [22.14s] 264|400040E8|BAE8|00008487|1|-0.015|-0.015|-0.015|-0.785|400040E8|7bc845a66abe3ba2
        // [46.83s] 271|400040E8|0.0000|00|00|100.0000|110.0000|0.0000|e596af2f04cbc142
        world.Events.Add(46.83f, () => kefka_400040E8_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 10.000f), 0.000f)));
        // [53.21s] 21|400040E8|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|110.00|0.00|0.00|00008586|0|0|00||01|BAE8|BAE8|1.100|7FFF|253c13b7798643bf
        world.Events.Add(53.21f, () => kefka_400040E8_2?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [53.21s] 264|400040E8|BAE8|00008586|1|-0.015|-0.015|-0.015|0.000|400040E8|5a3148efed635b2f
    }

    private void Run_Kefka_400040E9_2()
    {
        SimEnemy? kefka_400040E9_2 = null;
        // [24.72s] 271|400040E9|0.0000|00|00|100.0000|100.0000|0.0000|96a6ecd2e43c3724
        world.Events.Add(24.72f, () => kefka_400040E9_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [24.46s] 03|400040E9|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|100.00|0.00|0.00|526d58148231830d
        world.Events.Add(24.46f, () => kefka_400040E9_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        // [24.81s] 271|400040E9|-2.2128|00|00|100.0000|100.0000|0.0000|eba2aa3a055bae58
        world.Events.Add(24.81f, () => kefka_400040E9_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.213f)));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|10018AEA|MeleeDpsB|750603|C3454001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||93.83|94.22|0.00|0.74|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|0|8|00||01|BAEA|BAEA|1.100|25D8|2f706813cd65a03d
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|10066D86|ShieldHealer|750603|FE2B4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|7165|10000|||93.36|94.19|0.00|0.79|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|1|8|00||01|BAEA|BAEA|1.100|25D8|a1ea04f77c86046d
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AF82E|MainTank|750003|D37F4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|247238|325133|4200|10000|||94.71|92.79|0.00|0.70|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|2|8|00||01|BAEA|BAEA|1.100|25D8|fea52fd2460404c9
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AC8F1|CasterDps|750603|9E084001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||93.86|93.43|-0.01|0.63|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|3|8|00||01|BAEA|BAEA|1.100|25D8|f1675438ff41b2a3
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AE96C|RegenHealer|EC750005|6FAD4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|8800|10000|||92.64|94.17|0.00|0.14|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|4|8|00||01|BAEA|BAEA|1.100|25D8|72bfd7d6e22bd6e4
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|100702A3|Player|750603|D83E4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205207|205207|6700|10000|||93.13|93.77|0.00|0.51|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|5|8|00||01|BAEA|BAEA|1.100|25D8|493ca44df315cd6d
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|1009061B|MeleeDpsA|750603|AB694001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|8040|10000|||92.91|92.50|0.00|0.66|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|6|8|00||01|BAEA|BAEA|1.100|25D8|b6d0eb4c91cb74a5
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [24.81s] 22|400040E9|Kefka|BAEA|Shocking Impact|100A7A8F|OffTank|750603|9CB44001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||92.84|91.79|0.00|0.13|44|44|0|10000|||100.00|100.00|0.00|0.00|0000849B|7|8|00||01|BAEA|BAEA|1.100|25D8|8f67e8d4caf8126d
        world.Events.Add(24.81f, () => kefka_400040E9_2?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [24.81s] 264|400040E9|BAEA|0000849B|1|-0.015|-0.015|-0.015|-2.213|400040E9|06a3b691502e8bd1
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|3178c0feacd784f0
        world.Events.Add(24.81f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AF82E|MainTank|00|325133|44|f286d6267ccba348
        world.Events.Add(24.81f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AE96C|RegenHealer|00|325047|44|b40f20c83751e6eb
        world.Events.Add(24.81f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|f4b6c8c8041fc2fe
        world.Events.Add(24.81f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|cc550ed8313cbe89
        world.Events.Add(24.81f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100702A3|Player|00|205207|44|e4326254cd952769
        world.Events.Add(24.81f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|96532c9d4bb727ff
        world.Events.Add(24.81f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.81s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|42ade677527e8d63
        world.Events.Add(24.81f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [24.90s] 271|400040E9|-2.2129|00|00|100.0000|100.0000|0.0000|1a135dda95497615
        world.Events.Add(24.90f, () => kefka_400040E9_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.213f)));
        // [25.22s] 271|400040E9|-2.2587|00|00|100.0000|100.0000|0.0000|0104893084ef0837
        world.Events.Add(25.22f, () => kefka_400040E9_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.259f)));
        // [25.57s] 271|400040E9|-2.1896|00|00|100.0000|100.0000|0.0000|84db681c52d615bd
        world.Events.Add(25.57f, () => kefka_400040E9_2?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.190f)));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|f63567c15d9dc817
        world.Events.Add(26.77f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AF82E|MainTank|00|325133|44|a4d60b7f01afeab5
        world.Events.Add(26.77f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AE96C|RegenHealer|00|325047|44|5335862ffb7bf6e7
        world.Events.Add(26.77f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|47eb06f82c7ec464
        world.Events.Add(26.77f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|c7eda27dd264e22b
        world.Events.Add(26.77f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100702A3|Player|00|205207|44|f42bebb955665f1c
        world.Events.Add(26.77f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|2fffdf3a304eed95
        world.Events.Add(26.77f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [26.77s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|652e7b3a1349c4cd
        world.Events.Add(26.77f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [42.17s] 261|Change|400040E9|BNpcNameID|17A4|Heading|-2.1896|Name|Exdeath|1d80159fee5ffe57
    }

    private void Run_Black_Hole_40004166()
    {
        SimEnemy? black_Hole_40004166 = null;
        // [25.57s] 35|40004166||100AE96C|RegenHealer|0000|0000|0054|100AE96C|000F|0000|665c9150c5d061b1
        world.Events.Add(25.57f, () => world.Tether(black_Hole_40004166!, party.Get(PartyRole.RegenHealer)!, TetherId.Tether0054));
        // [25.17s] 261|Add|40004166|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-3.1416|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|117.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|ec7227a8247fee55
        // [25.17s] 03|40004166|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-3.14|f19c7bd30c844a33
        world.Events.Add(25.17f, () => black_Hole_40004166 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 17.000f), -3.140f))));
        // [25.57s] 272|40004166|10018AEA|0054|00|f3c8b1c7d9fb0f80
        // [28.07s] 35|40004166|Black Hole|1009061B|MeleeDpsA|0000|0000|0054|1009061B|000F|0000|6e0ba7e730ac9a75
        world.Events.Add(28.07f, () => world.Tether(black_Hole_40004166!, party.Get(PartyRole.MeleeDpsA)!, TetherId.Tether0054));
        // [27.73s] 261|Change|40004166|ModelStatus|0|7ed5f9ed10a28c22
        world.Events.Add(27.73f, () => black_Hole_40004166?.SetVisible(true));
        // [32.18s] 271|40004166|-2.5534|00|00|100.0000|117.0000|0.0000|7b636f3a87fa45a5
        world.Events.Add(32.18f, () => black_Hole_40004166?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 17.000f), -2.553f)));
        // [32.27s] 21|40004166|Black Hole|BAFC|Nothingness|1009061B|MeleeDpsA|750003|4CF50000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|203576|205177|7310|10000|||89.81|100.44|0.00|1.91|188300|188300|10000|10000|||100.00|117.00|0.00|-2.55|000084DA|0|1|00||01|BAFC|BAFC|1.100|17F7|f19c3c5f4d2016a9
        world.Events.Add(32.27f, () => black_Hole_40004166?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [32.27s] 264|40004166|BAFC|000084DA|0||||-2.553|40004166|a8c161b84ef5734c
        // [32.27s] 26|154C|Unbecoming|9999.00|40004166|Black Hole|1009061B|MeleeDpsA|01|205177|188300|f591a420b879c120
        world.Events.Add(32.27f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.Unbecoming));
        // [34.07s] 04|40004166|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-2.55|7eea0211bb262214
        world.Events.Add(34.07f, () => black_Hole_40004166?.Despawn());
        // [34.07s] 261|Remove|40004166|f9c8b3f1236fece0
    }

    private void Run_Black_Hole_40004167()
    {
        SimEnemy? black_Hole_40004167 = null;
        // [25.57s] 272|40004167|E0000000|0000|00|a39a50055f59a6d8
        // [25.17s] 261|Add|40004167|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.5709|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|117.0000|PosY|100.0000|Radius|1.0000|Type|2|WorldID|65535|be7543be721ddb15
        // [25.17s] 03|40004167|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-1.57|40fd1eec6a12d351
        world.Events.Add(25.17f, () => black_Hole_40004167 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.570f))));
        // [32.27s] 35|40004167|Black Hole|100AE96C|RegenHealer|0000|0000|0054|100AE96C|000F|0000|60d99b866f6631c4
        world.Events.Add(32.27f, () => world.Tether(black_Hole_40004167!, party.Get(PartyRole.RegenHealer)!, TetherId.Tether0054));
        // [32.27s] 35|40004167|Black Hole|100AF82E|MainTank|0000|0000|0054|100AF82E|000F|0000|79a1606638c66bb8
        world.Events.Add(32.27f, () => world.Tether(black_Hole_40004167!, party.Get(PartyRole.MainTank)!, TetherId.Tether0054));
        // [31.97s] 261|Change|40004167|ModelStatus|0|ea35b6c85ba4a8b2
        world.Events.Add(31.97f, () => black_Hole_40004167?.SetVisible(true));
        // [33.87s] 35|40004167|Black Hole|10066D86|ShieldHealer|0000|0000|0054|10066D86|000F|0000|31140dc2190ac4fc
        world.Events.Add(33.87f, () => world.Tether(black_Hole_40004167!, party.Get(PartyRole.ShieldHealer)!, TetherId.Tether0054));
        // [39.24s] 271|40004167|-0.9785|00|00|117.0000|100.0000|0.0000|99ec0b6112ee79d2
        world.Events.Add(39.24f, () => black_Hole_40004167?.SetPosition(new Placement(new Vector3(17.000f, 0.000f, 0.000f), -0.979f)));
        // [39.33s] 21|40004167|Black Hole|BAFC|Nothingness|10066D86|ShieldHealer|750003|4C9E0000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|205177|205177|7090|10000|||101.15|110.64|0.00|-2.68|188300|188300|10000|10000|||117.00|100.00|0.00|-0.98|00008511|0|1|00||01|BAFC|BAFC|1.100|5822|70f729424c529110
        world.Events.Add(39.33f, () => black_Hole_40004167?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [39.33s] 264|40004167|BAFC|00008511|0||||-0.978|40004167|34e47d795a3b0dc9
        // [39.33s] 26|154C|Unbecoming|9999.00|40004167|Black Hole|10066D86|ShieldHealer|01|205177|188300|676821c6ac85a2f5
        world.Events.Add(39.33f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.Unbecoming));
        // [41.33s] 04|40004167|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-0.98|95dae9a20797abc1
        world.Events.Add(41.33f, () => black_Hole_40004167?.Despawn());
        // [41.33s] 261|Remove|40004167|9d9365f7a8d2b259
    }

    private void Run_Black_Hole_40004168()
    {
        SimEnemy? black_Hole_40004168 = null;
        // [25.57s] 272|40004168|E0000000|0000|00|680f003803241984
        // [25.17s] 03|40004168|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|52a01b14bbbfaeca
        world.Events.Add(25.17f, () => black_Hole_40004168 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.000f))));
        // [25.17s] 261|Add|40004168|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|83.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|f247f780a9e099d9
        // [32.27s] 35|40004168|Black Hole|1009061B|MeleeDpsA|0000|0000|0054|1009061B|000F|0000|7e915302e9a2d35e
        world.Events.Add(32.27f, () => world.Tether(black_Hole_40004168!, party.Get(PartyRole.MeleeDpsA)!, TetherId.Tether0054));
        // [31.97s] 261|Change|40004168|ModelStatus|0|9421c58995f65a78
        world.Events.Add(31.97f, () => black_Hole_40004168?.SetVisible(true));
        // [39.24s] 271|40004168|-1.3636|00|00|100.0000|83.0000|0.0000|0374d26d9e50f950
        world.Events.Add(39.24f, () => black_Hole_40004168?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), -1.364f)));
        // [39.33s] 21|40004168|Black Hole|BAFC|Nothingness|1009061B|MeleeDpsA|750003|4F1B0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|6580|10000|||90.35|85.01|0.00|0.60|188300|188300|10000|10000|||100.00|83.00|0.00|-1.36|00008512|0|1|00||01|BAFC|BAFC|1.100|4871|664f259c48959822
        world.Events.Add(39.33f, () => black_Hole_40004168?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [39.33s] 264|40004168|BAFC|00008512|0||||-1.364|40004168|6df0f369e2d95844
        // [39.33s] 26|154D|Meanest Existence|9999.00|40004168|Black Hole|1009061B|MeleeDpsA|00|205177|188300|59f6dd1623f3d37a
        world.Events.Add(39.33f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MeanestExistence));
        // [41.33s] 04|40004168|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|-1.36|19bdc858ea97c268
        world.Events.Add(41.33f, () => black_Hole_40004168?.Despawn());
        // [41.33s] 261|Remove|40004168|682f825e4a191cbd
    }

    private void Run_Black_Hole_40004169()
    {
        SimEnemy? black_Hole_40004169 = null;
        // [25.57s] 272|40004169|E0000000|0000|00|4d31292452984086
        // [25.17s] 261|Add|40004169|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.3927|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|94.8343|PosY|87.5281|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|bc3c620e00bfb7bd
        // [25.17s] 03|40004169|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|87.53|0.00|0.39|01024d95bef5309a
        world.Events.Add(25.17f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-5.170f, 0.000f, -12.470f), 0.390f))));
        // [40.00s] 261|Change|40004169|ModelStatus|0|3b89b5cf2a901ab0
        world.Events.Add(40.00f, () => black_Hole_40004169?.SetVisible(true));
        // [41.33s] 04|40004169|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|87.53|0.00|0.39|548d67ac08424620
        world.Events.Add(41.33f, () => black_Hole_40004169?.Despawn());
        // [41.33s] 261|Remove|40004169|b166209e85a6e135
    }

    private void Run_Black_Hole_4000416A()
    {
        SimEnemy? black_Hole_4000416A = null;
        // [25.57s] 272|4000416A|E0000000|0000|00|d2c1b21615375b84
        // [25.17s] 261|Add|4000416A|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.9635|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|87.5281|PosY|105.1657|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|7d32f7ab545f93bb
        // [25.17s] 03|4000416A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|105.17|0.00|1.96|ec2076c870965bdd
        world.Events.Add(25.17f, () => black_Hole_4000416A = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-12.470f, 0.000f, 5.170f), 1.960f))));
        // [25.28s] 261|Change|4000416A|ModelStatus|0|a73ac6cf5c33a615
        world.Events.Add(25.28f, () => black_Hole_4000416A?.SetVisible(true));
        // [41.33s] 04|4000416A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|105.17|0.00|1.96|07c649e1652dc7ef
        world.Events.Add(41.33f, () => black_Hole_4000416A?.Despawn());
        // [41.33s] 261|Remove|4000416A|ccca0427ae0eaf10
    }

    private void Run_Black_Hole_4000416B()
    {
        SimEnemy? black_Hole_4000416B = null;
        // [25.57s] 272|4000416B|E0000000|0000|00|ae59e2a9825bb937
        // [25.17s] 261|Add|4000416B|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-2.7490|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|105.1657|PosY|112.4719|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|713816f46e20a814
        // [25.17s] 03|4000416B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|112.47|0.00|-2.75|373020296839394d
        world.Events.Add(25.17f, () => black_Hole_4000416B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(5.170f, 0.000f, 12.470f), -2.750f))));
        // [25.28s] 261|Change|4000416B|ModelStatus|0|62e361a08463e980
        world.Events.Add(25.28f, () => black_Hole_4000416B?.SetVisible(true));
        // [41.33s] 04|4000416B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|112.47|0.00|-2.75|d860af2a6f1d302a
        world.Events.Add(41.33f, () => black_Hole_4000416B?.Despawn());
        // [41.33s] 261|Remove|4000416B|bb0f755b93288c1e
    }

    private void Run_Black_Hole_4000416C()
    {
        SimEnemy? black_Hole_4000416C = null;
        // [25.57s] 272|4000416C|E0000000|0000|00|adce51c770c94bea
        // [25.17s] 03|4000416C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|94.83|0.00|-1.18|cf66bd92f268967d
        world.Events.Add(25.17f, () => black_Hole_4000416C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(12.470f, 0.000f, -5.170f), -1.180f))));
        // [25.28s] 261|Add|4000416C|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.1782|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|112.4719|PosY|94.8343|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|b363ba0c5ae6e334
        // [25.28s] 261|Change|4000416C|ModelStatus|0|8b9cc484417c596b
        world.Events.Add(25.28f, () => black_Hole_4000416C?.SetVisible(true));
        // [41.33s] 04|4000416C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|94.83|0.00|-1.18|4f5d9e1e2172e89a
        world.Events.Add(41.33f, () => black_Hole_4000416C?.Despawn());
        // [41.33s] 261|Remove|4000416C|7be3639adef607ed
    }

    private void Run_Black_Hole_4000416D()
    {
        SimEnemy? black_Hole_4000416D = null;
        // [25.57s] 272|4000416D|E0000000|0000|00|7d0e40df1ac4236e
        // [25.17s] 261|Add|4000416D|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.7854|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|93.6369|PosY|93.6369|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|2a456e180130a6d5
        // [25.17s] 03|4000416D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|93.64|0.00|0.79|7758e41a3c1c18b8
        world.Events.Add(25.17f, () => black_Hole_4000416D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-6.360f, 0.000f, -6.360f), 0.790f))));
        // [25.28s] 261|Change|4000416D|ModelStatus|0|cafe4220b6097a88
        world.Events.Add(25.28f, () => black_Hole_4000416D?.SetVisible(true));
        // [41.33s] 04|4000416D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|93.64|0.00|0.79|83940e24009bf9ac
        world.Events.Add(41.33f, () => black_Hole_4000416D?.Despawn());
        // [41.33s] 261|Remove|4000416D|503eb7864c7262e2
    }

    private void Run_Black_Hole_4000416E()
    {
        SimEnemy? black_Hole_4000416E = null;
        // [25.57s] 272|4000416E|E0000000|0000|00|93f2e11ed525370e
        // [25.17s] 03|4000416E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|106.36|0.00|2.36|1e5b60c952b820a5
        world.Events.Add(25.17f, () => black_Hole_4000416E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-6.360f, 0.000f, 6.360f), 2.360f))));
        // [25.17s] 261|Add|4000416E|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|2.3562|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|93.6369|PosY|106.3631|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|943b49691a098479
        // [25.28s] 261|Change|4000416E|ModelStatus|0|4c6503f93e10339b
        world.Events.Add(25.28f, () => black_Hole_4000416E?.SetVisible(true));
        // [41.33s] 04|4000416E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|106.36|0.00|2.36|7aa61ec5b7f5e841
        world.Events.Add(41.33f, () => black_Hole_4000416E?.Despawn());
        // [41.33s] 261|Remove|4000416E|f258a27b10975aea
    }

    private void Run_Black_Hole_4000416F()
    {
        SimEnemy? black_Hole_4000416F = null;
        // [25.57s] 272|4000416F|E0000000|0000|00|506dd32af7c479cd
        // [25.17s] 03|4000416F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|106.36|0.00|-2.36|b45c3cf72c8f0b12
        world.Events.Add(25.17f, () => black_Hole_4000416F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(6.360f, 0.000f, 6.360f), -2.360f))));
        // [25.17s] 261|Add|4000416F|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-2.3563|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|106.3631|PosY|106.3631|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|13897ac2ce43ab7e
        // [25.28s] 261|Change|4000416F|ModelStatus|0|4dafc9b9f541fc39
        world.Events.Add(25.28f, () => black_Hole_4000416F?.SetVisible(true));
        // [41.33s] 04|4000416F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|106.36|0.00|-2.36|8f097c066be0a514
        world.Events.Add(41.33f, () => black_Hole_4000416F?.Despawn());
        // [41.33s] 261|Remove|4000416F|f3e51cbf4be28d4a
    }

    private void Run_Black_Hole_40004170()
    {
        SimEnemy? black_Hole_40004170 = null;
        // [25.57s] 272|40004170|E0000000|0000|00|5c96b4ea3766f321
        // [25.17s] 03|40004170|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|93.64|0.00|-0.79|20b4ff6709ada662
        world.Events.Add(25.17f, () => black_Hole_40004170 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(6.360f, 0.000f, -6.360f), -0.790f))));
        // [25.17s] 261|Add|40004170|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-0.7855|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|106.3631|PosY|93.6369|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|2b7d91ab5ec9a1f3
        // [25.28s] 261|Change|40004170|ModelStatus|0|faa8b2e6f3fc9fb4
        world.Events.Add(25.28f, () => black_Hole_40004170?.SetVisible(true));
        // [41.33s] 04|40004170|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|93.64|0.00|-0.79|bae5995af97f23f6
        world.Events.Add(41.33f, () => black_Hole_40004170?.Despawn());
        // [41.33s] 261|Remove|40004170|4692ef97c07f4934
    }

    private void Run_Exdeath_400040E9_3()
    {
        SimEnemy? exdeath_400040E9_3 = null;
        // [42.28s] 03|400040E9|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|100.00|0.00|-2.19|c97da3cc36e29c72
        world.Events.Add(42.28f, () => exdeath_400040E9_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.190f))));
        // [42.63s] 21|400040E9|Exdeath|BB0C|Thunder III|100AE96C|RegenHealer|550103|0|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|325047|325047|9200|10000|||93.22|96.14|0.00|-0.38|44|44|0|10000|||100.00|100.00|0.00|-2.19|0000852D|0|1|00||01|BB0C|BB0C|1.100|26C9|1a69fca7a34552ea
        world.Events.Add(42.63f, () => exdeath_400040E9_3?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [42.63s] 264|400040E9|BB0C|0000852D|0||||-2.190|100AE96C|8696ad3ad3f137dd
        // [42.63s] 26|BB6|Lightning Resistance Down II|3.96|400040E9|Exdeath|100AE96C|RegenHealer|00|325047|44|f9fdc86222627e78
        world.Events.Add(42.63f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
        // [45.67s] 21|400040E9|Exdeath|BB0C|Thunder III|100AE96C|RegenHealer|550103|0|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|325047|325047|9400|10000|||92.64|95.84|0.00|0.99|44|44|0|10000|||100.00|100.00|0.00|-2.19|00008546|0|1|00||01|BB0C|BB0C|1.100|26C9|f96da63851a030d2
        world.Events.Add(45.67f, () => exdeath_400040E9_3?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [45.67s] 264|400040E9|BB0C|00008546|0||||-2.190|100AE96C|3896c00828e9b652
        // [45.67s] 26|BB6|Lightning Resistance Down II|3.96|400040E9|Exdeath|100AE96C|RegenHealer|00|325047|44|6e3daba5e2be9ec8
        world.Events.Add(45.67f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
    }

    private void Run_Kefka_400040E9_4()
    {
        SimEnemy? kefka_400040E9_4 = null;
        // [46.83s] 271|400040E9|0.0000|00|00|90.0000|110.0000|0.0000|b8c95d9134045ec6
        world.Events.Add(46.83f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(-10.000f, 0.000f, 10.000f), 0.000f)));
        // [46.53s] 261|Change|400040E9|BNpcNameID|1BDB|Heading|0.0000|Name|Kefka|PosX|90.0000|PosY|110.0000|PosZ|0.0000|b7c1dccc7759d7b7
        // [46.53s] 03|400040E9|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||90.00|110.00|0.00|0.00|93a364cfb589c1e8
        world.Events.Add(46.53f, () => kefka_400040E9_4 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-10.000f, 0.000f, 10.000f), 0.000f))));
        // [49.64s] 30|BB6|Lightning Resistance Down II|0.00|400040E9|Exdeath|100AE96C|RegenHealer|00|325047|44|5f37b685661ba2b0
        world.Events.Add(49.64f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.LightningResistanceDownII));
        // [52.59s] 21|400040E9|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||90.00|110.00|0.00|0.00|0000857E|0|0|00||01|BAE8|BAE8|1.100|7FFF|ea94c830e0569cdc
        world.Events.Add(52.59f, () => kefka_400040E9_4?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [52.59s] 264|400040E9|BAE8|0000857E|1|-0.015|-0.015|-0.015|0.000|400040E9|1804ae613042bad9
        // [55.17s] 271|400040E9|0.0000|00|00|100.0000|100.0000|0.0000|bbbbd005008951b4
        world.Events.Add(55.17f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [55.26s] 271|400040E9|-3.0609|00|00|100.0000|100.0000|0.0000|18952bd69265d641
        world.Events.Add(55.26f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.061f)));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|10066D86|ShieldHealer|750003|A34002|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|5765|10000|||99.71|91.55|-0.02|-2.01|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|0|8|00||01|BAEA|BAEA|1.100|034A|a260262402e630bb
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|100702A3|Player|750003|D6DD4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205207|205207|5300|10000|||98.98|91.66|-0.02|-0.14|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|1|8|00||01|BAEA|BAEA|1.100|034A|0f5aeb5ce1f5b8ff
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AC8F1|CasterDps|750003|8F1E4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||99.26|90.76|0.00|-1.93|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|2|8|00||01|BAEA|BAEA|1.100|034A|fca8831fcdc4caac
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|10018AEA|MeleeDpsB|750003|3ACD4002|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||99.69|90.80|-0.01|-0.04|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|3|8|00||01|BAEA|BAEA|1.100|034A|1642f33b18688fd4
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AF82E|MainTank|750003|435B4001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|275456|325133|8000|10000|||100.40|90.46|0.00|0.11|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|4|8|00||01|BAEA|BAEA|1.100|034A|b633baeccbccd6a3
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|100AE96C|RegenHealer|750003|2A254001|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|304662|325047|9200|10000|||98.99|90.53|0.00|-0.68|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|5|8|00||01|BAEA|BAEA|1.100|034A|2853198406ee9fe4
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|100A7A8F|OffTank|750003|1EBE4002|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||100.09|88.82|0.00|-0.26|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|6|8|00||01|BAEA|BAEA|1.100|034A|8e167cb788a82ec7
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [55.26s] 22|400040E9|Kefka|BAEA|Shocking Impact|1009061B|MeleeDpsA|750003|17D14002|E80E|B7D0000|1B|BAEA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|6840|10000|||100.05|88.00|0.00|0.02|44|44|0|10000|||100.00|100.00|0.00|0.00|0000859E|7|8|00||01|BAEA|BAEA|1.100|034A|238121fb685743ab
        world.Events.Add(55.26f, () => kefka_400040E9_4?.Cast(ActionId.ShockingImpact, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [55.26s] 264|400040E9|BAEA|0000859E|1|-0.015|-0.015|-0.015|-3.061|400040E9|16db318485ec9d6e
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|cc7971e0a108c590
        world.Events.Add(55.26f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AF82E|MainTank|00|325133|44|cdce4587707e46fe
        world.Events.Add(55.26f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AE96C|RegenHealer|00|325047|44|858fe018cb7fafa2
        world.Events.Add(55.26f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|65d997ffa3852541
        world.Events.Add(55.26f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|95ef3e52aedb2f5e
        world.Events.Add(55.26f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100702A3|Player|00|205207|44|4f97480b07531173
        world.Events.Add(55.26f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|61e79ed7832a3f05
        world.Events.Add(55.26f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.26s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|3faed11db0b48e54
        world.Events.Add(55.26f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [55.71s] 271|400040E9|-3.0603|00|00|100.0000|100.0000|0.0000|a6988b299b996635
        world.Events.Add(55.71f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.060f)));
        // [56.06s] 271|400040E9|-3.0107|00|00|100.0000|100.0000|0.0000|372b847f846508bf
        world.Events.Add(56.06f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.011f)));
        // [56.20s] 271|400040E9|-2.9935|00|00|100.0000|100.0000|0.0000|94039e6bcee25b32
        world.Events.Add(56.20f, () => kefka_400040E9_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -2.994f)));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|1009061B|MeleeDpsA|00|205177|44|e4452c86476e1d24
        world.Events.Add(57.22f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AF82E|MainTank|00|325133|44|e63f5f6a291a9acb
        world.Events.Add(57.22f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AE96C|RegenHealer|00|325047|44|f38e41336aa3b35d
        world.Events.Add(57.22f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10066D86|ShieldHealer|00|205177|44|9917cb9bf1af9781
        world.Events.Add(57.22f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|cef84302f61229f4
        world.Events.Add(57.22f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100702A3|Player|00|205207|44|a2446debe1445f8c
        world.Events.Add(57.22f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|73874865e3792688
        world.Events.Add(57.22f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [57.22s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|e07155fbddfd9b2f
        world.Events.Add(57.22f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [63.97s] 261|Change|400040E9|BNpcNameID|1E0B|Name|Chaos|PosY|104.0000|PosZ|0.0000|e644329cfc5e3169
    }

    private void Run_Black_Hole_40004177()
    {
        SimEnemy? black_Hole_40004177 = null;
        // [56.11s] 272|40004177|100AE96C|0054|00|1d616e89366a197b
        // [55.70s] 261|Add|40004177|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.5708|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|83.0000|PosY|100.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|e1867fce26c98b65
        // [55.70s] 03|40004177|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||83.00|100.00|0.00|1.57|61e2df91b4a214ea
        world.Events.Add(55.70f, () => black_Hole_40004177 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 1.570f))));
        // [57.98s] 35|40004177|Black Hole|1009061B|MeleeDpsA|0000|0000|0054|1009061B|000F|0000|5dd023ef358084bc
        world.Events.Add(57.98f, () => world.Tether(black_Hole_40004177!, party.Get(PartyRole.MeleeDpsA)!, TetherId.Tether0054));
        // [57.74s] 261|Change|40004177|ModelStatus|0|e232c01592163cb8
        world.Events.Add(57.74f, () => black_Hole_40004177?.SetVisible(true));
        // [57.74s] 261|Change|40004177|ModelStatus|0|08d8b4d6e0e21c3a
        world.Events.Add(57.74f, () => black_Hole_40004177?.SetVisible(true));
        // [62.75s] 271|40004177|2.2638|00|00|83.0000|100.0000|0.0000|4e8903648f7a2227
        world.Events.Add(62.75f, () => black_Hole_40004177?.SetPosition(new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 2.264f)));
        // [62.83s] 21|40004177|Black Hole|BAFC|Nothingness|1009061B|MeleeDpsA|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|157778|205177|7610|10000|||96.54|88.73|-0.02|0.37|188300|188300|10000|10000|||83.00|100.00|0.00|1.57|000085EF|0|1|00||01|BAFC|BAFC|1.100|DC3B|681b3f825662d3fc
        world.Events.Add(62.83f, () => black_Hole_40004177?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [62.83s] 264|40004177|BAFC|000085EF|0||||2.264|40004177|2fb9d84eee8c66be
        // [65.59s] 35|40004177|Black Hole|10018AEA|MeleeDpsB|0000|0000|0054|10018AEA|000F|0000|96db1a64051fc6b5
        world.Events.Add(65.59f, () => world.Tether(black_Hole_40004177!, party.Get(PartyRole.MeleeDpsB)!, TetherId.Tether0054));
        // [67.83s] 271|40004177|2.1913|00|00|83.0000|100.0000|0.0000|8eef4ed500fec3cd
        world.Events.Add(67.83f, () => black_Hole_40004177?.SetPosition(new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 2.191f)));
        // [67.92s] 21|40004177|Black Hole|BAFC|Nothingness|10018AEA|MeleeDpsB|750003|43FD0000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|191376|226668|10000|10000|||94.48|91.65|0.00|1.29|188300|188300|10000|10000|||83.00|100.00|0.00|2.19|00008624|0|1|00||01|BAFC|BAFC|1.100|D947|449bdc36182bd5f4
        world.Events.Add(67.92f, () => black_Hole_40004177?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [67.92s] 264|40004177|BAFC|00008624|0||||2.191|40004177|0a394a4d3a307358
        // [67.92s] 26|154C|Unbecoming|9999.00|40004177|Black Hole|10018AEA|MeleeDpsB|01|226668|188300|bb2851fd4c437851
        world.Events.Add(67.92f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.Unbecoming));
        // [72.91s] 271|40004177|2.1650|00|00|83.0000|100.0000|0.0000|1b6ccdcaeb55ea2d
        world.Events.Add(72.91f, () => black_Hole_40004177?.SetPosition(new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 2.165f)));
        // [73.00s] 21|40004177|Black Hole|BAFC|Nothingness|10018AEA|MeleeDpsB|750003|48FA0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|197681|226668|10000|10000|||95.32|91.63|0.00|0.62|188300|188300|10000|10000|||83.00|100.00|0.00|2.19|00008658|0|1|00||01|BAFC|BAFC|1.100|D835|616f30684714e968
        world.Events.Add(73.00f, () => black_Hole_40004177?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [73.00s] 264|40004177|BAFC|00008658|0||||2.165|40004177|4c57612840784398
        // [73.00s] 30|154C|Unbecoming|0.00|40004177|Black Hole|10018AEA|MeleeDpsB|01|226668|188300|f9ef3b9c3448da09
        world.Events.Add(73.00f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.Unbecoming));
        // [73.00s] 26|154D|Meanest Existence|9999.00|40004177|Black Hole|10018AEA|MeleeDpsB|00|226668|188300|07d4fbae07f39435
        world.Events.Add(73.00f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MeanestExistence));
        // [75.07s] 04|40004177|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||83.00|100.00|0.00|2.17|4ea33b8e89bc2d63
        world.Events.Add(75.07f, () => black_Hole_40004177?.Despawn());
        // [75.07s] 261|Remove|40004177|a0e0cb994c15f4fa
    }

    private void Run_Black_Hole_40004178()
    {
        SimEnemy? black_Hole_40004178 = null;
        // [56.11s] 272|40004178|10066D86|0054|00|08941ebae7a0977c
        // [55.70s] 261|Add|40004178|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-3.1416|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|117.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|382c3a6b36e5f7df
        // [55.70s] 03|40004178|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-3.14|09f6d592a3bd0ab7
        world.Events.Add(55.70f, () => black_Hole_40004178 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 17.000f), -3.140f))));
        // [56.20s] 35|40004178|Black Hole|100AC8F1|CasterDps|0000|0000|0054|100AC8F1|000F|0000|ed7d211e1b9f27c0
        world.Events.Add(56.20f, () => world.Tether(black_Hole_40004178!, party.Get(PartyRole.CasterDps)!, TetherId.Tether0054));
        // [56.28s] 35|40004178|Black Hole|10018AEA|MeleeDpsB|0000|0000|0054|10018AEA|000F|0000|88400d66f3e3494f
        world.Events.Add(56.28f, () => world.Tether(black_Hole_40004178!, party.Get(PartyRole.MeleeDpsB)!, TetherId.Tether0054));
        // [57.58s] 35|40004178|Black Hole|100AF82E|MainTank|0000|0000|0054|100AF82E|000F|0000|e674c99c63005800
        world.Events.Add(57.58f, () => world.Tether(black_Hole_40004178!, party.Get(PartyRole.MainTank)!, TetherId.Tether0054));
        // [57.16s] 261|Change|40004178|ModelStatus|0|ea2e709467399859
        world.Events.Add(57.16f, () => black_Hole_40004178?.SetVisible(true));
        // [57.62s] 35|40004178|Black Hole|100AC8F1|CasterDps|0000|0000|0054|100AC8F1|000F|0000|7f7c1242823336f7
        world.Events.Add(57.62f, () => world.Tether(black_Hole_40004178!, party.Get(PartyRole.CasterDps)!, TetherId.Tether0054));
        // [62.75s] 271|40004178|-2.6089|00|00|100.0000|117.0000|0.0000|16b8bb2677f95217
        world.Events.Add(62.75f, () => black_Hole_40004178?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 17.000f), -2.609f)));
        // [62.83s] 21|40004178|Black Hole|BAFC|Nothingness|100AC8F1|CasterDps|750003|47ED0000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||92.27|104.32|0.00|2.61|188300|188300|10000|10000|||100.00|117.00|0.00|-3.14|000085F0|0|1|00||01|BAFC|BAFC|1.100|15B4|1d04ed11eed2813d
        world.Events.Add(62.83f, () => black_Hole_40004178?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [62.83s] 264|40004178|BAFC|000085F0|0||||-2.609|40004178|d88fac20512ebc16
        // [62.84s] 26|154C|Unbecoming|9999.00|40004178|Black Hole|100AC8F1|CasterDps|01|227550|188300|d5bff7182b6bce5f
        world.Events.Add(62.84f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.Unbecoming));
        // [67.83s] 271|40004178|-2.6289|00|00|100.0000|117.0000|0.0000|f47fa39600019607
        world.Events.Add(67.83f, () => black_Hole_40004178?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 17.000f), -2.629f)));
        // [67.92s] 21|40004178|Black Hole|BAFC|Nothingness|100AC8F1|CasterDps|750003|44330000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|211301|227550|10000|10000|||92.64|103.93|0.00|2.18|188300|188300|10000|10000|||100.00|117.00|0.00|-2.63|00008625|0|1|00||01|BAFC|BAFC|1.100|14E3|9c8b3257aa49166d
        world.Events.Add(67.92f, () => black_Hole_40004178?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [67.92s] 264|40004178|BAFC|00008625|0||||-2.629|40004178|8a255007bdc0d1dd
        // [67.92s] 30|154C|Unbecoming|0.00|40004178|Black Hole|100AC8F1|CasterDps|01|227550|188300|5d6b1c4ad342726e
        world.Events.Add(67.92f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.Unbecoming));
        // [67.92s] 26|154D|Meanest Existence|9999.00|40004178|Black Hole|100AC8F1|CasterDps|00|227550|188300|48b8321c0e8ef0ff
        world.Events.Add(67.92f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MeanestExistence));
        // [73.00s] 21|40004178|Black Hole|BAFC|Nothingness|100AC8F1|CasterDps|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|184029|227550|10000|10000|||92.64|103.93|0.00|2.18|188300|188300|10000|10000|||100.00|117.00|0.00|-2.63|00008659|0|1|00||01|BAFC|BAFC|1.100|14E3|0f788cda69c08ea9
        world.Events.Add(73.00f, () => black_Hole_40004178?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [73.00s] 264|40004178|BAFC|00008659|0||||-2.629|40004178|68af396372d0c5c3
        // [75.07s] 04|40004178|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-2.63|9801985eba29c3dc
        world.Events.Add(75.07f, () => black_Hole_40004178?.Despawn());
        // [75.07s] 261|Remove|40004178|2d6abdca345574bb
    }

    private void Run_Black_Hole_40004179()
    {
        SimEnemy? black_Hole_40004179 = null;
        // [56.11s] 272|40004179|100A7A8F|0054|00|595c599703c2c7ab
        // [55.70s] 261|Add|40004179|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|83.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|cd2100c48008f7fb
        // [55.70s] 03|40004179|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|c8c3a9dba93cbe08
        world.Events.Add(55.70f, () => black_Hole_40004179 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.000f))));
        // [57.09s] 35|40004179|Black Hole|100702A3|Player|0000|0000|0054|100702A3|000F|0000|7533fa97c9132e6c
        world.Events.Add(57.09f, () => world.Tether(black_Hole_40004179!, party.Get(PartyRole.PhysRangedDps)!, TetherId.Tether0054));
        // [57.17s] 35|40004179|Black Hole|100A7A8F|OffTank|0000|0000|0054|100A7A8F|000F|0000|b4c0d66035d48007
        world.Events.Add(57.17f, () => world.Tether(black_Hole_40004179!, party.Get(PartyRole.OffTank)!, TetherId.Tether0054));
        // [56.75s] 261|Change|40004179|ModelStatus|0|319326882c51ad78
        world.Events.Add(56.75f, () => black_Hole_40004179?.SetVisible(true));
        // [57.80s] 35|40004179|Black Hole|10066D86|ShieldHealer|0000|0000|0054|10066D86|000F|0000|c4e971a43a06c16d
        world.Events.Add(57.80f, () => world.Tether(black_Hole_40004179!, party.Get(PartyRole.ShieldHealer)!, TetherId.Tether0054));
        // [62.75s] 271|40004179|0.5114|00|00|100.0000|83.0000|0.0000|e63dcadda0143f7d
        world.Events.Add(62.75f, () => black_Hole_40004179?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.511f)));
        // [62.83s] 21|40004179|Black Hole|BAFC|Nothingness|10066D86|ShieldHealer|750003|3E4E0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|176191|205177|7210|10000|||107.19|95.81|0.00|-1.58|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|000085F1|0|1|00||01|BAFC|BAFC|1.100|94D6|159bcc6d01966722
        world.Events.Add(62.83f, () => black_Hole_40004179?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [62.83s] 264|40004179|BAFC|000085F1|0||||0.511|40004179|c6d2d7e83a64a11d
        // [62.84s] 26|154D|Meanest Existence|9999.00|40004179|Black Hole|10066D86|ShieldHealer|00|205177|188300|e3c887a9bd4d645b
        world.Events.Add(62.84f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.MeanestExistence));
        // [67.83s] 271|40004179|0.4900|00|00|100.0000|83.0000|0.0000|0e363568550ca916
        world.Events.Add(67.83f, () => black_Hole_40004179?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.490f)));
        // [67.92s] 21|40004179|Black Hole|BAFC|Nothingness|10066D86|ShieldHealer|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|205177|205177|7240|10000|||106.93|95.98|0.00|-1.05|188300|188300|10000|10000|||100.00|83.00|0.00|0.49|00008626|0|1|00||01|BAFC|BAFC|1.100|93F6|3ca770dc946980ce
        world.Events.Add(67.92f, () => black_Hole_40004179?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [67.92s] 264|40004179|BAFC|00008626|0||||0.490|40004179|5c86e3cf0286749c
        // [69.79s] 35|40004179|Black Hole|100702A3|Player|0000|0000|0054|100702A3|000F|0000|49147788b203bcd7
        world.Events.Add(69.79f, () => world.Tether(black_Hole_40004179!, party.Get(PartyRole.PhysRangedDps)!, TetherId.Tether0054));
        // [72.91s] 271|40004179|0.5954|00|00|100.0000|83.0000|0.0000|67055cab4cf068e6
        world.Events.Add(72.91f, () => black_Hole_40004179?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.595f)));
        // [73.00s] 21|40004179|Black Hole|BAFC|Nothingness|100702A3|Player|750003|3F070000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|180155|205207|4900|10000|||107.68|94.35|0.00|-0.97|188300|188300|10000|10000|||100.00|83.00|0.00|0.49|0000865A|0|1|00||01|BAFC|BAFC|1.100|9842|afd1e015f574e8ff
        world.Events.Add(73.00f, () => black_Hole_40004179?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [73.00s] 264|40004179|BAFC|0000865A|0||||0.595|40004179|b76dd48de87572cf
        // [73.00s] 26|154C|Unbecoming|9999.00|40004179|Black Hole|100702A3|Player|01|205207|188300|3a161fb01dc2aebb
        world.Events.Add(73.00f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.Unbecoming));
        // [75.07s] 04|40004179|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.60|fb1187e70184fb74
        world.Events.Add(75.07f, () => black_Hole_40004179?.Despawn());
        // [75.07s] 261|Remove|40004179|4b43a85ae05c36ac
    }

    private void Run_Black_Hole_4000417A()
    {
        SimEnemy? black_Hole_4000417A = null;
        // [56.11s] 272|4000417A|E0000000|0000|00|88105209e95a22f1
        // [55.79s] 03|4000417A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|94.83|0.00|1.18|c3ddf3d6ac8ba702
        world.Events.Add(55.79f, () => black_Hole_4000417A = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-12.470f, 0.000f, -5.170f), 1.180f))));
        // [55.79s] 261|Add|4000417A|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.1781|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|87.5281|PosY|94.8343|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|d9275526522296d6
        // [55.79s] 261|Change|4000417A|ModelStatus|0|205535565fb06ad4
        world.Events.Add(55.79f, () => black_Hole_4000417A?.SetVisible(true));
        // [75.07s] 04|4000417A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|94.83|0.00|1.18|552a935db63ae6fd
        world.Events.Add(75.07f, () => black_Hole_4000417A?.Despawn());
        // [75.07s] 261|Remove|4000417A|ed197ffa3133e88a
    }

    private void Run_Black_Hole_4000417B()
    {
        SimEnemy? black_Hole_4000417B = null;
        // [56.11s] 272|4000417B|E0000000|0000|00|a20e16f6906ce4c8
        // [55.79s] 03|4000417B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|112.47|0.00|2.75|67a0235e126a8b93
        world.Events.Add(55.79f, () => black_Hole_4000417B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-5.170f, 0.000f, 12.470f), 2.750f))));
        // [55.79s] 261|Add|4000417B|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|2.7489|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|94.8343|PosY|112.4719|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|3ae88833203b5312
        // [55.79s] 261|Change|4000417B|ModelStatus|0|03310115a108ca29
        world.Events.Add(55.79f, () => black_Hole_4000417B?.SetVisible(true));
        // [75.07s] 04|4000417B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|112.47|0.00|2.75|ee4af5c1e1d9862c
        world.Events.Add(75.07f, () => black_Hole_4000417B?.Despawn());
        // [75.07s] 261|Remove|4000417B|648a5645901ea243
    }

    private void Run_Black_Hole_4000417C()
    {
        SimEnemy? black_Hole_4000417C = null;
        // [56.11s] 272|4000417C|E0000000|0000|00|1b62b8c519d7359e
        // [55.79s] 03|4000417C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|105.17|0.00|-1.96|61e3f9216932c5fa
        world.Events.Add(55.79f, () => black_Hole_4000417C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(12.470f, 0.000f, 5.170f), -1.960f))));
        // [55.79s] 261|Add|4000417C|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.9636|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|112.4719|PosY|105.1657|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|520311a7ccd2bf74
        // [55.79s] 261|Change|4000417C|ModelStatus|0|12efe03560f2162d
        world.Events.Add(55.79f, () => black_Hole_4000417C?.SetVisible(true));
        // [75.07s] 04|4000417C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|105.17|0.00|-1.96|fcb332eb0b719d6e
        world.Events.Add(75.07f, () => black_Hole_4000417C?.Despawn());
        // [75.07s] 261|Remove|4000417C|b4c2b3a1e37b27cc
    }

    private void Run_Black_Hole_4000417D()
    {
        SimEnemy? black_Hole_4000417D = null;
        // [56.11s] 272|4000417D|E0000000|0000|00|774b110c864f3159
        // [55.79s] 03|4000417D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|87.53|0.00|-0.39|c6f3c64fe47ff58f
        world.Events.Add(55.79f, () => black_Hole_4000417D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(5.170f, 0.000f, -12.470f), -0.390f))));
        // [55.79s] 261|Add|4000417D|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-0.3928|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|105.1657|PosY|87.5281|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|b2bc478a5331328e
        // [55.79s] 261|Change|4000417D|ModelStatus|0|4d9a772950c42ec0
        world.Events.Add(55.79f, () => black_Hole_4000417D?.SetVisible(true));
        // [75.07s] 04|4000417D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|87.53|0.00|-0.39|c7c8b6fe44149cd5
        world.Events.Add(75.07f, () => black_Hole_4000417D?.Despawn());
        // [75.07s] 261|Remove|4000417D|669468c8e7a02f32
    }

    private void Run_Black_Hole_4000417E()
    {
        SimEnemy? black_Hole_4000417E = null;
        // [56.11s] 272|4000417E|E0000000|0000|00|79914b456c45f039
        // [55.79s] 03|4000417E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||91.00|100.00|0.00|1.57|36dc7ba161b170bf
        world.Events.Add(55.79f, () => black_Hole_4000417E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.000f, 0.000f, 0.000f), 1.570f))));
        // [55.79s] 261|Add|4000417E|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.5708|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|91.0000|PosY|100.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|46099bcda89d0afa
        // [55.79s] 261|Change|4000417E|ModelStatus|0|cf5297e395369561
        world.Events.Add(55.79f, () => black_Hole_4000417E?.SetVisible(true));
        // [75.07s] 04|4000417E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||91.00|100.00|0.00|1.57|9b1e2f9b5db4cde8
        world.Events.Add(75.07f, () => black_Hole_4000417E?.Despawn());
        // [75.07s] 261|Remove|4000417E|2f9fc5a0320e5537
    }

    private void Run_Black_Hole_4000417F()
    {
        SimEnemy? black_Hole_4000417F = null;
        // [56.11s] 272|4000417F|E0000000|0000|00|3a65968170b0e2d0
        // [55.79s] 03|4000417F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|109.00|0.00|-3.14|c2d65b68e51e729b
        world.Events.Add(55.79f, () => black_Hole_4000417F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 9.000f), -3.140f))));
        // [55.79s] 261|Add|4000417F|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-3.1416|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|109.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|048dbe258346a9c3
        // [55.79s] 261|Change|4000417F|ModelStatus|0|1273234f938c2c4b
        world.Events.Add(55.79f, () => black_Hole_4000417F?.SetVisible(true));
        // [75.07s] 04|4000417F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|109.00|0.00|-3.14|f15064789c2fc14d
        world.Events.Add(75.07f, () => black_Hole_4000417F?.Despawn());
        // [75.07s] 261|Remove|4000417F|ce2cb073e8731f47
    }

    private void Run_Black_Hole_40004180()
    {
        SimEnemy? black_Hole_40004180 = null;
        // [56.11s] 272|40004180|E0000000|0000|00|68019b4197ae5509
        // [55.79s] 03|40004180|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||109.00|100.00|0.00|-1.57|8de20be8178206ab
        world.Events.Add(55.79f, () => black_Hole_40004180 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(9.000f, 0.000f, 0.000f), -1.570f))));
        // [55.79s] 261|Add|40004180|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.5709|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|109.0000|PosY|100.0000|Radius|1.0000|Type|2|WorldID|65535|b83b0c5604240d4a
        // [55.79s] 261|Change|40004180|ModelStatus|0|d2847af6b298aadf
        world.Events.Add(55.79f, () => black_Hole_40004180?.SetVisible(true));
        // [75.07s] 04|40004180|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||109.00|100.00|0.00|-1.57|0f8cad3bab6e6d28
        world.Events.Add(75.07f, () => black_Hole_40004180?.Despawn());
        // [75.07s] 261|Remove|40004180|aa293fff0b3af8dc
    }

    private void Run_Black_Hole_40004181()
    {
        SimEnemy? black_Hole_40004181 = null;
        // [56.11s] 272|40004181|E0000000|0000|00|3de0cb32acc21f1e
        // [55.79s] 03|40004181|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|91.00|0.00|0.00|45d37dac4e7be81f
        world.Events.Add(55.79f, () => black_Hole_40004181 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -9.000f), 0.000f))));
        // [55.79s] 261|Add|40004181|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|91.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|5522652a29293214
        // [55.79s] 261|Change|40004181|ModelStatus|0|21917f5fc0902517
        world.Events.Add(55.79f, () => black_Hole_40004181?.SetVisible(true));
        // [75.07s] 04|40004181|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|91.00|0.00|0.00|ee68b829e0c19a72
        world.Events.Add(75.07f, () => black_Hole_40004181?.Despawn());
        // [75.07s] 261|Remove|40004181|b3c277c1d36fc0ca
    }

    private void Run_Chaos_400040E9_5()
    {
        SimEnemy? chaos_400040E9_5 = null;
        // [64.30s] 271|400040E9|0.0000|00|00|100.0000|104.0000|0.0000|aea29c431f5c4cac
        world.Events.Add(64.30f, () => chaos_400040E9_5?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        // [64.06s] 03|400040E9|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|7fb12caee07dda16
        world.Events.Add(64.06f, () => chaos_400040E9_5 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|B39D0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|314077|325133|2400|10000|||101.09|101.55|0.00|-2.86|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|0|7|00||01|BAFA|BAFA|1.100|7FFF|b8c8e0c8d5498c69
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|DDC4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|171880|217488|10000|10000|||100.30|100.45|0.00|-2.66|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|1|7|00||01|BAFA|BAFA|1.100|7FFF|f445ca15e461de08
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|A8A30000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|8200|10000|||101.24|100.57|-0.02|-2.65|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|2|7|00||01|BAFA|BAFA|1.100|7FFF|1f6d90291877bcd7
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450003|FF950000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|185476|205207|4300|10000|||98.44|98.07|0.00|1.44|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|3|7|00||01|BAFA|BAFA|1.100|7FFF|eccc450cbfb75ce5
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|E9470000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|197577|226668|10000|10000|||99.66|98.15|0.00|-2.92|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|4|7|00||01|BAFA|BAFA|1.100|7FFF|c053d24acd856f6b
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|1C984001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|209137|227550|10000|10000|||92.48|103.93|0.00|1.87|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|5|7|00||01|BAFA|BAFA|1.100|7FFF|074e915923b5a3e8
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [64.39s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450003|890E0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|160241|205177|7210|10000|||106.88|95.99|-0.01|-1.06|44|44|0|10000|||100.00|104.00|0.00|0.00|000085FC|6|7|00||01|BAFA|BAFA|1.100|7FFF|bf38da2778ae1998
        world.Events.Add(64.39f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [64.39s] 264|400040E9|BAFA|000085FC|1|-0.015|-0.015|-0.015|0.000|400040E9|b9e1ba9a7ebdb70c
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|1a3e025f3d3bc424
        world.Events.Add(64.39f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|246e814b3cd99aed
        world.Events.Add(64.39f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|fb9591b55271a368
        world.Events.Add(64.39f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|4e453a4f3b39cdca
        world.Events.Add(64.39f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|2135e192f7120716
        world.Events.Add(64.39f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|2bff4a55caafa39d
        world.Events.Add(64.39f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [64.39s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|472b3ac9f6c8c78e
        world.Events.Add(64.39f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|eee34cad39877ba2
        world.Events.Add(66.35f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|0fabc874c0f9948d
        world.Events.Add(66.35f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|071c29816012b021
        world.Events.Add(66.35f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|16d709f8cf1b8638
        world.Events.Add(66.35f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|4db1b5a2ae01cb81
        world.Events.Add(66.35f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|aa26fca9286eafba
        world.Events.Add(66.35f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [66.35s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|b7c02620d376cd3c
        world.Events.Add(66.35f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|C1640000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325133|325133|3600|10000|||102.04|101.88|0.00|-2.65|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|0|7|00||01|BAFA|BAFA|1.100|7FFF|dc4645db649a8b7d
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|15484001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|175596|217488|10000|10000|||100.60|100.43|0.00|-2.39|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|1|7|00||01|BAFA|BAFA|1.100|7FFF|d84aa26c8fadb859
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|BB9F0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|6600|10000|||101.24|100.57|-0.02|-2.16|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|2|7|00||01|BAFA|BAFA|1.100|7FFF|cd421318cdf6b50c
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|AAB4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|192373|205177|8215|10000|||99.27|98.61|-0.02|0.76|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|3|7|00||01|BAFA|BAFA|1.100|7FFF|fc1d2868be92ec64
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|38174001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205605|227550|10000|10000|||92.64|103.93|0.00|2.18|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|4|7|00||01|BAFA|BAFA|1.100|7FFF|95596d1eaebc109d
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450003|FE200000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|185749|205207|4700|10000|||102.85|94.30|0.00|2.06|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|5|7|00||01|BAFA|BAFA|1.100|7FFF|b884331476470e32
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [69.48s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|484A4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|185589|226668|10000|10000|||95.32|91.63|0.00|0.62|44|44|0|10000|||100.00|104.00|0.00|0.00|00008635|6|7|00||01|BAFA|BAFA|1.100|7FFF|26bba15f759ebef7
        world.Events.Add(69.48f, () => chaos_400040E9_5?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [69.48s] 264|400040E9|BAFA|00008635|1|-0.015|-0.015|-0.015|0.000|400040E9|669596fb70fa173c
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|b7a1efc99d522b7a
        world.Events.Add(69.48f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|58d203841089ab98
        world.Events.Add(69.48f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|ae1c2254a07b91fb
        world.Events.Add(69.48f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|4049aebea286e487
        world.Events.Add(69.48f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|9525610cc00bc502
        world.Events.Add(69.48f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|2fa89709cb5f9d13
        world.Events.Add(69.48f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [69.48s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|e89c26960db5453e
        world.Events.Add(69.48f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|7f389e20c9b9370a
        world.Events.Add(71.44f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|8b0893ca5fe60916
        world.Events.Add(71.44f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|655df280ec29f97b
        world.Events.Add(71.44f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|424345d395e14e9f
        world.Events.Add(71.44f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|2c1761943f7d62e3
        world.Events.Add(71.44f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|8f2f0ddeb2856234
        world.Events.Add(71.44f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [71.44s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|79fd4e65724068cc
        world.Events.Add(71.44f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [73.93s] 261|Change|400040E9|BNpcNameID|1BDB|CastBuffID|47854|CastDurationCurrent|0.0130|CastDurationMax|4.7000|CastTargetID|400040E9|Heading|0.7854|IsCasting1|1|IsCasting2|1|Name|Kefka|PosY|100.0000|PosZ|0.0000|a2353f5f6ec4ff6d
    }

    private void Run_Chaos_400040E8_3()
    {
        SimEnemy? chaos_400040E8_3 = null;
        // [74.13s] 03|400040E8|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|ef387aa9dc453b1e
        world.Events.Add(74.13f, () => chaos_400040E8_3 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
        // [74.13s] 261|Change|400040E8|BNpcNameID|1E0B|Heading|0.0000|Name|Chaos|PosX|100.0000|PosY|104.0000|PosZ|0.0000|012bdce482d44d65
        // [74.48s] 271|400040E8|0.0000|00|00|100.0000|104.0000|0.0000|35f6a12214d6f60f
        world.Events.Add(74.48f, () => chaos_400040E8_3?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450003|F9800000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|91819|225694|6870|10000|||101.18|101.17|-0.01|-1.85|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|0|7|00||01|BAFA|BAFA|1.100|7FFF|8764b23bfc7497fd
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|AEE00000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325133|325133|3800|10000|||101.88|101.70|0.00|-2.65|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|1|7|00||01|BAFA|BAFA|1.100|7FFF|8aad511cc6e330f2
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|23ED4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|173485|217488|10000|10000|||101.58|100.11|0.00|-2.58|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|2|7|00||01|BAFA|BAFA|1.100|7FFF|f737c49f197820e5
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|A8830000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|8000|10000|||101.80|99.84|0.00|2.70|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|3|7|00||01|BAFA|BAFA|1.100|7FFF|e9c020015b358656
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|FA210000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|192910|205177|9600|10000|||99.50|98.63|0.00|3.06|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|4|7|00||01|BAFA|BAFA|1.100|7FFF|ee7ed3894930d4e6
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|1E704001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|187693|226668|10000|10000|||95.45|92.51|0.00|-0.17|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|5|7|00||01|BAFA|BAFA|1.100|7FFF|7e62f6c84995f390
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [74.57s] 22|400040E8|Chaos|BAFA|Earthquake|100702A3|Player|450003|3BE4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|179982|205207|5100|10000|||107.68|94.35|0.00|-0.97|44|44|0|10000|||100.00|104.00|0.00|0.00|00008665|6|7|00||01|BAFA|BAFA|1.100|7FFF|79fcf8bc109bf139
        world.Events.Add(74.57f, () => chaos_400040E8_3?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [74.57s] 264|400040E8|BAFA|00008665|1|-0.015|-0.015|-0.015|0.000|400040E8|d078cb4a7efb06b8
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|1009061B|MeleeDpsA|00|205177|44|424510d8032fbf30
        world.Events.Add(74.57f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|100AF82E|MainTank|00|325133|44|564c248da7913b07
        world.Events.Add(74.57f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|100AE96C|RegenHealer|00|325047|44|7718f6ba0a6944e3
        world.Events.Add(74.57f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|10066D86|ShieldHealer|00|225694|44|fb75c4523d0ed9a5
        world.Events.Add(74.57f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|100A7A8F|OffTank|00|217488|44|dafb02aadbd986c5
        world.Events.Add(74.57f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|100702A3|Player|00|205207|44|7fa4177b0063a3ed
        world.Events.Add(74.57f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [74.57s] 26|D2C|Earth Resistance Down II|1.96|400040E8|Chaos|10018AEA|MeleeDpsB|00|226668|44|fa3bf480da1a822e
        world.Events.Add(74.57f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|1009061B|MeleeDpsA|00|205177|44|b5b09b1a216cc18e
        world.Events.Add(76.53f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|100AF82E|MainTank|00|325133|44|8f1024087331e53e
        world.Events.Add(76.53f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|100AE96C|RegenHealer|00|325047|44|8130748117f068ff
        world.Events.Add(76.53f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|10066D86|ShieldHealer|00|225694|44|8b68a130e704f7a3
        world.Events.Add(76.53f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|100A7A8F|OffTank|00|217488|44|e592b2f545e76ddc
        world.Events.Add(76.53f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|100702A3|Player|00|205207|44|1e4b1bac14f9d302
        world.Events.Add(76.53f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [76.53s] 30|D2C|Earth Resistance Down II|0.00|400040E8|Chaos|10018AEA|MeleeDpsB|00|226668|44|f86a3f87ffa2dfda
        world.Events.Add(76.53f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [114.53s] 261|Change|400040E8|BNpcNameID|1BDB|Heading|-0.7855|Name|Kefka|PosX|92.9289|PosY|107.0711|PosZ|0.0000|948dbd2b7d6e5a19
    }

    private void Run_Kefka_400040E9_6()
    {
        SimEnemy? kefka_400040E9_6 = null;
        // [74.25s] 271|400040E9|0.7854|00|00|100.0000|100.0000|0.0000|7f91390cee26d41c
        world.Events.Add(74.25f, () => kefka_400040E9_6?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.785f)));
        // [74.29s] 20|400040E9|Chaos|BAEE|Look upon Me and Despair|400040E9|Chaos|4.700|100.00|104.00|0.00|0.00|896c33d2b24e6943
        world.Events.Add(74.29f, () => kefka_400040E9_6?.Cast(ActionId.LookUponMeAndDespair_BAEE, targetLocation: new Vector3(-35.378f, -0.015f, -35.378f), castSeconds: 4.700f, targetId: kefka_400040E9_6?.GameObjectId));
        // [74.02s] 03|400040E9|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|100.00|0.00|0.79|4b372183d54723c3
        world.Events.Add(74.02f, () => kefka_400040E9_6 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.790f))));
        // [79.25s] 21|400040E9|Kefka|BAEE|Look upon Me and Despair|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.79|00008687|0|0|00||01|BAEE|BAEE|1.100|9FFF|278237f0b6f24bec
        // [79.25s] 264|400040E9|BAEE|00008687|1|-0.015|-0.015|-0.015|0.785|400040E9|6e088124e9db380c
        // [78.98s] 261|Change|400040E9|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|984a4c7236993ee6
    }

    private void Run_Exdeath_400040E9_7()
    {
        SimEnemy? exdeath_400040E9_7 = null;
        // [83.41s] 261|Change|400040E9|BNpcNameID|17A4|Name|Exdeath|3505f9b692f3a5da
        // [83.41s] 03|400040E9|Exdeath|00|1|0000|00||6052|9020|44|44|0|10000|||100.00|100.00|0.00|0.79|4ab7e0be899e705e
        world.Events.Add(83.41f, () => exdeath_400040E9_7 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.790f))));
        // [83.94s] 21|400040E9|Exdeath|BB0C|Thunder III|100AE96C|RegenHealer|EC550205|2FB04002|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|325047|325047|8800|10000|||112.96|94.74|0.00|1.35|44|44|0|10000|||100.00|100.00|0.00|0.79|000086B4|0|1|00||01|BB0C|BB0C|1.100|9FFF|a3d33e6eb3e7646a
        world.Events.Add(83.94f, () => exdeath_400040E9_7?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [83.94s] 264|400040E9|BB0C|000086B4|0||||0.785|100AE96C|5b334c4cb61595f4
        // [83.94s] 26|BB6|Lightning Resistance Down II|3.96|400040E9|Exdeath|100AE96C|RegenHealer|00|325047|44|ba8fa8f450ac31d1
        world.Events.Add(83.94f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
        // [86.97s] 21|400040E9|Exdeath|BB0C|Thunder III|100AF82E|MainTank|550003|D7EE4001|FF0E|BB60000|1B|BB0C8000|0|0|0|0|0|0|0|0|0|0|322625|325133|2200|10000|||109.84|96.48|0.00|-1.39|44|44|0|10000|||100.00|100.00|0.00|0.79|000086CF|0|1|00||01|BB0C|BB0C|1.100|9FFF|f293b8556fe99cbe
        world.Events.Add(86.97f, () => exdeath_400040E9_7?.Cast(ActionId.ThunderIII_BB0C, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [86.97s] 264|400040E9|BB0C|000086CF|0||||0.785|100AF82E|b4230bd3e000d397
        // [86.97s] 26|BB6|Lightning Resistance Down II|3.96|400040E9|Exdeath|100AF82E|MainTank|00|325133|44|b6cb609588caf0b9
        world.Events.Add(86.97f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.LightningResistanceDownII, 3.960f));
        // [87.90s] 30|BB6|Lightning Resistance Down II|0.00|400040E9|Exdeath|100AE96C|RegenHealer|00|325047|44|c9755b2093b466e6
        world.Events.Add(87.90f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.LightningResistanceDownII));
        // [90.93s] 30|BB6|Lightning Resistance Down II|0.00|400040E9|Exdeath|100AF82E|MainTank|00|325133|44|1b00b09a92c23f94
        world.Events.Add(90.93f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.LightningResistanceDownII));
    }

    private void Run_Black_Hole_40004185()
    {
        SimEnemy? black_Hole_40004185 = null;
        // [90.22s] 272|40004185|100AE96C|0054|00|4d6d89e38717d022
        // [89.95s] 03|40004185|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||83.00|100.00|0.00|1.57|f66e448ee94aa015
        world.Events.Add(89.95f, () => black_Hole_40004185 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 1.570f))));
        // [89.95s] 261|Add|40004185|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.5708|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|83.0000|PosY|100.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|10798dd1faf1f6c7
        // [89.95s] 261|Change|40004185|ModelStatus|0|92a62e350686ae57
        world.Events.Add(89.95f, () => black_Hole_40004185?.SetVisible(true));
        // [97.03s] 271|40004185|2.3360|00|00|83.0000|100.0000|0.0000|03d78ba20e0c83d5
        world.Events.Add(97.03f, () => black_Hole_40004185?.SetPosition(new Placement(new Vector3(-17.000f, 0.000f, 0.000f), 2.336f)));
        // [97.12s] 21|40004185|Black Hole|BAFC|Nothingness|100AE96C|RegenHealer|750003|26F10000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||92.42|90.93|0.00|0.23|188300|188300|10000|10000|||83.00|100.00|0.00|2.34|00008719|0|1|00||01|BAFC|BAFC|1.100|DF2D|99e52207fa5b4d8d
        world.Events.Add(97.12f, () => black_Hole_40004185?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [97.12s] 264|40004185|BAFC|00008719|0||||2.336|40004185|7c46eabd0d88208c
        // [97.12s] 26|154C|Unbecoming|9999.00|40004185|Black Hole|100AE96C|RegenHealer|01|325047|188300|23b7442c4fd8f395
        world.Events.Add(97.12f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.Unbecoming));
        // [102.21s] 21|40004185|Black Hole|BAFC|Nothingness|100AE96C|RegenHealer|750003|303D0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||92.42|90.93|0.00|0.23|188300|188300|10000|10000|||83.00|100.00|0.00|2.34|00008740|0|1|00||01|BAFC|BAFC|1.100|DF2D|714e86c667f25d9a
        world.Events.Add(102.21f, () => black_Hole_40004185?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [102.21s] 264|40004185|BAFC|00008740|0||||2.336|40004185|f5eae903e33272f9
        // [102.21s] 26|154D|Meanest Existence|9999.00|40004185|Black Hole|100AE96C|RegenHealer|00|325047|188300|256530f30f58d4d3
        world.Events.Add(102.21f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MeanestExistence));
        // [102.21s] 30|154C|Unbecoming|0.00|40004185|Black Hole|100AE96C|RegenHealer|01|325047|188300|bf8c93d1c6b5d65a
        world.Events.Add(102.21f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.Unbecoming));
        // [107.31s] 21|40004185|Black Hole|BAFC|Nothingness|100AE96C|RegenHealer|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||92.42|90.93|0.00|0.23|188300|188300|10000|10000|||83.00|100.00|0.00|2.34|0000876A|0|1|00||01|BAFC|BAFC|1.100|DF2D|ba95e2383b39757b
        world.Events.Add(107.31f, () => black_Hole_40004185?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [107.31s] 264|40004185|BAFC|0000876A|0||||2.336|40004185|99068c65d274ca42
        // [109.12s] 04|40004185|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||83.00|100.00|0.00|2.34|e628332f05a0fe90
        world.Events.Add(109.12f, () => black_Hole_40004185?.Despawn());
        // [109.12s] 261|Remove|40004185|bbaeac58ea5af594
    }

    private void Run_Black_Hole_40004186()
    {
        SimEnemy? black_Hole_40004186 = null;
        // [90.22s] 272|40004186|100A7A8F|0054|00|177439ddbbd894e3
        // [89.95s] 03|40004186|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-1.57|b366df7727e5cac0
        world.Events.Add(89.95f, () => black_Hole_40004186 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.570f))));
        // [89.95s] 261|Add|40004186|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.5709|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|117.0000|PosY|100.0000|Radius|1.0000|Type|2|WorldID|65535|5dbb67459d6d4fdb
        // [90.04s] 261|Change|40004186|ModelStatus|0|80dd7a212ce32984
        world.Events.Add(90.04f, () => black_Hole_40004186?.SetVisible(true));
        // [91.87s] 35|40004186|Black Hole|100702A3|Player|0000|0000|0054|100702A3|000F|0000|088136ac3a4dcd6e
        world.Events.Add(91.87f, () => world.Tether(black_Hole_40004186!, party.Get(PartyRole.PhysRangedDps)!, TetherId.Tether0054));
        // [97.03s] 271|40004186|-1.0477|00|00|117.0000|100.0000|0.0000|2f8ac6f60fe3651e
        world.Events.Add(97.03f, () => black_Hole_40004186?.SetPosition(new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.048f)));
        // [97.12s] 21|40004186|Black Hole|BAFC|Nothingness|100702A3|Player|750003|0|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|205207|205207|5600|10000|||102.10|108.57|0.00|-2.52|188300|188300|10000|10000|||117.00|100.00|0.00|-1.05|0000871A|0|1|00||01|BAFC|BAFC|1.100|5550|e982394bcdb175c0
        world.Events.Add(97.12f, () => black_Hole_40004186?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [97.12s] 264|40004186|BAFC|0000871A|0||||-1.048|40004186|f67a7ba6e5ce2e9a
        // [97.12s] 26|154D|Meanest Existence|9999.00|40004186|Black Hole|100702A3|Player|00|205207|188300|f0f18ed0a76d0af9
        world.Events.Add(97.12f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MeanestExistence));
        // [102.12s] 271|40004186|-1.0746|00|00|117.0000|100.0000|0.0000|4155ed8ba78c862c
        world.Events.Add(102.12f, () => black_Hole_40004186?.SetPosition(new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.075f)));
        // [102.26s] 21|40004186|Black Hole|BAFC|Nothingness|100702A3|Player|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|205207|205207|5400|10000|||101.84|108.14|0.00|-2.57|188300|188300|10000|10000|||117.00|100.00|0.00|-1.07|00008743|0|1|00||01|BAFC|BAFC|1.100|53C1|2ed9e9727d006060
        world.Events.Add(102.26f, () => black_Hole_40004186?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [102.26s] 264|40004186|BAFC|00008743|0||||-1.086|40004186|f7e81ef4d48615d7
        // [104.49s] 35|40004186|Black Hole|100A7A8F|OffTank|0000|0000|0054|100A7A8F|000F|0000|44434e53b4628d66
        world.Events.Add(104.49f, () => world.Tether(black_Hole_40004186!, party.Get(PartyRole.OffTank)!, TetherId.Tether0054));
        // [107.26s] 271|40004186|-1.1463|00|00|117.0000|100.0000|0.0000|aba33c45f33a9f04
        world.Events.Add(107.26f, () => black_Hole_40004186?.SetPosition(new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.146f)));
        // [107.35s] 21|40004186|Black Hole|BAFC|Nothingness|100A7A8F|OffTank|750003|4C880000|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||101.28|106.85|0.00|-2.57|188300|188300|10000|10000|||117.00|100.00|0.00|-1.07|0000876C|0|1|00||01|BAFC|BAFC|1.100|514B|c536773b5b89a246
        world.Events.Add(107.35f, () => black_Hole_40004186?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [107.35s] 264|40004186|BAFC|0000876C|0||||-1.146|40004186|5bc9e5ec5de5aed7
        // [107.35s] 26|154C|Unbecoming|9999.00|40004186|Black Hole|100A7A8F|OffTank|01|217488|188300|224d8ef8f09e7150
        world.Events.Add(107.35f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.Unbecoming));
        // [109.12s] 04|40004186|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-1.15|a38a0a934d0d7f50
        world.Events.Add(109.12f, () => black_Hole_40004186?.Despawn());
        // [109.12s] 261|Remove|40004186|b861571181a2820d
    }

    private void Run_Black_Hole_40004187()
    {
        SimEnemy? black_Hole_40004187 = null;
        // [90.22s] 272|40004187|1009061B|0054|00|d50964bf32446213
        // [89.95s] 03|40004187|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|c14885da6798a6fa
        world.Events.Add(89.95f, () => black_Hole_40004187 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.000f))));
        // [89.95s] 261|Add|40004187|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|83.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|ffc520c2aadc2c72
        // [89.95s] 261|Change|40004187|ModelStatus|0|176a1dda69604389
        world.Events.Add(89.95f, () => black_Hole_40004187?.SetVisible(true));
        // [90.80s] 35|40004187|Black Hole|100AC8F1|CasterDps|0000|0000|0054|100AC8F1|000F|0000|378c89952ceaeb53
        world.Events.Add(90.80f, () => world.Tether(black_Hole_40004187!, party.Get(PartyRole.CasterDps)!, TetherId.Tether0054));
        // [90.85s] 35|40004187|Black Hole|10018AEA|MeleeDpsB|0000|0000|0054|10018AEA|000F|0000|8d6a9b73abf250ff
        world.Events.Add(90.85f, () => world.Tether(black_Hole_40004187!, party.Get(PartyRole.MeleeDpsB)!, TetherId.Tether0054));
        // [97.03s] 271|40004187|0.4774|00|00|100.0000|83.0000|0.0000|af5da613d8e88124
        world.Events.Add(97.03f, () => black_Hole_40004187?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.477f)));
        // [97.12s] 21|40004187|Black Hole|BAFC|Nothingness|10018AEA|MeleeDpsB|603|9BBC4097|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||107.87|98.25|0.00|2.12|188300|188300|10000|10000|||100.00|83.00|0.00|0.48|0000871B|0|1|00||01|BAFC|BAFC|1.100|9373|91bede2513b05e87
        world.Events.Add(97.12f, () => black_Hole_40004187?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [97.12s] 264|40004187|BAFC|0000871B|0||||0.477|40004187|9a3f9616a6822770
        // [98.64s] 35|40004187|Black Hole|100AF82E|MainTank|0000|0000|0054|100AF82E|000F|0000|77383310d690eccc
        world.Events.Add(98.64f, () => world.Tether(black_Hole_40004187!, party.Get(PartyRole.MainTank)!, TetherId.Tether0054));
        // [102.12s] 271|40004187|0.5336|00|00|100.0000|83.0000|0.0000|43aa3018743f3151
        world.Events.Add(102.12f, () => black_Hole_40004187?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.534f)));
        // [102.21s] 21|40004187|Black Hole|BAFC|Nothingness|100AF82E|MainTank|750003|0|100000E|154C0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|325133|325133|4600|10000|||108.23|96.94|0.00|-1.12|188300|188300|10000|10000|||100.00|83.00|0.00|0.53|00008741|0|1|00||01|BAFC|BAFC|1.100|95BD|cb9033cb16cfdbec
        world.Events.Add(102.21f, () => black_Hole_40004187?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [102.21s] 264|40004187|BAFC|00008741|0||||0.534|40004187|35b656f2fd9bdcfa
        // [102.21s] 26|154C|Unbecoming|9999.00|40004187|Black Hole|100AF82E|MainTank|01|325133|188300|2533083d688ecdf0
        world.Events.Add(102.21f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.Unbecoming));
        // [107.22s] 271|40004187|0.5407|00|00|100.0000|83.0000|0.0000|cbe3c2e555e101e4
        world.Events.Add(107.22f, () => black_Hole_40004187?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.541f)));
        // [107.31s] 21|40004187|Black Hole|BAFC|Nothingness|100AF82E|MainTank|750003|14260000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|5400|10000|||108.45|97.06|0.00|-1.14|188300|188300|10000|10000|||100.00|83.00|0.00|0.54|0000876B|0|1|00||01|BAFC|BAFC|1.100|9607|22e30963ba2869b7
        world.Events.Add(107.31f, () => black_Hole_40004187?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [107.31s] 264|40004187|BAFC|0000876B|0||||0.541|40004187|c506ec0cfb56fd5f
        // [107.31s] 30|154C|Unbecoming|0.00|40004187|Black Hole|100AF82E|MainTank|01|325133|188300|76fe2aceb977f330
        world.Events.Add(107.31f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.Unbecoming));
        // [107.31s] 26|154D|Meanest Existence|9999.00|40004187|Black Hole|100AF82E|MainTank|00|325133|188300|2b4d24fccb480b92
        world.Events.Add(107.31f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MeanestExistence));
        // [109.12s] 04|40004187|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.54|fdb5c3e6a52ac168
        world.Events.Add(109.12f, () => black_Hole_40004187?.Despawn());
        // [109.12s] 261|Remove|40004187|b25f8c99a50d3573
    }

    private void Run_Black_Hole_40004188()
    {
        SimEnemy? black_Hole_40004188 = null;
        // [90.22s] 272|40004188|E0000000|0000|00|39dde18f96c5d5a7
        // [89.95s] 03|40004188|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|87.53|0.00|0.39|8f3839a6030e9436
        world.Events.Add(89.95f, () => black_Hole_40004188 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-5.170f, 0.000f, -12.470f), 0.390f))));
        // [89.95s] 261|Add|40004188|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.3927|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|94.8343|PosY|87.5281|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|0f360e0f3ea6f782
        // [89.95s] 261|Change|40004188|ModelStatus|0|dea69acef6758280
        world.Events.Add(89.95f, () => black_Hole_40004188?.SetVisible(true));
        // [109.12s] 04|40004188|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|87.53|0.00|0.39|8c56435dacec60b9
        world.Events.Add(109.12f, () => black_Hole_40004188?.Despawn());
        // [109.12s] 261|Remove|40004188|d148dcbc868db2bd
    }

    private void Run_Black_Hole_40004189()
    {
        SimEnemy? black_Hole_40004189 = null;
        // [90.22s] 272|40004189|E0000000|0000|00|98be814dde2e6e68
        // [89.95s] 03|40004189|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|105.17|0.00|1.96|b6b4712e9dca1b8b
        world.Events.Add(89.95f, () => black_Hole_40004189 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-12.470f, 0.000f, 5.170f), 1.960f))));
        // [89.95s] 261|Add|40004189|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.9635|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|87.5281|PosY|105.1657|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|d43efe8b76c04d35
        // [89.95s] 261|Change|40004189|ModelStatus|0|8aaf8425af33e844
        world.Events.Add(89.95f, () => black_Hole_40004189?.SetVisible(true));
        // [109.12s] 04|40004189|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|105.17|0.00|1.96|df328e3fc0453562
        world.Events.Add(109.12f, () => black_Hole_40004189?.Despawn());
        // [109.12s] 261|Remove|40004189|a92fff59c19b3489
    }

    private void Run_Black_Hole_4000418A()
    {
        SimEnemy? black_Hole_4000418A = null;
        // [90.22s] 272|4000418A|E0000000|0000|00|132076a54bee2d8a
        // [89.95s] 03|4000418A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|112.47|0.00|-2.75|86bf8aa81c1fb9a4
        world.Events.Add(89.95f, () => black_Hole_4000418A = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(5.170f, 0.000f, 12.470f), -2.750f))));
        // [89.95s] 261|Add|4000418A|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-2.7490|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|105.1657|PosY|112.4719|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|82bedc3fd4bb590e
        // [89.95s] 261|Change|4000418A|ModelStatus|0|2b3b25a53dc2a7ab
        world.Events.Add(89.95f, () => black_Hole_4000418A?.SetVisible(true));
        // [109.12s] 04|4000418A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|112.47|0.00|-2.75|1b69b96ef33b3f5f
        world.Events.Add(109.12f, () => black_Hole_4000418A?.Despawn());
        // [109.12s] 261|Remove|4000418A|a9ef256593f4bfeb
    }

    private void Run_Black_Hole_4000418B()
    {
        SimEnemy? black_Hole_4000418B = null;
        // [90.22s] 272|4000418B|E0000000|0000|00|f68218ac12d887fb
        // [89.95s] 03|4000418B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|94.83|0.00|-1.18|7f023100e5e9c372
        world.Events.Add(89.95f, () => black_Hole_4000418B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(12.470f, 0.000f, -5.170f), -1.180f))));
        // [89.95s] 261|Add|4000418B|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.1782|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|112.4719|PosY|94.8343|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|30f8d9822e971fcb
        // [89.95s] 261|Change|4000418B|ModelStatus|0|1057d68a8220639c
        world.Events.Add(89.95f, () => black_Hole_4000418B?.SetVisible(true));
        // [109.12s] 04|4000418B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|94.83|0.00|-1.18|c4daa2cd9496e4b2
        world.Events.Add(109.12f, () => black_Hole_4000418B?.Despawn());
        // [109.12s] 261|Remove|4000418B|b3d133edc49d1ca6
    }

    private void Run_Black_Hole_4000418C()
    {
        SimEnemy? black_Hole_4000418C = null;
        // [90.22s] 272|4000418C|E0000000|0000|00|a6215eb6a749f1d2
        // [89.95s] 03|4000418C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|93.64|0.00|0.79|a79a8eccefb3d3dc
        world.Events.Add(89.95f, () => black_Hole_4000418C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-6.360f, 0.000f, -6.360f), 0.790f))));
        // [89.95s] 261|Add|4000418C|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.7854|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|93.6369|PosY|93.6369|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|d273a4780b4cbafb
        // [89.95s] 261|Change|4000418C|ModelStatus|0|7b0caebe179fa1dd
        world.Events.Add(89.95f, () => black_Hole_4000418C?.SetVisible(true));
        // [109.12s] 04|4000418C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|93.64|0.00|0.79|7ed8bb93dd5bc0b5
        world.Events.Add(109.12f, () => black_Hole_4000418C?.Despawn());
        // [109.12s] 261|Remove|4000418C|05159ec07bcc27b1
    }

    private void Run_Black_Hole_4000418D()
    {
        SimEnemy? black_Hole_4000418D = null;
        // [90.22s] 272|4000418D|E0000000|0000|00|8a9edfbd40b9477f
        // [89.95s] 03|4000418D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|106.36|0.00|2.36|d43eb85238a12515
        world.Events.Add(89.95f, () => black_Hole_4000418D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-6.360f, 0.000f, 6.360f), 2.360f))));
        // [89.95s] 261|Add|4000418D|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|2.3562|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|93.6369|PosY|106.3631|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|617d83215132a69e
        // [89.95s] 261|Change|4000418D|ModelStatus|0|a6248cbb67c556a5
        world.Events.Add(89.95f, () => black_Hole_4000418D?.SetVisible(true));
        // [109.12s] 04|4000418D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||93.64|106.36|0.00|2.36|edb70de4b7015a35
        world.Events.Add(109.12f, () => black_Hole_4000418D?.Despawn());
        // [109.12s] 261|Remove|4000418D|d5ba2b5a65eb291a
    }

    private void Run_Black_Hole_4000418E()
    {
        SimEnemy? black_Hole_4000418E = null;
        // [90.22s] 272|4000418E|E0000000|0000|00|8050c3e81566203c
        // [89.95s] 03|4000418E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|106.36|0.00|-2.36|fa649407dd91515b
        world.Events.Add(89.95f, () => black_Hole_4000418E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(6.360f, 0.000f, 6.360f), -2.360f))));
        // [89.95s] 261|Add|4000418E|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-2.3563|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|106.3631|PosY|106.3631|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|3b0ffb0ff0dc02c5
        // [89.95s] 261|Change|4000418E|ModelStatus|0|a4b5e71636af2c48
        world.Events.Add(89.95f, () => black_Hole_4000418E?.SetVisible(true));
        // [109.12s] 04|4000418E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|106.36|0.00|-2.36|006ff3d2fb9267d2
        world.Events.Add(109.12f, () => black_Hole_4000418E?.Despawn());
        // [109.12s] 261|Remove|4000418E|dda506f28db89874
    }

    private void Run_Black_Hole_4000418F()
    {
        SimEnemy? black_Hole_4000418F = null;
        // [90.22s] 272|4000418F|E0000000|0000|00|c0efb488e90fb31c
        // [89.95s] 03|4000418F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|93.64|0.00|-0.79|8d5e9f875b057006
        world.Events.Add(89.95f, () => black_Hole_4000418F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(6.360f, 0.000f, -6.360f), -0.790f))));
        // [89.95s] 261|Add|4000418F|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-0.7855|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|106.3631|PosY|93.6369|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|c2242d92af9d7dca
        // [90.04s] 261|Change|4000418F|ModelStatus|0|9887bbbbc67d3990
        world.Events.Add(90.04f, () => black_Hole_4000418F?.SetVisible(true));
        // [109.12s] 04|4000418F|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||106.36|93.64|0.00|-0.79|c8f50de2976a6c94
        world.Events.Add(109.12f, () => black_Hole_4000418F?.Despawn());
        // [109.12s] 261|Remove|4000418F|d15f6012adb0d45b
    }

    private void Run_Chaos_400040E9_8()
    {
        SimEnemy? chaos_400040E9_8 = null;
        // [98.60s] 271|400040E9|0.0000|00|00|100.0000|104.0000|0.0000|f400bfb9bb14122e
        world.Events.Add(98.60f, () => chaos_400040E9_8?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        // [98.24s] 261|Change|400040E9|BNpcNameID|1E0B|Heading|0.0000|Name|Chaos|PosY|104.0000|PosZ|0.0000|e3927634dea5b4eb
        // [98.24s] 03|400040E9|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|77a2713d0be55e1e
        world.Events.Add(98.24f, () => chaos_400040E9_8 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450603|46330000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|6980|10000|||100.54|101.30|0.00|-2.03|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|0|7|00||01|BAFA|BAFA|1.100|7FFF|88cd96d0f183d35c
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450603|3DF70000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||99.31|100.74|0.00|-2.18|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|1|7|00||01|BAFA|BAFA|1.100|7FFF|5fd091292a94895f
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450603|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|8440|10000|||100.12|100.55|0.00|1.89|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|2|7|00||01|BAFA|BAFA|1.100|7FFF|59844fb7ceeac9e3
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450603|7B0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||100.14|100.57|0.00|-0.56|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|3|7|00||01|BAFA|BAFA|1.100|7FFF|e4ae8303d7f80395
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450603|3C5B0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205207|205207|5800|10000|||102.10|108.57|0.00|-2.52|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|4|7|00||01|BAFA|BAFA|1.100|7FFF|127064ad1ee88b56
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|279572|325133|4200|10000|||106.32|96.85|0.00|1.73|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|5|7|00||01|BAFA|BAFA|1.100|7FFF|971bbe33256903c9
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [98.69s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|EC450005|7CE80000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|10000|10000|||92.42|90.93|0.00|0.23|44|44|0|10000|||100.00|104.00|0.00|0.00|00008722|6|7|00||01|BAFA|BAFA|1.100|7FFF|b846afddccfc721a
        world.Events.Add(98.69f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [98.69s] 264|400040E9|BAFA|00008722|1|-0.015|-0.015|-0.015|0.000|400040E9|57666d9ecd134a05
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|2cecbafc5c2f12d1
        world.Events.Add(98.69f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|18f170f6504cae15
        world.Events.Add(98.69f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|8bf805429b81ff3e
        world.Events.Add(98.69f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|f8d433e2c81a76c6
        world.Events.Add(98.69f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|b7371b600b339686
        world.Events.Add(98.69f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|8dbf53e719e5aaa4
        world.Events.Add(98.69f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [98.69s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|32e2a9abe8849403
        world.Events.Add(98.69f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|bea9540dc9f4f5a1
        world.Events.Add(100.65f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|18ef1444afb9acf4
        world.Events.Add(100.65f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|2c1f02a413546026
        world.Events.Add(100.65f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|3967127ad2ae23d5
        world.Events.Add(100.65f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|57a56f280ade76a8
        world.Events.Add(100.65f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|36685affbad77d2c
        world.Events.Add(100.65f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [100.65s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|c1f1a6efe38b1d1b
        world.Events.Add(100.65f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|43EC4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||101.59|103.63|0.00|0.49|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|0|7|00||01|BAFA|BAFA|1.100|7FFF|d87204a84b41444a
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|3F7E4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|6650|10000|||100.54|101.30|0.00|2.90|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|1|7|00||01|BAFA|BAFA|1.100|7FFF|2b157c1468691c56
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|E4390000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||100.09|100.86|-0.02|1.65|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|2|7|00||01|BAFA|BAFA|1.100|7FFF|09f87c6ee93dfad0
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|51B24001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|172415|226668|10000|10000|||100.66|100.82|0.00|-3.06|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|3|7|00||01|BAFA|BAFA|1.100|7FFF|a8894486fda9cf58
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450603|F75D0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|8070|10000|||100.33|100.63|0.00|-2.09|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|4|7|00||01|BAFA|BAFA|1.100|7FFF|5ab997c462086140
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|0|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325133|325133|4600|10000|||108.45|97.06|0.00|-0.89|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|5|7|00||01|BAFA|BAFA|1.100|7FFF|5813ebe506cadb3d
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [103.82s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|D07C0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|315948|325047|10000|10000|||92.42|90.93|0.00|0.23|44|44|0|10000|||100.00|104.00|0.00|0.00|00008752|6|7|00||01|BAFA|BAFA|1.100|7FFF|62981ccb497e9686
        world.Events.Add(103.82f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [103.82s] 264|400040E9|BAFA|00008752|1|-0.015|-0.015|-0.015|0.000|400040E9|601e3569895276c9
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|510c209d2b51bd3c
        world.Events.Add(103.82f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|e45c686f6104079c
        world.Events.Add(103.82f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|e6e0571c99d8c22e
        world.Events.Add(103.82f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|21474314ec9b0917
        world.Events.Add(103.82f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|09a39b979ab185e5
        world.Events.Add(103.82f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|a5f2113c705ba973
        world.Events.Add(103.82f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [103.82s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|e719a86d7d1770a7
        world.Events.Add(103.82f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|adc1dfc5c976d3c5
        world.Events.Add(105.79f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|19cb390a159d8e3c
        world.Events.Add(105.79f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|342647d9fce119fa
        world.Events.Add(105.79f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|b039bd543417d0b6
        world.Events.Add(105.79f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|25fef534f45e3f00
        world.Events.Add(105.79f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|5ee1a3aa49a31b04
        world.Events.Add(105.79f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [105.79s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|5276593e1b189cc5
        world.Events.Add(105.79f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|15AE4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|6320|10000|||100.54|101.30|0.00|2.90|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|0|7|00||01|BAFA|BAFA|1.100|7FFF|44e2a5ad4a2afe0c
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|29C14001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||100.66|106.62|0.00|-2.39|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|1|7|00||01|BAFA|BAFA|1.100|7FFF|10bedd87aa1fccb6
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|277B4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||100.66|100.82|0.00|2.89|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|2|7|00||01|BAFA|BAFA|1.100|7FFF|647f1e302c1b1b3b
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450003|8374001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|181607|205207|5200|10000|||100.69|100.66|0.00|-2.21|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|3|7|00||01|BAFA|BAFA|1.100|7FFF|163a55e3b0f666cd
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|37734001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||101.13|100.73|0.00|-2.59|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|4|7|00||01|BAFA|BAFA|1.100|7FFF|ac0d4edd4bc9d1ec
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450003|11114001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|7185|10000|||100.39|100.45|0.00|-1.95|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|5|7|00||01|BAFA|BAFA|1.100|7FFF|abf804ae84ada342
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [108.87s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|B4E30000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325133|325133|5600|10000|||108.45|97.06|0.00|-1.14|44|44|0|10000|||100.00|104.00|0.00|0.00|00008776|6|7|00||01|BAFA|BAFA|1.100|7FFF|530e9c050d68ecd7
        world.Events.Add(108.87f, () => chaos_400040E9_8?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [108.87s] 264|400040E9|BAFA|00008776|1|-0.015|-0.015|-0.015|0.000|400040E9|f87826024f675781
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|f2873a4e30f98409
        world.Events.Add(108.87f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|fd7f145fff13c18a
        world.Events.Add(108.87f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|34c5ca8c57a80994
        world.Events.Add(108.87f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|1d4696ccf22b73ff
        world.Events.Add(108.87f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|84b709cff154b862
        world.Events.Add(108.87f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|89dc67b3658292f6
        world.Events.Add(108.87f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [108.87s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|6c1155f6a2621671
        world.Events.Add(108.87f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|5fb26f0dc1ca7912
        world.Events.Add(110.84f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|0db7e947d77396be
        world.Events.Add(110.84f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|75646dfd6b2af697
        world.Events.Add(110.84f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|28178ef2ed6ff96f
        world.Events.Add(110.84f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|0ddc1877c2c6054b
        world.Events.Add(110.84f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|77882df710b3d0fb
        world.Events.Add(110.84f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [110.84s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|cf26fda015fecf56
        world.Events.Add(110.84f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [114.53s] 261|Change|400040E9|BNpcNameID|1BDB|Heading|-0.7855|Name|Kefka|PosX|100.0000|PosY|114.1421|PosZ|0.0000|2c4405e5650e32c2
    }

    private void Run_Kefka_400040E8_4()
    {
        SimEnemy? kefka_400040E8_4 = null;
        // [114.81s] 271|400040E8|-0.7855|00|00|92.9289|107.0711|0.0000|139546752386ec4b
        world.Events.Add(114.81f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(-7.071f, 0.000f, 7.071f), -0.785f)));
        // [114.64s] 03|400040E8|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||92.93|107.07|0.00|-0.79|718afa36a7cbf9e7
        world.Events.Add(114.64f, () => kefka_400040E8_4 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-7.070f, 0.000f, 7.070f), -0.790f))));
        // [121.20s] 21|400040E8|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||92.93|107.07|0.00|-0.79|000087D3|0|0|00||01|BAE8|BAE8|1.100|5FFF|8d1328227c444a07
        world.Events.Add(121.20f, () => kefka_400040E8_4?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [121.20s] 264|400040E8|BAE8|000087D3|1|-0.015|-0.015|-0.015|-0.785|400040E8|a5892f489049725a
        // [123.16s] 271|400040E8|0.0000|00|00|100.0000|100.0000|0.0000|df247d026a41859b
        world.Events.Add(123.16f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [123.25s] 22|400040E8|Kefka|BAEB|Shockwave|100AE96C|RegenHealer|750603|1E954001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|325047|325047|8200|10000|||106.75|103.59|0.00|-2.51|44|44|0|10000|||92.93|107.07|0.00|-0.79|000087E1|0|2|00||01|BAEB|BAEB|1.100|AB40|46d78930058c7ffd
        world.Events.Add(123.25f, () => kefka_400040E8_4?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [123.25s] 22|400040E8|Kefka|BAEB|Shockwave|100AF82E|MainTank|750603|D2B20000|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|325133|325133|6200|10000|||108.51|100.48|0.00|0.75|44|44|0|10000|||92.93|107.07|0.00|-0.79|000087E1|1|2|00||01|BAEB|BAEB|1.100|AB40|b56dcb83559ec592
        world.Events.Add(123.25f, () => kefka_400040E8_4?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [123.25s] 271|400040E8|1.0616|00|00|100.0000|100.0000|0.0000|f40ee9db7b1032f1
        world.Events.Add(123.25f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 1.062f)));
        // [123.25s] 264|400040E8|BAEB|000087E1|1|-0.015|-0.015|-0.015|1.062|400040E8|e2bc73612a531ff8
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Kefka|100AF82E|MainTank|00|325133|44|f9b7a476a794f035
        world.Events.Add(123.25f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E8|Kefka|100AE96C|RegenHealer|00|325047|44|588fc562b6fe84e6
        world.Events.Add(123.25f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.29s] 271|400040E8|0.8528|00|00|100.0000|100.0000|0.0000|e47c19bbc68b48ba
        world.Events.Add(123.29f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.853f)));
        // [123.34s] 271|400040E8|0.8489|00|00|100.0000|100.0000|0.0000|4b5858f8a1c48e9a
        world.Events.Add(123.34f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.849f)));
        // [123.38s] 271|400040E8|0.8469|00|00|100.0000|100.0000|0.0000|c28ad6856e1b5ad7
        world.Events.Add(123.38f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.847f)));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Kefka|100AF82E|MainTank|00|325133|44|f1de03248a249dbb
        world.Events.Add(125.21f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E8|Kefka|100AE96C|RegenHealer|00|325047|44|4783b969c48de4de
        world.Events.Add(125.21f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [132.02s] 261|Change|400040E8|CastBuffID|47854|CastDurationCurrent|0.0160|CastDurationMax|4.7000|CastTargetID|400040E8|Heading|0.7854|IsCasting1|1|IsCasting2|1|9da82f90f8ad7457
        // [132.26s] 271|400040E8|0.7854|00|00|100.0000|100.0000|0.0000|4809035d01e56bfc
        world.Events.Add(132.26f, () => kefka_400040E8_4?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.785f)));
        // [132.35s] 20|400040E8|Kefka|BAEE|Look upon Me and Despair|400040E8|Kefka|4.700|100.00|100.00|0.00|0.85|e7cc3cdf5f784152
        world.Events.Add(132.35f, () => kefka_400040E8_4?.Cast(ActionId.LookUponMeAndDespair_BAEE, targetLocation: new Vector3(-35.378f, -0.015f, -35.378f), castSeconds: 4.700f, targetId: kefka_400040E8_4?.GameObjectId));
        // [137.31s] 21|400040E8|Kefka|BAEE|Look upon Me and Despair|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|100.00|0.00|0.79|00008843|0|0|00||01|BAEE|BAEE|1.100|9FFF|5587d7adb36fa9a0
        // [137.31s] 264|400040E8|BAEE|00008843|1|-0.015|-0.015|-0.015|0.785|400040E8|a06a302f676c3915
        // [137.05s] 261|Change|400040E8|CastBuffID|0|CastDurationCurrent|0.0000|CastDurationMax|0.0000|CastTargetID|E0000000|IsCasting1|0|IsCasting2|0|dbe4bef40400ac3b
    }

    private void Run_Kefka_400040E9_9()
    {
        SimEnemy? kefka_400040E9_9 = null;
        // [114.81s] 271|400040E9|-0.7855|00|00|100.0000|114.1421|0.0000|f12dce175da6de58
        world.Events.Add(114.81f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 14.142f), -0.785f)));
        // [114.64s] 03|400040E9|Kefka|00|1|0000|00||7131|9020|44|44|0|10000|||100.00|114.14|0.00|-0.79|07b3dfa2ae360bd8
        world.Events.Add(114.64f, () => kefka_400040E9_9 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Kefka, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 14.140f), -0.790f))));
        // [120.57s] 21|400040E9|Kefka|BAE8|Slap Happy|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.00|114.14|0.00|-0.79|000087CD|0|0|00||01|BAE8|BAE8|1.100|5FFF|2a2e709c0f466d7d
        world.Events.Add(120.57f, () => kefka_400040E9_9?.Cast(ActionId.SlapHappy_BAE8, castSeconds: 0f));
        // [120.57s] 264|400040E9|BAE8|000087CD|1|-0.015|-0.015|-0.015|-0.785|400040E9|3576bf0511d24de6
        // [123.16s] 271|400040E9|0.0000|00|00|100.0000|100.0000|0.0000|acecb7ea741abd58
        world.Events.Add(123.16f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f)));
        // [123.25s] 271|400040E9|-3.1370|00|00|100.0000|100.0000|0.0000|448e05ea3e48ffe4
        world.Events.Add(123.25f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.137f)));
        // [123.25s] 22|400040E9|Kefka|BAEB|Shockwave|10018AEA|MeleeDpsB|750603|9B804002|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|182422|226668|10000|10000|||97.62|92.62|0.00|-0.65|44|44|0|10000|||100.00|114.14|0.00|-0.79|000087E2|0|4|00||01|BAEB|BAEB|1.100|0030|dcd2badc8738a8f2
        world.Events.Add(123.25f, () => kefka_400040E9_9?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [123.25s] 22|400040E9|Kefka|BAEB|Shockwave|100AC8F1|CasterDps|750603|E7084001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||100.23|92.09|0.00|-1.68|44|44|0|10000|||100.00|114.14|0.00|-0.79|000087E2|1|4|00||01|BAEB|BAEB|1.100|0030|d01144ad18b093eb
        world.Events.Add(123.25f, () => kefka_400040E9_9?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [123.25s] 22|400040E9|Kefka|BAEB|Shockwave|100702A3|Player|750603|B4034001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|205207|205207|5900|10000|||100.34|89.39|0.00|0.01|44|44|0|10000|||100.00|114.14|0.00|-0.79|000087E2|2|4|00||01|BAEB|BAEB|1.100|0030|98961c5cef3df0d8
        world.Events.Add(123.25f, () => kefka_400040E9_9?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [123.25s] 22|400040E9|Kefka|BAEB|Shockwave|100A7A8F|OffTank|750603|F5204001|E80E|B7D0000|1B|BAEB8000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||99.94|88.07|0.00|0.35|44|44|0|10000|||100.00|114.14|0.00|-0.79|000087E2|3|4|00||01|BAEB|BAEB|1.100|0030|4c302bcd0f7e4e65
        world.Events.Add(123.25f, () => kefka_400040E9_9?.Cast(ActionId.Shockwave_BAEB, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [123.25s] 264|400040E9|BAEB|000087E2|1|-0.015|-0.015|-0.015|-3.137|400040E9|a1c6ce4cafd18a92
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|48e8b6eb45892a78
        world.Events.Add(123.25f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100702A3|Player|00|205207|44|347ef67c8aa97420
        world.Events.Add(123.25f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|ce5635d6a40d825e
        world.Events.Add(123.25f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.25s] 26|B7D|Magic Vulnerability Up|1.96|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|e38b8cd1292bd59a
        world.Events.Add(123.25f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.MagicVulnerabilityUp, 1.960f));
        // [123.83s] 271|400040E9|-3.1362|00|00|100.0000|100.0000|0.0000|b53edc6ac6b734f8
        world.Events.Add(123.83f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.136f)));
        // [124.10s] 271|400040E9|-3.1348|00|00|100.0000|100.0000|0.0000|854020635825096a
        world.Events.Add(124.10f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.135f)));
        // [124.14s] 271|400040E9|-3.0866|00|00|100.0000|100.0000|0.0000|4e035212a455f28c
        world.Events.Add(124.14f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.087f)));
        // [124.27s] 271|400040E9|-3.0857|00|00|100.0000|100.0000|0.0000|b28b7ba2a34fa4a6
        world.Events.Add(124.27f, () => kefka_400040E9_9?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), -3.086f)));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100A7A8F|OffTank|00|217488|44|594be2a256d96883
        world.Events.Add(125.21f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100702A3|Player|00|205207|44|594e2c0cd8859286
        world.Events.Add(125.21f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|10018AEA|MeleeDpsB|00|226668|44|5642662183a4687f
        world.Events.Add(125.21f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [125.21s] 30|B7D|Magic Vulnerability Up|0.00|400040E9|Kefka|100AC8F1|CasterDps|00|227550|44|572ee87a4802d118
        world.Events.Add(125.21f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.MagicVulnerabilityUp));
        // [131.73s] 261|Change|400040E9|BNpcNameID|1E0B|Heading|0.0000|Name|Chaos|PosY|104.0000|PosZ|0.0000|525b2d6f1d54b55c
    }

    private void Run_Chaos_400040E4_0()
    {
        SimEnemy? chaos_400040E4_0 = null;
        // [-1158.08s] 261|Add|400040E4|BNpcID|233C|BNpcNameID|1BDB|CastTargetID|E0000000|CurrentWorldID|65535|Heading|0.0000|Level|1|MaxHP|44|ModelStatus|2048|Name|Kefka|NPCTargetID|E0000000|PosX|100.0000|PosY|90.0000|PosZ|0.0000|Radius|0.5000|Type|2|WorldID|65535|0f19a39e5471ed11
        // [-34.92s] 271|400040E4|0.0000|00|00|90.1005|109.8995|0.0000|181f777f06427460
        // world.Events.Add(-34.92f, () => chaos_400040E4_0?.SetPosition(new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f)));
        // [-35.12s] 03|400040E4|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||90.10|109.90|0.00|0.00|159ad92962700f79
        world.Events.Add(0f, () => chaos_400040E4_0 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.900f, 0.000f, 9.900f), 0.000f))));
        // [-34.83s] 22|400040E4|Chaos|BAF8|Cyclone|10066D86|ShieldHealer|350603|D8D4001|FF000014|41C0000|1B|BAF88000|0|0|0|0|0|0|0|0|0|0|205177|205177|7915|10000|||107.29|105.24|0.00|-1.65|44|44|0|10000|||90.10|109.90|0.00|0.00|000082A3|0|2|00||01|BAF8|BAF8|1.100|7FFF|7d2e44ea4a7d0c9c
        // world.Events.Add(-34.83f, () => chaos_400040E4_0?.Cast(ActionId.Cyclone, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [-34.83s] 22|400040E4|Chaos|BAF8|Cyclone|100A7A8F|OffTank|350603|E14E0000|FF000014|41C0000|1B|BAF88000|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||107.41|104.80|0.00|-0.51|44|44|0|10000|||90.10|109.90|0.00|0.00|000082A3|1|2|00||01|BAF8|BAF8|1.100|7FFF|2f641a85aa07404d
        // world.Events.Add(-34.83f, () => chaos_400040E4_0?.Cast(ActionId.Cyclone, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [-34.83s] 264|400040E4|BAF8|000082A3|1|-0.015|-0.015|-0.015|0.000|10066D86|1b1e413dc5b9c15b
        // [119.00s] 271|400040E4|-0.0169|00|00|100.7376|100.6464|0.2000|4fb02458333c025a
        world.Events.Add(119.00f, () => chaos_400040E4_0?.SetPosition(new Placement(new Vector3(0.738f, 0.200f, 0.646f), -0.017f)));
        // [119.09s] 21|400040E4|Chaos|BAFF|Shockwave|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.74|100.65|0.00|-0.02|000087BE|0|0|00||01|BAFF|BAFF|1.100|7F4F|a671863d6a40991c
        world.Events.Add(119.09f, () => chaos_400040E4_0?.Cast(ActionId.Shockwave, castSeconds: 0f));
        // [119.09s] 264|400040E4|BAFF|000087BE|1|-0.015|-0.015|-0.015|-0.017|400040E4|2789a6534e52b3f7
        // [121.01s] 271|400040E4|-1.5876|00|00|100.7376|100.6464|0.2000|d2c072feaef25508
        world.Events.Add(121.01f, () => chaos_400040E4_0?.SetPosition(new Placement(new Vector3(0.738f, 0.200f, 0.646f), -1.588f)));
        // [121.11s] 21|400040E4|Chaos|BAFF|Shockwave|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.74|100.65|0.00|-0.02|000087D0|0|0|00||01|BAFF|BAFF|1.100|3F50|46d685fff3410032
        world.Events.Add(121.11f, () => chaos_400040E4_0?.Cast(ActionId.Shockwave, castSeconds: 0f));
        // [121.11s] 264|400040E4|BAFF|000087D0|1|-0.015|-0.015|-0.015|-1.588|400040E4|2bf80f4ca8dc69e6
    }

    private void Run_Chaos_400040E5_2()
    {
        SimEnemy? chaos_400040E5_2 = null;
        // [119.00s] 271|400040E5|3.1247|00|00|100.7376|100.6464|0.2000|48a298dd0f4c5275
        world.Events.Add(119.00f, () => chaos_400040E5_2?.SetPosition(new Placement(new Vector3(0.738f, 0.200f, 0.646f), 3.125f)));
        // [118.74s] 03|400040E5|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.74|100.65|0.00|3.12|8cfce036c17349dd
        world.Events.Add(118.74f, () => chaos_400040E5_2 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.740f, 0.000f, 0.650f), 3.120f))));
        // [119.09s] 21|400040E5|Chaos|BAFF|Shockwave|10018AEA|MeleeDpsB|750003|44CF4002|A60E|B5F0000|1B|BAFF8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||101.27|97.52|0.00|2.35|44|44|0|10000|||100.74|100.65|0.00|3.12|000087BF|0|1|00||01|BAFF|BAFF|1.100|FF4F|e36ea130322500db
        world.Events.Add(119.09f, () => chaos_400040E5_2?.Cast(ActionId.Shockwave, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [119.09s] 264|400040E5|BAFF|000087BF|0||||3.125|400040E5|771dabe3bfb06067
        // [119.59s] 26|B5F|Damage Down|180.00|400040E5|Chaos|10018AEA|MeleeDpsB|00|226668|44|4a5254ac85f62815
        world.Events.Add(119.59f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.DamageDown, 180.000f));
        // [121.01s] 271|400040E5|1.5539|00|00|100.7376|100.6464|0.2000|13453899e238e01e
        world.Events.Add(121.01f, () => chaos_400040E5_2?.SetPosition(new Placement(new Vector3(0.738f, 0.200f, 0.646f), 1.554f)));
        // [121.11s] 21|400040E5|Chaos|BAFF|Shockwave|E0000000||0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|||||||||||44|44|0|10000|||100.74|100.65|0.00|3.12|000087D1|0|0|00||01|BAFF|BAFF|1.100|BF4F|c0c6838d35e6b108
        world.Events.Add(121.11f, () => chaos_400040E5_2?.Cast(ActionId.Shockwave, castSeconds: 0f));
        // [121.11s] 264|400040E5|BAFF|000087D1|1|-0.015|-0.015|-0.015|1.554|400040E5|7735abcec7f6a027
    }

    private void Run_Black_Hole_40004194()
    {
        SimEnemy? black_Hole_40004194 = null;
        // [123.56s] 272|40004194|E0000000|0000|00|62ed753d35b0b165
        // [123.34s] 03|40004194|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-3.14|6acaa4f2e5214482
        world.Events.Add(123.34f, () => black_Hole_40004194 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 17.000f), -3.140f))));
        // [123.34s] 261|Add|40004194|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-3.1416|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|117.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|20e8d446a8a23948
        // [123.34s] 261|Change|40004194|ModelStatus|0|1436fc20f9ac2b8f
        world.Events.Add(123.34f, () => black_Hole_40004194?.SetVisible(true));
        // [130.60s] 35|40004194|Black Hole|100702A3|Player|0000|0000|0054|100702A3|000F|0000|d96ac2577944ea05
        world.Events.Add(130.60f, () => world.Tether(black_Hole_40004194!, party.Get(PartyRole.PhysRangedDps)!, TetherId.Tether0054));
        // [130.60s] 35|40004194|Black Hole|100AE96C|RegenHealer|0000|0000|0054|100AE96C|000F|0000|0a891572cb295b1d
        world.Events.Add(130.60f, () => world.Tether(black_Hole_40004194!, party.Get(PartyRole.RegenHealer)!, TetherId.Tether0054));
        // [133.64s] 35|40004194|Black Hole|100A7A8F|OffTank|0000|0000|0054|100A7A8F|000F|0000|f1337fd536a36c40
        world.Events.Add(133.64f, () => world.Tether(black_Hole_40004194!, party.Get(PartyRole.OffTank)!, TetherId.Tether0054));
        // [137.58s] 271|40004194|-1.3522|00|00|100.0000|117.0000|0.0000|c98b02d27423ecb7
        world.Events.Add(137.58f, () => black_Hole_40004194?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 17.000f), -1.352f)));
        // [137.67s] 21|40004194|Black Hole|BAFC|Nothingness|100A7A8F|OffTank|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|130725|217488|10000|10000|||95.96|117.90|0.00|2.62|188300|188300|10000|10000|||100.00|117.00|0.00|-1.35|00008846|0|1|00||01|BAFC|BAFC|1.100|48E8|94b8496c80f27fdd
        world.Events.Add(137.67f, () => black_Hole_40004194?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [137.67s] 264|40004194|BAFC|00008846|0||||-1.352|40004194|6a58b467cd8b983d
        // [139.83s] 04|40004194|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|117.00|0.00|-1.35|0d143ba748fbf5c5
        world.Events.Add(139.83f, () => black_Hole_40004194?.Despawn());
        // [139.83s] 261|Remove|40004194|d8ba1337a8c90bcc
    }

    private void Run_Black_Hole_40004195()
    {
        SimEnemy? black_Hole_40004195 = null;
        // [123.56s] 272|40004195|100702A3|0054|00|54d4bf43bbed027e
        // [123.34s] 03|40004195|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-1.57|be36f19b136be9a3
        world.Events.Add(123.34f, () => black_Hole_40004195 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(17.000f, 0.000f, 0.000f), -1.570f))));
        // [123.34s] 261|Add|40004195|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.5709|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|117.0000|PosY|100.0000|Radius|1.0000|Type|2|WorldID|65535|246a16b0780390a3
        // [123.34s] 261|Change|40004195|ModelStatus|0|0ca6dd470060bb46
        world.Events.Add(123.34f, () => black_Hole_40004195?.SetVisible(true));
        // [125.92s] 35|40004195|Black Hole|100AF82E|MainTank|0000|0000|0054|100AF82E|000F|0000|d117122f58f86851
        world.Events.Add(125.92f, () => world.Tether(black_Hole_40004195!, party.Get(PartyRole.MainTank)!, TetherId.Tether0054));
        // [126.59s] 35|40004195|Black Hole|100A7A8F|OffTank|0000|0000|0054|100A7A8F|000F|0000|39b62fd0e1278648
        world.Events.Add(126.59f, () => world.Tether(black_Hole_40004195!, party.Get(PartyRole.OffTank)!, TetherId.Tether0054));
        // [130.51s] 271|40004195|-0.8794|00|00|117.0000|100.0000|0.0000|32f1751bbdcddb60
        world.Events.Add(130.51f, () => black_Hole_40004195?.SetPosition(new Placement(new Vector3(17.000f, 0.000f, 0.000f), -0.879f)));
        // [130.60s] 21|40004195|Black Hole|BAFC|Nothingness|100A7A8F|OffTank|750003|501A0000|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|217488|217488|10000|10000|||107.17|108.13|0.00|-1.20|188300|188300|10000|10000|||117.00|100.00|0.00|-1.57|0000881A|0|1|00||01|BAFC|BAFC|1.100|5C2B|1b1e728e2b8c4993
        world.Events.Add(130.60f, () => black_Hole_40004195?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [130.60s] 264|40004195|BAFC|0000881A|0||||-0.879|40004195|50392281d3f615b7
        // [130.60s] 26|154D|Meanest Existence|9999.00|40004195|Black Hole|100A7A8F|OffTank|00|217488|188300|72ce9f9f423b2e87
        world.Events.Add(130.60f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.MeanestExistence));
        // [132.58s] 04|40004195|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||117.00|100.00|0.00|-0.88|ac334e2ee816d1df
        world.Events.Add(132.58f, () => black_Hole_40004195?.Despawn());
        // [132.58s] 261|Remove|40004195|0eeafb0fdc4f5d08
    }

    private void Run_Black_Hole_40004196()
    {
        SimEnemy? black_Hole_40004196 = null;
        // [123.56s] 272|40004196|1009061B|0054|00|c36a14e577d0ffda
        // [123.34s] 03|40004196|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|101f4a2307487c90
        world.Events.Add(123.34f, () => black_Hole_40004196 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.000f))));
        // [123.34s] 261|Add|40004196|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|83.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|1c7c24086aa5c4db
        // [123.34s] 261|Change|40004196|ModelStatus|0|b201abb7838f00ef
        world.Events.Add(123.34f, () => black_Hole_40004196?.SetVisible(true));
        // [125.74s] 35|40004196|Black Hole|10018AEA|MeleeDpsB|0000|0000|0054|10018AEA|000F|0000|8c8ebf59d3e0ae2c
        world.Events.Add(125.74f, () => world.Tether(black_Hole_40004196!, party.Get(PartyRole.MeleeDpsB)!, TetherId.Tether0054));
        // [126.63s] 35|40004196|Black Hole|100AF82E|MainTank|0000|0000|0054|100AF82E|000F|0000|8125ff05577d2a82
        world.Events.Add(126.63f, () => world.Tether(black_Hole_40004196!, party.Get(PartyRole.MainTank)!, TetherId.Tether0054));
        // [130.51s] 271|40004196|0.7589|00|00|100.0000|83.0000|0.0000|e8fdff861f0cfa15
        world.Events.Add(130.51f, () => black_Hole_40004196?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, -17.000f), 0.759f)));
        // [130.60s] 21|40004196|Black Hole|BAFC|Nothingness|100AF82E|MainTank|3|967F4098|1B|BAFC8000|0|0|0|0|0|0|0|0|0|0|0|0|325133|325133|7600|10000|||108.60|92.09|0.00|-0.75|188300|188300|10000|10000|||100.00|83.00|0.00|0.00|0000881B|0|1|00||01|BAFC|BAFC|1.100|9EEB|a1deaee3dd34fb9e
        world.Events.Add(130.60f, () => black_Hole_40004196?.Cast(ActionId.Nothingness, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [130.60s] 264|40004196|BAFC|0000881B|0||||0.759|40004196|4c96c09313795a5e
        // [132.58s] 04|40004196|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|83.00|0.00|0.76|e67c3a4bedc7c5ba
        world.Events.Add(132.58f, () => black_Hole_40004196?.Despawn());
        // [132.58s] 261|Remove|40004196|402dd3c614bc7c58
    }

    private void Run_Black_Hole_40004197()
    {
        SimEnemy? black_Hole_40004197 = null;
        // [123.56s] 272|40004197|E0000000|0000|00|abc7e889ccdf06dd
        // [123.34s] 03|40004197|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|94.83|0.00|1.18|031d9e0b2db746ba
        world.Events.Add(123.34f, () => black_Hole_40004197 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-12.470f, 0.000f, -5.170f), 1.180f))));
        // [123.34s] 261|Add|40004197|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.1781|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|87.5281|PosY|94.8343|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|007c949bad970efb
        // [123.34s] 261|Change|40004197|ModelStatus|0|4521f011fe8a3965
        world.Events.Add(123.34f, () => black_Hole_40004197?.SetVisible(true));
        // [139.83s] 04|40004197|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||87.53|94.83|0.00|1.18|397e376c4ebf92f2
        world.Events.Add(139.83f, () => black_Hole_40004197?.Despawn());
        // [139.83s] 261|Remove|40004197|798db7c53fe7e499
    }

    private void Run_Black_Hole_40004198()
    {
        SimEnemy? black_Hole_40004198 = null;
        // [123.56s] 272|40004198|E0000000|0000|00|680ba73cbf9062b8
        // [123.34s] 03|40004198|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|112.47|0.00|2.75|6fce73e912cb2003
        world.Events.Add(123.34f, () => black_Hole_40004198 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-5.170f, 0.000f, 12.470f), 2.750f))));
        // [123.34s] 261|Add|40004198|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|2.7489|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|94.8343|PosY|112.4719|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|6bdf71136d648662
        // [123.34s] 261|Change|40004198|ModelStatus|0|c05b1f449da2cf88
        world.Events.Add(123.34f, () => black_Hole_40004198?.SetVisible(true));
        // [139.83s] 04|40004198|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||94.83|112.47|0.00|2.75|5854b2c05a77a15c
        world.Events.Add(139.83f, () => black_Hole_40004198?.Despawn());
        // [139.83s] 261|Remove|40004198|5d31b7470e47f246
    }

    private void Run_Black_Hole_40004199()
    {
        SimEnemy? black_Hole_40004199 = null;
        // [123.56s] 272|40004199|E0000000|0000|00|00432ec3990c6d15
        // [123.34s] 03|40004199|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|105.17|0.00|-1.96|beb735704879fa81
        world.Events.Add(123.34f, () => black_Hole_40004199 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(12.470f, 0.000f, 5.170f), -1.960f))));
        // [123.34s] 261|Add|40004199|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.9636|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|112.4719|PosY|105.1657|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|83985c84fc19719f
        // [123.34s] 261|Change|40004199|ModelStatus|0|01dc0a7e4c9c3887
        world.Events.Add(123.34f, () => black_Hole_40004199?.SetVisible(true));
        // [139.83s] 04|40004199|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||112.47|105.17|0.00|-1.96|472ad5f4ffc5d0e1
        world.Events.Add(139.83f, () => black_Hole_40004199?.Despawn());
        // [139.83s] 261|Remove|40004199|aab3aaa11f291f55
    }

    private void Run_Black_Hole_4000419A()
    {
        SimEnemy? black_Hole_4000419A = null;
        // [123.56s] 272|4000419A|E0000000|0000|00|08441441b86dd388
        // [123.34s] 03|4000419A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|87.53|0.00|-0.39|6f9e00ece902ccf2
        world.Events.Add(123.34f, () => black_Hole_4000419A = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(5.170f, 0.000f, -12.470f), -0.390f))));
        // [123.34s] 261|Add|4000419A|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-0.3928|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|105.1657|PosY|87.5281|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|3faecd1855810e0a
        // [123.47s] 261|Change|4000419A|ModelStatus|0|3ebf87a8389e85d8
        world.Events.Add(123.47f, () => black_Hole_4000419A?.SetVisible(true));
        // [139.83s] 04|4000419A|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||105.17|87.53|0.00|-0.39|4dab231080c99f96
        world.Events.Add(139.83f, () => black_Hole_4000419A?.Despawn());
        // [139.83s] 261|Remove|4000419A|97699481522fa33f
    }

    private void Run_Black_Hole_4000419B()
    {
        SimEnemy? black_Hole_4000419B = null;
        // [123.56s] 272|4000419B|E0000000|0000|00|a38e2827bb5a53d2
        // [123.34s] 03|4000419B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||91.00|100.00|0.00|1.57|27b648d8f4ca7a98
        world.Events.Add(123.34f, () => black_Hole_4000419B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(-9.000f, 0.000f, 0.000f), 1.570f))));
        // [123.34s] 261|Add|4000419B|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|1.5708|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|91.0000|PosY|100.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|f084c03654ba6512
        // [123.47s] 261|Change|4000419B|ModelStatus|0|3f49c477b40b0e28
        world.Events.Add(123.47f, () => black_Hole_4000419B?.SetVisible(true));
        // [139.83s] 04|4000419B|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||91.00|100.00|0.00|1.57|d09a698df342e7d4
        world.Events.Add(139.83f, () => black_Hole_4000419B?.Despawn());
        // [139.83s] 261|Remove|4000419B|2fe29546d8df3808
    }

    private void Run_Black_Hole_4000419C()
    {
        SimEnemy? black_Hole_4000419C = null;
        // [123.56s] 272|4000419C|E0000000|0000|00|639c8b9b8f2f312c
        // [123.34s] 03|4000419C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|109.00|0.00|-3.14|e53fbaf897201796
        world.Events.Add(123.34f, () => black_Hole_4000419C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 9.000f), -3.140f))));
        // [123.34s] 261|Add|4000419C|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-3.1416|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|109.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|a34a18b5142de9ee
        // [123.47s] 261|Change|4000419C|ModelStatus|0|e7af01878660a503
        world.Events.Add(123.47f, () => black_Hole_4000419C?.SetVisible(true));
        // [139.83s] 04|4000419C|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|109.00|0.00|-3.14|233653aa9837a343
        world.Events.Add(139.83f, () => black_Hole_4000419C?.Despawn());
        // [139.83s] 261|Remove|4000419C|902520466b34553a
    }

    private void Run_Black_Hole_4000419D()
    {
        SimEnemy? black_Hole_4000419D = null;
        // [123.56s] 272|4000419D|E0000000|0000|00|cbd656926337552e
        // [123.34s] 03|4000419D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||109.00|100.00|0.00|-1.57|d22021222616bae4
        world.Events.Add(123.34f, () => black_Hole_4000419D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(9.000f, 0.000f, 0.000f), -1.570f))));
        // [123.34s] 261|Add|4000419D|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|-1.5709|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|109.0000|PosY|100.0000|Radius|1.0000|Type|2|WorldID|65535|eb8f16e3585434c1
        // [123.47s] 261|Change|4000419D|ModelStatus|0|16a11de0ac769d65
        world.Events.Add(123.47f, () => black_Hole_4000419D?.SetVisible(true));
        // [139.83s] 04|4000419D|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||109.00|100.00|0.00|-1.57|6137a135290b4ddd
        world.Events.Add(139.83f, () => black_Hole_4000419D?.Despawn());
        // [139.83s] 261|Remove|4000419D|5582d7c7a1e8b167
    }

    private void Run_Black_Hole_4000419E()
    {
        SimEnemy? black_Hole_4000419E = null;
        // [123.56s] 272|4000419E|E0000000|0000|00|f69548ca9765efdf
        // [123.34s] 03|4000419E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|91.00|0.00|0.00|55650c156cb02425
        world.Events.Add(123.34f, () => black_Hole_4000419E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, -9.000f), 0.000f))));
        // [123.34s] 261|Add|4000419E|BNpcID|4C38|BNpcNameID|2097|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Black Hole|NPCTargetID|E0000000|PosX|100.0000|PosY|91.0000|PosZ|0.0000|Radius|1.0000|Type|2|WorldID|65535|828cd6787350fc74
        // [123.47s] 261|Change|4000419E|ModelStatus|0|60e9f35eb83d6ff4
        world.Events.Add(123.47f, () => black_Hole_4000419E?.SetVisible(true));
        // [139.83s] 04|4000419E|Black Hole|00|64|0000|00||8343|19512|188300|188300|10000|10000|||100.00|91.00|0.00|0.00|4426c3ef9b8502bb
        world.Events.Add(139.83f, () => black_Hole_4000419E?.Despawn());
        // [139.83s] 261|Remove|4000419E|dd6e1e61072518ed
    }

    private void Run_Chaos_400040E9_10()
    {
        SimEnemy? chaos_400040E9_10 = null;
        // [132.08s] 271|400040E9|0.0000|00|00|100.0000|104.0000|0.0000|80d448dfd26d215d
        world.Events.Add(132.08f, () => chaos_400040E9_10?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f)));
        // [131.82s] 03|400040E9|Chaos|00|1|0000|00||7691|9020|44|44|0|10000|||100.00|104.00|0.00|0.00|445c6445ae5e15d0
        world.Events.Add(131.82f, () => chaos_400040E9_10 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Chaos, Level: 1, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 0.000f, 4.000f), 0.000f))));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|450003|CDED0000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|7700|10000|||99.69|101.64|0.00|1.98|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|0|7|00||01|BAFA|BAFA|1.100|7FFF|a35bf026c0f72a6f
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|42F44001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|226668|226668|10000|10000|||99.65|99.60|0.00|2.37|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|1|7|00||01|BAFA|BAFA|1.100|7FFF|0cc9003208282f70
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450003|3ABA4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205207|205207|6500|10000|||99.60|99.57|0.00|1.60|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|2|7|00||01|BAFA|BAFA|1.100|7FFF|9d899600f0b6e8ec
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|398D4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|5765|10000|||99.84|99.50|0.00|1.97|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|3|7|00||01|BAFA|BAFA|1.100|7FFF|515dd3427ede55d3
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450003|3BD24001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|205177|205177|7905|10000|||99.84|99.38|0.00|1.57|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|4|7|00||01|BAFA|BAFA|1.100|7FFF|eeca0e6623e67c94
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|3E304001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|227550|227550|10000|10000|||99.95|99.85|0.00|2.44|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|5|7|00||01|BAFA|BAFA|1.100|7FFF|4374a14903edec98
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [132.17s] 22|400040E9|Chaos|BAFA|Earthquake|100A7A8F|OffTank|450003|58744001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|214557|217488|10000|10000|||106.43|107.32|0.00|-2.37|44|44|0|10000|||100.00|104.00|0.00|0.00|00008826|6|7|00||01|BAFA|BAFA|1.100|7FFF|f8b3e24f577e31b5
        world.Events.Add(132.17f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.OffTank)?.GameObjectId));
        // [132.17s] 264|400040E9|BAFA|00008826|1|-0.015|-0.015|-0.015|0.000|400040E9|5c3945310c6d4096
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|6703afd8761f10de
        world.Events.Add(132.17f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|1db94e6ee34ac605
        world.Events.Add(132.17f, () => party.Get(PartyRole.OffTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|73ad1b5b52c022b2
        world.Events.Add(132.17f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|4477bbeb4bf3cf2c
        world.Events.Add(132.17f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|dcd16e5dd4c011ce
        world.Events.Add(132.17f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|135092e2cd56df80
        world.Events.Add(132.17f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [132.17s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|e5e67c323e4ca49c
        world.Events.Add(132.17f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|b5dc45a683bd8bd3
        world.Events.Add(134.13f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100A7A8F|OffTank|00|217488|44|1e74e47dcd9fe7a5
        world.Events.Add(134.13f, () => party.Get(PartyRole.OffTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|886fa3c18185405c
        world.Events.Add(134.13f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|69a1968ae820c78b
        world.Events.Add(134.13f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|abfb24a4bc086133
        world.Events.Add(134.13f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|ae43b0ebdf9836d6
        world.Events.Add(134.13f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [134.13s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|e00596510273e9b4
        world.Events.Add(134.13f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|100AC8F1|CasterDps|450003|37304001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|189482|227550|10000|10000|||103.47|93.38|0.00|-0.61|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|0|7|00||01|BAFA|BAFA|1.100|7FFF|5de00f37868134d7
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.CasterDps)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|10066D86|ShieldHealer|450003|D154001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|153586|205177|8485|10000|||89.10|103.87|0.00|1.95|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|1|7|00||01|BAFA|BAFA|1.100|7FFF|c1abfb8b4375ec2a
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.ShieldHealer)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|100AF82E|MainTank|450003|6D110000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|310051|325133|5800|10000|||108.16|94.18|0.00|-0.94|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|2|7|00||01|BAFA|BAFA|1.100|7FFF|27356bc438b54c3b
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MainTank)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|1009061B|MeleeDpsA|450003|13044001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|142638|205177|8220|10000|||111.65|96.06|0.00|-1.28|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|3|7|00||01|BAFA|BAFA|1.100|7FFF|c81cd28c7f48e5c6
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsA)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|100AE96C|RegenHealer|EC450005|8F550000|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|325047|325047|7500|10000|||113.68|92.75|0.00|-1.12|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|4|7|00||01|BAFA|BAFA|1.100|7FFF|59de2c6486c2c5a9
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.RegenHealer)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|10018AEA|MeleeDpsB|450003|5AA94001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|148524|226668|10000|10000|||101.23|82.45|0.00|-0.34|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|5|7|00||01|BAFA|BAFA|1.100|7FFF|9a5ca7ae1dd72050
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.MeleeDpsB)?.GameObjectId));
        // [139.23s] 22|400040E9|Chaos|BAFA|Earthquake|100702A3|Player|450003|26BF4001|FF0E|D2C0000|1B|BAFA8000|0|0|0|0|0|0|0|0|0|0|128741|205207|6100|10000|||101.49|83.45|0.00|1.30|44|44|0|10000|||100.00|104.00|0.00|0.00|00008853|6|7|00||01|BAFA|BAFA|1.100|7FFF|73fe7d9d252cb09e
        world.Events.Add(139.23f, () => chaos_400040E9_10?.Cast(ActionId.Earthquake_BAFA, castSeconds: 0f, targetId: party.Get(PartyRole.PhysRangedDps)?.GameObjectId));
        // [139.23s] 264|400040E9|BAFA|00008853|1|-0.015|-0.015|-0.015|0.000|400040E9|2ba49b4b985c18b5
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|c2afd7f00cae241f
        world.Events.Add(139.23f, () => party.Get(PartyRole.MeleeDpsA)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AF82E|MainTank|00|325133|44|b661aaa9d3c50322
        world.Events.Add(139.23f, () => party.Get(PartyRole.MainTank)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|4acea82e070e82a7
        world.Events.Add(139.23f, () => party.Get(PartyRole.RegenHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|e8921cacaf96b4de
        world.Events.Add(139.23f, () => party.Get(PartyRole.ShieldHealer)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100702A3|Player|00|205207|44|898afbccfd84d74e
        world.Events.Add(139.23f, () => party.Get(PartyRole.PhysRangedDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|fe166fdcaff1b3f0
        world.Events.Add(139.23f, () => party.Get(PartyRole.MeleeDpsB)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [139.23s] 26|D2C|Earth Resistance Down II|1.96|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|b4004bba20e3a8f6
        world.Events.Add(139.23f, () => party.Get(PartyRole.CasterDps)?.AddStatus(StatusId.EarthResistanceDownII, 1.960f));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|1009061B|MeleeDpsA|00|205177|44|c2870126ef7f5687
        world.Events.Add(141.20f, () => party.Get(PartyRole.MeleeDpsA)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AF82E|MainTank|00|325133|44|185d293bf3251e0e
        world.Events.Add(141.20f, () => party.Get(PartyRole.MainTank)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AE96C|RegenHealer|00|325047|44|733b1ee62467e639
        world.Events.Add(141.20f, () => party.Get(PartyRole.RegenHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10066D86|ShieldHealer|00|205177|44|c662cf7bac1c68e1
        world.Events.Add(141.20f, () => party.Get(PartyRole.ShieldHealer)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100702A3|Player|00|205207|44|f32900cd881c77d5
        world.Events.Add(141.20f, () => party.Get(PartyRole.PhysRangedDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|10018AEA|MeleeDpsB|00|226668|44|d311069598ad291a
        world.Events.Add(141.20f, () => party.Get(PartyRole.MeleeDpsB)?.RemoveStatus(StatusId.EarthResistanceDownII));
        // [141.20s] 30|D2C|Earth Resistance Down II|0.00|400040E9|Chaos|100AC8F1|CasterDps|00|227550|44|afe44a49a83ace95
        world.Events.Add(141.20f, () => party.Get(PartyRole.CasterDps)?.RemoveStatus(StatusId.EarthResistanceDownII));
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

    private void Run_Graven_Image_4000414B()
    {
        SimEnemy? graven_Image_4000414B = null;
        // [-132.71s] 03|4000414B|Graven Image|00|64|0000|00||7132|19505|188300|188300|10000|10000|||100.00|52.50|10.70|0.00|91f1ff6ac318fda0
        // world.Events.Add(-132.71f, () => graven_Image_4000414B = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.GravenImage, NameId: BNpcNameId.GravenImage, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false, Placement: new Placement(new Vector3(0.000f, 10.700f, -47.500f), 0.000f))));
        // [-132.71s] 261|Add|4000414B|BNpcID|4C31|BNpcNameID|1BDC|CastTargetID|E0000000|CurrentMP|10000|CurrentWorldID|65535|Heading|0.0000|Level|100|MaxHP|188300|MaxMP|10000|ModelStatus|2048|Name|Graven Image|NPCTargetID|E0000000|PosX|100.0000|PosY|52.5000|PosZ|10.7000|Radius|0.5000|Type|2|WorldID|65535|fb9eddd41aac110b
        // [-132.71s] 261|Change|4000414B|ModelStatus|0|ee2f378609580e49
        // world.Events.Add(-132.71f, () => graven_Image_4000414B?.SetVisible(true));
    }
}
