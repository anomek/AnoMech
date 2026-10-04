using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Multiplayer;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P6AlphaOmega.TopP6AlphaOmegaConstants;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public enum LimitBreakKind { Tank, Healer, Melee, PhysRanged, Caster }

public sealed class TopP6AlphaOmegaScenario : IMultiplayerReplayable, IPartyLimitBreakScenario
{
    public string Name => "Alpha Omega";
    // A practice past the opening finds the arena edge already up.
    public IPhase Phase => Practiced.Start > 0f ? TopZone.P6Continued : TopZone.P6;
    public bool SupportsMultiplayer => true;
    public float BgmSecondsAtStart => 9.73f + Practiced.Start;
    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsSummary => settingsWindow.Summary();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    private readonly TopP6AlphaOmegaSettingsWindow settingsWindow = new();

    public IReadOnlyList<IScenarioAi> AiStrats => [new TopP6AlphaOmegaAi()];

    private SimWorld world = null!;
    private SimParty party = null!;
    private TopP6AlphaOmegaState state = null!;
    private TopP6AlphaOmegaMechanics mechanics = null!;

    public TopP6AlphaOmegaState? LastState { get; private set; }

    private SimEnemy? Boss => mechanics.Boss;
    private readonly SimEnemy?[] comets = new SimEnemy?[6];
    private readonly SimEnemy?[] meteors = new SimEnemy?[2];
    private readonly List<Vector3> puddleSpots = [];
    private readonly List<SimEnemy> puddleHelpers = [];
    private int cosmoDives;
    private float elapsed;
    private float realElapsed;
    private TopP6AlphaOmegaGauge gauge = new();
    private float limitBreakLandsAt;
    private P6PracticeEntry practice = TopP6AlphaOmegaPractice.Of(P6Practice.WholePhase);
    private TopP6AlphaOmegaFastForward fastForward = new(0f);
    private P6Practice section;

    private P6PracticeEntry Practiced => TopP6AlphaOmegaPractice.Of(settingsWindow.Overrides.Practice);

    // Alpha Omega turns north for Cosmo Meteor and doesn't turn again for the rest of the phase.
    private const float CosmoMeteorFacesNorth = 172.59f;
    // Cosmo Meteor's puddles drop where everyone stands at 177.66; a melee still locked into its
    // LB3 12y out past this can't reach the huddle (1.8s at run speed) and drops one on the party.
    private const float MeleeLimitBreakDoneBy = 175.5f;

    // Comets by the order they fell (NW, W, NE, SW, E, SE), each with its map-effect slot; the
    // meteors are N then S.
    private static readonly (Vector3 Position, float Rotation, byte Slot)[] CometSpots =
    [
        (new(-6.5f, 0f, -11.26f), 0.5235f, 0x11),
        (new(-13f, 0f, 0f), 1.5708f, 0x10),
        (new(6.5f, 0f, -11.26f), -0.5236f, 0x0C),
        (new(-6.5f, 0f, 11.26f), 2.6179f, 0x0F),
        (new(13f, 0f, 0f), -1.5709f, 0x0D),
        (new(6.5f, 0f, 11.26f), -2.618f, 0x0E),
    ];
    private static readonly (Vector3 Position, byte Slot)[] MeteorSpots = [(new(0f, 0f, -10f), 0x12), (new(0f, 0f, 10f), 0x13)];

