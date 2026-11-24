# First shared combat attack-check slice — November 24, 2026

**Status:** Experimental mechanics calculator on a feature branch. Not a complete combat executor, production gameplay default, or permission to deploy or merge.

The first executable slice deliberately reuses the already implemented native rolls, native condition roll/defense adjustments, and the four approved normal/touch/flat-footed AC profiles. The Standard/Complex Combatant distinction does not change the attack calculation. Neither combatant model is granted any automatic ability, attack or size effect by this slice.

## Scope

- Deterministically resolve an **already authorized** attack roll against effective Normal, Touch, Flat-Footed or Touch Flat-Footed AC.
- Combine attacker and defender conditions that affect attack-roll Advantage and Disadvantage; opposing effects cancel.
- Evaluate defender-side condition AC adjustments against a declared `melee` or `ranged` attack, preserving the existing native condition predicates and defender size contribution.
- Accept natural d20 rolls from an authorized roller; support Normal, Advantage and Disadvantage. Emphasis without competing modes delegates to the existing native roll evaluator; unapproved Emphasis interactions fail closed.
- Report a selected natural 1, natural 20, or qualifying expanded critical threat as `critical-resolution-required`, **not** as ordinary damage. A non-20 threat requires its attack total to hit AC. Natural 1 reports a miss and natural 20 a hit; the critical follow-up still requires separate table policy.
- Report unknown conditions, missing conditional evidence, unsupported defense components, invalid roll inputs or excessive numeric values without silently accepting an ordinary hit.

**Not implemented:** Source-authorized attacks, combatant/creature ownership, legal target and reach validation, action spending, bonus or reaction gating, weapon attributes, proficiency derivation, damage dice, critical damage stacking, Resistance, flat DR, HP persistence, dying or defeat, status transitions, grappling, creature parts, and Standard/Complex-specific capabilities.

## Authenticated API

The new operation is `POST /api/mechanics/v1/experimental/combat/attack-check` (or its private-tunnel equivalent). It inherits the existing authentication, ruleset fingerprint and source-access rules. A sample request is:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<validated-catalog-fingerprint>",
  "attackKind": "melee",
  "attackBonus": 5,
  "firstDie": 13,
  "secondDie": null,
  "defenseProfile": "Normal",
  "defenderContributions": {
    "armor": 4,
    "shield": 2,
    "dexterity": 3
  },
  "attackerConditionIds": [],
  "defenderConditionIds": [],
  "baseRollMode": "Normal",
  "criticalThreatMinimum": 20
}
```

Here `defenseProfile` and `baseRollMode` are the existing C# enums and use their default JSON representation unless an approved serializer configuration explicitly changes it; clients must verify the current transport representation rather than infer that these illustrative names are accepted JSON strings. The resulting effective normal AC is 19; the roll total is 18, so this is an ordinary miss. The response includes `contractVersion`, `experimental`, `rulesetId`, `fingerprint`, and a `result` containing `status`, `hit`, `attackTotal`, `armorClass`, `naturalDie`, `rollMode`, `issues` and `reason`.

**Trust boundary:** The consumer, not Rules Core, is responsible for validating attack permissions, entity state, target identities, dice provenance, the source of AC components, and all change requests. Do not treat a successful calculator result as an authorized encounter transaction. These authenticated endpoints are not anonymous, but their input fields are still caller claims rather than server-loaded combatant facts.

The separate `/experimental/combat/action-budget` operation is resource-only and explicitly permits `independent` or `shared-end-turn`; no global Reaction budget choice is approved. The encounter tool must select one policy for all combatants, persist their returned snapshots, and enforce encounter ordering and ability triggers.

## Tests and next milestone

The new xUnit tests exercise ordinary hit/miss, Advantage and Disadvantage, invalid and missing dice, overflow, natural 1/20 handling, expanded threat ranges, the four native defense profiles, condition-based AC, opposing condition roll modes, incompatible attack facts, unknown predicates and fingerprint mismatch. Existing condition tests remain authoritative for individual effect semantics.

Before integrating with Block Initiative or Character Sheet, run the complete .NET 10 build/test and native JSON suite, and verify the authenticated HTTP routes. These tests have **not** been executed by this editing environment. Then integrate an authoritative attack/event request and the separately reviewed damage and HP-resolution rules, instead of silently resolving outstanding damage arithmetic or critical procedures.

## Integration with current code

This development branch is based on November 24 `main`. Combat design records from the earlier November 12 branches were selectively ported without overwriting current Origins or unrelated mechanics files. The optional relative-size Touch AC adjustment remains a parked research proposal; it does not alter the resolver.
