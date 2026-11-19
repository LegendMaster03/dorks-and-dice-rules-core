# Psionics foundation — research and Ruby Dragonborn dependency

**Date:** 2026-11-11  
**Status:** **PARKED / OUT OF SCOPE for the current Character Origins Pillar 2 stage (2026-11-11).** Historical research and architecture options only; this document does not approve psionic mechanics, resource pools, class features, Ruby ancestry traits or native implementation. **Ordinary Gem leaves are temporarily `selectable: false` until a separate future psionics pass and explicit re-enablement.** Its research sequence is not a current project obligation.

## Why this is a shared Rules Core dependency

Ruby's historical sovereign and champion sources span mental communication, perception, telekinesis, mental defense, energy manipulation, psychic projection, and movement. Designing these as arbitrary Ruby-only features risks contradicting later Psion classes, creatures, feats, and items. **Ruby should use a shared psionic effects model**, but a complete power catalog or full Psion class is **not** a prerequisite to choosing Ruby's features.

### Existing authoritative content

| Existing piece | Repository evidence | What is missing |
| --- | --- | --- |
| **Psionic Mind** | `data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json`: one-way message to one visible creature within 30 feet that understands a language, no thought reading or reply | General semantics for different authored telepathic links, reciprocal communication and mind reading |
| **Gem Flight** | Same file: level-5 hovering flight, one minute once/Long Rest | No shared psionic levitation or psychoportation power definition; Gem Flight remains a specific granted trait |
| **Psionic-associated skills** | `data/rulesets/first-party-001/native/skills.json` and `docs/competency-reconciliation-audit.md`: Knowledge (Psionics), Psicraft and Use Psionic Device; also Psionics naming | Qualification, knowledge checks, skill overlap and device-operation procedures must be audited rather than assumed |
| **Psychic damage and creature telepathy** | Native bestiary, working rules and the damage taxonomy | Psychic is a **damage type**, not a universal psionic source tag; creature telepathy has different limitations across individual entries |
| **Casting resource separation** | `docs/rewrite/native-rules-design-specification.md`, spell slots/optional spell points and separate Sorcery Points | A Psion's **power points** or a Psi Warrior's **energy dice** must not silently merge with spell points or other class-specific pools |
| **Shared actions and conditions** | Native conditions, Held/Pinned and spellcasting/feature rules | Define how purely mental powers operate when physically constrained, and why an Unconscious/Incapacitated user does not automatically become able to act |

**Inspection finding:** The files and repository search results reviewed contain no identified **selected-native generalized psionic manifestation contract** for power definition, grant authorization, resource/augmentation resolution, psionics–magic interaction and advancement. This is a scoped inventory, not a claim that no experimental psionics notes exist.

## Mechanical source models

### 3.5 SRD / Expanded Psionics Handbook

- Six traditional disciplines: **Clairsentience, Metacreativity, Psychokinesis, Psychometabolism, Psychoportation, Telepathy**. A discipline groups powers by purpose, rather than implying a separate resource pool per group.
- **Power points** and **manifester level** govern repeated powers and augmentation. A power's optional augmentation is limited by source-defined costs and manifester level, not a universal license to spend unlimited points.
- The **default psionics–magic transparency** policy generally permits spells and powers to interact through resistance, dispelling, detection and suppression, while describing exceptions. A Dorks & Dice policy remains to be selected.
- Distinguish individually granted **psi-like/innate** abilities from a class's **known powers**, point pool and advancement. Having psionic features does not automatically make an ancestry a Psion.

Primary references: https://www.d20srd.org/srd/psionic/psionicPowersOverview.htm ; https://www.d20srd.org/srd/psionic/classes/psion.htm ; https://www.d20srd.org/srd/psionic/powerList.htm .

### 2024 Psi Warrior and Soulknife

These subclasses use **Psionic Energy Dice** as subclass-specific resources. They show how mental features may share ordinary action and damage rules without using 3.5's power-point subsystem. Do **not** make dice a universal requirement for innate Ruby traits or add the same dice automatically to other characters.

Reference: https://roll20.net/compendium/dnd5e/Subclasses%3APsi%20Warrior?expansion=32231 ; official 2024 *Player's Handbook*.

### 2025 Unearthed Arcana Psion

The playtest explores **spell-based** psionic manifestations, distinct class features and Wild Talents. It is useful for alternative modern architecture but remains **playtest evidence**, not automatically approved first-party Dorks & Dice rules.

