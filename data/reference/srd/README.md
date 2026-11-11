# SRD reference, attribution, and migration evidence

The authoritative Dorks & Dice rules are in `data/rulesets/first-party-001/native/`, not in the retired `data/native/` layout or the SRD reference tree. `structured/` preserves the pinned structured SRD 5.1 and SRD 5.2.1 inputs; `structured-manifest.json` tracks the source files. Source material and derived work require applicable attribution, license checks, and verification against official SRD publication boundaries.

The native ruleset is intentionally independent of any source edition at runtime. Candidate records and cross-edition evidence belong under `data/rulesets/first-party-001/working/`, `data/reference/`, or `data/conversions/`. These are never fallback native rules. Only approved, coherent definitions under `native/` are served.

The original structured-source comparison and native staging were historical migration steps. See [baseline import record](../../../docs/rewrite/srd-baseline-status.md), [native staging record](../../../docs/rewrite/native-srd-baseline-complete.md), and [migration audit](../../../docs/rewrite/phase-1a-audit.md) for their dated counts; those records must not be interpreted as current native catalog totals.

For current constraints see [Rules Core architecture](../../../docs/architecture.md), [data tree guidance](../../README.md), and the authoritative `native/` data. Source imports and external conversions require separate reviewed design and do not automatically extend the native package.
