# Dorks & Dice — native rules specification (pre-implementation)

Status: **working game-design specification**, started 2026-10-09. This document records the owner's established gameplay decisions, proposed generalized rules, and unresolved decisions for review. It is **not authorization to implement**, replace the production Rules Core service, or treat draft procedures as approved rules.

## Mission and compatibility premise

Dorks & Dice is a distinct, authoritative tabletop RPG system. Rules Core will host and evaluate that system. D&D 3e, D&D 3.5e, 5e (2014), and 5.5e (2024) are *compatibility inputs*, not alternate hidden base systems. **The intended experience is analogous to Pathfinder First Edition's compatibility with D&D 3.5e, with Dorks & Dice providing automated translation rather than relying on manual conversion alone.** Pathfinder 1e is a potential additional compatibility input, not yet a promised importer or incorporated default ruleset.

Automated conversion must preserve semantic intent and meaningful mechanical differences. A converted source rule has a Dorks & Dice concept and an implementation profile; known equivalents and compatible additive content may be mapped automatically, while ambiguous or contradictory rules need visible adjudication. Full source provenance, exact editions, allowed uses, and licenses remain intact. Importing an option does not automatically activate it as a native/default Dorks & Dice mechanic.

**Different compatibility deliverables must be scoped independently:** (a) source-rule/content conversion into the hosted ruleset; (b) character, creature, encounter, and campaign-state conversion for a future migration workflow; (c) preservation of source-native play behavior versus adoption of Dorks & Dice-native defaults. The requirement for automated rules conversion does not imply that all existing characters or campaigns can be converted losslessly without review.

## Governing design principle — individual choice of complexity, one shared game

**Owner-established design goal (2026-10-09):** A player who prefers the comparative simplicity of D&D 5.x **must be able to play at the same table and in the same encounter** as a player who prefers the mechanical depth of D&D 3.x. **Each player chooses the level of mechanical complexity they personally want to interact with.** Some compromise from both source systems is acceptable, but this is the guiding principle governing those compromises. This requirement takes priority over a maximally faithful simulation of either edition's action economy or combat procedures.

**Why this matters (owner clarification):** The owner could simply run D&D 3.5e if personal mechanical complexity were the only objective. The distinctive goal is the **shared enjoyment of the game among people who engage with tabletop roleplaying in very different ways**. Some players enjoy mastering detailed mechanical options; others may prioritize character portrayal, shared storytelling, social play, or a lower cognitive load. Dorks & Dice should let those preferences coexist **without requiring players to adopt the most complex participant's rules engagement or asking that participant to give up worthwhile complexity**. This is a table-experience and social design goal, not just a convenient toggle between simplified and advanced interfaces, and it does not require equal mechanical effectiveness.

**One authoritative Dorks & Dice ruleset, not parallel editions at the table.** Participants share encounter state, turn/action budgets, spatial relationships, defenses, conditions, damage, and adjudicated effects. Complexity is primarily exposed through **available choices and the detail a player elects to manage**, not by placing different players under contradictory game laws. The game must remain playable without players switching to an entire 3.x or 5.x rules mode.

### Required design outcomes

- **Simple playable path:** A player can choose a straightforward character and reliably take understandable common actions (attack, move, cast, help, defend, interact, attempt a basic shove or grapple where allowed), with concise consequences and no mandatory familiarity with advanced maneuver notation, BAB, CMB/CMD, or the origin edition of another player's abilities. **Playable and welcome does not mean equal in versatility, optimization, or power** to a character whose player elects and masters more intricate options.
- **Deeper optional path:** Another player can opt into a more tactical or rules-dense character: specialized maneuvers, class and weapon feature combinations, detailed positioning, precise action substitution, and more complex advancement options, subject to normal character eligibility and campaign permissions. **Some complex mechanics should remain complex**, including where their additional capability depends on multiple constraints, sequencing choices, or interactions; do not force an oversimplified procedure merely to match the simpler path.
- **Shared outcomes:** A basic shove and an advanced pushing technique may use different activation/check requirements but resolve into the **same forced-movement and positioning rules**. An ordinary attack and a mastered weapon attack use the same target/defense/damage/condition infrastructure.
- **Accept asymmetry of capability and power:** The owner explicitly expects players who choose richer, more involved mechanics to often be **more capable and stronger** than players pursuing straightforward play, just as mastery of options can increase effectiveness in other games. **Equal power between simple and complex playstyles is not a design requirement.** Preserve the meaningful tactical and character-building advantages of optional complex mechanics rather than flattening their effects into an equally effective simplified counterpart. Character grants, limits, action costs, and genuine source restrictions still apply; this acceptance is not permission to create powers or free actions that were never granted.
- **Limited spillover:** When an advanced player's option affects someone else, the other participant needs to understand **the resulting state and any response required**, not every intermediate rule or source-edition formula that produced it. A GM or tool may handle calculations, but the core tabletop rules must remain understandable without software.
- **Specialist-led collaboration:** An interested player (or small expert subset) can manage a complex procedure while other players **actively contribute through well-defined simpler roles**, gaining the group the real benefits of the full mechanic. Participation is not conditional on each helper knowing the whole subsystem; this is different from reducing the mechanic to an equally powerful simple version. Only apply this pattern where cooperation makes fictional and mechanical sense.
- **Cooperation independent of initiative and table composition:** **Standard initiative and optional Block Initiative** must both support all-basic, all-advanced and mixed-complexity tables. Loot Tavern harvesting/crafting illustrates separate, optional cooperative activity. Cooperation can be straightforward, intricate or combined; each character retains agency and their own resources. No particular initiative selection, specialist role or advanced subsystem is a prerequisite for ordinary play.
- **Permission remains real:** The player's desired depth is not permission to use unearned weapon masteries, feats, attack sequences or proficiency benefits. Character features, campaign allowances and resource/action costs still govern which complex options are actually available.
- **DM scope and campaign defaults:** The GM retains table-wide adjudication and campaign controls; personal preference does not let a player change global combat physics, immunity behavior or opponent defenses in isolation. The goal is player-local optional complexity **within one coherent shared ruleset**, not incompatible player-specific house rules.

### Combat design matrix — common rules, selectable depth

**Owner-favored design framework (2026-10-18):** For every combat area, identify **(1) the common, complete mechanic everybody uses** and **(2) optional additional ways for eligible characters to engage with it**. The added options can be genuinely more capable or powerful and may involve more decisions; they **do not create separate Basic/Advanced combat rulesets**. This matrix is a **design heuristic and review reference**, not approval of each individual procedure, grant, action cost or source-conversion decision.

| Combat area | Shared foundation | Optional mechanical depth |
| --- | --- | --- |
| **Action economy** | One native Action, independently splittable movement, a proposed shared Bonus/Swift category, and an individual Reaction framework | Rule-specific full-round commitments, attack substitutions, specialized reactions, and feature-specific restrictions |
| **Multiple attacks** | Straightforward ordinary Attack action using attacks actually granted to the character | BAB-derived iterative/full attacks, dual-wielding combinations, and specialized attack sequences where granted and correctly costed |
| **Attacks and defenses** | Consistent attack resolution against the relevant ordinary, Touch, Flat-Footed or other defined defense | Features targeting particular defenses, specialized modifiers, and additional attack options; defense categories are shared rules, **not player-selectable physics** |
| **Combat maneuvers** | Ordinary grapple, shove, or similar actions where the native baseline permits them | Additional maneuvers, improved techniques, attack replacements, and specialized combinations |
| **Opportunity attacks** | One predictable native triggering and Reaction policy | Feature-granted **additional eligible triggers, alternatives such as reactive maneuvers, or counterattacks using the existing Reaction**; **not more Reactions by default** |
| **Positioning** | Shared movement, reach, cover, terrain, and battlefield/condition rules | Tactical benefits from qualifying flanking, reach-control, positioning-related feats or maneuvers where granted; the shared flanking baseline itself remains to be selected |
| **Damage and conditions** | Everyone uses the same damage, critical, condition, and dying consequences | Eligible abilities that exploit a condition, modify damage, change defenses, or apply additional effects |
| **Spellcasting** | One coherent casting-time, targeting, concentration and spell-resolution framework | Metamagic, specialized spellcasting, qualified counter/interrupt options, combinations, and resource-management choices where allowed |
| **Weapon Mastery** | An ordinary weapon attack remains fully functional without mastery | Eligible characters gain weapon-specific effects and tactical options **on top of** normal combat |
| **Combo attacks** | Ordinary combat remains complete without participation | An optional **parallel cooperative procedure** inspired by Ryoko, operating without modification under either standard or Block Initiative |

