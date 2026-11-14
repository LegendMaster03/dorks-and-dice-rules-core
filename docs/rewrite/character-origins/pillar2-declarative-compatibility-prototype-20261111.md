# Pillar 2 — declarative compatibility contracts and exception review

**Date:** 2026-11-11  
**Status:** Non-authoritative, **synthetic** design proof. The owner approved broad, intentional support for mixed Ancestries within a Species, compared with best-effort cross-Species support. **No specific Dragonborn grouping, cross-trait damage-type rule, excluded combination, or exception is adopted by this prototype.**

**Tool:** `tools/character_origins_compatibility_contracts.py`  
**Fixture:** `pillar2-synthetic-compatibility-contract-20261111.json`  
**Tests:** `tests/test_character_origins_pillar2_compatibility_contracts.py`

## Purpose and boundary

**Work-order clarification:** [The universal Origin model takes precedence](pillar2-universal-origin-model-first-20261111.md) over any Species-specific mixing policy. This prototype demonstrates a limited expression language with a deliberately Species-scoped fixture; it must **not** be promoted to an Origin engine contract or integrated into actual Dragonborn, Elf or cross-Species outcomes yet. The general profile, identity, provenance, choice and authorization boundaries require design and validation first.


A formulaic Species can define the characteristic dimensions and known interactions among its Ancestries in **data**, rather than accumulating individual `Red + Bronze`-style code paths. This prototype demonstrates a small general-purpose **constraint/exception representation** that can be authored, validated and used to review a proposed combination.

**The JSON fixture is entirely invented.** Its `species.synthetic-scaled` root, `test.chromatic` and `test.metallic` values, material aliases, shared-defense rule and cold/glass exception are **not rules for actual Dragonborn**. They are synthetic examples used to test mechanics of the *contract language*. In particular, the fixture's `test.shared-affinity` rule **must not** be interpreted as owner approval of a global Dragonborn Breath Weapon/Resistance/Warding coupling.

The reviewer consumes **explicitly selected characteristic values**. It does not identify actual manifestations from a native Species record, award a trait, perform combat, infer a missing choice, or decide balance.

## Prototype contract representation

A contract declares its synthetic Species root, a list of allowed dimension identifiers, constraint rules and narrow exceptions.

| Declarative construct | Behavior on explicit choices | Unknown values |
| --- | --- | --- |
| `when: {dimension, equals}` | Activates a constraint when an authored trigger value matches | Missing trigger is **unresolved**, not nonapplicable |
| `requires: {dimension, oneOf}` | Requires one offered value from a declared set | Missing target is unresolved |
| `requires: {dimension, sameAs}` | Requires a characteristic to have the **same complete JSON value** as another explicitly selected characteristic | Missing either value is unresolved |
| `excludes: {dimension, oneOf}` | Flags a forbidden value when the trigger applies | Missing target is unresolved |
| `exceptions: [{id, match, waiveRuleIds, reason}]` | Annotates an actual violation matching authored values and rule IDs | An unmatched or unused exception has no effect |

Each exception must identify existing rule IDs and at least one exact characteristic match. Multiple exceptions that claim to waive the same actual violation **fail as ambiguous**, not as implicit authorization. Other violated rules remain violated. Contracts reject unsupported operators rather than evaluating arbitrary expressions or guessing semantics.

The API reports `ruleOutcomes`, `reviewedExceptions`, and one of these statuses:

- `default-rule-conflict`: An explicitly selected value violates a rule with no matching exception.
- `incomplete-selection-needs-review`: Missing values prevent deciding one or more applicable rules, unless another unwaived violation already exists.
- `authored-exception-needs-approval`: A narrow exception annotates a detected violation, but does not authorize a character or certify compatibility.
- `no-detected-conflicts-not-certified`: No known violations or unresolved requirements under this **particular** contract. This is not a claim of mechanical compatibility, completeness or balance.

All outputs explicitly set `compatibilityCertified: false`, `balanceEvaluated: false` and `authority: synthetic-design-evidence-only`.

## Integration with the existing Pillar 2 audits

The [shared-Species foundation study](pillar2-shared-species-foundation-audit-20261111.md) identifies common authored foundations and preserves distinct source-qualified characteristic offerings. The [same-Species pair matrix](pillar2-shared-species-foundation-audit-20261111.md#systematic-same-species-coverage-without-inventing-pairwise-policies) covers every currently selectable pair without presuming compatibility.

**Next required bridge:** Define an approved representation for *actual manifested category, skin material, elemental affinity and other characteristics* and their source provenance. Only then can pair-generated offers be converted to complete player selections and checked under **real authored Species policies**. The present `skinMaterial` field is not defined in native gameplay JSON. No policy evaluator should derive that choice from a source name.

For Dragonborn, the standard Ancestry is determined by **category, category-related skin material and damage type**, while ordinary Breath Weapon area normally follows category. The previously approved **mixed-Heritage ordinary Breath Weapon** rule independently offers complete area and damage type from contributors. A future compatibility contract must not accidentally disallow that approved recombination through an unreviewed shared-affinity requirement.

A real Species policy must be **explicitly approved** before its conditions can be treated as first-party defaults. Similarly, table customizations and selected curated cross-Species exceptions must retain their separate provenance and authority. The cross-Species solution is **generic conflict detection with selective exceptions**, not a requirement to author a matrix for all possible combinations.

## Design limitations

This proof has deliberate omissions:

- No expression language, arbitrary executable predicates, arithmetic, numeric comparison, or dynamic Species-name dispatch.
- No actual category/material vocabulary, mechanical trait identities, optional grants, replacement/upgrades, severity rankings, or priority rules.
- No authorization that an explicitly selected value was offered by a contributor; that remains the concern of the candidate/manifestation pipeline.
- No automatic waiver of other constraints. No official exception whitelist or guarantee that authoring an exception makes a character balanced.
- No test of every future Dragonborn combination yet: the corpus and real Species policies are incomplete.

These limitations should be resolved only when justified by observed content requirements, not by inventing hypothetical abilities.

## Usage

```bash
python3 tools/character_origins_compatibility_contracts.py \
  --selections-json '{"category":"test.chromatic","skinMaterial":"test.crimson-scale","damageType":"fire","resistanceDamageType":"fire"}'

python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_compatibility_contracts.py' -v
```

**Implementation boundary:** This document and its synthetic fixture do not modify the native corpus, the DM's table customization authority, or character creation APIs. PR #141 remains draft and must not be merged as an approved first-party rule solely because the prototype tests pass.
