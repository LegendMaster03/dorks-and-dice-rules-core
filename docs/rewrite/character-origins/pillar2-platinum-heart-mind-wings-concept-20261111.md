# Pillar 2 — Platinum Dragonborn: Heart, Mind, and Wings (working concept)

**Date:** 2026-11-11  
**Status:** Owner-approved three-expression identity (all three inherent to every Platinum Dragonborn) and Heart's two alternative damaging/restorative breath modes acquired together at rebirth with scaling effectiveness and one shared resource. Exact mechanics, damage/healing numbers, progression thresholds, and relationship to ordinary breath features remain unapproved. Not a native playable Ancestry. Preserve the approved exceptional Dragonborn acquisition/rebirth boundary in [Rites of Rebirth](pillar2-rites-of-rebirth-20261111.md).

## Purpose

A Platinum Dragonborn should **represent the singular sovereign of Metallic Dragons**, not simply be a stronger Gold Dragonborn or a sum of all five common Metallic Ancestries. Its exceptional qualities should communicate the physical and supernatural identity of that sovereign through three complementary expressions: **Heart, Mind, and Wings**.

**Approved structure:** Every Platinum Dragonborn manifests **Heart, Mind, and Wings together** through the exceptional rebirth. None is an exclusive selection or an optional replacement for another. These three expressions establish ancestry identity, **not** the original 3.5 aspect-selection mechanic, a separate class, three automatic fully specified feature packages, or any approved feature progression. The character remains Dragonborn of the Dragon creature type. Exceptional rebirth completely replaces the existing Species/Ancestry configuration, preserves historical parentage and cultural history, and normally requires the Metallic leader's direct personal participation. Exceptional forms are not ordinary creation options without DM approval, and are intentionally substantially stronger than standard Dragonborn.

The three-factor **ordinary** Dragonborn formula (category, skin material, damage type) does not dictate the exceptional Platinum's mechanical architecture or automatically select an elemental damage type.

## Existing native baseline to distinguish from

- `native/species/base/dragonborn.json`: Dragon type, 30-foot walking speed, Low-Light Vision, 50% damage resistance linked to ordinary leaf affinity, and one ordinary Breath Weapon replacing an Attack-action attack (PB uses per Long Rest; 1d10/2d10/3d10/4d10 by character level).
- `native/species/ancestry/dragonborn/metallic.json`: category 15-foot cone for ordinary breath; a **separate** level-5 Metallic Breath Weapon, one shared use per Long Rest, with Enervating and Repulsion choices.
- Base Dragonborn **do not** receive universal flight. A permanent-wing manifestation could be genuinely exceptional.
- Historical `working/` comparison notes sometimes describe older Humanoid/Darkvision assumptions; use the current `native/` records rather than treating those historical excerpts as live grants.

**Design consequence:** Simply offering an additional breath mode, more vision range, or a duplicate resistance does not alone establish the exceptional sovereign's identity. Each expression should change what the character *can do* in a recognizable way.

## Three inherent expressions: candidate mechanical roles

| Expression | Core role | Preliminary candidates, explicitly **not** grants |
| --- | --- | --- |
| **Heart** | Draconic breath and extraordinary vitality | Published damaging and restorative **alternative breath modes** sharing one resource; consider a later distinct defensive manifestation, but not automatically combined damage and healing |
| **Mind** | Heightened draconic perception and resilience | Source-grounded Darkvision/Blindsense progression (3.5), Truesight and specified condition protections (5e); precise translated senses, triggers and limits pending |
| **Wings** | Physical draconic flight | Source-grounded visible wings and progressive gliding/flight (3.5), with Bahamut's flight as a higher-power reference (5e); speed, maneuverability, endurance and acquisition milestones pending |

These are **mechanical roles and possible expressions**, not numeric choices, exact conditions, damage types, guaranteed immunity, specified ability resources, or a fixed moral/alignment profile. In particular, radiant damage, broad healing, a permanent protective aura, and truesight are **not automatically approved** by invoking a platinum theme.

