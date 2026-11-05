# Pillar 3 — Outlander Background and Wilderness Upbringing Reconciliation

**Status:** Source-backed first-pass design proposal. **No native Outlander Background or additional Upbringing is approved or promoted by this document.**

**Ruleset:** `first-party-001`  
**Development branch:** `feature/character-origins-pillar3-life-experiences`

## Source evidence and design problem

The 2014 *Player's Handbook* Outlander identifies the character narratively as someone who **grew up in the wilds**, yet its suggested origin roles include guides, foresters, trappers, pilgrims, explorers, exiles and others whose relevant skills may arise from **later travel or sustained work**. The current Pillar 3 architecture separates **formative cultural Upbringing** from an individual's meaningful **Background experience**, without imposing an arbitrary age cutoff.

The 2014 Outlander mechanical package:

| Component | 2014 Outlander |
| --- | --- |
| Skills | **Athletics**, **Survival** |
| Tool | Proficiency with **one Musical Instrument type** |
| Language | **One** language of choice |
| Feature | **Wanderer**: recall broad geography, and find food and water for oneself plus **up to five other people** where the land provides adequate natural resources |
| Starting possessions | **Staff**, **hunting trap**, **animal trophy**, **traveler's clothes**, **pouch containing 10 gp** |

Sources (non-SRD 2014 expression; author original native prose):
- https://dnd5e.wikidot.com/background:outlander
- https://dnd-5e.fandom.com/wiki/Outlander
- https://dnd-wiki.org/wiki/Outlander_(5e)
- https://www.dndbeyond.com/backgrounds

The source's explicit childhood framing is evidence **not to preserve as a mandatory prerequisite** in the native Background. Conversely, wilderness knowledge can genuinely be learned during childhood and may later appear in a broad environmental/community Upbringing. Do **not** move every Outlander grant to an Upbringing merely because the 2014 narrative opens with childhood.

## Classification and independent identity

### Outlander as Background — proposed

**Independent Background identity:** You have spent meaningful time traveling, living, exploring or working outside densely settled areas, acquiring practical knowledge of terrain, wilderness routes, gathering provisions, and dealing with remote environments. You may have been a guide, explorer, itinerant worker, forester, hunter, surveyor, scout, trapper, pilgrim, exile, or independent wanderer. The experience does **not** require a wilderness childhood, employment, nomadic ethnicity, isolation from all settlements, a specific Species or Upbringing, or a particular moral outlook.

The name **Outlander** is retained as a recognizable source-derived label, but its native description should explicitly avoid equating it with someone foreign to the party's culture or automatically unfamiliar with towns. Alternative labels such as **Wanderer** or **Wilderness Traveler** could be reviewed, but a rename is not required for native promotion.

### Wilderness/Rural/Itinerant upbringing — candidate only

A distinct formative Upbringing may represent growing up with regular access to extensive natural environments and learning local practices, ecological knowledge, and a community language. It should **not** be defined as one universal "tribal culture," a fixed Species association, or a blanket inability to understand cities. Depending on future balance needs, it might be a **Wilderness Community** Upbringing separate from **Rural Community** (settlement/agricultural living) and **Itinerant Community** (mobile formative environment); do not pre-create all three without approval.

**Possible future formative benefits** (unapproved): one language learned from family/community (Common may be suitable); one chosen training appropriate to formative practices (Survival, Nature, Animal Handling, Perception, Athletics); one practical competency where applicable (Cooking, Herbalism, Leatherworking, Navigation). Exact eligibility, count and special feature are **not decided**.

The **Wanderer** Background feature should not automatically be replicated in the future Upbringing, nor should a character selecting both get double daily provisions, double environmental map information, or free replacement ranks. Duplicate grant resolution is a shared character-creation issue.

## Proposed independent Outlander mechanical allocation

### Skills — retain 2014 pair

- Fixed `skill.athletics` (composite Skill).
- Fixed `skill.survival`.

