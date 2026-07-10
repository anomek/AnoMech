"""Extract the RSV (text) and RSF (file-path) tables that ARealmRecorded injects
into a .dat replay at instance load, and emit a single C# data file the AnoMech
engine can seed (Scenarios/Umad/UmadReplayData.cs).

Why: UMAD/Kefka action names, boss dialogue, status text (RSV) and the locked
model/VFX/sound file paths (RSF) are server-delivered only inside the duty. AnoMech
runs inn-only, so they never arrive and render blank / fail to load. ARealmRecorded
records them as two pseudo-opcodes at ms=0 (actor 0xE0000000):

  0xF001  RSVPacket : [u32 valueLen][48B key][value]        -> text
  0xF002  RSFPacket : fixed 0x48-byte RsfReceive record      -> file paths

(Layout confirmed against UnknownX7/ARealmRecorded ReplayPacketManager.cs; RSF is
the file-level twin of RSV per xiv.dev/game-internals/rsf.)

RSV values are emitted verbatim: clean UTF-8 values become readable C# string
literals (non-ASCII escaped to \\uXXXX so the source stays pure ASCII); values that
carry raw SeString payload bytes (not valid UTF-8, e.g. status descriptions with
embedded action-name links) are emitted as base64 and fed to AddRsvString unchanged.
RSF records are emitted as base64 of the exact 0x48-byte buffer.

Usage:
    python replay_export_rsv_rsf.py <replay.dat> [<replay2.dat> ...] <output.cs>
    python replay_export_rsv_rsf.py <replay.dat>            # -> Scenarios/Umad/UmadReplayData.cs

Args are classified by extension: *.cs is the output path, everything else is a
replay input (>=1). With no *.cs given it defaults to the UMAD file. The C# class
name is taken from the output filename stem and the namespace is derived from the
output path (the AnoMech project dir down to the file's folder); override either with
--class <Name> / --namespace <A.B.C>. Multiple replays are unioned (all injected at
load, so one pull is already complete; merging is belt-and-suspenders across patches).
"""
import struct, sys, os, re, base64

DATA_START = 0x36C
OP_RSV = 0xF001
OP_RSF = 0xF002
RSF_LEN = 0x48  # bytes RsfReceive consumes per record

DEFAULT_OUT = os.path.join(
    os.path.dirname(__file__), "..", "AnoMech", "Scenarios", "Umad", "UmadReplayData.cs")


def iter_segments(buf):
    replay_len = struct.unpack_from("<i", buf, 0x48)[0]
    off, end = DATA_START, DATA_START + replay_len
    while off + 12 <= end:
        opcode, length, ms, actor = struct.unpack_from("<HHII", buf, off)
        seg_end = off + 12 + length
        if seg_end > end:
            break
        yield opcode, ms, buf[off + 12:seg_end]
        off = seg_end


def parse_replay(path, rsv, rsf):
    with open(path, "rb") as f:
        buf = f.read()
    assert buf[:11] == b"FFXIVREPLAY", f"{path}: not a FFXIVREPLAY file"
    build = struct.unpack_from("<i", buf, 0x10)[0]
    for opcode, ms, p in iter_segments(buf):
        if opcode == OP_RSV:
            if len(p) < 4 + 0x30:
                continue
            vlen = struct.unpack_from("<I", p, 0)[0]
            key = p[4:4 + 0x30].split(b"\x00", 1)[0].decode("ascii", "replace")
            val = p[4 + 0x30:4 + 0x30 + vlen]
            if key:
                rsv[key] = val            # last-writer-wins on union
        elif opcode == OP_RSF:
            if len(p) >= RSF_LEN:
                rsf[p[:RSF_LEN]] = None    # dedup exact records
    return build


def rsv_sort_key(key):
    m = re.match(r"_rsv_(\d+)_", key)
    return (int(m.group(1)) if m else 1 << 62, key)


def is_clean_text(b):
    """Value is a plain readable string: valid UTF-8 with no control/format bytes."""
    try:
        s = b.decode("utf-8")
    except UnicodeDecodeError:
        return None
    if any(ord(c) < 0x20 for c in s):     # SeString START (0x02) etc. -> raw path
        return None
    return s


def cs_escape(s):
    out = []
    for c in s:
        o = ord(c)
        if c == '"':
            out.append('\\"')
        elif c == '\\':
            out.append('\\\\')
        elif 0x20 <= o <= 0x7E:
            out.append(c)
        elif o <= 0xFFFF:
            out.append(f"\\u{o:04X}")
        else:
            out.append(f"\\U{o:08X}")
    return "".join(out)


def preview_comment(b):
    """Short readable hint for a base64 row, control bytes shown as dots."""
    s = b.decode("utf-8", "replace")
    s = "".join(c if 0x20 <= ord(c) < 0x7F else "." for c in s)
    s = s.replace("*/", "* /")
    return (s[:80] + "...") if len(s) > 80 else s


