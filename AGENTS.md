# CLAUDE.md

## User-required local workflow

- The canonical working project is `D:\FF14\ModCoding\AnoMech`. Make all future AnoMech changes here, including work shared with the user's friend. Other AnoMech copies are reference/recovery sources only.
- Preserve the merged Yoruha custom scenarios and the collaborators' existing work. Keep the `(custom)` display name and Yoruha attribution.
- After changing the mod, run `tools/Rebuild-Custom.ps1` to perform a Release **Rebuild**, then verify the resulting DLL. Do not claim runtime/gameplay/audio validation from compilation or headless tests.
- Publish only to `D:\FF14\Mods\AnoMesh\` (DLL directly inside) and its `AnoMech-custom-Yoruha.zip`. Replace these fixed outputs on each successful rebuild. Do not create timestamped mod folders, backup ZIPs, or duplicate installation copies.
- Delete superseded generated outputs after the replacement has been built and verified. Temporary `.AnoMesh.next` / `.AnoMesh.previous` directories must not accumulate. Build failures must leave the last successful package intact.
- Cleanup is limited to this script's generated artifact directories and files. Never delete source, `.git`, game/plugin configuration, the friend's development files, or unrelated mods. Do not silently overwrite a different active development plugin.
- A deliberately focused test run may use `-TestFilter`. Use `-SkipTests` only when the same sources have already passed appropriate checks; record the limitation.

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## What this is

AnoMech is a Dalamud plugin that simulates FFXIV ultimate-raid mechanics client-side, so players can
practice them solo or in multiplayer. It spawns fake `BattleChara`s (party doppels and bosses) in an
inn, drives their casts and movement, and renders the real VFX, cast bars, tethers and HUD. 

## Build / run

- Build: `dotnet build` from the repo root. The Dalamud SDK resolves from
  `%AppData%/XIVLauncher/addon/Hooks/dev/`.
- The plugin can't run standalone: it only runs inside the game, loaded by Dalamud.

## Tests

- The user verifies most changes in game; unit tests cover the logic that can run headless.
- NUnit suites: `tests/AnoMech.Tests` and `tests/AnoMech.Relay.Tests`. 
- Scenarios run headless against fakes (`ScenarioCatalogTests`: every strat, random seeds, no
  deaths). `IGameData` in tests (`DataminingGameData`) reads real sheets from
  xivapi/ffxiv-datamining CSVs downloaded into `obj/datamining/<commit>/`; bump `DataminingCommit`
  in `AnoMech.Tests.csproj` for a newer patch, and add a sheet to `DataminingSheet` when `IGameData`
  starts reading it.
- Layout: one test file per concept tested, placed in the directory that mirrors the production
  code it covers (`AnoMech.Relay/Server/` → `tests/AnoMech.Relay.Tests/Server/`). 
  Fakes of a production interface go in a `Fakes/` folder beside that interface's package
  (`AnoMech/Core/Native/Interfaces/` → `tests/AnoMech.Tests/Core/Native/Fakes/`);

## Comments

Write a comment only for what the code cannot say itself — rationale, a non-obvious constraint, why
the obvious approach was avoided. Avoid comments that:
- restate a symbol's name in prose (`// P5 arena state` above `InitP5Arena`);
- restate literals or contract visible at a glance (`// TerritoryId 1363` beside
  `TerritoryId => 1363`);
- narrate what an edit changed (git records that);
- re-explain what an interface member's own doc already says;
- pile on examples for something trivial;
- cite where a value came from — log filenames, pull numbers, capture dates, sample sizes, tool
  invocations (`// Confirmed against 3 real pulls (Network_30208_*.log, pulls 2/3/5)`). State what
  the game does, not how it was measured. Provenance belongs in the session notes, not the source.
  The exception is a caveat that changes how the code should be trusted: mark a value that was never
  observed as UNVERIFIED, and keep a note that an approach was tried and found inert so nobody
  retries it.

