using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using AnoMech.Core;
using Dalamud.Hooking;

namespace AnoMech.Offline;

// While armed, nothing in the process can change the game's user folder, character folders or
// saved appearance data: not the game, not Dalamud, not another plugin. The hooks sit on
// KernelBase's own implementations, which every module's file calls end in; reads and every
// other path pass straight through.
internal sealed unsafe class OfflineFileGuard : IDisposable
{
    private const string KernelBase = "kernelbase.dll";
    private const string CharacterFolder = "FFXIV_CHR", SavedAppearance = "FFXIV_CHARA";
    private const string ProbeName = "AnoMech-offline-guard-probe";
    private const uint OpenExisting = 3;
    private const uint DeleteOnClose = 0x04000000;
    private const uint BackupSemantics = 0x02000000;
    private const uint ShareAll = 7;
    // GENERIC_WRITE, GENERIC_ALL, MAXIMUM_ALLOWED, DELETE, WRITE_DAC, WRITE_OWNER, FILE_WRITE_DATA,
    // FILE_APPEND_DATA, FILE_WRITE_EA, FILE_WRITE_ATTRIBUTES.
    private const uint WriteAccess = 0x40000000 | 0x10000000 | 0x02000000 | 0x10000 | 0x40000 | 0x80000 | 0x2 | 0x4 | 0x10 | 0x100;
    // CREATEFILE2_EXTENDED_PARAMETERS.dwFileFlags.
    private const int ExtendedFlagsOffset = 8;
    private const uint AccessDenied = 5;
    private const int AccessDeniedResult = unchecked((int)0x80070005);
    private const nint InvalidHandle = -1;
    private const int MaxPath = 1024;

    private delegate nint CreateFileDelegate(char* name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    private delegate nint CreateFile2Delegate(char* name, uint access, uint share, uint disposition, byte* parameters);
    private delegate nint ReOpenFileDelegate(nint handle, uint access, uint share, uint flags);
    private delegate int PathDelegate(char* path);
    private delegate int CreateDirectoryDelegate(char* path, nint security);
    private delegate int CreateDirectoryExDelegate(char* template, char* path, nint security);
    private delegate int SetAttributesDelegate(char* path, uint attributes);
    private delegate int MoveFileExDelegate(char* from, char* to, uint flags);
    private delegate int MoveFileWithProgressDelegate(char* from, char* to, nint progress, nint data, uint flags);
    private delegate int CopyFileDelegate(char* from, char* to, int failIfExists);
    private delegate int CopyFileExDelegate(char* from, char* to, nint progress, nint data, int* cancel, uint flags);
    private delegate int CopyFile2Delegate(char* from, char* to, nint parameters);
    private delegate int ReplaceFileDelegate(char* replaced, char* replacement, char* backup, uint flags, nint exclude, nint reserved);
    private delegate int CreateLinkDelegate(char* link, char* existing, nint security);
    private delegate byte CreateSymbolicLinkDelegate(char* link, char* target, uint flags);

    // A thread can still be inside a detour when the hooks are disposed, so the detours and the
    // originals they call stay reachable for the life of the process.
    private static readonly List<object> KeepAlive = [];

    private readonly string userFolder;
    private readonly string shortUserFolder;
    private readonly string finalUserFolder;
    private readonly List<IDisposable> hooks = [];
    private readonly List<Action> enables = [];
    private CreateFileDelegate createFile = null!;
    private CreateFile2Delegate createFile2 = null!;
    private ReOpenFileDelegate reOpenFile = null!;
    private CreateDirectoryDelegate createDirectory = null!;
    private CreateDirectoryExDelegate createDirectoryEx = null!;
    private PathDelegate deleteFile = null!;
    private PathDelegate removeDirectory = null!;
    private SetAttributesDelegate setAttributes = null!;
    private MoveFileExDelegate moveFileEx = null!;
    private MoveFileWithProgressDelegate moveFileWithProgress = null!;
    private CopyFileDelegate copyFile = null!;
    private CopyFileExDelegate copyFileEx = null!;
    private CopyFile2Delegate copyFile2 = null!;
    private ReplaceFileDelegate replaceFile = null!;
    private CreateLinkDelegate createHardLink = null!;
    private CreateSymbolicLinkDelegate createSymbolicLink = null!;
    private bool armed;
    private volatile bool probing;
    private long refused;

    public OfflineFileGuard()
    {
        try
        {
            userFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(OfflineGameFiles.UserFolder())) + Path.DirectorySeparatorChar;
        }
        catch
        {
            userFolder = "";
        }
        shortUserFolder = userFolder.Length == 0 ? "" : ShortPath(userFolder);
        finalUserFolder = userFolder.Length == 0 ? "" : FinalPath(userFolder);
    }

