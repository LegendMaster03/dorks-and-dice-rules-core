# Pillar 3 — Character Origins / Competency Contract Audit

> **Status update (2026-11-10):** This document records the initial November 9 competency audit. Its references to three unresolved `competency.thieves-tools` identities and the unimplemented equipment qualification contract describe that earlier state. Those dependencies have since been resolved or typed in Origin JSON after PR #135. See [Pillar 3 Equipment Integration — November 10](pillar3-equipment-integration-20261110.md) for the authoritative current status, conditional choice limitations, validation results, and remaining decisions.

**Audit date:** 2026-11-09  
**Working branch:** `feature/character-origins-competency-audit-20261109`, based on `feature/character-origins-pillar3-life-experiences`  
**Reference contract:** `main` versions of `docs/rewrite/competency-baseline-v1.md`, `docs/rewrite/native-seed-skills-and-competencies.md`, `docs/rewrite/competency-relationship-review.md`, and native `skills.json` / `competencies.json`.

**Status:** First compatibility audit and limited identity corrections. This is **not** a new approved Origin-grant budget, equipment qualification schema, rank allocation, balance sign-off, or executable Character Origins integration.

## Source boundary and branch condition

- On inspection, the Character Origins branch was **109 commits ahead of and 146 behind** `main`. Its corpus contains **16 Backgrounds and 13 Upbringings**; the three most recent general Upbringings include Aristocratic, Street-Raised, and Wilderness-Raised.
- `main` currently has **81** canonical native Skills and **26** native Competencies. The earlier corpus report was written against **88 Skills / 33 Competencies**. Its training-facet descriptions are historical analysis, not current semantics.
- Most Background files exist only on the Character Origins branch. The working branch was created from that branch, not from `main`. **It has not yet absorbed the Competency reconciliation commits.** All identity checks reported here intentionally compare the Origins records to the current `main` catalogs. Do not claim branch-local integration tests passed.
- One effective Upbringing is selected by default and inherits its Upbringing parent benefits. A Species default is a suggestion, not a second package. Direct Species/Ancestry selection remains a supported path. Mixed Ancestry is outside this audit.
- Existing Background features, resource packages, role-neutral character-history text, and approved special cases are preserved.

## Canonical relationship and conversion rules

| Historical representation | Current native model | Disposition |
| --- | --- | --- |
| `competency.disguise-kit` | `competency.disguise`; a single learned capability with Disguise Kit as associated equipment | All four observed occurrences were normalized (Charlatan, Entertainer, Spy, Street-Raised). Exact grant amount remains pending. |
| `competency.thieves-tools` | Binary, nonleveling **equipment qualification**. `skill.open-lock` remains independent. | **Three incompatible occurrences remain**, in Criminal, Spy, and Street-Raised; no tool-specific grant field contract was authorized in this pass. |
| `competency.blacksmithing` | No independent Blacksmithing track; `competency.smithing` is the relevant native metalworking capability | Removed the obsolete Blacksmithing example from Guild Artisan's sample pool. No automatic historical rank transfer. |
| Forgery as a historical Skill plus Forgery Kit | One `competency.forgery`; no native `skill.forgery` | Corrected Charlatan's description to remove the obsolete split. |
| Gaming Set or Musical Instrument trained as `competency-training` | One specific equipment/tool qualification by type, unless separately designed as a learned method | **Requires category/choice contract**; not in the 26 native Competencies. |
| Navigator's Tools, Smith's Tools, Tinker's Tools and other `associatedTools` | Equipment association of a single native Competency | Does **not** automatically add a second equipment proficiency or an inventory item. |
| Specific weapon, Land Vehicles, Water Vehicles | Equipment qualification distinct from Skills or Competencies | Existing qualification fields are candidates; canonical item/category matching and duplicate scopes need verification. |

The current Competency training state has **independent nonnegative ranks** and **binary proficiency**. Valid distinct rank grants accumulate, while repeated proficiency-only grants do not create ranks. Per the owner's subsequent clarification, the expected frequency of Competency conversions is **proficiency + 1 rank**, then **1 rank**, then **proficiency only**, in that order; this is a review heuristic rather than a source-by-source allocation. Its ordinary check projection adds the applicable Ability modifier, ranks, one normal Proficiency Bonus if proficient, and explicit contextual modifiers. This numerical projection remains provisional for balance purposes.

