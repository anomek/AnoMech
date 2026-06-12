"""Single-wave forensic for the 20:17:42 Fae case:
list all 8 party members at resolve with distance-from-center (positions FROZEN at
resolve), whether hit by BAD6/7/8/9 in this wave, and alive/dead (deaths=type 25)."""
from __future__ import annotations
import math

LOG = r"D:\Projects\ffxiv\AnoMech\logs\UMAD\Network_30201_20260611.log"
CX,CY=100.0,100.0
WAVE_LO, WAVE_HI = "20:17:42.0", "20:17:43.2"
STOP = "20:17:44"
PULL_LO = "20:16:00.0"
RES_ACTS={"BAD6","BAD7","BAD8","BAD9"}
PARTY_NAMES={"Anomek Rake","Quan Chi","H'vy Byeshnasti","Fae Nightwolf",
             "Sendo Sheng","Finaglia Relanah","Cellica Mercury","Lizard Butterfly"}

def f(x):
    try: return float(x)
    except: return None

pos={}; hit=set(); deaths=[]; hp_at={}
with open(LOG, encoding="utf-8", errors="replace") as fh:
    for line in fh:
        p=line.rstrip("\n").split("|")
        if len(p)<3: continue
        ts=p[1][11:23]
        if ts[:8] > STOP and ts[:2]=="20": break    # past the wave (file is chronological)
        op=p[0]
        if op in("270","271") and len(p)>8 and ts<=WAVE_HI:
            x,y=f(p[6]),f(p[7])
            if x is not None and p[2] in pos: pos[p[2]]=(x,y,ts)
        elif op in("21","22") and len(p)>46:
            if p[7] in PARTY_NAMES and ts<=WAVE_HI:
                tx,ty=f(p[30]),f(p[31])
                if tx is not None: pos[p[6]]=(tx,ty,ts)
                hp=f(p[24])
                if hp is not None: hp_at[p[6]]=hp
                pos.setdefault(p[6],(tx,ty,ts)); pos.setdefault(p[6],(None,)*3)
                globals().setdefault('NAME',{}); NAME[p[6]]=p[7]
            if p[3] in PARTY_NAMES and ts<=WAVE_HI:
                sx,sy=f(p[40]),f(p[41])
                if sx is not None: pos[p[2]]=(sx,sy,ts); NAME[p[2]]=p[3]
            if p[4] in RES_ACTS and WAVE_LO<=ts<=WAVE_HI:
                hit.add(p[6])
        elif op=="03" and len(p)>18 and p[3] in PARTY_NAMES and ts<=WAVE_HI:
            x,y=f(p[16]),f(p[17])
            globals().setdefault('NAME',{}); NAME[p[2]]=p[3]
            if x is not None: pos[p[2]]=(x,y,ts)
        elif op=="25" and PULL_LO<=ts<=WAVE_HI and len(p)>3 and p[3] in PARTY_NAMES:
            deaths.append((ts,p[3]))

print(f"WAVE resolve {WAVE_LO}..{WAVE_HI}  (2026-06-11, -07:00 / America/Los_Angeles)")
print("All party at resolve, sorted by distance from center (positions frozen <= resolve):")
rows=[]
for pid,(x,y,ts) in pos.items():
    nm=NAME.get(pid,pid)
    if x is None: continue
    rows.append((math.hypot(x-CX,y-CY),pid,nm,x,y,ts))
rows.sort()
for i,(d,pid,nm,x,y,ts) in enumerate(rows,1):
    h="HIT " if pid in hit else "----"
    dead=" DEAD" if any(nm==dn for _,dn in deaths) else ""
    hp=hp_at.get(pid)
    print(f"  #{i} {h} {nm:18} dCenter={d:5.2f} pos=({x:6.2f},{y:6.2f})@{ts} hp={hp}{dead}")

print("\nDeaths in this pull before/at resolve:")
for ts,who in deaths: print(f"  {ts}  {who}")
if not deaths: print("  none")
