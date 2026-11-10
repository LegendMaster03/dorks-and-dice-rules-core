# Pillar 3 — source-by-source Competency grant review

**Date:** 2026-11-10  
**Working branch:** `feature/character-origins-equipment-contract-20261110`  
**Status:** **Recommendations for owner review only.** No rank/proficiency values in Origin JSON have been changed. No universal conversion of imported source tool proficiencies is authorized.

## Constraints and rationale

A native Competency is **one independently trainable capability**, with `Ranks` and `Proficient` as independent state. It does not contain a separate advancing Skill and a shadow tool qualification. Actual grant occurrences aggregate valid rank increments; proficiency combines as a Boolean. Thieves' Tools, individual Gaming Sets, and individual Musical Instruments are rare **binary equipment qualifications** and are **excluded** from the ranked Competency matrix below.

The owner's expected relative conversion frequency is, in order: **proficiency + one rank**; **one rank only**; **proficiency only**. This is an expected outcome distribution, not evidence that any specific source must take the first category. The 13 selected Origin benefits below are the exact records in the current corpus with an explicit or selected native `competency.*` target. The proposed distribution happens to be **7 / 4 / 2**; it is **not** an approval or a required ratio.

Notation: **P+1** = `Proficient: true, Ranks: 1`; **R+1** = `Proficient: false, Ranks: 1`; **P** = `Proficient: true, Ranks: 0`. Values describe **per-source grant contributions**, not absolute character totals. A rank-only grant does not take away proficiency supplied by another source. Neither ranks nor proficiency include possession of tools.

## Proposed Background allocations

| Origin benefit | Target(s), one per approved choice where applicable | Proposal | Source-specific reasoning | Review risk |
| --- | --- | --- | --- | --- |
| `charlatan-disguise-kit` | `competency.disguise` | **P+1** | Fixed learned technique from a Background explicitly built around deception and changing appearance | Together with Forgery, Charlatan gets two learned Competencies; compare whole Background including False Identity |
| `charlatan-forgery` | `competency.forgery` | **P+1** | Fixed learned technique practiced as part of the character's deception work | Does not imply an additional `skill.forgery` or Forgery Kit proficiency |
| Entertainer → `entertainer-performance-method` → `disguise-kit` | `competency.disguise` | **P** | A narrower methodological qualification competes with **one Musical Instrument type** in an explicitly exclusive choice; the Background already grants Performance and a Skill choice | The narrower conversion is primarily an alternative-value judgment, not proven from training duration. Compare use frequency before approval |
| `explorer-field-competency` | **one of** `competency.navigation`, `competency.cartography` | **P+1** | Expedition Survey assumes practiced field methods; chosen technical method is a central learned capability | Not a second vehicle qualification or proficiency with every mapmaking task |
| `guild-artisan-trade` | **one approved** native crafting Competency | **P+1** | Sustained professional craft and guild membership are stronger evidence of an established practiced discipline than mere familiarity | Eligibility pool is not yet a canonical catalog; do not infer all 26 Competencies are valid crafting trades |
| Guild Merchant → `guild-merchant-professional-choice` → `navigation` | `competency.navigation` | **P** | Narrow optional Navigator's Tools background alternative to **one extra trade language**; no separate occupational Navigation requirement | A traveling merchant with sustained navigation practice may justify P+1; source wording alone does not settle degree |
| `sailor-navigation` | `competency.navigation` | **P+1** | Explicit practical shipboard experience and ordinary maritime navigation | Separate Water Vehicles qualification remains unresolved; no automatic Cartography |
| Spy → `spy-tradecraft-competency` → `disguise`, `forgery`, or `calligraphy` | **one of** three native Competencies; fourth alternative is Thieves' Tools equipment qualification | **P+1** for a selected Competency | Tradecraft represents actual operational practice, rather than a broad tool license. The option scope is essential | P+1 may substantially exceed value of binary Thieves' Tools; review mixed-choice balance before approving the magnitude |

## Proposed Upbringing allocations

| Origin benefit | Target(s), one per approved choice where applicable | Proposal | Source-specific reasoning | Review risk |
| --- | --- | --- | --- | --- |
| Aristocratic → `aristocratic-formative-competency` → `calligraphy`, `painting`, or `cooking` | **one** of those three Competencies; the fourth choice is one Gaming Set equipment qualification | **R+1** for a selected Competency | Formative exposure to household/cultural practices may award practice without formal proficiency; keeps learned upbringing training distinct from a professional Background | An upbringing with extensive formal arts education could justify P+1; compare with the Gaming Set option |
| `crafting-tradition` (General Dwarf) | **one** of Smithing, Brewing, Stonemasonry | **R+1** | Formative craft experience can add a rank that later combines with occupational training from Guild Artisan or other sources | Some cultural settings may regard the training as full proficiency; inspect complete Species–Ancestry–Upbringing package before approving |
| `clan-craft` (General Goliath) | **one** of Cooking, Leatherworking, Navigation | **R+1** | Chosen community craft is a learned familiar practice, not necessarily sustained professional qualification | Chosen Navigation should not imply a Land/Water Vehicles license; compare against Giant Foundling substitution |
| `tinkering` (Rock Gnome) | `competency.tinkering` | **P+1** | Defined intensive tinkering tradition with separately authorized Clockwork Tinker and Magical Tinkering features | This Upbringing also grants spell/technique utility. Check actual overlap and feature costs; no free rank from the associated Tinker's Tools item |
| Street-Raised → `street-raised-community-competency` → `disguise`, `cooking`, or `forgery` | **one** of three native Competencies; fourth choice is Thieves' Tools equipment qualification | **R+1** for a selected Competency | Community exposure and practice without necessarily verified professional qualification | A rank-only method versus a binary tool qualification is not automatically equal value; test useful tasks and overlap with Criminal, Spy, Charlatan |

