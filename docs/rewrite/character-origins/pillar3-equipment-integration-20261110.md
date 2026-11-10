# Pillar 3 — Equipment qualification integration after PR #135

> **Follow-up exception review (2026-11-10):** [Source conversion exceptions and whole-package balance](pillar3-origin-training-exceptions-review-20261110.md) confirms that Folk Hero/Hermit omit historical tools deliberately, Gladiator replaces them with one weapon qualification, and a potential additional Dwarf craft rank is not established as necessary from current evidence. No additional rank or proficiency was awarded by that review.

**Conversion clarification (2026-11-10):** No Origin receives Competency training by default. The Upbringing-proficiency/Background-rank guideline is considered **only for prior-version benefits actually being converted**. Criminal's fixed Thieves' Tools, Spy's conditional Thieves' Tools, and Street-Raised's conditional Thieves' Tools are direct, binary equipment-proficiency conversions; they are **not** unsettled ranked Competency conversions. Source-specific current amounts and unallocated native alternatives are recorded in [Historical Origin Training Conversion Scope](pillar3-competency-origin-baseline-20261110.md). Later historical candidate allocations below remain research context, not active default effects.

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

- **High Elf:** the merged native equipment catalog defines Longsword, Shortsword, Shortbow and Longbow. All four approved types now have fixed `equipmentProficiencyGrants` in `upbringings/elf/high.json`. These are base weapon-type proficiencies, not Sword/Bow family grants or Weapon Mastery.
- **Wood Elf:** Shortbow and Longbow are both available in the merged catalog, and `upbringings/elf/wood.json` now has both canonical fixed type grants. Its contextual Survival modifier remains separate. No Weapon Mastery is inferred.
- **Gladiator** grants one specific mundane arena-appropriate weapon type and a matching starting weapon. The current catalog is partial, with neither the example Net nor Trident present. Do not grant all martial weapons, invent an exclusive Net/Trident pool, or detach the inventory choice from the proficiency.
- **Soldier/Sailor** grant Land/Water Vehicles respectively. The approved equipment contract covers weapon, armor, shield, and tool, **not** a vehicle/operator category. Preserve these separate benefits without coercing them to `tool`.

Expanding the equipment item taxonomy and defining vehicle mechanics are not prerequisites for further Background and Upbringing content or balance review, although the affected proficiency mappings remain unfinished.

## Choice balance implications and catalog coverage

The approved equipment catalog is **representative**, not a complete list of every historically available equipment type. Currently it exposes only **Dice Set** and **Playing Card Set** in the Gaming Set group, and **Lute**, **Flute**, and **Drum** in the Musical Instrument group. The new typed choices correctly avoid inventing entries, but a live menu built from the present catalog would offer a **narrower set of choices** than an unrestricted historical `any gaming set` or `any instrument` grant. This is missing catalog coverage, **not** authorization to restrict the Origin's intended category to those examples.

The more consequential balance question is **cross-domain choice asymmetry**. Under the owner's expected common conversion of **Competency proficiency + one rank**, the Competency outcome may carry wider or more frequently useful advancement than the narrow binary item-tool proficiency. Explicitly compare the following outcomes rather than assuming they are numerically interchangeable:

- **Spy and Street-Raised:** Disguise/Forgery/Calligraphy or Disguise/Cooking/Forgery versus Thieves' Tools.
- **Entertainer:** Disguise Competency training versus one musical-instrument proficiency.
- **Aristocratic:** Calligraphy/Painting/Cooking Competency training versus one gaming-set proficiency.
- **Criminal versus Charlatan:** Criminal's fixed Thieves' Tools plus gaming-set qualifications versus Charlatan's two distinct learned Competencies, while also comparing their separate social features and starting equipment.

These contrasts are **review flags, not approved compensation rules**. A narrower equipment qualification can still have distinct use, especially when relevant item actions are gated or specialized. No extra ranks, bonus tools, substitute choices, or blanket qualifications were introduced merely to equalize the number of displayed grants.

## Validation and exclusions

**Static checks performed:** The full Origin corpus of **16 Backgrounds + 13 Upbringings** was parsed as JSON. Every `skill.*` and `competency.*` reference recursively found in these 29 records resolves against the current `main` catalogs (**81 Skills, 26 Competencies**). No `competency.thieves-tools` reference remains. The **four root** and **four option-scoped** equipment grant blocks reference existing `main` equipment qualification IDs; the four mixed-domain choices preserve exactly one top-level alternative. The previous audit established that all five native Upbringing parent links resolve without cycles; no inheritance execution test was performed in this pass.

