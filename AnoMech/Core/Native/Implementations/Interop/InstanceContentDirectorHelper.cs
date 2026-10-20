using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using System;

namespace AnoMech.Core.Native.Implementations.Interop;

internal static unsafe class InstanceContentDirectorHelper
{
    // Returns false when the zone/director isn't ready yet (async load still in
    // flight) so MapController can retry instead of silently losing the call.
    public static bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        // Outside a sim this resolves the director of whatever instance the player is really
        // in -- a live duty -- and drives its state machine. Nothing here may run there.
        if (Plugin.Game?.World.Map.IsInInstance != true)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.ProcessDirectorUpdate] no sim in progress -- ignoring.");
            return false;
        }

        var eventFramework = EventFramework.Instance();

        if (eventFramework == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.Commence] EventFramework.Instance() was null");
            return false;
        }

        var director = eventFramework->GetInstanceContentDirector();

        if (director == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.Commence] eventFramework->GetInstanceContentDirector() was null");
            return false;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null) return false;

        PacketDispatcher.HandleActorControlPacket(localPlayer.EntityId, DirectorUpdateCategory, director->GetEventId(),
            category, arg1, arg2, arg3, arg4, arg5, arg6, NoTarget, false);
        return true;
    }

    // ActorControlSelf: p1 director id, p2 variable, p3..p8 its values.
    private const uint DirectorUpdateCategory = 0x6D;
    private const uint NoTarget = 0xE0000000;

    // Same readiness contract as ProcessDirectorUpdate; a line the handler can't show is taken, so
    // nothing retries it.
    public static bool BattleTalk(uint speakerNameId, uint textId, uint durationMs)
    {
        if (Plugin.Game?.World.Map.IsInInstance != true)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.BattleTalk] no sim in progress -- ignoring.");
            return false;
        }

        var eventFramework = EventFramework.Instance();
        var director = eventFramework == null ? null : eventFramework->GetInstanceContentDirector();
        if (director == null) return false;

        if (PacketDispatcherPointers.HandleBattleTalkPacket is not { } handle)
        {
            DiagnosticLog.Warn($"[EventFrameworkHelper.BattleTalk] signature not found -- text {textId} not shown.");
            return true;
        }
        handle(eventFramework, director->GetEventId(), NoTarget, (byte)ObjectKind.BattleNpc, speakerNameId, textId, durationMs, 0, 0, null, 0);
        return true;
    }

    // 0 when no director is loaded.
    public static uint DirectorEventId()
    {
        var eventFramework = EventFramework.Instance();
        var director = eventFramework == null ? null : eventFramework->GetInstanceContentDirector();
        return director == null ? 0 : director->GetEventId();
    }

    public static void SetDirectorData(byte sequence, byte unknown, ReadOnlySpan<byte> unionData)
    {
        var eventFramework = EventFramework.Instance();
        var director = eventFramework == null ? null : eventFramework->GetInstanceContentDirector();

        if (director == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDirectorData] no InstanceContentDirector");
            return;
        }

        const int length = PacketDispatcherPointers.DirectorVarsDataLength;
        var data = stackalloc byte[length];
        var span = new Span<byte>(data, length);
        span.Clear();
        unionData[..Math.Min(unionData.Length, length)].CopyTo(span);

        PacketDispatcherPointers.HandleDirectorVarsPacket(director->GetEventId(), sequence, unknown, data, length);
    }

    public static void SetDutyData(ContentFinderCondition contentFinderCondition)
    {
        var eventFramework = EventFramework.Instance();

        if (eventFramework == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDutyData] EventFramework.Instance() was null");
            return;
        }

        var director = eventFramework->GetInstanceContentDirector();

        if (director == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDutyData] eventFramework->GetInstanceContentDirector() was null");
            return;
        }

        var uiState = UIState.Instance();

        if (uiState == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDutyData] UIState.Instance() was null");
            return;
        }

        var contentTypeRef = contentFinderCondition.ContentType;

        if (contentTypeRef.IsValid)
        {
            director->ContentTypeRowId = (byte)contentFinderCondition.ContentType.RowId;
            director->IconId = contentTypeRef.Value.IconDutyFinder;
        }
        else
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDutyData] contentFinderCondition.ContentType was not valid, skipping director->ContentTypeRowId and director->IconId.");
        }

        using var str = new Utf8String(contentFinderCondition.Name);

        director->Title.SetString(str);
        uiState->DirectorTodo.Title.SetString(str);
        uiState->DirectorTodo.IsShown = true;
    }

    // Fire the three DirectorUpdate events the server sends at instance Commence
    // Note: these IDs/params are TOP-specific — other content fires different values
    // (e.g. T=1045 uses 40000007 / 40000001-E10 / 80000004-E0F). Future work to parameterize.
    public static void Commence()
    {
        ProcessDirectorUpdate(0x4000000C);
        ProcessDirectorUpdate(0x40000001, 0x1C20);
        ProcessDirectorUpdate(0x80000004, 0x1C1F);
    }
}
