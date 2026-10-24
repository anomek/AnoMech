using System;
using AnoMech.Core;
using Dalamud.Hooking;

namespace AnoMech.Offline;

// With no server, NetworkModule holds no zone or chat client. Every sender found checks for that
// before calling ZoneClient/ChatClient.SendPacket, which dereference the client first thing; these
// detours drop whatever reaches them anyway, and the log shows it if anything ever does.
internal sealed class OfflineNetworkGuard : IDisposable
{
    private const string ZoneSendSignature = "48 83 EC 28 48 8B 89 98 00 00 00 48 85 C9 74 ?? 44 89 44 24";
    // The only call site that sends opcode 0x69 through the chat client; resolved to its callee.
    private const string ChatSendCallSite = "48 8B 89 78 0A 00 00 48 85 C9 74 ?? 48 89 54 24 ?? 48 8D 54 24 ?? C7 44 24 ?? 69 00 00 00 48 C7 44 24 ?? 18 00 00 00 E8";
    private const int ChatSendCallOffset = 39;

    private delegate byte SendDelegate(nint client, nint packet, nint a3, nint a4);

    private Hook<SendDelegate>? zoneSend;
    private Hook<SendDelegate>? chatSend;
    private long droppedZone;
    private long droppedChat;

    public bool Armed => zoneSend is { IsEnabled: true } && chatSend is { IsEnabled: true };

    public bool Arm(out string why)
    {
        why = "";
        if (Armed) return true;
        try
        {
            var zoneAddress = Plugin.SigScanner.ScanText(ZoneSendSignature);
            var site = Plugin.SigScanner.ScanText(ChatSendCallSite) + ChatSendCallOffset;
            var chatAddress = site + 5 + System.Runtime.InteropServices.Marshal.ReadInt32(site + 1);
            zoneSend ??= Plugin.GameInterop.HookFromAddress<SendDelegate>(zoneAddress, ZoneSendDetour);
            chatSend ??= Plugin.GameInterop.HookFromAddress<SendDelegate>(chatAddress, ChatSendDetour);
            zoneSend.Enable();
            chatSend.Enable();
            DiagnosticLog.Info($"[Offline] Network guard armed: zone send 0x{zoneAddress:X}, chat send 0x{chatAddress:X}.");
            return true;
        }
        catch (Exception e)
        {
            why = $"a required game function wasn't found ({e.Message})";
            DiagnosticLog.Warn($"[Offline] Network guard failed to arm: {e}");
            return false;
        }
    }

    private byte ZoneSendDetour(nint client, nint packet, nint a3, nint a4)
    {
        if (droppedZone++ % 500 == 0) DiagnosticLog.Info($"[Offline] Dropped outbound zone packets: {droppedZone} (client 0x{client:X}).");
        return 0;
    }

    private byte ChatSendDetour(nint client, nint packet, nint a3, nint a4)
    {
        if (droppedChat++ % 50 == 0) DiagnosticLog.Info($"[Offline] Dropped outbound chat packets: {droppedChat} (client 0x{client:X}).");
        return 0;
    }

    public void Dispose()
    {
        zoneSend?.Dispose();
        chatSend?.Dispose();
    }
}
