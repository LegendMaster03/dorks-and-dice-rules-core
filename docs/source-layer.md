# Source Layer

The Source Layer is the immutable record of what imported source material says before any Dorks & Dice adjudication, merging, or campaign decision is applied.

## Persistence model

The current implementation stores source representation identities plus independent user grants in PostgreSQL:

1. `source_package` - an imported package and its distribution metadata.
2. `source_work` - a work contained by the package.
3. `source_edition` - an edition/release of that work, including optional game-edition, publication-date, release-kind, and publisher provenance.
4. `source_entity` - a source-specific entity identity within an edition.
5. `source_entity_revision` - immutable JSON revisions of that entity.
6. `user_source_grant` - a stable Dorks & Dice user ID's permission to access a restricted package.

Package, work, and edition keys are normalized at the application boundary. Source entity identity is based on the imported format's source-specific identity. For the current 5e.tools adapter, that is the array/property name plus source code, entity name, and an upstream `uniqueId`/`id` when one is present. The database ID is the local persistent identifier; the natural key and revision fingerprint provide stable representation identity.

Source grants deliberately do not contain global or campaign roles. They answer only whether a specific site identity may access a specific restricted source package.

Acquisition provenance is stored separately from these identities and grants. `source_acquisition` records how the current account says it obtained a package, while `source_acquisition_revocation` marks an acquisition record void without deleting history. Acquisition records never create or remove `user_source_grant`; see `docs/source-acquisitions.md`.

## Canonical publication and occurrence identity

Representation identity is not the same as publication identity. Rules Core maintains a format-neutral identity index above imported source records:

- `canonical_publication` identifies the underlying publication independent of whether it arrived from 5e.tools, an uploaded file, or a future PDF adapter.
- `canonical_publication_alias` records format/provider identifiers such as a 5e.tools source code.
- `canonical_source_occurrence` identifies one rule-bearing occurrence inside one canonical publication.
- `source_entity_occurrence_binding` associates a representation-specific source entity with that canonical occurrence and records the match method, confidence, locator, and semantic fingerprint.

Canonical identity metadata is not source content and does not bypass source grants. Two users can therefore provide different representations that resolve to the same publication or occurrence while the underlying source records remain independently gated.

Exact aliases and exact bibliographic evidence can resolve a publication directly. A representation that lacks a provider-specific alias can also resolve against a unique body of exact semantic occurrence fingerprints. Ambiguous or approximate evidence does not silently merge publications. This is especially important for future PDF/OCR adapters.

Canonical source identity also remains separate from Rules Layer concepts. Recognizing two representations as the same occurrence in the same publication does not decide that another edition, SRD occurrence, or third-party variant is the same Dorks & Dice rule concept. Rules Layer binding/adjudication remains deliberate.

## Publisher provenance

Publisher is first-class provenance when it is available from the source or adapter. It is stored on source-release metadata and propagated into the canonical publication when that canonical publication does not already have publisher metadata.

Publisher conflicts are not overwritten automatically. A conflicting publisher observation is preserved as a reconciliation conflict for review rather than silently changing canonical publication identity. An adapter that can not reliably identify a publisher leaves the value unset; Rules Core does not assume that every source in a mixed repository has the same publisher.

## Lossless 5e.tools ingestion

`ISourceImportService` accepts a 5e.tools-shaped JSON document representing one logical work/release import. Top-level entity arrays are imported without projecting the entity into a fixed application schema. The complete entity object is stored as PostgreSQL `jsonb`, so fields unknown to Rules Core survive ingestion and can be returned later.

Physical upstream data files do not have to match Rules Core's logical work/release boundary one-to-one. Some 5e.tools-shaped distributions aggregate several item-level source codes in one JSON file. Source Administration and the normal Add Source workflow can partition such an aggregate by `source` code before invoking `ISourceImportService`, allowing the same physical document to feed separate work/release imports without rewriting the selected entity objects. An unfiltered mixed-source preview emits a provenance warning. See `docs/source-administration.md`.

Each entity also receives a SHA-256 fingerprint computed from a canonical JSON representation. Object property ordering does not affect the fingerprint. Array ordering remains significant. Reimporting semantically identical JSON is idempotent; changed content creates the next immutable revision instead of updating an existing revision.

Source package/work/edition registration metadata is immutable after first registration under a given key. A subsequent import using the same key with conflicting registration metadata is rejected rather than rewriting source provenance.

## Normal account Add Source workflow

A signed-in Dorks & Dice account uses one normal workflow rather than the administrative provenance controls:

1. Choose **Add Source**.
2. Choose **Upload file** or **Web source**.
3. Select the file or paste the HTTPS URL.
4. Choose **Add source**.

The backend performs compatibility detection, internal source partitioning, import, and the current account's source grant. Users do not have to create package/work/release identities or perform a separate acquisition/grant workflow.

Compatibility is the contract, not JSON. The first implemented file format is compatible 5e.tools JSON, but the Add Source API/UI is intended to accept additional format adapters. An uploaded file that no registered adapter can read is rejected as an invalid source. A Web source can contain unrelated/incompatible files, but it is rejected if no compatible files are found.

