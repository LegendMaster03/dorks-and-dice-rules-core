# Pillar 3 — Origin choice authorization and grant projection plan

> **Updated source allocation boundary (2026-11-10):** The Origin choice executor must project only grants **already authored and actually earned**. Some converted prior-version Competency benefits now have explicit rank/proficiency effects; the five independently authored native choice sites now carry individually approved rank/proficiency amounts. The Thieves' Tools option itself is a **direct conversion to binary** `equipment.tool.thieves-tools` for Criminal, Spy, and Street-Raised, not an unresolved numeric Competency grant. See [Historical Origin Training Conversion Scope](pillar3-competency-origin-baseline-20261110.md).

**Date:** 2026-11-10  
**Status:** Design/integration plan for future Character Origins execution. **Not an approved new gameplay rule or completed runtime.** An internal C# mixed-option **preview evaluator** now validates catalog-backed candidate effects, but it is not a character choice authority, trusted grant adapter or Character Sheet integration.

## Authority and scope

**Follow-on execution interface:** [Trusted Origin Grant-Projection Contract](pillar3-trusted-origin-grant-projection-contract-20261110.md) describes the proposed server verification boundary, source/option provenance, no-partial-grants behavior and per-domain adapters. Its [mixed-option conformance matrix](pillar3-authorized-option-cases-20261110.json) is a static test reference for the five current option-choice sources, not runtime authority. This plan remains the approved-content integration inventory, not an executable resolver.

The canonical selected sources are **one Background** and **one effective Upbringing** with its authored Upbringing parent chain. Species and Ancestry are selected through their own origin rules; a Species-default Upbringing is a suggestion, **not** an extra automatically inherited package. A character may use an applicable cross-cultural Upbringing. Neither biological heritage nor a named parent mechanically awards that parent's entire cultural package.

The Character/backend selection authority must prove each Origin and each selection **before** projecting grants into native mechanics. Rules Core defines applicable authored rule data and performs validated mechanics; a consumer-requested `sourceId` or option path alone is **not** evidence of entitlement.

No element in this plan depends on the separate mixed-Ancestry combining design.

## Benefit shape inventory

| Existing authored shape | Example | Minimum authorization rule |
| --- | --- | --- |
| Fixed `skill-training` | Criminal Deception / Stealth | Source acquired; target canonical `skill.*` |
| Skill `chooseOne` or `chooseFrom` with `choiceCount` | General Elf one of Insight/Perception/Survival; Spy two distinct intelligence Skills | Exactly the authored number, from the current eligible source list; `distinctChoices` honored |
| Fixed `competency-training` | Charlatan Forgery; Rock Gnome Tinkering; Sailor Navigation | Source acquired; use its **already authored** Ranks/Proficient effect, with no implicit separate tool qualification |
| Competency `chooseOne` | Explorer Navigation/Cartography; General Dwarf craft; General Goliath clan craft | Exactly one authorized Competency target; preserve source occurrence and the selected outcome |
| Catalog-backed `choiceCategoryId` | Guild Artisan `competency-category.crafting-method` | Resolve to the member Competency IDs in the authoritative shared category. A source-specific guild-trade constraint still applies; neither `associatedTools` nor all native Competencies determine eligibility |
| Top-level `equipmentProficiencyGrants.fixed` | Criminal Thieves' Tools | Authored fixed equipment type and acquired source; no item ownership or independent Competency granted |
| Top-level `equipmentProficiencyGrants.choices` | Criminal / Noble / Soldier Gaming Set; Knight Gaming Set **or** Instrument | Select exactly the authored number of distinct **specific equipment types** within approved groups, not a complete group unless explicitly granted |
| `benefit-choice` containing heterogeneous outcomes | Spy; Street-Raised; Entertainer; Aristocratic; Guild Merchant | Authorize one **outer option** first, then authorize only that option's nested Competency / equipment / language result |
| Language grants and source-restricted language choices | Acolyte faith language; Street-Raised community language; Guild Merchant professional choice | Enforce eligibility from the actual Origin source; do not assume language learned biologically or that Common is ineligible |
| Contextual and social features | Street-Raised Urban Navigation; Knight retainers; Hermit Discovery | Authorize source without converting special effects into generic extra ranks or proficiencies |

