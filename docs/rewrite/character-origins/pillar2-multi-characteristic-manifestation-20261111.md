# Pillar 2 — multiple traits and intrinsic characteristic candidates

**Date:** 2026-11-11  
**Status:** Read-only design study, **not** a finalized inheritance policy, approved character schema, Species reconstruction, Origin API, or gameplay implementation.

**Code:** `tools/character_origins_manifestation_inventory.py`  
**Tests:** `tests/test_character_origins_pillar2_manifestation_inventory.py`  
**Prior study:** [Nested Heritage](pillar2-nested-heritage-manifestation-20261111.md)

## Objective

Expand the single configurable Breath Weapon example into a representation of a character's **multiple inherent characteristics**, including the complete source traits and intrinsic dimensions of creature type, size, walking speed, movement modes, senses and needs.

This prototype does not implement any characteristic's effect in combat, spellcasting, movement, or other gameplay. It represents **what characteristics a character could express, what was explicitly selected, and which sources supplied them**.

It does not assume that the mere existence of a contributor confers all that contributor's Species traits. Mixed-contributor profiles in the experiment offer only the values they actually manifest; **latent or unmanifested transmissible characteristics remain a separate open design choice**.

## Candidate classification

A directly selectable Species or Ancestry resolves its authored source lineage. Every source trait and intrinsic field is then either presented as an explicit candidate or preserved in the deferred-evidence inventory for later classification.

| Input | Candidate identity | Treatment in this study |
| --- | --- | --- |
| `creatureType` | `creature.type` | A proposed intrinsic value, not a monster stat block |
| `size` | `creature.size` | Fixed-size source value |
| `sizeChoice.from` | `creature.size` | Multiple **choices**, not a creature simultaneously manifesting all listed sizes |
| `walkingSpeedFeet` | `movement.walk.speed` | Physical movement value; not summed when duplicate |
| `climbingSpeedFeet`, `swimmingSpeedFeet`, `burrowingSpeedFeet` | Matching movement modes | Separate attributes for review alongside authored movement traits |
| `darkvisionFeet` | `sense.darkvision.range` | Separate from the Low-Light Vision trait |
| `needs` | `creature.needs` | Whole authored requirement object, not independently chosen subfields |
| Source traits | Source-qualified `<source>#trait:<id>` | A complete source ability record; same local name from different sources is **not** assumed equivalent |
| `damageType`, `breathWeaponArea` | Context values only | Source data for a configured trait's parameters, **not** independent physical grants |
| `spellcastingAbilityChoice`, `ancestrySelection` | Choice/control evidence only | Requires a dependent choice or source-selection contract; **not** a free standalone characteristic |
| Unknown root fields | Explicit deferred evidence | Never silently dropped or interpreted as an inheritable ability |

The only currently configured compound mechanic in the experimental registry is ordinary Dragonborn Breath Weapon. Its parameter slots remain damage type and **complete area**. Its fixed cost, uses, and progression remain inside the unchanged trait record. A source trait with unmodeled symbolic bindings, such as Draconic Resistance, is **deferred** rather than guessed or silently granted.

Prose-only traits, including Halfling Luck and Elf Magical Sleep, can be recorded as authored traits for Origin review with a `prose-only-effect` flag. That is not executable semantic completion.

## Candidate pool versus manifestation

`characteristic_candidate_pool` collects all source occurrences from the listed contributors and reports conflicting or unclassified values. It grants **nothing**.

`draft_manifestation_profile` accepts an **explicit list of requested intrinsic values and source-qualified trait occurrences**. It verifies each choice against available offers, rejects duplicate selections of the same identity and values not offered by any contributor, and records source provenance.

A draft with no selected traits remains empty even if every contributor has many available traits. It does not silently acquire a complete Species package.

For parameterized traits, the user-approved ordinary Dragonborn Breath Weapon example permits independent selection among the damage-type and whole-area values supplied by contributors. A trait explicitly configured as `linked` still requires the complete offered parameter combination. Other traits do not acquire this recombination behavior by default.

## Conflicts retained for design review — not resolved

