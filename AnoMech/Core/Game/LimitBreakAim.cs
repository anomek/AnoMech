using System.Numerics;

namespace AnoMech.Core.Game;

// Where a limit break was aimed as it landed, scenario-local: its user's position and heading
// (toward the target it was used on, else the way the user faced), and an area-targeted one's
// ground location.
public readonly record struct LimitBreakAim(Vector3 Origin, float Heading, Vector3? Location = null);
