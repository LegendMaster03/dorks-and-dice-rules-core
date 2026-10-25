# Warlock Pact-specific Invocations — codification first, conflict resolution second

**Branch:** `feature/rules-core-rewrite`  
**Date:** 2026-10-25  
**Status:** **Pact-specific Invocation names, attachment types, source variants, and open conflicts codified.** **Do not claim native effects are playable or choices available.** Review/edit mechanics and entitlement costs only after individual owner approval.

## Owner-approved work order

1. **Codify** every previously identified Invocation that requires one of the four active Pacts.
2. **Resolve conflicts** where source editions disagree, Pact intrinsic improvements overlap, or source prerequisites refer to features relocated into native Pacts.
3. **Only after conflict reconciliation**, determine whether to expand into **3.5e Invocation research** or to decide **native Invocation selection progression**. Neither latter choice has been made.

## Codified catalog: 16 names, 4 Pacts

Source variants and typed Pact binding are recorded in [`data/reference/warlock-pact-specific-invocations.json`](../../data/reference/warlock-pact-specific-invocations.json). The **nine SRD names** also have staged metadata on their **existing** `data/native/features/optional/*.json` records, without altering original 2014/2024 candidate texts or replacing their `pending-native-reconciliation` status.

| Pact | Already imported SRD options (9) | Previously identified published non-SRD options (7) |
| --- | --- | --- |
| **Blade (5)** | **Thirsting Blade**; **Devouring Blade**; **Lifedrinker**; **Eldritch Smite** | **Improved Pact Weapon** (*Xanathar's*) |
| **Chain (4)** | **Voice of the Chain Master**; **Chains of Carceri**; **Investment of the Chain Master** | **Gift of the Ever-Living Ones** (*Xanathar's*) |
| **Tome (4)** | **Book of Ancient Secrets**; **Gift of the Protectors** | **Aspect of the Moon** (*Xanathar's*); **Far Scribe** (*Tasha's*) |
| **Talisman (3)** | — (not in either SRD) | **Rebuke**, **Protection**, and **Bond of the Talisman** (*Tasha's*) |

**Source provenance boundary:** The seven non-SRD entries are **independently paraphrased, source-attributed research candidates only**. Their original rules text is not imported into SRD data or activated in the native character builder. The three Talisman options have already been approved to **remain optional**, but their full native mechanical conversion is still staged; this approval does not supply free selections. The other four supplement options are newly indexed from **previously identified** research, **not newly approved playable options**.

**Shared Invocations:** This 16-name group is not the entire Warlock Invocation catalog. Every *general* Invocation may attach to any otherwise eligible concrete Pact, subject to feature prerequisites and associated Patron acceptance. This pass explicitly catalogs options with a **named Pact prerequisite**. Source edition names do not change native Pact eligibility without approval.

## Open conflicts — next work phase (not resolved here)

| Priority | Invocation | Conflict / missing ruling |
| --- | --- | --- |
| 1 | **Thirsting Blade** | Its 2014/2024 second Pact-weapon attack is **already automatically granted** at native Blade level 5. Decide archival-only, repurpose or separate source-compatibility representation; **never add another free attack** by duplication. |
| 2 | **Devouring Blade** | 2024 requires **Thirsting Blade Invocation** at level 12 to add a third total attack. Determine how to express eligibility after Thirsting's native disposition without silently granting three attacks. |
| 3 | **Investment of the Chain Master** | 2024 includes a Bonus Action familiar Attack command, while native Chain gains a level-5 familiar extra attack. Resolve base familiar command, Attack action, player-initiative house rule and optional Invocation enhancements **without requiring Investment just to use the base Pact**. |
| 4 | **Book of Ancient Secrets** | 2014 begins by granting two 1st-level rituals; 2024 Tome base already has two. Preserve advanced ritual acquisition without duplicate starters or an unnecessary Invocation tax; no ruling on advanced book progression yet. |
| 5 | **Lifedrinker** | 2014 **per-hit Charisma Necrotic** vs 2024 **once-per-turn d6 damage-type choice + Hit Die healing**; prerequisite 12 vs 9. These are substantially different mechanics, not a wording edit. |
| 6 | **Improved Pact Weapon** | Non-SRD option partly duplicates native Blade spellcasting-focus treatment. Conditional weapon bonus and additional conjured weapon categories still require review. |
| 7 | **Talisman/Rebuke** | Approved **Shared Ward** and optional **Rebuke** both use the Warlock's Reaction; clarify timing and mutual exclusion per triggering attack. They must remain separate effects and Rebuke consumes its own Invocation choice. |
| 8 | **Protection/Bond of the Talisman** | Each elective Invocation has **its own** PB/Long Rest usage pool, distinct from Talisman's intrinsic shared Fortunate Correction/Shared Ward charges. Preserve source-defined level gates 7 and 12. |
| 9 | **Other** | Chain familiar healing with **Gift of the Ever-Living Ones**, Tome rest/sleep with **Aspect of the Moon**, Tome spell/message lifecycle with **Far Scribe**, and special Hold Monster per-target recovery with **Chains of Carceri** require shared mechanic compatibility review but have no approved native replacements. |

**Already-settled foundations must remain fixed:** first Pact at Warlock level 1 **free of Invocation selections**; every Invocation attaches to a particular Pact even if generally eligible; the Patron **of that Pact** must accept the clause, and Pact/Patron prerequisites are evaluated in that same context. Broken Pact -> attached Invocations inactive, not history-deleted. **Blade, Chain, Tome and Talisman** retain their owner-approved level-5 intrinsic benefits. **Star Chain/Seeker remain disabled.** The new manifest does not change the budget or propose any 3.5e grant.

## Validation and workflow boundaries

- Nine existing SRD Invocations remain **staged**, with their `candidateDefinitions` intact and separate per 2014/2024 source; none has a `definition` activated by this pass.
- Seven supplement names exist as **reference-only** records; no new native playable entities or 2024 SRD licensing claims were created.
- A new test `tests/test_warlock_pact_specific_invocation_codification.py` verifies 16 unique options, distribution of **5 Blade / 4 Chain / 4 Tome / 3 Talisman**, native/source correspondence, source candidate preservation, non-SRD boundaries, owner-approved Pact state and unresolved conflict flags.
- Do not silently resolve Talisman recharge behavior, Thirsting/Devouring, Chain attacks or Tome ritual overlap by reusing another edition's description.
- At the conflict-resolution stage, treat each unresolved **choice as owner-reviewable** and update one reconciled native rule after approval. Ordinary compatible source-only options may follow a separate batch decision later.
