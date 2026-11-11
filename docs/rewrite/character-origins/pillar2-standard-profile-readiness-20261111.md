# Pillar 2 — standard Species/Ancestry Origin-profile readiness

**Date:** 2026-11-11  
**Status:** Read-only source-reconstruction **proposal**, not approved Origin defaults, a final characteristic model, character-creation permission, or runtime execution.

**Tool:** `tools/character_origins_standard_preset_readiness.py`  
**Tests:** `tests/test_character_origins_pillar2_standard_preset_readiness.py`

## Question

Can the existing Pillar 2 candidate/manifestation model express a **standard directly selected Species/Ancestry** using the same characteristic selection shape as custom Heritage?

A valid comparison needs to include **all authored trait occurrences and intrinsic source fields**, not just the traits easiest to evaluate mechanically. It must also distinguish *an entitlement to make a choice* from *having made that choice*. A standard Human has Skillful and Versatile as existing first-party features, but the skill and feat selected by those features are not given merely by identifying the feature.

This audit uses the complete native parent lineage for each selectable Species/Ancestry; it creates **proposals**, not earned characters.

## Provisional procedure

1. Inspect a selected Species/Ancestry plus its inherited parents.
2. Use the existing `characteristic_candidate_pool` and explicit compound binding contracts. Retain every source-qualified trait occurrence and intrinsic dimension.
3. **Suggest** an intrinsic choice only if all applicable authored records agree on one value; preserve duplicate-source provenance instead of adding benefits. A lone `sizeChoice` becomes a genuine required choice, not a physical-size conflict.
4. Suggest a trait-entitlement selection for each fully modeled source trait, preserving its parameter values. Report embedded unresolved skill/feat/allocation/spell choices **separately**; the trait being present does not settle them.
5. Retain unknown fields and unmodeled symbolic bindings as source evidence and unresolved design items. A source field used only to bind a trait parameter does not independently grant a new trait.
6. Keep `defaultUpbringing` as a separate cultural suggestion, never an inherent trait.
7. Return `proposedSelections` in the **same selection shape** already supported by `draft_manifestation_profile`, with `templateComplete` indicating only whether the current evidence inventory found unresolved items.

**Important:** `templateComplete` is narrowly defined. It does not approve a finished playable character, prove balance or resolve overlapping effects; a standard selection requires agreed Origin rules and player choices. Even a source with no detected unresolved items has not been declared automatically earned.

## Representative evidence

| Direct standard selection | Proposable traits and intrinsic data | Explicitly unresolved |
| --- | --- | --- |
| **Human** | Humanoid type, 30-foot walking speed; Skillful, Versatile, Adaptable Learning | Small or Medium selection; Skillful skill choice; Versatile feat choice; skill-rank allocation |
| **High Elf** | Base Elf traits, Medium/Fey/30-foot intrinsic facts, Arcane Cantrip, Detect Magic, Misty Step entitlements | Wizard cantrip choice; choice of spellcasting ability from the ancestry field; prose-only trait semantics |
| **Wood Elf** | Base Elf traits and Wood Elf spell traits | Base 30-foot vs Wood 35-foot walk speed must be reconciled explicitly; spellcasting ability choice |
| **Red Dragonborn** | Dragon type, Medium size, walking/climbing speeds; base Breath Weapon, Draconic Resistance, and Chromatic Warding with their own source-bound parameters | No missing source-parameter binding in this current sample; cross-trait consistency between different damage-type choices for mixed Heritage requires a separate design decision |
| **Rock Gnome** | Gnome base traits and intrinsic properties plus Earthbound Tremorsense | No missing first-party source trait in the current prototype; Rock Gnome Upbringing remains separate |

### Dragonborn expansion is a content requirement, not a closed schema

Each Dragonborn ancestry is intended to have an authored **category, related skin material, damage type, complete Breath Weapon shape/dimensions, and hoard-related Upbringing**. More categories and leaves are planned, including damage types repeated across major categories. See the [Dragonborn expansion design record](pillar2-dragonborn-expansion-dimensions-20261111.md).

Today's standard-preset audit **can not infer skin material or hoard-related Upbringing for native records that do not author those associations**. Current Red and Bronze native leaves do not have a `defaultUpbringing`, and there is not yet an approved skin-material field. That is an identified future content/modeling gap, not a reason to infer culture from a contributor or collapse category and affinity into one identity.

### Completed source bindings: ordinary Dragonborn resistance and Chromatic Warding

The source binding contracts already carried `damageResistance.damageTypeFrom` and `damageResistanceOverride.damageTypeFrom`. The missing part was the **experimental Origin parameter registry**, which initially contained only ordinary Breath Weapon.

The registry now includes three separate **existing** trait definitions:

