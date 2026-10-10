# CLAUDE.md

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
- **Frame loop.** `Plugin.OnFrameworkUpdate` → `Game.Tick` (`Core/Game/Game.cs`) → `EventScheduler`
  (scaled by `EventTimeScale`) → `SimWorld.Tick` (map, children, then `EnmityHud` / `PartyHud`
  refresh). All on the Framework thread. `EventTimeScale` only scales the scheduler; casts,
  animation, movement and statuses run at real time.
- **`Core/SimObjects/`** — in-world entities (`SimWorld` root, `SimCharacter` / `SimEnemy` /
  `SimParty`, `SimCast`, `SimStatus`, `SimTether`, …). Read the rules in the header of
  `ISimObject.cs` before adding a type: spawn through the parent's API (`SimWorld.SpawnEnemy`,
  `CreateParty`, `Tether`, …), never `new` from outside; helpers live in `Core/`, not here.
- **`Core/Game/`, `Core/Map/`, `Core/EnemyActions/`, `Core/UserActions/`** — orchestration (`Game`,
  `EventScheduler`, `Movement`, party creation, AI), arena/map state, boss action definitions, and
  client-side resolution of the player's own actions.
- **`Core/Native/`** — everything that touches the game client. `Interfaces/` is the game boundary
  and the only part of Native that sim code (`Core/SimObjects`, `Core/Game`, `Core/Map`,
  `Scenarios`) may name. Sim code reaches the game through the static `Natives` locator (Plugin
  installs implementations, tests install fakes via `FakeGame.Install`) and per-object proxies
  (`IBattleCharaProxy`, `IEventObjectProxy`). Implementations hold no sim logic. Windows,
  Multiplayer and UserActions handlers may use `Implementations/` directly. Where Dalamud already
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
  Each scenario points up at its phase, and `Game` builds the menu tree from that. Register new
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
- **Randomness** comes only from `world.Rng`, rolled into `*State`, so a seed reproduces a run.
  Every strat in `AiStrats` must clear the fight with no deaths: `ScenarioCatalogTests` checks this
  for every catalog entry.
