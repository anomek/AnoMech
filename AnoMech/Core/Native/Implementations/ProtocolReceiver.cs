using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Iced.Intel;

namespace AnoMech.Core.Native.Implementations;

// One section of the game's module, at the address the code it holds expects.
internal abstract class ImageSection(string name, ulong address, int length, bool code, bool writable)
{
    public string Name { get; } = name;
    public ulong Address { get; } = address;
    public int Length { get; } = length;
    public bool Code { get; } = code;
    public bool Writable { get; } = writable;
    public ulong End => Address + (ulong)Length;

    public abstract ReadOnlySpan<byte> Bytes { get; }

    public bool Holds(ulong address, int length)
        => length >= 0 && address >= Address && length <= Length && address - Address <= (ulong)(Length - length);
}

internal sealed class ArraySection(string name, ulong address, byte[] data, int offset, int length, bool code, bool writable)
    : ImageSection(name, address, length, code, writable)
{
    public ArraySection(string name, ulong address, byte[] data, bool code, bool writable) : this(name, address, data, 0, data.Length, code, writable) { }

    public override ReadOnlySpan<byte> Bytes => data.AsSpan(offset, Length);
}

internal sealed unsafe class LoadedSection(string name, ulong address, int length, bool code, bool writable)
    : ImageSection(name, address, length, code, writable)
{
    public override ReadOnlySpan<byte> Bytes => new((void*)Address, Length);
}

internal sealed class ModuleImage(IReadOnlyList<ImageSection> sections)
{
    private const uint Execute = 0x20000000;
    private const uint Read = 0x40000000;
    private const uint Write = 0x80000000;
    private const uint Discardable = 0x02000000;

    public IReadOnlyList<ImageSection> Sections { get; } = sections;
    // An array: the scan for receivers looks up every pointer-sized value of the constant data.
    private readonly ImageSection[] lookup = [.. sections];

    // The running game's module, from the exe on disk when that file is the build running: a file
    // holds none of the hooks other plugins may have put in the loaded code. Otherwise as loaded.
    public static ModuleImage OfRunningGame(nint moduleBase, string? exePath, out string source)
    {
        var loaded = Loaded(moduleBase, out var loadedIdentity);
        try
        {
            if (exePath != null && File.Exists(exePath))
            {
                byte[] file;
                using (var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    file = new byte[stream.Length];
                    stream.ReadExactly(file);
                }
                var fromFile = FromFile(file, out var fileIdentity);
                if (fileIdentity == loadedIdentity)
                {
                    source = "the game's exe";
                    return fromFile;
                }
                source = "the loaded game (its exe on disk is another build)";
                return loaded;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            source = $"the loaded game (its exe couldn't be read: {e.Message})";
            return loaded;
        }
        source = "the loaded game";
        return loaded;
    }

    // The sections the loader mapped, from the module's own headers; a discardable one may be gone.
    public static unsafe ModuleImage Loaded(nint moduleBase, out (uint TimeStamp, uint ImageSize) identity)
    {
        var headers = new ReadOnlySpan<byte>((void*)moduleBase, 0x1000);
        var sections = new List<ImageSection>();
        identity = Headers(headers, (name, rva, size, _, characteristics) =>
            sections.Add(new LoadedSection(name, (ulong)moduleBase + rva, (int)size, (characteristics & Execute) != 0, (characteristics & Write) != 0)));
        return new ModuleImage(sections);
    }

    // An exe's file mapped at its preferred base, as the loader would without relocating it.
    public static ModuleImage FromFile(byte[] file, out (uint TimeStamp, uint ImageSize) identity)
    {
        var sections = new List<ImageSection>();
        var preferredBase = 0UL;
        identity = Headers(file, (name, rva, size, raw, characteristics) =>
        {
            var (rawOffset, rawSize) = raw;
            var length = (int)Math.Min(size, rawSize);
            if (rawOffset + (long)length > file.Length) throw new InvalidDataException($"section {name} runs past the file's end");
            sections.Add(new ArraySection(name, preferredBase + rva, file, (int)rawOffset, length, (characteristics & Execute) != 0, (characteristics & Write) != 0));
        }, imageBase => preferredBase = imageBase);
        return new ModuleImage(sections);
    }

    private delegate void SectionFound(string name, uint rva, uint size, (uint Offset, uint Size) raw, uint characteristics);

    // Walks the PE headers; only readable sections that stay mapped are reported.
    private static (uint TimeStamp, uint ImageSize) Headers(ReadOnlySpan<byte> headers, SectionFound found, Action<ulong>? imageBase = null)
    {
        if (headers.Length < 0x40 || BitConverter.ToUInt16(headers) != 0x5A4D) throw new InvalidDataException("no DOS header");
        var nt = BitConverter.ToInt32(headers[0x3C..]);
        if (nt < 0 || nt + 0x108 > headers.Length || BitConverter.ToUInt32(headers[nt..]) != 0x00004550) throw new InvalidDataException("no PE header");
        if (BitConverter.ToUInt16(headers[(nt + 24)..]) != 0x20B) throw new InvalidDataException("not a 64-bit image");
        var timeStamp = BitConverter.ToUInt32(headers[(nt + 8)..]);
        var count = BitConverter.ToUInt16(headers[(nt + 6)..]);
        var optionalSize = BitConverter.ToUInt16(headers[(nt + 20)..]);
        imageBase?.Invoke(BitConverter.ToUInt64(headers[(nt + 24 + 24)..]));
        var imageSize = BitConverter.ToUInt32(headers[(nt + 24 + 56)..]);
        var at = nt + 24 + optionalSize;
        if (at + 40 * count > headers.Length) throw new InvalidDataException("section headers past the headers");
        for (var i = 0; i < count; i++, at += 40)
        {
            var header = headers.Slice(at, 40);
            var size = BitConverter.ToUInt32(header[8..]);
            var rva = BitConverter.ToUInt32(header[12..]);
            var raw = (BitConverter.ToUInt32(header[20..]), BitConverter.ToUInt32(header[16..]));
            var characteristics = BitConverter.ToUInt32(header[36..]);
            if (size == 0 || size > int.MaxValue || (characteristics & Read) == 0 || (characteristics & Discardable) != 0) continue;
            found(Encoding.ASCII.GetString(header[..8]).TrimEnd('\0'), rva, size, raw, characteristics);
        }
        return (timeStamp, imageSize);
    }

    public ImageSection? SectionHolding(ulong address, int length)
    {
        foreach (var section in lookup)
            if (section.Holds(address, length)) return section;
        return null;
    }
}

// The client's generated receiver for zone packets: its dispatch hands each packet to one method of a
// receiver interface by opcode, and the interface's packet methods, empty in the client, follow the
// protocol's own order. Patches reshuffle the opcodes but keep that order, so a packet's place among
// the methods names it where its opcode doesn't. A revision is recognised by its method count alone: a
// patch that inserts and removes as many packets keeps the count and shifts the places between them,
// which only the party watch's live checks (PartyOpcodeSources) can catch. Places count from the first
// method reached, so a dispatch that reached a virtual just outside the methods, and no method at their
// other end, would shift them too; the generated one reaches packet methods only.
// The map is read by running the dispatch itself for every opcode (DispatchModel), not by decoding its
// switch, whose encoding the compiler is free to change.
internal sealed class ProtocolReceiver
{
    // Well under the client's own count, and well over any other table of empty methods it has.
    public const int MinMethods = 256;
    // How far before its packet methods the vtable may keep the dispatch.
    private const int DispatchReach = 4;

    private readonly Dictionary<ushort, int> methodOf;
    private readonly Dictionary<int, ushort> opcodeOf;

    private ProtocolReceiver(ulong dispatch, int methods, Dictionary<ushort, int> methodOf, Dictionary<int, ushort> opcodeOf)
    {
        Dispatch = dispatch;
        Methods = methods;
        this.methodOf = methodOf;
        this.opcodeOf = opcodeOf;
    }

    public ulong Dispatch { get; }

    // The receiver's packet methods; their count names the protocol revision.
    public int Methods { get; }

    // A packet's place among the methods, from 0; -1 for an opcode the receiver hands to none.
    public int MethodOf(ushort opcode) => methodOf.TryGetValue(opcode, out var method) ? method : -1;

    // 0 when no opcode reaches that method.
    public ushort OpcodeOf(int method) => opcodeOf.TryGetValue(method, out var opcode) ? opcode : (ushort)0;

