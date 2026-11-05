# Pillar 3 — Native Corpus Balance and Consistency Review

**Review date:** 2026-11-05  
**Ruleset:** first-party-001  
**Branch:** feature/character-origins-pillar3-life-experiences  
**Status:** Completed first full native-content consistency inventory and qualitative balance review. No unapproved grant rebalance, Aristocratic selection count, or runtime implementation is authorized by this document.

## Scope and method

This pass inspects all **16 selected-native Backgrounds** and all **12 selected-native Upbringings** under data/rulesets/first-party-001/native/, the **88** native Skill identities and **33** native Competency identities, existing authorizations in Pillar 3 intake/reconciliation notes, and relevant Species and Ancestry defaults and traits. Representative conventional and cross-cultural character combinations are examined; this is **not** an exhaustive quantitative playtest of all Species/Ancestry/Class combinations or a live consumer execution test.

The prior approved design instruction is binding: the earliest Upbringings were extracted from previously reconciled **complete Species–Ancestry–Upbringing packages**. Their standalone grant counts were **not** intended to form an equal-budget list. Compare (1) complete conventional combinations, (2) the substitution of a different compatible Upbringing, and (3) the selected Background. A short Upbringing is not automatically defective and a long one is not automatically overpowered. An Upbringing's parent contributes its benefits; a character normally selects **one effective Upbringing**, not a Species default plus a separate explicit override. Species-derived default choices are **suggestions**, not restrictions. No forced numeric point system or mechanical normalization was authorized.

Review across distinct axes:
- **Training:** Fixed and chosen Skills; multi-facet Competencies; learned languages; weapon and vehicle qualifications; composite Skills and independently ranked children.
- **Effect:** Nature, trigger, reliability, frequency and beneficiaries of Background/Upbringing features; spell access; scope of bonuses.
- **Starting resources:** Ordinary inventory contents, currency, vehicles, kits, service relationships and access rights; not just the number of inventory entries.
- **Combinations:** Duplicated proficiencies, inherited parent benefits, culturally substituted packages, class overlaps and repeated feature functions.
- **Representation:** Stable IDs, choice cardinality, typed grant fields, legacy data shapes and meaningful executable preconditions.

**Evidence boundary:** An inconsistent JSON representation is a **schema/integration risk**, not proof of currently failing execution; the generic native grant executor and inventory materialization are deliberately deferred. A campaign-dependent feature is a **playtest/balance question**, not automatically a bug. Earlier approved mechanical effects are retained pending explicit targeted revision.

## All 16 native Backgrounds

Every inspected Background currently yields **two Skill trainings** (fixed, chosen, or mixed) and **one principal background-feature**. That uniformity is a useful structural baseline. Other grant categories vary substantially.

In the table, “Competency” means one semantic native training identity or one tool type; several have more than one facet. An alternative marked “or language” is not an extra simultaneous grant.