## Background discrepancy inventory (all 16)

Legend: **Corrected** = clear data identity or description only; **Pending** = requires rules or encoding decision; **No catalog issue** = no invalid Skills/Competencies seen, not an end-to-end test.

| Record | Existing grants / issue | Result and next action |
| --- | --- | --- |
| **Acolyte** | Insight, Religion, faith-associated language; no Competency | **No catalog issue.** Language choice is implicit `extraLanguages` with contextual constraint, not an explicit typed entitlement. |
| **Charlatan** | Deception, Sleight of Hand; Disguise Kit and Forgery | **Corrected:** `competency.disguise` and Forgery wording. **Pending:** how much Competency proficiency/rank each training benefit grants. Neither gives a second tool proficiency by association. |
| **Criminal** | Deception, Stealth; `competency.thieves-tools`; one Gaming Set | **Broken reference:** Thieves' Tools must be a tool qualification, not a Competency. **Pending:** Gaming Set typed tool choice. Fixed Thieves' Tools overlaps Rogue. |
| **Entertainer** | Performance + one chosen Skill; Disguise Kit **or** one Musical Instrument | **Corrected:** Disguise is `competency.disguise`. **Pending:** Musical Instrument option is equipment qualification, not `competency-training`; preserve exactly one choice. |
| **Explorer** | Perception + one chosen Skill; Navigation **or** Cartography | **Canonical IDs valid.** **Pending:** independent rank/proficiency effect of the chosen Competency. No automatic vehicle training. |
| **Folk Hero** | Two distinct selected Skills; Community Refuge | **No Competency catalog issue.** No artificial tool/Competency grant should be added for symmetry. |
| **Gladiator** | Performance + arena Skill; one specific mundane arena-appropriate weapon qualification | **Preserve approved exception.** Match specific weapon type against equipment identity and starting item; compare duplication against Class weapon category qualification. |
| **Guild Artisan** | Insight, Persuasion; one trade Competency; trade language | **Corrected:** obsolete Blacksmithing example and outdated “facets” wording. **Pending:** eligible `crafting-competency` pool, grant effect, and relation of guild craft tools to ordinary inventory. |
| **Guild Merchant** | Persuasion + chosen commercial Skill; language; Navigation **or** second language | **Corrected:** Navigation text no longer implies separate Navigator's Tools proficiency. **Pending:** Navigation rank/proficiency effect. Preserve one-of choice and mule/cart inventory. |
| **Hermit** | Two distinct chosen Skills; Discovery | **No Competency catalog issue.** Discovery's one-time information effect should not be treated as an always-on grant of checks. |
| **Knight** | Persuasion + one service Skill; one Gaming Set **or** Musical Instrument; language | **Pending:** replace `competency-training` category with one type-specific equipment qualification without granting two options. Preserve three noncombat retainers. |
| **Noble** | History, Persuasion; one Gaming Set; language | **Pending:** Gaming Set is equipment qualification rather than native Competency. Retain Position of Privilege and starting resources. |
| **Sage** | Arcana, History; scholarly language | **No Competency catalog issue.** No artificial tool allocation. |
| **Sailor** | Athletics, Perception; Navigation and Water Vehicles | **Corrected:** Navigation wording removes a second tool qualification implication. **Pending:** Navigation rank/proficiency and water-vehicle matching. The two benefits must remain distinct. |
| **Soldier** | Athletics, Intimidation; one Gaming Set and Land Vehicles | **Pending:** Gaming Set category is a specific tool qualification; Land Vehicles is a distinct vehicle qualification. Fixed Intimidation can overlap Giant Foundling. |
| **Spy** | Two of six Skills; one of Disguise, Forgery, Thieves' Tools, Calligraphy; language | **Corrected:** Disguise identity. **Broken choice contract:** `competency.thieves-tools` remains inside a `competency-training` `chooseOne`. Replace with one heterogeneous benefit choice containing three Competency alternatives and one tool qualification, subject to grant schema approval. |

## Upbringing discrepancy inventory (all 13)

