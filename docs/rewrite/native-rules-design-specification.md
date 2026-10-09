# Dorks & Dice — native rules specification (pre-implementation)

Status: **working game-design specification**, started 2026-10-09. This document records the owner's established gameplay decisions, proposed generalized rules, and unresolved decisions for review. It is **not authorization to implement**, replace the production Rules Core service, or treat draft procedures as approved rules.

## Mission and compatibility premise

Dorks & Dice is a distinct, authoritative tabletop RPG system. Rules Core will host and evaluate that system. D&D 3e, D&D 3.5e, 5e (2014), and 5.5e (2024) are *compatibility inputs*, not alternate hidden base systems. **The intended experience is analogous to Pathfinder First Edition's compatibility with D&D 3.5e, with Dorks & Dice providing automated translation rather than relying on manual conversion alone.** Pathfinder 1e is a potential additional compatibility input, not yet a promised importer or incorporated default ruleset.

Automated conversion must preserve semantic intent and meaningful mechanical differences. A converted source rule has a Dorks & Dice concept and an implementation profile; known equivalents and compatible additive content may be mapped automatically, while ambiguous or contradictory rules need visible adjudication. Full source provenance, exact editions, allowed uses, and licenses remain intact. Importing an option does not automatically activate it as a native/default Dorks & Dice mechanic.

**Different compatibility deliverables must be scoped independently:** (a) source-rule/content conversion into the hosted ruleset; (b) character, creature, encounter, and campaign-state conversion for a future migration workflow; (c) preservation of source-native play behavior versus adoption of Dorks & Dice-native defaults. The requirement for automated rules conversion does not imply that all existing characters or campaigns can be converted losslessly without review.

## Governing design principle — individual choice of complexity, one shared game

**Owner-established design goal (2026-10-09):** A player who prefers the comparative simplicity of D&D 5.x **must be able to play at the same table and in the same encounter** as a player who prefers the mechanical depth of D&D 3.x. **Each player chooses the level of mechanical complexity they personally want to interact with.** Some compromise from both source systems is acceptable, but this is the guiding principle governing those compromises. This requirement takes priority over a maximally faithful simulation of either edition's action economy or combat procedures.

**One authoritative Dorks & Dice ruleset, not parallel editions at the table.** Participants share encounter state, turn/action budgets, spatial relationships, defenses, conditions, damage, and adjudicated effects. Complexity is primarily exposed through **available choices and the detail a player elects to manage**, not by placing different players under contradictory game laws. The game must remain playable without players switching to an entire 3.x or 5.x rules mode.

### Required design outcomes

- **Simple playable path:** A player can choose a straightforward character and reliably take understandable common actions (attack, move, cast, help, defend, interact, attempt a basic shove or grapple where allowed), with concise consequences and no mandatory familiarity with advanced maneuver notation, BAB, CMB/CMD, or the origin edition of another player's abilities. **Playable and welcome does not mean equal in versatility, optimization, or power** to a character whose player elects and masters more intricate options.
- **Deeper optional path:** Another player can opt into a more tactical or rules-dense character: specialized maneuvers, class and weapon feature combinations, detailed positioning, precise action substitution, and more complex advancement options, subject to normal character eligibility and campaign permissions. **Some complex mechanics should remain complex**, including where their additional capability depends on multiple constraints, sequencing choices, or interactions; do not force an oversimplified procedure merely to match the simpler path.
- **Shared outcomes:** A basic shove and an advanced pushing technique may use different activation/check requirements but resolve into the **same forced-movement and positioning rules**. An ordinary attack and a mastered weapon attack use the same target/defense/damage/condition infrastructure.
- **Accept asymmetry of capability and power:** The owner explicitly expects players who choose richer, more involved mechanics to often be **more capable and stronger** than players pursuing straightforward play, just as mastery of options can increase effectiveness in other games. **Equal power between simple and complex playstyles is not a design requirement.** Preserve the meaningful tactical and character-building advantages of optional complex mechanics rather than flattening their effects into an equally effective simplified counterpart. Character grants, limits, action costs, and genuine source restrictions still apply; this acceptance is not permission to create powers or free actions that were never granted.
- **Limited spillover:** When an advanced player's option affects someone else, the other participant needs to understand **the resulting state and any response required**, not every intermediate rule or source-edition formula that produced it. A GM or tool may handle calculations, but the core tabletop rules must remain understandable without software.
- **Permission remains real:** The player's desired depth is not permission to use unearned weapon masteries, feats, attack sequences or proficiency benefits. Character features, campaign allowances and resource/action costs still govern which complex options are actually available.
- **DM scope and campaign defaults:** The GM retains table-wide adjudication and campaign controls; personal preference does not let a player change global combat physics, immunity behavior or opponent defenses in isolation. The goal is player-local optional complexity **within one coherent shared ruleset**, not incompatible player-specific house rules.

