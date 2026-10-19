# Full structured SRD baseline — source copy stage

The baseline is sourced from the unofficial **SRD-only 5etools-format JSON pack**
at https://github.com/CoolFireGiant/hewnhero-srd, pinned to commit
`d06d1dadee357857767b1e4da985df6609509bcf`.

The `data/reference/srd/structured/` directory contains a *verbatim copy* of
that pack's complete `data/` tree, including **both** structured SRD books
(`book-srd51.json`, `book-srd52.json`) and its classes, spells, items, monsters,
species, feats, conditions, actions, etc. The manifest lists every JSON file,
its byte size and SHA-256 so content can be audited. The one-time workflow was
used only to perform a bulk copy; it is not a native rules importer.

**Completeness boundary:** "Complete" means no files from the pinned JSON
pack have been omitted. The pack is independently maintained, not an official
Wizards release. We have **not** established that its extracted/transcribed
content precisely reproduces every line of the official SRD PDFs, nor that
every optional Foundry/generated extension is itself CC-licensed merely because
the pack repository claims SRD-only scope. Keep these fields unreviewed until
verification. Original official SRD documents and attribution statements are
identified in `data/reference/srd/manifest.json`.

This copy is **source material for the one-time native reconciliation process**,
not the active Dorks & Dice rule database and not a runtime dependency.
`data/native/` remains authoritative once rules have been selected. Source
edition markers, book-specific names and historical import mappings do not
belong in authoritative native rule semantics.

## Reconciliation rules

- First group matching entities by stable name plus mechanical type and genuine
  parent where applicable. Do not merge unlike types solely by name.
- Identical meanings can become one native rule; differences covered by prior
  Dorks & Dice decisions can be reconciled using those decisions.
- Where 5.1 and 5.2.1 differ in an unsupported way, retain their candidate
  evidence separately in this reference pack and **ask the Owner** before
  creating an authoritative native mechanic.
- Keep the approved layout: compact skills/competencies may be single JSON
  catalogs; large objects receive individual files; subclasses/subspecies
  are organized under their parent identity; no separate epic progression type.
- Do not introduce any permanent SRD import pipeline or source-specific runtime
  rule engine to seed native rules.

The reference import and native reconciliation are distinct milestones.
Copying a source pack is not, by itself, completion of reconciliation.
