# CC SRD attribution and native baseline context

Native Dorks & Dice rules live in `data/native/`, not in a runtime SRD source layer. `manifest.json` records the identities, license and attribution for the official SRD 5.1 and SRD 5.2.1 materials consulted in the baseline. **The native JSON is intended to be authoritative, not the upstream SRD or 5etools at runtime.**

The initial skill content has been curated directly from the structured `srd` and `srd52` skill records in 5etools at pinned commit `90d343cde64cf8dce1083ea0525a6b0b9e545f2e`. This is not a claim of importing the complete SRDs or independently verifying every text passage against the official PDFs. The project must fulfill CC BY 4.0 obligations and verify its redistributed excerpt coverage.

No baseline importer is required. Future user imports and third-party source support will have separate versioned conversion and provenance requirements. See `docs/rewrite/native-seed-skills-and-competencies.md`.

The compact, corrected starter skill data is in `data/native/skills.json`. It uses short descriptions selected from candidate SRD 5.2.1-marked material, subject to official-SRD checking. Older-edition names and decisions are not stored in the native skill records. Attribution remains relevant even though external source material is not an engine dependency.
