# Pillar 2 — Biological Origination provisional pricing

**Status:** Versioned first-party trial prices implemented on the Pillar 2 integration branch, November 23, 2026. Not a final Species/Ancestry balance approval.

## Policy and execution boundary

The authoritative host-side policy for this increment is `NativeOriginFirstPartyBiologicalPricing.Create()` in `src/RulesCore.Mechanics/NativeOriginFirstPartyBiologicalPricing.cs`, revision `biological-pricing-20261123-v2`. The optional `allowance` argument controls the table's initial creation budget. Use the existing `NativeOriginConstructionRequest` creation gate; the policy is not an untrusted proposal property and does not modify reconstruction or post-creation advancement. Existing DM/table authenticated waiver or allowance change remains in force.

This increment prices **all 83 active canonical native biological genes** (including five whose descriptions do not yet pass the mechanical-shape authorization gate), plus all five applicable intrinsic slots. An additional, newly authored gene is not assigned a default cheap price. All current biological source and expression requirements are unchanged: at most two inherited and one expressed instance per canonical gene kind. Interaction and multiplicity premiums remain unused.

## Trial point rules

- Creature Type and Size: **0** each.
- Ordinary authored walking speeds: **0**, including present 35-foot exceptions; the trial policy permits 20, 25, 30, 35, 40, or 45 feet and fails closed for unpriced exceptional intrinsic speeds. Baselines by size: Tiny 20, Small 25, Medium 30, Large 35, Huge 40, Gargantuan 45. A size-changing ability retains only its explicitly granted speed increase; it does not obtain a second increase from this baseline table.
- Intrinsic innate Darkvision: 60 feet **3**, 120 feet **4** including derived Sunlight Sensitivity. The threshold remains a configurable 60 feet in `rule.creature-origination`. The explicit linked Sunlight Sensitivity trait costs **0**; there is no bonus or refund for it.
- Each independently effective standalone resistance: 25% **2**, 50% **3**, 75% **4**, immunity (100%) **5**, regardless of damage type. The consolidated typed trait and the standalone Goliath Fire/Cold traits are configuration-priced. Composite Dwarf Poison Resilience retains its own complete 4-point cost; temporary abilities are not treated as permanent immunity. Authored inheritance and overlap remain authoritative.
- Innate spells: cantrip **1**, 1st-level **2**, through 9th-level **10**. Spell level is distinct from the character-level unlock field. Flexible Wizard cantrip replacement adds **1**, and PB-based free Speak with Animals uses add **1**. Hellish Rebuke uses its authored free second-level casting. Source-bound spellcasting Ability is **1**, independent of class spellcasting.
- Significant reviewed full-trait prices: Human Skillful **1**, Adaptable Learning **6**, Versatile **10**; Breath Weapon **3**, Hill's Tumble **5**, Spectral Flight **3**, Cloud's Jaunt **3**, and Cloudy Escape **4**. **Ordinary Dragonborn Draconic Natural Weapons (1)** and **Draconic Wings (5)** were approved on November 22 and are now canonical genes inherited from `species.dragonborn`. Wings permit level-1 gliding and level-5 limited non-hovering flight, not continuous flight. Other listed traits retain the WorkChat fine-scale estimates unless superseded by these general rules.

A price covers the complete ability, including delayed unlocks and shared resources. Unexpressed genes, redundant donor offers, derived conditions, and already consolidated identical grants are not extra charges. A price does not make an unsupported shape or invalid coexistence lawful.

## Recalculated conventional Species and Ancestry totals

The following incorporates the November 22 approved ordinary Dragonborn Natural Weapons and Wings (+6 per Dragonborn descendant), alongside the WorkChat report's 43 reconstructed catalog rows and revised zero-point foundation. The 28 selectable terminal rows are listed. These are **nominal full-preset values**, not a claim that the live creation gate currently authorizes every shape or every required choice.

