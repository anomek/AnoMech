using System.Numerics;

namespace AnoMech.Core.Map;

// Arena floor footprint, centered on scenario-local zero. XZ only; Y is ignored.
public interface IArenaShape
{
    bool IsOutside(Vector3 local);
}
