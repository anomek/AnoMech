using AnoMech.Core.Map;

namespace AnoMech.Core.SimObjects;

// A native scenery effect owned by the world. Reset must dismiss it even when
// the scenario's scheduled resolve never runs (death, restart, or leaving).
public sealed class SimMapEffect : ISimObject
{
    private readonly MapController map;
    private readonly byte index;
    private readonly uint hide;
    public bool IsActive { get; private set; } = true;

    internal SimMapEffect(MapController map, byte index, uint show, uint hide)
    {
        this.map = map;
        this.index = index;
        this.hide = hide;
        map.AddEffect(show, index);
    }

    public void Tick(float deltaSeconds) { }

    public void Despawn()
    {
        if (!IsActive) return;
        IsActive = false;
        map.AddEffect(hide, index);
    }
}
