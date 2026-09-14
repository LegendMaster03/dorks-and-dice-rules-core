# Source identity conventions

Rules Core separates four concerns that must not be collapsed into one identifier: D&D game edition, imported Source Layer identity, canonical source recognition, and Dorks & Dice Rules Layer concepts.

## Canonical D&D edition labels

Rules Core uses these canonical labels in authoring and resolved provenance:

- `1e`
- `2e`
- `3e`
- `3.5e`
- `4e`
- `5e`
- `5.5e`

For the fifth-edition family, `5e` identifies the rules originally labeled by year as the 2014 rules, while `5.5e` identifies the updated rules originally labeled by year as the 2024 rules. Legacy import labels such as `5e-2014`, `5e 2014`, `2014`, `5e-2024`, `5e 2024`, and `2024` may be normalized at compatibility boundaries, but persisted/resolved edition metadata should use the canonical labels.

Publication year is metadata. It is not itself a D&D edition identity.

## Persisted Source Layer identity

The current persisted Source Layer is:

```text
SourcePackage
  |- SourceRepresentation
  `- SourceEntity -> SourceEntityRevision -> SourceRepresentation
```

There is no persisted `SourceWork` or `SourceEdition` parent in the current model.

`SourcePackage` is the distribution and access boundary. It carries provider, license, visibility, and per-user grants. Independent user imports remain independent packages even when canonical recognition later determines that they represent the same publication.

`SourceRepresentation` is an immutable physical artifact/version. It stores the original bytes, format key, origin identity, optional source URI/media type, content hash, representation metadata, import time, and optional predecessor representation. Two identical reimports of the same origin/content reuse the stored representation rather than fabricating another physical version.

`SourceEntity` is one stable source-native identity within a package and format. `NativeKey` and `NativeIdentityJson` preserve the identity supplied or deterministically derived by the adapter. They must not encode a Dorks & Dice adjudication.

`SourceEntityRevision` is an immutable observed body of the same `SourceEntity`. A changed representation of the same native source identity creates another revision; an unchanged reimport does not. A later publication, a different upstream native identity, or a different rule variant must not be forced into the revision chain merely because its display name matches.

Legacy compatibility APIs still expose fields such as `WorkKey`, `WorkDisplayName`, `EditionKey`, and `EditionDisplayName`. Those values may contribute to artifact origin/provenance and bootstrap catalog organization, but they are not persisted Source Layer parents and must not be treated as cross-format canonical IDs.

## Canonical recognition

Shared recognition metadata is deliberately separate from Source Layer storage and grants:

```text
CanonicalPublication
  `- CanonicalSourceOccurrence -> CanonicalEntity

CanonicalEntityAlias
CanonicalEntityRelationship
```

`CanonicalPublication` identifies a real publication independently of import package, user, file format, or physical representation.

`CanonicalEntity` identifies a rule-bearing entity when Rules Core has sufficient evidence that representation-specific records refer to the same thing.

`CanonicalSourceOccurrence` identifies the occurrence of a canonical entity within a canonical publication. The same canonical entity may therefore occur in multiple publications without collapsing those publication-specific occurrences.

`CanonicalEntityAlias` records strong trusted source-lineage identity. Entity aliases are keyed by alias scheme, native alias value, and source-specific semantic fingerprint. Including the semantic fingerprint permits the same upstream native key to identify a later mechanical revision without collapsing that revision into the old canonical entity.

`CanonicalEntityRelationship` records explicit relationships such as revision or variant history between distinct canonical entities. Relationship direction is predecessor/base to later/related entity.

Canonical records are recognition metadata only. They do not contain a globally readable replacement for a restricted source document and they do not grant package access.

## Canonical publication matching

Adapters emit publication evidence; they do not choose canonical database IDs directly. Matching is conservative.

Current high-level precedence is:

1. exact globally strong bibliographic identifiers, including ISBN schemes;
2. exact normalized bibliographic identity when it resolves unambiguously;
3. source-specific publication aliases such as trusted 5e.tools corpus/book/adventure identifiers when their metadata is compatible;
4. contextual aliases only when additional title/edition/publisher/date context disambiguates them;
5. exact occurrence-fingerprint overlap only when at least three known occurrences identify a unique best publication;
6. otherwise create a new canonical publication rather than guessing.

A bare contextual source code such as `PHB` is not globally unique proof of publication identity. A representation-specific alias remains evidence about a publication; it does not become the canonical identity itself.

Publication evidence may enrich missing canonical metadata. Conflicting established metadata is recorded as evidence conflict with representation provenance rather than silently overwriting the canonical value.

## Canonical entity and occurrence matching

