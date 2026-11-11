# Pillar 3 — Entertainer and Gladiator Background Reconciliation

**Status:** Both independent Backgrounds are owner-approved and authored as selected-native content. Earlier alternatives remain in this document as design history; the authoritative final allocation is recorded below.
**Branch:** `feature/character-origins-pillar3-life-experiences`
**Ruleset:** `first-party-001`.

## 2014 Player's Handbook evidence

The 2014 **Entertainer** supplies fixed Acrobatics and Performance Skills; proficiency with Disguise Kit and one kind of Musical Instrument; no languages; *By Popular Demand*; and a starting Musical Instrument, admirer token, costume, and 15 gp. Its feature gives the performer opportunities to perform for lodging/food of a modest or comfortable standard while performing nightly, and situational favorable local recognition in places where they performed. The 2014 **Gladiator** is a variant: it has **the same fixed Skills and tool proficiencies and the same named feature**, but extends its performance venue to spectacle combat and **may replace the starting Musical Instrument item with an inexpensive unusual weapon**, such as a trident or net.

**Crucial source distinction:** The original Gladiator variant does **not** explicitly grant proficiency with the replacement weapon; the text modifies an **equipment choice**, not the tool-proficiency choice. Do not treat a third-party homebrew Gladiator with different ability scores, skills, feats or maneuvers as the 2014 source.

Primary historical comparison:
- https://dnd5e.wikidot.com/background:entertainer (2014 PHB transcription, Entertainer plus Gladiator).
- https://dnd-wiki.org/wiki/Entertainer_(5e) and https://dnd-wiki.org/wiki/Gladiator_(5e) (brief 2014 PHB pointers).
- https://rpg.stackexchange.com/questions/110679/does-should-the-gladiator-background-grant-proficiency-in-the-weapon-it-gives (historical distinction between the explicit item substitution and debated implied proficiency).

Use original Dorks & Dice feature wording instead of reproducing the PHB prose. The 2024 Entertainer's Musician Origin feat and ability-score package are not part of this 2014-based pass.

## Separation of identity

**Entertainer Background:** Sustained experience presenting performances for audiences, commercially, publicly or in community settings. It may include instrumental music, singing, oral storytelling, theater, juggling, comedy, dance, acrobatics or similar performances. This is an acquired practice/role, **not** necessarily a character's childhood. A character raised in a performing family might later choose a nonperforming Background; a city- or wilderness-raised adult can become an Entertainer.

**Gladiator Background:** Sustained experience presenting physical contests, staged fights, or combat exhibitions for spectators. The character need not have been enslaved, imprisoned, a professional soldier, cruel, a killer, legally employed, or a member of a specific arena organization. Their experience involves arena audiences and public competitive spectacle; it does not grant military standing, special class maneuvers, organized crime contacts, or guaranteed arena authority.

**Recommendation:** Separate `background.entertainer` and `background.gladiator` as independently selectable native entities, **without parent/inheritance**, following the already-approved treatment of Noble/Knight, Criminal/Spy, Guild Artisan/Guild Merchant. Both may independently receive venue-based audience hospitality because the *function* overlaps, not because one inherits the other. Avoid a new universal "performance career" superclass or forcing a specialized Gladiator to select Entertainer first.

**Counterargument:** The original Gladiator variant differs mostly by the equipment and the kind of venue. Without distinctive Skill/tool/weapon choices, splitting it risks duplicating an otherwise identical Background. The separate record becomes justified only if its approved native package genuinely represents arena practice, not merely different flavor wording. A single Entertainer with an optional "combat spectacle" routine remains a viable alternative if that distinction is rejected.

**Upbringing:** A future Performing Troupe / Performer Family formative Upbringing might represent childhood socialization, household languages and traditions. No such option is approved or necessary for either Background. No class, Species, alignment, noble standing or criminal history is automatically implied.

## Candidate A — Entertainer Background

### Skill proposal: skill-specific performance flexibility

The fixed 2014 Acrobatics/Performance pair overstates acrobatics as universal to musicians, storytellers, actors, singers and comedians. Recommend:

- Fixed `skill.performance`.
- **Choose one Skill** from `skill.acrobatics`, `skill.deception`, `skill.persuasion`, `skill.sleight-of-hand`, or `skill.insight`.

These IDs exist in `native/skills.json`. The second Skill covers movement arts, acting/in-character deception, audience rapport, legerdemain and reading an audience. A performer who wants original Acrobatics + Performance can still choose it.

