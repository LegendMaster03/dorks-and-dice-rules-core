# Dorks & Dice — native rules specification (pre-implementation)

Status: **working game-design specification**, started 2026-10-09. This document records the owner's established gameplay decisions, proposed generalized rules, and unresolved decisions for review. It is **not authorization to implement**, replace the production Rules Core service, or treat draft procedures as approved rules.

## Mission and compatibility premise

Dorks & Dice is a distinct, authoritative tabletop RPG system. Rules Core will host and evaluate that system. D&D 3e, D&D 3.5e, 5e (2014), and 5.5e (2024) are *compatibility inputs*, not alternate hidden base systems. **The intended experience is analogous to Pathfinder First Edition's compatibility with D&D 3.5e, with Dorks & Dice providing automated translation rather than relying on manual conversion alone.** Pathfinder 1e is a potential additional compatibility input, not yet a promised importer or incorporated default ruleset.

Automated conversion must preserve semantic intent and meaningful mechanical differences. A converted source rule has a Dorks & Dice concept and an implementation profile; known equivalents and compatible additive content may be mapped automatically, while ambiguous or contradictory rules need visible adjudication. Full source provenance, exact editions, allowed uses, and licenses remain intact. Importing an option does not automatically activate it as a native/default Dorks & Dice mechanic.

**Different compatibility deliverables must be scoped independently:** (a) source-rule/content conversion into the hosted ruleset; (b) character, creature, encounter, and campaign-state conversion for a future migration workflow; (c) preservation of source-native play behavior versus adoption of Dorks & Dice-native defaults. The requirement for automated rules conversion does not imply that all existing characters or campaigns can be converted losslessly without review.

## Game-design status vocabulary

- **Recovered / established:** a previously approved gameplay decision confirmed in existing Rules Core documentation/code or by the owner; cite concrete evidence and retain its meaning.
- **Owner proposal:** a newly articulated gameplay direction; write in the owner's own terminology and identify unsettled details.
- **Working model:** a proposed explanatory shape offered to clarify interactions, not an approved rule.
- **Open decision:** material behavior requiring the owner to decide.
- **Implementation-only:** old service behavior, fallbacks, persistence details, and API compromises that do not define game rules.

The final independent system must be specified enough that a player and GM can create a character, advance it, resolve uncertain actions and combat, apply consequences, recover resources, cast/activate features, and run exploration without choosing an external D&D edition to fill unstated fundamental procedures. A full game's **rules** need specification before engine implementation; exhaustive imported spells/monsters/items are content catalogs, not prerequisites to explaining the native procedures.

## Recovered foundations to preserve and refine

| Domain | Established basis | Primary evidence |
| --- | --- | --- |
| Universal competencies | Edition-independent learned identity; skills, tools, proficiencies, facets and training state distinguished. Numeric rank and proficiency formulas stay on separate profiles. | `docs/competency-reconciliation-audit.md`, `docs/universal-character-concepts.md` |
| Composite competencies | Distinct older contributors remain usable; Stealth, Perception, Athletics, Acrobatics derive through documented one-way mean, rounded toward zero, with component and parent modifiers applied separately. | `docs/mechanical-relationships.md` |
| Class families | Base classes, subclass bound to a parent class and its level, prestige classes with their own progression and prerequisites; advancement acquisition and eligibility should be core-owned. | `docs/mechanical-relationships.md`, `docs/character-advancement-eligibility.md` |
| Abilities and character mechanics | Stable semantic abilities, sizes, qualifications, defense mechanics, conditions and effects; do not treat source-native mechanics as automatically interchangeable. | `docs/universal-character-concepts.md`, `docs/character-mechanics-consumer.md` |
| Casting and advancement | Reviewed rules for spell preparation removal, slots/points choice, pact magic distinctness, caster-progression handling; distinguish established rules from unimplemented prestige spellcasting interactions. | `docs/baseline-bootstrap.md`, `docs/character-mechanics-consumer.md` |
| Native house rules | Healing potion action choice, spell preparation, slots/points, controlled-creature initiative, free flavor feats, additive cross-edition reconciliation. | `docs/baseline-bootstrap.md` |
| Core state ownership | Rules Core defines mechanic and resolution; Character/Tool owns current state and user choices; GM/campaign governs permitted overrides. | `docs/character-mechanics-consumer.md`, `docs/architecture.md` |
| Travel/environment | Rules Core defines source-governed quantities, checks and consequences; game tools own expedition state, random direction, and encounter flow. | `docs/travel-environment-mechanics.md` |

