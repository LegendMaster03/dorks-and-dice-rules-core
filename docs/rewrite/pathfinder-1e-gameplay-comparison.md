# Phase 0 — Pathfinder First Edition gameplay comparison

Status: **deferred comparative design reference**, 2026-10-09. Existing Dorks & Dice gameplay decisions already address most of these conversion areas, and the owner intends to describe the native Dorks & Dice system before relying on Pathfinder examples. **Combat maneuvers are one identifiable area needing a fuller native design**, but the owner already has ideas for that area. Pathfinder 1e is an optional later reference for improving any area, not an automatically adopted rules source or a replacement for the owner's design.

## Purpose and scope

The Dorks & Dice game is being developed from the cross-edition decisions already made while reconciling D&D 3e, 3.5e, 5e, and 5.5e. Pathfinder First Edition (PF1e) is useful because Paizo independently revised and published the D&D 3.5-derived rules framework. Paizo's official 2009 conversion guide covers conversion of characters, monsters, feats, spells, prestige classes, and magic items. Study the solutions and their consequences rather than importing every Pathfinder choice.

**Audit classification for every comparison:**
- **Existing D&D decision**: an already reviewed Dorks & Dice gameplay policy supported by the existing implementation and documents.
- **PF1e comparison**: a Pathfinder rule or alternative we can analyze.
- **Open design question**: not approved and not to be implemented until reviewed.

## Comparison matrix

| Area | Recovered Dorks & Dice policy | PF1e evidence and alternate approach | Phase 0 conclusion |
| --- | --- | --- | --- |
| Competency identity | Universal learned competency independent of source-native name and numeric profile; ranks and proficiency are not numerically interchangeable. | Core PF1e retains ranks and class skills, with a +3 class skill bonus when invested. | Compare training models but do not convert PF ranks into 5.x proficiency bonuses or adopt the +3 rule by default. |
| Skill combinations | Hide + Move Silently → Stealth; Listen + Spot → Perception; Climb + Jump + Swim → Athletics; Balance + Tumble → Acrobatics; preserve independently ranked contributors and explicit mean-based parent calculation. | PF1e merges some 3.5 functions into a smaller ranked skill list; Unchained further proposes a 35-to-12 consolidated-skills variant. | **Different tradeoffs:** PF consolidation changes trainable skills, while our existing approach preserves contributors and derives umbrella values. Use PF as a counterexample/test corpus, not grounds to discard our reviewed policy. |
| Skill families | Craft, Perform, Profession are families of independent specialties; Knowledge (X) resolves to X. | PF1e retains Craft and Profession specialization; Unchained adds Background Skills, Grouped Skills, Skill Unlocks, and revised Craft/Profession. | Review whether any PF advancement ideas can be expressed as optional core competency progression without merging independent specialties. |
| Class advancement | Class and subclass are distinct concepts; subclass uses parent class level, with no independent subclass-level track. | PF1e classes have independently advancing class levels. | Preserve track identity and identify each feature's level dependency. |
| Prestige classes | Prestige classes are independent progressions with prerequisites and distinct level caps; unlike subclasses, they acquire their own levels. | PF1e prestige classes distinguish class level from total character level; some advance caster level in another class. | Strong regression cases for prerequisites, spellcasting progression and per-track level-band behavior. No automatic PF level schedule adoption. |
| Class customization | Dorks & Dice subclasses are class-bound, not independent progression tracks. | PF1e archetypes replace or modify named class features in the parent class, usually leaving unaffected progression intact. Multiple archetypes can coexist when they do not replace the same feature. | Study **feature replacement overlays** as a distinct relationship from subclass and prestige progression; do not relabel every archetype as a 5e subclass. |
| Multiclassing | Character level and class occurrence level are distinct; starting-class and multiclass qualification rules follow their effective rules. | PF1e combines character levels and base attack/save contributions from selected classes. Unchained variant multiclassing provides secondary class features at total-character-level thresholds, sometimes with special effective-class-level formulas. | Excellent examples of a feature with different **acquisition, scaling, prerequisite, and effects level bases**. |
| Combat mechanics | 3.x BAB, grapple, Fortitude/Reflex/Will, touch and flat-footed AC are separate capability-dependent mechanics; 5.x mechanics are not coerced into them. | PF1e uses BAB, three saves, touch/flat-footed defense, and standardized CMB/CMD maneuvers. | Review generalizable combat operation/target-defense representation, without assuming CMB/CMD must replace existing grapple/contested checks. |
| Epic, bands, and extra advancement tracks | Epic is a content tier; class continuations and epic prestige classes differ. Emerging Dorks & Dice bands cover 1–20, 21–40, etc. separately for total character level and individual tracks. | PF1e Mythic Adventures uses a separately earned 1–10 mythic tier track based on story trials, not ordinary 21+ class-level advancement. | Good stress test of independently tracked advancement dimensions. **PF mythic tiers are not Dorks & Dice's second 20-level band.** |
| Optional mechanical systems | An imported rule may be additive, replace a procedure, or conflict with a Dorks & Dice core procedure; conflicts require policy. | Unchained offers revised action economy, consolidated and grouped skills, variant multiclassing, automatic bonus progression, and alternate attack resolution. | Treat each as a controlled variant/compatibility fixture. Explore structural composition and conflict reporting rather than adopting it. |
| Skill-level advancement transitions | Mechanics may be introduced, continue, stop advancing, change behavior, or expire at track-specific thresholds. | PF1e Unchained skill unlocks confer new effects at rank thresholds 5, 10, 15, 20. | Supports generalized threshold-triggered mechanics independent of total character-level bands. |

