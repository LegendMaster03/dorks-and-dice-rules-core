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


## Additional findings from official-PDF fragment comparison

| Finding | Severity | Imported source location | Native correction | Official evidence |
| --- | --- | --- | --- | --- |
| S52-020 | High | \`structured/items.json#/item/522/entries/0–2\`, \`/entries/6\`, \`/entries/9\`: Dragon Orb includes non-SRD Dragonlance setting exposition, old Orb of Dragonkind random-properties mechanic, and obsolete name | \`data/native/items/magic/dragon-orb.json\`: remove unsupported setting prose and random-properties mechanical bonuses; retain the actual 5.2.1 Dragon Orb item mechanics; normalize remaining item identity | SRD 5.2.1, Magic Items A–Z → Dragon Orb, PDF p. 218 (PDF index 217); the entire official entry lacks the random-properties rule |
| S52-021 | Medium | \`structured/items.json#/item/222/entries/11\`: Mysterious Deck includes extraneous "A Question of Enmity" DM inset, obsolete Bastion loss in the Ruin card, legacy Deck of Many Things cross-links and table/text labels | \`data/native/items/magic/mysterious-deck.json\`: remove unsupported inset, Bastion effect, and obsolete cross-links; use official table name while preserving each official card effect | SRD 5.2.1, Magic Items A–Z → Mysterious Deck, PDF pp. 231–233 (PDF index 230–232) |

### Broader descriptive-text provenance concern

The automated text-overlap triage reviewed 6,301 source fragments against the 403-page official SRD 5.1 PDF and 5,317 against the 364-page SRD 5.2.1 PDF. It flagged 917 and 342 low-similarity fragments, respectively. These are **not missing-rule counts** or proof that each record is unauthorized; the normalization can be thrown off by PDF columns, tables, and structured metadata. The low-similarity group is concentrated in \`bestiary/fluff-bestiary-srd51.json\`, \`bestiary/fluff-bestiary-srd52.json\`, and \`class/fluff-class-*.json\` narrative content. Representative original descriptions about aboleths, dryads, celestial arrogance, and class-character origin prompts are not present in the official PDF gameplay corpus. They have been preserved in the pinned pack but must be provenance-classified before being treated as official SRD content.

The comparison is reproducible using \`tools/audit_srd_text_fidelity.py\` and \`.github/workflows/audit-srd-text.yml\` on branch \`audit/source-srd-text-parity-20261019\`. Its diagnostic workflow first passed at https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/39198758922.

These findings do not resolve the full chapter/paragraph audit, complete table-cell verification, full monster/spell fidelity, or comprehensive licensing review. No wholesale narrative deletion is justified until source provenance is established and approved non-SRD homebrew is distinguished.

## Legacy structured reference-audit corrections

- S51-001 — Warlock Pact Magic example: replace Witch Bolt with Thunderwave (official p.47). Retain slot progression and spellcasting mechanics.
- S51-002 — Circle of the Land: official pp.21–22 list seven terrains, not Underdark. Remove Underdark from both descriptive table/choice and structured grants; Swamp's 3rd-level pair is Acid Arrow and Darkness. Preserve all other terrain tables.
- S51-003 — Twinned Spell: official p.44 uses Ray of Frost as the only eligible example; remove Chromatic Orb from the illustrative prose.

The pinned source remains unchanged; these corrections are reproduced by class and feature projection generators.

## Legacy unresolved inline target adjudication

At the prior exact-head audit, all 13 missing SRD 5.1 inline targets were confined to five lycanthrope curse links, seven Life Domain deity links, and one poison creature link. The official page extraction confirms p.327–329 uses plain lycanthropy curse wording and the complete PDF extraction contains no Player Characters as Lycanthropes variant rule. All five stat blocks retain their published curse and save, without an invented player-character rule. P.17 contains all seven deity names in its examples, even though the SRD does not publish separate entries for them; keep the literal names and links to the eligible pantheon entries, not the unsupported commercial deity references. P.204 says crawler, not Carrion Crawler; the poison's condition and saving-throw rules are retained. These are source corrections to the existing generators and report records, not broader source-body certification. No imported identities or mechanics were changed.

## Consolidated native publisher comparison

The historical audit on commit f4162a5aaf2ed6c2aa120d902f2f3fe1b16af95b compared the entire pinned reference pack, including unpromoted narrative. The current `tools/audit_srd_native_fidelity.py` instead inventories promoted native rule prose, nested table cells and labels, retaining exact entity/JSON pointers and source-page evidence. Spell bodies receive complete normalized token-sequence comparisons against their independently extracted official layouts, preserving small substitutions, numeric changes and missing/extra text; other prose is matched against publisher raw-text page indexes, with source-local and elsewhere matches distinguished. Unknown markup, short unmatched cells and partial matches remain review candidates. Table-cell presence is not row/column parity; casting header structures, stat-block numeric fields, and omitted publisher content require separate verification. The existing official-source workflow generates the complete report from the hash-verified publisher PDFs. It is diagnostic, not a new runtime schema, automatic text rewrite, or rights approval. Publisher source and third-party pinned source files are unchanged.

### First confirmed spell corrections

- **SRD 5.1 Acid Splash (p.114):** remove the imported requirement to see each selected creature; the pinned publisher PDF requires only the stated range and separation. Remove the corresponding sight-target tag. The modern spell remains unchanged.
- **SRD 5.2.1 Animate Dead (p.108):** restore the publisher cross-reference to the Monsters section for the Skeleton and Zombie stat blocks. No mechanical grant changes.

Both native corrections are reproducible in the existing spell promotion generator. Their entire normalized spell bodies are covered by the native fidelity comparison. The remainder of the spell inventory and other content categories have not been certified.

### Legacy spell body corrections — first batch

Seven further legacy spell descriptions differ from the pinned CC SRD 5.1, largely because the structured source includes later errata. Restore the publisher's specific rules rather than treating that later text as authoritative for this package.

| Spell | Publisher rule restored |
|---|---|
| Call Lightning | Cloud centered 100 feet directly above the caster; strike point within range |
| Clone | Clone grows in a sealed vessel |
| Color Spray | Blinded until the spell ends |
| Contagion | Disease selected and inflicted on hit; three failures sustain disease, three successes recover; remove imported poisoned condition |
| Disintegrate | Damage that reduces target to 0 HP disintegrates it |
| Glyph of Warding | Surface area limitation and object-specific movement restriction as published |
| Heroes' Feast | Up to twelve other creatures |

The existing generator preserves the unchanged upstream source and verifies specific publisher evidence before applying these corrections. The complete normalized descriptions are checked by the fidelity tests. No executable spell mechanics are added.