When unsure, cut it — sparse and load-bearing beats thorough. Scenario AI strats (`*Ai.cs`) go
stricter: no comments, intent carried entirely by descriptive method names.

**Don't trust comment**. They are often straight up wrong, written based on false evidence, not true in every 
context or just out of date. Always take into consideration that comment might be false,
communicate that with user and try to verify first.

## Architecture

- **Projects.** `AnoMech/` (the plugin), `AnoMech.Relay/` (multiplayer relay library: wire format
  in `Network/`, server in `Server/`), `AnoMech.Relay.Host/` (standalone relay exe + Dockerfile).
- **Frame loop.** `Plugin.OnFrameworkUpdate` → `Natives.Tick` (first, so the zone guard runs even
  when `Game.Tick` is paused or throws) → `RunController.Tick` → `Game.Tick` (`Core/Game/Game.cs`)
  → `Multiplayer.Tick`. `Game.Tick` ticks the `InstanceSession` and checks for falls, then, unless
  paused, ticks `EventScheduler` → `SimWorld.Tick` (map, party + `PartyHud` refresh, children, then
  `EnemyList`) → `ScenarioSession`. All on the Framework thread. `EventTimeScale` scales all three
  of those ticks, so sim-side cast, status and movement timers speed up with it; only the client's
  own animation runs at real time.
- **`Core/SimObjects/`** — in-world entities (`SimWorld` root, `SimCharacter` / `SimEnemy` /
  `SimParty`, `SimCast`, `SimStatus`, `SimTether`, …). Read the rules in the header of
  `ISimObject.cs` before adding a type: spawn through the parent's API (`SimWorld.SpawnEnemy`,
  `SeatParty`, `Tether`, …), never `new` from outside; helpers live in `Core/`, not here.
- **`Core/Game/`, `Core/Map/`, `Core/EnemyActions/`, `Core/UserActions/`** — orchestration (`Game`,
  `EventScheduler`, `Movement`, party creation, AI), arena/map state, boss action definitions, and
  client-side resolution of the player's own actions.
- **`Core/Native/`** — everything that touches the game client. `Interfaces/` is the game boundary
  and the only part of Native that sim code (`Core/SimObjects`, `Core/Game`, `Core/Map`,
  `Scenarios`) may name. Sim code reaches the game through the static `Natives` locator (Plugin
  installs implementations, tests install fakes via `FakeGame.Install`) and per-object proxies
  (`IBattleCharaProxy`, `IEventObjectProxy`). Implementations hold no sim logic. Plugin glue
  (`Plugin.cs`, `RunController.cs`), Windows, Multiplayer and UserActions handlers may use
  `Implementations/` directly. Where Dalamud already
  has a service interface (`IFramework`, `IChatGui`, …), call `Plugin.<Service>` and stand it in via
  `tests/AnoMech.Tests/Support/DalamudServices.cs`; `IGameData` is the exception, because Lumina
  rows can't be built without game files.
- **Native interop** walks `FFXIVClientStructs` types directly. 

## Scenarios

- **Purpose.** A scenario exists to let players practice a mechanic. Match the game where it
  matters for learning it (timing, positions, hitboxes, what you have to read off the screen), and
  simplify or skip the rest. Don't chase 1:1 fidelity that doesn't change how the mechanic is
  played.
- **Keep it simple.** A scenario should read as a plain description of the fight. Logic that more
  than one scenario needs belongs in `Core/`, built once and reused, not copied between scenarios
  or hidden in a family's helper class.
- **Structure.** `IZone` (territory, origin, waymarks) ← `IPhase` (weather, BGM) ← `IScenario`.
  Each scenario points up at its phase, and `ScenarioCatalog` builds the menu tree from that. Register new
  scenarios in `ScenarioCatalog`. A family folder (`Scenarios/Top/`) holds `<Family>Zone.cs`,
  `<Family>Constants.cs` (IDs) and `<Family>Actions.cs` (`EnemyAction` definitions). Each scenario
  folder holds `*Scenario` (timeline, spawns, casts), `*Ai` (party strats), `*State` (per-run
  rolls) and `*StateOverrides` + `*SettingsWindow` (user-forced rolls).