| Conventional terminal source | Revised points | Existing limitation |
| --- | ---: | --- |
| `ancestry.elf.drow` | 18 | — |
| `species.human` | 17 | — |
| `ancestry.elf.high` | 15 | — |
| `ancestry.elf.wood` | 14 | — |
| `ancestry.goliath.cloud` | 14 | — |
| `ancestry.tiefling.infernal` | 14 | — |
| `ancestry.goliath.stone` | 13 | — |
| `ancestry.tiefling.abyssal` | 13 | — |
| `ancestry.tiefling.chthonic` | 13 | — |
| `ancestry.dragonborn.white` | 18 | — |
| `ancestry.dwarf.hill` | 12 | Existing trait-shape gate block |
| `ancestry.goliath.fire` | 12 | — |
| `ancestry.goliath.frost` | 12 | — |
| `ancestry.goliath.hill` | 12 | — |
| `ancestry.dragonborn.black` | 17 | — |
| `ancestry.dragonborn.brass` | 17 | — |
| `ancestry.dragonborn.bronze` | 17 | — |
| `ancestry.dragonborn.copper` | 17 | — |
| `ancestry.dragonborn.gold` | 17 | — |
| `ancestry.dragonborn.green` | 17 | — |
| `ancestry.dragonborn.red` | 17 | — |
| `ancestry.goliath.storm` | 11 | — |
| `species.orc` | 11 | — |
| `ancestry.dragonborn.blue` | 16 | — |
| `ancestry.dragonborn.silver` | 16 | — |
| `ancestry.gnome.forest` | 10 | — |
| `ancestry.halfling.lightfoot` | 9 | Existing trait-shape gate block |
| `ancestry.gnome.rock` | 8 | — |

**Trial allowance: 18 points.** This is a recalculated choice rather than automatic adoption of the WorkChat 18-point proposal. Under the corrected schedule all 28 selectable terminal nominal packages fit: Drow and White Dragonborn are 18, Human and seven ordinary Dragonborn are 17, and none exceed the allowance. Other available direct Species parents not counted as terminal leaves also remain within 18. There is no reason in Task 2 to rewrite any Species traits solely for point parity. A larger allowance remains a configurable table choice.

## Known limitations and Task 3 handoff

The five native description-only genes `trait.brave`, `trait.larger-creature-passage`, `trait.luck-reroll`, `trait.naturally-stealthy`, and `trait.stability` have provisional prices but do not yet pass `NativeOriginBiologicalTraitShape`. They remain unresolved under a required allocation gate. Existing unknown mechanics, incomplete bindings, unauthorized contributors, and missing costs must still fail closed.

Task 3 should compare the corrected full preset values (28 terminal range **8–18**, mean **14.18**, median **14**), the verified human skill/feat entitlements, Drow's long-range benefit and practical sunlight protection, spell entitlements, Goliath late-level progressions, typed resistance exposure, and mixed-specialist opportunity costs. Do not silently adjust Species content or inferred interaction premiums during Task 2. The 18-point allowance is an initial playtest input, not a final balance verdict.

### November 23 ordinary Dragonborn correction

The November 23 WorkChat diagnostic was pinned to a pre-completion commit and incorrectly treated ordinary Natural Weapons and Wings as unapproved discussion estimates. The owner had explicitly approved the **ordinary** (not exceptional) mechanics on November 22. This corrected policy adds two independent canonical genes and makes them inherited Species grants for all ten selectable ordinary Dragonborn Ancestries. The base Species' former 8-point subtotal becomes **14 points**; Chromatic/Metallic family subtotals become **15** before their individual leaves. Gem Spectral Flight remains a separate, deferred family ability; exceptional Platinum/Fivefold Wings still have their own advanced continuous flight schedules.

All ten ordinary terminal Dragonborn now cost **16–18**, not 10–12. The 28-terminal ledger mean is **397 / 28 = 14.18**; the numerical range stays **8–18**. This correction changes the inventory, not the playtest evidence used to evaluate overall power. The diagnostic's relative scenario findings remain provisional and no additional Species changes are approved. The corrected source and independent balance note are in `pillar2-species-ancestry-balance-diagnostic-correction-20261123.md`.

**Source:** `pillar2-characteristic-pricing-evaluation-20261121.md` (WorkChat research at commit `28d25b51da133d3f1610d0c2923dc28630f5df5f`), amended by the decisions in the Biological Origination Pricing Implementation Handoff.
