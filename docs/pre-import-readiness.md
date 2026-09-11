# Pre-import readiness checklist

Before broad rule ingestion, Rules Core should prove the workflow on a deliberately small representative corpus.

## Framework status

As of September 10, 2026, the pre-import framework path is implemented and covered by integration tests on `feature/pre-import-source-consolidation`.

The framework now provides:

- non-persisting import preview showing new entities, unchanged entities, and new immutable revisions;
- canonical D&D edition metadata with compatibility aliases for older 2014/2024 labels;
- stable source identity conventions separating game edition, publication, release, entity, and revision;
- aggregate-file partitioning by item-level 5e.tools source code before a logical work/release import;
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
- `MixedSourceAggregateImportIntegrationTests`, covering partitioning one physical aggregate into separate logical source releases while preserving complete selected entity objects;
- `PreImportRepresentativeLifecycleIntegrationTests`, covering the complete connected workflow from source preview through campaign resolution and later source-revision review;
- `RealSrdImportPilotIntegrationTests`, exercising the ingestion and version-detection boundary with attributed CC-BY SRD 5.1/5.2.1 action data.

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

## Real SRD pilot

The first real-data pilot uses the `Attack` action records from SRD 5.1 (`SRD51`) and SRD 5.2.1 (`SRD52`) in the SRD-only `CoolFireGiant/hewnhero-srd` 5e.tools-shaped data pack. The two-record fixture and its CC-BY attribution are stored under `tests/RulesCore.IntegrationTests/Fixtures/real-srd/`.

The pilot proves that Rules Core can:

- partition one physical aggregate by item-level source code into separate 5e and 5.5e logical source releases;
- preserve actual nested 5e.tools arrays/objects and inline reference tags in immutable source revisions;
- preserve explicit `reprintedAs` provenance from the older implementation;
- retain the two versions as distinct source entities rather than revisions of one source identity;
- detect the later implementation as a high-confidence cross-edition candidate without automatically binding it to a concept;
- pass the complete browser, PostgreSQL integration, .NET, and container validation workflow with the real fixture.

The pilot also exposed why a physical upstream file can not be assumed to equal one Rules Core source release. Source Administration now warns on an unfiltered aggregate containing multiple source codes and can explicitly partition the same submitted JSON by `IncludedSourceCodes`.

## Upstream data-quality boundary

A later inspection of the same unofficial SRD-only repack found at least one ambiguous feat identity: two `Magic Initiate` records are labeled `SRD52`, while the first is page 168 and carries a self-referential `reprintedAs: Magic Initiate|SRD52`; the second is the current 2024/SRD52 form on page 201. This would correctly trigger Rules Core's existing duplicate natural-identity rejection for a broad unfiltered feat import.

Rules Core must not weaken durable source identity to accommodate that malformed provenance. In particular, page number should not be added to source identity merely to make the unofficial repack importable: a page change is publication metadata and could incorrectly turn a source revision into a new entity.

Before broad ingestion, source data should therefore be validated for duplicate natural identities and contradictory/self-referential version metadata. When an upstream distribution is internally ambiguous, ingestion should stop for review or use a corrected/licensable source representation rather than guessing an identity split.

## Broad-ingestion decision

The framework and first real-data pilot no longer expose a structural blocker. Broad ingestion can begin incrementally without adding more speculative framework abstractions first.

The next ingestion phase should remain staged by source family/entity type so that new real-data assumptions are visible early. Each candidate corpus should first run through preview and identity/provenance validation; duplicate identities or contradictory lineage hints should be treated as source-data review items, not silently normalized away.
