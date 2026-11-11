# Character Origins — Pillars 1 and 3 native readiness and consistency

**Date:** 2026-11-10  
**Branch:** `feature/character-origins-equipment-contract-20261110`  
**Authoritative content:** `data/rulesets/first-party-001/native/`  
**Status:** Pillars 1 and 3 native content finalization. The approved parent, duplicate-proficiency, and Gladiator choices are authored and have passed targeted tests on the review branch. Full Origin execution is **intentionally deferred until Pillar 2** provides its mixed-Ancestry and inheritance rules; this is not a Pillar 1/3 blocker.

## Intended deliverable

Produce a coherent native JSON specification for:

- **Pillar 1 — Species and Ancestry:** intrinsic traits, playable leaf selection, parent inheritance, default Upbringing *suggestions*, and valid identity references.
- **Pillar 3 — Backgrounds and Upbringings:** learned training and languages, culture, origin features, source choices, ordinary starting possessions, and applicable inherited Upbringing benefits.
- **Interface of Pillars 1 and 3:** a character directly chooses its Species and applicable Ancestry, one effective Upbringing (with inherited parents), and one Background. A Species/Ancestry default is a suggestion, **not** an extra cultural package or a Species restriction. Mixed Ancestry is independent of this deliverable.

**Usable at this phase** means an independently interpretable, internally consistent JSON content corpus capable of supporting manual review/playtest and later engine implementation. It does **not** require that the Character Sheet, C# selection authority, stored character state, or Origin UI already execute these records. This ruleset is still being designed and is not in use; **there is no existing-character Competency rank migration problem**.

Review source-edition mechanics as reference evidence. Do not automatically restore previously omitted grants, add a universal feature/point budget, or treat a historical item-product specialty as an independent current Competency. The retained native method/discipline resolves crafting applicability; product type and recipe are separate.

## Current corpus and foundational consistency

- **Pillar 1:** Nine base Species definitions and 29 Ancestry records, including nonselectable Dragonborn family nodes. Chromatic/Metallic Dragonborn have five selectable leaves each; Gem is currently a deliberately nonselectable, incomplete family, not an available playable leaf.
- **Pillar 3:** Sixteen native Backgrounds and thirteen Upbringings. The **nonselectable** Elf General parent is inherited by Drow, High and Wood Elf Upbringings; the **nonselectable** Gnome General parent is inherited by Forest and Rock Gnome Upbringings. General Dwarf and General Goliath are currently independent, selectable cultural packages, not inheritance parents.
- **Crafting identities:** 26 native Competencies, with Smithing retained and historical product-specific Bowmaking, Armorsmithing, Weaponsmithing, Shipmaking and Trapmaking retired. Bow manufacture is associated with the appropriate woodworking method/recipe (Carpentry or Woodcarving, for example) rather than a Bowmaking advancement track.
- **Selection policy authored:** `native/rules/species-subspecies-upbringing.json` defines one effective Upbringing, valid selectable defaults, nonselectable inherited parents and benefit inheritance once. The base Elf/Gnome Species no longer suggest nonselectable parents; their Ancestries provide playable default suggestions.
- **Tests kept current:** `test_goliath_upbringings.py` now refers to `species/ancestry/goliath/` and the authoritative `native/skills.json` / `native/competencies.json`. `test_character_origins_cross_pillar_consistency.py` tests identity, ancestry/upbringing parent graphs, default eligibility, canonical references, and absence of retired product-Competency IDs. `test_character_origins_equipment_integration.py` now asserts the six exact canonical High/Wood Elf weapon-type grants and absence of implied mastery. The active Origin feature branch recorded 52/52 targeted Origin/Goliath/weapon-training tests before finalization. The review branch separately passed 53/53 targeted tests in run 42949159902; the combined branch requires a fresh run.

## Remaining Pillar 1/Pillar 3 decisions and fixes