This is a summary, **not the completed recovered decision inventory**. Each chapter below should link to individually reviewed decisions in the Phase 0 audit rather than infer approval from an existing endpoint.

## Level Band progression — replaces epic-specific progression

**Owner direction: established at the conceptual level; precise transition defaults and edge cases remain open.**

Level advancement uses consecutive **20-level bands** where applicable: 1–20, 21–40, 41–60, and so on. A mechanic does not switch to a separate hard-coded epic engine at level 21. Each level-based advancement track has its **own band**, so character total level and individual class/prestige-class occurrence levels may occupy different bands simultaneously. A subclass normally uses its parent class track. Do not automatically treat a competence rank, mythic-like tier, or other non-level track as a 20-level band.

A mechanic can, at a threshold or transition on a declared track:

- become newly available;
- continue unchanged;
- continue progressing;
- **stop gaining improvements but remain active**;
- evolve its scaling, allowed effects, or other rule;
- cease applying if an explicit rule says so.

Mechanic **eligibility, advancement progression, effect scaling, and prerequisites** can use different tracks or derived effective levels. No default “level 21 makes all features epic” rule is permitted. As an example, a total level-25 Wizard 15 / Fighter 10 is in total-character band 2 while both classes are in their first bands.

“Epic” is **source-native terminology, not the name of Dorks & Dice's second band or a separate progression algorithm**. Historical Epic Feats/Epic Boons remain feats; an Epic Spell remains a spell; class continuations remain continuations of the relevant progression; independently selectable higher-level prestige classes remain prestige classes. Original terms and eligibility requirements remain in source provenance. For example, a 2024 Epic Boon available from a level-19 requirement is not silently moved to level 21 due to its label.

**To specify before engine work:** transition behavior when source data is silent; overlapping track effects; the semantics of paused versus terminated progression; effective level calculations for prestige spellcasting; effects of multiclassing on character-level grants; and whether a progression has a finite maximum. Do not presume every track supports indefinite advancement.

Supersedes the *native-gameplay interpretation* of `docs/epic-content-modeling.md`, which documents the **legacy implementation's** epic-specific metadata. Do not delete or change that legacy document or its currently deployed behavior until migration requirements have been validated.

## Core rules chapters to finish before implementation

For each chapter, recover the existing decision text, describe how the game is intended to work in plain rules language, supply at least one play example and one cross-edition conversion example, and mark every unresolved rule. Chapters should include:

1. **Character creation and identity:** ability creation/changes; species/subspecies; backgrounds; starting class; qualifications; equipment; starting resources.
2. **Advancement:** total and component level tracks; Level Bands and transitions; multiclassing; subclass, prestige and sidekick-like structures; class features; feats; prerequisites; XP and other eligible advancement methods.
3. **Checks and competencies:** abilities, ranks, proficiency, shared training, composite skills, passive/automatic checks, opposed tests, bonuses and penalties, roll selection (including Emphasis), success/failure and resolution states.
4. **Combat:** initiative and turns, action economy, attacks, defenses, damage and resistances, positioning/movement, conditions, reactions/opportunity mechanics, **combat maneuvers**, concentration, death and recovery.
5. **Magic and other powers:** spell acquisition/casting, preparation policy, slots/points/pact resources, components, DCs, concentration, conditions, rituals, psionics and other imported power systems.
6. **Equipment and resources:** weapons, armor, armor-class contributions, ammunition, tools, item usage, encumbrance, wealth/currency, crafting and harvesting integration.
7. **Rest and recovery:** ordinary rest, hit points, recovery, exhaustion/conditions, limited resources, other recuperation procedures.
8. **Exploration and environment:** time, speed, journey pace, terrain, weather, navigation, visibility, resource use, hazards and related checks.
9. **Creatures and GM procedures:** creatures/NPCs, creature abilities, encounter interaction, action applicability, challenge-related source mechanics, rulings and campaign overrides.
10. **Rules interoperability:** which imported content becomes accessible, eligible, adopted, or active; compatible additions, replacements, mutually exclusive alternatives, conflicts and provenance.