## Design-first sequence (owner-directed)

1. **Recover existing Dorks & Dice mechanics decisions** from code, documentation, and the owner's prior adjudications. Label evidence separately from intended design; avoid treating unimplemented ideas as missing merely because the old service does not support them.
2. **Establish the native system's basics in the owner's own terms** before looking to other games for solutions. This includes existing domains that the owner wishes to refine and new ideas, especially the proposed combat-maneuver system. Record proposals without converting them immediately into PF1e/CMB/CMD or D&D-specific terminology.
3. **Review internal coherence and compatibility** across those native decisions. Separate settled policy from proposals and open questions; do not decide for the owner.
4. **Only then compare Pathfinder 1e** as a potentially valuable source of refinement in any area, including competencies, classes, combat, and advancement. Test possible changes against the native goals and keep rejected alternatives documented as options, not unresolved obligations.

This is an ordering of design work, not a freeze on existing rules or a restriction on consulting references after the core concept is clear.

## Reassessment: existing design versus new work

The owner confirms that existing Dorks & Dice decisions already address most conversion cases presented here. **This does not mean those designs are frozen or that Pathfinder can not later inspire improvements.** Recover the actual decisions first; invite the owner to describe the intended native system, including developments that were never implemented. Compare Pathfinder alternatives afterward, explicitly highlighting where they might improve, challenge, or extend an existing decision without automatically replacing it. Competencies, subclasses, prestige classes, multiclassing, epic/level-band progression, 3.x defenses, and other implemented areas are existing design foundations, not blank-slate questions.

### Confirmed existing capabilities relevant to the gap

Repository review found:

- `src/RulesCore.Infrastructure/Rules/CharacterProjection/CharacterThreeXCombatResolver.cs` computes `combat.grapple` from 3.x BAB, Strength modifier, size-specific grapple modifier, and other modifiers, and also projects 3.x Armor Class variants and saving throws.
- `src/RulesCore.Domain/Rules/CharacterMechanics.cs` declares `combat.grapple` as a numeric mechanic.
- `docs/character-mechanics-consumer.md` retains target defenses, roll modes, and applicable target states as separate concepts; advantage is not equivalent to Flat-Footed AC or a touch attack.
- The current repository search did **not** identify first-class CMB/CMD mechanic identities or a general maneuver resolver for trip, bull rush, disarm, sunder, overrun, and related actions. This is a **scoped evidence finding**, not proof that no relevant imported raw/source rules exist.

