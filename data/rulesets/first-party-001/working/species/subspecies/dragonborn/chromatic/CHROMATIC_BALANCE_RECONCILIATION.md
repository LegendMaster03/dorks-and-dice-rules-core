# Chromatic Dragonborn — balance proposal for review (not approved)

This document collects a **proposed** first-pass Chromatic family and five-leaf package. It is not authoritative content. Only the **shared Dragonborn base Species** has been promoted to `native/species/base/dragonborn.json`. Its attack-replacement breath, PB/Long Rest uses, 1d10–4d10 scaling, Dexterity save, and 50% ancestry-element resistance are already selected and are **not under reconsideration here**.

## 1. Family mechanics — still awaiting explicit approval
- **Chromatic breath geometry:** 30-foot line, 5 feet wide, defined on nonselectable parent `subspecies.dragonborn.chromatic`. The individual dragon-type leaves supply their own element; damage/saving throw/uses come from the base Species. This matches *Fizban's Treasury of Dragons*' published Chromatic family. Older 5e cone geometry remains historical source evidence, not an automatic leaf override.
- **Chromatic Warding:** from character level 5, use an Action to gain immunity to the selected ancestry damage type for 1 minute; one use per Long Rest. When active, resistance reaches 100% to that damage type instead of adding 100 to the base 50% resistance. No immunity to associated conditions unless explicitly specified (for example, poison damage is not the Poisoned condition).
- **Not selected:** 5.5e universal Draconic Flight, flexible breath cone/line, or UA Chromatic Warding's earlier 10-minute profile. Do not automatically add them.

## 2. Recommended individual innate adaptations — review candidates

| Dragonborn | Source precedent | Initial mechanical candidate | Utility / limitation |
| --- | --- | --- | --- |
| Black | 3.5e Dragon Shaman, plus 5.5e Black Dragon Wyrmling's Amphibious | **Amphibious:** breathe air and water indefinitely. No automatic Swim Speed | Very useful around water; otherwise limited |
| Blue | 3.5e Dragon Shaman at-will Ventriloquism | **Displaced Voice:** at will, use an Action to make your vocal sounds appear to come from a point within 30 feet; no visuals, damage, automatic deception, or ability to impersonate others for free | Social/distraction utility can be broadly useful; precise illusion/discovery contract unresolved |
| Green | 3.5e Dragon Shaman, plus 5.5e Green Dragon Wyrmling's Amphibious | **Amphibious:** breathe air and water indefinitely. No automatic Swim Speed | Mechanically shares Black's adaptation, but differs in ancestry element; later differentiation may be worth testing |
| Red | 5e/5.5e Red Dragon Wyrmling's natural 30-foot Climb Speed | **Natural Climber:** Climb Speed 15 feet; does not confer Spider Climb, ceiling traversal, or automatic success on unusually difficult surfaces | Useful across more settings than aquatic/icy traits; limited to half walking speed |
| White | 5.5e White Dragon Wyrmling's Ice Walk + Swim Speed | **Icewalker:** travel and climb across icy surfaces without the ordinary climbing/check restriction; ice/snow Difficult Terrain imposes no extra movement. **Coldwater Swimmer:** Swim Speed 15 feet without water breathing | Two narrow/mode-specific abilities combine to offset lower everyday usefulness; explicit slippery-ice save interaction requires a ruling |

**No automatic extra dragon-type powers** are implied by these adaptations. A Swim Speed does not grant breathing underwater; a Climb Speed does not grant Spider Climb. These distinctions already exist in `native/spatial-rules.json`.

The source White Dragon's Ice Walk text excuses **ability checks** and ice/snow Difficult Terrain. The staged `hazard.slippery-ice` instead calls for a **Dexterity saving throw** against Prone. Whether White Dragonborn also bypass that save requires explicit approval; do not silently equate it with an ability check.

### Blue's alternative — desert/sand burrowing
The native Blue Dragon Wyrmling stat block includes a Burrow Speed of 15 feet. If at-will Displaced Voice proves too flexible, investigate a **restricted 10–15 foot burrowing mode in loose sand/earth** instead. Unlike general Burrow Speed, the restricted material specification is a new Dorks & Dice balance proposal, not sourced as an explicit limit from the monster stat block. Do not grant both sound magic and burrowing without further balancing.