**Wood Elf walking speed:** `species.elf` contributes 30 feet, while `ancestry.elf.wood` contributes 35 feet. The candidate inventory retains both and emits `multiple-source-values-needs-origin-policy`. It does not automatically choose the leaf or add the speeds. An explicit choice of 35 is representable, but approval of a generalized replacement rule requires further review.

**Elf versus Drow senses:** The base Elf's authored Low-Light Vision trait and the Drow's 120-foot Darkvision are retained independently. The inventory emits `senses-interaction-needs-review` without deciding coexistence, replacement, or stacking.

**Same local trait name:** Orc and Goliath both define `powerful-build` with different additional mechanics. They remain separate source-qualified candidates until their semantic relationship is deliberately reconciled.

**Physical size:** Human Small/Medium `sizeChoice` yields two eligible options. Neither option automatically manifests, and `sizeChoice` itself is not treated as an additional size.

**Movement mirrored by a trait:** A climbing speed at the ancestry root and a Natural Climber trait may describe the same capability. The generic profile does not assume they are additive, but this prototype has not normalized their overlap into one canonical characteristic. A separate conflict/overlap pass is needed.

## Nested mixed contributors

The experimental multi-characteristic profile includes:

- A profile ID and explicit, independent origination context.
- Contributor occurrences referring either to selected native sources or earlier draft profiles.
- Explicit manifestation selections and the corresponding validated trait/attribute records.
- Source-qualification and parameter provenance, without selecting a single arbitrary parent when several supply the same value.
- Review flags and a `not-authorized` status.

When a second-generation character references a draft mixed profile, it sees **only that profile's manifested intrinsic values and complete traits/parameters**. The prototype recursively recalculates the parent profile from its recorded contributors and selections, rejecting modified or unoffered values, missing profiles, and cyclic references.

This is an **internal design-consistency check**, not a security or trusted rules-authorization guarantee. Any caller still supplies catalog/registry/policy test data.

## What this demonstrates

- Human direct selection can explicitly represent Humanoid type, a size choice, 30-foot walk speed, and **all three distinct Human trait entitlements** in one profile; those traits' own skill/feat choices are still unresolved until other Pillar 2/common-character systems are designed.
- Two Human contributor occurrences can offer the same potential standard configuration without duplicating grants.
- A High Elf source chain preserves base Elf and Ancestry trait occurrences, rather than dropping parent definitions.
- A mixed Red/Bronze Dragonborn contributor that manifests only Fire + Cone can pass that one configured trait to a subsequent Origin without also transferring unmanifested Lightning + Line alternatives.
- A character can explicitly manifest traits from different contributors, retain their independent source identities, and decline eligible traits.
- The source audit counts trait/field records against direct candidates **or explicitly deferred evidence** for every selectable native record, without relying on a fixed record count.

## Remaining Pillar 2 decisions

1. **Actual characteristic identity:** Source-qualified trait occurrence IDs are evidence IDs, not reusable mechanic IDs. Reconcile equivalent mechanics and overlapping subtraits without collapsing different effects.
2. **Intrinsic conflict policies:** Which physical properties are exclusive, overridden, additive or coexisting? Standard presets must select one authoritative effective outcome where appropriate.
3. **Dependent choices and parameter sources:** Size choices, spellcasting ability choices, trait parameters and conditionally available traits need explicit dependency/correlation definitions.
4. **Complete standard profiles:** Construct all traits and intrinsic properties for Human, High Elf, Red/Gold Dragonborn, and the rest of the corpus through one profile format. This requires handling deferred symbolic parameters, not inventing their meanings.
5. **Heritage transmission:** Determine whether and how latent-but-transmissible characteristics can skip expression; do not settle genetics through code defaults.
6. **Eligibility versus manifestation limits:** No default universal trait budget, upgrade policy, or stacking rule has been approved; keep playtest options explicit.

**Out of scope:** Combat, spellcasting, conditions, runtime character state, Character Sheet UI, and any execution of the captured mechanics.

## Inspection

```bash
python3 tools/character_origins_manifestation_inventory.py
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```
