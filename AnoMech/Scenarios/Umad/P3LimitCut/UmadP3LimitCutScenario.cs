using AnoMech.Core.EnemyActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Scenarios.Umad.P3LimitCut;

using AnoMech.Core.Native.Interfaces;
using static UmadConstants;
using static UmadP3LimitCutState;

// Dancing Mad P3 "Limit Cut". Scenario time 0 is 8.0s before Chaos starts casting Umbra Smash,
// the earliest start inside this mechanic (the previous resolve is 9.2s before).
public sealed class UmadP3LimitCutScenario : IMultiplayerReplayable
{
    public string Name => "Limit Cut";
    public IPhase Phase => UmadZone.P3;
    public float BgmSecondsAtStart => 108.67f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP3LimitCutAi()];
    public void DrawSettings() => settingsWindow.Draw();
    public bool HasPerPlayerSettings => true;
    public void DrawPerPlayerSettings() => settingsWindow.DrawPerPlayer();
    public object SettingsOverrides => settingsWindow.Overrides;
    public IReadOnlyList<string> SettingsConflicts => settingsWindow.Overrides.Validate().Problems;
    public void DrawMultiplayerSettings() => settingsWindow.DrawThunderIIIPlan();

    private readonly UmadP3LimitCutSettingsWindow settingsWindow = new();
    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP3LimitCutState state = null!;
    private readonly SimEnemy?[] cycloneHelpers = new SimEnemy?[8];
    private SimEnemy? thunderHelper;
    private SimEventObject? windCrystal;
    private Vector3 umbraImpact;
    private EnemyActionCast? vacuumWave;
    private readonly SimCharacter?[] chargeTargets = new SimCharacter?[8];

    // Clone k is teleported to its spot, then fires its appearance ~0.1s later, ~2.0s apart.
    private static readonly float[] PlaceCloneAt = [8.803f, 10.808f, 12.815f, 14.821f, 16.830f, 18.836f, 20.840f, 22.846f];
    private static readonly float[] CloneAppearAt = [8.892f, 10.898f, 12.904f, 14.910f, 16.919f, 18.925f, 20.929f, 22.933f];
    // Charge k: teleport to the charge spot facing its number, then the rect ~0.1s later.
    private static readonly float[] PrepareChargeAt = [30.853f, 31.075f, 31.298f, 31.521f, 31.745f, 31.968f, 32.191f, 32.415f];
    private static readonly float[] ChargeAt = [30.953f, 31.175f, 31.398f, 31.621f, 31.845f, 32.068f, 32.291f, 32.515f];
    private const float NumbersAt = 18.83f;
    // Both bosses stop swinging for their own casts.
    private static readonly float[] ChaosAutoAt = [3.904f, 6.928f, 18.066f, 21.095f, 26.162f, 29.189f, 32.214f, 37.285f, 40.311f, 43.335f, 49.381f, 52.407f];
    private static readonly float[] ExdeathAutoAt = [4.259f, 7.284f, 20.379f, 26.473f, 29.501f, 32.525f, 41.603f, 47.647f, 50.671f];

    public UmadP3LimitCutState? LastState { get; private set; }

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP3LimitCutState(world.Rng, party, settingsWindow.Overrides);
        LastState = state;
        vacuumWave = null;
        Array.Clear(chargeTargets);
        DiagnosticLog.Info(
            $"[UmadP3LimitCut] Roll: clones start {SpotName(state.StartSpot)} going {(state.Clockwise ? "clockwise" : "counter-clockwise")}, "
            + $"bosses held {SpotName(state.BossSpot)}, bait {state.BaitRole}, numbers "
            + string.Join(" ", state.Numbers.Select((r, k) => $"{k + 1}={r}")) + ", winds "
            + string.Join(" ", state.Winds.Select(kv => $"{kv.Key}={kv.Value}")) + ".");

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP3LimitCutState>)AiStrats[idx]).Run(state, world);

        SpawnActors();
        ApplyStartingStatuses();

        world.Events.Add(1.0f, () =>
        {
            state.Objects.Chaos?.Follow(party.Get(PartyRole.MainTank));
            state.Objects.Exdeath?.Follow(party.Get(PartyRole.OffTank));
        });
        foreach (var at in ChaosAutoAt)
            world.Events.Add(at, () => state.Objects.Chaos?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank)));
        foreach (var at in ExdeathAutoAt)
            world.Events.Add(at, () => state.Objects.Exdeath?.Cast(UmadActions.ExdeathAutoAttack, party.Get(PartyRole.OffTank)));
        // The real packet refreshes Kefka's trance aura in place; AddStatusParam adds a slot, so
        // drop the first or Kefka wears two auras at once.
        world.Events.Add(6.75f, () =>
        {
            state.Objects.Kefka?.Cast(UmadActions.RingOfFire);
            state.Objects.Kefka?.RemoveStatus(StatusId.KefkaTrance);
            state.Objects.Kefka?.AddStatusParam(StatusId.KefkaTrance, 0x22B);
        });

        // Both bosses stand still for their casts, which lets the tanks step into the stack (at
        // every real wave both tanks were inside it, Exdeath frozen 5.5y further out).
        world.Events.Add(7.8f, () => state.Objects.Chaos?.Follow());
        world.Events.Add(7.928f, () => state.Objects.Exdeath?.Follow());
        world.Events.Add(8.0f, StartUmbraSmash);
        world.Events.Add(8.128f, () => vacuumWave = state.Objects.Exdeath?.Cast(UmadActions.VacuumWave));
        for (var k = 0; k < 8; k++)
        {
            var clone = k;
            world.Events.Add(PlaceCloneAt[k], () => PlaceClone(clone));
            world.Events.Add(CloneAppearAt[k], () => CloneAppear(clone));
        }
        world.Events.Add(14.55f, ChaosLands);
        world.Events.Add(17.56f, () => state.Objects.Exdeath?.Follow(party.Get(PartyRole.OffTank)));
        world.Events.Add(NumbersAt, AttachNumbers);
        world.Events.Add(19.967f, ResolveCyclones);
        world.Events.Add(21.379f, () =>
        {
            state.Objects.Chaos?.Cast(ActionId.Aetherlink_Chaos, animationLock: 3.1f);
            state.Objects.Exdeath?.Cast(ActionId.Aetherlink_Exdeath, animationLock: 3.1f);
        });
        for (var k = 0; k < 8; k++)
        {
            var clone = k;
            world.Events.Add(PrepareChargeAt[k], () => PrepareCharge(clone));
            world.Events.Add(ChargeAt[k], () => ResolveCharge(clone));
        }
        world.Events.Add(33.29f, () => state.Objects.Exdeath?.Follow());
        world.Events.Add(33.49f, () => state.Objects.Exdeath?.Cast(ActionId.ThunderIII_Cast, animationLock: 3.1f));
        world.Events.Add(38.57f, ResolveThunder);
        world.Events.Add(41.60f, ResolveThunder);
        world.Events.Add(42.10f, () => state.Objects.Exdeath?.Follow(party.Get(PartyRole.OffTank)));
        world.Events.Add(43.30f, () =>
        {
            state.Objects.Chaos?.Follow();
            state.Objects.Exdeath?.Follow();
        });
        world.Events.Add(43.50f, () =>
        {
            state.Objects.Chaos?.Cast(ActionId.DecisiveBattle_Chaos, animationLock: 3.1f);
            state.Objects.Exdeath?.Cast(ActionId.DecisiveBattle_Exdeath, animationLock: 3.1f);
        });
        world.Events.Add(46.49f, ResolveDecisiveBattle);
        world.Events.Add(46.69f, () =>
        {
            state.Objects.Chaos?.Follow(party.Get(PartyRole.MainTank));
            state.Objects.Exdeath?.Follow(party.Get(PartyRole.OffTank));
        });
        world.Events.Add(50.581f, () => state.Objects.Kefka?.PlayAnimationTimeline(TimelineId.Spawn));
    }

    // Scheduled by host and peer alike, so broadcast: false; a peer's own clones play the same
    // materialise timelines, so the preload belongs here too, and so does a tank's LB3 gauge.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        Natives.TimelinePreload.Preload(CloneTimelines, "UmadP3LimitCut");
        instanceWorld.Party.LimitBreak.Set(3f);
        instanceWorld.Events.Add(1.988f, () => instanceWorld.Map.DirectorUpdate(0x80000027U, 0x18U, 0x2U, 0x1BDBU, 0x4000EFB9U, broadcast: false));
        instanceWorld.Events.Add(8.981f, () => instanceWorld.Map.DirectorUpdate(0x80000027U, 0x2FU, 0x2U, 0x1BDBU, 0x4000EFB9U, broadcast: false));
        instanceWorld.Events.Add(31.059f, () => instanceWorld.Map.DirectorUpdate(0x80000027U, 0x19U, 0x2U, 0x1BDBU, 0x4000EFB9U, broadcast: false));
        instanceWorld.Events.Add(52.629f, () => instanceWorld.Map.DirectorUpdate(0x80000027U, 0x1AU, 0x2U, 0x1BDBU, 0x4000EFB9U, broadcast: false));
    }

    public MpMessage? BuildReplayStateMessage()
        => LastState is { } s
            ? new UmadP3LimitCutAiReplayStateMessage(s.StartSpot, s.Clockwise, s.Numbers.ToArray(),
                s.Winds.Where(kv => kv.Value == Wind.Headwind).Select(kv => kv.Key).ToArray(), s.BossSpot, s.BaitRole, s.ThunderPlan)
            : null;

    public object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld replayWorld)
    {
        if (message is not UmadP3LimitCutAiReplayStateMessage msg || aiIndex < 0 || aiIndex >= AiStrats.Count) return null;
        if (UmadP3LimitCutState.FromNetworkReplay(msg.StartSpot, msg.Clockwise, msg.Numbers, msg.Headwinds, msg.BossSpot, msg.BaitRole, msg.ThunderPlan) is not { } shadowState) return null;
        ((IScenarioAi<UmadP3LimitCutState>)AiStrats[aiIndex]).Run(shadowState, replayWorld);
        return shadowState;
    }

    // Chaos/Exdeath may not be replicated yet when StartReplay runs.
    public void RefreshLiveHandles(object shadowStateObj, IReadOnlyDictionary<int, SimEnemy> peerEnemies)
    {
        if (shadowStateObj is not UmadP3LimitCutState shadow) return;
        shadow.Objects.Chaos ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.ChaosP3);
        shadow.Objects.Exdeath ??= peerEnemies.Values.FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.Exdeath);
    }

    // The "hide" set's mon_sp003/mon_sp004 materialise the clone out of its dissolve; a timeline
    // that never comes up leaves it frozen white and translucent. Both key spellings, as Flood needed.
    private static readonly (ushort Id, string Key)[] CloneTimelines =
    [
        (4575, "mon_sp/m0462/hide/mon_sp003"),
        (4576, "mon_sp/m0462/hide/mon_sp004"),
    ];

    // The cyclone helpers sit on the wind crystal.
    private static readonly Vector3 WindCrystalPos = new(9.9f, 0f, -9.9f);

    private void SpawnActors()
    {
        var objects = state.Objects;
        var hold = SpotHeading(state.BossSpot);
        objects.Kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP3, NameId: BNpcNameId.Kefka, Level: 100,
            Targetable: false, EnemyList: EnemyListMode.Always, IsVisible: true,
            Placement: new Placement(new Vector3(0f, 0f, -10f), 0f)));
        objects.Chaos = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.ChaosP3, NameId: BNpcNameId.Chaos, Level: 100,
            Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true,
            Placement: new Placement(OnCircle(hold, 9f), hold + MathF.PI)));
        objects.Exdeath = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Exdeath, NameId: BNpcNameId.Exdeath, Level: 100,
            Targetable: true, EnemyList: EnemyListMode.Always, IsVisible: true,
            Placement: new Placement(
                OnCircle(hold, 10.6f) + OnCircle(hold + MathF.PI / 2f, 1.6f),
                hold + MathF.PI)));
        // Built from a real clone's NpcSpawn packet: visible at the centre, hidden only by
        // animation state (0,1), as the real ones sit for 86s. Spawning them invisible and
        // flipping RenderFlags at placement showed a washed-out white Kefka.
        for (var k = 0; k < 8; k++)
            objects.Clones[k] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaCloneP3, NameId: BNpcNameId.Kefka, Level: 100,
                Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: true,
                Placement: new Placement(Vector3.Zero, 0f),
                NpcSpawnTemplate: UmadRealPackets.CloneP3NpcSpawn, PacketSpawnEnableDraw: true));
        // Cyclone's caster-side VFX needs a real skeleton; the real 9020 helpers have none.
        for (var i = 0; i < 8; i++)
            cycloneHelpers[i] = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Chaos, NameId: BNpcNameId.Chaos, Level: 1,
                Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
                Placement: new Placement(WindCrystalPos, 0f)));
        // The strike's VFX sits on the target, so the helper's model never matters.
        thunderHelper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaHelper, NameId: BNpcNameId.Exdeath, Level: 1,
            Targetable: false, EnemyList: EnemyListMode.Never, IsVisible: false,
            Placement: new Placement(WindCrystalPos, 0f)));
        // The fire and water crystals faded when their elements resolved, before this window.
        windCrystal = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = EObjId.WindCrystal, Placement = new Placement(WindCrystalPos, -0.785f) });

        // Set once the draw objects exist.
        world.Events.Add(0.5f, () =>
        {
            objects.Kefka?.SetAnimationState(0, 1);
            foreach (var clone in objects.Clones) clone?.SetAnimationState(0, 1);
        });
    }

    // No Epic/Fated sides: stripped 44.3s before the Umbra cast, back with the Decisive Battle.
    // The winds land 68s long at Bowels of Agony, 48.4s before scenario start.
    private void ApplyStartingStatuses()
    {
        world.Events.Add(0.1f, () =>
        {
            state.Objects.Kefka?.AddStatusParam(StatusId.KefkaTrance, 0x1FF);
            foreach (var role in Enum.GetValues<PartyRole>())
            {
                if (party.Get(role) is not { } member) continue;
                member.AddStatus(state.Winds[role] == Wind.Headwind ? StatusId.Headwind : StatusId.Tailwind, 19.6f);
            }
        });
    }

    // Black Hole's buster, on whoever is closest to Exdeath.
    private void ResolveThunder()
    {
        if (state.Objects.Exdeath is not { } exdeath) return;
        thunderHelper?.Cast(UmadActions.ThunderIII, party.Find.Closest(exdeath.Position));
    }

    private void ResolveDecisiveBattle()
    {
        state.Objects.Chaos?.AddStatus(StatusId.EpicVillain);
        state.Objects.Exdeath?.AddStatus(StatusId.FatedVillain);
        foreach (var role in Enum.GetValues<PartyRole>())
            if (party.Get(role) is { } member && member.IsAlive())
                member.AddStatus(role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB
                    ? StatusId.EpicHero : StatusId.FatedHero);
    }

    private void StartUmbraSmash()
    {
        if (state.Objects.Chaos is not { } chaos) return;
        var bait = party.Find.Farest(chaos.Position);
        umbraImpact = bait?.Position ?? chaos.Position;
        DiagnosticLog.Info($"[UmadP3LimitCut] Umbra Smash: bait {(bait as ISimPartyMember)?.Role.ToString() ?? "none"} at ({umbraImpact.X:F1},{umbraImpact.Z:F1}), {Vector3.Distance(umbraImpact, chaos.Position):F1}y from Chaos.");
        chaos.Cast(UmadActions.UmbraSmash, umbraImpact);
    }

    private void ChaosLands()
    {
        if (state.Objects.Chaos is not { } chaos) return;
        chaos.SetPosition(new Placement(umbraImpact, chaos.Rotation));
        chaos.Follow(party.Get(PartyRole.MainTank));
    }

    private void PlaceClone(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        var spot = state.PlacementSpot(k);
        clone.SetPosition(new Placement(SpotPosition(spot), SpotHeading(spot) + MathF.PI));
        clone.SetVisible(true);
        if (k == 0) state.Objects.Kefka?.SetAnimationState(0, 0);
    }

    private void CloneAppear(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        clone.Cast(UmadActions.UltimaBlaster);
    }

    private void AttachNumbers()
    {
        for (var k = 0; k < 8; k++)
        {
            if (party.Get(state.Numbers[k]) is not { } member || !member.IsAlive()) continue;
            member.AttachLockonVfx(LockonId.LimitCutNumbers[k], duration: ChargeAt[k] - NumbersAt, persistent: false);
        }
    }

    // One Cyclone per player the Vacuum Wave reached: everyone carries a wind into it.
    private void ResolveCyclones()
    {
        windCrystal?.FadeOut();
        if (vacuumWave is null) return;
        var centres = vacuumWave.Hits.Select(h => h.Who).Where(t => t.IsAlive()).ToList();
        for (var i = 0; i < centres.Count; i++)
            cycloneHelpers[i % cycloneHelpers.Length]?.Cast(UmadActions.Cyclone, centres[i]);
    }

    // A dead number's clone still charges someone; none of the 8 real retargets fit a rule
    // (nearest, farthest, next slot, next number, enmity, or excluding a vuln carrier), so
    // uniformly random.
    private SimCharacter? ChargeTarget(int k)
    {
        if (party.Get(state.Numbers[k]) is { } numbered && numbered.IsAlive()) return numbered;
        var alive = party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
        return alive.Length == 0 ? null : state.PickRandom(alive);
    }

    private void PrepareCharge(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        var target = ChargeTarget(k);
        chargeTargets[k] = target;
        var spot = SpotPosition(state.ChargeSpot(k));
        var facing = target != null ? MathF.Atan2(target.Position.X - spot.X, target.Position.Z - spot.Z) : SpotHeading(state.ChargeSpot(k)) + MathF.PI;
        clone.SetPosition(new Placement(spot, facing));
    }

    // Aimed at the target wherever they stand, so a misplaced number drags the rect across
    // whoever is between.
    private void ResolveCharge(int k)
    {
        if (state.Objects.Clones[k] is not { } clone) return;
        var target = chargeTargets[k] is { } t && t.IsAlive() ? t : ChargeTarget(k);
        if (target != null) clone.Cast(UmadActions.UltimaBlasterCharge, target);
    }
}
