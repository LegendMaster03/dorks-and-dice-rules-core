# Chromatic reconciliation — decisions and remaining balance questions

The Chromatic family and its five individual innate adaptations were approved and promoted to **authoritative** files in `native/species/subspecies/dragonborn/`. This working document now preserves only **analysis and unresolved alternatives**, not duplicate mechanical definitions.

## Selected native records
- `native/species/subspecies/dragonborn/chromatic.json` — parent family, common breath geometry and Chromatic Warding.
- `native/species/subspecies/dragonborn/chromatic/{black,blue,green,red,white}.json` — the five selectable leaves, elements, and innate adaptations.
- `native/species/base/dragonborn.json` — shared Dragonborn biology, resistance and Breath Weapon.
- The published `Fizban's Treasury of Dragons` family differentiations were adopted for Chromatic Breath Weapon geometry and Chromatic Warding. The older SRD 5.1 cone/line variations and 5.5e universal geometry/flight remain **unselected** comparisons.

## What remains under review
- **Cross-species balance:** unequal access to useful terrain and exploration settings. Black and Green amphibious breathing have no automatic Swim Speed; Red's 15-foot Climb Speed has broader everyday terrain usefulness; White's Icewalker and 15-foot Swim Speed cover distinct narrow terrain, with no water breathing.
- **Blue's magic:** its approved Displaced Voice uses an Action to project only the Dragonborn's own vocal sounds within 30 feet. It guarantees no deception. Additional multi-round duration, impersonation, richer sound simulation, or a new detection DC was **not** approved and requires its own reconciliation.
- **White's ordinary-ice exception:** ordinary icy terrain movement/check relief extends explicitly to the Dexterity saving throw against Prone arising solely from ordinary slippery ice. Other hazards or effects with additional restrictions remain applicable. The source monster's Ice Walk phrasing alone was narrower; this is a conscious Dorks & Dice adaptation.
- **Red's alternate smoke vision:** a separate Pathfinder candidate, not selected and not treated as 3.5e D&D evidence.
- **Blue's alternative burrowing:** Blue Dragon wyrmlings have Burrow Speed in the native bestiary; choosing this instead of Displaced Voice would be a later balance change, not an additional automatic grant.
- **5.5e Draconic Flight:** rejected as an automatic shared extra here; future independent study may revisit.
- **Upbringings:** `HOARD_TRADITIONS_RESEARCH.md` contains source-based learned-tradition suggestions, but no hoard-inspired Upbringing is approved or given a default yet. Appraise/Search bonuses should not be moved into Species for balance convenience.

## Historical sources
- SRD 5.1 and SRD 5.2.1: `data/reference/srd/`.
- *Fizban's Treasury of Dragons* (2021), Chapter 1 Chromatic Dragonborn and Chapter 5 treasure profiles.
- 3.5e *Player's Handbook II*, Dragon Shaman's Draconic Adaptation.
- Native 5.5e true-dragon wyrmlings: Black/Green Amphibious, Blue Burrow Speed, Red Climb Speed, White Ice Walk and Swim Speed.
- The general spatial foundation `native/spatial-rules.json` distinguishes movement modes and breathing permissions.
- The staged `working/hazards/slippery-ice.json` contains a historical ice hazard with a Dexterity saving throw; applying the newly approved explicit exemption requires future native mechanics integration.
- The older generic SRD Dragonborn Subspecies candidate remains in `working/species/subspecies/dragonborn/unnamed-1a909417769c0dff.json` **because it also holds unresolved Metallic candidate evidence**. Do not treat its already-reconciled Chromatic variations as an alternate runtime definition.

All approved mechanics are in `native/`, not in this note.
