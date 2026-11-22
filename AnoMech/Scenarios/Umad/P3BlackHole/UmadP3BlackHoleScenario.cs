// Rehomed from tools/parser.py --code output into the canonical scenario layout.
// Player id -> role (first-seen order inside the window):
//   1001875C MT, 10056A3A OT, 10019262 H1, 10018A1B H2,
//   10018BF0 M1, 10018DC8 M2, 100188C6 R1, 1004E71C C.

using AnoMech.Core.EnemyActions;
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
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleScenario : IMultiplayerReplayable
{
    public string Name => "Black Hole";
    public IPhase Phase => UmadZone.P3;
    public bool SupportsMultiplayer => true;

    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    public void DrawMultiplayerSettings() => settingsWindow.DrawThunderIIIPlan();
    private readonly UmadP3BlackHoleSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP3BlackHoleAi(UmadP3BlackHoleAi.TetherOrder.DpsSupportAccretion),
        new UmadP3BlackHoleAi(UmadP3BlackHoleAi.TetherOrder.SupportDpsAccretion),
    ];

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private List<Vector3>[] BlackHolePositions = null!;
    private const float BlackSparkRadius = 2.2f;
    internal const float BlackHoleAvoidRadius = 2.3f;
    // Diagnostic: wider than the hit radius so a dump catches the approach.
    internal const float NearBlackHoleLogRadius = 3f;
    // Two crusts spent closer together than this wipe the party: the second cleanse hits everyone
    // still carrying the first one's vuln.
    private const float EarthResistanceDownDuration = 1.960f;

    private int PrimodialCrustsToResolve;
    private float CleanseCooldown;
    private SimEnemy? kefka;

    // Where the earth crystal's cleanses come from.
    private static readonly Placement CleanseSpot = new(new Vector3(0.000f, 0.000f, 4.000f), 0.000f);

    // For the multiplayer replay-state broadcast.
    public UmadP3BlackHoleState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        AnoMech.Core.DiagnosticLog.Clear();
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP3BlackHoleState(world, settingsWindow.Overrides);
        LastState = state;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP3BlackHoleState>)AiStrats[idx]).Run(state, world);

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
        Run_OtherDebuffs();
        Run_PlayerLockons();
    }

    // Fixed-time replays of the real director's messages with no dependency on this run's
    // rolls.
    public void RunInstanceEvents(SimWorld world)
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
        world.Events.Add(147.09f, () => state.StackTargets.Get(0)?.AttachLockonVfx(LockonId.Stack));
        world.Events.Add(152.58f, () => state.StackTargets.Get(1)?.AttachLockonVfx(LockonId.Stack));
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        Towers.ResolveTower(world, resolved, ActionId.StompAMole, UmadActions.UnmitigatedImpact, minSoakers: 1);
        QueueCleansesForSpentCrusts(resolved);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (lost is not { StatusId: StatusId.PrimordialCrust, Reason: StatusLossReason.Expired }) return;
        lost.Holder.RemoveStatus(StatusId.FirstInLine);
        lost.Holder.RemoveStatus(StatusId.SecondInLine);
        lost.Holder.RemoveStatus(StatusId.ThirdInLine);
        world.Events.Add(0.09f, () => state.ScenarioObjects.Chaos?.CastFromHelper(UmadActions.StrayEarth, lost.Holder, at: CleanseSpot));
    }

    public void Tick(float delta, float elapsed)
    {
       CleanseCooldown -= delta;
       CleanseCooldown = float.Max(CleanseCooldown, 0f);
       if (CleanseCooldown == 0f && PrimodialCrustsToResolve > 0)
       {
           PrimodialCrustsToResolve--;
           CleanseCooldown = UmadActions.EarthquakeCleanse.Cast.AnimationLock;
           state.ScenarioObjects.Chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, at: CleanseSpot);
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
                  var distSq = player.Placement().DistanceSq(bh);
                  // Diagnostic: a peer's position here is its last self-reported pose, so this
                  // records what the host believed at the moment that mattered.
                  if (distSq < NearBlackHoleLogRadius * NearBlackHoleLogRadius)
                  {
                      var role = (player as ISimPartyMember)?.Role.ToString() ?? "?";
                      AnoMech.Core.DiagnosticLog.Info(
                          $"[UmadP3BlackHoleScenario] Near black hole (wave {wave}): {role} at ({player.Position.X:F2},{player.Position.Z:F2}) is {MathF.Sqrt(distSq):F2}y from hole at ({bh.X:F2},{bh.Z:F2}).");
                  }
                  // Edge-triggered: 180s outlasts the fight.
                  if (!player.HasStatus(StatusId.DamageDown) && distSq < BlackSparkRadius * BlackSparkRadius)
                  {
                     var role = (player as ISimPartyMember)?.Role.ToString() ?? "?";
                     AnoMech.Core.DiagnosticLog.Info(
                         $"[UmadP3BlackHoleScenario] DamageDown triggered (wave {wave}): {role} at ({player.Position.X:F2},{player.Position.Z:F2}), {MathF.Sqrt(distSq):F2}y from hole at ({bh.X:F2},{bh.Z:F2}).");
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
        world.Events.Add(offset, () => enemy?.Cast(UmadActions.DamningEdict));
        world.Events.Add(offset + 6, () => enemy?.Follow(party.Get(PartyRole.MainTank)));
    }
    
    private void Run_Chaos_4000414D()
    {
        SimEnemy? chaos_4000414D = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.ChaosP3, Placement: new Placement(new Vector3(-8.000f, 0.000f, 0.000f), 0.000f)));
        state.ScenarioObjects.Chaos = chaos_4000414D;
        world.Events.Add(0.1f, () => chaos_4000414D?.AddStatus(StatusId.EpicVillain));
        world.Events.Add(0.98f, () => chaos_4000414D?.Cast(ActionId.Earthquake, animationLock: 3.1f));
        world.Events.Add(1f, () => chaos_4000414D?.Follow(party.Get(PartyRole.MainTank)));
        world.Events.Add(12.07f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(15.09f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(18.13f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(21.16f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(24.19f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(27.22f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(30.25f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(32.40f, () => chaos_4000414D?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f));
        world.Events.Add(33.29f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(36.34f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(39.37f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(42.41f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        RunEdict(chaos_4000414D, 45.58f);
        world.Events.Add(53.48f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(56.51f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(59.54f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        RunEdict(chaos_4000414D, 72.87f);
        world.Events.Add(80.90f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(83.94f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(86.97f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(90.00f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(93.02f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(99.18f, () => chaos_4000414D?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f));
        // The shockwaves keep the facing Chaos casts Implosion with, not wherever it turns after.
        var implosionFrom = new Placement();
        world.Events.Add(113.38f, () =>
        {
            if (chaos_4000414D is null) return;
            implosionFrom = chaos_4000414D.Placement();
            chaos_4000414D.Cast(state.ImplosionAttack, animationLock: 6.1f);
        });
        world.Events.Add(124.41f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(141.47f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(144.50f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(147.09f, () => chaos_4000414D?.Cast(ActionId.KnockDown_Cast, animationLock: 3.1f));
        world.Events.Add(152.53f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(155.57f, () => chaos_4000414D?.Cast(UmadActions.ChaosAutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(157.22f, () => chaos_4000414D?.Cast(ActionId.BigBang_Cast, animationLock: 3.1f));
        
        for (int i = 0; i < 2; i++)
        {
            var rotationAdjustment = i * MathF.PI + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0 : MathF.PI / 2);
            world.Events.Add(119.09f, () => chaos_4000414D?.CastFromHelper(UmadActions.ImplosionShockwave, at: implosionFrom.AddToRotation(rotationAdjustment)));
            world.Events.Add(121.11f, () => chaos_4000414D?.CastFromHelper(UmadActions.ImplosionShockwave, at: implosionFrom.AddToRotation(rotationAdjustment + MathF.PI / 2)));
        }
    }
    
    private void RunThunder(int setNumber, float time, SimEnemy? exdeath)
    {
        world.Events.Add(time - 0.2f, () => exdeath?.Follow());
        world.Events.Add(time, () => exdeath?.CastFromHelper(UmadActions.ThunderIII, party.Find.Closest(exdeath.Position)));
        world.Events.Add(time + 3f, () => exdeath?.CastFromHelper(UmadActions.ThunderIII, party.Find.Closest(exdeath.Position)));
        world.Events.Add(time + 3.5f, () => exdeath?.Follow(party.Get(PartyRole.OffTank)));
    }

    private void Run_Exdeath_4000414C()
    {
        SimEnemy? exdeath_4000414C = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.Exdeath, Placement: new Placement(new Vector3(8.000f, 0.000f, 0.000f), 0.000f)));
        state.ScenarioObjects.Exdeath = exdeath_4000414C;
        world.Events.Add(0.1f, () => exdeath_4000414C?.AddStatus(StatusId.FatedVillain));
        world.Events.Add(1f, () => exdeath_4000414C?.Follow(party.Get(PartyRole.OffTank)));
        world.Events.Add(12.07f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(15.09f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(18.13f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(21.16f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(22.18f, () => exdeath_4000414C?.Cast(ActionId.BlackHole, animationLock: 3.1f));
        world.Events.Add(27.22f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(30.25f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(32.40f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f));
        world.Events.Add(33.29f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(36.34f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(37.59f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        world.Events.Add(44.37f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(47.41f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(50.45f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(53.48f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(56.51f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(59.54f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(77.78f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(78.85f, () => exdeath_4000414C?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        world.Events.Add(85.85f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(88.89f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(91.91f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(99.18f, () => exdeath_4000414C?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f));
        world.Events.Add(113.38f, () => exdeath_4000414C?.Cast(UmadActions.WhiteHole));
        world.Events.Add(122.45f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(125.48f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(141.51f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        world.Events.Add(143.61f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_Cast, animationLock: 3.1f));
        world.Events.Add(150.66f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.RegenHealer)));
        world.Events.Add(153.69f, () => exdeath_4000414C?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.RegenHealer)));
        world.Events.Add(156.95f, () => exdeath_4000414C?.Cast(ActionId.BlizzardIII_Raidwide, animationLock: 4.1f));
        
        RunThunder(1, 42.63f, exdeath_4000414C);
        RunThunder(2, 83.94f, exdeath_4000414C);
    }

    private void Run_Chaos_400040E9_1()
    {
        var chaos = state.ScenarioObjects.Chaos;
        world.Events.Add(0.98f, () => chaos?.CastFromHelper(ActionId.Earthquake_Visual, at: new Placement(Vector3.Zero, 0f), animationLock: 1.1f));
        world.Events.Add(12.38f, () => state.Roles.Get(7)?.RemoveStatus(StatusId.Accretion));
        world.Events.Add(13.40f, () => chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, state.Roles.Get(7), at: CleanseSpot));
        world.Events.Add(16.30f, () => state.Roles.Get(3)?.RemoveStatus(StatusId.Accretion));
        world.Events.Add(17.32f, () => chaos?.CastFromHelper(UmadActions.EarthquakeCleanse, state.Roles.Get(3), at: CleanseSpot));
    }

    private void Run_Kefka_40004141()
    {
        var kefkaPos = new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0);
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.KefkaP3, NameId: BNpcNameId.Kefka, Level: 100, Targetable: false, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.KefkaP3, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 0.000f))));
        world.Events.Add(5.88f, () => kefka?.AddStatus(StatusId.Max, stacks: (ushort)506, overrideStacks: true));
        world.Events.Add(5.88f, () => kefka?.SetModelState((byte)0x05));
        world.Events.Add(5.88f, () => kefka?.SetPosition(kefkaPos));
        world.Events.Add(6.05f, () => kefka?.PlayActionTimeline(TimelineId.KefkaP3Show));
        
        world.Events.Add(12.21f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(14.16f, () => kefka?.SetPosition(state.KefkaPosition[0].Apply(kefkaPos)));
        world.Events.Add(14.33f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        
        world.Events.Add(16.39f, () => kefka?.Cast(state.SlapAttacks[0], animationLock: 6.1f));
        
        world.Events.Add(42.64f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(44.60f, () => kefka?.SetPosition(state.KefkaPosition[1].Apply(kefkaPos)));
        world.Events.Add(44.76f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        
        world.Events.Add(46.83f, () => kefka?.Cast(state.SlapAttacks[1], animationLock: 6.1f));
        
        world.Events.Add(70.06f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(72.02f, () => kefka?.SetPosition(state.KefkaPosition[2].Apply(kefkaPos)));
        world.Events.Add(72.21f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        
        world.Events.Add(74.25f, () => kefka?.Cast(ActionId.LookUponMeAndDespair, animationLock: 3.1f));
        world.Events.Add(79.38f, () => kefka?.SetModelState((byte)0x07));
        world.Events.Add(81.39f, () => kefka?.Cast(ActionId.StandUp_ToWall, animationLock: 4.1f));
        world.Events.Add(82.20f, () => kefka?.SetModelState((byte)0x05));
        
        world.Events.Add(106.61f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(108.56f, () => kefka?.SetPosition(state.KefkaPosition[3].Apply(kefkaPos)));
        world.Events.Add(108.75f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        
        world.Events.Add(114.81f, () => kefka?.Cast(state.SlapAttacks[2], animationLock: 6.1f));
        
        world.Events.Add(127.98f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpStart2));
        world.Events.Add(130.02f, () => kefka?.SetPosition(state.KefkaPosition[4].Apply(kefkaPos)));
        world.Events.Add(130.18f, () => kefka?.PlayActionTimeline(ActionTimelineId.WarpEnd2));
        
        world.Events.Add(132.26f, () => kefka?.Cast(ActionId.LookUponMeAndDespair2, animationLock: 3.1f));
        world.Events.Add(137.40f, () => kefka?.SetModelState((byte)0x07));
        world.Events.Add(139.41f, () => kefka?.Cast(ActionId.StandUp_Levitate, animationLock: 4.1f));
        world.Events.Add(140.22f, () => kefka?.SetModelState((byte)0x06));
        
        world.Events.Add(145.52f, () => kefka?.Cast(ActionId.StompAMole_Cast, animationLock: 8.1f));
        world.Events.Add(152.13f, () => kefka?.SetModelState((byte)0x05));
    }

    private void Run_EventObj_1EC03D_40004163()
    {
        SimEventObject? eventObj_1EC03D_40004163 = null;
        world.Events.Add(6.04f, () => eventObj_1EC03D_40004163 = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.EarthCore, LayoutId = EObjId.EarthCoreLayoutId, TargetableStatus = 5, Arg2 = EObjId.EarthCoreArg2, Placement = new Placement(new Vector3(0f, 0f, 4f), 0.000f) }));
        world.Events.Add(140.13f, () => eventObj_1EC03D_40004163?.Despawn());
    }


    private void Run_Kefka_400040E5_1()
    {
        var center = new Placement(Vector3.Zero, 0f);
        world.Events.Add(23.25f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));
        world.Events.Add(53.71f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));
        world.Events.Add(121.69f, () => kefka?.CastFromHelper(UmadActions.SlapHappyFinalSlap, at: center));
    }

    private void RunSlapAttack(float time, int slapIndex, int kefkaIndex, int row)
    {
        var mul = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1 : 1;
        var dir = state.KefkaPosition[kefkaIndex];
        var pos = dir.Apply(new Placement(new(10 * mul, 0, -10 + row*10), 0));
        world.Events.Add(time + row * 0.65f, () => kefka?.CastFromHelper(UmadActions.SlapHappySlap, at: pos));
    }

    private void Run_Kefka_400040E6_1()
    {
        for (int i = 0; i < 3; i++)
        {
            RunSlapAttack(22.14f, 0, 0, i);
            RunSlapAttack(52.59f, 1, 1, i);
            RunSlapAttack(120.57f, 2, 3, i);
        }
    }

    // From the centre, turned to the target as it fires.
    private void RunSlapCone(float time, int index, int row)
    {
        var targets = state.ConeTargets[index];
        if (row < targets.List.Length)
        {
            var target = targets.Get(row);
            var action = targets.List.Length == 1 ? UmadActions.ShockingImpact : UmadActions.ShockwaveCone;
            world.Events.Add(time, () => kefka?.CastFromHelper(action, LiveConeTarget(target), at: new Placement(Vector3.Zero, 0f)));
        }
    }

    // ConeTargets is rolled at start; if that pick has since died (usually to Implosion),
    // facing the corpse can sweep an unrelated live group. A boss never re-targets a corpse,
    // so retarget within the same role category, which shares the landing spot by design.
    private SimCharacter? LiveConeTarget(SimCharacter? target)
    {
        if (target is null || target.IsAlive() || target is not ISimPartyMember deadMember)
            return target;
        return party.ActiveMembers()
                    .FirstOrDefault(m => m is ISimPartyMember alive && SameRoleCategory(alive.Role, deadMember.Role))
               ?? target;
    }

    private static bool SameRoleCategory(PartyRole a, PartyRole b) => a.IsTank() == b.IsTank() && a.IsDps() == b.IsDps();
    
    private void Run_Kefka_400040E7_1()
    {
        for (int i = 0; i < 3; i++)
        {
            RunSlapCone(24.81f, 0, i);
            RunSlapCone(55.26f, 1, i);
            RunSlapCone(123.25f, 2, i);
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
            world.Events.Add(25.17f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.BlackHole, Placement: FacingCentre(BlackHolePositions[0][index]))));
            world.Events.Add(40.41f, () => black_Hole_40004169?.FadeOut());
            
            world.Events.Add(55.79f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.BlackHole, Placement: FacingCentre(BlackHolePositions[1][index]))));
            world.Events.Add(74.02f, () => black_Hole_40004169?.FadeOut());
            
            world.Events.Add(89.95f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.BlackHole, Placement: FacingCentre(BlackHolePositions[2][index]))));
            world.Events.Add(108.27f, () => black_Hole_40004169?.FadeOut());
            
            world.Events.Add(123.83f, () => black_Hole_40004169 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.BlackHole, Placement: FacingCentre(BlackHolePositions[3][index]))));
            world.Events.Add(138.79f, () => black_Hole_40004169?.FadeOut());
        }
    }
    
    private void RunActiveBlackHole(Vector3 pos, float spawnTime, float tetherTime, float firstShotTime, int numberOfShots)
    {
        SimEnemy? black_Hole_40004166 = null;
        SimTether? tether = null;
        world.Events.Add(spawnTime, () => black_Hole_40004166 = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BlackHole, NameId: BNpcNameId.BlackHole, Level: 100, Targetable: false, EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.BlackHole, Placement: FacingCentre(pos))));
        world.Events.Add(tetherTime, () => tether = world.Tether(black_Hole_40004166!, End.Passable(), TetherId.GrabbyTether));
        
        var fadeOutDelay = 1.05f;
        var shootCooldown = 5.1f;
        for (int i = 0; i < numberOfShots; i++)
        {
            var shotTime = firstShotTime + i * shootCooldown;
            // A snap, not a turn: the beam follows the model's facing, and a turn at the boss rate
            // isn't done in 0.1 s.
            world.Events.Add(shotTime - 0.1f, () =>
            {
                if (black_Hole_40004166 is { } hole && tether?.B is { } holder)
                    hole.SetRotation(hole.Placement().Face(holder.Position).Rotation);
            });
            world.Events.Add(shotTime, () => ShootNothingness(black_Hole_40004166, tether?.B));
        }
        world.Events.Add(firstShotTime + shootCooldown * (numberOfShots - 1), () => tether?.Despawn());
        world.Events.Add(firstShotTime + shootCooldown * (numberOfShots - 1) + fadeOutDelay, () => black_Hole_40004166?.FadeOut());
    }

    private static Placement FacingCentre(Vector3 position) => new Placement(position, 0f).Face(Vector3.Zero);

    // A crust this shot spends queues a cleanse for Tick, which retail casts this long after the
    // shot. Whoever it left at Meanest Existence without a crust, and didn't kill, spent theirs.
    private const float CleanseDelayAfterSpentCrust = 1.55f;

    private static void ShootNothingness(SimEnemy? hole, SimCharacter? target) => hole?.Cast(UmadActions.Nothingness, target);

    private void QueueCleansesForSpentCrusts(ActionResolved resolved)
    {
        if (resolved.Action.ActionId != ActionId.Nothingness) return;
        var spent = resolved.Hits.Count(h => h.HasStatus(StatusId.MeanestExistence)
                                             && !h.HasStatus(StatusId.PrimordialCrust)
                                             && !resolved.Killed.Contains(h));
        if (spent > 0)
            world.Events.Add(CleanseDelayAfterSpentCrust, () => PrimodialCrustsToResolve += spent);
    }

    private void Run_Black_Hole_40004166()
    {
        RunActiveBlackHole(BlackHolePositions[0][2], 25.17f, 26.00f, 32.27f, 1);
        RunActiveBlackHole(BlackHolePositions[0][1], 25.17f, 32.27f, 39.33f, 1);
        RunActiveBlackHole(BlackHolePositions[0][0], 25.17f, 32.27f, 39.33f, 1);
        
        for (var i = 0; i < 3; i++)
        {
            RunActiveBlackHole(BlackHolePositions[1][i], 55.70f, 56.36f, 62.83f, 3);
            RunActiveBlackHole(BlackHolePositions[2][i], 89.95f, 90.77f, 97.12f, 3);
        }
        
        RunActiveBlackHole(BlackHolePositions[3][0], 123.83f, 124.33f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][1], 123.83f, 124.33f, 130.60f, 1);
        RunActiveBlackHole(BlackHolePositions[3][2], 123.83f, 130.60f, 137.67f, 1);
    }

    private void Run_BlackHoleObstacles()
    {
        // Black holes appear, then 1.5s later (1.5s before their damage window opens)
        // bots steer around them; obstacles clear when the wave despawns.
        RunBlackHoleObstacleWave(0, 26.67f, 41.33f);
        RunBlackHoleObstacleWave(1, 57.29f, 75.07f);
        RunBlackHoleObstacleWave(2, 91.45f, 109.12f);
        RunBlackHoleObstacleWave(3, 125.33f, 139.83f);
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
        // The caster stays at the centre, facing across the arena; the cast's ground location is
        // where the 100-long rect starts, half its length behind the caster.
        world.Events.Add(74.29f, () => kefka?.CastFromHelper(UmadActions.LookUponMeAndDespair, state.KefkaPosition[2].Apply(new Vector3(0.000f, 0.000f, -50.000f)),
            at: state.KefkaPosition[2].Apply(new Placement(Vector3.Zero, 0))));
        world.Events.Add(132.35f, () => kefka?.CastFromHelper(UmadActions.LookUponMeAndDespair, state.KefkaPosition[4].Apply(new Vector3(0.000f, 0.000f, -50.000f)),
            at: state.KefkaPosition[4].Apply(new Placement(Vector3.Zero, 0))));
    }

    private void RunBlizzard(Vector3 from, float offset, PartyRole role)
    {
        world.Events.Add(offset, () =>
        {
            if (party.Get(role) is { } member)
                state.ScenarioObjects.Exdeath?.CastFromHelper(UmadActions.BlizzardIII, member.Position, at: new Placement(from, 0f));
        });
    }

    private void Run_Exdeath_400040E2()
    {
        for (int i = 0; i < 8; i++)
            RunBlizzard(new Vector3(-9.900f, 0.000f, 9.900f), 146.68f, (PartyRole)i);
    }

    private void Run_Chaos_400040E1()
    {
        var from = new Placement(new Vector3(-4.452f, 0.200f, -5.299f), -2.220f);
        world.Events.Add(152.13f, () => state.ScenarioObjects.Chaos?.CastFromHelper(UmadActions.KnockDown, state.StackTargets.Get(0), at: from));
        world.Events.Add(157.67f, () => state.ScenarioObjects.Chaos?.CastFromHelper(UmadActions.KnockDown, state.StackTargets.Get(1), at: from));
    }


    private void Run_Exdeath_400040D8_1()
    {
        for(int i = 0; i < 8; i++)
            RunBlizzard(new Vector3(0.000f, 0.000f, -10.000f), 149.73f, (PartyRole)i);
    }

    private void RunStomp(float offset, int mul)
        => world.Events.Add(offset, () => kefka?.CastFromHelper(UmadActions.StompAMole, at: state.KefkaPosition[4].Apply(new Placement(new Vector3(mul * 10, 0, 0), 0))));

    private void Run_Kefka_400040D6()
    {
        RunStomp(150.71f, -1);
        RunStomp(152.00f, 1);
        RunStomp(153.29f, -1);
        RunStomp(154.63f, 1);
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new AiReplayStateMessage(
            s.Roles.List, s.StackTargets.List, s.SlapAttacks.ToArray(),
            s.KefkaPosition.Select(d => d.RadiansFromNorth).ToArray(), s.ImplosionAttack,
            s.ThunderSet1, s.ThunderSet2)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not AiReplayStateMessage msg) return null;
        var shadowState = UmadP3BlackHoleState.FromNetworkReplay(
            replayWorld, msg.Roles, msg.StackTargets, msg.SlapAttacks, msg.KefkaPositionRadians, msg.ImplosionAttack,
            msg.ThunderSet1, msg.ThunderSet2);
        ((IScenarioAi<UmadP3BlackHoleState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Chaos/Exdeath may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not UmadP3BlackHoleState shadowState) return;
        shadowState.ScenarioObjects.Chaos ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.ChaosP3);
        shadowState.ScenarioObjects.Exdeath ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.Exdeath);
    }

    // A peer's bot steers round the black holes its client shows, while they stand.
    public void RebuildPeerObstacles(ObstacleField obstacles, IReadOnlyDictionary<int, SimEnemy> peerEnemies, IReadOnlyDictionary<int, SimEventObject> peerEventObjects, SimCharacter? localPlayer)
    {
        foreach (var blackHole in peerEnemies.Values)
            if (blackHole.BNpcBaseId == BNpcBaseId.BlackHole)
                obstacles.Add(new CircleObstacle(new Vector2(blackHole.Position.X, blackHole.Position.Z), BlackHoleAvoidRadius));
    }
}