### Red's alternative — smoke sight
A Red Dragon's climbing mode has a direct 5e/5.5e native monster precedent. Smoke Vision is a separate Pathfinder First Edition dragon feature; it is **not** established as a 3.5e Red Dragon trait and is not the leading recommendation.

## 3. Balance judgment — distinct from source facts
These are **exploration utility adaptations**. There is no defensible one-number power ranking without knowing play frequency:
- **High terrain independence:** Red's 15-foot climbing can matter in ruins, urban environments, caves, or wilderness; some surfaces require checks.
- **Versatile social/misdirection:** Blue's sound-only illusion may produce many opportunities, limited by the Action cost and need to deceive actual observers.
- **Environment-dependent but potentially transformative:** Black/Green breathing underwater can change entire underwater adventures without granting fast swimming.
- **Extremely environment-dependent without supplementation:** White Ice Walk is weak in most warm/dry locations; adding a 15-foot Swim Speed broadens its application while staying anchored in the true White Dragon's capabilities.
- **Element disparity already exists:** ancestry damage types and corresponding resistances are not equally common in every adventure. Equal counts of traits do not imply equal practical value.

The broad **Chromatic Warding** feature is common to all five and does not resolve their individual utility differences. Test these five before adding numeric compensations or universal innate flight.

## 4. Hoard themes are learned Upbringings, not compensation by misclassification
- The 3.5e Red Dragon Shaman's `+5 Appraise/Search` is a learned-seeming class ability, not an automatically hereditary Red Dragonborn trait. It belongs in future **treasure-appraisal cultural training** or optional feats after an independent budget review.
- `HOARD_TRADITIONS_RESEARCH.md` documents five possible learning traditions, informed by *Fizban's* Chapter 5. Other *Fizban's* dragon hoard profiles (Metallic, Gem, and the five additional draconic creatures) remain covered in `../HOARD_SOURCE_INDEX.md`.
- Favor skill and Competency IDs already in the native catalog (`skill.appraise`, `skill.search`, `skill.history`, `skill.nature`, `skill.survival`, `competency.gemcutting`) rather than creating unapproved new proficiencies.
- Upbringing defaults eventually belong in **Species/Subspecies JSON** as `defaultUpbringing`; the Upbringings inherit independently and are freely selectable by a character of any Species. No automatic forced hoarding or treasure tastes.

## 5. Decisions needed before promotion
1. Approve or revise the proposed **30-by-5-foot line** family breath geometry.
2. Approve or revise *Fizban's* **Chromatic Warding** (level 5 / Action / 1 minute / 1 per Long Rest / damage-type-only immunity).
3. Approve or revise the proposed five **innate adaptation profiles** in the table. White Swim Speed 15 and Red Climb Speed 15 are reductions from the true-dragon precedents, not published Dragonborn features. Blue Displaced Voice is an adaptation, not the full 3.5e Ventriloquism spell.
4. Specify whether **White Icewalker** removes the saving throw required by the `hazard.slippery-ice` source or only its ability checks and Difficult Terrain penalty.
5. Later: approve cultural Upbringings and skill/Competency benefit budgets separately, not as a backdoor to equalize physical traits.

**Do not promote this document's features to `native/` until the decisions are reviewed.** After approval, create the nonselectable Chromatic family record and five leaf Subspecies as coherent native definitions, then update working notes and tests.

## Sources and existing project anchors
- *Fizban's Treasury of Dragons* (2021), Chapter 1 Chromatic Dragonborn: https://www.dndbeyond.com/posts/1097-explore-the-chromatic-and-gem-dragonborn-from
- *Player's Handbook II* (2006), Dragon Shaman Draconic Adaptation (at 3rd Dragon Shaman level): https://www.dndtools.org/classes/dragon-shaman/
- SRD 2014 Red Dragon Wyrmling climb: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/monster-stat-blocks-r
- Current native true-dragon records: `native/bestiary/{black,blue,green,red,white}-dragon-wyrmling.json` (source creatures, not permission to inherit every ability).
- White slippery ice staged source: `working/hazards/slippery-ice.json`; shared movement definitions: `native/spatial-rules.json`.
- Shared Dragonborn approved baseline: `native/species/base/dragonborn.json`.