| Record | Existing grants / issue | Result and next action |
| --- | --- | --- |
| **Aristocratic** | Language; one selected Skill; Gaming Set **or** Calligraphy, Painting, Cooking; protocol | **Corrected:** descriptions no longer claim old training facets. **Pending:** Gaming Set qualification and exact chosen Competency grant. Selection count is still provisional. |
| **General Dwarf** | Dwarvish; Stonecunning scoped History multiplier; Smithing/Brewing/Stonemasonry choice | **Corrected:** `skill.history` canonical ID and explicit one-of `competency-training` category. **Pending:** training amount and contextual multiplier stacking. |
| **General Elf** | Elvish; Insight/Perception/Survival choice | **Corrected:** three canonical `skill.*` IDs, `skill-training` and one-choice cardinality. |
| **Drow Elf** | Inherits General Elf; Undercommon; underground Survival +2 | **Corrected:** `skill.survival`. **Pending:** independent contextual bonus stacking. Activity must qualify, not just location. |
| **High Elf** | Inherits General Elf; four weapon proficiencies; one extra language | **IDs are not Competency issues.** Specific-weapon qualification coverage and duplicate broad-martial proficiency need policy; do not award four extra ranks. |
| **Wood Elf** | Inherits General Elf; two bows; contextual forest Survival +2 | **Corrected:** `skill.survival`. **Pending:** broad weapon overlap and contextual-bonus stacking. |
| **Giant Foundling** | Giant plus extra language; Intimidation and Survival | **No catalog issue.** Fixed Intimidation duplication with Soldier is a separate skill-overlap decision. |
| **General Gnome** | Gnomish only | **No invalid reference.** Relative value requires complete conventional package comparison; do not add arbitrary training simply to equalize counts. |
| **Forest Gnome** | Inherits General Gnome; Nature/Survival/Animal Handling choice | **Corrected:** canonical `skill.*` IDs, `skill-training` and one-choice cardinality. |
| **Rock Gnome** | Inherits General Gnome; Tinkering, scoped History multiplier, Clockwork Tinker, Prestidigitation, Magical Tinkering | **Corrected:** `skill.history`, explicit `competency-training`, and single-profile Tinkering wording. **Pending:** training amount; clarify separate device, spell, and technique effects and their interaction. |
| **General Goliath** | Gol-Kaa, one of three Skills, one of Cooking/Leatherworking/Navigation | **Canonical IDs valid.** **Pending:** Competency rank/proficiency effect and qualification-versus-training boundaries. |
| **Street-Raised** | Language; one chosen Skill; Disguise, Thieves' Tools, Cooking, or Forgery; urban route effect | **Corrected:** Disguise identity and single-profile wording. **Broken choice contract:** `competency.thieves-tools` in an otherwise Competency-only `chooseOne`. Retain exactly one choice but give Thieves' Tools its proper qualification type. |
| **Wilderness-Raised** | Community language; one chosen Skill; conditional provisions for six people | **No catalog issue.** Compare actual resource access and provisioning circumstances rather than treating this as Skill ranks. |

## Exact unresolved references after identity-only corrections

The first-party `main` Competency and Skill catalogs contain 26 + 81 identities. Parsing all **29** working-branch Origin JSON records and recursively checking every `skill.*` / `competency.*` value against those catalogs left precisely:

1. `backgrounds/criminal.json` → `criminal-thieves-tools.competency = competency.thieves-tools`.
2. `backgrounds/spy.json` → `spy-tradecraft-competency.chooseOne[2] = competency.thieves-tools`.
3. `upbringings/street-raised.json` → `street-raised-community-competency.chooseOne[1] = competency.thieves-tools`.

There are no other invalid values matching these canonical identity prefixes in the scanned 29 Origin records. This check does not validate free-form equipment/item identifiers, language eligibility, selection authorization, contextual triggers, or the older native catalogs currently present on the unintegrated development branch.

## Deferred dependency — shared equipment proficiency

**Status:** Await separate equipment-proficiency reconciliation; do not block further Pillar 3 Background/Upbringing content and balance review.

The shared equipment proficiency framework is **architecturally approved but its native grant schema and evaluation are not yet complete**. The responsible follow-up work will address weapons, armor, shields, and comparatively rare equipment/tool qualifications. Most traditional crafting tools are represented through a single ranked Competency instead; its `associatedTools` does not automatically create or require a second tool proficiency. The independent tool-qualification category remains relevant for Thieves' Tools, Gaming Sets, Musical Instruments, and explicit exceptions. Vehicle qualifications require scoped review rather than assumed inclusion.

