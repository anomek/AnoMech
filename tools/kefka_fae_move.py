"""20:17:42 wave: trace each clone vs its assigned target player, with live distance."""
import math
LOG=r"D:\Projects\ffxiv\AnoMech\logs\UMAD\Network_30201_20260611.log"
LO,HI="20:17:41.5","20:17:50.0"; CX,CY=100.0,100.0
# clone -> (label, target id, target label)
CLONE={"4001026A":("cloneA","100192F0","Quan"),
       "4001026B":("cloneB","1004C6E4","Anomek"),
       "4001026C":("cloneC","1001A114","Lizard")}
TGT={t[1]:t[2] for t in CLONE.values()}
def f(x):
    try:return float(x)
    except:return None
last={}  # id->(x,y)
rows=[]
with open(LOG,encoding="utf-8",errors="replace") as fh:
    for line in fh:
        p=line.rstrip("\n").split("|")
        if len(p)<3:continue
        ts=p[1][11:23]
        if ts<LO:continue
        if ts>HI:break
        op=p[0]; idd=p[2]
        x=y=None; kind=None
        if op in("270","271") and len(p)>8: x,y=f(p[6]),f(p[7]); kind=op
        elif op in("21","22") and len(p)>46:
            # update both src and tgt positions
            tx,ty=f(p[30]),f(p[31]); sx,sy=f(p[40]),f(p[41])
            if p[6] in TGT and tx is not None: last[p[6]]=(tx,ty)
            if idd in CLONE and sx is not None: x,y=sx,sy; kind="ABL("+p[4]+")"
            if p[6] in TGT and tx is not None:
                rows.append((ts,"TGT",p[6],TGT[p[6]],tx,ty,None))
        if x is not None:
            last[idd]=(x,y)
            if idd in CLONE:
                lbl,tid,tlbl=CLONE[idd]
                tp=last.get(tid)
                dist=math.hypot(x-tp[0],y-tp[1]) if tp else None
                dcen=math.hypot(x-CX,y-CY)
                rows.append((ts,kind,idd,lbl,x,y,(dist,dcen,tlbl,tp)))
for r in rows:
    ts,kind,idd,lbl,x,y,extra=r
    if extra is None:
        print(f"{ts} {lbl:7} {kind:6}            -> ({x:6.2f},{y:6.2f})")
    else:
        dist,dcen,tlbl,tp=extra
        ds=f"d->{tlbl}={dist:5.2f}" if dist is not None else f"d->{tlbl}=  ? "
        tps=f"{tlbl}@({tp[0]:.2f},{tp[1]:.2f})" if tp else ""
        print(f"{ts} {lbl:7} {kind:9} clone=({x:6.2f},{y:6.2f}) dCen={dcen:4.2f}  {ds}  {tps}")
