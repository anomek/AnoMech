// Stand-ins for the plugin types the included sources reach outside their own files, plus three
// shadows inside AnoMech.Core.Map that redirect the zone session's clock, deferred work and
// process kill into VirtualGame. A type declared in the source's own namespace takes precedence
// over its using directives, so ZoneSession compiles against these unmodified.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AnoMech.SafetyTests.Fakes;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AnoMech
{
    internal static class Plugin
    {
        public static FakeSigScanner SigScanner => VirtualGame.Current.SigScanner;
        public static FakeGameInterop GameInterop => VirtualGame.Current.GameInterop;
        public static FakeLog Log => VirtualGame.Current.Log;
        public static FakeAddonLifecycle AddonLifecycle => VirtualGame.Current.AddonLifecycle;
        public static FakeDataManager DataManager => VirtualGame.Current.DataManager;
        public static FakeClientState ClientState => VirtualGame.Current.ClientState;
        public static FakeObjectTable ObjectTable => VirtualGame.Current.ObjectTable;
        public static FakePlayerState PlayerState => VirtualGame.Current.PlayerState;
        public static FakeCondition Condition => VirtualGame.Current.Condition;
        public static FakeFramework Framework => VirtualGame.Current.Framework;
        public static FakeConfig Config => VirtualGame.Current.Config;
    }
}

namespace AnoMech.Core
{
    internal static class DiagnosticLog
    {
        public static void Info(string line) => VirtualGame.Current.Diagnostic("INF", line);
        public static void Warn(string line) => VirtualGame.Current.Diagnostic("WRN", line);
        public static void Debug(string line) => VirtualGame.Current.Diagnostic("DBG", line);

        public static string? Fatal(string line)
        {
            VirtualGame.Current.Diagnostic("FTL", line);
            return "fatal.log";
        }
    }

    internal static class ActionLookup
    {
        public static string Name(uint actionId) => actionId switch
        {
            5 => "Teleport",
            6 => "Return",
            _ => $"action {actionId}",
        };
    }
}

namespace AnoMech.Pointers
{
    using System.Runtime.InteropServices;
    using FFXIVClientStructs.FFXIV.Client.Game;
    using FFXIVClientStructs.FFXIV.Client.Game.Event;

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct UpdateClassInfoPacket
    {
        [FieldOffset(0x0)] public byte ClassJobId;
        [FieldOffset(0x2)] public ushort CurrentLevel;
        [FieldOffset(0x4)] public ushort ClassJobLevel;
        [FieldOffset(0x6)] public ushort SyncedLevel;
        [FieldOffset(0x8)] public ushort ClassJobExp;
        [FieldOffset(0xC)] public uint BaseRestedExperience;
    }

    internal static unsafe class EventFrameworkPointers
    {
        public static void InitDirector(EventFramework* thisPtr, uint eventId, uint contentId, uint flags)
            => VirtualGame.Current.NativeInitDirector(eventId, contentId);

        public static void TerminateDirector(EventFramework* thisPtr, uint eventId)
            => VirtualGame.Current.NativeTerminateDirector(eventId);
    }

    internal static unsafe class GameMainPointers
    {
        public static void LoadZone(GameMain* thisPtr, uint territoryTypeId, uint transitionTerritoryFilterKey, byte a4, byte a5)
            => VirtualGame.Current.NativeLoadZone(territoryTypeId);
    }

    internal static unsafe class PacketDispatcherPointers
    {
        public static void HandleUpdateClassInfoPacket(uint entityId, UpdateClassInfoPacket* packet)
            => VirtualGame.Current.NativeUpdateClassInfo(packet->SyncedLevel);
    }
}

namespace AnoMech.Helpers
{
    internal static class InstanceContentDirectorHelper
    {
        public static void SetDutyData(ContentFinderCondition contentFinderCondition)
            => VirtualGame.Current.Note("native: duty data set");

        public static void Commence() => VirtualGame.Current.Note("native: director commenced");

        public static bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
            => VirtualGame.Current.NativeDirectorUpdate(category);
    }
}

namespace AnoMech.Core.Map
{
    internal sealed class MapEffects : IDisposable
    {
        private readonly HashSet<byte> suppressed = new();

        public bool Loaded { get; set; }
        public IEnumerable<byte> SuppressedSlots => suppressed;

        public bool Apply(uint packetFlags, byte index) => VirtualGame.Current.NativeMapEffect(packetFlags, index);
        public bool SuppressSlot(byte index) => suppressed.Add(index) || true;
        public void KeepSlotSuppressed(byte index) { }
        public void ForgetSuppressions() => suppressed.Clear();
        public void RestoreSlot(byte index) => suppressed.Remove(index);
        public void LogAllSlots(string label) { }
        public void Dispose() => VirtualGame.Current.Note("native: map effects disposed");
    }

    internal static class LayoutQuery
    {
        public static List<nint> CollectLayerInstances(ushort layerKey) => new();
        public static string DescribeActiveLayers() => "(fake layout)";
        public static string DescribeLiveEffects() => "(fake effects)";
    }

    internal static class DirectorFunctions
    {
        public static int DisableSpawnAreaColliders(Vector3 center, float radius) => 1;
    }

    // Shadows System.Diagnostics.Stopwatch for the zone session: the simulated clock.
    internal static class Stopwatch
    {
        public static readonly long Frequency = TimeSpan.TicksPerSecond;

        public static long GetTimestamp() => VirtualGame.Current.Ticks;

        public static TimeSpan GetElapsedTime(long startingTimestamp) => new(VirtualGame.Current.Ticks - startingTimestamp);

        public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) => new(endingTimestamp - startingTimestamp);
    }

    // Shadows System.Environment: FailFast never returns, which GameTerminated models by unwinding
    // to the harness after the death is recorded.
    internal static class Environment
    {
        public static long TickCount64 => VirtualGame.Current.Ticks / TimeSpan.TicksPerMillisecond;

        public static string NewLine => System.Environment.NewLine;

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void FailFast(string? message) => VirtualGame.Current.FailFast(message);
    }

    // Shadows the zone session's ThreadingTask alias: Delay(ms).ContinueWith(..) becomes a timer on
    // the simulated clock, whose continuation runs off the framework thread like the real one.
    internal static class ThreadingTask
    {
        public static FakeDelay Delay(int milliseconds) => new(milliseconds);
    }

    internal sealed class FakeDelay
    {
        private readonly int milliseconds;

        public FakeDelay(int milliseconds) => this.milliseconds = milliseconds;

        public FakeDelay ContinueWith<TResult>(Func<FakeDelay, TResult> continuation)
        {
            VirtualGame.Current.AddTimer(milliseconds, () => continuation(this), "ThreadingTask.Delay continuation");
            return this;
        }
    }
}
