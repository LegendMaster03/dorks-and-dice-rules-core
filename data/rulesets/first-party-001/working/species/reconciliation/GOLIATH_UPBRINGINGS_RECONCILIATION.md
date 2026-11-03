# Goliath and Giant Cultural Upbringings — First-Pass Native Reconciliation

**Status:** Seven playable Upbringings selected as experimental native content. Learned customs are independent of biological Species/Subspecies, including for non-Goliath characters raised among giants. No feat, Origin feat, Rune Shaper spellcasting, or extra Strike is granted here.

## Source distinctions

- *Races of Stone* (3.5e) identifies **Gol-Kaa** as a traditional Goliath language and **Giant** as a separate optional language. The clans feature cooperation, competitions, oral memory, and mountain community work. Later 5e typically grants Giant instead. These languages should not be silently equated.
- *Bigby Presents: Glory of the Giants* identifies six different giant societies and their preferred social measures and activities. Those are examples of learnable traditions, **not compulsory personalities, belief systems or ranks**.
- *Bigby's* Giant Foundling and Rune Carver are Backgrounds, not Goliath species features. They carry distinct feats in their original presentation; their eventual Background reconciliation must not silently duplicate the selected Goliath ancestry powers.

## Selected Upbringing records

| Record | Learned language | Training and cultural practice |
| --- | --- | --- |
| `upbringing.goliath.general` | Gol-Kaa | Choose one Skill training (Athletics, Insight, Survival) and one Competency (Cooking, Leatherworking, Navigation) |
| `upbringing.giant.cloud` | Giant | Choose Gemcutting or Calligraphy; +2 scoped Appraise for treasures and crafted gifts |
| `upbringing.giant.fire` | Giant | Choose Smithing, Weaponsmithing, Armorsmithing or Blacksmithing; +2 scoped Investigation for worked metal |
| `upbringing.giant.frost` | Giant | Choose Leatherworking or Woodcarving; +2 scoped Survival for cold-country tracking and navigation |
| `upbringing.giant.hill` | Giant | Choose Cooking or Herbalism; +2 scoped Survival for foraging and game tracking |
| `upbringing.giant.stone` | Giant | Choose Stonemasonry or Sculpting; +2 scoped History for stone monuments, carvings and inscriptions |
| `upbringing.giant.storm` | Giant | Choose Navigation or Cartography; +2 scoped Nature for weather patterns and tides |

Skill and Competency identifiers reference the existing catalog. Training in composite Athletics does not automatically train its independently ranked component Skills. The scoped +2 bonuses stack with training but apply to the listed activities only. These grants are **new Dorks & Dice cultural adaptations**, not published Upbringing stat blocks.

## Inheritance and defaults

`species.goliath` sets `"defaultUpbringing": "upbringing.goliath.general"`, and all six Goliath Subspecies inherit that default. The player may select another Upbringing without ancestry-based restriction. The six `upbringing.giant.*` records have **no parent** to `upbringing.goliath.general`: being raised among giants does not make a non-Goliath character inherit Gol-Kaa or Goliath clan practices. Every chosen Upbringing consumes one selection; none is automatically stacked with the Species default.

## Pending follow-up

- The separate global language catalog does not yet define `language.gol-kaa`; the selected Upbringing grants the learned language `gol-kaa` as an initial rule reference, pending the full language-catalog reconciliation. Giant is currently represented in the staged language catalog.
- The Character Sheet's native projector does not necessarily execute Upbringing inheritance, Skill/Competency selections, or scoped bonuses; those are separate integration tasks.
- Original Background/Origin feat grants, including Giant Foundling and Rune Carver, must be reconciled independently. Goliaths must not receive duplicate Strike benefits or rune casting through an Upbringing by accident.
- Balance across generic and specialized Upbringings, history of Gol-Kaa script, and the final Skill/Competency training procedure remain revisable in the wider character-building pass.

## Sources

- 3.5e Goliath characteristics: https://dnd.arkalseif.info/races/races-of-stone--82/goliath--13/
- Historical Gol-Kaa tongue: https://arkalseif.info/languages/gol-kaa/
- 5e Goliath source: https://www.dndbeyond.com/species/22-goliath
- Bigby's six giant cultures: https://www.dndbeyond.com/posts/1534-giant-personalities-an-infographic-on-the
- Bigby's book index: https://www.dndbeyond.com/sources/dnd/gotg
- Publisher Rune Carver overview: https://www.dndbeyond.com/posts/1551-channel-ancient-power-with-the-rune-carver
