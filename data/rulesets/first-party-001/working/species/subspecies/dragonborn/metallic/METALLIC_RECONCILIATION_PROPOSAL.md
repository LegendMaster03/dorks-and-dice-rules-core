# Metallic Dragonborn — first-pass mechanical reconciliation proposal

**Status: research and recommendation only. No native mechanics or Upbringing default is approved by this document.**
**Scope:** Brass, Bronze, Copper, Gold and Silver. The approved shared Species is `native/species/base/dragonborn.json`, and the selected Chromatic comparison is `native/species/subspecies/dragonborn/chromatic.json`.

## Proposed Metallic family (`subspecies.dragonborn.metallic`)

- Nonselectable intermediate Subspecies, parent `species.dragonborn`. Five selectable Metallic leaves inherit from it through explicit stable parent IDs. Do not duplicate the approved Species Breath Weapon, resistance, size, darkvision, speed, or ancestry traits.
- **Ordinary damaging breath:** 15-foot cone, following published *Fizban's* Metallic Dragonborn and contrasting with the selected Chromatic 30-foot by 5-foot line. Damage, use count, Dexterity save, progression and Attack replacement all remain inherited from `species.dragonborn`. SRD 5.1's individual line/cone shapes and SRD 5.2.1's flexible shapes remain reference alternatives, not additional choices.
- **Secondary Metallic Breath (candidate):** beginning at character level 5, once per Long Rest **independently** of the normal Proficiency Bonus Breath Weapon uses. On your turn, replace one attack in your Attack action to exhale a 15-foot cone; DC = 8 + Proficiency Bonus + Constitution modifier. Choose one effect when used:
  - **Enervating:** Constitution save. Failure: Incapacitated until the start of your next turn; success: no effect.
  - **Repulsion:** Strength save. Failure: pushed 20 feet directly away and knocked Prone; success: no effect.
- No extra universal Draconic Flight; no additional bonus breath uses; no culture or alignment restriction.
- **Balance warning:** incapacitation affecting several enemies may be more consistently valuable than Chromatic Warding's 1-minute single-element immunity, particularly in encounters without matching damage. Keep its use and independent resource explicit, and evaluate through playtests before declaring parity.

## Proposed individual inherited adaptations

| Dragonborn | Inherited damage | Candidate adaptation | Main source and deliberate adjustment |
| --- | --- | --- | --- |
| **Brass** | Fire | **Climatic Resilience:** immune to penalties from ordinary nonmagical severe heat/cold exposure, but **not** fire/cold damage, hazardous substances, magical weather, starvation, thirst, or exhaustion from unrelated causes. | 3.5e Dragon Shaman's self-only at-will *Endure Elements*. Rewrite as a bounded passive adaptation, not an at-will spell or an additional damage resistance. Alternative to research: a sand-only 15-foot Burrow Speed from the native SRD 5.2.1 wyrmling; tunnel creation and obstacle bypass make this harder to balance. |
| **Bronze** | Lightning | **Amphibious:** breathe air and water indefinitely, no automatic Swim Speed. | 3.5e Dragon Shaman water breathing and native Bronze Dragon Wyrmling Amphibious. Mechanically comparable to native Black and Green. |
| **Copper** | Acid | **Natural Climber:** 15-foot Climb Speed, no ceiling movement or Spider Climb; extraordinarily difficult surfaces may still require checks. | 3.5e Dragon Shaman self-only *Spider Climb* is substantially stronger; native Copper Dragon Wyrmling has a Climb Speed. Starting with the already selected Red Dragonborn speed is an intentionally conservative adaptation. |
| **Gold** | Fire | **Amphibious:** breathe air and water indefinitely, no automatic Swim Speed. | 3.5e Dragon Shaman water breathing and native Gold Dragon Wyrmling Amphibious. Distinction from Bronze is elemental/family breath, not a required unique adaptation. |
| **Silver** | Cold | **Soft Landing (self-only):** as a Reaction when falling, slow your own descent and avoid falling damage if you land while the effect persists. At will, no passenger or ally protection and no ability to hover or fly. | 3.5e Dragon Shaman's self-only at-will *Feather Fall*, adapted to a discrete falling trigger rather than importing the spell wholesale. |

These adaptations are intentionally **candidate** rules. Brass's severe-temperature threshold, Silver's reaction timing/duration, and any platform-specific Climb Speed behavior must be checked against existing native environmental and spatial mechanics before promotion. Duplicate amphibious adaptations are not automatically a design defect; true-dragon ancestry and equivalent choices matter more than uniqueness for its own sake.

## Source distinctions and alternative breaths

- *Fizban's Treasury of Dragons* (2021), Chapter 1: one Metallic family control breath with two selectable effects, **not** one effect per color. The control breath uses its own once-per-Long-Rest resource. Official publisher overview: https://www.dndbeyond.com/posts/1084-how-the-metallic-dragonborn-in-fizbans-treasury-of
- 3.5e *Player's Handbook II*, Dragon Shaman, Draconic Adaptation: Brass self-only *Endure Elements*; Bronze/Gold water breathing; Copper self-only *Spider Climb*; Silver self-only *Feather Fall*. These were class features, not automatically biological Species features. Cross-reference: https://srd.dndtools.org/srd/classes/basePhb2/dragonShaman.html
- 3.5e SRD true dragons and 5.5e native wyrmlings have **different color-specific secondary breaths** (e.g., Brass Sleep, Bronze Repulsion, Copper Slow, Gold Weaken, Silver Paralysis). These may inspire later selectable customization, but **do not stack** five additional color powers on the proposed shared family control breath. 3.5e SRD reference: https://www.d20srd.org/srd/monsters/dragonTrue ; native source: `native/bestiary/{brass,bronze,copper,gold,silver}-dragon-wyrmling.json`.
- *Fizban's* Chapter 5 hoard themes have already been researched for all five Metallic types in `HOARD_TRADITIONS_RESEARCH.md`. They suggest **learned Upbringings**, not innate powers, mandatory hoarding, or alignment. No default pointer is selected yet.

## Decisions required before native promotion

1. Approve or modify the proposed **15-foot cone** and the **level-5 Metallic secondary breath** (including independent use count and action economy).
2. Approve or modify the five candidate **leaf adaptations**; especially Brass's bounded climate protection and Silver's falling protection.
3. Compare total family-and-leaf value against selected Chromatic Warding plus Chromatic leaves. This is a provisional playtest balance decision, not a final cross-Species verdict.
4. Only after approval create `native/species/subspecies/dragonborn/metallic.json` and five leaves. Then update the native rule's review notes, working status/coverage notes and validate parent chains. Keep Upbringings, defaults, Gem and other dragon categories in `working/` pending their own reconciliation.

**Historical SRD candidate:** `working/species/subspecies/dragonborn/unnamed-1a909417769c0dff.json` contains unresolved Metallic variants. Do not delete this until its still-relevant evidence is reconciled and the coverage manifest is updated.
