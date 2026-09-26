// The Dalamud services the plugin reaches through its static Plugin class, backed by VirtualGame.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.SafetyTests.Harness;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AnoMech.SafetyTests.Fakes
{
    internal sealed class FakeSigScanner
    {
        private readonly VirtualGame game;

        public FakeSigScanner(VirtualGame game) => this.game = game;

        public nint ScanText(string signature) => game.ScanText(signature);
    }

    internal sealed class FakeGameInterop
    {
        private readonly VirtualGame game;

        public FakeGameInterop(VirtualGame game) => this.game = game;

        public Hook<T> HookFromAddress<T>(nint address, T detour) where T : Delegate => game.CreateHook(address, detour);
    }

    internal sealed class FakeLog
    {
        private readonly VirtualGame game;

        public FakeLog(VirtualGame game) => this.game = game;

        public void Information(string line) => game.Diagnostic("LOG", line);
        public void Info(string line) => game.Diagnostic("LOG", line);
        public void Debug(string line) => game.Diagnostic("LOG", line);
        public void Warning(string line) => game.Diagnostic("LOG-WRN", line);
        public void Error(string line) => game.Diagnostic("LOG-ERR", line);
    }

    internal sealed class FakeAddonLifecycle
    {
        private readonly List<(AddonEvent Event, string Addon, Dalamud.Plugin.Services.IAddonLifecycle.AddonEventDelegate Handler)> listeners = new();

        public int ListenerCount => listeners.Count;

        public void RegisterListener(AddonEvent eventType, string addonName, Dalamud.Plugin.Services.IAddonLifecycle.AddonEventDelegate handler)
            => listeners.Add((eventType, addonName, handler));

        public void UnregisterListener(AddonEvent eventType, string addonName, Dalamud.Plugin.Services.IAddonLifecycle.AddonEventDelegate handler)
            => listeners.RemoveAll(l => l.Event == eventType && l.Addon == addonName && l.Handler == handler);
    }

    internal sealed class FakeDataManager
    {
        private readonly VirtualGame game;

        public FakeDataManager(VirtualGame game) => this.game = game;

        public ExcelSheet<T> GetExcelSheet<T>() where T : struct => game.Sheet<T>();
    }

    // Dalamud's plugin-scoped client state: TerritoryType is get-only and backed by no field of
    // either name ZoneSession's reflection tries, so its territory sync is a no-op, as in game.
    internal class FakeClientState
    {
        private readonly VirtualGame game;
        private uint stored;

        public FakeClientState(VirtualGame game) => this.game = game;

        public uint TerritoryType => Stored;
        public bool IsLoggedIn => game.LoggedIn;

        internal virtual uint Stored
        {
            get => stored;
            set => stored = value;
        }
    }

    // A client state whose territory the sync can write, for a Dalamud that exposes one.
    internal sealed class FakeClientStateSyncable : FakeClientState
    {
        private ushort territoryType;

        public FakeClientStateSyncable(VirtualGame game) : base(game)
        {
        }

        internal override uint Stored
        {
            get => territoryType;
            set => territoryType = (ushort)value;
        }
    }

    internal sealed class FakeObjectRef
    {
        public FakeObjectRef(nint address) => Address = address;

        public nint Address { get; }
    }

    internal sealed class FakeObjectTable : IEnumerable<FakeObjectRef>
    {
        private readonly VirtualGame game;

        public FakeObjectTable(VirtualGame game) => this.game = game;

        public FakeLocalPlayer? LocalPlayer => game.LocalPlayerVisible;

        public IEnumerator<FakeObjectRef> GetEnumerator() => game.ObjectAddresses().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class FakeLocalPlayer
    {
        private readonly VirtualGame game;

        public FakeLocalPlayer(VirtualGame game, int id, nint address)
        {
            this.game = game;
            FakeId = id;
            Address = address;
        }

        public int FakeId { get; }
        public nint Address { get; }
        public uint EntityId => 0x10000001;
        public Vector3 Position => game.ClientPosition;
        public float Rotation => game.ClientRotation;
        public byte Level => game.PlayerLevel;
        public bool IsCasting => game.Cast != null;
        public uint CastActionId => game.Cast?.ActionId ?? 0;
        public float CurrentCastTime => game.Cast?.Elapsed ?? 0f;
        public float TotalCastTime => game.Cast?.Total ?? 0f;
    }

    internal sealed class FakePlayerState
    {
        private readonly VirtualGame game;

        public FakePlayerState(VirtualGame game) => this.game = game;

        public RowRef<ClassJob> ClassJob => new(game.ClassJobId);
        public uint BaseRestedExperience => 0;
        public short Level => game.PlayerLevel;

        public int GetClassJobExperience(ClassJob classJob) => 0;
    }

    internal sealed unsafe class FakeCondition
    {
        private readonly VirtualGame game;

        public FakeCondition(VirtualGame game) => this.game = game;

        public bool this[ConditionFlag flag] => game.HasCondition(flag);
    }

    // Framework.Run never runs inline: Dalamud queues it on the framework thread's scheduler,
    // drained during the next framework update.
    internal sealed class FakeFramework
    {
        private readonly VirtualGame game;

        public FakeFramework(VirtualGame game) => this.game = game;

        public Task Run(Action action, CancellationToken cancellationToken = default)
        {
            game.QueueFrameworkTask(action);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeConfig
    {
        public bool SafeMode { get; set; } = true;
        public uint[] ZoneDownOpcodes { get; set; } = [];
    }
}