**Executable equipment contract boundary:** `equipmentProficiencyGrants` at a source root or `definition` is interpretable by the merged native equipment resolver. When it is authored **inside an unselected nested benefit option**, it is **not executable merely by submitting the parent Origin source**. The future authorization layer must materialize an explicitly selected option as a trusted, catalog-backed grant source or use an equivalently validated source projection understood by the evaluator. The resolver must never accept arbitrary client-selected `targetId` values. This catalog-backed source projection detail remains a design dependency; the current nested JSON preserves option semantics but does not satisfy it by itself.

## Five cross-domain benefit-choice records

| Origin | Outer benefit | Authorized alternatives | Additional inner selection |
| --- | --- | --- | --- |
| Entertainer | `entertainer-performance-method` | Disguise Competency **or** one Musical Instrument proficiency | If instrument branch wins, select one native type from instrument group |
| Guild Merchant | `guild-merchant-professional-choice` | Navigation Competency **or** one additional learned trade language | Language chosen subject to commercial-experience constraint |
| Spy | `spy-tradecraft-competency` | Disguise / Forgery / Calligraphy Competency **or** Thieves' Tools proficiency | No additional tool-type choice: Thieves' Tools is a fixed type |
| Aristocratic | `aristocratic-formative-competency` | One Gaming Set proficiency **or** Calligraphy / Painting / Cooking Competency | If gaming branch wins, select one native Gaming Set type |
| Street-Raised | `street-raised-community-competency` | Disguise / Cooking / Forgery Competency **or** Thieves' Tools proficiency | No additional tool-type choice: Thieves' Tools is a fixed type |

There is **one** outer grant entitlement per row, not a separately earned grant for every listed option. The nested equipment selector in Entertainer/Aristocratic does not increase the number of outer alternatives.

## Proposed trusted selection process

1. **Establish character origin sources:** Validate the selected Species and Ancestry, the effective Upbringing and its parent chain, and the independently chosen Background. Keep default-suggestion and actual selection different. Validate that a source can be earned under the current game rules.
2. **Enumerate source occurrence identities:** Use stable, distinct occurrence IDs for each actually earned source/option acquisition. Do not equate similarly named benefits from two Sources or accept an arbitrary client-supplied occurrence as evidence.
3. **Authorize each choice:** Validate the exact authored option ID, selected number, distinctness, and source-specific restrictions. For mixed choices, select the parent outcome before validating any nested type or language option. An unselected option produces **zero** grants.
4. **Project only authorized mechanics:** Skill grants go to the Skill training rules; Competency grants eventually become validated `NativeCompetencyTrainingGrant(SourceId, OccurrenceId, CompetencyId, Ranks, Proficient)`; equipment grants become authorized native equipment source occurrences using catalog-backed authored targets. Language knowledge is tracked independently from all three.
5. **Combine actual grants:** Rank increments from distinct valid occurrences add. Competency proficiency is Boolean; equipment qualifications remain Boolean. A repeated proficiency is not a rank grant. Overlapping broad/specific weapon qualifications preserve source provenance with a single effective coverage result.
6. **Resolve inventory separately:** Starting possessions are ordinary inventory grants. Owning Thieves' Tools, a gaming set, an instrument, a weapon, or a vehicle does not itself establish proficiency. Where the selected type must match inventory (Gladiator), the source's two linked choices must be checked together.
7. **Fail closed on incomplete authorization:** Unknown choice IDs, selected options outside the actual source, insufficient/multiple picks, missing catalog types, invalid duplicate occurrence IDs, or unsupported conditional state must not silently produce partially earned proficiencies. Do not infer a replacement for an unusable selected grant.

An outer Origin benefit can have multiple mechanical consequences only if explicitly authored as such; a mixed `benefit-choice` is not that authorization. Feature-specific checks and contextual modifiers remain separate from these grant projections.

## Gladiator weapon-selection and starting-item binding

The native Gladiator Background now gives the proficiency benefit `gladiator-arena-weapon` and its **first** ordinary starting-item entry the same `equipmentTypeChoiceId: "arena-weapon"`. This points to the one canonical `equipmentProficiencyGrants.choices` record with `count: 1`, `selectionKind: "equipment-type"`, and `fromGroupIds: ["equipment.group.weapon.all"]`. The All Weapons group is only the *eligible base-type pool*. It is not a proficiency grant for a group, weapon family, or magic item.

The item's `itemRequirements` records `sameBaseWeaponTypeAsChoice`, `mundane`, and `inexpensive`. The written arena-suitability condition is judged by the DM, not automated eligibility logic. These requirements are ordinary equipment/identity constraints, not independent proficiency grants. No canonical item ID is invented here.

