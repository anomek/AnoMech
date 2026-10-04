using AnoMech.Core.Native.Interfaces;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Scenarios.Top.TopConstants;
using Actions = AnoMech.Scenarios.Top.TopActions;

namespace AnoMech.Scenarios.Top.P5Omega;

public sealed class TopP5OmegaScenario : IMultiplayerReplayable
{
    public string Name => "Omega";
    public IPhase Phase => TopZone.P5;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 190.62f;

    // Where the arena's outer wall stops a player once the edge is down.
    private const float ArenaWallRadius = 22.4f;

    // Down for the Count's param: the knockout_loop pose it holds its target in.
    private const int KnockoutLoopTimeline = 3783;

    private SimWorld world = null!;
    private SimParty party = null!;

    private SimEnemy? boss;
    private SimCharacter? bossTarget;
    private TopHelpers? omegaFHelpers;
    private TopHelpers? finalHelpers;
    private TopHelpers? blasterHelpers;

    private const string Tag = "TopP5Omega";

    private static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (10726, "mon_sp/gimmick/z3oz_boss_gimmick14"),
        (10727, "mon_sp/gimmick/z3oz_boss_gimmick15"),
        (10728, "mon_sp/gimmick/z3oz_boss_gimmick16"),
        (6738, "mon_sp/gimmick/z3of_boss_gimmick06"),
        (10713, "mon_sp/gimmick/z3oz_boss_gimmick01"),
        (1378, "mon_sp/gimmick/monster_hanyou_hitclip_nomi_saisoku"),
    ];

    TopP5OmegaState state = null!;
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP5OmegaSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP5OmegaAi()];

    // Exposed so MultiplayerManager can read the AI-relevant subset after a host Start and
    // broadcast it -- see UmadP3BlackHoleScenario.LastState for the pattern.
    public TopP5OmegaState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP5OmegaState(world.Rng, world.Party, settingsWindow.Overrides);
        LastState = state;
        helloWorld2Broadcast = false;
        boss = null;
        var solo = selectedAi is null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP5OmegaState>)AiStrats[idx]).Run(state, world);

        omegaFHelpers = finalHelpers = blasterHelpers = null;

        world.Events.Add(1.39f, SpawnHelpers);
        Run_Omega_M_4000A63C(solo);
        Run_Omega_F_4000A72A();
        if(!solo) Run_Omega_4000A72E();
        Run_Omega_4000A72F(solo);
        Run_Omega_4000A40B_1();
        Run_Omega_F_4000A40B_2(solo);
        if(!solo) Run_Omega_4000A409_1();
        Run_BlindFaith();
        if (!solo) Run_OtherDebuffs();
    }

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, Tag);
        instanceWorld.Events.Add(97.04f, () => Natives.Bgm.Silence());
    }

    // Real-packet helpers, named for the mechanic each casts, spawned well ahead of their first hit.
    private void SpawnHelpers()
    {
        omegaFHelpers = new TopHelpers(world, BNpcNameId.OmegaFDynamis, 6, Tag);
        finalHelpers = new TopHelpers(world, BNpcNameId.OmegaFinal, 8, Tag);
        blasterHelpers = new TopHelpers(world, BNpcNameId.OmegaBeetle, 2, Tag);
    }

    private void Run_OtherDebuffs()
    {
        world.Events.Add(0.5f, () =>
        {
            party.ForEachActive(member => member.AddStatus(StatusId.QuickeningDynamis, stacks: 1));
            state.DoubleDynamicTargets.ForEach(member => member.AddStatus(StatusId.QuickeningDynamis, stacks: 1));
        });
        world.Events.Add(9.21f, () =>
        {
            ushort[] statuses1 = [StatusId.HelloNearWorld, StatusId.HelloDistantWorld, StatusId.HelloNearWorld, StatusId.HelloDistantWorld];
            float[] durations = [32f, 32f, 50f, 50f];
            ushort[] statuses2 = [StatusId.FirstInLine, StatusId.FirstInLine, StatusId.SecondInLine, StatusId.SecondInLine];
            state.HelloWorldTargets.ForEach((i, member) =>
            {
               member.AddStatus(statuses1[i], durations[i]);
               member.AddStatus(statuses2[i]);
            });
        });
        // Host-only, resolved here (not in TopP5OmegaAi, which also runs for a peer's own
        // replay) and broadcast via BuildMidRunUpdateMessage below -- a shuffle run
        // independently on both sides would disagree on who stands where.
        world.Events.Add(40.5f, () => state.HelloWorld2 ??= ResolveHelloWorld2());
        world.Events.Add(41.21f, () =>
        {
            state.HelloWorldTargets.Get(0)?.RemoveStatus(StatusId.FirstInLine);
            state.HelloWorldTargets.Get(1)?.RemoveStatus(StatusId.FirstInLine);
        });
        world.Events.Add(59.22f, () =>
        {
            state.HelloWorldTargets.Get(2)?.RemoveStatus(StatusId.SecondInLine);
            state.HelloWorldTargets.Get(3)?.RemoveStatus(StatusId.SecondInLine);
        });
    }

    // Marked from the plan before the first set's jumps land, as the real markers are: the
    // first-in-line and the four jumpers each gain a stack, so the double-stack players who
    // take no monitor reach three and take the Blaster tethers.
    private PartyRole[] ResolveHelloWorld2()
    {
        var tethers = world.Rng.Shuffle(state.DoubleDynamicTargets.List.Where(role => !state.MonitorTargets.Contains(role))).ToList();
        var freeAgents = world.Rng.Shuffle(Enum.GetValues<PartyRole>()
                             .Where(role => role != state.HelloWorldTargets[2] && role != state.HelloWorldTargets[3] && !tethers.Contains(role))).ToList();
        return
        [
            state.HelloWorldTargets[2], state.HelloWorldTargets[3], tethers[0], tethers[1],
            freeAgents[0], freeAgents[1], freeAgents[2], freeAgents[3]
        ];
    }

    public void Tick(float delta, float elapsed)
    {
        HelloWorld.CheckHolderDeaths(world, omegaFHelpers);
    }

    private void Run_Omega_M_4000A63C(bool solo)
    {
        bossTarget = party.Get(PartyRole.MainTank) ?? party.Get(party.PlayerRole);
        var mainTank = bossTarget;
        world.Events.Add(0, () => boss = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: 0x32, BNpcBaseId: BNpcBaseId.OmegaMDynamis, NameId: BNpcNameId.OmegaMDynamis, Level: 90, Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true, Placement: new Placement(new Vector3(-000f, -0.000f, 0.000f), MathF.PI))));
        world.Events.Add(0.1f, () => boss?.AddStatus(StatusId.OmegaF, stacks: 492, overrideStacks: true));
        world.Events.Add(1.06f, () => world.Map.BattleTalk(BNpcNameId.OmegaFDynamis, BattleTalkId.BeginHypothesis, 6000));
        world.Events.Add(1.15f, () => boss?.Cast(Actions.RunMiOmegaVersion));
        world.Events.Add(6f, () => boss?.Follow(mainTank));
        world.Events.Add(6.59f, () => AutoAttack(2.7f));
        world.Events.Add(9.63f, () => AutoAttack(0.1f));
        world.Events.Add(12.66f, () => AutoAttack(0.1f));
        world.Events.Add(15.69f, () => AutoAttack(0.1f));
        world.Events.Add(18.72f, () => AutoAttack(0.1f));
        world.Events.Add(21.76f, () => AutoAttack(0.1f));
        world.Events.Add(24.79f, () => AutoAttack(0.1f));
        world.Events.Add(27.83f, () => AutoAttack(0.1f));
        world.Events.Add(30.86f, () => AutoAttack(0.1f));
        world.Events.Add(31.54f, () => boss?.Follow());
        world.Events.Add(31.54f, () => boss?.SetTargetable(false));
        world.Events.Add(31.63f, () => boss?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(59.40f, () => boss?.SetPosition(new Placement(new Vector3(0.000f, 0.000f, 0.000f), 3.142f)));
        world.Events.Add(59.49f, () => boss?.PlayActionTimeline(TimelineId.Spawn));
        // world.Events.Add(60.52f, () => boss?.SetVisible(true));
        world.Events.Add(63.59f, () => boss?.SetTargetable(true));
        if (!solo)
        {
            world.Events.Add(67.73f, CastSolarRay);
            world.Events.Add(73.00f, SwapToOffTank);
            world.Events.Add(75.94f, SolarRayFollowUp);
            world.Events.Add(80.01f, () => AutoAttack(0.1f));
        }
        world.Events.Add(82.73f, LowerArenaEdge);
        world.Events.Add(82.86f, () => boss?.Cast(ActionId.Teleport7b43, castSeconds: 0f, targetLocation: Vector3.Zero, animationLock: 1.1f));
        world.Events.Add(84.16f, () => boss?.SetRotation(0f));
        world.Events.Add(84.20f, () => world.Map.BattleTalk(BNpcNameId.OmegaFDynamis, BattleTalkId.EvaluationFailed, 6000));
        world.Events.Add(84.29f, () => boss?.Cast(ActionId.BlindFaith, castSeconds: 9.7f, targetId: boss?.GameObjectId, animationLock: 2.1f, fireDelay: 0.29f));
        world.Events.Add(94.27f, () => boss?.SetTargetable(false));
    }

    // Omega-F's auto on its current target, with any of its three swings.
    private void AutoAttack(float animationLock)
    {
        if (boss is not { IsActive: true, IsCasting: false } || bossTarget is not { } target || !target.IsAlive()) return;
        boss.SetTarget(target, follow: false);
        boss.Face(target.Position);
        boss.Cast(ActionId.Unknown7c02, castSeconds: 0f, targetId: target.GameObjectId, animationVariation: (byte)state.Rng.NextInt(3), animationLock: animationLock);
    }

    private void CastSolarRay()
    {
        if (boss is not { IsActive: true } || bossTarget is not { } target || !target.IsAlive()) return;
        boss.SetTarget(target, follow: false);
        boss.Face(target.Position);
        target.AttachLockonVfx(LockonId.SolarRay, persistent: false);
        boss.Cast(Actions.SolarRay, target);
    }

    private void SwapToOffTank()
    {
        if (party.Get(PartyRole.OffTank) is { } offTank && offTank.IsAlive()) bossTarget = offTank;
        if (boss is not { IsActive: true } || bossTarget is not { } target) return;
        boss.SetTarget(target, follow: false);
        boss.Face(target.Position);
    }

    private void SolarRayFollowUp()
    {
        if (boss is not { IsActive: true } || bossTarget is not { } target || !target.IsAlive()) return;
        boss.Cast(Actions.SolarRayFollowUp, target);
    }

    // The edge comes down ahead of the closing knockback, and with it the death wall.
    private void LowerArenaEdge()
    {
        world.Map.AddEffect(0x00080004, 0x00);
        world.LowerArenaBoundary();
    }

    private void Run_BlindFaith()
    {
        SimEnemy? helper = null;
        world.Events.Add(94.27f, () => helper = omegaFHelpers?.At(0, new Placement(Vector3.Zero, 0f)));
        world.Events.Add(94.36f, () => helper?.Cast(BlindFaith));
    }

    private static readonly EnemyAction BlindFaith = new(ActionId.BlindFaithSuccess)
    {
        Cast = new() { CastTime = 0.9f, AnimationLock = 1.1f },
        Effects = [Damage(Actions.Magic), new ThrownToTheWall()],
        Timing = new() { VfxOffset = 0.26f, ResolveOffset = 0.26f },
    };

    // Blind Faith throws everyone the way its caster faces until the arena wall stops them,
    // and leaves them Down for the Count.
    private sealed class ThrownToTheWall : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx)
        {
            KnockbackLookup.TryGet(KnockbackId.BlindFaith, out var distance, out var speed);
            var heading = ctx.Caster.Rotation;
            var dirX = MathF.Sin(heading);
            var dirZ = MathF.Cos(heading);
            foreach (var target in ctx.Hits)
            {
                if (ctx.IsKilled(target) || !target.IsAlive() || target is not ISimPartyMember member) continue;
                var x = target.Position.X;
                var z = target.Position.Z;
                var along = x * dirX + z * dirZ;
                var toWall = MathF.Sqrt(MathF.Max(0f, along * along - (x * x + z * z - ArenaWallRadius * ArenaWallRadius))) - along;
                member.PushInDirection(heading, Math.Clamp(toWall, 0f, distance), speed);
                target.AddStatus(StatusId.DownForTheCount, stacks: KnockoutLoopTimeline);
            }
        }
    }

    // The legs Omega-F and the shield Omega-M take their form while still hidden. The flags
    // go in at spawn rather than with the model state: only the spawn's reach a peer.
    private static bool HasForm(OmegaAttack attack) => attack == OmegaAttack.Legs || attack == OmegaAttack.Shield;

    private static byte FormFlags(OmegaAttack attack) => HasForm(attack) ? (byte)0x31 : (byte)0x10;

    private static void LookAt(SimEnemy? enemy, SimCharacter? target)
    {
        if (enemy is { IsActive: true } && target != null) enemy.SetTarget(target, follow: false);
    }

    private void Run_Omega_F_4000A72A()
    {
        uint[] bNpcBaseIds = [BNpcBaseId.OmegaFDynamis, BNpcBaseId.OmegaM_3D69, BNpcBaseId.OmegaFDynamis, BNpcBaseId.OmegaM_3D69];
        uint[] bNpcNameIds = [BNpcNameId.OmegaFDynamis, BNpcNameId.OmegaMDynamis, BNpcNameId.OmegaFDynamis, BNpcNameId.OmegaMDynamis];
        float[] warpIn = [11.35f, 11.35f, 15.30f, 15.30f];
        float[] shown = [11.44f, 11.44f, 15.39f, 15.39f];
        float[] lookAt = [13.47f, 13.47f, 16.50f, 16.50f];
        float[] attackAt = [23.01f, 23.01f, 26.98f, 26.98f];
        float[] warpOut = [31.66f, 31.66f, 31.61f, 31.61f];
        for (var i = 0; i < 4; i++)
        {
            var baseId = bNpcBaseIds[i];
            var nameId = bNpcNameIds[i];
            var attack = state.OmegaAttacks[i];
            var direction = state.AttackDirections[i];
            SimEnemy? omega_F_4000A72A = null;
            world.Events.Add(1.39f, () => omega_F_4000A72A = world.SpawnEnemy(new EnemySpawnConfig(InitialModeAttributeFlags: FormFlags(attack), BNpcBaseId: baseId, NameId: nameId, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: direction.Apply(new Placement(new Vector3(0, 0, -10f), 0)))));
            if (HasForm(attack)) world.Events.Add(9.23f, () => omega_F_4000A72A?.SetModelState(0x04));
            world.Events.Add(warpIn[i], () => omega_F_4000A72A?.PlayActionTimeline(TimelineId.Spawn));
            world.Events.Add(shown[i], () => omega_F_4000A72A?.SetVisible(true));
            world.Events.Add(lookAt[i], () => LookAt(omega_F_4000A72A, party.Get(PartyRole.RegenHealer)));
            world.Events.Add(attackAt[i], () => omega_F_4000A72A?.Cast(attack.Action));
            world.Events.Add(warpOut[i], () => omega_F_4000A72A?.PlayActionTimeline(TimelineId.WarpOut));
            world.Events.Add(34.09f, () => omega_F_4000A72A?.Despawn());
        }
    }

    private void Run_Omega_4000A72E()
    {
        SimEnemy? omega_4000A72E = null;
        SimTether? tether1 = null;
        SimTether? tether2 = null;
        var placement =state.BettleSpawnDirection.Apply(new Placement(new Vector3(0.000f, 0.000f, -20.000f), 0));
        world.Events.Add(1.39f, () => omega_4000A72E = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.BeetleHelper, NameId: BNpcNameId.OmegaBeetle, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: placement)));
        // UNVERIFIED: the spawn packet's AnimationState 0x10 read as nibbles, set 1 = 1.
        world.Events.Add(1.89f, () => omega_4000A72E?.SetAnimationState(1, 1));
        world.Events.Add(41.34f, () => omega_4000A72E?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(41.34f, () => omega_4000A72E?.SetVisible(true));
        world.Events.Add(43.16f, () => LookAt(omega_4000A72E, party.Get(PartyRole.RegenHealer)));
        world.Events.Add(45.35f, () => tether1 = world.Tether(End.Passable(state.BlasterTetherTargets.Get(0)), omega_4000A72E, TetherId.PassableTether));
        world.Events.Add(45.35f, () => tether2 = world.Tether(End.Passable(state.BlasterTetherTargets.Get(1)), omega_4000A72E, TetherId.PassableTether));
        world.Events.Add(45.48f, () => omega_4000A72E?.Cast(ActionId.OmegaBlaster, castSeconds: 11.6f, targetId: omega_4000A72E?.GameObjectId, animationLock: 0.2f, fireDelay: 0.26f));
        world.Events.Add(57.57f, () => omega_4000A72E?.Cast(ActionId.BlasterEffect, castSeconds: 0f, targetId: omega_4000A72E?.GameObjectId, animationLock: 3.1f));
        world.Events.Add(60.65f, () => omega_4000A72E?.PlayActionTimeline(TimelineId.WarpOut));
        // world.Events.Add(61.44f, () => omega_4000A72E?.SetVisible(false));
        world.Events.Add(62.92f, () => omega_4000A72E?.Despawn());

        var helpers = new SimEnemy?[2];
        for (int index = 0; index < helpers.Length; index++)
        {
            var i = index;
            world.Events.Add(57.52f, () => helpers[i] = blasterHelpers?.At(i, placement));
        }
        world.Events.Add(57.61f, () => FireBlaster(helpers, [tether1, tether2]));
    }

    // Each tether holder takes a 15y Blaster. Resolved one at a time, so anyone standing in
    // both is hit again under the first one's Magic Vulnerability Up.
    private static void FireBlaster(SimEnemy?[] helpers, SimTether?[] tethers)
    {
        for (var i = 0; i < tethers.Length; i++)
        {
            if (tethers[i] is not { } tether) continue;
            var holder = tether.A;
            tether.Despawn();
            if (holder is null || !holder.IsAlive()) continue;
            helpers[i]?.Cast(Actions.Blaster, holder);
        }
    }

    private void Run_Omega_4000A72F(bool solo)
    {
        SimEnemy? omega_4000A72F = null;
        var waveCannonId = state.FirstWaveCannonFront ? ActionId.OmegaDiffuseWaveCannonFront : ActionId.OmegaDiffuseWaveCannonSides;
        var repCannonId = state.FirstWaveCannonFront ? ActionId.OmegaDiffuseWaveCannonRepeatSides : ActionId.OmegaDiffuseWaveCannonRepeatFront;
        world.Events.Add(1.39f, () => omega_4000A72F = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.FinalHelper, NameId: BNpcNameId.OmegaFinal, Level: 90, Targetable: false, EnemyList: EnemyListMode.OnlyWhenVisible, IsVisible: false, Placement: new Placement(new Vector3(0.000f, -0.000f, 0.000f), 3.140f))));
        world.Events.Add(11.34f, () => omega_4000A72F?.PlayActionTimeline(TimelineId.Spawn));
        world.Events.Add(11.44f, () => omega_4000A72F?.SetVisible(true));
        world.Events.Add(13.47f, () => LookAt(omega_4000A72F, party.Get(PartyRole.RegenHealer)));
        world.Events.Add(15.47f, () => omega_4000A72F?.Cast(waveCannonId, castSeconds: 7.7f, targetId: omega_4000A72F?.GameObjectId, animationLock: 4.1f, fireDelay: 0.28f));
        world.Events.Add(27.56f, () => omega_4000A72F?.Cast(repCannonId, castSeconds: 0f, targetId: omega_4000A72F?.GameObjectId, animationLock: 3.1f));
        if (!solo)
            world.Events.Add(31.70f, () => omega_4000A72F?.Cast(state.MonitorSide.ActionId, castSeconds: 9.7f, targetId: omega_4000A72F?.GameObjectId, animationLock: 3.1f, fireDelay: 0.29f));
        world.Events.Add(44.86f, () => omega_4000A72F?.PlayActionTimeline(TimelineId.WarpOut));
        world.Events.Add(47.28f, () => omega_4000A72F?.Despawn());
    }

    private void Run_Omega_4000A40B_1()
    {
        float[] spawnAt = [23.50f, 23.50f, 27.56f, 27.56f];
        float[] castAt = [23.59f, 23.59f, 27.65f, 27.65f];
        float[] orientations = state.FirstWaveCannonFront
            ? [0, MathF.PI, MathF.PI / 2, -MathF.PI / 2]
            : [MathF.PI / 2, -MathF.PI / 2, 0, MathF.PI];

        for(var i = 0; i < 4; i++)
        {
            var orientation = orientations[i];
            SimEnemy? omega_4000A40B_1 = null;
            var index = i;
            world.Events.Add(spawnAt[i], () => omega_4000A40B_1 = finalHelpers?.At(index, new Placement(Vector3.Zero, orientation)));
            world.Events.Add(castAt[i], () => omega_4000A40B_1?.Cast(Actions.DiffuseWaveCannon));
        }
    }

    private void Run_Omega_4000A409_1()
    {
        world.Events.Add(41.79f, FireMonitor);
    }

    // Final Omega's monitor fires on everyone on its side, each target at the centre of its
    // own 7y circle. Resolved one at a time, so a second hit lands on the first one's Magic
    // Vulnerability Up.
    private void FireMonitor()
    {
        var finalOmega = new Placement(Vector3.Zero, MathF.PI);
        var rightX = -MathF.Cos(finalOmega.Rotation);
        var rightZ = MathF.Sin(finalOmega.Rotation);
        var targets = party.ActiveMembers()
                           .Where(member => ((member.Position.X - finalOmega.Position.X) * rightX + (member.Position.Z - finalOmega.Position.Z) * rightZ) * state.MonitorSide.Mul < 0f)
                           .ToList();
        foreach (var target in targets)
            finalHelpers?.Next(finalOmega)?.Cast(Actions.OversampledWaveCannon, target);
    }


    private void Run_Omega_F_4000A40B_2(bool solo)
    {
        var firstLegs = state.OmegaAttacks[0] == OmegaAttack.Legs;
        var secondLegs = state.OmegaAttacks[2] == OmegaAttack.Legs;
        EnemyAction?[] superliminalSteel =
        [
            firstLegs ? Actions.SuperliminalSteelR : null, firstLegs ? Actions.SuperliminalSteelL : null,
            secondLegs ? Actions.SuperliminalSteelR : null, secondLegs ? Actions.SuperliminalSteelL : null,
            null, null,
        ];
        Vector3[] superliminalTargets = [Geometry.LegsSideTargetR, Geometry.LegsSideTargetL, Geometry.LegsSideTargetR, Geometry.LegsSideTargetL, default, default];
        Direction[] superliminalDirections = [state.AttackDirections[0], state.AttackDirections[0], state.AttackDirections[2], state.AttackDirections[2], Direction.N, Direction.N];
        float[] spawnAt = [22.92f, 22.92f, 26.89f, 26.89f, 26.89f, 26.89f];
        float[] superliminalAt = [23.01f, 23.01f, 26.98f, 26.98f, 0, 0];
        int[] helloWorldHit = [0, 0, 1, 1, 2, 2];
        float[] helloWorld1MoveAt = [41.21f, 42.23f, 43.21f];
        float[] helloWorld1HitAt = [41.30f, 42.32f, 43.30f];
        float[] helloWorld2MoveAt = [59.21f, 60.24f, 61.22f];
        float[] helloWorld2HitAt = [59.30f, 60.33f, 61.31f];
        var nearHelper1 = new HelloWorld(party, state.HelloWorldTargets[0], true);
        var farHelper1 = new HelloWorld(party, state.HelloWorldTargets[1], false);
        var nearHelper2 = new HelloWorld(party, state.HelloWorldTargets[2], true);
        var farHelper2 = new HelloWorld(party, state.HelloWorldTargets[3], false);

        for (int i = 0; i < 6; i++)
        {
            SimEnemy? helloWorld1Helper = null;
            SimEnemy? helloWorld2Helper = null;
            var index = i;
            var direction = superliminalDirections[i];
            var steel = superliminalSteel[i];
            var target = superliminalTargets[i];
            var helper1 = i % 2 == 0 ? nearHelper1 : farHelper1;
            var helper2 = i  % 2 == 0 ? nearHelper2 : farHelper2;
            var hit = helloWorldHit[i];

            if (steel != null)
            {
                world.Events.Add(spawnAt[i], () => omegaFHelpers?.At(index, direction.Apply(Geometry.LegsSideHelperPlacement)));
                world.Events.Add(superliminalAt[i], () => omegaFHelpers?.At(index)?.Cast(steel, direction.Apply(target)));
            }

            if(solo) continue;
            world.Events.Add(helloWorld1MoveAt[hit], () => helper1.SetPosition(helloWorld1Helper = omegaFHelpers?.At(index)));
            world.Events.Add(helloWorld1HitAt[hit], () => helper1.CastSpell(helloWorld1Helper));

            world.Events.Add(helloWorld2MoveAt[hit], () => helper2.SetPosition(helloWorld2Helper = omegaFHelpers?.At(index)));
            world.Events.Add(helloWorld2HitAt[hit], () => helper2.CastSpell(helloWorld2Helper));
        }
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP5OmegaAiReplayStateMessage(
            s.HelloWorldTargets.List, s.DoubleDynamicTargets.List, s.MonitorTargets.List, s.HelloWorld1JumpOrder.List,
            s.AttackDirections.Select(d => d.RadiansFromNorth).ToArray(), s.OmegaAttacks.ToArray(),
            s.BettleSpawnDirection.RadiansFromNorth, s.FirstWaveCannonFront, s.MonitorSide == MonitorSide.Left)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP5OmegaAiReplayStateMessage msg) return null;
        var shadowState = TopP5OmegaState.FromNetworkReplay(
            replayWorld.Party, msg.HelloWorldTargets, msg.DoubleDynamicTargets, msg.MonitorTargets, msg.HelloWorld1JumpOrder,
            msg.AttackDirectionsRadians,
            msg.OmegaAttacks, msg.BettleSpawnDirectionRadians, msg.FirstWaveCannonFront, msg.MonitorIsLeft);
        ((IScenarioAi<TopP5OmegaState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Edge-triggers BuildMidRunUpdateMessage -- see IMultiplayerReplayable.BuildMidRunUpdateMessage.
    private bool helloWorld2Broadcast;

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (helloWorld2Broadcast || LastState?.HelloWorld2 is not { } roles) return null;
        helloWorld2Broadcast = true;
        DiagnosticLog.Info($"[Multiplayer] Host: broadcasting P5 Omega HelloWorld2 update -- [{string.Join(",", roles)}].");
        return new TopP5OmegaHelloWorld2UpdateMessage(roles);
    }

    public void ApplyMidRunUpdate(object shadowStateObj, MpMessage message)
    {
        if (shadowStateObj is TopP5OmegaState shadowState && message is TopP5OmegaHelloWorld2UpdateMessage update)
            shadowState.HelloWorld2 = update.Roles;
    }
}
