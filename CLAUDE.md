# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

AnoMech ("Another FFXIV mechanics simulator") is a Dalamud plugin that **simulates FFXIV raid mechanics client-side** — it spawns fake `BattleChara` instances (party doppels and bosses) into the live game, drives their actions, and renders the canonical VFX/cast bars/tethers so players can practice mechanics solo. Currently focused on ultimate-raid phases (TOP P5 Delta / Sigma) but the engine is general-purpose. It is NOT the SamplePlugin template the README still describes; only the project skeleton was inherited.

The reference scenario is **TOP P5 Delta** (`Scenarios/Top/P5Delta/`). Treat it as the canonical example of how a scenario consumes the engine — when designing new APIs, look at how it would read there. Scenarios are nested by family (e.g. `Scenarios/Top/`); IDs that recur across phases of the same family live in a shared `<Family>Constants.cs` (e.g. `Scenarios/Top/TopConstants.cs`) and are referenced from phase scenarios via the fully-qualified path `TopConstants.<Group>.<Const>`.

## Build / run

- Build: `dotnet build` from `D:/Projects/ffxiv/AnoMech/`. Output is `bin/Debug/AnoMech.dll`.
- The csproj uses `Dalamud.NET.Sdk/15.0.0` — Dalamud SDK resolves at build time from `%AppData%/XIVLauncher/addon/Hooks/dev/`. No NuGet restore tweaks are needed.
- **There are no automated tests.** Verification is "build clean → load DLL via Dalamud Dev Plugins → run a scenario in-game and watch." For UI / behavior changes, ask the user to run the plugin; you cannot.
- In-game entry point: chat command `/anomech` (alias `/ano`) opens the main window. Subcommands: `config`, `start`, `reset`, `leave`. Buttons in the main window run scenarios and despawn/reset.

## Comments

Write a comment only for what the code cannot say itself — rationale, a non-obvious constraint, why the obvious approach was avoided. Delete comments that:
- restate a symbol's name in prose (`// P5 arena state` above `InitP5Arena`);
- restate literals or contract visible at a glance (`// TerritoryId 1363` beside `TerritoryId => 1363`);
- narrate what an edit changed (git records that);
- re-explain what an interface member's own doc already says;
- pile on examples for something trivial.

When unsure, cut it — sparse and load-bearing beats thorough. Scenario AI strats (`*Ai.cs`) go stricter: no comments, intent carried entirely by descriptive method names.

## Architecture

### Frame loop
`Plugin.OnFrameworkUpdate` → `Game.Tick(dt)` → `SimWorld.Tick(dt)` → ticks `EventScheduler` (scaled by `EventTimeScale`), then each `SimEnemy`, `SimParty` (which ticks each `SimPartyMember`), `LocalPlayerOps`, each `SimTether`, then refreshes `EnmityHud` and `PartyHud`. Everything runs on the Framework thread.

### Two-layer Core

**`Core/SimObjects/`** — in-world entities. All implement `ISimObject` (`bool IsAlive`, `Tick(float)`, `Despawn()`); position-bearing ones implement `IPositioned`. The contract and design rules live in the header comment of `ISimObject.cs` — read it before adding a new SimObject type. Core types: `SimWorld` (root), `SimNpc` (base wrapper around a `BattleChara*`), `SimEnemy : SimNpc` (cast bars, action timeline), `SimPartyMember : SimNpc` (CharacterManager registration), `SimParty` (8-slot container), `SimTether` (effect with auto-expire). **Spawn through the parent** (`SimWorld.SpawnEnemy`, `SimWorld.InitializeParty`, `SimWorld.Tether`) — never `new` these from outside.

**`Core/` (root)** — helpers. Engine plumbing that is *not* a game object: `VfxFunctions` (signature-scanned native VFX/tether functions), `Statuses` (direct `StatusManager` writes that bypass server packets), `PinnedStatus` / `TimedStatus` (status-lifetime helpers re-stamped each tick), `LocalPlayerOps` (VFX/status on the real local player), `PartyHud` / `EnmityHud` (mirror SimObjects state into game UI addons via `AddonLifecycle.PreRequestedUpdate`), `EventScheduler`, `TetherTarget` (unifies `SimNpc` and the local player), `PartyMemberOrPlayer` (lets scenarios address all 8 slots uniformly with player-fallback), `MathUtil`, `PartyPresets`, `Waymarks`. These do NOT go in SimObjects.

### Scenarios
`Scenarios/IScenario.cs` is the contract: `Run(SimWorld, PartyRole)`. `Game.RunScenario` wires up origin snapshot, party init, waymarks, then invokes `Run`. A scenario typically declares its event timeline via `world.Events.Add(offset, action)` — the EventScheduler then fires each lambda at the scheduled time. The TOP P5 Delta scenario at `Scenarios/Top/P5Delta/` is split into `TopP5DeltaScenario` (event timeline + spawns + casts), `TopP5DeltaAi` (party-member movement choreography), `TopP5DeltaState` (per-run randomization), `TopP5DeltaConstants` (phase-specific BNpc / action / status / tether IDs), `TopP5DeltaSettingsWindow` + `TopP5DeltaStateOverrides` (debug overrides). Family-shared IDs (boss BNpcBase rows, Hello-World action IDs, arena radius) live in `Scenarios/Top/TopConstants.cs`.

