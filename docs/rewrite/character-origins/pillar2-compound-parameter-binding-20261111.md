# Pillar 2 — compound characteristics and typed parameter-binding prototype

**Date:** 2026-11-11  
**Status:** Read-only experimental proof for design review. **Not** a new native inheritance policy, character evaluator, gameplay rule, or authorization surface.  
**Code:** `tools/character_origins_compound_traits.py`  
**Evidence contracts:** `pillar2-legacy-binding-evidence-20261111.json`  
**Tests:** `tests/test_character_origins_pillar2_compound_traits.py`

## Why traits must remain compound

A single activated feature may contain action costs, timing, range, targets, saving throws, resource pools, outcomes, and level-based changes. Those fields are **parts of the same authored ability**, not free-standing selectable inheritances. The source-facet prototype can inspect parts independently, but an eventual Character Origin evaluator must not let a player inherit Breath Weapon's damage without its activation, restrictions, and resource use.

This prototype preserves each `traits[]` object verbatim as one source-qualified unit with all of its field-level candidate semantic components. It does **not** split one ability into individually earned effects, combine similar traits, or determine eligibility to manifest the trait.

## Declarative bindings rather than Species-specific code

Existing Dragonborn rules use specialized strings such as `selected-dragonborn-leaf` and `selected-dragonborn-family-or-leaf`. The prototype defines their interpretation as **source-owned evidence declarations**, not code branches keyed to Dragonborn.

Each declaration identifies:

- An exact, source-qualified trait occurrence and nested field path.
- The **exact original authored reference** that must still be present.
- A generic lookup of a named field in the chosen source's parent chain, looking from the most specific selected node backward.
- An expected value shape (currently a nonempty string or object).

The resolver has **no Species-specific switch statement**. An independently authored homebrew source can use the same lookup mechanism with its own explicit declarations. The supplied fixture binds only the existing Dragonborn references; **unknown bindings remain unresolved**, rather than being silently interpreted or ignored.

The resolved values are retained as `parameterBindings` beside the **unchanged** `authoredDefinition`. The prototype does not mutate native content, rewrite a source definition, or claim that resolved parameters are complete evaluated mechanics.

### Selected Red Dragonborn

Source lineage:

`species.dragonborn` → `ancestry.dragonborn.chromatic` → `ancestry.dragonborn.red`

| Whole trait | Authored symbolic field | Resolved value | Selected source of value |
| --- | --- | --- | --- |
| Draconic Resistance | `damageResistance.damageTypeFrom` | `fire` | Red leaf |
| Ordinary Breath Weapon | `damage.typeFrom` | `fire` | Red leaf |
| Ordinary Breath Weapon | `areaFrom` | 30-foot × 5-foot line | Chromatic family |
| Chromatic Warding | `damageResistanceOverride.damageTypeFrom` | `fire` | Red leaf |

Breath Weapon retains its Attack-action replacement, proficiency-bonus recharge pool, Dexterity save, and 1/5/11/17 level-based damage progression as **one feature**. Chromatic Warding retains its level-5 gate, one-minute duration, and 100-percent replacement semantics instead of adding resistances.

### Selected Gold Dragonborn

Source lineage:

`species.dragonborn` → `ancestry.dragonborn.metallic` → `ancestry.dragonborn.gold`

The ordinary Breath Weapon binds to Fire damage and the Metallic family's 15-foot cone. The **additional Metallic Breath Weapon remains a separate compound feature**: Enervating Breath and Repulsion Breath share one Long-Rest use, independently of ordinary Breath Weapon uses. No free extra use or additional feat is inferred.

### Homebrew demonstration

Tests construct an entirely synthetic Species and selectable Ancestry with a compound wave ability. A declarative contract binds its damage type and area from the selected lineage using **the identical resolver**. This demonstrates source-independent binding for known property kinds; it does **not** establish that arbitrary homebrew prose is automatically executable or balanced.

## Validation and failure modes

The prototype verifies:

1. Every trait of every currently directly selectable standard lineage appears exactly once as a whole source unit.
2. The source-qualified identity and exact original definition survive unchanged.
3. Explicit lineage fields are resolved with declared field paths and value types.
4. Missing source properties, unexpected types, changed authored references, nonexistent contract paths, and missing binding declarations are reported unresolved.
5. New homebrew contracts require no additional Species-specific evaluator code.
6. The projection does not mutate the native catalog or its binding declarations.

The output always reports `evaluationStatus: not-evaluated`. A trait marked `parameters-bound` can still have prose-only restrictions, unknown nested effect semantics, or player choices. **Bound parameters are not proof of complete gameplay execution.**

## Usage

```bash
python3 tools/character_origins_compound_traits.py --selected ancestry.dragonborn.red
python3 tools/character_origins_compound_traits.py --selected ancestry.dragonborn.gold
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

## Next design work (not approved or implemented here)

1. Replace temporary legacy binding fixtures with first-class **typed characteristic data** that both native and homebrew definitions can author.
2. Define how compound effects map to existing common Rules Core mechanics without duplicating effect code or granting individual subcomponents.
3. Review prose-only and partially structured abilities for the semantics needed to evaluate a standard character.
4. Build a **complete, mechanically evaluated standard reconstruction** using the same pipeline planned for mixed Heritage, with no Species-specific case handling.
5. Only then design trait manifestation, duplicate interactions, contributor eligibility, and balance controls. Two parents of the same standard Species must be able to produce a mechanically standard child; this is an existence constraint, not a requirement to auto-grant both parents' packages.

Do not require a completed numeric balance model before functional Character Origins playtesting. The approved direct-selection Species/Ancestry corpus remains the reference, **not** proof that all current Species are equally powerful.

**No current Origin API, approved character state, or gameplay content is changed by this study.**
