using System;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

// A weather + bgm bundle grouping sibling scenarios (e.g. TOP P5's Delta/Sigma/Omega).
// Weather is the initial weather; a scenario may still change it live via world.SetWeather.
public interface IPhase
{
    string Name { get; }
    IZone Zone { get; }
    byte? Weather => null;
    ushort Bgm => 0;
    // Optional per-frame hold of the zone's environment fog value (ZoneSession.FogHold);
    // null = let the engine's own weather transition run.
    float? FogHold => null;
    // The env scene the server has set by the time this phase plays, which scripts and layouts
    // gate on (see SplatoonCompat); null leaves the zone's own value.
    byte? EnvScene => null;

    // Optional phase-wide setup, between zone and scenario Run. Default no-op.
    void Run(SimWorld world) { }

    // Phase-wide client-side setup, run on host and peer alike (see IZone.RunClientSetup).
    // Default no-op.
    void RunClientSetup(SimWorld world) { }
}

// Default phase. Pass Init to attach custom phase-wide setup.
public sealed class Phase : IPhase
{
    private readonly Action<SimWorld>? init;
    private readonly Action<SimWorld>? clientSetup;

    public Phase(IZone zone, string name, byte? weather, ushort bgm, Action<SimWorld>? init = null, float? fogHold = null, Action<SimWorld>? clientSetup = null, byte? envScene = null)
    {
        Zone = zone;
        Name = name;
        Weather = weather;
        Bgm = bgm;
        FogHold = fogHold;
        EnvScene = envScene;
        this.init = init;
        this.clientSetup = clientSetup;
    }

    public IZone Zone { get; }
    public string Name { get; }
    public byte? Weather { get; }
    public ushort Bgm { get; }
    public float? FogHold { get; }
    public byte? EnvScene { get; }

    public void Run(SimWorld world) => init?.Invoke(world);
    public void RunClientSetup(SimWorld world) => clientSetup?.Invoke(world);
}