**Identity caution:** `skill.performance` is a broad 5.x Skill, while the nine `skill.perform-* ` family children from 3.5 are separately ranked Skills (Act, Comedy, Dance, keyboard/percussion/string/wind, Oratory, Sing). A broad Performance training grant does not silently award ranks/training in every Perform specialty or musical-tool qualification. Acrobatics is composite and does not automatically train Balance and Tumble independently.

### Tool/Competency proposal — choice, not compulsory musician identity

- Choose **one proficiency**: `competency.disguise-kit` **or** one type of **musical instrument** (using the established `choiceCategory: musical-instrument` rather than fabricating a standalone `competency.musical-instrument`).

The 2014 source grants **both**. Reducing it to one is a deliberate native change to avoid requiring instrumental training of every actor, juggler, dancer and storyteller. Disguise Kit is broadly relevant to stage makeup, costume transformation and role portrayal; a Musical Instrument suits musicians and musical theater.

**Open alternative:** Retain both original proficiencies, as the 2014 Entertainer does, rather than reducing the grant. This is simpler for source fidelity but forces one sometimes-unrelated proficiency and should be weighed alongside other Backgrounds (Charlatan grants two tools, though its vocational identity directly supports both). Another possible model is a broad "performance methods" choice including a Perform specialty, but that would mix ranked Skills with tool-proficiency identities and require a more complicated choice representation; do not introduce it in this pass.

There is **no extra language**. Performance, fluency and literacy are distinct; an occupation does not automatically supply a complete new language.

### Audience Hospitality — proposed native feature

> **By Popular Demand.** In a settlement or venue where your kind of performance can reasonably attract an audience, you can generally secure an opportunity to perform through ordinary hosts or organizers. While you fulfill the agreed performance engagements, including regular evening performances when required, your host provides food and lodging suited to the establishment, ordinarily of a modest or comfortable standard, without a separate room-and-board fee. Where people recognize performances you have given, they are generally inclined to a favorable initial reception. This does not grant guaranteed pay, celebrity in unfamiliar places, access to closed venues, or authority over organizers; availability, safety, and the conduct of spectators remain subject to circumstances.

The feature is an actual occupational accommodation, not permission to make an ordinary Performance check. In places with no feasible venue, audience or host it does not conjure a theater or sponsor. A successful prior performance may produce local recognition but not unrestricted popularity or persuasion success. Regular performance is an obligation, not guaranteed free room/board without work.

**Comparison:** Folk Hero's Community Refuge offers sympathy after a recognized helpful deed; Entertainer's hospitality is compensation for **current performance work**, not a gratuitous charitable refuge. Guild Membership is institutional; Entertainer does not require a guild. No unrestricted party-wide lodging guarantee is implied; the feature primarily covers the performer, with companions only where the host reasonably agrees.

### Ordinary starting equipment

Recommend **source-shaped**:
- One actual Musical Instrument **if selected as the trained item**, or a **Disguise Kit or suitable ordinary performance prop** if Disguise Kit training was chosen; a player may select a non-instrumental prop without automatically receiving an instrument proficiency.
- One **nonmagical favor or token** from an admirer (letter, ribbon, keepsake; no required romantic relationship).
- One **performance costume**.
- One **pouch with 15 gp**.

The source always includes an instrument and does not include a physical Disguise Kit, despite proficiency; this adaptive equipment option is a **deliberate divergence** to avoid giving a musical instrument to every nonmusician. The comparative monetary value of musical instruments, disguise kits and props is not established; ordinary item-identity/materialization remains deferred. No automatically granted magic, special reputation stat, entertainment guild membership, instrument mastery across all types, or retained troupe ownership.

## Candidate B — Gladiator Background

### Skills: arena-oriented independent training

Recommend **fixed `skill.performance`**, representing public presentation/showmanship, **and choose one** from:
- `skill.athletics` (strength-based feats and physical contests);
- `skill.acrobatics` (evasive/showy movement);
- `skill.intimidation` (intimidating opponents/audiences through spectacle).

The original Acrobatics/Performance combination remains selectable, but physical or psychological arena performers need not be acrobats. These choices are not class proficiency, combat maneuver training, or bonuses to attacks. Athletics and Acrobatics are composite without automatically training the independent Climb/Jump/Swim and Balance/Tumble Skills.

