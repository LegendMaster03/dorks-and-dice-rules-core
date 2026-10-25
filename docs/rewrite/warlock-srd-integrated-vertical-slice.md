# Warlock: integrated SRD-only vertical slice

**Status:** owner-directed vertical-slice scope (2026-10-24). This document fixes **content boundaries and evaluation requirements**; it does **not** automatically approve unresolved mechanical rulings or claim an implemented/playable class.

**Parent architecture:** `docs/rewrite/subclass-deconstruction-guided-complexity-policy.md`. Warlock is the first full application of that policy. This work deliberately overrides the older **base-class-only sequencing for Warlock**: its base class, Patron subclass, Pact Boons, and Eldritch Invocations must be designed, reconciled, implemented and tested **together**. Other classes remain subject to their established workflow until separately approved.

## Warlock development model — three pillars (owner-approved, 2026-10-24)

The Warlock has **three class-specific pillars**, which **together form its progression**:

1. **Patron** — the Warlock's source of supernatural power and its associated abilities. Patron identity begins at Warlock level 1; the exact timing of mechanical grants remains subject to SRD 5.1/5.2.1 reconciliation.
2. **Pacts** — Blade, Chain, and Tome specializations and their associated improvements. Initial choice and advancement entitlements remain to be decided.
3. **Eldritch Invocations** — individually selectable supernatural capabilities and enhancements, including options with Patron or Pact prerequisites. Preserve early level-1 access.

**Progression is not a fourth pillar.** It is the result of Warlock-level grants, selection rules, prerequisites, and dependencies operating across all three pillars and ordinary base-class features. Guided **subclass presets** are valid, lower-complexity acquisition paths using the *same* abilities and entitlements as custom choices; neither progression nor presets independently grant extra power.

The labels are intentionally **Warlock-specific**, not generic names every class must adopt. Review and implement **one pillar at a time, beginning with Patron**; record interactions with future pillars and test them together in the complete SRD-only Warlock slice. No previously unresolved numeric budgets, costs, or subclass feature timings are approved by this naming/structure decision.

## Patron identity, classification, and dependencies — owner clarification (2026-10-24)

- **Patron-derived power is mandatory for Warlock identity.** It may come through a direct bargain, an ancestral agreement, inherited influence, or involuntary designation without the Warlock's personal contract, ongoing contact, consent, or knowledge of an individual name. The level-1 source can be recorded as a Patron type or unrevealed individual; no named NPC record is required. A character with **no Patron-derived source** whose magic is innate belongs to **Sorcerer**, not a Patronless Warlock.
- **Cross-pillar prerequisites are permitted per option:** a Pact may require an eligible Patron source/feature, an Invocation may require a Pact or Patron part, and a Patron manifestation may have a Pact/Invocation dependency. Such links are explicitly modeled by the shared eligibility contract; do not impose blanket Patron requirements on all standard Pacts.
- **Hexblade and Vestige base-class placement remains undecided.** Since the historical Hexblade and Binder were distinct 3.5e base classes, we must eventually compare their independent progression/mechanical identity against reconstructing their 5.x Warlock configurations as Patrons and other parts. This is not yet content integration.
- **Fiend is retained as the first complete Patron conversion** because both in-SRD 5.x versions and fiendish 3.5e Warlock antecedents offer robust comparison. The latter remain reference material rather than new non-SRD entitlements.

See [Patron pillar rules and pending decisions](warlock-patron-pillar-reconciliation.md) and [ten-Patron cross-edition comparison](warlock-patron-3x-5x-comparison.md).

**Inheritance clarification (owner decision, 2026-10-24):** Descent from a Warlock does **not** itself establish Warlock class membership. Depending on the setting and original agreement, the child can inherit **power**, a **Patron connection**, **both**, or **neither**; inherited obligations may vary independently. **Continuing Patron-sourced power → Warlock; autonomous innate magic without a current Patron source → Sorcerer.** A descendant can therefore be a Sorcerer even when their ancestor was a Warlock. Patron connections without magic do not grant class levels. No universal contract-transfer or hereditary ability grant should be encoded; refer to [Patron inheritance rule](warlock-patron-pillar-reconciliation.md).

## Pillar-by-pillar implementation status

**Pillar 1 — Patron: source reconciliation underway.** The full Fiend SRD 5.1/5.2.1 source crosswalk and five stable Patron part identities have been recorded, with **source-faithful 2014/2024 preset projections** and 13 distinct spell-list entries. **2014 Fiend level-1 first grants, 2024 Blessing trigger, Charisma-modifier Luck uses with Long Rest recovery, and Hurl Through Hell are approved**. Fiend spell entitlement and Patron damage resistance remain unresolved. **Historical 3.5e fast healing has been reassigned from a universal base-class grant to one effect of the Fiend Patron's single Infernal Retaliation feature**. Its three effects share one use per Long Rest. Other historical base-class decisions are unaffected. See [Warlock Patron pillar reconciliation](warlock-patron-pillar-reconciliation.md) and `data/reference/srd/warlock-fiend-patron-crosswalk.json`. **This is a validated candidate mapping, not an active native Fiend feature package**; existing `pending-native-reconciliation` markers remain untouched.

