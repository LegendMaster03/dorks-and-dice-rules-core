# Pillar 2 — Platinum Dragonborn: Heart, Mind, and Wings (working concept)

**Date:** 2026-11-11  
**Status:** Owner-approved three-expression identity (all three inherent to every Platinum Dragonborn), **one combined Heart breath** with selective damage/healing and scaling effectiveness, and **Mind's combined sensory-progression and limited-resilience direction** drawing on the 3.5 transformation and 5e champion. Exact features, values, progressions, and rules-engine behavior remain unapproved. Not a native playable Ancestry. Preserve the approved exceptional Dragonborn acquisition/rebirth boundary in [Rites of Rebirth](pillar2-rites-of-rebirth-20261111.md).

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
| **Heart** | Draconic breath and extraordinary vitality | One combined damaging-and-healing breath, modeled on the published *Dragonborn of Bahamut* NPC's selective Radiant Breath; separate Healing Touch remains a non-approved reference candidate |
| **Mind** | Heightened draconic perception and resilience | **Approved combined direction:** progressive Darkvision/Blindsense (3.5) alongside the 5e champion's fear immunity and limited failed-save protection; original 3.5 paralysis/magical-sleep protection remains a relevant candidate. Exact manifestation, acquisition timing and uses pending; Truesight not approved. |
| **Wings** | Physical draconic flight | Source-grounded visible wings and progressive gliding/flight (3.5), with Bahamut's flight as a higher-power reference (5e); speed, maneuverability, endurance and acquisition milestones pending |

These are **mechanical roles and possible expressions**, not numeric choices, exact conditions, damage types, guaranteed immunity, specified ability resources, or a fixed moral/alignment profile. In particular, radiant damage, broad healing, a permanent protective aura, and truesight are **not automatically approved** by invoking a platinum theme.

## Published Bahamut mechanics — source baseline, not adopted gameplay

**Owner direction (2026-11-11):** Develop Platinum primarily from **established mechanics of Bahamut and the historical Dragonborn of Bahamut**, instead of inventing a new thematic power set and searching for justification afterward. Approved design commitments are the three inherent expressions and **one Heart breath with selective damage and healing in the same activation**, available through rebirth and improving afterward. Published numeric values and other source abilities remain historical research, not automatically approved Dorks & Dice features.

### Primary historical source comparison

| Source | Published mechanics (paraphrased) | Implications for Platinum ancestry |
| --- | --- | --- |
| *Races of the Dragon* (3.5), **Dragonborn of Bahamut** | **Heart:** shining line breath, choice of acid/cold/electricity/fire at use, Hit Dice-based damage/length, recharges in 1d4 rounds | Precedent for a versatile but **single** scaled offensive breath; does not establish radiant or healing breath for the original 3.5 Dragonborn |
| Same, **Mind** | Progressive low-light/Darkvision, eventual Blindsense; defense against paralysis and magical sleep | Direct mechanical starting point for the inherent **Mind** expression |
| Same, **Wings** | Wings enable gliding; flight arrives with higher Hit Dice and initially has endurance restrictions before becoming sustained | Direct mechanical starting point for inherent **Wings** and progressive aerial capability |
| *Fizban's Treasury of Dragons* (5e, 2021), **Aspect of Bahamut**, pp. 164–165 | **Platinum Breath** damages; **Exalting Breath** restores health and can revive the recently deceased. These are separate alternative breath outputs sharing recharge. | A **different Bahamut portrayal**, useful for eventual advanced options but **not the current approved combined Heart breath**. Do not merge the Aspect's resurrection or monster-scale values into a player ancestry. |
| *Fizban's Treasury of Dragons* (5e, 2021), **Dragonborn of Bahamut** NPC, pp. 184–185 | **Radiant Breath** deals radiant damage in a cone while exempting up to three selected creatures and restoring their HP instead; also has Healing Touch, flight, fear immunity, limited Legendary Resistance, radiant/blinding sword attacks. | **Primary direct precedent** for Platinum Heart's now-approved **one selective damage-and-healing breath**. Other NPC abilities must be allocated individually rather than copied as one ancestry package. |
| Same, defense and senses | Truesight, several damage/condition immunities, Legendary Resistance; **Platinum Brilliance** provides an additional powerful phase after an otherwise fatal defeat | High-end examples for Mind and exceptional resilience, **not blanket player immunities, full Truesight or automatic extra lives** |
| Same, mobility and mythic features | High flight speed, **Celestial Shield** temporary HP, **Celestial Lances** force attack | Flight supports Wings. Shield/lances are **separate abilities**, not Breath Weapon outputs and not necessarily inherent wing features |

