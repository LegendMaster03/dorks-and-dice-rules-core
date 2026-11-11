# Character Origins — Pillar 1 characteristic consistency audit

**Date:** 2026-11-10  
**Scope:** Nine native base Species and 29 native Ancestry records, including nonselectable lineage/family nodes.  
**Purpose:** Prepare the reviewed Pillar 1 content for Pillar 2 decomposition, reconstruction, mixed-heritage composition, and later balance analysis.  
**Status:** One behavior-preserving representation correction; remaining schema and balance questions deliberately deferred to Pillar 2.

This audit does **not** introduce a trait-point budget, define genetic inheritance, authorize a mixed-heritage evaluator, or change approved Species/Ancestry benefits. Pillar 1's present selection fields remain provisional. The previously approved Pillars 1 and 3 content and cultural Upbringing separation remain intact.

## Applied data correction

- **Dwarf — Dwarven Resilience:** Changed `damageResistance: {"poison": 50}` to `damageResistance: {"percent": 50, "damageType": "poison"}`. This represents the same 50-percent Poison resistance as before, using the explicit shape already used by Abyssal Tiefling and other resistance traits. The poison-saving-throw benefit was not changed.
- Added `tests/test_character_origins_trait_shape_consistency.py` for explicit native resistance shape, agreement of existing mirrored movement speeds, and uniqueness of trait IDs **within** each source record. These checks do not assume global trait-ID uniqueness or require unsupported new Origin mechanics.

## Inventory of findings and dispositions

| Area | Current native evidence | Disposition |
| --- | --- | --- |
| Common characteristic placement | `creatureType`, `size` / `sizeChoice`, `walkingSpeedFeet`, `darkvisionFeet`, and some movement speeds occur at record level; other senses, movement, and feature effects occur in `traits`. | **Pillar 2 design.** Decomposition must recognize both forms before moving fields or imposing a uniform layout. |
| Shared trait IDs with **different** effects | Both Orc and Goliath use `powerful-build`: Orc covers carrying, pushing, dragging, and lifting; Goliath grants carrying benefits and advantage when ending Grappled. | **Pillar 2 design.** Do not identify universal effects solely by the unqualified local trait ID or silently make these benefits identical. |
| Shared trait IDs with different spell entitlements | Drow and Infernal Tiefling both use `darkness`, but the Tiefling version explicitly has `alwaysPrepared: true`, and the records have different spellcasting-ability sources. | **Pillar 2 design.** Separate the underlying spell identity from the grant, availability, and casting configuration. |
| Shared traits with matching structured effects | `low-light-vision` references the shared rule across its base Species; `amphibious` and `natural-climber` appear in multiple Dragonborn leaves with matching structured behavior. | **Retain.** Good candidates for reusable characteristic definitions, without assuming all shared IDs are equivalent. |
| Prose-only effects | Elf `magical-sleep`; Halfling `halfling-luck`, `brave`, `halfling-nimbleness`, and `naturally-stealthy`; Hill Dwarf `stability` have no structured effect payload beyond `id` and `description`. | **Pillar 2 design.** Preserve exact rules; introduce reusable effect definitions or explicit adjudication rather than inventing numeric values. |
| Level-gated traits | Some features use `requiredCharacterLevel`, while many innate spells use `level` (1/3/5) and some immediately available Gnome spells omit a level field. | **Pillar 2 design.** Make character-level availability distinct from spell level and the spell being granted; do not infer missing entitlements or silently change timing. |
| Mirrored movement speed | Red and Copper Dragonborn represent 15-foot climbing speed in both a top-level field and a trait; White does this for swimming, and Brass for burrowing. | **Retain and test parity now.** A future characteristic projector must treat these as one effective grant rather than adding two movement speeds. |
| Inherited movement/vision overrides | Wood Elf increases Elf walking speed from 30 to 35 feet. Drow gains 120-foot Darkvision despite base Elf having Low-Light Vision. Stone Goliath Cavernous Sight grants or extends Darkvision. | **Pillar 2 design.** Express override, addition, or distinct sense behavior explicitly; do not apply a generic summation rule. |
| Parameterized inherited effects | Dragonborn Breath Weapon derives damage type from a selected leaf and area from the family/leaf; Chromatic Warding derives the same damage type. Goliath abilities share a selected ancestry casting Ability. | **Pillar 2 design.** Support resolved parameters and shared choice bindings without Species-specific evaluation code. |
| Nonselectable nodes | Dragonborn and Goliath parent Species, Tiefling parent Species, Chromatic and Metallic Dragonborn families, and unfinished Gem Dragonborn are intentionally nonselectable. | **Retain.** These nodes can contribute inheritance/parameters without becoming direct player options. Gem remains incomplete. |
| Cultural separation | `defaultUpbringing` points to a recommended cultural package, not an intrinsic grant. Existing rules specify one effective Upbringing. | **Retain.** Heritage decomposition must not reintroduce culture as biological inheritance. |

## Pillar 2 acceptance requirements implied by this audit

1. **Fidelity:** Reconstruct a standard selectable Species and Ancestry with the same intrinsic characteristics, original choices, prerequisites, and level progression. Parent-family data are applied once.
2. **Source identity versus mechanical identity:** A feature's source-local `id` and its reusable effects are different concepts. Similar names do not imply interchangeable mechanical packages.
3. **No lost or double-granted effects:** Record-level fields and equivalent trait-level fields must project to one effective movement/sense/resistance result, while genuinely separate grants and variants remain distinct.
4. **Choice resolution:** Parameterized effects (Dragonborn damage/area, Goliath ability choice, innate spellcasting) resolve using the selected ancestry/heritage contribution, not hardcoded Species branches.
5. **Homebrew compatibility:** A new Species/Ancestry can use the same characteristic schema and grant projection. Unsupported mechanics are surfaced for review instead of being fabricated or ignored.
6. **Balance as a later layer:** Trait valuation and synergy analysis compare reconstructed standard and mixed configurations, without treating current untested Species strength as guaranteed equal.

## Out of scope for this pass

- Changes to Poison resistance strength, flight costs, natural armor stacking, Size rules, creature-type consequences, or any other approved trait effects.
- A universal point budget, dominance/recessiveness simulation, automatic inheritance of all parent traits, or rules forcing offspring of matching parents to inherit every trait.
- Character Sheet and Origin executor implementation; Pillar 2 has not yet finalized acquisition or manifestation rules.
- Altering Upbringings, Backgrounds, duplicate-Competency policies, or cultural benefits.

**Review outcome:** The current Pillar 1 content can remain the reference corpus for Pillar 2, provided decomposition treats the listed representational differences explicitly. Do not make a bulk schema migration until the universal characteristic and round-trip contract is reviewed.
