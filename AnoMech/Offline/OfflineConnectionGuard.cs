using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using AnoMech.Core;

namespace AnoMech.Offline;

// While offline mode has changed what a server would see, the game reaching for a server means a
// login is starting, most likely a plugin logging in on its own. The game closes before the attempt
// leaves the process. Only the game's own imports are hooked: Dalamud's and plugins' own internet
// traffic doesn't go through them.
internal sealed unsafe class OfflineConnectionGuard : IDisposable
{
    private const string Winsock = "ws2_32.dll", WinHttp = "winhttp.dll";
    // The game imports these two by ordinal.
    private const uint ConnectOrdinal = 4, GetHostByNameOrdinal = 52;
    private const int SocketError = -1, HostNotFound = 11001;
    private const ushort InternetFamily = 2, Internet6Family = 23;

    private delegate int ConnectDelegate(nint socket, byte* address, int length);
    private delegate nint GetHostByNameDelegate(byte* name);
    private delegate int GetAddrInfoDelegate(byte* node, byte* service, nint hints, nint* result);
    private delegate nint WinHttpConnectDelegate(nint session, char* server, ushort port, uint reserved);
    private delegate int WinHttpSendRequestDelegate(nint request, char* headers, uint headersLength, nint optional, uint optionalLength, uint totalLength, nint context);

    // A thread can still be inside a detour when the hooks are disposed.
    private static readonly List<object> KeepAlive = [];
    private static int closing;

    private readonly List<IDisposable> hooks = [];
    private readonly List<Action> enables = [];
    private ConnectDelegate connect = null!;
    private GetHostByNameDelegate getHostByName = null!;
    private GetAddrInfoDelegate getAddrInfo = null!;
    private WinHttpConnectDelegate winHttpConnect = null!;
    private WinHttpSendRequestDelegate winHttpSendRequest = null!;
    private bool armed;

    public bool Arm(out string why)
    {
        why = "";
        if (armed) return true;
        try
        {
            connect = Install<ConnectDelegate>(Winsock, "connect", ConnectOrdinal, ConnectDetour);
            getHostByName = Install<GetHostByNameDelegate>(Winsock, "gethostbyname", GetHostByNameOrdinal, GetHostByNameDetour);
            getAddrInfo = Install<GetAddrInfoDelegate>(Winsock, "getaddrinfo", 0, GetAddrInfoDetour);
            winHttpConnect = Install<WinHttpConnectDelegate>(WinHttp, "WinHttpConnect", 0, WinHttpConnectDetour);
            winHttpSendRequest = Install<WinHttpSendRequestDelegate>(WinHttp, "WinHttpSendRequest", 0, WinHttpSendRequestDetour);
            foreach (var enable in enables) enable();
            armed = true;
            DiagnosticLog.Info("[Offline] Connection guard armed: a server connection or lookup by the game closes it while offline mode is active.");
            return true;
        }
        catch (Exception e)
        {
            Dispose();
            why = $"the connection guard couldn't be installed ({e.Message})";
            DiagnosticLog.Warn($"[Offline] Connection guard failed to arm: {e}");
            return false;
        }
    }

    private T Install<T>(string module, string function, uint ordinal, T detour) where T : Delegate
    {
        var hook = Plugin.GameInterop.HookFromImport(null, module, function, ordinal, detour);
        hooks.Add(hook);
        enables.Add(hook.Enable);
        KeepAlive.Add(detour);
        KeepAlive.Add(hook.Original);
        return hook.Original;
    }

    private int ConnectDetour(nint socket, byte* address, int length)
    {
        if (!OfflineProcessState.Tainted) return connect(socket, address, length);
        Close($"connect to {Describe(address, length)}");
        return SocketError;
    }

    private nint GetHostByNameDetour(byte* name)
    {
        if (!OfflineProcessState.Tainted) return getHostByName(name);
        Close($"look up {Marshal.PtrToStringAnsi((nint)name) ?? "a server"}");
        return 0;
    }

    private int GetAddrInfoDetour(byte* node, byte* service, nint hints, nint* result)
    {
        if (!OfflineProcessState.Tainted) return getAddrInfo(node, service, hints, result);
        Close($"look up {Marshal.PtrToStringAnsi((nint)node) ?? "a server"}");
        return HostNotFound;
    }

    private nint WinHttpConnectDetour(nint session, char* server, ushort port, uint reserved)
    {
        if (!OfflineProcessState.Tainted) return winHttpConnect(session, server, port, reserved);
        Close($"connect to {(server == null ? "a server" : new string(server))}:{port}");
        return 0;
    }

    private int WinHttpSendRequestDetour(nint request, char* headers, uint headersLength, nint optional, uint optionalLength, uint totalLength, nint context)
    {
        if (!OfflineProcessState.Tainted) return winHttpSendRequest(request, headers, headersLength, optional, optionalLength, totalLength, context);
        Close("send a web request");
        return 0;
    }

    // Any other thread that gets here meanwhile waits with its attempt held until the process ends.
    private static void Close(string attempt)
    {
        if (Interlocked.Exchange(ref closing, 1) != 0) Thread.Sleep(Timeout.Infinite);
        OfflineSession.CloseGameForLogin($"the game tried to {attempt}");
    }

    private static string Describe(byte* address, int length)
    {
        if (address == null || length < 8) return "a server";
        var family = *(ushort*)address;
        var port = (address[2] << 8) | address[3];
        if (family == InternetFamily) return $"{address[4]}.{address[5]}.{address[6]}.{address[7]}:{port}";
        if (family == Internet6Family && length >= 24) return $"[{new IPAddress(new ReadOnlySpan<byte>(address + 8, 16))}]:{port}";
        return "a server";
    }

    public void Dispose()
    {
        foreach (var hook in hooks) hook.Dispose();
        hooks.Clear();
        enables.Clear();
        armed = false;
    }
}
