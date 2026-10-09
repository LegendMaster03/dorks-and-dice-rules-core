# Combat across D&D 3.0, D&D 3.5, D&D 5e (2014), D&D 5.5e (2024), and Pathfinder 1e

Status: **comparative research / pre-design evidence**. 2026-10-09. This is **not** the proposed or accepted combat system of Dorks & Dice. The owner wants to examine what the five combat systems have in common and how they diverge before choosing native procedures. Treat the comparisons as **functional and mechanical**, not just a synonym lookup. The already recovered Dorks & Dice decisions take precedence as descriptions of the existing design; none of these five games supplies implicit missing defaults.

## 1. Invariant conceptual structure

All five systems support:
- combat rounds and initiative-ordered turns;
- actions/limited activity and movement during a turn;
- d20 attacks against armor/other target defenses, with attacks and weapon/ability modifiers;
- hit points, damage, healing and defeat/death procedures;
- reach, space, creature size, positioning and restrictions on legal targets;
- conditions, saves and reaction/opportunity attacks, each with different triggers and effects;
- non-damage combat control (grabbing, pushing, tripping, moving, dropping equipment), though definitions and procedures vary;
- feature- or equipment-driven exceptions, with GM adjudication for cases not covered.

These are **shared intentions/categories**, not numerically identical rules. A common effect name does not license substituting a source's different probability model, action cost, defense or consequences.

## 2. Procedure comparison

| Aspect | D&D 3.0 | D&D 3.5 | Pathfinder 1e core | D&D 5e 2014 | D&D 5.5e 2024 |
|---|---|---|---|---|---|
| Turn economy | Standard (typically action + move), move-equivalent, full-round, free, **partial** actions for restricted turns | Standard + move, full-round, free; 5-ft step without ordinary movement; immediate/swift introduced through later 3.5 supplements, not core SRD baseline | Standard, move, full-round, free, swift, immediate; 5-ft step | Action, movement, possible bonus action, reaction, free interaction | Same broad action/move/bonus/reaction framework; named actions and weapon handling updated |
| Extra attacks | BAB-based iterative attacks generally require full attack | BAB-based iterative attacks generally require full attack | BAB-based iterative attacks generally require full attack | Extra Attack / other features add attacks to Attack action; move may interleave | Extra Attack / feature-given attacks within Attack action; move may interleave |
| Movement and opportunity | Threatened squares, movement **through or out of** a threatened area can provoke; 5-ft no-provocation movement; partial/surprise movement specifics | Usually provoke when **leaving a threatened square**; normal 5-ft step and withdraw exceptions | Similar threatened-square and 5-ft-step approach, action-triggered opportunities | Usually provoke when **leaving a creature's reach**, not for every threatened square crossed; Disengage suppresses | Generally as 2014, but opportunity attacks may use an Unarmed Strike under the 2024 glossary |
| Special-action opportunities | Many special actions, ranged attacks, spellcasting, item interactions can provoke | Many special actions, ranged attacks, spells can provoke | Broadly similar to 3.5; maneuver-specific feat exceptions | Usually leaving reach; casting spells/ranged attacks do **not** provoke by default merely because performed in reach | Similar narrow movement trigger; no general 3.x-style casting/ranged opportunity rule |
| Surprise | Surprise round: aware characters have a **partial** action; unaware ones do not act | Surprise round: aware characters take one **standard or move** action | Surprise round: aware combatants generally take a standard or move action | Surprised creatures cannot move or act on first turn and cannot react until it ends | Surprised creatures have **Disadvantage on Initiative**; no separate lost first turn |
| Attack bonus | BAB + ability/size/other modifiers | BAB + ability/size/other modifiers | BAB + ability/size/other modifiers | Ability modifier + proficiency when proficient + other modifiers | Same broad proficiency model |
| Defense | Ordinary, **touch**, and **flat-footed** AC; different contribution formulas | Ordinary, touch and flat-footed AC | Ordinary, touch and flat-footed AC; **CMD** also protects against combat maneuvers | Ordinary AC; circumstances use advantage/disadvantage, ability contests, saves, cover, etc. | Ordinary AC; advantage/disadvantage, saves, cover; 2024 unarmed options and weapon masteries have defined other targets |
| Non-AC saves | Fortitude, Reflex, Will | Fortitude, Reflex, Will | Fortitude, Reflex, Will | Six ability-based saving throws | Six ability-based saving throws |
| Bonuses and roll conditions | Numerous typed numeric bonuses/penalties; cover and concealment use explicit numeric/probability effects | Numerous typed numeric bonuses/penalties; cover +4 AC for ordinary cover, concealment miss chance | Multiple named bonus types, cover/concealment; many extra defense inputs | Usually advantage/disadvantage and simpler bonus vocabulary; cover half +2, three-quarters +5 AC | Advantage/disadvantage; similar cover; some revised conditions and weapon-property effect rules |
| Critical hits | Threat range and a **confirmation attack**; weapon damage multipliers | Threat range and confirmation; variable weapon multipliers | Threat range and confirmation; variable weapon multipliers | Natural-20 attack crit rolls attack damage dice twice; no confirmation roll | Natural-20 attack crit rolls attack damage dice twice; no confirmation roll |
| Nonlethal/defeat | Subdual damage tracked separately; negatives and death at −10 HP | Nonlethal damage tracked separately; negatives and death at −10 HP | Nonlethal damage tracked separately; death at negative HP equal to Constitution score | 0 HP with death saves; separate knockout/immediate death rules | 0 HP with death saves; related updated terminology/procedures |
| Flanking | Default positional +2 melee attack | Default positional +2 melee attack | Default positional +2 melee attack | Not standard core combat; optional DMG flanking rule exists | Not a universal default in the free core rules; optional/specific features possible |
| Maneuver check family | Dedicated procedures: touch attacks, opposed Strength or attack checks, other steps | Dedicated procedures: touch attacks, opposed checks, other steps | **CMB attack check vs CMD**, but maneuver costs, effects and conditions remain maneuver-specific | Grapple and shove: **opposed Athletics** versus Athletics or Acrobatics | Grapple and shove: **Unarmed Strike options**, defender Strength or Dexterity saving throw, not an ordinary attack roll for those options |
| Maneuver availability | Built-in grapple, bull rush, disarm, overrun, trip; other special attacks | Built-in grapple, bull rush, disarm, overrun, trip; other special attacks | Built-in above, CMB/CMD common checks, later additional maneuvers in supplements | Core grapple and shove plus contextual improvisation; specific feats/DMG optional actions add cases | Core grapple/shove via Unarmed Strike; mastery effects such as Push and Topple through qualifying weapon feature; contextual improvisation |

