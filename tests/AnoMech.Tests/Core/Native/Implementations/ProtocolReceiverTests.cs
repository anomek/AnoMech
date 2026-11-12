using System.Runtime.InteropServices;
using AnoMech.Core.Native.Implementations;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace AnoMech.Tests;

// The client's generated receiver names each zone packet by its place among its methods, in the
// protocol's own order; ProtocolReceiver reads which opcode reaches which place by running the
// dispatch itself (FakeReceiverImage builds one). Patches reshuffle opcodes and the compiler
// re-encodes switches as it likes, so the map has to come out the same however the switch is
// encoded, and not at all when the dispatch does something the model doesn't understand or routes
// on anything but the opcode.
public class ProtocolReceiverTests
{
    private static readonly Dictionary<ushort, int> Places = FakeReceiverImage.ShuffledPlaces(400);

    public enum Encoding
    {
        JumpTable,
        JumpTableRebasedBy32BitImmediate,
        ByteTableLoneCompareAndCompareTree,
        CompareTree,
        CasesCallTheirMethod,
        CasesLoadTheirMethodFirst,
        PacketBehindOtherFields,
        DispatchWithAStackFrame,
    }

    [TestCase(Encoding.JumpTable)]
    [TestCase(Encoding.JumpTableRebasedBy32BitImmediate)]
    [TestCase(Encoding.ByteTableLoneCompareAndCompareTree)]
    [TestCase(Encoding.CompareTree)]
    [TestCase(Encoding.CasesCallTheirMethod)]
    [TestCase(Encoding.CasesLoadTheirMethodFirst)]
    [TestCase(Encoding.PacketBehindOtherFields)]
    [TestCase(Encoding.DispatchWithAStackFrame)]
    public void EveryEncodingOfTheSwitchGivesTheSameMap(Encoding encoding)
    {
        var rebased = Places.ToDictionary(p => (ushort)(p.Key + 0x400), p => p.Value);
        var (image, places) = encoding switch
        {
            Encoding.JumpTable => (FakeReceiverImage.JumpTable(Places), Places),
            Encoding.JumpTableRebasedBy32BitImmediate => (FakeReceiverImage.JumpTable(rebased), rebased),
            Encoding.ByteTableLoneCompareAndCompareTree => (FakeReceiverImage.ByteTableAndCompares(Places), Places),
            Encoding.CompareTree => (FakeReceiverImage.CompareTree(Places), Places),
            Encoding.CasesCallTheirMethod => (FakeReceiverImage.JumpTable(Places, FakeReceiverImage.CaseForm.Call), Places),
            Encoding.CasesLoadTheirMethodFirst => (FakeReceiverImage.JumpTable(Places, FakeReceiverImage.CaseForm.LoadThenJump), Places),
            Encoding.PacketBehindOtherFields => (FakeReceiverImage.JumpTable(Places, holderField: 0x28, channelField: 0x08), Places),
            _ => (FakeReceiverImage.WithStackFrame(Places), Places),
        };

        var receiver = ProtocolReceiver.Read(image.Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, places, methods: 400);
    }

    // A packet's place counts from the first packet method, wherever the vtable keeps it.
    public enum VtableLayout
    {
        AnotherVirtualBeforeTheDispatch,
        AVirtualBetweenTheDispatchAndThePacketMethods,
        EmptyMethodsThatReturnZero,
        EmptyMethodsThatOnlyReturn,
    }

