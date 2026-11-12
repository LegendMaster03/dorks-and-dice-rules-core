# Pillar 2 — Platinum Dragonborn: Heart, Mind, and Wings (approved design)

**Date:** 2026-11-11  
**Status:** **OWNER-APPROVED PLATINUM MECHANICAL DESIGN** (2026-11-11). Heart, Mind and Wings, their numerical progressions, corresponding native baseline replacements, and total-character-level rebirth semantics are selected in the authoritative specification immediately below. **Implementation is pending** the broader Character Origins/Pillar 2 integration. Optional involuntary-fall protection and Rite resource carryover remain unapproved. Preserve the approved exceptional Dragonborn acquisition/rebirth boundary in [Rites of Rebirth](pillar2-rites-of-rebirth-20261111.md).

## Approved Platinum mechanics — authoritative design

**Owner approval (2026-11-11):** This entire consolidated mechanical package is **approved as the Platinum design**, subject only to the exceptions explicitly marked as deferred below (such as ritual resource accounting and the optional involuntary-fall damage cap). Heart, Mind, and Wings all use **total character level**, including when a character is reborn at an advanced level. This is a finalized *design specification*, **not yet an implemented/selectable native record**. Extraordinary Platinum is not constrained by ordinary starting-ancestry power parity and ordinarily requires the Metallic sovereign's direct personal participation in an exceptional Rite, subject to express DM authorization.

### Approved Platinum ancestry inheritance

- The Platinum record is a **singular exceptional Dragonborn leaf directly under Dragonborn Species**, not a standard Metallic leaf inheriting the ordinary Metallic family's secondary Enervating/Repulsion Breath. Its thematic family is Metallic, but it does **not** inherit all ordinary Metallic abilities simply because of that label.
- Keep the Dragonborn Species' **Dragon creature type, Medium size, 30-foot walking speed, and Low-Light Vision**, unless later independent species work changes them.
- **Replace the ordinary Dragonborn Breath Weapon** with the one exceptional combined Heart breath below, rather than stacking two damaging breaths or creating an additional independent breath resource. **Do not inherit ordinary Metallic secondary Breath** for this exceptional leaf; this keeps the approved **single breath attack** straightforward. This parent/trait-resolution relationship is part of the approved Platinum design; applying it to native catalogs remains deferred until Pillar 2 integration.
- The base Species' **50% Draconic Resistance** depends on a leaf-defined affinity. **Platinum has explicit radiant affinity and 50% radiant resistance** rather than inheriting an ordinary Metallic affinity. Radiant is adapted from the 5e Dragonborn of Bahamut champion.
- The base record's traits must be **source-overridden deliberately**; a direct ancestry parent alone must not produce **two overlapping Breath Weapon grants**.

### Heart — one Breath Weapon with selective healing

| Concern | Approved mechanic | Basis |
| --- | --- | --- |
| **Area** | **30-foot cone** from the Dragonborn | Adapted from the 5e champion rather than ordinary Metallic 15-foot cone |
| **Activation** | On your turn, **replace one attack of your Attack action** | Reuses the existing native Dragonborn action contract |
| **Save** | All damaged creatures in the cone make a **Dexterity** save against **8 + PB + Constitution modifier**; success takes half damage | Reuses the native save formula |
| **Damage** | **Radiant** damage: character levels **1–4: 2d10**, **5–10: 4d10**, **11–16: 6d10**, **17+: 8d10** | Double ordinary Dragonborn dice; *not* NPC 8d10 at all levels |
| **Restoration** | When using the breath, designate **up to three other creatures in the cone**. They take **no damage from that breath** and instead regain HP: **1d10 / 2d10 / 3d10 / 4d10** at the same level thresholds | Source-inspired recipient count, half the damage dice |
| **Target semantics** | Chosen recipients are selected by the player, irrespective of creature alignment, ally/enemy relationship, or prior conditions; chosen recipients are healed **instead of** damaged. Other creatures in the area, including allies not chosen, face the save/damage. | One area, activation and output-selection contract |
| **Healing cap** | Healing does not exceed the target's HP maximum; can restore a living creature at **0 HP**, but does **not revive a dead creature**, cure conditions, grant temporary HP, or heal the user unless within the authored area | No resurrection or independent Healing Touch |
| **Use resource** | **PB uses per Long Rest**, shared across all uses of this one breath | Native Dragonborn model; not separate NPC Recharge 6 |
| **Resistance** | Platinum's innate resistance is **50% radiant** (not permanent immunity) | Explicit singular-ancestry affinity |

