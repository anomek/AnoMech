"""Extract MapEffect (type-257) events from a UMAD network log and annotate
each with the nearby Kefka cast, coalescing the per-pull repetition.

Usage: python tools/mapfx_context.py <logfile>
"""
import re
import sys
from datetime import datetime
from collections import defaultdict

NPC = re.compile(r"^4[0-9A-Fa-f]{7}$")


def parse_ts(s: str) -> datetime:
    # 2026-06-03T04:04:16.7860000-07:00  -> trim to microseconds
    head, _, tail = s.partition(".")
    frac = tail[:6]
    tzpos = max(tail.find("+"), tail.find("-"))
    tz = tail[tzpos:] if tzpos >= 0 else ""
    return datetime.fromisoformat(f"{head}.{frac}{tz}")


def decode(flags: str):
    v = int(flags, 16)
    return (v >> 16) & 0xFFFF, v & 0xFF  # State, Flags


def main(path: str):
    mapfx = []   # (t, flags, index)
    casts = []   # (t, ability) Kefka only
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("257|"):
                p = line.split("|")
                mapfx.append((parse_ts(p[1]), p[3], p[4]))
            elif line.startswith("20|"):
                p = line.split("|")
                if NPC.match(p[2]) and p[3] == "Kefka":
                    casts.append((parse_ts(p[1]), p[5]))

    # Segment pulls: gap > 90s in the map-effect stream starts a new pull.
    pulls = []
    cur = []
    prev = None
    for ev in mapfx:
        if prev is not None and (ev[0] - prev).total_seconds() > 90:
            pulls.append(cur)
            cur = []
        cur.append(ev)
        prev = ev[0]
    if cur:
        pulls.append(cur)

    print(f"# {len(mapfx)} map-effect lines, {len(casts)} Kefka casts, "
          f"{len(pulls)} pull-segments\n")

    def nearest_cast(t):
        """Most recent Kefka cast at/before t, plus its offset (s)."""
        best = None
        for ct, ab in casts:
            if ct <= t:
                best = (ct, ab)
            else:
                break
        if best is None:
            return None
        return best[1], (t - best[0]).total_seconds()

    for i, pull in enumerate(pulls):
        t0 = pull[0][0]
        print(f"=== PULL {i+1}  start {t0.isoformat()}  "
              f"({len(pull)} map-effects, "
              f"dur {(pull[-1][0]-t0).total_seconds():.0f}s) ===")
        for t, flags, index in pull:
            st, fl = decode(flags)
            rel = (t - t0).total_seconds()
            nc = nearest_cast(t)
            ctx = ""
            if nc:
                ab, off = nc
                ctx = f"   <- Kefka:{ab} (+{off:.1f}s)"
            on = "ON " if st in (0x0002, 0x0004) else (
                "OFF" if st == 0x0008 else f"0x{st:04X}")
            print(f"  {rel:7.1f}s  idx={index} {flags} [{on}]{ctx}")
        print()


if __name__ == "__main__":
    main(sys.argv[1])
