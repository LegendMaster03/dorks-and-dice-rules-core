# Pillar 3 — Competency grant allocation review

**Date:** 2026-11-10  
**Branch:** `feature/character-origins-equipment-contract-20261110`  
**Ruleset:** `first-party-001`  
**Status:** **Source-specific proposals, not approved numeric Origin grants.** No rank, proficiency, or choice effects are changed by this report.

This review follows the canonical Competency mechanics in `docs/rewrite/competency-baseline-v1.md` and the Origin/qualification integration documented in `pillar3-equipment-integration-20261110.md`. It addresses **Character Origins only**, not general feat/class progression or a new equipment subsystem.

## Agreed rules and conversion expectation

- One Competency has its own independent nonnegative **rank count** and **binary proficiency**. A grant can provide **proficiency + ranks**, **ranks only**, or **proficiency only**.
- The owner's expected relative frequency for source conversion is **(1) proficiency and one rank, (2) one rank only, (3) proficiency only**. This is not a mandated quota for each Origin or a requirement that initial training contain all three patterns.
- Distinct authorized rank-grant **occurrences** add. Duplicate proficiency is Boolean and never generates an additional rank by itself.
- A source grant must be authored independently of whether the recipient already has proficiency. For example, a source granting **proficiency plus one rank** continues to grant **one rank** if the character was already proficient; it does not need to be dynamically converted to a different source grant.
- **Thieves' Tools, Gaming Sets, and Musical Instruments** are narrow equipment qualifications when the authored benefit explicitly grants them; they are not ranked Competencies. A native Competency's `associatedTools` does not create a second training grant.
- Physical inventory and equipment qualifications must not be inferred from `competency-training`. Contextual bonuses and special techniques are separate effects, not implicit additional ranks.

## Source-by-source training candidate matrix

**Working candidate:** `P+1` denotes **Proficient = true, Ranks = 1** for the one actually earned Competency grant occurrence. It is **proposed**, not currently stored as an executable effect. `R` would be one rank only; `P` would be proficiency only. No mandatory distribution among these types has been approved.

| Origin | Authored grant identity and eligibility | Candidate | Source-specific assessment |
| --- | --- | --- | --- |
| **Charlatan Background** | `charlatan-disguise-kit` → Disguise (fixed) | **P+1** | Existing work in creating disguises supports practiced competence. This is the first of **two** fixed learned disciplines; compare their combined value with Criminal's two narrow tool qualifications and other Background features. |
| **Charlatan Background** | `charlatan-forgery` → Forgery (fixed) | **P+1** | Forgery is one native Competency, not an extra Forgery Skill plus tool proficiency. This and Disguise are separate distinct grants. Do not grant a physical Forgery Kit. |
| **Entertainer Background** | `entertainer-performance-method` / `disguise-kit` → Disguise **or** instrument proficiency | **P+1** if Disguise chosen | The alternative instrument result remains **one binary specific-instrument proficiency** with no rank. Preserve one outer option, not both rewards. Consider the resulting choice-value asymmetry. |
| **Explorer Background** | `explorer-field-competency` → Navigation **or** Cartography | **P+1** for one selected Competency | Both are learned expedition techniques, not separate navigator/cartographer equipment proficiencies. One source occurrence, one identity. |
| **Guild Artisan Background** | `guild-artisan-trade` → one reviewed `crafting-competency` | **P+1** for one eligible trade | Sustained trade practice supports initial ranked training. Its generic eligibility pool still needs a canonical source-owned validation rule. The guild's physical tools are inventory resources, not an extra training award. |
| **Guild Merchant Background** | `guild-merchant-professional-choice` / `navigation` → Navigation **or** additional language | **P+1** if Navigation chosen | Preserve the extra-language branch as a distinct outcome. Evaluate the utility of a ranked Navigation grant against a learned language in actual commerce/travel contexts. |
| **Sailor Background** | `sailor-navigation` → Navigation (fixed) | **P+1** | Navigation training is distinct from the separately authored Water Vehicles qualification. Check total value alongside Ship's Passage and starting possessions. |
| **Spy Background** | `spy-tradecraft-competency` → Disguise/Forgery/Calligraphy **or** Thieves' Tools | **P+1** for one selected Competency | Tool alternative is narrow and binary; its usefulness may differ substantially from a ranked tradecraft Competency. One authorized choice only. |
| **Aristocratic Upbringing** | `aristocratic-formative-competency` → Calligraphy/Painting/Cooking **or** one Gaming Set | **P+1** for one selected Competency | One chosen learned practice. The Gaming Set outcome remains a specific equipment proficiency, not a shadow Competency; compare complete conventional and cross-cultural packages. |
| **General Dwarf Upbringing** | `crafting-tradition` → Smithing/Brewing/Stonemasonry | **P+1** for one selected Competency | Cultural trade practice can grant initial training without being restricted to Dwarf Species. Stonecunning is separate and does not add the chosen craft rank a second time. |
| **General Goliath Upbringing** | `clan-craft` → Cooking/Leatherworking/Navigation | **P+1** for one selected Competency | Practical community training, independent of the one separately chosen Skill. A chosen Navigation grant may combine with Sailor or Guild Merchant Background training. |
| **Rock Gnome Upbringing** | `tinkering` → Tinkering (fixed) | **P+1**, high-priority balance review | A coherent learned discipline, but the record also has Artificer's Lore, Clockwork Tinker, Prestidigitation, and Magical Tinkering. Do not equate those features to extra Competency ranks or approve a numerical grant before evaluating the full package. |
| **Street-Raised Upbringing** | `street-raised-community-competency` → Disguise/Cooking/Forgery **or** Thieves' Tools | **P+1** for one selected Competency | Informal formative practice can count as learned proficiency; it need not be a formal credential. A rank-only variant would be a possible deliberate change **only** if the source is intended to confer limited experience without proficiency. No such decision has been made. |