**Not every existing game mechanic is complete merely because a normalized source profile exists.** The native rules must explain what happens at the table, including cases that currently surface as unresolved.

## Native combat decisions — recovered and newly established

The owner has now made or reaffirmed the following Dorks & Dice combat design decisions. This section is **gameplay design**, not an assertion that the current Rules Core engine implements all of these procedures.

| Subject | Current owner direction | Status |
| --- | --- | --- |
| Initiative | **Block Initiative** is already the native initiative system and is implemented separately. Do not choose an initiative system by preferring one of the editions in the comparison. | Established external Dorks & Dice design; integration still needs precise interface contract |
| Combat defenses | Preserve the existing 3.5-derived **ordinary, Touch and Flat-Footed AC** distinctions already reconciled in Rules Core. Do not silently substitute 5.x advantage/disadvantage or saving throws for these defenses. | Reaffirmed existing design |
| Opportunity attacks | Source versions use substantially the same underlying concept, with differences in names, triggers, applicability and action restrictions. Reconcile the common intent but **do not treat triggers as identical** or prematurely decide the native trigger policy. | Existing-concept recognition; native details open |
| Critical success/failure | **Adapted from Fool's Gold, not invented for Dorks & Dice:** critical successes and failures stack through consecutive extreme natural d20 results. An initial natural 20 is a critical success that calls for another roll; consecutive 20s raise the critical tier. An initial natural 1 is a critical failure that similarly stacks on consecutive natural 1s. The owner also attributes the **triple-critical effects** to Fool's Gold. | Adopted gameplay direction from Fool's Gold; integration details below |
| Single critical-hit damage | A single critical hit uses **maximum damage plus one additional damage-dice roll**, rather than simply doubling the dice. Static modifiers and bonus dice interactions require a later precision rule. | Owner-stated damage rule |
| Double critical-hit damage | A double critical hit uses **maximum damage plus two additional damage-dice rolls**, not the same damage as a single critical. For a base `1d8` attack (before modifiers), that is `8 + 2d8`. | Owner-stated damage rule | 
| Triple critical success | A triple critical success in combat **reduces the attacked opponent to 0 HP** and requires **three consecutive natural 20s**. An expanded critical range can reach a double critical but **cannot** contribute a non-20 result to a triple. | Owner-confirmed eligibility rule applied to Fool's Gold-derived tier-three consequence |
| Triple critical failure | A triple critical failure **reduces the failing character to 0 HP**. | Fool's Gold-derived consequence adopted by owner; precise scope beyond combat open |
| Reaching 0 HP | **5e-style death saving throws remain available** even when reaching 0 HP through a triple critical. This rule must not automatically become a 3.x negative-HP instant-death effect. | Owner rule; edge cases open |
| Critical failure consequences | A **single or double critical failure** has **GM-adjudicated consequences**, not a required global fumble table. Examples the owner has seen include a spell going wild or a weapon being knocked from its wielder's hand. The **triple failure at 0 HP** remains a specified exception. | Owner gameplay policy; GM determines situational consequences |
| Scope of critical rules | Whether critical successes/failures apply to attacks alone or also checks, saves, or other rolls is decided **by the DM for the table/campaign**, not as an engine-wide fixed scope. | Owner gameplay policy; DM-owned scope |
| Escalation rolls | Every follow-up roll used to escalate a critical is a **straight, unmodified d20 roll**, without Advantage, Disadvantage, Emphasis or numeric modifiers. | Owner gameplay rule |
| Critical chain termination | A follow-up roll **outside the applicable critical range** ends the success chain without changing the earned tier; a **qualifying non-20 critical** may advance a chain to **double**, but immediately ends/caps that chain because triple requires three natural 20s. For failures, a follow-up other than natural 1 ends the chain without altering the earned failure tier. | Owner gameplay rule |