**Caution:** The table compares *core/base combat rules* to avoid treating optional supplements as universal defaults; later 3.5 swift/immediate, 5e DMG alternatives, Pathfinder Unchained action economies and supplemental maneuvers should be separate variants. 3.0 and 3.5 are not identical despite the shared BAB terminology. In particular, 3.0's combat actions and surprise round use different action categories.

## 3. Maneuvers: shared intentions, different procedures

### Grapple — same intention, incompatible numeric/procedural implementations

- **D&D 3.0:** grabbing begins with a melee touch attack, opposed grapple check with **BAB + Strength + 3.x grapple-size modifier**, and movement into the target's space; grappling permits subsequent opposed checks for damaging, pinning, escaping and other effects.
- **D&D 3.5:** retains melee touch plus opposed grapple checks with BAB/Strength/grapple-size; uses updated grappling, unarmed-strike and combat-action language.
- **PF1e:** initiates with **d20 + CMB vs CMD**; successful grapple makes both parties grappled; sustaining, pinning, moving or damaging is governed by maneuver-specific actions/checks and status rules. **PF size modifiers are different from the 3.x grapple-size modifiers**, even when both formulas reference BAB and Strength.
- **5e 2014:** using the Attack action, replace one attack with Strength (Athletics) **contested** by Strength (Athletics) or Dexterity (Acrobatics), target's choice; free hand, reach and size requirements. Grappled primarily limits the target's speed; escape is a contested check.
- **5.5e 2024:** Grapple is an **Unarmed Strike** effect with the defender choosing a Strength or Dexterity **saving throw** against DC = 8 + attacker's Strength modifier + proficiency bonus; a hand is required and size matters. Escape on a later turn uses Athletics or Acrobatics vs the grapple's **escape DC**. **Initiating a 2024 grapple does not itself require an attack roll.**

### Push / bull rush / shove

