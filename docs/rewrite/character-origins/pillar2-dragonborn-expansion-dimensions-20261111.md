# Pillar 2 — Dragonborn ancestry dimensions and expansion constraints

**Date:** 2026-11-11  
**Status:** Owner-established Dragonborn content direction and Pillar 2 modeling implications. This does not authorize new native records, inferred gameplay effects, final mixed-Heritage selection grouping, or automatic Upbringing grants.

## Design intent: every ancestry has five defining associations

Each Dragonborn ancestry is intended to have all of the following:

1. **Category:** Its broader Dragonborn family, such as Chromatic or Metallic. More categories are planned.
2. **Skin material:** A physical material or quality **related to its category**. This is an intended category-related physical expression, not necessarily an independently inheritable parameter or an approved gameplay property.
3. **Damage type:** The ancestry's elemental/damage association. The same damage type is planned to be possible in **multiple major categories**.
4. **Breath Weapon area:** The full authored shape **and dimensions** (size). The approved mixed-Heritage ordinary Breath Weapon rule lets an eligible damage type and one *whole* eligible area be selected independently. Length/width or shape/dimensions are not separately interchangeable unless explicitly defined.
5. **Hoard-related Upbringing:** A cultural Upbringing thematically related to what the associated dragon hoards. **Upbringing remains a separate, independently selectable cultural part of Character Origins**, not a biological grant inherited with the Dragonborn's skin, category, or Breath Weapon. The correspondence can provide a meaningful **suggestion**; it does not force the character to adopt that culture.

The owner intends to expand **both the set of Dragonborn categories and the ancestries within each category**. The longer-term goal is to represent **at least one ancestry for each relevant damage type in every major category**. The present native sample was limited for scope, not because Chromatic and Metallic (plus a nonselectable Gem placeholder) exhaust the taxonomy. Nor should the existing category-specific leaf count be treated as a permanent limit.

## Expansion should be content authoring, not repeated implementation

The complexity of declaring Dragonborn-wide dimensions and their relationships is **up-front, reusable model work**. Once the underlying generic Origin system supports these kinds of definitions, adding another Dragonborn ancestry that uses existing mechanics should mainly require authoring and validating a new leaf (and any new category, trait definitions, or cultural Upbringing records that actually belong to it). The system must not need a new branch of Dragonborn-specific code or a manually expanded list of recognized category/damage combinations for each new leaf.

The intended architecture is:

- **General Origin/Heritage machinery:** source-neutral lineage, characteristic identity, candidate values, parameter bindings, constraints, manifestation, and provenance.
- **Species-level definitions:** reusable Dragonborn characteristic relationships, defaults, and permitted ancestry-family structures, expressed as data rather than a hard-coded Dragonborn resolver.
- **Category/family records:** category-specific relationships and abilities, authored without assuming a closed set of categories.
- **Ancestry records:** the actual category-related skin/material expression, damage type, full Breath Weapon geometry, distinctive traits, and associated cultural suggestion, as the authored content becomes available.
- **Upbringing records:** independently selectable cultural definitions; a hoard association can suggest one but never confer it biologically.

This architecture should be reused by **other Species**, whose defining characteristics may be entirely different. A new Species should not require a new engine if all its abilities can be represented using existing general capabilities. Equally, a new Ancestry can require *new generalized mechanic work* if it introduces something genuinely not yet modeled. **Content expansion should be routine; capability expansion should be exceptional and source-neutral.**

An extensibility test should add synthetic Dragonborn categories and leaves—including the same damage type in different categories—and verify all five authored associations are preserved without updating generic resolver logic. This is an acceptance goal; the fully declarative schema and its integration are not yet approved or implemented.

## What the current native corpus actually expresses