**Recommendation:** Retain both as fixed training for the initial native corpus. This is consistent with the practical experience identity. Training in Athletics does not independently train Climb/Jump/Swim ranks or grant separate training to those components. If the broad Outlander identity later proves to include too many nonphysical explorers or scouts, compare an optional skill selection during the general balance pass rather than immediately proliferating variants.

**Verified catalog:** `skill.athletics` and `skill.survival` exist in `native/skills.json`. `skill.profession-guide` also exists, but belongs to the separate independently ranked Profession family; do not conflate it with Survival or grant it invisibly.

### Tool/Competency — decision needed

The original 2014 entry grants a **Musical Instrument** proficiency with no necessary causal connection to navigating, foraging, or traveling. This is the clearest unresolved allocation question.

**Model A — source-faithful (recommended initial baseline):** Choose **one Musical Instrument type** and gain training with it, using a category choice rather than inventing a nonexistent `competency.musical-instrument` ID. Such learning may come through companions or communities encountered during the character's travels. It is *not* evidence of a mandatory folk-music culture. This matches the general category-choice treatment already approved for Knight.

**Model B — more adaptable experience choice:** Choose one of (1) a Musical Instrument type, (2) `competency.navigation`, (3) `competency.herbalism`, or (4) `competency.cooking`. This better supports travelers who never learned an instrument, but increases overlap with the broad wilderness survival skill/feature and might offer unequal mechanical benefit among the options. It would also require a mixed category-and-specific-competency choice mechanism during integration.

**Recommendation:** Prefer **Model A** as the unmodified 2014 baseline for initial native selection, with Model B kept as a viable alternative for owner consideration. Neither model is approved.

### Language — one learned language through travel

The original grants one language of choice. Proposed native adaptation:

> Learn one language of your choice through communities, companions or contacts encountered during your travels or extended time living away from settled centers. Common is valid if used by people the character learned from.

This is not intrinsically a childhood language and does not imply that every wilderness community speaks a unique language. The choice is a **Background language**, independent of Upbringing.

**Alternative:** Zero additional language, with all formative language learning belonging to Upbringing. Not recommended here: Outlander can meaningfully learn a new language through later travel, and 2014 explicitly granted one.

### Wanderer — primary Background feature

**Proposed independent original Dorks & Dice text:**

> **Wanderer.** Your travel and field experience give you a reliable memory of the general geography of places you have traveled through or studied, including their broad terrain, settlements and notable landmarks. Where the natural environment provides reachable food and fresh water in sufficient amounts, you can gather enough provisions each day for yourself and **up to five other people**. This does not produce supplies in barren environments, reveal unknown precise locations, bypass physical obstacles, or automatically make travel free from ordinary dangers.

**Key 2014 functional baseline:**

- **Recall:** An excellent broad memory for maps/geography and the general layout of terrain, settlements and nearby features; it is not perfect cartography, precise coordinates, automatic detection of hidden settlements, or full foreknowledge of unexplored regions.
- **Food and water:** Daily supplies for the character **plus up to five others** when the land genuinely contains sufficient food and fresh water; the source's limit is **six total people**, not an unrestricted group size.
- **No blanket denial:** In favorable terrain, the feature is a meaningful entitlement, not merely permission to attempt the same Survival checks any trained person can attempt. Do not weaken it to an undefined small bonus or turn it into an ordinary Survival proficiency reroll.
- **Environment matters:** Scarcity, inaccessible provisions, polluted sources and hostile conditions can limit what is feasible. The benefit does not create resources, conjure potable water, remove environmental hazards, or guarantee risk-free travel.
- **No new travel subsystem:** Normal DM adjudication determines terrain viability, available resources, travel challenges and circumstances. No automatic food-economy calculator or hardcoded foraging table is approved.

**Primary design issue:** Should the geographic memory cover only places the traveler personally visited, or also areas they studied on a map or learned about from credible people? The phrase in the 2014 feature is broad, and the proposed wording explicitly includes both **traveled through or studied**. This avoids impossible universal knowledge while making map literacy and informed travel meaningful. The owner may retain the 2014 ambiguity instead; no exact expansion or restriction has been approved.

