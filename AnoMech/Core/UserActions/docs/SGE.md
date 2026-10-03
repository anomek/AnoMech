# Sage (SGE)

_✅ works · ⚠️ partial · ❌ not simulated · ➖ nothing to simulate (pure damage/heal/mitigation, or a native game button-swap)._

## Actions
| Action | ID | Status | Note |
|--------|----|:------:|------|
| Dosis | 24283 | ➖ | |
| Diagnosis | 24284 | ➖ | |
| Kardia | 24285 | ➖ | Self-buff icon, no gate. |
| Prognosis | 24286 | ➖ | |
| Egeiro | 24287 | ➖ | Raise. |
| Physis | 24288 | ➖ | |
| Phlegma | 24289 | ➖ | |
| Eukrasia | 24290 | ✅ | Grants Eukrasia status (2606) and sets the gauge byte (`SgeEukrasia` in `JobActions`); every Eukrasian spell clears both. |
| Eukrasian Diagnosis | 24291 | ✅ | Consumes Eukrasia (in clear list). |
| Eukrasian Prognosis | 24292 | ✅ | Consumes Eukrasia (in clear list); at cap resolves to 37034. |
| Eukrasian Dosis | 24293 | ✅ | Consumes Eukrasia (in clear list) — the level-synced version, e.g. UWU at 70; at cap resolves to 24314. |
| Soteria | 24294 | ➖ | |
| Icarus | 24295 | ➖ | |
| Druochole | 24296 | ✅ | −1 Addersgall. |
| Dyskrasia | 24297 | ➖ | |
| Kerachole | 24298 | ✅ | −1 Addersgall. |
| Ixochole | 24299 | ✅ | −1 Addersgall. |
| Zoe | 24300 | ➖ | |
| Pepsis | 24301 | ➖ | |
| Physis II | 24302 | ➖ | |
| Taurochole | 24303 | ✅ | −1 Addersgall. |
| Toxikon | 24304 | ⚠️ | −1 Addersting wired, but Addersting never generated in-sim → unusable. |
| Haima | 24305 | ➖ | |
| Dosis II | 24306 | ➖ | |
| Phlegma II | 24307 | ➖ | |
| Eukrasian Dosis II | 24308 | ✅ | Consumes Eukrasia (in clear list); at cap resolves to 24314. |
| Rhizomata | 24309 | ✅ | +1 Addersgall. |
| Holos | 24310 | ➖ | |
| Panhaima | 24311 | ➖ | |
| Dosis III | 24312 | ➖ | |
| Phlegma III | 24313 | ➖ | |
| Eukrasian Dosis III | 24314 | ✅ | Consumes Eukrasia (in clear list). |
| Dyskrasia II | 24315 | ➖ | |
| Toxikon II | 24316 | ⚠️ | Same as Toxikon — Addersting never generated. |
| Krasis | 24317 | ➖ | |
| Pneuma | 24318 | ➖ | |
| Eukrasian Dyskrasia | 37032 | ✅ | Consumes Eukrasia (in clear list). |
| Psyche | 37033 | ➖ | |
| Eukrasian Prognosis II | 37034 | ✅ | Consumes Eukrasia (in clear list). |
| Philosophia | 37035 | ✅ | Grants Philosophia (3898) + Eudaimonia (3899), 20s. |

## Traits
| Trait | Status | Note |
|-------|:------:|------|
| Addersting | ⚠️ | Gain-on-barrier-absorb (Max 3) — the source of the un-generated Addersting gauge; not simulated. |

## Role actions
Repose, Esuna, Lucid Dreaming, Surecast, Rescue — ➖. Swiftcast ✅ (167 → next spell instant).

## Not simulated
- **Toxikon / Toxikon II (24304 / 24316)** — Addersting spend is wired (`CostGauges[69]`) but the gauge is never generated in-sim; its only source is a fully-absorbed barrier, and the sim has no incoming damage, so it stays at 0 and both actions are permanently unusable.