    // Null when no receiver is found, or its dispatch does anything but hand each opcode, on the opcode
    // alone, to at most one of its own methods and each method to at most one opcode (DispatchModel), or
    // the methods it reaches don't span its longest run of empty methods; `why` then says what.
    public static ProtocolReceiver? Read(ModuleImage image, out string why)
    {
        var candidates = DispatchCandidates(image);
        if (candidates.Count == 0)
        {
            why = "no table of receiver methods was found";
            return null;
        }
        var models = new[] { new DispatchModel(image, scrambled: false), new DispatchModel(image, scrambled: true) };
        var found = new List<ProtocolReceiver>();
        var failures = new List<string>();
        foreach (var (dispatch, methods) in candidates)
        {
            var maps = models.Select(model => Map(model, dispatch, methods, out var failure) ?? (object)failure).ToArray();
            if (maps[0] is not ProtocolReceiver receiver)
                failures.Add($"0x{dispatch:X}: {maps[0]}");
            else if (maps[1] is not ProtocolReceiver other || !receiver.SameAs(other))
                failures.Add($"0x{dispatch:X}: other stand-ins give {(maps[1] as string) ?? "another map"}");
            else if (found.All(f => !f.SameAs(receiver)))
                found.Add(receiver);
        }
        // The zone protocol's receiver is the client's largest: beside others, it's read only as the
        // largest and the one revision the table knows.
        var known = found.Where(f => PartyPacketMethods.ByRevision.ContainsKey(f.Methods)).ToList();
        if (found.Count == 1 || known.Count == 1 && found.All(f => f == known[0] || f.Methods < known[0].Methods))
        {
            var chosen = found.Count == 1 ? found[0] : known[0];
            // One the model refused may be the largest, and so the zone one.
            if (candidates.Values.Max() > chosen.Methods)
            {
                why = $"a run longer than the {chosen.Methods} methods of 0x{chosen.Dispatch:X} gave no map: {string.Join("; ", failures)}";
                return null;
            }
            why = "";
            return chosen;
        }
        why = found.Count > 1
            ? $"{found.Count} receivers, at {string.Join(", ", found.Select(f => $"0x{f.Dispatch:X}"))}, map opcodes differently"
            : string.Join("; ", failures);
        return null;
    }

    private bool SameAs(ProtocolReceiver other)
        => Methods == other.Methods && methodOf.Count == other.methodOf.Count && methodOf.All(pair => other.MethodOf(pair.Key) == pair.Value);

    // Every opcode through the dispatch. `methods`: the longest run of empty methods it heads in a
    // vtable, which the methods reached have to span exactly, so that their places line up with the
    // protocol's. A shorter run is as likely a derived class's vtable overriding the method at an end as
    // the receiver's own with no opcode reaching that method, so it can't count; a derived one that's
    // longer, its own next virtual empty, gives no map.
    private static ProtocolReceiver? Map(DispatchModel model, ulong dispatch, int methods, out string why)
    {
        var slotOf = new Dictionary<ushort, int>();
        var opcodeAt = new Dictionary<int, ushort>();
        for (var value = 0; value <= ushort.MaxValue; value++)
        {
            var opcode = (ushort)value;
            var slot = model.Run(dispatch, opcode, out var failure);
            if (failure != null)
            {
                why = $"opcode 0x{opcode:X4}: {failure}";
                return null;
            }
            if (slot < 0) continue;
            if (!opcodeAt.TryAdd(slot, opcode))
            {
                why = $"opcodes 0x{opcodeAt[slot]:X4} and 0x{opcode:X4} both reach method {slot}";
                return null;
            }
            slotOf[opcode] = slot;
        }
        if (opcodeAt.Count == 0)
        {
            why = "no opcode reaches a method";
            return null;
        }
        var first = opcodeAt.Keys.Min();
        var span = opcodeAt.Keys.Max() - first + 1;
        if (span != methods)
        {
            why = $"the methods reached span {span} slots, not the {methods} empty ones of its longest run";
            return null;
        }
        why = "";
        return new ProtocolReceiver(dispatch, span,
            slotOf.ToDictionary(pair => pair.Key, pair => pair.Value - first),
            opcodeAt.ToDictionary(pair => pair.Key - first, pair => pair.Value));
    }

    // For each run of at least MinMethods empty methods in a constant data section's pointers, the
    // functions just before it, each with the longest run it heads.
    private static Dictionary<ulong, int> DispatchCandidates(ModuleImage image)
    {
        var candidates = new Dictionary<ulong, int>();
        foreach (var section in image.Sections)
        {
            if (section.Code || section.Writable) continue;
            var first = (int)((8 - section.Address % 8) % 8);
            var run = 0;
            var end = first + 8 * ((section.Length - first) / 8);
            for (var at = first; at <= end; at += 8)
            {
                if (at < end && IsEmptyMethod(image, BitConverter.ToUInt64(section.Bytes.Slice(at, 8))))
                {
                    run++;
                    continue;
                }
                if (run >= MinMethods)
                {
                    var runStart = at - 8 * run;
                    for (var back = 1; back <= DispatchReach && runStart - 8 * back >= 0; back++)
                    {
                        var dispatch = BitConverter.ToUInt64(section.Bytes.Slice(runStart - 8 * back, 8));
                        if (image.SectionHolding(dispatch, 1) is not { Code: true } || IsEmptyMethod(image, dispatch)) continue;
                        candidates[dispatch] = Math.Max(candidates.GetValueOrDefault(dispatch), run);
                    }
                }
                run = 0;
            }
        }
        return candidates;
    }

    // A method that only returns: ret, ret 0, or a return of 0 or false.
    private static bool IsEmptyMethod(ModuleImage image, ulong address)
    {
        if (image.SectionHolding(address, 1) is not { Code: true } section) return false;
        var code = section.Bytes[(int)(address - section.Address)..];
        return code[0] == 0xC3
               || code.Length >= 3 && code[0] == 0xC2 && code[1] == 0 && code[2] == 0
               || code.Length >= 3 && code[0] is 0x30 or 0x31 or 0x32 or 0x33 && code[1] == 0xC0 && code[2] == 0xC3;
    }
}

// Runs the receiver's dispatch for one opcode and names the vtable slot it hands the packet to. Nothing
// runs natively: each instruction is decoded and applied to a model that reads only the module's code
// and constant data and its own stand-ins, and writes only its own stack. Anything it doesn't model
// ends the run, so a patch that turns the dispatch into something else gives no answer instead of a
// wrong one.
//
// Stand-ins: the receiver (rcx), whose vtable's slot i is a stub for its method, and the packet holder
// (r8) the dispatch reads the packet through. Every qword of the holder points at a channel of its own
// and every qword of a channel at a packet of its own, so the dispatch reaches the packet whichever
// fields it follows. The first one whose IPC header (magic 0x14, then the opcode) it reads is the zone
// packet, which is taken on trust.
//
// The model knows a bit only where the game holds the same whatever the packet beyond its magic and
// opcode, the receiver's members, the registers the dispatch isn't handed, its caller's stack and where
// the loader put the module: constants, the module's constant data, the magic and the opcode, and what
// follows from those. A pointer, the module's own addresses too, is only an address: none of its bits
// is known, and the one question it answers is a null test, for a pointer the game can't hold null.
// The holder and the fields read from it may be null until the dispatch reads through them, so a test
// before that is run both ways, and its null way may hand the packet to no other method. A method that
// returns into the dispatch may change all the dispatch hasn't kept to itself, and no second method may
// follow. A branch, a jump target, a store's address or a method slot that depends on a bit the model
// doesn't know ends the run, so a map is the one the opcode alone gives. A second model, with other
// stand-in addresses and other unknown bits, has to give the same map: a check on that bookkeeping.
internal sealed class DispatchModel
{
    private const int MaxSteps = 512;
    private const int MaxCalls = 16;
    private const int MaxSlots = 0x1000;
    private const int MaxRuns = 32;

    private const ulong ReceiverAt = 0x00000;
    private const ulong VtableAt = 0x100000;
    private const ulong StubsAt = 0x200000;
    private const ulong HolderAt = 0x300000;
    private const ulong ChannelAt = 0x400000;
    private const ulong PacketAt = 0x500000;
    private const ulong StackAt = 0x600000;
    private const ulong ReturnAt = 0x700000;
    private const int ObjectSize = 0x1000;
    private const int PacketSize = 0x4000;
    private const int StackSize = 0x10000;
    // 8 past a multiple of 16 in both models, as at any function's entry.
    private const int EntryRsp = StackSize - 0x108;
    // Above a called method's return address, which it may write.
    private const int HomeSpace = 0x20;
    private const int StubSize = 0x10;
    private const ushort IpcMagic = 0x14;
    // 16 past a multiple of 32, so an alignment the game's stack may or may not have moves one model's
    // and not the other's.
    private const ulong ScrambledOffset = 0x3A5B0;
    private const byte ScrambledFill = 0x5A;
    // Windows maps nothing in the lowest 64 KiB or the top 64 KiB below 2^47, so a valid pointer moved
    // by less stays nonzero and below 2^47.
    private const long PointerSlack = 0x10000;

