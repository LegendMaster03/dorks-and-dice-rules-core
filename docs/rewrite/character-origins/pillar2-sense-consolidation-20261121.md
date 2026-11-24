# Pillar 2 — Shared innate senses and vision threshold

**Status:** Implemented content and read-only projection on the Pillar 2 integration branch. The initial values remain provisional and require player testing.

## Consolidated Tremorsense

Rock Gnome and Dwarf now reference one canonical trait, `trait.stonebound-tremorsense`. The selected native ability requires contact with stone, activates with a Bonus Action, has a 60-foot range and 10-minute duration, and can be activated Proficiency Bonus times per Long Rest. The former passive Earthbound version is not an additional native selectable ability.

A shared mechanical identity does not erase source provenance. The origin system can still distinguish an occurrence inherited from a Dwarf from an occurrence inherited from a Rock Gnome. Two sources are not two independent sense activations.

Previously saved passive Earthbound records require explicit migration/re-evaluation; replacing an ID does not preserve their former mechanics. Historical source-reconstruction comparisons are archived as such.

## Darkvision

A Darkvision range remains a numerical intrinsic. The native rule `rule.creature-origination.darkvisionSensitivity.innateRangeThresholdFeet` controls the point above which **total effective innate Darkvision** produces Sunlight Sensitivity. The initial threshold is **60 feet**, so exactly 60 feet has no derived drawback and 61 feet or more does. The value is deliberately configurable for playtesting.

The native Darkvision projector calculates the final range from verified native Darkvision and currently unlocked permanent native range enhancements. It reports `RequiredSunlightSensitivity` using the canonical `trait.sunlight-sensitivity` definition (direct, unprotected sunlight causes Dazzled; removing exposure or using effective protection ends it). The condition is one consequence regardless of how many contributing sources there are. Temporary magical vision is not included.

For example, ordinary Dwarf 60-foot Darkvision has no threshold drawback. A Dwarf with the level-four Cavernous Sight enhancement has 120 feet and consequently becomes sunlight-sensitive at that level. Cavernous Sight by itself grants 60 feet at its unlock and stays below the threshold. Drow 120-foot Darkvision exceeds the threshold. The Drow source-authored Sunlight Sensitivity requirement remains as a compatibility constraint, but should not be applied as a second condition.

This is a **projection**, not an automatic mutation of `InherentGrants` or an implementation of real-time condition application in every consumer. The host/Character Sheet should consume the threshold result for gameplay display and conditional penalties; a saved character without an explicit trait must not be assumed sunlight-safe solely because the separate trait is absent. A scoped waiver of the Drow source-local selection link does not by itself override the general threshold; the table may adopt an alternate threshold or effective rule.

## Creation allocation and heredity

Sunlight Sensitivity is a limitation, not a separately refundable purchase. Existing creation-budget costs remain provisional; price total capabilities, range-enhancement progressions, and their practical drawbacks together. The threshold may change during playtesting and must be re-evaluated alongside candidate point prices.

No new latent-gene capacity or multi-gene-to-one-trait expression behavior is introduced here. The proposed advanced genetics model remains a separate discussion.