## Published Bahamut mechanics — source baseline, not adopted gameplay

**Owner direction (2026-11-11):** Develop Platinum primarily from **established mechanics of Bahamut and the historical Dragonborn of Bahamut**, instead of inventing a new thematic power set and searching for justification afterward. Approved design commitments are the three inherent expressions and Heart's two alternative breath modes gained together with scaling effectiveness. Published numeric values and other source abilities remain historical research, not automatically approved Dorks & Dice features.

### Primary historical source comparison

| Source | Published mechanics (paraphrased) | Implications for Platinum ancestry |
| --- | --- | --- |
| *Races of the Dragon* (3.5), **Dragonborn of Bahamut** | **Heart:** shining line breath, choice of acid/cold/electricity/fire at use, Hit Dice-based damage/length, recharges in 1d4 rounds | Precedent for a versatile but **single** scaled offensive breath; does not establish radiant or healing breath for the original 3.5 Dragonborn |
| Same, **Mind** | Progressive low-light/Darkvision, eventual Blindsense; defense against paralysis and magical sleep | Direct mechanical starting point for the inherent **Mind** expression |
| Same, **Wings** | Wings enable gliding; flight arrives with higher Hit Dice and initially has endurance restrictions before becoming sustained | Direct mechanical starting point for inherent **Wings** and progressive aerial capability |
| *Fizban's Treasury of Dragons* (5e, 2021), **Aspect of Bahamut**, pp. 164–165 | **Platinum Breath:** radiant-damage cone; **Exalting Breath:** restorative cone healing chosen creatures and potentially restoring recently deceased creatures; the two modes share the same breath recharge | **Most concrete reference for Heart:** distinguish *offensive* versus *restorative* breath. They are **alternatives on each use, not simultaneous damaging and healing outputs** |
| Same, defense and senses | Truesight, several damage/condition immunities, Legendary Resistance; **Platinum Brilliance** provides an additional powerful phase after an otherwise fatal defeat | High-end examples for Mind and exceptional resilience, **not blanket player immunities, full Truesight or automatic extra lives** |
| Same, mobility and mythic features | High flight speed, **Celestial Shield** temporary HP, **Celestial Lances** force attack | Flight supports Wings. Shield/lances are **separate abilities**, not Breath Weapon outputs and not necessarily inherent wing features |

Published mechanical evidence:
- 3.5 **Dragonborn of Bahamut** comparison: https://dnd3-5.wikidot.com/dragonbornrace (secondary transcription of *Races of the Dragon*).
- 5e **Aspect of Bahamut** comparison: https://onyx5e.com/en/bestiary/aspect-of-bahamut/ (secondary transcription of *Fizban's Treasury of Dragons*, pp. 164–165).
- Historical 3.5 Dragonborn notes: `data/rulesets/first-party-001/working/species/subspecies/dragonborn/SHARED_BREATH_REFERENCE.md`.
- 3.5 *Dragon Magic* also offers **Discorporating Breath of Bahamut** as an advanced **Dragonfire Adept breath effect**. That is **not Bahamut's creature stat block or an automatic Dragonborn of Bahamut grant**; consider separately as class/invocation or advanced option research, not proof of Platinum ancestry entitlement.

The published depictions differ **across editions and rules contexts**. We may adapt selected mechanical ideas to the existing Dorks & Dice system, but should not imply that a 3.5 transformed ancestry, a deity's high-level 5e creature aspect, and a prestige class have the same playable abilities.

### Heart — approved structure: two-mode Platinum Breath (mechanics pending)

**Owner-approved structural direction:** When a character becomes a Platinum Dragonborn through exceptional Draconic Rebirth, **both breath modes are available immediately** as parts of the Heart expression. They **share one Breath Weapon resource**, and **the effectiveness of each mode scales afterward**. The character **chooses one mode each time the breath is used**, rather than applying both at once. Acquisition of one mode never requires choosing against, replacing, or separately unlocking the other. This approves the two-mode structure and scaling principle, **not** an amount, frequency, resource count, recharge schedule, damage affinity, area, action economy, healing target rules, or specific advancement milestones.

