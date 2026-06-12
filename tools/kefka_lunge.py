"""For every clone (19513) Future's/Past's End resolve, measure:
  - clone pos before (it is 271-pinned to center) and its heading at resolve
  - assigned target (the target whose bearing best matches the clone's post-resolve step)
  - clone's settled position after the step (last 270/271 within 2.5s)
  - distance clone->target before vs after, the step magnitude, the standoff gap,
    and how collinear the step is with the center->target line.
Also flags whether the clone's *resolve heading* points at its target (facing check).
"""
from __future__ import annotations
import sys, math
from collections import defaultdict

LOG = sys.argv[1] if len(sys.argv) > 1 else r"D:\Projects\ffxiv\AnoMech\logs\UMAD\Network_30201_20260611.log"
ACTS = {"BAD8":"Future", "BAD9":"Past"}
CX, CY = 100.0, 100.0

def f(x):
    try: return float(x)
    except: return None
def secs(ts):  # HH:MM:SS.mmm -> float seconds
    return int(ts[0:2])*3600+int(ts[3:5])*60+float(ts[6:])
def norm(a):
    while a> math.pi: a-=2*math.pi
    while a<-math.pi: a+=2*math.pi
    return a

clone_ids=set()
pos=defaultdict(list)   # id -> [(t, x, y)]
resolves=[]             # list of dict

with open(LOG, encoding="utf-8", errors="replace") as fh:
    for line in fh:
        p=line.rstrip("\n").split("|")
        op=p[0]
        if op=="03" and len(p)>10:
            if p[10]=="19513": clone_ids.add(p[2])
        elif op in("270","271") and len(p)>8:
            x,y=f(p[6]),f(p[7])
            if x is not None: pos[p[2]].append((secs(p[1][11:23]),x,y))
        elif op in("21","22") and len(p)>46 and p[4] in ACTS:
            t=secs(p[1][11:23])
            sx,sy,sh=f(p[40]),f(p[41]),f(p[43])
            tx,ty=f(p[30]),f(p[31])
            # merge into existing resolve if same src within 0.7s
            r=None
            for rr in resolves[-6:]:
                if rr["src"]==p[2] and rr["act"]==p[4] and abs(rr["t"]-t)<0.7:
                    r=rr; break
            if r is None:
                r={"t":t,"ts":p[1][11:23],"src":p[2],"act":p[4],"sx":sx,"sy":sy,"sh":sh,"tg":[]}
                resolves.append(r)
            r["tg"].append((p[7],tx,ty))
            if sx is not None and (r["sx"] is None): r["sx"],r["sy"],r["sh"]=sx,sy,sh

def settled(eid,t):
    # last 270/271 sample in (t, t+2.5]; fallback first after t
    best=None
    for (tt,x,y) in pos.get(eid,[]):
        if t<tt<=t+2.5: best=(tt,x,y)
    return best

print(f"{'time':12} {'act':6} {'clone':9} | {'target':16} dBefore dAfter  step  gap  collinear  faceErr")
gaps=[]; faceerrs=[]
for r in resolves:
    if r["src"] not in clone_ids: continue
    s=settled(r["src"], r["t"])
    # pick assigned target = the one whose bearing best matches the step direction (or, if no
    # movement, the nearest target)
    if s:
        dispx,dispy=s[1]-CX, s[2]-CY
        dispmag=math.hypot(dispx,dispy)
    else:
        dispx=dispy=dispmag=0.0
    best=None
    for (tn,tx,ty) in r["tg"]:
        if tx is None: continue
        bx,by=tx-CX,ty-CY
        bmag=math.hypot(bx,by)
        if dispmag>0.4 and bmag>0.1:
            ang=abs(math.degrees(math.acos(max(-1,min(1,(dispx*bx+dispy*by)/(dispmag*bmag))))))
        else:
            ang=bmag  # no movement: prefer nearest
        if best is None or ang<best[0]: best=(ang,tn,tx,ty,bmag)
    if best is None: continue
    ang,tn,tx,ty,dbefore=best
    if s:
        dafter=math.hypot(s[1]-tx, s[2]-ty)
        step=dispmag
        gap=dbefore-step
    else:
        dafter=dbefore; step=0.0; gap=dbefore
    # facing: clone resolve heading vs bearing center->target
    faceerr=None
    if r["sh"] is not None:
        bear=math.atan2(tx-CX,ty-CY)
        faceerr=abs(math.degrees(norm(bear-r["sh"])))
        faceerrs.append(faceerr)
    gaps.append(dafter)
    fe=f"{faceerr:5.0f}" if faceerr is not None else "   --"
    print(f"{r['ts']:12} {ACTS[r['act']]:6} {r['src']:9} | {tn:16} {dbefore:6.2f} {dafter:6.2f} {step:5.2f} {ang:5.0f}deg  faceErr={fe}")

import statistics as st
print(f"\nN={len(gaps)}  dAfter(clone->target standoff): mean={st.mean(gaps):.2f} median={st.median(gaps):.2f} "
      f"min={min(gaps):.2f} max={max(gaps):.2f}")
befores=[]
for r in resolves:
    if r["src"] not in clone_ids: continue
    for (tn,tx,ty) in r["tg"]:
        if tx is not None: befores.append(math.hypot(tx-CX,ty-CY))
befores.sort()
print(f"dBefore(target dist from center): min={befores[0]:.2f}  10 smallest={[round(b,2) for b in befores[:10]]}")
print(f"  resolves with a target within 4y of center: {sum(1 for b in befores if b<4.0)} / {len(befores)}")
print(f"  resolves with a target within 5y of center: {sum(1 for b in befores if b<5.0)} / {len(befores)}")
if faceerrs:
    print(f"resolve-heading vs bearing-to-target error: mean={st.mean(faceerrs):.0f}deg median={st.median(faceerrs):.0f}deg "
          f"(low=faces target, ~random if cosmetic)")