Web sources are refreshable registrations. Rules Core checks the upstream version approximately once per 24 hours and does a full pull/re-import only when the upstream identity changed or no reliable version token can be established. GitHub tree sources use the upstream commit identity; other Web sources use available HTTP version metadata. Registrations for the same URL share the version probe. Manual refresh remains available.

Uploaded files are immutable snapshots. Adding a newer local file creates/updates through another explicit upload rather than periodically reading a path on the user's computer.

## Read API boundary

The read endpoints are:

- `GET /api/sources` - lists packages accessible in the current request context.
- `GET /api/sources/entities` - searches source entities the current request context may access.
- `GET /api/sources/entities/{entityId}` - returns the latest accessible source entity revision with package/work/edition provenance and the preserved source document.

Anonymous/direct requests can access only packages explicitly marked public. When a request arrives through the authenticated Dorks & Dice Tool gateway, Rules Core redeems the host-issued ticket to obtain the stable user ID and includes restricted packages having a matching `user_source_grant`.

A restricted entity without a grant returns the same not-found result as a missing entity. Restricted entities are likewise omitted from search results. This prevents the ordinary source API from becoming an oracle for restricted package contents or entity existence.

## Source grants

`ISourceGrantService` provides idempotent grant, revoke, and grant-check operations inside the application boundary. A grant is keyed by `(user_id, source_package_id)` and is deleted when revoked. Package deletion cascades to its grants.

The normal Add Source workflow creates the current account's grant automatically for its restricted imported package. The separate Source Administration grant controls remain an advanced/maintenance surface rather than a required end-user step.

The authenticated Source Administration workflow exposes a deliberately narrow grant-management surface for effective `Dev` users in Dorks & Dice mode:

- `GET /api/source-admin/packages` lists package-level control-plane metadata and whether the current authenticated account has a grant.
- `POST /api/source-admin/packages/{sourcePackageId}/current-user-grant` grants the current authenticated account access to a restricted package.
- `DELETE /api/source-admin/packages/{sourcePackageId}/current-user-grant` revokes that current-account grant.

The browser never supplies the target user ID. Rules Core derives it from the redeemed Tool Host identity. Arbitrary other-user grant mutation remains outside this slice.

## Global Rules Lawyer source disposition

A source can be valid and useful to its uploader while being irrelevant to the shared global ruleset. Global `Rules Lawyer` accounts can therefore mark an entire source package **ignored for global rules**.

Ignoring a package:

- does not delete the package, works, editions, entities, revisions, or canonical identity;
- does not revoke any user's source grant;
- does not remove it from the uploader's Source Library;
- removes its unbound entities from global normalization candidates;
- excludes it from new automatic global cross-edition resolution;
- does not silently rewrite an existing manual decision or published ruleset revision.

The disposition is reversible. Restoring the package makes it eligible for global review again. Package-level disposition is intentional so a Rules Lawyer can suppress a large irrelevant homebrew collection without reviewing thousands of entities individually.

This disposition is a global Rules Layer concern, not a statement that the user's content is invalid. Campaign-specific inclusion/exclusion can remain a separate concern when campaign source policy is built.

## Acquisition provenance

Acquisition history is intentionally distinct from source grants. Effective `Dev` users in Dorks & Dice mode can list, append, and void only the current authenticated account's acquisition records through the Source Administration API.

Recording a physical copy, digital copy, subscription, licensed access, or other acquisition is a self-recorded provenance statement. Rules Core does not currently verify receipts, ownership, subscription status, or external-provider entitlement, so this data is never consumed as an authorization decision.

A void operation preserves the original acquisition and appends a separate revocation record. It does not revoke a source grant. Conversely, source-grant mutation does not alter acquisition history.

## Authorization axes

The authorization boundaries remain independent:

- Dorks & Dice determines whether an identity may administer source imports/provenance or adjudicate/change global or campaign rules.
- Rules Core source grants determine whether that identity may read a restricted source.
- Source acquisition records describe provenance only and are not evidence that grants access.
- Global source disposition determines whether an otherwise accessible package participates in global Rules Lawyer normalization/automatic adjudication; it does not determine source access.

A user can therefore be a Rules Lawyer without access to a restricted source, have access to that source without being a Rules Lawyer, or upload a source that remains useful privately even after a Rules Lawyer ignores it for the global ruleset.

## Administrative import boundary

There is no unauthenticated source-import endpoint. Advanced `POST /api/source-admin/import` remains available only through a redeemed Tool Host identity with effective `Dev` authority while the site is in `dorks-and-dice` mode.

That administrative import authority is a control-plane permission, not an entitlement decision. It coexists with the normal authenticated **Add Source** workflow, which is intentionally narrower and automatically associates the imported restricted material with the signed-in account.

The Source Administration UI exposes package/work/edition provenance, visibility, optional aggregate source-code partitioning, 5e.tools-shaped JSON ingestion, current-account acquisition history, and current-account grant controls while leaving Rules Layer concept creation and adjudication as separate explicit operations. See `docs/source-administration.md` for the administrative workflow.