    // Where the folder really is, when a junction or redirect leads to it.
    private static string FinalPath(string folder)
    {
        var handle = CreateFileProbe(folder, 0, ShareAll, 0, OpenExisting, BackupSemantics, 0);
        if (handle == InvalidHandle) return folder;
        try
        {
            var buffer = stackalloc char[MaxPath];
            var length = GetFinalPathNameByHandleW(handle, buffer, MaxPath, 0);
            if (length is 0 or >= MaxPath) return folder;
            return Path.TrimEndingDirectorySeparator(Unprefixed(new string(buffer, 0, (int)length))) + Path.DirectorySeparatorChar;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static string Unprefixed(string path)
        => path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + path[8..]
            : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..]
            : path;

    private static string ShortPath(string path)
    {
        var buffer = stackalloc char[MaxPath];
        fixed (char* name = path)
        {
            var length = GetShortPathNameW(name, buffer, MaxPath);
            return length is > 0 and < MaxPath ? new string(buffer, 0, (int)length) : path;
        }
    }

    public bool Arm(out string why)
    {
        why = "";
        if (armed) return true;
        if (userFolder.Length == 0)
        {
            why = "the game's user folder couldn't be determined";
            return false;
        }
        try
        {
            createFile = Install<CreateFileDelegate>("CreateFileW", CreateFileDetour);
            createFile2 = Install<CreateFile2Delegate>("CreateFile2", CreateFile2Detour);
            reOpenFile = Install<ReOpenFileDelegate>("ReOpenFile", ReOpenFileDetour);
            createDirectory = Install<CreateDirectoryDelegate>("CreateDirectoryW", CreateDirectoryDetour);
            createDirectoryEx = Install<CreateDirectoryExDelegate>("CreateDirectoryExW", CreateDirectoryExDetour);
            deleteFile = Install<PathDelegate>("DeleteFileW", DeleteFileDetour);
            removeDirectory = Install<PathDelegate>("RemoveDirectoryW", RemoveDirectoryDetour);
            setAttributes = Install<SetAttributesDelegate>("SetFileAttributesW", SetAttributesDetour);
            moveFileEx = Install<MoveFileExDelegate>("MoveFileExW", MoveFileExDetour);
            moveFileWithProgress = Install<MoveFileWithProgressDelegate>("MoveFileWithProgressW", MoveFileWithProgressDetour);
            copyFile = Install<CopyFileDelegate>("CopyFileW", CopyFileDetour);
            copyFileEx = Install<CopyFileExDelegate>("CopyFileExW", CopyFileExDetour);
            copyFile2 = Install<CopyFile2Delegate>("CopyFile2", CopyFile2Detour);
            replaceFile = Install<ReplaceFileDelegate>("ReplaceFileW", ReplaceFileDetour);
            createHardLink = Install<CreateLinkDelegate>("CreateHardLinkW", CreateHardLinkDetour);
            createSymbolicLink = Install<CreateSymbolicLinkDelegate>("CreateSymbolicLinkW", CreateSymbolicLinkDetour);
            // Every thread in the process runs these, some possibly under the loader lock, where
            // compiling a detour or its marshalling stub on first use could deadlock.
            WarmUp();
            foreach (var enable in enables) enable();
            armed = true;
            if (Probe() is { } failed)
            {
                Dispose();
                why = $"the file guard didn't take effect ({failed})";
                DiagnosticLog.Warn($"[Offline] File guard failed its check: {failed}.");
                return false;
            }
            DiagnosticLog.Info($"[Offline] File guard armed and checked: nothing in the process can change {userFolder}, FFXIV_CHR folders or saved appearance data.");
            return true;
        }
        catch (Exception e)
        {
            Dispose();
            why = $"the file guard couldn't be installed ({e.Message})";
            DiagnosticLog.Warn($"[Offline] File guard failed to arm: {e}");
            return false;
        }
    }

    private T Install<T>(string function, T detour) where T : Delegate
    {
        var hook = Plugin.GameInterop.HookFromSymbol(KernelBase, function, detour);
        hooks.Add(hook);
        enables.Add(hook.Enable);
        KeepAlive.Add(detour);
        KeepAlive.Add(hook.Original);
        return hook.Original;
    }

    // Each detour is called once through its native entry point on an empty path, which it and the
    // original both reject.
    private void WarmUp()
    {
        foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            if (!method.ContainsGenericParameters) RuntimeHelpers.PrepareMethod(method.MethodHandle);
        fixed (char* empty = "")
        {
            ((delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)Entry<CreateFileDelegate>(CreateFileDetour))(empty, 0, 0, 0, OpenExisting, 0, 0);
            ((delegate* unmanaged<char*, uint, uint, uint, byte*, nint>)Entry<CreateFile2Delegate>(CreateFile2Detour))(empty, 0, 0, OpenExisting, null);
            ((delegate* unmanaged<nint, uint, uint, uint, nint>)Entry<ReOpenFileDelegate>(ReOpenFileDetour))(InvalidHandle, 0, 0, 0);
            ((delegate* unmanaged<char*, nint, int>)Entry<CreateDirectoryDelegate>(CreateDirectoryDetour))(empty, 0);
            ((delegate* unmanaged<char*, char*, nint, int>)Entry<CreateDirectoryExDelegate>(CreateDirectoryExDetour))(empty, empty, 0);
            ((delegate* unmanaged<char*, int>)Entry<PathDelegate>(DeleteFileDetour))(empty);
            ((delegate* unmanaged<char*, int>)Entry<PathDelegate>(RemoveDirectoryDetour))(empty);
            ((delegate* unmanaged<char*, uint, int>)Entry<SetAttributesDelegate>(SetAttributesDetour))(empty, 0);
            ((delegate* unmanaged<char*, char*, uint, int>)Entry<MoveFileExDelegate>(MoveFileExDetour))(empty, empty, 0);
            ((delegate* unmanaged<char*, char*, nint, nint, uint, int>)Entry<MoveFileWithProgressDelegate>(MoveFileWithProgressDetour))(empty, empty, 0, 0, 0);
            ((delegate* unmanaged<char*, char*, int, int>)Entry<CopyFileDelegate>(CopyFileDetour))(empty, empty, 1);
            ((delegate* unmanaged<char*, char*, nint, nint, int*, uint, int>)Entry<CopyFileExDelegate>(CopyFileExDetour))(empty, empty, 0, 0, null, 0);
            ((delegate* unmanaged<char*, char*, nint, int>)Entry<CopyFile2Delegate>(CopyFile2Detour))(empty, empty, 0);
            ((delegate* unmanaged<char*, char*, char*, uint, nint, nint, int>)Entry<ReplaceFileDelegate>(ReplaceFileDetour))(empty, empty, null, 0, 0, 0);
            ((delegate* unmanaged<char*, char*, nint, int>)Entry<CreateLinkDelegate>(CreateHardLinkDetour))(empty, empty, 0);
            ((delegate* unmanaged<char*, char*, uint, byte>)Entry<CreateSymbolicLinkDelegate>(CreateSymbolicLinkDetour))(empty, empty, 0);
        }
    }

    private static nint Entry<T>(T detour) where T : Delegate
    {
        KeepAlive.Add(detour);
        return Marshal.GetFunctionPointerForDelegate(detour);
    }

    // Every hooked function once, through the real exports, on paths in the user folder whose parent
    // or source doesn't exist: each must come back access denied, and none can change anything even
    // if its hook weren't there.
    private string? Probe()
    {
        var file = userFolder + ProbeName;
        var nested = userFolder + ProbeName + @"\missing";
        var outside = Path.Combine(Path.GetTempPath(), ProbeName, "missing");
        probing = true;
        try
        {
            if (!Denied(CreateFileProbe(file, 0x40000000, OpenExisting, 0))) return "CreateFileW";
            if (!Denied(CreateFile2Probe(file, 0x40000000, OpenExisting, 0))) return "CreateFile2";
            if (!Denied(ReOpenProbe())) return "ReOpenFile";
            if (!Denied(CreateDirectoryProbe(nested, 0))) return "CreateDirectoryW";
            if (!Denied(CreateDirectoryExProbe(null, nested, 0))) return "CreateDirectoryExW";
            if (!Denied(DeleteFileProbe(file))) return "DeleteFileW";
            if (!Denied(RemoveDirectoryProbe(file))) return "RemoveDirectoryW";
            if (!Denied(SetFileAttributesProbe(file, 0x80))) return "SetFileAttributesW";
            if (!Denied(MoveFileExProbe(file, file + "2", 0))) return "MoveFileExW";
            if (!Denied(MoveFileWithProgressProbe(file, file + "2", 0, 0, 0))) return "MoveFileWithProgressW";
            if (!Denied(CopyFileProbe(outside, file, 1))) return "CopyFileW";
            if (!Denied(CopyFileExProbe(outside, file, 0, 0, 0, 0))) return "CopyFileExW";
            if (CopyFile2Probe(outside, file, 0) != AccessDeniedResult) return "CopyFile2";
            if (!Denied(ReplaceFileProbe(file, file + "2", null, 0, 0, 0))) return "ReplaceFileW";
            if (!Denied(CreateHardLinkProbe(nested, outside, 0))) return "CreateHardLinkW";
            if (!Denied(CreateSymbolicLinkProbe(nested, outside, 0))) return "CreateSymbolicLinkW";
            return null;
        }
        finally
        {
            probing = false;
        }
    }

    private static bool Denied(nint handle)
    {
        var error = Marshal.GetLastPInvokeError();
        if (handle != InvalidHandle) CloseHandle(handle);
        return handle == InvalidHandle && error == AccessDenied;
    }

    private static bool Denied(int result) => result == 0 && Marshal.GetLastPInvokeError() == AccessDenied;

    private static bool Denied(byte result) => result == 0 && Marshal.GetLastPInvokeError() == AccessDenied;

    // A read handle on a file in the user folder, reopened to write its attributes; without the
    // guard that succeeds and changes nothing. A folder handle can't be reopened at all, so with no
    // file there, there is nothing a reopen could reach.
    private nint ReOpenProbe()
    {
        var existing = Directory.EnumerateFiles(userFolder, "*", SearchOption.AllDirectories).FirstOrDefault();
        if (existing == null)
        {
            Marshal.SetLastPInvokeError((int)AccessDenied);
            return InvalidHandle;
        }
        var original = CreateFileProbe(existing, 0x80000000, OpenExisting, 0);
        if (original == InvalidHandle) return InvalidHandle;
        var reopened = ReOpenFileProbe(original, 0x100, ShareAll, 0);
        var error = Marshal.GetLastPInvokeError();
        CloseHandle(original);
        Marshal.SetLastPInvokeError(error);
        return reopened;
    }

    private nint CreateFileDetour(char* name, uint access, uint share, nint security, uint disposition, uint flags, nint template)
        => Writes(access, disposition, flags) && Guarded(name)
            ? RefuseOpen(name)
            : createFile(name, access, share, security, disposition, flags, template);

    private nint CreateFile2Detour(char* name, uint access, uint share, uint disposition, byte* parameters)
        => Writes(access, disposition, parameters == null ? 0 : *(uint*)(parameters + ExtendedFlagsOffset)) && Guarded(name)
            ? RefuseOpen(name)
            : createFile2(name, access, share, disposition, parameters);

    private nint ReOpenFileDetour(nint handle, uint access, uint share, uint flags)
    {
        if ((access & WriteAccess) != 0 || (flags & DeleteOnClose) != 0)
        {
            var buffer = stackalloc char[MaxPath];
            var length = GetFinalPathNameByHandleW(handle, buffer, MaxPath, 0);
            if (length is > 0 and < MaxPath)
            {
                buffer[length] = '\0';
                if (Guarded(buffer)) return RefuseOpen(buffer);
            }
        }
        return reOpenFile(handle, access, share, flags);
    }

    private static bool Writes(uint access, uint disposition, uint flags)
        => (access & WriteAccess) != 0 || disposition != OpenExisting || (flags & DeleteOnClose) != 0;

    private int CreateDirectoryDetour(char* path, nint security)
        => Guarded(path) ? Refuse("create", path, 0) : createDirectory(path, security);

    private int CreateDirectoryExDetour(char* template, char* path, nint security)
        => Guarded(path) ? Refuse("create", path, 0) : createDirectoryEx(template, path, security);

    private int DeleteFileDetour(char* path)
        => Guarded(path) ? Refuse("delete", path, 0) : deleteFile(path);

    private int RemoveDirectoryDetour(char* path)
        => Guarded(path) ? Refuse("remove", path, 0) : removeDirectory(path);

    private int SetAttributesDetour(char* path, uint attributes)
        => Guarded(path) ? Refuse("change the attributes of", path, 0) : setAttributes(path, attributes);

    private int MoveFileExDetour(char* from, char* to, uint flags)
        => Guarded(from) || Guarded(to) ? Refuse("move", from, 0) : moveFileEx(from, to, flags);

    private int MoveFileWithProgressDetour(char* from, char* to, nint progress, nint data, uint flags)
        => Guarded(from) || Guarded(to) ? Refuse("move", from, 0) : moveFileWithProgress(from, to, progress, data, flags);

    private int CopyFileDetour(char* from, char* to, int failIfExists)
        => Guarded(to) ? Refuse("copy into", to, 0) : copyFile(from, to, failIfExists);

    private int CopyFileExDetour(char* from, char* to, nint progress, nint data, int* cancel, uint flags)
        => Guarded(to) ? Refuse("copy into", to, 0) : copyFileEx(from, to, progress, data, cancel, flags);

    private int CopyFile2Detour(char* from, char* to, nint parameters)
        => Guarded(to) ? Refuse("copy into", to, AccessDeniedResult) : copyFile2(from, to, parameters);

    private int ReplaceFileDetour(char* replaced, char* replacement, char* backup, uint flags, nint exclude, nint reserved)
        => Guarded(replaced) || Guarded(replacement) || Guarded(backup)
            ? Refuse("replace", replaced, 0)
            : replaceFile(replaced, replacement, backup, flags, exclude, reserved);

    // A link made elsewhere to a guarded file would write through to it.
    private int CreateHardLinkDetour(char* link, char* existing, nint security)
        => Guarded(link) || Guarded(existing) ? Refuse("link", link, 0) : createHardLink(link, existing, security);

    private byte CreateSymbolicLinkDetour(char* link, char* target, uint flags)
        => Guarded(link) || Guarded(target) ? (byte)Refuse("link", link, 0) : createSymbolicLink(link, target, flags);

    private bool Guarded(char* path)
    {
        if (path == null) return false;
        var name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(path);
        if (name.Contains(CharacterFolder, StringComparison.OrdinalIgnoreCase) || name.Contains(SavedAppearance, StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) name = (@"\\" + name[8..].ToString()).AsSpan();
        else if (name.StartsWith(@"\\?\") || name.StartsWith(@"\??\") || name.StartsWith(@"\\.\")) name = name[4..];
        if (name.Contains("..", StringComparison.Ordinal) && Normalized(name) is { } full) name = full;
        return Under(name, userFolder) || Under(name, shortUserFolder) || Under(name, finalUserFolder);
    }

    private static string? Normalized(ReadOnlySpan<char> name)
    {
        try
        {
            return Path.GetFullPath(name.ToString());
        }
        catch
        {
            return null;
        }
    }

    // The folder itself, or anything below it, with either separator.
    private static bool Under(ReadOnlySpan<char> name, string folder)
    {
        if (folder.Length == 0 || name.Length < folder.Length - 1) return false;
        for (var i = 0; i < folder.Length; i++)
        {
            if (i == name.Length) return i == folder.Length - 1;
            var c = name[i] == '/' ? '\\' : name[i];
            if (char.ToUpperInvariant(c) != char.ToUpperInvariant(folder[i])) return false;
        }
        return true;
    }

    private nint RefuseOpen(char* path)
    {
        Refuse("open for writing", path, 0);
        return InvalidHandle;
    }

    private int Refuse(string what, char* path, int failure)
    {
        if (!probing)
        {
            var count = Interlocked.Increment(ref refused);
            if (count <= 20 || count % 100 == 0)
                DiagnosticLog.Warn($"[Offline] Refused an attempt to {what} {(path == null ? "?" : new string(path))} (#{count}).");
        }
        SetLastError(AccessDenied);
        return failure;
    }

    public void Dispose()
    {
        foreach (var hook in hooks) hook.Dispose();
        hooks.Clear();
        armed = false;
    }

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint code);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll")]
    private static extern uint GetShortPathNameW(char* longPath, char* shortPath, uint length);

    [DllImport("kernel32.dll")]
    private static extern uint GetFinalPathNameByHandleW(nint handle, char* path, uint length, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateFileProbe(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    private static nint CreateFileProbe(string name, uint access, uint disposition, uint flags) => CreateFileProbe(name, access, ShareAll, 0, disposition, flags, 0);

    [DllImport("kernel32.dll", EntryPoint = "CreateFile2", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateFile2Probe(string name, uint access, uint share, uint disposition, nint parameters);

    private static nint CreateFile2Probe(string name, uint access, uint disposition, nint parameters) => CreateFile2Probe(name, access, ShareAll, disposition, parameters);

    [DllImport("kernel32.dll", EntryPoint = "ReOpenFile", SetLastError = true)]
    private static extern nint ReOpenFileProbe(nint handle, uint access, uint share, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CreateDirectoryProbe(string path, nint security);

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CreateDirectoryExProbe(string? template, string path, nint security);

    [DllImport("kernel32.dll", EntryPoint = "DeleteFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int DeleteFileProbe(string path);

    [DllImport("kernel32.dll", EntryPoint = "RemoveDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int RemoveDirectoryProbe(string path);

    [DllImport("kernel32.dll", EntryPoint = "SetFileAttributesW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SetFileAttributesProbe(string path, uint attributes);

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int MoveFileExProbe(string from, string to, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "MoveFileWithProgressW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int MoveFileWithProgressProbe(string from, string to, nint progress, nint data, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CopyFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CopyFileProbe(string from, string to, int failIfExists);

    [DllImport("kernel32.dll", EntryPoint = "CopyFileExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CopyFileExProbe(string from, string to, nint progress, nint data, nint cancel, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CopyFile2", CharSet = CharSet.Unicode)]
    private static extern int CopyFile2Probe(string from, string to, nint parameters);

    [DllImport("kernel32.dll", EntryPoint = "ReplaceFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int ReplaceFileProbe(string replaced, string replacement, string? backup, uint flags, nint exclude, nint reserved);

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CreateHardLinkProbe(string link, string existing, nint security);

    [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern byte CreateSymbolicLinkProbe(string link, string target, uint flags);
}