### Combat technique / tool choice — requires explicit decision

The 2014 variant grants **Disguise Kit and one Musical Instrument proficiency** just like Entertainer; the unusual weapon is **equipment only**, not an explicit weapon proficiency.

**Preferred native adjustment for consideration:** Give fixed `competency.disguise-kit` for arena persona/costuming **plus choose proficiency with exactly one type of appropriate inexpensive arena weapon**. Start the reviewed pool with **trident or net** (the source examples). A native weapon-grant field `weaponProficiencies` already exists in the approved High Elf and Wood Elf Upbringings; a narrow one-weapon choice still requires selection execution and item/weapon identity verification. Do not infer proficiency from possession of a starting item, nor grant all martial weapons, a fighting style, martial feats, class combat maneuvers, an extra attack, or armor proficiency.

**Balance caution:** Neither the approved Soldier nor Knight Background grants additional weapon proficiency, and a narrow combat qualification may be valuable to certain classes while redundant to others. A Gladiator-specific weapon grant is therefore a **substantive system decision**, not automatically authorized by the original PHB variant. It may ultimately be more suitable as part of optional combat technique training, especially once classes, tools and weapon qualifications are reconciled.

**Alternative G1 (closer to 2014):** Disguise Kit and one Musical Instrument type, with **no granted weapon proficiency**, even when the starting unusual weapon is received. This preserves literal source rules but gives some arena performers implausibly mandatory musical training and no qualification for their chosen fighting prop.

**Alternative G2 (no new combat entitlement):** Choose one from Disguise Kit or one Musical Instrument type, and receive the arena weapon as ordinary equipment but not training. This resembles Entertainer's one-tool choice, leaving weapon proficiency to Class, other origin sources or later advancement. It is mechanically more conservative but arguably less representative of practiced gladiatorial combat.

**Recommendation on this question:** Treat one narrow weapon proficiency as a serious, explicit **proposed** Gladiator distinction, not as part of the source baseline or an implicit rule. Obtain separate owner approval before selecting that part of the final package; do not silently add or remove it.

### Arena Performances — proposed independent Background feature

> **Arena Performances.** Where organized fighting exhibitions or similar spectator contests are a recognized form of entertainment, you can generally find a suitable organizer or venue willing to consider you for an appropriate contest or demonstration. When you regularly perform as agreed, you may receive food and lodging of modest or comfortable quality through the venue without an additional room-and-board fee. Spectators who recognize your past exhibitions may regard you favorably as a performer. This does not guarantee victory, prizes, employment in places without spectacles, safe matches, legal immunity, or access to an arena's restricted areas.

This reflects the combat-venue use of 2014 *By Popular Demand*. The character need not kill opponents or fight to the death to qualify; exhibitions, sparring and nonlethal spectacle count when a venue supports them. The feature never forces the GM to invent a combat pit in a settlement without one or compel dangerous contests as a condition of historical identity.

**Feature comparison:** Entertainer's ordinary shows and Gladiator's combat exhibitions have the same hospitality structure but distinct venue conditions. This does **not** produce two independently stacking hospitality entitlements because a character normally selects one Background. Gladiator is not Knight (retainers/standing), Soldier (military rank), Folk Hero (rescue-based solidarity) or Charlatan (false identity).

### Ordinary starting equipment

Recommend: **one inexpensive but distinctive arena weapon or fighting prop** appropriate to the character's chosen discipline (source examples: trident or net); one admirer favor or personal token, one performance or arena costume, and **15 gp in a pouch**. No automatic armor, arena ownership, sponsor, entourage, opponent, weapons cache or legal permit.

Receiving a trident or net does **not** itself award proficiency. If the narrow weapon training proposal is adopted, the item may match the selected weapon; if not, the character can still possess it as ordinary equipment, as in the 2014 source.

The choice between a trident and net must use verified native item identities when integrated with ordinary inventory. The money, costume and favor are ordinary starting possessions; their meaning is determined by the player/GM rather than mandatory biography.

## General corpus and life-experience review

