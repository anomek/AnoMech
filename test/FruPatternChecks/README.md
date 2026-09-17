# Fulgent Blade regression checks

Run `dotnet run --project test/FruPatternChecks` from the repository root. This
builds the production pattern geometry and party AI without Dalamud or a running game client.
It checks FRU-Sim's six-dodge route in all 32 combinations of origin, rotation,
and starting side, plus strip depth, lateral coverage, travel between hits, and
native arrow orientation for both colors on every seam. Bot travel is simulated
at six yalms/second at 15, 30, and 60 FPS through every wave snapshot, including
the transition into Akh Morn. Checks cover arena bounds, arrival before the hit,
opposite boss-relative stack sides, and exactly four members in each stack.
`AiIntegrationChecks` also runs the production AI with the real event scheduler
and minimal engine stand-ins to check all eight player roles are excluded from
AI movement, bots receive the correct route/stack, dead bots are skipped, and
clearing the scheduler cancels pending movement. Native client behavior still
requires an in-game run.

The comparison uses [FRU-Sim at 2a77c85](https://github.com/WCGH/FRU-Sim/tree/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p5):

- `controllers/exawave_controller.tscn`: three emitter transforms; E/N/W or W/N/E order.
- `exawave/exawave.tscn`: opposing light/dark pairs, seven snapshots two seconds apart.
- `exawave/wave_controller.gd`: wide strips traveling perpendicular to their length.
- `sequences/fb_positions.gd`: the independently authored safe route used by these checks.
- `p5_main.tscn`: base lines at 4s, group activation at 6.2/10.2/14.2s,
  first snapshots at 13/17/21s, and Akh Morn at 28.5s. AnoMech adds its 7.5s raidwide lead-in.

FRU-Sim's coordinates are larger than game units: its 11.855-unit step corresponds
to the native five-yalm wave. Positions are scaled by `5 / 11.855`; native action
data supplies the 80-yalm strip width and four-yalm Akh Morn radius. Godot serializes
TSCN basis matrices as rows, despite exposing basis columns through its public API.

The intermediate side/order cue advances the existing native line effect into
its charging stage at 13.5/17.5/21.5s, on the same randomized groups that resolve
at 20.5/24.5/28.5s. `b3560omn01_y1.avfx` trigger index 3 (queue number 4)
starts timeline 3, whose TRG clips activate stages 5/6/7 at authored frames
0/59/119. Those stages supply the native colored particles and transitions.
The earlier helper casts (40307/40118, `m0531_sp03l_o0v`/`m0531_sp03d_o0v`)
have been removed from this warning stage: their cast effects were a separate
visual layered over an otherwise unchanging seam, not the seam's charging
sequence. No custom RGB tint or replacement overlay is used. Initial activation,
arrow timing, wave snapshots and world-owned cleanup are unchanged.

The bouncing arrows are **not** action 40115 / `n4gw_b_g01_c0v.avfx`.
Local game-data inspection traced EObj `0x1EBBF7` through ExportedSG 38454 to
`bg/ex3/01_nvt_n4/shared/for_vfx/sgvf_n4gw_b3560.sgb`. Its VFX instance 2 uses
`bg/ex3/01_nvt_n4/common/vfx/eff/b3560omn02_y1.avfx`, at unit scale. This scenery
effect has no actor binder; its default scheduler starts timeline 1, with four
emitters. The arrow particles use `mark115_o.atex` and rise to 2.25 yalms before
falling. Emitter 0 sends purple particles toward native +X; emitter 1 sends gold
particles toward -X. `ArrowWarning` turns those axes onto the corresponding wave
directions (dark-wave yaw minus pi/2), checked for all 192 seams in the 32 patterns.
The effect is spawned as a world-owned StaticVfx at unit scale, not via AddVfx or
action-omen width/depth scaling. Its looping emission is explicitly removed after
2.3 scenario seconds or on reset. The warning plays
at 18.2/22.2/26.2s, 2.3s before each group's first wave. This starts the native
fade-in 0.5s earlier than the reference's box/arrow stage; wave snapshots are
unchanged, and the warning remains until each group's first wave.
Actions 40308/40309 supply the light/dark moving-hit effects and have no omen.
Initial seams now use the same scenery object's native `b3560omn01_y1.avfx`
at unit scale, with the same placement/rotation as the arrows. The ImGui ground-line
overlay and its hand-tinted color bands have been removed. The game now renders
the authored gold/purple particle colors, textures, brightness and blending,
with each color on its corresponding wave side. No global tint is applied.
Unlike the arrows, this asset has no default scheduler timeline: trigger 1 starts
timeline 1's persistent emitter 0. `SimOmen` waits for a resource instance, then
queues the trigger once through the native resource-instance API. That API handles
loading and dispatch on the game's VFX scheduler. AVFX slot 1 maps to queue number 2.
It logs a warning if no resource instance becomes available within five scenario seconds.
The earlier direct Apricot call was invalid: it expects an internal scheduler
object, not `Scene.VfxObject*`. The September 16 crash log showed it dereferencing
the scene object's sentinel at offset 0x230. That binding has been removed.
The replacement queue binding was rechecked against game build
`2026.09.15.0000.0000` with Dalamud `15.0.3.5` at RVA `0x38F5E0`;
the existing signature still matches once. It takes `VfxResourceInstance*`, returns
void, and uses one-based trigger numbers. The managed bridge checks guard against the wrong pointer type,
off-by-one indices, missing resources, and invalid trigger slots; they do not
execute game code or prove in-game rendering/crash freedom.

The plugin requires Dalamud `15.0.3.5` or newer and remains on SDK/API 15.
The September 17 compatibility audit found exactly one raw `.text` match for
each of the 27 custom signature usages (24 attributes and three direct scans).
The inspected executable's SHA-256 is
`5bbc501dd5c7f22fd61a11d08c25356041d878db7cd83203adae393e4dfacc44`.
The updated FFXIVClientStructs assembly still places the scene VFX resource pointer
at `0x2A0`; signature uniqueness and these metadata checks do not replace live-game
validation. Build against XIVLauncher's updated `Hooks/dev` assemblies; the SDK
version is not the same as the installed Dalamud runtime version.

The line effects advance through charging and remain through the directional
warning; their handles are removed when each
group's first wave starts (or by reset), so changing timeline speed cannot
expire a later group's seams early. The later warnings and hits continue to use
native effects. Line thickness and visibility now come from the game's VFX
renderer, not a fixed-pixel screen-space drawing.

An in-game run is still required to verify native effect orientation, brightness,
arrow timing, color matching to the waves, and the thin-seam presentation.
These checks validate geometry,
not the native renderer or Akh Morn targeting. Also check reset during a warning
and during traveling waves, and four-person Akh Morn stacks on both boss-relative sides.

Pandora targets and follows the existing Main Tank party slot, whether it holds
the player or a bot. Select Main Tank in the role picker to tank personally;
normal party starts fill that slot with a bot when the player selects another role.
Following uses the shared hitbox-aware movement and pauses during casts/animation
locks, resumes afterward, and stops when the tank dies or the world resets.
Solo non-MT practice leaves Pandora without a follow target until the late support
party arrives. Akh Morn faces the living main tank; if the solo MT slot is still
empty, it faces the dodge pocket's cardinal before starting its cast.
Check player-MT, bot-MT, cast pause/resume, tank death, and reset in-game.

The Standard AI's main-tank bot aims Pandora toward the cardinal side containing
the randomized exawave dodge pocket (north, south, east, or west). At 8.5s it
prepares six yalms toward that side. It follows the normal six dodges, except
at 27.5s it shifts the fourth dodge toward the cardinal ray from Pandora's actual
position, up to 1.5 yalms. Candidates must clear every 28.5s wave strip by at least
0.25 yalms; if exact alignment is unsafe it takes a smaller adjustment, with the
ordinary dodge as fallback. Akh Morn still faces the actual tank and locks that
facing for its cast. The 33.5s light-party assignments use that observed facing.
The AI never rotates Pandora directly or moves a player-controlled main tank.
The 32-pattern movement checks at 15/30/60 FPS cover tank arrival before the cast,
improved alignment, wave clearance, and both four-person stacks. Native movement,
cast-facing behavior, and the visual result still require an in-game check.

Normal party starts now run the Standard bot strategy. Bots preposition at 15.5s,
then dodge at 19.5/23.5/25.5/27.5/29.5/31.5s using the same randomized pattern as
the waves, with the main-tank adjustment described above. No positional jitter
is added inside the small dodge pockets. At
33.5s they move to the reference's two cleared Akh Morn spots, labeled left/right
using Pandora's current facing. MT/H1/M1/R1 take the left stack; OT/H2/M2/R2 take
the right. The player must dodge and join their assigned stack themselves; the
AI only issues movement to living `SimPartyNpc` instances. Solo starts leave the
exawave dodges manual and schedule only the late Akh Morn support described below.
Mispositioning Pandora or missing a stack can still cause failure;
bots do not teleport, gain immunity, or move the player to correct mistakes.

In solo practice, seven level-100 support bots appear directly at their assigned
light-party stack positions at 33.5s, before Akh Morn resolves at 36s. The player's
role stays empty of bots, giving their side three bots and the opposite side four.
The player must join their assigned group; both modes enforce the four-person and
non-overlap checks. `FillMissingPartyMembers` adds only unoccupied roles to the
existing party, retaining the player, HUD, damage references, and reset ownership.
It never replaces existing or KO'd members. The newly filled MT slot becomes
Pandora's follow target while her cast still locks facing. No support is spawned
after the player dies, after Pandora disappears, or after a reset cancels the event.
`SoloPartyChecks` executes the production solo scheduler across 256 pattern/role
combinations, checking timing, slot preservation, level, both stacks, and every
remaining wave snapshot. Native bot allocation/rendering and post-spawn cleanup
still require an in-game check.

Bot spawn levels use the duty's configured level: FRU bots are level 100,
including when the player selects an explicit role. Presets are copied per run
so this does not overwrite their defaults or leak level 100 into other duties.
Checks cover all player slots, automatic job selection, and a lower-level duty
restart. Bots retain simplified fixed HP and cosmetic gear; per-bot equipment
stat syncing is intentionally not modeled. FRU's existing item-level-735 zone
configuration is unchanged.
