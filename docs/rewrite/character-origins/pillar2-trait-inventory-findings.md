# Pillar 2 — Native Inherent-Trait Inventory Findings

**Status:** Evidence inventory and **provisional** classification, not approval of inheritance or balance mechanics.  
**Baseline snapshot:** `d5a2b15b7ae2bd88ae9e47a5ddfad01d481623bb` (Pillar 1 merge).  
**Sources:** `data/rulesets/first-party-001/native/species/base/` and `species/ancestry/`.  
**Audits:** `catalog-inventory/species-base.json`, `dragonborn-ancestry.json`, and `other-ancestry.json`.

## Coverage

| Record group | Entity files | Inline traits |
| --- | ---: | ---: |
| Base Species | 9 | 34 |
| Dragonborn Ancestries and families | 13 | 13 |
| Other Ancestries | 16 | 46 |
| **Total** | **38** | **93** |

These are **trait occurrences, not distinct independent or balanced trait choices**. Three Species bases (Dragonborn, Goliath, Tiefling) and three Dragonborn family records are not directly selectable. The Gem family currently contains no inline trait definitions; the Metallic family does have a selected native family definition and five playable leaf records. All 38 records were included to avoid silently omitting inherited family effects.

## Mechanical inventory and provisional categories

Each inventory entry records the source entity ID, local trait ID, source blob SHA, actual mechanical JSON keys, candidate categories, and **review flags**. Categories are heuristic labels for examining the content, not an approved trait taxonomy.

A single trait can occupy more than one category. Raw flag counts across the 93 inline traits:

| Candidate signal | Occurrences | Interpretation |
| --- | ---: | --- |
| Activated or combat effects | 50 | May require actions, triggering conditions, damage, or usages |
| Defense/survival | 24 | Includes saving throws, HP, resistance, immunities |
| Supernatural/spell | 24 | Includes innate spellcasting and magical capabilities |
| Physiology/movement | 15 | Includes size, breathing, resting, jumps or build |
| Senses | 10 | Includes sight and similar senses |
| Skills/advancement | 7 | Includes skills, training or feats |
| Not confidently classified | 9 | Requires manual mechanical inspection |

The categories overlap; counts must not be summed into a unique-trait total.

### Review flags (heuristic; not definitive)

- **37** inline traits have possible level-gated/progressive fields.
- **33** show usages, recovery or duration fields.
- **20** contain text or fields suggesting dependencies on other selections.
- **16** mention physical context that may require anatomical review.
- **5** refer to shared rule entities.
- **3** directly contain certain recognized choice fields.

The flags are deliberately conservative prompts for human review. A missing flag is **not** evidence that a trait is independent, balanced, or universally inheritable. Some very important mechanics live at the *entity level*, not inside an individual `traits[]` entry.

## Repeated local trait IDs

| Local ID | Found in | Design implication |
| --- | --- | --- |
| `low-light-vision` | Dragonborn, Elf, Gnome, Orc, Tiefling | Shared explicit `rule.low-light-vision`; do not multiply a sense by source count |
| `powerful-build` | Orc, Goliath | **Different effects**: Goliath includes grapple-escape advantage; Orc explicitly does not. Same local name does not establish an interchangeable global trait |
| `amphibious` | Several Dragonborn leaf Ancestries | Compare actual breathing and movement effects before treating names as identical |
| `natural-climber` | Red and Copper Dragonborn | Ancestry/local context matters |
| `darkness` | Drow and Infernal Tiefling | Spell acquisition has different source metadata/choices; local name alone is not a global mechanical identity |

A stable **grant occurrence** (source entity + local trait ID) is useful for auditing now. A shared **mechanical trait identity**, any parameterized variants, and whether a given trait is a selectable heritage option are *different questions* and remain unresolved.

## Four distinct structural needs

1. **Source provenance and eligibility.** Origination contributors and hereditary sources do not themselves grant mechanics. A source may be a mixed-Species individual, constructed design, material, or supernatural influence.
2. **Characteristic/effect definition.** A trait's effects may be scalar, conditional, progressive, resource-limited, or depend on a body part or spellcasting choice. Repeated names are not sufficient identity.
3. **Effective physical and creature profile.** Creature type, size or size choice, walking speed, darkvision range, needs, ancestry-conditional damage type, and specialized movement sometimes occur **outside** `traits[]`. A general inheritance model cannot assume every relevant attribute is already an inline trait.
4. **Selected expression and interactions.** Selection must distinguish eligible candidates from actually granted traits, preserve prerequisites/progression, avoid unintended duplication, and never import monster/NPC statblock defaults.

These are **data modeling responsibilities**, not approval of any particular class, table, JSON field, or numeric budget. The draft `pillar2-origin-profile.schema.json` and three fixtures are deliberately nonauthoritative.

## First prioritized design problems after tester feedback

1. Whether existing **conventional Species/Ancestry presets** remain equivalent under a generalized model (High Elf control).
2. What qualifies as an **independently selectable trait** versus a composite, dependent, or physical-profile characteristic.
3. Which effects from a **mixed Elf/Orc** origin can coexist, including Fey/Humanoid creature-type identity and duplicated Low-Light Vision.
4. Whether a **homebrew Species** can contribute traits without pair-specific hardcoding or automatically costing arbitrary points.
5. Whether a **constructed or fungal individual** can resolve anatomy, needs, type and supernatural traits with the same machinery.

**Deferred:** Point prices, point budgets, forced trait exchanges, hereditary simulations, cross-type targeting, and executable C# inheritance. Testers' observations should be incorporated as evidence when available, not treated as automatic approvals.

## Separation from Pillar 3

Languages, learned training and cultural experiences already appear in independent Upbringing records. The Pillar 2 catalog must not automatically classify every grant containing a `skillBonuses` field as acquired culture (e.g., Elf Keen Senses is an existing inherent Species rule). Conversely, Giant Foundling learned languages and skills should not become inheritable merely because they relate to Giants.