**Next gate:** Resolve Fiend spell entitlements, Patron damage resistance, and remaining Infernal Retaliation acquisition/effect details, then author one effective native Fiend definition, referencing the same underlying parts from guided and custom progression. Only then move on to **Pacts**, followed by **Eldritch Invocations**, keeping cross-pillar entitlements and budget questions visible.

## Content boundary

1. Use the existing imported **SRD 5.1 (2014)** and **SRD 5.2.1 (2024)** Warlock sources for complete Band 1 (class levels 1–20) base-class progression, all SRD Pact Boons and Eldritch Invocation options, and the **Fiend Patron** subclass from both editions.
2. The union of candidate option names is **42**, with **35** 2014 entries (**32 Eldritch Invocations plus 3 separate Pact Boons**) and **28** 2024 Eldritch Invocations (**including all 3 Pacts**). See the source-backed `data/reference/srd/warlock-srd-slice-inventory.json` manifest for exact names and source categories.
3. Both source SRDs contain only **one Warlock subclass: Fiend** (2014 The Fiend; 2024 Fiend Patron). This enables a real subclass-to-parts and preset conversion, but **cannot prove cross-Patron combinations**. Use generic **non-playable synthetic test fixtures** to verify that the generic development contract is not hard-coded to Fiend. Do not invent playable non-SRD Patrons simply to make the test seem complete.
4. Preserve historically approved 3.5e **base-class** decisions recorded in `data/reference/srd/class-reconciliation-decisions.json`: Damage Reduction, Deceive Item, Energy Resistance, Imbue Item, and the exclusion of automatic level-2 Detect Magic. **The previously approved base-class Fiendish Resilience healing has been reclassified as a Fiend-only effect within the single Infernal Retaliation feature**; it is no longer a universal native Warlock class grant. The historical Warlock originates in *Complete Arcane* and is **not itself SRD material**; these decisions are a narrow **already-approved native-rule exception**, not permission to import that non-SRD source's invocation catalog.
5. **Excluded for now:** Pact of the Talisman, Hexblade, every other non-SRD Patron, outside-SRD options and supplements, the 90-item historical invocation research catalog, and Band 2 (21–40). Do not delete their archival reference material.

## Legacy 3.5e Warlock prestige classification — source versus native

**Owner-approved classification distinction (2026-10-24):** A specialization involving **more than one base class must be a native prestige class**. A **single-class prestige remains valid**, and must be assessed **case by case** before deciding to retain independent prestige progression or to represent it as a subclass, Warlock parts/bundles, and/or a guided preset. Source prestige labels remain unchanged for provenance.

- **Not yet classified:** Hellfire Warlock and Enlightened Spirit. They principally develop Warlock, but their original progression and costs may warrant **either** native prestige status or integration as Warlock specializations.
- **Prestige required:** Eldritch Disciple (Warlock + divine spellcasting class) and Eldritch Theurge (Warlock + arcane spellcasting class) involve multiple base classes and therefore remain prestige classes.

See [3.5e Warlock prestige-to-native classification](warlock-3x-prestige-native-classification.md). **All four remain reference-only during the SRD-only Warlock vertical slice**; their non-SRD mechanics, prerequisites, budgets, and integration are deferred.

## The integrated mechanical work