For now, **preserve the intended benefits and choice cardinality** and mark unresolved equipment grants for later reconciliation instead of fabricating `competency.*` identities, numeric ranks, or an ad hoc qualification schema. The three legacy `competency.thieves-tools` references (Criminal; Spy; Street-Raised), existing gaming-set/instrument choices, qualified weapon/vehicle selections, and broad-vs-specific proficiency overlaps remain tracked in this audit. When the shared contract is available, convert these in one targeted pass with a canonical reference and entitlement check.

This temporary deferral does **not** postpone Competency grant review: the owner's expected relative frequency for native Competency grants is **proficiency + one rank**, then **one rank only**, then **proficiency only**, assigned source by source. These grant types do not apply to nonleveling item-tool qualifications.

## Proposed mechanical allocations — **not approved**

1. **Owner-specified expected conversion frequency (2026-11-10):** The expected order for converting learned-Competency source grants is **(most common) binary Competency proficiency plus one rank**, **(next) one rank only**, and **(least common) binary Competency proficiency only**. These are three independent grant effects, **not** successive stages, universal mandatory source mappings, or an approved record-by-record allocation. This revises the earlier proficiency-only-first recommendation.
2. **Source interpretation:** A source establishing a learned discipline or initial occupational training is a strong candidate for **proficiency + one rank**. A separate, genuinely earned opportunity to advance an existing discipline can grant **one rank only**, including when proficiency is absent; an explicitly limited qualification can grant **proficiency only**. Review individual sources, such as `Guild Artisan`, `General Dwarf`, `General Goliath`, and `Rock Gnome`, rather than converting all existing `competency-training` fields automatically. Each actual rank-grant occurrence retains its separate entitlement and provenance; mere duplicate proficiency is not an additional rank.
3. **Thieves' Tools:** These are an **item-tool proficiency** under the shared equipment qualification framework, outside the ranked Competency conversion frequency. They grant no `competency.locksmithing` rank and do not automatically grant `skill.open-lock` training. Preserve all three affected Origin grants/choice options while awaiting a concrete approved native item-tool qualification field contract. The current design specification approves the structural direction but explicitly defers exact schema and evaluator.
4. **Gaming Sets and Musical Instruments:** Use one type-specific, binary equipment qualification, with no automatic Perform specialty rank or broad artistic proficiency. Keep existing one-of choice counts.
5. **Navigation and other native methods:** Grant the selected Competency through its one profile; do not additionally award proficiency in associated physical equipment unless a separate explicit rule justifies it.
6. **Fixed duplication:** Preserve source occurrences and effective qualification/proficiency state. Do not create ranks, Expertise, alternate weapons, or arbitrary skill substitutions from repetition. Whether unavoidable fixed overlap deserves a replacement option remains an owner decision.
7. **Contextual multipliers and bonuses:** Dwarf/Rock Gnome History modifiers and Drow/Wood Elf Survival effects remain separate from proficiency/rank training. Define exact stacking only after comparing actual conditions and the ordinary check projection.

No numeric rank allocation, milestone scale, cap, advancement economy, or replacement rule has been written into JSON.

## Representative Class and package interactions

| Combination | Overlap | Character consequence / review |
| --- | --- | --- |
| Criminal + Rogue | Both give Thieves' Tools proficiency | Unavoidable given this Background/Class; one effective qualification, separate grant provenance; compensation unresolved. |
| Spy + Rogue | Rogue has Thieves' Tools; Spy may select them | Avoidable by selecting Disguise, Forgery, or Calligraphy instead. |
| Street-Raised + Rogue | Rogue has Thieves' Tools; Street-Raised may select them | Avoidable by selecting another formative training benefit. |
| Giant Foundling + Soldier | Both give Intimidation training | Fixed Skill overlap, not a Competency rank grant. No automatic extra rank or replacement Skill. |
| High Elf + Barbarian | Barbarian gives broad martial weapons; High Elf gives four particular types | Check broad-versus-narrow coverage, not identical string equality. Several specific benefits may be redundant. |
| Gladiator + Barbarian | A chosen arena weapon may already be covered by martial/simple weapon proficiency | The approved one-specific-weapon grant remains; test cases with redundant and genuinely novel proficiency. |
| Sailor + maritime-oriented Class | Navigation and Water Vehicles may be present from other sources | Navigation Competency proficiency and vehicle operation must be compared separately; equipment association does not resolve duplication. |

