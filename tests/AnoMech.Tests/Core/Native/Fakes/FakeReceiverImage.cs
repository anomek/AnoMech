using AnoMech.Core.Native.Implementations;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace AnoMech.Tests;

// The client's generated zone-packet receiver as a module image: a dispatch that reads the packet's
// opcode through the holder in r8 and hands the packet to one method of the receiver in rcx, and the
// receiver's vtable: a function, the dispatch, then the packet methods, empty. Methods are given by
// their place among the packet methods, from 0. How the dispatch's switch is encoded, and what else
// the vtable holds, is the test's choice, as it is the compiler's.
internal sealed class FakeReceiverImage
{
    public const ulong ImageBase = 0x1_4000_0000;
    public const ulong Text = ImageBase + 0x1000;
    public const ulong ReadOnlyData = ImageBase + 0x60000;
    public const ulong WritableData = ImageBase + 0x80000;
    private const int TextSize = 0x5F000;
    private const int ReadOnlySize = 0x20000;
    private const int WritableSize = 0x1000;
    private const ulong StubsAt = Text + 0x50000;

    // After the method returns into a case: another method, on a flag in the receiver, on what the
    // method returns or writes back to the case's own frame, on flags or home space the case set before
    // the call, on the packet as the method leaves it, or always; a case jumping to its method's
    // address moved past the method before it.
    public enum CaseForm
    {
        TailJump, Call, LoadThenJump, CallThenAnotherOnAReceiverFlag, CallThenAnotherOnItsResult, CallThenAnotherOnAnOutParameter,
        CallThenAnotherOnFlagsSetBefore, CallThenAnotherOnItsHomeSpace, CallThenAnotherOnThePacket, CallThenJumpToAnother,
        CallThenJumpToTheNextThroughTheVtableKept, JumpReturningIntoTheCase, JumpPastTheMethodBefore,
    }

    // Where the vtable has more than a function before the dispatch, or a function between the
    // dispatch and the packet methods; how its empty methods are encoded; and how far into the
    // constant data it starts.
    public sealed record Layout(int VirtualsBeforeDispatch = 1, bool VirtualAfterDispatch = false, byte[]? EmptyMethod = null, int VtableAt = 0)
    {
        public int FirstMethodSlot => VirtualsBeforeDispatch + 1 + (VirtualAfterDispatch ? 1 : 0);
    }

    private readonly byte[] text = new byte[TextSize];
    private readonly byte[] readOnly = new byte[ReadOnlySize];
    private readonly byte[] writable = new byte[WritableSize];
    private readonly Layout layout;
    // Where the image holds an absolute address, which the loader moves with the module.
    private readonly HashSet<ulong> pointers = [];
    private ulong nextCode = Text;
    private ulong nextData;

    // A vtable with that many empty packet methods.
    public FakeReceiverImage(int methods, Layout? layout = null)
    {
        this.layout = layout ?? new Layout();
        var function = Assemble(a =>
        {
            a.mov(eax, 1);
            a.ret();
        }).Start;
        for (var i = 0; i < this.layout.VirtualsBeforeDispatch; i++) WritePointer(Slot(i), function);
        if (this.layout.VirtualAfterDispatch) WritePointer(Slot(this.layout.VirtualsBeforeDispatch + 1), function);
        var empty = this.layout.EmptyMethod ?? [0xC2, 0x00, 0x00];
        for (var i = 0; i < methods; i++)
        {
            var stub = StubsAt + (ulong)(4 * i);
            Write(stub, empty);
            WritePointer(Slot(this.layout.FirstMethodSlot + i), stub);
        }
        nextData = Slot(this.layout.FirstMethodSlot + methods + 1);
    }

    public int FirstMethodSlot => layout.FirstMethodSlot;

    public static ulong Rva(ulong address) => address - ImageBase;

    private ulong Slot(int index) => ReadOnlyData + (ulong)layout.VtableAt + 8 * (ulong)index;