### Examples / requirements to test

1. **Mixed-depth attack encounter:** One player makes an ordinary attack with no mastery or declared special maneuver. Another uses a granted weapon mastery, or a detailed attack-replacement maneuver with extra triggers. Both attacks can target the same enemy and feed the same HP, defense and condition model. Only the involved player and GM need the advanced procedure.
2. **Prone and displacement:** A simple character sees that an opponent has been pushed or knocked Prone and knows the effect. A more invested player may care whether it came from Topple, Trip, Shove, a class feature or spell. The effect's shared resolution is consistent without equating all the procedures that caused it.
3. **Complex build, simple turn:** A player may own numerous proficiencies, features or derived statistics without having to choose a multi-step maneuver every round. The normal action/movement path must remain available, although choosing it may forgo the tactical or numerical advantage of using more involved features.
4. **No invented extra turn:** A 3.x full attack, 2024 Nick or Cleave, 5.x Extra Attack, or other tactical option must not silently grant extra action resources **beyond what their actual features authorize** during conversion. An explicitly granted extra attack or valuable complex interaction is allowed and need not have an equally effective simple counterpart.
5. **Cross-player defense:** A complicated attacker should not force a simpler defender to switch to an alternative edition's attack/defense procedure. The defender receives a normal resolution request and understandable consequence within Dorks & Dice.

### Decisions still needed to realize this principle

The **goal** is established. The **exact mechanical layering is not yet approved**: what everybody can attempt by default; which maneuvers and weapon mastery effects require special character grants; how optional complexity delivers its additional capability without making it mandatory for everyone; which source restrictions prevent unintended extra benefits during conversion; and how much intermediate detail a GM/Tool must expose. **Do not require simpler and more detailed procedures to be equally effective**, or redesign complex mechanics solely to erase their advantage. Assess understandability, option accessibility, source fidelity and deliberate balance consequences, rather than choosing a source edition wholesale or imposing numerical parity.

## Aspirational stretch goal — drop-in 5e character sheets for one-shots

**Owner preference (2026-10-09):** It would be desirable, as a **massive stretch goal**, for a guest who arrives at a Dorks & Dice one-shot with an already-filled **5e character sheet** to participate in the same game with reasonable DM accommodation, even if the conversion is imperfect. This is **not an approved promise of seamless play, not required for first release, and not a prerequisite for Gate 0 completion or the greenfield rewrite**.

Distinguish three separate compatibility goals:

| Goal | Status and meaning |
| --- | --- |
| **Imported rules/content compatibility** | **Required by the rewrite:** translate 2014/2024 5e and 3.x source mechanics into the authoritative Dorks & Dice system, with explicit decisions when meanings differ. |
| **Mixed-complexity play** | **Governing design requirement:** players choosing simple or complex Dorks & Dice options can participate together under one shared game. |
| **Drop-in existing character sheet** | **Long-term stretch:** allow a guest to keep a familiar 5e paper/digital sheet and play a one-shot with minimal setup, instead of creating a new Dorks & Dice character or fully migrating its state. |

### Plausible low-friction bridge (working idea, not a decided rule)