These are representative interactions, not an exhaustive Class catalog review.

## Next balance review that is now possible

- Compare completed conventional Species–Ancestry–Upbringing combinations and then cross-cultural substitutes. **General Gnome** is light in isolation; **Rock Gnome** is comparatively rich. Neither observation alone warrants normalization.
- Separate Rock Gnome's **Tinkering training**, **Clockwork Tinker fabrication access**, **Prestidigitation cantrip**, **Magical Tinkering technique**, and **Artificer's Lore** contextual multiplier. Test the actual activities and action/resource costs to identify redundancy rather than merging them by label.
- Contrast repeatable, environment-dependent benefits: **Wilderness Provisioning** (supplies up to six people where resources are available), **Street-Raised Urban Navigation** (half-time travel outside combat in substantial settlements), **Explorer Expedition Survey**, and **Sailor Ship's Passage**.
- Compare noncurrency resources and social services in context: Guild Merchant's mule/cart, Guild Artisan's tools, Knight's noncombat retainers, institutional shelter, networks, dues, and mundane starting possessions. Currency alone is not an adequate benefit budget.
- When the typed grant policy is approved, evaluate both initial proficiency coverage and plausible early ranks across complete Origin + first-level Class combinations, including cases where fixed overlap cannot be avoided.

## Owner decisions required before the next substantive migration

1. Confirm record-by-record allocation of current Background and Upbringing `competency-training` benefits using the **expected frequency order: proficiency + one rank; rank only; proficiency only**. This is the owner's expected common pattern, not a mandate to assign every source a fixed value.
2. Approve the type-specific tool qualification grant data shape, including selectable Gaming Sets, Musical Instruments, and Thieves' Tools; verify it against existing Class `toolProficiencies`.
3. Approve heterogeneous one-of choice encoding for Spy and Street-Raised that retains the same option counts and grants either a Competency effect or the Thieves' Tools qualification.
4. Define whether any unavoidable fixed duplicate qualification/Skill grant receives a constrained alternative, or whether duplicate provenance without added value is acceptable.
5. Decide how generic eligibility pools such as `crafting-competency` are validated and whether categories include Cartography/Navigation; source wording alone should not imply a complete catalog taxonomy.
6. Clarify contextual bonus stacking, language-choice collisions, specific weapon/item matching, and vehicle scopes.
7. Review high-impact or uneven Upbringing features (notably General/Rock Gnome) and equipment/social value after accounting for complete character packages.

## Edits, verification, and limits

**Changes on this working branch:** 14 native JSON files received only canonical identity normalization, explicit already-implied choice typing, or corrections to inaccurate old-facet/equipment-association descriptions. Previously approved training choices, Background features, Upbringing parent relationships, equipment packages, and benefit counts remain unchanged.

**Static checks executed:** Each edited file was parsed as JSON; the complete post-edit set of 16 Backgrounds and 13 Upbringings was independently parsed and scanned against the **current `main`** canonical IDs. Results: **29/29 parse**, **3 remaining invalid Competency references** (all noted above), and no other invalid `skill.*` / `competency.*` strings.


**Additional static checks:** All 13 Upbringing records have unique effective IDs. Their five parent relationships resolve to existing Upbringings with no cycles (three Elf children and two Gnome children). Their benefits include six explicit fixed-language grants and five additional-language choices. These facts check references and representation only; no runtime inherited-benefit application or prevention of duplicate learned languages was tested.

**Not executed:** Full native JSON schema and inventory resolution, Character Origins entitlement/choice authorization, inheritance execution, .NET compile/test suite, and end-to-end Character Sheet tests. Those are not valid passing test claims. The working branch still requires a deliberate synchronization with `main` without discarding existing Origins work, plus revalidation after integration.

**Next safe step:** obtain decisions on tool-qualification encoding and provisional competency training grants, then resolve the three remaining references and review early-character balance without imposing a global rank scale or unrelated C# changes.
