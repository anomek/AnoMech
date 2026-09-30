# FRU core regression checks

Run `dotnet test tests/AnoMech.FruCore.Tests` or open `AnoMech.sln` in Test Explorer.

Movement tests are separate NUnit cases: 72 gap-closer combinations from
`TestCaseSource`, seven behaviors parameterized at 15/30/60/144 FPS, and one
ordinary player-movement case. Failures use `Assert.That` and appear individually
in Test Explorer. List them with:

```sh
dotnet test tests/AnoMech.FruCore.Tests --list-tests
```

Static VFX tests expose 16 independent NUnit cases: null scene, missing resource,
all 12 one-based trigger mappings, and two invalid indices. They use `Assert.That`
and `Assert.Throws`; setup/teardown isolates and restores the native queue delegate.

These NUnit checks link the production movement, party-presets and static-VFX
trigger code. They cover knockback interruption by a gap closer, Thin Ice travel
at four frame rates, native trigger resource pointers/indices, and duty-level
party presets. Native actors and function pointers are test boundaries; rendering
and actual client input still need live-game verification.

The current upstream action/Sprint system is retained. Successful targeted native
actions notify the active scenario from UseActionLocation, so queued button presses
do not perform scenario tank swaps. The original FRU branch's separate Sprint
implementation is superseded by upstream's UserActions/SprintHandler.