- Treat the visitor's filled-in sheet as the **player-facing interface**, rather than requiring a Dorks & Dice Character Sheet import or conversion flow.
- Read familiar information directly where meaningful: ability scores/modifiers, skill bonuses, attack bonuses, HP, speed, equipment, basic class features, spell slots and known spells. **Do not assume every numeric statistic or formula is mechanically equivalent without context.**
- Use the **shared Dorks & Dice procedures** for turn order (Block Initiative), movement, criticals, conditions and encounter effects. The visitor need not memorize the old 3.x implementation profiles used by other players.
- Let the GM apply a **small set of published conversion/adjudication guidelines** for missing or incompatible information. For instance, a 5e sheet typically does not separately expose Touch or Flat-Footed AC: do not silently pretend its ordinary AC supplies those values. The GM may resolve a missing value explicitly for that one-shot.
- Leave advanced Dorks & Dice-only choices **optional for the visitor**. A 5e sheet should not imply unearned weapon mastery, prestige advancement, special maneuvers, or other capabilities the visitor's actual features do not grant.
- Preserve the distinction between **2014 5e** and **2024 5e**, which may differ in class features, spells, weapon mastery and other behavior, without making edition identification a large mandatory onboarding process.
- Accept that **case-by-case exceptions and balance disparities** may remain. A playable one-shot with limited GM rulings is the stretch goal; perfect character fidelity, automatic rebuild, lossless migration, and zero DM intervention are not.

### Future evaluation cases — intentionally not release gates

1. A guest presents an existing character sheet and makes ordinary attacks, checks and saves without rebuilding the character.
2. Their 5e actions, movement, spell resources and familiar features operate alongside Dorks & Dice Block Initiative and a more complex native character's maneuvers.
3. When another player's ability targets Touch AC or causes a native condition, the guest/GM receives an understandable resolution even if the sheet lacks a directly corresponding field.
4. The GM can recognize ambiguous or incompatible abilities, make an explicit ruling, and continue play, rather than silently producing unjustified derived values.
5. A one-shot is playable with the paper sheet and a concise compatibility reference **without requiring software or a fully converted persistent character record**.

**Scope guardrail:** Do not add mandatory adapters, parser work, consumer migration, back-compat mode switches, or release-blocking acceptance tests now solely to satisfy this aspirational scenario. Structure native mechanics and documented conversion relations so this path is possible later when resources permit.

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
| Action economy | **Merge the 3.x/PF1e and 5.x action economies into one native Dorks & Dice system**, recognizing their common functions while making conversion differences explicit. Do not require players to select an edition-wide action economy. **Use 5.x-style flexible movement as the native baseline**, while retaining rule-specific full-turn/full-round restrictions when expressly required. | Owner-confirmed unified system and movement baseline; other budget/timing semantics remain provisional |
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

### Critical stacking — Fool's Gold-inspired foundation with Dorks & Dice adaptations

On a natural **20**, mark a critical success and roll again. Another 20 makes it a **double critical** and prompts another roll. A third consecutive natural 20 makes it a **triple critical**. In combat, that successful attack puts its target at **0 HP**.