| Priority | Issue | Required content decision or correction | Readiness effect |
| --- | --- | --- | --- |
| **Resolved** | **Five independent native Competency choice sites** | All 11 grant effects explicitly authored: Explorer and Spy **one rank only**; Aristocratic, General Goliath and Street-Raised **proficiency only** | Every existing Competency alternative has an authored amount, with mixed equipment alternatives kept separate |
| **Resolved** | **Guild Artisan shared crafting-method eligibility** | `native/competencies.json` now contains `competency-category.crafting-method` and a canonical member list referenced by `background.guild-artisan` | Guild Artisan chooses one eligible method appropriate to its guild trade and grants **one rank only** |
| **Resolved** | **General Elf and General Gnome inheritance-only** | Both now have `selectable: false`; corresponding base Species defaults are removed, Ancestry defaults remain selectable, and inherited parent benefits are unchanged | One effective Upbringing, no invalid Species suggestion, and no parent-as-second-choice |
| **Resolved in content** | **Equipment and ordinary starting items** | High/Wood Elf Longbow grants are canonical. Gladiator links one native base weapon proficiency to its matching ordinary starting item; the DM decides arena suitability. Entertainer links its chosen Disguise/Instrument benefit to the matching physical performance item. Vehicle mechanics belong to another system. | Future Origin execution must preserve authorized choices and conditional inventory; no extra content allocation is required |
| **Resolved in JSON** | **Scoped History proficiency multipliers** | Both features now explicitly replace the normal proficiency contribution with **2×PB on qualifying History checks**, including when History is untrained. Stonecunning's considered-proficient status applies only to that check | Actual Skill/check evaluator integration remains separate |
| **Resolved in JSON** | **Rock Gnome Prestidigitation casting Ability** | Select **Intelligence, Wisdom, or Charisma** for the Upbringing-granted Prestidigitation cantrip | Spell authorization/execution is a later implementation concern |
| **Deferred** | **General Gnome shared cultural knowledge** | The proposed general cultural feature has no approved mechanical allocation. General Gnome currently grants its language and its children supply their own features. | Do not fabricate a new grant or block completion on this idea; revisit with further evidence |
| **Resolved allocation; monitor playtest** | **Craft-focused conversion exceptions** | General Dwarf remains **proficiency only** and Guild Artisan remains **one rank only**. No automatic both allocation was approved | Later balance changes require new source-specific evidence, not inferred default grants |

**Settled boundaries not to reopen during this pass:**

- **Neither Competency proficiency nor a rank is the default for an Origin.** For genuinely retained historical Competency training, Upbringing proficiency and Background one rank are *starting conversion guidelines*, with justified both/neither exceptions. Eight historical grant outcomes and eleven newly designed Competency grant outcomes are explicitly authored.
- **Thieves' Tools proficiency converts directly** to `equipment.tool.thieves-tools` in Criminal and the conditional Spy/Street-Raised options. It is not a ranked Competency or a new debate about its training amount.
- **Folk Hero and Hermit** deliberately do not preserve their historical source tools; **Gladiator** deliberately substitutes an arena weapon qualification. **Entertainer** intentionally grants one mutually exclusive Disguise/Instrument choice. Do not replay discarded 2014 grants.
- **Native Competency progression, recipe timing, rank features and traits** belong to the already established Competency rules, not a new Pillar 1/3 design process. Their internal formulas are out of scope.
- **Parent benefits** are inherited once. The effective Upbringing is chosen once and can be cross-cultural. Species, Ancestry, cultural traits, and occupational experience should not be remerged just to follow an old Race/Background package.
- No actual persisted characters use this experimental content. No migration/compatibility accounting for existing ranks is necessary.

## Content validation from the approved allocation pass

- **All 16 Backgrounds and 13 Upbringings** were read as live branch JSON. All **19** authored Competency-training objects now have explicit, nonempty `competencyTraining` effects with valid rank/proficiency types. Canonical Skill and Competency references checked without error.
- **All nine base Species** were checked for valid identity/default behavior. The five Elf/Gnome Ancestry suggestions still target their respective selectable Upbringings; the two general cultural parents are nonselectable and the base Elf/Gnome defaults were removed.
- **Guild Artisan** references the authoritative `competency-category.crafting-method` with 22 valid native method members; its illustrative list is a subset and does not override the shared category.
- **Scoped History and Rock Gnome spell selection** fields were checked for the approved replacement/eligibility semantics. **Gladiator** selects one individual weapon type through the approved equipment selector and grants no mastery.
- The original static JSON review is supplemented by **executed GitHub Actions** on PR #138: 52/52 dedicated Origin/Goliath/weapon-training Python tests, 99/99 Mechanics tests, 111/111 Host tests, and the rewrite structure validator passed. The .NET Release build completed with zero warnings or errors. This is source-contract and shared-resolver validation, **not** proof of a Character choice executor or end-to-end Origin authorization.

