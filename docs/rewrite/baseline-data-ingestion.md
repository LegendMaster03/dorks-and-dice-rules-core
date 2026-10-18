# Baseline data sources and staged verification

**Status: initial reference-fixture bootstrap; no native schema, gameplay evaluator, runtime, or complete SRD import is implemented.**

## Three inputs

1. **Official CC SRDs:** SRD **5.1** and **5.2.1** supply legally reusable 2014-era and 2024-era source content with their separate required CC BY 4.0 attributions. These are **source data**, not automatically the native Dorks & Dice ruleset. Preserve original artifacts, version IDs, hashes, source locators, exact provenance, and any alterations. The full 5etools catalog contains material outside these SRDs and can not be redistributed wholesale on an SRD-only rationale.
2. **Earlier implementation on `main`:** existing semantic competency identities, reviewed aliases, family memberships, facet-level skill/tool mechanics, class/species normalization, relationships, and existing test expectations are recovery evidence. Do not carry over the legacy application framework or automatically assume all its code reflects a final design decision.
3. **Current approved design documentation:** `docs/rewrite/` and reviewed historical `docs/` decisions establish the native departures, including the universal competency system, skill composition, modified rolling rules, defenses and epic Level Bands. Label unresolved choices as unresolved.

## Ingestion process

- Store each official SRD **as its own immutable source**, never merge its editions by matching names.
- Validate the integrity, license attribution and source identifiers of each extracted entity before considering it for conversion.
- Compare 5.1 and 5.2.1 source entity semantics. A shared skill name may have two distinct source occurrences and an explicit revision/reprint relation without becoming two separate native competencies.
- Apply reviewed Dorks & Dice decisions to produce or extend the **native** entity, with one JSON file per native entity in `data/native/`.
- Retain explicit lineage for source versions, imported occurrences, alias/composite/partial relationships, and each native adaptation.
- Report unknown or contradictory source details as unresolved; do not guess or require a Rules Lawyer to repeatedly review an already-approved mapping.
- Generate discovery indexes as build artifacts when useful. Do not turn `skills.json` or `competencies.json` into hand-maintained monolithic native files.

## Skills versus competencies

Source-native **skills**, source-native **tools**, and universal Dorks & Dice **competencies** must not be flattened together.

The reviewed system's competency catalog includes direct aliases (Bluff → Deception), shared training identities with distinct skill/tool facets (Craft (alchemy) + Alchemist's Supplies → Alchemy), independently ranked older competencies (Hide, Move Silently), one-way arithmetic composites (Hide + Move Silently → Stealth), and organized but independently ranked families (Craft, Perform, Profession). `Knowledge (X)` projects to competency `X`; historical Speak Language belongs to language proficiency rather than ordinary competencies. Some related concepts, such as Open Lock/Disable Device and Thieves' Tools, remain separate except for scoped relationships.

The source skill indexes under `data/reference/srd/` are **discovery fixtures only**; verified native skill definitions will live under `data/native/skills/<skill>.json` if a distinct native skill mechanic is necessary, and universal identities under `data/native/competencies/<competency>.json`. Their exact schemas require foundation review. `data/reference/competency-reconciliation-cases.json` records selected existing decisions as future acceptance test vectors.

## Coverage gates

The initial slice verifies the 18 familiar 5e skills for *both* editions as an index, and exercises reviewed cross-edition identity/composition semantics. It is **not** a claim to have imported the complete rules, spells, feats, classes, equipment, monsters, or either official full-text SRD.

Before an authoritative native baseline is claimed: finish importing and validating the two official SRD artifacts; establish the explicit native schema; compare and incorporate *all* approved deviations, not just skill examples; and record acceptance status per rule category. Implementation-language selection, gameplay execution, database changes, deployment and merge remain separate approvals.
