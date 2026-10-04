using System;
using System.Numerics;

namespace AnoMech.Scenarios.Top.P3Intermission;

public static class TopP3IntermissionConstants
{
    public const uint FinalOmegaMaxHealth = 11_125_976;

    public const int StackPlayers = 2;
    public const float DebuffSeconds = 19f;
    public const float MagicVulnerabilitySeconds = 1.96f;
    // UNVERIFIED: nobody stood in the void zone, so how long its kill takes is a guess.
    public const float LethalResultDelay = 0.49f;

    public const float ArmRadius = 14f;
    public const float VoidzoneRadius = 6f;
    // IntermissionTeleportM's slide from here into the centre.
    public static readonly Vector3 NorthOmega = new(0f, 0f, -10f);
    public const float GlideSeconds = 0.71f;

    // The void zone's spawn: bound to the director, off once it sends state 7.
    public const uint VoidzoneEntityId = 0x4000EC00u;
    public const uint VoidzoneArg2 = (0x50u << 16) | 0x3u;
    public const uint VoidzoneOff = 7;

    public static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (6758, "mon_sp/gimmick/z3of_boss_gimmick17"),
        (10715, "mon_sp/gimmick/z3oz_boss_gimmick03"),
        (10716, "mon_sp/gimmick/z3oz_boss_gimmick04"),
        (10717, "mon_sp/gimmick/z3oz_boss_gimmick05"),
        (10718, "mon_sp/gimmick/z3oz_boss_gimmick06"),
        (6728, "mon_sp/gimmick/z3of_boss_gimmick01"),
        (6730, "mon_sp/gimmick/z3of_boss_gimmick02"),
    ];
}
