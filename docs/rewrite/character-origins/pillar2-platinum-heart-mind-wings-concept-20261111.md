# Pillar 2 — Platinum Dragonborn: Heart, Mind, and Wings (working concept)

**Date:** 2026-11-11  
**Status:** Owner-approved three-expression identity (all three inherent to every Platinum Dragonborn); mechanical feature candidates, advancement, and numeric values remain unapproved. Not a native playable Ancestry. Preserve the approved exceptional Dragonborn acquisition/rebirth boundary in [Rites of Rebirth](pillar2-rites-of-rebirth-20261111.md).

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
| **Heart** | The sovereign's draconic essence and power projected into the world | A **distinct Platinum Breath** or enhancement to the existing single Breath Weapon that can both exert force against threats and project a protective effect; a personal aura or ward reflecting the same heart rather than a second unrelated pool of breath attacks |
| **Mind** | Perception, awareness, and command of one's own will | Senses beyond ordinary Low-Light Vision, potentially detecting threatening draconic/magical phenomena; improved defenses against hostile mental effects; a limited anticipatory awareness effect with a defined trigger rather than omniscience or automatic mind reading |
| **Wings** | Majestic physical manifestation and influence over aerial space | Permanent wings and flight, eventually enhanced maneuverability or control of movement; an active wing-based ability that can protect or reposition others, so Wings remains significant even after ordinary flight is commonplace |

These are **mechanical roles and possible expressions**, not numeric choices, exact conditions, damage types, guaranteed immunity, specified ability resources, or a fixed moral/alignment profile. In particular, radiant damage, broad healing, a permanent protective aura, and truesight are **not automatically approved** by invoking a platinum theme.

### Heart design pass — Sovereign Breath (working candidate; no mechanics approved)

**Design target:** Heart should visibly represent the Platinum sovereign's **exceptional power and protective influence**, not only a stronger ordinary elemental attack. The **single coherent Breath Weapon** remains the preferred starting unit to study; do not treat the different outputs below as multiple separately earned Breath Weapons or automatic independent usage pools.

Three alternatives considered:

| Alternative | Benefit | Main concern | Disposition |
| --- | --- | --- | --- |
| Stronger damaging breath alone | Straightforward link to the existing breath | Magnitude increases without an exceptional identity | Weak fit |
| Choose damage *or* protection each use | Clear choice and relatively simple resolution | Could resemble another ordinary Metallic secondary-breath mode | Viable fallback |
| **Sovereign Breath: selective destructive/protective projection in one use** | Distinct battlefield identity; both overwhelming force and selective protection | Requires careful area/target/resource contract; high combined action value | **Preferred working candidate**, not approved |

**Illustrative usage, not a finalized rule:** On the Platinum Dragonborn's turn, a single breath manifestation projects over an authored area. The player designates eligible creatures for a **destructive** effect or a **protective** effect, without relying on inferred alignment, moral status, or whether the creature is an ally or enemy. An affected creature would ordinarily receive one designated output rather than stacking both. A single activation could strike chosen opponents while shielding chosen companions in the same area. This is a mechanical example only; exact eligibility, geometries, saving throws, damage, ward effect, duration, resources and activation timing remain open.

**Identity distinctions from native records:**

- Base Dragonborn Breath Weapon already replaces **one attack** during the Attack action, deals ancestry-linked damage, and has Proficiency Bonus uses per Long Rest. Sovereign Breath might **enhance/replace that one entitlement** and preserve one shared resource rather than adding a separate damage pool. The use/action contract must be decided explicitly.
- Ordinary Metallic Breath Weapon is a **different** level-5, once-per-Long-Rest ability with Enervating/Repulsion options, affecting allies too. If Platinum is modeled under the Metallic family, its inherited secondary Breath must be explicitly **retained, superseded, or integrated**; it cannot be silently duplicated or dropped. None of those relationships is approved by this proposal.
- The native base Breath Weapon and Draconic Resistance both derive their damage type from the **selected ordinary Dragonborn leaf**. Platinum's singular identity does **not** yet define an affinity. Its own explicit ancestry value or source-qualified override will be required before proposing an executable definition. **Radiant, force, or any ordinary metallic damage affinity is not selected** by this design pass.
- A shield could mean temporary hit points, one-use damage prevention, a limited damage reduction, or a bounded protective condition. These have different timing, stacking and balance consequences. Do not automatically equate protection with healing, universal immunity, or granting an existing resistance to every ally.
- This ancestry is supposed to be **substantially more powerful** than ordinary Dragonborn. That permits a strong combined ability but does not remove the need to define who is affected, once-per-use accounting, whether effects repeat on multiple attacks, and whether multiple overlapping wards stack.