    // Cosmo Meteor's puddle helpers stay where they were placed and cast the rest of it: each
    // round's leading spreads from the last four, the trailing ones from the first four, the stack
    // from the fifth and the flares from the last three.
    private const int LeadingSpreadHelper = 4;
    private const int StackHelper = 4;
    private const int FirstFlareHelper = 5;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new TopP6AlphaOmegaState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        practice = TopP6AlphaOmegaPractice.Of(state.Practice);
        mechanics = new TopP6AlphaOmegaMechanics(world);
        mechanics.Damage.SetStatuses(DamageType.Magic, StatusId.MagicVulnerabilityUp);
        fastForward = new TopP6AlphaOmegaFastForward(practice.Start);
        Array.Clear(comets);
        Array.Clear(meteors);
        puddleSpots.Clear();
        puddleHelpers.Clear();
        cosmoDives = 0;
        elapsed = practice.Start;
        realElapsed = 0f;
        gauge = new TopP6AlphaOmegaGauge();
        limitBreakLandsAt = 0f;
        limitBreakDue = default;
        limitBreakDueSent = 0;
        magicNumberExpiry.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<TopP6AlphaOmegaState>)AiStrats[idx]).Run(state, world);

        if (practice.Start > 0f)
            world.Events.Add(practice.Start, PickUpAtPractice);
        else
        {
            world.Events.Add(0f, () => SpawnBoss(targetable: false));
            world.Events.Add(0f, mechanics.SpawnHelpers);
            world.Events.Add(0f, GrantQuickeningDynamis);
            world.Events.Add(0.04f, () => Boss?.SetTargetable(true));
        }

        Section(P6Practice.CosmoMemory);
        At(0.09f, () => Talk(BattleTalkId.IAmTheOmega));
        At(4.45f, mechanics.TargetMainTank);
        At(7.13f, () => Talk(BattleTalkId.PathThroughTheExpanse));
        At(7.22f, () => mechanics.BossCast(ActionId.CosmoMemory, castSeconds: 5.7f, fireDelay: 0.27f, animationLock: 4.1f));
        LimitBreakAt(8.73f, state.CosmoMemoryTank, LimitBreakKind.Tank);
        At(13.19f, () => ResolveLimitBreakRaidwide(ActionId.CosmoMemory, magicNumber: false));
        DynamisAt(15.47f);

        Section(P6Practice.CosmoArrowOne);
        At(17.34f, mechanics.BossAuto);
        FlashGaleAt(18.14f);
        At(20.45f, mechanics.BossAuto);
        FlashGaleAt(21.26f);
        At(21.48f, () => Talk(BattleTalkId.TenMillionSuns));
        At(21.57f, () => mechanics.CosmoArrow(state.Arrows[0]));
        At(36.62f, () => Talk(BattleTalkId.VelocityOfAWyrm));
        At(36.71f, () => mechanics.BossCast(ActionId.CosmoDive, castSeconds: 5.3f, fireDelay: 0.27f, animationLock: 9.1f));
        CosmoDiveAt(44.74f);

        Section(P6Practice.UnlimitedWaveCannonOne);
        At(51.46f, mechanics.BossAuto);
        FlashGaleAt(52.27f);
        At(54.58f, mechanics.BossAuto);
        FlashGaleAt(55.39f);
        At(55.61f, () => Talk(BattleTalkId.FlamesOfTheDragonstar));
        At(55.70f, () => UnlimitedWaveCannon(state.Exaflares[0]));

        Section(P6Practice.WaveCannonOne);
        At(78.87f, () => WaveCannon(0));

        Section(P6Practice.CosmoArrowTwo);
        At(94.33f, mechanics.BossAuto);
        FlashGaleAt(95.13f);
        At(97.45f, mechanics.BossAuto);
        FlashGaleAt(98.26f);
        At(98.57f, () => mechanics.CosmoArrow(state.Arrows[1]));
        At(112.68f, () => WaveCannon(1));

        Section(P6Practice.UnlimitedWaveCannonTwo);
        At(128.13f, mechanics.BossAuto);
        FlashGaleAt(128.94f);
        At(131.25f, mechanics.BossAuto);
        FlashGaleAt(132.06f);
        At(132.37f, () => UnlimitedWaveCannon(state.Exaflares[1]));

        Section(P6Practice.CosmoDiveTwo);
        At(150.54f, () => mechanics.BossCast(ActionId.CosmoDive, castSeconds: 5.3f, fireDelay: 0.27f, animationLock: 9.1f));
        ThirdBarAt(state.ThirdBarReadyAt);
        LimitBreakAt(156.38f, state.FirstMelee, LimitBreakKind.Melee, MeleeLimitBreakDoneBy);
        CosmoDiveAt(158.57f);
        At(165.20f, () => Talk(BattleTalkId.TestNearsItsConclusion));
        At(165.29f, mechanics.BossAuto);
        LimitBreakAt(165.74f, state.SecondMelee, LimitBreakKind.Melee, MeleeLimitBreakDoneBy);
        FlashGaleAt(166.09f);
        At(168.40f, mechanics.BossAuto);
        FlashGaleAt(169.21f);

        Section(P6Practice.CosmoMeteor);
        At(172.55f, () => Talk(BattleTalkId.ImpactInvitingExtinction));
        At(CosmoMeteorFacesNorth, mechanics.FaceNorthFromNowOn);
        At(172.64f, () => mechanics.BossCast(ActionId.CosmoMeteor, castSeconds: 4.7f, fireDelay: 0.28f, animationLock: 3.1f));
        // Cosmo Meteor's effect gives Alpha Omega its status and its model state; the ActorControl
        // repeating the state comes 2.5s later.
        At(177.62f, () => Boss?.AddStatus(StatusId.AlphaOmegaCosmoMeteor, stacks: 0x229, overrideStacks: true));
        At(177.62f, () => Boss?.SetModelState(0x1C));
        At(177.66f, CastMeteorPuddles);
        LimitBreakAt(180.65f, PartyRole.CasterDps, LimitBreakKind.Caster);
        At(180.69f, ShowCosmoAdds);
        At(181.00f, () => world.Map.AddEffect(0x00020001, TopZone.CosmoMeteorArenaSlot));
        At(181.09f, SpawnCosmoAdds);
        At(181.63f, ResolveMeteorPuddles);
        At(182.83f, () => MeteorSpreads(state.SpreadFirstWave[0], leadingWave: true));
        At(183.86f, () => MeteorSpreads(state.SpreadFirstWave[0], leadingWave: false));
        At(188.84f, () => MeteorSpreads(state.SpreadFirstWave[1], leadingWave: true));
        At(189.87f, () => MeteorSpreads(state.SpreadFirstWave[1], leadingWave: false));
        LimitBreakAt(190.00f, PartyRole.PhysRangedDps, LimitBreakKind.PhysRanged);
        At(192.76f, MarkFlares);
        At(193.06f, () => AddsEnrage(comets));
        At(199.59f, () => DespawnAdds(comets.Take(3)));
        At(199.96f, () => DespawnAdds(comets.Skip(3)));
        At(200.86f, ResolveFlaresAndStack);
        At(201.89f, () => Boss?.RemoveStatus(StatusId.AlphaOmegaCosmoMeteor));
        At(201.89f, () => mechanics.BossCast(ActionId.CosmoMeteorEnd, castSeconds: 0f, fireDelay: 0f, animationLock: 5.1f));
        At(201.89f, () => Boss?.SetModelState(0));
        At(203.19f, () => AddsEnrage(meteors));
        At(207.17f, () => DespawnAdds(meteors));

        Section(P6Practice.MagicNumberOne);
        At(207.94f, () => Talk(BattleTalkId.MostFeebleOfSpecies));
        At(208.03f, () => mechanics.BossCast(ActionId.MagicNumber, castSeconds: 4.7f, fireDelay: 0.29f, animationLock: 5.1f));
        LimitBreakAt(209.28f, state.MagicNumber1Tank, LimitBreakKind.Tank);
        At(213.02f, () => ResolveLimitBreakRaidwide(ActionId.MagicNumber, magicNumber: true));
        LimitBreakAt(215.42f, state.MagicNumber1Healer, LimitBreakKind.Healer);

        Section(P6Practice.MagicNumberTwo);
        At(224.11f, () => Talk(BattleTalkId.IncomprehensibleStrength));
        At(224.20f, () => mechanics.BossCast(ActionId.MagicNumber, castSeconds: 4.7f, fireDelay: 0.29f, animationLock: 5.1f));
        LimitBreakAt(225.09f, state.MagicNumber2Tank, LimitBreakKind.Tank);
        At(229.19f, () => ResolveLimitBreakRaidwide(ActionId.MagicNumber, magicNumber: true));
        LimitBreakAt(231.20f, state.MagicNumber2Healer, LimitBreakKind.Healer);

        Section(P6Practice.RunDynamis);
        At(238.28f, () => Talk(BattleTalkId.UnmeasurableMight));
        At(238.37f, () => mechanics.BossCast(ActionId.RunMi, castSeconds: 15.7f, fireDelay: 0.30f, animationLock: 16.1f));
        At(248.35f, () => Talk(BattleTalkId.ClaimingThisVictory));
        if (state.EnrageMelee is { } melee) LimitBreakAt(253.12f, melee, LimitBreakKind.Melee);
        At(254.37f, ShowRunMiHit);
        At(267.58f, () => Boss?.PlayDeath());
        At(267.61f, () => world.Map.AddEffect(0x00080004, 0x00));
        At(267.61f, ClearDynamis);

        foreach (var (t, units) in LimitBreakGaugeFills)
            GaugeFillAt(t, units);
        if (practice.Start > 0f) world.Events.Advance(practice.Start);
    }

    // Host and peer alike: the gimmick timelines, and a gauge full enough for whichever limit
    // break this seat owes. A practice after Cosmo Meteor starts on the arena it leaves.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(GimmickTimelines, "TopP6AlphaOmega");
        instanceWorld.Party.LimitBreak.Set(3f);
        instanceWorld.Party.LimitBreak.Landed += OnLocalLimitBreak;
        if (Practiced.Practice > P6Practice.CosmoMeteor)
            instanceWorld.Events.Add(0f, () => instanceWorld.Map.AddEffect(0x00010001, TopZone.CosmoMeteorArenaSlot, broadcast: false));
    }

    private void Section(P6Practice mechanic) => section = mechanic;

    private bool Covered => practice.Practice == P6Practice.WholePhase || practice.Practice == section;

    private void At(float time, Action action)
    {
        if (Covered) world.Events.Add(time, action);
    }

    private void FlashGaleAt(float time)
    {
        if (Covered) world.Events.Add(time, mechanics.FlashGale);
    }

    private void CosmoDiveAt(float time)
    {
        if (Covered) world.Events.Add(time, ResolveCosmoDive);
        else if (time < practice.Start) cosmoDives++;
    }

    private void LimitBreakAt(float time, PartyRole role, LimitBreakKind kind, float doneBy = float.MaxValue)
    {
        if (Covered) world.Events.Add(time, () => BotLimitBreak(role, kind, TargetOf(kind), doneBy: doneBy));
        else if (time < practice.Start) fastForward.Press(time, role, kind);
    }

    private void DynamisAt(float time)
    {
        if (Covered) world.Events.Add(time, mechanics.ApplyDynamis);
        else if (time < practice.Start) fastForward.Dynamis(time);
    }

    private void ThirdBarAt(float time)
    {
        if (Covered) world.Events.Add(time, ReleaseThirdBar);
        else if (time < practice.Start) fastForward.ThirdBar(time);
    }

    private void GaugeFillAt(float time, float units)
    {
        Section(TopP6AlphaOmegaPractice.MechanicAt(time));
        if (Covered) world.Events.Add(time, () => AddGauge(units));
        else if (time < practice.Start) fastForward.Fill(time, units);
    }

    private SimCharacter? TargetOf(LimitBreakKind kind) => kind is LimitBreakKind.Melee or LimitBreakKind.PhysRanged ? Boss : null;

    // A caster's LB3 goes on the ground at the centre, where every comet and meteor is in reach.
    private static readonly Vector3 CasterLimitBreakSpot = Vector3.Zero;

    private static Vector3? LocationOf(LimitBreakKind kind) => kind == LimitBreakKind.Caster ? CasterLimitBreakSpot : null;

    private void PickUpAtPractice()
    {
        SpawnBoss(targetable: true);
        mechanics.SpawnHelpers();
        mechanics.TargetMainTank();
        if (practice.Start > CosmoMeteorFacesNorth) mechanics.FaceNorthFromNowOn();
        fastForward.Run();
        gauge = fastForward.Gauge;
        limitBreakLandsAt = fastForward.LandsAt;
        PushGauge();
        if (fastForward.DynamisGiven)
        {
            Boss?.AddStatus(StatusId.CodeMi);
            foreach (var member in party.ActiveMembers().ToList())
                member.AddStatus(member is ISimPartyMember { Role: var role } && fastForward.Spark.Contains(role)
                    ? StatusId.SparkOfDynamis
                    : StatusId.BrilliantDynamis);
        }
        else
            GrantQuickeningDynamis();
        foreach (var owed in fastForward.Owed)
            world.Events.Add(MathF.Max(0f, owed.At - world.Events.Elapsed), () => PayOwed(owed));
        DiagnosticLog.Info($"[TopP6AlphaOmega] Practice {practice.Label} from {practice.Start:F2}s: gauge {gauge.Units:F0} (third bar held: {gauge.ThirdBarHeld}), "
            + $"Spark on [{string.Join(",", fastForward.Spark)}], {fastForward.Owed.Count} owed ({string.Join(", ", fastForward.Owed.Select(o => $"{o.Step} {o.Role} at {o.At:F2}"))}).");
    }

    private void PayOwed(TopP6AlphaOmegaFastForward.Pending owed)
    {
        switch (owed.Step)
        {
            case TopP6AlphaOmegaFastForward.Step.Fill:
                AddGauge(owed.Units);
                break;
            case TopP6AlphaOmegaFastForward.Step.Dynamis:
                mechanics.ApplyDynamis();
                break;
            case TopP6AlphaOmegaFastForward.Step.ThirdBar:
                ReleaseThirdBar();
                break;
            case TopP6AlphaOmegaFastForward.Step.Press:
                BotLimitBreak(owed.Role, owed.Kind, TargetOf(owed.Kind), owed.Waited, pressedBeforeStart: true);
                break;
            case TopP6AlphaOmegaFastForward.Step.Land:
                LandLimitBreak(owed.Role, owed.Kind, TargetOf(owed.Kind));
                break;
            case TopP6AlphaOmegaFastForward.Step.Refund:
                if (party.Get(owed.Role) is { } user) RefundLimitBreak(user, owed.Units);
                break;
        }
    }

    // The gauge follows the event clock, which the debug speed buttons scale; Magic Number counts
    // down in real time like every status.
    public void Tick(float delta, float time)
    {
        elapsed = world.Events.Elapsed;
        realElapsed = time;
        mechanics.FaceTarget();
        ExpireMagicNumbers();
        TickGauge();
    }

    private void Talk(uint textId) => world.Map.BattleTalk(BNpcNameId.AlphaOmega, textId, 6000);

    private void SpawnBoss(bool targetable)
    {
        mechanics.SpawnBoss(targetable);
        Boss?.SetMaxHealth(BossMaxHealth);
        Boss?.SetHealth(BossMaxHealth);
    }

    // Everyone comes out of P5 on three stacks, until Brilliant Dynamis takes their place.
    private void GrantQuickeningDynamis()
    {
        foreach (var member in party.ActiveMembers().ToList())
            member.AddStatus(StatusId.QuickeningDynamis, stacks: 3, overrideStacks: true);
    }

    private static bool HasTankLimitBreak(SimCharacter member)
        => member.HasStatus(196) || member.HasStatus(863) || member.HasStatus(864) || member.HasStatus(1931);

    private static bool IsInvulnerable(SimCharacter member) => member.ActiveStatusSnapshot.Any(s => Mitigation.IsInvuln(s.StatusId));

    private void ClearDynamis()
    {
        foreach (var member in party.ActiveMembers().ToList())
        {
            member.RemoveStatus(StatusId.BrilliantDynamis);
            member.RemoveStatus(StatusId.SparkOfDynamis);
        }
    }

    // Run: ****mi* reports 9,999,999 on everyone, and the result that would carry it out comes
    // long after the boss has died.
    private void ShowRunMiHit()
    {
        var name = ActionLookup.Name(ActionId.RunMi);
        foreach (var member in party.ActiveMembers().ToList())
            member.Proxy?.ShowFlyText(9_999_999, name);
    }

    // Its busters go on the two nearest; the stack on one of the rest.
    private void ResolveCosmoDive()
    {
        var dive = Math.Min(cosmoDives++, CosmoDiveRequiredMitigation.Length - 1);
        if (Boss is not { } boss) return;
        var byDistance = party.ActiveMembers().OrderBy(m => Vector3.DistanceSquared(m.Position, boss.Position)).ToList();
        if (byDistance.Count == 0) return;
        var busted = byDistance.Take(2).ToList();
        var others = byDistance.Skip(2).ToList();
        var stackTarget = others.Count > 0 ? others[state.Rng.NextInt(others.Count)] : null;
        foreach (var target in busted)
        {
            mechanics.Helpers?.Next(boss.Placement())?.Cast(ActionId.CosmoDive_7BA7, castSeconds: 0f, targetId: target.GameObjectId, animationLock: 1.1f);
            mechanics.Damage.Resolve(target, ActionId.CosmoDive_7BA7, [DamageType.TankBuster, DamageType.Magic],
                [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)], requiredMitigation: CosmoDiveRequiredMitigation[dive]);
        }
        if (stackTarget == null) return;
        mechanics.Helpers?.Next(boss.Placement())?.Cast(ActionId.CosmoDive_7BA8, castSeconds: 0f, targetId: stackTarget.GameObjectId, animationLock: 1.1f);
        mechanics.Damage.Resolve(stackTarget, ActionId.CosmoDive_7BA8, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)],
            stackMinTargets: 6);
    }

    private void WaveCannon(int index)
        => mechanics.WaveCannon(state.ProteanFirstWave[index], state.WildChargeTargets[index], WildChargeRequiredMitigation[index]);

    private static Vector3 Compass(int octant, float radius)
    {
        var angle = octant * MathF.PI / 4f;
        return new Vector3(MathF.Sin(angle) * radius, 0f, -MathF.Cos(angle) * radius);
    }

    // Four exaflares from 24y out, about 1s apart, then puddles under everyone every 2s. After a
    // line's first explosion its next comes 1.11s later, then one a second.
    private static readonly float[] ExaflareStarts = [0f, 1.025f, 2.005f, 3.03f];

    private void UnlimitedWaveCannon(ExaflareSweep sweep)
    {
        mechanics.BossCast(ActionId.UnlimitedWaveCannon, castSeconds: 4.7f, fireDelay: 0.29f, animationLock: 5.1f);
        for (var i = 0; i < 4; i++)
        {
            var octant = sweep.Octant(i);
            var rotation = -octant * MathF.PI / 4f;
            var start = ExaflareStarts[i];
            world.Events.Add(start, () => mechanics.HelperCast(new Placement(Compass(octant, 24f), rotation), ActionId.WaveCannon_7BAD,
                castSeconds: 11.7f, fireDelay: 0.295f, animationLock: 1.1f));
            for (var k = 0; k < 7; k++)
            {
                var radius = 24f - 8f * k;
                var at = start + 11.995f + (k == 0 ? 0f : 1.114f + 0.998f * (k - 1));
                var first = k == 0;
                world.Events.Add(at, () => Exaflare(Compass(octant, radius), rotation, first));
            }
        }
        for (var wave = 0; wave < 6; wave++)
            world.Events.Add(10.033f + 2.005f * wave, CastWaveCannonPuddles);
    }

    private void Exaflare(Vector3 position, float rotation, bool first)
    {
        var placement = new Placement(position, first ? rotation : ExaflareRestHeading(rotation));
        if (!first) mechanics.HelperEffect(placement, ActionId.WaveCannon_7BAE, 1.1f);
        mechanics.Damage.Resolve(TopPositioned.From(placement), ActionId.WaveCannon_7BAE, [DamageType.Lethal], []);
    }

    // The explosions after a line's first face along its axis, folded into [-pi, 0].
    private static float ExaflareRestHeading(float heading)
    {
        var folded = MathF.IEEERemainder(heading, MathF.Tau);
        if (folded < -MathF.PI + 0.001f) folded += MathF.Tau;
        return folded > 0.001f ? folded - MathF.PI : folded;
    }

    private void CastWaveCannonPuddles()
    {
        foreach (var member in party.ActiveMembers().ToList())
        {
            var spot = new Placement(member.Position, 0f);
            mechanics.HelperCast(spot, ActionId.WaveCannon_7BAF, castSeconds: 2.7f, fireDelay: 0.287f, animationLock: 1.1f);
            world.Events.Add(2.987f, () => mechanics.Damage.Resolve(TopPositioned.From(spot), ActionId.WaveCannon_7BAF, [DamageType.Lethal], []));
        }
    }

    private void CastMeteorPuddles()
    {
        puddleSpots.Clear();
        puddleHelpers.Clear();
        foreach (var member in party.ActiveMembers().ToList())
        {
            puddleSpots.Add(member.Position);
            if (mechanics.HelperCast(new Placement(member.Position, 0f), ActionId.CosmoMeteorPuddle, castSeconds: 3.7f, fireDelay: 0.265f,
                    animationLock: 1.1f) is { } helper)
                puddleHelpers.Add(helper);
        }
    }

    private SimEnemy? PuddleHelper(int index)
        => puddleHelpers.Count > 0 && puddleHelpers[index % puddleHelpers.Count] is { IsActive: true } helper
            ? helper
            : mechanics.Helpers?.Next(new Placement(Vector3.Zero, 0f));

    private void ResolveMeteorPuddles()
    {
        foreach (var spot in puddleSpots)
            mechanics.Damage.Resolve(TopPositioned.From(new Placement(spot, 0f)), ActionId.CosmoMeteorPuddle, [DamageType.Lethal], []);
    }

    private void ShowCosmoAdds()
    {
        foreach (var (_, _, slot) in CometSpots) world.Map.AddEffect(0x00020004, slot);
        foreach (var (_, slot) in MeteorSpots) world.Map.AddEffect(0x00020004, slot);
    }

    private void SpawnCosmoAdds()
    {
        for (var i = 0; i < CometSpots.Length; i++)
            comets[i] = SpawnAdd(BNpcBaseId.CosmoComet, BNpcNameId.CosmoComet, CometMaxHealth, CometHitboxRadius,
                new Placement(CometSpots[i].Position, CometSpots[i].Rotation));
        for (var i = 0; i < MeteorSpots.Length; i++)
            meteors[i] = SpawnAdd(BNpcBaseId.CosmoMeteor, BNpcNameId.CosmoMeteor, MeteorMaxHealth, MeteorHitboxRadius,
                new Placement(MeteorSpots[i].Position, 0f));
    }

    private SimEnemy? SpawnAdd(uint baseId, uint nameId, uint maxHealth, float hitboxRadius, Placement placement)
    {
        var add = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            HitboxRadius: hitboxRadius,
            Placement: placement));
        add?.SetMaxHealth(maxHealth);
        add?.SetHealth(maxHealth);
        return add;
    }

    private void KillAdd(SimEnemy? add, byte slot)
    {
        if (!AddAlive(add)) return;
        add!.PlayDeath();
        world.Events.Add(0.04f, () => world.Map.AddEffect(0x00080004, slot));
    }

    private static bool AddAlive(SimEnemy? add) => add is { IsActive: true, Health: > 0 };

    // An LB3 reaches the adds its area touches as it lands: a caster's circle kills the comets and
    // cuts the meteors, a physical ranged one's line finishes the meteors. UNVERIFIED: whether the
    // line alone finishes a meteor the caster's missed.
    private void BreakCosmoAdds(LimitBreakKind kind, LimitBreakAim aim)
    {
        if (kind == LimitBreakKind.Caster)
        {
            if (aim.Location is not { } centre) return;
            for (var i = 0; i < meteors.Length; i++)
            {
                var meteor = meteors[i];
                if (!AddAlive(meteor) || !InCasterLimitBreak(centre, meteor!, MeteorHitboxRadius)) continue;
                var (after, hp) = MeteorsAfterCasterLimitBreak[i];
                world.Events.Add(after, () => { if (AddAlive(meteor)) meteor!.SetHealth(Math.Min(hp, meteor.Health)); });
            }
            for (var i = 0; i < comets.Length; i++)
            {
                var comet = comets[i];
                if (!AddAlive(comet) || !InCasterLimitBreak(centre, comet!, CometHitboxRadius)) continue;
                var slot = CometSpots[i].Slot;
                world.Events.Add(CometsFallAfter + CometFallStep * i, () => KillAdd(comet, slot));
            }
            return;
        }
        var line = new Placement(aim.Origin, aim.Heading);
        for (var i = 0; i < meteors.Length; i++)
        {
            var meteor = meteors[i];
            if (!AddAlive(meteor) || !InPhysRangedLimitBreak(line, meteor!, MeteorHitboxRadius)) continue;
            var slot = MeteorSpots[i].Slot;
            world.Events.Add(MeteorsFallAfter[i], () => KillAdd(meteor, slot));
        }
    }

    private static bool InCasterLimitBreak(Vector3 centre, SimEnemy add, float hitboxRadius)
        => Vector2.Distance(new Vector2(add.Position.X, add.Position.Z), new Vector2(centre.X, centre.Z)) <= CasterLimitBreakRadius + hitboxRadius;

    // The line runs forward from its user; the add is reached when its hitbox touches it.
    private static bool InPhysRangedLimitBreak(Placement line, SimEnemy add, float hitboxRadius)
    {
        var dx = add.Position.X - line.Position.X;
        var dz = add.Position.Z - line.Position.Z;
        var along = dx * MathF.Sin(line.Rotation) + dz * MathF.Cos(line.Rotation);
        var side = dx * MathF.Cos(line.Rotation) - dz * MathF.Sin(line.Rotation);
        var pastEnds = MathF.Max(0f, MathF.Max(-along, along - PhysRangedLimitBreakLength));
        var pastSides = MathF.Max(0f, MathF.Abs(side) - PhysRangedLimitBreakHalfWidth);
        return pastEnds * pastEnds + pastSides * pastSides <= hitboxRadius * hitboxRadius;
    }

    // Adds still standing at their enrage take the whole party with them.
    private void AddsEnrage(SimEnemy?[] adds)
    {
        var standing = adds.Where(AddAlive).ToList();
        if (standing.Count == 0) return;
        foreach (var add in standing)
            add!.Cast(ActionId.CosmoMeteorEnrage, castSeconds: 0f, targetId: add.GameObjectId, animationLock: 1.1f);
        mechanics.Damage.Resolve(standing[0], ActionId.CosmoMeteorEnrage, [DamageType.Lethal], []);
    }

    private static void DespawnAdds(IEnumerable<SimEnemy?> adds)
    {
        foreach (var add in adds) add?.Despawn();
    }

    // Four of each round's spreads land a second before the other four.
    private void MeteorSpreads(IReadOnlyList<PartyRole> leading, bool leadingWave)
    {
        var roles = leadingWave ? leading : PerRole.All.Except(leading).ToList();
        var targets = new List<SimCharacter>();
        for (var j = 0; j < roles.Count; j++)
        {
            if (party.Get(roles[j]) is not { } target || !target.IsAlive()) continue;
            PuddleHelper(leadingWave ? LeadingSpreadHelper + j : j)
                ?.Cast(ActionId.CosmoMeteorSpread, castSeconds: 0f, targetId: target.GameObjectId, animationLock: 1.1f);
            targets.Add(target);
        }
        foreach (var target in targets)
            mechanics.Damage.Resolve(target, ActionId.CosmoMeteorSpread, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)]);
    }

    private void MarkFlares()
    {
        foreach (var role in state.FlareTargets)
            party.Get(role)?.AttachLockonVfx(LockonId.OptimizedMeteor, persistent: false);
    }

    // The five without a flare share the stack; each flare kills anyone else inside FlareLethalRange.
    private void ResolveFlaresAndStack()
    {
        if (party.Get(state.StackTarget) is { } stackTarget && stackTarget.IsAlive())
        {
            PuddleHelper(StackHelper)?.Cast(ActionId.CosmoMeteorStack, castSeconds: 0f, targetId: stackTarget.GameObjectId, animationLock: 1.1f);
            mechanics.Damage.Resolve(stackTarget, ActionId.CosmoMeteorStack, [DamageType.Magic], [], stackMinTargets: 5);
        }
        var flares = state.FlareTargets.Select(role => party.Get(role)).ToList();
        for (var k = 0; k < flares.Count; k++)
        {
            if (flares[k] is not { } flare || !flare.IsAlive()) continue;
            PuddleHelper(FirstFlareHelper + k)?.Cast(ActionId.CosmoMeteorFlare, castSeconds: 0f, targetId: flare.GameObjectId, animationLock: 1.1f);
            // Two flare targets this close both die, though the later one takes nothing from the earlier flare.
            mechanics.Damage.Resolve(flare, ActionId.CosmoMeteorFlare, [DamageType.Magic], [], excludeTargets: [flare], lethalWithin: FlareLethalRange);
        }
    }

    private readonly Dictionary<SimCharacter, float> magicNumberExpiry = [];

    // Cosmo Memory and Magic Number: only a tank LB3 makes them survivable, and each player's
    // result (with Magic Number's debuff) arrives in turn.
    private void ResolveLimitBreakRaidwide(uint actionId, bool magicNumber)
    {
        var members = state.Rng.Shuffle(party.ActiveMembers().ToArray());
        var spared = members.Where(member => HasTankLimitBreak(member) || IsInvulnerable(member)).ToArray();
        var dead = mechanics.Damage.Resolve(mechanics.Boss, actionId, [DamageType.Lethal], [], excludeTargets: spared, killTargets: false);
        for (var k = 0; k < members.Count; k++)
        {
            var member = members[k];
            var at = RaidwideFirstResult + ResultStep * k;
            if (dead.Contains(member))
            {
                mechanics.Kill([member], actionId, "no tank limit break", at);
                continue;
            }
            if (!magicNumber || !HasTankLimitBreak(member)) continue;
            world.Events.Add(at, () =>
            {
                if (!member.IsAlive()) return;
                member.AddStatus(StatusId.MagicNumber, MagicNumberSeconds);
                magicNumberExpiry[member] = realElapsed + MagicNumberSeconds;
                // On the event clock too, so the run stays open until it runs out.
                world.Events.Add(MagicNumberSeconds, ExpireMagicNumbers);
            });
        }
    }

    // Only a healer limit break lifts it; running out kills.
    private void ExpireMagicNumbers()
    {
        foreach (var (member, expiry) in magicNumberExpiry.ToList())
        {
            if (!member.HasStatus(StatusId.MagicNumber) && realElapsed < expiry - 0.1f)
            {
                magicNumberExpiry.Remove(member);
                continue;
            }
            if (realElapsed < expiry) continue;
            magicNumberExpiry.Remove(member);
            if (member.IsAlive()) member.Die(SimCharacterDeathExtensions.NoAction, "Magic Number ran out (no healer limit break)");
        }
    }

    private static LimitBreakKind KindOf(uint actionId) => actionId switch
    {
        197 or 198 or 199 or 4240 or 4241 or 17105 => LimitBreakKind.Tank,
        206 or 207 or 208 or 4247 or 4248 or 24859 => LimitBreakKind.Healer,
        4238 or 4239 => LimitBreakKind.PhysRanged,
        203 or 204 => LimitBreakKind.Caster,
        _ when PhysRangedLimitBreakActionIds.Contains(actionId) => LimitBreakKind.PhysRanged,
        _ when CasterLimitBreakActionIds.Contains(actionId) => LimitBreakKind.Caster,
        _ => LimitBreakKind.Melee,
    };

    private static (float Bar, float Lands) CastOf(LimitBreakKind kind) => kind switch
    {
        LimitBreakKind.Tank => (0f, 0f),
        LimitBreakKind.Healer => (HealerLimitBreakBar, HealerLimitBreakLands),
        _ => (LimitBreak.DpsBar, LimitBreak.DpsLands),
    };

    internal static float LandsAfter(LimitBreakKind kind) => CastOf(kind).Lands;

    internal static float RefundDelay(LimitBreakKind kind) => kind switch
    {
        LimitBreakKind.Tank => 3.83f,
        LimitBreakKind.Healer => 6.01f,
        _ => 5.03f,
    };

    // A bot presses its LB3 once the gauge is full and no other limit break is on its way, waiting
    // for that as a player would, and stands still until its animation ends. A human's seat presses
    // its own, the debug bot through the client, unless a practice starts past the mechanic that
    // owed it, and then it lands unseen.
    private void BotLimitBreak(PartyRole role, LimitBreakKind kind, SimCharacter? target = null, float waited = 0f, bool pressedBeforeStart = false,
                               float doneBy = float.MaxValue)
    {
        if (party.Get(role) is not { } member || !member.IsAlive()) return;
        if (!pressedBeforeStart && member is not SimPartyNpc && !IsDebugBot(member) && member is not SimNetworkPuppet) return;
        if (!gauge.Full || elapsed < limitBreakLandsAt || party.Player?.IsLimitBreaking == true)
        {
            if (waited >= LimitBreakWaitLimit)
            {
                DiagnosticLog.Info($"[TopP6AlphaOmega] {role}'s {kind} limit break given up: the gauge is at {gauge.Units:F0} after {waited:F1}s.");
                return;
            }
            world.Events.Add(LimitBreakWaitStep, () => BotLimitBreak(role, kind, target, waited + LimitBreakWaitStep, pressedBeforeStart, doneBy));
            return;
        }
        if (waited > 0f) DiagnosticLog.Info($"[TopP6AlphaOmega] {role} waited {waited:F2}s for the LB3.");
        var lockedUntil = elapsed + CastOf(kind).Lands + (LimitBreak.ByJob.TryGetValue((member as ISimPartyMember)?.ClassJob ?? 0, out var job) ? LimitBreak.AnimationLockOf(job) : 0f);
        if (member is not SimNetworkPuppet && lockedUntil > doneBy)
        {
            DiagnosticLog.Info($"[TopP6AlphaOmega] {role} holds its {kind} LB3: pressed now it would still be locked at {lockedUntil:F2}s, past {doneBy:F2}s.");
            return;
        }
        if (!pressedBeforeStart && member is SimNetworkPuppet)
        {
            limitBreakDue = (role, kind, limitBreakDue.Seq + 1);
            return;
        }
        if (!pressedBeforeStart && member is SimPlayer)
        {
            PressAsDebugBot(world, kind, LimitBreakWaitLimit - waited);
            return;
        }
        var (bar, lands) = CastOf(kind);
        limitBreakLandsAt = elapsed + lands;
        if (member is SimPartyNpc npc && LimitBreak.ByJob.TryGetValue(npc.ClassJob, out var actionId))
            npc.PlayAction(actionId, LimitBreak.AnimationLockOf(actionId), bar, lands, target, LocationOf(kind), holdStill: true);
        world.Events.Add(lands, () => LandLimitBreak(role, kind, target));
    }

    private bool IsDebugBot(SimCharacter member) => ReferenceEquals(member, party.Player) && DebugBotControl.Enabled;

    // The debug bot presses its seat's LB3 through the client once it stands still, again while
    // the client refuses it, and holds still through its animation; what it does reaches the
    // scenario as the player's own limit break.
    private static void PressAsDebugBot(SimWorld on, LimitBreakKind kind, float patience)
    {
        if (!DebugBotControl.Enabled || on.Party.Player is not { } player || !player.IsAlive()) return;
        var location = LocationOf(kind) is { } local ? on.Coordinates.ToGlobal(local) : (Vector3?)null;
        SimCharacter? target = kind is LimitBreakKind.Melee or LimitBreakKind.PhysRanged
            ? on.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.AlphaOmega)
            : player;
        if (!player.HasMoveInFlight && target != null && Natives.PlayerInput.PressLimitBreakThree((ulong)target.GameObjectId, location))
        {
            var animationLock = LimitBreak.ByJob.TryGetValue((player as ISimPartyMember)?.ClassJob ?? 0, out var own) ? LimitBreak.AnimationLockOf(own) : 0f;
            player.HoldStill(CastOf(kind).Lands + animationLock);
            DiagnosticLog.Info($"[TopP6AlphaOmega] The debug bot pressed its {kind} LB3.");
            return;
        }
        if (patience <= 0f)
        {
            DiagnosticLog.Info($"[TopP6AlphaOmega] The debug bot's {kind} LB3 given up: the client kept refusing it.");
            return;
        }
        on.Events.Add(LimitBreakWaitStep, () => PressAsDebugBot(on, kind, patience - LimitBreakWaitStep));
    }

    // A peer's seat is pressed on that peer, by its debug bot when it drives the seat.
    private (PartyRole Role, LimitBreakKind Kind, int Seq) limitBreakDue;
    private int limitBreakDueSent;

    public MpMessage? BuildMidRunUpdateMessage()
    {
        if (limitBreakDue.Seq == limitBreakDueSent) return null;
        limitBreakDueSent = limitBreakDue.Seq;
        return new TopP6AlphaOmegaLimitBreakDueMessage(limitBreakDue.Role, limitBreakDue.Kind);
    }

    public void ApplyMidRunUpdate(object shadowState, MpMessage message)
    {
        if (message is not TopP6AlphaOmegaLimitBreakDueMessage due || !Enum.IsDefined(due.Kind)) return;
        if (Plugin.GameInstance?.World is not { } peerWorld || peerWorld.Party.PlayerRole != due.Role) return;
        PressAsDebugBot(peerWorld, due.Kind, LimitBreakWaitLimit);
    }

    private void LandLimitBreak(PartyRole role, LimitBreakKind kind, SimCharacter? target)
    {
        if (party.Get(role) is not { } member || !member.IsAlive()) return;
        if (!gauge.Full)
        {
            DiagnosticLog.Info($"[TopP6AlphaOmega] {role}'s {kind} limit break found the gauge spent as it landed.");
            return;
        }
        LimitBreakLanded(role, kind, 3, byBot: true, AimOf(member, target, kind));
    }

    // Where the sim puts an LB3 it presses: a caster's on the ground at the centre, the rest from
    // where its user stands toward its target.
    private static LimitBreakAim AimOf(SimCharacter user, SimCharacter? target, LimitBreakKind kind)
    {
        var aim = target == null ? user.Placement() : user.Placement().Face(target.Position);
        return new LimitBreakAim(aim.Position, aim.Rotation, LocationOf(kind));
    }

    // Spends the bars of its level; a Brilliant Dynamis holder gets those back and turns to Spark
    // of Dynamis. Only an LB3 does what the mechanics ask of one.
    private void LimitBreakLanded(PartyRole role, LimitBreakKind kind, int level, bool byBot, LimitBreakAim aim)
    {
        if (party.Get(role) is not { } user) return;
        DiagnosticLog.Info($"[TopP6AlphaOmega] {role}'s {kind} LB{level} landed{(byBot ? "" : " (a human's)")}, aimed from "
            + $"({aim.Origin.X:F2},{aim.Origin.Z:F2}) heading {aim.Heading:F3}{(aim.Location is { } at ? $" at ({at.X:F2},{at.Z:F2})" : "")}.");
        var bars = level * LimitBreak.GaugeBar;
        SpendGauge(bars);
        if (level == 3) ApplyLimitBreakThree(user, kind, byBot, aim);
        world.Events.Add(RefundDelay(kind), () => RefundLimitBreak(user, bars));
    }

    private void RefundLimitBreak(SimCharacter user, float bars)
    {
        if (!user.IsAlive() || !user.HasStatus(StatusId.BrilliantDynamis)) return;
        AddGauge(bars);
        user.RemoveStatus(StatusId.BrilliantDynamis);
        user.AddStatus(StatusId.SparkOfDynamis);
    }

    private void ApplyLimitBreakThree(SimCharacter user, LimitBreakKind kind, bool byBot, LimitBreakAim aim)
    {
        switch (kind)
        {
            case LimitBreakKind.Tank when byBot:
                ApplyTankLimitBreak(user);
                break;
            case LimitBreakKind.Healer:
                HealerLimitBreak();
                break;
            case LimitBreakKind.Caster or LimitBreakKind.PhysRanged:
                BreakCosmoAdds(kind, aim);
                break;
        }
    }

    // A human's own tank LB3 gets its mitigation from the engine as it presses; a bot's plays only
    // the animation, so it is granted here by the same rule.
    private void ApplyTankLimitBreak(SimCharacter user)
    {
        var actionId = LimitBreak.ByJob.TryGetValue((user as ISimPartyMember)?.ClassJob ?? 0, out var own) && TankLimitBreakActionIds.Contains(own) ? own : DarkForce;
        JobActions.ApplyEffects(user, actionId, (ulong)user.GameObjectId);
    }

    private static readonly uint[] TankLimitBreakActionIds = [199, 4240, 4241, 17105];
    private const uint DarkForce = 4241;

    private void HealerLimitBreak()
    {
        foreach (var member in party.ActiveMembers().ToList())
        {
            member.RemoveStatus(StatusId.MagicNumber);
            magicNumberExpiry.Remove(member);
        }
    }

    // Host only: a peer's gauge is the host's (IPartyLimitBreakScenario), and its own limit
    // break reaches the host as a report.
    private void OnLocalLimitBreak(uint actionId, LimitBreakAim aim)
    {
        if (Plugin.GameInstance is not { } game || game.ActiveScenario != this || game.RunningScenario != this) return;
        HumanLimitBreakLanded(party.PlayerRole, actionId, aim);
    }

    public void OnPartyLimitBreak(PartyRole role, uint actionId, LimitBreakAim aim)
    {
        if (Plugin.GameInstance is not { } game || game.RunningScenario != this) return;
        HumanLimitBreakLanded(role, actionId, aim);
    }

    // Two limit breaks cast together can't both spend one gauge: the second to land finds it short.
    // Anything short of an LB3 breaks the chain of Brilliant Dynamis refunds the later LB3s run on.
    private void HumanLimitBreakLanded(PartyRole role, uint actionId, LimitBreakAim aim)
    {
        if (party.Get(role) is not { } user || !user.IsAlive()) return;
        var level = LimitBreak.LevelOf(actionId);
        if (gauge.Units < level * LimitBreak.GaugeBar)
        {
            DiagnosticLog.Info($"[TopP6AlphaOmega] {role}'s {ActionLookup.Name(actionId)} (LB{level}) landed on a short gauge ({gauge.Units:F0}) and does nothing.");
            return;
        }
        LimitBreakLanded(role, KindOf(actionId), level, byBot: false, aim);
        if (level == 3) return;
        foreach (var member in party.ActiveMembers().Where(m => m.IsAlive()).ToList())
            member.Die(SimCharacterDeathExtensions.NoAction, "Previous LB was not an LB3, you did not wait for it to become an LB3");
    }

    private void ReleaseThirdBar()
    {
        gauge.ReleaseThirdBar();
        PushGauge();
    }

    private void SpendGauge(float units)
    {
        gauge.Spend(units, elapsed);
        PushGauge();
    }

    private void AddGauge(float units)
    {
        gauge.Add(units);
        PushGauge();
    }

    private void TickGauge()
    {
        if (gauge.Tick(elapsed)) PushGauge();
    }

    private void PushGauge() => world.Party.LimitBreak.Set(gauge.Units / LimitBreak.GaugeBar);

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s ? new TopP6AlphaOmegaAiReplayStateMessage(
            s.Arrows.Select(a => a.InFirst).ToArray(),
            s.Exaflares.Select(e => e.StartOctant).ToArray(),
            s.Exaflares.Select(e => e.Turn).ToArray(),
            s.WildChargeTargets.ToArray(), s.FlareTargets.ToArray(), s.StackTarget,
            s.LeftDiveTank, s.MeteorMiddleHealer, s.WaveCannonInvulns[0], s.Practice)
        : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not TopP6AlphaOmegaAiReplayStateMessage msg) return null;
        if (TopP6AlphaOmegaState.FromNetworkReplay(msg.ArrowsInFirst, msg.ExaflareStarts, msg.ExaflareTurns, msg.WildChargeTargets, msg.FlareTargets,
                msg.StackTarget, msg.LeftDiveTank, msg.MeteorMiddleHealer, msg.FirstWaveCannonInvuln, msg.Practice) is not { } shadowState)
            return null;
        ((IScenarioAi<TopP6AlphaOmegaState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }
}
