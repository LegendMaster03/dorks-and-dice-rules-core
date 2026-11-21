# Homebrew Species Conversion Guide — Character Origins

**Status:** DM/content-author guidance, not a new rules engine or approved balance schedule  
**Evidence baseline:** `9f50ab70a37be69a6748603d210fa9406ba7ea59` (November 21, 2026)  
**Audience:** DMs converting a third-party Species, and authors proposing native Dorks & Dice content

This guide explains how to represent an unfamiliar Species through the same characteristics used by ordinary Species and other Character Origins. It does **not** grant permission to use third-party text, automatically publish new native content, certify gameplay balance, or make an unimplemented effect executable.

## The short procedure

**Deconstruct → Combine → Reconcile ↔ Manifest → Reconstruct** is the conceptual model. A DM can use it without knowing the C# data structures. Reconciliation and manifestation may repeat when a choice exposes a new dependency or conflict.

1. **Read the complete source, not its feature names.** Record edition/version, source permission, Species and optional Ancestry, prerequisites, and *all* benefits and limitations. Read the character-level progression, not just the level-one summary. Mark ambiguous wording for a ruling.
2. **Separate inherent from acquired.** Physical physiology, inherent senses, movement, natural defenses, innate magic, and other Species-derived mechanics are possible Origin characteristics. Training, cultural tools or languages, occupation, equipment, and learned practices generally belong to Background, Upbringing, competencies, class, or later acquisition instead. Record an explicit exception if the source inseparably couples them. A default Upbringing is a suggestion, not a genetic fact.
3. **Record the physical foundation.** Identify creature type, size (including player choices), walking speed, and any further movement or physiology. Under the native biological policy the three heritable intrinsic slots are `creatureType`, `size`, and `walkingSpeedFeet`; other properties require their own actual trait/authoring path. Do not silently copy a contributor's entire Species profile.
4. **Break each feature into a complete mechanical characteristic.** Record its trigger, action cost, targets, range, effect, conditions, duration, use limit, recharge, scaling, prerequisites, interaction with other abilities, and provenance. A named feature might contain several independent grants or one inseparable package.
5. **Find a canonical definition by mechanics.** Compare the actual effect and constraints with existing `origin-trait` definitions and supported intrinsic slots. If they agree, reference the existing identity and configure permitted choices. If only the names agree, do not equate them. If the effect agrees but a mechanical parameter differs, preserve that difference for the valuation and authoring review.
6. **Author only what is missing.** For a genuinely new characteristic, write a source-neutral definition of the complete effect, give it a distinct provisional identity, and specify required interpreter/runtime semantics. A prose description can guide a DM but does not by itself create an executable grant. A local table definition does not silently modify the published catalog.
7. **Reassemble the relationships.** Record dependencies, mutual exclusions, choices and their timing, linked characteristics, resource ownership, shared versus independent pools, other grant requirements, and every later unlock. Do not separate an ability from a limitation essential to its operation. Do not count repeated source offers as multiple active abilities.
8. **Select the Origination path and effective characteristics.** A direct Species preset, permitted mixed biological contributors, or authorized custom creation may all use the common Origin model. Keep source occurrences and chosen manifestations distinct. For biological reproduction, distinguish *offered*, *inherited*, *expressed*, *latent*, and *effective*; a latent gene does not provide mechanics. Nonbiological procedures have their own acquisition and resource rules.
9. **Reconcile and review the whole starting configuration.** Confirm effective creature type, size, walking movement, dependencies, choices, spell/resource behavior, progression, interactions, and consistency with independent grants and alternate forms. A proposal containing individual valid traits can still be an incomplete or conflicting character. Preserve every unresolved item and the actual DM decision.
10. **Evaluate creation allocation separately from mechanical validity.** When a trusted, fully configured price schedule exists, assess **every effective inherent grant** and authored interactions together, including future benefits committed at creation. Until prices and allowance are approved, record **price unresolved** rather than zero. A complete mechanical conversion is not evidence of fair balance. Seek later quantitative review and playtesting under a separate workflow.

### Conversion worksheet

Copy one row per distinct inherent *effective* characteristic or inseparable package. Keep duplicate contributor offers in a separate source-evidence note, not as duplicate grants.

| Field | DM/author should enter |
| --- | --- |
| Source/version | Original Species, edition, publication/license status, and exact feature text location |
| Origin scope | Species/Ancestry, Background, Upbringing, other independent effect, or later progression |
| Identity | Native intrinsic slot / canonical `origin-trait` ID / **new provisional ID** |
| Full behavior | Action/trigger, range, target, defense/save, duration, effect, limitations |
| Configuration | Chosen ability, damage type, speed, range, form, level, spell, or other parameter |
| Acquisition | Direct, mixed eligible contributor, verified profile, custom/table grant, or later event |
| Dependencies | Required grant or choice, exclusion, linked source, and exact owner |
| Resources | At-will, uses, maximum, recovery event, shared pool owner, slot casting, and tracking |
| Progression | Starting behavior and all later unlocks or growth, with **character level** if intended |
| Disposition | Reused / configured but needs authoring / new mechanic / ruled manually / unresolved |
| Valuation | Reused reviewed price / needs configured-value review / **unpriced**; no speculative zero |
| Verification | Mechanical status, DM ruling/scope, projection or runtime status, balance status |

