using AnoMech.Core.Native.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu;

public enum LandslideType
{
    Normal,
    Awaken,
    Ultima
}

public class UwuUtils(SimWorld world)
{
    private readonly SimWorld world = world;

    public void UpdateArena(byte value)
    {
        byte[] unionData = [value];
        Natives.Director.SetDirectorData(1, 0, unionData, true);
    }

    // Server-spawned floor EObj; the sky itself comes from the phase weather.
    public SimEventObject? SpawnArenaFloor() => world.SpawnEventObject(new EventObjectSpawnConfig
    {
        EObjId = 2007457,
        Placement = new(new(0.16f, 0, 1.4434f), 0),
        ObjectIndex = 1,
        TargetableStatus = 5,
        EntityId = 0x4000829C,
        LayoutId = 7538913,
        GimmickId = 7538258,
        TimelineState = 1,
    });

    // Titan's arena EObj (LVD_Battle_Titan): its yellow ring shows up and shrinks on the jumps.
    public SimEventObject? SpawnTitanArena() => world.SpawnEventObject(new EventObjectSpawnConfig
    {
        EObjId = 2007457,
        Placement = new(Vector3.Zero, 0),
        ObjectIndex = 2,
        TargetableStatus = 5,
        EntityId = 0x4000829D,
        LayoutId = 7372736,
        GimmickId = 7372735,
        TimelineState = 1,
        RestoreStateOnDespawn = true,
        ForceSharedGroupActive = true,
    });

    // Native head marker; its AVFX ends on its own, so it isn't tracked as a SimVfx.
    public static void Lockon(SimCharacter? target, uint lockonId)
    {
        if (target == null) return;
        target.ActorControl(SetLockonControl, lockonId, target.GameObjectId.ObjectId);
    }

    private const uint SetLockonControl = 34;

    public static void CastSelf(SimEnemy? caster, uint actionId, float castSeconds) =>
        caster?.NativeCast(actionId, ActionType.Action, 0f, castSeconds, false, targetId: caster.GameObjectId);

