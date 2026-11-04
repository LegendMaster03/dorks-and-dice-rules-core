# Character Origins — Pillar 3: Life Experiences

**Status:** Initial repository/source audit and **unapproved design recommendations**. No Pillar 3 grants or benefit allocations are newly approved by this document.
**Branch:** `feature/character-origins-pillar3-life-experiences`, based on `main` at `d5a2b15b7ae2bd88ae9e47a5ddfad01d481623bb` (PR #130).
**Ruleset:** `first-party-001`
**Authoritative content:** `data/rulesets/first-party-001/native/`. `working/` and source imports are evidence, not runtime rules.

## Existing approved boundary

- Species/Ancestry and cultural Upbringing are independent. A `defaultUpbringing` is a suggestion; an explicit selection overrides it.
- An Upbringing's `parent` inherits other Upbringing benefits, not biological ancestry. The character selects a single Upbringing package by default rather than accumulating a Species default and an explicit override.
- Learned languages, skills, weapons, and competencies are not automatically acquired by Species identity.
- Background and Upbringing are distinct components within Life Experiences, without requiring a fixed childhood/adulthood boundary.
- Starting equipment becomes ordinary character inventory.
- No universal Pillar 2 origin schema, trait budget, or inheritance procedure is approved. Pillar 2 proposals are independent.

The Pillar 2 branch had a descriptive `docs/rewrite/character-origins/pillar3-life-experiences-intake.md` snapshot; it explicitly approves **no** new Pillar 3 mechanics. Treat it as an intake aid, not an authority. On 2026-11-03 the Pillar 2 branch was 20 commits ahead of `main` and shared its merge base.

## Native Upbringing inventory and review questions

| Native ID | Direct benefit summary | Review questions |
| --- | --- | --- |
| `upbringing.dwarf.general` | Dwarvish; scoped Stonecunning History proficiency multiplier; choice of smithing, brewing, stonemasonry | Define interaction of multiplier and existing training; contextual strength relative to other packages |
| `upbringing.elf.general` | Elvish; choose Insight, Perception, Survival training | Confirm training choice category and default-language design |
| `upbringing.elf.drow` | Undercommon; +2 scoped subterranean Survival | Inherits General Elf; separate context from ambient underground-only bonus |
| `upbringing.elf.high` | Longsword, shortsword, shortbow, longbow proficiency; one language | Inherits General Elf; wording says “inherited Elvish” despite culturally learned Elvish |
| `upbringing.elf.wood` | Shortbow, longbow proficiency; +2 scoped woodland Survival | Inherits General Elf; clarify weapon overlap with Class/other grants |
| `upbringing.gnome.general` | Gnomish | General cultural knowledge is explicitly unresolved, not yet a mechanical grant |
| `upbringing.gnome.forest` | Choose Nature, Survival, Animal Handling training | Inherits General Gnome; naturalist choice |
| `upbringing.gnome.rock` | Tinkering Competency; scoped History multiplier; clockwork crafting; Prestidigitation; magical tinkering technique | Inherits General Gnome; casting ability unresolved; compare magical access and benefit weight |
| `upbringing.goliath.general` | Gol-Kaa; one of Athletics, Insight, Survival; one of Cooking, Leatherworking, Navigation competencies | Any Species; merely suggested by Goliath; composite Athletics does not train Climb/Jump/Swim |
| `upbringing.giant-foundling` | Giant; one more language; Intimidation and Survival training | Any Species; independent of Goliath Upbringing; no Strike of the Giants feat, giant ancestry, or size grant |

There are **ten** active native Upbringings. **Interpretive correction (owner clarification, 2026-11-03):** These were not created as standalone, equally valued packages. Another reconciliation first refined conventional Species and Ancestry identities, then separated inherent from acquired characteristics to construct Upbringings. Differences among individual Upbringing grants therefore reflect a *decomposition of previously reconciled combinations*, not evidence by themselves of a balance defect. Review the complete conventional Species–Ancestry–Upbringing combinations before assessing balance. Independently test cross-cultural substitution, since selecting a different Upbringing can expose genuine differences in the full resulting combination. Neither test authorizes automatic normalization or redistribution of already approved native benefits. A conventional 5.x conversion maps into the resulting Dorks & Dice rules rather than preserving all original 5.x numeric mechanics.

## Working Background inventory (not active native rules)

| ID | Working evidence | 2024 source package |
| --- | --- | --- |
| `background.acolyte` | Two candidates: 2014 and 2024 | Insight + Religion; Calligrapher's Supplies; Magic Initiate (Cleric); Intelligence/Wisdom/Charisma; equipment or 50 GP |
| `background.criminal` | One retained 2024 definition | Sleight of Hand + Stealth; Thieves' Tools; Alert; Dexterity/Constitution/Intelligence; equipment or 50 GP |
| `background.sage` | One retained 2024 definition | Arcana + History; Calligrapher's Supplies; Magic Initiate (Wizard); Constitution/Intelligence/Wisdom; equipment or 50 GP |
| `background.soldier` | One retained 2024 definition | Athletics + Intimidation; Gaming Set choice; Savage Attacker; Strength/Dexterity/Constitution; equipment or 50 GP |

All four are currently under `working/backgrounds/`. The label `retained-native-definition` on three files is not itself native publication. Acolyte 2014 evidence adds two languages, a thematic equipment package, and **Shelter of the Faithful** (religious hospitality/care subject to relationships and setting conditions).

### Edition comparison

- **2014 / SRD 5.1:** Background grants learned skills, commonly tools and/or languages, equipment, and potentially a social feature. Backgrounds can be customized. Ability improvements principally came from Race, not Background.
- **2024 / SRD 5.2.1:** Background grants two skills, one tool, one Origin feat, one of two ability-score distributions across three named abilities (+2/+1 or +1/+1/+1), and a choice between thematic equipment and 50 GP. Languages are supplied separately.
- Combining all grants from both editions would duplicate equipment and over-award features. Removing 2014 social benefits without review would discard potentially useful relationships and setting integration.
- Official 2014 rules replace a duplicated skill/tool proficiency with a different same-kind proficiency. That is comparative evidence, **not** an automatically adopted Dorks & Dice rule.

Primary comparisons:
- https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background
- https://www.dndbeyond.com/sources/dnd/br-2024/character-origins

### Further references

- 3.5e optional *Character Background* variant records activities over levels with skills, feats, gear, contacts, reputation and recognition. Its design lesson is experience history, **not** automatic retroactive class levels. https://www.d20srd.org/srd/variant/buildingCharacters/characterBackground.htm
- Pathfinder Unchained *Background Skills* addresses competition between adventuring skills and occupations by providing separate background skill ranks. Compare the motivation, not its numeric progression. https://legacy.aonprd.com/unchained/skillsAndOptions/index.html
- *An Elf and an Orc Had a Little Baby* separates parentage and Upbringing while retaining a separate Background. Its Upbringings include learned traits and ability changes; its published numeric budgets and parentage assumptions are not adopted. https://www.geeknative.com/77281/worth-your-money-an-elf-and-an-orc-had-a-little-baby-parentage-and-upbringing-in-dd/

## Existing Dorks & Dice mechanics to preserve

- `docs/universal-character-concepts.md`: one semantic competency can have ranked and tool-proficiency facets; numeric ranks and proficiency are not interchangeable. Craft, Perform and Profession are families with independently trainable children. Composite skills and family relationships differ.
- `docs/character-mechanics-consumer.md`: fixed proficiency and rule-defined choices use canonical competency identities; duplicate **choices within one source group** conflict. Language capabilities use a catalog; fixed and previously selected entries are removed from later language choices within projection. There is no approved Life Experiences *cross-package duplicate replacement* rule yet.
- Training in composite Athletics does not automatically train its independently ranked Climb/Jump/Swim contributors, per General Goliath.
- Grant occurrence and provenance should be retained when two sources refer to one competency, even where the effective training state is one fact.
- Rank grants, contextual fixed bonuses, proficiency qualifications and feat grants remain distinct effect types.
- The current native loader's ability to index JSON must not be confused with full executable origin-package grant resolution.

## Working recommendations — **not approved**

### Responsibility: divide by cause, not arbitrary age

**Upbringing:** formative environment, culture, socialization, early practices and learned languages; can also grant a skill, competency or technique where the upbringing concretely taught it.

**Background:** an established role, sustained activity, specialization, consequential experience, and relevant ties before adventuring; grants occupational expertise, appropriate ordinary equipment and potentially social access.

Both may cover the same historical years. A character raised in a religious order can have religious Upbringing and an Acolyte Background for distinct reasons; **two labels must not grant the same capability twice by default**. Neither categorization forces formal employment or biological childhood.

### Benefit-category allocation: recommended directions, not quantities

| Grant class | Candidate policy | Outstanding decision |
| --- | --- | --- |
| Ability improvements | Consider a character-wide initial allocation rather than Background-locked ability lists or cumulative Upbringing/Background bonuses. Explicit inherent traits remain separate. | Source, amount, timing, and limits |
| Skill/competency training | Both components may grant learned capabilities using shared universal identities and actual training rules. Background templates begin from 2024 two skills plus one tool/competency; Upbringings are reviewed individually. | Total allowance, specialty versus broad training, exceptions |
| Skill ranks | Grant only when a rule explicitly grants ranks; duplicate training alone does not imply ranks. | Starting and later rank schedules |
| Contextual bonuses | Retain defined conditions and stacking provenance; +2 to a specific Survival activity is not another training grant. | Stacking and duplication with other scoped bonuses |
| Languages | Predominantly learned through environment/education, also learnable through later experience; no Species-inherent language from biology alone. | Universal language entitlement, counts, overlap |
| Origin feats | Consider one standard Background-associated Origin feat, with bounded replacement/customization; no additional feat simply for choosing an advanced Upbringing. Preserve other approved sources, such as the Human bonus Origin feat. | Whether default feat is locked, selectable, or assigned outside Background |
| Equipment | One selected starting-resource policy; equipment granted through ordinary inventory; do not add 2014 and 2024 packages together. | Policy for spending cash, class equipment and equivalent presets |
| Social access | Consider preserving 2014-style relationships as conditional, setting-relevant privileges rather than automatic material entitlements, or as selectable features. | Mechanical equivalence and whether social features coexist with Origin feats |
| Multiple cultures | One coherent Upbringing grant resolution incorporating influences, not unbounded stacking of full packages. | Predefined combined entries versus custom options |

**Balance evaluation order:** Establish the source rationale and complete original conventional combinations; check whether separation preserved their intended distribution; then test Upbringing swaps and customized combinations against the shared Life Experiences mechanics. Upbringing-only grant counts are descriptive, **not** a standalone balance ranking. If a cross-cultural swap creates an actual exploit or inequity, analyze that combination rather than reclassifying the original split as defective.

**Duplicate grant recommendation for testing:** Identical training/proficiency grants should not stack and should not automatically become a rank/expertise upgrade. Where both sources would grant the same training, offer a same-category alternate selection at the same grant strength, with source provenance maintained. This resembles the 2014 fallback but needs a native rule for eligible alternatives, category boundaries, and limits. Language overlap and duplicate feat effects require separate identity-aware treatment rather than assuming identical conversion rates.

**Caution:** A fixed 2024 Background ability-score list can prevent valid concepts such as a Charisma-focused veteran Soldier or a Strength-focused Criminal. Automatically moving all ability bonuses to Upbringing reproduces the same problem with different names. Explore flexible general allocation before attaching scores to a mandatory narrative choice.

## Character acceptance cases

1. **High Elf, High Elf Upbringing, Sage:** Fey/Elven characteristics originate in Pillar 1; Elvish, cultural skill choice, weapon training, another language come from native Upbringing; Arcana, History, Calligraphy, Wizard Magic Initiate, and equipment are **working 2024 Background candidates**, not active rules. Check distinct innate vs learned spells, and possible choice overlap.
2. **Orc raised among Elves, Soldier:** Orc remains Humanoid with its approved inherent traits. General Elf Upbringing grants Elvish and a choice of Insight/Perception/Survival; working Soldier grants Athletics, Intimidation, Gaming Set, feat and equipment. Orc contextual Menacing Presence and Soldier training are distinct, as are Orc Savage Attacks and the distinct Savage Attacker feat.
3. **Goliath with Giant Foundling and Soldier:** The explicit Giant Foundling selection replaces suggested General Goliath Upbringing. Giant Foundling provides Intimidation and Survival training; working Soldier duplicates Intimidation. Test replacement rather than silent double training. No Goliath ancestry powers from upbringing.
4. **Human Acolyte:** Keep the approved Human extra Origin feat distinct from any standard Background feat. Assess whether 2014 Shelter of the Faithful is an optional conditional social benefit or part of another grant allowance. Do not give 2014 and 2024 equipment simultaneously.
5. **Construct educated by scholars:** Construct origins and inheritable traits remain Pillar 2 questions. Learned language, skills and Sage-like training can exist without conventional childhood. Avoid forcing an unspecified childhood mechanic.
6. **Several cultural communities:** Can document multiple influences without taking unrestricted complete Upbringings. An explicit combined package or one allocated pool needs further review.

## Open decisions for interactive reconciliation

1. Approve or revise the **mechanical responsibility division** between Upbringing and Background, without hard chronological cutoffs.
2. Decide where initial ability improvements reside, explicitly separating that from inherited or supernatural ability changes.
3. Decide one starting Origin feat via Background versus a broader shared Life Experiences choice, including treatment of the approved Human extra feat.
4. Set duplicate learned-benefit semantics before promoting Soldier or Giant Foundling/Soldier as a validated pair.
5. Reconcile the four backgrounds, beginning with Sage and Soldier and then the 2014/2024 Acolyte contrast.
6. Review culturally granted magic (Rock Gnome), contextual training/bonuses, and Gnome cultural knowledge.
7. Resolve representational questions for one/multiple/no formative communities and future acquired progression.
8. Evaluate Pillar 2 dependencies without adopting its unapproved draft schema.

No authoritative native JSON, APIs, Character Sheet code, or Pillar 2 files are modified in this initial intake.


## Desired 5.x character-conversion compatibility — user direction, not a requirement

**Status:** The project owner would **like** the ability to convert a conventional 5.x character into Dorks & Dice by selecting the corresponding Species/Ancestry and associated Upbringing. This is a compatibility objective, **not** a mandatory feature for Pillar 3 completion, and it does not approve an importer or a lossless round trip.

**Recommended design interpretation (pending specific approval):** Preserve explicit, edition/source-specific **compatibility pairings** between an original 5.x Race/Subrace or Species identity and the native Species/Ancestry plus conventional Upbringing that collectively reconstruct its intended character concept. Examples:
- A conventional **2014 High Elf** may map to `species.elf`, `ancestry.elf.high`, and `upbringing.elf.high` (whose parent is `upbringing.elf.general`), plus conversion of Background and other selections separately.
- A 2014 **Dwarf** maps to native Dwarf identity plus the appropriate conventional Dwarf Upbringing, but differences in currently selected weapon training, ability scores, and other traits must be reviewed, not silently treated as exact equivalence.
- A **2024 character** chooses Species, Background and languages in a different way than 2014. Do not automatically derive Upbringing from the Species and double-count learned languages or feats.

The compatibility relation is **a recommended/default reconstruction recipe**, not a required coupling, restriction, heritage rule, or automatic second grant. Users may independently select a different Upbringing. Multiple edition source identities may share a native target; a source identity may require additional choices or have no exact equivalent.

**Important design boundary:** A pairing can reconstruct the *intended conventional concept* without reconstructing all original numbers or effects. Native Dorks & Dice traits, creature types, speeds, feat policies and training may differ deliberately. A future converter should distinguish exact mappings, transformed/equivalent concepts, and rules requiring manual review or unsupported effects. Do not conceal mechanical divergence by labeling it lossless.

**Metadata needed if implemented later:** source game/edition, source Race/Species identity, source Subrace identity where applicable, native Species/Ancestry target, suggested Upbringing target, any additional independent Background/language/feat/ability choices, and known divergences. Keep this in a conversion crosswalk or equivalent versioned reference rather than making `defaultUpbringing` a mandatory identity association. Existing `defaultUpbringing` can be reused where valid, but a single native default does not necessarily identify the correct 2014 and 2024 source pairing.

**Pillar 3 implication:** Retain stable source-to-native relationship evidence when reconciling the ten Upbringings. Avoid restructuring them so that future conversion requires guessing which cultural benefits were separated from each 5.x Race/Species. Neither a converter nor new runtime schema needs to be implemented in this pass.


## Balance adjustment across Species/Ancestry and Upbringing — owner direction (2026-11-03)

The owner explicitly recognizes that the prior Species/Ancestry → Upbringing split is an **approved starting point, not a frozen balance allocation**. To make independently selectable Upbringings work, later reconciliation **may require shifting suitable benefits back into Ancestry or adding benefits to Upbringings**, alongside other justified adjustments. This supersedes any interpretation of the earlier audit as forbidding redistribution; it does **not** approve any particular trait move or numeric grant yet.

Design/evaluation approach:
1. Establish the complete conventional Species + Ancestry + associated Upbringing package and its original rationale, including the intended route for converting a conventional 5.x character *into Dorks & Dice* (not preserving exact source mechanics).
2. Test alternative Species/Ancestry + Upbringing combinations, including the cross-cultural and unusual-origin cases, to identify imbalances introduced by independence.
3. Distinguish a **classification problem** (a truly inherent characteristic placed in Upbringing, or learned training placed in Ancestry) from a **power-allocation problem** (individually sensible grants but disproportionate packages). A balance fix alone does not justify making cultural training biologically inherited.
4. Recommend targeted trait reassignment, added learned benefits, or adjusted grant conditions using the existing skill/competency/trait semantics. Evaluate the effect on both conventional and cross-cultural combinations, not standalone Upbringing counts alone.
5. Preserve the distinction between **proposed** adjustments and **approved** native content. Change authoritative JSON only after the relevant particular rules are reconciled and approved.

The objective is a workable, culturally independent Life Experiences system, not merely to preserve the first split verbatim or make every Upbringing grant the same number of features.

## Revised reconciliation source order — user proposal, 2026-11-03

**Status:** Proposed method of investigation, **not** approval of a final allocation or native mechanics. The owner observes that multiple new 2024/5.5e Background mechanics overlap substantially with the idea of Upbringing, and proposes using 2014 5e (or potentially 3.5e) as a foundation before assigning 2024 grants.

**Recommended methodological response:**
1. **2014 5e Backgrounds as initial structure.** Start with social/history identity, skill proficiencies, optional tools/languages, starting equipment, social features, and explicit customization. This avoids adopting the 2024 benefit grouping as a package by default.
2. **3.5e as a comparative mechanics source.** The optional 3.5e Character Background variant models per-level activities, class/feat/skill/gear progression, and optional reputation/contacts. That is not the same kind of default level-1 Background package as 2014 5e; selectively study specialized training, careers, contacts, and continued experience rather than grafting on the class-level generation procedure.
3. **2024 5.5e as a set of additional grants to allocate deliberately.** Review Background-provided ability-score increases, fixed Origin feats, two skills, a tool, and starting resources individually. Determine whether each grant belongs to Background, Upbringing, or a shared/general character-creation allocation, using narrative cause *and* game balance.
4. Test the resulting allocations against complete conventional Species/Ancestry/Upbringing + Background characters and independent cross-cultural combinations. Do not simply add 2014 social features and 2024 feat grants without a benefit comparison.

**Specifically open:** (a) whether initial +2/+1 or +1/+1/+1 ability adjustments are generally allocated rather than tied to Background or culture; (b) whether an Origin feat is a Background grant, shared Life Experiences allocation, or chosen through another character-creation component; (c) the degree of tool/language crossover; (d) how 2014 social benefits interact with any feat grants.

**Amendment to earlier working recommendation:** The prior suggestion to retain one Background-granted Origin feat is now only **one option for evaluation**, not the preferred settled allocation. The same applies to any presumption that the 2024 Background's two skills/one tool/ability list/feat are inseparable. Nothing in this source hierarchy reverses approved Pillar 1 Upbringings or determines Pillar 2 traits.

Sources checked:
- https://5e.d20srd.org/srd/backgrounds.htm (2014 Background skills, language, gear, social features, customization and duplicate-skill choice)
- https://www.d20srd.org/srd/variant/buildingCharacters/characterBackground.htm (3.5e optional activity-by-level variant)
- https://www.dndbeyond.com/sources/dnd/br-2024/character-origins (2024 Background grants)

## Reconciliation order clarified — 2026-11-04

**Owner direction:** The Species/Ancestry-to-Upbringing separation was **already carried out** as the initial Pillar 1 pass. The resulting native Upbringings are not yet balanced against each other or against Backgrounds. **Do not run the full balance/reallocation pass before performing an equivalent decomposition of the source Backgrounds.** The owner expects that completing both separations will make the remaining corpus inconsistencies visible and actionable.

### Stage 1 — Decompose Background source content (current work)

- Examine each source Background's identity and its individual mechanics. For each source benefit, classify the mechanical **destination** (Upbringing, Background, inherent origin/another pillar, general character-creation allocation, or no retained grant) and record its reason.
- Allow **uneven decomposition**. Acolyte can remain predominantly Background; Giant Foundling has already been moved predominantly into Upbringing. Do **not** impose a uniform percentage of grants or artificial Upbringing slice on every source Background.
- Track destination separately from grant type: a skill or language can be learned through either Upbringing or Background based on the source narrative and intended rules; a feat or ability adjustment may need broader system-level consideration.
- Preserve historical source identity and an explicit conversion relationship. A 5.x Background can map to Dorks & Dice Upbringing, Background, both, or an additional character-creation selection; this is **conversion into native rules**, not lossless reconstruction.
- Avoid double-granting Giant Foundling from its historical source Background and the already-native Upbringing. The 2023 source's excluded non-SRD feat remains excluded unless separately revisited.
- Promote only **individually approved coherent native content**. Do not declare a Background complete merely because a partial source-derived record was split; note any open destination or effect decisions.

### Stage 2 — Audit the complete resulting Character Origins corpus

Once background-derived formative grants have joined the Species-derived Upbringings, compare real complete combinations and legal cross-cultural selections. Check for duplicate qualification identities, grant omissions, scope/eligibility mismatch, strong and weak combinations, ability/feat/language/competency irregularities, and misclassified inherent versus learned benefits. The audit concerns **whole character-origin outcomes** and **independently selectable components**, not feature counts alone.

### Stage 3 — Rebalance and normalize only where justified

Address the audit findings through targeted reassignment between Ancestry and Upbringing, additions, substitutions, eligibility/scoping changes, or shared grant rules, with particular attention to Background overlap. This stage may revise initial Pillar 1 allocations. **Do not infer that the initial split must remain frozen** or that equal-looking package sizes are required. Maintain review/approval status for each rule change.

### Immediate implication for Acolyte

The existing Acolyte comparison document is a source inventory and candidate decomposition, **not** a finalized Acolyte Background. First classify each 2014 and 2024 element by destination and record open dependencies; do not finalize its balance or feature budget now. Apply the same method to the remaining background candidates. The 2014 model is the initial Background reference, with 3.5 alternatives and 2024 mechanics evaluated for their appropriate destination.

## Proposed baseline: formative language grants — owner proposal (2026-11-04)

**Status: proposed, for review; not yet a newly approved operative rule.**

- An Upbringing **normally provides at least one learned language**, derived from the character's formative environment, society, culture, community, education, or socialization.
- This would typically be a language associated with the culture referenced by the Upbringing, **not a biological Species grant**. A character raised in a different culture is not automatically entitled to the language traditionally associated with its Species.
- **Common may be granted in addition where justified by the actual formative community or contact**, but is not automatically required of all characters, and should not be granted for no discernible reason simply because the character is playable.
- There may be **exceptions**, including atypical isolated, nonverbal, recently created, or otherwise unconventional beginnings. The existence and exact handling of exceptions are still to be reconciled; do not require every concept to be given a fabricated childhood or language.
- **Backgrounds may also grant learned languages** through roles, professions, study, travel, and other significant experiences. Not all Backgrounds must do so. Do not remove the 2014 Acolyte's two language choices solely to enforce a supposed exclusive Upbringing responsibility.
- Repeated languages from independent sources need a consistent no-double-benefit policy, but the exact replacement/choice semantics remain unresolved. A primary language and Common may be separate grants; do not silently treat the same known language as multiple proficiencies.
- The rule is a **minimum expectation**, not a package-equivalence or balance metric. Its potential adoption does not prove that existing Upbringings are balanced.

**Baseline examples to test:** General Dwarf -> Dwarvish; General Elf -> Elvish; General Goliath -> Gol-Kaa; Giant Foundling -> Giant and its existing language choice; Common-speaking city -> Common as culturally learned; culturally displaced character -> language of actual upbringing, not inherited Species language. Existing native Upbringings are unchanged pending approval and later corpus-wide analysis.

## Language decision: community-granted Common and inherited group rules — 2026-11-04

**Approved direction:** Use the **first** Common-language allocation option: an Upbringing from a community in which Common is widely used may grant Common **as an additional learned language**, rather than taking that grant from a universal, separate character-creation language allowance. Common is not an automatic grant for every playable Species or every Upbringing.

**Relevant existing structural principle:** This system supports **non-selectable parent definitions** whose grants are inherited by selectable members of a group. Apply the same idea to language rules when genuinely shared across an Upbringing family:

- **Existing pattern, not new architecture:** General Elf Upbringing is a selectable parent of Drow, High Elf, and Wood Elf Upbringings; General Gnome Upbringing is a selectable parent of Forest Gnome and Rock Gnome Upbringings. Their descendants already inherit the shared language/benefit baseline. A parent Upbringing **may be selectable or non-selectable**; selectability is not a prerequisite for inheritance. Common can be added at a shared parent only when appropriate to all descendants, otherwise at the individual child.
- A selectable Upbringing can also contain its own language grant when the language is not genuinely shared by all members of the parent grouping.
- Selecting a child resolves inherited parent grants as **one effective Upbringing selection**, rather than treating its parent as an independently selected package or multiplying duplicate language benefits.
- A cultural parent must reflect a genuine shared setting/tradition. Do not create a fictitious universal cultural parent solely to grant Common across unrelated peoples; do not silently make a Society/Species language biological.
- The presence of non-selectable parent nodes does not require converting **existing selectable general Upbringings** into non-selectable records without review. The choice of parent/leaf structure is independent of the language allocation principle.
- Backgrounds remain free to supply languages learned later through education, occupation, or other experiences. The proposed normal Upbringing minimum of one language, and its exceptions, remain to be reconciled with other grants and the complete corpus balance review.

**Status boundaries:** The owner explicitly chose the first Common allocation approach. Exact per-Upbringing Common grants, changes to parent/selectability hierarchies, and duplicate-language replacement mechanics are **not** approved by this choice and remain pending content-by-content reconciliation. No native JSON changes yet.

## Further comparisons: Sage, Strixhaven, and the Elf/Orc parentage-and-upbringing books

**Status:** Research evidence and proposed tests, not approved mechanics or authorizations to promote non-SRD content.

### Corrected Sage comparison

- **2014 Sage:** Broad scholarship including Alchemist, Astronomer, Discredited Academic, Librarian, Professor, Researcher, Wizard's Apprentice and Scribe. It grants Arcana, History, two languages and the Researcher informational/social feature. Wizard study is one specialization rather than the meaning of Sage as a whole. Source: https://www.dndbeyond.com/backgrounds/11-sage
- **2024 Sage:** Still scholarly, but adds a particularly Wizard-oriented Magic Initiate (Wizard) Origin feat, Calligrapher's Supplies, and specific Ability choices. Do not recast broad scholarship as a compulsory Wizard upbringing. Source: https://www.dndbeyond.com/backgrounds/406485-sage
- **Strixhaven: A Curriculum of Chaos (2021):** Five *distinct* college Student Backgrounds, not literal inherited children of Sage. Their college-specific magical study intersects academic Background, upbringing/environment, and acquired magical feat/spell-list access. The five colleges **do not all emphasize Wizard magic**:
  - Lorehold: historical/archaeological/spiritual studies; Initiate spells from Cleric or Wizard.
  - Prismari: elemental arts/performance; Initiate spells from Bard or Sorcerer.
  - Quandrix: math, physics and nature; Initiate spells from Druid or Wizard.
  - Silverquill: language, influence, light and shadow; Initiate spells from Bard or Cleric.
  - Witherbloom: biology, life, death and healing; Initiate spells from Druid or Wizard; thematically especially hospitable to Druid, Cleric, Warlock and others.
  
  The different college spell choices provide evidence for **discipline-specific learned magic**, not five mandatory Wizard specializations. Strixhaven also adds college-related spells to spellcasting lists; neither feat grants nor spell-list additions can simply be copied into native Upbringings without examining class and feat rules. Direct source listing: https://www.dndbeyond.com/sources/dnd/sacoc . Official D&D Beyond comparative article: https://www.dndbeyond.com/posts/1128-building-around-the-new-backgrounds-found-in

**Design inference to test:** General scholarly exposure, an institutional/student environment, demonstrated scholarly activity and specialized magical study are *distinct dimensions*. A scholarly Upbringing might represent education, while Sage Background describes substantive research and scholarly practice. Alternatively, a college Student concept might largely be Background if joining the college was a deliberate later activity. Do not enforce a childhood boundary; classify grants by what the selected experience actually provides. No automatic Sage → Strixhaven parent relation is established.

### Requested review: *An Elf and an Orc Had a Little Baby* series

The owner requests this reference collection explicitly as part of the Background/Upbringing **decomposition** review, not just as a mixed-Species heritage precedent. A direct multi-volume source study was previously recorded on the separate Pillar 2 branch in `docs/rewrite/character-origins-pillar2-reconciliation.md`; those findings are research, not a binding Pillar 3 allocation.

**Existing relevant findings from that recorded study (do not mistake this for a new direct re-reading):**

- Volume 1 separates point-selected parentage traits from learned Upbringing; Appendix B also discusses optional Upbringing benefit selection and mixed cultural upbringing. Its published numerical ranges/traits are **not Dorks & Dice prices**.
- Later volumes expand available parentage/creature concepts; they do not by themselves establish a universal rule that all cultural/occupational benefits must sit in Upbringing.
- Compare each book's Upbringing content to its treatment of separate Background and class/feat selection, with special attention to overlapping learned skills, languages, non-biological origins and cases combining multiple formative cultures.

**Pillar 3 comparison checklist for a fuller review of all four books:**
1. What does a selected Upbringing actually grant, and which benefits are identical or comparable to ordinary 5.x Background grants?
2. Can non-biological, artificial, mixed-cultural or unconventional formative environments use the same approach?
3. How are broad formative education and later sustained activities/occupations distinguished, if at all?
4. How are overlap, duplicate grants, language entitlement, feats and exceptional magical education handled?
5. Does the source provide reusable parent/group inheritance, presets and advanced customization without unlimited benefit accumulation?
6. What useful design pattern transfers without importing the source's budgets, feature prices or parentage assumptions?

### Decomposition implications

- **Do not presume every scholarly or magical source Background needs a new Upbringing.** The decision is grant- and concept-specific; one may remain largely Background, another may primarily contribute formative training.
- Separate **source identity**, **native narrative role**, **mechanical grants**, **actual acquisition stage**, and **optional thematic specialization**. Those dimensions may have different destinations.
- Record the 2014 Sage Researcher feature and languages alongside 2024 Wizard-oriented choices without deciding yet whether any source grants are retained, replaced, allocated to Upbringing or folded into a general Character Origins choice.
- Treat Strixhaven and the Elf/Orc books as comparative design evidence. No non-SRD content is being promoted or newly claimed as authoritative native material.


## Sage source preference and working decomposition — 2026-11-04

**Owner-endorsed direction:** The 2014 5e Sage provides a broader and more useful identity basis for this reconciliation than the narrower 2024 Sage. Treat 2014's scholarly activities and social function as the starting point, with 2024 Magic Initiate (Wizard) and Calligrapher's Supplies evaluated individually rather than making Wizard training compulsory.

**Source comparison (2014 Basic Rules/PHB vs 2024 PHB):**

- 2014 Sage: Arcana and History skills; two chosen languages; a small researcher-themed equipment package and 10 GP; *Researcher* identifies where/from whom unknown lore might be found, subject to GM limitations; eight suggested *specialties*: Alchemist, Astronomer, Discredited Academic, Librarian, Professor, Researcher, Wizard's Apprentice, Scribe. These specialties are **narrative suggestions**, not eight independently granted mechanical subclass packages. Source: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background
- 2024 Sage: Arcana and History; Calligrapher's Supplies; Magic Initiate (Wizard); Constitution/Intelligence/Wisdom ability options; and equipment or 50 GP. Its character history describes access to libraries during formative years and early exposure to magic. Source: https://www.dndbeyond.com/backgrounds/406485-sage

**Candidate destination review; not yet approved as native rules:**

| Source concept/grant | Proposed location to test | Important boundary |
| --- | --- | --- |
| Identity of a researcher/scholar; Researcher information-access feature | Sage Background | This is a meaningful acquired role even when scholarly training started early |
| Arcana and History | Sage Background baseline | Both are acquired proficiencies, and specific variants could warrant alternative competencies |
| Two chosen scholarly languages | Sage Background or Upbringing as justified by source experience | Do not move merely because Upbringing normally grants a language; do not silently double count |
| Formative academic/literate social environment | Possible Scholarly Upbringing | Do not invent a new obligatory package for someone who became a scholar later |
| Alchemy, astronomy, teaching, scribal work | Optional acquired specialty | A 2014 Sage specialty is not itself a fixed mechanical benefit; must define actual grant separately |
| Wizard-apprentice magical initiation | Optional specialized training/feat | Wizard magic is not compulsory for all Sage backgrounds |
| 2024 Calligrapher's Supplies | Optional scholarly/scribal competency | Not a universal qualification for every scholarly role |
| 2024 fixed ability increases | Broader Character Origins allocation pending design | Do not auto-add +3 to an older edition package |

**Strixhaven caution:** Its five college Student Backgrounds span different magical disciplines (including Druid-related study). They are related examples of academic specialization, not proof of literal source-defined Sage parentage or mandatory Wizard spellcasting. Do not promote non-SRD spells or feats solely because they appear in a comparative source.

**Source coverage limitation:** The existing `working/backgrounds/sage.json` contains the 2024 SRD-derived Sage, not a local reconciled 2014 Sage entry. The 2014 description above is independently referenced historical material, not yet a promoted native rule. The general Background decomposition and later corpus balance audit remain open.

## Candidate rule: Background language allocation — 2026-11-04

**Status: owner-proposed design direction, with Sage one-language grant as recommendation pending specific approval. No native content changed.**

> Languages are normally acquired through Upbringing. A Background grants an additional language only when language acquisition or sustained use is meaningfully part of its education, duties, activities, or associated social experience. A Background may grant a fixed associated language, a restricted choice, or an open language choice when warranted. Most Backgrounds need not grant any language.

**Important distinction:** Reconciliation does **not** mean mechanically relocating every language slot removed from a 2014 Background into a particular Upbringing. The existing Upbringing requirement normally supplies language knowledge on its own terms, reflecting the character's actual society/culture. The source Background's old language count is retained as historical evidence for conversion and later balance audits.

### First test cases

| Source Background | Candidate language treatment | Status |
| --- | --- | --- |
| 2014 Sage (two languages) | **One language of choice** provisionally, as scholarly study and translation plausibly teach additional languages | Proposed, not approved native grant |
| 2014 Acolyte (two languages) | No universal language allocation by default; investigate faith-specific liturgical/scholarly language or optional study specialization | Candidate to test, not settled |
| 2024 Soldier | No default language; particular military/intelligence/diplomatic roles may justify one | Candidate to test |
| 2024 Criminal | No default language; specialized associations/trades may justify one | Candidate to test |
| Source Giant Foundling | **Fully converted** to `upbringing.giant-foundling`; it is **not** a native Dorks & Dice Background. It grants Giant plus an additional language choice. The historical source Background is conversion evidence only; do not recreate a second Background entitlement. | Existing selected native allocation |

**No automatic compensation:** Removing a source Background's former language slots does not itself authorize substitute skill, tool, feat, rank, equipment, or other benefits. Track removals and transformations as part of the full Character Origins corpus audit; rebalance after all source Backgrounds have been decomposed.

**Cross-source duplication:** A character can learn additional languages through Background even if its Upbringing already grants a language. Resolving duplicate selection and proficiency identity remains an open shared rule, not a reason to ban legitimate Background grants.

**Sage distinction:** Its broad 2014 identity includes scholarship, research, and translation opportunities; magical study is only one specialty. A language-of-choice grant need not imply Wizard magic or a fixed arcane language.


## Character creation order: mechanics-first or story-first — owner direction, 2026-11-04

**Approved design intent:** Players can (a) select rules-defined traits, Species/Ancestry, Upbringing, Background and other options first, then develop a fitting backstory; **or** (b) develop their backstory first and select the rules-defined options that best match it. Both routes are ordinary, equally legitimate D&D-style character creation. Neither has default priority, bonus power, or a separate character-creation ruleset. Narrative sequencing is not a mechanical prerequisite.

### Guidance for suggested characteristics

- Retain personality traits, ideals, bonds, flaws, specialties, motivations, associations, and backstory prompts as **optional inspiration**. They should help a player generate or express a character, regardless of which creation direction the player chose.
- Suggested characteristics may be displayed alongside a relevant Background such as 2014 Sage for discoverability, but **association is not exclusivity**: a character may adopt, combine, alter, or ignore them regardless of Background. They do not restrict freeform personality or biography.
- Do not make suggested characteristic prompts carry hidden skill, feat, affiliation, ability-score, language, or equipment grants. Any such effect must be a separately specified explicit rule, not a consequence of choosing a narrative prompt.
- A chosen Background should be mechanically comprehensible without the player accepting its default fictional story. A custom story need not be squeezed into one of the source's suggested archetypes. Players select the nearest appropriate mechanics where the concept fits.
- If a particular **actual mechanical grant** requires a story-world condition (for example, current access to a religious order's support), express that condition in the grant's rules rather than making the entire character history follow a mandatory template.
- The 2014 Sage's suggested specialties and characteristic tables should therefore be kept available as optional writing aids; they are **not** eight mandated mechanical subtypes or requirements to qualify as Sage.

### Giant Foundling naming and classification clarification

The already-native `upbringing.giant-foundling` was **fully** converted from its historical source Background into an **Upbringing**. It is not simultaneously a Dorks & Dice Background, and its earlier non-SRD source feat remains excluded. The owner is considering renaming the native concept to align with general culturally derived Upbringings (and to avoid unnecessary reuse of the source Background's distinctive name). **Naming is a proposal, not yet an approved rename**. A rename must preserve any needed historical conversion mapping and distinguish a general Giant-community upbringing from the particular published character concept. No native ID/name changed at this stage.

### Remaining design decision

Whether optional narrative guidance is cataloged centrally and referenced by multiple Backgrounds/Upbringings, or is authored alongside each Background for convenience, is a presentation/content-organization choice. Neither option makes narrative prompts mechanical prerequisites.

## Sage approved native baseline — 2026-11-04

**Owner decision:** Use **2014 5e Sage**; do not create a separate scholarly rule system. Supersedes the earlier Sage design candidates in this document. The previously discussed **reduction from two 2014 Background-granted languages to one** is retained to recognize languages normally supplied by Upbringing.

**Native file:** `data/rulesets/first-party-001/native/backgrounds/sage.json` (`background.sage`, status `selected-native`). The non-authoritative `working/backgrounds/sage.json` still preserves the 2024 source definition; no source history was overwritten.

| Decision | Native treatment |
| --- | --- |
| Background identity | Broad scholarship; not compulsory wizardry or magical college |
| Skills | Arcana and History training |
| Languages | One additional language of choice |
| Background feature | Researcher: knows possible avenues to find unknown information, subject to GM adjudication; not guaranteed knowledge |
| Equipment | 2014-style research/writing items, clothing, and 10 gp; story-hook correspondence is customizable, with no required deceased colleague in the character's biography |
| 2014 specialties | Retain all eight as optional suggestions without new mechanical subtypes |
| Personality, ideals, bonds, flaws | Optional player-facing narrative prompts; players can use story-first or mechanics-first creation |
| 2024 feat, abilities, tools | No automatic Magic Initiate (Wizard), fixed ability increase, or Calligraphy grant |

**Implementation boundary:** This is an approved **native JSON content** allocation, not a claim that Background-grant, narrative-feature or starting-equipment execution is already implemented in C#/consumers. The equipment list is descriptive and must later be reconciled to verified native item identities and ordinary inventory; no extra inventory feature or new system is introduced. This content's effect on the total Character Origins benefit distribution is subject to the later agreed corpus audit.

**Next:** Decompose Criminal and Soldier, then return to Acolyte using the same 2014-first approach; do not reopen Sage without a concrete new issue.
