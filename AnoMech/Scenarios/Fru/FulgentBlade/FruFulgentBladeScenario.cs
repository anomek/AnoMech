using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.FulgentBlade;

public sealed class FruFulgentBladeScenario : IScenario
{
    public string Name => "Fulgent Blade";
    public IPhase Phase => FruZone.P5;
    public bool SupportsSolo => true;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new FulgentBladeAi()];

    private const float ArrowLeadTime = 2.3f;

    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private SimCharacter? MainTank => party.Get(PartyRole.MainTank);
    private bool solo;
    private SimEnemy? pandora;
    private FulgentBladePattern pattern = null!;
    private readonly SimEnemy?[,] waveHelpers = new SimEnemy?[3, 4];
    private readonly SimOmen?[,] initialSeams = new SimOmen?[3, 2];
    private readonly SimEnemy?[] stackHelpers = new SimEnemy?[2];

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        damage = new DamageSolver(party);
        solo = selectedAi is null;
        pandora = null;
        pattern = new FulgentBladePattern(Random.Shared.Next(4), Random.Shared.Next(4), Random.Shared.Next(2) == 0);
        Array.Clear(waveHelpers);
        Array.Clear(initialSeams);
        Array.Clear(stackHelpers);

        if (selectedAi is { } aiIndex && aiIndex >= 0 && aiIndex < AiStrats.Count)
            ((IScenarioAi<FulgentBladeAiState>)AiStrats[aiIndex]).Run(new(pattern, () => pandora), world);
        else if (solo)
            FulgentBladeAi.RunSolo(new(pattern, () => pandora), world);

        // FRU-Sim's FB sequence starts after the opening raidwide (here at 7.5s).
        world.Events.Add(0.5f, SpawnPandora);
        world.Events.Add(1.5f, () => pandora?.Cast(ActionId.FulgentBlade, castSeconds: 6f));
        world.Events.Add(7.5f, ResolveRaidwide);
        world.Events.Add(11.5f, SpawnExalines);
        // Advance each randomized seam group into its native charging stage.
        world.Events.Add(13.5f, () => BeginExalineGroup(0));
        world.Events.Add(17.5f, () => BeginExalineGroup(1));
        // Start the native arrows 0.5s earlier than the reference's box warning
        // to allow for their fade-in; keep them visible until the first snapshot.
        world.Events.Add(20.5f - ArrowLeadTime, () => TelegraphGroup(0));
        world.Events.Add(21.5f, () => BeginExalineGroup(2));
        world.Events.Add(24.5f - ArrowLeadTime, () => TelegraphGroup(1));
        world.Events.Add(28.5f - ArrowLeadTime, () => TelegraphGroup(2));
        world.Events.Add(FulgentBladePartyPlan.AkhMornCastTime, CastAkhMorn);
        world.Events.Add(36f, ResolveAkhMorn);
        world.Events.Add(43.5f, DespawnHelpers);

        // Schedule every snapshot up front so low frame rates cannot accumulate
        // drift between the three overlapping, two-second wave trains.
        for (var group = 0; group < FulgentBladePattern.GroupCount; group++)
        {
            for (var hit = 0; hit < FulgentBladePattern.HitCount; hit++)
            {
                var g = group;
                var h = hit;
                world.Events.Add(20.5f + group * 4f + hit * 2f, () => ResolveWave(g, h));
            }
        }
    }

    private void SpawnExalines()
    {
        for (var group = 0; group < FulgentBladePattern.GroupCount; group++)
            for (var wave = 0; wave < FulgentBladePattern.WavesPerGroup; wave++)
            {
                var definition = pattern.Wave(group, wave);
                var placement = new Placement(definition.Position, definition.Rotation);
                waveHelpers[group, wave] = SpawnHelper(placement);
                if (wave % 2 == 0)
                {
                    // Line and arrows share the scenery's axes: purple +X,
                    // gold -X. Keep the native palette on its matching wave side.
                    var line = pattern.ArrowWarning(group, wave / 2);
                    initialSeams[group, wave / 2] = world.SpawnOmen(Vfx.InitialSeam,
                        new Placement(line.Position, line.Rotation), Vector3.One,
                        startTrigger: Vfx.InitialSeamTrigger);
                }
            }
    }

    private SimEnemy? SpawnHelper(Placement placement) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: BNpcBaseId.Helper,
        NameId: BNpcNameId.Pandora,
        Level: Level,
        Targetable: false,
        EnemyList: EnemyListMode.Never,
        IsVisible: true,
        Placement: placement));

    private void BeginExalineGroup(int group)
    {
        // The intermediate indicator belongs to the line scenery AVFX, not
        // the helpers' m0531_sp03 cast effects. Those produced a different
        // palette layered over the seam. Let its own timeline animate the
        // native colors/brightness and keep the same dark/light orientation.
        for (var line = 0; line < FulgentBladePattern.WavesPerGroup / 2; line++)
            initialSeams[group, line]?.Trigger(Vfx.SeamChargeTrigger);
    }

    private void TelegraphGroup(int group)
    {
        // The arrows belong to the line's scenery object, not action 40115.
        // Spawn its native world-space resource separately from the cast omens.
        for (var line = 0; line < FulgentBladePattern.WavesPerGroup / 2; line++)
        {
            var warning = pattern.ArrowWarning(group, line);
            // The AVFX already contains the full line of arrows at game size.
            // Do not apply the width/depth scaling used by action-backed omens.
            // SimWorld owns this looping StaticVfx and cleans it on expiry/reset.
            world.SpawnOmen(Vfx.PathArrows, new Placement(warning.Position, warning.Rotation),
                Vector3.One, durationSeconds: ArrowLeadTime);
        }
    }

    private void ResolveWave(int group, int hit)
    {
        // Snapshot all four strips before applying any deaths.
        var hits = new List<(SimCharacter Member, uint Action)>();
        for (var wave = 0; wave < FulgentBladePattern.WavesPerGroup; wave++)
        {
            var definition = pattern.Wave(group, wave, hit);
            var action = definition.IsLight ? ActionId.PathOfLightRest : ActionId.PathOfDarknessRest;
            var placement = new Placement(definition.Position, definition.Rotation);
            var helper = waveHelpers[group, wave];
            if (helper is { IsActive: true })
            {
                helper.SetPosition(definition.Position);
                helper.Cast(action, castSeconds: 0f, animationLock: 0f);
            }
            foreach (var member in party.Find.InsideRect(placement, Geometry.ExalineHalfWidth, Geometry.ExalineStep))
                hits.Add((member, action));
        }
        if (hit == 0)
        {
            for (var line = 0; line < FulgentBladePattern.WavesPerGroup / 2; line++)
            {
                initialSeams[group, line]?.Despawn();
                initialSeams[group, line] = null;
            }
        }
        foreach (var hitTarget in hits.DistinctBy(h => h.Member))
            damage.ApplyDamage(hitTarget.Member, 1f, hitTarget.Action, "Fulgent Blade exaline", lethal: true);
    }

    private void SpawnPandora()
    {
        pandora = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Pandora,
            NameId: BNpcNameId.Pandora,
            Level: Level,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            IsVisible: true,
            Placement: new Placement(Vector3.Zero, 0f)));
        // Follow is retained through casts, but Movement respects the boss's
        // cast/animation lock and only resumes chasing after it ends.
        pandora?.SetTarget(MainTank, follow: true);
    }

    private void ResolveRaidwide()
    {
        foreach (var member in party.ActiveMembers())
            damage.ApplyDamage(member, 0.8f, ActionId.FulgentBlade, "raidwide", lethal: false);
    }

    private void CastAkhMorn()
    {
        if (pandora is not { IsActive: true } boss) return;
        var mainTank = MainTank;
        if (mainTank.IsAlive())
        {
            var offset = mainTank!.Position - boss.Position;
            if (offset.LengthSquared() > 0.001f)
                boss.SetRotation(MathF.Atan2(offset.X, offset.Z));
        }
        else if (solo && mainTank == null)
        {
            // The solo support MT arrives after the dodges. Establish the
            // pocket's cardinal now so its two stacks straddle Pandora.
            boss.SetRotation(MathF.Atan2(pattern.Origin.X, pattern.Origin.Z));
        }
        boss.Cast(ActionId.AkhMornPandora, castSeconds: 7.5f);
        for (var i = 0; i < stackHelpers.Length; i++)
            stackHelpers[i] = SpawnHelper(new Placement(boss.Position, 0f));
    }

    private void ResolveAkhMorn()
    {
        if (pandora is not { IsActive: true } boss) return;
        var members = party.ActiveMembers().ToArray();
        if (members.Length == 0) return;
        var right = new Vector3(MathF.Cos(boss.Rotation), 0f, -MathF.Sin(boss.Rotation));
        var leftSide = members.Where(m => Vector3.Dot(m.Position - boss.Position, right) <= 0f).ToArray();
        var rightSide = members.Where(m => Vector3.Dot(m.Position - boss.Position, right) > 0f).ToArray();
        var targets = new List<SimCharacter>();
        if (leftSide.Length > 0) targets.Add(leftSide[Random.Shared.Next(leftSide.Length)]);
        if (rightSide.Length > 0) targets.Add(rightSide[Random.Shared.Next(rightSide.Length)]);

        // Solo practice fills the missing roles before this resolve, so both
        // modes enforce real four-person stacks. Allocation failures must not
        // silently turn this back into a free solo hit.
        if (targets.Count != 2)
        {
            party.WipeAllPlayers("Akh Morn: no target on one side of Pandora");
            return;
        }
        var stacks = targets.Select(t => (Target: t, Members: party.Find.InsideCircle(t.Position, Geometry.AkhMornRadius))).ToArray();
        for (var i = 0; i < stacks.Length; i++)
        {
            var stack = stacks[i];
            var action = i == 0 ? ActionId.AkhMornPandoraAoe1 : ActionId.AkhMornPandoraAoe2;
            var helper = stackHelpers[i];
            if (helper is { IsActive: true })
            {
                helper.SetPosition(stack.Target.Position);
                helper.Cast(action, targetId: stack.Target.GameObjectId, castSeconds: 0f);
            }
            foreach (var member in stack.Members)
            {
                var overlapping = stacks.Count(s => s.Members.Contains(member)) > 1;
                var failed = stack.Members.Count != 4 || overlapping;
                damage.ApplyDamage(member, failed ? 1f : 0.5f, action,
                    overlapping ? "overlapping Akh Morn stacks" : $"{stack.Members.Count}/4 players in stack", failed);
            }
        }
    }

    private void DespawnHelpers()
    {
        foreach (var helper in waveHelpers) helper?.Despawn();
        foreach (var helper in stackHelpers) helper?.Despawn();
        foreach (var seam in initialSeams) seam?.Despawn();
        Array.Clear(waveHelpers);
        Array.Clear(stackHelpers);
        Array.Clear(initialSeams);
    }
}