The two outputs are:

1. **Offensive breath:** A damaging manifestation, inspired by the 5e aspect's radiant damaging breath and the 3.5 Dragonborn's scaling line breath. Dorks & Dice damage affinity, geometry, saving throw, and the **amount/formula** of its scaling **remain open**.
2. **Restorative breath:** A healing manifestation, inspired by the 5e aspect's separate restorative breath. Beneficial targeting, healing amount, the **amount/formula** of its scaling, and possible advanced revival (if any) **remain open**. The approval of a restorative breath does **not** grant resurrection.

A character chooses one mode on each use. **No default rule permits damaging opponents and healing or shielding allies simultaneously with the same breath.** That earlier combined **Sovereign Breath** proposal was an independently invented alternative and is **superseded as the preferred working direction** by source-grounded alternative modes. It may be reconsidered only as an explicitly new design, not misrepresented as a published Bahamut mechanic.

**Interaction with existing native Dragonborn rules:** Ordinary Breath Weapon replaces one Attack-action attack (PB uses per Long Rest). Metallic Breath Weapon is a separate level-5, once-per-Long-Rest Enervating/Repulsion choice affecting creatures in its area, including allies. The Platinum ancestry must explicitly decide whether the two new modes **replace, extend or coexist** with those existing grants; do not silently append a third pool or treat historical 5e monster recharge as already applicable to player characters.

**Other published Bahamut mechanics** such as Platinum Brilliance (a mythic survival-phase mechanic), Celestial Shield (temporary HP) or Celestial Lances (force damage) are separate optional research candidates. They need not be included in Heart, and some may better fit later character progression or a future Draconic Patron. Previous decisions to keep the unrelated **Vassal of Bahamut** prestige-class powers largely in the future Patron candidate pool remain unchanged.

### Mind and Wings — next source-led design passes

**Mind:** Start with the 3.5 Dragonborn's progressive visual/Blindsense abilities and protection against paralysis/magical sleep, then compare Bahamut's 5e Truesight and broader condition defenses. Because the native Dragonborn already has Low-Light Vision, simple low-light duplication adds nothing. Decide what distinct senses or protections are genuinely inherent, and how acquisition at different levels affects progression. Do **not** assume all monster immunities.

**Wings:** Start with the original 3.5 progression of wings -> gliding -> restricted flight -> sustained flight, then compare the 5e aspect's powerful flying movement. All Platinum Dragonborn have the Wings expression, but the precise initial movement capabilities and advancement milestones are still open. Do **not** infer that they must use the original 3.5 Hit Dice milestones without adaptation.

**Preserve approved architecture:** All three expressions manifest together in exceptional Draconic Rebirth, including **both Heart breath modes with a shared resource and scaling effectiveness**. Neither ancestry Heart powers nor the overall Rite require becoming a Warlock, selecting a Vassal-inspired prestige class, or receiving a fixed contractual term. No specific level thresholds, die formulas, resource quantities/recharge schedule, spell-like effects or native executable data are approved by this design pass.

## Approved expression structure and pending feature design

**Every Platinum Dragonborn has all three expressions: Heart, Mind, and Wings.** The Rite grants the complete Platinum ancestry; there is **no one-of-three choice** and no need to select a preferred expression to receive the others. All three must be represented in its eventual authored mechanical identity.

This decision does **not** yet approve any exact abilities, numerical effects, activation requirements, trait resources, or level thresholds. A future proposal could investigate extra customization within an expression, but no emphasis/specialization choice, competing progression trees, or substitute aspect path is part of the current approved design. Avoid presenting them as default acquisition options.

## Timing and cross-pillar separation

