# Standard and Complex Combatants — native design record (2026-11-12)

**State:** Approved classification and generalization principles, with detailed combat mechanics, JSON schemas, individual creature migrations, and code implementation **pending**.

**Native design data:** `data/rulesets/first-party-001/native/combatant-models.json`.
**Related foundation:** `data/rulesets/first-party-001/native/monster-statblock-foundation.json`.
**Related research:** `docs/rewrite/combat-system-comparison-3x-5x-pf1.md`.
**Current specialized tool reference:** `LegendMaster03/dorks-and-dice-block-initiative/docs/kaiju-integration.md`.

## Decisions to preserve in native data

1. **Two statblock/combatant classifications:** **Standard Combatant** and **Complex Combatant**. These classify a creature's combat mechanics, not the initiative mode or the entire encounter.
2. **Shared encounter:** Standard and Complex Combatants coexist and interact in one combat. A Complex creature and ordinary minions require no parallel encounter or edition engine. Standard and optional Block Initiative remain separate encounter-level scheduling choices.
3. **Complexity is independent of size:** Neither classification implies Large/Huge/Gargantuan size, a boss role, challenge rating, or a particular player complexity preference. A very large Standard Combatant and a Medium Complex Combatant are both valid possibilities.
4. **Size-dependent features are separate:** Creature-climbing, body-scale positioning, exceptional reach, and related mechanics must be qualified by their own size/anatomy/feature rules where appropriate. They are not a standard package of Complex Combatant benefits.
5. **Generalize behavior transitions:** The original Kaiju-style rage/rampage concept is an example of creature-specific behavior changes, not the universal state transition. A creature can change tactics, abilities, defenses, targets or other explicitly authored capabilities for different reasons. No mandatory rage or phase ladder.
6. **Complex capabilities are optional:** Individually targetable components, alternative vitality, behavior transitions and special defeat conditions are candidates for composable capabilities; none is mandatory on every Complex Combatant. This direction is not yet a final capability/schema composition design.
7. **Standard does not mean featureless:** Standard Combatants may still have meaningful triggered traits, reactions and unusual attacks. The eventual rule dividing ordinary features from Complex-model subsystems needs review.

These decisions generalize mechanical ideas without incorporating Loot Tavern's Kaiju Fighting source procedures verbatim or relabeling the external system as an owned Dorks & Dice rules publication.

## Boundaries and explicit non-decisions

- **No creature migration in this change.** The  existing `native/bestiary/` entries remain as authored; none automatically gains a model classification, changes HP or CR, becomes a Kaiju-style creature, or receives new component/rage mechanics.
- **No runnable combat model is created.** The native records are design declarations. The current Rules Core loader can index them as native records; the combat executor and tool consumers do not implement their meaning simply by loading JSON.
- **No universal vitality or special defeat procedure is prescribed for Complex.** A Complex creature may retain ordinary HP; alternative pools or finishing requirements require explicit creature-specific definitions.
- **No automatic use of source-specific thresholds or terms.** Loot Tavern Kaiju behaviors, Chaos Threshold, Rampage, vulnerable areas and finishing conditions remain references to the existing external system; only separately approved Dorks & Dice-native mechanics become authoritative.
- **No universal behavior phase order, default rage, or fixed event triggers.**
- **No mandatory exclusive capability set.** The detailed resolution of combining multiple Complex capabilities, precedence, and the dividing line between Standard traits and Complex subsystems is still undecided.
- **No impact on the player complexity contract.** Choosing straightforward or tactical play for a character is independent of the creature's Standard/Complex statblock classification.

## Design tests before runtime implementation

| Case | Expected design property |
| --- | --- |
| Complex giant with Standard humanoid minions | All combatants participate in the same initiative and can target, damage or affect one another through shared interfaces |
| Huge Standard creature | Creature size alone does not activate the Complex model, vulnerable parts, or rage |
| Medium Complex duelist | Can have an explicitly designed stance or behavior transition without becoming oversized or acquiring anatomy-focused mechanics |
| Complex construct with destructible devices | Individual components can change its abilities without mandatory rampage or a special finishing blow |
| Complex creature retaining ordinary HP | Complex classification does not silently replace the ordinary vitality system |
| Large creature with body-climbing feature | Such a mechanic requires its own size/anatomy/action prerequisites, regardless of combatant model |
| Standard creature with a triggered reaction | Does not become Complex merely because it has an authored special ability |
| Two simultaneous behavior triggers | A deliberate ordering/precedence rule is required before code execution, not an arbitrary runtime guess |
| Existing SRD bestiary import | Current stats and traits remain intact until an explicit per-entry migration is designed and approved |

## Remaining design work

1. Decide where per-combatant classification lives and how the JSON model references optional capabilities.
2. Resolve whether Complex is a distinct state machine, a composition of optional traits, or both. Determine any necessary prerequisites and exclusions.
3. Specify shared targeting/attacking/effect contracts when different vitality, components or defeat models interact.
4. Design generalized behavior triggers, state/transition priority, duration, concurrent effects, reversion and persistence.
5. Specify size-qualified climbing/positioning rules separately, without granting them by model name.
6. Decide which per-creature abilities constitute a Standard triggered feature versus a Complex subsystem.
7. Reconcile the design with existing combat action-economy decisions, optional Block Initiative and the independent player-complexity goal.
8. Only after design approval, implement validation, runtime resolution, conversion from source statblocks, consumer APIs and tests in defined slices.

**Implementation policy:** Progressively update authoritative native JSON as decisions are approved. Keep unapproved decisions visibly unresolved. Do not infer that a record is runnable merely because it is stored under `native/`.
