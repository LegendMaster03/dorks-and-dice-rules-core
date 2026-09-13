# Source ingestion architecture

This document defines the durable boundary between imported source content, shared recognition data, and the Rules Layer.

## Core rule

**Rules Core may globally know the identity of non-SRD material without globally providing that material.**

Canonical recognition is metadata. It is not a source grant and it is not permission to read another user's imported representation.

## Content classes

### Bundled public content

The bundled 3e, 3.5e, 5e / SRD 5.1, and 5.5e / SRD 5.2.1 snapshots are application content. Their source packages are public and may be bootstrapped by Rules Core.

### Imported non-SRD content

All other content enters through a source import. The source package and its representations remain subject to the normal package/grant boundary. Canonical identities learned from one import may be reused by later imports without transferring access to the first import.

## Durable Source Layer model

The persistence model is deliberately shallower than the historical `SourcePackage -> SourceWork -> SourceEdition -> SourceEntity` hierarchy.

### `SourcePackage`

A package is the access and distribution boundary. It owns imported representations and imported entities. `IsPublic` and `UserSourceGrant` determine who may read package-owned content.

A package does not imply a publication, work, sourcebook, edition, or upstream corpus.

### `SourceRepresentation`

A representation is an immutable physical input snapshot: a JSON file, PDF, RTF, HTML response, or another supplied artifact.

It records at least the package, format key, origin identity, file name/source URI, media type, SHA-256, byte length, original bytes, format-specific metadata, import time, and predecessor relationship when a refresh produces changed bytes.

Reimporting the same origin with identical bytes reuses the existing representation. Changed bytes create a new immutable representation.

### `SourceEntity`

A source entity is an import-scoped logical entity under a package. It is identified by the native/source-specific identity supplied by the format adapter, not by an invented Rules Core publication hierarchy.

It records the package, format key, entity/property type, display name, source code when the format has one, native key, native identity/provenance JSON, and creation time.

For a native structured format, the native key follows that format's identity rules. For an extracted document format, the key is produced by the persistent extractor/translator and remains traceable to the representation.

### `SourceEntityRevision`

A source entity revision is the immutable imported body for one entity version. It records the source entity, source representation that produced it, revision number, canonical content fingerprint, complete native or translated JSON body, extraction/locator provenance when applicable, and import time.

A new representation does not force a new entity revision when the entity body is unchanged. A changed entity body creates the next revision.

The raw entity JSON is private whenever its package is private, even if shared canonical records point at the same underlying identity.

## Native structured formats

### 5e.tools

5e.tools is a native structured format. The adapter is an identity/projection reader, not a lossy normalizer.

The import sequence is:

`5e.tools artifact -> SourceRepresentation -> native SourceEntity/SourceEntityRevision -> canonical recognition -> optional Rules Layer binding`

The importer preserves each accepted entity object's complete JSON and preserves source-specific identity evidence such as `source`, `id`, `uniqueId`, corpus `id`, `parentSource`, `_meta`, edition hints, and other schema-defined fields without assigning new meanings to them.

A 5e.tools `source` value is not assumed to be a globally unique publication key. Corpus `id`, `source`, and `parentSource` retain their upstream meanings. Registry rows and corpus bodies may contribute publication evidence downstream without controlling whether valid upstream data can be stored.

For D&D edition projection, upstream `edition: classic` maps to canonical `5e` evidence and `edition: one` maps to canonical `5.5e` evidence. This projection does not rewrite the stored native object.

The first broad 5e.tools import can seed canonical recognition using the normal user-facing importer. It remains the importing user's private package unless the package itself is public. No global 5e.tools content mirror is created.

## Extracted formats

PDF, RTF, and similar document formats are not treated as native rule-entity schemas.

Their sequence is:

`original document -> SourceRepresentation -> persistent extractor/translator -> SourceEntity/SourceEntityRevision -> canonical recognition`

The original bytes remain authoritative provenance. Extracted/translated entities carry page/section/range provenance back to that representation. Canonical reconciliation is downstream of extraction and can not make the original document globally readable.

## Other structured Web formats

A future 3e/3.5e Web adapter may translate a site-specific representation into the Rules Core entity representation while preserving source-specific identity/provenance. The translated entity is still package-owned. Canonical matching happens afterward.

