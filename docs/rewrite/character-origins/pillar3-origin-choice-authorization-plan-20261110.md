# Pillar 3 — Origin choice authorization and grant projection plan

**Date:** 2026-11-10  
**Status:** Design/integration plan for future Character Origins execution. **Not an approved new gameplay rule or completed runtime.** No C# choice executor or Character Sheet integration is implemented by this document.

## Authority and scope

The canonical selected sources are **one Background** and **one effective Upbringing** with its authored Upbringing parent chain. Species and Ancestry are selected through their own origin rules; a Species-default Upbringing is a suggestion, **not** an extra automatically inherited package. A character may use an applicable cross-cultural Upbringing. Neither biological heritage nor a named parent mechanically awards that parent's entire cultural package.

The Character/backend selection authority must prove each Origin and each selection **before** projecting grants into native mechanics. Rules Core defines applicable authored rule data and performs validated mechanics; a consumer-requested `sourceId` or option path alone is **not** evidence of entitlement.

No element in this plan depends on the separate mixed-Ancestry combining design.

## Benefit shape inventory

| Existing authored shape | Example | Minimum authorization rule |
| --- | --- | --- |
| Fixed `skill-training` | Criminal Deception / Stealth | Source acquired; target canonical `skill.*` |
| Skill `chooseOne` or `chooseFrom` with `choiceCount` | General Elf one of Insight/Perception/Survival; Spy two distinct intelligence Skills | Exactly the authored number, from the current eligible source list; `distinctChoices` honored |
| Fixed `competency-training` | Charlatan Forgery; Rock Gnome Tinkering; Sailor Navigation | Source acquired; future reviewed **Ranks/Proficient** contribution, no implicit separate tool qualification |
| Competency `chooseOne` | Explorer Navigation/Cartography; General Dwarf craft; General Goliath clan craft | Exactly one authorized Competency target; preserve source occurrence and the selected outcome |
| Semantic `choiceCategory` not yet catalog-backed | Guild Artisan `crafting-competency` | Resolve to a reviewed, explicit eligible set; neither `associatedTools` nor all native Competencies define that set automatically |
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

## Worked source cases (no new gameplay allocations)

**Spy selects Thieves' Tools:** A validated Spy Background and exactly the `thieves-tools` tradecraft option yield the canonical binary `equipment.tool.thieves-tools` qualification. None of Spy's Disguise, Forgery, or Calligraphy alternatives is earned. If Rogue also grants Thieves' Tools, the outcome is one proficiency with independent provenance.

**Street-Raised selects Forgery and Charlatan also grants Forgery:** The Upbringing choice and the fixed Charlatan benefit are two different, earned **Competency** grant occurrences. If later approved allocations give both a rank increment, those increments accumulate. Their proficiency effects, if any, combine as a Boolean. This does not introduce `skill.forgery` or an extra Forgery Kit proficiency.

**Knight selects a flute:** The one-total-choice rule admits one specific `equipment.tool.flute` proficiency from the approved Musical Instrument group. It does not also grant a Gaming Set or the entire Musical Instrument group.

**Aristocratic selects Cooking:** The nested Gaming Set source must not be projected. Only the approved Cooking Competency grant (once its numeric/binary effect is selected) is eligible.

**Guild Merchant selects a second trade language:** Navigation Competency training is not earned from that same choice. The separate baseline trade-language grant remains, subject to rules for distinct language selections.

**General Elf + High Elf:** Inherit exactly the General Elf Upbringing parent benefits alongside the High Elf-specific grants; do not also apply General Gnome, General Dwarf, a second default Upbringing, or unrelated ancestry grants.

## Data and validation prerequisites

- **Choice categories:** The current native Competency catalog includes stable IDs but does not publish a canonical `crafting-competency` eligibility taxonomy. Guild Artisan needs a reviewed mapping, not guessed string matching.
- **Conditional child sources:** The equipment evaluator currently reads authored grants at canonical source root/`definition`. An Origin child option must be made an authorized, inspectable catalog-backed grant source before the same mechanics path can resolve it. This belongs with future Origin selection execution, not an informal client shortcut.
- **Equipment coverage:** The representative catalog lacks Longbow and the full Gladiator eligible arena weapon set; Land/Water Vehicles remain separate domain questions. Preserve source grants while leaving unsupported coverage unresolved.
- **Competency amounts:** The source-by-source options and proposed grant amounts are recorded in `pillar3-competency-grant-review-20261110.md`; none has been applied to native JSON.
- **Duplicate fixed skills:** Giant Foundling + Soldier both grant Intimidation. The system must retain source provenance without assuming a free substitution or automatic extra rank.
- **Testing:** Add eventual tests for partial/malformed mixed choices, stale eligibility categories, duplicate parent inheritance, unselected nested grants, two distinct Skill selections, and broad-versus-specific equipment coverage. The current Python content checks cannot prove these runtime authorization properties.

This plan deliberately stops before implementing a new C# choice executor or changing Origin balance, progression, and class-feature mechanics.
