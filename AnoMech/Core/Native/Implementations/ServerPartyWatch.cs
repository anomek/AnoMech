using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using ThreadingTask = System.Threading.Tasks.Task;
using Condition = Lumina.Excel.Sheets.Condition;
using LogMessage = Lumina.Excel.Sheets.LogMessage;
using World = Lumina.Excel.Sheets.World;

namespace AnoMech.Core.Native.Implementations;

// The game side of PartyStatusTracker. The receive filter needs the party packets' opcodes to pick
// them out, and opcodes change with every game version. They are read at load from the client's
// protocol receiver (ProtocolReceiver), and checked against what the client's own handlers do with
// live packets outside the sim: each handler, found through ClientStructs or a signature, names the
// packet it ran for. What the handlers show is kept per game version, and outranks the receiver except
// on which ready check start a packet was (PartyOpcodeSources). Built on the framework thread, where the
// client runs these handlers: a hook backend may run a detour before it hands back the hook the detour
// calls through.
internal sealed unsafe class ServerPartyWatch : IDisposable
{
    // The packet the client's handlers are running for right now, and the actor it's for; 0 outside
    // a dispatch. ZoneControl's receive hook sets them for each live packet it hands the client.
    [ThreadStatic] internal static nint DispatchingPacket;
    [ThreadStatic] internal static uint DispatchingActor;
    // ZoneControl is handing the client one of the plugin's own replays, which names nothing.
    [ThreadStatic] internal static bool Replaying;

    // InfoProxyCrossRealm's handler for the Party Finder notice packet, which the client reaches only
    // through that packet: it fetches AgentLookingForGroup first.
    private static readonly string NoticeHandlerSignature =
        "4C 8B DC 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 89 73 20 48 8B D9 48 8B 49 08 49 89 7B F0 4D 89 73 E8 4C 8B F2 48 8B 01 FF 90 ?? ?? ?? ?? 48 8B C8 BA "
        + string.Join(" ", BitConverter.GetBytes((uint)AgentId.LookingForGroup).Select(b => b.ToString("X2")));

    private const ulong NoTarget = 0xE0000000;

    // The client opens a ready check it receives with HeldReadyCheck.Window on this float of
    // AgentReadyCheck, counting down; ClientStructs doesn't name it.
    private const int ReadyCheckTimerOffset = 0x3C;

    // InfoProxyCrossRealm's roster parser: eight member entries into one group (0 for the party,
    // 0-5 for an alliance). A party roster hands it its payload + 8, after the leader's content id;
    // an alliance's groups start further in. The 0x348 multiply is the group's size, and pins the match.
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

    private readonly ConditionFlag[] networked = Plugin.DataManager.GetExcelSheet<Condition>()
        .Where(row => row.IsNetworked)
        .Select(row => (ConditionFlag)row.RowId)
        .OrderBy(flag => flag)
        .ToArray();

    private readonly string gameVersion = Framework.Instance()->GameVersionString;
    private readonly Hook<PacketDispatcherPointers.HandlePlayerStateFlagsPacketDelegate>? stateFlagsHook;
    private readonly Hook<SetOnlineStatusFlagsDelegate>? onlineStatusHook;
    private readonly Hook<HandleActorControlDelegate>? actorControlHook;
    private readonly Hook<InitiateReadyCheckDelegate>? readyCheckHook;
    private readonly Hook<NoticeHandlerDelegate>? noticeHook;
    private readonly Hook<RosterParserDelegate>? rosterHook;

    // Guards the tracker's opcodes against the receiver's read and the handlers at once.
    private readonly object opcodes = new();
    private readonly PartyOpcodeSources sources;
    private readonly HashSet<PartyPacket> confirmed = [];
    // Ready check opcodes the receiver's table filed under the other start; logged once each.
    private readonly HashSet<ushort> moved = [];
    private ProtocolReceiver? receiver;
    private bool heardThrew;
    private readonly HeldReadyCheckReplay heldReadyCheck = new();

    public PartyStatusTracker Tracker { get; }

