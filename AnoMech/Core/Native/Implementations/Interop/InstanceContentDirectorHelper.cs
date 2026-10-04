using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace AnoMech.Core.Native.Implementations.Interop;

internal static unsafe class InstanceContentDirectorHelper
{
    // Returns false when the zone/director isn't ready yet (async load still in
    // flight) so MapController can retry instead of silently losing the call.
    public static bool ProcessDirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        // Outside a sim this resolves the director of whatever instance the player is really
        // in -- a live duty -- and drives its state machine. Nothing here may run there.
        if (Plugin.GameInstance?.World.Map.IsInInstance != true)
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

        var eventId = director->GetEventId();
        eventFramework->ProcessDirectorUpdate(eventId, category, arg1, arg2, arg3, arg4, arg5, arg6);
        return true;
    }

    // A boss line (InstanceContentTextData) in the BattleTalk box, voiced, as the duty's own
    // director shows it. Same readiness contract as ProcessDirectorUpdate.
    public static bool BattleTalk(uint speakerNameId, uint textId, uint durationMs)
    {
        if (Plugin.GameInstance?.World.Map.IsInInstance != true)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.BattleTalk] no sim in progress -- ignoring.");
            return false;
        }

        var eventFramework = EventFramework.Instance();
        if (eventFramework == null) return false;
        var director = eventFramework->GetInstanceContentDirector();
        if (director == null) return false;

        if (EventFrameworkPointers.ProcessBattleTalk is not { } processBattleTalk)
        {
            Plugin.Log.Warning($"[EventFrameworkHelper.BattleTalk] signature not found -- text {textId} not shown.");
            return true;
        }
        const byte battleNpcKind = 2;
        processBattleTalk(eventFramework, director->GetEventId(), 0xE0000000, battleNpcKind, speakerNameId, textId, durationMs, 0, 0, null, 0);
        return true;
    }

    public static void SetDirectorData(byte sequence, byte unknown, byte* unionData, ulong length = 12)
    {
        var eventFramework = EventFramework.Instance();

        if (eventFramework == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDirectorData (ptr)] EventFramework.Instance() was null");
            return;
        }

        var director = eventFramework->GetInstanceContentDirector();

        if (director == null)
        {
            Plugin.Log.Debug("[EventFrameworkHelper.SetDirectorData (ptr)] eventFramework->GetInstanceContentDirector() was null");
            return;
        }

        var eventId = director->GetEventId();
        EventFrameworkPointers.SetDirectorData(eventFramework, eventId, sequence, unknown, unionData, length);
    }

    public static void SetDirectorData(byte sequence, byte unk, byte[] unionData, bool fillExtraData = true)
    {
        if (unionData.Length < 12 && fillExtraData)
        {
            var extraBytes = 12 - unionData.Length;
            Plugin.Log.Debug($"[EventFrameworkHelper.SetDirectorData (array)] Adding {extraBytes} to unionData.");
            Array.Resize(ref unionData, 12);
        }
        else if (unionData.Length > 12)
        {
            var extraBytes = unionData.Length - 12;
            Plugin.Log.Debug($"[EventFrameworkHelper.SetDirectorData (array)] Removing {extraBytes} from unionData, as the maximum sane size is 12.");
            unionData = unionData.Take(12).ToArray();
        }

        fixed (byte* unionDataPtr = unionData)
        {
            SetDirectorData(sequence, unk, unionDataPtr, (ulong)unionData.Length);
        }
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