- **3.0 / 3.5:** bull rush attempts to force a creature backward, normally using opposed **Strength checks**, with size, charging/movement restrictions, and possible opportunity-attack consequences.
- **PF1e:** bull rush uses **CMB vs CMD**, pushing 5 ft on success plus another 5 ft per 5 points above CMD; action and reactions remain specialized.
- **5e 2014:** shove uses the same contested Athletics model as grapple and gives a **choice** of push 5 ft or prone.
- **5.5e 2024:** the Unarmed Strike **Shove** option requires a Strength or Dexterity save; separately, weapon mastery **Push** can push a target on a qualifying hit, and **Topple** can force a Constitution save for Prone. Similar outcomes may come from materially different features and action chains.

### Trip / prone

- **3.0 / 3.5:** a melee touch attack followed by opposed Strength versus the defender's preferred Strength or Dexterity (plus modifiers); failure may allow a counter-trip. Unarmed trip in 3.5 can provoke an opportunity attack; trip-capable weapons and feats alter that.
- **PF1e:** trip uses CMB vs CMD; resistance depends on target features and condition exceptions.
- **5e 2014:** shove-to-prone, an opposed skill contest; source-specific features may also inflict prone.
- **5.5e 2024:** shove-to-prone uses a save and can be selected via Unarmed Strike; mastery Topple is another pathway.

### Disarm / sunder / overrun

- **3.0 / 3.5:** distinct special-attack families with action costs, attack/opposed checks, size/equipment constraints, attacks of opportunity, or counter-effects; not simply variants of trip.
- **PF1e:** these intents use CMB/CMD as the primary success resolution but keep distinct conditions, equipment effects, progression by degree of success, and action costs.
- **5e 2014 / 2024:** do not have these three as a universal equivalent to PF CMB/CMD in the *core combat action list*. A DM may adjudicate improvised attempts, optional supplements or a specific feature may provide the procedure. Do **not** convert the *absence of a universal core action* into a declaration that the intent is forbidden.

### A critical non-equivalence example: PF1e CMB is not 3.5 grapple

**D&D 3.5 grapple size (selected):** Small −4, Medium 0, Large +4.

**PF1e CMB/CMD size (selected):** Small −1, Medium 0, Large +1.

PF1e CMD is also 10 + BAB + Strength + Dexterity + special size + applicable modifiers. Sharing the concept of an attacker and defender does **not** make these two contests numerically interchangeable. This is a strong automated conversion regression case.

## 4. System-wide differences that affect maneuvers

### Action economy and opportunity triggers

A maneuver's success calculation can not be defined independently of its **cost, timing and opportunities**. A 3.x maneuver replacing an attack within full attack interacts with iterative BAB attacks differently from a 5e grapple/shove replacing one attack of an Extra Attack action. A PF1e maneuver may replace an attack or use a standard action. The same translated outcome requires explicit action-cost and reactivity semantics.

### Condition consequences

The **Grappled** and **Prone** labels do not designate identical state effects across games. Some grapple rules penalize or restrict both participants, some primarily restrict the target, and some add further consequences against non-grapplers. Conditions should be compared by actual consequences and termination rules, not by names alone.

### Target defense and resistance type

An attack against **touch AC**, a PF **CMB check against CMD**, an opposed 5e **Athletics check**, and a defender 2024 **saving throw** are four different resolution architectures. They may lead to equivalent *intent* (e.g. push five feet), but do not have the same participants' dice, modifiers, automatic success, or feature interactions.

### Cover, concealment, opportunity and casting

3.0 has multiple cover fractions with different AC bonuses and different concealment miss percentages. 3.5 and Pathfinder retain numeric cover / concealment effects; 5e replaces many concealment consequences with advantage/disadvantage. Casting while threatened in 3.x can provoke an opportunity attack; casting near an enemy in 5e is not generally such a trigger. Do not recast these as merely cosmetic variants.

### Injury and criticals

The owner has now established the **Dorks & Dice native critical-stacking direction**: consecutive natural 20s accumulate critical success tiers, consecutive natural 1s accumulate critical failure tiers, a triple combat critical success reduces the attacked opponent to **0 HP**, and a triple critical failure reduces the failing character to **0 HP**. A normal critical hit causes **maximum damage plus a further damage roll** instead of simply rolling doubled dice; 5e-style death saving throws remain. This combines some features of 3.x's additional confirmation roll with 5.x's 0-HP/death-save model but is **not equivalent** to either source rule. The double-critical effects, modifier arithmetic, and noncombat scope still need owner definition. See [the native rules specification](native-rules-design-specification.md) for explicit accepted and open details.

