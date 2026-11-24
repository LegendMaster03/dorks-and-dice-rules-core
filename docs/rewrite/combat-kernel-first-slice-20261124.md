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
- Independently preview ordinary **damage arithmetic** using the established signed resistance/vulnerability percentage first, then summed applicable flat DR, with an optional proposed remaining HP. If percentage multiplication produces fractional damage, fail closed until the native rounding rule is approved. This is not connected to hit, critical, or actual HP events.

**Not implemented:** Source-authorized attacks, combatant/creature ownership, legal target and reach validation, action spending, bonus or reaction gating, weapon attributes, proficiency derivation, rolling damage dice, critical damage stacking, selecting resistance/DR from native grants, fractional damage rounding, HP persistence, dying or defeat, status transitions, grappling, creature parts, and Standard/Complex-specific capabilities.

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

## Damage calculation preview

`POST /api/mechanics/v1/experimental/combat/damage-preview` (or the equivalent private tunnel route) accepts verified inputs such as:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<validated-catalog-fingerprint>",
  "incomingDamage": 20,
  "damageModificationPercent": 50,
  "applicableDamageReduction": [2, 3],
  "currentHitPoints": 20,
  "maximumHitPoints": 20
}
```

This produces modified damage `10`, applied flat DR `5`, final damage `5`, and **proposed** remaining HP `15`. Damage from source features, the eligibility of each DR grant and bypass condition, and whether the attack actually hit are not verified by this calculator. Fractional results such as 3 points of damage with 25% resistance report `rounding-unresolved` instead of introducing a rounding rule. A resulting HP of zero is only reported as a proposed state, not an adjudicated death or defeat.

## Tests and next milestone

The new xUnit tests exercise ordinary hit/miss, Advantage and Disadvantage, invalid and missing dice, overflow, natural 1/20 handling, expanded threat ranges, the four native defense profiles, condition-based AC, opposing condition roll modes, incompatible attack facts, unknown predicates and fingerprint mismatch. Existing condition tests remain authoritative for individual effect semantics. A second damage-arithmetic unit suite covers percentage-first ordering, summed flat DR, immunity, vulnerability, exact and fractional results, proposed HP, invalid inputs and nonmutation.

Before integrating with Block Initiative or Character Sheet, run the complete .NET 10 build/test and native JSON suite, and verify the authenticated HTTP routes. These tests have **not** been executed by this editing environment. Then integrate an authoritative attack/event request and the separately reviewed damage and HP-resolution rules, instead of silently resolving outstanding damage arithmetic or critical procedures.

## Integration with current code

This development branch is based on November 24 `main`. Combat design records from the earlier November 12 branches were selectively ported without overwriting current Origins or unrelated mechanics files. The optional relative-size Touch AC adjustment remains a parked research proposal; it does not alter the resolver.
