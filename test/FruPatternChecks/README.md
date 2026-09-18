# FRU regression checks

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

## Paradise Regained

`ParadiseRegainedChecks` covers all 12 patterns (three tower rotations, either
remaining-tower order, and dark-first/light-first). The standalone P5 scenario
uses a full party. Healers take the first tower; M1/R1 take relative northwest,
and M2/R2 relative northeast. MT takes the first cleave and OT the first distance
bait, then they swap. Player movement remains manual in every role.

Timing follows [FRU-Sim's Paradise Regained sequence at 2a77c85](https://github.com/WCGH/FRU-Sim/blob/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p5/sequences/p5_pr_seq.gd):
cast at 1.3s, towers at 6/9.5/13s, Wings at 8.3s, and tower snapshots at
15.7/19.2/22.7s. Wing cleaves and tank baits accompany the first two snapshots.
At 25s Pandora faces north, the tower/buster helpers are cleaned up, and
Polarizing Strikes begins with the same boss and party in this scenario.

Native visuals use these installed game resources:

- Map slots 51/52/53, `sgvf_n4gw_b3559.sgb`, activation `0x00020001`.
- Actions 40233/40313, ActionCastVFX 586/587, VFX 2265/2266:
  `vfx/common/eff/m0914_cst_b2lp_c0v.avfx` and `m0914_cst_b2lp_c1v.avfx`.
  The native cast supplies the ordered wing warnings automatically.
- Cleaves 40314/40315, tank baits 39879/39880, tower hit/failure 40320/40321.

There are no added overlay shapes, generic substitute omens, or custom-painted
effects. Geometry is used only for damage checks. Tower centers come from the
native layout at radius 7; towers use the action's radius 3 and baits radius 4.
The 240-degree cleaves use +/-60-degree offsets consistent with the
[BossMod encounter definitions at 0d922f7](https://github.com/awgil/ffxiv_bossmod/tree/0d922f7ab1149a10b091993124c7a165928175b3/BossMod.Ultimate/Dawntrail/Ultimate/FRU).
Native action rows give their range as 100. Bot positions account for the actual
tower radius instead of using the reference simulator's larger visual rings.

The checks simulate 288 pattern/role/frame-rate runs at 15/30/60 FPS and execute
96 full production-scenario runs with recording stand-ins for the native engine.
They cover two-person occupancy, isolated tank hits, nearest/farthest selection,
player control, native action/target/map requests, helper cleanup, over/under-soak
failures, missing actors, and early reset. `SimMapEffect` itself is linked into
the checks so hide-on-reset and idempotent teardown run production code.

Rendering and native packet handling still need an in-game run: verify both wing
orders, the tower activation/dismissal states, cleave alignment, tank-buster
placement, and reset during the cast. The harness validates requests and managed
behavior; it does not render or execute the native client.

### Polarizing Strikes continuation

The [reference Polarizing Strikes script](https://github.com/WCGH/FRU-Sim/blob/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p5/sequences/p5_ps_seq.gd)
and `p5_main.tscn` provide four stack hits at scenario times
35.3/40.0/44.7/49.4s, followed by echoes at 37.4/42.1/46.8/51.7s.
The first cast starts at 28.5s; three Polarizing Paths casts start at
37.2/41.9/46.6s. This is an internal sequence, not another scenario entry.

Living players closest to Pandora on each boss-relative side bait the native
100-by-6-yalm lines. This uses retail targeting rather than the Godot script's
random-side approximation. Each line needs exactly four members; overlapping
lines, underfilled stacks, and repeated exposure to a color's resistance-down
status fail. The front bait receives native Light/Dark Resistance Down
(4164/3323). Pairs lead in tank, melee, ranged, healer order, crossing to the
other side after their own bait. Bots move normally and never move the player.
Echoes query the original line placements; they do not retarget moving players.

Native resources, verified in the installed game data:

- Boss casts 40316/40234 share timeline 3205, `mon_sp/m0914/mon_sp005`.
- Light/dark strikes 40317/40318 use timelines 11290/11291,
  `n4gw_boss_gimmick06/07`, and the authored
  `vfx/monster/gimmick5/eff/n4gw_b_g06_c0v.avfx` / `n4gw_b_g07_c0v.avfx`.
- Echo actions 40119/40120 use the native hit-only timeline 1222. Their visible
  trail belongs to the original strike AVFX; the implementation does not replay
  the first-hit visual or add a substitute rectangle for the echo.

Each round has its own pair of native helpers, allocated at the handoff and kept
stationary through the echo. Cleanup at 54s removes all eight helpers and the
resistance statuses. Reset uses normal world/party ownership and clears all
pending events. The statuses last until cleanup, independent of event time scale.

`PolarizingStrikesChecks` executes the complete combined scenario in 288
pattern/role/frame-rate runs. It checks four 4+4 stacks, every role leading once,
all four echo dodges, native action counts/order, actor cleanup, and player
control. Negative checks cover stolen baits, a missing side, underfilled stacks,
same-color resistance, standing in an echo, missing Pandora, and early reset.
Native trail timing, colors, status appearance, and alignment still need live
verification. The reference scene accidentally calls the long initial cast for
two follow-ups; the implementation uses the documented/native Polarizing Paths
cast for all three follow-ups while retaining the reference hit schedule.

## Crystallize Time (P4)

The separate **Crystallize Time** scenario uses NA debuff priority and **7–1 Akh
Morn, with MT solo and the other seven on OT**. The solo tank receives AnoMech's
existing simulated invulnerability status during the four hits; this does not
press an actual player job action. Normal player movement remains manual.

Timing and initial routes follow [FRU-Sim's CT sequence at 2a77c85](https://github.com/WCGH/FRU-Sim/blob/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p4/sequences/crystal_time_seq.gd)
and its `ct_positions.gd` / `exaline.tscn`. The encounter uses game dimensions:
12-yalm hourglasses/dragon explosions, 6-yalm water/darkness/eruption circles,
3–12-yalm Blizzard donuts, 15-yalm Aero circles with 30-yalm knockbacks,
40-by-10-yalm traveling strips, 5-yalm Spirit Taker, and 20-yalm Wings knockbacks.
The donut inner radius follows the encounter reference; it is not present in
the Action row. Reference coordinates map north/east to game east/south at
`10 / 23.74` scale. Bots leave dragon intercepts after actual contact to start
their next route, and only knocked-back bots defer
their next move until the Aero slide finishes.

Each cleansing puddle records the role that popped its dragon head. That member
cannot consume their own puddle for its entire lifetime, including after leaving
and returning. This applies to players and bots on either pop. Other members can
still cleanse Fang while the creator overlaps the puddle; an unrelated member
without Fang can still waste it. The one-second arming delay and 17.8-second
lifetime remain unchanged. Checks cover both heads/pops, player and bot creators,
15/30/60 FPS, lingering overlap, re-entry, and a simultaneous eligible recipient.

Spirit Taker spread starts at 40.6 seconds. Bots reserve nearby positions with
six-yalm clearance from the north crystal, the living player's actual position,
and other bots' destinations. Positions stay inside an 18.5-yalm arena radius.
Bots keep safe assignments and yield a spot if the player approaches it, updating
until the 43.4-second jump snapshot. Normal six-yalm/second movement still applies;
a last-moment player approach can leave too little time to escape. The player is
never moved by this spread logic. This is the CT jump, not Somber Dance.
Checks include 480 stationary-player cases across all corners, player roles and
15/30/60 FPS, including a player beside the crystal, plus 12 cases where the
player takes an assigned bot spot. They assert actual pre-jump positions clear
the five-yalm hit radius, bot separation, successful resolution and player control.

Native presentation uses the P4 stage controller (map slot 40), Fragment of
Fate crystal scenery (46), and CT hourglass scenery (34–39), with weather 106. Boss
BNpc rows are Usurper 17833 and Oracle 17835. The Usurper overrides her default
ModelChara 4375 (m0640 body 2) with 4376 (body 3), the native dragon-armored
form with the shoulder dragon and wing bones. Her BNpc identity and scale remain
unchanged. Heads use 17836 and the invisible
hourglass/fragment anchors use 17837/17841. Cleansing puddles are actual EObj
`0x1EBD41`, backed by `sgvf_w_btl_b3566.sgb`. Tethers use Channeling 133/134.
The crystal is `sgvf_n4gw_b3557.sgb` (layout 10885836) at world (100, 0, 84),
scenario-local (0, 0, -16). Its invisible combat anchor and protection checks
use that same north position. Slot 41 is a separate center floor circle, not the
crystal. Checks cover correct activation, normal/reset cleanup, a harmful spell
at the crystal, and no false crystal hit from that spell at arena center.
Native encounter actors Vision of Ryne (17844) and Vision of Gaia (17845) are
placed inside the crystal, facing each other. The fragment is a friendly,
targetable actor outside the enemy list, with a native HP bar (1,000,000 training
HP, consistent with other simulated actors). A fatal crystal hit also sets its
HP to zero. Exact retail HP and a separate healing/damage model are not simulated.
Both visions and the HP actor belong to scenario cleanup.

Dragon heads are absent until their 14.3-second appearance cue, separately from
the tethers at 10 seconds. They face their travel direction and begin moving at
15.3 seconds, one second after the native appearance request. Their spawn
configuration requests ActionTimeline 4562 only after the native model is ready;
for model m0253 this resolves to `show/mon_sp002.tmb` and the game's
`m0253_show_sp02c0k1.avfx`. This is a native animation/VFX request, with no custom
fade or overlay. Initial movement/contact cooldown still runs from 15.3 to 17.3.
The pulse is requested only at appearance, with native finite emitter windows
(frames 0–30, 4–34 and 17–47). The game owns its particle fade-out; no persistent
status loop is attached during travel and no pulse is replayed on interception.
No extra crystal effect is requested. Checks cover the appearance request,
absence of persistent/replayed head VFX, reset and unchanged crystal HP/effects.
Asynchronous native loading, the exact burst's visual match and its fade-out
relative to the first movement still need live-client verification.
Checks verify opening absence, both spawn requests, health/targetability,
Ryne/Gaia placement, fatal crystal HP, and cleanup. Native appearance timing,
poses, HP rendering and appearance animation need live-client verification.
All casts, elemental hits, Tidal Light, Return, Spirit Taker, Wings and Akh Morn
dispatch native actions. Tidal Light's warning is its own Action/Omen 589
(`exa_rz_o1v`), not a custom drawn strip. No overlay renderer, replacement
omen, hand-tinted effect, or copied game asset was added.

The scenario includes randomized debuffs, three Quietus targets, both slow
hourglass configurations and all four exaline corners; reactive head contacts
and single-use puddles; delayed spells, fragment protection, cleanse expiry,
rewind snapshots, spread, stun/forced return, two ordered Wings knockbacks,
and four Akh Morn hits. The player input lock is explicitly released on normal
completion and on `SimPlayer.Despawn`; all native objects belong to the world.

`CrystallizeTimeChecks` executes 384 complete bot runs (eight patterns, 16 role
assignments/jump targets, three frame rates) plus 320 complete manual-player
runs (eight arena patterns × eight roles × five debuff choices including Random). Negative checks cover hourglasses,
underfilled stacks, fireworks, exalines, incorrect rewind leading/order,
overlapping Spirit Taker, expired uncleansed Fang, and an incomplete seven-person
Akh Morn stack. Reset is exercised during tethers, heads/puddles, rewind, stun,
and Wings; missing boss allocation is also covered. The player run uses a
separate rehearsal to supply intended user inputs and verifies production
never issues normal movement to the selected player.

These are managed tests with recording engine stand-ins, not live client tests.
Verify native stage switching, Usurper's composite model, hourglass state/fill
animations, head scaling/path, puddle visibility/consumption, Return traces,
cast/telegraph alignment, player input release on early reset, and the complete
7–1 finish in game. P4 plays native BGM 801, Promises to Keep
(`music/ex3/BGM_EX3_Raid_11.scd`), respecting the existing music suppression setting.

The first dragon interception now resizes the same native actor/model instead
of deleting and immediately reusing its object-table slot. Checks require only
two dragon actors for the full run. Heads explicitly spawn at scale 2, shrink to
scale 1 after the first pop, and play the native disappear action and hide after
the second. A staged check verifies each pop advances one head stage, drops one
puddle, and never recreates an actor. Local event objects
default to the non-networked entity sentinel `0xE0000000` instead of zero.
These changes address the suspect transition in the reported Boss Mod 7.5.6.5
`WorldStateGameSync.UpdateActor` crash. The user reports that this error is gone.

Each dragon head now has a two-second initial and retrigger cooldown on the scenario clock.
Initial protection runs from movement activation at 15.3 seconds until 17.3,
so early overlap near the north side cannot pop either head. The appearance lead-in does not consume any of this movement-start protection.
Heads continue moving while protected; overlap causes no hit, shrink, puddle,
or Wyrmclaw consumption. Initial protection is reapplied on restart and checked
on both heads at 15/30/60 fps, including just before expiry and after expiry.
The previous interceptor must leave its original two-yalm contact area before
they can trigger the same head again. This prevents a continuous overlap from
consuming both pops when the timer expires or the model shrinks. A different
interceptor can take the second pop after cooldown; a genuine later contact
without Wyrmclaw still fails. Both heads track cooldown/contact state independently,
and restart clears that state. Checks at 15/30/60 fps cover lingering overlap,
shrink boundaries, early re-entry, a different interceptor, invalid later soaks,
independent heads, and restarting the same scenario instance.

The native actor-bound clock (`vfx/common/eff/d1049_stlp_b0k1.avfx`) is requested
with the initial debuffs at 6.5 seconds and remains until the 39.6-second snapshot.
It is also removed on death/reset; it is not limited to the final five seconds.
At snapshot, each living member leaves a world-space Return marker
(`vfx/common/eff/d1049_returnmark_c0k1.avfx`, VFX row 1013). This replaces the
enlarged boss variant `m0640_returnmark_c0v` (row 2269), whose authored root scale
is 2 instead of 1. The user confirmed using the original native player marker,
including its authored small rewind arrows. It stays fixed
when they move away and is removed at
42.7 seconds, matching the reference's Wings transition. This uses the native
StaticVfx owner with unit scale; no overlay or replacement artwork is involved.
Checks cover early/late timing, living recipients, actual saved positions,
stationary traces during movement, and cleanup. Asset identity is inferred from
the installed AVFX's clock textures, bindings and Return naming; the exact
countdown appearance and floor rendering still require live client verification.

CT's Scenario config panel has a **Your debuffs** selector: Random, RED+ICE,
RED+AERO, BLUE+DARK, and the grouped BLUE+ICE / BLUE / YELLOW stack option.
The grouped option selects blue ice, water, or eruption randomly. Auto restores
Random. The choice applies to the actual player slot (including role overrides)
on the next Start and stays selected for subsequent runs in the current session.
The remaining slots are filled by bots, with the red pairs sorted by NA priority
after the player's category is chosen. Hourglass/exaline patterns, Quietus and
Spirit Taker targets stay randomized. The selected group is captured at Start;
changing settings mid-run affects the next run only.

Selection checks cover 2,560 seeded role/choice combinations, uniqueness of all
eight roles, NA side priority, all three grouped-blue outcomes, and unchanged
Random behavior. The public scenario Start path is checked for all 32 fixed
choice/player-role pairs, including settings changes and restart. The settings
renderer is omitted from the headless test build through an optional partial
method; it is compiled by the full plugin build and needs a live UI check.

The dragon-armored Usurper uses native teleport animations around Wings.
The windup (136/137) precedes action 40229 at 42.7 seconds; its 4.8-second cast
restores the cast bar and releases `hide/mon_sp010`. First-edge arrival at 48
uses show/loop (7780/7781), followed by swing (4568) at 49.3 and impact at 49.9.
The first-edge departure uses hide (4582) at 53.0, retaining the earlier recovery
delay; second arrival, swing and impact remain at 53.1, 53.9 and 54.5 respectively.
The Usurper's draw flag stays enabled so native opacity animations can play;
there is no abrupt `SetVisible(false)` at these transitions or in the opening.
Presentation checks cover enabled drawing at 15/30/60 FPS, the retained dragon
model, cast/teleport ordering, and direct native wing timelines before both frozen-party
knockbacks in all four corner patterns. They check damage is not applied during
the animation lead-in and stun ends after the second knockback's recovery,
before Akh Morn movement. The two native visions
request persistent native draw elevation above their grounded actors through rewind and are
removed on reset. These checks record native API requests; actual animation,
crystal framing and model height require in-game confirmation.

## Apocalypse (P3)

The **Futures Rewritten → P3 → Apocalypse** scenario uses NA water priority
(MT > OT > H1 > H2 and M1 > M2 > R1 > R2), static/Freepoc eruption spreads,
and an OT Darkest Dance bait. It requires a full party; player movement stays
manual. Each run randomizes two of each water duration (including no water),
four initial axes, and clockwise/counterclockwise rotation.

Run only these checks with `dotnet run --project test/FruPatternChecks -- --apocalypse`.
The separate `dotnet run --project test/MovementChecks` harness exercises the
production player movement code, including a gap-closer interrupting the end of
the knockback without the old slide pushing the player out again.
The normal command below runs them with the existing FRU checks.
`ApocalypseChecks` exhausts all 2,520 water assignments, then executes 384 full
bot runs across eight rotations, 16 assignments, and 15/30/60 FPS. Another 64
runs replay externally recorded player inputs across every role and rotation,
checking that production AI never moves the player. Checks cover all three
four-person stacks, explosion/spread clearance, arena bounds, tank targeting,
knockback landing positions, native requests, cleanup, missing actors, reset,
and deliberate positioning failures.

The reference is [FRU-Sim at 2a77c85](https://github.com/WCGH/FRU-Sim/tree/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p3):
`sequences/apoc_seq.gd`, `position_coords/apoc_pc_pos.gd`, `apoc_lights.gd`, and
`p3_apoc_main.tscn`. The animation tracks are authoritative when script comments
disagree: swaps at 16.9s, eruption at 35.7s, Apocalypse hits at 33.3/35.3/37.3/
39.3/41.3/43.3s, dance damage at 43.2s, and its visual jump at 44.2s. Water hits
remain 23.1/41.9/50.9s. The script samples the tank again for the later jump;
using the earlier damage position for both can put knockback landings outside
the arena. The OT leaves after the second water at 42.0s (0.2s earlier than the
reference track) to isolate the native eight-yalm splash at ordinary run speed.
At the same time, the other seven bots move to arena center so they clear the
baited tank buster. Checks assert their actual center arrival, splash clearance,
and OT-only damage at the Dance snapshot across all 384 bot runs.
Both groups preposition two yalms behind Oracle at +/-30 degrees, then take the
native 21-yalm knockback. Bots return at 48.4s, after the full 0.7-second slide,
to their adjusted water groups' respective sides of the boss: two yalms inward
and four yalms left/right. The groups remain eight yalms apart at their final
positions, allowing long water to resolve safely during their approach at 50.9s.
Checks verify the full knockback distance, uninterrupted slide, successful final
water stacks, and actual return positions across all 384 bot runs.
These sides follow the flexed water assignments, including bots that swap out
of their original support/DPS group. An additional 96 runs use explicit
short-, medium-, and long-water flex cases plus a two-pair swap case, checking
the actual four occupants of each of the three stacks and the final boss sides
against independently specified groups. Static eruption spreads still use the
original roles; stack movements retain the flex assignments.
Final-stack sides are defined while facing the boss from arena center:
supports on the left, DPS on the right, with flexed roles joining the other
group. Both the knockback setup and return use this orientation. The previous
signs mirrored these sides and could put a correctly flexing human into the
wrong bot group. A PLD/OT with long water now has 24 additional manual-player
checks across all rotations and 15/30/60 FPS: its final route independently
uses the right side while the non-flexing WAR stays left. This reproduced a
final-water wipe before the sign fix; earlier player checks replayed bot routes
and therefore could not detect the shared orientation error.

Native action dimensions replace the simulator's visual dimensions: Apocalypse
radius 9 on a radius-14 ring, water/eruption radius 6, Spirit Taker radius 5,
and Dance radius 8. Godot north (+X) maps to native north (-Z); route coordinates
scale by `20 / 47.4`. P3 uses Oracle BNpc 17831, BGM 802, and the existing
P3/P4 stage controller at map slot 40. Weather 106 and live floor presentation
still need visual confirmation.

Installed build `2026.09.15.0000.0000` supplies action 40297's native
`n4gw_boss_gimmick18` timeline and `n4gw_b_g18_c0v.avfx` hit effect. EObj
`0x1EB0FF` resolves through ExportedSG 25372 to `sgvf_n4gc_b2164.sgb` and
`b2164kido1_o.avfx`. Its authored timelines provide the light's straight path,
right/left 45-degree arcs, and warning pulses (zero-based AVFX triggers 1/2/3/4).
Static VFX use stationary sources and unit scale; the resource already supplies
14-yalm movement over 60 authored frames. There are no custom-drawn warning
circles. Effect handles are owned by the world and expire through scenario events.
The opening water stack marker uses Lockon 62 (`com_share0c`) at 7.2s,
before debuff application at 12.8s. The countdown uses Lockon 184
(`m0581trg_dice0h`) at 18.0/36.8/45.8s, 5.1 seconds before each water hit,
only on that duration's two recipients. These two resources were previously
assigned in reverse. Checks assert the literal native paths, opening recipients,
and each countdown window. Eruption uses Lockon 139 (`target_ae_s5f`).

At debuff application (12.8s), each water recipient also gets the native
overhead waiting clock `vfx/common/eff/d1049_stlp_b0k1.avfx`. It stays until
that recipient's countdown starts, then is removed before the finite countdown
is requested. The installed resource has an indefinite default timeline,
emitter/particle lifetimes and actor binder, so this waiting clock is owned
persistently and cleaned up on death, debuff loss, finish or world reset.
Checks cover all three handoffs, no-water exclusions, one spawn per recipient,
manual-player recipients and cleanup. Exact native appearance needs a live check.

The three opening/countdown/eruption lockons are finite native animations and use `persistent: false`.
The game owns their lifetimes, including while the simulation is paused; neither
mechanic resolution nor Reset manually destroys their handles. This avoids
destroying a handle the game may already have freed after a death/pause. Six
additional pause/reset checkpoints assert this ownership contract and repeated
cleanup. These are managed checks, not a reproduction of native heap corruption.
An early reset can leave a player lockon visible until its native animation ends.

Managed checks do not execute the native renderer, packets, or player input.
Verify light direction, pulse size/timing, cast/jump animation, floor/weather,
status clocks, player knockback, and reset in-game. As with existing scenarios,
native animations run in real time while the event-speed option scales scheduled
events; the validated choreography is at normal speed.