    [TestCase(VtableLayout.AnotherVirtualBeforeTheDispatch)]
    [TestCase(VtableLayout.AVirtualBetweenTheDispatchAndThePacketMethods)]
    [TestCase(VtableLayout.EmptyMethodsThatReturnZero)]
    [TestCase(VtableLayout.EmptyMethodsThatOnlyReturn)]
    public void EveryLayoutOfTheVtableGivesTheSamePlaces(VtableLayout vtable)
    {
        var layout = vtable switch
        {
            VtableLayout.AnotherVirtualBeforeTheDispatch => new FakeReceiverImage.Layout(VirtualsBeforeDispatch: 2),
            VtableLayout.AVirtualBetweenTheDispatchAndThePacketMethods => new FakeReceiverImage.Layout(VirtualAfterDispatch: true),
            VtableLayout.EmptyMethodsThatReturnZero => new FakeReceiverImage.Layout(EmptyMethod: [0x31, 0xC0, 0xC3]),
            _ => new FakeReceiverImage.Layout(EmptyMethod: [0xC3]),
        };

        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(Places, layout: layout).Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, Places, methods: 400);
    }

    // A packet method no opcode reaches keeps its place, between others: the protocol has the packet.
    [Test]
    public void AMethodNoOpcodeReachesInsideTheRunKeepsThePlacesAfterIt()
    {
        var places = Places.Where(p => p.Value != 123).ToDictionary();

        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places, methods: 400).Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, places, methods: 400);
    }

    // At either end, nothing says where the places start: not even a derived class's vtable that
    // overrides that method, whose run of empty methods the reached ones span.
    [TestCase(0, false)]
    [TestCase(399, false)]
    [TestCase(0, true)]
    [TestCase(399, true)]
    public void AMethodNoOpcodeReachesAtAnEndGivesNoMap(int unreached, bool derivedVtableOverridesIt)
    {
        var places = Places.Where(p => p.Value != unreached).ToDictionary();
        var image = FakeReceiverImage.JumpTable(places, methods: 400);
        if (derivedVtableOverridesIt) image.AddDerivedVtable(methods: 400, overridden: unreached);

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A patch that inserts a packet, on a build where no opcode reaches the last method yet, beside a
    // derived class's vtable overriding that method: the revision's count and the places after the
    // insert would both be read wrong.
    [Test]
    public void ARevisionOneMethodLongerWithItsLastMethodUnreachedGivesNoMap()
    {
        var places = ClientReceiverMaps.Places(ClientReceiverMaps.Build20261103).ToDictionary(p => p.Key, p => p.Value < 100 ? p.Value : p.Value + 1);
        places[0x0400] = 100;
        places.Remove(places.Single(p => p.Value == 775).Key);
        var image = FakeReceiverImage.JumpTable(places, methods: 776);
        image.AddDerivedVtable(methods: 776, overridden: 775);

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // The model's semantics, each where a slip would send opcodes to other places: the predicate
    // splits opcodes 0x7F00-0x802B, across the sign of a 16-bit value, between two banks of methods.
    private static IEnumerable<TestCaseData> Semantics()
    {
        TestCaseData Case(string name, Action<Assembler, Label> predicate, Func<ushort, bool> holds)
            => new TestCaseData(predicate, holds).SetArgDisplayNames(name);

        yield return Case("a 32-bit write clears the upper half", (a, yes) =>
        {
            a.mov(r11, -1L);
            a.mov(r11d, edx);
            a.shr(r11, 32);
            a.test(r11, r11);
            a.jnz(yes);
        }, _ => false);
        yield return Case("a 32-bit shift by zero still clears the upper half", (a, yes) =>
        {
            a.push(rcx);
            a.xor(ecx, ecx);
            a.mov(r11, -1L);
            a.shl(r11d, cl);
            a.pop(rcx);
            a.shr(r11, 32);
            a.test(r11, r11);
            a.jnz(yes);
        }, _ => false);
        yield return Case("signed less than", (a, yes) =>
        {
            a.movsx(r11d, dx);
            a.cmp(r11d, 0x7F80);
            a.jl(yes);
        }, op => (short)op < 0x7F80);
        yield return Case("signed greater or equal", (a, yes) =>
        {
            a.movsx(r11d, dx);
            a.cmp(r11d, 0x7FC0);
            a.jge(yes);
        }, op => (short)op >= 0x7FC0);
        yield return Case("unsigned below", (a, yes) =>
        {
            a.cmp(edx, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("unsigned below or equal, 16-bit", (a, yes) =>
        {
            a.cmp(dx, 0x8010);
            a.jbe(yes);
        }, op => op <= 0x8010);
        yield return Case("signed greater after a 16-bit subtract", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.sub(r11w, 0x10);
            a.cmp(r11w, 0x7F70);
            a.jg(yes);
        }, op => (short)(op - 0x10) > 0x7F70);
        yield return Case("signed overflow", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.add(r11w, 0x80);
            a.jo(yes);
        }, op => op is >= 0x7F80 and <= 0x7FFF);
        yield return Case("sign", (a, yes) =>
        {
            a.test(dx, dx);
            a.js(yes);
        }, op => op >= 0x8000);
        yield return Case("parity of the low byte", (a, yes) =>
        {
            a.test(dl, dl);
            a.jp(yes);
        }, op => System.Numerics.BitOperations.PopCount((uint)(op & 0xFF)) % 2 == 0);
        yield return Case("a high-byte register", (a, yes) =>
        {
            a.mov(eax, edx);
            a.cmp(ah, 0x80);
            a.je(yes);
        }, op => op >> 8 == 0x80);
        yield return Case("a bit test", (a, yes) =>
        {
            a.mov(r11d, unchecked((int)0xAAAAAAAA));
            a.bt(r11d, edx);
            a.jb(yes);
        }, op => (0xAAAAAAAAu >> (op & 31) & 1) != 0);
        yield return Case("a conditional move", (a, yes) =>
        {
            a.xor(r11d, r11d);
            a.mov(eax, 1);
            a.cmp(edx, 0x7FA0);
            a.cmovae(r11d, eax);
            a.test(r11d, r11d);
            a.jnz(yes);
        }, op => op >= 0x7FA0);
        yield return Case("a conditional set", (a, yes) =>
        {
            a.cmp(edx, 0x7FC0);
            a.setb(al);
            a.test(al, al);
            a.jnz(yes);
        }, op => op < 0x7FC0);
        yield return Case("an arithmetic shift right", (a, yes) =>
        {
            a.movsx(r11d, dx);
            a.sar(r11d, 4);
            a.cmp(r11d, -0x7FC);
            a.jl(yes);
        }, op => (short)op >> 4 < -0x7FC);
        yield return Case("a 16-bit rotate", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.rol(r11w, 4);
            a.test(r11w, 0x8);
            a.jnz(yes);
        }, op => (((op << 4) | (op >> 12)) & 0x8) != 0);
        yield return Case("negate, not, increment and decrement", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.neg(r11d);
            a.not(r11d);
            a.inc(r11d);
            a.dec(r11d);
            a.cmp(r11d, 0x7F8F);
            a.ja(yes);
        }, op => op - 1 > 0x7F8F);
        yield return Case("a multiply and an address", (a, yes) =>
        {
            a.imul(r11d, edx, 3);
            a.lea(r11d, __[r11 + r11 * 2 + 7]);
            a.cmp(r11d, 0x7FB0 * 9 + 7);
            a.jae(yes);
        }, op => op >= 0x7FB0);
        yield return Case("an exchange", (a, yes) =>
        {
            a.mov(r11d, 0x7FE0);
            a.xchg(r11d, edx);
            a.cmp(edx, r11d);
            a.xchg(r11d, edx);
            a.ja(yes);
        }, op => 0x7FE0 > op);
    }

    [TestCaseSource(nameof(Semantics))]
    public void TheModelRunsEachInstructionAsTheProcessorDoes(Action<Assembler, Label> predicate, Func<ushort, bool> holds)
    {
        const ushort Start = 0x7F00;
        const int Count = 300;
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.Computed(Start, Count, predicate).Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, FakeReceiverImage.ComputedPlaces(Start, Count, holds), methods: 2 * Count + 2);
    }

    // What the model knows whatever else the game holds: the magic, constants, what the dispatch put
    // somewhere or settled itself, and that the receiver, its vtable and the pointers the packet is
    // read through aren't null. A dispatch that reads more but routes on the opcode alone gives its
    // map.
    private static IEnumerable<TestCaseData> OpcodeAlone()
    {
        TestCaseData Case(string name, Action<Assembler, Label> predicate, Func<ushort, bool> holds)
            => new TestCaseData(predicate, holds).SetArgDisplayNames(name);

        yield return Case("the opcode read with the magic, as a dword", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10]);
            a.shr(r11d, 16);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the opcode read with the field after it, then zero-extended", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 2]);
            a.movzx(r11d, r11w);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the opcode read with the field after it, then masked", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 2]);
            a.and(r11d, 0xFFFF);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the opcode read with the field after it, then shifted out", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 2]);
            a.shl(r11d, 16);
            a.shr(r11d, 16);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the opcode sign-extended from the top of a dword", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 2]);
            a.shl(r11d, 16);
            a.sar(r11d, 16);
            a.cmp(r11d, 0x7F80);
            a.jl(yes);
        }, op => (short)op < 0x7F80);
        yield return Case("the opcode read again through the same fields", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x10]);
            a.mov(r11, __qword_ptr[r11 + 0x38]);
            a.cmp(__word_ptr[r11 + 2], 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the packet pointer stored and reloaded", (a, yes) =>
        {
            a.mov(__qword_ptr[rsp + 8], r10);
            a.mov(r11, __qword_ptr[rsp + 8]);
            a.cmp(__word_ptr[r11 + 2], 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the magic", (a, yes) =>
        {
            a.cmp(__word_ptr[r10], 0x14);
            a.jne(yes);
        }, _ => false);
        yield return Case("a constant through the stack", (a, yes) =>
        {
            a.push(0x7F80);
            a.pop(r11);
            a.cmp(edx, r11d);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("a call to a function of its own", (a, yes) =>
        {
            var function = a.CreateLabel();
            var back = a.CreateLabel();
            a.call(function);
            a.test(al, al);
            a.jnz(yes);
            a.jmp(back);
            a.Label(ref function);
            a.cmp(edx, 0x7F80);
            a.setb(al);
            a.ret();
            a.Label(ref back);
        }, op => op < 0x7F80);
        yield return Case("registers it isn't handed, saved and restored", (a, yes) =>
        {
            a.push(rbx);
            a.push(r12);
            a.mov(__qword_ptr[rsp + 0x18], r9);
            a.mov(r9, __qword_ptr[rsp + 0x18]);
            a.pop(r12);
            a.pop(rbx);
        }, _ => false);
        yield return Case("a register it isn't handed, zeroed", (a, yes) =>
        {
            a.xor(r12d, r12d);
            a.test(r12d, r12d);
            a.jnz(yes);
        }, _ => false);
        yield return Case("a register it isn't handed, less itself", (a, yes) =>
        {
            a.mov(r11, r12);
            a.sub(r11, r11);
            a.jnz(yes);
        }, _ => false);
        yield return Case("a 32-bit write over a register it isn't handed", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.shr(r11, 32);
            a.jnz(yes);
        }, _ => false);
        yield return Case("the opcode exchanged into a register it isn't handed", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.xchg(r11d, r12d);
            a.cmp(r12d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("a bit it knows is set, whatever the rest", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 2]);
            a.or(r11d, 0x10000);
            a.test(r11d, r11d);
            a.jz(yes);
        }, _ => false);
        yield return Case("a conditional move between equal values", (a, yes) =>
        {
            a.mov(r11d, edx);
            a.mov(eax, edx);
            a.test(r12, r12);
            a.cmovs(r11d, eax);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("a conditional set it doesn't know, masked off", (a, yes) =>
        {
            a.test(r12, r12);
            a.sets(r11b);
            a.and(r11d, 0xFE);
            a.jnz(yes);
        }, _ => false);
        yield return Case("the packet pointer tested for null", (a, yes) =>
        {
            a.test(r10, r10);
            a.jz(yes);
        }, _ => false);
        yield return Case("the holder tested for null", (a, yes) =>
        {
            a.test(r8, r8);
            a.jz(yes);
        }, _ => false);
        yield return Case("the field the packet is read through tested for null", (a, yes) =>
        {
            a.cmp(__qword_ptr[r8 + 0x10], 0);
            a.je(yes);
        }, _ => false);
        yield return Case("the receiver tested for null", (a, yes) =>
        {
            a.test(rcx, rcx);
            a.js(yes);
            a.jz(yes);
        }, _ => false);
        yield return Case("its vtable tested for null", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[rcx]);
            a.cmp(r11, 0);
            a.jbe(yes);
        }, _ => false);
        yield return Case("the packet pointer moved with inc", (a, yes) =>
        {
            a.lea(r11, __[r10 + 1]);
            a.inc(r11);
            a.cmp(__word_ptr[r11], 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("the packet pointer moved with dec", (a, yes) =>
        {
            a.lea(r11, __[r10 + 3]);
            a.dec(r11);
            a.cmp(__word_ptr[r11], 0x7F80);
            a.jb(yes);
        }, op => op < 0x7F80);
        yield return Case("a speculation barrier", (a, yes) =>
        {
            a.cmp(edx, 0x7F80);
            a.lfence();
            a.jb(yes);
        }, op => op < 0x7F80);
    }

    [TestCaseSource(nameof(OpcodeAlone))]
    public void ADispatchThatReadsMoreButRoutesOnTheOpcodeAloneGivesItsMap(Action<Assembler, Label> predicate, Func<ushort, bool> holds)
    {
        const ushort Start = 0x7F00;
        const int Count = 300;
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.Computed(Start, Count, predicate).Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, FakeReceiverImage.ComputedPlaces(Start, Count, holds), methods: 2 * Count + 2);
    }

    // How the dispatch gets at the opcode: the holder and the fields the packet is read through may be
    // null until it reads through them, so a test before that may drop the packet but not route it
    // another way; other objects it reads don't make the packet theirs. Each leaves the opcode in edx
    // and the bank in r11d.
    private static IEnumerable<TestCaseData> OpcodeReads()
    {
        TestCaseData Case(string name, bool read, Action<Assembler> load) => new TestCaseData(read, load).SetArgDisplayNames(name);

        yield return Case("the holder tested for null first, returning when it is", true, a =>
        {
            var ok = a.CreateLabel();
            a.test(r8, r8);
            a.jne(ok);
            a.ret();
            a.Label(ref ok);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
        });
        yield return Case("a field tested for null first, returning when it is", true, a =>
        {
            var ok = a.CreateLabel();
            a.cmp(__qword_ptr[r8 + 0x10], 0);
            a.jne(ok);
            a.ret();
            a.Label(ref ok);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
        });
        yield return Case("the holder, the channel and the packet tested for null in turn", true, a =>
        {
            var no = a.CreateLabel();
            var ok = a.CreateLabel();
            a.test(r8, r8);
            a.je(no);
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.test(rax, rax);
            a.je(no);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.test(r10, r10);
            a.jne(ok);
            a.Label(ref no);
            a.ret();
            a.Label(ref ok);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("a field tested for null with or", true, a =>
        {
            var ok = a.CreateLabel();
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.or(rax, rax);
            a.jne(ok);
            a.ret();
            a.Label(ref ok);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("the packet tested for null after its opcode is read", true, a =>
        {
            var go = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.test(r10, r10);
            a.jnz(go);
            a.mov(r11d, 300);
            a.Label(ref go);
        });
        yield return Case("another object read first, for the method", true, a =>
        {
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.mov(r9, __qword_ptr[rax + 0x40]);
            a.mov(r9d, __dword_ptr[r9 + 8]);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("another object read after the opcode, for the method", true, a =>
        {
            FakeReceiverImage.ReadOpcode(a, edx);
            a.mov(r9, __qword_ptr[rax + 0x40]);
            a.mov(r9d, __dword_ptr[r9 + 8]);
            a.xor(r11d, r11d);
        });
        yield return Case("its stack realigned to 16 bytes", true, a =>
        {
            a.push(rbp);
            a.mov(rbp, rsp);
            a.and(rsp, -16);
            a.sub(rsp, 0x20);
            a.push(rcx);
            a.pop(rcx);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.mov(rsp, rbp);
            a.pop(rbp);
        });
        yield return Case("a field the packet isn't read through tested for null first, the other bank when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.cmp(__qword_ptr[r8 + 0x18], 0);
            a.je(held);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.jmp(done);
            a.Label(ref held);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("a field tested for null first, the packet read through another when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.cmp(__qword_ptr[r8 + 0x18], 0);
            a.je(held);
            FakeReceiverImage.ReadOpcode(a, edx, holderField: 0x18);
            a.xor(r11d, r11d);
            a.jmp(done);
            a.Label(ref held);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("a field tested for null before it's read through, another read when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.test(rax, rax);
            a.jz(held);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
            a.jmp(done);
            a.Label(ref held);
            FakeReceiverImage.ReadOpcode(a, edx, holderField: 0x18);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("the packet tested for null before it's read, another one's read when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.test(r10, r10);
            a.jz(held);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
            a.jmp(done);
            a.Label(ref held);
            FakeReceiverImage.ReadOpcode(a, edx, holderField: 0x20);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("the holder tested for null first, the packet from r9 when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.test(r8, r8);
            a.jz(held);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.jmp(done);
            a.Label(ref held);
            a.mov(r10, r9);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("a field tested for null with and", true, a =>
        {
            var ok = a.CreateLabel();
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.and(rax, rax);
            a.jne(ok);
            a.ret();
            a.Label(ref ok);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("a field tested for null first, the same method either way", true, a =>
        {
            var skip = a.CreateLabel();
            a.cmp(__qword_ptr[r8 + 0x18], 0);
            a.jne(skip);
            a.nop();
            a.Label(ref skip);
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
        });
        yield return Case("another object's dword at +4 read first, for the method", true, a =>
        {
            a.mov(rax, __qword_ptr[r8 + 0x10]);
            a.mov(r9, __qword_ptr[rax + 0x40]);
            a.mov(r9d, __dword_ptr[r9 + 4]);
            a.mov(r10, __qword_ptr[rax + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("a null-safe getter, its null way reading the null packet's header", true, a =>
        {
            var got = a.CreateLabel();
            a.mov(r10, __qword_ptr[r8 + 0x10]);
            a.test(r10, r10);
            a.je(got);
            a.mov(r10, __qword_ptr[r10 + 0x38]);
            a.Label(ref got);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.xor(r11d, r11d);
        });
        yield return Case("a field the packet isn't read through, below zero, routing elsewhere", false, a =>
        {
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.cmp(__qword_ptr[r8 + 0x18], 0);
            a.jge(done);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("a field the packet isn't read through, moved and then tested, routing elsewhere", false, a =>
        {
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.xor(r11d, r11d);
            a.mov(r9, __qword_ptr[r8 + 0x18]);
            a.add(r9, 0x100);
            a.test(r9, r9);
            a.jnz(done);
            a.mov(r11d, 300);
            a.Label(ref done);
        });
        yield return Case("a field tested for null first, read past the null page when it is", false, a =>
        {
            var held = a.CreateLabel();
            var done = a.CreateLabel();
            a.mov(r10, __qword_ptr[r8 + 0x10]);
            a.test(r10, r10);
            a.je(held);
            a.mov(r10, __qword_ptr[r10 + 0x38]);
            a.movzx(edx, __word_ptr[r10 + 2]);
            a.jmp(done);
            a.Label(ref held);
            a.movzx(edx, __word_ptr[r10 + 0x20000]);
            a.Label(ref done);
            a.xor(r11d, r11d);
        });
    }

    [TestCaseSource(nameof(OpcodeReads))]
    public void HowTheOpcodeIsReadDecidesWhetherThereIsAMap(bool read, Action<Assembler> load)
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.Banked(0x7F00, 300, load).Image(), out var why);

        if (!read)
        {
            Assert.That(receiver, Is.Null);
            Assert.That(why, Is.Not.Empty);
            return;
        }
        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, FakeReceiverImage.ComputedPlaces(0x7F00, 300, _ => false), methods: 602);
    }

    // Whatever the model doesn't understand ends the read with no map, not a guess, even when only
    // some opcodes take that path: here those from 0x8000 on.
    private static IEnumerable<TestCaseData> NotUnderstood()
    {
        TestCaseData Case(string name, Action<Assembler, Label> poison) => new TestCaseData((Action<Assembler, Label>)((a, yes) =>
        {
            var fine = a.CreateLabel();
            a.cmp(edx, 0x8000);
            a.jb(fine);
            poison(a, yes);
            a.Label(ref fine);
        })).SetArgDisplayNames(name);

        yield return Case("an instruction it doesn't model", (a, _) =>
        {
            a.xor(r11d, r11d);
            a.adc(r11d, edx);
        });
        yield return Case("a write outside its own stack", (a, _) => a.mov(__dword_ptr[r8], edx));
        yield return Case("a write through an address the game decides", (a, _) => a.mov(__dword_ptr[r12 + 8], edx));
        yield return Case("a read of data the game changes as it runs", (a, _) =>
            a.AddInstruction(Instruction.Create(Code.Mov_r32_rm32, Register.R11D, new MemoryOperand(Register.RIP, (long)FakeReceiverImage.WritableData))));
        yield return Case("a jump out of the module, as into a hook", (a, _) =>
        {
            a.mov(r11, 0x7FF0_1234_0000L);
            a.jmp(r11);
        });
        yield return Case("a loop that never ends", (a, _) =>
        {
            var again = a.CreateLabel();
            a.Label(ref again);
            a.jmp(again);
        });
        yield return Case("a flag the last instruction left undefined", (a, yes) =>
        {
            a.imul(r11d, edx, 3);
            a.jz(yes);
        });
        yield return Case("a segment-relative read", (a, _) =>
            a.AddInstruction(Instruction.Create(Code.Mov_r64_rm64, Register.R11, new MemoryOperand(Register.R8, 0L, 0, false, Register.GS))));
        yield return Case("a 16-bit push", (a, _) =>
        {
            a.AddInstruction(Instruction.Create(Code.Pushw_imm8, 5));
            a.pop(r11);
        });
    }

    [TestCaseSource(nameof(NotUnderstood))]
    public void ADispatchItDoesNotUnderstandGivesNoMap(Action<Assembler, Label> predicate)
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.Computed(0x7F00, 300, predicate).Image(), out var why);

        Assert.That(receiver, Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A dispatch that routes on anything but the opcode (another packet field, the holder, the
    // receiver's members, a register it isn't handed, its caller's stack, where a stand-in points)
    // would route live packets in ways a map can't say. Many test bits that the second model's other
    // stand-ins and fill leave as the first model has them.
    private static IEnumerable<TestCaseData> RoutedOnMore()
    {
        TestCaseData Case(string name, Action<Assembler, Label> predicate) => new TestCaseData(predicate).SetArgDisplayNames(name);

        yield return Case("its stack realigned to 32 bytes, read back through the frame pointer", (a, yes) =>
        {
            a.push(rbp);
            a.mov(rbp, rsp);
            a.and(rsp, -32);
            a.mov(__qword_ptr[rsp], 0);
            a.mov(__qword_ptr[rbp - 0x10], 1);
            a.cmp(__qword_ptr[rsp], 0);
            a.mov(rsp, rbp);
            a.pop(rbp);
            a.jne(yes);
        });
        yield return Case("a jump Intel and AMD processors take to different places", (a, yes) =>
        {
            a.lea(rax, __[yes]);
            a.db(0x66, 0xFF, 0xE0);
        });
        yield return Case("a return Intel and AMD processors size differently", (a, _) =>
        {
            var fine = a.CreateLabel();
            a.cmp(edx, 0x8000);
            a.jb(fine);
            a.db(0x66, 0xC3);
            a.Label(ref fine);
        });

        yield return Case("another field of the packet", (a, yes) =>
        {
            a.movzx(r11d, __word_ptr[r10 + 4]);
            a.test(r11d, r11d);
            a.jnz(yes);
        });
        for (var bit = 0; bit < 8; bit++)
        {
            var mask = 1 << bit;
            yield return Case($"bit {bit} of a header byte", (a, yes) =>
            {
                a.test(__byte_ptr[r10 + 5], mask);
                a.jnz(yes);
            });
        }
        yield return Case("bit 7 of another header byte", (a, yes) =>
        {
            a.test(__byte_ptr[r10 + 4], 0x80);
            a.jnz(yes);
        });
        yield return Case("bit 0 of a body byte", (a, yes) =>
        {
            a.test(__byte_ptr[r10 + 0x10], 1);
            a.jnz(yes);
        });
        yield return Case("bit 2 of a body byte", (a, yes) =>
        {
            a.test(__byte_ptr[r10 + 0x11], 4);
            a.jnz(yes);
        });
        yield return Case("the sign of a header word", (a, yes) =>
        {
            a.movsx(r11d, __word_ptr[r10 + 6]);
            a.test(r11d, r11d);
            a.js(yes);
        });
        yield return Case("a body dword below zero", (a, yes) =>
        {
            a.cmp(__dword_ptr[r10 + 8], 0);
            a.jl(yes);
        });
        yield return Case("the sign of a body dword", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x18]);
            a.test(r11d, r11d);
            a.js(yes);
        });
        yield return Case("the sign of a body dword, spread", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x18]);
            a.sar(r11d, 31);
            a.jnz(yes);
        });
        yield return Case("a packet field the packet itself indexes", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.and(r11d, 1);
            a.cmp(__word_ptr[r10 + r11 * 2 + 2], 0x7F80);
            a.jb(yes);
        });
        yield return Case("a body byte above a bound", (a, yes) =>
        {
            a.cmp(__byte_ptr[r10 + 0x12], 0x60);
            a.ja(yes);
        });
        yield return Case("a body word above a bound", (a, yes) =>
        {
            a.cmp(__word_ptr[r10 + 0x12], 0x6000);
            a.ja(yes);
        });
        yield return Case("a body dword above a bound", (a, yes) =>
        {
            a.cmp(__dword_ptr[r10 + 0x14], 0x60000000);
            a.ja(yes);
        });
        yield return Case("a body byte equal to 1", (a, yes) =>
        {
            a.cmp(__byte_ptr[r10 + 0x10], 1);
            a.je(yes);
        });
        yield return Case("a body byte equal to 2", (a, yes) =>
        {
            a.cmp(__byte_ptr[r10 + 0x10], 2);
            a.je(yes);
        });
        yield return Case("the opcode keyed with a body bit", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.and(r11d, 0x80);
            a.xor(r11d, edx);
            a.cmp(r11d, 0x7F80);
            a.jb(yes);
        });
        for (var at = 0x3B; at < 0x40; at++)
        {
            var field = at;
            yield return Case($"the opcode keyed with byte {field - 0x38} of a channel field", (a, yes) =>
            {
                a.movzx(r11d, __byte_ptr[rax + field]);
                a.xor(r11d, edx);
                a.cmp(r11d, 0x7F80);
                a.jb(yes);
            });
        }
        yield return Case("a body byte with a bit set beside it", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.or(r11d, 0x100);
            a.test(r11d, 0x80);
            a.jnz(yes);
        });
        yield return Case("a carry out of a body bit", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.and(r11d, 1);
            a.add(r11d, 0xFF);
            a.test(r11d, 0x100);
            a.jnz(yes);
        });
        yield return Case("a shift by a body bit", (a, yes) =>
        {
            a.push(rcx);
            a.movzx(ecx, __byte_ptr[r10 + 0x10]);
            a.and(ecx, 1);
            a.mov(r11d, -1);
            a.shl(r11d, cl);
            a.pop(rcx);
            a.test(r11d, 1);
            a.jnz(yes);
        });
        yield return Case("a jump to where a body bit points", (a, yes) =>
        {
            var stay = a.CreateLabel();
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.and(r11d, 1);
            a.lea(rax, __[stay]);
            a.lea(r9, __[yes]);
            a.sub(r9, rax);
            a.imul(r9, r11);
            a.add(rax, r9);
            a.jmp(rax);
            a.Label(ref stay);
        });
        yield return Case("another packet, read through another field of the holder", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x18]);
            a.mov(r11, __qword_ptr[r11 + 0x38]);
            a.cmp(__word_ptr[r11 + 2], 0x7F80);
            a.jb(yes);
        });
        yield return Case("another packet, read through another field of the channel", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x10]);
            a.mov(r11, __qword_ptr[r11 + 0x40]);
            a.cmp(__word_ptr[r11 + 2], 0x7F80);
            a.jb(yes);
        });
        yield return Case("a field of the holder", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r8 + 0x20]);
            a.test(r11d, 0x7FFF8);
            a.jnz(yes);
        });
        yield return Case("the low bits of a field of the holder", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r8 + 0x20]);
            a.test(r11d, 7);
            a.jnz(yes);
        });
        yield return Case("bit 0 of a holder byte", (a, yes) =>
        {
            a.test(__byte_ptr[r8 + 0x20], 1);
            a.jnz(yes);
        });
        yield return Case("bit 2 of a holder byte", (a, yes) =>
        {
            a.test(__byte_ptr[r8 + 0x28], 4);
            a.jnz(yes);
        });
        yield return Case("bit 22 of a holder dword", (a, yes) =>
        {
            a.test(__dword_ptr[r8 + 0x20], 0x400000);
            a.jnz(yes);
        });
        yield return Case("a byte of the receiver", (a, yes) =>
        {
            a.cmp(__byte_ptr[rcx + 0x34], 0);
            a.jne(yes);
        });
        yield return Case("bit 7 of a byte of the receiver", (a, yes) =>
        {
            a.test(__byte_ptr[rcx + 0x34], 0x80);
            a.jnz(yes);
        });
        yield return Case("a flag in the receiver", (a, yes) =>
        {
            a.cmp(__byte_ptr[rcx + 0x0E], 0);
            a.jne(yes);
        });
        yield return Case("a dword of the receiver", (a, yes) =>
        {
            a.cmp(__dword_ptr[rcx + 0x0C], 0);
            a.jne(yes);
        });
        yield return Case("the receiver's members against each other", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[rcx + 0x1C]);
            a.mov(r9d, __dword_ptr[rcx + 0x18]);
            a.lea(eax, __[r9 + r11]);
            a.cmp(__dword_ptr[rcx + 0x20], eax);
            a.jb(yes);
        });
        yield return Case("a method of an object the receiver holds", (a, yes) =>
        {
            var skip = a.CreateLabel();
            a.cmp(edx, 0x7F80);
            a.jne(skip);
            a.mov(rax, __qword_ptr[rcx + 0x30]);
            a.jmp(__qword_ptr[rax + 8 * 431]);
            a.Label(ref skip);
        });
        yield return Case("a register it isn't handed", (a, yes) =>
        {
            a.test(r12d, r12d);
            a.jnz(yes);
        });
        yield return Case("the sign of a register it isn't handed", (a, yes) =>
        {
            a.test(r12, r12);
            a.js(yes);
        });
        yield return Case("bit 0 of a register it isn't handed", (a, yes) =>
        {
            a.bt(r12d, 0);
            a.jb(yes);
        });
        yield return Case("the sign of another register it isn't handed", (a, yes) =>
        {
            a.test(rbx, rbx);
            a.js(yes);
        });
        yield return Case("the sign of a register it isn't handed, under a 16-bit write", (a, yes) =>
        {
            a.mov(r12w, dx);
            a.test(r12, r12);
            a.js(yes);
        });
        yield return Case("the second byte of a register it isn't handed", (a, yes) =>
        {
            a.mov(eax, r12d);
            a.test(ah, 0x80);
            a.jnz(yes);
        });
        yield return Case("bit 31 of the fourth argument", (a, yes) =>
        {
            a.test(r9d, unchecked((int)0x80000000));
            a.jnz(yes);
        });
        yield return Case("a product of a register it isn't handed", (a, yes) =>
        {
            a.imul(r11d, r12d, 3);
            a.test(r11d, unchecked((int)0x80000000));
            a.jnz(yes);
        });
        yield return Case("a register it isn't handed, through the stack", (a, yes) =>
        {
            a.push(r12);
            a.pop(r11);
            a.test(r11, r11);
            a.js(yes);
        });
        yield return Case("a register it isn't handed, exchanged in", (a, yes) =>
        {
            a.mov(eax, edx);
            a.xchg(eax, r12d);
            a.test(eax, eax);
            a.js(yes);
        });
        yield return Case("a conditional move on a register it isn't handed", (a, yes) =>
        {
            a.xor(r11d, r11d);
            a.mov(eax, 1);
            a.test(r12, r12);
            a.cmovs(r11d, eax);
            a.test(r11d, r11d);
            a.jnz(yes);
        });
        yield return Case("a conditional set on a register it isn't handed", (a, yes) =>
        {
            a.test(r12, r12);
            a.sets(al);
            a.test(al, al);
            a.jnz(yes);
        });
        yield return Case("its caller's stack", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[rsp + 0x10]);
            a.test(r11, r11);
            a.jnz(yes);
        });
        yield return Case("bit 7 of the fifth argument", (a, yes) =>
        {
            a.test(__byte_ptr[rsp + 0x28], 0x80);
            a.jnz(yes);
        });
        yield return Case("the fifth argument below zero", (a, yes) =>
        {
            a.cmp(__dword_ptr[rsp + 0x28], 0);
            a.jl(yes);
        });
        yield return Case("its caller's stack below zero", (a, yes) =>
        {
            a.cmp(__qword_ptr[rsp + 0x30], 0);
            a.jl(yes);
        });
        yield return Case("where the receiver is", (a, yes) =>
        {
            a.mov(r11, rcx);
            a.shr(r11, 32);
            a.cmp(r11d, 0x1000);
            a.jae(yes);
        });
        yield return Case("where the packet is", (a, yes) =>
        {
            a.cmp(r10, 0x7FFF0000);
            a.jb(yes);
        });
        yield return Case("the packet pointer against an address it may have", (a, yes) =>
        {
            a.cmp(r10, 0x7FFF0000);
            a.je(yes);
        });
        yield return Case("the low half of the packet pointer tested for zero", (a, yes) =>
        {
            a.test(r10d, r10d);
            a.jz(yes);
        });
        yield return Case("the parity of a pointer", (a, yes) =>
        {
            a.lea(r11, __[r10 + 8]);
            a.test(r11, r11);
            a.jp(yes);
        });
        yield return Case("a pointer moved far, tested for null", (a, yes) =>
        {
            a.mov(r11, r10);
            a.sub(r11, 0x10000000);
            a.test(r11, r11);
            a.jz(yes);
        });
        yield return Case("a field of the holder the packet isn't read through, tested for null", (a, yes) =>
        {
            a.cmp(__qword_ptr[r8 + 0x20], 0);
            a.je(yes);
        });
        yield return Case("a field of the holder the packet isn't read through, read and tested for null", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x20]);
            a.test(r11, r11);
            a.jz(yes);
        });
        yield return Case("a vtable entry tested for null", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[rcx]);
            a.mov(r11, __qword_ptr[r11 + 8 * 0x800]);
            a.test(r11, r11);
            a.jz(yes);
        });
        yield return Case("another packet's pointer tested for null", (a, yes) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x18]);
            a.mov(r11, __qword_ptr[r11 + 0x38]);
            a.test(r11, r11);
            a.jz(yes);
        });
        yield return Case("a field the packet isn't read through tested for null, returning unless it is", (a, _) =>
        {
            var go = a.CreateLabel();
            a.cmp(__qword_ptr[r8 + 0x20], 0);
            a.je(go);
            a.ret();
            a.Label(ref go);
        });
        yield return Case("a bit of the packet pointer", (a, yes) =>
        {
            a.test(r10, 0x10);
            a.jnz(yes);
        });
        yield return Case("a bit of a code address the constant data holds", (a, yes) =>
        {
            a.AddInstruction(Instruction.Create(Code.Mov_r64_rm64, Register.R11, new MemoryOperand(Register.RIP, (long)FakeReceiverImage.ReadOnlyData)));
            a.bt(r11, 16);
            a.jb(yes);
        });
        yield return Case("a bit of a code address the code holds", (a, yes) =>
        {
            a.mov(r11, unchecked((long)FakeReceiverImage.Text));
            a.bt(r11, 16);
            a.jb(yes);
        });
        yield return Case("a branch Intel and AMD processors decode differently", (a, yes) =>
        {
            var no = a.CreateLabel();
            a.test(edx, 0x10000);
            a.db(0x66, 0x0F, 0x85, 0x00, 0x00, 0xEB, 0x02);
            a.jmp(no);
            a.jmp(yes);
            a.Label(ref no);
        });
        // Flags and bits as each instruction leaves them, on bits the second model's fill leaves alone.
        yield return Case("the carry of a body dword plus 1", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x10]);
            a.add(r11d, 1);
            a.jc(yes);
        });
        yield return Case("the carry of a body bit negated", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.and(r11d, 1);
            a.neg(r11d);
            a.jc(yes);
        });
        yield return Case("the overflow of a body dword incremented", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x10]);
            a.inc(r11d);
            a.jo(yes);
        });
        yield return Case("the carry of a body byte shifted left", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.shl(r11d, 25);
            a.jc(yes);
        });
        yield return Case("the carry of a body byte shifted right arithmetically", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.sar(r11d, 1);
            a.jc(yes);
        });
        yield return Case("the overflow of a body dword shifted right", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x10]);
            a.shr(r11d, 1);
            a.jo(yes);
        });
        yield return Case("the parity of a body dword", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x10]);
            a.test(r11d, r11d);
            a.jp(yes);
        });
        yield return Case("a body dword at most 5", (a, yes) =>
        {
            a.cmp(__dword_ptr[r10 + 0x10], 5);
            a.jle(yes);
        });
        yield return Case("the zero flag after a shift by a body field", (a, yes) =>
        {
            a.push(rcx);
            a.movzx(ecx, __byte_ptr[r10 + 0x10]);
            a.and(ecx, 7);
            a.mov(r11d, 1);
            a.cmp(r11d, r11d);
            a.shl(r11d, cl);
            a.pop(rcx);
            a.jnz(yes);
        });
        yield return Case("a bit of a constant a register it isn't handed picks", (a, yes) =>
        {
            a.mov(r11d, unchecked((int)0xAAAAAAAA));
            a.bt(r11d, r12d);
            a.jb(yes);
        });
        yield return Case("the sign of a body dword, spread by cdq", (a, yes) =>
        {
            a.push(rdx);
            a.mov(eax, __dword_ptr[r10 + 0x18]);
            a.cdq();
            a.test(edx, edx);
            a.pop(rdx);
            a.jnz(yes);
        });
        yield return Case("a body dword inverted", (a, yes) =>
        {
            a.mov(r11d, __dword_ptr[r10 + 0x10]);
            a.not(r11d);
            a.test(r11d, r11d);
            a.jz(yes);
        });
        yield return Case("a body byte rotated", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.rol(r11d, 4);
            a.test(r11d, 0x10);
            a.jnz(yes);
        });
        yield return Case("a body byte times 3", (a, yes) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x10]);
            a.imul(r11d, r11d, 3);
            a.test(r11d, 0x400);
            a.jnz(yes);
        });
        yield return Case("a body byte loaded into ah", (a, yes) =>
        {
            a.mov(rax, r10);
            a.mov(ah, __byte_ptr[rax + 0x10]);
            a.test(ah, 0x80);
            a.jnz(yes);
        });
    }

    [TestCaseSource(nameof(RoutedOnMore))]
    public void ADispatchRoutingOnMoreThanTheOpcodeGivesNoMap(Action<Assembler, Label> predicate)
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.Computed(0x7F00, 300, predicate).Image(), out var why);

        Assert.That(receiver, Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // The client's own dispatch shape with something mixed in before its jump table: a key in the
    // opcode (in the game it isn't 0), or one packet sent to another method on a bit of its own.
    private static IEnumerable<TestCaseData> MixedIn()
    {
        TestCaseData Case(string name, IReadOnlyDictionary<ushort, int> places, Action<Assembler, Func<int, Label>> beforeSwitch)
            => new TestCaseData(places, beforeSwitch).SetArgDisplayNames(name);
        var client = ClientReceiverMaps.Places(ClientReceiverMaps.Build20261103);

        yield return Case("a key in the packet", Places, (a, _) => a.xor(ax, __word_ptr[r10 + 0x0A]));
        yield return Case("a key in the packet, into the opcode's high byte", Places, (a, _) =>
        {
            a.movzx(r11d, __byte_ptr[r10 + 0x0F]);
            a.shl(r11d, 8);
            a.xor(eax, r11d);
        });
        for (var at = 0x38; at < 0x40; at++)
        {
            var field = at;
            yield return Case($"a key in byte {field - 0x38} of a channel field", Places, (a, _) =>
            {
                a.mov(r11, __qword_ptr[r8 + 0x10]);
                a.movzx(r11d, __byte_ptr[r11 + field]);
                a.xor(eax, r11d);
            });
        }
        foreach (var at in new[] { 0x3C, 0x3E })
            yield return Case($"a key in channel word +0x{at:X}", Places, (a, _) =>
            {
                a.mov(r11, __qword_ptr[r8 + 0x10]);
                a.xor(ax, __word_ptr[r11 + at]);
            });
        yield return Case("a key in a channel the packet isn't read through", Places, (a, _) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x18]);
            a.xor(ax, __word_ptr[r11 + 0x20]);
        });
        foreach (var at in new[] { 0x08, 0x0C, 0x0E })
            yield return Case($"a key in receiver word +0x{at:X}", Places, (a, _) => a.xor(ax, __word_ptr[rcx + at]));
        yield return Case("a key in a holder word", Places, (a, _) => a.xor(ax, __word_ptr[r8 + 0x1E]));
        yield return Case("a key added from the holder", Places, (a, _) => a.add(ax, __word_ptr[r8 + 0x10]));
        yield return Case("a key in the second argument", Places, (a, _) =>
        {
            a.mov(r11d, edx);
            a.and(r11d, 1);
            a.xor(eax, r11d);
        });
        yield return Case("game version 2026.11.03: a key in a channel word", client, (a, _) =>
        {
            a.mov(r11, __qword_ptr[r8 + 0x10]);
            a.xor(ax, __word_ptr[r11 + 0x3E]);
        });
        yield return Case("game version 2026.11.03: the notice sent on with a header bit", client, (a, caseOf) =>
        {
            var other = a.CreateLabel();
            a.cmp(eax, 0x02AB);
            a.jne(other);
            a.test(__byte_ptr[r10 + 4], 0x80);
            a.jnz(caseOf(39));
            a.Label(ref other);
        });
        yield return Case("game version 2026.11.03: the state flags sent on with a body bit", client, (a, caseOf) =>
        {
            var other = a.CreateLabel();
            a.cmp(eax, 0x007A);
            a.jne(other);
            a.test(__byte_ptr[r10 + 0x10], 0x80);
            a.jnz(caseOf(297));
            a.Label(ref other);
        });
        yield return Case("game version 2026.11.03: the ready check sent on when a body byte is 1", client, (a, caseOf) =>
        {
            var other = a.CreateLabel();
            a.cmp(eax, 0x007F);
            a.jne(other);
            a.cmp(__byte_ptr[r10 + 0x10], 1);
            a.je(caseOf(34));
            a.Label(ref other);
        });
    }

    [TestCaseSource(nameof(MixedIn))]
    public void ADispatchMixingMoreThanTheOpcodeIntoItsSwitchGivesNoMap(IReadOnlyDictionary<ushort, int> places, Action<Assembler, Func<int, Label>> beforeSwitch)
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places, beforeSwitch: beforeSwitch).Image(), out var why);

        Assert.That(receiver, Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A method the dispatch calls returns into it, having changed what the dispatch hasn't kept to
    // itself: what the case does after it may hand the packet to no second method.
    public enum AfterTheMethod
    {
        CallAnotherOnAReceiverFlag, CallAnotherOnWhatItReturns, CallAnotherOnWhatItWritesBack, CallAnotherOnFlagsSetBeforeTheCall,
        CallAnotherOnWhatTheCaseLeftInTheMethodsHomeSpace, CallAnotherOnThePacketAsItLeavesIt, JumpToAnotherThroughTheReceiver,
        JumpToTheNextThroughTheVtableItKept, ReturnIntoTheCaseThenAnotherOnAReceiverFlag,
    }

    [TestCase(AfterTheMethod.CallAnotherOnAReceiverFlag)]
    [TestCase(AfterTheMethod.CallAnotherOnWhatItReturns)]
    [TestCase(AfterTheMethod.CallAnotherOnWhatItWritesBack)]
    [TestCase(AfterTheMethod.CallAnotherOnFlagsSetBeforeTheCall)]
    [TestCase(AfterTheMethod.CallAnotherOnWhatTheCaseLeftInTheMethodsHomeSpace)]
    [TestCase(AfterTheMethod.CallAnotherOnThePacketAsItLeavesIt)]
    [TestCase(AfterTheMethod.JumpToAnotherThroughTheReceiver)]
    [TestCase(AfterTheMethod.JumpToTheNextThroughTheVtableItKept)]
    [TestCase(AfterTheMethod.ReturnIntoTheCaseThenAnotherOnAReceiverFlag)]
    public void ACaseThatHandsThePacketOnAfterItsMethodGivesNoMap(AfterTheMethod after)
    {
        var form = after switch
        {
            AfterTheMethod.CallAnotherOnAReceiverFlag => FakeReceiverImage.CaseForm.CallThenAnotherOnAReceiverFlag,
            AfterTheMethod.CallAnotherOnWhatItReturns => FakeReceiverImage.CaseForm.CallThenAnotherOnItsResult,
            AfterTheMethod.CallAnotherOnWhatItWritesBack => FakeReceiverImage.CaseForm.CallThenAnotherOnAnOutParameter,
            AfterTheMethod.CallAnotherOnFlagsSetBeforeTheCall => FakeReceiverImage.CaseForm.CallThenAnotherOnFlagsSetBefore,
            AfterTheMethod.CallAnotherOnWhatTheCaseLeftInTheMethodsHomeSpace => FakeReceiverImage.CaseForm.CallThenAnotherOnItsHomeSpace,
            AfterTheMethod.CallAnotherOnThePacketAsItLeavesIt => FakeReceiverImage.CaseForm.CallThenAnotherOnThePacket,
            AfterTheMethod.JumpToAnotherThroughTheReceiver => FakeReceiverImage.CaseForm.CallThenJumpToAnother,
            AfterTheMethod.JumpToTheNextThroughTheVtableItKept => FakeReceiverImage.CaseForm.CallThenJumpToTheNextThroughTheVtableKept,
            _ => FakeReceiverImage.CaseForm.JumpReturningIntoTheCase,
        };

        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(Places, form).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A method's address moved lands in some method's code, which is no method to hand a packet to.
    [Test]
    public void ACaseJumpingPastAMethodsAddressGivesNoMap()
    {
        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(Places, FakeReceiverImage.CaseForm.JumpPastTheMethodBefore).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A field's sign, unlike whether it's null, is the game's before the dispatch reads through it: the
    // client's own table with a case decided by it gives no map.
    [Test]
    public void TheClientsTableWithACaseDecidedByAFieldsSignGivesNoMap()
    {
        var image = FakeReceiverImage.JumpTable(ClientReceiverMaps.Places(ClientReceiverMaps.Build20261103), beforeSwitch: (a, caseOf) =>
        {
            var other = a.CreateLabel();
            a.cmp(eax, 0x02AB);
            a.jne(other);
            a.cmp(__qword_ptr[r8 + 0x18], 0);
            a.jl(caseOf(39));
            a.Label(ref other);
        });

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A method may write anything in the dispatch's stack but its return address, through pointers the
    // model may no longer know point there: a case that hands the packet to another method on what's
    // there after the call gives no map.
    [TestCase("a fifth argument")]
    [TestCase("an out-parameter handed only when a receiver flag is set")]
    [TestCase("an out-parameter whose pointer was rewritten a byte at a time")]
    [TestCase("one of two out-parameters, picked by a receiver byte")]
    [TestCase("an out-parameter in its own home space, above its return address")]
    public void ACaseThatHandsThePacketOnOnWhatItsMethodMayHaveWrittenGivesNoMap(string written)
    {
        const ushort start = 0x0100;
        const int count = 300;
        var image = new FakeReceiverImage(count);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.sub(edx, start);
            a.cmp(edx, count - 1);
            a.ja(fallback);
            a.push(rbx);
            a.sub(rsp, 0x30);
            a.mov(rbx, rcx);
            a.mov(rax, __qword_ptr[rcx]);
            a.add(edx, image.FirstMethodSlot);
            a.mov(__qword_ptr[rsp + 0x28], 0);
            var slot = 0x28;
            switch (written)
            {
                case "a fifth argument":
                    a.mov(__qword_ptr[rsp + 0x20], 0);
                    slot = 0x20;
                    break;
                case "an out-parameter handed only when a receiver flag is set":
                    a.lea(r10, __[rsp + 0x28]);
                    a.xor(r9d, r9d);
                    a.test(__byte_ptr[rbx + 0x20], 1);
                    a.cmovne(r9, r10);
                    break;
                case "an out-parameter whose pointer was rewritten a byte at a time":
                    a.lea(r9, __[rsp + 0x28]);
                    a.mov(r9b, r9b);
                    break;
                case "one of two out-parameters, picked by a receiver byte":
                    a.mov(__qword_ptr[rsp + 0x20], 0);
                    a.movzx(r11d, __byte_ptr[rbx + 0x21]);
                    a.and(r11d, 1);
                    a.lea(r9, __[rsp + r11 * 8 + 0x20]);
                    break;
                default:
                    a.mov(__qword_ptr[rsp + 0x40], 0);
                    a.lea(r9, __[rsp + 0x40]);
                    a.mov(r9b, r9b);
                    slot = 0x40;
                    break;
            }
            a.call(__qword_ptr[rax + rdx * 8]);
            a.cmp(__qword_ptr[rsp + slot], 0);
            a.je(done);
            a.mov(rax, __qword_ptr[rbx]);
            a.mov(rcx, rbx);
            a.call(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref done);
            a.add(rsp, 0x30);
            a.pop(rbx);
            a.ret();
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // Opcode 0x7F00 + i reaches method i through a call on a 0x40-byte frame; `after` may jump to
    // `done`, and falling through hands the packet to method 0 as well.
    private static FakeReceiverImage Calling(Action<Assembler> before, Action<Assembler, Label> after)
    {
        var image = new FakeReceiverImage(300);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.sub(edx, 0x7F00);
            a.cmp(edx, 299);
            a.ja(fallback);
            a.push(rbx);
            a.sub(rsp, 0x40);
            a.mov(rbx, rcx);
            before(a);
            a.mov(rax, __qword_ptr[rcx]);
            a.call(__qword_ptr[rax + rdx * 8 + 8 * image.FirstMethodSlot]);
            after(a, done);
            a.mov(rax, __qword_ptr[rbx]);
            a.mov(rcx, rbx);
            a.call(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref done);
            a.add(rsp, 0x40);
            a.pop(rbx);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // The same, reaching its method by a jump that returns into the case, which then branches on r10.
    private static FakeReceiverImage JumpReturning()
    {
        var image = new FakeReceiverImage(300);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            var back = a.CreateLabel();
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.sub(edx, 0x7F00);
            a.cmp(edx, 299);
            a.ja(fallback);
            a.push(rbx);
            a.mov(rbx, rcx);
            a.sub(rsp, 0x20);
            a.mov(r10d, 1);
            a.lea(r11, __[back]);
            a.push(r11);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + rdx * 8 + 8 * image.FirstMethodSlot]);
            a.Label(ref back);
            a.add(rsp, 0x20);
            a.test(r10d, r10d);
            a.jnz(done);
            a.mov(rax, __qword_ptr[rbx]);
            a.mov(rcx, rbx);
            a.pop(rbx);
            a.jmp(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref done);
            a.pop(rbx);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);
        return image;
    }

    // What a method may have changed by the time it returns into the case: the volatile registers, what
    // the objects hold, and the stack a pointer it's handed reaches. A case that hands the packet on to
    // another method on any of it gives no map, and so does one that hands a method a pointer to its own
    // return address, which the run can't follow.
    private static IEnumerable<TestCaseData> ChangedByTheMethod()
    {
        // As object: the fake is internal, and the test method public.
        TestCaseData Case(string name, Func<FakeReceiverImage> image) => new TestCaseData((object)image).SetArgDisplayNames(name);
        void Always(Assembler a, Label done) => a.jmp(done);

        yield return Case("a volatile register set before the call", () => Calling(a => a.mov(r11d, 1), (a, done) =>
        {
            a.test(r11d, r11d);
            a.jnz(done);
        }));
        yield return Case("the holder register after the call", () => Calling(_ => { }, (a, done) =>
        {
            a.test(r8, r8);
            a.jnz(done);
        }));
        yield return Case("a holder field, re-read after the call through a register the call keeps", () => Calling(a => a.mov(r12, r8), (a, done) =>
        {
            a.mov(r11, __qword_ptr[r12 + 0x10]);
            a.test(r11, r11);
            a.jnz(done);
        }));
        yield return Case("a frame slot handed as the fifth argument", () => Calling(a =>
        {
            a.mov(__qword_ptr[rsp + 0x30], 0);
            a.lea(r11, __[rsp + 0x30]);
            a.mov(__qword_ptr[rsp + 0x20], r11);
        }, (a, done) =>
        {
            a.cmp(__qword_ptr[rsp + 0x30], 0);
            a.je(done);
        }));
        yield return Case("a frame slot handed in r9", () => Calling(a =>
        {
            a.mov(__qword_ptr[rsp + 0x30], 0);
            a.lea(r9, __[rsp + 0x30]);
        }, (a, done) =>
        {
            a.cmp(__qword_ptr[rsp + 0x30], 0);
            a.je(done);
        }));
        yield return Case("a volatile register set before a jump that returns into the case", JumpReturning);
        yield return Case("a pointer to its return address handed as the fifth argument", () => Calling(a =>
        {
            a.lea(r11, __[rsp + 0x48]);
            a.mov(__qword_ptr[rsp + 0x20], r11);
        }, Always));
        yield return Case("a pointer to its return address handed in r9", () => Calling(a => a.lea(r9, __[rsp + 0x48]), Always));
        yield return Case("a pointer to its return address handed in r8", () => Calling(a => a.lea(r8, __[rsp + 0x48]), Always));
    }

    [TestCaseSource(nameof(ChangedByTheMethod))]
    public void ACaseThatHandsThePacketOnOnWhatItsMethodChangedGivesNoMap(object image)
    {
        Assert.That(ProtocolReceiver.Read(((Func<FakeReceiverImage>)image)().Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A method's address moved back lands before it, in some other method's code.
    [Test]
    public void ACaseJumpingBeforeTheNextMethodsAddressGivesNoMap()
    {
        var image = new FakeReceiverImage(300);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.sub(edx, 0x7F00);
            a.cmp(edx, 299);
            a.ja(fallback);
            a.mov(rax, __qword_ptr[rcx]);
            a.mov(rax, __qword_ptr[rax + rdx * 8 + 8 * (image.FirstMethodSlot + 1)]);
            a.sub(rax, 0x10);
            a.jmp(rax);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // A method owns its stack parameters as it owns its home space: a case that hands the packet to
    // another method on what's there after the call gives no map, and one that only returns maps.
    [TestCase(false)]
    [TestCase(true)]
    public void ACaseReadingItsMethodsStackParameterBackAfterTheCall(bool handsOnOnIt)
    {
        const ushort start = 0x100;
        const int count = 400;
        var image = new FakeReceiverImage(count + 1);
        var (dispatch, _) = image.Assemble(a =>
        {
            var first = a.CreateLabel();
            var fallback = a.CreateLabel();
            var done = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.cmp(edx, 1);
            a.je(first);
            a.sub(edx, start);
            a.cmp(edx, count - 1);
            a.ja(fallback);
            a.push(rbx);
            a.sub(rsp, 0x30);
            a.mov(rbx, rcx);
            a.mov(__qword_ptr[rsp + 0x20], 0);
            a.mov(rax, __qword_ptr[rcx]);
            a.add(edx, image.FirstMethodSlot + 1);
            a.call(__qword_ptr[rax + rdx * 8]);
            if (handsOnOnIt)
            {
                a.cmp(__qword_ptr[rsp + 0x20], 0);
                a.je(done);
                a.mov(rax, __qword_ptr[rbx]);
                a.mov(rcx, rbx);
                a.call(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            }
            a.Label(ref done);
            a.add(rsp, 0x30);
            a.pop(rbx);
            a.ret();
            a.Label(ref first);
            a.mov(rax, __qword_ptr[rcx]);
            a.jmp(__qword_ptr[rax + 8 * image.FirstMethodSlot]);
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);

        var receiver = ProtocolReceiver.Read(image.Image(), out var why);

        if (handsOnOnIt)
        {
            Assert.That(receiver, Is.Null);
            return;
        }
        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, Enumerable.Range(0, count).ToDictionary(i => (ushort)(start + i), i => i + 1).Append(new KeyValuePair<ushort, int>(1, 0)).ToDictionary(), methods: count + 1);
    }

    // A method may take its payload as a copy in the case's frame, handed over by pointer, and change
    // any of that frame; a pointer to the return address or past it is the caller's.
    [TestCase(0x20, true)]
    [TestCase(0x38, false)]
    public void ACaseHandingItsMethodACopyInItsFrame(int handedAt, bool mapped)
    {
        const ushort start = 0x100;
        const int count = 400;
        var image = new FakeReceiverImage(count);
        var (dispatch, _) = image.Assemble(a =>
        {
            var fallback = a.CreateLabel();
            FakeReceiverImage.ReadOpcode(a, edx);
            a.sub(edx, start);
            a.cmp(edx, count - 1);
            a.ja(fallback);
            a.sub(rsp, 0x38);
            a.lea(rax, __[r10 + 0x10]);
            a.mov(__qword_ptr[rsp + 0x20], rax);
            a.mov(__qword_ptr[rsp + 0x28], 0x40);
            a.lea(r9, __[rsp + handedAt]);
            a.mov(rax, __qword_ptr[rcx]);
            a.add(edx, image.FirstMethodSlot);
            a.call(__qword_ptr[rax + rdx * 8]);
            a.add(rsp, 0x38);
            a.ret();
            a.Label(ref fallback);
            a.ret();
        });
        image.SetDispatch(dispatch);

        var receiver = ProtocolReceiver.Read(image.Image(), out var why);

        if (!mapped)
        {
            Assert.That(receiver, Is.Null);
            return;
        }
        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, Enumerable.Range(0, count).ToDictionary(i => (ushort)(start + i), i => i), methods: count);
    }

    // A qword read across two fields of the holder or a channel is no pointer the dispatch is handed.
    [TestCase(0x14, 0x38)]
    [TestCase(0x10, 0x3C)]
    public void APacketReadThroughAFieldAcrossTwoGivesNoMap(int holderField, int channelField)
    {
        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(Places, holderField: holderField, channelField: channelField).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // Where the loader puts the module is the game's: a dispatch routing on a bit of its own address
    // gives no map, from the exe on disk or as loaded.
    [TestCase(16)]
    [TestCase(32)]
    public void ADispatchRoutingOnWhereTheModuleIsGivesNoMap(int bit)
    {
        var image = FakeReceiverImage.Computed(0x7F00, 300, (a, yes) =>
        {
            FakeReceiverImage.LoadImageBase(a, Register.R11);
            a.bt(r11, (byte)bit);
            a.jb(yes);
        });
        var module = Marshal.AllocHGlobal((nint)FakeReceiverImage.ExeIdentity.ImageSize);
        try
        {
            image.LoadAt(module, image.ExeFile());

            Assert.That(ProtocolReceiver.Read(ModuleImage.FromFile(image.ExeFile(), out _), out _), Is.Null);
            Assert.That(ProtocolReceiver.Read(ModuleImage.Loaded(module, out _), out _), Is.Null);
        }
        finally
        {
            Marshal.FreeHGlobal(module);
        }
    }

    // Part of an absolute address in the constant data is where the loader put the module too.
    [TestCase(16)]
    [TestCase(20)]
    [TestCase(24)]
    public void ADispatchRoutingOnPartOfAnAddressTheLoaderMovesGivesNoMap(int bit)
    {
        var image = FakeReceiverImage.Computed(0x7F00, 300, (a, yes) =>
        {
            a.AddInstruction(Instruction.Create(Code.Mov_r32_rm32, Register.R11D, new MemoryOperand(Register.RIP, (long)FakeReceiverImage.ReadOnlyData)));
            a.bt(r11d, (byte)bit);
            a.jb(yes);
        });

        Assert.That(ProtocolReceiver.Read(ModuleImage.FromFile(image.ExeFile(), out _), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    [Test]
    public void TwoOpcodesForOneMethodGiveNoMap()
    {
        var places = new Dictionary<ushort, int>(Places) { [0x3F0] = Places.Values.First() };

        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // Its methods are the receiver's own: a dispatch that sends packets past them isn't this receiver.
    [Test]
    public void ADispatchReachingPastItsVtableGivesNoMap()
    {
        var places = new Dictionary<ushort, int>(Places) { [Places.Keys.First()] = 900 };

        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places, methods: Places.Count).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    [Test]
    public void AModuleWithNoTableOfEmptyMethodsHasNoReceiver()
    {
        var places = FakeReceiverImage.ShuffledPlaces(ProtocolReceiver.MinMethods - 1);

        Assert.That(ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places).Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // The packets the party watch reads, at their places in the revision PartyPacketMethods knows.
    [Test]
    public void TheRevisionsTableNamesThePartyPacketsOpcodes()
    {
        var known = PartyPacketMethods.ByRevision[775];
        var places = FakeReceiverImage.ShuffledPlaces(775);
        ushort OpcodeAt(int place) => places.Single(p => p.Value == place).Key;
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(places).Image(), out var why);
        Assert.That(receiver, Is.Not.Null, why);

        var opcodes = PartyPacketMethods.For(receiver!);

        Assert.That(opcodes, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(opcodes!.PlayerStateFlags, Is.EqualTo(OpcodeAt(known.PlayerStateFlags)));
            Assert.That(opcodes.OnlineStatus, Is.EqualTo(OpcodeAt(known.OnlineStatus)));
            Assert.That(opcodes.ActorControl, Is.EquivalentTo(known.ActorControl.Select(OpcodeAt)));
            Assert.That(opcodes.Notice, Is.EqualTo(OpcodeAt(known.Notice)));
            Assert.That(opcodes.Roster, Is.EqualTo(OpcodeAt(known.Roster)));
            Assert.That(opcodes.MemberEvent, Is.EqualTo(OpcodeAt(known.MemberEvent)));
            Assert.That(opcodes.MemberLeft, Is.EqualTo(OpcodeAt(known.MemberLeft)));
            Assert.That(opcodes.MemberRemoved, Is.EqualTo(OpcodeAt(known.MemberRemoved)));
            Assert.That(opcodes.Disbanded, Is.EqualTo(OpcodeAt(known.Disbanded)));
            Assert.That(opcodes.MemberStatus, Is.EqualTo(OpcodeAt(known.MemberStatus)));
            Assert.That(opcodes.PartyList, Is.EqualTo(OpcodeAt(known.PartyList)));
            Assert.That(opcodes.PartyKey, Is.EqualTo(OpcodeAt(known.PartyKey)));
            Assert.That(opcodes.PartyView, Is.EquivalentTo(known.PartyView.Select(OpcodeAt)));
            Assert.That(opcodes.PartyEvent, Is.EqualTo(OpcodeAt(known.PartyEvent)));
            Assert.That(opcodes.CrossWorldReadyCheck, Is.EqualTo(OpcodeAt(known.CrossWorldReadyCheck)));
            Assert.That(opcodes.CrossWorldReadyCheckAnswer, Is.EqualTo(OpcodeAt(known.CrossWorldReadyCheckAnswer)));
        });
    }

    // The client's own receiver map: the revision's table names the opcodes the client uses.
    [Test]
    public void OnGameVersion20261103TheTableNamesThePartyPacketsOpcodes()
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(ClientReceiverMaps.Places(ClientReceiverMaps.Build20261103)).Image(), out var why);
        Assert.That(receiver, Is.Not.Null, why);

        var opcodes = PartyPacketMethods.For(receiver!);

        Assert.That(opcodes, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(opcodes!.PlayerStateFlags, Is.EqualTo(0x007A));
            Assert.That(opcodes.OnlineStatus, Is.EqualTo(0x0280));
            Assert.That(opcodes.ActorControl, Is.EquivalentTo(new ushort[] { 0x02DF, 0x00A9, 0x0100 }));
            Assert.That(opcodes.Notice, Is.EqualTo(0x02AB));
            Assert.That(opcodes.Roster, Is.EqualTo(0x0358));
            Assert.That(opcodes.MemberEvent, Is.EqualTo(0x0296));
            Assert.That(opcodes.MemberLeft, Is.EqualTo(0x0197));
            Assert.That(opcodes.MemberRemoved, Is.EqualTo(0x035F));
            Assert.That(opcodes.Disbanded, Is.EqualTo(0x0290));
            Assert.That(opcodes.MemberStatus, Is.EqualTo(0x0365));
            Assert.That(opcodes.PartyList, Is.EqualTo(0x03E3));
            Assert.That(opcodes.PartyKey, Is.EqualTo(0x02F5));
            Assert.That(opcodes.PartyEvent, Is.EqualTo(0x0145));
            Assert.That(opcodes.CrossWorldReadyCheck, Is.EqualTo(0x007F));
            Assert.That(opcodes.CrossWorldReadyCheckAnswer, Is.EqualTo(0x032F));
        });
    }

    // The two game versions before it, on the same revision.
    [Test]
    public void OnGameVersion20261016TheTableNamesThePartyPacketsOpcodes()
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(ClientReceiverMaps.Places(ClientReceiverMaps.Build20261016)).Image(), out var why);
        Assert.That(receiver, Is.Not.Null, why);

        var opcodes = PartyPacketMethods.For(receiver!);

        Assert.That(opcodes, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(opcodes!.PlayerStateFlags, Is.EqualTo(0x00C2));
            Assert.That(opcodes.OnlineStatus, Is.EqualTo(0x0253));
            Assert.That(opcodes.ActorControl, Is.EquivalentTo(new ushort[] { 0x0229, 0x0101, 0x022B }));
            Assert.That(opcodes.Notice, Is.EqualTo(0x02DD));
            Assert.That(opcodes.Roster, Is.EqualTo(0x0180));
            Assert.That(opcodes.MemberEvent, Is.EqualTo(0x0355));
            Assert.That(opcodes.MemberLeft, Is.EqualTo(0x01C8));
            Assert.That(opcodes.MemberRemoved, Is.EqualTo(0x01A4));
            Assert.That(opcodes.Disbanded, Is.EqualTo(0x02B8));
            Assert.That(opcodes.MemberStatus, Is.EqualTo(0x01F6));
            Assert.That(opcodes.PartyList, Is.EqualTo(0x02E7));
            Assert.That(opcodes.PartyKey, Is.EqualTo(0x034E));
            Assert.That(opcodes.PartyEvent, Is.EqualTo(0x0328));
            Assert.That(opcodes.CrossWorldReadyCheck, Is.EqualTo(0x03AD));
            Assert.That(opcodes.CrossWorldReadyCheckAnswer, Is.EqualTo(0x0282));
        });
    }

    [Test]
    public void OnGameVersion20261006TheTableNamesThePartyPacketsOpcodes()
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(ClientReceiverMaps.Places(ClientReceiverMaps.Build20261006)).Image(), out var why);
        Assert.That(receiver, Is.Not.Null, why);

        var opcodes = PartyPacketMethods.For(receiver!);

        Assert.That(opcodes, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(opcodes!.PlayerStateFlags, Is.EqualTo(0x0326));
            Assert.That(opcodes.OnlineStatus, Is.EqualTo(0x013D));
            Assert.That(opcodes.ActorControl, Is.EquivalentTo(new ushort[] { 0x00C8, 0x0198, 0x01E6 }));
            Assert.That(opcodes.Notice, Is.EqualTo(0x008F));
            Assert.That(opcodes.Roster, Is.EqualTo(0x0157));
            Assert.That(opcodes.MemberEvent, Is.EqualTo(0x032A));
            Assert.That(opcodes.MemberLeft, Is.EqualTo(0x00D5));
            Assert.That(opcodes.MemberRemoved, Is.EqualTo(0x02BC));
            Assert.That(opcodes.Disbanded, Is.EqualTo(0x02CB));
            Assert.That(opcodes.MemberStatus, Is.EqualTo(0x014F));
            Assert.That(opcodes.PartyList, Is.EqualTo(0x016A));
            Assert.That(opcodes.PartyKey, Is.EqualTo(0x02FE));
            Assert.That(opcodes.PartyEvent, Is.EqualTo(0x03DC));
            Assert.That(opcodes.CrossWorldReadyCheck, Is.EqualTo(0x03DD));
            Assert.That(opcodes.CrossWorldReadyCheckAnswer, Is.EqualTo(0x0363));
        });
    }

    // An expansion inserts packets, so its places are numbered anew.
    [Test]
    public void ARevisionWithAnotherMethodCountNamesNoPartyPacket()
    {
        var receiver = ProtocolReceiver.Read(FakeReceiverImage.JumpTable(FakeReceiverImage.ShuffledPlaces(776)).Image(), out var why);
        Assert.That(receiver, Is.Not.Null, why);

        Assert.That(PartyPacketMethods.For(receiver!), Is.Null);
    }

    // Another protocol's receiver in the module too: only a revision the table knows is read, and
    // two of them give no map.
    [TestCase(300, true)]
    [TestCase(775, false)]
    public void AnotherReceiverBesideTheZoneOne(int otherMethods, bool read)
    {
        var zone = FakeReceiverImage.ShuffledPlaces(775);
        var image = FakeReceiverImage.JumpTable(FakeReceiverImage.ShuffledPlaces(otherMethods, seed: 9));
        image.AddReceiver(zone);

        var receiver = ProtocolReceiver.Read(image.Image(), out var why);

        if (!read)
        {
            Assert.That(receiver, Is.Null);
            return;
        }
        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, zone, methods: 775);
    }

    // The zone receiver is the largest: a smaller one beside it is no zone receiver, whether the larger
    // one maps or the model refuses it.
    [TestCase(776, 775, false)]
    [TestCase(776, 775, true)]
    [TestCase(775, 300, true)]
    public void AReceiverBesideALargerOneGivesNoMap(int largerMethods, int otherMethods, bool largerRefused)
    {
        Action<Assembler, Func<int, Label>>? refused = largerRefused ? (a, _) => a.movups(xmm0, __xmmword_ptr[r10]) : null;
        var image = FakeReceiverImage.JumpTable(FakeReceiverImage.ShuffledPlaces(largerMethods, seed: 1), beforeSwitch: refused);
        image.AddReceiver(FakeReceiverImage.ShuffledPlaces(otherMethods, seed: 2));

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // The exe on disk, mapped where its code expects to be: the same map as the module.
    [Test]
    public void AnExeFileMapsLikeItsLoadedModule()
    {
        var file = FakeReceiverImage.JumpTable(Places).ExeFile();

        var receiver = ProtocolReceiver.Read(ModuleImage.FromFile(file, out var identity), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, Places, methods: 400);
        Assert.That(identity, Is.EqualTo(FakeReceiverImage.ExeIdentity));
    }

    // The module as the loader maps it, away from the base its code was built for.
    [Test]
    public void TheLoadedModuleMapsLikeItsExe()
    {
        var image = FakeReceiverImage.JumpTable(Places);
        var module = Marshal.AllocHGlobal((nint)FakeReceiverImage.ExeIdentity.ImageSize);
        try
        {
            image.LoadAt(module, image.ExeFile());

            var receiver = ProtocolReceiver.Read(ModuleImage.Loaded(module, out var identity), out var why);

            Assert.That(receiver, Is.Not.Null, why);
            AssertMap(receiver!, Places, methods: 400);
            Assert.That(identity, Is.EqualTo(FakeReceiverImage.ExeIdentity));
        }
        finally
        {
            Marshal.FreeHGlobal(module);
        }
    }

    // Another vtable of the same dispatch, for a class deriving from the receiver, has fewer empty
    // methods where it overrides one: before the receiver's or after it, it doesn't hide the receiver.
    [TestCase(false)]
    [TestCase(true)]
    public void ADerivedVtableDoesNotHideTheReceiver(bool placedFirst)
    {
        var image = FakeReceiverImage.JumpTable(Places, layout: new FakeReceiverImage.Layout(VtableAt: placedFirst ? 0x4000 : 0));
        image.AddDerivedVtable(methods: 400, overridden: 300, at: placedFirst ? FakeReceiverImage.ReadOnlyData : null);

        var receiver = ProtocolReceiver.Read(image.Image(), out var why);

        Assert.That(receiver, Is.Not.Null, why);
        AssertMap(receiver!, Places, methods: 400);
    }

    // One with more, where its own next virtual is empty, is as much a receiver a method longer whose
    // last method no opcode reaches.
    [TestCase(false)]
    [TestCase(true)]
    public void ADerivedVtableWithAnEmptyVirtualOfItsOwnGivesNoMap(bool placedFirst)
    {
        var image = FakeReceiverImage.JumpTable(Places, layout: new FakeReceiverImage.Layout(VtableAt: placedFirst ? 0x4000 : 0));
        image.AddDerivedVtable(methods: 400, emptyVirtualAfter: true, at: placedFirst ? FakeReceiverImage.ReadOnlyData : null);

        Assert.That(ProtocolReceiver.Read(image.Image(), out var why), Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    // The exe on disk holds none of the hooks other plugins may have put in the loaded code, but a
    // file of another build would map another build's opcodes.
    [TestCase(0u, 0u, true)]
    [TestCase(1u, 0u, false)]
    [TestCase(0u, 0x1000u, false)]
    public void TheExeOnDiskIsReadOnlyForTheBuildRunning(uint otherTimeStamp, uint otherImageSize, bool fromDisk)
    {
        var onDisk = FakeReceiverImage.ShuffledPlaces(400, seed: 8);
        var running = FakeReceiverImage.JumpTable(Places);
        var module = Marshal.AllocHGlobal((nint)FakeReceiverImage.ExeIdentity.ImageSize);
        var path = Path.GetTempFileName();
        try
        {
            running.LoadAt(module, running.ExeFile());
            File.WriteAllBytes(path, FakeReceiverImage.JumpTable(onDisk).ExeFile(
                FakeReceiverImage.ExeIdentity.TimeStamp + otherTimeStamp, FakeReceiverImage.ExeIdentity.ImageSize + otherImageSize));

            var receiver = ProtocolReceiver.Read(ModuleImage.OfRunningGame(module, path, out _), out var why);

            Assert.That(receiver, Is.Not.Null, why);
            AssertMap(receiver!, fromDisk ? onDisk : Places, methods: 400);
        }
        finally
        {
            Marshal.FreeHGlobal(module);
            File.Delete(path);
        }
    }

    private static void AssertMap(ProtocolReceiver receiver, IReadOnlyDictionary<ushort, int> expected, int methods)
    {
        int Expected(ushort opcode) => expected.TryGetValue(opcode, out var place) ? place : -1;
        var wrong = Enumerable.Range(0, 0x10000)
            .Select(o => (ushort)o)
            .Where(opcode => receiver.MethodOf(opcode) != Expected(opcode))
            .Take(5)
            .Select(opcode => $"0x{opcode:X4}: place {receiver.MethodOf(opcode)}, not {Expected(opcode)}")
            .ToArray();
        Assert.That(wrong, Is.Empty);
        Assert.That(receiver.Methods, Is.EqualTo(methods));
    }
}
