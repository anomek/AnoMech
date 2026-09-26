using System;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using CSPlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace AnoMech.Offline;

internal static unsafe class OfflineProbe
{
    public static string Describe(string where)
    {
        try
        {
            return Build(where);
        }
        catch (Exception e)
        {
            return $"[Offline] {where}: probe failed ({e.GetType().Name}: {e.Message})";
        }
    }

    // NetworkModule.ZoneClient and ChatClient; the installed ClientStructs doesn't expose them.
    public static nint ZoneClient() => ClientSlot(0xA70);

    public static nint ChatClient() => ClientSlot(0xA78);

    private static nint ClientSlot(int offset)
    {
        var framework = Framework.Instance();
        var proxy = framework != null ? framework->NetworkModuleProxy : null;
        var module = proxy != null ? proxy->NetworkModule : null;
        return module == null ? 0 : *(nint*)((byte*)module + offset);
    }

    private static string Build(string where)
    {
        var text = new StringBuilder($"[Offline] {where}:");

        var game = GameMain.Instance();
        if (game != null)
            text.Append($" territory current={game->CurrentTerritoryTypeId} next={game->NextTerritoryTypeId} transition={game->TransitionTerritoryTypeId} "
                        + $"loadState={game->TerritoryLoadState} transitionState={game->TerritoryTransitionState} connected={game->ConnectedToZone} map={game->CurrentMapId};");

        text.Append($" zoneClient=0x{ZoneClient():X} chatClient=0x{ChatClient():X};");

        var control = Control.Instance();
        if (control != null)
            text.Append($" localEntity=0x{control->LocalPlayerEntityId:X} localPlayer=0x{(nint)control->LocalPlayer:X};");

        var state = CSPlayerState.Instance();
        if (state != null)
            text.Append($" playerState loaded={state->IsLoaded} entity=0x{state->EntityId:X} job={state->CurrentClassJobId} level={state->CurrentLevel};");

        var characters = CharacterManager.Instance();
        if (characters != null)
        {
            var slot0 = characters->BattleCharas[0].Value;
            text.Append(slot0 == null
                ? " slot0=none;"
                : $" slot0=0x{slot0->EntityId:X} kind={slot0->ObjectKind} drawn={slot0->DrawObject != null} pos=({slot0->Position.X:F1},{slot0->Position.Y:F1},{slot0->Position.Z:F1});");
        }

        var ui = UIModule.Instance();
        var atk = ui != null ? ui->GetRaptureAtkModule() : null;
        if (atk != null) text.Append($" uiScene={atk->UIScene};");

        var addons = RaptureAtkUnitManager.Instance();
        if (addons != null)
        {
            var fade = addons->GetAddonByName("FadeMiddle");
            text.Append($" fade={(fade == null ? "none" : fade->IsVisible ? "shown" : "hidden")};");
        }

        var lobby = AgentLobby.Instance();
        if (lobby != null)
            text.Append($" lobby loggedIn={lobby->IsLoggedIn} intoZone={lobby->IsLoggedIntoZone} updateStage={lobby->LobbyUpdateStage} uiStage={lobby->LobbyUIStage};");

        var cameras = CameraManager.Instance();
        if (cameras != null) text.Append($" camera={cameras->ActiveCameraIndex};");

        var conditions = string.Join(",", Enum.GetValues<ConditionFlag>().Where(f => (int)f != 0 && Plugin.Condition[f]).Select(f => f.ToString()).Distinct());
        text.Append($" conditions=[{conditions}];");
        text.Append($" dalamud loggedIn={Plugin.ClientState.IsLoggedIn} territory={Plugin.ClientState.TerritoryType} localPlayer={Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? "none"}");
        return text.ToString();
    }
}