    private static readonly Register[] Arguments = [Register.RCX, Register.RDX, Register.R8, Register.R9];
    private static readonly Register[] Volatile = [Register.RAX, Register.RCX, Register.RDX, Register.R8, Register.R9, Register.R10, Register.R11];

    private readonly ModuleImage image;
    private readonly ulong fake;
    private readonly ulong fill;
    private readonly bool scrambled;
    // The loader moves every address in the module with it.
    private readonly ulong moduleStart;
    private readonly ulong moduleEnd;
    private readonly Dictionary<ulong, Instruction> decoded = [];
    private readonly ulong[] gpr = new ulong[16];
    private readonly ulong[] gprKnown = new ulong[16];
    private readonly int[] gprPointer = new int[16];
    private readonly byte[] stack = new byte[StackSize];
    private readonly byte[] stackKnown = new byte[StackSize];
    // A pointer stored whole on the stack, byte by byte, and which of its bytes each one is.
    private readonly int[] stackPointer = new int[StackSize];
    private readonly byte[] stackPart = new byte[StackSize];
    private readonly byte[] packet = new byte[PacketSize];
    // The holder and the fields read from it that the run has read through: the game holds those valid.
    private readonly HashSet<int> readThrough = [];
    // Those it tested for null before reading through them, each a run of its own where it's null.
    private readonly List<int> splits = [];
    // Which of them the game holds null in this run, and which earlier tests took to be valid.
    private int[] heldNull = [];
    private int[] heldValid = [];
    private int stackWrittenFrom;
    private int packetPath;
    // The method the dispatch has handed the packet to, -1 before.
    private int reached = -1;
    private Flag known;
    private Flag set;

    public DispatchModel(ModuleImage image, bool scrambled)
    {
        this.image = image;
        this.scrambled = scrambled;
        fake = new[] { 0x1000_0000_0000UL, 0x2000_0000_0000UL, 0x3000_0000_0000UL }
                   .First(at => image.Sections.All(s => s.End + 0x1_0000_0000 < at || s.Address > at + ReturnAt + 0x1_0000_0000))
               + (scrambled ? ScrambledOffset : 0);
        fill = scrambled ? 0x5A5A_5A5A_5A5A_5A5AUL : 0;
        if (image.Sections.Count > 0)
        {
            // From the headers, on the 64 KiB boundary the loader puts the module at.
            moduleStart = image.Sections.Min(s => s.Address) & ~0xFFFFUL;
            moduleEnd = image.Sections.Max(s => s.End);
        }
        if (scrambled) Array.Fill(packet, ScrambledFill);
        packet[0] = (byte)IpcMagic;
        packet[1] = (byte)(IpcMagic >> 8);
    }

    [Flags]
    private enum Flag : byte { Carry = 1, Zero = 2, Sign = 4, Overflow = 8, Parity = 16 }

    private const Flag EveryFlag = Flag.Carry | Flag.Zero | Flag.Sign | Flag.Overflow | Flag.Parity;

    private enum Region { None, Receiver, Vtable, Stubs, Holder, Channel, Packet, Stack, Return, Module }

    // A value as the run has it: its bits, those of them the game is sure to hold too, and for a
    // pointer what it points into. The bits above a narrower value's size are known zero.
    private readonly record struct Value(ulong Bits, ulong Known, int Pointer = 0)
    {
        public bool IsKnown => Known == ulong.MaxValue;
    }

    private sealed class Unmodelled(string message) : Exception(message);

    // Windows maps nothing in the lowest 64 KiB: the game faults there and goes no further.
    private sealed class Faults() : Exception;

    private const ulong NullPage = 0x10000;

    // The vtable slot the dispatch hands the packet to, or -1 if it returns without one; `failure`
    // says why the run stopped when the dispatch did something this doesn't model.
    public int Run(ulong dispatch, ushort opcode, out string? failure)
    {
        var slot = RunWith([], [], dispatch, opcode, out failure);
        if (failure != null) return -1;
        var nullWays = new Queue<(int[] Null, int[] Valid)>();
        QueueNullWays(nullWays, [], []);
        for (var runs = 1; nullWays.TryDequeue(out var way); runs++)
        {
            var held = Name(way.Null[^1]);
            if (runs == MaxRuns)
            {
                failure = $"null tests split it into more than {MaxRuns} runs";
                return -1;
            }
            var other = RunWith(way.Null, way.Valid, dispatch, opcode, out failure);
            if (failure != null)
            {
                failure = $"with {held} null, {failure}";
                return -1;
            }
            // The game may drop the packet where the field is null, but not send it elsewhere.
            if (other >= 0 && other != slot)
            {
                failure = $"with {held} null, it reaches method {other}{(slot >= 0 ? $", not {slot}" : "")}";
                return -1;
            }
            QueueNullWays(nullWays, way.Null, way.Valid);
        }
        return slot;
    }

    // The null way of each test the last run split at, its earlier ones taken as that run took them.
    private void QueueNullWays(Queue<(int[] Null, int[] Valid)> nullWays, int[] wasNull, int[] wasValid)
    {
        for (var i = 0; i < splits.Count; i++) nullWays.Enqueue(([.. wasNull, splits[i]], [.. wasValid, .. splits.Take(i)]));
    }

    private int RunWith(int[] nulls, int[] valid, ulong dispatch, ushort opcode, out string? failure)
    {
        for (var i = 0; i < gpr.Length; i++)
        {
            gpr[i] = scrambled ? 0x5A5A_5A5A_5A5A_5A00UL | (uint)i : 0;
            gprKnown[i] = 0;
            gprPointer[i] = 0;
        }
        stack.AsSpan(stackWrittenFrom).Fill(scrambled ? ScrambledFill : (byte)0);
        stackKnown.AsSpan(stackWrittenFrom).Clear();
        stackPointer.AsSpan(stackWrittenFrom).Clear();
        stackWrittenFrom = StackSize;
        packet[2] = (byte)opcode;
        packet[3] = (byte)(opcode >> 8);
        packetPath = -1;
        reached = -1;
        heldNull = nulls;
        heldValid = valid;
        readThrough.Clear();
        splits.Clear();
        known = 0;
        set = 0;
        SetRegister(Register.RCX, PointerInto(Region.Receiver));
        SetRegister(Register.R8, Produce(Region.Holder));
        var rsp = PointerInto(Region.Stack, EntryRsp);
        SetRegister(Register.RSP, rsp);
        Store(rsp, 8, PointerInto(Region.Return));
        try
        {
            var rip = dispatch;
            var calls = 0;
            for (var step = 0; step < MaxSteps; step++)
            {
                var ins = Decode(rip);
                if (ins.HasLockPrefix || (ins.HasRepPrefix || ins.HasRepnePrefix) && ins.Mnemonic != Mnemonic.Ret)
                    throw new Unmodelled($"{ins.Mnemonic} with a lock or repeat prefix at 0x{ins.IP:X}");
                var next = Execute(ins, ref calls);
                if (next.Ended)
                {
                    failure = null;
                    return reached;
                }
                rip = next.Rip;
            }
            throw new Unmodelled($"still running after {MaxSteps} instructions");
        }
        catch (Faults)
        {
            failure = null;
            return reached;
        }
        catch (Unmodelled e)
        {
            failure = e.Message;
            return -1;
        }
    }

    private readonly record struct Next(ulong Rip, bool Ended = false)
    {
        public static readonly Next End = new(0, Ended: true);
    }