    public void SetDispatch(ulong dispatch) => WritePointer(Slot(layout.VirtualsBeforeDispatch), dispatch);

    private void WritePointer(ulong at, ulong target)
    {
        pointers.Add(at);
        Write(at, BitConverter.GetBytes(target));
    }

    public ModuleImage Image() => new([
        new ArraySection(".text", Text, text, code: true, writable: false),
        new ArraySection(".rdata", ReadOnlyData, readOnly, code: false, writable: false),
        new ArraySection(".data", WritableData, writable, code: false, writable: true),
    ]);

    // Another vtable for a class deriving from the receiver: the same dispatch, with one packet method
    // overridden by a function that does something, or an empty virtual of its own after the packet
    // methods. After the receiver's, or `at`.
    public void AddDerivedVtable(int methods, int? overridden = null, bool emptyVirtualAfter = false, ulong? at = null)
    {
        var function = Assemble(a =>
        {
            a.mov(eax, 2);
            a.ret();
        }).Start;
        var vtable = at ?? Reserve(8 * (FirstMethodSlot + methods + 3));
        for (var slot = 0; slot < FirstMethodSlot + methods; slot++)
        {
            var entry = BitConverter.ToUInt64(readOnly, (int)(Slot(slot) - ReadOnlyData));
            if (slot == FirstMethodSlot + overridden) entry = function;
            WritePointer(vtable + 8 * (ulong)slot, entry);
        }
        if (!emptyVirtualAfter) return;
        WritePointer(vtable + 8 * (ulong)(FirstMethodSlot + methods), Assemble(a => a.ret()).Start);
        WritePointer(vtable + 8 * (ulong)(FirstMethodSlot + methods + 1), function);
    }

    // Another generated receiver in the module, its vtable after this one's: a function, its own
    // dispatch, then its packet methods.
    public void AddReceiver(IReadOnlyDictionary<ushort, int> places)
    {
        var methods = places.Values.Max() + 1;
        var empty = layout.EmptyMethod ?? [0xC2, 0x00, 0x00];
        for (var i = 0; i < methods; i++) Write(StubsAt + (ulong)(4 * i), empty);
        var function = Assemble(a =>
        {
            a.mov(eax, 3);
            a.ret();
        }).Start;
        var vtable = Reserve(8 * (FirstMethodSlot + methods + 1));
        for (var i = 0; i < layout.VirtualsBeforeDispatch; i++) WritePointer(vtable + 8 * (ulong)i, function);
        for (var i = 0; i < methods; i++) WritePointer(vtable + 8 * (ulong)(FirstMethodSlot + i), StubsAt + (ulong)(4 * i));
        WritePointer(vtable + 8 * (ulong)layout.VirtualsBeforeDispatch, AssembleJumpTable(places, CaseForm.TailJump, 0x10, 0x38, null));
    }

    public static readonly (uint TimeStamp, uint ImageSize) ExeIdentity = (0x6AE8C80E, 0x90000);

    // The image as the loader maps it: each section at its address relative to the base, and every
    // absolute address moved with it.
    public void LoadAt(nint moduleBase, byte[] exeFile)
    {
        System.Runtime.InteropServices.Marshal.Copy(new byte[ExeIdentity.ImageSize], 0, moduleBase, (int)ExeIdentity.ImageSize);
        System.Runtime.InteropServices.Marshal.Copy(exeFile, 0, moduleBase, 0x400);
        System.Runtime.InteropServices.Marshal.Copy(text, 0, moduleBase + (nint)Rva(Text), text.Length);
        System.Runtime.InteropServices.Marshal.Copy(readOnly, 0, moduleBase + (nint)Rva(ReadOnlyData), readOnly.Length);
        System.Runtime.InteropServices.Marshal.Copy(writable, 0, moduleBase + (nint)Rva(WritableData), writable.Length);
        foreach (var at in pointers)
        {
            var field = moduleBase + (nint)Rva(at);
            System.Runtime.InteropServices.Marshal.WriteInt64(field, System.Runtime.InteropServices.Marshal.ReadInt64(field) + ((long)moduleBase - (long)ImageBase));
        }
    }

