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