    // The notice handler and the roster parser, 0 where not found. A scan takes tens of milliseconds, so
    // this runs before the watch is built on the framework thread.
    public static (nint Notice, nint Roster) FindHandlers()
    {
        if (!Plugin.SigScanner.TryScanText(NoticeHandlerSignature, out var notice))
            DiagnosticLog.Warn("[PartyWatch] The Party Finder notice handler was not found; the notice opcode is never confirmed from live packets.");
        if (!Plugin.SigScanner.TryScanText(RosterParserSignature, out var roster))
            DiagnosticLog.Warn("[PartyWatch] The cross-world roster parser was not found; the roster opcode is never confirmed from live packets.");
        return (notice, roster);
    }

    public ServerPartyWatch((nint Notice, nint Roster) handlers)
    {
        Tracker = new PartyStatusTracker(networked, LogMessageText, WorldName, IsBlacklisted);
        // Not from inside a packet's handler: saving writes the config file.
        sources = new PartyOpcodeSources(Tracker, () => Plugin.Framework.RunOnTick(Save));
        if (HandledPartyOpcodes.SavedFor(Plugin.Config, gameVersion) is { } saved) sources.Restore(saved);
        try
        {
            stateFlagsHook = Plugin.GameInterop.HookFromAddress<PacketDispatcherPointers.HandlePlayerStateFlagsPacketDelegate>(
                Marshal.GetFunctionPointerForDelegate(PacketDispatcherPointers.HandlePlayerStateFlagsPacket), StateFlagsDetour);
            stateFlagsHook.Enable();
            onlineStatusHook = HookOrWarn<SetOnlineStatusFlagsDelegate>((nint)InfoModule.MemberFunctionPointers.SetOnlineStatusFlags, OnlineStatusDetour,
                "InfoModule.SetOnlineStatusFlags isn't resolved; the online status opcode is never confirmed from live packets.");
            actorControlHook = HookOrWarn<HandleActorControlDelegate>((nint)PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket, ActorControlDetour,
                "PacketDispatcher.HandleActorControlPacket isn't resolved; the ActorControl opcodes are never confirmed from live packets.");
            readyCheckHook = HookOrWarn<InitiateReadyCheckDelegate>((nint)AgentReadyCheck.MemberFunctionPointers.InitiateReadyCheck, ReadyCheckDetour,
                "AgentReadyCheck.InitiateReadyCheck isn't resolved; the ready check opcodes are never confirmed from live packets.");
            if (handlers.Notice != 0)
            {
                noticeHook = Plugin.GameInterop.HookFromAddress<NoticeHandlerDelegate>(handlers.Notice, NoticeDetour);
                noticeHook.Enable();
            }
            if (handlers.Roster != 0)
            {
                rosterHook = Plugin.GameInterop.HookFromAddress<RosterParserDelegate>(handlers.Roster, RosterDetour);
                rosterHook.Enable();
            }
        }
        catch
        {
            Dispose();
            throw;
        }
        var moduleBase = Plugin.SigScanner.Module.BaseAddress;
        var exePath = Environment.ProcessPath;
        // Up to about a second of work, kept off the framework thread.
        ThreadingTask.Run(() =>
        {
            try
            {
                ReadReceiver(moduleBase, exePath);
            }
            catch (Exception e)
            {
                DiagnosticLog.Warn($"[PartyWatch] Reading the client's protocol receiver threw: {e.Message}");
            }
        });
    }

