# Native skills and universal competencies baseline

**Status:** Initial native data-only fixtures; no executable rules engine or final JSON schema. The JSON under `data/native/` is the **authority for Dorks & Dice** when approved and published. Original external source files are neither required at evaluation time nor imported into a source-layer database as part of this baseline.

## Approach

This baseline was **curated directly** from SRD-flagged records in 5etools `data/skills.json` pinned at commit `90d343cde64cf8dce1083ea0525a6b0b9e545f2e`, plus reviewed Dorks & Dice competency decisions recovered from `main` documentation and implementation. There is **no baseline importer or generator** in this repository.

The 18 skills are stored as `data/native/skills/<name>.json`. For initial review, each file retains a shorter summary based on the SRD 5.2.1-marked entry and detailed examples from the SRD 5.1-marked entry. Combining text is not automatic permission to combine inconsistent procedures; mechanically divergent material must be reviewed before final approval. No source-specific `srd`, `srd52` or `PHB` flags are used by our native evaluator. Native IDs are stable independently of paths.

Universal `data/native/competencies/` entries reference native skills when the concept has a straightforward skill presentation. They retain existing Dorks & Dice distinctions: direct aliases; independently ranked 3.x skill components; conditional one-way composites; Craft/Perform/Profession families whose children have independent rank state; and Alchemy with one learned training identity but distinct skill/tool mechanical profiles. This first group is **not** the complete competency catalog; later reviewed Craft/Perform/Profession specialties, standalone tools and other identities must still be added.

For example, `competency.stealth` references `skill.stealth`, retains `competency.hide` and `competency.move-silently` as independent state, and declares the reviewed arithmetic-mean composition without erasing those components. The precise conflict behavior between native defaults, global edits, campaigns and optional source contributions remains a separate foundation decision.

## Copyright and attribution

The initial skill texts were copied from the `srd` and `srd52`-marked records in [5etools' data/skills.json](https://github.com/5etools-mirror-3/5etools-src/blob/90d343cde64cf8dce1083ea0525a6b0b9e545f2e/data/skills.json). These are treated as **candidate CC-licensed excerpts** and require comparison against the official SRD 5.1 and 5.2.1 before final publication. Licensed material still requires attribution. Official document identities and CC BY 4.0 attribution statements are recorded in `data/reference/srd/manifest.json`; preserving legal attribution does **not** require a runtime dependency on imported source editions or source records.

The original `skills.index.json` discovery records were removed as redundant; native skills are now maintained in individual files. No full-SRD import, test database, source ingestion pipeline or gameplay implementation has been introduced.

## Additional native data

Continue creating **one native entity per JSON file** by adapting appropriate CC-covered rules from the two SRDs and applying reviewed changes. Do not embed non-SRD 5etools publications under an assumption that the entire repository is CC. Use the old Rules Core code and `docs/rewrite/` to recover previous *decisions*, not the old application structure. Unsupported or contradictory mechanics must remain marked for review, never guessed.