| Background | Skills | Other training or language | Defining feature | Gold | Particular review issue |
| --- | --- | --- | --- | ---: | --- |
| Acolyte | Insight, Religion | One faith-associated language | Shelter of the Faithful | 15 gp | Healing/support and lodging can affect companions; contextual institutional access |
| Charlatan | Deception, Sleight of Hand | Disguise Kit **and** Forgery | False Identity | 15 gp | Two Competencies; Forgery includes a Skill and tool facet |
| Criminal | Deception, Stealth | Thieves' Tools **and** one Gaming Set | Criminal Contact | 15 gp | Two Competencies and potentially reliable network messaging |
| Entertainer | Performance + one of five | **One** Disguise Kit **or** Musical Instrument choice | By Popular Demand | 15 gp | Mixed benefit choice; work-for-lodging reliability |
| Explorer | Perception + one of five | One Navigation/Cartography Competency | Expedition Survey | 10 gp | Evidence-bounded but repeatable actionable geographic conclusions |
| Folk Hero | Two distinct of eight | None | Community Refuge | 10 gp | No tool/language; meaningful conditional shelter/recognition |
| Gladiator | Performance + one of three | One **specific arena weapon** proficiency; no tool | Arena Performances | 15 gp | Combat qualification, exact weapon choice and matching item resolution |
| Guild Artisan | Insight, Persuasion | One crafting Competency; one language | Guild Membership | 15 gp | Multi-facet craft choice, guild support and included craft tools |
| Guild Merchant | Persuasion + one of three | One language, plus **Navigation or second language** | Guild Membership | 15 gp | Mule **and** cart starting possessions; choice value varies |
| Hermit | Two distinct of eight | None | Discovery | 5 gp | One genuine prior revelation vs repeatable feature effects |
| Knight | Persuasion + one of seven | One Gaming Set/Musical Instrument type; one language | Retainers | 10 gp | Three ongoing noncombat NPC attendants |
| Noble | History, Persuasion | One Gaming Set type; one language | Position of Privilege | 25 gp | Standing/social privilege plus largest starting GP |
| Sage | Arcana, History | One language | Researcher | 10 gp | Reliable source-location information, not an automatic answer |
| Sailor | Athletics, Perception | Navigation **and** Water Vehicles qualification | Ship's Passage | 10 gp | Two distinct proficiencies and possible party-wide travel service |
| Soldier | Athletics, Intimidation | Gaming Set **and** Land Vehicles qualification | Military Rank | 10 gp | Two distinct proficiencies, rank-based institutional support |
| Spy | Two distinct of six | One of four tradecraft Competencies; one language | Intelligence Sources | 10 gp | Forgery/Calligraphy choices include extra facets; informant reliability |

**Background observations**

