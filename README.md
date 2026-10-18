# Dorks & Dice — Rules Core

Rules Core is the **headless, authoritative native rules engine and rules-content service** for the Dorks & Dice tabletop system. The separately hosted [Rules Wiki](https://github.com/LegendMaster03/dorks-and-dice-rules-wiki) provides its user-facing rules experience.

## Rewrite branch status

`feature/rules-core-rewrite` is a **clean-slate architecture and foundation branch**. The old implementation has deliberately been removed from this branch. Its source code, historical behavior, and previous tests remain available on `main` until the rewrite is approved for merging.

**No executable service, database migration, or gameplay engine exists on this branch yet.** Do not deploy this branch or enable public/tester access. The deployment workflow is intentionally inert. Choice of implementation language and framework remains open; C#, C++, Java, and TypeScript are candidates, not requirements.

## Organization

- `data/native/` — version-controlled, independently identified native Dorks & Dice entities, one primary JSON document per entity
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

Keep immutable external source representations, provenance, source-revision history, rules-lineage relationships, restricted source grants, explicitly adopted homebrew, campaign overrides, and globally published Rules Lawyer decisions conceptually distinct. A source import must not silently become authoritative native rules. Do not commit restricted content, user data, secrets, or deployed database snapshots.

## Next steps

Agree on foundational native data contracts, rules composition and access boundaries; choose a suitable runtime; implement the minimum approved engine foundation; add mechanics and converters one subsystem at a time with tests. Review and explicit authorization are required before gameplay implementation, database changes, deployment, re-enabling the service, or merging to `main`.
