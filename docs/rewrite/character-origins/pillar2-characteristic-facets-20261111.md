# Character Origins — Pillar 2 provisional characteristic facets

**Date:** 2026-11-11  
**Status:** Read-only *design study*. Candidate facet identities and classification are **not approved native trait definitions**, inherited grants, or a rules engine.  
**Companion tooling:** `tools/character_origins_source_inventory.py` and `tools/character_origins_characteristic_facets.py`.

## Objective

Develop one reusable mechanical vocabulary for the entire first-party Species/Ancestry corpus and future homebrew, without implementing Species-specific evaluators or changing current traits.

The established source-inventory step faithfully retains base Species, optional Ancestry, and any nonselectable parents. The new provisional *facet projection* adds the **field-level mechanical dimension** that a property might affect, while retaining the exact authored property and its source-qualified occurrence. This is a deliberately narrower step than gameplay evaluation.

### Distinct identities

- **Source ID:** `species.orc`, `species.goliath`, etc.
- **Trait occurrence ID:** `species.orc#trait:powerful-build` — a source-qualified record of what was authored.
- **Candidate semantic facet:** `carrying.effective-size-categories` — a proposed shared mechanical dimension, **not** an executable grant or a promise that two whole traits are equivalent.
- **Facet parameter:** The exact authored value, e.g. `1`, retained for later interpretation.
- **Resolution status:** `mapped-candidate` identifies a recognized property; `unmapped` preserves unsupported properties for review. The enclosing projection is always `not-evaluated`.

The same `powerful-build` source-local ID has different contents in Orc and Goliath. The overlap on carrying capacity can be recognized without erasing the other Orc size-equivalence benefits or the Goliath's escape advantage. Similarly, Drow and Infernal Tiefling both have a `darkness` spell grant but differ in preparation and source context. Their common spell reference is **not** evidence that the full entitlements are identical.

## Generalized field classification (illustrative, not exhaustive)

| Source field | Provisional facet | Distinction |
| --- | --- | --- |
| `creatureType` | `creature.type` | Creature classification, not automatically a monster stat-block grant |
| `size` / `sizeChoice` | `creature.size` / `creature.size-choice` | Fixed physical size versus an unresolved selection |
| `walkingSpeedFeet` | `movement.walk.speed` | Character movement speed |
| `climbingSpeedFeet` | `movement.climb.speed` | May mirror a trait-level movement grant; never add both blindly |
| `darkvisionFeet` | `sense.darkvision.range` | Darkvision distinct from Low-Light Vision |
| `damageResistance` | `defense.damage-resistance` | Retains specific damage type, amount, conditions and scope |
| `spell` / `spellList` | `spellcasting.spell-reference` / `spellcasting.spell-list-reference` | Spell identity/list only; not permission to cast |
| `level` | `ability.level-as-authored` | Intentionally **not yet** declared to mean character level universally |
| `requiredCharacterLevel` | `ability.minimum-character-level` | Explicit requirement at a character level |
| `uses` / `activation` | `ability.uses` / `ability.activation` | Retains use pool, recharge, and action cost without evaluation |
| `damageType` | `parameter.damage-type` | Dragonborn leaves bind inherited effects in existing content |

Candidate labels are field-specific. They do not collapse adjacent fields into an approved action/trait schema; compound abilities still require semantic modeling of triggers, resources, consequences, and dependencies together. The tool does **not** use Species names or trait IDs to dispatch a particular mechanic.

## Observed provisional diagnostics

### Prose-only effects

Several existing source traits contain no machine-readable mechanical fields beyond `id`, `name`, and `description`. Examples include Elf Magical Sleep, Halfling Luck/Brave/Nimbleness/Naturally Stealthy, and Hill Dwarf Stability. The facet inventory marks these `prose-only-effect`; it does not interpret their prose or discard their source descriptions. These require generalized effect definitions or explicit adjudication before **evaluated** standard reconstruction is possible.

**Important:** The presence of structured fields does not guarantee that a trait is already fully executable. Some restrictions, exceptions, and consequences still reside in prose; that semantic completeness review is a later step.

### Duplicate descriptions of movement

Red/Copper Dragonborn Climb Speed, White Swim Speed, and Brass Burrow Speed are currently present both at the record level and inside an individual movement trait. The tool reports matching mirror values, or conflicting values if an introduced change diverges. It does not award duplicate speed or decide which entry becomes canonical in a future schema.

### Unbound parameters

The source retains `damageTypeFrom`, `typeFrom`, `areaFrom`, and `spellcastingAbilityFrom` expressions. For example, the Dragonborn Breath Weapon must obtain damage type from the selected leaf and area geometry from the selected family/leaf. The tool reports symbolic references as `unbound`. It does not hardcode Dragonborn-specific resolution.

### Unknown homebrew fields

A field unknown to the provisional registry becomes `unmapped` and retains its original value, even if it appears in a registered Species or Ancestry. A homebrew definition must not become silently weaker simply because a converter does not yet recognize one of its fields. Recognizing a new field's semantics should be an extensible general rule, not a new `if species == ...` branch.

## Inspection commands

From the repository root:

```bash
python3 tools/character_origins_characteristic_facets.py --selected species.human
python3 tools/character_origins_characteristic_facets.py --selected ancestry.elf.high
python3 tools/character_origins_characteristic_facets.py --selected ancestry.dragonborn.red
python3 tools/character_origins_characteristic_facets.py --shared-ids
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

## Acceptance checks of this slice

- The tool retains **every** original intrinsic field and every non-metadata trait field in the selected source chain, including unfamiliar and nested values.
- Direct standard selection and nonselectable intermediate ancestry inheritance are still represented without duplicating the ancestor's source record.
- Candidate common components can be identified across different source-qualified traits without treating whole traits as identical.
- Unexpected source fields remain explicit and unresolved.
- Unbound source parameters and mirrored movement fields are visible diagnostics, not silently evaluated grants.
- No authoring changes are made to native Species, Ancestry, Background, or Upbringing records.

## Remaining decisions before a universal evaluator

1. **Atomic effects versus composite abilities:** Determine how activated abilities with multiple conditions, resources, saving throws, and effects become reusable characteristic definitions.
2. **Semantic completeness:** Translate existing prose-only traits and prose-only clauses on partly structured traits into approved, source-independent mechanics without changing their behavior.
3. **Binding vocabulary:** Replace specialized strings such as `selected-dragonborn-leaf` with general, typed bindings to selected contributor properties and choices.
4. **Inheritance/override:** Decide how base and leaf traits interact, including duplicates, mechanical upgrades, movement/vision replacements, and source-specified exclusions.
5. **Reconstruction:** Build an actual **evaluated** standard-character round trip that compares mechanics, not only source fields. Standard presets and custom Heritage must use the same evaluator.
6. **Homebrew and balance:** Introduce generic authoring/adjudication support, then study trait values and interactions against the original standard configurations. Numeric allocations are still unresolved.

**Design boundary:** This stage does not determine how many ancestry contributions may manifest, how many trait choices are allowed, whether a duplicated trait upgrades another, or how many points flight should cost. Those remain deliberate Pillar 2 game-design decisions.