    private Next Execute(in Instruction ins, ref int calls)
    {
        var after = new Next(ins.NextIP);
        switch (ins.Mnemonic)
        {
            case Mnemonic.Nop or Mnemonic.Endbr64 or Mnemonic.Lfence:
                return after;
            case Mnemonic.Mov:
                Put(ins, 0, Get(ins, 1, Size(ins, 0)));
                return after;
            case Mnemonic.Movzx:
                Put(ins, 0, Get(ins, 1, Size(ins, 1)));
                return after;
            case Mnemonic.Movsx or Mnemonic.Movsxd:
                Put(ins, 0, SignExtend(Get(ins, 1, Size(ins, 1)), Size(ins, 1)));
                return after;
            case Mnemonic.Cbw:
                SetRegister(Register.AX, SignExtend(GetRegister(Register.AL), 1));
                return after;
            case Mnemonic.Cwde:
                SetRegister(Register.EAX, SignExtend(GetRegister(Register.AX), 2));
                return after;
            case Mnemonic.Cdqe:
                SetRegister(Register.RAX, SignExtend(GetRegister(Register.EAX), 4));
                return after;
            case Mnemonic.Cwd:
                SetRegister(Register.DX, SignFill(GetRegister(Register.AX), 2));
                return after;
            case Mnemonic.Cdq:
                SetRegister(Register.EDX, SignFill(GetRegister(Register.EAX), 4));
                return after;
            case Mnemonic.Cqo:
                SetRegister(Register.RDX, SignFill(GetRegister(Register.RAX), 8));
                return after;
            case Mnemonic.Lea:
                if (ins.GetOpKind(1) != OpKind.Memory) throw new Unmodelled($"lea without memory at 0x{ins.IP:X}");
                Put(ins, 0, Address(ins));
                return after;
            case Mnemonic.Add or Mnemonic.Sub or Mnemonic.Cmp or Mnemonic.And or Mnemonic.Or or Mnemonic.Xor or Mnemonic.Test:
                Arithmetic(ins);
                return after;
            case Mnemonic.Inc or Mnemonic.Dec or Mnemonic.Neg or Mnemonic.Not:
                Unary(ins);
                return after;
            case Mnemonic.Shl or Mnemonic.Sal or Mnemonic.Shr or Mnemonic.Sar or Mnemonic.Rol or Mnemonic.Ror:
                Shift(ins);
                return after;
            case Mnemonic.Bt:
                BitTest(ins);
                return after;
            case Mnemonic.Imul when ins.OpCount >= 2:
                Multiply(ins);
                return after;
            case Mnemonic.Xchg:
                if (ins.GetOpKind(0) != OpKind.Register || ins.GetOpKind(1) != OpKind.Register)
                    throw new Unmodelled($"xchg with memory at 0x{ins.IP:X}");
                var first = GetRegister(ins.GetOpRegister(0));
                SetRegister(ins.GetOpRegister(0), GetRegister(ins.GetOpRegister(1)));
                SetRegister(ins.GetOpRegister(1), first);
                return after;
            case Mnemonic.Push:
                if (ins.StackPointerIncrement != -8) throw new Unmodelled($"a push of other than a qword at 0x{ins.IP:X}");
                Push(Get(ins, 0, 8));
                return after;
            case Mnemonic.Pop:
                if (ins.StackPointerIncrement != 8) throw new Unmodelled($"a pop of other than a qword at 0x{ins.IP:X}");
                Put(ins, 0, Pop());
                return after;
            case Mnemonic.Jmp:
                return Transfer(Target(ins));
            case Mnemonic.Call:
                if (++calls > MaxCalls) throw new Unmodelled($"makes more than {MaxCalls} calls");
                var callee = Target(ins);
                if (RegionOf(callee.Pointer) == Region.Stubs)
                {
                    CallMethod(callee);
                    return after;
                }
                Push(ModuleAddress(ins.NextIP));
                return Transfer(callee);
            case Mnemonic.Ret:
                var to = Pop();
                if (ins.OpCount == 1) SetRegister(Register.RSP, Sum(GetRegister(Register.RSP), Constant(ins.GetImmediate(0) & 0xFFFF), 8));
                return Transfer(to);
        }
        if (Jcc.Contains(ins.Mnemonic))
            return (Evaluate(ins.ConditionCode, known) ?? throw new Unmodelled($"{ins.Mnemonic} at 0x{ins.IP:X} reads a flag the game decides"))
                ? Transfer(ModuleAddress(ins.NearBranchTarget))
                : after;
        if (Cmovcc.Contains(ins.Mnemonic))
        {
            // A 32-bit destination is written, and so zero-extended, either way. On flags the game
            // decides, either value may land: only the bits both know alike are known.
            var size = Size(ins, 0);
            var source = Get(ins, 1, size);
            var kept = Get(ins, 0, size);
            Put(ins, 0, Evaluate(ins.ConditionCode, known) switch
            {
                true => source,
                false => kept,
                null => new Value(Evaluate(ins.ConditionCode, EveryFlag) == true ? source.Bits : kept.Bits, source.Known & kept.Known & ~(source.Bits ^ kept.Bits)),
            });
            return after;
        }
        if (Setcc.Contains(ins.Mnemonic))
        {
            Put(ins, 0, Evaluate(ins.ConditionCode, known) switch
            {
                true => Constant(1),
                false => Constant(0),
                null => new Value(Evaluate(ins.ConditionCode, EveryFlag) == true ? 1UL : 0, ~1UL),
            });
            return after;
        }
        throw new Unmodelled($"{ins.Mnemonic} at 0x{ins.IP:X}");
    }

    private static readonly HashSet<Mnemonic> Jcc =
    [
        Mnemonic.Jo, Mnemonic.Jno, Mnemonic.Jb, Mnemonic.Jae, Mnemonic.Je, Mnemonic.Jne, Mnemonic.Jbe, Mnemonic.Ja,
        Mnemonic.Js, Mnemonic.Jns, Mnemonic.Jp, Mnemonic.Jnp, Mnemonic.Jl, Mnemonic.Jge, Mnemonic.Jle, Mnemonic.Jg,
    ];

    private static readonly HashSet<Mnemonic> Cmovcc =
    [
        Mnemonic.Cmovo, Mnemonic.Cmovno, Mnemonic.Cmovb, Mnemonic.Cmovae, Mnemonic.Cmove, Mnemonic.Cmovne, Mnemonic.Cmovbe, Mnemonic.Cmova,
        Mnemonic.Cmovs, Mnemonic.Cmovns, Mnemonic.Cmovp, Mnemonic.Cmovnp, Mnemonic.Cmovl, Mnemonic.Cmovge, Mnemonic.Cmovle, Mnemonic.Cmovg,
    ];

    private static readonly HashSet<Mnemonic> Setcc =
    [
        Mnemonic.Seto, Mnemonic.Setno, Mnemonic.Setb, Mnemonic.Setae, Mnemonic.Sete, Mnemonic.Setne, Mnemonic.Setbe, Mnemonic.Seta,
        Mnemonic.Sets, Mnemonic.Setns, Mnemonic.Setp, Mnemonic.Setnp, Mnemonic.Setl, Mnemonic.Setge, Mnemonic.Setle, Mnemonic.Setg,
    ];

    private Instruction Decode(ulong rip)
    {
        if (decoded.TryGetValue(rip, out var cached)) return cached;
        if (image.SectionHolding(rip, 1) is not { Code: true } section) throw new Unmodelled($"runs 0x{rip:X}, outside the module's code");
        var available = (int)Math.Min(15, section.End - rip);
        var bytes = section.Bytes.Slice((int)(rip - section.Address), available).ToArray();
        var ins = DecodeAs(bytes, rip, DecoderOptions.None);
        // An operand-size prefix on a branch is one instruction to Intel processors and another to AMD.
        var amd = DecodeAs(bytes, rip, DecoderOptions.AMD);
        if (amd.Code != ins.Code || amd.Length != ins.Length) throw new Unmodelled($"code at 0x{rip:X} that Intel and AMD processors decode differently");
        decoded[rip] = ins;
        return ins;
    }

    private static Instruction DecodeAs(byte[] bytes, ulong rip, DecoderOptions options)
    {
        var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(bytes), rip, options);
        decoder.Decode(out var ins);
        if (ins.Code == Code.INVALID || decoder.LastError != DecoderError.None) throw new Unmodelled($"undecodable code at 0x{rip:X}");
        return ins;
    }

    private Value Target(in Instruction ins) => ins.GetOpKind(0) switch
    {
        OpKind.NearBranch64 => ModuleAddress(ins.NearBranchTarget),
        OpKind.Register when ins.GetOpRegister(0).IsGPR64() => GetRegister(ins.GetOpRegister(0)),
        OpKind.Memory when Size(ins, 0) == 8 => Load(Address(ins), 8),
        _ => throw new Unmodelled($"a {ins.Mnemonic} through {ins.GetOpKind(0)} at 0x{ins.IP:X}"),
    };

    private Next Transfer(Value target)
    {
        if (target is { Pointer: 0, IsKnown: true } && target.Bits < NullPage) throw new Faults();
        switch (RegionOf(target.Pointer))
        {
            case Region.Module:
                if (image.SectionHolding(target.Bits, 1) is not { Code: true }) throw new Unmodelled($"jumps to 0x{target.Bits:X}, outside the module's code");
                return new Next(target.Bits);
            case Region.Return when target.Bits == fake + ReturnAt:
                return Next.End;
            case Region.Stubs:
                // A tail call returns to the dispatch's caller; any other jump to a method returns into
                // the dispatch, wherever the top of the stack says.
                var rsp = GetRegister(Register.RSP);
                if (rsp.Pointer == PointerTo(Region.Stack) && rsp.Bits == fake + StackAt + EntryRsp
                    && Load(rsp, 8) is var top && top.Pointer == PointerTo(Region.Return) && top.Bits == fake + ReturnAt)
                {
                    Reach(target);
                    return Next.End;
                }
                var back = Pop();
                CallMethod(target);
                return Transfer(back);
        }
        throw new Unmodelled(target is { Pointer: 0, IsKnown: true }
            ? $"jumps to 0x{target.Bits:X}, which isn't the module's"
            : $"jumps to an address the game decides, 0x{target.Bits:X} here");
    }

