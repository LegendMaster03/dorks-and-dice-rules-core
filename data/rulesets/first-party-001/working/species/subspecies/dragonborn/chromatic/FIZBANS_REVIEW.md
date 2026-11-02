# Chromatic Dragonborn — family reconciliation research (not approved)

**Current first-pass scope:** Black, Blue, Green, Red, White. The common Dragonborn Species and Breath Weapon activation/damage/resource/save rules are approved in `native/species/base/dragonborn.json`; this file records only proposed family/individual traits and source comparisons. Do not interpret this file as an executable native rule.

## Family Breath Weapon geometry
- *Fizban's Treasury of Dragons* (published 5e, 2021) assigns **all Chromatic Dragonborn a 30-foot line that is 5 feet wide**, independent of their elemental ancestry.
- 5e SRD 5.1 differentiates shapes: Black and Blue use 30-foot lines; Green, Red, and White use 15-foot cones.
- 5.5e SRD 5.2.1 grants a flexible line-or-cone choice to Dragonborn generally.
- **Pending proposal:** set the Chromatic parent Subspecies' default geometry to a 30-foot, 5-foot-wide line, reflecting *Fizban's* family distinction. Do not make the 5e cone geometry automatic individual overrides; preserve them as candidate alternatives.

| Chromatic type | Selected elemental affinity | 5e SRD 5.1 geometry | *Fizban's* geometry |
| --- | --- | --- | --- |
| Black | Acid | Line | Line |
| Blue | Lightning | Line | Line |
| Green | Poison | Cone | Line |
| Red | Fire | Cone | Line |
| White | Cold | Cone | Line |

## Chromatic Warding — published version versus UA
- *Fizban's* published form: gained at **character level 5**, takes **an Action**, lasts **1 minute**, grants immunity to the Dragonborn's ancestry-linked damage type, and recharges **once per Long Rest**.
- The earlier Unearthed Arcana iteration differs materially: level 3, 10-minute duration. **Do not conflate UA with the published ability.**
- **Pending proposal:** use the *Fizban's* level-5, one-minute, Action, once/Long Rest profile. Treat the duration as an upgrade from baseline 50% elemental resistance to **100% immunity** for the same damage type, not a 150% sum; confirm against our native resistance/stacking contract when implementing.
- Damage-type immunity does **not** automatically imply immunity to elemental conditions. Poison damage and the Poisoned condition are distinct.
- The 5.5e general Draconic Flight gained at level 5 conflicts in design space with family-specific options; do not grant both automatically without a separate balancing decision.

## Candidate 3.5e Dragon Shaman totem adaptations — not Species traits yet
3.5e *Player's Handbook II* supplies an optional dragon-totem class feature at Dragon Shaman level 3. These are precedents to evaluate individually, **not pre-approved inherited Dragonborn abilities**.

| Ancestry | Dragon Shaman precedent | Review concern |
| --- | --- | --- |
| Black | Underwater breathing | Physiology candidate, but compare against other ancestry power budgets |
| Blue | Ventriloquism (magical voice displacement) | Innate magic versus learned technique; possible at-will power |
| Green | Underwater breathing | Water-related adaptation distinct from elemental poison resistance |
| Red | Strong appraisal and searching for valuables | Knowledge/training may fit Upbringing or feat better than biology |
| White | Movement across icy surfaces unhindered by ordinary slippery terrain | Potential innate physical adaptation; verify fit with existing terrain and condition rules |

The Dragon Shaman's class-granted auras, party healing, skill proficiencies, and alignment restrictions **must not** be copied into Dragonborn Species definitions.

## Unresolved decisions before promotion
1. Accept 30-by-5-foot **line** as the family breath default, or use ancestry-specific geometry?
2. Accept *Fizban's* Chromatic Warding exactly, or modify activation/duration/charges?
3. When reviewing each leaf, decide whether the historical dragon-specific adaptations are biological, learned Upbringing benefits, optional feats, or excluded.
4. Reconcile 5e Green Dragon poison affinity with 3.5e green dragon acid precedents without changing the approved shared ability arbitrarily.

## Sources
- SRD 5.1 and SRD 5.2.1: pinned reference records under `data/reference/srd/`.
- Official D&D Beyond published *Fizban's* overview: https://www.dndbeyond.com/posts/1097-explore-the-chromatic-and-gem-dragonborn-from
- *Fizban's Treasury of Dragons* (2021), Chapter 1: Chromatic Dragonborn.
- *Player's Handbook II* (2006), Dragon Shaman's Totem Dragon and Draconic Adaptation: https://srd.dndtools.org/srd/classes/basePhb2/dragonShaman.html
- *Unearthed Arcana: Draconic Options* (2021), early Chromatic Warding iteration (research only).
