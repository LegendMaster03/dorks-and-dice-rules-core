# Pre-import readiness checklist

Before broad rule ingestion, Rules Core should prove the workflow on a deliberately small representative corpus.

## Required framework capabilities

The pre-import framework is expected to provide:

- non-persisting import preview showing new entities, unchanged entities, and new immutable revisions;
- canonical D&D edition metadata with compatibility aliases for older 2014/2024 labels;
- stable source identity conventions separating game edition, publication, release, entity, and revision;
- advisory detection of likely cross-source/cross-edition versions of the same conceptual rule;
- explicit, reviewable source lineage including branching and merging histories;
- manual binding of detected implementations to stable rule concepts;
- a multi-version consolidation workspace with exact source documents/revisions;
- contribution provenance identifying which additional source revisions were incorporated or reviewed;
- existing preview -> save -> publish separation and independent restricted-source access checks.

## First test corpus

Do not begin with an entire edition. Use a small public/licensable corpus containing several distinct behaviors:

1. One rule whose name is stable across versions.
2. One rule that was renamed or substantially reworked across editions.
3. One UA/playtest implementation with a later published implementation.
4. One entity that is reimported with a changed representation so SourceEntityRevision handling is exercised separately from lineage.
5. One rule containing an array/list so structured patch consolidation is exercised.

The test should execute the complete lifecycle:

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
-> review/adopt or deliberately reject the new source revision
```

Bulk ingestion should wait until this representative path exposes no structural blocker.