    // Stubs-tagged values sit exactly on a stub: they're made only at a slot and moved only by 0.
    private void Reach(Value stub)
    {
        var slot = (int)((stub.Bits - (fake + StubsAt)) / StubSize);
        if (reached >= 0) throw new Unmodelled($"reaches method {slot} after method {reached}");
        reached = slot;
    }

    // A method that returns into the dispatch may write the volatile registers and the flags, what it
    // can reach (the receiver, the holder, the packet), and the dispatch's stack: its own home space and
    // stack parameters, however many it takes, and whatever a pointer it's handed reaches, which the
    // model may no longer know points there. Only the dispatch's return address is kept; a pointer
    // handed to it that the model knows reaches that far is refused.
    private void CallMethod(Value stub)
    {
        var rsp = GetRegister(Register.RSP);
        var top = rsp.Bits - (fake + StackAt) + HomeSpace;
        if (RegionOf(rsp.Pointer) != Region.Stack || top > StackSize) throw new Unmodelled($"calls a method with its stack pointer at 0x{rsp.Bits:X}");
        foreach (var argument in Arguments)
            if (RegionOf(gprPointer[Slot(argument)]) == Region.Stack) BelowTheReturnAddress(gpr[Slot(argument)]);
        for (var at = (int)top; at + 8 <= StackSize; at++)
            if (RegionOf(stackPointer[at]) == Region.Stack && stackPart[at] == 0) BelowTheReturnAddress(Little(stack.AsSpan(at, 8)));
        Reach(stub);
        foreach (var register in Volatile) SetRegister(register, Unknown(8));
        known = 0;
        for (var at = stackWrittenFrom; at < StackSize; at++)
        {
            if (at >= EntryRsp && at < EntryRsp + 8) continue;
            stack[at] = (byte)fill;
            stackKnown[at] = 0;
            stackPointer[at] = 0;
        }
    }

    private void BelowTheReturnAddress(ulong pointer)
    {
        if (pointer - (fake + StackAt) >= EntryRsp) throw new Unmodelled($"hands a method a pointer to its return address or above, 0x{pointer:X}");
    }

    private void Arithmetic(in Instruction ins)
    {
        var size = Size(ins, 0);
        var a = Get(ins, 0, size);
        var b = Get(ins, 1, size);
        var mask = Mask(size);
        var sign = SignBit(size);
        var whole = ((a.Known & b.Known) | ~mask) == ulong.MaxValue;
        Value result;
        switch (ins.Mnemonic)
        {
            case Mnemonic.Sub or Mnemonic.Cmp or Mnemonic.Xor when SameRegister(ins):
                // Zero, whatever the register holds.
                result = Constant(0);
                SetFlag(Flag.Carry, false);
                SetFlag(Flag.Overflow, false);
                break;
            case Mnemonic.Test or Mnemonic.And or Mnemonic.Or or Mnemonic.Cmp when size == 8 && a.Pointer != 0
                                                   && (ins.Mnemonic == Mnemonic.Cmp ? b.IsKnown && b.Bits == 0 : SameRegister(ins)):
                // A null test, which leaves the pointer as it is: a valid pointer is nonzero and below
                // 2^47 wherever it points, but its low byte's parity is anyone's. A field not yet read
                // through may hold any value at all: the test says only whether it's zero.
                var proven = TestedForNull(a, out var moved);
                SetFlag(Flag.Carry, false);
                SetFlag(Flag.Overflow, false);
                SetFlag(Flag.Zero, false, isKnown: proven || moved == 0);
                SetFlag(Flag.Sign, false, isKnown: proven);
                SetFlag(Flag.Parity, BitOperations.PopCount(a.Bits & 0xFF) % 2 == 0, isKnown: false);
                return;
            case Mnemonic.Add:
                result = Sum(a, b, size);
                SetFlag(Flag.Carry, result.Bits < a.Bits, whole);
                SetFlag(Flag.Overflow, ((a.Bits ^ result.Bits) & (b.Bits ^ result.Bits) & sign) != 0, whole);
                break;
            case Mnemonic.Sub or Mnemonic.Cmp:
                result = Difference(a, b, size);
                SetFlag(Flag.Carry, a.Bits < b.Bits, whole);
                SetFlag(Flag.Overflow, ((a.Bits ^ b.Bits) & (a.Bits ^ result.Bits) & sign) != 0, whole);
                break;
            default:
                // A bit is known where both operands know it, or where one knows what settles it. The
                // stack is aligned as the game's, so aligning a pointer into it to 16 bytes or less
                // moves it by a known amount.
                result = ins.Mnemonic switch
                {
                    Mnemonic.And when size == 8 && RegionOf(a.Pointer) == Region.Stack && b.IsKnown && b.Bits >= unchecked((ulong)-16) && BitOperations.IsPow2(0UL - b.Bits)
                        => new Value(a.Bits & b.Bits, 0, a.Pointer),
                    Mnemonic.And or Mnemonic.Test => new Value(a.Bits & b.Bits, (a.Known & b.Known) | (a.Known & ~a.Bits) | (b.Known & ~b.Bits)),
                    Mnemonic.Or => new Value(a.Bits | b.Bits, (a.Known & b.Known) | (a.Known & a.Bits) | (b.Known & b.Bits)),
                    _ => new Value(a.Bits ^ b.Bits, a.Known & b.Known),
                };
                SetFlag(Flag.Carry, false);
                SetFlag(Flag.Overflow, false);
                break;
        }
        SetResultFlags(result, size);
        if (ins.Mnemonic is not (Mnemonic.Cmp or Mnemonic.Test)) Put(ins, 0, result);
    }

    private void Unary(in Instruction ins)
    {
        var size = Size(ins, 0);
        var a = Get(ins, 0, size);
        var mask = Mask(size);
        var sign = SignBit(size);
        var whole = (a.Known | ~mask) == ulong.MaxValue;
        Value result;
        switch (ins.Mnemonic)
        {
            case Mnemonic.Not:
                Put(ins, 0, new Value(~a.Bits & mask, a.Known | ~mask));
                return;
            case Mnemonic.Inc:
                result = Sum(a, Constant(1), size);
                SetFlag(Flag.Overflow, result.Bits == sign, whole);
                break;
            case Mnemonic.Dec:
                result = Difference(a, Constant(1), size);
                SetFlag(Flag.Overflow, a.Bits == sign, whole);
                break;
            default:
                result = Difference(Constant(0), a, size);
                SetFlag(Flag.Carry, a.Bits != 0, whole || (a.Bits & a.Known & mask) != 0);
                SetFlag(Flag.Overflow, a.Bits == sign, whole);
                break;
        }
        SetResultFlags(result, size);
        Put(ins, 0, result);
    }