- **Strats live only in `*Ai`.** `*Scenario` and `*State` describe what the fight does, the same
  for every strat, and never branch on which strat is selected. The one allowed exception is a
  simplification that depends on the strat, e.g. skipping something the selected strat makes
  irrelevant.
- **Timeline.** Prefer `world.Events.Add` with an absolute time literal from scenario start, kept in
  ascending order, so the scenario reads as the fight's timeline. Avoid offset arithmetic and
  scheduling from inside handlers by default; exceptions are fine where they make the scenario
  simpler 
- **Boss attacks** are `EnemyAction`s. Take cast times and AoE shapes from the Action sheet rather
  than hardcoding them. Use literal coordinates for spawns and moves unless a position really
  changes from run to run.
- **Sim events** reach the running scenario through its `IWorldEventListener` hooks
  (`OnActionResolved`, `OnStatusAdded`, `OnStatusLost`, `OnTetherEnded`). They are for what retail
  itself does when that happens: a debuff that detonates as it runs out or on its holder's death, a
  tether that snaps, an NPC that breaks when hit, a tower punished when left unsoaked. The timeline
  stays in `Events.Add`, and what an action does to its own targets stays in its `EnemyAction`
  effects. A reaction several scenarios share is a helper each one calls from its hook
  (`HelloWorldFailures`, `Lightsteeped`, `Towers.ResolveTower`). Scheduling the follow-up from a hook
  is fine, and beats storing a cast's result for a later timeline step. Add a new event type only
  when a scenario can't be written cleanly without it.
- **Debuff that goes off when it runs out:** drive it from `OnStatusLost` only when something can
  remove, cleanse or pass the debuff, or when its holder's death changes what happens. A plain
  countdown nothing interacts with stays a timeline step at its expiry time.
- **Avoid scenario state.** Every field a scenario fills mid-run is something to reset, keep in
  sync and reason about. Read what the event already carries instead (a tether's `TetherId`, a
  status's `Stacks`, a resolve's `Origin` and `Hits`), and pass values through the closure of a
  scheduled follow-up. Keep a field only when the fight genuinely needs memory across steps that
  no event can carry.
- **Randomness** comes only from `world.Rng`, rolled into `*State`, so a seed reproduces a run.
  Every strat in `AiStrats` must clear the fight with no deaths: `ScenarioCatalogTests` checks this
  for every catalog entry.

## Multiplayer

Being rewritten. Existing code doesn't follow it yet.

- **Keep multiplayer out of Core and Scenarios where possible.** The sim runs once, on the host,
  as it does solo, and multiplayer logic belongs in `Multiplayer/`. Avoid host/peer branches,
  multiplayer-only flags, overloads or mirrored state in Core and Scenarios; add them only when
  there's no other way. Core objects that exist because of multiplayer (e.g. the remote player's
  puppet, `SimNetworkPuppet` / `RemoteMotion`) are fine. Keep them minimal.
- **Broadcast through Natives.** Multiplayer installs its own `Natives` implementations (and
  proxies) that call the real one and forward the call to peers. Peers run no sim; they apply what
  they receive. Spawn ids are deterministic, so most calls replay verbatim; calls aimed at the local
  player (self-only ActorControl, local statuses, knockback, gauge, LB) and per-client views (party
  HUD, enemy list) are routed or rebuilt for each client's own seat.
- **Zone-in and start are separate.** Everyone zones in first; the host starts once all are in.
- **Movement.** Each client sends its own pose every tick; other seats are steered from those
  poses via `RemoteMotion`, like bots and NPCs. Forced moves go to the owning client, which runs
  them itself and reports the result through its poses.
- **Cleanup.** Multiplayer tracks what it spawned on each peer and clears it on run end,
  disconnect or host leave.
