# Character Origins — Pillars 1 and 3 native readiness and consistency

**Date:** 2026-11-10  
**Branch:** `feature/character-origins-equipment-contract-20261110`  
**Authoritative content:** `data/rulesets/first-party-001/native/`  
**Status:** Reconciliation checklist; content **not yet certified usable as a complete Origin rules corpus**. Previously approved grants remain authoritative. This document does not approve new mechanical benefits or selectability changes.

## Intended deliverable

Produce a coherent native JSON specification for:

- **Pillar 1 — Species and Ancestry:** intrinsic traits, playable leaf selection, parent inheritance, default Upbringing *suggestions*, and valid identity references.
- **Pillar 3 — Backgrounds and Upbringings:** learned training and languages, culture, origin features, source choices, ordinary starting possessions, and applicable inherited Upbringing benefits.
- **Interface of Pillars 1 and 3:** a character directly chooses its Species and applicable Ancestry, one effective Upbringing (with inherited parents), and one Background. A Species/Ancestry default is a suggestion, **not** an extra cultural package or a Species restriction. Mixed Ancestry is independent of this deliverable.

**Usable at this phase** means an independently interpretable, internally consistent JSON content corpus capable of supporting manual review/playtest and later engine implementation. It does **not** require that the Character Sheet, C# selection authority, stored character state, or Origin UI already execute these records. This ruleset is still being designed and is not in use; **there is no existing-character Competency rank migration problem**.

Review source-edition mechanics as reference evidence. Do not automatically restore previously omitted grants, add a universal feature/point budget, or treat a historical item-product specialty as an independent current Competency. The retained native method/discipline resolves crafting applicability; product type and recipe are separate.

## Current corpus and foundational consistency

- **Pillar 1:** Nine base Species definitions and 29 Ancestry records, including nonselectable Dragonborn family nodes. Chromatic/Metallic Dragonborn have five selectable leaves each; Gem is currently a deliberately nonselectable, incomplete family, not an available playable leaf.
- **Pillar 3:** Sixteen native Backgrounds and thirteen Upbringings. The Elf General parent is inherited by Drow, High and Wood Elf Upbringings; the Gnome General parent is inherited by Forest and Rock Gnome Upbringings. General Dwarf and General Goliath are currently independent, selectable cultural packages, not inheritance parents.
- **Crafting identities:** 26 native Competencies, with Smithing retained and historical product-specific Bowmaking, Armorsmithing, Weaponsmithing, Shipmaking and Trapmaking retired. Bow manufacture is associated with the appropriate woodworking method/recipe (Carpentry or Woodcarving, for example) rather than a Bowmaking advancement track.
- **Selection policy recovered:** `native/rules/species-subspecies-upbringing.json` now records the already established one-Upbringing/default-as-suggestion/parent-inherited-once contract that existing tests referenced but the branch lacked.
- **Tests kept current:** `test_goliath_upbringings.py` now refers to `species/ancestry/goliath/` and the authoritative `native/skills.json` / `native/competencies.json`. `test_character_origins_cross_pillar_consistency.py` tests identity, ancestry/upbringing parent graphs, default eligibility, canonical references, and absence of retired product-Competency IDs. `test_character_origins_equipment_integration.py` now asserts the six exact canonical High/Wood Elf weapon-type grants and absence of implied mastery. These test definitions still need a local or CI execution.

## Remaining Pillar 1/Pillar 3 decisions and fixes

