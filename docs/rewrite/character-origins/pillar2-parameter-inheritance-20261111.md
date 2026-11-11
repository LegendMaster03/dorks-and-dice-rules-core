# Pillar 2 — inheritable trait parameters and contributor provenance

**Date:** 2026-11-11  
**Status:** Provisional design proposal plus read-only eligibility prototype. The user approved *supporting* linked and independent parameter-selection policies on a per-trait basis, **not** which policy is authoritative for any specific trait.  
**Code:** `tools/character_origins_parameter_inheritance.py`  
**Test-only blueprint:** `pillar2-parameter-inheritance-evidence-20261111.json`  
**Tests:** `tests/test_character_origins_pillar2_parameter_inheritance.py`

## Problem

A universally defined mechanic may have customizable parameters. A character's Heritage candidate pool therefore must describe both **which traits are eligible to manifest** and **which parameter values each contributor provides**. It must retain the relationship between values so that combining independent parameter pools does not inadvertently permit combinations that only appear as a package in the original content.

For the existing Dragonborn ordinary Breath Weapon:

- One common compound mechanic contains its Attack-action replacement, save, uses, damage scaling, and other operational rules.
- Its **damage type** comes from a selected Dragonborn leaf.
- Its **area** comes from its selected ancestry family/leaf.
- The prototype uses the existing source-qualified Breath Weapon trait occurrence and resolves the existing native source values using the declarative bindings in `pillar2-legacy-binding-evidence-20261111.json`. It does **not** copy a distinct full Breath Weapon implementation for every ancestry.

## Trait-level parameter inheritance policy

The provisional trait definition specifies a `parameterInheritance.mode`:

| Mode | Permitted hypothetical proposal | Excluded by the mode |
| --- | --- | --- |
| `linked` | An **entire** parameter configuration supplied by one eligible contributor | Mixing damage type from one contributor with area from another unless that full pair is also offered |
| `independent` | One eligible value **for each** parameter, with independent source provenance | Values not supplied by any contributor |

For Red (Fire, 30-by-5-foot line) and Bronze (Lightning, 15-foot cone):

- **Linked** allows (Fire, line) or (Lightning, cone); it rejects (Fire, cone).
- **Independent** additionally allows (Fire, cone) and (Lightning, line), because both values in each pairing are separately represented in the candidate pool.
- Both policies are supported by one generic evaluator, configured by the trait's policy. The provisional fixture currently shows `linked` as an *example* only; no native Dragonborn parameter-inheritance policy has been finalized.

This first proof supports one linkage policy for all designated inheritable parameters of a trait. **Mixed groups**, per-parameter dependencies, conditional eligibility, and more complicated relationships remain future design matters. Do not infer automatic cross-combination where the final trait definition has not explicitly allowed it.

## Contributor evidence and choice authorization

Each contributor has a distinct occurrence identity (`contributorId`) and a selected Species/Ancestry lineage. Two Red Dragonborn contributors are **two separate sources** even though they supply equal values. Their identical candidate configurations can be recognized without duplicating the Breath Weapon grant.

The read-only `candidate_pool` reports:

- whether at least one contributor makes the trait's **configured candidate** available;
- the entire parameter configuration for each contributing lineage;
- each eligible value, its contributing occurrence(s), selected source, and native field source;
- missing or unbound parameter evidence, with the reason it could not be used;
- the trait's authored policy, its source-qualified definition occurrence, and its distinct mechanic identity.

The `validate_parameter_proposal` function checks a **hypothetical** proposed parameter selection against that pool. A positive result is explicitly `eligible-proposal-not-earned`. It does **not** claim the character inherited that ability, has paid its cost, or has permission to use any of its actions. Final manifestation, dependencies, authorization, and trusted grant projection are deferred until the wider Pillar 2 design is settled.

The test of two matching Red contributors establishes the *parameter-configurability* part of the required same-Species child reconstruction, not the entire character's mechanical equivalence.

## Parameter versus whole-trait boundaries

The only parameters exposed by this Breath Weapon design evidence are `damageType` and `area`. Action cost, saving throw, resource recharge, damage progression and other fixed rules remain part of the **one complete compound mechanic**. They can not be modified by supplying additional keys to a parameter proposal.

Parameter objects are structured: an area includes both shape and dimensions. Two dimensions of the same area object are **not automatically independently inheritable**. The final trait definition can later make a new authorized choice explicit if that is desired.

This separation prevents accidental creation of a feature that retains Breath Weapon damage but removes its Action cost, grants extra uses, or changes a saving throw with no permission.

## Failure and homebrew behavior

- A contributor who lacks the configured underlying trait contributes no parameter configurations for it.
- A contributor with a missing, unsupported or type-invalid source parameter contributes **no partial** configuration; the problem is reported.
- An unrecognized symbolic binding does not become an automatically available option.
- The evaluator uses no Dragonborn-name dispatch. A synthetic homebrew trait and its explicitly authored parameter bindings use the same generic mechanism in tests.
- Duplicate contributor occurrence IDs are rejected; equal selected Species are fine.
- A parameter proposal must supply exactly the declared parameter slots; extra cost, recharge, or activation overrides are rejected.

**Important limitation:** This proof does not address arbitrary mixed contributors who themselves possess new composite Heritage records. It presently inventories directly selectable native or synthetic source records; contributor-derived compound characteristics and further ancestry provenance require an explicitly designed, recursively composable representation.

## Commands

```bash
python3 tools/character_origins_parameter_inheritance.py \
  --contributors ancestry.dragonborn.red ancestry.dragonborn.bronze

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

## Next design milestone

Review how a **configured trait instance** can serve as the same reusable object for: (1) a direct standard Species preset, (2) a mixed-Heritage character, and (3) a homebrew Species. The shared evaluation and trusted manifestation authority must preserve resource/action constraints and all embedded effect rules. A candidate pool alone is not yet a gameplay evaluator or a balance model.

The current user decision does not authorize changing standard Dragonborn benefits, assigning point costs, selecting a default parameter policy for all traits, or merging/deploying any of these experimental artifacts.