### Critical stacking — Fool's Gold mechanic adopted for Dorks & Dice

On a natural **20**, mark a critical success and roll again. Another 20 makes it a **double critical** and prompts another roll. A third consecutive natural 20 makes it a **triple critical**. In combat, that successful attack puts its target at **0 HP**.

An **expanded critical range** (for example, natural 19–20 on a qualifying attack) also participates in stacking: a critical result within that range can start or extend the critical-success chain. **Any qualifying non-20 value caps the chain at a double critical**. For example, `19 → 20` and `20 → 19` both yield a double critical if 19 is within the applicable critical range, but neither can escalate to triple. Even `19 → 20 → 20` cannot become triple because the initial 19 disqualifies triple; stop at double instead. A triple critical **always** requires `20 → 20 → 20`. Escalation dice remain straight unmodified d20 rolls; the feature's critical range determines which natural values **qualify** as a critical, not a modifier to the escalation roll.

Conversely, natural **1s** can accumulate as critical failures. The GM adjudicates situational consequences for one or two consecutive natural 1s (for example, a spell going wild or a weapon being knocked from a wielder's hand), rather than following a mandatory critical-fumble table. **Three consecutive natural 1s** produce a **triple critical failure**, putting the failing character at **0 HP**. Death saving throws are retained from the 5e family.

Each escalation attempt is a **single unmodified d20 roll**, with no Advantage, Disadvantage, Emphasis, or numeric modifier. For **critical successes**, a follow-up result outside the currently applicable critical range ends the chain without changing the achieved tier. A qualifying **non-20** follow-up may produce a double critical, but prevents any further escalation. For **critical failures**, a follow-up result other than natural 1 ends the chain without changing the tier. **The previously achieved critical tier is retained**. The follow-up cannot convert a critical success into a failure or vice versa. The **DM sets the scope** for which kinds of d20 rolls at that table can critically succeed or fail.

**Critical damage progression:**

- **Single critical success (natural 20):** maximum damage plus **one** additional damage-dice roll. Example for base `1d8` damage (before modifiers): `8 + 1d8`.
- **Double critical success (20 → 20):** maximum damage plus **two** additional damage-dice rolls. Example for base `1d8` damage (before modifiers): `8 + 2d8`.
- **Triple critical success (20 → 20 → 20):** in combat, reduce the target to **0 HP**; the normal damage formula no longer determines that stated outcome.

These examples exclude static damage bonuses and other dice deliberately. The owner has not yet specified how ability bonuses, additional feature-granted dice, or damage riders contribute to the maximized and rolled portions.

**Attribution and adaptation:** Fool's Gold is the source of the consecutive critical-success/failure tiers **and the triple-critical consequences**. Dorks & Dice is **adopting**, not inventing, those rules. The owner's separately stated critical-damage progression uses **maximum damage plus one additional damage-dice roll for a single critical or two for a double critical**; retaining **5e-style death saving throws** integrates the adopted stacking mechanic into Dorks & Dice combat. The additional rolls may superficially resemble 3.x critical confirmation, but should **not** be presented as the origin or mechanism of Fool's Gold stacking: repeated natural 20s/1s escalate severity rather than merely confirming a threatened hit against AC.

**Source-verification boundary:** This specification records the owner's account of the Fool's Gold rules, not a checked transcription of the published source. The **double critical success damage** is explicitly defined by the owner for Dorks & Dice; do not attribute that exact arithmetic to Fool's Gold without independent verification. Single/double critical failure consequences are **intentionally left to the GM**, not awaiting a universal damage or fumble formula.

### Critical rule details remaining open

1. **Range applicability details:** Critical results from an expanded range **do** stack up to double, but only `20 → 20 → 20` can produce triple. Still verify how multiple expanding features, source-defined modified critical range, or effects that make a result critical without a natural result interact; do not assume these special cases are equivalent.
2. **Beyond triple:** What happens, if anything, after the third matching natural 20 or 1? The owner has specified effects through triple, not beyond.
3. **Damage arithmetic:** Define precisely what is maximized and rerolled (weapon dice, extra dice, static modifiers), and how immunities, resistances, damage prevention, and multi-target effects interact.
4. **0-HP effects:** Confirm the exact interaction with immunities, special creature abilities, active conditions, and special death/defeat features. Normal death saving throws remain in scope as already established.
5. **Reroll capabilities:** The escalation die is specified as a straight unmodified d20 without Advantage, Disadvantage or Emphasis. If any abilities explicitly permit rerolling an individual d20 after the fact, their applicability to an escalation die has not been separately addressed.

**Not open:** the DM's ability to decide the scope of criticals; the GM's adjudication of ordinary/double critical-failure consequences; unmodified escalation dice; preservation of an achieved tier when the follow-up roll does not qualify; **expanded-range criticals stacking only up to double**; and **triple critical success requiring three consecutive natural 20s**. These are user decisions, not unanswered design questions.

### Native combat regression scenarios

- A Block Initiative encounter uses **block resolution** even when some participants or sources originate in 3.x, PF1e, 2014 5e, or 2024 5e.
- An attack targets **Touch AC** and is not rewritten into advantage; a Flat-Footed AC effect is not rewritten into a Dexterity saving throw.
- A natural **20, then non-20** yields a single critical and `max + 1 damage-dice roll`; **20 → 20 → non-20** yields a double critical and `max + 2 damage-dice rolls`; **20 → 20 → 20** yields the combat target at **0 HP**. For base `1d8` damage, the first two are `8 + 1d8` and `8 + 2d8` before modifiers.
- A natural **1 → 1 → 1** yields the failing character at 0 HP. A surviving combatant's resulting death-save process remains available under the adopted 5e-style dying rules.
- **1 → non-1** and **1 → 1 → non-1** remain critical failure tiers one and two; the GM adjudicates any narrative or mechanical consequences. **20 → 20 → 1** is a retained double critical **success**, not a critical failure.
- Escalation dice ignore Advantage, Disadvantage, Emphasis and numeric bonuses/penalties, whatever mode governed the original check. DM-defined campaign scope determines whether a check, save or attack is eligible for criticals; no global attack-only assumption.
- With critical range **19–20**, a sequence `19 → 20`, `20 → 19`, or `19 → 19` produces **double critical**; `19 → 15` produces **single critical**. `19 → 20 → 20` remains **double** because a non-20 participated. `20 → 20 → 19` remains **double**, not triple. Only `20 → 20 → 20` produces **triple**. With normal 20-only range, a 19 does not advance the chain.
- A **normal critical hit** uses maximum-plus-roll damage rather than 2× dice. All still-open arithmetic and critical-tier interaction rules must have approved examples before code implementation.
- An imported opportunity-attack rule with broader 3.x triggers does not silently rewrite the Dorks & Dice native trigger policy when that policy is established.

## Combat systems comparative evidence (non-authoritative)

The five-system [combat comparison](combat-system-comparison-3x-5x-pf1.md) documents shared combat intentions and specific differences across D&D 3.0, 3.5, 2014 5e, 2024 5e, and Pathfinder 1e. It covers action economy, surprise, threatened areas, defensive statistics, criticals, conditions, damage, and multiple maneuver procedures. **It does not select the Dorks & Dice combat defaults.** Use it when refining the owner's proposed native combat system, preserving an independent description of owner intent before proposing borrowed mechanics.

## Combat maneuvers — owner-ideas-first design worksheet

**Status: owner has ideas; no Dorks & Dice maneuver-resolution formula or default list is approved.** Do not first translate these ideas into Pathfinder's CMB/CMD scheme, D&D 3.x attack-of-opportunity procedures, or D&D 5.x contested checks/saving throws.

An initial *working model* to evaluate is that a **maneuver is a declared combat action or effect that changes the situation of one or more targets**, often positioning, restraint, control, equipment possession, or status rather than simply reducing hit points. This is intentionally wider than a combat-statistic or opposed-check category, and it is only a starting definition.

Capture the owner's intended rules for each candidate maneuver through common questions:

- **Intent/outcome:** What is the character attempting to accomplish? Is it a separate action, an option on an attack, or the effect of a feature?
- **Acquisition:** Available to everyone, trained characters, or granted by a feature? Any proficiency, weapon, size, or level requirements?
- **Cost and timing:** Action, attack replacement, bonus action, reaction, free interaction, multiple turns, or a sustained state?
- **Target and range:** Creature/object, size limits, reach, location, environment, equipment, visibility, or free hand required?
- **Resolution:** What rolls or checks occur, who makes them, what statistic or defense matters, and what outcomes depend on the margin of success? No universal formula assumed.
- **Consequences and persistence:** Movement, prone, grappled, pinned, disarmed, restraint, equipment effects, duration, escape, reactions, or other changes? Who owns persistent state?
- **Interactions:** Bonuses/penalties from size, competencies, feats, class features, creature traits, weapons and conditions; stacking; repeated attempts; ties.
- **Source adaptation:** How should a 3.x, 5.x, or later PF1e maneuver map to a native intent/outcome while preserving different original resolution procedures when needed?

Candidate examples for *comparison after the owner's explanation*, not an approved list: grapple, shove/forced movement, trip/knock prone, disarm, overrun, sunder, and restraint. The old Rules Core computes a `combat.grapple` **modifier**, but this is not the same as having a full native grapple **procedure**.

### First native design checkpoint

Obtain the owner's description of (1) what counts as a maneuver, (2) what makes different maneuvers use shared or distinct resolution, (3) how training or features improve them, and (4) the intended consequences. Record these **before** presenting Pathfinder or D&D formulas. It is acceptable to leave numeric tuning and exotic cases open while agreeing on the underlying procedure structure.

## Pre-implementation acceptance gate

Do not begin the engine, persistence, or importer rewrite until the owner has reviewed an independently intelligible **native gameplay specification** that:

- distinguishes settled, proposed, and open mechanics without falsely upgrading implementation accidents into rules;
- replaces native epic-only level progression with Level Bands and explicit track/transition semantics;
- states an initial coherent combat-maneuver design in the owner's own terminology, including at least grapple and one movement/control maneuver;
- covers a **playable end-to-end loop**: character creation → advancement → basic checks → turn/combat → conditions/damage → recovery → exploration;
- states automatic conversion scope/fidelity rules for 3e, 3.5e, 2014 5e and 2024 5e, with PF1e as a possible later input;
- identifies remaining decisions by priority and explicitly records any intentional omissions/optional rules;
- uses Pathfinder and other systems for **subsequent critical comparison**, without requiring external game rules to supply unknown core procedures;
- includes owner approval of the gameplay baseline **before** an implementation design or code phase starts.

Architecture, database, performance tests, and legacy-to-new migration remain later tasks. They may be inventoried for safety during Phase 0 but should not dictate game design.