References: https://media.dndbeyond.com/compendium-images/ua/the-psion/mXCPWlh2yy5tBKqP/UA2025-ThePsion.pdf ; https://www.dndbeyond.com/posts/2072-designer-insights-from-unearthed-arcana-psion .

## Foundation decisions to make before Ruby

| Decision | What must be established |
| --- | --- |
| **Power ontology** | Is psionic origin a tag on the existing shared action/effect system, or does it require a wholly separate engine? Recommended for evaluation: **tag plus shared resolver**, retaining distinct powers and capabilities. |
| **Magic interaction** | Choose the default transparency policy for detecting, resisting, countering, dispelling and suppressing psionic manifestations. It must distinguish ongoing effects, innate passive traits and supernatural abilities. |
| **Authorization and source** | Which particular ancestry/class/feat/item/creature grants a power? A skill, psionic heritage or creature type alone must not confer a complete power list. |
| **Resources** | Support **at-will**, limited uses/rest, independently granted power points, independently granted psionic dice and explicitly authorized spell resources. These are not interchangeable by name. Do not select point/die quantities yet. |
| **Advancement** | **Total character level** for approved origin progressions such as Ruby; potentially class/manifester level for class-granted powers. Preserve explicit source-defined exceptions. |
| **Classification** | Consider the six historical disciplines as extensible **tags**, not six mandatory exclusive class choices or a new damage taxonomy. |
| **Activation and limitations** | Define mental versus physical components, Actions/Bonus Actions/Reactions, concentration, targeting, visibility, range, duration and the conditions that block activation. |
| **Interactions and stacking** | Prevent mental-defense AC formulas, telepathy modes, protections and power resources from stacking or replacing one another incorrectly. |

### Recommended architecture to evaluate, not an approved decision

**Shared effect resolution with explicit psionic origin metadata and separately authorized resources.** This fits Rules Core's source-of-truth architecture, avoids reimplementing the same targeting/damage/save logic, and supports both augmentation-based 3.5 powers and modern limited-use/dice abilities. Psionic damage need not be Psychic damage: a psionic power may deal Force, Fire or another specified type.

A manifestation definition should be able to identify **origin, power/discipline identifiers, grants, trigger/action, targets and visibility, save/DC, damage or non-damage outcome, duration/concentration, resource/cost, scaling, and explicit interaction exceptions**. Not every entry needs every field. Innate telepathy may be at-will; a class may require points; an object may have charges. **None of these defaults have been approved.**

## Parked future research and implementation sequence

1. **Reconcile existing content and sources.** Audit native psionic-associated skills, Gem Psionic Mind, psychic damage, creature telepathy, current spellcasting resources and the published 3.5/2024 source models.
2. **Approve the shared foundation.** Establish a power descriptor, effect semantics, magic-interaction policy, resource/augmentation separation and source-specific advancement. Make the written rules manually interpretable, not engine-dependent.
3. **Test one small vertical slice.** Use existing Gem Psionic Mind as a baseline and explore telepathy, mental shielding, telekinesis, psychic projection and sensory interference as **reusable effect families**, without granting these powers to Ruby. Test action restrictions, concentration, dispelling, level scaling and source qualification.
4. **Return to Ruby.** Select Ruby's breath, psionic powers, Mental Defense, hover progression and resilience by comparing [the first-party Ruby source corpus](character-origins/pillar2-ruby-dragonborn-source-corpus-20261111.md) against the accepted shared subsystem. Ruby remains a **singular exceptional Gem ancestry** acquired through Rite of Rebirth, not a combination of ordinary Gem leaves or an entire Psion class.
5. **Expand later.** Full Psion/Psychic Warrior classes, complete 3.5 power catalogs, advanced augmentation and metapsionic feats, psionic equipment and focus systems should be reconciled as needed after the coherent shared framework, not silently dragged into Ruby's initial feature grant.

## Constraints

- Ruby has **no approved features yet**, beyond its already approved exceptional singular identity and rebirth boundary.
- No native changes or main-branch merges follow from this research. The 3.5, 2024 and UA models are **comparison sources**, not automatic adopted mechanics.
- Rules Core remains the authoritative rules source; dependent tools should consume its resolution and not write local psionic adjudication.
- Preserve the existing spellcasting resource separation and ordinary Gem Dragonborn behavior unless independently reviewed and approved.