The proposed allocations **do not** change approved choice counts or grant alternatives. The broad Skill grants in Backgrounds/Upbringings are separate and are not silently treated as Competency rank grants.

## Consequences and representative overlap tests

1. **General Dwarf + Guild Artisan, both Smithing:** Under the *proposed* mapping, Upbringing contributes **R+1**; Background contributes **P+1**. The effective character state is **Smithing 2 ranks, proficient**, provided the selected guild trade is actually eligible. This is cumulative specialization rather than compensation for duplicate proficiency.
2. **General Goliath + Guild Artisan, both Leatherworking:** Likewise **2 ranks, proficient**, if Leatherworking is eligible for the guild's profession. The two grants must retain distinct earned-source occurrences.
3. **Street-Raised (Forgery) + Charlatan:** Under these proposals, Forgery becomes **2 ranks, proficient** and Charlatan also grants its distinct Disguise training. Neither Forgery Kit ownership nor a separate historical Forgery Skill is implied.
4. **Spy (Thieves' Tools) + Rogue:** The item-tool qualification is binary; two earned occurrences produce **one effective proficiency**, with both source identities retained. There is **no** invented Competency rank.
5. **Criminal + Rogue:** Criminal's fixed Thieves' Tools likewise overlaps Rogue. Whether a fixed duplicate warrants an optional replacement is still undecided; do **not** grant one automatically.
6. **Spy selected Calligraphy versus Spy selected Thieves' Tools:** P+1 provides a general advancing Competency; the tool option is narrower but can be important for equipment-dependent actions. No universal conversion equivalence or bonus is approved.
7. **Aristocratic + Noble:** One Gaming Set chosen by both packages duplicates the *equipment* qualification, while choosing an artistic Competency is a different benefit. Language and Skill collisions remain independent issues.

For every example, the authoritative Origin-choice layer must first validate a character's Species/Ancestry/Upbringing/Background selections, the one-of alternatives, and the actual source occurrences. The current `NativeCompetencyTraining.CombineValidated` reducer only combines already-authorized grants and rejects duplicate occurrence IDs. The native equipment qualification evaluator also does not adjudicate Origin ownership.

## Important unresolved design points

- **Proficiency-only versus rank-only selection:** The proposed assignments are not forced by historical tool labels. If the owner's intended meaning of formative practice implies learned full proficiency, the Upbringing R+1 entries need revision. If rank-only is supposed to represent an *additional increase* rather than an initial character capability, a first-level rank-only grant may be inappropriate. The approved mechanical baseline permits independent ranks without proficiency, but source semantics must still justify it.
- **Mixed outcome fairness:** The proposals for Entertainer and Guild Merchant use P (the least common category) partly because their alternative is a narrow instrument or language grant. Alternative-value parity is a design inference, not confirmed mechanical equivalence.
- **No fixed rank caps or proficiency thresholds:** Journeyman, Expert, Master, rank cap, rank unlocks, proficiency bonus scaling, and later advancement economy remain undeclared. P+1 here does not imply a promotion title or extra technique.
- **Crafting eligibility:** `choiceCategory: crafting-competency` has no authoritative machine-readable category among the 26 native Competencies; the sample `exampleCompetencies` is not an exhaustive grant pool. This is a concrete Origin-choice validation blocker.
- **Conditional feature effects:** Rock Gnome's Artificer's Lore scoped multiplier, Dwarf Stonecunning, Drow/Wood Elf Survival bonuses, and profession/travel features should remain separate from a generic training grant. No universal stacking or compensation rules were added.
- **Starting equipment and cross-cultural substitution:** Evaluate final comparisons using whole Species–Ancestry–Upbringing–Background packages, not one displayed benefit count. Guild Merchant's mule/cart, Knight's retainers, background institutional access, and conditional survival/travel benefits influence relative value.

## Decision boundary and next safe step

These **13 recommendations** form a coherent first owner-review matrix with the expected prevalence ordering. They should not be bulk-applied until the owner has assessed at least (a) professional/background versus formative/upbringing training, (b) rank-only initial training, and (c) mixed-choice Competency versus equipment/language alternative value.

After approval, use a single typed Origin training-grant authoring contract that preserves the existing `benefits` and one-of option identity, and validates the selected source before producing `NativeCompetencyTrainingGrant(SourceId, OccurrenceId, CompetencyId, Ranks, Proficient)`. Do not fabricate an Origin grant from a mere string match or treat the client's requested `SourceId` as entitlement.