## Executed validation status

The dedicated Character Origins CI step now runs independently of the repository-wide migration suite. In PR #138 the **39 Character Origins**, **7 Goliath Upbringings**, and **6 weapon-training** tests passed. The .NET build, **99 Mechanics** tests, **111 Host** tests, and rewrite structure validation passed as well.

The pinned source-coverage manifest retains four historical `subspecies.*` identifiers but explicitly links them to corresponding approved `ancestry.*` records. This corrects the legacy path/identity mismatch without resurrecting old Subspecies mechanics or altering the pinned SRD source count. The full Python suite now reports two remaining cross-domain failures, so the full CI workflow is not green and downstream smoke tests have not run.

## Final comparative review and approved policy disposition

| Combination or issue | Final interpretation |
| --- | --- |
| Giant Foundling + Soldier | Both sources grant Intimidation. A second authorized Skill proficiency permits a different Skill of the same kind, not a rank. |
| General Dwarf + Guild Artisan, both choosing Smithing | One grants proficiency; the other grants one rank. They complement each other, so no duplicate-proficiency conversion applies. |
| General Goliath + Sailor, both choosing Navigation | One grants proficiency and the other one rank; Water Vehicles belongs to the separate vehicle domain. |
| Street-Raised + Spy, both choosing Thieves' Tools | A duplicated specific-tool qualification permits another eligible tool proficiency after authorization, never a Competency rank. |
| Aristocratic + Noble, overlapping Gaming Set choice | Duplicate game-set qualifications follow same-kind replacement; their social features remain distinct. |
| High Elf and broad weapon coverage | Wide/narrow weapon overlaps preserve proficiency provenance but do not generate wildcard weapon choices or mastery. |
| Forest versus Rock Gnome | Keep asymmetric approved benefits; the unallocated General Gnome cultural feature is deferred, not automatically added. |
| Gladiator | One mundane base weapon proficiency and its matching ordinary starting item; DM adjudicates arena suitability. |
| Duplicate Competency proficiency | The independent policy supports `add-rank` (provisional default) and `alternate-proficiency`, without changing individual Origin grants. |
| Duplicate languages | Preserve learned-language sources; automatic extra language choice has not been approved. |

**Parent Upbringings:** Parents are inheritance-only by default. A directly selectable parent needs an explicit exception; inherited benefits apply once.

**Policy switch status:** Both outcomes are exposed in native JSON as an **experimental optional rule for playtest selection**, with `competencyDuplicateProficiencyResolution` set to `add-rank` (specialization) or `alternate-proficiency` (versatility). The DM/playtest organizer chooses the mode and records it with the character/session results. The future Character Sheet can expose a UI control, but a UI is not required to try either rule now. Relative balance and future rank-milestone pacing remain open empirical questions, not prerequisites to testing.

**Audit integration:** Recursive reference checks, fixed Skill overlap detection, all directly selectable Origin combinations and five mutually exclusive mixed-domain Origin choices are tested at the content level. The newer five-choice, sixteen-outcome conformance matrix and non-authoritative C# choice preview are additional checks, not a character acquisition/authorization implementation.

## Finalization branch validation (PR #139)

The branch was tested through a temporary push-only GitHub Actions trigger on commit `6f3abc70437157d8d7f8bb279618235a54fc85ba` ([workflow run 42948414412](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/42948414412)). The original main-only workflow configuration was restored after validation; it is **not** part of the final PR diff.

- Release restore/build: **passed**.
- Entire C# test suite: **93/93 Mechanics** and **111/111 Host tests passed**; this includes the two duplicate-Competency proficiency policy modes.
- Origin source-contract regressions: **40/40** passed. Goliath Upbringing tests: **7/7** passed. Native weapon-training tests: **6/6** passed.
- Repository-wide Python suite: **376 executed, 2 failed**. Those failures are the already documented `test_native_creature_types_reconciled` inherited working-evidence comparison and `test_ruleset_split` broad authoritative/working conflicts. No newly added Origin assertion failed.
- As the full Python step failed, later Docker validation steps were **skipped**; this run does not demonstrate full repository CI health or end-to-end Origin character creation.

