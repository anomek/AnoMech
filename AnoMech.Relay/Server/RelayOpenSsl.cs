using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AnoMech.Relay;

// The OpenSSL a relay on Linux serves TLS with: .NET loads the system's libssl. An update to it
// replaces the library on disk while a running process keeps the copy it loaded, which
// /proc/self/maps then marks deleted: that's how this tells a restart is due, as needrestart
// does. Windows and macOS bring their own TLS, updated with the system.
public static class RelayOpenSsl
{
    private const string MapsFile = "/proc/self/maps";
    private const string Deleted = " (deleted)";
    private static readonly string[] SslNames = ["libssl.so.3", "libssl.so.1.1"];
    private static readonly string[] CryptoNames = ["libcrypto.so.3", "libcrypto.so.1.1"];

    // Without a libssl, .NET ends the process the moment TLS first needs it.
    public static bool Available => !OperatingSystem.IsLinux() || SslNames.Any(name => NativeLibrary.TryLoad(name, out _));

    // Its version and where it was loaded from, once TLS has loaded it; null off Linux.
    public static string? Describe()
    {
        if (!OperatingSystem.IsLinux()) return null;
        var loaded = Libraries(ReadMaps());
        var ssl = loaded.FirstOrDefault(l => IsSsl(l.Path)).Path;
        var crypto = loaded.FirstOrDefault(l => IsCrypto(l.Path)).Path;
        return $"{Version(crypto) ?? "OpenSSL"} ({ssl ?? "not loaded yet"})";
    }

    // The OpenSSL libraries this process loaded that have been replaced on disk since.
    public static IReadOnlyList<string> Replaced() => OperatingSystem.IsLinux() ? Replaced(ReadMaps()) : [];

    public static bool InContainer => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || File.Exists("/.dockerenv");

    internal static IReadOnlyList<string> Replaced(string maps) => Libraries(maps).Where(l => l.Replaced).Select(l => l.Path).ToList();

    // Each libssl and libcrypto mapped, once, replaced if any of its mappings is of a deleted file.
    internal static IReadOnlyList<(string Path, bool Replaced)> Libraries(string maps)
    {
        var found = new List<(string Path, bool Replaced)>();
        foreach (var line in maps.Split('\n'))
        {
            // address perms offset device inode path
            var fields = line.Split((char[]?)null, 6, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 6) continue;
            var path = fields[5].Trim();
            var replaced = path.EndsWith(Deleted, StringComparison.Ordinal);
            if (replaced) path = path[..^Deleted.Length];
            if (!IsSsl(path) && !IsCrypto(path)) continue;
            var known = found.FindIndex(f => f.Path == path);
            if (known < 0) found.Add((path, replaced));
            else if (replaced) found[known] = (path, true);
        }
        return found;
    }

    private static bool IsSsl(string path) => Path.GetFileName(path).StartsWith("libssl.so", StringComparison.Ordinal);

    private static bool IsCrypto(string path) => Path.GetFileName(path).StartsWith("libcrypto.so", StringComparison.Ordinal);

    private static string ReadMaps()
    {
        try
        {
            return File.ReadAllText(MapsFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr VersionText(int type);

    // OpenSSL_version(OPENSSL_VERSION), from the libcrypto this process already runs on.
    private static string? Version(string? cryptoPath)
    {
        var handle = IntPtr.Zero;
        if (cryptoPath is null || !NativeLibrary.TryLoad(cryptoPath, out handle))
            foreach (var name in CryptoNames)
                if (NativeLibrary.TryLoad(name, out handle)) break;
        if (handle == IntPtr.Zero || !NativeLibrary.TryGetExport(handle, "OpenSSL_version", out var export)) return null;
        return Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<VersionText>(export)(0));
    }
}