- **Rebirth is the acquisition event**, not a Warlock level, prestige-class level, or inherited-characteristic recombination step. Its Species/Ancestry and creature type replacement semantics are already established.
- The ordinary native Dragonborn uses **character-level scaling**. Scaling an exceptional trait by character level would also permit a high-level character completing a Rite to enter with a high-level manifestation rather than restarting from level 1. Whether Platinum progresses immediately with character level, with subsequent rites/achievements, or by another independent mechanism is an unresolved design question.
- **Ancestry** should express what the creature has become. **Vassal of Bahamut** (*Book of Exalted Deeds*) is a separate historical prestige-class reference, including social standing, equipment, protective themes, and anti-dragon features; service, vows, weapons, treasure or a specific class progression must not automatically become inherited Platinum biology.
- A prospective **Draconic Warlock Patron** may reuse broad dragon-themed expressions through its own negotiated arrangements and source-specific progression. It is **only a future idea**, not an approved Patron or a prerequisite to Platinum rebirth. Warlock Patron, Pact, Invocations, and the Rite remain distinct systems.
- Do not automatically confer a personality, alignment, duty, loyalty, or Upbringing because of the new Ancestry. The Metallic leader's direct participation does not by itself determine contractual terms.

## Vassal ability allocation — 2026-11-11 working direction

The *Vassal of Bahamut* prestige-class features are now **primarily candidate material for a future Draconic Warlock Patron**, not default Platinum Ancestry features. No source feature has been approved for either implementation.

**Potential ancestry exception:** **Dragon Senses** aligns naturally with Platinum **Mind** because heightened innate sensory capacity is a characteristic of what the creature has become. This is a candidate only; exact senses, range and progression remain unsettled.

**Dragonwrack**, despite its draconic name, is a specialized anti-dragon attack and retaliation effect, and appears more closely associated with a dragon's granted mission or power than innate Platinum biology. It remains a future Patron/prestige adaptation candidate, not a Heart grant.

Imperious Aura, Platinum Armor, Shared Trove, bonus feats and divine spells are similarly **external patron/service-based candidate concepts**. They need not all become automatic Warlock class features: armor or trove could be negotiated gifts, feats might represent training, and spells could inform patron-specific spell access. Do not convert them into ancestry traits solely because the inspiration is Bahamut-linked.

The prior **Sovereign Breath** proposal remains a separate, *unapproved* Heart exploration; none of these Vassal features supplies proof of or automatic content for it. The design boundary remains **inherent transformed characteristics versus externally granted patron benefits**.

## Research provenance and editorial boundary

Original inspiration: *Races of the Dragon* (3.5), **Dragonborn of Bahamut** (Heart, Mind, Wings selection and HD-based enhancements); and *Book of Exalted Deeds* (3.5), **Vassal of Bahamut** as a distinct prestige-class source. Retain these as **internal source references only**. Player-facing first-party rules should use descriptive category-leader identities and independently written lore, rather than naming or reconstructing a recognizable D&D-specific character portrayal. Retaining the broad design themes does not authorize copying source prose or unique expression.

## Questions for a later decision pass

1. What inherent feature best expresses **each** of Heart, Mind, and Wings, and how should their effects interact without redundancy?
2. For the **approved damaging and restorative breath modes sharing one resource**, how do they relate to the existing base and Metallic Breath Weapons? What affinity, scaling formula, area, healing limits, action, use count, recharge schedule and optional advanced revival rules apply?
3. Which **source-established** Mind features (progressive Darkvision/Blindsense, paralysis/sleep protection, later Truesight or comparable resilience) should manifest, and at what progression?
4. How should Wings adapt the 3.5 glide/flight progression for a character reborn at any level? Which abilities are available immediately and which scale?
5. Should the three expressions combine in a unique later ability, and what triggers advancement for characters reborn at different levels?
6. What small set of feature interactions provides the intended substantial power increase without creating redundant or contradictory feature packages?

**Implementation boundary:** No native rules JSON, executable code, API, Character Sheet integration, Warlock progression, or final ritual procedures are changed or authorized by this design record.