A conversion may contain several rows with different readiness states. **Do not label the Species mechanically complete while any required row is unresolved.** Do not remove an unfamiliar effect to make the construction artificially pass.

## How to choose reuse versus new authoring

- **Exact reuse:** Both benefits and restrictions agree. Example: breathing air and water indefinitely without a Swim Speed can reuse native `trait.amphibious` when the source means exactly that.
- **Reuse with a verified configuration:** The same complete mechanic permits an authored choice or parameter, such as a source-bound innate spellcasting ability. Preserve the chosen value and the authorizing source. Not every field is currently configurable.
- **Related but mechanically different:** A 15-foot Swim Speed (`trait.coldwater-swimmer`) is not water breathing, and Low-Light Vision (`trait.low-light-vision`) is not Darkvision. An ability named "Flight" is not automatically equivalent to a 1-minute Bonus Action flight use. Review distinct configuration/price treatment.
- **New characteristic:** No definition faithfully represents the effect. Specify it once as a new canonical *candidate*, identify integration work, and leave any engine-dependent resolution unresolved. Do not introduce Species-name switches.
- **Unclear source:** Request clarification or make an explicit scoped table ruling. Unknown wording never means "no effect" or "free."

Names, display labels, narrative flavor, and proposed comparison categories do not define gene classes. Mechanical identity, configuration, source occurrence, inherited instance, expressed grant, and price are different things.

## Authoring a Species or Ancestry

For a candidate native preset, retain parent Species/Ancestry relationships and reference existing canonical traits rather than copying the trait payload into each Species. Native files use `data/rulesets/first-party-001/native/origination/biological/species/` for presets and `.../biological/genes/` for reviewed `origin-trait` definitions. These paths describe current native content organization, **not** a DM-facing automatic import interface.

An author should submit (a) the source and legal attribution, (b) a normalized characteristic inventory, (c) the proposed intrinsic/trait references and new definitions, (d) the choice/dependency/resource/progression graph, (e) comparison evidence and unresolved prices, and (f) explicit DM/table policy where necessary. Reuse the published trait ID only when its entire mechanical contract is applicable. Preserve version identifiers for revisions and already constructed characters.

**Acquisition is not guaranteed by existence.** A canonical gene's presence in the native biological catalog does not mean it may be independently chosen in any construction. A selected nonbiological component does not become a biological parent. A custom table trait needs appropriate trusted authoring and selection evidence; a self-declared `Eligible` flag is not authority.

### Biological contributor caution

The existing biological combination exposes exact source offers, selected transmission, expression attempts, and effective grants. A selected inherited gene may remain latent; only surviving effective grants supply abilities and prerequisites. The default capacities in `rules/creature-origination.json` are two inherited instances and one expressed instance **per canonical kind**, not a two-feature creation limit and not a power budget. Do not assume Mendelian dominance, two mandatory parents, stacking duplicates, free upgrades, or that historical ancestors supply current entitlements.

A stronger characteristic assembled from multiple inherited genes requires explicit permitted combination authoring and validation. The parallel inherited-gene-combinations track is responsible for that extension; this guide specifies desired semantics only.

## DM rulings and unresolved cases

A DM may explicitly change even first-party Origin defaults, including source eligibility, compatibility, traits, parameters, reconciliation, manifestation, and whole-profile outcome. The decision should identify **authority, scope, target, and effective decision**, and preserve the original rule for other tables. This is game-rule authority, not permission to impersonate a trusted host or forge a native source.

At the table, a DM may run a fully specified unfamiliar effect manually while marking its software behavior **manual**. This is neither an engine-authenticated grant nor a zero-cost mechanic. Do not claim the C# engine can execute arbitrary new effect grammars or a general homebrew editor without a verified adapter and supported runtime.

Recommended final review checklist:

- [ ] No Species feature was silently discarded or moved into culture without justification.
- [ ] Intrinsic slots and all essential traits are represented exactly once in effective results.
- [ ] Every configuration choice has a legal source, timing, and persistable outcome.
- [ ] Prerequisites, conflicting alternatives, linked effects, and resource owners are retained.
- [ ] Level unlocks and scaling remain committed, without being available too early.
- [ ] The Origin is explicitly marked mechanically valid, unresolved, conflicting, or manual as appropriate.
- [ ] Every effective mechanic has a valuation disposition; unknown means unpriced, not zero.
- [ ] No arbitrary disadvantage produced a negative-point refund.
- [ ] Applicable DM decisions are documented without rewriting unrelated first-party defaults.
- [ ] Mechanical acceptance and later balance/playtest acceptance are separate.

## Other origination methods

Use the **same characteristic vocabulary** for Construct Assembly, magical creation, transformation, elemental manifestation, and other approved processes. Change **how a characteristic is acquired and made applicable**, not its meaning. Construct modules can have materials, installation cost, power draw, dependencies, and operating-state applicability; none of those is an inherited gene or automatically governed by the biological creation allocation. Post-creation acquisitions use their own procedures. A Rite of Rebirth ordinarily replaces the complete Species/Ancestry configuration rather than stacking a second biological inheritance.

See the [comparison and valuation reference](pillar2-homebrew-characteristic-comparison-20261121.md), [worked conversions](pillar2-homebrew-worked-conversions-20261121.md), and [repository capability and tool gap review](pillar2-homebrew-capabilities-and-review-20261121.md).