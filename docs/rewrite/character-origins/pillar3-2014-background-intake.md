# Pillar 3 — 2014 Background Corpus Decomposition Intake

**Status:** Source-backed first-pass classification and **unapproved** proposals. No new Background or Upbringing grant is approved by this intake, and no native JSON is changed.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`

## Scope and source order

The 2014 Player's Handbook has **13 primary Backgrounds**. Four have already been reconciled and selected as native Backgrounds in this branch:

- `background.acolyte` — 2014 Insight and Religion, one **faith-associated** chosen language instead of two unrestricted choices, Shelter of the Faithful.
- `background.criminal` — 2014 Deception and Stealth, Thieves' Tools, Gaming Set choice, Criminal Contact.
- `background.sage` — 2014 Arcana and History, **one** language of choice instead of two, Researcher.
- `background.soldier` — 2014 Athletics and Intimidation, Gaming Set and Land Vehicles training, Military Rank.

Review the **remaining nine** 2014 PHB Backgrounds for whether their formative components belong to Upbringing. Historical source descriptions and features are evidence, not automatically approved native grants. Do not replace the approved first four with their 2024 versions. Also retain the already-native `upbringing.giant-foundling` as an Upbringing **only**, not a pending Background.

Sources:
- Official 2014 Background listing: https://www.dndbeyond.com/backgrounds?filter-name=&filter-source=1
- Comprehensive legacy Background list and individual PHB feature mapping: https://rpg.stackexchange.com/questions/162624/what-are-all-the-background-features-in-the-players-handbook
- 2014 Basic Rules for Acolyte, Criminal, Folk Hero, Noble, Sage, Soldier: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background
- Additional PHB options and variants may require the licensed PHB for full original text; do not mistake an index entry for full detailed evidence.


## Non-SRD PHB source handling — 2026-11-04

**Source/licensing clarification:** The 2014 Player's Handbook has thirteen standard Backgrounds; SRD 5.1 contains **only Acolyte** among them. The absence of a CC-BY grant for the other PHB Background text **does not** mean the underlying common concepts or mechanics are copyright-protected. Under U.S. law, 17 U.S.C. § 102(b) excludes ideas, procedures, processes, systems, and methods of operation; the U.S. Copyright Office's Games guidance specifically distinguishes game-play methods from copyrightable rules explanations and illustrations.

**Implementation/editorial handling for the nine candidates and existing 2014-based native Backgrounds:**

- It is permissible to analyze and independently implement **generic experience concepts and functional game mechanics**: e.g., city navigation, identifying a contact, military standing, learned skills and other mechanical procedures. Source provenance and comparisons stay documented.
- Write **original Dorks & Dice prose** for all native descriptions, feature wording, roleplaying prompts, specialty explanations and invented setting details. Do **not** bulk-copy PHB feature paragraphs, background narratives, suggested-personality tables, illustrations or other expressive material absent licensing or another applicable basis.
- Short titles and names, including generic Background names or short feature labels, are **not protected by copyright as names**; trademark and brand-confusion questions are separate. Renaming a concept can improve clarity and reduce unnecessary publication risk, but renaming alone neither creates originality nor is universally required.
- Do **not** distort mechanical rules solely for apparent copyright differentiation. Change mechanics when required for Dorks & Dice Species/Ancestry/Upbringing/Background division, compatibility, balance or design quality.
- Keep the status of **individual source excerpts** distinct from the status of native expressions. The presence of a PHB concept in source reference notes does not establish that the entire PHB entry was released under the SRD license.
- Re-review the authored native Sage, Criminal and Soldier descriptions for independent prose and nonmandatory narrative material during the later quality pass; do **not** presume a problem merely because their functional mechanics resemble the 2014 PHB.
- The source `working/`, `reference/`, and comparison documents do **not** become part of the authoritative native content path merely by being useful research sources.

**Authoritative legal sources:** https://uscode.house.gov/us/usc/119-136/t17/s102/ ; https://www.copyright.gov/register/tx-games.html ; https://copyright.gov/help/faq/faq-protect.html ; https://docs.dnd5eapi.co/api/get-a-background-by-index (SRD 5.1 background inventory). This is a content-reconciliation policy, not a determination that any particular reproduction or distribution is legally cleared.

## Remaining nine: first-pass destination hypotheses

| 2014 source Background | Distinctive 2014 feature | Most likely destination | What needs a careful split? |
| --- | --- | --- | --- |
| **Charlatan** | False Identity | Background | Deceptive work and forged identity are experiences and practiced roles; do not assume a particular upbringing |
| **Entertainer** (Gladiator variant) | By Popular Demand | Background | Learned performance or gladiatorial expertise is usually occupational, although performer-family upbringing is possible |
| **Folk Hero** | Rustic Hospitality | Background | Fame and assistance earned through a defining event are Background; agricultural or rural training may have come from an independent Upbringing |
| **Guild Artisan** (Guild Merchant variant) | Guild Membership | Background | Membership, trade practice, dues and institutional contacts versus apprenticeship within a formative artisan household |
| **Hermit** | Discovery | Case-dependent | Isolation and self-study could be formative; a deliberate later retreat/discovery remains a meaningful Background; no compulsory shared biography |
| **Noble** (Knight variant) | Position of Privilege (Knight: Retainers) | **High-priority split review** | Noble-house upbringing, learned etiquette/languages, recognized social rank, current privileges, and independently earned roles should not be conflated; no general social-class subsystem presumed |
| **Outlander** | Wanderer | **High-priority split review** | Community upbringing and learned wilderness language/training versus later travel, guidance, exploration or wayfinding; do not equate all wilderness people with one culture |
| **Sailor** (Pirate variant) | Ship's Passage (Pirate: Bad Reputation) | Background | Shipboard experience and professional contacts are acquired practice; childhood aboard ship may justify a separate optional maritime Upbringing |
| **Urchin** | City Secrets | **Highest-priority Upbringing conversion candidate** | The 2014 definition directly describes growing up on the streets. Street-learned navigation, Stealth, Sleight of Hand, Thieves' Tools, Disguise Kit may describe formative adaptation, while starting possessions and post-childhood occupations must not be inherited as permanent assumptions |

**Classification is provisional.** No row authorizes a one-to-one native conversion or a required new Upbringing. A source Background may yield a Background, an Upbringing, both, or neither where a concept is already represented. Classify each *grant* with its narrative cause. A character's life can overlap both classifications; no arbitrary childhood/adulthood gate.

### Specific Urchin evidence and implications

The 2014 source explicitly describes growing up without a stable home on city streets. It grants **Sleight of Hand and Stealth**; **Disguise Kit and Thieves' Tools**; **City Secrets**, which accelerates noncombat traversal between urban locations; and an initial possessions package. It does **not** grant languages.

This makes a **Street-Raised Upbringing** (working label) a natural test: formative urban navigation and learned survival skills belong there. But the Dorks & Dice Upbringing generally also grants at least one learned language, so the resulting package must examine what urban speech actually was (often Common, not automatically all cities), rather than pretending its source possessed a language grant.

The original source implies orphanhood and poverty; those are **not necessary prerequisites** of a broadly useful urban-street formative experience. A player should be able to use the mechanics first or write their backstory first, without a mandatory dead-parent or homeless-child narrative.

**Not approved:** A new `upbringing.street-raised` ID, specific skill/competency grants, the City Secrets effect wording, a new language, a source name change, starting equipment, and any interaction with an independent Criminal Background. An individual approval pass is needed.

### Noble and Outlander are not automatic full conversions

**Noble:** Being raised in a noble household is a formative social environment, but the 2014 *Position of Privilege* also represents a standing recognized by others. It is not settled whether that recognition is a conditional social grant of Upbringing, Background, or a wider status relation. A noble-raised Soldier and a non-noble later ennobled should both be representable without giving everyone automatic duplicate privileges.

**Outlander:** Cultural knowledge, a language, and environmental learning can be formative; repeated travel or professional wilderness survival is a separate experience. Avoid inventing a universal "wild peoples" culture or assuming every wilderness character has the same Species or linguistic background.

**Hermit:** One may withdraw after substantial previous life experience or grow up in isolation. Those are different historical paths that could grant similar learned benefits; the 2014 *Discovery* is not automatically an inheritable cultural trait.

## Relevant 2024 and supplemental source contrasts

The 2024 PHB also includes Artisan, Farmer, Guard, Guide, Merchant, Scribe, and Wayfarer, while reconfiguring many older Background packages. These are **future comparison candidates**, not replacements of the 2014 baseline. Some may illuminate formative rural, educational or urban experiences, while others remain recognizable occupational Backgrounds. Source listing: https://www.dndbeyond.com/backgrounds and https://www.rollfortwo.com/articles/dnd-backgrounds-explained/ .

**Strixhaven: A Curriculum of Chaos:** Its five college backgrounds represent different magical disciplines, including Druid-related study. They are not mechanically inherited children of Sage; use them to study the distinction between educational environment, a sustained scholar role, and elective magical specialization, without importing automatic feats.

**The four Elf/Orc series books:** The **separate Pillar 2 branch** already documents a direct inspection of *An Elf and an Orc Had a Little Baby* Volumes 1–3 and *An Owl and a Bear Had a Little Baby* (fourth volume). See `docs/rewrite/character-origins-pillar2-reconciliation.md`, section `Direct review of user-provided source PDFs — 2026-11-03`. The present intake relies on those **recorded observations**, not a fresh comprehensive read of the files:

- Volume 1 includes an independent Upbringing selection and optional customized/mixed Upbringings in Appendix B, with a published Upbringing benefit budget; its numerical prices are **not approved native balance costs**.
- Later volumes enlarge parentage/material options without requiring all cultural/occupation traits to be inherited.
- The fourth volume explicitly **does not add new Upbringings** and directs players to earlier Upbringings or authoring guidance.
- A source Upbringing can include familiar training and language mechanics; this supports keeping **the source's social/narrative component separate from mechanical class and Background entitlements**. Do not infer that a third-party Upbringing automatically supplies any given 5.x Background feature.

## Review sequence

1. **Urchin:** Test a predominantly Upbringing conversion, including language expectations and City Secrets, against a character with a separate Criminal, Sage, or Soldier Background.
2. **Noble:** Work through social standing, formative education, and the Knight variant, testing a noble-raised Soldier and a later ennobled character.
3. **Outlander:** Distinguish cultural environment from later wilderness travel; test compatibility with the existing Elven and Giant-community Upbringings.
4. **Hermit:** Determine whether a common learned-trait pattern is needed, without prescribing age or biography.
5. **Other five:** Charlatan, Entertainer, Folk Hero, Guild Artisan, Sailor; largely Background-first but each still undergoes an individual source-benefit decomposition.
6. **Supplemental 2024/Strixhaven/other Background review:** Use as examples of additional native concepts, not a guaranteed expansion to every published option.
7. **After classification and approved promotion:** Audit the *effective combined corpus* for unequal benefits, grant duplication, scope mismatches and cross-cultural compatibility, then recommend any targeted balance changes.

Keep source `working/` evidence non-authoritative. Only individually approved native allocations belong in `native/`. No executable C#/consumer implementation is authorized by this classification.


## Approved Urchin-to-Upbringing conversion direction — 2026-11-04

**Owner approval:** Treat the 2014 PHB Urchin primarily as an **Upbringing source**, in the same manner that historical Giant Foundling was fully converted to an independent Giant-community Upbringing. Rework the concept and mechanical allocation rather than retaining an Urchin Background alongside it.

The provisional working label is **Street-Raised Upbringing**, not yet a final native ID or finalized name. The intended concept is being shaped by life among the streets, markets, lanes, and informal networks of a substantial settlement. Orphanhood, poverty, and criminal activity are **not** required facts. Any Species may choose the Upbringing, and a Street-Raised character may select Criminal, Sage, Soldier, or another Background independently.

**Approved design direction (not yet a final numeric grant package):**

1. **Language:** Reflect an actual language used by the formative community, commonly Common but not universally so. Do not infer a language from the original 2014 Urchin, which had no Background language grant.
2. **Flexible training:** Preserve Stealth, Sleight of Hand, Disguise Kit and Thieves' Tools as useful **source candidates**, but do not force every Street-Raised character into criminal or deceptive specialization. Expand the options to include credible observational and survival training.
3. **Urban traversal:** Retain an adapted version of the functional *City Secrets* idea, rewritten in original Dorks & Dice prose and appropriately scoped to navigating a settlement outside combat.
4. **No Upbringing equipment package:** Original Urchin starting items do not become a second inventory entitlement; starting equipment belongs to the selected Background and normal inventory.
5. **Separate formative experience from later occupation:** Do not reintroduce Urchin as a selectable Background merely because the 2014 source used that category. Character concepts may combine this Upbringing with independent Backgrounds.
6. **Later balancing:** The exact quantity of learned skill and competency grants, travel benefit power, and comparison to Giant/community Upbringings remain explicitly **provisional** until the wider corpus balance pass.

### Concrete training and feature candidates for the next reconciliation

**Skill pool (proposed, not individually approved):** `skill.stealth`, `skill.sleight-of-hand`, `skill.perception`, `skill.insight`, `skill.survival`. A broad pool can represent observing urban crowds, evading threats, managing scarce resources, or practiced manual dexterity without requiring criminality.

**Competency pool (proposed, not individually approved):** `competency.disguise-kit`, `competency.thieves-tools`, `competency.cooking`, `competency.forgery`. The first two preserve the historical source options; the others are existing reviewed native semantic IDs for practical street and informal-trade experience. Neither training category is an inherent Species/Ancestry grant.

**Urban travel candidate, mechanical scope not yet approved:** A character who can navigate accessible connected passages in a substantial settlement can guide companions through suitable routes to reduce noncombat travel time between locations. Whether the historical **half-time** effect should be preserved exactly, and how familiarity, obstructions, settlement scale and inaccessible areas limit it, should be decided without prematurely changing the global travel system. It does not grant combat movement bonuses, ignore barriers, or reveal hidden locations automatically.

**Unresolved for selection:** Which skill and competency alternatives are acceptable, how many choices each character receives, whether a street-raised character receives any fixed training beyond language and urban navigation, and the detailed urban-route rule. These should be reconciled before writing `native/upbringings/street-raised.json`; no incomplete mechanically active native Upbringing has been introduced by this approval.

**Copyright boundary:** Urchin is not an SRD 5.1 Creative Commons Background. The general formative concept and functional mechanics may inform Dorks & Dice design, while new native descriptions and feature explanations must be independently authored. Do not reproduce PHB narrative or suggestion tables.


## Approved Street-Raised language, training pools, and Urban Navigation — 2026-11-04

**Owner decision:** The specific community-language treatment, five-option Skill pool, four-option Competency pool, and the independently authored Urban Navigation rule below are **approved**. This supersedes the preceding *candidate/not individually approved* labels for those particular elements. The **number of Skill choices and Competency choices is still unapproved**, so the complete package is **not yet eligible for a selectable native Upbringing record**. Its name remains provisional pending final content selection.

### Approved language rule

Learn **one language used by the community that shaped your upbringing**. Common is a valid choice when spoken in that settlement or formative community, but it is not universally mandatory. The player chooses a language that fits the actual setting and their experience; this is a learned Upbringing grant, not a Species trait.

### Approved Skill choices — pool, not quantity

- `skill.stealth`
- `skill.sleight-of-hand`
- `skill.perception`
- `skill.insight`
- `skill.survival`

### Approved Competency choices — pool, not quantity

- `competency.disguise-kit`
- `competency.thieves-tools`
- `competency.cooking`
- `competency.forgery`

These are legitimate alternatives representing different urban experiences. **Do not award all nine options**, presume any particular choice counts, grant skill ranks twice, or imply every street-raised character engaged in crime. The final quantity/combination rules require later cross-Upbringing benefit review.

### Approved Urban Navigation rule — native-original expression

> **Urban Navigation.** When traveling outside combat through a substantial settlement, you can identify efficient routes through its accessible streets, alleys, and passages. You and companions following your guidance can travel between locations in half the usual time. This benefit does not bypass physical barriers or grant additional movement during combat.

The **half-time** traversal effect is intentional and approved. Its domain is accessible city/settlement routes and **noncombat travel**, not every environment, instantaneous travel, combat movement, forced entry, or supernatural knowledge of concealed routes. If a campaign encounter introduces a special obstruction, it is resolved normally rather than bypassed by the feature. Do not require a detailed street-map subsystem to implement this content.

### Finalization boundary

- The historical PHB Urchin is **fully redirected to an Upbringing design**, not duplicated as a native Background. The underlying functional mechanics are adapted without reproducing protected non-SRD prose.
- All Species may select this experience once its package is finalized. Street-Raised can coexist with a separate Criminal or other Background; cross-grant duplicates are resolved under the later general policy.
- No separate Urchin equipment grant, compulsory orphanhood, compulsory poverty, or compulsory criminal conduct.
- **Remaining for native promotion:** Number of Skill selections, number of Competency selections, potential other fixed grants (none approved so far), final name/ID, and comparison of the full package against existing native Upbringings. The approved benefits should be retained intact while these remaining decisions are resolved.
- **Implementation is deferred** as with the other Pillar 3 Backgrounds: authored native content is distinct from working consumer grant projection.


## Noble and Aristocratic Upbringing — corrected design direction (2026-11-04)

**Owner direction:** Use **Aristocratic Upbringing**, not `Court-Raised`. The broader term includes upbringing in noble households, estates, and aristocratic social environments, without requiring that a character was physically raised at court or personally holds a hereditary title.

**Keep three independent concepts, with different responsibilities:**

- **Aristocratic Upbringing:** Formative cultural environment and acquired social or educational practices, language, etiquette, and training. Does **not** automatically confer noble rank, current wealth, political recognition, or privilege. A household attendant raised in that environment may qualify.
- **Noble Background:** Actual experience and recognized standing in aristocratic structures, with contextual social access, political ties and privilege as appropriate. A character can become noble in later life without Aristocratic Upbringing.
- **Knight Background:** The owner corrected the earlier recollection: **Soldier** was the previously designed Background, not Knight. Design **Knight as a new, independent Background**, using Soldier and the 2014 Knight material as mechanical references where appropriate. Do not create it as a Noble or Soldier variant or automatically inherit either package.

**Repository verification and owner correction (2026-11-04):** The rewrite does **not** contain `background.knight`. The owner clarified that **Soldier**, not Knight, was the previously developed Background. Knight is therefore a **new Background candidate**. Its independent mechanics, privileges and any retainer relationships require reconciliation; the 2014 PHB Knight variant is research evidence only.

**Historical mapping:** The 2014 PHB `Noble (Knight variant)` is **source evidence only**. For the Dorks & Dice native allocation, use `Noble` and separately existing `Knight` Backgrounds. Do not confuse the source edition's variant classification with the desired system's authoritative Background taxonomy.

**Still unapproved:** Exact Aristocratic Upbringing skill/competency choices or choice counts; the Nobility feature scope and starting equipment; mapping of rank and retainer mechanics between the existing Knight and Noble definitions; final native IDs/wording. No new native file is authorized by this classification alone.

## Aristocratic Upbringing and independent Noble — first mechanical candidates (2026-11-04)

**Status:** Source-backed design candidate. The owner **approved the name Aristocratic Upbringing** and the independent concept split. The detailed pools, feature, numbers, and Noble allocations below are **proposals only**. Do not add a native selectable record before resolving them.

### Source and design distinction

The official 2014 Noble Background offers History and Persuasion training, one type of Gaming Set training, one chosen language, the social feature Position of Privilege, and fine clothes, signet ring, pedigree scroll, and 25 gp. Its Knight variant replaces the feature and is **not a native inheritance relationship**. See https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background and https://www.dndbeyond.com/backgrounds/10-noble.

**Correction to an earlier proposal:** Do not take History or Gaming Set training **away** from Noble solely because Aristocratic Upbringing might also teach them. An individual who gains noble status later in life must still be able to take Noble without relying on a prescribed childhood. Design each package independently; duplicate choices between Upbringing and Background need a general overlap policy later.

### Aristocratic Upbringing — proposed

- **Meaning:** Formative immersion in aristocratic social customs, education, family service, household relationships or elite institutions. The character need not hold a title, belong biologically to a noble family, possess current wealth, or have attended a royal court. Merely residing on aristocratic lands without exposure to those practices does not imply all such training.
- **Language:** One language regularly used in the character's formative household or social environment, selected according to the setting. Common may qualify. This is cultural learning, not a bonus language awarded merely because someone is of noble blood.
- **Skill selection pool:** History, Insight, Persuasion, Deception, and Performance (`skill.history`, `skill.insight`, `skill.persuasion`, `skill.deception`, `skill.performance`). Represent formal education, reading social expectations, diplomacy, intrigue or ceremonial presentation. The number of choices is deferred.
- **Competency selection pool:** A type of Gaming Set (`choiceCategory: gaming-set`) or an existing native competency such as `competency.calligraphy`, `competency.painting`, or `competency.cooking`, depending on upbringing. Art and household service are equally legitimate; neither wealth nor leisure-game training is mandatory. The number of choices is deferred.
- **Aristocratic Protocol — candidate cultural feature:** When dealing with aristocratic communities whose customs you know, you can identify ordinary ranks and titles, forms of address, etiquette, and accepted procedures for requesting an introduction or audience. This conveys practical social knowledge, **not** admission, an automatic audience, influence, land, wealth, recognized title or command over others. Foreign or unfamiliar conventions may require ordinary inquiry or learning.
- **No separate starting equipment, social rank, mandatory servants, retainers, inherited wealth, or class-dependent feats.** Equipment comes from the selected Background and ordinary inventory.

The feature provides recognizable **information and procedure**, unlike Noble's potential *social access based on actual standing*. If it creates an unreasonable asymmetry with other Upbringings, revise it deliberately during the corpus-wide review rather than silently stripping it from the approved concept.

### Noble Background — independent 2014-derived candidate

- **Meaning:** Meaningful experience holding or exercising recognized noble standing, including a title acquired later in life. No Aristocratic Upbringing prerequisite.
- **Skills:** Retain 2014 History and Persuasion as the primary baseline for comparison; consider more flexible later revisions on their own merits, not as a forced subtraction to fund the Upbringing.
- **Competency:** One Gaming Set type as in 2014; this is a category choice pending shared training execution.
- **Language:** Retain one chosen learned language as the 2014 comparison baseline, subject to review whether noble experience justifies this in every setting. Do not tie it to hereditary biology.
- **Position of Privilege:** Contextual recognition and social access where the character's actual title or standing is acknowledged; not an automatic power over everyone or guaranteed entrance to hostile institutions. The character's learned skills persist if recognition changes.
- **Starting equipment:** Evaluate the 2014 package of fine clothes, signet ring, evidence of title or pedigree, and 25 gp as ordinary inventory. Acquired status need not imply biological lineage: a new grant of rank may be represented by a charter, commission, or other relevant record.

### Independent Knight Background — later candidate

Knight is **not** an existing selected native Background and will be designed as a **distinct** Background. Historical 2014 Knight and Retainers material may inspire it, as may Soldier's military-service training, but it will not be a mechanical subtype with automatic inheritance. Review Knight alongside Noble when testing distinctions among social privilege, recognized service, mounted/martial experience, and followers. Do not make Retainers universal or create a follower subsystem by assumption.

### Cross-component test cases and unresolved decisions

1. **Aristocratic + Noble:** The character has cultural education plus actual noble status. Overlapping History, language or gaming benefits await general duplication handling, not automatic stacking.
2. **Aristocratic + Criminal:** Knowledge of aristocratic customs remains, but criminal contacts come from the separate Background; no noble recognition is implied.
3. **Non-aristocratic + Noble:** An ennobled individual receives the Noble Background's full learned package without a formative prerequisite.
4. **Aristocratic + Knight:** A person raised within a noble household enters knightly service; upbringing does not independently confer knighthood.
5. **Non-aristocratic + Knight:** Knightly recognition and acquired martial obligations are possible without noble childhood or any Noble Background.

**Next approval points:** Aristocratic Skill pool, Competency pool, community-language allocation, whether Aristocratic Protocol is a distinct mechanical feature, and Noble's proposed retention of its 2014 learned grants. Final choice counts and total-benefit comparison remain deferred until the first general audit.


## Approved Aristocratic Upbringing and Noble Background baseline — 2026-11-04

**Owner approval:** The immediately preceding reconciliation's proposed **Aristocratic Upbringing** language, skill options, competency options and *Aristocratic Protocol* are accepted. Its training-selection **counts are deferred** to the corpus-level balance review. The parallel proposal to retain **2014-derived Noble** as a self-contained, independent Background baseline is accepted. Neither implies that the character must select the other.

### Aristocratic Upbringing — approved content, unresolved counts

- **Scope:** Formative exposure to aristocratic customs or household life; any Species, family role, or social status can qualify where the character actually acquired those practices. No required noble birth, title, wealth, royal court residence, servants, or starting equipment.
- **Language:** Learn **one language used in the formative aristocratic household or social environment**, including Common if appropriate.
- **Skill choice pool:** `skill.history`, `skill.insight`, `skill.persuasion`, `skill.deception`, `skill.performance`.
- **Competency choice pool:** one Gaming Set type (`choiceCategory: gaming-set`), `competency.calligraphy`, `competency.painting`, `competency.cooking`.
- **Aristocratic Protocol:** When dealing with aristocratic communities whose customs you know, you understand ordinary titles, ranks, forms of address, etiquette, and ways to request introductions or an audience. This learned familiarity gives no automatic admission, audience, title, authority, wealth, or control over another person. Unfamiliar customs may require inquiry or ordinary adjudication.
- **Unresolved:** The number of choices from each pool and final native ID. No selectable native Aristocratic Upbringing is added until its total grants are coherent. Do not automatically award every option.

### Noble Background — approved 2014-derived native baseline

- **Skills:** History and Persuasion training.
- **Competency:** Choose one type of Gaming Set to train.
- **Language:** One additional language of choice, acquired through noble social experience. Distinct from a language learned in Upbringing.
- **Social feature:** Position of Privilege, representing recognized noble rank and access among institutions and people who acknowledge the character's standing. Recognition does not mean universal command or entrance everywhere; current standing may differ by jurisdiction and relationship.
- **Inventory:** Fine clothes, a signet ring, evidence of pedigree **or** an equivalent document of bestowed status (such as a charter or commission), and 25 GP in a purse. Ordinary inventory; not automatically inherited from Aristocratic Upbringing.
- **Narrative:** No required biological noble lineage or aristocratic childhood, and no compulsory moral, political or familial outlook.
- **Do not grant** fixed 2024 ability increments or the 2024 Skilled Origin feat. Knight is a distinct future Background, not a Noble variant.
- **Execution:** Native JSON is selected content; projection, category-qualification resolution, item identity and consumer execution remain deferred until integration work.

**Interaction tests:** Aristocratic + Noble may overlap History, language and gaming grants; use the later general duplicate policy rather than silently stacking ranks or replacing unrelated benefits. A character with other Upbringing can also select Noble, and Aristocratic upbringing does not independently imply recognized rank.


## Independent Knight promoted to native Background — 2026-11-04

**Owner-approved resolution superseding earlier Knight intake hypotheses:** Knight is a **new, independent native Background**, not a subtype of Soldier or Noble. The historical 2014 `Noble: Knight` source is reference material only; its **Retainers** mechanic is adapted in original Dorks & Dice wording.

**Native file:** `data/rulesets/first-party-001/native/backgrounds/knight.json`, independently selectable as `background.knight`.

**Grant summary:** Persuasion; choose **one** of Athletics, Animal Handling, Ride, History, Religion, Arcana, Nature; choose **one total** Gaming Set or Musical Instrument type; learn one language associated with knightly duties, order, patron or training; three noncombat retainers including a squire; Knightly insignia or commission, service clothing, token, pouch, **10 gp**.

**Retainer limits:** Noncombat support only, no dangerous excursions, departure for repeated danger or mistreatment, DM adjudication. Wages, housing, replacement, travel arrangements, inventory, level advancement and follower management remain unspecified. Do not automatically grant an additional institutional privilege or noble status.

The Knight source decomposition and full approval boundary are recorded in `docs/rewrite/character-origins/pillar3-knight-reconciliation.md`. Other unapproved Upbringings and Backgrounds remain subject to individual reconciliation. Native execution and balance audit still follow the broader project plan.


## Spy promoted independently; Criminal variant retired — 2026-11-04

**Owner-approved resolution:** The PHB's 2014 Spy variant is used as comparative source material only. It is **not** a selectable variant of Criminal or an inherited template. Spy has an independent selected-native definition at `data/rulesets/first-party-001/native/backgrounds/spy.json`.

**Native grant allocation:** Two distinct Skills chosen from Deception, Stealth, Investigation, Insight, Perception, Persuasion; one Competency chosen from Disguise Kit, Forgery, Thieves' Tools, Calligraphy; one language relevant to intelligence work; the bounded **Intelligence Sources** relationship feature; clothing, notes, ink, a writing instrument, a pouch and **10 gp**.

**Separation:** The Criminal record retains its original mechanical grants and equipment. Its former Spy `suggestedVariants` entry has been removed; covert intelligence work is not presumed unlawful. A character's espionage may still overlap criminal experiences in fiction without mandating inheritance or duplication.

**Pending:** Generic choice execution for multiple distinct skills, competency options, inventory item resolution, and corpus-wide comparison. No special espionage system or automatic false identity was introduced.

See `docs/rewrite/character-origins/pillar3-spy-reconciliation.md` for the source comparison, alternatives, and final approval record.