The remaining comparisons of 3.x subdual/nonlethal tracking, 5e nonlethal melee knockouts, and variant dying/death mechanics should now be checked against this owner-established native direction, not treated as a choice between importing entire external critical/death systems.

## 5. Design conclusions for Dorks & Dice — questions, not rulings

1. **Shared vocabulary is feasible:** initiative, turn, action cost, movement, attack, defense, save, contest, effect, condition, damage, and maneuver intent are broad concepts recognized across all five systems.
2. **A maneuver should have separable meaning and procedure**: target/outcome, requirements, resolution, timing, and resulting conditions can be examined independently. Whether Dorks & Dice has **one** maneuver formula or many is an owner decision.
3. **Apply the native critical system rather than source defaults**: critical confirmation/stacking, maximum-plus-roll damage, triple-critical 0 HP, and 5e-style death saves have owner-stated direction. Unspecified tier-two consequences and arithmetic remain open.
4. **Do not equate mechanical statistics that happen to use the same ability**: BAB, proficiency, ranks, a skill contest, a maneuver attack, and a saving-throw DC are not automatically transformable by name.
5. **Do not inherit a source-specific core combat action economy by accident.** Determine Dorks & Dice-native action/reaction model before trying to preserve or convert individual 3.x and 5.x special attacks into it.
6. **Preserve older options where compatible** with established additive philosophy, but no unconditional union of contradictory procedures, conditions, or action costs.
7. **Owner first:** review this matrix as evidence, invite the owner's combat-system and maneuver ideas, and record intended native rules before proposing algorithms. Pathfinder influences may apply to any area, not exclusively maneuvers.

## 6. Questions for the next owner review

These are natural discussion prompts, not prerequisites for using the comparison.

- Should **maneuver** mean a category of combat actions (grapple, trip, disarm, reposition) defined by intent, regardless of whether a weapon, spell, class feature, or skill performs it?
- Should there be a **single baseline maneuver resolution**, or a general procedure contract with different defenses and dice for particular actions?
- What should be the default **turn/action/movement/reaction model**, since this affects every maneuver?
- Should a successful maneuver directly apply a condition such as Prone/Grappled, or first produce a more general effect such as move, restrain, knockdown, take/disable equipment?
- When a source procedure is different but produces the same broad outcome, does the native Dorks & Dice procedure govern by default, with opt-in source-specific behavior, or do some alternative procedures coexist?

## Primary source references

- [3.0 SRD, Combat Basics](https://www.dragon.ee/30srd/combat_basics.htm)
- [3.0 SRD, Combat Actions](https://www.dragon.ee/30srd/combat_actions.htm)
- [3.0 SRD, Cover and Concealment](https://www.dragon.ee/30srd/coverconceal.htm)
- [3.0 SRD, Death/Dying](https://www.dragon.ee/30srd/death_dying_healing.htm)
- [3.5 SRD, Actions in Combat](https://www.d20srd.org/srd/combat/actionsInCombat.htm)
- [3.5 SRD, Special Attacks](https://www.d20srd.org/srd/combat/specialAttacks.htm)
- [3.5 SRD, Attacks of Opportunity](https://www.d20srd.org/srd/combat/attacksOfOpportunity.htm)
- [3.5 SRD, Injury and Death](https://www.d20srd.org/srd/combat/injuryandDeath.htm)
- [PF1e Core, Combat](https://legacy.aonprd.com/coreRuleBook/COMBAT.html)
- [PF1e Archives of Nethys, Combat Maneuvers](https://www.aonprd.com/Rules.aspx?ID=185)
- [PF1e Archives of Nethys, Dead](https://aonprd.com/Rules.aspx?ID=168)
- [5e (2014), Basic Rules Combat](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/combat)
- [5.5e (2024), Playing the Game](https://www.dndbeyond.com/sources/dnd/br-2024/playing-the-game)
- [5.5e (2024), Rules Glossary](https://www.dndbeyond.com/sources/dnd/br-2024/rules-glossary)
- [5.5e (2024), Equipment and Mastery](https://www.dndbeyond.com/sources/dnd/br-2024/equipment)

No gameplay decisions, engine code, schemas, data migration, or deploy actions are authorized by this comparison.
