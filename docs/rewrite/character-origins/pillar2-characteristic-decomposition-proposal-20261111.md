# Character Origins — Pillar 2 characteristic decomposition and reconstruction

**Date:** 2026-11-11  
**Status:** Proposed design contract and read-only source-inventory proof; **not** an approved gameplay rule, new native character schema, executable mixed-Heritage resolution, or implementation authorization.  
**Corpus:** `data/rulesets/first-party-001/native/species/` (nine base Species, 29 Ancestries).  
**Companion evidence:** `tools/character_origins_source_inventory.py`, `tests/test_character_origins_pillar2_source_inventory.py`, [Pillars 1 and 3 readiness](pillars-1-and-3-native-readiness-20261110.md), and the [separate Pillar 1 audit PR #140](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/140).

## Owner-established goals and constraints

1. Standard Species and Ancestries **and** custom Heritage configurations must use the **same** authoritative mechanical characteristic system. A predefined standard package is a convenient selection preset, not a privileged Species-specific evaluator.
2. The standard Species selection path remains available to players who do not want to assemble a custom Heritage. Optional complexity must not require an additional ruleset for those players.
3. Two contributors of the same standard Species/Ancestry must **permit** a descendant with mechanically the same standard configuration. The descendant need not automatically acquire all contributed traits; contributor identity is not a grant.
4. Homebrew Species and Ancestries use the same definitions and processes as first-party content. An unknown effect may require new generalized rules or adjudication; the system must not guess from prose or silently discard it.
5. The existing Pillar 1 corpus provides initial **reference configurations**, not a presumption of equal strength. Trait synergy and numerical values require separate study and playtesting.
6. Heritage, origination, manifestation, and culturally learned Upbringing are distinct. Upbringing defaults are cultural suggestions rather than biological grants; do not rejoin Pillars 1 and 3 or infer biological parenthood for all characters.

These are design goals and boundaries. They do **not** approve a particular trait allocation, point cost, parent contributor limit, stacking rule, character schema, or automated import policy.

## Data-driven Species and Ancestry extensibility

**Owner design direction (2026-11-11):** The extra effort to model a Species and its characteristic relationships should be invested **once in reusable definitions**, so future Ancestries within that Species normally require **new content records**, rather than dedicated code or an inheritance-rule redesign for every leaf. Dragonborn is a stress test because additional categories and many additional Ancestries are planned.

The **universal Origin engine** must remain source-neutral: it should consume declarative Species, optional intermediate family/category records, Ancestries, trait/parameter contracts, constraints, manifestation, and provenance. Cultural Upbringing is separate from the inherent-characteristic system. A Species may supply a reusable set of characteristic dimensions and links; its Ancestries supply concrete values, distinct authored traits, and any explicit deviations. This is **not** a separate custom engine for each Species, nor a requirement that all Species share Dragonborn's three-factor standard-Ancestry formula.

**Normal extension acceptance:** A new standard Dragonborn Ancestry using existing mechanics should be definable through **category, category-related skin material, and damage type** by content authoring and validation alone. Its ordinary Breath Weapon area **usually derives from its category**, with explicit authored exceptions possible; it is not a fourth defining factor. Any cultural Upbringing belongs to a separate Origin subsystem and is not required to define the Dragonborn Ancestry. Existing standard-preset reconstruction and mixed-Heritage logic should process additional leaves without source-name switches, hard-coded enumerations, runtime code changes, or fixed corpus-count tests.

**Important qualification:** A new Species is one likely occasion for defining new characteristic relationships, but **not the only occasion for engineering work**. A newly added Ancestry can also introduce a genuinely new mechanic, dependency, or characteristic dimension that the current model does not support. Conversely, a new Species composed entirely of existing generic traits and rules should be possible through content alone. The correct implementation boundary is **new generalized capability versus new authored content**, not **new Species versus new Ancestry**.

Future acceptance tests should use **synthetic additional Ancestries** under one existing Species, including (a) an additional category, (b) two categories using the same damage type, (c) multiple types within one category, (d) distinct family-sourced abilities, and (e) a separate cultural suggestion. Test that existing resolution discovers them without source-ID conditionals or unintended grants. The fixtures should not change authoritative first-party content or imply every hypothetical combination is already approved for a manifested character.

## First slice: lossless native source inventory (implemented as tooling only)

The read-only tool:

- indexes both base Species and Ancestry records directly from native JSON;
- follows each **directly selectable** record's `parent` chain to a base Species, retaining intermediate, nonselectable family nodes;
- emits source-ordered `lineage` entries, each holding every native intrinsic field and every original trait definition **without normalization or interpretation**;
- assigns each trait an evidence identifier `<sourceId>#trait:<localTraitId>`, so coincidentally equal trait names/IDs do not erase their distinct definitions;
- excludes record/selection metadata and cultural `defaultUpbringing` suggestions from **intrinsic** fields, without changing the source data;
- detects nonexistent/unselectable selections, missing/cyclic parents, repeated local trait IDs, and malformed input.

The output's `evaluationStatus` is always `not-evaluated`. It is not a fully resolved character and is not an entitlement or authorization token. Source-qualified *evidence IDs* are not yet canonical universal *mechanical* trait IDs. When multiple contributors exist, future earned occurrences will also require verified contributor/source-acquisition provenance; this study does not supply that identity.

Usage from repository root:

```bash
python3 tools/character_origins_source_inventory.py --summary
python3 tools/character_origins_source_inventory.py --selected species.human
python3 tools/character_origins_source_inventory.py --selected ancestry.elf.high
python3 tools/character_origins_source_inventory.py --selected ancestry.dragonborn.red
```

The test file audits *every currently selectable source* rather than pinning an evolving record count. It verifies that the inventory retains the native intrinsic field values and exact local trait definitions; the explicit samples below verify meaningful source identity and progression details. This is **source conservation**, not proof of mechanical round-trip reconstruction.

## Three reference configurations

### Human: one direct source with independent choices

`species.human` contributes `creatureType: humanoid`, `walkingSpeedFeet: 30`, `sizeChoice` (Small or Medium), plus **three cumulative** source traits:

| Trait | Exact character entitlement retained from native content |
| --- | --- |
| `skillful` | Choose one additional Skill proficiency from the native Skill catalog. |
| `versatile` | Choose one Origin feat; future feat catalog/eligibility integration remains separate. |
| `adaptable-learning` | Allocate four extra Skill ranks at level 1 and one extra rank at each subsequent character level, within ordinary limits. |

An inventory must preserve the *unresolved* choice and rank-progression definitions, not select an arbitrary Skill, size, or feat. These are already selected Species traits even though they grant player choices.

### High Elf: base-plus-Ancestry composition

`ancestry.elf.high` has the source lineage `species.elf` → `ancestry.elf.high`.

The base Elf supplies Fey type, Medium size, 30-foot walk speed, Low-Light Vision, Keen Senses, Fey Ancestry, Trance, and magical sleep immunity. High Elf adds a Wizard cantrip choice at character level 1, Detect Magic at character level 3, Misty Step at character level 5, and the Intelligence/Wisdom/Charisma casting Ability choice. The base Elf `magical-sleep` feature is still prose-only; inventory preservation is not executable semantic interpretation.

`upbringing.elf.high` is merely a suggested independent cultural selection. It does **not** arrive as an intrinsic grant through this lineage.

### Red Dragonborn: three-level source chain and cross-source parameters

`ancestry.dragonborn.red` has lineage:
`species.dragonborn` → `ancestry.dragonborn.chromatic` → `ancestry.dragonborn.red`.

- The base Species supplies Dragon type, Medium size, 30-foot walk speed, Low-Light Vision, a damage resistance bound to the chosen leaf damage type, and the base Breath Weapon with its 1/5/11/17 character-level progression and Proficiency Bonus use pool.
- The Chromatic family supplies a 30-by-5-foot line geometry and Chromatic Warding at level 5, bound to the same chosen damage type. It is a nonselectable parent, not a second player selection.
- Red contributes Fire as the bound damage type, plus a 15-foot climbing speed and its constrained Natural Climber trait. The speed appears at both record and trait level today; it represents **one** capability, not two additive grants.

The **initial lossless source-inventory slice** intentionally did not resolve placeholders such as `selected-dragonborn-leaf` or `selected-dragonborn-family-or-leaf`. Subsequent read-only Pillar 2 source-binding and parameter-registry studies now resolve ordinary Breath Weapon, Draconic Resistance, and Chromatic Warding for standard Dragonborn examples, preserving their native authored definitions. This does **not** settle how distinct damage-type traits must interact when inherited from different contributors.

A second test case inventories Gold Dragonborn and preserves the Metallic family cone, separate Metallic Breath resource, choice of two breath modes, and Amphibious leaf trait.

## Proposed universal processing boundary

```text
Authoritative first-party or homebrew Species/Ancestry definitions
  |
  v
Source decomposition (lossless, provenance-preserving)   [first slice]
  |
  v
Characteristic definitions and parameter bindings         [future design]
  |
  +---- Standard preset choice ----+
  |                                |
  +---- Custom Heritage sources ---+--> Eligible candidates
                                       |
                                       v
                               Manifestation and conflict rules
                                       |
                                       v
                               Proposed manifested Origin profile
                                       |
                                       v
                               Future trusted Rules Core integration
                               [outside Pillar 2 design pass]
```

**Proposed distinction:**

- **Source / occurrence identity:** Who authored or contributed a grant, where it is recorded, and which selection authorized it.
- **Characteristic / semantic identity:** What the mechanical characteristic *means* independent of its source (e.g., a movement mode, a defense formula, or a spell-grant operation).
- **Parameters and conditions:** Its values, level requirements, recharge, scope, eligibility, and restrictions.
- **Composition behavior:** Whether multiple candidate effects are independent, overlap, replace one another, are mutually exclusive, or form an explicitly defined upgrade.

There is no approved universal registry schema or universal numeric budget yet. In particular, equal local IDs do not imply equal mechanics: Orc and Goliath `powerful-build` differ; Drow and Infernal Tiefling `darkness` are distinct *grants* of the same named spell.

## Reconstruction acceptance criteria (not yet implemented)

The eventual round-trip tests should perform the following against **each** selectable standard configuration:

1. Decompose the approved Species/Ancestry source chain into reusable mechanic definitions, preserving exact source provenance.
2. Bind every actual authorized choice and conditional parameter (including character-level progression), rather than assuming a single default.
3. Construct the standard configuration **using the same Origin trait-and-parameter selection model** used for mixed Heritage, without applying a separate Species-specific inheritance system. The standard selection path may supply a predefined package, but may not silently skip provenance, dependencies, or the approved characteristic structure.
4. Compare the **resulting Origin characteristics**: manifested trait identities, fixed behavior references, selected parameters, restrictions, conditions, progression definitions, and source provenance. This is an Origin-content equivalence test, **not** a requirement to implement attacks, saves, damage or any combat subsystem in Pillar 2.
5. Verify that matching contributors *can* produce the corresponding standard configuration when allowed to express its characteristics. This is an **existence** property, not a mandatory auto-grant or a requirement to copy two full packages.
6. Confirm that an independently authored homebrew Species can participate in the same pipeline without custom code keyed to its Species name.

Test at least the Human, High Elf, Red and Gold Dragonborn, and opposing variants of shared trait names before extending round-trip coverage to all records. Nonselectable source nodes may be inherited in valid chains but cannot be directly selected as characters.

## Explicit Pillar 2 design questions still open

- Which authored fields are **atomic mechanical characteristic definitions**, and which are parameters of a composite grant (e.g., a Dragonborn Breath Weapon)?
- How will the universal trait semantics represent currently prose-only abilities without silently weakening or adding mechanics?
- How are `level` and `requiredCharacterLevel` normalized into unambiguous character-level availability, distinct from a spell's level?
- How does an Ancestry alter or add to an inherited size, movement mode, or sense? Which effects replace, extend, coexist, or stack?
- Which characteristics are eligible to manifest from a contributor, and how are contributors that are mixed, created, nonbiological, or nonstandard modeled?
- What selection resources/limits apply, if any, when different traits have greatly different effective power (e.g., unrestricted flight)?
- How are compatible duplicate traits treated, including natural armor upgrades, without universal unreviewed stacking?
- What is the import/adjudication path when homebrew content contains mechanics outside the known semantic catalog?

These decisions must remain reviewable, with first-party and homebrew following the same general rules.

## Next implementation milestone

**Complete the Pillar 2 Origin model before runtime integration.** Define Heritage contributors (including mixed contributors), inheritability of traits and their parameter values, eligibility dependencies, manifestation versus mere source availability, conflict/duplicate resolution, and preservation of standard presets. The next proof should construct **Origin profiles**, not execute the mechanics those profiles refer to. Preserve source-inventory and configured-trait tools as design evidence; do not expose their outputs as authorized character state.

**Release boundary:** No changes in this branch to selected-native Species/Ancestry gameplay, Backgrounds, Upbringings, trusted Origin acquisition, Character Sheet, deployed API, or combat execution. Pillar 2 remains incomplete until the contributor, manifestation, parameter-composition, and Origin-profile reconstruction decisions are made and validated.
