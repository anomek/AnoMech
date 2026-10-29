# Bard (BRD)

_✅ works · ⚠️ partial · ❌ not simulated · ➖ nothing to simulate (pure damage/heal/mitigation, or a native game button-swap)._

## Actions
| Action | ID | Status | Note |
|--------|----|:------:|------|
| Heavy Shot | 97 | ➖ | Upgraded to Burst Shot. |
| Straight Shot | 98 | ➖ | Upgraded to Refulgent Arrow. |
| Raging Strikes | 101 | ✅ | |
| Venomous Bite | 100 | ✅ | DoT (124, 45s) on the target; upgraded to Caustic Bite. |
| Bloodletter | 110 | ✅ | A Mage's Ballad Repertoire runs its recast on by a charge (BardStateHandler). |
| Repelling Shot | 112 | ➖ | |
| Quick Nock | 106 | ➖ | Upgraded to Ladonsbite. |
| Wide Volley | 36974 | ✅ | Consumes Hawk's Eye/Barrage. |
| Windbite | 113 | ✅ | DoT (129, 45s) on the target; upgraded to Stormbite. |
| Mage's Ballad | 114 | ✅ | Song Gauge (BardStateHandler); every 3s with DoTs up, 80% to refund a Bloodletter charge. |
| The Warden's Paean | 3561 | ➖ | |
| Barrage | 107 | ✅ | Barrage + Resonant Arrow Ready. |
| Army's Paeon | 116 | ✅ | Song Gauge; every 3s with DoTs up, 80% for a Repertoire (max 4). |
| Rain of Death | 117 | ➖ | |
| Battle Voice | 118 | ✅ | |
| The Wanderer's Minuet | 3559 | ✅ | Song Gauge; every 3s with DoTs up, 80% for a Repertoire (max 3, gates Pitch Perfect). |
| Pitch Perfect | 7404 | ✅ | Spends all Repertoire. |
| Empyreal Arrow | 3558 | ✅ | A Repertoire for the song playing. |
| Iron Jaws | 3560 | ✅ | 35% Hawk's Eye; renews the DoTs already on its target. |
| Sidewinder | 3562 | ➖ | |
| Troubadour | 7405 | ➖ | |
| Caustic Bite | 7406 | ✅ | DoT (1200, 45s) on the target; 35% Hawk's Eye. DoTs deal no damage in the sim. |
| Stormbite | 7407 | ✅ | DoT (1201, 45s) on the target; 35% Hawk's Eye. DoTs deal no damage in the sim. |
| Nature's Minne | 7408 | ➖ | |
| Refulgent Arrow | 7409 | ✅ | Consumes Hawk's Eye/Barrage. |
| Shadowbite | 16494 | ✅ | Consumes Hawk's Eye/Barrage. |
| Burst Shot | 16495 | ✅ | 35% Hawk's Eye. |
| Apex Arrow | 16496 | ⚠️ | Grants Blast Arrow Ready unconditionally; SV≥80 gate + 20 Soul Voice spend inert (Soul Voice never fills). |
| Ladonsbite | 25783 | ✅ | 35% Hawk's Eye. |
| Blast Arrow | 25784 | ✅ | Consumes Blast Arrow Ready. |
| Radiant Finale | 25785 | ✅ | Radiant Finale + Radiant Encore Ready; coda gate out of scope. |
| Heartbreak Shot | 36975 | ➖ | |
| Resonant Arrow | 36976 | ✅ | Consumes Resonant Arrow Ready. |
| Radiant Encore | 36977 | ✅ | Consumes Radiant Encore Ready. |

## Traits
| Trait | Status | Note |
|-------|:------:|------|
| Enhanced Empyreal Arrow | ✅ | Empyreal Arrow gives the song's Repertoire. |
| Bite Mastery II | ✅ | Adds 35% Hawk's Eye to Caustic/Stormbite/Iron Jaws. |
| Soul Voice | ✅ | +5 Soul Voice per Repertoire from level 80 (UNVERIFIED in game). |
| Minstrel's Coda | ❌ | Grants the three Codas on singing — coda flags unmodelled (procs still fire; potency/display only). |

## Role actions
Leg Graze, Second Wind, Foot Graze, Peloton, Head Graze, Arm's Length — all ➖.

## Not simulated
- **Apex Arrow spends 20 Soul Voice, not all of it**, and grants Blast Arrow Ready whatever the Soul Voice (retail: 80+).
- **Codas** (Radiant Finale's gate) are not written.