| Dimension | Current source evidence | Current limitation |
| --- | --- | --- |
| Category | Explicit Species/Ancestry `parent` lineage through nonselectable family records | Only presently authored families; future taxonomy not enumerated |
| Skin material | Not a normalized, authoritative dedicated field in the audited sample | Do not derive a universal material/skin mechanical property from `Chromatic` or `Metallic` names |
| Damage type | Leaf `damageType` field | It describes an authored association, not a one-to-one category classifier |
| Breath geometry | Family/leaf `breathWeaponArea` resolved through an explicit binding contract | Whole area only; more categories may author different shapes/dimensions |
| Hoard-related Upbringing | Ancestries support `defaultUpbringing` as an optional cultural suggestion | Current Red and Bronze native leaves explicitly **have no Upbringing default yet**; the future hoard-linked Upbringings and their associations have not been authored |

The absence of material or hoard-related Upbringing data in today's source files is a **content gap relative to the intended expanded design**, not evidence that those concepts are prohibited or that Pillar 2 should guess them from a color, metal, gem, or damage type.

**Important:** The five associations describe a **standard authored ancestry definition**. They do **not** automatically mean five unrestricted, independent mixed-Heritage choices. The user is considering fewer choices with broader consequences; relationships among category, physical material, elemental affinity, traits and allowed options remain a distinct design problem.

## Consequences for characteristic and profile structure

**Separate identity from relationship.** Category and elemental affinity must be distinguishable because the long-term content plan crosses every major category with damage types. However, skin material is related to category, and a hoard-related Upbringing is a *cultural association*, not another inherited characteristic.

**Separate direct preset from mixed manifestation.** A standard ancestry can author a coherent package (category, related material, damage type, whole area and cultural suggestion). A mixed Origin must not automatically inherit every part of every contributor's package or assume all links have been severed. The selected damage type may be a powerful shared parameter, but any cross-trait coupling must be defined explicitly rather than inferred from today's leaf layout.

**Preserve existing approved mechanics.** Ordinary Breath Weapon remains one compound trait with independently selectable contributor-supplied damage type and complete area. Draconic Resistance is a separate ability; Chromatic Warding remains a category-sourced distinct ability that overrides same-type resistance when used. Whether a single manifested affinity should drive several of these traits is a **current design proposal**, not an implemented or owner-approved general selection rule. The existing default Red Dragonborn source consistently uses Fire for all three; that source example is not a universal constraint on future category–element pairs.

**Do not assume category names are mechanics.** The trait structure should resolve any future category-specific features through **authored definitions and explicit source lineage**, not by a permanent switch over the currently known categories. The same principle applies to physical material: its narrative description and any actual gameplay properties need separate authoring.

**Preserve the cultural boundary.** Upbringing choices remain free from a mandatory biological connection. The hoard relationship informs a suggested cultural default, where authored, without forcing an inherited benefit or preventing another Upbringing selection.

## Representative future-facing questions (not answered here)

- Is the manifested **skin material** always tied to the manifested **category**, and if so what is the precise grouping rule for mixed contributors?
- Does a single **elemental affinity** drive Breath Weapon damage, Draconic Resistance and eligible family-specific effects, or can one or more be independently configured?
- How are category-specific traits (Chromatic Warding, Metallic Breath Weapon and future families' alternatives) made available when an Origin has multiple category contributors?
- How should a **hoard-related Upbringing suggestion** be authored per ancestry, and how does its provenance remain separate from the character's actual independent cultural choice?

Do not settle these with automatic selection rules or new native fields until the relevant design decisions are made.

## Compatibility examples for later testing

1. **Cross-category matching damage type:** Two future leaves, in different categories, may share a damage type. A default-source audit must not reject them as duplicate or infer that they have identical skin material or family traits.
2. **Multiple damage types within one category:** A category must not hardcode a single elemental affinity.
3. **Skin–category association:** A selectable leaf should be able to identify its physical material without automatically granting every other category member's physical features.
4. **Hoard culture:** A leaf may suggest an upbringing related to its dragon's hoard without automatically selecting, granting, or restricting the character's cultural choice.
5. **Present Red + Bronze example:** Fire/Lightning and Line/Cone retain the already approved independent ordinary Breath Weapon parameter selection; the other characteristic relationships remain explicitly governed or unresolved.

**Implementation boundary:** This document is a design record and future acceptance guide. No active first-party Species/Ancestry, Upbringing, creature mechanics, or character-creation API is modified by this clarification.
