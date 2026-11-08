using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AnoMech.Core.Native.Implementations.Pointers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Text.ReadOnly;
using Condition = Lumina.Excel.Sheets.Condition;
using LogMessage = Lumina.Excel.Sheets.LogMessage;
using World = Lumina.Excel.Sheets.World;

namespace AnoMech.Core.Native.Implementations;

// The game side of PartyStatusTracker. The receive filter needs the party-status packets' opcodes
// to pick them out, and opcodes change with every game version, so they are learned from the
// client's own handlers as live packets reach them, and kept per game version. The ones a fix
// can't do without (conditions, online status, ActorControl) are learned through handlers that
// upstream or ClientStructs keep current through patches; the ones that may not arrive before a
// run (Party Finder notice, member events) are also read from the client's code at load.
internal sealed unsafe class ServerPartyWatch : IDisposable
{
    // The packet the receive hook is handing the client right now, and the actor it's for; 0
    // outside a dispatch (ZoneControl sets them around the real dispatcher's call).
    [ThreadStatic] internal static nint DispatchingPacket;
    [ThreadStatic] internal static uint DispatchingActor;

    // InfoProxyCrossRealm's handler for the Party Finder notice packet, which the client reaches only
    // through that packet. It switches on the code at +6; code 0 clears AgentLookingForGroup's own
    // listing, which nothing else does short of a zone change.
    private const string NoticeHandlerSignature =
        "4C 8B DC 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 73 20 48 8B D9 48 8B 49 08 49 89 7B F0 4D 89 73 E8 4C 8B F2 48 8B 01 FF 90 ?? ?? ?? ?? 48 8B C8 BA 82 00 00 00";

    // The zone dispatcher's switch on the opcode (DispatcherSwitch), and its case for the Party
    // Finder notice: it prints the notice's log message unless the code at +6 is 0x27. Only the
    // notice reaches that case.
    private const string DispatcherSwitchSignature =
        "41 83 C5 ?? 49 8B FC 41 81 FD ?? ?? ?? ?? 0F 87 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 49 63 C5 8B 8C 82 ?? ?? ?? ?? 48 03 CA FF E1";
    private const string NoticeCaseSignature =
        "48 8D 4F 10 E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 ?? 80 7F 16 27";

    // How each of the dispatcher's ActorControl cases starts: hand the packet to the replay
    // recorder, then check its category against the replay filter. Matches all three forms.
    private const string ActorControlCaseSignature =
        "4C 8D 47 10 41 8B D6 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? F6 05 ?? ?? ?? ?? 04 74 ?? 0F B7 57 10 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 ?? 0F B7 57 10 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 84";
    private const ulong NoTarget = 0xE0000000;

    // The party handler's branch for the cross-world party's member events (it checks the kind at
    // +1 against 2) and its branch for "member left" (it compares the content id at +0 with the
    // player's), and the two forms of the handler's switch that reach them: a byte-table range and
    // a lone compare (SwitchCode).
    private const string MemberEventCaseSignature = "40 32 FF 38 9E ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 41 0F B6 4E 11 80 F9 02 0F 85";
    private const string MemberLeftCaseSignature =
        "49 39 5E 10 0F 84 ?? ?? ?? ?? 49 8B 07 49 8B CF FF 90 ?? ?? ?? ?? 48 8B 88 ?? ?? ?? ?? 48 85 C9 75 ?? 48 8B 0D";
    private const string ByteTableSwitchSignature =
        "2D ?? ?? ?? ?? 3D ?? ?? ?? ?? 0F 87 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 48 98 0F B6 84 02 ?? ?? ?? ?? 8B 8C 82 ?? ?? ?? ?? 48 03 CA FF E1";
    private const string CompareJumpSignature = "3D ?? ?? ?? ?? 0F 87 ?? ?? ?? ?? 0F 84";

    // The party event packet's handler (PartyEvent); its dispatcher case calls it, which names the
    // opcode (SwitchCode.JumpTableOpcodeCalling).
    private const string PartyEventHandlerSignature =
        "40 53 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B F2 48 89 54 24 ?? E8";