**Branch synchronization (2026-11-10):** The latest `main` was merged **into this Origin working branch only** in commit `89f7c6e7`; comparison now reports **0 behind `main`**. Git tree verification showed 44 Origin-side and 54 `main`-side changed paths, no overlapping changed paths, and preservation of both sets. This resolves the earlier branch-divergence blocker but does **not** mean Origin grants have executable end-to-end selection authorization.

**Origin regression coverage added:** `tests/test_character_origins_equipment_integration.py` verifies the canonical Skill/Competency reference catalog, fixed and selected equipment grant scopes, four exclusive mixed-domain choices, absence of duplicate associated-tool qualifications, and separate vehicle qualifications. The exact source divergences for selected Acolyte, Criminal, Sage, and Soldier were recorded in `docs/content-promotion/promotion-ledger.json` so the strict preserved-source comparison can continue validating those approved rewrites without treating their historical working copies as operative rules. Those four recorded changed-path sets were checked independently against the corresponding native/working JSON and matched exactly.

**Observed CI limitation:** An earlier branch validation run (prior to the four ledger additions) failed with five known existing Python/SRD migration cases **and** the four just-recorded Background source divergences. The divergence entries have passed the static exact-path check, but a subsequent complete workflow run has **not** been verified. No passing full native JSON audit, .NET build, native evaluator run against these new Origins, Character choice authorization, inventory materialization, or end-to-end Character Sheet test is claimed. The branch can now be tested against current `main` once the workflow reports its results.

**Competency allocation review:** `pillar3-competency-grant-allocation-review-20261110.md` inventories all 13 Origin Competency grant sites, proposes source-by-source candidate effects, records potential rank stacking and asymmetries, and evaluates a contrasting allocation sensitivity case without imposing a quota. It does **not** assign numeric rank/proficiency effects or alter approved choices.

**Origin choice authorization plan:** `pillar3-origin-choice-authorization-plan-20261110.md` specifies the five heterogeneous benefit choices, trusted selection boundaries, and the critical unresolved issue of making **selected nested equipment options** available as catalog-backed authorized source occurrences. A nested option is **not** executable simply by submitting its parent Origin to the equipment evaluator.

**Scoped Upbringing checks:** `pillar3-scoped-upbringing-bonuses-20261110.md` distinguishes Dwarf Stonecunning and Rock Gnome Artificer's Lore from Drow/Wood Elf fixed Survival bonuses, documents the historical source distinction, and isolates the unapproved 2 × Proficiency Bonus replacement/stacking question from general Competency rank allocation.

**Fresh synchronized-branch content verification:** All 29 native Origin JSON records parsed, with **129 recursively checked `skill.*`/`competency.*` references** and **8 equipment grant blocks** (four root, four nested). Every checked canonical reference and equipment target/group matched the synchronized native catalogs. Existing Upbringing parent relationships remain five, with no additional parent added. These are static checks, not runtime test results.

**Workflow trigger boundary:** The current `.github/workflows/validate.yml` runs automatically for `main` or pull requests into `main` and may be dispatched manually; ordinary pushes to this working branch alone do **not** establish new passing CI. Existing failed run results predate synchronization and the four exact source divergence records.

## Remaining Character Origins decisions

1. Allocate actual Competency **proficiency + one rank / rank only / proficiency only** source by source. Do not create rank gains merely from repeated binary equipment qualifications. Pay attention to fixed Charlatan/Sailor/Rock Gnome grants and one-of trade or cultural training choices.
2. Specify and validate the Origin selection-authority projection for cross-domain choices, retaining the same one-of cardinality and provenance; Guild Merchant's Navigation-versus-extra-language is a similar unresolved cross-domain case.
3. **Longbow is reconciled** and High/Wood Elf weapon type grants have been authored and checked. Define the specific Gladiator eligible types and linked starting item when that eligibility is approved; defer vehicle operator/category semantics until separately approved. These should not block unrelated Origin balance work.
4. Decide whether any unavoidable duplicate Origin/Class Skill or equipment qualification receives an alternative benefit. At present, Criminal + Rogue gives one effective Thieves' Tools proficiency with two independent source occurrences and no invented compensation.
5. Reassess conventional and cross-cultural full character packages, including General/Rock Gnome value, skills, contextual modifiers, language grants, equipment costs, social support, and conditional versus persistent benefits. No numerical balancing changes were made in this pass.