    private void ReadReceiver(nint moduleBase, string? exePath)
    {
        var clock = Stopwatch.StartNew();
        ProtocolReceiver? read;
        string why;
        var source = "the game";
        try
        {
            read = ProtocolReceiver.Read(ModuleImage.OfRunningGame(moduleBase, exePath, out source), out why);
        }
        catch (Exception e)
        {
            read = null;
            why = e.Message;
        }
        lock (opcodes)
        {
            receiver = read;
            if (read == null)
                DiagnosticLog.Warn($"[PartyWatch] The client's protocol receiver couldn't be read ({why}); {WithoutTheReceiver}");
            else if (PartyPacketMethods.For(read) is not { } known)
                DiagnosticLog.Warn($"[PartyWatch] The client's protocol receiver has {read.Methods} methods, a revision this plugin doesn't know; {WithoutTheReceiver}");
            else if (sources.UseReceiver(known) is { } disagreement)
                DiagnosticLog.Warn($"[PartyWatch] {Capitalised(disagreement)}; the receiver's table doesn't fit this build, so its opcodes aren't used; {WithoutTheReceiver}");
            DiagnosticLog.Info($"[PartyWatch] Opcodes for {gameVersion}{(read == null ? "" : $" (receiver: {read.Methods} methods, read from {source} in {clock.ElapsedMilliseconds} ms)")}: "
                               + $"PlayerStateFlags 0x{Tracker.PlayerStateFlagsOpcode:X4}, online status 0x{Tracker.OnlineStatusOpcode:X4}, Party Finder notice 0x{Tracker.NoticeOpcode:X4}, roster {Opcodes(Tracker.RosterOpcodes)}, ActorControl {Opcodes(Tracker.ActorControlOpcodes)}, member event 0x{Tracker.MemberEventOpcode:X4}, member left 0x{Tracker.MemberLeftOpcode:X4}, member removed 0x{Tracker.MemberRemovedOpcode:X4}, disbanded 0x{Tracker.DisbandedOpcode:X4}, members' status 0x{Tracker.MemberStatusOpcode:X4}, party list 0x{Tracker.PartyListOpcode:X4} (key 0x{Tracker.PartyKeyOpcode:X4}), party event 0x{Tracker.PartyEventOpcode:X4}, cross-world ready check 0x{Tracker.CrossWorldReadyCheckOpcode:X4}/0x{Tracker.CrossWorldReadyCheckAnswerOpcode:X4} (0 or empty = not known).");
        }
    }