The `add-rank` policy is the provisional default; `alternate-proficiency` is an available experimental playtest option. **Neither is presumed balanced against the other.** Both are usable via manually selected campaign/session policy and the existing typed reducer. A Character Sheet control and automatic collection of feedback are still unimplemented.

## Experimental duplicate-Competency rule: playtest procedure

The ruleset exposes two supported experimental variants through `rule.competency-duplicate-proficiency` and `optionalRule.settingKey = competencyDuplicateProficiencyResolution`. Choose **one** before a campaign/playtest session: `add-rank` converts every additional distinct proficiency grant into a rank in the same Competency, while `alternate-proficiency` converts it into a new, authorized proficiency in a different Competency. Both variants preserve separate explicit rank grants; no same-source occurrence may be replayed.

Record the session or build identifier, the **selected policy value**, the affected Competency and original grant sources, the actual extra rank or new proficiency, and what happened during relevant checks or rank-dependent features. A session with no duplicates should still record the selected value so the denominator of the comparison remains visible. Changing the setting between sessions requires recalculating character training from retained source occurrences, not stacking the new interpretation on the prior projection.

Playtest feedback should compare opportunities used, limitations encountered, and perceived tradeoffs between specialization and broader access. **Do not decide equivalence before observing results.** The future Origin authority and Character Sheet should eventually offer the setting directly; the current source-only test phase can use manual selection and recorded feedback.
### Experimental optional-rule contract verification

The optional-rule metadata and feedback fields were checked in [finalization CI run 42949159902](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/42949159902). The .NET build and full C# tests passed (**93 Mechanics**, **111 Host**). The focused Python suites passed (**40 Character Origins**, **7 Goliath**, **6 native weapon training**). The existing full-suite Python stage still has **2 unrelated failures in 376 tests**, concerning creature source inheritance and the native/working split; remaining Docker stages were skipped. The temporary workflow trigger used to run this branch check was restored afterward.

These tests confirm the authored, discoverable playtest choice contract; they do not constitute a deployed Character Sheet UI or automatic collection of playtest observations.

## Pillar 2 dependency and current-phase boundary

Pillars 1 and 3 can be accepted as a reviewed **native content specification**, not as a functioning Character Origin execution system. Pillar 2 has not completed the mixed-Ancestry and manifestation rules needed to finalize acquisition, authorized inheritance, and Character Sheet behavior. The trusted projection document remains a **proposal** and its accompanying static matrix and source preview do not bypass that dependency. No Origin authorization runtime, stored character schema or character sheet UI is approved by this pass. The optional Competency duplicate mode may be selected and recorded manually for playtesting now.

## Completion conditions for this phase

1. Every playable Species/Ancestry/Upbringing/Background combination has resolvable references and a selection path; nonselectable nodes are only inherited, not direct choices.
2. Every **approved player-facing Origin training choice** has a defined effect, including an explicit decision when it grants neither. Sources without a training benefit receive none.
3. Every conditional Origin choice is mutually exclusive exactly as authored; its equipment qualifications and ordinary starting inventory remain separate.
4. Each existing Origin feature can be adjudicated from the native JSON and established common rules, or a specific outstanding decision is called out instead of inventing behavior.
5. Source-specific and comparative balance exceptions are reviewed without imposing equal budgets or changing unrelated mechanics.
6. Content validation and the relevant Origin tests run successfully on the current branch; unrelated pre-existing suite failures are separated from these changes.

**Next highest-value work:** Finish combined feature-branch content validation, then complete Pillar 2. The [trusted Origin grant-projection contract](pillar3-trusted-origin-grant-projection-contract-20261110.md) and [five-choice conformance matrix](pillar3-authorized-option-cases-20261110.json) are provisional implementation references, not an approved executable character creation contract. The existing `NativeOriginMixedChoicePreviewer` is a non-authoritative preview, not an Origin authorization engine. Revisit the proposed contract after Pillar 2; do not start Character Sheet or Origin executor integration in this pass. Vehicle mechanics remain separate, and the experimental Competency duplicate policy is ready for manual playtesting without assuming balance equivalence.