    // A cross-world party's ready check (CrossWorldReadyCheck): the function its start is handed
    // to (it opens AgentReadyCheck, 0x89), and the party handler's branch for an answer (it fetches
    // AgentReadyCheck, then the content id at +0). Both are reached through the handler's
    // byte-table switch.
    private const string CrossWorldReadyCheckSignature =
        "40 53 55 48 83 EC 48 48 89 74 24 60 48 8B F1 48 8B 49 08 48 89 7C 24 68 48 8B FA 48 8B 01 FF 90 ?? ?? ?? ?? 48 8B C8 BA 89 00 00 00";
    private const string CrossWorldReadyCheckAnswerCaseSignature =
        "49 8B 4C 24 08 48 8B 01 FF 90 ?? ?? ?? ?? 48 8B C8 BA 89 00 00 00 E8 ?? ?? ?? ?? 4D 8B 46 10";

    // The client opens a ready check it receives with HeldReadyCheck.Window on this float of
    // AgentReadyCheck, counting down; ClientStructs doesn't name it.
    private const int ReadyCheckTimerOffset = 0x3C;

    // InfoProxyCrossRealm's roster parser: eight member entries into one group (0 for the party,
    // 0-5 for an alliance). A party roster hands it its payload + 8, after the leader's content id;
    // an alliance's groups start at + 0x10. The 0x348 multiply is the group's size, and pins the match.
    private const string RosterParserSignature =
        "44 88 44 24 18 48 89 54 24 10 53 55 41 54 41 55 41 56 41 57 48 83 EC 48 48 8B E9 48 8B DA 48 8B 49 08 48 8B 01 FF 90 ?? ?? ?? ?? 4C 8B A0 ?? ?? ?? ?? 4D 85 E4 75 ?? 48 8B 0D ?? ?? ?? ?? 48 89 88 ?? ?? ?? ?? 4C 8B E1 48 85 C9 4C 0F 44 25 ?? ?? ?? ?? 45 32 ED 0F B6 8C 24 ?? ?? ?? ?? 48 69 C1 48 03 00 00";
    private const int RosterMembersOffset = 8;

    private delegate void SetOnlineStatusFlagsDelegate(InfoModule* info, ulong flags);
    private delegate void InitiateReadyCheckDelegate(AgentReadyCheck* agent);
    // ClientStructs' HandleActorControlPacket; the target is a GameObjectId and the last a bool,
    // passed through as they came.
    private delegate void HandleActorControlDelegate(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, byte isRecorded);
    private delegate void NoticeHandlerDelegate(nint infoProxy, byte* payload);
    private delegate void RosterParserDelegate(nint infoProxy, byte* members, byte group);

    // ZoneControl.InjectIncomingPacket: a server packet through the client's own dispatcher.
    internal delegate bool Inject(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what);

    private enum Packet { PlayerStateFlags, Notice, Roster }

    private readonly ConditionFlag[] networked = Plugin.DataManager.GetExcelSheet<Condition>()
        .Where(row => row.IsNetworked)
        .Select(row => (ConditionFlag)row.RowId)
        .OrderBy(flag => flag)
        .ToArray();

    private readonly string gameVersion = Framework.Instance()->GameVersionString;
    private readonly Hook<PacketDispatcherPointers.HandlePlayerStateFlagsPacketDelegate> stateFlagsHook;
    private readonly Hook<SetOnlineStatusFlagsDelegate>? onlineStatusHook;
    private readonly Hook<HandleActorControlDelegate>? actorControlHook;
    private readonly Hook<InitiateReadyCheckDelegate>? readyCheckHook;
    private readonly Hook<NoticeHandlerDelegate>? noticeHook;
    private readonly Hook<RosterParserDelegate>? rosterHook;

    public PartyStatusTracker Tracker { get; }