    // The image as an exe file: its PE headers, then each section's bytes at a file offset of its own.
    public byte[] ExeFile(uint? timeStamp = null, uint? imageSize = null)
    {
        (string Name, ulong Address, byte[] Bytes, uint Characteristics)[] sections =
        [
            (".text", Text, text, 0x60000020),
            (".rdata", ReadOnlyData, readOnly, 0x40000040),
            (".data", WritableData, writable, 0xC0000040),
        ];
        const int NtHeaders = 0x80;
        const int OptionalSize = 0xF0;
        const int RawStart = 0x400;
        var file = new byte[RawStart + sections.Sum(s => s.Bytes.Length)];
        void Put(int at, ReadOnlySpan<byte> bytes) => bytes.CopyTo(file.AsSpan(at));
        Put(0, "MZ"u8);
        Put(0x3C, BitConverter.GetBytes(NtHeaders));
        Put(NtHeaders, "PE\0\0"u8);
        Put(NtHeaders + 4, BitConverter.GetBytes((ushort)0x8664));
        Put(NtHeaders + 6, BitConverter.GetBytes((ushort)sections.Length));
        Put(NtHeaders + 8, BitConverter.GetBytes(timeStamp ?? ExeIdentity.TimeStamp));
        Put(NtHeaders + 20, BitConverter.GetBytes((ushort)OptionalSize));
        Put(NtHeaders + 24, BitConverter.GetBytes((ushort)0x20B));
        Put(NtHeaders + 24 + 24, BitConverter.GetBytes(ImageBase));
        Put(NtHeaders + 24 + 56, BitConverter.GetBytes(imageSize ?? ExeIdentity.ImageSize));
        var raw = RawStart;
        for (var i = 0; i < sections.Length; i++)
        {
            var header = NtHeaders + 24 + OptionalSize + 40 * i;
            var (name, address, bytes, characteristics) = sections[i];
            Put(header, System.Text.Encoding.ASCII.GetBytes(name));
            Put(header + 8, BitConverter.GetBytes((uint)bytes.Length));
            Put(header + 12, BitConverter.GetBytes((uint)Rva(address)));
            Put(header + 16, BitConverter.GetBytes((uint)bytes.Length));
            Put(header + 20, BitConverter.GetBytes((uint)raw));
            Put(header + 36, BitConverter.GetBytes(characteristics));
            Put(raw, bytes);
            raw += bytes.Length;
        }
        return file;
    }

    public ulong Reserve(int bytes)
    {
        var at = nextData;
        nextData += (ulong)((bytes + 15) & ~15);
        return at;
    }

    public (ulong Start, Func<Label, ulong> RipOf) Assemble(Action<Assembler> emit)
    {
        var assembler = new Assembler(64);
        emit(assembler);
        var start = nextCode;
        var writer = new ByteWriter();
        var result = assembler.Assemble(writer, start, BlockEncoderOptions.ReturnNewInstructionOffsets);
        Write(start, [.. writer.Bytes]);
        nextCode = (start + (ulong)writer.Bytes.Count + 15) & ~15UL;
        return (start, label => result.GetLabelRIP(label));
    }

    public void Write(ulong address, ReadOnlySpan<byte> bytes)
    {
        var (section, at) = address switch
        {
            >= WritableData => (writable, address - WritableData),
            >= ReadOnlyData => (readOnly, address - ReadOnlyData),
            _ => (text, address - Text),
        };
        bytes.CopyTo(section.AsSpan((int)at));
    }

    // The packet holder in r8 points at a channel, which points at the packet; the opcode is at +2.
    public static void ReadOpcode(Assembler a, AssemblerRegister32 into, int holderField = 0x10, int channelField = 0x38)
    {
        a.mov(rax, __qword_ptr[r8 + holderField]);
        a.mov(r10, __qword_ptr[rax + channelField]);
        a.movzx(into, __word_ptr[r10 + 2]);
    }