1. **Different causes:** Entertainer is sustained public performance and Gladiator sustained competitive spectacle. Neither requires a performer-family or arena-raised Upbringing; either can be acquired later in life.
2. **Perform Skill:** The universal `skill.performance` and independently ranked `skill.perform-*` children are not interchangeable automatic grants. One Musical Instrument type is likewise not a Perform family rank.
3. **Other established Background features:** Folk Hero's Community Refuge, Soldier's Military Rank, Knight's Retainers, Guild Artisan's Membership and Charlatan's False Identity remain distinct from hospitality earned by performances.
4. **Social benefit scope:** Where suitable venues and audiences exist, regular work should secure food and room without an ordinary fee, not merely confer a chance to negotiate exactly what any person might. No automatic cash proceeds, private lodging for all companions, or universal fame.
5. **Gladiator weapon choice:** A background-granted weapon proficiency is a deliberate system-level exception to the 2014 baseline, not a consequence of replacing one piece of equipment.
6. **Musical instruments:** Neither an instrument item nor a proficiency should be mandatory for every acting/acrobatics-only entertainer unless the owner explicitly prefers source fidelity.
7. **Costume and favor:** Do not require a romantic admirer, fixed noble patron, enslaved arena background or persona that has magically compelling effects.
8. **Relative balance:** Current source 2014 provides two Skills, two tools and hospitality. Candidate Entertainer provides two Skills and **one** tool. Candidate Gladiator with narrow weapon qualification would provide two Skills, Disguise Kit and a weapon qualification. Compare the value of these configurations during the corpus-wide audit.
9. **Incomplete grant execution:** Mixed tool categories and single-weapon selection, equipment alternatives and conditional social grants await common native choice and character-projection integration.

## Decision/approval boundary

**First recommendation:** Approve two independent native Background identities with no parent/inheritance, rather than a single Entertainer plus a mechanically inherited Gladiator variant.

**Proposed Entertainer:** fixed Performance; choose one Acrobatics/Deception/Persuasion/Sleight of Hand/Insight; choose one Disguise Kit **or** Musical Instrument type; no language; performance-based lodging and local recognition (By Popular Demand); suitable performance item, admirer token, costume and **15 gp**.

**Proposed Gladiator:** fixed Performance; choose one Athletics/Acrobatics/Intimidation; fixed Disguise Kit training; **consider** one specific arena weapon proficiency (trident/net) **only by separate explicit approval**; no language; arena-specific performance hospitality and recognition; one inexpensive arena weapon/fighting prop, admirer token, costume and **15 gp**.

**Remaining decisions:** Whether to retain both 2014 tool grants for Entertainer, and whether to grant Gladiator one arena weapon proficiency despite its absence from 2014 (or prefer literal source or conservative no-weapon alternatives). These are not trivial phrasing changes: they affect relative Background and class access.

**Nothing in this document approves native `background.entertainer`, `background.gladiator`, new Upbringing, or runtime system changes.** Preserve source evidence and only author selected-native JSON after owner approval.


## Design refinement — Gladiator weapon proficiency replaces tool training

**Owner direction (2026-11-04):** The Gladiator's **one arena-weapon proficiency is intended as the tradeoff for not receiving an ordinary tool proficiency**, not an extra proficiency stacked atop mandatory Disguise Kit training. The prior candidate's fixed Disguise Kit + weapon proficiency recommendation above is **superseded**. This discussion refines the proposal but does not constitute final authorization to promote native JSON.

**Revised independent Gladiator candidate:**

- **Fixed Skill:** `skill.performance`.
- **Second Skill:** Choose one of `skill.athletics`, `skill.acrobatics`, `skill.intimidation`.
- **One proficiency in place of tool training:** Choose **one specific arena-appropriate weapon type** for proficiency, with the starting weapon normally corresponding to that selection. This is a **single weapon proficiency**, not all martial weapons, additional attacks, a Fighting Style, armor training or another combat-class benefit.
- **No automatic Disguise Kit, Musical Instrument, other tool proficiency, or language.** An arena costume or persona does not itself confer Disguise Kit expertise.
- **Feature:** Arena Performances, for venue-appropriate opportunities, conditional hospitality, and recognition.
- **Starting equipment:** A single appropriate arena weapon, costume, admirer token and pouch containing 15 gp, as ordinary inventory. Final selection must use an actual allowed weapon definition and consider the value of the item. The 2014 trident/net examples are not an exhaustive list or automatic mechanical restriction.

**Entertainer comparison:** The Entertainer candidate keeps fixed Performance, one additional Skill, and a choice of one Musical Instrument proficiency **or** Disguise Kit. Gladiator instead uses the **one selected weapon proficiency** to represent a combat-performance specialty. This is parity of **grant count**, not proven parity of strength: weapon training can affect combat performance while most tool training contributes different utility.