Published mechanical evidence:
- 3.5 **Dragonborn of Bahamut** comparison: https://dnd3-5.wikidot.com/dragonbornrace (secondary transcription of *Races of the Dragon*).
- 5e **Aspect of Bahamut** comparison: https://onyx5e.com/en/bestiary/aspect-of-bahamut/ (secondary transcription of *Fizban's Treasury of Dragons*, pp. 164–165).
- 5e **Dragonborn of Bahamut** NPC comparison: https://onyx5e.com/en/bestiary/dragonborn-of-bahamut/ (*Fizban's Treasury of Dragons*, pp. 184–185; NPC statistics, not a playable Ancestry grant).
- Compare neighboring champion NPCs in the same source: https://onyx5e.com/en/bestiary/dragonborn-of-tiamat/ and https://onyx5e.com/en/bestiary/dragonborn-of-sardior/.
- Historical 3.5 Dragonborn notes: `data/rulesets/first-party-001/working/species/subspecies/dragonborn/SHARED_BREATH_REFERENCE.md`.
- 3.5 *Dragon Magic* also offers **Discorporating Breath of Bahamut** as an advanced **Dragonfire Adept breath effect**. That is **not Bahamut's creature stat block or an automatic Dragonborn of Bahamut grant**; consider separately as class/invocation or advanced option research, not proof of Platinum ancestry entitlement.

The published depictions differ **across editions and rules contexts**. We may adapt selected mechanical ideas to the existing Dorks & Dice system, but should not imply that a 3.5 transformed ancestry, a deity's high-level 5e creature aspect, and a prestige class have the same playable abilities.

### *Dragonborn of Bahamut* NPC — detailed source audit (research only)

The *Fizban's* Dragonborn of Bahamut (CR 8 NPC; pp. 184–185) should be examined **as a complete source**, not reduced to the Aspect of Bahamut's breath modes. Historical numerical values are listed for comparison only, **not approved player-character features**:

| NPC feature | Source mechanic (paraphrased) | Current Platinum interpretation |
| --- | --- | --- |
| **Radiant Breath** | A **30-foot cone**; **DC 15 Dexterity** save; **8d10 radiant** damage (half on success). At activation, **up to three chosen creatures inside** receive **4d10 healing instead of taking damage**. **Recharge 6**. | **Approved structural inspiration for Heart:** one breath, one area/use; chosen recipients are healed **instead of** damaged. Exact figures, damage type, cone geometry, count, recharge, action cost, and scaling **not selected**. |
| **Healing Touch** | **Once/day** touch healing of **40 HP** to another creature; removes its diseases and poisons. | **Separate candidate**, not an inherent automatic Heart benefit; decide later whether an independent advanced restorative feature belongs to ancestry or patron progression. |
| **Wings/Flight** | **30-foot flying speed** with prominent platinum wings, no staged glide-to-flight limit in the stat block. | Strong **Wings** precedent for full flight, distinct from 3.5's progressive gliding/flight; exact movement mechanics remain open. |
| **Fear immunity** | Immune to the Frightened condition. | Possible **Mind** defense; notably **also present** on the two other category-leader champion NPCs, so not uniquely Platinum. |
| **Legendary Resistance** | Can change **one failed save/day** into success. | Also shared by all three champion NPCs; may be an exceptional-champion motif, an advanced resilience candidate, or NPC-only power. **Not an automatic ancestry grant**. |
| **Radiant sword/blinding strike** | Three sword attacks on Multiattack; extra radiant damage; once/turn radiant flash can briefly blind a struck target. | Weapon/combat and NPC action-economy mechanics are **not intrinsically biological**. Consider for future Patron, training or other advancement; do not automatically confer three attacks, weapon grants, or scaling weapon damage. |
| **Other profile** | Medium **Humanoid**, CR 8, AC 18 from equipment, mundane Athletics/Perception/Persuasion skill entries, typical alignment. | Native Dorks & Dice Dragonborn have **Dragon** type. NPC CR, gear, skills, alignment, class/organization role, and ability scores **do not override** our player-Ancestry or Upbringing boundaries. The NPC does not establish a Rite of Rebirth requirement. |

**Cross-champion pattern within the same book:** The Dragonborn of **Tiamat** (CR 7) and **Sardior** (CR 6) also have fear immunity and once/day Legendary Resistance. Their unique attacks and powers diverge: the former uses **necrotic breath with a fear effect**, while the latter has **psionic spellcasting, a mind blade, hovering flight and heat breath**. This suggests separating any future general exceptional-champion chassis from the **category-specific breath, senses, magic and movement**. It does **not** approve a generic champion rules package or revise the already separate Fivefold Chromatic/Ruby design concepts.

