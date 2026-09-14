# Source Format Adapters

Rules Core treats a physical source representation and canonical recognition as separate concerns. A compatible file can be imported even when Rules Core has never seen its publication or rules content before.

The normal pipeline is:

`physical bytes -> ISourceFormatAdapter -> lossless normalized records/publication evidence -> Source Layer persistence -> canonical reconciliation -> optional Rules Layer binding`

Canonical reconciliation is downstream of ingestion. Failure to identify a canonical publication/entity does not make otherwise valid source material invalid.

## Implemented adapters

The current adapter set is:

- `FiveEToolsSourceFormatAdapter` for native 5e.tools-shaped JSON;
- `PcGenSourceFormatAdapter` for PCGen `.pcc` and `.lst` 3.x data;
- `PdfSourceFormatAdapter` for PDFs with a usable text layer.

The registry is format-oriented rather than edition-oriented. Future 3.x Web translators, OCR, RTF, or other structured inputs should enter through the same normalized contract instead of adding another persistence hierarchy.

## Representation contract

Each accepted physical artifact becomes an immutable `source_representation` containing its original bytes and representation-level provenance. Adapter output uses two independent evidence channels:

### Source records

`NormalizedSourceRecord` supplies:

- source entity type and display name;
- source/provider code when the format has one;
- stable native key;
- complete `RawJson` representing what the adapter retained from the source;
- locator and publication-local evidence when known;
- `NativeIdentityJson` for source-specific identity metadata;
- optional `SemanticJson` for rule-bearing comparison;
- optional strong canonical aliases when a trusted source lineage has been established.

`RawJson` is the immutable Source Layer body. Its canonicalized SHA-256 determines whether a new source-entity revision is required.

`SemanticJson` is optional comparison input only. It allows an adapter to exclude provenance-only fields such as page numbers, repository paths, or source metadata from canonical mechanical comparison while leaving those fields intact in `RawJson`. It never rewrites the source body or changes its revision fingerprint.

### Publication evidence

`NormalizedSourcePublication` supplies bibliographic evidence independently from source records: local publication key, title, publisher, D&D edition, exact date when actually known, and external identifiers.

Publication evidence is reconciled into `canonical_publication`. There is no persisted `SourceWork` or `SourceEdition` parent for imported entities.

## Adapter responsibilities

An adapter should:

1. reject material it can not read safely;
2. retain the original physical representation through the common Source Layer;
3. preserve native identity and provenance rather than inventing Dorks & Dice identities;
4. extract publication evidence only when the source actually supports it;
5. emit the most specific rule entity boundary it can establish confidently;
6. retain unsupported or ambiguous material as source evidence rather than silently dropping or guessing it;
7. provide stable native keys so reimport is deterministic;
8. use `SemanticJson` only when it can make mechanical comparison more representation-neutral without losing the raw source;
9. leave canonical matching to the downstream resolver.

An adapter must not require a pre-existing canonical publication, 5e.tools source code, rule concept, or another user's representation.

## 5e.tools

5e.tools is handled as a native structured representation, not as an interchange format that must be flattened.

The adapter preserves complete accepted entity objects, including unknown fields. Native identity evidence such as `source`, `id`, `uniqueId`, corpus `id`, `parentSource`, `_meta`, and edition hints retain their upstream meanings.

Corpus registry identity and source code are intentionally separate. A parent publication and child adventure can therefore share a `source` value while retaining distinct corpus identities.

Known native edition projection is limited to publication evidence:

- `classic` -> `5e`
- `one` -> `5.5e`

That projection does not alter the stored native JSON.

The 5e.tools detector uses known entity-array/schema families rather than accepting arbitrary JSON arrays. Generic JSON documents are not automatically treated as 5e.tools.

## PCGen

PCGen is the first persistent 3.x structured translator.

### PCC files

`.pcc` files preserve campaign metadata and references. Relevant fields include campaign/key, game mode, publisher, long/short source names, source date, and referenced LST families.

Game-mode projection currently recognizes 3e and 35e as `3e` and `3.5e`. An exact `yyyy-MM-dd` source date can become canonical publication-date evidence. A partial source date such as `2003-07` remains preserved raw metadata and is not converted into an invented exact day.

### LST files

Supported single-line families are translated to native records with stable keys and line locators. Raw lines, duplicate tags, unknown tags, and source metadata remain available in `RawJson`.

