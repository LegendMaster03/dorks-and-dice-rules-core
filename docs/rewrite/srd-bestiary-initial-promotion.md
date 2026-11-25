# Initial edition-specific bestiary promotion

Status: partial; not acceptance-ready. Two native monster records per D&D edition were reviewed against the already pinned official publisher page extracts. No first-party content, source corpus, publication policy or executable mechanics changed.

| Package | Entity | Official PDF page | Source classification |
| --- | --- | ---: | --- |
| dnd-5e | Avatar of Death | 218 | Reviewed source candidate |
| dnd-5e | Acolyte | 395 | Reviewed source candidate |
| dnd-5-5e | Avatar of Death | 232 | Official embedded stat block missing from the source pack (S52-018) |
| dnd-5-5e | Priest Acolyte | 316 | Reviewed source candidate |

The native monster definitions use the common catalog envelope and retain applicable 5e.tools-shaped fields. Source media, fluff flags and derived filter tags are not included. The modern Avatar has an initiative score, explicit saves, summoner-dependent proficiency bonus, Exhaustion immunity and Multiattack; the 2014 block has darkvision and Turning Immunity instead. Neither block has a numeric challenge rating; both award zero XP. Modern Divine Aid is a shared one-use-per-day choice, not one use for each spell. Spell references resolve inside the selected package. Modern Priest Acolyte gear tags remain descriptive pending equipment promotion.

This is a limited manual review of the extracted official page text, not a visual audit of the entire PDFs. The source pointers and PDF hashes are retained. The modern Avatar has no fabricated third-party pointer. The first-party game is not a source of authority for these definitions.

The pinned bestiary has 323 SRD 5.1 and 334 SRD 5.2.1 monster occurrences. The modern Avatar is an additional official embedded definition; those occurrence counts are not a certified official monster inventory. 321 and 333 source occurrences respectively remain unpromoted in this category, plus any other embedded or missing official definitions. No claims of completeness are made.

The coverage inventory command `python3 tools/report_srd_promotion.py --output build/srd-review/promotion-coverage.json` reports authoritative native categories, source-pointer joins, identity-only spell matches, unresolved references and unreviewed candidate groups. It is a diagnostic, not a license or semantic fidelity certification. It never writes native content. Unclassified candidates remain unreviewed, not presumed excluded. Future reviewed corrections should add source evidence without modifying the original corpus.

The existing four-package HTTP test additionally requests the real bestiary entities, verifies edition-specific Avatar mechanics in the returned definitions, and checks that the Acolyte identities and Fate remain isolated. Exact-head CI must pass before this batch is considered validated.
