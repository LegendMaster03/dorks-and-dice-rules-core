# Phase 0 — Pathfinder First Edition gameplay comparison

Status: **research and comparison only**, 2026-10-09. Pathfinder 1e is an additional design reference for the Dorks & Dice rewrite, not an automatically accepted rule source, mandatory import target, or substitute for already established Dorks & Dice gameplay decisions.

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

## Priority investigations

### 1. Skill/competency mapping and loss of meaning — highest priority

Compare PF1e's core skill simplification and Unchained consolidation with Dorks & Dice's existing *derived-parent* relationships. Test composite skills with unequal components and component-targeted modifiers; ensure PF skill mappings do not accidentally make the old components disappear or treat skill identities as equal numeric profiles. Preserve explicit group/facet/family distinctions.

Unchained's consolidated skills may merge formerly different functions (for example Bluff + Diplomacy + Intimidate into Influence). This is **not** equivalent to Dorks & Dice's exact arithmetic-mean Stealth/Perception/Athletics/Acrobatics rule. Unchained also offers Background Skills, which specifically protects vocational skills from being outcompeted by combat/adventuring choices; that is relevant to our independently trainable Craft/Perform/Profession model.

### 2. Class-customization semantics — highest priority

Represent three distinct structures and verify each against example PF1e and D&D material:

1. **Subclass:** a class-bound specialization with its own features that uses its parent class's level track.
2. **Prestige class:** a separately leveled class with prerequisites and possible contributions to another class's mechanics (such as spellcasting progression).
3. **Feature replacement / archetype:** an optional modification to the base class that removes or replaces named features, preserving unaffected levels and features.

The same feature may be a parent-track modification, a prerequisite gate, or a separately progressed grant. Do not collapse these into a generic "specialized class" type or infer that a PF archetype equals a 5e subclass.

### 3. Level basis and bands — highest priority

For each source-derived feature, capture at least these *semantic questions*, without settling the eventual data representation:

- **Acquisition basis:** total character level, a particular class occurrence, prestige-class occurrence, effective class level, competency rank, independent tier, or other track?
- **Progression basis:** what track causes the feature to improve and at what thresholds/bands?
- **Prerequisite basis:** which levels/qualifications are checked, and does a separate effective-level formula apply?
- **Effect basis:** what level controls an effect's damage, duration, uses, DC, etc.?
- **Transition behavior:** acquired, continued, stopped improving but retained, evolved, replaced, or no longer applicable?

PF1e's ordinary multiclassing, Unchained variant multiclassing, prestige caster progression, skill unlocks, and mythic tiers are complementary verification examples. They illustrate why acquisition and effect basis can differ from a feature's owning class.

### 4. Combat and alternative procedures — medium priority

Use CMB/CMD and Unchained revised action economy as case studies in converting repeated rule text into typed reusable procedures. Keep the existing Dorks & Dice rule that touch attacks, flat-footed states, denied Dexterity, and advantage/disadvantage are distinct effects. Pathfinder's combat formulas are source implementations, not automatically authoritative Dorks & Dice defaults.

### 5. Scope of PF1e content and import licensing — separate decisions

A comparative game-design audit **does not authorize** a new Pathfinder importer or the import of PF1e's full published content. If desired, a later decision can scope PF1e as an additional supported source. Check the specific publication's OGL declaration and other conditions; Pathfinder 1e's OGL-licensed material and Paizo Product Identity are distinct. Do not infer that its material became CC BY because D&D 5.1/5.2.1 SRDs did.

## Acceptance scenarios to add to the design specification

1. A Dorks & Dice skill has multiple granular 3.x contributors and one modern umbrella concept; verify independent ranks and parent derivation. Compare PF core and Unchained skill entries without losing granularity.
2. A character multiclasses without either class entering its second 20-level band; total character level can enter that band separately. Verify a level-dependent global benefit and a class-specific effect do not use the same threshold automatically.
3. A PF1e-inspired prestige feature advances a previous class's casting without granting that class an extra level; record progression ownership and effect calculation separately.
4. A class specialization replaces two features but preserves all other class features. Multiple replacements targeting the same base feature must conflict, unlike independent subclasses and prestige levels.
5. A skill rank threshold unlocks a feature even if the character does not reach a new total character-level band.
6. An independent mythic-like track changes a feature's effect without incrementing character or class level.
7. A PF-style CMB/CMD combat procedure coexists as an alternative rule implementation without silently replacing grapple, touch AC, advantage, or another core operation.
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

**No new Dorks & Dice gameplay rules were approved or implemented by this comparison.** It is a research input to Phase 0 and a list of targeted regression/design scenarios. In particular, do not automatically adopt PF skill arithmetic, archetypes as subclasses, CMB/CMD as the universal combat model, mythic tiers as epic level bands, or Unchained variants as base rules.