### Scenario timeline conventions
- **Use absolute time literals in `world.Events.Add`.** Every entry in a scenario's `Run` should be `world.Events.Add(<absolute t>, ...)` from scenario start (e.g. `55f`, `56.5f`) — not `<base> + offset` arithmetic, named time constants, or chained `Events.Add` calls inside event handlers. The whole scenario timeline should be readable top-to-bottom as a single list of absolute timestamps. If state needs to flow between events, store it on `TopP5DeltaState` (or the equivalent scenario-state object) and have each handler read/mutate it.

### Heavy native interop
Most SimObject code is `unsafe` and walks `FFXIVClientStructs` types directly (`BattleChara*`, `GameObject*`, `StatusManager`, `CastInfo`, `Timeline`, `DrawData`, `CharacterManager`, `ClientObjectManager`, `GroupManager.MainGroup`, `UIState.Hate/Hater`, `AgentHUD`, `AtkArrayData`). When you need to know what a field does, prefer the local checkouts (see `~/.claude/projects/D--Projects-ffxiv-AnoMech/memory/MEMORY.md` references) over websearch.

## Non-obvious things to know before changing engine code

- **Scenarios are inn-only; CharacterManager registration is unconditional.** Scenario runs are hard-gated at `Game.RunScenarioInternal` (`ZoneSession.IsInInn()` check); UI gates in `Plugin.OnCommand "start"` and `MainWindow` are friendly UX layers over the same invariant. Every spawned BattleChara — party doppels (via `PartyCreator.Populate`) and bosses (via `SimEnemy.Spawn`, when visible) — is inserted into `CharacterManager._battleCharas` and unregistered in the matching `Despawn`. This is safe because the inn-only invariant keeps us out of the open-world render-cache teardown crash path (mechanism documented in `Core/EnmityHud.cs`): the per-frame `CharacterManager` update attaches the BC into render-side caches (Skeleton, CharacterLookAtController) that `DeleteObjectByIndex` doesn't drain, but the inn's low BC density and controlled teardown avoid the crash window. A resolver hook (`Hook<>` on `LookupBattleCharaByEntityId`) was tried as an alternative and removed: crashed reliably via Reloaded.Hooks trampoline interaction with upstream `ActorControl` detours. With doppels always in the array the canonical `LookupBattleCharaByEntityId` walks them, so the engine resolves them naturally — `_PartyList`'s addon agent then drives status icons + timer text + Targetable from each resolved BC on its own. `PartyHud` only writes `MainGroup.PartyMembers` per frame (required because the addon's render-members path is gated on `MemberCount > 0`); status icon arrays and timer text nodes are no longer touched. Row-click targeting and mouseover tooltips on doppels work everywhere a scenario can run.
- **Status countdowns are managed by us, not the engine.** Direct-slot writes via `Statuses.Apply` / `Statuses.Remove` keep `bc->StatusManager.Status[]` packed at low indices. `PinnedStatus` re-stamps `RemainingTime = -1f` each frame; `TimedStatus` and `SimTether` re-stamp the visible counter against their own elapsed time. Don't trust the engine's StatusManager decrement.
- **Cast bars are entirely simulated.** `SimEnemy.Cast` writes `CastInfo` directly and ticks `CurrentCastTime` itself; on completion it manually fires an `ActionEffectHandler.Receive` with a synthetic header (mimicking the server's ActionEffect packet) so the release animation/VFX play. Bypassing `Character::StartCast` means the AOE telegraph (omen) is never auto-spawned — `SpawnCastOmen` reads `Action.Omen.Path` and spawns a StaticVfx manually.
- **Bad VFX paths crash on the file thread.** Always validate via `Plugin.DataManager.FileExists` before calling any `VfxFunctions.Spawn*` — `SimNpc.AddVfx` and `SimEnemy.SpawnCastOmen` already do this.
- **Movement requires both `SetPosition` and a timeline override.** Direct field writes only move the hitbox/nameplate; the visible model needs `SetPosition` (propagates to DrawObject) + `Timeline.BaseOverride = 22` (run loop) for the model to actually animate. See `SimNpc.MoveTo` / `StartMoveAnim`.
- **`Game` is the orchestrator, `SimWorld` is the root SimObject.** `Game` lives in `Core/` (not `SimObjects/`) because it's not an in-world entity — it owns the World and holds the scenario catalog. `SimWorld.Despawn()` is implemented as an explicit interface method that forwards to `Reset()` (kept separate from `Dispose()` because `Reset` is reusable teardown between scenarios).
- **`EventTimeScale` only scales the `EventScheduler`.** Cast bars, animations, movement, and status ticks all run at real time. The Speed buttons in `MainWindow` set `EventTimeScale` so a scenario's timeline can be sped up without breaking visible animation timing.
