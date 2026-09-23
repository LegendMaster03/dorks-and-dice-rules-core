# Source Layer

The Source Layer is the immutable record of imported source material before Dorks & Dice adjudication, merging, or campaign decisions are applied.

The current persistence model is package-native. `SourceWork` and `SourceEdition` are not Source Layer persistence concepts.

## Persistence model

Rules Core stores four different kinds of information separately:

### Access-scoped source material

- `source_package` identifies one imported or bundled package and its distribution metadata.
- `source_content_blob` stores immutable physical artifact bytes once globally, keyed by SHA-256. Byte-identical imports reuse the same blob regardless of account or package.
- `source_representation` stores the package-scoped representation metadata: format, origin identity, file name, source URI when applicable, media type, SHA-256/blob reference, byte length, adapter metadata, import time, and predecessor representation when the same moving origin changes.
- `source_entity` identifies a native entity inside a package by `(package, format, native key)`.
- `source_entity_revision` stores immutable revisions of that native entity. Its raw JSON is the lossless adapter record, not a canonical Dorks & Dice rule document.
- `source_representation_entity` records which entity revision occurred in a particular physical representation.
- `user_source_grant` determines which stable Dorks & Dice user IDs may read a restricted package.

Package access is the authorization boundary for source content. A private entity or revision never becomes public merely because Rules Core recognizes what it is.

### Canonical publication identity

- `canonical_publication` identifies an underlying publication independent of representation format.
- `canonical_publication_alias` stores publication identifiers and aliases with scheme-specific uniqueness rules.
- `source_representation_publication` records which canonical publication a physical representation supplied evidence for.
- publication evidence conflicts retain provenance rather than silently replacing an established value.

A 5e.tools `source` code, corpus `id`, `parentSource`, ISBN, PDF bibliographic field, or PCGen source label is evidence about publication identity; none is a substitute for the physical source representation.

### Canonical entity identity

- `canonical_entity` identifies a rule-bearing entity independently of the package that supplied it.
- `canonical_entity_relationship` records directed semantic relationships such as `revision`, `reprint`, `rename`, and `variant` instead of forcing all related material into one identity.
- `canonical_entity_alias` stores developer-confirmed strong source-lineage aliases. Alias identity includes the source-specific semantic fingerprint so a later mechanical revision of the same upstream key can map to a different canonical entity.
- `canonical_source_occurrence` identifies the occurrence of a canonical entity in one canonical publication.
- `source_entity_occurrence_binding` links an exact source-entity revision to a canonical occurrence and records its source-specific semantic fingerprint, locator, match method, and confidence.
- `source_reconciliation_issue` records canonical reconciliation conflicts against the immutable source representation that produced them. These rows contain recognition metadata only; they do not contain a replacement source body or grant package access.

Canonical identity is global identity metadata, not global source content. **Rules Core may globally know the identity of non-SRD material without globally providing that material.**

### Rules Layer identity

Rules concepts bind to canonical entities rather than directly to private package entities. An exact `source_entity_revision` is still retained where a decision needs reproducibility and provenance.

A directed `revision` relationship extends a concept to later revisions of its bound canonical entity. `variant`, `reprint`, and `rename` relationships do not automatically extend that binding. Runtime resolution still requires an accessible source implementation; canonical identity never bypasses `user_source_grant`.

## Native identity and revisions

A source entity's native identity is defined by its adapter. The database ID is local persistence identity; the adapter's `NativeKey` and `NativeIdentityJson` preserve the source-specific identity needed to recognize the entity again.

`source_entity_revision.fingerprint` is computed from canonicalized **raw adapter JSON**. This makes physical/native source revision history lossless and idempotent:

- reimporting equivalent raw JSON does not create a fake revision;
- changed raw source data creates the next immutable revision;
- the same unchanged revision can be linked to a newer physical representation without being duplicated.

Adapters may also provide `SemanticJson`. That document is used only for canonical mechanical comparison. It does not replace `RawJson`, does not determine the immutable source revision fingerprint, and is not written back into the source material. This distinction lets an adapter retain page numbers, source-path metadata, comments, and other provenance without treating provenance movement as a rules change.

When one native source entity changes mechanically, Rules Core creates or resolves the new canonical entity and records an explicit directed `revision` relationship from the prior canonical entity. A provenance-only revision with unchanged semantic content retains the same canonical entity.

## Lossless 5e.tools ingestion

5e.tools is a native structured representation, not an intermediate schema that must be reduced into work/edition rows.

The adapter preserves the complete entity JSON and native identity fields, including unknown fields. Corpus metadata preserves `id`, `source`, and `parentSource` independently. A child adventure and parent publication can therefore legitimately share one source code without being collapsed.

The known edition markers are translated only at the publication-evidence boundary:

- `classic` -> `5e`
- `one` -> `5.5e`

