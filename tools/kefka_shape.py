"""For each Future's/Past's End wave, snapshot ALL party members (10xxxxxx) and
mark who was hit, with bearing-from-center vs the shared cast heading, to reveal
the AOE shape (cone / line / safe-wedge / full-room)."""
from __future__ import annotations
import sys, math
from collections import defaultdict

LOG = sys.argv[1] if len(sys.argv) > 1 else r"D:\Projects\ffxiv\AnoMech\logs\UMAD\Network_30201_20260611.log"
ACTIONS = {"BAD8": "Future's End", "BAD9": "Past's End"}
CX, CY = 100.0, 100.0

def t(s): return s[11:23]
def f(x):
    try: return float(x)
    except: return None
def sec(ts):
    h,m,s = ts.split(":"); return int(h)*3600+int(m)*60+float(s)
def norm(a):
    while a > math.pi: a -= 2*math.pi
    while a < -math.pi: a += 2*math.pi
    return a

last_pos = {}; names = {}; waves = []

with open(LOG, encoding="utf-8", errors="replace") as fh:
    for line in fh:
        p = line.rstrip("\n").split("|")
        op = p[0]
        if op == "03" and len(p) > 18:
            names[p[2]] = p[3]
            x,y = f(p[16]), f(p[17])
            if x is not None: last_pos[p[2]] = (x,y)
        elif op in ("270","271") and len(p) > 8:
            x,y = f(p[6]), f(p[7])
            if x is not None: last_pos[p[2]] = (x,y)
        elif op in ("21","22") and len(p) > 46:
            tx,ty = f(p[30]), f(p[31]); sx,sy = f(p[40]), f(p[41])
            if tx is not None: last_pos[p[6]] = (tx,ty); names[p[6]] = p[7]
            if sx is not None: last_pos[p[2]] = (sx,sy); names[p[2]] = p[3]
            if p[4] in ACTIONS:
                heading = f(p[43])
                if waves and waves[-1]["act"]==p[4] and sec(t(p[1]))-sec(waves[-1]["time"])<1.2:
                    waves[-1]["hit"].add(p[6])
                else:
                    snap = {k:v for k,v in last_pos.items() if k.startswith("10")}
                    waves.append({"time":t(p[1]),"act":p[4],"hit":{p[6]},"h":heading,"snap":snap})

for w in waves:
    h = w["h"]
    rows=[]
    for pid,(x,y) in w["snap"].items():
        dx,dy = x-CX, y-CY
        dist = math.hypot(dx,dy)
        if dist > 30:            # drop bystanders outside the arena
            continue
        bear = math.atan2(dx,dy)              # bearing from center; 0 = +Y (south), = log heading 0
        delta = math.degrees(norm(bear - h))  # player bearing relative to boss facing
        rows.append((abs(delta), delta, dist, names.get(pid,pid), x, y, pid in w["hit"]))
    rows.sort()
    hit_d  = sorted(abs(r[1]) for r in rows if r[6])
    safe_d = sorted(abs(r[1]) for r in rows if not r[6])
    front_hits = sum(1 for r in rows if r[6] and abs(r[1])<90)
    back_hits  = sum(1 for r in rows if r[6] and abs(r[1])>=90)
    print(f"\n=== {w['time']} {ACTIONS[w['act']]:12} facing={math.degrees(h):+6.1f}deg | "
          f"hits front/back(of facing)={front_hits}/{back_hits} | "
          f"|delta| hits={[round(d) for d in hit_d]} safe={[round(d) for d in safe_d]}")
    for adelta,delta,dist,nm,x,y,hit in rows:
        side = "FRONT" if abs(delta)<90 else "BACK "
        mark = "HIT " if hit else "safe"
        print(f"   {mark} {side} {nm:18} ({x:6.2f},{y:6.2f}) dist={dist:5.2f} delta={delta:+7.1f}")
