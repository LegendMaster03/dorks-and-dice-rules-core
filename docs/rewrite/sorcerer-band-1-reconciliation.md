# Sorcerer — Band 1 source comparison and decisions for review

**Status (2026-10-23):** Source comparison started; **no new Sorcerer feature designs are owner-approved and no native Sorcerer class implementation has been made**. The owner approved the class review sequence **Sorcerer → Wizard → Monk** after Rogue. Restrict this pass to Sorcerer class levels 1–20. Subclasses/origins/bloodlines, prestige classes, higher level bands, and executable spell/point conversion are outside this class pass.

## Already established across Dorks & Dice

- **One native spellcasting system:** Players may choose the class-eligible **spell slots or spell points** resource profile. Class rules must not silently create a separate spellcasting system.
- **No daily spell preparation:** This general rule is approved; it does **not by itself** decide whether a class knows its full spell list or a finite selection. Bard retains a known-spells progression while Cleric and Druid gained their respective approved broad eligible-list access.
- **Rest-based recharge** is standard for class resources unless an explicit exception has a compelling calendar-time purpose.
- **Band 1** is class levels 1–20, with Epic Boons available as feats only when total character level reaches 21. No permanent universal ability score maximum is imposed.
- **General combat and shared spell rules**, including action economy, other spellcasting classes, familiar/controlled creature actions, resource exchange and feats, should be centralized rather than customized separately for Sorcerer.

## Verified source contrasts

The staged 2014 and 2024 source candidates live in `data/native/classes/base/sorcerer.json` and `data/native/features/classes/sorcerer/`. The historical 3.5e source is `data/reference/3x/band1-baseclasses/srd35/classes-ii.md` on branch `feature/band1-baseclass-srd-sources`. The 3.0 material remains a separate comparison to verify; **do not treat 3.0 values as independently confirmed here**.

| Capability | 3.5e source | 2014 SRD 5.1 source | 2024 SRD 5.2.1 source | Native decision required |
| --- | --- | --- | --- | --- |
| Hit Die | d4 | d6 | d6 | Modern d6 is the natural proposal; no numeric decision yet |
| Spell selection | Spontaneous spellcasting, **known spells by spell level**; no prepared spells | Finite **known** leveled spells; 15 at Rogue-independent Sorcerer level 20 | 22 entries labeled **prepared** in the 2024 advancement; native general rule removes daily preparation | Choose native spell-access breadth and advancement, not merely relabel "prepared" without review |
| Sorcery Points | No equivalent default class pool; historical Metamagic uses feats and spell-level costs | Class pool begins at level 2, grows to 20, refreshes on Long Rest | Same Sorcery Point progression | Choose Sorcery Point accounting and how it coexists with slot-mode or point-mode **casting** |
| Flexible Casting | Not part of 3.5e base class | Convert points to slots and expend slots to gain Sorcery Points, normally as Bonus Actions | Creates slots using Sorcery Points as Bonus Action; slot-to-point conversion costs no action | Define one conversion model with **no duplicate currency or arbitrage** in spell-point casting mode; reconcile action costs |
| Metamagic | Feat-based modifications, higher spell-level cost and spontaneous casting-time restrictions | Two options at level 3; one additional option each at 10 and 17 | Two options at level 2; **two additional options** each at 10 and 17 | Consider earlier broader 2024 training while allowing meaningful historical Metamagic Feat compatibility under shared rules |
| Innate Sorcery | No class ability equivalent in historical source | No class ability equivalent | Bonus Action, 1 minute, **2 uses per Long Rest**; +1 Sorcerer spell save DC and Advantage on Sorcerer spell attacks | Consider retain; evaluate overlapping modifiers and rest pool |
| Sorcery Incarnate | No class ability equivalent | No class ability equivalent | Level 7: activate Innate Sorcery when out of uses by spending 2 Sorcery Points, and use two Metamagic options per spell while Innate Sorcery active | Consider retain; preserve costs and normal Metamagic limits outside its active window |
| Sorcerous Restoration | Not the same class resource | Level 20: regain **4 spent Sorcery Points each Short Rest** | Level 5: on Short Rest regain up to **half Sorcerer level** expended Sorcery Points **once per Long Rest** | Decide whether the level-5 benefit and historical level-20 recovery remain separate stages of one feature; avoid unintended duplication |
| Level-20 Arcane Apotheosis | No exact base feature | No exact equivalent | During Innate Sorcery, use **one Metamagic option each turn** without paying its Sorcery Point cost | Consider compatibility with retained Sorcerous Restoration; avoid erasing 2014 capstone solely because 2024 renames it |
| Familiar | Level-1 native Sorcerer class option; historically 24-hour/100 gp summoning and severe death/replacement penalties | Not a default class feature | Not a default class feature | Recover familiar capability using shared familiar and controlled-creature rules; separately review obsolete XP-loss/year-and-day penalties |
| Origin/subclass | Not necessarily identical to later bloodline/origin models | Sorcerous Origin at level 1, later improvements at 6, 14, 18 | Sorcerer subclass at level 3, improvements at 6, 14, 18 | **Placeholders only** during base class pass; do not grant either subclass's features by default |

## Proposed order of decisions (none approved in this record)

1. **Sorcery Points versus spell-point casting:** Define whether they are distinct tracked resources and an explicit, bounded exchange mechanism. Keep both slot and spell-point casting viable without granting free extra spellcasting or Metamagic usage.
2. **Spell access and advancement:** Determine finite known-spell capacity versus broader native access, and which 3.5e/2014/2024 progression is the appropriate coherent baseline. No spell preparation applies regardless.
3. **Metamagic and Innate Sorcery:** Review 2024 earlier option access and stronger later progression, with 3.5e feat options retained or converted where genuinely compatible. Resolve casting-time costs through shared action economy.
4. **Restoration and capstones:** Reconcile level-5 2024 Sorcerous Restoration, level-20 2014 Restoration and 2024 Arcane Apotheosis into one progression without redundant restoration.
5. **Historical Familiar and baseline advancement:** Preserve meaningful companion capability, but revisit XP penalties and extremely long replacement times; finish Hit Die, proficiencies, subclass placeholders, ASIs and the level 1–20 table.

## Boundaries

Comparing 3.0/3.5e/2014/2024 is not an automatic latest-edition preference. The owner generally regards 3.5e as an improvement on 3.0e but prefers many 2014 mechanics over 2024, while retaining useful 2024 additions. **This file is a proposal and source inventory only**: do not change Sorcerer native JSON, the class reconciliation decision ledger, or source candidate statuses to approved until the owner decides the specific mechanics. Nothing here reopens already approved Bard, Cleric or Druid spells/slots/points rulings.
