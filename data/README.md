# Repository-authored rules data

`rulesets/first-party-001/native/` is the **only authoritative runtime content root**. `rulesets/first-party-001/working/` preserves non-operative SRD candidates, provisional catalogs and incomplete reconciliations; it may not be used as an API source or fallback. `rulesets/first-party-001/manifest.json` provides stable internal identity and path information, not a per-entity approval allowlist.

- `conversions/`: approved conversion mappings and research; no source edition is an automatic runtime fallback
- `schemas/`: data contracts
- `reference/`: original pinned sources, licenses, attribution and historical decisions

Approved complete rules move from working to native, leaving no duplicate authority. See `docs/rewrite/phase-1a-audit.md` and the migration ledger.

**Runtime data is not kept here.** User uploads, source grants, caches and databases live outside the repository.
