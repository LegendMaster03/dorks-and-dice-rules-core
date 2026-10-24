# Warlock Band 1 — 3.x source review (pre-reconciliation)

**Status:** 2026-10-24, historical source researched; **no Warlock class-feature decisions approved or applied**. The next Band 1 base class to reconcile is Warlock.

## 3.x is a required source even though it is outside the SRD

The original 3.5e Warlock appeared in Wizards of the Coast's **Complete Arcane (2004), class beginning on page 5**. It is a supplementary, non-core-SRD class. Excluding it merely because it was not staged in the pinned SRD 5.1/5.2.1 pack would eliminate important baseline material. There is no established corresponding 3.0 Warlock base class; do not invent a 3.0 analogue.

**Authorized publication reference:** [Complete Arcane (3.5), DMsGuild](https://www.dmsguild.com/en/product/3733/complete-arcane-3-5). For research verification only, third-party mechanical indexes consulted: [Warlock class](https://srd.dndtools.org/srd/classes/baseCarc/warlock.html), [Warlock invocations](https://srd.dndtools.org/srd/magic/invocations/invocations.html). A third-party site labeling a page “SRD” does not make the Complete Arcane class part of the official core SRD.

The accompanying `data/reference/3x/warlock-complete-arcane-research.json` records historical mechanics and provenance. It does **not** import or redistribute the copyrighted class prose or invocation descriptions. Verify wording against an authorized copy before final legal/public release.

## Why the earlier SRD-only assessment was incomplete

| Subject | 3.5e Complete Arcane | 2014 SRD | 2024 SRD | Reconciliation problem |
|---|---|---|---|---|
| Chassis | d6, three-quarter BAB, good Will; light armor | d8, Pact Magic | d8, Pact Magic | Preserve historical attack/saving projections without replacing modern chassis blindly |
| Invocation design | Mostly at-will spell-like powers; least, lesser, greater, dark grades | Invocation choices start at Warlock level 2 | Invocation choices start at level 1 | Decide one catalog/selection progression versus distinct historical tracks |
| Invocation capacity | 12 known at class level 20 | 8 known at class level 20 | 10 known at class level 20 | Counts are alternatives, not automatically additive |
| Eldritch Blast | At-will ranged-touch ray; 1d6 initially, 9d6 by 20; shape/essence upgrades | Cantrip with later level scaling and invocation upgrades | Cantrip with later level scaling and invocation upgrades | Preserve useful blast controls without stacking incompatible damage and attack-count formulas |
| Intrinsic defenses | Cold-iron-qualified damage reduction, limited fast healing, typed energy resistance | No direct class-wide equivalents | No direct class-wide equivalents | Owner decision: additive adapted historical features versus alternative treatment |
| Item interactions | At-will magic detection, Deceive Item, Imbue Item | Modern invocation choices | Modern invocation choices | Reuse shared competencies and crafting rather than inventing a second item subsystem |
| Patron/subclass | Patron relationship in fiction; no modern subclass choice track | Patron class features from level 1 | Subclass milestones from level 3 | Keep historical class chassis and modern subclass passes distinct |
| Casting resources | Original base Warlock did not use ordinary spells or spell slots | Short-Rest Pact Magic slots and Mystic Arcanum | Updated Pact Magic, Mystic Arcanum, Magical Cunning | Distinct at-will effects must not become free ordinary leveled Pact spells |

## Historical mechanical landmarks

The 3.5e invocation grades become available at **levels 1, 6, 11 and 16** (least, lesser, greater, dark). Its known invocations reach **12 by level 20**, compared with eight in 2014 and ten in 2024. These differences are not resolved by summing the lists.

Historical Eldritch Blast shapes alter delivery; essences alter damage type or add effects. In that system a blast could use **at most one shape and one essence**. The modern blast's multiple beams and damage scaling are mechanically different; both advancement formulas must not be combined automatically.

Historical standalone class features worth retaining for comparison are **Detect Magic at 2**, **Damage Reduction starting at 3** (improves 7/11/15/19), **Deceive Item at 4**, **Fiendish Resilience starting at 8** (improves 13/18), **Energy Resistance starting at 10** (improves 20), and **Imbue Item at 12**. They need source-faithful reconciliation through the native combat, recovery, competency, and crafting subsystems rather than wholesale addition of parallel rules.

The 3.5e original also restricted alignment. This is historical evidence; it is not introduced as a native class prerequisite without approval.

*Complete Mage* (2006) adds further Warlock invocations and related prestige classes. Record it for the later invocation-option and prestige-class passes. It is not part of the class-base source entry above.

## Proposed design sequence (questions, not decisions)

1. **Core resource architecture:** Retain modern Pact Magic as its distinct slot progression; determine whether the historical at-will invocation functions live inside the same shared Eldritch Invocation catalog as modern options, rather than a second automatic pool.
2. **Eldritch Blast:** Select a single native attack/damage progression; reconcile optional historical blast shapes and essences as selectable invocation capabilities, preserving their one-each historical limits where appropriate.
3. **Historical independent features:** Examine Damage Reduction, Fiendish Resilience, Energy Resistance, Detect Magic, Deceive Item and Imbue Item for additive class-level grants, with exact check/crafting/recovery conversions delegated to shared systems.
4. **Known invocation progression:** Choose the level-by-level count and grade/level prerequisites; do not sum the 3.5, 2014 and 2024 counts.
5. **Subclass boundaries:** Preserve modern Warlock subclass milestones without prematurely importing patron features into the base class.
6. **Only after approval:** Reconcile levels 1–20, update the native coverage manifest for the existing 5.x candidates, add tests and statistics. The new 3.x research reference is *outside* the pinned 5.x SRD coverage manifest.

**Guardrails:** Preserve source meaning, avoid invented costs, do not turn spell-like invocations into unlimited ordinary Pact spellcasting, and do not copy unlicensed text into the native rule data.