    private void Shift(in Instruction ins)
    {
        var size = Size(ins, 0);
        var bits = size * 8;
        var a = Get(ins, 0, size);
        var by = Get(ins, 1, 1);
        var countMask = size == 8 ? 63UL : 31UL;
        var mask = Mask(size);
        if ((by.Known & countMask) != countMask)
        {
            // By as much as the game decides: every bit, and every flag it may set, is the game's.
            Put(ins, 0, new Value(a.Bits, ~mask));
            known &= ~EveryFlag;
            return;
        }
        var count = (int)(by.Bits & countMask);
        if (count == 0)
        {
            // No flags change, but a 32-bit destination is still written, and so zero-extended.
            Put(ins, 0, a);
            return;
        }
        if (count >= bits) throw new Unmodelled($"a {bits}-bit {ins.Mnemonic} by {count} at 0x{ins.IP:X}");
        var sign = SignBit(size);
        var aKnown = a.Known & mask;
        Value result;
        bool carry;
        bool carryKnown;
        switch (ins.Mnemonic)
        {
            case Mnemonic.Shl or Mnemonic.Sal:
                result = new Value((a.Bits << count) & mask, (((aKnown << count) | ((1UL << count) - 1)) & mask) | ~mask);
                carry = ((a.Bits >> (bits - count)) & 1) != 0;
                carryKnown = ((a.Known >> (bits - count)) & 1) != 0;
                SetFlag(Flag.Overflow, ((result.Bits & sign) != 0) != carry, count == 1 && carryKnown && (result.Known & sign) != 0);
                break;
            case Mnemonic.Shr:
                result = new Value(a.Bits >> count, (aKnown >> count) | ~(mask >> count));
                carry = ((a.Bits >> (count - 1)) & 1) != 0;
                carryKnown = ((a.Known >> (count - 1)) & 1) != 0;
                SetFlag(Flag.Overflow, (a.Bits & sign) != 0, count == 1 && (a.Known & sign) != 0);
                break;
            case Mnemonic.Sar:
                var signed = (long)SignExtend(a.Bits, size);
                result = new Value((ulong)(signed >> count) & mask, ((ulong)((long)SignExtend(aKnown, size) >> count) & mask) | ~mask);
                carry = ((signed >> (count - 1)) & 1) != 0;
                carryKnown = ((a.Known >> (count - 1)) & 1) != 0;
                SetFlag(Flag.Overflow, false, count == 1);
                break;
            case Mnemonic.Rol:
                result = new Value(((a.Bits << count) | (a.Bits >> (bits - count))) & mask, (((aKnown << count) | (aKnown >> (bits - count))) & mask) | ~mask);
                carry = (result.Bits & 1) != 0;
                SetFlag(Flag.Carry, carry, (result.Known & 1) != 0);
                SetFlag(Flag.Overflow, ((result.Bits & sign) != 0) != carry, count == 1 && (result.Known & 1) != 0 && (result.Known & sign) != 0);
                Put(ins, 0, result);
                return;
            default:
                result = new Value(((a.Bits >> count) | (a.Bits << (bits - count))) & mask, (((aKnown >> count) | (aKnown << (bits - count))) & mask) | ~mask);
                carry = (result.Bits & sign) != 0;
                SetFlag(Flag.Carry, carry, (result.Known & sign) != 0);
                SetFlag(Flag.Overflow, carry != ((result.Bits & (sign >> 1)) != 0), count == 1 && (result.Known & sign) != 0 && (result.Known & (sign >> 1)) != 0);
                Put(ins, 0, result);
                return;
        }
        SetFlag(Flag.Carry, carry, carryKnown);
        SetResultFlags(result, size);
        Put(ins, 0, result);
    }

    private void BitTest(in Instruction ins)
    {
        if (ins.GetOpKind(0) == OpKind.Memory && ins.GetOpKind(1) == OpKind.Register)
            throw new Unmodelled($"bt on a bit string in memory at 0x{ins.IP:X}");
        var size = Size(ins, 0);
        var which = Get(ins, 1, size);
        var a = Get(ins, 0, size);
        var bitMask = (ulong)(size * 8 - 1);
        var bit = (int)(which.Bits & bitMask);
        SetFlag(Flag.Carry, ((a.Bits >> bit) & 1) != 0, (which.Known & bitMask) == bitMask && ((a.Known >> bit) & 1) != 0);
        known &= ~(Flag.Overflow | Flag.Sign | Flag.Parity);
    }

    private void Multiply(in Instruction ins)
    {
        var size = Size(ins, 0);
        var three = ins.OpCount == 3;
        var x = Get(ins, three ? 1 : 0, size);
        var y = Get(ins, three ? 2 : 1, size);
        var mask = Mask(size);
        var full = (Int128)(long)SignExtend(x.Bits, size) * (long)SignExtend(y.Bits, size);
        var result = (ulong)full & mask;
        var overflow = full != (long)SignExtend(result, size);
        var whole = ((x.Known & y.Known) | ~mask) == ulong.MaxValue;
        SetFlag(Flag.Carry, overflow, whole);
        SetFlag(Flag.Overflow, overflow, whole);
        known &= ~(Flag.Zero | Flag.Sign | Flag.Parity);
        // A product's low bits come from the factors' bits no higher.
        Put(ins, 0, new Value(result, CarryKnown(x.Known, y.Known, mask)));
    }

    // Whether the condition holds on the flags `readable`; null when it reads one that isn't.
    private bool? Evaluate(ConditionCode condition, Flag readable)
    {
        bool? On(Flag flag) => (readable & flag) != 0 ? (set & flag) != 0 : null;
        return condition switch
        {
            ConditionCode.o => On(Flag.Overflow),
            ConditionCode.no => !On(Flag.Overflow),
            ConditionCode.b => On(Flag.Carry),
            ConditionCode.ae => !On(Flag.Carry),
            ConditionCode.e => On(Flag.Zero),
            ConditionCode.ne => !On(Flag.Zero),
            ConditionCode.be => On(Flag.Carry) | On(Flag.Zero),
            ConditionCode.a => !(On(Flag.Carry) | On(Flag.Zero)),
            ConditionCode.s => On(Flag.Sign),
            ConditionCode.ns => !On(Flag.Sign),
            ConditionCode.p => On(Flag.Parity),
            ConditionCode.np => !On(Flag.Parity),
            ConditionCode.l => On(Flag.Sign) ^ On(Flag.Overflow),
            ConditionCode.ge => !(On(Flag.Sign) ^ On(Flag.Overflow)),
            ConditionCode.le => On(Flag.Zero) | (On(Flag.Sign) ^ On(Flag.Overflow)),
            ConditionCode.g => !(On(Flag.Zero) | (On(Flag.Sign) ^ On(Flag.Overflow))),
            _ => throw new Unmodelled($"condition {condition}"),
        };
    }

    private void SetFlag(Flag flag, bool on, bool isKnown = true)
    {
        if (isKnown) known |= flag;
        else known &= ~flag;
        if (on) set |= flag;
        else set &= ~flag;
    }

    // Zero is known once a known bit is set, whatever the others.
    private void SetResultFlags(Value result, int size)
    {
        var mask = Mask(size);
        var knownBits = result.Known & mask;
        SetFlag(Flag.Zero, (result.Bits & mask) == 0, knownBits == mask || (result.Bits & knownBits) != 0);
        SetFlag(Flag.Sign, (result.Bits & SignBit(size)) != 0, (result.Known & SignBit(size)) != 0);
        SetFlag(Flag.Parity, BitOperations.PopCount(result.Bits & 0xFF) % 2 == 0, (result.Known & 0xFF) == 0xFF);
    }

    // True when the pointer is proven valid. The receiver, its vtable, the stack, the return address and
    // the module are valid in any call; the holder and the fields read from it once the dispatch has
    // read through them, and a test before that splits the run. Not a vtable entry: where the vtable
    // ends is the game's.
    private bool TestedForNull(Value pointer, out long moved)
    {
        var region = RegionOf(pointer.Pointer);
        moved = region == Region.Module ? 0 : (long)(pointer.Bits - (fake + RegionAt(region)));
        if (region == Region.Stubs || moved <= -PointerSlack || moved >= PointerSlack || region == Region.Module && !InModule(pointer.Bits))
            throw new Unmodelled($"tests 0x{pointer.Bits:X} for null, which the game may hold null");
        if (region is not (Region.Holder or Region.Channel or Region.Packet) || readThrough.Contains(pointer.Pointer)) return true;
        if (Array.IndexOf(heldValid, pointer.Pointer) < 0 && !splits.Contains(pointer.Pointer)) splits.Add(pointer.Pointer);
        return false;
    }

    // An operand's size in bytes; 0 for one with none of its own (an immediate, a lea's address).
    private static int Size(in Instruction ins, int operand) => ins.GetOpKind(operand) switch
    {
        OpKind.Register => ins.GetOpRegister(operand).GetSize(),
        OpKind.Memory => ins.MemorySize.GetSize(),
        _ => 0,
    };

    private static bool SameRegister(in Instruction ins)
        => ins.OpCount == 2 && ins.GetOpKind(0) == OpKind.Register && ins.GetOpKind(1) == OpKind.Register && ins.GetOpRegister(0) == ins.GetOpRegister(1);

    private Value Get(in Instruction ins, int operand, int size)
    {
        switch (ins.GetOpKind(operand))
        {
            case OpKind.Register:
                return GetRegister(ins.GetOpRegister(operand));
            case OpKind.Memory:
                return Load(Address(ins), ins.MemorySize.GetSize());
            case OpKind.Immediate8 or OpKind.Immediate16 or OpKind.Immediate32 or OpKind.Immediate64
                or OpKind.Immediate8to16 or OpKind.Immediate8to32 or OpKind.Immediate8to64 or OpKind.Immediate32to64:
                var value = ins.GetImmediate(operand) & Mask(size == 0 ? 8 : size);
                return size == 8 && InModule(value) ? ModuleAddress(value) : Constant(value);
            default:
                throw new Unmodelled($"operand {ins.GetOpKind(operand)} at 0x{ins.IP:X}");
        }
    }

    private void Put(in Instruction ins, int operand, Value value)
    {
        switch (ins.GetOpKind(operand))
        {
            case OpKind.Register:
                SetRegister(ins.GetOpRegister(operand), value);
                return;
            case OpKind.Memory:
                Store(Address(ins), ins.MemorySize.GetSize(), value);
                return;
            default:
                throw new Unmodelled($"writes {ins.GetOpKind(operand)} at 0x{ins.IP:X}");
        }
    }

