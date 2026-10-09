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
