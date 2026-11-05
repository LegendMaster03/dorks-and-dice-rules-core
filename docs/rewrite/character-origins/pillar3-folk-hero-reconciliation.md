# Pillar 3 — Folk Hero Background and Rural Upbringing Reconciliation

**Status:** Folk Hero approved and authored as an independent native Background. Earlier proposals remain preserved as research; the final approved grant package appears below.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`

## Primary source and scope

The 2014 D&D Basic Rules identify **Folk Hero** with an earlier ordinary occupation, a consequential **Defining Event**, recognition among common people, and the **Rustic Hospitality** feature. The source assumes a socially modest, often rural origin, but the mechanical significance of the defining event is **recognition earned through action**, not the character's inherited status or childhood.

Official 2014 reference: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background  
Official 2014 Folk Hero entry: https://www.dndbeyond.com/backgrounds/9-folk-hero

**2014 baseline:**

| Dimension | Original Folk Hero |
| --- | --- |
| Skills | Animal Handling and Survival |
| Tool proficiency | One kind of artisan's tools |
| Vehicle proficiency | Land Vehicles |
| Languages | None |
| Social feature | Rustic Hospitality |
| Equipment | One artisan tool set, shovel, iron pot, common clothes, pouch with 10 gp |
| Defining narrative | An event causes ordinary people to regard the character as a champion |

In source, ordinary people may offer shelter, rest and concealment, including help against those seeking the character, **without risking their lives**. The original text ties this to being from common folk. Dorks & Dice should instead connect the relationship to **earned solidarity and recognition**: a hero can come from a wealthy, urban, aristocratic, nomadic or wilderness-raised formative environment and still have standing among ordinary people.

The PHB expression is not reproduced in native text; its gameplay function is the comparison basis. No 2024 ability-score increase, Origin feat or class feature is imported.

## Classification decision proposed

**Folk Hero is an independent Background**, grounded in a recognized consequential act or sustained conduct on behalf of a community. Its impact, public reputation and ongoing relationships are acquired experiences. A single significant deed can be sufficient to establish the background's identity if it plausibly produced enduring recognition or support; no official title, military commission or predetermined heroic alignment is required.

**Rural Community Upbringing is a separate candidate, not inherited.** Growing up on a farm, in a hamlet or in a working household may account for Animal Handling, land-based practical training and local language, but should not by itself grant a heroic reputation. Someone could be raised in a city, a noble household, or the native Wilderness-Raised Upbringing and later become a Folk Hero. A rural-raised Sage or Guild Merchant need not have saved anyone.

**Identity must not become an unrestricted status:** Folk Hero need not be celebrated worldwide, morally flawless, universally admired, lawfully recognized, or immune to criminal consequences. A defining deed could involve preventing a fire, stopping an oppressive local official, organizing emergency relief, exposing wrongdoing, or shielding a threatened community. The motivation, methods, beneficiaries and outside opinions vary. Do not force the source's optional event table or biography onto every character.

## Main mechanical tension: source-shaped training versus earned hero status

The original **Animal Handling, Survival, artisan tools and Land Vehicles** package reflects an assumed working/rural past, not necessarily the experience of saving a community. It is coherent for a shepherd who defended a hamlet but arbitrary for a city physician, merchant, teacher, or aristocratic rescuer. This is a stronger case for modifying the source's mechanical allocation than Guild Artisan, whose trade training genuinely follows its vocational identity.

### Model A — exact 2014 baseline retained

- Fixed `skill.animal-handling` and `skill.survival`.
- One existing native crafting Competency corresponding to a chosen artisan's tools type; full native facets may exceed the historical tool-only training.
- Land Vehicles qualification, in the established native shape `grantType: qualification-training`, `qualificationCategory: vehicle`, `vehicleType: land`.
- No language.
- Folk Hero social refuge feature.
- Artisan tool set, shovel, iron pot, common clothes and 10 gp.

**Advantages:** Straightforward source comparison; no new choice group type aside from a craft-category choice already present on Guild Artisan.  
**Disadvantages:** Treats a specific rural trade/transport package as obligatory for any community-recognized hero; adds substantial vocational grants to a Background whose distinctive identity is social and reputational. It recreates part of the childhood/livelihood coupling already rejected elsewhere in Pillar 3.

### Model B — deed- and livelihood-aware, independent Background (recommended)

**Skill training:** Choose **two distinct** Skills from:

- Animal Handling — community animal rescue or practical husbandry;
- Survival — emergency response, outdoor rescue or resource gathering;
- Athletics — strenuous rescue or protection;
- Medicine — treating the injured during a crisis;
- Persuasion — organizing aid and rallying cooperation;
- Insight — understanding the needs and motives of those helped;
- Intimidation — confronting a threat or coercive authority;
- Investigation — discovering wrongdoing or the source of a community danger.

Native IDs: `skill.animal-handling`, `skill.survival`, `skill.athletics`, `skill.medicine`, `skill.persuasion`, `skill.insight`, `skill.intimidation`, `skill.investigation`. **All eight have been verified in `native/skills.json`.** A selected pair may match the original Animal Handling and Survival, but it is not mandatory.

This uses Spy's established native pattern of `grantType: skill-training` with two distinct selected Skills, rather than inventing automatic replacement training for Upbringing overlap. Athletics is composite, without separately training Climb/Jump/Swim.

**Additional practical training (provisional): Choose one total**, based on the character's earlier experience or the work surrounding the defining event:

1. One existing native crafting Competency appropriate to the person's practical experience, using a source-shaped `crafting-competency` category and trade constraint. Existing native Crafting Competencies have **unequal facets** (crafting skill, tool proficiency, or both), as documented for Guild Artisan.
2. A **Land Vehicles qualification**, using the approved Soldier representation.

This is **one choice, not both**. It can be modeled with a mixed-result option group like Guild Merchant's Navigation-or-language choice, although generic consumer execution is deferred. **Open concern:** Even this broad menu assumes either a crafting/trade or vehicle background, and may be too restrictive for performers, physicians or purely civic organizers. A third option based on a verified profession specialty or a different earned training benefit could be evaluated, but should not be invented or added without a clear equal-strength rule. This is the main recommendation requiring judgment before approval.

**Languages:** No language from the Background. Languages learned through Upbringing remain independent; a heroic event does not automatically teach another full language.

### Feature: Community Refuge (original native wording candidate)

> **Community Refuge.** Because people know or can credibly learn of what you have done for others, you can seek temporary shelter, modest hospitality, or a discreet place to recover among ordinary people who sympathize with your deeds. Such supporters may help keep your whereabouts from casual inquiry or people pursuing you when they believe doing so is justified. Their help depends on recognition, trust and practical circumstances; they will not be expected to risk their lives, sacrifice their livelihoods, or defy overwhelming danger. This feature grants neither authority over them nor universal protection from laws or enemies.

**Functional relationship to the 2014 feature:** Preserve the meaningful possibility of respite and concealment, bounded by real social sympathy and danger, without presuming a peasant childhood. Where a supportive community can safely provide shelter or concealment, the feature is intended to confer actual help, not merely permission to attempt a normal Persuasion check. An established deed may be vouched for outside its original locale, but it is not automatically famous everywhere.

**Companions:** The historical feature explicitly addresses the character. Extending shelter to traveling companions when the hosts can reasonably accommodate them is plausible, but **does not confer an unrestricted party-wide guarantee**. Recommend allowing companions under ordinary capacity/safety adjudication without promising accommodation for arbitrary numbers.

**Recognition:** A defining deed creates community ties, not universal popularity. A character's actions may be celebrated in one settlement, controversial in another and unknown elsewhere. The DM decides which sympathizers and institutions exist. The feature is neither Noble's Position of Privilege, Soldier's Military Rank, Acolyte's faith shelter, Spy's intelligence contacts nor Guild Artisan's professional organization.

**Reputation changes:** Betraying supporters or endangering them can reduce aid without erasing prior Skills or training. No numeric Renown, faction reputation or social-standing subsystem is approved here.

### Starting equipment

**Source-faithful initial alternative (A):** artisan tool set appropriate to craft, shovel, iron pot, common clothes, pouch with 10 gp.

**Recommended adaptable candidate (B):**

- A set of **practical tools appropriate to the chosen craft**, *if the crafting option is selected and a corresponding set exists*; **or** a shovel and iron pot as mundane working possessions *if Land Vehicles training is selected*.
- **Common or work-appropriate clothes**.
- A nonmagical **token or memento of the defining event**, which could be a letter, ribbon, damaged implement or simple keepsake. No public official insignia or magical item is automatically granted.
- A pouch containing **10 gp**.

The two tool/working-gear alternatives are not proven equal in price or practical value. This equipment allocation is deliberately *not approved* yet and needs a concrete choice because a craft tool set can be much more valuable than mundane equipment. Do not award a cart, livestock, land, a house, title or additional monetary compensation automatically. All selected possessions are ordinary inventory; final item identities/materialization are deferred.

**Alternative simplification:** Keep shovel and iron pot for everyone, and include the one corresponding artisan tool set only if the craft option is selected. This might give craft heroes more equipment value. An ordinary memento tied to the event is narratively appropriate but not a required magic/relic.

## Rural Community Upbringing — distinct future candidate

Do not promote a Rural Community Upbringing with this Background. It may reflect a formative **settled agricultural or village household**, including language, practical duties, local customs and community ties. Do not make it a generic peasant class, impose poverty, grant Rustic Hospitality/Community Refuge by default, or assume all rural people have animal training. Compare with:

- **Wilderness-Raised:** formative gathering from uncultivated environments and its approved conditional six-person Wilderness Provisioning; rural agriculture is not automatically wild foraging.
- **General Goliath:** a culturally specific learned language plus chosen Skill and chosen Competency, available to any Species.
- **Street-Raised:** formative urban navigation and street practices; its selection counts remain unresolved.
- **Aristocratic:** formative etiquette and exposure to institutions, not automatically Noble rank.

A Folk Hero's background is a separate earned relationship, not a granted or inherited Rural Community feature.

## Corpus tests and design risks

1. **Wilderness-Raised + Folk Hero:** Wilderness Provisioning is separate from hero supporters and their shelter; overlaps from selected Survival use general duplicate semantics.
2. **Rural-raised Sage and city-raised Folk Hero:** The former lacks heroic status; the latter has it without compulsory Animal Handling, land vehicles or farm tools.
3. **Noble/Aristocratic + Folk Hero:** May earn support among ordinary people independent of noble status, without automatically receiving Position of Privilege.
4. **Soldier + Folk Hero comparison:** Soldier has two fixed military Skills, gaming-set choice, Land Vehicles and Military Rank; Folk Hero's flexible training/reputation must not simply duplicate military standing.
5. **Acolyte + Folk Hero comparison:** Acolyte hospitality comes through an institution of shared faith; Folk Hero refuge comes through sympathy and recognition of deeds.
6. **Criminal or Spy comparison:** Help hiding is not a universal permission to evade lawful consequences; social refuge is not a criminal communications channel or guaranteed intelligence source.
7. **Guild Artisan comparison:** Craft training is an actual vocational identity for Guild Artisan; for Folk Hero it is at most one contingent earlier work experience, not guild membership.
8. **Feature value:** Unlike guaranteed travel or six-person food gathering in suitable environments, shelter may be scarce or risky; the feature should remain meaningful when applicable.
9. **Training/gear parity:** A source-like *craft plus Land Vehicles* grant would be stronger than the proposed *craft or Land Vehicles* choice; do not silently claim numerical equivalence to 2014. Tool set value and the exact equipment alternative still need review.
10. **Moral assumptions:** A person regarded as a hero by one group may be opposed elsewhere. No global alignment, guaranteed charitable motive or special lawful authority is implied.

## Approval boundary

**Recommendation for owner review:** Adopt independent Folk Hero with **two distinct Skills selected from eight**, **one practical training choice between an appropriate native crafting Competency and Land Vehicles**, **no new language**, **Community Refuge**, and a 10 gp ordinary equipment package tied to the training choice and defining event.

**Not yet decided:** Whether Folk Hero should retain exact 2014 fixed training, whether the proposed two-category vocational choice is too narrow, how to equate the craft-tool and noncraft equipment alternatives, and how far Community Refuge can support companions without becoming an unrestricted group shelter power.

No `data/rulesets/first-party-001/native/backgrounds/folk-hero.json` is approved. No Rural Community Upbringing is approved. Do not author either native record, modify existing grants, add global reputation accounting or create new consumer/runtime mechanics until a separate owner decision.


## Owner agreement and practical-training refinement — 2026-11-04

**Agreement received:** Folk Hero remains an **independent Background** defined by earned recognition, with flexible Skills and **Community Refuge** rather than a mandatory rural, agricultural or social-class history. A possible **Rural Community Upbringing** receives a distinct later reconciliation; it is not automatically inherited. This agreement is **not authorization to promote a finished native Folk Hero**, since the practical qualification and linked equipment were expressly left unresolved.

### Practical-training alternatives tested

| Option | Mechanical package | What it solves | Problem |
| --- | --- | --- | --- |
| **A. 2014 fixed grant** | Artisan tool proficiency and Land Vehicles, in addition to the two Skills | Closest to source baseline | Forces rural working-history assumptions onto a city-born rescuer, scholar, healer or organizer |
| **B. Mixed craft/vehicle choice** (earlier draft) | One crafting Competency **or** Land Vehicles | Narrows the extra grant and permits either source tradition | Still forces a vocational identity; craft Competencies have unequal facet strength; a noncraft, nonvehicle deed has no plausible option; equipment must branch |
| **C. No compulsory vocational grant** (recommended) | Two selected Skills plus Community Refuge, without tool/vehicle grant | Training follows consequential deeds rather than presumed childhood employment; no forced trade or arbitrary extra language | Fewer mechanical training grants than many 2014 Backgrounds; may need later corpus-wide power review |
| **D. Extra flexible training** | A third distinct Skill, a crafting Competency, or a vehicle qualification as one mixed-result choice | Can represent practical, social, or specialist backgrounds | Multiple fundamentally different benefit types, different power levels, more projection complexity, and an incentive to maximize mechanical value rather than characterize the deed |

**Recommended selection:** **C**, omit the mandatory tool/vehicle grant altogether for the first native Folk Hero package. The character's ordinary childhood, past livelihood, and personal equipment may still include tools and transport, but their training is **not an inherent consequence of being a recognized Folk Hero**. No existing general starting-trade-training subsystem grants them automatically; players can obtain them through other applicable character options or later play.

Removing vocational training represents a **deliberate mechanical departure** from the original 2014 Folk Hero. It is not numerically claimed to equal the historical two qualifications. The defining feature offers bounded social shelter and protection when recognition and sympathetic supporters exist; its actual utility varies with campaign. Compare the package across the first corpus before changing it purely for grant-count parity.

### Revised Folk Hero package — proposed for final approval, not yet approved

- **Identity:** You performed a meaningful deed or sustained acts that earned recognition and practical solidarity among a community. A rural upbringing is possible but not a prerequisite; noble birth, city life and a morally complicated deed are all possible.
- **Skills:** Choose **two distinct** from `skill.animal-handling`, `skill.survival`, `skill.athletics`, `skill.medicine`, `skill.persuasion`, `skill.insight`, `skill.intimidation`, `skill.investigation`. The 2014 Animal Handling/Survival pair remains selectable.
- **Other training:** **No mandatory Competency, vehicle qualification, language, or extra Skill** in this first-pass package. Rural upbringing and prior occupations are separate sources of learning; no replacement is silently invented.
- **Feature:** **Community Refuge** as drafted earlier: recognized supporters offer shelter, modest hospitality or concealment from casual inquiries when justified, provided help is feasible and does not require risk to their lives or livelihoods. They retain agency; recognition is contextual rather than universal. Companions may be accommodated when supporters can realistically offer help, not as an unlimited party-wide entitlement.
- **Starting possessions:** Common or practical clothing, a nonmagical keepsake or testimonial connected to the defining deed, one ordinary personal item associated with prior work or the incident **subject to DM determination without guaranteed monetary value or training**, and a pouch with **10 gp**. No artisan tool set, cart, working livestock, office, land, inherited family status or guaranteed rewards. All possessions enter ordinary inventory; exact item selection/materialization awaits shared equipment work.
- **Optional narrative prompts:** What deed produced the reputation? Who witnessed it? Which people still support the character? Where is the deed disputed or unknown?
- **Cross-corpus tests:** Compare this Background with Soldier and Knight standing, Acolyte shelter, Guild Artisan contacts, Wilderness-Raised provisioning and Rural Community if added. A recognized person can have another occupation in biography without accumulating an additional full Background.

**Open for owner approval:** Whether to adopt Option C and this equipment set as the complete package. The feature and Skill pool had conceptual agreement but are not thereby marked as a selected-native record. No repository runtime or native content change is approved by this note.


## Approved native Folk Hero allocation — 2026-11-04

**Owner approval:** Adopt the revised **Option C** package proposed after the earlier agreement, including flexible Skills, Community Refuge, **no automatic vocational grant**, and simple ordinary equipment. This supersedes the earlier pending approval boundary while retaining its comparison of source alternatives.

**Selected native record:** `data/rulesets/first-party-001/native/backgrounds/folk-hero.json`, `background.folk-hero`, independently selectable with `status: selected-native`.

- **Skills:** Choose **two distinct** from Animal Handling (`skill.animal-handling`), Survival (`skill.survival`), Athletics (`skill.athletics`), Medicine (`skill.medicine`), Persuasion (`skill.persuasion`), Insight (`skill.insight`), Intimidation (`skill.intimidation`), Investigation (`skill.investigation`). Composite Athletics training does not independently train its child Skills.
- **Other training:** None automatically. No craft or musical Competency, land-vehicle qualification, extra Skill, or learned language is granted by this Background. This intentionally departs from the original 2014 artisan-tool-and-vehicle package.
- **Feature — Community Refuge:** Recognized or credibly attested past deeds allow the character to seek temporary shelter, modest hospitality, or discreet recovery among sympathetic ordinary people. Supporters may conceal whereabouts when justified and reasonably safe. Assistance depends on trust, recognition, available resources, and circumstances; they are not expected to risk their lives or livelihoods. Traveling companions may be hosted only where realistically feasible. No global reputation, automatic legal immunity, command authority, or guaranteed shelter for an arbitrary party size.
- **Ordinary starting inventory:** One set of common or work-appropriate clothes; a mundane keepsake/testimonial of the deed; **one DM-agreed ordinary personal item** connected to the deed or previous work, without a guaranteed monetary value or training; pouch; **10 gp**. No automatically granted artisan tools, shovel, iron pot, cart, livestock, estate, or reward.
- **Origin separation:** The recognition is earned; any Species and Upbringing can qualify. The possible Rural Community Upbringing is **still unapproved** and confers no automatic Folk Hero feature. No special inheritance or profession is implied.
- **Pending wider review:** Comparative feature strength, Skill overlap and duplication, item identity/materialization, meaningful source feature execution and group hospitality adjudication.

Native record authored without adding C# mechanics, new social-reputation tracking, feature execution, or consumer changes. Earlier unapproved options remain in the document for audit, not as current native rules.