    private Value Address(in Instruction ins)
    {
        if (ins.SegmentPrefix is Register.FS or Register.GS) throw new Unmodelled($"a {ins.SegmentPrefix}-relative access at 0x{ins.IP:X}");
        if (ins.IsIPRelativeMemoryOperand) return ModuleAddress(ins.IPRelativeMemoryAddress);
        var displacement = ins.MemoryDisplacement64;
        if (ins.MemoryBase == Register.None && ins.MemoryIndex == Register.None)
            return InModule(displacement) ? ModuleAddress(displacement) : Constant(displacement);
        var address = Constant(displacement);
        if (ins.MemoryBase != Register.None) address = Sum(address, AddressRegister(ins, ins.MemoryBase), 8);
        if (ins.MemoryIndex != Register.None) address = Sum(address, Scaled(AddressRegister(ins, ins.MemoryIndex), ins.MemoryIndexScale), 8);
        return address;
    }

    private Value AddressRegister(in Instruction ins, Register register)
        => register.IsGPR64() ? GetRegister(register) : throw new Unmodelled($"an address through {register} at 0x{ins.IP:X}");

    private static Value Scaled(Value index, int scale)
    {
        if (scale == 1) return index;
        var shift = BitOperations.TrailingZeroCount(scale);
        return new Value(index.Bits << shift, (index.Known << shift) | ((1UL << shift) - 1));
    }

    // A pointer moved by a known amount is still that pointer, but a method's: moved, it's somewhere in a
    // method's code, not at one. A pointer in anything else is unknown.
    private static Value Sum(Value a, Value b, int size)
    {
        var mask = Mask(size);
        var bits = (a.Bits + b.Bits) & mask;
        if (size == 8 && Moves(a, b)) return new Value(bits, 0, a.Pointer);
        if (size == 8 && Moves(b, a)) return new Value(bits, 0, b.Pointer);
        return new Value(bits, CarryKnown(a.Known, b.Known, mask));
    }

    private static Value Difference(Value a, Value b, int size)
    {
        var mask = Mask(size);
        var bits = (a.Bits - b.Bits) & mask;
        if (size == 8 && Moves(a, b)) return new Value(bits, 0, a.Pointer);
        return new Value(bits, CarryKnown(a.Known, b.Known, mask));
    }

    private static bool Moves(Value pointer, Value by)
        => pointer.Pointer != 0 && by.Pointer == 0 && by.IsKnown && (RegionOf(pointer.Pointer) != Region.Stubs || by.Bits == 0);

    // A carry takes what an operand doesn't know upwards: a sum knows only the bits below the lowest
    // one either operand doesn't.
    private static ulong CarryKnown(ulong a, ulong b, ulong mask)
    {
        var unknown = ~(a & b) & mask;
        return unknown == 0 ? ulong.MaxValue : ((1UL << BitOperations.TrailingZeroCount(unknown)) - 1) | ~mask;
    }

    private static Value SignExtend(Value value, int size)
    {
        if (size >= 8) return value;
        var mask = Mask(size);
        return new Value(SignExtend(value.Bits, size), (value.Known & SignBit(size)) != 0 ? value.Known | ~mask : value.Known & mask);
    }

    // cwd, cdq, cqo: every bit a copy of the source's sign.
    private static Value SignFill(Value value, int size)
    {
        var sign = SignBit(size);
        var mask = Mask(size);
        return new Value((value.Bits & sign) != 0 ? mask : 0, (value.Known & sign) != 0 ? ulong.MaxValue : ~mask);
    }

    private static Value Constant(ulong bits) => new(bits, ulong.MaxValue);

    private Value Unknown(int size) => new(fill & Mask(size), ~Mask(size));

    // A pointer names its stand-in and, for a channel or a packet, the fields it was read through.
    private static int PointerTo(Region region, int path = 0) => (int)region | path << 4;

    private static Region RegionOf(int pointer) => (Region)(pointer & 0xF);

    private static int PathOf(int pointer) => pointer >> 4;

    private Value PointerInto(Region region, ulong offset = 0, int path = 0) => new(fake + RegionAt(region) + offset, 0, PointerTo(region, path));

    // The holder, or a field read from it, as this run's world holds it.
    private Value Produce(Region region, int path = 0)
        => Array.IndexOf(heldNull, PointerTo(region, path)) >= 0 ? Constant(0) : PointerInto(region, path: path);

    private bool InModule(ulong address) => address >= moduleStart && address < moduleEnd;

    private static Value ModuleAddress(ulong address) => new(address, 0, PointerTo(Region.Module));

    private static string Name(int pointer) => RegionOf(pointer) switch
    {
        Region.Holder => "the holder",
        Region.Channel => $"holder field +0x{PathOf(pointer):X}",
        _ => $"field +0x{PathOf(pointer) % ObjectSize:X} of holder field +0x{PathOf(pointer) / ObjectSize:X}",
    };

    private static ulong RegionAt(Region region) => region switch
    {
        Region.Receiver => ReceiverAt,
        Region.Vtable => VtableAt,
        Region.Stubs => StubsAt,
        Region.Holder => HolderAt,
        Region.Channel => ChannelAt,
        Region.Packet => PacketAt,
        Region.Stack => StackAt,
        _ => ReturnAt,
    };

    private static int Slot(Register register) => register.GetFullRegister() - Register.RAX;

    private Value GetRegister(Register register)
    {
        if (!register.IsGPR()) throw new Unmodelled($"register {register}");
        var slot = Slot(register);
        if (register is Register.AH or Register.CH or Register.DH or Register.BH)
            return new Value((gpr[slot] >> 8) & 0xFF, ((gprKnown[slot] >> 8) & 0xFF) | ~0xFFUL);
        var size = register.GetSize();
        if (size == 8) return new Value(gpr[slot], gprKnown[slot], gprPointer[slot]);
        var mask = Mask(size);
        return new Value(gpr[slot] & mask, (gprKnown[slot] & mask) | ~mask);
    }

    private void SetRegister(Register register, Value value)
    {
        if (!register.IsGPR()) throw new Unmodelled($"register {register}");
        var slot = Slot(register);
        gprPointer[slot] = 0;
        if (register is Register.AH or Register.CH or Register.DH or Register.BH)
        {
            gpr[slot] = (gpr[slot] & ~0xFF00UL) | ((value.Bits & 0xFF) << 8);
            gprKnown[slot] = (gprKnown[slot] & ~0xFF00UL) | ((value.Known & 0xFF) << 8);
            return;
        }
        switch (register.GetSize())
        {
            case 8:
                gpr[slot] = value.Bits;
                gprKnown[slot] = value.Known;
                gprPointer[slot] = value.Pointer;
                return;
            case 4:
                gpr[slot] = value.Bits & 0xFFFF_FFFF;
                gprKnown[slot] = value.Known | 0xFFFF_FFFF_0000_0000;
                return;
            default:
                var mask = Mask(register.GetSize());
                gpr[slot] = (gpr[slot] & ~mask) | (value.Bits & mask);
                gprKnown[slot] = (gprKnown[slot] & ~mask) | (value.Known & mask);
                return;
        }
    }

    private void Push(Value value)
    {
        var rsp = Difference(GetRegister(Register.RSP), Constant(8), 8);
        SetRegister(Register.RSP, rsp);
        Store(rsp, 8, value);
    }

    private Value Pop()
    {
        var rsp = GetRegister(Register.RSP);
        var value = Load(rsp, 8);
        SetRegister(Register.RSP, Sum(rsp, Constant(8), 8));
        return value;
    }

