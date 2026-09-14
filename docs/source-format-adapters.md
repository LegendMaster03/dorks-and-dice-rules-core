# Source Format Adapters

Rules Core treats physical source representation, mechanical translation, and canonical recognition as separate concerns. A compatible file can be imported even when Rules Core has never seen its publication or rules content before.

The normal pipeline is:

`physical bytes -> ISourceFormatAdapter -> native records/publication evidence -> Source Layer persistence -> mechanical translation -> canonical reconciliation -> optional Rules Layer binding`

Canonical reconciliation is downstream of ingestion. Failure to identify a canonical publication/entity does not make otherwise valid source material invalid.

The detailed mechanical-content contract is documented in `mechanical-content-schema.md`.

## Implemented adapters

The current adapter set is:

- `FiveEToolsSourceFormatAdapter` for native 5e.tools-shaped JSON;
- `PcGenSourceFormatAdapter` for PCGen `.pcc` and `.lst` 3.x data;
- `PdfSourceFormatAdapter` for PDFs with a usable text layer.

The registry is format-oriented rather than edition-oriented. This does not imply that the mechanical content schema is format-neutral. Source ingestion is format-neutral; translated mechanical content uses the 5e.tools family model as the reference schema.

## Representation contract

Each accepted physical artifact becomes an immutable `source_representation` containing its original bytes and representation-level provenance.

### Native source records

`NormalizedSourceRecord` supplies source-native evidence:

- source entity type and display name;
- source/provider code when the format has one;
- stable native key;
- complete `RawJson` representing what the adapter retained from the source;
- locator and publication-local evidence when known;
- `NativeIdentityJson` for source-specific identity metadata;
- optional strong canonical aliases when a trusted source lineage has been established.

`RawJson` is the immutable Source Layer body. Its canonicalized SHA-256 determines whether a new source-entity revision is required.

### Mechanical content

`SourceEntityRevision.ContentJson` is separate from `RawJson`. It contains the Rules Core mechanical body after translation into the 5e.tools-derived reference schema.

For ordinary native 5e.tools entities, this translation is identity-preserving: the complete native entity object is the mechanical body, including unknown upstream fields.

For recognized PCGen entities, translation produces the applicable spell, feat, item, race/species, creature, or other family shape. Mechanics that have no faithful 5e.tools equivalent are preserved under an explicit `_rulesCore` extension rather than discarded or forced into an unrelated field.

PDF fragments and unsupported source records may legitimately have no `ContentJson` at all.

A translation-only change does not create a new native revision. Native revision history remains a history of source-native content.

### Publication evidence

`NormalizedSourcePublication` supplies bibliographic evidence independently from source records: local publication key, title, publisher, D&D edition, exact date when actually known, and external identifiers.

Publication evidence is reconciled into `canonical_publication`. There is no persisted `SourceWork` or `SourceEdition` parent for imported entities.

## Adapter responsibilities

An adapter should:

1. reject material it can not read safely;
2. retain the original physical representation through the common Source Layer;
3. preserve native identity and provenance rather than inventing Dorks & Dice identities;
4. extract publication evidence only when the source actually supports it;
5. emit the most specific native rule entity boundary it can establish confidently;
6. retain unsupported or ambiguous material as source evidence rather than silently dropping or guessing it;
7. provide stable native keys so reimport is deterministic;
8. leave canonical matching to the downstream resolver.

Translation is a separate responsibility from lossless native parsing. A source adapter must not flatten already-rich 5e.tools entities merely to make all formats look alike.

An adapter must not require a pre-existing canonical publication, 5e.tools source code, rule concept, or another user's representation.

## 5e.tools

5e.tools is handled as the reference structured mechanical representation, not as an interchange format that must be flattened.

The adapter preserves complete accepted entity objects, including unknown fields. Native identity evidence such as `source`, applicable `id`/`uniqueId`, corpus `id`, `parentSource`, `_meta`, edition hints, copy/version fields, nested mechanical structures, renderer tags, and family-specific fields retain their upstream meanings.

These fields are not assumed to be universal. The upstream family schemas determine which conventions apply to each entity family.

Corpus registry identity and source code are intentionally separate. A parent publication and child adventure can therefore share a `source` value while retaining distinct corpus identities.

Known native edition projection is limited to publication evidence:

- `classic` -> `5e`
- `one` -> `5.5e`

That projection does not alter the stored native JSON.

The 5e.tools detector uses known entity-array/schema families rather than accepting arbitrary JSON arrays. Generic JSON documents are not automatically treated as 5e.tools.

## PCGen

PCGen is the first persistent 3.x structured source translator.

### PCC files

`.pcc` files preserve campaign metadata and references. Relevant fields include campaign/key, game mode, publisher, long/short source names, source date, and referenced LST families.

Game-mode projection currently recognizes 3e and 35e as `3e` and `3.5e`. An exact `yyyy-MM-dd` source date can become canonical publication-date evidence. A partial source date such as `2003-07` remains preserved raw metadata and is not converted into an invented exact day.

### LST files

Supported single-line families are parsed into source-native records with stable keys and line locators. Raw lines, duplicate tags, unknown tags, source metadata, and unsupported fragments remain available in `RawJson`.

Recognized records are then translated into the same family-shaped `ContentJson` contract used by native 5e.tools content. The translator maps only mechanics with a defensible equivalent and stores unmapped 3.x-specific material under `_rulesCore.pcgen.unmappedSegments`.

