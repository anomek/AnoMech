# Red Mage (RDM)

_✅ works · ⚠️ partial · ❌ not simulated · ➖ nothing to simulate (pure damage/heal/mitigation, or a native game button-swap)._

## Actions
| Action | ID | Status | Note |
|--------|----|:------:|------|
| Riposte | 7504 | ➖ | Enchanted swap is native off the White/Black gauge. |
| Jolt | 7503 | ✅ | Via Jolt III (37004) at 100. |
| Verthunder | 7505 | ⚠️ | Grants 50% Verfire Ready but omits +6 B mana; moot at 100 (→ Verthunder III). |
| Corps-a-corps | 7506 | ➖ | |
| Veraero | 7507 | ⚠️ | Grants 50% Verstone Ready but omits +6 W mana; moot at 100 (→ Veraero III). |
| Scatter | 7509 | ✅ | +3 W/+3 B via Impact (16526). |
| Verthunder II | 16524 | ✅ | +7 B. |
| Veraero II | 16525 | ✅ | +7 W. |
| Verfire | 7510 | ✅ | +5 B; consumes Verfire Ready. |
| Verstone | 7511 | ✅ | +5 W; consumes Verstone Ready. |
| Zwerchhau | 7512 | ➖ | Linear combo; Enchanted swap native off gauge. |
| Displacement | 7515 | ➖ | |
| Engagement | 16527 | ➖ | |
| Fleche | 7517 | ➖ | |
| Redoublement | 7516 | ➖ | Linear combo; Enchanted swap native off gauge. |
| Acceleration | 7518 | ✅ | Grants Acceleration + Grand Impact Ready. |
| Moulinet | 7513 | ➖ | Enchanted swap native off gauge. |
| Vercure | 7514 | ➖ | Heal. |
| Contre Sixte | 7519 | ➖ | |
| Embolden | 7520 | ✅ | Grants Embolden + Thorned Flourish. |
| Manafication | 7521 | ✅ | Grants Magicked Swordplay (3) + Prefulgence Ready + range buff. |
| Jolt II | 7524 | ✅ | Via Jolt III. |
| Verraise | 7523 | ➖ | Raise. |
| Impact | 16526 | ✅ | +3 W/+3 B. |
| Verflare | 7525 | ✅ | +11 B, −3 Mana Stacks, 20% Verfire Ready. |
| Verholy | 7526 | ✅ | +11 W, −3 Mana Stacks, 20% Verstone Ready. |
| Reprise | 16529 | ➖ | Enchanted swap native off gauge. |
| Scorch | 16530 | ✅ | +4 W/+4 B; chained after Verflare/Verholy. |
| Verthunder III | 25855 | ✅ | +6 B, 50% Verfire Ready. |
| Veraero III | 25856 | ✅ | +6 W, 50% Verstone Ready. |
| Jolt III | 37004 | ✅ | +2 W/+2 B. |
| Magick Barrier | 25857 | ➖ | Mitigation. |
| Resolution | 25858 | ✅ | +4 W/+4 B; chained after Scorch. |
| Vice of Thorns | 37005 | ✅ | Consumes Thorned Flourish. |
| Grand Impact | 37006 | ✅ | +3 W/+3 B; consumes Grand Impact Ready. |
| Prefulgence | 37007 | ✅ | Consumes Prefulgence Ready. |
| Enchanted Riposte | 7527 | ✅ | −20 W/−20 B via cost path; +1 Mana Stack. |
| Enchanted Zwerchhau | 7528 | ✅ | −15 W/−15 B; +1 Mana Stack. |
| Enchanted Redoublement | 7529 | ✅ | −15 W/−15 B; +1 Mana Stack. |
| Enchanted Riposte/Zwerchhau/Redoublement (Manafication) | 45960-45962 | ✅ | +1 Mana Stack; combo as their base action. |
| Enchanted Moulinet | 7530 | ✅ | −20 W/−20 B; +1 Mana Stack. |
| Enchanted Moulinet Deux | 37002 | ✅ | Mana spend + 1 Mana Stack; decrements Magicked Swordplay. |
| Enchanted Moulinet Trois | 37003 | ✅ | Mana spend + 1 Mana Stack; decrements Magicked Swordplay. |
| Enchanted Reprise | 16528 | ✅ | −5 W/−5 B; grants no stack (correct). |

## Traits
| Trait | Status | Note |
|-------|:------:|------|
| Dualcast | ✅ | Buff granted on cast completion; consumed by any non-ability action (Acceleration-reduced casts excepted). The next spell is made instant by answering the client's cast-time query. |

## Role actions
Addle, Sleep, Lucid Dreaming, Surecast — ➖. Swiftcast ✅ (167 → next spell instant; priority Acceleration → Dualcast → Swiftcast).

## Not simulated
- **Unbalanced mana** — White/Black clamp independently; the |White − Black| > 30 penalty (lower colour generates at half rate) and crystal recolour are unmodelled (`// TODO`).
- **Magicked Swordplay free-cast** — under Magicked Swordplay enchanted melee should cost a stack instead of mana; the generic cost path always subtracts White/Black mana, so enchanted melee never becomes free.
- **Conditional proc guarantees** — procs roll their flat chance (Verthunder III/Veraero III/Impact 50%, Verflare/Verholy 20%) even where the game guarantees 100%: while Acceleration is up, and (for Verflare/Verholy) when the opposite mana colour is higher.
- **Base Verthunder/Veraero (7505/7507)** — grant only the proc, not their +6 mana (moot at 100; the III forms are correct).
