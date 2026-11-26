using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Multiplayer;

// Validation for everything off the wire; the relay forwards whatever any session member
// sends. Limits are ~10x what a real 8-player run produces.
internal static class NetGuard
{
    public const int MaxStringLength = 192;
    public const int MaxReportedEnemies = 256;
    public const int MaxMapEffectSlots = 128;
    // Bound how long a burst can hold the framework thread.
    public const int MaxQueuedMessages = 8192;
    public const int MaxMessagesPerDrain = 2048;
    public const int MaxPacketsPerMessage = 4096;
    // Past the longest cast bar a player action has.
    public const float MaxPlayerCastSeconds = 10f;
    public const int MaxSessionPeers = 64;
    public const uint MaxHp = 50_000_000;
    public const float MaxMitigationSeconds = 120f;

    private const float MaxCoordinate = 4096f;
    private const float MaxSpeed = 100f;

    // Control characters would corrupt the diagnostic log's line format, and an unbounded
    // string is a per-frame ImGui hang.
    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var span = value.Length > MaxStringLength ? value.AsSpan(0, MaxStringLength) : value.AsSpan();
        Span<char> buffer = stackalloc char[span.Length];
        var length = 0;
        foreach (var c in span)
            if (!char.IsControl(c)) buffer[length++] = c;
        return new string(buffer[..length]);
    }

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float Clamp(float value, float min, float max, float fallback = 0f)
        => IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public static bool TryPosition(float x, float y, float z, out Vector3 position)
    {
        position = default;
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) return false;
        if (MathF.Abs(x) > MaxCoordinate || MathF.Abs(y) > MaxCoordinate || MathF.Abs(z) > MaxCoordinate) return false;
        position = new Vector3(x, y, z);
        return true;
    }

    public static Vector3? TryPosition(float? x, float? y, float? z)
        => x is { } px && y is { } py && z is { } pz && TryPosition(px, py, pz, out var position) ? position : null;

    public static float Rotation(float value) => IsFinite(value) ? value : 0f;

    public static Vector3 Velocity(float x, float z) => new(Clamp(x, -MaxSpeed, MaxSpeed), 0f, Clamp(z, -MaxSpeed, MaxSpeed));

    public static IReadOnlyList<T> Cap<T>(IReadOnlyList<T>? items, int max)
    {
        if (items == null) return [];
        if (items.Count <= max) return items;
        var capped = new T[max];
        for (var i = 0; i < max; i++) capped[i] = items[i];
        return capped;
    }

    // Written straight onto a BattleChara, whose sheet row the party list and nameplate resolve.
    public static byte ClassJob(byte value) =>
        value != 0 && Core.Native.Interfaces.Natives.Data.ClassJob(value) != null ? value : (byte)0;

    public static bool InRange(int index, int count) => index >= 0 && index < count;

    public static bool IsReplayableDirectorCategory(uint category) => PacketRules.DirectorCategories.Contains(category);

    private static readonly HashSet<uint> warnedActorControls = [];

    public static bool IsReplayableActorControl(uint category)
    {
        if (PacketRules.Controls.ContainsKey(category)) return true;
        if (warnedActorControls.Add(category))
            Core.DiagnosticLog.Warn($"[Multiplayer] Dropped ActorControl category 0x{category:X} from the host -- this build never sends it.");
        return false;
    }
}