**Edition distinction:** The 3.5 *Races of the Dragon* **Dragonborn of Bahamut** is a **transformation/ancestry** with a **choice** of Heart, Mind or Wings and Hit Dice-based advancement. The 5e *Fizban's* **Dragonborn of Bahamut** is a **champion NPC**, not documentation for that same transformation. Dorks & Dice combines **all three expressions** through its independent exceptional Draconic Rebirth framework, without automatically adopting the 5e champion's external/martial grants or original creature type.

### Heart — approved combined Radiant Breath structure; exact mechanics pending

**Owner-approved direction (supersedes the former two-mode breath design):** A Platinum Dragonborn receives **one combined offensive/restorative Breath Weapon through exceptional rebirth**. In one use, it produces one authored area. **Some eligible creatures in that area are selected to receive healing instead of damage; other affected creatures receive the damaging effect.** A creature receiving healing from that activation does **not** also take its damage. The breath's **damage and healing effectiveness increase with advancement**. This is an ability of Heart, which all Platinum Dragonborn inherently possess.

The source-inspired action is **one breath ability and one resource**, not separate damaging and restorative breath modes or independent usage pools. This is the direct 5e NPC precedent—not the 5e Aspect's alternative damaging/restorative breaths. That earlier two-mode decision is **explicitly superseded**. Selective recipients should be determined by an actual **player choice**, not assumed moral alignment or friendship.

**Still to reconcile for Dorks & Dice:**
- **Inheritance and native Breath Weapon:** Specify whether this combined breath replaces, enhances or coexists with the base Dragonborn Breath Weapon and the ordinary Metallic family's separate Enervating/Repulsion breath; no silent duplication or removal.
- **Targeting:** Determine area shape, eligibility, maximum protected/healed creatures, exclusions, whether willing targets are required, interaction with allies/enemies, and targeting information. The NPC's up-to-three selection is a **source number**, not an approval.
- **Damage/healing:** Determine damage affinity (the NPC uses radiant), damage/save formula, healing dice, whether healing stabilizes or affects creatures at 0 HP, and whether healing can restore more than current HP. Reviving dead creatures is **not** part of the current approved ability.
- **Scaling:** Both outputs grow after rebirth; exact character-level brackets, formulas and handling of rebirth at high character level are unresolved.
- **Resource/action contract:** Determine whether to preserve the base Dragonborn's Attack-action attack replacement, Proficiency Bonus uses per Long Rest, or introduce an explicit exceptional resource/recharge; do **not** silently copy the NPC's Action + Recharge 6.

**Other source abilities:** Healing Touch and weapon flash remain unapproved individual candidates. **Fear immunity and limited failed-save protection now form part of Mind's approved combined design direction**, but their exact effects, timing, and resource rules are not approved by the Heart breath decision. Wing-based flight awaits its own mechanical pass.

### Mind — approved combined sensory and resilience direction (mechanics pending)

**Owner-approved design direction (2026-11-11):** Platinum's **Mind** should combine **the progressive sensing capabilities associated with the 3.5 Dragonborn of Bahamut** with **the fear immunity and limited saving-throw protection represented in the 5e Dragonborn of Bahamut champion**. These belong in a single coherent Mind expression, not competing paths or mutually exclusive options. The 3.5 paralysis and magical-sleep protections are relevant source candidates within the same defensive theme; their exact disposition and levels have not been decided.

| Source precedent | Proposed place in Mind | What remains undecided |
| --- | --- | --- |
| 3.5 low-light vision / Darkvision progression | **Progressive Darkvision** beyond the native Dragonborn's existing Low-Light Vision. Source milestones: 30 ft initially, then 60/90/120 ft at 6/9/12 Hit Dice. | How to translate those distances and progression thresholds into native character-level scaling, particularly for characters reborn at high level. Do not grant redundant Low-Light Vision as a separate reward. |
| 3.5 Blindsense (30 ft at 15 Hit Dice) | **Advanced sensing** after Darkvision. | Its exact range, sense rules, interaction with Invisibility, total concealment, hiding, and whether it functions while blinded. Blindsense must not silently become Blindsight or Truesight. |
| 3.5 immunity to paralysis and magical sleep | Additional **defensive resilience** candidate, consistent with Mind. | Whether and when it manifests, exact native conditions/effect categories covered, and interaction with other conditions. |
| 5e champion immunity to Frightened | **Fear immunity** as part of the approved Mind capability direction. | Whether present immediately after rebirth or gained with progression; whether it applies to every fear effect or specifically the native Frightened condition. |
| 5e champion Legendary Resistance (1/day) | **Limited failed-saving-throw protection** as part of the approved Mind capability direction. | Player-facing expression and eligibility, use count, recharge, timing of invoking the effect, and initial-versus-later availability. **One/day and automatic success are source mechanics, not yet native approvals.** |
| 5e Aspect of Bahamut's Truesight and much broader immunities | **Higher-power reference only**, not presently in the approved Mind feature set. | Whether any advanced equivalents are needed at all. |

