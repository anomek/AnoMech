# Safety tests

Automated checks that the sim's packet filter is up whenever the client holds state the server
doesn't, comes down only once the client is verifiably back where the server has it, and that no
code can move the real character or talk to the server outside a stay. They run on every push and
pull request, and daily against the current Dalamud release and staging build
(`.github/workflows/safety-tests.yml`).

## Running them

```
dotnet build AnoMech.sln -c Release -p:Platform=x64
dotnet test tests/SafetyTests -c Release
```

The architecture tests read the built `AnoMech/bin/x64/Release/AnoMech.dll` and refuse a DLL older
than the sources. `ANOMECH_DLLS` (`;`-separated) points them at other builds, e.g. Release and
Debug together as CI does. The contract tests use the Dalamud the plugin builds with
(`DALAMUD_HOME`, else XIVLauncher's dev folder). `ANOMECH_HOOK_BACKEND=safetyhook` runs the whole
suite with SafetyHook's hook failures instead of Reloaded's.

## What is in here

- `Firewall/`: the plugin's real `ZoneSession`, `ZoneSession.Guard`, `MapController` and
  `NetGuard` sources, compiled unmodified against the fakes in `Fakes/` and driven through
  `Harness/VirtualGame`: a simulated client, server and Dalamud. Every packet the send path lets
  through is judged against what the server believes; every hook change, every frame and every
  sim load is checked against the invariants below.
  - Deterministic tests for the start gate, the stay guard, lift verification, zone-load
    failures, unloads, the detours (all 65,536 opcodes each way) and the debug hold.
  - `IncidentRegressionTests`: one test per problem found during development.
  - `HookFailureTests`: filter hooks that will not enable or disable, under both of Dalamud's
    hook backends.
  - `FuzzTests`: random sequences of everything a player, the server, Dalamud and a faulty
    scenario can do (starts, leaves, casts, lag, server moves, logouts, hook failures, missing
    players, refused position writes, throwing loads, unloads, hooks that refuse to change), each
    under a random hook backend, ZoneInit timing and order of a frame's queued tasks. 2,000
    sequences by default;
    `ANOMECH_FUZZ_SEEDS=100000` for a deep run, `ANOMECH_FUZZ_REPRO=<seed>` to replay one
    failure with its full trace.
- `Architecture/`: rules over the built plugin's IL:
  - who may raise, lower, or read the filter hooks, and who may create hooks at all;
  - who may end the process, and proof that the safety stop never returns;
  - proofs that the filter only lowers once nothing blocks it;
  - proofs that the character is only moved and a scenario only run after the gate and the
    zone load succeeded;
  - which code may write a game object's position or fabricate server packets;
  - what a scenario may never do: timers, threads, async, framework or game event hooks, zone
    or run control, the real action manager, the weather manager;
  - the event wiring the guard depends on, the unload order, and the `PeerInRun` gate on every
    host message that moves a character;
  - `StayBoundaryTests`: every call path in the plugin, followed from every place code can start
    running (Dalamud callbacks, UI, timers, tasks, anything a delegate is handed to), to a move of
    the real character or to something put in the sim world; each must pass a check that a stay
    is running first. The path is printed when one doesn't. Also proven there: what makes each of
    those checks mean a stay;
  - `ControlFlowTests`: the branch reading those proofs rest on, on hand-built IL.
- `Contract/`: everything the fakes assume of Dalamud, ClientStructs and Lumina, checked against
  the real assemblies, so a Dalamud release that changes one fails here instead of silently
  voiding the firewall tests:
  - Dalamud's own hooks, both backends, patch a function in the test process, and every
    sequence of the operations the plugin uses runs on a real and a fake hook side by side;
  - Dalamud's own framework scheduler runs the same script as the harness's queue;
  - from IL, where running needs the game: how each backend fails, that the plugin's hooks come
    from `Hook<T>.FromAddress` and are disposed on unload, that `Framework.Run` never runs
    inline and drains before `Update`, that each `Update` handler is isolated and runs every
    tick, that conditions are ClientStructs' `Conditions` bytes read live, and that Dalamud's
    territory moves only on a ZoneInit packet and says so only on a change;
  - every enum value, explicit field offset and member signature the fakes declare.

## The invariants

1. A packet other than the heartbeat reaches the server only when the client's loaded zone,
   position and duty state match what the server has.
2. The send filter (and, while a sim zone is loaded, the receive filter) is up whenever they
   don't.
3. The plugin never moves the real character to a place the server hasn't seen while the send
   filter is down.
4. A sim zone is only loaded from a state the server can no longer act on: see
   `Harness/StartSpecification.cs`, written from the game's point of view. The plugin may be
   stricter, never looser.
5. After the safety stop, nothing changes.
6. Nothing is left stuck: no filter up without a stay to lower it, no pending lift that never
   resolves, no condition the plugin set left behind.
7. The game is only stopped when something unsafe actually happened.

## Checking the tests themselves

`Mutation/run.py` copies the repository, puts back each bug fixed during development and each
mistake a new scenario could plausibly make (`Mutation/mutants.py`), one at a time, and reports
any the tests fail to catch. It takes minutes, so it is not part of CI; run it after changing
the firewall or the tests. Update a mutant's edit when the code it targets changes.

`Mutation/Dalamud/run.py` does the same to Dalamud: it writes copies of `Dalamud.dll` with one
behaviour changed as a release might (hooks that stay enabled, `Framework.Run` inline, a
renumbered condition, a territory that follows the sim's loads, ...) and checks each fails a
contract test. Run it after changing the contract tests or the fakes.

## When a rule fails on purpose

An architecture failure names the code and the rule. If the new call is genuinely safe, add it
to that rule's allowlist in `ArchitectureTests.cs` with a comment saying why it cannot lower the
filter or act on the character outside a stay, and have it reviewed like a firewall change. A
stay-boundary failure prints the path from where the code starts running to the move: do that
work from the scenario's timeline, or check `IsInInstance` first. If
`ZoneSession` starts using a new game or Dalamud API, extend the fakes to match the real API's
behaviour, not just its shape, and add a contract test pinning that behaviour.

A contract failure means Dalamud (or ClientStructs) changed something the fakes model. Update
the fake to the new behaviour, then see what the firewall tests make of it: that is the change
the plugin has to answer.

## What these tests cannot see

- Whether the hooks sit on the right game functions after a patch, and whether the game has a
  send path that bypasses the hooked one. Only the runtime checks (the arm check, the per-frame
  guard) and packet capture can.
- A move of the character no call path shows: reflection, a delegate made at runtime, native
  code. The stay-boundary rule follows every path it can see; the guard only runs during a stay,
  so nothing stops the rest at runtime.
- A server-side move with no client-side sign (a GM move) while the receive filter hides it.
- The single frame after something outside the plugin takes a filter hook down: the guard stops
  the game on its next tick, but the game may send in between (`AcceptedRisks` in the harness).
- An unload mid-sim: the inn reload can't be waited for without frames, so the game's post-load
  packet can follow the lift (`AcceptedRisks`).
- Dalamud behaviour that only runs inside the game (which packet moves Dalamud's territory, a
  SafetyHook patch failing) is pinned from its IL, not run.
- The debug hold's release puts the character back where the server last placed it (the hold's
  start, or a zone-in during it) but does not verify it, cannot when the character is absent at
  the release, and follows no other server-side move made during the hold (Debug builds only).
