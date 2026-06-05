"""Coalesce MapEffect (type-257) events: for each distinct (flags,index) combo,
aggregate the concurrent Kefka cast (closest cast-start in time, signed offset)
across all occurrences to reveal the consistent trigger.

Usage: python tools/mapfx_coalesce.py <logfile>
"""
import re
import sys
from datetime import datetime
from collections import defaultdict, Counter

NPC = re.compile(r"^4[0-9A-Fa-f]{7}$")


def parse_ts(s: str) -> datetime:
    head, _, tail = s.partition(".")
    frac = tail[:6]
    tzpos = max(tail.find("+"), tail.find("-"))
    tz = tail[tzpos:] if tzpos >= 0 else ""
    return datetime.fromisoformat(f"{head}.{frac}{tz}")


def decode(flags: str):
    v = int(flags, 16)
    return (v >> 16) & 0xFFFF, v & 0xFF


def main(path: str):
    mapfx = []
    casts = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("257|"):
                p = line.split("|")
                mapfx.append((parse_ts(p[1]), p[3], p[4]))
            elif line.startswith("20|"):
                p = line.split("|")
                if NPC.match(p[2]) and p[3] == "Kefka":
                    casts.append((parse_ts(p[1]), p[5]))

    cast_ts = [c[0] for c in casts]
    import bisect

    def closest_cast(t):
        i = bisect.bisect_left(cast_ts, t)
        best = None
        for j in (i - 1, i):
            if 0 <= j < len(casts):
                off = (t - casts[j][0]).total_seconds()
                if best is None or abs(off) < abs(best[1]):
                    best = (casts[j][1], off)
        return best

    # group occurrences by (index, flags)
    groups = defaultdict(list)
    for t, flags, index in mapfx:
        groups[(index, flags)].append(t)

    print("Coalesced MapEffect catalog (source 800375D2 = content director)\n")
    print("Concurrent-cast = the Kefka cast-start closest in time to the map "
          "effect; offset is map-effect minus cast (negative = effect precedes "
          "the cast).\n")
    for (index, flags) in sorted(groups, key=lambda k: (k[0], k[1])):
        ts = groups[(index, flags)]
        st, fl = decode(flags)
        # tally concurrent cast (ability, rounded offset)
        ctx = Counter()
        offs = []
        for t in ts:
            cc = closest_cast(t)
            if cc:
                ctx[cc[0]] += 1
                offs.append(cc[1])
        label = {0x0002: "ON", 0x0004: "ON*", 0x0008: "OFF",
                 0x0020: "S20", 0x0080: "S80", 0x0100: "S100"}.get(st, hex(st))
        omed = sorted(offs)[len(offs) // 2] if offs else 0
        print(f"idx={index} flags={flags} State=0x{st:04X} [{label:4}]  "
              f"n={len(ts):4d}  medOff={omed:+.1f}s")
        for ab, c in ctx.most_common(4):
            print(f"        {c:4d}x  near Kefka:{ab}")
        print()


if __name__ == "__main__":
    main(sys.argv[1])