A future executor must resolve the whole source-owned choice together:

1. Authenticate that the character earned `background.gladiator` and obtain one stable choice occurrence for `arena-weapon`.
2. Resolve exactly one **existing canonical base weapon type** eligible under `equipment.group.weapon.all`; do not accept group IDs, arbitrary item identifiers, or a magical variant as the selection.
3. The DM determines whether the chosen type suits the character's arena discipline under the written rule. The selector does not implement an arena-weapon allowlist or algorithmic arena-suitability validation.
4. Resolve one ordinary item definition/instance linked to that **same** base type and verify that it is **mundane** and **inexpensive**, under the adopted item/equipment rules. The current catalog is intentionally not sufficient to perform those checks.
5. Apply the single selected weapon proficiency and materialize its matching ordinary starting item only after the canonical type and ordinary item requirements resolve. Do not generate an unrelated weapon or broader proficiency when the type or item is invalid. DM arena judgment does not require machine verification.
6. Keep weapon Mastery independent: this Background provides no `weaponMasteryGrants`. The selected item alone never grants mastery or proficiency.

**Boundary:** The new metadata is a source-level linkage, not an executable runtime binding or confirmation that every current native type can be bought as an arena weapon. The historical Net/Trident examples remain unreconciled. The Entertainer's performance-item choice has an analogous item/training relationship, but its *Disguise versus Instrument* branches differ; future authorization must preserve that mutual exclusivity and cannot inherit Gladiator's one-weapon constraint.

## Entertainer performance method and ordinary item binding

The approved `entertainer-performance-method` is one outer `benefit-choice`, **not** two parallel grants. Its single starting `Performance item` now declares `sourceBenefitChoiceId: "entertainer-performance-method"` and two `conditionalItemChoices` keyed by the same `whenOptionId` values as the authored training alternatives:

| Selected outer option | Actual training | Permitted ordinary starting item | Additional validation |
| --- | --- | --- | --- |
| `disguise-kit` | One rank in `competency.disguise`, **not** a separate Disguise Kit proficiency | A physical Disguise Kit or mundane performance prop | One ordinary item; no extra tool training from receiving it |
| `musical-instrument` | One specific type of Musical Instrument proficiency | The instrument of the **same selected type** | Its `equipmentTypeChoiceId: "entertainer-musical-instrument"` must match the nested equipment qualification choice; ordinary, nonmagical item |

The instrument branch's inner `equipmentProficiencyGrants.choices` is applicable **only when** the outer `musical-instrument` option is chosen. Neither its raw nested grant metadata nor its matching physical item can be treated as earned from `background.entertainer` alone. An unselected option yields no training and no matching-item rights. The Display `options` text on the starting item is explanatory; `conditionalItemChoices` is the structural record of branch correspondence.

The future Origin authority must authenticate the Background, validate one outer choice, validate a single eligible musical-instrument base type when applicable, resolve an ordinary physical item permitted by that selected branch, and **commit the whole selection consistently**. The current native equipment resolver can check proficiency in an explicitly selected canonical instrument type, but can not authorize nested Origin choices or materialize inventory. These JSON fields are **not** a claim of implemented runtime selection.

## Vehicle operation qualifications: explicit boundary

Soldier's `soldier-land-vehicles` and Sailor's `sailor-water-vehicles` already have independent source-authored `qualification-training` benefits with `qualificationCategory: "vehicle"` and `vehicleType: "land"` or `"water"`. These are **operator qualifications**, neither item ownership nor Navigator's Tools/Navigation Competency nor tool proficiency. They are not interchangeable; receiving one does not grant the other.

The shared `equipment-qualifications.json` currently supports the four equipment categories `weapon`, `armor`, `shield`, and `tool`, and its two-bit mask includes Weapon Mastery only within that equipment-training contract. Do **not** silently introduce `equipmentCategory: "vehicle"`, turn these into tool IDs, or pass the existing vehicle-benefit objects to `NativeEquipmentQualifications` as if it supported them. Likewise, owning a cart, ship or riding animal does not confer operation training.

**Next implementation decision (not made here):** Either create a separately approved vehicle-operation qualification contract with canonical land/water scopes and provenance, or explicitly extend the equipment taxonomy and resolver with reviewed vehicle semantics and no Weapon Mastery implication. A future Character/Origin executor must preserve the existing source meanings and ensure duplicated valid vehicle training remains one entitlement with retained source provenance. Until that design is approved, the source benefits remain authored but **not executable** through the current equipment resolver. No additional Soldier/Sailor benefits are awarded by this note.

