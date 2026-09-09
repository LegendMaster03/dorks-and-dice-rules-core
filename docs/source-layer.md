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

## Lossless 5e.tools ingestion

`ISourceImportService` accepts a 5e.tools-shaped JSON document. Top-level entity arrays are imported without projecting the entity into a fixed application schema. The complete entity object is stored as PostgreSQL `jsonb`, so fields unknown to Rules Core survive ingestion and can be returned later.

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

The two authorization axes remain independent:

- Dorks & Dice determines whether an identity may administer source imports or adjudicate/change global or campaign rules.
- Rules Core determines whether that identity may read a restricted source.

A user can therefore be a Rules Lawyer without access to a restricted source, have access to that source without being a Rules Lawyer, or be a Dev who can administer package metadata while still lacking the package's source-content grant.

## Import boundary

There is no unauthenticated source-import endpoint. `POST /api/source-admin/import` is available only through a redeemed Tool Host identity with effective `Dev` authority while the site is in `dorks-and-dice` mode.

Import authority is a control-plane permission, not an entitlement decision. Public imports become available according to normal public-source rules. Restricted imports remain unreadable to the importer until a separate grant exists.

The Source Administration UI exposes package/work/edition provenance, visibility, and 5e.tools-shaped JSON ingestion while leaving Rules Layer concept creation and adjudication as separate explicit operations. See `docs/source-administration.md` for the administrative workflow and its current-account grant controls.
