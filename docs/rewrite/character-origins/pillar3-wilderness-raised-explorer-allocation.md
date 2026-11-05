# Pillar 3 — Wilderness-Raised Upbringing and Explorer Background: candidate allocations

**Status:** Independently assessed candidate mechanics following owner direction to place the 2014 Outlander concept primarily in Upbringing and potentially add a separate Explorer Background. **Neither package nor any individual grant is approved.** No native content is authorized by this design record.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`  
**Source reconciliation:** `docs/rewrite/character-origins/pillar3-outlander-reconciliation.md`

## Design choice: split the historical Wanderer feature

The 2014 PHB Outlander's **Wanderer** includes both (1) memory of broad maps and geography and (2) gathering sufficient food and fresh water daily for the character **plus up to five others**, provided suitable resources exist. Original equipment consists of staff, hunting trap, animal trophy, travel clothes and 10 gp. The PHB Outlander grants Athletics, Survival, one Musical Instrument type and one language. These are **source facts**, not an approved native allocation.

**Recommendation for owner review:** Move **resource gathering** to a narrowly identified formative **Wilderness-Raised Upbringing**; assign **geographic recall, field mapping and retracing surveyed routes** to an independently selected **Explorer Background**. Do not award the two effects together automatically in either package and do not require a character to select both.

The split is a design decision rather than a claim that growing up outdoors is the *only* possible way to learn foraging or that explorers must never be able to forage. The Background/Upbringing distinction is based on the **cause of the specific grant**, not an exclusive or exhaustive categorization of real-world learning. Other training sources and play can teach similar activities under general mechanics.

### Why not put all of Wanderer in Upbringing?

A full six-person provisioning feature **and** unrestricted map memory bundled with broad Skill/Competency choices would make this Upbringing unusually powerful relative to several already-native cultures. Also, maps studied in later expeditions describe a distinct acquired professional experience. Conversely, simply giving everything to Explorer would continue treating the 2014 formative wilderness premise as an optional occupational flavor.

Do not normalize on arbitrary equal-sized grant lists. In the eventual corpus review, assess complete conventional Species–Ancestry–Upbringing combinations and separately test cross-cultural substitutions. The new option is selectable by any Species.

## Candidate A — Wilderness-Raised Upbringing

**Working name:** Wilderness-Raised. **Alternative name:** Outlander. Name and native ID are *not* approved. The more precise name avoids defining a culture by being foreign to an unspecified observer.

**Identity:** The character's formative life involved a community, household, or other sustained arrangement in which natural resources and outdoor practices were central to daily life. This does not require orphanhood, homelessness, nomadism, subsistence hunting, a particular Species, hostility toward settlements, or a single shared ethnic culture.

**Distinguish from Rural Community:** Rural Community would cover settled agriculture, hamlets and local institutions; Wilderness-Raised covers formative practical familiarity with obtaining resources from uncultivated environments. A rural person may fit both *narratively*, but the character selects their actual applicable Upbringing according to normal origin rules; neither option mechanically inherits the other.

### Proposed grant allocation (not yet approved)

| Grant | Proposed rule |
| --- | --- |
| Learned language | **One** language learned from the formative household/community. Common is valid where spoken. No mandatory invented wilderness dialect |
| Skill training | **Choose one**: `skill.survival`, `skill.nature`, `skill.animal-handling`, `skill.perception` or `skill.athletics` |
| Competency training | **None automatically** in the initial candidate baseline. Cooking, Herbalism, Leatherworking, Navigation and an instrument type are later choices to consider **instead of** or in addition to some other benefit only if comparison warrants it |
| Defining feature | **Wilderness Provisioning** (proposed below) |
| Equipment | No automatic starting equipment; ordinary inventory comes from Background and other actual sources |

**Reason to start with one Skill and no separate Competency:** The proposed provisioning entitlement has meaningful group utility. Giving it alongside a language, Skill *and* broad craft/navigation choice would risk eclipsing General Goliath (`Gol-Kaa`, one Skill and one Competency), an existing Upbringing that any Species may select. This is a substantive cross-cultural concern, not proof of exact numerical imbalance. The six-person capacity itself is also subject to approval.

**Proposed feature text:**

> **Wilderness Provisioning.** From your formative experience gathering natural provisions, you know how to obtain suitable food and fresh water in an environment where such resources are present and accessible. On a day when you have a reasonable opportunity to gather them, you can supply enough food and fresh water for yourself and **up to five other people**. This benefit does not create provisions where none exist, make unsafe water potable, bypass obstacles, or eliminate unrelated dangers of travel.

- Like 2014 Wanderer, in sufficiently resource-rich and accessible terrain this is a **dependable entitlement**, not merely permission to attempt an ordinary Survival check.
- No declared fixed hour, automatic tracker, additional hunting mini-game or requirement to personally kill an animal.
- Conditions of availability are adjudicated by the GM; a mountain village upbringing does not guarantee food from a lifeless desert.
- Not every Rural, urban, itinerant or aristocratic community automatically provides this training.

**Potential counterargument:** Supplying six people routinely could still be much more valuable than a local language and Skill and may make some travel/logistics mechanics irrelevant. The source feature already has that property; the proposed package preserves it for consideration rather than quietly weakening it.

## Candidate B — Explorer Background

**Working name and ID:** Explorer / `background.explorer`, neither approved yet.

**Identity:** Sustained experience deliberately exploring, surveying, documenting or undertaking expeditions in unfamiliar or little-known regions. Exploration might be wilderness, maritime, underground, urban-ruin, planar or archaeological. It need not involve childhood outdoors, survivalist culture, a military command, a permanent sponsoring organization or a specific class.

### Proposed grant allocation (not yet approved)

| Grant | Proposed rule |
| --- | --- |
| Skills | **Perception (fixed)** and **choose one** of Survival, Investigation, History, Nature, or Athletics |
| Competency | **Choose one** of `competency.navigation` (Navigator's Tools) or `competency.cartography` (Cartographer's Tools) |
| Language | **None automatically**; learning new languages through a particular expedition is a possible future adjustment, but not implied by all explorers |
| Defining feature | **Geographic Recall**, below |
| Ordinary starting equipment | Field notes or journal, map paper and writing charcoal/quill, traveler's clothes, pouch containing **10 gp**; no automatically owned map of an unexplored region or guaranteed information |

**Why Perception fixed:** Careful observation is a shared part of surveying and exploration; the second Skill accommodates wilderness guides, archaeologists, expedition scholars and endurance-oriented explorers. An alternate two-from-six choice pool is viable, as shown in Spy, but the fixed identity is the recommended first-pass baseline.

**Why Navigation or Cartography:** Both IDs are verified in the native Competencies catalog. Navigation aids wayfinding; Cartography aids mapmaking. Neither by itself grants Water Vehicles, Land Vehicles or automatic familiarity with every region. There is no need for a new universal Explorer Competency.

**Proposed feature text:**

> **Geographic Recall.** Your experience surveying and traveling through unfamiliar places has trained you to retain broad geographical information. You can reliably recall the general arrangement of terrain, waterways, major landmarks, settlements, and routes that you personally explored or learned from credible maps and accounts, without needing to consult those records again. You can generally retrace routes you previously traveled when their recognizable features remain accessible. This does not reveal unexplored locations, supply exact maps or coordinates, defeat concealed routes, or guarantee that a route remains safe or passable.

**Meaningful benefit:** Previously acquired broad geographical knowledge is reliably retained and usable without a fresh ordinary memory check. It does not create knowledge out of nothing or automatically identify the fastest, safest or unknown route.

**Counterargument:** An Explorer mainly discovering previously uncharted areas may have limited immediate use for recall until they have completed an expedition. The feature rewards repeat journeys, guiding others through known territory and remembering maps, but might be insufficiently active during first-time exploration. Before promotion, compare a slightly more active **Expedition Survey** alternative (for example, reliable retracing or directions after carefully surveying an unfamiliar route) without guaranteeing hazard avoidance or automating exploration.

**No free source-derived staff or hunting trap:** Those are wilderness-oriented source equipment; Explorer need not hunt or travel on foot. The recommended portable field kit fits more environments. Its value will be compared with other Background packages when item identities exist.

## The two source features do not stack automatically

| Character example | What the proposal grants |
| --- | --- |
| Wilderness-Raised + Sage | Provisioning from Upbringing; Researcher from Sage; no automatic Explorer geographic feature |
| Street-Raised + Explorer | Urban Navigation from Upbringing; Geographic Recall from Explorer; no automatic wilderness provisioning |
| Wilderness-Raised + Explorer | Both independent features, justified by two components; no doubling of food provision or geography scope |
| General Goliath + Explorer | Goliath's Skill and Competency choices + Explorer; possible overlapping Navigation or Survival follows later duplicate-grant semantics |
| Giant Foundling + Explorer | Giant Foundling's language/Skill package + Explorer; no automatic giant power or extra Survival ranks |
| Wilderness-Raised + Sailor | Provisioning and Ship's Passage operate in different circumstances; Sailor's Water Vehicles qualification is not an inherent part of Upbringing |
| Rural Community + Explorer | Professional exploration without wilderness-resource upbringing; this remains valid |

## Interactions requiring later decisions

1. **Cross-cultural choice strength:** At identical Species/Ancestry, compare Wilderness-Raised with General Goliath, General Elf, General Gnome, Giant Foundling, and any new generalized Upbringings. A full provision-supply feature may be attractive enough to outweigh a Competency.
2. **Common language:** The formative household/community language is eligible to be Common if that is what the character learned; duplicate-language resolution follows later rules rather than awarding a free substitute automatically.
3. **Composite Skills:** `skill.athletics` and `skill.perception` are composite. Training in either does not automatically train their independently ranked child Skills.
4. **Travel adjudication:** GM retains authority over feasible resources, terrain changes, hazards and available route information. Neither candidate introduces an economy, mapping engine or generic time-of-day subsystem.
5. **Outlander source's Musical Instrument:** This is research evidence, not a compulsory grant once the original Background is divided by cause. A musical tradition may be selected through a suitable future cultural option rather than imposed on every Wilderness-Raised character.
6. **Wanderer as single combined feature:** Revisit only if separating its parts produces a clearly weaker or less understandable result; no combined feature is approved by this proposal.

## Approval boundary

This is a **working recommendation**, not approved mechanics. Before native authoring, individually review the Wilderness-Raised language, one-Skill count/pool, six-person Wilderness Provisioning entitlement, and working name; then separately review Explorer's two Skills, Navigation/Cartography choice, Geographic Recall (or more active alternative), no-language decision and ordinary equipment. Preserve the earlier 2014 Outlander evidence without treating its source package as a native rule.

No new native Upbringing or Background, no modification of previously approved Backgrounds, and no runtime or consumer code are authorized by this proposal.


## Explorer feature refinement — 2026-11-04: Expedition Survey recommended

**Status:** Independently evaluated recommendation, **not owner-approved**. The original `Geographic Recall` proposal above remains historical comparison; the preferred single Explorer feature is now **Expedition Survey**, which includes bounded recall without requiring the character to have explored an area previously.

### Existing Rules Core travel contract to respect

The repository document `docs/travel-environment-mechanics.md` establishes a separation of concerns: Rules Core provides effective travel/environment definitions and source-grounded travel checks; **Hex Crawl** owns expedition map state, route/navigation transitions, cadence and DM workflow. Its 3.5e navigation rules include:

- `travel.navigation.avoid-getting-lost`: Survival checks, once per hour or portion, with contextual DCs;
- `travel.navigation.recognize-lost`: Survival checks to recognize lost state;
- `travel.navigation.set-new-course`: Survival checks to establish a fresh course;
- existing deferral of mapping/scouting responsibility integration and character-dependent modifier composition.

A new Explorer feature must not silently exempt the group from these checks, change journey distance or tempo, mutate Hex Crawl expedition state, or award an automatic general tool/skill bonus outside the unresolved shared grant contract.

### Comparison of feature designs

| Model | Meaningful benefit | Primary weakness | Judgment |
| --- | --- | --- | --- |
| **Geographic Recall only** | Dependably remember geography and surveyed routes without fresh recall checks | Little use during a first expedition into uncharted territory; memory of prior study may resemble ordinary competence | Preserve its useful component but do not make it the entire feature |
| **Expedition Survey** (proposed) | Obtain reliable, bounded orientation and route information from actual observations/usable charts while traveling; retain key details for return travel | The GM must distinguish clearly observable facts from genuinely uncertain or hidden routefinding; can feel trivial if reduced to common observation | Preferred baseline |
| **Numerical navigation bonus or automatic no-getting-lost effect** | Quantifiable advantage to relevant routefinding checks | A new bonus competes with existing Survival/Navigation/Knowledge compositions; automatic success could bypass established travel procedures | Do not adopt without broader travel/skill integration review |

### Expedition Survey — preferred original native wording, unapproved

> **Expedition Survey.** When you have a reasonable opportunity to observe an area, travel through it, or study a usable map or account, you can identify the broad relationships among major landmarks, terrain features, and routes that the available evidence reveals. When a destination is visible or marked in the available information, you can identify a plausible approach and suitable landmarks for orientation, if the evidence supports them. You can reliably retain or record the major landmarks, turns, and junctions of a route you personally survey and use that information to describe or retrace it. The GM determines what the observations or records make discernible. This feature does not reveal concealed or unexplored routes, guarantee the shortest or safest approach, or eliminate checks to avoid getting lost, recognize that you are lost, or establish a new course when conditions make navigation uncertain.

**Mechanical interpretation and testable entitlement:**

1. **Active:** The Explorer can use available observations or maps to establish an actionable **plausible approach and orientation reference**, including on the first visit, if those facts can genuinely be inferred. The GM need not disclose unknown or hidden facts. If evidence is sufficient and unambiguous, the Explorer can understand the broad relationships **without a separate check merely to interpret that evident information**.
2. **Persistent:** The Explorer does not need a fresh memory check to recount a route's observed major junctions and landmarks after personally surveying it, provided the character had a reasonable opportunity to examine them. It is not flawless photographic memory or an exact map.
3. **Not automatic routefinding:** Getting-lost checks, obscured/repeating landmarks, broken routes, shifting conditions, dangerous encounters, and the need to choose a new course remain normal gameplay, informed by the available survey data rather than replaced by it.
4. **Not another system:** The definition is content-level character entitlement. The existing Hex Crawl system owns travel state and adjudication; no new general mapping procedure, action economy, guaranteed ten-minute interval, or map data structure is authorized.
5. **Tool distinction:** Explorer's Navigation-or-Cartography choice remains independently useful. Expedition Survey does not give both tool proficiencies. An Explorer can communicate a broad route without needing flawless professional chart production.

**Boundary tests:**

- **First entry into a forest:** From a hilltop, the Explorer identifies the visible ridge and a plausible approach to a mapped ruin if those relationships are discernible. The feature does not reveal hidden trails beneath the canopy.
- **Established return route:** The Explorer remembers a previously surveyed crossing and junctions without rechecking memory. Thick fog or altered terrain can still trigger ordinary getting-lost checks.
- **Featureless desert:** If there are no usable landmarks or sufficiently informative records, Expedition Survey can not conjure a route or waive Survival checks.
- **City-raised Explorer:** The feature works without Wilderness-Raised and does not grant the Upbringing's six-person provisions.
- **Wilderness-Raised Sage:** Provisioning from childhood works without Explorer's route survey.
- **Sailor:** Ship's Passage is a relationship-based transportation benefit. Explorer's surveyed route does not guarantee that a ship will sail it.
- **Guild Merchant:** Navigation proficiency, if chosen, is a separate qualification from Expedition Survey's limited information entitlement.
- **Sage Researcher:** Researcher points to where lore might be found; Expedition Survey interprets already available observation/terrain records. Neither guarantees access to concealed information.

**Residual design risk:** Even this version may be too close to what a competent character can normally do with Survival, Navigation and Cartography. If the owner considers it insufficiently distinctive, evaluate a **small scoped check benefit** for using a personally surveyed route, but only after clarifying how character capabilities compose with `travel.navigation.*` checks. Do not insert an ungrounded reroll, passive advantage or numeric bonus into native content now.

**What this refines:** Use `Expedition Survey` **instead of**, not in addition to, `Geographic Recall` as the single Explorer Background feature. The existing candidate Explorer skills (Perception plus one selected Skill), one Navigation-or-Cartography Competency, no extra language, and ordinary expedition equipment remain **unchanged proposals**. The Wilderness-Raised feature, skill choice and community language also remain unapproved.

**Approval still required:** The feature wording, its functional distinction from ordinary navigation skill checks, and both complete origin packages must be approved before native promotion.


## Final review: actionable Expedition Survey and approval choices

**Status: recommendation only, not approved.**

**Wilderness-Raised Upbringing candidate:** One formative community language (Common eligible); choose one of Survival, Nature, Animal Handling, Perception, Athletics; Wilderness Provisioning supplies food and fresh water for the character and up to five others when enough resources are accessible and there is a reasonable opportunity to gather. No other Competency or starting equipment is granted by this candidate. Its value is campaign-dependent; omitting a Competency does not prove balance.

**Explorer Background candidate:** Perception plus one of Survival, Investigation, History, Nature, Athletics; choose Navigation or Cartography; no language; expedition journal, map paper, writing tool, traveler's clothes, pouch with 10 gp. The defining feature is Expedition Survey rather than passive Geographic Recall.

**Revised Expedition Survey proposal:** When observing or traversing a distinct area, or studying useful maps or accounts, the Explorer can request one reliable, actionable conclusion supported by the available evidence about visible or recorded terrain, landmarks or routes. Examples include a plausible approach to a visible destination, a reference landmark for orientation, or the relationship between two observed paths. If evidence supports a conclusion, the GM supplies it without a separate check solely to interpret that evidence. If evidence is insufficient, the GM explains that limitation rather than inventing a route or hidden fact. The Explorer can later recall major turns and landmarks of routes personally surveyed without an additional check solely for memory. Ordinary checks for becoming lost, recognizing lost state and selecting a new course still apply when conditions require them. Repeated questions about unchanged observations do not generate unlimited new facts.

**Why this refinement:** The earlier feature risked restating what any trained character could do. An explicit entitlement to one evidence-supported navigational conclusion gives a meaningful first-expedition use without creating map knowledge, general skill bonuses, automatic safe travel, or a competing Hex Crawl procedure. It remains situational and should be playtested.

**Approval needed separately for each package:** Wilderness-Raised name, scope, language, skill choices and six-person provision feature; Explorer's two skills, Navigation/Cartography, no language, field equipment and actionable Expedition Survey. No native JSON, C# or consumer changes are authorized by this note.