1. **Training heterogeneity is real but source-shaped:** Charlatan/Criminal have two Competencies; Sailor/Soldier add vehicle qualifications; Guild Artisan/Knight/Noble/Spy typically have a Competency plus a learned language; Acolyte/Sage have a language but no tools; Hermit/Folk Hero have neither. The fact that all have two Skills is consistent; reducing every package to an identical number of remaining grants is not justified without examining feature value and contextual access.
2. **Multi-facet Competencies complicate counting:** \`competency.forgery\` includes \`skill.forgery\` and Forgery Kit tool proficiency. Calligraphy and Painting include crafting-skill and tool facets. These are **one reviewed training identity** each but are not equivalent in mechanical effects to a standalone tool-only identity. Relevant to Charlatan, Spy, Guild Artisan, Street-Raised and the proposed Aristocratic Upbringing.
3. **Starting resources are not economically comparable by GP alone:** Noble has 25 gp; Hermit has 5 gp. Guild Merchant starts with a **mule and cart plus 15 gp**; Guild Artisan has guild craft tools. Such noncurrency possessions may matter more than the GP range. There is no authoritative native item-cost/identity materialization in this pass, so a precise price parity claim is **not** supported.
4. **Feature utility depends on frequency and adjudication:** Hermit's one-time Discovery and Sage's Researcher differ from repeated travel/resource benefits. Knight's three retainers, Guild Membership, Shelter of the Faithful, Community Refuge and Military Rank have meaningful but setting-dependent access. Compare actual opportunities and obligations rather than arbitrary feature-point values.
5. **Gladiator's one weapon proficiency is an explicit approved native deviation** from the 2014 variant's tool/equipment structure. It is a one-type proficiency, not a martial category, Fighting Style or extra attack. Whether it is stronger than an instrument or Disguise Kit proficiency depends on Class and combat access.
6. **Execution is pending, not necessarily incorrect:** Disguise-or-instrument choice, instrument/gaming types, single specific-weapon choice, certain mixed language/competency alternatives and ordinary starting-item choices need common executor/consumer support, but no new C# code is part of this content audit.

## All 12 native Upbringings — effective benefits

The “effective” column **includes parent Upbringing benefits** for the Elf and Gnome children. It does not add a Species default and an explicit alternate Upbringing at the same time.

| Upbringing | Effective learned/training benefits | Additional effects | Review implications |
| --- | --- | --- | --- |
| General Dwarf | Dwarvish; one choice of Smithing/Brewing/Stonemasonry | Scoped Stonecunning double Proficiency Bonus on stonework History | Multiplication when History is untrained; legacy choice format |
| General Elf | Elvish; one Skill of Insight/Perception/Survival | None separately | Untyped choice and unprefixed Skill IDs |
| Drow Elf | **General Elf +** Undercommon | +2 Survival for specified subterranean activities | Two learned languages; fixed scoped bonus, not an extra full Skill |
| High Elf | **General Elf +** one additional language; four weapon proficiencies | No other Upbringing feature | Four fixed weapon proficiencies may overlap class/other grants |
| Wood Elf | **General Elf +** shortbow/longbow proficiency | +2 Survival for specified woodland activities | Two fixed weapons and scoped bonus; no automatic child-Skill training |
| General Gnome | Gnomish | Shared cultural knowledge mechanically unresolved | Selectable minimal record; whether to add an actual feature is undecided |
| Forest Gnome | **General Gnome +** one of Nature/Survival/Animal Handling | None separately | Legacy untyped choice and unprefixed Skill IDs |
| Rock Gnome | **General Gnome +** Tinkering Competency | History object-lore multiplier, clockwork tinkering, Prestidigitation and magical tinkering technique | Upbringing-granted cantrip casting ability explicitly unresolved; feature overlap and tool conditions |
| General Goliath | Gol-Kaa; one of Athletics/Insight/Survival; one of Cooking/Leatherworking/Navigation | None separately | 1 Skill + 1 Competency, but no separate feature |
| Giant Foundling | Giant plus one additional language; fixed Intimidation and Survival | No independent Upbringing feature | Two Skills and two learned languages; can replace General Goliath |
| Street-Raised | One community language; one Skill of five; one Competency of four | Urban Navigation: half ordinary noncombat settlement travel time, guided companions included | Selection counts explicitly provisional; Forgery facet value; city-campaign frequency |
| Wilderness-Raised | One community language; one Skill of five | Wilderness Provisioning: food/fresh water for character plus up to five others, when obtainable | Powerful where survival logistics matter; minimal impact in supplied environments |

**Critical comparison rule:** General Gnome is markedly smaller than Rock Gnome **as an Upbringing**, but the original decomposition process did not promise standalone equality. A conventional Rock Gnome character has biological Species and Ancestry benefits as well. A mechanically justified change requires comparison of **whole characters and cross-cultural substitutions**, not merely observing that one Upbringing lists more entries.

## Structural and semantic findings

| ID | Priority | Finding | Evidence / effect | Recommended handling |
| --- | --- | --- | --- | --- |
| C1 | High — integration | **Legacy grant representations are not uniform.** | Audit found **17 field-level normalization flags across six legacy Upbringings**: General Dwarf, General Elf, Drow, Wood Elf, Forest Gnome, Rock Gnome. Examples include \`chooseOne: ["insight", ...]\` rather than \`skill.insight\`, no \`grantType\` / \`choiceCount\` for older choices, and scoped \`skill: "history"\` or \`"survival"\` rather than a canonical key. All referenced skill names have corresponding reviewed identities. | Define and test a **single content grant parsing rule**. Canonicalize only with compatibility-aware validation; never imply a scoped bonus is ordinary Skill training. The 17 flags are not 17 demonstrated bugs. |
| C2 | High — integration | **Rock Gnome Prestidigitation is mechanically underspecified.** | \`upbringing.gnome.rock\` explicitly notes the spellcasting Ability for its Upbringing-granted cantrip remains unresolved. | Resolve through general spellcasting Ability semantics and actual Species–Ancestry–Upbringing comparisons; do not invent a class ability or arbitrary default. |
| C3 | High — rule semantics | **Scoped double-Proficiency wording lacks an explicit untrained-history rule.** | Dwarf Stonecunning and Rock Gnome Artificer's Lore say double Proficiency Bonus on qualifying History checks, but do not explicitly state how the bonus works without training in general History or whether it combines with Expertise. | Confirm whether this is a self-contained scoped proficiency entitlement and how it interacts with general proficiency and Expertise; avoid counting it as automatic broad History training. |
| C4 | High — combined origins | **Cross-package duplicate proficiency behavior is unresolved.** | Multiple valid combinations overlap fixed or selected Skills/Competencies, and Class or selected Upbringing weapon grants may duplicate. Existing notes explicitly defer replacement/stacking policy. | Establish one global policy distinguishing fixed-vs-choice duplicates, one semantic Competency with multiple facets, and independently ranked child Skills. Do **not** add local compensating grants now. |
| C5 | High — choices | **Some choices require type-specific runtime resolution.** | Entertainer's mutually exclusive nested Disguise/Instrument benefit, Guild Merchant's Navigation/Language benefit, Knight's two training categories, Gladiator's one selected weapon and matching item, and Guild Artisan's eligible crafting choice use multiple JSON choice shapes. | Record a choice-contract matrix before building the common executor. Choice implementation must preserve one-total-choice semantics. |
| C6 | Medium — balance | **Multi-facet Competency options are not mechanically equivalent to tool-only alternatives.** | Forgery includes Skill and tool; Calligraphy/Painting include crafting-skill and tool; Disguise Kit/Thieves' Tools/Cooking are generally single-tool facet. | Test whether the unified Competency model reasonably values such choices; avoid fabricating shadow “kit-only” identities without a broader decision. |
| C7 | Medium — text; corrected | **High Elf language wording wrongly implied biological inheritance.** | Previous High Elf Upbringing text described “inherited Elvish” although Elvish comes from learned General Elf Upbringing. | **Corrected text only** in \`upbringings/elf/high.json\`: “Elvish learned through the General Elf Upbringing.” No grant changed. |
| C8 | Medium — content completeness | **General Gnome's shared cultural knowledge is explicitly unresolved.** | Native selectable General Gnome contains Gnomish alone; its own \`reviewNotes\` say shared cultural knowledge is approved conceptually but no exact grant was established. | Decide whether this remains an intentional language-only Upbringing or needs a concrete, reviewed feature/choice. Do not fill the gap from the Rock/Forest packages by assumption. |

**Severity denotes priority for design/integration decisions, not that the rule is unauthorized or currently failing.** Source-neutral native content is authoritative even before its C# executor; a pending executor does not revoke approval.

## Qualitative balance watchlist

| Issue | What may drive imbalance | Controlled comparison |
| --- | --- | --- |
| Urban vs wilderness resource features | Street-Raised halves accessible settlement travel time for guided companions; Wilderness-Raised provisions up to six in resource-rich environments | City-based and exploration-based sample itineraries, record actual turns/hours and daily resource impact |
| Combat-vs-tool grants | Gladiator grants one arena weapon proficiency; High/Wood Elf grant multiple fixed proficiencies; other packages grant tool training | Compare characters with and without class weapon proficiency, and choices that duplicate a martial class grant |
| Rock Gnome spell + tools | Prestidigitation and tinkering techniques can add utility and possibly new procedures; General Gnome/Forest Gnome much narrower | Same Species/Ancestry with conventional Rock Gnome vs a general/social Upbringing; compare frequency, not just benefit count |
| Social and institutional benefits | Knight Retainers, Noble privileges, Acolyte healing/care, Guild services, criminal/intelligence contacts | Urban social scenario with the same objective, available institutions and consequences; no universal compelled NPC cooperation |
| Discovery and evidence features | Hermit has one established fact; Sage identifies a possible source; Explorer derives evidenced field conclusions | Knowledge/investigation scenario measuring guaranteed information and whether use remains meaningful after the first session |
| Wealth and logistics | Guild Merchant's mule and cart, Noble's 25 gp, Hermit's 5 gp, kits and instruments as owned equipment | Materialize real item identities and prices before claims of parity; starting equipment belongs in ordinary inventory |

These differences are *candidates for playtest*, not grounds to immediately cut approved benefits.

## Representative complete-character / substitution scenarios

The following are **specific tests to perform**, not claims that all combinations are already balanced or unbalanced.

1. **High Elf Ancestry + conventional High Elf Upbringing vs High Elf Ancestry + Street-Raised:** Keep biological traits constant. Conventional package inherits Elvish + chosen general Skill and adds language + four weapon proficiencies; substitution has one community language, chosen Skill/Competency and Urban Navigation. Assess class weapon overlap and urban usefulness.
2. **Drow Ancestry + conventional Drow Upbringing vs Wilderness-Raised:** Darkvision/Ancestry traits remain inherent. Compare Elvish+Undercommon, chosen Skill and scoped subterranean Survival with one community language, chosen Skill and six-person provisioning. Do not transfer Drow culture automatically with biological identity.
3. **Rock Gnome Ancestry + conventional Rock Gnome Upbringing vs Street-Raised:** Same Species/Ancestry traits; trade Gnomish/Tinkering/History multiplier/Prestidigitation/techniques against urban training and navigation. The unresolved cantrip Ability blocks a fully executable comparison today.
4. **Goliath + General Goliath vs Giant Foundling Upbringing:** The Goliath Species traits stay constant; compare Gol-Kaa + chosen Skill/Competency against Giant + additional language + Intimidation/Survival. They are substitutes, not cumulative.
5. **Human or Elf + Rock Gnome Upbringing:** Test the approved availability of a learned culture independent of Species. Check whether the same cultural feature operates coherently on other Species; do not impose a biological prerequisite or assume Gnomish automatically appears from Gnome Species.
6. **Street-Raised + Criminal/Spy/Charlatan Background:** Choose a duplicated Skill or Competency deliberately and compare to a nonoverlapping selection. Duplicated Stealth, Sleight of Hand, Thieves' Tools, Disguise Kit and Forgery require a common policy.
7. **Giant Foundling + Soldier:** Fixed Intimidation appears in both. Test whether “ignore duplicate” leaves an unjustifiably weak combination compared with alternate Upbringings, without assuming free replacement is approved.
8. **Wilderness-Raised + Explorer:** Survival/Nature/Perception/Athletics choices can overlap; wilderness provisioning and Expedition Survey are distinct and can both operate.
9. **High Elf + Gladiator / weapon-proficient Class:** Compare one chosen arena weapon against existing four weapon grants and class proficiency. Equipment ownership never implies extra proficiency.
10. **Aristocratic + Noble (once Aristocratic is approved):** History/Persuasion, a Gaming Set and language can overlap. Learned protocol must not confer Noble's Position of Privilege. Also test Aristocratic + Soldier and non-aristocratic Upbringing + Noble.

**No global duplicate replacement/stacking rule is selected by this audit.** Specific test outcomes require either an agreed rule or recorded open questions.

## Aristocratic Upbringing — count decision prepared, not presumed approved

Already approved: independent formative scope; one appropriate household/community language; Skill pool **History, Insight, Persuasion, Deception, Performance**; Competency pool **one Gaming Set type, Calligraphy, Painting, Cooking**; **Aristocratic Protocol** (knowledge of familiar titles, etiquette and appropriate ways to seek introductions or audiences, with no automatic admission, title, authority, or property); no starting Upbringing equipment. Aristocratic need not imply noble birth, privilege or the Noble Background.

**Initial recommendation for owner approval:** **Choose one Skill and one Competency** from the approved pools, plus one language and Aristocratic Protocol. Mark those selection counts **provisional for corpus balancing**, as the owner did for Street-Raised. The Competency choice needs a mixed category construct for a Gaming Set type versus one of three existing semantic crafting/tool Competencies.

**Key distinction:** This is not proven equal to Street-Raised merely because both would have language + 1 Skill + 1 Competency + feature. The approved Aristocratic Protocol is primarily *reliable learned social knowledge*, whereas Urban Navigation halves travel time for guided companions. A useful mechanical clarification for owner consideration would be that a character familiar with a court's customs knows the **ordinary protocol without a check solely to recall it**, while uncertain or unfamiliar customs remain adjudicated normally. This would make the existing feature's intended knowledge entitlement meaningful without granting actual admission or social rank. **That clarification is a proposal, not an approved change.**

Calligraphy and Painting can carry both crafting-skill and tool facets, so one Competency choice may be qualitatively more valuable than a Gaming Set type or Cooking. This should be tested alongside Street-Raised's Forgery option. Do not auto-create \`upbringing.aristocratic\` or set the counts without approval; prior owner direction explicitly reserved them for this pass.

## Recommended resolution order

1. **Preserve existing package structure; stop content expansion.** No Pirate, Isolated-Raised, Rural Community or other new content until consistency and balance decisions are completed.
2. **Review C1–C5 mechanical semantics and duplicate rules.** Choose canonical grant and choice representation, the Rock Gnome spellcasting Ability policy, the two double-Proficiency cases, and global proficiency overlap treatment **once**, without character-specific exceptions.
3. **Approve or adjust provisional training counts:** Street-Raised's current 1 Skill + 1 Competency is implemented and may change; decide Aristocratic's 1+1 recommendation; compare the two against Goliath and Wilderness-Raised and against meaningful feature frequency.
4. **Assess comparative features and equipment:** Special attention to Guild Merchant's mule/cart, Rock Gnome magic, Knight retainers, and Forgery/Calligraphy/Painting multi-facet grants. Avoid arbitrary per-feature numeric points.
5. **Apply individually approved content corrections, then re-run the complete 16 Background + 12 Upbringing audit and the combination scenarios**. Recount when Aristocratic is promoted; do not claim 12 Upbringings remain afterward.
6. **Only after content decisions** proceed to common runtime execution, ordinary inventory, and consumer grant projection as originally planned.

## Audit verification and changes made

- Read and compared **16 Background JSON records** and **12 Upbringing JSON records**, including child/parent Upbringings, against native Skill and Competency definitions. Current catalogs have **88 Skills and 33 Competencies**.
- Confirmed all 16 Backgrounds contain two Skill trainings and one principal \`background-feature\`. Background Skill/Competency references sampled by the audit resolve to reviewed identities; nested and open-ended choice categories still require executor validation.
- Found **17 canonicalization flags in six older Upbringing records**; these include several flags on the same choice and scoped modifiers. A follow-up normalization should occur only with an explicit compatible grammar. No new Skill/Competency identities were invented.
- Compared selected Species defaults and representative Ancestry defaults; did **not** treat defaultUpbringing as mandatory biological inheritance.
- **One text-only consistency correction applied:** \`data/rulesets/first-party-001/native/upbringings/elf/high.json\` now correctly identifies Elvish as learned through General Elf rather than inherited as a Species trait. All mechanical fields remained unchanged.
- **No balance adjustments, Arbitrocratic native allocation, duplicate policy or runtime engine changes have been implemented in this audit.** Structural checks do not constitute playtest results.

## Decision summary

**Ready for owner review:** Aristocratic's proposed provisional **1 Skill + 1 Competency** choice counts and optional no-check-for-known-etiquette clarification; whether to normalize the legacy Upbringing grant representations now or alongside common executor implementation; global duplicate proficiency behavior; Rock Gnome cantrip casting Ability and scoped History multipliers.

**No evidence yet to justify:** An across-the-board nerf to Street-Raised Urban Navigation; automatic deletion of Rock Gnome features; forced equal GP inventory value; uniform number of tools/languages for all Backgrounds; new Upbringings or Backgrounds; modifications to Class mechanics to conceal origin overlap.

**Approval boundary:** This review records findings and one content-only wording correction. Any alteration to approved grant numbers, effects, skill/competency option pools, feature entitlements, or creation of native Aristocratic requires explicit owner approval.
