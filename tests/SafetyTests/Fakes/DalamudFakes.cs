// Stand-ins for the Dalamud types the included plugin sources use. Shapes match the real API;
// behaviour matches Dalamud 15 where the firewall depends on it, and Contract/ checks both against
// the Dalamud the plugin builds with.
using System;
using AnoMech.SafetyTests.Harness;

namespace Dalamud.Hooking
{
    // Dalamud's two hook backends (Hook<T>.CreateBackend: SafetyHook when DALAMUD_USE_SAFETYHOOK
    // is set, Reloaded otherwise) share everything Contract/HookContractTests runs them through:
    // IsEnabled is false once disposed, Enable throws once disposed, Disable and Dispose are no-ops
    // once disposed, Original throws once disposed and is callable whether or not the hook is
    // enabled, and disposing restores the function. They differ on a failed native patch:
    // SafetyHook throws InvalidOperationException from Enable/Disable and leaves the hook as it
    // was. A Reloaded hook cannot fail once created; the fake's refusal there is the worse, silent
    // one.
    public class Hook<T> : IDisposable, IFakeHook where T : Delegate
    {
        private readonly T original;
        private readonly IHookHost host;
        private bool enabled;

        internal Hook(nint address, T detour, T original, HookRole role, IHookHost host)
        {
            Address = address;
            Detour = detour;
            this.original = original;
            Role = role;
            this.host = host;
        }

        public HookRole Role { get; }
        internal T Detour { get; }
        public nint Address { get; }
        public bool IsDisposed { get; private set; }
        public bool IsEnabled => !IsDisposed && enabled;

        // What a call to the hooked function runs, as the game calls it.
        internal T Target => IsEnabled ? Detour : original;

        public T Original
        {
            get
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return original;
            }
        }

        public void Enable()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (enabled) return;
            if (host.Refuses(Role, enabling: true))
            {
                Refused("enable");
                return;
            }
            enabled = true;
            host.OnHookStateChanged(Role, true, "Enable");
        }

        public void Disable()
        {
            if (IsDisposed || !enabled) return;
            if (host.Refuses(Role, enabling: false))
            {
                Refused("disable");
                return;
            }
            enabled = false;
            host.OnHookStateChanged(Role, false, "Disable");
        }

        private void Refused(string what)
        {
            if (host.Backend == HookBackend.SafetyHook)
                throw new InvalidOperationException($"Could not {what} safetyhook hook at 0x{Address:X}: injected native failure");
        }

        // Both backends take the function back on dispose, whatever a refused Disable left.
        public void Dispose()
        {
            if (IsDisposed) return;
            if (enabled)
            {
                enabled = false;
                host.OnHookStateChanged(Role, false, "Disable");
            }
            IsDisposed = true;
            host.OnHookDisposed(Role);
        }

        // Something outside the plugin took the hook down (Dalamud's unload sweep, a failed
        // re-hook). Not reachable from plugin code.
        public void ForceDisable(string by)
        {
            if (IsDisposed || !enabled) return;
            enabled = false;
            host.OnHookStateChanged(Role, false, by);
        }

        public void ForceDispose(string by)
        {
            if (IsDisposed) return;
            ForceDisable(by);
            IsDisposed = true;
            host.OnHookDisposed(Role);
        }
    }
}

namespace Dalamud.Game.ClientState.Conditions
{
    public enum ConditionFlag
    {
        None = 0, NormalConditions = 1, Unconscious = 2, Emoting = 3, Mounted = 4, Crafting = 5, Gathering = 6,
        MeldingMateria = 7, OperatingSiegeMachine = 8, CarryingObject = 9, RidingPillion = 10, Mounted2 = 10,
        InThatPosition = 11, ChocoboRacing = 12, PlayingMiniGame = 13, PlayingLordOfVerminion = 14,
        ParticipatingInCustomMatch = 15, Performing = 16, Occupied = 25, InCombat = 26, Casting = 27,
        SufferingStatusAffliction = 28, SufferingStatusAffliction2 = 29, Occupied30 = 30, OccupiedInEvent = 31,
        OccupiedInQuestEvent = 32, Occupied33 = 33, BoundByDuty = 34, OccupiedInCutSceneEvent = 35,
        InDuelingArea = 36, TradeOpen = 37, Occupied38 = 38, Occupied39 = 39, ExecutingCraftingAction = 40,
        PreparingToCraft = 41, ExecutingGatheringAction = 42, Fishing = 43, BetweenAreas = 45, Stealthed = 46,
        Jumping = 48, UsingChocoboTaxi = 49, OccupiedSummoningBell = 50, BetweenAreas51 = 51, SystemError = 52,
        LoggingOut = 53, ConditionLocation = 54, WaitingForDuty = 55, BoundByDuty56 = 56,
        MountOrOrnamentTransition = 57, WatchingCutscene = 58, WaitingForDutyFinder = 59, CreatingCharacter = 60,
        Jumping61 = 61, PvPDisplayActive = 62, SufferingStatusAffliction63 = 63, Mounting = 64, CarryingItem = 65,
        UsingPartyFinder = 66, UsingHousingFunctions = 67, Transformed = 68, OnFreeTrial = 69, BeingMoved = 70,
        Mounting71 = 71, SufferingStatusAffliction72 = 72, SufferingStatusAffliction73 = 73,
        RegisteringForRaceOrMatch = 74, WaitingForRaceOrMatch = 75, WaitingForTripleTriadMatch = 76, InFlight = 77,
        WatchingCutscene78 = 78, InDeepDungeon = 79, Swimming = 80, Diving = 81, RegisteringForTripleTriadMatch = 82,
        WaitingForTripleTriadMatch83 = 83, ParticipatingInCrossWorldPartyOrAlliance = 84, Unknown85 = 85,
        DutyRecorderPlayback = 86, Casting87 = 87, MountImmobile = 88, InThisState88 = 88, InThisState89 = 89,
        RolePlaying = 90, InDutyQueue = 91, ReadyingVisitOtherWorld = 92, WaitingToVisitOtherWorld = 93,
        UsingFashionAccessory = 94, BoundByDuty95 = 95, Unknown96 = 96, Disguised = 97, RecruitingWorldOnly = 98,
        Unknown99 = 99, EditingPortrait = 100, Unknown101 = 101, PilotingMech = 102, EditingStrategyBoard = 104,
    }
}

namespace Dalamud.Game.Addon.Lifecycle
{
    using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

    public enum AddonEvent { PreSetup = 0, PostSetup = 1, PostDraw = 5, PreFinalize = 6 }
}

namespace Dalamud.Plugin.Services
{
    using Dalamud.Game.Addon.Lifecycle;
    using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

    public interface IAddonLifecycle
    {
        public delegate void AddonEventDelegate(AddonEvent type, AddonArgs args);
    }
}

namespace Dalamud.Game.Addon.Lifecycle.AddonArgTypes
{
    public class AddonArgs
    {
        public string AddonName { get; init; } = "";
    }
}