**Compatibility and source note:** This departs intentionally from the literal 2014 Gladiator variant, which did **not** expressly award a weapon proficiency and continued to provide Entertainer's tool proficiencies. The approved native catalog already uses `weaponProficiencies` for fixed weapon grants in certain Upbringings; a **player-selected weapon type** must receive appropriate common choice representation later. Do not infer weapon training from an inventory item, grant a whole weapon category accidentally, or introduce a special Gladiator fighting subsystem.

**Approval still required:** Both independent Backgrounds' complete grants, whether the selected weapon may be any arena-appropriate native weapon subject to a bounded equipment choice, and exact ordinary inventory wording. No native content or runtime code has been authorized.


## Owner-approved native allocation — Entertainer and Gladiator (2026-11-04)

**Owner approval:** The two independent complete packages were approved, following the explicit change that **Gladiator's single arena-weapon proficiency replaces tool training**. The pre-refinement proposal for Gladiator to gain both Disguise Kit training and a weapon proficiency was rejected. These final rules supersede the earlier speculative sections above.

**Native records:**
- `data/rulesets/first-party-001/native/backgrounds/entertainer.json` — `background.entertainer`, selected-native.
- `data/rulesets/first-party-001/native/backgrounds/gladiator.json` — `background.gladiator`, selected-native.

### Approved Entertainer

- Fixed **Performance** (`skill.performance`) and **choose one** from Acrobatics, Deception, Persuasion, Sleight of Hand, Insight.
- **One total proficiency choice**, represented as a native `benefit-choice`: either `competency.disguise-kit` **or** one musical instrument type using existing `musical-instrument` category selection. Neither option grants the other, and no independent Perform child Skills are automatically trained.
- **No additional language.**
- **By Popular Demand:** Available, appropriate venues allow regular performance work in exchange for food and lodging at modest/comfortable standards without a separate fee; prior performances may yield favorable local recognition. No compelled host where none exists, guaranteed pay, universal celebrity, private rooms for arbitrary companions, or institutional authority.
- **Starting inventory:** One suitable performance item consistent with the chosen method (instrument of selected type; or physical Disguise Kit/ordinary prop when Disguise Kit training is selected), admirer token, costume, pouch and **15 gp**. An instrument is not compulsory for nonmusicians.

### Approved Gladiator

- Fixed **Performance** (`skill.performance`) and **choose one** from Athletics, Acrobatics, Intimidation.
- **Choose proficiency with one specific existing mundane arena-appropriate weapon type**, rather than any tool proficiency. Trident and net are examples, not an exhaustive list. The new native selection is represented with `grantType: qualification-training`, `qualificationCategory: weapon`, `choiceCategory: specific-weapon`, `choiceCount: 1`. This is the source-of-truth content intent, **not** a claim that the current consumer choice executor already supports it.
- **No Disguise Kit, Musical Instrument, other tool, additional language, entire martial weapon category, class maneuver, attack bonus, Fighting Style, or automatic class benefit.**
- **Arena Performances:** Suitable combat-exhibition venues provide opportunities to perform, conditional food and lodging in exchange for ongoing performances, and local recognition. No guaranteed prizes, victories, danger-free matches, sponsor, legal immunity or restricted-area entry.
- **Starting inventory:** One inexpensive mundane arena weapon **of the same specific type as the chosen proficiency**, admirer token, arena costume, pouch and **15 gp**. The weapon belongs to ordinary inventory and possession does not itself convey proficiency.

### Final design boundaries and deferred integration

- Each Background is selected independently; Gladiator is **not** an inherited Entertainer variant. An upbringing in a performer or arena community is possible narrative context, not an automatically granted Upbringing. Neither Background requires particular Species, age chronology, social status, or 2024 Origin feats.
- The 2014 Gladiator variant **did not itself add weapon proficiency**. The native substitution is a deliberate owner-approved change to make arena training distinct from Entertainer's chosen utility training. Both packages contain one additional proficiency choice, but weapon vs. tool strength is not mechanically equivalent by assumption.
- **Deferred:** Generic mixed benefit-choice execution, musical instrument type resolution, specific-weapon choice/qualification projection, actual item identities and cost comparison, duplicate training semantics, and conditional performance/venue adjudication. No C# host/consumer, Character Sheet, or equipment engine changes are authorized by this content approval.