    public ServerPartyWatch()
    {
        Tracker = new PartyStatusTracker(networked, LogMessageText, WorldName, IsBlacklisted);
        if (Plugin.Config.PartyOpcodesGameVersion == gameVersion)
        {
            Tracker.PlayerStateFlagsOpcode = Plugin.Config.PlayerStateFlagsOpcode;
            Tracker.OnlineStatusOpcode = Plugin.Config.OnlineStatusOpcode;
            Tracker.NoticeOpcode = Plugin.Config.PartyNoticeOpcode;
            Tracker.RosterOpcodes = Plugin.Config.PartyRosterOpcodes;
            Tracker.ActorControlOpcodes = Plugin.Config.PartyActorControlOpcodes;
            Tracker.PartyEventOpcode = Plugin.Config.PartyEventOpcode;
        }
        var dispatcherSwitch = Plugin.SigScanner.TryScanText(DispatcherSwitchSignature, out var switchCode) ? switchCode : 0;
        if (NoticeOpcodeFromDispatcher(dispatcherSwitch) is var noticeOpcode and not 0)
        {
            if (Tracker.NoticeOpcode is var saved and not 0 && saved != noticeOpcode)
                DiagnosticLog.Warn($"[PartyWatch] The dispatcher sends the Party Finder notice as 0x{noticeOpcode:X4}, not the saved 0x{saved:X4}.");
            Tracker.NoticeOpcode = noticeOpcode;
        }
        else DiagnosticLog.Warn("[PartyWatch] The Party Finder notice's opcode couldn't be read from the dispatcher; until one arrives outside the sim, a listing that ends during a run stays open.");
        var actorControlFromDispatcher = ActorControlOpcodesFromDispatcher(dispatcherSwitch);
        Tracker.ActorControlOpcodes = [.. Tracker.ActorControlOpcodes.Union(actorControlFromDispatcher)];
        if (actorControlFromDispatcher.Length == 0)
            DiagnosticLog.Warn("[PartyWatch] The ActorControl opcodes couldn't be read from the dispatcher; they're learned from live packets instead.");
        var module = Plugin.SigScanner.Module;
        Tracker.MemberEventOpcode = OpcodeReaching(MemberEventCaseSignature, ByteTableSwitchSignature,
            (switchCode, caseStart) => SwitchCode.ByteTableOpcodeOf(switchCode, caseStart, module.BaseAddress, module.ModuleMemorySize));
        Tracker.MemberLeftOpcode = OpcodeReaching(MemberLeftCaseSignature, CompareJumpSignature, SwitchCode.CompareOpcodeOf);
        if (Tracker.MemberEventOpcode == 0 || Tracker.MemberLeftOpcode == 0)
            DiagnosticLog.Warn("[PartyWatch] The party handler's member opcodes couldn't be read; who joins or leaves during a run, and a listing filling, aren't shown.");
        if (PartyEventOpcodeFromDispatcher(dispatcherSwitch) is var partyEventOpcode and not 0)
        {
            if (Tracker.PartyEventOpcode is var savedEvent and not 0 && savedEvent != partyEventOpcode)
                DiagnosticLog.Warn($"[PartyWatch] The dispatcher sends party events as 0x{partyEventOpcode:X4}, not the saved 0x{savedEvent:X4}.");
            Tracker.PartyEventOpcode = partyEventOpcode;
        }
        else if (Tracker.PartyEventOpcode == 0)
            DiagnosticLog.Warn("[PartyWatch] The party event opcode couldn't be read from the dispatcher; until a ready check happens outside the sim, one during a run isn't seen.");
        Tracker.CrossWorldReadyCheckOpcode = CrossWorldReadyCheckOpcodeFromHandler(module.BaseAddress, module.ModuleMemorySize);
        Tracker.CrossWorldReadyCheckAnswerOpcode = OpcodeReaching(CrossWorldReadyCheckAnswerCaseSignature, ByteTableSwitchSignature,
            (switchCode, caseStart) => SwitchCode.ByteTableOpcodeOf(switchCode, caseStart, module.BaseAddress, module.ModuleMemorySize));
        if (Tracker.CrossWorldReadyCheckOpcode == 0 || Tracker.CrossWorldReadyCheckAnswerOpcode == 0)
            DiagnosticLog.Warn("[PartyWatch] The cross-world ready check opcodes couldn't be read; a cross-world party's ready check during a run isn't seen.");
        stateFlagsHook = Plugin.GameInterop.HookFromAddress<PacketDispatcherPointers.HandlePlayerStateFlagsPacketDelegate>(
            Marshal.GetFunctionPointerForDelegate(PacketDispatcherPointers.HandlePlayerStateFlagsPacket), StateFlagsDetour);
        stateFlagsHook.Enable();
        onlineStatusHook = HookOrWarn<SetOnlineStatusFlagsDelegate>((nint)InfoModule.MemberFunctionPointers.SetOnlineStatusFlags, OnlineStatusDetour,
            "InfoModule.SetOnlineStatusFlags isn't resolved; the online status opcode can't be learned and recruiting stays on until the next zone change.");
        actorControlHook = HookOrWarn<HandleActorControlDelegate>((nint)PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket, ActorControlDetour,
            "PacketDispatcher.HandleActorControlPacket isn't resolved; ActorControl opcodes come only from the dispatcher.");
        readyCheckHook = HookOrWarn<InitiateReadyCheckDelegate>((nint)AgentReadyCheck.MemberFunctionPointers.InitiateReadyCheck, ReadyCheckDetour,
            "AgentReadyCheck.InitiateReadyCheck isn't resolved; the party event opcode comes only from the dispatcher.");
        if (Plugin.SigScanner.TryScanText(NoticeHandlerSignature, out var noticeHandler))
        {
            noticeHook = Plugin.GameInterop.HookFromAddress<NoticeHandlerDelegate>(noticeHandler, NoticeDetour);
            noticeHook.Enable();
        }
        else DiagnosticLog.Warn("[PartyWatch] The Party Finder notice handler was not found; the Party Finder's own listing is left to the next zone change.");
        if (Plugin.SigScanner.TryScanText(RosterParserSignature, out var rosterParser))
        {
            rosterHook = Plugin.GameInterop.HookFromAddress<RosterParserDelegate>(rosterParser, RosterDetour);
            rosterHook.Enable();
        }
        else DiagnosticLog.Warn("[PartyWatch] The cross-world roster parser was not found; who joins and leaves isn't shown.");
        DiagnosticLog.Info($"[PartyWatch] Opcodes for {gameVersion}: PlayerStateFlags 0x{Tracker.PlayerStateFlagsOpcode:X4}, online status 0x{Tracker.OnlineStatusOpcode:X4}, Party Finder notice 0x{Tracker.NoticeOpcode:X4}, roster {Opcodes(Tracker.RosterOpcodes)}, ActorControl {Opcodes(Tracker.ActorControlOpcodes)}, member event 0x{Tracker.MemberEventOpcode:X4}, member left 0x{Tracker.MemberLeftOpcode:X4}, party event 0x{Tracker.PartyEventOpcode:X4}, cross-world ready check 0x{Tracker.CrossWorldReadyCheckOpcode:X4}/0x{Tracker.CrossWorldReadyCheckAnswerOpcode:X4} (0 or empty = not known).");
    }