**Heart's relationship to Mind and Wings:** Mind may later make threats easier to identify and distinguish; Wings may later improve positioning and spatial reach. **Neither expression should be a prerequisite to using Heart**, because all three manifest together at rebirth. A combined advanced ability can be designed separately once each expression's actual mechanics exist.

**Open approval gates:** (1) Is selective simultaneous damage-and-protection the desired core Heart identity? (2) Is protection an effect of Sovereign Breath, a separate persistent/activated Heartward, or both? (3) Does Platinum explicitly replace the ordinary Breath Weapon and/or Metallic secondary breath? (4) What affinity, damage/ward effects, activation, area, and scaling are authored? No answer is implied by the sample.

### Unifying ability: all three expressions acting together (candidate)

A later feature could combine **Heart** (projects a protective or forceful effect), **Mind** (identifies or anticipates the relevant threat), and **Wings** (interposes or directs the effect across space). This is one possible way to represent the sovereign as a whole, rather than assigning three disconnected abilities. No unique action, reaction, cost, timing, duration, targeting, or power tier has been selected.

## Approved expression structure and pending feature design

**Every Platinum Dragonborn has all three expressions: Heart, Mind, and Wings.** The Rite grants the complete Platinum ancestry; there is **no one-of-three choice** and no need to select a preferred expression to receive the others. All three must be represented in its eventual authored mechanical identity.

This decision does **not** yet approve any exact abilities, numerical effects, activation requirements, trait resources, or level thresholds. A future proposal could investigate extra customization within an expression, but no emphasis/specialization choice, competing progression trees, or substitute aspect path is part of the current approved design. Avoid presenting them as default acquisition options.

## Timing and cross-pillar separation

- **Rebirth is the acquisition event**, not a Warlock level, prestige-class level, or inherited-characteristic recombination step. Its Species/Ancestry and creature type replacement semantics are already established.
- The ordinary native Dragonborn uses **character-level scaling**. Scaling an exceptional trait by character level would also permit a high-level character completing a Rite to enter with a high-level manifestation rather than restarting from level 1. Whether Platinum progresses immediately with character level, with subsequent rites/achievements, or by another independent mechanism is an unresolved design question.
- **Ancestry** should express what the creature has become. **Vassal of Bahamut** (*Book of Exalted Deeds*) is a separate historical prestige-class reference, including social standing, equipment, protective themes, and anti-dragon features; service, vows, weapons, treasure or a specific class progression must not automatically become inherited Platinum biology.
- A prospective **Draconic Warlock Patron** may reuse broad dragon-themed expressions through its own negotiated arrangements and source-specific progression. It is **only a future idea**, not an approved Patron or a prerequisite to Platinum rebirth. Warlock Patron, Pact, Invocations, and the Rite remain distinct systems.
- Do not automatically confer a personality, alignment, duty, loyalty, or Upbringing because of the new Ancestry. The Metallic leader's direct participation does not by itself determine contractual terms.

## Research provenance and editorial boundary

Original inspiration: *Races of the Dragon* (3.5), **Dragonborn of Bahamut** (Heart, Mind, Wings selection and HD-based enhancements); and *Book of Exalted Deeds* (3.5), **Vassal of Bahamut** as a distinct prestige-class source. Retain these as **internal source references only**. Player-facing first-party rules should use descriptive category-leader identities and independently written lore, rather than naming or reconstructing a recognizable D&D-specific character portrayal. Retaining the broad design themes does not authorize copying source prose or unique expression.

## Questions for a later decision pass

1. What inherent feature best expresses **each** of Heart, Mind, and Wings, and how should their effects interact without redundancy?
2. Should the proposed **Sovereign Breath** affect selected creatures destructively and protectively in one use, and how should it explicitly relate to the base and Metallic Breath Weapons? What affinity, ward effect, geometry, action and resource does it require?
3. Does Mind emphasize sensing, anticipation, will, or a linked progression without making all special senses permanently available?
4. Does Wings grant permanent flight immediately on rebirth, and what secondary wing ability makes it distinctive?
5. Should the three expressions combine in a unique later ability, and what triggers advancement for characters reborn at different levels?
6. What small set of feature interactions provides the intended substantial power increase without creating redundant or contradictory feature packages?

**Implementation boundary:** No native rules JSON, executable code, API, Character Sheet integration, Warlock progression, or final ritual procedures are changed or authorized by this design record.
