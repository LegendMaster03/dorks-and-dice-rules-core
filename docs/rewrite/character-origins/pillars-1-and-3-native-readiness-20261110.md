# Character Origins — Pillars 1 and 3 native readiness and consistency

**Date:** 2026-11-10  
**Branch:** `feature/character-origins-equipment-contract-20261110`  
**Authoritative content:** `data/rulesets/first-party-001/native/`  
**Status:** Reconciliation checklist updated after the approved November 10 Origin decisions and executed PR #138 validation. Native JSON and source-contract tests are reviewed; a Character Origins choice executor and full repository CI certification remain separate.

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
- **Tests kept current:** `test_goliath_upbringings.py` now refers to `species/ancestry/goliath/` and the authoritative `native/skills.json` / `native/competencies.json`. `test_character_origins_cross_pillar_consistency.py` tests identity, ancestry/upbringing parent graphs, default eligibility, canonical references, and absence of retired product-Competency IDs. `test_character_origins_equipment_integration.py` now asserts the six exact canonical High/Wood Elf weapon-type grants and absence of implied mastery. The targeted Origin, Goliath and weapon-training suites have now executed and passed in PR #138 (48/48 tests).

## Remaining Pillar 1/Pillar 3 decisions and fixes

| Priority | Issue | Required content decision or correction | Readiness effect |
| --- | --- | --- | --- |
| **Resolved** | **Five independent native Competency choice sites** | All 11 grant effects explicitly authored: Explorer and Spy **one rank only**; Aristocratic, General Goliath and Street-Raised **proficiency only** | Every existing Competency alternative has an authored amount, with mixed equipment alternatives kept separate |
| **Resolved** | **Guild Artisan shared crafting-method eligibility** | `native/competencies.json` now contains `competency-category.crafting-method` and a canonical member list referenced by `background.guild-artisan` | Guild Artisan chooses one eligible method appropriate to its guild trade and grants **one rank only** |
| **Resolved** | **General Elf and General Gnome inheritance-only** | Both now have `selectable: false`; corresponding base Species defaults are removed, Ancestry defaults remain selectable, and inherited parent benefits are unchanged | One effective Upbringing, no invalid Species suggestion, and no parent-as-second-choice |
| **P1** | **Some specific equipment and inventory choices remain conditional** | **Resolved:** High/Wood Elf exact canonical weapon proficiencies include Longbow; Gladiator now has one canonical base-weapon type choice from the All Weapons pool; Bard's retained one-martial-weapon choice is also typed. **Partially resolved in native JSON:** Gladiator's proficiency and starting-item records now share `equipmentTypeChoiceId: "arena-weapon"` and the item declares ordinary/low-cost/arena-specific requirements. **Partially resolved in native JSON:** Entertainer's performance item now branches on the chosen Disguise/Instrument method and reuses the nested instrument type choice. **Open in runtime:** actual item eligibility, matching materialization, and Land/Water Vehicle operator qualifications still require Origin authorization and domain-specific representation | Typed proficiency choices resolve against the catalog; source-specific contextual eligibility and inventory matching are separate |
| **Resolved in JSON** | **Scoped History proficiency multipliers** | Both features now explicitly replace the normal proficiency contribution with **2×PB on qualifying History checks**, including when History is untrained. Stonecunning's considered-proficient status applies only to that check | Actual Skill/check evaluator integration remains separate |
| **Resolved in JSON** | **Rock Gnome Prestidigitation casting Ability** | Select **Intelligence, Wisdom, or Charisma** for the Upbringing-granted Prestidigitation cantrip | Spell authorization/execution is a later implementation concern |
| **P2** | **General Gnome shared cultural knowledge** | Decide whether its approved-in-principle but unallocated cultural feature exists as a concrete benefit, or whether a language-only General Gnome parent is intentional | Qualitative completeness; does not justify inventing an extra universal Competency |
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
- The original static JSON review is supplemented by **executed GitHub Actions** on PR #138: 48/48 dedicated Origin/Goliath/weapon-training Python tests, 99/99 Mechanics tests, 111/111 Host tests, and the rewrite structure validator passed. The .NET Release build completed with zero warnings or errors. This is source-contract and shared-resolver validation, **not** proof of a Character choice executor or end-to-end Origin authorization.

## Executed validation status

The dedicated Character Origins CI step now runs independently of the repository-wide migration suite. In PR #138 the **35 Character Origins**, **7 Goliath Upbringings**, and **6 weapon-training** tests passed. The .NET build, **99 Mechanics** tests, **111 Host** tests, and rewrite structure validation passed as well.

The pinned source-coverage manifest retains four historical `subspecies.*` identifiers but explicitly links them to corresponding approved `ancestry.*` records. This corrects the legacy path/identity mismatch without resurrecting old Subspecies mechanics or altering the pinned SRD source count. The full Python suite now reports two remaining cross-domain failures, so the full CI workflow is not green and downstream smoke tests have not run.

## Completion conditions for this phase

1. Every playable Species/Ancestry/Upbringing/Background combination has resolvable references and a selection path; nonselectable nodes are only inherited, not direct choices.
2. Every **approved player-facing Origin training choice** has a defined effect, including an explicit decision when it grants neither. Sources without a training benefit receive none.
3. Every conditional Origin choice is mutually exclusive exactly as authored; its equipment qualifications and ordinary starting inventory remain separate.
4. Each existing Origin feature can be adjudicated from the native JSON and established common rules, or a specific outstanding decision is called out instead of inventing behavior.
5. Source-specific and comparative balance exceptions are reviewed without imposing equal budgets or changing unrelated mechanics.
6. Content validation and the relevant Origin tests run successfully on the current branch; unrelated pre-existing suite failures are separated from these changes.

**Next highest-value work:** Develop a reviewed **Origin choice-authority execution contract** for the now-authored Gladiator and Entertainer item/training choice linkages, the other nested mutually exclusive options, and separate vehicle operator qualifications without inventing gameplay allocations. The complete Python suite on PR #138 ran 371 tests and retains **two unrelated or cross-domain failures**: creature inheritance evidence and broad native/working source divergences. Original SRD `subspecies.*` IDs are now preserved with explicit links to four approved `ancestry.*` native records, and the pinned baseline integrity test passes. No Character builder or combat-effect implementation is implied by the source-level pass.
