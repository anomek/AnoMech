using System;
using System.Collections.Generic;
using AnoMech.Core;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AnoMech.Offline;

// The game's own menus in the offline world. Settings can't be kept and server features can't
// work, so their commands are greyed out the way the game greys out a command it doesn't allow at
// the moment. Greying only changes the menus, so a command that still arrives (a keybind, a hotbar
// slot) is stopped here and explains itself in the red error text. Log Out and Exit Game, which
// would wait on a server, go through offline mode's own return to the title screen.
internal sealed unsafe class OfflineMenuGuard : IDisposable
{
    private const string SettingsMessage = "These settings cannot be changed while in an AnoMech offline sim.";
    private const string ServerMessage = "This isn't available in an AnoMech offline sim.";
    // MainCommand rows.
    private const uint LogOut = 23, ExitGame = 24;
    private static readonly HashSet<uint> Settings = [19, 20, 21, 22, 34, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 63, 71, 83, 97, 99];
    private static readonly HashSet<uint> ServerOnly = [13, 14, 15, 27, 28, 32, 33, 35, 36, 44, 57, 70, 72, 74, 76, 78, 80, 82, 85, 86, 88, 91, 94, 96];

    private delegate byte ExecuteMainCommandDelegate(UIModule* module, uint command);

    private readonly OfflineSession session;
    private readonly Dictionary<uint, bool> enabledBefore = [];
    private Hook<ExecuteMainCommandDelegate>? execute;
    private bool greying;

    public OfflineMenuGuard(OfflineSession session) => this.session = session;

    public void Arm()
    {
        try
        {
            var ui = UIModule.Instance();
            execute ??= Plugin.GameInterop.HookFromAddress<ExecuteMainCommandDelegate>((nint)ui->VirtualTable->ExecuteMainCommand, ExecuteDetour);
            execute.Enable();
            greying = true;
            Apply();
        }
        catch (Exception e)
        {
            // Only the menus are affected: the file guard, not this, keeps settings from being saved.
            DiagnosticLog.Warn($"[Offline] The game's menus couldn't be adjusted for offline mode: {e}");
        }
    }

    // Every frame while in the world: the game recomputes some commands' states on its own.
    public void Apply()
    {
        var hud = AgentHUD.Instance();
        if (!greying || hud == null) return;
        foreach (var command in Settings) Grey(hud, command);
        foreach (var command in ServerOnly) Grey(hud, command);
    }

    private void Grey(AgentHUD* hud, uint command)
    {
        var enabled = hud->IsMainCommandEnabled(command);
        enabledBefore.TryAdd(command, enabled);
        if (enabled) hud->SetMainCommandEnabledState(command, false);
    }

    public void Restore()
    {
        greying = false;
        var hud = AgentHUD.Instance();
        if (hud != null)
            foreach (var (command, enabled) in enabledBefore)
                if (enabled) hud->SetMainCommandEnabledState(command, true);
        enabledBefore.Clear();
    }

    private byte ExecuteDetour(UIModule* module, uint command)
    {
        if (session.Phase == OfflinePhase.InWorld)
        {
            if (command is LogOut or ExitGame)
            {
                if (session.LeaveBlockedReason() is { } blocked) module->ShowErrorText(blocked, true);
                else session.RequestReturn(exitAfterwards: command == ExitGame);
                return 0;
            }
            var message = Settings.Contains(command) ? SettingsMessage : ServerOnly.Contains(command) ? ServerMessage : null;
            if (message != null)
            {
                module->ShowErrorText(message, true);
                return 0;
            }
        }
        return execute!.Original(module, command);
    }

    public void Dispose() => execute?.Dispose();
}