The distinction is important: **a grapple modifier is not a complete grapple action procedure**, and a collection of independently imported special attacks is not a unified Dorks & Dice combat-maneuver model.

## Native design to document — combat maneuvers

### What must be recovered before designing anything

Audit how the current Rules Core represents each of these source-native actions and whether it already has any of their target selection, contest, conditions, restrictions, and effect behavior:

- D&D 3.x: grapple, trip, bull rush, disarm, sunder, overrun, feint, and other special attacks that use different tests and action costs. Not all such actions should be forced into a single formula.
- Pathfinder 1e Core Rulebook: bull rush, disarm, grapple, overrun, sunder, and trip using **Combat Maneuver Bonus (CMB)** against **Combat Maneuver Defense (CMD)**. Later PF1e rules include additional maneuver types such as dirty trick, drag, reposition, and steal.
- 5.x: 2014 grapple/shove via opposed ability checks, compared with SRD 5.2.1 (2024) grapple/shove as Unarmed Strike options resisted by saving throws. These are distinct resolution procedures; a shared outcome label does not make the procedures mechanically identical.

### Design questions to elicit from the owner's proposal

1. **Native Dorks & Dice resolution:** Record the owner's intended procedures in the owner's own terminology before presenting alternatives from D&D 3.x, 5.x, or Pathfinder. What is common to different maneuvers, and what differs? No predetermined formula, contested roll, saving throw, or CMB/CMD analogue is assumed.
2. **Maneuver identity and outcome:** Which actions are separate first-class mechanics, which are alternate implementations of one outcome (for example pushing or knocking prone), and which are mechanically distinct?
3. **Inputs and defenses:** How do BAB, Strength/Dexterity, proficiency, size, other modifiers, special maneuver bonuses, defenses, target states, and applicable conditions contribute? Is CMD a native Dorks & Dice defensive statistic or merely one imported profile?
4. **Action and consequence semantics:** How do action cost, attacks of opportunity/reactions, reach, size restrictions, movement, prone, grappled, pinned, disarmed objects, damaged equipment, failure consequences, and repeated attempts work?
5. **Compatibility:** Can source-specific 3.x, PF1e, 2014 5e, and 2024 5e maneuver procedures coexist without silently changing a character or encounter's governing mechanics? How is a conflicting rule reviewed and published?
6. **Effects and advancement:** Can feats, class features, monster capabilities, and level/competency thresholds grant maneuvers or modify their checks/defenses without special code for each source family?

The new Dorks & Dice system should expose a **typed maneuver/procedure identity** and rule-declared eligibility, resolution method, and consequences. This is a design direction, not approval of PF1e's CMB/CMD formula.

### Proposed acceptance fixtures

- A 3.5e grapple modifier calculated from BAB, Strength, and source-appropriate size modifier remains unchanged when PF maneuver support is absent.
- PF1e CMB/CMD use their own correct source-specific modifiers; do not reuse the different 3.5e grapple size modifier as though it were Pathfinder CMB.
- A successful trip and a successful shove-to-prone may have a comparable outcome but different check/defense, size, and action-economy requirements.
- Disarm, sunder, and steal are distinct in their targets and effects; none becomes an alias of grapple.
- Importing a PF maneuver option does not activate it or override an effective Dorks & Dice combat procedure without the designated acceptance/adjudication rule.
- A feat or feature that modifies one maneuver does not silently modify all maneuvers.
- Insufficient context or contradictory active procedures return explicit unresolved/conflict states, never a guessed winner.

### Other Pathfinder comparisons: possible later influences

