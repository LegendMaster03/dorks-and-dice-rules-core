# Pre-import readiness checklist

Before broad rule ingestion, Rules Core should prove the workflow on a deliberately small representative corpus.

## Framework status

As of September 10, 2026, the pre-import framework path is implemented and covered by integration tests on `feature/pre-import-source-consolidation`.

The framework now provides:

- non-persisting import preview showing new entities, unchanged entities, and new immutable revisions;
- canonical D&D edition metadata with compatibility aliases for older 2014/2024 labels;
- stable source identity conventions separating game edition, publication, release, entity, and revision;
- advisory detection of likely cross-source/cross-edition versions of the same conceptual rule;
- explicit, reviewable source lineage including branching and merging histories;
- manual binding of detected implementations to stable rule concepts;
- a multi-version consolidation workspace with exact source documents/revisions;
- contribution provenance identifying which additional source revisions were incorporated or reviewed;
- preview -> save -> publish separation and independent restricted-source access checks;
- explicit source-revision adoption after review;
- append-only deliberate source-revision rejection scoped to the exact global decision and reviewed source revision.

The main framework tests are:

- `SourceVersioningAndImportPreviewIntegrationTests`, covering import preview, alias normalization, version detection, manual binding, lineage, consolidation, provenance, and immutable source revisions;
- `SourceRevisionRejectionIntegrationTests`, covering deliberate rejection, idempotence, stale review tokens, decision-scoped suppression, and resurfacing after a newer decision or source revision;
- `PreImportRepresentativeLifecycleIntegrationTests`, covering the complete connected workflow from source preview through campaign resolution and later source-revision review.

## Representative framework corpus

The synthetic representative lifecycle corpus intentionally exercises the five required behaviors:

1. A stable re-preview/re-import path that remains unchanged.
2. A renamed/reworked implementation across the 5e and 5.5e source families.
3. A UA/playtest implementation explicitly related to a later published implementation.
4. A selected source entity reimported with changed content, producing a new immutable `SourceEntityRevision` rather than new lineage.
5. A source rule containing an array/list that is consolidated with a structured array patch and then carried through global and campaign publication.

The integration path executes:

```text
import preview
-> import
-> inspect source provenance
-> detect likely versions
-> confirm concept bindings and lineage
-> compare implementations
-> manually consolidate
-> preview the resolved rule
-> save the append-only decision
-> publish a global ruleset
-> browse the resolved global rule
-> select a campaign baseline
-> create one campaign variation
-> publish the campaign ruleset
-> resolve the campaign rule
-> reimport a changed source entity
-> review and deliberately reject the new source revision
```

The rejection path is used in the representative lifecycle because adoption already has independent integration coverage. Rejection changes only review provenance; it does not alter or publish the Rules Layer.

## Remaining gate before broad ingestion

The synthetic framework proof is not a substitute for a real-data pilot. Before importing an entire edition or large third-party corpus, run the same workflow against a deliberately small **public/licensable real corpus** containing representative source documents.

The real-data pilot should be treated as a schema and normalization stress test. Its purpose is to expose assumptions in source identity, 5e.tools-shaped document preservation, edition/release metadata, version detection, and structured patches that synthetic fixtures may not reveal.

If that pilot exposes no structural blocker, broad ingestion can begin without adding more speculative framework abstractions first.
