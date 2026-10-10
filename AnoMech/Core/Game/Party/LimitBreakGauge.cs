using System;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game.Party;

// The party's faked limit break gauge. Solo in the inn the real gauge is empty, so Set writes
// one into LimitBreakController, for display only: the real values are saved once and written
// back when the party despawns. The client gates presses on it; the request packet is eaten by
// the firewall. Spending is the client's own: SimParty sends the limit break's ActorControl,
// whose handler empties the gauge.
public sealed class LimitBreakGauge
{
    private const ushort UnitsPerBar = 10000;
    private const byte Bars = 3;

    private ushort units;
    private LimitBreakBars? saved;

    public int FilledBars => units / UnitsPerBar;

    public void Set(float bars)
    {
        units = (ushort)(Math.Clamp(bars, 0f, Bars) * UnitsPerBar);
        if (Natives.LimitBreak.Read() is not { } real) return;
        saved ??= real;
        Natives.LimitBreak.Write(new LimitBreakBars(Bars, units, UnitsPerBar));
    }

    internal void Spend() => units = 0;

    internal void Restore()
    {
        units = 0;
        if (saved is not { } s) return;
        saved = null;
        Natives.LimitBreak.Write(s);
    }
}