Competency ranks and facets, reviewed composite-skill arithmetic, Craft/Perform/Profession families, class versus subclass versus prestige-class track ownership, archetype-like feature replacements, effective class level, multiclass progression, skill rank thresholds, independent advancement tracks, and general epic/level-band transitions already have substantial Dorks & Dice design work behind them. Preserve and document that foundation. **After** the user has defined the native system's goals and mechanics, Pathfinder examples may be used to test those decisions or suggest worthwhile changes. Do not treat them as either automatically authoritative or permanently excluded from consideration.

## Later compatibility and design review scenarios

1. A Dorks & Dice skill has multiple granular 3.x contributors and one modern umbrella concept; verify independent ranks and parent derivation. Compare PF core and Unchained skill entries without losing granularity.
2. A character multiclasses without either class entering its second 20-level band; total character level can enter that band separately. Verify a level-dependent global benefit and a class-specific effect do not use the same threshold automatically.
3. A PF1e-inspired prestige feature advances a previous class's casting without granting that class an extra level; record progression ownership and effect calculation separately.
4. A class specialization replaces two features but preserves all other class features. Multiple replacements targeting the same base feature must conflict, unlike independent subclasses and prestige levels.
5. A skill rank threshold unlocks a feature even if the character does not reach a new total character-level band.
6. An independent mythic-like track changes a feature's effect without incrementing character or class level.
7. **Open combat-maneuver design:** compare a PF-style CMB/CMD procedure with 3.x/5.x maneuver resolution and confirm that no imported procedure silently replaces grapple, touch AC, advantage, or another core operation.
8. A proposed consolidation that loses semantic or numeric distinctions is flagged for Rules Lawyer review and not automatically accepted.

## Repo evidence used

- `docs/competency-reconciliation-audit.md`
- `docs/universal-character-concepts.md`
- `docs/mechanical-relationships.md`
- `docs/character-advancement-eligibility.md`
- `docs/epic-content-modeling.md`
- `docs/character-mechanics-consumer.md`
- `docs/source-versioning.md`

## Pathfinder sources (PF1e, not PF2e)

- [Paizo, Pathfinder Roleplaying Game Conversion Guide (2009)](https://paizo.com/products/btpy89m6?Pathfinder-Roleplaying-Game-Conversion-Guide=)
- [PF1e, Character Advancement and multiclassing](https://www.aonprd.com/Rules.aspx?Category=Basics&Name=Character+Advancement)
- [PF1e, Prestige Classes and class-level definitions](https://legacy.aonprd.com/coreRulebook/prestigeClasses.html)
- [PF1e, Class Archetypes and Alternate Class Features](https://legacy.aonprd.com/ultimateCombat/classArchetypes.html)
- [PF1e, Combat and combat maneuvers](https://legacy.aonprd.com/coreRuleBook/COMBAT.html)
- [PF1e, Pathfinder Unchained — Skills and Options](https://legacy.aonprd.com/unchained/skillsAndOptions/index.html)
- [PF1e, Consolidated Skills variant](https://pathfinder.d20srd.org/unchained/skillsAndOptions/consolidatedSkills/index.html)
- [PF1e, Unchained Variant Multiclassing](https://www.aonprd.com/Rules.aspx?ID=1852)
- [PF1e, Unchained Skill Unlocks](https://legacy.aonprd.com/unchained/skillsAndOptions/skillUnlocks.html)
- [PF1e, Mythic tier definitions](https://www.aonprd.com/Rules.aspx?ID=1609)
- [PF1e, Unchained Revised Action Economy](https://aonprd.com/Rules.aspx?ID=1885)
- [Paizo compatibility and source license FAQ](https://paizo.com/licenses/compatibility/faq)

## Decision status

**Owner direction:** begin with the Dorks & Dice design that already exists, and let the owner establish its basic principles and any intended new mechanics before doing more Pathfinder-driven comparison. Pathfinder remains a useful **optional influence across any system area**, including those already substantially designed. Combat maneuvers currently have less developed Rules Core support, and the owner intends to explain ideas for them before outside models are evaluated. Neither PF1e CMB/CMD nor any specific maneuver policy has been adopted. This document approves no new gameplay code or rules.
