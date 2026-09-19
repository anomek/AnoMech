using System.Numerics;
using System.Reflection;
using AnoMech;
using AnoMech.Core.Game;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Input;

unsafe {
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    var native = new BattleChara { Health = 100, MaxHealth = 100 };
    Plugin.ObjectTable.LocalPlayer = new() { Address = (nint)(&native) };
    var interop = new FakeInterop();
    using var hooks = new LocalPlayerInputHooks(interop);
    Plugin.PlayerInputHooks = hooks;
    Plugin.GameInstance = new();
    SimPlayer NewPlayer() {
        Plugin.GameInstance!.Player?.Despawn();
        var player = new SimPlayer(new Coordinates());
        Plugin.GameInstance.Player = player;
        return player;
    }
    var player = NewPlayer();
    player.Tick(1);
    Check(NativeCalls.Writes.Count == 0, "No automatic/permanent Sprint on scenario start");
    interop.Accept = false;
    interop.Execute(ActionType.Action, 3);
    Check(NativeCalls.Writes.Count == 0, "Rejected Sprint does not apply a buff");
    interop.Accept = true;
    interop.Queue = true;
    interop.Press(ActionType.Action, 3);
    Check(NativeCalls.Writes.Count == 0, "Queued Sprint waits for actual execution");
    interop.Queue = false;
    interop.Press(ActionType.GeneralAction, 4);
    Check(native.StatusManager.GetStatusIndex(50) >= 0, "General hotbar Sprint reaches adjusted execution and applies status 50");
    Check(NativeCalls.Writes.Count == 1, "Nested action hooks grant Sprint once");
    Check(NativeCalls.Writes[0] == (50, 10f, 30, 42UL, true), "Native duration, speed parameter, source and flag refresh");
    Check(hooks.PollActionUsed() && !hooks.PollActionUsed(), "Sprint counts as an action for stillness mechanics");
    player.Tick(4);
    Check(Math.Abs(native.StatusManager.Status[native.StatusManager.GetStatusIndex(50)].RemainingTime - 6) < .001f, "Sprint duration decreases");
    var count = NativeCalls.Writes.Count;
    interop.Accept = false; interop.Execute(ActionType.Action, 3); interop.Accept = true;
    Check(NativeCalls.Writes.Count == count, "Cooldown rejection cannot refresh active Sprint");
    // Simulate native removal/pruning between frames. Reapplication must restore flags as well as the icon.
    native.StatusManager.RemoveStatus(native.StatusManager.GetStatusIndex(50));
    player.Tick(1);
    Check(NativeCalls.Writes[^1] == (50, 5f, 30, 42UL, true), "Recreated native slot retains only remaining duration and refreshes flags");
    player.Tick(5);
    Check(native.StatusManager.GetStatusIndex(50) < 0, "Sprint expires after ten seconds");
    count = NativeCalls.Writes.Count; player.Tick(20);
    Check(NativeCalls.Writes.Count == count, "Expired Sprint never reappears");

    player.SetMechanicInputLock(true);
    count = interop.Executions;
    Check(!interop.Execute(ActionType.Action, 3) && interop.Executions == count, "Mechanic stun blocks Sprint before native execution");
    player.SetMechanicInputLock(false);
    interop.Execute(ActionType.Action, 3);
    player.OnKilled();
    Check(native.StatusManager.GetStatusIndex(50) < 0 && !interop.Execute(ActionType.Action, 3), "Death clears Sprint and blocks another use");
    player = NewPlayer();
    interop.Execute(ActionType.Action, 3); player.Tick(2); player.Despawn();
    Check(native.StatusManager.GetStatusIndex(50) < 0 && !hooks.DisableAllActions && !hooks.ZeroMovement, "Reset clears owned Sprint and input locks");
    player = NewPlayer();
    native.StatusManager.SetStatus(0, 50, 7, 30, default, true);
    player.Despawn();
    Check(native.StatusManager.Status[0].StatusId == 50, "Reset does not remove an unowned server Sprint");
    native.StatusManager.RemoveStatus(0);
    Plugin.GameInstance.Player = null;
    count = NativeCalls.Writes.Count;
    interop.Execute(ActionType.Action, 3);
    Check(NativeCalls.Writes.Count == count, "Outside a simulated party, native Sprint is untouched");
    player = NewPlayer();
    interop.Execute(ActionType.Action, 99);
    Check(NativeCalls.Writes.Count == count, "Other actions do not grant Sprint");
    for (var i = 0; i < 60; i++) native.StatusManager.Status[i].StatusId = (ushort)(1000 + i);
    interop.Execute(ActionType.Action, 3); player.Tick(1);
    Check(NativeCalls.Writes.Count == count && native.StatusManager.Status[59].StatusId == 1059,
        "A full status manager never overwrites another buff");
    native.StatusManager.Status[17] = default;
    player.Tick(1);
    Check(native.StatusManager.Status[17].StatusId == 50 && NativeCalls.Writes[^1].Duration == 8,
        "A freed slot receives only the remaining Sprint duration");
    player.Despawn();
    for (var i = 0; i < 60; i++) native.StatusManager.Status[i] = default;
    player = NewPlayer();
    count = NativeCalls.Writes.Count;
    Plugin.ObjectTable.LocalPlayer = null;
    interop.Execute(ActionType.Action, 3); player.Tick(1); player.Despawn();
    Check(NativeCalls.Writes.Count == count, "Missing native player safely skips Sprint");
    Console.WriteLine("PASS: production player/input hooks; executed vs queued/rejected Sprint, general hotbar routing, native flag refresh, finite duration, death/reset ownership and missing player.");
}

unsafe sealed class FakeInterop : IGameInteropProvider {
    public bool Accept = true, Queue;
    public int Executions;
    private ActionManager.Delegates.UseAction press = null!;
    private ActionManager.Delegates.UseActionLocation execute = null!;
    public bool Execute(ActionType type, uint id) => execute(null, type, id, 42, null, 0, 0);
    public bool Press(ActionType type, uint id) => press(null, type, id, 42, 0, ActionManager.UseActionMode.None, 0, null);
    public void InitializeFromAttributes(object target) {
        foreach (var field in target.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            if (field.GetCustomAttributes().Any(a => a.GetType().Name == "SignatureAttribute"))
                field.SetValue(target, Activator.CreateInstance(field.FieldType));
    }
    public Hook<T> HookFromAddress<T>(nint address, T detour) where T : Delegate {
        Delegate original;
        if (detour is ActionManager.Delegates.UseAction p) {
            press = p;
            original = new ActionManager.Delegates.UseAction((self, type, id, target, extra, mode, combo, area) =>
                Queue || Execute(type == ActionType.GeneralAction && id == 4 ? ActionType.Action : type, type == ActionType.GeneralAction && id == 4 ? 3u : id));
        } else if (detour is ActionManager.Delegates.UseActionLocation e) {
            execute = e;
            original = new ActionManager.Delegates.UseActionLocation((self, type, id, target, location, extra, a7) => { Executions++; return Accept; });
        } else if (detour is ActionManager.Delegates.Update) original = new ActionManager.Delegates.Update(self => { });
        else original = new InputData.Delegates.IsInputIdPressed((self, id) => false);
        return new Hook<T> { Original = (T)original };
    }
}
