# Pillar 2 — Rites of Rebirth and exceptional Dragonborn

**Date:** 2026-11-11  
**Status:** Owner-approved conceptual replacement rules and Dragonborn content direction. Ritual procedures, exact exceptional traits, data contracts, runtime behavior, and balance are not yet designed or implemented.

## Rite of Rebirth — general transformation rule

A **Rite of Rebirth** is a ritual that **completely replaces a creature's current Species and Ancestry configuration** with a new Species and Ancestry configuration. **Draconic Rebirth** is one specialization of this general class of rituals, not a separate character-creation or inheritance engine.

- **Complete configuration replacement:** The previous Species and Ancestry cease to be the source of the character's *current* inherent mechanical characteristics. All traits, proficiencies, affinities, resistances, intrinsic values, **creature type**, and other benefits granted **by the former Species or Ancestry** are replaced by those granted by the new configuration. A character does not retain an old trait or creature type solely because it once manifested or because a historical parent offered it.
- **Creature type follows the new configuration:** Rebirth replaces the creature type supplied by the previous Species/Ancestry with the creature type defined by the new Species/Ancestry. For example, a Humanoid Human reborn as a Red Dragonborn becomes a Dragon, not both Humanoid and Dragon. Independently sourced effects on creature type remain governed by their own rules; rebirth does not automatically erase effects unrelated to the former Species/Ancestry.
- **No accumulated Species/Ancestry packages:** Rebirth is not trait addition, cross-Species inheritance, or a mixed-Heritage candidate merge. A subsequent Rite replaces the then-current configuration as a whole, even if one Species label happens to remain the same. It must not collect earlier configurations' grants.
- **Independent grants remain independent:** A proficiency or benefit that still exists through an unrelated source (for example, Background, Upbringing, class, or training) is not removed merely because the old Species also granted a similar benefit. Attribution of a grant matters.
- **History does not change:** Biological parents and other historical contributors remain who they were. The original Species/Ancestry and the rite itself belong in the character's history, but historical records do **not** automatically restore old trait eligibility or alter the new manifestation.
- **Life experience does not reset:** Background and Upbringing remain unchanged. The character's origination and prior experiences are not rewritten by the physical or supernatural transformation.
- **Source-neutral framework:** Different rites specify their own target configuration, eligibility, participants, materials, prerequisites and procedure. No universal ritual requirements, durations, costs, power budget, or repeatability restrictions have yet been approved.

**Boundary:** A change that modifies only one trait or separately adjusts only Species *or* Ancestry is not the complete replacement defined here. Other transformation mechanics can be designed independently; this Rite of Rebirth framework must not silently treat partial changes as full rebirths.

### Illustrative outcomes (not new native records)

| Previous configuration | Specialized rite and source | New current Species/Ancestry |
| --- | --- | --- |
| Human | Draconic Rebirth involving a Red Dragon | Dragonborn / Red |
| Dragonborn / Red | Draconic Rebirth involving a Gold Dragon | Dragonborn / Gold, replacing Red traits |
| Any mixed Species/Ancestry configuration | Draconic Rebirth involving a Blue Dragon | Dragonborn / Blue; previous contributions remain historical |
| Dragonborn / Gold | Proposed exceptional Draconic Rebirth involving Bahamut | Dragonborn / exceptional Platinum |

These examples establish the **replacement behavior**, not the right of every named source to perform a ritual, an automatic grant, or finalized ritual procedures. The proposed dragon-to-result mapping extends the inspiration from *Fizban's Treasury of Dragons*' Draconic Rebirth gift and the earlier *Races of the Dragon* Rite of Rebirth; the Dorks & Dice general framework is an adaptation, not a claim that the published options are identical.

## Dragonborn exceptional Ancestries

The [three-factor formula](pillar2-dragonborn-expansion-dimensions-20261111.md) remains the content pattern for **ordinary** Dragonborn Ancestries: category, category-related skin material, and damage type. It does not require extraordinary category-leader Ancestries to follow the same pattern.

**Owner direction:** Exceptional forms are not normally available at initial character creation without DM approval. Their intended ordinary in-game acquisition path is an appropriate **Draconic Rebirth** rite requiring the **direct, personal participation of the corresponding category leader**. For the currently proposed forms, this means Tiamat for the composite Chromatic form and Bahamut for the singular Platinum form. Participation by another powerful dragon, an intermediary, or an artifact is not an automatic substitute. Future exceptional forms must author their own corresponding leader rather than relying on an unrestricted powerful-dragon threshold.

**Power intent:** Exceptional Dragonborn are intentionally and substantially more powerful than ordinary starting Dragonborn Ancestries. Equal mechanical strength with ordinary Ancestries is **not** a balancing objective. Restricted initial selection and the exceptional Rite are acquisition gates, not claims of mechanical parity. When designing these forms, consider the expected campaign power tier, party progression and playtest evidence without using ordinary character-creation balance as the required ceiling. The exact strength, features and progression remain open.

Two structural patterns are supported:

