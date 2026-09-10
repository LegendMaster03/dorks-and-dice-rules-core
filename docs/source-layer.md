# Source Layer

The Source Layer is the immutable record of what imported source material says before any Dorks & Dice adjudication, merging, or campaign decision is applied.

## Persistence model

The current implementation stores five source identities plus independent user grants in PostgreSQL:

1. `source_package` - an imported package and its distribution metadata.
2. `source_work` - a work contained by the package.
3. `source_edition` - an edition/release of that work.
4. `source_entity` - a source-specific entity identity within an edition.
5. `source_entity_revision` - immutable JSON revisions of that entity.
6. `user_source_grant` - a stable Dorks & Dice user ID's permission to access a restricted package.

Package, work, and edition keys are normalized at the application boundary. Source entity identity is based on the 5e.tools array/property name plus source code, entity name, and an upstream `uniqueId`/`id` when one is present. The database ID is the local persistent identifier; the natural key and revision fingerprint provide stable content identity.

Source grants deliberately do not contain global or campaign roles. They answer only whether a specific site identity may access a specific restricted source package.

Acquisition provenance is stored separately from these identities and grants. `source_acquisition` records how the current account says it obtained a package, while `source_acquisition_revocation` marks an acquisition record void without deleting history. Acquisition records never create or remove `user_source_grant`; see `docs/source-acquisitions.md`.

## Lossless 5e.tools ingestion

`ISourceImportService` accepts a 5e.tools-shaped JSON document representing one logical work/release import. Top-level entity arrays are imported without projecting the entity into a fixed application schema. The complete entity object is stored as PostgreSQL `jsonb`, so fields unknown to Rules Core survive ingestion and can be returned later.

Physical upstream data files do not have to match Rules Core's logical work/release boundary one-to-one. Some 5e.tools-shaped distributions aggregate several item-level source codes in one JSON file. Source Administration can partition such an aggregate by `source` code before invoking `ISourceImportService`, allowing the same physical document to feed separate work/release imports without rewriting the selected entity objects. An unfiltered mixed-source preview emits a provenance warning. See `docs/source-administration.md`.

Each entity also receives a SHA-256 fingerprint computed from a canonical JSON representation. Object property ordering does not affect the fingerprint. Array ordering remains significant. Reimporting semantically identical JSON is idempotent; changed content creates the next immutable revision instead of updating an existing revision.

Source package/work/edition registration metadata is immutable after first registration under a given key. A subsequent import using the same key with conflicting registration metadata is rejected rather than rewriting source provenance.

## Read API boundary

The read endpoints are:

- `GET /api/sources` - lists packages accessible in the current request context.
- `GET /api/sources/entities` - searches source entities the current request context may access.
- `GET /api/sources/entities/{entityId}` - returns the latest accessible source entity revision with package/work/edition provenance and the preserved source document.

Anonymous/direct requests can access only packages explicitly marked public. When a request arrives through the authenticated Dorks & Dice Tool gateway, Rules Core redeems the host-issued ticket to obtain the stable user ID and includes restricted packages having a matching `user_source_grant`.

A restricted entity without a grant returns the same not-found result as a missing entity. Restricted entities are likewise omitted from search results. This prevents the ordinary source API from becoming an oracle for restricted package contents or entity existence.

## Source grants

`ISourceGrantService` provides idempotent grant, revoke, and grant-check operations inside the application boundary. A grant is keyed by `(user_id, source_package_id)` and is deleted when revoked. Package deletion cascades to its grants.

The authenticated Source Administration workflow exposes a deliberately narrow grant-management surface for effective `Dev` users in Dorks & Dice mode:

- `GET /api/source-admin/packages` lists package-level control-plane metadata and whether the current authenticated account has a grant.
- `POST /api/source-admin/packages/{sourcePackageId}/current-user-grant` grants the current authenticated account access to a restricted package.
- `DELETE /api/source-admin/packages/{sourcePackageId}/current-user-grant` revokes that current-account grant.

The browser never supplies the target user ID. Rules Core derives it from the redeemed Tool Host identity. This preserves a narrow first implementation: a Dev can explicitly change only the signed-in account's restricted-source entitlement. Public packages reject grant mutation because they require no entitlement. Arbitrary other-user grant mutation remains outside this slice.

Importing a restricted package does not grant the importing Dev access. Granting access remains a separate explicit operation, so Dev authority itself still does not imply restricted source-content access.

## Acquisition provenance

Acquisition history is intentionally distinct from source grants. Effective `Dev` users in Dorks & Dice mode can list, append, and void only the current authenticated account's acquisition records through the Source Administration API.

Recording a physical copy, digital copy, subscription, licensed access, or other acquisition is a self-recorded provenance statement. Rules Core does not currently verify receipts, ownership, subscription status, or external-provider entitlement, so this data is never consumed as an authorization decision.

A void operation preserves the original acquisition and appends a separate revocation record. It does not revoke a source grant. Conversely, source-grant mutation does not alter acquisition history.

## Authorization axes

The authorization boundaries remain independent:

- Dorks & Dice determines whether an identity may administer source imports/provenance or adjudicate/change global or campaign rules.
- Rules Core `user_source_grant` determines whether that identity may read a restricted source.
- Source acquisition records describe provenance only and are not evidence that grants access.

A user can therefore be a Rules Lawyer without access to a restricted source, have access to that source without being a Rules Lawyer, or be a Dev who can administer package/provenance metadata while still lacking the package's source-content grant.

## Import boundary

There is no unauthenticated source-import endpoint. `POST /api/source-admin/import` is available only through a redeemed Tool Host identity with effective `Dev` authority while the site is in `dorks-and-dice` mode.

Import authority is a control-plane permission, not an entitlement decision. Public imports become available according to normal public-source rules. Restricted imports remain unreadable to the importer until a separate grant exists.

The Source Administration UI exposes package/work/edition provenance, visibility, optional aggregate source-code partitioning, 5e.tools-shaped JSON ingestion, current-account acquisition history, and current-account grant controls while leaving Rules Layer concept creation and adjudication as separate explicit operations. See `docs/source-administration.md` for the administrative workflow.