Adapters must not invent bibliographic facts merely to satisfy persistence.

## Shared canonical recognition

The shared recognition layer contains no private source body. It may retain identifying evidence necessary to recognize equivalent material across independently imported representations.

### `CanonicalPublication`

Represents a bibliographic publication identity. Publisher, D&D edition, date, title, and identifiers are evidence/projections rather than parents of imported entities.

Publication aliases have two classes: strong globally unique identifiers, such as ISBN, may be unique across canonical publications; contextual identifiers, such as a 5e.tools source code or an acronym such as `PHB`, may identify multiple candidate publications and must be resolved with additional context.

### `CanonicalEntity`

Represents the stable identity of an underlying rule-bearing entity independently of any one imported representation. Multiple private/public source entities may bind to the same canonical entity.

Canonical entities are the durable recognition target required for source-independent Rules Layer behavior.

### `CanonicalSourceOccurrence`

Represents an occurrence of a canonical entity in a canonical publication, including sourcebook/page/locator evidence when known.

One canonical entity may therefore have multiple occurrences/reprints. Distinct revised entities remain distinct canonical entities and can be related explicitly rather than flattened because their names match.

Canonical matching may retain source-specific identifiers, normalized identifying fields, semantic fingerprints, locators, aliases, match method, confidence, and explicit relationships such as reprint/revision/rename/variant where evidence supports them. Ambiguous evidence does not silently merge identities.

## Rules Layer boundary

Rules concepts and adjudications are separate from source ingestion and canonical recognition.

A Rules concept may bind to one or more canonical entities. That binding survives replacement of an import representation because it does not depend on the package-specific source entity ID.

A published/adjudicated decision may still snapshot the exact `SourceEntityRevision` used to produce a reproducible resolved rule. Reading that snapshot remains subject to the selected revision's package access boundary. The canonical binding itself never grants access.

This distinction allows a later independently imported equivalent representation to resolve to the same canonical entity and inherit the relevant Rules concept/adjudication relationship without receiving access to the earlier package.

## Access invariants

All read paths that can expose package-owned source content must enforce package visibility/grants before returning representation data, imported entity content, source search/catalog results, resolved rule bodies, or revision-review/consolidation content.

An inaccessible entity should behave like a missing entity at ordinary read boundaries. Shared canonical APIs may expose non-content identity metadata, but they must not expose stored private source bodies merely because a canonical record exists.

## Refresh and versioning

Web refresh compares upstream/version identity and representation bytes. If selected artifact bytes did not change, no new representation is created. If bytes changed, a new representation snapshot is stored and linked to the previous snapshot for that package/origin.

Entity reconciliation then reuses native source entity identities and creates entity revisions only for changed entity bodies.

Failed initial imports may remove an incomplete ungranted package and its package-owned representations/entities. Cleanup must not delete canonical records still referenced by other imports.

## 3e / 3.5e bootstrap

The eventual 3.x bootstrap is a one-time development process, not a permanent user-facing subsystem.

The persistent pieces are normal source packages/grants/representations/entities/revisions, durable source-specific 3.x translators/parsers useful for later normal imports, the ordinary canonical resolver, and canonical entities/publications/occurrences with supported relationship/evidence records.

A development seeding utility will feed multiple 3.x representations through those same translators and resolver. It may present unresolved groups for developer confirmation of exact identity, reprint, 3.0-to-3.5 revision, rename, legitimate variant, source-data error, parser error, or unresolved cases.

Only the resulting shared canonical identity/matching knowledge persists globally. Imported non-SRD source bodies remain governed by their source packages/grants. Later normal user imports use the ordinary importer and resolver against that seeded identity data.

The complete seeding workbench is not a runtime dependency and is not required in CI. Deterministic fixtures from at least two differently structured 3.x representations are sufficient to verify source-independent resolution during the Source Layer redesign.

## Migration policy

There are no production user source uploads requiring preservation at this stage. The source-related schema may therefore be reset/rebuilt during this redesign. Bundled SRDs and development imports are reproducible.

The migration must not casually destroy unrelated account, campaign, or site data. Source tables and Rules Layer rows that directly reference source entity/revision IDs may require coordinated reset/rebootstrap; that implication must be explicit before deployment.
