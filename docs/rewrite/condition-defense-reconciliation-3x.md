# Native conditions: 3.x defensive overlap reconciliation

**Status:** approved native-data reconciliation; executable Rules Core combat evaluator has not yet been implemented.

The existing sixteen 5.x-derived/house-rule conditions remain the starting point. The 3.x overlap adds **Flat-Footed** and **Helpless** (18 total) and updates selected overlapping conditions. This does **not** import the full 3.0 or 3.5 condition lists, and it does not establish a second edition-specific combat engine.

## Defense selection and state

The attack/effect selects **normal AC** or **Touch AC**. Touch ignores armor, shield, and natural armor contributions, but not size, Dexterity, deflection, or other permitted components. The defender's condition and feature-granted exceptions then modify the included components. Touch targeting and Flat-Footed effects can operate together, including a Touch attack against a Helpless defender.

**Flat-Footed** is a complete condition: deny positive Dexterity and dodge AC bonuses and disallow opportunity attacks, without blanket prohibition of all Reactions. **Denied Dexterity defense** is a distinct numerical effect with no implicit Flat-Footed condition or opportunity-attack restriction. **Helpless** treats effective Dexterity as 0 (−5 modifier) for AC, and applies the melee exposure penalty equivalent to Prone (−4 effective AC). When both Helpless and Prone apply, count this exposure only once. Dexterity penalties are retained when a positive Dexterity bonus is suppressed.

A defensive impairment and an attacker's independent roll improvement can coexist; do not grant attacker Advantage twice merely by translating a *single condition's* old Advantage into the analogous AC penalty. These changes are intentional native choices, not claims that Advantage and −2/−4 AC are mathematically equivalent.

### Selected condition changes

| Condition | Native result | Replaced rule |
| --- | --- | --- |
| Blinded | Half Speed; own attack Disadvantage; loss of positive Dexterity/dodge AC, −2 AC | Attackers no longer gain automatic Advantage solely from Blinded; no second blind-only 50% miss roll |
| Grappled | Retain all prior initiation/escape and target-specific attack restrictions; denial of Dexterity/dodge defense only against an attacker outside that grapple | No universal Flat-Footed condition |
| Invisible | Unseen Invisible attacker denies defender positive Dexterity/dodge AC; still protected by attack Disadvantage when opponent cannot see it | No additional Advantage on its own attacks solely due to Invisible |
| Prone | −4 AC against melee, +4 AC against ranged; own attack Disadvantage | No longer uses within/beyond 5 feet for attacks *against* Prone |
| Stunned | Half Speed, faltering speech, Incapacitated, loss of Dexterity/dodge defense, −2 AC | Does not automatically drop held objects; attack Advantage from Stunned replaced |
| Paralyzed | Incapacitated and Helpless; retain automatic critical hits for attackers within 5 feet | Generic attacks-against Advantage replaced by Helpless defenses |
| Unconscious | Incapacitated, Helpless, and Prone; retains critical hits, drop items, unawareness and post-recovery Prone | Generic attacks-against Advantage replaced by defensive contributions |

**Unchanged:** 2014 Exhaustion tiers, Wet, Incapacitated scope, and the conscious-Petrified design. Petrified still has its established Advantage-to-attackers effect; it is *not* implicitly Unconscious or Helpless. Deafened's old Initiative and verbal-component penalties and Frightened's compulsory flight remain for separate reconciliation. Restrained retains its existing 5.x effects.

## Numeric illustration

For a defender with base 10, armor +4, shield +2 and Dexterity +3, with no other modifiers:

| Situation | AC |
| --- | ---: |
| Normal | 19 |
| Touch | 13 |
| Flat-Footed | 16 |
| Touch while Flat-Footed | 10 |
| Blinded (normal attack) | 14 |
| Blinded (Touch attack) | 8 |
| Grappled (attack by grappler) | 19 |
| Grappled (attack by outsider) | 16 |
| Prone (melee attack) | 15 |
| Prone (ranged attack) | 23 |
| Helpless (melee attack) | 7 |
| Helpless and Prone (melee) | 7, not 3 |

These are illustrative consequences of the *native* rules, not a universal static AC reduction for all targets. Class features may retain normally denied contributions when explicitly granted; armor caps, other modifiers and attack-specific exceptions must be respected.

## Scope boundaries and provenance

The JSON contract and regression matrix are implemented in `data/native/defenses.json`, `data/native/conditions.json`, and `tests/test_3x_condition_defenses.py`. The Python test interpreter validates the stated data semantics but is **not production combat execution code**; that will be added when the rewrite's executable engine is built.

3.5 source references (public SRD transcription, OGL 1.0a): [Condition Summary](https://www.d20srd.org/srd/conditionSummary.htm), [Combat Modifiers](https://www.d20srd.org/srd/combat/combatModifiers.htm), and [Armor Class and Touch](https://www.d20srd.org/srd/combat/combatStatistics.htm#armorClass). Reconciliation decisions and source boundaries are recorded in `data/reference/3x/condition-defense-reconciliation.json`. The pre-existing **pinned 5.x SRD coverage manifest is not changed**.

Still deferred: other 3.x conditions, full grapple action/provocation/multiple-grappler mechanics, fear tiers and compulsory flight, Deafened spellcasting/Initiative, coup de grace, and all production runtime evaluation.
