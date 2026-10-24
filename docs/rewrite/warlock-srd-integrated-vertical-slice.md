# Warlock: integrated SRD-only vertical slice

**Status:** owner-directed vertical-slice scope (2026-10-24). This document fixes **content boundaries and evaluation requirements**; it does **not** automatically approve unresolved mechanical rulings or claim an implemented/playable class.

**Parent architecture:** `docs/rewrite/subclass-deconstruction-guided-complexity-policy.md`. Warlock is the first full application of that policy. This work deliberately overrides the older **base-class-only sequencing for Warlock**: its base class, Patron subclass, Pact Boons, and Eldritch Invocations must be designed, reconciled, implemented and tested **together**. Other classes remain subject to their established workflow until separately approved.

## Content boundary

1. Use the existing imported **SRD 5.1 (2014)** and **SRD 5.2.1 (2024)** Warlock sources for complete Band 1 (class levels 1–20) base-class progression, all SRD Pact Boons and Eldritch Invocation options, and the **Fiend Patron** subclass from both editions.
2. The union of candidate option names is **42**, with **35** 2014 entries (**32 Eldritch Invocations plus 3 separate Pact Boons**) and **28** 2024 Eldritch Invocations (**including all 3 Pacts**). See the source-backed `data/reference/srd/warlock-srd-slice-inventory.json` manifest for exact names and source categories.
3. Both source SRDs contain only **one Warlock subclass: Fiend** (2014 The Fiend; 2024 Fiend Patron). This enables a real subclass-to-parts and preset conversion, but **cannot prove cross-Patron combinations**. Use generic **non-playable synthetic test fixtures** to verify that the generic development contract is not hard-coded to Fiend. Do not invent playable non-SRD Patrons simply to make the test seem complete.
4. Preserve the previously approved historical 3.5e **base-class** decisions recorded in `data/reference/srd/class-reconciliation-decisions.json`: Damage Reduction, Deceive Item, Fiendish Resilience (healing), Energy Resistance, Imbue Item, and the exclusion of automatic level-2 Detect Magic. The historical Warlock originates in *Complete Arcane* and is **not itself SRD material**; these decisions are a narrow **already-approved native-rule exception**, not permission to import that non-SRD source's invocation catalog.
5. **Excluded for now:** Pact of the Talisman, Hexblade, every other non-SRD Patron, outside-SRD options and supplements, the 90-item historical invocation research catalog, and Band 2 (21–40). Do not delete their archival reference material.

## Legacy Warlock prestige classification — source versus native

**Case-by-case classification is required.** A 3.5e source label of `prestige class` is preserved as source provenance but is **not an automatic native prestige-class decision**. Review whether the feature chain is intrinsically a Warlock specialization, an independent advancement path, a cross-class hybrid, or a cohesive set of separately eligible parts. Do not convert 3.5e entries into fictitious 5e subclasses first.

**Reference-only during this SRD slice:** Hellfire Warlock and Enlightened Spirit appear likely candidates for Warlock-specific specializations or coherent part/bundle progressions; Eldritch Disciple and Eldritch Theurge are materially different because they progress invocation use **and another class's divine/arcane spellcasting**. They are candidates for retaining prestige/hybrid progression unless a shared class-development model can faithfully preserve dual-class advancement. **None of these four has an approved final native category or executable option conversion.**

See [3.5e Warlock prestige-to-native classification research](warlock-3x-prestige-native-classification.md). Preserve the existing source-concept distinction between subclasses and prestige classes; native transformations must maintain versioned mappings to the original source records. This reference does not expand the approved SRD-only implementation scope.

## The integrated mechanical work

- Reconcile source-neutral Warlock base progression, Pact Magic, spell availability under the established native *no daily preparation* policy, Mystic Arcanum, Magical Cunning, Contact Patron, Eldritch Master, and approved historical class features. Preserve current class-level versus total-character-level Band rules and level-21+ Epic Boon eligibility.
- **Patron identity starts at Warlock level 1.** A Warlock can know whom they bargained with (or the nature of an inherited/noncontractual origin) from the beginning. This is not a mandate to grant all first Patron combat features at level 1: compare the actual 2014 level-1 and 2024 level-3 feature progressions and decide entitlement/timing explicitly. The 2024 Contact Patron level-9 mechanic can represent *direct communication* rather than the first knowledge of the Patron's identity.
- Deconstruct Fiend into distinct eligible parts plus coherent dependencies/bundles: Pact-related spell access, Dark One's Blessing, Dark One's Own Luck, the Fiend damage-resistance version of Fiendish Resilience, and Hurl Through Hell. Keep the **historical base-class healing** Fiendish Resilience separate. Preserve actual native triggers, source differences, resources, replacement rules, and compatibility provenance.
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