## Worked source cases (no new gameplay allocations)

**Spy selects Thieves' Tools:** A validated Spy Background and exactly the `thieves-tools` tradecraft option yield the canonical binary `equipment.tool.thieves-tools` qualification. None of Spy's Disguise, Forgery, or Calligraphy alternatives is earned. If Rogue independently grants Thieves' Tools too, the character keeps both source occurrences and may choose an authorized replacement proficiency of the same tool kind under the approved duplicate rule.

**Street-Raised selects Forgery and Charlatan also grants Forgery:** The Upbringing choice and the fixed Charlatan benefit are two different, earned **Competency** grant occurrences. The Street-Raised choice now grants proficiency only, while Charlatan's fixed Forgery grants one rank only. Together, the character becomes proficient in Forgery and gains one rank; overlapping proficiency is Boolean and ranks accumulate only from actually authored distinct rank grants. This does not introduce `skill.forgery` or an extra Forgery Kit proficiency.

**Knight selects a flute:** The one-total-choice rule admits one specific `equipment.tool.flute` proficiency from the approved Musical Instrument group. It does not also grant a Gaming Set or the entire Musical Instrument group.

**Aristocratic selects Cooking:** The nested Gaming Set source must not be projected. Only the approved Cooking Competency proficiency (zero ranks) is eligible.

**Guild Merchant selects a second trade language:** Navigation Competency training is not earned from that same choice. The separate baseline trade-language grant remains, subject to rules for distinct language selections.

**General Elf + High Elf:** Inherit exactly the General Elf Upbringing parent benefits alongside the High Elf-specific grants; do not also apply General Gnome, General Dwarf, a second default Upbringing, or unrelated ancestry grants.

## Data and validation prerequisites

- **Choice categories:** The native Competency catalog publishes `competency-category.crafting-method` with an explicit member list; Guild Artisan references it through `choiceCategoryId`. The remaining Origin authority must validate the chosen method against the actual guild trade, not derive it by display-name matching.
- **Conditional child sources:** The equipment evaluator currently reads authored grants at canonical source root/`definition`. An Origin child option must be made an authorized, inspectable catalog-backed grant source before the same mechanics path can resolve it. This belongs with future Origin selection execution, not an informal client shortcut.
- **Equipment coverage:** Longbow is now a canonical type and High/Wood Elf grants resolve. Gladiator has a typed one-weapon selection whose chosen base type also identifies an ordinary starting item; the DM judges arena suitability without software enforcement. Net and Trident remain unavailable in the native catalog until their base types are reconciled. Land/Water Vehicle operator mechanics remain a separate system outside this content phase.
- **Competency amounts:** Eight historical conversion effects and eleven independently designed Competency outcome effects are now explicitly authored in the native JSON. The five newer choice sites were individually approved: Explorer/Spy one rank only, Aristocratic/General Goliath/Street-Raised proficiency only. The earlier P+1-for-all candidate matrix remains superseded; see `pillar3-competency-origin-baseline-20261110.md`.
- **Duplicate fixed Skills:** Giant Foundling + Soldier both grant Intimidation. Preserve both grant sources and use an authorized same-kind alternative Skill proficiency for the duplicate; do not grant an extra rank.
- **Testing:** `tests/test_character_origins_authorized_option_contract.py` now checks all five current nested choices (sixteen alternative effects) against the native corpus, inheritance graph behavior and starting-item linkage. Still add **runtime** tests for forged source/occurrence, partial/malformed selections, stale eligibility categories, duplicate parent application, unselected nested grants, two distinct Skill selections, and broad-versus-specific equipment coverage when authorization is implemented. The new internal C# preview tests verify source/option lookup and fail-closed candidate validation, but neither these tests nor static Python checks prove a runtime trust boundary.

This plan deliberately stops before implementing a new C# choice executor or changing Origin balance, progression, and class-feature mechanics.

**Pillar 2 implementation gate:** The proposed trusted selection contract is reference material only. Full Character Origins execution, inventory authorization, and Character Sheet integration are deferred until Pillar 2 resolves mixed Ancestry and inherited-trait semantics. Do not interpret the existing non-authoritative choice preview as that integration.