**Progression constraint:** All Platinum Dragonborn possess **Mind from rebirth**. That does **not** establish that every advanced sensory and defensive mechanic is granted immediately. A progressive profile should not reset an already high-level reborn character to level-1 functionality by default; the exact eligibility rule remains open. Keeping capabilities separate within one Mind expression permits coherent advancement without requiring a separate Mind class or three independent leveling tracks.

**Shared-champion observation:** The published Dragonborn of Tiamat and Dragonborn of Sardior NPCs also have fear immunity and limited Legendary Resistance. That supports evaluating those two defenses later as **general exceptional-champion features**, but does **not** automatically grant them to Fivefold Chromatic or Ruby Dragonborn or change the approved Platinum design direction.

**Boundaries:** Do not inherit NPC legendary-action rules, the full monster save profile, unrelated immunities, or a universal alignment-based condition rule. Native game-condition semantics and actual source-qualified player-character grants must be specified before implementation.

### Wings — next source-led design pass

**Wings:** Start with the original 3.5 progression of wings -> gliding -> restricted flight -> sustained flight, then compare the 5e champion's flying movement and the 5e Aspect's greater flying speed. Every Platinum Dragonborn inherently possesses Wings on rebirth, but its exact initial mobility and progression remain open. Do **not** presume the original 3.5 Hit Dice thresholds automatically apply to reborn characters at all levels.

**Preserve approved architecture:** All three expressions manifest together in exceptional Draconic Rebirth, including **one combined Heart breath with selective damage/healing and scaling effectiveness**. Neither ancestry Heart powers nor the overall Rite require becoming a Warlock, selecting a Vassal-inspired prestige class, or receiving a fixed contractual term. No specific level thresholds, die formulas, resource quantities/recharge schedule, spell-like effects or native executable data are approved by this design pass.

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

Heart's current **one combined damaging-and-healing breath** is supported by the separate 5e *Dragonborn of Bahamut* champion NPC and is now an **approved structural direction**, but not an approved numeric rules package. The original invented **Sovereign Breath** name, proposed effects and resource details were not separately approved, and the 3.5 Vassal features do not automatically become part of Heart. The boundary remains **inherent transformed characteristics versus externally granted patron benefits**.

## Research provenance and editorial boundary

Original inspiration: *Races of the Dragon* (3.5), **Dragonborn of Bahamut** (Heart, Mind, Wings selection and HD-based enhancements); and *Book of Exalted Deeds* (3.5), **Vassal of Bahamut** as a distinct prestige-class source. Retain these as **internal source references only**. Player-facing first-party rules should use descriptive category-leader identities and independently written lore, rather than naming or reconstructing a recognizable D&D-specific character portrayal. Retaining the broad design themes does not authorize copying source prose or unique expression.

## Questions for a later decision pass

1. What inherent feature best expresses **each** of Heart, Mind, and Wings, and how should their effects interact without redundancy?
2. For the **approved combined damaging-and-healing Breath Weapon**, how should it replace/enhance/coexist with the base and Metallic Breath Weapons? What damage affinity, shape, number of recipients, scaling formula, healing conditions, action, uses and recharge schedule should be selected?
3. For **approved Mind's combined sensory and resilience direction**, which Darkvision/Blindsense ranges and milestones apply? When do fear immunity and limited failed-save protection begin, how many uses apply, and are paralysis/magical-sleep protections also selected? Truesight is only a future candidate.
4. How should Wings adapt the 3.5 glide/flight progression for a character reborn at any level? Which abilities are available immediately and which scale?
5. Should the three expressions combine in a unique later ability, and what triggers advancement for characters reborn at different levels?
6. What small set of feature interactions provides the intended substantial power increase without creating redundant or contradictory feature packages?

**Implementation boundary:** No native rules JSON, executable code, API, Character Sheet integration, Warlock progression, or final ritual procedures are changed or authorized by this design record.
