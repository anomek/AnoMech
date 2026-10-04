using System;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game.Party;

// The party's faked limit break gauge. Solo in the inn the real gauge is empty, so a scenario
// that grants one has it written into LimitBreakController every tick, for display only: the
// real values are saved once and written back when the party despawns. The client gates presses
// on it; the request packet is eaten by the firewall.
public sealed class LimitBreakGauge
{
    private const ushort UnitsPerBar = 10000;
    private const byte Bars = 3;

    // Null until a scenario grants a gauge; the native one is then left alone.
    private ushort? units;
    private LimitBreakBars? saved;

    public int FilledBars => (units ?? 0) / UnitsPerBar;

    // Null while no scenario grants a gauge.
    public float? CurrentBars => units / (float)UnitsPerBar;

    // The local player's own limit break as it resolves, and where it was aimed: a scenario reacts to
    // what it did (a healer LB3's cleanse, where a caster LB3 came down).
    public event Action<uint, LimitBreakAim>? Landed;

    internal void Land(uint actionId, LimitBreakAim aim) => Landed?.Invoke(actionId, aim);

    public void Set(float bars)
        => units = (ushort)(Math.Clamp(bars, 0f, Bars) * UnitsPerBar);

    public void Spend()
    {
        if (units != null) units = 0;
    }

    internal void Tick()
    {
        if (units is not { } current) return;
        if (Natives.LimitBreak.Read() is not { } real) return;
        saved ??= real;
        Natives.LimitBreak.Write(new LimitBreakBars(Bars, current, UnitsPerBar));
    }

    internal void Restore()
    {
        units = null;
        if (saved is not { } s) return;
        saved = null;
        Natives.LimitBreak.Write(s);
    }
}