**One-attack consequence:** Replacing one attack, rather than using a full separate action, is intentional because ordinary Dragonborn already use that contract. **One use produces only one 30-foot cone**, even when the character has Extra Attack, Action Surge or similar effects; taking a separate legal Attack action could permit another use if resources remain. Prevent a single attack replacement from duplicating the area or reapplying healing. Existing ordinary Metallic secondary breath is excluded from the approved singular Platinum leaf.

**Balance visibility (averages on dice, before saves/resistance):**

| Total level | Damage to each nonselected creature on failed save | Healing to **each** of up to 3 selected creatures | Uses/Long Rest |
| --- | --- | --- | --- |
| 1–4 | 11 | 5.5 | PB (usually 2) |
| 5–10 | 22 | 11 | PB (typically 3–4) |
| 11–16 | 33 | 16.5 | PB (typically 4–5) |
| 17+ | 44 | 22 | PB (typically 6) |

This is **substantially stronger** than ordinary Dragonborn (1d10/2d10/3d10/4d10), because each use may also heal up to three creatures. That power difference is intentional; playtesting can later justify balance adjustments without reopening the conceptual design by default.

### Mind — senses and resilience as character-level abilities

| Total character level | Approved innate Mind benefits |
| --- | --- |
| **1+** | **Darkvision 30 ft** in addition to native Low-Light Vision; immunity to **fear-originated impairments** (subject to native fear-family translation), **Paralyzed**, and magical sleep effects, following the two source precedents |
| **6+** | Darkvision increases to **60 ft** |
| **9+** | Darkvision increases to **90 ft**; **one failed saving throw per Long Rest may instead succeed**, declared after a failure (limited champion-style protection, not NPC Legendary Actions) |
| **12+** | Darkvision increases to **120 ft** |
| **15+** | **Blindsense 30 ft**, not Blindsight or Truesight |

**Sense contract:** Darkvision does not displace Low-Light Vision, but its actual light/darkness behavior must match the authoritative Dorks & Dice sense rules. Blindsense indicates the **location/presence** of a creature within 30 ft when ordinary sight is insufficient, subject to occlusion and environment rules; it is not equivalent to seeing a creature, automatically bypassing hidden/invisible-target restrictions, or making the creature immune to blindness. Precise authored rule references and concealment handling must be reconciled before native implementation.

**Defense contract:** The native fear-family rule distinguishes **Shaken**, **Frightened**, **Panicked**, and **Cowering** rather than collapsing them into a single 5e Frightened condition. The approved adaptation is **immunity to fear-sourced conditions/effects** across this family, while preserving Cowering or another condition imposed for an independent nonfear reason. This adaptation is **not a claim that the 5e champion explicitly lists four immunities**. Paralyzed immunity should reference the native condition. Immunity to *magical sleep* needs an explicit rule effect/category; it is **not** immunity to ordinary rest, unconsciousness, every effect that renders a character unconscious, or every charm/mind-control effect. The saving-throw intervention is **once per Long Rest**, usable only after the result would fail, without a separate Action; it should not negate a roll that was never a saving throw. Condition terminology and ability triggers must be validated against native authoritative IDs.

**Not part of this proposal:** blanket Truesight, every Legendary Resistance from the 5e monster Aspect, universal condition immunity, spellcasting, or weapon flashes. Future generalized exceptional-champion content may also consider fear immunity and limited save protection for other leaders, but this does not automatically grant those features to Chromatic or Ruby.

### Wings — approved levels and numerical movement rules

The **1–5 / 6–11 / 12+ stages**, **total-character-level metric**, and following movement numbers and limits are approved:

| Total level | Approved mobility |
| --- | --- |
| **1–5** | Wings can **glide at up to 30 feet of horizontal movement per turn** while descending at least **5 feet for each 20 feet traveled horizontally**. Controlled gliding prevents falling damage on a safe landing and requires space to deploy wings. **No powered ascent, hover or sustained flight** |
| **6–11** | Gain a **30-foot flying speed** for a period of **up to 1 minute**, usable **once per Short or Long Rest**. Once spent, the character can still glide. Do not copy the original 3.5 fatigue/Constitution-round/10-minute-per-day bookkeeping into a new condition system |
| **12+** | The **30-foot flying speed** becomes **continuous** with no special duration or usage restriction; gliding and controlled landing remain available |

