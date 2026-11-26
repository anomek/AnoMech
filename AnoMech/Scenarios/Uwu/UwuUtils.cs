using AnoMech.Core.Native.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
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
    public const byte Level = 70;

    private readonly SimWorld world = world;

    // Server-spawned floor EObj; the sky itself comes from the phase weather.
    public SimEventObject? SpawnArenaFloor() => world.SpawnEventObject(new EventObjectSpawnConfig
    {
        EObjId = 2007457,
        Placement = new(new(0.16f, 0, 1.4434f), 0),
        TargetableStatus = 5,
        EntityId = 0x4000829C,
        LayoutId = 7538913,
        GimmickId = 7538258,
        TimelineState = 1,
    });

    // Native head marker; its AVFX ends on its own, so it isn't tracked as a SimVfx.
    public static void Lockon(SimCharacter? target, uint lockonId)
    {
        if (target == null) return;
        target.ActorControl.HeadMarker(lockonId);
    }

    public void UpdateArena(byte value)
    {
        byte[] unionData = [value];
        Natives.Director.SetDirectorData(1, 0, unionData);
    }

    // The awakening is earned: each charge adds an Aetherially Charged stack, which Awaken swaps for Woken.
    public static void Charge(SimEnemy? enemy) => enemy?.AddStatus(StatusId.AetheriallyCharged);

    public static int Charges(SimEnemy? enemy) => enemy?.FindStatus(StatusId.AetheriallyCharged)?.Stacks ?? 0;

    public void Awaken(SimEnemy? enemy, bool isUltima)
    {
        enemy?.RemoveStatus(StatusId.AetheriallyCharged);
        enemy?.AddStatusParam(StatusId.Woken, WokenParam(isUltima));
        enemy?.SetAnimationState(0, AwakenedAnimationState);
    }

    // An enemy already awakened when the scenario starts spawns that way.
    public static SpawnStatus[] WokenAtSpawn(bool isUltima) => [new(StatusId.Woken, WokenParam(isUltima))];

    public const byte AwakenedAnimationState = 1;

    private static ushort WokenParam(bool isUltima) => isUltima ? (ushort)97 : (ushort)0;

    // Five rains, each dropped where its target stood at `targetOffset`; `onTargeted` learns each spot then.
    public void FeatherRain(Func<SimEnemy?> getCaster, float targetOffset, float castOffset,
        IReadOnlyList<PartyRole>? targets = null, Func<SimCharacter?>? getExcluded = null, Action<Vector3>? onTargeted = null)
    {
        const int rains = 5;
        var positions = new List<Vector3>();

        world.Events.Add(targetOffset, () =>
        {
            PartyRole[] excluded = getExcluded?.Invoke() is ISimPartyMember { Role: var role } ? [role] : [];
            positions.AddRange(
                (targets ?? RoleList.AllExcept(world.Rng, world.Party, excluded).List.Take(rains))
                .Select(x => world.Party.Get(x)!.Position));
            if (onTargeted != null) positions.ForEach(onTargeted);
        });

        world.Events.Add(castOffset, () =>
        {
            // Unlike the other location casts, retail teleports Feather Rain's helpers onto their spot, turned south.
            foreach (var position in positions.Take(rains))
                getCaster()?.CastFromHelper(UwuActions.FeatherRain, position, at: new Placement(position, 0f));
        });
    }

    public void EruptionPuddle(Func<SimEnemy?> getCaster, Func<SimCharacter?> getBait, float castOffset)
        => world.Events.Add(castOffset, () => CastOnLocation(getCaster(), UwuActions.EruptionPuddle, getBait()!.Position));

    // Retail's location casts carry a north rotation; their helpers cast from wherever they stand,
    // which the pool can't do, so it stands them on the location.
    public static Placement OnLocation(Vector3 location) => new(location, MathF.PI);

    public static void CastOnLocation(SimEnemy? caster, EnemyAction action, Vector3 location) =>
        caster?.CastFromHelper(action, location, at: OnLocation(location));

    // One line per offset, each from the enemy's spot turned by its offset.
    public void LandslideLines(Func<SimEnemy?> getEnemy, float castOffset, LandslideType type)
    {
        var (rotationOffsets, action) = type switch
        {
            LandslideType.Normal => (Geometry.TitanLandslideOffsets.ToArray(), UwuActions.LandslideLine),
            LandslideType.Awaken => (Geometry.TitanLandslideAwakenOffsets.ToArray(), UwuActions.LandslideAwaken),
            LandslideType.Ultima => (Geometry.UltimaLandslideOffsets.ToArray(), UwuActions.LandslideLineUltima),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        world.Events.Add(castOffset, () =>
        {
            var enemy = getEnemy()!;
            foreach (var offset in rotationOffsets)
                enemy.CastFromHelper(action, at: new Placement(enemy.Position, enemy.Rotation + offset));
        });
    }
}
