# SRD 5.2.1 Animals extraction

Status: structured section import, pending exact-head CI and comprehensive source review. This is not the complete bestiary.

The publisher Animals section on PDF pages 344–364 has 95 distinct stat-block headings, independently counted using the size/type and AC/initiative header. All 95 match the pinned source inventory. Ogre Zombie precedes this section and is excluded. The earlier two reviewed blocks remain separate (97 modern monsters total).

Run `node tools/import_official_srd52_animals.mjs --check` from the repository. The existing publisher PDF workflow independently re-extracts and verifies the page records before checking the generated catalog. The generator uses an unambiguous per-page column gutter, retains the complete stat-block layout, parses ability scores and saves, initiative, armor class, hit points, movement, skills, senses, resistances and immunities, challenge rating, XP, proficiency and named trait/action/bonus/reaction entries. Swarm movement choice and the Giant Owl's spellcasting are explicitly represented. The pinned third-party records supply names and entry-heading comparison, not the text or numeric values. No first-party mechanics or source media are imported.

The coverage report `data/reference/srd/official-animals-import.json` records independent official heading count, extraction failures, unmatched official names, field differences and gutter positions. A zero-difference result is limited to the compared fields, not a certification of every source field. The Seahorse explicitly awards 0 XP, preserved from the publisher. The source stores that CR as an object; the native representation retains the same rating and XP.

Entry text preserves line breaks and hyphenation from the publisher layout. It is structurally compatible with 5e.tools entry arrays, but does not recreate all inline attack/damage tags or guarantee identical consumer rendering. Explicit references cover source pages, condition immunities and Giant Owl spells; other descriptive mentions remain unresolved as catalog references. All 95 source pointers and explicit reference targets were checked against the respective edition inventories. The remaining Monsters chapter, embedded stat blocks, SRD 5.1 bestiary and other catalog categories remain outstanding.

The complete native section is reproducible from pinned page records. No public distribution approval or new executable mechanics is implied.