    // lea into, [image base]: RIP-relative, as the client's code reaches its own tables.
    public static void LoadImageBase(Assembler a, Register into)
        => a.AddInstruction(Instruction.Create(Iced.Intel.Code.Lea_r64_m, into, new MemoryOperand(Register.RIP, (long)ImageBase)));

    // The dispatch as the client has it: rebase the opcode, check the range, jump through a table of
    // case offsets from the image base. `methods`: the vtable's packet methods, by default as many as
    // the places reached need. `beforeSwitch` runs with the opcode in eax and the packet in r10, and
    // may jump to the case of a place.
    public static FakeReceiverImage JumpTable(IReadOnlyDictionary<ushort, int> places, CaseForm form = CaseForm.TailJump, int holderField = 0x10, int channelField = 0x38,
        int? methods = null, Layout? layout = null, Action<Assembler, Func<int, Label>>? beforeSwitch = null)
    {
        var image = new FakeReceiverImage(methods ?? places.Values.Max() + 1, layout);
        image.SetDispatch(image.AssembleJumpTable(places, form, holderField, channelField, beforeSwitch));
        return image;
    }

    private ulong AssembleJumpTable(IReadOnlyDictionary<ushort, int> places, CaseForm form, int holderField, int channelField,
        Action<Assembler, Func<int, Label>>? beforeSwitch)
    {
        var first = places.Keys.Min();
        var last = places.Keys.Max();
        var table = Reserve(4 * (last - first + 1));
        var cases = new Dictionary<int, Label>();
        var fallback = default(Label);
        var (dispatch, ripOf) = Assemble(a =>
        {
            foreach (var place in places.Values.Distinct()) cases[place] = a.CreateLabel();
            fallback = a.CreateLabel();
            ReadOpcode(a, eax, holderField, channelField);
            beforeSwitch?.Invoke(a, place => cases[place]);
            a.add(eax, -first);
            a.cmp(eax, last - first);
            a.ja(fallback);
            LoadImageBase(a, Register.R11);
            a.cdqe();
            a.mov(r9d, __dword_ptr[r11 + rax * 4 + (int)Rva(table)]);
            a.add(r9, r11);
            a.jmp(r9);
            Cases(a, cases, form);
            a.Label(ref fallback);
            a.ret();
        });
        for (var opcode = first; opcode <= last; opcode++)
        {
            var target = places.TryGetValue(opcode, out var place) ? ripOf(cases[place]) : ripOf(fallback);
            Write(table + 4 * (ulong)(opcode - first), BitConverter.GetBytes((uint)Rva(target)));
        }
        return dispatch;
    }

