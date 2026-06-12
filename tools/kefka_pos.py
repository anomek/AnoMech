"""Positional analysis of Kefka-clone Future's End / Past's End (UMAD log).

Extracts, for every clone (BNpcName 19513) use of BAD8 (Future's End) and
BAD9 (Past's End):
  - clone position just BEFORE the ability (latest 271 ActorSetPos / 270 move / 03 spawn)
  - clone position AT RESOLVE (source xyz+heading baked into the 21/22 ability line)
  - any ground-target xyz baked into the spell (263 ActorCastExtra)
  - each entity target hit + that target's xyz

Also dumps the matching BAD2/BAD3 cast-bar telegraphs from the main Kefka (19506).
"""
from __future__ import annotations
import sys
from collections import defaultdict

LOG = sys.argv[1] if len(sys.argv) > 1 else r"D:\Projects\ffxiv\AnoMech\logs\UMAD\Network_30201_20260611.log"

ACTIONS = {"BAD8": "Future's End", "BAD9": "Past's End",
           "BAD2": "Future's End(cast)", "BAD3": "Past's End(cast)"}

# BNpcName -> role
CLONE_BNPC = "19513"   # Kefka clone
BOSS_BNPC = "19506"    # main Kefka

def t(s):  # short timestamp HH:MM:SS.mmm
    return s[11:23]

def f(x):
    try:
        return float(x)
    except ValueError:
        return None

# Pass 1: classify entity ids via 03 AddCombatant (id -> bnpcname)
id_role = {}          # entityid -> bnpcname
# position timeline per entity: list of (timestr, x, y, z, src)
pos_hist = defaultdict(list)

# collected ability uses: key = (seqid) -> dict
waves = {}

with open(LOG, encoding="utf-8", errors="replace") as fh:
    for line in fh:
        p = line.rstrip("\n").split("|")
        op = p[0]
        if op == "03" and len(p) > 10:
            id_role[p[2]] = p[10]
            x, y, z = f(p[16]), f(p[17]), f(p[18]) if len(p) > 18 else None
            if x is not None:
                pos_hist[p[2]].append((t(p[1]), x, y, z, "spawn03"))
        elif op == "271" and len(p) > 8:
            x, y, z = f(p[6]), f(p[7]), f(p[8])
            if x is not None:
                pos_hist[p[2]].append((t(p[1]), x, y, z, "setpos271"))
        elif op == "270" and len(p) > 8:
            x, y, z = f(p[6]), f(p[7]), f(p[8])
            if x is not None:
                pos_hist[p[2]].append((t(p[1]), x, y, z, "move270"))
        elif op in ("21", "22") and len(p) > 46:
            act = p[4]
            if act not in ACTIONS:
                continue
            seq = p[44]
            tx, ty, th = f(p[30]), f(p[31]), f(p[33])
            sx, sy, sz, sh = f(p[40]), f(p[41]), f(p[42]), f(p[43])
            w = waves.setdefault((act, seq, p[2]), {
                "act": act, "seq": seq, "time": t(p[1]), "src": p[2],
                "sx": sx, "sy": sy, "sz": sz, "sh": sh, "targets": []})
            w["targets"].append((p[6], p[7], tx, ty, th))
        elif op == "263" and len(p) > 7 and p[3] in ACTIONS:
            # ground-target xyz for a cast: store keyed by (action, src) most-recent
            pos_hist[("GT", p[3], p[2])] = (t(p[1]), f(p[4]), f(p[5]), f(p[6]), f(p[7]))

def last_pos_before(eid, ts):
    best = None
    for (tt, x, y, z, src) in pos_hist.get(eid, []):
        if tt < ts:
            best = (tt, x, y, z, src)
        else:
            break
    return best

def first_pos_after(eid, ts):
    for (tt, x, y, z, src) in pos_hist.get(eid, []):
        if tt > ts:
            return (tt, x, y, z, src)
    return None

# Print, grouped by time
rows = sorted(waves.values(), key=lambda w: (w["time"], w["act"], w["src"]))
print(f"{'time':12} {'act':18} clone        before(271/move)        atResolve(x,y,head)       targets")
prev_time = None
for w in rows:
    role = id_role.get(w["src"], "?")
    tag = "CLONE" if role == CLONE_BNPC else ("BOSS" if role == BOSS_BNPC else role)
    before = last_pos_before(w["src"], w["time"])
    bstr = f"{before[1]:6.2f},{before[2]:6.2f} [{before[4]}]" if before else "    --        "
    astr = f"{w['sx']:6.2f},{w['sy']:6.2f} h={w['sh']:+.2f}" if w["sx"] is not None else "  --"
    tgts = ", ".join(f"{tn}@({tx:.1f},{ty:.1f})" for (_, tn, tx, ty, _) in w["targets"])
    if prev_time is not None and w["time"][:5] != prev_time[:5]:
        print("  " + "-" * 90)
    print(f"{w['time']:12} {ACTIONS[w['act']]:18} {w['src']}({tag:5}) {bstr:24} {astr:24} {tgts}")
    prev_time = w["time"]
