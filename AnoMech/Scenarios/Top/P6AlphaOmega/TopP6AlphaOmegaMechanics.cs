using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Scenarios.Top.P6AlphaOmega.TopP6AlphaOmegaConstants;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// Alpha Omega and its gimmick helpers, and how their hits land.
public sealed class TopP6AlphaOmegaMechanics(SimWorld world)
{
    private const string Tag = "TopP6AlphaOmega";
    private readonly SimParty party = world.Party;

    public DamageSolver Damage { get; } = new(world.Party);

    public SimEnemy? Boss { get; private set; }
    public TopHelpers? Helpers { get; private set; }

    private bool tracksMainTank;
    private bool holdsHeadingIntoNextCast;
    private bool facesNorth;

    public void SpawnBoss(bool targetable)
    {
        Boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.AlphaOmega,
            NameId: BNpcNameId.AlphaOmega,
            Level: Level,
            Targetable: targetable,
            EnemyList: EnemyListMode.Always,
            Placement: new Placement(Vector3.Zero, 0f)));
    }

    public void SpawnHelpers() => Helpers = new TopHelpers(world, BNpcNameId.AlphaOmega, 32, Tag);

    public void TargetMainTank()
    {
        tracksMainTank = true;
        if (party.Get(PartyRole.MainTank) is { } tank) Boss?.SetTarget(tank, follow: false);
    }

    // Alpha Omega turns with its main tank between its own casts.
    public void FaceTarget()
    {
        if (!tracksMainTank || holdsHeadingIntoNextCast || facesNorth) return;
        if (Boss is not { IsCasting: false, Health: > 0 } b || b.AnimationLock) return;
        if (party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive()) b.Face(tank.Position);
    }

    public void FaceNorthFromNowOn()
    {
        facesNorth = true;
        Boss?.SetRotation(MathF.PI);
    }

    // Alpha Omega's own casts, and its helpers', play on the caster itself.
    public void BossCast(uint actionId, float castSeconds, float fireDelay, float animationLock)
    {
        holdsHeadingIntoNextCast = false;
        if (Boss is { } boss) boss.Cast(actionId, castSeconds: castSeconds, targetId: boss.GameObjectId, animationLock: animationLock, fireDelay: fireDelay);
    }

    public SimEnemy? HelperCast(Placement at, uint actionId, float castSeconds, float fireDelay, float animationLock)
    {
        if (Helpers?.Next(at) is not { } helper) return null;
        helper.Cast(actionId, castSeconds: castSeconds, targetId: helper.GameObjectId, animationLock: animationLock, fireDelay: fireDelay);
        return helper;
    }

    // An instant helper effect: the helper plays it on itself, reaching `target` if it has one.
    public void HelperEffect(Placement at, uint actionId, float animationLock, SimCharacter? target = null)
    {
        if (Helpers?.Next(at) is not { } helper) return;
        helper.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0, rotation: at.Rotation, position: at.Position,
            animationTargetId: helper.GameObjectId, actionTargetId: target?.GameObjectId);
    }

    public void ApplyDynamis()
    {
        Boss?.AddStatus(StatusId.CodeMi);
        foreach (var member in party.ActiveMembers().ToList())
        {
            member.RemoveStatus(StatusId.QuickeningDynamis);
            member.AddStatus(StatusId.BrilliantDynamis);
        }
    }

    public void Kill(IEnumerable<SimCharacter> targets, uint actionId, string context, float resultDelay = HitResultDelay, float resultStep = 0f)
    {
        var k = 0;
        foreach (var member in targets.Where(m => m.IsAlive()).ToList())
            world.Events.Add(resultDelay + resultStep * k++, () => member.Die(actionId, context));
    }

    public void BossAuto() => BossCast(ActionId.AlphaOmegaAutoAttack, castSeconds: 0f, fireDelay: 0f, animationLock: 1.1f);

    public void FlashGale()
    {
        if (Boss is not { } boss) return;
        var targets = new List<SimCharacter>();
        if (party.Get(PartyRole.MainTank) is { } tank && tank.IsAlive()) targets.Add(tank);
        if (party.Find.Farest(boss.Position) is { } far) targets.Add(far);
        foreach (var target in targets)
        {
            Helpers?.Next(boss.Placement())?.Cast(ActionId.FlashGale, castSeconds: 0f, targetId: target.GameObjectId, animationLock: 1.1f);
            Damage.Resolve(target, ActionId.FlashGale, [DamageType.TankBuster, DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)]);
        }
    }

    // The outer pinwheel: each line starts 15y off centre along an edge and sweeps the arena.
    private static readonly (Vector3 Origin, float Rotation, Vector3 Step)[] OuterLines =
    [
        (new(-20f, 0f, 15f), MathF.PI / 2, new(0f, 0f, -1f)),
        (new(15f, 0f, 20f), MathF.PI, new(-1f, 0f, 0f)),
        (new(20f, 0f, -15f), -MathF.PI / 2, new(0f, 0f, 1f)),
        (new(-15f, 0f, -20f), 0f, new(1f, 0f, 0f)),
    ];

    // The centre cross: each line splits and runs out to both edges.
    private static readonly (Vector3 Origin, float Rotation, Vector3 Step)[] CrossLines =
    [
        (new(-20f, 0f, 0f), MathF.PI / 2, new(0f, 0f, 1f)),
        (new(0f, 0f, -20f), 0f, new(1f, 0f, 0f)),
    ];

    // The rest explosions land every 2s after a line's first; the first moves it 7.5y, the rest 5y.
    private static readonly float[] ArrowSteps = [10.06f, 12.07f, 14.07f, 16.08f, 18.08f, 20.09f, 22.10f];

    public void CosmoArrow(ArrowPattern pattern)
    {
        BossCast(ActionId.CosmoArrow, castSeconds: 5.7f, fireDelay: 0.27f, animationLock: 8.1f);
        holdsHeadingIntoNextCast = true;
        var (leading, trailing) = pattern.InFirst ? (CrossLines, OuterLines) : (OuterLines, CrossLines);
        StartArrowLines(leading);
        world.Events.Add(2.01f, () => StartArrowLines(trailing));
        ScheduleArrowRest(leading, pattern.InFirst, firstStep: 0);
        ScheduleArrowRest(trailing, !pattern.InFirst, firstStep: 1);
    }

    private void StartArrowLines((Vector3 Origin, float Rotation, Vector3 Step)[] lines)
    {
        foreach (var line in lines)
        {
            var placement = new Placement(line.Origin, line.Rotation);
            HelperCast(placement, ActionId.CosmoArrowOmen, castSeconds: 7.7f, fireDelay: 0.27f, animationLock: 2.1f);
            HelperCast(placement, ActionId.Inhale, castSeconds: 5.2f, fireDelay: 0.27f, animationLock: 2.1f);
            world.Events.Add(7.97f, () => Damage.Resolve(TopPositioned.From(placement), ActionId.CosmoArrowOmen, [DamageType.Lethal], []));
        }
    }

    private void ScheduleArrowRest((Vector3 Origin, float Rotation, Vector3 Step)[] lines, bool cross, int firstStep)
    {
        var steps = cross ? 3 : 6;
        for (var k = 1; k <= steps; k++)
        {
            var offset = 7.5f + 5f * (k - 1);
            var at = ArrowSteps[firstStep + k - 1];
            foreach (var line in lines)
            {
                if (cross)
                {
                    ScheduleArrowRestAt(at, new Placement(line.Origin + line.Step * offset, line.Rotation));
                    ScheduleArrowRestAt(at, new Placement(line.Origin - line.Step * offset, line.Rotation));
                }
                else
                    ScheduleArrowRestAt(at, new Placement(line.Origin + line.Step * offset, line.Rotation));
            }
        }
    }

    private void ScheduleArrowRestAt(float at, Placement placement)
    {
        world.Events.Add(at, () =>
        {
            HelperEffect(placement, ActionId.CosmoArrowDamage, 2.1f);
            Damage.Resolve(TopPositioned.From(placement), ActionId.CosmoArrowDamage, [DamageType.Lethal], []);
        });
    }

    // Eight proteans from the centre in two waves of four, then a wild charge the whole party
    // lines up in, a tank at its head.
    public void WaveCannon(IReadOnlyList<PartyRole> proteanFirstWave, PartyRole wildChargeTarget, float frontRequiredMitigation)
    {
        BossCast(ActionId.WaveCannon_7BA9, castSeconds: 10.6f, fireDelay: 0.27f, animationLock: 0.2f);
        world.Events.Add(3.03f, () => Proteans(proteanFirstWave));
        world.Events.Add(5.035f, () => Proteans(PerRole.All.Except(proteanFirstWave).ToList()));
        world.Events.Add(11.36f, () => WildCharge(wildChargeTarget, frontRequiredMitigation));
    }

    private void Proteans(IReadOnlyList<PartyRole> roles)
    {
        var shots = new List<Placement>();
        foreach (var role in roles)
        {
            if (party.Get(role) is not { } target || !target.IsAlive()) continue;
            var shot = new Placement(Vector3.Zero, 0f).Face(target.Position);
            HelperEffect(shot, ActionId.WaveCannonProtean, 1.1f, target);
            shots.Add(shot);
        }
        foreach (var shot in shots)
            Damage.Resolve(TopPositioned.From(shot), ActionId.WaveCannonProtean, [DamageType.Magic], [(StatusId.MagicVulnerabilityUp, MagicVulnerabilitySeconds)]);
    }

    // The line needs every living member. Only its front two, nearest the boss, take it as a
    // tankbuster; the rest share it.
    private void WildCharge(PartyRole targetRole, float frontRequiredMitigation)
    {
        if (Boss is not { } boss) return;
        var target = party.Get(targetRole) is { } t && t.IsAlive() ? t : party.ActiveMembers().FirstOrDefault();
        if (target == null) return;
        var line = new Placement(boss.Position, 0f).Face(target.Position);
        boss.SetRotation(line.Rotation);
        boss.NativeActionEffect(ActionId.WaveCannonWildCharge, 4.1f, (ushort)ActionId.WaveCannonWildCharge, 0, ActionType.Action, 0,
            rotation: line.Rotation, position: boss.Position, animationTargetId: boss.GameObjectId, actionTargetId: target.GameObjectId);
        var inLine = party.Find.InsideActionAoe(ActionId.WaveCannonWildCharge, line);
        var alive = party.ActiveMembers().Count();
        if (inLine.Count < alive)
        {
            Damage.Resolve(TopPositioned.From(line), ActionId.WaveCannonWildCharge, [DamageType.Magic], [], stackMinTargets: alive);
            return;
        }
        var front = inLine.Take(2).ToArray();
        var rest = inLine.Skip(2).ToArray();
        Damage.Resolve(TopPositioned.From(line), ActionId.WaveCannonWildCharge, [DamageType.TankBuster, DamageType.Magic], [], excludeTargets: rest,
            requiredMitigation: frontRequiredMitigation);
        Damage.Resolve(TopPositioned.From(line), ActionId.WaveCannonWildCharge, [DamageType.Magic], [], excludeTargets: front);
    }
}
