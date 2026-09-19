using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

public sealed class FruParadiseRegainedScenario : IScenario
{
    public string Name => "Paradise Regained";
    public float Duration => PolarizingStrikesPlan.CleanupTime;
    public IPhase Phase => FruZone.P5;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new ParadiseRegainedAi()];

    private SimWorld world = null!;
    private DamageSolver damage = null!;
    private ParadiseRegainedPattern pattern = null!;
    private SimEnemy? pandora;
    private SimEnemy? busterHelper;
    private readonly SimEnemy?[] towerHelpers = new SimEnemy?[3];
    private readonly SimMapEffect?[] towers = new SimMapEffect?[3];

    public void Run(SimWorld worldParam, int? selectedAi)
        => Run(worldParam, new ParadiseRegainedPattern(Random.Shared.Next(3), Random.Shared.Next(2) == 0, Random.Shared.Next(2) == 0));

    internal void Run(SimWorld worldParam, ParadiseRegainedPattern chosenPattern)
    {
        world = worldParam;
        damage = new DamageSolver(world.Party);
        pattern = chosenPattern;
        pandora = null;
        busterHelper = null;
        Array.Clear(towers);
        Array.Clear(towerHelpers);
        ((ParadiseRegainedAi)AiStrats[0]).Run(pattern, world);

        world.Events.Add(0.5f, SpawnActors);
        world.Events.Add(1.3f, () => pandora?.Cast(ActionId.ParadiseRegained, castSeconds: 3.7f));
        // ActionCastVFX 586/587 selects m0914_cst_b2lp_c0v/c1v. The native
        // cast owns both wing glows and their order; no extra drawn telegraphs.
        world.Events.Add(8.3f, () => pandora?.Cast(pattern.DarkFirst
            ? ActionId.WingsDarkThenLight : ActionId.WingsLightThenDark, castSeconds: 6.8f));
        world.Events.Add(15f, () => FaceTank(PartyRole.MainTank));
        world.Events.Add(18.5f, () => FaceTank(PartyRole.OffTank));
        for (var tower = 0; tower < 3; tower++)
        {
            var index = tower;
            world.Events.Add(ParadiseRegainedPattern.TowerSpawnTime(index), () =>
                towers[index] = world.SpawnMapEffect(pattern.TowerSlot(index), MapEffect.ParadiseTowerShow, MapEffect.Hide));
            world.Events.Add(ParadiseRegainedPattern.TowerHitTime(index), () => Resolve(index));
        }
        // Continue with the same Pandora and party; keep one scenario entry.
        world.Events.Add(PolarizingStrikesPlan.Start, FinishTowers);
        new PolarizingStrikesSequence(world, () => pandora).Schedule();
    }

    private void SpawnActors()
    {
        pandora = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.Pandora,
            NameId: BNpcNameId.Pandora, Level: Level, Targetable: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
        pandora?.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        busterHelper = SpawnHelper(Vector3.Zero);
        for (var tower = 0; tower < 3; tower++) towerHelpers[tower] = SpawnHelper(pattern.TowerPosition(tower));
    }

    private SimEnemy? SpawnHelper(Vector3 position)
        => world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId.Helper, NameId: BNpcNameId.Pandora,
            Level: Level, Targetable: false, EnemyList: EnemyListMode.Never,
            Placement: new Placement(position, 0f)));

    private void FaceTank(PartyRole role)
    {
        var tank = world.Party.Get(role);
        if (pandora is not { IsActive: true } boss || !tank.IsAlive()) return;
        boss.SetTarget(tank, follow: false);
        var offset = tank!.Position - boss.Position;
        if (offset.LengthSquared() > 0.001f) boss.SetRotation(MathF.Atan2(offset.X, offset.Z));
    }

    private ParadiseMember[] Members()
        => Enum.GetValues<PartyRole>().Select(role => (Role: role, Member: world.Party.Get(role)))
            .Where(pair => pair.Member.IsAlive())
            .Select(pair => new ParadiseMember(pair.Role, pair.Member!.Position)).ToArray();

    private void Resolve(int index)
    {
        towers[index]?.Despawn();
        if (pandora is not { IsActive: true } boss) return;
        var members = Members();
        if (members.Length == 0) return;
        var occupants = ParadiseRegainedSnapshot.InCircle(members, pattern.TowerPosition(index), ParadiseRegainedPattern.TowerRadius);
        var wing = index < 2 ? ParadiseRegainedSnapshot.Capture(members, boss.Position, boss.Rotation, pattern.IsDark(index)) : null;

        // Dispatch the real tower explosion even on failure, using its actual
        // helper location. Hit geometry is only for damage, never rendered.
        var towerAction = occupants.Length == 2 ? ActionId.ParadiseTowerExplosion : ActionId.ParadiseTowerFailure;
        if (towerHelpers[index] is { IsActive: true } helper) helper.Cast(towerAction, castSeconds: 0f);
        if (wing != null)
        {
            var dark = pattern.IsDark(index);
            var cleaveAction = dark ? ActionId.WingsCleaveDark : ActionId.WingsCleaveLight;
            var busterAction = dark ? ActionId.WingsBusterDark : ActionId.WingsBusterLight;
            // The cleave resource already includes its +/-60-degree rotation.
            // Keep Pandora facing the tank; only the damage query adds it.
            boss.Cast(cleaveAction, castSeconds: 0f);
            if (wing.BusterTarget is { } bait && busterHelper is { IsActive: true } source)
            {
                source.SetPosition(wing.BusterPosition);
                source.Cast(busterAction, targetId: world.Party.Get(bait)?.GameObjectId, castSeconds: 0f);
            }
            foreach (var role in wing.CleaveTargets)
                Hit(role, cleaveAction, "wing cleave / tank swap", role != ParadiseRegainedPattern.CleaveTank(index));
            foreach (var role in wing.BusterTargets)
                Hit(role, busterAction, dark ? "nearest tank bait / splash" : "farthest tank bait / splash",
                    role != ParadiseRegainedPattern.BusterTank(index));
        }
        if (occupants.Length != 2)
        {
            foreach (var member in members)
                Hit(member.Role, towerAction, $"{occupants.Length}/2 players in tower", true);
        }
        else
        {
            foreach (var role in occupants) Hit(role, towerAction, "two-person tower", false);
        }
    }

    private void Hit(PartyRole role, uint action, string cause, bool lethal)
    {
        var member = world.Party.Get(role);
        if (member.IsAlive())
            damage.ApplyDamage(member!, lethal ? 1f : 0.5f, action, cause, lethal);
    }

    private void FinishTowers()
    {
        foreach (var tower in towers) tower?.Despawn();
        foreach (var helper in towerHelpers) helper?.Despawn();
        busterHelper?.Despawn();
        if (pandora is { IsActive: true } boss) boss.SetRotation(MathF.PI);
    }
}