`SemanticJson` removes source/provenance tags such as `SOURCEPAGE` from mechanical comparison while retaining mechanical tags and descriptions.

A direct book-local PCC reference can provide publication context for an LST file. A shared list referenced by multiple publications may provide entity-family information but remains publication-unassociated when ownership is ambiguous.

PCGen operations such as `.COPY=`, `.MOD`, and `.FORGET` are preserved as `pcgen-operation` records rather than being guessed into standalone complete rules. Unsupported/multi-line families are preserved as `pcgen-fragment` evidence until a translator can model their semantics correctly.

### Trusted PCGen lineage

PCGen syntax alone is not a strong canonical identity signal. A local upload or unrelated repository can use the same format.

Only an artifact whose actual source URI proves it came from the official `PCGen/pcgen` or `PCGen/pcgen-newsources` GitHub repositories may present trusted PCGen lineage aliases to canonical reconciliation.

Even then, the alias is useful only after the bootstrap/reconciliation process has confirmed what canonical entity that source-specific identity represents. Rules Core does not globally merge entities merely because their names match.

Strong canonical entity aliases are versioned by source-lineage scheme, native alias value, and the source-specific semantic fingerprint. This allows the same upstream native key to identify a later mechanical revision without incorrectly collapsing the revision back into the earlier canonical entity.

## PDF

A text-readable PDF is a first-class physical representation. The adapter:

- retains the original PDF bytes;
- extracts the text layer with PdfPig;
- rejects scan-only files that provide no usable text;
- extracts explicit publication evidence such as metadata title, labeled publisher/system/date values, and ISBN when present;
- retains each readable page as a source fragment with page provenance;
- does not infer monsters, spells, feats, or other entity types from weak layout cues.

A generic source fragment is valid Source Layer content. Later extraction improvements can add better translated entities without rewriting the original PDF representation.

OCR is not implemented in the current adapter.

## Canonical publication reconciliation

Canonical publication identity is representation-neutral. Strong identifiers such as ISBN can resolve directly. Source-specific identifiers are interpreted according to their scheme. Contextual aliases such as a 5e.tools source code or `PHB` are not globally unique and require corroborating context.

Bibliographic evidence can fill previously missing canonical metadata. Conflicting established metadata is not silently overwritten; the observation is recorded with representation provenance for review.

Sparse title-only evidence does not force a merge. Content overlap is used only under guarded conditions and ambiguous candidates remain separate.

## Canonical entity reconciliation

Exact semantic fingerprints can reuse canonical entities across representations when the comparison documents are structurally equivalent.

Different source formats may legitimately encode the same rule with different semantic structures. Rules Core therefore does not require every exact representation of one canonical entity to share one fingerprint. Cross-format reuse with different fingerprints requires a strong, bootstrap-confirmed source-lineage alias rather than name-only matching.

Mechanical changes in one native source lineage remain explicit revisions. A strong alias can not be used to collapse a mechanically changed source revision into its prior canonical entity.

Canonical occurrences remain publication-specific. Reprint/revision/rename/variant relationships remain explicit rather than being inferred solely from matching names.

## Access model

Source packages and their representations/entities/revisions remain access-scoped. Canonical publications, entities, aliases, occurrences, fingerprints, and relationship records are shared recognition metadata only.

A user who uploads a private representation does not grant another user access to that representation. A later independent import may reuse the same canonical IDs while remaining separately stored and separately granted.

## Web-source behavior

A Web source re-enters the same adapter pipeline on refresh. GitHub tree sources enumerate candidate files, fetch each physical artifact separately, and use the batch-adapter path when a format needs cross-file context such as PCGen PCC-to-LST references or 5e.tools corpus metadata.

GitHub tree version checks use commit identity. Other HTTP sources use available `ETag`/`Last-Modified` metadata. A full re-import is skipped when the upstream version has not changed.

The refresh worker is an ASP.NET hosted service and queued import processor; there is no detached fire-and-forget import path.

## 3.x bootstrap boundary

The eventual 3e/3.5e bootstrap is a developer seeding workflow built on the same persistent adapters and canonical resolver used by normal imports. It is not a separate global source-content database.

The workbench may classify candidate pairs as exact identity, reprint, 3.0-to-3.5 revision, rename, variant, source-data error, parser error, or unresolved. Confirmed exact identities can register strong source-lineage aliases. Only canonical identity/matching knowledge becomes shared globally; non-SRD source bodies remain governed by their packages and grants.