A direct book-local PCC reference can provide publication context for an LST file. A shared list referenced by multiple publications may provide entity-family information but remains publication-unassociated when ownership is ambiguous.

PCGen operations such as `.COPY=`, `.MOD`, and `.FORGET` are preserved as `pcgen-operation` records rather than being guessed into standalone complete rules. Unsupported/multi-line families are preserved as `pcgen-fragment` evidence until a translator can model their semantics correctly.

### Trusted PCGen lineage

PCGen syntax alone is not a strong canonical identity signal. A local upload or unrelated repository can use the same format.

Only an artifact whose actual source URI proves it came from the official `PCGen/pcgen` or `PCGen/pcgen-newsources` GitHub repositories may present trusted PCGen lineage aliases to canonical reconciliation.

Even then, the alias is useful only after the bootstrap/reconciliation process has confirmed what canonical entity that source-specific identity represents. Rules Core does not globally merge entities merely because their names match.

Strong canonical entity aliases are versioned by source-lineage scheme, native alias value, and the translated mechanical fingerprint. This allows the same upstream native key to identify a later mechanical revision without incorrectly collapsing the revision back into the earlier canonical entity.

## PDF

A text-readable PDF is a first-class physical representation. The adapter:

- retains the original PDF bytes;
- extracts the text layer with PdfPig;
- rejects scan-only files that provide no usable text;
- extracts explicit publication evidence such as metadata title, labeled publisher/system/date values, and ISBN when present;
- retains each readable page as a source fragment with page provenance;
- does not infer monsters, spells, feats, or other entity types from weak layout cues.

A generic source fragment is valid Source Layer content and normally has no translated mechanical body. Later extraction improvements can add legitimate translated entities without rewriting the original PDF representation.

OCR is not implemented in the current adapter.

## Canonical publication reconciliation

Canonical publication identity is representation-neutral. Strong identifiers such as ISBN can resolve directly. Source-specific identifiers are interpreted according to their scheme. Contextual aliases such as a 5e.tools source code or `PHB` are not globally unique and require corroborating context.

Bibliographic evidence can fill previously missing canonical metadata. Conflicting established metadata is not silently overwritten; the observation is recorded with representation provenance for review.

Sparse title-only evidence does not force a merge. Content overlap is used only under guarded conditions and ambiguous candidates remain separate.

## Canonical entity reconciliation

Canonical identity remains separate from both native representation and mechanical translation. Different packages and representations can retain independent access and native revision history while resolving to the same canonical entity.

Translated mechanical fingerprints can contribute to exact-identity reconciliation, but they do not by themselves override strong conflicting source identity evidence. Cross-format reuse with different mechanical fingerprints requires trusted lineage/reconciliation evidence rather than name-only matching.

Mechanical changes in one native source lineage remain explicit relationships or revisions as appropriate. A strong alias can not be used to collapse a mechanically changed source revision into its prior canonical entity.

Canonical reconciliation conflicts are isolated from native ingestion. A valid `SourcePackage`, `SourceRepresentation`, `SourceEntity`, and `SourceEntityRevision` remain committed when one publication group can not be reconciled safely. The conflict is persisted as representation-scoped `source_reconciliation_issue` metadata and returned by the normalized import result.

Canonical occurrences remain publication-specific. Reprint/revision/rename/variant relationships remain explicit and directed from predecessor/base to later/related entities.

## Access model

Source packages and their representations/entities/revisions remain access-scoped. Canonical publications, entities, aliases, occurrences, fingerprints, relationship records, and reconciliation issues are shared recognition metadata only.

A user who uploads a private representation does not grant another user access to that representation. A later independent import may reuse the same canonical IDs while remaining separately stored and separately granted.

Translated `ContentJson` does not weaken this boundary. Rules Layer and source-browser consumers still resolve an accessible source revision before returning mechanical content.

## Web-source behavior

A Web source re-enters the same adapter pipeline on refresh. GitHub tree sources enumerate candidate files, fetch each physical artifact separately, and use the batch-adapter path when a format needs cross-file context such as PCGen PCC-to-LST references or 5e.tools corpus metadata.

GitHub tree version checks use commit identity. Other HTTP sources use available `ETag`/`Last-Modified` metadata. A full re-import is skipped when the upstream version has not changed.

The refresh worker is an ASP.NET hosted service and queued import processor; there is no detached fire-and-forget import path.

## 3.x bootstrap boundary

The eventual 3e/3.5e bootstrap is a developer seeding workflow built on the same persistent adapters, translator, and canonical resolver used by normal imports. It is not a separate global source-content database and does not require Rules Core to maintain a mirror of PCGen.

The same principle applies to 5e/5.5e source seeding: the special import capability establishes source access and recognition while ordinary persistence remains package-scoped. CI relies on deterministic local fixtures rather than live upstream repositories.

The workbench may classify candidate pairs as exact identity, corroborated exact identity, reprint, revision, rename, variant, same-name different entity, bad source data, parser error, or unresolved. Fully confirmed exact identities register strong source-lineage aliases. Fully confirmed reprint/revision/rename/variant decisions register the candidate lineage alias and persist a directed canonical relationship from predecessor/base to candidate. Partial classifications remain review state until the canonical endpoints are independently confirmed.