    private static string Opcodes(IEnumerable<ushort> opcodes) => $"[{string.Join(",", opcodes.Select(o => $"0x{o:X4}"))}]";

    private static ushort OpcodeOf(nint dispatcherSwitch, nint caseStart)
    {
        var module = Plugin.SigScanner.Module;
        return SwitchCode.JumpTableOpcodeOf(dispatcherSwitch, caseStart, module.BaseAddress, module.ModuleMemorySize);
    }

    // The one opcode any of the switches at `switchSignature` sends to the case at `caseSignature`.
    private static ushort OpcodeReaching(string caseSignature, string switchSignature, Func<nint, nint, ushort> decode)
    {
        try
        {
            if (!Plugin.SigScanner.TryScanText(caseSignature, out var caseStart)) return 0;
            var opcodes = Plugin.SigScanner.ScanAllText(switchSignature)
                .Select(switchCode => decode(switchCode, caseStart))
                .Where(opcode => opcode != 0)
                .Distinct()
                .ToArray();
            return opcodes.Length == 1 ? opcodes[0] : (ushort)0;
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Reading a party opcode from the handler threw: {e.Message}");
            return 0;
        }
    }

    // As the client checks a joiner before printing their name.
    private static bool IsBlacklisted(ulong accountId, ulong contentId)
    {
        try
        {
            var blacklist = InfoProxyBlacklist.Instance();
            return blacklist != null && blacklist->GetBlockResultType(accountId, contentId) != InfoProxyBlacklist.BlockResultType.NotBlocked;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ushort CrossWorldReadyCheckOpcodeFromHandler(nint moduleBase, long moduleSize)
    {
        try
        {
            if (!Plugin.SigScanner.TryScanText(CrossWorldReadyCheckSignature, out var start)) return 0;
            var opcodes = Plugin.SigScanner.ScanAllText(ByteTableSwitchSignature)
                .Select(switchCode => SwitchCode.ByteTableOpcodeCalling(switchCode, start, moduleBase, moduleSize))
                .Where(opcode => opcode != 0)
                .Distinct()
                .ToArray();
            return opcodes.Length == 1 ? opcodes[0] : (ushort)0;
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Reading the cross-world ready check opcode threw: {e.Message}");
            return 0;
        }
    }

    private static ushort PartyEventOpcodeFromDispatcher(nint dispatcherSwitch)
    {
        if (dispatcherSwitch == 0 || !Plugin.SigScanner.TryScanText(PartyEventHandlerSignature, out var handler)) return 0;
        var module = Plugin.SigScanner.Module;
        return SwitchCode.JumpTableOpcodeCalling(dispatcherSwitch, handler, module.BaseAddress, module.ModuleMemorySize);
    }

    private static ushort NoticeOpcodeFromDispatcher(nint dispatcherSwitch)
        => dispatcherSwitch != 0 && Plugin.SigScanner.TryScanText(NoticeCaseSignature, out var noticeCase)
            ? OpcodeOf(dispatcherSwitch, noticeCase)
            : (ushort)0;

    private static ushort[] ActorControlOpcodesFromDispatcher(nint dispatcherSwitch)
    {
        if (dispatcherSwitch == 0) return [];
        try
        {
            return Plugin.SigScanner.ScanAllText(ActorControlCaseSignature)
                .Select(actorControlCase => OpcodeOf(dispatcherSwitch, actorControlCase))
                .Where(opcode => opcode != 0)
                .Distinct()
                .ToArray();
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Scanning for the ActorControl cases threw: {e.Message}");
            return [];
        }
    }

    private static Hook<T>? HookOrWarn<T>(nint address, T detour, string warning) where T : Delegate
    {
        if (address == 0)
        {
            DiagnosticLog.Warn($"[PartyWatch] {warning}");
            return null;
        }
        var hook = Plugin.GameInterop.HookFromAddress(address, detour);
        hook.Enable();
        return hook;
    }

    // Only messages that read whole on their own: one that names someone needs the client to fill it in.
    private static string? LogMessageText(uint id)
    {
        if (!Plugin.DataManager.GetExcelSheet<LogMessage>().TryGetRow(id, out var row)) return null;
        foreach (var payload in row.Text.AsSpan())
            if (payload.Type != ReadOnlySePayloadType.Text) return null;
        return row.Text.ExtractText();
    }

    // Only a world a player can be from.
    private static string? WorldName(ushort id)
        => Plugin.DataManager.GetExcelSheet<World>().TryGetRow(id, out var row) && row.IsPublic ? row.Name.ExtractText() : null;

    // At arm, before the sim takes the conditions over.
    public void Begin()
    {
        var info = InfoModule.Instance();
        var status = PartyOnlineStatus.FromFlags(info != null ? info->OnlineStatusFlags : 0);
        var entityId = Plugin.ObjectTable.LocalPlayer?.EntityId ?? 0;
        Tracker.Begin(ClientRoster(), PartyConditions.Read(flag => Plugin.Condition[flag]), status, Plugin.PlayerState.ContentId, entityId);
    }

    // Null when it can't be read (ClientStructs throws for a function a patch moved), so entering
    // the sim never depends on it.
    private static PartyRoster? ClientRoster()
    {
        try
        {
            if (!InfoProxyCrossRealm.IsCrossRealmParty()) return PartyRoster.None;
            var members = new List<PartyMember>();
            ulong leader = 0;
            for (var i = 0u; i < InfoProxyCrossRealm.GetGroupMemberCount(0); i++)
            {
                var member = InfoProxyCrossRealm.GetGroupMember(i, 0);
                if (member == null || member->ContentId == 0) continue;
                // The roster entry's account id, which the parser copies to +0x08; ClientStructs leaves it unnamed.
                var accountId = *(ulong*)((byte*)member + 0x08);
                members.Add(new PartyMember(member->ContentId, member->NameString, (ushort)member->HomeWorld, accountId));
                if (member->IsPartyLeader) leader = member->ContentId;
            }
            return new PartyRoster(members, leader);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] The cross-world party couldn't be read at arm: {e.Message}");
            return null;
        }
    }

    // After the lift: what the server said about the party during the stay, put on the client
    // through the handlers its packets go to; the notices themselves are replayed. Everything else
    // stays as the client has it. Null when nothing changed. Never throws, so the lift's own party
    // resync still runs.
    public string? ApplyServerParty(Inject inject)
    {
        try
        {
            return ApplyServerPartyOrThrow(inject);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Putting the server's party state on the client threw: {e.Message}");
            return null;
        }
    }

    private string? ApplyServerPartyOrThrow(Inject inject)
    {
        var (conditions, status, statusIcon, notices, readyCheck, held) = Tracker.TakeFromServer(DateTime.Now);
        if (held.Count > 0) DiagnosticLog.Info($"[PartyWatch] The stay held {string.Join(", ", held.Select(h => $"0x{h.Key:X4}x{h.Value}"))}.");
        if (Tracker.RecruitmentEnded && statusIcon is null)
            DiagnosticLog.Warn("[PartyWatch] Recruiting ended during the run but no status icon came with it; if the game still says you're looking for a party, the icon's ActorControl (category 504) or its opcode has changed.");
        var applied = new List<string>();
        if (conditions is { } serverConditions && serverConditions != PartyConditions.Read(flag => Plugin.Condition[flag]))
        {
            var bits = stackalloc byte[PacketDispatcherPointers.PlayerStateFlagsLength];
            serverConditions.WriteStateFlags(new Span<byte>(bits, PacketDispatcherPointers.PlayerStateFlagsLength), networked, flag => Plugin.Condition[flag]);
            PacketDispatcherPointers.HandlePlayerStateFlagsPacket(Conditions.Instance(), bits);
            applied.Add($"conditions {serverConditions}");
        }
        var info = InfoModule.Instance();
        if (status is { } serverStatus && info != null && serverStatus.MergeInto(info->OnlineStatusFlags) is var flags && flags != info->OnlineStatusFlags)
        {
            var before = info->OnlineStatusFlags;
            info->SetOnlineStatusFlags(flags);
            applied.Add($"online status 0x{before:X} -> 0x{flags:X}");
        }
        if (statusIcon is { } icon)
        {
            PacketDispatcher.HandleActorControlPacket(icon.EntityId, icon.Category, icon.P1, icon.P2, icon.P3, icon.P4, 0, 0, 0, 0, NoTarget, false);
            applied.Add($"status icon {icon.P1}");
        }
        var replayed = notices.Count(notice => inject(notice.TargetId, Tracker.NoticeOpcode, notice.Payload, "held Party Finder notice"));
        if (notices.Count > 0)
            applied.Add($"{replayed} of {notices.Count} Party Finder notices replayed ({string.Join(", ", notices.Select(n => $"code {n.Code} message {n.LogMessage}"))})");
        if (Tracker.NoticesDropped > 0) applied.Add($"{Tracker.NoticesDropped} notices past the cap dropped");
        if (readyCheck is { } check)
        {
            var left = check.LeftAt(DateTime.Now);
            var replayedCheck = check.Packets.Count(packet => inject(packet.TargetId, packet.Opcode, packet.Payload, "held ready check"));
            applied.Add($"ready check from {check.From} replayed ({replayedCheck} of {check.Packets.Count} packets), {SetReadyCheckTimer(left)}");
        }
        return applied.Count == 0 ? null : $"the server's party state: {string.Join("; ", applied)}";
    }

    // The replayed check started that long ago, so it gets only what's left. Left alone unless the
    // timer reads what the client just opened it with: a patch may have moved it, or the client may
    // not have opened the window.
    private static string SetReadyCheckTimer(TimeSpan left)
    {
        var agent = AgentReadyCheck.Instance();
        if (agent == null) return "no ready check agent";
        var timer = (float*)((byte*)agent + ReadyCheckTimerOffset);
        if (*timer != (float)HeldReadyCheck.Window.TotalSeconds)
        {
            DiagnosticLog.Warn($"[PartyWatch] The ready check's timer read {*timer} after the replay, not {HeldReadyCheck.Window.TotalSeconds}; left as is.");
            return "timer left as is";
        }
        *timer = (float)left.TotalSeconds;
        return $"{left.TotalSeconds:F0}s left";
    }

    private void StateFlagsDetour(Conditions* conditions, byte* bits)
    {
        Learn((nint)bits, Packet.PlayerStateFlags);
        stateFlagsHook.Original(conditions, bits);
    }

    private void OnlineStatusDetour(InfoModule* info, ulong flags)
    {
        if (DispatchingPacket != 0 && Tracker.LearnOnlineStatus(DispatchingPacket, flags))
            Learned($"The online status arrives as opcode 0x{Tracker.OnlineStatusOpcode:X4}.");
        onlineStatusHook!.Original(info, flags);
    }

    // Called for every ActorControl, the plugin's own included; those come outside a dispatch.
    private void ActorControlDetour(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, byte isRecorded)
    {
        if (DispatchingPacket != 0 && Tracker.LearnActorControl(DispatchingPacket, DispatchingActor, entityId, category, arg1))
            Learned($"An ActorControl arrives as opcode 0x{*(ushort*)(DispatchingPacket + 2):X4}.");
        actorControlHook!.Original(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);
    }

    // The client starting a ready check it received; the plugin's own starts come outside a dispatch.
    private void ReadyCheckDetour(AgentReadyCheck* agent)
    {
        if (DispatchingPacket != 0 && Tracker.LearnPartyEvent(DispatchingPacket))
            Learned($"Party events arrive as opcode 0x{Tracker.PartyEventOpcode:X4}.");
        readyCheckHook!.Original(agent);
    }

    private void Learned(string what)
    {
        DiagnosticLog.Info($"[PartyWatch] {what}");
        Plugin.Framework.Run(Save);
    }

    private void NoticeDetour(nint infoProxy, byte* payload)
    {
        Learn((nint)payload, Packet.Notice);
        noticeHook!.Original(infoProxy, payload);
    }

    // An alliance's group 0 is 8 bytes further in, so its header check reads the wrong bytes.
    private void RosterDetour(nint infoProxy, byte* members, byte group)
    {
        if (group == 0) Learn((nint)(members - RosterMembersOffset), Packet.Roster);
        rosterHook!.Original(infoProxy, members, group);
    }

    private void Learn(nint payload, Packet packet)
    {
        try
        {
            GetCurrentThreadStackLimits(out var stackLow, out var stackHigh);
            if (!PartyStatusTracker.TryReadLiveOpcode(payload, stackLow, stackHigh, out var opcode)) return;
            if (packet == Packet.Roster)
            {
                if (Array.IndexOf(Tracker.RosterOpcodes, opcode) >= 0) return;
                Tracker.RosterOpcodes = [.. Tracker.RosterOpcodes, opcode];
                DiagnosticLog.Info($"[PartyWatch] A cross-world roster arrives as opcode 0x{opcode:X4}.");
                Plugin.Framework.Run(Save);
                return;
            }
            var known = packet == Packet.PlayerStateFlags ? Tracker.PlayerStateFlagsOpcode : Tracker.NoticeOpcode;
            if (opcode == known) return;
            if (packet == Packet.PlayerStateFlags) Tracker.PlayerStateFlagsOpcode = opcode;
            else Tracker.NoticeOpcode = opcode;
            DiagnosticLog.Info($"[PartyWatch] {packet} arrives as opcode 0x{opcode:X4} (was 0x{known:X4}).");
            Plugin.Framework.Run(Save);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Reading a live {packet} opcode threw: {e.Message}");
        }
    }

    private void Save()
    {
        Plugin.Config.PartyOpcodesGameVersion = gameVersion;
        Plugin.Config.PlayerStateFlagsOpcode = Tracker.PlayerStateFlagsOpcode;
        Plugin.Config.OnlineStatusOpcode = Tracker.OnlineStatusOpcode;
        Plugin.Config.PartyNoticeOpcode = Tracker.NoticeOpcode;
        Plugin.Config.PartyRosterOpcodes = Tracker.RosterOpcodes;
        Plugin.Config.PartyActorControlOpcodes = Tracker.ActorControlOpcodes;
        Plugin.Config.PartyEventOpcode = Tracker.PartyEventOpcode;
        Plugin.Config.Save();
    }

    [DllImport("kernel32.dll")]
    private static extern void GetCurrentThreadStackLimits(out nuint lowLimit, out nuint highLimit);

    public void Dispose()
    {
        stateFlagsHook.Dispose();
        onlineStatusHook?.Dispose();
        actorControlHook?.Dispose();
        readyCheckHook?.Dispose();
        noticeHook?.Dispose();
        rosterHook?.Dispose();
    }
}
