# Pillar 3 — Equipment qualification integration after PR #135

**Date:** 2026-11-10  
**Branch:** `feature/character-origins-equipment-contract-20261110`  
**Predecessor:** `feature/character-origins-competency-audit-20261109`  
**Canonical equipment contract:** `docs/rewrite/equipment-qualification-contract.md` on `main` (merged in PR #135).  
**Existing origin audit:** `docs/rewrite/character-origins/pillar3-competency-contract-audit.md`, whose November 9 broken-reference and dependency statuses are superseded by this report.

## Scope and established rules

This is a Character Origins **data migration**, not an equipment proficiency redesign or a generalized Character builder. Most traditional tool training remains part of one native **Competency** with independent binary proficiency and nonnegative ranks. Its `associatedTools` does not automatically confer a second equipment qualification or physical item.

Thieves' Tools, individual Gaming Sets, and individual Musical Instruments are explicit **binary equipment proficiency** exceptions. The native equipment resolver handles fixed scopes, counted specific-type choices, group coverage, and repeated Boolean grant provenance. It does not grant Competency ranks, inventory items, eligibility to attempt every action, or a universal check modifier.

For source-to-native Competency conversion, the owner's expected order of prevalence is **proficiency + one rank**, then **one rank only**, then **proficiency only**. No record-by-record rank allocation or global rank budget was approved by this migration.

## Changes to Origin grants

| Origin | Authored result | Authorization requirement |
| --- | --- | --- |
| **Criminal Background** | Root fixed `equipment.tool.thieves-tools` and exactly one Gaming Set type from `equipment.group.tool.gaming-set` | Character must hold the Background and choose one eligible Gaming Set |
| **Noble Background** | Root one-Gaming-Set-type choice | Character must choose one eligible type |
| **Soldier Background** | Root one-Gaming-Set-type choice; Land Vehicles unchanged | Land Vehicles remain a separate unresolved operator/vehicle qualification |
| **Knight Background** | Root **one total choice** from the union of Gaming Sets and Musical Instruments | One instrument **or** one gaming-set type, never both |
| **Spy Background** | One heterogeneous `benefit-choice`: Disguise, Forgery, or Calligraphy Competency **or** Thieves' Tools equipment proficiency | Selected tool option alone holds an `equipmentProficiencyGrants.fixed` block; Origin must authorize and materialize that selected option as a source occurrence |
| **Street-Raised Upbringing** | One heterogeneous `benefit-choice`: Disguise, Cooking, or Forgery Competency **or** Thieves' Tools equipment proficiency | Same conditional option authorization as Spy |
| **Entertainer Background** | Existing choice: Disguise Competency **or** one Musical Instrument type; instrument option holds conditional `equipmentProficiencyGrants.choices` | Selecting Disguise must not also emit an instrument grant; instrument type selection is independently counted as one |
| **Aristocratic Upbringing** | Existing choice: one Gaming Set type **or** Calligraphy, Painting, or Cooking Competency; Gaming Set option has conditional equipment choice | One of four branches; equipment resolver alone does not enforce cross-domain choice exclusivity |
| **Charlatan Background** | Clarified documentation that native Forgery is one Competency rather than a historical Skill/tool split | Existing two Competency grants retained; no equipment proficiency implied |
| **Guild Artisan Background** | Clarified `crafting-competency` comments; retired Thieves' Tools Competency and old facet model no longer described | Craft eligibility pool and specific rank/proficiency values still need review |

All conditional `equipmentProficiencyGrants` blocks on the nested options are **option metadata**, not unconditional root grants. The merged native evaluator reads source-level grants, not nested `benefit-choice` branches. The Origin choice authority must validate the winner, then submit the actual option grant as an authorized source occurrence. Simply sending the whole Spy, Street-Raised, Entertainer, or Aristocratic entity does **not** authorize the nested equipment outcome.

All original Skills, Competency option identities, selected-option counts, starting possessions, Background features, and Upbringing inheritance relationships were retained.

## Native equipment taxonomy gaps relevant to Origins

- **High Elf** has proficiency with Longsword, Shortsword, Shortbow, and Longbow. The representative catalog defines the first three but not Longbow. No partial fixed grants were emitted; preserve the approved four-type entitlement pending Longbow reconciliation.
- **Wood Elf** has Shortbow and Longbow; the same missing type blocks complete mapping. Its contextual Survival modifier is separate from weapon proficiency.
- **Gladiator** grants one specific mundane arena-appropriate weapon type and a matching starting weapon. The current catalog is partial, with neither the example Net nor Trident present. Do not grant all martial weapons, invent an exclusive Net/Trident pool, or detach the inventory choice from the proficiency.
- **Soldier/Sailor** grant Land/Water Vehicles respectively. The approved equipment contract covers weapon, armor, shield, and tool, **not** a vehicle/operator category. Preserve these separate benefits without coercing them to `tool`.

Expanding the equipment item taxonomy and defining vehicle mechanics are not prerequisites for further Background and Upbringing content or balance review, although the affected proficiency mappings remain unfinished.

## Validation and exclusions

**Static checks performed:** The full Origin corpus of **16 Backgrounds + 13 Upbringings** was parsed as JSON. Every `skill.*` and `competency.*` reference recursively found in these 29 records resolves against the current `main` catalogs (**81 Skills, 26 Competencies**). No `competency.thieves-tools` reference remains. The **four root** and **four option-scoped** equipment grant blocks reference existing `main` equipment qualification IDs; the four mixed-domain choices preserve exactly one top-level alternative. The previous audit established that all five native Upbringing parent links resolve without cycles; no inheritance execution test was performed in this pass.

**Not executed:** Native .NET resolver/host tests against this Origins branch, full Origin choice authorization, inventory materialization, and end-to-end Character Sheet tests. The integration branch was created from the independent Origin feature lineage and has **not synchronized the latest `main`**, where PR #135 implemented the new evaluator. A successful cross-branch static identity check is not evidence of native runtime integration. The equipment foundation itself was tested and merged in PR #135, but those results do not validate these new Origin records.

## Remaining Character Origins decisions

1. Allocate actual Competency **proficiency + one rank / rank only / proficiency only** source by source. Do not create rank gains merely from repeated binary equipment qualifications. Pay attention to fixed Charlatan/Sailor/Rock Gnome grants and one-of trade or cultural training choices.
2. Specify and validate the Origin selection-authority projection for cross-domain choices, retaining the same one-of cardinality and provenance; Guild Merchant's Navigation-versus-extra-language is a similar unresolved cross-domain case.
3. Reconcile Longbow and specific Gladiator eligible types; defer vehicle category semantics until separately approved. These should not block unrelated Origin balance work.
4. Decide whether any unavoidable duplicate Origin/Class Skill or equipment qualification receives an alternative benefit. At present, Criminal + Rogue gives one effective Thieves' Tools proficiency with two independent source occurrences and no invented compensation.
5. Reassess conventional and cross-cultural full character packages, including General/Rock Gnome value, skills, contextual modifiers, language grants, equipment costs, social support, and conditional versus persistent benefits. No numerical balancing changes were made in this pass.