5e.tools source ingestion does not create a global mirror of the upstream corpus. Public bundled SRDs are app content; non-SRD imports remain package-scoped to the accounts that imported them.

## PDF sources

A readable PDF is retained as its original byte representation. Text extraction produces source fragments with page provenance; canonical matching happens after the representation has been accepted and stored.

A PDF does not need an existing canonical publication, 5e.tools source code, or matching rule concept to be ingestible. Scan-only PDFs still require a future OCR adapter.

## PCGen 3.x sources

PCGen `.pcc` and `.lst` files are native structured 3.x sources.

The PCGen adapter preserves campaign metadata, raw list lines, duplicate and unknown tags, native paths, and line locators. Supported single-line rule families receive semantic projections. Operations such as `.COPY=`, `.MOD`, and `.FORGET`, and unsupported multi-line families, are retained as source evidence rather than guessed into complete rules.

Only artifacts whose actual source URI proves they came from the `PCGen/pcgen` or `PCGen/pcgen-newsources` GitHub repositories are eligible to present trusted PCGen lineage aliases. A local `.lst` upload or another repository using PCGen syntax does not receive that trust merely because its format looks correct.

Strong aliases are not created by name matching. They are registered only after canonical identity has been confirmed by bootstrap/reconciliation evidence. Once registered, a later import from that trusted lineage can reuse the canonical entity ID without gaining access to another user's source package.

## Normal account Add Source workflow

A signed-in Dorks & Dice account uses the normal workflow:

1. Choose **Add Source**.
2. Choose **Upload file** or **Web source**.
3. Supply the file or HTTPS source URL.
4. Rules Core detects compatible representations, imports them into one private package, and grants that account access.

Current adapters include 5e.tools JSON, text-readable PDF, and PCGen `.pcc`/`.lst` data. Compatibility is determined by adapters, not by a blanket extension allowlist.

Uploaded files are immutable snapshots. Web sources are moving registrations and re-enter the same adapter/import pipeline when refreshed.

Canonical reconciliation is downstream of valid source ingestion. If canonical identity evidence conflicts, Rules Core keeps the imported representation and native entity revision, records a reconciliation issue, and exposes that issue only through an owning user's source registration. A corrected retry resolves the active issue across that representation lineage without fabricating another source revision.

GitHub tree imports enumerate compatible files and preserve each fetched file as its own source representation. The current-user package identity is derived from the logical source origin rather than the user ID: accounts importing the same Web source URL share that package, its representations, entities, and revisions, while retaining independent registrations and grants. Identical uploads converge by their content-derived upload origin. Different origins with byte-identical files still reuse the same `source_content_blob`. Large Web adds are resumable across service restarts: each completed representation remains committed, interrupted running jobs are requeued, and retry reuses those deterministic package/origin representations instead of deleting the partial package.

## Web refresh

Moving Web sources keep per-account registrations while version checks may be shared by normalized URL. GitHub trees use commit identity; other HTTP sources use `ETag` and/or `Last-Modified` when available.

The refresh worker is an ASP.NET hosted background service. It performs explicit queued imports and periodic refresh sweeps; it does not use detached fire-and-forget process work.

## Read boundary

The ordinary read endpoints remain source-access scoped:

- `GET /api/sources` lists packages accessible to the current request.
- `GET /api/sources/entities` searches only accessible source entities.
- `GET /api/sources/entities/{entityId}` returns the latest accessible revision and its preserved source document.

Anonymous requests see only public packages. Authenticated requests add restricted packages having a matching `user_source_grant`. A restricted entity without a grant behaves as not found and is omitted from search results.

Canonical publication/entity tables are not an alternate source-content API. Reconciliation issue reads are likewise scoped through the caller's current-user source registration rather than exposed as a global source-content endpoint.

## Source grants, acquisition, and disposition

These axes remain independent:

- source grants authorize reading restricted package content;
- acquisition records describe how the current account says it obtained material and never grant access;
- global source disposition determines whether an otherwise accessible package participates in global Rules Lawyer normalization/automatic adjudication;
- Dorks & Dice roles determine who may use administrative or Rules Lawyer controls.

A future active-source selection layer is deliberately separate from all of these. Selection answers whether an accessible canonical publication participates in the current personal/campaign content context; it does not grant access, change global disposition, or rewrite a published Rules Layer decision. The planned category/default/override model is documented in `source-selection-architecture.md`.

Ignoring a package for global rules does not delete its representations, entities, revisions, grants, or canonical identities. Revoking a grant does not rewrite acquisition history. Canonical recognition does not broaden either authorization boundary.

## Development database policy

During the current pre-production refactor, source/rules persistence is rebuildable. The implementation does not retain `SourceWork`/`SourceEdition` merely to preserve development-only database state. Schema currentization exists to keep feature-branch development databases usable while the branch is under active construction; the target fresh schema is the package/representation/entity/canonical model documented here.