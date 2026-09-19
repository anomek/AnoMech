using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Dalamud.Game.ClientState.Conditions { public enum ConditionFlag { Jumping } }
namespace Dalamud.Utility.Signatures {
    [AttributeUsage(AttributeTargets.Field)] public sealed class SignatureAttribute : Attribute { public SignatureAttribute(string value) { } }
}
namespace Dalamud.Hooking {
    public sealed class Hook<T> where T : Delegate {
        public T Original = null!;
        public void Enable() { } public void Dispose() { }
    }
}
namespace Dalamud.Plugin.Services {
    public interface IGameInteropProvider {
        void InitializeFromAttributes(object target);
        Hook<T> HookFromAddress<T>(nint address, T detour) where T : Delegate;
    }
}
namespace FFXIVClientStructs.FFXIV.Client.System.Input {
    public enum InputId { JUMP, PAD_JUMPANDCANCELCAST }
    public unsafe struct InputData {
        public static class Delegates { public delegate bool IsInputIdPressed(InputData* self, InputId id); }
        public static class Addresses { public static Address IsInputIdPressed = new(); }
    }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.UI {
    public struct WeaponState { public AutoAttackState AutoAttackState; }
    public struct AutoAttackState { public bool IsAutoAttacking; }
    public unsafe struct UIState {
        public WeaponState WeaponState;
        private static readonly UIState* instance = (UIState*)NativeMemory.AllocZeroed((nuint)sizeof(UIState));
        public static UIState* Instance() => instance;
    }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.Object {
    public struct GameObjectId { public ulong Value; }
}
namespace FFXIVClientStructs.FFXIV.Client.Game {
    public sealed class Address { public nint Value; }
    public enum ActionType { Action = 1, GeneralAction = 5 }
    public unsafe struct ActionManager {
        public enum UseActionMode { None }
        public bool UseAction(ActionType type, uint id) => true;
        public static class Addresses {
            public static Address Update = new(), UseAction = new(), UseActionLocation = new();
        }
        public static class Delegates {
            public delegate void Update(ActionManager* self);
            public delegate bool UseAction(ActionManager* self, ActionType type, uint id, ulong target, uint extra, UseActionMode mode, uint combo, bool* area);
            public delegate bool UseActionLocation(ActionManager* self, ActionType type, uint id, ulong target, Vector3* location, uint extra, byte a7);
        }
    }
    public struct Status { public ushort StatusId, Param; public float RemainingTime; }
    [InlineArray(60)] public struct StatusArray { private Status first; }
    public struct StatusManager {
        private StatusArray slots;
        public byte NumValidStatuses;
        public Span<Status> Status => MemoryMarshal.CreateSpan(ref slots[0], 60);
        public int GetStatusIndex(uint id) {
            for (var i = 0; i < 60; i++) if (slots[i].StatusId == id) return i;
            return -1;
        }
        public bool SetStatus(int index, ushort id, float remaining, ushort param, GameObjectId source, bool refreshFlags) {
            NativeCalls.Writes.Add((id, remaining, param, source.Value, refreshFlags));
            slots[index] = new Status { StatusId = id, RemainingTime = remaining, Param = param };
            return true;
        }
        public void RemoveStatus(int index) { NativeCalls.Removes.Add(slots[index].StatusId); slots[index] = default; }
    }
    public static class NativeCalls {
        public static readonly List<(ushort Id, float Duration, ushort Param, ulong Source, bool Flags)> Writes = [];
        public static readonly List<ushort> Removes = [];
    }
}
namespace FFXIVClientStructs.FFXIV.Client.Game.Character {
    public struct Character { }
    public struct BattleChara {
        public StatusManager StatusManager;
        public uint Health, MaxHealth;
        public GameObjectId GetGameObjectId() => new() { Value = 42 };
    }
}
namespace AnoMech.Core.Game {
    public sealed class Coordinates { }
    public class Movement {
        public bool IsMoving;
        public void Knockback(Vector3 source, float distance, float speed) => IsMoving = true;
    }
    public sealed class PlayerMovement : Movement { public PlayerMovement(AnoMech.Core.SimObjects.SimCharacter player) { } }
}
namespace AnoMech.Core.Game.Party {
    public enum PartyRole { MainTank }
    public interface ISimPartyMember { }
}
namespace AnoMech.Core.SimObjects {
    public abstract unsafe class SimCharacter {
        protected SimCharacter(AnoMech.Core.Game.Coordinates coordinates) { }
        internal abstract BattleChara* BattleCharaPtr { get; }
        private protected abstract AnoMech.Core.Game.Movement Movement { get; }
        public virtual void Tick(float delta) { }
        public virtual void Despawn() { }
        public void StopMoving() => Movement.IsMoving = false;
        public void AddStatus(ushort id) { }
        public void ResetActionTimeline() { }
        public void PlayActionTimeline(ushort id) { }
    }
    public static class KoExtensions { public static void PlayKoActionTimeline(this SimPlayer player) { } }
}
namespace AnoMech.Core.Native {
    internal static unsafe class Statuses {
        public static void Remove(Character* chara, ushort id) {
            if (chara == null) return;
            var sm = &((BattleChara*)chara)->StatusManager;
            var slot = sm->GetStatusIndex(id);
            if (slot >= 0) sm->RemoveStatus(slot);
        }
    }
}
namespace AnoMech {
    public sealed class Actor { public nint Address; }
    public sealed class Objects { public Actor? LocalPlayer; }
    public sealed class Conditions { public bool this[Dalamud.Game.ClientState.Conditions.ConditionFlag flag] => false; }
    public sealed class GameState { public Core.SimObjects.SimPlayer? Player; }
    public static class Plugin {
        public static Objects ObjectTable = new();
        public static Conditions Condition = new();
        public static GameState? GameInstance;
        public static Core.Native.LocalPlayerInputHooks PlayerInputHooks = null!;
    }
}