**Flight edge handling:** Gliding is **not** hovering. When limited powered flight ends in midair, the character may continue to glide while descending; this is not levitation. A creature blocked from moving by a condition still obeys that condition; no special permission to move through obstacles or hover is implied. Existing carrying/encumbrance and movement limitations apply. **Deferred:** exact handling when incapacitated in midair, including the previously floated optional **1d6 maximum involuntary-fall damage**, is not part of this approval and must be reconciled with general falling rules before implementing it.

**Reason for simplifying early flight:** The original 3.5 rule used Constitution-modifier rounds, fatigue and a cumulative daily flight budget. The **approved** single 1-minute use per Short or Long Rest preserves the limited-versus-sustained distinction while using an established rest-based resource model. Historical fatigue is **not** an additional native cost.

### Rebirth at any level — approved progression semantics

At the **instant the completed Rite takes effect**, replace the entire former Species/Ancestry-derived intrinsic package with the Platinum Species/Ancestry configuration, including creature type and source-qualified traits. Resolve **every level-gated Platinum benefit against current total character level**, not the old Species' Hit Dice, the source creature's CR, Warlock level, the time since transformation, or a newly created ancestry level.

- **Level 3 rebirth:** Combined Heart Breath at **2d10 damage/1d10 healing**, Mind Darkvision 30 ft and initial defenses, Wings gliding.
- **Level 8 rebirth:** Combined Heart Breath at **4d10/2d10**, Mind Darkvision 60 ft and initial defenses, Wings limited flight.
- **Level 15 rebirth:** Combined Heart Breath at **6d10/3d10**, Mind Darkvision 120 ft, Blindsense 30 ft, one failed-save intervention per Long Rest, Wings sustained flight.
- **Level 20 rebirth:** Combined Heart Breath at **8d10/4d10**, Mind's full proposed senses/defenses, Wings sustained flight.

These examples illustrate the **approved Platinum design**, but **do not claim that the native runtime currently implements rebirth or these features**. The original parents, Heritage contributors, Background and Upbringing remain historical/independent and **are not rewritten**. A DM-authorized exceptional starting character uses the same current-level rules. Ordinary mixed-Heritage eligibility remains gated against automatically selecting the complete exceptional Platinum package.

**Usage resources on rebirth:** The rules engine must explicitly establish how already-spent Species/Ancestry resource counts are replaced, including preventing a second full allocation by repeatedly transforming during a single rest cycle. **Recommended anti-exploit rule for review:** when a rebirth occurs during an active rest interval, the new breath and limited flight start with their full capacity **minus the amount already spent from mechanically corresponding old grants in that same interval, capped to the new capacity**, unless the Rite explicitly restores them. Different resource entitlements should not be conflated solely because they are sourced from Species. This accounting remains unapproved and may require generalized provenance support.

### Closing disposition and remaining integration boundaries

**Approved:** All three aspects acquired together; the singular Platinum ancestry/content structure and radiant resistance; Heart's combined damaging/healing breath and its full numeric/target/resource progression; Mind's Darkvision/Blindsense milestones, fear-family and paralysis/magical-sleep protections, and single Long-Rest failed-save intervention; Wings' three stages, speeds, glide/descent rules and rest-based limited flight; **all features scale by total character level**, including the moment of late rebirth.

**Still pending implementation-level reconciliation, not reopening Platinum's approved feature package:** Native feature/condition/sense IDs, the mechanical-sleep effect category, movement/fall handling if a creature becomes incapacitated, and resource carryover or expenditure when a Rite changes the intrinsic package partway through a rest cycle. The optional 1d6 involuntary-fall cap and proposed resource anti-exploit calculation below are **not approved**.

**Deliberately not included:** Vassal-derived patron/service powers, Healing Touch as a separate action, automatically granted resurrection, 5e NPC Multiattack/equipment/alignment/CR or independent mythic phases. Rebirth and Draconic Patron remain separate; no hard two-path narrative enforcement or unrestricted mixed-ancestry access is introduced. No further Platinum-specific feature families are needed to complete this ancestry concept unless playtesting identifies a real gap.