The feature is associated with the **Background's sustained field experience**, not automatically awarded to someone solely because their childhood was rural or nomadic. A remote-raised Sage should be able to represent formative wilderness familiarity without being an automatically provision-generating expert.

### Starting inventory — source-shaped, without mandatory killing

Proposed ordinary equipment:

- One **staff**, usable as an ordinary quarterstaff if the weapon identity matches.
- One **hunting trap**.
- One **token or trophy of past travel**, which may be an animal trophy but **does not require the character to have killed an animal**.
- **Traveler's clothes**.
- A **pouch containing 10 gp**.

The source's trophy prescribes a particular act (killing an animal). Since this is a roleplaying biographical assumption, broadening it to any significant journey token preserves ordinary equipment utility without compelling a story.

No guaranteed tent, permanent campsite, travel rations, animal companion, hunting proficiency outside granted skills, vehicle operation, supernatural sense or magical survival power. Item materialization will happen through the ordinary character inventory system when implemented.

### Suggested roles and independence

Possible prior experiences: Guide, Explorer, Forester, Trapper, Hunter, Scout, Pathfinder, Surveyor, Pilgrim, Homesteader, Seasonal Worker, Exile or Wanderer. These are **optional prompts**, not new sub-backgrounds or automatic class specializations. A character might have lived in towns between excursions and still have Outlander as their defining experience.

Do not introduce separate mechanical variants for each role merely to recreate the historical "Outlander origin" table.

## Cross-corpus and balance tests

1. **General Goliath Upbringing + Outlander:** Goliath may select Survival or Athletics; these overlaps await the general duplicate policy. Its Cooking/Leatherworking/Navigation choice is a different category from Outlander's proposed Musical Instrument.
2. **Giant Foundling + Outlander:** Giant Foundling grants Survival and two languages (one fixed Giant). Outlander adds Athletics, a possible instrument, a learned language and Wanderer. Do not assume the duplicated Survival becomes a second rank.
3. **General Elf / Forest Gnome + Outlander:** Both may choose Survival through their formative selection. Review full conventional origin combinations before normalizing.
4. **Wilderness Community Upbringing + Outlander:** A character with both should have different formative and professional reasons for training; no double supplies or automatic feat.
5. **Street-Raised + Outlander:** The character can have an urban childhood then make a living as an explorer; Urban Navigation and Wanderer refer to different domains without impossible movement benefits.
6. **Sailor + Outlander comparison:** Sailor supplies conditional ship passage, Water Vehicles and Navigation but no new language; Outlander supplies terrestrial/remote environmental provisioning and map memory, not a vessel or passenger transport.
7. **Guild Merchant + Outlander:** Navigation is an optional guild trade qualification; it does not grant Wanderer or substitute for Survival.
8. **Relative feature power:** Wanderer's dependable daily provisions for six people can eliminate ordinary foraging scarcity in resource-rich areas; compare with restricted institutional features and group transportation rather than assuming all 2014 features have equal power.
9. **Narrative assumptions:** A wilderness childhood alone must not force Outlander; being a city-born professional guide must not be disallowed. An animal trophy must not force a killing scene or morality.
10. **Musical Instrument:** If source-faithful instrument choice remains, evaluate whether its category-only grant versus broad native Competencies produces an unacceptable value gap before runtime projection.

## Approval boundary

**No native content is authorized yet.** Approve or amend fixed Athletics/Survival, the Musical Instrument choice (Model A) versus an adaptable Competency choice (Model B), one travel-linked language, Wanderer's memory/food-and-water wording and six-person limit, and source-shaped equipment with a nonmandatory trophy before authoring `native/backgrounds/outlander.json`.

No new `upbringing.wilderness-community`, `upbringing.rural-community`, or `upbringing.itinerant-community` is authorized. Their formative scopes are research hypotheses only; counts and features must be separately approved. No new shared skill or travel systems, consumer runtime code, or automatic grants are introduced in this reconciliation.