**Important classifications:** This is a contrast between **shared procedures** and **additional depth**, not a prescription to move all 3.x features into feats. BAB iterative attacks and full-round limits may be part of a character's legitimate source-derived abilities or inherent costs; Touch/Flat-Footed AC, criticals and conditions belong to the shared rules; an optional spellcasting procedure may already be granted by class/spell features. The table does **not** establish final opportunity triggers, a final flanking rule, a default universal list of maneuvers, or Bonus/Swift-to-Immediate conversion. The options must be evaluated on actual source permissions and their costs, not labeled "advanced" simply because their text is long.

**The three patterns for implementing that matrix:**

1. **Simple action, advanced alternatives:** A complete ordinary action exists; an eligible character may **choose a more involved alternative** with its own meaningful restrictions and advantages. Examples to examine: distinct attack sequences, specialized maneuvers, more intricate spell choices. Do not force mechanical parity.
2. **Additional features on the same ordinary action:** The character performs a familiar action but a feat, class feature, weapon property, or similar grant **modifies what it can accomplish or permits extra choices**. Weapon Mastery and specialized uses of an existing Reaction are representative; the feat changes capability, not the shared turn budget.
3. **Optional cooperative mechanics:** A separate, opt-in activity such as Ryoko-inspired combo attacks or harvesting/crafting provides **meaningful roles, costs and shared consequences** for willing qualified participants. The group may be all-basic, all-advanced or mixed; it need not designate an expert with passive helpers. The ordinary combat system is **never replaced** by this optional participation.

**Design checks:** A simple player can use the shared mechanic without learning optional subsystems. An advanced player can use granted options to meaningful advantage and can still elect an ordinary action when appropriate. The consequences of both approaches use the same encounter state. A feature may grant substantially different power without demanding equivalent simple-path power; it must still respect acquisition requirements, eligibility, costs, and interaction limits. **Not every row requires two procedures**: conditions such as Grappled remain one shared state even if their initiation, maintenance, escape, or exploitation permit different actions. More complicated *presentation* alone does not create a feat-worthy capability.

**Boundary:** The owner favors this framework as a guide for design. Exact feats, additional reaction triggers, attack progressions, maneuver-resolution formulas and other rows are **not yet approved gameplay rules**. GM-awarded free combat feats remain explicit table exceptions, never a veteran entitlement or default feat progression policy.

### Reuse-first combat design — adapt existing procedures, resolve differences case by case

**Owner direction (2026-10-18):** In many combat areas, 5e already supplies a more approachable descendant of a 3.x concept. Treat this as a **head start**, not as a requirement to invent independent procedures. Use established Dorks & Dice house rules and the shared-foundation/optional-depth matrix above, then settle genuine differences **case by case**. Avoid reopening mechanics whose outcomes and interactions are already compatible merely because they come from different editions.

**Decision method for each unresolved combat rule:**

1. **Recover what is already defined.** Compare the actual 3.0/3.5e, 2014 5e, 2024 5e, and existing Dorks & Dice rule and confirm whether the ordinary operation really is the same. Prefer a proven, understandable **shared native default** when its math, action cost, target, and consequences can be reconciled.
2. **Preserve real additional capabilities.** Where 3.x includes richer positioning, maneuvers, full-attack sequences or reactive choices, assess how their **actual authorized character grants, costs, restrictions or appropriate feats/features** can offer that depth within the same native rules. **Complex wording alone does not justify a feat**; inherited/source-granted abilities must not be stripped and sold back.
3. **Apply established Dorks & Dice rules first.** Honor the native initiative choices, flexible movement with explicit full-turn constraints, normal/Touch/Flat-Footed AC, critical stacking, controlled-creature turns, and the optional feature/cooperation boundaries before importing conflicting source defaults.
4. **Resolve non-equivalences individually.** Do not pretend 3.5e grapple (touch attack plus opposed grapple), 2014 5e grapple (Athletics contest), and 2024 grapple (defender save) are identical simply because all can impose Grappled. Similarly, BAB full attacks and Extra Attack differ in attack-count progression and movement constraints; 3.x opportunity attacks differ from 5e Reactions in both triggers and budgets. Choose the native resolution, note legitimate feature-specific alternatives, and test mixed-content interactions instead of keeping independent edition rulesets.
5. **Document only residual differences and test them.** For each decision, record the shared rule, optional authorized extensions, source-to-native conversion, interaction hazards, and an example with basic, advanced and mixed characters. **Do not assume a separate advanced procedure is required for every mechanic.** Truly equivalent operations may need no additional design at all.

**Status:** This is an **approved approach to evaluating remaining combat mechanics**, not automatic approval of a blanket “5e always wins” rule, an automatic import of every 3.x subsystem, or a new rule granting feats or attacks for free. The remaining native rulings are limited to **actual incompatibilities and intentional departures**; their effects on action economy, prerequisites, and the same shared encounter state must still be validated.

### Arithmetic simplification — prefer existing 5e expressions when meaningful

**Owner preference (2026-10-18):** Beyond the specific **Blinded** ruling, look for other 3.x percentage checks, multiplications and divisions whose purpose can be expressed **more simply through existing 5e-style mechanics**, without eliminating the meaningful advanced choices, defense distinctions, or source-granted abilities of 3.x. The native game should require arithmetic only when it serves a real mechanical purpose; **do not treat the presence of a fraction or multiplier alone as a reason to preserve that formula**. Apply this through the already established **reuse-first, case-by-case reconciliation** method.

**Scope by type of arithmetic (candidate treatment, not blanket conversions):**

| Source calculation/use | Reuse or simplification direction | Key caveat |
| --- | --- | --- |
| **Chance of succeeding/hitting or failing** (e.g., a second 50% miss roll for an attacker who is Blinded) | Prefer the existing 5e **Advantage/Disadvantage** roll-selection vocabulary when its intended effect and scope match. **Blinded attacking with Disadvantage is already adopted.** | A percentage miss chance multiplied into attack success probability **is not numerically equal** to Disadvantage. Preserve independent miss-chance effects when their source or purpose needs them. |
| **Other situational bonuses/penalties** | Review whether the effect belongs to the **actor's roll** (Advantage/Disadvantage/Emphasis) or the **target's status and AC contributions** under the established effect-ownership rule. | Do not erase 3.x ordinary/Touch/Flat-Footed AC, or replace a genuine static modifier with Advantage without checking what changes. |
| **Critical damage multipliers** | Use the **already approved Dorks & Dice critical-stacking and damage-dice procedure**, not 3.x's generic critical multiplier as a parallel default. | Source features with distinct multipliers may need a separately reviewed compatibility mapping; do not erase meaningful permissions. |
| **Resistance and Vulnerability damage fractions** | Reuse 5e's understandable **half damage** and **double damage** where they genuinely fit the imported resistance/vulnerability concept. | 5e itself **retains multiplication/division** in damage calculations; 3.x fixed damage reduction, energy resistance and immunity are not automatically interchangeable with Resistance. |
| **Damage ability-modifier scaling** (e.g., 3.x two-handed 1.5× Strength or off-hand half Strength) | Compare with 5e weapon/attack-feature presentation and consider simplifying **only when the combat style's identity survives**. | These changes directly alter damage, weapon niches and feats; no wholesale conversion to flat/full modifiers is approved. |
| **Movement, resources or progression fractions** | Keep or simplify through normal, visible 5e-style rules *when appropriate*. | Half-speed/half damage, class advancement fractions and rounding conventions can be substantive mechanics. Do not replace them with Advantage/Disadvantage, which affects d20 odds instead. |

**Compatibility and fairness checks:** Compare each candidate's effect on hit odds, damage and scaling, source eligibility, action cost and stacking. Preserve source provenance and document an explicit native change where the replacement is similar in intent but different in probability or outcome. Mechanically meaningful fractional effects can remain even if most players use simpler actions. Tabletop play must remain understandable without software, and existing Dorks & Dice rulings override generic source conventions.

**Status:** The **arithmetic-simplification preference** is a design guideline for evaluating other mechanics, not approval to replace all 3.x multipliers/divisions or remove general miss-chance, armor, damage and progression mechanisms. **Blinded Disadvantage and the native critical-stacking design are already decided separately**; other listed transformations need their own checks only where an actual conflict exists.

### Cooperative play as a positive design goal

**Owner clarification:** The design must support **a whole table of basic players, a whole table of advanced players, and any mixture of the two**. The earlier specialist-plus-helpers framing was too narrow: standard initiative, optional Block Initiative and Loot Tavern Harvesting & Crafting Lite all leave room for parties to coordinate without requiring a designated split between an expert and casual participants. Block Initiative changes the opportunities for within-block planning, but **must not be presumed active**; harvesting offers separate skilled roles and helper contributions with its own actual participation rules. These are different cooperation mechanisms, not a generic helper bonus or pooled action allowance.

