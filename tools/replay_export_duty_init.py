"""Extract what a duty's server sends the client on entering it from ARealmRecorded .dat
replays, and emit the C# file a zone's IZone.DutyInitData points at:

  - the entrance barrier: the spawn of the first EObj the director later opens with
    ActorControl 0x6A (p1 = 7) at commence;
  - the helper pool: the invisible helpers (ModelChara 480) of one generic BNpcBase the server
    spawns at the start of every pull, the same count each time;
  - the MapEffect snapshot: every slot's resting state, from the first snapshot packet;
  - RSV (text) and RSF (file path) records: ARealmRecorded's pseudo-opcodes 0xF001/0xF002.

Opcodes change every game build, so the barrier and snapshot packets are found by shape, not by
opcode: payload size, which kind of actor sends them, and what they carry. A replay is a whole
lockout, so it always holds the zone-in.

Usage:
    python tools/replay_export_duty_init.py <replay.dat> [<replay2.dat> ...] [--rs <replay.dat> ...] <output.cs>

The class name comes from the output filename stem and the namespace from its path (the AnoMech
project dir down to the file's folder); override with --class <Name> / --namespace <A.B.C>.
Several replays of the same duty are merged: RSV/RSF records are unioned, and the barrier, helper
pool and snapshot must agree. A --rs replay only adds RSV/RSF records: one recorded in another client
language carries that language's strings, and need not reach the pull.
"""
import base64, collections, math, os, re, struct, sys

DATA_START = 0x36C
OP_RSV = 0xF001
OP_RSF = 0xF002
RSF_LEN = 0x48  # bytes RsfReceive consumes per record

PLAYER, ENEMY = 0x1, 0x4
AC_DIRECTOR = 0x69          # on party members: p1 = the instance director's id (0x8003xxxx)
AC_DIRECTOR_EOBJ_MOD = 0x6A
BARRIER_OPEN = 7
HELPER_MODEL = 480
PULL_GAP_MS = 3000          # a pull's helper spawns all land within this of each other


def u16(b, o): return struct.unpack_from("<H", b, o)[0]
def u32(b, o): return struct.unpack_from("<I", b, o)[0]


def read_segments(path):
    with open(path, "rb") as f:
        buf = f.read()
    if buf[:11] != b"FFXIVREPLAY":
        sys.exit(f"{path}: not a FFXIVREPLAY file")
    segs = []
    off, end = DATA_START, DATA_START + struct.unpack_from("<i", buf, 0x48)[0]
    while off + 12 <= end:
        opcode, length, ms, actor = struct.unpack_from("<HHII", buf, off)
        if off + 12 + length > end:
            break
        segs.append((opcode, ms, actor, buf[off + 12:off + 12 + length]))
        off += 12 + length
    return segs


# ---------------------------------------------------------------- opcode identification

def actor_control_op(by_op):
    # ActorControl: 24 bytes, dozens of small categories, tethers (0x23) or 0x36 among them.
    best = None
    for op, v in by_op.items():
        if len(v[0][2]) != 24:
            continue
        cats = collections.Counter(u16(b, 0) for _, _, b in v)
        if sum(c for k, c in cats.items() if k < 0x800) < 0.99 * len(v) or not (0x23 in cats or 0x36 in cats):
            continue
        if best is None or len(cats) > best[0]:
            best = (len(cats), op)
    return best and best[1]


def object_spawn_op(by_op):
    # ObjectSpawn: 64 bytes on the EObj itself, which carries its own entity id at 0x8.
    cands = [op for op, v in by_op.items() if len(v[0][2]) == 64 and {a >> 28 for _, a, _ in v} == {ENEMY}
             and sum(u32(b, 8) == a for _, a, b in v) >= 0.95 * len(v)]
    return max(cands, key=lambda op: len(by_op[op]), default=None)


def npc_spawn_op(by_op):
    # NpcSpawn: 656 bytes on the spawned enemy, BNpcName row at 0x44.
    cands = [op for op, v in by_op.items() if len(v[0][2]) == 656 and {a >> 28 for _, a, _ in v} == {ENEMY}
             and sum(u32(b, 0x44) < 100000 for _, _, b in v) >= 0.95 * len(v)]
    return max(cands, key=lambda op: len(by_op[op]), default=None)


def snapshot_ops(by_op, director_ids):
    # MapEffect snapshot: {u32 director id, u16 header, u16 state[slot]...}, on the player.
    return {op for op, v in by_op.items() if len(v[0][2]) >= 0x40 and {a >> 28 for _, a, _ in v} == {PLAYER}
            and all(u32(b, 0) in director_ids and u16(b, 4) <= 7 for _, _, b in v)}


# ---------------------------------------------------------------- extraction