def emit(rsv, rsf, builds, sources, out_path, class_name, namespace):
    text_rows, raw_rows = [], []
    for key in sorted(rsv, key=rsv_sort_key):
        val = rsv[key]
        clean = is_clean_text(val)
        if clean is not None:
            text_rows.append((key, cs_escape(clean)))
        else:
            raw_rows.append((key, base64.b64encode(val).decode("ascii"), preview_comment(val)))
    rsf_rows = [(base64.b64encode(rec).decode("ascii"), preview_comment(rec)) for rec in rsf]

    src = ", ".join(os.path.basename(s) for s in sources)
    L = []
    w = L.append
    w("// <auto-generated>")
    w(f"//   Produced by tools/replay_export_rsv_rsf.py")
    w(f"//   Source replay(s): {src}")
    w(f"//   Game build(s): {', '.join(str(b) for b in sorted(set(builds)))}")
    w(f"//   RSV: {len(text_rows) + len(raw_rows)} rows ({len(text_rows)} text, {len(raw_rows)} raw)"
      f"  |  RSF: {len(rsf_rows)} records")
    w("//   Re-run the extractor against a newer replay to refresh; do not edit by hand.")
    w("// </auto-generated>")
    w("using System;")
    w("using AnoMech.Core.Native;")
    w("")
    w(f"namespace {namespace};")
    w("")
    w("// RSV (text) + RSF (file paths) captured from a duty replay. Scenarios run inn-only")
    w("// so the server never delivers these; Seed() writes them into the game's native")
    w("// tables at scenario start. See replay_export_rsv_rsf.py and memory")
    w("// reference_rsv_action_names.")
    w(f"public static class {class_name}")
    w("{")
    w("    // Verbatim RSV key -> resolved UTF-8 text (action / status names, boss dialogue).")
    w("    private static readonly (string Key, string Value)[] RsvText =")
    w("    [")
    for key, val in text_rows:
        w(f'        ("{key}", "{val}"),')
    w("    ];")
    w("")
    w("    // RSV values carrying raw SeString payload bytes (not valid UTF-8); base64 of the")
    w("    // exact value bytes, fed to AddRsvString unchanged.")
    w("    private static readonly (string Key, string ValueBase64)[] RsvRaw =")
    w("    [")
    for key, b64, prev in raw_rows:
        w(f'        ("{key}", "{b64}"), // {prev}')
    w("    ];")
    w("")
    w("    // RSF: exact 0x48-byte RsfReceive records (unlock locked model/VFX/sound files).")
    w("    private static readonly string[] RsfRecords =")
    w("    [")
    for b64, prev in rsf_rows:
        w(f'        "{b64}", // {prev}')
    w("    ];")
    w("")
    w("    // Idempotent; safe to call at the top of the scenario's Run.")
    w("    public static void Seed()")
    w("    {")
    w("        foreach (var (key, value) in RsvText)")
    w("            RsvFunctions.Add(key, value);")
    w("        foreach (var (key, valueBase64) in RsvRaw)")
    w("            RsvFunctions.AddRaw(key, Convert.FromBase64String(valueBase64));")
    w("        foreach (var record in RsfRecords)")
    w("            RsfFunctions.Add(Convert.FromBase64String(record));")
    w("    }")
    w("}")
    w("")

    with open(out_path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(L))

    print(f"RSV: {len(text_rows)} text + {len(raw_rows)} raw = {len(text_rows)+len(raw_rows)} rows")
    print(f"RSF: {len(rsf_rows)} records")
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
    paths, out_path, ns_override, cls_override = [], None, None, None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--out", "--namespace", "--class"):
            val = argv[i + 1]; i += 2
            if a == "--out": out_path = val
            elif a == "--namespace": ns_override = val
            else: cls_override = val
        elif a.lower().endswith(".cs"):
            out_path = a; i += 1
        else:
            paths.append(a); i += 1
    if not paths:
        print(__doc__); return 1

    if out_path is None:                       # bare-replay convenience: default to UMAD
        out_path = DEFAULT_OUT
        namespace = ns_override or "AnoMech.Scenarios.Umad"
        class_name = cls_override or "UmadReplayData"
    else:
        namespace = ns_override or derive_namespace(out_path)
        if namespace is None:
            print("error: could not derive namespace from output path (no 'AnoMech' dir "
                  "in it); pass --namespace <A.B.C>")
            return 2
        class_name = cls_override or sanitize_identifier(
            os.path.splitext(os.path.basename(out_path))[0])

    rsv, rsf, builds = {}, {}, []
    for p in paths:
        builds.append(parse_replay(p, rsv, rsf))
    emit(rsv, list(rsf.keys()), builds, paths, out_path, class_name, namespace)
    print(f"class {class_name} in namespace {namespace}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
