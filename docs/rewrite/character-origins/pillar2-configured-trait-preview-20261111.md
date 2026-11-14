# Pillar 2 — configured trait instances from contributor parameters

**Date:** 2026-11-11  
**Status:** Read-only design prototype. The configurations are **not earned grants, active native rules, executable gameplay effects, or a serialized character schema**.  
**Tool:** `tools/character_origins_configured_trait.py`  
**Tests:** `tests/test_character_origins_pillar2_configured_trait.py`

## Objective and current result

A standard Species and a mixed-Heritage character should not have distinct implementations for a mechanic such as ordinary Breath Weapon. The mechanic has one complete authoritative definition, while different sources contribute permitted values for the parameters that the trait declares inheritable.

The preview now constructs a single **configured trait definition**, retaining the entire authored compound mechanic with its operational constraints while resolving the selected configurable parameters. Unlike the previous candidate-pool prototype, it produces an inspectable, concrete definition that can eventually be compared against the standard native rule.

The output contains:

- `mechanicId` and source-qualified `definitionOccurrenceId`;
- the exact `authoredDefinition`, including its original symbolic references, untouched for audit;
- one `configuredDefinition` with concrete values in place of declared symbolic references;
- the chosen `selectedParameters` and the provenance of each value;
- the trait-level `parameterInheritance` mode and explicit `configured-preview-not-earned` / `not-evaluated` status.

The function re-creates the parameter candidate pool from the supplied source catalog, blueprint, and binding contracts. A caller can submit proposed parameter values, **not** a self-asserted pool of authorizations or the fixed mechanics to overwrite. This is still test tooling: callers can supply synthetic catalogs and contracts, so the output must never be trusted as server-authorized character state.

## Example: Red + Bronze contributor sources

The base `species.dragonborn#trait:breath-weapon` definition remains common to both. Red contributes Fire damage and a 30-by-5-foot line; Bronze contributes Lightning damage and a 15-foot cone.

The owner-approved `independent` parameter policy allows **all four** damage/area combinations:

| Damage type | Area | Evidence of eligibility |
| --- | --- | --- |
| Fire | Line | Both from Red |
| Fire | Cone | Fire from Red, cone from Bronze |
| Lightning | Line | Lightning from Bronze, line from Red |
| Lightning | Cone | Both from Bronze |

Each configured instance has the **same** fixed Attack-action replacement, Proficiency Bonus Long-Rest resource pool, Dexterity saving throw with the original DC formula, and 1/5/11/17 damage progression. Only the `damage.typeFrom` and `areaFrom` symbolic fields are materialized into `damage.type` and `area`. All other fields, including the original prose description, are copied unchanged.

This is a **structured data round trip for ordinary Breath Weapon**, not proof that all consequences of the mechanics have been evaluated or that a player has acquired the ability.

## Generic conversion scope

The prototype converts a symbolic `...From` source field to the corresponding concrete field after validating the contributor-supplied value. It accepts nested dotted paths for this existing evidence, without code dispatch on Dragonborn or any source trait name. A synthetic homebrew trait with a different attack action and recharge keeps those fields unchanged while using the same mechanism to configure its parameters.

This temporary naming convention is not an approved universal characteristic schema. First-class typed parameter bindings should eventually include explicit output paths, target value validation, compound dependencies, and compatibility checks.

## Safety and completeness boundaries

- Unsupported or unoffered parameters result in a rejected preview with **no** partial configured definition.
- No contributor with the underlying trait means no configuration.
- Additional unapproved parameter keys such as `uses`, `activation`, or `damage.progression` are rejected.
- Any unexpected symbolic field remaining outside the two declared parameter bindings blocks materialization.
- A preexisting concrete target field can not be silently overwritten.
- A preview preserves each parameter's contributing occurrence and original source node.
- Repeated identical Species contributors remain distinct contributor occurrences but do not create extra use pools.
- No physical character trait is automatically manifested, no point/resource cost is assigned, and no untested skill/creature-type/condition logic is executed.

## Inspection

From the repository root:

```bash
python3 tools/character_origins_configured_trait.py \
  --contributors ancestry.dragonborn.red ancestry.dragonborn.bronze \
  --damage-type fire \
  --area '{"shape":"cone","lengthFeet":15}'

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

## Next milestone

The next Pillar 2 step is to use a configured trait as an element of an **Origin manifestation profile**, including the distinction between eligible candidate traits and chosen, manifested traits. Demonstrate that direct standard Species selection, mixed Heritage (including mixed contributors), and homebrew use the same provenance and manifestation record shape. This work does **not** require executing the configured trait's mechanics. Rules Core combat, spell, resource, condition and runtime integration are separate future concerns. Do not mistake successful parameter substitution or approved parameter recombination for permission to acquire the trait or ignore trait-budget questions.