**Dorks & Dice-specific ruling (Fool's Gold handling not verified):** An **expanded critical range** (for example, natural 19–20 on a qualifying attack) also participates in stacking: a critical result within that range can start or extend the critical-success chain. **Any qualifying non-20 value caps the chain at a double critical**. For example, `19 → 20` and `20 → 19` both yield a double critical if 19 is within the applicable critical range, but neither can escalate to triple. After `19 → 20`, **do not roll a third escalation die**: that chain is already capped at double. A non-20 can never be part of a qualifying triple-success chain. A triple critical **always** requires `20 → 20 → 20`. Escalation dice remain straight unmodified d20 rolls; the feature's critical range determines which natural values **qualify** as a critical, not a modifier to the escalation roll.

Conversely, natural **1s** can accumulate as critical failures. The GM adjudicates situational consequences for one or two consecutive natural 1s (for example, a spell going wild or a weapon being knocked from a wielder's hand), rather than following a mandatory critical-fumble table. **Three consecutive natural 1s** produce a **triple critical failure**, putting the failing character at **0 HP**. Death saving throws are retained from the 5e family.

Each escalation attempt is a **single unmodified d20 roll**, with no Advantage, Disadvantage, Emphasis, or numeric modifier. For **critical successes**, a follow-up result outside the currently applicable critical range ends the chain without changing the achieved tier. A qualifying **non-20** follow-up may produce a double critical, but prevents any further escalation. For **critical failures**, a follow-up result other than natural 1 ends the chain without changing the tier. **The previously achieved critical tier is retained**. The follow-up cannot convert a critical success into a failure or vice versa. The **DM sets the scope** for which kinds of d20 rolls at that table can critically succeed or fail.

**Critical damage progression:**

- **Single critical success (natural 20):** maximum damage plus **one** additional damage-dice roll. Example for base `1d8` damage (before modifiers): `8 + 1d8`.
- **Double critical success (20 → 20):** maximum damage plus **two** additional damage-dice rolls. Example for base `1d8` damage (before modifiers): `8 + 2d8`.
- **Triple critical success (20 → 20 → 20):** in combat, reduce the target to **0 HP**; the normal damage formula no longer determines that stated outcome.

These examples exclude static damage bonuses and other dice deliberately. The owner has not yet specified how ability bonuses, additional feature-granted dice, or damage riders contribute to the maximized and rolled portions.

**Attribution and adaptation:** Fool's Gold is the source of the consecutive critical-success/failure tiers **and the triple-critical consequences**. Dorks & Dice is **adopting**, not inventing, those rules. The owner's separately stated critical-damage progression uses **maximum damage plus one additional damage-dice roll for a single critical or two for a double critical**; retaining **5e-style death saving throws** integrates the adopted stacking mechanic into Dorks & Dice combat. The additional rolls may superficially resemble 3.x critical confirmation, but should **not** be presented as the origin or mechanism of Fool's Gold stacking: repeated natural 20s/1s escalate severity rather than merely confirming a threatened hit against AC.

**Source-verification boundary:** This specification records the owner's account of the Fool's Gold rules, not a checked transcription of the published source. The owner **does not know how Fool's Gold handles expanded critical ranges**. Treat the rule allowing a 19 (or other non-20 within critical range) to stack **up to double**, while requiring **three natural 20s for triple**, as an **independent Dorks & Dice design decision**, **not** as an attributed Fool's Gold rule. The **double critical success damage** is also explicitly defined by the owner for Dorks & Dice; do not attribute that exact arithmetic to Fool's Gold without independent verification. Single/double critical failure consequences are **intentionally left to the GM**, not awaiting a universal damage or fumble formula.

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
- With critical range **19–20**, a sequence `19 → 20`, `20 → 19`, or `19 → 19` produces **double critical**; `19 → 15` produces **single critical**. `19 → 20` ends immediately at **double**, with no third escalation die, because a non-20 participated. `20 → 20 → 19` remains **double**, not triple. Only `20 → 20 → 20` produces **triple**. With normal 20-only range, a 19 does not advance the chain.
- A **normal critical hit** uses maximum-plus-roll damage rather than 2× dice. All still-open arithmetic and critical-tier interaction rules must have approved examples before code implementation.
- An imported opportunity-attack rule with broader 3.x triggers does not silently rewrite the Dorks & Dice native trigger policy when that policy is established.

## Unified action economy — design direction and provisional mapping

**Owner direction:** The D&D 3.x / Pathfinder 1e and D&D 5.x action economies are close enough to **merge into a single native Dorks & Dice action economy** rather than remain separate edition-specific turn systems. This is an accepted **structural design decision**. The owner has also approved **5.x-style flexible movement as the native baseline**: movement is an independently spendable allowance and may be divided around other permitted activities, including between attacks where the attack/feature permits it. **A particular action or ability can explicitly impose full-turn/full-round restrictions** that constrain ordinary flexible movement. Other budget and conversion details below remain working candidates, not yet confirmed gameplay rules.

| Working native concept | D&D 3.0 / 3.5 / PF1e analogue | D&D 2014 / 2024 analogue | Conversion warning |
| --- | --- | --- | --- |
| **Action** | Standard action | Action | Most directly similar, but a 3.x standard attack is not automatically a 5.x Extra Attack action |
| **Movement / move-type activity** | Move action or move-equivalent action | Movement; occasionally Action to perform some nonmovement activity | **Native baseline is independent, splittable 5.x-style movement**, not a mandatory 3.x move action. How an imported nonmovement move-equivalent activity consumes native resources remains **undecided** |
| **Quick action** (working name) | Swift action (3.5 supplements / PF1e; no universal 3.0 equivalent) | Bonus Action | 5.x requires a specific feature to grant an eligible Bonus Action; a swift action has its own source timing/rules. Map permitted uses, not an unrestricted extra action |
| **Reaction** | Immediate action / attacks of opportunity | Reaction / opportunity attack | A 3.x immediate action may spend the upcoming swift action; 3.x opportunity attacks may have multiple uses and separate triggers. Do not quietly erase those costs |
| **Free activity** | Free action or no-action activity | Free activity, one ordinary object interaction, or feature-defined no-action activity | Number and scope of interactions differ; unlimited free object manipulation cannot be inferred |
| **Full-turn commitment** (working name) | Full-round action | Activities that require, constrain or span the character's turn | **Keep an explicit full-turn restriction when a particular rule requires it.** A 3.x full attack commonly restricts movement to a 5-foot step, while 5.x Extra Attack permits movement between attacks; they are **not automatically interchangeable** |

### Why unify the model without flattening timing

The common model should answer **what portion of a turn an activity consumes**, **when it can occur**, **what grants it**, and **what activity it excludes**. A source-defined cost can map to a native cost plus explicit restrictions rather than remain a separate set of edition-specific turn counters.

**Approved movement behavior:** Each participant has a movement allowance independent of the primary Action. In ordinary play, movement can be split before and after other permitted activities; multiple attacks may have movement between them when the applicable attack procedure allows it. A **source-specific full-turn/full-round requirement overrides this flexibility only for that particular activity**. Do not automatically assign a 3.x full attack restriction to a 5.x Extra Attack, or remove an explicit full-round restriction during automated conversion.

**Working candidate, for owner review:** Each participant ordinarily has a primary Action, eligible Quick Actions, reactions to valid triggers and reasonable Free Activities. A Full-Turn Action would be a **constraint on existing resources**, not an extra independent action granted in addition to them. The precise number of Quick Actions and reactions, possible exchanges between primary Action and movement resources, and other restricted-turn treatment are not yet approved.

A 5e-style Extra Attack and a 3.5-style full attack must be allowed to have different movement/attack restrictions even if both present multiple attacks. Likewise, the 3.x five-foot step, move-equivalent actions, free object interactions, swift/immediate resource coupling, and 3.x opportunity-attack counts must be **explicitly reconciled**, not dismissed as synonyms.

### Interaction with Block Initiative

Block Initiative remains authoritative for **whose turn or block is active**. The unified action model defines **what one acting participant can do**, not an alternative initiative order. The handling of turn-start/turn-end action refresh, reactions between allied or opposing blocks, and simultaneous group activity needs a precise integration contract; do not infer ordinary 5.x initiative sequencing or consume multiple units' budgets as one shared pool.

### Cases to verify before adopting final action-budget numbers

1. Move before/after an Action; movement between attacks granted by a 5e class feature.
2. 3.x full attack with iterative BAB attacks and its 5-foot-step restriction; no added full-turn resource beyond the consumed action/movement.
3. 3.x move-equivalent activity (stand, draw or manipulate equipment, depending on source) when the character could otherwise move.
4. Swift/immediate action interaction and 5.x Bonus Action/Reaction, including when a character has both 3.x and 5.x features.
5. Opportunity attacks under 3.x threatened-square triggers and 5.x leaving-reach triggers; this is a **separate still-open native opportunity rule**, even though the action-budget concept is unified.
6. A healing potion's previously approved Bonus Action (rolled healing) versus Action (maximum healing) policy using the unified budget.
7. Block Initiative resolution with multiple allies acting in one block, including when reactions refresh and interrupt.

### Specific remaining owner decisions

- **Move-equivalent conversion:** With independent, splittable 5.x-style movement **now approved**, how do imported nonmovement 3.x **move actions** consume Action, movement allowance, or another resource? The baseline itself is not open.
- **Full-turn operations:** **Explicit rule-specific full-turn restrictions are preserved.** Which activities impose them, precisely what movement remains permissible, and whether a 3.x five-foot step is permitted only for specified features or generally are still to be defined.
- **Quick/reaction relationship:** Are Swift and Bonus combined into one slot, and do Immediate Actions use an ordinary Reaction, an upcoming Quick Action, or both? How many opportunity attacks can be made?
- **Action substitution:** Can a standard Action be converted to additional movement or to a move-equivalent task, and can Action be used to perform an eligible Quick Action?
- **Eligibility and refresh:** What is the default availability of Quick Action, Free Activities, and Reaction, and when do they replenish under Block Initiative?

These questions refine the unified action economy; **they do not reopen the owner's decisions to unify action economy, use 5.x-style splittable movement by default, or retain explicit full-turn restrictions where a rule requires them**.

## Weapon Mastery and combat-maneuver integration — proposal for review

**Status: comparative design analysis; no Weapon Mastery unification procedure has been approved.** The owner suggested integrating Weapon Mastery during the unification of 3.x / Pathfinder 1e and 5.x combat procedures, but has not determined whether or how its effects belong to the same native maneuver system. **Evaluate all proposed integration against the established individual-complexity principle above:** a simple player must not be required to adopt weapon mastery or advanced maneuver bookkeeping to participate normally, while an interested player can engage with the full tactical options allowed by their character, **including legitimate advantages that require greater complexity**. Do not replace mastery properties or advanced maneuver resolution with equally powerful simplified versions solely to enforce parity.

### Existing Rules Core implementation is a starting point, not a finished combat evaluator

The legacy `src/RulesCore.Infrastructure/Rules/CharacterProjection/CharacterWeaponMasteryProjector.cs` already recognizes 2024 Weapon Mastery from source weapon entries, checks whether the appropriate class feature or feat grants it, projects eligible weapon choices and class-level counts, and creates per-weapon **capabilities** such as `weapon-mastery.item:longsword`. The projector does **not** implement the runtime effects of Push, Topple, Nick, Graze, Cleave, Sap, Slow or Vex. Recover its source-evidenced eligibility and selection logic; do not mistake showing a mastery capability for executing the full effect in combat.

### Shared mechanics and distinct acquisition paths

| 2024 mastery property | Source-native function | Proposed native shared operation |
| --- | --- | --- |
| Push | On eligible weapon hit, forced movement; no extra save in this source procedure | **Forced displacement**, also usable by ordinary shove/bull rush, spells and features |
| Topple | On eligible weapon hit, Constitution save to impose Prone | **Knock prone** effect, shared with trip/shove-to-prone; preserve the separate saving-throw resolution |
| Sap | On hit, disadvantage to a later enemy attack | **Conditional temporary attack modifier / timed effect** |
| Slow | On a damaging hit, reduce target movement allowance by a limited amount; repeated instances do not stack by default in source rules | **Temporary movement modifier** |
| Vex | On a damaging hit, gain advantage on an eligible follow-up attack on the target | **Conditional follow-up attack modifier** |
| Nick | Relocate the Light-property bonus attack into the Attack action rather than grant another new attack | **Attack scheduling/action-resource substitution** |
| Cleave | On eligible hit, add an attack against a second target, limited per turn | **Conditional additional attack** |
| Graze | On a miss, inflict limited damage based on an ability modifier | **Alternate miss consequence** |

**Proposed unification compatible with player-chosen complexity:** the same procedure/outcome vocabulary can serve ordinary maneuvers, weapon masteries, spells, feats and creature traits. A **maneuver** names an intended action/outcome; **mastery** names a granted permission or attack-triggered modifier. Not every mastery property is a maneuver, and not every maneuver requires a weapon or mastery. Acquisition, activation condition, action cost, target eligibility, resistance, effect, duration and per-turn limitations remain independent data about each rule.

This is analogous to the existing universal-competency approach: a shared concept can have source-specific execution profiles without forcing separate systems or falsely equating incompatible dice/defense calculations. Do not blindly turn Pathfinder CMB/CMD, 3.5 grapple checks, 2014 5e Athletics contests and 2024 mastery effects into one success formula.

### Exactly what owner decisions are still needed

**Action-economy alignment (high priority):**
1. Whether **Quick Actions** unify Bonus/Swift actions into one ordinary turn allowance, and how Immediate actions consume or borrow the same allowance versus Reactions. Block Initiative dictates turn/block ownership; refresh and interruptions require an explicit rule.
2. How nonmovement 3.x **move-equivalent actions** are charged under approved flexible movement; what full-round restrictions permit; whether primary Action can substitute for movement/Quick Action.
3. How **BAB iterative attacks, 5.x Extra Attack and feature-created attacks** interact on a mixed character, including whether multiple sources add attacks, override each other or remain alternative attack schedules.

**Maneuver and mastery alignment (high priority):**
4. The owner's own **combat-maneuver** proposal: which types of maneuvers exist, who can attempt them, which attacks/actions they replace, what resists them and what effects occur.
5. Should **2024 mastery eligibility and weapon-choice counts** be retained as source-specific class/feat grants, expanded into a universal native weapon-training framework, or both? Weapon proficiency **is not automatically mastery**. Existing class and feat selections should remain meaningful during conversion.
6. When a **weapon hit** both triggers a mastery (e.g., Push or Topple) and satisfies another ability or maneuver requirement, can multiple effects occur on one attack? Does choosing an attack-replacing maneuver trigger the weapon's normal hit properties? Default stacking/priority must be explicit.
7. Preserve special activation: **Push** on hit without a separate save, **Topple** on hit followed by a save, **Nick** as relocating—not duplicating—a Light-property attack, **Graze** on miss, and **Cleave** with a per-turn cap. Decide which source-specific behavior becomes Dorks & Dice-native default rather than assuming all use one contest.

**Secondary decisions (after basic native maneuver procedure):**
8. How timed Mastery effects with **until start/end of next turn** operate under **Block Initiative**, and how often per turn/block limited masteries reset.
9. How mastery attacks interact with **critical stacking**, especially Graze on a miss caused by a natural 1, additional Cleave/Nick attacks, and criticals involving more than one target.
10. How DM-selected, campaign-effective rules can modify grants/effects without invalidating legacy source profiles or source attribution.

### Conversion/regression checks to preserve

- A character's class level controls acquisition/number of mastered weapons; weapon proficiency alone does **not** imply access to the corresponding mastery property.
- A mastered **Topple** does not become an ordinary **Trip** using a different check merely because both cause Prone. A mastered **Push** does not acquire an unintended saving throw.
- **Nick** does not grant a free third Light attack on top of the bonus attack it relocates. **Cleave** does not duplicate across each attack merely because an Extra Attack, full attack or Action Surge occurs.
- A 3.5/PF maneuver feature retains its documented attack-replacement or full-round costs under the native flexible movement baseline when applicable.
- The shared forced-movement/condition executor respects movement limits, immunities, stacking policy and opportunity-attack triggers once the owner has specified those rules.

### References

- [2024 D&D Beyond: Weapon Mastery explained](https://www.dndbeyond.com/posts/1742-your-guide-to-weapon-mastery-in-the-2024-players)
- [Pathfinder 1e: Combat maneuvers, CMB/CMD](https://www.aonprd.com/Rules.aspx?ID=185)
- Current Rules Core `src/RulesCore.Infrastructure/Rules/CharacterProjection/CharacterWeaponMasteryProjector.cs` and `tests/RulesCore.IntegrationTests/CharacterWeaponMasteryProjectionTests.cs`
- [Five-system combat comparison](combat-system-comparison-3x-5x-pf1.md)

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
