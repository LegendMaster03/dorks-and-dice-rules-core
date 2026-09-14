# Pre-import readiness checklist

Before broad rule ingestion, Rules Core should prove the workflow on representative real and synthetic sources without weakening native source identity to accommodate malformed upstream data.

## Current framework status

The pre-import framework now provides:

- non-persisting import preview showing new entities, unchanged entities, and new immutable revisions;
- adapter-neutral ingestion into `SourcePackage`, `SourceRepresentation`, `SourceEntity`, and `SourceEntityRevision`;
- canonical D&D edition metadata with compatibility aliases for older 2014/2024 labels;
- format-independent `CanonicalPublication`, `CanonicalEntity`, and `CanonicalSourceOccurrence` recognition;
- byte-preserving source representations for structured files and text-readable PDFs;
- 5e.tools, PCGen 3.x, and PDF adapters behind one normalized source contract;
- aggregate-file partitioning where a physical upstream file contains multiple logical publications/source codes;
- advisory detection of likely cross-source/cross-edition versions of the same conceptual rule;
- explicit, reviewable source lineage including branching and merging histories;
- trusted source-lineage aliases for bootstrap-confirmed exact identity across different semantic projections;
- canonical reconciliation conflicts isolated from otherwise valid Source Layer persistence;
- manual binding of implementations to stable Rules Layer concepts;
- multi-version comparison/consolidation with exact source revisions;
- contribution provenance identifying which source revisions were incorporated or reviewed;
- preview -> save -> publish separation and independent restricted-source access checks;
- explicit source-revision adoption after review;
- append-only deliberate source-revision rejection scoped to the exact global decision and reviewed revision;
- runtime substitution through an accessible representation of the same canonical entity without exposing another user's private source.

The older `WorkKey`/`EditionKey` compatibility fields may still appear on legacy import and maintenance request contracts, but there is no persisted `SourceWork` or `SourceEdition` hierarchy in the current Source Layer.

## Representative lifecycle

The synthetic representative lifecycle exercises these behaviors:

1. Stable re-preview/re-import remains unchanged.
2. A renamed/reworked implementation across source families remains explicit rather than being merged by name.
3. UA/playtest and later published implementations can be related without treating publication brand as identity.
4. A changed source body for the same native identity creates a new immutable `SourceEntityRevision`.
5. Structured array/list consolidation carries through global and campaign publication with contribution provenance.
6. A recognized canonical-identity conflict can leave the native representation/entity/revision intact for later reconciliation.
7. Retrying the same immutable bytes after correcting reconciliation evidence reuses the existing representation/revision rather than fabricating another version.

The connected workflow is:

```text
preview source
-> import immutable representation/entities/revisions
-> reconcile publication/entity identity
-> inspect source provenance
-> detect likely versions/relationships
-> confirm Rules Layer bindings
-> compare implementations
-> manually consolidate or select
-> preview the resolved rule
-> save the append-only decision
-> publish a global ruleset
-> resolve with source-access enforcement
-> select a campaign baseline
-> create/publish a campaign variation
-> reimport changed native source content
-> review, adopt, or deliberately reject the new source revision
```

Canonical reconciliation is deliberately downstream of Source Layer validity. A reconciliation issue should block the affected canonical association from being treated as settled; it should not erase valid source bytes or native revision history.

## Real SRD pilot

The original real-data pilot used `Attack` action records from SRD 5.1 (`SRD51`) and SRD 5.2.1 (`SRD52`) in the SRD-only `CoolFireGiant/hewnhero-srd` 5e.tools-shaped data pack. The fixture demonstrated that Rules Core can:

- partition one physical aggregate by item-level source code;
- preserve nested 5e.tools arrays/objects and inline reference tags in immutable source revisions;
- preserve explicit `reprintedAs` provenance;
- retain changed implementations as distinct source identities rather than forcing them into one revision chain;
- detect a later implementation as a high-confidence cross-edition candidate without automatically binding it to a Rules Layer concept.

Subsequent adapter work generalized this boundary beyond 5e.tools. The same Source Layer/canonical architecture now accepts PCGen 3.x data and text-readable PDFs without translating either into a fake 5e.tools document model.

## Upstream data-quality boundary

The SRD pilot exposed why upstream convenience datasets must not define durable identity policy. One unofficial aggregate contained two `Magic Initiate` records labeled `SRD52`, including a malformed/self-referential provenance entry.

Rules Core must not make page number part of native identity merely to import such a dataset. Page movement is provenance/location information and can occur without creating a new source identity.

The same rule applies to other adapters:

- duplicate native keys with contradictory identity evidence require review;
- source-specific aliases are not globally trusted merely because their syntax looks familiar;
- parser uncertainty must remain parser/source evidence rather than becoming a guessed canonical merge;
- malformed provenance should be corrected, isolated, or explicitly reconciled rather than silently normalized away.

## Reconciliation readiness

Before using a source family for broad ingestion, verify all of the following:

- The adapter preserves the original physical bytes and enough native metadata to reproduce identity decisions.
- Stable native keys do not depend on page numbers, transient paths, property order, or Dorks & Dice rulings unless the source format itself makes those values identity-defining.
- Publication evidence distinguishes strong identifiers from contextual aliases.
- Exact semantic matching excludes only fields that are safely non-mechanical for that adapter.
- Trusted source-lineage aliases are emitted only for an actually trusted lineage and only after bootstrap confirmation establishes their canonical meaning.
- Mechanical revisions remain distinct canonical entities when appropriate and record explicit relationships.
- A canonical conflict produces an explicit reconciliation issue rather than rolling back valid Source Layer data.
- Retrying the same bytes after reconciliation does not create another representation or revision.
- Package grants remain independent even when canonical IDs are shared.

## Broad-ingestion decision

The framework no longer has a structural blocker requiring another speculative source hierarchy before broad ingestion. Ingestion should continue incrementally by source family/entity type so real-data assumptions remain visible.

Each corpus should first pass adapter/native-identity validation, canonical reconciliation, and access-boundary tests. Duplicate identities, contradictory lineage hints, parser errors, and ambiguous relationships are review items. They should not be resolved by weakening durable identity or by broadening source access.