**Interpretation:** All 13 current sites are *initial training* grants, not grants that explicitly advance an existing training track. Therefore **P+1 is a plausible common candidate at each site**, subject to owner review and balance. This is **not** a claim that 13/13 must be adopted, or that rank-only and proficiency-only grants must be absent from the wider system. The latter are particularly useful for separately authored practice, specialized advancement, and narrow credentials, but no new source of those effects is invented in Pillar 3.

## Representative interaction checks

| Complete effective combination | Consequence **if** each P+1 candidate is accepted | What is not implied |
| --- | --- | --- |
| Street-Raised → Forgery + Charlatan | **Forgery rank 2, proficient**, from two distinct authorized rank-grant occurrences; Charlatan also grants Disguise | No second Forgery Kit qualification or ownership |
| General Goliath → Navigation + Sailor | **Navigation rank 2, proficient** from two sources; Sailor still separately grants Water Vehicles | No automatic Cartography or ship ownership |
| General Dwarf → Smithing + Guild Artisan choosing Smithing (if eligible) | **Smithing rank 2, proficient**; Guild Artisan tools may still be inventory possessions | No separate Smith's Tools proficiency requirement inferred merely from the shared Competency |
| Rock Gnome → Tinkering + Guild Artisan selecting Tinkering (if eligible) | **Tinkering rank 2, proficient** from distinct grants | No automatic extra ranks from Clockwork Tinker or Magical Tinkering |
| Aristocratic → Gaming Set + Noble choosing the same Gaming Set | One **binary** equipment proficiency with separate source provenance | No rank, alternate free choice, or compensation without an approved rule |
| Criminal + Rogue | One effective Thieves' Tools proficiency; source provenance from both | No Open Lock Skill proficiency, rank, or replacement training |
| Spy choosing Thieves' Tools | Only the tool proficiency result | No simultaneous selected tradecraft Competency |
| Street-Raised choosing Cooking | Only the native Cooking training result | No simultaneous Thieves' Tools proficiency |

These are **hypothetical consequences** using the accepted independent rank-grant semantics and proposed P+1 values, not results of an executable Character builder.

## Important balance conclusions before adoption

1. **Charlatan's two fixed learned disciplines** would grant two initial Competency ranks and proficiency in both. Criminal instead receives two narrow equipment qualifications. The packages also differ in social features, Skills, and material resources. Do not silently convert Criminal's tools back into ranked Competencies to make the display symmetrical.
2. **Mixed-domain choices need explicit value review.** Entertainer, Spy, Street-Raised, and Aristocratic all juxtapose a possible P+1 Competency with a single narrow binary tool qualification. The alternatives are intentionally different, but we do not yet know whether the differential is acceptable in a starting character.
3. **Duplication has two distinct forms.** Independent Competency rank grants add when earned; duplicate equipment qualifications remain binary. Unavoidable duplicate *Skill* training (for example, Giant Foundling + Soldier Intimidation) is a separate unresolved policy and must not borrow Competency rank-stacking rules automatically.
4. **No standalone Upbringing budget is assumed.** General/Rock Gnome, General Goliath/Giant Foundling, and cultural substitutions must be reviewed as complete Species–Ancestry–Upbringing–Background packages, not by counting one field in isolation.
5. **Missing specifics are real blockers to full execution, not to this content review.** The exact `crafting-competency` membership, the General Gnome value question, Upbringing-granted Prestidigitation ability, scoped doubled proficiency stacking, selected weapon eligibility, and vehicle operator benefits remain separate Origin-domain questions.

## Approval and implementation boundary

No number in the proposed candidate column is yet a rule. The existing `grantType: "competency-training"` retains the original descriptive entitlement. Before emitting `NativeCompetencyTrainingGrant` records, the Origin authority must validate the selected Background/Upbringing and its one-of choices, establish a unique source occurrence, and supply the explicitly reviewed `Ranks` and `Proficient` effect.

The final representation should remain one grant per learned capability, not a Competency container with separately trained internal tool/Skill facets. Do not commit `ranks: 1` to all Origin JSON solely from the frequency guidance. Instead review the candidates with particular attention to **Charlatan, Rock Gnome, and the four mixed-domain choices**, then author and test the actual effects when approved.