    // An effect without a position plays at the arena centre, so default it to the caster.
    public static void PlayEffect(SimEnemy? caster, uint actionId, float animationLock, float? rotation = null, GameObjectId? target = null, Vector3? at = null) =>
        caster?.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            rotation: rotation, position: at ?? caster.Position, animationTargetId: target ?? caster.GameObjectId);

    public void Awaken(SimEnemy? enemy, bool isUltima)
    {
        enemy?.AddStatusParam(StatusId.Woken, isUltima ? 97 : 0);
        enemy?.SetAnimationState(0, 1);
    }

    // Kills a snapshotted hit list, sparing gaol prisoners.
    public static void KillSnapshot(DamageSolver damage, IEnumerable<SimCharacter> snapshot, uint actionId, string context)
    {
        foreach (var hit in snapshot.Where(h => !h.HasStatus(StatusId.Fetters)).ToList())
            damage.ApplyDamage(hit, 1f, actionId, context, lethal: true);
    }

    public void ResolveSnapshot(IReadOnlyList<SimCharacter> snapshot, string dieCause)
    {
        foreach (var character in snapshot)
        {
            if (character.HasStatus(StatusId.Fetters))
            {
                continue;
            }

            character.Die(dieCause);
        }
    }

    public void Cast(Func<SimEnemy?> getEnemy,
        float castOffset, UwuUtilsRecords castInfo, float effectOffset, ActionEffectInfo actionEffectInfo,
        DynamicInfo dynamicInfo,
        float resolveDelay = 0, Action<IReadOnlyList<SimCharacter>>? resolveAction = null)
    {
        world.Events.Add(castOffset, () =>
        {
            var enemy = getEnemy();

            enemy?.NativeCast(
                castInfo.ActionId,
                castInfo.ActionType,
                castInfo.OmenDelay,
                castInfo.CastTime,
                castInfo.Interruptible,
                dynamicInfo.GetValue(dynamicInfo.CastRotation),
                dynamicInfo.GetValue(dynamicInfo.CastPosition),
                dynamicInfo.GetValue(dynamicInfo.CastTarget),
                dynamicInfo.GetValue(dynamicInfo.CastBallistaTarget)
                );
        });

        IReadOnlyList<SimCharacter> snapshot = null!;

        if (resolveAction != null)
        {
            world.Events.Add(castOffset + castInfo.CastTime, () =>
            {
                var enemy = getEnemy();
                Placement placement;

                if (dynamicInfo.CastRotation != null && dynamicInfo.CastPosition != null)
                {
                    placement = new(dynamicInfo.CastPosition(), dynamicInfo.CastRotation());
                }
                else if (dynamicInfo.CastRotation != null)
                {
                    placement = new(enemy!.Position, dynamicInfo.CastRotation());
                }
                else if (dynamicInfo.CastPosition != null)
                {
                    placement = new(dynamicInfo.CastPosition(), enemy!.Rotation);
                }
                else
                {
                    placement = enemy!.Placement();
                }

                snapshot = world.Party.Find.InsideActionAoe(castInfo.ActionId, placement);
            });
        }

        world.Events.Add(effectOffset, () =>
        {
            var enemy = getEnemy();

            enemy?.NativeActionEffect(
                actionEffectInfo.ActionId,
                actionEffectInfo.AnimationLock,
                actionEffectInfo.SpellId,
                actionEffectInfo.AnimationVariaton,
                actionEffectInfo.ActionType,
                actionEffectInfo.Flags,
                dynamicInfo.GetValue(dynamicInfo.ActionEffectRotation),
                dynamicInfo.GetValue(dynamicInfo.ActionEffectPosition),
                dynamicInfo.GetValue(dynamicInfo.ActionEffectAnimationTarget),
                dynamicInfo.GetValue(dynamicInfo.ActionEffectActionTarget),
                dynamicInfo.GetValue(dynamicInfo.ActionEffectBallistaTarget)
                );
        });

        if (resolveAction != null)
        {
            world.Events.Add(effectOffset + resolveDelay, () => resolveAction(snapshot));
        }
    }

    public void FeatherRain(Func<SimEnemy?>[] getDummies, float snapshotOffset, float castOffset, float effectOffset, Action<Vector3>? onTargeted = null,
        Action<IReadOnlyList<SimCharacter>>? resolve = null)
    {
        var positions = new List<Vector3>();

        world.Events.Add(snapshotOffset, () =>
        {
            positions.AddRange(
                RoleList.Random(world.Rng, world.Party, getDummies.Length).List
                .Select(x => world.Party.Get(x)!.Position));
            if (onTargeted != null) positions.ForEach(onTargeted);
        });

        var castInfo = new UwuUtilsRecords
        {
            ActionId = ActionId.FeatherRain,
            ActionType = ActionType.Action,
            CastTime = 0.7f
        };

        var actionEffectInfo = new ActionEffectInfo
        {
            ActionId = ActionId.FeatherRain,
            AnimationLock = 1.1f,
            SpellId = (ushort)ActionId.FeatherRain,
            ActionType = ActionType.Action
        };

        for (int i = 0; i < getDummies.Length; i++)
        {
            var getDummy = getDummies[i];

            var dynamicInfo = new DynamicInfo
            {
                ActionEffectPosition = () => getDummy()!.Position
            };

            // if "i" is used directly, then the value will be 5 when the Action is executed
            var index = i;

            world.Events.Add(castOffset, () =>
            {
                var dummy = getDummy();

                dummy?.SetPosition(
                    new Placement(
                        positions[index],
                        0
                        ));
            });

            Cast(getDummy, castOffset, castInfo, effectOffset, actionEffectInfo, dynamicInfo, 0.2f, resolve ?? (snapshot => ResolveSnapshot(snapshot, "Feather Rain")));
        }
    }

    public void EruptionPuddle(Func<SimEnemy?> getDummy, Func<SimCharacter?> getBait, float castOffset, float effectOffset)
    {
        var baitPosition = Vector3.Zero;

        var getBaitPosition = () => baitPosition;
        var getPi = () => float.Pi;

        world.Events.Add(castOffset, () =>
        {
            baitPosition = getBait()!.Position;
        });

        var castInfo = new UwuUtilsRecords
        {
            ActionId = ActionId.EruptionPuddle,
            ActionType = ActionType.Action,
            OmenDelay = 0f,
            CastTime = 2.7f,
            Interruptible = false
        };

        var actionEffectInfo = new ActionEffectInfo
        {
            ActionId = ActionId.EruptionPuddle,
            AnimationLock = 0.1f,
            SpellId = (ushort)ActionId.EruptionPuddle,
            AnimationVariaton = 0,
            ActionType = ActionType.Action,
            Flags = 0
        };

        var dynamicInfo = new DynamicInfo
        {
            CastRotation = getPi,
            CastPosition = getBaitPosition,
            ActionEffectRotation = getPi,
            ActionEffectPosition = getBaitPosition,
        };

        Cast(getDummy, castOffset, castInfo, effectOffset, actionEffectInfo, dynamicInfo, 0.66f, snapshot => ResolveSnapshot(snapshot, "Eruption"));
    }

    public void LandslideLines(Func<SimEnemy?> getEnemy, Func<SimEnemy?>[] getDummies, float castOffset, float effectOffset, LandslideType type)
    {
        float[] rotationOffsets;
        uint actionId;
        float castTime;
        float animationLock;

        switch (type)
        {
            case LandslideType.Normal:
                rotationOffsets = Geometry.TitanLandslideOffsets.ToArray();
                actionId = ActionId.LandslideLine;
                castTime = 1.9f;
                animationLock = 2.1f;
                break;
            case LandslideType.Awaken:
                rotationOffsets = Geometry.TitanLandslideAwakenOffsets.ToArray();
                actionId = ActionId.LandslideAwaken;
                castTime = 1.7f;
                animationLock = 1.1f;
                break;
            case LandslideType.Ultima:
                rotationOffsets = Geometry.UltimaLandslideOffsets.ToArray();
                actionId = ActionId.LandslideLineUltima;
                castTime = 1.9f;
                animationLock = 1.1f;
                break;
            default:
                throw new Exception($"[UltimatePredationScenario.Landslide] Unsupported LandslideType {type}");
        }

        var castInfo = new UwuUtilsRecords
        {
            ActionId = actionId,
            ActionType = ActionType.Action,
            CastTime = castTime
        };

        var actionEffectInfo = new ActionEffectInfo
        {
            ActionId = actionId,
            AnimationLock = animationLock,
            SpellId = (ushort)actionId,
            ActionType = ActionType.Action
        };

        for (int i = 0; i < getDummies.Length; i++)
        {
            var getDummy = getDummies[i];

            var dynamicInfo = new DynamicInfo
            {
                CastTarget = getDummy,
                ActionEffectAnimationTarget = getDummy
            };

            // if "i" is used directly, then the value will be 5 when the Action is executed
            var index = i;

            world.Events.Add(castOffset, () =>
            {
                var enemy = getEnemy();
                var dummy = getDummy();

                dummy?.SetPosition(
                    new Placement(
                        enemy!.Position,
                        enemy.Rotation + rotationOffsets[index]
                        ));
            });

            Cast(getDummy, castOffset, castInfo, effectOffset, actionEffectInfo, dynamicInfo, 0.73f, snapshot =>
            {
                foreach (var character in snapshot)
                {
                    // TODO: "30" and "50" are from Titan EX, but doubled. Need to find the proper UWU values.
                    (character as ISimPartyMember)?.Knockback(getEnemy()!.Position, 30, 50);
                }
            });
        }
    }
}