    private Value Load(Value address, int size)
    {
        if (size is not (1 or 2 or 4 or 8)) throw new Unmodelled($"a {size}-byte read at 0x{address.Bits:X}");
        var region = RegionOf(address.Pointer);
        if (region == Region.None)
        {
            // From an address the game decides, a value the game decides.
            if (!address.IsKnown) return Unknown(size);
            if (address.Bits < NullPage) throw new Faults();
            throw new Unmodelled($"reads 0x{address.Bits:X}, which isn't the module's");
        }
        if (region == Region.Module)
        {
            if (image.SectionHolding(address.Bits, size) is not { } section) throw new Unmodelled($"reads 0x{address.Bits:X}, outside what it models");
            if (section.Writable) throw new Unmodelled($"reads {section.Name} at 0x{address.Bits:X}, which the game changes as it runs");
            var value = Little(section.Bytes.Slice((int)(address.Bits - section.Address), size));
            if (size == 8 && InModule(value)) return ModuleAddress(value);
            // Part of an address the loader moves is where the module is.
            var knownBits = ulong.MaxValue;
            for (var cell = address.Bits & ~7UL; cell < address.Bits + (ulong)size; cell += 8)
            {
                if (image.SectionHolding(cell, 8) is not { } holding || !InModule(Little(holding.Bytes.Slice((int)(cell - holding.Address), 8)))) continue;
                for (var i = 0; i < size; i++)
                    if (address.Bits + (ulong)i >= cell && address.Bits + (ulong)i < cell + 8) knownBits &= ~(0xFFUL << (8 * i));
            }
            return new Value(value, knownBits);
        }
        var at = address.Bits - (fake + RegionAt(region));
        var qword = size == 8 && at % 8 == 0;
        // A method the dispatch called may have changed what the objects hold, the receiver's vtable
        // pointer too.
        var changed = reached >= 0;
        switch (region)
        {
            case Region.Receiver when Inside(at, size, ObjectSize):
                // Its vtable pointer; its members are the game's.
                return at == 0 && size == 8 && !changed ? PointerInto(Region.Vtable) : Unknown(size);
            case Region.Vtable when Inside(at, size, MaxSlots * 8):
                if (!qword) throw new Unmodelled($"a {size}-byte read inside the receiver's vtable at +0x{at:X}");
                return PointerInto(Region.Stubs, at / 8 * StubSize);
            case Region.Holder when Inside(at, size, ObjectSize):
                readThrough.Add(address.Pointer);
                return qword && !changed ? Produce(Region.Channel, (int)at) : Unknown(size);
            case Region.Channel when Inside(at, size, ObjectSize):
                readThrough.Add(address.Pointer);
                return qword && !changed ? Produce(Region.Packet, PathOf(address.Pointer) * ObjectSize + (int)at) : Unknown(size);
            case Region.Packet when Inside(at, size, PacketSize):
                readThrough.Add(address.Pointer);
                return changed ? Unknown(size) : ReadPacket(PathOf(address.Pointer), (int)at, size);
            case Region.Stack when Inside(at, size, StackSize):
                return ReadStack((int)at, size);
        }
        throw new Unmodelled($"reads 0x{address.Bits:X}, outside what it models");
    }

    // In the game only one of the packets the stand-ins lead to is the packet: the first whose header
    // the dispatch reads. The rest of any of them is the game's.
    private Value ReadPacket(int path, int at, int size)
    {
        if (packetPath < 0 && at < 4) packetPath = path;
        if (path != packetPath) return Unknown(size);
        var knownBits = 0UL;
        for (var i = 0; i < size && at + i < 4; i++) knownBits |= 0xFFUL << (8 * i);
        return new Value(Little(packet.AsSpan(at, size)), knownBits | ~Mask(size));
    }

    private Value ReadStack(int at, int size)
    {
        var bits = 0UL;
        var knownBits = 0UL;
        for (var i = size - 1; i >= 0; i--)
        {
            bits = (bits << 8) | stack[at + i];
            knownBits = (knownBits << 8) | stackKnown[at + i];
        }
        var pointer = stackPointer[at];
        if (size == 8 && pointer != 0)
        {
            var whole = true;
            for (var i = 0; i < 8; i++) whole &= stackPointer[at + i] == pointer && stackPart[at + i] == i;
            if (whole) return new Value(bits, 0, pointer);
        }
        return new Value(bits, knownBits | ~Mask(size));
    }

    private void Store(Value address, int size, Value value)
    {
        if (address is { Pointer: 0, IsKnown: true } && address.Bits < NullPage) throw new Faults();
        var at = address.Bits - (fake + StackAt);
        if (RegionOf(address.Pointer) != Region.Stack)
            throw new Unmodelled(address.Pointer != 0
                ? $"writes {size} bytes into the {RegionOf(address.Pointer).ToString().ToLowerInvariant()}"
                : $"writes {size} bytes at 0x{address.Bits:X}, not through its stack pointer");
        if (size is not (1 or 2 or 4 or 8) || !Inside(at, size, StackSize))
            throw new Unmodelled($"writes {size} bytes at 0x{address.Bits:X}, outside its own stack");
        var offset = (int)at;
        stackWrittenFrom = Math.Min(stackWrittenFrom, offset);
        var pointer = size == 8 ? value.Pointer : 0;
        for (var i = 0; i < size; i++)
        {
            stack[offset + i] = (byte)(value.Bits >> (8 * i));
            stackKnown[offset + i] = (byte)(value.Known >> (8 * i));
            stackPointer[offset + i] = pointer;
            stackPart[offset + i] = (byte)i;
        }
    }

    private static bool Inside(ulong at, int size, int regionSize) => at <= (ulong)(regionSize - size);

    private static ulong Little(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        for (var i = bytes.Length - 1; i >= 0; i--) value = (value << 8) | bytes[i];
        return value;
    }

    private static ulong Mask(int size) => size >= 8 ? ulong.MaxValue : (1UL << (size * 8)) - 1;

    private static ulong SignBit(int size) => 1UL << (size * 8 - 1);

    private static ulong SignExtend(ulong value, int size)
        => size >= 8 ? value : (value & SignBit(size)) != 0 ? value | ~Mask(size) : value & Mask(size);
}

// Where the packets the party watch reads sit among the receiver's packet methods (ProtocolReceiver
// .MethodOf), per protocol revision (ProtocolReceiver.Methods). PartyList is the party of one world
// (UpdateParty), PartyKey the packet the dispatcher keys its decoding of it with. PartyView: every
// method whose packet changes what the client knows of the party, broad on purpose.
internal sealed record PartyPacketMethods(
    int PlayerStateFlags,
    int OnlineStatus,
    int[] ActorControl,
    int Notice,
    int Roster,
    int MemberEvent,
    int MemberLeft,
    int MemberRemoved,
    int Disbanded,
    int MemberStatus,
    int PartyList,
    int PartyKey,
    int PartyEvent,
    int CrossWorldReadyCheck,
    int CrossWorldReadyCheckAnswer,
    int[] PartyView)
{
    public static readonly IReadOnlyDictionary<int, PartyPacketMethods> ByRevision = new Dictionary<int, PartyPacketMethods>
    {
        [775] = new(PlayerStateFlags: 298, OnlineStatus: 145, ActorControl: [228, 229, 230], Notice: 38, Roster: 37,
            MemberEvent: 25, MemberLeft: 29, MemberRemoved: 30, Disbanded: 22, MemberStatus: 46, PartyList: 284, PartyKey: 286,
            PartyEvent: 156, CrossWorldReadyCheck: 33, CrossWorldReadyCheckAnswer: 34,
            PartyView: [.. Enumerable.Range(21, 11), .. Enumerable.Range(35, 8), .. Enumerable.Range(44, 6), 88, 117, 135, 284]),
    };

    // Each packet's opcode on this build, 0 (or left out) where no opcode reaches its method; null for
    // a revision this doesn't know.
    public static PartyOpcodes? For(ProtocolReceiver receiver)
    {
        if (!ByRevision.TryGetValue(receiver.Methods, out var methods)) return null;
        return new PartyOpcodes(
            receiver.OpcodeOf(methods.PlayerStateFlags),
            receiver.OpcodeOf(methods.OnlineStatus),
            [.. methods.ActorControl.Select(receiver.OpcodeOf).Where(opcode => opcode != 0)],
            receiver.OpcodeOf(methods.Notice),
            receiver.OpcodeOf(methods.Roster),
            receiver.OpcodeOf(methods.MemberEvent),
            receiver.OpcodeOf(methods.MemberLeft),
            receiver.OpcodeOf(methods.MemberRemoved),
            receiver.OpcodeOf(methods.Disbanded),
            receiver.OpcodeOf(methods.MemberStatus),
            receiver.OpcodeOf(methods.PartyList),
            receiver.OpcodeOf(methods.PartyKey),
            receiver.OpcodeOf(methods.PartyEvent),
            receiver.OpcodeOf(methods.CrossWorldReadyCheck),
            receiver.OpcodeOf(methods.CrossWorldReadyCheckAnswer),
            [.. methods.PartyView.Select(receiver.OpcodeOf).Where(opcode => opcode != 0)]);
    }
}

internal sealed record PartyOpcodes(
    ushort PlayerStateFlags,
    ushort OnlineStatus,
    ushort[] ActorControl,
    ushort Notice,
    ushort Roster,
    ushort MemberEvent,
    ushort MemberLeft,
    ushort MemberRemoved,
    ushort Disbanded,
    ushort MemberStatus,
    ushort PartyList,
    ushort PartyKey,
    ushort PartyEvent,
    ushort CrossWorldReadyCheck,
    ushort CrossWorldReadyCheckAnswer,
    ushort[] PartyView);
