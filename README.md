# Dorks & Dice — Rules Core

Rules Core is the **headless, authoritative native rules engine and rules-content service** for the Dorks & Dice tabletop system. The separately hosted [Rules Wiki](https://github.com/LegendMaster03/dorks-and-dice-rules-wiki) provides its user-facing rules experience.

## Rewrite branch status

`feature/rules-core-rewrite` is a **clean-slate architecture and foundation branch**. The old implementation has deliberately been removed from this branch. Its source code, historical behavior, and previous tests remain available on `main` until the rewrite is approved for merging.

**No executable service, database migration, or gameplay engine exists on this branch yet.** Do not deploy this branch or enable public/tester access. The deployment workflow is intentionally inert. Choice of implementation language and framework remains open; C#, C++, Java, and TypeScript are candidates, not requirements.

## Organization

- `data/native/skills.json` — native skills, including all adopted historical-edition skill concepts but **no edition/import metadata**
- `data/native/competencies.json` — unified crafting-skill and tool-proficiency capabilities, not duplicated ordinary Skills
- `data/native/` — version-controlled, independently identified native Dorks & Dice entities, one primary JSON document per substantial entity; contained categories such as Skills and Competencies each use one JSON file
- `data/conversions/` — reviewed, versioned imported-source-to-native conversion definitions
- `data/schemas/` — data validation schemas as they are defined
- `data/reference/` — only material cleared for inclusion in version control, with source/license attribution
- `src/` — new headless rules engine/service, currently intentionally empty
- `docs/rewrite/` — authoritative rewrite design decisions, staged foundation, and subsystem notes
- `docs/` — other earlier design and implementation documentation retained as **historical reference**
- `tests/` — future deterministic tests and authorization/isolation checks
- `tools/` — future offline conversion, data validation, and development utilities
- `.github/workflows/` — language-neutral skeleton checks; deployment disabled pending reviewed implementation

## Rules-data principles

Each native rule entity has a stable identity independent of file path. Mechanics, descriptions, and applicable lore can share a single structured JSON entity; `fluff` is not automatically a separate file. Classes and species have typed subfolders; subclasses and subspecies are grouped under their parent identity in directory paths **and** record an explicit parent relationship in data. Class advancement, including epic Level Bands, belongs to its class rather than a separate continuation/progression entity.

Keep future source import/conversion data, provenance and source-revision history outside the core native skill rules; separate permissions, restricted source grants, explicitly adopted homebrew, campaign overrides and global Rules Lawyer decisions. A source import must not silently become authoritative native rules. Do not commit restricted content, user data, secrets, or deployed database snapshots.

## Next steps

Agree on foundational native data contracts, rules composition and access boundaries; choose a suitable runtime; implement the minimum approved engine foundation; add mechanics and converters one subsystem at a time with tests. Review and explicit authorization are required before gameplay implementation, database changes, deployment, re-enabling the service, or merging to `main`.

## SRD native baseline

The pinned SRD 5.1 and 5.2.1 structured corpus has been fully staged
into native category files and 2,225 per-entity records.
**Unreviewed candidate definitions are inert**, not executable rules or
approved native mechanics; resolved Skills and Conditions remain authoritative
where already decided. The original 90-file source pack is immutable reference
material. For counts, structure and verification caveats, see
`docs/rewrite/native-srd-baseline-complete.md`.