**Review questions for future mechanics:** (1) Can an all-basic group enjoy the game and cooperate without appointing a player to master an optional subsystem? (2) Can an all-advanced group use several genuinely complex options and coordinate them without being artificially assigned one specialist and the rest helpers? (3) Can a mixed group cooperate at different depths without forcing complexity on the less interested participants? (4) Can a character/player choose a different depth from one activity to the next? The answers need not produce identical rewards or power across complexity levels, and a particular optional activity can retain the skilled participants and requirements it genuinely needs. Cooperation must not mean an expert taking ownership of another player's character or choices.

### Existing example: Loot Tavern Harvesting & Crafting Lite

**Owner-identified positive model:** Harvesting/crafting supports different combinations of participation. A mixed group may have a mechanically invested participant coordinate Assessment, Carving, materials and crafting while others choose straightforward eligible contributions. An advanced group may have several interested participants engaging in distinct specialized roles, subject to the actual assessment/carving and helper limits. An all-basic party does not need to pursue the optional advanced harvesting activities to have a valid game session. **The system is not designed around the assumption that every table contains one specialist and several helpers.**

The currently deployed Rules Core mechanics (see `docs/character-mechanics-consumer.md` and `src/RulesCore.Domain/Rules/HarvestingRules.cs`) distinguish Assessment and Carving checks, include a first-class `helpers` contributor group, apply creature-size-dependent helper limits, and calculate full proficiency for proficient helpers or half proficiency rounded down for non-proficient helpers. Helpers are subject to actual participation/eligibility requirements; the generic Help action is **not** an interchangeable harvesting/crafting bonus. The responsible player or GM can manage those details while a helper simply chooses to help and supplies relevant character facts.

**Design insight, not a new universal procedure:** Where suitable, expose a clear **specialist role** for resolving complex choices and **limited participation roles** for companions, with defined eligibility, contribution, and shared outcome. Do not force each participant to learn assessment and carving arithmetic, do not make the helpers mere spectators, and do not generalize the harvesting bonus/participant-count formulas to unrelated mechanics.

**Potential applications to evaluate, not established rules:**
- Complex combat setups, positioning or tactical combinations in which a mechanically involved character directs the procedure and other characters provide help or follow-up actions without using that procedure themselves.
- Crafting, enchanting, investigation, research, expedition planning, or other group tasks where a lead participant makes the intricate decisions and allies offer meaningful contributions.
- Tools can explain the simple helper's **own requirement and effect** while making deeper subsystem detail available to the specialist/GM; this separation should remain practical around a physical table.

**Acceptance scenarios:** A mixed group can have a knowledgeable participant manage the relevant harvesting procedure while other eligible characters contribute through simple helper roles. A group of interested players can engage with Assessment, Carving, and helper rules through the different roles those actual rules allow, without inventing additional concurrent lead roles or overriding helper caps. An all-basic group can choose not to engage with optional harvesting while remaining fully capable of playing Dorks & Dice; ordinary game participation must not rely on compulsory harvesting mastery. Preserve the source's original collaboration constraints and attribution. Do not promise equal crafting/harvesting rewards to parties that decline the procedure.

### Candidate combat example: Loot Tavern's Ryoko's Guide combo attacks