    private const string WithoutTheReceiver = "a party packet is known once the client has handled one outside the sim on this game version, "
                                              + "and member joins, leaves and removals, disbands and cross-world ready check answers aren't seen during runs.";

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Opcodes(IEnumerable<ushort> opcodes) => $"[{string.Join(",", opcodes.Select(o => $"0x{o:X4}"))}]";

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
        DropReadyCheck("a new run began");
        var info = InfoModule.Instance();
        var status = PartyOnlineStatus.FromFlags(info != null ? info->OnlineStatusFlags : 0);
        var player = Plugin.ObjectTable.LocalPlayer;
        var accountId = player != null ? ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address)->AccountId : 0;
        Tracker.Begin(ClientRoster(), PartyConditions.Read(flag => Plugin.Condition[flag]), status, Plugin.PlayerState.ContentId, player?.EntityId ?? 0, accountId);
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
                members.Add(new PartyMember(member->ContentId, member->NameString, (ushort)member->HomeWorld, CrossWorldAccountId(member)));
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
    // through the handlers its packets go to (PartyLift); a held ready check waits for the client
    // (TickReadyCheck). Everything else stays as the client has it. Applied: null when nothing
    // changed. Refresh: why upstream's Social refresh still has to run, null when the client already
    // asked for what it would (PartyResync). Never throws, so the lift goes on.
    public (string? Applied, string? Refresh) ApplyServerParty(Inject inject)
    {
        try
        {
            return ApplyServerPartyOrThrow(inject);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Putting the server's party state on the client threw: {e.Message}");
            return (null, "putting the server's party state on the client threw");
        }
    }

    private (string? Applied, string? Refresh) ApplyServerPartyOrThrow(Inject inject)
    {
        var now = PartyClock.UtcNow;
        var state = Tracker.TakeFromServer(now);
        if (state.Held.Count > 0) DiagnosticLog.Info($"[PartyWatch] The stay held {string.Join(", ", state.Held.Select(h => $"0x{h.Key:X4}x{h.Value}"))}.");
        if (Tracker.RecruitmentEnded && state.StatusIcon is null)
            DiagnosticLog.Warn("[PartyWatch] Recruiting ended during the run but no status icon came with it; if the game still says you're looking for a party, the icon's ActorControl (category 504) or its opcode has changed.");
        if (state.Steps.Any(step => step is LiftStep.Packet { Held.Kind: HeldKind.PartyKey }))
            DiagnosticLog.Warn("[PartyWatch] The server rekeyed the party list's decoding during the run; the key is replayed in its place.");
        if (Tracker.KeysNotReplayed > 0)
            DiagnosticLog.Warn($"[PartyWatch] {Tracker.KeysNotReplayed} packets on the party list key's opcode weren't replayed (no live packet confirmed the opcode, or past the cap): no party list after them is, and what the client decodes with those keys may read wrongly until the server rekeys again.");
        var client = new GameClient(inject);
        var (done, readyCheck, handedOver, warnings) = PartyLift.Apply(state, client, networked, Tracker.LocalContentId, now);
        foreach (var warning in warnings) DiagnosticLog.Warn($"[PartyWatch] At the lift, {warning}.");
        if (Tracker.NoticesDropped > 0) done.Add($"{Tracker.NoticesDropped} notices past the cap dropped");
        if (Tracker.MembershipDropped > 0) done.Add($"{Tracker.MembershipDropped} joins, leaves, removals and disbands past the cap dropped");
        if (Tracker.WholePartyDropped > 0) done.Add($"{Tracker.WholePartyDropped} older rosters, party lists or members' status past the cap dropped");
        if (Tracker.StatesMerged > 0) done.Add($"{Tracker.StatesMerged} party states past the cap merged");
        if (readyCheck != null)
        {
            DiagnosticLog.Info($"[PartyWatch] A ready check from {readyCheck.Check.From} was held through the run: {readyCheck.Described()}.");
            heldReadyCheck.Hold(readyCheck, client);
            TickReadyCheck();
        }
        string? refresh;
        lock (opcodes) refresh = PartyResync.Reason(state.Held, handedOver, Tracker.PartyViewOpcodes);
        return (done.Count == 0 ? null : $"the server's party state, in the order it came: {string.Join("; ", done)}", refresh);
    }

    // Every frame while the firewall is down.
    public void TickReadyCheck()
    {
        var from = heldReadyCheck.Check?.From;
        Ended(from, heldReadyCheck.Tick(PartyClock.UtcNow));
    }

    // A held ready check still waiting is never handed over after this.
    public void DropReadyCheck(string why)
    {
        var from = heldReadyCheck.Check?.From;
        Ended(from, heldReadyCheck.Drop(why, PartyClock.UtcNow));
    }

    // ZoneControl's receive hook saw a live packet on the key's opcode change the dispatcher's keys.
    internal void KeyConfirmed(ushort opcode)
    {
        if (Tracker.ConfirmedPartyKeyOpcode == opcode) return;
        Tracker.ConfirmedPartyKeyOpcode = opcode;
        DiagnosticLog.Info($"[PartyWatch] A live 0x{opcode:X4} changed the dispatcher's keys: party list keys held on it are replayed.");
    }

    // A live packet on its way to the client: an answer to the held ready check is kept for it. Inside
    // the dispatch, so it never throws.
    internal void HeardLive(nint packet, uint actor)
    {
        if (heldReadyCheck.Check is null) return;
        try
        {
            heldReadyCheck.HeardLive(Tracker, packet, actor);
        }
        catch (Exception)
        {
            // Missed, the answer shows as not given in the replayed check.
        }
    }

    private static void Ended(string? from, ReadyCheckStep? step)
    {
        if (step is not { } end) return;
        var line = $"[PartyWatch] The ready check from {from} held through the run was {(end.Outcome == ReadyCheckOutcome.Opened ? "opened" : "dropped")}: {end.Note}.";
        if (end.Warn) DiagnosticLog.Warn(line);
        else DiagnosticLog.Info(line);
    }

    // The game as the lift puts the server's word on it.
    private sealed class GameClient(Inject inject) : IPartyClient
    {
        public bool Condition(ConditionFlag flag) => Plugin.Condition[flag];

        public void HandleStateFlags(ReadOnlySpan<byte> bits)
        {
            fixed (byte* at = bits) PacketDispatcherPointers.HandlePlayerStateFlagsPacket(Conditions.Instance(), at);
        }

        public ulong? OnlineStatusFlags
        {
            get
            {
                var info = InfoModule.Instance();
                return info != null ? info->OnlineStatusFlags : null;
            }
        }

        public void SetOnlineStatusFlags(ulong flags)
        {
            var info = InfoModule.Instance();
            if (info != null) info->SetOnlineStatusFlags(flags);
        }

        public void HandleActorControl(HeldActorControl icon)
            => PacketDispatcher.HandleActorControlPacket(icon.EntityId, icon.Category, icon.P1, icon.P2, icon.P3, icon.P4, 0, 0, 0, 0, NoTarget, false);

        public bool Inject(HeldPacket packet, string what) => inject(packet.TargetId, packet.Opcode, packet.Payload, what);

        public bool CountsItselfInAParty() => InfoProxyCrossRealm.IsLocalPlayerInParty();

        public bool KnowsMember(ulong contentId) => InfoProxyCrossRealm.GetMemberByContentId(contentId) != null;

        public bool StillInPartyOf(bool crossWorld)
        {
            if (crossWorld) return InfoProxyCrossRealm.IsCrossRealmParty();
            var groups = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance();
            return groups != null && groups->MainGroup.IsAlliance || InfoProxyCrossRealm.IsLocalPlayerInParty();
        }

        public bool MainGroupHas(ulong contentId)
        {
            var groups = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance();
            return groups != null && groups->MainGroup.GetPartyMemberByContentId(contentId) != null;
        }

        public bool CanAskThePlayer() => Plugin.ObjectTable.LocalPlayer != null
                                         && !Plugin.Condition[ConditionFlag.BetweenAreas] && !Plugin.Condition[ConditionFlag.BetweenAreas51]
                                         && !Plugin.Condition[ConditionFlag.InCombat] && !Plugin.Condition[ConditionFlag.BoundByDuty56]
                                         && !Plugin.Condition[ConditionFlag.WatchingCutscene] && !Plugin.Condition[ConditionFlag.WatchingCutscene78]
                                         && !Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];

        public float? ReadyCheckTimer
        {
            get
            {
                var agent = AgentReadyCheck.Instance();
                return agent != null ? *(float*)((byte*)agent + ReadyCheckTimerOffset) : null;
            }
            set
            {
                var agent = AgentReadyCheck.Instance();
                if (agent != null && value is { } seconds) *(float*)((byte*)agent + ReadyCheckTimerOffset) = seconds;
            }
        }
    }

    private void StateFlagsDetour(Conditions* conditions, byte* bits)
    {
        Heard(PartyHandlerCalls.Payload(DispatchingPacket, (nint)bits, PartyPacket.PlayerStateFlags));
        stateFlagsHook!.OriginalDisposeSafe(conditions, bits);
    }

    private void OnlineStatusDetour(InfoModule* info, ulong flags)
    {
        Heard(PartyHandlerCalls.OnlineStatus(DispatchingPacket, flags));
        onlineStatusHook!.OriginalDisposeSafe(info, flags);
    }

    // Called for every ActorControl, the plugin's own included; those come outside a dispatch.
    private void ActorControlDetour(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, byte isRecorded)
    {
        Heard(PartyHandlerCalls.ActorControl(DispatchingPacket, DispatchingActor, entityId, category, arg1, isRecorded != 0));
        actorControlHook!.OriginalDisposeSafe(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);
    }

    // ClientStructs' InitiateReadyCheck resets the agent (HeldReadyCheckReplay.AgentReset). The plugin's
    // replays come outside a dispatch.
    private void ReadyCheckDetour(AgentReadyCheck* agent)
    {
        try
        {
            var from = heldReadyCheck.Check?.From;
            Ended(from, heldReadyCheck.AgentReset(Replaying, PartyClock.UtcNow));
        }
        catch (Exception)
        {
            // Inside the client's handler: nothing may escape.
        }
        if (DispatchingPacket is var packet and not 0) Heard(ReadyCheckStart(packet));
        readyCheckHook!.OriginalDisposeSafe(agent);
    }

    // Null when ClientStructs can't say who's in the party (it throws for a function a patch moved).
    private static NamedOpcode? ReadyCheckStart(nint packet)
    {
        try
        {
            return PartyHandlerCalls.ReadyCheck(packet, MemberAccount);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[PartyWatch] Telling a received ready check's packet threw: {e.Message}");
            return null;
        }
    }

    // A member of the cross-world party or of this world's party: their account id, 0 when not known.
    private static ulong? MemberAccount(ulong contentId)
    {
        var crossWorld = InfoProxyCrossRealm.GetMemberByContentId(contentId);
        if (crossWorld != null) return CrossWorldAccountId(crossWorld);
        var groups = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance();
        var member = groups != null ? groups->MainGroup.GetPartyMemberByContentId(contentId) : null;
        return member != null ? (ulong?)member->AccountId : null;
    }

    // The roster entry's account id, which the parser copies to +0x08; ClientStructs leaves it unnamed.
    private static ulong CrossWorldAccountId(CrossRealmMember* member) => *(ulong*)((byte*)member + 0x08);

    private void NoticeDetour(nint infoProxy, byte* payload)
    {
        Heard(PartyHandlerCalls.Payload(DispatchingPacket, (nint)payload, PartyPacket.Notice));
        noticeHook!.OriginalDisposeSafe(infoProxy, payload);
    }

    // An alliance roster's group 0 isn't at its payload + 8, so it names nothing.
    private void RosterDetour(nint infoProxy, byte* members, byte group)
    {
        if (group == 0) Heard(PartyHandlerCalls.Payload(DispatchingPacket, (nint)(members - RosterMembersOffset), PartyPacket.Roster));
        rosterHook!.OriginalDisposeSafe(infoProxy, members, group);
    }

    // Under the opcode lock, as the receiver's read may be filling the tracker at the same time. Inside
    // the client's handler, so it never throws.
    private void Heard(NamedOpcode? named)
    {
        if (named is not { } handled || Replaying || handled.Opcode == 0) return;
        try
        {
            lock (opcodes)
            {
                var seen = sources.Observed(handled);
                if (seen.Retired != null)
                {
                    DiagnosticLog.Warn($"[PartyWatch] {Capitalised(seen.Retired)}; the receiver's table doesn't fit this build, so its opcodes aren't used again this session; {WithoutTheReceiver}");
                    return;
                }
                if (seen.Packet != handled.Packet && moved.Add(handled.Opcode))
                    DiagnosticLog.Info($"[PartyWatch] A ready check on 0x{handled.Opcode:X4} read as {handled.Packet}, but the receiver's table gives that opcode {seen.Packet}; kept as {seen.Packet}.");
                if (!seen.Expected)
                    DiagnosticLog.Info($"[PartyWatch] {seen.Packet} arrives as opcode 0x{handled.Opcode:X4}{MethodNote(handled.Opcode)}.");
                else if (confirmed.Add(seen.Packet))
                    DiagnosticLog.Info($"[PartyWatch] The client handled {seen.Packet} as 0x{handled.Opcode:X4}{MethodNote(handled.Opcode)}, as expected.");
            }
        }
        catch (Exception e)
        {
            if (!heardThrew) DiagnosticLog.Warn($"[PartyWatch] Learning from the client's {handled.Packet} handler threw: {e.Message}");
            heardThrew = true;
        }
    }

    private string MethodNote(ushort opcode) => receiver?.MethodOf(opcode) is var method and >= 0 ? $" (receiver method {method})" : "";

    private void Save()
    {
        HandledPartyOpcodes saved;
        lock (opcodes) saved = sources.Saved();
        saved.SaveTo(Plugin.Config, gameVersion);
        Plugin.Config.Save();
    }

    public void Dispose()
    {
        stateFlagsHook?.Dispose();
        onlineStatusHook?.Dispose();
        actorControlHook?.Dispose();
        readyCheckHook?.Dispose();
        noticeHook?.Dispose();
        rosterHook?.Dispose();
    }
}