| Trait occurrence | Configurable source slot | Fixed behavior retained |
| --- | --- | --- |
| `species.dragonborn#trait:breath-weapon` | Damage type and whole area | Attack replacement, saves, damage scaling and uses |
| `species.dragonborn#trait:draconic-resistance` | Damage type | Authored 50% resistance |
| `ancestry.dragonborn.chromatic#trait:chromatic-warding` | Damage type | Authored 100% override, replacement policy, activation, duration and uses |

This lets a directly selected Red Dragonborn proposal record Fire for all three traits, while Bronze Dragonborn has resistance and Breath Weapon but **not** Chromatic Warding. It does not create a new inheritance rule requiring or permitting independent damage-type choices across the three traits in a mixed Heritage character. That is a **cross-trait compatibility question** for a later Pillar 2 decision. In particular, passing per-trait candidate validation alone must not be interpreted as approving an inconsistent full character package.

The two added single-parameter definitions currently use the existing `linked` policy, which is indistinguishable from `independent` for a single slot. This is a representation choice in a provisional fixture, **not a new global trait composition policy**.

### Explicit intrinsic choices without an automatic override rule

The standard-source proposal also supports a read-only **explicit choice preview**:

`preview_explicit_standard_intrinsic_choices(selected_source_id, chosen_values, ...)` re-derives the standard candidate pool, checks that a particular intrinsic dimension was unresolved, and accepts **only an explicitly offered value**. It records the exact source occurrence that offered the chosen value and leaves all unrelated choices pending. The resulting selections can be inspected through the same draft manifestation profile format as mixed Heritage, without granting or activating anything.

For **Wood Elf**, the base source records a 30-foot walk speed and the ancestry records 35 feet. Their relationship is now identified as `ancestor-descendant-authored-values`, with `defaultOverrideDecision: not-approved`. An explicit preview can choose either authored value, but the tool does **not** proclaim that standard Wood Elf characters should choose freely between 30 and 35 feet; **that is not the approved default**. We still need a design decision about whether the descendant value replaces its ancestor for a normal standard selection.

For **Human**, Small or Medium are author-written alternatives from one `sizeChoice`, not a parent-child replacement conflict. The same preview can select either, while keeping Human's Skillful proficiency, Versatile feat, and rank allocation choices unresolved.

The purpose is to prove that both situations use a common choice and provenance representation, while keeping the **standard preset policy**, **optional custom selection**, and **actual manifested character state** distinct. No implicit higher-value selection, value addition, or global child-overrides-parent rule is introduced.

### What this reveals

- **Source correctness is not the same as complete Origin correctness.** An entitlement can be recorded before its dependent selection is made. Missing symbolic bindings must not be silently ignored.
- **A standard ancestry is not necessarily an unrestricted bag of inherited parent traits.** The source lineage may contain values intended as an ancestry replacement (Wood Elf speed), but the general override rule has not been approved. For now the two source values remain visible and no automatic 35-foot selection occurs.
- **Do not invent a Species-specific creation path.** A proposed standard set uses the same intrinsic and trait selection keys as the existing mixed-Heritage prototype. Tests feed representative unambiguous proposal selections into that profile model, while keeping the resulting profile explicitly unauthorized.
- **Direct standard, mixed Heritage, and homebrew all remain possible.** The standing Dorks & Dice principle that tables may customize any subsystem is recorded in the [global design specification](../native-rules-design-specification.md). First-party reconstruction is a useful default, not a limitation on a DM-approved custom Origin.

## Outstanding Pillar 2 tasks

1. Audit any other missing trait parameter-binding definitions and retain unmodeled source parameters explicitly. Draconic Resistance and Chromatic Warding now have provisional mappings through the shared blueprint registry; this establishes no cross-trait dependency policy.
2. Decide the **default standard-selection intrinsic composition contract**, particularly whether a descendant's authored replacement value prevails over its parent. Explicit source-backed preview choices and same-value provenance are now representable, but this is not a decision about what default Wood Elf speed should be. Then decide how standard composition differs from mixed contributors.
3. Define **dependent choice contracts** for existing trait entitlements and linked spellcasting ability choices. The fact that a feature is manifested does not automatically select a skill, spell, feat or ability.
4. Define semantic characteristic identities and overlap rules, so duplicate and partially overlapping feature grants are not incorrectly stacked or collapsed.
5. Re-audit every selectable standard source against those rules, then evaluate proposed trait-budget and optional balance policies separately.

**Out of scope:** Executing combat or spell effects, condition resolution, Character Sheet integration, deploying a new Origin runtime model, or mandating that every homebrew Ancestry be reconstructible from a finite set of parents.

## Commands

```bash
python3 tools/character_origins_standard_preset_readiness.py --selected species.human
python3 tools/character_origins_standard_preset_readiness.py --selected ancestry.elf.high
python3 tools/character_origins_standard_preset_readiness.py --selected ancestry.dragonborn.red
python3 tools/character_origins_standard_preset_readiness.py --all

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```