1. **Composite leader — Chromatic:** Inspired by Tiamat's five traditional Chromatic types (Black, Blue, Green, Red, White). An extraordinary form may combine characteristics of those **defined founding houses**, rather than recomputing its source list whenever new Chromatic Ancestries are published. The precise manifestations, limits, and whether multiple affinities can be selected or held simultaneously are **unresolved**. Composite does **not** mean an automatic union of all five complete trait packages.
2. **Singular leader — Metallic:** Inspired by Bahamut's Platinum identity. An extraordinary Platinum Ancestry is a **distinct singular material/type**, not a composite of Copper, Bronze, Brass, Silver, and Gold. Its damage affinity and exceptional abilities require separate design; no automatic access to all Metallic features or any particular new damage type is approved.

### Fivefold Chromatic — working mechanical concept (not approved)

The initial candidate is a **single exceptional Chromatic Dragonborn Ancestry** associated with Tiamat and the traditional Black, Blue, Green, Red, and White houses. This is a **design exploration only**; no trait, resource, resistance, or numeric progression is yet approved.

- **Breath Weapon:** Consider access to each traditional Chromatic damage affinity (Acid, Lightning, Poison, Fire, Cold) through one coherent Breath Weapon mechanic. Investigate additional power beyond merely selecting a damage type, since ordinary mixed Heritage can already combine eligible damage and whole-area choices.
- **Defenses:** Compare simultaneous possession of the five Chromatic resistances against a selectable or shifting affinity model. Simultaneous protection is a plausible exceptional-power option, **not an approved grant**.
- **Chromatic Warding:** Explore how the existing category ability might be extended or replaced in a form reflecting all five founding houses. No automatic grant of five independent Warding features or separate usage pools.
- **Strength target:** Exceptional ancestry is **intentionally substantially stronger** than standard Dragonborn; compare its power with the intended campaign tier and party progression, not ordinary starting-ancestry parity.
- **Acquisition:** The general restriction and **Tiamat's direct participation** in its Draconic Rebirth remain established; these tentative abilities do not change those requirements.

**Historical design reference for later Platinum work:** The existing original Dragonborn notes at `data/rulesets/first-party-001/working/species/subspecies/dragonborn/SHARED_BREATH_REFERENCE.md` explicitly discuss *Dragonborn of Bahamut* from *Races of the Dragon* (3.5), including the Heart aspect's round-based Breath Weapon recharge. That 3.5 transformation also offers **Heart, Mind, or Wings** as an aspect choice with distinct advancement, a useful design precedent for exceptional ancestral manifestations, **not adopted Dorks & Dice mechanics**. The same legacy notes separately defer **Dragonfire Adept-derived feat/prestige-class** concepts; Dragonfire Adept is not itself the Dragonborn of Bahamut ancestry or its transformation. Another historical Bahamut-linked prestige class is **Vassal of Bahamut** (*Book of Exalted Deeds*); its mechanics have not been selected for this ancestry. In particular, the original 3.5 transformed race's partial retention of prior racial characteristics and Humanoid (dragonblood) type must **not** silently override our separately established **complete Species/Ancestry and creature type replacement**.

Other Dragonborn categories may later use composite, singular, or different exceptional patterns, or have no exceptional Ancestry. Sardior/Ruby for Gem Dragons is a possible future example, **not an approved playable record or trait specification**.

**Separation from ordinary mixed Heritage:** The already approved ordinary Breath Weapon rule permits independent selection of a contributed damage type and **complete** area. That rule continues to apply to eligible ordinary mixed-Heritage candidates; it does not give a player access to an exceptional Ancestry, merge five separate Breath Weapons, or resolve exceptional abilities.

## Pillar 2 representation consequences (design requirements, not finalized schema)

1. Track **historical origination/contributors** separately from **current Species/Ancestry** and their manifested characteristic provenance. A rebirth can change the latter without modifying the former.
2. The prospective Rite operation needs explicit **complete replacement semantics** and source-qualified removal/retention of grants, not the general inherited-candidate pool's merge procedure.
3. Make **ordinary versus exceptional** eligibility explicit in authored content; material names or compatible damage types alone must not accidentally unlock ritual-only options through ordinary creation or mixing.
4. Preserve the specific exceptional form's authored identity and constituents; do not derive a composite's membership dynamically from every new category leaf.
5. Reuse source-neutral characteristic definitions and rules where appropriate. No dedicated Dragonborn-only Origin engine is needed, but ritual eligibility and transformation must be general-purpose concepts.
6. Neither the present read-only Pillar 2 profiles nor the existing native catalogs currently execute rebirth, remove grants, or enforce ritual-gated character creation.

## Unresolved design decisions

- General Rite of Rebirth procedure, costs, timing, failure conditions and reversibility; the exceptional Draconic Rebirth requirement for direct category-leader participation is established, but the remaining details of that participation are not.
- Exact Dragonborn exceptional traits, damage choices and resistances, balancing, and interactions with standard category powers.
- Final persistent schema and rules-engine representation for successive rebirths and their historical record.
- Whether more Dragon categories receive leader-associated forms and how those are acquired.

**Scope:** Design record only. No first-party Species/Ancestry content changes, character creation permissions, runtime APIs, combat rules, or ritual execution are authorized by this document.
