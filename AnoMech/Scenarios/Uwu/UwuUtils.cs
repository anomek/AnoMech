using AnoMech.Core.Native.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

public partial class UwuUtils(SimWorld world)
{
    private readonly SimWorld world = world;

    public void UpdateArena(byte value)
    {
        byte[] unionData = [value];
        Natives.Director.SetDirectorData(1, 0, unionData);
    }

    public void Awaken(SimEnemy? enemy, bool isUltima)
    {
        enemy?.AddStatusParam(StatusId.Woken, isUltima ? 97 : 0);
        enemy?.SetAnimationState(0, 1);
    }

    // Each dummy drops its rain where its target stood at `targetOffset`.
    public void FeatherRain(Func<SimEnemy?>[] getDummies, float targetOffset, float castOffset,
        IReadOnlyList<PartyRole>? targets = null, Func<SimCharacter?>? getExcluded = null)
    {
        var positions = new List<Vector3>();

        world.Events.Add(targetOffset, () =>
        {
            PartyRole[] excluded = getExcluded?.Invoke() is ISimPartyMember { Role: var role } ? [role] : [];
            positions.AddRange(
                (targets ?? RoleList.AllExcept(world.Rng, world.Party, excluded).List.Take(getDummies.Length))
                .Select(x => world.Party.Get(x)!.Position));
        });

        world.Events.Add(castOffset, () =>
        {
            for (var i = 0; i < getDummies.Length; i++)
            {
                var dummy = getDummies[i]();
                dummy?.SetPosition(new Placement(positions[i], 0));
                dummy?.Cast(UwuActions.FeatherRain, positions[i]);
            }
        });
    }

    public void EruptionPuddle(Func<SimEnemy?> getDummy, Func<SimCharacter?> getBait, float castOffset)
        => world.Events.Add(castOffset, () => getDummy()?.Cast(UwuActions.EruptionPuddle, getBait()!.Position));

    public void LandslideLines(Func<SimEnemy?> getEnemy, Func<SimEnemy?>[] getDummies, float castOffset, LandslideType type)
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
            for (var i = 0; i < getDummies.Length; i++)
            {
                var dummy = getDummies[i]();
                dummy?.SetPosition(new Placement(enemy.Position, enemy.Rotation + rotationOffsets[i]));
                dummy?.Cast(action);
            }
        });
    }
}