Entity matching distinguishes representation equality from rule identity.

Within a canonical publication, an exact semantic fingerprint can identify an existing occurrence when the match is unique for the entity type and, when supplied, locator. Representation-specific fields such as source code, page encoding, repository path, and JSON property order should not create false mechanical differences when an adapter can safely exclude them through `SemanticJson`.

Different formats can legitimately encode the same rule with different semantic structures. Exact semantic fingerprint equality is therefore not required when a bootstrap-confirmed trusted source-lineage alias establishes exact canonical identity. Name-only similarity is not sufficient for that merge.

For revisions of one source-native entity:

- unchanged semantics inherit the prior canonical entity;
- changed mechanics normally create a distinct canonical entity and an explicit revision relationship;
- a trusted alias can not move an unchanged revision to an unrelated canonical entity;
- a trusted alias can not collapse a mechanically changed revision back into its prior canonical entity;
- conflicting trusted aliases or a trusted alias that contradicts an existing exact semantic occurrence are reconciliation conflicts rather than reasons to corrupt Source Layer identity.

## Reconciliation failure boundary

Source ingestion and canonical recognition are separate validity boundaries.

A valid package, physical representation, source entity, and source revision remain valid even if canonical reconciliation for their publication group produces a recognized identity conflict. Normalized ingestion isolates canonical reconciliation with a transaction savepoint, rolls back only the affected canonical changes, commits the Source Layer import, and returns a `canonical-identity-conflict` reconciliation issue.

The same immutable bytes can be retried after the identity evidence is corrected. The existing representation and source revision are reused rather than creating fake physical versions or revisions.

This recovery behavior is intentionally narrow. Database failures, malformed input, missing integrity dependencies, and immutable package/native-identity violations still fail the import normally.

## PDF identity

PDF support is implemented for documents with a usable text layer. The PDF adapter preserves the original bytes, extracts readable pages as source fragments, and extracts explicit bibliographic evidence such as labeled title/publisher/system/date values and ISBN when present.

Scan-only PDFs are currently rejected because OCR is not implemented. Weak layout cues or approximate text similarity are not used to guess a spell, feat, monster, or canonical identity.

A later richer PDF/OCR extractor can add better source records while continuing to use the same representation and canonical identity boundaries.

## PCGen and trusted source lineage

PCGen syntax by itself is not proof of canonical identity. A local `.pcc` or `.lst` file can use the same syntax as the official PCGen repositories.

Only artifacts whose source URI establishes the configured trusted PCGen lineage may emit the corresponding strong canonical aliases. Even then, bootstrap/reconciliation must first confirm which canonical entity a lineage alias represents. The globally reusable result is identity knowledge, not PCGen source content.

## Access boundary

Canonicalization must never broaden source access. Access is evaluated against `SourcePackage`.

Two representations may point to the same `CanonicalPublication`, `CanonicalEntity`, or `CanonicalSourceOccurrence` while their bytes, source entities, revisions, and grants remain completely separate. A user who supplied representation A does not gain representation B merely because both share canonical recognition.

At runtime, a published rule whose selected revision is inaccessible may substitute another revision that the requesting user can access when both revisions resolve to the same canonical entity. The returned document must come from the requesting user's accessible package, not from the inaccessible snapshot.

## Rules Layer identity

A canonical source entity is still not a Dorks & Dice `RuleConcept`.

Canonical recognition answers whether source records are the same entity or how distinct entities are historically related. The Rules Layer answers which implementations Dorks & Dice considers one conceptual rule and how those implementations are selected, consolidated, modified, or overridden.

This separation permits, for example, a 3e implementation, a 3.5e revision, a later reprint, and a campaign-specific house variation to retain correct source history without forcing any of them into one source identity.

## Unearthed Arcana

Unearthed Arcana is publication/source provenance, not a D&D edition. It has referred to published books as well as playtest/preview material across D&D history. Do not infer `playtest` solely from the name.

Each actual publication receives normal source/canonical identity. Historical relationships among contained entities are represented explicitly rather than inferred from the publication brand.

## Stable-key guidance

Keys should identify source-native or acquisition objects, not encode a Dorks & Dice ruling. Prefer stable values that survive display-name changes.

Examples:

```text
package:        wotc-srd-cc
origin:         bundled:srd-5-1
native key:     spell|SRD51|acid-arrow

package:        user-source-<stable-origin-hash>
origin:         web:<normalized-origin>#path/to/file
native key:     <adapter-defined stable identity>
```

The exact key shape is adapter-specific. The important constraints are stable native identity, explicit physical provenance, canonical recognition independent of package access, and Rules Layer adjudication independent of source identity.