    // The client's party handler's shape: one opcode on a lone compare, those below it through a byte
    // table of case numbers and a table of case offsets, those above through a tree of compares.
    public static FakeReceiverImage ByteTableAndCompares(IReadOnlyDictionary<ushort, int> places)
    {
        var image = new FakeReceiverImage(places.Values.Max() + 1);
        var sorted = places.Keys.Order().ToArray();
        var low = sorted[..200];
        var lone = sorted[200];
        var high = sorted[201..];
        var lowFirst = low[0];
        var lowLast = low[^1];
        var numbers = image.Reserve(lowLast - lowFirst + 1);
        var offsets = image.Reserve(4 * (low.Length + 1));
        var cases = new Dictionary<int, Label>();
        var fallback = default(Label);
        var (dispatch, ripOf) = image.Assemble(a =>
        {
            foreach (var place in places.Values.Distinct()) cases[place] = a.CreateLabel();
            fallback = a.CreateLabel();
            var upper = a.CreateLabel();
            ReadOpcode(a, eax);
            a.cmp(eax, lone);
            a.ja(upper);
            a.je(cases[places[lone]]);
            a.sub(eax, lowFirst);
            a.cmp(eax, lowLast - lowFirst);
            a.ja(fallback);
            LoadImageBase(a, Register.RDX);
            a.cdqe();
            a.movzx(eax, __byte_ptr[rdx + rax + (int)Rva(numbers)]);
            a.mov(r9d, __dword_ptr[rdx + rax * 4 + (int)Rva(offsets)]);
            a.add(r9, rdx);
            a.jmp(r9);
            a.Label(ref upper);
            Tree(a, high, places, cases, fallback);
            image.Cases(a, cases, CaseForm.TailJump);
            a.Label(ref fallback);
            a.ret();
        });
        image.Write(offsets, BitConverter.GetBytes((uint)Rva(ripOf(fallback))));
        for (var i = 0; i < low.Length; i++)
        {
            image.Write(numbers + (ulong)(low[i] - lowFirst), [(byte)(i + 1)]);
            image.Write(offsets + 4 * (ulong)(i + 1), BitConverter.GetBytes((uint)Rva(ripOf(cases[places[low[i]]]))));
        }
        image.SetDispatch(dispatch);
        return image;
    }

