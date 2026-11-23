# Pillar 2 — Halfling luck and Lightfoot completion (2026-11-23)

Status: approved design increment; staged on a feature branch and not merged.

## Shared Halfling Species — provisional 12 points

| Gene | Trial load | Decision |
| --- | ---: | --- |
| Luck Reroll | 2 | Retain existing inherent natural-1 reroll |
| Brave | 1 | Retain |
| Larger-Creature Passage | 1 | Retain |
| Keen Hearing | 1 | Retain |
| Second Chance | 4 | Promote defensive half-feat's reroll effect to biological trait |
| Bountiful Luck | 3 | Promote ally-facing natural-1 reroll effect to biological trait |
| **Total** | **12** | |

Second Chance uses the Reaction when a **visible attacker** lands an attack, forces a new attack roll, and refreshes on initiative or a Short/Long Rest. Bountiful Luck uses the Reaction when a **visible ally within 30 feet** rolls a natural 1 on a d20 test, allows the ally to reroll, and suspends the Halfling's own Luck Reroll through the end of their next turn. Both draw on the ordinary Reaction budget. No ability-score bonus, extra Reaction, or additional feat acquisition is granted.

These two gene traits are adaptations of historical feats from *Xanathar's Guide to Everything*, not automatic acquisition of the original feats in addition to their biological versions. The shared Species remains Luck-focused.

## Lightfoot — provisional additional 5 points

| Gene | Trial load | Decision |
| --- | ---: | --- |
| Natural Agility | 2 | Retain |
| Naturally Stealthy | 2 | Move from shared Halfling to Lightfoot |
| Trackless Passage | 1 | Reuse the canonical environment-configured gene from PR #198 |
| **Total** | **5** | |

**Lightfoot total: 17 / 18**, a provisional nominal allocation within the accepted range. Halflings selected without an Ancestry remain at 12; other potential Ancestries are not given Lightfoot's stealth characteristics.

### Ancestral biome configuration

Wood Elf and Forest Gnome keep their existing fixed `environmentSubTrait.biome = woodland` grants. Lightfoot instead offers an explicit source-local finite list (`woodland`, `grassland`, `wetland`, `desert`, `mountain`, `coastal`, `tundra`). Selecting Lightfoot Trackless Passage requires choosing exactly one biome at creation. The choice materializes to the same canonical `subTrait.biome` effective parameter consumed by the existing Trackless Passage trait. This represents the adapted environment of a lineage and is not freely changeable or stacked across multiple biomes.

The finite menu is the initial candidate set for this first playable Ancestry; it does not establish a universal biome taxonomy or extend other Species. A different authentic lineage source may supply another biome configuration after separate authoring/review. No choice is auto-filled and an invalid choice cannot authorize the gene.

## Boundaries

- Trial pricing revision v3 adds Second Chance (4) and Bountiful Luck (3); all other gene pricing is unchanged from PR #198.
- The 2024 shared Naturally Stealthy placement is intentionally revised to preserve Lightfoot specialization.
- No change to Gnome, Dwarf, Dragonborn, or other pending historical feat reviews.
- Canonical trait descriptions and choice/binding data are native; full combat Reaction, reroll scheduling, luck suppression, and tracking gameplay execution are not implemented by this data increment.
- No changes to main. Independent integrated gameplay testing remains a separate WorkChat pass.
