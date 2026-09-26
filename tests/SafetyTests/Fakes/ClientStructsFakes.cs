// Stand-ins for the FFXIVClientStructs types the included plugin sources touch. Every native
// singleton and member function lands in VirtualGame, which plays the game client.
using System;
using System.Runtime.InteropServices;
using AnoMech.SafetyTests.Harness;

namespace FFXIVClientStructs.FFXIV.Client.Game
{
    public enum ActionType : uint
    {
        None = 0, Action = 1, Item = 2, EventItem = 3, EventAction = 4, GeneralAction = 5, BuddyAction = 6,
        MainCommand = 7, Companion = 8, CraftAction = 9, PetAction = 11, Mount = 13, PvPAction = 14,
    }

    // One bool per ConditionFlag value, which is how Dalamud's ICondition reads it.
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public unsafe struct Conditions
    {
        [FieldOffset(25)] public bool Occupied;
        [FieldOffset(28)] public bool SufferingStatusAffliction;
        [FieldOffset(29)] public bool SufferingStatusAffliction2;
        [FieldOffset(63)] public bool SufferingStatusAffliction63;
        [FieldOffset(72)] public bool SufferingStatusAffliction72;
        [FieldOffset(73)] public bool SufferingStatusAffliction73;

        public static Conditions* Instance() => (Conditions*)VirtualGame.Current.ConditionsMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GameMain
    {
        public uint CurrentTerritoryTypeId;

        public static GameMain* Instance() => (GameMain*)VirtualGame.Current.GameMainMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Status
    {
        public ushort StatusId;
        public ushort Param;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StatusManager
    {
        public int Reserved;
        public Span<Status> Status => Span<Status>.Empty;
    }

    public enum InventoryType : uint { Inventory1 = 0, EquippedItems = 1000 }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InventoryItem
    {
        public uint ItemId;
        public Span<ushort> Materia => new ushort[5];
        public Span<byte> MateriaGrades => new byte[5];

        public static uint GetParameterMaxValue(uint baseParamId, void* item) => 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InventoryContainer
    {
        public InventoryItem* Items;
        public int Size;

        public int GetSize() => Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InventoryManager
    {
        public InventoryContainer Equipped;

        public static InventoryManager* Instance() => (InventoryManager*)VirtualGame.Current.InventoryMemory;

        public InventoryContainer* GetInventoryContainer(InventoryType type)
            => (InventoryContainer*)VirtualGame.Current.InventoryMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WeatherManager
    {
        public byte WeatherId;
        public byte WeatherOverride;
        public byte WeatherIndex;
        public Span<ServerWeather> Weathers => Span<ServerWeather>.Empty;

        [StructLayout(LayoutKind.Sequential)]
        public struct ServerWeather
        {
            public byte NextWeatherId;
            public byte CurrentWeatherId;
            public bool IsCurrentWeatherForced;
            public bool IsNextWeatherForced;
        }

        public byte GetCurrentWeather() => WeatherId;

        // The weather diagnostics are logging only; the fake has no weather manager.
        public static WeatherManager* Instance() => null;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Game.Object
{
    [StructLayout(LayoutKind.Explicit)]
    public struct GameObjectId
    {
        [FieldOffset(0)] public ulong Id;
        [FieldOffset(0)] public uint ObjectId;
        [FieldOffset(4)] public byte Type;

        public static implicit operator ulong(GameObjectId id) => id.Id;

        public static implicit operator GameObjectId(ulong id) => new() { Id = id };
    }

    // Every fake object's memory starts with its VirtualGame id, so the member functions can find
    // the object they were called on from `this`.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GameObject
    {
        public int FakeId;

        public void SetPosition(float x, float y, float z) => VirtualGame.Current.NativeSetPosition(FakeId, x, y, z);

        public void SetRotation(float rotation) => VirtualGame.Current.NativeSetRotation(FakeId, rotation);

        public void DisableDraw() => VirtualGame.Current.NativeDisableDraw(FakeId);
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Game.Character
{
    using FFXIVClientStructs.FFXIV.Client.Game;
    using FFXIVClientStructs.FFXIV.Client.Game.Object;

    [StructLayout(LayoutKind.Sequential)]
    public struct Character
    {
        public GameObject GameObject;
        public byte Level;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BattleChara
    {
        public Character Character;
        public StatusManager StatusManager;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Game.Event
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EventFramework
    {
        public int Reserved;

        public static EventFramework* Instance() => (EventFramework*)VirtualGame.Current.EventFrameworkMemory;

        public void SetTerritoryTypeId(ushort territory) => VirtualGame.Current.NativeSetEventFrameworkTerritory(territory);
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Game.UI
{
    public enum PlayerAttribute : uint
    {
        Piety = 6, Tenacity = 19, DirectHitRate = 22, CriticalHit = 27, Determination = 44, SkillSpeed = 45, SpellSpeed = 46,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DirectorTodo
    {
        public bool IsShown;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PlayerState
    {
        private fixed int attributes[128];

        [System.Diagnostics.CodeAnalysis.UnscopedRef]
        public Span<int> Attributes => System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref attributes[0], 128);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UIState
    {
        public DirectorTodo DirectorTodo;
        public PlayerState PlayerState;

        public static UIState* Instance() => (UIState*)VirtualGame.Current.UIStateMemory;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Graphics.Scene
{
    [StructLayout(LayoutKind.Sequential, Size = 0x100)]
    public struct EnvScene
    {
        public uint LocationCount;
    }

    public struct EnvLocation
    {
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EnvSpace
    {
        public EnvLocation* EnvLocation;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Graphics.Environment
{
    using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

    [StructLayout(LayoutKind.Sequential, Size = 0x400)]
    public struct EnvState
    {
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x400)]
    public struct EnvSimulator
    {
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EnvManager
    {
        public byte ActiveWeather;
        public float TransitionTime;
        public EnvScene* EnvScene;
        public EnvSpace* EnvSpace;
        public EnvState EnvState;
        public EnvSimulator EnvSimulator;

        public static EnvManager* Instance() => (EnvManager*)VirtualGame.Current.EnvManagerMemory;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Network
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PacketDispatcher
    {
        public int Reserved;

        public static PacketDispatcherVirtualTable* StaticVirtualTablePointer
            => (PacketDispatcherVirtualTable*)VirtualGame.Current.PacketDispatcherVTableMemory;

        public static void HandleActorControlPacket(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
            uint arg5, uint arg6, uint arg7, uint arg8, FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId targetId, bool isReplay)
            => VirtualGame.Current.NativeActorControl(entityId, category, arg1, arg2);

        [StructLayout(LayoutKind.Sequential)]
        public struct PacketDispatcherVirtualTable
        {
            public delegate* unmanaged<PacketDispatcher*, uint, nint, void> OnReceivePacket;
        }

        public static class Delegates
        {
            public delegate void OnReceivePacket(PacketDispatcher* thisPtr, uint targetId, nint packet);
        }
    }
}

namespace FFXIVClientStructs.FFXIV.Client.UI.Agent
{
    public enum AgentId : uint { Social = 56 }

    [StructLayout(LayoutKind.Sequential)]
    public struct AgentInterface
    {
        public int Reserved;

        public void Show() => VirtualGame.Current.Note("native: Social agent shown");

        public void Hide() => VirtualGame.Current.Note("native: Social agent hidden");
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AgentModule
    {
        public int Reserved;

        public static AgentModule* Instance() => (AgentModule*)VirtualGame.Current.AgentModuleMemory;

        public AgentInterface* GetAgentByInternalId(AgentId id) => (AgentInterface*)VirtualGame.Current.AgentMemory;
    }
}

namespace FFXIVClientStructs.FFXIV.Client.LayoutEngine
{
    public struct ILayoutInstance
    {
        public int Reserved;

        public void SetActive(bool active)
        {
        }
    }
}