**Owner-recommended reference (2026-10-09), not an adopted native mechanic:** *Ryoko's Guide to the Yokai Realms* from Loot Tavern offers **party combo attacks** and an **advanced weapon masteries** system. The [publisher's product description](https://loottavern.com/product/ryokos-guide-to-the-yokai-realms-book/) confirms these as distinct features of the 5e supplement and describes simultaneous/coordinated group attacks. The user raised this immediately after discussing how Block Initiative and Loot Tavern harvesting enable cooperation at basic, advanced and mixed-complexity tables.

**Design relevance:** Unlike harvesting, which is a specialized noncombat procedure, this is **direct combat cooperation**. A group may choose straightforward joint tactics, several players may invest deeply in coordinated combinations, or a mixed party may combine simple support with sophisticated setup. It is therefore a potentially useful third example alongside **optional Block Initiative (one coordination method)** and **Harvesting (different participation roles)**; combo attacks must also function with **standard initiative**. Do not assume a party must contain both novices and experts for combos to be useful.

**Relationship to already considered mechanics:** Combo attacks are a **parallel, optional cooperative attack system** that adds to normal combat and **never replaces the ordinary Attack or other standard combat options**. The Ryoko reference **already provides a standard-initiative procedure**, and the owner confirms it **works with optional Block Initiative without modifying the combo rules**. Block Initiative groups turns but preserves initiative information and individual combatants' choices; collaborators may still spend reactions in initiative order before the instigator rolls. Investigate only **actual independently evidenced conflicts**, such as unresolved global action-economy rules or mastery/critical interactions—not a presumed initiative-mode incompatibility. Shared effects do not imply shared procedures or a unified maneuver/combo engine.

**Combo procedure works with both initiative modes without a special variant:** Accessible copies of *Ryoko*'s combo rules describe a character initiating on their own turn after declaring an attack but before rolling, eligible allies contributing with **Reactions**, collaborators resolving in **initiative order**, and the initiating attacker rolling after those contributions. **The owner confirms the same procedure applies under optional Block Initiative without modification.** The existing Block Initiative engine retains original initiative placements and can separately record the free tactical action order within a block; that flexibility does not erase initiative order for resolving combo reactions. The assistant's earlier objections—first that standard-initiative timing was undefined and then that Block Initiative necessarily needed an adaptation—were incorrect. A publicly accessible [rules reproduction](https://anyflip.com/qwvsj/qbqi/basic) and earlier [beta playtest](https://anyflip.com/xbfbm/fefn/basic) both describe this structure. The reproduction's identity as final official text has **not been authenticated**; consult an authorized final copy before settling precise numerical rules and edition-specific edge cases.

**Block Initiative implementation evidence:** The independent [initiative engine](https://github.com/LegendMaster03/dorks-and-dice-block-initiative/blob/main/src/BlockInitiative.Core/Initiative/InitiativeEngine.cs) orders and retains combatant placements, and [TurnBlock](https://github.com/LegendMaster03/dorks-and-dice-block-initiative/blob/main/src/BlockInitiative.Core/Initiative/Blocks/TurnBlock.cs) separately tracks members' tactical order without altering initiative facts. The UI notes that players may act in any order within a block. **Initiative order for Ryoko collaborator reactions remains available regardless of allied action order.** This supports using the original combo procedure unchanged; it is not permission to alter general action/reaction rules.

**Verification boundary:** Publicly circulating *Ryoko* **playtest** texts describe an initiating attacker and other participants contributing through reactions with melee, ranged, or spell-oriented combo choices. Those texts also describe an enlarged critical-hit threat range based on collaborators and other option-specific effects. **Do not treat the playtest specifics as the final published rules** without checking an authorized definitive copy. Publisher marketing verifies the existence and intent of party combos, but not every numerical cost/limit/interaction. Keep provenance and licensing separate; document mechanics in independently authored language, not copied proprietary content.

**Questions before considering adoption or native adaptation:**
1. Is a combo a built-in option for any cooperating party, a trained/unlocked capability, an imported third-party option, or campaign-controlled optional content?
2. **Keep the source's existing timing and costs:** Accessible Ryoko rules describe an instigator spending an attack on their turn and eligible collaborators responding with reactions. The same sequence functions in either initiative mode **without combo-specific modifications**. Verify any additional costs, prerequisites or restrictions against a definitive published source; generic reaction-refresh rules remain part of the wider native action-economy specification, **not a blocker or a proposed modification to Ryoko combos**.
3. Which cooperative roles are simple enough to support **all-basic** groups without obligating each player to learn the advanced combo list? Can **all-advanced** players compose multiple detailed contributions, including from different characters, without invalidating those resource limits?
4. The Ryoko reference handles contributors in **initiative order** before the instigator's attack roll resolves; **use that same preserved initiative ordering within a Block Initiative block**, even if allies choose a different order for taking their own turns. No new combo-specific ordering or timing rule is needed. Opportunity attacks, any special additional participant costs, and multiple-attack feature interactions can be checked as separate general compatibility cases.
5. If a combo expands critical threat range, how does it combine with other crit-range effects under the **already established Dorks & Dice critical-stacking rule**? Any non-20 can contribute no more than a double critical, while only natural `20 → 20 → 20` produces triple. Do not silently import source critical-damage calculations or assume the published Ryoko combo rules follow Dorks & Dice adjudication.
6. Do combo maneuvers and weapon mastery effects stack on one initiating attack? Can combining them cause unintended action duplication or repeated triggers?
7. What are the intended limits on frequency, participants, proficiency, prior familiarity, and other prerequisites, and which belong to the original source versus a native adaptation?

**Scope:** This is a design/reference candidate, **not authorization to integrate third-party mechanics**, copy protected rule text, or implement new code. Source confirmation and explicit owner review are required before promoting specific combo rules into native defaults.

### Examples / requirements to test

- **Different kinds of enjoyment at any table:** All-basic, all-advanced, and mixed groups each play a coherent Dorks & Dice encounter using the same mechanics, but choose different amounts of detail and complexity in their actions and coordination. Neither simple nor advanced play requires the presence of players who prefer the opposite style. This is the primary acceptance scenario, not equal optimization or identical combat efficacy.
1. **Table-composition and initiative matrix:** Test (a) basic characters collaborating through straightforward actions, (b) advanced characters using intricate eligible abilities, and (c) both in one encounter; **run each case under standard initiative and Block Initiative**. Ordinary attacks must work without combo attacks or Weapon Mastery. All effects target the same HP, defense and condition rules.
2. **Prone and displacement:** A simple character sees that an opponent has been pushed or knocked Prone and knows the effect. A more invested player may care whether it came from Topple, Trip, Shove, a class feature or spell. The effect's shared resolution is consistent without equating all the procedures that caused it.
3. **Complex build, simple turn:** A player may own numerous proficiencies, features or derived statistics without having to choose a multi-step maneuver every round. The normal action/movement path must remain available, although choosing it may forgo the tactical or numerical advantage of using more involved features.
4. **No invented extra turn:** A 3.x full attack, 2024 Nick or Cleave, 5.x Extra Attack, or other tactical option must not silently grant extra action resources **beyond what their actual features authorize** during conversion. An explicitly granted extra attack or valuable complex interaction is allowed and need not have an equally effective simple counterpart.
5. **Cross-player defense:** A complicated attacker should not force a simpler defender to switch to an alternative edition's attack/defense procedure. The defender receives a normal resolution request and understandable consequence within Dorks & Dice.

### Decisions still needed to realize this principle

The **goal** is established for **all-basic, all-advanced and mixed tables**, including players changing their level of involvement between activities. The **exact mechanical layering is not yet approved**: what everybody can attempt by default; which maneuvers and weapon mastery effects require special character grants; how optional complexity delivers its additional capability without making it mandatory for everyone; which source restrictions prevent unintended extra benefits during conversion; and how much intermediate detail a GM/Tool must expose. **Do not require simpler and more detailed procedures to be equally effective**, or redesign complex mechanics solely to erase their advantage. Assess understandability, option accessibility, source fidelity and deliberate balance consequences, rather than choosing a source edition wholesale or imposing numerical parity.

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
- Use the **table-selected Dorks & Dice initiative procedure (standard or Block Initiative)** and shared native movement, critical, condition and encounter rules. A guest does not need to memorize the old 3.x profiles used by other players.
- Let the GM apply a **small set of published conversion/adjudication guidelines** for missing or incompatible information. For instance, a 5e sheet typically does not separately expose Touch or Flat-Footed AC: do not silently pretend its ordinary AC supplies those values. The GM may resolve a missing value explicitly for that one-shot.
- Leave advanced Dorks & Dice-only choices **optional for the visitor**. A 5e sheet should not imply unearned weapon mastery, prestige advancement, special maneuvers, or other capabilities the visitor's actual features do not grant.
- Preserve the distinction between **2014 5e** and **2024 5e**, which may differ in class features, spells, weapon mastery and other behavior, without making edition identification a large mandatory onboarding process.
- Accept that **case-by-case exceptions and balance disparities** may remain. A playable one-shot with limited GM rulings is the stretch goal; perfect character fidelity, automatic rebuild, lossless migration, and zero DM intervention are not.

### Future evaluation cases — intentionally not release gates

1. A guest presents an existing character sheet and makes ordinary attacks, checks and saves without rebuilding the character.
2. Their 5e actions, movement, spell resources and familiar features operate under the selected **standard or Block Initiative** option alongside a more complex native character's maneuvers.
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
| Initiative | **Standard initiative and Block Initiative are both supported options.** Block Initiative is an existing Dorks & Dice tool/system, **not the required native initiative mode**. The selected initiative option determines ordering; ordinary combat, Weapon Mastery and optional combos must function under either. Do not silently assume Block Initiative for native combat. | Owner correction (2026-10-09); detailed selection, timing and refresh contracts still need review |
| Action economy | **Merge the 3.x/PF1e and 5.x action economies into one native Dorks & Dice system**, recognizing their common functions while making conversion differences explicit. Do not require players to select an edition-wide action economy. **Use 5.x-style flexible movement as the native baseline**, while retaining rule-specific full-turn/full-round restrictions when expressly required. | Owner-confirmed unified system and movement baseline; other budget/timing semantics remain provisional |
| Related but distinct combat options | **Maneuvers** resolve declared combat actions; **Weapon Mastery** gives eligible characters additional ways to interact with ordinary attacks; **combo attacks** form a parallel, optional cooperative system without replacing standard combat. Their effects may use the same conditions, displacement and damage rules. Both initiative options must work. | Owner clarification; **no combined Combat Techniques subsystem is approved** |
| Combat defenses | Preserve the existing 3.5-derived **ordinary, Touch and Flat-Footed AC** distinctions already reconciled in Rules Core. Do not silently substitute 5.x advantage/disadvantage or saving throws for these defenses. | Reaffirmed existing design |
| Preserving 3.x defensive roles | **Prefer retaining Touch AC and Flat-Footed AC in native procedures where their 3.x role remains meaningful**, even if other parts of the procedure are rewritten to be simpler or edition-neutral. **Grapple initiation is the owner's concrete Touch AC example**: contact and maintaining a hold are different mechanical questions. Preserve source-granted bypasses and distinct auto-grapple effects. | Owner preference/direction; exact native grapple procedure, stages, exception policy and outcomes remain open |
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

- An encounter using **Block Initiative** follows that option's block ordering regardless of the origin of participants or features. An encounter using **standard initiative** follows standard individual turn ordering. Neither option changes which ordinary attacks, mastery benefits or cooperative combos exist.
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

### Interaction with optional initiative procedures

**Standard initiative and Block Initiative are alternative turn-order procedures**; neither is a hidden prerequisite for normal attacks, maneuvers, Weapon Mastery or optional combos. The table's selected procedure governs **whose turn or block is active**. The unified action model governs **what each actor can do** without imposing an initiative choice. Define start/end-of-turn durations, individual actions, reaction refresh, opportunity attacks, interrupts, and cooperative contributions under **both** turn-order options. Never assume one pooled party-wide action budget from Block Initiative.

### Cases to verify before adopting final action-budget numbers

1. Move before/after an Action; movement between attacks granted by a 5e class feature.
2. 3.x full attack with iterative BAB attacks and its 5-foot-step restriction; no added full-turn resource beyond the consumed action/movement.
3. 3.x move-equivalent activity (stand, draw or manipulate equipment, depending on source) when the character could otherwise move.
4. Swift/immediate action interaction and 5.x Bonus Action/Reaction, including when a character has both 3.x and 5.x features.
5. Opportunity attacks under 3.x threatened-square triggers and 5.x leaving-reach triggers; this is a **separate still-open native opportunity rule**, even though the action-budget concept is unified.
6. A healing potion's previously approved Bonus Action (rolled healing) versus Action (maximum healing) policy using the unified budget.
7. **Both initiative modes:** standard individual ordering and optional Block Initiative with multiple allies in one block, including reactions, refresh, interruptions and durations.

### Specific remaining owner decisions

- **Move-equivalent conversion:** With independent, splittable 5.x-style movement **now approved**, how do imported nonmovement 3.x **move actions** consume Action, movement allowance, or another resource? The baseline itself is not open.
- **Full-turn operations:** **Explicit rule-specific full-turn restrictions are preserved.** Which activities impose them, precisely what movement remains permissible, and whether a 3.x five-foot step is permitted only for specified features or generally are still to be defined.
- **Quick/reaction relationship:** Are Swift and Bonus combined into one slot, and do Immediate Actions use an ordinary Reaction, an upcoming Quick Action, or both? How many opportunity attacks can be made?
- **Action substitution:** Can a standard Action be converted to additional movement or to a move-equivalent task, and can Action be used to perform an eligible Quick Action?
- **Eligibility and refresh:** What is the default availability of Quick Action, Free Activities and Reaction, and how do they replenish under **standard and Block Initiative**?

These questions refine the unified action economy; **they do not reopen the owner's decisions to unify action economy, use 5.x-style splittable movement by default, or retain explicit full-turn restrictions where a rule requires them**.

## Feat-granted advanced expressions of basic combat mechanics — proposed design pattern

**Owner-endorsed direction (2026-10-18):** Design **shared mechanics that can be engaged with at varying levels of complexity**. This is the intended pattern for advanced combat choices: an ordinary, complete action remains available under the same resolution and resource rules, while eligible feats, class features or other legitimate grants can offer **richer, potentially substantially stronger ways of using that same mechanic**. A participant can choose the ordinary action even when they possess the advanced option. It is **not** a Basic versus Advanced combat mode, not automatic power parity, and not a directive to merge fundamentally different mechanics (such as Weapon Mastery and cooperative combo attacks) into one procedure.

**Specific application to reactions and opportunity attacks:** An ordinary opportunity attack and an advanced reactive maneuver still consume **the same existing Reaction**. A specialized feat may broaden *which actions are permitted when an appropriate trigger occurs*, or *which triggers are eligible*. It does not create extra Reactions or make the specialized choice mandatory. The feat's additional option needs its own eligibility, cost, trigger and consequences, not separate combat physics.

**Review criterion:** Differentiate (a) additional character capability that may justify feat/feature access, (b) inherently shared rules that everyone must use (conditions, reaction budget, ordinary provocation rules), and (c) complex explanation or bookkeeping that could be made easier without a mechanical feat. Do not assign feat costs merely for using a longer calculation. **An advanced option that is always superior at no additional opportunity or acquisition cost can make the ordinary option an illusory choice**; evaluate actual prerequisites, limits, costs and situation-specific tradeoffs without imposing artificial numerical equality.

**Approval boundary:** The owner endorses this **design principle**, not the illustrative reactive feats, a final opportunity-attack trigger list, automatic free feat access, or a new feat progression scheme. A GM's discretionary award of an advanced feat remains a character/campaign exception, not the ordinary acquisition rule.

**Owner preference (2026-10-18):** When a more involved version of an ordinary combat mechanic grants a substantial new capability or tactical advantage, **consider granting that extension through an eligible feat or class feature**. This preserves the accessible ordinary mechanic while allowing interested players to invest in a deeper version. It is a **design pattern to evaluate**, not a rule that every complex action must be feat-locked or that the examples below are already approved.

**Specific owner clarification:** The intended Reaction/AoO approach is **more permitted uses of an existing Reaction**, **not more Reactions per turn or round**. Do not translate this preference into extra reaction slots, unrestricted opportunity-attack counts, or a second reaction budget. Any actual ability that increases available reactions would need its own separate, explicit approval.

**Examples for scrutiny (not new feats):**
- A character ordinarily makes an opportunity attack when the established shared trigger occurs. A qualifying feat might allow the character to use **the same Reaction** for a Trip/Disarm maneuver **instead** of an ordinary weapon attack.
- A feat might authorize **additional qualifying triggers** for a particular reactive action, still paid for with the character's normal Reaction. More triggers provide more choices, **not** more uses.
- A defensive specialist might gain a new eligible Reaction to intervene for an ally, instead of gaining another Reaction.
- A feat or class feature may improve an ordinary Shove/Attack with a specialized, stronger procedure while ordinary Shove/Attack remains available and fully playable.

**Limitations / design objections to check before adopting each conversion:**
1. **A complexity label alone is not a feat prerequisite.** Grant features because the character gains a meaningful capability, not to put common combat actions behind a feat tax.
2. **Default rules stay shared.** A general provocation trigger, condition, or movement rule should not silently differ based solely on which player wants more intricate gameplay. A granted feature can expand a character's options or triggers without making two incompatible universal combat rules.
3. **Costs remain meaningful.** Reactive maneuvers may replace a basic reaction attack, or may add a permitted effect only when a particular feature explicitly grants it; do not assume both damage **and** maneuver effect are free. Multiple qualifying triggers do not restore an already-spent Reaction.
4. **Access is not feat-exclusive.** Existing class features, source feats or other legitimate grants can supply an advanced ability. Preserve source-character conversion and provenance rather than stripping an intrinsic 3.x class capability to sell it back as a new feat.
5. **Combat feats are distinct from free flavor feats.** The owner's rule granting flavor feats freely must not be used to grant substantive reactive combat power for free.
6. **Table depth remains opt-in.** A character who owns an advanced feat may still choose the straightforward action; all-basic, all-advanced and mixed groups use the same reaction/action budget. Equal power is not a design requirement.

**DM-discretionary feat grants versus native feat rules (owner clarification, 2026-10-18):** The **native default** for a mechanically significant advanced-combat feat is its ordinary eligibility and acquisition cost; **do not establish a precedent that veteran players or all-advanced tables receive such feats for free**. At the table, the GM **may choose** to award a particular advanced feat without its usual acquisition cost, for example to an experienced player or as a group-specific campaign allowance. This is a **campaign/individual GM exception**, not a built-in veteran entitlement, universal feat allowance, automatically applied expertise bonus, or new rule of feat acquisition. It does not change the number of Reactions or the costs of using the granted feature; it only changes whether the character has the feature. Preserve the separately established **free flavor feat** policy; do not automatically treat combat feats as flavor feats.

**Practical design safeguard:** Record any discretionary combat-feat award as an explicit, authorized character/campaign grant with provenance (GM decision, not automatic character-generation eligibility), so ordinary rules remain predictable and an advanced-table exception does not silently leak into a basic or mixed table. The GM may award exceptions to individuals, but should consider player expectations and access fairness when offering different starting capabilities based on experience. **That social consideration is guidance, not a prohibition on individual grants or a parity requirement.**

**Open decisions:** The basic opportunity-attack triggers have not been chosen; neither have the exact reaction-refresh budget and Swift/Immediate conversion. Those determine which advanced reactive feat benefits would be genuinely additional. Assess each candidate against the actual default and its opportunity cost before specifying feat requirements, numbers or outcomes.

## Combat maneuvers, Weapon Mastery and combo attacks — distinct roles

**Owner clarification (2026-10-09):** The suggestion to combine all of these mechanics into a single *Combat Techniques* system was deliberately presented as a test of critical design review. **It is not a design requirement, an approved system, or even a demonstrated improvement.** Do not build a new mandatory unified combat mechanic from conceptual similarities alone.

- **Normal combat:** Standard attacks, actions, movement and ordinary combat resolution remain independently playable. No combo attack or mastery selection is required to fight.
- **Combat maneuvers:** Actions or attack replacements with their own requirements, defenses and consequences. Which native maneuvers are generally available and how they resolve remains open. Their existence does not require Weapon Mastery or combos.
- **Weapon Mastery:** Additional eligible weapon/character features layered over the ordinary combat rules. Mastery can modify the result or sequencing of an attack but is neither the root attack mechanic nor an alternative combat mode. Actual feature grants and costs control access; do not award mastery to everyone by default.
- **Combo attacks:** A **parallel, optional cooperative attack system**. Players may initiate and contribute when requirements are met; ordinary combat continues to exist unchanged for participants not using combos. **Ryoko's existing procedure works under standard initiative and optional Block Initiative without a different combo procedure**; willing eligible allies spend reactions and resolve them in initiative order. Neither initiative mode is a prerequisite for the combo system.
- **Initiative:** **Standard initiative and Block Initiative are options** for organizing normal combat turns, not separate combat rulesets and not intrinsic elements of mastery or combos.
- **Harvesting/crafting:** An example of optional complex cooperation and helper roles, not a combat procedure or numerical template for combos.

**Share effects, not rules merely because they resemble one another.** Trip, Topple and a cooperative knockdown may all produce Prone; that warrants consistent Prone consequences and target-state tracking, not necessarily a unified check, feat requirement, attack cost, or participant procedure. Ordinary attacks and mastered attacks use the same baseline hit/damage state, but mastering a weapon is still an additional feature.

**Design review challenges to address rather than paper over:**
1. Does a proposed combination remove any player option, confuse an existing trigger, or make basic combat dependent on advanced procedure knowledge? Reject it unless the benefits clearly justify the loss.
2. **Do not invent an initiative incompatibility:** Ryoko's combo procedure works **unchanged with standard initiative or optional Block Initiative**: the instigator declares an attack, collaborators spend reactions in initiative order, and the roll resolves. Both procedures preserve the needed initiative information. Only reopen this if a concrete source rule or generic action-economy choice produces an actual contradiction.
3. How does a combo avoid extra attacks, duplicated reactions and repeated mastery triggers that were never earned? How do duration and critical effects behave across multiple participants?
4. Can all-basic, all-advanced, and mixed parties use normal combat productively without activating the combo layer? Can those who choose combos coordinate without controlling other characters?
5. Does a proposed shared component reflect a *genuinely identical consequence*, or are we erasing the distinct costs, eligibility, target defenses or outcome probabilities of different rules?

**Approval state:** The owner has corrected these relationships, **not approved a new mechanical formula for maneuvers, a particular combo cost/timing design, or mastery-combo stacking policy**. Preserve the normal combat foundation and separately evaluate each addition, its dependencies and its interactions.

## Weapon Mastery and combat-maneuver integration — proposal for review

**Status: preserve distinct mechanics.** Weapon Mastery is an additional way for eligible characters to interact with normal combat, while maneuvers and optional cooperative combo attacks retain their separate roles. They may share consequences such as Prone or forced movement, but do not assume a merged player-facing procedure. **Evaluate all proposed integration against the established individual-complexity principle above:** a simple player must not be required to adopt weapon mastery or advanced maneuver bookkeeping to participate normally, while an interested player can engage with the full tactical options allowed by their character, **including legitimate advantages that require greater complexity**. Do not replace mastery properties or advanced maneuver resolution with equally powerful simplified versions solely to enforce parity.

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
1. Whether **Quick Actions** unify Bonus/Swift actions into one ordinary turn allowance, and how Immediate actions consume or borrow the same allowance versus Reactions. **The selected standard or Block Initiative option** determines individual turn/block ordering; refresh and interruptions require explicit rules for both.
2. How nonmovement 3.x **move-equivalent actions** are charged under approved flexible movement; what full-round restrictions permit; whether primary Action can substitute for movement/Quick Action.
3. How **BAB iterative attacks, 5.x Extra Attack and feature-created attacks** interact on a mixed character, including whether multiple sources add attacks, override each other or remain alternative attack schedules.

**Maneuver and mastery alignment (high priority):**
4. The owner's own **combat-maneuver** proposal: which types of maneuvers exist, who can attempt them, which attacks/actions they replace, what resists them and what effects occur.
5. Should **2024 mastery eligibility and weapon-choice counts** be retained as source-specific class/feat grants, expanded into a universal native weapon-training framework, or both? Weapon proficiency **is not automatically mastery**. Existing class and feat selections should remain meaningful during conversion.
6. When a **weapon hit** both triggers a mastery (e.g., Push or Topple) and satisfies another ability or maneuver requirement, can multiple effects occur on one attack? Does choosing an attack-replacing maneuver trigger the weapon's normal hit properties? Default stacking/priority must be explicit.
7. Preserve special activation: **Push** on hit without a separate save, **Topple** on hit followed by a save, **Nick** as relocating—not duplicating—a Light-property attack, **Graze** on miss, and **Cleave** with a per-turn cap. Decide which source-specific behavior becomes Dorks & Dice-native default rather than assuming all use one contest.

**Secondary decisions (after basic native maneuver procedure):**
8. How timed Mastery effects with **until start/end of next turn** operate under **standard and Block Initiative**, and how limited mastery uses reset under either.
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

### Status effects and defensive AC profiles — restoration candidate

**Owner suggestion (2026-10-18):** Restore much of the gameplay significance of **Flat-Footed AC and Touch AC** through interactions with existing **status effects and the actions that cause them**, rather than confining alternative AC values to inert imported statistics. This continues the established policy of retaining meaningful 3.x defensive categories in one Dorks & Dice combat ruleset. **The owner has not yet approved a complete condition-by-condition mapping or automatic conversion of all 5e conditions.**

**Separate the two axes:**
- **Target's defensive state:** Some statuses deny a target an effective Dexterity/dodge response, make it helpless, or impose other position-dependent AC penalties. This can engage the **Flat-Footed AC calculation** or a more precise **denied Dexterity/dodge defense modifier**. **Denying Dexterity does not necessarily mean imposing the entire Flat-Footed status**, including its unrelated consequences. Defense calculation must respect exceptional abilities (e.g., Uncanny Dodge where applicable), conditional opponents, and specific remaining penalties.
- **Attack/effect's defense target:** A touch attack targets **Touch AC** because mere contact is sufficient; it ignores armor/shield/natural armor, not Dexterity/dodge by default. Examples to preserve or evaluate include **3.x Grapple contact**, 3.x Trip contact, and qualifying touch-delivered spells or powers. **Being Blinded, Grappled, Restrained or Stunned does not inherently make every incoming weapon attack a Touch attack.**
- **Combined cases:** If an attack requires touch and the target is also denied a Dexterity/dodge defense, evaluate both properties together. Do not choose exactly one stored AC label and discard the other; compute the effective defense from the active components and source-defined exceptions.

**Source-grounded 3.5e examples for evaluation (not approved native status conversions):**

| Target state | 3.5e defensive effect | Native integration question |
| --- | --- | --- |
| **Flat-Footed** | Lose applicable Dexterity and dodge AC bonuses; other flat-footed consequences also exist | Keep the full condition distinct from merely denying Dexterity |
| **Blinded** | Lose Dexterity bonus to AC and take an additional **−2 AC** | **Resolved attack side:** Attacks made while Blinded use Disadvantage, **not a separate 50% miss check**. **Defense side:** Resolve AC/defense consequences under the established target-impairment rule; avoid duplicated Advantage for the same penalty, with exact native penalty mapping still subject to condition-level review. |
| **Stunned** | Lose Dexterity bonus to AC and take an additional **−2 AC** | Does the native condition adopt these AC effects and which exceptions apply? |
| **Grappling** | Lose Dexterity AC bonus **against non-grappling attackers** | Retain attacker-relative conditional defense, not a global Grappled = Flat-Footed shortcut |
| **Helpless (e.g., Paralyzed)** | Treat defender Dexterity as **0** (modifier −5), with additional helpless and melee/ranged distinctions | Preserve distinction from ordinary denied-Dexterity and avoid reducing helpless to Flat-Footed |
| **Prone** | **−4 AC versus melee, +4 versus ranged** | Evaluate source-specific attack-position effects instead of marking every Prone target Flat-Footed |

The [3.5e combat-modifier table](https://www.d20srd.org/srd/combat/combatModifiers.htm), [condition summary](https://www.d20srd.org/srd/conditionSummary.htm), and [AC/touch definitions](https://www.d20srd.org/srd/combat/combatStatistics.htm#armorClass) distinguish these states. A 5e condition may encode some similar danger through **Advantage on attacks** rather than 3.x's AC changes; **do not indiscriminately stack both effects** when migrating that condition without an explicit native rule and impact test.

**Design test:** For any condition, distinguish (1) the status's own effects, (2) any active change to the defender's AC components, (3) whether a **specific incoming attack** targets normal/Touch/other defense, and (4) feature-granted exceptions. Keep the native rules playable without the player choosing an edition's combat physics. Preserve attack-side versus defender-side responsibility so Touch AC remains a meaningful **targeting rule** as well as interacting with status-modified defenses.

**Current status:** **Conceptual direction for case-by-case review**. No new status-specific native penalties, advantage conversion, blanket Flat-Footed assignment, or touch-targeting rule is approved by this note alone.

### Blinded: keep attack Disadvantage instead of a separate concealment roll

**Owner decision (2026-10-18):** The **attacker/defender effect attribution** rule is already established; there is no need to reopen the choice of underlying representation as a new condition framework. When a creature **attacks while Blinded**, use the normal **Disadvantage** d20 mode rather than the older 3.x **50% miss chance** associated with the blinded creature's inability to see its opponent. This is a deliberate Dorks & Dice simplification, **not a claim that Disadvantage exactly equals a 50% miss chance**.

**Probability distinction (not an obstacle to the approved choice):** Given a normal hit probability `p`, Disadvantage results in `p²` before other complexities such as criticals, while a separate 50% miss chance after an otherwise successful attack gives `0.5p`. At `p = 0.60`, these are **36%** and **30%**, respectively; the two treatments may differ significantly at other hit rates. The owner explicitly prefers simpler play here.

**Blinded defender:** Evaluate the defender's condition-based AC/defensive restrictions using the existing subject-of-effect and retained 3.x defense principles. Do **not** automatically grant each attacker 5e-style Advantage **in addition to** a semantically equivalent target-side AC penalty. Preserve the distinction between actually becoming Flat-Footed and merely losing relevant Dexterity/dodge contributions; finalize individual numeric condition consequences through ordinary native status review rather than creating a new resolution architecture.

**Scope:** This decision replaces the 3.x blindness-related 50% miss roll **for an attack made while Blinded**. It does **not** delete or globally replace **other distinct concealment, miss-chance or source-specific avoidance effects**. Apply explicit rule grants and exception handling as normal; different independently granted effects should be evaluated for actual scope and legitimate stacking. See [3.5 condition rules](https://www.d20srd.org/srd/conditionSummary.htm#blinded) and [3.5 concealment mechanics](https://www.d20srd.org/srd/combat/combatModifiers.htm#concealment).

### Attacker versus defender effect ownership — representation principle

**Owner direction (2026-10-18):** When reconciling otherwise similar 3.x and 5.x attack/defense effects, **first distinguish which character the ability or condition actually affects**. Prefer to represent an **attacker-facing improvement** as an appropriate attack-side roll benefit (such as Advantage), while a **defender-facing impairment** may instead be represented through a native status, lost defensive component, Flat-Footed applicability, or AC adjustment. This explains how to preserve the useful distinction between improving someone's attack and undermining another creature's defense **within the same native combat rules**. Apply it to each effect's actual scope, rather than treating Advantage and reduced AC as interchangeable formulas.

| Effect's primary mechanical subject | Preferred design direction | Scope guardrail |
| --- | --- | --- |
| **Attacker gains accuracy or an offensive edge** | Attack-side Advantage, another applicable roll mode or attack modifier | A one-character or one-target attack buff is **not** a global reduction in the opponent's AC |
| **Defender loses defensive capability** | Native condition/status, loss of applicable Dexterity/dodge defense, targeted AC reduction or other specifically justified status consequence | A creature's debuff may be attacker-relative, duration-limited or limited to particular attack types; **do not universally apply Flat-Footed AC** without checking the condition |
| **Defender gains protection** | AC/defensive bonus or another applicable defensive rule | Avoid changing every attacker's roll mode when the benefit genuinely belongs to the defender |
| **Attacker suffers an offensive impairment** | Disadvantage, another roll mode or an attack penalty where warranted | Do not automatically increase the target's AC against *all* other attackers |

**Cause and scope are more important than syntax:** Some 5e effects are **worded** as “attacks against this creature gain Advantage” but are **caused by a condition or exposure on that creature**. Conversely, a feature can give one attacker Advantage **against one particular target** without reducing that target's global AC. Record the source, recipient, affected attack(er)s, duration, trigger and actual mechanical outcome before choosing a native representation. Effects that are already functionally identical may map directly, without rewriting them.

**Do not double count equivalent consequences:** Translating a single 5e defender-impairing condition into native defensive penalties should not automatically preserve its old attack-side Advantage **as an extra independent bonus**. Distinct independently acquired attack buffs and target debuffs **may** coexist under ordinary native stacking and roll-selection rules; they are not prohibited merely because both improve hit probability. Preserve source-authorized exceptions and actual typed modifiers where they matter, rather than replacing every bonus with Advantage.

**Touch AC is a different axis:** Whether a particular incoming attack requires **Touch AC** is normally a property of that attack or effect (e.g., a qualifying touch spell or grapple contact). The defender's condition can further alter the effective defense, but a defender debuff does **not** automatically convert an ordinary hit into a Touch attack. Combined touch and lost-Dexterity defense must still calculate correctly.

**Status:** The **attacker-versus-defender attribution principle** is accepted as a design direction. No blanket conversion table, specific condition penalty, universal Advantage substitution, or finalized numeric stacking rule is approved by this principle alone. Decide effects case by case and test attack-side buffs, defender debuffs, and their genuinely independent combination with ordinary/Touch/Flat-Footed AC.

### Emphasis within attacker/defender effect attribution

**Owner decision (2026-10-18):** **Emphasis** is a distinct d20 roll-selection mode alongside Normal, Advantage and Disadvantage. It favors an extreme result on either side of the d20's center; it is neither inherently a buff nor a debuff, and is not an AC adjustment or condition.

| Mode | Native roll procedure |
| --- | --- |
| Normal | Roll one d20 and use its result |
| Advantage | Roll two d20s and use the higher |
| Disadvantage | Roll two d20s and use the lower |
| **Emphasis** | Roll **two d20s**. **If they add up to 21, they cancel**: apply a specified cancellation outcome, or **reroll both dice** if none is specified. Otherwise, use the die with the more extreme result (**closer to 1 or 20**). If both dice match, use that number. |

**Canonical player-facing Emphasis rule (owner-approved wording, 2026-10-18; reproduce verbatim in rules text and player-facing tools):**

> Emphasis: Roll two d20s. If the results add up to 21, they cancel each other out. Apply any special cancellation effect defined by the applicable rule. If no such effect exists, reroll both dice.
>
> Otherwise, use whichever result is more extreme (closer to 1 or 20). If both dice show the same number, use that number.

**Examples:** [7,14] cancels; [7,13] selects 7; [7,7] selects 7. The exact player-facing rule above deliberately avoids midpoint notation.

**Implementation explanation (not player-facing text):** On a d20, opposing rolls equally far from the midpoint of **10.5** necessarily sum to **21**. Consequently the sum-to-21 cancellation check is exactly equivalent to cancellation for equal-and-opposite absolute values, without an extra calculation. Between noncanceling rolls, compare their distances to the endpoints 1 and 20 to choose the more extreme roll. Do not alter the specified tie rule for identical dice.

**Cancellation resolution precedence (owner-approved):**
1. Identify a cancellation **before selecting either d20** or adjudicating ordinary success, failure or critical effects from that pair.
2. If an **applicable, explicitly defined cancellation-specific outcome** exists in the effective rules, **apply that outcome instead of rerolling**. Do not assume a predefined universal cancellation result.
3. **Otherwise reroll both d20s together** and reapply the entire Emphasis selection/cancellation procedure to the new pair. Repeat for as many consecutive cancellations as occur. Do not treat either canceled die as a retained result.
4. The player's manual dice and any consuming tool must be able to represent the canceled pair, its applicable special result or the need to roll two new dice. The headless rules engine should request the next pair as needed rather than silently selecting one canceled result.

**Acceptance examples:**

| Dice rolled | Effective comparison | Native result without a special cancellation outcome |
| --- | --- | --- |
| [1,20] or [20,1] | Dice add up to 21 | **Cancel; reroll both** |
| [7,14] | Dice add up to 21 | **Cancel; reroll both** |
| [10,11] | Dice add up to 21 | **Cancel; reroll both** |
| [7,13] | 7 is closer to 1 than 13 is to 20 | **Select 7** |
| [4,16] | 4 is closer to 1 than 16 is to 20 | **Select 4** |
| [7,7] | Identical results, not a total of 21 | **Select 7** |
| [1,20] with an applicable special cancellation rule | Dice add up to 21 | **Apply the explicit cancellation outcome** |
| [1,20] → [7,14] → [4,13] | First two pairs cancel; third selects 4 | **Reroll twice, then select 4** |

**Compatibility note:** The deployed/legacy Rules Core selector currently uses `abs(d20-10)`, keeps the first die when equally scored and reports `selectionTied`. Those were **implementation behavior**, not the adopted symmetric Dorks & Dice Emphasis rule; the new midpoint and cancellation handling supersede them for a future rewrite. Do **not** modify the currently deployed selector simply by editing this specification. The [existing consumer contract](../character-mechanics-consumer.md#d20-roll-selection-modes) describes that **legacy behavior**, not this newly approved native rule.

**Effects and interactions:** Attributing an ability to the attacker or defender helps determine whether to modify that actor's roll or the target's status/AC, but does **not** mechanically prescribe Advantage, Disadvantage, or Emphasis for every buff/debuff. A native feature may apply Emphasis to an eligible attack, check or save for more extreme results without guaranteeing a higher or lower roll. Defending characters can likewise use Emphasis on applicable saving throws/checks when legitimately granted. An effect should not double-count a 5e defender condition's Advantage and its translated native AC penalty.

**Remaining decisions:** The interaction/precedence of **separate simultaneous roll-mode grants** (Advantage, Disadvantage, Emphasis), possible specialized cancellation outcomes, and source/campaign applicability remain to be designed case by case. **Critical escalation rolls are already straight, unmodified d20s** and must not receive Emphasis cancellation or any other roll-mode rule. The ordinary cancellation/reroll rule and symmetric selection formula **are settled**.

### Preserve the mechanical role of legacy AC categories — grapple contact example

**Owner direction (2026-10-18):** Use the existing **ordinary, Touch and Flat-Footed AC** categories purposefully within native Dorks & Dice mechanics, not only as dormant compatibility data. Where 3.x used a particular defense for a physically meaningful step, **prefer keeping that defense even when the rest of the mechanic is redesigned toward a simpler common rules procedure**. This is a case-by-case design principle, not a blanket rule that every maneuver uses Touch AC or that every import gains a new roll.

**Concrete preference: ordinary Grapple initiation should involve Touch AC where feasible.** D&D 3.5e distinguished a melee touch attack to **grab** from an opposed grapple check to **hold** the opponent. It is mechanically coherent that an armored creature can be touched more easily than it can be damaged, while strength, size, technique and other factors govern whether the grab actually becomes restraint. Compare the [3.5e Grapple steps](https://www.d20srd.org/srd/combat/specialAttacks.htm#grapple) and the [Touch AC defense definition](https://www.d20srd.org/srd/combat/combatStatistics.htm#armorClass).

**Important non-equivalence:** Neither 2014 5e Grapple (contested Athletics) nor 2024 5e Grapple (defender Strength/Dexterity save against a grapple DC) uses an AC attack roll as its ordinary initiating resolution. Adding an initial Touch AC check to a 5e-style follow-up **would have been a new hybrid procedure** with two chances to fail, not a lossless import. **The owner has instead selected the existing 3.x Touch AC contact plus opposed grapple-check hold procedure**, whose role and source constraints are described below. Touch AC alone does not measure a strong opponent's ability to resist or break a hold.

**3.x-style step illustration (the contact/hold sequence is now selected as the native Grapple baseline; action-economy and condition integrations remain open):**

1. **Grab/contact:** For an ordinary attempt requiring physical contact, make an authorized attack against the target's Touch AC (reach, size, a free grappling limb and attack cost must be defined).
2. **Control/hold:** If contact succeeds, resolve an **opposed 3.x-style grapple check**, reflecting combat training/BAB, Strength, and special grapple-size effects, to establish control.
3. **Grappled state:** Apply one coherent native condition, then use native rules for moving, escaping, maintaining, pinning or exploiting a grapple. Do not let a character's preferred complexity setting change the resulting state.
4. **Specific feature exceptions:** A monster's on-hit automatic grapple, a spell, magical restraint or another feature that grants a different trigger may have its own source-authorized initiation procedure. **Do not append a redundant Touch AC check to an effect that already has an explicitly sufficient hit/save condition** without a deliberate conversion ruling.

**Owner follow-up and source check (2026-10-18):** The owner finds **3e's underlying grapple design generally more coherent** and has selected it as the native Grapple baseline, while recognizing the appeal of 5e's Strength (Athletics) escape choice. Source comparison confirms this is **not a missing 3e capability**: both 3.0 and 3.5e permit breaking free by an opposed **grapple check**, whose modifier includes **BAB + Strength + special size**, or by a **Dexterity-based Escape Artist check** opposed by the grappler's grapple check. In 3.5e the grapple-check escape can **replace an attack**, whereas using Escape Artist requires a **standard action**. The 2014 5e procedure instead permits Strength (Athletics) or Dexterity (Acrobatics) contested against the holder's Athletics, while 2024 uses the escaped character's Athletics or Acrobatics against a defined grapple escape DC. See the [3.0 grapple SRD](https://a.osmarks.net/content/dandwiki_en_all_maxi_2021-03/A/3e_SRD:Grapple), [3.5 grapple SRD](https://www.d20srd.org/srd/combat/specialAttacks.htm#grapple), and [2014 5e grappling rules](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/combat#Grappling).

**Design implication (updated decision):** Retain **the classic 3.x opposed grapple check**, not Strength (Athletics), for establishing a hold and escaping by force. Retain the separate Dexterity-based **Escape Artist** check opposed by the holder's grapple check for skill-based escape. **Athletics is not the native grapple check** and does not automatically add proficiency to BAB + Strength + size. Review cross-edition character feature conversions and action costs separately. This settles the core check family, but not every 3.x positioning, provocation, condition or multicreature interaction.

**Owner's escape-method preference (2026-10-18):** Since both **3.x Escape Artist** and the relevant **5e Dexterity (Acrobatics)** escape application are Dexterity-based, **favor Escape Artist as the native named Dexterity escape skill**, rather than inventing a separate Dexterity escape skill or treating Acrobatics and Escape Artist as globally equivalent. A 5e character's Acrobatics capability **may map contextually into an Escape Artist grapple-escape attempt**; retain distinctions for unrelated Acrobatics (e.g., balancing or tumbling) and Escape Artist (e.g., bonds) uses. The owner is also open to a **Strength-based skill route**, but its addition is not required merely to support strong characters because **the 3.x opposed grapple check already supplies a Strength-based escape method**. Evaluate whether Strength (Athletics) proficiency/competency should grant an alternative check or map to grapple-training eligibility; **do not simply add Athletics and BAB/proficiency together** without source-backed stacking rules. Determine the action economy, the precise native grapple modifier and which check contests which defense later. **Naming Escape Artist as the preferred Dexterity skill is a direction, not yet a final escape algorithm.**

**Tradeoff to validate in play:** The **selected** 3.x two-stage procedure gives physical contact and resistance distinct roles, but has more rolls and a lower overall chance of success than a one-roll procedure with comparable per-roll odds. Test novice play speed, basic-character viability, success probabilities, and interactions with Improved Grapple and similar grants during acceptance review.

**Status update (2026-10-18):** The owner subsequently chose the **3.x Grapple procedure as the native baseline**, including **Touch AC contact followed by an opposed grapple check**, with **Escape Artist** (Dexterity) as the skill-based escape option rather than substituting Athletics or Acrobatics. See the adopted core and remaining integration questions below. The prior hybrid check/save candidates are no longer the preferred default. This does not invent a universal maneuver formula; apply legacy defensive-role preservation **case by case**, including distinct uses of Flat-Footed AC.

### Native Grapple baseline — 3.x procedure with Escape Artist

**Owner decision (2026-10-18):** Use **D&D 3.x Grapple as the native Dorks & Dice starting procedure**, rather than converting it wholesale to the 2014/2024 5e Athletics/Acrobatics or saving-throw methods. Preserve **Escape Artist** as an individually meaningful Dexterity-based skill and as the designated **skill-based escape from Grapple**. Do **not** replace Escape Artist with Acrobatics merely because both are Dexterity-based, and do **not** substitute Strength (Athletics) for the 3.x opposed grapple check. Athletics remains a separate skill/competency with its own functions.

**Established native procedure — source-grounded common core:**
1. **Initiate:** Attempt a qualifying melee touch attack **against Touch AC** to grab the target, with the native action/attack cost and provocation policy applied.
2. **Establish a hold:** On successful contact, resolve an **opposed grapple check**, reflecting combat ability, Strength, and special size modifiers (3.5e source: **BAB + Strength modifier + special grapple-size modifier**). Success initiates Grapple; contact alone is insufficient.
3. **Escape by force:** Use an opposed **grapple check** against the grappler. 3.5e permits this **in place of an attack**; preserve that as the source-based timing to map into the eventual unified attack economy.
4. **Escape by technique:** Use a **Dexterity-based Escape Artist check** opposed by the holder's grapple check; in 3.5e this **requires a standard action**. Escape Artist is subject to an **armor check penalty** in its 3.5e source profile, which illustrates that retaining Touch AC does not make armor entirely irrelevant to grappling.
5. **Other grapple actions:** Use the 3.x repertoire (e.g., maintain/move, damage, pin, break a pin, attack under restrictions, join an existing grapple) as the **reference for native capability**, while explicitly reconciling their costs, condition consequences, spacing, and source-authorized feature exceptions with the one Dorks & Dice combat system.

**Integration decisions that are *not* automatically settled by choosing 3.x:**
- **Opportunity attacks and feats:** 3.5e initiating grapple provokes an opportunity attack unless a specific exception applies (e.g., Improved Grapple); the native ordinary trigger and Reaction consumption rules remain open. Do not automatically add new Reactions.
- **Shared positioning:** 3.5e requires the initiating grappler to enter the target's occupied space to maintain the hold. Verify how this interacts with native occupied-space movement, larger creatures, and the already adopted flexible movement model.
- **Condition semantics and other costs:** Specify the exact shared Grappled/Pinned consequences, nonlethal damage, initial unarmed damage, constraints on casting and attacking, tie/size handling, multiple grapplers, and duration/escape costs before calling the full procedure complete. Avoid silent 5e-style condition substitution.
- **Cross-edition characters:** A character importing 5e Acrobatics proficiency or a 5e grappling-related feature may need a semantic conversion for its **grapple-escape capability**, **without globally equating Acrobatics and Escape Artist or creating an unrestricted duplicate bonus**. The mapping and effective grant remain to be decided.
- **3.0 versus 3.5 details:** The shared 3.x concept is approved as the baseline; use 3.5e as a concrete comparison reference, but identify genuine 3.0/3.5 differences before fixing native timing and side effects.

**Scope of approval:** The owner has selected **3.x Grapple with Touch AC, opposed grapple checks and Escape Artist escapes**. This decision supersedes the earlier suggestion to select a 5e-style saving throw/contest as the native hold procedure. It is **not** blanket approval of every 3.5e incidental movement/provocation rule, a wholesale return to the 3.5e action economy, or an authorization to change gameplay code before Gate 0 owner review. Sources: [3.5 Grapple](https://www.d20srd.org/srd/combat/specialAttacks.htm#grapple) and [Escape Artist (Dex; armor check penalty)](https://www.d20srd.org/srd/skills/escapeArtist.htm).

## Combat maneuvers — owner-ideas-first design worksheet

**Status:** The owner has selected the **3.x touch attack + opposed grapple check, with grapple-check or Escape Artist escape**, as the specific native **Grapple** baseline above. **Other maneuvers remain unapproved**, including whether they share a resolution formula or a default action list. Do not automatically translate those others into Pathfinder's CMB/CMD scheme, D&D 3.x attack-of-opportunity procedures, or D&D 5.x contested checks/saving throws.

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

**Unresolved maneuver examples** for comparison: shove/forced movement, trip/knock prone, disarm, overrun, sunder, and restraint. **Grapple is now the first native maneuver with a selected procedure family**, though complete action, state and source-conversion interactions are pending. The old Rules Core computes a `combat.grapple` **modifier**, but that does not by itself implement the newly selected Grapple procedure.

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
