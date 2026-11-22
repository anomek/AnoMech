using AnoMech.Core.EnemyActions;
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
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Umad.UmadConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Umad.P4KefkaSays;

// Auto-generated body from tools/parser.py --code, rehomed into the canonical layout.
// Player id -> role (first-seen order in the window):
//   10066D86 MT, 100AC8F1 OT, 100AE96C H1, 100702A3 H2,
//   10018AEA M1, 100AF82E M2, 100A7A8F R1, 1009061B C.
public sealed class UmadP4KefkaSaysScenario : IMultiplayerReplayable
{
    public string Name => "Kefka Says";
    public IPhase Phase => UmadZone.P4;
    public bool SupportsMultiplayer => true;

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly UmadP4KefkaSaysSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats =>
    [
        new UmadP4KefkaSaysAi(UmadP4KefkaSaysAi.GazeLayout.SupportsNorthDpsSouth),
        new UmadP4KefkaSaysAi(UmadP4KefkaSaysAi.GazeLayout.CentreLane),
    ];

    private UmadP4KefkaSaysState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka, neoExdeath, chaos;

    // The current run's randomized per-run assignments, exposed so
    // MultiplayerManager can read them after a host Start and broadcast them --
    // lets a peer's local "debug: bot controls my character" mode replay the
    // same choreography a host-side bot in that role would produce. Mirrors
    // UmadP3BlackHoleScenario.LastState.
    public UmadP4KefkaSaysState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP4KefkaSaysState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP4KefkaSaysState>)AiStrats[idx]).Run(state, world);

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

    // First played on a fresh actor, where an unloaded timeline can drop (see ActionTimelinePreload).
    private static readonly (ushort Id, string Key)[] NeoExdeathTimelines =
    [
        (TimelineId.NeoExdeathShow, "mon_sp/m0418/show/mon_sp001"),
    ];

    // Scheduled by host and peer alike, so each client fakes its own tank's LB3 gauge.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(NeoExdeathTimelines, "UmadP4KefkaSays");
        instanceWorld.Party.LimitBreak.Set(3f);
    }

    private void Run_InstanceEvents()
    {
        // [1.36s] 33|800375D2|80000027|1F|02|1BDB|40004142|5f5214a2bc97cd97
        world.Events.Add(1.45f, () => world.Map.DirectorUpdate(0x80000027U, 0x1FU, 0x2U, 0x1BDBU, 0x40004142U));
        // [9.50s] 33|800375D2|80000027|20|02|1BDB|40004142|f8e7da06e555fe6f
        world.Events.Add(9.50f, () => world.Map.DirectorUpdate(0x80000027U, 0x20U, 0x2U, 0x1BDBU, 0x40004142U));
        // [103.36s] 33|800375D2|80000027|04|02|1BDB|40004142|33182e2038e021ee
        world.Events.Add(103.36f, () => world.Map.DirectorUpdate(0x80000027U, 0x4U, 0x2U, 0x1BDBU, 0x40004142U));
        // [119.94s] 33|800375D2|80000027|21|02|1BDB|40004142|2b29864def0a7636
        world.Events.Add(119.94f, () => world.Map.DirectorUpdate(0x80000027U, 0x21U, 0x2U, 0x1BDBU, 0x40004142U));
    }

    private void Run_OtherDebuffs()
    {
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
        // Unconditional so a survival shows up in the dump too -- otherwise there's
        // no way to tell "the fix worked" from "this run's bomb never resolved".
        DiagnosticLog.Info($"[AccelerationBomb] real={real} IsActing={player.IsActing} IsMoving={player.IsMoving} role={player.Role} pos=({player.Position.X:F1},{player.Position.Z:F1}).");
        // real (honest) -> must be still, die if acting; fake (lie) -> must move, die if still.
        if (real ? player.IsActing : !player.IsActing)
            neoExdeath?.CastFromHelper(UmadActions.DeathBomb, player);
    }

    // UNVERIFIED: both replays roll Wave 3 true and cleanse every Beyond Death, so only an expiring
    // Allagan Field is seen; an uncleansed Beyond Death and either status lost on death are not.
    private void ResolveBeyondDeathAndAllaganField(StatusLost lost)
    {
        if (lost.StatusId == state.BeyondDeathStatus && lost.Reason == StatusLossReason.Expired)
            lost.Holder.DieOfStatus(lost.StatusId, "not cleansed");
        else if (lost.StatusId == state.AllaganFieldStatus && lost.Reason != StatusLossReason.Removed)
            DetonateAllaganField(lost.Holder, lost.Reason == StatusLossReason.Died ? UmadActions.DeathSurgeWipe : UmadActions.DeathSurge);
    }

    private void DetonateAllaganField(SimCharacter holder, EnemyAction surge)
    {
        if (holder is not ISimPartyMember member || !state.Wave3.List.Contains(member.Role)) return;
        var at = new Placement(holder.Position, 0f);
        world.Events.Add(0.1f, () => neoExdeath?.CastFromHelper(surge, at: at));
    }

    public void OnStatusLost(StatusLost lost) => ResolveBeyondDeathAndAllaganField(lost);

    public void Tick(float delta, float elapsed)
    {
    }

    private void Run_Kefka_40004142()
    {
        world.Events.Add(0f, () => kefka = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Kefka, NameId: BNpcNameId.Kefka, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.KefkaP4, Placement: new Placement(new Vector3(0.000f, 0.000f, 0.000f), 3.140f))));
        world.Events.Add(1.36f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(1.45f, () => kefka?.Cast(ActionId.KefkaSays, animationLock: 3.1f));
        world.Events.Add(9.41f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(9.59f, () => kefka?.Cast(ActionId.KefkaPoof, animationLock: 1.1f));
        world.Events.Add(10.93f, () => kefka?.AttachLockonVfx(state.Mystery[0].Blizzard.Lockon));
        world.Events.Add(10.93f, () => kefka?.AttachLockonVfx(state.Mystery[0].Lightning.Lockon));
        world.Events.Add(11.02f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        world.Events.Add(17.45f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(20.49f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(23.53f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(25.85f, () => kefka?.AttachLockonVfx(state.Mystery[1].Blizzard.Lockon));
        world.Events.Add(25.85f, () => kefka?.AttachLockonVfx(state.Mystery[1].Lightning.Lockon));
        world.Events.Add(25.94f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        world.Events.Add(31.57f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(34.61f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(37.64f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(40.68f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(40.99f, () => kefka?.AttachLockonVfx(state.Mystery[2].Blizzard.Lockon));
        world.Events.Add(40.99f, () => kefka?.AttachLockonVfx(state.Mystery[2].Lightning.Lockon));
        world.Events.Add(41.08f, () => kefka?.Cast(ActionId.MysteryMagic, animationLock: 3.1f));
        world.Events.Add(48.76f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        
        world.Events.Add(49.25f, () => kefka?.Cast(ActionId.KefkaRest, animationLock: 2.1f));
        world.Events.Add(53.05f, () => kefka?.SetModelState((byte)0x04));
        world.Events.Add(66.10f, () => kefka?.Cast(ActionId.KefkaUnrest, animationLock: 2.1f));
        world.Events.Add(66.91f, () => kefka?.SetModelState((byte)0x00));
        
        world.Events.Add(68.20f, () => kefka?.Cast(ActionId.ManaCharge, animationLock: 3.1f));
        world.Events.Add(72.00f, () => kefka?.AddStatus(StatusId.ManaCharge));
        world.Events.Add(73.20f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        
        world.Events.Add(74.37f, () => kefka?.AddStatus(StatusId.ThunderCharged));
        world.Events.Add(74.37f, () => kefka?.AttachLockonVfx(state.Mystery[3].Lightning.Lockon));
        world.Events.Add(74.45f, () => kefka?.Cast(ActionId.ThrummingThunderIII_Cast, animationLock: 3.1f));
        world.Events.Add(81.24f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(84.28f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(84.60f, () => kefka?.Cast(UmadActions.UltimaUpsurge));
        
        world.Events.Add(92.33f, () => kefka?.AddStatus(StatusId.BlizzardCharged));
        world.Events.Add(92.33f, () => kefka?.AttachLockonVfx(state.Mystery[3].Blizzard.Lockon));
        world.Events.Add(92.41f, () => kefka?.Cast(ActionId.BlizzardIIIBlowout_Cast, animationLock: 3.1f));
        world.Events.Add(98.13f, () => kefka?.RemoveStatus(StatusId.ManaCharge));
        
        world.Events.Add(100.41f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(103.45f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(103.54f, () => kefka?.AttachLockonVfx(state.ManaReleaseBlizzardLockon));
        world.Events.Add(103.54f, () => kefka?.AttachLockonVfx(state.ManaReleaseLightningLockon));
        world.Events.Add(103.63f, () => kefka?.Cast(ActionId.ManaRelease, animationLock: 3.1f));
        world.Events.Add(111.56f, () => kefka?.RemoveStatus(StatusId.BlizzardCharged));
        world.Events.Add(111.56f, () => kefka?.RemoveStatus(StatusId.ThunderCharged));
        world.Events.Add(113.51f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(116.51f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(119.54f, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        world.Events.Add(119.90f, () => kefka?.SetTargetable(false));
        world.Events.Add(120.03f, () => kefka?.Cast(ActionId.LightOfJudgment_Enrage, animationLock: 3.1f));
    }

    private void Run_Neo_Exdeath_400041A4()
    {
        world.Events.Add(0f, () => neoExdeath = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.NeoExdeath, NameId: BNpcNameId.NeoExdeath, Level: 100, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.NeoExdeath, Placement: new Placement(new Vector3(20.000f, 0.000f, 0.000f), -1.570f))));
        world.Events.Add(6.32f, () => neoExdeath?.SetPosition(new Placement(new Vector3(14.142f, 0.000f, -14.142f), -0.785f)));
        world.Events.Add(6.46f, () => neoExdeath?.PlayActionTimeline(TimelineId.NeoExdeathShow));
        
        world.Events.Add(11.28f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave1TrueVal, overrideStacks: true));
        world.Events.Add(11.37f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        world.Events.Add(21.82f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(26.21f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave2TrueVal, overrideStacks: true));
        world.Events.Add(26.30f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        world.Events.Add(36.81f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(41.17f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave3TrueVal, overrideStacks: true));
        world.Events.Add(41.26f, () => neoExdeath?.Cast(UmadActions.GrandCross));
        world.Events.Add(51.76f, () => neoExdeath?.RemoveStatus(StatusId.KefkaLiesVfx));
        
        world.Events.Add(53.28f, () => neoExdeath?.PlayActionTimeline(ActionTimelineId.WarpStart));
        world.Events.Add(55.25f, () => neoExdeath?.SetPosition(state.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
        world.Events.Add(55.58f, () => neoExdeath?.PlayActionTimeline(TimelineId.NeoExdeathShow));
        
        world.Events.Add(57.30f, () => neoExdeath?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.Wave4TrueVal, overrideStacks: true));
        world.Events.Add(57.39f, () => neoExdeath?.Cast(state.Antilights[0].ResolveFloodAction, animationLock: 3.1f));
        
        world.Events.Add(65.52f, () => neoExdeath?.PlayActionTimeline(ActionTimelineId.WarpStart));
    }

    private void Run_Chaos_400041A5()
    {
        world.Events.Add(0f, () => chaos = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.Chaos, NameId: BNpcNameId.Chaos, Level: 100, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, Visibility: SpawnVisibility.HiddenUntilShown, LayoutId: BNpcLayoutId.ChaosP4, Placement: new Placement(new Vector3(-18.000f, 0.000f, 0.000f), 1.570f))));
        world.Events.Add(6.37f, () => chaos?.SetPosition(new Placement(new Vector3(-12.728f, 0.000f, -12.728f), 0.785f)));
        world.Events.Add(6.46f, () => chaos?.PlayActionTimeline(ActionTimelineId.WarpEnd));
        
        world.Events.Add(16.42f, () => chaos?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.ChaosMysteries[0].StatusValue, overrideStacks: true));
        world.Events.Add(16.51f, () => chaos?.Cast(state.ChaosMysteries[0].Cast.Action, animationLock: 3.0f));
        world.Events.Add(31.35f, () => chaos?.RemoveStatus(StatusId.KefkaLiesVfx));
        world.Events.Add(31.35f, () => chaos?.AddStatus(StatusId.KefkaLiesVfx, stacks: state.ChaosMysteries[1].StatusValue, overrideStacks: true));
        world.Events.Add(31.43f, () => chaos?.Cast(state.ChaosMysteries[1].Cast.Action, animationLock: 3.0f));
        
        world.Events.Add(43.49f, () => chaos?.PlayActionTimeline(ActionTimelineId.WarpStart));
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
            float[] lightTiming = [11.02f, 25.94f, 41.08f, 74.45f, 110.63f];
            for (int k = 0; k < 5; k++)
            {
                var mystery = state.Mystery[k];
                var real = (i + mystery.LightningOffset) % 2 == 0;

                var location = placements[i].MulX(mystery.LightningOrientation).MulRot(mystery.LightningOrientation);
                if (real)
                    world.Events.Add(lightTiming[k], () => kefka?.CastFromHelper(mystery.Lightning.Damage, at: location));
                else if (mystery.Lightning.OmenAction != 0)
                    world.Events.Add(lightTiming[k], () => kefka?.CastFromHelper(mystery.Lightning.OmenAction, at: location, animationLock: 1.1f));
            }
        }
    }

    private void Run_Kefka_400040E6_1()
    {
        for (int i = 0; i < 4; i++)
        {
            var from = new Placement(Vector3.Zero, MathF.PI / 2 * i + MathF.PI / 4);
            float[] blizzTiming = [11.02f, 25.94f, 41.08f, 92.41f, 110.63f];
            for (int k = 0; k < 5; k++)
            {
                var mystery = state.Mystery[k];
                var real = (i + mystery.BlizzardOffset) % 2 == 0;
                if (real)
                    world.Events.Add(blizzTiming[k], () => kefka?.CastFromHelper(mystery.Blizzard.Damage, at: from));
                else if (mystery.Blizzard.OmenAction != 0)
                    world.Events.Add(blizzTiming[k], () => kefka?.CastFromHelper(mystery.Blizzard.OmenAction, at: from, animationLock: 1.1f));
            }
        }
    }

    private void Run_Chaos_400040E3_1()
    {
        var center = new Placement(Vector3.Zero, 0f);
        world.Events.Add(16.51f, () => chaos?.CastFromHelper(state.ChaosMysteries[0].Cast.Raidwide, at: center));
        world.Events.Add(31.43f, () => chaos?.CastFromHelper(state.ChaosMysteries[1].Cast.Raidwide, at: center));
    }


    private void Run_Neo_Exdeath_400040E7_1()
    {
        world.Events.Add(57.39f, () => neoExdeath?.CastFromHelper(UmadActions.EdgeOfDeath, at: state.NeoExdeathDirection.Apply(new Placement(new Vector3(0, 0, -20), 0))));
    }

    private void ResolveAntilight(MysteryAntilight mystery, float offset)
        => world.Events.Add(57.39f, () => neoExdeath?.CastFromHelper(mystery.Action,
            at: state.NeoExdeathDirection.Apply(new Placement(new Vector3(offset, 0.000f, -20f), 0f))));

    private void Run_Neo_Exdeath_400040E8_2() => ResolveAntilight(state.Antilights[0], -9.5f);

    private void Run_Neo_Exdeath_400040E9_2() => ResolveAntilight(state.Antilights[1], 9.5f);

    private void Run_Chaos_400040E2_2()
    {
        for (int i = 0; i < 8; i++)
        {
            PartyRole role = (PartyRole)i;
            world.Events.Add(87.36f, () => chaos?.CastFromHelper(state.InfernoMystery.Solution, at: party.Get(role)!.Placement()));
            world.Events.Add(110.07f, () => chaos?.CastFromHelper(state.TsunamiMystery.Solution, at: party.Get(role)!.Placement()));
        }
    }

    private void Run_Neo_Exdeath_400040E9_5()
    {
        for (int i = 0; i < 4; i++)
        {
            var targetId = i * 2 + 1 + (i+1) % 2;
            var shriekTargetId = i * 4;
            
            var bolt = i % 2 == 0;
            var first = DeathElement(bolt, stack: bolt ^ state.ElemTrue[0]);
            var second = DeathElement(bolt, stack: bolt ^ state.ElemTrue[1]);

            world.Events.Add(71.37f, () => neoExdeath?.CastFromHelper(first, state.ElemRoles[0].Get(targetId), at: state.ElemRoles[0].Get(targetId)!.Placement()));
            if (i < 2)
                world.Events.Add(80.49f, () => neoExdeath?.CastFromHelper(DeathShriek(state.Wave1True), at: new Placement(state.Wave1.Get(shriekTargetId)!.Position, 0f)));
            world.Events.Add(96.48f, () => neoExdeath?.CastFromHelper(second, state.ElemRoles[1].Get(targetId), at: new Placement(state.ElemRoles[1].Get(targetId)!.Position, 0f)));
            if (i < 2)
                world.Events.Add(104.39f, () => neoExdeath?.CastFromHelper(DeathShriek(state.Wave2True), at: new Placement(state.Wave2.Get(shriekTargetId)!.Position, 0f)));
        }
    }

    private static EnemyAction DeathElement(bool bolt, bool stack) => (bolt, stack) switch
    {
        (true, true) => UmadActions.DeathBoltStack,
        (true, false) => UmadActions.DeathBoltSpread,
        (false, true) => UmadActions.DeathWaveStack,
        (false, false) => UmadActions.DeathWaveSpread,
    };

    private static EnemyAction DeathShriek(bool real) => real ? UmadActions.DeathShriekLookAway : UmadActions.DeathShriekLookAt;

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new P4AiReplayStateMessage(
            s.Mystery.Select(m => m.BlizzardOffset).ToArray(),
            s.Mystery.Select(m => m.LightningOffset).ToArray(),
            s.Mystery.Select(m => m.LightningOrientation).ToArray(),
            s.Wave1First, s.Wave1.List, s.Wave1True,
            s.Wave2.List, s.Wave2True,
            s.InfernoMystery.IsTrue, s.TsunamiMystery.IsTrue,
            s.Wave3.List, s.Wounds,
            s.Antilights[0].Antilight == Antilight.White,
            s.NeoExdeathDirection.RadiansFromNorth)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not P4AiReplayStateMessage msg) return null;
        var shadowState = UmadP4KefkaSaysState.FromNetworkReplay(
            replayWorld.Party, msg.MysteryBlizzardOffset, msg.MysteryLightningOffset, msg.MysteryLightningOrientation,
            msg.Wave1First, msg.Wave1, msg.Wave1True, msg.Wave2, msg.Wave2True,
            msg.InfernoIsTrue, msg.TsunamiIsTrue, msg.Wave3, msg.Wounds,
            msg.Antilight0IsWhite, msg.NeoExdeathDirectionRadians);
        ((IScenarioAi<UmadP4KefkaSaysState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
