# Pillar 2 — Gnome + Dwarf compared with Rock Gnome

**Date:** 2026-11-11  
**Purpose:** Test how closely two directly selectable Species source records can reconstruct a third, standard Ancestry's intrinsic characteristics, **without** inferring traits or changing any current rule.

**Target:** `ancestry.gnome.rock` (Rock Gnome, including its parent `species.gnome`)  
**Contributors:** `species.gnome` + `species.dwarf`  
**Tool:** `tools/character_origins_target_reconstruction.py`  
**Tests:** `tests/test_character_origins_pillar2_target_reconstruction.py`

## Target characteristics versus available sources

| Target Rock Gnome characteristic | Contributor source evidence | Result |
| --- | --- | --- |
| Fey creature type | Gnome `creatureType: fey` | Exact candidate |
| Small size | Gnome `size: small` | Exact candidate |
| 25-foot walking speed | Both Gnome and Dwarf `walkingSpeedFeet: 25` | Exact candidate, two independent sources |
| Low-Light Vision | Gnome `low-light-vision`, `rule.low-light-vision` | Exact source-qualified trait candidate |
| Gnomish Cunning | Gnome `gnomish-cunning` | Exact source-qualified trait candidate |
| Earthbound Tremorsense | Dwarf supplies **Stonebound Tremorsense** instead | Related sense type, **not equivalent** |

**Exact source-candidate coverage: 5 of 6 modeled target characteristics.** This is not a gameplay power rating, not a measure of balanced Origin points, and not proof of six effective character benefits. It is a count of modeled source facts and trait identities that can be sourced without inventing grants. The five exact candidates are already available from **Gnome alone**. Adding Dwarf does **not** increase exact coverage; it contributes a similar Tremorsense mechanic that warrants comparison.

## The decisive sixth characteristic

| Constraint | Rock Gnome: Earthbound Tremorsense | Dwarf: Stonebound Tremorsense |
| --- | --- | --- |
| Sense kind | Tremorsense | Tremorsense |
| Range | 15 feet | 60 feet |
| Activation | Continuously passive | Bonus Action |
| Contact | Both character and target must touch connected earth or stone | Character must be in contact with stone |
| Duration | Ongoing while requirements apply | 10 minutes per activation |
| Uses | No limited-use counter defined | Proficiency Bonus uses per Long Rest |

The two traits share `sense: tremorsense` but are **not** the same complete grant. Neither the increased range of the Dwarf's sense nor its use of the same sense type supplies the Rock Gnome's passive activation, ongoing duration, or connected-earth contact rule. A shorter range should not be treated as automatically exchanging for passive operation; that is a new design/balance decision.

A future generic Tremorsense mechanic could support parameters such as range, activation, required contacts, duration, and usage restriction. **Parameter support does not imply every parameter value is inheritable from every contributor.** Gnome + Dwarf, as currently authored, do not supply the Rock variant's exact parameter values or complete definition.

A direct Rock Gnome source *does* supply that original trait; comparing Rock Gnome with itself yields all six modeled characteristics.

## Design example: distinct traits arising through ancestral divergence

**Narrative hypothesis, not established Rock Gnome canon:** Rock Gnomes might descend from a population with both Gnome and Dwarf ancestry. Over many generations, their Tremorsense might have developed into a distinct, shorter-range passive sense. The current Gnome and Dwarf definitions need **not** combine to produce that trait in a first-generation mixed character. The shared Tremorsense family makes this a plausible example of mechanical storytelling, not proof of a particular biological history.

The example distinguishes **four questions** that the Pillar 2 rules must not collapse:

1. **Possible historical origin:** Which populations might an Ancestry have descended from? Narrative worldbuilding, potentially uncertain, and not itself an eligibility rule.
2. **Shared mechanical family:** Are two distinct traits implementations or variants of a general capability (here, Tremorsense)? This is a content-authoring relationship, not proof of grant equivalence.
3. **Immediate Heritage eligibility:** Which traits and parameter values are actually available from the character's direct contributors? The Dwarf's Stonebound Tremorsense does not independently offer Rock Gnome's passive Earthbound configuration.
4. **Manifested characteristic:** What trait does the character actually possess? A Rock Gnome can manifest its established Earthbound Tremorsense from a Rock Gnome source regardless of whether distant population history is known.

This is a useful **regression example for the future rule**: a related descendant trait must be expressible as an established, distinct trait variant, without requiring an exhaustive reproduction of all historical generations or pretending that any combination of its distant ancestral traits automatically produces it. Conversely, describing a possible historical origin should **never** cause a first-generation Gnome + Dwarf character to automatically receive Earthbound Tremorsense.

A general variant/derivation relationship may become useful documentation for a canonical characteristic registry. Whether any such relationship ever has selectable mechanics is a **separate explicit rules decision**, and no inheritance-by-divergence or genetics simulation is approved here.

**Disposition:** Retain both Tremorsense definitions unchanged. Treat the hypothetical interbreeding history as optional narrative evidence for future design, not a new Rock Gnome origin rule.

## Additional contributor facts and potential conflicts

The Dwarf provides alternatives that are **not** part of the standard Rock Gnome target: Humanoid instead of Fey, Medium instead of Small, 60-foot Darkvision alongside Low-Light Vision, Dwarven Toughness, Dwarven Resilience and Stonebound Tremorsense. This could form a distinct custom Heritage; no automatic replacement, summation, or simultaneous size/type assignment is authorized.

Gnome and Dwarf both provide 25-foot walking speed, which does not double into 50 feet.

The Rock Gnome ancestry's `defaultUpbringing: upbringing.gnome.rock` is **a cultural suggestion, not an inherited trait**. Rock Gnome Upbringing grants separate Tinkering and other cultural/learned benefits. Selecting a Rock Gnome Upbringing is an independent Pillar 3 decision; it is excluded from the six inherent characteristic comparisons in this test.

## Test assertion and interpretation

The tests explicitly verify that:

- Gnome alone matches five of six inherent target records.
- Gnome + Dwarf matches five of six and detects the Dwarf's related, non-equivalent Tremorsense.
- Dwarf alone matches only the target's 25-foot walk speed.
- Rock Gnome itself matches all six source records.
- A draft Gnome + Dwarf Origin may explicitly manifest the five exact candidate characteristics, but **can not** manifest `earthbound-tremorsense` as though it came from either source.
- Even copying the Earthbound trait's fields onto the Dwarf's differently identified source trait does not silently declare source-trait equivalence.
- Dwarf's extra candidates remain separate and conflicting intrinsic values stay visible; Upbringing remains separate.
- The comparison does not mutate native content, grant traits, or require combat/vision/sense execution.

This is an intentionally conservative **source reconstruction** test. It proves neither that the completed Origin balance is appropriate nor that a player should always be allowed to select all available traits.

## Pillar 2 design implication

The missing sixth trait is evidence that we need **explicit semantic trait identities and parameter-level provenance**, not automatic conversion based on a matching category name.

If the goal is to make Gnome + Dwarf reconstruct Earthbound Tremorsense exactly, that requires an additional approved rule or content source: for example, an explicitly authored derived-trait/recombination recipe. It can not be justified simply by assuming that the Dwarf's 60-foot activated sense subsumes a passive 15-foot sense.

This comparison should remain a regression fixture when the canonical trait registry and overlap/compatibility policies are designed. It does not implement or prescribe those future policies.

## Run

```bash
python3 tools/character_origins_target_reconstruction.py \
  --target ancestry.gnome.rock \
  --contributors species.gnome species.dwarf

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_target_reconstruction.py' -v
```
