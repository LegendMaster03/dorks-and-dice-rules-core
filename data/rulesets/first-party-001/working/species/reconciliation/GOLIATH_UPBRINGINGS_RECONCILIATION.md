# Goliath and Giant Foundling Upbringings — First-Pass Reconciliation

**Status:** The native playtest choices are **General Goliath Upbringing** and **Giant Foundling Upbringing**, both independently selectable; only the former is suggested by `species.goliath`. The six formerly native Giant Society Upbringings have been withdrawn as unsupported cultural subdivisions of the six Goliath biological ancestries. Their exploratory ideas are retained below, **not playable rules**.

## Evidence and interpretation

- *Races of Stone* (3.5e) describes Goliath clan societies and oral traditions with the Gol-Kaa language; Giant is listed separately. Different clans may have different practices and relationships with giants. It does **not** establish six culturally separate Goliath tribes corresponding to the six 2024 giant ancestry powers.
- 5e Goliath descriptions preserve a recognizable, broadly shared clan culture (mountain survival, competition, and communal responsibility), although the language in the 2014-era version is Giant rather than Gol-Kaa.
- The 2024/5.5e Goliath rules make the Cloud, Fire, Frost, Hill, Stone and Storm features ancestry-derived supernatural options, not distinct social institutions, clan cultures or six separate Upbringing choices.
- *Bigby Presents: Glory of the Giants* (2023) describes **giant societies**; the separate Giant Foundling **Background** explicitly represents a person of another Species raised among giants. Neither its backstory nor the six distinct giant societies establishes six standard Goliath clan cultures.

**Selected Dorks & Dice reconciliation:** Retain Gol-Kaa as a learned traditional Goliath clan language; the historical shift to Giant in later editions does not require us to erase Gol-Kaa or claim the two languages are identical. Preserve Giant as a separate language available through Giant Foundling and other language-learning paths.

## Authoritative native choices

| Upbringing | Independent benefit package | Applicable Species |
| --- | --- | --- |
| `upbringing.goliath.general` | Gol-Kaa; train one of Athletics, Insight or Survival; train one of Cooking, Leatherworking or Navigation | Any Species; suggested default for Goliaths |
| `upbringing.giant-foundling` | Giant; one additional language choice; train Intimidation and Survival | Any Species, including any Goliath Subspecies; no Species default |

Both records stand alone, with **no Upbringing parent**. A character selects one Upbringing, not both by default. Goliaths with Giant Foundling do **not** simultaneously receive Gol-Kaa or Goliath clan training unless another independent rule grants them. A non-Goliath with Giant Foundling does **not** acquire inherited Goliath supernatural abilities.

## Source Background → Upbringing conversion

The source Giant Foundling Background grants Intimidation, Survival, Giant, one additional language, a starting-equipment package, and the Strike of the Giants feat. This Upbringing selects the **learned languages and skills** without assigning a separate Background or equipment entitlement (starting inventory belongs in the appropriate character-creation system). Its source feat grant is intentionally excluded from the Upbringing.

**Selected feat disposition:** The original Giant Foundling feat grant is deliberately removed in the Dorks & Dice Upbringing conversion. Giant Foundling confers **no automatic Strike of the Giants feat** and does not duplicate a Goliath Subspecies' Strike-derived abilities. This does not prohibit a character from independently acquiring a giant-themed feat if the later Feat rules allow it. It does not deny that the published Background originally granted the feat.

The original narrative discusses magically unusual size, but neither the source nor this conversion grants an automatic change to the character's actual Size category.

## Rejected/retired six-way cultural candidates — reference only

An earlier proposal incorrectly treated the six giant societies described in *Bigby's* as if they were six standard Goliath Upbringings parallel to the six ancestral Subspecies. Those invented training bonuses are **not** selected native content. Their former themes are preserved only for later independent giant-worldbuilding and possible individual cultural specialties, should the setting establish them.

| Former record | Proposed name | Former invented cultural skill benefit |
| --- | --- | --- |
| `upbringing.giant.cloud` | Cloud Giant Society Upbringing | Gain +2 on Appraise checks for these learned activities: precious goods appraisal, crafted gift valuation, art object valuation. The activity, not merely the character's location or ancestry, must qualify. |
| `upbringing.giant.fire` | Fire Giant Society Upbringing | Gain +2 on Investigation checks for these learned activities: metalworking construction analysis, forge flaw identification, worked metal damage assessment. The activity, not merely the character's location or ancestry, must qualify. |
| `upbringing.giant.frost` | Frost Giant Society Upbringing | Gain +2 on Survival checks for these learned activities: snowy wilderness tracking, cold country hunting routes, snow and ice navigation. The activity, not merely the character's location or ancestry, must qualify. |
| `upbringing.giant.hill` | Hill Giant Society Upbringing | Gain +2 on Survival checks for these learned activities: edible plant foraging, food game tracking, safe foraging area selection. The activity, not merely the character's location or ancestry, must qualify. |
| `upbringing.giant.stone` | Stone Giant Society Upbringing | Gain +2 on History checks for these learned activities: stone art history, worked stone inscription history, stone monument provenance. The activity, not merely the character's location or ancestry, must qualify. |
| `upbringing.giant.storm` | Storm Giant Society Upbringing | Gain +2 on Nature checks for these learned activities: observable weather pattern analysis, natural storm patterns, tidal condition knowledge. The activity, not merely the character's location or ancestry, must qualify. |

The six original native JSON records have been removed from `native/upbringings/giant/`. The facts about actual Cloud, Fire, Frost, Hill, Stone and Storm giant society remain valid worldbuilding research, but there is no automatic six-way Goliath culture subdivision.

## Open dependencies

- Gol-Kaa is now selected as `language.gol-kaa` in `native/languages/gol-kaa.json`. Giant remains a separate staged language in the broader working language catalog; full language-family and script reconciliation is separate.
- Upbringing benefit execution and character language/Skill grants depend on the Character Sheet and native projector integration.
- Strike of the Giants is **not** granted by Giant Foundling, by design. Independent feat ownership and eligibility can be evaluated in the wider Feat pass; Rune Carver remains separate material, and no rune magic was awarded here.
- The broader cultural pass may revise the skill balance (Goliath Clan grants one choice of Skill and one Competency, Giant Foundling grants two fixed Skills and a language choice) without inventing extra Species powers.

## Sources

- D&D Beyond 2024 Goliath rules: https://www.dndbeyond.com/sources/dnd/br-2024/character-origins
- D&D Beyond 2014 Goliath tribal culture: https://www.dndbeyond.com/species/22-goliath
- Publisher Giant Foundling overview: https://www.dndbeyond.com/posts/1531-first-look-giant-foundling-background-and-strike
- *Bigby Presents: Glory of the Giants* book index: https://www.dndbeyond.com/sources/dnd/gotg
- 3.5e Goliath clan-language notes: https://dnd.arkalseif.info/races/races-of-stone--82/goliath--13/