    // Every opcode through a binary tree of compares.
    public static FakeReceiverImage CompareTree(IReadOnlyDictionary<ushort, int> places)
    {
        var image = new FakeReceiverImage(places.Values.Max() + 1);
        var cases = new Dictionary<int, Label>();
        var (dispatch, _) = image.Assemble(a =>
        {
            foreach (var place in places.Values.Distinct()) cases[place] = a.CreateLabel();
            var fallback = a.CreateLabel();
            ReadOpcode(a, eax);
            Tree(a, places.Keys.Order().ToArray(), places, cases, fallback);
            image.Cases(a, cases, CaseForm.TailJump);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // A dispatch on its own stack frame: it saves a register and the receiver, and restores both.
    public static FakeReceiverImage WithStackFrame(IReadOnlyDictionary<ushort, int> places)
    {
        var image = new FakeReceiverImage(places.Values.Max() + 1);
        var cases = new Dictionary<int, Label>();
        var (dispatch, _) = image.Assemble(a =>
        {
            foreach (var place in places.Values.Distinct()) cases[place] = a.CreateLabel();
            var fallback = a.CreateLabel();
            a.push(rbx);
            a.sub(rsp, 0x20);
            a.mov(__qword_ptr[rsp + 0x18], rcx);
            a.mov(rbx, r8);
            a.xor(ecx, ecx);
            a.mov(r8, rbx);
            ReadOpcode(a, eax);
            a.mov(rcx, __qword_ptr[rsp + 0x18]);
            a.add(rsp, 0x20);
            a.pop(rbx);
            Tree(a, places.Keys.Order().ToArray(), places, cases, fallback);
            image.Cases(a, cases, CaseForm.TailJump);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // Opcode First reaches the first packet method and Last the last; opcodes from `start` on, `count`
    // of them, reach the methods after the first in order, or `count` further on when `predicate`
    // jumps to its label. The predicate gets the opcode in edx and must leave edx, rcx and r8 as it
    // found them.
    public const ushort First = 0x0001;
    public const ushort Last = 0x0002;

    public static FakeReceiverImage Computed(ushort start, int count, Action<Assembler, Label> predicate)
    {
        var image = new FakeReceiverImage(2 * count + 2);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            var secondBank = a.CreateLabel();
            var go = a.CreateLabel();
            var first = a.CreateLabel();
            var last = a.CreateLabel();
            ReadOpcode(a, edx);
            a.cmp(edx, First);
            a.je(first);
            a.cmp(edx, Last);
            a.je(last);
            predicate(a, secondBank);
            a.xor(r11d, r11d);
            a.jmp(go);
            a.Label(ref secondBank);
            a.mov(r11d, count);
            a.Label(ref go);
            a.sub(edx, start);
            a.cmp(edx, count - 1);
            a.ja(fallback);
            a.add(edx, r11d);
            a.add(edx, image.FirstMethodSlot + 1);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + rdx * 8]);
            a.Label(ref first);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref last);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + 8 * (image.FirstMethodSlot + 2 * count + 1)]);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // Computed's methods, with the opcode read as the test likes: `read` leaves it in edx and the bank,
    // 0 or `count`, in r11d, or returns.
    public static FakeReceiverImage Banked(ushort start, int count, Action<Assembler> read)
    {
        var image = new FakeReceiverImage(2 * count + 2);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            var first = a.CreateLabel();
            var last = a.CreateLabel();
            read(a);
            a.cmp(edx, First);
            a.je(first);
            a.cmp(edx, Last);
            a.je(last);
            a.sub(edx, start);
            a.cmp(edx, count - 1);
            a.ja(fallback);
            a.add(edx, r11d);
            a.add(edx, image.FirstMethodSlot + 1);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + rdx * 8]);
            a.Label(ref first);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref last);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + 8 * (image.FirstMethodSlot + 2 * count + 1)]);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // The places Computed gives: First and Last at the ends, each opcode i after start at 1 + i, or
    // 1 + i + count where `holds` says the predicate jumps.
    public static Dictionary<ushort, int> ComputedPlaces(ushort start, int count, Func<ushort, bool> holds)
    {
        var places = Enumerable.Range(0, count).ToDictionary(i => (ushort)(start + i), i => 1 + i + (holds((ushort)(start + i)) ? count : 0));
        places[First] = 0;
        places[Last] = 2 * count + 1;
        return places;
    }

    // `count` distinct opcodes from a seeded shuffle of the client's opcode range, at places 0 on.
    public static Dictionary<ushort, int> ShuffledPlaces(int count, int seed = 7)
    {
        var opcodes = Enumerable.Range(0x66, 0x3E8 - 0x66).Select(o => (ushort)o).ToArray();
        new Random(seed).Shuffle(opcodes);
        return opcodes.Take(count).Select((opcode, i) => (opcode, place: i)).ToDictionary(p => p.opcode, p => p.place);
    }

    private static void Tree(Assembler a, ushort[] keys, IReadOnlyDictionary<ushort, int> places, Dictionary<int, Label> cases, Label fallback)
    {
        if (keys.Length <= 3)
        {
            foreach (var key in keys)
            {
                a.cmp(eax, key);
                a.je(cases[places[key]]);
            }
            a.jmp(fallback);
            return;
        }
        var mid = keys.Length / 2;
        var right = a.CreateLabel();
        a.cmp(eax, keys[mid]);
        a.je(cases[places[keys[mid]]]);
        a.ja(right);
        Tree(a, keys[..mid], places, cases, fallback);
        a.Label(ref right);
        Tree(a, keys[(mid + 1)..], places, cases, fallback);
    }

    private void Cases(Assembler a, Dictionary<int, Label> cases, CaseForm form)
    {
        foreach (var place in cases.Keys.ToArray())
        {
            var label = cases[place];
            a.Label(ref label);
            cases[place] = label;
            var slot = 8 * (FirstMethodSlot + place);
            a.mov(rax, __qword_ptr[rcx]);
            switch (form)
            {
                case CaseForm.TailJump:
                    a.lea(r9, __[r10 + 0x10]);
                    a.jmp(__qword_ptr[rax + slot]);
                    break;
                case CaseForm.Call:
                    a.sub(rsp, 0x28);
                    a.call(__qword_ptr[rax + slot]);
                    a.add(rsp, 0x28);
                    a.ret();
                    break;
                case CaseForm.LoadThenJump:
                    a.mov(rax, __qword_ptr[rax + slot]);
                    a.jmp(rax);
                    break;
                case CaseForm.CallThenAnotherOnAReceiverFlag:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.sub(rsp, 0x20);
                    a.mov(rbx, rcx);
                    a.call(__qword_ptr[rax + slot]);
                    a.test(__byte_ptr[rbx + 0x20], 1);
                    a.jz(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x20);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenAnotherOnItsResult:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.sub(rsp, 0x20);
                    a.mov(rbx, rcx);
                    a.mov(r11, rax);
                    a.xor(eax, eax);
                    a.call(__qword_ptr[r11 + slot]);
                    a.test(eax, eax);
                    a.jz(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x20);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenAnotherOnAnOutParameter:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.sub(rsp, 0x30);
                    a.mov(rbx, rcx);
                    a.mov(__qword_ptr[rsp + 0x28], 0);
                    a.lea(rdx, __[rsp + 0x28]);
                    a.call(__qword_ptr[rax + slot]);
                    a.cmp(__qword_ptr[rsp + 0x28], 0);
                    a.je(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x30);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenAnotherOnFlagsSetBefore:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.sub(rsp, 0x20);
                    a.mov(rbx, rcx);
                    a.mov(r11, rax);
                    a.xor(eax, eax);
                    a.call(__qword_ptr[r11 + slot]);
                    a.jz(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x20);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenAnotherOnItsHomeSpace:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.sub(rsp, 0x20);
                    a.mov(rbx, rcx);
                    a.mov(__qword_ptr[rsp + 8], 0);
                    a.call(__qword_ptr[rax + slot]);
                    a.cmp(__qword_ptr[rsp + 8], 0);
                    a.je(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x20);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenAnotherOnThePacket:
                {
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.push(rsi);
                    a.sub(rsp, 0x28);
                    a.mov(rbx, rcx);
                    a.mov(rsi, r10);
                    a.call(__qword_ptr[rax + slot]);
                    a.cmp(__word_ptr[rsi + 2], 0);
                    a.jne(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.call(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.add(rsp, 0x28);
                    a.pop(rsi);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.CallThenJumpToAnother:
                    a.push(rbx);
                    a.sub(rsp, 0x20);
                    a.mov(rbx, rcx);
                    a.call(__qword_ptr[rax + slot]);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.add(rsp, 0x20);
                    a.pop(rbx);
                    a.jmp(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    break;
                case CaseForm.CallThenJumpToTheNextThroughTheVtableKept:
                    a.push(rbx);
                    a.push(rsi);
                    a.sub(rsp, 0x28);
                    a.mov(rbx, rcx);
                    a.mov(rsi, rax);
                    a.call(__qword_ptr[rsi + slot]);
                    a.mov(rcx, rbx);
                    a.mov(rax, rsi);
                    a.add(rsp, 0x28);
                    a.pop(rsi);
                    a.pop(rbx);
                    a.jmp(__qword_ptr[rax + slot + 8]);
                    break;
                case CaseForm.JumpReturningIntoTheCase:
                {
                    var back = a.CreateLabel();
                    var done = a.CreateLabel();
                    a.push(rbx);
                    a.mov(rbx, rcx);
                    a.lea(r11, __[back]);
                    a.push(r11);
                    a.jmp(__qword_ptr[rax + slot]);
                    a.Label(ref back);
                    a.test(__byte_ptr[rbx + 0x20], 1);
                    a.jz(done);
                    a.mov(rax, __qword_ptr[rbx]);
                    a.mov(rcx, rbx);
                    a.pop(rbx);
                    a.jmp(__qword_ptr[rax + 8 * FirstMethodSlot]);
                    a.Label(ref done);
                    a.pop(rbx);
                    a.ret();
                    break;
                }
                case CaseForm.JumpPastTheMethodBefore:
                    a.mov(rax, __qword_ptr[rax + slot - 8]);
                    a.add(rax, 0x10);
                    a.jmp(rax);
                    break;
            }
        }
    }

    private sealed class ByteWriter : CodeWriter
    {
        public List<byte> Bytes { get; } = [];

        public override void WriteByte(byte value) => Bytes.Add(value);
    }
}
