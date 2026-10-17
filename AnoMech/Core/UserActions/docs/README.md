# UserActions coverage — overview

How completely the `UserActions` module resolves each job's **client-observable, non-damage**
behaviour (job gauges, status grants/procs, combo advancement, button swaps, cast interrupts).
Damage, healing, and mitigation numbers are out of scope by design.

Two layers resolve a player's own actions client-side:
- **Data table** (`JobActions.cs`) — per-action scalar gauge writes, status grants, linear-combo
  gates, proc chances, status clears, gauge spends (`CostGauges`, keyed on `(job, costType)`), and
  passive/decay gauges (`TimedGauges`).
- **Per-job state handlers** (`Jobs/<Job>StateHandler.cs`, one `IUserActionHandler` each) — the
  non-scalar gauge state a scalar can't hold: bitfields, packed bytes, enums, arrays. Present for
  SAM (Sen/Kaeshi), BLM (elemental gauge), SMN (trance/attunement), AST (cards), MNK (beast
  chakra/nadi), PCT (canvas/motifs), DNC (dance steps), VPR (serpent/dread combos).

One doc per job in this folder lists every action with a ✅/⚠️/❌/➖ mark and a short list of what
doesn't work. Marks: ✅ works · ⚠️ partial · ❌ not simulated · ➖ nothing to simulate.

## Per-job state

| Job | Remaining gaps |
|-----|----------------|
| DRG | none (fully simulated) |
| GNB | Bloodfest temporary cartridge cap→6 not modelled (cosmetic) |
| BLM | Paradox's Firestarter grant |
| DNC | Improvisation → Rising Rhythm status |
| PCT | Rainbow Bright never granted |
| SMN | Arcanum (Ifrit/Titan/Garuda-Ready) summon-select bits |
| VPR | Twinfang/Twinblood reciprocal venom grants; Ready-to-Reawaken over-charges SO |
| RDM | unbalanced-mana coupling; Magicked Swordplay free-cast |
| PLD | Holy Spirit/Circle double-consume Divine Might + a Requiescat stack |
| SGE | Addersting never generated (Toxikon unusable); Eukrasia gauge byte unwritten |
| WHM | none (fully simulated) |
| SAM | Meditate's channeled Meditation/Kenki build; Hagakure adds no Kenki |
| WAR | Lv96 Burgeoning Fury → Wrathful → Primal Wrath chain; Defiance icon |
| MCH | Flamethrower channel; Hypercharge over-drains Heat; Full Metal Field clears Reassembled |
| DRK | Blood Weapon never generates Blood; Dark Arts is a status only, not the gauge flag |
| MNK | Fury gauge unwritten; blitz-window encoding unverified |
| BRD | Repertoire/Soul Voice/Song-Gauge economy unmodelled |
| RPR | Shadow/Whorl of Death KO-gated Soul over-generates; Plentiful Harvest unreachable solo (party mechanic) |
| SCH | Emergency Tactics/Protraction/Expedient/Dissipation self-buffs; Faerie Gauge unmodelled |
| AST | card set per draw unverified; Synastry/Collective Unconscious/Horoscope self-buff icons |
| NIN | Mudra → Ninjutsu not simulable (no writable field); Raiton over-grants Raiju Ready; Huton → Shadow Walker |

## Recurring open gaps

- **Spender gauges with no in-sim generation** — SGE Addersting, BRD Soul Voice/Repertoire. These
  can't be generated in a solo sim at all (SGE Addersting = barrier absorption; RPR Plentiful Harvest
  needs Immortal Sacrifice = party mechanic), so their spenders sit at 0. RPR Void/Lemure Shroud, MNK
  Chakra, SAM Meditation, and VPR Anguine Tribute are now generated/spent in-sim.
- **Cast-time enablers** — Swiftcast (all casters/healers), BLM Triplecast, and RDM Acceleration are
  granted and consumed by `CastTimeHandler`; cast time is client-side, so the granted status makes the
  next cast instant. One enabler is consumed per spell in priority order Acceleration → Dualcast →
  Swiftcast. **RDM Dualcast** is the exception: its buff is granted (on cast completion) and consumed,
  but the client ignores a granted Dualcast status for cast time, so the next cast isn't actually made
  instant. Duration cast-time reductions (Ley Lines, Lightspeed, Presence of Mind, PCT Inspiration)
  are granted in the data table and applied by the client.
  (SCH Seraphism, WHM Divine Caress, and AST Neutral Sect / Earthly Star / Macrocosmos are now wired.)
- **NIN mudra is not simulable** — the Ten/Chi/Jin → Ninjutsu selector is computed by native
  `ProcessDeferredReplaceAction` from an internal ordered-sequence value FFXIVClientStructs never
  exposes; no writable field exists.
- **Handler gauge encodings unverified in-game** — the state handlers write documented CS fields, but
  the exact packed-bit/enum constants the game's `GetAdjustedActionId` reads can't be confirmed without
  running the client (SMN attunement type codes, AST card sets, BLM stance transitions/Paradox arm,
  MNK blitz window, VPR `SerpentComboState` layout, PCT creature-cycle, DNC step-array HUD write).