- Reconcile source-neutral Warlock base progression, Pact Magic, spell availability under the established native *no daily preparation* policy, Mystic Arcanum, Magical Cunning, Contact Patron, Eldritch Master, and approved historical class features. Preserve current class-level versus total-character-level Band rules and level-21+ Epic Boon eligibility.
- **Patron identity starts at Warlock level 1.** A Warlock can know whom they bargained with (or the nature of an inherited/noncontractual origin) from the beginning. This is not a mandate to grant all first Patron combat features at level 1: compare the actual 2014 level-1 and 2024 level-3 feature progressions and decide entitlement/timing explicitly. The 2024 Contact Patron level-9 mechanic can represent *direct communication* rather than the first knowledge of the Patron's identity.
- Deconstruct Fiend into distinct eligible parts plus coherent dependencies/bundles: Pact-related spell access, Dark One's Blessing, Dark One's Own Luck, the Fiend damage-resistance version of Fiendish Resilience, and Hurl Through Hell. Keep the 3.5e **historical base-class healing** provenance distinct from the modern Patron damage-resistance feature: in native rules, fast healing is one option under the **Fiend-only Infernal Retaliation** feature alongside Flamewreath and Hellfire Shield, with one total use per Long Rest. Preserve triggers, sources, and compatibility provenance.
- Reconstruct a **2014 Fiend preset** and a **2024 Fiend preset** with source-appropriate Pact entitlement and milestone mapping. They should resolve into the **same native part/effect definitions**, not duplicate source-specific gameplay engines. Native curated presets can offer a low-complexity experience, with a custom Warlock able to choose other qualifying parts under the same mechanical budget.
- Treat Blade/Chain/Tome as meaningful specializations, not just names on ordinary invocations. Compare separate 2014 Pact Boon entitlements and 2024 invocation expenditures before deciding if the first Pact is free, reserved, mandatory, or a normal choice. Ensure a guided Pact choice *feels consequential*, with compatible specialization upgrades; do not force every Warlock to pick a Pact until that is decided.
- Preserve at-level-1 **invocation access** in the design. Reconcile the invocation count and acquisition budget **after** determining what Pact and Patron parts consume. Validate prerequisites, option replacements, repeatability, tier/level unlocking, and dependencies (e.g., Blade → Thirsting Blade → Devouring Blade).
- Review Pact of the Blade in particular for fair **weapon-combat viability** against Eldritch Blast, considering attack counts, Charisma attacks, invocation investment, resource costs, and optional weapon mastery integration. This is a balance **test requirement**, not prior approval of new weapon mastery access or a numeric buff.
- Keep Magical Cunning's 2024 once-per-Long-Rest short-rite slot recovery as a comparison baseline. Evaluate its actual value but do not automatically increase it because one tester regards it as minor.

## Retz's initial 5e versus 2024 Warlock feedback — actionable acceptance criteria

| Tester observation | Design/test implication | Status |
| --- | --- | --- |
| Pact Boons being invocations improves versatility but reduces their feeling of significance | Test both the customization benefit and the **identity/advancement importance** of Pact choice; reconcile acquisition and costs first | Open mechanics |
| Pact of the Talisman is missed | Explicit future-extension consideration, **not in either SRD** and **out of this content slice** | Deferred |
| Pact of the Blade improves melee Warlocks beyond Hexblade, but Eldritch Blast may still outperform it | Test comparable melee and ranged configurations across levels, with equal budgets and realistic targets; consider, but do not grant, weapon mastery | Evaluation required |
| Invocation selection at level 1 is welcome | Preserve early class customization; validate level-1 experience including guided vs custom build | Direction established |
| Magical Cunning is only a minor recovery improvement | Evaluate resource value in short- versus Long-Rest play; do not expand the pool without evidence | Test requirement |
| A Warlock should know their Patron from level 1, not discover their subclass identity at level 3 | Make Patron/origin **identity explicit at level 1**; decide mechanical manifestation levels separately using both source progressions | Identity established; feature timing open |

Feedback is evidence about **experience and priorities**, not blanket approval of any balance change. Preserve the tester's concerns in evaluable terms for the first PDF and playtest.

## Minimal architecture and vertical-slice gates

1. **Universal development contract:** part ID, native effect identity, required feature/resources, eligibility, class and level track, grant source, prerequisite/dependency links, replacement/supersession, source/version provenance, and consumed advancement entitlement. Presets reference these parts; no hard-coded Fiend-specific engine.
2. **Integrated Warlock reconciled data:** complete Band 1 native class progression, complete in-scope selectable catalog, Fiend constituent parts, Pact definitions, preset mappings, source comparisons, and unresolved items addressed by owner review. Do not mark candidate data as approved native content until rulings are settled.
3. **Guided test:** construct recognizable 2014 and 2024 Fiend builds from presets without custom-only knowledge and without obtaining duplicate grants.
4. **Advanced test:** construct at least one meaningfully different but legally eligible Warlock from **the same in-scope parts**; validate prerequisite/upgrade chains, costs, replacements, spell access, and bounded resources.
5. **Edition tests:** exercise SRD51 vs SRD52 mapping, source timing, native no-preparation spell availability, level-1 Patron identity, and already approved historical class features. Preserve mechanical distinction for both Fiendish Resilience features.
6. **Balance/UX tests:** pact identity, Blade vs Eldritch Blast at representative levels (1/5/11/12/17/20), Magical Cunning/rest scenarios, advancement complexity, and a clear, accessible guided mode.
7. **Feedback gate:** create tester-ready class reference/PDF and use Retz's feedback to refine or reject the specialized Warlock development system *before* migrating additional classes. One SRD Patron means **generalization to all Patron combinations remains unproven**; identify this as a limit.

## Implementation boundary

This is a **source inventory and test-scope baseline**, not a declaration that all modules and game rules have already been implemented. No new non-SRD gameplay features are authorized. Keep existing accepted native class rulings intact. Character Sheet and Rules Wiki integration should follow actual working mechanics rather than claiming them ready from schema alone.