def segments_by_op(path):
    by_op = collections.defaultdict(list)
    for op, ms, actor, b in read_segments(path):
        by_op[op].append((ms, actor, b))
    return by_op


def parse_replay(path, rsv, rsf):
    by_op = segments_by_op(path)
    add_rs(by_op, rsv, rsf)
    ac, spawn, npc = actor_control_op(by_op), object_spawn_op(by_op), npc_spawn_op(by_op)
    if ac is None or spawn is None or npc is None:
        missing = 'ActorControl' if ac is None else 'ObjectSpawn' if spawn is None else 'NpcSpawn'
        sys.exit(f"{path}: no {missing} packet found")
    return barrier(path, by_op, ac, spawn), helper_pool(path, by_op[npc]), snapshot(by_op, ac)


def add_rs(by_op, rsv, rsf):
    for _, _, p in by_op.get(OP_RSV, []):
        if len(p) < 4 + 0x30:
            continue
        key = p[4:4 + 0x30].split(b"\x00", 1)[0].decode("ascii", "replace")
        if key:
            rsv[key] = p[:4 + 0x30 + u32(p, 0)]   # last-writer-wins on union
    for _, _, p in by_op.get(OP_RSF, []):
        if len(p) >= RSF_LEN:
            rsf[p[:RSF_LEN]] = None                  # dedup exact records


def barrier(path, by_op, ac, spawn):
    opened = {a for _, a, b in by_op[ac] if u16(b, 0) == AC_DIRECTOR_EOBJ_MOD and u32(b, 4) == BARRIER_OPEN}
    for _, a, b in by_op[spawn]:
        if a in opened:
            x, y, z = struct.unpack_from("<fff", b, 0x34)
            rotation = u16(b, 0x22) / 65535 * 2 * math.pi - math.pi
            return u32(b, 4), u32(b, 0xC), u32(b, 0x18), (x, y, z), rotation
    sys.exit(f"{path}: no spawned EObj is opened with ActorControl 0x{AC_DIRECTOR_EOBJ_MOD:X} p1={BARRIER_OPEN}")


def helper_pool(path, spawns):
    # BNpcBase at 0x40, ModelChara at 0x70. Other helpers of their own BNpcBase are dedicated actors.
    bases = collections.Counter(u32(b, 0x40) for _, _, b in spawns if u16(b, 0x70) == HELPER_MODEL)
    if not bases:
        return 0, 0
    base = bases.most_common(1)[0][0]
    pulls = []
    for ms in sorted(ms for ms, _, b in spawns if u32(b, 0x40) == base):
        if pulls and ms - pulls[-1][-1] < PULL_GAP_MS:
            pulls[-1].append(ms)
        else:
            pulls.append([ms])
    counts = {len(p) for p in pulls}
    if len(counts) != 1:
        sys.exit(f"{path}: helper pool of BNpcBase {base} differs between pulls: {[len(p) for p in pulls]}")
    return base, counts.pop()


def snapshot(by_op, ac):
    director_ids = {u32(b, 4) for _, _, b in by_op[ac] if u16(b, 0) == AC_DIRECTOR and u32(b, 4) >> 16 == 0x8003}
    packets = sorted((ms, b) for op in snapshot_ops(by_op, director_ids) for ms, _, b in by_op[op])
    if not packets:
        return []
    b = packets[0][1]
    states = [u16(b, o) for o in range(6, len(b) - 1, 2)]
    while states and states[-1] == 0:   # slots past the list get state 0
        states.pop()
    return states


# ---------------------------------------------------------------- C#

def rsv_sort_key(key):
    m = re.match(r"_rsv_(\d+)_", key)
    return (int(m.group(1)) if m else 1 << 62, key)


def preview_comment(b):
    """Short readable hint for a base64 row, control bytes shown as dots."""
    s = b.decode("utf-8", "replace")
    s = "".join(c if 0x20 <= ord(c) < 0x7F else "." for c in s)
    s = s.replace("*/", "* /")
    return (s[:80] + "...") if len(s) > 80 else s


