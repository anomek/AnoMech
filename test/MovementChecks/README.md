# Production movement checks

Run `dotnet run --project test/MovementChecks`.

This harness compiles the real `Movement`, `PlayerMovement`, `Placement` and
obstacle geometry. Its actor stand-ins record position writes and animation
requests; native actor updates are represented by changing `Position` between
ticks, as happens when `SimCharacter.Tick` samples the client after a gap-closer.

The 72 interruption cases cover 15/30/60/144 FPS, gap-closers at the beginning,
middle, end and after the Apocalypse slide, and inward/lateral/outward movement.
They require immediate release of the old destination, no later push or position
writes, no reset of the new native action's animation, and a working subsequent
knockback. Other checks cover the full uninterrupted 21-yalm/0.7-second slide,
facing, one animation, player AI movement exclusion, height/rounding tolerance,
explicit stop/reset, and unchanged bot behavior.

Before the fix, moving the native player to `(0, 0, 3)` after knockback began
caused the next 15 FPS tick to push them to approximately `(0, 0, 5)` toward the
old landing point. The player now yields that knockback if its horizontal
position differs from the previous simulation write by more than 0.1 yalm.
Height changes and tiny rounding are ignored. `SimPlayer` derives its movement
input lock from `Movement.IsMoving`, so clearing the destination releases that
lock on the same tick. Other mechanic/death locks still apply.

These are managed movement checks, not native gap-closer or input-hook tests.
Verify the real client action, animation handoff and input release in game.