| Priority | Issue | Required content decision or correction | Readiness effect |
| --- | --- | --- | --- |
| **P0** | **Five independent native Competency choice sites with 11 concrete outcomes unallocated** | Decide effects from **actual Origin experience and full-package balance** for Explorer (Navigation/Cartography), Spy (Disguise/Forgery/Calligraphy), Aristocratic (Calligraphy/Painting/Cooking), General Goliath (Cooking/Leatherworking/Navigation), and Street-Raised (Disguise/Cooking/Forgery). No blanket Background-rank/Upbringing-proficiency rule applies to these new allocations | An option is selectable in JSON, but its effective proficiency/rank result is not specified |
| **P0** | **Guild Artisan `choiceCategory: crafting-competency` has no defined eligibility set** | Publish a source-owned, canonical membership contract or approved explicit list, respecting *method-based* crafts and excluding noncraft qualifications; illustrative examples alone are insufficient | A player cannot determine the complete range of legal trade choices from current authoritative data |
| **P1** | **Parent Upbringings may become inheritance-only** | Decide if General Elf and General Gnome remain standalone cultures or are solely structural parents. If made `selectable:false`, revise the Species `defaultUpbringing` suggestions that currently target them, ensuring the default always points to a valid selectable package. Preserve parent benefits for each child | Prevents presenting an invalid default or accidentally requiring a second selected Upbringing |
| **P1** | **Some specific equipment and inventory choices remain conditional** | **Resolved:** High/Wood Elf exact canonical weapon proficiencies include Longbow; Gladiator now has one canonical base-weapon type choice from the All Weapons pool; Bard's retained one-martial-weapon choice is also typed. **Open:** the Gladiator's mundane/arena-appropriate restriction and matched ordinary starting item require Origin authorization; Land/Water Vehicle qualifications require their distinct operator qualification representation | Typed proficiency choices resolve against the catalog; source-specific contextual eligibility and inventory matching are separate |
| **P1** | **Scoped History proficiency multipliers** | Confirm how General Dwarf Stonecunning and Rock Gnome Artificer's Lore grant/rewrite 2×PB on qualifying checks without broad History training or additive double counting; preserve exact task scope. The +2 Drow/Wood Elf Survival effects are distinct, additive authored effects | Check arithmetic for two existing Upbringing benefits is not self-contained |
| **P1** | **Rock Gnome Prestidigitation casting Ability** | Supply the intended Origin casting-Ability selection/restriction consistently with existing source wording and general spell rules | One authored magical benefit has an explicit unresolved parameter |
| **P2** | **General Gnome shared cultural knowledge** | Decide whether its approved-in-principle but unallocated cultural feature exists as a concrete benefit, or whether a language-only General Gnome parent is intentional | Qualitative completeness; does not justify inventing an extra universal Competency |
| **P2** | **Comparative benefit balance** | Revisit General Dwarf proficiency+rank *only if* total conventional and cross-cultural packages justify it; compare Guild Artisan and Charlatan after method-based rank effects are considered. Do not assume ranks are merely +1 check bonuses | Deliberate, evidence-led exceptions rather than equal-size grant lists |

**Settled boundaries not to reopen during this pass:**

- **Neither Competency proficiency nor a rank is the default for an Origin.** For genuinely retained historical Competency training, Upbringing proficiency and Background one rank are *starting conversion guidelines*, with justified both/neither exceptions. Eight historical grant outcomes are authored; the eleven newly designed outcomes are not.
- **Thieves' Tools proficiency converts directly** to `equipment.tool.thieves-tools` in Criminal and the conditional Spy/Street-Raised options. It is not a ranked Competency or a new debate about its training amount.
- **Folk Hero and Hermit** deliberately do not preserve their historical source tools; **Gladiator** deliberately substitutes an arena weapon qualification. **Entertainer** intentionally grants one mutually exclusive Disguise/Instrument choice. Do not replay discarded 2014 grants.
- **Native Competency progression, recipe timing, rank features and traits** belong to the already established Competency rules, not a new Pillar 1/3 design process. Their internal formulas are out of scope.
- **Parent benefits** are inherited once. The effective Upbringing is chosen once and can be cross-cultural. Species, Ancestry, cultural traits, and occupational experience should not be remerged just to follow an old Race/Background package.
- No actual persisted characters use this experimental content. No migration/compatibility accounting for existing ranks is necessary.

## Completion conditions for this phase

1. Every playable Species/Ancestry/Upbringing/Background combination has resolvable references and a selection path; nonselectable nodes are only inherited, not direct choices.
2. Every **approved player-facing Origin training choice** has a defined effect, including an explicit decision when it grants neither. Sources without a training benefit receive none.
3. Every conditional Origin choice is mutually exclusive exactly as authored; its equipment qualifications and ordinary starting inventory remain separate.
4. Each existing Origin feature can be adjudicated from the native JSON and established common rules, or a specific outstanding decision is called out instead of inventing behavior.
5. Source-specific and comparative balance exceptions are reviewed without imposing equal budgets or changing unrelated mechanics.
6. Content validation and the relevant Origin tests run successfully on the current branch; unrelated pre-existing suite failures are separated from these changes.

**Next highest-value work:** Resolve the five unallocated native choice sites source by source, then the Guild Artisan eligibility set. Parent selectability can be decided in parallel, but should not be changed merely to make hierarchy look simpler. No code/runtime implementation is required for this checkpoint.