def flt(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return ("0" if s in ("-0", "") else s) + "f"


def emit(barrier_, pool, states, rsv, rsf, out_path, class_name, namespace):
    rsv_rows = [(base64.b64encode(rsv[key]).decode("ascii"), preview_comment(rsv[key][4 + 0x30:]))
                for key in sorted(rsv, key=rsv_sort_key)]
    rsf_rows = [(base64.b64encode(rec).decode("ascii"), preview_comment(rec)) for rec in rsf]
    eobj, eobj_layout, wall_layout, (x, y, z), rotation = barrier_

    L = []
    w = L.append
    w("// <auto-generated>")
    w("//   Produced by tools/replay_export_duty_init.py")
    w(f"//   MapEffect slots: {len(states)}  |  RSV: {len(rsv_rows)} records  |  RSF: {len(rsf_rows)} records")
    w("//   Re-run the extractor against a newer replay to refresh; do not edit by hand.")
    w("// </auto-generated>")
    if rsv_rows or rsf_rows:
        w("using System;")
    w("using System.Numerics;")
    w("using AnoMech.Core.Map;")
    w("")
    w(f"namespace {namespace};")
    w("")
    w("// What the duty's server sends on entering it, captured from replays. Scenarios run inn-only, so")
    w("// the server never sends it.")
    w(f"public static class {class_name}")
    w("{")
    if rsv_rows:
        w("    // RSV payloads: [u32 valueLength][0x30-byte key][value].")
        w("    private static readonly string[] RsvRecords =")
        w("    [")
        for b64, prev in rsv_rows:
            w(f'        "{b64}", // {prev}')
        w("    ];")
        w("")
    if rsf_rows:
        w("    // RSF payloads: fixed 0x48-byte records.")
        w("    private static readonly string[] RsfRecords =")
        w("    [")
        for b64, prev in rsf_rows:
            w(f'        "{b64}", // {prev}')
        w("    ];")
        w("")
        w("    // After the records: static fields initialize in textual order.")
    w("    public static readonly DutyInitData DutyInitData = new(")
    w(f"        new EntranceBarrier({eobj}, 0x{eobj_layout:X}, 0x{wall_layout:X}, "
      f"new Vector3({flt(x)}, {flt(y)}, {flt(z)}), {flt(rotation)}),")
    w(f"        new HelperPool({pool[0]}, {pool[1]}),")
    w(f"        [{', '.join(map(str, states))}],")
    w("        " + ("Array.ConvertAll(RsvRecords, Convert.FromBase64String)," if rsv_rows else "[],"))
    w("        " + ("Array.ConvertAll(RsfRecords, Convert.FromBase64String));" if rsf_rows else "[]);"))
    w("}")
    w("")

    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L))

    print(f"barrier: EObj {eobj} layout 0x{eobj_layout:X} wall 0x{wall_layout:X} "
          f"at ({x:.2f}, {y:.2f}, {z:.2f}) rotation {rotation:.2f}")
    print(f"helper pool: {pool[1]} x BNpcBase {pool[0]}")
    print(f"MapEffect snapshot: {states}")
    print(f"RSV: {len(rsv_rows)} records, RSF: {len(rsf_rows)} records")
    print(f"wrote {os.path.normpath(out_path)}")


def sanitize_identifier(stem):
    ident = re.sub(r"\W", "_", stem)
    if not ident or ident[0].isdigit():
        ident = "_" + ident
    return ident


def derive_namespace(out_path):
    """AnoMech project dir down to the file's folder, e.g. AnoMech.Scenarios.Umad."""
    parts = os.path.normpath(os.path.abspath(out_path)).split(os.sep)
    anchors = [i for i, p in enumerate(parts[:-1]) if p == "AnoMech"]
    if not anchors:
        return None
    return ".".join(parts[anchors[-1]:-1])  # innermost 'AnoMech' .. parent of file


def main(argv):
    paths, rs_paths, out_path, ns_override, cls_override = [], [], None, None, None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--out", "--namespace", "--class", "--rs"):
            val = argv[i + 1]; i += 2
            if a == "--out": out_path = val
            elif a == "--rs": rs_paths.append(val)
            elif a == "--namespace": ns_override = val
            else: cls_override = val
        elif a.lower().endswith(".cs"):
            out_path = a; i += 1
        else:
            paths.append(a); i += 1
    if not paths or out_path is None:
        print(__doc__); return 1

    namespace = ns_override or derive_namespace(out_path)
    if namespace is None:
        print("error: could not derive namespace from output path (no 'AnoMech' dir in it); pass --namespace <A.B.C>")
        return 2
    class_name = cls_override or sanitize_identifier(os.path.splitext(os.path.basename(out_path))[0])

    rsv, rsf, found = {}, {}, []
    for p in paths:
        found.append((p, *parse_replay(p, rsv, rsf)))
    for p in rs_paths:
        add_rs(segments_by_op(p), rsv, rsf)
    _, barrier_, pool, states = found[0]
    for p, b, h, s in found[1:]:
        if b != barrier_ or h != pool or s != states:
            print(f"error: {os.path.basename(p)} disagrees with {os.path.basename(found[0][0])}:\n"
                  f"  barrier {b} vs {barrier_}\n  helper pool {h} vs {pool}\n  snapshot {s} vs {states}")
            return 3
    emit(barrier_, pool, states, rsv, list(rsf.keys()), out_path, class_name, namespace)
    print(f"class {class_name} in namespace {namespace}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
