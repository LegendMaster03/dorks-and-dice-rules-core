# Second-pass official SRD 5.1 / 5.2.1 source-fidelity audit

**Status:** Active, not exhaustive. These findings extend the October 18 initial audit. They do not certify any untested category. The 90-file pinned third-party source pack remains unchanged; the native candidates below reflect corrections verified against the official 5.2.1 Creative Commons PDF. Approved Dorks & Dice reconciliations are not altered.

## Additional confirmed findings

| Finding | Severity | Imported source location | Propagated native correction | Official evidence |
| --- | --- | --- | --- | --- |
| S52-015 | Medium | `structured/bestiary/bestiary-srd52.json#/monster/1` and `#/monster/13`: four Melf's Acid Arrow references across Adult and Ancient Black Dragons | `data/native/bestiary/adult-black-dragon.json`, `ancient-black-dragon.json`: replace with Acid Arrow in the 5.2.1 candidates | SRD 5.2.1, Adult Black Dragon / Ancient Black Dragon, PDF pages 264–265 |
| S52-016 | Medium | `structured/items.json#/item/210`: Cube of Force table and `attachedSpells` refer to Leomund's Tiny Hut, Mordenkainen's Private Sanctum, and Otiluke's Resilient Sphere | `data/native/items/magic/cube-of-force.json`: use Tiny Hut, Private Sanctum, and Resilient Sphere in table **and** machine-readable spell links | SRD 5.2.1, Cube of Force, PDF page 216 |
| S52-017 | Medium | `structured/optionalfeatures.json#/optionalfeature/75`: Pact of the Chain includes Slaad Tadpole and an inappropriate Appendix B pointer | `data/native/features/optional/pact-of-the-chain.json`: remove extra familiar and use correct Monsters cross-reference | SRD 5.2.1, Pact of the Chain, PDF page 74 |
| S52-018 | High | No `Avatar of Death` SRD52 entity in `structured/bestiary/bestiary-srd52.json`; `structured/items.json#/item/222` (Mysterious Deck) references its stat block, but the third-party import does not carry the modern block | `data/native/bestiary/avatar-of-death.json`: retain separate 5.1 candidate and add full SRD 5.2.1 stat block (AC, HP, ability scores, senses, immunities, proficiency relation, trait, attacks, scaling); update `native-coverage-manifest.json` with authoritative-PDF evidence | SRD 5.2.1, Mysterious Deck / Avatar of Death, PDF page 232 |
| S52-019 | Medium | `structured/class/class-warlock.json#/classFeature/19`: Pact Magic example uses Witch Bolt, which is not the spell in the official SRD 5.2.1 example | `data/native/features/classes/warlock/pact-magic-level-1.json`: use Charm Person | SRD 5.2.1, Warlock → Pact Magic, PDF page 71 |

Official documents:
- 5.2.1: https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf
- 5.1: https://media.dndbeyond.com/compendium-images/srd/5.1/SRD_CC_v5.1.pdf

## Scope and provenance

The official SRD 5.1 CC PDF is accessible and its attribution/legal-information section has been directly examined against `structured/book/book-srd51.json`. The same comparison was made for the 5.2.1 CC legal-information section. This resolves the original PDF-access obstacle but does **not** close the broader licensing/third-party-metadata audit.

A cross-reference inventory of 19 principal structured JSON files found 5,110 explicit SRD-tagged spell, creature, item, and feat occurrences. There were 87 unmatched occurrences representing 63 unique name/edition/kind combinations before accounting for aliases, templated equipment, and special references. These are **candidate references for adjudication, not 63 additional confirmed defects**. All twelve class files were separately checked for explicit SRD-tagged spell references: one additional broken reference was found (S52-019).

The first-pass findings S52-001–014 are documented separately. S52-015–019 have been corrected **only in native candidates**, not rewritten in the pinned source corpus, with targeted tests added to `tests/test_native_source_fidelity.py`.

## Outstanding completeness work

All original U-001 through U-011 verification categories remain open except for the limited first-party CC legal-information excerpt comparison described above. Outstanding work includes paragraph/exception comparison, every table cell, all class level progressions and features, all 658 imported spell bodies, 657 imported monster records plus separately embedded stat blocks, every item/feat origin, lore mechanics, and third-party provenance. Record names and passing structural tests must not be represented as complete content fidelity.

**Source pack record counts remain the counts of what was originally pinned.** The extra official-PDF-backed Avatar of Death definition is recorded only as a second native candidate; this does not retroactively alter the unofficial source inventory.
