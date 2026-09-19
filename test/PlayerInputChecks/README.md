# Player input checks

Run `dotnet run --project test/PlayerInputChecks`.

This harness compiles the production `LocalPlayerInputHooks` and `SimPlayer`
against stubbed Dalamud hooks, native structures and unrelated player services.
It drives the registered action detours, rather than copying their logic.

Sprint checks cover accepted execution, rejected/cooldown presses, queued
presses, adjusted GeneralAction hotbar routing, exactly one buff application,
native status 50 with speed parameter 30 and `refreshFlags: true`, decreasing
ten-second combat duration, native slot replacement, full status arrays, death,
mechanic input locks, reset, unowned buffs, other actions and missing players.
The stub models GeneralAction 4 mapping to Action 3; the native sheet confirms
that mapping, but this harness does not execute the game's action dispatcher.

The simulation's packet firewall prevents the server from applying Sprint.
The execution hook supplies a finite local buff, using native `SetStatus` with
flag refresh so the speed effect is requested as well as the visible icon.
The original native action path still decides usability and cooldown; the
existing scenario-start cooldown reset remains in place.

These are managed checks of calls and lifetime. They do not measure live
movement speed or execute native status functions. An in-game pass must confirm
the speed increase, hotbar/cooldown behavior, expiration and reset/leave cleanup.
