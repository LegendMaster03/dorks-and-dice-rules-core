# Chromatic hoard traditions and individual ancestry balancing — research only

**Status: proposal, not approved.** The five Chromatic Dragonborn Subspecies have not been promoted to native. Do not create Upbringing defaults, ability grants or playable leaves from these notes until reconciled. Family Breath Weapon geometry and Chromatic Warding likewise remain unresolved. The shared Dragonborn Species definition is already selected in `native/species/base/dragonborn.json`.

## Goal: equivalent kinds of benefits in both pillars
A 3.5e *Dragon Shaman* totem provides four conspicuously innate adaptations and one (Red) Appraise/Search training bonus. Simply moving Red's bonus into Upbringing while the other four retain their innate adaptations could leave Red with less innate utility. Investigate **one innate adaptation for Red** and **hoard-inspired learned traditions for all five**, without assuming biological inheritance imposes greed, hoarding behavior, alignment, or culture.

| Chromatic type | Inherited adaptation candidate, not approved | *Fizban's* dragon hoard motif | Learned Upbringing focus candidate |
| --- | --- | --- | --- |
| Black | Water breathing (3.5e Dragon Shaman) | Antiquities and artifacts from lost cultures, particularly opals, durable stone/metal relics | `skill.history` / `skill.search` for ruins, cultures and relic provenance |
| Blue | At-will Ventriloquism (3.5e Dragon Shaman) | Carefully curated jewelry and colored gemstones; artwork and weather-themed objects | `competency.gemcutting` / `skill.appraise` for jewelry and gemstone assessment |
| Green | Water breathing (3.5e Dragon Shaman) | Green and forest-colored gems, botanical art, treasures integrated with cultivated plants | `skill.nature` / art-object identification and horticultural training |
| Red | **Proposed 15-foot climbing speed** from the 5e true Red Dragon's natural climbing precedent | Gold, durable metal/stone treasures, rubies, fire opals and trophies of personal achievement | `skill.appraise` / `skill.search` for valuables and precious metals; possibly gem-related Competency |
| White | Icewalker (3.5e Dragon Shaman) | Battle trophies, recovered weapons, trophies of victory, silver and ice-colored gems | `skill.survival` for trophy tracking / `skill.history` or `skill.appraise` for weapon and trophy identification |

These historical dragon **hoard themes are descriptive source material**, not the motivation or instincts of every Dragonborn with matching scales. A character of any Species/Subspecies could select an Upbringing developed in a community with those traditions.

## Proposed common Upbringing structure
- A future **General Dragonborn Upbringing** may contain shared cultural training (e.g., Draconic language), but its exact definition has not been reviewed.
- Five optional Chromatic-inspired Upbringings can independently inherit the General Dragonborn Upbringing if approved. **One Upbringing selection** resolves its own ancestors. An ancestry can provide a `defaultUpbringing` reference after individual approval, without restricting who may choose it.
- To avoid unequal starting benefits, each cultural specialization should use a *comparable training budget* (e.g., one appropriate native Skill or Competency choice plus an appropriately narrow conditional bonus, rather than Red alone receiving +5 to two broadly useful skills). Precise grants, bonuses, stacking, and choices remain unresolved.
- Do not turn favorite-hoard categories into compulsory possessions, greed, treasure detection, monetary rewards, or background alignment.
- Dragon Shaman's `+5` was a **class ability**, not already balanced as an Upbringing grant. Do not import it unchanged.

## Red Dragon biological candidate
5e Red Dragons, including wyrmlings, have a climbing speed. A **15-foot climbing speed** is a starting design candidate: climbing uses that speed on appropriate surfaces; exceptionally smooth or difficult climbs may still require checks. Distinguish this from Spider Climb (including ceilings) and from lava/heat immunity, which it does not grant. Its usefulness depends on map terrain and should be compared with passive water breathing, environmental movement, and at-will Ventriloquism.

**Secondary candidate:** Pathfinder First Edition Red Dragons possess Smoke Vision; Pathfinder is a distinct source, not evidence of a 3.5e D&D Red Dragon feature. Smoke Vision may be less broadly useful than the climbing candidate and should not stack by default.

## Open balance and system questions
1. Does a 15-foot climbing speed match the other four Chromatic adaptation choices well enough? Compare actual encounter, exploration, action-economy and environmental relevance, not number of trait entries.
2. Does Blue's at-will Ventriloquism merit casting-time or use restrictions? Does Green/Black indefinite water breathing confer excessive advantage in aquatic campaigns? Are White's icy-terrain benefits too narrow?
3. Does the catalog's existing `skill.appraise` and `competency.gemcutting` provide sufficient mechanically distinct, non-duplicative learning choices?
4. Should the cultural variants be organized by draconic hoard tradition rather than named as if biological ancestry determines culture? Their `defaultUpbringing` association can be set separately.
5. What are the underlying General Dragonborn Upbringing grants? This must be reconciled prior to declaring five finished variants.

## Sources
- *Fizban's Treasury of Dragons* (2021), Draconomicon, individual Black, Blue, Green, Red and White Dragon treasure entries; published D&D Beyond landing page: https://www.dndbeyond.com/sources/dnd/ftod
- *Player's Handbook II* (2006), Dragon Shaman, Draconic Adaptation: https://drinkinganddragons.com/d20/srd/classes/dragonShaman.htm
- 5e SRD, true Red Dragons (natural climbing speeds): https://dnd5e.info/monsters/monster/dragon-red-adult/
- Pathfinder First Edition Red Dragon, Smoke Vision (distinct rules source): https://www.d20pfsrd.com/bestiary/monster-listings/dragons/dragon/chromatic-red/adult-red-dragon
- Native Skills and Competencies: `data/rulesets/first-party-001/native/skills.json` and `data/rulesets/first-party-001/native/competencies.json`.

**No mechanics or cultural defaults in this document are approved.**