**Implementation boundary:** The Platinum **design is approved**, but this commit is documentation-only. Native ancestry records, ordinary mixed-Heritage exclusion, rebirth engine/resource handling, executable combat rules and validation tests require the planned broader Pillar 2 integration. Exceptional Platinum is still **not** an ordinary character-creation selection without explicit DM authorization. No main-branch merge or native gameplay change is made by this decision.


## Prior design history and source comparisons

**Historical record:** The following exploratory discussions predate the final approval above. Any statement that mechanics, values, or progression were still candidates is superseded by the authoritative approved specification. The separate Vassal/patron discussion and the distinctions between published NPC mechanics and this ancestry remain relevant.

### Purpose

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

### Wings — approved character-level progression structure (mechanics pending)

**Owner-approved Wings progression (2026-11-11):** Translate the three historical stages from *Races of the Dragon* (3.5), **Dragonborn of Bahamut**, into **total Dorks & Dice character level**, not Hit Dice or class-specific levels. All Platinum Dragonborn receive the Wings expression upon exceptional Draconic Rebirth. The **current total character level** determines the immediately available flight stage:

| Total character level | Approved capability stage | Source relationship |
| --- | --- | --- |
| **1–5** | **Gliding**: physical wings permit controlled descent and horizontal gliding, but not powered/sustained flight | Original 3.5 Wings stage from 1–5 total Hit Dice |
| **6–11** | **Limited powered flight**: active flying is possible, subject to an endurance/duration limitation; gliding remains available | Original 3.5 flight unlock at 6 Hit Dice |
| **12+** | **Sustained flight**: the special endurance limitation from the intermediate stage no longer applies | Original 3.5 unrestricted sustained-flight unlock at 12 Hit Dice |

**Late rebirth and advancement:** The character does **not** restart Wings progression at its first stage merely because the Rite happened late. A character reborn at level 15 immediately qualifies for the **12+ sustained-flight stage**. Subsequent character-level advancement changes the permitted stage without new ancestry selection or a separate Wings level.

**Not yet approved:** Exact gliding speed, vertical-to-horizontal travel ratio, carrying restrictions, fall prevention, flying speed, maneuverability, flight-duration/fatigue formula for levels 6–11, midair failure/consciousness rules, and interactions with conditions and the movement system. The original 3.5 rules (30-foot glide/fly speed, glide ratio, constitution-based endurance, fatigue, etc.) are *research precedents*, **not automatically imported values**. The 5e Dragonborn of Bahamut NPC's 30-foot flight is a cross-edition comparison, not an override of this approved staged progression.

**Scaling consistency:** Wings advances by **total character level**, consistent with the existing native Dragonborn Breath Weapon's character-level scaling and with a source-neutral rebirth model. This decides the **metric and stage thresholds for Wings only**; it does not yet approve copying the Wings thresholds to Mind or Heart, which each have their own unresolved numerical progression.

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

## Archived research questions (superseded by approved specification)

These questions reflect the earlier exploratory pass. The approved rules in the first section resolve the numeric and feature choices; they are retained only for provenance and must not be treated as current blocking decisions.

1. What inherent feature best expresses **each** of Heart, Mind, and Wings, and how should their effects interact without redundancy?
2. For the **approved combined damaging-and-healing Breath Weapon**, how should it replace/enhance/coexist with the base and Metallic Breath Weapons? What damage affinity, shape, number of recipients, scaling formula, healing conditions, action, uses and recharge schedule should be selected?
3. For **approved Mind's combined sensory and resilience direction**, which Darkvision/Blindsense ranges and milestones apply? When do fear immunity and limited failed-save protection begin, how many uses apply, and are paralysis/magical-sleep protections also selected? Truesight is only a future candidate.
4. For the **approved Wings stages at character levels 1–5, 6–11 and 12+**, what speed, gliding descent ratio, limited-flight endurance, fall protection and movement restrictions should apply? Which original 3.5 values, if any, should be adapted?
5. Should the three expressions combine in a unique later ability, and what triggers advancement for characters reborn at different levels?
6. What small set of feature interactions provides the intended substantial power increase without creating redundant or contradictory feature packages?

**Implementation boundary:** No native rules JSON, executable code, API, Character Sheet integration, Warlock progression, or final ritual procedures are changed or authorized by this design record.
